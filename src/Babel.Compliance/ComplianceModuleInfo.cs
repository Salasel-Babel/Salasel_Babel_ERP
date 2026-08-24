using Babel.SharedKernel;

namespace Babel.Compliance;

/// <summary>بطاقة الوحدة.</summary>
public static class ComplianceModuleInfo
{
    /// <summary>هوية الوحدة.</summary>
    public static BabelModule Module => BabelModule.Compliance;

    /// <summary>اسم الوحدة ثنائي اللغة.</summary>
    public static LocalizedName Name { get; } = new("الالتزام", "Compliance");

    /// <summary>الوحدات التي تتطلبها هذه الوحدة في مجموعة الاستحقاق.</summary>
    public static IReadOnlyList<BabelModule> Requires { get; } =
        Core.Entitlement.ModuleDependencyGraph.RequirementsOf(BabelModule.Compliance);
}
