namespace Babel.Compliance.Abstractions;

public enum ReportingDisposition
{
    Accepted,
    AcceptedWithWarnings,
    Rejected
}

/// <summary>
/// إرسال إبلاغ. <b>المستند سُلِّم للعميل فعلاً قبل هذا الإرسال</b> — الرفض هنا
/// لا يُبطل الإصدار، بل يستوجب معالجة وتصحيحاً.
/// <para/>
/// لاحظ ما <b>ليس</b> هنا مقارنةً بـ<see cref="ClearanceRequest"/>: لا نسخة مختومة عائدة،
/// ولا حالة انتظار في الواجهة، ولا مهلة تحجز المستخدم. هذا ليس إغفالاً — هذا هو الفرق البنيوي.
/// </summary>
public sealed record ReportingSubmission(
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
    string SubmissionFingerprint);

public sealed record ReportingAcknowledgement(
    ReportingDisposition Disposition,
    IReadOnlyList<ComplianceNotice> Notices,
    DateTimeOffset ObservedAt,
    string? ProviderReference = null,
    bool RecognisedAsDuplicate = false);

/// <summary>
/// <b>قناة الإبلاغ — آلية مستقلة تماماً عن قناة المقاصة.</b>
/// تُستدعى من عامل خلفي يقرأ من الصندوق الصادر، لا من طلب المستخدم.
/// </summary>
public interface IReportingChannel
{
    ValueTask<ReportingAcknowledgement> ReportAsync(
        ReportingSubmission submission,
        CancellationToken ct);
}
