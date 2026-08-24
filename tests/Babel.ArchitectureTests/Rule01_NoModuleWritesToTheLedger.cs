using System.Reflection;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Posting;
using NetArchTest.Rules;
using ArchTestResult = NetArchTest.Rules.TestResult;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 1 — لا وحدة تكتب في دفتر الأستاذ.</b> أهم ثابت في المشروع.
/// <para>
/// الترحيل عبر <see cref="IPostingService"/> حصراً (CONTRIBUTING §3 بند 1 · وثيقة المعمارية §13).
/// </para>
/// <para>
/// ثلاث طبقات، وكل واحدة وحدها قابلة للالتفاف:
/// <list type="number">
///   <item>لا مرجع مشروع من أي وحدة أفقية إلى <c>Babel.Ledger</c> — لا يوجد ما يُستدعى أصلاً.</item>
///   <item>أنواع استمرارية الدفتر <c>internal</c> — لا يراها حتى الجذر التركيبي.</item>
///   <item>صلاحيات PostgreSQL: <c>INSERT</c> + <c>SELECT</c> فقط للدور التطبيقي (وثيقة المعمارية §3.2،
///         مقيس برمز الرفض 42501). هذه الطبقة تُنفَّذ في موجة الهجرات وتُفحص هناك.</item>
/// </list>
/// </para>
/// </summary>
public sealed class Rule01_NoModuleWritesToTheLedger
{
    [Fact]
    public void NoProjectOtherThanTheCompositionRootReferencesTheLedger()
    {
        List<string> violations = [];

        foreach (ProjectFile project in RepositoryLayout.SourceProjects)
        {
            if (project.Name is ModuleMap.Ledger or ModuleMap.Api)
            {
                continue;
            }

            if (project.ProjectReferences.Contains(ModuleMap.Ledger, StringComparer.Ordinal))
            {
                violations.Add($"{project.RelativePath} يشير إلى {ModuleMap.Ledger}");
            }
        }

        Assert.True(
            violations.Count == 0,
            "لا وحدة تكتب في دفتر الأستاذ: الترحيل عبر IPostingService حصراً. مراجع مخالفة:\n"
            + string.Join('\n', violations));
    }

    [Fact]
    public void LedgerPersistenceTypesAreInvisibleOutsideTheLedger()
    {
        Assembly ledger = BabelAssemblies.Named(ModuleMap.Ledger);

        List<Type> persistence = [.. BabelAssemblies.TypesOf(ledger)
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(static type => type.Namespace?.StartsWith("Babel.Ledger.Persistence", StringComparison.Ordinal) == true)];

        Assert.NotEmpty(persistence);

        List<string> exposed = [.. persistence
            .Where(TypeShapes.IsVisibleOutsideAssembly)
            .Select(static type => type.FullName!)];

        Assert.True(
            exposed.Count == 0,
            "أنواع استمرارية الدفتر يجب أن تبقى internal:\n" + string.Join('\n', exposed));
    }

    [Fact]
    public void NothingOutsideTheLedgerDependsOnLedgerInternals()
    {
        List<string> violations = [];

        foreach (Assembly assembly in BabelAssemblies.Product)
        {
            string name = assembly.GetName().Name!;
            if (name == ModuleMap.Ledger)
            {
                continue;
            }

            ArchTestResult result = Types.InAssembly(assembly)
                .Should()
                .NotHaveDependencyOnAny("Babel.Ledger.Persistence", "Babel.Ledger.Accounts", "Babel.Ledger.PostingMatrix")
                .GetResult();

            if (!result.IsSuccessful)
            {
                violations.AddRange((result.FailingTypeNames ?? []).Select(typeName => $"{name}: {typeName}"));
            }
        }

        Assert.True(
            violations.Count == 0,
            "أنواع خارج الدفتر تعتمد على داخله:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void ThePostingServiceIsTheOnlyImplementationAndItLivesInTheLedger()
    {
        List<Type> implementations = [.. BabelAssemblies.AllTypes()
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(static type => type is { IsClass: true, IsAbstract: false } && typeof(IPostingService).IsAssignableFrom(type))];

        Type implementation = Assert.Single(implementations);
        Assert.Equal(ModuleMap.Ledger, implementation.Assembly.GetName().Name);
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        // حارس: لو صار عدد المشاريع صفراً أو اختفى الدفتر، تمرّ القواعد أعلاه فراغاً.
        Assert.Contains(RepositoryLayout.SourceProjects, project => project.Name == ModuleMap.Ledger);
        Assert.True(RepositoryLayout.SourceProjects.Count() >= 16);
        Assert.Contains(BabelAssemblies.Product, assembly => assembly.GetName().Name == ModuleMap.Ledger);
    }
}
