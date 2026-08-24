using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Model;

/// <summary>
/// سجل مستند الالتزام. <b>يعيش بجوار القيد لا داخله</b>: يحمل إشارة إلى القيد
/// ولا يملك تغييره. حذف هذا السجل كله لا يمسّ ريالاً واحداً في دفتر الأستاذ.
/// </summary>
public sealed class ComplianceRecord
{
    public required ComplianceDocumentId DocumentId { get; init; }
    public required Guid DocumentUuid { get; init; }
    public required TenantId Tenant { get; init; }
    public required IssuingUnitId IssuingUnit { get; init; }
    public required ComplianceEnvironment Environment { get; init; }
    public required ComplianceDocumentKind Kind { get; init; }
    public required ComplianceFlow Flow { get; init; }
    public required string DocumentNumber { get; init; }
    public required JournalEntryRef JournalEntry { get; init; }
    public required DateTimeOffset IssuedAt { get; init; }

    /// <summary>العدّاد: مُخصَّص <b>مرة واحدة عند البناء</b>. لا محاولة إرسال تحرقه ولا تعيد تخصيصه.</summary>
    public required long Counter { get; init; }
    public required byte[] PreviousHash { get; init; }

    /// <summary>بصمتنا نحن على الحقيقة المجالية — رابط السلسلة.</summary>
    public required byte[] DocumentHash { get; init; }

    /// <summary>
    /// <b>البايتات المجمَّدة.</b> تُخزَّن كما بُنيت ولا تُشتقّ من قاعدة البيانات عند الطلب أبداً
    /// (02-architecture §8.2 بند 10: أشيع عطل إنتاجي في نظام مقارن هو إعادة توليد مصنوع مختوم).
    /// </summary>
    public required byte[] FrozenPayload { get; init; }
    public required SealState SealState { get; init; }
    public required string SubmissionFingerprint { get; init; }

    /// <summary>البايتات كما بُنيت قبل الاستبعاد — للأرشفة والتدقيق.</summary>
    public byte[]? RenderedBody { get; init; }

    public decimal NetTotal { get; init; }
    public decimal TaxTotal { get; init; }
    public decimal GrossTotal { get; init; }
    public string CurrencyCode { get; init; } = "SAR";

    public ComplianceStatus Status { get; set; } = ComplianceStatus.Built;
    public int AttemptCount { get; set; }
    public int ResolutionAttemptCount { get; set; }
    public DateTimeOffset? QueuedAt { get; set; }
    public DateTimeOffset? SettledAt { get; set; }
    public string? ProviderReference { get; set; }

    /// <summary>النسخة المختومة العائدة من الجهة. تُخزَّن كما وصلت.</summary>
    public byte[]? StampedDocument { get; set; }

    public List<ComplianceNotice> Notices { get; set; } = [];

    public string? HumanReviewReasonAr { get; set; }
    public string? HumanReviewReasonEn { get; set; }

    public long Version { get; set; }

    public bool IsSettled => ComplianceStatusMachine.IsSettled(Status);
    public bool IsAccepted => Status is ComplianceStatus.Accepted or ComplianceStatus.AcceptedWithWarnings;
}

/// <summary>
/// نتيجة محاولة إرسال واحدة. <b>InFlight يُكتب قبل النداء</b>؛ ولهذا معنى تشغيلي حاسم:
/// سقوط العملية في منتصف النداء يترك صفاً InFlight قديماً — وهو <b>بالضبط</b> حالة الغموض.
/// </summary>
public enum AttemptOutcome
{
    InFlight,
    Accepted,
    AcceptedWithWarnings,
    Rejected,
    NotSent,
    Ambiguous
}

public sealed class SubmissionAttempt
{
    public required AttemptId AttemptId { get; init; }
    public required ComplianceDocumentId DocumentId { get; init; }
    public required int AttemptNo { get; init; }
    public required DateTimeOffset StartedAt { get; init; }

    /// <summary>
    /// بصمة الحمولة <b>وقت هذه المحاولة بالذات</b>. مقارنتها ببصمة المحاولة الأولى
    /// هي ما يكشف أن المزوّد أعاد الختم فتغيّرت البايتات — وهو ما يحدث حتماً
    /// تحت شكل «المزوّد يحوز المفتاح».
    /// </summary>
    public required string PayloadFingerprint { get; init; }

    /// <summary>هذه المحاولة ليست إرسالاً بل محاولة حسم لغموض سابق.</summary>
    public bool IsResolution { get; init; }

    public AttemptOutcome Outcome { get; set; } = AttemptOutcome.InFlight;
    public DateTimeOffset? CompletedAt { get; set; }
    public FaultClass? FaultClass { get; set; }
    public string? FaultCode { get; set; }
    public string? FaultMessageAr { get; set; }
    public string? FaultMessageEn { get; set; }
    public string? ProviderReference { get; set; }
    public bool ProviderReportedDuplicate { get; set; }

    public bool IsStale(DateTimeOffset now, TimeSpan lease) =>
        Outcome == AttemptOutcome.InFlight && now - StartedAt > lease;
}

/// <summary>سجل انتقال حالة. يُضاف إليه فقط — لا تعديل ولا حذف.</summary>
public sealed class StatusTransition
{
    public required Guid TransitionId { get; init; }
    public required ComplianceDocumentId DocumentId { get; init; }
    public required int Seq { get; init; }
    public required ComplianceStatus From { get; init; }
    public required ComplianceStatus To { get; init; }
    public required DateTimeOffset At { get; init; }
    public required string Actor { get; init; }
    public required string ReasonAr { get; init; }
    public required string ReasonEn { get; init; }
    public AttemptId? Attempt { get; init; }
}

/// <summary>رأس سلسلة وحدة إصدار: العدّاد التالي وبصمة آخر مستند.</summary>
public sealed class IssuingUnitChainHead
{
    public required TenantId Tenant { get; init; }
    public required IssuingUnitId IssuingUnit { get; init; }
    public required long NextCounter { get; set; }
    public required byte[] HeadHash { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }

    /// <summary>
    /// السلسلة مُوقفة على هذه الوحدة. كسر السلسلة تنبيه بأعلى درجة يوقف الإصدار
    /// على الوحدة المتأثرة حتى المعالجة (04-zatca §7).
    /// </summary>
    public bool IsHalted { get; set; }
    public string? HaltReasonAr { get; set; }
    public string? HaltReasonEn { get; set; }
}

/// <summary>عنصر عمل في الطابور. للإبلاغ فقط — المقاصة لا تمرّ من هنا.</summary>
public enum ComplianceWorkKind
{
    /// <summary>إرسال إبلاغ: أطلق وانسَ.</summary>
    ReportDocument,

    /// <summary>حسم غموض: استعلام حالة، أو إعادة إرسال محدودة ببايتات مطابقة.</summary>
    ResolveAmbiguity
}

public sealed class ComplianceWorkItem
{
    public required Guid WorkItemId { get; init; }
    public required ComplianceDocumentId DocumentId { get; init; }
    public required TenantId Tenant { get; init; }
    public required ComplianceWorkKind Kind { get; set; }
    public DateTimeOffset NotBefore { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset EnqueuedAt { get; init; }
    public string? LastErrorAr { get; set; }
    public string? LastErrorEn { get; set; }
    public bool Done { get; set; }
}
