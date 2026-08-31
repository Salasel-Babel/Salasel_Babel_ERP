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
    /// <summary>الجذع الثابت لاسم قاعدة الدفتر — تُلحق به لاحقة هذه العملية.</summary>
    public const string LedgerDatabaseStem = "babel_arap_sales_ledger";

    /// <summary>الجذع الثابت لاسم قاعدة الوحدة — تُلحق به لاحقة هذه العملية.</summary>
    public const string ModuleDatabaseStem = "babel_arap_sales";

    /// <summary>
    /// قاعدة الدفتر <b>لهذه العملية وحدها</b>.
    /// <para>
    /// الاسم كان ثابتاً، وكانت التهيئة تبدأ بـ<c>drop database … with (force)</c>.
    /// فعمليتان متزامنتان تُسقط كلٌّ منهما قاعدة الأخرى في منتصف تشغيلها. مقيس على
    /// هذا الجهاز على الشيفرة قبل هذا الإصلاح: تشغيلان متوازيان لهذه المجموعة
    /// أسقطا 15 و23 اختباراً من 24 بـ<c>57P01</c> و<c>42P04</c> و<c>23505</c>.
    /// </para>
    /// </summary>
    public static string LedgerDatabase { get; } = TestRunScope.Name(LedgerDatabaseStem);

    /// <summary>
    /// الجذع الثابت لقاعدة مسبار الترقية في
    /// <c>PostingIdentityIncludesEventCodeTests</c> — تُلحق به لاحقة هذه العملية كذلك.
    /// <para>
    /// وهو معلن هنا لا هناك لسبب واحد: الكنس أدناه يجب أن يعرفه. فبعد أن صار
    /// الاسم خاصّاً بالعملية، لم يعد تشغيلٌ لاحق يستعيد قاعدة تشغيلٍ قُتل قبل
    /// أن ينفّذ <c>finally</c> — فيكنسها هذا الكنس بشرطه: مالكٌ ثبت موته.
    /// </para>
    /// </summary>
    public const string UpgradeProbeDatabaseStem = "babel_arap_sales_upgrade_probe";

    /// <summary>قاعدة وحدة المبيعات <b>لهذه العملية وحدها</b>.</summary>
    public static string ModuleDatabase { get; } = TestRunScope.Name(ModuleDatabaseStem);

    /// <summary>
    /// دور التطبيق — اسمه <b>مشترك عمداً</b>: الأدوار عامّة على مستوى العنقود، ولا
    /// يحذفها أحد ولا يملك أي منها كائناً، فلا شيء فيها يُدمَّر. الشيء الوحيد الذي
    /// كان يتسابق عليه هو <b>إنشاؤه</b>، وقد صار الإنشاء محصَّناً في <c>CreateAsync</c>.
    /// </summary>
    public const string AppRole = "babel_arap_sales_app";

    public const int FiscalYear = 2026;

    /// <summary>المستأجر المستعمل في كل هذه الاختبارات.</summary>
    public static TenantId Tenant { get; } = new(new Guid("5a1e5a1e-0000-4000-8000-000000000001"));

    /// <summary>
    /// مستأجر ثانٍ معزول تُحقن فيه حركة على نقطة الضبط بلا مستند في الدفتر المساعد —
    /// أشيع سبب حقيقي للانحراف: قيد يدوي على الحساب الضابط.
    /// </summary>
    public static TenantId InjectedTenant { get; } = new(new Guid("5a1e5a1e-0000-4000-8000-000000000002"));

    /// <summary>
    /// مستأجر ثالث معزول تُختبر فيه هوية الإحكام على مستوى البوابة مباشرةً.
    /// <para>
    /// عزله ليس ترفاً: إثبات الهوية يُرحّل أحداثاً <b>بلا مستند مقابل في الدفتر
    /// المساعد</b>، وذلك بالضبط ما تسمّيه المطابقة انحرافاً. خلطه بمستأجر
    /// المطابقة يجعل كل إثبات يُفسد الآخر.
    /// </para>
    /// </summary>
    public static TenantId GatewayTenant { get; } = new(new Guid("5a1e5a1e-0000-4000-8000-000000000003"));

    /// <summary>
    /// مستأجر رابع <b>يُشغّل قدرة الدفعة المقدمة</b> في ملفّ قدراته.
    /// <para>
    /// وهو نصف إثبات القبول: هو وأخوه أدناه لهما دفتر أستاذ مبذور بالكامل وشيفرة
    /// واحدة ومصفوفة واحدة، ولا يفترقان إلا في <b>صفّ ملفّ القدرات</b>. ولو كان
    /// الرفض عند المُطفأ ناتجاً عن نقص في الحساب أو في المصفوفة لسقط عند المُشغَّل
    /// أيضاً — فوجود الاثنين هو ما يجعل الإثبات إثباتاً.
    /// </para>
    /// </summary>
    public static TenantId AdvanceEnabledTenant { get; } = new(new Guid("5a1e5a1e-0000-4000-8000-000000000004"));

    /// <summary>مستأجر خامس <b>يُطفئ قدرة الدفعة المقدمة</b>، وكل شيء آخر فيه مطابق لأخيه.</summary>
    public static TenantId AdvanceDisabledTenant { get; } = new(new Guid("5a1e5a1e-0000-4000-8000-000000000005"));

    /// <summary>كل منشآت هذه المجموعة — مُعلنةً مرّة، فلا تُنسى واحدة عند التأسيس.</summary>
    public static TenantId[] AllTenants { get; } =
        [Tenant, InjectedTenant, GatewayTenant, AdvanceEnabledTenant, AdvanceDisabledTenant];

    /// <summary>عدد محاولات الحذف قبل اللجوء إلى الإنهاء القسري.</summary>
    private const int DropAttempts = 40;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;
    private static Exception? _failure;
    private static int _cleanupRegistered;

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

            // فشلٌ جزئي واحد يكفي: لا تُعاد التهيئة أبداً. إعادتها تعني إعادة الإنشاء
            // على قاعدة نصف مبنيّة، وذلك طريق تعافٍ **مُدمِّر**. الفشل يبقى مرفوعاً
            // بصوته الأصلي في كل نداء تالٍ، ولا يُترجَم إلى إسقاط أي شيء.
            if (_failure is not null)
            {
                throw new InvalidOperationException(
                    "فشلت تهيئة بيئة الاختبار مرّة واحدة في هذه العملية، ولن يُعاد بناؤها: "
                    + "إعادة البناء تبدأ بإسقاط قواعد قد تكون نصف مبنيّة أو قيد الاستعمال. "
                    + "السبب الأصلي مرفق.",
                    _failure);
            }

            try
            {
                // يُسجَّل الحذف **قبل** الإنشاء: تشغيل ينهار في منتصف التهيئة يترك
                // قاعدة نصف مبنيّة، وهذه القاعدة تُحذف أيضاً عند خروج العملية.
                RegisterCleanup();

                await CreateAsync(cancellationToken).ConfigureAwait(false);
                await DeployLedgerAsync(cancellationToken).ConfigureAwait(false);
                await SeedAsync(cancellationToken).ConfigureAwait(false);
                await SalesSchemaDeployer.DeployAsync(Sales, cancellationToken).ConfigureAwait(false);
                _ready = true;
            }
            catch (Exception failure)
            {
                _failure = failure;
                throw;
            }
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

        // كنس المتروك من تشغيلات **ماتت**: لا إسقاط عند البدء لقاعدة أحدٌ فيها.
        await SweepAbandonedAsync(admin, cancellationToken).ConfigureAwait(false);

        // ولا إسقاط هنا إطلاقاً: الاسم خاصّ بهذه العملية ولم يوجد قبلها. فإن وُجد
        // فذلك خلل حقيقي يُرفع بصوته (‏42P04)، لا يُبتلع بإسقاط قاعدة غريبة.
        await ExecAsync(admin, $"create database {ModuleDatabase}", cancellationToken).ConfigureAwait(false);
        await ExecAsync(admin, $"create database {LedgerDatabase}", cancellationToken).ConfigureAwait(false);

        // الدور التطبيقي: يدخل، ولا يملك شيئاً، وليس superuser — الطبقة الأولى (فخ-30).
        //
        // والاسم مشترك بين العمليات، فإنشاؤه يتسابق. وقُيس على هذا الجهاز أن الكتلة
        // بلا قفل لا تكفي: ثماني عمليات متزامنة تُنشئ الدور نفسه أخفقت واحدةً في كل
        // جولة من ثلاث جولات، مرّة بـ‏23505 على pg_authid_rolname_index (لا 42710،
        // فلا يلتقطها duplicate_object) ومرّة بـ‏XX000 «tuple concurrently updated»
        // من alter role في مسار الاستثناء. فالقفل الاستشاري على اسم الدور يُسلسل
        // الإنشاء عبر العمليات — والكتلة $$ معاملة واحدة، فالقفل يُفكّ بإيداعها.
        // وبعد القفل: ثلاث جولات × ثماني عمليات = 24 عملية، صفر إخفاق.
        await ExecAsync(
            admin,
            $"""
            do $$
            begin
                perform pg_advisory_xact_lock(hashtextextended('{AppRole}', 0));
                begin
                    create role {AppRole} login nosuperuser nocreatedb nocreaterole noinherit;
                exception when duplicate_object or unique_violation then
                    alter role {AppRole} login nosuperuser nocreatedb nocreaterole noinherit;
                end;
            end
            $$;
            """,
            cancellationToken).ConfigureAwait(false);

        await ExecAsync(admin, $"grant connect on database {LedgerDatabase} to {AppRole}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يُسجّل حذف قواعد هذه العملية عند خروجها — لا عند بدئها.
    /// <para>
    /// <b>الحذف عند البدء هو العطل نفسه:</b> هو افتراضٌ صامت بأن لا أحد غيرك يعمل
    /// الآن. أمّا الحذف عند الخروج فيُصفّي ما تملكه أنت وحدك.
    /// </para>
    /// <para>
    /// <b>وماذا لو قُتل التشغيل؟</b> ‏<c>ProcessExit</c> يعمل عند الخروج الطبيعي
    /// وعند الفشل وعند <c>SIGTERM</c>. أمّا <c>SIGKILL</c> فلا يترك للعملية أي
    /// فرصة، ولذلك يُكنس المتروك في بداية التشغيل التالي — بشرط أن تكون العملية
    /// المالكة قد <b>ماتت</b>، وبإسقاط غير قسري.
    /// </para>
    /// </summary>
    private static void RegisterCleanup()
    {
        if (Interlocked.Exchange(ref _cleanupRegistered, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DropOwnDatabases();
    }

    private static void DropOwnDatabases()
    {
        try
        {
            NpgsqlConnection.ClearAllPools();

            using NpgsqlConnection admin = new(Maintenance);
            admin.Open();
            DropOne(admin, ModuleDatabase);
            DropOne(admin, LedgerDatabase);
        }
        catch (NpgsqlException exception)
        {
            // الخروج لا يُفشَل بسببه، لكنه لا يمرّ صامتاً: قاعدة متروكة خبرٌ يُقال.
            Console.WriteLine("        تعذّر حذف قواعد هذا التشغيل: " + exception.Message);
        }
    }

    private static void DropOne(NpgsqlConnection admin, string database)
    {
        // تُقطع اتصالات هذه العملية **قبل** أول محاولة، لا بعد فشلها: ‏PostgreSQL
        // ينتظر خمس ثوانٍ كاملة قبل أن يعلن «القاعدة مستعملة» — مقيس على هذا الجهاز
        // 5061 مِلّي ثانية — فالمحاولة الفاشلة وحدها تكلّف خمس ثوانٍ في كل تشغيل.
        //
        // والقطع هنا لا يمسّ أحداً: الاسم خاصّ بهذه العملية، والجلسات عليه جلساتها.
        // وهذا هو الفرق كلّه عن `with (force)` على اسم ثابت، الذي كان يقطع جلسات
        // عملية أخرى تعمل.
        TerminateOwnSessions(admin, database);

        for (int attempt = 1; ; attempt++)
        {
            try
            {
                using NpgsqlCommand command = new($"drop database if exists {database}", admin);
                command.ExecuteNonQuery();
                return;
            }
            catch (PostgresException exception)
                when (exception.SqlState == PostgresErrorCodes.ObjectInUse && attempt < DropAttempts)
            {
                // جلسة تأخّرت في الموت: تُعاد المحاولة قصيراً بدل الانتظار الطويل.
                TerminateOwnSessions(admin, database);
                Thread.Sleep(25);
            }
            catch (PostgresException exception) when (exception.SqlState == PostgresErrorCodes.ObjectInUse)
            {
                using NpgsqlCommand forced = new($"drop database if exists {database} with (force)", admin);
                forced.ExecuteNonQuery();
                return;
            }
        }
    }

    private static void TerminateOwnSessions(NpgsqlConnection admin, string database)
    {
        using NpgsqlCommand command = new(
            "select pg_terminate_backend(pid) from pg_stat_activity where datname = $1 and pid <> pg_backend_pid()",
            admin);
        command.Parameters.AddWithValue(database);
        command.ExecuteNonQuery();
    }

    /// <summary>
    /// يحذف قواعد تشغيلات سابقة قُتلت قبل أن تُصفّي نفسها — ولا يمسّ قاعدة عمليةٍ
    /// حيّة أبداً. وعند الشكّ في حياة المالك، القاعدة <b>تُترك</b>: تسريب قاعدة
    /// خبرٌ سيّئ، وحذف قاعدة تحت اختبارٍ جارٍ عطلٌ يوم كامل.
    /// </summary>
    private static async Task SweepAbandonedAsync(NpgsqlConnection admin, CancellationToken cancellationToken)
    {
        foreach (string stem in new[] { ModuleDatabaseStem, LedgerDatabaseStem, UpgradeProbeDatabaseStem })
        {
            List<string> candidates = [];

            await using (NpgsqlCommand query = new(
                "select datname from pg_database where datname like $1", admin))
            {
                query.Parameters.AddWithValue(stem + "_p%");
                await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken)
                    .ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    candidates.Add(reader.GetString(0));
                }
            }

            foreach (string database in candidates)
            {
                int? owner = TestRunScope.OwnerProcessId(database, stem);
                if (owner is null || TestRunScope.OwnerIsAlive(owner.Value))
                {
                    continue;
                }

                try
                {
                    // بلا (force): إن كان عليها اتصال حيّ فالمالك لم يمت حقاً، فتُترك.
                    await ExecAsync(admin, $"drop database if exists {database}", cancellationToken)
                        .ConfigureAwait(false);
                    Console.WriteLine("        كُنست قاعدة متروكة من تشغيل ميت: " + database);
                }
                catch (PostgresException exception)
                {
                    Console.WriteLine(
                        "        تُركت قاعدة متروكة كما هي (" + exception.SqlState + "): " + database);
                }
            }
        }
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

    /// <summary>
    /// يكتب ترجمة اسم صفّاً في <c>ledger.name_translation</c> (ADR-0021): مصدر التأليف
    /// ما زال ملف CSV بعموده الإنجليزي، والمخطّط لم يعد يعرف عموداً — والتحويل هنا.
    /// والنصّ الفارغ لا يُكتب: غياب الترجمة صفٌّ غائب يرتدّ العرض عنده إلى العربية.
    /// </summary>
    private static async Task TranslateAsync(
        NpgsqlConnection owner,
        Guid company,
        string kind,
        string key,
        string languageTag,
        string? name,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        await using NpgsqlCommand command = new(
            """
            insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
            values ($1,$2,$3,$4,$5) on conflict do nothing
            """, owner);
        command.Parameters.AddWithValue(company);
        command.Parameters.AddWithValue(kind);
        command.Parameters.AddWithValue(key);
        command.Parameters.AddWithValue(languageTag);
        command.Parameters.AddWithValue(name.Trim());
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
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
                    (role_code, name_ar, expected_account_type, expected_side, status, note_ar, note_en)
                values ($1,$2,$3,$4,$5,$6,$7) on conflict do nothing
                """, owner);
            command.Parameters.AddWithValue(row["role_code"]);
            command.Parameters.AddWithValue(row["name_ar"]);
            command.Parameters.AddWithValue(Null(row["expected_account_type"]));
            command.Parameters.AddWithValue(Null(row["expected_side"]));
            command.Parameters.AddWithValue(row["status"]);
            command.Parameters.AddWithValue(Null(row["note_ar"]));
            command.Parameters.AddWithValue(Null(row["note_en"]));
            await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

            await TranslateAsync(
                owner, Guid.Empty, "posting_role", row["role_code"], "en", row["name_en"], cancellationToken)
                .ConfigureAwait(false);
        }

        List<Dictionary<string, string>> accounts =
            [.. Csv(Path.Combine(RepositoryRoot, "data", "chart-of-accounts", "accounts.csv"))];

        foreach (TenantId company in AllTenants)
        {
            foreach (Dictionary<string, string> row in accounts
                         .OrderBy(static a => a["code"].Length).ThenBy(static a => a["code"], StringComparer.Ordinal))
            {
                await using NpgsqlCommand command = new(
                    """
                    insert into ledger.account
                        (company_id, account_code, name_ar, name_ar_search, parent_code, account_level,
                         account_type, natural_side, is_postable, is_contra, statement_section, subledger_type,
                         required_dimensions, currency_mode, currency_code, is_protected, is_active, status,
                         source_ref, caveat_ar, caveat_en)
                    values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,true,$17,$18,$19,$20)
                    """, owner);
                command.Parameters.AddWithValue(company.Value);
                command.Parameters.AddWithValue(row["code"]);
                command.Parameters.AddWithValue(row["name_ar"]);
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

                await TranslateAsync(
                    owner, company.Value, "account", row["code"], "en", row["name_en"], cancellationToken).ConfigureAwait(false);
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
                        (company_id, fiscal_year, period_no, period_code, starts_on, ends_on, state, name_ar)
                    values ($1,$2,$3,$4,$5,$6,$7,$8)
                    """, owner);
                command.Parameters.AddWithValue(company.Value);
                command.Parameters.AddWithValue(FiscalYear);
                command.Parameters.AddWithValue(month);
                command.Parameters.AddWithValue(code);
                command.Parameters.AddWithValue(start);
                command.Parameters.AddWithValue(start.AddMonths(1).AddDays(-1));
                command.Parameters.AddWithValue(state);
                command.Parameters.AddWithValue("الفترة " + code);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                await TranslateAsync(
                    owner, company.Value, "fiscal_period", code, "en", "Period " + code, cancellationToken).ConfigureAwait(false);
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
