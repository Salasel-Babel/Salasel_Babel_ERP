using Babel.Core.CompanySetup;

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
    /// <summary>
    /// يقرّب إلى الوحدة الصغرى لعملة المنشأة — الهللة خانتان، والفلسُ الكويتي ثلاث — والنصف
    /// يبتعد عن الصفر. ولا رقمَ هنا: عددُ الخانات من صفّ التأسيس (ADR-0089).
    /// </summary>
    /// <param name="value">القيمة.</param>
    /// <param name="money">عملة المنشأة.</param>
    public static decimal Round(decimal value, CompanyMoney money) => money.Round(value);

    /// <summary>صافي السطر وضريبته، كلاهما مقرَّب على السطر.</summary>
    public static (decimal Net, decimal Tax) Line(
        decimal quantity,
        decimal unitPrice,
        decimal discount,
        decimal taxRate,
        string taxClassification,
        CompanyMoney money)
    {
        decimal extended = Round(quantity * unitPrice, money);
        decimal net = Round(extended - discount, money);
        decimal effectiveRate = string.Equals(taxClassification, "standard", StringComparison.Ordinal) ? taxRate : 0m;
        decimal tax = Round(net * effectiveRate, money);
        return (net, tax);
    }
}
