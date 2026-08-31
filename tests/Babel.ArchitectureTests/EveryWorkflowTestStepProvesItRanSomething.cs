using System.Globalization;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>خطوةُ اختبارٍ لا تشغّل اختباراً واحداً هي أخطر من خطوةٍ محذوفة.</b>
/// <para>
/// وقع، ومقيس: خطوة «اختبارات أداة التحقق» في <c>data-validation.yml</c> كانت تُمرّر
/// <c>--nologo</c>. و<c>global.json</c> يختار منصّة الاختبار
/// (<c>"test": { "runner": "Microsoft.Testing.Platform" }</c>)، وفي هذا الوضع <b>لا
/// يعرف</b> <c>dotnet test</c> هذا الخيار — وهو خيارٌ صالح تماماً لـ<c>dotnet build</c>
/// ولـ<c>dotnet test</c> في زمن VSTest — فيُمرَّر إلى مضيف الاختبار كخيار امتداد، فيرفضه
/// المضيف، ويطبع مساعدته، ويخرج <b>بلا اختبارٍ واحد</b>:
/// <c>total: 0 · failed: 0 · succeeded: 0</c> ورمز خروج 5، وأربعون اختباراً في التجميعة
/// لم يُشغَّل منها شيء.
/// </para>
/// <para>
/// <b>ولماذا اختبارٌ لا مراجعة:</b> السطر يقرأ سليماً بالعين — الخيار موجود في كل مثال
/// عن <c>dotnet build</c> في هذا المستودع نفسه — والفرق كلّه في مَن يشغّل الاختبارات،
/// وهو مكتوب في ملفٍّ آخر. ولا يظهر العطل إلا في سطرٍ من مخرَج تشغيلة حمراء أصلاً.
/// </para>
/// <para>
/// <b>والشقّ الثاني:</b> العتبة الضمنية في المنصّة اختبارٌ واحد. فمجموعةٌ ذبلت من أربعين
/// اختباراً إلى واحد تمرّ <b>خضراء</b>، ولا شيء يفرّق «مرّت» عن «لم يبقَ ما يُشغَّل».
/// فكل خطوة اختبارٍ مُوجَّهة إلى مشروعٍ بعينه تُعلن حدّها الأدنى بالرقم.
/// </para>
/// <para>
/// (‏<c>docs/evidence/traps.md#fakh-an-argument-valid-for-one-runner-tells-another-to-run-nothing</c>)
/// </para>
/// </summary>
public sealed class EveryWorkflowTestStepProvesItRanSomething
{
    private const string WorkflowFolder = ".github/workflows";
    private const string GlobalJson = "global.json";
    private const string DataValidationWorkflow = ".github/workflows/data-validation.yml";

    /// <summary>
    /// الحدّ الأدنى لعدد أسطر <c>dotnet test</c> المقروءة. مسحٌ عاد بلا سطر واحد يُرضي
    /// كل تأكيد تحته، فيصير هذا الملفّ نفسه حارساً لا يقرأ شيئاً — وهو عين ما يمنعه.
    /// </summary>
    private const int MinimumTestInvocations = 3;

    /// <summary>الحدّ المُعلَن لمجموعة أداة التحقق — مقيس: أربعون اختباراً.</summary>
    private const int ValidatorSuiteFloor = 40;

    /// <summary>
    /// خيارات لا تقبلها <c>dotnet test</c> تحت منصّة الاختبار: تُمرَّر إلى المضيف فيرفضها،
    /// فتصير الخطوة «تشتغل» ولا تشغّل شيئاً. كلّها صالحة في مكانٍ آخر — وهذا سبب خفائها.
    /// </summary>
    private static readonly string[] OptionsTheTestPlatformRejects =
    [
        "--nologo",
        "--logger",
        "--blame",
        "--collect",
        "--settings",
        "--test-adapter-path",
    ];

    private static IEnumerable<(string File, int Line, string Text)> TestInvocations()
    {
        string folder = Path.Combine(RepositoryLayout.Root, WorkflowFolder);
        Assert.True(Directory.Exists(folder), $"مجلد سير العمل غير موجود: {WorkflowFolder}");

        foreach (string path in Directory.EnumerateFiles(folder, "*.yml").OrderBy(static p => p, StringComparer.Ordinal))
        {
            string[] lines = File.ReadAllLines(path);
            for (int i = 0; i < lines.Length; i++)
            {
                string text = lines[i];
                if (text.Contains("dotnet test", StringComparison.Ordinal) && !text.TrimStart().StartsWith('#'))
                {
                    yield return (Path.GetRelativePath(RepositoryLayout.Root, path), i + 1, text);
                }
            }
        }
    }

    /// <summary>
    /// لا سطر <c>dotnet test</c> في أي سير عمل يحمل خياراً ترفضه منصّة الاختبار. والمسح
    /// يُثبت أنه قرأ قبل أن يُصدَّق صمتُه.
    /// </summary>
    [Fact]
    public void NoWorkflowPassesTheTestRunnerAnOptionItRejects()
    {
        List<(string File, int Line, string Text)> invocations = [.. TestInvocations()];

        Assert.True(
            invocations.Count >= MinimumTestInvocations,
            FormattableString.Invariant(
                $"المسح عاد بـ{invocations.Count} سطر «dotnet test» والحدّ الأدنى {MinimumTestInvocations} — النطاق ضامر، وخُضرته لا تعني شيئاً."));

        List<string> problems = [];
        foreach ((string file, int line, string text) in invocations)
        {
            foreach (string option in OptionsTheTestPlatformRejects)
            {
                if (Regex.IsMatch(text, @"(?<![\w-])" + Regex.Escape(option) + @"(?![\w-])", RegexOptions.CultureInvariant, TimeSpan.FromSeconds(5)))
                {
                    problems.Add(FormattableString.Invariant(
                        $"{file}:{line} — «{option}» لا تقبله dotnet test تحت منصّة الاختبار، فيُمرَّر إلى المضيف فيرفضه ولا يُشغَّل اختبارٌ واحد:{Environment.NewLine}    {text.Trim()}"));
                }
            }
        }

        Assert.True(
            problems.Count == 0,
            "خطوةُ اختبارٍ تُمرّر خياراً يجعلها لا تشغّل شيئاً:\n" + string.Join('\n', problems));
    }

    /// <summary>
    /// القائمة أعلاه صحيحة <b>لأن</b> <c>global.json</c> يختار منصّة الاختبار. تغيّر ذلك
    /// يُبطل مبرّر القائمة، فيجب أن يحمرّ هذا الملفّ بدل أن يبقى قاعدةً بلا سبب.
    /// </summary>
    [Fact]
    public void TheRuleAboveRestsOnGlobalJsonSelectingTheTestingPlatform()
    {
        string path = Path.Combine(RepositoryLayout.Root, GlobalJson);
        Assert.True(File.Exists(path), $"{GlobalJson} غير موجود");

        string text = File.ReadAllText(path);
        Assert.True(
            text.Contains("Microsoft.Testing.Platform", StringComparison.Ordinal),
            $"{GlobalJson} لم يعد يختار منصّة الاختبار — راجع قائمة الخيارات المرفوضة في هذا الملفّ قبل أن تُصدّقها.");
    }

    /// <summary>
    /// خطوة اختبارات أداة التحقق تُعلن حدّها الأدنى بالرقم: «صفر اختبار» و«أربعون اختباراً
    /// خضراء» يجب أن يكونا حالتين مختلفتين في المخرَج، لا حالةً واحدة.
    /// </summary>
    [Fact]
    public void TheValidatorTestStepDeclaresItsFloor()
    {
        string path = Path.Combine(RepositoryLayout.Root, DataValidationWorkflow);
        Assert.True(File.Exists(path), $"{DataValidationWorkflow} غير موجود");

        string text = File.ReadAllText(path);
        Match match = Regex.Match(
            text,
            @"dotnet test[^\r\n]*MatrixValidator\.Tests\.csproj[^\r\n]*--minimum-expected-tests\s+(?<floor>[0-9]+)",
            RegexOptions.CultureInvariant,
            TimeSpan.FromSeconds(5));

        Assert.True(
            match.Success,
            $"خطوة اختبارات أداة التحقق في {DataValidationWorkflow} لا تُعلن «--minimum-expected-tests» — فمجموعةٌ ذبلت إلى اختبارٍ واحد تمرّ خضراء.");

        int floor = int.Parse(match.Groups["floor"].Value, CultureInfo.InvariantCulture);
        Assert.True(
            floor >= ValidatorSuiteFloor,
            FormattableString.Invariant($"الحدّ المُعلَن {floor} وهو دون المقيس {ValidatorSuiteFloor} — الحدّ يُخفَّض بقرارٍ مكتوب لا بالسهو."));
    }
}
