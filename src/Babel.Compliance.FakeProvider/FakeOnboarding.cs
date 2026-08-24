using System.Collections.Concurrent;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.FakeProvider;

/// <summary>
/// دورة حياة الشهادة الوهمية. <b>التوقيعات نفسها تحت الشكلين</b>؛
/// الفرق كله في السطر الأول من <see cref="CreateSigningRequestAsync"/>: من يولّد المفتاح.
/// </summary>
public sealed class FakeOnboardingChannel(
    EphemeralKeyVault vault,
    KeyCustody custody,
    ILocalKeyCustodian? localCustodian,
    TimeProvider clock) : IOnboardingChannel
{
    private readonly ConcurrentDictionary<string, OnboardingStage> _stages = new();

    public IReadOnlyDictionary<string, OnboardingStage> Stages => _stages;

    public async ValueTask<CsrMaterial> CreateSigningRequestAsync(CsrRequest request, CancellationToken ct)
    {
        // ------------------------------------------------------------------
        // النقطة التي لا رجعة فيها. تحت «نحن نحوز»: المفتاح يُولَّد في خزينتنا
        // ولا يغادرها. وتحت «المزوّد يحوز»: المفتاح يُولَّد لدى المزوّد ولا نراه أبداً.
        // ما بعد هذا السطر متطابق تماماً في الشكلين — وما قبله لا يتحوّل أحدهما إلى الآخر.
        // ------------------------------------------------------------------
        var credential = custody == KeyCustody.SelfHeld && localCustodian is not null
            ? await localCustodian.CreateKeyAsync(request.Tenant, request.IssuingUnit, request.Environment, ct)
            : vault.Create(request.Tenant, request.IssuingUnit, request.Environment);

        var csr = vault.BuildSigningRequest(credential, request.Subject);
        _stages[credential.Value] = OnboardingStage.SigningRequestBuilt;
        return new CsrMaterial(credential, csr);
    }

    public ValueTask<CertificateGrant> RequestComplianceCertificateAsync(
        CredentialRef credential, OneTimePasswordRef otp, CancellationToken ct)
    {
        _ = otp; // المقبض فقط؛ لا يمرّ السرّ نفسه عبر هذا الحدّ ولا يُسجَّل.
        var cert = vault.IssueSelfSigned(credential, "babel-compliance-fake", TimeSpan.FromDays(30));
        _stages[credential.Value] = OnboardingStage.ComplianceCertificateIssued;
        return ValueTask.FromResult(new CertificateGrant(
            credential, cert.RawData, SecretRef.None, clock.GetUtcNow(),
            cert.NotAfter, OnboardingStage.ComplianceCertificateIssued));
    }

    public ValueTask<ComplianceCheckResult> RunComplianceChecksAsync(CredentialRef credential, CancellationToken ct)
    {
        _stages[credential.Value] = OnboardingStage.ComplianceChecksPassed;
        return ValueTask.FromResult(new ComplianceCheckResult(true,
            [ComplianceNotice.Info("fake-checks",
                "فحوصات امتثال وهمية — عدد المستندات المطلوبة وأنواعها غير مُتحقَّق منهما",
                "fake compliance checks — the required document count and types are unverified")],
            DocumentsExercised: 0));
    }

    public ValueTask<CertificateGrant> RequestProductionCertificateAsync(CredentialRef credential, CancellationToken ct)
    {
        var cert = vault.IssueSelfSigned(credential, "babel-production-fake", TimeSpan.FromDays(365));
        _stages[credential.Value] = OnboardingStage.Active;
        return ValueTask.FromResult(new CertificateGrant(
            credential, cert.RawData, SecretRef.None, clock.GetUtcNow(),
            cert.NotAfter, OnboardingStage.ProductionCertificateIssued));
    }

    public ValueTask<CertificateGrant> RenewProductionCertificateAsync(CredentialRef credential, CancellationToken ct) =>
        RequestProductionCertificateAsync(credential, ct);

    public ValueTask RevokeAsync(CredentialRef credential, string reason, CancellationToken ct)
    {
        _stages[credential.Value] = OnboardingStage.Revoked;
        return ValueTask.CompletedTask;
    }
}
