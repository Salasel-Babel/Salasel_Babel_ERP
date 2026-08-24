using System.Reflection;
using System.Runtime.CompilerServices;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 7 — النواة المشتركة والعقود بلا منطق أعمال.</b>
/// <para>
/// لا أنواع خدمة، ولا وصول لبيانات، ولا اعتماد على EF Core.
/// </para>
/// <para>
/// لماذا: هاتان التجميعتان يعتمد عليهما <b>كل شيء</b>. أي منطق يدخلهما يصبح فوراً
/// منطقاً لا يمكن تغييره دون تغيير الجميع، ولا اختباره دون تشغيل الجميع.
/// وأول ما يدخل عادةً هو «مجرد دالة مساعدة صغيرة» تقرأ من قاعدة البيانات.
/// </para>
/// <para>الواجهات مستثناة: <c>IPostingService</c> عقدٌ، لا تنفيذ.</para>
/// </summary>
public sealed class Rule07_SharedKernelAndContractsArePure
{
    private static readonly string[] PureAssemblies = [ModuleMap.SharedKernel, ModuleMap.Contracts];

    private static readonly string[] ForbiddenReferencePrefixes =
    [
        "Microsoft.EntityFrameworkCore",
        "Npgsql",
        "Wolverine",
        "Marten",
        "Dapper",
        "Microsoft.Extensions.DependencyInjection",
        "Microsoft.AspNetCore",
        "System.Data",
    ];

    private static readonly string[] ForbiddenTypeSuffixes =
    [
        "Service", "Repository", "Manager", "Store", "Engine", "Handler",
        "Provider", "Factory", "Context", "Controller", "Processor", "Validator",
    ];

    [Fact]
    public void NeitherAssemblyReferencesPersistenceOrMessagingOrHosting()
    {
        List<string> violations = [];

        foreach (string name in PureAssemblies)
        {
            Assembly assembly = BabelAssemblies.Named(name);

            violations.AddRange(assembly.GetReferencedAssemblies()
                .Select(static reference => reference.Name ?? string.Empty)
                .Where(static reference => reference.Length > 0)
                .Where(static reference => ForbiddenReferencePrefixes.Any(prefix => reference.StartsWith(prefix, StringComparison.Ordinal)))
                .Select(reference => $"{name} → {reference}"));
        }

        Assert.True(violations.Count == 0, "اعتماد ممنوع في النواة المشتركة أو العقود:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void NeitherProjectDeclaresAnyPackageReference()
    {
        // الفحص على ملف المشروع أيضاً: حزمة مضافة ولم تُستعمل بعد لا تظهر في مراجع التجميعة.
        foreach (string name in PureAssemblies)
        {
            ProjectFile project = RepositoryLayout.SourceProjects.Single(p => p.Name == name);
            Assert.True(
                project.PackageReferences.Count == 0,
                $"{project.RelativePath} يعلن حزماً: {string.Join(", ", project.PackageReferences)}");
        }
    }

    [Fact]
    public void NoConcreteServiceLikeTypeExists()
    {
        List<string> violations = [];

        foreach (string name in PureAssemblies)
        {
            violations.AddRange(BabelAssemblies.TypesOf(BabelAssemblies.Named(name))
                .Where(static type => !TypeShapes.IsCompilerGenerated(type))
                .Where(static type => type is { IsInterface: false, IsEnum: false })
                .Where(static type => ForbiddenTypeSuffixes.Any(suffix => type.Name.EndsWith(suffix, StringComparison.Ordinal)))
                .Select(static type => type.FullName!));
        }

        Assert.True(
            violations.Count == 0,
            "أنواع تشبه الخدمات في النواة المشتركة أو العقود — الواجهات وحدها مسموحة:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void EveryPublicPropertyIsImmutableAfterConstruction()
    {
        List<string> violations = [];

        foreach (string name in PureAssemblies)
        {
            foreach (Type type in BabelAssemblies.TypesOf(BabelAssemblies.Named(name)).Where(static t => !TypeShapes.IsCompilerGenerated(t)))
            {
                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
                {
                    MethodInfo? setter = property.SetMethod;
                    if (setter is null || !setter.IsPublic)
                    {
                        continue;
                    }

                    bool isInitOnly = setter.ReturnParameter
                        .GetRequiredCustomModifiers()
                        .Any(static modifier => modifier == typeof(IsExternalInit));

                    if (!isInitOnly)
                    {
                        violations.Add($"{type.FullName}.{property.Name}");
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "خصائص قابلة للتعديل بعد الإنشاء في أنواع القيمة والعقود:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void NoApplicationServiceLivesInEitherAssembly()
    {
        foreach (string name in PureAssemblies)
        {
            Assert.DoesNotContain(
                BabelAssemblies.TypesOf(BabelAssemblies.Named(name)),
                static type => typeof(Core.Application.IApplicationService).IsAssignableFrom(type));
        }
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        Assert.True(BabelAssemblies.TypesOf(BabelAssemblies.Named(ModuleMap.SharedKernel)).Count() >= 10);
        Assert.True(BabelAssemblies.TypesOf(BabelAssemblies.Named(ModuleMap.Contracts)).Count() >= 10);
    }
}
