using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>العضوية تُمنح، ويتغيّر دورها، وتُسحب — وآخر مالكٍ لا يُمَسّ.</b>
/// <para>
/// وكان هذان البابان <b>مُعلَنَين في ADR-0045 §7 بندَي ٢ وغير مبنيَّين</b>: «لا سحب
/// عضوية ولا تغيير دور. والصلاحية تتبع ما هو مبنيّ: <c>DELETE</c> و<c>UPDATE</c> على
/// <c>core.access_membership</c> مسحوبتان من دور التطبيق». وقد بُنيا، ومُنحت الصلاحيتان
/// في الإيداع نفسه — و<c>UPDATE</c> على <b>عمود الدور وحده</b> لا على الجدول.
/// </para>
/// </summary>
public sealed class MembershipLifecycleTests
{
    [Fact]
    public async Task تغييرُ_الدور_يُنزل_عضواً_إلى_قارئ_فيقرأ_ولا_يكتب()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (_, TestCredential owner, Guid companyId) = await SubscriptionSurfaceTests.SignUpAsync(api);
        await FoundAsync(api, owner, companyId).ConfigureAwait(true);

        // ١ · عضوٌ كاتب، وجلسته تكتب فعلاً.
        (string memberId, TestCredential member) = await InviteAsync(api, owner, companyId, "بدر العتيبي", "Contributor");

        using (HttpResponseMessage wrote = await api.Call(Http.Request(
            HttpMethod.Post, CostCenters(companyId), member, """{"nameAr":"فرع الكاتب"}""")))
        {
            (string text, _) = await Http.BodyAsync(wrote);
            Console.WriteLine(text);
            Assert.Equal(HttpStatusCode.Created, wrote.StatusCode);
        }

        // ٢ · ثم يُخفَض إلى قارئ — بفعلٍ له فاعل ولحظة، لا بتعديل حقل.
        using (HttpResponseMessage changed = await api.Call(Http.Request(
            HttpMethod.Post, RoleChanges(companyId, memberId), owner, """{"role":"Reader"}""")))
        {
            (string text, JsonElement body) = await Http.BodyAsync(changed);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Created, changed.StatusCode);
            Assert.Equal("Contributor", body.GetProperty("previousRole").GetString());
            Assert.Equal("Reader", body.GetProperty("member").GetProperty("role").GetString());
        }

        // ٣ · وأثرُه فوري: القراءة تبقى، والكتابة تُردّ برمز الدور لا برمز الاستحقاق.
        using (HttpResponseMessage read = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(companyId, ApiTestDatabase.Book), member)))
        {
            Assert.Equal(HttpStatusCode.OK, read.StatusCode);
        }

        using (HttpResponseMessage refused = await api.Call(Http.Request(
            HttpMethod.Post, CostCenters(companyId), member, """{"nameAr":"فرع القارئ"}""")))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(refused);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Forbidden, refused.StatusCode);

            // ورمزٌ يفترق عن entitlement.read_only عمداً: ذاك يقول «جدّد اشتراكك»
            // وهذا يقول «اطلب صلاحية»، وخلطهما يجعل قارئاً يتّصل بالمحاسبة بلا سبب.
            Assert.Equal("membership.read_only", Http.CodeOf(problem));
        }
    }

    [Fact]
    public async Task سحبُ_العضوية_يقطع_وصولها_فوراً_ولا_ينتظر_انقضاءً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (_, TestCredential owner, Guid companyId) = await SubscriptionSurfaceTests.SignUpAsync(api);
        await FoundAsync(api, owner, companyId).ConfigureAwait(true);

        (string memberId, TestCredential member) = await InviteAsync(api, owner, companyId, "ريم الحربي", "Contributor");

        using (HttpResponseMessage before = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(companyId, ApiTestDatabase.Book), member)))
        {
            Assert.Equal(HttpStatusCode.OK, before.StatusCode);
        }

        using (HttpResponseMessage revoked = await api.Call(Http.Request(
            HttpMethod.Post, Revocation(companyId, memberId), owner)))
        {
            (string text, JsonElement body) = await Http.BodyAsync(revoked);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Created, revoked.StatusCode);
            Assert.Equal(memberId, body.GetProperty("member").GetProperty("userId").GetString());
            Assert.Equal("Contributor", body.GetProperty("member").GetProperty("role").GetString());
        }

        // والأثر فوري: ما تبلغه الجلسة يُقرأ من العضويات في كل طلب، فالاعتماد نفسه
        // — وهو حيٌّ ولم يُبطَل — لم يعد يبلغ المنشأة.
        using (HttpResponseMessage after = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(companyId, ApiTestDatabase.Book), member)))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(after);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Forbidden, after.StatusCode);
            Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));
        }

        // وسحبٌ ثانٍ يُسمّى باسمه: «لا عضوية بهذا المعرّف» — لا «تمّ» على فعلٍ لم يقع.
        using (HttpResponseMessage again = await api.Call(Http.Request(
            HttpMethod.Post, Revocation(companyId, memberId), owner)))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(again);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.NotFound, again.StatusCode);
            Assert.Equal("membership.not_found", Http.CodeOf(problem));
        }
    }

    [Fact]
    public async Task آخرُ_مالكٍ_لا_يُسحب_ولا_يُخفَض()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (_, TestCredential owner, Guid companyId) = await SubscriptionSurfaceTests.SignUpAsync(api);

        string ownerId = owner.User.ToString("D", CultureInfo.InvariantCulture);

        using (HttpResponseMessage revoked = await api.Call(Http.Request(
            HttpMethod.Post, Revocation(companyId, ownerId), owner)))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(revoked);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Conflict, revoked.StatusCode);
            Assert.Equal("membership.last_owner", Http.CodeOf(problem));
        }

        using (HttpResponseMessage demoted = await api.Call(Http.Request(
            HttpMethod.Post, RoleChanges(companyId, ownerId), owner, """{"role":"Reader"}""")))
        {
            (string text, JsonElement problem) = await Http.BodyAsync(demoted);
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Conflict, demoted.StatusCode);
            Assert.Equal("membership.last_owner", Http.CodeOf(problem));
        }

        // والمنشأة ما تزال قابلة للإدارة: المالك موجود ويستطيع أن يدعو.
        (string _, TestCredential _) = await InviteAsync(api, owner, companyId, "ناصر الزهراني", "Contributor");
    }

    [Fact]
    public async Task دورٌ_هو_الدور_القائم_يُرفض_بدل_أن_يُردَّ_عليه_بتمّ()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (_, TestCredential owner, Guid companyId) = await SubscriptionSurfaceTests.SignUpAsync(api);

        (string memberId, _) = await InviteAsync(api, owner, companyId, "هند الشمري", "Reader");

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, RoleChanges(companyId, memberId), owner, """{"role":"Reader"}"""));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("membership.role_unchanged", Http.CodeOf(problem));
    }

    [Fact]
    public async Task سحبُ_العضوية_فعلُ_مالكٍ_ولا_يفعله_كاتب()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        (_, TestCredential owner, Guid companyId) = await SubscriptionSurfaceTests.SignUpAsync(api);

        (string first, TestCredential writer) = await InviteAsync(api, owner, companyId, "خالد الغامدي", "Contributor");
        (string second, _) = await InviteAsync(api, owner, companyId, "لمياء السبيعي", "Contributor");
        Assert.NotEqual(first, second);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Revocation(companyId, second), writer));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("membership.actor_is_not_an_owner", Http.CodeOf(problem));
    }

    // ── أدوات ────────────────────────────────────────────────────────────────

    private static string Memberships(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/memberships");

    private static string Revocation(Guid company, string membershipId) =>
        Memberships(company) + "/" + membershipId + "/revocation";

    private static string RoleChanges(Guid company, string membershipId) =>
        Memberships(company) + "/" + membershipId + "/role-changes";

    private static string CostCenters(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/cost-centers");

    private static async Task FoundAsync(ApiProcess api, TestCredential owner, Guid companyId)
    {
        using HttpResponseMessage founded = await api.Call(Http.Request(
            HttpMethod.Put,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{companyId:D}/setup"),
            owner,
            """{"companyNameAr":"منشأة دورة حياة العضوية","costCenters":"One","decimalPlaces":2}"""));

        (string text, _) = await Http.BodyAsync(founded);
        Console.WriteLine(text);
        Assert.Equal(HttpStatusCode.Created, founded.StatusCode);
    }

    /// <summary>يدعو عضواً ويفتح جلسته، فيُرجع معرّفه واعتماده المُصدَر.</summary>
    private static async Task<(string MemberId, TestCredential Credential)> InviteAsync(
        ApiProcess api, TestCredential owner, Guid companyId, string nameAr, string role)
    {
        using HttpResponseMessage granted = await api.Call(Http.Request(
            HttpMethod.Post, Memberships(companyId), owner,
            $$"""{"displayNameAr":"{{nameAr}}","role":"{{role}}"}"""));

        (string grantText, JsonElement grant) = await Http.BodyAsync(granted);
        Console.WriteLine(grantText);
        Assert.Equal(HttpStatusCode.Created, granted.StatusCode);

        string memberId = grant.GetProperty("member").GetProperty("userId").GetString()!;
        string enrolment = grant.GetProperty("enrolmentCredential").GetString()!;

        using HttpResponseMessage opened = await api.Call(Http.Request(
            HttpMethod.Post, "/api/v1/access/sessions", credential: null,
            $$"""{"enrolmentCredential":"{{enrolment}}"}"""));

        (string openText, JsonElement session) = await Http.BodyAsync(opened);
        Console.WriteLine(openText);
        Assert.Equal(HttpStatusCode.Created, opened.StatusCode);

        return (memberId, AccessSurfaceTests.Bearer(session, "accessCredential"));
    }
}
