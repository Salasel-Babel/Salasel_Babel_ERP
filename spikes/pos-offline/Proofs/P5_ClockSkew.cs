using System.Globalization;
using BabelPosOffline.Device;
using BabelPosOffline.Server;
using BabelPosOffline.Support;
using Npgsql;

namespace BabelPosOffline.Proofs;

/// <summary>
/// (5) انحراف الساعة. القاعدة المُثبَتة هنا، بثلاث لحظات زمنية متمايزة لا واحدة:
///
///   • <b>device_clock_at</b> — ساعة الجهاز، غير موثوقة، لكنها <b>مدخل تجزئة ثابت لا يتغيّر</b>.
///     تُوثَّق كما ادّعاها الجهاز، ولا تُصحَّح أبداً بعد الختم.
///   • <b>business_date</b> — التاريخ المحاسبي، يُشتق من <b>مرساة الخادم + الساعة الرتيبة</b>،
///     لا من ساعة الحائط إطلاقاً.
///   • <b>server_received_at</b> — الزمن الحُجّة لأثر التدقيق ولنافذة الإبلاغ التنظيمية.
/// </summary>
public static class P5_ClockSkew
{
    private static string Db(string n) => Path.Combine(Config.DeviceDir, $"{n}.sqlite");
    private static string UtcRaw(DateTime d) =>
        d.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ss.fffffffZ", CultureInfo.InvariantCulture);

    private static async Task<PosDevice> NewAsync(SyncServer s, string dev, DeviceClock clock, PosSettings? cfg = null)
    {
        await s.RegisterDeviceAsync(Config.Tenant, dev);
        var g = await s.GrantRangeAsync(Config.Tenant, dev, 5000);
        var p = Db(dev); LocalStore.Delete(p);
        var d = PosDevice.Open(p, dev, Config.Tenant, clock, cfg);
        d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
        return d;
    }

    public static async Task RunAsync()
    {
        Proof.Section("(5) انحراف الساعة — أي زمن هو الحُجّة، ولمن، وكيف تبقى السلسلة قابلة للتحقق");
        var server = new SyncServer(Config.Admin);

        await Proof.RunAsync("5-أ", "جهاز متأخّر 7 ساعات: التاريخ المحاسبي صحيح لأنه من مرساة الخادم لا من ساعة الحائط", async () =>
        {
            var clock = new DeviceClock();
            using var d = await NewAsync(server, "D5A", clock);
            // اتصال أول وهو مضبوط: يثبّت المرساة
            d.RecordSale(P1_LocalStore.Basket);
            await new SyncClient(d, server).RunAsync();

            clock.Step(TimeSpan.FromHours(-7));      // الكاشير يضبط الساعة خطأً
            d.OpenShift();
            var s = d.RecordSale(P1_LocalStore.Basket);
            var truthDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var wallDate = DateOnly.FromDateTime(clock.WallUtcNow);
            var (anchored, isAnchored) = d.ServerEstimatedNow();

            await new SyncClient(d, server).RunAsync();
            var row = await Sql.TableAsync(Config.Admin, $"""
                select invoice_no, business_date, device_clock_at, server_received_at, clock_skew_ms
                from pos.sale_inbox where device_id = 'D5A' order by invoice_no
                """);
            var skew = await Sql.ScalarAsync<long>(Config.Admin,
                "select max(abs(clock_skew_ms)) from pos.sale_inbox where device_id = 'D5A'");
            var raised = await Verifier.OpenExceptionsAsync(Config.Admin, "CLOCK_SKEW");
            var ok = s.BusinessDate == truthDate && isAnchored && skew > 6 * 3600 * 1000;
            return (ok,
                $"device wall clock now says {Canonical.Store(clock.WallUtcNow)} (date {wallDate})\n" +
                $"server-anchored estimate    {Canonical.Store(anchored)} (anchored = {isAnchored})\n" +
                $"business_date recorded on the sale = {s.BusinessDate}, truth = {truthDate} ⇒ " +
                $"{(s.BusinessDate == truthDate ? "CORRECT" : "WRONG")}\n{row}\n" +
                $"max recorded skew = {skew} ms ({skew / 3600000.0:F2} h); CLOCK_SKEW exceptions open = {raised}\n" +
                "ساعة الجهاز تُوثَّق ولا تُصدَّق. الحُجّة للتاريخ المحاسبي هي مرساة الخادم،\n" +
                "والحُجّة لأثر التدقيق ولنافذة الإبلاغ هي server_received_at.");
        });

        await Proof.RunAsync("5-ب", "جهاز متقدّم 30 ساعة: القاعدة تمنع ترحيل إيراد إلى فترة مستقبلية", async () =>
        {
            var clock = new DeviceClock();
            using var d = await NewAsync(server, "D5B", clock);
            d.RecordSale(P1_LocalStore.Basket);
            await new SyncClient(d, server).RunAsync();      // مرساة

            clock.Step(TimeSpan.FromHours(30));
            d.OpenShift();
            var s = d.RecordSale(P1_LocalStore.Basket);
            var wallDate = DateOnly.FromDateTime(clock.WallUtcNow);
            var truth = DateOnly.FromDateTime(DateTime.UtcNow);
            var ok = s.BusinessDate == truth && wallDate > truth;
            return (ok,
                $"naive rule (business date = device wall clock) would book this sale on {wallDate}\n" +
                $"the rule in force (server anchor + monotonic)  books it on {s.BusinessDate}; truth = {truth}\n" +
                $"⇒ فارق {(wallDate.ToDateTime(TimeOnly.MinValue) - truth.ToDateTime(TimeOnly.MinValue)).TotalDays:F0} يوم\n" +
                "لو أُخذ التاريخ من ساعة الجهاز لرُحِّل إيراد إلى فترة قد تكون مقفلة أو غير موجودة بعد،\n" +
                "ولانكسر ميزان المراجعة الشهري بلا سبب ظاهر — وهو خطأ يُكتشَف بعد الإقفال لا قبله.");
        });

        await Proof.RunAsync("5-ج", "السلسلة تبقى قابلة للتحقق: القصّ إلى الميكروثانية على الجهاز قبل التجزئة", async () =>
        {
            var raw = new DateTime(DateTime.UtcNow.Ticks / 10 * 10 + 7, DateTimeKind.Utc);   // 100ns لا تقبل القسمة على 10
            var hashUntruncated = Canonical.Hex(Canonical.HashOf(UtcRaw(raw)));
            var hashTruncated = Canonical.Hex(Canonical.HashOf(Canonical.Utc(raw)));

            await Sql.ExecAsync(Config.Admin, "create table if not exists pos.ts_probe (id int primary key, t timestamptz)");
            await Sql.ExecAsync(Config.Admin, "truncate pos.ts_probe");
            DateTime back;
            await using (var conn = await Sql.OpenAsync(Config.Admin))
            {
                await using (var cmd = new NpgsqlCommand("insert into pos.ts_probe values (1, @t)", conn))
                { cmd.Parameters.AddWithValue("t", raw); await cmd.ExecuteNonQueryAsync(); }
                back = (DateTime)(await Sql.ScalarAsync<DateTime>(conn, "select t from pos.ts_probe where id = 1"))!;
            }
            var hashAfterRoundTrip = Canonical.Hex(Canonical.HashOf(UtcRaw(back)));
            var hashAfterRoundTripTrunc = Canonical.Hex(Canonical.HashOf(Canonical.Utc(back)));

            // وعلى الجهاز فعلياً: سلسلة كاملة مع انحراف ساعة، ثم تحقّق بعد رحلة PostgreSQL
            var clock = new DeviceClock();
            clock.Step(TimeSpan.FromHours(-13));
            using var d = await NewAsync(server, "D5C", clock);
            for (int i = 0; i < 25; i++)
            {
                if (i == 10) clock.Step(TimeSpan.FromHours(19));
                d.RecordSale(P1_LocalStore.Basket);
            }
            var deviceChain = DeviceVerifier.VerifyChain(d.Store, Config.Tenant, "D5C");
            await new SyncClient(d, server).RunAsync();
            var storedClocks = await Sql.ScalarAsync<long>(Config.Admin, """
                select count(*) from pos.sale_inbox si
                where si.device_id = 'D5C'
                  and si.device_clock_at <> date_trunc('microsecond', si.device_clock_at)
                """);

            var ok = hashUntruncated != hashAfterRoundTrip
                     && hashTruncated == hashAfterRoundTripTrunc
                     && deviceChain.Ok && storedClocks == 0;
            return (ok,
                $"device instant with 100-ns precision: {UtcRaw(raw)}\n" +
                $"after a PostgreSQL timestamptz round trip: {UtcRaw(back)}  ← الرقم الأخير سقط\n" +
                $"hash of the untruncated form: {hashUntruncated[..24]}… → after round trip {hashAfterRoundTrip[..24]}… ⇒ " +
                $"{(hashUntruncated == hashAfterRoundTrip ? "same" : "DIFFERENT — السلسلة تصبح غير قابلة للتحقق بعد أول مزامنة")}\n" +
                $"hash of the truncated form  : {hashTruncated[..24]}… → after round trip {hashAfterRoundTripTrunc[..24]}… ⇒ " +
                $"{(hashTruncated == hashAfterRoundTripTrunc ? "IDENTICAL" : "different")}\n" +
                $"device chain over 25 sales with the clock stepped −13 h then +19 h mid-chain: {deviceChain.Reason}\n" +
                $"rows whose stored device_clock_at is not microsecond-aligned: {storedClocks}\n" +
                "لأن اللحظة مدخل تجزئة ثابت، فالانحراف لا يكسر السلسلة أصلاً: نحن نجزّئ ما ادّعاه\n" +
                "الجهاز لا ما هو صحيح. الذي يكسرها هو اختلاف الدقّة بين .NET وPostgreSQL — والقصّ يجب\n" +
                "أن يقع على الجهاز قبل التجزئة، لا على الخادم بعدها.");
        });

        Proof.Run("5-د", "قفزة ساعة للخلف تُخفي تراكماً قديماً — والساعة الرتيبة تكشفه", () =>
        {
            var clock = new DeviceClock();
            var p = Db("D5D"); LocalStore.Delete(p);
            using var d = PosDevice.Open(p, "D5D", Config.Tenant, clock);
            d.InstallRange("R", 500000, 505000); d.OpenShift();
            d.RecordSale(P1_LocalStore.Basket);

            clock.Advance(TimeSpan.FromHours(12));     // مرّت 12 ساعة فعلاً وهو غير متصل
            var honest = d.Backlog();

            clock.Step(TimeSpan.FromHours(-11));       // ثم ضُبطت الساعة للخلف 11 ساعة
            var afterStep = d.Backlog();
            var events = d.Store.Scalar<long>("select count(*) from clock_event");

            var ok = honest.Age.Age.TotalHours >= 11.9
                     && afterStep.Age.WallEstimate.TotalHours < 2
                     && afterStep.Age.Age.TotalHours >= 11.9
                     && afterStep.Age.Confidence == AgeConfidence.Monotonic;
            return (ok,
                $"after 12 real hours offline: {honest.Age}, level = {honest.Level}\n" +
                $"after the wall clock is set back 11 h:\n" +
                $"    naive wall-clock age = {afterStep.Age.WallEstimate.TotalHours:F2} h  ← يقول إن التراكم طازج\n" +
                $"    monotonic age        = {afterStep.Age.MonotonicEstimate.TotalHours:F2} h  ← الحقيقة\n" +
                $"    age used = max(...)  = {afterStep.Age.Age.TotalHours:F2} h, level = {afterStep.Level}\n" +
                $"clock_event rows recorded = {events}\n" +
                "نتيجة سلبية مهمة: جهاز بساعة خاطئة لا يستطيع قياس سقف الـ24 ساعة بساعة الحائط.\n" +
                "وأخذ max(الحائط, الرتيبة) محافظ في الاتجاهين: القفزة للخلف تصحّحها الرتيبة،\n" +
                "والقفزة للأمام تجعلنا نتوقّف مبكّراً — وكلاهما الخطأ الآمن.");
        });

        Proof.Run("5-هـ", "سلوك السقف قابل للضبط: يتوقّف أو يستمر بإنذار — قرار عمل لا ثابت في الكود", () =>
        {
            var lines = new List<string>();
            bool stopOk = false, contOk = false; int flagged = 0;

            foreach (var behaviour in new[] { CeilingBehaviour.StopTrading, CeilingBehaviour.ContinueWithAlarm })
            {
                var clock = new DeviceClock();
                var p = Db($"D5E-{behaviour}"); LocalStore.Delete(p);
                using var d = PosDevice.Open(p, $"D5E{(int)behaviour}", Config.Tenant, clock,
                    PosSettings.Default with { AtCeiling = behaviour });
                d.InstallRange("R", 600000 + (int)behaviour * 10000, 605000 + (int)behaviour * 10000);
                d.OpenShift();
                d.RecordSale(P1_LocalStore.Basket);

                var seen = new List<string>();
                foreach (var h in new[] { 10.0, 15.0, 21.0, 25.0 })
                {
                    clock.Advance(TimeSpan.FromHours(h - (seen.Count == 0 ? 0 : new[] { 10.0, 15.0, 21.0 }[seen.Count - 1])));
                    var b = d.Backlog();
                    seen.Add($"    at {h,4:F0} h → {b.Level,-10} trading = {(b.TradingAllowed ? "allowed" : "BLOCKED")}");
                }
                lines.Add($"policy = {behaviour}:\n" + string.Join('\n', seen));

                if (behaviour == CeilingBehaviour.StopTrading)
                {
                    try { d.RecordSale(P1_LocalStore.Basket); }
                    catch (TradingBlockedException ex) { stopOk = ex.Code == "CEILING_REACHED"; lines.Add($"    ⇒ {ex.Code}: البيع متوقّف"); }
                }
                else
                {
                    var s = d.RecordSale(P1_LocalStore.Basket);
                    contOk = s.PastCeiling;
                    flagged = (int)d.Store.Scalar<long>("select count(*) from sale where past_ceiling = 1");
                    lines.Add($"    ⇒ البيع مستمر، والعملية مُعلَّمة past_ceiling = {s.PastCeiling} (عدد المُعلَّم: {flagged})");
                }
            }
            var ok = stopOk && contOk && flagged == 1;
            return (ok, string.Join('\n', lines) + "\n" +
                "نافذة الإبلاغ 24 ساعة: غير مُتحقَّق منه — موقع الهيئة محجوب عن هذه الشبكة.\n" +
                "لذلك هي إعداد لا ثابت، والإنذار عند 60٪ (14.4 س) والحرج عند 85٪ (20.4 س) إعدادان كذلك.\n" +
                "الافتراضي هو الأكثر تحفّظاً: StopTrading. تحويله إلى ContinueWithAlarm قرار عمل موثّق،\n" +
                "وكل عملية بعده تحمل علامة past_ceiling تنتقل إلى الخادم وتظهر في طابور الاستثناءات.");
        });

        Proof.Run("5-و", "عمر مجهول بعد إعادة إقلاع بساعة مشكوك فيها: يُعامَل كأنه عند السقف", () =>
        {
            var p = Db("D5F"); LocalStore.Delete(p);
            var c1 = new DeviceClock();
            using (var d1 = PosDevice.Open(p, "D5F", Config.Tenant, c1))
            {
                d1.InstallRange("R", 700000, 705000); d1.OpenShift();
                d1.RecordSale(P1_LocalStore.Basket);
                c1.Step(TimeSpan.FromHours(-9));       // ساعة عابثة قبل الإطفاء
                d1.Heartbeat();
            }
            // إقلاع جديد: boot_id جديد، الساعة الرتيبة تصفّرت، والساعة مشكوك فيها
            var c2 = new DeviceClock();
            c2.Step(TimeSpan.FromHours(-9));
            using var d2 = PosDevice.Open(p, "D5F", Config.Tenant, c2);
            var b = d2.Backlog();
            var evs = d2.Store.Query("select kind, detail from clock_event", r => $"{r.GetString(0)}: {r.GetString(1)}");
            var ok = b.Age.Confidence == AgeConfidence.Unknown && !b.TradingAllowed;
            return (ok,
                $"after reboot: {b.Age}\nlevel = {b.Level}, trading = {(b.TradingAllowed ? "allowed" : "BLOCKED")}\n" +
                $"reason: {b.Reason}\nclock events: {string.Join(" | ", evs)}\n" +
                "الساعة الرتيبة تُصفَّر عند الإقلاع، والساعة مشكوك فيها ⇒ العمر غير معلوم.\n" +
                "الخيار المحافظ هو المنع: خطأ «توقّف مبكّراً» قابل للإصلاح بإعادة اتصال،\n" +
                "وخطأ «تاجَر بعد السقف» غير قابل للإصلاح. ويبقى سؤال عمل مفتوح: هل يُقبل\n" +
                "هذا الحسم في فرع نائٍ يفقد الاتصال كل ليلة؟ — انظر الأسئلة المفتوحة.");
        });
    }
}
