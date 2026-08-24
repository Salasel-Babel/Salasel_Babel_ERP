using Babel.ControlPlane.Support;
using Npgsql;
using NpgsqlTypes;

namespace Babel.ControlPlane.TenantSide;

/// <summary>
/// ترحيل قيد داخل قاعدة المستأجر.
///
/// <para>هذا الملف في مستوى التحكّم <b>لأن الفحص السابق للأرشفة والاستحقاق
/// يحتاجان بيانات حقيقية ليُثبَتا عليها</b> — لا ليكون نواة المحاسبة. ومع ذلك
/// يلتزم <b>بالقواعد الأربع</b> حرفياً، فلا يُودَع في الشجرة مسار كتابة
/// يخالفها ولو كان للاختبار:</para>
///
/// <list type="number">
/// <item><b>نداء خادم واحد</b>: العدّاد والرأس والسطور والأرصدة في أمر واحد.
/// صفر رحلات ذهاب وإياب بين أخذ القفل والـ<c>COMMIT</c> (فخ-14: 127× عند
/// 30 مللي‌ثانية).</item>
/// <item><b><c>INSERT … ON CONFLICT DO UPDATE</c></b> للأرصدة، لا <c>UPDATE</c>
/// مجرّد — صفوف الفترة الجديدة غير موجودة أصلاً (فخ-09).</item>
/// <item><b>ترتيب أقفال كلّي</b>: الأسطر مرتّبة بـ<c>account_code</c> في ‏C#
/// <b>و</b>بـ<c>ORDER BY</c> صريح داخل العبارة (فخ-10، فخ-11).</item>
/// <item><b>صفّ عدّاد</b> لكل (دفتر × سنة مالية) داخل نفس المعاملة — لا
/// <c>SEQUENCE</c> (فخ-12، فخ-15).</item>
/// </list>
/// </summary>
public static class Ledger
{
    public sealed record PostedEntry(Guid EntryId, long EntryNo);

    public static async Task<PostedEntry> PostAsync(NpgsqlConnection c, string bookCode,
        int fiscalYearIgnored, string periodCode, DateOnly entryDate, string moduleCode,
        string descriptionAr, IReadOnlyList<(string Account, decimal Debit, decimal Credit)> lines,
        CancellationToken ct = default)
    {
        if (lines.Count < 2)
            throw new ArgumentException("القيد يحتاج سطرين على الأقل", nameof(lines));

        var debit = lines.Sum(l => l.Debit);
        var credit = lines.Sum(l => l.Credit);
        if (decimal.Round(debit, 4) != decimal.Round(credit, 4))
            throw new InvalidOperationException(
                $"قيد غير متوازن: مدين {Canon.Amount(debit)} ≠ دائن {Canon.Amount(credit)}");

        var sorted = lines.OrderBy(l => l.Account, StringComparer.Ordinal).ToList();
        var entryId = Guid.CreateVersion7();

        // انضباط التوسيع/الانكماش داخل مسار الكتابة نفسه: عمود الوصف قد يكون
        // القديم أو الجديد حسب إصدار مخطط هذا المستأجر بالذات. الشيفرة الواحدة
        // تخدم مستأجرين على إصدارين — وهذا شرط طرح لا لطف (‏Migration/ExpandContract).
        var (_, hasNewMemo) = await Migration.ExpandContract.ProbeAsync(c, ct);
        var memoColumn = hasNewMemo ? "memo_ar" : "description_ar";

        var sql = $"""
            with ctr as (
                insert into ledger.entry_counter (book_code, fiscal_year, next_no)
                values (@book, @year, 1)
                on conflict (book_code, fiscal_year) do update
                   set next_no = ledger.entry_counter.next_no + 1
                returning next_no as entry_no
            ),
            head as (
                insert into ledger.journal_entry
                    (entry_id, book_code, entry_no, period_code, entry_date, module_code,
                     {memoColumn}, description_en, actor, posted_at)
                select @id, @book, ctr.entry_no, @period, @date, @module,
                       @ar, @en, @actor, @now
                  from ctr
                returning entry_id, entry_no
            ),
            ln as (
                insert into ledger.journal_line
                    (line_id, entry_id, line_no, account_code, debit, credit)
                select gen_random_uuid(), (select entry_id from head), t.ord,
                       t.code, t.debit, t.credit
                  from unnest(@codes::text[], @debits::numeric[], @credits::numeric[])
                       with ordinality as t(code, debit, credit, ord)
                returning account_code, debit, credit
            ),
            bal as (
                insert into ledger.account_balance (account_code, period_code, debit, credit)
                select account_code, @period, sum(debit), sum(credit)
                  from ln
                 group by account_code
                 order by account_code asc          -- ترتيب الأقفال صريح
                on conflict (account_code, period_code) do update
                   set debit  = ledger.account_balance.debit  + excluded.debit,
                       credit = ledger.account_balance.credit + excluded.credit
                returning account_code
            )
            select (select entry_no from head), (select count(*) from bal)
            """;

        await using var tx = await c.BeginTransactionAsync(ct);
        await using var cmd = Db.Cmd(c, sql, tx);
        cmd.Parameters.Add(Db.P("id", entryId, NpgsqlDbType.Uuid));
        cmd.Parameters.AddWithValue("book", bookCode);
        cmd.Parameters.AddWithValue("year", entryDate.Year);
        cmd.Parameters.AddWithValue("period", periodCode);
        cmd.Parameters.Add(Db.P("date", entryDate, NpgsqlDbType.Date));
        cmd.Parameters.AddWithValue("module", moduleCode);
        cmd.Parameters.AddWithValue("ar", descriptionAr);
        cmd.Parameters.AddWithValue("en", "journal entry");
        cmd.Parameters.AddWithValue("actor", "control-plane.proof");
        cmd.Parameters.AddWithValue("now", Canon.Now());
        cmd.Parameters.Add(new NpgsqlParameter("codes", NpgsqlDbType.Array | NpgsqlDbType.Text)
        { Value = sorted.Select(l => l.Account).ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter("debits", NpgsqlDbType.Array | NpgsqlDbType.Numeric)
        { Value = sorted.Select(l => decimal.Round(l.Debit, 4)).ToArray() });
        cmd.Parameters.Add(new NpgsqlParameter("credits", NpgsqlDbType.Array | NpgsqlDbType.Numeric)
        { Value = sorted.Select(l => decimal.Round(l.Credit, 4)).ToArray() });

        long entryNo;
        long balanceRows;
        await using (var r = await cmd.ExecuteReaderAsync(ct))
        {
            if (!await r.ReadAsync(ct))
                throw new InvalidOperationException("الترحيل لم يُرجِع صفّاً — المعاملة تُفشَل");
            entryNo = r.GetInt64(0);
            balanceRows = r.GetInt64(1);
        }

        // تأكيد عدد الصفوف: «صفر صفوف أرصدة بلا خطأ» هو فخ-09 بعينه.
        var expected = sorted.Select(l => l.Account).Distinct(StringComparer.Ordinal).Count();
        if (balanceRows != expected)
            throw new UnexpectedRowCountException("account_balance upsert", expected, (int)balanceRows);

        await tx.CommitAsync(ct);
        return new PostedEntry(entryId, entryNo);
    }

    public static async Task OpenDocumentAsync(NpgsqlConnection c, string moduleCode, string docNo,
        CancellationToken ct = default) =>
        await Db.WriteAsync(c, """
            insert into app.document (document_id, module_code, doc_no, state, created_at)
            values (gen_random_uuid(), @m, @n, 'Open', @t)
            """, 1, p =>
        {
            p.AddWithValue("m", moduleCode);
            p.AddWithValue("n", docNo);
            p.AddWithValue("t", Canon.Now());
        }, null, ct);

    public static async Task ClosePeriodAsync(NpgsqlConnection c, string periodCode,
        CancellationToken ct = default) =>
        await Db.WriteAsync(c,
            "update ledger.period set state = 'Closed', closed_at = @t where period_code = @p", 1,
            p => { p.AddWithValue("t", Canon.Now()); p.AddWithValue("p", periodCode); }, null, ct);

    public static async Task<decimal> NetBalanceAsync(NpgsqlConnection c, string moduleCode,
        CancellationToken ct = default) =>
        await Db.ScalarAsync<decimal>(c, """
            select coalesce(sum(b.debit - b.credit), 0)
              from ledger.account a
              left join ledger.account_balance b on b.account_code = a.account_code
             where a.subledger = @m
            """, p => p.AddWithValue("m", moduleCode), null, ct);
}
