namespace Babel.Sales.Application;

/// <summary>
/// حساب السطر والمجاميع.
/// <para>
/// <b>القاعدة:</b> الضريبة تُحسب وتُقرَّب <b>على مستوى السطر</b>، ومجموع المستند هو
/// <b>مجموع سطور مقرَّبة</b> ولا يُعاد تقريبه. القلب — تقريب المجموع بعد جمع قيم غير
/// مقرَّبة — يُنتج فروق هللة واحدة على كل فاتورة تقريباً، وهي فروق تُناقَش مع الهيئة
/// ومع العميل ولا يُدافَع عنها.
/// </para>
/// </summary>
internal static class LineMath
{
    /// <summary>خانتان عشريتان: الهللة أصغر وحدة نقدية، والتقريب سياسة لا صدفة.</summary>
    private const int Halalas = 2;

    /// <summary>يقرّب إلى الهللة، والنصف يبتعد عن الصفر.</summary>
    public static decimal Round(decimal value) => decimal.Round(value, Halalas, MidpointRounding.AwayFromZero);

    /// <summary>صافي السطر وضريبته، كلاهما مقرَّب على السطر.</summary>
    public static (decimal Net, decimal Tax) Line(
        decimal quantity,
        decimal unitPrice,
        decimal discount,
        decimal taxRate,
        string taxClassification)
    {
        decimal extended = Round(quantity * unitPrice);
        decimal net = Round(extended - discount);
        decimal effectiveRate = string.Equals(taxClassification, "standard", StringComparison.Ordinal) ? taxRate : 0m;
        decimal tax = Round(net * effectiveRate);
        return (net, tax);
    }
}
