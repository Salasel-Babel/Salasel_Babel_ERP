using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Core.Audit;

/// <summary>
/// تنفيذ في الذاكرة. المخزن الدائم يأتي في موجة الاستمرارية؛
/// الحدّ موجود من اليوم الأول لأن قيداً لم يُلتقط لا يُستعاد لاحقاً.
/// </summary>
public sealed class InMemoryAuditLog : IAuditLog
{
    private readonly ConcurrentDictionary<TenantId, List<AuditEntry>> _entries = new();

    /// <inheritdoc />
    public ValueTask RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);
        cancellationToken.ThrowIfCancellationRequested();

        List<AuditEntry> bucket = _entries.GetOrAdd(entry.Tenant, static _ => []);
        lock (bucket)
        {
            bucket.Add(entry);
        }

        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<AuditEntry>> ReadAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!_entries.TryGetValue(tenant, out List<AuditEntry>? bucket))
        {
            return ValueTask.FromResult<IReadOnlyList<AuditEntry>>([]);
        }

        lock (bucket)
        {
            return ValueTask.FromResult<IReadOnlyList<AuditEntry>>([.. bucket]);
        }
    }
}
