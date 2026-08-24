using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>حارس سجل القرارات (ADR) — الرقم معرّف مُخصَّص، لا اسم يختاره كل فرع لنفسه.</b>
/// <para>
/// <b>العطل الذي يجعله مستحيلاً:</b> فرعان متوازيان أنشأ كلٌّ منهما <c>ADR-0016</c>: أحدهما
/// «هوية الترحيل تشمل رمز الحدث» والآخر «سطح HTTP عقدٌ منشور». و<c>git</c> دمجهما
/// <b>بلا تعارض واحد</b> لأن اسمَي الملفين مختلفان — فصار في السجل وثيقتان تحملان الرقم
/// نفسه ولم يشتكِ شيء. وهذه هي <b>المرّة الرابعة</b> لهذا الصنف من العطل في هذا المستودع
/// (فخ-38/39 · «القاعدة 10» · فخ-41 · ثم هذه)، وقد عولج مرّةً في سجل المصائد بجعل المفتاح
/// النصّي هو المعرّف الدائم والرقم عرضاً يُخصَّص عند الدمج — ولم تُعطَ الـADR الحماية نفسها،
/// فتكرّر التصادم حرفياً.
/// </para>
/// <para>
/// <b>ولماذا اختبار لا مراجعة:</b> الدمج نفسه كان نظيفاً، والمراجعة ترى ملفين بأسماء مختلفة.
/// لا يوجد في مسار العمل موضعٌ يصرخ فيه أحد — إلا هنا. والفحوص أدناه تُفشل البناء على:
/// رقم مكرَّر، وفجوة في الترقيم، وترويسة داخلية تخالف اسم الملف، ووثيقة على القرص غائبة عن
/// فهرس <c>README.md</c> (أو مفهرسة وغير موجودة)، وكلمة عدد في الفهرس تخالف العدّ الفعلي،
/// وإشارة في أي مكان بالمستودع إلى رقم ADR لا وجود له — بالأرقام اللاتينية
/// <b>وبالعربية-الهندية</b> معاً، لأن الإشارة المكتوبة <c>ADR-٠٠١٦</c> لا يراها بحثٌ بأرقام
/// لاتينية، وهي بالضبط الطريقة التي «انتهى» بها تصادمٌ سابق وهو معطوب.
/// </para>
/// </summary>
public sealed partial class AdrRegisterIsSelfConsistent
{
    private const string DecisionsFolder = "docs/decisions";
    private const string IndexPath = "docs/decisions/README.md";

    /// <summary>الحدّ الأدنى للعدّ — حارس ضدّ مُحلِّل يقرأ صفراً فيمرّ فارغاً (فخ-43).</summary>
    private const int MinimumAdrCount = 18;

    private static readonly Lazy<Register> Parsed = new(Register.Load);

    /// <summary>كلمات العدد العربية كما تُكتب في صدر الفهرس. الفهرس يقول «الثمانية عشر ADR».</summary>
    private static readonly string[] CountWords =
    [
        "صفر", "الواحد", "الاثنان", "الثلاثة", "الأربعة", "الخمسة", "الستة", "السبعة", "الثمانية",
        "التسعة", "العشرة", "الأحد عشر", "الاثنا عشر", "الثلاثة عشر", "الأربعة عشر", "الخمسة عشر",
        "الستة عشر", "السبعة عشر", "الثمانية عشر", "التسعة عشر", "العشرون",
    ];

    // ── الأنماط ─────────────────────────────────────────────────────────────

    /// <summary>اسم ملف القرار: <c>ADR-0018-http-surface-as-a-published-contract.md</c>.</summary>
    [GeneratedRegex(@"^ADR-(?<id>[0-9]{4})-(?<slug>[a-z0-9][a-z0-9-]*)\.md$", RegexOptions.CultureInvariant)]
    private static partial Regex FileNamePattern();

    /// <summary>الترويسة الداخلية: أول سطر <c># ADR-0018: …</c>.</summary>
    [GeneratedRegex(@"^#\s*ADR-(?<id>[0-9]{4}):\s*(?<title>.+?)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    /// <summary>صفّ الفهرس: <c>| [0018](ADR-0018-….md) | … |</c>.</summary>
    [GeneratedRegex(@"^\|\s*\[(?<id>[0-9]{4})\]\((?<file>ADR-[0-9]{4}-[a-z0-9-]+\.md)\)\s*\|(?<rest>.*)$", RegexOptions.CultureInvariant)]
    private static partial Regex IndexRowPattern();

    /// <summary>كلمة العدد في صدر الفهرس: <c>**الثمانية عشر ADR**</c>.</summary>
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
            .GroupBy(static d => d.Number)
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
    /// الأرقام متّصلة من 1 إلى N. الفجوة تعني إمّا وثيقة حُذفت وبقي مكانها، وإمّا ترقيماً
    /// بمدى لكل فرع — وهو ما يُنتج التصادم التالي.
    /// </summary>
    [Fact]
    public void AdrNumbersAreContiguousFromOne()
    {
        Register register = Parsed.Value;
        List<int> numbers = [.. register.Documents.Select(static d => d.Number).Distinct().Order()];

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
            "ترقيم سجل القرارات غير متّصل — والرقم التالي يُخصَّص بالتسلسل عند الدمج:\n"
            + string.Join('\n', gaps));

        Assert.NotEmpty(numbers);
    }

    /// <summary>
    /// ترويسة الوثيقة الداخلية تطابق اسم ملفها. إعادة تسمية الملف بلا تعديل الترويسة تترك
    /// وثيقة تُعرّف نفسها برقم غير رقمها — وهو ما يقرؤه الإنسان، لا اسم الملف.
    /// </summary>
    [Fact]
    public void EveryAdrHeadingNumberMatchesItsFileName()
    {
        Register register = Parsed.Value;

        List<string> mismatched = [.. register.Documents
            .Where(static d => d.HeadingNumber != d.Number)
            .Select(static d => d.HeadingNumber is null
                ? $"{d.FileName}: لا ترويسة «# ADR-NNNN:» في أول سطر"
                : FormattableString.Invariant($"{d.FileName}: الترويسة تقول ADR-{d.HeadingNumber:0000} واسم الملف يقول ADR-{d.Number:0000}"))];

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
    /// لا يجدها أحد؛ وصفّ فهرس بلا وثيقة رابط مكسور — وقد وقع الأول فعلاً في هذا الدمج.
    /// </summary>
    [Fact]
    public void TheIndexAndTheDirectoryAreTheSameSet()
    {
        Register register = Parsed.Value;
        List<string> problems = [];

        foreach (Document document in register.Documents.OrderBy(static d => d.Number))
        {
            if (!register.IndexedFiles.Contains(document.FileName))
            {
                problems.Add(FormattableString.Invariant(
                    $"وثيقة على القرص وغائبة عن فهرس {IndexPath}: {document.FileName}"));
            }
        }

        foreach (string indexed in register.IndexedFiles.Order(StringComparer.Ordinal))
        {
            if (!register.Documents.Any(d => string.Equals(d.FileName, indexed, StringComparison.Ordinal)))
            {
                problems.Add($"صفّ فهرس بلا وثيقة على القرص: {indexed}");
            }
        }

        foreach ((int rowNumber, string file) in register.IndexRows)
        {
            Match name = FileNamePattern().Match(file);
            if (name.Success && int.Parse(name.Groups["id"].Value, CultureInfo.InvariantCulture) != rowNumber)
            {
                problems.Add(FormattableString.Invariant($"صفّ الفهرس {rowNumber:0000} يشير إلى {file}"));
            }
        }

        Assert.True(problems.Count == 0, "فهرس القرارات لا يطابق ما على القرص:\n" + string.Join('\n', problems));
        Assert.NotEmpty(register.IndexedFiles);
    }

    /// <summary>
    /// كلمة العدد في صدر الفهرس تساوي العدّ الفعلي. رقمٌ يُكتب بيد ولا يُشتقّ من البيانات
    /// ينحرف عند أول إضافة — وقد انحرف فعلاً: كتب الفهرس «السبعة عشر» وعلى القرص ستّ عشرة.
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
        HashSet<int> numbers = [.. register.Documents.Select(static d => d.Number)];

        List<string> dangling = [];
        int filesScanned = 0;
        int referencesSeen = 0;

        foreach (string path in TextFiles())
        {
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
    /// حارس لافراغ الأنماط نفسها: يُثبت أن الصيغ الثلاث تُلتقَط فعلاً، وأن نمط الأرقام
    /// اللاتينية لا يبتلع العربية-الهندية. نمطٌ توقّف عن المطابقة يجعل كل ما فوقه يمرّ فارغاً.
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

        Assert.Equal("0018", FileNamePattern().Match("ADR-0018-http-surface-as-a-published-contract.md").Groups["id"].Value);
        Assert.Equal("0018", HeadingPattern().Match("# ADR-0018: سطح HTTP عقدٌ منشور").Groups["id"].Value);
        Assert.Matches(IndexRowPattern(), "| [0018](ADR-0018-http-surface-as-a-published-contract.md) | عنوان | مقبول |");
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
                if (relative.StartsWith(".git/", StringComparison.Ordinal)
                    || relative.Contains("/bin/", StringComparison.Ordinal)
                    || relative.Contains("/obj/", StringComparison.Ordinal)
                    || relative.Contains("/node_modules/", StringComparison.Ordinal))
                {
                    return false;
                }

                return extensions.Contains(Path.GetExtension(path), StringComparer.OrdinalIgnoreCase);
            })
            .Order(StringComparer.Ordinal);
    }

    // ── النموذج ─────────────────────────────────────────────────────────────

    private sealed record Document(int Number, string FileName, int? HeadingNumber, string Title);

    private sealed record Register(
        IReadOnlyList<Document> Documents,
        IReadOnlyList<(int Number, string File)> IndexRows,
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

                documents.Add(new Document(
                    int.Parse(file.Groups["id"].Value, CultureInfo.InvariantCulture),
                    name,
                    heading.Success ? int.Parse(heading.Groups["id"].Value, CultureInfo.InvariantCulture) : null,
                    heading.Success ? heading.Groups["title"].Value : string.Empty));
            }

            string indexText = File.ReadAllText(Path.Combine(RepositoryLayout.Root, IndexPath));
            List<(int, string)> rows = [];

            foreach (string line in indexText.Split('\n'))
            {
                Match row = IndexRowPattern().Match(line.TrimEnd('\r'));
                if (row.Success)
                {
                    rows.Add((int.Parse(row.Groups["id"].Value, CultureInfo.InvariantCulture), row.Groups["file"].Value));
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
