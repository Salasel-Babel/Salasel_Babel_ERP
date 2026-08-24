using System.Collections.ObjectModel;
using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>
/// رسم اعتماد الوحدات، وهو أساس رفض أي مجموعة استحقاق غير متسقة.
/// <para>
/// «إلزامية» هنا تعني: لا تُباع منفصلة ولا تُطفأ. الدفتر والنواة إلزاميان دائماً؛
/// المبيعات والمشتريات إلزاميتان مع الدفتر؛ والالتزام إلزامي في السوق السعودي.
/// </para>
/// </summary>
public static class ModuleDependencyGraph
{
    private static readonly ReadOnlyDictionary<BabelModule, IReadOnlyList<BabelModule>> RequirementsMap =
        new(new Dictionary<BabelModule, IReadOnlyList<BabelModule>>
        {
            [BabelModule.Core] = [],
            [BabelModule.Ledger] = [BabelModule.Core],
            [BabelModule.Sales] = [BabelModule.Core, BabelModule.Ledger],
            [BabelModule.Purchasing] = [BabelModule.Core, BabelModule.Ledger],
            [BabelModule.Compliance] = [BabelModule.Core, BabelModule.Ledger, BabelModule.Sales],
            [BabelModule.Inventory] = [BabelModule.Core, BabelModule.Ledger],
            [BabelModule.Pos] = [BabelModule.Core, BabelModule.Ledger, BabelModule.Sales, BabelModule.Inventory],
            [BabelModule.Hr] = [BabelModule.Core, BabelModule.Ledger],
            [BabelModule.Projects] = [BabelModule.Core, BabelModule.Ledger, BabelModule.Inventory],
            [BabelModule.RealEstate] = [BabelModule.Core, BabelModule.Ledger],
            [BabelModule.Assets] = [BabelModule.Core, BabelModule.Ledger],
            [BabelModule.Portals] = [BabelModule.Core, BabelModule.Ledger],
            [BabelModule.Ai] = [BabelModule.Core],
        });

    private static readonly ReadOnlyCollection<BabelModule> MandatoryModules =
        new([BabelModule.Core, BabelModule.Ledger, BabelModule.Sales, BabelModule.Purchasing, BabelModule.Compliance]);

    /// <summary>كل الوحدات.</summary>
    public static IReadOnlyList<BabelModule> All { get; } = new ReadOnlyCollection<BabelModule>(Enum.GetValues<BabelModule>());

    /// <summary>الوحدات التي لا تُباع منفصلة ولا تُطفأ.</summary>
    public static IReadOnlyList<BabelModule> Mandatory => MandatoryModules;

    /// <summary>هل الوحدة إلزامية؟</summary>
    public static bool IsMandatory(BabelModule module) => MandatoryModules.Contains(module);

    /// <summary>الوحدات التي تعتمد عليها الوحدة المعطاة اعتماداً مباشراً.</summary>
    public static IReadOnlyList<BabelModule> RequirementsOf(BabelModule module) =>
        RequirementsMap.TryGetValue(module, out IReadOnlyList<BabelModule>? requirements)
            ? requirements
            : throw new ArgumentOutOfRangeException(nameof(module), module, "وحدة غير معروفة. / Unknown module.");

    /// <summary>الوحدات التي تعتمد على الوحدة المعطاة اعتماداً مباشراً.</summary>
    public static IReadOnlyList<BabelModule> DependentsOf(BabelModule module) =>
        [.. RequirementsMap.Where(pair => pair.Value.Contains(module)).Select(static pair => pair.Key)];

    /// <summary>الإغلاق التعدّي لاعتمادات وحدة.</summary>
    public static IReadOnlyList<BabelModule> TransitiveRequirementsOf(BabelModule module)
    {
        HashSet<BabelModule> visited = [];
        Stack<BabelModule> pending = new(RequirementsOf(module));

        while (pending.Count > 0)
        {
            BabelModule current = pending.Pop();
            if (!visited.Add(current))
            {
                continue;
            }

            foreach (BabelModule requirement in RequirementsOf(current))
            {
                pending.Push(requirement);
            }
        }

        return [.. visited];
    }
}
