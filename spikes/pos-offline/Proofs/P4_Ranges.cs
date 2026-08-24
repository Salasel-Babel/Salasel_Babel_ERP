using BabelPosOffline.Device;
using BabelPosOffline.Server;
using BabelPosOffline.Support;

namespace BabelPosOffline.Proofs;

/// <summary>(4) إدارة المديات المحجوزة: التخصيص، النفاد، استبدال جهاز، وغياب طويل.</summary>
public static class P4_Ranges
{
    private static string Db(string n) => Path.Combine(Config.DeviceDir, $"{n}.sqlite");

    private static async Task<PosDevice> NewAsync(SyncServer s, string dev, long size = 5000)
    {
        await s.RegisterDeviceAsync(Config.Tenant, dev);
        var g = await s.GrantRangeAsync(Config.Tenant, dev, size);
        var p = Db(dev); LocalStore.Delete(p);
        var d = PosDevice.Open(p, dev, Config.Tenant);
        d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
        return d;
    }

    public static async Task RunAsync()
    {
        Proof.Section("(4) المديات المحجوزة — تخصيص، نفاد، استبدال جهاز، غياب طويل، وإثبات الفجوة");
        var server = new SyncServer(Config.Admin);

        await Proof.RunAsync("4-أ", "20 جهازاً: مديات لا تتداخل، والمحرّك نفسه يرفض التداخل", async () =>
        {
            var grants = new List<RangeGrant>();
            for (int i = 0; i < 20; i++)
            {
                var dev = $"D4A{i:00}";
                await server.RegisterDeviceAsync(Config.Tenant, dev);
                grants.Add(await server.GrantRangeAsync(Config.Tenant, dev, 5000));
            }
            var sorted = grants.OrderBy(g => g.Start).ToList();
            var contiguous = sorted.Zip(sorted.Skip(1)).All(p => p.Second.Start == p.First.End + 1);

            // محاولة إدراج مدى متداخل يدوياً — يجب أن يرفضها PostgreSQL نفسه
            var mid = sorted[10];
            var ex = await Sql.ExpectFailureAsync(Config.Admin, $"""
                insert into pos.number_range (range_id, tenant_id, device_id, range_start, range_end, state, granted_at)
                values ('OVERLAP-TEST', '{Config.Tenant}', 'D4A00', {mid.Start + 100}, {mid.End + 100}, 'active', now())
                """);
            var ok = contiguous && ex is not null && ex.SqlState == "23P01";
            return (ok,
                $"20 ranges of 5,000: {sorted[0].Start}..{sorted[^1].End}, contiguous & non-overlapping = {contiguous}\n" +
                $"manual overlapping insert → {(ex is null ? "ACCEPTED (bad)" : Sql.Describe(ex))}\n" +
                "المنع من المحرّك عبر EXCLUDE USING gist، لا من شرط في الكود يمكن نسيانه في مسار واحد.\n" +
                "مدى 5,000 لجهاز يبيع 500–1500 يومياً ⇒ 3–10 أيام تشغيل قبل الحاجة إلى تجديد.");
        });

        await Proof.RunAsync("4-ب", "نفاد المدى وهو غير متصل: التوقّف هو السلوك الوحيد الأمين، والعلاج مدى احتياطي مُنصَّب مسبقاً", async () =>
        {
            var d = await NewAsync(server, "D4B", size: 10);
            using var _ = d;
            int sold = 0;
            string? blocked = null;
            for (int i = 0; i < 15; i++)
            {
                try { d.RecordSale(P1_LocalStore.Basket); sold++; }
                catch (TradingBlockedException ex) { blocked = $"{ex.Code}: {ex.Message}"; break; }
            }

            // نفس الجهاز بسياسة «استمر مع إنذار»: لا يستطيع اختراع أرقام
            d.ApplySettings(PosSettings.Default with { AtRangeExhaustion = RangeExhaustionBehaviour.ContinueWithAlarm });
            string? alarmed = null;
            try { d.RecordSale(P1_LocalStore.Basket); } catch (TradingBlockedException ex) { alarmed = ex.Code; }

            // العلاج الحقيقي: مدى احتياطي مُنصَّب قبل الانقطاع
            var spare = await server.GrantRangeAsync(Config.Tenant, "D4B", 10);
            d.InstallRange(spare.RangeId, spare.Start, spare.End);
            d.Store.Exec("update device_counter set next_no = $n where singleton = 1", ("$n", spare.Start));
            var after = d.RecordSale(P1_LocalStore.Basket);
            var warned = d.Store.Scalar<long>("select count(*) from local_exception where kind in ('RANGE_LOW','RANGE_EXHAUSTED')");

            var ok = sold == 10 && blocked is not null && alarmed == "RANGE_EXHAUSTED_NO_FALLBACK" && after.InvoiceNo == spare.Start;
            return (ok,
                $"range of 10 numbers: sold {sold}, then blocked → {blocked}\n" +
                $"policy = ContinueWithAlarm → still blocked with {alarmed}\n" +
                $"نتيجة سلبية مهمة: «استمر مع إنذار» غير قابل للتنفيذ عند نفاد المدى، لأن اختراع رقم\n" +
                $"فاتورة يكسر عدم التداخل ولا يمكن تصحيحه لاحقاً. الخيار القابل للضبط الحقيقي هو\n" +
                $"«مدى احتياطي مُنصَّب مسبقاً» لا «استمر بلا أرقام».\n" +
                $"after installing a pre-granted spare range: invoice {after.InvoiceNo} issued from {spare.Start}..{spare.End}\n" +
                $"local warnings raised before exhaustion: {warned}");
        });

        await Proof.RunAsync("4-ج", "استبدال جهاز في منتصف مداه: الذيل يُبطَل وتُثبَت الفجوة إيجاباً", async () =>
        {
            var old = await NewAsync(server, "D4C-OLD", size: 200);
            for (int i = 0; i < 37; i++) old.RecordSale(P1_LocalStore.Basket);
            var lastUsed = old.Store.Scalar<long>("select max(invoice_no) from sale");
            await new SyncClient(old, server).RunAsync();
            old.Dispose();

            var newGrant = await server.ReplaceDeviceAsync(Config.Tenant, "D4C-OLD", "D4C-NEW",
                lastUsedNo: lastUsed, rangeSize: 200, by: "ops:manager@branch-01");
            var p = Db("D4C-NEW"); LocalStore.Delete(p);
            using var nd = PosDevice.Open(p, "D4C-NEW", Config.Tenant);
            nd.InstallRange(newGrant.RangeId, newGrant.Start, newGrant.End); nd.OpenShift();
            for (int i = 0; i < 12; i++) nd.RecordSale(P1_LocalStore.Basket);
            await new SyncClient(nd, server).RunAsync();

            var audit = await Verifier.AuditNumberingAsync(Config.Admin, Config.Tenant, "D4C-");
            var assertion = await Sql.TableAsync(Config.Admin, $"""
                select device_id, from_no, to_no, reason_code, asserted_by
                from pos.number_gap_assertion where device_id = 'D4C-OLD'
                """);
            var overlap = await Sql.ScalarAsync<long>(Config.Admin, """
                select count(*) from pos.number_range a join pos.number_range b
                  on a.range_id < b.range_id and int8range(a.range_start,a.range_end+1) && int8range(b.range_start,b.range_end+1)
                """);
            var ok = audit.Ok && overlap == 0;
            return (ok,
                $"old device sold 37 invoices; last used invoice_no = {lastUsed}\n" +
                $"tail of its range voided and asserted:\n{assertion}\n" +
                $"replacement range: {newGrant.Start}..{newGrant.End}; overlapping ranges anywhere = {overlap}\n" +
                $"numbering audit: {audit.Reason}");
        });

        await Proof.RunAsync("4-د", "الفجوة غير المُثبتة تُكتشَف — أي أن الإثبات حامل للوزن لا زينة", async () =>
        {
            var d = await NewAsync(server, "D4D", size: 100);
            using var _ = d;
            for (int i = 0; i < 20; i++) d.RecordSale(P1_LocalStore.Basket);
            var client = new SyncClient(d, server);
            var batch = client.NextBatch(20);
            // نُسقط القيدين 7 و8 من الإرسال: كما لو ضاعا قبل أن يصلا (أو حُذفا)
            var withHole = batch.Where((_, i) => i is not (6 or 7)).ToList();
            await server.SyncAsync(new SyncBatch(Config.Tenant, "D4D", "B", DateTime.UtcNow, withHole));

            var before = await Verifier.AuditNumberingAsync(Config.Admin, Config.Tenant, "D4D");
            // ثم يأتي قرار بشري صريح: هذان الرقمان لن يصلا أبداً، والسبب مُسجَّل
            await server.AssertGapAsync(Config.Tenant, "D4D", batch[6].InvoiceNo, batch[7].InvoiceNo,
                "DEVICE_LOST", "الجهاز أُتلف قبل مزامنة هاتين الفاتورتين — أُقرّ الفراغ بقرار موثّق", "ops:finance-manager");
            var after = await Verifier.AuditNumberingAsync(Config.Admin, Config.Tenant, "D4D");

            var ok = !before.Ok && before.Unexplained.Count == 1 && after.Ok;
            return (ok,
                $"before the assertion: {before.Reason}\n" +
                $"                      unexplained run = {before.Unexplained[0].From}..{before.Unexplained[0].To}\n" +
                $"after  the assertion: {after.Reason}\n" +
                "المُدقّق لا يستطيع التفريق بين «لم يحدث شيء» و«حُذفت سجلات» من غياب السجل.\n" +
                "لذلك الفراغ نفسه واقعة تُسجَّل بسبب ومُقرٍّ وبصمة، وإلا فهو فجوة غير مُفسَّرة.");
        });

        await Proof.RunAsync("4-هـ", "جهاز يزامن بعد غياب طويل: أرقامه أقدم من المُرحَّل، والتدقيق يبقى سليماً", async () =>
        {
            var late = await NewAsync(server, "D4E-LATE", size: 1000);
            using var _1 = late;
            for (int i = 0; i < 250; i++) late.RecordSale(P1_LocalStore.Basket);   // 250 عملية دون اتصال

            // بينما هو غائب، جهازان آخران يأخذان مديات أعلى ويزامنان
            var a = await NewAsync(server, "D4E-A", size: 1000);
            var b = await NewAsync(server, "D4E-B", size: 1000);
            using var _2 = a; using var _3 = b;
            for (int i = 0; i < 60; i++) { a.RecordSale(P1_LocalStore.Basket); b.RecordSale(P1_LocalStore.Basket); }
            await new SyncClient(a, server).RunAsync();
            await new SyncClient(b, server).RunAsync();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            var r = await new SyncClient(late, server).RunAsync();
            sw.Stop();

            var audit = await Verifier.AuditNumberingAsync(Config.Admin, Config.Tenant, "D4E-");
            var chain = await Verifier.VerifyBookAsync(Config.Admin, "POS:D4E-LATE");
            var order = await Sql.TableAsync(Config.Admin, """
                select device_id, min(invoice_no) as first_no, max(invoice_no) as last_no,
                       min(server_received_at) as first_seen
                from pos.sale_inbox where device_id like 'D4E-%' group by device_id order by min(invoice_no)
                """);
            var ok = r.Posted == 250 && audit.Ok && chain.Ok;
            return (ok,
                $"late device synced {r.Posted} entries in {sw.Elapsed.TotalMilliseconds:F0} ms\n{order}\n" +
                $"numbering audit: {audit.Reason}\nledger chain: {chain.Reason}\n" +
                "الجهاز الغائب يحمل أرقاماً أدنى من أرقام رُحِّلت قبله. هذا طبيعي ولا يكسر شيئاً:\n" +
                "الترقيم لكل جهاز، والسلسلة لكل جهاز، والحصانة لكل قيد. لا يوجد أي عدّاد عالمي\n" +
                "يفترض وصولاً بالترتيب — ولو وُجد لانكسر هنا بالضبط.");
        });

        await Proof.RunAsync("4-و", "جهاز مفقود بمبيعات غير مُزامَنة: ما يُعرف، وما لا يُعرف، وما يُقرّه بشر", async () =>
        {
            var lost = await NewAsync(server, "D4F", size: 500);
            for (int i = 0; i < 40; i++) lost.RecordSale(P1_LocalStore.Basket);
            var client = new SyncClient(lost, server);
            var first25 = client.NextBatch(25);
            var nextNo = lost.Store.Scalar<long>("select next_no from device_counter where singleton = 1");
            await server.SyncAsync(new SyncBatch(Config.Tenant, "D4F", "B1", DateTime.UtcNow, first25, nextNo));
            var lastSynced = first25.Max(e => e.InvoiceNo);
            var lastIssued = lost.Store.Scalar<long>("select max(invoice_no) from sale");
            var rangeEnd = await Sql.ScalarAsync<long>(Config.Admin,
                "select range_end from pos.number_range where device_id = 'D4F'");
            lost.Dispose();
            LocalStore.Delete(Db("D4F"));      // الجهاز يُدمَّر: الملف لم يعد موجوداً

            var gapAudit = await Verifier.AuditNumberingAsync(Config.Admin, Config.Tenant, "D4F");
            var hwAudit = await Verifier.AuditIssuedButMissingAsync(Config.Admin, Config.Tenant, "D4F");

            await server.ReplaceDeviceAsync(Config.Tenant, "D4F", "D4F-REPL", lastUsedNo: lastSynced,
                rangeSize: 500, by: "ops:finance-manager", reasonCode: "DEVICE_LOST");
            var hwAfter = await Verifier.AuditIssuedButMissingAsync(Config.Admin, Config.Tenant, "D4F");
            var gapAfter = await Verifier.AuditNumberingAsync(Config.Admin, Config.Tenant, "D4F");
            var assertion = await Sql.TableAsync(Config.Admin,
                "select from_no, to_no, reason_code, asserted_by from pos.number_gap_assertion where device_id = 'D4F'");

            var ok = gapAudit.Ok && !hwAudit.Ok && hwAudit.Missing.Count == 1
                     && hwAudit.Missing[0].From == lastSynced + 1 && hwAudit.Missing[0].To == lastIssued
                     && hwAfter.Ok && gapAfter.Ok;
            return (ok,
                $"the device issued invoices up to {lastIssued}; the server ever received only up to {lastSynced}\n" +
                $"never-issued tail of its range = {lastIssued + 1}..{rangeEnd}\n" +
                $"(1) الفحص المبني على الوارد وحده: {gapAudit.Reason}\n" +
                $"    ⇒ نتيجة سلبية حاسمة: لا يرى شيئاً. الفراغ يقع فوق أعلى رقم يعرفه الخادم،\n" +
                $"      فلا يبدو فراغاً بل يبدو أن الجهاز توقّف عن البيع. الحذف الكامل للذيل غير قابل للاكتشاف.\n" +
                $"(2) الفحص المبني على علامة المياه العليا التي أبلغ عنها الجهاز: {hwAudit.Reason}\n" +
                $"    ⇒ {hwAudit.Missing[0].To - hwAudit.Missing[0].From + 1} فاتورة صدرت ولم تصل، بأموال حقيقية، ومحتواها ذهب مع الجهاز\n" +
                $"(3) القرار البشري الموثّق:\n{assertion}\n" +
                $"(4) بعد الإقرار: {hwAfter.Reason}\n" +
                "الحدّ المعرفي الذي لا يزول: ما أُصدر بعد آخر اتصال يبقى مجهولاً حتى للعلامة العليا.\n" +
                "لذلك لا حلّ آلياً كاملاً؛ الحلّ إجراء تشغيلي موثّق + أدلّة خارجية (تسويات الشبكة، الجرد).\n" +
                "التفصيل في DESIGN.md §10 — كتاب التشغيل لحالة الجهاز المفقود.");
        });
    }
}
