namespace Babel.ControlPlane.Entitlement;

/// <summary>
/// الحالات الثلاث — ولا رابعة (ADR-0014).
/// <list type="bullet">
/// <item><b>NotEntitled</b> — لم تُشترَ قط. مخفيّة تماماً.</item>
/// <item><b>Entitled</b> — قراءة وكتابة.</item>
/// <item><b>ReadOnly</b> — اشتُريت ثم انقطع السداد: قراءة وتقارير وتصدير
/// كاملة، بلا مستند جديد وبلا ترحيل جديد.</item>
/// </list>
/// <para><b>لماذا ReadOnly:</b> عميل توقّف عن سداد وحدة الرواتب ما يزال
/// مضطراً إلى إخراج قيد رواتب السنة الماضية لنزاع عمالي — ولا يجوز حجب
/// سجلاته المحاسبية عنه. الحالتان فقط (مستحق/غير مستحق) تجعلان كل إلغاء
/// اشتراك <b>قطعاً لوصول العميل إلى بياناته هو</b>.</para>
/// </summary>
public enum EntitlementState
{
    /// <summary>لم تُشترَ قط: مخفيّة من القائمة، ومرفوضة عند الخدمة.</summary>
    NotEntitled = 0,

    /// <summary>اشتُريت ثم انقطع السداد: قراءة وتقارير وتصدير كاملة، بلا مستند جديد وبلا ترحيل.</summary>
    ReadOnly = 1,

    /// <summary>مستحقّة: قراءة وكتابة.</summary>
    Entitled = 2
}

/// <summary>نيّة الوصول التي يُقاس عليها الاستحقاق.</summary>
public enum AccessIntent
{
    /// <summary>قراءة أو تقرير أو تصدير — متاحة في <c>Entitled</c> و<c>ReadOnly</c> معاً.</summary>
    Read,

    /// <summary>إنشاء مستند أو ترحيل قيد — متاحة في <c>Entitled</c> وحدها.</summary>
    Write
}

/// <summary>تغيير استحقاق مطلوب على وحدة واحدة. تُقدَّم المجموعة كاملةً وتُقبل أو تُرفض كاملةً.</summary>
/// <param name="ModuleCode">رمز الوحدة.</param>
/// <param name="NewState">الحالة المطلوبة.</param>
public sealed record EntitlementChange(string ModuleCode, EntitlementState NewState);

/// <summary>مخالفة واحدة في مجموعة استحقاق مطلوبة — تُعرَض بلغتين ولا تُصلَح صامتةً.</summary>
/// <param name="ModuleCode">الوحدة المخالِفة.</param>
/// <param name="MessageAr">وصف المخالفة بالعربية.</param>
/// <param name="MessageEn">وصف المخالفة بالإنجليزية.</param>
public sealed record EntitlementViolation(string ModuleCode, string MessageAr, string MessageEn);

/// <summary>
/// رفض عند <b>حدّ الخدمة</b>. إخفاء عنصر قائمة لا يمنع نداء واجهة برمجية،
/// فالحارس يعمل حيث تُنفَّذ العملية لا حيث تُعرض.
/// </summary>
/// <param name="tenantCode">رمز المستأجر.</param>
/// <param name="moduleCode">رمز الوحدة المرفوضة.</param>
/// <param name="state">حالة الاستحقاق وقت الرفض.</param>
/// <param name="intent">نيّة الوصول التي رُفضت.</param>
public sealed class EntitlementDeniedException(
    string tenantCode, string moduleCode, EntitlementState state, AccessIntent intent)
    : Exception(BuildMessage(tenantCode, moduleCode, state, intent))
{
    /// <summary>رمز المستأجر الذي رُفض له الوصول.</summary>
    public string TenantCode { get; } = tenantCode;

    /// <summary>رمز الوحدة المرفوضة.</summary>
    public string ModuleCode { get; } = moduleCode;

    /// <summary>حالة الاستحقاق وقت الرفض.</summary>
    public EntitlementState State { get; } = state;

    /// <summary>نيّة الوصول التي رُفضت.</summary>
    public AccessIntent Intent { get; } = intent;

    private static string BuildMessage(string t, string m, EntitlementState s, AccessIntent i) =>
        s switch
        {
            EntitlementState.NotEntitled =>
                $"الوحدة «{m}» غير مستحقّة للمستأجر «{t}» — لم تُشترَ.",
            EntitlementState.ReadOnly when i == AccessIntent.Write =>
                $"الوحدة «{m}» في حالة قراءة فقط للمستأجر «{t}»: "
                + "القراءة والتقارير والتصدير متاحة، وإنشاء المستندات والترحيل موقوف.",
            _ => $"وصول مرفوض للوحدة «{m}» عند المستأجر «{t}»."
        };
}

/// <summary>
/// تُرفَض المجموعة <b>كاملةً</b> ولا تُصلَح جزئياً: إصلاح صامت يُنتج اشتراكاً
/// لم يطلبه أحد ولم يوافق عليه أحد، وتُفوتَر عليه وحدة لم تُشترَ.
/// </summary>
/// <param name="violations">كل المخالفات المكتشَفة، لا أوّلها.</param>
public sealed class IncoherentEntitlementSetException(IReadOnlyList<EntitlementViolation> violations)
    : Exception("مجموعة استحقاق غير متماسكة:\n  - "
                + string.Join("\n  - ", violations.Select(v => v.MessageAr)))
{
    /// <summary>كل المخالفات التي رُفضت المجموعة بسببها.</summary>
    public IReadOnlyList<EntitlementViolation> Violations { get; } = violations;
}
