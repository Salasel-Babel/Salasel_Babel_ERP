using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Vouchers;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>القيدُ اليدوي يسمّي حساباً — والبوّابة بيانيةٌ، والفحوصُ هي هي</b> (ADR-0096).
/// <para>
/// سطرُ الحساب يدخل من سطح الدفتر وحده، ولا يُقبل إلا على حدثٍ تُعلن المصفوفةُ سطورَه
/// <c>manual</c>. وما بعد البوّابة هو خطُّ الحلّ نفسه الذي يمرّ به سطرُ الدور: الحساب
/// موجود، وعامل، وتفصيلي، وأبعاده حاضرة، وطرفُه حاضر — والحرّاسُ المقيَّدون بالدور تُقيَّم
/// على سطر الحساب لكل دورٍ تحوّله الخريطة إليه، فلا يُلتفّ على GR-RE-001 باختيار الحساب.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class ManualVoucherNamesAnAccountTests : IAsyncLifetime
{
    /// <summary>دفتر مستقل — العدّاد والسلسلة والأرصدة بنطاق (شركة × دفتر × سنة).</summary>
    private const string Book = "MANUALACC";

    private const string ManualVoucherCode = "ledger.manual_voucher.posted";

    /// <summary>حدثٌ من الدفتر سطورُه <c>import</c> لا <c>manual</c> — البوّابة بيانية لا بالوحدة.</summary>
    private const string ImportEventCode = "ledger.opening_balance.posted";

    /// <summary>نقد في الطريق — تفصيلي، بلا بُعد إلزامي وبلا دفتر مساعد.</summary>
    private const string TransitAccount = "1204";

    /// <summary>حساب معلَّق لأرصدة الافتتاح — تفصيلي بلا شروط.</summary>
    private const string SuspenseAccount = "3901";

    /// <summary>حسابٌ تجميعي.</summary>
    private const string RollupAccount = "110";

    /// <summary>إيرادات الإيجار — بُعد العقار إلزامي، والخريطة تحوّل rental_revenue إليه.</summary>
    private const string RentalRevenueAccount = "4301";

    /// <summary>ذمم العملاء — حساب ضابط لدفتر مساعد.</summary>
    private const string CustomersControlAccount = "1301";

    /// <summary>احتياطيات أخرى — يُعطَّل في اختبار واحد ولا يمسّه غيره.</summary>
    private const string ReservesAccount = "3152";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);
        await LedgerTestEnvironment.EnsureCounterAsync(
            LedgerTestEnvironment.TenantA, Book, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · سطرُ الحساب على حدث القيد اليدوي يُرحَّل — والسطرُ المكتوب يحمل الحساب لا دوراً
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task سطر_الحساب_على_حدث_القيد_اليدوي_يُرحَّل_ويُكتب_برمز_حسابه_بلا_دور()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = Document("POST");

        Result<PostingReceipt> result = await _harness.Vouchers.PostAsync(
            Header(documentId, ManualVoucherCode),
            [Line(TransitAccount, PostingSide.Debit), Line(SuspenseAccount, PostingSide.Credit)],
            token);

        Proof.Require(
            result.IsSuccess,
            "قيدٌ يدوي سطورُه تسمّي حساباتها يُرحَّل على حدثه المعرَّف",
            result.IsSuccess
                ? $"القيد {result.Value.EntryNumber} بسطوره {result.Value.LineCount.ToString(CultureInfo.InvariantCulture)}"
                : string.Join(" | ", result.Errors.Select(static e => e.Code + ": " + e.MessageAr)));

        List<(string Account, string Role)> written = await LinesAsync(result.Value.JournalEntryId, token);

        Proof.Require(
            written.Count == 2 && written[0] == (TransitAccount, string.Empty) && written[1] == (SuspenseAccount, string.Empty),
            "السطران المكتوبان يحملان رمزَي الحساب المسمَّيَين ودوراً فارغاً — لا دورٌ مُخترَع",
            string.Join(" · ", written.Select(static w => $"{w.Account}/«{w.Role}»")));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · البوّابة بيانية: حدثٌ من الدفتر نفسه سطورُه ليست يدوية يرفض سطر الحساب
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task سطر_الحساب_على_حدث_لا_تُعلن_المصفوفة_سطوره_يدوية_يُرفض_برمزه()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        Result<PostingReceipt> result = await _harness.Vouchers.PostAsync(
            Header(Document("IMPORT"), ImportEventCode),
            [Line(TransitAccount, PostingSide.Debit), Line(SuspenseAccount, PostingSide.Credit)],
            token);

        Require(result, "ledger.posting.event_takes_no_manual_lines",
            "حدثُ أرصدة الافتتاح (import) يرفض سطور الحساب وإن كان من الدفتر — البوّابة من المصفوفة لا من اسم الوحدة");

        Proof.Require(
            result.Errors[0].MessageAr.Contains(ImportEventCode, StringComparison.Ordinal),
            "الرسالة تسمّي الحدث المرفوض بنصّه",
            result.Errors[0].MessageAr);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · فحوصُ الدليل هي هي: مجهول · معطَّل · تجميعي · بُعد ناقص · طرف ناقص
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task حساب_مجهول_يُرفض_بالرمز_القائم()
    {
        Result<PostingReceipt> result = await PostPairAsync("9999999", SuspenseAccount, Document("UNKNOWN"));
        Require(result, "ledger.posting.unknown_account", "حسابٌ ليس في الدليل يُرفض كما يُرفض دورٌ حُلّ إلى حساب مجهول");
    }

    [Fact]
    public async Task حساب_معطَّل_يُرفض_بالرمز_القائم()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        // يُعطَّل بدور المالك ثم تُسقَط اللقطة — كما يفعل المالك حين يُعطّل حساباً فعلاً.
        await using (NpgsqlConnection owner = LedgerHarness.OpenOwner())
        {
            await using NpgsqlCommand command = new(
                "update ledger.account set is_active = false where company_id = $1 and account_code = $2", owner);
            command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
            command.Parameters.AddWithValue(ReservesAccount);
            await command.ExecuteNonQueryAsync(token);
        }

        _harness.Runtime.InvalidateReference(LedgerTestEnvironment.TenantA);

        Result<PostingReceipt> result = await PostPairAsync(TransitAccount, ReservesAccount, Document("INACTIVE"));
        Require(result, "ledger.posting.inactive_account", "حسابٌ معطَّل يُرفض — الحساب لا يُحذف بل يُعطَّل، والترحيل عليه مرفوض");
    }

    [Fact]
    public async Task حساب_تجميعي_يُرفض_بقاعدة_GR_COA_001()
    {
        Result<PostingReceipt> result = await PostPairAsync(RollupAccount, SuspenseAccount, Document("ROLLUP"));
        Require(result, "ledger.posting.guard.GR-COA-001", "الحساب التجميعي مرفوض على سطر الحساب كما على سطر الدور");
    }

    [Fact]
    public async Task حساب_ببُعد_إلزامي_غائب_يُرفض_بقاعدة_GR_COA_002()
    {
        Result<PostingReceipt> result = await PostPairAsync(TransitAccount, RentalRevenueAccount, Document("NODIM"));
        Require(result, "ledger.posting.guard.GR-COA-002", "إيراد الإيجار بلا بُعد العقار مرفوض — البُعد الإلزامي من الدليل يسري على سطر الحساب");
    }

    [Fact]
    public async Task حساب_ضابط_بلا_طرف_يُرفض_بالرمز_القائم()
    {
        Result<PostingReceipt> result = await PostPairAsync(CustomersControlAccount, SuspenseAccount, Document("NOPARTY"));
        Require(result, "ledger.posting.missing_subledger", "الحساب الضابط بلا مرجع طرف مرفوض — المطابقة اليومية لا تنكسر من سطر يدوي");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · الحارس المقيَّد بالدور لا يُلتفّ عليه باختيار الحساب مباشرة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GR_RE_001_تحجب_حساب_إيراد_الإيجار_المسمَّى_مباشرةً_على_عقار_مُدار_لصالح_الغير()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = Document("GRRE");

        Result<PostingReceipt> result = await _harness.Vouchers.PostAsync(
            Header(documentId, ManualVoucherCode),
            [
                Line(TransitAccount, PostingSide.Debit),
                Line(RentalRevenueAccount, PostingSide.Credit) with
                {
                    Dimensions = [new PostingDimension("property", LedgerTestEnvironment.ManagedProperty)],
                },
            ],
            token);

        bool blocked = result.IsFailure && result.Errors.Any(static e => e.Code == "ledger.posting.guard.GR-RE-001");
        Proof.Require(
            blocked,
            "GR-RE-001 تحجب الحساب 4301 المسمَّى مباشرةً — الدور rental_revenue مُشتقّ من الخريطة معكوسةً",
            result.IsFailure
                ? string.Join(" | ", result.Errors.Select(static e => e.Code))
                : $"القيد {result.Value.EntryNumber} رُحّل — والحارس التُفَّ عليه");

        Proof.Require(
            await EntryCountAsync(documentId, token) == 0,
            "لا قيد كُتب على العقار المُدار",
            "عدد القيود على المستند = 0");

        // والعقار المملوك يمرّ بالحساب نفسه: الحارس يحجب الشرط لا الحساب.
        string ownDocument = Document("GROWN");
        Result<PostingReceipt> allowed = await _harness.Vouchers.PostAsync(
            Header(ownDocument, ManualVoucherCode),
            [
                Line(TransitAccount, PostingSide.Debit),
                Line(RentalRevenueAccount, PostingSide.Credit) with
                {
                    Dimensions = [new PostingDimension("property", LedgerTestEnvironment.OwnProperty)],
                },
            ],
            token);

        Proof.Require(
            allowed.IsSuccess,
            "الحساب نفسه على عقار مملوك يُرحَّل — الحارس حجب الشرط لا الحساب",
            allowed.IsSuccess
                ? $"القيد {allowed.Value.EntryNumber}"
                : string.Join(" | ", allowed.Errors.Select(static e => e.Code + ": " + e.MessageAr)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // أدوات
    // ═══════════════════════════════════════════════════════════════════════
    private static void Require(Result<PostingReceipt> result, string code, string title)
    {
        Proof.Require(
            result.IsFailure && result.Errors.Any(e => e.Code == code),
            title,
            result.IsFailure
                ? string.Join(" | ", result.Errors.Select(static e => e.Code + ": " + e.MessageAr))
                : $"القيد {result.Value.EntryNumber} رُحّل");
    }

    private async Task<Result<PostingReceipt>> PostPairAsync(string debitAccount, string creditAccount, string documentId)
        => await _harness.Vouchers.PostAsync(
            Header(documentId, ManualVoucherCode),
            [Line(debitAccount, PostingSide.Debit), Line(creditAccount, PostingSide.Credit)],
            TestContext.Current.CancellationToken);

    private static string Document(string prefix)
        => "JV-ACC-" + prefix + "-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];

    private static ManualVoucherLine Line(string accountCode, PostingSide side) => new()
    {
        AccountCode = accountCode,
        Side = side,
        Amount = SharedKernel.Money.Of(100.0000m, CurrencyCode.Sar),
        Scope = new PostingScope("cc.001", "BR-01"),
    };

    /// <summary>الترويسة: الهوية والسياق — وسطورُها فارغة، فالسطور تُسلَّم بجوارها.</summary>
    private static PostingRequest Header(string documentId, string eventCode) => new()
    {
        Tenant = new TenantId(LedgerTestEnvironment.TenantA),
        IdempotencyKey = new IdempotencyKey("manual-account:" + documentId + ":" + eventCode),
        Source = new SourceDocument(BabelModule.Ledger, "ManualVoucher", documentId),
        Trigger = PostingTrigger.OnApproval,
        DocumentDate = new DateOnly(2026, 6, 15),
        Narration = new LocalizedName("قيد يومية يدوي بحسابات مسمّاة", "Manual journal voucher naming its accounts"),
        Book = Book,
        Currency = CurrencyCode.Sar,
        Event = new PostingEventCode(eventCode),
        Lines = [],
        Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
    };

    private static async Task<List<(string Account, string Role)>> LinesAsync(Guid entryId, CancellationToken token)
    {
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            "select account_code, role_code from ledger.journal_line where entry_id = $1 order by line_no", connection);
        command.Parameters.AddWithValue(entryId);

        List<(string, string)> lines = [];
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        while (await reader.ReadAsync(token))
        {
            lines.Add((reader.GetString(0), reader.GetString(1)));
        }

        return lines;
    }

    private static async Task<long> EntryCountAsync(string documentId, CancellationToken token)
    {
        await using NpgsqlConnection connection = LedgerHarness.OpenApp();
        await using NpgsqlCommand command = new(
            "select count(*) from ledger.journal_entry where company_id = $1 and source_doc_id = $2",
            connection);
        command.Parameters.AddWithValue(LedgerTestEnvironment.TenantA);
        command.Parameters.AddWithValue(documentId);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }
}
