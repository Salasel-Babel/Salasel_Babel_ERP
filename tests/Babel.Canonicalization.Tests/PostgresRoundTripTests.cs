using System.Globalization;
using Babel.Canonicalization.Schemas;
using Npgsql;

namespace Babel.Canonicalization.Tests;

/// <summary>
/// <b>الدورة الحقيقية.</b> اختبار في الذاكرة لا يمسك مصيدة الميكروثانية ولا مصيدة
/// المقياس: كلاهما لا ينفجر إلا بعد أن تمرّ القيمة على PostgreSQL وتعود.
///
/// هنا تُكتب N قيوداً إلى قاعدة بيانات محلية فعلية بأنواعها الحقيقية
/// (<c>timestamptz</c>, <c>numeric(19,4)</c>, <c>text</c>, <c>bytea</c>)، ثم تُقرأ،
/// ثم يُعاد بناء المستند من الصفوف المقروءة، ثم يُعاد التحقق من السلسلة كاملة.
///
/// لا كلمات مرور في المستودع: الاتصال يُقرأ من BABEL_CANON_TEST_DB ويسقط إلى
/// اتصال محلي بلا كلمة مرور (pg_hba: trust على 127.0.0.1).
/// </summary>
[Collection("postgres")]
public sealed class PostgresRoundTripTests : IAsyncLifetime
{
    private const string Database = "babel_canon_tests";

    private static string Maintenance =>
        Environment.GetEnvironmentVariable("BABEL_CANON_TEST_ADMIN_DB")
        ?? "Host=127.0.0.1;Port=5432;Database=postgres;Username=postgres;Include Error Detail=true";

    private static string Target =>
        Environment.GetEnvironmentVariable("BABEL_CANON_TEST_DB")
        ?? $"Host=127.0.0.1;Port=5432;Database={Database};Username=postgres;Include Error Detail=true";

    private bool _available;

    public async ValueTask InitializeAsync()
    {
        try
        {
            await using (var admin = new NpgsqlConnection(Maintenance))
            {
                await admin.OpenAsync();
                await using var check = new NpgsqlCommand(
                    $"select 1 from pg_database where datname = '{Database}'", admin);
                if (await check.ExecuteScalarAsync() is null)
                {
                    await using var create = new NpgsqlCommand($"create database {Database}", admin);
                    await create.ExecuteNonQueryAsync();
                }
            }

            await using var conn = await OpenAsync();
            await ExecAsync(conn, """
                drop table if exists canon_chain;
                create table canon_chain (
                    chain_scope      text        not null,
                    chain_seq        bigint      not null,
                    canon_version    text        not null,
                    entry_id         uuid        not null,
                    entry_no         bigint      not null,
                    entry_date       date        not null,
                    posted_at        timestamptz not null,
                    status           text        not null,
                    actor            text        not null,
                    memo             text            null,
                    memo_ar          text            null,
                    source_ref       text            null,
                    idempotency_key  text        not null,
                    currency         text        not null,
                    prev_hash        bytea       not null,
                    entry_hash       bytea       not null,
                    canonical_bytes  bytea       not null,
                    -- عمود البحث المشتقّ: مستثنى من التجزئة، وموجود عمداً لإثبات الفصل
                    memo_ar_search   text            null,
                    primary key (chain_scope, chain_seq)
                );
                drop table if exists canon_line;
                create table canon_line (
                    chain_scope   text          not null,
                    chain_seq     bigint        not null,
                    line_no       int           not null,
                    account_code  text          not null,
                    debit         numeric(19,4) not null,
                    credit        numeric(19,4) not null,
                    cost_center   text              null,
                    description   text              null,
                    primary key (chain_scope, chain_seq, line_no)
                );
                """);
            _available = true;
        }
        catch (NpgsqlException)
        {
            _available = false;
        }
        catch (System.Net.Sockets.SocketException)
        {
            _available = false;
        }
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static async Task<NpgsqlConnection> OpenAsync()
    {
        var c = new NpgsqlConnection(Target);
        await c.OpenAsync();
        return c;
    }

    private static async Task ExecAsync(NpgsqlConnection c, string sql)
    {
        await using var cmd = new NpgsqlCommand(sql, c);
        await cmd.ExecuteNonQueryAsync();
    }

    // =====================================================================

    /// <summary>
    /// N سجلاً تُكتب وتُقرأ ويُعاد التحقق منها. هذا هو الاختبار الذي يمسك
    /// مصيدة الميكروثانية ومصيدة المقياس 4 فعلياً.
    /// </summary>
    [Fact]
    public async Task FullChainReVerifiesAfterAPostgresRoundTrip()
    {
        Assert.True(_available, "PostgreSQL غير متاح — لا يجوز اعتبار هذا الاختبار ناجحاً بالتجاهل.");

        const int n = 40;
        var scope = JournalEntrySchema.ChainScope(Fixtures.Tenant, Fixtures.Book, Fixtures.Year);
        var genesis = Fixtures.Genesis;

        await using var conn = await OpenAsync();
        await ExecAsync(conn, $"delete from canon_line where chain_scope = '{scope}'; " +
                              $"delete from canon_chain where chain_scope = '{scope}'");

        var previous = genesis;
        for (var seq = 1; seq <= n; seq++)
        {
            // قيم مصمّمة لتفجير المصائد:
            //  • المبلغ يُكتب بمقياس 2 عمداً (100.00m) بينما العمود numeric(19,4)
            //  • اللحظة تحمل ميكروثانية غير صفرية، وتُقصّ عند الحدّ
            //  • البيان عربي/إنجليزي مختلط بعد تنظيف الحدّ
            var amount = decimal.Parse(
                (seq * 100).ToString(CultureInfo.InvariantCulture) + ".00",
                NumberStyles.Number, CultureInfo.InvariantCulture);

            var postedAt = Instants.Truncate(
                new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc)
                    .AddSeconds(seq)
                    .AddTicks(1234567));   // دقّة دون الميكروثانية تُقصّ هنا

            var memoAr = TextRules.CleanForInput(
                $"قيد رقم {seq.ToString(CultureInfo.InvariantCulture)} \u200F- فرع الرياض Riyadh");

            var doc = Document(scope, seq, amount, postedAt, memoAr);
            var link = Canonicalizer.Compute(doc, seq, previous);

            await InsertAsync(conn, scope, seq, link, amount, postedAt, memoAr);
            previous = link.Hash;
        }

        var records = await ReadAsync(conn, scope);
        Assert.Equal(n, records.Count);

        var verdict = ChainVerifier.VerifyChain(records, genesis);
        Assert.True(verdict.Ok, verdict.ToString());
        Assert.Equal(n, verdict.Checked);
        Assert.Null(verdict.FirstDivergentSequence);
    }

    /// <summary>
    /// المصيدة رقم 1 و2 مباشرةً: القيم المخزَّنة تعود <b>بنفس الشكل اللفظي</b>.
    /// </summary>
    [Fact]
    public async Task StoredValuesReturnWithTheIdenticalCanonicalLexicalForm()
    {
        Assert.True(_available, "PostgreSQL غير متاح.");

        await using var conn = await OpenAsync();
        await ExecAsync(conn, "drop table if exists canon_probe; " +
                              "create table canon_probe(id int primary key, m numeric(19,4), ts timestamptz, t text)");

        var written = Instants.Truncate(
            new DateTime(2026, 3, 29, 1, 30, 0, DateTimeKind.Utc).AddTicks(1234567));
        const decimal money = 100.00m;                 // مقياس 2 عمداً
        var text = TextRules.CleanForInput("فرع الرياض \u200F- Riyadh ١٠٠");

        await using (var ins = new NpgsqlCommand(
            "insert into canon_probe(id,m,ts,t) values(1,@m,@ts,@t)", conn))
        {
            ins.Parameters.AddWithValue("m", money);
            ins.Parameters.AddWithValue("ts", written);
            ins.Parameters.AddWithValue("t", text);
            await ins.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        await using var read = await new NpgsqlCommand("select m, ts, t from canon_probe where id = 1", conn)
            .ExecuteReaderAsync(TestContext.Current.CancellationToken);
        Assert.True(await read.ReadAsync(TestContext.Current.CancellationToken));

        var backMoney = read.GetDecimal(0);
        var backTs = read.GetFieldValue<DateTime>(1);
        var backText = read.GetString(2);

        // المقياس المُعلَن تغيّر (2 -> 4) لكن الشكل اللفظي القانوني لم يتغيّر.
        Assert.NotEqual(
            (decimal.GetBits(money)[3] >> 16) & 0xFF,
            (decimal.GetBits(backMoney)[3] >> 16) & 0xFF);
        Assert.Equal(Amounts.Render(money), Amounts.Render(backMoney));

        Assert.Equal(DateTimeKind.Utc, backTs.Kind);
        Assert.Equal(written.Ticks, backTs.Ticks);
        Assert.Equal(Instants.Render(written), Instants.Render(backTs));

        Assert.Equal(text, backText);
        TextRules.RequireCanonical(backText);   // لا يرمي: ما خُزِّن ما يزال قانونياً
    }

    /// <summary>
    /// عبث مباشر بـSQL من مالك الجدول، <b>يحافظ على توازن مدين = دائن</b>،
    /// فلا يراه فحص التوازن ولا مُشغِّل الإدراج. السلسلة تراه، وتسمّي أول تسلسل منحرف.
    /// </summary>
    [Fact]
    public async Task ABalancePreservingTamperIsDetectedAtTheFirstDivergentSequence()
    {
        Assert.True(_available, "PostgreSQL غير متاح.");

        const int n = 8;
        const long target = 4;
        var scope = "TAMPER|" + JournalEntrySchema.ChainScope(Fixtures.Tenant, "TAMPER", Fixtures.Year);
        var genesis = Canonicalizer.Genesis(scope);

        await using var conn = await OpenAsync();
        await SeedAsync(conn, scope, genesis, n);

        var clean = ChainVerifier.VerifyChain(await ReadAsync(conn, scope), genesis);
        Assert.True(clean.Ok, clean.ToString());

        // ── العبث: 40.0000 تُنقل بين سطرين مدينين. الفرق: صفر. التوازن: سليم.
        await ExecAsync(conn, $"""
            update canon_line set debit = debit + 40.0000
             where chain_scope = '{scope}' and chain_seq = {target} and line_no = 1;
            update canon_line set debit = debit - 40.0000
             where chain_scope = '{scope}' and chain_seq = {target} and line_no = 3;
            """);

        await using (var bal = new NpgsqlCommand(
            $"select coalesce(sum(debit),0) = coalesce(sum(credit),0) from canon_line " +
            $"where chain_scope = '{scope}' and chain_seq = {target}", conn))
        {
            Assert.True((bool)(await bal.ExecuteScalarAsync(TestContext.Current.CancellationToken))!,
                "العبث يجب أن يُبقي القيد متوازناً، وإلا لم يُثبت شيئاً.");
        }

        var detected = ChainVerifier.VerifyChain(await ReadAsync(conn, scope), genesis);
        Assert.False(detected.Ok);
        Assert.Equal(target, detected.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.ContentTampered, detected.Verdict);

        // ── العابث الأذكى: يعيد حساب entry_hash للسجل المعبوث به ويكتبه.
        var records = await ReadAsync(conn, scope);
        var tampered = records.Single(r => r.Sequence == target);
        var repaired = Canonicalizer.Compute(
            tampered.Document.Unbind(), target, tampered.StoredPreviousHash);

        await using (var upd = new NpgsqlCommand(
            "update canon_chain set entry_hash = @h where chain_scope = @s and chain_seq = @q", conn))
        {
            upd.Parameters.AddWithValue("h", repaired.Hash);
            upd.Parameters.AddWithValue("s", scope);
            upd.Parameters.AddWithValue("q", target);
            await upd.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var stillDetected = ChainVerifier.VerifyChain(await ReadAsync(conn, scope), genesis);
        Assert.False(stillDetected.Ok);
        Assert.Equal(target + 1, stillDetected.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.LinkBroken, stillDetected.Verdict);
    }

    /// <summary>حذف سجل من منتصف السلسلة يُكشف كفجوة تسلسل، لا يُستنتج من الغياب.</summary>
    [Fact]
    public async Task ADeletedRecordIsDetectedAsASequenceGap()
    {
        Assert.True(_available, "PostgreSQL غير متاح.");

        const int n = 6;
        var scope = "GAP|" + JournalEntrySchema.ChainScope(Fixtures.Tenant, "GAP", Fixtures.Year);
        var genesis = Canonicalizer.Genesis(scope);

        await using var conn = await OpenAsync();
        await SeedAsync(conn, scope, genesis, n);

        await ExecAsync(conn, $"delete from canon_line where chain_scope = '{scope}' and chain_seq = 3; " +
                              $"delete from canon_chain where chain_scope = '{scope}' and chain_seq = 3");

        var verdict = ChainVerifier.VerifyChain(await ReadAsync(conn, scope), genesis);
        Assert.False(verdict.Ok);
        Assert.Equal(3, verdict.FirstDivergentSequence);
        Assert.Equal(ChainVerdicts.SequenceGap, verdict.Verdict);
    }

    /// <summary>
    /// ‼ المصيدة ع-4 حيّة: تشغيل تطبيع البحث على العمود المُوقَّع نفسه.
    /// السلسلة تنكسر فوراً، والمتحقّق يسمّي أول تسلسل منحرف.
    /// </summary>
    [Fact]
    public async Task RunningSearchNormalisationOverASignedColumnBreaksTheChain()
    {
        Assert.True(_available, "PostgreSQL غير متاح.");

        const int n = 5;
        var scope = "SEARCH|" + JournalEntrySchema.ChainScope(Fixtures.Tenant, "SEARCH", Fixtures.Year);
        var genesis = Canonicalizer.Genesis(scope);

        await using var conn = await OpenAsync();
        await SeedAsync(conn, scope, genesis, n, memoAr: "قيد أرباح مكتبة الرياض الكبرى");

        Assert.True(ChainVerifier.VerifyChain(await ReadAsync(conn, scope), genesis).Ok);

        // العمود المشتقّ يُملأ — وهذا سليم تماماً.
        var records = await ReadAsync(conn, scope);
        foreach (var r in records)
        {
            var memo = MemoArOf(r);
            await using var ok = new NpgsqlCommand(
                "update canon_chain set memo_ar_search = @v where chain_scope = @s and chain_seq = @q", conn);
            ok.Parameters.AddWithValue("v", ArabicSearch.Normalize(memo).Value);
            ok.Parameters.AddWithValue("s", scope);
            ok.Parameters.AddWithValue("q", r.Sequence);
            await ok.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }
        Assert.True(ChainVerifier.VerifyChain(await ReadAsync(conn, scope), genesis).Ok,
            "ملء عمود البحث المشتقّ لا يجوز أن يمسّ السلسلة.");

        // ثم «التحسين» الكارثي: نفس التطبيع يُكتب فوق العمود المُوقَّع.
        var victim = records.Single(r => r.Sequence == 3);
        await using (var bad = new NpgsqlCommand(
            "update canon_chain set memo_ar = @v where chain_scope = @s and chain_seq = 3", conn))
        {
            bad.Parameters.AddWithValue("v", ArabicSearch.Normalize(MemoArOf(victim)).Value);
            bad.Parameters.AddWithValue("s", scope);
            await bad.ExecuteNonQueryAsync(TestContext.Current.CancellationToken);
        }

        var broken = ChainVerifier.VerifyChain(await ReadAsync(conn, scope), genesis);
        Assert.False(broken.Ok);
        Assert.Equal(3, broken.FirstDivergentSequence);
    }

    // =====================================================================
    //  بناء المستند من الصفوف — نفس المسار الذي يستخدمه التحقق في الإنتاج
    // =====================================================================

    private static string MemoArOf(ChainRecord r)
        => r.Document.Entries.Single(e => e.Name == "memo_ar").Value!.Payload;

    private static CanonicalDocument Document(
        string scope, long seq, decimal amount, DateTime postedAt, string memoAr)
    {
        var parts = scope.Split('|');
        var tenant = parts.Length >= 3 ? parts[^3] : Fixtures.Tenant;
        var book = parts.Length >= 2 ? parts[^2] : Fixtures.Book;

        return JournalEntrySchema.V1.NewDocument()
            .Set("tenant_id", CanonicalValue.Text(tenant))
            .Set("book_id", CanonicalValue.Text(book))
            .Set("fiscal_year", CanonicalValue.Integer(Fixtures.Year))
            .Set("entry_id", CanonicalValue.Uuid(DeterministicGuid(seq)))
            .Set("entry_no", CanonicalValue.Integer(seq))
            .Set("entry_date", CanonicalValue.Date(new DateOnly(2026, 6, 1)))
            .Set("posted_at", CanonicalValue.Instant(postedAt))
            .Set("status", CanonicalValue.Token("POSTED"))
            .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
            .Set("memo", CanonicalValue.Text("round trip"))
            .Set("memo_ar", CanonicalValue.Text(memoAr))
            .Set("source_ref", CanonicalValue.Null())
            .Set("idempotency_key", CanonicalValue.Text("rt-" + seq.ToString(CultureInfo.InvariantCulture)))
            .Set("currency", CanonicalValue.Token("SAR"))
            .SetGroup("lines",
            [
                i => i.Set("line_no", CanonicalValue.Integer(1))
                      .Set("account_code", CanonicalValue.Text("1010"))
                      .Set("debit", CanonicalValue.Amount(amount))
                      .Set("credit", CanonicalValue.Amount(0m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("النقدية")),
                i => i.Set("line_no", CanonicalValue.Integer(2))
                      .Set("account_code", CanonicalValue.Text("4010"))
                      .Set("debit", CanonicalValue.Amount(0m))
                      .Set("credit", CanonicalValue.Amount(amount + 50.0000m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("المبيعات")),
                i => i.Set("line_no", CanonicalValue.Integer(3))
                      .Set("account_code", CanonicalValue.Text("1210"))
                      .Set("debit", CanonicalValue.Amount(50.0000m))
                      .Set("credit", CanonicalValue.Amount(0m))
                      .Set("cost_center", CanonicalValue.Null())
                      .Set("description", CanonicalValue.Text("الذمم المدينة"))
            ])
            .Build();
    }

    private static Guid DeterministicGuid(long seq)
    {
        var b = new byte[16];
        BitConverter.TryWriteBytes(b.AsSpan(8), seq);
        b[6] = 0x70; b[8] = 0x80;
        return new Guid(b);
    }

    private static async Task SeedAsync(
        NpgsqlConnection conn, string scope, byte[] genesis, int n, string? memoAr = null)
    {
        await ExecAsync(conn, $"delete from canon_line where chain_scope = '{scope}'; " +
                              $"delete from canon_chain where chain_scope = '{scope}'");
        var previous = genesis;
        for (var seq = 1; seq <= n; seq++)
        {
            var amount = 100.0000m * seq;
            var postedAt = Instants.Truncate(
                new DateTime(2026, 6, 1, 12, 0, 0, DateTimeKind.Utc).AddSeconds(seq).AddTicks(1234567));
            var memo = memoAr ?? TextRules.CleanForInput(
                $"قيد رقم {seq.ToString(CultureInfo.InvariantCulture)} - فرع الرياض");
            var doc = Document(scope, seq, amount, postedAt, memo);
            var link = Canonicalizer.Compute(doc, seq, previous);
            await InsertAsync(conn, scope, seq, link, amount, postedAt, memo);
            previous = link.Hash;
        }
    }

    private static async Task InsertAsync(
        NpgsqlConnection conn, string scope, long seq, ChainLink link,
        decimal amount, DateTime postedAt, string memoAr)
    {
        var parts = scope.Split('|');
        var tenant = parts.Length >= 3 ? parts[^3] : Fixtures.Tenant;
        var book = parts.Length >= 2 ? parts[^2] : Fixtures.Book;

        await using (var cmd = new NpgsqlCommand("""
            insert into canon_chain
              (chain_scope, chain_seq, canon_version, entry_id, entry_no, entry_date, posted_at,
               status, actor, memo, memo_ar, source_ref, idempotency_key, currency,
               prev_hash, entry_hash, canonical_bytes)
            values
              (@scope, @seq, @ver, @eid, @eno, @edate, @posted,
               @status, @actor, @memo, @memoar, null, @idem, @cur,
               @prev, @hash, @bytes)
            """, conn))
        {
            cmd.Parameters.AddWithValue("scope", scope);
            cmd.Parameters.AddWithValue("seq", seq);
            cmd.Parameters.AddWithValue("ver", link.CanonVersion);
            cmd.Parameters.AddWithValue("eid", DeterministicGuid(seq));
            cmd.Parameters.AddWithValue("eno", seq);
            cmd.Parameters.AddWithValue("edate", new DateOnly(2026, 6, 1));
            cmd.Parameters.AddWithValue("posted", postedAt);
            cmd.Parameters.AddWithValue("status", "POSTED");
            cmd.Parameters.AddWithValue("actor", "muhasib@acme.sa");
            cmd.Parameters.AddWithValue("memo", "round trip");
            cmd.Parameters.AddWithValue("memoar", memoAr);
            cmd.Parameters.AddWithValue("idem", "rt-" + seq.ToString(CultureInfo.InvariantCulture));
            cmd.Parameters.AddWithValue("cur", "SAR");
            cmd.Parameters.AddWithValue("prev", link.PreviousHash);
            cmd.Parameters.AddWithValue("hash", link.Hash);
            cmd.Parameters.AddWithValue("bytes", link.CanonicalBytes);
            _ = tenant; _ = book;
            await cmd.ExecuteNonQueryAsync();
        }

        (int no, string acc, decimal d, decimal c, string desc)[] lines =
        [
            (1, "1010", amount, 0m, "النقدية"),
            (2, "4010", 0m, amount + 50.0000m, "المبيعات"),
            (3, "1210", 50.0000m, 0m, "الذمم المدينة")
        ];

        foreach (var l in lines)
        {
            await using var cmd = new NpgsqlCommand("""
                insert into canon_line(chain_scope, chain_seq, line_no, account_code, debit, credit, cost_center, description)
                values(@scope, @seq, @no, @acc, @d, @c, null, @desc)
                """, conn);
            cmd.Parameters.AddWithValue("scope", scope);
            cmd.Parameters.AddWithValue("seq", seq);
            cmd.Parameters.AddWithValue("no", l.no);
            cmd.Parameters.AddWithValue("acc", l.acc);
            cmd.Parameters.AddWithValue("d", l.d);
            cmd.Parameters.AddWithValue("c", l.c);
            cmd.Parameters.AddWithValue("desc", l.desc);
            await cmd.ExecuteNonQueryAsync();
        }
    }

    /// <summary>يقرأ السلسلة من قاعدة البيانات ويعيد بناء المستندات من الصفوف.</summary>
    private static async Task<IReadOnlyList<ChainRecord>> ReadAsync(NpgsqlConnection conn, string scope)
    {
        var lines = new Dictionary<long, List<(int no, string acc, decimal d, decimal c, string? cc, string? desc)>>();
        await using (var lc = new NpgsqlCommand(
            "select chain_seq, line_no, account_code, debit, credit, cost_center, description " +
            "from canon_line where chain_scope = @s order by chain_seq, line_no", conn))
        {
            lc.Parameters.AddWithValue("s", scope);
            await using var r = await lc.ExecuteReaderAsync();
            while (await r.ReadAsync())
            {
                var seq = r.GetInt64(0);
                if (!lines.TryGetValue(seq, out var list)) lines[seq] = list = [];
                list.Add((r.GetInt32(1), r.GetString(2), r.GetDecimal(3), r.GetDecimal(4),
                    r.IsDBNull(5) ? null : r.GetString(5), r.IsDBNull(6) ? null : r.GetString(6)));
            }
        }

        var records = new List<ChainRecord>();
        await using var cc = new NpgsqlCommand("""
            select chain_seq, canon_version, entry_id, entry_no, entry_date, posted_at, status, actor,
                   memo, memo_ar, source_ref, idempotency_key, currency, prev_hash, entry_hash
            from canon_chain where chain_scope = @s order by chain_seq
            """, conn);
        cc.Parameters.AddWithValue("s", scope);

        await using var rd = await cc.ExecuteReaderAsync();
        while (await rd.ReadAsync())
        {
            var seq = rd.GetInt64(0);
            var version = rd.GetString(1);

            var b = JournalEntrySchema.V1.NewDocument()
                .Set("tenant_id", CanonicalValue.Text(TenantOf(scope)))
                .Set("book_id", CanonicalValue.Text(BookOf(scope)))
                .Set("fiscal_year", CanonicalValue.Integer(Fixtures.Year))
                .Set("entry_id", CanonicalValue.Uuid(rd.GetGuid(2)))
                .Set("entry_no", CanonicalValue.Integer(rd.GetInt64(3)))
                .Set("entry_date", CanonicalValue.Date(rd.GetFieldValue<DateOnly>(4)))
                .Set("posted_at", CanonicalValue.Instant(rd.GetFieldValue<DateTime>(5)))
                .Set("status", CanonicalValue.Token(rd.GetString(6)))
                .Set("actor", CanonicalValue.Text(rd.GetString(7)))
                .Set("memo", CanonicalValue.TextOrNull(rd.IsDBNull(8) ? null : rd.GetString(8)))
                .Set("memo_ar", CanonicalValue.TextOrNull(rd.IsDBNull(9) ? null : rd.GetString(9)))
                .Set("source_ref", CanonicalValue.TextOrNull(rd.IsDBNull(10) ? null : rd.GetString(10)))
                .Set("idempotency_key", CanonicalValue.Text(rd.GetString(11)))
                .Set("currency", CanonicalValue.Token(rd.GetString(12)));

            var group = (lines.TryGetValue(seq, out var ls) ? ls : [])
                .Select(l => (Action<CanonicalItemBuilder>)(i =>
                    i.Set("line_no", CanonicalValue.Integer(l.no))
                     .Set("account_code", CanonicalValue.Text(l.acc))
                     .Set("debit", CanonicalValue.Amount(l.d))
                     .Set("credit", CanonicalValue.Amount(l.c))
                     .Set("cost_center", CanonicalValue.TextOrNull(l.cc))
                     .Set("description", CanonicalValue.TextOrNull(l.desc))));

            records.Add(new ChainRecord
            {
                Sequence = seq,
                CanonVersion = version,
                Document = b.SetGroup("lines", group).Build(),
                StoredPreviousHash = (byte[])rd["prev_hash"],
                StoredHash = (byte[])rd["entry_hash"]
            });
        }

        return records;
    }

    private static string TenantOf(string scope)
    {
        var p = scope.Split('|');
        return p.Length >= 3 ? p[^3] : Fixtures.Tenant;
    }

    private static string BookOf(string scope)
    {
        var p = scope.Split('|');
        return p.Length >= 2 ? p[^2] : Fixtures.Book;
    }
}

/// <summary>اختبارات PostgreSQL تُشغَّل تتابعياً: تتشارك الجداول نفسها.</summary>
[CollectionDefinition("postgres", DisableParallelization = true)]
public sealed class PostgresCollection;
