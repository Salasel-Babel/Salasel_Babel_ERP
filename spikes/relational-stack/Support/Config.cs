using Npgsql;

namespace BabelRelationalSpike.Support;

/// <summary>
/// Connection settings. NO credentials live in this repository: everything is
/// read from the environment and falls back to a password-less local dev
/// connection (see README.md).
/// لا توجد أي كلمة مرور داخل المستودع؛ كل شيء يُقرأ من متغيرات البيئة.
/// </summary>
public static class Config
{
    public const string Database = "babel_relspike";
    public const string AppRole = "babel_ledger_app";

    /// <summary>Owner / DDL connection. Creates schemas, roles, grants; also the tamperer in (E).</summary>
    public static string Admin { get; } =
        Environment.GetEnvironmentVariable("BABEL_RELSPIKE_ADMIN_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true";

    /// <summary>Least-privilege application connection: INSERT + SELECT on the ledger, nothing else.</summary>
    public static string App { get; } =
        Environment.GetEnvironmentVariable("BABEL_RELSPIKE_APP_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username={AppRole};Include Error Detail=true";

    /// <summary>Maintenance connection used only to CREATE DATABASE on first run.</summary>
    public static string Maintenance
    {
        get
        {
            var b = new NpgsqlConnectionStringBuilder(Admin) { Database = "postgres" };
            return b.ConnectionString;
        }
    }

    public const string LedgerSchema = "ledger";
    public const string AppSchema = "app";
    public const string WolverineSchema = "wolverine";
}
