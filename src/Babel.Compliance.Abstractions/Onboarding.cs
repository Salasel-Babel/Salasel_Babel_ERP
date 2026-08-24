namespace Babel.Compliance.Abstractions;

/// <summary>
/// دورة حياة وحدة الإصدار. <b>خمسون نقطة بيع = خمسون وحدة إصدار</b>، لكل منها
/// شهادتها ومسارها المستقل. هذا التعداد هو ما تعرضه شاشة إدارة وحدات الإصدار.
/// <para/>
/// ترتيب المراحل مأخوذ من وثيقة التخطيط الداخلية، لا من الهيئة.
/// </summary>
[Provisional("عدد مراحل التسجيل وترتيبها وأسماء كل مرحلة",
    DerivedFrom = "docs/analysis/04-zatca-integration.md §4 — وهي وثيقة تخطيط داخلية",
    Risk = ProvisionalRisk.Structural,
    VerifyBy = "دليل التسجيل (Onboarding) المنشور على بوابة الهيئة")]
public enum OnboardingStage
{
    NotStarted,
    KeyCreated,
    SigningRequestBuilt,
    ComplianceCertificateIssued,
    ComplianceChecksPassed,
    ProductionCertificateIssued,
    Active,
    RenewalDue,
    Revoked,
    Expired
}

/// <summary>
/// موضوع طلب توقيع الشهادة. <b>مقيس على .NET 10:</b> منحنى secp256k1 وطلب CSR بشكل كامل
/// مع امتداد قالب (OID 1.3.6.1.4.1.311.20.2) و SAN من نوع directoryName يحمل خمسة
/// معرّفات RDN مخصّصة — كلها تعمل على <c>System.Security.Cryptography</c> القياسية.
/// BouncyCastle غير مطلوب.
/// <para/>
/// <b>ما هو مقيس:</b> أن المنصّة قادرة على بناء هذا الشكل.
/// <b>ما هو غير مُتحقَّق منه:</b> أيّ قيمة تذهب في أيّ RDN، وما القيم المقبولة.
/// </summary>
public sealed record CsrSubject(
    string CommonName,
    string OrganisationName,
    string OrganisationalUnitName,
    string CountryCode,
    [property: Provisional("معرّفات RDN المخصّصة الخمسة وقيمها المقبولة داخل SAN من نوع directoryName",
        DerivedFrom = "تنفيذات مفتوحة المصدر أثبتت الشكل؛ دلالة كل حقل وقيمه المقبولة غير مُتحقَّق منها",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة CSR المنشورة ونموذج الإعداد المرافق لها")]
    IReadOnlyDictionary<string, string> SubjectAlternativeNameRdns,
    [property: Provisional("معرّف قالب الشهادة المطلوب في الامتداد 1.3.6.1.4.1.311.20.2",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة CSR المنشورة")]
    string CertificateTemplateName);

public sealed record CsrRequest(
    TenantId Tenant,
    IssuingUnitId IssuingUnit,
    ComplianceEnvironment Environment,
    CsrSubject Subject);

/// <summary>ناتج بناء طلب التوقيع: البايتات ومقبض الاعتماد. لا مفتاح خاص.</summary>
public sealed record CsrMaterial(
    CredentialRef Credential,
    ReadOnlyMemory<byte> CsrDer)
{
    public string CsrPem =>
        "-----BEGIN CERTIFICATE REQUEST-----\n" +
        Convert.ToBase64String(CsrDer.Span, Base64FormattingOptions.InsertLineBreaks) +
        "\n-----END CERTIFICATE REQUEST-----\n";
}

/// <summary>
/// كلمة المرور لمرة واحدة تُؤخذ من بوابة الجهة يدوياً. <b>مقبض إلى سرّ، لا السرّ نفسه</b> —
/// كي لا تظهر في سجل ولا في تتبّع ولا في رسالة خطأ.
/// </summary>
public sealed record OneTimePasswordRef(SecretRef Secret, DateTimeOffset ObtainedAt);

/// <summary>منحة شهادة. لا تحمل مادة سرّية — الشهادة عامة، والسرّ المرافق مقبض.</summary>
public sealed record CertificateGrant(
    CredentialRef Credential,
    ReadOnlyMemory<byte> CertificateDer,
    [property: Provisional("وجود سرّ مرافق للشهادة وطريقة استعماله في المصادقة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة المصادقة على واجهات الإرسال")]
    SecretRef ApiSecret,
    DateTimeOffset IssuedAt,
    DateTimeOffset? NotAfter,
    OnboardingStage Stage);

public sealed record ComplianceCheckResult(
    bool Passed,
    IReadOnlyList<ComplianceNotice> Notices,
    int DocumentsExercised);

/// <summary>
/// دورة حياة الشهادة عبر الحدّ. <b>التوقيعات نفسها تحت الشكلين</b> —
/// وهذا هو الجزء الوحيد الذي نجح فيه التعميم بلا ثمن يُذكر.
/// الفرق يقع كله <b>داخل</b> التنفيذ: من يولّد المفتاح ومن يحوزه.
/// </summary>
public interface IOnboardingChannel
{
    /// <summary>
    /// تحت «نحن نحوز»: نولّد المفتاح محلياً ونبني الطلب، والمزوّد ينقله فقط.
    /// تحت «المزوّد يحوز»: المزوّد يولّد المفتاح ويبني الطلب، ولا نرى المفتاح أبداً.
    /// </summary>
    ValueTask<CsrMaterial> CreateSigningRequestAsync(CsrRequest request, CancellationToken cancellationToken);

    ValueTask<CertificateGrant> RequestComplianceCertificateAsync(
        CredentialRef credential, OneTimePasswordRef otp, CancellationToken cancellationToken);

    ValueTask<ComplianceCheckResult> RunComplianceChecksAsync(
        CredentialRef credential, CancellationToken cancellationToken);

    ValueTask<CertificateGrant> RequestProductionCertificateAsync(
        CredentialRef credential, CancellationToken cancellationToken);

    ValueTask<CertificateGrant> RenewProductionCertificateAsync(
        CredentialRef credential, CancellationToken cancellationToken);

    ValueTask RevokeAsync(CredentialRef credential, string reason, CancellationToken cancellationToken);
}
