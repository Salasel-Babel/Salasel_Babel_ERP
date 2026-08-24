using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>العبور بين المستأجرين مستحيل عند حدّ HTTP — لا عند حدّ الخدمة وحده.</b>
/// <para>
/// وهذا هو أخطر صنف عطل في خدمة تُباع بالاشتراك: يكفي وقوعه مرّة واحدة مع عميل واحد
/// ليُنهي المنتج. ولذلك يُفحص من حيث يقع فعلاً — من الشبكة، بعميل لا يملك إلا اعتماداً
/// وعنواناً، لا من داخل العملية حيث كل شيء متاح على أي حال.
/// </para>
/// <para>
/// والمبدأ المفروض: <b>الهوية من الاعتماد وحده</b>. لا ترويسة مستأجر، ولا حقل مستأجر في
/// جسم، ولا وسيط استعلام. ومعرّف الشركة في المسار ليس مصدر هوية بل <b>ادّعاء يُطابَق</b>
/// بما يحمله الاعتماد، ويُرفض قبل قراءة الجسم وقبل أي اتصال بقاعدة بيانات.
/// </para>
/// </summary>
public sealed class TenancyTests
{
    [Fact]
    public async Task طلب_بلا_اعتماد_يُغلق_عليه_الباب_ويُسمّى_المخطّط_المقبول()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book), credential: null));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.credential_missing", Http.CodeOf(problem));
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task اعتماد_مختلَق_يُرفض_ولا_يُفرَّق_عن_غيره()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();
        TestCredential forged = TestCredential.Create(
            ApiTestDatabase.CompanyA, Guid.CreateVersion7(), ApiTestDatabase.CompanyA);

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book), forged));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.credential_rejected", Http.CodeOf(problem));
    }

    [Fact]
    public async Task القراءة_عبر_المستأجرين_مستحيلة_عند_حدّ_HTTP()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // «أ» يرحّل في شركته.
        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("tenant-a"), amount: "777.0000", documentDate: "2026-11-03")));

        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        // «ب» يحاول قراءة ميزان شركة «أ» — والباب مُغلق قبل أي اتصال بقاعدة بيانات.
        foreach (string path in new[]
        {
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            Http.ChainVerification(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, ApiTestDatabase.FiscalYear),
            Http.ReadEntry(ApiTestDatabase.CompanyA, Guid.CreateVersion7()),
        })
        {
            using HttpResponseMessage denied = await api.Call(
                Http.Request(HttpMethod.Get, path, ApiFixture.TokenB));

            (string text, JsonElement problem) = await Http.BodyAsync(denied);
            Console.WriteLine($"«ب» → {path} → {denied.StatusCode} {Http.CodeOf(problem)}");

            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));

            // ولا يتسرّب من الاستجابة شيء عن بيانات «أ».
            Assert.DoesNotContain("777.0000", text, StringComparison.Ordinal);
        }

        // والكتابة كذلك.
        using HttpResponseMessage write = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenB,
            Payloads.BalancedEntry(Payloads.Key("cross-write"))));

        (_, JsonElement writeProblem) = await Http.BodyAsync(write);
        Assert.Equal(HttpStatusCode.Forbidden, write.StatusCode);
        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(writeProblem));
    }

    [Fact]
    public async Task ما_يراه_مستأجر_في_ميزانه_لا_يحمل_شيئاً_من_ميزان_غيره()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("isolation"), amount: "4242.4242", documentDate: "2026-12-01")));

        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        using HttpResponseMessage own = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, "2026-12"), ApiFixture.TokenA));
        (string ownText, _) = await Http.BodyAsync(own);

        using HttpResponseMessage other = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyB, ApiTestDatabase.Book, "2026-12"), ApiFixture.TokenB));
        (string otherText, JsonElement otherBalance) = await Http.BodyAsync(other);

        Console.WriteLine("ميزان «أ»: " + ownText);
        Console.WriteLine("ميزان «ب»: " + otherText);

        Assert.Contains("4242.4242", ownText, StringComparison.Ordinal);
        Assert.DoesNotContain("4242.4242", otherText, StringComparison.Ordinal);
        Assert.Equal(0, otherBalance.GetProperty("rowCount").GetInt32());
    }

    [Fact]
    public async Task حقل_مستأجر_في_جسم_الطلب_يُفشل_الطلب_ولا_يُتجاهَل_بصمت()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(
                Payloads.Key("body-tenant"),
                extraField: "\"tenantId\": \"" + ApiTestDatabase.CompanyB.ToString("D") + "\"")));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        // التجاهل الصامت هنا كان سيجعل عميلاً يظنّ أنه رحّل لشركة أخرى، ويرى نجاحاً.
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("wire.body.malformed", Http.CodeOf(problem));
    }

    [Fact]
    public async Task معرّف_شركة_مشوّه_في_المسار_يُرفض_قبل_أي_شيء()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        foreach (string raw in new[] { "not-a-guid", "00000000-0000-0000-0000-000000000000", "{a1a1a1a1-0000-4000-8000-000000000001}" })
        {
            using HttpResponseMessage response = await api.Call(Http.Request(
                HttpMethod.Get, $"/api/v1/companies/{raw}/trial-balance?book=MAIN", ApiFixture.TokenA));

            (_, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"«{raw}» → {response.StatusCode} {Http.CodeOf(problem)}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.Equal("tenancy.company_id_malformed", Http.CodeOf(problem));
        }
    }
}
