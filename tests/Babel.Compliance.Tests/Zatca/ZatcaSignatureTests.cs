using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca;
using Babel.Compliance.Zatca.Canonicalization;
using Babel.Compliance.Zatca.Documents;
using Babel.Compliance.Zatca.Signing;
using Xunit;

namespace Babel.Compliance.Tests.Zatca;

/// <summary>
/// التوقيع: بنيته، وما يغطّيه، والحاجز الذي يمنع التجزئة المزدوجة.
/// <para/>
/// <b>ولا مفتاح ولا شهادة في المستودع.</b> كل مفتاح هنا يُولَّد عند تشغيل الاختبار
/// ويموت مع العملية، ولذلك <b>لا يدخل التوقيع المتجهات الذهبية</b> — يُتحقَّق منه
/// تشفيرياً بدلاً من ذلك، وهو إثبات أقوى لا أضعف.
/// </summary>
public sealed class ZatcaSignatureTests(ITestOutputHelper output)
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private sealed record Sealed(
        SealedPayload Payload,
        XadesSignatureParts Parts,
        byte[] CertificateDer,
        ZatcaDocumentRenderer Renderer,
        RenderedDocument Rendered);

    private static async Task<Sealed> SealAsync(ComplianceDocument? document = null, ChainSlot? slot = null)
    {
        using EphemeralZatcaKeyStore keys = new();
        ZatcaKeyCustodian custodian = new(keys);
        ZatcaDocumentRenderer renderer = new(ZatcaFixtures.Seller);
        FixedClock clock = new(ZatcaFixtures.IssuedAt);

        CredentialRef credential = keys.Create(
            ZatcaFixtures.Tenant, ZatcaFixtures.Unit, ComplianceEnvironment.Simulation);
        byte[] certificate = keys.IssueSelfSignedForTesting(
            credential, "babel-zatca-test", TimeSpan.FromDays(1), ZatcaFixtures.IssuedAt);

        ZatcaSealer sealer = new(custodian, renderer, clock);
        RenderedDocument rendered = renderer.Render(
            document ?? ZatcaFixtures.Standard(), slot ?? ZatcaFixtures.Slot(2));

        SealedPayload payload = await sealer.SealAsync(
            new SealingContext(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, credential, ComplianceEnvironment.Simulation),
            rendered,
            TestContext.Current.CancellationToken);

        return new Sealed(payload, sealer.LastSignature!, certificate, renderer, rendered);
    }

    // ── ما يغطّيه التوقيع ───────────────────────────────────────────────────

    [Fact]
    public async Task The_signature_is_over_the_canonical_SignedInfo_not_over_the_invoice_digest()
    {
        Sealed result = await SealAsync();

        using X509Certificate2 certificate = X509CertificateLoader.LoadCertificate(result.CertificateDer);
        using ECDsa publicKey = certificate.GetECDsaPublicKey()!;

        bool overSignedInfo = publicKey.VerifyHash(
            result.Parts.SignedInfoDigest, result.Parts.SignatureValue, DSASignatureFormat.Rfc3279DerSequence);

        bool overInvoice = publicKey.VerifyHash(
            result.Rendered.SigningInputDigest.Span, result.Parts.SignatureValue, DSASignatureFormat.Rfc3279DerSequence);

        output.WriteLine("التوقيع يتحقّق فوق بصمة SignedInfo: " + overSignedInfo.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("التوقيع يتحقّق فوق بصمة الفاتورة  : " + overInvoice.ToString(CultureInfo.InvariantCulture));

        Assert.True(overSignedInfo, "التوقيع لا يتحقّق فوق بصمة SignedInfo");
        Assert.False(overInvoice,
            "التوقيع يتحقّق فوق بصمة الفاتورة مباشرةً: أي أنه لا يغطّي وقت التوقيع ولا هوية الشهادة");
    }

    [Fact]
    public async Task The_signed_properties_carry_the_signing_time_and_the_certificate_identity()
    {
        Sealed result = await SealAsync();
        string properties = Utf8.GetString(result.Parts.SignedPropertiesCanonical);

        output.WriteLine(properties);

        Assert.Contains("SigningTime", properties, StringComparison.Ordinal);
        Assert.Contains(XadesSignatureWriter.SigningTimestamp(ZatcaFixtures.IssuedAt), properties, StringComparison.Ordinal);
        Assert.Contains("CertDigest", properties, StringComparison.Ordinal);
        Assert.Contains("X509IssuerName", properties, StringComparison.Ordinal);
        Assert.Contains("X509SerialNumber", properties, StringComparison.Ordinal);

        // الرقم التسلسلي عدد عشري لا نصّ ستّ‌عشري: العنصر معرَّف عدداً في مخطّط XMLDSig.
        XElement parsed = XElement.Parse(properties);
        string serial = parsed.Descendants(ZatcaProfile.Ds + "X509SerialNumber").Single().Value;
        Assert.True(System.Numerics.BigInteger.TryParse(serial, CultureInfo.InvariantCulture, out _),
            $"الرقم التسلسلي «{serial}» ليس عدداً عشرياً");
    }

    /// <summary>
    /// <b>أهم حارس في مسار الختم:</b> حقن التوقيع ورمز QR لا يجوز أن يُحرّك بصمة الفاتورة.
    /// إن حرّكها فقاعدة الاستبعاد لا تعمل، والمستند يتحقّق عندنا ويُرفض عند الجهة.
    /// </summary>
    [Fact]
    public async Task Injecting_the_signature_and_the_QR_does_not_move_the_invoice_digest()
    {
        Sealed result = await SealAsync();

        byte[] before = result.Rendered.SigningInputDigest.ToArray();
        byte[] after = result.Renderer.RecomputeInvoiceDigest(result.Payload.Bytes.Span);

        output.WriteLine("قبل الختم: " + Convert.ToHexString(before));
        output.WriteLine("بعد الختم: " + Convert.ToHexString(after));

        Assert.True(before.AsSpan().SequenceEqual(after));

        // وحضور التوقيع والرمز فعلاً في البايتات المختومة — وإلّا كان التطابق تافهاً.
        string sealedXml = Utf8.GetString(result.Payload.Bytes.Span);
        Assert.Contains("SignatureValue", sealedXml, StringComparison.Ordinal);
        Assert.Contains("X509Certificate", sealedXml, StringComparison.Ordinal);
        Assert.Contains("xadesSignedProperties", sealedXml, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>إثبات لافراغ الحارس السابق:</b> يُحقن التوقيع في موضع <b>غير مستبعَد</b> فتتحرّك
    /// البصمة فعلاً. لولا هذا لكان «لم تتحرّك» عبارة لا تُميّز حارساً يعمل من حارس ميت.
    /// </summary>
    [Fact]
    public async Task Injecting_into_a_non_excluded_place_really_does_move_the_digest()
    {
        Sealed result = await SealAsync();

        XElement tree = ZatcaDocumentRenderer.Parse(result.Payload.Bytes.Span);

        // مخالفة حقيقية: عنصر جديد في موضع لا تستبعده القاعدة.
        tree.Add(new XElement(ZatcaProfile.Cbc + "Note", "تعليق أُضيف بعد الختم"));

        byte[] moved = result.Renderer.RecomputeInvoiceDigest(
            new ZatcaCanonicalXml().Canonicalise(tree));

        output.WriteLine("الأصلية: " + Convert.ToHexString(result.Rendered.SigningInputDigest.Span));
        output.WriteLine("بعد حقن عنصر غير مستبعَد: " + Convert.ToHexString(moved));

        Assert.False(result.Rendered.SigningInputDigest.Span.SequenceEqual(moved),
            "حقن عنصر غير مستبعَد لم يُحرّك البصمة: الحارس لا يقيس شيئاً");
    }

    // ── الحاجز ضدّ التجزئة المزدوجة ─────────────────────────────────────────

    /// <summary>
    /// <b>الفخّ:</b> تمرير <b>نصّ</b> البصمة بترميز base64 إلى موقِّع يُجزّئ من جديد.
    /// النتيجة توقيع على بصمة البصمة: يتحقّق محلياً بنجاح تام ويُرفض عند الجهة.
    /// <para/>
    /// والحاجز هنا <b>بالطول</b>: بصمة SHA-256 اثنان وثلاثون بايتاً، ونصّها 44.
    /// </summary>
    [Fact]
    public async Task The_signer_refuses_a_base64_digest_string_because_its_length_gives_it_away()
    {
        using EphemeralZatcaKeyStore keys = new();
        ZatcaKeyCustodian custodian = new(keys);
        CredentialRef credential = keys.Create(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, ComplianceEnvironment.Simulation);
        keys.IssueSelfSignedForTesting(credential, "babel-zatca-test", TimeSpan.FromDays(1), ZatcaFixtures.IssuedAt);

        byte[] digest = SHA256.HashData(Utf8.GetBytes("فاتورة"));
        byte[] digestAsText = Utf8.GetBytes(Convert.ToBase64String(digest));

        output.WriteLine("طول البصمة الخام: " + digest.Length.ToString(CultureInfo.InvariantCulture));
        output.WriteLine("طول نصّها المُرمَّز: " + digestAsText.Length.ToString(CultureInfo.InvariantCulture));

        ZatcaSigningException error = await Assert.ThrowsAsync<ZatcaSigningException>(async () =>
            await custodian.SignAsync(new SigningInput(
                ZatcaFixtures.Unit, credential, digestAsText,
                SigningInputForm.PrecomputedDigestSignDirectly,
                ZatcaProfile.CustodianHashAlgorithm,
                ZatcaProfile.CustodianSignatureAlgorithm), TestContext.Current.CancellationToken));

        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("44", error.Message, StringComparison.Ordinal);

        // والبصمة الخام تمرّ: الحاجز يمنع الخطأ لا كل شيء.
        SignatureMaterial material = await custodian.SignAsync(new SigningInput(
            ZatcaFixtures.Unit, credential, digest,
            SigningInputForm.PrecomputedDigestSignDirectly,
            ZatcaProfile.CustodianHashAlgorithm,
            ZatcaProfile.CustodianSignatureAlgorithm), TestContext.Current.CancellationToken);

        Assert.False(material.Signature.IsEmpty);
    }

    [Fact]
    public async Task The_signer_refuses_an_input_form_it_does_not_understand()
    {
        using EphemeralZatcaKeyStore keys = new();
        ZatcaKeyCustodian custodian = new(keys);
        CredentialRef credential = keys.Create(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, ComplianceEnvironment.Simulation);

        await Assert.ThrowsAsync<NotSupportedException>(async () =>
            await custodian.SignAsync(new SigningInput(
                ZatcaFixtures.Unit, credential, Utf8.GetBytes("بايتات خام"),
                SigningInputForm.RawBytesToHashThenSign,
                ZatcaProfile.CustodianHashAlgorithm,
                ZatcaProfile.CustodianSignatureAlgorithm), TestContext.Current.CancellationToken));
    }

    /// <summary>
    /// <b>الحدّ الذي كان ناقصاً في العقد:</b> بلا قراءة للشهادة يستحيل بناء الخصائص
    /// الموقَّعة، لأن بصمة الشهادة ومُصدِرها ورقمها التسلسلي تدخل البايتات الموقَّعة.
    /// </summary>
    [Fact]
    public async Task Sealing_without_a_certificate_fails_loudly_because_XAdES_needs_it_before_signing()
    {
        using EphemeralZatcaKeyStore keys = new();
        ZatcaKeyCustodian custodian = new(keys);
        ZatcaDocumentRenderer renderer = new(ZatcaFixtures.Seller);
        ZatcaSealer sealer = new(custodian, renderer, new FixedClock(ZatcaFixtures.IssuedAt));

        // مفتاح بلا شهادة: الحالة التي تقع بين إنشاء المفتاح ومنح الشهادة.
        CredentialRef credential = keys.Create(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, ComplianceEnvironment.Simulation);

        ZatcaSigningException error = await Assert.ThrowsAsync<ZatcaSigningException>(async () =>
            await sealer.SealAsync(
                new SealingContext(ZatcaFixtures.Tenant, ZatcaFixtures.Unit, credential, ComplianceEnvironment.Simulation),
                renderer.Render(ZatcaFixtures.Standard(), ZatcaFixtures.Slot(2)),
                TestContext.Current.CancellationToken));

        output.WriteLine("رُفض: " + error.Message);
        Assert.Contains("قبل التوقيع", error.Message, StringComparison.Ordinal);
    }

    // ── استقرار البايتات ────────────────────────────────────────────────────

    /// <summary>
    /// <b>خاصية بنيوية لا وعد:</b> البايتات تُجمَّد عند أول ختم. وإعادة الختم تُنتج
    /// توقيعاً آخر لأن ECDSA عشوائي — <b>وهذا بالضبط سبب وجوب عدم إعادة الختم</b>،
    /// ولذلك يُخزَّن المصنوع ولا يُعاد توليده.
    /// </summary>
    [Fact]
    public async Task Two_seals_of_the_same_document_differ_which_is_why_the_bytes_are_frozen_once()
    {
        Sealed first = await SealAsync();
        Sealed second = await SealAsync();

        Assert.False(first.Payload.Bytes.Span.SequenceEqual(second.Payload.Bytes.Span),
            "ختمان متتاليان أعطيا البايتات نفسها: توقيع ECDSA حتمي؟ راجع مصدر العشوائية");

        // والبصمة قبل الختم واحدة: المستند نفسه، والتوقيع وحده هو ما اختلف.
        Assert.True(first.Rendered.SigningInputDigest.Span.SequenceEqual(second.Rendered.SigningInputDigest.Span));

        output.WriteLine("بصمة الفاتورة متطابقة، وبايتات الختم مختلفة — ولذلك تُجمَّد مرة واحدة وتُخزَّن.");
        Assert.Equal(SealState.SealedLocally, first.Payload.State);
        Assert.True(first.Payload.IsByteStableAcrossRetries);
    }

    private sealed class FixedClock(DateTimeOffset at) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => at;
    }
}
