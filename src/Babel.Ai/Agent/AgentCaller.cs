using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// مَن يتكلّم، وفي أي منشأة وشركة وجلسة، وبأي صلاحيات.
/// <para>
/// <b>ولا واحدة من الخمس تأتي من النموذج.</b> المنشأة من بيانات الاعتماد، والشركة من
/// مسار الطلب، والجلسة تُنشأ مربوطةً بالمستخدم والمنشأة، والصلاحيات من كتالوج الاستحقاق.
/// والنموذج لا يملك أن يسمّي شركةً ولا أن يوسّع مجموعة.
/// </para>
/// </summary>
/// <param name="Tenant">المنشأة.</param>
/// <param name="CompanyId">الشركة المفتوحة.</param>
/// <param name="SessionId">الجلسة — داخل بايتات كل مِقبض، فلا يُفكّ مِقبضُ محادثةٍ في أخرى.</param>
/// <param name="CompanyNameAr">
/// اسم الشركة بالعربية — يُرسَل رسالةَ نظامٍ <b>في وسط الرسائل</b> لا في نصّ النظام العلوي،
/// كي يبقى النصّ العلوي واحداً بايتاً ببايت لكل منشأة.
/// </param>
/// <param name="PermittedOperationIds">
/// العمليات المسموح بها لهذا المتكلّم. <b>مجموعة مغلقة</b>: مسار الوكيل مدخلٌ آخر إلى
/// الصلاحيات نفسها، لا باب أوسع منها — وهي صياغة <c>VoiceCaller.PermittedIntentIds</c> نفسها.
/// </param>
public sealed record AgentCaller(
    TenantId Tenant,
    Guid CompanyId,
    Guid SessionId,
    string CompanyNameAr,
    IReadOnlySet<string> PermittedOperationIds);
