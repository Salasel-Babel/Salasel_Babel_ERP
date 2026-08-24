using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Model;
using Babel.Compliance.Reconciliation;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace Babel.Compliance.Store.Ef;

/// <summary>
/// نسخة الإنتاج من حدّ التخزين: معاملة PostgreSQL واحدة تلفّ حجز العدّاد وكتابة السجل
/// وصف المحاولة وعنصر الصندوق الصادر معاً.
/// <para/>
/// <b>العدّاد صف مقفول، لا <c>SEQUENCE</c></b> — والسبب مقيس في 02-architecture §7.3:
/// تسلسلات PostgreSQL غير معاملاتية عمداً، و<c>nextval</c> ثم تراجع يُضيّع الرقم نهائياً.
/// </summary>
public sealed class EfComplianceStore(IDbContextFactory<ComplianceDbContext> factory, TimeProvider clock)
    : IComplianceStore
{
    public async Task<T> InTransactionAsync<T>(
        Func<IComplianceUnitOfWork, CancellationToken, Task<T>> work, CancellationToken ct)
    {
        await using var ctx = await factory.CreateDbContextAsync(ct);
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        var uow = new Uow(ctx, clock);
        var result = await work(uow, ct);
        await ctx.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);
        return result;
    }

    public Task InTransactionAsync(Func<IComplianceUnitOfWork, CancellationToken, Task> work, CancellationToken ct) =>
        InTransactionAsync<object?>(async (uow, c) => { await work(uow, c); return null; }, ct);

    private sealed class Uow(ComplianceDbContext ctx, TimeProvider clock) : IComplianceUnitOfWork
    {
        public async Task<ChainSlot> AllocateChainSlotAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct)
        {
            var conn = (NpgsqlConnection)ctx.Database.GetDbConnection();
            if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync(ct);
            var tx = (NpgsqlTransaction)ctx.Database.CurrentTransaction!.GetDbTransaction();

            // قفل الصف داخل معاملة العمل نفسها. الرقم يُهدر فقط إن تراجعت المعاملة كلها،
            // وعندها لا يوجد مستند أصلاً — وهذا هو الفرق كله عن SEQUENCE.
            await using (var cmd = new NpgsqlCommand(
                $"select next_counter, head_hash, is_halted, coalesce(halt_reason_ar,''), coalesce(halt_reason_en,'') " +
                $"from {ComplianceDbContext.Schema}.chain_head " +
                "where tenant_id = @t and issuing_unit_id = @u for update", conn, tx))
            {
                cmd.Parameters.AddWithValue("t", tenant.Value);
                cmd.Parameters.AddWithValue("u", unit.Value);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (await r.ReadAsync(ct))
                {
                    if (r.GetBoolean(2))
                        throw new ChainHaltedException(
                            $"سلسلة وحدة الإصدار «{unit}» موقوفة: {r.GetString(3)} / chain halted: {r.GetString(4)}");
                    return new ChainSlot(r.GetInt64(0), (byte[])r[1]);
                }
            }

            // أول مستند على هذه الوحدة: يُنشأ رأس السلسلة ببصمة تكوين، تحت نفس المعاملة.
            var genesis = ComplianceCanonical.Genesis(tenant, unit);
            await using (var insert = new NpgsqlCommand(
                $"insert into {ComplianceDbContext.Schema}.chain_head " +
                "(tenant_id, issuing_unit_id, next_counter, head_hash, updated_at, is_halted) " +
                "values (@t, @u, 1, @h, @n, false) on conflict do nothing", conn, tx))
            {
                insert.Parameters.AddWithValue("t", tenant.Value);
                insert.Parameters.AddWithValue("u", unit.Value);
                insert.Parameters.AddWithValue("h", genesis);
                insert.Parameters.AddWithValue("n", clock.GetUtcNow());
                await insert.ExecuteNonQueryAsync(ct);
            }
            return new ChainSlot(1, genesis);
        }

        public async Task AdvanceChainHeadAsync(
            TenantId tenant, IssuingUnitId unit, long counter, byte[] newHead, CancellationToken ct)
        {
            var conn = (NpgsqlConnection)ctx.Database.GetDbConnection();
            var tx = (NpgsqlTransaction)ctx.Database.CurrentTransaction!.GetDbTransaction();
            await using var cmd = new NpgsqlCommand(
                $"update {ComplianceDbContext.Schema}.chain_head " +
                "set next_counter = @c + 1, head_hash = @h, updated_at = @n " +
                "where tenant_id = @t and issuing_unit_id = @u and next_counter = @c", conn, tx);
            cmd.Parameters.AddWithValue("t", tenant.Value);
            cmd.Parameters.AddWithValue("u", unit.Value);
            cmd.Parameters.AddWithValue("c", counter);
            cmd.Parameters.AddWithValue("h", newHead);
            cmd.Parameters.AddWithValue("n", clock.GetUtcNow());
            var rows = await cmd.ExecuteNonQueryAsync(ct);
            if (rows != 1)
                throw new InvalidOperationException(
                    $"تعذّر تقديم رأس السلسلة للوحدة «{unit}» عند العدّاد {counter} — تسابق أو فجوة");
        }

        public Task<IssuingUnitChainHead?> GetChainHeadAsync(TenantId tenant, IssuingUnitId unit, CancellationToken ct) =>
            ctx.ChainHeads.FirstOrDefaultAsync(x => x.Tenant == tenant && x.IssuingUnit == unit, ct);

        public async Task HaltChainAsync(
            TenantId tenant, IssuingUnitId unit, string reasonAr, string reasonEn, CancellationToken ct)
        {
            var head = await ctx.ChainHeads.FirstOrDefaultAsync(x => x.Tenant == tenant && x.IssuingUnit == unit, ct);
            if (head is null) return;
            head.IsHalted = true;
            head.HaltReasonAr = reasonAr;
            head.HaltReasonEn = reasonEn;
        }

        public async Task InsertAsync(ComplianceRecord record, CancellationToken ct)
        {
            await ctx.Documents.AddAsync(record, ct);
        }

        public Task<ComplianceRecord?> GetAsync(ComplianceDocumentId id, CancellationToken ct) =>
            ctx.Documents.FirstOrDefaultAsync(x => x.DocumentId == id, ct);

        public Task UpdateAsync(ComplianceRecord record, CancellationToken ct)
        {
            if (ctx.Entry(record).State == EntityState.Detached) ctx.Documents.Update(record);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<ComplianceRecord>> ListAsync(ComplianceQuery q, CancellationToken ct)
        {
            var query = ctx.Documents.Where(x => x.Tenant == q.Tenant);
            if (q.IssuingUnit is { } u) query = query.Where(x => x.IssuingUnit == u);
            if (q.Flow is { } f) query = query.Where(x => x.Flow == f);
            if (q.Statuses is { Count: > 0 }) query = query.Where(x => q.Statuses.Contains(x.Status));
            if (q.IssuedFrom is { } from) query = query.Where(x => x.IssuedAt >= from);
            if (q.IssuedTo is { } to) query = query.Where(x => x.IssuedAt <= to);
            return await query.OrderBy(x => x.IssuingUnit).ThenBy(x => x.Counter)
                .Take(q.Limit == int.MaxValue ? int.MaxValue - 1 : q.Limit).ToListAsync(ct);
        }

        public async Task InsertAttemptAsync(SubmissionAttempt attempt, CancellationToken ct) =>
            await ctx.Attempts.AddAsync(attempt, ct);

        public Task UpdateAttemptAsync(SubmissionAttempt attempt, CancellationToken ct)
        {
            if (ctx.Entry(attempt).State == EntityState.Detached) ctx.Attempts.Update(attempt);
            return Task.CompletedTask;
        }

        public async Task<IReadOnlyList<SubmissionAttempt>> AttemptsAsync(ComplianceDocumentId id, CancellationToken ct) =>
            await ctx.Attempts.Where(x => x.DocumentId == id).OrderBy(x => x.AttemptNo).ToListAsync(ct);

        public async Task AppendTransitionAsync(StatusTransition transition, CancellationToken ct) =>
            await ctx.Transitions.AddAsync(transition, ct);

        public async Task<IReadOnlyList<StatusTransition>> TransitionsAsync(ComplianceDocumentId id, CancellationToken ct) =>
            await ctx.Transitions.Where(x => x.DocumentId == id).OrderBy(x => x.Seq).ToListAsync(ct);

        public async Task EnqueueAsync(ComplianceWorkItem item, CancellationToken ct) =>
            await ctx.WorkItems.AddAsync(item, ct);

        public async Task<IReadOnlyList<ComplianceWorkItem>> DueWorkAsync(
            DateTimeOffset now, int max, CancellationToken ct) =>
            await ctx.WorkItems.Where(x => !x.Done && x.NotBefore <= now)
                .OrderBy(x => x.NotBefore).Take(max).ToListAsync(ct);

        public Task UpdateWorkAsync(ComplianceWorkItem item, CancellationToken ct)
        {
            if (ctx.Entry(item).State == EntityState.Detached) ctx.WorkItems.Update(item);
            return Task.CompletedTask;
        }

        public async Task AddFindingsAsync(IReadOnlyList<ReconciliationFinding> findings, CancellationToken ct) =>
            await ctx.Findings.AddRangeAsync(findings, ct);

        public async Task<IReadOnlyList<ReconciliationFinding>> OpenFindingsAsync(TenantId tenant, CancellationToken ct) =>
            await ctx.Findings.Where(x => x.Tenant == tenant && !x.Resolved)
                .OrderByDescending(x => x.Severity).ToListAsync(ct);

        public async Task ResolveFindingAsync(
            Guid findingId, string actor, string noteAr, string noteEn, CancellationToken ct)
        {
            var f = await ctx.Findings.FirstOrDefaultAsync(x => x.FindingId == findingId, ct);
            if (f is null) return;
            f.Resolved = true;
            f.ResolvedAt = clock.GetUtcNow();
            f.ResolvedBy = actor;
            f.ResolutionNoteAr = noteAr;
            f.ResolutionNoteEn = noteEn;
        }
    }
}
