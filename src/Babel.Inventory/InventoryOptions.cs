namespace Babel.Inventory;

/// <summary>
/// إعدادات وحدة المخزون. لا كلمة مرور في المستودع: الاتصال من متغيّر بيئة وله
/// افتراضي محلي يعمل مع <c>pg_hba: trust</c> على 127.0.0.1.
/// </summary>
public sealed class InventoryOptions
{
    /// <summary>اتصال قاعدة بيانات المخزون.</summary>
    public string ConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_INVENTORY_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=babel_inventory;Username=postgres;Include Error Detail=true";

    /// <summary>عملة الشركة. ⚠️ مكانها الطبيعي جدول إعدادات الشركة، لا ثابت.</summary>
    public string CompanyCurrency { get; set; } = "SAR";
}
