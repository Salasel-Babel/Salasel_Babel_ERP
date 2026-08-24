namespace Babel.Compliance.Abstractions;

/// <summary>
/// المساران <b>ليسا إعداداً على نفس الآلية</b> — هما آليتان مختلفتان بنيوياً،
/// ولهما واجهتان منفصلتان (<see cref="IClearanceChannel"/> و<see cref="IReportingChannel"/>).
/// هذا التعداد يقرّر أيّ الآليتين تملك المستند، لا كيف تتصرّف آلية واحدة.
/// </summary>
public enum ComplianceFlow
{
    /// <summary>مقاصة: طلب/استجابة حاجز. المستند لا يُسلَّم للمشتري قبل الرد.</summary>
    Clearance,

    /// <summary>إبلاغ: أطلق وانسَ عبر الصندوق الصادر. المستند سُلِّم فعلاً.</summary>
    Reporting
}

public enum ComplianceDocumentKind
{
    Invoice,
    CreditNote,
    DebitNote
}

/// <summary>
/// أيّ مستند يذهب إلى أيّ مسار: <b>قرار سياسة يُقرأ من إعدادات المستأجر، لا ثابت في الكود.</b>
/// المعيار الفعلي (ما الذي يجعل الفاتورة «مبسطة») يجب أن يُقرأ من الوثيقة الرسمية.
/// </summary>
[Provisional("معيار تصنيف المستند إلى مسار مقاصة أو مسار إبلاغ",
    DerivedFrom = "docs/analysis/04-zatca-integration.md §3 — وهي نفسها وثيقة تخطيط لا وثيقة رسمية",
    Risk = ProvisionalRisk.Structural,
    VerifyBy = "قراءة تعريف الفاتورة المبسطة وشروطها في المواصفة السارية")]
public interface IFlowPolicy
{
    ComplianceFlow FlowFor(ComplianceDocumentKind kind, PartyRef? buyer, DocumentTotals totals);
}

/// <summary>طرف في المستند. الاسم بالعربية والإنجليزية إلزاماً (CONTRIBUTING §3 بند 5).</summary>
public sealed record PartyRef(
    string NameAr,
    string NameEn,
    [property: Provisional("شكل الرقم الضريبي وطوله وقواعد التحقق منه",
        Risk = ProvisionalRisk.Cosmetic,
        VerifyBy = "قواعد التحقق المنشورة للرقم الضريبي")]
    string? TaxRegistrationNumber,
    string? AddressAr = null,
    string? AddressEn = null,
    [property: Provisional("حقول العنوان المطلوبة ودرجة إلزاميتها لكل نوع مستند",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "جدول الحقول الإلزامية لكل نوع مستند في مواصفة الفاتورة الإلكترونية")]
    IReadOnlyDictionary<string, string>? AddressParts = null);

/// <summary>
/// سطر مستند. <b>كل مبلغ decimal</b> — CONTRIBUTING §3 بند 2.
/// المقياس القانوني في هذا النطاق كله هو 4 خانات عشرية (numeric(19,4)).
/// </summary>
public sealed record DocumentLine(
    int LineNo,
    string DescriptionAr,
    string DescriptionEn,
    decimal Quantity,
    decimal UnitPrice,
    decimal NetAmount,
    decimal TaxRate,
    decimal TaxAmount,
    decimal GrossAmount);

public sealed record DocumentTotals(
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrossTotal)
{
    /// <summary>فحص داخلي بحت، لا علاقة له بقواعد الهيئة.</summary>
    public bool IsInternallyConsistent => NetTotal + TaxTotal == GrossTotal;
}

/// <summary>
/// المستند كحقيقة مجالية داخلية — <b>قبل</b> أي تمثيل XML وقبل أي ختم.
/// هذا هو ما نملكه نحن. كل ما بعده (UBL، توقيع، QR) تمثيل قابل للاستبدال.
/// <para/>
/// The document as an internal domain fact, before any XML representation and before any
/// seal. This is the part we own; everything downstream is a replaceable representation.
/// </summary>
public sealed record ComplianceDocument(
    ComplianceDocumentId DocumentId,
    Guid DocumentUuid,
    TenantId Tenant,
    IssuingUnitId IssuingUnit,
    ComplianceDocumentKind Kind,
    ComplianceFlow Flow,
    string DocumentNumber,
    DateTimeOffset IssuedAt,
    string CurrencyCode,
    PartyRef Seller,
    PartyRef? Buyer,
    IReadOnlyList<DocumentLine> Lines,
    DocumentTotals Totals,
    JournalEntryRef JournalEntry,
    ComplianceDocumentId? OriginalDocument = null,
    string? CorrectionReasonAr = null,
    string? CorrectionReasonEn = null);

/// <summary>
/// موضع المستند في سلسلة وحدة الإصدار: العدّاد والبصمة السابقة.
/// <b>القيمتان تدخلان جسم المستند المُجزَّأ، لا تجاوره.</b> هذا بالضبط ما يجعل السلسلة
/// رابطة تشفيرياً بدل أن تكون عموداً يمكن إعادة كتابته.
/// </summary>
public readonly record struct ChainSlot(
    long Counter,
    ReadOnlyMemory<byte> PreviousHash)
{
    public string PreviousHashHex => Convert.ToHexString(PreviousHash.Span).ToLowerInvariant();
}
