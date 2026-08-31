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

/// <summary>
/// <b>جدول القرار — وهو موضع واحد لا غير.</b>
///
/// <para>«هل تسمح هذه الحالة بهذه النيّة؟» سؤالٌ إجابته سطران، ولذلك بالضبط
/// يُغري بالنسخ: مؤلّف وحدةٍ يكتب <c>if (state == Entitled)</c> في خدمته فيبدو
/// صحيحاً — وقد <b>أسقط <c>ReadOnly</c> من القراءة صامتاً</b>، أي قطع عن عميلٍ
/// انقطع سداده سجلَّه المحاسبي وهو يظنّ أنه يُنفِذ الاستحقاق.</para>
///
/// <para>فالقرار هنا وحده، وحارس معماري (القاعدة 6) يمنع أي شيفرة إنتاج خارج
/// حدّ الاستحقاق من أن تفرّع على <see cref="EntitlementState"/> أصلاً.</para>
///
/// <para><b>وهو نفس الجدول الذي في <c>Babel.Core.Entitlement.EntitlementRules</c>،
/// حرفاً بحرف بعد التوحيد.</b> التجميعتان لا تتراجعان — هذه بلا مرجعية إلى أي
/// مشروع بابل وبلا مرجعية إليها — فلا نوع مشترك يحملهما، والرابط بينهما مسحُ
/// مصدر: <c>Rule06_NothingBypassesEntitlement</c> يقرأ الجدولين من القرص ويُفشل
/// البناء إن اختلفا. <b>فلا تُحرَّر هذه الدالّة وحدها.</b>
/// (‏<c>docs/evidence/traps.md#fakh-the-decision-table-is-duplicated-inside-its-own-seam</c>)</para>
/// </summary>
public static class EntitlementRules
{
    /// <summary>هل تسمح هذه الحالة بهذه النيّة؟</summary>
    /// <param name="state">حالة الاستحقاق.</param>
    /// <param name="intent">نيّة الوصول.</param>
    /// <returns><c>true</c> إن كان الوصول مسموحاً.</returns>
    public static bool Allows(EntitlementState state, AccessIntent intent) => state switch
    {
        EntitlementState.Entitled => true,
        EntitlementState.ReadOnly => intent == AccessIntent.Read,
        _ => false,
    };
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
    : Exception(Describe(tenantCode, moduleCode, state, intent))
{
    /// <summary>رمز المستأجر الذي رُفض له الوصول.</summary>
    public string TenantCode { get; } = tenantCode;

    /// <summary>رمز الوحدة المرفوضة.</summary>
    public string ModuleCode { get; } = moduleCode;

    /// <summary>حالة الاستحقاق وقت الرفض.</summary>
    public EntitlementState State { get; } = state;

    /// <summary>نيّة الوصول التي رُفضت.</summary>
    public AccessIntent Intent { get; } = intent;

    /// <summary>رمز الرفض الثابت — نقطة الاعتماد البرمجية، لا النصّ.</summary>
    public string Code => _refusal.Code;

    /// <summary>سبب الرفض بالعربية، مصوغاً للمحاسب لا للمبرمج.</summary>
    public string MessageAr => _refusal.Ar;

    /// <summary>سبب الرفض بالإنجليزية — نفس السبب، لا ترجمة أفقر.</summary>
    public string MessageEn => _refusal.En;

    private readonly (string Code, string Ar, string En) _refusal =
        Refusal(tenantCode, moduleCode, state, intent);

    /// <summary>
    /// <b>تسمية الرفض في موضع واحد: الرمز والرسالتان معاً.</b>
    /// <para>وهي <b>لا تقرّر</b> شيئاً — القرار وقع في
    /// <see cref="EntitlementRules.Allows"/> قبل الوصول إلى هنا. غير أنها تقرن
    /// حالةً بنيّة، فلو كُتبت ثلاث مرّات (رمزاً ثم عربية ثم إنجليزية) لصار
    /// «‏<c>(ReadOnly, Write)</c> هو حالة الانقطاع» مكتوباً ثلاثاً، ولانحرفت
    /// إحداها عند إضافة حالة رابعة. مرّةً واحدة، وثلاثة مخرجات.</para>
    /// </summary>
    private static (string Code, string Ar, string En) Refusal(
        string t, string m, EntitlementState s, AccessIntent i) =>
        (s, i) switch
        {
            (EntitlementState.ReadOnly, AccessIntent.Write) =>
            (
                "entitlement.read_only",
                $"الوحدة «{m}» عند المستأجر «{t}» في حالة قراءة فقط لانقطاع الاشتراك: "
                + "القراءة والتقارير وتصدير بياناتك متاحة كاملةً، "
                + "وإنشاء المستندات والترحيل والعكس موقوفة حتى يُستأنف الاشتراك.",
                $"Module '{m}' is read-only for tenant '{t}' because the subscription has lapsed: "
                + "reading, reports and export of your own data remain fully available; "
                + "creating documents, posting and reversing are suspended until the subscription resumes."
            ),
            (EntitlementState.NotEntitled, _) =>
            (
                "entitlement.not_entitled",
                $"الوحدة «{m}» غير مشمولة باشتراك المستأجر «{t}» — لم تُشترَ.",
                $"Module '{m}' is not part of the subscription for tenant '{t}' - it was never purchased."
            ),
            _ =>
            (
                "entitlement.denied",
                $"وصول مرفوض للوحدة «{m}» عند المستأجر «{t}».",
                $"Access denied to module '{m}' for tenant '{t}'."
            )
        };

    // نفس شكل Babel.SharedKernel.Error.ToString: رمزٌ ثم الرسالتان.
    private static string Describe(string t, string m, EntitlementState s, AccessIntent i)
    {
        (string code, string ar, string en) = Refusal(t, m, s, i);
        return $"{code}: {ar} / {en}";
    }
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
