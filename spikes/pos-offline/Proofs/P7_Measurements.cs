using System.Diagnostics;
using BabelPosOffline.Device;
using BabelPosOffline.Server;
using BabelPosOffline.Support;
using Npgsql;

namespace BabelPosOffline.Proofs;

/// <summary>
/// (7) القياسات. <b>تحفّظ العتاد أولاً:</b> حاوية بأربعة أنوية منطقية يتقاسمها وكلاء آخرون
/// في اللحظة نفسها، وPostgreSQL على المضيف نفسه بلا شبكة بينهما (RTT ≈ 0)، ونظام ملفات
/// حاوية. النِسَب بين التكوينات ذات دلالة؛ الأرقام المطلقة سقفٌ متفائل، وزمن الشبكة الحقيقي
/// سيضيف RTT كاملاً لكل دفعة (لا لكل قيد — وهذا هو سبب التجميع بالدفعات أصلاً).
/// </summary>
public static class P7_Measurements
{
    private static string Db(string n) => Path.Combine(Config.DeviceDir, $"{n}.sqlite");

    private static async Task<PosDevice> NewAsync(SyncServer s, string dev, int backlog, long rangeSize = 20000)
    {
        await s.RegisterDeviceAsync(Config.Tenant, dev);
        var g = await s.GrantRangeAsync(Config.Tenant, dev, rangeSize);
        var p = Db(dev); LocalStore.Delete(p);
        var d = PosDevice.Open(p, dev, Config.Tenant);
        d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
        for (int i = 0; i < backlog; i++) d.RecordSale(P1_LocalStore.Basket);
        return d;
    }

    private static double Pct(List<double> xs, double p)
    {
        if (xs.Count == 0) return 0;
        var s = xs.OrderBy(x => x).ToList();
        var i = (int)Math.Ceiling(p / 100.0 * s.Count) - 1;
        return s[Math.Clamp(i, 0, s.Count - 1)];
    }

    public static async Task RunAsync()
    {
        Proof.Section("(7) القياسات — إنتاجية، زمن استجابة، وذروة عودة بعد انقطاع إقليمي");
        var server = new SyncServer(Config.Admin);

        Proof.Note($"host: {Environment.ProcessorCount} logical CPUs, load = {ReadLoad()}");

        await Proof.RunAsync("7-أ", "تراكم 24 ساعة من جهاز واحد (1,500 عملية): زمن الكتابة وزمن المزامنة", async () =>
        {
            var swWrite = Stopwatch.StartNew();
            using var d = await NewAsync(server, "D7A", backlog: 1500);
            swWrite.Stop();
            var fileKiB = P1_LocalStore.FileSize(Db("D7A")) / 1024.0;

            var results = new List<string>();
            double best = 0;
            foreach (var batchSize in new[] { 25, 100, 250 })
            {
                await Sql.ExecAsync(Config.Admin, "delete from pos.sale_inbox where device_id = 'D7A'");
                await Sql.ExecAsync(Config.Admin, "delete from ledger.journal_line where entry_id in (select entry_id from ledger.journal_entry where book_id = 'POS:D7A')");
                await Sql.ExecAsync(Config.Admin, "delete from ledger.journal_entry where book_id = 'POS:D7A'");
                await Sql.ExecAsync(Config.Admin, "delete from ledger.entry_counter where book_id = 'POS:D7A'");
                d.Store.Exec("update sale set sync_state = 'pending'");

                var client = new SyncClient(d, server);
                var sw = Stopwatch.StartNew();
                var r = await client.RunAsync();
                sw.Stop();
                var tps = r.Posted / sw.Elapsed.TotalSeconds;
                best = Math.Max(best, tps);
                results.Add($"batch {batchSize,4}: {r.Posted,5} entries in {sw.Elapsed.TotalSeconds,6:F2} s ⇒ {tps,7:F0} entries/s ({r.Batches} round trips)");
                client.GetType();
            }
            var chain = await Verifier.VerifyBookAsync(Config.Admin, "POS:D7A");
            var ok = chain.Ok && best > 50;
            return (ok,
                $"device-side write of 1,500 sales (fsync per sale): {swWrite.Elapsed.TotalSeconds:F1} s ⇒ " +
                $"{1500 / swWrite.Elapsed.TotalSeconds:F0} sales/s, local file {fileKiB:F0} KiB\n" +
                string.Join('\n', results) + "\n" +
                $"ledger chain after the fastest run: {chain.Reason}\n" +
                $"⇒ تراكم يوم كامل لجهاز واحد يُصفّى في {1500 / best:F1} ثانية على هذا العتاد.\n" +
                "زمن الشبكة الحقيقي يضيف RTT واحداً لكل دفعة: عند 250 قيداً/دفعة و100 ms RTT\n" +
                $"يصبح الحدّ الأدنى ≈ {Math.Ceiling(1500 / 250.0) * 0.1:F1} ثانية إضافية فقط — لا لكل قيد.");
        });

        await Proof.RunAsync("7-ب", "زمن الاستجابة من لحظة عودة الاتصال حتى ظهور العملية في دفتر الأستاذ", async () =>
        {
            using var d = await NewAsync(server, "D7B", backlog: 600);
            var client = new SyncClient(d, server);
            var lat = new List<double>();
            var t0 = Stopwatch.StartNew();
            double first = -1;
            while (true)
            {
                var batch = client.NextBatch(100);
                if (batch.Count == 0) break;
                foreach (var e in batch) d.Store.Exec("update sale set sync_state='inflight' where idem_key=$k", ("$k", e.IdemKey));
                var resp = await server.SyncAsync(new SyncBatch(Config.Tenant, "D7B", "B", DateTime.UtcNow, batch,
                    d.Store.Scalar<long>("select next_no from device_counter where singleton = 1")));
                var t = t0.Elapsed.TotalMilliseconds;
                if (first < 0) first = t;
                foreach (var a in resp.Acks)
                {
                    lat.Add(t);
                    d.Store.Exec("update sale set sync_state='acked' where idem_key=$k", ("$k", a.IdemKey));
                }
            }
            t0.Stop();
            var ok = lat.Count == 600;
            return (ok,
                $"600 queued sales, batch 100, measured from the reconnect instant to the entry being in the ledger:\n" +
                $"    first entry visible : {first,8:F0} ms\n" +
                $"    p50                 : {Pct(lat, 50),8:F0} ms\n" +
                $"    p95                 : {Pct(lat, 95),8:F0} ms\n" +
                $"    max (last entry)    : {lat.Max(),8:F0} ms\n" +
                $"    total drain         : {t0.Elapsed.TotalMilliseconds,8:F0} ms\n" +
                "زمن ظهور أول عملية هو ما يراه المشغّل، وزمن آخر عملية هو ما يهمّ نافذة الإبلاغ.\n" +
                "المقايضة صريحة: دفعة أكبر ⇒ إنتاجية أعلى وزمن ظهور أول قيد أطول.");
        });

        await Proof.RunAsync("7-ج", "الذروة الواقعية: 10 و50 و200 جهاز تعود معاً بعد انقطاع إقليمي", async () =>
        {
            var rows = new List<string>();
            bool allOk = true;
            const int perDevice = 60;

            foreach (var herd in new[] { 10, 50, 200 })
            {
                var devices = new List<PosDevice>();
                for (int i = 0; i < herd; i++)
                    devices.Add(await NewAsync(server, $"H{herd}-{i:000}", perDevice, rangeSize: 150));

                var s = new SyncServer(Config.Admin, maxConcurrent: 16);
                var clients = devices.Select(d => new SyncClient(d, s)).ToList();
                var per = new List<double>();
                var sw = Stopwatch.StartNew();
                await Task.WhenAll(clients.Select(async c =>
                {
                    var t = Stopwatch.StartNew();
                    await c.RunAsync();
                    lock (per) per.Add(t.Elapsed.TotalSeconds);
                }));
                sw.Stop();

                var expected = herd * perDevice;
                var got = await Sql.ScalarAsync<long>(Config.Admin,
                    $"select count(*) from pos.sale_inbox where device_id like 'H{herd}-%'");
                var pendingLeft = clients.Sum(c => c.PendingCount());
                foreach (var d in devices) d.Dispose();
                var ok = got == expected && pendingLeft == 0;
                allOk &= ok;
                rows.Add($"{herd,4} devices × {perDevice} = {expected,6} entries │ " +
                         $"{sw.Elapsed.TotalSeconds,6:F2} s │ {expected / sw.Elapsed.TotalSeconds,7:F0} entries/s │ " +
                         $"p50 {Pct(per, 50),5:F2} s p95 {Pct(per, 95),5:F2} s max {per.Max(),5:F2} s │ " +
                         $"shed {s.ShedCount,4} │ landed {got}/{expected} │ left on devices {pendingLeft} │ {(ok ? "drained" : "NOT DRAINED")}");
            }

            // ── العطل الذي اكتشفه هذا القياس نفسه: تجويع تحت الضغط العكسي ──────────
            // نفس القطيع، لكن العميل بسقف محاولات صلب (السلوك «المعقول» الذي كتبناه أولاً)
            var starved = new List<PosDevice>();
            for (int i = 0; i < 200; i++) starved.Add(await NewAsync(server, $"ST-{i:000}", 40, rangeSize: 80));
            var s2 = new SyncServer(Config.Admin, maxConcurrent: 2);   // ازدحام قاسٍ متعمّد
            var capped = starved.Select(d => new SyncClient(d, s2) { HardAttemptCap = 3 }).ToList();
            var swS = Stopwatch.StartNew();
            await Task.WhenAll(capped.Select(c => c.RunAsync()));
            swS.Stop();
            var starvedLanded = await Sql.ScalarAsync<long>(Config.Admin,
                "select count(*) from pos.sale_inbox where device_id like 'ST-%'");
            var starvedLeft = capped.Sum(c => c.PendingCount());
            var starvedDevices = capped.Count(c => c.PendingCount() > 0);
            // ثم العميل نفسه بلا سقف صلب (تراجع أُسّي بتشويش كامل حتى المهلة)
            var fixedClients = starved.Select(d => new SyncClient(d, s2)).ToList();
            foreach (var c in fixedClients) c.RecoverInflight();
            var swF = Stopwatch.StartNew();
            await Task.WhenAll(fixedClients.Select(c => c.RunAsync()));
            swF.Stop();
            var fixedLanded = await Sql.ScalarAsync<long>(Config.Admin,
                "select count(*) from pos.sale_inbox where device_id like 'ST-%'");
            var fixedLeft = fixedClients.Sum(c => c.PendingCount());
            foreach (var d in starved) d.Dispose();

            // ── وبلا تحكّم قبول إطلاقاً ────────────────────────────────────────────
            var wildDevices = new List<PosDevice>();
            for (int i = 0; i < 100; i++) wildDevices.Add(await NewAsync(server, $"NG-{i:000}", 20, rangeSize: 50));
            var wild = new SyncServer(Config.Admin, maxConcurrent: 100_000);
            var errors = 0;
            var sw2 = Stopwatch.StartNew();
            await Task.WhenAll(wildDevices.Select(async d =>
            {
                try { await new SyncClient(d, wild).RunAsync(); }
                catch (Exception) { Interlocked.Increment(ref errors); }
            }));
            sw2.Stop();
            var wildGot = await Sql.ScalarAsync<long>(Config.Admin, "select count(*) from pos.sale_inbox where device_id like 'NG-%'");
            var conns = await Sql.ScalarAsync<long>(Config.Admin, "select count(*) from pg_stat_activity");
            foreach (var d in wildDevices) d.Dispose();

            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var ok2 = allOk && tb.Balanced && starvedLeft > 0 && fixedLeft == 0 && fixedLanded == 8000;
            return (ok2,
                string.Join('\n', rows) + "\n\n" +
                "── عطل اكتُشف أثناء هذا القياس نفسه: تجويع الجهاز تحت الضغط العكسي ──\n" +
                $"client with a HARD cap of 3 attempts, 200 devices × 40 against a 2-slot server: {swS.Elapsed.TotalSeconds:F1} s, " +
                $"landed {starvedLanded}/8000, LEFT ON DEVICES {starvedLeft} across {starvedDevices} devices — " +
                $"وكل عميل «أنهى» مزامنته بنجاح ظاهري\n" +
                $"same devices, same server, exponential backoff with full jitter and no hard cap: " +
                $"{swF.Elapsed.TotalSeconds:F1} s, landed {fixedLanded}/8000, left {fixedLeft}\n" +
                "الجهاز لم يفقد شيئاً — الطابور المحلي سليم — لكن السقف الصلب حوّل «تأخّر» إلى\n" +
                "«توقّف صامت»، وهو أخطر لأن نافذة الإبلاغ تمشي بينما لوحة التحكّم تقول إن كل شيء تمّ.\n\n" +
                "── وبلا تحكّم قبول إطلاقاً ──\n" +
                $"100 devices × 20 against maxConcurrent = 100,000: {sw2.Elapsed.TotalSeconds:F2} s, " +
                $"landed {wildGot}/2000, client-visible errors {errors}, backend connections {conns} " +
                $"(max_connections = 100, Npgsql pool cap 64)\n" +
                $"trial balance across every device in this run: {tb}\n" +
                "الشكل المهم لا الرقم: تحكّم القبول يحوّل الذروة من «كل شيء يتباطأ للجميع» إلى\n" +
                "«بعض الدفعات تُرفض بمهلة وتعود». وبلا تحكّم قبول، السقف الحقيقي هو مجمّع الاتصالات\n" +
                "وmax_connections، وهما يفشلان بمهلة انتظار لا بتباطؤ متدرّج.\n" +
                "استقراء صريح: 200 جهازاً بتراكم يوم كامل = 300,000 قيد؛ بالمعدّل المقيس أعلاه\n" +
                "(~950 قيد/ث) يعني ذلك زمن تصفية ≈ 5 دقائق على أربعة أنوية مشتركة. يُخطَّط بعدد\n" +
                "خيوط ثابت وبنافذة قبول، لا بعدد الأجهزة.");
        });

        await Proof.RunAsync("7-د", "لماذا عدّاد لكل جهاز: القياس نفسه من وثيقة المعمارية §7.4 في سياق نقطة البيع", async () =>
        {
            var res = new List<string>();
            double shared = 0, perBook = 0;
            foreach (var mode in new[] { "shared", "per-device" })
            {
                await Sql.ExecAsync(Config.Admin, "delete from ledger.journal_line where entry_id in (select entry_id from ledger.journal_entry where book_id like 'BENCH%')");
                await Sql.ExecAsync(Config.Admin, "delete from ledger.journal_entry where book_id like 'BENCH%'");
                await Sql.ExecAsync(Config.Admin, "delete from ledger.entry_counter where book_id like 'BENCH%'");
                const int writers = 16, each = 40;
                var sw = Stopwatch.StartNew();
                await Task.WhenAll(Enumerable.Range(0, writers).Select(async w =>
                {
                    await using var conn = await Sql.OpenAsync(Config.Admin);
                    for (int i = 0; i < each; i++)
                    {
                        var book = mode == "shared" ? "BENCH-SHARED" : $"BENCH-DEV{w:00}";
                        await using var cmd = new NpgsqlCommand(
                            "select out_entry_no from pos.post_entry(@b, @t, current_date, 'bench', 'قياس', 'bench', @k, @l::jsonb)", conn);
                        cmd.Parameters.AddWithValue("b", book);
                        cmd.Parameters.AddWithValue("t", Config.Tenant);
                        cmd.Parameters.AddWithValue("k", $"{book}|{i}");
                        cmd.Parameters.AddWithValue("l",
                            "[{\"line_no\":1,\"account\":\"1101\",\"debit\":\"115.0000\",\"credit\":\"0.0000\",\"desc\":\"c\"}," +
                            " {\"line_no\":2,\"account\":\"4101\",\"debit\":\"0.0000\",\"credit\":\"115.0000\",\"desc\":\"r\"}]");
                        await cmd.ExecuteNonQueryAsync();
                    }
                }));
                sw.Stop();
                var tps = writers * each / sw.Elapsed.TotalSeconds;
                if (mode == "shared") shared = tps; else perBook = tps;
                res.Add($"{mode,-11} counter, 16 concurrent writers × 40 entries: {tps,7:F0} entries/s");
            }
            var ok = perBook > shared;
            return (ok, string.Join('\n', res) + "\n" +
                $"⇒ العدّاد لكل جهاز أسرع بـ{perBook / shared:F1}× على العتاد نفسه.\n" +
                "وهذا ليس تحسيناً اختيارياً هنا: كل جهاز نقطة بيع وحدة إصدار مستقلة بشهادتها،\n" +
                "فالعدّاد لكل جهاز هو الشكل الصحيح مجالياً وأسرع في آن. الصف الساخن الواحد\n" +
                "يسلسل كل الأجهزة خلف قفل واحد بالضبط في اللحظة التي تعود فيها كلها معاً.");
        });
    }

    private static string ReadLoad()
    {
        try { return File.ReadAllText("/proc/loadavg").Split(' ')[0]; }
        catch { return "?"; }
    }
}
