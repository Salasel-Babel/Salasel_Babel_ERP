using System.Xml.Linq;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 15 — البوّابة تبني ما تدّعي تغطيته.</b>
/// <para>
/// <b>شقيقة القاعدة 9، لا تكرارٌ لها.</b> تلك تمنع مشروعاً من البقاء <b>خارج</b> ملف الحلّ
/// (فخ-43). وهذه تمنع الحالة التالية لها مباشرةً: المشروع <b>داخل</b> الحلّ، والقاعدة 9
/// خضراء عليه، والتكامل المستمر يبنيه — ومع ذلك <b>الأمر الذي يثق به الناس محلياً لا
/// يبنيه</b>. ‏<c>dotnet test --solution Babel.slnx</c> يبني مشاريع الاختبار ومراجعها
/// المتعدّية وحدها؛ ومشروعٌ لا يشير إليه أي مشروع اختبار لا يُترجَم في ذلك الأمر أصلاً،
/// فعطلُ ترجمةٍ فيه يعيش على فرعٍ تُعلن كل اختباراته أنه سليم.
/// </para>
/// <para>
/// <b>وقد وقع:</b> على <c>develop</c> عند <c>ed02df2</c> كان <c>demo/company/Verify.cs</c>
/// لا يُترجَم (<c>CS7036</c> مرّتين)، و<c>dotnet test --solution Babel.slnx -c Release</c>
/// يُعطي <b>871 · 0 فاشلاً</b> بينما <c>dotnet build Babel.slnx -c Release</c> يُعطي
/// <b>خطأين</b> — والمشروع المكسور هو الذي يقوم عليه العرض.
/// (‏<c>docs/evidence/traps.md#fakh-dotnet-test-does-not-build-what-no-test-references</c>)
/// </para>
/// <para>
/// <b>ولماذا اختبار لا تعليق:</b> الحارس ضدّ العودة ليس الفقرة في <c>CONTRIBUTING.md</c>
/// بل هذا الملف: يُفشل البناء إن اختفى البناء من البوّابة المحلية أو من التكامل المستمر،
/// وإن ظهر مشروع جديد لا يبنيه أي اختبار بلا أن يُكتب في القائمة أدناه بسببه.
/// <b>والقائمة ليست إعفاءً</b>: هي إعلانٌ بأن هذه المشاريع تعتمد على خطوة البناء وحدها،
/// فمن يحذف تلك الخطوة يعرف بالضبط ماذا أطفأ.
/// </para>
/// </summary>
public sealed class Rule15_TheGateBuildsWhatItClaimsToCover
{
    private const string GateScript = "tools/gate/run.sh";
    private const string Contributing = "CONTRIBUTING.md";
    private const string CiWorkflow = ".github/workflows/ci.yml";

    /// <summary>الجملة الوحيدة التي تعني «المستودع كلّه يُترجم».</summary>
    private const string SolutionBuild = "dotnet build Babel.slnx";

    /// <summary>
    /// المشاريع التي لا يشير إليها أي مشروع اختبار — <b>مقيسة على هذا الفرع</b>، ومعها
    /// سبب كلٍّ منها. ما دام مشروعٌ هنا فلا شيء يبنيه إلا خطوة البناء الصريحة.
    /// </summary>
    private static readonly (string Path, string Why)[] BuiltOnlyByTheExplicitBuild =
    [
        ("src/Babel.Compliance.Wolverine/Babel.Compliance.Wolverine.csproj",
            "طبقة ربط Wolverine — لا مجموعة اختبارات تشير إليها"),
        ("tests/Babel.ControlPlane.Proofs/Babel.ControlPlane.Proofs.csproj",
            "براهين لا تنتهي بـTests فليست مشروع اختبار بحكم Directory.Build.props"),
        ("demo/vertical-slice/BabelDemo.csproj",
            "تنفيذي عرضٍ لا يشير إليه شيء"),
        ("demo/company/BabelDemoCompany.csproj",
            "مُنشئ الشركة التجريبية — تنفيذي العرض نفسه، وهو الذي انكسر عند ed02df2"),
    ];

    /// <summary>
    /// المشاريع غير المبنيّة بـ<c>dotnet test</c> هي بالضبط المكتوبة أعلاه. مشروعٌ جديد
    /// يسقط في هذه الفئة بصمت هو الحالة التي كلّفت العرض بناءً مكسوراً.
    /// </summary>
    [Fact]
    public void ProjectsThatNoTestProjectReferencesAreExactlyTheDeclaredList()
    {
        IReadOnlyList<string> actual = ProjectsUnreachableFromTests();
        string[] declared = [.. BuiltOnlyByTheExplicitBuild.Select(static entry => entry.Path).Order(StringComparer.Ordinal)];

        List<string> problems = [];

        foreach (string appeared in actual.Except(declared, StringComparer.Ordinal))
        {
            problems.Add(
                $"مشروع جديد لا يبنيه `dotnet test --solution`: {appeared}\n"
                + "  → إمّا أن تشير إليه مجموعة اختبارات، وإمّا أن يُكتب في BuiltOnlyByTheExplicitBuild بسببه.");
        }

        foreach (string gone in declared.Except(actual, StringComparer.Ordinal))
        {
            problems.Add($"مشروع مكتوب في القائمة وصار يبنيه `dotnet test` (أو زال): {gone} — احذفه من القائمة.");
        }

        Assert.True(
            problems.Count == 0,
            "قائمة المشاريع التي لا يبنيها إلا خطوة البناء الصريحة لم تعد تطابق الواقع "
            + "(traps.md#fakh-dotnet-test-does-not-build-what-no-test-references):\n"
            + string.Join('\n', problems));
    }

    /// <summary>
    /// البوّابة المحلية تبني الحلّ كلّه، و<b>قبل</b> أن تُشغّل أي اختبار. الترتيب هو الحكم:
    /// بناءٌ بعد الاختبارات يجعل النتيجة الخضراء تُقرأ قبل أن يُعرف أن الشجرة تُترجم.
    /// </summary>
    [Fact]
    public void TheLocalGateBuildsTheWholeSolutionBeforeItTests()
    {
        // **الأوامر لا التعليقات.** رأس النصّ يشرح العطل ويقتبس `dotnet test --solution`
        // في شرحه، فقراءةٌ ساذجة تجد «الاختبار قبل البناء» في نصٍّ ترتيبه سليم.
        string script = string.Join('\n', Read(GateScript)
            .Split('\n')
            .Where(static line => !line.TrimStart().StartsWith('#')));

        int build = script.IndexOf(SolutionBuild, StringComparison.Ordinal);
        int test = script.IndexOf("dotnet test", StringComparison.Ordinal);

        Assert.True(build >= 0, $"{GateScript} لا يبني الحلّ. بوّابةٌ لا تبني تُعلن خُضرةً عن شجرة قد لا تُترجم.");
        Assert.True(test >= 0, $"{GateScript} لا يُشغّل اختباراً. بوّابةٌ تبني ولا تختبر ليست بوّابة.");
        Assert.True(build < test, $"{GateScript}: البناء يجب أن يسبق أول `dotnet test`، لا أن يليه.");
    }

    /// <summary>
    /// ونظيرتها في التكامل المستمر: خطوة بناء الحلّ موجودة، فالبوّابة الأخيرة لا تعتمد على
    /// أن أحداً شغّل النصّ المحلي.
    /// </summary>
    [Fact]
    public void ContinuousIntegrationBuildsTheWholeSolution()
    {
        Assert.Contains(
            SolutionBuild,
            Read(CiWorkflow),
            StringComparison.Ordinal);
    }

    /// <summary>
    /// وقائمة التحقّق المكتوبة تُحيل إلى البوّابة وتسمّي البناء. طقسُ قبولٍ موثّق لا يذكر
    /// البناء هو الطريق الذي دخل منه العطل أوّل مرّة.
    /// </summary>
    [Fact]
    public void TheDocumentedChecklistNamesTheGateAndTheBuild()
    {
        string text = Read(Contributing);

        Assert.Contains(GateScript, text, StringComparison.Ordinal);
        Assert.Contains(SolutionBuild, text, StringComparison.Ordinal);
    }

    /// <summary>
    /// حارس اللافراغ: لو قرأ الحساب مجموعةً فارغة — لأن اكتشاف المشاريع انكسر مثلاً —
    /// لمرّت الفحوص أعلاه خضراء بلا معنى، وهو عطل فخ-43 نفسه داخل حارسه.
    /// </summary>
    [Fact]
    public void TheComputationIsNotVacuous()
    {
        Assert.NotEmpty(BuiltOnlyByTheExplicitBuild);
        Assert.NotEmpty(ProjectsUnreachableFromTests());

        // مشاريع الاختبار نفسها لا يجوز أن تظهر في الفئة: هي التي يُشتقّ منها الوصول.
        Assert.True(
            TestProjectNames().Count >= 15,
            $"عدد مشاريع الاختبار المكتشفة {TestProjectNames().Count} أقل من المتوقّع — الاكتشاف انكسر.");

        // ومشروعٌ مرجعيّ نعرف أنه مبنيّ: Babel.Core تشير إليه كل مجموعة تقريباً.
        Assert.DoesNotContain(
            ProjectsUnreachableFromTests(),
            path => path.EndsWith("src/Babel.Core/Babel.Core.csproj", StringComparison.Ordinal));
    }

    // ── الحساب ──────────────────────────────────────────────────────────────

    /// <summary>
    /// مشروعٌ اختبارٍ هو ما ينتهي اسمه بـ<c>Tests</c> — وهو <b>نفس الشرط</b> الذي يُسند به
    /// <c>Directory.Build.props</c> الخاصية <c>IsTestProject</c>، لا تخميناً موازياً له.
    /// </summary>
    private static IReadOnlyList<string> TestProjectNames() =>
        [.. InTheSolution()
            .Select(static project => project.Name)
            .Where(static name => name.EndsWith("Tests", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    /// <summary>
    /// مشاريع الحلّ التي لا تصلها مراجع أي مشروع اختبار — أي التي لا يبنيها
    /// <c>dotnet test --solution</c>، مقيسةً بإغلاق مراجع المشاريع لا بقائمة مكتوبة.
    /// </summary>
    private static IReadOnlyList<string> ProjectsUnreachableFromTests()
    {
        Dictionary<string, ProjectFile> byName = InTheSolution()
            .ToDictionary(static project => project.Name, StringComparer.Ordinal);

        HashSet<string> reached = [];
        Stack<string> pending = new(TestProjectNames());

        while (pending.Count > 0)
        {
            string name = pending.Pop();

            if (!reached.Add(name) || !byName.TryGetValue(name, out ProjectFile? project))
            {
                continue;
            }

            foreach (string reference in project.ProjectReferences)
            {
                pending.Push(reference);
            }
        }

        return [.. byName.Values
            .Where(project => !reached.Contains(project.Name))
            .Select(static project => project.RelativePath)
            .Order(StringComparer.Ordinal)];
    }

    /// <summary>مشاريع المستودع التي يحملها <c>Babel.slnx</c> فعلاً — لا كل ما على القرص.</summary>
    private static IEnumerable<ProjectFile> InTheSolution()
    {
        HashSet<string> paths = [.. XDocument
            .Load(Path.Combine(RepositoryLayout.Root, "Babel.slnx"))
            .Descendants("Project")
            .Select(static element => (string?)element.Attribute("Path") ?? string.Empty)
            .Select(static path => path.Replace('\\', '/'))
            .Where(static path => path.Length > 0)];

        return RepositoryLayout.Projects.Where(project => paths.Contains(project.RelativePath));
    }

    private static string Read(string relative)
    {
        string path = Path.Combine(RepositoryLayout.Root, relative);
        Assert.True(File.Exists(path), $"{relative} غير موجود — وهو جزء من البوّابة.");
        return File.ReadAllText(path);
    }
}
