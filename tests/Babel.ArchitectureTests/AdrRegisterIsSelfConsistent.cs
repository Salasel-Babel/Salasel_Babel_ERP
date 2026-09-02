using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>حارس سجل القرارات (ADR) — قاعدة تخصيص المعرّفات مُنفَّذة لا موصوفة.</b>
/// <para>
/// <b>العطل الذي يجعله مستحيلاً:</b> فرعان متوازيان أنشأ كلٌّ منهما <c>ADR-0016</c>: أحدهما
/// «هوية الترحيل تشمل رمز الحدث» والآخر «سطح HTTP عقدٌ منشور». و<c>git</c> دمجهما
/// <b>بلا تعارض واحد</b> لأن اسمَي الملفين مختلفان — فصار في السجل وثيقتان تحملان الرقم
/// نفسه ولم يشتكِ شيء. وهذه هي <b>المرّة الخامسة</b> لهذا الصنف من العطل في هذا المستودع
/// (فخ-38/39 · «القاعدة 10» · فخ-41 · <c>ADR-0016</c> · ثم ثلاثة فروع متوازية كلٌّ منها على
/// وشك أخذ «الرقم التالي المتاح»).
/// </para>
/// <para>
/// <b>القاعدة المفروضة هنا</b> (نصّها الكامل في <c>docs/decisions/README.md §0.0</c>):
/// المعرّف الدائم للقرار هو <b>مفتاحه النصّي</b> — ذيل اسم ملفه — والرقم عرضٌ يُخصَّص
/// <b>عند الإنزال</b>. المؤلف يسمّي ملفه <c>ADR-جديد-اسم-المفتاح.md</c> ويكتب ترويسته
/// <c># ADR-جديد: …</c>، ومن يُنزل الدمج يخصّص الرقم.
/// </para>
/// <para>
/// <b>ولماذا هذا يهمّ حارساً بعينه:</b> المُسكِّن الذي كان مستعملاً — توزيع أرقام على الفروع
/// مسبقاً — يمنع التصادم لكنه <b>يُنتج فجوة على كل فرع بحكم البناء</b>، فيبقى
/// <see cref="AdrNumbersAreContiguousFromOne"/> أحمر لأسبوع لسبب لا علاقة له بما يحرسه،
/// وذلك تدريبٌ مباشر على تجاهل الحارس. القرار غير المُرقَّم لا يدخل فحص الاتّصال أصلاً،
/// فالفرع أخضر بلا إضعاف فحص واحد.
/// </para>
/// <para>
/// <b>ولماذا اختبار لا مراجعة:</b> الدمج نفسه كان نظيفاً، والمراجعة ترى ملفين بأسماء مختلفة.
/// لا يوجد في مسار العمل موضعٌ يصرخ فيه أحد — إلا هنا. والفحوص أدناه تُفشل البناء على:
/// رقم مكرَّر، وفجوة في الترقيم، ومفتاح مكرَّر أو مخالف للشكل، وترويسة داخلية تخالف اسم
/// الملف، ووثيقة على القرص غائبة عن فهرس <c>README.md</c> (أو مفهرسة وغير موجودة)، وكلمة
/// عدد في الفهرس تخالف العدّ الفعلي، وإشارة في أي مكان بالمستودع إلى رقم ADR لا وجود له —
/// بالأرقام اللاتينية <b>وبالعربية-الهندية</b> معاً، لأن الإشارة المكتوبة <c>ADR-٠٠١٦</c>
/// لا يراها بحثٌ بأرقام لاتينية، وهي بالضبط الطريقة التي «انتهى» بها تصادمٌ سابق وهو معطوب.
/// </para>
/// </summary>
public sealed partial class AdrRegisterIsSelfConsistent
{
    private const string DecisionsFolder = "docs/decisions";
    private const string IndexPath = "docs/decisions/README.md";

    /// <summary>النائب الذي يكتبه المؤلف بدل الرقم قبل الإنزال (‏README.md §0.0).</summary>
    private const string Placeholder = "جديد";

    /// <summary>الحدّ الأدنى للعدّ — حارس ضدّ مُحلِّل يقرأ صفراً فيمرّ فارغاً (فخ-43).</summary>
    private const int MinimumAdrCount = 24;

    /// <summary>أقصر مفتاح مقبول. مفتاحٌ من ثلاثة محارف ليس معرّفاً دائماً بل اختصار.</summary>
    private const int MinimumSlugLength = 8;

    private static readonly Lazy<Register> Parsed = new(Register.Load);

    /// <summary>
    /// كلمات العدد العربية كما تُكتب في صدر الفهرس.
    /// <para>
    /// <b>ومُدَّ إلى الثمانين حين بلغ السجل الثالث والسبعين.</b> والدرس أدناه هو سببُ المدّ
    /// نفسه: قاموسٌ ينفد يُسقط البناء برسالة «كلمة العدد غير معروفة» — وهي رسالة صحيحة عن
    /// حدٍّ في الحارس لا عن عطلٍ في الفهرس — فيُمَدّ عشرةً عشرة قبل أن يُصطدم به لا بعده.
    /// </para>
    /// <para>
    /// <b>الجدول يمتدّ إلى الأربعين عمداً.</b> النسخة الأولى وقفت عند «العشرون»، فأسقطت البناء
    /// عند القرار الحادي والعشرين برسالة «كلمة العدد غير معروفة» — وهي رسالة صحيحة عن حدٍّ في
    /// الحارس لا عن عطل في الفهرس. حارسٌ ينفد قاموسه يُدرّب من يصطدم به على توسيع القاموس
    /// بلا قراءة، وتلك أوّل خطوة نحو تعطيله.
    /// </para>
    /// </summary>
    private static readonly string[] CountWords =
    [
        "صفر", "الواحد", "الاثنان", "الثلاثة", "الأربعة", "الخمسة", "الستة", "السبعة", "الثمانية",
        "التسعة", "العشرة", "الأحد عشر", "الاثنا عشر", "الثلاثة عشر", "الأربعة عشر", "الخمسة عشر",
        "الستة عشر", "السبعة عشر", "الثمانية عشر", "التسعة عشر", "العشرون",
        "الواحد والعشرون", "الاثنان والعشرون", "الثلاثة والعشرون", "الأربعة والعشرون",
        "الخمسة والعشرون", "الستة والعشرون", "السبعة والعشرون", "الثمانية والعشرون",
        "التسعة والعشرون", "الثلاثون", "الواحد والثلاثون", "الاثنان والثلاثون",
        "الثلاثة والثلاثون", "الأربعة والثلاثون", "الخمسة والثلاثون", "الستة والثلاثون",
        "السبعة والثلاثون", "الثمانية والثلاثون", "التسعة والثلاثون", "الأربعون",
        "الواحد والأربعون", "الاثنان والأربعون", "الثلاثة والأربعون", "الأربعة والأربعون",
        "الخمسة والأربعون", "الستة والأربعون", "السبعة والأربعون", "الثمانية والأربعون",
        "التسعة والأربعون", "الخمسون",
        "الحادي والخمسون", "الثاني والخمسون", "الثالث والخمسون", "الرابع والخمسون",
        "الخامس والخمسون", "السادس والخمسون", "السابع والخمسون", "الثامن والخمسون",
        "التاسع والخمسون", "الستون", "الحادي والستون", "الثاني والستون",
        "الثالث والستون", "الرابع والستون", "الخامس والستون", "السادس والستون",
        "السابع والستون", "الثامن والستون", "التاسع والستون", "السبعون",
        "الحادي والسبعون", "الثاني والسبعون", "الثالث والسبعون", "الرابع والسبعون",
        "الخامس والسبعون", "السادس والسبعون", "السابع والسبعون", "الثامن والسبعون",
        "التاسع والسبعون", "الثمانون",
    ];

    // ── الأنماط ─────────────────────────────────────────────────────────────

    /// <summary>
    /// اسم ملف القرار: <c>ADR-0018-http-surface-as-a-published-contract.md</c> قبل الإنزال
    /// أو <c>ADR-جديد-http-surface-as-a-published-contract.md</c> بعده. والمفتاح مُلتقَط
    /// في المجموعتين، لأنه هو المعرّف الدائم في الحالتين.
    /// </summary>
    [GeneratedRegex(@"^ADR-(?<id>[0-9]{4}|جديد)-(?<slug>[a-z0-9][a-z0-9-]*)\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();

    /// <summary>الترويسة الداخلية: أول سطر <c># ADR-0018: …</c> أو <c># ADR-جديد: …</c>.</summary>
    [GeneratedRegex(@"^#\s*ADR-(?<id>[0-9]{4}|جديد):\s*(?<title>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    /// <summary>صفّ الفهرس: <c>| [0018](ADR-0018-….md) | … |</c> أو <c>| [جديد](ADR-جديد-….md) | … |</c>.</summary>
    [GeneratedRegex(@"^\|\s*\[(?<id>[0-9]{4}|جديد)\]\((?<file>ADR-(?:[0-9]{4}|جديد)-[a-z0-9-]+\.md)\)\s*\|(?<rest>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex IndexRowPattern();

    /// <summary>كلمة العدد في صدر الفهرس: <c>**الأربعة والعشرون ADR**</c>.</summary>
    [GeneratedRegex(@"\*\*(?<word>[\p{IsArabic}\s]+?)\s+ADR\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex CountWordPattern();

    /// <summary>
    /// ‏<c>ADR-0018</c> بأرقام لاتينية. و<c>[0-9]</c> مكتوبة صراحةً بدل <c>\d</c> عن قصد:
    /// ‏<c>\d</c> في .NET يطابق كل أرقام يونيكود ومنها العربية-الهندية، فيبتلع
    /// <c>ADR-٠٠١٦</c> ثم يرميها إلى <c>int.Parse</c> الثابتة فترمي استثناءً — وهو فخ-25
    /// نفسه واقعاً داخل الحارس المكتوب لمنعه.
    /// </summary>
    [GeneratedRegex(@"ADR-([0-9]{4})(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex LatinDigitReference();

    /// <summary>‏<c>ADR-٠٠١٦</c> — الأرقام العربية-الهندية. هذه الصيغة هي ما يفوت البحث.</summary>
    [GeneratedRegex(@"ADR-([٠-٩]{4})", RegexOptions.CultureInvariant)]
    private static partial Regex ArabicIndicReference();

    /// <summary>‏<c>ADR-۰۰۱۶</c> — الأرقام الفارسية الموسّعة، لأن النصّ نفسه قد يُنسخ بها.</summary>
    [GeneratedRegex(@"ADR-([۰-۹]{4})", RegexOptions.CultureInvariant)]
    private static partial Regex EasternArabicReference();

    // ── الفحوص ──────────────────────────────────────────────────────────────

    /// <summary>
    /// لا رقمان متطابقان على القرص. هذا هو التصادم بعينه: ملفان باسمين مختلفين يحملان
    /// الرقم نفسه، ويدمجهما <c>git</c> بلا تعارض.
    /// </summary>
    [Fact]
    public void EveryAdrNumberOnDiskIsUnique()
    {
        Register register = Parsed.Value;

        List<string> duplicates = [.. register.Documents
            .Where(static d => d.Number is not null)
            .GroupBy(static d => d.Number!.Value)
            .Where(static g => g.Count() > 1)
            .Select(static g => FormattableString.Invariant(
                $"الرقم {g.Key:0000} تحمله {g.Count()} وثائق: {string.Join(" · ", g.Select(static d => d.FileName))}"))];

        Assert.True(
            duplicates.Count == 0,
            "رقم قرار مُخصَّص لأكثر من وثيقة — وهذا هو التصادم الذي يمرّ من الدمج بلا تعارض:\n"
            + string.Join('\n', duplicates));

        Assert.True(
            register.Documents.Count >= MinimumAdrCount,
            FormattableString.Invariant($"قُرئت {register.Documents.Count} وثيقة قرار فقط — المُحلِّل ضامر والقاعدة تمرّ فراغاً"));
    }

    /// <summary>
    /// الأرقام <b>المُخصَّصة</b> متّصلة من 1 إلى N. الفجوة تعني إمّا وثيقة حُذفت وبقي مكانها،
    /// وإمّا أرقاماً وُزّعت على الفروع مسبقاً — وهو البديل المرفوض صراحةً في §0.0.
    /// <para>
    /// <b>والقرار غير المُرقَّم لا يدخل هذا الفحص أصلاً</b>، وهذا هو بيت القصيد: مؤلّفٌ يكتب
    /// <c>ADR-جديد-…</c> يبقى فرعه أخضر، فلا يتعلّم أحدٌ تجاهل حارسٍ أحمر لسبب لا يخصّه.
    /// ولا فحص واحد أُضعف مقابل ذلك: الفجوة بين رقمين مُخصَّصين ما زالت تُفشل البناء.
    /// </para>
    /// </summary>
    [Fact]
    public void AdrNumbersAreContiguousFromOne()
    {
        Register register = Parsed.Value;
        List<int> numbers = [.. register.Documents
            .Where(static d => d.Number is not null)
            .Select(static d => d.Number!.Value)
            .Distinct()
            .Order()];

        List<string> gaps = [];
        for (int expected = 1; expected <= numbers.Count; expected++)
        {
            if (!numbers.Contains(expected))
            {
                gaps.Add(FormattableString.Invariant($"فجوة في الترقيم عند {expected:0000}"));
            }
        }

        Assert.True(
            gaps.Count == 0,
            "ترقيم سجل القرارات غير متّصل — والرقم يُخصَّص عند الإنزال بالتسلسل، ولا يُحجَز مسبقاً "
            + "(‏docs/decisions/README.md §0.0):\n"
            + string.Join('\n', gaps));

        // حارس اللافراغ: لو صارت الوثائق كلها بلا رقم لمرّ الفحص فارغاً، وهو عطل فخ-43.
        Assert.True(
            numbers.Count >= MinimumAdrCount,
            FormattableString.Invariant($"عُدَّ {numbers.Count} قراراً مُرقَّماً فقط — المُحلِّل ضامر والقاعدة تمرّ فراغاً"));
    }

    /// <summary>
    /// لكل قرار مفتاح واحد، والمفاتيح فريدة وعلى الشكل المُلزِم وليست اختصارات.
    /// <b>المفتاح هو المعرّف الدائم</b> (‏README.md §0.0)، فمفتاح مكرَّر يعيد التصادم نفسه
    /// في مستوى آخر: وثيقتان لا تُميَّز إحداهما عن الأخرى إلا برقمٍ قيل إنه عرض.
    /// </summary>
    [Fact]
    public void EveryAdrCarriesAUniquePermanentSlug()
    {
        Register register = Parsed.Value;

        List<string> problems = [.. register.Documents
            .Where(d => d.Slug.Length < MinimumSlugLength)
            .Select(static d => $"{d.FileName}: المفتاح «{d.Slug}» أقصر من أن يكون وصفياً")];

        problems.AddRange(register.Documents
            .GroupBy(static d => d.Slug, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => FormattableString.Invariant(
                $"المفتاح «{g.Key}» مستعمل {g.Count()} مرات: {string.Join(" · ", g.Select(static d => d.FileName))}")));

        Assert.True(
            problems.Count == 0,
            "مفاتيح القرارات غير سليمة — والمفتاح هو المعرّف الدائم (docs/decisions/README.md §0.0):\n"
            + string.Join('\n', problems));

        Assert.True(
            register.Documents.Count >= MinimumAdrCount,
            FormattableString.Invariant($"قُرئت {register.Documents.Count} وثيقة قرار فقط — المُحلِّل ضامر والقاعدة تمرّ فراغاً"));
    }

    /// <summary>
    /// ترويسة الوثيقة الداخلية تطابق اسم ملفها — رقماً كانت أو نائباً. إعادة تسمية الملف بلا
    /// تعديل الترويسة تترك وثيقة تُعرّف نفسها برقم غير رقمها — وهو ما يقرؤه الإنسان، لا اسم
    /// الملف. وهذا الفحص هو ما يمنع <b>إنزالاً نصف مُنجَز</b>: ملفٌّ سُمِّي بالرقم وترويسته
    /// ما زالت <c>ADR-جديد</c>، أو العكس.
    /// </summary>
    [Fact]
    public void EveryAdrHeadingNumberMatchesItsFileName()
    {
        Register register = Parsed.Value;

        List<string> mismatched = [.. register.Documents
            .Where(static d => !string.Equals(d.HeadingToken, d.FileToken, StringComparison.Ordinal))
            .Select(static d => d.HeadingToken is null
                ? $"{d.FileName}: لا ترويسة «# ADR-NNNN:» ولا «# ADR-جديد:» في أول سطر"
                : $"{d.FileName}: الترويسة تقول ADR-{d.HeadingToken} واسم الملف يقول ADR-{d.FileToken}")];

        Assert.True(
            mismatched.Count == 0,
            "ترويسة قرار تخالف اسم ملفه — والقارئ يصدّق الترويسة:\n" + string.Join('\n', mismatched));

        // حارس اللافراغ: مقارنةٌ على مجموعة فارغة تمرّ دائماً — وهو بالضبط عطل فخ-43.
        Assert.True(
            register.Documents.Count >= MinimumAdrCount,
            FormattableString.Invariant($"قُرئت {register.Documents.Count} وثيقة قرار فقط — المُحلِّل ضامر والقاعدة تمرّ فراغاً"));
    }

    /// <summary>
    /// ما على القرص وما في الفهرس مجموعة واحدة، في الاتجاهين. وثيقة غير مفهرسة وثيقة
    /// لا يجدها أحد؛ وصفّ فهرس بلا وثيقة رابط مكسور — وقد وقع الأول فعلاً في دمج
    /// <c>ADR-0016</c>.
    /// </summary>
    [Fact]
    public void TheIndexAndTheDirectoryAreTheSameSet()
    {
        Register register = Parsed.Value;
        List<string> problems = [];

        foreach (Document document in register.Documents.OrderBy(static d => d.Number ?? int.MaxValue).ThenBy(static d => d.Slug, StringComparer.Ordinal))
        {
            if (!register.IndexedFiles.Contains(document.FileName))
            {
                problems.Add($"وثيقة على القرص وغائبة عن فهرس {IndexPath}: {document.FileName}");
            }
        }

        foreach (string indexed in register.IndexedFiles.Order(StringComparer.Ordinal))
        {
            if (!register.Documents.Any(d => string.Equals(d.FileName, indexed, StringComparison.Ordinal)))
            {
                problems.Add($"صفّ فهرس بلا وثيقة على القرص: {indexed}");
            }
        }

        foreach ((string rowToken, string file) in register.IndexRows)
        {
            Match name = FileNamePattern().Match(file);
            if (name.Success && !string.Equals(name.Groups["id"].Value, rowToken, StringComparison.Ordinal))
            {
                problems.Add($"صفّ الفهرس {rowToken} يشير إلى {file}");
            }
        }

        Assert.True(problems.Count == 0, "فهرس القرارات لا يطابق ما على القرص:\n" + string.Join('\n', problems));
        Assert.True(
            register.IndexedFiles.Count >= MinimumAdrCount,
            FormattableString.Invariant($"قُرئ {register.IndexedFiles.Count} صفّ فهرس فقط — المُحلِّل ضامر والقاعدة تمرّ فراغاً"));
    }

    /// <summary>
    /// كلمة العدد في صدر الفهرس تساوي العدّ الفعلي. رقمٌ يُكتب بيد ولا يُشتقّ من البيانات
    /// ينحرف عند أول إضافة — وقد انحرف فعلاً: كتب الفهرس «السبعة عشر» وعلى القرص ستّ عشرة.
    /// <para>
    /// <b>وهذا هو السطر الوحيد الذي يتعارض عمداً</b> حين يهبط فرعان في اليوم نفسه: كلاهما
    /// يكتب الكلمة نفسها وقد صار العدد أكبر. التعارض مقصود، وهذا الفحص هو ما يجعله مرئياً.
    /// </para>
    /// </summary>
    [Fact]
    public void TheStatedCountWordMatchesTheActualCount()
    {
        Register register = Parsed.Value;

        Assert.True(
            register.StatedCountWord is not null,
            $"لم يُعثر على كلمة العدد «**… ADR**» في {IndexPath} — والعدد المُعلن ليس زينة: يُقتبَس في العروض");

        int stated = Array.FindIndex(CountWords, w => string.Equals(w, register.StatedCountWord, StringComparison.Ordinal));

        Assert.True(
            stated >= 0,
            $"كلمة العدد «{register.StatedCountWord}» غير معروفة — تُكتب بإحدى صيغ: {string.Join(" · ", CountWords[1..])}");

        Assert.True(
            stated == register.Documents.Count,
            FormattableString.Invariant(
                $"الفهرس يقول «{register.StatedCountWord}» (أي {stated}) وعلى القرص {register.Documents.Count} وثيقة قرار."));
    }

    /// <summary>
    /// كل إشارة إلى رقم ADR في <b>أي</b> ملف بالمستودع تصل إلى قرار موجود — بالأرقام
    /// اللاتينية، وبالعربية-الهندية، وبالفارسية الموسّعة.
    /// <para>
    /// هذا هو الفحص الذي كان غيابه يجعل إعادة الترقيم «تنتهي» وهي معطوبة: مرجع
    /// <c>ADR-٠٠١٦</c> في وثيقة عربية لا يراه <c>grep</c> بأرقام لاتينية، ولا يصرخ عنه شيء.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryAdrReferenceInTheRepositoryResolves()
    {
        Register register = Parsed.Value;
        HashSet<int> numbers = [.. register.Documents.Where(static d => d.Number is not null).Select(static d => d.Number!.Value)];

        List<string> dangling = [];
        int filesScanned = 0;
        int referencesSeen = 0;

        foreach (string path in TextFiles())
        {
            // شجرةُ عملٍ حيّة: ملفٌّ قد يختفي بين تعداده وقراءته. تخطٍّ صامتٌ هنا
            // آمنٌ لأن حارس اللافراغ أدناه يمنع أن يصير التخطّي هو القاعدة.
            if (!File.Exists(path))
            {
                continue;
            }

            filesScanned++;
            string text = File.ReadAllText(path);
            string relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');

            foreach (Match m in LatinDigitReference().Matches(text))
            {
                referencesSeen++;
                int n = int.Parse(m.Groups[1].Value, CultureInfo.InvariantCulture);
                if (!numbers.Contains(n))
                {
                    dangling.Add(FormattableString.Invariant($"{relative}: «ADR-{n:0000}» لا يقابل قراراً في السجل"));
                }
            }

            foreach (Regex pattern in new[] { ArabicIndicReference(), EasternArabicReference() })
            {
                foreach (Match m in pattern.Matches(text))
                {
                    referencesSeen++;
                    int n = FromNonLatinDigits(m.Groups[1].Value);
                    if (!numbers.Contains(n))
                    {
                        dangling.Add(FormattableString.Invariant($"{relative}: «{m.Value}» (أي {n:0000}) لا يقابل قراراً في السجل"));
                    }
                }
            }
        }

        Assert.True(
            dangling.Count == 0,
            "إشارات إلى قرارات غير موجودة:\n" + string.Join('\n', dangling.Distinct(StringComparer.Ordinal)));

        // حارس اللافراغ: مسحٌ لا يقرأ شيئاً يمرّ دائماً — وهو بالضبط عطل فخ-43.
        Assert.True(filesScanned >= 100, FormattableString.Invariant($"المسح قرأ {filesScanned} ملفاً فقط — النطاق ضامر"));
        Assert.True(referencesSeen >= 40, FormattableString.Invariant($"المسح وجد {referencesSeen} إشارة فقط — الأنماط لم تعد تطابق"));
    }

    /// <summary>
    /// نصّ القاعدة نفسه موجود وفي صدر الفهرس، قبل جدول القرارات. قاعدةٌ يقرأها من يضيف القرار
    /// التالي <b>بعد</b> أن أضافه ليست قاعدة. ونظيرها في سجل المصائد هو
    /// <c>TheAllocationRuleIsDocumentedAtTheTopOfTheRegister</c> — وغيابه هنا هو حرفياً
    /// ما جعل التصادم يتكرّر في هذا السجل بعد أن حُرس ذاك (فخ-52).
    /// </summary>
    [Fact]
    public void TheAllocationRuleIsDocumentedBeforeTheIndex()
    {
        string text = File.ReadAllText(Path.Combine(RepositoryLayout.Root, IndexPath));
        int rule = text.IndexOf("قاعدة تخصيص المعرّفات", StringComparison.Ordinal);
        int index = text.IndexOf("## الفهرس", StringComparison.Ordinal);

        Assert.True(rule >= 0, "قاعدة تخصيص المعرّفات مفقودة من docs/decisions/README.md — بدونها يعود التصادم في أول فرعين متوازيين");
        Assert.True(index > rule, "قاعدة تخصيص المعرّفات يجب أن تسبق الفهرس: تُقرأ قبل إضافة القرار لا بعده");
        Assert.Contains("ADR-جديد", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// حارس لافراغ الأنماط نفسها: يُثبت أن الصيغ الثلاث تُلتقَط فعلاً، وأن نمط الأرقام
    /// اللاتينية لا يبتلع العربية-الهندية، وأن النائب <c>جديد</c> يُقبَل في المواضع الثلاثة
    /// (اسم الملف، والترويسة، وصفّ الفهرس) ولا يُقرأ رقماً. نمطٌ توقّف عن المطابقة يجعل كل
    /// ما فوقه يمرّ فارغاً.
    /// </summary>
    [Fact]
    public void TheReferencePatternsAreNotVacuous()
    {
        Assert.Equal("0016", LatinDigitReference().Match("انظر ADR-0016 هنا").Groups[1].Value);
        Assert.Equal("٠٠١٦", ArabicIndicReference().Match("انظر ADR-٠٠١٦ هنا").Groups[1].Value);
        Assert.Equal("۰۰۱۶", EasternArabicReference().Match("انظر ADR-۰۰۱۶ هنا").Groups[1].Value);
        Assert.Equal(16, FromNonLatinDigits("٠٠١٦"));
        Assert.Equal(16, FromNonLatinDigits("۰۰۱۶"));

        Assert.DoesNotMatch(LatinDigitReference(), "انظر ADR-٠٠١٦ هنا");
        Assert.DoesNotMatch(ArabicIndicReference(), "انظر ADR-0016 هنا");
        Assert.DoesNotMatch(LatinDigitReference(), "ADR-00160");

        // النائب لا يُقرأ رقماً بأي من الأنماط الثلاثة — وإلا لسقط في int.Parse.
        Assert.DoesNotMatch(LatinDigitReference(), "انظر ADR-جديد هنا");
        Assert.DoesNotMatch(ArabicIndicReference(), "انظر ADR-جديد هنا");
        Assert.DoesNotMatch(EasternArabicReference(), "انظر ADR-جديد هنا");

        Assert.Equal("0018", FileNamePattern().Match("ADR-0018-http-surface-as-a-published-contract.md").Groups["id"].Value);
        Assert.Equal("http-surface-as-a-published-contract", FileNamePattern().Match("ADR-0018-http-surface-as-a-published-contract.md").Groups["slug"].Value);
        Assert.Equal(Placeholder, FileNamePattern().Match("ADR-جديد-http-surface-as-a-published-contract.md").Groups["id"].Value);
        Assert.Equal("http-surface-as-a-published-contract", FileNamePattern().Match("ADR-جديد-http-surface-as-a-published-contract.md").Groups["slug"].Value);
        Assert.DoesNotMatch(FileNamePattern(), "ADR-0018.md");

        Assert.Equal("0018", HeadingPattern().Match("# ADR-0018: سطح HTTP عقدٌ منشور").Groups["id"].Value);
        Assert.Equal(Placeholder, HeadingPattern().Match("# ADR-جديد: سطح HTTP عقدٌ منشور").Groups["id"].Value);

        Assert.Matches(IndexRowPattern(), "| [0018](ADR-0018-http-surface-as-a-published-contract.md) | عنوان | مقبول |");
        Assert.Matches(IndexRowPattern(), "| [جديد](ADR-جديد-http-surface-as-a-published-contract.md) | عنوان | مقترح |");
        Assert.Equal("الثمانية عشر", CountWordPattern().Match("(هذه) + **الثمانية عشر ADR** | القرارات").Groups["word"].Value);

        Assert.True(Parsed.Value.Documents.Count >= MinimumAdrCount);
        Assert.NotEmpty(Parsed.Value.IndexRows);
    }

    // ── الأدوات ─────────────────────────────────────────────────────────────

    private static int FromNonLatinDigits(string value)
    {
        StringBuilder latin = new(value.Length);
        foreach (char c in value)
        {
            latin.Append(c switch
            {
                >= '٠' and <= '٩' => (char)('0' + (c - '٠')),
                >= '۰' and <= '۹' => (char)('0' + (c - '۰')),
                _ => c,
            });
        }

        return int.Parse(latin.ToString(), CultureInfo.InvariantCulture);
    }

    private static IEnumerable<string> TextFiles()
    {
        string[] extensions =
        [
            ".md", ".cs", ".csproj", ".props", ".slnx", ".yml", ".yaml",
            ".json", ".html", ".css", ".js", ".sql", ".txt", ".sh",
        ];

        return Directory
            .EnumerateFiles(RepositoryLayout.Root, "*", SearchOption.AllDirectories)
            .Where(path =>
            {
                string relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
                // مخرَج البناء ليس محتوى المستودع. واستثناؤه ليس تجميلاً: قاعدةُ مسحٍ
                // هي القرص لا المستودع **تقيس البيئة لا الشيفرة** — وقد كلّف ذلك دورة
                // تنقيح كاملة من قبل. و«dist» تحديداً يزرع فيها حارسٌ آخر شاهداً موجباً
                // ثم يحذفه، فيتسابق المسحان على ملفّ يختفي بينهما.
                if (relative.StartsWith(".git/", StringComparison.Ordinal)
                    || relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("/dist/", StringComparison.Ordinal)
                    || relative.Contains("/node_modules/", StringComparison.Ordinal))
                {
                    return false;
                }

                return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
            })
            .Order(StringComparer.Ordinal);
    }

    // ── النموذج ─────────────────────────────────────────────────────────────

    /// <summary>
    /// وثيقة قرار. <c>FileToken</c> و<c>HeadingToken</c> نصّان لا رقمان عمداً: النائب
    /// <c>جديد</c> ليس رقماً، ومقارنته رقماً تعني إمّا استثناءً في <c>int.Parse</c> وإمّا
    /// قيمةً سحرية تُنسى. و<c>Number</c> يكون <c>null</c> قبل الإنزال.
    /// </summary>
    private sealed record Document(string FileToken, int? Number, string Slug, string FileName, string? HeadingToken, string Title);

    private sealed record Register(
        IReadOnlyList<Document> Documents,
        IReadOnlyList<(string Token, string File)> IndexRows,
        IReadOnlySet<string> IndexedFiles,
        string? StatedCountWord)
    {
        public static Register Load()
        {
            string folder = Path.Combine(RepositoryLayout.Root, DecisionsFolder);
            List<Document> documents = [];

            foreach (string path in Directory.EnumerateFiles(folder, "*.md").Order(StringComparer.Ordinal))
            {
                string name = Path.GetFileName(path);
                Match file = FileNamePattern().Match(name);
                if (!file.Success)
                {
                    continue;
                }

                string[] lines = File.ReadAllLines(path);
                Match heading = lines.Length > 0 ? HeadingPattern().Match(lines[0]) : Match.Empty;
                string token = file.Groups["id"].Value;

                documents.Add(new Document(
                    token,
                    string.Equals(token, Placeholder, StringComparison.Ordinal)
                        ? null
                        : int.Parse(token, CultureInfo.InvariantCulture),
                    file.Groups["slug"].Value,
                    name,
                    heading.Success ? heading.Groups["id"].Value : null,
                    heading.Success ? heading.Groups["title"].Value : string.Empty));
            }

            string indexText = File.ReadAllText(Path.Combine(RepositoryLayout.Root, IndexPath));
            List<(string, string)> rows = [];

            foreach (string line in indexText.Split('\n'))
            {
                Match row = IndexRowPattern().Match(line.TrimEnd('\r'));
                if (row.Success)
                {
                    rows.Add((row.Groups["id"].Value, row.Groups["file"].Value));
                }
            }

            Match count = CountWordPattern().Match(indexText);

            return new Register(
                documents,
                rows,
                new HashSet<string>(rows.Select(static r => r.Item2), StringComparer.Ordinal),
                count.Success ? count.Groups["word"].Value.Trim() : null);
        }
    }
}
