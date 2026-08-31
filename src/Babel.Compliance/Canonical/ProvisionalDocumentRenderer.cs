using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Canonical;

/// <summary>
/// <b>مُولِّد مستند مؤقَّت.</b> يبني شكلاً شبيهاً بـUBL يكفي لتشغيل خط الأنابيب كاملاً
/// ولإثبات ثلاثة أشياء فقط:
/// <list type="number">
///   <item>أن العدّاد والبصمة السابقة يركبان <b>داخل جسم المستند</b> كمراجع مستند إضافية.</item>
///   <item>أن قاعدة الاستبعاد تُزيل <b>ثلاث مجموعات بالضبط</b> ولا تلمس العدّاد ولا البصمة.</item>
///   <item>أن البايتات المقصودة بالتوقيع تُشتقّ مرة واحدة وتُجمَّد.</item>
/// </list>
/// <para/>
/// <b>أسماء العناصر ومساحات الأسماء وترتيبها كلها مؤقَّتة.</b> توليد UBL 2.1 حقيقي
/// بند «اشترِ ولا تكتب»، وهو حسّاس لترتيب العناصر ولتسميات المساحات، ويُتحقَّق منه
/// مقابل المخطط الرسمي قبل أي إرسال حقيقي.
/// </summary>
[Provisional("بنية مستند UBL كاملة: أسماء العناصر، مساحات الأسماء، ترتيب العناصر، الحقول الإلزامية، تنسيق التاريخ، ورمز QR بترميز TLV",
    DerivedFrom = "لا مصدر رسمي — شكل مؤقَّت لتشغيل خط الأنابيب",
    Risk = ProvisionalRisk.Structural,
    VerifyBy = "مواصفة الفاتورة الإلكترونية والمخطط (XSD) وقوائم التحقق المنشورة، ثم توليد UBL بمكتبة ناضجة")]
public sealed class ProvisionalDocumentRenderer(
    IXmlCanonicaliser? canonicaliser = null,
    SigningExclusionRule? exclusionRule = null) : IDocumentRenderer
{
    private readonly IXmlCanonicaliser _canonicaliser = canonicaliser ?? new DeterministicXmlSerialiser();
    private readonly SigningInputExtractor _extractor =
        new(canonicaliser ?? new DeterministicXmlSerialiser(), exclusionRule);

    public const string ProvisionalProfileId = "babel.provisional.profile.v1";

    public SigningInputExtractor Extractor => _extractor;

    public RenderedDocument Render(ComplianceDocument document, ChainSlot chain)
    {
        var xml = BuildElement(document, chain);
        var body = _canonicaliser.Canonicalise(xml);

        var signingInput = _extractor.Extract(xml);
        var signingInputDigest = SHA256.HashData(signingInput);

        // بصمتنا نحن على الحقيقة المجالية — مسار مستقل تماماً عن مسار التوقيع.
        var domainDigest = ComplianceCanonical.Hash(document, chain);

        return new RenderedDocument(
            document.DocumentId,
            ProvisionalProfileId,
            body,
            "application/xml",
            signingInput,
            signingInputDigest,
            domainDigest,
            chain);
    }

    private XElement BuildElement(ComplianceDocument d, ChainSlot chain)
    {
        var root = new XElement("Invoice",
            new XElement("ProfileID", ProvisionalProfileId),
            new XElement("ID", d.DocumentNumber),
            new XElement("UUID", d.DocumentUuid.ToString("D", CultureInfo.InvariantCulture)),
            new XElement("IssueDate", d.IssuedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement("IssueTime", d.IssuedAt.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            new XElement("InvoiceTypeCode", d.Kind.ToString()),
            new XElement("DocumentCurrencyCode", d.CurrencyCode));

        // (1) امتدادات UBL — مجموعة مستبعدة رقم 1. هنا يسكن التوقيع المضمَّن لاحقاً.
        root.AddFirst(new XElement("UBLExtensions", new XElement("UBLExtension", new XElement("ExtensionContent", ""))));

        // (2) عدّاد المستند — مرجع مستند إضافي، و**ليس** مستبعداً: يدخل البايتات الموقَّعة.
        root.Add(new XElement("AdditionalDocumentReference",
            new XElement("ID", ChainCarryingReferences.CounterReferenceId),
            new XElement("UUID", chain.Counter.ToString(CultureInfo.InvariantCulture))));

        // (3) بصمة المستند السابق — مرجع مستند إضافي، و**ليس** مستبعداً.
        root.Add(new XElement("AdditionalDocumentReference",
            new XElement("ID", ChainCarryingReferences.PreviousHashReferenceId),
            new XElement("Attachment",
                new XElement("EmbeddedDocumentBinaryObject",
                    Convert.ToBase64String(chain.PreviousHash.Span)))));

        // (4) رمز QR — مرجع مستند إضافي **مستبعد** (المجموعة الثالثة).
        root.Add(new XElement("AdditionalDocumentReference",
            new XElement("ID", ChainCarryingReferences.QrReferenceId),
            new XElement("Attachment",
                new XElement("EmbeddedDocumentBinaryObject", BuildProvisionalQr(d)))));

        root.Add(Party("AccountingSupplierParty", d.Seller));
        if (d.Buyer is not null) root.Add(Party("AccountingCustomerParty", d.Buyer));

        if (d.OriginalDocument is { } original)
            root.Add(new XElement("BillingReference",
                new XElement("InvoiceDocumentReference",
                    new XElement("ID", original.Value.ToString("D", CultureInfo.InvariantCulture)),
                    new XElement("DocumentDescription", d.CorrectionReasonAr ?? ""))));

        foreach (var l in d.Lines.OrderBy(x => x.LineNo))
        {
            root.Add(new XElement("InvoiceLine",
                new XElement("ID", l.LineNo.ToString(CultureInfo.InvariantCulture)),
                new XElement("InvoicedQuantity", ComplianceCanonical.Money(l.Quantity)),
                new XElement("LineExtensionAmount", ComplianceCanonical.Money(l.NetAmount)),
                new XElement("TaxTotal",
                    new XElement("TaxAmount", ComplianceCanonical.Money(l.TaxAmount)),
                    new XElement("RoundingAmount", ComplianceCanonical.Money(l.GrossAmount))),
                new XElement("Item",
                    new XElement("Name", l.DescriptionAr),
                    new XElement("NameEn", l.DescriptionEn)),
                new XElement("Price", new XElement("PriceAmount", ComplianceCanonical.Money(l.UnitPrice)))));
        }

        root.Add(new XElement("TaxTotal", new XElement("TaxAmount", ComplianceCanonical.Money(d.Totals.TaxTotal))));
        root.Add(new XElement("LegalMonetaryTotal",
            new XElement("LineExtensionAmount", ComplianceCanonical.Money(d.Totals.NetTotal)),
            new XElement("TaxExclusiveAmount", ComplianceCanonical.Money(d.Totals.NetTotal)),
            new XElement("TaxInclusiveAmount", ComplianceCanonical.Money(d.Totals.GrossTotal)),
            new XElement("PayableAmount", ComplianceCanonical.Money(d.Totals.GrossTotal))));

        // (5) عنصر التوقيع — مجموعة مستبعدة رقم 2.
        root.Add(new XElement("Signature",
            new XElement("ID", "babel-seal"),
            new XElement("SignatureMethod", _canonicaliser.AlgorithmName)));

        return root;
    }

    private static XElement Party(string wrapper, PartyRef p) =>
        new(wrapper,
            new XElement("Party",
                new XElement("PartyIdentification", new XElement("ID", p.TaxRegistrationNumber ?? "")),
                new XElement("PartyName", new XElement("Name", p.NameAr), new XElement("NameEn", p.NameEn)),
                new XElement("PostalAddress",
                    new XElement("StreetName", p.AddressAr ?? ""),
                    new XElement("StreetNameEn", p.AddressEn ?? ""))));

    /// <summary>
    /// رمز QR بترميز TLV. <b>ترتيب الوسوم وعددها ومحتوى كل وسم غير مُتحقَّق منه.</b>
    /// موجود هنا كي يوجد عنصر QR حقيقي تستبعده قاعدة الاستبعاد، لا كتنفيذ للمواصفة.
    /// </summary>
    [Provisional("ترميز TLV لرمز QR: أرقام الوسوم وترتيبها والحقول المطلوبة في كل مرحلة",
        DerivedFrom = "docs/analysis/04-zatca-integration.md §5 — وثيقة تخطيط داخلية",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "مواصفة رمز QR المنشورة، ثم التحقق بأداة تحقق مستقلة")]
    private static string BuildProvisionalQr(ComplianceDocument d)
    {
        var buffer = new List<byte>();
        void Tlv(byte tag, string value)
        {
            var bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length > 255) bytes = bytes[..255];
            buffer.Add(tag);
            buffer.Add((byte)bytes.Length);
            buffer.AddRange(bytes);
        }

        Tlv(1, d.Seller.NameAr);
        Tlv(2, d.Seller.TaxRegistrationNumber ?? "");
        Tlv(3, ComplianceCanonical.Instant(d.IssuedAt));
        Tlv(4, ComplianceCanonical.Money(d.Totals.GrossTotal));
        Tlv(5, ComplianceCanonical.Money(d.Totals.TaxTotal));
        return Convert.ToBase64String(buffer.ToArray());
    }
}
