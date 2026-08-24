using System.Globalization;
using System.Reflection;
using Babel.Ledger;
using Npgsql;

namespace Babel.Api.Tests;

/// <summary>
/// قاعدة بيانات الاختبار — <b>PostgreSQL حقيقية</b>، ودور تطبيق غير مالك وغير superuser،
/// والمخطّط منشوراً بالهجرات نفسها، والبيانات المرجعية مبذورة من <c>data/</c>.
/// <para>
/// النمط مأخوذ حرفياً من <c>tests/Babel.Ledger.Tests/LedgerTestEnvironment.cs</c> — بقاعدة
/// بيانات باسم مختلف كي تعمل المجموعتان متوازيتين بلا تداخل. ولا قاعدة بيانات في الذاكرة:
/// أربع من المصائد المقيسة لا تظهر إلا بعد أن تمرّ القيمة على PostgreSQL وتعود.
/// </para>
/// <para>
/// <b>وملاحظة على الانعكاس أدناه:</b> <c>LedgerSchemaDeployer</c> نوع <c>internal</c> في
/// <c>Babel.Ledger</c>، و<c>InternalsVisibleTo</c> فيه يسمّي <c>Babel.Ledger.Tests</c> وحدها.
/// نشر المخطّط من هنا يحتاج إمّا سطراً في <c>Babel.Ledger.csproj</c> — وهو ملف لا يملكه هذا
/// الفرع — وإمّا انعكاساً. اختير الانعكاس كي يبقى مسار النشر <b>هو نفسه</b> مسار الإنتاج،
/// بدل نسخة ثانية من نصوص المخطّط تنحرف عنه بصمت. والطلب مسجَّل في التقرير.
/// </para>
/// </summary>
internal static class ApiTestDatabase
{
    /// <summary>اسم قاعدة بيانات هذه المجموعة.</summary>
    public const string Database = "babel_api_tests";

    /// <summary>دور التطبيق: يدخل، ولا يملك شيئاً، وليس superuser.</summary>
    public const string AppRole = "babel_api_test_app";

    /// <summary>الدفتر الافتراضي.</summary>
    public const string Book = "MAIN";

    /// <summary>السنة المالية المبذورة.</summary>
    public const int FiscalYear = 2026;

    /// <summary>الشركة الأولى — مستأجر «أ».</summary>
    public static Guid CompanyA { get; } = new("a1a1a1a1-0000-4000-8000-000000000001");

    /// <summary>الشركة الثانية — مستأجر «ب». لا يبلغها اعتماد «أ» أبداً.</summary>
    public static Guid CompanyB { get; } = new("b2b2b2b2-0000-4000-8000-000000000002");

    /// <summary>الشركة الثالثة — مستأجر «ج»، وعليه تُشهَد حالات الاستحقاق الثلاث.</summary>
    public static Guid CompanyC { get; } = new("c3c3c3c3-0000-4000-8000-000000000003");

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;

    /// <summary>اتصال الصيانة — لإنشاء قاعدة البيانات والدور.</summary>
    public static string Maintenance =>
        Environment.GetEnvironmentVariable("BABEL_API_TEST_ADMIN_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true";

    /// <summary>إعدادات الدفتر لهذه المجموعة.</summary>
    public static LedgerOptions Options { get; } = new()
    {
        OwnerConnectionString =
            Environment.GetEnvironmentVariable("BABEL_API_TEST_OWNER_DB")
            ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true",
        AppConnectionString =
            Environment.GetEnvironmentVariable("BABEL_API_TEST_APP_DB")
            ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username={AppRole};Include Error Detail=true;Maximum Pool Size=5;Minimum Pool Size=0",
        AppRole = AppRole,
        CompanyCurrency = "SAR",
    };

    /// <summary>جذر المستودع.</summary>
    public static string RepositoryRoot { get; } = RepositoryPaths.Root;

    /// <summary>ينشئ قاعدة البيانات والدور، وينشر المخطّط، ويبذر البيانات المرجعية.</summary>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
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

            await CreateDatabaseAndRoleAsync(cancellationToken).ConfigureAwait(false);
            await ResetSchemaAsync(cancellationToken).ConfigureAwait(false);
            await DeploySchemaAsync(cancellationToken).ConfigureAwait(false);
            await SeedAsync(cancellationToken).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            Gate.Release();
        }
    }

    private static async Task DeploySchemaAsync(CancellationToken cancellationToken)
    {
        Type deployer = typeof(LedgerOptions).Assembly
            .GetType("Babel.Ledger.Persistence.LedgerSchemaDeployer", throwOnError: true)!;

        MethodInfo deploy = deployer.GetMethod("DeployAsync", BindingFlags.Public | BindingFlags.Static)!;

        await ((Task)deploy.Invoke(null, [Options, cancellationToken])!).ConfigureAwait(false);
    }

    private static async Task CreateDatabaseAndRoleAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection admin = new(Maintenance);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        if (await ScalarAsync(admin, $"select count(*) from pg_database where datname = '{Database}'", cancellationToken)
            .ConfigureAwait(false) == 0)
        {
            await ExecAsync(admin, $"create database {Database}", cancellationToken).ConfigureAwait(false);
        }

        // ‏nosuperuser ليست تفصيلاً: بدونها تسقط كل طبقات الحصانة معاً (فخ-30 · ADR-0003).
        if (await ScalarAsync(admin, $"select count(*) from pg_roles where rolname = '{AppRole}'", cancellationToken)
            .ConfigureAwait(false) == 0)
        {
            await ExecAsync(admin, $"create role {AppRole} login nosuperuser nocreatedb nocreaterole noinherit", cancellationToken)
                .ConfigureAwait(false);
        }
        else
        {
            await ExecAsync(admin, $"alter role {AppRole} login nosuperuser nocreatedb nocreaterole noinherit", cancellationToken)
                .ConfigureAwait(false);
        }

        await ExecAsync(admin, $"grant connect on database {Database} to {AppRole}", cancellationToken).ConfigureAwait(false);
    }

    private static async Task ResetSchemaAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection owner = new(Options.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);
        await ExecAsync(owner, "drop schema if exists ledger cascade", cancellationToken).ConfigureAwait(false);
        await ExecAsync(owner, """drop table if exists "__EFMigrationsHistory" """, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>البذر بدور المالك: دور التطبيق لا يملك <c>INSERT</c> على أي جدول مرجعي.</summary>
    private static async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection owner = new(Options.OwnerConnectionString);
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
        List<Dictionary<string, string>> roleMap =
            [.. Csv(Path.Combine(RepositoryRoot, "data", "posting-matrix", "role-map.default.csv"))];

        foreach (Guid company in new[] { CompanyA, CompanyB, CompanyC })
        {
            foreach (Dictionary<string, string> row in accounts.OrderBy(
                static a => a["code"].Length).ThenBy(static a => a["code"], StringComparer.Ordinal))
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
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(row["code"]);
                command.Parameters.AddWithValue(row["name_ar"]);
                command.Parameters.AddWithValue(row["name_en"]);
                command.Parameters.AddWithValue(Babel.Canonicalization.ArabicSearch.Normalize(row["name_ar"]).Value);
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

            foreach (Dictionary<string, string> row in roleMap)
            {
                await using NpgsqlCommand command = new(
                    """
                    insert into ledger.role_account_map (company_id, role_code, qualifier, account_code, status, note_ar, note_en)
                    values ($1,$2,$3,$4,$5,$6,$7) on conflict do nothing
                    """, owner);
                command.Parameters.AddWithValue(company);
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
                string code = FormattableString.Invariant($"{FiscalYear:0000}-{month:00}");
                DateOnly start = new(FiscalYear, month, 1);
                DateOnly end = start.AddMonths(1).AddDays(-1);

                // الفترة 01 مقفلة و02 مقفلة نهائياً والبقية مفتوحة — نفس بذر مجموعة الدفتر،
                // فالرفض الافتراضي والقفل النهائي مشهودان على السلك أيضاً.
                string state = month switch { 1 => "closed", 2 => "permanently_closed", _ => "open" };

                await using NpgsqlCommand command = new(
                    """
                    insert into ledger.fiscal_period
                        (company_id, fiscal_year, period_no, period_code, starts_on, ends_on, state, name_ar, name_en)
                    values ($1,$2,$3,$4,$5,$6,$7,$8,$9)
                    """, owner);
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(FiscalYear);
                command.Parameters.AddWithValue(month);
                command.Parameters.AddWithValue(code);
                command.Parameters.AddWithValue(start);
                command.Parameters.AddWithValue(end);
                command.Parameters.AddWithValue(state);
                command.Parameters.AddWithValue("الفترة " + code);
                command.Parameters.AddWithValue("Period " + code);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (NpgsqlCommand command = new(
                """
                insert into ledger.posting_counter (company_id, book_id, fiscal_year, next_entry_no, next_chain_seq)
                values ($1,$2,$3,1,1) on conflict do nothing
                """, owner))
            {
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(Book);
                command.Parameters.AddWithValue(FiscalYear);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static object Null(string value) => value.Length == 0 ? DBNull.Value : value;

    /// <summary>قارئ CSV بسيط يكفي لهذه الملفات.</summary>
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
        System.Text.StringBuilder current = new();
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
}

/// <summary>مسارات المستودع كما حُقنت في التجميعة وقت البناء.</summary>
internal static class RepositoryPaths
{
    /// <summary>جذر المستودع.</summary>
    public static string Root { get; } = Metadata("BabelRepositoryRoot", static () => FindRoot());

    /// <summary>تهيئة البناء (‏Debug أو Release) — يُشتقّ منها مسار ثنائي الخادم.</summary>
    public static string Configuration { get; } = Metadata("BabelConfiguration", static () => "Debug");

    /// <summary>ثنائي الخادم المبنيّ — يُقلَع عمليةً مستقلّة.</summary>
    public static string ApiExecutable { get; } =
        Path.Combine(Root, "src", "Babel.Api", "bin", Configuration, "net10.0", "Babel.Api");

    private static string Metadata(string key, Func<string> fallback) =>
        typeof(RepositoryPaths).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>()
            .FirstOrDefault(a => string.Equals(a.Key, key, StringComparison.Ordinal))?.Value
        ?? fallback();

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Babel.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName
            ?? throw new InvalidOperationException("تعذّر العثور على جذر المستودع (Babel.slnx).");
    }
}
