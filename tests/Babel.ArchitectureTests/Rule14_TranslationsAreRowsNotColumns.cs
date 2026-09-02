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
/// <b>وما الذي يعدّه هذا الحارس بالضبط — وهذا هو التغيير:</b> كان يعدّ <b>كل ظهور</b>
/// للسلسلة <c>name_en</c> في الشجرة، فيخلط شيئين مختلفين في رقم واحد: <b>العمود
/// المخزَّن</b> الذي يعجز عن لغةٍ ثالثة، و<b>الشرح الإنجليزي في وثيقة تصميم</b> الذي لا
/// يعجز عن شيء لأنه ليس عموداً أصلاً. وثُلثا السقف القديم كانا من الصنف الثاني: مقيس على
/// <c>develop</c> أنّ <c>data/posting-matrix/</c> وحدها تساهم بـ<b>641</b> من <b>862</b>.
    /// وأثرُ الخلط لم يكن نظرياً: كلُّ حدث ترحيلٍ جديد يضيف <b>أربعةَ مواضع شرحٍ على
    /// الأقلّ</b> — الاسم ومُطلِقه وشرطه المسبق وعكسه، وأكثرَ مع كل مبلغٍ وشرطٍ وسيناريو —
    /// فيكسر سقفاً <b>لا يرتفع</b>. مقيس بإعادة إنتاج الحارس القديم حرفياً: <b>862</b> على
    /// <c>develop</c>، و<b>866</b> بعد إضافة <c>inventory.transfer.between_locations</c>
    /// وحده. فسُحب الحدث ولم يُرفع السقف — <b>وكان ذلك صواباً</b>، وصار النموّ ممنوعاً
    /// بنيوياً في كل الوحدات لا المخزون وحده.
/// </para>
/// <para>
/// <b>فصار الفصل بالشكل لا بقائمة مسارات</b> (‏ADR-جديد · gloss-is-not-column-debt):
/// الموضع <b>دَينٌ</b> إن كان يُنشئ عموداً مخزَّناً أو يخرّطه — تصريحُ DDL، أو عمودُ هجرة،
/// أو خريطةُ عمود، أو <c>alter … add column</c>. وهو <b>شرحٌ</b> إن كان مفتاحاً وقيمةً في
/// وثيقة بيانات. وحارسُ الشرح — قاعدةُ اتّساقٍ بلا سقف — في
/// <see cref="Rule14_TheDesignGlossIsConsistentNotCapped"/>.
/// </para>
/// <para>
/// <b>وثمن ذلك أنّ قائمة الإقصاء بالمسارات سقطت كلّها.</b> لم تعد هناك حاجةٌ إلى إقصاء
/// <c>PostingMatrix</c> ولا <c>PostingPlanner</c> ولا حارسِ السلك النظير: مساهمة كلٍّ منها
/// بالكاشف البنيوي <b>صفر</b> — مقيس. وهجراتُ الدفتر المجمَّدة كانت مُقصاةً وصارت
/// <b>معدودة</b>: هي بالضبط الموضع الذي يُنشَأ فيه العمود، وإقصاؤها كان ثقباً يمرّ منه
/// عمودٌ جديد بلا أن يحمرّ شيء. اثنا عشر موضعاً دخلت العدّ بهذا التغيير.
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
    /// <b>الكاشف الفاصل: ما الذي يجعل موضعاً دَيناً.</b>
    /// <para>
    /// سبع صيغ، وكلٌّ منها <b>يضع الاسم الإنجليزي الثابت في جدول</b> — وهو وحده ما يعجز
    /// عن لغةٍ ثالثة بلا هجرة:
    /// </para>
    /// <list type="number">
    ///   <item>تصريحُ عمود في DDL خام — الاسم يتبعه نوعُ عمود؛</item>
    ///   <item>عمودُ هجرة EF — <c>table.Column</c>؛</item>
    ///   <item>خريطةُ عمود صريحة — <c>HasColumnName</c>؛</item>
    ///   <item>سمةُ العمود — <c>[Column(…)]</c>؛</item>
    ///   <item>خاصيّةٌ مظلَّلة بالاسم — <c>Property&lt;T&gt;(…)</c>؛</item>
    ///   <item><b>تخريطةٌ بالاصطلاح</b> — <c>Property(x =&gt; x.NameEn)</c> بلا
    ///         <c>HasColumnName</c> بعدها: العمود يوجد واسمه يأتي من الاصطلاح؛</item>
    ///   <item>إضافةُ عمود إلى جدول قائم — <c>add column</c>.</item>
    /// </list>
    /// <para>
    /// <b>والشكل السادس هو ما كان يُفلت من كل حارس.</b> مقيس: <c>SalesRows.NameEn</c> و
    /// <c>PurchasingRows.NameEn</c> عمودان مخزَّنان على صفٍّ حيّ، وتخريطُهما
    /// <c>entity.Property(row =&gt; row.NameEn).HasMaxLength(200).IsRequired();</c> — بلا
    /// <c>HasColumnName</c> واحدة، فاسم العمود من الاصطلاح. لا الكاشفُ القديم كان
    /// يسمّيهما (كانا موضعين من 862 بلا اسم) ولا كاشفُ الأعمدة الصريحة يراهما.
    /// <b>والنفيُ في آخر الشكل — <c>(?!\s*\.HasColumnName)</c> — هو ما يمنع عدّ العمود
    /// الواحد مرّتين</b> حين تُسمّى تخريطتُه صراحةً.
    /// </para>
    /// <para>
    /// <b>وما ليس فيها ليس دَيناً بحكم هذا الحارس</b>، ولكلٍّ سببٌ يُقرأ: مفتاحُ JSON
    /// (<c>"name_en": "…"</c>) قيمةٌ في وثيقة؛ ومرآتُه في الشيفرة
    /// (<c>JsonPropertyName</c>) قراءةُ تلك الوثيقة؛ وقائمةُ أعمدةٍ في <c>insert</c> أو
    /// <c>\copy</c> <b>تكتب في عمودٍ معدودٍ عند تصريحه</b> — وعدُّها ثانيةً يضاعف الدَّين
    /// الواحد. <b>القاعدة: كل عمودٍ مخزَّن يُعدّ مرّةً واحدة، عند إنشائه أو تخريطه.</b>
    /// </para>
    /// </summary>
    private static readonly Regex StoredEnglishNameColumn =
        new(@"\bname_en\s+(?:text|citext|varchar|character\s+varying|nvarchar|jsonb)\b"
            + @"|\bname_en\s*=\s*table\.Column"
            + @"|\bHasColumnName\(\s*""{1,2}name_en""{1,2}\s*\)"
            + @"|\[\s*Column\(\s*""{1,2}name_en""{1,2}\s*\)\s*\]"
            + @"|\bProperty\s*<[^>\n]*>\s*\(\s*""{1,2}name_en""{1,2}\s*\)"
            + @"|\bProperty\(\s*[A-Za-z_]\w*\s*=>\s*[A-Za-z_]\w*\.NameEn\s*\)(?!\s*\.HasColumnName)"
            + @"|\b(?:add|alter)\s+column\s+(?:if\s+not\s+exists\s+)?name_en\b",
            RegexOptions.None, TimeSpan.FromSeconds(5));

    /// <summary>
    /// <b>سقف دَين الأعمدة، مقيسٌ لا مُقدَّر — ولا يرتفع أبداً.</b>
    /// <para>
    /// <b>أمرُ قياسه، يُعاد تشغيله حرفياً من جذر المستودع</b> (ويُعطي العدد نفسه الذي
    /// يعدّه هذا الملفّ: تجريدُ التعليقات في <c>sed</c> هو نظير
    /// <see cref="StripComments"/>، وبدونه يزيد العدد واحداً هو ذكرُ الشكل في تعليقِ
    /// توثيقٍ داخل هذا الملفّ نفسه):
    /// </para>
    /// <code>
    /// git ls-files -z -- src tests web contracts tools demo design data \
    ///  | grep -zE '\.(cs|sql|json|ts|tsx|js|css|csproj|yml|yaml)$' \
    ///  | grep -zvE '/(bin|obj|node_modules)/' \
    ///  | xargs -0 sed -E 's;//.*$;;; s;--.*$;;' \
    ///  | grep -oP 'name_en\s+(text|citext|varchar|character\s+varying|nvarchar|jsonb)\b\
    ///             |name_en\s*=\s*table\.Column\
    ///             |HasColumnName\(\s*"{1,2}name_en"{1,2}\s*\)\
    ///             |\[\s*Column\(\s*"{1,2}name_en"{1,2}\s*\)\s*\]\
    ///             |Property\s*&lt;[^&gt;]*&gt;\s*\(\s*"{1,2}name_en"{1,2}\s*\)\
    ///             |Property\(\s*\w+\s*=&gt;\s*\w+\.NameEn\s*\)(?!\s*\.HasColumnName)\
    ///             |(add|alter)\s+column\s+(if\s+not\s+exists\s+)?name_en\b' \
    ///  | wc -l          # 47 على هذا الفرع — والبدائل السبعة تُكتب في سطرٍ واحد بلا مسافات
    /// </code>
    /// <para>
    /// <b>ومنه خفضٌ من 862 إلى هذا الرقم، وهو ليس تسديداً بل تصحيحُ ما يُقاس.</b> السقف
    /// القديم كان يعدّ الشرح مع الدَّين، فثُلثاه شروحٌ في وثيقة تصميم. ولم يُحذف موضعٌ
    /// واحدٌ من الشيفرة بهذا الخفض — بل <b>دخلت</b> فيه اثنا عشر موضعاً كانت مُقصاةً
    /// بقائمة مسارات (هجرات الدفتر). <b>ومن قرأ 862 على أنه حجم عملٍ فقد ضاعفه نحو
    /// عشرين مرّة، ويُصحَّح.</b>
    /// </para>
    /// <para>
    /// <b>ولا يرتفع.</b> من يحتاج رفعه فقد أدخل عموداً ثنائياً مخزَّناً جديداً، وهو
    /// ممنوع بنصّ ADR-0021 §6.3 بند 2: الاسم العربي عمودٌ لأنه السجلّ، والترجمات صفوفٌ
    /// في جدول ترجمات. وخفضُه عند كل هجرة جزئية <b>مطلوب</b>.
    /// </para>
    /// </summary>
    public const int MaximumEnglishNameSites = 47;

    /// <summary>
    /// <b>ما يساهم به هذا الملفّ نفسه — معدودٌ لا مُعفى.</b>
    /// <para>
    /// الحارس يكتب الشكل الممنوع بالضرورة في شواهده الموجبة. والعلاج المعتاد — إقصاء
    /// ملفّ الحارس — هو بعينه العطب الذي وقع في ماسح الأسرار حين كان <b>يُعفي ملفّه
    /// هو</b>، فصار الإقصاء بابَ إخفاءٍ لا بابَ دقّة. فلا إقصاء هنا: الشواهد <b>تدخل
    /// السقف</b>، ويُثبَّت نصيبها برقمٍ مستقلّ مقيس. وأثرُ ذلك أنّ تغييراً في دَين
    /// المنتج لا يستطيع أن يختبئ خلف تغييرٍ في شواهد الحارس، ولا العكس.
    /// </para>
    /// <para>
    /// <b>وتفصيلُ العشرة مقيس:</b> سبعةٌ في الشاهد الموجب — صيغةٌ واحدة لكلٍّ من بنود
    /// الكاشف السبعة — وواحدٌ في شاهد «العمود يُعدّ مرّةً واحدة»، واثنان في نصّ الملفّ
    /// الذي يزرعه شاهدُ ناتج البناء على القرص.
    /// </para>
    /// <para>
    /// <b>وأمرُ قياسه</b> هو أمرُ السقف نفسه مقصوراً على هذا الملفّ: يُستبدَل
    /// <c>git ls-files … | xargs -0</c> بمسار الملفّ وحده، ويبقى <c>sed</c> و<c>grep</c>
    /// كما هما.
    /// </para>
    /// </summary>
    private const int WitnessSitesInThisGuard = 10;

    /// <summary>
    /// <b>مقدار التراخي المسموح بين السقف والمقيس.</b> سقفٌ يبقى أعلى من الواقع بفارقٍ
    /// كبير يكفّ عن أن يحرس — يسمح بعودة ما أُزيل. والتراخي ليس صفراً عمداً: فرعان
    /// متوازيان يُنقص كلٌّ منهما موضعاً ويكتبان السقف نفسه، فيصير المدموج أقلّ من كليهما.
    /// </summary>
    private const int CeilingSlack = 5;

    private static List<(string Path, string Code)> Sources { get; } = Load();

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · الكاشف يميّز العمود من الشرح — الشاهد الموجب، وهو أهمّ ما في الملف
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>شاهدٌ موجب: الكاشف يلتقط مخالفةً حقيقية، ولا يلتقط شرحاً.</b>
    /// <para>
    /// حارسٌ يمسح مجموعةً <b>لا تستطيع بنيتها أن تحوي مخالفة</b> يمرّ ولا يُثبت شيئاً.
    /// وغيرُ الفراغ وحده لا يكفي: المفحوص هنا هو <b>الكاشف نفسه</b>، بنصوصٍ هي حرفياً ما
    /// كان في <c>LedgerRows.cs</c> و<c>LedgerDbContext.cs</c> و<c>LedgerFoundation.cs</c>
    /// قبل الهجرة، وبنصوصٍ هي حرفياً ما في <c>data/posting-matrix/events/*.json</c> اليوم.
    /// </para>
    /// <para>
    /// <b>والنصف السالب هو التغيير الجوهري:</b> لو التقط الكاشف مفتاح JSON لعاد السقف
    /// يقيس الشرح، ولعاد كلُّ حدث ترحيلٍ جديد ممنوعاً. فالتمييز <b>مُثبَتٌ بنصٍّ</b> لا
    /// مُدَّعىً في تعليق.
    /// </para>
    /// </summary>
    [Fact]
    public void TheDetectorTellsAStoredColumnFromADocumentValue()
    {
        (string Label, string Snippet)[] storedColumns =
        [
            ("مخطّط SQL خام", "    name_en         text not null check (length(btrim(name_en)) > 0),"),
            ("جدول الهجرة كما كان", "name_en = table.Column<string>(type: \"text\", nullable: false),"),
            ("خريطة العمود كما كانت", @"entity.Property(row => row.NameEn).HasColumnName(""name_en"");"),
            ("سمة العمود", @"[Column(""name_en"")] public string NameEn { get; set; }"),
            ("خاصيّة مظلَّلة بالاسم", @"builder.Property<string>(""name_en"");"),
            ("تخريطة بالاصطلاح بلا تسمية عمود", "entity.Property(row => row.NameEn).HasMaxLength(200).IsRequired();"),
            ("إضافة عمود إلى جدول قائم", "alter table matrix.event add column name_en;"),
        ];

        foreach ((string label, string snippet) in storedColumns)
        {
            Assert.True(
                StoredEnglishNameColumn.IsMatch(snippet),
                "الكاشف لم يلتقط عموداً مخزَّناً — " + label + ": " + snippet);
        }

        // والعمود الواحد يُعدّ مرّةً واحدة، لا مرّةً بخاصيّته ومرّةً باسمه.
        Assert.Equal(
            1,
            StoredEnglishNameColumn.Count(@"e.Property(x => x.NameEn).HasColumnName(""name_en"");"));

        // والشرح ليس ديناً: هذه كلها مواضع حقيقية في المستودع اليوم، ولا واحد منها عمود.
        foreach (string gloss in new[]
                 {
                     @"      ""name_en"": ""Sales invoice and its cost"",",
                     @"[JsonPropertyName(""name_en"")] public string? NameEn { get; set; }",
                     @"""required"": [""event_code"", ""name_ar"", ""name_en"", ""module""],",
                     "       e ->> 'name_en',",
                     @"\copy matrix.account_role (code, name_ar, name_en, status) from 'account-roles.csv'",
                     "insert into matrix.event (event_code, name_ar, name_en, module) select",
                     "    name_ar         text not null check (length(btrim(name_ar)) > 0),",
                     "public required string MessageEn { get; init; }",
                 })
        {
            Assert.False(
                StoredEnglishNameColumn.IsMatch(gloss),
                "الكاشف عدّ شرحاً دَيناً — وهذا بعينه ما كان يمنع كل حدث ترحيل جديد: " + gloss);
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · نطاق المسح — حارسٌ نطاقُه انكسر يمرّ أخضر على لا شيء
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>والمجموعة المفحوصة تحوي فعلاً الملفّات التي تحمل الدَّين.</b>
    /// نطاقٌ لا يشمل موضع العطل يمرّ أبداً ولا يحرس شيئاً. والقائمة أدناه <b>مقيسة</b>:
    /// هي الملفّات التي يجدها أمرُ القياس المُودَع بجانب السقف، ومنها هجراتُ الدفتر التي
    /// كانت مُقصاةً قبل هذا التغيير.
    /// </summary>
    [Fact]
    public void TheScannedSetContainsTheFilesThatCarryTheDebt()
    {
        foreach (string expected in new[]
                 {
                     "src/Babel.Ledger/Persistence/Migrations/20260824135544_LedgerFoundation.cs",
                     "src/Babel.ControlPlane/Registry/ControlSchema.cs",
                     "src/Babel.ControlPlane/Migration/TenantSchema.cs",
                     "data/chart-of-accounts/ddl/001-schema.sql",
                     "data/posting-matrix/ddl/001-schema.sql",
                     "demo/vertical-slice/Db/Model.cs",
                     "tests/Babel.ControlPlane.Proofs/Harness.cs",
                 })
        {
            (string Path, string Code) file = Sources.FirstOrDefault(
                candidate => string.Equals(candidate.Path, expected, StringComparison.Ordinal));

            Assert.True(
                file.Path is not null,
                "ملفٌّ يحمل دَيناً مقيساً خرج من المجموعة المفحوصة: " + expected);

            Assert.True(
                StoredEnglishNameColumn.IsMatch(file.Code),
                "المجموعة تحوي الملفّ ولا ترى دَينه — الكاشف أو التجريد كسر: " + expected);
        }

        // وملفّ الحارس نفسه داخل المجموعة: بدونه يصير تثبيت نصيبه أدناه فحصاً على فراغ.
        Assert.Contains(
            Sources,
            static file => file.Path.EndsWith("Rule14_TranslationsAreRowsNotColumns.cs", StringComparison.Ordinal));
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
    /// لو عاد المسح إلى القرص لالتقطه فوراً. والمزروع <b>عمودٌ مخزَّن</b> لا مجرّد سلسلة،
    /// كي يفحص العدّ الذي يحرسه هذا الملفّ فعلاً بعد تضييق الكاشف.
    /// </para>
    /// </summary>
    [Fact]
    public void GeneratedBuildOutputOnDiskNeverEntersTheCount()
    {
        int before = Load().Sum(file => StoredEnglishNameColumn.Count(file.Code));

        string planted = Path.Combine(RepositoryLayout.Root, "web", "dist", "__rule14_witness.sql");
        Directory.CreateDirectory(Path.GetDirectoryName(planted)!);

        try
        {
            File.WriteAllText(
                planted,
                "create table t (\n  name_en text not null,\n  other_en varchar(20)\n);\n"
                + "alter table t add column name_en text;\n");

            Assert.True(
                File.Exists(planted),
                "لم يُزرع ملف الشاهد أصلاً، فالاختبار لا يفحص شيئاً.");

            Assert.True(
                StoredEnglishNameColumn.IsMatch(File.ReadAllText(planted)),
                "الشاهد المزروع لا يحوي مخالفةً يراها الكاشف، فوجودُه خارج العدّ لا يُثبت شيئاً.");

            int after = Load().Sum(file => StoredEnglishNameColumn.Count(file.Code));

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
    // ٣ · سقفٌ لا يرتفع — البند §6.3-2 مفروضاً على الأعمدة وحدها
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public void TheStoredColumnDebtNeverGrows()
    {
        int sites = Sources.Sum(file => StoredEnglishNameColumn.Count(file.Code));

        Assert.True(
            sites <= MaximumEnglishNameSites,
            string.Create(
                CultureInfo.InvariantCulture,
                $"مواضع العمود الإنجليزي الثابت = {sites}، والسقف {MaximumEnglishNameSites}.\n")
            + "عمودٌ ثنائي مخزَّن جديد ممنوع بنصّ ADR-0021 §6.3 بند 2: الاسم العربي عمودٌ لأنه\n"
            + "السجلّ، والترجمات صفوفٌ في جدول ترجمات — لا عمودٌ ثانٍ للإنجليزية.\n"
            + "  · وإن كان ما أضفته شرحاً في وثيقة بيانات لا عموداً، فهذا الحارس لا يراه أصلاً،\n"
            + "    وحارسُه هو Rule14_TheDesignGlossIsConsistentNotCapped ولا سقف فيه.\n"
            + "وخفضُ السقف عند كل هجرة جزئية مطلوب — سقفٌ يبقى أعلى من الواقع يكفّ عن أن يحرس.\n"
            + "الملفّات المخالفة:\n"
            + string.Join(
                '\n',
                Sources
                    .Where(file => StoredEnglishNameColumn.IsMatch(file.Code))
                    .Select(file => FormattableString.Invariant(
                        $"  {StoredEnglishNameColumn.Count(file.Code),4}  {file.Path}"))));

        Assert.True(
            sites > MaximumEnglishNameSites - CeilingSlack,
            string.Create(
                CultureInfo.InvariantCulture,
                $"المواضع {sites} أقلّ من السقف {MaximumEnglishNameSites} بفارق كبير. ")
            + "اخفض MaximumEnglishNameSites إلى العدد المقيس، وإلا صار السقف يسمح بعودة ما أُزيل.");
    }

    /// <summary>
    /// <b>نصيبُ الحارس من السقف مثبَّتٌ بذاته — فلا يختبئ دَينٌ خلف شاهد.</b>
    /// <para>
    /// لو أُعفي هذا الملفّ لصار بابَ إخفاء: من يضيف عموداً يستطيع أن يضيفه هنا. ولو
    /// عُدَّ بلا تثبيت لصار تقويةُ الحارس ترفع الدَّين الذي يقيسه. فالنصيب <b>معدودٌ
    /// ومثبَّت</b>، وأي تغيير فيه يظهر سطراً في الفرق.
    /// </para>
    /// </summary>
    [Fact]
    public void ThisGuardsOwnWitnessesAreCountedAndPinnedNotExempted()
    {
        (string Path, string Code) self = Sources.Single(
            static file => file.Path.EndsWith("Rule14_TranslationsAreRowsNotColumns.cs", StringComparison.Ordinal));

        int mine = StoredEnglishNameColumn.Count(self.Code);

        Assert.True(
            mine == WitnessSitesInThisGuard,
            string.Create(
                CultureInfo.InvariantCulture,
                $"شواهد هذا الحارس صارت {mine} وكانت {WitnessSitesInThisGuard}. ")
            + "عدّل WitnessSitesInThisGuard مع السقف معاً، كي يبقى الفرق بينهما هو دَين المنتج وحده.");

        Assert.True(
            mine > 0,
            "الحارس لا يكتب شكلاً ممنوعاً واحداً — أي أنّ شواهده الموجبة ذهبت، وهو يمرّ على لا شيء.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٤ · ولا عمود إنجليزي ثابت في نموذج الدفتر الحيّ — صفرٌ لا سقف
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>نموذج الدفتر الحيّ صفرٌ لا سقف — والكاشفُ هو نفسه كاشفُ السقف.</b>
    /// <para>
    /// الهجرات المجمَّدة خارج «الحيّ» <b>بالمعنى</b>: الهجرة التي <b>أزالت</b> العمود
    /// تذكره بالضرورة، ولا تُحرَّر أبداً. وهي مع ذلك <b>داخل السقف</b> في §٣ أعلاه، فلا
    /// تختفي — وهذا هو الفرق عن الإقصاء القديم الذي كان يُخرجها من <b>كلّ</b> عدّ.
    /// </para>
    /// </summary>
    [Fact]
    public void TheLiveLedgerModelDeclaresNoFixedEnglishNameColumn()
    {
        List<string> offenders =
        [
            .. Sources
                .Where(static file => file.Path.StartsWith("src/Babel.Ledger/", StringComparison.Ordinal))
                .Where(static file => !file.Path.StartsWith("src/Babel.Ledger/Persistence/Migrations/", StringComparison.Ordinal))
                .Where(static file => StoredEnglishNameColumn.IsMatch(file.Code))
                .Select(static file => file.Path),
        ];

        Assert.True(
            offenders.Count == 0,
            "عمودٌ إنجليزي ثابت عاد إلى نموذج الدفتر الحيّ. الترجمة صفٌّ في "
            + "ledger.name_translation لا عمود (ADR-0021 بند 2):\n"
            + string.Join('\n', offenders));

        // وحارس لافراغ: المجموعة تحوي فعلاً ملفّات الدفتر الحيّة التي كانت تحمل المخالفة.
        foreach (string expected in new[]
                 {
                     "src/Babel.Ledger/Persistence/LedgerRows.cs",
                     "src/Babel.Ledger/Persistence/LedgerDbContext.cs",
                 })
        {
            Assert.Contains(Sources, file => string.Equals(file.Path, expected, StringComparison.Ordinal));
        }
    }

    /// <summary>
    /// <b>الباقي المُعلَن: خاصيّةُ <c>NameEn</c> غيرِ المخرَّطة — مُسمّاةٌ لا معدودة.</b>
    /// <para>
    /// خاصيّةٌ في الشيفرة اسمها <c>NameEn</c> ليست عموداً مخزَّناً حتى تُخرَّط، فهي خارج
    /// السقف بحكم القاعدة الفاصلة. <b>ولا تُترك بلا حارس مع ذلك</b>: السقفُ العريض القديم
    /// كان يحدّها عرضاً وهو يقيس الشيء الخطأ، فلو ذهب بلا بديل لصار الفصلُ تخفيفاً.
    /// </para>
    /// <para>
    /// فالباقي <b>يُسمّى بمواضعه وأسبابه</b> — كما في ADR-0037 — ويسقط الحارس على أيّ
    /// موضعٍ <b>جديد</b>. والقائمة تُقرأ لا تُفترض:
    /// </para>
    /// <list type="bullet">
    ///   <item><c>src/Babel.Ledger/PostingMatrix/MatrixModel.cs</c> — نموذج حدث المصفوفة
    ///         في الذاكرة. اسمُه الإنجليزي ينساب إلى <c>journal_line.description</c> وهو
    ///         <b>حقل مُجزَّأ</b> في الشكل القانوني v2؛ نقلُه إصدارٌ قانوني ثالث لا هجرةُ
    ///         عرض (‏ADR-0027 §2، ومُثبَت في <c>DisplayTextInsideTheHashedBytesTests</c>).</item>
    ///   <item><c>src/Babel.Sales/Persistence/SalesRows.cs</c> و
    ///         <c>src/Babel.Purchasing/Persistence/PurchasingRows.cs</c> — <b>عمودان
    ///         مخزَّنان فعلاً</b>، وتخريطتاهما معدودتان في السقف بالشكل السادس. والخاصيّتان
    ///         هنا هما نصفُهما في الشيفرة، فلا تُعدّان ثانيةً.</item>
    ///   <item><c>src/Babel.ControlPlane/Metering/BillableUsers.cs</c> — اسمُ استراتيجية
    ///         عدّ المستخدمين، قيمةٌ محسوبة في الذاكرة لا صفٌّ يُحفظ. <b>وهي دَينُ عرضٍ
    ///         مُعلَن</b> بحكم ADR-0021 §6.3 بند 2، ولا يعالجها هذا الفرع.</item>
    /// </list>
    /// </summary>
    [Fact]
    public void TheUnmappedEnglishNamePropertiesAreTheNamedRemainderAndNoNewOneJoinsThem()
    {
        Regex property = new(
            @"\b(?:public|internal|protected)\s+(?:required\s+)?(?:virtual\s+)?string\??\s+NameEn\b",
            RegexOptions.None, TimeSpan.FromSeconds(5));

        string[] named =
        [
            "src/Babel.ControlPlane/Metering/BillableUsers.cs",
            "src/Babel.Ledger/PostingMatrix/MatrixModel.cs",
            "src/Babel.Purchasing/Persistence/PurchasingRows.cs",
            "src/Babel.Sales/Persistence/SalesRows.cs",
        ];

        List<string> found =
        [
            .. Sources
                .Where(static file => file.Path.StartsWith("src/", StringComparison.Ordinal))
                .Where(file => property.IsMatch(file.Code))
                .Select(static file => file.Path)
                .Order(StringComparer.Ordinal),
        ];

        List<string> unnamed = [.. found.Except(named, StringComparer.Ordinal)];

        Assert.True(
            unnamed.Count == 0,
            "نصفٌ إنجليزي ثابت جديد في شيفرة المنتج. الاسم العربي هو السجلّ والترجمات صفوف "
            + "(‏ADR-0021 بند 2 · §6.3 بند 2). وإن كان عموداً مخزَّناً فهو دَينٌ يُعدّ في "
            + "MaximumEnglishNameSites، وإن لم يكن فسمّه هنا بسببه المكتوب:\n"
            + string.Join('\n', unnamed));

        // حارس لافراغ: القائمة المُسمّاة ما زالت تصف الواقع، وإلا صارت قائمةً تصف ماضياً.
        Assert.True(
            found.Count >= 3,
            FormattableString.Invariant($"مواضع الخاصيّة المقروءة {found.Count} — الكاشف أو المسح ضامر."));

        // وشاهدٌ موجب على الكاشف نفسه: صيغٌ حقيقية كانت في LedgerRows.cs.
        Assert.Matches(property, "public string NameEn { get; set; } = string.Empty;");
        Assert.Matches(property, "public required string NameEn { get; init; }");
        Assert.DoesNotMatch(property, "public string NameAr { get; set; } = string.Empty;");
        Assert.DoesNotMatch(property, "public required string MessageEn { get; init; }");
    }

    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>المجموعة المفحوصة هي ما يتعقّبه git، لا ما يقع على القرص.</b>
    /// <para>
    /// <b>ولماذا هذا التمييز حاسم:</b> المسح على القرص كان يبتلع <c>web/dist/</c> — ناتج
    /// بناء مُهمَل في <c>.gitignore</c> — فيصير حكم الحارس تابعاً <b>لتخطيط ناتج المصغِّر
    /// وللحظة آخر بناء</b>. وقد وقع ذلك فعلاً ومُقيس: العدد اختلف بين شجرتين محتواهما
    /// <b>متطابق بايتاً بايت</b> (881 مقابل 882)، وأحمرَّ الحارس على شجرة سليمة.
    /// </para>
    /// <para>
    /// وحارسٌ يحمرّ لسبب لا علاقة له بما يحرسه يُدرَّب الناس على تجاهله — وذلك أسوأ من
    /// غيابه ([`traps.md` فخ-65](../../docs/evidence/traps.md)). وgit هو المرجع الوحيد
    /// لسؤال «ما محتوى هذا المستودع؟».
    /// </para>
    /// <para>
    /// <b>ولا قائمة إقصاء بالمسارات هنا — ولا واحدة.</b> التصنيف يقع بالشكل: ما ليس
    /// عموداً مخزَّناً لا يدخل العدّ أصلاً، فلا حاجة إلى استثنائه. وقائمةٌ من خمسة مسارات
    /// يهزمها مسارٌ سادس غداً.
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

            // ‏45 ملفّ ناتج بناء **مُودَعة في المستودع** تحت مسار مقطعه `bin\Debug` —
            // بشرطة **خلفية** داخل اسم المجلّد لا فاصلَ مسار. ولذلك لا يستبعدها
            // `.gitignore` ولا أي نمط `/bin/` في هذا المستودع. والتطبيع قبل الفحص هو
            // ما يجعل النمط يراها.
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
    /// <para>
    /// <b>وتمريرةٌ واحدة لا ثلاث — وهذا عطلٌ وقع في هذه الجلسة لا احتياطٌ نظري.</b>
    /// كان التجريد ثلاث تمريرات، أولاها تحذف <c>/*…*/</c> على المستوى الشامل. فحين ذكر
    /// تعليقُ توثيقٍ في هذا الملفّ المسارَ <c>events/[نجمة].json</c>، قرأت التمريرة الأولى
    /// النجمةَ بعد الشرطة المائلة <b>فتحاً لتعليق كتلة</b>، وأغلقته عند أول <c>*∕</c>
    /// بعده — وهو في التعبير النمطي للتجريد نفسه بعد <b>أربعمئة سطر</b>. فابتُلع
    /// <b>13,550 محرفاً</b> (مقيس) ومعها كلُّ شواهد الحارس، فصار يعدّ <b>صفراً</b> ويمرّ.
    /// والتمريرة الواحدة بتبادلٍ يُمسح يساراً إلى يمين تجعل <c>//</c> يبتلع سطره كلّه
    /// قبل أن تُقرأ النجمة التي فيه.
    /// </para>
    /// </summary>
    private static string StripComments(string text) =>
        Regex.Replace(
            text,
            @"//[^\n]*|--[^\n]*|/\*.*?\*/",
            " ",
            RegexOptions.Singleline,
            TimeSpan.FromSeconds(5));
}
