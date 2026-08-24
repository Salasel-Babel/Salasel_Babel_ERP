using BabelRelationalSpike.Db;
using BabelRelationalSpike.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.RDBMS;
using Wolverine.Runtime;

namespace BabelRelationalSpike.Proofs;

/// <summary>
/// (A) THE DECISIVE ITEM: Wolverine's durable transactional outbox WITHOUT Marten.
///
///     Message store  : WolverineFx.Postgresql  (plain PostgreSQL tables)
///     Transaction    : (i) a raw Npgsql transaction via Wolverine.RDBMS
///                          DatabaseEnvelopeTransaction, and
///                      (ii) EF Core 10 via WolverineFx.EntityFrameworkCore.
///     Marten         : not referenced, not restored, not loaded.
///
///     صندوق الرسائل الصادر المعاملاتي يعمل بدون Marten إطلاقاً.
/// </summary>
public static class ProofA_Outbox
{
    private const string Outgoing = "wolverine.wolverine_outgoing_envelopes";

    public static async Task RunAsync(IServiceProvider services, ProofRecorder rec)
    {
        rec.Section("(A) Wolverine durable transactional outbox, NO Marten");

        var runtime = services.GetRequiredService<IWolverineRuntime>();
        var store = runtime.Storage as IMessageDatabase;

        // ---- A1 : the durable store is genuinely in play ------------------
        var tables = await Sql.TableAsync(Config.Admin, """
            select table_name,
                   (select count(*) from information_schema.columns c
                     where c.table_schema = t.table_schema and c.table_name = t.table_name) as columns
            from information_schema.tables t
            where table_schema = 'wolverine'
            order by table_name
            """);
        var core = await Sql.ScalarAsync<long>(Config.Admin,
            "select count(*) from information_schema.tables where table_schema='wolverine' and table_name in " +
            "('wolverine_incoming_envelopes','wolverine_outgoing_envelopes','wolverine_dead_letters')");
        rec.Check("A1", "durable Postgres message store exists, provided WITHOUT Marten",
            core == 3 && store is not null,
            $"message store implementation: {runtime.Storage.GetType().FullName}\n" +
            $"(from WolverineFx.Postgresql -> Wolverine.RDBMS; Marten assemblies loaded: " +
            $"{(AppDomain.CurrentDomain.GetAssemblies().Any(a => a.GetName().Name?.StartsWith("Marten") == true) ? "YES" : "NONE")})\n" +
            tables);

        if (store is null)
        {
            rec.Fail("A2", "outbox enlists a raw transaction", "no IMessageDatabase resolved");
            return;
        }

        // ---- A2/A3 : committed transaction -> delivered -------------------
        await Sql.ExecAsync(Config.Admin, $"delete from {Outgoing}");
        var committedId = Guid.CreateVersion7();
        long insideTx, otherConn, beforePersist;
        {
            var ctx = new MessageContext(runtime);
            await using var conn = await Sql.OpenAsync(Config.App);   // least-privilege app role
            await using var tx = await conn.BeginTransactionAsync();

            // ONE transaction shared by the business write and the envelope insert
            var envelopeTx = new DatabaseEnvelopeTransaction(store, tx);
            await ctx.EnlistInOutboxAsync(envelopeTx);

            await using (var cmd = new NpgsqlCommand("""
                insert into app.tenant_document (tenant_id, doc_type, doc_key, doc, updated_at)
                values ('acme', 'outbox-proof', @k, '{"state":"committed"}'::jsonb, now())
                """, conn, tx))
            {
                cmd.Parameters.AddWithValue("k", committedId.ToString());
                await cmd.ExecuteNonQueryAsync();
            }

            await ctx.PublishAsync(new JournalPostedNotice(committedId, 1, "OUTBOX", "committed"));
            beforePersist = await CountAsync(conn, tx, committedId);

            // Wolverine buffers outgoing envelopes on the MessageContext and writes
            // them through IEnvelopeTransaction as part of the transaction commit
            // step. We invoke that same DatabaseEnvelopeTransaction explicitly so
            // the row can be OBSERVED while the transaction is still open.
            await envelopeTx.PersistOutgoingAsync(ctx.Outstanding.ToArray());

            insideTx = await CountAsync(conn, tx, committedId);
            otherConn = await Sql.ScalarAsync<long>(Config.Admin, $"select count(*) from {Outgoing}");

            await tx.CommitAsync();
            await ctx.FlushOutgoingMessagesAsync();
        }

        rec.Check("A2", "outgoing envelope is INSERTED INSIDE the business transaction",
            insideTx == 1 && otherConn == 0,
            $"buffered on the MessageContext, not yet written : {beforePersist} row\n" +
            $"after IEnvelopeTransaction.PersistOutgoingAsync  : {insideTx} row  (same connection, tx still OPEN)\n" +
            $"seen from a different connection at that instant : {otherConn} rows (uncommitted -> invisible)\n" +
            "=> the envelope INSERT and the business INSERT are in one PostgreSQL transaction");

        var delivered = await DeliveryLog.WaitForAsync(committedId, TimeSpan.FromSeconds(20));
        var drained = await Sql.ScalarAsync<long>(Config.Admin, $"select count(*) from {Outgoing}");
        rec.Check("A3", "message published in a COMMITTED transaction IS delivered", delivered,
            $"handler invoked for {committedId}: {delivered}\n" +
            $"{Outgoing} after delivery: {drained} row(s) still parked (this scenario persisted the\n" +
            "envelope by hand; in the framework's own path the sending agent reclaims the row)");

        // ---- A4 : rolled back transaction -> NEVER delivered --------------
        await Sql.ExecAsync(Config.Admin, $"delete from {Outgoing}");   // start from an empty table
        var rolledBackId = Guid.CreateVersion7();
        long stagedInTx;
        {
            var ctx = new MessageContext(runtime);
            await using var conn = await Sql.OpenAsync(Config.App);
            await using var tx = await conn.BeginTransactionAsync();
            var envelopeTx = new DatabaseEnvelopeTransaction(store, tx);
            await ctx.EnlistInOutboxAsync(envelopeTx);

            await using (var cmd = new NpgsqlCommand("""
                insert into app.tenant_document (tenant_id, doc_type, doc_key, doc, updated_at)
                values ('acme', 'outbox-proof', @k, '{"state":"rolled-back"}'::jsonb, now())
                """, conn, tx))
            {
                cmd.Parameters.AddWithValue("k", rolledBackId.ToString());
                await cmd.ExecuteNonQueryAsync();
            }

            await ctx.PublishAsync(new JournalPostedNotice(rolledBackId, 2, "OUTBOX", "rolled-back"));
            await envelopeTx.PersistOutgoingAsync(ctx.Outstanding.ToArray());
            stagedInTx = await CountAsync(conn, tx, rolledBackId);

            await tx.RollbackAsync();          // <- and we never flush
        }

        rec.Evidence($"the envelope WAS staged inside the doomed transaction ({stagedInTx} row) - " +
                     "now waiting 20s to prove the ROLLBACK erased it");
        var absent = await DeliveryLog.StaysAbsentAsync(rolledBackId, TimeSpan.FromSeconds(20));
        var leftOver = await Sql.ScalarAsync<long>(Config.Admin, $"select count(*) from {Outgoing}");
        var deadLetters = await Sql.ScalarAsync<long>(Config.Admin,
            "select count(*) from wolverine.wolverine_dead_letters");
        var ghostDoc = await Sql.ScalarAsync<long>(Config.Admin,
            $"select count(*) from app.tenant_document where doc_key = '{rolledBackId}'");

        rec.Check("A4", "message published in a ROLLED BACK transaction is NEVER delivered",
            stagedInTx == 1 && absent && leftOver == 0 && ghostDoc == 0,
            $"waited 20s (requirement: >=15s); handler never invoked for {rolledBackId}\n" +
            $"{Outgoing}              : {leftOver} row(s)  <- clean\n" +
            $"wolverine.wolverine_dead_letters : {deadLetters} row(s)\n" +
            $"the business row itself          : {ghostDoc} row(s)  <- rolled back with it");

        // ---- A5 : the same thing through the EF Core 10 integration -------
        var efId = Guid.CreateVersion7();
        await using (var scope = services.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<LedgerDbContext>>();
            var ctx = outbox.DbContext;
            await using var tx = await ctx.Database.BeginTransactionAsync();

            var entry = await Ledger.BuildAndInsertAsync(ctx, "OUTBOX", "acme",
                DateOnly.FromDateTime(DateTime.UtcNow), "ef core outbox", "صندوق صادر عبر EF Core", "spike",
                [
                    new LineSpec(1, "1010", "cash", 250.0000m, 0m),
                    new LineSpec(2, "4010", "revenue", 0m, 250.0000m)
                ]);
            efId = entry.EntryId;
            await outbox.PublishAsync(new JournalPostedNotice(entry.EntryId, entry.EntryNo, "OUTBOX", "ef-core"));
            // NOTE: this one call persists the envelopes, SaveChanges, COMMITS the
            // ambient EF transaction, and only then flushes to the sending agents.
            await outbox.SaveChangesAndFlushMessagesAsync();
        }

        var efDelivered = await DeliveryLog.WaitForAsync(efId, TimeSpan.FromSeconds(20));
        var efPersisted = await Sql.ScalarAsync<long>(Config.Admin,
            $"select count(*) from ledger.journal_entry where entry_id = '{efId}'");
        rec.Check("A5", "EF Core 10 DbContext is a first-class outbox transaction owner",
            efDelivered && efPersisted == 1,
            $"WolverineFx.EntityFrameworkCore IDbContextOutbox<LedgerDbContext>\n" +
            $"journal entry committed: {efPersisted}, message delivered: {efDelivered}\n" +
            "GOTCHA worth writing down: SaveChangesAndFlushMessagesAsync() COMMITS the ambient\n" +
            "EF transaction for you. For hand-rolled rollback control use the raw\n" +
            "DatabaseEnvelopeTransaction path shown in A2/A4.");

        // ---- A6 : a transaction that ABORTS AT COMMIT (deferred trigger) ---
        // The most realistic accounting failure: the entry looks fine when the
        // rows are inserted and is rejected by PostgreSQL at COMMIT. The message
        // published in that transaction must die with it.
        await Sql.ExecAsync(Config.Admin, $"delete from {Outgoing}");
        var doomedId = Guid.CreateVersion7();
        string commitError = "(no exception - PROBLEM)";
        await using (var scope = services.CreateAsyncScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<LedgerDbContext>>();
            var ctx = outbox.DbContext;
            await using var tx = await ctx.Database.BeginTransactionAsync();
            try
            {
                var entry = await Ledger.BuildAndInsertAsync(ctx, "OUTBOX", "acme",
                    DateOnly.FromDateTime(DateTime.UtcNow), "doomed", "قيد غير متوازن", "spike",
                    [
                        new LineSpec(1, "1010", "cash", 100.0000m, 0m),
                        new LineSpec(2, "4010", "revenue", 0m, 99.9999m)   // out by 0.0001
                    ]);
                doomedId = entry.EntryId;
                await outbox.PublishAsync(new JournalPostedNotice(entry.EntryId, entry.EntryNo, "OUTBOX", "doomed"));
                await outbox.SaveChangesAndFlushMessagesAsync();
            }
            catch (Exception ex)
            {
                commitError = ex.InnerException is PostgresException pg ? Sql.Describe(pg) : ex.Message.Split('\n')[0];
                try { await tx.RollbackAsync(); } catch { /* already aborted */ }
            }
        }

        var doomedAbsent = await DeliveryLog.StaysAbsentAsync(doomedId, TimeSpan.FromSeconds(16));
        var doomedRows = await Sql.ScalarAsync<long>(Config.Admin,
            $"select count(*) from ledger.journal_entry where entry_id = '{doomedId}'");
        var doomedEnvelopes = await Sql.ScalarAsync<long>(Config.Admin, $"select count(*) from {Outgoing}");
        rec.Check("A6", "transaction rejected AT COMMIT: business rows and message both vanish",
            doomedAbsent && doomedRows == 0 && doomedEnvelopes == 0 && !commitError.StartsWith("(no"),
            $"PostgreSQL refused the COMMIT: {commitError}\n" +
            $"waited 16s; handler never invoked for {doomedId}\n" +
            $"ledger.journal_entry rows : {doomedRows}\n" +
            $"{Outgoing} : {doomedEnvelopes} row(s)  <- clean");

        rec.Evidence("wolverine_outgoing_envelopes columns:\n" + await Sql.TableAsync(Config.Admin, """
            select column_name, data_type from information_schema.columns
            where table_schema='wolverine' and table_name='wolverine_outgoing_envelopes'
            order by ordinal_position
            """));
    }

    private static async Task<long> CountAsync(NpgsqlConnection conn, NpgsqlTransaction tx, Guid id)
    {
        // the envelope body is Wolverine's own binary framing, so match on bytes
        await using var cmd = new NpgsqlCommand(
            $"select count(*) from {Outgoing} where position(convert_to(@g, 'UTF8') in body) > 0", conn, tx);
        cmd.Parameters.AddWithValue("g", id.ToString());
        var byMessage = (long)(await cmd.ExecuteScalarAsync())!;
        if (byMessage > 0) return byMessage;
        await using var all = new NpgsqlCommand($"select count(*) from {Outgoing}", conn, tx);
        return (long)(await all.ExecuteScalarAsync())!;
    }
}
