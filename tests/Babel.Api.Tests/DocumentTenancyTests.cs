using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>سطح المستندات لا يعبر بين المستأجرين — والإثبات نفيٌ لا إيجاب.</b>
/// <para>
/// وإثبات «أ يقرأ فاتورته» لا يُثبت شيئاً عن التعدّد؛ ما يجب أن يُثبت هو أن <b>ب لا
/// يبلغ فاتورة أ بأي فعل</b>، وأن الرفض <b>لا يفرّق</b> بين شركة موجودة لا يبلغها وشركة
/// لا وجود لها — وإلّا صار السطح عدّاد وجود لشركات مستأجرين آخرين.
/// </para>
/// </summary>
public sealed class DocumentTenancyTests
{
    [Fact]
    public async Task مستأجرٌ_آخر_لا_يبلغ_مستندات_شركةٍ_ليست_له_بأي_فعل()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid a = ApiTestDatabase.CompanyA;

        // «أ» يُنشئ فاتورةً حقيقية في شركته — فالمورد **موجود فعلاً**، والرفض التالي
        // ليس رفض «غير موجود» متنكّراً.
        string customerId = await Documents.AddCustomerAsync(api, a, ApiFixture.TokenA);
        string invoiceId = await Documents.DraftInvoiceAsync(api, a, ApiFixture.TokenA, customerId);

        (HttpMethod Method, string Path, string? Body)[] attempts =
        [
            (HttpMethod.Get, Documents.SalesInvoice(a, invoiceId), null),
            (HttpMethod.Post, Documents.SalesInvoicePosting(a, invoiceId), null),
            (HttpMethod.Get, Documents.Customer(a, customerId), null),
            (HttpMethod.Post, Documents.Customers(a), Documents.Customer("X-CUST")),
            (HttpMethod.Post, Documents.SalesInvoices(a), Documents.Invoice("X-INV", customerId)),
            (HttpMethod.Post, Documents.CreditNotes(a), Documents.CreditNote("X-CN", invoiceId)),
            (HttpMethod.Post, Documents.Suppliers(a), Documents.Supplier("X-SUPP")),
            (HttpMethod.Post, Documents.SupplierBills(a), Documents.ExpenseBill("X-EXP", customerId, "main")),
            (HttpMethod.Get, Documents.ReceivablesAging(a, "2026-03-31"), null),
            (HttpMethod.Get, Documents.PayablesAging(a, "2026-03-31"), null),
        ];

        foreach ((HttpMethod method, string path, string? body) in attempts)
        {
            using HttpResponseMessage response = await api.Call(
                Http.Request(method, path, ApiFixture.TokenB, body));

            (string text, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"{method} {path} → {(int)response.StatusCode} {Http.CodeOf(problem)}");

            Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
            Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));

            // ولا يعبر معرّف المستند ولا اسم العميل ولا أي شذرة من بيانات «أ».
            //
            // و‏instance يُستثنى قبل الفحص لأنه **مسار الطلب نفسه**: معلومة أرسلها
            // المستدعي، لا معلومة خادم. وفحصٌ لا يستثنيه يسقط على معرّفٍ كتبه المهاجم
            // بيده — فيبدو أنه أمسك تسريباً وهو يقرأ صدى طلبه.
            string leaked = text.Replace(
                problem.GetProperty("instance").GetString()!, "{instance}", StringComparison.Ordinal);

            // و‏instance مسارٌ بلا وسائط استعلام: الوسائط بيانات مستدعٍ قد تحمل ما لا
            // يجوز أن يُردَّد في جسم خطأ يُسجَّل، والحدّ يُلقيها عمداً.
            Assert.Equal(path.Split('?')[0], problem.GetProperty("instance").GetString());
            Assert.DoesNotContain(invoiceId, leaked, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain(customerId, leaked, StringComparison.OrdinalIgnoreCase);
        }

        // والفاتورة باقية على حالها: لا نداء من «ب» غيّر شيئاً.
        using HttpResponseMessage after = await api.Call(Http.Request(
            HttpMethod.Get, Documents.SalesInvoice(a, invoiceId), ApiFixture.TokenA));
        (_, JsonElement stillDraft) = await Http.BodyAsync(after);

        Assert.Equal(HttpStatusCode.OK, after.StatusCode);
        Assert.Equal("DRAFT", stillDraft.GetProperty("state").GetString());
    }

    [Fact]
    public async Task شركةٌ_لا_وجود_لها_وشركةٌ_لا_يبلغها_تُرفضان_بالجواب_نفسه_حرفاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // شركةٌ **موجودة** لا يبلغها الاعتماد، وشركةٌ **مختلَقة** لا وجود لها.
        Guid unreachable = ApiTestDatabase.CompanyA;
        Guid nonexistent = Guid.CreateVersion7();

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Get, Documents.ReceivablesAging(unreachable, "2026-03-31"), ApiFixture.TokenB));
        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Get, Documents.ReceivablesAging(nonexistent, "2026-03-31"), ApiFixture.TokenB));

        (string firstText, JsonElement firstProblem) = await Http.BodyAsync(first);
        (string secondText, JsonElement secondProblem) = await Http.BodyAsync(second);
        Console.WriteLine("لا يبلغها : " + firstText);
        Console.WriteLine("لا وجود لها: " + secondText);

        Assert.Equal(HttpStatusCode.Forbidden, first.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, second.StatusCode);
        Assert.Equal(Http.CodeOf(firstProblem), Http.CodeOf(secondProblem));

        // ونصّاً بنصّ بعد إزالة ما يختلف بالضرورة: المسار ومعرّف التتبّع.
        Assert.Equal(Normalise(firstText, unreachable, firstProblem), Normalise(secondText, nonexistent, secondProblem));
    }

    private static string Normalise(string body, Guid company, JsonElement problem) => body
        .Replace(company.ToString("D", CultureInfo.InvariantCulture), "{company}", StringComparison.OrdinalIgnoreCase)
        .Replace(problem.GetProperty("traceId").GetString()!, "{trace}", StringComparison.Ordinal);
}
