using System.Text.RegularExpressions;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>الحدّ بين ما يبلغه الصوت وما لا يبلغه — قاعدةٌ على شكل المعرّف، لا قائمةٌ بأسماء النيّات.</b>
/// <para>
/// <b>القاعدة:</b> يبلغ الصوت <b>كل عملية إنشاء مسوّدة</b>، ولا يبلغ <b>عملية ترحيلٍ
/// واحدة</b>، ولا توقيعاً ولا اعتماداً ولا إنهاءً ولا عكساً.
/// </para>
/// <para>
/// <b>ولماذا هي آمنة في هذا النظام بعينه:</b> المسوّدة <b>لا تمسّ الدفتر</b>. والدفتر هنا
/// يُضاف إليه فقط — <c>REVOKE UPDATE, DELETE</c> على دور التطبيق، وسلسلةُ بصمات
/// SHA‑256، وعدّادٌ بلا فجوات. فمسوّدةٌ خاطئة تُقرأ على الشاشة وتُلقى ولا تُكلّف شيئاً؛
/// وقيدٌ خاطئ يُكلّف <b>قيداً عاكساً وجيلاً ثانياً يبقيان في السجلّ إلى الأبد</b>.
/// والصوت كلّه على الجانب الرخيص من هذا اللاتماثل — وهذا هو الفرق الذي أضاعه المعيار
/// السابق حين خلط <b>الإدخال بالصوت</b> بـ<b>التنفيذ بالصوت</b>.
/// </para>
/// <para>
/// <b>وبوابة التأكيد لم تُرفَع بل انتقلت:</b> كانت تحرس الجملة المنطوقة، وصارت تحرس
/// <b>الالتزام</b> — تظهر المسوّدة على الشاشة، ويقرؤها إنسان، ويكون الترحيل فعلاً
/// بصرياً يدوياً. <b>ولا «نعم» منطوقة تُرحّل شيئاً أبداً.</b>
/// </para>
/// <para>
/// <b>ولماذا شكل المعرّف لا قائمةُ نيّات:</b> حارسٌ يعدّ نيّات اليوم لا يمنع خطأ الغد.
/// وهذا الحارس يفحص <b>الفعل</b> الذي يبدأ به معرّف العملية المنشورة: فعلٌ في قائمة
/// الممنوع يُسقط البناء باسمه، وفعلٌ <b>خارج قائمة المسموح</b> يُسقطه أيضاً — فعمليةٌ
/// تُنشر غداً بفعلٍ لم يُصنَّف لا تصل الصوت حتى يصنّفها إنسان.
/// </para>
/// </summary>
public static partial class VoiceOperationGuard
{
    /// <summary>شكل معرّف العملية المنشورة: حرفٌ صغير ثم حروفٌ وأرقام (‏camelCase).</summary>
    [GeneratedRegex("^[a-z][A-Za-z0-9]*$", RegexOptions.CultureInvariant)]
    private static partial Regex OperationShape();

    /// <summary>الفعل الأوّل في المعرّف — المقطع الصغير الذي يبدأ به.</summary>
    [GeneratedRegex("^[a-z]+", RegexOptions.CultureInvariant)]
    private static partial Regex LeadingVerb();

    /// <summary>
    /// <b>الأفعال الممنوعة على المسار المنطوق، ولكلٍّ سببُه.</b> الترحيل أوّلها،
    /// ومعه التوقيع والاعتماد والإنهاء والعكس والإبطال — <b>وكلّها أثرٌ لا يُعكَس
    /// بلا أثرٍ ثانٍ يبقى في السجلّ</b>.
    /// </summary>
    public static IReadOnlyDictionary<string, string> ForbiddenVerbs { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["post"] = "ترحيلٌ إلى الدفتر — وأثرُه لا يُمحى بل يُعكَس بقيدٍ ثانٍ يبقى",
            ["activate"] = "تفعيلٌ يقوم مقام التوقيع — وعقدٌ وُقّع لا يُسحب توقيعه",
            ["sign"] = "توقيعُ عقد — وقراءتُه على الطرفين هي الغرض لا اختصارُه",
            ["approve"] = "اعتمادٌ — والاعتماد قرارُ إنسانٍ يُتَّخذ بالعين واليد",
            ["terminate"] = "إنهاءُ خدمةٍ أو علاقة — قرارٌ يُغيّر حياة إنسان ولا يُتراجَع عنه",
            ["revoke"] = "إبطالٌ — أثرٌ يُسقط حقّاً قائماً",
            ["reverse"] = "عكسُ قيد — وهو نفسه قيدٌ يبقى في الدفتر",
            ["lapse"] = "إسقاطُ اشتراك — أثرٌ يُغلق باباً على مستخدمين",
            ["delete"] = "حذف — ولا حذف في هذا النظام أصلاً",
            ["forfeit"] = "مصادرة — قرارٌ خلافي بين طرفين",
            ["void"] = "إبطالُ مستند — أثرٌ لا يُعكَس بلا أثرٍ ثانٍ",
        };

    /// <summary>
    /// <b>الأفعال المسموحة — قائمةٌ مغلقة.</b> إنشاءُ مسوّدة، وإنشاءُ مستندٍ لا يُرحَّل،
    /// وقراءةٌ لا تكتب شيئاً. <b>وما ليس هنا مرفوضٌ حتى يصنّفه إنسان</b>: عمليةٌ تُنشر
    /// غداً بفعلٍ جديد لا تبلغ الصوت بالصدفة.
    /// </summary>
    public static IReadOnlySet<string> PermittedVerbs { get; } =
        new HashSet<string>(StringComparer.Ordinal)
        {
            "draft", "create", "add", "record", "read", "list", "reconcile", "verify",
        };

    /// <summary>
    /// يفحص معرّف عملية. يعيد <b>سبب الرفض بالعربية</b>، أو <c>null</c> إن كان مسموحاً.
    /// </summary>
    /// <param name="operationId">معرّف العملية كما هو في العقد المنشور.</param>
    public static string? Refuse(string? operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId) || !OperationShape().IsMatch(operationId))
        {
            return "ليس على شكل معرّف عملية منشورة (‏camelCase)";
        }

        string verb = LeadingVerb().Match(operationId).Value;

        return ForbiddenVerbs.TryGetValue(verb, out string? why)
            ? why
            : PermittedVerbs.Contains(verb)
                ? null
                : "فعلٌ غير مصنَّف «" + verb + "» — ولا يبلغ الصوت عمليةً لم يصنّفها إنسان";
    }

    /// <summary>هل يبلغها الصوت؟</summary>
    /// <param name="operationId">معرّف العملية.</param>
    public static bool Permits(string? operationId) => Refuse(operationId) is null;
}
