using Npgsql;

namespace BabelPosOffline.Support;

/// <summary>
/// إعدادات الاتصال. لا توجد أي كلمة مرور داخل هذا المستودع؛ كل شيء من متغيرات البيئة
/// مع افتراضي محلي بلا كلمة مرور (loopback trust).
/// No credential is stored in this repository: everything is read from the
/// environment, with a password-less local loopback default.
/// </summary>
public static class Config
{
    public const string Database = "babel_posspike";

    public static string Admin { get; } =
        Environment.GetEnvironmentVariable("BABEL_POSSPIKE_ADMIN_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true;" +
           "Maximum Pool Size=64;Timeout=30;Command Timeout=120";

    public static string Maintenance
    {
        get
        {
            var b = new NpgsqlConnectionStringBuilder(Admin) { Database = "postgres" };
            return b.ConnectionString;
        }
    }

    /// <summary>مجلّد قواعد بيانات الأجهزة (خارج المستودع دائماً) / device DB directory, never inside the repo.</summary>
    public static string DeviceDir { get; } =
        Environment.GetEnvironmentVariable("BABEL_POSSPIKE_DEVICE_DIR")
        ?? Path.Combine(Path.GetTempPath(), "babel-pos-devices");

    public const string Tenant = "T-001";
}
