using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الوثيقة المخدومة هي الملفّ المُودَع — لا وثيقةٌ تُبنى وقت التشغيل.</b>
/// <para>
/// هذا العقد يحرسه اليوم حارسان: <c>PublishedContractTests</c> يقارن المُودَع بما يولّده
/// السطح بايتاً بايت، و<c>Rule18</c> يقارن العميل المُولَّد بالمُودَع. وخادمٌ يبني وثيقةً
/// ثالثة عند كل طلب يضع <b>طرفاً خارج الحارسَين</b> — وهو فخ-84 من بابه الثالث: عند
/// <c>2a34cc9</c> اتّسع العقد، وخضِرت حرّاس .NET أربعةً من أربعة، ونزل عميلٌ يخالف عقده
/// المنشور لأن أحد أطرافه لم يكن محروساً. وواجهةُ توثيقٍ تعرض عقداً لم يولّده أحد ليست
/// نقصاً في ميزة — هي <b>مرجعٌ كاذب يبدو مرجعاً</b>.
/// </para>
/// <para>
/// <b>ولذلك يقارن هذا الملفّ بايتات لا معانيَ:</b> وثيقةٌ «مكافئة» بمسافات مختلفة أو
/// بترتيب مفاتيح مختلف تمرّ من أي مقارنة دلالية، وهي بالضبط الحالة التي تُثبت أن مصدراً
/// ثانياً قد وُلد.
/// </para>
/// </summary>
public sealed class DocsSurfaceTests
{
    private static string CommittedPath => Path.Combine(RepositoryPaths.Root, "contracts", "openapi", "v1.json");

    private static string Hash(byte[] bytes) => Convert.ToHexStringLower(SHA256.HashData(bytes));

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · الوثيقة المخدومة = الملفّ المُودَع، بايتاً بايت
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task الوثيقة_المخدومة_تطابق_الملفّ_المُودَع_بايتاً_بايت()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, "/openapi/v1.json", credential: null));

        byte[] served = await response.Content.ReadAsByteArrayAsync(ApiFixture.Token);
        byte[] committed = await Http.ReadBytesAsync(CommittedPath);

        Console.WriteLine($"مخدوم : {served.Length} بايت · {Hash(served)}");
        Console.WriteLine($"مُودَع  : {committed.Length} بايت · {Hash(committed)}");

        // ‏**غير الفراغ أولاً** (فخ-43): مقارنةُ مصفوفتين فارغتين تنجح ولا تُثبت شيئاً،
        // وهي بالضبط ما يقع لو توقّف الباب عن الخدمة أو رُدّ 404 بجسم فارغ.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(served.Length > 10_000, $"الوثيقة المخدومة أصغر من أن تصف سطحاً: {served.Length} بايت.");

        Assert.True(
            served.AsSpan().SequenceEqual(committed),
            "الوثيقة المخدومة على /openapi/v1.json لا تطابق contracts/openapi/v1.json بايتاً بايت.\n"
            + "والعلاج **ليس** أن يبني الخادم وثيقةً من نفسه: العقد يُولَّد بـ--emit-openapi ويُودَع،\n"
            + "والتجميعة تضمّه وقت البناء. فإن تغيّر الملفّ ولم يُعَد البناء، أعِد البناء:\n"
            + "  dotnet build src/Babel.Api/Babel.Api.csproj -c Release\n"
            + FormattableString.Invariant($"بصمة المخدوم: {Hash(served)}\nبصمة المُودَع : {Hash(committed)}"));

        // ونوع المحتوى يُعلن أنه JSON، فلا يُنزَّل الملفّ بدل أن يُقرأ.
        Assert.Equal("application/json", response.Content.Headers.ContentType?.MediaType);
    }

    /// <summary>
    /// <b>والوثيقة المخدومة وثيقة فعلاً — لا نصّ يصادف أن يتطابق.</b>
    /// <para>
    /// مقارنةُ البايتات وحدها تمرّ لو كان الملفّان **معاً** خطأً (ملفٌّ فارغ يُخدَم
    /// وملفٌّ فارغ مُودَع). فيُفحص المحتوى مرّةً على معناه.
    /// </para>
    /// </summary>
    [Fact]
    public async Task المخدوم_وثيقة_OpenAPI_حقيقية_لا_نصّ_تصادف_تطابقه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, "/openapi/v1.json", credential: null));

        (string text, JsonElement document) = await Http.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        Assert.Equal("3.1.0", document.GetProperty("openapi").GetString());
        Assert.Equal("v1", document.GetProperty("info").GetProperty("version").GetString());

        JsonElement paths = document.GetProperty("paths");
        Console.WriteLine($"مسارات في الوثيقة المخدومة: {paths.EnumerateObject().Count()}");
        Assert.True(paths.EnumerateObject().Count() >= 16, "الوثيقة المخدومة تصف أبواباً أقلّ من أن تكون هذا السطح.");

        // والباب الذي يخدمها موصوفٌ فيها: وثيقةٌ لا تذكر نفسها تترك بابها غير موثَّق.
        Assert.True(paths.TryGetProperty("/openapi/v1.json", out _), "الوثيقة لا تصف الباب الذي خدمها.");
        Assert.True(paths.TryGetProperty("/docs", out _), "الوثيقة لا تصف صفحة استعراضها.");

        Assert.Contains("chart-of-accounts", text, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · الصفحة: قائمة بذاتها، ولا رمز فيها
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task صفحة_الاستعراض_تُخدَم_ولا_تجلب_أصلاً_خارجياً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, "/docs", credential: null));

        string html = await response.Content.ReadAsStringAsync(ApiFixture.Token);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
        Assert.True(html.Length > 2_000, $"الصفحة أصغر من أن تكون صفحة: {html.Length} محرفاً.");

        // ‏**لا أصل خارجي البتّة.** وصفحةٌ تُخفق في العرض لأن شبكة التوصيل غير مبلوغة هي
        // فخ-83 نفسه: نجاحٌ على مسارٍ لا تسلكه الحركة الحقيقية. والفحص على النصّ لا على
        // النيّة — كل مخطّط شبكة يُبحث عنه بالاسم.
        foreach (string external in new[]
                 {
                     "http://", "https://", "//cdn", "//unpkg", "//jsdelivr",
                     "integrity=", "crossorigin", "@import url(",
                 })
        {
            Assert.False(
                html.Contains(external, StringComparison.OrdinalIgnoreCase),
                $"الصفحة تشير إلى أصل خارجي «{external}» — وخروجٌ مقيَّد يجعلها تُخفق بصمت (فخ-83).");
        }

        // وتقرأ العقد من هذا الخادم نفسه — مسارٌ نسبيّ، لا مضيف.
        Assert.Contains("/openapi/v1.json", html, StringComparison.Ordinal);

        // ‏**ولا رمز مطبوع فيها.** بايتاتها نفسها تُخدَم لكل طالب.
        foreach (string secret in new[]
                 {
                     "Bearer ey", "authorization: Bearer",
                     ApiFixture.TokenA.Value, ApiFixture.TokenB.Value, ApiFixture.TokenC.Value,
                 })
        {
            Assert.False(
                html.Contains(secret, StringComparison.OrdinalIgnoreCase),
                "الصفحة تحمل اعتماداً مطبوعاً — وهي تُخدَم بلا مصادقة للجميع.");
        }

        // ولا تُخزَّن الاعتمادات: الرمز في ذاكرة الصفحة وحدها.
        //
        // والفحص على **ظهور الاسم أصلاً** لا على استعماله: «‏localStorage.setItem» فحصٌ
        // يلتفّ عليه «‏window["local"+"Storage"]»، والصيغة الفظّة أقوى وثمنها أن الصفحة
        // لا تذكر هذه الأسماء ولو في شرحٍ عربي. وقد وقع ذلك فعلاً: أوّل صياغة للصفحة
        // كتبت «لا في localStorage» في ملاحظةٍ للمستخدم فأحمرّ هذا السطر — فغُيّرت
        // الصفحة ولم يُضعَّف الحارس.
        Assert.DoesNotContain("localStorage", html, StringComparison.Ordinal);
        Assert.DoesNotContain("sessionStorage", html, StringComparison.Ordinal);
        Assert.DoesNotContain("document.cookie", html, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>الشاهد الموجب على فحص «لا أصل خارجي».</b>
    /// حارسٌ يمسح نصّاً لا يستطيع أن يحوي مخالفة يمرّ ولا يُثبت شيئاً.
    /// </summary>
    [Fact]
    public void كاشف_الأصل_الخارجي_يلتقط_مخالفة_حقيقية_ولا_يلتقط_ما_ليس_مخالفة()
    {
        string[] detectors = ["http://", "https://", "//cdn", "integrity=", "crossorigin"];

        foreach (string violation in new[]
                 {
                     "<script src=\"https://cdn.example.net/swagger-ui-bundle.js\"></script>",
                     "<link rel=\"stylesheet\" href=\"//cdn.example.net/swagger-ui.css\">",
                     "<script src=\"/x.js\" integrity=\"sha384-abc\" crossorigin=\"anonymous\"></script>",
                 })
        {
            Assert.True(
                detectors.Any(d => violation.Contains(d, StringComparison.OrdinalIgnoreCase)),
                "الكاشف لم يلتقط مخالفةً حقيقية: " + violation);
        }

        foreach (string innocent in new[]
                 {
                     "fetch(\"/openapi/v1.json\")",
                     "<style>body{margin:0}</style>",
                     "const url = path;",
                 })
        {
            Assert.False(
                detectors.Any(d => innocent.Contains(d, StringComparison.OrdinalIgnoreCase)),
                "الكاشف التقط ما ليس مخالفة: " + innocent);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٣ · البابان مجهولان — وما لا يفتحه ذلك
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task بابا_التوثيق_يُفتحان_بلا_اعتماد_وما_عداهما_لا_يُفتح()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        foreach (string open in new[] { "/openapi/v1.json", "/docs", "/health" })
        {
            using HttpResponseMessage response = await api.Call(
                Http.Request(HttpMethod.Get, open, credential: null));

            Console.WriteLine($"بلا اعتماد → {open} → {(int)response.StatusCode}");
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }

        // ‏**وفتحُ بابَي التوثيق لا يفتح شيئاً وراءهما.** والسطح كلّه ما زال مغلقاً:
        // لا الجلسة، ولا أي مسار داخل نطاق شركة.
        foreach (string closed in new[]
                 {
                     "/api/v1/session",
                     Http.ChartOfAccounts(ApiTestDatabase.CompanyA),
                     Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book),
                 })
        {
            using HttpResponseMessage response = await api.Call(
                Http.Request(HttpMethod.Get, closed, credential: null));

            (_, JsonElement problem) = await Http.BodyAsync(response);
            Console.WriteLine($"بلا اعتماد → {closed} → {(int)response.StatusCode} {Http.CodeOf(problem)}");

            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("auth.credential_missing", Http.CodeOf(problem));
        }
    }

    /// <summary>
    /// <b>ما تفعله الصفحة حين تُجرَّب: عميلٌ كأي عميل.</b>
    /// <para>
    /// زرّ «جرّب» لا يملك مساراً خاصاً — يُصدر <c>fetch</c> عادياً إلى المسار نفسه
    /// بترويسة <c>Authorization</c> التي كتبها المستخدم. فيُحاكى هنا ما يفعله بالضبط،
    /// ويُثبَت أن النطاق يُنفَّذ عليه كما يُنفَّذ على <c>curl</c>: شركةٌ خارج نطاق
    /// الاعتماد تُرفض بـ<c>403 tenancy.company_out_of_scope</c> ولا يتسرّب منها شيء.
    /// </para>
    /// </summary>
    [Fact]
    public async Task طلب_من_الصفحة_يمرّ_بالنطاق_كأي_عميل_ولا_يلتفّ_عليه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // ما يرسله الزرّ حرفياً: الترويسة نفسها، والمسار نفسه، ومرجعٌ من صفحة /docs.
        HttpRequestMessage request = Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenB);
        request.Headers.Referrer = new Uri("http://localhost/docs");

        using HttpResponseMessage denied = await api.Call(request);
        (string text, JsonElement problem) = await Http.BodyAsync(denied);

        Console.WriteLine($"«ب» من صفحة /docs → دليل «أ» → {(int)denied.StatusCode} {Http.CodeOf(problem)}");

        Assert.True(
            denied.StatusCode == HttpStatusCode.Forbidden,
            $"طلبٌ صادر من صفحة التوثيق بلغ شركةً خارج نطاقه: {(int)denied.StatusCode}. الجسم:\n" + text);

        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));
        Assert.DoesNotContain("accountCount", text, StringComparison.Ordinal);

        // ولا اعتماد أصلاً من الصفحة = 401، لا تجاوز.
        HttpRequestMessage anonymous = Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), credential: null);
        anonymous.Headers.Referrer = new Uri("http://localhost/docs");

        using HttpResponseMessage unauthorised = await api.Call(anonymous);
        (_, JsonElement second) = await Http.BodyAsync(unauthorised);

        Assert.Equal(HttpStatusCode.Unauthorized, unauthorised.StatusCode);
        Assert.Equal("auth.credential_missing", Http.CodeOf(second));
    }
}
