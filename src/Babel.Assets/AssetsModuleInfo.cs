using Babel.SharedKernel;

namespace Babel.Assets;

/// <summary>بطاقة الوحدة.</summary>
public static class AssetsModuleInfo
{
    /// <summary>هوية الوحدة.</summary>
    public static BabelModule Module => BabelModule.Assets;

    /// <summary>اسم الوحدة ثنائي اللغة.</summary>
    public static LocalizedName Name { get; } = new("الأصول", "Assets");

    /// <summary>الوحدات التي تتطلبها هذه الوحدة في مجموعة الاستحقاق.</summary>
    public static IReadOnlyList<BabelModule> Requires { get; } =
        Core.Entitlement.ModuleDependencyGraph.RequirementsOf(BabelModule.Assets);
}
