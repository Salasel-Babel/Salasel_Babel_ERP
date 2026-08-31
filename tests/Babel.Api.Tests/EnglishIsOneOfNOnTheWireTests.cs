using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>الإنجليزية واحدة من N على السلك — لا نصف الاثنين.</b>
/// <para>
/// ‏ADR-0021 بند 2: تعدّد اللغات معناه <b>قابلية الترجمة إلى أيّ عدد من اللغات</b>. وحقلٌ
/// ثابت اسمه <c>nameEn</c> بجانب <c>nameAr</c> يمنح الإنجليزية <b>امتيازاً بنيوياً</b>
/// ينفيه القرار: المحاسب الأردي أو الهندي يقرأ إنجليزيةً بدل لغته، ولا علاج لذلك إلا
/// <b>تعديل عقد</b> لكل لغة تُضاف. وقد كان الحقل باقياً <b>مهجوراً ومشتقّاً</b> من الترجمة
/// ذات الوسم <c>en</c>، وحُذف بتعديل v1 في مكانه (‏ADR-0018، تعديل 2026-08-26).
/// </para>
/// <para>
/// <b>ولماذا حارسٌ لا اتفاق:</b> لأن الحقل عاد مرّةً بعد أن نزل عموده من قاعدة البيانات —
/// بقي في العقد «حفاظاً على التوافق» بينما لا مستهلك يحفظ له توافقاً. والحارس هنا يمسح
/// <b>العقد المُودَع نفسه</b>، وهو الشيء الذي يقرؤه فريق الواجهة، لا شيفرةً تولّده.
/// </para>
/// </summary>
public sealed class EnglishIsOneOfNOnTheWireTests
{
    /// <summary>
    /// <b>النصف الإنجليزي من زوج اسمٍ ثابت</b> — وهو وحده الممنوع.
    /// <para>
    /// ولا يلتقط النصّ <b>التشخيصي</b>: <c>messageEn</c> و<c>detailEn</c> و<c>noteEn</c>
    /// ثنائية «حالياً» بقرار المالك (‏ADR-0021 §6.2)، لأن قارئها من <b>يصلح</b> لا من
    /// <b>يقرّر</b>، ويصحبها رمز ثابت. والتضييق يُثبَت ولا يُدَّعى، فالنمط يسمّي ما يمنعه.
    /// </para>
    /// </summary>
    private static readonly Regex FixedEnglishNameHalf =
        new(@"^(.*[A-Za-z0-9_])?[Nn]ame_?[Ee]n$", RegexOptions.None, TimeSpan.FromSeconds(5));

    private static string ContractPath => Path.Combine(RepositoryPaths.Root, "contracts", "openapi", "v1.json");

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · لا نصف إنجليزي ثابت في أي مخطّط من مخطّطات العقد
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task لا_حقل_اسم_إنجليزي_ثابت_في_أي_مخطّط_من_العقد_المنشور()
    {
        using JsonDocument document = JsonDocument.Parse(await Http.ReadTextAsync(ContractPath));
        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");

        List<string> offenders = [];
        int properties = 0;

        foreach (JsonProperty schema in schemas.EnumerateObject())
        {
            if (!schema.Value.TryGetProperty("properties", out JsonElement fields))
            {
                continue;
            }

            foreach (JsonProperty field in fields.EnumerateObject())
            {
                properties++;

                if (FixedEnglishNameHalf.IsMatch(field.Name))
                {
                    offenders.Add(schema.Name + "." + field.Name);
                }
            }
        }

        Console.WriteLine($"مخطّطات: {schemas.EnumerateObject().Count()} · حقول ممسوحة: {properties}");

        Assert.True(
            offenders.Count == 0,
            "نصفٌ إنجليزي ثابت عاد إلى العقد. الإنجليزية مدخلٌ في nameTranslations لا حقلٌ "
            + "مستقلّ (ADR-0021 بند 2):\n" + string.Join('\n', offenders));
    }

    /// <summary>
    /// <b>والمسح يبلغ فعلاً المخطّط الذي كان يحمل المخالفة، وحقولَه الباقية.</b>
    /// مسحٌ لا يبلغ موضع العطل يمرّ أبداً ولا يحرس شيئاً — وغيرُ الفراغ وحده لا يكفي.
    /// </summary>
    [Fact]
    public async Task المخطّط_الذي_حمل_المخالفة_ما_زال_ممسوحاً_وحقلاه_الباقيان_في_مكانهما()
    {
        using JsonDocument document = JsonDocument.Parse(await Http.ReadTextAsync(ContractPath));
        JsonElement row = document.RootElement
            .GetProperty("components").GetProperty("schemas").GetProperty("TrialBalanceRow");

        JsonElement fields = row.GetProperty("properties");

        Assert.True(fields.TryGetProperty("nameAr", out _), "السجلّ العربي غاب — وهو ما لا يُحذف أبداً.");
        Assert.True(fields.TryGetProperty("nameTranslations", out _), "الترجمات غابت، فلا بديل عن المحذوف.");
        Assert.False(fields.TryGetProperty("nameEn", out _), "nameEn ما زال في مخطّط صفّ الميزان.");

        string[] required = [.. row.GetProperty("required").EnumerateArray().Select(static x => x.GetString()!)];
        Assert.Contains("nameAr", required);
        Assert.Contains("nameTranslations", required);
        Assert.DoesNotContain("nameEn", required);
    }

    /// <summary>
    /// <b>شاهدٌ موجب: الكاشف يلتقط مخالفةً حقيقية، ولا يلتقط ما ليس مخالفة.</b>
    /// <para>
    /// حارسٌ يمسح مجموعةً <b>لا تستطيع بنيتها أن تحوي مخالفة</b> يمرّ ولا يُثبت شيئاً.
    /// والنصوص أدناه هي حرفياً أسماء الحقول كما كانت في العقد قبل هذا الحذف، ومعها
    /// أسماءُ النصّ التشخيصي التي <b>يجب</b> أن تمرّ بنصّ §6.2.
    /// </para>
    /// </summary>
    [Fact]
    public void الكاشف_يلتقط_المخالفة_الحقيقية_ولا_يلتقط_النصّ_التشخيصي()
    {
        foreach (string violation in new[] { "nameEn", "name_en", "accountNameEn", "roleNameEn", "branch_name_en" })
        {
            Assert.True(
                FixedEnglishNameHalf.IsMatch(violation),
                "الكاشف لم يلتقط مخالفةً حقيقية: " + violation);
        }

        foreach (string innocent in new[]
                 {
                     "nameAr", "nameTranslations", "messageEn", "detailEn", "noteEn",
                     "reasonEn", "titleEn", "summaryEn", "name", "displayName",
                 })
        {
            Assert.False(
                FixedEnglishNameHalf.IsMatch(innocent),
                "الكاشف التقط ما ليس مخالفة: " + innocent);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · والسلك الحيّ يقول ما يقوله العقد — لا الوثيقة وحدها
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>الجسم الحيّ لميزان المراجعة: سجلٌّ عربي وترجمات، ولا nameEn.</b>
    /// <para>
    /// عقدٌ نظيف وخادمٌ يرسل الحقل هو أسوأ الحالتين: العميل المُولَّد من العقد يرفض
    /// الحقل المجهول، فالعطل لا يظهر إلا عند أول قارئ خامّ. والاختبار <b>يبني حالته</b>:
    /// يرحّل قيداً في فترة يملكها وحده، ثم يقرأ ميزانها.
    /// </para>
    /// </summary>
    [Fact]
    public async Task جسم_ميزان_المراجعة_الحيّ_يحمل_السجلّ_وترجماته_ولا_يحمل_nameEn()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage posted = await api.Call(Http.Request(
            HttpMethod.Post,
            Http.PostEntry(ApiTestDatabase.CompanyA),
            ApiFixture.TokenA,
            Payloads.BalancedEntry(Payloads.Key("one-of-n"), documentDate: "2026-11-09")));

        Assert.Equal(HttpStatusCode.Created, posted.StatusCode);

        using HttpResponseMessage balance = await api.Call(Http.Request(
            HttpMethod.Get,
            Http.TrialBalance(ApiTestDatabase.CompanyA, ApiTestDatabase.Book, "2026-11"),
            ApiFixture.TokenA));

        (string text, JsonElement trial) = await Http.BodyAsync(balance);
        Console.WriteLine(text);
        Assert.Equal(HttpStatusCode.OK, balance.StatusCode);

        JsonElement[] rows = [.. trial.GetProperty("rows").EnumerateArray()];
        Assert.NotEmpty(rows);

        foreach (JsonElement row in rows)
        {
            Assert.False(row.TryGetProperty("nameEn", out _), "صفّ ميزان يحمل nameEn على السلك الحيّ.");
            Assert.False(string.IsNullOrWhiteSpace(row.GetProperty("nameAr").GetString()));
            Assert.Equal(JsonValueKind.Array, row.GetProperty("nameTranslations").ValueKind);
        }

        // والإنجليزية موجودة **مدخلاً** لا حقلاً: مخطّط الحسابات المبذور يحمل ترجمة en،
        // فغيابها من المدخلات يعني أن الحذف أخذ معه الترجمة نفسها لا الحقل وحده.
        Assert.Contains(
            rows,
            static row => row.GetProperty("nameTranslations").EnumerateArray()
                .Any(static entry => entry.GetProperty("name").GetString() == "en"));
    }

    /// <summary>
    /// <b>وجسم المشروع الحيّ كذلك — لأن المخطّط النظيف لا يُثبت أن المُسلسِل نظيف.</b>
    /// <para>
    /// المسح الأول أعلاه يقرأ <b>العقد</b>، فيبلغ مخطّطات المقاولات كلها بالضرورة. وهذا
    /// الاختبار يقرأ <b>ما يخرج من المقبس</b> على وحدةٍ ثانية: تسجيلُ مشروعٍ باسمٍ عربي
    /// وترجمةٍ واحدة، ثم قراءةُ الجسم المُعاد.
    /// </para>
    /// <para>
    /// <b>وموضعه هنا لا في مجموعة المقاولات مقصود:</b> حارسُ القاعدة 14 يعدّ مواضع
    /// «النصف الإنجليزي الثابت» في الشجرة المتعقَّبة، وشاهدٌ <b>موجب</b> يكتب الشكل
    /// الممنوع ليؤكّد غيابه يرفع الدين الذي يقيسه الحارس — وهو انعكاسٌ تامّ لما يقيسه.
    /// ولذلك هذا الملفّ وحده مُقصى من العدّ بسببٍ مكتوب، وكل شاهدٍ من هذا النوع يُجمع
    /// فيه بدل أن يُنثر في المجموعات فتُقصى واحدةً بعد أخرى حتى يصير الإقصاء هو القاعدة.
    /// </para>
    /// <para>
    /// <b>وعلى الشركة «ب»</b>: المقاولات وحدةٌ اختيارية، والشركة «ب» وحدها تشتريها
    /// (‏<c>ApiFixture</c>) لأنها وحدها اشترت المخزون الذي تعتمد عليه.
    /// </para>
    /// </summary>
    [Fact]
    public async Task جسم_المشروع_الحيّ_يحمل_السجلّ_وترجماته_ولا_يحمل_nameEn()
    {
        ApiProcess api = await ApiFixture.DefaultAsync();

        using HttpResponseMessage created = await api.Call(Http.Request(
            HttpMethod.Post,
            string.Create(
                System.Globalization.CultureInfo.InvariantCulture,
                $"/api/v1/companies/{ApiTestDatabase.CompanyB:D}/projects"),
            ApiFixture.TokenB,
            $$"""
              {
                "code": "{{Documents.Number("PRJ")}}",
                "nameAr": "مشروع شاهد اللغات",
                "nameTranslations": [{ "name": "en", "value": "Language witness project" }],
                "startedOn": "2026-01-01"
              }
              """));

        (string text, JsonElement project) = await Http.BodyAsync(created);
        Console.WriteLine(text);
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);

        Assert.False(project.TryGetProperty("nameEn", out _), "مشروعٌ يحمل nameEn على السلك الحيّ.");
        Assert.Equal("مشروع شاهد اللغات", project.GetProperty("nameAr").GetString());

        // والإنجليزية **مدخل** لا حقل: ما أُرسل وسماً عاد وسماً، فالترجمة حُفظت صفّاً.
        JsonElement entry = Assert.Single(project.GetProperty("nameTranslations").EnumerateArray().ToList());
        Assert.Equal("en", entry.GetProperty("name").GetString());
        Assert.Equal("Language witness project", entry.GetProperty("value").GetString());
    }
}
