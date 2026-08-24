using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Model;
using Babel.Compliance.Reconciliation;

namespace Babel.Compliance.Store;

/// <summary>
/// تخزين في الذاكرة يحاكي <b>دلالات</b> النسخة العلائقية: معاملة واحدة تلفّ العمل كله،
/// وقفل لكل وحدة إصدار يقوم مقام <c>SELECT … FOR UPDATE</c>، وتراجع يمحو كل شيء.
/// <para/>
/// غرضه الوحيد أن يكون خط الأنابيب <b>قابلاً للتشغيل والاختبار اليوم بلا قاعدة بيانات
/// وبلا اعتمادات</b>. النسخة العلائقية في Store/Ef هي نسخة الإنتاج.
/// </summary>
public sealed class InMemoryComplianceStore : IComplianceStore
{
    private readonly Lock _global = new();
    private readonly Dictionary<(string, string), IssuingUnitChainHead> _heads = [];
    private readonly Dictionary<ComplianceDocumentId, ComplianceRecord> _records = [];
    private readonly Dictionary<ComplianceDocumentId, List<SubmissionAttempt>> _attempts = [];
    private readonly Dictionary<ComplianceDocumentId, List<StatusTransition>> _transitions = [];
    private readonly List<ComplianceWorkItem> _work = [];
    private readonly List<ReconciliationFinding> _findings = [];

    /// <summary>تسلسل معاملات، تماماً كما يفعل قفل الصف على وحدة الإصدار في القاعدة الحقيقية.</summary>
    private readonly SemaphoreSlim _txGate = new(1, 1);

    public TimeProvider Clock { get; init; } = TimeProvider.System;

    public async Task<T> InTransactionAsync<T>(
        Func<IComplianceUnitOfWork, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await _txGate.WaitAsync(ct);
        var uow = new Uow(this);
        try
        {
            var result = await work(uow, ct);
            uow.Commit();
            return result;
        }
        catch
        {
            uow.Rollback();
            throw;
        }
        finally
        {
            _txGate.Release();
        }
    }

    public Task InTransactionAsync(Func<IComplianceUnitOfWork, CancellationToken, Task> work, CancellationToken ct) =>
        InTransactionAsync<object?>(async (uow, c) => { await work(uow, c); return null; }, ct);

    /// <summary>قراءة خارج المعاملة، للوحات المتابعة والاختبارات.</summary>
    public ComplianceRecord? Peek(ComplianceDocumentId id)
    {
        lock (_global) return _records.TryGetValue(id, out var r) ? Clone(r) : null;
    }

    public IReadOnlyList<SubmissionAttempt> PeekAttempts(ComplianceDocumentId id)
    {
        lock (_global) return _attempts.TryGetValue(id, out var a) ? [.. a] : [];
    }

    public IReadOnlyList<StatusTransition> PeekTransitions(ComplianceDocumentId id)
    {
        lock (_global) return _transitions.TryGetValue(id, out var t) ? [.. t] : [];
    }

    public IReadOnlyList<ComplianceWorkItem> PeekWork()
    {
        lock (_global) return [.. _work];
    }

    private static ComplianceRecord Clone(ComplianceRecord r) => new()
    {
        DocumentId = r.DocumentId,
        DocumentUuid = r.DocumentUuid,
        Tenant = r.Tenant,
        IssuingUnit = r.IssuingUnit,
        Environment = r.Environment,
        Kind = r.Kind,
        Flow = r.Flow,
        DocumentNumber = r.DocumentNumber,
        JournalEntry = r.JournalEntry,
        IssuedAt = r.IssuedAt,
        Counter = r.Counter,
        PreviousHash = r.PreviousHash,
        DocumentHash = r.DocumentHash,
        FrozenPayload = r.FrozenPayload,
        SealState = r.SealState,
        SubmissionFingerprint = r.SubmissionFingerprint,
        RenderedBody = r.RenderedBody,
        NetTotal = r.NetTotal,
        TaxTotal = r.TaxTotal,
        GrossTotal = r.GrossTotal,
        CurrencyCode = r.CurrencyCode,
        Status = r.Status,
        AttemptCount = r.AttemptCount,
        ResolutionAttemptCount = r.ResolutionAttemptCount,
        QueuedAt = r.QueuedAt,
        SettledAt = r.SettledAt,
        ProviderReference = r.ProviderReference,
        StampedDocument = r.StampedDocument,
        Notices = [.. r.Notices],
        HumanReviewReasonAr = r.HumanReviewReasonAr,
        HumanReviewReasonEn = r.HumanReviewReasonEn,
        Version = r.Version
    };

    /// <summary>
    /// وحدة العمل: تجمع التغييرات ثم تطبّقها دفعة واحدة عند الإتمام.
    /// التراجع يترك المخزن كما كان — بما في ذلك العدّاد.
    /// </summary>
    private sealed class Uow(InMemoryComplianceStore store) : IComplianceUnitOfWork
    {
        private readonly List<Action> _pending = [];
        private readonly Dictionary<(string, string), long> _reserved = [];

        public void Commit()
        {
            lock (store._global)
                foreach (var a in _pending) a();
            _pending.Clear();
        }

        public void Rollback() => _pending.Clear();

        public Task<ChainSlot> AllocateChainSlotAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct)
        {
            lock (store._global)
            {
                var key = (tenant.Value, unit.Value);
                if (!store._heads.TryGetValue(key, out var head))
                {
                    head = new IssuingUnitChainHead
                    {
                        Tenant = tenant,
                        IssuingUnit = unit,
                        NextCounter = 1,
                        HeadHash = ComplianceCanonical.Genesis(tenant, unit),
                        UpdatedAt = store.Clock.GetUtcNow()
                    };
                    store._heads[key] = head;
                }

                if (head.IsHalted)
                    throw new ChainHaltedException(
                        $"سلسلة وحدة الإصدار «{unit}» موقوفة: {head.HaltReasonAr} / chain halted: {head.HaltReasonEn}");

                var counter = _reserved.TryGetValue(key, out var r) ? r : head.NextCounter;
                _reserved[key] = counter;
                return Task.FromResult(new ChainSlot(counter, head.HeadHash));
            }
        }

        public Task AdvanceChainHeadAsync(TenantId tenant, IssuingUnitId unit, long counter, byte[] newHead, CancellationToken ct)
        {
            var key = (tenant.Value, unit.Value);
            _pending.Add(() =>
            {
                var head = store._heads[key];
                if (head.NextCounter != counter)
                    throw new InvalidOperationException(
                        $"محاولة تقديم رأس السلسلة بعدّاد {counter} بينما التالي {head.NextCounter}");
                head.NextCounter = counter + 1;
                head.HeadHash = newHead;
                head.UpdatedAt = store.Clock.GetUtcNow();
            });
            return Task.CompletedTask;
        }

        public Task<IssuingUnitChainHead?> GetChainHeadAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct)
        {
            lock (store._global)
                return Task.FromResult(store._heads.GetValueOrDefault((tenant.Value, unit.Value)));
        }

        public Task HaltChainAsync(TenantId tenant, IssuingUnitId unit, string reasonAr, string reasonEn, CancellationToken ct)
        {
            var key = (tenant.Value, unit.Value);
            _pending.Add(() =>
            {
                if (!store._heads.TryGetValue(key, out var head)) return;
                head.IsHalted = true;
                head.HaltReasonAr = reasonAr;
                head.HaltReasonEn = reasonEn;
            });
            return Task.CompletedTask;
        }

        public Task InsertAsync(ComplianceRecord record, CancellationToken ct)
        {
            _pending.Add(() => store._records[record.DocumentId] = Clone(record));
            return Task.CompletedTask;
        }

        public Task<ComplianceRecord?> GetAsync(ComplianceDocumentId id, CancellationToken ct)
        {
            lock (store._global)
                return Task.FromResult(store._records.TryGetValue(id, out var r) ? Clone(r) : null);
        }

        public Task UpdateAsync(ComplianceRecord record, CancellationToken ct)
        {
            _pending.Add(() =>
            {
                if (store._records.TryGetValue(record.DocumentId, out var existing) &&
                    existing.Version != record.Version)
                    throw new ComplianceConcurrencyException(
                        $"تعارض تعديل على المستند {record.DocumentId}: النسخة المخزَّنة {existing.Version} " +
                        $"والنسخة المُقدَّمة {record.Version}");
                record.Version++;
                store._records[record.DocumentId] = Clone(record);
            });
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ComplianceRecord>> ListAsync(ComplianceQuery q, CancellationToken ct)
        {
            lock (store._global)
            {
                IEnumerable<ComplianceRecord> rows = store._records.Values
                    .Where(r => r.Tenant.Value == q.Tenant.Value);
                if (q.IssuingUnit is { } u) rows = rows.Where(r => r.IssuingUnit.Value == u.Value);
                if (q.Flow is { } f) rows = rows.Where(r => r.Flow == f);
                if (q.Statuses is { Count: > 0 }) rows = rows.Where(r => q.Statuses.Contains(r.Status));
                if (q.IssuedFrom is { } from) rows = rows.Where(r => r.IssuedAt >= from);
                if (q.IssuedTo is { } to) rows = rows.Where(r => r.IssuedAt <= to);
                return Task.FromResult<IReadOnlyList<ComplianceRecord>>(
                    [.. rows.OrderBy(r => r.IssuingUnit.Value, StringComparer.Ordinal)
                            .ThenBy(r => r.Counter)
                            .Take(q.Limit)
                            .Select(Clone)]);
            }
        }

        public Task InsertAttemptAsync(SubmissionAttempt attempt, CancellationToken ct)
        {
            _pending.Add(() =>
            {
                if (!store._attempts.TryGetValue(attempt.DocumentId, out var list))
                    store._attempts[attempt.DocumentId] = list = [];
                list.Add(attempt);
            });
            return Task.CompletedTask;
        }

        public Task UpdateAttemptAsync(SubmissionAttempt attempt, CancellationToken ct)
        {
            _pending.Add(() =>
            {
                var list = store._attempts[attempt.DocumentId];
                var i = list.FindIndex(a => a.AttemptId == attempt.AttemptId);
                if (i >= 0) list[i] = attempt;
            });
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<SubmissionAttempt>> AttemptsAsync(ComplianceDocumentId id, CancellationToken ct)
        {
            lock (store._global)
                return Task.FromResult<IReadOnlyList<SubmissionAttempt>>(
                    store._attempts.TryGetValue(id, out var l) ? [.. l.OrderBy(a => a.AttemptNo)] : []);
        }

        public Task AppendTransitionAsync(StatusTransition transition, CancellationToken ct)
        {
            _pending.Add(() =>
            {
                if (!store._transitions.TryGetValue(transition.DocumentId, out var list))
                    store._transitions[transition.DocumentId] = list = [];
                list.Add(transition);
            });
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<StatusTransition>> TransitionsAsync(ComplianceDocumentId id, CancellationToken ct)
        {
            lock (store._global)
                return Task.FromResult<IReadOnlyList<StatusTransition>>(
                    store._transitions.TryGetValue(id, out var l) ? [.. l.OrderBy(t => t.Seq)] : []);
        }

        public Task EnqueueAsync(ComplianceWorkItem item, CancellationToken ct)
        {
            _pending.Add(() => store._work.Add(item));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ComplianceWorkItem>> DueWorkAsync(DateTimeOffset now, int max, CancellationToken ct)
        {
            lock (store._global)
                return Task.FromResult<IReadOnlyList<ComplianceWorkItem>>(
                    [.. store._work.Where(w => !w.Done && w.NotBefore <= now)
                                   .OrderBy(w => w.NotBefore)
                                   .Take(max)]);
        }

        public Task UpdateWorkAsync(ComplianceWorkItem item, CancellationToken ct)
        {
            _pending.Add(() =>
            {
                var i = store._work.FindIndex(w => w.WorkItemId == item.WorkItemId);
                if (i >= 0) store._work[i] = item;
            });
            return Task.CompletedTask;
        }

        public Task AddFindingsAsync(IReadOnlyList<ReconciliationFinding> findings, CancellationToken ct)
        {
            _pending.Add(() => store._findings.AddRange(findings));
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReconciliationFinding>> OpenFindingsAsync(TenantId tenant, CancellationToken ct)
        {
            lock (store._global)
                return Task.FromResult<IReadOnlyList<ReconciliationFinding>>(
                    [.. store._findings.Where(f => f.Tenant.Value == tenant.Value && !f.Resolved)]);
        }

        public Task ResolveFindingAsync(Guid findingId, string actor, string noteAr, string noteEn, CancellationToken ct)
        {
            _pending.Add(() =>
            {
                var f = store._findings.FirstOrDefault(x => x.FindingId == findingId);
                if (f is null) return;
                f.Resolved = true;
                f.ResolvedAt = store.Clock.GetUtcNow();
                f.ResolvedBy = actor;
                f.ResolutionNoteAr = noteAr;
                f.ResolutionNoteEn = noteEn;
            });
            return Task.CompletedTask;
        }
    }
}

public sealed class ChainHaltedException(string message) : Exception(message);

public sealed class ComplianceConcurrencyException(string message) : Exception(message);
