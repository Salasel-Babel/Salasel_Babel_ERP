using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Canonicalization;

namespace Babel.Compliance.Zatca.Signing;

/// <summary>ما ينتجه بناء التوقيع، للفحص والمتجهات الذهبية.</summary>
public sealed record XadesSignatureParts(
    byte[] SignedPropertiesCanonical,
    byte[] SignedPropertiesDigest,
    byte[] SignedInfoCanonical,
    byte[] SignedInfoDigest,
    byte[] SignatureValue,
    byte[] CertificateDer,
    DateTimeOffset SigningTime);

/// <summary>
/// الطور الأول من التوقيع: كل ما بُني وحُسب قبل أن يقع التوقيع نفسه.
/// </summary>
public sealed record XadesPreparation(
    XElement SignatureElement,
    byte[] SignedPropertiesCanonical,
    byte[] SignedPropertiesDigest,
    byte[] SignedInfoCanonical,
    byte[] SignedInfoDigest,
    byte[] CertificateDer,
    DateTimeOffset SigningTime);

/// <summary>
/// بناء توقيع <b>XAdES B-B</b> مُغلَّف داخل امتدادات UBL.
/// <para/>
/// <b>ترتيب العمليات هنا ليس أسلوباً — هو العقد نفسه، وأي تبديل فيه يُنتج توقيعاً
/// يتحقّق في أداة XML عامة ويُرفض عند الجهة:</b>
/// <list type="number">
///   <item>تُبنى <c>SignedProperties</c> كاملةً <b>داخل</b> الشجرة: وقت التوقيع، وبصمة
///         الشهادة، ومُصدِرها، ورقمها التسلسلي. ولا شيء منها يعتمد على التوقيع.</item>
///   <item>تُوحَّد <c>SignedProperties</c> قياسياً <b>في موضعها</b> — مع تصريحات مساحات
///         الأسماء السارية عليها من أسلافها — وتُجزَّأ. حسابها على نسخة مقتطعة يعطي
///         بايتات أقصر وبصمة أخرى.</item>
///   <item>تُملأ بصمتها في مرجع <c>SignedInfo</c> الثاني، وبصمة الفاتورة في الأول.</item>
///   <item>تُوحَّد <c>SignedInfo</c> قياسياً وتُجزَّأ، و<b>هذه البصمة وحدها هي ما يُوقَّع</b>.
///         توقيع بصمة الفاتورة مباشرةً خطأ شائع: يُنتج توقيعاً لا يغطّي وقت التوقيع
///         ولا هوية الشهادة.</item>
///   <item>يُكتب التوقيع والشهادة، ثم يُبنى رمز QR — لأنه يحمل التوقيع، فلا يمكن أن يسبقه.</item>
/// </list>
/// <para/>
/// وكل ما سبق يقع <b>خارج البايتات المُجزَّأة للفاتورة</b>: امتدادات UBL ورمز QR
/// وعنصر <c>cac:Signature</c> مستبعَدة، ولذلك لا يتحرّك <c>invoiceHash</c> بحقنها —
/// وهذه الخاصية <b>مفحوصة بعد الختم</b> في <see cref="ZatcaSealer"/>، لا مفترضة.
/// </summary>
public sealed class XadesSignatureWriter(
    ZatcaCanonicalXml canonicaliser,
    ZatcaDigestPolicy? digests = null)
{
    private static readonly XNamespace Ds = ZatcaProfile.Ds;
    private static readonly XNamespace Xades = ZatcaProfile.Xades;
    private static readonly XNamespace Ext = ZatcaProfile.Ext;
    private static readonly XNamespace Cbc = ZatcaProfile.Cbc;
    private static readonly XNamespace SigNs = ZatcaProfile.Sig;
    private static readonly XNamespace Sac = ZatcaProfile.Sac;
    private static readonly XNamespace Sbc = ZatcaProfile.Sbc;

    private readonly ZatcaDigestPolicy _digests = digests ?? ZatcaDigestPolicy.Default;

    public ZatcaDigestPolicy Digests => _digests;

    /// <summary>
    /// <b>الطور الأول:</b> يحقن هيكل التوقيع في الشجرة، ويحسب كل ما لا يعتمد على التوقيع،
    /// ويعيد <b>بصمة <c>SignedInfo</c></b> — وهي وحدها ما يُوقَّع.
    /// <para/>
    /// الفصل إلى طورين مقصود: التوقيع نداء غير متزامن قد يذهب إلى خزينة مفاتيح أو جهاز،
    /// وتغليفه داخل مُفوَّض متزامن يُنتج انتظاراً متزامناً فوق نداء غير متزامن — وهو نمط
    /// يجمّد الخيوط تحت الحمل ويظهر عند العميل لا في الاختبار.
    /// </summary>
    public XadesPreparation Prepare(
        XElement tree,
        ReadOnlyMemory<byte> invoiceDigest,
        ReadOnlyMemory<byte> certificateDer,
        DateTimeOffset signingTime)
    {
        ArgumentNullException.ThrowIfNull(tree);

        if (certificateDer.IsEmpty)
        {
            throw new ZatcaSigningException(
                "لا شهادة مرتبطة بالمقبض. توقيع XAdES يوجب أن تدخل بصمة الشهادة ومُصدِرها ورقمها " +
                "التسلسلي **داخل** الخصائص الموقَّعة، فالشهادة مطلوبة قبل التوقيع لا بعده. / " +
                "XAdES requires the certificate digest, issuer and serial inside the signed properties.");
        }

        XElement extensions = tree.Element(Ext + "UBLExtensions")
            ?? throw new ZatcaSigningException(
                "الشجرة بلا موضع امتدادات. المستند يُبنى بموضع فارغ يسكنه التوقيع؛ غيابه يعني " +
                "أن التوقيع سيُلحق في موضع غير مستبعَد فيغيّر بصمة الفاتورة. / the tree carries no UBLExtensions slot.");

        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(certificateDer.Span);

        XElement signedProperties = BuildSignedProperties(certificate, certificateDer, signingTime);
        XElement signedInfo = BuildSignedInfo(invoiceDigest);
        XElement signature = Assemble(signedInfo, signedProperties, certificateDer);

        extensions.Add(BuildExtension(signature));

        // (2) البصمة تُحسب والعنصر **في موضعه**، بعد أن صار له أسلاف وتصريحات مساحات أسماء.
        byte[] signedPropertiesCanonical = canonicaliser.CanonicaliseInScope(signedProperties);
        byte[] signedPropertiesDigest = ZatcaDigests.Sha256(signedPropertiesCanonical);

        // (3) تُملأ في المرجع الثاني، **قبل** توحيد SignedInfo قياسياً.
        XElement propertiesReference = signedInfo
            .Elements(Ds + "Reference")
            .First(static r => (string?)r.Attribute("URI") == "#" + ZatcaProfile.SignedPropertiesId);
        propertiesReference.Element(Ds + "DigestValue")!.Value =
            ZatcaDigests.Render(signedPropertiesDigest, _digests.SignedPropertiesReference);

        // (4) وهذه هي البصمة التي تُوقَّع — لا بصمة الفاتورة.
        byte[] signedInfoCanonical = canonicaliser.CanonicaliseInScope(signedInfo);
        byte[] signedInfoDigest = ZatcaDigests.Sha256(signedInfoCanonical);

        return new XadesPreparation(
            signature,
            signedPropertiesCanonical,
            signedPropertiesDigest,
            signedInfoCanonical,
            signedInfoDigest,
            certificateDer.ToArray(),
            signingTime);
    }

    /// <summary><b>الطور الثاني:</b> يكتب التوقيع في موضعه ويعيد أجزاء التوقيع كاملةً.</summary>
    public static XadesSignatureParts Complete(XadesPreparation preparation, ReadOnlyMemory<byte> signatureValue)
    {
        ArgumentNullException.ThrowIfNull(preparation);

        if (signatureValue.IsEmpty)
        {
            throw new ZatcaSigningException(
                "توقيع فارغ. مستند بعنصر توقيع فارغ يُبنى بنجاح ويُرفض عند الجهة. / an empty signature value is refused.");
        }

        preparation.SignatureElement.Element(Ds + "SignatureValue")!.Value =
            Convert.ToBase64String(signatureValue.Span);

        return new XadesSignatureParts(
            preparation.SignedPropertiesCanonical,
            preparation.SignedPropertiesDigest,
            preparation.SignedInfoCanonical,
            preparation.SignedInfoDigest,
            signatureValue.ToArray(),
            preparation.CertificateDer,
            preparation.SigningTime);
    }

    private XElement BuildSignedProperties(
        X509Certificate2 certificate, ReadOnlyMemory<byte> certificateDer, DateTimeOffset signingTime)
    {
        byte[] certificateDigest = ZatcaDigests.Sha256(ZatcaDigests.CertificateDigestInput(certificateDer.Span));

        return new XElement(Xades + "SignedProperties",
            new XAttribute("Id", ZatcaProfile.SignedPropertiesId),
            new XElement(Xades + "SignedSignatureProperties",
                new XElement(Xades + "SigningTime", SigningTimestamp(signingTime)),
                new XElement(Xades + "SigningCertificate",
                    new XElement(Xades + "Cert",
                        new XElement(Xades + "CertDigest",
                            new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", ZatcaProfile.DigestAlgorithm)),
                            new XElement(Ds + "DigestValue",
                                ZatcaDigests.Render(certificateDigest, _digests.CertificateDigest))),
                        new XElement(Xades + "IssuerSerial",
                            new XElement(Ds + "X509IssuerName", certificate.IssuerName.Name),
                            new XElement(Ds + "X509SerialNumber", SerialNumber(certificate)))))));
    }

    /// <summary>
    /// وقت التوقيع. <b>ثانية واحدة بلا كسور، و UTC صريح، وثقافة ثابتة.</b>
    /// تحت <c>ar-SA</c> يعطي التنسيق الافتراضي تاريخاً هجرياً يحمل بداخله <c>U+200F</c>
    /// — وقيمة كهذه تدخل البايتات الموقَّعة فتُنتج توقيعاً على نصّ لا يفهمه أحد.
    /// </summary>
    [Provisional("تنسيق وقت التوقيع داخل الخصائص الموقَّعة ودقّته ومنطقته الزمنية",
        DerivedFrom = "قراءة المواصفة — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "المثال المرجعي الموقَّع المنشور مع مواصفة الختم التشفيري")]
    public static string SigningTimestamp(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// الرقم التسلسلي عدداً عشرياً. <b>لا ستّ‌عشرياً</b>: العنصر
    /// <c>ds:X509SerialNumber</c> معرَّف عدداً صحيحاً في مخطّط XMLDSig، وكتابته ستّ‌عشرياً
    /// تُنتج مستنداً يمرّ في محرّر ويُرفض عند التحقّق.
    /// </summary>
    private static string SerialNumber(X509Certificate2 certificate)
    {
        System.Numerics.BigInteger value = new(certificate.GetSerialNumber(), isUnsigned: true, isBigEndian: false);
        return value.ToString(CultureInfo.InvariantCulture);
    }

    private static XElement BuildSignedInfo(ReadOnlyMemory<byte> invoiceDigest) =>
        new(Ds + "SignedInfo",
            new XElement(Ds + "CanonicalizationMethod",
                new XAttribute("Algorithm", ZatcaProfile.CanonicalizationAlgorithm)),
            new XElement(Ds + "SignatureMethod",
                new XAttribute("Algorithm", ZatcaProfile.SignatureAlgorithm)),
            new XElement(Ds + "Reference",
                new XAttribute("Id", "invoiceSignedData"),
                new XAttribute("URI", string.Empty),
                new XElement(Ds + "Transforms",
                    XPathTransform("not(//ancestor-or-self::ext:UBLExtensions)"),
                    XPathTransform("not(//ancestor-or-self::cac:Signature)"),
                    XPathTransform("not(//ancestor-or-self::cac:AdditionalDocumentReference[cbc:ID='QR'])"),
                    new XElement(Ds + "Transform",
                        new XAttribute("Algorithm", ZatcaProfile.CanonicalizationAlgorithm))),
                new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", ZatcaProfile.DigestAlgorithm)),
                new XElement(Ds + "DigestValue", Convert.ToBase64String(invoiceDigest.Span))),
            new XElement(Ds + "Reference",
                new XAttribute("Type", ZatcaProfile.SignedPropertiesReferenceType),
                new XAttribute("URI", "#" + ZatcaProfile.SignedPropertiesId),
                new XElement(Ds + "DigestMethod", new XAttribute("Algorithm", ZatcaProfile.DigestAlgorithm)),
                new XElement(Ds + "DigestValue", string.Empty)));

    private static XElement XPathTransform(string expression) =>
        new(Ds + "Transform",
            new XAttribute("Algorithm", ZatcaProfile.TransformXPath),
            new XElement(Ds + "XPath", expression));

    private static XElement Assemble(XElement signedInfo, XElement signedProperties, ReadOnlyMemory<byte> certificateDer) =>
        new(Ds + "Signature",
            new XAttribute(XNamespace.Xmlns + "ds", Ds.NamespaceName),
            new XAttribute("Id", ZatcaProfile.SignatureElementId),
            signedInfo,
            new XElement(Ds + "SignatureValue", string.Empty),
            new XElement(Ds + "KeyInfo",
                new XElement(Ds + "X509Data",
                    new XElement(Ds + "X509Certificate", Convert.ToBase64String(certificateDer.Span)))),
            new XElement(Ds + "Object",
                new XElement(Xades + "QualifyingProperties",
                    new XAttribute(XNamespace.Xmlns + "xades", Xades.NamespaceName),
                    new XAttribute("Target", ZatcaProfile.SignatureElementId),
                    signedProperties)));

    private static XElement BuildExtension(XElement signature) =>
        new(Ext + "UBLExtension",
            new XElement(Ext + "ExtensionURI", ZatcaProfile.SignatureMethod),
            new XElement(Ext + "ExtensionContent",
                new XElement(SigNs + "UBLDocumentSignatures",
                    new XAttribute(XNamespace.Xmlns + "sig", SigNs.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "sac", Sac.NamespaceName),
                    new XAttribute(XNamespace.Xmlns + "sbc", Sbc.NamespaceName),
                    new XElement(Sac + "SignatureInformation",
                        new XElement(Cbc + "ID", "urn:oasis:names:specification:ubl:signature:1"),
                        new XElement(Sbc + "ReferencedSignatureID", ZatcaProfile.SignatureId),
                        signature))));
}
