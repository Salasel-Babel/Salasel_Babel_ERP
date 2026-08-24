using Babel.Core.Metering;
using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>
/// الإنفاذ والقياس في مكان واحد.
/// <para>
/// الدمج مقصود: لو كان القياس مساراً منفصلاً لنُسي في نصف نقاط الدخول، ولاكتُشف النقص
/// عند أول فاتورة اشتراك — بعد فوات أوان الالتقاط. هنا لا يمكن أن يمرّ استدعاء مستحَق
/// دون أن يُقاس على المحورين.
/// </para>
/// </summary>
public sealed class EntitlementEnforcer : IEntitlementEnforcer
{
    private readonly IEntitlementService _entitlements;
    private readonly IUsageMeter _usageMeter;
    private readonly TimeProvider _timeProvider;

    /// <summary>ينشئ المنفِّذ.</summary>
    public EntitlementEnforcer(IEntitlementService entitlements, IUsageMeter usageMeter, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(entitlements);
        ArgumentNullException.ThrowIfNull(usageMeter);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _entitlements = entitlements;
        _usageMeter = usageMeter;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public async ValueTask<Result> EnsureAsync(
        TenantId tenant,
        UserId actor,
        BabelModule module,
        EntitlementAccess access,
        string operation,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(operation);

        EntitlementState state = await _entitlements.GetStateAsync(tenant, module, cancellationToken).ConfigureAwait(false);

        if (!Allows(state, access))
        {
            return Result.Failure(state == EntitlementState.ReadOnly
                ? EntitlementErrors.ReadOnly(module)
                : EntitlementErrors.NotEntitled(module));
        }

        DateTimeOffset now = _timeProvider.GetUtcNow();
        await _usageMeter.RecordModuleUsageAsync(new ModuleUsageEvent(tenant, module, operation, actor, now, 1), cancellationToken).ConfigureAwait(false);
        await _usageMeter.RecordUserActivityAsync(new UserActivityEvent(tenant, actor, module, operation, now), cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }

    private static bool Allows(EntitlementState state, EntitlementAccess access) => state switch
    {
        EntitlementState.Entitled => true,
        EntitlementState.ReadOnly => access == EntitlementAccess.Read,
        _ => false,
    };
}
