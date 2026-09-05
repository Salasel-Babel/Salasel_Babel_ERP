using Babel.Contracts.Parameters;
using Babel.SharedKernel;

namespace Babel.Core.Parameters;

/// <summary>
/// <b>حرّاس القيم — والحارس الأول فيها هو الذي يمنع تضاعف الوعاء خمس عشرة مرّة.</b>
/// <para>
/// النِّسَب في هذا النظام <b>كسورٌ عشرية لا مئويات</b>. وقيمةٌ تُكتب <c>15</c> بدل
/// <c>0.15</c> لا يمسكها توازنٌ ولا سلسلةُ إحكام — القيد الناتج متوازن تماماً — فيجب
/// أن تُمسك هنا، عند بابِ الإيداع، قبل أن تصير صفّاً.
/// </para>
/// </summary>
internal static class ParameterGuards
{
    /// <summary>أقصى مقياس لنسبة — مقياس <c>PostingRequest.ExchangeRate</c> نفسه.</summary>
    public const int RateScale = 8;

    /// <summary>أقصى مقياس لمبلغ — المقياس القانوني نفسه في <c>Money</c>.</summary>
    public const int MoneyScale = 4;

    /// <summary>يفحص قيمةً واحدة بصنفها، ويعيد الخطأ أو <c>null</c>.</summary>
    /// <param name="key">المفتاح.</param>
    /// <param name="kind">الصنف.</param>
    /// <param name="value">القيمة.</param>
    public static Error? Check(string key, ParameterValueKind kind, decimal value)
    {
        switch (kind)
        {
            case ParameterValueKind.Rate:
                // ‏**الترتيب مقصود:** «تبدو مئوية» تُقال قبل «خارج المدى»، لأنها الرسالة
                // التي تصف الخطأ الواقع فعلاً. و«خارج المدى» جوابٌ صحيح لا يُصلِح أحداً.
                if (value > 1m && value <= 100m)
                {
                    return ParameterErrors.RateLooksLikeAPercentage(key, value);
                }

                if (value < 0m || value > 1m)
                {
                    return ParameterErrors.RateOutOfRange(key, value);
                }

                return SignificantScale(value) > RateScale
                    ? ParameterErrors.ScaleTooFine(key, value, RateScale)
                    : null;

            case ParameterValueKind.Money:
                if (value < 0m)
                {
                    return ParameterErrors.NegativeValue(key, value);
                }

                return SignificantScale(value) > MoneyScale
                    ? ParameterErrors.ScaleTooFine(key, value, MoneyScale)
                    : null;

            case ParameterValueKind.Count:
                if (value < 0m)
                {
                    return ParameterErrors.NegativeValue(key, value);
                }

                return SignificantScale(value) > 0
                    ? ParameterErrors.ScaleTooFine(key, value, 0)
                    : null;

            default:
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "صنف قيمة غير معروف / unknown value kind");
        }
    }

    /// <summary>
    /// المقياس <b>المعنوي</b> لا التمثيلي: <c>0.150000</c> مقياسها اثنتان لا ستّ.
    /// <para>
    /// والفرق يهمّ: عميلٌ يرسل «0.1500» يقصد خمسة عشر بالمئة، ورفضُه بحجّة المقياس
    /// رفضٌ لرقمٍ صحيح.
    /// </para>
    /// </summary>
    /// <param name="value">القيمة.</param>
    public static int SignificantScale(decimal value)
    {
        int scale = value.Scale;

        while (scale > 0 && value == decimal.Round(value, scale - 1))
        {
            scale--;
        }

        return scale;
    }
}
