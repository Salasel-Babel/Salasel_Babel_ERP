using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Subledger;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>منفذ نقطة الضبط عقدٌ واحد، ومحوّلُه لا يُنسخ.</b>
/// <para>
/// كان <c>IControlPointReader</c> ولقطتُه وحركتُه مكتوبةً <b>ثلاث مرّات</b> — في
/// <c>Babel.Sales.Subledger</c> و<c>Babel.Purchasing.Subledger</c>
/// و<c>Babel.Inventory.Subledger</c> — وتنفيذُها المطابق حرفاً بحرف <b>خمس مرّات</b>:
/// ثلاث تجهيزات اختبار وصنفان في أداة العرض. أي <b>ثمانية مواضع</b> لقاعدة واحدة.
/// </para>
/// <para>
/// وهذا هو شكل <c>docs/evidence/traps.md#fakh-81</c> بعينه، وقد أُغلق للاستحقاق
/// بـ<c>ADR-0036</c> بالطريقة نفسها: موضعٌ واحد، وحارسٌ يمنع الثاني. والعطل لا يقع
/// عند التأليف بل عند <b>الصيانة</b>: من يضيف حقلاً إلى اللقطة — تاريخ القيد مثلاً،
/// أو عملته — يُحرّر نسخةً ويترك اثنتين، فيقرأ دفتران مساعدان نقطةَ ضبطهما بتعريفين
/// مختلفين. ولا يُكتشف ذلك بانهيار: يُكتشف بمطابقةٍ تسقط على مستند سليم.
/// </para>
/// <para>
/// <b>وثلاثة فحوص لأن الالتفافات ثلاثة:</b> إعلانٌ ثانٍ باسمٍ آخر (يُمسَك بالشكل عبر
/// الانعكاس) · إعلانٌ ثانٍ بالاسم نفسه في تجميعة أخرى (يُمسَك بمسح المصدر) · ومحوّلٌ
/// يُنسَخ إلى كل مستهلك (يُمسَك بعدّ مواضع التنفيذ في كل سطح على حدة).
/// </para>
/// </summary>
public sealed class ControlPointPortIsDeclaredOnce
{
    /// <summary>التجميعة الوحيدة التي يجوز أن تُعلن العقد.</summary>
    private const string ContractsAssembly = "Babel.Contracts";

    /// <summary>المسار الوحيد الذي يجوز أن يُعلن فيه العقد.</summary>
    private const string ContractsPath = "src/Babel.Contracts/";

    /// <summary>أنواع العقد الثلاثة — كلٌّ منها يُعلَن مرّة واحدة لا أكثر.</summary>
    private static readonly string[] PortTypeNames =
    [
        nameof(IControlPointReader),
        nameof(ControlPointSnapshot),
        nameof(ControlPointMovement),
    ];

    /// <summary>
    /// إعلان نوعٍ في المصدر: الكلمة المفتاحية ثم الاسم، مع تجاهل الأنواع المتداخلة
    /// في التعليقات لأن سطر التعليق يُجرَّد قبل الفحص.
    /// </summary>
    private static readonly Regex Declaration = new(
        @"\b(?:interface|class|record|struct)\s+(?<name>[A-Za-z_][A-Za-z0-9_]*)\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>تنفيذ المنفذ: نوعٌ تُذكر في قائمة أسسه واجهةُ نقطة الضبط.</summary>
    private static readonly Regex Implementation = new(
        @"\b(?:class|record|struct)\s+[A-Za-z_][A-Za-z0-9_]*[^;{]*?:\s*[^;{]*?\bIControlPointReader\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>
    /// ‏١ · <b>بالشكل لا بالاسم:</b> واجهةٌ واحدة في المنتج كلّه تُعلن عضواً يُعيد لقطة
    /// نقطة ضبط — وهي في العقود.
    /// <para>
    /// فإعادة تسمية الواجهة لا تُفلت من هذا الفحص: نسخةٌ ثانية باسم آخر لا بدّ أن
    /// تُعيد اللقطة نفسها كي تكون نسخة.
    /// </para>
    /// </summary>
    [Fact]
    public void منفذ_نقطة_الضبط_واجهةٌ_واحدة_في_تجميعات_المنتج()
    {
        List<string> ports = [];
        int interfacesExamined = 0;

        foreach (Type type in BabelAssemblies.AllTypes().Where(static t => t.IsInterface))
        {
            interfacesExamined++;

            bool declaresSnapshotMember = type
                .GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
                .Any(static method => TypeShapes.Unwrap(method.ReturnType)
                    .Any(static returned => string.Equals(
                        returned.Name, nameof(ControlPointSnapshot), StringComparison.Ordinal)));

            if (declaresSnapshotMember)
            {
                ports.Add((type.Assembly.GetName().Name ?? "?") + " · " + type.FullName);
            }
        }

        // ولا يمرّ فراغاً: لو توقّف الماسح عن رؤية أي واجهة، أو لم يعد يجد المنفذ
        // نفسه، فالخُضرة لا تعني شيئاً (‏traps.md#fakh-68).
        Assert.True(
            interfacesExamined >= 20,
            FormattableString.Invariant(
                $"المسح ضامر: {interfacesExamined} واجهةً فقط في تجميعات المنتج — المجموعة ليست المستودع."));

        Assert.True(
            ports.Count >= 1,
            "لا واجهة تُعلن لقطة نقطة ضبط إطلاقاً — الماسح توقّف عن المطابقة، فالخُضرة "
            + "لا تعني شيئاً. (‏traps.md#fakh-68)");

        Assert.True(
            ports.Count == 1,
            "منفذ نقطة الضبط مُعلَن في أكثر من موضع — وقاعدةٌ في مواضع تُحرَّر في أحدها "
            + "وتُنسى في الباقي:\n" + string.Join('\n', ports));

        Assert.StartsWith(ContractsAssembly + " · ", ports[0], StringComparison.Ordinal);
    }

    /// <summary>
    /// ‏٢ · <b>وبالاسم كذلك، على المستودع كلّه:</b> كل نوعٍ من أنواع العقد الثلاثة
    /// مُعلَن <b>مرّة واحدة</b> في ما يتعقّبه git، وفي <c>src/Babel.Contracts/</c>.
    /// <para>
    /// والفحص الأول انعكاسي فلا يرى مشاريع الاختبار ولا أداة العرض؛ وهذا يراها.
    /// </para>
    /// </summary>
    [Fact]
    public void أنواع_العقد_مُعلَنة_مرّة_واحدة_في_المستودع_كلّه()
    {
        RepositoryScan scan = Scan();
        AssertTheSourceScanIsNotVacuous(scan);

        foreach (string name in PortTypeNames)
        {
            IReadOnlyList<string> sites = scan.Declarations[name];

            Assert.True(
                sites.Count == 1,
                FormattableString.Invariant(
                    $"النوع {name} مُعلَن في {sites.Count} موضعاً ولا يجوز إلا واحد:\n")
                + string.Join('\n', sites));

            Assert.True(
                sites[0].StartsWith(ContractsPath, StringComparison.Ordinal),
                name + " مُعلَن خارج العقود: " + sites[0]);
        }
    }

    /// <summary>
    /// ‏٣ · <b>والمحوّل لا يُنسَخ:</b> لكل سطحٍ من أسطح المستودع
    /// (<c>src</c> · <c>tests</c> · <c>demo</c> · <c>tools</c>) ملفٌّ واحد على الأكثر
    /// ينفّذ المنفذ.
    /// <para>
    /// وهذا هو نصف العطل الذي لا يمسكه الفحصان قبله: العقد كان يمكن أن يكون واحداً
    /// ومحوّله مكتوباً في كل مشروع اختبار. وقد كان كذلك فعلاً — ثلاثة ملفّات
    /// متطابقة بايتاً بايت عدا سطرَي تعليق — فصار ملفّاً واحداً تربطه المشاريع
    /// بـ<c>Compile Include</c>.
    /// </para>
    /// </summary>
    [Fact]
    public void محوّل_نقطة_الضبط_لا_يُنسَخ_في_كل_مستهلك()
    {
        RepositoryScan scan = Scan();
        AssertTheSourceScanIsNotVacuous(scan);

        Assert.True(
            scan.Implementations.Count >= 2,
            "لا موضع ينفّذ منفذ نقطة الضبط إطلاقاً — أو موضعٌ واحد فقط. وفي الحالتين "
            + "توقّف الماسح عن المطابقة أو ضمر المستودع، والخُضرة لا تعني شيئاً. "
            + "(‏traps.md#fakh-68)");

        Assert.Contains(
            scan.Implementations,
            static path => path.StartsWith("tests/", StringComparison.Ordinal));

        var perSurface = scan.Implementations
            .GroupBy(static path => path.Split('/', 2)[0], StringComparer.Ordinal)
            .Where(static group => group.Count() > 1)
            .Select(static group => group.Key + ": " + string.Join(" · ", group))
            .ToArray();

        Assert.True(
            perSurface.Length == 0,
            "محوّل نقطة الضبط منسوخ أكثر من مرّة في السطح الواحد — والنسخة تنحرف عن "
            + "أختها عند أول تعديل:\n" + string.Join('\n', perSurface));
    }

    // ────────────────────────────────────────────────────────────────────────

    private static void AssertTheSourceScanIsNotVacuous(RepositoryScan scan)
    {
        // المجموعة هي المستودع لا القرص، وضمورُها يُقرأ أحمر لا أخضر.
        Assert.True(
            scan.FilesRead >= 400,
            FormattableString.Invariant(
                $"المسح ضامر: {scan.FilesRead} ملفّ ‎.cs‎ فقط في ما يتعقّبه git — المجموعة ليست المستودع."));

        // والمُحلِّل نفسه يُثبت أنه ما زال يفهم شكل الإعلان: لو توقّف عن المطابقة
        // لصار كل عدد صفراً، وكل «مرّة واحدة» تصير «صفر مرّات» — أي أخضر صامت.
        Assert.True(
            scan.DeclarationsSeen >= 400,
            FormattableString.Invariant(
                $"المُحلِّل لم يجد إلا {scan.DeclarationsSeen} إعلان نوع في المستودع كلّه — كفّ عن فهم شكل الإعلان."));

        foreach (string name in PortTypeNames)
        {
            Assert.True(
                scan.Declarations[name].Count >= 1,
                "النوع " + name + " غير مُعلَن في أي موضع — الماسح يحرس اسماً لا وجود له. "
                + "(‏traps.md#fakh-68)");
        }
    }

    private static RepositoryScan Scan()
    {
        Dictionary<string, List<string>> declarations = PortTypeNames
            .ToDictionary(static name => name, static _ => new List<string>(), StringComparer.Ordinal);

        List<string> implementations = [];
        int filesRead = 0;
        int declarationsSeen = 0;

        foreach (string tracked in TrackedFiles())
        {
            // فواصل المسار تُطبَّع قبل أي فحص نمطي (‏traps.md#fakh-68 · فخ-69).
            string path = tracked.Replace('\\', '/');
            if (!path.EndsWith(".cs", StringComparison.Ordinal))
            {
                continue;
            }

            filesRead++;

            StringBuilder code = new();
            foreach (string line in File.ReadAllLines(Path.Combine(RepositoryLayout.Root, tracked)))
            {
                code.Append(StripComment(line)).Append('\n');
            }

            string source = code.ToString();

            foreach (Match match in Declaration.Matches(source))
            {
                declarationsSeen++;
                string name = match.Groups["name"].Value;
                if (declarations.TryGetValue(name, out List<string>? sites))
                {
                    sites.Add(path);
                }
            }

            if (Implementation.IsMatch(source))
            {
                implementations.Add(path);
            }
        }

        return new RepositoryScan(
            declarations.ToDictionary(
                static entry => entry.Key,
                static entry => (IReadOnlyList<string>)entry.Value,
                StringComparer.Ordinal),
            implementations,
            filesRead,
            declarationsSeen);
    }

    /// <summary>الشيفرة بلا تعليق سطري: الحارس لا يحسب شرحاً يذكر الشكل الممنوع.</summary>
    private static string StripComment(string line)
    {
        int marker = line.IndexOf("//", StringComparison.Ordinal);
        return marker < 0 ? line : line[..marker];
    }

    /// <summary>
    /// ما يتعقّبه git في المستودع كلّه — لا ما يقع على القرص.
    /// </summary>
    /// <exception cref="InvalidOperationException">إن تعذّر سؤال git — والصمت هنا أسوأ من الرمي.</exception>
    private static string[] TrackedFiles()
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

    private sealed record RepositoryScan(
        IReadOnlyDictionary<string, IReadOnlyList<string>> Declarations,
        IReadOnlyList<string> Implementations,
        int FilesRead,
        int DeclarationsSeen);
}
