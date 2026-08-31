using System.Globalization;
using System.Text.Json;
using BabelPosOffline.Support;
using Npgsql;

namespace BabelPosOffline.Server;

/// <summary>
/// دفعة مزامنة. <c>DeviceNextNo</c> هو أعلى رقم أصدره الجهاز + 1 لحظة الإرسال،
/// ويُبلَّغ عنه حتى لو لم تُرسَل تلك القيود بعد. هو ما يجعل «فاتورة صدرت ولم تصل»
/// قابلة للاكتشاف أصلاً.
/// </summary>
public sealed record SyncBatch(string TenantId, string DeviceId, string BatchId,
                               DateTime DeviceClockAtSend, IReadOnlyList<SyncEntry> Entries,
                               long DeviceNextNo = 0);

public enum BatchTxMode
{
    /// <summary>معاملة لكل قيد: قيد مسموم واحد لا يمنع 1,499 قيداً سليماً. <b>هذا هو الصحيح.</b></summary>
    PerEntry,
    /// <summary>معاملة واحدة للدفعة كلها: أسرع قليلاً، وتُنتج انسداد رأس الطابور. للإثبات المضاد.</summary>
    PerBatch
}

/// <summary>
/// طرف الخادم. يقبل الدفعات بأي ترتيب، ويعطي إقراراً <b>لكل قيد على حدة</b>،
/// ويضغط عكسياً بالرفض المبكّر بدل التكديس غير المحدود.
/// </summary>
public sealed class SyncServer(string connectionString, int maxConcurrent = 16)
{
    private int _inflight;
    private long _shed;
    private long _entriesIngested;

    public int MaxConcurrent { get; } = maxConcurrent;
    public long ShedCount => Interlocked.Read(ref _shed);
    public long EntriesIngested => Interlocked.Read(ref _entriesIngested);
    public int Inflight => Volatile.Read(ref _inflight);
    public BatchTxMode TxMode { get; set; } = BatchTxMode.PerEntry;
    public string OrphanPolicy { get; set; } = "Quarantine";

    /// <summary>عطل عابر محقون لاختبار الاستئناف / injected transient failure for resume tests.</summary>
    public Func<SyncEntry, int, bool>? FailAfter { get; set; }

    public async Task<SyncResponse> SyncAsync(SyncBatch batch, CancellationToken ct = default)
    {
        // ── الضغط العكسي: رفض مبكّر بمهلة إعادة محاولة، لا طابور غير محدود ──
        var now = Interlocked.Increment(ref _inflight);
        try
        {
            if (now > MaxConcurrent)
            {
                Interlocked.Increment(ref _shed);
                var jitter = Random.Shared.Next(250, 1500);
                return new SyncResponse(false, "SERVER_BUSY", [], Math.Max(10, batch.Entries.Count / 2),
                                        jitter, DateTime.UtcNow, 0);
            }

            var acks = new List<EntryAck>(batch.Entries.Count);
            await using var conn = await Sql.OpenAsync(connectionString);
            if (batch.DeviceNextNo > 0)
            {
                await using var hw = new NpgsqlCommand(
                    "update pos.device set last_reported_next_no = greatest(coalesce(last_reported_next_no, 0), @n), " +
                    "last_contact_at = now() where device_id = @d", conn);
                hw.Parameters.AddWithValue("n", batch.DeviceNextNo);
                hw.Parameters.AddWithValue("d", batch.DeviceId);
                await hw.ExecuteNonQueryAsync(ct);
            }
            NpgsqlTransaction? batchTx = null;
            if (TxMode == BatchTxMode.PerBatch) batchTx = await conn.BeginTransactionAsync(ct);

            int i = 0;
            foreach (var e in batch.Entries)
            {
                i++;
                if (FailAfter is not null && FailAfter(e, i))
                {
                    if (batchTx is not null) { await batchTx.RollbackAsync(ct); }
                    throw new IOException($"INJECTED_TRANSPORT_FAILURE after {i - 1} entries of batch {batch.BatchId}");
                }
                try
                {
                    var ack = await IngestOneAsync(conn, batchTx, e, ct);
                    acks.Add(ack);
                    if (ack.Outcome == EntryOutcome.Posted) Interlocked.Increment(ref _entriesIngested);
                }
                catch (PostgresException ex)
                {
                    if (batchTx is not null)
                    {
                        // انسداد رأس الطابور: قيد واحد يُسقط الدفعة كلها
                        await batchTx.RollbackAsync(ct);
                        return new SyncResponse(false, $"BATCH_ABORTED_BY_ENTRY {e.IdemKey}: {Sql.Describe(ex)}",
                                                [], batch.Entries.Count, 0, DateTime.UtcNow, 0);
                    }
                    acks.Add(new EntryAck(e.IdemKey, EntryOutcome.Rejected, Sql.Describe(ex), null));
                }
            }
            if (batchTx is not null)
            {
                try { await batchTx.CommitAsync(ct); }
                catch (PostgresException ex)
                {
                    // مشغّل التوازن مؤجَّل إلى COMMIT: الخطأ يظهر هنا، بعد أن قُبلت كل القيود ظاهرياً،
                    // ولا يسمّي القيد الجاني. الدفعة كلها تسقط ولا يعرف الجهاز أيّ قيد أسقطها.
                    return new SyncResponse(false,
                        $"BATCH_ABORTED_AT_COMMIT (the deferred balance trigger fired at COMMIT; " +
                        $"NO entry is named): {Sql.Describe(ex)}", [], batch.Entries.Count, 0, DateTime.UtcNow, 0);
                }
            }

            var serverUtc = Canonical.PgInstant(DateTime.UtcNow);
            return new SyncResponse(true, "", acks, SuggestBatchSize(batch.Entries.Count), 0, serverUtc,
                                    (long)(serverUtc - batch.DeviceClockAtSend).TotalMilliseconds);
        }
        finally { Interlocked.Decrement(ref _inflight); }
    }

    private int SuggestBatchSize(int current)
    {
        var load = (double)Volatile.Read(ref _inflight) / MaxConcurrent;
        return load > 0.75 ? Math.Max(10, current / 2)
             : load < 0.35 ? Math.Min(500, current + 25)
             : current;
    }

    private async Task<EntryAck> IngestOneAsync(NpgsqlConnection conn, NpgsqlTransaction? tx, SyncEntry e, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            select outcome, note, entry_no from pos.ingest_sale(
                @idem, @tenant, @device, @sale, @doctype, @invno, @seq, @bdate, @dclock, @shift, @orig,
                @net, @vat, @gross, @prev, @hash, @payload, @past, @lines::jsonb, @journal::jsonb, @policy)
            """, conn, tx);
        cmd.Parameters.AddWithValue("idem", e.IdemKey);
        cmd.Parameters.AddWithValue("tenant", e.TenantId);
        cmd.Parameters.AddWithValue("device", e.DeviceId);
        cmd.Parameters.AddWithValue("sale", Guid.Parse(e.SaleId));
        cmd.Parameters.AddWithValue("doctype", e.DocType);
        cmd.Parameters.AddWithValue("invno", e.InvoiceNo);
        cmd.Parameters.AddWithValue("seq", e.DeviceSeq);
        // تاريخ العمل قيمة سلك كتبها الجهاز بـCanonical.Date («yyyy-MM-dd» ثابتة) ثم تُخزَّن
        // في الدفتر. قراءتها بثقافة الخادم كارثة مقيسة: تحت ar-SA تُلقي FormatException،
        // وتحت fa-IR تعود 2647-11-15 وتحت th-TH تعود 1483-08-24 — **بصمت** وتُكتب كما هي.
        // Wire value written invariantly by the device; parsing it in the server culture
        // silently yields a date centuries off (fa-IR, th-TH) or throws (ar-SA).
        cmd.Parameters.AddWithValue("bdate", DateOnly.ParseExact(e.BusinessDate, "yyyy-MM-dd", CultureInfo.InvariantCulture));
        cmd.Parameters.AddWithValue("dclock", e.DeviceClockAt);
        cmd.Parameters.AddWithValue("shift", e.ShiftId);
        cmd.Parameters.AddWithValue("orig", (object?)e.OriginalIdemKey ?? DBNull.Value);
        cmd.Parameters.AddWithValue("net", e.TotalNet);
        cmd.Parameters.AddWithValue("vat", e.TotalVat);
        cmd.Parameters.AddWithValue("gross", e.TotalGross);
        cmd.Parameters.AddWithValue("prev", e.PrevHash);
        cmd.Parameters.AddWithValue("hash", e.EntryHash);
        cmd.Parameters.AddWithValue("payload", e.PayloadHash);
        cmd.Parameters.AddWithValue("past", e.PastCeiling);
        cmd.Parameters.AddWithValue("lines", JsonSerializer.Serialize(e.Lines));
        cmd.Parameters.AddWithValue("journal", JsonSerializer.Serialize(e.JournalLines));
        cmd.Parameters.AddWithValue("policy", OrphanPolicy);

        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) return new EntryAck(e.IdemKey, EntryOutcome.TransientError, "no result", null);
        var outcome = r.GetString(0);
        var note = r.IsDBNull(1) ? "" : r.GetString(1);
        long? no = r.IsDBNull(2) ? null : r.GetInt64(2);
        return new EntryAck(e.IdemKey, outcome switch
        {
            "posted" => EntryOutcome.Posted,
            "duplicate" => EntryOutcome.Duplicate,
            "quarantined" => EntryOutcome.Quarantined,
            "rejected" => EntryOutcome.Rejected,
            "conflict_mismatch" => EntryOutcome.ConflictMismatch,
            _ => EntryOutcome.TransientError
        }, note, no);
    }

    // ── إدارة الأجهزة والمديات ──────────────────────────────────────────────
    public async Task RegisterDeviceAsync(string tenantId, string deviceId, string branch = "BR-01")
        => await Sql.ExecAsync(connectionString, $"""
            insert into pos.device (device_id, tenant_id, branch_id, state, registered_at)
            values ('{deviceId}', '{tenantId}', '{branch}', 'active', now())
            on conflict (device_id) do nothing
            """);

    /// <summary>
    /// يمنح مدىً محجوزاً. عدم التداخل يفرضه قيد الاستبعاد في المحرّك؛ هذه الدالة
    /// تختار البداية بعد أقصى نهاية ممنوحة للمستأجر، وتعيد المحاولة إن سبقها آخر.
    /// </summary>
    public async Task<RangeGrant> GrantRangeAsync(string tenantId, string deviceId, long size)
    {
        await using var conn = await Sql.OpenAsync(connectionString);
        for (int attempt = 0; attempt < 8; attempt++)
        {
            await using var tx = await conn.BeginTransactionAsync();
            var next = await Sql.ScalarAsync<long>(conn,
                $"select coalesce(max(range_end),0) + 1 from pos.number_range where tenant_id = '{tenantId}'", tx);
            var id = $"R-{tenantId}-{deviceId}-{next}";
            try
            {
                await using var cmd = new NpgsqlCommand("""
                    insert into pos.number_range (range_id, tenant_id, device_id, range_start, range_end, state, granted_at)
                    values (@i, @t, @d, @s, @e, 'active', now())
                    """, conn, tx);
                cmd.Parameters.AddWithValue("i", id);
                cmd.Parameters.AddWithValue("t", tenantId);
                cmd.Parameters.AddWithValue("d", deviceId);
                cmd.Parameters.AddWithValue("s", next);
                cmd.Parameters.AddWithValue("e", next + size - 1);
                await cmd.ExecuteNonQueryAsync();
                await tx.CommitAsync();
                return new RangeGrant(id, next, next + size - 1);
            }
            catch (PostgresException ex) when (ex.SqlState is "23P01" or "23505")
            {
                await tx.RollbackAsync();   // سبقنا جهاز آخر: أعد المحاولة بعد أقصى نهاية جديدة
            }
        }
        throw new InvalidOperationException("could not allocate a non-overlapping range after 8 attempts");
    }

    /// <summary>
    /// يُثبت فجوة <b>إيجاباً</b>. غياب السجل ليس تفسيراً: المُدقّق لا يفرّق بين
    /// «لم يُصدَر شيء» و«حُذفت سجلات» ما لم يُسجَّل الفراغ نفسه بوصفه واقعة موقّعة.
    /// </summary>
    public async Task AssertGapAsync(string tenantId, string deviceId, long fromNo, long toNo,
                                     string reasonCode, string reasonAr, string by)
    {
        var evidence = Canonical.HashOf($"babel.pos.gap.v1|{tenantId}|{deviceId}|{fromNo}|{toNo}|{reasonCode}|{by}");
        await using var conn = await Sql.OpenAsync(connectionString);
        await using var cmd = new NpgsqlCommand("""
            insert into pos.number_gap_assertion
                (assertion_id, tenant_id, device_id, from_no, to_no, reason_code, reason_ar, asserted_at, asserted_by, evidence_hash)
            values (gen_random_uuid(), @t, @d, @f, @o, @rc, @ra, now(), @by, @ev)
            """, conn);
        cmd.Parameters.AddWithValue("t", tenantId); cmd.Parameters.AddWithValue("d", deviceId);
        cmd.Parameters.AddWithValue("f", fromNo); cmd.Parameters.AddWithValue("o", toNo);
        cmd.Parameters.AddWithValue("rc", reasonCode); cmd.Parameters.AddWithValue("ra", reasonAr);
        cmd.Parameters.AddWithValue("by", by); cmd.Parameters.AddWithValue("ev", evidence);
        await cmd.ExecuteNonQueryAsync();
    }

    /// <summary>يستبدل جهازاً: يُبطل ذيل مداه غير المستعمل <b>مع إثبات فجوة</b>، ويمنح خلَفَه مدىً جديداً.</summary>
    public async Task<RangeGrant> ReplaceDeviceAsync(string tenantId, string oldDevice, string newDevice,
                                                     long lastUsedNo, long rangeSize, string by, string reasonCode = "DEVICE_REPLACED")
    {
        await RegisterDeviceAsync(tenantId, newDevice);
        await using var conn = await Sql.OpenAsync(connectionString);
        var tail = await Sql.ScalarAsync<long>(conn,
            $"select coalesce(max(range_end),0) from pos.number_range where tenant_id='{tenantId}' and device_id='{oldDevice}'");
        await Sql.ExecAsync(conn, $"""
            update pos.number_range set state = 'voided', voided_at = now()
             where tenant_id='{tenantId}' and device_id='{oldDevice}' and state='active';
            update pos.device set state = '{(reasonCode == "DEVICE_LOST" ? "lost" : "replaced")}',
                   replaced_by = '{newDevice}', retired_at = now(), note = 'retired by {by}'
             where device_id = '{oldDevice}';
            """);
        if (tail > lastUsedNo)
            await AssertGapAsync(tenantId, oldDevice, lastUsedNo + 1, tail, reasonCode,
                reasonCode == "DEVICE_LOST"
                    ? "الجهاز فُقد أو أُتلف؛ ما تبقّى من مداه أُبطل ولن يُصدَر"
                    : "الجهاز استُبدل؛ ما تبقّى من مداه أُبطل ولن يُصدَر",
                by);
        return await GrantRangeAsync(tenantId, newDevice, rangeSize);
    }
}
