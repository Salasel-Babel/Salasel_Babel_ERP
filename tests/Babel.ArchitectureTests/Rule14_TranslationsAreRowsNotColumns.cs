using System.Globalization;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 14 — الترجمة صفٌّ لا عمود، والاسم العربي سجلٌّ لا يغيب.</b>
/// <para>
/// ‏ADR-0021 بند 2: تعدّد اللغات معناه <b>قابلية الترجمة إلى أيّ عدد من اللغات</b>. وزوجٌ
/// ثابت <c>name_ar</c>/<c>name_en</c> عاجزٌ <b>بنيوياً</b> عن الثالثة، فنقلُه مُقرَّر.
/// و§6.3 بند 2 من القرار نفسه يقول صراحةً: <b>«لا يرخّص إدخال زوج ar/en جديد في أي حقل
/// عرض»</b> — وهذا الملف هو ذلك البند مفروضاً بدل أن يُذكَّر به.
/// </para>
/// <para>
/// <b>ولماذا حارسٌ الآن:</b> لأن البند خُولف بعد كتابته بأربعة إيداعات. قِيس على هذا
/// المستودع أن مجموعة الأسماء نمت من <b>118 ملفاً · 1,744 موضعاً</b> عند إيداع القرار
/// إلى <b>127 · 1,813</b> عند <c>develop</c> — أي أن الدين كان يتراكم أسرع مما يُوثَّق،
/// وأن الاتفاق وحده لم يمنع الزوج الجديد. (الأرقام بالطريقة المُودَعة في §4 حرفياً،
/// ومُعاد إنتاجها.)
/// </para>
/// </summary>
public sealed class Rule14_TranslationsAreRowsNotColumns
{
    /// <summary>الأشجار المفحوصة — معلنة كي يكون نطاق المسح مقروءاً لا مُستنتَجاً.</summary>
    private static readonly string[] Roots =
        ["src", "tests", "web", "contracts", "tools", "demo", "design", "data"];

    private static readonly string[] Extensions =
        [".cs", ".sql", ".json", ".ts", ".tsx", ".js", ".css", ".csproj", ".yml", ".yaml"];

    /// <summary>
    /// <b>النصف الإنجليزي</b> — وهو وحده ما يوجب القرار نقله. والاسم العربي <b>يبقى</b>
    /// (هو السجلّ)، فعدُّه مع الزوج يجعل الرقم يرتفع كلّما صحّت الهجرة.
    /// </summary>
    private static readonly Regex EnglishHalf =
        new(@"name_en|nameEn|NameEn", RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// <b>سقف الدين المتبقّي، مقيسٌ لا مُقدَّر.</b>
    /// <para>
    /// هذا الرقم <b>لا يرتفع أبداً</b>. من يحتاج رفعه فقد أدخل زوجاً جديداً، وعليه أن
    /// يصنّف نصّه بـ§6.2 أولاً: تشخيصيٌّ (يصحبه رمز ثابت، وقارئه يُصلح) فيبقى ثنائياً
    /// ويُستثنى صراحةً أدناه؛ أو نصّ عرضٍ فيُنقَل إلى جدول ترجمات ولا يُرفَع السقف.
    /// </para>
    /// <para>
    /// <b>وعند الشكّ: نصّ عرض</b> — فالتضييق يُثبَت ولا يُدَّعى (ADR-0021 §6.2).
    /// </para>
    /// </summary>
    public const int MaximumEnglishNameSites = 881;

    /// <summary>
    /// ما يُقصى من العدّ، ولكلٍّ سببٌ يُقرأ لا سببٌ يُفترض:
    /// <list type="bullet">
    ///   <item><b>هجرات الدفتر</b> — تاريخٌ مجمَّد. الهجرة التي <b>أزالت</b> العمود
    ///         تذكره بالضرورة، ومِلفّات <c>Designer</c> تصف نماذج ماضية لا تُحرَّر.</item>
    ///   <item><b>مصفوفة الترحيل في الدفتر</b> — <c>MatrixModel</c> و<c>MatrixCatalog</c>
    ///         و<c>PostingPlanner</c>: اسم الحدث الإنجليزي يُكتب في
    ///         <c>journal_line.description</c>، وهو <b>حقل مُجزَّأ</b> في الشكل القانوني
    ///         v2. نقلُه يغيّر البايتات المُوقَّعة — فهو v3 لا هجرةُ عرض. مثبَّت في
    ///         <c>DisplayTextInsideTheHashedBytesTests</c>.</item>
    /// </list>
    /// </summary>
    private static readonly string[] Exempt =
    [
        Path.Combine("src", "Babel.Ledger", "Persistence", "Migrations"),
        Path.Combine("src", "Babel.Ledger", "PostingMatrix"),
        Path.Combine("src", "Babel.Ledger", "Posting", "PostingPlanner.cs"),
    ];

    private static List<(string Path, string Code)> Sources { get; } = Load();

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · لا عمود إنجليزي ثابت في نموذج الدفتر الحيّ ولا في مخطّطه
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>الشكل الممنوع: عمود <c>name_en</c> أو خاصيّة <c>NameEn</c> على صفّ مخزَّن.</summary>
    /// <remarks>
    /// وقد <b>أضاف الشاهدُ الموجب شكلاً كان الكاشف يفوته</b>: عمود هجرة EF المكتوب
    /// <c>name_en = table.Column&lt;string&gt;(…)</c>. وهذا بالضبط ما يفعله الشاهد الموجب —
    /// يفحص الكاشف لا المجموعة، فيُظهر الثقب قبل أن يمرّ منه شيء.
    /// </remarks>
    private static readonly Regex ForbiddenColumn =
        new(@"name_en\s+text"
            + @"|""name_en"""
            + @"|\bpublic\s+string\??\s+NameEn\b"
            + @"|\bname_en\s*=\s*table\.Column",
            RegexOptions.None, TimeSpan.FromSeconds(5));

    [Fact]
    public void TheLiveLedgerModelDeclaresNoFixedEnglishNameColumn()
    {
        List<string> offenders =
        [
            .. Sources
                .Where(static file => file.Path.Replace('\\', '/').StartsWith("src/Babel.Ledger/", StringComparison.Ordinal))
                .Where(static file => ForbiddenColumn.IsMatch(file.Code))
                .Select(static file => file.Path),
        ];

        Assert.True(
            offenders.Count == 0,
            "عمودٌ إنجليزي ثابت عاد إلى نموذج الدفتر الحيّ. الترجمة صفٌّ في "
            + "ledger.name_translation لا عمود (ADR-0021 بند 2):\n"
            + string.Join('\n', offenders));
    }

    /// <summary>
    /// <b>شاهدٌ موجب: الكاشف يلتقط مخالفةً حقيقية.</b>
    /// <para>
    /// حارسٌ يمسح مجموعةً <b>لا تستطيع بنيتها أن تحوي مخالفة</b> يمرّ ولا يُثبت شيئاً.
    /// وغيرُ الفراغ وحده لا يكفي: المفحوص هنا هو <b>الكاشف نفسه</b>، بنصوصٍ هي حرفياً
    /// ما كان في <c>LedgerRows.cs</c> و<c>LedgerDbContext.cs</c> و
    /// <c>LedgerFoundation.cs</c> قبل هذه الهجرة.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDetectorActuallyCatchesTheShapeThatWasRemoved()
    {
        (string Label, string Snippet)[] realViolations =
        [
            ("صفّ الاستمرارية كما كان", "public string NameEn { get; set; } = string.Empty;"),
            ("خريطة العمود كما كانت", @"entity.Property(row => row.NameEn).HasColumnName(""name_en"");"),
            ("جدول الهجرة كما كان", @"name_en = table.Column<string>(type: ""text"", nullable: false),"),
            ("مخطّط SQL خام", "    name_en         text not null check (length(btrim(name_en)) > 0),"),
        ];

        foreach ((string label, string snippet) in realViolations)
        {
            Assert.True(
                ForbiddenColumn.IsMatch(snippet),
                "الكاشف لم يلتقط مخالفةً حقيقية — " + label + ": " + snippet);
        }

        // ولا يلتقط ما ليس مخالفة: حارسٌ يرفض كل شيء لا يميّز شيئاً.
        foreach (string innocent in new[]
                 {
                     @"entity.Property(row => row.NameAr).HasColumnName(""name_ar"");",
                     "public string NameAr { get; set; } = string.Empty;",
                     @"insert into ledger.name_translation (company_id, entity_kind) values ($1, 'account')",
                     "public required string MessageEn { get; init; }",
                 })
        {
            Assert.False(
                ForbiddenColumn.IsMatch(innocent),
                "الكاشف التقط ما ليس مخالفة: " + innocent);
        }
    }

    /// <summary>
    /// <b>والمجموعة المفحوصة تحوي فعلاً الملفّات التي كانت تحمل المخالفة.</b>
    /// نطاقٌ لا يشمل موضع العطل يمرّ أبداً ولا يحرس شيئاً.
    /// </summary>
    [Fact]
    public void TheScannedSetContainsTheFilesThatCarriedTheViolation()
    {
        foreach (string expected in new[]
                 {
                     "src/Babel.Ledger/Persistence/LedgerRows.cs",
                     "src/Babel.Ledger/Persistence/LedgerDbContext.cs",
                     "src/Babel.Ledger/Audit/LedgerAuditService.cs",
                 })
        {
            Assert.Contains(
                Sources,
                file => string.Equals(file.Path.Replace('\\', '/'), expected, StringComparison.Ordinal));
        }

        Assert.True(Sources.Count > 200, "المجموعة المفحوصة أصغر من أن تكون المستودع: " + Sources.Count);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · سقفٌ لا يرتفع — البند §6.3-2 مفروضاً
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TheEnglishNameDebtNeverGrows()
    {
        int sites = Sources.Sum(file => EnglishHalf.Count(file.Code));

        Assert.True(
            sites <= MaximumEnglishNameSites,
            string.Create(
                CultureInfo.InvariantCulture,
                $"مواضع الاسم الإنجليزي الثابت = {sites}، والسقف {MaximumEnglishNameSites}.\n")
            + "زوج ar/en جديد في حقل عرض ممنوع بنصّ ADR-0021 §6.3 بند 2. صنّف نصّك بـ§6.2 أولاً:\n"
            + "  · تشخيصي (يصحبه رمز ثابت، ولا يدخل البايتات المُجزَّأة، وعمره عمر العطل) ⇒ يبقى\n"
            + "    ثنائياً، ويُضاف موضعه إلى قائمة الإقصاء أعلاه بسببه المكتوب؛\n"
            + "  · نصّ عرض (قارئه يقرّر أو لا يفعل شيئاً) ⇒ اسم عربي إلزامي وترجمات صفوفاً.\n"
            + "  · وعند الشكّ: نصّ عرض.\n"
            + "وخفضُ السقف عند كل هجرة جزئية مطلوب — سقفٌ يبقى أعلى من الواقع يكفّ عن أن يحرس.");

        // والسقف يُخفَّض حين يُخفَّض الدين: فارقٌ كبير يعني حارساً صار زينة.
        Assert.True(
            sites > MaximumEnglishNameSites - 40,
            string.Create(
                CultureInfo.InvariantCulture,
                $"المواضع {sites} أقلّ من السقف {MaximumEnglishNameSites} بفارق كبير. ")
            + "اخفض MaximumEnglishNameSites إلى العدد المقيس، وإلا صار السقف يسمح بعودة ما أُزيل.");
    }

    // ═══════════════════════════════════════════════════════════════════════

    private static List<(string Path, string Code)> Load()
    {
        List<(string Path, string Code)> files = [];

        foreach (string root in Roots)
        {
            string absolute = Path.Combine(RepositoryLayout.Root, root);

            if (!Directory.Exists(absolute))
            {
                continue;
            }

            foreach (string path in Directory.EnumerateFiles(absolute, "*", SearchOption.AllDirectories))
            {
                string relative = Path.GetRelativePath(RepositoryLayout.Root, path);
                string slashes = relative.Replace('\\', '/');

                if (slashes.Contains("/bin/", StringComparison.Ordinal)
                    || slashes.Contains("/obj/", StringComparison.Ordinal)
                    || slashes.Contains("/node_modules/", StringComparison.Ordinal))
                {
                    continue;
                }

                if (!Extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (Exempt.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)))
                {
                    continue;
                }

                files.Add((slashes, StripComments(File.ReadAllText(path))));
            }
        }

        return files;
    }

    /// <summary>
    /// الشيفرة بلا تعليقات — كما في القاعدة 12، وللسبب نفسه: قاعدةٌ تعدّ الشرح تُجبر
    /// المهندس على <b>حذف الشرح</b> ليمرّ البناء، وهذا الملف نفسه يشرح الشكل الممنوع.
    /// </summary>
    private static string StripComments(string text)
    {
        text = Regex.Replace(text, @"/\*.*?\*/", " ", RegexOptions.Singleline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"//.*$", " ", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        text = Regex.Replace(text, @"--.*$", " ", RegexOptions.Multiline, TimeSpan.FromSeconds(5));
        return text;
    }
}
