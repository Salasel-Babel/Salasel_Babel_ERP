using System.Diagnostics;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.Migration;

/// <summary>قاعدة مستأجر واحدة داخل خطة ترحيل، كما التقطها العامل من طابور الأهداف.</summary>
/// <param name="TenantId">معرّف المستأجر.</param>
/// <param name="TenantCode">رمز المستأجر — وهو مفتاح الترتيب الكلّي الذي يمنع تعارض الأقفال.</param>
/// <param name="DatabaseName">اسم قاعدة البيانات المراد ترحيلها.</param>
/// <param name="Attempts">عدد المحاولات على هذا الهدف حتى الآن — يكشف إعادة العمل بعد قتل العملية.</param>
public sealed record FleetTarget(Guid TenantId, string TenantCode, string DatabaseName, int Attempts);

/// <summary>حالة خطة ترحيل مقروءة من قاعدة التحكّم لا من ذاكرة العملية — فتنجو من قتلها.</summary>
/// <param name="Total">عدد قواعد الأسطول في الخطة.</param>
/// <param name="Pending">لم تبدأ بعد.</param>
/// <param name="Leased">محجوزة لدى عامل الآن (‏أو لدى عامل ميّت حتى تنتهي مهلة الحجز).</param>
/// <param name="Done">اكتملت.</param>
/// <param name="Failed">فشلت وستُلتقط ثانية.</param>
/// <param name="Skipped">استُثنيت من الخطة عمداً.</param>
/// <param name="MaxAttempts">أكبر عدد محاولات على قاعدة واحدة — مؤشّر إعادة العمل.</param>
/// <param name="TotalDurationMs">مجموع زمن الترحيل داخل القواعد وحده، بلا زمن الحجز والاتصال.</param>
public sealed record FleetStats(
    int Total, int Pending, int Leased, int Done, int Failed, int Skipped,
    int MaxAttempts, long TotalDurationMs)
{
    /// <summary>اكتملت الخطة: كل قاعدة إمّا انتهت أو استُثنيت عمداً.</summary>
    public bool Complete => Done + Skipped == Total;
}

/// <summary>حصيلة تشغيلة عامل واحد على خطة ترحيل.</summary>
/// <param name="MigrationId">معرّف الخطة.</param>
/// <param name="WorkerId">معرّف العامل.</param>
/// <param name="Processed">عدد القواعد التي عالجها هذا العامل.</param>
/// <param name="AlreadyDone">قواعد وُجدت على الإصدار المستهدف أصلاً — لم يُنفَّذ عليها شيء.</param>
/// <param name="Failed">قواعد فشلت في هذه التشغيلة.</param>
/// <param name="Elapsed">زمن التشغيلة.</param>
/// <param name="Stats">حالة الخطة كاملةً بعد التشغيلة.</param>
public sealed record FleetRunReport(
    Guid MigrationId, string WorkerId, int Processed, int AlreadyDone, int Failed,
    TimeSpan Elapsed, FleetStats Stats)
{
    /// <summary>إنتاجية هذا العامل بالقواعد في الثانية — أساس تقدير زمن الإصدار.</summary>
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
/// <param name="options">إعدادات مستوى التحكّم (‏حجم الدفعة ومهلة الحجز وسلاسل الاتصال).</param>
/// <param name="registry">سجل المستأجرين — يُقرأ منه الأسطول ويُحدَّث فيه إصدار المخطط.</param>
public sealed class FleetMigrationRunner(ControlPlaneOptions options, TenantRegistry registry)
{
    /// <summary>
    /// خطّاف يُنفَّذ بعد كل قاعدة تنتهي. موجود <b>للإثباتات وحدها</b>: به تُبطَّأ
    /// التشغيلة أو تُقتل العملية في منتصفها لإثبات الاستئناف.
    /// </summary>
    public Func<FleetTarget, Task>? AfterEach { get; set; }

    /// <summary>
    /// يبني خطة ترحيل أو يستأنف الخطة نفسها بنفس <c>planKey</c>.
    /// صفوف الأهداف تُدرَج في <b>عبارة واحدة مرتّبة</b> بـ<c>tenant_code</c>.
    /// </summary>
    /// <param name="planKey">مفتاح الخطة الطبيعي — إعادة النداء به تستأنف الخطة نفسها ولا تُنشئ ثانية.</param>
    /// <param name="targetVersion">إصدار المخطط المستهدف.</param>
    /// <param name="name">اسم الخطة بالعربية والإنجليزية.</param>
    /// <param name="createdBy">من أنشأ الخطة.</param>
    /// <param name="tenants">أسطول صريح؛ إن أُهمل قُرئ المستأجرون النشِطون من السجل.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>معرّف الخطة — الجديدة أو القائمة.</returns>
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
    /// <param name="migrationId">معرّف الخطة.</param>
    /// <param name="workerId">معرّف هذا العامل — يُكتب في الحجز ليُعرَف صاحبه.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>حصيلة التشغيلة وحالة الخطة بعدها.</returns>
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
                        await TenantRegistry.SetSchemaVersionAsync(rc, t.TenantId, targetVersion, null, ct);

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
    /// <param name="migrationId">معرّف الخطة.</param>
    /// <param name="deadWorkerId">العامل الميّت؛ إن كان <c>null</c> استُرجِعت كل الحجوزات.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>عدد الحجوزات المُسترجَعة.</returns>
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

    /// <summary>حالة الخطة مقروءة من قاعدة التحكّم.</summary>
    /// <param name="migrationId">معرّف الخطة.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>الإحصاء المُجمَّع لكل الحالات.</returns>
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

    /// <summary>كل أهداف الخطة مفصّلةً، مرتّبةً برمز المستأجر.</summary>
    /// <param name="migrationId">معرّف الخطة.</param>
    /// <param name="ct">رمز الإلغاء.</param>
    /// <returns>قائمة (‏الرمز، الحالة، المحاولات، المدّة بالمللي‌ثانية).</returns>
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
/// <param name="message">وصف نقطة القتل المُحاكاة.</param>
public sealed class SimulatedKillException(string message) : Exception(message);
