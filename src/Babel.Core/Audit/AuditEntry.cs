using Babel.SharedKernel;

namespace Babel.Core.Audit;

/// <summary>
/// قيد تدقيق: مَن فعل ماذا ومتى وعلى أي موضوع.
/// «سجل التدقيق كامل، مؤرَّخ، لا يمكن تعطيله» (وثيقة المعمارية §14).
/// </summary>
/// <param name="Tenant">المستأجر.</param>
/// <param name="Actor">الفاعل.</param>
/// <param name="OccurredAt">لحظة الوقوع بتوقيت UTC، مقصوصة إلى الميكروثانية (وثيقة المعمارية §8.2 مصيدة 1).</param>
/// <param name="Action">رمز الإجراء الثابت، مثل <c>entitlement.changed</c>.</param>
/// <param name="Subject">موضوع الإجراء.</param>
/// <param name="Details">تفصيل نصي اختياري.</param>
public sealed record AuditEntry(
    TenantId Tenant,
    UserId Actor,
    DateTimeOffset OccurredAt,
    string Action,
    string Subject,
    string? Details);
