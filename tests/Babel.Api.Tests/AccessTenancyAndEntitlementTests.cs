using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>السطح الجديد لا يفتح باباً حول العزل ولا حول ADR-0034.</b>
/// <para>
/// وهذان أخطر ما يمكن أن يُكسر بإضافة سطح مصادقة:
/// </para>
/// <list type="number">
///   <item><b>العزل:</b> بابٌ جديد يقرأ بيانات مستأجر ولا يمرّ بالمطابقة التي يمرّ بها
///         كل باب غيره. و«من يعمل في هذه المنشأة» بيانات مستأجرٍ كأي بيانات.</item>
///   <item><b>الأرضية:</b> ‏ADR-0034 يقرّر أن الاشتراك المنقطع <b>يُخفَّض إلى القراءة ولا
///         يُنتزَع به السجلّ</b> — لأن حفظ السجلات المحاسبية وإبرازها التزامٌ على المنشأة.
///         وجعلُ <b>الدخول نفسه</b> مشروطاً بالاستحقاق يُبطل ذلك القرار من بابه الخلفي:
///         من مُنع الدخول لا يستطيع أن يقرأ.</item>
/// </list>
/// </summary>
public sealed class AccessTenancyAndEntitlementTests
{
    private static string Memberships(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/memberships");

    [Fact]
    public async Task مستأجرٌ_لا_يقرأ_أعضاء_منشأة_غيره_ولا_يعبر_من_الرفض_شيء_عنها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // «أ» يدعو عضواً باسمٍ مميّز في منشأته.
        const string secretName = "زهرة-اسم-لا-يجوز-أن-يعبر";

        using HttpResponseMessage granted = await api.Call(Http.Request(
            HttpMethod.Post,
            Memberships(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            $$"""{"displayNameAr":"{{secretName}}","role":"Contributor"}"""));

        Assert.Equal(HttpStatusCode.Created, granted.StatusCode);
        (_, JsonElement grantedBody) = await Http.BodyAsync(granted);
        string invitedUser = grantedBody.GetProperty("member").GetProperty("userId").GetString()!;
        string enrolment = grantedBody.GetProperty("enrolmentCredential").GetString()!;

        // و«أ» نفسه يراه.
        using HttpResponseMessage own = await api.Call(Http.Request(
            HttpMethod.Get, Memberships(ApiTestDatabase.CompanyA), ApiFixture.TokenA));
        (string ownText, _) = await Http.BodyAsync(own);
        Assert.Contains(secretName, ownText, StringComparison.Ordinal);

        // و«ب» يحاول القراءة والكتابة على منشأة «أ» — والبابان مُغلقان قبل أي عمل.
        foreach ((HttpMethod method, string? body) in new[]
        {
            (HttpMethod.Get, (string?)null),
            (HttpMethod.Post, """{"displayNameAr":"دخيل","role":"Owner"}"""),
        })
        {
            using HttpResponseMessage denied = await api.Call(Http.Request(
                method, Memberships(ApiTestDatabase.CompanyA), ApiFixture.TokenB, body));

            (string text, JsonElement problem) = await Http.BodyAsync(denied);
            Console.WriteLine($"«ب» {method} → {denied.StatusCode} {Http.CodeOf(problem)} · {text}");

            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));

            // ‏**والإثبات سالب**، ومقيسٌ على الجسم بعد نزع قيمة instance — وهي **مسار
            // الطلب كما كتبه «ب» نفسه**، فوجود كلمة memberships فيها صدىً لما أرسله لا
            // تسريباً من الخادم. وما لا يعبر: اسم عضو، ومعرّف مستخدم، واعتماد انتساب،
            // وعدد أعضاء، ومصفوفة أعضاء — ولا أي لفظ يفرّق «منشأة موجودة لا تبلغها» عن
            // «منشأة لا وجود لها».
            string leaked = AccessSurfaceTests.WithoutInstance(text, problem);

            Assert.DoesNotContain(secretName, leaked, StringComparison.Ordinal);
            Assert.DoesNotContain(invitedUser, leaked, StringComparison.Ordinal);
            Assert.DoesNotContain(enrolment, leaked, StringComparison.Ordinal);
            Assert.DoesNotContain("memberCount", leaked, StringComparison.Ordinal);
            Assert.DoesNotContain("displayNameAr", leaked, StringComparison.Ordinal);
            Assert.DoesNotContain("userId", leaked, StringComparison.Ordinal);
            Assert.DoesNotContain("\"members\"", leaked, StringComparison.Ordinal);

            // ولا يُفرَّق الرفض عن رفض منشأةٍ لا وجود لها إطلاقاً: الرمز نفسه والنصّ نفسه.
            Assert.Equal(
                "tenancy.company_out_of_scope",
                Http.CodeOf(problem));
        }
    }

    [Fact]
    public async Task جلسةٌ_مُصدَرة_لمستأجرٍ_لا_تبلغ_منشأة_مستأجرٍ_آخر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        JsonElement session = await AccessSurfaceTests.SessionAsync(
            api, ApiTestDatabase.CompanyB, ApiFixture.TokenB, "فيصل المطيري");

        // النطاق يُشتقّ من العضويات، فالجلسة تبلغ منشأة «ب» وحدها.
        Assert.Single(session.GetProperty("memberships").EnumerateArray());

        using HttpResponseMessage denied = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            AccessSurfaceTests.Bearer(session, "accessCredential")));

        (string text, JsonElement problem) = await Http.BodyAsync(denied);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));
        Assert.DoesNotContain("rowCount", text, StringComparison.Ordinal);
    }

    [Fact]
    public async Task مستأجرٌ_وحدتُه_للقراءة_فقط_يدخل_ويقرأ_ولا_يُحجَب()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // المستأجر «ج»: وحدة العقارات عنده **للقراءة فقط** — أي اشتُريت ثم انقطع الاشتراك.
        // والسؤال الذي يجيب عنه هذا الاختبار: أيستطيع أن يدخل أصلاً؟
        JsonElement session = await AccessSurfaceTests.SessionAsync(
            api, ApiTestDatabase.CompanyC, ApiFixture.TokenC, "عبدالله الغامدي");

        TestCredential issued = AccessSurfaceTests.Bearer(session, "accessCredential");

        // ١ · الدخول وقع. ولو مُنع لصار «التخفيض إلى القراءة» حجباً باسم آخر.
        Assert.Equal(1, session.GetProperty("generation").GetInt32());

        // ٢ · والقراءة تعمل بالاعتماد المُصدَر — وهذا هو ما تفرضه ADR-0034 حرفياً:
        //     حفظُ السجلات المحاسبية وإبرازها التزامٌ على المنشأة، فنزاعٌ تجاري بيننا
        //     وبين عميل لا يجوز أن يضعه في مخالفة.
        using HttpResponseMessage balance = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyC, ApiTestDatabase.Book), issued));
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        using HttpResponseMessage chart = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyC), issued));
        Assert.Equal(HttpStatusCode.OK, chart.StatusCode);

        using HttpResponseMessage sessionRead = await api.Call(Http.Request(HttpMethod.Get, Http.Session, issued));
        Assert.Equal(HttpStatusCode.OK, sessionRead.StatusCode);

        // ٣ · والتجديد يعمل كذلك: جلسةٌ تُقرأ اليوم ولا تُجدَّد غداً حجبٌ مؤجَّل لا تخفيض.
        using HttpResponseMessage renewed = await api.Call(Http.Request(
            HttpMethod.Post,
            "/api/v1/access/sessions/renewal",
            credential: null,
            $$"""{"refreshCredential":"{{session.GetProperty("refreshCredential").GetString()}}"}"""));
        Assert.Equal(HttpStatusCode.Created, renewed.StatusCode);

        // ٤ · وما يُغلق هو الكتابة على الوحدة المنقطعة وحدها، برمزها المتمايز.
        using HttpResponseMessage write = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyC),
            issued,
            Payloads.BalancedEntry(Payloads.Key("lapsed-write"), module: "RealEstate")));

        (string text, JsonElement problem) = await Http.BodyAsync(write);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("entitlement.read_only", Http.CodeOf(problem));
    }

    [Fact]
    public async Task إبطالُ_الجلسة_لا_يقع_على_اعتماد_التزويد_المُهيَّأ_من_الإعداد()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, "/api/v1/access/sessions/revocation", ApiFixture.TokenA));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        // وقولُ ذلك برمزه أصدق من ردّ «تمّ» على فعلٍ لم يقع: اعتماد التزويد لا عائلة له،
        // وسحبُه إعدادٌ يُغيَّر ونشرٌ يُعاد.
        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("access.session_not_issued_here", Http.CodeOf(problem));
    }
}
