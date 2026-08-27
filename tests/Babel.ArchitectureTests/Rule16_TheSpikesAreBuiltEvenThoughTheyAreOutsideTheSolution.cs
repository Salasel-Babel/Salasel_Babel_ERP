using System.Xml.Linq;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 16 — المسابر تبقى خارج ملف الحلّ، و<u>مع ذلك</u> يبنيها شيء.</b>
/// <para>
/// <b>الشقّ الثاني من القاعدة 9، لا نقضٌ لها.</b> تلك تُعفي <c>spikes/</c> من
/// <c>Babel.slnx</c> بالاسم وتُثبت أنّها الإعفاء الوحيد. وهذه تدفع ثمن ذلك الإعفاء:
/// مجلدٌ خارج ملف الحلّ <b>لا يبنيه أحد</b>، فيتعفّن بصمت. والقاعدة 15 لا تلتقطه لأنها
/// تحسب «ما لا يصله مرجع اختبار» <b>داخل ملف الحلّ</b>، والمسبار ليس فيه أصلاً.
/// </para>
/// <para>
/// <b>وقد وقع:</b> قِيس على <c>develop</c> أنّ ثلاثة من المسابر الأربعة <b>لا تستعيد</b>
/// أصلاً (<c>NU1008</c>: ‏<c>Directory.Build.props</c> يفرض الإدارة المركزية للحزم على
/// الشجرة كلّها بينما المسبار يثبّت إصداره في <c>PackageReference</c>)، وخلف جدار
/// الاستعادة <b>353 تشخيصاً متمايزاً</b> لم يرها أحد قط لأن المصرّف لم يبلغها.
/// ومنها خطأ ثقافةٍ حقيقي: <c>DateOnly.Parse</c> بلا ثقافة على تاريخ عملٍ قادم من
/// السلك يُلقي تحت <c>ar-SA</c> ويعود <b>بصمت</b> بتاريخ يبعد قروناً تحت <c>fa-IR</c>.
/// (‏<c>docs/evidence/traps.md#fakh-an-exemption-from-the-solution-guard-rots-unbuilt</c>)
/// </para>
/// <para>
/// <b>لماذا هذه القاعدة لا تبني بنفسها:</b> على شاكلة القاعدة 15 تماماً — تلك لا تُشغّل
/// <c>dotnet build</c> بل تُثبت أنّ <b>البوّابة</b> تُشغّله. البناء الفعلي خطوةٌ في
/// <c>tools/gate/run.sh</c> وفي <c>ci.yml</c>، وهي التي تحمرّ حين ينكسر مسبار؛ وهذه
/// القاعدة تمنع أن <b>تُحذف تلك الخطوة</b> أو أن يُضاف مسبارٌ خامس فلا يدخلها.
/// اختبارٌ يستدعي <c>dotnet build</c> أربع مرّات كان سيُضاعف مسح العزل بلا مقابل.
/// </para>
/// <para>
/// <b>ولماذا يبقى المسبار خارج ملف الحلّ:</b> ‏ADR-جديد-spikes-are-built-beside-the-solution-not-inside-it —
/// إدخالها يجرّ <c>Marten</c> و<c>WolverineFx.RuntimeCompilation</c> (وهي Roslyn في
/// الإنتاج، ممنوعة بالقاعدة 8) إلى شجرة استعادة المنتج، فيصير بناءُ المنتج معتمداً على
/// شيفرة تجريبية. الثمن مقبول؛ الثمن الآخر لم يكن.
/// </para>
/// </summary>
public sealed class Rule16_TheSpikesAreBuiltEvenThoughTheyAreOutsideTheSolution
{
    private const string SpikesFolder = "spikes/";
    private const string GateScript = "tools/gate/run.sh";
    private const string CiWorkflow = ".github/workflows/ci.yml";

    /// <summary>
    /// عددٌ أدنى معلوم وقت كتابة القاعدة. حارس لافراغٍ: لو انكسر الاكتشاف وعاد بمجموعة
    /// فارغة لمرّ كل ما تحته خضراءَ بلا معنى — وهو عطل القاعدة نفسها داخل حارسها.
    /// </summary>
    private const int SpikesKnownWhenThisRuleWasWritten = 4;

    /// <summary>كل مسبارٍ على القرص يبنيه نصّ البوّابة بالاسم. مسبارٌ يُضاف ولا يدخل هنا يتعفّن.</summary>
    [Fact]
    public void TheLocalGateBuildsEverySpikeOnDisk()
    {
        string gate = Read(GateScript);

        List<string> missing = [.. SpikeProjects().Where(path => !gate.Contains(path, StringComparison.Ordinal))];

        Assert.True(
            missing.Count == 0,
            $"مسابر على القرص لا يبنيها {GateScript} — ودليلٌ لا يُبنى توقّف عن كونه دليلاً "
            + "(traps.md#fakh-an-exemption-from-the-solution-guard-rots-unbuilt):\n"
            + string.Join('\n', missing));
    }

    /// <summary>ونظيرتها في التكامل المستمر، فالحارس لا يعتمد على أن أحداً شغّل النصّ المحلي.</summary>
    [Fact]
    public void ContinuousIntegrationBuildsEverySpikeOnDisk()
    {
        string workflow = Read(CiWorkflow);

        List<string> missing = [.. SpikeProjects().Where(path => !workflow.Contains(path, StringComparison.Ordinal))];

        Assert.True(
            missing.Count == 0,
            $"مسابر على القرص لا يبنيها {CiWorkflow}:\n" + string.Join('\n', missing));
    }

    /// <summary>
    /// والخطوة تسبق الاختبارات: مسبارٌ مكسور يُعرف قبل أن تُقرأ خُضرةُ مجموعةٍ لا علاقة لها به.
    /// الترتيب هو الحكم، كما في القاعدة 15.
    /// </summary>
    [Fact]
    public void TheGateBuildsTheSpikesBeforeItTests()
    {
        string script = string.Join('\n', Read(GateScript)
            .Split('\n')
            .Where(static line => !line.TrimStart().StartsWith('#')));

        int firstSpike = SpikeProjects()
            .Select(path => script.IndexOf(path, StringComparison.Ordinal))
            .Where(static index => index >= 0)
            .DefaultIfEmpty(-1)
            .Min();
        int test = script.IndexOf("dotnet test", StringComparison.Ordinal);

        Assert.True(firstSpike >= 0, $"{GateScript} لا يذكر أي مسبار خارج التعليقات.");
        Assert.True(test >= 0, $"{GateScript} لا يُشغّل اختباراً.");
        Assert.True(firstSpike < test, $"{GateScript}: بناء المسابر يجب أن يسبق أول `dotnet test`.");
    }

    /// <summary>
    /// ولا مسبار داخل <c>Babel.slnx</c>. القاعدة 9 تُثبت أنّ <c>spikes/</c> هي الإعفاء
    /// الوحيد من الحلّ؛ وهذه تُثبت الاتجاه المقابل — أنّ الإعفاء ما زال مُطبَّقاً فعلاً،
    /// فلا يدخل مسبارٌ الحلَّ فيجرّ معه Roslyn إلى شجرة استعادة المنتج (القاعدة 8).
    /// </summary>
    [Fact]
    public void NoSpikeIsAMemberOfTheSolution()
    {
        HashSet<string> inSolution = [.. XDocument
            .Load(Path.Combine(RepositoryLayout.Root, "Babel.slnx"))
            .Descendants("Project")
            .Select(static element => (string?)element.Attribute("Path") ?? string.Empty)
            .Select(static path => path.Replace('\\', '/'))
            .Where(static path => path.Length > 0)];

        List<string> intruders = [.. SpikeProjects().Where(inSolution.Contains)];

        Assert.True(
            intruders.Count == 0,
            "مسبار دخل Babel.slnx — فصار بناء المنتج معتمداً على شيفرة تجريبية، ومعها\n"
            + "‏Marten وWolverineFx.RuntimeCompilation في شجرة الاستعادة (القاعدة 8):\n"
            + string.Join('\n', intruders));
    }

    /// <summary>حارس اللافراغ: الاكتشاف يجد المسابر فعلاً، وإلا فكل ما فوقه خُضرةٌ فارغة.</summary>
    [Fact]
    public void TheComputationIsNotVacuous()
    {
        IReadOnlyList<string> spikes = SpikeProjects();

        Assert.True(
            spikes.Count >= SpikesKnownWhenThisRuleWasWritten,
            $"عدد المسابر المكتشفة {spikes.Count} أقل من {SpikesKnownWhenThisRuleWasWritten} — الاكتشاف انكسر، "
            + "والقاعدة كلّها تمرّ خضراء على لا شيء.");

        Assert.All(spikes, path => Assert.StartsWith(SpikesFolder, path, StringComparison.Ordinal));
    }

    // ── الحساب ──────────────────────────────────────────────────────────────

    /// <summary>
    /// كل <c>*.csproj</c> تحت <c>spikes/</c> — بالبحث على القرص لا بقائمة مكتوبة، فمسبارٌ
    /// جديد يدخل النطاق من تلقاء نفسه. نفس مصدر الاكتشاف الذي تستعمله القاعدة 9.
    /// </summary>
    private static IReadOnlyList<string> SpikeProjects() =>
        [.. RepositoryLayout.AllProjectFilesOnDisk
            .Where(static path => path.StartsWith(SpikesFolder, StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

    private static string Read(string relative)
    {
        string path = Path.Combine(RepositoryLayout.Root, relative);
        Assert.True(File.Exists(path), $"{relative} غير موجود — وهو جزء من البوّابة.");
        return File.ReadAllText(path);
    }
}
