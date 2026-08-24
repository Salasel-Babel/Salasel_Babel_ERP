using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;

namespace Babel.Compliance.Pipeline;

/// <summary>
/// التطبيع عند الحدّ، <b>مرة واحدة</b>، قبل أي تجزئة — والناتج هو ما يُخزَّن ويُوقَّع.
/// (02-architecture §8.3 ع-1). كل نص يعبر إلى الالتزام يمرّ من هنا ولا شيء غيره.
/// </summary>
public static class ComplianceDocumentNormaliser
{
    public static ComplianceDocument Normalise(ComplianceDocument d) => d with
    {
        DocumentNumber = ComplianceText.Normalise(d.DocumentNumber, nameof(d.DocumentNumber)),
        CurrencyCode = ComplianceText.Normalise(d.CurrencyCode, nameof(d.CurrencyCode)),
        CorrectionReasonAr = d.CorrectionReasonAr is null
            ? null : ComplianceText.Normalise(d.CorrectionReasonAr, nameof(d.CorrectionReasonAr)),
        CorrectionReasonEn = d.CorrectionReasonEn is null
            ? null : ComplianceText.Normalise(d.CorrectionReasonEn, nameof(d.CorrectionReasonEn)),
        Seller = Party(d.Seller, "seller"),
        Buyer = d.Buyer is null ? null : Party(d.Buyer, "buyer"),
        Lines = [.. d.Lines.Select(Line)],
        IssuedAt = ComplianceCanonical.PgInstant(d.IssuedAt)
    };

    private static PartyRef Party(PartyRef p, string prefix) => p with
    {
        NameAr = ComplianceText.Normalise(p.NameAr, $"{prefix}.NameAr"),
        NameEn = ComplianceText.Normalise(p.NameEn, $"{prefix}.NameEn"),
        TaxRegistrationNumber = p.TaxRegistrationNumber is null
            ? null : ComplianceText.Normalise(p.TaxRegistrationNumber, $"{prefix}.TaxRegistrationNumber"),
        AddressAr = p.AddressAr is null ? null : ComplianceText.Normalise(p.AddressAr, $"{prefix}.AddressAr"),
        AddressEn = p.AddressEn is null ? null : ComplianceText.Normalise(p.AddressEn, $"{prefix}.AddressEn"),
        AddressParts = p.AddressParts?.ToDictionary(
            kv => ComplianceText.Normalise(kv.Key, $"{prefix}.AddressParts.key"),
            kv => ComplianceText.Normalise(kv.Value, $"{prefix}.AddressParts[{kv.Key}]"))
    };

    private static DocumentLine Line(DocumentLine l) => l with
    {
        DescriptionAr = ComplianceText.Normalise(l.DescriptionAr, $"line[{l.LineNo}].DescriptionAr"),
        DescriptionEn = ComplianceText.Normalise(l.DescriptionEn, $"line[{l.LineNo}].DescriptionEn")
    };
}
