using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>شروط الترحيل تُعرَف قبل الترحيل، لا بالرفض بعده.</b>
/// <para>
/// <b>ما الذي كان معطوباً بالضبط:</b> السطح المنشور كان أربع عشرة نقطة نهاية <b>ولا واحدة
/// منها تكشف دليل الحسابات</b>، بينما حرّاس الترحيل ترفض على أسبابٍ لا يستطيع العميل
/// اكتشافها: <c>ledger.posting.missing_subledger</c> حين يغيب طرف الأستاذ المساعد،
/// و<c>guard.GR-COA-002</c> حين يغيب بُعد إلزامي. والرسالتان ممتازتان — تسمّيان الحساب
/// والطرف والبُعد — لكنّ بلوغهما كان <b>بالترحيل والرفض</b> وحده. وقد وقع ذلك مقيساً: شاشة
/// قيدٍ يدوية بُنيت من العقد المنشور وحده لم تستطع تكوين قيد صالح من أول محاولة.
/// </para>
/// <para>
/// <b>والأرقام في هذا الملفّ مقروءة من الدليل المُودَع</b>
/// (<c>data/chart-of-accounts/accounts.csv</c>) لا مكتوبةً بيد: حارسٌ يقارن الاستجابة برقمٍ
/// منسوخ في مكانين ينحرف في أحدهما عند أول تعديل دليل، فيصير أخضر عن دليلٍ لم يعد هو.
/// </para>
/// </summary>
public sealed class ChartOfAccountsTests
{
    // ═══════════════════════════════════════════════════════════════════════
    // ١ · الدليل يصل، وشروطه معه
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task الدليل_يصل_كاملاً_وعدّاداه_يطابقان_الدليل_المُودَع()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (_, JsonElement chart) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement[] accounts = [.. chart.GetProperty("accounts").EnumerateArray()];
        ChartFacts expected = await ChartFacts.FromCommittedChartAsync();

        Console.WriteLine(
            $"من الدليل المُودَع: {expected.Total} حساباً · {expected.Postable} يقبل الترحيل · "
            + $"{expected.RequiringSubledger} يطلب طرفاً · {expected.RequiringDimension} يطلب بُعداً");
        Console.WriteLine(
            $"من الاستجابة      : {accounts.Length} حساباً · {chart.GetProperty("postableCount").GetInt32()} يقبل الترحيل");

        // ‏غير الفراغ وحده لا يكفي (فخ-43): حلقةُ فحصٍ لا تدور تمرّ فارغة.
        Assert.NotEmpty(accounts);

        Assert.Equal(expected.Total, accounts.Length);
        Assert.Equal(expected.Total, chart.GetProperty("accountCount").GetInt32());
        Assert.Equal(expected.Postable, chart.GetProperty("postableCount").GetInt32());
        Assert.Equal(expected.Postable, accounts.Count(static a => a.GetProperty("postable").GetBoolean()));
    }

    [Fact]
    public async Task كل_حساب_يطلب_طرفاً_أو_بُعداً_يُعلنه_على_السلك()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (_, JsonElement chart) = await Http.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement[] postable =
            [.. chart.GetProperty("accounts").EnumerateArray().Where(static a => a.GetProperty("postable").GetBoolean())];

        Assert.NotEmpty(postable);

        int subledger = postable.Count(static a =>
            a.GetProperty("subledgerType").GetString() is not ("none" or null));

        int dimensions = postable.Count(static a =>
            a.GetProperty("requiredDimensions").GetArrayLength() > 0);

        ChartFacts expected = await ChartFacts.FromCommittedChartAsync();

        Console.WriteLine(
            $"يطلب طرف أستاذ مساعد: {subledger} (المتوقَّع {expected.RequiringSubledger}) · "
            + $"يطلب بُعداً إلزامياً: {dimensions} (المتوقَّع {expected.RequiringDimension})");

        // وهذان الرقمان بالضبط هما ما كان **مجهولاً للعميل ومعلوماً للخادم**.
        Assert.Equal(expected.RequiringSubledger, subledger);
        Assert.Equal(expected.RequiringDimension, dimensions);
    }

    /// <summary>
    /// <b>الشرط يُقرأ عن حسابٍ بعينه — لا كإحصاء فقط.</b>
    /// <para>
    /// إحصاءٌ صحيح على حقلٍ يحمل القيمة الخطأ في كل صفّ يمرّ. فيُفحَص هنا حسابٌ معلوم
    /// شرطُه في الدليل المُودَع، حقلاً حقلاً.
    /// </para>
    /// </summary>
    [Fact]
    public async Task حساب_العملاء_يُعلن_أنه_يطلب_عميلاً_قبل_أن_يُرحَّل_عليه()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (_, JsonElement chart) = await Http.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement receivable = Single(chart, "1301");

        Console.WriteLine(receivable.ToString());

        Assert.True(receivable.GetProperty("postable").GetBoolean());
        Assert.Equal("customer", receivable.GetProperty("subledgerType").GetString());
        Assert.Equal("asset", receivable.GetProperty("accountType").GetString());
        Assert.Equal("debit", receivable.GetProperty("naturalSide").GetString());
        Assert.Equal(4, receivable.GetProperty("level").GetInt32());

        // والاسم العربي سجلٌّ لا ترجمة أولى، والإنجليزية مدخلٌ في الترجمات (ADR-0021).
        Assert.False(string.IsNullOrWhiteSpace(receivable.GetProperty("nameAr").GetString()));
        Assert.False(receivable.TryGetProperty("nameEn", out _));
        Assert.Contains(
            receivable.GetProperty("nameTranslations").EnumerateArray(),
            static t => t.GetProperty("name").GetString() == "en");

        // وحسابٌ يطلب بُعداً إلزامياً يُعلنه باسمه.
        JsonElement payroll = Single(chart, "5501");
        Console.WriteLine(payroll.ToString());
        Assert.True(payroll.GetProperty("postable").GetBoolean());
        Assert.Equal(
            "cost_center",
            Assert.Single(payroll.GetProperty("requiredDimensions").EnumerateArray()).GetString());

        // وحسابٌ لا يقبل إلا عملة الشركة يُعلن ذلك أيضاً.
        Assert.Equal("company_only", Single(chart, "1305").GetProperty("currencyMode").GetString());
    }

    /// <summary>
    /// <b>الحساب التجميعي يصل، ويصل مُعلَناً أنه لا يقبل الترحيل.</b>
    /// <para>
    /// وهذا هو الفارق بين «الدليل كلّه» و«الأوراق وحدها»: الأب يصل ليُبنى به التبويب،
    /// و<c>postable = false</c> عليه يمنع عرضَه خياراً. وإسقاطُه كان سيدفع العميل إلى
    /// اشتقاق الشجرة من بادئات الرموز.
    /// </para>
    /// </summary>
    [Fact]
    public async Task الحساب_التجميعي_يصل_ويُعلن_أنه_لا_يقبل_الترحيل_وأبوه_مذكور()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (_, JsonElement chart) = await Http.BodyAsync(response);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        JsonElement root = Single(chart, "1");
        Assert.False(root.GetProperty("postable").GetBoolean());
        Assert.Equal(1, root.GetProperty("level").GetInt32());
        Assert.Equal(JsonValueKind.Null, root.GetProperty("parentCode").ValueKind);

        // والقابل للترحيل ورقةٌ دائماً — ‏GR-COA-001 من باب البيانات.
        foreach (JsonElement account in chart.GetProperty("accounts").EnumerateArray())
        {
            if (account.GetProperty("postable").GetBoolean())
            {
                Assert.Equal(4, account.GetProperty("level").GetInt32());
            }

            // وكل حساب غير جذر يسمّي أباه، فلا يُشتقّ التبويب من بادئة الرمز.
            if (account.GetProperty("level").GetInt32() > 1)
            {
                Assert.Equal(JsonValueKind.String, account.GetProperty("parentCode").ValueKind);
            }
        }
    }

    /// <summary>الترتيب حرفي ثابت — استجابتان تُقارَنان لا مجموعتان (فخ-10).</summary>
    [Fact]
    public async Task الحسابات_تصل_مرتّبة_ترتيباً_حرفياً_ثابتاً()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (_, JsonElement chart) = await Http.BodyAsync(response);

        string[] codes =
            [.. chart.GetProperty("accounts").EnumerateArray().Select(static a => a.GetProperty("accountCode").GetString()!)];

        Assert.NotEmpty(codes);
        Assert.Equal([.. codes.Order(StringComparer.Ordinal)], codes);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · النطاق والاستحقاق
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>شركةٌ لا يبلغها الاعتماد تُرفض ولا يتسرّب عنها حرف.</b>
    /// <para>
    /// ودليل الحسابات <b>أخطر من غيره</b> في هذا الباب: تسريبُه يكشف بنية عمل المستأجر
    /// الآخر — أقسامه وعقاراته وبنوكه — لا رقماً واحداً.
    /// </para>
    /// </summary>
    [Fact]
    public async Task دليل_شركةٍ_لا_يبلغها_الاعتماد_يُرفض_ولا_يُسرَّب_منه_شيء()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        // «أ» يقرأ دليله فعلاً — فالرفض على «ب» رفضُ نطاق لا غيابُ مسار.
        using HttpResponseMessage mine = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenA));

        (string served, JsonElement chart) = await Http.BodyAsync(mine);
        Assert.Equal(HttpStatusCode.OK, mine.StatusCode);

        string witness = chart.GetProperty("accounts")[0].GetProperty("nameAr").GetString()!;

        // و«ب» يطلب دليل «أ».
        using HttpResponseMessage denied = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), ApiFixture.TokenB));

        (string text, JsonElement problem) = await Http.BodyAsync(denied);
        Console.WriteLine(text);

        // ‏**رمز الحالة أولاً، ثم الجسم.** ولو قُرئ الرمز من الجسم قبل الحكم على الحالة،
        // لخرج الحارس عند العبور بـKeyNotFoundException عن حقل مفقود — أي بعُطلٍ يقرأ
        // «اختبار معطوب» لا «مستأجر قرأ دليل مستأجر آخر». وهذا مقيس هنا لا مفترَض:
        // نُزع فحص النطاق عمداً فكان هذا بالضبط ما ظهر.
        Assert.True(
            denied.StatusCode == HttpStatusCode.Forbidden,
            $"«ب» طلب دليل «أ» فوصله {(int)denied.StatusCode}. وعبورٌ هنا يعني أن مستأجراً "
            + "قرأ بنية عمل مستأجر آخر — أقسامه وعقاراته وبنوكه. الجسم:\n" + text);

        Assert.Equal("tenancy.company_out_of_scope", Http.CodeOf(problem));

        // ولا يتسرّب شيء: لا اسم حساب، ولا عدّاد، ولا رمز حساب واحد.
        Assert.DoesNotContain(witness, text, StringComparison.Ordinal);
        Assert.DoesNotContain("accountCount", text, StringComparison.Ordinal);
        Assert.DoesNotContain("postableCount", text, StringComparison.Ordinal);
        Assert.DoesNotContain("subledgerType", text, StringComparison.Ordinal);

        // والشاهد الموجب: النصّ المسرَّب لو تسرّب لكان موجوداً في استجابة «أ».
        Assert.Contains(witness, served, StringComparison.Ordinal);

        // ومعرّف مشوَّه يُرفض شكلاً قبل أي شيء آخر.
        using HttpResponseMessage malformed = await api.Call(Http.Request(
            HttpMethod.Get, "/api/v1/companies/not-a-guid/chart-of-accounts", ApiFixture.TokenA));

        (_, JsonElement bad) = await Http.BodyAsync(malformed);
        Assert.Equal(HttpStatusCode.BadRequest, malformed.StatusCode);
        Assert.Equal("tenancy.company_id_malformed", Http.CodeOf(bad));
    }

    [Fact]
    public async Task طلب_الدليل_بلا_اعتماد_يُغلق_عليه_الباب()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyA), credential: null));

        (_, JsonElement problem) = await Http.BodyAsync(response);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("auth.credential_missing", Http.CodeOf(problem));
    }

    /// <summary>
    /// <b>المستأجر الذي انحدر إلى «القراءة فقط» ما زال يقرأ دليله.</b>
    /// <para>
    /// ‏ADR-0034: انقطاع الاشتراك ينحدر إلى <b>القراءة فقط لا إلى المنع</b>. وقراءةٌ
    /// تُمنَع عن عميلٍ متأخّر عن السداد تعني أنه لا يستطيع حتى أن يرى دفتره ليُخرج منه
    /// بياناته — وهو ما لا يفعله هذا المنتج.
    /// </para>
    /// </summary>
    [Fact]
    public async Task مستأجر_وحدته_للقراءة_فقط_يقرأ_دليل_حساباته()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get, Http.ChartOfAccounts(ApiTestDatabase.CompanyC), ApiFixture.TokenC));

        (_, JsonElement chart) = await Http.BodyAsync(response);
        Console.WriteLine($"المستأجر «ج» (للقراءة فقط) → {response.StatusCode}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        // ويصل الدليل عامراً لا فارغاً: 200 على جسمٍ فارغ رفضٌ متنكّر.
        Assert.NotEmpty(chart.GetProperty("accounts").EnumerateArray());
        Assert.True(chart.GetProperty("postableCount").GetInt32() > 0);
    }

    // ═══════════════════════════════════════════════════════════════════════

    private static JsonElement Single(JsonElement chart, string code) =>
        Assert.Single(
            chart.GetProperty("accounts").EnumerateArray(),
            a => a.GetProperty("accountCode").GetString() == code);

    /// <summary>
    /// حقائق الدليل مقروءةً من الملفّ المُودَع — <b>مصدرٌ واحد لا رقمٌ منسوخ</b>.
    /// </summary>
    /// <param name="Total">عدد الحسابات.</param>
    /// <param name="Postable">ما يقبل الترحيل.</param>
    /// <param name="RequiringSubledger">ما يطلب طرف أستاذ مساعد.</param>
    /// <param name="RequiringDimension">ما يطلب بُعداً إلزامياً.</param>
    private sealed record ChartFacts(int Total, int Postable, int RequiringSubledger, int RequiringDimension)
    {
        public static async Task<ChartFacts> FromCommittedChartAsync()
        {
            string path = Path.Combine(RepositoryPaths.Root, "data", "chart-of-accounts", "accounts.csv");
            string[] lines = (await Http.ReadTextAsync(path)).Split('\n', StringSplitOptions.RemoveEmptyEntries);

            string[] header = lines[0].Split(',');
            int postable = Array.IndexOf(header, "is_postable");
            int subledger = Array.IndexOf(header, "subledger_type");
            int dimensions = Array.IndexOf(header, "required_dimensions");

            Assert.True(postable >= 0 && subledger >= 0 && dimensions >= 0, "أعمدة الدليل المُودَع تغيّرت.");

            int total = 0;
            int post = 0;
            int withSubledger = 0;
            int withDimension = 0;

            foreach (string line in lines.Skip(1))
            {
                // الدليل المُودَع لا يحمل فاصلة داخل حقل مقتبس في هذه الأعمدة — والفحص
                // أدناه يُسقط أي صفّ يخالف ذلك بدل أن يعدّه خطأً بصمت.
                string[] cells = line.TrimEnd('\r').Split(',');
                if (cells.Length <= dimensions)
                {
                    continue;
                }

                total++;

                if (cells[postable].Trim() != "true")
                {
                    continue;
                }

                post++;

                if (cells[subledger].Trim() is not ("" or "none"))
                {
                    withSubledger++;
                }

                if (cells[dimensions].Trim().Length > 0)
                {
                    withDimension++;
                }
            }

            Assert.True(total > 100, "الدليل المُودَع أصغر من أن يُقاس عليه: " + total);
            return new ChartFacts(total, post, withSubledger, withDimension);
        }
    }
}
