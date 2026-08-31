using System.Globalization;
using Babel.Contracts.Posting;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// هوية الترحيل — <b>ما الذي يجعل قيدين قيداً واحداً؟</b>
/// <para>
/// المفتاح <c>uq_posting_identity</c> هو تعريف النظام لـ«هذا رُحّل من قبل».
/// وكل حقل ناقص منه يعني حقيقتين محاسبيتين مختلفتين يعدّهما المحرك حقيقة واحدة،
/// فيبتلع الثانية ويُرجع إيصالاً يقول «مُرحَّل سلفاً» — بلا خطأ، وبلا اختلال
/// توازن، وبلا كسر في سلسلة البصمات. العَرَض الوحيد دفتر مساعد لا يطابق حسابه
/// الضابط، ويُكتشف بعد أسابيع (D-3).
/// </para>
/// <para>
/// المستند الواحد يُنتج حدثين عند نفس الإطلاق في حالات يومية لا استثنائية:
/// فاتورة مبيعات تعترف بالإيراد و<b>تُنزل المخزون بالتكلفة</b> في اللحظة نفسها؛
/// وفاتورة مورد تُثبت الالتزام و<b>تعترف بفرق سعر</b> مقابل استلام سابق؛ ودفعةٌ
/// تُسدّد التزاماً و<b>تسجّل رسماً بنكياً</b>؛ ومسيرُ رواتب يُثبت الأجر الإجمالي
/// و<b>حصة المنشأة في التأمينات</b>.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class PostingIdentityTests : IAsyncLifetime
{
    /// <summary>دفتر مستقل: العدّاد والسلسلة والأرصدة كلها بنطاق (شركة × دفتر × سنة).</summary>
    private const string Book = "IDENTITY";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);
        await LedgerTestEnvironment.EnsureCounterAsync(
            LedgerTestEnvironment.TenantA, Book, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · حدثان مختلفان من مستند واحد عند إطلاق واحد — كلاهما يجب أن يصل الدفتر
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Two_different_events_from_one_document_at_one_trigger_both_reach_the_ledger()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = "INV-D3-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];

        // الحدث الأول: الاعتراف بالإيراد. 1301 مدين 11,500 · 4101 دائن 10,000 · 2131 دائن 1,500.
        Result<PostingReceipt> revenue = await _harness.Posting.PostAsync(
            SalesRevenue(documentId, 10_000.0000m, 1_500.0000m), token);

        Proof.Require(revenue.IsSuccess, "الحدث الأول (الاعتراف بالإيراد) رُحّل",
            revenue.IsSuccess
                ? $"القيد {revenue.Value.EntryNumber.ToString(CultureInfo.InvariantCulture)}"
                : string.Join(" | ", revenue.Errors.Select(static e => e.Code + ": " + e.MessageAr)));

        // الحدث الثاني: إنزال المخزون بالتكلفة. 5101 مدين 6,400 · 1401 دائن 6,400.
        // نفس الشركة، ونفس نوع المستند، ونفس رقمه، ونفس الإطلاق، ونفس الجيل —
        // والفرق الوحيد رمز الحدث والمبالغ والحسابات.
        Result<PostingReceipt> cost = await _harness.Posting.PostAsync(
            SalesCostOfSales(documentId, 6_400.0000m), token);

        Proof.Require(cost.IsSuccess, "الحدث الثاني (تكلفة المبيعات) لم يُرفض",
            cost.IsSuccess
                ? "لم يُرفض"
                : string.Join(" | ", cost.Errors.Select(static e => e.Code + ": " + e.MessageAr)));

        Proof.Note("إيصال الأول: قيد=" + revenue.Value.JournalEntryId.ToString("D", CultureInfo.InvariantCulture)
                   + " · تسلسل=" + revenue.Value.ChainSequence.ToString(CultureInfo.InvariantCulture)
                   + " · مُرحَّل سلفاً=" + revenue.Value.WasAlreadyPosted.ToString()
                   + " · سطور=" + revenue.Value.LineCount.ToString(CultureInfo.InvariantCulture));
        Proof.Note("إيصال الثاني: قيد=" + cost.Value.JournalEntryId.ToString("D", CultureInfo.InvariantCulture)
                   + " · تسلسل=" + cost.Value.ChainSequence.ToString(CultureInfo.InvariantCulture)
                   + " · مُرحَّل سلفاً=" + cost.Value.WasAlreadyPosted.ToString()
                   + " · سطور=" + cost.Value.LineCount.ToString(CultureInfo.InvariantCulture));

        // ── الشاهد الأول: الإيصال لا يدّعي أن الثاني «مُرحَّل سلفاً» ─────────
        Proof.Require(!cost.Value.WasAlreadyPosted,
            "قيد التكلفة ليس تكراراً لقيد الإيراد — والإيصال لا يقول إنه مُرحَّل سلفاً",
            "‏WasAlreadyPosted = " + cost.Value.WasAlreadyPosted.ToString()
            + (cost.Value.WasAlreadyPosted
                ? " ← ضياع صامت: حقيقة محاسبية كاملة لم تُكتب ولم يُبلَّغ عنها خطأ"
                : string.Empty));

        Proof.Require(cost.Value.JournalEntryId != revenue.Value.JournalEntryId,
            "القيدان معرّفان مستقلان",
            "الأول=" + revenue.Value.JournalEntryId.ToString("D", CultureInfo.InvariantCulture)
            + " · الثاني=" + cost.Value.JournalEntryId.ToString("D", CultureInfo.InvariantCulture));

        Proof.Require(cost.Value.ChainSequence != revenue.Value.ChainSequence,
            "لكل قيد موقعه في سلسلة البصمات",
            "تسلسل الأول=" + revenue.Value.ChainSequence.ToString(CultureInfo.InvariantCulture)
            + " · تسلسل الثاني=" + cost.Value.ChainSequence.ToString(CultureInfo.InvariantCulture));

        // ── الشاهد الثاني: الدفتر نفسه، لا الإيصال ────────────────────────
        List<(string EventCode, Guid EntryId, long EntryNo, decimal Debit)> written =
            await EntriesOfAsync(documentId, token);

        foreach ((string eventCode, Guid entryId, long entryNo, decimal debit) in written)
        {
            Proof.Note($"في الدفتر: {eventCode} ⇒ قيد رقم {entryNo.ToString(CultureInfo.InvariantCulture)} "
                       + $"({entryId.ToString("D", CultureInfo.InvariantCulture)}) مدين {Proof.Money(debit)}");
        }

        Proof.Require(written.Count == 2,
            "المستند الواحد كتب قيدين في الدفتر: الإيراد والتكلفة",
            "عدد القيود المكتوبة = " + written.Count.ToString(CultureInfo.InvariantCulture)
            + (written.Count < 2 ? " ← حدث محاسبي كامل اختفى من الدفتر" : string.Empty));

        Proof.Require(
            written.Exists(static row => row.EventCode == "sales.invoice.posted" && row.Debit == 11_500.0000m),
            "قيد الإيراد بمبلغه الصحيح", string.Join(" · ", written.Select(static row =>
                row.EventCode + "=" + Proof.Money(row.Debit))));

        Proof.Require(
            written.Exists(static row => row.EventCode == "sales.invoice.cost_of_sales" && row.Debit == 6_400.0000m),
            "قيد التكلفة بمبلغه الصحيح", string.Join(" · ", written.Select(static row =>
                row.EventCode + "=" + Proof.Money(row.Debit))));

        Proof.Require(written[0].EntryNo != written[1].EntryNo,
            "رقما القيدين مختلفان من العدّاد بلا فجوات",
            written[0].EntryNo.ToString(CultureInfo.InvariantCulture) + " · "
            + written[1].EntryNo.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · وتوسيع المفتاح لم يفتح باب الازدواج: الطلب نفسه مرّتين يكتب مرّة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task The_identical_event_posted_twice_is_written_exactly_once()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = "INV-DUP-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];

        PostingRequest request = SalesRevenue(documentId, 4_000.0000m, 600.0000m);

        Result<PostingReceipt> first = await _harness.Posting.PostAsync(request, token);
        Proof.Require(first.IsSuccess, "الترحيل الأول نجح",
            first.IsSuccess ? "نجح" : string.Join(" | ", first.Errors.Select(static e => e.MessageAr)));

        // نفس رمز الحدث بالحرف، ونفس كل شيء آخر.
        Result<PostingReceipt> second = await _harness.Posting.PostAsync(
            SalesRevenue(documentId, 4_000.0000m, 600.0000m), token);

        Proof.Require(second.IsSuccess, "الترحيل الثاني ليس خطأ",
            second.IsSuccess ? "نجح" : string.Join(" | ", second.Errors.Select(static e => e.MessageAr)));

        Proof.Require(second.Value.WasAlreadyPosted,
            "الوصول الثاني بالهوية نفسها يُبلَّغ عنه «مُرحَّل سلفاً»",
            "‏WasAlreadyPosted = " + second.Value.WasAlreadyPosted.ToString());

        Proof.Require(second.Value.JournalEntryId == first.Value.JournalEntryId
                      && second.Value.EntryNumber == first.Value.EntryNumber,
            "الإيصال الثاني يشير إلى القيد الأول نفسه لا إلى قيد جديد",
            "قيد الأول=" + first.Value.EntryNumber.ToString(CultureInfo.InvariantCulture)
            + " · قيد الثاني=" + second.Value.EntryNumber.ToString(CultureInfo.InvariantCulture));

        Proof.Require(second.Value.LineCount == 0,
            "الوصول الثاني لم يكتب سطراً واحداً",
            "عدد السطور المكتوبة = " + second.Value.LineCount.ToString(CultureInfo.InvariantCulture));

        List<(string EventCode, Guid EntryId, long EntryNo, decimal Debit)> written =
            await EntriesOfAsync(documentId, token);

        Proof.Require(written.Count == 1,
            "الدفتر يحمل قيداً واحداً بالضبط لهذا المستند",
            "عدد القيود = " + written.Count.ToString(CultureInfo.InvariantCulture));

        Proof.Require(written[0].Debit == 4_600.0000m,
            "ولم يتضاعف مبلغه",
            "المدين = " + Proof.Money(written[0].Debit));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · رمز حدث فارغ يُعيد الحدثين حدثاً واحداً — فيُرفض قبل أن يصل الدفتر
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task An_empty_event_code_is_refused_because_it_collapses_two_events_into_one()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = "JV-EMPTY-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];

        // المسار الصريح: سطور مذكورة بلا رمز حدث. كان يكتب '' في عمود الهوية،
        // فيصير مستندان بحدثين مختلفين مستنداً واحداً بحدث واحد.
        PostingRequest request = new()
        {
            Tenant = new TenantId(LedgerTestEnvironment.TenantA),
            IdempotencyKey = new IdempotencyKey("manual:" + documentId),
            Source = new SourceDocument(BabelModule.Ledger, "ManualVoucher", documentId),
            Trigger = PostingTrigger.OnApproval,
            DocumentDate = new DateOnly(2026, 6, 15),
            Narration = new LocalizedName("قيد يدوي بلا رمز حدث", "Manual voucher with no event code"),
            Book = Book,
            Currency = CurrencyCode.Sar,
            Lines =
            [
                new PostingLine
                {
                    Role = PostingRole.RoundingDifference,
                    Side = PostingSide.Debit,
                    Amount = SharedKernel.Money.Of(100.0000m, CurrencyCode.Sar),
                    Scope = new PostingScope("cc.001", "BR-01"),
                },
                new PostingLine
                {
                    Role = PostingRole.RoundingDifference,
                    Side = PostingSide.Credit,
                    Amount = SharedKernel.Money.Of(100.0000m, CurrencyCode.Sar),
                    Scope = new PostingScope("cc.001", "BR-01"),
                },
            ],
            Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
        };

        Result<PostingReceipt> result = await _harness.Posting.PostAsync(request, token);

        Proof.Require(result.IsFailure,
            "طلب بلا رمز حدث مرفوض — ورمزٌ فارغ في مفتاح الهوية يبتلع كل حدث ثانٍ",
            result.IsFailure
                ? result.Errors[0].Code + ": " + result.Errors[0].MessageAr
                : "رُحّل — والحارس بلا أثر");

        Proof.Require(result.Errors.Any(static e => e.Code == "ledger.posting.missing_event_code"),
            "الرفض برمز خطأ يسمّي السبب بالضبط",
            string.Join(" | ", result.Errors.Select(static e => e.Code)));

        // ولا شيء كُتب.
        List<(string EventCode, Guid EntryId, long EntryNo, decimal Debit)> written =
            await EntriesOfAsync(documentId, token);
        Proof.Require(written.Count == 0, "ولم يُكتب في الدفتر شيء",
            "عدد القيود = " + written.Count.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · الحرّاس الثلاثة ليسوا زخرفاً: كلٌّ منهم يرفض انتهاكاً محقوناً
    // ═══════════════════════════════════════════════════════════════════════
    // حارسٌ يمرّ لأن المجموعة التي يفحصها **لا يمكن** أن تحمل مخالفة ليس حارساً.
    // ولذلك تُحقن هنا مخالفة حقيقية لكل واحد، ويُقرأ رمز الرفض من PostgreSQL،
    // ثم تُلغى المعاملة فلا يبقى للحقن أثر.
    [Fact]
    public async Task Every_new_guard_refuses_an_injected_violation()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // ── أ) تعريف الفهرس في كتالوج PostgreSQL نفسه، لا في نموذج EF ─────
        string definition;
        await using (NpgsqlConnection reader = LedgerHarness.OpenApp())
        {
            await using NpgsqlCommand command = new(
                "select indexdef from pg_indexes where schemaname = 'ledger' and indexname = 'uq_posting_identity'",
                reader);
            definition = (string)(await command.ExecuteScalarAsync(token))!;
        }

        Proof.Require(definition.Contains("event_code", StringComparison.Ordinal),
            "الفهرس الفريد الحيّ يحمل رمز الحدث — مقروءاً من كتالوج PostgreSQL",
            definition);

        // ── ب) الفهرس الموسَّع لا يزال يرفض تكراراً حقيقياً ────────────────
        string documentId = "INV-RAW-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];
        Result<PostingReceipt> seed = await _harness.Posting.PostAsync(
            SalesRevenue(documentId, 1_000.0000m, 150.0000m), token);
        Proof.Require(seed.IsSuccess, "قيد أصلي لتُحقن عليه المخالفة",
            seed.IsSuccess ? "رُحّل" : string.Join(" | ", seed.Errors.Select(static e => e.MessageAr)));

        string duplicateState = await InjectAsync(
            documentId, "sales.invoice.posted", token);

        Proof.Require(duplicateState == "23505",
            "الفهرس uq_posting_identity يرفض هوية ترحيل مكرّرة — بدور المالك نفسه",
            "SQLSTATE " + duplicateState);

        // ── ج) قيد التحقق يرفض رمز حدث فارغ ───────────────────────────────
        string blankState = await InjectAsync(
            "INV-BLANK-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8],
            string.Empty, token);

        Proof.Require(blankState == "23514",
            "قيد التحقق ck_journal_entry_event_code يرفض رمز حدث فارغ",
            "SQLSTATE " + blankState);

        // ── د) وحارس الدالة نفسها: البوابة الوحيدة إلى الدفتر ──────────────
        string functionState = string.Empty;
        string functionMessage = string.Empty;
        await using (NpgsqlConnection connection = LedgerHarness.OpenApp())
        {
            try
            {
                await using NpgsqlCommand command = new(EmptyEventCodeCall, connection);
                await command.ExecuteScalarAsync(token);
            }
            catch (PostgresException exception)
            {
                functionState = exception.SqlState;
                functionMessage = exception.MessageText;
            }
        }

        Proof.Require(functionState.Length > 0 && functionMessage.Contains("MISSING_EVENT_CODE", StringComparison.Ordinal),
            "‏ledger.post_entry ترفض رمز حدث فارغ قبل أي قفل وقبل أي كتابة",
            "SQLSTATE " + functionState + ": " + functionMessage[..Math.Min(140, functionMessage.Length)]);

        // ── هـ) وإسقاط الفهرس وإعادة إنشائه ممكنان بدور المالك على جدول
        //        مملوء، مع الصلاحيات المسحوبة والمشغّل المؤجَّل قائمَين ────
        long entryRows;
        await using (NpgsqlConnection counting = LedgerHarness.OpenApp())
        {
            await using NpgsqlCommand counter = new("select count(*) from ledger.journal_entry", counting);
            entryRows = (long)(await counter.ExecuteScalarAsync(token))!;
        }

        Proof.Require(entryRows > 0, "الجدول مملوء فعلاً وقت اختبار الـDDL",
            "عدد القيود = " + entryRows.ToString(CultureInfo.InvariantCulture));

        await using (NpgsqlConnection owner = LedgerHarness.OpenOwner())
        {
            await using NpgsqlTransaction transaction = await owner.BeginTransactionAsync(token);
            await ExecAsync(owner, transaction, "drop index ledger.uq_posting_identity", token);
            await ExecAsync(owner, transaction,
                """
                create unique index uq_posting_identity on ledger.journal_entry
                    (company_id, source_doc_type, source_doc_id, posting_trigger_code, posting_generation, event_code)
                """, token);
            await transaction.RollbackAsync(token);
        }

        Proof.Pass("إسقاط الفهرس الفريد وإعادة إنشاؤه ينجحان بدور المالك — والهجرة بدور المالك",
            "على جدول فيه " + entryRows.ToString(CultureInfo.InvariantCulture)
            + " قيداً، مع REVOKE UPDATE, DELETE قائمة والمشغّل المؤجَّل مركَّباً (المعاملة أُلغيت بعدها)");

        // ── و) والدور التطبيقي لا يستطيع ذلك — الصلاحيات ليست زينة ─────────
        string appDdlState = string.Empty;
        await using (NpgsqlConnection app = LedgerHarness.OpenApp())
        {
            await using NpgsqlTransaction transaction = await app.BeginTransactionAsync(token);
            try
            {
                await ExecAsync(app, transaction, "drop index ledger.uq_posting_identity", token);
            }
            catch (PostgresException exception)
            {
                appDdlState = exception.SqlState;
            }

            await transaction.RollbackAsync(token);
        }

        Proof.Require(appDdlState == "42501",
            "ودور التطبيق لا يُسقط الفهرس — الهجرة بيد المالك وحده",
            "SQLSTATE " + appDdlState);
    }

    /// <summary>يحقن رأس قيد خاماً بدور المالك، ويُرجع رمز رفض PostgreSQL ثم يُلغي.</summary>
    private static async Task<string> InjectAsync(string documentId, string eventCode, CancellationToken token)
    {
        await using NpgsqlConnection owner = LedgerHarness.OpenOwner();
        await using NpgsqlTransaction transaction = await owner.BeginTransactionAsync(token);
        try
        {
            await using NpgsqlCommand command = new(
                """
                insert into ledger.journal_entry
                    (entry_id, company_id, book_id, fiscal_year, entry_no, entry_date, period_code, posted_at,
                     status, actor, source_module, source_doc_type, source_doc_id, posting_trigger_code,
                     posting_generation, event_code, idempotency_key, currency)
                values ($1, $2, 'IDENTITY', 2026, $3, '2026-06-15', '2026-06', now(), 'POSTED', 'raw-sql',
                        'Sales', 'SalesInvoice', $4, 'OnApproval', 1, $5, $6, 'SAR')
                """, owner, transaction);
            command.Parameters.AddWithValue(Guid.CreateVersion7());
            command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
            command.Parameters.AddWithValue(900_000L + Random.Shared.NextInt64(1, 90_000));
            command.Parameters.AddWithValue(documentId);
            command.Parameters.AddWithValue(eventCode);
            command.Parameters.AddWithValue("raw:" + documentId);
            await command.ExecuteNonQueryAsync(token);
            return string.Empty;
        }
        catch (PostgresException exception)
        {
            return exception.SqlState;
        }
        finally
        {
            await transaction.RollbackAsync(token);
        }
    }

    private static async Task ExecAsync(
        NpgsqlConnection connection, NpgsqlTransaction transaction, string sql, CancellationToken token)
    {
        await using NpgsqlCommand command = new(sql, connection, transaction);
        await command.ExecuteNonQueryAsync(token);
    }

    /// <summary>
    /// نداء <c>ledger.post_entry</c> بكل معامِلاته فارغة إلا رمز الحدث: فارغٌ عمداً.
    /// الحارس أوّل عبارة في الدالة، فلا يُقرأ أي معامِل آخر قبل الرفض.
    /// </summary>
    private const string EmptyEventCodeCall =
        """
        select out_already_posted from ledger.post_entry(
            null::uuid, null::text, null::int, null::uuid, null::date, null::text, null::timestamptz,
            null::text, null::text, null::text, null::text, null::text, null::text, null::text,
            null::text, null::text, null::text, null::int,
            ''::text,
            null::text, null::text, null::uuid, null::text, null::text, null::text, null::text,
            null::text, null::bytea, null::bytea, null::bytea, null::bytea,
            null::uuid[], null::int[], null::text[], null::text[], null::text[],
            null::numeric[], null::numeric[], null::numeric[], null::numeric[], null::numeric[],
            null::text[], null::text[], null::text[], null::text[], null::text[], null::text[], null::text[],
            null::text[], null::text[], null::text[], null::text[], null::text[],
            null::text[], null::numeric[], null::numeric[])
        """;

    // ═══════════════════════════════════════════════════════════════════════
    // القراءة من الدفتر نفسه
    // ═══════════════════════════════════════════════════════════════════════
    private static async Task<List<(string EventCode, Guid EntryId, long EntryNo, decimal Debit)>> EntriesOfAsync(
        string documentId, CancellationToken token)
    {
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            """
            select e.event_code, e.entry_id, e.entry_no,
                   (select coalesce(sum(l.debit_company), 0) from ledger.journal_line l where l.entry_id = e.entry_id)
              from ledger.journal_entry e
             where e.company_id = $1 and e.source_doc_id = $2
             order by e.entry_no
            """, connection);
        command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
        command.Parameters.AddWithValue(documentId);

        List<(string, Guid, long, decimal)> rows = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            rows.Add((reader.GetString(0), reader.GetGuid(1), reader.GetInt64(2), reader.GetDecimal(3)));
        }

        return rows;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // طلبان بمفردات الحدث — لا برقم حساب واحد
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>الاعتراف بالإيراد من فاتورة مبيعات — <c>sales.invoice.posted</c>.</summary>
    private static PostingRequest SalesRevenue(string documentId, decimal net, decimal tax) => new()
    {
        Tenant = new TenantId(LedgerTestEnvironment.TenantA),
        IdempotencyKey = new IdempotencyKey("sales-invoice:" + documentId),
        Source = new SourceDocument(BabelModule.Sales, "SalesInvoice", documentId),
        Trigger = PostingTrigger.OnApproval,
        DocumentDate = new DateOnly(2026, 6, 15),
        Narration = new LocalizedName("فاتورة مبيعات " + documentId, "Sales invoice " + documentId),
        Book = Book,
        Lines = [],
        Event = new PostingEventCode("sales.invoice.posted"),
        Amounts =
        [
            new PostingAmount("net", SharedKernel.Money.Of(net, CurrencyCode.Sar)),
            new PostingAmount("tax", SharedKernel.Money.Of(tax, CurrencyCode.Sar)),
        ],
        Facts =
        [
            new PostingFact("condition.is_taxable_supply", "true"),
            new PostingFact("subledger.customer", "CUST-D3"),
            new PostingFact("line.item_group", "*"),
        ],
        // مركز التكلفة مُحلٌّ قبل بناء الطلب — هذا ما تُسلّمه البوّابة (ADR-0026).
        Dimensions =
        [
            new PostingDimension("branch", "BR-01"),
            new PostingDimension("cost_center", Requests.DefaultCostCenter),
        ],
        Currency = CurrencyCode.Sar,
        Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
    };

    /// <summary>إنزال المخزون بالتكلفة عند الفاتورة نفسها — <c>sales.invoice.cost_of_sales</c>.</summary>
    private static PostingRequest SalesCostOfSales(string documentId, decimal cost) => new()
    {
        Tenant = new TenantId(LedgerTestEnvironment.TenantA),
        IdempotencyKey = new IdempotencyKey("sales-cogs:" + documentId),
        Source = new SourceDocument(BabelModule.Sales, "SalesInvoice", documentId),
        Trigger = PostingTrigger.OnApproval,
        DocumentDate = new DateOnly(2026, 6, 15),
        Narration = new LocalizedName("تكلفة مبيعات " + documentId, "Cost of sales " + documentId),
        Book = Book,
        Lines = [],
        Event = new PostingEventCode("sales.invoice.cost_of_sales"),
        Amounts = [new PostingAmount("cost", SharedKernel.Money.Of(cost, CurrencyCode.Sar))],
        Facts =
        [
            new PostingFact("subledger.item", "ITEM-D3"),
            new PostingFact("line.item_group", "*"),
        ],
        Dimensions =
        [
            new PostingDimension("branch", "BR-01"),
            new PostingDimension("cost_center", Requests.DefaultCostCenter),
            new PostingDimension("warehouse", "WH-01"),
        ],
        Currency = CurrencyCode.Sar,
        Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
    };
}
