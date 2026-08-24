using System.Globalization;
using BabelRelationalSpike.Db;
using BabelRelationalSpike.Support;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace BabelRelationalSpike.Proofs;

/// <summary>
/// (B) EF Core 10 owns an append-only ledger whose immutability is enforced by
///     PostgreSQL itself: the application role holds INSERT + SELECT and nothing
///     else, and a DEFERRABLE INITIALLY DEFERRED constraint trigger refuses an
///     unbalanced entry at COMMIT no matter which code path wrote the rows.
/// </summary>
public static class ProofB_Ledger
{
    /// <summary>The exact values the evaluation asked for.</summary>
    private static readonly decimal[] MoneyProbes =
    [
        1234567890.1234m,
        0.0001m,
        99999999999999.9999m,
        100.00m
    ];

    public static async Task RunAsync(IServiceProvider services, ProofRecorder rec)
    {
        rec.Section("(B) EF Core 10 append-only ledger with revoked grants");

        // ---- B0 : the application role really is least-privilege ----------
        var roleFacts = await Sql.TableAsync(Config.Admin, $"""
            select rolname, rolsuper as is_superuser, rolcreatedb, rolcreaterole,
                   pg_catalog.pg_get_userbyid(c.relowner) as journal_entry_owner
            from pg_roles r
            cross join pg_class c
            join pg_namespace n on n.oid = c.relnamespace
            where r.rolname = '{Config.AppRole}' and n.nspname = 'ledger' and c.relname = 'journal_entry'
            """);
        var grants = await Sql.TableAsync(Config.Admin, $"""
            select table_name, string_agg(privilege_type, ', ' order by privilege_type) as privileges
            from information_schema.table_privileges
            where grantee = '{Config.AppRole}' and table_schema in ('ledger','app')
            group by table_name order by table_name
            """);
        var noWrite = await Sql.ScalarAsync<long>(Config.Admin, $"""
            select count(*) from information_schema.table_privileges
            where grantee = '{Config.AppRole}' and table_schema = 'ledger'
              and table_name in ('journal_entry','journal_line','process_event')
              and privilege_type in ('UPDATE','DELETE','TRUNCATE')
            """);
        var isSuper = await Sql.ScalarAsync<bool>(Config.Admin,
            $"select rolsuper from pg_roles where rolname = '{Config.AppRole}'");
        rec.Check("B0", "application role is non-superuser, non-owner, INSERT+SELECT only",
            noWrite == 0 && !isSuper, $"{roleFacts}\n\n{grants}\n\nUPDATE/DELETE/TRUNCATE grants on the append-only tables: {noWrite}");

        // ---- B1 : a balanced entry inserts through EF Core ----------------
        await using var ctx = Contexts.Create();
        JournalEntry posted;
        try
        {
            posted = await Ledger.PostAsync(ctx, "MAIN", "acme", new DateOnly(2026, 3, 15),
                "opening sale", "قيد مبيعات افتتاحي", "muhasib@acme.sa",
                [
                    new LineSpec(1, "1010", "النقدية", 1150.0000m, 0m),
                    new LineSpec(2, "4010", "المبيعات", 0m, 1000.0000m),
                    new LineSpec(3, "2310", "ضريبة القيمة المضافة المستحقة", 0m, 150.0000m)
                ]);
            var stored = await Sql.TableAsync(Config.Admin,
                $"select line_no, account_code, debit, credit from ledger.journal_line " +
                $"where entry_id = '{posted.EntryId}' order by line_no");
            rec.Pass("B1", "balanced entry INSERTs through EF Core as the app role",
                $"entry_no={posted.EntryNo} chain_seq={posted.ChainSeq} (15% VAT split)\n{stored}");
        }
        catch (Exception ex)
        {
            rec.Fail("B1", "balanced entry INSERTs through EF Core as the app role", ex.Message);
            return;
        }

        // ---- B2 : UPDATE of a posted line is refused by PostgreSQL --------
        var lineId = await Sql.ScalarAsync<Guid>(Config.Admin,
            $"select line_id from ledger.journal_line where entry_id = '{posted.EntryId}' order by line_no limit 1");

        string efUpdateError = "(EF Core UPDATE unexpectedly SUCCEEDED)";
        try
        {
            await using var c2 = Contexts.Create();
            var line = await c2.JournalLines.SingleAsync(l => l.LineId == lineId);
            line.Debit = 9_999_999.0000m;
            await c2.SaveChangesAsync();
        }
        catch (Exception ex) when (Unwrap(ex) is PostgresException pg)
        {
            efUpdateError = Sql.Describe(pg);
        }

        var rawUpdate = await Sql.ExpectFailureAsync(Config.App,
            $"update ledger.journal_line set debit = 1 where line_id = '{lineId}'");

        rec.Check("B2", "UPDATE of a posted line is REJECTED by PostgreSQL itself",
            efUpdateError.StartsWith("SQLSTATE 42501") && rawUpdate?.SqlState == "42501",
            $"through EF Core 10 SaveChangesAsync : {efUpdateError}\n" +
            $"through raw SQL on the same role    : {(rawUpdate is null ? "SUCCEEDED - FAIL" : Sql.Describe(rawUpdate))}");

        // ---- B3 : DELETE of a posted entry is refused ---------------------
        string efDeleteError = "(EF Core DELETE unexpectedly SUCCEEDED)";
        try
        {
            await using var c3 = Contexts.Create();
            var line = await c3.JournalLines.SingleAsync(l => l.LineId == lineId);
            c3.JournalLines.Remove(line);
            await c3.SaveChangesAsync();
        }
        catch (Exception ex) when (Unwrap(ex) is PostgresException pg)
        {
            efDeleteError = Sql.Describe(pg);
        }

        var rawDeleteLine = await Sql.ExpectFailureAsync(Config.App,
            $"delete from ledger.journal_line where entry_id = '{posted.EntryId}'");
        var rawDeleteEntry = await Sql.ExpectFailureAsync(Config.App,
            $"delete from ledger.journal_entry where entry_id = '{posted.EntryId}'");
        var rawTruncate = await Sql.ExpectFailureAsync(Config.App, "truncate ledger.journal_line");

        rec.Check("B3", "DELETE (and TRUNCATE) of a posted entry is REJECTED by PostgreSQL itself",
            efDeleteError.StartsWith("SQLSTATE 42501") && rawDeleteLine?.SqlState == "42501" &&
            rawDeleteEntry?.SqlState == "42501" && rawTruncate?.SqlState == "42501",
            $"EF Core delete of a line   : {efDeleteError}\n" +
            $"raw delete of the lines    : {Describe(rawDeleteLine)}\n" +
            $"raw delete of the entry    : {Describe(rawDeleteEntry)}\n" +
            $"raw TRUNCATE of the table  : {Describe(rawTruncate)}");

        // ---- B4 : deferred constraint trigger, 0.0001 out, any code path --
        string efImbalance = "(EF Core accepted an unbalanced entry)";
        bool firedAtCommit = false;
        try
        {
            await using var c4 = Contexts.Create();
            await using var tx = await c4.Database.BeginTransactionAsync();
            await Ledger.BuildAndInsertAsync(c4, "MAIN", "acme", new DateOnly(2026, 3, 16),
                "off by a hair", "فرق بمقدار جزء من عشرة آلاف", "muhasib@acme.sa",
                [
                    new LineSpec(1, "1010", "النقدية", 1000.0000m, 0m),
                    new LineSpec(2, "4010", "المبيعات", 0m, 999.9999m)
                ]);
            await c4.SaveChangesAsync();     // INSERTs succeed - the trigger is DEFERRED
            firedAtCommit = true;            // nothing thrown yet
            await tx.CommitAsync();          // <- this is where PostgreSQL says no
            firedAtCommit = false;
        }
        catch (Exception ex) when (Unwrap(ex) is PostgresException pg)
        {
            efImbalance = Sql.Describe(pg);
        }

        // the identical entry written by raw SQL, bypassing EF Core entirely
        var rawImbalance = await Sql.ExpectFailureAsync(Config.App, $"""
            begin;
            insert into ledger.journal_entry
                (entry_id, book_id, tenant_id, entry_no, chain_seq, entry_date, memo, memo_ar,
                 posted_at, actor, prev_hash, entry_hash)
            values ('{Guid.NewGuid()}', 'MAIN', 'acme', 900001, 900001, date '2026-03-16',
                    'raw sql bypass', 'تجاوز عبر SQL مباشر', now(), 'dba', '\x00'::bytea, '\x00'::bytea);
            insert into ledger.journal_line (line_id, entry_id, line_no, account_code, description, debit, credit)
            select '{Guid.NewGuid()}', entry_id, 1, '1010', 'x', 1000.0000, 0
              from ledger.journal_entry where entry_no = 900001;
            insert into ledger.journal_line (line_id, entry_id, line_no, account_code, description, debit, credit)
            select '{Guid.NewGuid()}', entry_id, 2, '4010', 'y', 0, 999.9999
              from ledger.journal_entry where entry_no = 900001;
            commit;
            """);

        var singleLine = await Sql.ExpectFailureAsync(Config.App, $"""
            begin;
            insert into ledger.journal_entry
                (entry_id, book_id, tenant_id, entry_no, chain_seq, entry_date, memo, memo_ar,
                 posted_at, actor, prev_hash, entry_hash)
            values ('{Guid.NewGuid()}', 'MAIN', 'acme', 900002, 900002, date '2026-03-16',
                    'one legged', 'قيد بطرف واحد', now(), 'dba', '\x00'::bytea, '\x00'::bytea);
            commit;
            """);

        rec.Check("B4", "DEFERRABLE INITIALLY DEFERRED trigger rejects a 0.0001 imbalance at COMMIT",
            efImbalance.StartsWith("SQLSTATE 23514") && rawImbalance?.SqlState == "23514" &&
            singleLine?.SqlState == "23514" && firedAtCommit,
            $"the INSERTs themselves succeeded, the COMMIT did not: {firedAtCommit}\n" +
            $"through EF Core 10 : {efImbalance}\n" +
            $"through raw SQL    : {Describe(rawImbalance)}\n" +
            $"entry with no lines: {Describe(singleLine)}\n" +
            "1000.0000 vs 999.9999 - one ten-thousandth of a riyal is enough");

        // ---- B5 : decimal round-trip through EF Core, bit for bit ---------
        await RoundTripAsync(rec);
    }

    private static async Task RoundTripAsync(ProofRecorder rec)
    {
        // one balanced entry carrying every probe value as a debit and its mirror as a credit
        var lines = new List<LineSpec>();
        var n = 1;
        foreach (var v in MoneyProbes)
        {
            lines.Add(new LineSpec(n++, "1010", "probe debit", v, 0m));
            lines.Add(new LineSpec(n++, "4010", "probe credit", 0m, v));
        }

        await using var ctx = Contexts.Create();
        var entry = await Ledger.PostAsync(ctx, "MAIN", "acme", new DateOnly(2026, 3, 17),
            "decimal probes", "قيم اختبار الدقة", "muhasib@acme.sa", lines);

        // fresh context => no identity map, the values really come back from PostgreSQL
        await using var read = Contexts.Create();
        var back = await read.JournalLines.AsNoTracking()
            .Where(l => l.EntryId == entry.EntryId).OrderBy(l => l.LineNo).ToListAsync();

        var rows = new List<string>();
        var valueOk = true;
        var bitsOk = true;
        var scaleNotes = new List<string>();

        foreach (var v in MoneyProbes)
        {
            var got = back.Single(l => l.LineNo == MoneyProbes.ToList().IndexOf(v) * 2 + 1).Debit;
            var same = got == v;
            var sameBits = decimal.GetBits(got).SequenceEqual(decimal.GetBits(v));
            valueOk &= same;
            if (!sameBits)
            {
                // numeric(19,4) is a FIXED scale: 100.00 comes back as 100.0000.
                // Same value, different scale byte - only acceptable if the value matches.
                scaleNotes.Add($"{Str(v)} (scale {Scale(v)}) -> {Str(got)} (scale {Scale(got)})");
                bitsOk &= same;   // tolerated only because the value is identical
            }
            rows.Add($"  in={Str(v),-24} out={Str(got),-24} ==:{(same ? "yes" : "NO ")}  " +
                     $"GetBits:{(sameBits ? "identical" : "scale normalised")}  " +
                     $"bits(out)={string.Join(",", decimal.GetBits(got))}");
        }

        // a canonical scale-4 value must survive bit for bit
        var canonical = 100.0000m;
        await using var ctx2 = Contexts.Create();
        var e2 = await Ledger.PostAsync(ctx2, "MAIN", "acme", new DateOnly(2026, 3, 18),
            "canonical scale", "قيمة بمقياس أربع خانات", "muhasib@acme.sa",
            [new LineSpec(1, "1010", "d", canonical, 0m), new LineSpec(2, "4010", "c", 0m, canonical)]);
        await using var read2 = Contexts.Create();
        var canonicalBack = (await read2.JournalLines.AsNoTracking()
            .SingleAsync(l => l.EntryId == e2.EntryId && l.LineNo == 1)).Debit;
        var canonicalBits = decimal.GetBits(canonicalBack).SequenceEqual(decimal.GetBits(canonical));

        rec.Check("B5", "decimal round-trips through EF Core with no loss of value",
            valueOk && bitsOk && canonicalBits,
            string.Join("\n", rows) +
            $"\n  canonical 100.0000m round-trip: GetBits identical = {canonicalBits} " +
            $"(bits {string.Join(",", decimal.GetBits(canonicalBack))})" +
            (scaleNotes.Count == 0 ? "" :
             "\n  HONEST FINDING - numeric(19,4) is a FIXED scale, so PostgreSQL normalises\n" +
             "  the trailing zeros of a lower-scale input:\n    " + string.Join("\n    ", scaleNotes) +
             "\n  The VALUE is preserved exactly; only decimal's scale byte changes. Domain code\n" +
             "  must therefore treat scale-4 as the canonical form (that is also what the hash\n" +
             "  chain in (E) hashes), or decimal.GetBits comparisons will report false diffs."));
    }

    private static string Str(decimal d) => d.ToString(CultureInfo.InvariantCulture);
    private static int Scale(decimal d) => (decimal.GetBits(d)[3] >> 16) & 0xFF;
    private static string Describe(PostgresException? ex) => ex is null ? "SUCCEEDED - FAIL" : Sql.Describe(ex);

    private static Exception? Unwrap(Exception ex)
    {
        for (var e = ex; e is not null; e = e.InnerException)
            if (e is PostgresException) return e;
        return null;
    }
}
