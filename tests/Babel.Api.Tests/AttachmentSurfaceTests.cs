using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>سطح المرفقات: الدورة كاملةً على الخادم المبنيّ، من خارج العملية.</b>
/// <para>
/// وما يُثبت هنا ليس «الباب يعمل» بل <b>شكل الباب</b>: أن النوع يأتي من البايتات لا من
/// الإعلان، وأن التصحيح إصدارٌ يشير إلى سلفه ولا يتفرّع، وأن السحب علامةٌ لا محو،
/// وأن الاسم يُطهَّر ولا يبني مساراً، وأن الحدّ يُفرض عند الحدّ برمز 413 بجسم مشكلة
/// لا باستثناء.
/// </para>
/// </summary>
public sealed class AttachmentSurfaceTests
{
    [Fact]
    public async Task الإيداع_يردّ_البصمة_والحجم_والنوع_المسنون_ولا_يذكر_مفتاح_الكائن()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        byte[] bytes = Attachments.Jpeg(1024);

        using HttpResponseMessage response = await api.Call(Attachments.Deposit(
            Attachments.Root(ApiTestDatabase.CompanyA), ApiFixture.TokenA, bytes));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Console.WriteLine("إيداع → " + (int)response.StatusCode + " " + text);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Equal(bytes.Length, body.GetProperty("byteLength").GetInt64());
        Assert.Equal("image/jpeg", body.GetProperty("mediaType").GetString());
        Assert.Equal(Digest(bytes), body.GetProperty("contentHash").GetString());
        Assert.Equal(1, body.GetProperty("version").GetInt32());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("supersedes").ValueKind);
        Assert.Equal(JsonValueKind.Null, body.GetProperty("withdrawal").ValueKind);

        // ‏Location يشير إلى المورد نفسه، و‏contentPath إلى بابه الثنائي.
        Assert.Equal(
            Attachments.One(ApiTestDatabase.CompanyA, Attachments.IdOf(body)),
            response.Headers.Location!.ToString());
        Assert.EndsWith("/content", body.GetProperty("contentPath").GetString()!, StringComparison.Ordinal);

        // **ولا مفتاح كائن يعبر**: هو مسارٌ فيزيائي يعيش في القاعدة وحدها (ADR-0046 §5).
        Assert.DoesNotContain("objectKey", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task النوع_يأتي_من_البايتات_فإعلانٌ_يخالفها_رفضٌ_باسمه_لا_تصحيحٌ_صامت()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // بايتات PDF مُعلَنةً image/jpeg — وهو الشكل الذي يمرّ من كل فحص يقرأ الإعلان.
        using HttpResponseMessage response = await api.Call(Attachments.Deposit(
            Attachments.Root(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Attachments.Pdf(),
            declaredFileName: "فاتورة.jpg",
            declaredMediaType: "image/jpeg"));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine("إعلان مخالف → " + (int)response.StatusCode + " " + text);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("storage.declared_type_mismatch", Http.CodeOf(problem));
        Assert.Contains("application/pdf", problem.GetProperty("detailAr").GetString()!, StringComparison.Ordinal);
        Assert.Contains("application/pdf", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task بايتاتٌ_لا_تُتعرَّف_تُرفض_ولا_تُخزَّن_بنوع_محايد()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Attachments.Deposit(
            Attachments.Root(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            "MZ\u0090\u0000\u0003"u8.ToArray(),
            declaredFileName: "فاتورة.jpg",
            declaredMediaType: null));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, response.StatusCode);
        Assert.Equal("storage.content_not_recognised", Http.CodeOf(problem));
    }

    [Theory]
    [InlineData("../../etc/passwd", "etcpasswd.jpg")]
    [InlineData("..\\..\\windows\\system32\\cmd", "windowssystem32cmd.jpg")]
    [InlineData("sub/dir/فاتورة.jpg", "subdirفاتورة.jpg")]
    [InlineData("C:\\Users\\a\\فاتورة.jpg", "CUsersaفاتورة.jpg")]
    [InlineData("فاتورة\u202Egpj.exe", "فاتورةgpj.jpg")]
    [InlineData("CON", "CON.jpg")]
    [InlineData("NUL.jpg", "NUL.jpg")]
    public async Task الاسم_يُطهَّر_بقائمة_سماح_ولا_يشارك_في_بناء_مسار(string declared, string expected)
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        JsonElement body = await Attachments.DepositAsync(
            api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, Attachments.Jpeg(), declaredFileName: declared);

        string stored = body.GetProperty("fileName").GetString()!;
        Console.WriteLine("«" + declared + "» ⇒ «" + stored + "»");

        Assert.Equal(expected, stored);

        // **ولا فاصل مسار ولا محرف اتجاهي يبقى** — لا في الاسم ولا في أي ترويسة تُبنى منه.
        Assert.DoesNotContain("/", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("\\", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("\u202E", stored, StringComparison.Ordinal);
        Assert.DoesNotContain("..", stored, StringComparison.Ordinal);

        // **والامتداد من البايتات لا من الاسم**: اسمٌ ينتهي بـ.exe وبايتاته JPEG يُحفظ jpg.
        Assert.EndsWith(".jpg", stored, StringComparison.Ordinal);
    }

    [Fact]
    public async Task حدُّ_الحجم_يُفرض_عند_الحدّ_ويردّ_413_بجسم_مشكلة_بلغتيه_لا_استثناءً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ميبي‌بايت ونصف على سقفٍ ميبي‌بايت — والسقف يصل الخادم من إعداده كما في النشر.
        byte[] oversized = Attachments.Jpeg((int)ApiTestDatabase.StorageMaximumBytes + (512 * 1024));

        using HttpResponseMessage response = await api.Call(Attachments.Deposit(
            Attachments.Root(ApiTestDatabase.CompanyA), ApiFixture.TokenA, oversized));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine("حجم زائد → " + (int)response.StatusCode + " " + Http.CodeOf(problem));

        Assert.Equal(HttpStatusCode.RequestEntityTooLarge, response.StatusCode);
        Assert.Equal("storage.content_too_large", Http.CodeOf(problem));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);

        // **بالعربية والإنجليزية معاً** — لا واحدة منهما ولا نصّ استثناء.
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detailAr").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.DoesNotContain("Exception", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task التصحيح_إصدارٌ_يشير_إلى_سلفه_والسلف_يبقى_مقروءاً_ببايتاته_الأصلية()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        byte[] first = Attachments.Jpeg(128);
        JsonElement original = await Attachments.DepositAsync(api, company, ApiFixture.TokenA, first);
        string originalId = Attachments.IdOf(original);

        byte[] second = Attachments.Png(256);
        using HttpResponseMessage revision = await api.Call(Attachments.Deposit(
            Attachments.Revisions(company, originalId),
            ApiFixture.TokenA,
            second,
            declaredFileName: "فاتورة-مصحّحة.png",
            declaredMediaType: "image/png"));

        (string revisionText, JsonElement revised) = await Http.BodyAsync(revision);
        Console.WriteLine("تصحيح → " + (int)revision.StatusCode + " " + revisionText);

        Assert.Equal(HttpStatusCode.Created, revision.StatusCode);
        Assert.Equal(2, revised.GetProperty("version").GetInt32());
        Assert.Equal(originalId, revised.GetProperty("supersedes").GetString());
        Assert.Equal("image/png", revised.GetProperty("mediaType").GetString());

        // والسلف يبقى — **ببايتاته الأصلية وببصمته الأصلية** — ويعرف من خلَفه.
        using HttpResponseMessage predecessor = await api.Call(
            Http.Request(HttpMethod.Get, Attachments.One(company, originalId), ApiFixture.TokenA));
        (_, JsonElement kept) = await Http.BodyAsync(predecessor);

        Assert.Equal(HttpStatusCode.OK, predecessor.StatusCode);
        Assert.Equal(Digest(first), kept.GetProperty("contentHash").GetString());
        Assert.Equal(Attachments.IdOf(revised), kept.GetProperty("supersededBy").GetString());
    }

    [Fact]
    public async Task إصداران_على_سلفٍ_واحد_يُردّ_ثانيهما_بـ409_برسالتيه_لا_بـ500()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string original = Attachments.IdOf(
            await Attachments.DepositAsync(api, company, ApiFixture.TokenA, Attachments.Jpeg(96)));

        using HttpResponseMessage winner = await api.Call(Attachments.Deposit(
            Attachments.Revisions(company, original), ApiFixture.TokenA, Attachments.Jpeg(97)));
        Assert.Equal(HttpStatusCode.Created, winner.StatusCode);

        using HttpResponseMessage loser = await api.Call(Attachments.Deposit(
            Attachments.Revisions(company, original), ApiFixture.TokenA, Attachments.Jpeg(98)));

        (string text, JsonElement problem) = await Http.BodyAsync(loser);
        Console.WriteLine("تفرّع → " + (int)loser.StatusCode + " " + Http.CodeOf(problem));

        // **409 لا 500**: الفهرس الفريد الجزئي يحسم السباق في القاعدة، والمحوّل يمسك
        // التصادم ويعيده رفضاً باسمه بدل أن يصعد استثناءً.
        Assert.Equal(HttpStatusCode.Conflict, loser.StatusCode);
        Assert.Equal("storage.attachment_already_superseded", Http.CodeOf(problem));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detailAr").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));
        Assert.DoesNotContain("23505", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task السحب_علامةٌ_لا_محو_والبايتات_تبقى_تُنزَّل_بعده()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        byte[] bytes = Attachments.Pdf(200);
        string id = Attachments.IdOf(await Attachments.DepositAsync(
            api, company, ApiFixture.TokenA, bytes, declaredFileName: "عقد.pdf", declaredMediaType: "application/pdf"));

        using HttpResponseMessage withdrawal = await api.Call(Http.Request(
            HttpMethod.Post,
            Attachments.Withdrawal(company, id),
            ApiFixture.TokenA,
            """{"reasonKey":"uploaded_by_mistake"}"""));

        (string text, JsonElement withdrawn) = await Http.BodyAsync(withdrawal);
        Console.WriteLine("سحب → " + (int)withdrawal.StatusCode + " " + text);

        Assert.Equal(HttpStatusCode.Created, withdrawal.StatusCode);
        Assert.Equal("uploaded_by_mistake", withdrawn.GetProperty("withdrawal").GetProperty("reasonKey").GetString());
        Assert.Equal(Digest(bytes), withdrawn.GetProperty("contentHash").GetString());

        // **ولا يُسحب مرّتين** — والقاعدة تقولها لا الشيفرة: مفتاح جدول العلامات هو المرفق.
        using HttpResponseMessage again = await api.Call(Http.Request(
            HttpMethod.Post, Attachments.Withdrawal(company, id), ApiFixture.TokenA, """{"reasonKey":"uploaded_by_mistake"}"""));
        (_, JsonElement twice) = await Http.BodyAsync(again);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("storage.attachment_withdrawn", Http.CodeOf(twice));

        // **والبايتات باقية**: التنزيل بعد السحب يعمل ويعيد البايتات نفسها.
        string ticket = await Attachments.TicketAsync(api, company, ApiFixture.TokenA, id);
        using HttpResponseMessage download = await api.Call(
            Http.Request(HttpMethod.Get, Attachments.Content(company, id, ticket), ApiFixture.TokenA));

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync(ApiFixture.Token));
    }

    [Fact]
    public async Task التنزيل_يخدم_البايتات_بترويسة_من_النوع_المشموم_وبـattachment_لا_inline()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        byte[] bytes = Attachments.Png(512);

        // **الاسم عربيّ والإعلان يقول jpeg** — والنوع المخدوم يجب أن يكون png من البايتات.
        string id = Attachments.IdOf(await Attachments.DepositAsync(
            api, company, ApiFixture.TokenA, bytes, declaredFileName: "صورة الفاتورة.png", declaredMediaType: null));

        string ticket = await Attachments.TicketAsync(api, company, ApiFixture.TokenA, id);

        using HttpResponseMessage download = await api.Call(
            Http.Request(HttpMethod.Get, Attachments.Content(company, id, ticket), ApiFixture.TokenA));

        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
        Assert.Equal("image/png", download.Content.Headers.ContentType!.MediaType);
        Assert.Equal(bytes, await download.Content.ReadAsByteArrayAsync(ApiFixture.Token));

        string disposition = download.Content.Headers.GetValues("Content-Disposition").Single();
        Console.WriteLine("Content-Disposition: " + disposition);

        Assert.StartsWith("attachment;", disposition, StringComparison.Ordinal);
        Assert.DoesNotContain("inline", disposition, StringComparison.Ordinal);

        // والاسم العربي يبقى عربياً — مُرمَّزاً بـRFC 5987 لا مبدَّلاً بشُرَط.
        Assert.Contains("filename*=UTF-8''", disposition, StringComparison.Ordinal);
        Assert.Contains(Uri.EscapeDataString("صورة الفاتورة.png"), disposition, StringComparison.Ordinal);
    }

    [Fact]
    public async Task الجرد_يُرشَّح_على_المستند_المصدر_ولا_يقبل_نصف_ربط()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;
        string document = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);

        for (int i = 0; i < 3; i++)
        {
            await Attachments.DepositAsync(
                api, company, ApiFixture.TokenA, Attachments.Jpeg(32 + i),
                sourceDocumentType: "sales.invoice", sourceDocumentId: document);
        }

        // ومرفقٌ على مستند آخر — كي لا يمرّ الترشيح أخضر على مجموعة من صنف واحد.
        await Attachments.DepositAsync(
            api, company, ApiFixture.TokenA, Attachments.Jpeg(99),
            sourceDocumentType: "sales.invoice",
            sourceDocumentId: Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture));

        using HttpResponseMessage listed = await api.Call(Http.Request(
            HttpMethod.Get,
            Attachments.Root(company) + "?sourceDocumentType=sales.invoice&sourceDocumentId=" + document,
            ApiFixture.TokenA));

        (string text, JsonElement page) = await Http.BodyAsync(listed);
        Console.WriteLine("جرد → " + (int)listed.StatusCode + " total=" + page.GetProperty("total").GetInt32());

        Assert.Equal(HttpStatusCode.OK, listed.StatusCode);
        Assert.Equal(3, page.GetProperty("total").GetInt32());
        Assert.Equal(3, page.GetProperty("items").GetArrayLength());
        Assert.All(
            page.GetProperty("items").EnumerateArray(),
            item => Assert.Equal(document, item.GetProperty("sourceDocumentId").GetString()));
        Assert.DoesNotContain("objectKey", text, StringComparison.OrdinalIgnoreCase);

        // **ونصفُ ربطٍ يُرفض ولا يُنفَّذ**: نوعٌ وحده يعني مسحاً على المستأجر كلّه.
        using HttpResponseMessage half = await api.Call(Http.Request(
            HttpMethod.Get, Attachments.Root(company) + "?sourceDocumentType=sales.invoice", ApiFixture.TokenA));
        (_, JsonElement problem) = await Http.BodyAsync(half);

        Assert.Equal(HttpStatusCode.BadRequest, half.StatusCode);
        Assert.Equal("storage.source_document_incomplete", Http.CodeOf(problem));
    }

    [Fact]
    public async Task حجم_صفحةٍ_فوق_السقف_يُرفض_ولا_يُقصّ_صامتاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Attachments.Root(ApiTestDatabase.CompanyA) + "?take=1000", ApiFixture.TokenA));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("storage.page_refused", Http.CodeOf(problem));
    }

    [Fact]
    public async Task عمرُ_تذكرةٍ_فوق_السقف_يُرفض_ولا_يُقصّ_صامتاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string id = Attachments.IdOf(
            await Attachments.DepositAsync(api, company, ApiFixture.TokenA, Attachments.Jpeg()));

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Attachments.Tickets(company, id), ApiFixture.TokenA, """{"lifetimeSeconds":3600}"""));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("storage.ticket_lifetime_refused", Http.CodeOf(problem));
    }

    [Fact]
    public async Task حمولةٌ_بلا_جزء_بايتات_أو_بجزء_غير_معروف_تُفشل_الطلب_كلّه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage missing = await api.Call(Attachments.Deposit(
            Attachments.Root(ApiTestDatabase.CompanyA), ApiFixture.TokenA, Attachments.Jpeg(), partName: "file"));

        (_, JsonElement problem) = await Http.BodyAsync(missing);

        Assert.Equal(HttpStatusCode.BadRequest, missing.StatusCode);
        Assert.Equal("wire.multipart.malformed", Http.CodeOf(problem));

        // وجسم JSON على باب الإيداع: **415 لا 400** — نوع محتوى غير مدعوم باسمه.
        using HttpResponseMessage json = await api.Call(Http.Request(
            HttpMethod.Post, Attachments.Root(ApiTestDatabase.CompanyA), ApiFixture.TokenA, """{"content":"AAAA"}"""));
        (_, JsonElement wrongType) = await Http.BodyAsync(json);

        Assert.Equal(HttpStatusCode.UnsupportedMediaType, json.StatusCode);
        Assert.Equal("wire.body.unsupported_media_type", Http.CodeOf(wrongType));
    }

    private static string Digest(byte[] bytes) =>
        Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(bytes));
}
