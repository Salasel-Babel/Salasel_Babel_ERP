using System.Diagnostics;
using BabelPosOffline.Device;
using BabelPosOffline.Server;
using BabelPosOffline.Support;

namespace BabelPosOffline.Proofs;

/// <summary>(2) بروتوكول المزامنة: تجميع، ترتيب، استئناف بعد فشل جزئي، وضغط عكسي.</summary>
public static class P2_SyncProtocol
{
    private static string Db(string n) => Path.Combine(Config.DeviceDir, $"{n}.sqlite");

    public static async Task RunAsync(string exePath)
    {
        Proof.Section("(2) بروتوكول المزامنة — الدفعات والترتيب والاستئناف والضغط العكسي");
        var server = new SyncServer(Config.Admin);

        await Proof.RunAsync("2-أ", "دفعات مرتّبة: 350 عملية تصل مرة واحدة بالضبط وميزان المراجعة مضبوط", async () =>
        {
            const string dev = "D2A";
            await server.RegisterDeviceAsync(Config.Tenant, dev);
            var g = await server.GrantRangeAsync(Config.Tenant, dev, 20000);
            var p = Db(dev); LocalStore.Delete(p);
            using var d = PosDevice.Open(p, dev, Config.Tenant);
            d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
            decimal expected = 0m;
            for (int i = 0; i < 350; i++) expected += d.RecordSale(P1_LocalStore.Basket).TotalGross;

            var client = new SyncClient(d, server);
            var r = await client.RunAsync();
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var chain = await Verifier.VerifyBookAsync(Config.Admin, $"POS:{dev}");
            var cash = await Verifier.AccountBalanceAsync(Config.Admin, PosDevice.AccCash);
            var mult = await Verifier.PostingMultiplicityAsync(Config.Admin);
            var pend = client.PendingCount();
            var ok = r.Posted == 350 && tb.Balanced && chain.Ok && cash == expected && mult.MaxTimes == 1 && pend == 0;
            return (ok, $"{r}\ntrial balance: {tb} ⇒ {(tb.Balanced ? "EXACT" : "OUT OF BALANCE")}\n" +
                        $"ledger chain : {chain.Reason}\n" +
                        $"cash account : {Money.Canonical(cash)} vs expected {Money.Canonical(expected)}\n" +
                        $"max postings per idempotency key = {mult.MaxTimes} (must be 1), device pending = {pend}");
        });

        await Proof.RunAsync("2-ب", "قتل العميل (SIGKILL) في منتصف المزامنة ثم استئنافه: مرة واحدة بالضبط", async () =>
        {
            const string dev = "D2B";
            await server.RegisterDeviceAsync(Config.Tenant, dev);
            var g = await server.GrantRangeAsync(Config.Tenant, dev, 20000);
            var p = Db(dev); LocalStore.Delete(p);

            var synced = await KillChildAsync(exePath,
                $"--child=syncrun --db={p} --device={dev} --count=300 --rangestart={g.Start} --rangesize=20000",
                killAfterSyncedLines: 6);

            var afterKill = await Sql.ScalarAsync<long>(Config.Admin,
                $"select count(*) from pos.sale_inbox where device_id = '{dev}'");

            // إقلاع جديد للجهاز نفسه على الملف نفسه: كل ما كان inflight يعود pending
            using var d = PosDevice.Open(p, dev, Config.Tenant);
            var client = new SyncClient(d, server);
            var recovered = client.RecoverInflight();
            var r = await client.RunAsync();

            var total = await Sql.ScalarAsync<long>(Config.Admin,
                $"select count(*) from pos.sale_inbox where device_id = '{dev}'");
            var entries = await Sql.ScalarAsync<long>(Config.Admin,
                $"select count(*) from ledger.journal_entry where book_id = 'POS:{dev}' and source_idem_key not like '%#cogs'");
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var chain = await Verifier.VerifyBookAsync(Config.Admin, $"POS:{dev}");
            var localCount = d.Store.Scalar<long>("select count(*) from sale");
            var ok = total == 300 && entries == 300 && localCount == 300 && tb.Balanced && chain.Ok && client.PendingCount() == 0;
            return (ok,
                $"child synced {synced} entries then was SIGKILLed mid-run; server held {afterKill} of 300\n" +
                $"restart: {recovered} entries were 'inflight' with unknown fate → returned to the queue and re-sent\n" +
                $"resume : {r}\n" +
                $"server inbox = {total}/300, ledger entries = {entries}/300, device rows = {localCount}\n" +
                $"trial balance: {tb}\nledger chain : {chain.Reason}\n" +
                "ملاحظة جوهرية: الجهاز لا يعرف — ولا يمكنه أن يعرف — مصير ما كان في الطريق.\n" +
                "لهذا الحصانة لكل قيد ليست تحسيناً بل شرط صحّة للبروتوكول.");
        });

        await Proof.RunAsync("2-ج", "عطل نقل في منتصف الدفعة: لا فقدان، والاستئناف يعطي مرة واحدة بالضبط", async () =>
        {
            const string dev = "D2C";
            await server.RegisterDeviceAsync(Config.Tenant, dev);
            var g = await server.GrantRangeAsync(Config.Tenant, dev, 20000);
            var p = Db(dev); LocalStore.Delete(p);
            using var d = PosDevice.Open(p, dev, Config.Tenant);
            d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
            for (int i = 0; i < 120; i++) d.RecordSale(P1_LocalStore.Basket);

            int failures = 0;
            server.FailAfter = (_, i) => { if (i == 7 && failures < 3) { failures++; return true; } return false; };
            var client = new SyncClient(d, server) { HardAttemptCap = 2 };
            var r1 = await client.RunAsync();
            var midway = await Sql.ScalarAsync<long>(Config.Admin, $"select count(*) from pos.sale_inbox where device_id = '{dev}'");
            server.FailAfter = null;
            client.RecoverInflight();
            var r2 = await client.RunAsync();

            var total = await Sql.ScalarAsync<long>(Config.Admin, $"select count(*) from pos.sale_inbox where device_id = '{dev}'");
            var entries = await Sql.ScalarAsync<long>(Config.Admin,
                $"select count(*) from ledger.journal_entry where book_id = 'POS:{dev}' and source_idem_key not like '%#cogs'");
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var ok = total == 120 && entries == 120 && tb.Balanced;
            return (ok, $"transport cut after 6 of each batch, {failures} times\nrun 1: {r1}\n" +
                        $"server held {midway}/120 after the failed run\nrun 2 (resume): {r2}\n" +
                        $"final: inbox {total}/120, ledger entries {entries}/120, duplicates ack'd = {r2.Duplicate}\n" +
                        $"trial balance: {tb}");
        });

        await Proof.RunAsync("2-د", "الضغط العكسي: 40 جهازاً على خادم سعته 4 — رفض مبكّر لا تكديس، وبلا فقدان", async () =>
        {
            var small = new SyncServer(Config.Admin, maxConcurrent: 4);
            var devices = new List<PosDevice>();
            var clients = new List<SyncClient>();
            for (int i = 0; i < 40; i++)
            {
                var dev = $"D2D{i:00}";
                await small.RegisterDeviceAsync(Config.Tenant, dev);
                var g = await small.GrantRangeAsync(Config.Tenant, dev, 1000);
                var p = Db(dev); LocalStore.Delete(p);
                var d = PosDevice.Open(p, dev, Config.Tenant);
                d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
                for (int k = 0; k < 25; k++) d.RecordSale(P1_LocalStore.Basket);
                devices.Add(d); clients.Add(new SyncClient(d, small));
            }
            var sw = Stopwatch.StartNew();
            var results = await Task.WhenAll(clients.Select(c => c.RunAsync()));
            sw.Stop();
            var posted = results.Sum(r => r.Posted);
            var maxInflightOk = small.Inflight == 0;
            var inbox = await Sql.ScalarAsync<long>(Config.Admin,
                "select count(*) from pos.sale_inbox where device_id like 'D2D%'");
            var pending = clients.Sum(c => c.PendingCount());
            foreach (var d in devices) d.Dispose();
            var ok = posted == 1000 && inbox == 1000 && pending == 0;
            return (ok,
                $"40 devices × 25 sales = 1,000 entries against a server admitting {small.MaxConcurrent} concurrent batches\n" +
                $"posted = {posted}, server inbox = {inbox}, still pending on devices = {pending}\n" +
                $"batches shed with a retry-after hint = {small.ShedCount}; wall time {sw.Elapsed.TotalSeconds:F2} s\n" +
                $"final inflight = {small.Inflight} ⇒ {(maxInflightOk ? "drained cleanly" : "LEAK")}\n" +
                "الرفض المبكّر مع مهلة إعادة محاولة يحمي الخادم؛ التكديس غير المحدود يحوّل\n" +
                "ذروة العودة من الانقطاع إلى انهيار كامل بدل تباطؤ محتمَل.");
        });

        await Proof.RunAsync("2-هـ", "قيد مسموم: معاملة لكل دفعة تُسقط 99 قيداً سليماً، ومعاملة لكل قيد لا تفعل", async () =>
        {
            const string devA = "D2EA", devB = "D2EB";
            var lines = new List<string>();
            long postedBatch = 0, postedEntry = 0;
            foreach (var (dev, mode) in new[] { (devA, BatchTxMode.PerBatch), (devB, BatchTxMode.PerEntry) })
            {
                await server.RegisterDeviceAsync(Config.Tenant, dev);
                var g = await server.GrantRangeAsync(Config.Tenant, dev, 20000);
                var p = Db(dev); LocalStore.Delete(p);
                using var d = PosDevice.Open(p, dev, Config.Tenant);
                d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
                for (int i = 0; i < 100; i++) d.RecordSale(P1_LocalStore.Basket);

                var client = new SyncClient(d, server);
                var batch = client.NextBatch(100);
                // تسميم القيد رقم 50: سطور غير متوازنة (كما لو أفسدها خلل أو عبث)
                var poisoned = batch[49] with
                {
                    JournalLines = [.. batch[49].JournalLines.Select((j, i) => i == 0 ? j with { Debit = "999999.0000" } : j)]
                };
                batch[49] = poisoned;

                server.TxMode = mode;
                var resp = await server.SyncAsync(new SyncBatch(Config.Tenant, dev,
                    Guid.CreateVersion7().ToString("N"), d.Clock.WallUtcNow, batch));
                var got = await Sql.ScalarAsync<long>(Config.Admin, $"select count(*) from pos.sale_inbox where device_id = '{dev}'");
                if (mode == BatchTxMode.PerBatch) postedBatch = got; else postedEntry = got;
                lines.Add($"{mode,-8}: accepted={resp.Accepted}, per-entry acks={resp.Acks.Count}, entries landed = {got}/100" +
                          (resp.Accepted ? "" : $"\n          reason: {resp.RejectReason.Split('\n')[0]}"));
            }
            server.TxMode = BatchTxMode.PerEntry;
            var ok = postedBatch == 0 && postedEntry == 99;
            return (ok, string.Join('\n', lines) + "\n" +
                "قيد واحد فاسد أسقط الدفعة كلها تحت معاملة واحدة، والجهاز سيعيد إرسالها إلى الأبد:\n" +
                "انسداد رأس الطابور — 99 عملية بيع سليمة محتجزة بسبب واحدة.\n" +
                "والنتيجة الأخطر التي لم تكن في الحسبان: مشغّل التوازن DEFERRABLE INITIALLY DEFERRED\n" +
                "يعمل عند COMMIT، فالخطأ لا يظهر عند القيد الجاني بل بعد قبول المئة كلها ظاهرياً،\n" +
                "ولا يسمّي أيّها الجاني. أي أن الجمع بين «قيد مؤجَّل» و«معاملة لكل دفعة» يُنتج فشلاً\n" +
                "غير قابل للتشخيص أصلاً — لا مجرد فشل مكلف.\n" +
                "معاملة لكل قيد ترفض الجاني وحده وتُرحّل الباقي، وتعطي إقراراً لكل قيد على حدة.");
        });
    }

    /// <summary>يقتل العملية الابن بعد عدد أسطر SYNCED؛ يعيد عدد القيود المُزامَنة المُبلَّغ عنها.</summary>
    private static async Task<int> KillChildAsync(string exePath, string args, int killAfterSyncedLines)
    {
        var psi = new ProcessStartInfo("dotnet", $"{exePath} {args}")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var proc = Process.Start(psi)!;
        int lines = 0, total = 0;
        string? l;
        while ((l = await proc.StandardOutput.ReadLineAsync()) is not null)
        {
            if (l.StartsWith("SYNCED ")) { lines++; total += int.Parse(l[7..]); }
            if (lines >= killAfterSyncedLines) { try { proc.Kill(entireProcessTree: true); } catch { } break; }
        }
        await proc.WaitForExitAsync();
        return total;
    }
}
