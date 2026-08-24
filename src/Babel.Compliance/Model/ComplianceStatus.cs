using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Model;

/// <summary>
/// <b>حالة الالتزام — لا الوجود المحاسبي.</b> القيد مُرحَّل قبل أن تُنشأ أول قيمة هنا،
/// ولا قيمة من هذه القيم تغيّره. الرفض يغيّر هذه الحالة فقط.
/// <para/>
/// الأسماء المعروضة للمستخدم في 04-zatca §7: في الانتظار / مقبولة / مقبولة بملاحظات /
/// مرفوضة / فشل الإرسال — ويُضاف إليها حالتان تشغيليتان لا يراهما المستخدم إلا في لوحة المتابعة.
/// </summary>
public enum ComplianceStatus
{
    /// <summary>بُني المستند: حُجز العدّاد، وأُغلقت السلسلة، وجُمِّدت البايتات. لم يُرسل بعد.</summary>
    Built,

    /// <summary>في الطابور. للإبلاغ: داخل الصندوق الصادر. للمقاصة: بانتظار نداء حاجز.</summary>
    Queued,

    /// <summary>محاولة إرسال قائمة الآن. <b>مسجَّلة قبل النداء لا بعده</b> — وهي حالة الانتظار المرئية.</summary>
    Submitting,

    /// <summary>
    /// <b>انتهت محاولة بلا جواب.</b> لا نعرف هل نُفِّذ الإرسال أم لا.
    /// إعادة الإرسال العمياء ممنوعة من هنا؛ المسار يتحوّل إلى حسم.
    /// </summary>
    Ambiguous,

    Accepted,

    AcceptedWithWarnings,

    /// <summary>مرفوض من الجهة. القيد باقٍ كما هو؛ المستند معزول عن الإقرار وعن أعمار الذمم.</summary>
    Rejected,

    /// <summary>استُنفدت المحاولات دون أن يصل الطلب أصلاً. لم يصدر حكم من الجهة.</summary>
    TransportFailed,

    /// <summary>
    /// تعذّر حسم الغموض آلياً. <b>بند في طابور بشري</b> مع كل ما يحتاجه الإنسان للحسم.
    /// </summary>
    NeedsHumanReview
}

public static class ComplianceStatusText
{
    public static string Ar(ComplianceStatus s) => s switch
    {
        ComplianceStatus.Built => "مُنشأ",
        ComplianceStatus.Queued => "في الطابور",
        ComplianceStatus.Submitting => "قيد الإرسال",
        ComplianceStatus.Ambiguous => "غير محسوم",
        ComplianceStatus.Accepted => "مقبولة",
        ComplianceStatus.AcceptedWithWarnings => "مقبولة بملاحظات",
        ComplianceStatus.Rejected => "مرفوضة",
        ComplianceStatus.TransportFailed => "فشل الإرسال",
        ComplianceStatus.NeedsHumanReview => "تحتاج مراجعة بشرية",
        _ => s.ToString()
    };

    public static string En(ComplianceStatus s) => s switch
    {
        ComplianceStatus.Built => "built",
        ComplianceStatus.Queued => "queued",
        ComplianceStatus.Submitting => "submitting",
        ComplianceStatus.Ambiguous => "unresolved",
        ComplianceStatus.Accepted => "accepted",
        ComplianceStatus.AcceptedWithWarnings => "accepted with warnings",
        ComplianceStatus.Rejected => "rejected",
        ComplianceStatus.TransportFailed => "delivery failed",
        ComplianceStatus.NeedsHumanReview => "needs human review",
        _ => s.ToString()
    };
}

/// <summary>
/// جدول الانتقالات المسموحة. <b>صريح لا ضمني</b>: أي انتقال غير مذكور هنا يرمي،
/// كي لا يتسلّل مسار يقفز فوق تسجيل المحاولة أو فوق حالة الغموض.
/// </summary>
public static class ComplianceStatusMachine
{
    private static readonly Dictionary<ComplianceStatus, ComplianceStatus[]> Allowed = new()
    {
        [ComplianceStatus.Built] = [ComplianceStatus.Queued],
        [ComplianceStatus.Queued] = [ComplianceStatus.Submitting, ComplianceStatus.TransportFailed],
        [ComplianceStatus.Submitting] =
        [
            ComplianceStatus.Accepted,
            ComplianceStatus.AcceptedWithWarnings,
            ComplianceStatus.Rejected,
            ComplianceStatus.Ambiguous,
            ComplianceStatus.Queued,            // عطل «لم يُرسل» — إعادة المحاولة آمنة
            ComplianceStatus.TransportFailed
        ],
        [ComplianceStatus.Ambiguous] =
        [
            ComplianceStatus.Accepted,
            ComplianceStatus.AcceptedWithWarnings,
            ComplianceStatus.Rejected,
            ComplianceStatus.Submitting,        // إعادة إرسال محدودة ببايتات مطابقة، عبر الحارس وحده
            ComplianceStatus.Queued,            // فقط حين يؤكد استعلام الحالة إيجاباً أن الطلب لم يصل
            ComplianceStatus.NeedsHumanReview
        ],
        [ComplianceStatus.NeedsHumanReview] =
        [
            ComplianceStatus.Accepted,
            ComplianceStatus.AcceptedWithWarnings,
            ComplianceStatus.Rejected,
            ComplianceStatus.TransportFailed,
            ComplianceStatus.Submitting         // قرار بشري صريح بإعادة المحاولة
        ],
        [ComplianceStatus.Accepted] = [],
        [ComplianceStatus.AcceptedWithWarnings] = [],
        [ComplianceStatus.Rejected] = [],
        [ComplianceStatus.TransportFailed] = [ComplianceStatus.Queued]  // قرار بشري بإعادة الإدراج
    };

    public static bool IsTerminal(ComplianceStatus s) =>
        s is ComplianceStatus.Accepted or ComplianceStatus.AcceptedWithWarnings or ComplianceStatus.Rejected;

    public static bool IsSettled(ComplianceStatus s) => IsTerminal(s) || s == ComplianceStatus.TransportFailed;

    public static bool CanTransition(ComplianceStatus from, ComplianceStatus to) =>
        Allowed.TryGetValue(from, out var next) && next.Contains(to);

    public static void EnsureTransition(ComplianceStatus from, ComplianceStatus to)
    {
        if (!CanTransition(from, to))
            throw new InvalidComplianceTransitionException(
                $"انتقال حالة التزام غير مسموح: {ComplianceStatusText.Ar(from)} ← {ComplianceStatusText.Ar(to)} " +
                $"/ illegal compliance transition: {from} -> {to}");
    }

    public static IReadOnlyList<ComplianceStatus> AllowedFrom(ComplianceStatus from) =>
        Allowed.TryGetValue(from, out var next) ? next : [];
}

public sealed class InvalidComplianceTransitionException(string message) : Exception(message);
