using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الاشتراك المنقضي يقرأ ولا يكتب — على سطح المستندات نفسه، ومن الشبكة.</b>
/// <para>
/// وهذا ما تعنيه <c>ReadOnly</c> في ADR-0034: <b>لا يُنزَع سجلّ محاسبي أبداً</b>. من
/// توقّف عن الدفع يبقى قادراً على قراءة فواتيره وأعمار ذممه وطباعة ما يحتاجه لإقراره،
/// ويُغلق عليه باب إنشاء مستند جديد وترحيله. والفارق يُقرأ من <b>الرمز</b> لا من نصّ
/// الرسالة: <c>entitlement.read_only</c>.
/// </para>
/// <para>
/// <b>ولا آلية تصريح ثانية في السطح:</b> خدمات الوحدتين تنادي <c>IEntitlementEnforcer</c>
/// بأنفسها أوّل شيء، والسطح <b>يترجم</b> رفضها إلى 403 برمزه. آليتان متوازيتان تعني أن
/// إحداهما تُصان وتُنسى الأخرى.
/// </para>
/// </summary>
public sealed class DocumentEntitlementTests
{
    /// <summary>مسارات <b>القراءة</b> التي يجب أن تبقى مفتوحة لاشتراكٍ منقضٍ.</summary>
    public static TheoryData<string> ReadPaths => new(
    [
        Documents.ReceivablesAging(ApiTestDatabase.CompanyC, "2026-03-31"),
        Documents.PayablesAging(ApiTestDatabase.CompanyC, "2026-03-31"),
    ]);

    [Theory]
    [MemberData(nameof(ReadPaths))]
    public async Task اشتراكٌ_منقضٍ_يقرأ_ذممه_كاملةً(string path)
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Get, path, ApiFixture.TokenC));
        (string text, _) = await Http.BodyAsync(response);
        Console.WriteLine(path + " → " + (int)response.StatusCode);

        Assert.True(response.StatusCode == HttpStatusCode.OK, text);
    }

    [Fact]
    public async Task اشتراكٌ_منقضٍ_يقرأ_مستنداً_قائماً_ولا_يُنشئ_جديداً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // القراءة تعمل: مستندٌ لا وجود له يُجاب بـ404 — أي أن الطلب **بلغ الوحدة**
        // ولم يُردّ عند بوّابة الاستحقاق. ولو كانت القراءة مقطوعة لجاء 403 قبل ذلك.
        string stranger = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);

        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Documents.SalesInvoice(ApiTestDatabase.CompanyC, stranger), ApiFixture.TokenC));

        (string readText, JsonElement readProblem) = await Http.BodyAsync(read);
        Console.WriteLine("القراءة: " + readText);

        Assert.Equal(HttpStatusCode.NotFound, read.StatusCode);
        Assert.Equal("sales.document_not_found", Http.CodeOf(readProblem));
    }

    /// <summary>كل مسارات <b>الكتابة</b> على سطح المستندات، ومعها حمولاتها.</summary>
    public static TheoryData<string, string?> WritePaths => new()
    {
        { Documents.Customers(ApiTestDatabase.CompanyC), Documents.Customer("RO-CUST") },
        { Documents.Suppliers(ApiTestDatabase.CompanyC), Documents.Supplier("RO-SUPP") },
        {
            Documents.SalesInvoices(ApiTestDatabase.CompanyC),
            Documents.Invoice("RO-INV", "11111111-1111-4111-8111-111111111111")
        },
        {
            Documents.CreditNotes(ApiTestDatabase.CompanyC),
            Documents.CreditNote("RO-CN", "11111111-1111-4111-8111-111111111111")
        },
        {
            Documents.SupplierBills(ApiTestDatabase.CompanyC),
            Documents.ExpenseBill("RO-EXP", "11111111-1111-4111-8111-111111111111", "main")
        },
        {
            Documents.SalesInvoicePosting(ApiTestDatabase.CompanyC, "11111111-1111-4111-8111-111111111111"),
            null
        },
        {
            Documents.CreditNotePosting(ApiTestDatabase.CompanyC, "11111111-1111-4111-8111-111111111111"),
            null
        },
        {
            Documents.SupplierBillPosting(ApiTestDatabase.CompanyC, "11111111-1111-4111-8111-111111111111"),
            null
        },
    };

    [Theory]
    [MemberData(nameof(WritePaths))]
    public async Task اشتراكٌ_منقضٍ_لا_يكتب_ولا_يرحّل_من_أي_باب(string path, string? body)
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Post, path, ApiFixture.TokenC, body));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(path + " → " + text);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("entitlement.read_only", Http.CodeOf(problem));

        // الرسالتان معاً — والمحاسب يقرأ بالعربية.
        Assert.Contains("للقراءة فقط", problem.GetProperty("detailAr").GetString()!, StringComparison.Ordinal);
        Assert.Contains("read-only", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task الرفض_الشكلي_يقع_قبل_الاستحقاق_ولا_يُفرَّق_به_بين_مستأجرَين()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // جسمٌ **معطوب شكلياً** (‏creditLimit رمزاً رقمياً): يُرفض عند الحدّ قبل أن
        // تُنادى الوحدة — فالاستحقاق يُنفَّذ **داخل الخدمة** لا في السطح، عمداً، لئلّا
        // تكون في المستودع آليتا تصريح متوازيتان (القاعدة 6).
        const string malformed =
            """{"code":"X","name":{"ar":"س","en":"X"},"creditLimit":1000,"paymentTermsDays":30}""";

        // و**هذا هو ما يُثبَت هنا نفياً:** الجواب واحد على المستأجرَين — المستحِقّ
        // والمنقضي — فلا يستطيع أحد أن يقرأ من شكل الرفض أيّ الوحدات مشتراة عند
        // جاره. ولو اختلف الرفضان لصار الحدّ الشكلي **عدّاد استحقاق**.
        using HttpResponseMessage entitled = await api.Call(Http.Request(
            HttpMethod.Post, Documents.Customers(ApiTestDatabase.CompanyA), ApiFixture.TokenA, malformed));
        using HttpResponseMessage lapsed = await api.Call(Http.Request(
            HttpMethod.Post, Documents.Customers(ApiTestDatabase.CompanyC), ApiFixture.TokenC, malformed));

        (string entitledText, JsonElement entitledProblem) = await Http.BodyAsync(entitled);
        (string lapsedText, JsonElement lapsedProblem) = await Http.BodyAsync(lapsed);
        Console.WriteLine("المستحِقّ : " + entitledText);
        Console.WriteLine("المنقضي  : " + lapsedText);

        Assert.Equal(HttpStatusCode.BadRequest, entitled.StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, lapsed.StatusCode);
        Assert.Equal("wire.money.number_token", Http.CodeOf(entitledProblem));
        Assert.Equal(Http.CodeOf(entitledProblem), Http.CodeOf(lapsedProblem));
    }
}
