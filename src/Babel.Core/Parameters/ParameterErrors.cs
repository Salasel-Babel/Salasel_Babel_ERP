using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Core.Parameters;

/// <summary>
/// أخطاء خدمة المعامِلات. كل خطأ برمز ثابت ورسالتين — <b>والرسالة تقول ما يفعله
/// القارئ</b>، لا أنّ شيئاً ما وقع.
/// </summary>
public static class ParameterErrors
{
    /// <summary>مجموعةٌ ليست في الفهرس — والفهرس مغلق عمداً.</summary>
    /// <param name="setCode">الرمز المطلوب.</param>
    public static Error SetUnknown(string setCode) => new(
        "core.parameter_set_unknown",
        "لا مجموعة معامِلات بالرمز «" + setCode + "» في الفهرس. والفهرس مغلق: شكلُ المجموعة "
        + "ومفاتيحُها معلَنة في ParameterCatalogue، والقيم وحدها بيانات. أضِف التعريف قبل الإيداع.",
        "No parameter set with code '" + setCode + "' is in the catalogue. The catalogue is closed: the shape of "
        + "a set and its keys are declared in ParameterCatalogue, and only the values are data. Declare it first.");

    /// <summary>
    /// <b>الرفض الحاكم:</b> لا إصدار — لا تجاوزُ مستأجرٍ ولا افتراضُ منصّة — يغطّي هذا
    /// التاريخ. ولا قيمة تُخترع عند الغياب.
    /// </summary>
    /// <param name="setCode">المجموعة.</param>
    /// <param name="on">التاريخ.</param>
    public static Error SetMissing(string setCode, DateOnly on) => new(
        "core.parameters_missing",
        "لا إصدار من مجموعة المعامِلات «" + setCode + "» يغطّي " + Date(on)
        + " — لا تجاوزٌ لهذه المنشأة ولا افتراضٌ للمنصّة. ولا قيمة تُخترع هنا: أودِع إصداراً "
        + "بتاريخ سريانه ومعتمِده ومصدره من شاشة /setup/parameters، ثم أعد المحاولة.",
        "No version of parameter set '" + setCode + "' covers " + Date(on)
        + " — neither a tenant override nor a platform default. No value is invented here: deposit a version with "
        + "its effective date, its approver and its source from /setup/parameters, then retry.");

    /// <summary>الإيداع لا يحمل مفاتيح المجموعة كلَّها — أو يحمل ما ليس منها.</summary>
    /// <param name="setCode">المجموعة.</param>
    /// <param name="missing">ما نقص.</param>
    /// <param name="extra">ما زاد.</param>
    public static Error KeysDoNotMatchTheSet(string setCode, IReadOnlyList<string> missing, IReadOnlyList<string> extra)
    {
        ArgumentNullException.ThrowIfNull(missing);
        ArgumentNullException.ThrowIfNull(extra);

        string missingText = missing.Count == 0 ? "—" : string.Join("، ", missing);
        string extraText = extra.Count == 0 ? "—" : string.Join("، ", extra);

        return new Error(
            "core.parameter_keys_incomplete",
            "إيداع مجموعة «" + setCode + "» ناقصٌ أو زائد. الناقص: " + missingText + ". الزائد: " + extraText
            + ". والمجموعة تُودَع كاملةً لأن قيمها تسري معاً — وإيداعٌ جزئي يُنتج خليطاً من "
            + "إصدارين لم يعتمده أحد.",
            "The deposit of set '" + setCode + "' is incomplete or has extra keys. Missing: " + missingText
            + ". Extra: " + extraText + ". A set is deposited whole because its values take effect together — a "
            + "partial deposit produces a mixture of two versions that nobody approved.");
    }

    /// <summary>
    /// <b>الحارس الذي يمنع تضاعف الوعاء خمس عشرة مرّة.</b> نسبةٌ كُتبت مئويةً بدل كسر.
    /// </summary>
    /// <param name="key">المفتاح.</param>
    /// <param name="value">القيمة كما وردت.</param>
    public static Error RateLooksLikeAPercentage(string key, decimal value) => new(
        "core.parameter_rate_looks_like_a_percentage",
        "النسبة «" + key + "» وصلت بالقيمة " + Number(value) + "، وهي أكبر من واحد. والنسب في هذا "
        + "النظام **كسورٌ عشرية لا مئويات**: خمسة عشر بالمئة تُكتب 0.15 لا 15. "
        + "وقيمةٌ تُكتب 15 بدل 0.15 تضاعف الوعاء خمس عشرة مرّة — فتُرفض هنا ولا تُصحَّح صامتةً، "
        + "لأن التصحيح الصامت يجعل من كتبها يظنّ أنه أودع ما لم يُودِع.",
        "Rate '" + key + "' arrived as " + Number(value) + ", which is greater than one. Rates in this system are "
        + "decimal fractions, not percentages: fifteen percent is 0.15, never 15. A value written 15 instead of "
        + "0.15 multiplies the base fifteenfold — so it is refused here and never silently corrected, because a "
        + "silent correction leaves the depositor believing they deposited what they did not.");

    /// <summary>نسبة سالبة أو أكبر من واحد بلا أن تبدو مئوية.</summary>
    /// <param name="key">المفتاح.</param>
    /// <param name="value">القيمة.</param>
    public static Error RateOutOfRange(string key, decimal value) => new(
        "core.parameter_rate_out_of_range",
        "النسبة «" + key + "» وصلت بالقيمة " + Number(value) + "، والنسبة كسرٌ عشري بين صفر وواحد.",
        "Rate '" + key + "' arrived as " + Number(value) + "; a rate is a decimal fraction between zero and one.");

    /// <summary>مبلغ أو عدد سالب.</summary>
    /// <param name="key">المفتاح.</param>
    /// <param name="value">القيمة.</param>
    public static Error NegativeValue(string key, decimal value) => new(
        "core.parameter_negative_value",
        "القيمة «" + key + "» سالبة: " + Number(value) + ".",
        "Value '" + key + "' is negative: " + Number(value) + ".");

    /// <summary>خانات عشرية أكثر ممّا يحتمله صنف القيمة.</summary>
    /// <param name="key">المفتاح.</param>
    /// <param name="value">القيمة.</param>
    /// <param name="maximumScale">أقصى مقياس مسموح.</param>
    public static Error ScaleTooFine(string key, decimal value, int maximumScale) => new(
        "core.parameter_scale_too_fine",
        "القيمة «" + key + "» = " + Number(value) + " أدقّ من مقياس صنفها ("
        + maximumScale.ToString(CultureInfo.InvariantCulture) + " خانة). والقصّ الصامت يُغيّر رقماً "
        + "أودعه إنسان، فيُرفض بدله.",
        "Value '" + key + "' = " + Number(value) + " is finer than its kind's scale ("
        + maximumScale.ToString(CultureInfo.InvariantCulture) + " places). Silent truncation would change a number "
        + "a human deposited, so it is refused instead.");

    /// <summary>إصدارٌ ثانٍ على (المستوى · المجموعة · تاريخ السريان) نفسه.</summary>
    /// <param name="setCode">المجموعة.</param>
    /// <param name="effectiveFrom">تاريخ السريان.</param>
    public static Error DuplicateVersion(string setCode, DateOnly effectiveFrom) => new(
        "core.parameter_version_duplicate",
        "للمجموعة «" + setCode + "» إصدارٌ قائم يسري من " + Date(effectiveFrom)
        + " على هذا المستوى. والإصدار لا يُعدَّل في مكانه: غيّر تاريخ السريان، فالماضي لا يُعاد كتابته.",
        "Set '" + setCode + "' already has a version effective from " + Date(effectiveFrom)
        + " at this level. A version is never edited in place: change the effective date — the past is not rewritten.");

    /// <summary>اعتمادٌ بلا اسم إنسان.</summary>
    public static Error ApproverIsNotAHuman() => new(
        "core.parameter_approver_missing",
        "الإصدار المُودَع من منشأة يحمل اسم **من اعتمده** — إنسان، لا نظام. "
        + "وافتراضُ المنصّة وحده هو الذي لا يحمل اسماً، وهو لا يُودَع من هنا أصلاً.",
        "A version deposited by a tenant carries the name of the human who approved it — a human, never a system. "
        + "Only the platform default carries no name, and it is not deposited through this door at all.");

    /// <summary>إيداعٌ بلا مرجع مصدر.</summary>
    public static Error SourceRefMissing() => new(
        "core.parameter_source_ref_missing",
        "الإصدار بلا مرجع مصدر. والمرجع نصٌّ يقرؤه مراجع بعد سنتين ليعرف من أين جاء الرقم — "
        + "وغيابه يجعل الرقم مجهول المنشأ ولو كان صحيحاً.",
        "The version has no source reference. The reference is text an auditor reads two years later to learn "
        + "where the number came from — without it the number has no provenance even when it is right.");

    /// <summary>حالة اعتماد لا تصلح لإيداعٍ من منشأة.</summary>
    /// <param name="token">الحالة كما وصلت.</param>
    public static Error ApprovalNotDepositable(string token) => new(
        "core.parameter_approval_not_depositable",
        "حالة الاعتماد «" + token + "» لا تُودَع من هذا الباب. المنشأة تُودِع باعتمادها أو بتوقيع "
        + "محاسبها القانوني؛ و«افتراضُ منصّة» يُشحن مع المنتج ولا يُكتب من هنا.",
        "Approval state '" + token + "' is not depositable through this door. A tenant deposits with its own "
        + "approval or with its auditor's signature; a platform default ships with the product and is never "
        + "written here.");

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Number(decimal value) => value.ToString(CultureInfo.InvariantCulture);
}
