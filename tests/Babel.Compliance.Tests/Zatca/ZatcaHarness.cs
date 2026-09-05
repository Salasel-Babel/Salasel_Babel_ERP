using Babel.Compliance.Abstractions;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Store;
using Babel.Compliance.Zatca;
using Babel.Compliance.Zatca.Signing;
using Babel.Compliance.Zatca.Transport;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// تركيب كامل: <b>المُنسِّق الحقيقي</b> من <c>Babel.Compliance</c> فوق <b>المزوّد الحقيقي</b>
/// للهيئة، بسلك وهمي بدل الشبكة.
/// <para/>
/// وهذا هو الإثبات الذي لا يُغني عنه اختبار وحدة: أن الحدّ يحتمل المزوّد الحقيقي بلا
/// تعديل سطر واحد في المُنسِّق — لا مقاصة، ولا إبلاغ، ولا حجز عدّاد، ولا حارس حصانة.
/// </summary>
public sealed class ZatcaHarness : IDisposable
{
    public ZatcaHarness(DateTimeOffset? start = null, ComplianceSettings? settings = null)
    {
        Clock = new ManualClock(start ?? ZatcaFixtures.IssuedAt);
        Wire = new FakeZatcaWire(Clock);
        Keys = new EphemeralZatcaKeyStore();
        Secrets = new DictionarySecretResolver { ["vault://zatca/secret"] = "test-secret-not-a-credential" };

        Settings = settings ?? new ComplianceSettings();

        Provider = new ZatcaComplianceProvider(
            new ZatcaSettings(
                new Uri("https://gw-fatoora.example.invalid/e-invoicing/simulation/"),
                ComplianceEnvironment.Simulation,
                ZatcaFixtures.Seller,
                ClearanceTimeout: TimeSpan.FromSeconds(30),
                ReportingTimeout: TimeSpan.FromSeconds(30)),
            Wire,
            Secrets,
            credential => new ZatcaCredential(credential, new SecretRef("vault://zatca/secret")),
            Clock,
            Keys);

        Store = new InMemoryComplianceStore { Clock = Clock };
        Registry = new InMemoryIssuingUnitRegistry();
        Ledger = new FakeLedger();

        Factory = new ComplianceDocumentFactory(Store, Provider.Renderer, Provider, Registry, Clock);
        Clearance = new ClearanceCoordinator(Store, Provider, Registry, Settings, Clock);
        Reporting = new ReportingWorker(Store, Provider, Registry, Settings, Clock);
        Service = new ComplianceService(Factory, Clearance, Reporting, Store, Settings, Clock);
    }

    public ManualClock Clock { get; }
    public FakeZatcaWire Wire { get; }
    public EphemeralZatcaKeyStore Keys { get; }
    public DictionarySecretResolver Secrets { get; }
    public ComplianceSettings Settings { get; }
    public ZatcaComplianceProvider Provider { get; }
    public InMemoryComplianceStore Store { get; }
    public InMemoryIssuingUnitRegistry Registry { get; }
    public FakeLedger Ledger { get; }
    public ComplianceDocumentFactory Factory { get; }
    public ClearanceCoordinator Clearance { get; }
    public ReportingWorker Reporting { get; }
    public ComplianceService Service { get; }

    public CredentialRef Credential { get; private set; }

    /// <summary>
    /// يُنشئ مفتاح الوحدة ويمنحها شهادة اختبار موقَّعة ذاتياً، ثم يسجّلها قادرةً على الإصدار.
    /// <b>لا شهادة ولا مفتاح من المستودع</b> — كلاهما يُولَّد الآن ويموت مع العملية.
    /// </summary>
    public async Task<IssuingUnitRegistration> OnboardAsync(CancellationToken ct = default)
    {
        Credential = Keys.Create(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, ComplianceEnvironment.Simulation);
        Keys.IssueSelfSignedForTesting(Credential, "babel-zatca-harness", TimeSpan.FromDays(30), Clock.GetUtcNow());

        IssuingUnitRegistration registration = new()
        {
            Tenant = ZatcaFixtures.Tenant,
            IssuingUnit = ZatcaFixtures.Unit,
            Environment = ComplianceEnvironment.Simulation,
            DisplayNameAr = "نقطة بيع ١",
            DisplayNameEn = "POS 1",
            Credential = Credential,
            Stage = OnboardingStage.Active,
            CertificateNotAfter = Clock.GetUtcNow().AddDays(30)
        };

        await Registry.UpsertAsync(registration, ct);
        return registration;
    }

    /// <summary>مستند مبني على قيد مُرحَّل فعلاً — بهذا الترتيب دائماً: يُرحَّل القيد أولاً.</summary>
    public ComplianceDocument NewDocument(ComplianceFlow flow, string number)
    {
        const decimal net = 1000.00m;
        const decimal tax = 150.00m;
        const decimal gross = 1150.00m;

        JournalEntryRef entry = Ledger.Post(
            ZatcaFixtures.Tenant, ZatcaFixtures.Unit, number, net, tax, gross, Clock.GetUtcNow());

        return new ComplianceDocument(
            ComplianceDocumentId.New(),
            Guid.CreateVersion7(),
            ZatcaFixtures.Tenant,
            ZatcaFixtures.Unit,
            ComplianceDocumentKind.Invoice,
            flow,
            number,
            Clock.GetUtcNow(),
            "SAR",
            ZatcaFixtures.SellerParty,
            flow == ComplianceFlow.Clearance ? ZatcaFixtures.BuyerParty : null,
            [
                new DocumentLine(1, "خدمات استشارية هندسية", "Engineering consultancy",
                    Quantity: 1m, UnitPrice: net, NetAmount: net,
                    TaxRate: 15m, TaxAmount: tax, GrossAmount: gross)
            ],
            new DocumentTotals(net, tax, gross),
            entry);
    }

    public void Dispose()
    {
        // ‏**والمزوّد لم يعد يملك مخزناً فيتخلّص منه**: صار المخزن مُعامِلاً إلزامياً
        // يمرّره من يركّب، فمن ملَكه يتخلّص منه — وهو هذا المِرقاب.
        Keys.Dispose();
    }
}

/// <summary>
/// مُحلّل أسرار للاختبار. <b>لا سرّ حقيقي هنا</b>: القيم من اختراع الاختبار، ولا تُقرأ
/// من ملف ولا تُودَع في المستودع.
/// </summary>
public sealed class DictionarySecretResolver : IZatcaSecretResolver
{
    private readonly Dictionary<string, string> _values = new(StringComparer.Ordinal);

    public string this[string handle]
    {
        get => _values[handle];
        set => _values[handle] = value;
    }

    public ValueTask<string> ResolveAsync(SecretRef secret, CancellationToken ct) =>
        _values.TryGetValue(secret.Value, out string? value)
            ? ValueTask.FromResult(value)
            : throw new ZatcaConfigurationException($"لا سرّ للمقبض {secret}");
}
