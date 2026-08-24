using System.Collections.Concurrent;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// تسجيل وحدة إصدار. <b>خمسون نقطة بيع = خمسون صفاً هنا</b>، ولكل صف شهادته
/// ومقبضه ومرحلته وتاريخ انتهائه. هذه هي البيانات التي تعرضها شاشة إدارة وحدات الإصدار.
/// <b>لا مادة مفتاح هنا — مقابض فقط.</b>
/// </summary>
public sealed class IssuingUnitRegistration
{
    public required TenantId Tenant { get; init; }
    public required IssuingUnitId IssuingUnit { get; init; }
    public required ComplianceEnvironment Environment { get; init; }
    public required string DisplayNameAr { get; init; }
    public required string DisplayNameEn { get; init; }
    public CredentialRef Credential { get; set; } = CredentialRef.None;
    public OnboardingStage Stage { get; set; } = OnboardingStage.NotStarted;
    public DateTimeOffset? CertificateNotAfter { get; set; }
    public DateTimeOffset? LastRenewalCheck { get; set; }

    public bool CanIssue => Stage == OnboardingStage.Active && !Credential.IsNone;

    /// <summary>الشهادات تنتهي؛ التنبيه قبل الانتهاء بمدة كافية متطلَّب تشغيلي لا رفاهية.</summary>
    public bool RenewalDue(DateTimeOffset now, TimeSpan lead) =>
        CertificateNotAfter is { } exp && now + lead >= exp;
}

public interface IIssuingUnitRegistry
{
    Task<IssuingUnitRegistration?> GetAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct);
    Task<IReadOnlyList<IssuingUnitRegistration>> ListAsync(TenantId tenant, CancellationToken ct);
    Task UpsertAsync(IssuingUnitRegistration registration, CancellationToken ct);
}

public sealed class InMemoryIssuingUnitRegistry : IIssuingUnitRegistry
{
    private readonly ConcurrentDictionary<(string, string), IssuingUnitRegistration> _rows = new();

    public Task<IssuingUnitRegistration?> GetAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct) =>
        Task.FromResult(_rows.GetValueOrDefault((tenant.Value, unit.Value)));

    public Task<IReadOnlyList<IssuingUnitRegistration>> ListAsync(TenantId tenant, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<IssuingUnitRegistration>>(
            [.. _rows.Values.Where(r => r.Tenant.Value == tenant.Value)
                            .OrderBy(r => r.IssuingUnit.Value, StringComparer.Ordinal)]);

    public Task UpsertAsync(IssuingUnitRegistration registration, CancellationToken ct)
    {
        _rows[(registration.Tenant.Value, registration.IssuingUnit.Value)] = registration;
        return Task.CompletedTask;
    }
}

public sealed class IssuingUnitNotReadyException(string message) : Exception(message);
