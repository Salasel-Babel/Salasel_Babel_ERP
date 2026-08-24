using BabelPosOffline.Device;
using BabelPosOffline.Server;
using BabelPosOffline.Support;
using Npgsql;

namespace BabelPosOffline.Proofs;

/// <summary>
/// (3) الحصانة ضد التكرار: <b>لكل قيد</b> و<b>مستقلة عن الترتيب</b>، بمفتاح أساسي
/// يوفّره العميل. ويُثبَت هنا أيضاً فشل التصميم المحظور على المدخلات نفسها.
/// </summary>
public static class P3_Idempotency
{
    private static string Db(string n) => Path.Combine(Config.DeviceDir, $"{n}.sqlite");

    private static async Task<(PosDevice Device, SyncClient Client, List<SyncEntry> Batch, decimal Expected)>
        MakeAsync(SyncServer server, string dev, int n)
    {
        await server.RegisterDeviceAsync(Config.Tenant, dev);
        var g = await server.GrantRangeAsync(Config.Tenant, dev, 20000);
        var p = Db(dev); LocalStore.Delete(p);
        var d = PosDevice.Open(p, dev, Config.Tenant);
        d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
        decimal expected = 0m;
        for (int i = 0; i < n; i++) expected += d.RecordSale(P1_LocalStore.Basket).TotalGross;
        var c = new SyncClient(d, server);
        return (d, c, c.NextBatch(n), expected);
    }

    private static Task<SyncResponse> SendAsync(SyncServer s, string dev, IEnumerable<SyncEntry> e) =>
        s.SyncAsync(new SyncBatch(Config.Tenant, dev, Guid.CreateVersion7().ToString("N"), DateTime.UtcNow, [.. e]));

    private static async Task<(long Rows, decimal Gross)> LedgerFactsAsync(string dev)
    {
        var rows = await Sql.ScalarAsync<long>(Config.Admin,
            $"select count(*) from ledger.journal_entry where book_id = 'POS:{dev}' and source_idem_key not like '%#cogs'");
        var gross = await Sql.ScalarAsync<decimal>(Config.Admin, $"""
            select coalesce(sum(l.debit),0) from ledger.journal_line l
            join ledger.journal_entry e on e.entry_id = l.entry_id
            where e.book_id = 'POS:{dev}' and l.account_code = '{PosDevice.AccCash}'
            """);
        return (rows, gross);
    }

    public static async Task RunAsync()
    {
        Proof.Section("(3) الحصانة ضد التكرار — مفتاح أساسي من العميل، مستقل عن الترتيب");
        var server = new SyncServer(Config.Admin);

        await Proof.RunAsync("3-أ", "إعادة إرسال الدفعة نفسها ثلاث مرات: كل عملية مرة واحدة بالضبط", async () =>
        {
            var (d, c, batch, expected) = await MakeAsync(server, "D3A", 60);
            using var _ = d;
            var r1 = await SendAsync(server, "D3A", batch);
            var r2 = await SendAsync(server, "D3A", batch);
            var r3 = await SendAsync(server, "D3A", batch);
            var (rows, gross) = await LedgerFactsAsync("D3A");
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var chain = await Verifier.VerifyBookAsync(Config.Admin, "POS:D3A");
            var ok = rows == 60 && gross == expected && tb.Balanced && chain.Ok;
            return (ok,
                $"run 1: posted={r1.Acks.Count(a => a.Outcome == EntryOutcome.Posted)} duplicate={r1.Acks.Count(a => a.Outcome == EntryOutcome.Duplicate)}\n" +
                $"run 2: posted={r2.Acks.Count(a => a.Outcome == EntryOutcome.Posted)} duplicate={r2.Acks.Count(a => a.Outcome == EntryOutcome.Duplicate)}\n" +
                $"run 3: posted={r3.Acks.Count(a => a.Outcome == EntryOutcome.Posted)} duplicate={r3.Acks.Count(a => a.Outcome == EntryOutcome.Duplicate)}\n" +
                $"ledger entries = {rows}/60, cash = {Money.Canonical(gross)} vs expected {Money.Canonical(expected)}\n" +
                $"trial balance: {tb}\nchain: {chain.Reason}");
        });

        await Proof.RunAsync("3-ب", "إعادة الإرسال بترتيب معكوس ومخلوط: النتيجة نفسها تماماً", async () =>
        {
            var (d, c, batch, expected) = await MakeAsync(server, "D3B", 60);
            using var _ = d;
            var shuffled = batch.OrderBy(_ => Random.Shared.Next()).ToList();
            var reversed = Enumerable.Reverse(batch).ToList();
            await SendAsync(server, "D3B", reversed);            // الأول: معكوس تماماً
            await SendAsync(server, "D3B", shuffled);            // ثم مخلوط
            await SendAsync(server, "D3B", batch);               // ثم بالترتيب
            var (rows, gross) = await LedgerFactsAsync("D3B");
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var chain = await Verifier.VerifyBookAsync(Config.Admin, "POS:D3B");
            // سلسلة الجهاز مُحفوظة كبيانات، وترتيب دفتر الخادم هو ترتيب الوصول — وكلاهما صحيح
            var firstArrived = await Sql.ScalarAsync<long>(Config.Admin,
                "select device_seq from pos.sale_inbox where device_id='D3B' order by server_received_at limit 1");
            var ok = rows == 60 && gross == expected && tb.Balanced && chain.Ok;
            return (ok,
                $"delivered reversed, then shuffled, then in order — three full passes over the same 60 entries\n" +
                $"ledger entries = {rows}/60, cash = {Money.Canonical(gross)} vs expected {Money.Canonical(expected)}\n" +
                $"trial balance: {tb}\nchain: {chain.Reason}\n" +
                $"first entry to reach the server had device_seq = {firstArrived} (i.e. NOT 1) — الترتيب لا يعني شيئاً للحصانة.\n" +
                "ملاحظة معمارية: توجد سلسلتان — سلسلة الجهاز بترتيب الإصدار، وسلسلة دفتر الخادم\n" +
                "بترتيب الوصول. كلٌّ منهما تتحقّق مستقلة، والربط بينهما هو مفتاح الحصانة.");
        });

        await Proof.RunAsync("3-ج", "تداخل دفعتَي جهازين مختلفين: لا تلوّث بينهما", async () =>
        {
            var (d1, c1, b1, e1) = await MakeAsync(server, "D3C1", 40);
            var (d2, c2, b2, e2) = await MakeAsync(server, "D3C2", 40);
            using var _1 = d1; using var _2 = d2;
            var mixed = new List<SyncEntry>();
            for (int i = 0; i < 40; i++) { mixed.Add(b1[i]); mixed.Add(b2[39 - i]); }
            await SendAsync(server, "MIXED", mixed);
            await SendAsync(server, "MIXED", mixed.OrderBy(_ => Random.Shared.Next()).ToList());
            var (r1, g1) = await LedgerFactsAsync("D3C1");
            var (r2, g2) = await LedgerFactsAsync("D3C2");
            var ch1 = await Verifier.VerifyBookAsync(Config.Admin, "POS:D3C1");
            var ch2 = await Verifier.VerifyBookAsync(Config.Admin, "POS:D3C2");
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var ok = r1 == 40 && r2 == 40 && g1 == e1 && g2 == e2 && ch1.Ok && ch2.Ok && tb.Balanced;
            return (ok,
                $"one interleaved stream (D3C1[i], D3C2[39-i]) sent twice, the second time shuffled\n" +
                $"D3C1: {r1}/40 entries, cash {Money.Canonical(g1)} vs {Money.Canonical(e1)} — {ch1.Reason}\n" +
                $"D3C2: {r2}/40 entries, cash {Money.Canonical(g2)} vs {Money.Canonical(e2)} — {ch2.Reason}\n" +
                $"trial balance: {tb}\n" +
                "عدّاد وسلسلة لكل جهاز ⇒ لا صف ساخن عالمي، ولا تعارض بين جهازين على رقم واحد.");
        });

        await Proof.RunAsync("3-د", "إعادة الإرسال بعد فشل جزئي: النصف المُرحَّل لا يُرحَّل ثانية", async () =>
        {
            var (d, c, batch, expected) = await MakeAsync(server, "D3D", 60);
            using var _ = d;
            await SendAsync(server, "D3D", batch.Take(31));       // نجحت 31 ثم انقطع كل شيء
            var partial = await LedgerFactsAsync("D3D");
            await SendAsync(server, "D3D", batch);                // الجهاز لا يعرف، فيعيد الدفعة كاملة
            var (rows, gross) = await LedgerFactsAsync("D3D");
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var chain = await Verifier.VerifyBookAsync(Config.Admin, "POS:D3D");
            var ok = partial.Rows == 31 && rows == 60 && gross == expected && tb.Balanced && chain.Ok;
            return (ok,
                $"after the partial send: {partial.Rows} entries, cash {Money.Canonical(partial.Gross)}\n" +
                $"after re-sending all 60: {rows} entries, cash {Money.Canonical(gross)} vs expected {Money.Canonical(expected)}\n" +
                $"trial balance: {tb}\nchain: {chain.Reason}");
        });

        await Proof.RunAsync("3-هـ", "التصميم المحظور — حارس تسلسل لكل حساب — يفشل على المدخلات نفسها", async () =>
        {
            // المشهد المقيس في وثيقة المعمارية §6.4، مُعاد إنتاجه هنا حرفياً:
            // ثلاثة قيود بقيمة 500 ريال، يصل رقم 40 بعد رقم 41.
            await Sql.ExecAsync(Config.Admin, "truncate pos.forbidden_balance");
            var order = new (long Seq, decimal Amount)[] { (39, 500m), (41, 500m), (40, 500m) };
            var applied = new List<string>();
            await using (var conn = await Sql.OpenAsync(Config.Admin))
                foreach (var (seq, amt) in order)
                {
                    await using var cmd = new NpgsqlCommand("select pos.forbidden_apply('4101', @a, @s)", conn);
                    cmd.Parameters.AddWithValue("a", amt);
                    cmd.Parameters.AddWithValue("s", seq);
                    var n = (int)(await cmd.ExecuteScalarAsync())!;
                    applied.Add($"seq {seq}: amount {Money.Canonical(amt)} → rows affected = {n}" +
                                (n == 0 ? "   ← لم يُطبَّق شيء، وبلا أي خطأ" : ""));
                }
            var forbidden = await Sql.ScalarAsync<decimal>(Config.Admin,
                "select balance from pos.forbidden_balance where account_code = '4101'");

            // التصميم الصحيح على المدخلات نفسها: مفتاح أساسي من العميل، لا حارس
            await Sql.ExecAsync(Config.Admin, "truncate pos.naive_inbox");
            await using (var conn = await Sql.OpenAsync(Config.Admin))
                foreach (var (seq, amt) in order)
                {
                    await using var cmd = new NpgsqlCommand(
                        "insert into pos.naive_inbox (idem_key, gross) values (@k, @g) on conflict (idem_key) do nothing", conn);
                    cmd.Parameters.AddWithValue("k", $"T|SALE|D|{seq}");   // مفتاح فريد لكل قيد
                    cmd.Parameters.AddWithValue("g", amt);
                    await cmd.ExecuteNonQueryAsync();
                }
            var correct = await Sql.ScalarAsync<decimal>(Config.Admin, "select coalesce(sum(gross),0) from pos.naive_inbox");

            var expected = 1500m;
            var ok = forbidden == 1000m && correct == expected;
            var shortfall = expected - forbidden;
            return (ok,
                string.Join('\n', applied) + "\n" +
                $"FORBIDDEN (per-account applied_seq guard): balance = {Money.Canonical(forbidden)} of {Money.Canonical(expected)} " +
                $"⇒ نقص {Money.Canonical(shortfall)} ريال = {shortfall / expected * 100:F0}٪، بصمت تام\n" +
                $"CORRECT   (client-supplied idempotency key as PRIMARY KEY): {Money.Canonical(correct)} of {Money.Canonical(expected)} ⇒ exact\n" +
                "المزامنة دون اتصال تُسلِّم خارج الترتيب بطبيعتها، فهذا الفشل مضمون لا افتراضي.\n" +
                "والأخطر أن الحارس لا يرفع خطأً: يُثبِّت المعاملة بنجاح ويُبلغ بنجاح.");
        });

        await Proof.RunAsync("3-و", "مفتاح حصانة بلا هوية جهاز يبتلع عملية بيع حقيقية — وحارس المحتوى يكشفها", async () =>
        {
            var (dA, cA, bA, eA) = await MakeAsync(server, "D3F1", 20);
            var (dB, cB, bB, eB) = await MakeAsync(server, "D3F2", 20);
            using var _1 = dA; using var _2 = dB;

            // (1) المفتاح الساذج: tenant|doctype|device_seq — وتسلسل كل جهاز يبدأ من 1
            await Sql.ExecAsync(Config.Admin, "truncate pos.naive_inbox");
            decimal naiveExpected = 0m;
            await using (var conn = await Sql.OpenAsync(Config.Admin))
                foreach (var e in bA.Concat(bB))
                {
                    naiveExpected += e.TotalGross;
                    await using var cmd = new NpgsqlCommand(
                        "insert into pos.naive_inbox (idem_key, gross) values (@k, @g) on conflict (idem_key) do nothing", conn);
                    cmd.Parameters.AddWithValue("k", PosDevice.NaiveIdemKey(Config.Tenant, e.DocType, e.DeviceSeq));
                    cmd.Parameters.AddWithValue("g", e.TotalGross);
                    await cmd.ExecuteNonQueryAsync();
                }
            var naiveGot = await Sql.ScalarAsync<decimal>(Config.Admin, "select coalesce(sum(gross),0) from pos.naive_inbox");
            var naiveRows = await Sql.ScalarAsync<long>(Config.Admin, "select count(*) from pos.naive_inbox");

            // (2) المفتاح الصحيح عبر المسار الحقيقي
            await SendAsync(server, "D3F1", bA);
            await SendAsync(server, "D3F2", bB);
            var (rA, gA) = await LedgerFactsAsync("D3F1");
            var (rB, gB) = await LedgerFactsAsync("D3F2");

            // (3) حارس المحتوى: المفتاح نفسه بمبلغ مختلف — يجب أن يُرفع لا أن يُبتلع
            var tampered = bA[0] with { TotalGross = bA[0].TotalGross + 100m, PayloadHash = Canonical.HashOf("different-content") };
            var resp = await SendAsync(server, "D3F1", [tampered]);
            var mismatch = resp.Acks[0].Outcome;
            var raised = await Verifier.OpenExceptionsAsync(Config.Admin, "CONFLICT_MISMATCH");
            var afterTamper = await LedgerFactsAsync("D3F1");

            var ok = naiveRows == 20 && naiveGot < naiveExpected
                     && rA == 20 && rB == 20 && gA == eA && gB == eB
                     && mismatch == EntryOutcome.ConflictMismatch && raised > 0
                     && afterTamper.Rows == rA && afterTamper.Gross == gA;
            return (ok,
                $"(1) NAIVE key 'tenant|doctype|device_seq' over 40 sales from two devices:\n" +
                $"    rows stored = {naiveRows} of 40; value stored = {Money.Canonical(naiveGot)} of {Money.Canonical(naiveExpected)}\n" +
                $"    ⇒ ابتُلعت {40 - naiveRows} عملية بيع حقيقية بقيمة {Money.Canonical(naiveExpected - naiveGot)} ريال — بلا خطأ ولا سجل\n" +
                $"    ON CONFLICT DO NOTHING على مفتاح خاطئ = فقدان صامت، تماماً كحارس التسلسل\n" +
                $"(2) CORRECT key 'tenant|device|doctype|invoice_no': D3F1 {rA}/20 = {Money.Canonical(gA)}, D3F2 {rB}/20 = {Money.Canonical(gB)} ⇒ exact\n" +
                $"(3) نفس المفتاح بمحتوى مختلف (مبلغ +100): outcome = {mismatch}, exceptions raised = {raised}, ledger unchanged = {afterTamper.Rows == rA}\n" +
                "    الدرس: الحصانة ليست 'ON CONFLICT DO NOTHING'. هي مفتاح صحيح المجال +\n" +
                "    مقارنة بصمة المحتوى عند التصادم، وإلا فالفقدان الصامت يعود من باب آخر.");
        });
    }
}
