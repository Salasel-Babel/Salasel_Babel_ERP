using System.Globalization;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>قيد مركز التكلفة على قاعدة بيانات عامرة — لا على مخطّط فارغ.</b>
/// <para>
/// هجرةٌ تُختبَر على مخطّط فارغ تُثبت أنها تُنتج الشكل الجديد، ولا تُثبت شيئاً عن
/// <b>البيانات القائمة</b> — وهي وحدها ما يُفقَد. ولذلك تبني كل حالة هنا قاعدةً
/// بالمخطّط <b>السابق</b> للهجرة، وتكتب فيها قيوداً وسطوراً، ثم تُشغّل الهجرة وحدها،
/// ثم تقرأ من <c>pg_constraint</c> و<c>pg_attribute</c> — لا من نموذج EF الذي يصف ما
/// <b>ينبغي</b> أن يكون.
/// </para>
/// <para>
/// <b>والسؤال الحاكم هنا ليس «هل يُضاف القيد؟» بل «ماذا يقع للتاريخ؟».</b> سطر القيد
/// <b>واقعة مُجزَّأة</b>: إعادة التحقق من السلسلة تُعيد بناء البايتات القانونية من هذا
/// الصفّ نفسه، و<c>cost_center</c> حقلٌ فيها في الشكلين v1 وv2 معاً (‏ADR-0007 ·
/// <c>LedgerAuditService</c>). فتعبئةُ عمودٍ فارغ على قيد مُرحَّل تجعل <b>دفتراً سليماً
/// يُبلّغ عن عبث</b>. ولذلك تُقاس هنا <b>بصمة محتوى الجدول قبل الهجرة وبعدها</b>، ويُشترط
/// تطابقها بايتاً بايت — لا «عدد الصفوف كما هو» وحده، فعددٌ ثابت لا ينفي إعادة كتابة.
/// </para>
/// </summary>
[Collection("ledger-migration")]
public sealed class CostCenterMigrationOnPopulatedDatabaseTests
{
    /// <summary>الهجرة السابقة لهذه — نقطة البدء التي تُبنى عندها القاعدة العامرة.</summary>
    private const string Previous = "20260825201239_TranslationsAreRowsNotColumns";

    /// <summary>الهجرة قيد الإثبات.</summary>
    private const string Target = "20260826020745_CostCenterIsNeverAbsentOnAJournalLine";

    private const string Constraint = "ck_journal_line_cost_center_present";

    private static readonly Guid Company = new("c057c051-0000-4000-8000-000000000001");

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · دفتر كل سطوره بمركز: الهجرة تمرّ، والقيد يصير مُصادَقاً على الجدول كلّه
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task دفترٌ_عامر_كل_سطوره_بمركز_يُهاجَر_بلا_فقد_والقيد_يصير_مُصادَقاً()
    {
        await using MigrationSandbox sandbox = await MigrationSandbox.OpenAsync(Previous);

        await SeedAccountsAsync(sandbox);
        await SeedEntryAsync(sandbox, 1, "cc.001", "cc.001");
        await SeedEntryAsync(sandbox, 2, "cc.002", "cc.001");

        long entriesBefore = await sandbox.ScalarAsync("select count(*) from ledger.journal_entry");
        long linesBefore = await sandbox.ScalarAsync("select count(*) from ledger.journal_line");
        string fingerprintBefore = await FingerprintAsync(sandbox);

        Proof.Note(FormattableString.Invariant(
            $"قبل الهجرة — قيود: {entriesBefore}، سطور: {linesBefore}، بصمة محتوى السطور: {fingerprintBefore}"));

        // والقيد غير موجود أصلاً قبلها: مسحٌ يجد ما يبحث عنه قبل أن يُضاف لا يُثبت شيئاً.
        Proof.Require(
            await sandbox.ScalarAsync(ConstraintCount, Constraint) == 0,
            "القيد غير موجود قبل الهجرة",
            "pg_constraint لا يحوي " + Constraint);

        await sandbox.MigrateToAsync(Target);

        // ── ١) لا صفّ ضاع، ولا بايت تغيّر ─────────────────────────────────
        Proof.Require(
            await sandbox.ScalarAsync("select count(*) from ledger.journal_entry") == entriesBefore,
            "صفوف القيود محفوظة", entriesBefore.ToString(CultureInfo.InvariantCulture));

        Proof.Require(
            await sandbox.ScalarAsync("select count(*) from ledger.journal_line") == linesBefore,
            "صفوف السطور محفوظة", linesBefore.ToString(CultureInfo.InvariantCulture));

        string fingerprintAfter = await FingerprintAsync(sandbox);
        Proof.Require(
            string.Equals(fingerprintAfter, fingerprintBefore, StringComparison.Ordinal),
            "محتوى سطور القيود لم يتغيّر بايتاً — والبايتات مُجزَّأة، فتغيّرها عبثٌ يُبلَّغ عنه",
            fingerprintBefore + " ⇒ " + fingerprintAfter);

        // ── ٢) والقيد حيّ ومُصادَق — مقروءاً من pg_catalog لا من نموذج EF ──
        string definition = await sandbox.TextAsync(
            """
            select pg_get_constraintdef(c.oid)
              from pg_constraint c
              join pg_class t on t.oid = c.conrelid
              join pg_namespace n on n.oid = t.relnamespace
             where n.nspname = 'ledger' and t.relname = 'journal_line' and c.conname = $1
            """, Constraint);

        Proof.Require(
            definition.Contains("cost_center_id IS NOT NULL", StringComparison.OrdinalIgnoreCase)
            && definition.Contains("btrim", StringComparison.OrdinalIgnoreCase),
            "القيد موجود بنصّه في pg_constraint", definition);

        Proof.Require(
            await sandbox.ScalarAsync(
                """
                select case when c.convalidated then 1 else 0 end
                  from pg_constraint c
                  join pg_class t on t.oid = c.conrelid
                  join pg_namespace n on n.oid = t.relnamespace
                 where n.nspname = 'ledger' and t.relname = 'journal_line' and c.conname = $1
                """, Constraint) == 1,
            "convalidated = true — الثابتة تامّة على الجدول كلّه لا على الجديد وحده",
            "pg_constraint.convalidated");

        // ── ٣) والعمود يبقى null-able في SQL، وهذا مقصود ومُعلن ────────────
        Proof.Require(
            await sandbox.ScalarAsync(
                """
                select case when a.attnotnull then 1 else 0 end
                  from pg_attribute a
                  join pg_class t on t.oid = a.attrelid
                  join pg_namespace n on n.oid = t.relnamespace
                 where n.nspname = 'ledger' and t.relname = 'journal_line' and a.attname = 'cost_center_id'
                """) == 0,
            "العمود لم يُجبَر not null — لأن ذلك يسقط على دفتر سبق الثابتة، والقيد يحمل الضمان نفسه",
            "pg_attribute.attnotnull = false");

        // ── ٤) وهو يعمل: كاتبٌ خارج C# يُرفض ───────────────────────────────
        string refusal = await RefusalOfNullCostCenterAsync(sandbox, 3);
        Proof.Require(
            refusal.StartsWith("23514", StringComparison.Ordinal) && refusal.Contains(Constraint, StringComparison.Ordinal),
            "إدراج SQL خام بلا مركز تكلفة مرفوض — الثابتة تلزم أي كاتب لا من يمرّ بـC# فحسب",
            refusal);

        string blank = await RefusalOfNullCostCenterAsync(sandbox, 4, blank: true);
        Proof.Require(
            blank.StartsWith("23514", StringComparison.Ordinal),
            "ونصٌّ من مسافات مرفوض كذلك — الخواء غيابٌ في ثوب حضور",
            blank);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · دفتر فيه سطر سبق الثابتة: لا يُكتب، ولا يُخترَع له مركز، ويُعلَن
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task سطرٌ_مُرحَّل_سبق_الثابتة_لا_يُكتب_ولا_يُخترَع_له_مركز_والقيد_يبقى_غير_مُصادَق()
    {
        await using MigrationSandbox sandbox = await MigrationSandbox.OpenAsync(Previous);

        await SeedAccountsAsync(sandbox);
        await SeedEntryAsync(sandbox, 1, "cc.001", "cc.001");

        // القيد الثاني كما كان يُكتب قبل الثابتة: بلا مركز، وهو الشكل الذي كان العقد
        // يسمح به والنوع يقول عنه string?.
        await SeedEntryAsync(sandbox, 2, null, null);

        long linesBefore = await sandbox.ScalarAsync("select count(*) from ledger.journal_line");
        long legacyBefore = await sandbox.ScalarAsync(LegacyCount);
        string fingerprintBefore = await FingerprintAsync(sandbox);

        Proof.Note(FormattableString.Invariant(
            $"قبل الهجرة — سطور: {linesBefore}، منها بلا مركز: {legacyBefore}، بصمة المحتوى: {fingerprintBefore}"));

        await sandbox.MigrateToAsync(Target);

        // ── ١) الهجرة نجحت — ولم تتوقّف ولم تُعِد الكتابة ─────────────────
        Proof.Require(
            await sandbox.ScalarAsync("select count(*) from ledger.journal_line") == linesBefore,
            "صفوف السطور محفوظة", linesBefore.ToString(CultureInfo.InvariantCulture));

        Proof.Require(
            await sandbox.ScalarAsync(LegacyCount) == legacyBefore,
            "السطور التي سبقت الثابتة **ما زالت بلا مركز** — لا قيمة اخترعتها الهجرة",
            legacyBefore.ToString(CultureInfo.InvariantCulture) + " قبل وبعد");

        string fingerprintAfter = await FingerprintAsync(sandbox);
        Proof.Require(
            string.Equals(fingerprintAfter, fingerprintBefore, StringComparison.Ordinal),
            "ولا بايت واحد كُتب في journal_line — فبصمة كل قيد تُعاد بناؤها كما كانت",
            fingerprintBefore + " ⇒ " + fingerprintAfter);

        // ── ٢) والقيد موجود وغير مُصادَق — وهذا هو الإعلان ─────────────────
        Proof.Require(
            await sandbox.ScalarAsync(ConstraintCount, Constraint) == 1,
            "القيد أُضيف رغم وجود التاريخ", Constraint);

        Proof.Require(
            await sandbox.ScalarAsync(
                """
                select case when c.convalidated then 1 else 0 end
                  from pg_constraint c
                  join pg_class t on t.oid = c.conrelid
                  join pg_namespace n on n.oid = t.relnamespace
                 where n.nspname = 'ledger' and t.relname = 'journal_line' and c.conname = $1
                """, Constraint) == 0,
            "convalidated = false — قاعدة البيانات نفسها تقول: ألزم من هنا، وهذه السطور سبقتني",
            "pg_constraint.convalidated");

        // ── ٣) وهو ملزم رغم ذلك لكل كتابة جديدة ───────────────────────────
        string refusal = await RefusalOfNullCostCenterAsync(sandbox, 3);
        Proof.Require(
            refusal.StartsWith("23514", StringComparison.Ordinal) && refusal.Contains(Constraint, StringComparison.Ordinal),
            "قيدٌ غير مُصادَق يلزم الكتابة الجديدة كاملةً — وهذا هو الفرق بين not valid وغياب القيد",
            refusal);

        // ── ٤) والسطر القديم ما زال مقروءاً كما كُتب ───────────────────────
        Proof.Require(
            await sandbox.ScalarAsync(
                "select count(*) from ledger.journal_line where cost_center_id is null") == legacyBefore,
            "السطر القديم يُقرأ بقيمته الأصلية — الغياب الصادق أرخص من الحضور الكاذب",
            legacyBefore.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════

    private const string ConstraintCount =
        """
        select count(*)
          from pg_constraint c
          join pg_class t on t.oid = c.conrelid
          join pg_namespace n on n.oid = t.relnamespace
         where n.nspname = 'ledger' and t.relname = 'journal_line'
           and c.contype = 'c' and c.conname = $1
        """;

    private const string LegacyCount =
        "select count(*) from ledger.journal_line where cost_center_id is null or length(btrim(cost_center_id)) = 0";

    /// <summary>
    /// بصمة محتوى <c>journal_line</c> كاملاً — <b>لا عدد صفوفه</b>. عددٌ ثابت لا ينفي
    /// إعادة كتابة، والبصمة تنفيها.
    /// </summary>
    private static Task<string> FingerprintAsync(MigrationSandbox sandbox) => sandbox.TextAsync(
        """
        select coalesce(md5(string_agg(row_shape, '|' order by row_shape)), 'فارغ')
          from (select l::text as row_shape from ledger.journal_line l) as rows
        """);

    private static Task SeedAccountsAsync(MigrationSandbox sandbox) => sandbox.ExecuteAsync(
        """
        insert into ledger.account
            (company_id, account_code, name_ar, name_ar_search, parent_code, account_level,
             account_type, natural_side, is_postable, is_contra, subledger_type, required_dimensions,
             currency_mode, is_protected, is_active, status)
        values
            ($1, '1',    'الأصول',    '', null,  1, 'asset',   'debit',  false, false, 'none', '{}', 'any', false, true, 'drafted'),
            ($1, '11',   'المتداولة', '', '1',   2, 'asset',   'debit',  false, false, 'none', '{}', 'any', false, true, 'drafted'),
            ($1, '110',  'النقدية',   '', '11',  3, 'asset',   'debit',  false, false, 'none', '{}', 'any', false, true, 'drafted'),
            ($1, '1101', 'الصندوق',   '', '110', 4, 'asset',   'debit',  true,  false, 'none', '{}', 'any', false, true, 'drafted'),
            ($1, '4',    'الإيرادات', '', null,  1, 'revenue', 'credit', false, false, 'none', '{}', 'any', false, true, 'drafted'),
            ($1, '41',   'التشغيلية', '', '4',   2, 'revenue', 'credit', false, false, 'none', '{}', 'any', false, true, 'drafted'),
            ($1, '410',  'المبيعات',  '', '41',  3, 'revenue', 'credit', false, false, 'none', '{}', 'any', false, true, 'drafted'),
            ($1, '4101', 'مبيعات نقدية', '', '410', 4, 'revenue', 'credit', true, false, 'none', '{}', 'any', false, true, 'drafted')
        """, Company);

    /// <summary>
    /// قيدٌ متوازن بسطرين — في معاملة واحدة، فالمشغّل المؤجَّل عند COMMIT يقبله.
    /// </summary>
    /// <param name="sandbox">القاعدة.</param>
    /// <param name="entryNo">رقم القيد.</param>
    /// <param name="debitCentre">مركز السطر المدين، أو <c>null</c> فالشكل الذي سبق الثابتة.</param>
    /// <param name="creditCentre">مركز السطر الدائن، أو <c>null</c>.</param>
    private static async Task SeedEntryAsync(
        MigrationSandbox sandbox, int entryNo, string? debitCentre, string? creditCentre)
    {
        Guid entryId = new(string.Create(CultureInfo.InvariantCulture, $"e0000000-0000-4000-8000-{entryNo:D12}"));

        // معاملة صريحة: مشغّل التوازن مؤجَّل إلى COMMIT، فالقيد وسطراه يُكتبون معاً أو
        // لا يُكتبون — وهذا هو مسار الإنتاج نفسه لا تسهيلاً للاختبار.
        await using NpgsqlConnection connection = new(sandbox.ConnectionString);
        await connection.OpenAsync().ConfigureAwait(false);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync().ConfigureAwait(false);

        await using (NpgsqlCommand head = new(
            """
            insert into ledger.journal_entry
                (entry_id, company_id, book_id, fiscal_year, entry_no, entry_date, period_code,
                 posted_at, status, actor, source_module, source_doc_type, source_doc_id,
                 posting_trigger_code, idempotency_key, currency, event_code,
                 actor_search, memo, memo_ar, memo_ar_search, posting_generation)
            values ($1, $2, 'MAIN', 2026, $3, date '2026-08-15', '2026-08',
                    timestamptz '2026-08-15T10:00:00Z', 'POSTED', 'tester', 'Ledger', 'ManualJournal',
                    $3::text, 'OnApproval', 'seed-' || $3::text, 'SAR',
                    'ledger.manual_voucher.posted', 'tester', '', '', '', 1)
            """, connection, transaction))
        {
            head.Parameters.AddWithValue(entryId);
            head.Parameters.AddWithValue(Company);
            head.Parameters.AddWithValue(entryNo);
            await head.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await using (NpgsqlCommand body = new(
            """
            insert into ledger.journal_line
                (line_id, entry_id, line_no, company_id, account_code, role_code, qualifier,
                 debit, credit, currency, fx_rate, debit_company, credit_company, cost_center_id)
            values
                (gen_random_uuid(), $1, 1, $2, '1101', '', '*', 100.0000, 0, 'SAR', 1, 100.0000, 0, $3),
                (gen_random_uuid(), $1, 2, $2, '4101', '', '*', 0, 100.0000, 'SAR', 1, 0, 100.0000, $4)
            """, connection, transaction))
        {
            body.Parameters.AddWithValue(entryId);
            body.Parameters.AddWithValue(Company);
            body.Parameters.Add(new NpgsqlParameter { Value = (object?)debitCentre ?? DBNull.Value });
            body.Parameters.Add(new NpgsqlParameter { Value = (object?)creditCentre ?? DBNull.Value });
            await body.ExecuteNonQueryAsync().ConfigureAwait(false);
        }

        await transaction.CommitAsync().ConfigureAwait(false);
    }

    /// <summary>
    /// يحاول إدراج سطر بلا مركز تكلفة <b>بـSQL خام</b>، ويُعيد رمز الرفض ونصّه.
    /// و«خام» هي الكلمة: الاختبار يتجاوز C# كاملةً كي يفحص ما تفرضه قاعدة البيانات.
    /// </summary>
    private static async Task<string> RefusalOfNullCostCenterAsync(
        MigrationSandbox sandbox, int entryNo, bool blank = false)
    {
        try
        {
            await SeedEntryAsync(sandbox, entryNo, blank ? "   " : null, "cc.001");
            return "لم يُرفض إطلاقاً";
        }
        catch (PostgresException exception)
        {
            return exception.SqlState + ": " + exception.MessageText;
        }
    }
}
