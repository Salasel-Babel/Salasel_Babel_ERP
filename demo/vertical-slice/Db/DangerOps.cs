using System.Globalization;
using System.Text;
using BabelDemo.Support;
using Npgsql;

namespace BabelDemo.Db;

internal sealed record SqlAttempt(bool Succeeded, string Sql, string Connection, string Role,
                                int RowsAffected, string SqlState, string SqlStateName,
                                string Message, string? Detail, string? Hint, string Explanation);

internal sealed record LineSnapshot(int LineNo, string AccountCode, string NameAr, decimal Debit, decimal Credit);

internal sealed record TamperResult(bool Ok, string Message, long EntryNo, long ChainSeq, decimal Delta,
                                  string Connection, string Role, string Script,
                                  int StatementsRun, int RowsAffected,
                                  LineSnapshot[] Before, LineSnapshot[] After,
                                  string StoredHashBefore, string StoredHashAfter, string Note);

internal sealed record BidiResult(string Plain, string WithMark, int PlainLength, int MarkLength,
                                string PlainBytes, string MarkBytes, string PlainHash, string MarkHash,
                                bool Equal, string Note);

internal sealed record GrantRow(string Table, string Privileges);

/// <summary>
/// إجراءات العرض «الخطرة». كل واحدة تُظهر ردّ PostgreSQL الخام كما هو.
/// The demo's danger actions. Each one surfaces PostgreSQL's raw answer verbatim.
/// </summary>
internal static class DangerOps
{
    private static async Task<(Guid EntryId, Guid LineId, decimal Debit)> LocateAsync(long entryNo, CancellationToken ct)
    {
        await using var c = await Sql.OpenAsync(Config.App, ct);
        await using var cmd = new NpgsqlCommand("""
            select e.entry_id, l.line_id, l.debit
            from ledger.journal_entry e
            join ledger.journal_line l on l.entry_id = e.entry_id
            where e.book_id = @b and e.entry_no = @n
            order by l.debit desc, l.line_no
            limit 1
            """, c);
        cmd.Parameters.AddWithValue("b", Config.BookId);
        cmd.Parameters.AddWithValue("n", entryNo);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        if (!await r.ReadAsync(ct)) throw new InvalidOperationException($"لا يوجد قيد بالرقم {entryNo}");
        return (r.GetGuid(0), r.GetGuid(1), r.GetDecimal(2));
    }

    /// <summary>محاولة تعديل مبلغ في قيد مرحَّل، بدور التطبيق.</summary>
    public static async Task<SqlAttempt> TryUpdateAsync(long entryNo, decimal newAmount, CancellationToken ct = default)
    {
        var (_, lineId, _) = await LocateAsync(entryNo, ct);
        var sql = $"update ledger.journal_line\n   set debit = {Money.Render(newAmount)}\n where line_id = '{lineId}';";
        return await AttemptAsync(sql, ct);
    }

    /// <summary>محاولة حذف قيد مرحَّل، بدور التطبيق.</summary>
    public static async Task<SqlAttempt> TryDeleteAsync(long entryNo, CancellationToken ct = default)
    {
        var (entryId, _, _) = await LocateAsync(entryNo, ct);
        var sql = $"delete from ledger.journal_entry\n where entry_id = '{entryId}';";
        return await AttemptAsync(sql, ct);
    }

    private static async Task<SqlAttempt> AttemptAsync(string sql, CancellationToken ct)
    {
        try
        {
            await using var c = await Sql.OpenAsync(Config.App, ct);
            await using var cmd = new NpgsqlCommand(sql, c);
            var n = await cmd.ExecuteNonQueryAsync(ct);
            return new SqlAttempt(true, sql, Config.Describe(Config.App), Config.AppRole, n, "", "",
                "نُفِّذت العبارة — وهذا يعني أن الصلاحيات مضبوطة خطأً.", null, null,
                "متوقّع الفشل هنا. إن نجحت العبارة فالمخطّط ليس محصّناً.");
        }
        catch (PostgresException ex)
        {
            return new SqlAttempt(false, sql, Config.Describe(Config.App), Config.AppRole, 0,
                ex.SqlState ?? "", Sql.StateName(ex.SqlState), ex.MessageText, ex.Detail, ex.Hint,
                ex.SqlState == "42501"
                    ? "رفضت PostgreSQL العبارة لأن دور التطبيق لا يملك صلاحية UPDATE أو DELETE على جداول الدفتر. "
                    + "الرفض من قاعدة البيانات نفسها، لا من واجهة المستخدم ولا من شيفرة التطبيق. "
                    + "التصحيح الوحيد المتاح هو قيد عكسي جديد."
                    : "رفض من قاعدة البيانات.");
        }
    }

    /// <summary>لقطة صلاحيات حيّة من information_schema — لا من ذاكرة التطبيق.</summary>
    public static async Task<GrantRow[]> GrantsAsync(CancellationToken ct = default)
    {
        await using var c = await Sql.OpenAsync(Config.Owner, ct);
        await using var cmd = new NpgsqlCommand("""
            select t.table_name,
                   coalesce(string_agg(p.privilege_type, ', ' order by p.privilege_type), '—')
            from (select unnest(array['account','journal_entry','journal_line','entry_counter','account_balance']) as table_name) t
            left join information_schema.table_privileges p
                   on p.table_name = t.table_name and p.table_schema = 'ledger' and p.grantee = @g
            group by t.table_name
            order by t.table_name
            """, c);
        cmd.Parameters.AddWithValue("g", Config.AppRole);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<GrantRow>();
        while (await r.ReadAsync(ct)) rows.Add(new GrantRow(r.GetString(0), r.GetString(1)));
        return [.. rows];
    }

    /// <summary>
    /// المشهد الختامي: فتح قاعدة البيانات بحساب المالك (تجاوزاً لدور التطبيق) وتعديل
    /// مبلغ مرحَّل بـ SQL خام، مع المحافظة على تساوي المدين والدائن، وتحديث إسقاط
    /// الأرصدة أيضاً كي يبقى ميزان المراجعة متسقاً. لا فحص توازن يكتشف هذا.
    ///
    /// THE CLOSING SCENE: as the OWNER, edit a posted amount with raw SQL in a way
    /// that preserves debit = credit — and fix the balance projection too, so the
    /// trial balance still ties. Nothing but the hash chain can see this.
    /// </summary>
    public static async Task<TamperResult> TamperAsync(long entryNo, decimal delta, CancellationToken ct = default)
    {
        await using var c = await Sql.OpenAsync(Config.Owner, ct);

        var pending = await Sql.ScalarAsync<long>(c, "select count(*) from demo.tamper_log where not undone", ct);
        if (pending > 0)
            return new TamperResult(false, "هناك عبث مطبَّق بالفعل ولم يُستعد بعد. اضغط «استعادة» أولاً.",
                entryNo, 0, delta, Config.Describe(Config.Owner), "postgres", "", 0, 0, [], [], "", "", "");

        // القيد المستهدف وسطراه: أكبر سطر مدين وأكبر سطر دائن
        Guid entryId = default;
        long chainSeq = 0;
        string period = "";
        string hashBefore = "";
        await using (var cmd = new NpgsqlCommand("""
            select entry_id, chain_seq, to_char(entry_date, 'YYYY-MM'), encode(entry_hash,'hex')
            from ledger.journal_entry where book_id = @b and entry_no = @n
            """, c))
        {
            cmd.Parameters.AddWithValue("b", Config.BookId);
            cmd.Parameters.AddWithValue("n", entryNo);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return new TamperResult(false, $"لا يوجد قيد بالرقم {entryNo}.", entryNo, 0, delta,
                    Config.Describe(Config.Owner), "postgres", "", 0, 0, [], [], "", "", "");
            entryId = r.GetGuid(0); chainSeq = r.GetInt64(1); period = r.GetString(2); hashBefore = r.GetString(3);
        }

        var before = await LinesAsync(c, entryId, ct);
        var debitLine = before.OrderByDescending(l => l.Debit).First();
        var creditLine = before.OrderByDescending(l => l.Credit).First();
        if (debitLine.Debit <= 0 || creditLine.Credit <= 0)
            return new TamperResult(false, "هذا القيد لا يصلح لهذا العرض.", entryNo, chainSeq, delta,
                Config.Describe(Config.Owner), "postgres", "", 0, 0, before, [], hashBefore, "", "");

        var d = Money.Render(delta);
        var script = new StringBuilder();
        script.Append(CultureInfo.InvariantCulture, $"""
            -- المتصل: {Config.Describe(Config.Owner)}  (مالك المخطط، لا دور التطبيق)
            -- connected as the OWNER, bypassing the application role entirely
            begin;

            -- (1) ارفع سطر المدين {delta:0.0000} في القيد رقم {entryNo}
            update ledger.journal_line
               set debit = debit + {d}
             where entry_id = '{entryId}' and line_no = {debitLine.LineNo};

            -- (2) ارفع سطر الدائن بالمبلغ نفسه، فيبقى مدين = دائن تماماً
            update ledger.journal_line
               set credit = credit + {d}
             where entry_id = '{entryId}' and line_no = {creditLine.LineNo};

            -- (3) صحّح إسقاط الأرصدة أيضاً، فيبقى ميزان المراجعة متسقاً مع الدفتر
            update ledger.account_balance
               set debit = debit + {d}
             where book_id = '{Config.BookId}' and period = '{period}' and account_code = '{debitLine.AccountCode}';

            update ledger.account_balance
               set credit = credit + {d}
             where book_id = '{Config.BookId}' and period = '{period}' and account_code = '{creditLine.AccountCode}';

            commit;
            -- لم يُطلق أي قيد ولا مُشغِّل: القيد المؤجّل يعمل على INSERT فقط،
            -- ولو عمل هنا لمرّ بنجاح لأن مدين = دائن.
            """);

        var restore = $"""
            begin;
            update ledger.journal_line set debit = debit - {d}
             where entry_id = '{entryId}' and line_no = {debitLine.LineNo};
            update ledger.journal_line set credit = credit - {d}
             where entry_id = '{entryId}' and line_no = {creditLine.LineNo};
            update ledger.account_balance set debit = debit - {d}
             where book_id = '{Config.BookId}' and period = '{period}' and account_code = '{debitLine.AccountCode}';
            update ledger.account_balance set credit = credit - {d}
             where book_id = '{Config.BookId}' and period = '{period}' and account_code = '{creditLine.AccountCode}';
            commit;
            """;

        var affected = 0;
        await using (var tx = await c.BeginTransactionAsync(ct))
        {
            affected += await ExecAsync(c, tx, $"update ledger.journal_line set debit = debit + {d} where entry_id = '{entryId}' and line_no = {debitLine.LineNo}", ct);
            affected += await ExecAsync(c, tx, $"update ledger.journal_line set credit = credit + {d} where entry_id = '{entryId}' and line_no = {creditLine.LineNo}", ct);
            affected += await ExecAsync(c, tx, $"update ledger.account_balance set debit = debit + {d} where book_id = '{Config.BookId}' and period = '{period}' and account_code = '{debitLine.AccountCode}'", ct);
            affected += await ExecAsync(c, tx, $"update ledger.account_balance set credit = credit + {d} where book_id = '{Config.BookId}' and period = '{period}' and account_code = '{creditLine.AccountCode}'", ct);

            await using (var log = new NpgsqlCommand(
                "insert into demo.tamper_log (entry_no, applied_at, forward_sql, restore_sql) values (@n, now(), @f, @r)", c, tx))
            {
                log.Parameters.AddWithValue("n", entryNo);
                log.Parameters.AddWithValue("f", script.ToString());
                log.Parameters.AddWithValue("r", restore);
                await log.ExecuteNonQueryAsync(ct);
            }
            await tx.CommitAsync(ct);
        }

        var after = await LinesAsync(c, entryId, ct);
        var hashAfter = await Sql.ScalarAsync<string>(c,
            $"select encode(entry_hash,'hex') from ledger.journal_entry where entry_id = '{entryId}'", ct) ?? "";

        return new TamperResult(true,
            $"نُفِّذت أربع عبارات UPDATE بحساب المالك على القيد رقم {entryNo} (تسلسل {chainSeq}).",
            entryNo, chainSeq, delta, Config.Describe(Config.Owner), "postgres",
            script.ToString(), 4, affected, before, after, hashBefore, hashAfter,
            "عمود entry_hash لم يُمَسّ: العابث لا يستطيع إعادة حسابه لأن البصمة تشمل رقم التسلسل "
            + "وبصمة القيد السابق، وأي إعادة حساب تكسر كل ما بعده في السلسلة.");
    }

    public static async Task<TamperResult> RestoreAsync(CancellationToken ct = default)
    {
        await using var c = await Sql.OpenAsync(Config.Owner, ct);
        long id = 0, entryNo = 0;
        string restoreSql = "";
        await using (var cmd = new NpgsqlCommand(
            "select tamper_id, entry_no, restore_sql from demo.tamper_log where not undone order by tamper_id desc limit 1", c))
        {
            await using var r = await cmd.ExecuteReaderAsync(ct);
            if (!await r.ReadAsync(ct))
                return new TamperResult(false, "لا يوجد عبث مطبَّق لاستعادته.", 0, 0, 0m,
                    Config.Describe(Config.Owner), "postgres", "", 0, 0, [], [], "", "", "");
            id = r.GetInt64(0); entryNo = r.GetInt64(1); restoreSql = r.GetString(2);
        }

        await Sql.ExecAsync(c, restoreSql, ct: ct);
        await Sql.ExecAsync(c, $"update demo.tamper_log set undone = true where tamper_id = {id}", ct: ct);

        return new TamperResult(true, $"استُعيدت القيم الأصلية للقيد رقم {entryNo}.", entryNo, 0, 0m,
            Config.Describe(Config.Owner), "postgres", restoreSql, 4, 0, [], [], "", "",
            "السلسلة تعود سليمة لأن البايتات عادت كما كانت بالضبط.");
    }

    private static async Task<int> ExecAsync(NpgsqlConnection c, NpgsqlTransaction tx, string sql, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand(sql, c, tx);
        return await cmd.ExecuteNonQueryAsync(ct);
    }

    private static async Task<LineSnapshot[]> LinesAsync(NpgsqlConnection c, Guid entryId, CancellationToken ct)
    {
        await using var cmd = new NpgsqlCommand("""
            select l.line_no, l.account_code, a.name_ar, l.debit, l.credit
            from ledger.journal_line l join ledger.account a on a.account_code = l.account_code
            where l.entry_id = @e order by l.line_no
            """, c);
        cmd.Parameters.AddWithValue("e", entryId);
        await using var r = await cmd.ExecuteReaderAsync(ct);
        var rows = new List<LineSnapshot>();
        while (await r.ReadAsync(ct))
            rows.Add(new LineSnapshot(r.GetInt32(0), r.GetString(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4)));
        return [.. rows];
    }

    /// <summary>
    /// الحاشية: حرف U+200F (علامة اتجاه من اليمين لليسار) غير مرئي إطلاقاً،
    /// ومع ذلك يغيّر البايتات فيغيّر البصمة. لهذا التوحيد القياسي مواصفة لا نصيحة.
    /// </summary>
    public static BidiResult Bidi(string text)
    {
        var plain = Canonical.Text(text);
        var withMark = Canonical.Text(text + "‏");
        var pb = new UTF8Encoding(false).GetBytes(plain);
        var mb = new UTF8Encoding(false).GetBytes(withMark);
        var ph = Canonical.Hex(Canonical.HashOf(plain));
        var mh = Canonical.Hex(Canonical.HashOf(withMark));
        return new BidiResult(plain, withMark, plain.Length, withMark.Length,
            Convert.ToHexString(pb).ToLowerInvariant(), Convert.ToHexString(mb).ToLowerInvariant(),
            ph, mh, ph == mh,
            "النصّان متطابقان على الشاشة تماماً، ويختلفان في البايتات بحرف واحد غير مرئي (U+200F)، "
            + "فتختلف البصمة كلياً. لذلك تُوحَّد النصوص إلى NFC قبل البصم، ويُثبَّت مقياس الأرقام "
            + "وتُوحَّد الأوقات إلى UTC — وإلا صارت البصمة تعتمد على من كتب النص وبأي لوحة مفاتيح.");
    }
}
