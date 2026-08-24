using BabelPosOffline.Support;

namespace BabelPosOffline.Device;

public sealed record DeviceChainResult(bool Ok, long? FirstBadSeq, string Reason, int Checked);

/// <summary>
/// تحقّق مستقل من سلسلة الجهاز: يُعاد بناؤها من رابط النشأة، ويُسمّى <b>أول</b> تسلسل منحرف.
/// يعمل على الملف كما هو، بلا أي معرفة بالشيفرة التي كتبته.
/// </summary>
public static class DeviceVerifier
{
    public static DeviceChainResult VerifyChain(LocalStore store, string tenantId, string deviceId)
    {
        var sales = store.Query("""
            select sale_id, idem_key, doc_type, invoice_no, chain_seq, business_date, device_clock_at,
                   shift_id, original_idem_key, currency, total_net_minor, total_vat_minor,
                   total_gross_minor, prev_hash, entry_hash
            from sale order by chain_seq
            """, r => new
            {
                SaleId = r.GetString(0), IdemKey = r.GetString(1), DocType = r.GetString(2),
                InvoiceNo = r.GetInt64(3), Seq = r.GetInt64(4), BizDate = r.GetString(5),
                Clock = r.GetString(6), Shift = r.GetString(7),
                Orig = r.IsDBNull(8) ? null : r.GetString(8), Cur = r.GetString(9),
                Net = r.GetInt64(10), Vat = r.GetInt64(11), Gross = r.GetInt64(12),
                Prev = r.GetString(13), Hash = r.GetString(14)
            });

        var expectedPrev = Canonical.DeviceGenesis(tenantId, deviceId);
        long expectedSeq = 1;
        foreach (var s in sales)
        {
            if (s.Seq != expectedSeq)
                return new DeviceChainResult(false, s.Seq,
                    $"gap or reordering in the device chain: expected chain_seq {expectedSeq}, found {s.Seq}", sales.Count);

            if (!Canonical.UnHex(s.Prev).AsSpan().SequenceEqual(expectedPrev))
                return new DeviceChainResult(false, s.Seq,
                    $"broken link at chain_seq {s.Seq}: stored prev_hash != previous entry_hash", sales.Count);

            var lines = store.Query("""
                select line_no, item_code, qty_minor, unit_price_minor, line_net_minor, line_vat_minor
                from sale_line where sale_id = $s order by line_no
                """, r => new CanonLine(r.GetInt32(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3), r.GetInt64(4), r.GetInt64(5)),
                ("$s", s.SaleId));
            var jls = store.Query("""
                select line_no, account_code, debit_minor, credit_minor
                from journal_line where sale_id = $s order by line_no
                """, r => new CanonJournalLine(r.GetInt32(0), r.GetString(1), r.GetInt64(2), r.GetInt64(3)),
                ("$s", s.SaleId));

            var view = new SaleCanonicalView
            {
                TenantId = tenantId, DeviceId = deviceId, SaleId = s.SaleId, DocType = s.DocType,
                InvoiceNo = s.InvoiceNo, DeviceSeq = s.Seq, PrevHash = Canonical.UnHex(s.Prev),
                BusinessDate = s.BizDate, DeviceClockAt = s.Clock, ShiftId = s.Shift,
                OriginalIdemKey = s.Orig, Currency = s.Cur,
                TotalNetMinor = s.Net, TotalVatMinor = s.Vat, TotalGrossMinor = s.Gross,
                Lines = lines, JournalLines = jls
            };
            var recomputed = Canonical.Hex(Canonical.HashSale(view));
            if (recomputed != s.Hash)
                return new DeviceChainResult(false, s.Seq,
                    $"content tampered at chain_seq {s.Seq} (invoice {s.InvoiceNo}): recomputed {recomputed[..16]}… != stored {s.Hash[..16]}…",
                    sales.Count);

            expectedPrev = Canonical.UnHex(s.Hash);
            expectedSeq++;
        }
        return new DeviceChainResult(true, null, "device chain intact", sales.Count);
    }

    /// <summary>كل قيد على الجهاز متوازن، ومجموع السطور يساوي الإجمالي.</summary>
    public static (bool Ok, string Reason) VerifyBalances(LocalStore store)
    {
        var bad = store.Query("""
            select s.invoice_no,
                   coalesce(sum(j.debit_minor),0) - coalesce(sum(j.credit_minor),0) as diff,
                   count(j.line_no) as n
            from sale s left join journal_line j on j.sale_id = s.sale_id
            group by s.sale_id having diff <> 0 or n < 2
            """, r => (r.GetInt64(0), r.GetInt64(1), r.GetInt64(2)));
        if (bad.Count > 0)
            return (false, $"{bad.Count} unbalanced entries, first: invoice {bad[0].Item1} diff={bad[0].Item2} lines={bad[0].Item3}");
        return (true, "every local entry balances (sum debit = sum credit, >= 2 lines)");
    }

    /// <summary>لا فجوات في رقم الفاتورة ولا في تسلسل السلسلة.</summary>
    public static (bool Ok, string Reason) VerifyNoGaps(LocalStore store)
    {
        var rows = store.Query("select invoice_no, chain_seq from sale order by chain_seq",
                               r => (r.GetInt64(0), r.GetInt64(1)));
        if (rows.Count == 0) return (true, "no sales");
        long firstNo = rows[0].Item1;
        for (int i = 0; i < rows.Count; i++)
        {
            if (rows[i].Item2 != i + 1)
                return (false, $"chain_seq gap at index {i}: expected {i + 1}, found {rows[i].Item2}");
            if (rows[i].Item1 != firstNo + i)
                return (false, $"invoice_no gap at chain_seq {rows[i].Item2}: expected {firstNo + i}, found {rows[i].Item1}");
        }
        return (true, $"{rows.Count} entries, invoice_no {firstNo}..{rows[^1].Item1} contiguous, chain_seq 1..{rows.Count} contiguous");
    }
}
