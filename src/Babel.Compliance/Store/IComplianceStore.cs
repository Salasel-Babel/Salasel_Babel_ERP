using Babel.Compliance.Abstractions;
using Babel.Compliance.Model;
using Babel.Compliance.Reconciliation;

namespace Babel.Compliance.Store;

/// <summary>
/// حدّ التخزين. <b>وحدة العمل هنا معاملة قاعدة بيانات واحدة</b> تملك:
/// حجز العدّاد، وكتابة السجل، وكتابة صف المحاولة، وإدراج عنصر الصندوق الصادر — معاً.
/// توزيع هذه على أكثر من معاملة يعيد إنتاج نفس مشكلة الغموض داخل نظامنا.
/// </summary>
public interface IComplianceStore
{
    Task<T> InTransactionAsync<T>(
        Func<IComplianceUnitOfWork, CancellationToken, Task<T>> work,
        CancellationToken cancellationToken);

    Task InTransactionAsync(
        Func<IComplianceUnitOfWork, CancellationToken, Task> work,
        CancellationToken cancellationToken);
}

public interface IComplianceUnitOfWork
{
    // ---- العدّاد والسلسلة -------------------------------------------------

    /// <summary>
    /// يحجز الخانة التالية في سلسلة وحدة الإصدار <b>تحت قفل صف</b>، لا عبر SEQUENCE.
    /// <para/>
    /// <b>مقيس في 02-architecture §7.3:</b> تسلسلات PostgreSQL غير معاملاتية عمداً —
    /// <c>nextval</c> ثم تراجع يُضيّع الرقم نهائياً، وهو غير متوافق مع متطلب سلسلة بلا فجوات.
    /// الرقم هنا يُهدر فقط إن تراجعت المعاملة كلها، وعندها لا يوجد مستند أصلاً.
    /// <para/>
    /// <b>مقيس أيضاً §7.4:</b> العدّاد هو الاختناق لا SHA-256. لذلك نطاقه
    /// <b>وحدة الإصدار</b>، لا المستأجر ولا المنتج — والتسلسل يقع داخل جهاز واحد فقط.
    /// </summary>
    Task<ChainSlot> AllocateChainSlotAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct);

    /// <summary>يثبّت رأس السلسلة بعد حساب بصمة المستند. داخل المعاملة نفسها.</summary>
    Task AdvanceChainHeadAsync(TenantId tenant, IssuingUnitId unit, long counter, byte[] newHead, CancellationToken ct);

    Task<IssuingUnitChainHead?> GetChainHeadAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct);

    Task HaltChainAsync(TenantId tenant, IssuingUnitId unit, string reasonAr, string reasonEn, CancellationToken ct);

    // ---- السجلات ----------------------------------------------------------

    Task InsertAsync(ComplianceRecord record, CancellationToken ct);
    Task<ComplianceRecord?> GetAsync(ComplianceDocumentId id, CancellationToken ct);
    Task UpdateAsync(ComplianceRecord record, CancellationToken ct);

    Task<IReadOnlyList<ComplianceRecord>> ListAsync(ComplianceQuery query, CancellationToken ct);

    // ---- المحاولات وسجل الانتقالات ----------------------------------------

    Task InsertAttemptAsync(SubmissionAttempt attempt, CancellationToken ct);
    Task UpdateAttemptAsync(SubmissionAttempt attempt, CancellationToken ct);
    Task<IReadOnlyList<SubmissionAttempt>> AttemptsAsync(ComplianceDocumentId id, CancellationToken ct);

    Task AppendTransitionAsync(StatusTransition transition, CancellationToken ct);
    Task<IReadOnlyList<StatusTransition>> TransitionsAsync(ComplianceDocumentId id, CancellationToken ct);

    // ---- الطابور ----------------------------------------------------------

    Task EnqueueAsync(ComplianceWorkItem item, CancellationToken ct);
    Task<IReadOnlyList<ComplianceWorkItem>> DueWorkAsync(DateTimeOffset now, int max, CancellationToken ct);
    Task UpdateWorkAsync(ComplianceWorkItem item, CancellationToken ct);

    // ---- المطابقة ---------------------------------------------------------

    Task AddFindingsAsync(IReadOnlyList<ReconciliationFinding> findings, CancellationToken ct);
    Task<IReadOnlyList<ReconciliationFinding>> OpenFindingsAsync(TenantId tenant, CancellationToken ct);
    Task ResolveFindingAsync(Guid findingId, string actor, string noteAr, string noteEn, CancellationToken ct);
}

public sealed record ComplianceQuery(
    TenantId Tenant,
    IssuingUnitId? IssuingUnit = null,
    ComplianceFlow? Flow = null,
    IReadOnlyCollection<ComplianceStatus>? Statuses = null,
    DateTimeOffset? IssuedFrom = null,
    DateTimeOffset? IssuedTo = null,
    int Limit = 1000);
