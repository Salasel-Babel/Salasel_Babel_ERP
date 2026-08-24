using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>قراءة الاستحقاق وتغييره. التغيير مُدقَّق دائماً.</summary>
public interface IEntitlementService
{
    /// <summary>مجموعة استحقاق المستأجر.</summary>
    ValueTask<EntitlementSet> GetAsync(TenantId tenant, CancellationToken cancellationToken = default);

    /// <summary>حالة وحدة بعينها.</summary>
    ValueTask<EntitlementState> GetStateAsync(TenantId tenant, BabelModule module, CancellationToken cancellationToken = default);

    /// <summary>
    /// يطبّق تغييراً بعد التحقق من اتساق الناتج، ويكتب قيد تدقيق لكل وحدة تغيّرت حالتها.
    /// المجموعة غير المتسقة تُرفض كاملة — لا تطبيق جزئي.
    /// </summary>
    ValueTask<Result<EntitlementSet>> ApplyAsync(EntitlementChangeRequest request, CancellationToken cancellationToken = default);
}
