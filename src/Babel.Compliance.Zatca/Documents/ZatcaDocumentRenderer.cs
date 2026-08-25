using System.Globalization;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;
using Babel.Compliance.Zatca.Canonicalization;

namespace Babel.Compliance.Zatca.Documents;

/// <summary>
/// يبني التمثيل الذي تفهمه الهيئة، ويشتقّ منه <b>مرة واحدة</b> البايتات المقصودة بالتوقيع.
/// <para/>
/// <b>قرار صريح يجب أن يُقرأ قبل أي تعديل هنا:</b> في هذا المزوّد
/// <c>DomainChainDigest</c> يساوي <c>SigningInputDigest</c> — أي أن <b>سلسلتنا هي سلسلة
/// الهيئة نفسها</b>، لا سلسلة ثانية بجوارها.
/// <para/>
/// وعقد <c>RenderedDocument</c> يحذّر من الخلط بين الاثنين، والتحذير في محلّه <b>لمزوّد
/// تُعرَّف سلسلته على بايتات غير البايتات الموقَّعة</b>. أما هنا فالهيئة تُعرِّف السلسلة
/// على <b>نفس</b> البايتات الموقَّعة (‏PIH هو بصمة الفاتورة السابقة بعد التحويل والتوحيد
/// القياسي). فلو أمسكنا سلسلة ثانية مستقلة لحصلنا على ما يحذّر منه السجل بالضبط، مقلوباً:
/// <b>سلسلة تتحقّق عندنا وليست السلسلة التي تفحصها الجهة.</b>
/// <para/>
/// <b>وما نخسره بهذا القرار مذكور صراحةً</b>: بصمتنا المجالية كانت تغطّي حقولاً لا تدخل
/// المستند (‏مرجع القيد المحاسبي، والمستأجر، والمسار). هذه الحقول تخرج من تغطية السلسلة
/// وتبقى في الصفّ وحده، ومطابقتها مسؤولية <c>Reconciler</c> لا السلسلة. البند مُسجَّل في
/// <c>docs/evidence/verification-debt.md</c>.
/// </summary>
public sealed class ZatcaDocumentRenderer : IDocumentRenderer
{
    private readonly UblInvoiceWriter _writer;
    private readonly ZatcaSigningTransform _transform;
    private readonly IZatcaXmlCanonicaliser _canonicaliser;

    public ZatcaDocumentRenderer(ZatcaSellerIdentity seller, IZatcaXmlCanonicaliser? canonicaliser = null)
    {
        _canonicaliser = canonicaliser ?? new ZatcaCanonicalXml();
        _writer = new UblInvoiceWriter(seller);
        _transform = new ZatcaSigningTransform(_canonicaliser);
    }

    /// <summary>الأعلام الخمسة على السمة <c>name</c>. إعداد للمستأجر، لا استنتاج من البيانات.</summary>
    public InvoiceTraits Flags { get; init; } = InvoiceTraits.None;

    public ZatcaSigningTransform Transform => _transform;

    public IZatcaXmlCanonicaliser Canonicaliser => _canonicaliser;

    /// <summary>آخر شجرة بُنيت — يستعملها الخاتم كي لا يُعاد تحليل النصّ.</summary>
    public RenderedDocument Render(ComplianceDocument document, ChainSlot chain)
    {
        ArgumentNullException.ThrowIfNull(document);

        XElement tree = _writer.Build(document, chain, Flags);

        // الجسم: المستند كاملاً كما بُني، بمواضع التوقيع وQR فارغة.
        byte[] body = _canonicaliser.Canonicalise(tree);

        // بايتات التوقيع: بعد استبعاد المجموعات الثلاث. تُشتقّ مرة واحدة وتُجمَّد.
        SigningTransformResult transformed = _transform.Apply(tree);
        byte[] invoiceDigest = ZatcaDigests.Sha256(transformed.Canonical);

        return new RenderedDocument(
            document.DocumentId,
            ZatcaProfile.ProfileId,
            body,
            "application/xml",
            transformed.Canonical,
            invoiceDigest,
            // السلسلة هي سلسلة الهيئة — انظر تعليق النوع أعلاه.
            invoiceDigest,
            chain);
    }

    /// <summary>
    /// إعادة بناء الشجرة من بايتات محفوظة. <b>لا يُعاد توليد مستند من بياناته أبداً</b> —
    /// إعادة التوليد بعد الإرسال هي العطل الإنتاجي المهيمن في المنظومة التركية المقارنة،
    /// ويظهر بعد سنوات عند التفتيش
    /// (‏<c>docs/evidence/traps.md#fakh-regenerating-a-sealed-artefact</c>).
    /// هذه الدالة تقرأ البايتات المخزَّنة، ولا تبني شيئاً من جديد.
    /// </summary>
    public static XElement Parse(ReadOnlySpan<byte> xml)
    {
        using MemoryStream stream = new(xml.ToArray(), writable: false);
        return XElement.Load(stream, LoadOptions.PreserveWhitespace);
    }

    /// <summary>
    /// يُعيد حساب بصمة الفاتورة من بايتات <b>مختومة</b>، للتحقق من أن حقن التوقيع ورمز QR
    /// لم يُحرّك البصمة. <b>هذا هو الاختبار الذي يُثبت أن قاعدة الاستبعاد تعمل فعلاً</b>،
    /// لا مجرّد أنها كُتبت.
    /// </summary>
    public byte[] RecomputeInvoiceDigest(ReadOnlySpan<byte> sealedXml) =>
        ZatcaDigests.Sha256(_transform.Apply(Parse(sealedXml)).Canonical);

    /// <summary>نصّ البصمة كما يُكتب في المستند وفي رمز QR وفي جسم الإرسال.</summary>
    public static string InvoiceHashBase64(ReadOnlySpan<byte> digest) =>
        ZatcaDigests.Render(digest, DigestEncoding.RawDigestBase64);

    /// <summary>تمثيل قابل للقراءة في رسالة عطل. لا يدخل أي بايت مُجزَّأ.</summary>
    public static string Describe(ChainSlot slot) => string.Create(
        CultureInfo.InvariantCulture,
        $"ICV={slot.Counter} PIH={Chain.ZatcaChain.PreviousInvoiceHash(slot)}");
}
