using System.Text.RegularExpressions;
using Babel.Ai.Voice;

namespace Babel.Ai.Boundary;

/// <summary>
/// <b>الأشكال السبعة — قائمة مغلقة، وترتيبها هو ترتيب الجُمل التي يقرؤها المستخدم.</b>
/// <para>
/// كلّها تُطابَق على النصّ <b>بعد الطيّ</b> (‏<see cref="AgentBoundaryText.Fold(string)"/>):
/// لا محارف غير مرئية، ولا تطويل، والأرقام في أنظمتها الأربعة كلّها صارت لاتينية.
/// </para>
/// <para>
/// <b>ولماذا يحمل هذا الملفّ شكل رقم التسجيل الضريبي بدل أن ينادي
/// <c>SaudiVatNumber</c>:</b> ذلك النوع <c>internal</c> داخل <c>Babel.Purchasing</c>،
/// و<c>Babel.Ai</c> <b>لا تستطيع</b> الإشارة إليه — القاعدة 3، وهي مفروضة ببناء لا
/// باتّفاق. فالتكرار هنا <b>مفروضٌ بالمعمارية لا مُختار</b>، وثمنه محروس: اختبار
/// <c>TheScrubberAgreesWithTheRepositoryValidators</c> يقرأ <c>SaudiVatNumber.Validate</c>
/// بالانعكاس ويُطابق حكمَه بحكم هذا الشكل على جدولٍ من الحالات — فأيّ انحرافٍ بين
/// التعريفين يُحمِّر البناء بدل أن يعيش صامتاً.
/// </para>
/// </summary>
public static partial class AgentIdentifierShapes
{
    /// <summary>رقم هوية أو إقامة: عشر خانات تبدأ بـ<c>1</c> (مواطن) أو <c>2</c> (مقيم).</summary>
    public static AgentIdentifierShape NationalId { get; } = new(
        "national_id", NationalIdPattern(), AgentBoundaryErrors.NationalId,
        tolerance: AgentSplitTolerance.Whitespace, isCatchAll: false);

    /// <summary>
    /// آيبان سعودي — <b>بالصيغة المجموعة أيضاً</b>: <c>SA03 8000 0000 6080 1016 7519</c>.
    /// <para>
    /// وهذه الصيغة هي ما يكتبه الناس فعلاً، وهي التي كان يفوتها الشكل القائم في
    /// <c>VoiceDisclosure</c> (‏<c>SA[0-9]{22}</c> المتّصل وحده). الثغرة كانت حقيقية،
    /// وقد أُغلقت بجعل ذلك الحارس يقرأ <b>هذا الشكل نفسه</b> بدل أن يحمل نسخةً ثانية.
    /// </para>
    /// </summary>
    public static AgentIdentifierShape Iban { get; } = new(
        "iban", IbanPattern(), AgentBoundaryErrors.Iban,
        tolerance: AgentSplitTolerance.AnchoredSeparators, isCatchAll: false);

    /// <summary>رقم تسجيل ضريبي: خمس عشرة خانة، أولاها <c>3</c> وآخرها <c>3</c>.</summary>
    public static AgentIdentifierShape Vat { get; } = new(
        "vat", VatPattern(), AgentBoundaryErrors.Vat,
        tolerance: AgentSplitTolerance.AnchoredSeparators, isCatchAll: false);

    /// <summary>
    /// عشر خانات ليست هويةً (‏<c>1</c>/<c>2</c>) ولا جوّالاً (‏<c>05</c>): سجلٌّ تجاري
    /// على الأرجح — <b>ولا يُقطع بذلك</b>، فكلا التفسيرين معرّف وكلاهما يُرفض.
    /// </summary>
    public static AgentIdentifierShape CommercialRegisterOrNationalId { get; } = new(
        "cr_or_national_id", CommercialRegisterPattern(), AgentBoundaryErrors.CommercialRegisterOrNationalId,
        tolerance: AgentSplitTolerance.Whitespace, isCatchAll: false);

    /// <summary>جوّال سعودي: <c>05…</c> أو <c>+966 5…</c> أو <c>00966 5…</c>.</summary>
    public static AgentIdentifierShape Phone { get; } = new(
        "phone", PhonePattern(), AgentBoundaryErrors.Phone,
        tolerance: AgentSplitTolerance.AnchoredSeparators, isCatchAll: false);

    /// <summary>
    /// الشبكة الأخيرة: تسع خانات متّصلة فأكثر لم يُطالب بها شكلٌ مُسمّى.
    /// <b>ولا تُعاد على النصّ الملموم</b> — والسبب مكتوب في
    /// <see cref="AgentSplitTolerance"/>.
    /// </summary>
    public static AgentIdentifierShape DigitRun { get; } = new(
        "digit_run", DigitRunPattern(), AgentBoundaryErrors.DigitRun,
        tolerance: AgentSplitTolerance.None, isCatchAll: true);

    /// <summary>
    /// القيمة المُقنَّعة — <c>••••1234</c> بقناع <c>VoiceDisclosure.MaskPrefix</c> نفسه.
    /// وهي الشكل السابع الذي لا يذكره جدول التصميم في صفٍّ مستقلّ وتذكره حاشيته.
    /// </summary>
    public static AgentIdentifierShape MaskedValue { get; } = new(
        "masked_value", BuildMaskPattern(), AgentBoundaryErrors.MaskedValue,
        tolerance: AgentSplitTolerance.None, isCatchAll: false);

    /// <summary>الأشكال بترتيبها المُلزِم: المُسمّاة الستّ، ثم القناع.</summary>
    public static IReadOnlyList<AgentIdentifierShape> All { get; } =
    [
        NationalId, Iban, Vat, CommercialRegisterOrNationalId, Phone, DigitRun, MaskedValue,
    ];

    /// <summary>شكلٌ بمفتاحه. مفتاحٌ غير معروف خطأ برمجي لا حالة تشغيل.</summary>
    /// <param name="key">المفتاح.</param>
    public static AgentIdentifierShape ByKey(string key) =>
        All.FirstOrDefault(shape => string.Equals(shape.Key, key, StringComparison.Ordinal))
        ?? throw new ArgumentOutOfRangeException(nameof(key), key, "لا شكل بهذا المفتاح. / No identifier shape carries this key.");

    // ── الأنماط ─────────────────────────────────────────────────────────────
    //
    // ‏**[0-9] مكتوبة صراحةً ولا يُستعمل \d**: في .NET يطابق \d كلّ أرقام يونيكود، ومنها
    // العربية-الهندية — أي أن الطيّ والنمط كانا سيتداخلان، ويصير المدى المُبلَّغ عنه
    // محسوباً على نصٍّ غير الذي فُحص. النمط يعمل على المطويّ وحده، ولاتينياً وحده.

    [GeneratedRegex(@"(?<![0-9])[12][0-9]{9}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex NationalIdPattern();

    [GeneratedRegex(@"(?<![A-Za-z0-9])SA[ -]?(?:[0-9][ -]?){22}(?![0-9])", RegexOptions.CultureInvariant | RegexOptions.IgnoreCase)]
    private static partial Regex IbanPattern();

    [GeneratedRegex(@"(?<![0-9])3[0-9]{13}3(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex VatPattern();

    [GeneratedRegex(@"(?<![0-9])(?![12])(?!05)[0-9]{10}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex CommercialRegisterPattern();

    [GeneratedRegex(@"(?<![0-9])(?:(?:\+?966|00966)[ -]?5[0-9]{8}|05[0-9]{8})(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex PhonePattern();

    [GeneratedRegex(@"(?<![0-9])[0-9]{9,}(?![0-9])", RegexOptions.CultureInvariant)]
    private static partial Regex DigitRunPattern();

    /// <summary>
    /// <b>نمط القناع يُبنى من <c>VoiceDisclosure.MaskPrefix</c> نفسه، ولا يُكتب حرفياً.</b>
    /// <para>
    /// كان النمط <c>•{4}</c> مكتوباً بيده بينما القناع يُنشئه ذلك الثابت — فكان الحارس
    /// <b>أحاديّ الاتجاه</b>: تغييرُ القناع يُحمِّر الاختبار، وتغييرُ النمط لا يُحمِّر
    /// شيئاً. وبناؤه من الثابت يجعل الاتجاهين واحداً: لا يبقى للنمط وجودٌ مستقلّ يُحرَّر.
    /// </para>
    /// <para>
    /// <b>ويحتمل الفراغ بين محارف القناع</b>: <c>«• • • •1234»</c> قناعٌ عند من يقرؤه،
    /// وكان يعبر نظيفاً. والنصّ الذي بينه حروفٌ — <c>«• بند أول • بند ثانٍ»</c> — لا
    /// يطابق، فالثمن معدوم.
    /// </para>
    /// </summary>
    private static Regex BuildMaskPattern()
    {
        string pattern = string.Join(
            @"\s*",
            VoiceDisclosure.MaskPrefix.Select(static unit => Regex.Escape(unit.ToString())));

        return new Regex(pattern, RegexOptions.CultureInvariant);
    }
}
