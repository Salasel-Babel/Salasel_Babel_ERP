using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// الطبقتان الأبعد عن الكود: الصلاحيات، والمشغّل المؤجَّل عند COMMIT.
/// <para>
/// كلتاهما تُفحصان <b>عبر أكثر من مسار كتابة</b> عمداً. «القيد المرحَّل لا يُعدَّل»
/// جملة يفرضها كل نظام في طبقة التطبيق، وطبقة التطبيق يُلتَفّ عليها: سكربت صيانة،
/// أداة إدارة، بيانات اعتماد مسرّبة. السؤال العملي هو ما الذي يبقى صحيحاً حين يفشل
/// كل ما سبق (ADR-0003).
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class ImmutabilityTests : IAsyncLifetime
{
    private LedgerHarness _harness = null!;
    private Guid _entryId;

    public async ValueTask InitializeAsync()
    {
        _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);

        Result<PostingReceipt> posted = await _harness.Posting.PostAsync(
            Requests.RentInvoice(LedgerTestEnvironment.TenantA, "IMM-" + Guid.NewGuid().ToString("N")[..8],
                2_000.0000m, 300.0000m, new DateOnly(2026, 8, 12)),
            TestContext.Current.CancellationToken);

        _entryId = posted.Value.JournalEntryId;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // الطبقة الأولى — الصلاحيات: 42501 عبر SQL خام وعبر EF Core
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Update_delete_and_truncate_are_refused_with_42501_through_raw_sql()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await using NpgsqlConnection app = LedgerHarness.OpenApp();

        await using (NpgsqlCommand who = new(
            "select current_user, (select rolsuper from pg_roles where rolname = current_user)", app))
        {
            await using NpgsqlDataReader reader = await who.ExecuteReaderAsync(token);
            await reader.ReadAsync(token);
            Proof.Require(reader.GetString(0) == LedgerTestEnvironment.AppRole && !reader.GetBoolean(1),
                "دور التطبيق غير مالك وغير superuser — بدون هذا كل ما تحته زينة (فخ-30)",
                $"المستخدم {reader.GetString(0)} · superuser = {reader.GetBoolean(1)}");
        }

        (string Statement, string Sql)[] probes =
        [
            ("UPDATE journal_entry", "update ledger.journal_entry set memo_ar = 'عبث' where entry_id = $1"),
            ("DELETE journal_entry", "delete from ledger.journal_entry where entry_id = $1"),
            ("UPDATE journal_line", "update ledger.journal_line set debit = 1 where entry_id = $1"),
            ("DELETE journal_line", "delete from ledger.journal_line where entry_id = $1"),
            ("TRUNCATE journal_line", "truncate ledger.journal_line"),
            ("TRUNCATE journal_entry", "truncate ledger.journal_entry cascade"),
            ("UPDATE chain_link", "update ledger.chain_link set entry_hash = entry_hash where entry_id = $1"),
        ];

        foreach ((string statement, string sql) in probes)
        {
            string state = "لا خطأ إطلاقاً";
            try
            {
                await using NpgsqlCommand command = new(sql, app);
                if (sql.Contains("$1", StringComparison.Ordinal))
                {
                    command.Parameters.AddWithValue(_entryId);
                }

                await command.ExecuteNonQueryAsync(token);
            }
            catch (PostgresException exception)
            {
                state = exception.SqlState;
            }

            Proof.Require(state == "42501", $"{statement} مرفوض بالرمز 42501 من PostgreSQL نفسها",
                $"SQLSTATE = {state}");
        }
    }

    [Fact]
    public async Task Update_and_delete_are_refused_with_42501_through_ef_core()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        DbContextOptionsBuilder<LedgerDbContext> builder = new();
        builder.UseNpgsql(LedgerTestEnvironment.Options.AppConnectionString);
        await using LedgerDbContext context = new(builder.Options);

        JournalEntryRow entry = await context.JournalEntries.SingleAsync(row => row.EntryId == _entryId, token);
        entry.MemoAr = "عبث عبر EF Core";

        string updateState = "لا خطأ إطلاقاً";
        try
        {
            await context.SaveChangesAsync(token);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            updateState = postgres.SqlState;
        }

        Proof.Require(updateState == "42501", "‏EF Core لا يملك امتيازاً خاصاً: التعديل مرفوض بالرمز نفسه",
            $"SQLSTATE = {updateState}");

        context.ChangeTracker.Clear();

        JournalLineRow line = await context.JournalLines.FirstAsync(row => row.EntryId == _entryId, token);
        context.JournalLines.Remove(line);

        string deleteState = "لا خطأ إطلاقاً";
        try
        {
            await context.SaveChangesAsync(token);
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            deleteState = postgres.SqlState;
        }

        Proof.Require(deleteState == "42501", "الحذف عبر EF Core مرفوض بالرمز نفسه",
            $"SQLSTATE = {deleteState}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // الطبقة الثانية — المشغّل المؤجَّل: 0.0001 مرفوضة عند COMMIT، بثلاثة مسارات
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task An_imbalance_of_one_ten_thousandth_is_refused_at_commit_through_three_code_paths()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        await LedgerTestEnvironment.EnsureCounterAsync(LedgerTestEnvironment.TenantA, "TRIG", token);

        // ── المسار الأول: SQL خام بدور التطبيق ────────────────────────────
        string first = await AttemptAsync(LedgerHarness.OpenApp, 950_001, 1_000.0000m, 999.9999m, token);
        Proof.Require(first.Contains("UNBALANCED_ENTRY", StringComparison.Ordinal),
            "مسار 1 — SQL خام بدور التطبيق: فرق 0.0001 مرفوض عند COMMIT",
            first);

        // ── المسار الثاني: SQL خام بدور **المالك** ────────────────────────
        // المالك يستطيع UPDATE وDELETE؛ ومع ذلك لا يستطيع أن يُثبِّت قيداً غير
        // متوازن. الطبقتان مستقلّتان عمداً.
        string second = await AttemptAsync(LedgerHarness.OpenOwner, 950_002, 1_000.0000m, 999.9999m, token);
        Proof.Require(second.Contains("UNBALANCED_ENTRY", StringComparison.Ordinal),
            "مسار 2 — SQL خام بدور المالك: الفرق نفسه مرفوض، والامتياز لا يشتري توازناً",
            second);

        // ── المسار الثالث: EF Core ────────────────────────────────────────
        string third = await AttemptThroughEfAsync(950_003, 1_000.0000m, 999.9999m, token);
        Proof.Require(third.Contains("UNBALANCED_ENTRY", StringComparison.Ordinal),
            "مسار 3 — EF Core: الفرق نفسه مرفوض عند COMMIT لا عند الإدراج",
            third);

        // ── وقيد بسطر واحد: عدد السطور >= 2 يُفحص في اللحظة نفسها ─────────
        string single = await AttemptSingleLineAsync(950_004, token);
        Proof.Require(single.Contains("UNBALANCED_ENTRY", StringComparison.Ordinal),
            "قيد بسطر واحد مرفوض عند COMMIT",
            single);

        // ── وقيد بلا سطور إطلاقاً ─────────────────────────────────────────
        string empty = await AttemptZeroLineAsync(950_005, token);
        Proof.Require(empty.Contains("UNBALANCED_ENTRY", StringComparison.Ordinal),
            "قيد بلا سطور إطلاقاً مرفوض عند COMMIT — والرأس وحده لا يصنع قيداً",
            empty);

        // ── ونفس التركيب متوازناً يمرّ: الاختبار ليس «كل شيء يفشل» ────────
        string balanced = await AttemptAsync(LedgerHarness.OpenApp, 950_006, 1_000.0000m, 1_000.0000m, token);
        Proof.Require(balanced == "COMMITTED", "التركيب نفسه متوازناً يمرّ — المشغّل يفحص التوازن لا يمنع الكتابة",
            balanced);
    }

    private static async Task<string> AttemptAsync(
        Func<NpgsqlConnection> open,
        long entryNo,
        decimal debit,
        decimal credit,
        CancellationToken token)
    {
        await using NpgsqlConnection connection = open();
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        Guid entryId = Guid.CreateVersion7();

        try
        {
            await PostingEngineTests.InsertRawEntryAsync(
                connection, transaction, LedgerTestEnvironment.TenantA, "TRIG", entryId, entryNo, token);
            await PostingEngineTests.InsertRawLineAsync(connection, transaction, entryId, 1, "1310", debit, 0m, null, token);
            await PostingEngineTests.InsertRawLineAsync(connection, transaction, entryId, 2, "2131", 0m, credit, null, token);
            await transaction.CommitAsync(token);
            return "COMMITTED";
        }
        catch (PostgresException exception)
        {
            return $"SQLSTATE {exception.SqlState}: {Head(exception.MessageText)}";
        }
    }

    private static async Task<string> AttemptSingleLineAsync(long entryNo, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(LedgerTestEnvironment.Options.AppConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        Guid entryId = Guid.CreateVersion7();

        try
        {
            await PostingEngineTests.InsertRawEntryAsync(
                connection, transaction, LedgerTestEnvironment.TenantA, "TRIG", entryId, entryNo, token);
            await PostingEngineTests.InsertRawLineAsync(connection, transaction, entryId, 1, "1310", 0m, 0m, null, token);
            await transaction.CommitAsync(token);
            return "COMMITTED";
        }
        catch (PostgresException exception)
        {
            return $"SQLSTATE {exception.SqlState}: {Head(exception.MessageText)}";
        }
    }

    private static async Task<string> AttemptZeroLineAsync(long entryNo, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(LedgerTestEnvironment.Options.AppConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        Guid entryId = Guid.CreateVersion7();

        try
        {
            await PostingEngineTests.InsertRawEntryAsync(
                connection, transaction, LedgerTestEnvironment.TenantA, "TRIG", entryId, entryNo, token);
            await transaction.CommitAsync(token);
            return "COMMITTED";
        }
        catch (PostgresException exception)
        {
            return $"SQLSTATE {exception.SqlState}: {Head(exception.MessageText)}";
        }
    }

    private static async Task<string> AttemptThroughEfAsync(
        long entryNo,
        decimal debit,
        decimal credit,
        CancellationToken token)
    {
        DbContextOptionsBuilder<LedgerDbContext> builder = new();
        builder.UseNpgsql(LedgerTestEnvironment.Options.AppConnectionString);
        await using LedgerDbContext context = new(builder.Options);

        Guid entryId = Guid.CreateVersion7();
        context.JournalEntries.Add(new JournalEntryRow
        {
            EntryId = entryId,
            CompanyId = LedgerTestEnvironment.TenantA,
            BookId = "TRIG",
            FiscalYear = LedgerTestEnvironment.FiscalYear,
            EntryNo = entryNo,
            EntryDate = new DateOnly(2026, 3, 15),
            PeriodCode = "2026-03",
            PostedAt = DateTimeOffset.UtcNow,
            Status = "POSTED",
            Actor = "ef-core",
            SourceModule = "Ledger",
            SourceDocType = "EfProbe",
            SourceDocId = entryId.ToString("D", CultureInfo.InvariantCulture),
            PostingTriggerCode = "EF",

            // رمز الحدث جزء من هوية الترحيل ولا افتراضي له (D-3): حتى مسار
            // EF الخام في اختبار يسمّي حدثه، وإلا رفضه ck_journal_entry_event_code
            // قبل أن يصل المشغّل المؤجَّل — فلا يبقى ما يُقاس هنا.
            EventCode = "ledger.manual_voucher.posted",
            IdempotencyKey = entryId.ToString("D", CultureInfo.InvariantCulture),
            Currency = "SAR",
        });

        context.JournalLines.Add(new JournalLineRow
        {
            LineId = Guid.CreateVersion7(), EntryId = entryId, LineNo = 1,
            CompanyId = LedgerTestEnvironment.TenantA, Code = "1310", Currency = "SAR",
            Debit = debit, DebitCompany = debit,
        });
        context.JournalLines.Add(new JournalLineRow
        {
            LineId = Guid.CreateVersion7(), EntryId = entryId, LineNo = 2,
            CompanyId = LedgerTestEnvironment.TenantA, Code = "2131", Currency = "SAR",
            Credit = credit, CreditCompany = credit,
        });

        try
        {
            await context.SaveChangesAsync(token);
            return "COMMITTED";
        }
        catch (DbUpdateException exception) when (exception.InnerException is PostgresException postgres)
        {
            return $"SQLSTATE {postgres.SqlState}: {Head(postgres.MessageText)}";
        }
        catch (PostgresException exception)
        {
            // وهذا هو بيت القصيد: المشغّل مؤجَّل، فالرفض يقع عند **COMMIT** لا عند
            // الإدراج — أي أنه لا يصل مغلَّفاً في DbUpdateException أصلاً. من يمسك
            // DbUpdateException وحده يظنّ أن الكتابة نجحت.
            return $"SQLSTATE {exception.SqlState}: {Head(exception.MessageText)}";
        }
    }

    private static string Head(string text) => text[..Math.Min(150, text.Length)];
}
