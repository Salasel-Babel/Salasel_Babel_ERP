using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Canonicalization;
using Babel.Compliance.Zatca.Documents;
using Babel.Compliance.Zatca.Onboarding;
using Babel.Compliance.Zatca.Qr;
using Babel.Compliance.Zatca.Signing;
using Babel.Compliance.Zatca.Transport;

namespace Babel.Compliance.Zatca;

/// <summary>إعداد المزوّد. <b>لا سرّ ولا مفتاح ولا شهادة هنا — مقابض وعناوين ومهل فقط.</b></summary>
public sealed record ZatcaSettings(
    Uri BaseAddress,
    ComplianceEnvironment Environment,
    ZatcaSellerIdentity Seller,
    TimeSpan ClearanceTimeout,
    TimeSpan ReportingTimeout)
{
    /// <summary>ترميز البصمات في المواضع الثلاثة. يُبدَّل حين تُقرأ المواصفة.</summary>
    public ZatcaDigestPolicy Digests { get; init; } = ZatcaDigestPolicy.Default;

    /// <summary>شكل قيم وسوم رمز QR الأربعة. يُبدَّل حين تُقرأ المواصفة.</summary>
    public ZatcaQrValueForms QrForms { get; init; } = ZatcaQrValueForms.Default;

    /// <summary>ترميز توقيع ECDSA.</summary>
    public EcdsaSignatureFormat SignatureFormat { get; init; } = EcdsaSignatureFormat.DerSequence;

    /// <summary>أعلام نوع الفاتورة الخمسة.</summary>
    public InvoiceTraits Flags { get; init; } = InvoiceTraits.None;
}

/// <summary>
/// <b>المزوّد الحقيقي: المرحلة الأولى (التوليد) والمرحلة الثانية (التكامل).</b>
/// <para/>
/// وما يُصرَّح به في <see cref="Capabilities"/> هو <b>ما نستطيع إثباته</b>، لا ما نأمله:
/// <list type="bullet">
///   <item>
///     <c>StatusQuery = NotSupported</c> — لا مسار قراءة موثَّق لدى الجهة. وليس هذا
///     نقصاً في التنفيذ: <b>غياب الاستعلام هو الحالة المتوقَّعة</b>، ومسار حسم الغموض
///     يعمل بدونه وينتهي إلى مراجعة بشرية بدل إعادة إرسال عمياء.
///   </item>
///   <item>
///     <c>DeduplicatesBySubmissionFingerprint = false</c> — <b>لا يُدَّعى كشف تكرار من
///     جانب الجهة بلا وثيقة.</b> الادّعاء بلا دليل هو ما يُنتج فاتورة ضريبية مُصفَّاة
///     مرتين يوم يُصدَّق. نُرسل مفتاح إحكام من جانبنا في كل محاولة، ولا نبني عليه شيئاً.
///   </item>
///   <item>
///     <c>GuaranteesByteStableRetransmission = true</c> — وهذه <b>خاصية بنيوية</b> لا
///     وعد: الختم يقع عندنا، والبايتات تُجمَّد عند أول ختم وتُخزَّن، فكل إعادة إرسال
///     مطابقة بايتياً بالضرورة.
///   </item>
/// </list>
/// </summary>
public sealed class ZatcaComplianceProvider : IComplianceProvider, IDisposable
{
    private readonly EphemeralZatcaKeyStore? _ownedKeys;

    public ZatcaComplianceProvider(
        ZatcaSettings settings,
        IZatcaWire wire,
        IZatcaSecretResolver secrets,
        Func<CredentialRef, ZatcaCredential> credentials,
        TimeProvider clock,
        IZatcaKeyStore? keys = null,
        IZatcaOnboardingState? onboardingState = null)
    {
        ArgumentNullException.ThrowIfNull(settings);

        Settings = settings;
        _ownedKeys = keys is null ? new EphemeralZatcaKeyStore() : null;
        Keys = keys ?? _ownedKeys!;

        ZatcaEndpoints endpoints = new(settings.BaseAddress);

        Renderer = new ZatcaDocumentRenderer(settings.Seller) { Flags = settings.Flags };
        LocalCustodian = new ZatcaKeyCustodian(Keys, settings.SignatureFormat);
        Sealer = new ZatcaSealer(LocalCustodian, Renderer, clock, settings.Digests, settings.QrForms);
        FlowPolicy = new ZatcaFlowPolicy();

        Onboarding = new ZatcaOnboardingChannel(
            wire, endpoints, Keys, LocalCustodian, secrets,
            onboardingState ?? new InMemoryOnboardingState(),
            credentials, settings.ClearanceTimeout, clock);

        Clearance = new ZatcaClearanceChannel(
            wire, endpoints, Renderer, Keys, secrets, credentials, settings.ClearanceTimeout, clock);

        Reporting = new ZatcaReportingChannel(
            wire, endpoints, Renderer, Keys, secrets, credentials, settings.ReportingTimeout, clock);

        Capabilities = new ProviderCapabilities(
            ProviderId: "zatca.direct",
            DisplayNameAr: "الهيئة — تكامل مباشر، ونحن نحوز المفتاح",
            DisplayNameEn: "ZATCA — direct integration, we hold the key",
            Custody: KeyCustody.SelfHeld,
            SupportsClearance: true,
            SupportsReporting: true,
            StatusQuery: ZatcaStatusQuery.Support,
            ReturnsStampedDocument: true,
            RendersDocument: true,
            ClearanceTimeout: settings.ClearanceTimeout,
            ReportingTimeout: settings.ReportingTimeout,
            DeduplicatesBySubmissionFingerprint: false,
            GuaranteesByteStableRetransmission: true);
    }

    public ZatcaSettings Settings { get; }

    public IZatcaKeyStore Keys { get; }

    public ZatcaDocumentRenderer Renderer { get; }

    public ZatcaFlowPolicy FlowPolicy { get; }

    public ZatcaKeyCustodian LocalCustodian { get; }

    public ProviderCapabilities Capabilities { get; }

    public IDocumentSealer Sealer { get; }

    public IOnboardingChannel Onboarding { get; }

    public IClearanceChannel? Clearance { get; }

    public IReportingChannel? Reporting { get; }

    /// <summary>لا استعلام حالة. الغياب مُصرَّح به في القدرات، ومسار الحسم مبنيّ عليه.</summary>
    public IComplianceStatusQuery? StatusQuery => null;

    public void Dispose() => _ownedKeys?.Dispose();
}
