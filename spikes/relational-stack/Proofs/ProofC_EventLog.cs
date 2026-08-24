using System.Diagnostics;
using BabelRelationalSpike.Db;
using BabelRelationalSpike.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using NpgsqlTypes;

namespace BabelRelationalSpike.Proofs;

/// <summary>
/// (C) A relational append-only event log replacing Marten's event store for the
///     "process narrative" use case. One table, a JSONB payload, a GIN index, and
///     the same REVOKE UPDATE, DELETE protection as the ledger.
/// </summary>
public static class ProofC_EventLog
{
    private const int BulkRows = 300_000;

    public static async Task RunAsync(IServiceProvider services, ProofRecorder rec)
    {
        rec.Section("(C) relational append-only event log for the process narrative");

        var tenant = "acme";
        var zatcaStream = Guid.CreateVersion7();
        var shiftStream = Guid.CreateVersion7();
        var approvalStream = Guid.CreateVersion7();
        var leaseStream = Guid.CreateVersion7();
        var aiStream = Guid.CreateVersion7();
        var correlation = Guid.CreateVersion7();

        // ---- C1 : polymorphic payloads round-trip through EF Core JSONB ---
        await using (var ctx = Contexts.Create())
        {
            var t0 = DateTime.UtcNow;
            var events = new List<(Guid stream, string type, ProcessPayload payload)>
            {
                (approvalStream, "DocumentApproval", new DraftSaved("INV-2026-0001", 1150.0000m) { Status = "DRAFT" }),
                (approvalStream, "DocumentApproval", new ApprovalRequested("INV-2026-0001", "المدير المالي") { Status = "PENDING" }),
                (approvalStream, "DocumentApproval", new ApprovalGranted("INV-2026-0001", "المدير المالي", "معتمد بعد المراجعة") { Status = "APPROVED" }),

                (shiftStream, "PosShift", new ShiftOpened("POS-03", "سعاد", 500.0000m) { Status = "OPEN" }),
                (shiftStream, "PosShift", new ShiftClosed("POS-03", 4820.5000m, -12.5000m) { Status = "CLOSED" }),

                (zatcaStream, "ZatcaSubmission", new ZatcaSubmitted("6f1e...a1", "9c8b7a", 1) { Status = "SUBMITTED" }),
                (zatcaStream, "ZatcaSubmission", new ZatcaRejectedByPortal("6f1e...a1", "BR-KSA-16", "رقم تسجيل ضريبي غير صالح", 1) { Status = "REJECTED" }),
                (zatcaStream, "ZatcaSubmission", new ZatcaRetryScheduled("6f1e...a1", t0.AddMinutes(5), 2) { Status = "RETRY_SCHEDULED" }),
                (zatcaStream, "ZatcaSubmission", new ZatcaSubmitted("6f1e...a1", "9c8b7a", 2) { Status = "SUBMITTED" }),
                (zatcaStream, "ZatcaSubmission", new ZatcaCleared("6f1e...a1", "cl-778", 2) { Status = "CLEARED" }),

                (leaseStream, "Lease", new LeaseSigned("LSE-114", 180000.0000m, "شركة سلاسل بابل") { Status = "ACTIVE" }),
                (leaseStream, "Lease", new LeaseTerminated("LSE-114", "إنهاء بالتراضي") { Status = "TERMINATED" }),

                (aiStream, "AiSuggestion", new AiSuggestionOffered("sg-1", "AccountCoding", "5310", 0.91) { Status = "OFFERED" }),
                (aiStream, "AiSuggestion", new AiSuggestionAccepted("sg-1", "muhasib@acme.sa") { Status = "ACCEPTED" }),
                (aiStream, "AiSuggestion", new AiSuggestionOffered("sg-2", "VatCategory", "S", 0.62) { Status = "OFFERED" }),
                (aiStream, "AiSuggestion", new AiSuggestionRejected("sg-2", "muhasib@acme.sa", "التصنيف الضريبي غير صحيح") { Status = "REJECTED" })
            };

            var seqPerStream = new Dictionary<Guid, int>();
            var seconds = 0;
            foreach (var (stream, type, payload) in events)
            {
                seqPerStream.TryGetValue(stream, out var seq);
                seqPerStream[stream] = ++seq;
                ctx.ProcessEvents.Add(new ProcessEvent
                {
                    EventId = Guid.CreateVersion7(),
                    TenantId = tenant,
                    StreamType = type,
                    StreamId = stream,
                    StreamSeq = seq,
                    EventType = payload.GetType().Name,
                    OccurredAt = t0.AddSeconds(seconds++),
                    Actor = "muhasib@acme.sa",
                    CorrelationId = correlation,
                    CausationId = null,
                    Payload = PayloadJson.Write(payload)
                });
            }
            await ctx.SaveChangesAsync();
        }

        await using (var read = Contexts.Create())
        {
            var rows = await read.ProcessEvents.AsNoTracking()
                .Where(e => e.StreamId == zatcaStream).OrderBy(e => e.StreamSeq).ToListAsync();
            var typed = rows.Select(r => PayloadJson.Read(r.Payload)).ToList();

            var rejected = typed.OfType<ZatcaRejectedByPortal>().SingleOrDefault();
            var cleared = typed.OfType<ZatcaCleared>().SingleOrDefault();
            var aiRows = await read.ProcessEvents.AsNoTracking()
                .Where(e => e.StreamId == aiStream).OrderBy(e => e.StreamSeq).ToListAsync();
            var aiTyped = aiRows.Select(r => PayloadJson.Read(r.Payload)).ToList();
            var aiRejected = aiTyped.OfType<AiSuggestionRejected>().SingleOrDefault();

            var ok = rejected is { ErrorCode: "BR-KSA-16", Attempt: 1 }
                     && rejected.MessageAr == "رقم تسجيل ضريبي غير صالح"
                     && cleared is { ClearanceUuid: "cl-778" }
                     && aiRejected is { ReasonAr: "التصنيف الضريبي غير صحيح" }
                     && typed.Count == 5;

            var raw = await Sql.ScalarAsync<string>(Config.Admin,
                $"select jsonb_pretty(payload) from ledger.process_event where stream_id = '{zatcaStream}' and stream_seq = 2");

            rec.Check("C1", "EF Core 10 writes and reads a POLYMORPHIC JSONB payload", ok,
                $"{typed.Count} events replayed from the ZATCA stream, deserialised back to their .NET types:\n" +
                string.Join("\n", typed.Select((t, i) => $"  seq {i + 1}: {t.GetType().Name,-22} status={t.Status}")) +
                $"\n  AI suggestion stream also carries a REJECTED outcome: {aiRejected?.ReasonAr}\n" +
                $"stored jsonb (note PostgreSQL re-ordered the keys - $type is NOT first):\n{raw}");
        }

        rec.Note("System.Text.Json needs AllowOutOfOrderMetadataProperties=true (net9.0+) to read a " +
                 "polymorphic payload back out of jsonb, because jsonb re-orders object keys.");

        // ---- C2 : a real EF Core query INTO the JSONB uses the GIN index --
        await BulkLoadAsync(rec);

        var capture = new SqlCapture();
        int hits;
        await using (var ctx = Contexts.Create(interceptor: capture))
        {
            var sw = Stopwatch.StartNew();
            hits = await ctx.ProcessEvents.AsNoTracking()
                .Where(e => EF.Functions.JsonContains(e.Payload, """{"status":"REJECTED"}"""))
                .CountAsync();
            sw.Stop();
            rec.Evidence($"EF Core LINQ -> {hits} rows out of {BulkRows + 16} in {sw.ElapsedMilliseconds} ms");
        }

        var plan = await capture.ExplainAsync(Config.App);
        var usesGin = plan.Contains("ix_process_event_payload_gin", StringComparison.OrdinalIgnoreCase);
        rec.Check("C2", "EF Core query INTO the JSONB is served by the GIN index", usesGin && hits > 0,
            $"EF Core generated SQL:\n  {capture.CommandText?.Replace("\n", "\n  ")}\n" +
            $"EXPLAIN (ANALYZE, BUFFERS):\n  {plan.Replace("\n", "\n  ")}");

        // the "one hot scalar field" alternative: a plain expression index.
        // (EF Core's graded expression-index proof lives in (D), where the JSON
        //  document is mapped to a typed POCO with ToJson().)
        var exprPlan = await ExplainRawAsync(Config.App,
            "select count(*) from ledger.process_event where payload ->> 'status' = 'REJECTED'");
        rec.Evidence("same predicate through an expression index on (payload ->> 'status'):\n  " +
                     exprPlan.Replace("\n", "\n  "));

        // ---- C3 : same append-only protection as the ledger ---------------
        var eventId = await Sql.ScalarAsync<Guid>(Config.Admin,
            $"select event_id from ledger.process_event where stream_id = '{zatcaStream}' limit 1");
        var upd = await Sql.ExpectFailureAsync(Config.App,
            $"update ledger.process_event set payload = '{{}}'::jsonb where event_id = '{eventId}'");
        var del = await Sql.ExpectFailureAsync(Config.App,
            $"delete from ledger.process_event where event_id = '{eventId}'");
        string efUpd = "(EF Core UPDATE unexpectedly SUCCEEDED)";
        try
        {
            await using var c = Contexts.Create();
            var e = await c.ProcessEvents.SingleAsync(x => x.EventId == eventId);
            e.Payload = """{"status":"TAMPERED"}""";
            await c.SaveChangesAsync();
        }
        catch (Exception ex) when (Find<PostgresException>(ex) is { } pg) { efUpd = Sql.Describe(pg); }

        rec.Check("C3", "the event log is append-only: UPDATE and DELETE are revoked too",
            upd?.SqlState == "42501" && del?.SqlState == "42501" && efUpd.StartsWith("SQLSTATE 42501"),
            $"raw UPDATE   : {(upd is null ? "SUCCEEDED - FAIL" : Sql.Describe(upd))}\n" +
            $"raw DELETE   : {(del is null ? "SUCCEEDED - FAIL" : Sql.Describe(del))}\n" +
            $"EF Core UPDATE: {efUpd}\n" +
            "This is exactly what Marten cannot offer on mt_events: Marten itself issues\n" +
            "UPDATE/DELETE there for archiving, masking and tombstones, so the grant must stay.");

        // ---- C4 : rebuild current state from the log ----------------------
        await RebuildAsync(rec, tenant);

        rec.Evidence(MartenGap.Text);
    }

    private static async Task BulkLoadAsync(ProofRecorder rec)
    {
        var existing = await Sql.ScalarAsync<long>(Config.Admin, "select count(*) from ledger.process_event");
        if (existing >= BulkRows) return;

        var sw = Stopwatch.StartNew();
        await using var conn = await Sql.OpenAsync(Config.App);     // INSERT-only role, COPY still allowed
        await using (var writer = await conn.BeginBinaryImportAsync("""
            copy ledger.process_event (event_id, tenant_id, stream_type, stream_id, stream_seq,
                                       event_type, occurred_at, actor, correlation_id, causation_id, payload)
            from stdin (format binary)
            """))
        {
            var rnd = new Random(20260824);
            var baseTime = DateTime.UtcNow.AddDays(-30);
            for (var i = 0; i < BulkRows; i++)
            {
                // realistic skew: only ~0.5% of submissions are rejected
                var status = rnd.Next(0, 200) == 0 ? "REJECTED" : (rnd.Next(0, 2) == 0 ? "SUBMITTED" : "CLEARED");
                var payload = $$"""
                    {"$type":"ZatcaSubmitted","status":"{{status}}","invoiceUuid":"inv-{{i}}","hash":"h{{i}}","attempt":1}
                    """;
                await writer.StartRowAsync();
                await writer.WriteAsync(Guid.CreateVersion7(), NpgsqlDbType.Uuid);
                await writer.WriteAsync("acme", NpgsqlDbType.Text);
                await writer.WriteAsync("ZatcaSubmission", NpgsqlDbType.Text);
                await writer.WriteAsync(Guid.CreateVersion7(), NpgsqlDbType.Uuid);
                await writer.WriteAsync(1, NpgsqlDbType.Integer);
                await writer.WriteAsync("ZatcaSubmitted", NpgsqlDbType.Text);
                await writer.WriteAsync(baseTime.AddSeconds(i), NpgsqlDbType.TimestampTz);
                await writer.WriteAsync("bulk", NpgsqlDbType.Text);
                await writer.WriteAsync(Guid.CreateVersion7(), NpgsqlDbType.Uuid);
                await writer.WriteNullAsync();
                await writer.WriteAsync(payload, NpgsqlDbType.Jsonb);
            }
            await writer.CompleteAsync();
        }
        sw.Stop();
        // GIN indexes buffer new entries in a pending list (fastupdate); until it is
        // flushed the planner costs the index far too high. VACUUM flushes it.
        await Sql.ExecAsync(Config.Admin, "vacuum analyze ledger.process_event");
        rec.Evidence($"loaded {BulkRows:N0} process events by binary COPY as the INSERT-only role " +
                     $"in {sw.ElapsedMilliseconds} ms, then VACUUM ANALYZE (flushes the GIN pending list)");
    }

    private static async Task RebuildAsync(ProofRecorder rec, string tenant)
    {
        // (i) fold in C#
        await using var ctx = Contexts.Create();
        var streams = await ctx.ProcessEvents.AsNoTracking()
            .Where(e => e.Actor != "bulk" && e.TenantId == tenant)
            .OrderBy(e => e.StreamId).ThenBy(e => e.StreamSeq)
            .ToListAsync();

        var state = new Dictionary<Guid, (string Type, string Status, int Events, string Last)>();
        foreach (var e in streams)
        {
            var p = PayloadJson.Read(e.Payload);
            state.TryGetValue(e.StreamId, out var s);
            state[e.StreamId] = (e.StreamType, p.Status, s.Events + 1, e.EventType);
        }

        // (ii) the same rebuild done entirely in SQL - the "projection rebuild"
        await Sql.ExecAsync(Config.Admin, """
            drop materialized view if exists ledger.process_current_state;
            create materialized view ledger.process_current_state as
            select distinct on (stream_id)
                   stream_id, tenant_id, stream_type,
                   payload ->> 'status' as status,
                   event_type as last_event,
                   stream_seq as events,
                   occurred_at as last_at
            from ledger.process_event
            where actor <> 'bulk'
            order by stream_id, stream_seq desc;
            grant select on ledger.process_current_state to babel_ledger_app;
            """);

        var sqlState = await Sql.TableAsync(Config.Admin, """
            select stream_type, status, events, last_event
            from ledger.process_current_state order by stream_type
            """);

        var expected = new Dictionary<string, string>
        {
            ["DocumentApproval"] = "APPROVED",
            ["PosShift"] = "CLOSED",
            ["ZatcaSubmission"] = "CLEARED",
            ["Lease"] = "TERMINATED",
            ["AiSuggestion"] = "REJECTED"
        };
        var folded = state.Values.ToDictionary(v => v.Type, v => v.Status);
        var ok = expected.All(kv => folded.TryGetValue(kv.Key, out var st) && st == kv.Value);

        // and prove a rebuild is repeatable: drop the view, rebuild, same answer
        var before = await Sql.ScalarAsync<long>(Config.Admin, "select count(*) from ledger.process_current_state");
        await Sql.ExecAsync(Config.Admin, "refresh materialized view ledger.process_current_state");
        var after = await Sql.ScalarAsync<long>(Config.Admin, "select count(*) from ledger.process_current_state");

        rec.Check("C4", "current-state view is rebuilt from the log (in C# and in SQL)",
            ok && before == after && before == expected.Count,
            "folded in C# from the raw events:\n" +
            string.Join("\n", state.Values.Select(v => $"  {v.Type,-18} status={v.Status,-16} events={v.Events} last={v.Last}")) +
            $"\nrebuilt as a materialized view (DISTINCT ON (stream_id) ... ORDER BY stream_seq DESC):\n{sqlState}" +
            $"\nREFRESH MATERIALIZED VIEW is idempotent: {before} -> {after} rows");
    }

    private static async Task<string> ExplainRawAsync(string cs, string sql)
    {
        await using var conn = await Sql.OpenAsync(cs);
        await using var cmd = new NpgsqlCommand("explain (analyze, buffers) " + sql, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        var lines = new List<string>();
        while (await r.ReadAsync()) lines.Add(r.GetString(0));
        return string.Join("\n", lines);
    }

    private static T? Find<T>(Exception ex) where T : Exception
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is T t) return t;
        return null;
    }
}
