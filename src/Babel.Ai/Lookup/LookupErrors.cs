using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Ai.Lookup;

/// <summary>أخطاء البحث المحلّي ومقابضه. الرمز نقطة الاعتماد، والعربية هي السجلّ.</summary>
public static class LookupErrors
{
    /// <summary>
    /// مِقبض غير موثَّق، أو مبتور، أو مكتوبٌ بكتابةٍ أخرى، أو من نسخةٍ أخرى.
    /// <b>والرمز واحد للأربعة عمداً</b>: رسالةٌ تفرّق بينها تُخبر من يجرّب أين أصاب.
    /// </summary>
    public static Error HandleNotSigned => new(
        "ai.lookup.handle_not_signed",
        "المِقبض غير موثَّق أو تالف — ولا يُقرأ منه حقلٌ واحد قبل أن تصحّ علامته.",
        "the handle is unauthenticated or malformed; no field is read before its tag verifies.");

    /// <summary>غرضٌ خارج المفردة المغلقة عند الإصدار — يُرفض ولا يُسكّ.</summary>
    public static Error HandlePurposeUndefined => new(
        "ai.lookup.handle_purpose_undefined",
        "غرضُ المِقبض خارج المفردة المغلقة. وغرضٌ لا اسم له لا يُقارَن بشيءٍ عند الاسترداد، "
        + "فيصير المِقبض صالحاً لكل باب.",
        "the handle purpose is outside the closed vocabulary; an unnamed purpose compares to nothing on redemption.");

    /// <summary>انتهت مدّة المِقبض.</summary>
    public static Error HandleExpired => new(
        "ai.lookup.handle_expired",
        "انتهت مدّة المِقبض. اطلب الاسم من جديد — ولا تُمدَّد مدّةٌ ولا تُبطَل قائمةٌ.",
        "the handle has expired; ask for the name again.");

    /// <summary>غرضٌ لا يطابق: معرّف سؤالٍ يُقدَّم في موضع كِيان مثلاً.</summary>
    /// <param name="expected">الغرض المطلوب.</param>
    /// <param name="actual">الغرض المكتوب داخل البايتات الموقَّعة.</param>
    public static Error HandlePurposeMismatch(LookupHandlePurpose expected, LookupHandlePurpose actual) => new(
        "ai.lookup.handle_purpose_mismatch",
        string.Format(
            CultureInfo.InvariantCulture,
            "المِقبض غرضه «{0}» ويُقدَّم في موضع «{1}» — والغرض داخل التوقيع فلا يُبدَّل.",
            actual,
            expected),
        string.Format(
            CultureInfo.InvariantCulture,
            "the handle's purpose is '{0}' but it is presented where '{1}' is required.",
            actual,
            expected));

    /// <summary>
    /// المِقبض صحيح التوقيع ولكنه من جلسةٍ أو منشأةٍ أو شركةٍ أخرى.
    /// <b>ولا تُذكر في الرسالة أيٌّ منها</b>: رسالةٌ تقول «هذا المِقبض لمنشأةٍ أخرى» تؤكّد
    /// وجود الصفّ هناك، وهو بذاته تسريب.
    /// </summary>
    public static Error HandleOutOfScope => new(
        "ai.lookup.handle_out_of_scope",
        "المِقبض لا يخصّ هذه الجلسة. اطلب الاسم من جديد.",
        "the handle does not belong to this session.");

    /// <summary>لا مصدر مسجَّل لهذا السجلّ — رفضٌ لا بحثٌ في سجلٍّ آخر.</summary>
    /// <param name="registerKey">مفتاح السجلّ المطلوب.</param>
    public static Error NoRegisterSource(string registerKey) => new(
        "ai.lookup.register_not_registered",
        "لا سجلّ أسماء مسجَّل باسم «" + registerKey + "» — ولا يُبحَث في سجلٍّ غيره.",
        "no name register is registered under '" + registerKey + "'.");

    /// <summary>نصّ بحثٍ فارغ. لا يُبحَث عن لا شيء فيُعاد كلّ شيء.</summary>
    public static Error EmptyText => new(
        "ai.lookup.text_empty",
        "لا نصّ للبحث. سؤالٌ بلا اسم يُطابق السجلّ كلّه.",
        "the lookup text is empty; a nameless question matches the whole register.");
}
