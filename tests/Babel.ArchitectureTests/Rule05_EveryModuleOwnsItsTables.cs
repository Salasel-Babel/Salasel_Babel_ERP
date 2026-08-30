using System.Reflection;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>القاعدة 5 — كل وحدة تملك جداولها.</b>
/// <para>
/// لا كيان EF يعبر حدّ وحدة، ولا <c>JOIN</c> عابر للوحدات؛ القراءة العابرة عبر واجهات معلنة
/// (وثيقة المعمارية §13).
/// </para>
/// <para>
/// لماذا تستحق قاعدة مفروضة: مفتاح خارجي واحد من <c>sales.invoice</c> إلى <c>inventory.item</c>
/// يبدو مريحاً يوم كتابته، ثم يصبح هو السبب في أن ترقية المخزون توقف المبيعات، وأن أرشفة
/// سنة مالية تفشل، وأن «القراءة عبر واجهة» تصير مستحيلة عملياً.
/// </para>
/// </summary>
public sealed class Rule05_EveryModuleOwnsItsTables
{
    private static IEnumerable<Type> PersistenceTypes(Assembly assembly)
    {
        string prefix = assembly.GetName().Name + ".Persistence";
        return BabelAssemblies.TypesOf(assembly)
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(type => type.Namespace?.StartsWith(prefix, StringComparison.Ordinal) == true);
    }

    [Fact]
    public void EveryDbContextIsInternalAndLivesInItsOwnModulePersistenceNamespace()
    {
        List<Type> contexts = [.. BabelAssemblies.AllTypes()
            .Where(static type => !TypeShapes.IsCompilerGenerated(type))
            .Where(TypeShapes.IsDbContext)];

        Assert.NotEmpty(contexts);

        List<string> violations = [];
        foreach (Type context in contexts)
        {
            string expected = context.Assembly.GetName().Name + ".Persistence";

            if (TypeShapes.IsVisibleOutsideAssembly(context))
            {
                violations.Add($"{context.FullName} معلن عاماً — أي وحدة أخرى تستطيع حقنه.");
            }

            if (context.Namespace != expected)
            {
                violations.Add($"{context.FullName} خارج {expected}");
            }
        }

        Assert.True(violations.Count == 0, "سياقات EF مخالفة:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void NoEntityTypeIsVisibleOutsideItsModule()
    {
        List<string> violations = [.. BabelAssemblies.Product
            .SelectMany(PersistenceTypes)
            .Where(TypeShapes.IsVisibleOutsideAssembly)
            .Select(static type => type.FullName!)];

        Assert.True(
            violations.Count == 0,
            "كيانات استمرارية مكشوفة خارج وحدتها — أول خطوة نحو JOIN عابر:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void NoEntityReferencesAnEntityOfAnotherModule()
    {
        HashSet<Type> allEntities = [.. BabelAssemblies.Product.SelectMany(PersistenceTypes)];
        Assert.NotEmpty(allEntities);

        List<string> violations = [];

        foreach (Type entity in allEntities)
        {
            string owner = entity.Assembly.GetName().Name!;

            foreach (MemberInfo member in TypeShapes.DeclaredMembers(entity))
            {
                foreach ((string description, Type valueType) in TypeShapes.ValueTypesOf(member))
                {
                    foreach (Type candidate in TypeShapes.Unwrap(valueType))
                    {
                        if (allEntities.Contains(candidate) && candidate.Assembly.GetName().Name != owner)
                        {
                            violations.Add($"{entity.FullName}.{description} → {candidate.FullName}");
                        }
                    }
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "مرجع كيان عابر للوحدات:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void NoDbSetMapsAnEntityFromAnotherModule()
    {
        List<string> violations = [];

        foreach (Type context in BabelAssemblies.AllTypes().Where(TypeShapes.IsDbContext))
        {
            string owner = context.Assembly.GetName().Name!;

            foreach (PropertyInfo property in context.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            {
                Type? element = TypeShapes.DbSetElement(property.PropertyType);
                if (element is not null && element.Assembly.GetName().Name != owner)
                {
                    violations.Add($"{context.FullName}.{property.Name} → {element.FullName}");
                }
            }
        }

        Assert.True(violations.Count == 0, "DbSet يربط كياناً من وحدة أخرى:\n" + string.Join('\n', violations));
    }

    [Fact]
    public void TheRuleIsNotVacuous()
    {
        // ستّ وحدات تملك جداول فعلاً بعد دفتر المخزون المساعد: الالتزام والنواة
        // والدفتر والمخزون والمشتريات والمبيعات. ومعها **مشروع مساند سابع**:
        // ‏`Babel.Storage`، محوّل المرفقات — يملك مخطّط `storage` وجدوليه.
        //
        // ‏**ولماذا ظهر الآن ولم يكن ظاهراً:** هذه القاعدة تقرأ التجميعات التي **تصل
        // إلى مُخرَج هذه المجموعة**، ووصولُها بمرجعٍ متعدٍّ. وكان `Babel.Storage` في
        // ملفّ الحلّ ومبنيّاً في البوّابة، **ولا مشروع واحد يشير إليه** — فلم يكن
        // ملفّه ينزل إلى مُخرَج اختبارات المعمارية أصلاً، فلم تره هذه القاعدة ولا
        // أخواتها. ولمّا صار الجذر التركيبي يشير إليه (سطح المرفقات) دخل الإحصاء
        // ومعه مخطّطه. أي أن الحرّاس كانوا **يمرّون خضراً على مشروع لا يفحصونه**،
        // وهو فخّ مُسجَّل: traps.md#fakh-a-project-outside-the-reference-graph-is-outside-every-guard
        //
        // القائمة جرد صريح لا حدّ أعلى: وحدة جديدة تملك جدولاً تُضاف هنا بقرار واعٍ،
        // وهذا هو ما يمنع ظهور سياق EF جديد دون أن يراه أحد.
        string[] owners = [.. BabelAssemblies.AllTypes()
            .Where(TypeShapes.IsDbContext)
            .Select(static type => type.Assembly.GetName().Name!)
            .Distinct()
            .Order(StringComparer.Ordinal)];

        // و`Babel.Hr` هي الثامنة — دخلت الإحصاء يوم صارت وحدةً حقيقية بمخطّط `hr`
        // تملكه وحدها، وسياقُها `internal` كأخواتها فلا يعبر حدّها ولا يُقرأ منه بـJOIN.
        Assert.Equal(
            [
                ModuleMap.Compliance, ModuleMap.Core, "Babel.Hr", "Babel.Inventory", ModuleMap.Ledger,
                "Babel.Purchasing", "Babel.RealEstate", "Babel.Sales", ModuleMap.Storage,
            ],
            owners);
    }
}
