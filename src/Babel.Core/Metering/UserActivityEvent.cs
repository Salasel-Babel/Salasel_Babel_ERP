using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Core.Metering;

/// <summary>
/// نشاط على محور المستخدم — المحور الثاني للتسعير.
/// «المستخدم الفعّال» تعريف تجاري يُحسم لاحقاً؛ ما لا يُحسم لاحقاً هو الالتقاط:
/// البيانات التاريخية التي لم تُلتقط لا تُستعاد.
/// </summary>
/// <param name="Tenant">المستأجر.</param>
/// <param name="User">المستخدم.</param>
/// <param name="Module">الوحدة التي جرى النشاط فيها.</param>
/// <param name="Activity">اسم النشاط.</param>
/// <param name="OccurredAt">لحظة الوقوع بتوقيت UTC.</param>
/// <param name="State">
/// حالة الاستحقاق التي وقع النشاط تحتها.
/// <para>
/// <b>يُلتقَط ولا يُحتسَب.</b> «هل يُعدّ من يقرأ فقط مستخدماً قابلاً للفوترة؟»
/// سؤالٌ <b>تجاري</b> جوابه للمالك، وهو غير محسوم هنا: العدّ الافتراضي لم يتغيّر،
/// و<c>GetActiveUsersAsync</c> ما يزال يعيد كل من عمل شيئاً.
/// </para>
/// <para>
/// وما يُحسم هنا هو <b>الالتقاط</b>: العدّان مختلفان مادّياً على فاتورة عميلٍ
/// انقطع سداده وبقي يُخرج سجلاته، و<b>لا استعلام يستخرج ما لم يُكتب</b>. فبوجود
/// هذا الحقل يصير أيّ التعريفين قابلاً للحساب <b>بأثر رجعي</b> على شهور مضت،
/// وبغيابه يصير الاختيار نهائياً وقت الكتابة — وهو الخطأ الذي لا يُصلَح
/// (نفس حجّة <c>BillableUsers</c> في مستوى التحكّم).
/// </para>
/// </param>
public sealed record UserActivityEvent(
    TenantId Tenant,
    UserId User,
    BabelModule Module,
    string Activity,
    DateTimeOffset OccurredAt,
    EntitlementState State);
