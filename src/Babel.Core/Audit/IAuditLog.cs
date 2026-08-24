namespace Babel.Core.Audit;

/// <summary>سجل التدقيق. لا واجهة حذف ولا تعديل — عمداً.</summary>
public interface IAuditLog
{
    /// <summary>يسجّل قيد تدقيق.</summary>
    ValueTask RecordAsync(AuditEntry entry, CancellationToken cancellationToken = default);

    /// <summary>يقرأ قيود التدقيق لمستأجر، بترتيب وقوعها.</summary>
    ValueTask<IReadOnlyList<AuditEntry>> ReadAsync(SharedKernel.TenantId tenant, CancellationToken cancellationToken = default);
}
