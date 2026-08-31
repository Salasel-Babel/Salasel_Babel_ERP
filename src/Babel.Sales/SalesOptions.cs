namespace Babel.Sales;

/// <summary>
/// إعدادات وحدة المبيعات. لا كلمة مرور واحدة في المستودع: الاتصال يُقرأ من متغيّر بيئة
/// وله افتراضي محلي يعمل مع <c>pg_hba: trust</c> على 127.0.0.1.
/// </summary>
public sealed class SalesOptions
{
    /// <summary>اتصال قاعدة بيانات المبيعات.</summary>
    public string ConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_SALES_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=babel_sales;Username=postgres;Include Error Detail=true";

    /// <summary>عملة الشركة. ⚠️ مكانها الطبيعي جدول إعدادات الشركة، لا ثابت.</summary>
    public string CompanyCurrency { get; set; } = "SAR";
}
