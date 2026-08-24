using System.Diagnostics;
using BabelPosOffline.Device;
using BabelPosOffline.Support;
using Microsoft.Data.Sqlite;

namespace BabelPosOffline.Proofs;

/// <summary>(1) المخزن المحلي: الديمومة، الحصانة، التوازن، ولا float في أي مكان.</summary>
public static class P1_LocalStore
{
    private static string Db(string name) => Path.Combine(Config.DeviceDir, $"{name}.sqlite");

    public static PosDevice NewDevice(string name, DeviceClock? clock = null, PosSettings? s = null,
                                      long rangeStart = 1, long rangeSize = 5000, bool fullSync = true)
    {
        var p = Db(name);
        LocalStore.Delete(p);
        var d = PosDevice.Open(p, name, Config.Tenant, clock, s, fullSync);
        d.InstallRange($"R-{name}", rangeStart, rangeStart + rangeSize - 1);
        d.OpenShift($"SH-{name}");
        return d;
    }

    public static readonly SaleItem[] Basket =
    [
        new("ITM-COFFEE", 2m, 12.5000m, 0.15m),
        new("ITM-CAKE",   1m, 23.0000m, 0.15m)
    ];

    public static async Task RunAsync(string exePath)
    {
        Proof.Section("(1) المخزن المحلي على الجهاز — SQLite بنمط WAL و synchronous=FULL");

        Proof.Run("1-أ", "الإعدادات الفعلية: WAL + synchronous=FULL + مفاتيح أجنبية مفعّلة", () =>
        {
            using var d = NewDevice("P1A");
            var jm = d.Store.Pragma("journal_mode");
            var sy = d.Store.Scalar<long>("pragma synchronous;");
            var fk = d.Store.Scalar<long>("pragma foreign_keys;");
            var ok = jm.Equals("wal", StringComparison.OrdinalIgnoreCase) && sy == 2 && fk == 1;
            return (ok, $"journal_mode={jm}, synchronous={sy} (2 = FULL ⇒ fsync عند كل COMMIT), foreign_keys={fk}\n" +
                        "synchronous=FULL هو الفارق بين النجاة من انهيار العملية والنجاة من انقطاع الكهرباء.");
        });

        Proof.Run("1-ب", "لا float في المخزن المحلي — والفخّ ليس حيث يُتوقَّع", () =>
        {
            var p = Db("P1B"); LocalStore.Delete(p);
            using var st = new LocalStore(p);

            // (1) الاختبار الساذج ينجح، وهذا هو الفخّ: قيمة بمقياس 4 تحت 10^8
            //     تدور ذهاباً وإياباً عبر double بلا فقدان، لأن 53 بتاً تكفيها.
            st.Exec("create table t_real (v real);");
            int rtFail = 0; var rnd = new Random(7);
            for (int i = 0; i < 20000; i++)
            {
                var v = new decimal(rnd.Next(1, 999_999_99)) / 10000m;
                st.Exec("delete from t_real; insert into t_real (v) values ($v)", ("$v", (double)v));
                if ((decimal)st.Scalar<double>("select v from t_real") != v) rtFail++;
            }

            // (2) وجمع SQLite يستخدم تعويض كاهان، فحتى المجاميع تنجو غالباً
            st.Exec("delete from t_real;");
            decimal exact = 0m;
            foreach (var v in new[] { 19.9900m, 4.7500m, 0.3500m, 120.4000m, 7.1500m })
                for (int k = 0; k < 300; k++) { st.Exec("insert into t_real (v) values ($v)", ("$v", (double)v)); exact += v; }
            var sumOk = (decimal)st.Scalar<double>("select sum(v) from t_real") == exact;

            // (3) لكن المقارنة تنهار: وهذا يُبطل كل قيد قائم على المساواة
            var eq = st.Scalar<long>("select 0.1 + 0.2 = 0.3");

            // (4) والحساب ينهار: ضريبة القيمة المضافة على أسعار حقيقية تنحرف 0.0001 ريال
            decimal[] nets = [2.6750m, 1.0050m, 0.6150m, 12.3500m, 8.9000m, 3.5670m];
            var diverge = new List<string>();
            foreach (var n in nets)
            {
                var dec = decimal.Round(n * 0.15m, 4, MidpointRounding.AwayFromZero);
                var dbl = (decimal)Math.Round((double)n * 0.15d, 4, MidpointRounding.AwayFromZero);
                if (dec != dbl) diverge.Add($"net {Money.Canonical(n)} × 15% ⇒ decimal {Money.Canonical(dec)} vs double {Money.Canonical(dbl)} (Δ {Money.Canonical(dec - dbl)})");
            }

            var ok = rtFail == 0 && eq == 0 && diverge.Count > 0;
            return (ok,
                $"(1) round-trip decimal→REAL→decimal over 20,000 four-dp values: {rtFail} losses ⇒ النقل وحده لا يفقد شيئاً\n" +
                $"(2) SQLite sum() over 1,500 REAL money values equals the decimal sum: {sumOk} (تعويض كاهان)\n" +
                $"(3) SQLite: 0.1 + 0.2 = 0.3 ⇒ {(eq == 1 ? "TRUE" : "FALSE")}  ⇒ كل قيد قائم على المساواة يصبح غير موثوق\n" +
                $"(4) VAT في double ينحرف عن decimal في {diverge.Count} من {nets.Length} حالة سعر واقعية:\n    " +
                string.Join("\n    ", diverge) + "\n" +
                "النتيجة السلبية المهمة: خطأ الـfloat في نقطة البيع لا يظهر في اختبار التخزين\n" +
                "الذي يكتبه المطوّر عادةً؛ يظهر في سطر الضريبة وفي شرط المساواة. لذلك المنع بنيوي:\n" +
                "أعداد صحيحة بوحدات صغرى في SQLite، وdecimal في C#، وNUMERIC(19,4) في PostgreSQL،\n" +
                "ونصّ بمقياس ثابت في حمولة المزامنة — ويُفرض ذلك بـtypeof() في المحرّك لا بالمراجعة.");
        });

        Proof.Run("1-ج", "المشغّل يرفض قيداً غير متوازن عند الختم، مهما كان مسار الشيفرة", () =>
        {
            using var d = NewDevice("P1C");
            d.RecordSale(Basket);
            // كتابة خام تتجاوز PosDevice تماماً — كما لو فعلها سكربت أو مطوّر
            var saleId = Guid.CreateVersion7().ToString("D");
            d.Store.Exec("""
                insert into sale (sale_id, idem_key, device_id, doc_type, invoice_no, chain_seq, business_date,
                    device_clock_at, monotonic_ms, boot_id, shift_id, currency, total_net_minor, total_vat_minor,
                    total_gross_minor, prev_hash, entry_hash, payload_hash, sealed)
                values ($s,'raw-1','P1C','SALE',999999,999999,'2026-01-01','2026-01-01T00:00:00.000000Z',0,'b','s','SAR',
                        1000000,150000,1150000,'00','00','00',0)
                """, ("$s", saleId));
            d.Store.Exec("insert into sale_line values ($s,1,'X',1000,1000000,1000000,150000)", ("$s", saleId));
            d.Store.Exec("insert into journal_line values ($s,1,'1101',1150000,0)", ("$s", saleId));
            d.Store.Exec("insert into journal_line values ($s,2,'4101',0,1000000)", ("$s", saleId));   // ناقص 150000
            string msg;
            try { d.Store.Exec("update sale set sealed = 1 where sale_id = $s", ("$s", saleId)); msg = "NOT REJECTED (bad)"; }
            catch (SqliteException ex) { msg = ex.Message.Split('\n')[0]; }
            var ok = msg.Contains("UNBALANCED_ENTRY");
            return (ok, $"raw write bypassing all C# code → seal rejected by the engine:\n{msg}");
        });

        Proof.Run("1-د", "عملية مختومة غير قابلة للتعديل ولا للحذف على الجهاز", () =>
        {
            using var d = NewDevice("P1D");
            var s = d.RecordSale(Basket);
            string upd, del, delLine;
            try { d.Store.Exec("update sale set total_gross_minor = 1 where sale_id = $s", ("$s", s.SaleId)); upd = "ALLOWED (bad)"; }
            catch (SqliteException ex) { upd = ex.Message.Split('\n')[0]; }
            try { d.Store.Exec("delete from sale where sale_id = $s", ("$s", s.SaleId)); del = "ALLOWED (bad)"; }
            catch (SqliteException ex) { del = ex.Message.Split('\n')[0]; }
            try { d.Store.Exec("delete from journal_line where sale_id = $s", ("$s", s.SaleId)); delLine = "ALLOWED (bad)"; }
            catch (SqliteException ex) { delLine = ex.Message.Split('\n')[0]; }
            var ok = upd.Contains("IMMUTABLE") && del.Contains("UNDELETABLE") && delLine.Contains("UNDELETABLE");
            return (ok, $"UPDATE sale        → {upd}\nDELETE sale        → {del}\nDELETE journal_line→ {delLine}\n" +
                        "التصحيح بقيد عكسي فقط — وهذا يسري على البيع دون اتصال كما على أي قيد آخر.\n" +
                        "اعتراف صريح: هذه مشغّلات لا صلاحيات. من يملك ملف SQLite يستطيع نزع المشغّل؛\n" +
                        "لذلك الاكتشاف الحقيقي يقع على الخادم عبر سلسلة التجزئة، لا على الجهاز.");
        });

        await Proof.RunAsync("1-هـ", "انهيار العملية (SIGKILL) أثناء الكتابة: لا فقدان ولا فجوة ولا قيد نصف مكتوب", async () =>
        {
            var p = Db("P1E"); LocalStore.Delete(p);
            var reported = await CrashChildAsync(exePath, $"--child=write --db={p} --device=P1E --count=400", killAfterLines: 150);
            using var st = new LocalStore(p);
            var count = (int)st.Scalar<long>("select count(*) from sale");
            var unsealed = (int)st.Scalar<long>("select count(*) from sale where sealed = 0");
            var orphanLines = (int)st.Scalar<long>("select count(*) from journal_line where sale_id not in (select sale_id from sale)");
            var chain = DeviceVerifier.VerifyChain(st, Config.Tenant, "P1E");
            var gaps = DeviceVerifier.VerifyNoGaps(st);
            var bal = DeviceVerifier.VerifyBalances(st);
            var nextNo = st.Scalar<long>("select next_no from device_counter where singleton = 1");
            var maxNo = st.Scalar<long>("select coalesce(max(invoice_no),0) from sale");
            var counterOk = nextNo == maxNo + 1;
            var ok = count >= reported && unsealed == 0 && orphanLines == 0 && chain.Ok && gaps.Ok && bal.Ok && counterOk;
            return (ok,
                $"child reported {reported} commits before SIGKILL; the file holds {count} sealed sales\n" +
                $"unsealed rows = {unsealed}, orphan journal lines = {orphanLines}\n" +
                $"counter next_no = {nextNo}, max invoice_no = {maxNo} ⇒ {(counterOk ? "consistent" : "INCONSISTENT")}\n" +
                $"chain: {chain.Reason}\ngaps : {gaps.Reason}\nbalance: {bal.Reason}");
        });

        Proof.Run("1-و", "سعة 24 ساعة تداول (1,500 عملية) وثلاثة أيام (4,500)", () =>
        {
            var p = Db("P1F"); LocalStore.Delete(p);
            using var d = PosDevice.Open(p, "P1F", Config.Tenant);
            d.InstallRange("R", 1, 20000); d.OpenShift();
            var sw = Stopwatch.StartNew();
            for (int i = 0; i < 1500; i++) d.RecordSale(Basket);
            var t24 = sw.Elapsed;
            var size24 = FileSize(p);
            for (int i = 0; i < 3000; i++) d.RecordSale(Basket);
            sw.Stop();
            var size72 = FileSize(p);
            var ok = t24.TotalSeconds < 120;
            return (ok,
                $"1,500 sales (24 h of one lane): {t24.TotalSeconds:F1} s wall, {1500 / t24.TotalSeconds:F0} sales/s, file {size24 / 1024.0:F0} KiB\n" +
                $"4,500 sales (72 h)            : {sw.Elapsed.TotalSeconds:F1} s wall, file {size72 / 1024.0:F0} KiB\n" +
                $"⇒ نحو {size24 / 1500.0:F0} بايت لكل عملية؛ 24 ساعة ≈ {size24 / 1024.0:F0} KiB. السعة ليست قيداً إطلاقاً — القيد تنظيمي لا تخزيني.");
        });

        Proof.Run("1-ز", "ثمن الديمومة: قياس FULL مقابل NORMAL مقابل OFF (وهو فخّ إعداد حقيقي)", () =>
        {
            var res = new List<string>();
            double fullTps = 0, normalTps = 0;
            foreach (var (label, full) in new[] { ("synchronous=FULL", true), ("synchronous=NORMAL", false) })
            {
                var p = Db($"P1G-{full}"); LocalStore.Delete(p);
                using var d = PosDevice.Open(p, "P1G", Config.Tenant, fullSync: full);
                d.InstallRange("R", 1, 5000); d.OpenShift();
                var sw = Stopwatch.StartNew();
                for (int i = 0; i < 300; i++) d.RecordSale(Basket);
                sw.Stop();
                var tps = 300 / sw.Elapsed.TotalSeconds;
                if (full) fullTps = tps; else normalTps = tps;
                res.Add($"{label,-20} {tps,8:F0} sales/s");
            }
            var ok = fullTps > 20;   // يجب أن يبقى بعيداً جداً عن 1500 عملية/يوم ≈ 0.017 عملية/ث
            return (ok, string.Join('\n', res) + "\n" +
                $"النسبة: NORMAL أسرع بـ{normalTps / fullTps:F1}× — وهذا بالضبط إغراء الإعداد الخاطئ.\n" +
                "تحت WAL، synchronous=NORMAL يعني أن معاملة مُثبَّتة قد تضيع عند انقطاع الكهرباء\n" +
                "(لا عند انهيار العملية). لجهاز نقطة بيع يحمل نقداً محصَّلاً هذا غير مقبول.\n" +
                $"حتى FULL يعطي {fullTps:F0} عملية/ث، أي ~{fullTps * 3600:N0} في الساعة مقابل حاجة فعلية 60/ساعة.");
        });

        Proof.Run("1-ح", "أمانة القياس: هل يقول نظام الملفات الحقيقة عن fsync؟", () =>
        {
            var p = Db("P1H"); LocalStore.Delete(p);
            using var st = new LocalStore(p);
            var fs = ReadMountFs(Config.DeviceDir);
            return (true,
                $"device dir = {Config.DeviceDir}, filesystem = {fs}\n" +
                "تحفّظ صريح: SIGKILL يثبت النجاة من انهيار العملية فقط. النجاة من انقطاع الكهرباء\n" +
                "تعتمد على أن fsync لا يكذب — وهذا خارج سيطرة الشيفرة (طبقة الحاوية، ذاكرة القرص\n" +
                "المؤقتة، وحدة SSD رخيصة بلا حماية طاقة). القرار التشغيلي المقابل: بطارية/UPS صغيرة\n" +
                "لكل جهاز نقطة بيع، وهي أرخص بكثير من التحقيق في فاتورة مفقودة.");
        });
    }

    public static long FileSize(string p) =>
        new[] { p, p + "-wal", p + "-shm" }.Where(File.Exists).Sum(f => new FileInfo(f).Length);

    private static string ReadMountFs(string dir)
    {
        try
        {
            var best = ""; var bestLen = -1;
            foreach (var line in File.ReadLines("/proc/mounts"))
            {
                var parts = line.Split(' ');
                if (parts.Length < 3) continue;
                if (dir.StartsWith(parts[1]) && parts[1].Length > bestLen) { bestLen = parts[1].Length; best = $"{parts[2]} at {parts[1]}"; }
            }
            return best;
        }
        catch { return "unknown"; }
    }

    /// <summary>يشغّل عملية ابن ويقتلها بـSIGKILL بعد عدد أسطر تقدُّم؛ يعيد عدد الالتزامات المُبلَّغ عنها.</summary>
    public static async Task<int> CrashChildAsync(string exePath, string args, int killAfterLines)
    {
        var psi = new ProcessStartInfo("dotnet", $"{exePath} {args}")
        { RedirectStandardOutput = true, RedirectStandardError = true, UseShellExecute = false };
        using var proc = Process.Start(psi)!;
        int lines = 0; int last = 0;
        var reader = Task.Run(async () =>
        {
            string? l;
            while ((l = await proc.StandardOutput.ReadLineAsync()) is not null)
            {
                if (l.StartsWith("WROTE ")) { lines++; last = int.Parse(l[6..]); }
                if (lines >= killAfterLines) { try { proc.Kill(entireProcessTree: true); } catch { } break; }
            }
        });
        await reader;
        await proc.WaitForExitAsync();
        return last;
    }
}
