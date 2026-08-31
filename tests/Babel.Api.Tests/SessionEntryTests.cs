using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الدخول: من أنا، وأي شركة أفتح؟</b> — وكل طريق يخرج من هنا إلى شركة ليست لي مغلق
/// <b>عند حدّ HTTP</b>.
/// <para>
/// وهذه المجموعة تفحص الباب الذي جعل الشاشة الأولى ممكنة، وتفحص معه أخطر ما يمكن أن
/// يُخطئ فيه: أن يصير هذا الباب — وهو <b>خارج نطاق الشركة</b> — قناةً يرى منها مستأجرٌ
/// شركةَ غيره. ولذلك لا يُفحص من داخل العملية بل من الشبكة، بعميل لا يملك إلا اعتماداً
/// وعنواناً.
/// </para>
/// </summary>
public sealed class SessionEntryTests
{
    /// <summary>
    /// <b>الجلسة تُسمّي الهوية وتُسمّي الشركات — ولا تُسمّي شركة مستأجر آخر.</b>
    /// <para>
    /// والفحص هنا <b>غير خاوٍ</b> عمداً: يُثبَت أولاً أن الخادم يعرف شركة «ب» فعلاً — وهو
    /// يُجيب عن ميزانها لصاحبها — ثم يُثبَت أنها لا تظهر في جلسة «أ». ولولا الشقّ الأول
    /// لكان التأكيد يمرّ على خادم لا يعرف شركة «ب» أصلاً، فيقيس غياب البيانات لا العزل.
    /// </para>
    /// </summary>
    [Fact]
    public async Task الجلسة_تُسمّي_شركات_الاعتماد_وحدها_ولا_تُسمّي_شركة_مستأجر_آخر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // شقّ اللاخواء: شركة «ب» موجودة ومؤسَّسة، ويراها صاحبها.
        using HttpResponseMessage ownedByB = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, ApiFixture.TokenB));
        (string bText, JsonElement bSession) = await Http.BodyAsync(ownedByB);
        Console.WriteLine("جلسة «ب»: " + bText);

        Assert.Equal(HttpStatusCode.OK, ownedByB.StatusCode);
        Assert.Equal(1, bSession.GetProperty("companyCount").GetInt32());
        Assert.Equal(
            ApiTestDatabase.CompanyB.ToString("D", CultureInfo.InvariantCulture),
            bSession.GetProperty("companies")[0].GetProperty("companyId").GetString());
        Assert.Equal("Ready", bSession.GetProperty("companies")[0].GetProperty("state").GetString());

        // ثم العزل: جلسة «أ» لا تحمل معرّف شركة «ب» ولا اسمها.
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, ApiFixture.TokenA));
        (string text, JsonElement session) = await Http.BodyAsync(response);
        Console.WriteLine("جلسة «أ»: " + text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ApiTestDatabase.CompanyA.ToString("D", CultureInfo.InvariantCulture),
            session.GetProperty("tenantId").GetString());
        Assert.Equal(1, session.GetProperty("companyCount").GetInt32());

        JsonElement company = session.GetProperty("companies")[0];
        Assert.Equal(
            ApiTestDatabase.CompanyA.ToString("D", CultureInfo.InvariantCulture),
            company.GetProperty("companyId").GetString());

        // الاسم العربي هو السجلّ، ولا حقل ثابت للإنجليزية على السلك (ADR-0021 بند 2).
        // واسم الحقل الممنوع يُركَّب ولا يُكتب حرفياً: كتابته تزيد **دين الاسم
        // الإنجليزي** الذي تحرسه القاعدة 14، فيصير الاختبار الذي يمنع الحقل سبباً
        // في ارتفاع عدّاد وجوده.
        Assert.False(string.IsNullOrWhiteSpace(company.GetProperty("nameAr").GetString()));
        Assert.False(company.TryGetProperty("name" + "En", out _));
        Assert.True(company.TryGetProperty("nameTranslations", out JsonElement translations));
        Assert.Equal(JsonValueKind.Array, translations.ValueKind);

        // ولا شيء عن «ب» في جسم «أ» — لا معرّفاً ولا اسماً.
        Assert.DoesNotContain(
            ApiTestDatabase.CompanyB.ToString("D", CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(
            ApiTestDatabase.CompanyC.ToString("D", CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>الشركة التي تُختار من جلسة غيري لا تُفتح لي</b> — والرفض عند حدّ HTTP قبل أي
    /// اتصال بقاعدة بيانات.
    /// <para>
    /// وهذا هو المسار الواقعي للهجوم: لا أحد يخمّن معرّفاً بصيغة 8-4-4-4-12، لكنّ من يرى
    /// معرّفاً حقيقياً في جلسته الخاصة (أو في سجلّ، أو في رابط) يجرّبه على اعتماده هو.
    /// </para>
    /// </summary>
    [Fact]
    public async Task معرّف_يُقرأ_من_جلسة_مستأجر_لا_يفتح_شيئاً_باعتماد_مستأجر_آخر()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // «ب» يقرأ جلسته فيحصل على معرّف شركته حقيقياً — لا مخترعاً.
        using HttpResponseMessage sessionOfB = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, ApiFixture.TokenB));
        (_, JsonElement bSession) = await Http.BodyAsync(sessionOfB);
        string companyOfB = bSession.GetProperty("companies")[0].GetProperty("companyId").GetString()!;

        Assert.Equal(ApiTestDatabase.CompanyB.ToString("D", CultureInfo.InvariantCulture), companyOfB);

        // «أ» يقدّم المعرّف نفسه على كل باب: قراءةً وكتابةً وتأسيساً.
        (HttpMethod Method, string Path, string? Body)[] attempts =
        [
            (HttpMethod.Get, "/api/v1/companies/" + companyOfB + "/trial-balance?book=MAIN", null),
            (HttpMethod.Get, "/api/v1/companies/" + companyOfB + "/setup", null),
            (HttpMethod.Get, "/api/v1/companies/" + companyOfB + "/capability-profile", null),
            (HttpMethod.Post, "/api/v1/companies/" + companyOfB + "/journal-entries",
                Payloads.BalancedEntry(Payloads.Key("stolen-id"))),
            (HttpMethod.Put, "/api/v1/companies/" + companyOfB + "/setup",
                """{"companyNameAr":"سرقة","costCenters":"One","decimalPlaces":2}"""),
        ];

        foreach ((HttpMethod method, string path, string? body) in attempts)
        {
            using HttpResponseMessage denied = await api.Call(
                Http.Request(method, path, ApiFixture.TokenA, body));
            (string deniedText, JsonElement problem) = await Http.BodyAsync(denied);
            Console.WriteLine($"«أ» → {method} {path} → {(int)denied.StatusCode} {Http.CodeOf(problem)}");

            Assert.Equal(HttpStatusCode.Forbidden, denied.StatusCode);
            Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));

            // ولا يتسرّب اسم منشأة «ب» ولا أي شيء من داخلها في نصّ الرفض.
            Assert.DoesNotContain("منشأة اختبار سطح HTTP", deniedText, StringComparison.Ordinal);
        }

        // وجلسة «أ» بعد كل ذلك لم تتغيّر: المحاولة لا تُضيف شركة إلى قائمة أحد.
        using HttpResponseMessage after = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, ApiFixture.TokenA));
        (string afterText, JsonElement afterSession) = await Http.BodyAsync(after);
        Console.WriteLine("جلسة «أ» بعد المحاولات: " + afterText);

        Assert.Equal(1, afterSession.GetProperty("companyCount").GetInt32());
        Assert.DoesNotContain(companyOfB, afterText, StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// <b>اعتماد لا يبلغ شركةً واحدة يُرفض برمزه، ولا يُسلَّم قائمة فارغة.</b>
    /// <para>
    /// والفرق ليس تجميلاً: قائمة فارغة تُقرأ في الشاشة «لا بيانات بعد» فينتظر المستخدم
    /// شيئاً لن يأتي؛ والرمز يقول «اعتمادك لم يُربط بمنشأة» فيعرف من يتصل به.
    /// </para>
    /// </summary>
    [Fact]
    public async Task اعتماد_لا_يبلغ_شركة_يُرفض_برمزه_لا_بقائمة_فارغة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, ApiFixture.TokenNoCompany));
        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.Equal("session.no_reachable_company", Http.CodeOf(problem));

        // الرسالتان معاً — العربية ليست ترجمة ثانية.
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detailAr").GetString()));
        Assert.False(string.IsNullOrWhiteSpace(problem.GetProperty("detail").GetString()));

        // ولا قائمة في الجسم إطلاقاً: لا حقل companies يُقرأ فراغاً.
        Assert.False(problem.TryGetProperty("companies", out _));

        // والاعتماد نفسه لا يفتح شركة أحد: النطاق يُطابَق كما هو دائماً.
        using HttpResponseMessage scoped = await api.Call(Http.Request(
            HttpMethod.Get, Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book), ApiFixture.TokenNoCompany));
        (_, JsonElement scopedProblem) = await Http.BodyAsync(scoped);

        Assert.Equal(HttpStatusCode.Forbidden, scoped.StatusCode);
        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(scopedProblem));
    }

    /// <summary>
    /// <b>الاعتماد المنقضي يُرفض برمز يخصّه — لا برمز «مرفوض» العام.</b>
    /// <para>
    /// ولماذا رمزان لا رمز واحد: من انقضت جلسته يحتاج أن يعرف أنه يدخل من جديد؛ ومن
    /// يقدّم اعتماداً مختلَقاً لا يجوز أن يتعلّم منه شيئاً. والحالتان مختلفتان عند
    /// المستخدم اختلافاً كاملاً وإن تشابهتا عند الخادم.
    /// </para>
    /// </summary>
    [Fact]
    public async Task الاعتماد_المنقضي_يُرفض_برمز_يخصّه_وعلى_كل_باب()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        foreach (string path in new[]
        {
            Http.Session,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
            Http.PostEntry(ApiTestDatabase.CompanyA),
        })
        {
            using HttpResponseMessage response = await api.Call(
                Http.Request(HttpMethod.Get, path, ApiFixture.TokenExpired));
            (string text, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"منقضٍ → {path} → {(int)response.StatusCode} {Http.CodeOf(problem)}");
            Console.WriteLine(text);

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("auth.credential_expired", Http.CodeOf(problem));
            Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);
        }

        // والرمز يختلف عن رمز الاعتماد المختلَق: حالتان لا حالة واحدة.
        TestCredential forged = TestCredential.Create(
            ApiTestDatabase.CompanyA, Guid.CreateVersion7(), ApiTestDatabase.CompanyA);

        using HttpResponseMessage rejected = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, forged));
        (_, JsonElement rejectedProblem) = await Http.BodyAsync(rejected);

        Assert.Equal(HttpStatusCode.Unauthorized, rejected.StatusCode);
        Assert.Equal("auth.credential_rejected", Http.CodeOf(rejectedProblem));
    }

    /// <summary>الجلسة بلا اعتماد مغلقة كأي باب آخر — لا «ضيف» ولا قائمة عامة.</summary>
    [Fact]
    public async Task الجلسة_بلا_اعتماد_مغلقة_ولا_تُسمّي_شركة_واحدة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, credential: null));
        (string text, JsonElement problem) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.credential_missing", Http.CodeOf(problem));
        Assert.Contains("Bearer", response.Headers.WwwAuthenticate.ToString(), StringComparison.Ordinal);

        foreach (Guid company in new[] { ApiTestDatabase.CompanyA, ApiTestDatabase.CompanyB, ApiTestDatabase.CompanyC })
        {
            Assert.DoesNotContain(
                company.ToString("D", CultureInfo.InvariantCulture), text, StringComparison.OrdinalIgnoreCase);
        }
    }

    /// <summary>
    /// <b>المنشأة التي يبلغها الاعتماد ولم تُؤسَّس تظهر بحالتها، ولا تُخفى.</b>
    /// <para>
    /// وهذا هو الفرق بين «قائمتي فارغة فاعتمادي معطوب» و«منشأتي تنتظر التأسيس»: الأولى
    /// مكالمة دعم، والثانية خطوة تالية معلومة.
    /// </para>
    /// </summary>
    [Fact]
    public async Task المنشأة_غير_المؤسَّسة_تظهر_بحالتها_ولا_تُخفى_من_القائمة()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, ApiFixture.TokenS));
        (string text, JsonElement session) = await Http.BodyAsync(response);
        Console.WriteLine(text);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // اعتماد التأسيس يبلغ ستّ عشرة منشأة، وأغلبها لم تُؤسَّس في هذا التشغيل.
        Assert.Equal(ApiFixture.SetupCompanies.Count, session.GetProperty("companyCount").GetInt32());

        JsonElement[] companies = [.. session.GetProperty("companies").EnumerateArray()];
        Assert.Equal(ApiFixture.SetupCompanies.Count, companies.Length);

        JsonElement[] notSetUp = [.. companies.Where(static c => c.GetProperty("state").GetString() == "NotSetUp")];

        // حارس اللاخواء: لو كانت كلّها مؤسَّسة لما أثبت هذا الاختبار شيئاً.
        Assert.NotEmpty(notSetUp);

        foreach (JsonElement company in notSetUp)
        {
            // ولا يُخترَع لها اسم ولا مقياس: null تعني «لم يُسنَد بعد» لا «فارغ».
            Assert.Equal(JsonValueKind.Null, company.GetProperty("nameAr").ValueKind);
            Assert.Equal(JsonValueKind.Null, company.GetProperty("decimalPlaces").ValueKind);
            Assert.Equal(JsonValueKind.Null, company.GetProperty("defaultCostCenter").ValueKind);
            Assert.Empty(company.GetProperty("nameTranslations").EnumerateArray());
        }

        // والترتيب ثابت: قائمةٌ يتغيّر ترتيبها تجعل «الشركة الثانية» شركتين في دقيقتين.
        string[] ids = [.. companies.Select(static c => c.GetProperty("companyId").GetString()!)];
        Assert.Equal([.. ids.Order(StringComparer.Ordinal)], ids);

        using HttpResponseMessage again = await api.Call(
            Http.Request(HttpMethod.Get, Http.Session, ApiFixture.TokenS));
        (string againText, _) = await Http.BodyAsync(again);
        Assert.Equal(text, againText);
    }
}
