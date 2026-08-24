using System.Reflection;
using Babel.ArchitectureTests.Support;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 3 — الاعتماد دائماً إلى الأسفل.</b>
/// <para>
/// <c>Babel.Core</c> و<c>Babel.Ledger</c> لا تعتمدان على أي وحدة أعلى منهما — أبداً.
/// والوحدات الأفقية لا تستدعي بعضها مباشرة، بل عبر <c>Babel.Contracts</c> أو الأحداث
/// (وثيقة المعمارية §13 — قواعد الحدود).
/// </para>
/// <para>
/// المرجع الممنوع يُمنع حتى وهو غير مستعمل: <see cref="RepositoryLayout"/> يشرح لماذا
/// لا يكفي فحص IL وحده.
/// </para>
/// </summary>
public sealed class Rule03_DependencyDirectionIsAlwaysDownward
{
    [Fact]
    public void EveryProjectReferenceIsDeclaredAllowed()
    {
        List<string> violations = [];

        foreach (ProjectFile project in RepositoryLayout.SourceProjects)
        {
            if (!ModuleMap.AllowedProjectReferences.TryGetValue(project.Name, out IReadOnlySet<string>? allowed))
            {
                violations.Add($"{project.RelativePath}: مشروع غير مذكور في ModuleMap — أضِفه بقرار معماري صريح.");
                continue;
            }

            foreach (string reference in project.ProjectReferences)
            {
                if (!allowed.Contains(reference))
                {
                    violations.Add($"{project.RelativePath} → {reference} (غير مسموح)");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "مراجع تخالف اتجاه الاعتماد المعلن في ModuleMap:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void CoreAndLedgerDoNotDependOnAnyModuleAboveThem()
    {
        string[] above = [.. ModuleMap.Horizontal, ModuleMap.Api];

        foreach (string lower in new[] { ModuleMap.SharedKernel, ModuleMap.Contracts, ModuleMap.Canonicalization, ModuleMap.Core, ModuleMap.Ledger })
        {
            Assembly assembly = BabelAssemblies.Named(lower);
            string[] forbidden = [.. above.Where(name => name != lower)];

            ArchTestResult result = Types.InAssembly(assembly).Should().NotHaveDependencyOnAny(forbidden).GetResult();

            Assert.True(
                result.IsSuccessful,
                $"{lower} يعتمد على وحدة أعلى منه: {string.Join(", ", result.FailingTypeNames ?? [])}");
        }
    }

    [Fact]
    public void HorizontalModulesDoNotDependOnEachOther()
    {
        List<string> violations = [];

        foreach (string module in ModuleMap.Horizontal)
        {
            Assembly assembly = BabelAssemblies.Named(module);
            string[] siblings = [.. ModuleMap.Horizontal.Where(other => other != module), ModuleMap.Ledger];

            ArchTestResult result = Types.InAssembly(assembly).Should().NotHaveDependencyOnAny(siblings).GetResult();

            if (!result.IsSuccessful)
            {
                violations.AddRange((result.FailingTypeNames ?? []).Select(typeName => $"{module}: {typeName}"));
            }
        }

        Assert.True(
            violations.Count == 0,
            "وحدات أفقية تستدعي بعضها مباشرة بدل العقود والأحداث:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void NothingDependsOnTheCompositionRoot()
    {
        List<string> violations = [.. RepositoryLayout.Projects
            .Where(static project => project.Name != ModuleMap.Api && project.Name != "Babel.ArchitectureTests")
            .Where(static project => project.ProjectReferences.Contains(ModuleMap.Api, StringComparer.Ordinal))
            .Select(static project => project.RelativePath)];

        Assert.True(
            violations.Count == 0,
            "مشاريع تعتمد على الجذر التركيبي:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void SharedKernelDependsOnNothing()
    {
        ProjectFile sharedKernel = RepositoryLayout.SourceProjects.Single(static p => p.Name == ModuleMap.SharedKernel);

        Assert.Empty(sharedKernel.ProjectReferences);
        Assert.Empty(sharedKernel.PackageReferences);
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        // لو مرّت القاعدة على مجموعة مراجع فارغة لما أثبتت شيئاً.
        int declaredReferences = RepositoryLayout.SourceProjects.Sum(static project => project.ProjectReferences.Count);
        Assert.True(declaredReferences >= 40, $"عدد المراجع المفحوصة {declaredReferences} أقل من المتوقّع.");
        Assert.Equal(ModuleMap.AllProjects.Count, RepositoryLayout.SourceProjects.Count());
    }
}
