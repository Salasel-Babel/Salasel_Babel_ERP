using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>كتالوجُ الخطط بياناتُ المنصّة لا شيفرتُها — من الشبكة (ADR-0092).</b>
/// <para>
/// بابان: قراءةُ الخطط <b>المنشورة</b> لكلّ مصادَق، وسطحُ <b>مشغّل المنصّة</b> الذي يُنشئ
/// الخطّة ويُسعّرها وينشرها بسندٍ وسبب. وما يُثبَت هنا أربعة: أن المنشور يُقرأ بسعره
/// ووحداته؛ وأن سطح المنصّة لا يبلغه اعتمادُ منشأة؛ وأن خطّةً غيرَ منشورة لا تُباع
/// حتى تُنشر؛ وأن الرفض — بلا سند، أو بوحدةٍ مجهولة، أو برمزٍ معتلّ — يُقرأ باسمه.
/// </para>
/// </summary>
public sealed class PlanCatalogueSurfaceTests
{
    private const string Plans = "/api/v1/plans";
    private const string PlatformPlans = "/api/v1/platform/plans";

    [Fact]
    public async Task الخططُ_المنشورة_تُقرأ_بأيّ_اعتمادٍ_مصادَق_بسعرها_ووحداتها_مرتَّبةً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Get, Plans, ApiFixture.TokenA));
        (string text, JsonElement body) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        List<JsonElement> plans = [.. body.GetProperty("plans").EnumerateArray()];
        List<string> codes = [.. plans.Select(static p => p.GetProperty("code").GetString()!)];

        Assert.Contains("ESSENTIAL", codes);
        Assert.Equal(codes.Order(StringComparer.Ordinal), codes);
        Assert.All(plans, static p => Assert.True(p.GetProperty("published").GetBoolean()));

        JsonElement essential = plans.Single(static p => p.GetProperty("code").GetString() == "ESSENTIAL");

        // المالُ نصٌّ على السلك، والعملةُ مكتوبة، والوحداتُ برموزها.
        Assert.Equal(JsonValueKind.String, essential.GetProperty("monthlyPrice").ValueKind);
        Assert.Equal(JsonValueKind.String, essential.GetProperty("perUserPrice").ValueKind);
        Assert.Equal("SAR", essential.GetProperty("currency").GetString());
        Assert.Contains("CORE", essential.GetProperty("modules").EnumerateArray().Select(static m => m.GetString()));
    }

    [Fact]
    public async Task سطحُ_المنصّة_لمشغّلها_وحده_واعتمادُ_منشأةٍ_يُردّ_باسم_الرفض()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ١ · اعتمادُ منشأة — مهما اتّسع — لا يبلغ الكتالوج كلّه ولا يكتب فيه.
        using HttpResponseMessage listRefused = await api.Call(Http.Request(
            HttpMethod.Get, PlatformPlans, ApiFixture.TokenA));

        (string text, JsonElement problem) = await Http.BodyAsync(listRefused);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, listRefused.StatusCode);
        Assert.Equal("platform.operator_required", Http.CodeOf(problem));

        using HttpResponseMessage putRefused = await api.Call(Http.Request(
            HttpMethod.Put, PlatformPlans + "/" + NewCode(), ApiFixture.TokenA, Body(published: false)));

        (_, JsonElement putProblem) = await Http.BodyAsync(putRefused);

        Assert.Equal(HttpStatusCode.Forbidden, putRefused.StatusCode);
        Assert.Equal("platform.operator_required", Http.CodeOf(putProblem));

        // ٢ · ومشغّلُ المنصّة يرى الكتالوج كلّه — وما تراه المنشأةُ جزءٌ منه.
        using HttpResponseMessage all = await api.Call(Http.Request(
            HttpMethod.Get, PlatformPlans, ApiFixture.TokenPlatform));

        (_, JsonElement catalogue) = await Http.BodyAsync(all);

        Assert.Equal(HttpStatusCode.OK, all.StatusCode);
        Assert.Contains(
            catalogue.GetProperty("plans").EnumerateArray(),
            static p => p.GetProperty("code").GetString() == "ESSENTIAL");
    }

    [Fact]
    public async Task خطّةٌ_تُنشأ_غيرَ_منشورة_فلا_تُباع_ثم_تُنشر_فتظهر_وتُشترى()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (Guid tenantId, _, _) = await SubscriptionSurfaceTests.SignUpAsync(api);
        string code = NewCode();

        // ١ · تُنشأ مسودّةً: صفٌّ في الكتالوج، وليست في المنشور.
        JsonElement draft = await PutAsync(api, code, Body(published: false));

        Assert.Equal(code, draft.GetProperty("code").GetString());
        Assert.False(draft.GetProperty("published").GetBoolean());

        // والإنجليزية ترجمةٌ موسومة لا حقلٌ ثابت (ADR-0021).
        JsonElement translation = draft.GetProperty("nameTranslations")[0];
        Assert.Equal("en", translation.GetProperty("name").GetString());
        Assert.Equal("Test plan", translation.GetProperty("value").GetString());
        Assert.Equal("1234.5000", draft.GetProperty("monthlyPrice").GetString());
        Assert.Equal(["AP", "AR", "CORE", "INV"], draft.GetProperty("modules").EnumerateArray().Select(static m => m.GetString()));

        Assert.DoesNotContain(code, await PublishedCodesAsync(api));

        // ٢ · ولا يُغيَّر إليها اشتراك: غيرُ المنشور ليس من «المنشور» الذي يسمّيه الرفض.
        (JsonElement refused, HttpStatusCode refusedStatus) = await SubscriptionSurfaceTests.ChangePlanAsync(api, tenantId, code);

        Assert.Equal(HttpStatusCode.UnprocessableContent, refusedStatus);
        Assert.Equal("subscription.plan_unknown", Http.CodeOf(refused));

        // ٣ · ثم تُنشر بسندٍ — فتظهر للمنشأة وتُشترى.
        JsonElement published = await PutAsync(api, code, Body(published: true));

        Assert.True(published.GetProperty("published").GetBoolean());
        Assert.Contains(code, await PublishedCodesAsync(api));

        (JsonElement changed, HttpStatusCode changedStatus) = await SubscriptionSurfaceTests.ChangePlanAsync(api, tenantId, code);

        Assert.Equal(HttpStatusCode.Created, changedStatus);
        Assert.Equal(code, changed.GetProperty("planCode").GetString());

        Dictionary<string, string> modules = TenantSignupTests.ModulesOf(changed);
        Assert.Equal("Entitled", modules["INV"]);
    }

    [Fact]
    public async Task الكتابةُ_بلا_سندٍ_أو_بوحدةٍ_مجهولة_أو_برمزٍ_معتلّ_تُرفض_باسمها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ١ · بلا سند.
        using HttpResponseMessage noAuthority = await api.Call(Http.Request(
            HttpMethod.Put, PlatformPlans + "/" + NewCode(), ApiFixture.TokenPlatform,
            Body(published: false, authority: "   ")));

        (string text, JsonElement problem) = await Http.BodyAsync(noAuthority);
        Console.WriteLine(text);

        Assert.Equal("platform.plan_authority_missing", Http.CodeOf(problem));
        Assert.Equal("authority", problem.GetProperty("errors")[0].GetProperty("field").GetString());

        // ٢ · وحدةٌ ليست في الكتالوج.
        using HttpResponseMessage unknownModule = await api.Call(Http.Request(
            HttpMethod.Put, PlatformPlans + "/" + NewCode(), ApiFixture.TokenPlatform,
            Body(published: false, modules: """["CORE","XYZ"]""")));

        (_, JsonElement moduleProblem) = await Http.BodyAsync(unknownModule);

        Assert.Equal(HttpStatusCode.UnprocessableContent, unknownModule.StatusCode);
        Assert.Equal("platform.plan_refused", Http.CodeOf(moduleProblem));
        Assert.Contains("XYZ", moduleProblem.GetProperty("detailAr").GetString(), StringComparison.Ordinal);

        // ٣ · خطّةٌ بلا وحدات لا تُباع.
        using HttpResponseMessage noModules = await api.Call(Http.Request(
            HttpMethod.Put, PlatformPlans + "/" + NewCode(), ApiFixture.TokenPlatform,
            Body(published: false, modules: "[]")));

        (_, JsonElement emptyProblem) = await Http.BodyAsync(noModules);

        Assert.Equal(HttpStatusCode.UnprocessableContent, noModules.StatusCode);
        Assert.Equal("platform.plan_refused", Http.CodeOf(emptyProblem));

        // ٤ · رمزٌ معتلّ في المسار يُردّ قبل أن يُقرأ الجسم.
        using HttpResponseMessage badCode = await api.Call(Http.Request(
            HttpMethod.Put, PlatformPlans + "/bad-code", ApiFixture.TokenPlatform, Body(published: false)));

        (_, JsonElement codeProblem) = await Http.BodyAsync(badCode);

        Assert.Equal(HttpStatusCode.BadRequest, badCode.StatusCode);
        Assert.Equal("wire.path.malformed", Http.CodeOf(codeProblem));

        // ٥ · وسعرٌ ليس مالاً على السلك يُرفض بشكله لا بقيمته.
        using HttpResponseMessage badMoney = await api.Call(Http.Request(
            HttpMethod.Put, PlatformPlans + "/" + NewCode(), ApiFixture.TokenPlatform,
            Body(published: false, monthly: "1,234")));

        (_, JsonElement moneyProblem) = await Http.BodyAsync(badMoney);

        Assert.Equal(HttpStatusCode.BadRequest, badMoney.StatusCode);
        Assert.StartsWith("wire.", Http.CodeOf(moneyProblem), StringComparison.Ordinal);
    }

    // ── أدوات ──────────────────────────────────────────────────────────────

    /// <summary>رمزُ خطّةٍ لا يشاركه اختبارٌ آخر — الكتالوج واحدٌ لكلّ تشغيلة.</summary>
    private static string NewCode() =>
        "T" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture)[..10].ToUpperInvariant();

    private static string Body(
        bool published,
        string authority = "PRICE-2026-01",
        string modules = """["CORE","AR","AP","INV"]""",
        string monthly = "1234.5000") =>
        $$"""
        {"nameAr":"خطّةُ اختبار","nameTranslations":[{"name":"en","value":"Test plan"}],"monthlyPrice":"{{monthly}}","perUserPrice":"45.0000",
         "includedUsers":5,"modules":{{modules}},"published":{{(published ? "true" : "false")}},
         "authority":"{{authority}}","reasonAr":"تسعيرٌ في اختبار"}
        """;

    private static async Task<JsonElement> PutAsync(ApiProcess api, string code, string body)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Put, PlatformPlans + "/" + code, ApiFixture.TokenPlatform, body));

        (string text, JsonElement json) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        // ‏والعطلُ الداخلي يُقرأ من سجلّ الخادم نفسه لا من رمزه وحده.
        Assert.True(
            response.StatusCode == HttpStatusCode.OK,
            text + "\n" + api.Output[Math.Max(0, api.Output.Length - 4000)..]);
        return json;
    }

    private static async Task<List<string>> PublishedCodesAsync(ApiProcess api)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(HttpMethod.Get, Plans, ApiFixture.TokenA));
        (_, JsonElement body) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return [.. body.GetProperty("plans").EnumerateArray().Select(static p => p.GetProperty("code").GetString()!)];
    }
}
