namespace Babel.RealEstate;

/// <summary>
/// إعدادات وحدة العقارات. لا كلمة مرور في المستودع: الاتصال من متغيّر بيئة وله
/// افتراضي محلي يعمل مع <c>pg_hba: trust</c> على 127.0.0.1.
/// </summary>
public sealed class RealEstateOptions
{
    /// <summary>اتصال قاعدة بيانات العقارات.</summary>
    public string ConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_REALESTATE_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=babel_realestate;Username=postgres;Include Error Detail=true";

    /// <summary>عملة الشركة. ⚠️ مكانها الطبيعي جدول إعدادات الشركة، لا ثابت.</summary>
    public string CompanyCurrency { get; set; } = "SAR";
}
