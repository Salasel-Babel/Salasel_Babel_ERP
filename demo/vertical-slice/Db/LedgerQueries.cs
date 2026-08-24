using System.Diagnostics;
using System.Globalization;
using BabelDemo.Support;
using Npgsql;

namespace BabelDemo.Db;

public sealed record AccountDto(string Code, string? Parent, string NameAr, string NameEn,
                                string Type, string TypeAr, string NormalSide, string NormalSideAr,
                                bool Postable, int Level);

public sealed record TrialBalanceRow(string Code, string NameAr, string NameEn, string Type,
                                     decimal Debit, decimal Credit, decimal Balance, string BalanceSide);

public sealed record TrialBalance(string Period, string[] Periods, TrialBalanceRow[] Rows,
                                  decimal TotalDebit, decimal TotalCredit, bool Balanced, int AccountCount);

public sealed record EntryLineDto(int LineNo, string AccountCode, string NameAr, string NameEn,
                                  string Description, decimal Debit, decimal Credit);

public sealed record EntryDto(long EntryNo, long ChainSeq, string EntryId, string EntryDate,
                              string MemoAr, string Memo, string Actor, string PostedAt,
                              string EntryHash, string PrevHash, decimal Total, EntryLineDto[] Lines);

public sealed record ChainRow(long ChainSeq, long EntryNo, string EntryDate, string MemoAr,
                              decimal Total, string StoredHash, string RecomputedHash, string Status);

public sealed record NaiveBalanceCheck(decimal TotalDebit, decimal TotalCredit, bool Balanced, string Verdict);

public sealed record VerifyResult(bool Ok, long? FirstDivergentSeq, string Reason, string ReasonEn,
                                  int Checked, long ElapsedMs, string Connection,
                                  ChainRow[] Rows, NaiveBalanceCheck Naive);

/// <summary>قراءات فقط. لا شيء هنا يكتب في الدفتر.</summary>
public static class LedgerQueries
{
    public static string TypeAr(string t) => t switch
    {
        "asset" => "أصول",
        "liability" => "خصوم",
        "equity" => "حقوق ملكية",
        "revenue" => "إيرادات",
        "expense" => "مصروفات",
        _ => t
    };

    public static async Task<AccountDto[]> AccountsAsync(CancellationToken ct = default)
    {
        await using var c = await Sql.OpenAsync(Config.App, ct);
        await using var cmd = new NpgsqlCommand("""
            select account_code, parent_code, name_ar, name_en, account_type, normal_side, is_postable
            from ledger.account order by sort_order
            """, c);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var list = new List<AccountDto>();
        while (await r.ReadAsync(ct))
        {
            var code = r.GetString(0);
            var type = r.GetString(4);
            var side = r.GetString(5);
            list.Add(new AccountDto(
                code, r.IsDBNull(1) ? null : r.GetString(1), r.GetString(2), r.GetString(3),
                type, TypeAr(type), side, side == "debit" ? "مدين" : "دائن",
                r.GetBoolean(6), code.Length switch { 1 => 0, 2 => 1, _ => 2 }));
        }
        return [.. list];
    }

    public static async Task<TrialBalance> TrialBalanceAsync(string? period, CancellationToken ct = default)
    {
        await using var c = await Sql.OpenAsync(Config.App, ct);

        var periods = new List<string>();
        await using (var cmd = new NpgsqlCommand(
            "select distinct period from ledger.account_balance where book_id = @b order by period", c))
        {
            cmd.Parameters.AddWithValue("b", Config.BookId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct)) periods.Add(r.GetString(0));
        }

        var filter = string.IsNullOrWhiteSpace(period) || period == "all" ? null : period;
        await using var cmd2 = new NpgsqlCommand($"""
            select a.account_code, a.name_ar, a.name_en, a.account_type, a.normal_side,
                   sum(b.debit) as debit, sum(b.credit) as credit
            from ledger.account_balance b
            join ledger.account a on a.account_code = b.account_code
            where b.book_id = @b {(filter is null ? "" : "and b.period = @p")}
            group by a.account_code, a.name_ar, a.name_en, a.account_type, a.normal_side, a.sort_order
            having sum(b.debit) <> 0 or sum(b.credit) <> 0
            order by a.sort_order
            """, c);
        cmd2.Parameters.AddWithValue("b", Config.BookId);
        if (filter is not null) cmd2.Parameters.AddWithValue("p", filter);

        var rows = new List<TrialBalanceRow>();
        decimal td = 0m, tc = 0m;
        await using (var r = await cmd2.ExecuteReaderAsync(ct))
        {
            while (await r.ReadAsync(ct))
            {
                var d = r.GetDecimal(5);
                var cr = r.GetDecimal(6);
                td += d; tc += cr;
                var net = d - cr;
                rows.Add(new TrialBalanceRow(
                    r.GetString(0), r.GetString(1), r.GetString(2), r.GetString(3),
                    d, cr, Math.Abs(net), net >= 0 ? "debit" : "credit"));
            }
        }
        return new TrialBalance(filter ?? "all", [.. periods], [.. rows], td, tc, td == tc, rows.Count);
    }

    public static async Task<EntryDto[]> EntriesAsync(CancellationToken ct = default)
    {
        await using var c = await Sql.OpenAsync(Config.App, ct);
        var entries = new List<EntryDto>();
        var lines = new Dictionary<long, List<EntryLineDto>>();

        await using (var cmd = new NpgsqlCommand("""
            select e.entry_no, l.line_no, l.account_code, a.name_ar, a.name_en, l.description, l.debit, l.credit
            from ledger.journal_line l
            join ledger.journal_entry e on e.entry_id = l.entry_id
            join ledger.account a on a.account_code = l.account_code
            where e.book_id = @b
            order by e.entry_no, l.line_no
            """, c))
        {
            cmd.Parameters.AddWithValue("b", Config.BookId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var no = r.GetInt64(0);
                if (!lines.TryGetValue(no, out var list)) lines[no] = list = [];
                list.Add(new EntryLineDto(r.GetInt32(1), r.GetString(2), r.GetString(3), r.GetString(4),
                                          r.GetString(5), r.GetDecimal(6), r.GetDecimal(7)));
            }
        }

        await using (var cmd = new NpgsqlCommand("""
            select entry_no, chain_seq, entry_id, entry_date, memo_ar, memo, actor, posted_at,
                   encode(entry_hash, 'hex'), encode(prev_hash, 'hex')
            from ledger.journal_entry where book_id = @b order by chain_seq
            """, c))
        {
            cmd.Parameters.AddWithValue("b", Config.BookId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
            {
                var no = r.GetInt64(0);
                var ls = lines.TryGetValue(no, out var l) ? l : [];
                entries.Add(new EntryDto(
                    no, r.GetInt64(1), r.GetGuid(2).ToString(),
                    r.GetFieldValue<DateOnly>(3).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                    r.GetString(4), r.GetString(5), r.GetString(6),
                    r.GetFieldValue<DateTime>(7).ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture),
                    r.GetString(8), r.GetString(9),
                    ls.Sum(x => x.Debit), [.. ls]));
            }
        }
        return [.. entries];
    }

    /// <summary>
    /// تمريرة تحقّق مستقلة: تقرأ الصفوف المخزّنة وتعيد بناء السلسلة من نقطة البداية،
    /// وتسمّي أول رقم تسلسل يختلف. تعمل على أي اتصال يملك SELECT فقط.
    ///
    /// Independent verification pass, adapted from
    /// spikes/relational-stack/Db/Ledger.cs (VerifyAsync).
    /// </summary>
    public static async Task<VerifyResult> VerifyAsync(CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        await using var conn = await Sql.OpenAsync(Config.App, ct);

        var entries = new List<JournalEntry>();
        await using (var cmd = new NpgsqlCommand("""
            select entry_id, book_id, tenant_id, entry_no, chain_seq, entry_date,
                   memo, memo_ar, posted_at, actor, prev_hash, entry_hash
            from ledger.journal_entry where book_id = @b order by chain_seq
            """, conn))
        {
            cmd.Parameters.AddWithValue("b", Config.BookId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
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
            cmd.Parameters.AddWithValue("b", Config.BookId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            while (await r.ReadAsync(ct))
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

        var rows = new List<ChainRow>();
        var expectedPrev = Canonical.Genesis(Config.BookId);
        long expectedSeq = 1;
        long? firstBad = null;
        var reason = "السلسلة سليمة — كل قيد يطابق بصمته وارتباطه بما قبله.";
        var reasonEn = "chain intact";

        foreach (var e in entries)
        {
            var ls = linesByEntry.TryGetValue(e.EntryId, out var l) ? l : [];
            var recomputed = Canonical.Hash(e, ls);
            var status = "ok";

            if (e.ChainSeq != expectedSeq)
            {
                status = "gap";
                if (firstBad is null)
                {
                    firstBad = e.ChainSeq;
                    reason = $"فجوة أو إعادة ترتيب: كان المتوقّع تسلسل {expectedSeq} فوُجد {e.ChainSeq}.";
                    reasonEn = $"gap or reordering: expected chain_seq {expectedSeq}, found {e.ChainSeq}";
                }
            }
            else if (!e.PrevHash.AsSpan().SequenceEqual(expectedPrev))
            {
                status = "broken-link";
                if (firstBad is null)
                {
                    firstBad = e.ChainSeq;
                    reason = $"ارتباط مكسور عند التسلسل {e.ChainSeq}: البصمة السابقة المخزّنة "
                           + $"{Canonical.Hex(e.PrevHash)[..16]}… لا تساوي بصمة القيد السابق "
                           + $"{Canonical.Hex(expectedPrev)[..16]}…";
                    reasonEn = $"broken link at chain_seq {e.ChainSeq}";
                }
            }
            else if (!recomputed.AsSpan().SequenceEqual(e.EntryHash))
            {
                status = "tampered";
                if (firstBad is null)
                {
                    firstBad = e.ChainSeq;
                    reason = $"محتوى معدَّل عند التسلسل {e.ChainSeq} (القيد رقم {e.EntryNo}): "
                           + $"البصمة المعاد حسابها {Canonical.Hex(recomputed)[..16]}… "
                           + $"لا تساوي المخزّنة {Canonical.Hex(e.EntryHash)[..16]}…";
                    reasonEn = $"content tampered at chain_seq {e.ChainSeq} (entry_no {e.EntryNo})";
                }
            }

            rows.Add(new ChainRow(
                e.ChainSeq, e.EntryNo,
                e.EntryDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                e.MemoAr, ls.Sum(x => x.Debit),
                Canonical.Hex(e.EntryHash), Canonical.Hex(recomputed), status));

            expectedPrev = e.EntryHash;
            expectedSeq = e.ChainSeq + 1;
        }

        // الفحص الساذج الذي يستخدمه أي نظام محاسبي: هل مجموع المدين = مجموع الدائن؟
        // العابث الذكي يحافظ على تساويهما، فيمرّ هذا الفحص بنجاح ولا يكتشف شيئاً.
        decimal naiveD = 0m, naiveC = 0m;
        await using (var cmd = new NpgsqlCommand("""
            select coalesce(sum(l.debit), 0), coalesce(sum(l.credit), 0)
            from ledger.journal_line l
            join ledger.journal_entry e on e.entry_id = l.entry_id
            where e.book_id = @b
            """, conn))
        {
            cmd.Parameters.AddWithValue("b", Config.BookId);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (await r.ReadAsync(ct)) { naiveD = r.GetDecimal(0); naiveC = r.GetDecimal(1); }
        }

        var naive = new NaiveBalanceCheck(naiveD, naiveC, naiveD == naiveC,
            naiveD == naiveC
                ? "مجموع المدين = مجموع الدائن ✓ — هذا الفحص لا يكتشف شيئاً، لأن العابث حافظ على التوازن."
                : "مجموع المدين ≠ مجموع الدائن.");

        sw.Stop();
        return new VerifyResult(firstBad is null, firstBad, reason, reasonEn, entries.Count,
                                sw.ElapsedMilliseconds, Config.Describe(Config.App), [.. rows], naive);
    }
}
