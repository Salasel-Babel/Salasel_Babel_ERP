using System.Collections.Concurrent;
using System.Diagnostics;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Npgsql;

namespace Babel.ControlPlane.Connections;

public sealed class ConnectionCapExceededException(int cap, TimeSpan waited)
    : Exception($"تعذّر حجز اتصال خلال {waited.TotalSeconds:F1} ثانية — السقف العام {cap} مستهلَك بالكامل. "
                + "الرفض السريع مقصود: الانتظار غير المحدود يحوّل ضغطاً على مستأجر إلى تعطّل منصّة.");

/// <summary>حجز اتصال. التخلّص منه يُعيد الاتصال إلى تجميعة المستأجر ويُحرّر الحجز العام.</summary>
public sealed class TenantLease : IAsyncDisposable
{
    private readonly Action _release;
    private bool _disposed;

    internal TenantLease(NpgsqlConnection connection, string tenantCode, Action release)
    {
        Connection = connection;
        TenantCode = tenantCode;
        _release = release;
    }

    public NpgsqlConnection Connection { get; }
    public string TenantCode { get; }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        _disposed = true;
        try { await Connection.DisposeAsync(); }
        finally { _release(); }
    }
}

public sealed record ConnectionManagerStats(
    int LiveDataSources, int LeasesInFlight, int GlobalCap, int MaxLivePools,
    long Leased, long FastRejectedByCircuit, long RejectedByCap, long Evicted,
    long OverflowUnpooled, int OpenCircuits);

/// <summary>
/// إدارة الاتصالات لكل مستأجر — القاتل الكلاسيكي عند مئات القواعد.
///
/// <para><b>الحساب الذي يُفاجئ الجميع:</b> تجميعة لكل قاعدة × N قاعدة تتجاوز
/// <c>max_connections</c> قبل أن يشكّ أحد. على هذا الجهاز <c>max_connections
/// = 100</c>؛ فمئة مستأجر بتجميعة افتراضية (‏Npgsql: <c>MaxPoolSize = 100</c>)
/// تعني سقفاً نظرياً 10,000 اتصال مقابل 100 متاحة — والانهيار يقع عند
/// <b>عشرات قليلة</b> من المستأجرين النشِطين، لا عند المئة.</para>
///
/// <para><b>ثلاث آليات، وكلها ضرورية:</b></para>
/// <list type="number">
/// <item><b>سقف صلب عام</b> على الاتصالات المستعمَلة في آن واحد، برفض سريع
/// عند التجاوز بدل انتظار غير محدود.</item>
/// <item><b>إخلاء بالخمول وبالأقدمية</b>: مصدر بيانات مستأجر لم يُستعمل
/// يُتخلَّص منه، فلا تتراكم تجميعات لمستأجرين نائمين.</item>
/// <item><b>قاطع دارة لكل مستأجر</b>: قاعدة واحدة غير قابلة للوصول لا تحجز
/// السقف العام بانتظار المهلة.</item>
/// </list>
/// </summary>
public sealed class TenantConnectionManager : IAsyncDisposable
{
    private sealed class Entry
    {
        public required NpgsqlDataSource DataSource { get; init; }
        public required string DatabaseName { get; init; }
        public long LastUsedTicks;
        public int InFlight;
    }

    private readonly ControlPlaneOptions _options;
    private readonly TenantRegistry _registry;
    private readonly SemaphoreSlim _globalCap;
    private readonly ConcurrentDictionary<string, Entry> _sources = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, TenantCircuitBreaker> _breakers = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, (DateTimeOffset At, TenantRecord Rec)> _routes =
        new(StringComparer.Ordinal);
    private readonly Lock _evictGate = new();

    private long _leased;
    private long _rejectedByCap;
    private long _evicted;
    private long _overflow;
    private int _inFlight;

    public TenantConnectionManager(ControlPlaneOptions options, TenantRegistry registry)
    {
        _options = options;
        _registry = registry;
        _globalCap = new SemaphoreSlim(options.GlobalConnectionCap, options.GlobalConnectionCap);
    }

    public int GlobalCap => _options.GlobalConnectionCap;

    /// <summary>
    /// <b>الثابتة التي يقوم عليها السقف الصلب كلّه:</b>
    /// <c>(عدد التجميعات الحيّة) × (سقف المستأجر) ≤ (السقف العام)</c>.
    ///
    /// <para>بدونها يكون السقف العام سقفاً على الاتصالات <b>المستعمَلة</b> فقط،
    /// بينما تحتفظ تجميعة كل مستأجر باتصالات <b>خاملة</b> لا يحسبها أحد —
    /// وهذا بالضبط ما قِيس: عند 50 مستأجراً وسقف عام 48 وسقف مستأجر 4، بلغت
    /// اتصالات الخادم 97 من 100 وظهر <c>53300</c>. السقف كان قائماً والانهيار وقع.</para>
    /// </summary>
    public int EffectiveMaxLivePools => Math.Max(1, Math.Min(
        _options.MaxLiveDataSources,
        _options.GlobalConnectionCap / Math.Max(1, _options.MaxConnectionsPerTenant)));

    /// <summary>
    /// عدد الطلبات التي لم تجد فتحة تجميعة فخدمها اتصال <b>غير مُجمَّع</b>
    /// تحت الحجز العام. ارتفاعه هو المؤشّر التشغيلي الذي يقول:
    /// <b>عدد المستأجرين النشِطين في آن واحد تجاوز ما يحتمله التجميع لكل قاعدة</b>
    /// ⇒ آن أوان مُجمِّع خارجي.
    /// </summary>
    public long OverflowUnpooled => Interlocked.Read(ref _overflow);

    /// <summary>
    /// عمر ذاكرة التوجيه المؤقّتة. <b>قصير عمداً وبلا استثناء:</b> الأرشفة قد
    /// تقع من عملية أخرى (‏أداة تشغيل، عامل خلفي)، وذاكرة بلا انتهاء صلاحية
    /// تُبقي هذه العملية توجّه الطلبات إلى مستأجر مؤرشف <b>إلى الأبد</b> —
    /// وهو عطل صامت: لا خطأ، ولا سجل، والقاعدة ترفض الاتصال فيبدو الأمر
    /// «عطل شبكة».
    /// </summary>
    public TimeSpan RouteCacheTtl { get; init; } = TimeSpan.FromSeconds(15);

    public TenantCircuitBreaker Breaker(string tenantCode) =>
        _breakers.GetOrAdd(tenantCode, _ =>
            new TenantCircuitBreaker(_options.CircuitFailureThreshold, _options.CircuitOpenDuration));

    /// <summary>
    /// مُحلّل التوجيه — بذرة ADR-0009. اليوم يقرأ <c>isolation_model</c> ويرفض
    /// ما لم يُبنَ بعد صراحةً بدل أن يفترض. تحويل المشروع إلى قاعدة مشتركة
    /// يبدأ من هنا، لا من كل مسار استعلام.
    /// </summary>
    public async Task<TenantRecord> ResolveAsync(string tenantCode, CancellationToken ct = default)
    {
        if (_routes.TryGetValue(tenantCode, out var cached)
            && cached.Rec.IsReachable
            && DateTimeOffset.UtcNow - cached.At < RouteCacheTtl)
            return cached.Rec;

        var t = await _registry.FindByCodeAsync(tenantCode, ct)
                ?? throw new TenantNotFoundException(tenantCode);

        if (t.Status == TenantStatus.Archived)
        {
            _routes.TryRemove(tenantCode, out _);
            await DropSourceAsync(tenantCode);
            throw new TenantArchivedException(tenantCode);
        }

        if (t.Isolation == IsolationModel.SharedSchema)
            throw new NotSupportedException(
                $"المستأجر «{tenantCode}» مُسجَّل على نموذج «مخطط مشترك». "
                + "ADR-0009 مفتوح عمداً: العمود موجود والمُحلّل يقرؤه، والمسار المشترك "
                + "لم يُبنَ بعد. يُبنى هنا حين يُحسم القرار، لا في كل استعلام.");

        _routes[tenantCode] = (DateTimeOffset.UtcNow, t);
        return t;
    }

    public void InvalidateRoute(string tenantCode) => _routes.TryRemove(tenantCode, out _);

    // =======================================================================

    public async Task<TenantLease> LeaseAsync(string tenantCode, CancellationToken ct = default)
    {
        var breaker = Breaker(tenantCode);
        breaker.ThrowIfOpen(tenantCode);           // ① رفض سريع: صفر اتصال، صفر انتظار

        var tenant = await ResolveAsync(tenantCode, ct);

        var sw = Stopwatch.StartNew();
        if (!await _globalCap.WaitAsync(_options.LeaseTimeout, ct))   // ② السقف الصلب
        {
            Interlocked.Increment(ref _rejectedByCap);
            throw new ConnectionCapExceededException(_options.GlobalConnectionCap, sw.Elapsed);
        }

        Entry? entry = null;
        var released = false;
        void Release()
        {
            if (released) return;
            released = true;
            if (entry is not null) Interlocked.Decrement(ref entry.InFlight);
            Interlocked.Decrement(ref _inFlight);
            _globalCap.Release();
        }

        try
        {
            Interlocked.Increment(ref _inFlight);
            entry = GetOrCreate(tenantCode, tenant.DatabaseName);

            NpgsqlConnection conn;
            if (entry is null)
            {
                // لا فتحة تجميعة متاحة: يُخدَم الطلب باتصال **غير مُجمَّع** تحت
                // الحجز العام نفسه. الاتصالات الفيزيائية تبقى محكومة بالسقف،
                // والثمن كمون فتح اتصال لكل طلب — وهو بالضبط الثمن الذي يوجد
                // مُجمِّع خارجي (‏PgBouncer بوضع المعاملة) ليُلغيه.
                Interlocked.Increment(ref _overflow);
                conn = new NpgsqlConnection(
                    _options.TenantAppProbeConnectionString(tenant.DatabaseName));
                await conn.OpenAsync(ct);
            }
            else
            {
                Interlocked.Increment(ref entry.InFlight);
                Volatile.Write(ref entry.LastUsedTicks, DateTimeOffset.UtcNow.Ticks);
                conn = await entry.DataSource.OpenConnectionAsync(ct);
            }

            breaker.RecordSuccess();
            Interlocked.Increment(ref _leased);
            return new TenantLease(conn, tenantCode, Release);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            breaker.RecordFailure();
            Release();
            throw;
        }
    }

    /// <summary>
    /// يُعيد تجميعة المستأجر، أو <c>null</c> إن لم تكن هناك فتحة — عندها
    /// يُخدَم الطلب باتصال غير مُجمَّع (انظر <see cref="OverflowUnpooled"/>).
    /// </summary>
    private Entry? GetOrCreate(string tenantCode, string databaseName)
    {
        if (_sources.TryGetValue(tenantCode, out var existing)) return existing;

        lock (_evictGate)
        {
            if (_sources.TryGetValue(tenantCode, out existing)) return existing;
            if (_sources.Count >= EffectiveMaxLivePools && EvictOne() == 0) return null;

            var b = new NpgsqlDataSourceBuilder(
                _options.TenantAppConnectionString(databaseName, _options.MaxConnectionsPerTenant));
            var e = new Entry
            {
                DataSource = b.Build(),
                DatabaseName = databaseName,
                LastUsedTicks = DateTimeOffset.UtcNow.Ticks
            };
            _sources[tenantCode] = e;
            return e;
        }
    }

    // =======================================================================

    /// <summary>
    /// إخلاء بالخمول ثم بالأقدمية. لا يُخلى مصدر عليه حجز قائم — التخلّص من
    /// <c>NpgsqlDataSource</c> وعليه اتصالات مستعمَلة يقطعها في منتصف عملها.
    /// </summary>
    public int EvictIdle(DateTimeOffset? now = null)
    {
        var cutoff = (now ?? DateTimeOffset.UtcNow) - _options.IdleEviction;
        var removed = 0;
        foreach (var (code, e) in _sources.ToArray())
        {
            if (Volatile.Read(ref e.InFlight) > 0) continue;
            if (new DateTimeOffset(Volatile.Read(ref e.LastUsedTicks), TimeSpan.Zero) > cutoff) continue;
            if (!_sources.TryRemove(code, out var got)) continue;
            _ = got.DataSource.DisposeAsync().AsTask();
            Interlocked.Increment(ref _evicted);
            removed++;
        }
        return removed;
    }

    /// <summary>يُخلي أقدم تجميعة غير مشغولة. يُرجِع 1 إن أُخليت، و0 إن كان الكل مشغولاً.</summary>
    private int EvictOne()
    {
        var victim = _sources.ToArray()
            .Where(kv => Volatile.Read(ref kv.Value.InFlight) == 0)
            .OrderBy(kv => Volatile.Read(ref kv.Value.LastUsedTicks))
            .Select(kv => (KeyValuePair<string, Entry>?)kv)
            .FirstOrDefault();
        if (victim is null) return 0;                       // الكل مشغول: لا نقطع عملاً جارياً
        if (!_sources.TryRemove(victim.Value.Key, out var got)) return 0;
        _ = got.DataSource.DisposeAsync().AsTask();
        Interlocked.Increment(ref _evicted);
        return 1;
    }

    private async Task DropSourceAsync(string tenantCode)
    {
        if (_sources.TryRemove(tenantCode, out var e)) await e.DataSource.DisposeAsync();
    }

    public ConnectionManagerStats Stats() => new(
        _sources.Count, Volatile.Read(ref _inFlight), _options.GlobalConnectionCap,
        EffectiveMaxLivePools,
        Interlocked.Read(ref _leased),
        _breakers.Values.Sum(b => b.RejectedFast),
        Interlocked.Read(ref _rejectedByCap),
        Interlocked.Read(ref _evicted),
        Interlocked.Read(ref _overflow),
        _breakers.Values.Count(b => b.State == CircuitState.Open));

    public async ValueTask DisposeAsync()
    {
        foreach (var (_, e) in _sources.ToArray()) await e.DataSource.DisposeAsync();
        _sources.Clear();
        _globalCap.Dispose();
    }
}
