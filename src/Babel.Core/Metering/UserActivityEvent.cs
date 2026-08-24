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
public sealed record UserActivityEvent(
    TenantId Tenant,
    UserId User,
    BabelModule Module,
    string Activity,
    DateTimeOffset OccurredAt);
