using System.Collections.Concurrent;
using System.Globalization;
using Babel.Core.Audit;
using Babel.SharedKernel;

namespace Babel.Core.Entitlement;

/// <summary>
/// تنفيذ في الذاكرة. لا قاعدة بيانات في هذه الموجة عمداً — المطلوب الآن أن يوجد
/// <b>الحدّ</b>، لأن الحدّ هو ما يصعب إضافته لاحقاً، لا الجدول.
/// </summary>
public sealed class InMemoryEntitlementService : IEntitlementService
{
    private readonly ConcurrentDictionary<TenantId, EntitlementSet> _sets = new();
    private readonly IAuditLog _auditLog;
    private readonly TimeProvider _timeProvider;

    /// <summary>ينشئ التنفيذ.</summary>
    public InMemoryEntitlementService(IAuditLog auditLog, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(auditLog);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _auditLog = auditLog;
        _timeProvider = timeProvider;
    }

    /// <inheritdoc />
    public ValueTask<EntitlementSet> GetAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_sets.GetOrAdd(tenant, static t => EntitlementSet.Baseline(t)));
    }

    /// <inheritdoc />
    public async ValueTask<EntitlementState> GetStateAsync(TenantId tenant, BabelModule module, CancellationToken cancellationToken = default)
    {
        EntitlementSet set = await GetAsync(tenant, cancellationToken).ConfigureAwait(false);
        return set.StateOf(module);
    }

    /// <inheritdoc />
    public async ValueTask<Result<EntitlementSet>> ApplyAsync(EntitlementChangeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        EntitlementSet current = await GetAsync(request.Tenant, cancellationToken).ConfigureAwait(false);
        Result<EntitlementSet> next = current.With(request.Changes);

        if (next.IsFailure)
        {
            return next;
        }

        IReadOnlyList<(BabelModule Module, EntitlementState From, EntitlementState To)> diff = current.DiffTo(next.Value);
        _sets[request.Tenant] = next.Value;

        DateTimeOffset now = _timeProvider.GetUtcNow();
        foreach ((BabelModule module, EntitlementState from, EntitlementState to) in diff)
        {
            await _auditLog.RecordAsync(
                new AuditEntry(
                    request.Tenant,
                    request.ChangedBy,
                    now,
                    "entitlement.changed",
                    module.ToString(),
                    string.Create(CultureInfo.InvariantCulture, $"{from} -> {to}; reason: {request.Reason}")),
                cancellationToken).ConfigureAwait(false);
        }

        return next;
    }
}
