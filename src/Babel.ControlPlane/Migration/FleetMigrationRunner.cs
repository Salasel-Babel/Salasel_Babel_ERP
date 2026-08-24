using System.Diagnostics;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Migration;

public sealed record FleetTarget(Guid TenantId, string TenantCode, string DatabaseName, int Attempts);

public sealed record FleetStats(
    int Total, int Pending, int Leased, int Done, int Failed, int Skipped,
    int MaxAttempts, long TotalDurationMs)
{
    public bool Complete => Done + Skipped == Total;
}

public sealed record FleetRunReport(
    Guid MigrationId, string WorkerId, int Processed, int AlreadyDone, int Failed,
    TimeSpan Elapsed, FleetStats Stats)
{
    public double DatabasesPerSecond =>
        Elapsed.TotalSeconds <= 0 ? 0 : Processed / Elapsed.TotalSeconds;
}

/// <summary>
/// مُرحِّل الأسطول — الشيء الذي ينكسر عند مئات المستأجرين.
///
/// <para><b>ثلاث خصائص، وكلها مطلوبة معاً:</b></para>
/// <list type="number">
/// <item><b>دفعات</b>: لا يُفتح مئة اتصال دفعةً واحدة، ولا تُقرأ قائمة الأسطول
/// كاملةً في الذاكرة ثم يُعمل عليها — الحالة في قاعدة البيانات لا في العملية.</item>
/// <item><b>حالة لكل قاعدة</b>: كل قاعدة صفٌّ بحالته ومحاولاته وخطئه ومدّته.</item>
/// <item><b>استئناف</b>: عملية قُتلت لا تترك شيئاً غامضاً. الترحيلة الواحدة
/// معاملاتية داخل قاعدة المستأجر، فالقاعدة إمّا على الإصدار القديم أو الجديد
/// — <b>ولا حالة ثالثة</b>؛ والحجز ينتهي بمهلته فتُلتقط من جديد.</item>
/// </list>
///
/// <para>الحجز بـ<c>FOR UPDATE SKIP LOCKED</c> على صفوف <b>مرتّبة</b> بـ
/// <c>tenant_code</c> تصاعدياً: عدّة عمّال على نفس الخطة لا يتصارعون، وترتيب
/// الأقفال كلّي وثابت (فخ-10، فخ-11).</para>
/// </summary>
public sealed class FleetMigrationRunner(ControlPlaneOptions options, TenantRegistry registry)
{
    public Func<FleetTarget, Task>? AfterEach { get; set; }

    /// <summary>
    /// يبني خطة ترحيل أو يستأنف الخطة نفسها بنفس <c>planKey</c>.
    /// صفوف الأهداف تُدرَج في <b>عبارة واحدة مرتّبة</b> بـ<c>tenant_code</c>.
    /// </summary>
    public async Task<Guid> PlanAsync(string planKey, int targetVersion, BilingualName name,
        string createdBy, IReadOnlyList<TenantRecord>? tenants = null, CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);

        var candidate = Guid.CreateVersion7();
        var inserted = await Db.WriteIdempotentAsync(c, """
            insert into control.fleet_migration
                (migration_id, plan_key, target_version, name_ar, name_en, created_at, created_by)
            values (@id, @k, @v, @ar, @en, @t, @by)
            on conflict (plan_key) do nothing
            """, p =>
            {
                p.Add(Db.P("id", candidate, NpgsqlDbType.Uuid));
                p.AddWithValue("k", planKey);
                p.AddWithValue("v", targetVersion);
                p.AddWithValue("ar", name.Ar);
                p.AddWithValue("en", name.En);
                p.AddWithValue("t", Canon.Now());
                p.AddWithValue("by", createdBy);
            }, null, ct);

        var migrationId = inserted == 1
            ? candidate
            : (await Db.QueryAsync(c,
                "select migration_id from control.fleet_migration where plan_key = @k",
                r => r.GetGuid(0), p => p.AddWithValue("k", planKey), null, ct))[0];

        var fleet = tenants ?? await registry.ListAsync(TenantStatus.Active, ct);
        var rows = fleet.Where(t => t.IsReachable && t.Isolation == IsolationModel.DatabasePerTenant)
                        .OrderBy(t => t.TenantCode, StringComparer.Ordinal).ToList();
        if (rows.Count == 0) return migrationId;

        // إدراج متعدد الصفوف على دفعات، بصفوف مرتّبة صراحةً.
        const int chunk = 200;
        for (var off = 0; off < rows.Count; off += chunk)
        {
            var slice = rows.Skip(off).Take(chunk).ToList();
            var values = string.Join(", ",
                slice.Select((_, i) => $"(@m, @t{i}, @c{i}, @d{i}, 'Pending', @f{i}, @v)"));
            await Db.WriteIdempotentManyAsync(c, $"""
                insert into control.fleet_migration_target
                    (migration_id, tenant_id, tenant_code, database_name, state, from_version, to_version)
                values {values}
                on conflict (migration_id, tenant_id) do nothing
                """, slice.Count, p =>
                {
                    p.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid));
                    for (var i = 0; i < slice.Count; i++)
                    {
                        p.Add(Db.P($"t{i}", slice[i].TenantId, NpgsqlDbType.Uuid));
                        p.AddWithValue($"c{i}", slice[i].TenantCode);
                        p.AddWithValue($"d{i}", slice[i].DatabaseName);
                        p.AddWithValue($"f{i}", slice[i].SchemaVersion);
                    }
                    p.AddWithValue("v", targetVersion);
                }, null, ct);
        }

        return migrationId;
    }

    /// <summary>
    /// يشتغل حتى لا يبقى هدف قابل للالتقاط. آمن للتشغيل من عدّة عمليات معاً،
    /// وآمن للقتل في أي لحظة.
    /// </summary>
    public async Task<FleetRunReport> RunAsync(Guid migrationId, string workerId,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var processed = 0;
        var failed = 0;
        var alreadyDone = 0;

        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        var targetVersion = (int)(await Db.ScalarAsync<long>(c,
            "select target_version from control.fleet_migration where migration_id = @m",
            p => p.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid)), null, ct));

        while (!ct.IsCancellationRequested)
        {
            var batch = await ClaimBatchAsync(c, migrationId, workerId, ct);
            if (batch.Count == 0) break;

            foreach (var t in batch)
            {
                var itemSw = Stopwatch.StartNew();
                try
                {
                    int applied;
                    int before;
                    await using (var tc = await Db.OpenAsync(
                        options.TenantOwnerConnectionString(t.DatabaseName), ct))
                    {
                        before = await TenantSchema.CurrentVersionAsync(tc, ct);
                        applied = await TenantSchema.MigrateToAsync(tc, targetVersion, ct);
                    }
                    itemSw.Stop();
                    if (applied == 0) alreadyDone++;

                    await MarkAsync(c, migrationId, t.TenantId, "Done", before, targetVersion,
                        (int)itemSw.ElapsedMilliseconds, null, ct);

                    await using (var rc = await Db.OpenAsync(options.ControlConnectionString, ct))
                        await registry.SetSchemaVersionAsync(rc, t.TenantId, targetVersion, null, ct);

                    processed++;
                    if (AfterEach is not null) await AfterEach(t);
                }
                catch (Exception ex) when (ex is not OperationCanceledException
                                              and not SimulatedKillException)
                {
                    itemSw.Stop();
                    failed++;
                    await MarkAsync(c, migrationId, t.TenantId, "Failed", null, targetVersion,
                        (int)itemSw.ElapsedMilliseconds,
                        ex.GetType().Name + ": " + ex.Message.Split('\n')[0], ct);
                }
            }
        }

        sw.Stop();
        return new FleetRunReport(migrationId, workerId, processed, alreadyDone, failed,
            sw.Elapsed, await StatsAsync(migrationId, ct));
    }

    // =======================================================================

    private async Task<List<FleetTarget>> ClaimBatchAsync(NpgsqlConnection c, Guid migrationId,
        string workerId, CancellationToken ct)
    {
        await using var tx = await c.BeginTransactionAsync(ct);

        // الترتيب صريح: tenant_code تصاعدياً. SKIP LOCKED يجعل عمّالاً متعددين
        // يتقاسمون الأسطول بلا تصارع وبلا تكرار.
        var rows = await Db.QueryAsync(c, """
            select tenant_id, tenant_code, database_name, attempts
              from control.fleet_migration_target
             where migration_id = @m
               and (state = 'Pending'
                    or state = 'Failed'
                    or (state = 'Leased' and lease_until < @now))
             order by tenant_code asc
             limit @lim
             for update skip locked
            """,
            r => new FleetTarget(r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt32(3)),
            p =>
            {
                p.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid));
                p.AddWithValue("now", Canon.Now());
                p.AddWithValue("lim", options.FleetBatchSize);
            }, tx, ct);

        foreach (var t in rows)
            await Db.WriteAsync(c, """
                update control.fleet_migration_target
                   set state = 'Leased', lease_owner = @w, lease_until = @until,
                       attempts = attempts + 1,
                       started_at = coalesce(started_at, @now)
                 where migration_id = @m and tenant_id = @t
                """, 1, p =>
                {
                    p.AddWithValue("w", workerId);
                    p.AddWithValue("until", Canon.Now() + options.FleetLeaseDuration);
                    p.AddWithValue("now", Canon.Now());
                    p.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid));
                    p.Add(Db.P("t", t.TenantId, NpgsqlDbType.Uuid));
                }, tx, ct);

        await tx.CommitAsync(ct);
        return rows;
    }

    private static async Task MarkAsync(NpgsqlConnection c, Guid migrationId, Guid tenantId,
        string state, int? fromVersion, int toVersion, int durationMs, string? error,
        CancellationToken ct)
    {
        await Db.WriteAsync(c, """
            update control.fleet_migration_target
               set state = @s,
                   from_version = coalesce(@fv, from_version),
                   to_version = @tv,
                   finished_at = @now,
                   duration_ms = @ms,
                   lease_owner = null,
                   lease_until = null,
                   last_error = @err
             where migration_id = @m and tenant_id = @t
            """, 1, p =>
            {
                p.AddWithValue("s", state);
                p.Add(Db.P("fv", fromVersion, NpgsqlDbType.Integer));
                p.AddWithValue("tv", toVersion);
                p.AddWithValue("now", Canon.Now());
                p.AddWithValue("ms", durationMs);
                p.Add(Db.P("err", error, NpgsqlDbType.Text));
                p.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid));
                p.Add(Db.P("t", tenantId, NpgsqlDbType.Uuid));
            }, null, ct);
    }

    /// <summary>
    /// استرجاع صريح لحجوزات عامل يعلم المشغّل أنه مات. البديل هو انتظار
    /// انتهاء المهلة — وكلاهما مدعوم، والتلقائي هو الافتراضي.
    /// </summary>
    public async Task<int> ReclaimAsync(Guid migrationId, string? deadWorkerId = null,
        CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        await using var cmd = Db.Cmd(c, """
            update control.fleet_migration_target
               set state = 'Pending', lease_owner = null, lease_until = null
             where migration_id = @m and state = 'Leased'
               and (@w is null or lease_owner = @w)
            """);
        cmd.Parameters.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid));
        cmd.Parameters.Add(Db.P("w", deadWorkerId, NpgsqlDbType.Text));
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    public async Task<FleetStats> StatsAsync(Guid migrationId, CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        var rows = await Db.QueryAsync(c, """
            select state, count(*), coalesce(max(attempts),0), coalesce(sum(duration_ms),0)
              from control.fleet_migration_target
             where migration_id = @m
             group by state
             order by state asc
            """,
            r => (State: r.GetString(0), N: r.GetInt64(1), MaxAtt: r.GetInt32(2), Dur: r.GetInt64(3)),
            p => p.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid)), null, ct);

        int N(string s) => (int)(rows.FirstOrDefault(x => x.State == s).N);
        return new FleetStats(
            rows.Sum(x => (int)x.N), N("Pending"), N("Leased"), N("Done"), N("Failed"), N("Skipped"),
            rows.Count == 0 ? 0 : rows.Max(x => x.MaxAtt), rows.Sum(x => x.Dur));
    }

    public async Task<List<(string Code, string State, int Attempts, int? DurationMs)>>
        TargetsAsync(Guid migrationId, CancellationToken ct = default)
    {
        await using var c = await Db.OpenAsync(options.ControlConnectionString, ct);
        return await Db.QueryAsync(c, """
            select tenant_code, state, attempts, duration_ms
              from control.fleet_migration_target
             where migration_id = @m
             order by tenant_code asc
            """,
            r => (r.GetString(0), r.GetString(1), r.GetInt32(2),
                  r.IsDBNull(3) ? (int?)null : r.GetInt32(3)),
            p => p.Add(Db.P("m", migrationId, NpgsqlDbType.Uuid)), null, ct);
    }
}

/// <summary>قتل مُحاكى داخل العملية — للإثباتات.</summary>
public sealed class SimulatedKillException(string message) : Exception(message);
