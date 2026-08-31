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
    /// <summary>الجذع الثابت لاسم قاعدة الاختبار — تُلحق به لاحقة هذه العملية.</summary>
    public const string DatabaseStem = "babel_ledger_tests";

    /// <summary>
    /// قاعدة الاختبار <b>لهذه العملية وحدها</b>.
    /// <para>
    /// الاسم كان ثابتاً، وكانت التهيئة تُنفّذ <c>drop schema ledger cascade</c> عليه
    /// عند البدء. فعمليتان متزامنتان تسحب كلٌّ منهما المخطّط من تحت اختبارات
    /// الأخرى. مقيس على هذا الجهاز على الشيفرة قبل هذا الإصلاح: تشغيل تجميعة
    /// الحل كاملةً أنتج <b>13 حالة</b> <c>42P01: relation … does not exist</c> في
    /// هذه المجموعة، ثم مرّت 62 من 62 حين شُغّلت وحدها بعد ذلك مباشرةً.
    /// </para>
    /// </summary>
    public static string Database { get; } = TestRunScope.Name(DatabaseStem);

    /// <summary>
    /// دور التطبيق — اسمه <b>مشترك عمداً</b>: الأدوار عامّة على مستوى العنقود، ولا
    /// يملك أيٌّ منها كائناً ولا يحذفها أحد، فلا شيء فيها يُدمَّر. الشيء الوحيد الذي
    /// كان يتسابق عليه هو <b>إنشاؤه</b> (‏42710)، وقد صار الإنشاء محصَّناً أدناه.
    /// </summary>
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

    /// <summary>عدد محاولات الحذف قبل اللجوء إلى الإنهاء القسري.</summary>
    private const int DropAttempts = 40;

    private static readonly SemaphoreSlim Gate = new(1, 1);
    private static bool _ready;
    private static Exception? _failure;
    private static int _cleanupRegistered;

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
        // لا تجاوز من البيئة على هذين: متغيّرٌ يحمل اسماً ثابتاً يُبطل الاسم الخاصّ
        // بالعملية بصمت، فيعود العطل كاملاً بينما الشيفرة تبدو مُصلَحة. المتغيّر
        // الوحيد الباقي هو اتصال الصيانة، وهو لا يسمّي قاعدة الاختبار أصلاً.
        OwnerConnectionString =
            $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true",
        AppConnectionString =
            $"Host=127.0.0.1;Port=5432;Database={Database};Username={AppRole};Include Error Detail=true;Maximum Pool Size=40",
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

            // فشلٌ جزئي واحد يكفي: لا تُعاد التهيئة أبداً. إعادتها تعني إعادة البناء
            // على قاعدة نصف مبنيّة، وذلك طريق تعافٍ **مُدمِّر**. الفشل يبقى مرفوعاً
            // بصوته الأصلي في كل نداء تالٍ.
            if (_failure is not null)
            {
                throw new InvalidOperationException(
                    "فشلت تهيئة بيئة الاختبار مرّة واحدة في هذه العملية، ولن يُعاد بناؤها: "
                    + "إعادة البناء تبدأ بإسقاط قاعدة قد تكون نصف مبنيّة أو قيد الاستعمال. "
                    + "السبب الأصلي مرفق.",
                    _failure);
            }

            try
            {
                // يُسجَّل الحذف **قبل** الإنشاء: تشغيل ينهار في منتصف التهيئة يترك
                // قاعدة نصف مبنيّة، وهذه القاعدة تُحذف أيضاً عند خروج العملية.
                RegisterCleanup();

                await CreateDatabaseAndRoleAsync(cancellationToken).ConfigureAwait(false);

                // ولا إعادة ضبط للمخطّط: القاعدة أُنشئت لهذه العملية قبل سطور، فلا
                // مخطّط فيها يُسقَط. و`drop schema … cascade` على اسم ثابت هو الفعل
                // المُدمِّر الذي كان في قلب هذا العطل.
                await LedgerSchemaDeployer.DeployAsync(Options, cancellationToken).ConfigureAwait(false);
                await SeedAsync(cancellationToken).ConfigureAwait(false);
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

    private static async Task CreateDatabaseAndRoleAsync(CancellationToken cancellationToken)
    {
        await using NpgsqlConnection admin = new(Maintenance);
        await admin.OpenAsync(cancellationToken).ConfigureAwait(false);

        // كنس المتروك من تشغيلات **ماتت**: لا إسقاط عند البدء لقاعدة أحدٌ فيها.
        await SweepAbandonedAsync(admin, cancellationToken).ConfigureAwait(false);

        // ولا فحص وجود هنا ولا إسقاط: الاسم خاصّ بهذه العملية ولم يوجد قبلها. فإن
        // وُجد فذلك خلل حقيقي يُرفع بصوته (‏42P04)، لا يُبتلع بتبنّي قاعدة غريبة.
        await ExecAsync(admin, $"create database {Database}", cancellationToken).ConfigureAwait(false);

        // الدور التطبيقي: يدخل، ولا يملك شيئاً، وليس superuser. هذه هي الطبقة
        // الأولى من الحصانة، ومن دون nosuperuser تسقط كل الطبقات (فخ-30).
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

        if (AppPassword.Length > 0)
        {
            await ExecAsync(
                admin,
                $"alter role {AppRole} password '{AppPassword.Replace("'", "''", StringComparison.Ordinal)}'",
                cancellationToken).ConfigureAwait(false);
        }

        await ExecAsync(admin, $"grant connect on database {Database} to {AppRole}", cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يُسجّل حذف قاعدة هذه العملية عند خروجها — لا عند بدئها.
    /// <para>
    /// <b>الحذف عند البدء هو العطل نفسه:</b> هو افتراضٌ صامت بأن لا أحد غيرك يعمل
    /// الآن. أمّا الحذف عند الخروج فيُصفّي ما تملكه أنت وحدك.
    /// </para>
    /// <para>
    /// <b>وماذا لو قُتل التشغيل؟</b> ‏<c>ProcessExit</c> يعمل عند الخروج الطبيعي
    /// وعند الفشل وعند <c>SIGTERM</c>. أمّا <c>SIGKILL</c> فلا يترك للعملية فرصة،
    /// ولذلك يُكنس المتروك في بداية التشغيل التالي — بشرط أن تكون العملية المالكة
    /// قد <b>ماتت</b>، وبإسقاط غير قسري.
    /// </para>
    /// </summary>
    private static void RegisterCleanup()
    {
        if (Interlocked.Exchange(ref _cleanupRegistered, 1) == 1)
        {
            return;
        }

        AppDomain.CurrentDomain.ProcessExit += static (_, _) => DropOwnDatabase();
    }

    private static void DropOwnDatabase()
    {
        try
        {
            NpgsqlConnection.ClearAllPools();

            using NpgsqlConnection admin = new(Maintenance);
            admin.Open();
            DropOne(admin, Database);
        }
        catch (NpgsqlException exception)
        {
            // الخروج لا يُفشَل بسببه، لكنه لا يمرّ صامتاً: قاعدة متروكة خبرٌ يُقال.
            Console.WriteLine("        تعذّر حذف قاعدة هذا التشغيل: " + exception.Message);
        }
    }

    private static void DropOne(NpgsqlConnection admin, string database)
    {
        // تُقطع اتصالات هذه العملية **قبل** أول محاولة، لا بعد فشلها: ‏PostgreSQL
        // ينتظر قبل أن يعلن «القاعدة مستعملة»، فالمحاولة الفاشلة وحدها تكلّف ثوانٍ.
        // والقطع هنا لا يمسّ أحداً: الاسم خاصّ بهذه العملية والجلسات عليه جلساتها.
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
        List<string> candidates = [];

        await using (NpgsqlCommand query = new("select datname from pg_database where datname like $1", admin))
        {
            query.Parameters.AddWithValue(DatabaseStem + "_p%");
            await using NpgsqlDataReader reader = await query.ExecuteReaderAsync(cancellationToken)
                .ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                candidates.Add(reader.GetString(0));
            }
        }

        foreach (string database in candidates)
        {
            int? owner = TestRunScope.OwnerProcessId(database, DatabaseStem);
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

    /// <summary>
    /// البذر <b>بدور المالك</b>: دور التطبيق لا يملك <c>INSERT</c> على أي جدول
    /// مرجعي، وهذا مقصود — دليل الحسابات ليس شيئاً يكتبه مسار الترحيل.
    /// </summary>
    /// <summary>
    /// يكتب ترجمة اسم صفّاً في <c>ledger.name_translation</c>. والنصّ الفارغ لا يُكتب:
    /// غياب الترجمة صفٌّ غائب يرتدّ العرض عنده إلى العربية، لا صفٌّ فارغ يُعرض فراغاً.
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
        await using NpgsqlConnection owner = new(Options.OwnerConnectionString);
        await owner.OpenAsync(cancellationToken).ConfigureAwait(false);

        // ── كتالوج الأدوار (جدول عام، ليس لكل شركة) ──────────────────────
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

            // الاسم الإنجليزي صفٌّ لا عمود (ADR-0021): مصدر التأليف ما زال ملف CSV
            // بعموده، والمخطّط لم يعد يعرف عموداً — والتحويل هنا، عند البذر.
            await TranslateAsync(
                owner, NameTranslationScope.Global, "posting_role", row["role_code"], "en", row["name_en"], cancellationToken)
                .ConfigureAwait(false);
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
                        (company_id, account_code, name_ar, name_ar_search, parent_code, account_level,
                         account_type, natural_side, is_postable, is_contra, statement_section, subledger_type,
                         required_dimensions, currency_mode, currency_code, is_protected, is_active, status,
                         source_ref, caveat_ar, caveat_en)
                    values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14,$15,$16,true,$17,$18,$19,$20)
                    """, owner);
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(row["code"]);
                command.Parameters.AddWithValue(row["name_ar"]);
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

                await TranslateAsync(
                    owner, company, "account", row["code"], "en", row["name_en"], cancellationToken).ConfigureAwait(false);
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
                insert into ledger.property_dimension (company_id, property_id, ownership_model, name_ar)
                values ($1,$2,'own_property','برج الملكية الذاتية'),
                       ($1,$3,'managed_for_others','برج مُدار لصالح الغير')
                """, owner))
            {
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(OwnProperty);
                command.Parameters.AddWithValue(ManagedProperty);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
            }

            await TranslateAsync(owner, company, "property", OwnProperty, "en", "Own Property Tower", cancellationToken)
                .ConfigureAwait(false);
            await TranslateAsync(owner, company, "property", ManagedProperty, "en", "Managed-for-Others Tower", cancellationToken)
                .ConfigureAwait(false);

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
                        (company_id, fiscal_year, period_no, period_code, starts_on, ends_on, state, name_ar)
                    values ($1,$2,$3,$4,$5,$6,$7,$8)
                    """, owner);
                command.Parameters.AddWithValue(company);
                command.Parameters.AddWithValue(FiscalYear);
                command.Parameters.AddWithValue(month);
                command.Parameters.AddWithValue(code);
                command.Parameters.AddWithValue(start);
                command.Parameters.AddWithValue(end);
                command.Parameters.AddWithValue(state);
                command.Parameters.AddWithValue("الفترة " + code);
                await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);

                await TranslateAsync(
                    owner, company, "fiscal_period", code, "en", "Period " + code, cancellationToken).ConfigureAwait(false);
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
