using System.Globalization;

namespace Babel.Canonicalization;

/// <summary>
/// أسعار الصرف — شكل لفظي واحد بالضبط: <c>-?\d{1,11}\.\d{8}</c>.
///
/// <b>لماذا نوع مستقلّ عن المبلغ ولا يكفي <see cref="Amounts"/>:</b>
/// عمود <c>ledger.journal_line.fx_rate</c> هو <c>numeric(19,8)</c> لا
/// <c>numeric(19,4)</c>. تمرير سعر صرف عبر قواعد المبلغ يعني أحد أمرين، وكلاهما
/// عطب صامت:
/// <list type="number">
///   <item>‏<b>رفض</b> كل سعر بأكثر من أربع خانات (‏<c>3.75123456</c> سعر مشروع
///         تماماً في <c>numeric(19,8)</c>) — فيصير الحقل غير قابل للتجزئة أصلاً؛</item>
///   <item>أو <b>تقريب</b> إلى أربع خانات — فتختلف القيمة المُجزَّأة عن القيمة
///         المخزَّنة، وهي بالضبط المصيدة التي كُتبت <see cref="Amounts"/> لمنعها.</item>
/// </list>
/// ولذلك: مقياس ثابت 8، ومدى <c>numeric(19,8)</c>، ورفض بلا تقريب — نفس فلسفة
/// <see cref="Amounts"/> بأرقام العمود الفعلي.
///
/// <b>والثقافة الثابتة صراحةً في كل تحويل</b> (SPEC §8.1): تحت <c>ar-SA</c> يعطي
/// <c>3.75m.ToString("0.00000000")</c> فاصلة عربية <c>U+066B</c> داخل البايتات
/// المُجزَّأة.
/// </summary>
public static class Rates
{
    /// <summary>عدد الخانات العشرية القانوني لسعر الصرف. مطابق لـ<c>numeric(19,8)</c>.</summary>
    public const int Scale = 8;

    /// <summary>أكبر قيمة تسع في <c>numeric(19,8)</c>: 11 خانة صحيحة + 8 عشرية.</summary>
    public const decimal Max = 99_999_999_999.99999999m;

    /// <summary>أصغر قيمة تسع في <c>numeric(19,8)</c>.</summary>
    public const decimal Min = -99_999_999_999.99999999m;

    private const string Format = "0.00000000";

    /// <summary>
    /// يعيد نفس القيمة بمقياس 8 بالضبط، أو يرمي. <b>يُستدعى عند الحدّ ويُخزَّن ناتجه</b>،
    /// فيكون المخزَّن والمُجزَّأ شيئاً واحداً.
    /// </summary>
    public static decimal Normalize(decimal value, string? field = null)
    {
        Require(value, field);

        // لا تقريب هنا: تحقّقنا أعلاه أن المقياس الفعلي <= 8. الغرض رفع المقياس
        // المُعلَن إلى 8 بالضبط (1m -> 1.00000000m) ومحو إشارة الصفر السالب.
        var scaled = decimal.Round(value, Scale, MidpointRounding.ToEven);
        if (scaled == 0m) return 0.00000000m;
        return scaled + 0.00000000m;
    }

    /// <summary>يتحقّق أن القيمة صالحة للتجزئة كما هي، أو يرمي. لا يعدّل.</summary>
    public static decimal Require(decimal value, string? field = null)
    {
        if (value < Min || value > Max)
            throw new CanonicalizationException(CanonErrors.RateOutOfRange,
                $"سعر الصرف {value.ToString(CultureInfo.InvariantCulture)} خارج مدى numeric(19,8) " +
                $"[{Min.ToString(Format, CultureInfo.InvariantCulture)} .. {Max.ToString(Format, CultureInfo.InvariantCulture)}]. " +
                "قيمة لا تُخزَّن لا يجوز أن تُجزَّأ.", -1, field);

        var declared = (decimal.GetBits(value)[3] >> 16) & 0xFF;
        if (declared > Scale)
        {
            // المقياس المُعلَن قد يتجاوز المقياس الفعلي (1.500000000m مقياسه 9
            // وقيمته 1.5). الفحص الحاسم هو فقدان الدقّة لا الرقم المُعلَن.
            var truncated = decimal.Truncate(value * 100_000_000m) / 100_000_000m;
            if (truncated != value)
                throw new CanonicalizationException(CanonErrors.RateScaleExceeded,
                    $"سعر الصرف {value.ToString(CultureInfo.InvariantCulture)} يحمل أكثر من {Scale} خانات عشرية. " +
                    "لا يُقرَّب هنا: .NET تقرّب «نصف إلى الزوجي» وPostgreSQL تقرّب «نصف بعيداً عن الصفر»، " +
                    "فيختلف المخزَّن عن المُجزَّأ عند نقاط المنتصف.",
                    -1, field);
        }

        return value;
    }

    /// <summary>الشكل اللفظي القانوني. مثال: <c>1.00000000</c>، <c>3.75123456</c>.</summary>
    public static string Render(decimal value, string? field = null)
    {
        Require(value, field);
        var s = value.ToString(Format, CultureInfo.InvariantCulture);
        if (s is "-0.00000000") s = "0.00000000";
        return s;
    }

    /// <summary>يقرأ سعر صرف من نصّ <b>بالشكل القانوني وحده</b>.</summary>
    public static decimal ParseCanonical(string text, string? field = null)
    {
        ArgumentNullException.ThrowIfNull(text);

        if (!IsCanonicalLiteral(text))
            throw new CanonicalizationException(CanonErrors.RateBadLiteral,
                $"«{text}» ليس شكلاً قانونياً لسعر صرف. الشكل الوحيد المقبول هو -?\\d{{1,11}}\\.\\d{{8}} " +
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
        if (text.Length - i < 10) return false; // "0.00000000" على الأقل

        var dot = text.IndexOf('.');
        if (dot < 0 || dot != text.Length - 9) return false;

        var intDigits = dot - i;
        if (intDigits is < 1 or > 11) return false;
        if (intDigits > 1 && text[i] == '0') return false; // لا صفر بادئ

        for (var k = i; k < text.Length; k++)
        {
            if (k == dot) continue;
            if (text[k] is < '0' or > '9') return false;
        }

        if (text[0] == '-' && text.AsSpan(1).SequenceEqual("0.00000000")) return false; // لا صفر سالب
        return true;
    }
}
