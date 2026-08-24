using System.Globalization;
using System.Reflection;
using System.Text;
using Babel.Canonicalization;
using Babel.Core.Entitlement;
using Babel.Ledger;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Sales.Tests;

/// <summary>منفِّذ استحقاق يسمح دائماً — الاستحقاق نفسه مُختبَر في Babel.Core.Tests.</summary>
internal sealed class AlwaysEntitled : IEntitlementEnforcer
{
    public ValueTask<Result> EnsureAsync(
        TenantId tenant,
        UserId actor,
        BabelModule module,
        EntitlementAccess access,
        string operation,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result.Success());
}

/// <summary>طباعة الإثبات: كل بند يُطبع بحكمه وبالدليل الذي أنتجه.</summary>
internal static class Proof
{
    public static void Require(bool condition, string title, string evidence)
    {
        Console.WriteLine((condition ? "PASS — " : "FAIL — ") + title + "\n        الدليل: " + evidence);
        if (!condition)
        {
            throw new InvalidOperationException("FAIL — " + title + ": " + evidence);
        }
    }

    public static void Note(string text) => Console.WriteLine("        " + text);

    public static string Money(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}

/// <summary>
/// بيئة الاختبار: PostgreSQL <b>حقيقية</b>، ودفتر أستاذ منشور بهجراته وبياناته
/// المرجعية من <c>data/</c> نفسها، ووحدة مبيعات بمخطّطها الخاص في قاعدة بيانات
/// منفصلة — تماماً كما يفرض «كل وحدة تملك جداولها».
/// <para>
/// <b>لماذا لا محاكاة للدفتر:</b> المطلوب إثباته هو أن الحساب الضابط في دفتر أستاذ
/// حقيقي يتحرّك بالمبلغ الصحيح بالضبط، وأن المشغّل المؤجَّل ومقياس <c>numeric(19,4)</c>
/// ودقّة <c>timestamptz</c> كلها تعمل. محاكاة الدفتر تُثبت أن المحاكاة تعمل.
/// </para>
/// </summary>
internal static class SalesTestEnvironment
{
    public const string LedgerDatabase = "babel_arap_sales_ledger";
    public const string ModuleDatabase = "babel_arap_sales";
    public const string AppRole = "babel_arap_sales_app";
    public const int FiscalYear = 2026;

    /// <summary>المستأجر المستعمل في كل هذه الاختبارات.</summary>
    public static TenantId Tenant { get; } = new(new Guid("5a1e5a1e-0000-4000-8000-000000000001"));

    /// <summary>
    /// مستأجر ثانٍ معزول تُحقن فيه حركة على نقطة الضبط بلا مستند في الدفتر المساعد —
    /// أشيع سبب حقيقي للانحراف: قيد يدوي على الحساب الضابط.
    /// </summary>
    public static TenantId InjectedTenant { get; } = new(new Guid("5a1e5a1e-0000-4000-8000-000000000002"));

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;

    public static string Maintenance =>
        Environment.GetEnvironmentVariable("BABEL_ARAP_TEST_ADMIN_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true";

    public static LedgerOptions Ledger { get; } = new()
    {
        OwnerConnectionString = $"Host=127.0.0.1;Port=5432;Database={LedgerDatabase};Username=postgres;Include Error Detail=true",
        AppConnectionString = $"Host=127.0.0.1;Port=5432;Database={LedgerDatabase};Username={AppRole};Include Error Detail=true;Maximum Pool Size=40",
        AppRole = AppRole,
        CompanyCurrency = "SAR",
    };

    public static SalesOptions Sales { get; } = new()
    {
        ConnectionString = $"Host=127.0.0.1;Port=5432;Database={ModuleDatabase};Username=postgres;Include Error Detail=true",
        CompanyCurrency = "SAR",
    };

    public static string RepositoryRoot { get; } = FindRoot();

    /// <summary>ينشئ قاعدتي البيانات، وينشر المخطّطين، ويبذر البيانات المرجعية.</summary>
    public static async Task EnsureAsync(CancellationToken cancellationToken = default)
    {
        if (_ready)
        {
            return;
        }

        await Gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_ready)
            {
                return;
            }

            await CreateAsync(cancellationToken).ConfigureAwait(false);
            await DeployLedgerAsync(cancellationToken).ConfigureAwait(false);
            await SeedAsync(cancellationToken).ConfigureAwait(false);
            await SalesSchemaDeployer.DeployAsync(Sales, cancellationToken).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task CreateAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection admin = new(Maintenance);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        await ExecAsync(admin, $"drop database if exists {ModuleDatabase} with (force)", cancellationToken).ConfigureAwait(false);
        await ExecAsync(admin, $"drop database if exists {LedgerDatabase} with (force)", cancellationToken).ConfigureAwait(false);
        await ExecAsync(admin, $"create database {ModuleDatabase}", cancellationToken).ConfigureAwait(false);
        await ExecAsync(admin, $"create database {LedgerDatabase}", cancellationToken).ConfigureAwait(false);

        // الدور التطبيقي: يدخل، ولا يملك شيئاً، وليس superuser — الطبقة الأولى (فخ-30).
        long roles = await ScalarAsync(admin, $"select count(*) from pg_roles where rolname = '{AppRole}'", cancellationToken)
            .ConfigureAwait(false);

        await ExecAsync(
            admin,
            (roles == 0 ? "create role " : "alter role ") + AppRole + " login nosuperuser nocreatedb nocreaterole noinherit",
            cancellationToken).ConfigureAwait(false);

        await ExecAsync(admin, $"grant connect on database {LedgerDatabase} to {AppRole}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// ينشر مخطّط الدفتر.
    /// <para>
    /// <b>عبر الانعكاس، وهذا نتيجة لا اختيار:</b> <c>LedgerSchemaDeployer</c> نوع
    /// <c>internal</c> ولا يراه إلا <c>Babel.Ledger.Tests</c>، فلا يملك أي مستهلك
    /// خارجي — ولا الجذر التركيبي — طريقاً معلناً لنشر مخطّط الدفتر. هذا بند في
    /// تقرير هذا التسليم، لا تعديل يُتخذ ضمناً في مشروع لا نملكه.
    /// </para>
    /// </summary>
    private static async Task DeployLedgerAsync(CancellationToken cancellationToken)
    {
        Type deployer = typeof(LedgerRuntime).Assembly.GetType("Babel.Ledger.Persistence.LedgerSchemaDeployer")
            ?? throw new InvalidOperationException("لم يُعثر على ناشر مخطّط الدفتر.");

        MethodInfo deploy = deployer.GetMethod("DeployAsync", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("لم يُعثر على DeployAsync.");

        await ((Task)deploy.Invoke(null, [Ledger, cancellationToken])!).ConfigureAwait(false);
    }

    private static async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection owner = new(Ledger.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);

        foreach (Dictionary<string, string> row in Csv(Path.Combine(RepositoryRoot, "data", "posting-matrix", "account-roles.csv")))
        {
            await using NpgsqlCommand command = new(
                """
                insert into ledger.posting_role
                    (role_code, name_ar, name_en, expected_account_type, expected_side, status, note_ar, note_en)
                values ($1,$2,$3,$4,$5,$6,$7,$8) on conflict do nothing
                """, owner);
            command.Parameters.AddWithValue(row["role_code"]);
            command.Parameters.AddWithValue(row["name_ar"]);
            command.Parameters.AddWithValue(row["name_en"]);
            command.Parameters.AddWithValue(Null(row["expected_account_type"]));
            command.Parameters.AddWithValue(Null(row["expected_side"]));
            command.Parameters.AddWithValue(row["status"]);
            command.Parameters.AddWithValue(Null(row["note_ar"]));
            command.Parameters.AddWithValue(Null(row["note_en"]));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
        }

        List<Dictionary<string, string>> accounts =
            [.. Csv(Path.Combine(RepositoryRoot, "data", "chart-of-accounts", "accounts.csv"))];

        foreach (TenantId company in new[] { Tenant, InjectedTenant })
        {
            foreach (Dictionary<string, string> row in accounts
                         .OrderBy(static a => a["code"].Length).ThenBy(static a => a["code"], StringComparer.Ordinal))
            {
                await using NpgsqlCommand command = new(
                    """
                    insert into ledger.account
                        (company_id, account_code, name_ar, name_en, name_ar_search, parent_code, account_level,
                         account_type, natural_side, is_postable, is_contra, statement_section, subledger_type,
                         required_dimensions, currency_mode, currency_code, is_protected, is_active, status,
                         source_ref, caveat_ar, caveat_en)
                    values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,$17,true,$18,$19,$20,$21)
                    """, owner);
                command.Parameters.AddWithValue(company.Value);
                command.Parameters.AddWithValue(row["code"]);
                command.Parameters.AddWithValue(row["name_ar"]);
                command.Parameters.AddWithValue(row["name_en"]);
                command.Parameters.AddWithValue(ArabicSearch.Normalize(row["name_ar"]).Value);
                command.Parameters.AddWithValue(Null(row["parent_code"]));
                command.Parameters.AddWithValue(int.Parse(row["level"], CultureInfo.InvariantCulture));
                command.Parameters.AddWithValue(row["account_type"]);
                command.Parameters.AddWithValue(row["natural_side"]);
                command.Parameters.AddWithValue(row["is_postable"] == "true");
                command.Parameters.AddWithValue(row["is_contra"] == "true");
                command.Parameters.AddWithValue(Null(row["statement_section"]));
                command.Parameters.AddWithValue(row["subledger_type"]);
                command.Parameters.AddWithValue(row["required_dimensions"].Length == 0
                    ? Array.Empty<string>()
                    : row["required_dimensions"].Split('|'));
                command.Parameters.AddWithValue(row["currency_mode"]);
                command.Parameters.AddWithValue(Null(row["currency_code"]));
                command.Parameters.AddWithValue(row["is_protected"] == "true");
                command.Parameters.AddWithValue(row["status"]);
                command.Parameters.AddWithValue(Null(row["source_ref"]));
                command.Parameters.AddWithValue(Null(row["caveat_ar"]));
                command.Parameters.AddWithValue(Null(row["caveat_en"]));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            foreach (Dictionary<string, string> row in Csv(Path.Combine(RepositoryRoot, "data", "posting-matrix", "role-map.default.csv")))
            {
                await using NpgsqlCommand command = new(
                    """
                    insert into ledger.role_account_map (company_id, role_code, qualifier, account_code, status, note_ar, note_en)
                    values ($1,$2,$3,$4,$5,$6,$7) on conflict do nothing
                    """, owner);
                command.Parameters.AddWithValue(company.Value);
                command.Parameters.AddWithValue(row["role_code"]);
                command.Parameters.AddWithValue(row["qualifier"]);
                command.Parameters.AddWithValue(row["account_code"]);
                command.Parameters.AddWithValue(row["status"]);
                command.Parameters.AddWithValue(Null(row["note_ar"]));
                command.Parameters.AddWithValue(Null(row["note_en"]));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            for (int month = 1; month <= 12; month++)
            {
                string code = FiscalYear.ToString(CultureInfo.InvariantCulture) + "-" + month.ToString("00", CultureInfo.InvariantCulture);
                DateOnly start = new(FiscalYear, month, 1);

                // الفترة 02 مقفلة نهائياً: عليها يُثبَت أن الرفض يترك المستند متّسقاً.
                string state = month == 2 ? "permanently_closed" : "open";

                await using NpgsqlCommand command = new(
                    """
                    insert into ledger.fiscal_period
                        (company_id, fiscal_year, period_no, period_code, starts_on, ends_on, state, name_ar, name_en)
                    values ($1,$2,$3,$4,$5,$6,$7,$8,$9)
                    """, owner);
                command.Parameters.AddWithValue(company.Value);
                command.Parameters.AddWithValue(FiscalYear);
                command.Parameters.AddWithValue(month);
                command.Parameters.AddWithValue(code);
                command.Parameters.AddWithValue(start);
                command.Parameters.AddWithValue(start.AddMonths(1).AddDays(-1));
                command.Parameters.AddWithValue(state);
                command.Parameters.AddWithValue("الفترة " + code);
                command.Parameters.AddWithValue("Period " + code);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (NpgsqlCommand command = new(
                """
                insert into ledger.posting_counter (company_id, book_id, fiscal_year, next_entry_no, next_chain_seq)
                values ($1,'MAIN',$2,1,1)
                """, owner))
            {
                command.Parameters.AddWithValue(company.Value);
                command.Parameters.AddWithValue(FiscalYear);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static object Null(string value) => value.Length == 0 ? DBNull.Value : value;

    public static IEnumerable<Dictionary<string, string>> Csv(string path)
    {
        string[] lines = File.ReadAllLines(path);
        string[] header = SplitCsv(lines[0]);

        for (int i = 1; i < lines.Length; i++)
        {
            if (lines[i].Length == 0)
            {
                continue;
            }

            string[] cells = SplitCsv(lines[i]);
            Dictionary<string, string> row = new(StringComparer.Ordinal);
            for (int c = 0; c < header.Length; c++)
            {
                row[header[c]] = c < cells.Length ? cells[c] : string.Empty;
            }

            yield return row;
        }
    }

    private static string[] SplitCsv(string line)
    {
        List<string> cells = [];
        StringBuilder current = new();
        bool quoted = false;

        for (int i = 0; i < line.Length; i++)
        {
            char c = line[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < line.Length && line[i + 1] == '"')
                    {
                        current.Append('"');
                        i++;
                    }
                    else
                    {
                        quoted = false;
                    }
                }
                else
                {
                    current.Append(c);
                }

                continue;
            }

            switch (c)
            {
                case '"':
                    quoted = true;
                    break;
                case ',':
                    cells.Add(current.ToString());
                    current.Clear();
                    break;
                default:
                    current.Append(c);
                    break;
            }
        }

        cells.Add(current.ToString());
        return [.. cells];
    }

    private static async Task ExecAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task<long> ScalarAsync(NpgsqlConnection connection, string sql, CancellationToken cancellationToken)
    {
        await using NpgsqlCommand command = new(sql, connection);
        object? value = await command.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
        return Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Babel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("تعذّر العثور على جذر المستودع.");
    }
}
