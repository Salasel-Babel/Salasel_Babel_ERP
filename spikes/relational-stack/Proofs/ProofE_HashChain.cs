using System.Globalization;
using System.Text;
using BabelRelationalSpike.Db;
using BabelRelationalSpike.Support;
using Npgsql;

namespace BabelRelationalSpike.Proofs;

/// <summary>
/// (E) Gapless document numbering with SELECT ... FOR UPDATE (never a sequence),
///     a SHA-256 chain whose canonical bytes CONTAIN the sequence number and the
///     previous hash, and detection of a tamper performed by the table OWNER.
/// </summary>
public static class ProofE_HashChain
{
    public static async Task RunAsync(IServiceProvider services, ProofRecorder rec)
    {
        rec.Section("(E) hash chain + gapless counter on the relational ledger");

        await ProveSequenceLeaksAsync(rec);
        await ProveGaplessCounterAsync(rec);
        await ProveCanonicalisationAsync(rec);
        await ProveChainAndTamperAsync(rec);
    }

    // -----------------------------------------------------------------------
    // E1 : why NOT a PostgreSQL sequence
    // -----------------------------------------------------------------------
    private static async Task ProveSequenceLeaksAsync(ProofRecorder rec)
    {
        await Sql.ExecAsync(Config.Admin, "alter sequence ledger.leaky_demo_seq restart with 1");

        long first, afterRollback;
        await using (var conn = await Sql.OpenAsync(Config.App))
        {
            await using var tx = await conn.BeginTransactionAsync();
            first = await Sql.ScalarAsync<long>(conn, "select nextval('ledger.leaky_demo_seq')", tx);
            await tx.RollbackAsync();
        }
        afterRollback = await Sql.ScalarAsync<long>(Config.App, "select nextval('ledger.leaky_demo_seq')");

        rec.Check("E1", "a PostgreSQL SEQUENCE leaks numbers on rollback (so we do not use one)",
            first == 1 && afterRollback == 2,
            $"inside a transaction nextval() returned {first}\n" +
            $"the transaction ROLLED BACK\n" +
            $"the next nextval() returned {afterRollback}  ->  document number {first} can never exist\n" +
            "For a ZATCA-audited journal a missing number reads as a deleted document.\n" +
            "الأرقام المتسلسلة في PostgreSQL لا تعود عند التراجع، فتظهر فجوة يفسّرها المدقّق كمستند محذوف.");
    }

    // -----------------------------------------------------------------------
    // E2 : the gapless counter row
    // -----------------------------------------------------------------------
    private static async Task ProveGaplessCounterAsync(ProofRecorder rec)
    {
        const string book = "GAPLESS";
        await Sql.ExecAsync(Config.Admin,
            $"insert into ledger.entry_counter (book_id, next_no, next_seq) values ('{book}', 1, 1) " +
            "on conflict (book_id) do update set next_no = 1, next_seq = 1");
        await Sql.ExecAsync(Config.Admin, $"delete from ledger.journal_line where entry_id in " +
            $"(select entry_id from ledger.journal_entry where book_id = '{book}'); " +
            $"delete from ledger.journal_entry where book_id = '{book}'");

        // (i) a rolled back post does NOT consume a number
        await using (var ctx = Contexts.Create())
        {
            await Ledger.PostAsync(ctx, book, "acme", new DateOnly(2026, 4, 1), "doomed", "قيد متراجَع", "spike",
                [new LineSpec(1, "1010", "d", 10.0000m, 0m), new LineSpec(2, "4010", "c", 0m, 10.0000m)],
                commit: false);
        }
        var afterRollback = await Sql.ScalarAsync<long>(Config.Admin,
            $"select next_no from ledger.entry_counter where book_id = '{book}'");

        // (ii) 8 concurrent writers, 6 entries each, every 5th one deliberately rolled back
        const int writers = 8, perWriter = 6;
        var tasks = Enumerable.Range(0, writers).Select(async w =>
        {
            await using var ctx = Contexts.Create();
            for (var i = 0; i < perWriter; i++)
            {
                var rollback = (w * perWriter + i) % 5 == 4;
                try
                {
                    await Ledger.PostAsync(ctx, book, "acme", new DateOnly(2026, 4, 2),
                        $"w{w}-{i}", $"كاتب {w} قيد {i}", "spike",
                        [new LineSpec(1, "1010", "d", 25.0000m, 0m), new LineSpec(2, "4010", "c", 0m, 25.0000m)],
                        commit: !rollback);
                }
                catch (PostgresException) { /* counted as a rollback */ }
            }
        });
        await Task.WhenAll(tasks);

        var numbers = new List<long>();
        await using (var conn = await Sql.OpenAsync(Config.Admin))
        await using (var cmd = new NpgsqlCommand(
            $"select entry_no from ledger.journal_entry where book_id = '{book}' order by entry_no", conn))
        await using (var r = await cmd.ExecuteReaderAsync())
            while (await r.ReadAsync()) numbers.Add(r.GetInt64(0));

        var expectedCommitted = writers * perWriter - Enumerable.Range(0, writers * perWriter).Count(x => x % 5 == 4);
        var contiguous = numbers.Select((n, i) => n == i + 1).All(x => x);
        var noDupes = numbers.Distinct().Count() == numbers.Count;

        rec.Check("E2", "SELECT ... FOR UPDATE counter is gapless under 8 concurrent writers",
            afterRollback == 1 && contiguous && noDupes && numbers.Count == expectedCommitted,
            $"after a single ROLLED BACK post the counter is still at {afterRollback} (no number burned)\n" +
            $"{writers} concurrent writers x {perWriter} attempts, {writers * perWriter - expectedCommitted} deliberately rolled back\n" +
            $"committed entries: {numbers.Count} (expected {expectedCommitted})\n" +
            $"entry_no range   : {(numbers.Count == 0 ? "-" : $"{numbers[0]}..{numbers[^1]}")}\n" +
            $"contiguous 1..N  : {contiguous}   duplicates: {(noDupes ? "none" : "FOUND")}");
    }

    // -----------------------------------------------------------------------
    // E3 : canonicalisation - Arabic, NFC, bidi controls, invariant decimals
    // -----------------------------------------------------------------------
    private static async Task ProveCanonicalisationAsync(ProofRecorder rec)
    {
        await Task.CompletedTask;

        JournalEntry Make(string memoAr, decimal amount = 1500.0000m) => new()
        {
            EntryId = Guid.Parse("0192f3c8-0000-7000-8000-000000000001"),
            BookId = "MAIN", TenantId = "acme", EntryNo = 42, ChainSeq = 42,
            EntryDate = new DateOnly(2026, 5, 1),
            PostedAt = new DateTime(2026, 5, 1, 9, 30, 0, DateTimeKind.Utc),
            Actor = "muhasib@acme.sa",
            Memo = "revenue recognition",
            MemoAr = memoAr,
            PrevHash = Canonical.Genesis("MAIN"),
            Lines =
            [
                new JournalLine { LineNo = 1, AccountCode = "1010", Description = "النقدية", Debit = amount, Credit = 0m },
                new JournalLine { LineNo = 2, AccountCode = "4010", Description = "المبيعات", Debit = 0m, Credit = amount }
            ]
        };

        // -- money formatting must be culture independent --------------------
        var invariant = Canonical.Money(1500m);
        var invariant2 = Canonical.Money(1500.0000m);
        var german = 1500m.ToString("0.0000", new CultureInfo("de-DE"));
        var arabic = 1500m.ToString("0.0000", new CultureInfo("ar-SA"));
        var moneyOk = invariant == "1500.0000" && invariant == invariant2 && german != invariant;

        // -- the same entry, hashed at three decimal scales -------------------
        var h100 = Canonical.Hash(Make("قيد إثبات إيراد مبيعات - فرع الرياض", 1500m), Make("قيد إثبات إيراد مبيعات - فرع الرياض", 1500m).Lines);
        var h1004 = Canonical.Hash(Make("قيد إثبات إيراد مبيعات - فرع الرياض", 1500.0000m), Make("قيد إثبات إيراد مبيعات - فرع الرياض", 1500.0000m).Lines);
        var scaleStable = h100.AsSpan().SequenceEqual(h1004);

        // -- NFC: composed vs decomposed Arabic must hash identically ---------
        const string composed = "قيد أرباح";                       // U+0623 ALEF WITH HAMZA ABOVE
        var decomposed = "قيد أرباح";                    // U+0627 + U+0654
        var eC = Make(composed); var eD = Make(decomposed);
        var hC = Canonical.Hash(eC, eC.Lines);
        var hD = Canonical.Hash(eD, eD.Lines);
        var nfcOk = hC.AsSpan().SequenceEqual(hD) &&
                    !Encoding.UTF8.GetBytes(composed).SequenceEqual(Encoding.UTF8.GetBytes(decomposed));

        rec.Check("E3", "canonical form: invariant fixed-scale money, UTC, NFC-normalised Arabic",
            moneyOk && scaleStable && nfcOk,
            $"money  : invariant \"{invariant}\"  |  de-DE \"{german}\"  |  ar-SA \"{arabic}\"\n" +
            $"         1500m and 1500.0000m produce the SAME hash: {scaleStable}\n" +
            $"NFC    : \"{composed}\" (U+0623) and the decomposed form (U+0627 U+0654) are\n" +
            $"         {Encoding.UTF8.GetBytes(composed).Length} vs {Encoding.UTF8.GetBytes(decomposed).Length} raw UTF-8 bytes, " +
            $"yet hash identically: {hC.AsSpan().SequenceEqual(hD)}\n" +
            $"         hash = {Canonical.Hex(hC)[..32]}...");

        // -- the bidi trap ----------------------------------------------------
        const string clean = "قيد إثبات إيراد مبيعات - فرع الرياض";
        var withRlm = clean.Insert(4, "‏");                    // an invisible RIGHT-TO-LEFT MARK
        var eClean = Make(clean); var eRlm = Make(withRlm);
        var hClean = Canonical.Hash(eClean, eClean.Lines);
        var hRlm = Canonical.Hash(eRlm, eRlm.Lines);
        var looksIdentical = clean != withRlm;
        var hashChanged = !hClean.AsSpan().SequenceEqual(hRlm);

        // and the hardened canonicaliser that strips the control first
        var hStripped = Canonical.Hash(eRlm, eRlm.Lines, Canonical.BidiPolicy.Strip);
        var hCleanStripped = Canonical.Hash(eClean, eClean.Lines, Canonical.BidiPolicy.Strip);
        var stripFixes = hStripped.AsSpan().SequenceEqual(hCleanStripped);

        rec.Check("E4", "an invisible U+200F inside the Arabic memo CHANGES the hash",
            looksIdentical && hashChanged && stripFixes && Canonical.ContainsBidiControl(withRlm),
            $"rendered text looks the same on screen; the strings differ by one code point\n" +
            $"  clean       : {clean.Length} chars, hash {Canonical.Hex(hClean)[..24]}...\n" +
            $"  with U+200F : {withRlm.Length} chars, hash {Canonical.Hex(hRlm)[..24]}...\n" +
            $"  same hash?  : {!hashChanged}\n" +
            $"  stripping bidi controls before hashing reconciles them: {stripFixes}\n" +
            "RECOMMENDATION: do NOT strip during hashing (that would let anyone add or remove\n" +
            "invisible marks without breaking the chain). REJECT bidi controls at input\n" +
            "validation and NFC-normalise on the way in, so stored text never contains them.\n" +
            "التوصية: رفض محارف التحكم الاتجاهية عند الإدخال بدل إزالتها عند حساب البصمة.");
    }

    // -----------------------------------------------------------------------
    // E5/E6 : the chain, and a tamper performed by the table OWNER
    // -----------------------------------------------------------------------
    private static async Task ProveChainAndTamperAsync(ProofRecorder rec)
    {
        const string book = "TAMPER";
        await Sql.ExecAsync(Config.Admin,
            $"delete from ledger.journal_line where entry_id in " +
            $"(select entry_id from ledger.journal_entry where book_id = '{book}'); " +
            $"delete from ledger.journal_entry where book_id = '{book}'; " +
            $"update ledger.entry_counter set next_no = 1, next_seq = 1 where book_id = '{book}'");

        await using (var ctx = Contexts.Create())
        {
            for (var i = 1; i <= 6; i++)
            {
                var amount = 100.0000m * i;
                await Ledger.PostAsync(ctx, book, "acme", new DateOnly(2026, 6, i),
                    $"entry {i}", $"قيد رقم {i} - فرع الرياض", "muhasib@acme.sa",
                    [
                        new LineSpec(1, "1010", "النقدية", amount, 0m),
                        new LineSpec(2, "1210", "الذمم المدينة", 50.0000m, 0m),
                        new LineSpec(3, "4010", "المبيعات", 0m, amount + 50.0000m)
                    ]);
            }
        }

        var intact = await Ledger.VerifyAsync(Config.App, book);
        rec.Check("E5", "SHA-256 chain over canonical bytes verifies end to end", intact.Ok,
            $"{intact.Checked} entries verified from the genesis link: {intact.Reason}\n" +
            $"canonical bytes of chain_seq 1 (chain_seq and prev_hash are INSIDE them):\n" +
            await SampleCanonicalAsync(book));

        // --- the tamper. Performed as the OWNER (postgres), i.e. a customer
        //     with real database access, and crafted to keep debits = credits
        //     so a balance check would never notice.
        var target = await Sql.ScalarAsync<Guid>(Config.Admin,
            $"select entry_id from ledger.journal_entry where book_id = '{book}' and chain_seq = 3");
        await Sql.ExecAsync(Config.Admin, $"""
            update ledger.journal_line set debit = debit + 40.0000
             where entry_id = '{target}' and line_no = 1;
            update ledger.journal_line set debit = debit - 40.0000
             where entry_id = '{target}' and line_no = 2;
            """);
        var stillBalanced = await Sql.ScalarAsync<bool>(Config.Admin, $"""
            select coalesce(sum(debit),0) = coalesce(sum(credit),0)
            from ledger.journal_line where entry_id = '{target}'
            """);

        var detected = await Ledger.VerifyAsync(Config.App, book);
        rec.Check("E6", "a direct SQL tamper by the table OWNER is DETECTED, naming the first bad chain_seq",
            !detected.Ok && detected.FirstDivergentSeq == 3 && stillBalanced,
            $"tamper: 40.0000 moved between two debit lines of chain_seq 3, as user 'postgres' (the OWNER)\n" +
            $"the entry still balances, so a debit=credit check sees nothing wrong: {stillBalanced}\n" +
            $"the INSERT-only balance trigger never fires on an UPDATE, so it sees nothing either\n" +
            $"verification pass says -> first divergent chain_seq = {detected.FirstDivergentSeq}\n" +
            $"  {detected.Reason}");

        // --- the smarter tamperer: also recompute and rewrite that entry's hash
        var repaired = await RecomputeAndRewriteAsync(book, 3);
        var stillDetected = await Ledger.VerifyAsync(Config.App, book);
        rec.Check("E7", "even after the tamperer rewrites entry_hash, the CHAIN still breaks",
            !stillDetected.Ok && stillDetected.FirstDivergentSeq == 4,
            $"entry 3's entry_hash rewritten to the correct hash of its tampered content ({repaired[..16]}...)\n" +
            $"chain_seq 3 now self-verifies, but chain_seq 4 stored prev_hash still points at the OLD hash\n" +
            $"verification pass says -> first divergent chain_seq = {stillDetected.FirstDivergentSeq}\n" +
            $"  {stillDetected.Reason}\n" +
            "To hide the change the tamperer must rewrite EVERY later entry, which is exactly what\n" +
            "publishing the head hash (daily, to the customer and to an external witness) prevents.");

        var mainStillGood = await Ledger.VerifyAsync(Config.App, "MAIN");
        rec.Evidence($"control: the untouched MAIN book still verifies -> {mainStillGood.Ok} " +
                     $"({mainStillGood.Checked} entries)");
    }

    private static async Task<string> SampleCanonicalAsync(string book)
    {
        await using var conn = await Sql.OpenAsync(Config.Admin);
        await using var cmd = new NpgsqlCommand("""
            select e.entry_id, e.book_id, e.tenant_id, e.entry_no, e.chain_seq, e.entry_date,
                   e.memo, e.memo_ar, e.posted_at, e.actor, e.prev_hash
            from ledger.journal_entry e where e.book_id = @b and e.chain_seq = 1
            """, conn);
        cmd.Parameters.AddWithValue("b", book);
        await using var r = await cmd.ExecuteReaderAsync();
        if (!await r.ReadAsync()) return "(none)";
        var e = new JournalEntry
        {
            EntryId = r.GetGuid(0), BookId = r.GetString(1), TenantId = r.GetString(2),
            EntryNo = r.GetInt64(3), ChainSeq = r.GetInt64(4), EntryDate = r.GetFieldValue<DateOnly>(5),
            Memo = r.GetString(6), MemoAr = r.GetString(7), PostedAt = r.GetFieldValue<DateTime>(8),
            Actor = r.GetString(9), PrevHash = (byte[])r["prev_hash"]
        };
        await r.CloseAsync();
        await using var lines = new NpgsqlCommand(
            "select line_no, account_code, description, debit, credit from ledger.journal_line " +
            "where entry_id = @e order by line_no", conn);
        lines.Parameters.AddWithValue("e", e.EntryId);
        await using var lr = await lines.ExecuteReaderAsync();
        var list = new List<JournalLine>();
        while (await lr.ReadAsync())
            list.Add(new JournalLine
            {
                LineNo = lr.GetInt32(0), AccountCode = lr.GetString(1), Description = lr.GetString(2),
                Debit = lr.GetDecimal(3), Credit = lr.GetDecimal(4)
            });
        var text = Canonical.Render(e, list);
        return string.Join("\n", text.TrimEnd('\n').Split('\n').Select(l => "  " + l));
    }

    private static async Task<string> RecomputeAndRewriteAsync(string book, long chainSeq)
    {
        await using var conn = await Sql.OpenAsync(Config.Admin);
        await using var cmd = new NpgsqlCommand("""
            select entry_id, book_id, tenant_id, entry_no, chain_seq, entry_date, memo, memo_ar,
                   posted_at, actor, prev_hash
            from ledger.journal_entry where book_id = @b and chain_seq = @s
            """, conn);
        cmd.Parameters.AddWithValue("b", book);
        cmd.Parameters.AddWithValue("s", chainSeq);
        JournalEntry e;
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            await r.ReadAsync();
            e = new JournalEntry
            {
                EntryId = r.GetGuid(0), BookId = r.GetString(1), TenantId = r.GetString(2),
                EntryNo = r.GetInt64(3), ChainSeq = r.GetInt64(4), EntryDate = r.GetFieldValue<DateOnly>(5),
                Memo = r.GetString(6), MemoAr = r.GetString(7), PostedAt = r.GetFieldValue<DateTime>(8),
                Actor = r.GetString(9), PrevHash = (byte[])r["prev_hash"]
            };
        }
        await using var lines = new NpgsqlCommand(
            "select line_no, account_code, description, debit, credit from ledger.journal_line " +
            "where entry_id = @e order by line_no", conn);
        lines.Parameters.AddWithValue("e", e.EntryId);
        var list = new List<JournalLine>();
        await using (var lr = await lines.ExecuteReaderAsync())
            while (await lr.ReadAsync())
                list.Add(new JournalLine
                {
                    LineNo = lr.GetInt32(0), AccountCode = lr.GetString(1), Description = lr.GetString(2),
                    Debit = lr.GetDecimal(3), Credit = lr.GetDecimal(4)
                });

        var hash = Canonical.Hash(e, list);
        await using var upd = new NpgsqlCommand(
            "update ledger.journal_entry set entry_hash = @h where entry_id = @e", conn);
        upd.Parameters.AddWithValue("h", hash);
        upd.Parameters.AddWithValue("e", e.EntryId);
        await upd.ExecuteNonQueryAsync();
        return Canonical.Hex(hash);
    }
}
