using System.Globalization;

namespace BabelPosOffline.Support;

/// <summary>
/// النقود: <c>decimal</c> في الكود، <c>NUMERIC(19,4)</c> في PostgreSQL،
/// و<b>عدد صحيح 64-bit بوحدات صغرى بمقياس 4</b> داخل SQLite.
///
/// لماذا العدد الصحيح في SQLite تحديداً: SQLite لا يملك نوعاً عشرياً دقيقاً. أي عمود
/// بتقارب <c>REAL</c> — أو أي <c>sum()</c> على نص — يتحوّل إلى <c>double</c> بصمت،
/// وهذا يخالف القاعدة الصريحة «لا float في أي طبقة، بما فيها المخزن المحلي». العدد
/// الصحيح بوحدات صغرى دقيق تماماً، و<c>sum()</c> عليه في SQLite يبقى صحيحاً ويرفع
/// خطأ فيضان بدل أن ينزلق إلى العائم.
///
/// SQLite has no exact decimal type: a REAL-affinity column — or sum() over TEXT —
/// silently becomes a double. Scaled 64-bit integers are exact, and SQLite's sum()
/// over integers stays integral and raises on overflow instead of sliding into float.
/// </summary>
public static class Money
{
    public const int Scale = 4;
    private const decimal Factor = 10_000m;

    /// <summary>أقصى مبلغ يسع في 64-bit بمقياس 4 / largest amount representable.</summary>
    public static readonly decimal MaxAmount = long.MaxValue / Factor;

    /// <summary>يرفض — ولا يقرّب بصمت — أي مبلغ بمقياس أكبر من 4.</summary>
    public static void AssertScale(decimal v, string what)
    {
        if (decimal.Round(v, Scale) != v)
            throw new InvalidOperationException(
                $"MONEY_SCALE_VIOLATION: {what} = {v.ToString(CultureInfo.InvariantCulture)} has more than {Scale} decimal places. " +
                "Rounding must be an explicit, recorded step - never a silent storage side effect.");
        if (Math.Abs(v) > MaxAmount)
            throw new InvalidOperationException($"MONEY_OVERFLOW: {what} = {v} exceeds {MaxAmount}");
    }

    /// <summary>decimal → وحدات صغرى (دقيق) / exact conversion to scaled minor units.</summary>
    public static long ToMinor(decimal v, string what = "amount")
    {
        AssertScale(v, what);
        return (long)(v * Factor);
    }

    /// <summary>وحدات صغرى → decimal (دقيق) / exact conversion back.</summary>
    public static decimal FromMinor(long minor) => minor / Factor;

    /// <summary>الشكل القانوني للتجزئة: مقياس ثابت، ثقافة ثابتة. 100m و100.0000m متطابقان.</summary>
    public static string Canonical(decimal v) => v.ToString("0.0000", CultureInfo.InvariantCulture);

    public static string CanonicalMinor(long minor) => Canonical(FromMinor(minor));

    /// <summary>الكميّات: مقياس 3، وتخزَّن أيضاً كعدد صحيح.</summary>
    public const int QtyScale = 3;
    private const decimal QtyFactor = 1_000m;

    public static long QtyToMinor(decimal q)
    {
        if (decimal.Round(q, QtyScale) != q)
            throw new InvalidOperationException($"QTY_SCALE_VIOLATION: {q} has more than {QtyScale} decimal places");
        return (long)(q * QtyFactor);
    }

    public static decimal QtyFromMinor(long m) => m / QtyFactor;
    public static string CanonicalQty(decimal q) => q.ToString("0.000", CultureInfo.InvariantCulture);
}
