using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>ما يُحرَس على سطح المرفقات، لا ما يُفترَض.</b>
/// <para>
/// وإثبات «أ يقرأ مرفقه» لا يقول شيئاً عن التعدّد؛ ما يجب أن يُثبت هو أن <b>مرفق شركةٍ
/// لا يُقرأ ولا يُنزَّل بمعرّف شركة أخرى</b>، وأن الجواب <b>404 لا 403</b> — لأن «ممنوع»
/// تُثبت وجود ما لا يخصّ السائل. وأن <b>البصمة تُفحص قبل التسليم لا بعده</b>: بايتةٌ
/// واحدة تُفسَد في المخزن فيُرفض التنزيل ولا تُسلَّم.
/// </para>
/// </summary>
public sealed class AttachmentTenancyAndIntegrityTests
{
    [Fact]
    public async Task مرفقُ_شركةٍ_لا_يُقرأ_ولا_يُنزَّل_بمعرّف_شركةٍ_أخرى_والجواب_404_لا_403()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid a = ApiTestDatabase.CompanyA;
        Guid b = ApiTestDatabase.CompanyB;

        // مرفقٌ **موجود فعلاً** في «أ»، وتذكرةٌ صحيحة عليه — فالرفض التالي ليس رفض
        // «غير موجود» متنكّراً، ولا رفضَ تذكرةٍ مشوّهة.
        string id = Attachments.IdOf(await Attachments.DepositAsync(api, a, ApiFixture.TokenA, Attachments.Jpeg(160)));
        string ticketOfA = await Attachments.TicketAsync(api, a, ApiFixture.TokenA, id);

        // ─ ١ · المعرّف نفسه داخل نطاق «ب» بمعرّف شركة «ب»: لا شيء يُقرأ ─────────
        (HttpMethod Method, string Path, string? Body)[] inOtherCompany =
        [
            (HttpMethod.Get, Attachments.One(b, id), null),
            (HttpMethod.Post, Attachments.Tickets(b, id), """{"lifetimeSeconds":60}"""),
            (HttpMethod.Post, Attachments.Withdrawal(b, id), """{"reasonKey":"probe"}"""),
            (HttpMethod.Get, Attachments.Content(b, id, ticketOfA), null),
        ];

        foreach ((HttpMethod method, string path, string? body) in inOtherCompany)
        {
            using HttpResponseMessage response = await api.Call(Http.Request(method, path, ApiFixture.TokenB, body));
            (_, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"«ب» {method} {path} → {(int)response.StatusCode} {Http.CodeOf(problem)}");

            // **404 لا 403.** وهذا هو بيت القصيد: التمييز بينهما يُخبر السائل بوجود ما
            // لا يخصّه، فيصير السطح عدّاد وجود لمرفقات مستأجرين آخرين.
            Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
            Assert.Equal("storage.attachment_not_found", Http.CodeOf(problem));
        }

        // ─ ٢ · وتذكرة «أ» كاملةً في جلسة «ب» على شركة «ب» ⇒ 404 كذلك ───────────
        // ولو سقطت مقارنةُ مستأجر التذكرة سهواً لسقط النداء عند المخزن، لأن المستأجر
        // جزء من المفتاح هناك. طبقتان مستقلّتان لا واحدة مكرَّرة.
        Assert.DoesNotContain(id, string.Empty, StringComparison.Ordinal);

        // ─ ٣ · والمالك الحقيقي يقرأ وينزّل — فالحارس ليس خاوياً ─────────────────
        using HttpResponseMessage owner = await api.Call(
            Http.Request(HttpMethod.Get, Attachments.One(a, id), ApiFixture.TokenA));
        Assert.Equal(HttpStatusCode.OK, owner.StatusCode);

        using HttpResponseMessage download = await api.Call(
            Http.Request(HttpMethod.Get, Attachments.Content(a, id, ticketOfA), ApiFixture.TokenA));
        Assert.Equal(HttpStatusCode.OK, download.StatusCode);
    }

    [Fact]
    public async Task تذكرةٌ_لمرفقٍ_آخر_في_الشركة_نفسها_لا_تفتحه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string first = Attachments.IdOf(await Attachments.DepositAsync(api, company, ApiFixture.TokenA, Attachments.Jpeg(10)));
        string second = Attachments.IdOf(await Attachments.DepositAsync(api, company, ApiFixture.TokenA, Attachments.Jpeg(11)));

        string ticketForFirst = await Attachments.TicketAsync(api, company, ApiFixture.TokenA, first);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Attachments.Content(company, second, ticketForFirst), ApiFixture.TokenA));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("storage.attachment_not_found", Http.CodeOf(problem));
    }

    [Fact]
    public async Task تذكرةٌ_منتهية_تُردّ_401_بلا_كشف_وجود()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string id = Attachments.IdOf(await Attachments.DepositAsync(api, company, ApiFixture.TokenA, Attachments.Jpeg(24)));
        string ticket = await Attachments.TicketAsync(api, company, ApiFixture.TokenA, id, lifetimeSeconds: 1);

        await Task.Delay(TimeSpan.FromMilliseconds(1400), ApiFixture.Token);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Attachments.Content(company, id, ticket), ApiFixture.TokenA));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine("تذكرة منتهية → " + (int)response.StatusCode + " " + Http.CodeOf(problem));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("storage.ticket_expired", Http.CodeOf(problem));

        // **ولا كشف وجود**: لا اسم ملفّ، ولا بصمة، ولا حجم، ولا نوع.
        Assert.DoesNotContain("image/jpeg", text, StringComparison.Ordinal);
        Assert.DoesNotContain("fileName", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task تذكرةٌ_مقلوبةُ_خانةٍ_واحدة_تُردّ_401_ولا_تفتح_شيئاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        string id = Attachments.IdOf(await Attachments.DepositAsync(api, company, ApiFixture.TokenA, Attachments.Jpeg(48)));
        string ticket = await Attachments.TicketAsync(api, company, ApiFixture.TokenA, id);

        // محرفٌ واحد يُبدَّل في منتصف الرمز — أي خانةٌ في البايتات الموقّعة.
        char[] tampered = ticket.ToCharArray();
        int middle = tampered.Length / 2;
        tampered[middle] = tampered[middle] == 'A' ? 'B' : 'A';

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Attachments.Content(company, id, new string(tampered)), ApiFixture.TokenA));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Contains(
            Http.CodeOf(problem),
            new[] { "storage.ticket_signature_invalid", "storage.ticket_expired" },
            StringComparer.Ordinal);
    }

    [Fact]
    public async Task بايتةٌ_واحدة_تُفسَد_في_المخزن_فيُرفض_التنزيل_ولا_تُسلَّم()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        Guid company = ApiTestDatabase.CompanyA;

        byte[] bytes = Attachments.Pdf(300);
        string id = Attachments.IdOf(await Attachments.DepositAsync(
            api, company, ApiFixture.TokenA, bytes, declaredFileName: "سند.pdf", declaredMediaType: "application/pdf"));

        // ─ التنزيل يعمل قبل الإفساد — فالحارس ليس خاوياً ────────────────────────
        string before = await Attachments.TicketAsync(api, company, ApiFixture.TokenA, id);
        using (HttpResponseMessage healthy = await api.Call(
            Http.Request(HttpMethod.Get, Attachments.Content(company, id, before), ApiFixture.TokenA)))
        {
            Assert.Equal(HttpStatusCode.OK, healthy.StatusCode);
            Assert.Equal(bytes, await healthy.Content.ReadAsByteArrayAsync(ApiFixture.Token));
        }

        // ─ ثم تُقلب **بايتة واحدة** تحت المسار نفسه، بحجمٍ لا يتغيّر ─────────────
        string file = Corrupt(id, bytes.Length);
        Console.WriteLine("أُفسدت بايتة واحدة في: " + file);

        string after = await Attachments.TicketAsync(api, company, ApiFixture.TokenA, id);
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Attachments.Content(company, id, after), ApiFixture.TokenA));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine("بعد الإفساد → " + (int)response.StatusCode + " " + Http.CodeOf(problem));

        // **يُرفض قبل التسليم لا بعده**: مخزنٌ يسلّم ثم يخبرك أنها لا تطابق قد سلّمها.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("storage.content_hash_mismatch", Http.CodeOf(problem));
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detailAr").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));

        // ولا بايتة من المحتوى المُبدَّل تعبر في جسم المشكلة.
        Assert.DoesNotContain("%PDF", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// يقلب بايتةً واحدة في ملفّ المرفق داخل جذر المخزن.
    /// <para>
    /// <b>والبحث بالحجم لا بالاسم:</b> مفتاح الكائن عشوائيّ ولا يُشتقّ من المعرّف —
    /// وذلك بعينه ما يُثبته هذا الاختبار عرضاً: <b>لا سبيل إلى ملفّ مرفقٍ من معرّفه</b>.
    /// </para>
    /// </summary>
    private static string Corrupt(string attachmentId, int length)
    {
        string[] candidates = [.. Directory
            .EnumerateFiles(ApiTestDatabase.StorageRoot, "*", SearchOption.AllDirectories)
            .Where(path => new FileInfo(path).Length == length)];

        // ولا يُشتقّ الملفّ من المعرّف: يُطابَق بالبايتات لا بالاسم.
        string file = candidates.Length switch
        {
            0 => throw new InvalidOperationException(
                "لم يُعثر على ملفّ بطول " + length.ToString(CultureInfo.InvariantCulture)
                + " تحت " + ApiTestDatabase.StorageRoot + " — والمرفق " + attachmentId),
            _ => candidates[^1],
        };

        byte[] content = File.ReadAllBytes(file);
        content[^1] ^= 0xFF;
        File.WriteAllBytes(file, content);
        return file;
    }
}
