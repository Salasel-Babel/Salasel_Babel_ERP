using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>
/// طلب تغيير استحقاق. <paramref name="ChangedBy"/> و<paramref name="Reason"/> إلزاميان
/// لأن كل تغيير يُكتب في سجل التدقيق بمن ومتى (وثيقة المعمارية §14).
/// </summary>
/// <param name="Tenant">المستأجر.</param>
/// <param name="Changes">الوحدات المطلوب تغييرها وحالاتها الجديدة.</param>
/// <param name="ChangedBy">من طلب التغيير.</param>
/// <param name="Reason">سبب التغيير — رقم اشتراك أو أمر بيع أو قرار تحصيل.</param>
public sealed record EntitlementChangeRequest(
    TenantId Tenant,
    IReadOnlyDictionary<BabelModule, EntitlementState> Changes,
    UserId ChangedBy,
    string Reason);
