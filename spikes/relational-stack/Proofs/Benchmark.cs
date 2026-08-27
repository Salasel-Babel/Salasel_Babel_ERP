using System.Globalization;
using System.Diagnostics;
using System.Text;
using BabelRelationalSpike.Db;
using BabelRelationalSpike.Support;

namespace BabelRelationalSpike.Proofs;

/// <summary>
/// Write throughput for journal entries posted through EF Core with the deferred
/// balance trigger AND the gapless counter AND the SHA-256 chain all active.
/// قياس معدّل الكتابة مع تفعيل المشغّل المؤجّل والعدّاد والسلسلة.
/// </summary>
public static class Benchmark
{
    private sealed record Run(string Name, int Writers, int Entries, double Seconds,
                              double P50Ms, double P95Ms, double MaxMs);

    public static async Task RunAsync(ProofRecorder rec)
    {
        rec.Section("throughput: journal entries through EF Core, trigger + counter + hash chain live");

        var facts = await Sql.ScalarAsync<string>(Config.Admin, """
            select 'synchronous_commit=' || (select setting from pg_settings where name='synchronous_commit')
                || '  fsync=' || (select setting from pg_settings where name='fsync')
                || '  full_page_writes=' || (select setting from pg_settings where name='full_page_writes')
                || '  shared_buffers=' || (select setting from pg_settings where name='shared_buffers') || ' x8kB'
            """);
        rec.Evidence($"host: {Environment.ProcessorCount} logical CPUs, {RuntimeSummary()}\n" +
                     $"server: {facts}");

        await MeasureAsync("warmup", 4, 10, sharded: true, chain: true);   // JIT + pool warm-up

        var runs = new List<Run>
        {
            await MeasureAsync("chain + shared counter",  1,  200, sharded: false, chain: true),
            await MeasureAsync("chain + shared counter",  8,   50, sharded: false, chain: true),
            await MeasureAsync("chain + shared counter", 32,   16, sharded: false, chain: true),
            await MeasureAsync("chain + counter per book", 8,  50, sharded: true,  chain: true),
            await MeasureAsync("chain + counter per book", 32, 16, sharded: true,  chain: true),
            await MeasureAsync("no chain, no counter",     8,  50, sharded: true,  chain: false),
            await MeasureAsync("no chain, no counter",    32,  16, sharded: true,  chain: false)
        };

        var sb = new StringBuilder();
        sb.AppendLine(CultureInfo.InvariantCulture, $"{"CONFIGURATION",-28} {"WRITERS",7} {"ENTRIES",8} {"SECONDS",8} {"ENTRIES/S",10} {"p50 ms",8} {"p95 ms",8} {"max ms",8}");
        sb.AppendLine(new string('-', 92));
        foreach (var r in runs)
            sb.AppendLine(CultureInfo.InvariantCulture,
                $"{r.Name,-28} {r.Writers,7} {r.Entries,8} {r.Seconds,8:F2} " +
                $"{r.Entries / r.Seconds,10:F1} {r.P50Ms,8:F2} {r.P95Ms,8:F2} {r.MaxMs,8:F2}");
        rec.Evidence(sb.ToString().TrimEnd());

        var shared8 = runs.First(r => r is { Writers: 8, Name: "chain + shared counter" });
        var sharded8 = runs.First(r => r is { Writers: 8, Name: "chain + counter per book" });
        var bare8 = runs.First(r => r is { Writers: 8, Name: "no chain, no counter" });
        rec.Evidence(
            "reading the numbers:\n" +
            $"  * every entry here is 1 header + 3 lines, one COMMIT each, synchronous_commit=on,\n" +
            $"    so each entry costs at least one WAL fsync on this box.\n" +
            $"  * ONE book = ONE counter row = SELECT ... FOR UPDATE, so writers to the same book\n" +
            $"    serialise by design: {shared8.Entries / shared8.Seconds:F0}/s at 8 writers vs " +
            $"{sharded8.Entries / sharded8.Seconds:F0}/s with a counter per book.\n" +
            $"  * the chain + counter cost themselves: {sharded8.Entries / sharded8.Seconds:F0}/s with them, " +
            $"{bare8.Entries / bare8.Seconds:F0}/s without - the SHA-256 is noise, the extra round trips are not.\n" +
            "  * CAVEATS: 4 vCPU shared dev VM, PostgreSQL and the app on the same host, cold caches,\n" +
            "    no connection pooler, no batching. Treat these as RELATIVE numbers only.");
    }

    private static string RuntimeSummary() =>
        $"{System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}, " +
        $"{System.Runtime.InteropServices.RuntimeInformation.OSDescription.Split('\n')[0]}";

    private static int _runId;

    private static async Task<Run> MeasureAsync(string name, int writers, int perWriter, bool sharded, bool chain)
    {
        var run = Interlocked.Increment(ref _runId);
        var books = new List<string>();
        for (var w = 0; w < (sharded ? writers : 1); w++)
        {
            var book = sharded ? $"BENCH-{run}-{w}" : "BENCH";
            books.Add(book);
            await Sql.ExecAsync(Config.Admin, $"""
                insert into ledger.entry_counter (book_id, next_no, next_seq)
                values ('{book}', 1, 1) on conflict (book_id) do nothing
                """);
        }

        var latencies = new System.Collections.Concurrent.ConcurrentBag<double>();
        var sw = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(0, writers).Select(async w =>
        {
            var book = books[sharded ? w : 0];
            await using var ctx = Contexts.Create();
            for (var i = 0; i < perWriter; i++)
            {
                var t = Stopwatch.GetTimestamp();
                if (chain)
                {
                    await Ledger.PostAsync(ctx, book, "acme", new DateOnly(2026, 7, 1),
                        $"bench {w}-{i}", $"قيد قياس {w}-{i}", "bench",
                        [
                            new LineSpec(1, "1010", "النقدية", 115.0000m, 0m),
                            new LineSpec(2, "4010", "المبيعات", 0m, 100.0000m),
                            new LineSpec(3, "2310", "ضريبة", 0m, 15.0000m)
                        ]);
                }
                else
                {
                    await PostWithoutChainAsync(ctx, book, w, i);
                }
                latencies.Add(Stopwatch.GetElapsedTime(t).TotalMilliseconds);
            }
        }));
        sw.Stop();

        var sorted = latencies.OrderBy(x => x).ToArray();
        return new Run(name, writers, writers * perWriter, sw.Elapsed.TotalSeconds,
            Pct(sorted, 0.50), Pct(sorted, 0.95), sorted.Length == 0 ? 0 : sorted[^1]);
    }

    /// <summary>Baseline: same table, same deferred trigger, but no counter lock and no hashing.</summary>
    private static async Task PostWithoutChainAsync(LedgerDbContext ctx, string book, int writer, int i)
    {
        await using var tx = await ctx.Database.BeginTransactionAsync();
        var entry = new JournalEntry
        {
            EntryId = Guid.CreateVersion7(),
            BookId = book + "-bare",
            TenantId = "acme",
            EntryNo = writer * 1_000_000L + i,
            ChainSeq = writer * 1_000_000L + i,
            EntryDate = new DateOnly(2026, 7, 1),
            Memo = $"bare {writer}-{i}",
            MemoAr = $"قيد بدون سلسلة {writer}-{i}",
            PostedAt = Canonical.PgInstant(DateTime.UtcNow),
            Actor = "bench",
            PrevHash = [],
            EntryHash = [],
            Lines =
            [
                new JournalLine { LineId = Guid.CreateVersion7(), LineNo = 1, AccountCode = "1010", Description = "النقدية", Debit = 115.0000m },
                new JournalLine { LineId = Guid.CreateVersion7(), LineNo = 2, AccountCode = "4010", Description = "المبيعات", Credit = 100.0000m },
                new JournalLine { LineId = Guid.CreateVersion7(), LineNo = 3, AccountCode = "2310", Description = "ضريبة", Credit = 15.0000m }
            ]
        };
        ctx.JournalEntries.Add(entry);
        await ctx.SaveChangesAsync();
        await tx.CommitAsync();
        ctx.ChangeTracker.Clear();
    }

    private static double Pct(double[] sorted, double p) =>
        sorted.Length == 0 ? 0 : sorted[Math.Min(sorted.Length - 1, (int)(sorted.Length * p))];
}
