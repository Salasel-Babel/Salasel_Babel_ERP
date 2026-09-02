using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.SharedKernel;

namespace Babel.Inventory.Application;

/// <summary>
/// معامل تحويل وحدةٍ إلى وحدة أساس — <b>عددٌ نسبيّ دقيق، بسطٌ ومقام، لا عددٌ عائم</b>.
/// <para>
/// «الكرتون اثنتا عشرة حبّة» يُكتب <c>12/1</c>، و«الحبّة ثلث علبة» يُكتب <c>1/3</c>.
/// والثاني <b>لا يُمثَّل عشرياً بلا خسارة</b>، وأي خسارة فيه تصل إلى المال: الكمية
/// تُضرب في تكلفة الوحدة. فالنسبة تبقى نسبة، والتحويل الذي لا يقع بلا باقٍ
/// <b>يُرفض باسمه</b> ولا يُقرَّب في الخفاء.
/// </para>
/// </summary>
/// <param name="Numerator">البسط — موجب.</param>
/// <param name="Denominator">المقام — موجب.</param>
internal readonly record struct UnitRatio(long Numerator, long Denominator)
{
    /// <summary>نسبة الوحدة إلى نفسها: واحد على واحد.</summary>
    public static UnitRatio Identity => new(1L, 1L);

    /// <summary>هل هي واحد على واحد؟</summary>
    public bool IsIdentity => Numerator == Denominator;

    /// <summary>تمثيل نصّي ثابت الثقافة للعرض في رسائل الرفض.</summary>
    public override string ToString() =>
        Numerator.ToString(CultureInfo.InvariantCulture) + "/" + Denominator.ToString(CultureInfo.InvariantCulture);
}

/// <summary>
/// تحويل الكمّيات بين وحدات الصنف.
/// <para>
/// <b>دالّة صافية بلا قاعدة بيانات</b> — المعامل يُقرأ في الخدمة ويُسلَّم إليها هنا،
/// كي تُختبر حالات الحدّ (التحويل غير التامّ، والوحدة المجهولة، والمقام الصفري) بلا
/// أي تهيئة.
/// </para>
/// </summary>
internal static class UnitConversion
{
    /// <summary>مقياس الكمّية بعد التحويل — هو مقياس عمود الكمية في المخطّط.</summary>
    public const int QuantityScale = 6;

    /// <summary>
    /// يحوّل مقداراً من وحدته إلى وحدة الأساس بمعاملٍ نسبيّ.
    /// <para>
    /// <b>والتحويل يجب أن يقع بلا باقٍ.</b> يُحسب <c>المقدار × البسط</c> ثم يُقسَم على
    /// المقام، ويُتحقَّق أن الناتج مضروباً في المقام يعود إلى المقسوم <b>بالضبط</b>.
    /// فتحويلٌ يُنتج كسراً غير منتهٍ — نصف حبّة من ثلث علبة — <b>يُرفض</b> ولا يُقرَّب:
    /// الرقم المقرَّب في دفترٍ يُضاف إليه فقط يتراكم على كل حركة، وينتهي إلى رصيد
    /// قيمةٍ لا يساوي مجموع حركاته.
    /// </para>
    /// </summary>
    /// <param name="magnitude">المقدار بوحدته.</param>
    /// <param name="ratio">معامل التحويل إلى وحدة الأساس.</param>
    public static Result<decimal> ToBase(decimal magnitude, UnitRatio ratio)
    {
        if (ratio.Numerator <= 0L || ratio.Denominator <= 0L)
        {
            return Result<decimal>.Failure(InventoryErrors.UnitRatioNotPositive(ratio.ToString()));
        }

        if (ratio.IsIdentity)
        {
            return Result<decimal>.Success(magnitude);
        }

        // ‏**الطفح يُمسَك ويُسمّى، ولا يُترك يرمي.** المقدار يعبر السلك بعشرين خانة
        // صحيحة (‏<c>WireNumbers.MaxIntegerDigits</c>) والبسط يبلغ ملياراً، وحاصلُهما
        // يتجاوز مدى <c>decimal</c>. واستثناءٌ غير مُمسَك هنا يخرج من السطح خطأَ خادم
        // ‏500 — أي «عطلٌ عندنا» — وهو في الحقيقة **مُدخَل مرفوض** له علاجٌ يُقال.
        decimal scaled;
        decimal converted;

        try
        {
            scaled = magnitude * ratio.Numerator;
            converted = scaled / ratio.Denominator;
        }
        catch (OverflowException)
        {
            return Result<decimal>.Failure(InventoryErrors.ConversionOverflows(magnitude, ratio.ToString()));
        }

        decimal rounded = decimal.Round(converted, QuantityScale, MidpointRounding.ToEven);

        return rounded * ratio.Denominator == scaled
            ? Result<decimal>.Success(rounded)
            : Result<decimal>.Failure(InventoryErrors.ConversionNotExact(magnitude, ratio.ToString()));
    }

    /// <summary>يقارن رمزَي وحدة — بالحرف لا بالثقافة (‏فخ-38 · القاعدة 10).</summary>
    /// <param name="left">الأول.</param>
    /// <param name="right">الثاني.</param>
    public static bool SameUnit(string left, string right)
        => string.Equals(left, right, StringComparison.Ordinal);

    /// <summary>
    /// يتحقّق أن الكمّية تحمل وحدةً ومقداراً موجباً — <b>قبل أي حساب</b>.
    /// </summary>
    /// <param name="quantity">الكمّية.</param>
    public static Result Validate(InventoryQuantity quantity)
    {
        ArgumentNullException.ThrowIfNull(quantity);

        if (string.IsNullOrWhiteSpace(quantity.Unit))
        {
            return Result.Failure(InventoryErrors.UnitMissing());
        }

        return quantity.Magnitude <= 0m
            ? Result.Failure(InventoryErrors.QuantityNotPositive(quantity.Magnitude))
            : Result.Success();
    }
}
