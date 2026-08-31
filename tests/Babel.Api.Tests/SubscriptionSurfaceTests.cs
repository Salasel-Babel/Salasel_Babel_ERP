using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الاشتراك يُقرأ، وتُغيَّر خطّته، وينقطع، ويُستأنف — من الشبكة.</b>
/// <para>
/// وكل ما هنا يمرّ بالمستأجر الذي أنشأه <b>هذا الاختبار نفسه</b> من الباب المفتوح، لا
/// بمستأجرٍ مبذور من الإعداد: تغييرُ خطّةٍ يمسّ استحقاق مستأجر، ومستأجرٌ مشترك بين
/// اختبارين يجعل الثاني يمرّ أو يسقط بحسب من سبقه.
/// </para>
/// </summary>
public sealed class SubscriptionSurfaceTests
{
    private const string Sessions = "/api/v1/access/sessions";

    [Fact]
    public async Task قراءة_الاشتراك_حقُّ_صاحبه_ولا_يبلغها_اعتماد_مستأجرٍ_آخر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (Guid tenantId, TestCredential owner, _) = await SignUpAsync(api);

        // ١ · صاحب الاشتراك يقرؤه.
        (JsonElement subscription, HttpStatusCode status) = await ReadAsync(api, tenantId, owner);

        Assert.Equal(HttpStatusCode.OK, status);
        Assert.Equal("ESSENTIAL", subscription.GetProperty("planCode").GetString());
        Assert.Equal("Active", subscription.GetProperty("state").GetString());

        // وتاريخ التجديد موجودٌ على اشتراك فعّال — وهو ما يعد به هذا الباب.
        Assert.Equal(JsonValueKind.String, subscription.GetProperty("renewsOn").ValueKind);

        // ٢ · واعتماد مستأجرٍ آخر — **مُصدَراً من جلسة، لا اعتماد تزويد** — لا يبلغه.
        //     والفرق مقصود ومُعلَن: اعتماد التزويد لا عائلة له وهو باب الإقلاع الأسطولي
        //     (‏ADR-0045 §٣٫٣)، فهو يبلغ كل مستأجر بحكم دوره. وما عداه لا يبلغ إلا
        //     مستأجره، والرفض لا يُفرَّق فيه بين «لا وجود له» و«ليس مستأجرك» — والتمييز
        //     بينهما يجعل السطح عدّاد وجود لمستأجرين آخرين.
        (Guid _, TestCredential stranger, Guid _) = await SignUpAsync(api);

        using HttpResponseMessage foreign = await api.Call(Http.Request(
            HttpMethod.Get, Subscription(tenantId), stranger));

        (string text, JsonElement problem) = await Http.BodyAsync(foreign);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, foreign.StatusCode);
        Assert.Equal("tenancy.tenant_out_of_scope", Http.CodeOf(problem));
    }

    [Fact]
    public async Task تغيير_الخطّة_فعلُ_مشغِّل_ولا_يرفع_صاحبُ_الاشتراك_خطّته_بنفسه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (Guid tenantId, TestCredential owner, _) = await SignUpAsync(api);

        using HttpResponseMessage refused = await api.Call(Http.Request(
            HttpMethod.Post, PlanChanges(tenantId), owner,
            """{"planCode":"FULL","authority":"عقد-1","reasonAr":"ترقية"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(refused);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);
        Assert.Equal("subscription.operator_credential_required", Http.CodeOf(problem));
    }

    [Fact]
    public async Task تغيير_الخطّة_بسندٍ_يرفع_الوحدات_المشمولة_ويُبقي_ما_خرج_مقروءاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (Guid tenantId, _, _) = await SignUpAsync(api);

        // ١ · صعودٌ إلى الحزمة الشاملة: نقاط البيع والمخزون تصيران مستحقّتين.
        (JsonElement full, HttpStatusCode raised) = await ChangePlanAsync(api, tenantId, "FULL");

        Assert.Equal(HttpStatusCode.Created, raised);
        Assert.Equal("FULL", full.GetProperty("planCode").GetString());

        Dictionary<string, string> afterRise = TenantSignupTests.ModulesOf(full);
        Assert.Equal("Entitled", afterRise["POS"]);
        Assert.Equal("Entitled", afterRise["INV"]);

        // ٢ · ثم نزولٌ إلى الأساسية: ما خرج من الحزمة يهبط إلى **أرضيته** لا إلى العدم.
        //     وهذا هو ADR-0034 مقروءاً على السلك: وحدةٌ بلغ عملُها الدفتر تبقى مقروءة.
        (JsonElement essential, HttpStatusCode lowered) = await ChangePlanAsync(api, tenantId, "ESSENTIAL");

        Assert.Equal(HttpStatusCode.Created, lowered);

        Dictionary<string, string> afterFall = TenantSignupTests.ModulesOf(essential);
        Assert.Equal("Entitled", afterFall["CORE"]);
        Assert.Equal("ReadOnly", afterFall["POS"]);
        Assert.Equal("ReadOnly", afterFall["INV"]);

        // والتقارير التحليلية لا تُرحّل قيداً، فأرضيتها نزعٌ فعلي — الاستثناء المذكور
        // في ADR-0014 مقروءاً هنا، لا مفترَضاً.
        Assert.Equal("NotEntitled", afterFall["REP"]);
    }

    [Fact]
    public async Task خطّة_غير_معروفة_تُرفض_ورسالتُها_تُسمّي_المعروف()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (Guid tenantId, _, _) = await SignUpAsync(api);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, PlanChanges(tenantId), ApiFixture.TokenA,
            """{"planCode":"PLATINUM","authority":"عقد-2","reasonAr":"سبب"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("subscription.plan_unknown", Http.CodeOf(problem));
        Assert.Contains("ESSENTIAL", problem.GetProperty("detailAr").GetString(), StringComparison.Ordinal);
        Assert.Contains("ESSENTIAL", problem.GetProperty("detail").GetString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task تغييرُ_اشتراكٍ_بلا_سند_يُرفض_لأن_الاستحقاق_حدثٌ_تدقيقي()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (Guid tenantId, _, _) = await SignUpAsync(api);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Lapse(tenantId), ApiFixture.TokenA,
            """{"authority":"   ","reasonAr":"سبب"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.UnprocessableContent, response.StatusCode);
        Assert.Equal("subscription.authority_missing", Http.CodeOf(problem));
    }

    // ── أدوات مشتركة بين هذه المجموعة ومجموعة الانقطاع ───────────────────────

    internal static string Subscription(Guid tenantId) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/tenants/{tenantId:D}/subscription");

    internal static string PlanChanges(Guid tenantId) => Subscription(tenantId) + "/plan-changes";

    internal static string Lapse(Guid tenantId) => Subscription(tenantId) + "/lapse";

    internal static string Resumption(Guid tenantId) => Subscription(tenantId) + "/resumption";

    internal static async Task<(JsonElement Body, HttpStatusCode Status)> ReadAsync(
        ApiProcess api, Guid tenantId, TestCredential credential)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Subscription(tenantId), credential));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Console.WriteLine(text);
        return (body, response.StatusCode);
    }

    internal static async Task<(JsonElement Body, HttpStatusCode Status)> ChangePlanAsync(
        ApiProcess api, Guid tenantId, string planCode)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, PlanChanges(tenantId), ApiFixture.TokenA,
            $$"""{"planCode":"{{planCode}}","authority":"عقد-اختبار","reasonAr":"تغيير خطّة في اختبار"}"""));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Console.WriteLine(text);
        return (body, response.StatusCode);
    }

    /// <summary>
    /// يسجّل مستأجراً جديداً ويفتح جلسة مالكه — <b>ويُرجع اعتماداً حقيقياً مُصدَراً</b>،
    /// لا اعتماداً مُهيَّأ من الإعداد.
    /// </summary>
    internal static async Task<(Guid TenantId, TestCredential Owner, Guid CompanyId)> SignUpAsync(ApiProcess api)
    {
        (JsonElement registered, HttpStatusCode status) =
            await TenantSignupTests.RegisterAsync(api, TenantSignupTests.NewKey());

        Assert.Equal(HttpStatusCode.Created, status);

        Guid tenantId = Guid.ParseExact(registered.GetProperty("tenantId").GetString()!, "D");
        Guid companyId = Guid.ParseExact(registered.GetProperty("companyId").GetString()!, "D");
        string enrolment = registered.GetProperty("enrolmentCredential").GetString()!;

        using HttpResponseMessage opened = await api.Call(Http.Request(
            HttpMethod.Post, Sessions, credential: null,
            $$"""{"enrolmentCredential":"{{enrolment}}"}"""));

        (string text, JsonElement session) = await Http.BodyAsync(opened);
        Console.WriteLine(text);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);

        return (tenantId, AccessSurfaceTests.Bearer(session, "accessCredential"), companyId);
    }
}
