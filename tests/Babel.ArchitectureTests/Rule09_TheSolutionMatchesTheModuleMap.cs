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

    [Fact]
    public void EveryProjectOnDiskIsInTheSolution()
    {
        string solutionPath = Path.Combine(RepositoryLayout.Root, "Babel.slnx");
        Assert.True(File.Exists(solutionPath), "ملف الحل Babel.slnx غير موجود.");

        string solution = File.ReadAllText(solutionPath);
        List<string> missing = [.. RepositoryLayout.Projects
            .Where(project => !solution.Contains(project.Name + ".csproj", StringComparison.Ordinal))
            .Select(static project => project.RelativePath)];

        Assert.True(missing.Count == 0, "مشاريع خارج ملف الحل — لن يبنيها التكامل المستمر:\n" + string.Join('\n', missing));
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
