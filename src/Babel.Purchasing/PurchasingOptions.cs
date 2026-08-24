namespace Babel.Purchasing;

/// <summary>
/// إعدادات وحدة المشتريات. لا كلمة مرور في المستودع: الاتصال من متغيّر بيئة وله
/// افتراضي محلي يعمل مع <c>pg_hba: trust</c> على 127.0.0.1.
/// </summary>
public sealed class PurchasingOptions
{
    /// <summary>اتصال قاعدة بيانات المشتريات.</summary>
    public string ConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_PURCHASING_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=babel_purchasing;Username=postgres;Include Error Detail=true";

    /// <summary>عملة الشركة. ⚠️ مكانها الطبيعي جدول إعدادات الشركة، لا ثابت.</summary>
    public string CompanyCurrency { get; set; } = "SAR";
}
