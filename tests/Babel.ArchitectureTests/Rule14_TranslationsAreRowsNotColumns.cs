using System.Diagnostics;
using System.Globalization;
using System.Text;
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
    /// <para>
    /// <b>ثم خُفِض من 864 إلى 862</b> حين نالت النواة مخطّطها الحقيقي (تثبيت التأسيس).
    /// والفرق موضعان لا أكثر، <b>مقيسان ومُفسَّران</b>: كان في <c>Babel.Core</c> صفُّ
    /// مستأجر ميت — <c>TenantRow</c> بعمودَي <c>name_ar</c>/<c>name_en</c>، وسياقٌ
    /// يخرّطهما — لا يُسجَّل في أي حاوية ولا تقابله هجرة واحدة. فلمّا صار للنواة مخطّط
    /// يُنشر فعلاً، حُذف الصفّ الميت ولم يُهاجَر: الاسم العربي عمودٌ على الكيان لأنه
    /// السجلّ، والترجمات صفوفٌ في <c>core.name_translation</c>. أي أن المخطّط الجديد
    /// <b>لم يُدخل موضعاً واحداً</b>، وأن الموضعين الساقطين هما ما كان سيُولَد لو نُقل
    /// الصفّ الميت كما هو.
    /// </para>
    /// <para>
    /// <b>وقبلها خُفِض من 868 إلى 864</b> حين حُذف <c>nameEn</c> من العقد المنشور (‏ADR-جديد
    /// «نافذة العقد تُغلق قبل النشر»). والفرق أربعة لا أكثر <b>مقيسٌ ومُفسَّر</b>: الحقل
    /// كان يشغل سبعة مواضع معدودة (الشكل على السلك، والباعث، والعميل المُولَّد، والخادم
    /// الوهمي)، وسجَّل التعديلُ نفسَه أربعة مواضع جديدة — سطرا التعديل المُسجَّل في وصف
    /// العقد، عربياً وإنجليزياً، في الباعث وفي الملفّ المُولَّد منه. و<b>تسمية المحذوف
    /// في وثيقة الحذف ليست ديناً يُخفى</b>: هي ما يجعل من يقرأ العقد بعد سنتين يعرف أن
    /// الحقل حُذف عمداً ولم يسقط سهواً.
    /// </para>
    /// </summary>
    public const int MaximumEnglishNameSites = 862;

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
    ///   <item><b>حارس السلك النظير</b> — <c>tests/Babel.Api.Tests/EnglishIsOneOfNOnTheWireTests.cs</c>:
    ///         مجموعته المفحوصة هي <b>العقد المنشور</b> لا هذه الشجرة، وهو يكتب الشكل الممنوع
    ///         في شواهده الموجبة بالضرورة — كما يفعل هذا الملف تماماً وللسبب نفسه. وعدُّه
    ///         يجعل <b>حذف الحقل من العقد يرفع الدين الذي يقيسه الحارس</b>، وهو انعكاس تامّ
    ///         لما يُفترض أن يقيسه.</item>
    ///   <item><b>هذا الملفّ نفسه</b> — الحارس يكتب الشكل الممنوع بالضرورة: في تعبيره
    ///         النمطي، وفي شواهده الموجبة التي تُطعمه مخالفاتٍ حقيقية. وعدُّه يجعل
    ///         <b>تقويةَ الحارس ترفع الدين الذي يقيسه</b> — وهو نفس العطب الذي حلّته
    ///         القاعدة 12 بتجريد التعليقات، إلا أنّ ما هنا شيفرةٌ لا تعليق. مقيس:
    ///         <b>16 موضعاً</b> يساهم بها هذا الملفّ.</item>
    /// </list>
    /// </summary>
    private static readonly string[] Exempt =
    [
        Path.Combine("src", "Babel.Ledger", "Persistence", "Migrations"),
        Path.Combine("src", "Babel.Ledger", "PostingMatrix"),
        Path.Combine("src", "Babel.Ledger", "Posting", "PostingPlanner.cs"),
        Path.Combine("tests", "Babel.ArchitectureTests", "Rule14_TranslationsAreRowsNotColumns.cs"),
        Path.Combine("tests", "Babel.Api.Tests", "EnglishIsOneOfNOnTheWireTests.cs"),
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

    /// <summary>
    /// <b>ناتج البناء غير المتعقَّب لا يدخل العدّ — مُثبَتاً بزرع ملفّ، لا بالثقة.</b>
    /// <para>
    /// هذا هو الحارس على الحارس. العطل الذي يمنعه <b>وقع فعلاً</b>: كان المسح يقرأ القرص،
    /// فيبتلع <c>web/dist/</c>. وشجرتان محتواهما متطابق بايتاً بايت أعطتا <b>881 و882</b>،
    /// لأن حزمة المصغِّر في إحداهما وضعت الرمز بعد <c>//</c> فجُرِّد، وفي الأخرى لم تفعل.
    /// </para>
    /// <para>
    /// والزرع هنا في مجلّد <b>مُهمَل في <c>.gitignore</c></b>، وهو ما يجعل الشاهد صادقاً:
    /// لو عاد المسح إلى القرص لالتقطه فوراً.
    /// </para>
    /// </summary>
    [Fact]
    public void GeneratedBuildOutputOnDiskNeverEntersTheCount()
    {
        int before = Load().Sum(file => EnglishHalf.Count(file.Code));

        string planted = Path.Combine(RepositoryLayout.Root, "web", "dist", "__rule14_witness.js");
        Directory.CreateDirectory(Path.GetDirectoryName(planted)!);

        try
        {
            // ثلاثة مواضع في سطور مستقلّة، فلا يبتلعها تجريد التعليقات بحال.
            File.WriteAllText(planted, "export const nameEn = 1;\nconst name_en = 2;\nlet NameEn = 3;\n");

            Assert.True(
                File.Exists(planted),
                "لم يُزرع ملف الشاهد أصلاً، فالاختبار لا يفحص شيئاً.");

            int after = Load().Sum(file => EnglishHalf.Count(file.Code));

            Assert.True(
                after == before,
                FormattableString.Invariant(
                    $"ناتج بناء غير متعقَّب غيّر عدّ الحارس: {before} ⇒ {after}. ")
                + "المجموعة المفحوصة يجب أن تكون ما يتعقّبه git وحده، وإلا صار حكم الحارس "
                + "تابعاً للحظة آخر بناء ولتخطيط ناتج المصغِّر.");

            Assert.DoesNotContain(
                Sources,
                file => file.Path.Contains("__rule14_witness", StringComparison.Ordinal));
        }
        finally
        {
            File.Delete(planted);
        }
    }

    /// <summary>
    /// وأن المجموعة المفحوصة ليست فارغة ولا ضامرة: مسحٌ لا يقرأ شيئاً يمرّ دائماً.
    /// </summary>
    [Fact]
    public void TheTrackedScanReadsARealRepository()
    {
        Assert.True(Sources.Count > 400, "الملفّات المتعقَّبة الممسوحة: " + Sources.Count);

        Assert.Contains(
            Sources,
            static file => file.Path.StartsWith("data/posting-matrix/events/", StringComparison.Ordinal));

        // ولا ملفّ من مجلّدات النواتج يعبر إليها بحال.
        foreach (string generated in new[] { "web/dist/", "web/node_modules/", "/bin/", "/obj/" })
        {
            Assert.DoesNotContain(
                Sources,
                file => file.Path.Contains(generated, StringComparison.Ordinal));
        }
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

    /// <summary>
    /// <b>المجموعة المفحوصة هي ما يتعقّبه git، لا ما يقع على القرص.</b>
    /// <para>
    /// <b>ولماذا هذا التمييز حاسم:</b> المسح على القرص كان يبتلع <c>web/dist/</c> — ناتج
    /// بناء مُهمَل في <c>.gitignore</c> — فيصير حكم الحارس تابعاً <b>لتخطيط ناتج المصغِّر
    /// وللحظة آخر بناء</b>. وقد وقع ذلك فعلاً ومُقيس: البنية نفسها أنتجت حزمة يمرّ فيها
    /// الرمز فيُقرأ موضعاً، وحزمةً أخرى يقع فيها بعد <c>//</c> فيُجرَّد ولا يُقرأ. فالعدد
    /// اختلف بين شجرتين محتواهما <b>متطابق بايتاً بايت</b> (881 مقابل 882)، وأحمرَّ الحارس
    /// على شجرة سليمة.
    /// </para>
    /// <para>
    /// وحارسٌ يحمرّ لسبب لا علاقة له بما يحرسه يُدرَّب الناس على تجاهله — وذلك أسوأ من
    /// غيابه ([`traps.md` فخ-65](../../docs/evidence/traps.md)). وgit هو المرجع الوحيد
    /// لسؤال «ما محتوى هذا المستودع؟»، فلا حاجة بعده إلى إقصاء <c>bin</c> و<c>obj</c>
    /// و<c>node_modules</c> بقائمة تُصان بيد: غير المتعقَّب غير ممسوح بالبناء.
    /// </para>
    /// </summary>
    private static List<(string Path, string Code)> Load()
    {
        List<(string Path, string Code)> files = [];

        foreach (string relative in TrackedPaths())
        {
            if (!Extensions.Contains(Path.GetExtension(relative), StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            if (Exempt.Any(prefix => relative.StartsWith(prefix, StringComparison.Ordinal)))
            {
                continue;
            }

            // ‏45 ملفّ ناتج بناء **مُودَعة في المستودع** تحت مسار مقطعه `bin\Debug` —
            // بشرطة **خلفية** داخل اسم المجلّد لا فاصلَ مسار. ولذلك لا يستبعدها
            // `.gitignore` ولا أي نمط `/bin/` في هذا المستودع، ومنها منهجُ القياس في
            // ADR-0021 §4 نفسه. (مساهمتها في المقياس **صفر** — مقيس — لكنها تلوّث
            // أي مسح.) والتطبيع قبل الفحص هو ما يجعل النمط يراها.
            string normalised = relative.Replace('\\', '/');

            if (normalised.Contains("/bin/", StringComparison.Ordinal)
                || normalised.Contains("/obj/", StringComparison.Ordinal)
                || normalised.Contains("/node_modules/", StringComparison.Ordinal))
            {
                continue;
            }

            string absolute = Path.Combine(RepositoryLayout.Root, relative);

            if (File.Exists(absolute))
            {
                files.Add((normalised, StripComments(File.ReadAllText(absolute))));
            }
        }

        return files;
    }

    /// <summary>
    /// مسارات الملفّات المتعقَّبة تحت <see cref="Roots"/>، بفواصل صفرية حتى لا يكسر اسمٌ
    /// فيه سطرٌ جديد القراءة.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن تعذّر سؤال git — والصمت هنا أسوأ من الرمي.</exception>
    private static string[] TrackedPaths()
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(RepositoryLayout.Root);
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");
        start.ArgumentList.Add("--");

        foreach (string root in Roots)
        {
            start.ArgumentList.Add(root);
        }

        using Process? git = Process.Start(start)
            ?? throw new InvalidOperationException("تعذّر تشغيل git. / Could not start git.");

        string output = git.StandardOutput.ReadToEnd();
        string error = git.StandardError.ReadToEnd();
        git.WaitForExit();

        if (git.ExitCode != 0)
        {
            throw new InvalidOperationException(
                "‏git ls-files أخفق، فلا سبيل إلى معرفة محتوى المستودع — والحارس يرمي ولا "
                + "يخمّن على ما يقع على القرص. / git ls-files failed: " + error);
        }

        return output.Split('\0', StringSplitOptions.RemoveEmptyEntries);
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
