using BabelPosOffline.Server;
using BabelPosOffline.Support;

namespace BabelPosOffline.Device;

public sealed record SyncRunResult(int Sent, int Posted, int Duplicate, int Quarantined, int Rejected,
                                   int Mismatch, int Batches, int Shed, TimeSpan Elapsed)
{
    public override string ToString() =>
        $"sent={Sent} posted={Posted} dup={Duplicate} quar={Quarantined} rej={Rejected} mismatch={Mismatch} " +
        $"batches={Batches} shed={Shed} in {Elapsed.TotalMilliseconds:F0} ms";
}

/// <summary>
/// عميل المزامنة على الجهاز: تجميع بالدفعات، ترتيب، استئناف بعد فشل جزئي، وضغط عكسي.
///
/// قاعدة الاستئناف: كل ما هو <c>inflight</c> عند الإقلاع يعود <c>pending</c>. الجهاز
/// <b>لا يعرف</b> إن كان الخادم استلمه أم لا، ولا يحتاج أن يعرف — الحصانة لكل قيد
/// تجعل إعادة الإرسال بلا أثر. <b>هذا هو السبب الحقيقي لوجود الحصانة</b>: لا التكرار
/// العرضي، بل استحالة معرفة العميل بمصير رسالته.
/// </summary>
public sealed class SyncClient(PosDevice device, SyncServer server)
{
    public int BatchSize { get; private set; } = 100;

    /// <summary>
    /// سقف صلب لعدد المحاولات لكل دفعة. <b>الافتراضي: لا سقف.</b>
    /// السقف الصلب هو بالضبط ما أنتج تجويع الأجهزة في القياس (7-ج): تحت ذروة عودة
    /// من انقطاع إقليمي، جهاز يُرفض ست مرات متتالية «ينهي» مزامنته وقد بقي في طابوره
    /// آلاف القيود — بلا خطأ ظاهر. يُترك هنا قابلاً للضبط <b>للإثبات المضاد فقط</b>.
    /// </summary>
    public int? HardAttemptCap { get; init; }

    /// <summary>مهلة التصفية الكلية: بعدها يتوقّف السعي ويبقى الطابور سليماً على الجهاز.</summary>
    public TimeSpan DrainDeadline { get; init; } = TimeSpan.FromMinutes(10);

    /// <summary>عدد المرات التي رُفضت فيها دفعة بضغط عكسي خلال آخر تشغيل.</summary>
    public int LastShed { get; private set; }

    /// <summary>يُستدعى عند إقلاع الجهاز: كل ما كان في الطريق يعود إلى الطابور.</summary>
    public int RecoverInflight()
    {
        var n = (int)device.Store.Scalar<long>("select count(*) from sale where sync_state = 'inflight'");
        device.Store.Exec("update sale set sync_state = 'pending' where sync_state = 'inflight'");
        return n;
    }

    public int PendingCount() => (int)device.Store.Scalar<long>("select count(*) from sale where sync_state = 'pending'");

    public List<SyncEntry> NextBatch(int max)
    {
        var ids = device.Store.Query(
            "select sale_id from sale where sync_state = 'pending' order by chain_seq limit $n",
            r => r.GetString(0), ("$n", max));
        return [.. ids.Select(BuildEntry)];
    }

    public SyncEntry BuildEntry(string saleId)
    {
        var s = device.Store.Query("""
            select sale_id, idem_key, device_id, doc_type, invoice_no, chain_seq, business_date, device_clock_at,
                   shift_id, original_idem_key, total_net_minor, total_vat_minor, total_gross_minor,
                   prev_hash, entry_hash, payload_hash, past_ceiling
            from sale where sale_id = $s
            """, r => new
            {
                Id = r.GetString(0), Idem = r.GetString(1), Dev = r.GetString(2), Type = r.GetString(3),
                No = r.GetInt64(4), Seq = r.GetInt64(5), Bd = r.GetString(6), Clock = r.GetString(7),
                Shift = r.GetString(8), Orig = r.IsDBNull(9) ? null : r.GetString(9),
                Net = r.GetInt64(10), Vat = r.GetInt64(11), Gross = r.GetInt64(12),
                Prev = r.GetString(13), Hash = r.GetString(14), Pay = r.GetString(15), Past = r.GetInt64(16) == 1
            }, ("$s", saleId))[0];

        var lines = device.Store.Query("""
            select line_no, item_code, qty_minor, unit_price_minor, line_net_minor, line_vat_minor
            from sale_line where sale_id = $s order by line_no
            """, r => new SyncLine(r.GetInt32(0), r.GetString(1),
                Money.CanonicalQty(Money.QtyFromMinor(r.GetInt64(2))),
                Money.CanonicalMinor(r.GetInt64(3)), Money.CanonicalMinor(r.GetInt64(4)), Money.CanonicalMinor(r.GetInt64(5))),
            ("$s", saleId));

        var jls = device.Store.Query("""
            select line_no, account_code, debit_minor, credit_minor from journal_line where sale_id = $s order by line_no
            """, r => new SyncJournalLine(r.GetInt32(0), r.GetString(1),
                Money.CanonicalMinor(r.GetInt64(2)), Money.CanonicalMinor(r.GetInt64(3))), ("$s", saleId));

        return new SyncEntry
        {
            IdemKey = s.Idem, SaleId = s.Id, TenantId = device.TenantId, DeviceId = s.Dev, DocType = s.Type,
            InvoiceNo = s.No, DeviceSeq = s.Seq, BusinessDate = s.Bd, DeviceClockAt = Canonical.Parse(s.Clock),
            ShiftId = s.Shift, OriginalIdemKey = s.Orig,
            TotalNet = Money.FromMinor(s.Net), TotalVat = Money.FromMinor(s.Vat), TotalGross = Money.FromMinor(s.Gross),
            PrevHash = Canonical.UnHex(s.Prev), EntryHash = Canonical.UnHex(s.Hash), PayloadHash = Canonical.UnHex(s.Pay),
            PastCeiling = s.Past, Lines = lines, JournalLines = jls
        };
    }

    private void Mark(IEnumerable<string> keys, string state)
    {
        foreach (var k in keys)
            device.Store.Exec("update sale set sync_state = $s where idem_key = $k", ("$s", state), ("$k", k));
    }

    public async Task<SyncRunResult> RunAsync(CancellationToken ct = default)
    {
        var sw = System.Diagnostics.Stopwatch.StartNew();
        int sent = 0, posted = 0, dup = 0, quar = 0, rej = 0, mism = 0, batches = 0, shed = 0;

        while (!ct.IsCancellationRequested)
        {
            var batch = NextBatch(BatchSize);
            if (batch.Count == 0) break;

            Mark(batch.Select(e => e.IdemKey), "inflight");
            var payload = new SyncBatch(device.TenantId, device.DeviceId,
                                        Guid.CreateVersion7().ToString("N"), device.Clock.WallUtcNow, batch,
                                        device.Store.Scalar<long>("select next_no from device_counter where singleton = 1"));

            SyncResponse? resp = null;
            var deadline = DateTime.UtcNow + DrainDeadline;
            for (int attempt = 1; ; attempt++)
            {
                int serverHint = 0;
                try
                {
                    resp = await server.SyncAsync(payload, ct);
                    if (resp.Accepted) break;
                    shed++;
                    serverHint = resp.RetryAfterMs;
                    BatchSize = Math.Max(10, Math.Min(BatchSize, resp.MaxBatchSize));   // نقصان ضربي
                    resp = null;
                }
                catch (IOException) { /* انقطاع نقل: تبقى inflight وتعود pending عند الاستئناف */ }

                if (HardAttemptCap is int cap && attempt >= cap) break;
                if (DateTime.UtcNow >= deadline) break;

                // تراجع أُسّي بتشويش كامل: يفكّك القطيع بدل أن يعيده كتلة واحدة
                var backoff = Math.Min(8000, 100 * Math.Pow(2, Math.Min(attempt, 7)));
                var wait = Math.Max(serverHint, (int)(backoff * (0.5 + Random.Shared.NextDouble())));
                await Task.Delay(wait, ct);
            }

            if (resp is null || !resp.Accepted)
            {
                Mark(batch.Select(e => e.IdemKey), "pending");   // نُعيدها إلى الطابور ونتوقّف
                break;
            }

            batches++; sent += batch.Count;
            foreach (var a in resp.Acks)
            {
                switch (a.Outcome)
                {
                    case EntryOutcome.Posted: posted++; MarkAck(a, "acked"); break;
                    case EntryOutcome.Duplicate: dup++; MarkAck(a, "acked"); break;
                    case EntryOutcome.Quarantined: quar++; MarkAck(a, "quarantined"); break;
                    case EntryOutcome.Rejected: rej++; MarkAck(a, "rejected"); break;
                    case EntryOutcome.ConflictMismatch: mism++; MarkAck(a, "rejected"); break;
                    default: MarkAck(a, "pending"); break;
                }
            }
            // ما لم يصل عنه إقرار يعود إلى الطابور صراحةً
            var acked = resp.Acks.Select(a => a.IdemKey).ToHashSet();
            Mark(batch.Where(e => !acked.Contains(e.IdemKey)).Select(e => e.IdemKey), "pending");

            BatchSize = Math.Clamp(resp.MaxBatchSize, 10, 500);   // زيادة جمعية عند الرخاء
            RecordAnchor(resp);
        }

        LastShed = shed;
        return new SyncRunResult(sent, posted, dup, quar, rej, mism, batches, shed, sw.Elapsed);
    }

    private void MarkAck(EntryAck a, string state) =>
        device.Store.Exec("update sale set sync_state = $s, acked_at = $t, server_note = $n where idem_key = $k",
            ("$s", state), ("$t", Canonical.Store(device.Clock.WallUtcNow)), ("$n", a.Note), ("$k", a.IdemKey));

    /// <summary>
    /// مرساة الوقت: زمن الخادم لحظة النجاح + قيمة الساعة الرتيبة في اللحظة نفسها.
    /// هذه المرساة — لا ساعة الحائط — هي ما يشتق منه التاريخ المحاسبي لاحقاً.
    /// </summary>
    private void RecordAnchor(SyncResponse r)
    {
        device.Store.Exec("""
            insert into sync_checkpoint (singleton, last_contact_server_utc, last_contact_monotonic_ms,
                                         last_contact_boot_id, server_skew_ms, batches_sent)
            values (1, $u, $m, $b, $k, 1)
            on conflict (singleton) do update set
                last_contact_server_utc = excluded.last_contact_server_utc,
                last_contact_monotonic_ms = excluded.last_contact_monotonic_ms,
                last_contact_boot_id = excluded.last_contact_boot_id,
                server_skew_ms = excluded.server_skew_ms,
                batches_sent = sync_checkpoint.batches_sent + 1
            """, ("$u", Canonical.Store(r.ServerUtc)), ("$m", device.Clock.MonotonicMs),
                 ("$b", device.Clock.BootId), ("$k", r.ServerMinusDeviceMs));

        if (Math.Abs(r.ServerMinusDeviceMs) > device.Settings.ClockSkewEscalateAbove.TotalMilliseconds)
        {
            device.RaiseClockEvent("server_skew", r.ServerMinusDeviceMs,
                $"device wall clock is {TimeSpan.FromMilliseconds(-r.ServerMinusDeviceMs).TotalHours:F2}h from server time");
            device.RaiseException("CLOCK_SKEW", "warn",
                $"server-device skew {r.ServerMinusDeviceMs} ms exceeds the configured escalation threshold");
        }
    }
}
