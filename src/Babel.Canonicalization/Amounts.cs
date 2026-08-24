using System.Globalization;

namespace Babel.Canonicalization;

/// <summary>
/// المبالغ — شكل لفظي واحد بالضبط: <c>-?\d{1,15}\.\d{4}</c>.
///
/// <b>1. الثقافة الثابتة ليست تفصيلاً.</b> مقيس على هذا الجهاز:
/// <code>
///   CultureInfo.CurrentCulture = ar-SA;
///   100.5m.ToString("0.0000")                 -> "100٫5000"   (U+066B لا '.')
///   new DateTime(2026,1,2).ToString("d")      -> "13\u200F/7\u200F/1447 بعد الهجرة"
/// </code>
/// أي أن جهازاً واحداً بلغة عربية يُنتج فاصلة عربية <b>داخل البايتات المُجزَّأة</b>،
/// وتاريخاً هجرياً <b>يحمل بداخله محارف U+200F غير مرئية</b>. المصيدتان تتراكبان.
/// ولذلك: كل تحويل إلى نص في هذه المكتبة يمرّ بـ<c>CultureInfo.InvariantCulture</c> صراحة.
///
/// <b>2. المقياس 4 هو الشكل القانوني في كل النطاق.</b> مقيس:
/// <code>
///   كُتب 100.00m إلى numeric(19,4) وقُرئ  -> بايت المقياس 4، "100.0000"
///   كُتب 100.00m إلى numeric غير مقيَّد وقُرئ -> بايت المقياس 2، "100.00"
/// </code>
/// أي أن PostgreSQL تحفظ «مقياس العرض» المُرسَل في العمود غير المقيَّد. ولذلك:
/// العمود يجب أن يكون <c>numeric(19,4)</c>، <b>و</b>لا يجوز أبداً مقارنة
/// <c>decimal.GetBits()</c> أو استدعاء <c>ToString()</c> بلا صيغة ثابتة.
///
/// <b>3. لا تقريب. رفض.</b> مقيس:
/// <code>
///   decimal.Round(0.00005m, 4)                 -> 0.0000   (.NET: نصف إلى الزوجي)
///   PostgreSQL: insert 0.00005 into numeric(19,4) -> 0.0001 (نصف بعيداً عن الصفر)
/// </code>
/// النظامان يقرّبان بقاعدتين مختلفتين. لو قرّبنا في .NET وخزّنّا، لاختلفت القيمة
/// المُجزَّأة عن القيمة المخزَّنة عند نقاط المنتصف. ولذلك أي قيمة بأكثر من أربع
/// خانات عشرية <b>تُرفض</b>، ولا تُقرَّب.
/// </summary>
public static class Amounts
{
    /// <summary>عدد الخانات العشرية القانوني. غير قابل للتغيير في v1.</summary>
    public const int Scale = 4;

    /// <summary>أكبر قيمة تسع في <c>numeric(19,4)</c>: 15 خانة صحيحة + 4 عشرية.</summary>
    public const decimal Max = 999_999_999_999_999.9999m;

    /// <summary>أصغر قيمة تسع في <c>numeric(19,4)</c>.</summary>
    public const decimal Min = -999_999_999_999_999.9999m;

    private const string Format = "0.0000";

    /// <summary>
    /// يعيد نفس القيمة بمقياس 4 بالضبط، أو يرمي إن كانت تحتاج تقريباً أو تتجاوز المدى.
    /// <b>يُستدعى عند الحدّ، ويُخزَّن ناتجه</b>، حتى يكون المخزَّن والمُجزَّأ شيئاً واحداً.
    /// </summary>
    public static decimal Normalize(decimal value, string? field = null)
    {
        Require(value, field);

        // decimal.Round هنا لا يقرّب شيئاً: تحقّقنا أعلاه أن المقياس <= 4.
        // الغرض هو رفع المقياس إلى 4 بالضبط (100.00m -> 100.0000m) وإزالة
        // إشارة الصفر السالب.
        var scaled = decimal.Round(value, Scale, MidpointRounding.ToEven);
        if (scaled == 0m) return 0.0000m;
        return scaled + 0.0000m;
    }

    /// <summary>يتحقّق أن القيمة صالحة للتجزئة كما هي، أو يرمي. لا يعدّل.</summary>
    public static decimal Require(decimal value, string? field = null)
    {
        if (value < Min || value > Max)
            throw new CanonicalizationException(CanonErrors.AmountOutOfRange,
                $"القيمة {value.ToString(CultureInfo.InvariantCulture)} خارج مدى numeric(19,4) " +
                $"[{Min.ToString(Format, CultureInfo.InvariantCulture)} .. {Max.ToString(Format, CultureInfo.InvariantCulture)}]. " +
                "قيمة لا تُخزَّن لا يجوز أن تُجزَّأ.", -1, field);

        var scale = (decimal.GetBits(value)[3] >> 16) & 0xFF;
        if (scale > Scale)
        {
            // المقياس المُعلَن قد يكون أكبر من المقياس الفعلي (100.5000m مقياسه 4
            // لكن 100.50000m مقياسه 5 وقيمته نفسها). الفحص الحاسم هو فقدان الدقّة.
            var truncated = decimal.Truncate(value * 10_000m) / 10_000m;
            if (truncated != value)
                throw new CanonicalizationException(CanonErrors.AmountScaleExceeded,
                    $"القيمة {value.ToString(CultureInfo.InvariantCulture)} تحمل أكثر من {Scale} خانات عشرية. " +
                    "لا تُقرَّب هنا: .NET تقرّب «نصف إلى الزوجي» وPostgreSQL تقرّب «نصف بعيداً عن الصفر» (مقيس)، " +
                    "فيختلف المخزَّن عن المُجزَّأ عند نقاط المنتصف. قرّب صراحةً عند الحدّ بقرار محاسبي معلن.",
                    -1, field);
        }

        return value;
    }

    /// <summary>
    /// الشكل اللفظي القانوني. مثال: <c>100.0000</c>، <c>-2500.7500</c>، <c>0.0000</c>.
    /// لا فاصل آلاف، لا علامة موجب، لا صيغة أسّية، لا صفر سالب.
    /// </summary>
    public static string Render(decimal value, string? field = null)
    {
        Require(value, field);
        var s = value.ToString(Format, CultureInfo.InvariantCulture);

        // حزام أمان: الصيغة "0.0000" تُسقط إشارة الصفر السالب على .NET 10 (مقيس)،
        // لكن السلوك لم يكن كذلك دائماً عبر الإصدارات. نثبّته صراحة.
        if (s is "-0.0000") s = "0.0000";
        return s;
    }

    /// <summary>
    /// يقرأ مبلغاً من نصّ <b>بالشكل القانوني وحده</b>.
    /// يرفض <c>100</c> و<c>100.00</c> و<c>+100.0000</c> و<c>1.0E2</c> و<c>100,0000</c>
    /// و<c>١٠٠.٠٠٠٠</c> — كلها قيم صحيحة رقمياً وأشكال لفظية غير قانونية.
    /// </summary>
    public static decimal ParseCanonical(string text, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsCanonicalLiteral(text))
            throw new CanonicalizationException(CanonErrors.AmountBadLiteral,
                $"«{text}» ليس شكلاً قانونياً. الشكل الوحيد المقبول هو -?\\d{{1,15}}\\.\\d{{4}} " +
                "بأرقام ASCII ونقطة عشرية ASCII، بلا علامة موجب وبلا صفر بادئ زائد وبلا صيغة أسّية.",
                -1, field);

        var value = decimal.Parse(text, NumberStyles.AllowLeadingSign | NumberStyles.AllowDecimalPoint,
            CultureInfo.InvariantCulture);
        return Require(value, field);
    }

    /// <summary>يقبل الشكل القانوني وحده. مكتوب يدوياً لتجنّب اعتماد Regex.</summary>
    public static bool IsCanonicalLiteral(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        var i = 0;
        if (text[0] == '-') i = 1;
        if (text.Length - i < 6) return false; // "0.0000" على الأقل

        var dot = text.IndexOf('.');
        if (dot < 0 || dot != text.Length - 5) return false;

        var intDigits = dot - i;
        if (intDigits is < 1 or > 15) return false;
        if (intDigits > 1 && text[i] == '0') return false; // لا صفر بادئ

        for (var k = i; k < text.Length; k++)
        {
            if (k == dot) continue;
            if (text[k] is < '0' or > '9') return false;
        }

        if (text[0] == '-' && text.AsSpan(1).SequenceEqual("0.0000")) return false; // لا صفر سالب
        return true;
    }
}
