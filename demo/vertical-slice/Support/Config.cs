using Npgsql;

namespace BabelDemo.Support;

/// <summary>
/// إعدادات الاتصال. لا توجد أي كلمة مرور داخل المستودع: كل شيء يُقرأ من متغيّرات
/// البيئة وله قيمة افتراضية محلية بلا كلمة مرور (انظر README.md).
///
/// Connection settings. NO credentials live in this repository: everything is read
/// from the environment and falls back to a password-less local dev connection.
/// </summary>
public static class Config
{
    public const string Database = "babel_demo";
    public const string AppRole = "babel_demo_app";
    public const string BookId = "MAIN";
    public const string TenantId = "DEMO";

    /// <summary>مالك المخطط: ينشئ الجداول والأدوار والصلاحيات، وهو أيضاً «العابث» في المشهد السادس.</summary>
    public static string Owner { get; } =
        Environment.GetEnvironmentVariable("BABEL_DEMO_ADMIN_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true";

    /// <summary>دور التطبيق الأقل امتيازاً: INSERT + SELECT على الدفتر، بلا UPDATE ولا DELETE.</summary>
    public static string App { get; } =
        Environment.GetEnvironmentVariable("BABEL_DEMO_APP_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username={AppRole};Include Error Detail=true";

    /// <summary>اتصال الصيانة، يُستخدم مرة واحدة فقط لإنشاء قاعدة البيانات والدور.</summary>
    public static string Maintenance
    {
        get
        {
            var b = new NpgsqlConnectionStringBuilder(Owner) { Database = "postgres" };
            return b.ConnectionString;
        }
    }

    public static string Describe(string cs)
    {
        var b = new NpgsqlConnectionStringBuilder(cs);
        return $"{b.Username}@{b.Host}:{b.Port}/{b.Database}";
    }
}
