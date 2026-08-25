using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الترحيل والعكس عبر السلك — والحذف غير موجود.</b>
/// </summary>
public sealed class PostingAndReversalTests
{
    [Fact]
    public async Task الترحيل_يُعيد_إيصالاً_كاملاً_ورقم_قيد_بلا_فجوات()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        string key = Payloads.Key("post");

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(key, amount: "500.0000", documentDate: "2026-08-01")));

        (string text, JsonElement receipt) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.False(receipt.GetProperty("alreadyPosted").GetBoolean());
        Assert.Equal(2, receipt.GetProperty("lineCount").GetInt32());
        Assert.Equal("2026-08", receipt.GetProperty("periodCode").GetString());
        Assert.Equal(64, receipt.GetProperty("entryHash").GetString()!.Length);
        Assert.True(long.Parse(receipt.GetProperty("entryNumber").GetString()!, System.Globalization.CultureInfo.InvariantCulture) > 0);

        // ‏Location يوجّه إلى مورد القراءة — عقدٌ يستطيع العميل اتّباعه بلا تركيب مسار بيده.
        Assert.Equal(
            Http.ReadEntry(ApiTestDatabase.CompanyA, Guid.Parse(receipt.GetProperty("entryId").GetString()!)),
            response.Headers.Location?.OriginalString);
    }

    [Fact]
    public async Task المفتاح_نفسه_مرّتين_يُعيد_الإيصال_ذاته_ويُعلن_أنه_مُرحَّل_من_قبل()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        string key = Payloads.Key("idem");
        string body = Payloads.BalancedEntry(key, amount: "321.0000", documentDate: "2026-08-02");

        using HttpResponseMessage first = await api.Call(
            Http.Request(HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA, body));
        (string firstText, JsonElement firstReceipt) = await Http.BodyAsync(first);

        using HttpResponseMessage second = await api.Call(
            Http.Request(HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA, body));
        (string secondText, JsonElement secondReceipt) = await Http.BodyAsync(second);

        Console.WriteLine("الأول : " + firstText);
        Console.WriteLine("الثاني: " + secondText);

        Assert.Equal(HttpStatusCode.Created, first.StatusCode);
        Assert.False(firstReceipt.GetProperty("alreadyPosted").GetBoolean());

        // ‏200 لا 201، و«مُرحَّل من قبل» معلَن في الجسم أيضاً — رمز الحالة وحده يضيع
        // خلف أي وسيط، والعميل الذي أعاد المحاولة بعد انقطاع شبكة يحتاج جواباً لا تخميناً.
        Assert.Equal(HttpStatusCode.OK, second.StatusCode);
        Assert.True(secondReceipt.GetProperty("alreadyPosted").GetBoolean());

        Assert.Equal(firstReceipt.GetProperty("entryId").GetString(), secondReceipt.GetProperty("entryId").GetString());
        Assert.Equal(firstReceipt.GetProperty("entryNumber").GetString(), secondReceipt.GetProperty("entryNumber").GetString());
        Assert.Equal(firstReceipt.GetProperty("entryHash").GetString(), secondReceipt.GetProperty("entryHash").GetString());
    }

    [Fact]
    public async Task العكس_ينشئ_قيداً_جديداً_ولا_يمسّ_الأصل_والعكس_مرّة_واحدة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("to-reverse"), amount: "980.0000", documentDate: "2026-08-03")));

        (_, JsonElement receipt) = await Http.BodyAsync(posted);
        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        Guid entryId = Guid.Parse(receipt.GetProperty("entryId").GetString()!);

        using HttpResponseMessage reversed = await api.Call(Http.Request(
            HttpMethod.Post, Http.Reverse(ApiTestDatabase.CompanyA, entryId), ApiFixture.TokenA, Payloads.Reversal()));

        (string reversedText, JsonElement reversal) = await Http.BodyAsync(reversed);
        Console.WriteLine(reversedText);

        Assert.Equal(HttpStatusCode.Created, reversed.StatusCode);
        Assert.NotEqual(receipt.GetProperty("entryId").GetString(), reversal.GetProperty("entryId").GetString());

        using HttpResponseMessage again = await api.Call(Http.Request(
            HttpMethod.Post, Http.Reverse(ApiTestDatabase.CompanyA, entryId), ApiFixture.TokenA, Payloads.Reversal()));

        (string againText, JsonElement problem) = await Http.BodyAsync(again);
        Console.WriteLine(againText);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal("ledger.posting.already_reversed", Http.CodeOf(problem));
    }

    [Fact]
    public async Task الحذف_غير_موجود_على_هذا_السطح_أصلاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("no-delete"), amount: "60.0000", documentDate: "2026-08-04")));

        (_, JsonElement receipt) = await Http.BodyAsync(posted);
        Guid entryId = Guid.Parse(receipt.GetProperty("entryId").GetString()!);

        foreach (string path in new[]
        {
            Http.ReadEntry(ApiTestDatabase.CompanyA, entryId),
            Http.PostEntry(ApiTestDatabase.CompanyA),
        })
        {
            using HttpResponseMessage deleted = await api.Call(
                Http.Request(HttpMethod.Delete, path, ApiFixture.TokenA));

            Console.WriteLine($"DELETE {path} → {(int)deleted.StatusCode}");
            Assert.Equal(HttpStatusCode.MethodNotAllowed, deleted.StatusCode);
        }

        // ولا يوجد في العقد المنشور فعل حذف واحد — على أي مسار كان.
        string contract = await Http.ReadTextAsync(
            Path.Combine(RepositoryPaths.Root, "contracts", "openapi", "v1.json"));

        Assert.DoesNotContain("\"delete\"", contract, StringComparison.Ordinal);
        Assert.DoesNotContain("\"patch\"", contract, StringComparison.Ordinal);

        // وأفعال الاستبدال تُفحص **بالمسار** لا بالنصّ كلّه: القاعدة هي أن مورد الدفتر
        // لا يُستبدل ولا يُعدَّل — لا أن الفعل PUT ممنوع في المنتج. وإعداد المستأجر مورد
        // قابل للاستبدال بطبيعته، ومنعُ الفعل عليه كان سيدفع الاستبدال إلى POST فيضيع
        // التمييز الذي تقوم عليه هذه القاعدة نفسها.
        using JsonDocument document = JsonDocument.Parse(contract);
        List<string> mutable = [];

        foreach (JsonProperty path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (operation.Name is "put" or "patch" or "delete")
                {
                    mutable.Add(operation.Name + " " + path.Name);
                }
            }
        }

        Assert.Equal(["put /api/v1/companies/{companyId}/capability-profile"], mutable);

        foreach (string ledgerPath in new[] { "journal-entries", "trial-balance", "ledger-chain" })
        {
            Assert.DoesNotContain(mutable, entry => entry.Contains(ledgerPath, StringComparison.Ordinal));
        }
    }

    [Fact]
    public async Task الفترة_المقفلة_تُرفض_والمقفلة_نهائياً_لا_يفتحها_شيء()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage closed = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("closed"), documentDate: "2026-01-15")));

        (string closedText, JsonElement closedProblem) = await Http.BodyAsync(closed);
        Console.WriteLine(closedText);

        Assert.Equal(HttpStatusCode.Conflict, closed.StatusCode);
        Assert.Equal("ledger.posting.closed_period", Http.CodeOf(closedProblem));

        using HttpResponseMessage permanently = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("permanent"), documentDate: "2026-02-15")));

        (string permanentText, JsonElement permanentProblem) = await Http.BodyAsync(permanently);
        Console.WriteLine(permanentText);

        Assert.Equal(HttpStatusCode.Conflict, permanently.StatusCode);
        Assert.Equal("ledger.posting.permanently_closed_period", Http.CodeOf(permanentProblem));
    }

    [Fact]
    public async Task القيد_غير_المتوازن_يُرفض_برمز_يُميّزه_بلا_قراءة_نصّ()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.UnbalancedEntry(Payloads.Key("unbalanced"))));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ledger.posting.unbalanced", Http.CodeOf(problem));
    }

    /// <summary>
    /// <b>الطلب بلا رمز حدث مرفوض — والرفض يقول لماذا، لا «مرفوض» وحدها.</b>
    /// <para>
    /// ‏<c>event</c> صار حقلاً إلزامياً في العقد المنشور (تعديل v1 في مكانه قبل أول نشر —
    /// ‏ADR-0018 §«تعديل مُسجَّل»)، لأن رمز الحدث جزء من هوية الترحيل: بدونه يصير حدثان
    /// مختلفان من المستند نفسه عند الإطلاق نفسه هويةً واحدة فيُبتلع الثاني بصمت
    /// (‏ADR-0016). ولذلك يُفحص هنا شيئان لا شيء واحد: أن الطلب يُرفض، وأن الرسالتين
    /// تشرحان السبب — عميلٌ يقرأ «حقل مفقود» يضيف قيمة عشوائية، وعميلٌ يقرأ «الهوية
    /// تُبتلع» يسمّي حدثه.
    /// </para>
    /// </summary>
    [Fact]
    public async Task الطلب_بلا_رمز_حدث_يُرفض_ويشرح_أن_الهوية_هي_السبب()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("no-event"), @event: null)));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, response.StatusCode);
        Assert.Equal("ledger.posting.missing_event_code", Http.CodeOf(problem));

        // والمسار الصريح ليس استثناءً: الحمولة أعلاه تحمل سطوراً متوازنة، ورُفضت مع ذلك.
        string arabic = problem.GetProperty("detailAr").GetString()!;
        string english = problem.GetProperty("detail").GetString()!;

        Assert.Contains("هوية الترحيل", arabic, StringComparison.Ordinal);
        Assert.Contains("بصمت", arabic, StringComparison.Ordinal);
        Assert.Contains("posting identity", english, StringComparison.Ordinal);
        Assert.Contains("silently", english, StringComparison.Ordinal);

        // ورمزٌ فارغ ليس أرحم من غيابه: الحقل الموجود بقيمة فارغة يُرفض بالرمز نفسه.
        using HttpResponseMessage blank = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("blank-event"), @event: string.Empty)));

        (string blankText, JsonElement blankProblem) = await Http.BodyAsync(blank);
        Console.WriteLine(blankText);

        Assert.Equal(HttpStatusCode.UnprocessableEntity, blank.StatusCode);
        Assert.Equal("ledger.posting.missing_event_code", Http.CodeOf(blankProblem));
    }

    [Fact]
    public async Task إعادة_التحقق_من_السلسلة_تُعيد_حكماً_قابلاً_للقراءة_في_تدقيق()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("chain"), amount: "12.3400", documentDate: "2026-08-05")));

        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.ChainVerification(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, ApiTestDatabase.FiscalYear),
            ApiFixture.TokenA));

        (string text, JsonElement verdict) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(verdict.GetProperty("ok").GetBoolean());
        Assert.True(verdict.GetProperty("checked").GetInt32() > 0);
        Assert.Equal(JsonValueKind.Null, verdict.GetProperty("firstDivergentSequence").ValueKind);
        Assert.False(string.IsNullOrWhiteSpace(verdict.GetProperty("reasonAr").GetString()));
    }

    [Fact]
    public async Task قراءة_قيد_مفرد_ترفض_بصوت_عالٍ_ولا_تُرجع_فراغاً_يُقرأ_لا_قيد()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("read"), amount: "9.0000", documentDate: "2026-08-06")));

        (_, JsonElement receipt) = await Http.BodyAsync(posted);
        Guid entryId = Guid.Parse(receipt.GetProperty("entryId").GetString()!);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ReadEntry(ApiTestDatabase.CompanyA, entryId), ApiFixture.TokenA));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        // ⚠️ هذا الاختبار **مؤقّت بحكم بنائه**، ويُحذف في نفس طلب الدمج الذي يضيف
        // ReadEntryAsync إلى LedgerAuditService. و501 هنا أصدق من 404 على قيد موجود:
        // الثاني يجعل الواجهة تعرض «لا قيد» عن قيد مُرحَّل — رقم خاطئ صامت بعينه.
        Assert.Equal(HttpStatusCode.NotImplemented, response.StatusCode);
        Assert.Equal("ledger.read.entry_surface_unavailable", Http.CodeOf(problem));
    }
}
