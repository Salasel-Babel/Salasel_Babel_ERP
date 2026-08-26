using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Audit;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>لا سطر يبلغ الدفتر بلا مركز تكلفة — على المسارين، وبطبقتين.</b>
/// <para>
/// ‏ADR-0026: لكل منشأة مركز تكلفة واحد على الأقل، و<c>CostCenterId</c> لا يكون فارغاً
/// في أي موضع. وكان القرار مفروضاً عند <b>التأسيس</b> فقط، بينما العمود يقبل
/// <c>null</c> والنوع يقول <c>string?</c>. وهذه المجموعة تفحص الطبقتين اللتين أُضيفتا:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>المخطِّط يرفض ويُسمّي</b> — <c>ledger.posting.missing_cost_center</c> بالسطر
///     والدور، فيقرأ من يُصلح ما يُصلحه بدل رسالة <c>23514</c> خامّ تسمّي جدولاً.
///   </description></item>
///   <item><description>
///     <b>وقاعدة البيانات ترفض أي كاتب</b> — نصّ SQL خام بدور التطبيق يُرفض بالقيد نفسه.
///     وهذا هو الفرق بين ثابتة تُصان بالانضباط وثابتة يفرضها المخطّط.
///   </description></item>
/// </list>
/// </summary>
[Collection("ledger")]
public sealed class CostCenterNeverAbsentOnALineTests : IAsyncLifetime
{
    private const string Book = "COSTCENTER";

    /// <summary>دفترٌ مستقلّ للعبث — سلسلةٌ تُكسَر عمداً لا تُشارَك مع أحد.</summary>
    private const string TamperBook = "CCTAMPER";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);
        await LedgerTestEnvironment.EnsureCounterAsync(
            LedgerTestEnvironment.TenantA, Book, TestContext.Current.CancellationToken);
        await LedgerTestEnvironment.EnsureCounterAsync(
            LedgerTestEnvironment.TenantA, TamperBook, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // ١ · مسار القالب: بُعد cost_center غائب ⇒ رفضٌ يُسمّي السطر والدور
    // ═══════════════════════════════════════════════════════════════════════

    [Fact]
    public async Task طلبٌ_على_مسار_القالب_بلا_بُعد_مركز_تكلفة_يُرفض_باسمه_لا_برمز_قاعدة_بيانات()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        // ‏**المعرّف كاملاً لا أوائله**: أول ثماني خانات في GUID v7 هي الطابع الزمني،
        // فمعرّفان يُولَّدان في المللي ثانية نفسها يتطابقان — وقد تطابقا هنا فعلاً،
        // فقرأ هذا الاختبار قيدَ جاره وحكم بأن الرفض لم يقع.
        string documentId = "CC-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);

        Result<PostingReceipt> result = await _harness.Posting.PostAsync(
            WithoutCostCenter(documentId), token);

        Proof.Require(result.IsFailure, "الطلب مرفوض", Describe(result));

        Error first = result.Errors[0];
        Proof.Require(
            first.Code == "ledger.posting.missing_cost_center",
            "والرمز يصف السبب لا العَرَض", first.Code);

        Proof.Require(
            first.MessageAr.Contains("مركز تكلفة", StringComparison.Ordinal)
            && first.MessageAr.Contains("السطر", StringComparison.Ordinal),
            "والرسالة العربية تسمّي السطر — فمن يُصلح يعرف ماذا يُصلح", first.MessageAr);

        // ولا أثر في الدفتر: الرفض قبل أي كتابة، لا بعد نصفها.
        long written = await CountAsync(
            "select count(*) from ledger.journal_entry where source_doc_id = $1", documentId);

        Proof.Require(
            written == 0,
            "ولا قيد كُتب",
            documentId + " ⇒ " + written.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// <b>والطلب نفسه بمركز تكلفة يُرحَّل</b> — فالرفض أعلاه ليس رفضاً لكل شيء.
    /// حارسٌ يرفض المشروع مع الممنوع لا يحرس، يعطّل.
    /// </summary>
    [Fact]
    public async Task والطلب_نفسه_بمركز_تكلفة_يُرحَّل_ويصل_المركز_إلى_السطر_كما_أُرسل()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        // ‏**المعرّف كاملاً لا أوائله**: أول ثماني خانات في GUID v7 هي الطابع الزمني،
        // فمعرّفان يُولَّدان في المللي ثانية نفسها يتطابقان — وقد تطابقا هنا فعلاً،
        // فقرأ هذا الاختبار قيدَ جاره وحكم بأن الرفض لم يقع.
        string documentId = "CC-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);

        PostingRequest request = WithoutCostCenter(documentId) with
        {
            Dimensions =
            [
                new PostingDimension("cost_center", Requests.DefaultCostCenter),
                new PostingDimension("property", LedgerTestEnvironment.OwnProperty),
                new PostingDimension("unit", "U-01"),
            ],
        };

        Result<PostingReceipt> result = await _harness.Posting.PostAsync(request, token);

        Proof.Require(result.IsSuccess, "الطلب نفسه بمركز تكلفة يُرحَّل", Describe(result));

        long lines = await CountAsync(
            """
            select count(*) from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where e.source_doc_id = $1 and l.cost_center_id = 'cc.001'
            """, documentId);

        Proof.Require(lines > 0, "والمركز وصل إلى كل سطر كما أُرسل", lines.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٢ · وقاعدة البيانات ترفض أي كاتب — لا من يمرّ بـC# فحسب
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>‏SQL خام بدور التطبيق: سطرٌ بلا مركز مرفوض بالقيد نفسه.</b>
    /// <para>
    /// وهذا هو معنى «ثابتة مفروضة بالمخطّط»: أداة استيراد، أو نصّ صيانة، أو هجرة سهت،
    /// كلها تُرفض. ولو كان الحارس في C# وحدها لكان أولها يمرّ.
    /// </para>
    /// </summary>
    [Fact]
    public async Task إدراج_SQL_خام_بلا_مركز_تكلفة_مرفوض_بالقيد_نفسه_والخواء_مثل_الفراغ()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        foreach ((string label, string? centre) in new (string, string?)[]
                 {
                     ("فراغ صريح (null)", null),
                     ("خواء (مسافات)", "   "),
                 })
        {
            string refusal = await RawInsertAsync(centre, token);

            Proof.Require(
                refusal.Contains("23514", StringComparison.Ordinal)
                && refusal.Contains("ck_journal_line_cost_center_present", StringComparison.Ordinal),
                "قاعدة البيانات ترفض — " + label,
                refusal);
        }

        // ولا ترفض ما ليس مخالفة: قيدٌ يرفض كل شيء لا يميّز شيئاً.
        string accepted = await RawInsertAsync("cc.001", token);
        Proof.Require(
            accepted.Contains("23514", StringComparison.Ordinal)
            && !accepted.Contains("ck_journal_line_cost_center_present", StringComparison.Ordinal),
            "وسطرٌ بمركز يعبر هذا القيد — ويسقط عند مشغّل التوازن وحده، وهو حارس آخر",
            accepted);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ٣ · ولماذا لا تُعبَّأ السطور القديمة: العمود **داخل البايتات المُجزَّأة**
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// <b>كتابة مركز تكلفة على سطر مُرحَّل تجعل دفتراً سليماً يُبلّغ عن عبث.</b>
    /// <para>
    /// وهذا هو الدليل الذي تقوم عليه هجرة <c>CostCenterIsNeverAbsentOnAJournalLine</c>:
    /// لا تُعبَّأ سطورٌ سبقت الثابتة، ولا يُخترَع لها مركز. والسبب ليس تحفّظاً محاسبياً
    /// وحده — بل أن <c>cost_center</c> حقلٌ في الشكل القانوني، وإعادة التحقق من السلسلة
    /// تُعيد بناء البايتات من هذا الصفّ نفسه (‏ADR-0007). فالهجرة «اللطيفة» التي تملأ
    /// الفراغ تُنتج دفتراً <b>يُعلن أنه مُتلاعَب به</b> عند أول تدقيق.
    /// </para>
    /// <para>
    /// والاختبار يفعل العبث <b>بدور المالك</b>: دور التطبيق لا يملك <c>UPDATE</c> أصلاً
    /// (‏ADR-0003)، وهجرةٌ تعمل بدور المالك تملكه — فالمحاكاة هنا هي حالة الهجرة بالضبط.
    /// </para>
    /// </summary>
    [Fact]
    public async Task تعبئة_مركز_تكلفة_على_سطر_مُرحَّل_تجعل_السلسلة_تُبلّغ_عن_عبث()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = "CC-TAMPER-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture);

        PostingRequest request = WithoutCostCenter(documentId) with
        {
            IdempotencyKey = new IdempotencyKey("cc-tamper:" + documentId),
            Book = TamperBook,
            Dimensions =
            [
                new PostingDimension("cost_center", Requests.DefaultCostCenter),
                new PostingDimension("property", LedgerTestEnvironment.OwnProperty),
                new PostingDimension("unit", "U-01"),
            ],
        };

        Result<PostingReceipt> posted = await _harness.Posting.PostAsync(request, token);
        Proof.Require(posted.IsSuccess, "قيدٌ مُرحَّل سليم", Describe(posted));

        Result<LedgerChainReport> before = await _harness.Auditing.VerifyChainAsync(
            new TenantId(LedgerTestEnvironment.TenantA), UserId.SystemActor, TamperBook, 2026, token);

        Proof.Require(before.IsSuccess && before.Value.Ok, "والسلسلة سليمة قبل العبث", before.Value.Verdict);

        // ── العبث: تعبئة المركز على سطر مُرحَّل، كما كانت هجرة «لطيفة» ستفعل ──
        int affected = await TamperAsync(posted.Value.JournalEntryId, token);
        Proof.Require(affected > 0, "كُتب مركزٌ على سطر مُرحَّل", affected.ToString(CultureInfo.InvariantCulture));

        Result<LedgerChainReport> after = await _harness.Auditing.VerifyChainAsync(
            new TenantId(LedgerTestEnvironment.TenantA), UserId.SystemActor, TamperBook, 2026, token);

        Proof.Require(
            after.IsSuccess && !after.Value.Ok,
            "والسلسلة صارت تُبلّغ عن عبث — فالهجرة التي «تُصلح» العمود تُتلف الدفتر",
            after.Value.Verdict + " · أول تسلسل منحرف: " + (after.Value.FirstDivergentSequence?.ToString(CultureInfo.InvariantCulture) ?? "—"));
    }

    private static async Task<int> TamperAsync(Guid entryId, CancellationToken token)
    {
        await using NpgsqlConnection owner = new(LedgerTestEnvironment.Options.OwnerConnectionString);
        await owner.OpenAsync(token);

        await using NpgsqlCommand command = new(
            "update ledger.journal_line set cost_center_id = 'cc.999' where entry_id = $1", owner);
        command.Parameters.AddWithValue(entryId);

        return await command.ExecuteNonQueryAsync(token);
    }

    // ═══════════════════════════════════════════════════════════════════════

    private static PostingRequest WithoutCostCenter(string documentId) => new()
    {
        Tenant = new TenantId(LedgerTestEnvironment.TenantA),
        IdempotencyKey = new IdempotencyKey("cc-missing:" + documentId),
        Source = new SourceDocument(BabelModule.RealEstate, "RentInvoice", documentId),
        Trigger = PostingTrigger.OnApproval,
        DocumentDate = new DateOnly(2026, 8, 15),
        Narration = new LocalizedName("فاتورة إيجار بلا مركز تكلفة", "Rent invoice without a cost centre"),
        Book = Book,
        Lines = [],
        Event = new PostingEventCode("realestate.rent_invoice.own_property"),
        Amounts =
        [
            new PostingAmount("net", Money.Of(1_000m, CurrencyCode.Sar)),
            new PostingAmount("tax", Money.Of(150m, CurrencyCode.Sar)),
        ],
        Facts =
        [
            new PostingFact("unit.vat_treatment", "standard"),
            new PostingFact("subledger.tenant", "TEN-" + documentId),
            new PostingFact("subledger.lease_contract", "LC-" + documentId),
        ],
        // ‏**لا بُعد cost_center** — وهذا هو الشكل الذي كان يمرّ فيُكتب null في العمود.
        Dimensions =
        [
            new PostingDimension("property", LedgerTestEnvironment.OwnProperty),
            new PostingDimension("unit", "U-01"),
        ],
        Currency = CurrencyCode.Sar,
        Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
    };

    private static string Describe(Result<PostingReceipt> result) => result.IsSuccess
        ? "نجح"
        : string.Join(" | ", result.Errors.Select(static error => error.Code));

    private static async Task<long> CountAsync(string sql, string parameter)
    {
        await using NpgsqlConnection connection = new(LedgerTestEnvironment.Options.AppConnectionString);
        await connection.OpenAsync(TestContext.Current.CancellationToken);
        await using NpgsqlCommand command = new(sql, connection);
        command.Parameters.AddWithValue(parameter);
        object? value = await command.ExecuteScalarAsync(TestContext.Current.CancellationToken);
        return value is null or DBNull ? 0 : (long)value;
    }

    /// <summary>
    /// يحاول كتابة سطر خام بدور التطبيق ويُعيد رمز الرفض. والمعاملة تُلغى دائماً:
    /// الاختبار يفحص الرفض لا يبني حالة.
    /// </summary>
    private static async Task<string> RawInsertAsync(string? centre, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(LedgerTestEnvironment.Options.AppConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlTransaction transaction = await connection.BeginTransactionAsync(token);
        Guid entryId = Guid.CreateVersion7();

        try
        {
            await PostingEngineTests.InsertRawEntryAsync(
                connection, transaction, LedgerTestEnvironment.TenantA, "TRIG", entryId, 900_000 + Random.Shared.Next(90_000), token);

            await using NpgsqlCommand command = new(
                """
                insert into ledger.journal_line
                    (line_id, entry_id, line_no, company_id, account_code, role_code, qualifier,
                     debit, credit, currency, fx_rate, debit_company, credit_company, cost_center_id)
                select $1, $2, 1, e.company_id, '1310', '', '*', 100.0000, 0, 'SAR', 1, 100.0000, 0, $3
                  from ledger.journal_entry e where e.entry_id = $2
                """, connection, transaction);

            command.Parameters.AddWithValue(Guid.CreateVersion7());
            command.Parameters.AddWithValue(entryId);
            command.Parameters.Add(new NpgsqlParameter { Value = (object?)centre ?? DBNull.Value });

            await command.ExecuteNonQueryAsync(token);
            await transaction.CommitAsync(token);
            return "لم يُرفض إطلاقاً";
        }
        catch (PostgresException exception)
        {
            return exception.SqlState + ": " + exception.MessageText;
        }
        finally
        {
            // ‏DisposeAsync على معاملة مُلغاة أو مُثبَّتة لا يفعل شيئاً، وعلى معاملة
            // مفتوحة يُلغيها — فلا حاجة إلى سؤال عن حالتها.
            await transaction.DisposeAsync();
        }
    }
}
