namespace Babel.Core.Entitlement;

/// <summary>
/// <b>جدول القرار في النواة — وهو موضع واحد لا غير.</b>
///
/// <para>«هل تسمح هذه الحالة بهذا الوصول؟» سؤالٌ إجابته سطران، ولذلك بالضبط
/// يُغري بالنسخ. وكان مكتوباً هنا <b>مرّتين</b> فعلاً — في
/// <see cref="EntitlementEnforcer"/> وفي <see cref="EntitlementSet"/> — ولكلٍّ
/// نسخته من «‏<c>ReadOnly</c> تعني القراءة وحدها». وتعديلُ إحداهما وسهوُ الأخرى
/// يمنح مستأجراً منقطع الاشتراك <b>كتابةً</b> من الطريق الذي لم يُحدَّث، وهو
/// بعينه العطل الذي وُجد
/// <see href="../../../docs/decisions/ADR-0034-a-lapsed-subscription-degrades-to-read-only-it-never-revokes-the-record.md">ADR-0034</see>
/// لمنعه — مُعاداً مجلداً واحداً إلى الداخل
/// (‏<c>docs/evidence/traps.md#fakh-the-decision-table-is-duplicated-inside-its-own-seam</c>).</para>
///
/// <para><b>وهو نفس الجدول الذي في مستوى التحكّم، حرفاً بحرف بعد التوحيد.</b>
/// التجميعتان لا تتراجعان — <c>Babel.ControlPlane</c> بلا مرجعية إلى أي مشروع
/// بابل وبلا مرجعية إليه — فلا نوع مشترك يحملهما. والرابط بينهما مسحُ مصدر:
/// ‏<c>Rule06_NothingBypassesEntitlement</c> يقرأ الجدولين من القرص ويُفشل البناء
/// إن اختلفا. <b>فلا تُحرَّر هذه الدالّة وحدها.</b></para>
/// </summary>
public static class EntitlementRules
{
    /// <summary>هل تسمح هذه الحالة بهذا الوصول؟</summary>
    /// <param name="state">حالة الاستحقاق.</param>
    /// <param name="access">الوصول المطلوب.</param>
    /// <returns><c>true</c> إن كان الوصول مسموحاً.</returns>
    public static bool Allows(EntitlementState state, EntitlementAccess access) => state switch
    {
        EntitlementState.Entitled => true,
        EntitlementState.ReadOnly => access == EntitlementAccess.Read,
        _ => false,
    };
}
