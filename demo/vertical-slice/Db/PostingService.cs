using System.Globalization;
using System.Text;
using BabelDemo.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Npgsql;

namespace BabelDemo.Db;

public sealed record LineInput(string AccountCode, string Description, decimal Debit, decimal Credit);

public sealed record PostRequest(DateOnly EntryDate, string MemoAr, string Memo, string Actor,
                                 IReadOnlyList<LineInput> Lines);

public sealed record PostedEntry(
    long EntryNo, long ChainSeq, Guid EntryId, string EntryHash, string PrevHash,
    string Canonical, DateTime PostedAt, string Period,
    decimal TotalDebit, decimal TotalCredit,
    int BalanceRowsExpected, int BalanceRowsAffected, string BalanceSql,
    string[] TouchedAccounts);

public sealed record PostError(
    string Stage, string SqlState, string SqlStateName, string Message,
    string? Detail, string? Where, string? ConstraintName, string Connection, string Explanation);

public sealed record PostOutcome(bool Ok, PostedEntry? Entry, PostError? Error)
{
    public static PostOutcome Good(PostedEntry e) => new(true, e, null);
    public static PostOutcome Bad(PostError e) => new(false, null, e);
}

/// <summary>
/// مسار الكتابة الوحيد إلى دفتر الأستاذ. لا شيء آخر في هذا التطبيق يكتب في
/// ledger.journal_entry أو ledger.journal_line أو ledger.account_balance.
///
/// الترحيل كله نداء خادم واحد ومعاملة واحدة:
///   1. أخذ العدّاد بلا فجوات بـ SELECT ... FOR UPDATE (ليس sequence)
///   2. قراءة بصمة القيد السابق تحت القفل نفسه
///   3. حساب بصمة SHA-256 فوق بايتات قانونية تتضمّن رقم التسلسل والبصمة السابقة
///   4. إدراج القيد وسطوره
///   5. تحديث إسقاط الأرصدة بعبارة INSERT ... ON CONFLICT DO UPDATE واحدة،
///      صفوفها مرتّبة تصاعدياً بالحساب، مع التأكّد من عدد الصفوف المتأثرة
///   6. COMMIT — وهنا يعمل القيد المؤجّل الذي يرفض أي قيد غير متوازن
///
/// The single write path. One server-side call, one transaction; no client
/// round trip is held open inside it.
/// </summary>
public static class PostingService
{
    public static LedgerDbContext CreateContext(string? cs = null)
        => new(new DbContextOptionsBuilder<LedgerDbContext>().UseNpgsql(cs ?? Config.App).Options);

    public static async Task EnsureBookAsync()
        => await Sql.ExecAsync(Config.Owner, $"""
            insert into ledger.entry_counter (book_id, next_no, next_seq)
            values ('{Config.BookId}', 1, 1)
            on conflict (book_id) do nothing
            """);

    public static async Task<PostOutcome> PostAsync(PostRequest req, CancellationToken ct = default)
    {
        // ملاحظة مقصودة: لا يوجد أي فحص للتوازن في C#. قاعدة البيانات هي التي ترفض،
        // كي يرى الحاضر أن الحماية في المخطّط لا في التطبيق.
        // DELIBERATE: no balance check in C#. PostgreSQL is the enforcer.
        var stage = "OPEN";
        await using var ctx = CreateContext();
        IDbContextTransaction? tx = null;
        try
        {
            tx = await ctx.Database.BeginTransactionAsync(ct);
            var conn = (NpgsqlConnection)ctx.Database.GetDbConnection();
            var ntx = (NpgsqlTransaction)tx.GetDbTransaction();

            // ---- 1) العدّاد بلا فجوات: قفل صف، لا sequence -----------------
            stage = "COUNTER";
            long entryNo, chainSeq;
            await using (var cmd = new NpgsqlCommand(
                "select next_no, next_seq from ledger.entry_counter where book_id = @b for update", conn, ntx))
            {
                cmd.Parameters.AddWithValue("b", Config.BookId);
                await using var r = await cmd.ExecuteReaderAsync(ct);
                if (!await r.ReadAsync(ct))
                    throw new InvalidOperationException($"no counter row for book '{Config.BookId}'");
                entryNo = r.GetInt64(0);
                chainSeq = r.GetInt64(1);
            }

            // ---- 2) البصمة السابقة، تُقرأ تحت القفل نفسه ------------------
            stage = "PREV_HASH";
            byte[] prevHash;
            await using (var cmd = new NpgsqlCommand(
                "select entry_hash from ledger.journal_entry where book_id = @b and chain_seq = @s", conn, ntx))
            {
                cmd.Parameters.AddWithValue("b", Config.BookId);
                cmd.Parameters.AddWithValue("s", chainSeq - 1);
                var v = await cmd.ExecuteScalarAsync(ct);
                prevHash = v is byte[] bytes ? bytes : Canonical.Genesis(Config.BookId);
            }

            // ---- 3) البناء والبصمة ----------------------------------------
            stage = "HASH";
            var entry = new JournalEntry
            {
                EntryId = Guid.CreateVersion7(),
                BookId = Config.BookId,
                TenantId = Config.TenantId,
                EntryNo = entryNo,
                ChainSeq = chainSeq,
                EntryDate = req.EntryDate,
                Memo = req.Memo,
                MemoAr = req.MemoAr,
                PostedAt = Canonical.PgInstant(DateTime.UtcNow),
                Actor = req.Actor,
                PrevHash = prevHash,
                Lines = [.. req.Lines.Select((l, i) => new JournalLine
                {
                    LineId = Guid.CreateVersion7(),
                    LineNo = i + 1,
                    AccountCode = l.AccountCode,
                    Description = l.Description,
                    Debit = l.Debit,
                    Credit = l.Credit
                })]
            };
            var canonical = Canonical.Render(entry, entry.Lines);
            entry.EntryHash = Canonical.Hash(entry, entry.Lines);

            // ---- 4) الإدراج ------------------------------------------------
            stage = "INSERT";
            ctx.JournalEntries.Add(entry);
            await ctx.SaveChangesAsync(ct);

            await using (var cmd = new NpgsqlCommand(
                "update ledger.entry_counter set next_no = next_no + 1, next_seq = next_seq + 1 where book_id = @b",
                conn, ntx))
            {
                cmd.Parameters.AddWithValue("b", Config.BookId);
                var n = await cmd.ExecuteNonQueryAsync(ct);
                if (n != 1) throw new InvalidOperationException($"COUNTER_ROWCOUNT_MISMATCH expected 1, affected {n}");
            }

            // ---- 5) إسقاط الأرصدة داخل المعاملة نفسها ----------------------
            stage = "BALANCE_PROJECTION";
            var period = req.EntryDate.ToString("yyyy-MM", CultureInfo.InvariantCulture);
            var (expected, affected, balanceSql, touched) =
                await UpsertBalancesAsync(conn, ntx, period, entry.Lines, entry.PostedAt, ct);

            // ---- 6) COMMIT: هنا يعمل القيد المؤجّل -------------------------
            stage = "COMMIT";
            await tx.CommitAsync(ct);

            return PostOutcome.Good(new PostedEntry(
                entryNo, chainSeq, entry.EntryId,
                Canonical.Hex(entry.EntryHash), Canonical.Hex(prevHash), canonical,
                entry.PostedAt, period,
                entry.Lines.Sum(l => l.Debit), entry.Lines.Sum(l => l.Credit),
                expected, affected, balanceSql, touched));
        }
        catch (PostgresException ex)
        {
            if (tx is not null) await SafeRollbackAsync(tx);
            return PostOutcome.Bad(new PostError(
                stage, ex.SqlState ?? "", Sql.StateName(ex.SqlState), ex.MessageText,
                ex.Detail, ex.Where, ex.ConstraintName, Config.Describe(Config.App),
                Explain(stage, ex.SqlState)));
        }
        catch (Exception ex)
        {
            if (tx is not null) await SafeRollbackAsync(tx);
            return PostOutcome.Bad(new PostError(
                stage, "", "", ex.Message, null, null, null, Config.Describe(Config.App),
                "خطأ في التطبيق قبل بلوغ قاعدة البيانات."));
        }
        finally
        {
            if (tx is not null) await tx.DisposeAsync();
        }
    }

    private static async Task SafeRollbackAsync(IDbContextTransaction tx)
    {
        try { await tx.RollbackAsync(); } catch { /* المعاملة أُجهضت أصلاً */ }
    }

    private static string Explain(string stage, string? sqlState) => (stage, sqlState) switch
    {
        ("COMMIT", "23514") =>
            "رفضت PostgreSQL القيد عند COMMIT عبر قيد مؤجّل (DEFERRABLE INITIALLY DEFERRED). "
            + "لم يرفضه المتصفح ولا شيفرة C#: المعاملة كاملة — القيد وسطوره وصفوف الأرصدة — تراجعت.",
        (_, "42501") =>
            "دور التطبيق لا يملك هذه الصلاحية. الحماية في صلاحيات قاعدة البيانات لا في التطبيق.",
        (_, "23503") =>
            "الحساب غير موجود في دليل الحسابات، أو ليس حساباً قابلاً للترحيل.",
        _ => "رفض من قاعدة البيانات."
    };

    /// <summary>
    /// القاعدة: عبارة واحدة بالضبط، INSERT ... ON CONFLICT DO UPDATE (لا UPDATE مجرّد)،
    /// صفوفها مرتّبة تصاعدياً برمز الحساب حتى يكون ترتيب أخذ الأقفال ثابتاً بين الكتّاب،
    /// ثم تأكيد عدد الصفوف المتأثرة. UPDATE مجرّد في فترة لم تُنشأ صفوفها بعد يحدّث صفر
    /// صفوف ولا يرفع خطأً ويُثبِّت المعاملة بنجاح — وهذا الخطأ الصامت ممنوع هنا.
    /// </summary>
    private static async Task<(int Expected, int Affected, string Sql, string[] Touched)> UpsertBalancesAsync(
        NpgsqlConnection conn, NpgsqlTransaction tx, string period,
        IReadOnlyList<JournalLine> lines, DateTime at, CancellationToken ct)
    {
        var rows = lines
            .GroupBy(l => l.AccountCode)
            .Select(g => new { Code = g.Key, Debit = g.Sum(x => x.Debit), Credit = g.Sum(x => x.Credit) })
            .OrderBy(x => x.Code, StringComparer.Ordinal)   // ORDER BY account ASC
            .ToList();

        var sb = new StringBuilder(
            "insert into ledger.account_balance (book_id, period, account_code, debit, credit, updated_at)\nvalues ");
        var display = new StringBuilder(
            "insert into ledger.account_balance (book_id, period, account_code, debit, credit, updated_at)\nvalues ");
        for (var i = 0; i < rows.Count; i++)
        {
            if (i > 0) { sb.Append(",\n       "); display.Append(",\n       "); }
            sb.Append(CultureInfo.InvariantCulture, $"(@b, @p, @c{i}, @d{i}, @r{i}, @t)");
            display.Append(CultureInfo.InvariantCulture,
                $"('{Config.BookId}', '{period}', '{rows[i].Code}', {Money.Render(rows[i].Debit)}, {Money.Render(rows[i].Credit)}, now())");
        }
        const string tail = """

            on conflict (book_id, period, account_code) do update set
                debit      = ledger.account_balance.debit  + excluded.debit,
                credit     = ledger.account_balance.credit + excluded.credit,
                updated_at = excluded.updated_at
            """;
        sb.Append(tail);
        display.Append(tail);

        await using var cmd = new NpgsqlCommand(sb.ToString(), conn, tx);
        cmd.Parameters.AddWithValue("b", Config.BookId);
        cmd.Parameters.AddWithValue("p", period);
        cmd.Parameters.AddWithValue("t", at);
        for (var i = 0; i < rows.Count; i++)
        {
            cmd.Parameters.AddWithValue($"c{i}", rows[i].Code);
            cmd.Parameters.Add(new NpgsqlParameter<decimal>($"d{i}", rows[i].Debit));
            cmd.Parameters.Add(new NpgsqlParameter<decimal>($"r{i}", rows[i].Credit));
        }

        var affected = await cmd.ExecuteNonQueryAsync(ct);
        if (affected != rows.Count)
            throw new InvalidOperationException(
                $"BALANCE_ROWCOUNT_MISMATCH: expected {rows.Count} rows, statement affected {affected}");

        return (rows.Count, affected, display.ToString(), [.. rows.Select(r => r.Code)]);
    }

    /// <summary>التصحيح الوحيد المسموح: قيد عكسي جديد — لا تعديل ولا حذف.</summary>
    public static async Task<PostOutcome> ReverseAsync(long entryNo, string actor, CancellationToken ct = default)
    {
        await using var conn = await Sql.OpenAsync(Config.App, ct);
        DateOnly date;
        string memoAr, memo;
        var lines = new List<LineInput>();
        await using (var cmd = new NpgsqlCommand("""
            select e.entry_date, e.memo_ar, e.memo, l.account_code, l.description, l.debit, l.credit
            from ledger.journal_entry e
            join ledger.journal_line l on l.entry_id = e.entry_id
            where e.book_id = @b and e.entry_no = @n
            order by l.line_no
            """, conn))
        {
            cmd.Parameters.AddWithValue("b", Config.BookId);
            cmd.Parameters.AddWithValue("n", entryNo);
            await using var r = await cmd.ExecuteReaderAsync(ct);
            date = DateOnly.FromDateTime(DateTime.UtcNow);
            memoAr = memo = "";
            while (await r.ReadAsync(ct))
            {
                memoAr = $"قيد عكسي للقيد رقم {entryNo} — {r.GetString(1)}";
                memo = $"Reversal of entry {entryNo} — {r.GetString(2)}";
                lines.Add(new LineInput(r.GetString(3), r.GetString(4), r.GetDecimal(6), r.GetDecimal(5)));
            }
        }

        if (lines.Count == 0)
            return PostOutcome.Bad(new PostError("LOOKUP", "", "", $"لا يوجد قيد بالرقم {entryNo}",
                null, null, null, Config.Describe(Config.App), "تحقّق من رقم القيد."));

        return await PostAsync(new PostRequest(date, memoAr, memo, actor, lines), ct);
    }
}
