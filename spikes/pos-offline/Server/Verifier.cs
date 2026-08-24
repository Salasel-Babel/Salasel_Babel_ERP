using System.Globalization;
using System.Text;
using BabelPosOffline.Support;
using Npgsql;

namespace BabelPosOffline.Server;

public sealed record BookChainResult(bool Ok, long? FirstBadSeq, string Reason, int Checked);
public sealed record TrialBalance(decimal Debit, decimal Credit, int Lines, int Entries)
{
    public bool Balanced => Debit == Credit;
    public override string ToString() =>
        $"debit={Debit.ToString("0.0000", CultureInfo.InvariantCulture)} " +
        $"credit={Credit.ToString("0.0000", CultureInfo.InvariantCulture)} " +
        $"diff={(Debit - Credit).ToString("0.0000", CultureInfo.InvariantCulture)} entries={Entries} lines={Lines}";
}
public sealed record GapAudit(bool Ok, string Reason, List<(long From, long To)> Unexplained, int AssertedGaps, long Used);

/// <summary>
/// تحقّق مستقل على الخادم. مكتوب بـC# عمداً بينما البصمة تُحسب داخل PL/pgSQL:
/// تطبيقان مختلفان للشكل القانوني نفسه، فأي انحراف بينهما يظهر بدل أن يُخفيه اشتراك الشيفرة.
/// </summary>
public static class Verifier
{
    /// <summary>يعيد بناء سلسلة الدفتر من رابط النشأة ويُسمّي أول تسلسل منحرف.</summary>
    public static async Task<BookChainResult> VerifyBookAsync(string cs, string book)
    {
        await using var conn = await Sql.OpenAsync(cs);
        var entries = new List<(Guid Id, string Book, string Tenant, long No, long Seq, DateOnly Date,
                               string Source, byte[] Prev, byte[] Hash)>();
        await using (var cmd = new NpgsqlCommand("""
            select entry_id, book_id, tenant_id, entry_no, chain_seq, entry_date,
                   coalesce(source_idem_key,''), prev_hash, entry_hash
            from ledger.journal_entry where book_id = @b order by chain_seq
            """, conn))
        {
            cmd.Parameters.AddWithValue("b", book);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
                entries.Add((r.GetGuid(0), r.GetString(1), r.GetString(2), r.GetInt64(3), r.GetInt64(4),
                             r.GetFieldValue<DateOnly>(5), r.GetString(6), (byte[])r[7], (byte[])r[8]));
        }

        var lines = new Dictionary<Guid, List<(int No, string Acc, decimal D, decimal C)>>();
        await using (var cmd = new NpgsqlCommand("""
            select l.entry_id, l.line_no, l.account_code, l.debit, l.credit
            from ledger.journal_line l join ledger.journal_entry e on e.entry_id = l.entry_id
            where e.book_id = @b order by l.entry_id, l.line_no
            """, conn))
        {
            cmd.Parameters.AddWithValue("b", book);
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var id = r.GetGuid(0);
                if (!lines.TryGetValue(id, out var l)) lines[id] = l = [];
                l.Add((r.GetInt32(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4)));
            }
        }

        var expectedPrev = Canonical.HashOf($"babel.genesis.v1|{book}");
        long expectedSeq = 1;
        foreach (var e in entries)
        {
            if (e.Seq != expectedSeq)
                return new BookChainResult(false, e.Seq, $"gap/reorder: expected chain_seq {expectedSeq}, found {e.Seq}", entries.Count);
            if (!e.Prev.AsSpan().SequenceEqual(expectedPrev))
                return new BookChainResult(false, e.Seq, $"broken link at chain_seq {e.Seq}", entries.Count);

            var sb = new StringBuilder();
            sb.Append("babel.journal.v1\n");
            sb.Append("chain_seq=").Append(e.Seq).Append('\n');
            sb.Append("prev_hash=").Append(Canonical.Hex(e.Prev)).Append('\n');
            sb.Append("book_id=").Append(e.Book).Append('\n');
            sb.Append("tenant_id=").Append(e.Tenant).Append('\n');
            sb.Append("entry_id=").Append(e.Id.ToString("D")).Append('\n');
            sb.Append("entry_no=").Append(e.No).Append('\n');
            sb.Append("entry_date=").Append(Canonical.Date(e.Date)).Append('\n');
            sb.Append("source=").Append(e.Source).Append('\n');
            var el = lines.TryGetValue(e.Id, out var ll) ? ll : [];
            foreach (var l in el.OrderBy(x => x.No))
                sb.Append("line=").Append(l.No).Append('|').Append(l.Acc).Append('|')
                  .Append(Money.Canonical(l.D)).Append('|').Append(Money.Canonical(l.C)).Append('\n');
            sb.Append("end\n");

            var recomputed = Canonical.HashOf(sb.ToString());
            if (!recomputed.AsSpan().SequenceEqual(e.Hash))
                return new BookChainResult(false, e.Seq,
                    $"content tampered at chain_seq {e.Seq} (entry_no {e.No}): recomputed {Canonical.Hex(recomputed)[..16]}… != stored {Canonical.Hex(e.Hash)[..16]}…",
                    entries.Count);

            expectedPrev = e.Hash; expectedSeq++;
        }
        return new BookChainResult(true, null, $"book '{book}' chain intact over {entries.Count} entries", entries.Count);
    }

    public static async Task<TrialBalance> TrialBalanceAsync(string cs, string? tenant = null)
    {
        await using var conn = await Sql.OpenAsync(cs);
        await using var cmd = new NpgsqlCommand($"""
            select coalesce(sum(l.debit),0), coalesce(sum(l.credit),0), count(*), count(distinct l.entry_id)
            from ledger.journal_line l join ledger.journal_entry e on e.entry_id = l.entry_id
            {(tenant is null ? "" : "where e.tenant_id = @t")}
            """, conn);
        if (tenant is not null) cmd.Parameters.AddWithValue("t", tenant);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return new TrialBalance(r.GetDecimal(0), r.GetDecimal(1), r.GetInt32(2), r.GetInt32(3));
    }

    public static async Task<decimal> AccountBalanceAsync(string cs, string account)
    {
        await using var conn = await Sql.OpenAsync(cs);
        await using var cmd = new NpgsqlCommand(
            "select coalesce(sum(debit) - sum(credit),0) from ledger.journal_line where account_code = @a", conn);
        cmd.Parameters.AddWithValue("a", account);
        return (decimal)(await cmd.ExecuteScalarAsync())!;
    }

    /// <summary>كم مرة رُحِّل مفتاح حصانة واحد؟ يجب أن يكون 1 بالضبط دائماً.</summary>
    public static async Task<(int MaxTimes, string Worst, int Distinct)> PostingMultiplicityAsync(string cs)
    {
        await using var conn = await Sql.OpenAsync(cs);
        await using var cmd = new NpgsqlCommand("""
            select coalesce(max(n),0), coalesce((select source_idem_key from (
                     select source_idem_key, count(*) n from ledger.journal_entry
                      where source_idem_key is not null and source_idem_key not like '%#cogs'
                      group by 1) z order by n desc limit 1), ''),
                   count(*)
            from (select source_idem_key, count(*) n from ledger.journal_entry
                   where source_idem_key is not null and source_idem_key not like '%#cogs'
                   group by 1) q
            """, conn);
        await using var r = await cmd.ExecuteReaderAsync();
        await r.ReadAsync();
        return (r.GetInt32(0), r.GetString(1), r.GetInt32(2));
    }

    /// <summary>
    /// تدقيق الترقيم لكل جهاز على حدة:
    ///  • لا تتداخل المديات (والمحرّك يمنع ذلك أصلاً بقيد استبعاد).
    ///  • كل رقم مفقود <b>تحت أعلى رقم أصدره الجهاز</b> يجب أن يغطّيه إثبات فجوة.
    ///  • وكل ذيل غير مستعمل في مدى <b>مُبطَل</b> يجب أن يغطّيه إثبات فجوة كذلك.
    ///
    /// الذيل غير المستعمل في مدى <b>نشط</b> ليس فجوة: تلك أرقام لم تُصدَر بعد.
    /// وهذا هو الفرق الذي لا يستطيع المُدقّق استنتاجه من غياب السجل وحده.
    /// </summary>
    public static async Task<GapAudit> AuditNumberingAsync(string cs, string tenant, string devicePrefix = "")
    {
        await using var conn = await Sql.OpenAsync(cs);

        var overlaps = await Sql.ScalarAsync<long>(conn, $"""
            select count(*) from pos.number_range a join pos.number_range b
              on a.tenant_id = b.tenant_id and a.range_id < b.range_id
             and int8range(a.range_start, a.range_end + 1) && int8range(b.range_start, b.range_end + 1)
            where a.tenant_id = '{tenant}'
            """);
        if (overlaps > 0) return new GapAudit(false, $"{overlaps} overlapping range pairs", [], 0, 0);

        var used = new Dictionary<string, SortedSet<long>>();
        await using (var cmd = new NpgsqlCommand(
            $"select device_id, invoice_no from pos.sale_inbox where tenant_id = @t and device_id like @p", conn))
        {
            cmd.Parameters.AddWithValue("t", tenant);
            cmd.Parameters.AddWithValue("p", devicePrefix + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var dev = r.GetString(0);
                if (!used.TryGetValue(dev, out var set)) used[dev] = set = [];
                set.Add(r.GetInt64(1));
            }
        }

        var asserted = new Dictionary<string, List<(long From, long To)>>();
        await using (var cmd = new NpgsqlCommand(
            $"select device_id, from_no, to_no from pos.number_gap_assertion where tenant_id = @t and device_id like @p", conn))
        {
            cmd.Parameters.AddWithValue("t", tenant);
            cmd.Parameters.AddWithValue("p", devicePrefix + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var dev = r.GetString(0);
                if (!asserted.TryGetValue(dev, out var l)) asserted[dev] = l = [];
                l.Add((r.GetInt64(1), r.GetInt64(2)));
            }
        }

        var voided = new List<(string Dev, long Start, long End)>();
        await using (var cmd = new NpgsqlCommand(
            $"select device_id, range_start, range_end from pos.number_range where tenant_id = @t and device_id like @p and state = 'voided'", conn))
        {
            cmd.Parameters.AddWithValue("t", tenant);
            cmd.Parameters.AddWithValue("p", devicePrefix + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) voided.Add((r.GetString(0), r.GetInt64(1), r.GetInt64(2)));
        }

        bool Covered(string dev, long n) =>
            asserted.TryGetValue(dev, out var l) && l.Any(a => n >= a.From && n <= a.To);

        var unexplained = new List<(long, long)>();
        long totalUsed = 0;
        var devices = used.Keys.Union(voided.Select(v => v.Dev)).Distinct().OrderBy(x => x);
        foreach (var dev in devices)
        {
            var set = used.TryGetValue(dev, out var s2) ? s2 : [];
            totalUsed += set.Count;

            // (أ) ثقوب تحت أعلى رقم أصدره الجهاز
            if (set.Count > 0)
            {
                long lo = set.Min, hi = set.Max;
                long? run = null;
                for (long n = lo; n <= hi; n++)
                {
                    var missing = !set.Contains(n) && !Covered(dev, n);
                    if (missing) run ??= n;
                    else if (run is not null) { unexplained.Add((run.Value, n - 1)); run = null; }
                }
                if (run is not null) unexplained.Add((run.Value, hi));
            }

            // (ب) ذيل كل مدى مُبطَل
            foreach (var v in voided.Where(v => v.Dev == dev))
            {
                long from = set.Count > 0 && set.Max >= v.Start ? set.Max + 1 : v.Start;
                long? run = null;
                for (long n = from; n <= v.End; n++)
                {
                    var missing = !set.Contains(n) && !Covered(dev, n);
                    if (missing) run ??= n;
                    else if (run is not null) { unexplained.Add((run.Value, n - 1)); run = null; }
                }
                if (run is not null) unexplained.Add((run.Value, v.End));
            }
        }

        var assertedCount = asserted.Values.Sum(l => l.Count);
        return new GapAudit(unexplained.Count == 0,
            unexplained.Count == 0
                ? $"{totalUsed} numbers issued across {used.Count} device(s); every hole and every voided tail is covered by a positive gap assertion ({assertedCount} assertion(s))"
                : $"{unexplained.Count} UNEXPLAINED gap run(s), first {unexplained[0].Item1}..{unexplained[0].Item2} ({assertedCount} assertion(s) present)",
            unexplained, assertedCount, totalUsed);
    }

    /// <summary>
    /// «فواتير صدرت ولم تصل»: تُقارَن أرقام الوارد بعلامة المياه العليا التي أبلغ عنها
    /// الجهاز في آخر اتصال. بدون هذه العلامة يكون هذا الفراغ <b>غير قابل للاكتشاف</b>
    /// إطلاقاً، لأنه يقع فوق أعلى رقم يعرفه الخادم فيبدو كأن الجهاز توقّف عن البيع.
    /// </summary>
    public static async Task<(bool Ok, string Reason, List<(string Dev, long From, long To)> Missing)>
        AuditIssuedButMissingAsync(string cs, string tenant, string devicePrefix = "")
    {
        await using var conn = await Sql.OpenAsync(cs);
        var hw = new Dictionary<string, long>();
        await using (var cmd = new NpgsqlCommand(
            "select device_id, last_reported_next_no from pos.device where tenant_id = @t and device_id like @p and last_reported_next_no is not null", conn))
        {
            cmd.Parameters.AddWithValue("t", tenant); cmd.Parameters.AddWithValue("p", devicePrefix + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) hw[r.GetString(0)] = r.GetInt64(1);
        }
        var got = new Dictionary<string, SortedSet<long>>();
        await using (var cmd = new NpgsqlCommand(
            "select device_id, invoice_no from pos.sale_inbox where tenant_id = @t and device_id like @p", conn))
        {
            cmd.Parameters.AddWithValue("t", tenant); cmd.Parameters.AddWithValue("p", devicePrefix + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var d = r.GetString(0);
                if (!got.TryGetValue(d, out var set)) got[d] = set = [];
                set.Add(r.GetInt64(1));
            }
        }
        var asserted = new List<(string Dev, long From, long To)>();
        await using (var cmd = new NpgsqlCommand(
            "select device_id, from_no, to_no from pos.number_gap_assertion where tenant_id = @t and device_id like @p", conn))
        {
            cmd.Parameters.AddWithValue("t", tenant); cmd.Parameters.AddWithValue("p", devicePrefix + "%");
            await using var r = await cmd.ExecuteReaderAsync();
            while (await r.ReadAsync()) asserted.Add((r.GetString(0), r.GetInt64(1), r.GetInt64(2)));
        }

        var missing = new List<(string, long, long)>();
        foreach (var (dev, next) in hw)
        {
            var set = got.TryGetValue(dev, out var s2) ? s2 : [];
            if (set.Count == 0) continue;
            long? run = null;
            for (long n = set.Min; n < next; n++)
            {
                var gone = !set.Contains(n) && !asserted.Any(a => a.Dev == dev && n >= a.From && n <= a.To);
                if (gone) run ??= n;
                else if (run is not null) { missing.Add((dev, run.Value, n - 1)); run = null; }
            }
            if (run is not null) missing.Add((dev, run.Value, next - 1));
        }
        return (missing.Count == 0,
            missing.Count == 0
                ? $"every invoice number reported as issued by {hw.Count} device(s) has either arrived or been positively asserted"
                : $"{missing.Count} run(s) ISSUED BUT NEVER RECEIVED, first {missing[0].Item1} {missing[0].Item2}..{missing[0].Item3}",
            missing);
    }

    public static async Task<int> OpenExceptionsAsync(string cs, string? kind = null)
    {
        await using var conn = await Sql.OpenAsync(cs);
        return (int)await Sql.ScalarAsync<long>(conn,
            $"select count(*) from pos.exception_queue where resolved_at is null" +
            (kind is null ? "" : $" and kind = '{kind}'"));
    }
}
