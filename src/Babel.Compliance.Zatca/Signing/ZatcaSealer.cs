using System.Formats.Asn1;
using System.Globalization;
using System.Security.Cryptography.X509Certificates;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Canonicalization;
using Babel.Compliance.Zatca.Documents;
using Babel.Compliance.Zatca.Qr;

namespace Babel.Compliance.Zatca.Signing;

/// <summary>
/// <b>شكل «نحن نحوز المفتاح».</b> الختم يقع عندنا، والبايتات تُجمَّد ولا تتحرّك بعدها.
/// <para/>
/// ونتيجة بنيوية مجانية تُشترى بهذا الشكل: <b>كل إعادة إرسال مطابقة بايتياً للأولى</b>،
/// لأن التوقيع حُسب مرة واحدة وخُزِّن. الشكل الآخر — أن يختم المزوّد — يُعيد الختم في كل
/// محاولة، وتوقيع ECDSA عشوائي بطبيعته، فتختلف البايتات بين محاولتين على المستند نفسه
/// ويُضعف ذلك كشف التكرار بعد المهلة الغامضة إضعافاً حقيقياً.
/// <para/>
/// <b>وحارس واحد يقع في نهاية هذه الدالة يستحقّ أن يُقرأ:</b> بعد حقن التوقيع ورمز QR،
/// تُعاد بصمة الفاتورة من البايتات <b>المختومة</b> وتُقارن بالبصمة التي وُقِّع عليها.
/// فإن اختلفتا فقاعدة الاستبعاد لا تعمل، والمستند سيُرفض. وهذا الفحص هو الفرق بين
/// «كتبنا قاعدة الاستبعاد» و«قاعدة الاستبعاد تعمل».
/// </summary>
public sealed class ZatcaSealer(
    ILocalKeyCustodian custodian,
    ZatcaDocumentRenderer renderer,
    TimeProvider clock,
    ZatcaDigestPolicy? digests = null,
    ZatcaQrValueForms? qrForms = null) : IDocumentSealer
{
    private readonly ZatcaCanonicalXml _canonicaliser = new();
    private readonly ZatcaQrValueForms _qrForms = qrForms ?? ZatcaQrValueForms.Default;

    public KeyCustody Custody => KeyCustody.SelfHeld;

    /// <summary>أجزاء آخر توقيع — للفحص وللمتجهات الذهبية، لا لمسار الإنتاج.</summary>
    public XadesSignatureParts? LastSignature { get; private set; }

    public async ValueTask<SealedPayload> SealAsync(
        SealingContext context, RenderedDocument document, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(document);

        XElement tree = ZatcaDocumentRenderer.Parse(document.Body.Span);
        ReadOnlyMemory<byte> certificateDer = await custodian.ReadCertificateAsync(context.Credential, ct);

        DateTimeOffset signingTime = clock.GetUtcNow();

        XadesSignatureWriter writer = new(_canonicaliser, digests);
        XadesPreparation preparation = writer.Prepare(
            tree, document.SigningInputDigest, certificateDer, signingTime);

        // التصريح بشكل المدخل صريح، والموقِّع ملزم باحترامه: بصمة محسوبة سلفاً فلا تجزئة
        // ثانية. والحاجز مفروض بالطول في ZatcaKeyCustodian، لا بالتوثيق.
        SignatureMaterial material = await custodian.SignAsync(new SigningInput(
            context.IssuingUnit,
            context.Credential,
            preparation.SignedInfoDigest,
            SigningInputForm.PrecomputedDigestSignDirectly,
            ZatcaProfile.CustodianHashAlgorithm,
            ZatcaProfile.CustodianSignatureAlgorithm), ct);

        XadesSignatureParts parts = XadesSignatureWriter.Complete(preparation, material.Signature);

        LastSignature = parts;

        InjectQrCode(tree, document.SigningInputDigest, parts, certificateDer);

        byte[] sealedBytes = _canonicaliser.Canonicalise(tree);

        // ── الحارس: حقن التوقيع ورمز QR لا يجوز أن يُحرّك بصمة الفاتورة ────────────
        byte[] recomputed = renderer.RecomputeInvoiceDigest(sealedBytes);
        if (!recomputed.AsSpan().SequenceEqual(document.SigningInputDigest.Span))
        {
            throw new ZatcaSigningException("بصمة الفاتورة تغيّرت بعد حقن التوقيع ورمز QR: " +
                FormattableString.Invariant($"وُقِّع على {Convert.ToHexString(document.SigningInputDigest.Span)} ") +
                FormattableString.Invariant($"والمحسوب من البايتات المختومة {Convert.ToHexString(recomputed)}. ") +
                "أي أن قاعدة الاستبعاد لا تستبعد ما حُقن — والمستند سيُرفض عند الجهة " +
                "بينما يتحقّق عندنا. / the invoice digest moved after injecting the signature and QR.");
        }

        return new SealedPayload(
            SealState.SealedLocally,
            sealedBytes,
            material,
            ZatcaDigests.Sha256(sealedBytes));
    }

    private void InjectQrCode(
        XElement tree, ReadOnlyMemory<byte> invoiceDigest, XadesSignatureParts parts, ReadOnlyMemory<byte> certificateDer)
    {
        XElement slot = tree.Descendants(ZatcaProfile.Cac + "AdditionalDocumentReference")
            .FirstOrDefault(reference => string.Equals(
                reference.Element(ZatcaProfile.Cbc + "ID")?.Value,
                ChainCarryingReferences.QrReferenceId,
                StringComparison.Ordinal))
            ?.Element(ZatcaProfile.Cac + "Attachment")
            ?.Element(ZatcaProfile.Cbc + "EmbeddedDocumentBinaryObject")
            ?? throw new ZatcaQrException(
                "لا موضع لرمز QR في المستند. الموضع يُبنى فارغاً ومستبعَداً من البصمة؛ " +
                "غيابه يعني أن الرمز سيُلحق في موضع يدخل البصمة. / no QR slot in the document.");

        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(certificateDer.Span);

        string typeName = tree.Element(ZatcaProfile.Cbc + "InvoiceTypeCode")?.Attribute("name")?.Value ?? string.Empty;
        bool isSimplified = typeName.StartsWith("02", StringComparison.Ordinal);

        // البيانات تُقرأ من الشجرة نفسها لا من المستند المجالي: الشجرة هي ما وُقِّع عليه،
        // وقراءة القيم من مصدر ثانٍ تفتح باب رمز يحمل مبلغاً غير المبلغ الموقَّع.
        slot.Value = ZatcaQr.Phase2(
            sellerNameAr: Read(tree, ZatcaProfile.Cac + "AccountingSupplierParty", "RegistrationName"),
            sellerVatNumber: Read(tree, ZatcaProfile.Cac + "AccountingSupplierParty", "CompanyID"),
            issuedAt: ReadIssuedAt(tree),
            grossTotal: ReadAmount(tree, "TaxInclusiveAmount"),
            taxTotal: ReadTaxTotal(tree),
            invoiceHashBase64: ZatcaDocumentRenderer.InvoiceHashBase64(invoiceDigest.Span),
            signatureBase64: Convert.ToBase64String(parts.SignatureValue),
            publicKeyDer: certificate.PublicKey.ExportSubjectPublicKeyInfo(),
            certificateSignature: ExtractCertificateSignature(certificateDer.Span),
            isSimplified: isSimplified,
            forms: _qrForms);
    }

    private static string Read(XElement tree, XName party, string field) =>
        tree.Element(party)?.Descendants(ZatcaProfile.Cbc + field).FirstOrDefault()?.Value ?? string.Empty;

    private static DateTimeOffset ReadIssuedAt(XElement tree)
    {
        string date = tree.Element(ZatcaProfile.Cbc + "IssueDate")?.Value ?? string.Empty;
        string time = tree.Element(ZatcaProfile.Cbc + "IssueTime")?.Value ?? "00:00:00";
        return DateTimeOffset.ParseExact(
            date + "T" + time + "Z", "yyyy-MM-ddTHH:mm:ssZ",
            CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
    }

    private static decimal ReadAmount(XElement tree, string field) =>
        decimal.Parse(
            tree.Element(ZatcaProfile.Cac + "LegalMonetaryTotal")?.Element(ZatcaProfile.Cbc + field)?.Value ?? "0",
            CultureInfo.InvariantCulture);

    private static decimal ReadTaxTotal(XElement tree) =>
        decimal.Parse(
            tree.Elements(ZatcaProfile.Cac + "TaxTotal").First().Element(ZatcaProfile.Cbc + "TaxAmount")?.Value ?? "0",
            CultureInfo.InvariantCulture);

    /// <summary>
    /// توقيع الشهادة نفسها — أي توقيع جهة الإصدار عليها.
    /// المنصّة لا تكشفه على <see cref="X509Certificate2"/>، فيُقرأ من ترميز DER مباشرةً:
    /// ‏<c>Certificate ::= SEQUENCE { tbsCertificate, signatureAlgorithm, signatureValue BIT STRING }</c>.
    /// </summary>
    [Provisional("هل يحمل الوسم التاسع توقيع الشهادة الخام أم تمثيلاً آخر له",
        DerivedFrom = "قراءة مواصفة رمز QR — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة رمز QR المنشورة ومثالها المرجعي")]
    public static byte[] ExtractCertificateSignature(ReadOnlySpan<byte> certificateDer)
    {
        AsnReader outer = new(certificateDer.ToArray(), AsnEncodingRules.DER);
        AsnReader certificate = outer.ReadSequence();
        certificate.ReadEncodedValue();   // tbsCertificate
        certificate.ReadEncodedValue();   // signatureAlgorithm
        return certificate.ReadBitString(out _);
    }
}
