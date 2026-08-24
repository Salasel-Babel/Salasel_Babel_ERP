using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Provisioning;

/// <summary>لحظة المقاطعة المُحاكاة في الإثباتات.</summary>
public enum InterruptPhase
{
    /// <summary>بعد تنفيذ أثر الخطوة، و<b>قبل</b> تسجيلها مكتملة — أخطر الحالتين.</summary>
    AfterEffect,
    /// <summary>بعد تسجيل اكتمال الخطوة، وقبل بدء التالية.</summary>
    AfterCommit
}

public sealed class SimulatedCrashException(string step, InterruptPhase phase)
    : Exception($"مقاطعة مُحاكاة عند الخطوة «{step}» ({phase})")
{
    public string Step { get; } = step;
    public InterruptPhase Phase { get; } = phase;
}

public sealed record ProvisioningStepState(
    string Name, int Ordinal, string State, int Attempts,
    DateTimeOffset StartedAt, DateTimeOffset? FinishedAt);

/// <summary>
/// دفتر التزويد. الإحكام هنا <b>لكل خطوة على حدة</b> ومستقلّ عن الترتيب —
/// وليس «آخر خطوة مُطبَّقة &gt; رقم الخطوة». ذلك النمط بالذات هو فخ-13:
/// حارس تصاعدي يُسقِط بصمت ما يصل خارج الترتيب، وقد ضاعت به 500 ريال من
/// 1,500 في القياس المُسجَّل.
/// </summary>
public sealed class ProvisioningJournal(NpgsqlConnection control)
{
    /// <summary>
    /// يفتح تشغيلة تزويد أو يستعيد القائمة بنفس مفتاح الإحكام.
    /// المفتاح <b>يورّده النداء</b> — لا يُولَّد داخلياً، وإلا لم يكن مفتاح إحكام.
    /// </summary>
    public async Task<(Guid RunId, bool Resumed)> OpenRunAsync(string idempotencyKey,
        Guid tenantId, string tenantCode, string requestedBy, CancellationToken ct = default)
    {
        var candidate = Guid.CreateVersion7();
        var inserted = await Db.WriteIdempotentAsync(control, """
            insert into control.provisioning_run
                (run_id, idempotency_key, tenant_id, tenant_code, requested_by, requested_at)
            values (@id, @k, @t, @code, @by, @at)
            on conflict (idempotency_key) do nothing
            """, p =>
            {
                p.Add(Db.P("id", candidate, NpgsqlDbType.Uuid));
                p.AddWithValue("k", idempotencyKey);
                p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
                p.AddWithValue("code", tenantCode);
                p.AddWithValue("by", requestedBy);
                p.AddWithValue("at", Canon.Now());
            }, null, ct);

        if (inserted == 1) return (candidate, false);

        var existing = await Db.QueryAsync(control,
            "select run_id, tenant_id from control.provisioning_run where idempotency_key = @k",
            r => (Run: r.GetGuid(0), Tenant: r.GetGuid(1)),
            p => p.AddWithValue("k", idempotencyKey), null, ct);

        if (existing[0].Tenant != tenantId)
            throw new InvalidOperationException(
                $"مفتاح الإحكام «{idempotencyKey}» مستعمَل لمستأجر آخر — "
                + "إعادة استعمال مفتاح لمستأجر مختلف خطأ نداء، لا حالة استئناف.");

        return (existing[0].Run, true);
    }

    /// <summary>
    /// يحجز خطوة. يُرجِع <c>false</c> إن كانت <b>مكتملة سلفاً</b> فتُتخطّى؛
    /// و<c>true</c> إن كانت جديدة أو مُتوقّفة في منتصفها فتُعاد.
    /// </summary>
    public async Task<bool> ClaimStepAsync(Guid runId, int ordinal, string step,
        CancellationToken ct = default)
    {
        var n = await Db.WriteIdempotentAsync(control, """
            insert into control.provisioning_step
                (run_id, step_ordinal, step_name, state, attempts, started_at)
            values (@r, @o, @s, 'Started', 1, @t)
            on conflict (run_id, step_name) do update
               set state = 'Started',
                   attempts = control.provisioning_step.attempts + 1,
                   started_at = excluded.started_at
             where control.provisioning_step.state <> 'Completed'
            """, p =>
            {
                p.Add(Db.P("r", runId, NpgsqlDbType.Uuid));
                p.AddWithValue("o", ordinal);
                p.AddWithValue("s", step);
                p.AddWithValue("t", Canon.Now());
            }, null, ct);
        return n == 1;
    }

    public async Task CompleteStepAsync(Guid runId, string step, string detailJson = "{}",
        CancellationToken ct = default)
    {
        await Db.WriteAsync(control, """
            update control.provisioning_step
               set state = 'Completed', finished_at = @t, detail = @d
             where run_id = @r and step_name = @s
            """, 1, p =>
            {
                p.AddWithValue("t", Canon.Now());
                p.Add(new NpgsqlParameter("d", NpgsqlDbType.Jsonb) { Value = detailJson });
                p.Add(Db.P("r", runId, NpgsqlDbType.Uuid));
                p.AddWithValue("s", step);
            }, null, ct);
    }

    public async Task CompleteRunAsync(Guid runId, CancellationToken ct = default)
    {
        await Db.WriteAsync(control, """
            update control.provisioning_run
               set completed_at = @t, outcome = 'Completed'
             where run_id = @r
            """, 1, p =>
            {
                p.AddWithValue("t", Canon.Now());
                p.Add(Db.P("r", runId, NpgsqlDbType.Uuid));
            }, null, ct);
    }

    public async Task<List<ProvisioningStepState>> ReadStepsAsync(Guid runId,
        CancellationToken ct = default) =>
        await Db.QueryAsync(control, """
            select step_name, step_ordinal, state, attempts, started_at, finished_at
              from control.provisioning_step
             where run_id = @r
             order by step_ordinal asc
            """,
            r => new ProvisioningStepState(r.GetString(0), r.GetInt32(1), r.GetString(2),
                r.GetInt32(3), r.GetFieldValue<DateTimeOffset>(4),
                r.IsDBNull(5) ? null : r.GetFieldValue<DateTimeOffset>(5)),
            p => p.Add(Db.P("r", runId, NpgsqlDbType.Uuid)), null, ct);
}
