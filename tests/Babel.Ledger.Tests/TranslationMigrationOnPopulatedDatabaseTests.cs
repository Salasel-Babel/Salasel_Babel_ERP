using System.Globalization;
using Babel.Ledger.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>الهجرة على قاعدة بيانات عامرة، لا على قاعدة فارغة.</b>
/// <para>
/// هجرةٌ تُختبَر على مخطّط فارغ تُثبت أنها تُنتج الشكل الجديد، ولا تُثبت شيئاً عن
/// <b>البيانات القائمة</b> — وهي وحدها ما يُفقَد. ولذلك تبني كل حالة هنا قاعدةً
/// بالمخطّط <b>السابق</b> للهجرة، وتكتب فيها صفوفاً بأسماء إنجليزية، ثم تُشغّل
/// الهجرة وحدها، ثم تقرأ من <c>pg_catalog</c> و<c>pg_constraint</c> — لا من نموذج
/// EF الذي يصف ما <b>ينبغي</b> أن يكون.
/// </para>
/// <para>
/// <b>وما ولّدته الأداة كان يُسقط الأعمدة قبل أن يُنشئ الجدول.</b> أي أن الترتيب
/// نفسه هو الهجرة: هذه الاختبارات هي ما يمنع عودته.
/// </para>
/// </summary>
[Collection("ledger-migration")]
public sealed class TranslationMigrationOnPopulatedDatabaseTests
{
    /// <summary>الهجرة السابقة لهذه — نقطة البدء التي تُبنى عندها القاعدة العامرة.</summary>
    private const string Previous = "20260824173958_PostingIdentityIncludesEventCode";

    /// <summary>الهجرة قيد الإثبات.</summary>
    private const string Target = "20260825201239_TranslationsAreRowsNotColumns";

    private static readonly Guid CompanyOne = new("11111111-0000-4000-8000-000000000001");
    private static readonly Guid CompanyTwo = new("22222222-0000-4000-8000-000000000002");

    [Fact]
    public async Task الهجرة_تنقل_كل_اسم_إنجليزي_ولا_تفقد_صفّاً_واحداً()
    {
        await using MigrationSandbox sandbox = await MigrationSandbox.OpenAsync(Previous);

        await SeedAsync(sandbox);

        long accountsBefore = await sandbox.ScalarAsync("select count(*) from ledger.account");
        long rolesBefore = await sandbox.ScalarAsync("select count(*) from ledger.posting_role");
        long propertiesBefore = await sandbox.ScalarAsync("select count(*) from ledger.property_dimension");
        long periodsBefore = await sandbox.ScalarAsync("select count(*) from ledger.fiscal_period");
        long namedBefore = await sandbox.ScalarAsync(
            """
            select (select count(*) from ledger.account            where btrim(name_en) <> '')
                 + (select count(*) from ledger.posting_role       where btrim(name_en) <> '')
                 + (select count(*) from ledger.property_dimension where btrim(name_en) <> '')
                 + (select count(*) from ledger.fiscal_period      where btrim(name_en) <> '')
            """);

        Proof.Note("قبل الهجرة — حسابات: " + accountsBefore + "، أدوار: " + rolesBefore
            + "، عقارات: " + propertiesBefore + "، فترات: " + periodsBefore
            + "، أسماء إنجليزية غير فارغة: " + namedBefore);

        await sandbox.MigrateToAsync(Target);

        // ── ١) لا صفّ كيان ضاع ────────────────────────────────────────────
        Proof.Require(
            await sandbox.ScalarAsync("select count(*) from ledger.account") == accountsBefore,
            "صفوف الحسابات محفوظة", accountsBefore.ToString(CultureInfo.InvariantCulture));
        Proof.Require(
            await sandbox.ScalarAsync("select count(*) from ledger.posting_role") == rolesBefore,
            "صفوف الأدوار محفوظة", rolesBefore.ToString(CultureInfo.InvariantCulture));
        Proof.Require(
            await sandbox.ScalarAsync("select count(*) from ledger.property_dimension") == propertiesBefore,
            "صفوف العقارات محفوظة", propertiesBefore.ToString(CultureInfo.InvariantCulture));
        Proof.Require(
            await sandbox.ScalarAsync("select count(*) from ledger.fiscal_period") == periodsBefore,
            "صفوف الفترات محفوظة", periodsBefore.ToString(CultureInfo.InvariantCulture));

        // ── ٢) ولا اسم إنجليزي ضاع ────────────────────────────────────────
        long moved = await sandbox.ScalarAsync(
            "select count(*) from ledger.name_translation where language_tag = 'en'");

        Proof.Require(
            moved == namedBefore,
            "كل اسم إنجليزي غير فارغ صار صفّاً في جدول الترجمات",
            "المتوقَّع " + namedBefore + " والمنقول " + moved);

        // ── ٣) والقيمة نفسها لا عددها فقط ─────────────────────────────────
        string cash = await sandbox.TextAsync(
            """
            select name from ledger.name_translation
             where company_id = $1 and entity_kind = 'account' and entity_key = '1101' and language_tag = 'en'
            """, CompanyOne);

        Proof.Require(cash == "Cash on hand", "قيمة الاسم المنقول مطابقة", cash);

        string role = await sandbox.TextAsync(
            """
            select name from ledger.name_translation
             where entity_kind = 'posting_role' and entity_key = 'cash' and language_tag = 'en'
            """);

        Proof.Require(role == "Cash", "اسم الدور المنقول مطابق", role);

        // ── ٤) والفارغ لم يُنقل صفّاً فارغاً ──────────────────────────────
        long blank = await sandbox.ScalarAsync(
            """
            select count(*) from ledger.name_translation
             where company_id = $1 and entity_kind = 'account' and entity_key = '1102'
            """, CompanyOne);

        Proof.Require(blank == 0, "الاسم الإنجليزي الفارغ لم يُنقل صفّاً فارغاً", "عدد صفوفه " + blank);

        // ── ٥) وشركتان لا تختلطان ─────────────────────────────────────────
        string other = await sandbox.TextAsync(
            """
            select name from ledger.name_translation
             where company_id = $1 and entity_kind = 'account' and entity_key = '1101' and language_tag = 'en'
            """, CompanyTwo);

        Proof.Require(other == "Petty cash", "ترجمة الشركة الثانية مستقلّة عن الأولى", other);
    }

    [Fact]
    public async Task الشكل_الجديد_حيّ_ويُقرأ_من_pg_catalog_لا_من_نموذج_EF()
    {
        await using MigrationSandbox sandbox = await MigrationSandbox.OpenAsync(Previous);
        await SeedAsync(sandbox);
        await sandbox.MigrateToAsync(Target);

        // ── الأعمدة القديمة سقطت فعلاً ────────────────────────────────────
        long survivors = await sandbox.ScalarAsync(
            """
            select count(*) from pg_catalog.pg_attribute a
              join pg_catalog.pg_class c on c.oid = a.attrelid
              join pg_catalog.pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'ledger' and a.attname = 'name_en' and a.attnum > 0 and not a.attisdropped
               and c.relname in ('account','posting_role','property_dimension','fiscal_period')
            """);

        Proof.Require(survivors == 0, "لا عمود name_en باقياً في أي كيان مُسمّى", "عددها " + survivors);

        // ── والعربي باقٍ عموداً not null ─────────────────────────────────
        long arabic = await sandbox.ScalarAsync(
            """
            select count(*) from pg_catalog.pg_attribute a
              join pg_catalog.pg_class c on c.oid = a.attrelid
              join pg_catalog.pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'ledger' and a.attname = 'name_ar' and a.attnotnull
               and c.relname in ('account','posting_role','property_dimension','fiscal_period')
            """);

        Proof.Require(arabic == 4, "الاسم العربي عمود not null على الكيانات الأربعة", "عددها " + arabic);

        // ── والجدول الجديد قائم بمفتاحه ──────────────────────────────────
        string key = await sandbox.TextAsync(
            """
            select pg_catalog.pg_get_constraintdef(oid) from pg_catalog.pg_constraint
             where conname = 'pk_name_translation'
            """);

        Proof.Require(
            key.Contains("company_id", StringComparison.Ordinal)
                && key.Contains("entity_kind", StringComparison.Ordinal)
                && key.Contains("entity_key", StringComparison.Ordinal)
                && key.Contains("language_tag", StringComparison.Ordinal),
            "مفتاح الترجمات هو (كيان × لغة) كما ينصّ القرار", key);

        // ── وقيود التحقّق موجودة بأسمائها لا بوصفها ──────────────────────
        foreach (string constraint in new[]
                 {
                     "ck_account_name_ar_not_blank",
                     "ck_posting_role_name_ar_not_blank",
                     "ck_property_name_ar_not_blank",
                     "ck_fiscal_period_name_ar_not_blank",
                     "ck_name_translation_kind",
                     "ck_name_translation_scope",
                     "ck_name_translation_not_arabic",
                     "ck_name_translation_tag_shape",
                     "ck_name_translation_name_not_blank",
                 })
        {
            string definition = await sandbox.TextAsync(
                "select pg_catalog.pg_get_constraintdef(oid) from pg_catalog.pg_constraint where conname = $1",
                constraint);

            Proof.Require(definition.Length > 0, "القيد « " + constraint + " » حيٌّ في المخطّط", definition);
        }
    }

    [Fact]
    public async Task حارس_الاسم_العربي_يعضّ_فعلاً_بعد_الهجرة()
    {
        // الحارس غير الضامر: صفٌّ بحرف عربي واحد يمرّ، وصفٌّ بمسافات وحدها يُرفض —
        // فالفحص يميّز فعلاً ولا يقبل كل شيء ولا يرفض كل شيء.
        await using MigrationSandbox sandbox = await MigrationSandbox.OpenAsync(Previous);
        await SeedAsync(sandbox);
        await sandbox.MigrateToAsync(Target);

        await sandbox.ExecuteAsync(
            """
            insert into ledger.account
                (company_id, account_code, name_ar, name_ar_search, parent_code, account_level, account_type,
                 natural_side, is_postable, is_contra, subledger_type, required_dimensions,
                 currency_mode, is_protected, is_active, status)
            values ($1, '1103', 'ص', '', '110', 4, 'asset', 'debit', true, false, 'none', '{}', 'any', false, true, 'drafted')
            """, CompanyOne);

        Proof.Pass("اسم عربي من حرف واحد مقبول", "1103");

        PostgresException blank = await Assert.ThrowsAsync<PostgresException>(
            () => sandbox.ExecuteAsync(
                """
                insert into ledger.account
                    (company_id, account_code, name_ar, name_ar_search, parent_code, account_level, account_type,
                     natural_side, is_postable, is_contra, subledger_type, required_dimensions,
                     currency_mode, is_protected, is_active, status)
                values ($1, '1104', '   ', '', '110', 4, 'asset', 'debit', true, false, 'none', '{}', 'any', false, true, 'drafted')
                """, CompanyOne));

        Proof.Require(
            blank.SqlState == "23514" && blank.ConstraintName == "ck_account_name_ar_not_blank",
            "الاسم العربي الفارغ ترفضه قاعدة البيانات لا الشيفرة",
            blank.SqlState + " · " + blank.ConstraintName);
    }

    [Fact]
    public async Task جدول_الترجمات_يرفض_العربية_والوسم_المشوَّه_والنصّ_الفارغ()
    {
        await using MigrationSandbox sandbox = await MigrationSandbox.OpenAsync(Previous);
        await SeedAsync(sandbox);
        await sandbox.MigrateToAsync(Target);

        // شاهدٌ موجب أولاً: اللغة الخامسة تدخل بلا هجرة ولا عمود ولا إصدار.
        foreach ((string tag, string name) in new[]
                 {
                     ("ur", "نقدی"), ("hi", "नकद"), ("am", "ጥሬ ገንዘብ"), ("tl", "Salapi"),
                 })
        {
            await sandbox.ExecuteAsync(
                """
                insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                values ($1, 'account', '1101', $2, $3)
                """, CompanyOne, tag, name);
        }

        long languages = await sandbox.ScalarAsync(
            """
            select count(*) from ledger.name_translation
             where company_id = $1 and entity_kind = 'account' and entity_key = '1101'
            """, CompanyOne);

        Proof.Require(languages == 5, "خمس لغات على حساب واحد بلا هجرة مخطّط", "عددها " + languages);

        await RefusedAsync(sandbox, "ar", "اسم عربي ثانٍ", "ck_name_translation_not_arabic");
        await RefusedAsync(sandbox, "AR-sa", "اسم عربي ثانٍ", "ck_name_translation_not_arabic");
        await RefusedAsync(sandbox, "en_GB", "Sales", "ck_name_translation_tag_shape");
        await RefusedAsync(sandbox, "1en", "Sales", "ck_name_translation_tag_shape");
        await RefusedAsync(sandbox, "fr", "   ", "ck_name_translation_name_not_blank");

        // والنطاق: دورٌ مملوك لشركة، أو كيانُ شركةٍ بلا شركة — كلاهما مرفوض.
        PostgresException scoped = await Assert.ThrowsAsync<PostgresException>(
            () => sandbox.ExecuteAsync(
                """
                insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                values ($1, 'posting_role', 'cash', 'fr', 'Caisse')
                """, CompanyOne));

        Proof.Require(
            scoped.ConstraintName == "ck_name_translation_scope",
            "دورٌ محاسبي مملوك لشركة مرفوض — الأدوار عامّة",
            scoped.ConstraintName ?? string.Empty);

        PostgresException global = await Assert.ThrowsAsync<PostgresException>(
            () => sandbox.ExecuteAsync(
                """
                insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                values ('00000000-0000-0000-0000-000000000000'::uuid, 'account', '1101', 'fr', 'Caisse')
                """));

        Proof.Require(
            global.ConstraintName == "ck_name_translation_scope",
            "حسابٌ بلا شركة مرفوض — الحسابات مملوكة",
            global.ConstraintName ?? string.Empty);
    }

    [Fact]
    public async Task التراجع_يُعيد_الإنجليزية_إلى_عمودها_ويُعلن_ما_يفقده()
    {
        await using MigrationSandbox sandbox = await MigrationSandbox.OpenAsync(Previous);
        await SeedAsync(sandbox);
        await sandbox.MigrateToAsync(Target);

        await sandbox.ExecuteAsync(
            """
            insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
            values ($1, 'account', '1101', 'ur', 'نقدی')
            """, CompanyOne);

        await sandbox.MigrateToAsync(Previous);

        string restored = await sandbox.TextAsync(
            "select name_en from ledger.account where company_id = $1 and account_code = '1101'",
            CompanyOne);

        Proof.Require(restored == "Cash on hand", "التراجع أعاد الإنجليزية إلى عمودها", restored);

        long table = await sandbox.ScalarAsync(
            """
            select count(*) from pg_catalog.pg_class c
              join pg_catalog.pg_namespace n on n.oid = c.relnamespace
             where n.nspname = 'ledger' and c.relname = 'name_translation'
            """);

        Proof.Require(table == 0, "جدول الترجمات سقط مع التراجع", "عدده " + table);
        Proof.Note(
            "والأردية فُقدت بالتراجع، ولا موضع لها في المخطّط القديم — وهو الفقد "
            + "المُعلَن في وثيقة الهجرة، وهو نصّ المشكلة التي جاءت الهجرة لتحلّها.");
    }

    private static async Task RefusedAsync(MigrationSandbox sandbox, string tag, string name, string constraint)
    {
        PostgresException refusal = await Assert.ThrowsAsync<PostgresException>(
            () => sandbox.ExecuteAsync(
                """
                insert into ledger.name_translation (company_id, entity_kind, entity_key, language_tag, name)
                values ($1, 'account', '1101', $2, $3)
                """, CompanyOne, tag, name));

        Proof.Require(
            refusal.ConstraintName == constraint,
            "الوسم « " + tag + " » مرفوض بـ" + constraint,
            refusal.ConstraintName ?? refusal.SqlState);
    }

    /// <summary>يكتب صفوفاً بالمخطّط <b>القديم</b> — بعمود <c>name_en</c> الذي ستُسقطه الهجرة.</summary>
    private static async Task SeedAsync(MigrationSandbox sandbox)
    {
        await sandbox.ExecuteAsync(
            """
            insert into ledger.posting_role (role_code, name_ar, name_en, status) values
                ('cash', 'الصندوق', 'Cash', 'drafted'),
                ('bank', 'البنك', 'Bank', 'drafted'),
                ('ar_control', 'ذمم العملاء', '', 'drafted')
            """);

        foreach (Guid company in new[] { CompanyOne, CompanyTwo })
        {
            string cash = company == CompanyOne ? "Cash on hand" : "Petty cash";

            await sandbox.ExecuteAsync(
                """
                insert into ledger.account
                    (company_id, account_code, name_ar, name_en, name_ar_search, parent_code, account_level,
                     account_type, natural_side, is_postable, is_contra, subledger_type, required_dimensions,
                     currency_mode, is_protected, is_active, status)
                values
                    ($1, '1',    'الأصول',     'Assets', '', null, 1, 'asset', 'debit', false, false, 'none', '{}', 'any', false, true, 'drafted'),
                    ($1, '11',   'المتداولة',  'Current', '', '1', 2, 'asset', 'debit', false, false, 'none', '{}', 'any', false, true, 'drafted'),
                    ($1, '110',  'النقدية',    'Liquidity', '', '11', 3, 'asset', 'debit', false, false, 'none', '{}', 'any', false, true, 'drafted'),
                    ($1, '1101', 'الصندوق',    $2, '', '110', 4, 'asset', 'debit', true, false, 'none', '{}', 'any', false, true, 'drafted'),
                    ($1, '1102', 'البنك',      '', '', '110', 4, 'asset', 'debit', true, false, 'none', '{}', 'any', false, true, 'drafted')
                """, company, cash);

            await sandbox.ExecuteAsync(
                """
                insert into ledger.property_dimension (company_id, property_id, ownership_model, name_ar, name_en)
                values ($1, 'P-001', 'own_property', 'برج السلام', 'Al Salam Tower')
                """, company);

            await sandbox.ExecuteAsync(
                """
                insert into ledger.fiscal_period
                    (company_id, fiscal_year, period_no, period_code, starts_on, ends_on, state, name_ar, name_en)
                values ($1, 2026, 8, '2026-08', date '2026-08-01', date '2026-08-31', 'open', 'أغسطس 2026', 'August 2026')
                """, company);
        }
    }
}

/// <summary>
/// قاعدة بيانات خاصّة بحالة اختبار واحدة، تُنشأ بمخطّط هجرةٍ بعينها وتُحذف بعدها.
/// كل حالة تبني ما تفحصه بنفسها (CONTRIBUTING §3 بند 8) — ولا واحدة منها تقرأ ما كتبته أخرى.
/// </summary>
internal sealed class MigrationSandbox : IAsyncDisposable
{
    private static int _counter;

    private readonly string _database;
    private readonly string _connectionString;

    private MigrationSandbox(string database, string connectionString)
    {
        _database = database;
        _connectionString = connectionString;
    }

    public static async Task<MigrationSandbox> OpenAsync(string startAt)
    {
        int ordinal = Interlocked.Increment(ref _counter);
        string database = TestRunScope.Name("babel_mig_" + ordinal.ToString(CultureInfo.InvariantCulture));

        await using (NpgsqlConnection admin = new(LedgerTestEnvironment.Maintenance))
        {
            await admin.OpenAsync().ConfigureAwait(false);
            await using NpgsqlCommand create = new("create database " + database, admin);
            await create.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        string connectionString =
            $"Host=127.0.0.1;Port=5432;Database={database};Username=postgres;Include Error Detail=true";

        MigrationSandbox sandbox = new(database, connectionString);
        await sandbox.MigrateToAsync(startAt).ConfigureAwait(false);
        return sandbox;
    }

    /// <summary>
    /// يُشغّل الهجرات حتى هدف بعينه — صعوداً أو نزولاً. وهذا هو الفرق بين إثبات
    /// «الشكل الجديد يُنتَج» وإثبات «البيانات القائمة تعبر».
    /// </summary>
    public async Task MigrateToAsync(string migration)
    {
        DbContextOptionsBuilder<LedgerDbContext> builder = new();
        builder.UseNpgsql(_connectionString);

        await using LedgerDbContext context = new(builder.Options);
        IMigrator migrator = context.GetInfrastructure().GetRequiredService<IMigrator>();
        await migrator.MigrateAsync(migration).ConfigureAwait(false);
    }

    public async Task ExecuteAsync(string sql, params object[] parameters)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = new(sql, connection);

        foreach (object parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        await command.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    public async Task<long> ScalarAsync(string sql, params object[] parameters)
    {
        object? value = await ReadAsync(sql, parameters).ConfigureAwait(false);
        return value is null or DBNull ? 0 : Convert.ToInt64(value, CultureInfo.InvariantCulture);
    }

    public async Task<string> TextAsync(string sql, params object[] parameters)
    {
        object? value = await ReadAsync(sql, parameters).ConfigureAwait(false);
        return value is null or DBNull ? string.Empty : (string)value;
    }

    public async ValueTask DisposeAsync()
    {
        NpgsqlConnection.ClearAllPools();

        await using NpgsqlConnection admin = new(LedgerTestEnvironment.Maintenance);
        await admin.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand drop = new(
            "drop database if exists " + _database + " with (force)", admin);
        await drop.ExecuteNonQueryAsync().ConfigureAwait(false);
    }

    private async Task<object?> ReadAsync(string sql, object[] parameters)
    {
        await using NpgsqlConnection connection = new(_connectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlCommand command = new(sql, connection);

        foreach (object parameter in parameters)
        {
            command.Parameters.AddWithValue(parameter);
        }

        return await command.ExecuteScalarAsync().ConfigureAwait(false);
    }
}
