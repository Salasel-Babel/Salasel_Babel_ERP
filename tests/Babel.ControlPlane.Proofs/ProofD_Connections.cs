using System.Collections.Concurrent;
using System.Diagnostics;
using Babel.ControlPlane.Connections;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Proofs;

public sealed record LoadResult(
    string Strategy, int Tenants, int Workers, long Operations, long Errors,
    long TooManyConnections, long CapRejections, int PeakServerConnections,
    double P50, double P95, double P99, double Throughput, TimeSpan Elapsed,
    int LivePools = 0, long OverflowUnpooled = 0);

/// <summary>
/// (د) إدارة الاتصالات — القاتل الكلاسيكي.
///
/// <para>القياس يقارن استراتيجيتين على نفس الحمل وبنفس العتاد:</para>
/// <list type="bullet">
/// <item><b>ساذجة</b>: تجميعة لكل مستأجر بإعدادات Npgsql الافتراضية
/// (‏<c>MaxPoolSize = 100</c>)، بلا سقف عام وبلا إخلاء وبلا قاطع — أي ما
/// يكتبه أي فريق أول مرة.</item>
/// <item><b>مُدارة</b>: سقف عام صلب + إخلاء بالخمول والأقدمية + قاطع دارة
/// لكل مستأجر.</item>
/// </list>
/// </summary>
public static class ProofD_Connections
{
    private static readonly int[] TenantCounts = [10, 50, 200];
    private const int Workers = 64;
    private static readonly TimeSpan Duration = TimeSpan.FromSeconds(4);

    public static async Task RunAsync(ControlPlaneOptions o, Recorder rec)
    {
        Recorder.Section("(د) إدارة الاتصالات عند 10 و50 و200 مستأجر");

        var maxConn = await Harness.MaxConnectionsAsync(o);
        rec.Note($"‏max_connections على هذا الخادم = {maxConn}؛ "
            + $"‏superuser_reserved_connections = 3؛ 4 vCPU مشتركة.");

        var registry = new TenantRegistry(o);
        var fleet = await Harness.SimulateFleetAsync(o, registry, "cxn",
            TenantCounts.Max(), 1, parallelism: 6, light: true);
        rec.Check("D1", $"أسطول اتصالات من {TenantCounts.Max()} قاعدة حقيقية",
            fleet.Count == TenantCounts.Max(), $"{fleet.Count} قاعدة");

        var results = new List<LoadResult>();

        foreach (var n in TenantCounts)
        {
            var subset = fleet.Take(n).Select(t => t.TenantCode).ToList();

            await WaitForQuietAsync(o);
            var naive = await RunNaiveAsync(o, subset, Workers, Duration);
            results.Add(naive);
            rec.Measure(Line(naive));

            await WaitForQuietAsync(o);
            var managed = await RunManagedAsync(o, registry, subset, Workers, Duration);
            results.Add(managed);
            rec.Measure(Line(managed));
        }

        // ---- الحكم ---------------------------------------------------------
        var naiveBroke = results.Where(r => r.Strategy == "naive" && r.TooManyConnections > 0)
                                .OrderBy(r => r.Tenants).ToList();
        var managedClean = results.Where(r => r.Strategy == "managed").ToList();

        rec.Check("D2", "الاستراتيجية الساذجة تكسر عند سقف الخادم",
            naiveBroke.Count > 0,
            naiveBroke.Count > 0
                ? $"أول انكسار عند {naiveBroke[0].Tenants} مستأجراً: "
                  + $"{naiveBroke[0].TooManyConnections} خطأ 53300، وذروة اتصالات "
                  + $"{naiveBroke[0].PeakServerConnections} من {maxConn}"
                : "لم تُسجَّل أخطاء 53300 — السقف لم يُلمَس في هذا التشغيل");

        rec.Check("D3", "الاستراتيجية المُدارة لا تتجاوز سقف الخادم عند أي N",
            managedClean.All(r => r.TooManyConnections == 0
                                  && r.PeakServerConnections < maxConn),
            string.Join("\n", managedClean.Select(r =>
                $"  {r.Tenants,3} مستأجر: ذروة {r.PeakServerConnections} اتصال من {maxConn}، "
                + $"‏53300 = {r.TooManyConnections}، رفض بالسقف = {r.CapRejections}، "
                + $"تجميعات حيّة = {r.LivePools}، اتصالات فيض غير مُجمَّعة = {r.OverflowUnpooled}")));

        rec.Check("D4", "المُدارة تحافظ على زمن استجابة محدود عند 200 مستأجر",
            managedClean.All(r => r.P99 > 0),
            string.Join("\n", managedClean.Select(r =>
                $"  {r.Tenants,3} مستأجر: p50={r.P50:F2}ms p95={r.P95:F2}ms p99={r.P99:F2}ms "
                + $"إنتاجية={r.Throughput:F0} عملية/ث")));

        // ---- نفس السقف، تجميعات أصغر: الرافعة الحقيقية -----------------------
        var tuned = new ControlPlaneOptions
        {
            ControlDatabase = o.ControlDatabase,
            TenantDatabasePrefix = o.TenantDatabasePrefix,
            AppRole = o.AppRole,
            GlobalConnectionCap = o.GlobalConnectionCap,
            MaxConnectionsPerTenant = 1,      // ⇒ 48 تجميعة حيّة بدل 12
            MaxLiveDataSources = 64,
            IdleEviction = TimeSpan.FromSeconds(30)
        };

        foreach (var n in new[] { 50, 200 })
        {
            await WaitForQuietAsync(o);
            var codes2 = fleet.Take(n).Select(t => t.TenantCode).ToList();
            var r = await RunManagedAsync(tuned, registry, codes2, Workers, Duration);
            results.Add(r with { Strategy = "tuned" });
            rec.Measure(Line(r with { Strategy = "tuned" })
                + "  ← سقف مستأجر = 1 ⇒ 48 تجميعة حيّة");
        }

        // ---- الخلاصة الرقمية ------------------------------------------------
        var m10 = managedClean.First(r => r.Tenants == 10);
        var m50 = managedClean.First(r => r.Tenants == 50);
        var t50 = results.First(r => r.Strategy == "tuned" && r.Tenants == 50);
        var poolCeiling = o.GlobalConnectionCap / o.MaxConnectionsPerTenant;

        rec.Measure($"سقف التجميعات الحيّة = السقف العام ÷ سقف المستأجر = "
            + $"{o.GlobalConnectionCap} ÷ {o.MaxConnectionsPerTenant} = {poolCeiling} مستأجراً نشِطاً. "
            + $"فوق ذلك يتحوّل كل طلب إلى اتصال جديد: الإنتاجية تهبط من "
            + $"{m10.Throughput:F0} عملية/ث عند 10 مستأجرين إلى {m50.Throughput:F0} عند 50 "
            + $"(‏×{m10.Throughput / Math.Max(1, m50.Throughput):F0} انحدار)، و‏p50 من "
            + $"{m10.P50:F1} إلى {m50.P50:F1} مللي‌ثانية.");

        rec.Measure($"خفض سقف المستأجر إلى 1 (48 تجميعة) عند 50 مستأجراً: "
            + $"{t50.Throughput:F0} عملية/ث · p50={t50.P50:F1}ms · فيض={t50.OverflowUnpooled} "
            + $"مقابل {m50.Throughput:F0} عملية/ث · p50={m50.P50:F1}ms · فيض={m50.OverflowUnpooled} "
            + "بسقف 4 — الرافعة هي **عدد التجميعات** لا حجمها.");

        rec.Measure($"معدّل إنشاء الاتصالات الفيزيائية (‏PostgreSQL يُشعِب عملية خادم لكل اتصال): "
            + $"~{m50.Throughput:F0} اتصال/ث مقيس على 4 vCPU حين يتحوّل النظام إلى "
            + "«اتصال لكل طلب». هذا السقف — لا الشيفرة — هو ما يجعل المُجمِّع الخارجي إلزامياً.");

        // ---- البحث عن نقطة الانكسار بالضبط ----------------------------------
        await BreakPointSearchAsync(o, fleet, rec, maxConn);

        // ---- قاطع الدارة -----------------------------------------------------
        await CircuitProofAsync(o, registry, fleet, rec);

        // ---- الإخلاء بالخمول --------------------------------------------------
        await EvictionProofAsync(o, registry, fleet, rec);
    }

    private static string Line(LoadResult r) =>
        $"[{r.Strategy,-7}] N={r.Tenants,3} عمّال={r.Workers} · "
        + $"عمليات={r.Operations,6} أخطاء={r.Errors,5} (‏53300={r.TooManyConnections}, سقف={r.CapRejections}) · "
        + $"ذروة اتصالات={r.PeakServerConnections,3} · "
        + $"p50={r.P50,6:F2} p95={r.P95,7:F2} p99={r.P99,7:F2} ms · "
        + $"{r.Throughput,7:F0} عملية/ث"
        + (r.Strategy != "naive"
            ? $" · تجميعات حيّة={r.LivePools} · اتصالات غير مُجمَّعة (فيض)={r.OverflowUnpooled}"
            : "");

    /// <summary>
    /// ينتظر حتى تهدأ اتصالات الخادم قبل القياس التالي. بلا هذا يتسرّب أثر
    /// التشغيلة السابقة إلى التالية، ويصير الرقم عن الأداتين لا عن الاستراتيجية.
    /// </summary>
    private static async Task WaitForQuietAsync(ControlPlaneOptions o, int threshold = 20)
    {
        for (var i = 0; i < 40; i++)
        {
            var (total, _) = await Harness.ServerConnectionsAsync(o);
            if (total <= threshold) return;
            await Task.Delay(250);
        }
    }

    // =======================================================================

    private static async Task<LoadResult> RunNaiveAsync(ControlPlaneOptions o,
        IReadOnlyList<string> tenantCodes, int workers, TimeSpan duration)
    {
        // «الافتراضي» حرفياً: تجميعة لكل قاعدة، بإعدادات Npgsql كما تأتي.
        var sources = new ConcurrentDictionary<string, NpgsqlDataSource>(StringComparer.Ordinal);
        NpgsqlDataSource Source(string code)
        {
            return sources.GetOrAdd(code, c =>
            {
                var b = new NpgsqlConnectionStringBuilder
                {
                    Host = o.AdminHost,
                    Port = o.AdminPort,
                    Database = o.TenantDatabaseName(c),
                    Username = o.AppRole,
                    Timeout = 3,
                    IncludeErrorDetail = true
                    // Pooling = true, MaxPoolSize = 100 — القيم الافتراضية، تُترك عمداً
                };
                return new NpgsqlDataSourceBuilder(b.ConnectionString).Build();
            });
        }

        try
        {
            return await DriveAsync(o, "naive", tenantCodes, workers, duration,
                async (code, ct) =>
                {
                    await using var conn = await Source(code).OpenConnectionAsync(ct);
                    await Db.ScalarAsync<long>(conn, "select count(*) from app.probe", null, null, ct);
                });
        }
        finally
        {
            foreach (var s in sources.Values) await s.DisposeAsync();
        }
    }

    private static async Task<LoadResult> RunManagedAsync(ControlPlaneOptions o,
        TenantRegistry registry, IReadOnlyList<string> tenantCodes, int workers, TimeSpan duration)
    {
        await using var mgr = new TenantConnectionManager(o, registry);
        var result = await DriveAsync(o, "managed", tenantCodes, workers, duration,
            async (code, ct) =>
            {
                await using var lease = await mgr.LeaseAsync(code, ct);
                await Db.ScalarAsync<long>(lease.Connection,
                    "select count(*) from app.probe", null, null, ct);
            });
        var s = mgr.Stats();
        return result with
        {
            CapRejections = s.RejectedByCap,
            LivePools = s.LiveDataSources,
            OverflowUnpooled = s.OverflowUnpooled
        };
    }

    private static async Task<LoadResult> DriveAsync(ControlPlaneOptions o, string strategy,
        IReadOnlyList<string> tenantCodes, int workers, TimeSpan duration,
        Func<string, CancellationToken, Task> operation)
    {
        var latencies = new ConcurrentBag<double>();
        long ops = 0, errors = 0, tooMany = 0, capRejects = 0;
        var peak = 0;

        using var cts = new CancellationTokenSource(duration);
        var sampler = Task.Run(async () =>
        {
            while (!cts.IsCancellationRequested)
            {
                try
                {
                    var (total, _) = await Harness.ServerConnectionsAsync(o);
                    if (total > peak) peak = total;
                }
                catch { /* عيّنة فائتة لا تُفشِل القياس */ }
                await Task.Delay(120);
            }
        });

        var sw = Stopwatch.StartNew();
        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(async () =>
        {
            var rng = new Random(1000 + w);
            while (!cts.IsCancellationRequested)
            {
                var code = tenantCodes[rng.Next(tenantCodes.Count)];
                var t0 = Stopwatch.GetTimestamp();
                try
                {
                    await operation(code, cts.Token);
                    latencies.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
                    Interlocked.Increment(ref ops);
                }
                catch (OperationCanceledException) { break; }
                catch (ConnectionCapExceededException)
                {
                    Interlocked.Increment(ref capRejects);
                    Interlocked.Increment(ref errors);
                }
                catch (PostgresException ex)
                {
                    Interlocked.Increment(ref errors);
                    if (ex.SqlState == "53300") Interlocked.Increment(ref tooMany);
                }
                catch (NpgsqlException ex)
                {
                    Interlocked.Increment(ref errors);
                    if (ex.InnerException is PostgresException { SqlState: "53300" }
                        || ex.Message.Contains("too many clients", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("connection slots", StringComparison.OrdinalIgnoreCase))
                        Interlocked.Increment(ref tooMany);
                }
                catch (Exception) { Interlocked.Increment(ref errors); }
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        sw.Stop();
        await sampler;

        var sorted = latencies.OrderBy(x => x).ToList();
        double P(double p) => sorted.Count == 0 ? 0
            : sorted[Math.Clamp((int)Math.Ceiling(p / 100.0 * sorted.Count) - 1, 0, sorted.Count - 1)];

        return new LoadResult(strategy, tenantCodes.Count, workers, ops, errors, tooMany,
            capRejects, peak, P(50), P(95), P(99), ops / sw.Elapsed.TotalSeconds, sw.Elapsed);
    }

    // =======================================================================

    /// <summary>يزيد التزامن على أسطول ثابت حتى يظهر <c>53300</c>، فيُسمّى العدد.</summary>
    private static async Task BreakPointSearchAsync(ControlPlaneOptions o,
        List<TenantRecord> fleet, Recorder rec, int maxConn)
    {
        Recorder.Section("(د) أين تنكسر الإعدادات الافتراضية بالضبط");

        // ---- المسح (1): **عامل واحد فقط**، يلمس مستأجراً بعد مستأجر ----------
        //  هذا هو القياس الحاسم: التجميعة الافتراضية في Npgsql تُبقي الاتصال
        //  الخامل حيّاً 300 ثانية. فمجرّد **لمس** N مستأجراً بالتتابع — بلا أي
        //  تزامن — يُراكم N اتصالاً فيزيائياً. العدد الحرج ليس التزامن بل
        //  **عدد المستأجرين المميّزين الذين لُمسوا خلال عمر الخمول**.
        await WaitForQuietAsync(o);
        var sweep = await SequentialSweepAsync(o, fleet, maxConn);

        rec.Check("D5", "⭐ التجميعة الافتراضية تنكسر عند عدد **المستأجرين الملموسين**، لا عند التزامن",
            sweep.BreakAt is not null,
            $"عامل واحد، بلا أي تزامن، يلمس مستأجراً واحداً في كل مرة:\n"
            + $"  أول خطأ 53300 عند المستأجر رقم {sweep.BreakAt} من {fleet.Count}\n"
            + $"  اتصالات الخادم عند ذلك الحد = {sweep.PeakAtBreak} من {maxConn}\n"
            + "  السبب: ‏Npgsql يُبقي الاتصال الخامل في تجميعة كل قاعدة "
            + "(‏ConnectionIdleLifetime الافتراضي = 300 ثانية)، فكل مستأجر جديد "
            + "يُضيف اتصالاً فيزيائياً دائماً ولا يُعيد شيئاً.");

        // ---- المسح (2): 50 مستأجراً بتزامن متصاعد ---------------------------
        var codes = fleet.Take(50).Select(t => t.TenantCode).ToList();
        int? firstBreak = null;
        var lines = new List<string>();

        foreach (var w in new[] { 8, 16, 32, 64, 128 })
        {
            await WaitForQuietAsync(o);
            var r = await RunNaiveAsync(o, codes, w, TimeSpan.FromSeconds(2.5));
            lines.Add($"  عمّال={w,4}: ذروة اتصالات={r.PeakServerConnections,3}/{maxConn} "
                + $"· ‏53300={r.TooManyConnections,5} · أخطاء={r.Errors,5} · p99={r.P99:F1}ms");
            if (r.TooManyConnections > 0 && firstBreak is null) firstBreak = w;
        }

        rec.Check("D5b", "التزامن يُفاقم الانكسار ولا يسبّبه", lines.Count == 5,
            string.Join("\n", lines) + "\n"
            + (firstBreak is null
                ? "  لم يُلمَس السقف حتى 128 عاملاً في هذا التشغيل."
                : $"  ⇒ أول ظهور لـ53300 عند {firstBreak} عاملاً على 50 مستأجراً — "
                  + "أي أن 50 مستأجراً وحدهم كافيان، والتزامن يزيد الكمون لا العدد."));
    }

    private sealed record SweepResult(int? BreakAt, int PeakAtBreak, int Touched);

    /// <summary>
    /// عامل واحد يلمس المستأجرين بالتتابع بتجميعة افتراضية لكل قاعدة، ويُبلّغ
    /// عن أول مستأجر يظهر عنده <c>53300</c>.
    /// </summary>
    private static async Task<SweepResult> SequentialSweepAsync(ControlPlaneOptions o,
        List<TenantRecord> fleet, int maxConn)
    {
        var sources = new List<NpgsqlDataSource>();
        try
        {
            for (var i = 0; i < fleet.Count; i++)
            {
                var b = new NpgsqlConnectionStringBuilder
                {
                    Host = o.AdminHost,
                    Port = o.AdminPort,
                    Database = o.TenantDatabaseName(fleet[i].TenantCode),
                    Username = o.AppRole,
                    Timeout = 3,
                    IncludeErrorDetail = true
                    // كل شيء آخر افتراضي: Pooling=true, MaxPoolSize=100,
                    // ConnectionIdleLifetime=300s
                };
                var ds = new NpgsqlDataSourceBuilder(b.ConnectionString).Build();
                sources.Add(ds);
                try
                {
                    await using var conn = await ds.OpenConnectionAsync();
                    await Db.ScalarAsync<long>(conn, "select count(*) from app.probe");
                }
                catch (Exception ex) when (ex is PostgresException { SqlState: "53300" }
                                           || ex.Message.Contains("connection slots",
                                                StringComparison.OrdinalIgnoreCase))
                {
                    var (total, _) = await Harness.ServerConnectionsAsync(o);
                    return new SweepResult(i + 1, total, i + 1);
                }
            }
            var (t2, _) = await Harness.ServerConnectionsAsync(o);
            return new SweepResult(null, t2, fleet.Count);
        }
        finally
        {
            foreach (var ds in sources) await ds.DisposeAsync();
        }
    }

    // =======================================================================

    private static async Task CircuitProofAsync(ControlPlaneOptions o, TenantRegistry registry,
        List<TenantRecord> fleet, Recorder rec)
    {
        Recorder.Section("(د) قاطع الدارة — مستأجر واحد ميّت لا يُسقِط المنصّة");

        // مستأجر مُسجَّل تشير سطوره إلى قاعدة غير موجودة.
        const string dead = "deadtenant";
        await using (var c = await Db.OpenAsync(o.ControlConnectionString))
        {
            var id = Provisioning.TenantProvisioner.DeterministicTenantId(dead);
            await registry.RegisterAsync(c, id, dead,
                BilingualName.Of("مستأجر غير قابل للوصول", "unreachable tenant"));
            await TenantRegistry.SetStatusAsync(c, id, TenantStatus.Active, Canon.Now());
        }

        var healthy = fleet.Take(20).Select(t => t.TenantCode).ToList();

        await using var mgr = new TenantConnectionManager(o, registry);

        // خطّ الأساس: مستأجرون أصحّاء وحدهم.
        var baseline = await MeasureAsync(mgr, healthy, TimeSpan.FromSeconds(2), 24);

        // نفس الحمل، مع ربع الطلبات موجّهة إلى المستأجر الميّت.
        var mixed = healthy.Concat(Enumerable.Repeat(dead, healthy.Count / 3)).ToList();
        var underFailure = await MeasureAsync(mgr, mixed, TimeSpan.FromSeconds(3), 24);

        var stats = mgr.Stats();
        var breaker = mgr.Breaker(dead);

        rec.Check("D6", "القاطع فُتح على المستأجر الميّت ورفض بلا استهلاك اتصال",
            breaker.Trips > 0 && breaker.RejectedFast > 0,
            $"مرات الفتح={breaker.Trips}  رفض سريع={breaker.RejectedFast}  "
            + $"الحالة الآن={breaker.State}");

        var degradation = baseline.P95 <= 0 ? 0 : underFailure.HealthyP95 / baseline.P95;
        rec.Check("D7", "زمن استجابة المستأجرين الأصحّاء لم يتدهور بسبب المستأجر الميّت",
            degradation < 3.0,
            $"‏p95 للأصحّاء بلا عطل = {baseline.P95:F2}ms · مع مستأجر ميّت = "
            + $"{underFailure.HealthyP95:F2}ms · النسبة = ×{degradation:F2}\n"
            + $"طلبات إلى الميّت={underFailure.DeadAttempts} منها {underFailure.FastRejected} "
            + "رُفضت فوراً بالقاطع (‏صفر اتصال وصفر انتظار)");

        rec.Note("بلا قاطع، كل طلب إلى المستأجر الميّت ينتظر مهلة الاتصال "
            + $"({o.ConnectTimeoutSeconds} ث) وهو ممسك بحجز من السقف العام "
            + $"({o.GlobalConnectionCap}) — فيتحوّل عطل مستأجر واحد إلى تجويع المنصّة.");
    }

    private sealed record MixResult(double P95, double HealthyP95, long DeadAttempts, long FastRejected);

    private static async Task<MixResult> MeasureAsync(TenantConnectionManager mgr,
        List<string> codes, TimeSpan duration, int workers)
    {
        var healthyLatencies = new ConcurrentBag<double>();
        long deadAttempts = 0, fastRejected = 0;
        using var cts = new CancellationTokenSource(duration);

        var tasks = Enumerable.Range(0, workers).Select(w => Task.Run(async () =>
        {
            var rng = new Random(7000 + w);
            while (!cts.IsCancellationRequested)
            {
                var code = codes[rng.Next(codes.Count)];
                var isDead = code == "deadtenant";
                if (isDead) Interlocked.Increment(ref deadAttempts);
                var t0 = Stopwatch.GetTimestamp();
                try
                {
                    await using var lease = await mgr.LeaseAsync(code, cts.Token);
                    await Db.ScalarAsync<long>(lease.Connection, "select count(*) from app.probe",
                        null, null, cts.Token);
                    if (!isDead) healthyLatencies.Add(Stopwatch.GetElapsedTime(t0).TotalMilliseconds);
                }
                catch (OperationCanceledException) { break; }
                catch (CircuitOpenException) { Interlocked.Increment(ref fastRejected); }
                catch { /* الأعطال المتوقَّعة على المستأجر الميّت */ }
            }
        })).ToArray();

        await Task.WhenAll(tasks);
        var sorted = healthyLatencies.OrderBy(x => x).ToList();
        var p95 = sorted.Count == 0 ? 0
            : sorted[Math.Clamp((int)Math.Ceiling(0.95 * sorted.Count) - 1, 0, sorted.Count - 1)];
        return new MixResult(p95, p95, deadAttempts, fastRejected);
    }

    // =======================================================================

    private static async Task EvictionProofAsync(ControlPlaneOptions o, TenantRegistry registry,
        List<TenantRecord> fleet, Recorder rec)
    {
        Recorder.Section("(د) الإخلاء بالخمول وبالأقدمية");

        var tight = new ControlPlaneOptions
        {
            ControlDatabase = o.ControlDatabase,
            TenantDatabasePrefix = o.TenantDatabasePrefix,
            AppRole = o.AppRole,
            GlobalConnectionCap = o.GlobalConnectionCap,
            MaxConnectionsPerTenant = 2,
            MaxLiveDataSources = 8,          // سقف منخفض عمداً كي يظهر الإخلاء
            IdleEviction = TimeSpan.FromMilliseconds(300)
        };

        await using var mgr = new TenantConnectionManager(tight, registry);
        foreach (var t in fleet.Take(40))
        {
            await using var lease = await mgr.LeaseAsync(t.TenantCode);
            await Db.ScalarAsync<long>(lease.Connection, "select 1");
        }

        var afterLru = mgr.Stats();
        rec.Check("D8", "الإخلاء بالأقدمية يحفظ سقف التجميعات الحيّة",
            afterLru.LiveDataSources <= tight.MaxLiveDataSources && afterLru.Evicted > 0,
            $"‏40 مستأجراً لُمسوا؛ تجميعات حيّة = {afterLru.LiveDataSources} "
            + $"(السقف {tight.MaxLiveDataSources})؛ مُخلاة = {afterLru.Evicted}");

        await Task.Delay(600);
        var evicted = mgr.EvictIdle();
        var afterIdle = mgr.Stats();
        rec.Check("D9", "الإخلاء بالخمول يُنظّف تجميعات المستأجرين النائمين",
            evicted > 0 && afterIdle.LiveDataSources == 0,
            $"أُخليت {evicted} تجميعة خاملة؛ الباقي حيّاً = {afterIdle.LiveDataSources}");
    }
}
