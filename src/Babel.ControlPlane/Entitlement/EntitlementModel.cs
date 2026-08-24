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
public enum EntitlementState { NotEntitled = 0, ReadOnly = 1, Entitled = 2 }

public enum AccessIntent { Read, Write }

public sealed record EntitlementChange(string ModuleCode, EntitlementState NewState);

public sealed record EntitlementViolation(string ModuleCode, string MessageAr, string MessageEn);

/// <summary>
/// رفض عند <b>حدّ الخدمة</b>. إخفاء عنصر قائمة لا يمنع نداء واجهة برمجية،
/// فالحارس يعمل حيث تُنفَّذ العملية لا حيث تُعرض.
/// </summary>
public sealed class EntitlementDeniedException(
    string tenantCode, string moduleCode, EntitlementState state, AccessIntent intent)
    : Exception(BuildMessage(tenantCode, moduleCode, state, intent))
{
    public string TenantCode { get; } = tenantCode;
    public string ModuleCode { get; } = moduleCode;
    public EntitlementState State { get; } = state;
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

public sealed class IncoherentEntitlementSetException(IReadOnlyList<EntitlementViolation> violations)
    : Exception("مجموعة استحقاق غير متماسكة:\n  - "
                + string.Join("\n  - ", violations.Select(v => v.MessageAr)))
{
    public IReadOnlyList<EntitlementViolation> Violations { get; } = violations;
}
