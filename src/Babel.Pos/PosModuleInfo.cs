using Babel.SharedKernel;

namespace Babel.Pos;

/// <summary>بطاقة الوحدة.</summary>
public static class PosModuleInfo
{
    /// <summary>هوية الوحدة.</summary>
    public static BabelModule Module => BabelModule.Pos;

    /// <summary>اسم الوحدة ثنائي اللغة.</summary>
    public static LocalizedName Name { get; } = new("نقاط البيع", "Pos");

    /// <summary>الوحدات التي تتطلبها هذه الوحدة في مجموعة الاستحقاق.</summary>
    public static IReadOnlyList<BabelModule> Requires { get; } =
        Core.Entitlement.ModuleDependencyGraph.RequirementsOf(BabelModule.Pos);
}
