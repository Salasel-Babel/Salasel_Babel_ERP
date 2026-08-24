using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Core.Application;

/// <summary>
/// إدارة استحقاق المستأجر. مثال حيّ على الشكل الذي تتخذه كل خدمة تطبيق في المنتج:
/// علامة <see cref="IApplicationService"/>، وسمة استحقاق على كل نقطة دخول، ونداء المنفِّذ أولاً.
/// </summary>
public sealed class EntitlementAdministrationService : IApplicationService
{
    private readonly IEntitlementService _entitlements;
    private readonly IEntitlementEnforcer _enforcer;

    /// <summary>ينشئ الخدمة.</summary>
    public EntitlementAdministrationService(IEntitlementService entitlements, IEntitlementEnforcer enforcer)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        ArgumentNullException.ThrowIfNull(enforcer);
        _entitlements = entitlements;
        _enforcer = enforcer;
    }

    /// <summary>يقرأ مجموعة استحقاق المستأجر.</summary>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<EntitlementSet>> GetAsync(TenantId tenant, UserId actor, CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Read, "Core.Entitlement.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<EntitlementSet>.Failure(gate.Errors);
        }

        return Result<EntitlementSet>.Success(await _entitlements.GetAsync(tenant, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>يغيّر الاستحقاق. التغيير مُدقَّق: من ومتى ومن أي حالة إلى أي حالة.</summary>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<EntitlementSet>> ApplyAsync(EntitlementChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await _enforcer
            .EnsureAsync(request.Tenant, request.ChangedBy, BabelModule.Core, EntitlementAccess.Write, "Core.Entitlement.Apply", cancellationToken)
            .ConfigureAwait(false);

        return gate.IsFailure
            ? Result<EntitlementSet>.Failure(gate.Errors)
            : await _entitlements.ApplyAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
