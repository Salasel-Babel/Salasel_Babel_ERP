using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Core.Metering;

/// <summary>
/// مخزن جذع في الذاكرة. يُستبدل بجدول مقسَّم بالفترة في موجة الاستمرارية،
/// ولا يتغيّر معه أي مستدعٍ.
/// </summary>
public sealed class InMemoryUsageStore : IUsageStore, IUsageReader, IUsageMeter
{
    private readonly ConcurrentBag<ModuleUsageEvent> _moduleUsage = [];
    private readonly ConcurrentBag<UserActivityEvent> _userActivity = [];

    /// <inheritdoc />
    public ValueTask RecordModuleUsageAsync(ModuleUsageEvent usage, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(usage);
        return AppendModuleUsageAsync([usage], cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask RecordUserActivityAsync(UserActivityEvent activity, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(activity);
        return AppendUserActivityAsync([activity], cancellationToken);
    }

    /// <inheritdoc />
    public ValueTask AppendModuleUsageAsync(IReadOnlyList<ModuleUsageEvent> batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (ModuleUsageEvent usage in batch)
        {
            _moduleUsage.Add(usage);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask AppendUserActivityAsync(IReadOnlyList<UserActivityEvent> batch, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(batch);
        cancellationToken.ThrowIfCancellationRequested();

        foreach (UserActivityEvent activity in batch)
        {
            _userActivity.Add(activity);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyDictionary<BabelModule, long>> GetModuleUsageAsync(
        TenantId tenant,
        BillingPeriod period,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        Dictionary<BabelModule, long> totals = [];
        foreach (ModuleUsageEvent usage in _moduleUsage)
        {
            if (usage.Tenant != tenant || BillingPeriod.FromInstant(usage.OccurredAt) != period)
            {
                continue;
            }

            totals[usage.Module] = totals.GetValueOrDefault(usage.Module) + usage.Quantity;
        }

        return ValueTask.FromResult<IReadOnlyDictionary<BabelModule, long>>(totals);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyCollection<UserId>> GetActiveUsersAsync(
        TenantId tenant,
        BillingPeriod period,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        HashSet<UserId> users = [];
        foreach (UserActivityEvent activity in _userActivity)
        {
            if (activity.Tenant == tenant && BillingPeriod.FromInstant(activity.OccurredAt) == period)
            {
                users.Add(activity.User);
            }
        }

        return ValueTask.FromResult<IReadOnlyCollection<UserId>>(users);
    }
}
