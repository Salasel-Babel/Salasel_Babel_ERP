namespace Babel.Compliance.Abstractions;

/// <summary>نتيجة المقاصة كما تصل من الجهة أو المزوّد.</summary>
public enum ClearanceDisposition
{
    Cleared,
    ClearedWithWarnings,
    Rejected
}

/// <summary>
/// طلب مقاصة. <b>مسار حاجز</b>: المستند لا يُسلَّم للمشتري قبل عودة الرد.
/// </summary>
public sealed record ClearanceRequest(
    ComplianceDocumentId DocumentId,
    Guid DocumentUuid,
    TenantId Tenant,
    IssuingUnitId IssuingUnit,
    CredentialRef Credential,
    ComplianceEnvironment Environment,
    SealedPayload Payload,
    ChainSlot Chain,
    AttemptId Attempt,
    int AttemptNo,
    // بصمة المحتوى بالـhex — مفتاح الحصانة الوحيد الذي نملكه.
    // لا يوجد مفتاح حصانة موثَّق من جانب الجهة، ولا يُفترض أن المزوّد يحترم هذه القيمة
    // ما لم يصرّح بذلك في ProviderCapabilities.DeduplicatesBySubmissionFingerprint.
    string SubmissionFingerprint);

public sealed record ClearanceOutcome(
    ClearanceDisposition Disposition,
    IReadOnlyList<ComplianceNotice> Notices,
    DateTimeOffset ObservedAt,
    // النسخة التي تعيدها الجهة مختومة: تُخزَّن كما هي ولا تُشتقّ من قاعدة البيانات لاحقاً.
    // فارغة حين لا يعيدها المزوّد.
    ReadOnlyMemory<byte> StampedDocument,
    string? ProviderReference = null,
    // صرّح المزوّد بأنه تعرّف على هذا الإرسال كتكرار لإرسال سابق.
    // لا يُعتمد عليه ما لم تُصرَّح القدرة؛ وغيابه لا يعني عدم التكرار.
    bool RecognisedAsDuplicate = false);

/// <summary>
/// <b>قناة المقاصة — آلية مستقلة تماماً عن قناة الإبلاغ.</b>
/// طلب/استجابة حاجز، بحالة انتظار مرئية في الواجهة، ومهلة صريحة.
/// دمجها مع الإبلاغ في مسار واحد يعطي إما مقاصة غير آمنة أو إبلاغاً بطيئاً بلا داعٍ
/// (02-architecture §11.3 بند 2).
/// </summary>
public interface IClearanceChannel
{
    ValueTask<ClearanceOutcome> ClearAsync(ClearanceRequest request, CancellationToken ct);
}
