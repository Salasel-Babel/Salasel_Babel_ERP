namespace Babel.Projects;

/// <summary>
/// إعدادات وحدة المقاولات. لا كلمة مرور واحدة في المستودع: الاتصال يُقرأ من متغيّر بيئة
/// وله افتراضي محلي يعمل مع <c>pg_hba: trust</c> على 127.0.0.1.
/// </summary>
public sealed class ProjectsOptions
{
    /// <summary>اتصال قاعدة بيانات المقاولات.</summary>
    public string ConnectionString { get; set; } =
        Environment.GetEnvironmentVariable("BABEL_PROJECTS_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=babel_projects;Username=postgres;Include Error Detail=true";

    /// <summary>عملة الشركة. ⚠️ مكانها الطبيعي جدول إعدادات الشركة، لا ثابت — كما في المبيعات.</summary>
    public string CompanyCurrency { get; set; } = "SAR";
}
