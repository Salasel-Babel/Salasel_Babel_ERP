using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Audit;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>مجموعا ميزان المراجعة — من <c>sum()</c> على <c>numeric</c>، لا من جمعٍ في طبقة أعلى.</b>
/// <para>
/// الجمع في طبقة HTTP حسابٌ على المال يمنعه البند (أ) من القاعدة 13، والجمع في المتصفّح
/// يُعيد الفخّ نفسه لأن <c>Number</c> في JavaScript فاصلة عائمة ثنائية. فالموضع الوحيد
/// الصحيح هو الاستعلام: <c>numeric</c> في PostgreSQL يجمع بضبط تامّ.
/// </para>
/// <para>
/// وما يُفحص هنا شيئان: أن المجموعين يساويان جمع الصفوف المُعادة، و<b>أن ميزاناً لا
/// يتوازن يُرى كذلك</b> — لا يُقرَّب، ولا يُخفى خلف علامة «سليم».
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class TrialBalanceTotalsTests : IAsyncLifetime
{
    /// <summary>دفتر مستقل: العبث أدناه لا يجوز أن يلوّث ميزان دفتر آخر.</summary>
    private const string Book = "UNBAL";

    private const string Period = "2026-09";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · المجموعان يساويان جمع الصفوف — والميزان المتوازن يُعلَن متوازناً
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task مجموعا_الاستعلام_يساويان_جمع_الصفوف_المُعادة()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await LedgerTestEnvironment.EnsureCounterAsync(LedgerTestEnvironment.TenantA, "TOTALS", token);

        for (int i = 1; i <= 4; i++)
        {
            Result<PostingReceipt> posted = await _harness.Posting.PostAsync(
                Voucher("TOT-" + i.ToString(CultureInfo.InvariantCulture), 111.1111m * i), token);

            Proof.Require(posted.IsSuccess, $"القيد {i.ToString(CultureInfo.InvariantCulture)} رُحّل",
                posted.IsSuccess ? posted.Value.EntryNumber.ToString(CultureInfo.InvariantCulture)
                                 : string.Join(" | ", posted.Errors.Select(static e => e.Code)));
        }

        TrialBalanceReport trial =
            await ReadAsync("TOTALS", "2026-06", token);

        decimal sumDebit = trial.Rows.Sum(static row => row.Debit);
        decimal sumCredit = trial.Rows.Sum(static row => row.Credit);

        Proof.Require(
            trial.Rows.Count > 0,
            "الميزان ليس فارغاً — فالمقارنة تقارن شيئاً",
            trial.Rows.Count.ToString(CultureInfo.InvariantCulture) + " صفّاً");

        Proof.Require(
            trial.TotalDebit == sumDebit && trial.TotalCredit == sumCredit,
            "مجموعا sum() على numeric يساويان جمع الصفوف المُعادة بالضبط",
            $"مدين {Proof.Money(trial.TotalDebit)} = {Proof.Money(sumDebit)} · "
            + $"دائن {Proof.Money(trial.TotalCredit)} = {Proof.Money(sumCredit)}");

        // ‏111.1111 × (1+2+3+4) = 1,111.1110 — رقم لا يُمثَّل تمثيلاً تامّاً بفاصلة عائمة ثنائية.
        Proof.Require(
            trial.TotalDebit == 1_111.1110m && trial.Balanced,
            "المجموع بالضبط إلى الخانة الرابعة، والميزان مُعلَن متوازناً",
            $"{Proof.Money(trial.TotalDebit)} · متوازن {trial.Balanced}");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · ميزان لا يتوازن يُرى — ولا يُقرَّب ولا يُخفى
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// المحرك لا ينتج قيداً غير متوازن: <c>AssertBalanced</c> يمنعه، والمشغّل المؤجَّل
    /// <c>trg_journal_line_balanced</c> يرفضه عند <c>COMMIT</c> مهما كان مسار الكتابة.
    /// فالانحراف يُحقن هنا بتعطيل المشغّل بدور <b>المالك</b> — وهو بالضبط نموذج التهديد
    /// الذي بُنيت له سلسلة البصمات (ADR-0003 · ADR-0007): من يملك قاعدة البيانات يكتب.
    /// والسؤال الذي يفحصه هذا الاختبار: <b>هل يُرى ذلك في التقرير؟</b>
    /// </summary>
    [Fact]
    public async Task ميزان_لا_يتوازن_يظهر_غير_متوازن_بمجموعين_مختلفين()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        await InjectUnbalancedAsync(token);

        TrialBalanceReport trial =
            await ReadAsync(Book, Period, token);

        Proof.Require(
            trial.Rows.Count == 1,
            "صفّ واحد في دفتر العبث",
            trial.Rows.Count.ToString(CultureInfo.InvariantCulture) + " صفّاً");

        Proof.Require(
            trial.TotalDebit == 100.0000m && trial.TotalCredit == 60.0000m,
            "المجموعان مختلفان ويظهران بقيمتيهما الحقيقيتين — لا يُقرَّبان ولا يُسوَّيان",
            $"مدين {Proof.Money(trial.TotalDebit)} · دائن {Proof.Money(trial.TotalCredit)} · "
            + $"الفرق {Proof.Money(trial.TotalDebit - trial.TotalCredit)}");

        Proof.Require(
            !trial.Balanced,
            "حكم التوازن يقول «غير متوازن» — والحكم محسوم في الدفتر لا عند العميل",
            "balanced = " + trial.Balanced.ToString());

        // والمجموعان يبقيان مطابقين لجمع الصفوف حتى في الحالة المنحرفة.
        Proof.Require(
            trial.TotalDebit == trial.Rows.Sum(static row => row.Debit)
            && trial.TotalCredit == trial.Rows.Sum(static row => row.Credit),
            "المجموعان مشتقّان من الصفوف نفسها — الانحراف في البيانات لا بين استعلامين",
            $"{Proof.Money(trial.TotalDebit)} · {Proof.Money(trial.TotalCredit)}");
    }

    // ═══════════════════════════════════════════════════════════════════════

    private async Task<TrialBalanceReport>
        ReadAsync(string book, string period, CancellationToken token)
    {
        Result<TrialBalanceReport> result =
            await _harness.Auditing.TrialBalanceFromLinesAsync(
                new TenantId(LedgerTestEnvironment.TenantA), LedgerTestEnvironment.Auditor, book, period, token);

        Proof.Require(result.IsSuccess, $"قراءة ميزان الدفتر {book}",
            result.IsSuccess ? "نجحت" : string.Join(" | ", result.Errors.Select(static e => e.Code)));

        return result.Value;
    }

    /// <summary>
    /// يحقن قيداً غير متوازن بدور المالك، بتعطيل المشغّلين المؤجَّلين ثم إعادة تفعيلهما
    /// حتماً. والإعادة في <c>finally</c>: مشغّل يبقى معطَّلاً يجعل كل اختبار بعده يقيس
    /// دفتراً بلا حارس.
    /// </summary>
    private static async Task InjectUnbalancedAsync(CancellationToken token)
    {
        await using NpgsqlConnection owner = LedgerHarness.OpenOwner();

        await ExecuteAsync(owner, "alter table ledger.journal_line disable trigger trg_journal_line_balanced", token);
        await ExecuteAsync(owner, "alter table ledger.journal_entry disable trigger trg_journal_entry_balanced", token);

        try
        {
            Guid entryId = Guid.CreateVersion7();

            await using (NpgsqlCommand entry = new(
                """
                insert into ledger.journal_entry
                    (entry_id, company_id, book_id, fiscal_year, entry_no, entry_date, period_code, posted_at,
                     status, actor, source_module, source_doc_type, source_doc_id, posting_trigger_code,
                     event_code, idempotency_key, currency)
                values ($1, $2, $3, 2026, $4, '2026-09-15', $5, now(), 'POSTED', 'owner-tamper',
                        'Ledger', 'TamperProbe', $6, 'RAW', 'ledger.manual_voucher.posted', $6, 'SAR')
                on conflict do nothing
                """, owner))
            {
                entry.Parameters.AddWithValue(entryId);
                entry.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
                entry.Parameters.AddWithValue(Book);
                entry.Parameters.AddWithValue(900_101L);
                entry.Parameters.AddWithValue(Period);
                entry.Parameters.AddWithValue(entryId.ToString("D", CultureInfo.InvariantCulture));
                await entry.ExecuteNonQueryAsync(token);
            }

            await InsertLineAsync(owner, entryId, 1, 100.0000m, 0m, token);
            await InsertLineAsync(owner, entryId, 2, 0m, 60.0000m, token);
        }
        finally
        {
            await ExecuteAsync(owner, "alter table ledger.journal_line enable trigger trg_journal_line_balanced", token);
            await ExecuteAsync(owner, "alter table ledger.journal_entry enable trigger trg_journal_entry_balanced", token);
        }
    }

    private static async Task InsertLineAsync(
        NpgsqlConnection owner, Guid entryId, int lineNo, decimal debit, decimal credit, CancellationToken token)
    {
        await using NpgsqlCommand line = new(
            """
            insert into ledger.journal_line
                (line_id, entry_id, line_no, company_id, account_code, role_code, qualifier,
                 debit, credit, currency, fx_rate, debit_company, credit_company, cost_center_id)
            values ($1, $2, $3, $4, '1310', '', '*', $5, $6, 'SAR', 1, $5, $6, 'cc.001')
            on conflict do nothing
            """, owner);
        line.Parameters.AddWithValue(Guid.CreateVersion7());
        line.Parameters.AddWithValue(entryId);
        line.Parameters.AddWithValue(lineNo);
        line.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
        line.Parameters.AddWithValue(debit);
        line.Parameters.AddWithValue(credit);
        await line.ExecuteNonQueryAsync(token);
    }

    private static async Task ExecuteAsync(NpgsqlConnection connection, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection);
        await command.ExecuteNonQueryAsync(token);
    }

    private static PostingRequest Voucher(string documentId, decimal amount) => new()
    {
        Tenant = new TenantId(LedgerTestEnvironment.TenantA),
        IdempotencyKey = new IdempotencyKey("totals:" + documentId),
        Source = new SourceDocument(BabelModule.Ledger, "ManualVoucher", documentId),
        Trigger = PostingTrigger.OnApproval,
        DocumentDate = new DateOnly(2026, 6, 20),
        Narration = new LocalizedName("قيد يومية يدوي", "Manual journal voucher"),
        Book = "TOTALS",
        Currency = CurrencyCode.Sar,
        Event = new PostingEventCode("ledger.manual_voucher.posted"),
        Lines =
        [
            new PostingLine
            {
                Role = PostingRole.RoundingDifference,
                Side = PostingSide.Debit,
                Amount = SharedKernel.Money.Of(amount, CurrencyCode.Sar),
                Scope = new PostingScope("cc.001", "BR-01"),
            },
            new PostingLine
            {
                Role = PostingRole.RoundingDifference,
                Side = PostingSide.Credit,
                Amount = SharedKernel.Money.Of(amount, CurrencyCode.Sar),
                Scope = new PostingScope("cc.001", "BR-01"),
            },
        ],
        Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
    };
}
