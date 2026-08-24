using System.Xml.Linq;
using Babel.ArchitectureTests.Support;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 9 — الحل يطابق خريطة الوحدات.</b>
/// <para>
/// وحدة موجودة في <see cref="BabelModule"/> بلا مشروع، أو مشروع بلا اختبارات، أو مشروع
/// خارج ملف الحل: كلها انحرافات صامتة تتراكم حتى تصير الخريطة وثيقة تاريخية.
/// هذه القاعدة تجعل «أضِف وحدة» عملية مكتملة أو فاشلة، لا شيء بينهما.
/// </para>
/// <para>
/// <b>وعد هذه القاعدة الآن يطابق سلوكها.</b> كانت <c>EveryProjectOnDiskIsInTheSolution</c> تقرأ
/// <c>RepositoryLayout.Projects</c>، وكان نطاقه قائمةً ثابتة من مجلدين: <c>{src, tests}</c>.
/// فمشروع تحت <c>tools/</c> أو <c>demo/</c> لم يكن «على القرص» بنظرها، وكانت تمرّ خضراء
/// على ثلاثة مشاريع لا يبنيها أي شيء — أسوأ من غياب القاعدة، لأنها توحي بتغطية فتُوقف البحث.
/// الآن يُكتشف كل <c>*.csproj</c> بالبحث، والإعفاء الوحيد <c>spikes/</c> مكتوب بالاسم ومُثبَت
/// بأنه ما زال وحيداً.
/// </para>
/// </summary>
public sealed class Rule09_TheSolutionMatchesTheModuleMap
{
    [Fact]
    public void EveryDeclaredModuleHasAProjectATestProjectAndAModuleInfo()
    {
        List<string> violations = [];

        foreach (BabelModule module in Enum.GetValues<BabelModule>())
        {
            string project = ModuleMap.ProjectOf(module);

            if (!Directory.Exists(Path.Combine(RepositoryLayout.Root, "src", project)))
            {
                violations.Add($"{module}: لا مشروع src/{project}");
            }

            if (!Directory.Exists(Path.Combine(RepositoryLayout.Root, "tests", project + ".Tests")))
            {
                violations.Add($"{module}: لا مشروع tests/{project}.Tests");
            }

            bool hasModuleInfo = BabelAssemblies.Product
                .Where(assembly => assembly.GetName().Name == project)
                .SelectMany(BabelAssemblies.TypesOf)
                .Any(type => type.Name == module + "ModuleInfo");

            if (!hasModuleInfo)
            {
                violations.Add($"{module}: لا بطاقة {module}ModuleInfo");
            }
        }

        Assert.True(violations.Count == 0, "خريطة الوحدات والحل غير متطابقين:\n" + string.Join('\n', violations));
    }

    /// <summary>
    /// المجلد الوحيد المُعفى من ملف الحل، ومعه سببه. ‏<c>spikes/</c> تجارب لا منتج، وإحداها
    /// تستعمل <c>WolverineFx.RuntimeCompilation</c> التي تمنعها القاعدة 8 في المنتج — فلا يمكن
    /// أن تدخل الحل. الإعفاء **مكتوب هنا بالاسم** لا مستنتَجاً من نطاق ماسح، وأي مجلد آخر
    /// يظهر فيه مشروع خارج الحل يُفشِل البناء.
    /// </summary>
    private const string TheOnlyExemptFolder = "spikes/";

    [Fact]
    public void EveryProjectOnDiskIsInTheSolution()
    {
        string solutionPath = Path.Combine(RepositoryLayout.Root, "Babel.slnx");
        Assert.True(File.Exists(solutionPath), "ملف الحل Babel.slnx غير موجود.");

        // المقارنة بالمسار النسبي كاملاً لا بالاسم وحده: مشروعان بالاسم نفسه في مجلدين
        // مختلفين يجعلان المطابقة بالاسم تمرّ على أحدهما وهو خارج الحل.
        string solution = File.ReadAllText(solutionPath).Replace('\\', '/');

        // البحث على القرص لا في قائمة مجلدات: مشروع تحت مجلد جديد يدخل النطاق تلقائياً.
        List<string> missing = [.. RepositoryLayout.AllProjectFilesOnDisk
            .Where(static path => !path.StartsWith(TheOnlyExemptFolder, StringComparison.Ordinal))
            .Where(path => !solution.Contains($"\"{path}\"", StringComparison.Ordinal))];

        Assert.True(
            missing.Count == 0,
            "مشاريع على القرص وخارج Babel.slnx — لا يبنيها شيء، فمحلّلاتها واختباراتها وأخطاء "
            + $"ترجمتها غير مرئية والتكامل المستمر أخضر (traps.md — فخ-41):\n{string.Join('\n', missing)}");
    }

    [Fact]
    public void EveryProjectInTheSolutionExistsOnDisk()
    {
        // الاتجاه المعاكس: مسار في ملف الحل لا يقابله ملف يُفشِل الاستعادة عند أول من يسحب الفرع.
        List<string> dangling = [.. XDocument
            .Load(Path.Combine(RepositoryLayout.Root, "Babel.slnx"))
            .Descendants("Project")
            .Select(static element => (string?)element.Attribute("Path") ?? string.Empty)
            .Select(static path => path.Replace('\\', '/'))
            .Where(static path => path.Length > 0)
            .Where(static path => !File.Exists(Path.Combine(RepositoryLayout.Root, path)))];

        Assert.True(dangling.Count == 0, "مسارات في Babel.slnx بلا ملف على القرص:\n" + string.Join('\n', dangling));
    }

    [Fact]
    public void TheOnlyFolderOutsideTheSolutionIsSpikes()
    {
        // إعفاء غير مُقاس هو الباب الذي دخل منه التعفّن أول مرة. هذا الاختبار يُثبت أن الإعفاء
        // ما زال واحداً بالاسم، فلا يتسلّل مجلد ثانٍ خلف صيغة عامة.
        string solution = File.ReadAllText(Path.Combine(RepositoryLayout.Root, "Babel.slnx")).Replace('\\', '/');

        List<string> exemptFolders = [.. RepositoryLayout.AllProjectFilesOnDisk
            .Where(path => !solution.Contains($"\"{path}\"", StringComparison.Ordinal))
            .Select(static path => path[..(path.IndexOf('/', StringComparison.Ordinal) + 1)])
            .Distinct(StringComparer.Ordinal)
            .Order(StringComparer.Ordinal)];

        Assert.True(
            exemptFolders.All(folder => folder == TheOnlyExemptFolder),
            $"مجلدات خارج الحل غير المُعفى الوحيد ({TheOnlyExemptFolder}):\n" + string.Join('\n', exemptFolders));
    }

    [Fact]
    public void TheScanIsNotVacuous()
    {
        // لو مرّ الفحص على مجموعة فارغة أو ضامرة لما أثبت شيئاً — وهذا بالضبط ما كان يحدث:
        // النطاق القديم {src, tests} كان يقرأ 40 مشروعاً ويصمت عن أربعة تحت tools/ وdemo/.
        Assert.True(
            RepositoryLayout.AllProjectFilesOnDisk.Count >= 45,
            $"عدد ملفات المشاريع المكتشفة {RepositoryLayout.AllProjectFilesOnDisk.Count} أقل من المتوقّع.");

        Assert.Contains(RepositoryLayout.AllProjectFilesOnDisk, path => path.StartsWith("tools/", StringComparison.Ordinal));
        Assert.Contains(RepositoryLayout.AllProjectFilesOnDisk, path => path.StartsWith("demo/", StringComparison.Ordinal));
        Assert.Contains(RepositoryLayout.AllProjectFilesOnDisk, path => path.StartsWith(TheOnlyExemptFolder, StringComparison.Ordinal));
    }

    [Fact]
    public void EveryModuleInTheEntitlementGraphIsADeclaredModule()
    {
        foreach (BabelModule module in Enum.GetValues<BabelModule>())
        {
            Assert.All(
                Core.Entitlement.ModuleDependencyGraph.RequirementsOf(module),
                requirement => Assert.Contains(requirement, Enum.GetValues<BabelModule>()));
        }
    }

    [Fact]
    public void TheMandatoryModulesAreExactlyTheOnesTheProductCannotBeSoldWithout()
    {
        // النواة والدفتر دائماً؛ المبيعات والمشتريات مع الدفتر؛ الالتزام في السوق السعودي.
        Assert.Equal(
            [BabelModule.Core, BabelModule.Ledger, BabelModule.Sales, BabelModule.Purchasing, BabelModule.Compliance],
            Core.Entitlement.ModuleDependencyGraph.Mandatory);
    }
}
