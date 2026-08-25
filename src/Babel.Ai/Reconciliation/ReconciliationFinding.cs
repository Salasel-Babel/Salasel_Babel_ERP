using Babel.SharedKernel;

namespace Babel.Ai.Reconciliation;

/// <summary>هل تمنع الملاحظة الترقية أم تُعرض فقط؟</summary>
public enum FindingSeverity
{
    /// <summary>تُعرض ولا تمنع.</summary>
    Advisory = 1,

    /// <summary>تمنع الترقية حتى تُحلّ.</summary>
    Blocking = 2,
}

/// <summary>
/// ملاحظة مطابقة حسابية. <b>تسمّي الرقم المختلِف ومقدار الاختلاف</b>.
/// <para>
/// «الأرقام لا تتّسق» جملة تُلقي العبء على الإنسان ليبحث عن الرقم بنفسه — وهو بالضبط
/// ما لا يفعله أحد على الشاشة الخمسين. ولذلك تحمل الملاحظة: أي حقل، وما المتوقَّع،
/// وما المرصود، وكم الفرق، و<b>أي الطرفين هو المشتبه به</b> — وهو الطرف الأضعف مصدراً.
/// </para>
/// </summary>
public sealed record ReconciliationFinding
{
    /// <summary>رمز الملاحظة الثابت — نقطة الاعتماد البرمجية.</summary>
    public required string Code { get; init; }

    /// <summary>مفتاح الحقل المشتبه به.</summary>
    public required string SuspectField { get; init; }

    /// <summary>الرسالة ثنائية اللغة، وفيها الرقمان والفرق.</summary>
    public required LocalizedName Message { get; init; }

    /// <summary>القيمة المتوقَّعة حسابياً.</summary>
    public required decimal Expected { get; init; }

    /// <summary>القيمة المرصودة.</summary>
    public required decimal Observed { get; init; }

    /// <summary>الفرق: المرصود ناقص المتوقَّع.</summary>
    public decimal Divergence => Observed - Expected;

    /// <summary>الأثر.</summary>
    public FindingSeverity Severity { get; init; } = FindingSeverity.Blocking;

    /// <inheritdoc />
    public override string ToString() => Code + ": " + Message.Arabic;
}
