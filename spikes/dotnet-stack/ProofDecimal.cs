using System.Diagnostics;
using System.Globalization;
using Marten;
using Npgsql;

namespace BabelSpike;

/// <summary>
/// Proof (a): does a C# decimal survive a round trip through a Marten JSONB
/// document body, and what does it cost to aggregate money out of JSONB?
/// </summary>
public static class ProofDecimal
{
    public const string Schema = "babel_spike";

    private static readonly (string Label, decimal Value)[] Cases =
    [
        ("large_4dp",       1234567890.1234m),
        ("tiny",            0.0001m),
        ("very_large_4dp",  99999999999999.9999m),
        ("trailing_zeros",  100.00m),
        ("negative",        -9876543210.9876m),
    ];

    private static string Inv(decimal d) => d.ToString(CultureInfo.InvariantCulture);

    public static async Task RunAsync(IDocumentStore store, ProofRecorder rec, string conn)
    {
        rec.Note($"Marten serializer in use: {store.Options.Serializer().GetType().FullName}");

        // ---- 1. round trip every case through a document -------------------
        var ids = new Dictionary<string, Guid>();
        await using (var session = store.LightweightSession())
        {
            foreach (var (label, value) in Cases)
            {
                var doc = new MoneyDoc { Label = label, Amount = value };
                ids[label] = doc.Id;
                session.Store(doc);
            }
            await session.SaveChangesAsync();
        }

        var failures = new List<string>();
        var evidence = new List<string>();

        foreach (var (label, expected) in Cases)
        {
            // brand new session => no identity map, forces a real deserialisation
            await using var read = store.QuerySession();
            var doc = await read.LoadAsync<MoneyDoc>(ids[label]);

            if (doc is null)
            {
                failures.Add($"{label}: document did not load");
                continue;
            }

            var actual = doc.Amount;
            var valueEqual = actual == expected;
            // decimal '==' ignores scale (100.00m == 100.0m). Compare the raw bits
            // and the invariant string too, so a lost scale is caught.
            var bitsEqual = decimal.GetBits(actual).SequenceEqual(decimal.GetBits(expected));
            var textEqual = Inv(actual) == Inv(expected);

            if (!valueEqual) failures.Add($"{label}: value mismatch expected={Inv(expected)} actual={Inv(actual)}");
            if (!bitsEqual || !textEqual)
                evidence.Add($"{label}: value equal but SCALE changed expected='{Inv(expected)}' actual='{Inv(actual)}'");
        }

        // ---- 2. what does Postgres actually hold? --------------------------
        await using var db = new NpgsqlConnection(conn);
        await db.OpenAsync();

        foreach (var (label, expected) in Cases)
        {
            await using var cmd = new NpgsqlCommand(
                $"select jsonb_typeof(data->'Amount'), data->>'Amount', data::text from {Schema}.mt_doc_moneydoc where id = @id", db);
            cmd.Parameters.AddWithValue("id", ids[label]);
            await using var r = await cmd.ExecuteReaderAsync();
            if (!await r.ReadAsync()) { failures.Add($"{label}: no row in Postgres"); continue; }

            var jsonType = r.GetString(0);
            var jsonText = r.GetString(1);
            var body = r.GetString(2);

            evidence.Add($"{label,-15} jsonb_typeof={jsonType,-8} data->>'Amount'='{jsonText}'");
            if (label == "large_4dp") evidence.Add($"                raw jsonb body = {body}");

            if (jsonType != "number")
                failures.Add($"{label}: stored as jsonb '{jsonType}', not 'number'");
            // The text Postgres reports must still parse back to the exact decimal.
            if (!decimal.TryParse(jsonText, NumberStyles.Float, CultureInfo.InvariantCulture, out var fromPg) || fromPg != expected)
                failures.Add($"{label}: jsonb text '{jsonText}' does not parse back to {Inv(expected)}");
        }

        // ---- 3. SQL aggregation straight out of JSONB ----------------------
        var expectedSum = Cases.Sum(c => c.Value);
        await using (var cmd = new NpgsqlCommand(
            $"select sum((data->>'Amount')::numeric) from {Schema}.mt_doc_moneydoc", db))
        {
            var sum = (decimal)(await cmd.ExecuteScalarAsync())!;
            evidence.Add($"sum((data->>'Amount')::numeric) = {Inv(sum)}  (C# expected {Inv(expectedSum)})");
            if (sum != expectedSum)
                failures.Add($"JSONB SQL sum is not exact: got {Inv(sum)} expected {Inv(expectedSum)}");
        }

        rec.Record("(a)", "DECIMAL PRECISION through Marten JSONB",
            failures.Count == 0,
            string.Join("\n", evidence.Concat(failures.Select(f => "!! " + f))));

        await RunAggregationBenchmarkAsync(store, rec, db);
    }

    /// <summary>
    /// Aggregating money four ways over the same rows: out of the JSONB body,
    /// out of Marten duplicated columns, and out of a plain normalised table -
    /// unfiltered (full scan) and filtered by account (index territory).
    /// </summary>
    private static async Task RunAggregationBenchmarkAsync(IDocumentStore store, ProofRecorder rec, NpgsqlConnection db)
    {
        const int rows = 1_000_000;
        const string probeAccount = "4123";

        var rng = new Random(20260823);
        var lines = new List<LedgerLine>(rows);
        for (var i = 0; i < rows; i++)
        {
            // 4-decimal-place monetary amounts, the shape a real GL line has
            var amount = Math.Round((decimal)(rng.NextDouble() * 100_000d), 4);
            lines.Add(new LedgerLine { Account = $"4{i % 900:000}", Amount = amount });
        }
        var expectedTotal = lines.Sum(l => l.Amount);
        var expectedAccount = lines.Where(l => l.Account == probeAccount).Sum(l => l.Amount);

        var swLoad = Stopwatch.StartNew();
        await store.BulkInsertAsync(lines, batchSize: 25_000);
        swLoad.Stop();

        await using (var cmd = new NpgsqlCommand($"""
            drop table if exists {Schema}.gl_lines_normalised;
            create table {Schema}.gl_lines_normalised (
                id uuid primary key, account text not null, amount numeric(19,4) not null);
            insert into {Schema}.gl_lines_normalised (id, account, amount)
            select id, data->>'Account', (data->>'Amount')::numeric(19,4) from {Schema}.mt_doc_ledgerline;
            create index gl_norm_account_idx on {Schema}.gl_lines_normalised (account) include (amount);
            create index mt_ledgerline_account_idx on {Schema}.mt_doc_ledgerline (account) include (amount);
            create index mt_ledgerline_gin on {Schema}.mt_doc_ledgerline using gin (data jsonb_path_ops);
            analyze {Schema}.gl_lines_normalised;
            analyze {Schema}.mt_doc_ledgerline;
            """, db))
        {
            cmd.CommandTimeout = 900;
            await cmd.ExecuteNonQueryAsync();
        }

        async Task SetParallel(bool on)
        {
            await using var c = new NpgsqlCommand(
                $"set max_parallel_workers_per_gather = {(on ? 4 : 0)}", db);
            await c.ExecuteNonQueryAsync();
        }

        async Task<(decimal Value, double Ms)> Time(string sql)
        {
            decimal value = 0;
            var best = double.MaxValue;
            for (var i = 0; i < 5; i++)
            {
                await using var cmd = new NpgsqlCommand(sql, db) { CommandTimeout = 900 };
                var sw = Stopwatch.StartNew();
                value = (decimal)(await cmd.ExecuteScalarAsync())!;
                sw.Stop();
                best = Math.Min(best, sw.Elapsed.TotalMilliseconds);
            }
            return (value, best);
        }

        async Task<string> Plan(string sql)
        {
            await using var cmd = new NpgsqlCommand("explain (analyze, buffers, costs off) " + sql, db) { CommandTimeout = 900 };
            await using var r = await cmd.ExecuteReaderAsync();
            var all = new List<string>();
            while (await r.ReadAsync()) all.Add(r.GetString(0).Trim());
            return string.Join(" / ", all.Take(3));
        }

        var jsonbTextSql  = $"select sum((data->>'Amount')::numeric) from {Schema}.mt_doc_ledgerline";
        var jsonbCastSql  = $"select sum((data->'Amount')::numeric) from {Schema}.mt_doc_ledgerline";
        var dupSql        = $"select sum(amount) from {Schema}.mt_doc_ledgerline";
        var normSql       = $"select sum(amount) from {Schema}.gl_lines_normalised";
        var jsonbFiltSql  = $"select sum((data->>'Amount')::numeric) from {Schema}.mt_doc_ledgerline where data->>'Account' = '{probeAccount}'";
        var dupFiltSql    = $"select sum(amount) from {Schema}.mt_doc_ledgerline where account = '{probeAccount}'";
        var normFiltSql   = $"select sum(amount) from {Schema}.gl_lines_normalised where account = '{probeAccount}'";

        // --- single-threaded: isolates the raw CPU cost of parsing JSONB ---
        await SetParallel(false);
        var sJsonbText = await Time(jsonbTextSql);
        var sJsonbCast = await Time(jsonbCastSql);
        var sDup       = await Time(dupSql);
        var sNorm      = await Time(normSql);

        // --- with parallel workers, the way production would run ------------
        await SetParallel(true);
        var pJsonbText = await Time(jsonbTextSql);
        var pDup       = await Time(dupSql);
        var pNorm      = await Time(normSql);

        // --- filtered by account (the common GL query) ----------------------
        var fJsonb = await Time(jsonbFiltSql);
        var fDup   = await Time(dupFiltSql);
        var fNorm  = await Time(normFiltSql);

        var planJsonb     = await Plan(jsonbTextSql);
        var planNorm      = await Plan(normSql);
        var planJsonbFilt = await Plan(jsonbFiltSql);
        var planNormFilt  = await Plan(normFiltSql);

        string sizes;
        await using (var cmd = new NpgsqlCommand($"""
            select pg_size_pretty(pg_total_relation_size('{Schema}.mt_doc_ledgerline')),
                   pg_size_pretty(pg_relation_size('{Schema}.mt_doc_ledgerline')),
                   pg_size_pretty(pg_total_relation_size('{Schema}.gl_lines_normalised')),
                   pg_size_pretty(pg_relation_size('{Schema}.gl_lines_normalised'))
            """, db))
        await using (var r = await cmd.ExecuteReaderAsync())
        {
            await r.ReadAsync();
            sizes = $"marten doc table {r.GetString(1)} heap / {r.GetString(0)} with indexes;  " +
                    $"normalised {r.GetString(3)} heap / {r.GetString(2)} with indexes";
        }

        var allExact =
            sJsonbText.Value == expectedTotal && sJsonbCast.Value == expectedTotal &&
            sDup.Value == expectedTotal && sNorm.Value == expectedTotal &&
            pJsonbText.Value == expectedTotal && pDup.Value == expectedTotal && pNorm.Value == expectedTotal &&
            fJsonb.Value == expectedAccount && fDup.Value == expectedAccount && fNorm.Value == expectedAccount;

        var failures = new List<string>();
        if (!allExact)
        {
            failures.Add($"a sum did not match the C# control total {Inv(expectedTotal)} / account {Inv(expectedAccount)}");
            failures.Add($"  jsonb={Inv(sJsonbText.Value)} dup={Inv(sDup.Value)} norm={Inv(sNorm.Value)}");
            failures.Add($"  filtered jsonb={Inv(fJsonb.Value)} dup={Inv(fDup.Value)} norm={Inv(fNorm.Value)}");
        }

        var detail = string.Join("\n",
        new[]
        {
            $"{rows:N0} GL lines bulk-inserted in {swLoad.Elapsed.TotalMilliseconds:N0} ms",
            $"C# decimal control total = {Inv(expectedTotal)}   (account {probeAccount} = {Inv(expectedAccount)})",
            "",
            "FULL-TABLE SUM, single-threaded (max_parallel_workers_per_gather=0):",
            $"  sum((data->>'Amount')::numeric)  {sJsonbText.Ms,9:N1} ms   {(sJsonbText.Value == expectedTotal ? "exact" : "WRONG")}",
            $"  sum((data->'Amount')::numeric)   {sJsonbCast.Ms,9:N1} ms   {(sJsonbCast.Value == expectedTotal ? "exact" : "WRONG")}",
            $"  sum(amount) duplicated column    {sDup.Ms,9:N1} ms   {(sDup.Value == expectedTotal ? "exact" : "WRONG")}",
            $"  sum(amount) normalised numeric   {sNorm.Ms,9:N1} ms   {(sNorm.Value == expectedTotal ? "exact" : "WRONG")}",
            $"  -> JSONB extraction costs {sJsonbText.Ms / Math.Max(sNorm.Ms, 0.001):N1}x the normalised NUMERIC scan",
            "",
            "FULL-TABLE SUM, parallel workers allowed:",
            $"  jsonb text-extract               {pJsonbText.Ms,9:N1} ms",
            $"  duplicated column                {pDup.Ms,9:N1} ms",
            $"  normalised numeric               {pNorm.Ms,9:N1} ms",
            "",
            $"FILTERED SUM (account = '{probeAccount}', ~{rows / 900:N0} rows):",
            $"  jsonb  where data->>'Account'    {fJsonb.Ms,9:N1} ms",
            $"  marten duplicated+indexed        {fDup.Ms,9:N1} ms",
            $"  normalised indexed               {fNorm.Ms,9:N1} ms",
            $"  -> unindexed JSONB filter is {fJsonb.Ms / Math.Max(fNorm.Ms, 0.001):N0}x slower than an indexed NUMERIC column",
            "",
            "PLANS:",
            $"  jsonb  full : {planJsonb}",
            $"  norm   full : {planNorm}",
            $"  jsonb  filt : {planJsonbFilt}",
            $"  norm   filt : {planNormFilt}",
            "",
            $"STORAGE: {sizes}"
        }.Concat(failures.Select(f => "!! " + f)));

        rec.Record("(a2)", "Monetary aggregation is exact from JSONB and from NUMERIC", allExact, detail);
    }
}
