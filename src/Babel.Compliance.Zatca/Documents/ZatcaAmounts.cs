using System.Globalization;

namespace Babel.Compliance.Zatca.Documents;

/// <summary>
/// كتابة المبالغ داخل المستند. <b>ثقافة ثابتة دائماً وبلا استثناء.</b>
/// <para/>
/// السبب مقيس في هذه المنظومة نفسها: تحت <c>ar-SA</c> يعطي
/// <c>100.5m.ToString("0.00")</c> فاصلةً عربية <c>U+066B</c> ومحارف اتجاه غير مرئية،
/// فتخرج الفاتورة بمبلغ يبدو صحيحاً على الشاشة و<b>يفشل عند أي متحقّق</b>
/// (‏<c>docs/evidence/traps.md#fakh-local-decimal-format-in-hashed-bytes</c>).
/// <para/>
/// <b>والمقياس هنا خانتان، والمقياس القانوني للنظام أربع.</b> الفارق يُعالَج
/// <b>بالرفض لا بالتقريب</b>: التقريب قرار محاسبي يقع قبل هذا الحدّ. مُولِّد مستند
/// يقرّب مبلغاً هو مُولِّد يغيّر رقماً مالياً بصمت، وهو أسوأ ما يمكن أن يفعله هذا الحدّ.
/// </summary>
public static class ZatcaAmounts
{
    /// <summary>عدد الخانات العشرية داخل المستند.</summary>
    public static int Scale => ZatcaProfile.DocumentAmountScale;

    /// <summary>
    /// مُحدِّد التنسيق، مبنيّ مرة واحدة.
    /// <para/>
    /// وبناؤه هنا لا عند الاستدعاء مقصود: <c>value.ToString("0." + …, InvariantCulture)</c>
    /// يجعل ماسح الثقافة المصدري يقرأ <c>.ToString("0."</c> بلا مزوّد فيُبلّغ عن مخالفة.
    /// الماسح مُعجَمي بالضرورة — المُستكمَلة لا تترك في IL ما يميّز «بلا مزوّد» من «بمزوّد» —
    /// فالعلاج أن يختفي النصّ الحرفي من موضع النداء، لا أن يُعفى السطر.
    /// </summary>
    private static readonly string DocumentFormat = "0." + new string('0', ZatcaProfile.DocumentAmountScale);

    /// <summary>
    /// يكتب مبلغاً. يرفض حين تحمل القيمة معلومة لا تسعها خانتان — أي حين يكون التقريب
    /// <b>فقداناً</b> لا مجرّد إعادة تنسيق. <c>100.5000m</c> مقبولة (لا فقدان)،
    /// و<c>100.5050m</c> مرفوضة.
    /// </summary>
    public static string Render(decimal value, string fieldName)
    {
        if (decimal.Round(value, Scale, MidpointRounding.ToEven) != value)
        {
            throw new ZatcaDocumentException(FormattableString.Invariant($"الحقل «{fieldName}» يحمل {value.ToString(CultureInfo.InvariantCulture)} ") +
                FormattableString.Invariant($"ولا تسعه {Scale} خانتان عشريتان بلا فقدان. ") +
                "التقريب قرار محاسبي يقع قبل حدّ الالتزام، لا داخل مُولِّد المستند: " +
                "مُولِّدٌ يقرّب يغيّر رقماً مالياً بصمت. / " +
                FormattableString.Invariant($"field '{fieldName}' carries {value.ToString(CultureInfo.InvariantCulture)}; ") +
                FormattableString.Invariant($"it does not fit {Scale} decimals without loss. Rounding is an accounting decision taken before this boundary."));
        }

        return value.ToString(DocumentFormat, CultureInfo.InvariantCulture);
    }

    /// <summary>الكمية. مقياسها أوسع لأنها ليست مبلغاً — والخلط بينهما يقرّب كميات بصمت.</summary>
    public static string RenderQuantity(decimal value) =>
        value.ToString("0.######", CultureInfo.InvariantCulture);

    /// <summary>النسبة المئوية للضريبة، كما تُكتب في <c>cbc:Percent</c>.</summary>
    public static string RenderPercent(decimal ratePercent) =>
        ratePercent.ToString("0.##", CultureInfo.InvariantCulture);
}

/// <summary>عطل في بناء المستند. يخرج بصوت عالٍ، ولا يُنتج مستنداً ناقصاً.</summary>
public sealed class ZatcaDocumentException(string message) : Exception(message);
