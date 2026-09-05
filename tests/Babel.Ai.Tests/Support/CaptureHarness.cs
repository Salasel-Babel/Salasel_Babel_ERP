using Babel.Ai.Capture;
using Babel.Ai.Extraction;
using Babel.Ai.Promotion;
using Babel.Ai.Suggestions;
using Babel.Compliance.Zatca.Qr;
using Babel.Contracts.Capture;
using Babel.Contracts.Storage;
using Babel.Storage;
using Babel.Core.Entitlement;
using Babel.Core.Parameters;
using Babel.SharedKernel;

namespace Babel.Ai.Tests.Support;

/// <summary>منفِّذ استحقاق يسمح دائماً — الاستحقاق نفسه مُختبَر في Babel.Core.Tests.</summary>
internal sealed class AlwaysEntitled : IEntitlementEnforcer
{
    public ValueTask<Result> EnsureAsync(
        TenantId tenant,
        UserId actor,
        BabelModule module,
        EntitlementAccess access,
        string operation,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result.Success());
}

/// <summary>ساعة ثابتة: المسوّدة تحمل لحظة التقاط حتمية، فتتساوى تشغيلتان.</summary>
internal sealed class FixedClock(DateTimeOffset now) : TimeProvider
{
    public override DateTimeOffset GetUtcNow() => now;
}

/// <summary>
/// وحدةٌ مالكة للمستند، مُحاكاة. تسجّل ما وصلها، ولا تكتب شيئاً — وهذا هو المقصود:
/// الاختبار يُثبت <b>ما يُسلَّم</b> لا ما تفعله وحدة المشتريات بعده.
/// </summary>
internal sealed class RecordingReceiver : ICapturedInvoiceReceiver
{
    public List<PromotionOrder> Received { get; } = [];

    public ValueTask<Result<PromotedDocumentReference>> ReceiveAsync(
        PromotionOrder order,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(order);
        Received.Add(order);

        return ValueTask.FromResult(Result<PromotedDocumentReference>.Success(
            new PromotedDocumentReference(BabelModule.Purchasing, "SupplierBill", "BILL-" + Received.Count.ToString(System.Globalization.CultureInfo.InvariantCulture))));
    }
}

/// <summary>
/// وحدة مالكة ترفض دائماً — تُثبت أن الرفض عند الوحدة المالكة يمنع تحوّل المسوّدة
/// إلى «مُرقّاة» ولا يترك حالة نصفية.
/// </summary>
internal sealed class RefusingReceiver : ICapturedInvoiceReceiver
{
    public ValueTask<Result<PromotedDocumentReference>> ReceiveAsync(
        PromotionOrder order,
        CancellationToken cancellationToken = default)
        => ValueTask.FromResult(Result<PromotedDocumentReference>.Failure(new Error(
            "purchasing.duplicate_number",
            "رقم مستند مستعمل من قبل.",
            "Document number already used.")));
}

/// <summary>
/// بيئة اختبار الالتقاط.
/// <para>
/// <b>كل اختبار يبني بيئته كاملةً</b>: مخزن جديد، ومزوّد مبذور، ومستأجر بمعرّف جديد.
/// لا حالة مشتركة بين اختبارين، فالاختبار يمرّ وحده كما يمرّ في التشغيل الكامل.
/// </para>
/// </summary>
internal sealed class CaptureHarness
{
    public const string SellerName = "شركة سلاسل بابل للمقاولات";
    public const string SellerVatNumber = "300000000000003";
    public const string DocumentId = "CAP-0001";
    public const string EventCode = "purchasing.invoice.expense.posted";
    public const string RoleCode = "ap_supplier_control";

    public static readonly DateTimeOffset IssuedAt = new(2026, 8, 25, 10, 30, 0, TimeSpan.Zero);

    private CaptureHarness(
        InvoiceCaptureService service,
        DeterministicExtractionProvider provider,
        RecordingReceiver receiver,
        ICapturedDraftStore store,
        InMemoryAttachmentStore attachments,
        AttachmentId document,
        TenantId tenant)
    {
        Service = service;
        Provider = provider;
        Receiver = receiver;
        Store = store;
        Attachments = attachments;
        Document = document;
        Tenant = tenant;
    }

    /// <summary>مخزن المرفقات — البايتات فيه، والمسوّدة تشير إليها.</summary>
    public InMemoryAttachmentStore Attachments { get; }

    /// <summary>المستند المُودَع الذي تشير إليه طلبات هذه البيئة.</summary>
    public AttachmentId Document { get; }

    public InvoiceCaptureService Service { get; }

    public DeterministicExtractionProvider Provider { get; }

    public RecordingReceiver Receiver { get; }

    public ICapturedDraftStore Store { get; }

    public TenantId Tenant { get; }

    public UserId Actor { get; } = new(Guid.CreateVersion7());

    /// <summary>ينشئ بيئة كاملة. المزوّد يُبذر بالمُخرَج المعطى.</summary>
    public static CaptureHarness Create(string json, ICapturedInvoiceReceiver? receiver = null)
    {
        RecordingReceiver recording = new();
        InMemoryCapturedDraftStore store = new();
        InMemoryAttachmentStore attachments = new();
        TenantId tenant = new(Guid.CreateVersion7());

        // **البايتات تُودَع أولاً، ثم يُشار إليها.** ولذلك يُبذر المزوّد بمعرّف المرفق
        // نفسه: هوية المستند في هذا المسار صارت هوية ما أُودِع، لا نصّاً يخترعه المستدعي.
        AttachmentId document = Deposit(attachments, tenant, DocumentBytes);

        DeterministicExtractionProvider provider = new DeterministicExtractionProvider()
            .Answering(document.ToString(), json);

        InvoiceCaptureService service = new(
            new AlwaysEntitled(),
            provider,
            new ZatcaQrAttestationReader(),
            MatrixPostingVocabulary.Default,
            store,
            attachments,
            receiver ?? recording,
            // ‏**منفذ المعامِلات حقيقيّ لا نائب**: `ParameterDirectory` فوق مخزنٍ في
            // الذاكرة يقرأ **ملفّ افتراضات المنصّة نفسه** الذي يُشحن. فالنسبة التي
            // يقرأها هذا الاختبار هي المشحونة، ولا رقم مكتوب هنا.
            new ParameterDirectory(new InMemoryParameterStore(new FixedClock(IssuedAt))),
            new AiOptions(),
            new FixedClock(IssuedAt));

        return new CaptureHarness(service, provider, recording, store, attachments, document, tenant);
    }

    /// <summary>بايتات مستند صادقة الترويسة — ترويسة JPEG.</summary>
    public static byte[] DocumentBytes => [0xFF, 0xD8, 0xFF, 0xE0, 0x00, 0x10, 0x4A, 0x46, 0x49, 0x46];

    /// <summary>يودِع بايتات في مخزن ويعيد معرّفها.</summary>
    public static AttachmentId Deposit(InMemoryAttachmentStore attachments, TenantId tenant, byte[] content)
    {
        // النائب يكتمل تزامنياً بحكم بنائه، والتأكيد يقولها بدل أن يفترضها القارئ.
        ValueTask<Result<StoredAttachment>> put = attachments.PutAsync(new AttachmentSubmission
        {
            Tenant = tenant,
            Actor = new UserId(Guid.CreateVersion7()),
            Content = content,
            DeclaredFileName = "فاتورة.jpg",
            DeclaredMediaType = "image/jpeg",
        });

        return put.IsCompleted
            ? put.Result.Value.Id
            : throw new InvalidOperationException("نائب المخزن لم يكتمل تزامنياً — النائب تغيّر ولم يتغيّر هذا الافتراض.");
    }

    /// <summary>يودِع مستنداً ثانياً في هذه البيئة ويعيد معرّفه.</summary>
    public AttachmentId DepositAnother(byte[] content) => Deposit(Attachments, Tenant, content);

    /// <summary>ينشئ بيئة بمُخرَج مُركَّب.</summary>
    public static CaptureHarness Create(ComposedExtraction extraction, ICapturedInvoiceReceiver? receiver = null) =>
        Create(DeterministicExtractionProvider.Compose(extraction), receiver);

    /// <summary>رمز المرحلة الأولى للفاتورة القياسية، مولَّداً بالمُرمِّز القائم.</summary>
    public static string Phase1Qr(decimal grossTotal, decimal taxTotal) =>
        ZatcaQr.Phase1(SellerName, SellerVatNumber, IssuedAt, grossTotal, taxTotal);

    /// <summary>رمز المرحلة الثانية لفاتورة قياسية موقَّعة.</summary>
    public static string Phase2Qr(decimal grossTotal, decimal taxTotal) =>
        ZatcaQr.Phase2(
            SellerName,
            SellerVatNumber,
            IssuedAt,
            grossTotal,
            taxTotal,
            invoiceHashBase64: "3PbTHVmVaOKQd9GsNCbTZLPHVGT5xN6HvfxK9wHDnvE=",
            signatureBase64: "MEUCIQD0",
            publicKeyDer: new byte[] { 0x30, 0x2A, 0x30, 0x05, 0x06, 0x03, 0x2B, 0x65, 0x70 },
            certificateSignature: new byte[] { 0x01, 0x02, 0x03, 0x04 },
            isSimplified: false);

    /// <summary>
    /// طلب التقاط من قناة محادثة، مع حمولة رمز أو بدونها.
    /// <b>ولا بايتة فيه</b> — معرّف مرفق وقناة وحمولة رمز.
    /// </summary>
    public CaptureRequest Request(string? qrPayload) => new()
    {
        Tenant = Tenant,
        Document = Document,
        Channel = CaptureChannel.Chat,
        QrPayload = qrPayload,
    };

    /// <summary>
    /// فاتورة مصروف متّسقة: صافٍ 1000، ضريبة 150 عند 15٪، وإجمالي 1150، وسطران بـ500.
    /// </summary>
    public static ComposedExtraction ConsistentInvoice(decimal net = 1000.00m) => new()
    {
        SellerName = SellerName,
        SellerVatNumber = SellerVatNumber,
        InvoiceNumber = "INV-4417",
        IssuedOn = new DateOnly(2026, 8, 25),
        Net = net,
        TaxTotal = 150.00m,
        GrossTotal = 1150.00m,
        Lines =
        [
            new ComposedLine("خدمات صيانة — أغسطس", 1m, 500.00m, 500.00m),
            new ComposedLine("قطع غيار", 1m, 500.00m, 500.00m),
        ],
        SuggestedEventCode = EventCode,
        SuggestedRoleCode = RoleCode,
        Rationale = "مورد خدمات بلا أمر شراء ولا استلام مخزني",
        Confidence = 0.94m,
        SuggestionConfidence = 0.80m,
    };

    /// <summary>كل الحقول التي توجب مراجعة أو قراراً، مؤكَّدةً — تأكيد إنسان قرأ.</summary>
    public static PromotionConfirmation ConfirmAll(CapturedInvoiceDraft draft) =>
        new(new HashSet<string>(draft.FieldsNeedingHumanJudgement(), StringComparer.Ordinal));
}
