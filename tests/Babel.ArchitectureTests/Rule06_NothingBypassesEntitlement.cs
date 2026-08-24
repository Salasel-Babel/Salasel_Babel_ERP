using System.Reflection;
using Babel.ArchitectureTests.Support;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 6 — لا شيء يتجاوز الاستحقاق.</b>
/// <para>
/// كل نقطة دخول عامة في خدمة تطبيق تحمل <see cref="RequiresEntitlementAttribute"/>.
/// الاختبار يعدّ نقاط الدخول ويُفشل البناء على أي واحدة بلا سمة.
/// </para>
/// <para>
/// لماذا عند حدّ الخدمة لا عند الواجهة: إخفاء عنصر من القائمة لا يمنع نداء HTTP.
/// وحدة انقضى اشتراكها تبقى مقروءة بالكامل — وهذا هو المطلوب — لكن مسار الكتابة
/// يجب أن يُغلق في مكان واحد يستحيل نسيانه، لا في كل شاشة.
/// </para>
/// <para>
/// ولماذا الآن لا لاحقاً: <c>ReadOnly</c> يمسّ كل مسار كتابة وكل تقرير في كل وحدة؛
/// إضافته بعد أول عميل يدفع تعني إعادة فتح كل ملف (وثيقة المعمارية §17 م-7).
/// </para>
/// </summary>
public sealed class Rule06_NothingBypassesEntitlement
{
    private static IEnumerable<Type> ApplicationServices() =>
        BabelAssemblies.AllTypes()
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(static type => type is { IsClass: true, IsAbstract: false })
            .Where(static type => typeof(IApplicationService).IsAssignableFrom(type))
            .Where(TypeShapes.IsVisibleOutsideAssembly);

    private static IEnumerable<MethodInfo> EntryPoints(Type service) =>
        service.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .Where(static method => !method.IsSpecialName)
            .Where(static method => method.DeclaringType != typeof(object));

    [Fact]
    public void EveryPublicEntryPointDeclaresItsEntitlementRequirement()
    {
        List<string> violations = [];
        int entryPoints = 0;

        foreach (Type service in ApplicationServices())
        {
            bool typeAttributed = service.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true) is not null;

            foreach (MethodInfo method in EntryPoints(service))
            {
                entryPoints++;

                if (!typeAttributed && method.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true) is null)
                {
                    violations.Add($"{service.FullName}.{method.Name}");
                }
            }
        }

        Assert.True(entryPoints > 0, "لم تُعثر أي نقطة دخول — القاعدة تمرّ فراغاً.");
        Assert.True(
            violations.Count == 0,
            "نقاط دخول عامة بلا [RequiresEntitlement] — أي بلا إنفاذ استحقاق:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void EveryEntryPointDeclaresTheModuleItActuallyLivesIn()
    {
        // سمة تعلن وحدة غير وحدتها تفتح ثغرة أدهى من غيابها: تبدو مؤمَّنة وليست كذلك.
        List<string> violations = [];

        foreach (Type service in ApplicationServices())
        {
            string assemblyName = service.Assembly.GetName().Name!;

            foreach (MethodInfo method in EntryPoints(service))
            {
                RequiresEntitlementAttribute? attribute =
                    method.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true)
                    ?? service.GetCustomAttribute<RequiresEntitlementAttribute>(inherit: true);

                if (attribute is null)
                {
                    continue;
                }

                string declared = ModuleMap.ProjectOf(attribute.Module);
                if (declared != assemblyName)
                {
                    violations.Add($"{service.FullName}.{method.Name} يعلن {attribute.Module} وهو في {assemblyName}");
                }
            }
        }

        Assert.True(violations.Count == 0, "سمات استحقاق تعلن وحدة غير وحدتها:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void TheEnforcementSeamExistsAndIsUsedByEveryModuleThatHasEntryPoints()
    {
        // السمة إعلان؛ ما يجعلها فعّالة هو استدعاء المنفِّذ. القاعدة تتحقق من وجود
        // اعتماد على IEntitlementEnforcer في كل تجميعة فيها خدمة تطبيق.
        List<string> violations = [];

        foreach (IGrouping<Assembly, Type> group in ApplicationServices().GroupBy(static service => service.Assembly))
        {
            bool takesEnforcer = group.Any(static service => service
                .GetConstructors()
                .SelectMany(static constructor => constructor.GetParameters())
                .Any(static parameter => parameter.ParameterType == typeof(IEntitlementEnforcer)));

            if (!takesEnforcer)
            {
                violations.Add(group.Key.GetName().Name!);
            }
        }

        Assert.True(
            violations.Count == 0,
            "تجميعات فيها خدمات تطبيق لا تحقن IEntitlementEnforcer إطلاقاً:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        string[] modulesWithServices = [.. ApplicationServices()
            .Select(static service => service.Assembly.GetName().Name!)
            .Distinct()
            .Order(StringComparer.Ordinal)];

        Assert.Equal(
            ["Babel.Compliance", ModuleMap.Core, "Babel.Inventory", ModuleMap.Ledger, "Babel.Purchasing", "Babel.Sales"],
            modulesWithServices);
    }
}
