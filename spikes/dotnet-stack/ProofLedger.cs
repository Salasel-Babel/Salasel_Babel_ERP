using JasperFx;
using System.Globalization;
using Marten;
using Npgsql;
using Wolverine.Marten;

namespace BabelSpike;

public static class ProofLedger
{
    private const string Schema = ProofDecimal.Schema;
    private static string Inv(decimal d) => d.ToString(CultureInfo.InvariantCulture);

    // =======================================================================
    // (b) BALANCED JOURNAL ENTRY
    // =======================================================================
    public static async Task ProveBalancedEntryAsync(IDocumentStore store, ProofRecorder rec)
    {
        var evidence = new List<string>();
        var failures = new List<string>();

        // --- balanced entry must post -------------------------------------
        var balanced = new JournalEntry
        {
            Reference = "JV-2026-0001",
            PostingDate = new DateOnly(2026, 8, 23),
            Lines =
            [
                new JournalLine("1010 Cash",           1234567890.1234m, 0m),
                new JournalLine("4000 Revenue",        0m,               1234567890.1230m),
                new JournalLine("2100 Rounding",       0m,               0.0004m)
            ]
        };

        try
        {
            balanced.AssertBalanced();
            await using var s = store.LightweightSession();
            s.Store(balanced);
            await s.SaveChangesAsync();

            await using var q = store.QuerySession();
            var back = await q.LoadAsync<JournalEntry>(balanced.Id);
            if (back is null) failures.Add("balanced entry was not persisted");
            else
            {
                evidence.Add($"balanced   debit={Inv(back.TotalDebit)} credit={Inv(back.TotalCredit)} -> persisted, IsBalanced={back.IsBalanced}");
                if (!back.IsBalanced) failures.Add("reloaded balanced entry no longer balances (precision lost)");
                if (back.TotalDebit != balanced.TotalDebit) failures.Add("debit total changed across persistence");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"balanced entry was wrongly rejected: {ex.Message}");
        }

        // --- unbalanced entry must be rejected ----------------------------
        var unbalanced = new JournalEntry
        {
            Reference = "JV-2026-0002",
            PostingDate = new DateOnly(2026, 8, 23),
            Lines =
            [
                new JournalLine("1010 Cash",    100.0000m, 0m),
                new JournalLine("4000 Revenue", 0m,        99.9999m)   // out by 0.0001
            ]
        };

        var rejected = false;
        try
        {
            unbalanced.AssertBalanced();
            await using var s = store.LightweightSession();
            s.Store(unbalanced);
            await s.SaveChangesAsync();
        }
        catch (UnbalancedJournalEntryException ex)
        {
            rejected = true;
            evidence.Add($"unbalanced debit=100.0000 credit=99.9999 -> REJECTED: {ex.Message}");
        }

        if (!rejected) failures.Add("unbalanced entry was accepted - the 0.0001 difference was not detected");

        await using (var q = store.QuerySession())
        {
            var leaked = await q.LoadAsync<JournalEntry>(unbalanced.Id);
            if (leaked is not null) failures.Add("unbalanced entry leaked into the database");
            else evidence.Add("unbalanced entry is absent from the database");
        }

        rec.Record("(b)", "BALANCED JOURNAL ENTRY enforced (posts balanced, rejects unbalanced)",
            failures.Count == 0, string.Join("\n", evidence.Concat(failures.Select(f => "!! " + f))));
    }

    // =======================================================================
    // (c) EVENT STORE + PROJECTION REBUILD
    // =======================================================================
    public static async Task ProveEventStoreAsync(IDocumentStore store, ProofRecorder rec)
    {
        var evidence = new List<string>();
        var failures = new List<string>();

        var entryId = Guid.NewGuid();
        var lines = new List<JournalLine>
        {
            new("1010 Cash",     1234567890.1234m, 0m),
            new("1200 AR",       0.0001m,          0m),
            new("4000 Revenue",  0m,               1234567890.1235m)
        };
        var expectedDebit = lines.Sum(l => l.Debit);
        var expectedCredit = lines.Sum(l => l.Credit);

        await using (var s = store.LightweightSession())
        {
            var events = new List<object> { new JournalEntryPosted(entryId, "JV-2026-0003", new DateOnly(2026, 8, 23)) };
            foreach (var l in lines)
            {
                if (l.Debit != 0m) events.Add(new LineDebited(l.Account, l.Debit));
                if (l.Credit != 0m) events.Add(new LineCredited(l.Account, l.Credit));
            }
            s.Events.StartStream<LedgerState>(entryId, events.ToArray());
            await s.SaveChangesAsync();
            evidence.Add($"appended {events.Count} events to stream {entryId}");
        }

        // 1. inline projection
        await using (var q = store.QuerySession())
        {
            var inline = await q.LoadAsync<LedgerState>(entryId);
            if (inline is null) failures.Add("inline projection produced no document");
            else
            {
                evidence.Add($"inline projection    debit={Inv(inline.TotalDebit)} credit={Inv(inline.TotalCredit)} lines={inline.LineCount}");
                if (inline.TotalDebit != expectedDebit) failures.Add($"inline debit {Inv(inline.TotalDebit)} != {Inv(expectedDebit)}");
                if (inline.TotalCredit != expectedCredit) failures.Add($"inline credit {Inv(inline.TotalCredit)} != {Inv(expectedCredit)}");
            }
        }

        // 2. live aggregation straight from the event stream
        await using (var q = store.LightweightSession())
        {
            var live = await q.Events.AggregateStreamAsync<LedgerState>(entryId);
            if (live is null) failures.Add("live aggregation returned null");
            else
            {
                evidence.Add($"live aggregation     debit={Inv(live.TotalDebit)} credit={Inv(live.TotalCredit)} lines={live.LineCount}");
                if (live.TotalDebit != expectedDebit || live.TotalCredit != expectedCredit)
                    failures.Add("live aggregation does not match the appended events");
            }
        }

        // 3. hard rebuild: wipe the read model and rebuild it from the events
        await store.Advanced.Clean.DeleteDocumentsByTypeAsync(typeof(LedgerState));
        await using (var q = store.QuerySession())
        {
            if (await q.LoadAsync<LedgerState>(entryId) is not null)
                failures.Add("read model was not actually wiped before rebuild");
            else evidence.Add("read model wiped (LedgerState table emptied)");
        }

        using (var daemon = await store.BuildProjectionDaemonAsync())
        {
            await daemon.RebuildProjectionAsync<LedgerStateProjection>(CancellationToken.None);
        }

        await using (var q = store.QuerySession())
        {
            var rebuilt = await q.LoadAsync<LedgerState>(entryId);
            if (rebuilt is null) failures.Add("projection rebuild produced no document");
            else
            {
                evidence.Add($"REBUILT projection   debit={Inv(rebuilt.TotalDebit)} credit={Inv(rebuilt.TotalCredit)} lines={rebuilt.LineCount} ref={rebuilt.Reference}");
                if (rebuilt.TotalDebit != expectedDebit) failures.Add($"rebuilt debit {Inv(rebuilt.TotalDebit)} != {Inv(expectedDebit)}");
                if (rebuilt.TotalCredit != expectedCredit) failures.Add($"rebuilt credit {Inv(rebuilt.TotalCredit)} != {Inv(expectedCredit)}");
                if (rebuilt.Reference != "JV-2026-0003") failures.Add("rebuilt reference is wrong");
                if (rebuilt.LineCount != 3) failures.Add($"rebuilt line count {rebuilt.LineCount} != 3");
                var cash = rebuilt.BalanceByAccount.GetValueOrDefault("1010 Cash");
                if (cash != 1234567890.1234m) failures.Add($"rebuilt per-account balance wrong: {Inv(cash)}");
                else evidence.Add($"per-account balance '1010 Cash' = {Inv(cash)} (exact)");
            }
        }

        rec.Record("(c)", "EVENT STORE append + projection rebuilt from events",
            failures.Count == 0, string.Join("\n", evidence.Concat(failures.Select(f => "!! " + f))));
    }

    // =======================================================================
    // (d) WOLVERINE TRANSACTIONAL OUTBOX
    // =======================================================================
    public static async Task ProveOutboxAsync(IServiceProvider services, IDocumentStore store, ProofRecorder rec, string conn)
    {
        var evidence = new List<string>();
        var failures = new List<string>();
        MessageLog.Clear();

        await using var db = new NpgsqlConnection(conn);
        await db.OpenAsync();

        async Task<long> OutgoingCount()
        {
            await using var cmd = new NpgsqlCommand(
                $"select count(*) from {Schema}.wolverine_outgoing_envelopes", db);
            return (long)(await cmd.ExecuteScalarAsync())!;
        }

        // ------------------------------------------------------------------
        // CASE 1: transaction COMMITS -> message must be delivered
        // ------------------------------------------------------------------
        var committedId = Guid.NewGuid();
        using (var scope = services.CreateScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IMartenOutbox>();
            await using var session = store.LightweightSession();
            outbox.Enroll(session);

            await outbox.PublishAsync(new JournalPosted(committedId, "JV-COMMIT", 1234567890.1234m));

            // message must NOT have escaped before the commit
            if (MessageLog.Contains(committedId))
                failures.Add("message was delivered BEFORE SaveChangesAsync - not a real outbox");
            else
                evidence.Add("commit case: nothing delivered before SaveChangesAsync (held in outbox)");

            session.Store(new JournalEntry
            {
                Id = committedId,
                Reference = "JV-COMMIT",
                PostingDate = new DateOnly(2026, 8, 23),
                Lines = [new JournalLine("1010 Cash", 1234567890.1234m, 0m), new JournalLine("4000 Rev", 0m, 1234567890.1234m)]
            });
            await session.SaveChangesAsync();
        }

        var delivered = await MessageLog.WaitForAsync(committedId, TimeSpan.FromSeconds(30));
        if (!delivered) failures.Add("COMMIT case: message was never delivered after the transaction committed");
        else evidence.Add("commit case: JournalPosted DELIVERED after commit");

        await using (var q = store.QuerySession())
        {
            if (await q.LoadAsync<JournalEntry>(committedId) is null)
                failures.Add("COMMIT case: the journal entry itself was not persisted");
            else evidence.Add("commit case: journal entry is in the database");
        }

        // ------------------------------------------------------------------
        // CASE 2: transaction ROLLS BACK -> message must NOT be delivered.
        // We force a real Postgres error inside the same transaction that
        // carries the outbox rows, so the whole thing is rolled back.
        // ------------------------------------------------------------------
        var rolledBackId = Guid.NewGuid();
        var outgoingBefore = await OutgoingCount();
        var threw = false;

        using (var scope = services.CreateScope())
        {
            var outbox = scope.ServiceProvider.GetRequiredService<IMartenOutbox>();
            await using var session = store.LightweightSession();
            outbox.Enroll(session);

            await outbox.PublishAsync(new JournalPosted(rolledBackId, "JV-ROLLBACK", 555.5555m));

            session.Store(new JournalEntry
            {
                Id = rolledBackId,
                Reference = "JV-ROLLBACK",
                PostingDate = new DateOnly(2026, 8, 23),
                Lines = [new JournalLine("1010 Cash", 555.5555m, 0m), new JournalLine("4000 Rev", 0m, 555.5555m)]
            });

            // poison the same transaction: this table does not exist
            session.QueueSqlCommand($"insert into {Schema}.table_that_does_not_exist(id) values (1)");

            try
            {
                await session.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                threw = true;
                evidence.Add($"rollback case: SaveChangesAsync threw {ex.GetType().Name} -> transaction rolled back");
            }
        }

        if (!threw) failures.Add("ROLLBACK case: the poisoned transaction did not fail, test is invalid");

        var stayedAway = await MessageLog.StaysAbsentAsync(rolledBackId, TimeSpan.FromSeconds(15));
        if (!stayedAway) failures.Add("ROLLBACK case: message WAS delivered even though the transaction rolled back");
        else evidence.Add("rollback case: JournalPosted NOT delivered after 15s (correct)");

        await using (var q = store.QuerySession())
        {
            if (await q.LoadAsync<JournalEntry>(rolledBackId) is not null)
                failures.Add("ROLLBACK case: the journal entry was persisted despite the rollback");
            else evidence.Add("rollback case: journal entry absent from the database");
        }

        var outgoingAfter = await OutgoingCount();
        evidence.Add($"wolverine_outgoing_envelopes rows: before={outgoingBefore} after={outgoingAfter}");
        if (outgoingAfter > outgoingBefore)
            failures.Add($"ROLLBACK case: {outgoingAfter - outgoingBefore} outbox row(s) survived the rollback");

        rec.Record("(d)", "WOLVERINE OUTBOX atomic with the DB transaction (commit delivers, rollback does not)",
            failures.Count == 0, string.Join("\n", evidence.Concat(failures.Select(f => "!! " + f))));
    }

    // =======================================================================
    // (e) MULTI-TENANCY (conjoined) + Row Level Security investigation
    // =======================================================================
    public static async Task ProveMultiTenancyAsync(IDocumentStore store, ProofRecorder rec, string conn)
    {
        var evidence = new List<string>();
        var failures = new List<string>();

        var acmeId = Guid.NewGuid();
        var globexId = Guid.NewGuid();

        await using (var s = store.LightweightSession("acme"))
        {
            s.Store(new TenantScopedDoc { Id = acmeId, Name = "acme-secret-invoice", Amount = 1000.0001m });
            await s.SaveChangesAsync();
        }
        await using (var s = store.LightweightSession("globex"))
        {
            s.Store(new TenantScopedDoc { Id = globexId, Name = "globex-secret-invoice", Amount = 2000.0002m });
            await s.SaveChangesAsync();
        }

        await using (var acme = store.QuerySession("acme"))
        {
            var visible = await acme.Query<TenantScopedDoc>().ToListAsync();
            var names = visible.Select(v => v.Name).OrderBy(n => n).ToList();
            evidence.Add($"tenant 'acme' query sees: [{string.Join(", ", names)}]");
            if (names.Any(n => n.Contains("globex"))) failures.Add("acme session can see globex documents");
            if (!names.Contains("acme-secret-invoice")) failures.Add("acme session cannot see its own document");

            var crossLoad = await acme.LoadAsync<TenantScopedDoc>(globexId);
            if (crossLoad is not null) failures.Add("acme session loaded a globex document by id");
            else evidence.Add("tenant 'acme' cannot Load() globex's document by id (returns null)");
        }

        await using (var globex = store.QuerySession("globex"))
        {
            var visible = await globex.Query<TenantScopedDoc>().ToListAsync();
            evidence.Add($"tenant 'globex' query sees: [{string.Join(", ", visible.Select(v => v.Name))}]");
            if (visible.Any(v => v.Name.Contains("acme"))) failures.Add("globex session can see acme documents");
        }

        // confirm this is genuinely conjoined (tenant_id column in one table)
        await using var db = new NpgsqlConnection(conn);
        await db.OpenAsync();
        await using (var cmd = new NpgsqlCommand($"""
            select string_agg(column_name, ', ' order by ordinal_position)
            from information_schema.columns
            where table_schema='{Schema}' and table_name='mt_doc_tenantscopeddoc'
            """, db))
        {
            var cols = (string?)(await cmd.ExecuteScalarAsync()) ?? "(none)";
            evidence.Add($"mt_doc_tenantscopeddoc columns: {cols}");
            if (!cols.Contains("tenant_id")) failures.Add("no tenant_id column - conjoined tenancy is not actually configured");
        }

        await using (var cmd = new NpgsqlCommand(
            $"select tenant_id, count(*) from {Schema}.mt_doc_tenantscopeddoc group by tenant_id order by tenant_id", db))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            var rowsFound = new List<string>();
            while (await r.ReadAsync()) rowsFound.Add($"{r.GetString(0)}={r.GetInt64(1)}");
            evidence.Add($"raw table tenant_id distribution: {string.Join(" ", rowsFound)}");
        }

        rec.Record("(e)", "MULTI-TENANCY conjoined isolation (acme cannot see globex)",
            failures.Count == 0, string.Join("\n", evidence.Concat(failures.Select(f => "!! " + f))));

        await InvestigateRowLevelSecurityAsync(db, rec, conn);
    }

    /// <summary>
    /// Can Postgres Row Level Security be layered on Marten's tables as a second
    /// line of defence? Marten 9 has first-class support via
    /// StoreOptions.UseRowLevelSecurity(). This builds a second store with it
    /// enabled and verifies the policy actually confines a non-owner role.
    /// </summary>
    private static async Task InvestigateRowLevelSecurityAsync(NpgsqlConnection db, ProofRecorder rec, string conn)
    {
        const string RlsSchema = "babel_rls";
        const string Guc = "app.tenant_id";
        const string AppRole = "babel_rls_app";

        var evidence = new List<string>();
        var failures = new List<string>();

        await using var rlsStore = DocumentStore.For(opts =>
        {
            opts.Connection(conn);
            opts.DatabaseSchemaName = RlsSchema;
            opts.AutoCreateSchemaObjects = AutoCreate.All;
            opts.UseRowLevelSecurity(Guc);                 // <-- Marten's built-in RLS
            opts.Schema.For<TenantScopedDoc>().MultiTenanted();
        });

        await rlsStore.Storage.ApplyAllConfiguredChangesToDatabaseAsync();
        evidence.Add($"second store built with opts.UseRowLevelSecurity(\"{Guc}\") in schema '{RlsSchema}'");

        await using (var s = rlsStore.LightweightSession("acme"))
        {
            s.Store(new TenantScopedDoc { Name = "acme-rls-doc", Amount = 11.1111m });
            await s.SaveChangesAsync();
        }
        await using (var s = rlsStore.LightweightSession("globex"))
        {
            s.Store(new TenantScopedDoc { Name = "globex-rls-doc", Amount = 22.2222m });
            await s.SaveChangesAsync();
        }

        // Does Marten set the GUC on its own session connections?
        await using (var s = rlsStore.QuerySession("acme"))
        {
            await s.Query<TenantScopedDoc>().ToListAsync();     // force the connection open
            await using var c = new NpgsqlCommand($"select current_setting('{Guc}', true)", s.Connection);
            var guc = (string?)await c.ExecuteScalarAsync();
            evidence.Add($"inside a Marten session for tenant 'acme', current_setting('{Guc}') = '{guc}'");
            if (guc != "acme") failures.Add($"Marten did not set the GUC on the session connection (got '{guc}')");
        }

        // What policy did Marten actually write?
        await using (var cmd = new NpgsqlCommand($"""
            select policyname, cmd, coalesce(qual,'-')
            from pg_policies where schemaname = '{RlsSchema}' and tablename = 'mt_doc_tenantscopeddoc'
            """, db))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            var found = 0;
            while (await r.ReadAsync())
            {
                found++;
                evidence.Add($"policy '{r.GetString(0)}' cmd={r.GetString(1)} using({r.GetString(2)})");
            }
            if (found == 0) failures.Add("Marten created no RLS policy on the tenanted table");
        }

        await using (var cmd = new NpgsqlCommand($"""
            select c.relrowsecurity, c.relforcerowsecurity
            from pg_class c join pg_namespace n on n.oid = c.relnamespace
            where n.nspname = '{RlsSchema}' and c.relname = 'mt_doc_tenantscopeddoc'
            """, db))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            if (await r.ReadAsync())
            {
                var enabled = r.GetBoolean(0);
                var forced = r.GetBoolean(1);
                evidence.Add($"pg_class: relrowsecurity={enabled}, relforcerowsecurity={forced}");
                if (!enabled) failures.Add("row level security is not enabled on the table");
                if (!forced)
                    evidence.Add("NOTE: FORCE is off, so the TABLE OWNER bypasses the policy. " +
                                 "Marten must run as a role that does not own its tables, or you must " +
                                 "'alter table ... force row level security' yourself.");
            }
        }

        // The real test: a plain, non-owner, non-superuser role.
        await using (var cmd = new NpgsqlCommand($"""
            do $$ begin
                if not exists (select 1 from pg_roles where rolname = '{AppRole}') then
                    create role {AppRole} login;
                end if;
            end $$;
            grant usage on schema {RlsSchema} to {AppRole};
            grant select, insert, update, delete on all tables in schema {RlsSchema} to {AppRole};
            """, db))
        {
            try { await cmd.ExecuteNonQueryAsync(); evidence.Add($"created non-owner login role '{AppRole}' with table grants"); }
            catch (Exception ex) { failures.Add($"could not create the app role: {ex.Message}"); }
        }

        var appConn = new NpgsqlConnectionStringBuilder(conn) { Username = AppRole, Password = null }.ToString();
        try
        {
            await using var appDb = new NpgsqlConnection(appConn);
            await appDb.OpenAsync();

            async Task<string> SeenBy(string tenant)
            {
                await using (var set = new NpgsqlCommand($"select set_config('{Guc}', '{tenant}', false)", appDb))
                    await set.ExecuteNonQueryAsync();
                await using var q = new NpgsqlCommand(
                    $"select coalesce(string_agg(distinct tenant_id, ','), '(none)') from {RlsSchema}.mt_doc_tenantscopeddoc", appDb);
                return (string)(await q.ExecuteScalarAsync())!;
            }

            var asAcme = await SeenBy("acme");
            evidence.Add($"non-owner role with {Guc}='acme' sees tenant_ids: {asAcme}");
            if (asAcme != "acme") failures.Add($"RLS did not confine the app role to acme (saw '{asAcme}')");

            var asGlobex = await SeenBy("globex");
            evidence.Add($"same connection switched to {Guc}='globex' sees: {asGlobex}");
            if (asGlobex != "globex") failures.Add($"RLS did not switch tenants correctly (saw '{asGlobex}')");

            // no tenant set at all => nothing visible
            await using (var reset = new NpgsqlCommand($"select set_config('{Guc}', '', false)", appDb))
                await reset.ExecuteNonQueryAsync();
            await using (var q = new NpgsqlCommand(
                $"select coalesce(string_agg(distinct tenant_id, ','), '(none)') from {RlsSchema}.mt_doc_tenantscopeddoc", appDb))
            {
                var seen = (string)(await q.ExecuteScalarAsync())!;
                evidence.Add($"non-owner role with {Guc} unset sees: {seen}");
                if (seen != "(none)") failures.Add($"a connection with no tenant GUC could still read rows ('{seen}')");
            }
        }
        catch (Exception ex)
        {
            failures.Add($"RLS app-role test failed: {ex.Message}");
        }

        // Superuser caveat, stated as evidence rather than a failure.
        await using (var cmd = new NpgsqlCommand(
            $"select coalesce(string_agg(distinct tenant_id, ','), '(none)') from {RlsSchema}.mt_doc_tenantscopeddoc", db))
        {
            var seen = (string)(await cmd.ExecuteScalarAsync())!;
            evidence.Add($"CAVEAT: superuser/table-owner connection with no tenant GUC still sees: {seen} " +
                         "- superusers always bypass RLS, so the application must connect as an unprivileged role");
        }

        rec.Record("(e2)", "ROW LEVEL SECURITY layered on Marten's tables (second line of defence)",
            failures.Count == 0, string.Join("\n", evidence.Concat(failures.Select(f => "!! " + f))));
    }
}
