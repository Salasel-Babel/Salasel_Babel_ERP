using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>لا شيء يتجاوز الاستحقاق — والقاعدة 6 تعمل من فوق HTTP كما تعمل من تحته.</b>
/// <para>
/// ولا آلية تصريح ثانية في هذا السطح: <c>PostingService</c> و<c>LedgerAuditService</c>
/// يستدعيان <c>IEntitlementEnforcer</c> بأنفسهما، والسطح <b>يترجم</b> رفضهما إلى 403 برمزه.
/// آليتان متوازيتان تعني أن إحداهما تُصان وتُنسى الأخرى، والفارق لا يظهر إلا يوم يتجاوزه أحد.
/// </para>
/// <para>
/// <b>وما يبقى غير قابل للتمثيل، بدقّة:</b> «مستأجر <b>دفترُه</b> للقراءة فقط». الدفتر
/// والنواة إلزاميان، والإلزامية ترفض <c>NotEntitled</c> — أمّا <c>ReadOnly</c> فمقبولة
/// على الإلزامية كلّها، ومنها المبيعات والمشتريات (‏<c>EntitlementSet.Validate</c>).
/// ولذلك تُشهَد «القراءة تعمل والكتابة تُمنع» على المبيعات والمشتريات نفسيهما في
/// <c>DocumentEntitlementTests</c> — على السطح الذي يهمّ العميل فعلاً، لا على وحدة
/// طرفية. والباقي سؤالٌ على المالك: هل يُترك دفتر مَن توقّف عن الدفع مفتوحاً للترحيل؟
/// </para>
/// </summary>
public sealed class EntitlementTests
{
    [Fact]
    public async Task مستأجر_وحدته_للقراءة_فقط_يقرأ_ميزان_المراجعة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyC, ApiTestDatabase.Book), ApiFixture.TokenC));

        (string text, _) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task مستأجر_وحدته_للقراءة_فقط_لا_يرحّل_منها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyC),
            ApiFixture.TokenC,
            Payloads.BalancedEntry(Payloads.Key("readonly"), module: "RealEstate")));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("entitlement.read_only", Http.CodeOf(problem));

        // الرسالتان معاً — والمحاسب يقرأ بالعربية.
        Assert.Contains("للقراءة فقط", problem.GetProperty("detailAr").GetString()!, StringComparison.Ordinal);
        Assert.Contains("read-only", problem.GetProperty("detail").GetString()!, StringComparison.Ordinal);
    }

    [Fact]
    public async Task وحدة_لم_تُشترَ_قط_لا_تُرحّل_ولا_تُرى()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyC),
            ApiFixture.TokenC,
            Payloads.BalancedEntry(Payloads.Key("not-entitled"), module: "Assets")));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("entitlement.not_entitled", Http.CodeOf(problem));
    }

    [Fact]
    public async Task رمزا_الاستحقاق_يفترقان_فلا_يحتاج_العميل_قراءة_نصّ()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage readOnly = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyC), ApiFixture.TokenC,
            Payloads.BalancedEntry(Payloads.Key("distinct-ro"), module: "RealEstate")));

        using HttpResponseMessage notEntitled = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyC), ApiFixture.TokenC,
            Payloads.BalancedEntry(Payloads.Key("distinct-ne"), module: "Assets")));

        (_, JsonElement a) = await Http.BodyAsync(readOnly);
        (_, JsonElement b) = await Http.BodyAsync(notEntitled);

        // الحالتان تشتركان في 403 وتفترقان في الرمز — وهو الفرق الذي يبني عليه العميل
        // شاشتين مختلفتين: «جدّد اشتراكك» مقابل «هذه الوحدة ليست في باقتك».
        Assert.Equal(HttpStatusCode.Forbidden, readOnly.StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, notEntitled.StatusCode);
        Assert.NotEqual(Http.CodeOf(a), Http.CodeOf(b));
    }
}
