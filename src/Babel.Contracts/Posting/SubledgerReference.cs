namespace Babel.Contracts.Posting;

/// <summary>
/// إشارة إلى طرف في دفتر مساعد. الوحدة تقول «هذا السطر يخصّ العميل ع-١٢٣»،
/// ولا تقول «رحّله على حساب ١٢١٠١». الحساب الضابط يقرّره Babel.Ledger.
/// </summary>
/// <param name="Kind">نوع الدفتر المساعد.</param>
/// <param name="PartyId">معرّف الطرف داخل الوحدة المالكة له.</param>
public readonly record struct SubledgerReference(SubledgerKind Kind, string PartyId)
{
    /// <summary>لا إشارة.</summary>
    public static SubledgerReference None => new(SubledgerKind.None, string.Empty);
}
