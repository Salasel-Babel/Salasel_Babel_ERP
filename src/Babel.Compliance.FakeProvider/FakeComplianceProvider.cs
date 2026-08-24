using Babel.Compliance.Abstractions;

namespace Babel.Compliance.FakeProvider;

/// <summary>
/// مزوّد وهمي كامل، بشكل حيازة قابل للاختيار. <b>هذه هي النقطة التي تُثبت أن الحدّ
/// يحتمل الشكلين فعلاً</b>: المُنسِّق نفسه يقود الاثنين بلا تفريع واحد.
/// <para/>
/// لا شبكة، ولا اعتمادات، ولا شهادات في المستودع. كل مفتاح يُولَّد عند التشغيل.
/// </summary>
public sealed class FakeComplianceProvider : IComplianceProvider, IDisposable
{
    private readonly EphemeralKeyVault _vault = new();
    private ProviderCapabilities _capabilities;

    public FakeComplianceProvider(
        KeyCustody custody,
        FakeAuthority authority,
        TimeProvider clock,
        StatusProbeSupport statusQuery = StatusProbeSupport.NotSupported,
        bool supportsClearance = true,
        bool supportsReporting = true,
        bool deduplicates = false)
    {
        Authority = authority;
        Clock = clock;

        _capabilities = new ProviderCapabilities(
            ProviderId: custody == KeyCustody.SelfHeld ? "fake.self-held" : "fake.provider-held",
            DisplayNameAr: custody == KeyCustody.SelfHeld
                ? "مزوّد وهمي — نحن نحوز المفتاح" : "مزوّد وهمي — المزوّد يحوز المفتاح",
            DisplayNameEn: custody == KeyCustody.SelfHeld
                ? "fake provider — we hold the key" : "fake provider — the provider holds the key",
            Custody: custody,
            SupportsClearance: supportsClearance,
            SupportsReporting: supportsReporting,
            StatusQuery: statusQuery,
            ReturnsStampedDocument: true,
            RendersDocument: false,
            ClearanceTimeout: TimeSpan.FromSeconds(30),
            ReportingTimeout: TimeSpan.FromSeconds(30),
            DeduplicatesBySubmissionFingerprint: deduplicates,
            // خاصية بنيوية تحت «نحن نحوز»، ووعد تعاقدي غائب تحت «المزوّد يحوز».
            GuaranteesByteStableRetransmission: custody == KeyCustody.SelfHeld);

        LocalCustodian = custody == KeyCustody.SelfHeld ? new VaultKeyCustodian(_vault) : null;
        Sealer = custody == KeyCustody.SelfHeld
            ? new SelfHeldSealer(LocalCustodian!)
            : new ProviderHeldSealer();

        Onboarding = new FakeOnboardingChannel(_vault, custody, LocalCustodian, clock);
        Clearance = supportsClearance ? new FakeClearanceChannel(authority, _vault, () => _capabilities, clock) : null;
        Reporting = supportsReporting ? new FakeReportingChannel(authority, _vault, () => _capabilities, clock) : null;
        StatusQuery = statusQuery == StatusProbeSupport.NotSupported
            ? null : new FakeStatusQuery(authority, statusQuery, clock);
    }

    public FakeAuthority Authority { get; }
    public TimeProvider Clock { get; }
    public EphemeralKeyVault Vault => _vault;
    public ILocalKeyCustodian? LocalCustodian { get; }

    public ProviderCapabilities Capabilities => _capabilities;
    public IDocumentSealer Sealer { get; }
    public IOnboardingChannel Onboarding { get; }
    public IClearanceChannel? Clearance { get; }
    public IReportingChannel? Reporting { get; }
    public IComplianceStatusQuery? StatusQuery { get; }

    /// <summary>يسمح للاختبار بتغيير قدرة واحدة دون إعادة تركيب المزوّد كله.</summary>
    public void MutateCapabilities(Func<ProviderCapabilities, ProviderCapabilities> mutate) =>
        _capabilities = mutate(_capabilities);

    public void Dispose() => _vault.Dispose();
}
