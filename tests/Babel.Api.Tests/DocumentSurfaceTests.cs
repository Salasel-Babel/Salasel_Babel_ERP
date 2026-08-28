using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>سطح المستندات عبر السلك</b> — دورة الفاتورة كاملةً من خارج العملية.
/// <para>
/// وما تُثبته هذه المجموعة ليس «الوحدة تعمل» — لذلك مجموعتاها الخاصّتان — بل أن
/// <b>ما تفعله الوحدة يمكن طلبه من الشبكة</b>: باعتماد وعنوان وعقد منشور، بلا مرجع
/// مشروع وبلا معرفة بأي نوع داخلي. وذلك هو الفرق كلّه بين منتج يعمل من اختباراته
/// ومنتج يعمل من واجهته.
/// </para>
/// </summary>
public sealed class DocumentSurfaceTests
{
    private const string March = "2026-03-10";

    [Fact]
    public async Task دورة_الفاتورة_كاملةً_من_الشبكة_تنتهي_بقيدٍ_في_الدفتر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        // ── ١ · عميل ────────────────────────────────────────────────────────
        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenA);

        // ── ٢ · فاتورة مسوّدة: لا قيد ولا أثر ───────────────────────────────
        using HttpResponseMessage drafted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SalesInvoices(company), ApiFixture.TokenA,
            Documents.Invoice(Documents.Number("INV"), customerId)));

        (string draftText, JsonElement draft) = await Http.BodyAsync(drafted);
        Console.WriteLine("المسوّدة: " + draftText);

        Assert.Equal(HttpStatusCode.Created, drafted.StatusCode);
        Assert.Equal("DRAFT", draft.GetProperty("state").GetString());
        Assert.Equal(JsonValueKind.Null, draft.GetProperty("entryId").ValueKind);
        Assert.False(draft.GetProperty("alreadyPosted").GetBoolean());

        // المجاميع تصل **محسوبة من الوحدة** ونصّاً: 10 × 100 = 1000، وضريبة 15٪ = 150.
        Assert.Equal("1000.0000", draft.GetProperty("net").GetString());
        Assert.Equal("150.0000", draft.GetProperty("tax").GetString());
        Assert.Equal("1150.0000", draft.GetProperty("gross").GetString());

        string invoiceId = draft.GetProperty("id").GetString()!;

        // و‏Location يوجّه إلى مورد القراءة، فلا يركّب العميل مساراً بيده.
        Assert.Equal(Documents.SalesInvoice(company, invoiceId), drafted.Headers.Location?.OriginalString);

        // ── ٣ · القراءة تُعيد المسوّدة نفسها ────────────────────────────────
        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Documents.SalesInvoice(company, invoiceId), ApiFixture.TokenA));

        (_, JsonElement readBody) = await Http.BodyAsync(read);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("DRAFT", readBody.GetProperty("state").GetString());

        // ── ٤ · الترحيل: 201 وقيدٌ في الدفتر ────────────────────────────────
        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SalesInvoicePosting(company, invoiceId), ApiFixture.TokenA));

        (string postedText, JsonElement postedBody) = await Http.BodyAsync(posted);
        Console.WriteLine("الترحيل: " + postedText);

        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);
        Assert.Equal("POSTED", postedBody.GetProperty("state").GetString());
        Assert.False(postedBody.GetProperty("alreadyPosted").GetBoolean());

        string entryId = postedBody.GetProperty("entryId").GetString()!;
        Assert.NotEqual(Guid.Empty, Guid.Parse(entryId));

        // ── ٥ · إشعار دائن على فاتورة مُرحَّلة ──────────────────────────────
        using HttpResponseMessage note = await api.Call(Http.Request(
            HttpMethod.Post, Documents.CreditNotes(company), ApiFixture.TokenA,
            Documents.CreditNote(Documents.Number("CN"), invoiceId)));

        (string noteText, JsonElement noteBody) = await Http.BodyAsync(note);
        Console.WriteLine("الإشعار: " + noteText);

        Assert.Equal(HttpStatusCode.Created, note.StatusCode);
        Assert.Equal("DRAFT", noteBody.GetProperty("state").GetString());
        Assert.Equal("115.0000", noteBody.GetProperty("gross").GetString());

        using HttpResponseMessage notePosted = await api.Call(Http.Request(
            HttpMethod.Post,
            Documents.CreditNotePosting(company, noteBody.GetProperty("id").GetString()!),
            ApiFixture.TokenA));

        (string notePostedText, JsonElement notePostedBody) = await Http.BodyAsync(notePosted);
        Console.WriteLine("ترحيل الإشعار: " + notePostedText);

        Assert.Equal(HttpStatusCode.Created, notePosted.StatusCode);
        Assert.Equal("POSTED", notePostedBody.GetProperty("state").GetString());
        Assert.NotEqual(entryId, notePostedBody.GetProperty("entryId").GetString());
    }

    [Fact]
    public async Task ترحيل_الفاتورة_مرّتين_يُعيد_القيد_ذاته_ويُعلن_أنه_مُرحَّل_من_قبل()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenA);
        string invoiceId = await Documents.DraftInvoiceAsync(api, company, ApiFixture.TokenA, customerId);

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SalesInvoicePosting(company, invoiceId), ApiFixture.TokenA));
        (string firstText, JsonElement firstBody) = await Http.BodyAsync(first);

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SalesInvoicePosting(company, invoiceId), ApiFixture.TokenA));
        (string secondText, JsonElement secondBody) = await Http.BodyAsync(second);

        Console.WriteLine("الأول : " + firstText);
        Console.WriteLine("الثاني: " + secondText);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.False(firstBody.GetProperty("alreadyPosted").GetBoolean());

        // ‏200 لا 201، و«مُرحَّل من قبل» **معلَن في الجسم أيضاً**: رمز الحالة وحده يضيع
        // خلف أي وسيط يعيد التوجيه، وعميلٌ أعاد المحاولة بعد انقطاع شبكة يحتاج جواباً.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(secondBody.GetProperty("alreadyPosted").GetBoolean());

        // والقيد **هو هو**: لا قيد ثانٍ، ولا معرّف ثانٍ.
        Assert.Equal(firstBody.GetProperty("entryId").GetString(), secondBody.GetProperty("entryId").GetString());
    }

    [Fact]
    public async Task دورة_فاتورة_المورد_من_الشبكة_وحصانتها_بالشكل_نفسه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string supplierId = await Documents.AddSupplierAsync(api, company, ApiFixture.TokenA);
        string costCenter = await Documents.DefaultCostCenterAsync(api, company, ApiFixture.TokenA);

        using HttpResponseMessage drafted = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SupplierBills(company), ApiFixture.TokenA,
            Documents.ExpenseBill(Documents.Number("EXP"), supplierId, costCenter)));

        (string draftText, JsonElement draft) = await Http.BodyAsync(drafted);
        Console.WriteLine("مسوّدة المورد: " + draftText);

        Assert.Equal(HttpStatusCode.Created, drafted.StatusCode);
        Assert.Equal("DRAFT", draft.GetProperty("state").GetString());
        Assert.Equal("500.0000", draft.GetProperty("net").GetString());

        string billId = draft.GetProperty("id").GetString()!;

        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Documents.SupplierBill(company, billId), ApiFixture.TokenA));
        (_, JsonElement readBody) = await Http.BodyAsync(read);
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        Assert.Equal("DRAFT", readBody.GetProperty("state").GetString());

        using HttpResponseMessage first = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SupplierBillPosting(company, billId), ApiFixture.TokenA));
        (string firstText, JsonElement firstBody) = await Http.BodyAsync(first);
        Console.WriteLine("ترحيل المورد: " + firstText);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.Equal("POSTED", firstBody.GetProperty("state").GetString());
        Assert.False(firstBody.GetProperty("alreadyPosted").GetBoolean());

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SupplierBillPosting(company, billId), ApiFixture.TokenA));
        (_, JsonElement secondBody) = await Http.BodyAsync(second);

        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(secondBody.GetProperty("alreadyPosted").GetBoolean());
        Assert.Equal(firstBody.GetProperty("entryId").GetString(), secondBody.GetProperty("entryId").GetString());
    }

    [Fact]
    public async Task أعمار_الذمم_تُقرأ_من_الطرفين_بالشكل_نفسه_والمجموع_مجموع_شرائحه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        foreach (string path in new[]
                 {
                     Documents.ReceivablesAging(company, "2026-03-31"),
                     Documents.PayablesAging(company, "2026-03-31"),
                 })
        {
            using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Get, path, ApiFixture.TokenA));
            (string text, JsonElement report) = await Http.BodyAsync(response);
            Console.WriteLine(path + " → " + text[..Math.Min(240, text.Length)]);

            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
            Assert.Equal("2026-03-31", report.GetProperty("asOf").GetString());

            // الشكل واحد للطرفين: نفس أسماء الشرائح ونفس ترتيبها ونفس تمثيل المال.
            JsonElement totals = report.GetProperty("totals");
            foreach (string band in new[] { "notDue", "days1To30", "days31To60", "days61To90", "over90", "total" })
            {
                string value = totals.GetProperty(band).GetString()!;
                Assert.Matches(@"^-?(0|[1-9][0-9]*)\.[0-9]{4}$", value);
            }
        }
    }

    [Fact]
    public async Task وسيط_التاريخ_مفقوداً_أو_بأرقام_غير_لاتينية_يُرفض_عند_الحدّ()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        using HttpResponseMessage missing = await api.Call(Http.Request(
            HttpMethod.Get,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/receivables-aging"),
            ApiFixture.TokenA));
        (_, JsonElement missingProblem) = await Http.BodyAsync(missing);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("wire.query.missing", Http.CodeOf(missingProblem));

        // ‏«٢٠٢٦-٠٣-٣١» بأرقام عربية-هندية: تُرفض ولا تُطبَّع صامتاً — التطبيع يجعل
        // العميل يظنّ أنه أرسل ما لم يصل (فخ-25).
        using HttpResponseMessage arabic = await api.Call(Http.Request(
            HttpMethod.Get, Documents.ReceivablesAging(company, "٢٠٢٦-٠٣-٣١"), ApiFixture.TokenA));
        (_, JsonElement arabicProblem) = await Http.BodyAsync(arabic);

        Assert.Equal(HttpStatusCode.BadRequest, arabic.StatusCode);
        Assert.Equal("wire.date.non_latin_digits", Http.CodeOf(arabicProblem));
    }

    [Fact]
    public async Task المبلغ_رمزاً_رقمياً_في_سطر_فاتورة_يُرفض_به_الطلب_كلّه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;
        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenA);

        // ‏unitPrice بلا اقتباس: رمز رقمي في حقل مالي — القناة نفسها تُرفض.
        string body = Documents.Invoice(Documents.Number("INV"), customerId).Replace(
            "\"unitPrice\":\"100.0000\"", "\"unitPrice\":100.0000", StringComparison.Ordinal);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SalesInvoices(company), ApiFixture.TokenA, body));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.money.number_token", Http.CodeOf(problem));
    }

    [Fact]
    public async Task خانة_عشرية_خامسة_في_سعر_الوحدة_تُرفض_ولا_تُقرَّب()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;
        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenA);

        string body = Documents.Invoice(Documents.Number("INV"), customerId).Replace(
            "\"unitPrice\":\"100.0000\"", "\"unitPrice\":\"100.00001\"", StringComparison.Ordinal);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SalesInvoices(company), ApiFixture.TokenA, body));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.number.scale_exceeded", Http.CodeOf(problem));
    }

    [Fact]
    public async Task حقل_غير_معروف_في_جسم_المستند_يُفشل_الطلب_ولا_يُتجاهل_صامتاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;
        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenA);

        string body = Documents.Invoice(Documents.Number("INV"), customerId)
            .Replace("{\"number\"", "{\"tenantId\":\"11111111-1111-4111-8111-111111111111\",\"number\"", StringComparison.Ordinal);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Documents.SalesInvoices(company), ApiFixture.TokenA, body));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.body.malformed", Http.CodeOf(problem));
    }

    [Fact]
    public async Task رقم_التسجيل_الضريبي_على_العميل_يُرفض_ولا_يُبتلع()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string body = """
            {"code":"CUST-VAT","name":{"ar":"عميل","en":"Customer"},
             "creditLimit":"1000.0000","paymentTermsDays":30,"vatNumber":"300000000000003"}
            """;

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Documents.Customers(company), ApiFixture.TokenA, body));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        // العقد المنشور لا يحمل vatNumber على CustomerRequest، والخادم يرفضه صراحةً:
        // التجاهل الصامت يجعل المُرسِل يظنّ أنه سجّل رقماً لم يصل.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.field.not_on_this_resource", Http.CodeOf(problem));
    }

    [Fact]
    public async Task مستند_لا_وجود_له_يُرفض_بـ404_داخل_النطاق_نفسه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;
        string stranger = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Documents.SalesInvoice(company, stranger), ApiFixture.TokenA));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("sales.document_not_found", Http.CodeOf(problem));
    }

    [Fact]
    public async Task لا_فعل_تعديل_ولا_حذف_على_مستند_واحد_من_هذا_السطح()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;
        string customerId = await Documents.AddCustomerAsync(api, company, ApiFixture.TokenA);
        string invoiceId = await Documents.DraftInvoiceAsync(api, company, ApiFixture.TokenA, customerId);

        // والمستند **مسوّدة** هنا لا واقعة مُرحَّلة: أي أن الغياب ليس «لأنه مُرحَّل»،
        // بل لأن السطح لا يحمل هذه الأفعال أصلاً.
        foreach (HttpMethod method in new[] { HttpMethod.Put, HttpMethod.Patch, HttpMethod.Delete })
        {
            using HttpResponseMessage response = await api.Call(Http.Request(
                method, Documents.SalesInvoice(company, invoiceId), ApiFixture.TokenA, "{}"));

            Console.WriteLine($"{method}: {(int)response.StatusCode}");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, response.StatusCode);
        }
    }
}
