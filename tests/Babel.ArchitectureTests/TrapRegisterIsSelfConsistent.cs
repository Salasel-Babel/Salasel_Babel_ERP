using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>حارس سجل المصائد — قاعدة تخصيص المعرّفات مُنفَّذة لا موصوفة.</b>
/// <para>
/// <b>العطل الذي يجعله مستحيلاً:</b> تصادم معرّفات بين فرعين متوازيين. وقع <b>أربع مرات</b>
/// في هذا المستودع (فخ-38/39، ثم «القاعدة 10»، ثم فخ-41، ثم <c>ADR-0016</c> في السجلّ الشقيق
/// بعد أن حُرس هذا السجلّ وحده — فخ-52)، وكل مرة كلّف إعادة ترقيم يدوية
/// عبر اثنتي عشرة وثيقة متقاطعة المراجع — وبعض المراجع مكتوب بالأرقام العربية-الهندية
/// (‏فخ-١٨) فلا يراه بحثٌ بأرقام لاتينية، فيبقى معطوباً بصمت بعد «انتهاء» الدمج.
/// </para>
/// <para>
/// <b>القاعدة المفروضة هنا</b> (نصّها الكامل في <c>docs/evidence/traps.md §0.0</c>):
/// المعرّف الدائم للفخّ هو <b>مفتاحه النصّي</b>، والرقم عرضٌ يُخصَّص <b>عند الدمج</b>.
/// المؤلف يكتب <c>فخ-جديد</c> بمفتاح، ومن يُنزل الدمج يخصّص الرقم — فلا إشارة مرجعية
/// واحدة تحتاج تعديلاً، لأن كل الإشارات تستشهد بالمفتاح.
/// </para>
/// <para>
/// <b>لماذا اختبار لا مراجعة:</b> المرات السابقة وقعت كلها ومعها اصطلاح مكتوب.
/// اصطلاحٌ لا يستطيع أحد مخالفته أثمن من اصطلاح يُقال للناس. وهذا الاختبار يُفشل البناء
/// على: رقم مكرَّر، فجوة في الترقيم، مفتاح مكرَّر أو مخالف للشكل، اختلاف بين الفهرس
/// والمراسي والعناوين، حصيلة معلنة لا تطابق العدّ، مجموع مُعاد في وثيقة شقيقة يخالف
/// السجل، أو إشارة إلى فخّ لا وجود له بأي من صيغ الأرقام الثلاث.
/// </para>
/// </summary>
public sealed partial class TrapRegisterIsSelfConsistent
{
    private const string RegisterPath = "docs/evidence/traps.md";

    /// <summary>الحدّ الأدنى للعدّ — حارس ضدّ مُحلِّل يقرأ صفراً فيمرّ فارغاً (فخ-43).</summary>
    private const int MinimumTrapCount = 73;

    private static readonly Lazy<Register> Parsed = new(Register.Load);

    // ── الشكل ───────────────────────────────────────────────────────────────

    [GeneratedRegex(@"^### فخ-(?<id>[0-9]{2,3}|جديد) — (?<title>.+?) · `(?<slug>[a-z][a-z0-9-]*)`\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingPattern();

    [GeneratedRegex(@"^<a id=""fakh-(?<token>[a-z0-9][a-z0-9-]*)""></a>\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex AnchorPattern();

    [GeneratedRegex(@"^\| \[فخ-(?<id>[0-9]{2,3}|جديد)\]\(#fakh-(?<target>[a-z0-9][a-z0-9-]*)\)", RegexOptions.CultureInvariant)]
    private static partial Regex IndexRowPattern();

    [GeneratedRegex(@"\*\*الحصيلة: (?<silent>[0-9]+) من (?<total>[0-9]+) فخّاً تفشل بصمت\*\* \(منها (?<delayed>\S+) بصمت مؤجّل.*?و\*\*(?<loud>[0-9]+) فقط\*\* تفشل بصوت عالٍ صريح، و\*\*(?<disguised>[0-9]+) تتنكّر\*\*", RegexOptions.CultureInvariant)]
    private static partial Regex TallyPattern();

    /// <summary>
    /// ‏<c>فخ-NN</c> بأرقام لاتينية. النظرة الأمامية تمنع التقاط <c>فخ-9f3a1c</c> رقماً.
    /// <para>
    /// <b>و<c>[0-9]</c> مكتوبة صراحةً بدل <c>\d</c> عن قصد:</b> ‏<c>\d</c> في .NET يطابق كل
    /// أرقام يونيكود، ومنها العربية-الهندية ٠-٩ — أي أن هذا النمط كان سيبتلع <c>فخ-١٨</c>
    /// ويحاول قراءتها بـ<c>int.Parse</c> الثابتة فيرمي. وهو فخ-25 نفسه واقعاً داخل الحارس
    /// الذي كُتب ليمنعه. مقيس هنا: النسخة الأولى من هذا الملف سقطت به.
    /// </para>
    /// </summary>
    [GeneratedRegex(@"فخ-([0-9]{1,3})(?![0-9A-Za-z٠-٩۰-۹])", RegexOptions.CultureInvariant)]
    private static partial Regex LatinDigitReference();

    /// <summary>‏<c>فخ-١٨</c> — الأرقام العربية-الهندية. هذه الصيغة هي ما فات البحث في الدمجات السابقة.</summary>
    [GeneratedRegex(@"فخ-([٠-٩]{1,3})", RegexOptions.CultureInvariant)]
    private static partial Regex ArabicIndicReference();

    /// <summary>‏<c>فخ-۱۸</c> — الأرقام الفارسية/الشرقية الموسّعة، لأن نفس النصّ قد يُنسخ بها.</summary>
    [GeneratedRegex(@"فخ-([۰-۹]{1,3})", RegexOptions.CultureInvariant)]
    private static partial Regex EasternArabicReference();

    /// <summary>الرابط العميق <c>#fakh-…</c> أو <c>fakh-…</c> — رقماً كان أو مفتاحاً.</summary>
    [GeneratedRegex(@"fakh-([0-9a-z][0-9a-z-]*)", RegexOptions.CultureInvariant)]
    private static partial Regex AnchorReference();

    // ── الفحوص ──────────────────────────────────────────────────────────────

    /// <summary>
    /// لكل فخّ مفتاح واحد، والمفاتيح فريدة وعلى الشكل المُلزِم. المفتاح هو المعرّف الدائم،
    /// فمفتاح مكرَّر يعيد المشكلة التي أُلغيت.
    /// </summary>
    [Fact]
    public void EveryTrapCarriesAUniquePermanentSlug()
    {
        Register register = Parsed.Value;

        List<string> malformed = [.. register.Traps
            .Where(static t => t.Slug.Length < 8)
            .Select(static t => $"{t.Display}: المفتاح «{t.Slug}» أقصر من أن يكون وصفياً")];

        List<string> duplicates = [.. register.Traps
            .GroupBy(static t => t.Slug, StringComparer.Ordinal)
            .Where(static g => g.Count() > 1)
            .Select(static g => $"المفتاح «{g.Key}» مستعمل {g.Count()} مرات")];

        Assert.True(
            malformed.Count == 0 && duplicates.Count == 0,
            "مفاتيح المصائد غير سليمة — والمفتاح هو المعرّف الدائم (traps.md §0.0):\n"
            + string.Join('\n', malformed.Concat(duplicates)));
    }

    /// <summary>
    /// الأرقام المُخصَّصة متّصلة من 1 إلى N بلا تكرار ولا فجوة. الفجوة تعني ترقيماً بمدى
    /// لكل فرع — وهو البديل المرفوض صراحةً في §0.0 — والتكرار هو التصادم نفسه.
    /// </summary>
    [Fact]
    public void AssignedNumbersAreContiguousAndUnique()
    {
        Register register = Parsed.Value;
        List<int> numbers = [.. register.Traps.Where(static t => t.Number is not null).Select(static t => t.Number!.Value).Order()];

        List<string> problems = [];

        problems.AddRange(numbers
            .GroupBy(static n => n)
            .Where(static g => g.Count() > 1)
            .Select(static g => FormattableString.Invariant($"الرقم {g.Key} مُخصَّص لأكثر من فخّ — هذا هو التصادم بعينه")));

        for (int expected = 1; expected <= numbers.Count; expected++)
        {
            if (!numbers.Contains(expected))
            {
                problems.Add(FormattableString.Invariant($"فجوة في الترقيم عند {expected}"));
            }
        }

        Assert.True(
            problems.Count == 0,
            "ترقيم سجل المصائد غير متّصل (traps.md §0.0 — الأرقام تُخصَّص عند الدمج بالتسلسل):\n"
            + string.Join('\n', problems));
    }

    /// <summary>
    /// صفوف الفهرس والمراسي والعناوين ثلاث مجموعات متطابقة. اختلافها هو ما يُنتج فخّاً
    /// موجوداً في الفهرس بلا قسم، أو قسماً لا يصل إليه رابط.
    /// </summary>
    [Fact]
    public void IndexRowsAnchorsAndHeadingsAreTheSameSet()
    {
        Register register = Parsed.Value;
        List<string> problems = [];

        HashSet<string> headingKeys = [.. register.Traps.Select(static t => t.Key)];
        HashSet<string> indexKeys = [.. register.IndexKeys];

        foreach (string missing in headingKeys.Except(indexKeys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            problems.Add($"قسم بلا صفّ فهرس: {missing}");
        }

        foreach (string extra in indexKeys.Except(headingKeys, StringComparer.Ordinal).Order(StringComparer.Ordinal))
        {
            problems.Add($"صفّ فهرس بلا قسم: {extra}");
        }

        foreach (Trap trap in register.Traps)
        {
            string[] expected = trap.Number is null
                ? [trap.Slug]
                : [trap.Slug, trap.Number.Value.ToString("00", CultureInfo.InvariantCulture)];

            if (!trap.Anchors.OrderBy(static a => a, StringComparer.Ordinal)
                    .SequenceEqual(expected.OrderBy(static a => a, StringComparer.Ordinal), StringComparer.Ordinal))
            {
                problems.Add(
                    $"{trap.Display}: المراسي [{string.Join(", ", trap.Anchors)}] لا تطابق المتوقّع "
                    + $"[{string.Join(", ", expected)}] — مرساة المفتاح إلزامية، ومرساة الرقم إلزامية بعد تخصيصه");
            }
        }

        foreach ((string key, string target) in register.IndexTargets)
        {
            Trap? trap = register.Traps.FirstOrDefault(t => string.Equals(t.Key, key, StringComparison.Ordinal));
            if (trap is null)
            {
                continue;
            }

            string expectedTarget = trap.Number is null
                ? trap.Slug
                : trap.Number.Value.ToString("00", CultureInfo.InvariantCulture);

            if (!string.Equals(target, expectedTarget, StringComparison.Ordinal))
            {
                problems.Add($"{trap.Display}: صفّ الفهرس يشير إلى #fakh-{target} والمتوقّع #fakh-{expectedTarget}");
            }
        }

        Assert.True(problems.Count == 0, "سجل المصائد غير متسق بين فهرسه ومراسيه وعناوينه:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// الحصيلة المعلنة في صدر الفهرس تساوي العدّ الفعلي لأعمدة «يفشل». رقمٌ يُكتب بيد
    /// ولا يُشتقّ من البيانات ينحرف عند أول إضافة — وقد انحرف فعلاً في هذا الدمج.
    /// </summary>
    [Fact]
    public void TheStatedTallyEqualsTheActualCount()
    {
        Register register = Parsed.Value;
        Tally actual = register.ActualTally;
        Tally stated = register.StatedTally;

        Assert.True(
            actual == stated,
            "الحصيلة المعلنة لا تطابق العدّ الفعلي لصفوف الفهرس.\n"
            + FormattableString.Invariant($"المعلن : المجموع {stated.Total} · بصمت {stated.Silent} (منها {stated.Delayed} مؤجّل) · بصوت {stated.Loud} · تتنكّر {stated.Disguised}\n")
            + FormattableString.Invariant($"الفعلي : المجموع {actual.Total} · بصمت {actual.Silent} (منها {actual.Delayed} مؤجّل) · بصوت {actual.Loud} · تتنكّر {actual.Disguised}"));

        Assert.Equal(register.Traps.Count, actual.Total);
    }

    /// <summary>
    /// كل وثيقة تُعيد ذكر مجموع المصائد تُوافق السجل. مجموع مُعاد في أربع وثائق ينحرف في
    /// إحداها — وهو نفس العطل الذي يحرسه §0.1 في وسوم الإثبات.
    /// </summary>
    [Fact]
    public void EveryDocumentThatRepeatsTheTotalAgreesWithTheRegister()
    {
        Register register = Parsed.Value;
        int total = register.Traps.Count;
        int silent = register.ActualTally.Silent;

        (string Path, string Pattern, int Expected)[] restatements =
        [
            ("docs/evidence/README.md", @"\*\*الأعطال\.\*\* ‏([0-9]+) فخّاً", total),
            ("docs/evidence/README.md", @"\*\*([0-9]+) منها تفشل بصمت\*\*", silent),
            ("docs/evidence/measurements.md", @"\*\*الأعطال\*\* \(‏([0-9]+) فخّاً", total),
            ("docs/RECORD.md", @"الأعطال \(([0-9]+) فخّاً", total),
            ("docs/decisions/README.md", @"\*\*‏([0-9]+) فخّاً\*\*", total),
        ];

        List<string> problems = [];

        foreach ((string path, string pattern, int expected) in restatements)
        {
            string full = Path.Combine(RepositoryLayout.Root, path);
            if (!File.Exists(full))
            {
                problems.Add($"{path}: الملف غير موجود، والمجموع مُعاد فيه");
                continue;
            }

            Match match = Regex.Match(File.ReadAllText(full), pattern, RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5));
            if (!match.Success)
            {
                problems.Add($"{path}: لم يُعثر على المجموع المُعاد بالنمط {pattern} — لا تُحوَّل الأرقام إلى كلمات، فالحارس يقرأ الأرقام");
                continue;
            }

            int found = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            if (found != expected)
            {
                problems.Add(FormattableString.Invariant($"{path}: مكتوب {found} والصحيح {expected}"));
            }
        }

        Assert.True(problems.Count == 0, "وثيقة تُعيد مجموع المصائد وتخالف السجل:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// كل إشارة إلى فخّ في <b>أي</b> ملف بالمستودع تصل إلى فخّ موجود — بالأرقام اللاتينية،
    /// وبالأرقام العربية-الهندية، وبالروابط العميقة <c>#fakh-…</c>.
    /// <para>
    /// هذا هو الفحص الذي كان غيابه يجعل الدمج «ينتهي» وهو معطوب: مرجعٌ عربي-هندي في وثيقة
    /// تصميم لا يراه <c>grep</c> بأرقام لاتينية، ولا يصرخ عنه شيء.
    /// </para>
    /// </summary>
    [Fact]
    public void EveryTrapReferenceInTheRepositoryResolves()
    {
        Register register = Parsed.Value;
        HashSet<int> numbers = [.. register.Traps.Where(static t => t.Number is not null).Select(static t => t.Number!.Value)];
        HashSet<string> slugs = [.. register.Traps.Select(static t => t.Slug)];

        List<string> dangling = [];
        int referencesSeen = 0;
        int filesScanned = 0;

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
                    dangling.Add(FormattableString.Invariant($"{relative}: «فخ-{n}» لا يقابل فخّاً في السجل"));
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
                        dangling.Add(FormattableString.Invariant($"{relative}: «{m.Value}» (أي {n}) لا يقابل فخّاً في السجل"));
                    }
                }
            }

            foreach (Match m in AnchorReference().Matches(text))
            {
                string token = m.Groups[1].Value;
                referencesSeen++;

                if (token.All(char.IsAsciiDigit))
                {
                    if (!numbers.Contains(int.Parse(token, CultureInfo.InvariantCulture)))
                    {
                        dangling.Add($"{relative}: الرابط «fakh-{token}» لا يقابل رقماً في السجل");
                    }
                }
                else if (!slugs.Contains(token))
                {
                    dangling.Add($"{relative}: الرابط «fakh-{token}» لا يقابل مفتاحاً في السجل");
                }
            }
        }

        Assert.True(dangling.Count == 0, "إشارات إلى مصائد غير موجودة:\n" + string.Join('\n', dangling.Distinct(StringComparer.Ordinal)));

        // حارس اللافراغ: مسحٌ لا يقرأ شيئاً يمرّ دائماً — وهو بالضبط عطل فخ-43.
        Assert.True(filesScanned >= 100, FormattableString.Invariant($"المسح قرأ {filesScanned} ملفاً فقط — النطاق ضامر"));
        Assert.True(referencesSeen >= 40, FormattableString.Invariant($"المسح وجد {referencesSeen} إشارة فقط — الأنماط لم تعد تطابق"));
    }

    /// <summary>
    /// نصّ القاعدة نفسه موجود وفي صدر الوثيقة، قبل الفهرس. قاعدةٌ يقرأها من يضيف الفخّ
    /// التالي **بعد** أن أضافه ليست قاعدة.
    /// </summary>
    [Fact]
    public void TheAllocationRuleIsDocumentedAtTheTopOfTheRegister()
    {
        string text = File.ReadAllText(Path.Combine(RepositoryLayout.Root, RegisterPath));
        int rule = text.IndexOf("قاعدة تخصيص المعرّفات", StringComparison.Ordinal);
        int index = text.IndexOf("## 1 · الفهرس", StringComparison.Ordinal);

        Assert.True(rule >= 0, "قاعدة تخصيص المعرّفات مفقودة من traps.md — بدونها يعود التصادم في أول فرعين متوازيين");
        Assert.True(index > rule, "قاعدة تخصيص المعرّفات يجب أن تسبق الفهرس: تُقرأ قبل إضافة الفخّ لا بعده");
        Assert.Contains("فخ-جديد", text, StringComparison.Ordinal);
    }

    /// <summary>
    /// حارس لافراغ الأنماط نفسها: يُثبت أن الصيغ الثلاث تُلتقَط فعلاً، وأن
    /// <c>فخ-9f3a1c</c> لا يُقرأ رقماً. نمطٌ توقّف عن المطابقة يجعل كل ما فوقه يمرّ فارغاً.
    /// </summary>
    [Fact]
    public void TheReferencePatternsAreNotVacuous()
    {
        Assert.Equal("18", LatinDigitReference().Match("انظر فخ-18 هنا").Groups[1].Value);
        Assert.Equal("١٨", ArabicIndicReference().Match("انظر فخ-١٨ هنا").Groups[1].Value);
        Assert.Equal("۱۸", EasternArabicReference().Match("انظر فخ-۱۸ هنا").Groups[1].Value);
        Assert.Equal(18, FromNonLatinDigits("١٨"));
        Assert.Equal(18, FromNonLatinDigits("۱۸"));
        Assert.Equal("fakh-18", AnchorReference().Match("traps.md#fakh-18").Value);
        Assert.Equal("fakh-nfc-versus-nfd", AnchorReference().Match("traps.md#fakh-nfc-versus-nfd").Value);
        Assert.DoesNotMatch(LatinDigitReference(), "فخ-9f3a1c");

        // الصيغ الثلاث لا تتداخل: نمط الأرقام اللاتينية لا يبتلع العربية-الهندية.
        Assert.DoesNotMatch(LatinDigitReference(), "انظر فخ-١٨ هنا");
        Assert.DoesNotMatch(ArabicIndicReference(), "انظر فخ-18 هنا");
        Assert.True(Parsed.Value.Traps.Count >= MinimumTrapCount);
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
                // هي القرص لا المستودع **تقيس البيئة لا الشيفرة**. و«dist» تحديداً يزرع
                // فيها Rule14 شاهداً موجباً ثم يحذفه، فيتسابق المسحان على ملفّ يختفي
                // بينهما — ويسقط هذا الحارس بـFileNotFoundException لسبب لا علاقة له بما
                // يحرسه، وذلك أسوأ من غيابه (فخ-65).
                //
                // والسطر نفسه موجود في AdrRegisterIsSelfConsistent منذ 4969303: حارسان
                // بالشكل نفسه، أُصلح أحدهما وبقي الآخر — وقد ظهر هنا حين أزاح تسليمٌ
                // آخر توقيتَ التشغيل بضعة ملّي ثانية.
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

    private sealed record Trap(int? Number, string Slug, string Title, IReadOnlyList<string> Anchors)
    {
        /// <summary>مفتاح المطابقة بين الفهرس والقسم: الرقم إن وُجد، وإلا المفتاح النصّي.</summary>
        public string Key => Number is null ? Slug : Number.Value.ToString("00", CultureInfo.InvariantCulture);

        public string Display => Number is null
            ? $"فخ-جديد · {Slug}"
            : FormattableString.Invariant($"فخ-{Number.Value:00} · {Slug}");
    }

    private readonly record struct Tally(int Total, int Silent, int Delayed, int Loud, int Disguised);

    private sealed record Register(
        IReadOnlyList<Trap> Traps,
        IReadOnlyList<string> IndexKeys,
        IReadOnlyList<(string Key, string Target)> IndexTargets,
        Tally ActualTally,
        Tally StatedTally)
    {
        private static readonly string[] DelayedWords =
            ["صفر", "واحد", "اثنان", "ثلاثة", "أربعة", "خمسة", "ستة", "سبعة", "ثمانية", "تسعة", "عشرة"];

        public static Register Load()
        {
            string path = Path.Combine(RepositoryLayout.Root, RegisterPath);
            string[] lines = File.ReadAllLines(path);

            List<Trap> traps = [];
            List<string> pendingAnchors = [];

            List<string> indexKeys = [];
            List<(string, string)> indexTargets = [];
            int silent = 0, delayed = 0, loud = 0, disguised = 0;

            bool insideFence = false;

            foreach (string line in lines)
            {
                // كتل الشيفرة تُتخطّى: §0.0 يعرض **مثالاً** لشكل الفخّ الجديد، وقارئٌ ساذج
                // يقرأ المثال فخّاً حقيقياً. حارسٌ يخلط التوثيق بالبيانات يُبلّغ عن عدد خاطئ.
                if (line.StartsWith("```", StringComparison.Ordinal))
                {
                    insideFence = !insideFence;
                    continue;
                }

                if (insideFence)
                {
                    continue;
                }

                Match anchor = AnchorPattern().Match(line);
                if (anchor.Success)
                {
                    pendingAnchors.Add(anchor.Groups["token"].Value);
                    continue;
                }

                Match heading = HeadingPattern().Match(line);
                if (heading.Success)
                {
                    string id = heading.Groups["id"].Value;
                    traps.Add(new Trap(
                        string.Equals(id, "جديد", StringComparison.Ordinal) ? null : int.Parse(id, CultureInfo.InvariantCulture),
                        heading.Groups["slug"].Value,
                        heading.Groups["title"].Value.Trim(),
                        [.. pendingAnchors]));
                    pendingAnchors.Clear();
                    continue;
                }

                Match row = IndexRowPattern().Match(line);
                if (row.Success)
                {
                    string id = row.Groups["id"].Value;
                    string[] cells = [.. line.Trim().Trim('|').Split('|').Select(static c => c.Trim())];
                    string mode = cells.Length > 3 ? cells[3] : string.Empty;

                    bool isSilent = mode.Contains("بصمت", StringComparison.Ordinal);
                    bool isLoud = mode.Contains("بصوت", StringComparison.Ordinal);

                    if (isSilent && !isLoud)
                    {
                        silent++;
                        if (!string.Equals(mode, "**بصمت**", StringComparison.Ordinal))
                        {
                            delayed++;
                        }
                    }
                    else if (isLoud && !isSilent)
                    {
                        loud++;
                    }
                    else
                    {
                        disguised++;
                    }

                    string key = string.Equals(id, "جديد", StringComparison.Ordinal) ? row.Groups["target"].Value : id;
                    indexKeys.Add(key);
                    indexTargets.Add((key, row.Groups["target"].Value));
                    continue;
                }

                // المراسي لا تُعبَر إلى قسم آخر: أي سطر غير فارغ بينهما يُبطلها.
                if (line.Trim().Length > 0 && pendingAnchors.Count > 0)
                {
                    pendingAnchors.Clear();
                }
            }

            Tally actual = new(indexKeys.Count, silent, delayed, loud, disguised);
            Tally stated = ReadStatedTally(File.ReadAllText(path));

            return new Register(traps, indexKeys, [.. indexTargets], actual, stated);
        }

        private static Tally ReadStatedTally(string text)
        {
            Match m = TallyPattern().Match(text);
            if (!m.Success)
            {
                throw new InvalidOperationException(
                    "لم يُعثر على جملة الحصيلة في traps.md بالصيغة المتوقّعة. "
                    + "الحصيلة ليست زينة: هي ما يُقتبَس في العروض والتقارير، فتُفحَص آلياً.");
            }

            string delayedWord = m.Groups["delayed"].Value;
            int delayed = Array.FindIndex(DelayedWords, w => string.Equals(w, delayedWord, StringComparison.Ordinal));
            if (delayed < 0)
            {
                throw new InvalidOperationException($"عدد «بصمت مؤجّل» مكتوب بكلمة غير معروفة: «{delayedWord}»");
            }

            return new Tally(
                int.Parse(m.Groups["total"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["silent"].Value, CultureInfo.InvariantCulture),
                delayed,
                int.Parse(m.Groups["loud"].Value, CultureInfo.InvariantCulture),
                int.Parse(m.Groups["disguised"].Value, CultureInfo.InvariantCulture));
        }
    }
}
