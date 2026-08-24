namespace Babel.Compliance.Abstractions;

/// <summary>
/// <b>مُتحقَّق منه من مصدر:</b> أنضج عميل مفتوح المصدر لهذه المنظومة يعرض <b>صفر GET</b>.
/// أي أن الاستعلام عن الحالة قد <b>لا يوجد أصلاً</b>. لذلك <see cref="NotSupported"/>
/// قيمة من الدرجة الأولى، ومسار الغموض <b>يجب أن يعمل بدونها</b>.
/// </summary>
public enum StatusProbeSupport
{
    NotSupported,
    ByProviderReference,
    ByDocumentIdentity
}

public enum ProbedState
{
    Unknown,
    NotFound,
    Pending,
    Accepted,
    AcceptedWithWarnings,
    Rejected
}

public sealed record StatusProbe(
    ComplianceDocumentId DocumentId,
    Guid DocumentUuid,
    TenantId Tenant,
    IssuingUnitId IssuingUnit,
    ChainSlot Chain,
    string SubmissionFingerprint,
    string? ProviderReference);

public sealed record StatusProbeResult(
    ProbedState State,
    IReadOnlyList<ComplianceNotice> Notices,
    DateTimeOffset ObservedAt,
    ReadOnlyMemory<byte> StampedDocument,
    string? ProviderReference = null);

public interface IComplianceStatusQuery
{
    StatusProbeSupport Support { get; }

    ValueTask<StatusProbeResult> ProbeAsync(StatusProbe probe, CancellationToken cancellationToken);
}
