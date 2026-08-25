using System.Collections.Concurrent;
using Babel.SharedKernel;

namespace Babel.Ai.Capture;

/// <summary>
/// مخزن المسوّدات. واجهة لأن الاستمرارية موجة لاحقة، ولأن المسوّدة <b>ليست</b> مستنداً
/// محاسبياً فلا تسكن جداول وحدة مالية.
/// </summary>
public interface ICapturedDraftStore
{
    /// <summary>يحفظ مسوّدة أو يستبدلها.</summary>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask SaveAsync(CapturedInvoiceDraft draft, CancellationToken cancellationToken = default);

    /// <summary>يجلب مسوّدة داخل مستأجرها. المستأجر جزء من المفتاح لا مرشّح.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="draftId">معرّف المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<CapturedInvoiceDraft?> FindAsync(TenantId tenant, Guid draftId, CancellationToken cancellationToken = default);

    /// <summary>يعدّد مسوّدات مستأجر مرتَّبةً بلحظة الالتقاط.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<IReadOnlyList<CapturedInvoiceDraft>> ListAsync(TenantId tenant, CancellationToken cancellationToken = default);
}

/// <summary>
/// مخزن في الذاكرة. كافٍ لتشغيل المسار كاملاً بلا قاعدة بيانات، ومطابق في شكله
/// لـ<c>InMemoryComplianceStore</c> في وحدة الالتزام.
/// </summary>
public sealed class InMemoryCapturedDraftStore : ICapturedDraftStore
{
    private readonly ConcurrentDictionary<(Guid Tenant, Guid Draft), CapturedInvoiceDraft> _drafts = new();

    /// <inheritdoc />
    public ValueTask SaveAsync(CapturedInvoiceDraft draft, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);
        cancellationToken.ThrowIfCancellationRequested();
        _drafts[(draft.Tenant.Value, draft.DraftId)] = draft;
        return ValueTask.CompletedTask;
    }

    /// <inheritdoc />
    public ValueTask<CapturedInvoiceDraft?> FindAsync(TenantId tenant, Guid draftId, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return ValueTask.FromResult(_drafts.TryGetValue((tenant.Value, draftId), out CapturedInvoiceDraft? found) ? found : null);
    }

    /// <inheritdoc />
    public ValueTask<IReadOnlyList<CapturedInvoiceDraft>> ListAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        IReadOnlyList<CapturedInvoiceDraft> found = [.. _drafts
            .Where(pair => pair.Key.Tenant == tenant.Value)
            .Select(static pair => pair.Value)
            .OrderBy(static draft => draft.CapturedAt)
            .ThenBy(static draft => draft.DraftId)];

        return ValueTask.FromResult(found);
    }
}
