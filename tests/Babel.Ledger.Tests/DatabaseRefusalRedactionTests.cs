using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Ledger.Posting;
using Babel.SharedKernel;
using Microsoft.Extensions.Logging;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// <b>رفض قاعدة البيانات: التصنيف يعبر والنصّ لا يعبر — والتشخيص لا يضيع.</b>
/// <para>
/// كان محرّك الترحيل يبني الخطأ المجالي من <c>PostgresException.MessageText</c> الخام،
/// وهو نصّ يحمل اسم القيد واسم الجدول وأحياناً قيمة الصفّ المخالف. وشكل الخطأ كان
/// <b>صحيحاً تماماً</b> — رمز ثابت ورسالتان — فلا شيء في النوع نفسه يُنبّه؛ الحمولة
/// وحدها كانت خاطئة. وحدّ HTTP كان يحجب ذلك عند نفسه، لكن الخطأ المجالي يصل إلى كل
/// مستدعٍ: معالج رسالة، ومهمة مجدولة، وتقرير — ولا شيء منها يمرّ بذلك الحدّ.
/// </para>
/// <para>
/// والرفض المستعمل هنا <b>حقيقي وكامل المسار</b>: <c>posting_generation = 0</c> لا
/// يفحصه المخطِّط، فيصل إلى <c>ck_journal_entry_generation</c> في PostgreSQL ويعود
/// <c>23514</c> باسم القيد داخل نصّه.
/// </para>
/// </summary>
[Collection("ledger")]
public sealed class DatabaseRefusalRedactionTests : IAsyncLifetime
{
    private const string Book = "REDACT";
    private const string Constraint = "ck_journal_entry_generation";

    private LedgerHarness _harness = null!;

    public async ValueTask InitializeAsync()
    {
        _harness = await LedgerHarness.CreateAsync(TestContext.Current.CancellationToken);
        await LedgerTestEnvironment.EnsureCounterAsync(
            LedgerTestEnvironment.TenantA, Book, TestContext.Current.CancellationToken);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    [Fact]
    public async Task رفض_قاعدة_البيانات_لا_يحمل_اسم_القيد_إلى_المُستدعي_ويحمله_كاملاً_إلى_السجلّ()
    {
        CancellationToken token = TestContext.Current.CancellationToken;

        CapturingLogger log = new();
        PostingService posting = new(new AlwaysEntitled(), _harness.Runtime, log);

        string documentId = "JV-REDACT-" + Guid.CreateVersion7().ToString("N", CultureInfo.InvariantCulture)[..8];
        Result<PostingReceipt> result = await posting.PostAsync(RefusedVoucher(documentId), token);

        Proof.Require(
            result.IsFailure && result.Errors[0].Code == "ledger.posting.database.23514",
            "الرفض وصل من قاعدة البيانات فعلاً — لا من فحص في الكود",
            result.IsFailure
                ? result.Errors[0].Code
                : "رُحّل — والمسار لم يبلغ قاعدة البيانات، فالاختبار يفحص لا شيء");

        Error error = result.Errors[0];

        // ── الحارس ليس فارغاً: النصّ الخام يحمل اسم القيد فعلاً ────────────────
        Proof.Require(
            log.Entries.Count == 1,
            "سطر تشخيص واحد كُتب في السجلّ",
            log.Entries.Count.ToString(CultureInfo.InvariantCulture) + " سطراً");

        LogEntry entry = log.Entries[0];

        Proof.Require(
            entry.Exception?.Message.Contains(Constraint, StringComparison.Ordinal) == true,
            "النصّ الخام يحمل اسم القيد فعلاً — فالحجب يحجب شيئاً موجوداً لا فراغاً",
            entry.Exception?.Message ?? "(لا استثناء)");

        // ── الرسالة المعروضة: لا اسم قيد ولا جدول ولا مخطّط ──────────────────
        string[] mustNotAppear = [Constraint, "journal_entry", "ledger.", "constraint", "23514: "];
        List<string> leaked =
        [
            .. mustNotAppear.Where(needle =>
                error.MessageAr.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || error.MessageEn.Contains(needle, StringComparison.OrdinalIgnoreCase)),
        ];

        Proof.Require(
            leaked.Count == 0,
            "الرسالتان المعروضتان لا تحملان اسم قيد ولا اسم جدول ولا اسم مخطّط",
            leaked.Count == 0 ? error.MessageAr : "تسرّب: " + string.Join(" · ", leaked));

        // ── والتصنيف يعبر: SQLSTATE في الرمز وفي الرسالة ─────────────────────
        Proof.Require(
            error.MessageAr.Contains("23514", StringComparison.Ordinal)
            && error.MessageEn.Contains("23514", StringComparison.Ordinal),
            "‏SQLSTATE يعبر — تصنيف معياري لا معلومة مخطّط",
            error.Code);

        // ── والربط ممكن: معرّف التشخيص نفسه في الرسالة وفي السجلّ ────────────
        string diagnosticId = DiagnosticIdOf(error.MessageAr);

        Proof.Require(
            diagnosticId.Length == 32,
            "الرسالة تحمل معرّف تشخيص واحداً بصيغة ثابتة",
            diagnosticId.Length == 32 ? diagnosticId : "لم يُعثر على معرّف — " + error.MessageAr);

        Proof.Require(
            entry.Text.Contains(diagnosticId, StringComparison.Ordinal),
            "المعرّف نفسه في سطر السجلّ — فمن يملك السجلّ يربط الرسالة بالتفصيل",
            diagnosticId);

        Proof.Require(
            entry.Text.Contains(Constraint, StringComparison.Ordinal)
            && entry.State.Any(pair => pair.Key == "ConstraintName" && (pair.Value as string) == Constraint),
            "سطر السجلّ يحمل اسم القيد — حقلاً مسمّى لا نصّاً مدموجاً فقط",
            string.Join(
                " · ",
                entry.State
                    .Where(static pair => pair.Key is "DiagnosticId" or "SqlState" or "ConstraintName" or "TableName")
                    .Select(static pair => pair.Key + "=" + pair.Value)));
    }

    /// <summary>
    /// أوّل سلسلة من 32 محرفاً ست‑عشرياً صغيراً في الرسالة — هي معرّف التشخيص.
    /// </summary>
    private static string DiagnosticIdOf(string message)
    {
        foreach (string word in message.Split([' ', '\n', '\r', '،', '.'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (word.Length == 32 && word.All(static c => c is (>= '0' and <= '9') or (>= 'a' and <= 'f')))
            {
                return word;
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// قيد يدوي سليم في كل شيء إلا <c>Generation = 0</c>. والمخطِّط لا يفحص الجيل،
    /// فيصل الطلب إلى <c>ck_journal_entry_generation</c> ويُرفض هناك.
    /// </summary>
    private static PostingRequest RefusedVoucher(string documentId) => new()
    {
        Tenant = new TenantId(LedgerTestEnvironment.TenantA),
        IdempotencyKey = new IdempotencyKey("redact:" + documentId),
        Source = new SourceDocument(BabelModule.Ledger, "ManualVoucher", documentId),
        Trigger = PostingTrigger.OnApproval,
        DocumentDate = new DateOnly(2026, 6, 15),
        Narration = new LocalizedName("قيد يدوي بجيل غير مقبول", "Manual voucher with an unacceptable generation"),
        Book = Book,
        Currency = CurrencyCode.Sar,
        Event = new PostingEventCode("ledger.manual_voucher.posted"),
        Generation = 0,
        Lines =
        [
            new PostingLine
            {
                Role = PostingRole.RoundingDifference,
                Side = PostingSide.Debit,
                Amount = SharedKernel.Money.Of(50.0000m, CurrencyCode.Sar),
                Scope = new PostingScope("cc.001", "BR-01"),
            },
            new PostingLine
            {
                Role = PostingRole.RoundingDifference,
                Side = PostingSide.Credit,
                Amount = SharedKernel.Money.Of(50.0000m, CurrencyCode.Sar),
                Scope = new PostingScope("cc.001", "BR-01"),
            },
        ],
        Actor = new UserId(new Guid("11111111-1111-4111-8111-111111111111")),
    };
}

/// <summary>سطر سجلّ ملتقَط — بنصّه المُنسَّق وبحقوله المسمّاة معاً.</summary>
internal sealed record LogEntry(
    LogLevel Level,
    string Text,
    Exception? Exception,
    IReadOnlyList<KeyValuePair<string, object?>> State);

/// <summary>
/// سجلّ يلتقط ما يُكتب فيه. الحقول المسمّاة تُحفظ كما هي: اختبارٌ يفحص النصّ المُنسَّق
/// وحده يمرّ حتى لو صار السجلّ نصّاً مدموجاً لا سجلّاً مبنيَ الحقول.
/// </summary>
internal sealed class CapturingLogger : ILogger<PostingService>
{
    public List<LogEntry> Entries { get; } = [];

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        ArgumentNullException.ThrowIfNull(formatter);

        IReadOnlyList<KeyValuePair<string, object?>> fields = state is IReadOnlyList<KeyValuePair<string, object?>> pairs
            ? pairs
            : [];

        Entries.Add(new LogEntry(logLevel, formatter(state, exception), exception, fields));
    }
}
