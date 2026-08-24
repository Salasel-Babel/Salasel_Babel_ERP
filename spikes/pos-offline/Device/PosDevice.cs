using System.Globalization;
using BabelPosOffline.Support;
using Microsoft.Data.Sqlite;

namespace BabelPosOffline.Device;

public sealed record SaleItem(string ItemCode, decimal Qty, decimal UnitPrice, decimal VatRate);

public sealed record SaleRecord(
    string SaleId, string IdemKey, long InvoiceNo, long ChainSeq, string DocType,
    DateOnly BusinessDate, DateTime DeviceClockAt, decimal TotalNet, decimal TotalVat, decimal TotalGross,
    string EntryHash, bool PastCeiling);

public enum BacklogLevel { Green, Warn, Critical, AtCeiling }

public sealed record BacklogStatus(int Pending, BacklogAge Age, BacklogLevel Level, bool TradingAllowed, string Reason);

public sealed class TradingBlockedException(string code, string message) : Exception(message)
{
    public string Code { get; } = code;
}

/// <summary>
/// جهاز نقطة بيع مستقل: وحدة إصدار لها عدّادها ومداها وسلسلتها.
/// كل الكتابة تمر من هنا، تماماً كما تمر كتابة الخادم من <c>Ledger.PostAsync</c>.
/// </summary>
public sealed class PosDevice : IDisposable
{
    public LocalStore Store { get; }
    public string DeviceId { get; }
    public string TenantId { get; }
    public DeviceClock Clock { get; }
    public PosSettings Settings { get; private set; }
    public string ShiftId { get; private set; } = "SH-INIT";

    public const string AccCash = "1101";
    public const string AccRevenue = "4101";
    public const string AccVatPayable = "2301";

    private PosDevice(LocalStore store, string deviceId, string tenantId, DeviceClock clock, PosSettings settings)
    { Store = store; DeviceId = deviceId; TenantId = tenantId; Clock = clock; Settings = settings; }

    public static PosDevice Open(string path, string deviceId, string tenantId,
                                 DeviceClock? clock = null, PosSettings? settings = null, bool fullSync = true)
    {
        var store = new LocalStore(path, fullSync);
        store.ApplySchema();
        var d = new PosDevice(store, deviceId, tenantId, clock ?? new DeviceClock(), settings ?? PosSettings.Default);
        store.Exec("""
            insert into device_identity (singleton, device_id, tenant_id, branch_id, registered_at)
            values (1, $d, $t, 'BR-01', $now) on conflict (singleton) do nothing
            """, ("$d", deviceId), ("$t", tenantId), ("$now", Canonical.Store(d.Clock.WallUtcNow)));
        d.RecordBoot();
        d.DetectClockStepAgainstHistory();
        return d;
    }

    public void ApplySettings(PosSettings s) => Settings = s;

    // ── دفتر أزمنة التشغيل: يمنح حدّاً أدنى لعمر التراكم عبر إعادات الإقلاع ─────
    private void RecordBoot()
    {
        var seq = Store.Scalar<long>("select coalesce(max(seq),0) from uptime_ledger") + 1;
        Store.Exec("""
            insert into uptime_ledger (boot_id, started_wall, accum_ms, seq) values ($b, $w, 0, $s)
            on conflict (boot_id) do nothing
            """, ("$b", Clock.BootId), ("$w", Canonical.Store(Clock.WallUtcNow)), ("$s", seq));
    }

    /// <summary>
    /// يُستدعى دورياً (وقبل كل قرار سقف): يثبّت زمن التشغيل المتراكم، <b>ويكشف قفزة ساعة
    /// داخل دورة التشغيل نفسها</b> بمقارنة ما تقوله ساعة الحائط بما تقوله الساعة الرتيبة.
    /// هذه المقارنة هي الوحيدة التي لا يستطيع أحد تعطيلها من إعدادات الجهاز.
    /// </summary>
    public void Heartbeat()
    {
        var mono = Clock.MonotonicMs;
        var wall = Clock.WallUtcNow;
        var prev = Store.Query("select last_wall, last_mono_ms from uptime_ledger where boot_id = $b",
            r => (r.IsDBNull(0) ? null : r.GetString(0), r.IsDBNull(1) ? (long?)null : r.GetInt64(1)),
            ("$b", Clock.BootId));
        if (prev.Count > 0 && prev[0].Item1 is not null && prev[0].Item2 is not null)
        {
            var expected = Canonical.Parse(prev[0].Item1!).AddMilliseconds(mono - prev[0].Item2!.Value);
            var drift = (long)(wall - expected).TotalMilliseconds;
            if (Math.Abs(drift) > 60_000)
                RaiseClockEvent(drift < 0 ? "step_back" : "step_forward", drift,
                    $"wall clock moved {TimeSpan.FromMilliseconds(drift).TotalHours:F2}h relative to the monotonic clock within one boot");
        }
        Store.Exec("update uptime_ledger set accum_ms = $m, last_wall = $w, last_mono_ms = $m2 where boot_id = $b",
                   ("$m", mono), ("$w", Canonical.Store(wall)), ("$m2", mono), ("$b", Clock.BootId));
    }

    private void DetectClockStepAgainstHistory()
    {
        var lastWall = Store.Scalar<string>(
            "select started_wall from uptime_ledger where boot_id <> $b order by seq desc limit 1", ("$b", Clock.BootId));
        if (lastWall is null) return;
        var delta = Clock.WallUtcNow - Canonical.Parse(lastWall);
        if (delta < TimeSpan.Zero)
            RaiseClockEvent("step_back", (long)delta.TotalMilliseconds,
                $"wall clock at boot is {(-delta).TotalHours:F2}h BEFORE the previous boot's wall clock");
    }

    public void RaiseClockEvent(string kind, long deltaMs, string detail) =>
        Store.Exec("""
            insert into clock_event (event_id, detected_at, boot_id, kind, delta_ms, detail)
            values ($i, $t, $b, $k, $d, $x)
            """, ("$i", Guid.CreateVersion7().ToString("N")), ("$t", Canonical.Store(Clock.WallUtcNow)),
                 ("$b", Clock.BootId), ("$k", kind), ("$d", deltaMs), ("$x", detail));

    public void RaiseException(string kind, string severity, string detail) =>
        Store.Exec("""
            insert into local_exception (exception_id, raised_at, kind, severity, detail)
            values ($i, $t, $k, $s, $d)
            """, ("$i", Guid.CreateVersion7().ToString("N")), ("$t", Canonical.Store(Clock.WallUtcNow)),
                 ("$k", kind), ("$s", severity), ("$d", detail));

    // ── الساعة المرجعية: مرساة الخادم + الساعة الرتيبة، لا ساعة الحائط ────────
    /// <summary>
    /// «الآن» بتقدير الخادم: آخر زمن خادم معروف + ما مضى على الساعة الرتيبة منذ تلك اللحظة.
    /// هذا هو ما يحدّد <b>التاريخ المحاسبي</b>. ساعة الحائط لا تحدّده أبداً.
    /// </summary>
    public (DateTime Now, bool Anchored) ServerEstimatedNow()
    {
        var anchorUtc = Store.Scalar<string>("select last_contact_server_utc from sync_checkpoint where singleton = 1");
        var anchorMono = Store.Scalar<long?>("select last_contact_monotonic_ms from sync_checkpoint where singleton = 1");
        var anchorBoot = Store.Scalar<string>("select last_contact_boot_id from sync_checkpoint where singleton = 1");
        if (anchorUtc is null || anchorMono is null || anchorBoot != Clock.BootId)
            return (Clock.WallUtcNow, false);   // لا مرساة صالحة في دورة التشغيل هذه
        return (Canonical.PgInstant(Canonical.Parse(anchorUtc).AddMilliseconds(Clock.MonotonicMs - anchorMono.Value)), true);
    }

    public DateOnly ResolveBusinessDate()
    {
        var (now, anchored) = ServerEstimatedNow();
        if (!anchored)
            RaiseException("BUSINESS_DATE_FROM_DEVICE_CLOCK", "warn",
                $"no valid server anchor in this boot; business date taken from device wall clock {Canonical.Store(now)}");
        return DateOnly.FromDateTime(now);
    }

    public void OpenShift(string? id = null) =>
        ShiftId = id ?? $"SH-{Canonical.Date(ResolveBusinessDate())}-{DeviceId}";

    // ── المدى المحجوز ────────────────────────────────────────────────────────
    public void InstallRange(string rangeId, long start, long end)
    {
        Store.Exec("""
            insert into number_range (range_id, range_start, range_end, state, granted_at)
            values ($r, $s, $e, 'active', $g) on conflict (range_id) do nothing
            """, ("$r", rangeId), ("$s", start), ("$e", end), ("$g", Canonical.Store(Clock.WallUtcNow)));
        Store.Exec("""
            insert into device_counter (singleton, next_no, next_seq) values (1, $s, 1)
            on conflict (singleton) do nothing
            """, ("$s", start));
    }

    public (long Start, long End, long Remaining)? ActiveRange()
    {
        var next = Store.Scalar<long?>("select next_no from device_counter where singleton = 1");
        if (next is null) return null;
        var rows = Store.Query("select range_start, range_end from number_range where state = 'active' order by range_start",
            r => (r.GetInt64(0), r.GetInt64(1)));
        foreach (var (s, e) in rows)
            if (next.Value >= s && next.Value <= e) return (s, e, e - next.Value + 1);
        // العدّاد خارج كل مدى نشط
        return rows.Count > 0 ? (rows[^1].Item1, rows[^1].Item2, 0) : null;
    }

    // ── حالة التراكم وسقف نافذة الإبلاغ ──────────────────────────────────────
    public BacklogStatus Backlog()
    {
        Heartbeat();
        var pending = (int)Store.Scalar<long>("select count(*) from sale where sync_state in ('pending','inflight')");
        if (pending == 0)
            return new BacklogStatus(0, new BacklogAge(TimeSpan.Zero, AgeConfidence.Monotonic, TimeSpan.Zero, TimeSpan.Zero),
                                     BacklogLevel.Green, true, "no pending sales");

        var oldest = Store.Query("""
            select device_clock_at, monotonic_ms, boot_id from sale
            where sync_state in ('pending','inflight') order by chain_seq limit 1
            """, r => (Canonical.Parse(r.GetString(0)), r.GetInt64(1), r.GetString(2)))[0];

        // مجموع أزمنة التشغيل المُثبَّتة في دورات لاحقة لدورة أقدم عملية معلّقة
        var accum = Store.Scalar<long>("""
            select coalesce(sum(accum_ms),0) from uptime_ledger
            where seq > coalesce((select seq from uptime_ledger where boot_id = $b), 0) and boot_id <> $cur
            """, ("$b", oldest.Item3), ("$cur", Clock.BootId));
        if (oldest.Item3 != Clock.BootId)
            accum += Store.Scalar<long>("select coalesce(accum_ms,0) from uptime_ledger where boot_id = $b", ("$b", oldest.Item3))
                     - oldest.Item2;

        var clockSuspect = Store.Scalar<long>("select count(*) from clock_event") > 0;
        var age = AgeEstimator.Estimate(oldest.Item1, oldest.Item2, oldest.Item3, Clock, accum, clockSuspect);

        var ceiling = Settings.ReportingCeiling;
        var level = age.Age >= ceiling ? BacklogLevel.AtCeiling
                  : age.Age >= ceiling * Settings.CriticalAtFraction ? BacklogLevel.Critical
                  : age.Age >= ceiling * Settings.WarnAtFraction ? BacklogLevel.Warn
                  : BacklogLevel.Green;

        var allowed = level != BacklogLevel.AtCeiling || Settings.AtCeiling == CeilingBehaviour.ContinueWithAlarm;
        var reason = level switch
        {
            BacklogLevel.AtCeiling when !allowed =>
                $"CEILING_REACHED: oldest unsynced sale is {age} >= ceiling {ceiling.TotalHours:F0}h; policy = StopTrading",
            BacklogLevel.AtCeiling =>
                $"CEILING_REACHED but policy = ContinueWithAlarm; every further sale is flagged past_ceiling",
            _ => $"{level}: {age}"
        };
        return new BacklogStatus(pending, age, level, allowed, reason);
    }

    // ── مسار الكتابة الوحيد ──────────────────────────────────────────────────
    public SaleRecord RecordSale(IReadOnlyList<SaleItem> items, string docType = "SALE", string? originalIdemKey = null)
    {
        if (items.Count == 0) throw new InvalidOperationException("a sale needs at least one line");

        var backlog = Backlog();
        if (!backlog.TradingAllowed)
            throw new TradingBlockedException("CEILING_REACHED", backlog.Reason);

        var range = ActiveRange() ?? throw new TradingBlockedException("NO_RANGE", "no reserved number range installed");
        if (range.Remaining <= 0)
        {
            RaiseException("RANGE_EXHAUSTED", "block", $"reserved range {range.Start}-{range.End} is exhausted");
            if (Settings.AtRangeExhaustion == RangeExhaustionBehaviour.StopTrading)
                throw new TradingBlockedException("RANGE_EXHAUSTED",
                    $"reserved range {range.Start}-{range.End} exhausted and the device is offline; policy = StopTrading");
            throw new TradingBlockedException("RANGE_EXHAUSTED_NO_FALLBACK",
                "range exhausted; ContinueWithAlarm still cannot invent invoice numbers - a spare range must be pre-installed");
        }
        if (range.Remaining <= Settings.RangeSize * Settings.RangeRefillAtRemainingFraction)
            RaiseException("RANGE_LOW", "warn", $"only {range.Remaining} numbers left in range {range.Start}-{range.End}");

        // حساب المبالغ بـ decimal، والتقريب خطوة صريحة مُسجَّلة لا أثراً جانبياً للتخزين
        var lines = new List<CanonLine>();
        long netMinor = 0, vatMinor = 0;
        for (int i = 0; i < items.Count; i++)
        {
            var it = items[i];
            var lineNet = decimal.Round(it.Qty * it.UnitPrice, Money.Scale, MidpointRounding.AwayFromZero);
            var lineVat = decimal.Round(lineNet * it.VatRate, Money.Scale, MidpointRounding.AwayFromZero);
            var ln = Money.ToMinor(lineNet, $"line {i + 1} net");
            var lv = Money.ToMinor(lineVat, $"line {i + 1} vat");
            netMinor += ln; vatMinor += lv;
            lines.Add(new CanonLine(i + 1, it.ItemCode, Money.QtyToMinor(it.Qty),
                                    Money.ToMinor(it.UnitPrice, $"line {i + 1} price"), ln, lv));
        }
        var grossMinor = netMinor + vatMinor;

        var jl = docType == "RETURN"
            ? new List<CanonJournalLine>
              {
                  new(1, AccRevenue,    netMinor,   0),
                  new(2, AccVatPayable, vatMinor,   0),
                  new(3, AccCash,       0,          grossMinor)
              }
            : new List<CanonJournalLine>
              {
                  new(1, AccCash,       grossMinor, 0),
                  new(2, AccRevenue,    0,          netMinor),
                  new(3, AccVatPayable, 0,          vatMinor)
              };

        var businessDate = ResolveBusinessDate();
        var deviceClockAt = Clock.WallUtcNow;                  // غير موثوقة، لكنها مُوثَّقة وثابتة
        var monoMs = Clock.MonotonicMs;
        var pastCeiling = backlog.Level == BacklogLevel.AtCeiling;

        using var tx = Store.Connection.BeginTransaction(System.Data.IsolationLevel.Serializable, deferred: false);
        try
        {
            long invoiceNo, chainSeq;
            using (var c = Store.Connection.CreateCommand())
            {
                c.Transaction = tx;
                c.CommandText = "select next_no, next_seq from device_counter where singleton = 1";
                using var r = c.ExecuteReader();
                if (!r.Read()) throw new InvalidOperationException("device counter missing");
                invoiceNo = r.GetInt64(0); chainSeq = r.GetInt64(1);
            }

            byte[] prevHash;
            using (var c = Store.Connection.CreateCommand())
            {
                c.Transaction = tx;
                c.CommandText = "select entry_hash from sale where chain_seq = $s";
                c.Parameters.AddWithValue("$s", chainSeq - 1);
                var v = c.ExecuteScalar();
                prevHash = v is string s2 ? Canonical.UnHex(s2) : Canonical.DeviceGenesis(TenantId, DeviceId);
            }

            var saleId = Guid.CreateVersion7().ToString("D");
            var idemKey = IdemKey(TenantId, DeviceId, docType, invoiceNo);
            var view = new SaleCanonicalView
            {
                TenantId = TenantId, DeviceId = DeviceId, SaleId = saleId, DocType = docType,
                InvoiceNo = invoiceNo, DeviceSeq = chainSeq, PrevHash = prevHash,
                BusinessDate = Canonical.Date(businessDate), DeviceClockAt = Canonical.Store(deviceClockAt),
                ShiftId = ShiftId, OriginalIdemKey = originalIdemKey, Currency = "SAR",
                TotalNetMinor = netMinor, TotalVatMinor = vatMinor, TotalGrossMinor = grossMinor,
                Lines = lines, JournalLines = jl
            };
            var entryHash = Canonical.HashSale(view);
            var payloadHash = Canonical.PayloadHash(view);

            ExecTx(tx, """
                insert into sale (sale_id, idem_key, device_id, doc_type, invoice_no, chain_seq, business_date,
                                  device_clock_at, monotonic_ms, boot_id, shift_id, original_idem_key, currency,
                                  total_net_minor, total_vat_minor, total_gross_minor, prev_hash, entry_hash,
                                  payload_hash, past_ceiling, sealed)
                values ($id,$ik,$dev,$dt,$no,$sq,$bd,$dc,$mm,$bt,$sh,$ok,'SAR',$n,$v,$g,$ph,$eh,$yh,$pc,0)
                """,
                ("$id", saleId), ("$ik", idemKey), ("$dev", DeviceId), ("$dt", docType), ("$no", invoiceNo),
                ("$sq", chainSeq), ("$bd", Canonical.Date(businessDate)), ("$dc", Canonical.Store(deviceClockAt)),
                ("$mm", monoMs), ("$bt", Clock.BootId), ("$sh", ShiftId), ("$ok", (object?)originalIdemKey),
                ("$n", netMinor), ("$v", vatMinor), ("$g", grossMinor),
                ("$ph", Canonical.Hex(prevHash)), ("$eh", Canonical.Hex(entryHash)),
                ("$yh", Canonical.Hex(payloadHash)), ("$pc", pastCeiling ? 1 : 0));

            foreach (var l in lines)
                ExecTx(tx, """
                    insert into sale_line (sale_id, line_no, item_code, qty_minor, unit_price_minor, line_net_minor, line_vat_minor)
                    values ($s,$l,$i,$q,$u,$n,$v)
                    """, ("$s", saleId), ("$l", l.LineNo), ("$i", l.ItemCode), ("$q", l.QtyMinor),
                         ("$u", l.UnitPriceMinor), ("$n", l.LineNetMinor), ("$v", l.LineVatMinor));

            foreach (var j in jl)
                ExecTx(tx, """
                    insert into journal_line (sale_id, line_no, account_code, debit_minor, credit_minor)
                    values ($s,$l,$a,$d,$c)
                    """, ("$s", saleId), ("$l", j.LineNo), ("$a", j.AccountCode), ("$d", j.DebitMinor), ("$c", j.CreditMinor));

            // الختم: هنا يعمل مشغّل التوازن
            ExecTx(tx, "update sale set sealed = 1 where sale_id = $s", ("$s", saleId));
            ExecTx(tx, "update device_counter set next_no = next_no + 1, next_seq = next_seq + 1 where singleton = 1");

            tx.Commit();   // fsync هنا تحت synchronous=FULL

            return new SaleRecord(saleId, idemKey, invoiceNo, chainSeq, docType, businessDate, deviceClockAt,
                Money.FromMinor(netMinor), Money.FromMinor(vatMinor), Money.FromMinor(grossMinor),
                Canonical.Hex(entryHash), pastCeiling);
        }
        catch { tx.Rollback(); throw; }
    }

    private static void ExecTx(SqliteTransaction tx, string sql, params (string, object?)[] ps)
    {
        using var c = tx.Connection!.CreateCommand();
        c.Transaction = tx; c.CommandText = sql;
        foreach (var (k, v) in ps) c.Parameters.AddWithValue(k, v ?? DBNull.Value);
        c.ExecuteNonQuery();
    }

    /// <summary>
    /// مفتاح الحصانة. <b>يحمل هوية الجهاز والنوع داخله.</b> مفتاح مبني على رقم الفاتورة
    /// وحده يتصادم بين جهازين — والتصادم مع <c>ON CONFLICT DO NOTHING</c> يعني
    /// <b>ابتلاع عملية بيع حقيقية بصمت</b>. انظر الإثبات (3-و).
    /// </summary>
    public static string IdemKey(string tenantId, string deviceId, string docType, long invoiceNo) =>
        $"{tenantId}|{deviceId}|{docType}|{invoiceNo}";

    /// <summary>
    /// الشكل الخاطئ الشائع: مفتاح مبني على تسلسل الجهاز المحلي، وهو يبدأ من 1 على كل جهاز.
    /// جهازان يولّدان المفتاح نفسه لعمليتَي بيع مختلفتين تماماً. للإثبات المضاد فقط.
    /// </summary>
    public static string NaiveIdemKey(string tenantId, string docType, long deviceSeq) =>
        $"{tenantId}|{docType}|{deviceSeq}";

    public void Dispose() => Store.Dispose();
}
