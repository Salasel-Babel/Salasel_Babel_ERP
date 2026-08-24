using System.Globalization;
using Babel.Ledger.Persistence;
using Babel.SharedKernel;
using Npgsql;

namespace Babel.Ledger.Tests;

/// <summary>
/// بيئة الاختبار: قاعدة بيانات PostgreSQL <b>حقيقية</b>، ودور تطبيق <b>غير مالك
/// وغير superuser</b>، والمخطّط منشوراً بالهجرات، والبيانات المرجعية مبذورة من
/// <c>data/</c> نفسها — لا من نسخة مكتوبة بيد في ملف اختبار.
/// <para>
/// <b>لماذا لا قاعدة بيانات في الذاكرة:</b> أربع من المصائد المقيسة في هذا المشروع
/// لا تظهر إلا بعد أن تمرّ القيمة على PostgreSQL وتعود — مقياس <c>numeric(19,4)</c>،
/// ودقّة <c>timestamptz</c>، ورفض الصلاحيات 42501، والمشغّل المؤجَّل عند COMMIT.
/// اختبار بلا قاعدة بيانات حقيقية يمرّ وكل واحدة منها مكسورة.
/// </para>
/// <para>
/// ولا كلمة مرور في أي مكان: <c>pg_hba</c> يمنح <c>trust</c> على 127.0.0.1، وكل
/// اتصال يُقرأ من متغيّر بيئة له افتراضي محلي (انظر <see cref="LedgerOptions"/>).
/// </para>
/// </summary>
internal static class LedgerTestEnvironment
{
    public const string Database = "babel_ledger_tests";
    public const string AppRole = "babel_ledger_test_app";
    public const string Book = "MAIN";
    public const int FiscalYear = 2026;

    /// <summary>المستأجر الأول — خريطة الأدوار المرجعية كما هي.</summary>
    public static Guid TenantA { get; } = new("aaaaaaaa-0000-4000-8000-000000000001");

    /// <summary>المستأجر الثاني — خريطة مختلفة في دور واحد، بلا سطر كود واحد.</summary>
    public static Guid TenantB { get; } = new("bbbbbbbb-0000-4000-8000-000000000002");

    /// <summary>
    /// المدقّق الذي يقرأ — <b>إنسان</b> لا فاعل نظام. قراءات الدفتر تُقاس على محور
    /// «المستخدم الفاعل» كما تُقاس الكتابة، فالفاعل يُمرَّر إليها صراحةً.
    /// </summary>
    public static UserId Auditor { get; } = new(new Guid("4d4d4d4d-0000-4000-8000-00000000000a"));

    /// <summary>عقار مملوك للشركة.</summary>
    public const string OwnProperty = "P-OWN-001";

    /// <summary>عقار مُدار لصالح الغير — عليه تُفحص GR-RE-001.</summary>
    public const string ManagedProperty = "P-MANAGED-001";

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;

    /// <summary>
    /// كلمة مرور دور التطبيق في بيئات لا تسمح بـ<c>trust</c> (خدمة PostgreSQL في CI).
    /// فارغة افتراضياً: التشغيل المحلي بلا كلمة مرور إطلاقاً، ولا سرّ في المستودع.
    /// </summary>
    private static string AppPassword =>
        Environment.GetEnvironmentVariable("BABEL_LEDGER_TEST_APP_PASSWORD") ?? string.Empty;

    public static string Maintenance =>
        Environment.GetEnvironmentVariable("BABEL_LEDGER_TEST_ADMIN_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true";

    public static LedgerOptions Options { get; } = new()
    {
        OwnerConnectionString =
            Environment.GetEnvironmentVariable("BABEL_LEDGER_TEST_OWNER_DB")
            ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true",
        AppConnectionString =
            Environment.GetEnvironmentVariable("BABEL_LEDGER_TEST_APP_DB")
            ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username={AppRole};Include Error Detail=true;Maximum Pool Size=40",
        AppRole = AppRole,
        CompanyCurrency = "SAR",
    };

    /// <summary>جذر المستودع — تُقرأ منه بيانات دليل الحسابات والمصفوفة.</summary>
    public static string RepositoryRoot { get; } = FindRoot();

    /// <summary>ينشئ قاعدة البيانات والدور، وينشر المخطّط، ويبذر البيانات المرجعية.</summary>
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
            await LedgerSchemaDeployer.DeployAsync(Options, cancellationToken).ConfigureAwait(false);
            await SeedAsync(cancellationToken).ConfigureAwait(false);
            _ready = true;
        }
        finally
        {
            Gate.Release();
        }
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

        // الدور التطبيقي: يدخل، ولا يملك شيئاً، وليس superuser. هذه هي الطبقة
        // الأولى من الحصانة، ومن دون nosuperuser تسقط كل الطبقات (فخ-30).
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

        if (AppPassword.Length > 0)
        {
            await ExecAsync(
                admin,
                $"alter role {AppRole} password '{AppPassword.Replace("'", "''", StringComparison.Ordinal)}'",
                cancellationToken).ConfigureAwait(false);
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

    /// <summary>
    /// البذر <b>بدور المالك</b>: دور التطبيق لا يملك <c>INSERT</c> على أي جدول
    /// مرجعي، وهذا مقصود — دليل الحسابات ليس شيئاً يكتبه مسار الترحيل.
    /// </summary>
    private static async Task SeedAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection owner = new(Options.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ── كتالوج الأدوار (جدول عام، ليس لكل شركة) ──────────────────────
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

        foreach (Guid company in new[] { TenantA, TenantB })
        {
            // الأب قبل الابن: القيد ck_account_parent_matches_code يفرض ذلك.
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
                command.Parameters.AddWithValue(Canonicalization.ArabicSearch.Normalize(row["name_ar"]).Value);
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
                // المستأجر الثاني يوجّه دوراً واحداً إلى رقم حسابه هو — وهذا كل ما
                // يلزم لجعل الحدث نفسه يُنتج حساباً آخر: صفٌّ في جدول، لا كود.
                string account = company == TenantB && row["role_code"] == "rental_revenue" ? "4305" : row["account_code"];

                await using NpgsqlCommand command = new(
                    """
                    insert into ledger.role_account_map (company_id, role_code, qualifier, account_code, status, note_ar, note_en)
                    values ($1,$2,$3,$4,$5,$6,$7) on conflict do nothing
                    """, owner);
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(row["role_code"]);
                command.Parameters.AddWithValue(row["qualifier"]);
                command.Parameters.AddWithValue(account);
                command.Parameters.AddWithValue(row["status"]);
                command.Parameters.AddWithValue(Null(row["note_ar"]));
                command.Parameters.AddWithValue(Null(row["note_en"]));
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await using (NpgsqlCommand command = new(
                """
                insert into ledger.property_dimension (company_id, property_id, ownership_model, name_ar, name_en)
                values ($1,$2,'own_property','برج الملكية الذاتية','Own Property Tower'),
                       ($1,$3,'managed_for_others','برج مُدار لصالح الغير','Managed-for-Others Tower')
                """, owner))
            {
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(OwnProperty);
                command.Parameters.AddWithValue(ManagedProperty);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            for (int month = 1; month <= 12; month++)
            {
                string code = $"{FiscalYear.ToString(CultureInfo.InvariantCulture)}-{month:00}";
                DateOnly start = new(FiscalYear, month, 1);
                DateOnly end = start.AddMonths(1).AddDays(-1);

                // الفترة 01 مقفلة، و02 مقفلة نهائياً، والبقية مفتوحة: الرفض
                // الافتراضي والإذن الاستثنائي والقفل الذي لا يفتحه إذن، كلها
                // مشهودة على بيانات واحدة.
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
                values ($1,$2,$3,1,1)
                """, owner))
            {
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(Book);
                command.Parameters.AddWithValue(FiscalYear);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    /// <summary>ينشئ صفّ عدّاد لدفتر إضافي — كل اختبار على نطاقه فلا يتداخل مع غيره.</summary>
    public static async Task EnsureCounterAsync(Guid company, string book, CancellationToken cancellationToken = default)
    {
        await using NpgsqlConnection owner = new(Options.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using NpgsqlCommand command = new(
            """
            insert into ledger.posting_counter (company_id, book_id, fiscal_year, next_entry_no, next_chain_seq)
            values ($1,$2,$3,1,1) on conflict do nothing
            """, owner);
        command.Parameters.AddWithValue(company);
        command.Parameters.AddWithValue(book);
        command.Parameters.AddWithValue(FiscalYear);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static object Null(string value) => value.Length == 0 ? DBNull.Value : value;

    /// <summary>قارئ CSV بسيط يكفي لهذه الملفات: اقتباس مزدوج وفواصل داخل الاقتباس.</summary>
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
