using System.Globalization;
using Babel.Contracts.Posting;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>رمز حدث مُختلَق على المسار الصريح.</b>
/// <para>
/// رمز الحدث صار جزءاً من هوية الترحيل (ADR-0016). والرمز <b>الفارغ</b> مرفوض منذ ذلك
/// القرار لأنه يجعل حقيقتين محاسبيتين هويةً واحدة فيُبتلع الثاني بصمت. والرمز
/// <b>المُختلَق</b> هو الصورة المرآتية وهو أسوأ: خطأ مطبعي واحد يجعل الحقيقة الواحدة
/// <b>هويتين</b>، فيُرحَّل الأثر المحاسبي نفسه <b>مرّتين</b> بقيدين كاملين متوازنين،
/// وتبقى السلسلة سليمة والترقيم بلا فجوات — ولا يظهر شيء إلا حين ينحرف دفتر مساعد عن
/// حسابه الضابط. والازدواج الصامت في نظام محاسبي لا يقلّ ضرراً عن الفقد الصامت،
/// وهو أظهر للعميل منه بكثير.
/// </para>
/// <para>
/// و<c>PostingPlanner</c> يختار المسار بـ<c>request.Lines.Count &gt; 0</c> وحدها:
/// مسار القالب يستدعي <c>MatrixCatalog.Find</c> فيتحقق من الرمز ضمناً، والمسار الصريح
/// كان لا يمسّ المصفوفة إطلاقاً. أي أن التحقق كان <b>يبدو</b> موجوداً لأنه موجود على
/// أحد المسارين.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class InventedEventCodeTests : IAsyncLifetime
{
    /// <summary>دفتر مستقل — العدّاد والسلسلة والأرصدة بنطاق (شركة × دفتر × سنة).</summary>
    private const string Book = "INVENTED";

    /// <summary>رمز لا يوجد في المصفوفة ولن يوجد: مُختلَق عمداً.</summary>
    private const string InventedCode = "totally.invented.nonsense";

    /// <summary>الحدث المعرَّف للقيد اليدوي في <c>data/posting-matrix/events/ledger.json</c>.</summary>
    private const string ManualVoucherCode = "ledger.manual_voucher.posted";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);
        await LedgerTestEnvironment.EnsureCounterAsync(
            LedgerTestEnvironment.TenantA, Book, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · الثغرة: رمز مُختلَق يُقبل على المسار الصريح
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task رمز_حدث_ليس_في_المصفوفة_يُرفض_على_المسار_الصريح_كما_يُرفض_على_مسار_القالب()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = "JV-INVENTED-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];

        Result<PostingReceipt> result = await _harness.Posting.PostAsync(
            ManualVoucher(documentId, InventedCode), token);

        Proof.Require(
            result.IsFailure,
            "طلبٌ بسطور صريحة ورمز حدث مُختلَق مرفوض — مكوّن الهوية يُؤخذ من المصفوفة ولا يُخترع",
            result.IsFailure
                ? result.Errors[0].Code + ": " + result.Errors[0].MessageAr
                : $"رُحّل بالرمز «{InventedCode}» — القيد {result.Value.EntryNumber} وسطوره "
                  + result.Value.LineCount.ToString(CultureInfo.InvariantCulture));

        Proof.Require(
            result.Errors[0].Code == "ledger.posting.event_code_not_in_matrix",
            "الرمز الثابت يميّز «رمز خارج المصفوفة» عن كل رفض آخر",
            result.Errors[0].Code);

        Proof.Require(
            result.Errors[0].MessageAr.Contains(InventedCode, StringComparison.Ordinal),
            "الرسالة العربية تسمّي الرمز المرفوض بنصّه — فلا يُخمّن المشغّل أيّها",
            result.Errors[0].MessageAr);

        // ولا أثر في الدفتر: الرفض قبل أي كتابة.
        Proof.Require(
            await EntryCountAsync(documentId, token) == 0,
            "لا قيد كُتب بالرمز المُختلَق",
            "عدد القيود على المستند = 0");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · الحارس ليس فارغاً: المسار المشروع يمرّ كما كان
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task القيد_اليدوي_بالحدث_المعرَّف_في_المصفوفة_يُرحَّل_كما_كان()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        string documentId = "JV-LEGIT-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];

        Result<PostingReceipt> result = await _harness.Posting.PostAsync(
            ManualVoucher(documentId, ManualVoucherCode), token);

        Proof.Require(
            result.IsSuccess,
            $"القيد اليدوي بالحدث «{ManualVoucherCode}» يُرحَّل — الحارس يرفض المُختلَق ولا يرفض المشروع",
            result.IsSuccess
                ? $"القيد {result.Value.EntryNumber} بسطوره {result.Value.LineCount.ToString(CultureInfo.InvariantCulture)}"
                : string.Join(" | ", result.Errors.Select(static e => e.Code + ": " + e.MessageAr)));

        Proof.Require(
            await EntryCountAsync(documentId, token) == 1,
            "قيد واحد في الدفتر بالحدث المعرَّف",
            "عدد القيود على المستند = 1");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · كل أحداث القيود اليدوية التي يحتاجها المسار الصريح معرَّفة فعلاً
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// حارسٌ على البيانات لا على الكود: لو حُذف حدث من <c>ledger.json</c> صار الرفض
    /// أعلاه يمنع حالة ترحيل <b>مشروعة</b>. والاختبار يسمّي الأحداث بنصّها كي يُقرأ
    /// الحذف رفضاً صريحاً لا انحرافاً صامتاً.
    /// </summary>
    [Fact]
    public async Task أحداث_الدفتر_التي_يسلكها_المسار_الصريح_معرَّفة_كلّها_في_المصفوفة()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        string[] required =
        [
            "ledger.manual_voucher.posted",
            "ledger.opening_balance.posted",
            "ledger.period_close.year_end",
            "ledger.opening_entry.new_year",
            "ledger.fx_revaluation.period_end",
            "ledger.entry.reversal",
        ];

        // ‏المسبار نفسه يُثبَت أولاً: رمزٌ مُختلَق يجب أن يُبلَّغ عنه «مفقوداً». ولولا
        // ذلك لمرّ هذا الاختبار فارغاً لو تعطّل الرفض أو تغيّر رمز الخطأ.
        Proof.Require(
            await IsMissingAsync(InventedCode, token),
            "المسبار يرى الغياب فعلاً — رمزٌ مُختلَق يُبلَّغ عنه مفقوداً",
            InventedCode);

        List<string> missing = [];
        foreach (string code in required)
        {
            if (await IsMissingAsync(code, token))
            {
                missing.Add(code);
            }
        }

        Proof.Require(
            required.Length == 6 && missing.Count == 0,
            "كل أحداث الدفتر الستّة معرَّفة في المصفوفة — فالرفض لا يمنع حالة مشروعة",
            missing.Count == 0 ? "لا رمز مفقود" : "مفقود: " + string.Join(" · ", missing));
    }

    /// <summary>هل يرفض المحرك هذا الرمز لأنّه ليس في المصفوفة؟</summary>
    private async Task<bool> IsMissingAsync(string code, CancellationToken token)
    {
        string documentId = "PROBE-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];
        Result<PostingReceipt> probe = await _harness.Posting.PostAsync(ManualVoucher(documentId, code), token);
        return probe.IsFailure && probe.Errors[0].Code == "ledger.posting.event_code_not_in_matrix";
    }

    // ═══════════════════════════════════════════════════════════════════════
    // طلب قيد يومية يدوي بسطور صريحة — الرمز يعطي الهوية والسطور تعطي المحتوى
    // ═══════════════════════════════════════════════════════════════════════
    private static PostingRequest ManualVoucher(string documentId, string eventCode) => new()
    {
        Tenant = new TenantId(LedgerTestEnvironment.TenantA),
        IdempotencyKey = new IdempotencyKey("manual:" + documentId + ":" + eventCode),
        Source = new SourceDocument(BabelModule.Ledger, "ManualVoucher", documentId),
        Trigger = PostingTrigger.OnApproval,
        DocumentDate = new DateOnly(2026, 6, 15),
        Narration = new LocalizedName("قيد يومية يدوي", "Manual journal voucher"),
        Book = Book,
        Currency = CurrencyCode.Sar,
        Event = new PostingEventCode(eventCode),
        Lines =
        [
            new PostingLine
            {
                Role = PostingRole.RoundingDifference,
                Side = PostingSide.Debit,
                Amount = SharedKernel.Money.Of(100.0000m, CurrencyCode.Sar),
                Scope = new PostingScope("BR-01", null, null),
            },
            new PostingLine
            {
                Role = PostingRole.RoundingDifference,
                Side = PostingSide.Credit,
                Amount = SharedKernel.Money.Of(100.0000m, CurrencyCode.Sar),
                Scope = new PostingScope("BR-01", null, null),
            },
        ],
        Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
    };

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
