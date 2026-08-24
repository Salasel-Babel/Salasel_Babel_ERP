using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>عقد الأخطاء: رمز ثابت، ورسالتان، ولا شيء عن الداخل.</b>
/// <para>
/// وأخطر ما يُختبر هنا هو الأخير: أن العطل التشغيلي — قاعدة بيانات لا تُبلَغ — يخرج
/// <b>نظيفاً</b>. محرّك الترحيل نفسه يُرجع اليوم <c>MessageText</c> الخام من PostgreSQL
/// داخل خطئه المجالي، وهو نصّ يحمل أسماء جداول وقيوداً؛ فلو مرّره السطح كما هو لكان
/// أول عميل يرسل حمولة سيئة يحصل على خريطة المخطّط.
/// </para>
/// </summary>
public sealed class ErrorContractTests
{
    private static readonly string[] MustNeverAppear =
    [
        "Npgsql", "PostgresException", "at Babel.", "at Microsoft.", "System.",
        "select ", "insert into", "ledger.journal_entry", "ledger.account", "SqlState",
        "StackTrace", ".cs:line",
    ];

    [Fact]
    public async Task كل_خطأ_يحمل_رمزاً_ثابتاً_ورسالتين_ومعرّف_تتبّع()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.UnbalancedEntry(Payloads.Key("shape"))));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);

        foreach (string field in new[] { "type", "title", "titleAr", "status", "detail", "detailAr", "instance", "code", "traceId", "errors" })
        {
            Assert.True(problem.TryGetProperty(field, out _), $"حقل مفقود من تفاصيل المشكلة: {field}");
        }

        Assert.Equal((int)response.StatusCode, problem.GetProperty("status").GetInt32());
        Assert.NotEmpty(problem.GetProperty("errors").EnumerateArray());
        Assert.Equal(
            problem.GetProperty("traceId").GetString(),
            response.Headers.GetValues("X-Babel-Trace-Id").Single());

        foreach (JsonElement error in problem.GetProperty("errors").EnumerateArray())
        {
            Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("code").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("messageAr").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(error.GetProperty("messageEn").GetString()));
        }
    }

    [Fact]
    public async Task الأخطاء_المحاسبية_تفترق_برموزها_لا_بنصوصها()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        Dictionary<string, string> observed = new(StringComparer.Ordinal);

        (string label, string body)[] cases =
        [
            ("غير متوازن", Payloads.UnbalancedEntry(Payloads.Key("codes-unbalanced"))),
            ("فترة مقفلة", Payloads.BalancedEntry(Payloads.Key("codes-closed"), documentDate: "2026-01-10")),
            ("فترة مقفلة نهائياً", Payloads.BalancedEntry(Payloads.Key("codes-perm"), documentDate: "2026-02-10")),
            ("مبلغ رمزاً رقمياً", Payloads.BalancedEntry(Payloads.Key("codes-num"), rawAmountToken: "12.5")),
        ];

        foreach ((string label, string body) in cases)
        {
            using HttpResponseMessage response = await api.Call(
                Http.Request(HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA, body));

            (_, JsonElement problem) = await Http.BodyAsync(response);
            observed[label] = Http.CodeOf(problem);
            Console.WriteLine($"{label} → {(int)response.StatusCode} {observed[label]}");
        }

        // أربع حالات، أربعة رموز مختلفة: العميل يُفرّق بينها بلا تحليل نصّ واحد.
        Assert.Equal(4, observed.Values.Distinct(StringComparer.Ordinal).Count());
    }

    [Fact]
    public async Task عطل_تشغيلي_لا_يسرّب_نصّ_قاعدة_بيانات_ولا_أثر_مكدّس()
    {
        // خادم موجَّه إلى قاعدة بيانات غير موجودة: كل ترحيل عليه ينفجر في طبقة الاتصال.
        await using ApiProcess broken = await ApiFixture.WithUnreachableDatabaseAsync();

        using HttpResponseMessage response = await broken.Call(Http.Request(
            HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("broken"))));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine("الاستجابة: " + text);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("api.internal_error", Http.CodeOf(problem));

        foreach (string forbidden in MustNeverAppear)
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }

        // والأثر كاملاً موجود — في سجلّ الخادم، تحت معرّف التتبّع نفسه.
        string traceId = problem.GetProperty("traceId").GetString()!;
        Assert.Contains(traceId, broken.Output, StringComparison.Ordinal);
        Assert.Contains("Npgsql", broken.Output, StringComparison.Ordinal);
    }

    [Fact]
    public async Task القراءة_من_قاعدة_بيانات_غير_مبلوغة_لا_تسرّب_شيئاً_أيضاً()
    {
        await using ApiProcess broken = await ApiFixture.WithUnreachableDatabaseAsync();

        using HttpResponseMessage response = await broken.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book), ApiFixture.TokenA));

        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine("الاستجابة: " + text);

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.Equal("api.internal_error", Http.CodeOf(problem));

        foreach (string forbidden in MustNeverAppear)
        {
            Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task جسم_مشوّه_يُرفض_برمزه_ويسمّي_موضعه_في_الحمولة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        foreach ((string label, string body) in new[]
        {
            ("JSON غير صالح", "{ this is not json"),
            ("حقل مجهول", Payloads.BalancedEntry(Payloads.Key("unknown"), extraField: "\"accountCode\": \"4101\"")),
            ("جسم فارغ", "{}"),
        })
        {
            using HttpResponseMessage response = await api.Call(
                Http.Request(HttpMethod.Post, Http.PostEntry(ApiTestDatabase.CompanyA), ApiFixture.TokenA, body));

            (string text, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"{label} → {(int)response.StatusCode} {Http.CodeOf(problem)}");

            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            Assert.StartsWith("wire.", Http.CodeOf(problem), StringComparison.Ordinal);

            foreach (string forbidden in MustNeverAppear)
            {
                Assert.DoesNotContain(forbidden, text, StringComparison.OrdinalIgnoreCase);
            }
        }
    }
}
