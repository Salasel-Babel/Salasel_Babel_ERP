using System.Globalization;

namespace Babel.Api.Wire;

/// <summary>
/// <b>المال على السلك — القرار وتنفيذه.</b>
/// <para>
/// ‏JSON لا يملك نوعاً عشرياً. مواصفة <c>RFC 8259 §6</c> تسمح صراحةً لأي مُنفِّذ بتمثيل
/// الأرقام بدقّة محدودة، وأغلب أطر العميل — وأولها <c>JSON.parse</c> في المتصفّح — تحوّل
/// كل رمز رقمي إلى <c>double</c> ثنائي. أي أن <b>رمزاً رقمياً في حقل مالي هو قناة فقدان
/// دقّة مفتوحة</b> لا يملك الخادم أن يُغلقها بعد وصولها: حين تصل القيمة يكون التلف قد وقع
/// عند العميل.
/// </para>
/// <para>
/// <b>القرار:</b> كل مبلغ يعبر السلك <b>نصّاً</b>، ويُرفض الرمز الرقمي في الحقل المالي
/// رفضاً صريحاً برمز <c>wire.money.number_token</c>. النصّ يُحلَّل بماسح مكتوب بيدنا يقبل
/// نحواً واحداً لا غير:
/// </para>
/// <code>
/// -?(0|[1-9][0-9]*)(\.[0-9]{1,4})?
/// </code>
/// <para>
/// ولذلك تُرفض: الصيغة الأسّية (<c>1e2</c>)، والصفر البادئ (<c>007</c>)، والإشارة الموجبة
/// الصريحة (<c>+5</c>)، والفراغ في الطرفين، والفاصلة العشرية العربية (<c>٫</c>)، والأرقام
/// العربية-الهندية (<c>٠-٩</c>) والديفاناغارية (<c>०-९</c>)، وما زاد على أربع خانات عشرية.
/// </para>
/// <para>
/// <b>ولماذا الرفض لا التطبيع في الأرقام غير اللاتينية:</b> التطبيع الصامت يعني أن العميل
/// يظن أنه أرسل ما لم يصل، والفرق بين «٠٫٤٠١٣» و«0.4013» لا يُرى في سجلّ ولا في مراجعة.
/// الرفض يُرى فوراً، وتحويلُ الأرقام قرار <b>واجهة</b> يقع عند العميل حيث يعرف سياقه —
/// لا قراراً يتخذه الخادم نيابةً عنه (فخ-25).
/// </para>
/// </summary>
internal static class WireNumbers
{
    /// <summary>أقصى عدد خانات عشرية لمبلغ مالي — المقياس القانوني نفسه في <c>Money</c>.</summary>
    public const int MoneyScale = 4;

    /// <summary>أقصى عدد خانات عشرية لسعر الصرف (‏<c>PostingRequest.ExchangeRate</c> بمقياس 8).</summary>
    public const int RateScale = 8;

    /// <summary>أقصى عدد خانات صحيحة — يمنع نصّاً طويلاً يُرهق المحلّل قبل أن يُرفض.</summary>
    private const int MaxIntegerDigits = 20;

    /// <summary>
    /// يحلّل نصّاً عشرياً بالنحو الصارم أعلاه.
    /// </summary>
    /// <param name="text">النصّ الوارد من العميل.</param>
    /// <param name="maxScale">أقصى عدد خانات عشرية مقبول.</param>
    /// <param name="field">اسم الحقل، لرسالة الخطأ.</param>
    /// <exception cref="WireFormatException">النصّ لا يطابق النحو.</exception>
    public static decimal ParseStrict(string? text, int maxScale, string field)
    {
        if (text is null)
        {
            throw Reject("wire.number.missing", field, "قيمة عددية مفقودة.", "A numeric value is missing.");
        }

        if (text.Length == 0)
        {
            throw Reject("wire.number.empty", field, "قيمة عددية فارغة.", "A numeric value is empty.");
        }

        // الأرقام غير اللاتينية تُسمّى قبل كل شيء: رسالة «شكل غير صالح» عامة على
        // «٠٫٤٠١٣» تُرسل المطوّر إلى مكان خاطئ تماماً.
        foreach (char c in text)
        {
            if (IsNonLatinDigit(c))
            {
                throw Reject(
                    "wire.number.non_latin_digits",
                    field,
                    "الأرقام غير اللاتينية مرفوضة على السلك رفضاً صريحاً؛ التحويل قرار واجهة يقع عند العميل، "
                    + "ولا يقع صامتاً في الخادم.",
                    "Non-Latin digits are explicitly refused on the wire; converting them is a client-side "
                    + "presentation decision, never a silent server-side one.");
            }
        }

        int index = 0;
        bool negative = false;

        if (text[index] == '-')
        {
            negative = true;
            index++;
        }

        int integerStart = index;
        while (index < text.Length && text[index] is >= '0' and <= '9')
        {
            index++;
        }

        int integerDigits = index - integerStart;
        if (integerDigits == 0)
        {
            throw Malformed(field, text);
        }

        if (integerDigits > MaxIntegerDigits)
        {
            throw Reject(
                "wire.number.too_many_integer_digits",
                field,
                "عدد الخانات الصحيحة يتجاوز الحدّ المسموح.",
                "The number has more integer digits than allowed.");
        }

        // الصفر البادئ ممنوع: «007» و«7» رمزان مختلفان لقيمة واحدة، والاختلاف يظهر
        // في أي مقارنة نصّية وفي أي مفتاح إحكام.
        if (integerDigits > 1 && text[integerStart] == '0')
        {
            throw Reject(
                "wire.number.leading_zero",
                field,
                "الصفر البادئ ممنوع: تمثيلان لقيمة واحدة يكسران كل مقارنة نصّية.",
                "A leading zero is refused: two spellings of one value break every textual comparison.");
        }

        int scale = 0;
        if (index < text.Length && text[index] == '.')
        {
            index++;
            int fractionStart = index;
            while (index < text.Length && text[index] is >= '0' and <= '9')
            {
                index++;
            }

            scale = index - fractionStart;
            if (scale == 0)
            {
                throw Malformed(field, text);
            }
        }

        if (index != text.Length)
        {
            throw Malformed(field, text);
        }

        if (scale > maxScale)
        {
            throw Reject(
                "wire.number.scale_exceeded",
                field,
                FormattableString.Invariant(
                    $"القيمة تحمل {scale} خانة عشرية والحدّ {maxScale}. التقريب قرار محاسبي صريح، لا سلوك ضمني عند الحدّ."),
                FormattableString.Invariant(
                    $"The value carries {scale} decimal places and the limit is {maxScale}. Rounding is an explicit accounting decision, never implicit at the boundary."));
        }

        // ‏decimal.Parse بثقافة ثابتة وأنماط ضيّقة. النحو فُحص بالكامل أعلاه، فما يصل
        // هنا لا يحتمل إلا التمثيل الواحد — ولا يمرّ في أي طريق على double.
        if (!decimal.TryParse(
                text,
                NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
                CultureInfo.InvariantCulture,
                out decimal value))
        {
            throw Reject(
                "wire.number.not_representable",
                field,
                "القيمة خارج المدى الذي يمثّله النوع العشري.",
                "The value is outside the range the decimal type represents.");
        }

        // ‏«-0» يمرّ من النحو ويحمل إشارة سالبة في بايتات decimal. تسويته هنا بلا
        // حساب: نُعيد الصفر الحرفي بمقياسه.
        return negative && value == 0m ? ZeroAtScale(scale) : value;
    }

    /// <summary>التمثيل القانوني لمبلغ على السلك: مقياس أربعة وثقافة ثابتة، دائماً.</summary>
    /// <param name="value">القيمة.</param>
    public static string FormatMoney(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

    /// <summary>التمثيل القانوني لعدد صحيح 64 بت على السلك — نصّاً لا رقماً.</summary>
    /// <remarks>
    /// ‏<c>Number</c> في JavaScript يفقد الدقّة فوق ‎2^53، ورقم القيد ورقم التسلسل
    /// معرّفان لا كمّيتان: خسارة خانة فيهما تعني قيداً يُشار إليه خطأً. النصّ يُغلق الباب
    /// عند الحدّ بدل أن يُترك لكل عميل أن يتذكّره.
    /// </remarks>
    /// <param name="value">القيمة.</param>
    public static string FormatInt64(long value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>هل المحرف رقم يونيكود من خارج المجموعة اللاتينية <c>0-9</c>؟</summary>
    /// <param name="c">المحرف.</param>
    public static bool IsNonLatinDigit(char c) => char.IsDigit(c) && c is not (>= '0' and <= '9');

    /// <summary>ينشئ رفضاً شكلياً موسوماً بحقله.</summary>
    /// <param name="code">الرمز الثابت.</param>
    /// <param name="field">الحقل.</param>
    /// <param name="ar">الرسالة العربية.</param>
    /// <param name="en">الرسالة الإنجليزية.</param>
    public static WireFormatException Reject(string code, string field, string ar, string en) =>
        new(code, $"«{field}»: {ar}", $"'{field}': {en}", field);

    private static WireFormatException Malformed(string field, string text) => Reject(
        "wire.number.malformed",
        field,
        $"القيمة «{Truncate(text)}» لا تطابق النحو المسموح: ‎-?(0|[1-9][0-9]*)(\\.[0-9]+)?‎ — "
        + "لا صيغة أسّية، ولا فراغ، ولا إشارة موجبة صريحة، ولا فاصلة عشرية غير النقطة.",
        $"The value '{Truncate(text)}' does not match the permitted grammar -?(0|[1-9][0-9]*)(\\.[0-9]+)? — "
        + "no exponent, no whitespace, no explicit plus sign, and no decimal separator other than '.'.");

    private static decimal ZeroAtScale(int scale) => scale switch
    {
        0 => 0m,
        1 => 0.0m,
        2 => 0.00m,
        3 => 0.000m,
        4 => 0.0000m,
        5 => 0.00000m,
        6 => 0.000000m,
        7 => 0.0000000m,
        _ => 0.00000000m,
    };

    private static string Truncate(string text) => text.Length <= 32 ? text : text[..32] + "…";
}
