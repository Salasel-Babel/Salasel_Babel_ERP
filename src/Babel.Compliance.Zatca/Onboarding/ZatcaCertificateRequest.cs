using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Onboarding;

/// <summary>
/// بناء طلب توقيع الشهادة (‏CSR).
/// <para/>
/// <b>مقيس على .NET 10.0.111:</b> المنصّة القياسية تبني هذا الشكل كاملاً — منحنى
/// <c>secp256k1</c>، وامتداد قالب على المعرّف <c>1.3.6.1.4.1.311.20.2</c>، واسم بديل
/// من نوع <c>directoryName</c> يحمل معرّفات RDN مخصّصة. <b>BouncyCastle غير مطلوب.</b>
/// <para/>
/// <b>وترتيب معرّفات RDN مفروض هنا بقائمة معلنة، لا مأخوذ من ترتيب القاموس.</b>
/// عقد <c>CsrSubject</c> يحمل المعرّفات في <c>IReadOnlyDictionary</c>، وهو نوع
/// <b>غير مرتَّب بالتعريف</b> — بينما ترتيب مكوّنات الاسم المميّز <b>جزء من ترميزه</b>،
/// فترتيبان مختلفان يعطيان بايتات مختلفة ويعطيان طلبين مختلفين. الترتيب هنا مثبَّت في
/// <see cref="RdnOrder"/>، وأي مفتاح خارجها يُرفض بدل أن يُلحق في موضع عشوائي.
/// </summary>
public static class ZatcaCertificateRequest
{
    /// <summary>معرّف امتداد قالب الشهادة.</summary>
    [Provisional("معرّف امتداد القالب واسم القالب المطلوب لكل بيئة",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة CSR المنشورة ونموذج الإعداد المرافق لها")]
    public const string CertificateTemplateOid = "1.3.6.1.4.1.311.20.2";

    /// <summary>معرّف امتداد الاسم البديل للموضوع.</summary>
    public const string SubjectAlternativeNameOid = "2.5.29.17";

    /// <summary>
    /// ترتيب معرّفات RDN داخل الاسم البديل، <b>مثبَّتاً</b>.
    /// </summary>
    [Provisional("أسماء معرّفات RDN الخمسة وترتيبها ودلالة كل واحد وقيمه المقبولة",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Structural,
        VerifyBy = "مواصفة CSR المنشورة ونموذج الإعداد المرافق لها")]
    public static IReadOnlyList<string> RdnOrder { get; } =
        ["SN", "UID", "title", "registeredAddress", "businessCategory"];

    /// <summary>
    /// المعرّف الرقمي لكل اسم مألوف. <b>الأسماء المألوفة لا تُرمَّز</b> — ترميز الاسم
    /// المميّز يحمل معرّفات رقمية حصراً، والمنصّة ترفض الاسم المألوف بـ«معرّف غير صالح».
    /// وقد رفضته فعلاً في هذا الفرع، وكُشف باختبار لا بمراجعة.
    /// </summary>
    public static IReadOnlyDictionary<string, string> RdnOids { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["SN"] = "2.5.4.5",                              // serialNumber
            ["UID"] = "0.9.2342.19200300.100.1.1",           // userId
            ["title"] = "2.5.4.12",                          // title
            ["registeredAddress"] = "2.5.4.26",              // registeredAddress
            ["businessCategory"] = "2.5.4.15"                // businessCategory
        };

    /// <summary>
    /// اسم القالب لكل بيئة. <b>بيئتان منفصلتان تماماً</b>: شهادات وإعدادات ومسارات
    /// مستقلة، ولا تُنقل شهادة محاكاة إلى إنتاج ولا العكس.
    /// </summary>
    [Provisional("اسم القالب المطلوب لكل بيئة",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "دليل التسجيل المنشور على بوابة الهيئة")]
    public static string TemplateFor(ComplianceEnvironment environment) => environment switch
    {
        ComplianceEnvironment.Simulation => "PREZATCA-Code-Signing",
        ComplianceEnvironment.Production => "ZATCA-Code-Signing",
        _ => throw new ArgumentOutOfRangeException(nameof(environment), environment, "بيئة غير معروفة / unknown environment")
    };

    /// <summary>يبني الطلب بترميز DER. المفتاح الخاص يبقى داخل المُستدعي ولا يُصدَّر.</summary>
    public static byte[] Build(ECDsa key, CsrSubject subject)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(subject);

        string distinguishedName =
            $"CN={subject.CommonName}, OU={subject.OrganisationalUnitName}, " +
            $"O={subject.OrganisationName}, C={subject.CountryCode}";

        CertificateRequest request = new(
            new X500DistinguishedName(distinguishedName), key, HashAlgorithmName.SHA256);

        AsnWriter template = new(AsnEncodingRules.DER);
        template.WriteCharacterString(UniversalTagNumber.UTF8String, subject.CertificateTemplateName);
        request.CertificateExtensions.Add(
            new X509Extension(new Oid(CertificateTemplateOid), template.Encode(), critical: false));

        request.CertificateExtensions.Add(
            new X509Extension(new Oid(SubjectAlternativeNameOid), BuildDirectoryName(subject), critical: false));

        return request.CreateSigningRequest();
    }

    /// <summary>
    /// ‏<c>SubjectAltName ::= SEQUENCE OF GeneralName</c>، و<c>GeneralName</c> من نوع
    /// <c>directoryName</c> هو <c>[4] Name</c>. البانِي القياسي
    /// <c>SubjectAlternativeNameBuilder</c> لا يدعم <c>directoryName</c>، فتُبنى البنية
    /// بالترميز مباشرةً.
    /// </summary>
    private static byte[] BuildDirectoryName(CsrSubject subject)
    {
        List<string> unknown =
        [
            .. subject.SubjectAlternativeNameRdns.Keys
                .Where(key => !RdnOrder.Contains(key, StringComparer.Ordinal))
                .Order(StringComparer.Ordinal)
        ];

        if (unknown.Count > 0)
        {
            throw new ArgumentException(
                $"معرّفات RDN غير معروفة: {string.Join("، ", unknown)}. " +
                $"الترتيب المعلن هو {string.Join(" ← ", RdnOrder)}، وإلحاق معرّف خارجها في موضع " +
                "عشوائي يغيّر بايتات الاسم المميّز ويعطي طلباً آخر. / " +
                $"unknown RDN identifiers: {string.Join(", ", unknown)}.",
                nameof(subject));
        }

        X500DistinguishedNameBuilder builder = new();
        int written = 0;

        foreach (string rdn in RdnOrder)
        {
            if (subject.SubjectAlternativeNameRdns.TryGetValue(rdn, out string? value))
            {
                builder.Add(RdnOids[rdn], value, UniversalTagNumber.UTF8String);
                written++;
            }
        }

        if (written == 0)
        {
            throw new ArgumentException(
                "الاسم البديل بلا معرّف واحد. طلبٌ بلا معرّفات يُبنى بنجاح ويُرفض عند التسجيل. / "
                + "an empty subject alternative name builds successfully and is refused at onboarding.",
                nameof(subject));
        }

        byte[] directoryName = builder.Build().RawData;

        AsnWriter writer = new(AsnEncodingRules.DER);
        using (writer.PushSequence())
        using (writer.PushSequence(new Asn1Tag(TagClass.ContextSpecific, 4, isConstructed: true)))
        {
            writer.WriteEncodedValue(directoryName);
        }

        return writer.Encode();
    }

    /// <summary>
    /// يبني موضوع الطلب من إعداد وحدة الإصدار. <b>موضع واحد يعرف أسماء المعرّفات</b>،
    /// فلا تُكتب حرفياً في شيفرة استدعاء.
    /// </summary>
    public static CsrSubject SubjectFor(ZatcaUnitRegistration registration, ComplianceEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(registration);

        return new CsrSubject(
            CommonName: registration.CommonName,
            OrganisationName: registration.OrganisationNameAr,
            OrganisationalUnitName: registration.OrganisationalUnitName,
            CountryCode: registration.CountryCode,
            SubjectAlternativeNameRdns: new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["SN"] = registration.DeviceSerialNumber,
                ["UID"] = registration.VatRegistrationNumber,
                ["title"] = registration.InvoiceTypeFlags,
                ["registeredAddress"] = registration.RegisteredAddress,
                ["businessCategory"] = registration.BusinessCategory
            },
            CertificateTemplateName: TemplateFor(environment));
    }
}

/// <summary>
/// إعداد وحدة إصدار واحدة كما يلزم للتسجيل. <b>لكل جهاز سجلّ</b>، لا سجلّ لكل مستأجر.
/// </summary>
public sealed record ZatcaUnitRegistration(
    string CommonName,
    string OrganisationNameAr,
    string OrganisationalUnitName,
    string VatRegistrationNumber,
    string DeviceSerialNumber,
    string RegisteredAddress,
    string BusinessCategory,
    string CountryCode = "SA",
    [property: Provisional("شكل علم أنواع الفواتير في المعرّف title وعدد خاناته",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة CSR المنشورة")]
    string InvoiceTypeFlags = "1100")
{
    /// <summary>
    /// الرقم التسلسلي بالشكل المركّب: اسم الحل، والطراز، والرقم التسلسلي للجهاز.
    /// </summary>
    [Provisional("شكل الرقم التسلسلي المركّب وفواصله",
        DerivedFrom = "تنفيذات مفتوحة المصدر — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة CSR المنشورة")]
    public static string ComposeSerial(string solutionName, string model, string serial) =>
        string.Create(CultureInfo.InvariantCulture, $"1-{solutionName}|2-{model}|3-{serial}");
}
