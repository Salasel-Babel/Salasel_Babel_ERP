using Babel.SharedKernel;

namespace Babel.Ledger;

/// <summary>بطاقة الوحدة.</summary>
public static class LedgerModuleInfo
{
    /// <summary>هوية الوحدة.</summary>
    public static BabelModule Module => BabelModule.Ledger;

    /// <summary>اسم الوحدة ثنائي اللغة.</summary>
    public static LocalizedName Name { get; } = new("دفتر الأستاذ", "Ledger");
}
