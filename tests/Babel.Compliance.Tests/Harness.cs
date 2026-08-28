using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.FakeProvider;
using Babel.Compliance.Model;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Reconciliation;
using Babel.Compliance.Store;

namespace Babel.Compliance.Tests;

/// <summary>
/// دفتر أستاذ وهمي مصغَّر. غرضه الوحيد في هذه الاختبارات أن يجيب على سؤال واحد:
/// <b>هل مسّ الالتزامُ القيدَ؟</b> ولذلك يحتفظ بلقطة غير قابلة للتغيير من كل قيد
/// ويقارنها في نهاية كل اختبار.
/// </summary>
public sealed class FakeLedger : ILedgerTaxableDocumentSource
{
    private readonly List<PostedTaxableDocument> _posted = [];
    private readonly Dictionary<Guid, string> _snapshots = [];

    public JournalEntryRef Post(
        TenantId tenant, IssuingUnitId unit, string number,
        decimal net, decimal tax, decimal gross, DateTimeOffset at)
    {
        var entry = new JournalEntryRef(Guid.CreateVersion7());
        var doc = new PostedTaxableDocument(entry, tenant, unit, number, at, net, tax, gross, null);
        _posted.Add(doc);
        _snapshots[entry.Value] = Snapshot(doc);
        return entry;
    }

    /// <summary>يرمي إن تغيّر أي قيد منذ ترحيله. القيد <b>يجب</b> أن ينجو من كل ما يفعله الالتزام.</summary>
    public void AssertUntouched()
    {
        foreach (var doc in _posted)
        {
            var now = Snapshot(doc);
            if (now != _snapshots[doc.JournalEntry.Value])
                throw new Xunit.Sdk.XunitException(
                    $"القيد {doc.JournalEntry} تغيّر: كان\n  {_snapshots[doc.JournalEntry.Value]}\nوصار\n  {now}");
        }
    }

    private static string Snapshot(PostedTaxableDocument d) =>
        $"{d.JournalEntry.Value:D}|{d.Tenant.Value}|{d.IssuingUnit.Value}|{d.DocumentNumber}|" +
        $"{ComplianceCanonical.Money(d.NetTotal)}|{ComplianceCanonical.Money(d.TaxTotal)}|" +
        $"{ComplianceCanonical.Money(d.GrossTotal)}|{ComplianceCanonical.Instant(d.PostedAt)}";

    public Task<IReadOnlyList<PostedTaxableDocument>> ListAsync(
        TenantId tenant, DateTimeOffset from, DateTimeOffset to, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<PostedTaxableDocument>>(
            [.. _posted.Where(p => p.Tenant.Value == tenant.Value && p.PostedAt >= from && p.PostedAt <= to)]);
}

/// <summary>ساعة يدوية: بعض الاختبارات تحتاج تقديم الوقت لتجاوز مهلة الإيجار.</summary>
public sealed class ManualClock(DateTimeOffset start) : TimeProvider
{
    private DateTimeOffset _now = start;
    public override DateTimeOffset GetUtcNow() => _now;
    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>
/// تركيب كامل قابل للتشغيل بلا قاعدة بيانات وبلا اعتمادات: مزوّد وهمي، ومخزن في الذاكرة،
/// ومُنسِّق حقيقي — <b>هو نفسه</b> الذي يعمل في الإنتاج.
/// </summary>
public sealed class Harness : IDisposable
{
    public static readonly TenantId Tenant = new("acme");
    public static readonly IssuingUnitId Unit = new("POS-01");

    public Harness(
        KeyCustody custody = KeyCustody.SelfHeld,
        StatusProbeSupport statusQuery = StatusProbeSupport.NotSupported,
        bool deduplicates = false,
        ComplianceSettings? settings = null,
        DateTimeOffset? start = null)
    {
        Clock = new ManualClock(start ?? new DateTimeOffset(2026, 3, 1, 9, 0, 0, TimeSpan.Zero));
        Authority = new FakeAuthority();
        Provider = new FakeComplianceProvider(custody, Authority, Clock, statusQuery, deduplicates: deduplicates);
        Settings = settings ?? new ComplianceSettings();
        Store = new InMemoryComplianceStore { Clock = Clock };
        Registry = new InMemoryIssuingUnitRegistry();
        Ledger = new FakeLedger();

        Renderer = new ProvisionalDocumentRenderer();
        Factory = new ComplianceDocumentFactory(Store, Renderer, Provider, Registry, Clock);
        Clearance = new ClearanceCoordinator(Store, Provider, Registry, Settings, Clock);
        Reporting = new ReportingWorker(Store, Provider, Registry, Settings, Clock);
        Service = new ComplianceService(Factory, Clearance, Reporting, Store, Settings, Clock);
        Reconciler = new Reconciler(Store, Ledger, Settings, Clock);
    }

    public ManualClock Clock { get; }
    public FakeAuthority Authority { get; }
    public FakeComplianceProvider Provider { get; }
    public ComplianceSettings Settings { get; }
    public InMemoryComplianceStore Store { get; }
    public InMemoryIssuingUnitRegistry Registry { get; }
    public FakeLedger Ledger { get; }
    public ProvisionalDocumentRenderer Renderer { get; }
    public ComplianceDocumentFactory Factory { get; }
    public ClearanceCoordinator Clearance { get; }
    public ReportingWorker Reporting { get; }
    public ComplianceService Service { get; }
    public Reconciler Reconciler { get; }

    /// <summary>يمرّ بدورة التسجيل كاملة حتى تصير الوحدة قادرة على الإصدار.</summary>
    public async Task<IssuingUnitRegistration> OnboardAsync(
        IssuingUnitId? unit = null, TenantId? tenant = null, CancellationToken ct = default)
    {
        var u = unit ?? Unit;
        var t = tenant ?? Tenant;
        var csr = await Provider.Onboarding.CreateSigningRequestAsync(
            new CsrRequest(t, u, ComplianceEnvironment.Simulation, new CsrSubject(
                CommonName: $"{t.Value}-{u.Value}",
                OrganisationName: "سلاسل بابل",
                OrganisationalUnitName: "المبيعات",
                CountryCode: "SA",
                SubjectAlternativeNameRdns: new Dictionary<string, string>
                {
                    // خمسة معرّفات RDN مخصّصة. القيم بلا معنى تنظيمي — الشكل وحده هو المُثبَت.
                    ["1.3.6.1.4.1.311.20.2.3"] = "babel-egs-01",
                    ["2.5.4.4"] = "babel-serial",
                    ["2.5.4.5"] = "300000000000003",
                    ["2.5.4.12"] = "1100",
                    ["2.5.4.26"] = "الرياض"
                },
                CertificateTemplateName: "PREZATCA-Code-Signing")), ct);

        await Provider.Onboarding.RequestComplianceCertificateAsync(
            csr.Credential, new OneTimePasswordRef(new SecretRef("vault://otp/handle"), Clock.GetUtcNow()), ct);
        await Provider.Onboarding.RunComplianceChecksAsync(csr.Credential, ct);
        var grant = await Provider.Onboarding.RequestProductionCertificateAsync(csr.Credential, ct);

        if (Provider.LocalCustodian is { } local)
            await local.AttachCertificateAsync(csr.Credential, grant.CertificateDer, ct);

        var registration = new IssuingUnitRegistration
        {
            Tenant = t,
            IssuingUnit = u,
            Environment = ComplianceEnvironment.Simulation,
            DisplayNameAr = "نقطة بيع ١",
            DisplayNameEn = "POS 1",
            Credential = grant.Credential,
            Stage = OnboardingStage.Active,
            CertificateNotAfter = grant.NotAfter
        };
        await Registry.UpsertAsync(registration, ct);
        return registration;
    }

    /// <summary>مستند مبني على قيد مُرحَّل فعلاً — بهذا الترتيب دائماً: يُرحَّل القيد أولاً.</summary>
    public ComplianceDocument NewDocument(
        ComplianceFlow flow,
        string number,
        decimal net = 1000.0000m,
        decimal taxRate = 0.15m,
        IssuingUnitId? unit = null)
    {
        var u = unit ?? Unit;
        var tax = decimal.Round(net * taxRate, 4, MidpointRounding.ToEven);
        var gross = net + tax;
        var entry = Ledger.Post(Tenant, u, number, net, tax, gross, Clock.GetUtcNow());

        return new ComplianceDocument(
            ComplianceDocumentId.New(),
            Guid.CreateVersion7(),
            Tenant,
            u,
            ComplianceDocumentKind.Invoice,
            flow,
            number,
            Clock.GetUtcNow(),
            "SAR",
            new PartyRef("سلاسل بابل للمقاولات", "Salasel Babel Contracting", "300000000000003", "الرياض", "Riyadh"),
            flow == ComplianceFlow.Clearance
                ? new PartyRef("شركة العميل", "Client Co", "310000000000003", "جدة", "Jeddah")
                : null,
            [
                new DocumentLine(1, "خدمات استشارية", "Consulting services",
                    Quantity: 1.0000m, UnitPrice: net, NetAmount: net,
                    TaxRate: taxRate, TaxAmount: tax, GrossAmount: gross)
            ],
            new DocumentTotals(net, tax, gross),
            entry);
    }

    public ComplianceRecord Record(ComplianceDocumentId id) =>
        Store.Peek(id) ?? throw new InvalidOperationException("لا سجل");

    public IReadOnlyList<ComplianceStatus> StatusPath(ComplianceDocumentId id)
    {
        var t = Store.PeekTransitions(id);
        return t.Count == 0 ? [] : [t[0].From, .. t.Select(x => x.To)];
    }

    public void Dispose() => Provider.Dispose();
}
