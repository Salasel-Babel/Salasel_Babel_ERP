using System.Globalization;
using System.Xml.Linq;
using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Zatca.Documents;

/// <summary>
/// بانٍ مستند UBL 2.1 بالشكل الذي تطلبه الهيئة.
/// <para/>
/// <b>العربية هي المحتوى، لا الترجمة.</b> اسم الطرف الذي يدخل <c>cbc:RegistrationName</c>
/// هو <b>الاسم العربي</b>، ووصف السطر الذي يدخل <c>cbc:Name</c> هو <b>الوصف العربي</b>.
/// وبنية UBL هنا تحمل حقل اسم واحداً لكل طرف، فالاسم الإنجليزي المُلزَم في بيانات
/// النظام (‏<c>CONTRIBUTING §3</c> بند 5) يبقى <b>في بياناتنا</b> ولا يدخل هذا المستند —
/// وهذا قرار مُعلَن هنا لا إغفال.
/// <para/>
/// <b>موضعان يُتركان فارغين عمداً</b>، ويُملآن بعد حساب البصمة:
/// <list type="number">
///   <item><c>ext:UBLExtensions</c> — يسكنه التوقيع.</item>
///   <item>مرجع المستند الإضافي <c>QR</c> — يسكنه رمز الاستجابة السريعة.</item>
/// </list>
/// وكلاهما <b>مستبعَد</b> من بايتات التوقيع، فملؤهما بعد التوقيع لا يغيّر البصمة —
/// وهذه الخاصية بالذات مفحوصة بعد الختم لا مفترضة.
/// </summary>
public sealed class UblInvoiceWriter(ZatcaSellerIdentity seller)
{
    private static readonly XNamespace Inv = ZatcaProfile.Invoice;
    private static readonly XNamespace Cac = ZatcaProfile.Cac;
    private static readonly XNamespace Cbc = ZatcaProfile.Cbc;
    private static readonly XNamespace Ext = ZatcaProfile.Ext;

    /// <summary>
    /// يبني الشجرة كاملةً بمواضع التوقيع وQR فارغة.
    /// </summary>
    public XElement Build(ComplianceDocument document, ChainSlot chain, InvoiceTraits flags = InvoiceTraits.None)
    {
        ArgumentNullException.ThrowIfNull(document);

        string currency = document.CurrencyCode;

        XElement root = new(Inv + "Invoice",
            new XAttribute(XNamespace.Xmlns + "cac", Cac.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "cbc", Cbc.NamespaceName),
            new XAttribute(XNamespace.Xmlns + "ext", Ext.NamespaceName));

        // (1) موضع التوقيع — فارغ الآن، ومستبعَد من البصمة.
        root.Add(new XElement(Ext + "UBLExtensions"));

        root.Add(
            new XElement(Cbc + "ProfileID", ZatcaProfile.ProfileId),
            new XElement(Cbc + "ID", document.DocumentNumber),
            new XElement(Cbc + "UUID", document.DocumentUuid.ToString("D", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "IssueDate", document.IssuedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "IssueTime", document.IssuedAt.UtcDateTime.ToString("HH:mm:ss", CultureInfo.InvariantCulture)),
            new XElement(Cbc + "InvoiceTypeCode",
                new XAttribute("name", ZatcaProfile.TypeNameOf(document.Flow, flags)),
                ZatcaProfile.TypeCodeOf(document.Kind)),
            new XElement(Cbc + "DocumentCurrencyCode", currency),
            new XElement(Cbc + "TaxCurrencyCode", currency));

        if (document.OriginalDocument is { } original)
        {
            root.Add(new XElement(Cac + "BillingReference",
                new XElement(Cac + "InvoiceDocumentReference",
                    new XElement(Cbc + "ID", original.Value.ToString("D", CultureInfo.InvariantCulture)))));
        }

        // (2) عدّاد المستند — **داخل** البايتات الموقَّعة.
        root.Add(new XElement(Cac + "AdditionalDocumentReference",
            new XElement(Cbc + "ID", ChainCarryingReferences.CounterReferenceId),
            new XElement(Cbc + "UUID", chain.Counter.ToString(CultureInfo.InvariantCulture))));

        // (3) بصمة المستند السابق — **داخل** البايتات الموقَّعة.
        root.Add(new XElement(Cac + "AdditionalDocumentReference",
            new XElement(Cbc + "ID", ChainCarryingReferences.PreviousHashReferenceId),
            new XElement(Cac + "Attachment",
                new XElement(Cbc + "EmbeddedDocumentBinaryObject",
                    new XAttribute("mimeCode", "text/plain"),
                    Chain.ZatcaChain.PreviousInvoiceHash(chain)))));

        // (4) موضع رمز QR — فارغ الآن، ومستبعَد من البصمة.
        root.Add(new XElement(Cac + "AdditionalDocumentReference",
            new XElement(Cbc + "ID", ChainCarryingReferences.QrReferenceId),
            new XElement(Cac + "Attachment",
                new XElement(Cbc + "EmbeddedDocumentBinaryObject",
                    new XAttribute("mimeCode", "text/plain"),
                    string.Empty))));

        // (5) عنصر التوقيع المرجعي — مستبعَد من البصمة أيضاً.
        root.Add(new XElement(Cac + "Signature",
            new XElement(Cbc + "ID", ZatcaProfile.SignatureId),
            new XElement(Cbc + "SignatureMethod", ZatcaProfile.SignatureMethod)));

        root.Add(SupplierParty(document.Seller));
        root.Add(CustomerParty(document.Buyer));

        root.Add(new XElement(Cac + "Delivery",
            new XElement(Cbc + "ActualDeliveryDate",
                document.IssuedAt.UtcDateTime.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))));

        XElement paymentMeans = new(Cac + "PaymentMeans",
            new XElement(Cbc + "PaymentMeansCode", seller.PaymentMeansCode));

        if (document.Kind != ComplianceDocumentKind.Invoice)
        {
            // سبب التصحيح إلزامي على الإشعارين، ويُكتب بالعربية لأنه نصّ يقرأه إنسان.
            paymentMeans.Add(new XElement(Cbc + "InstructionNote",
                document.CorrectionReasonAr
                ?? throw new ZatcaDocumentException(
                    "إشعار دائن أو مدين بلا سبب تصحيح بالعربية. السبب حقل يقرأه إنسان ولا يُولَّد. / "
                    + "a credit or debit note without an Arabic correction reason is refused.")));
        }

        root.Add(paymentMeans);

        root.Add(TaxTotalSummary(document, currency));
        root.Add(TaxTotalDetailed(document, currency));
        root.Add(LegalMonetaryTotal(document, currency));

        foreach (DocumentLine line in document.Lines.OrderBy(static l => l.LineNo))
        {
            root.Add(InvoiceLine(line, currency));
        }

        return root;
    }

    private XElement SupplierParty(PartyRef party) =>
        new(Cac + "AccountingSupplierParty", PartyElement(party, seller.CommercialRegistrationNumber, "CRN"));

    private XElement CustomerParty(PartyRef? party)
    {
        // المشتري غائب في الفاتورة المبسّطة — وغيابه بنية لا نقص بيانات.
        if (party is null)
        {
            return new XElement(Cac + "AccountingCustomerParty",
                new XElement(Cac + "Party",
                    new XElement(Cac + "PostalAddress",
                        new XElement(Cac + "Country", new XElement(Cbc + "IdentificationCode", seller.CountryCode)))));
        }

        return new XElement(Cac + "AccountingCustomerParty", PartyElement(party, null, null));
    }

    private XElement PartyElement(PartyRef party, string? schemeValue, string? schemeId)
    {
        XElement element = new(Cac + "Party");

        if (!string.IsNullOrWhiteSpace(schemeValue) && schemeId is not null)
        {
            element.Add(new XElement(Cac + "PartyIdentification",
                new XElement(Cbc + "ID", new XAttribute("schemeID", schemeId), schemeValue)));
        }

        element.Add(PostalAddress(party));

        if (!string.IsNullOrWhiteSpace(party.TaxRegistrationNumber))
        {
            element.Add(new XElement(Cac + "PartyTaxScheme",
                new XElement(Cbc + "CompanyID", party.TaxRegistrationNumber),
                new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT"))));
        }

        // الاسم العربي هو الاسم. لا يُترجَم ولا يُنقَل حرفياً.
        element.Add(new XElement(Cac + "PartyLegalEntity",
            new XElement(Cbc + "RegistrationName", party.NameAr)));

        return element;
    }

    private XElement PostalAddress(PartyRef party)
    {
        IReadOnlyDictionary<string, string> parts = party.AddressParts ?? new Dictionary<string, string>(StringComparer.Ordinal);

        string Part(string key, string fallback = "") =>
            parts.TryGetValue(key, out string? value) ? value : fallback;

        return new XElement(Cac + "PostalAddress",
            new XElement(Cbc + "StreetName", Part("StreetName", party.AddressAr ?? string.Empty)),
            new XElement(Cbc + "BuildingNumber", Part("BuildingNumber")),
            new XElement(Cbc + "PlotIdentification", Part("PlotIdentification")),
            new XElement(Cbc + "CitySubdivisionName", Part("CitySubdivisionName")),
            new XElement(Cbc + "CityName", Part("CityName")),
            new XElement(Cbc + "PostalZone", Part("PostalZone")),
            new XElement(Cbc + "CountrySubentity", Part("CountrySubentity")),
            new XElement(Cac + "Country",
                new XElement(Cbc + "IdentificationCode", Part("CountryCode", seller.CountryCode))));
    }

    private static XElement TaxTotalSummary(ComplianceDocument document, string currency) =>
        new(Cac + "TaxTotal",
            Amount(Cbc + "TaxAmount", document.Totals.TaxTotal, currency, "tax_total"));

    private static XElement TaxTotalDetailed(ComplianceDocument document, string currency)
    {
        XElement total = new(Cac + "TaxTotal",
            Amount(Cbc + "TaxAmount", document.Totals.TaxTotal, currency, "tax_total"));

        // التجميع بنسبة الضريبة: مجموعة فرعية لكل نسبة، بترتيب ثابت — لأن ترتيب
        // المجموعات يدخل البايتات الموقَّعة، فترتيبٌ غير محسوم يعني بصمة غير محسومة.
        foreach (IGrouping<decimal, DocumentLine> group in document.Lines
                     .GroupBy(static line => line.TaxRate)
                     .OrderBy(static g => g.Key))
        {
            decimal taxable = group.Sum(static line => line.NetAmount);
            decimal tax = group.Sum(static line => line.TaxAmount);

            total.Add(new XElement(Cac + "TaxSubtotal",
                Amount(Cbc + "TaxableAmount", taxable, currency, "taxable_amount"),
                Amount(Cbc + "TaxAmount", tax, currency, "tax_amount"),
                new XElement(Cac + "TaxCategory",
                    new XElement(Cbc + "ID", CategoryOf(group.Key)),
                    new XElement(Cbc + "Percent", ZatcaAmounts.RenderPercent(group.Key)),
                    new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))));
        }

        return total;
    }

    private static XElement LegalMonetaryTotal(ComplianceDocument document, string currency) =>
        new(Cac + "LegalMonetaryTotal",
            Amount(Cbc + "LineExtensionAmount", document.Totals.NetTotal, currency, "net_total"),
            Amount(Cbc + "TaxExclusiveAmount", document.Totals.NetTotal, currency, "net_total"),
            Amount(Cbc + "TaxInclusiveAmount", document.Totals.GrossTotal, currency, "gross_total"),
            Amount(Cbc + "AllowanceTotalAmount", 0m, currency, "allowance_total"),
            Amount(Cbc + "PrepaidAmount", 0m, currency, "prepaid"),
            Amount(Cbc + "PayableAmount", document.Totals.GrossTotal, currency, "gross_total"));

    private XElement InvoiceLine(DocumentLine line, string currency) =>
        new(Cac + "InvoiceLine",
            new XElement(Cbc + "ID", line.LineNo.ToString(CultureInfo.InvariantCulture)),
            new XElement(Cbc + "InvoicedQuantity",
                new XAttribute("unitCode", seller.UnitCode),
                ZatcaAmounts.RenderQuantity(line.Quantity)),
            Amount(Cbc + "LineExtensionAmount", line.NetAmount, currency, "line_net"),
            new XElement(Cac + "TaxTotal",
                Amount(Cbc + "TaxAmount", line.TaxAmount, currency, "line_tax"),
                Amount(Cbc + "RoundingAmount", line.GrossAmount, currency, "line_gross")),
            new XElement(Cac + "Item",
                new XElement(Cbc + "Name", line.DescriptionAr),
                new XElement(Cac + "ClassifiedTaxCategory",
                    new XElement(Cbc + "ID", CategoryOf(line.TaxRate)),
                    new XElement(Cbc + "Percent", ZatcaAmounts.RenderPercent(line.TaxRate)),
                    new XElement(Cac + "TaxScheme", new XElement(Cbc + "ID", "VAT")))),
            new XElement(Cac + "Price",
                Amount(Cbc + "PriceAmount", line.UnitPrice, currency, "unit_price")));

    private static XElement Amount(XName name, decimal value, string currency, string field) =>
        new(name, new XAttribute("currencyID", currency), ZatcaAmounts.Render(value, field));

    /// <summary>
    /// فئة الضريبة. <c>S</c> للنسبة القياسية و<c>Z</c> للصفرية.
    /// <b>الإعفاء (‏E) والخارج عن النطاق (‏O) ليسا مشتقّين من النسبة</b> — نسبتهما صفر أيضاً —
    /// فلا يُخمَّنان هنا: يحتاجان حقلاً في المستند لا استنتاجاً من رقم.
    /// </summary>
    [Provisional("رموز فئات الضريبة، وكيف يُفرَّق بين الصفرية والمعفاة والخارجة عن النطاق",
        DerivedFrom = "قراءة المواصفة — لا من الهيئة",
        Risk = ProvisionalRisk.Reworkable,
        VerifyBy = "جدول فئات الضريبة وأسباب الإعفاء في مواصفة الفاتورة الإلكترونية")]
    private static string CategoryOf(decimal ratePercent) => ratePercent > 0m ? "S" : "Z";
}

/// <summary>
/// ما يعرفه المزوّد عن البائع ولا يحمله المستند المجالي: رقم السجل التجاري،
/// ورمز الدولة، ووسيلة السداد الافتراضية، ووحدة القياس.
/// <b>إعداد لكل مستأجر، لا ثوابت في الشيفرة.</b>
/// </summary>
public sealed record ZatcaSellerIdentity(
    string CommercialRegistrationNumber,
    string CountryCode = "SA",
    [property: Provisional("رمز وسيلة السداد الافتراضي وقائمة الرموز المقبولة",
        Risk = ProvisionalRisk.Cosmetic,
        VerifyBy = "قائمة رموز وسائل السداد في مواصفة الفاتورة الإلكترونية")]
    string PaymentMeansCode = "10",
    [property: Provisional("رمز وحدة القياس الافتراضي",
        Risk = ProvisionalRisk.Cosmetic,
        VerifyBy = "قائمة رموز وحدات القياس المقبولة")]
    string UnitCode = "PCE");
