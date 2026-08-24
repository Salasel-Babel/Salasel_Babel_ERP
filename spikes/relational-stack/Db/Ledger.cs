using BabelRelationalSpike.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BabelRelationalSpike.Db;

public readonly record struct LineSpec(int LineNo, string Account, string Description, decimal Debit, decimal Credit);

public sealed record ChainVerification(bool Ok, long? FirstDivergentSeq, string Reason, int Checked)
{
    public static ChainVerification Good(int n) => new(true, null, "chain intact", n);
}

/// <summary>
/// The single write path for the general ledger: gapless counter taken with
/// SELECT ... FOR UPDATE inside the business transaction, SHA-256 chain link
/// computed over canonical bytes that include the sequence number and the
/// previous hash, then one EF Core SaveChanges + COMMIT (where the deferred
/// balance trigger fires).
/// </summary>
public static class Ledger
{
    public static async Task EnsureBookAsync(string bookId)
    {
        await Sql.ExecAsync(Config.Admin, $"""
            insert into ledger.entry_counter (book_id, next_no, next_seq)
            values ('{bookId}', 1, 1)
            on conflict (book_id) do nothing
            """);
    }

    public static async Task<JournalEntry> PostAsync(
        LedgerDbContext ctx,
        string bookId,
        string tenantId,
        DateOnly date,
        string memo,
        string memoAr,
        string actor,
        IReadOnlyList<LineSpec> lines,
        bool commit = true,
        CancellationToken ct = default)
    {
        await using var tx = await ctx.Database.BeginTransactionAsync(ct);
        var entry = await BuildAndInsertAsync(ctx, bookId, tenantId, date, memo, memoAr, actor, lines, ct);
        await ctx.SaveChangesAsync(ct);
        if (commit) await tx.CommitAsync(ct);   // deferred constraint trigger fires HERE
        else await tx.RollbackAsync(ct);
        return entry;
    }

    /// <summary>Everything except transaction control, so callers can share a transaction (outbox proof).</summary>
    public static async Task<JournalEntry> BuildAndInsertAsync(
        LedgerDbContext ctx,
        string bookId,
        string tenantId,
        DateOnly date,
        string memo,
        string memoAr,
        string actor,
        IReadOnlyList<LineSpec> lines,
        CancellationToken ct = default)
    {
        var conn = (NpgsqlConnection)ctx.Database.GetDbConnection();
        var tx = (NpgsqlTransaction)ctx.Database.CurrentTransaction!.GetDbTransaction();

        // --- gapless counter: a row lock, NOT a sequence -------------------
        long entryNo, chainSeq;
        await using (var cmd = new NpgsqlCommand(
            "select next_no, next_seq from ledger.entry_counter where book_id = @b for update", conn, tx))
        {
            cmd.Parameters.AddWithValue("b", bookId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                throw new InvalidOperationException($"no counter row for book '{bookId}'");
            entryNo = r.GetInt64(0);
            chainSeq = r.GetInt64(1);
        }

        // --- previous link, read under the same counter lock ---------------
        byte[] prevHash;
        await using (var cmd = new NpgsqlCommand(
            "select entry_hash from ledger.journal_entry where book_id = @b and chain_seq = @s", conn, tx))
        {
            cmd.Parameters.AddWithValue("b", bookId);
            cmd.Parameters.AddWithValue("s", chainSeq - 1);
            var v = await cmd.ExecuteScalarAsync(ct);
            prevHash = v is byte[] bytes ? bytes : Canonical.Genesis(bookId);
        }

        var entry = new JournalEntry
        {
            EntryId = Guid.CreateVersion7(),
            BookId = bookId,
            TenantId = tenantId,
            EntryNo = entryNo,
            ChainSeq = chainSeq,
            EntryDate = date,
            Memo = memo,
            MemoAr = memoAr,
            PostedAt = Canonical.PgInstant(DateTime.UtcNow),
            Actor = actor,
            PrevHash = prevHash,
            Lines = [.. lines.Select(l => new JournalLine
            {
                LineId = Guid.CreateVersion7(),
                LineNo = l.LineNo,
                AccountCode = l.Account,
                Description = l.Description,
                Debit = l.Debit,
                Credit = l.Credit
            })]
        };
        entry.EntryHash = Canonical.Hash(entry, entry.Lines);

        ctx.JournalEntries.Add(entry);

        await using (var cmd = new NpgsqlCommand(
            "update ledger.entry_counter set next_no = next_no + 1, next_seq = next_seq + 1 where book_id = @b",
            conn, tx))
        {
            cmd.Parameters.AddWithValue("b", bookId);
            await cmd.ExecuteNonQueryAsync(ct);
        }

        return entry;
    }

    /// <summary>
    /// Independent verification pass. Reads the stored rows and rebuilds the
    /// chain from the genesis link, naming the FIRST sequence number that
    /// diverges. Runs on any connection with SELECT rights.
    /// </summary>
    public static async Task<ChainVerification> VerifyAsync(string connectionString, string bookId)
    {
        await using var conn = await Sql.OpenAsync(connectionString);

        var entries = new List<JournalEntry>();
        await using (var cmd = new NpgsqlCommand("""
            select entry_id, book_id, tenant_id, entry_no, chain_seq, entry_date,
                   memo, memo_ar, posted_at, actor, prev_hash, entry_hash
            from ledger.journal_entry where book_id = @b order by chain_seq
            """, conn))
        {
            cmd.Parameters.AddWithValue("b", bookId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                entries.Add(new JournalEntry
                {
                    EntryId = r.GetGuid(0),
                    BookId = r.GetString(1),
                    TenantId = r.GetString(2),
                    EntryNo = r.GetInt64(3),
                    ChainSeq = r.GetInt64(4),
                    EntryDate = r.GetFieldValue<DateOnly>(5),
                    Memo = r.GetString(6),
                    MemoAr = r.GetString(7),
                    PostedAt = r.GetFieldValue<DateTime>(8),
                    Actor = r.GetString(9),
                    PrevHash = (byte[])r["prev_hash"],
                    EntryHash = (byte[])r["entry_hash"]
                });
        }

        var linesByEntry = new Dictionary<Guid, List<JournalLine>>();
        await using (var cmd = new NpgsqlCommand("""
            select l.entry_id, l.line_no, l.account_code, l.description, l.debit, l.credit
            from ledger.journal_line l
            join ledger.journal_entry e on e.entry_id = l.entry_id
            where e.book_id = @b order by l.entry_id, l.line_no
            """, conn))
        {
            cmd.Parameters.AddWithValue("b", bookId);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = r.GetGuid(0);
                if (!linesByEntry.TryGetValue(id, out var list)) linesByEntry[id] = list = [];
                list.Add(new JournalLine
                {
                    EntryId = id,
                    LineNo = r.GetInt32(1),
                    AccountCode = r.GetString(2),
                    Description = r.GetString(3),
                    Debit = r.GetDecimal(4),
                    Credit = r.GetDecimal(5)
                });
            }
        }

        var expectedPrev = Canonical.Genesis(bookId);
        long expectedSeq = 1;
        foreach (var e in entries)
        {
            if (e.ChainSeq != expectedSeq)
                return new ChainVerification(false, e.ChainSeq,
                    $"gap or reordering: expected chain_seq {expectedSeq}, found {e.ChainSeq}", entries.Count);

            if (!e.PrevHash.AsSpan().SequenceEqual(expectedPrev))
                return new ChainVerification(false, e.ChainSeq,
                    $"broken link at chain_seq {e.ChainSeq}: stored prev_hash {Canonical.Hex(e.PrevHash)[..16]}... " +
                    $"!= previous entry_hash {Canonical.Hex(expectedPrev)[..16]}...", entries.Count);

            var lines = linesByEntry.TryGetValue(e.EntryId, out var l) ? l : [];
            var recomputed = Canonical.Hash(e, lines);
            if (!recomputed.AsSpan().SequenceEqual(e.EntryHash))
                return new ChainVerification(false, e.ChainSeq,
                    $"content tampered at chain_seq {e.ChainSeq} (entry_no {e.EntryNo}): recomputed " +
                    $"{Canonical.Hex(recomputed)[..16]}... != stored {Canonical.Hex(e.EntryHash)[..16]}...",
                    entries.Count);

            expectedPrev = e.EntryHash;
            expectedSeq++;
        }
        return ChainVerification.Good(entries.Count);
    }
}
