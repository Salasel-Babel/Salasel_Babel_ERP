using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الاعتماد يُصدَر، ويدور، ويُبطَل — من الشبكة، بعميل لا يملك إلا عنواناً وجسم طلب.</b>
/// <para>
/// وقبل هذا السطح كان الاعتماد يُحقن عند الإقلاع من الإعداد: لا يُصدَر، ولا يدور، ولا
/// يُبطَل، ولا ينقضي إلا بلحظةٍ ساكنة تُكتب بيد. وهو شكلٌ يكفي عرضاً ولا يُباع —
/// خدمةٌ تُباع بالاشتراك لا يوجد فيها طريق لأن يُنشئ عميلٌ اعتماده ولا لأن يسحبه حين
/// يترك موظّفٌ عمله.
/// </para>
/// <para>
/// وكل ما هنا يُفحص من حيث يقع فعلاً: من HTTP، بالاستجابات نفسها التي تصل عميلاً حقيقياً.
/// </para>
/// </summary>
public sealed class AccessSurfaceTests
{
    /// <summary>مسار فتح الجلسة — بلا مصادقة، والاعتماد في الجسم.</summary>
    private const string Sessions = "/api/v1/access/sessions";

    /// <summary>مسار تجديد الجلسة.</summary>
    private const string Renewal = "/api/v1/access/sessions/renewal";

    /// <summary>مسار إبطال الجلسة.</summary>
    private const string Revocation = "/api/v1/access/sessions/revocation";

    private static string Memberships(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/memberships");

    [Fact]
    public async Task دعوةُ_عضوٍ_تُنتج_اعتماد_انتساب_يُبدَّل_بجلسة_تفتح_مسارات_المنشأة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ١ · التسجيل: يُسكّ للمدعوّ معرّف، ويُمنح دوره، ويخرج اعتماد انتسابه مرّة واحدة.
        (JsonElement granted, HttpStatusCode grantStatus) =
            await InviteAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "منى القحطاني", "Contributor");

        Assert.Equal(HttpStatusCode.Created, grantStatus);
        string enrolment = granted.GetProperty("enrolmentCredential").GetString()!;
        string invitedUser = granted.GetProperty("member").GetProperty("userId").GetString()!;
        Assert.Equal("Contributor", granted.GetProperty("member").GetProperty("role").GetString());

        // ٢ · الإصدار: الانتساب يُبدَّل بجلسة كاملة.
        (JsonElement session, HttpStatusCode openStatus) = await OpenAsync(api, enrolment);
        Assert.Equal(HttpStatusCode.Created, openStatus);

        Assert.Equal(invitedUser, session.GetProperty("userId").GetString());
        Assert.Equal(1, session.GetProperty("generation").GetInt32());
        Assert.False(session.GetProperty("writeReachesNothing").GetBoolean());
        Assert.Equal(
            ApiTestDatabase.CompanyA.ToString("D", CultureInfo.InvariantCulture),
            session.GetProperty("memberships")[0].GetProperty("companyId").GetString());

        // ٣ · والاعتماد المُصدَر يعمل على مسارات المنشأة كأي اعتماد آخر — لا باب خاصّ به.
        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            Bearer(session, "accessCredential")));

        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task اعتماد_الانتساب_يُقبل_مرّة_واحدة_والثانية_تُسمّى_باسمها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        (JsonElement granted, _) =
            await InviteAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "سعد الدوسري", "Contributor");
        string enrolment = granted.GetProperty("enrolmentCredential").GetString()!;

        (_, HttpStatusCode first) = await OpenAsync(api, enrolment);
        Assert.Equal(HttpStatusCode.Created, first);

        using HttpResponseMessage second = await api.Call(Http.Request(
            HttpMethod.Post, Sessions, credential: null, Body("enrolmentCredential", enrolment)));

        (string text, JsonElement problem) = await Http.BodyAsync(second);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Unauthorized, second.StatusCode);

        // ورمزٌ يفترق عن «اعتماد غير مقبول» عمداً: «استُعملت دعوتك» يُخبر صاحبها أن شيئاً
        // وقع فيسأل عنه، و«غير مقبول» لا يتعلّم منه مختلِقٌ شيئاً.
        Assert.Equal("access.enrolment_consumed", Http.CodeOf(problem));
    }

    [Fact]
    public async Task التجديد_يدوّر_الاعتمادين_ويُبطل_القديم_ويرفع_رقم_الدورة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "ريم العتيبي");

        string firstRefresh = session.GetProperty("refreshCredential").GetString()!;
        string firstAccess = session.GetProperty("accessCredential").GetString()!;

        using HttpResponseMessage renewed = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null, Body("refreshCredential", firstRefresh)));

        (_, JsonElement next) = await Http.BodyAsync(renewed);
        Assert.Equal(HttpStatusCode.Created, renewed.StatusCode);

        // العائلة واحدة، والدورة تزيد، والاعتمادان جديدان — ولا واحد منهما يساوي سلفه.
        Assert.Equal(session.GetProperty("sessionId").GetString(), next.GetProperty("sessionId").GetString());
        Assert.Equal(2, next.GetProperty("generation").GetInt32());
        Assert.NotEqual(firstAccess, next.GetProperty("accessCredential").GetString());
        Assert.NotEqual(firstRefresh, next.GetProperty("refreshCredential").GetString());

        // والاعتماد الفاعل الجديد يعمل.
        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(next, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);
    }

    [Fact]
    public async Task اعتماد_تجديد_يُقدَّم_مرّتين_يُسقط_العائلة_كلّها_لا_الطلب_الثاني_وحده()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "خالد الشمري");

        string stolen = session.GetProperty("refreshCredential").GetString()!;

        // الاستعمال الأول — المشروع — ينجح ويُنتج اعتماداً فاعلاً جديداً.
        using HttpResponseMessage honest = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null, Body("refreshCredential", stolen)));
        (_, JsonElement rotated) = await Http.BodyAsync(honest);
        Assert.Equal(HttpStatusCode.Created, honest.StatusCode);

        // والاعتماد الفاعل الناتج حيّ **قبل** إعادة الاستعمال — وإلا لما أثبت الفحص شيئاً.
        using HttpResponseMessage before = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(rotated, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, before.StatusCode);

        // الاستعمال الثاني — بالاعتماد نفسه — سرقة.
        using HttpResponseMessage replay = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null, Body("refreshCredential", stolen)));

        (string text, JsonElement problem) = await Http.BodyAsync(replay);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Unauthorized, replay.StatusCode);
        Assert.Equal("access.refresh_replayed", Http.CodeOf(problem));

        // ‏**وهذا هو بيت القصيد**: لا يُرفض الطلب الثاني وحده — تسقط العائلة كلّها، فالاعتماد
        // الفاعل الذي كان حيّاً قبل سطرين يموت الآن. والبديل — رفضُ الثاني وحده — يترك
        // سارقاً بجلسة حيّة ولا يعلم بذلك أحد.
        using HttpResponseMessage after = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(rotated, "accessCredential")));

        (_, JsonElement denied) = await Http.BodyAsync(after);
        Assert.Equal(HttpStatusCode.Unauthorized, after.StatusCode);
        Assert.Equal("auth.credential_revoked", Http.CodeOf(denied));

        // والتجديد بالاعتماد الجديد ساقطٌ كذلك: العائلة كلّها، لا فرعٌ منها.
        using HttpResponseMessage renewAgain = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null,
            Body("refreshCredential", rotated.GetProperty("refreshCredential").GetString()!)));

        (_, JsonElement renewProblem) = await Http.BodyAsync(renewAgain);
        Assert.Equal(HttpStatusCode.Unauthorized, renewAgain.StatusCode);
        Assert.Equal("access.session_revoked", Http.CodeOf(renewProblem));
    }

    [Fact]
    public async Task الإبطال_يقع_فوراً_ولا_يُنتظر_به_انقضاء()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "نورة الحربي");

        // الاعتماد حيّ الآن — والانقضاء بعد ربع ساعة، فما يلي ليس مرور وقت.
        using HttpResponseMessage alive = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(session, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, alive.StatusCode);

        using HttpResponseMessage revoked = await api.Call(Http.Request(
            HttpMethod.Post, Revocation, Bearer(session, "accessCredential")));

        (string revokedText, JsonElement revocation) = await Http.BodyAsync(revoked);
        Console.WriteLine(revokedText);

        Assert.Equal(HttpStatusCode.Created, revoked.StatusCode);
        Assert.Equal("signed_out", revocation.GetProperty("reason").GetString());
        Assert.Equal(session.GetProperty("sessionId").GetString(), revocation.GetProperty("sessionId").GetString());

        // الطلب التالي مباشرة — لا بعد انقضاء — يُرفض برمزه المستقلّ.
        using HttpResponseMessage dead = await api.Call(Http.Request(
            HttpMethod.Get, Http.Session, Bearer(session, "accessCredential")));

        (_, JsonElement problem) = await Http.BodyAsync(dead);
        Assert.Equal(HttpStatusCode.Unauthorized, dead.StatusCode);
        Assert.Equal("auth.credential_revoked", Http.CodeOf(problem));

        // واعتماد التجديد لا يُحيي المُبطَل: الإبطال على العائلة لا على الاعتماد المفرد.
        using HttpResponseMessage renew = await api.Call(Http.Request(
            HttpMethod.Post, Renewal, credential: null,
            Body("refreshCredential", session.GetProperty("refreshCredential").GetString()!)));

        (_, JsonElement renewProblem) = await Http.BodyAsync(renew);
        Assert.Equal(HttpStatusCode.Unauthorized, renew.StatusCode);
        Assert.Equal("access.session_revoked", Http.CodeOf(renewProblem));
    }

    [Fact]
    public async Task اعتماد_مختلَق_على_بابَي_الجلسة_يُرفض_ولا_يُفرَّق_عن_غيره()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        foreach ((string path, string field) in new[] { (Sessions, "enrolmentCredential"), (Renewal, "refreshCredential") })
        {
            using HttpResponseMessage response = await api.Call(Http.Request(
                HttpMethod.Post, path, credential: null, Body(field, "ZmFrZS1jcmVkZW50aWFsLXRoYXQtd2FzLW5ldmVyLWlzc3VlZA")));

            (string text, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"{path} → {response.StatusCode} {Http.CodeOf(problem)}");

            // ‏401 لا 403: الفرق بينهما هو الفرق بين «لم تُصادِق» و«صادقتَ ومُنعت».
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("access.credential_rejected", Http.CodeOf(problem));

            // ولا يعبر من الرفض شيء عمّا في الخادم: لا معرّف جلسة، ولا مستأجر، ولا اعتماد.
            // (والمقارنة على الجسم بعد نزع instance — وهو **سطر الطلب الذي كتبه العميل
            // نفسه**، فوجودُ اسم المسار فيه ليس تسريباً بل صدىً لما أرسله.)
            string withoutInstance = WithoutInstance(text, problem);
            Assert.DoesNotContain("sessionId", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("tenantId", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("userId", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("accessCredential", withoutInstance, StringComparison.Ordinal);
            Assert.DoesNotContain("refreshCredential", withoutInstance, StringComparison.Ordinal);

            // ولا يُردّ النصّ المُقدَّم إلى مُقدِّمه: صدىً كهذا يجعل السطح مرآةً تُستعمل
            // في تسميم سجلّات من يقرأ الاستجابة أو يخزّنها.
            Assert.DoesNotContain("ZmFrZS1jcmVk", text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task دورُ_القارئ_يقرأ_ولا_يكتب_ورمزُه_يفترق_عن_رمز_الاستحقاق()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(
            api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "هند الزهراني", role: "Reader");

        Assert.True(session.GetProperty("writeReachesNothing").GetBoolean());

        using HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            Bearer(session, "accessCredential")));
        Assert.Equal(HttpStatusCode.OK, read.StatusCode);

        using HttpResponseMessage write = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            Bearer(session, "accessCredential"),
            Payloads.BalancedEntry(Payloads.Key("reader-role"))));

        (string text, JsonElement problem) = await Http.BodyAsync(write);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);

        // ورمزه ليس entitlement.read_only: ذاك يقول «جدّد اشتراكك» وهذا يقول «اطلب صلاحية»،
        // وخلطهما يجعل قارئاً يتّصل بالمحاسبة بلا سبب.
        Assert.Equal("membership.read_only", Http.CodeOf(problem));
        Assert.NotEqual("entitlement.read_only", Http.CodeOf(problem));
    }

    [Fact]
    public async Task غيرُ_المالك_لا_يدعو_أحداً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        JsonElement session = await SessionAsync(
            api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "بدر السبيعي", role: "Contributor");

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Memberships(ApiTestDatabase.CompanyA),
            Bearer(session, "accessCredential"),
            """{"displayNameAr":"دعوةٌ لا تقع","role":"Owner"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("membership.inviter_is_not_an_owner", Http.CodeOf(problem));
    }

    [Fact]
    public async Task قائمةُ_الأعضاء_لا_تحمل_اعتماداً_واحداً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        (JsonElement granted, _) =
            await InviteAsync(api, ApiTestDatabase.CompanyA, ApiFixture.TokenA, "لطيفة العنزي", "Contributor");
        string enrolment = granted.GetProperty("enrolmentCredential").GetString()!;

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Memberships(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (string text, JsonElement list) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(list.GetProperty("memberCount").GetInt32() >= 1);

        // ‏**سالباً**: اعتماد الانتساب الذي أُصدر قبل سطور لا يظهر في القائمة، ولا أي نصّ
        // يصلح للاستعمال. المُودَع بصمةٌ، والنصّ خرج مرّة واحدة في استجابة الدعوة.
        Assert.DoesNotContain(enrolment, text, StringComparison.Ordinal);
        Assert.DoesNotContain("Credential", text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>يدعو عضواً ويُعيد استجابة الدعوة.</summary>
    private static async Task<(JsonElement Body, HttpStatusCode Status)> InviteAsync(
        ApiProcess api, Guid company, TestCredential inviter, string nameAr, string role)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Memberships(company),
            inviter,
            $$"""{"displayNameAr":"{{nameAr}}","role":"{{role}}"}"""));

        (_, JsonElement body) = await Http.BodyAsync(response);
        return (body, response.StatusCode);
    }

    private static async Task<(JsonElement Body, HttpStatusCode Status)> OpenAsync(ApiProcess api, string enrolment)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Sessions, credential: null, Body("enrolmentCredential", enrolment)));

        (_, JsonElement body) = await Http.BodyAsync(response);
        return (body, response.StatusCode);
    }

    /// <summary>يدعو عضواً ثم يفتح له جلسة — الطريق الكامل الذي يسلكه عميل حقيقي.</summary>
    internal static async Task<JsonElement> SessionAsync(
        ApiProcess api, Guid company, TestCredential inviter, string nameAr, string role = "Contributor")
    {
        (JsonElement granted, HttpStatusCode grantStatus) = await InviteAsync(api, company, inviter, nameAr, role);
        Assert.Equal(HttpStatusCode.Created, grantStatus);

        (JsonElement session, HttpStatusCode openStatus) =
            await OpenAsync(api, granted.GetProperty("enrolmentCredential").GetString()!);
        Assert.Equal(HttpStatusCode.Created, openStatus);

        return session;
    }

    /// <summary>اعتمادٌ مُصدَر ملفوفاً في نوع الاعتماد الاختباري نفسه — فيُقدَّم كما يُقدَّمه عميل.</summary>
    internal static TestCredential Bearer(JsonElement session, string field) => new(
        session.GetProperty(field).GetString()!,
        Guid.Parse(session.GetProperty("tenantId").GetString()!),
        Guid.Parse(session.GetProperty("userId").GetString()!),
        []);

    private static string Body(string field, string value) =>
        $$"""{"{{field}}":"{{value}}"}""";

    /// <summary>
    /// الجسم بعد نزع قيمة <c>instance</c>.
    /// <para>
    /// و<c>instance</c> هو <b>مسار الطلب كما كتبه العميل</b>، فوجود اسم المسار فيه صدىً
    /// لما أرسله لا تسريباً من الخادم. ونزعُه يجعل الإثبات السالب يقول ما يقصده بالضبط:
    /// «لا شيء من حالة الخادم عبر».
    /// </para>
    /// </summary>
    internal static string WithoutInstance(string text, JsonElement problem) =>
        text.Replace(problem.GetProperty("instance").GetString() ?? string.Empty, "«المسار»", StringComparison.Ordinal);
}
