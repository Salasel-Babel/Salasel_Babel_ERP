using Babel.SharedKernel;

namespace Babel.Portals;

/// <summary>بطاقة الوحدة.</summary>
public static class PortalsModuleInfo
{
    /// <summary>هوية الوحدة.</summary>
    public static BabelModule Module => BabelModule.Portals;

    /// <summary>اسم الوحدة ثنائي اللغة.</summary>
    public static LocalizedName Name { get; } = new("البوابات", "Portals");

    /// <summary>الوحدات التي تتطلبها هذه الوحدة في مجموعة الاستحقاق.</summary>
    public static IReadOnlyList<BabelModule> Requires { get; } =
        Core.Entitlement.ModuleDependencyGraph.RequirementsOf(BabelModule.Portals);
}
