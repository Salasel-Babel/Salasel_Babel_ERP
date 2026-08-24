using Babel.SharedKernel;

namespace Babel.Sales;

/// <summary>بطاقة الوحدة.</summary>
public static class SalesModuleInfo
{
    /// <summary>هوية الوحدة.</summary>
    public static BabelModule Module => BabelModule.Sales;

    /// <summary>اسم الوحدة ثنائي اللغة.</summary>
    public static LocalizedName Name { get; } = new("المبيعات", "Sales");

    /// <summary>الوحدات التي تتطلبها هذه الوحدة في مجموعة الاستحقاق.</summary>
    public static IReadOnlyList<BabelModule> Requires { get; } =
        Core.Entitlement.ModuleDependencyGraph.RequirementsOf(BabelModule.Sales);
}
