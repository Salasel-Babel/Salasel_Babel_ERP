namespace Babel.Projects.Application;

/// <summary>
/// بندٌ معلَّق: رمزه الثابت وعنوانه بلغتين والموضع الذي يحمل حجّته.
/// </summary>
/// <param name="Code">الرمز الثابت — وهو ما يُكتب في <c>projects.contract_policy.ItemCode</c>.</param>
/// <param name="TitleAr">عنوان البند بالعربية، كما يظهر في رسالة الرفض.</param>
/// <param name="TitleEn">عنوانه بالإنجليزية.</param>
/// <param name="SourceRef">الموضع الذي يحمل السؤال كاملاً بخياراته.</param>
public sealed record PendingPolicyItem(string Code, string TitleAr, string TitleEn, string SourceRef);

/// <summary>
/// <b>البنود التي يجب أن يعتمدها محاسب قبل أن يُرحَّل أول مستخلص — مُعلَنةً بأسمائها.</b>
/// <para>
/// <b>ولماذا قائمةٌ لا سلسلةُ <c>if</c> متناثرة:</b> لأن الرفض يجب أن <b>يسمّي البند</b>
/// لا أن يقول «ينقص إعداد». ولأن من يضيف بنداً معلَّقاً سادساً يضيفه هنا فيدخل الرفض
/// تلقائياً في كل مسارٍ يستشير هذه القائمة — بدل أن يُضاف فحصٌ سادس يُنسى في المسار الثاني.
/// </para>
/// <para>
/// <b>ولا قيمة افتراضية لأيٍّ منها في هذه الشيفرة، ولا في اختبار.</b> جدول
/// <c>projects.contract_policy</c> يُبنى <b>فارغاً</b> ولا باب على السطح المنشور يكتب
/// فيه: هذه إجاباتُ محاسبٍ لا إعداداتُ مستخدم. والوحدة تقرأ الصفوف وترفض ما نقص.
/// </para>
/// <para>
/// <b>وما الذي تعطّله هذه القائمة فعلاً:</b> ترحيلَ مستخلص العميل ومستخلص الباطن —
/// وبالتبعية الإفراجَ عن المحتجز وتحصيلَه، لأن حركات المحتجز تُشتقّ من المُرحَّل وحده.
/// وما لا تعطّله: تسجيلَ المشاريع والعقود وجداول الكميات وأوامر التغيير والضمانات
/// والمقاولين وعقودهم، ومسوّدات المستخلصات كلها، <b>وترحيل دفعة المقاول المقدمة</b>
/// — فمبلغها يُدخله المستخدم ولا يشتقّه حاسب، فلا بند فيها معلَّق.
/// </para>
/// </summary>
internal static class PendingPolicyItems
{
    /// <summary>وعاء نسبة المحتجز وقاعدة استرداد الدفعة المقدمة.</summary>
    public const string RetentionBaseAndAdvanceRecovery = "retention_base_and_advance_recovery";

    /// <summary>مستوى التصنيف الضريبي، ومن يحسب مبلغ الضريبة.</summary>
    public const string TaxClassificationLevel = "tax_classification_level";

    /// <summary>موضع التقريب على المستخلص التراكمي.</summary>
    public const string RoundingPolicy = "rounding_policy";

    /// <summary>ظهور المحتجز المدين في مطابقة العميل — تعريف الأثر على نقطة الضبط.</summary>
    public const string RetentionControlEffect = "retention_control_effect";

    /// <summary>
    /// البنود الأربعة بترتيبها الثابت. مرتَّبة صراحةً كي تكون رسالة الرفض نفسها في كل
    /// تشغيل — رسالةٌ يتغيّر ترتيب بنودها تُقرأ تغيّراً في الحال وليست كذلك.
    /// </summary>
    public static IReadOnlyList<PendingPolicyItem> All { get; } =
    [
        new(RetentionBaseAndAdvanceRecovery,
            "وعاء نسبة المحتجز (صافٍ قبل الضريبة أم إجمالي شاملها) وقاعدة استرداد الدفعة المقدمة",
            "The base the retention rate applies to (net before tax or gross including it) and the advance recovery rule",
            "posting-matrix.md §5.1 — اشتقاق retention وadvance_recovery"),

        new(TaxClassificationLevel,
            "مستوى التصنيف الضريبي: بند جدول الكميات أم العقد، ومن يحسب مبلغ الضريبة",
            "The tax classification level: the BOQ line or the contract, and who computes the tax amount",
            "posting-matrix.md §5.1 — «حسب التصنيف الضريبي لبنود العقد»"),

        new(RoundingPolicy,
            "موضع التقريب: على امتداد السطر ثم يُجمَع، أم على مجموع الفترة، أم على التراكمي ثم يُطرَح السابق",
            "Where rounding falls: per line then summed, on the period total, or on the cumulative then less the prior",
            "‏Money يفرض المقياس أربعاً ويرفض ما زاد — «التقريب يجب أن يكون قراراً محاسبياً صريحاً»"),

        new(RetentionControlEffect,
            "ظهور المحتجز المدين في مطابقة العميل — وهو ما يُعرِّف الأثر المكتوب على صفّ محاولة الترحيل",
            "Whether debit retention appears in the customer reconciliation — which defines the effect written on the posting attempt row",
            "تناقضٌ قائم بين accounts.csv وsubledger-types.csv على الحساب الضابط للمحتجز"),
    ];
}
