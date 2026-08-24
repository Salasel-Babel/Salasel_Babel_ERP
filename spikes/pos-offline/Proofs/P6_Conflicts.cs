using BabelPosOffline.Device;
using BabelPosOffline.Server;
using BabelPosOffline.Support;
using Npgsql;

namespace BabelPosOffline.Proofs;

/// <summary>
/// (6) حالات التعارض. القاعدة الحاكمة الواحدة:
/// <b>عملية بيع وقعت دون اتصال لا تُرفض أبداً</b> — البضاعة سُلِّمت والنقد قُبض والفاتورة
/// بيد العميل. ما يمكن فعله هو <b>إظهار</b> التعارض لقرار بشري، لا حلّه بصمت.
/// </summary>
public static class P6_Conflicts
{
    private static string Db(string n) => Path.Combine(Config.DeviceDir, $"{n}.sqlite");

    private static async Task<PosDevice> NewAsync(SyncServer s, string dev, DeviceClock? c = null, PosSettings? cfg = null)
    {
        await s.RegisterDeviceAsync(Config.Tenant, dev);
        var g = await s.GrantRangeAsync(Config.Tenant, dev, 5000);
        var p = Db(dev); LocalStore.Delete(p);
        var d = PosDevice.Open(p, dev, Config.Tenant, c, cfg);
        d.InstallRange(g.RangeId, g.Start, g.End); d.OpenShift();
        return d;
    }

    private static async Task SeedAsync(string item, decimal onHand, decimal price, decimal cost)
    {
        await using var conn = await Sql.OpenAsync(Config.Admin);
        await using var cmd = new NpgsqlCommand("""
            insert into pos.stock (tenant_id, item_code, on_hand) values (@t, @i, @h)
                on conflict (tenant_id, item_code) do update set on_hand = excluded.on_hand;
            insert into pos.price (tenant_id, item_code, effective_from, unit_price)
                values (@t, @i, now() - interval '30 days', @p)
                on conflict (tenant_id, item_code, effective_from) do update set unit_price = excluded.unit_price;
            insert into pos.item_cost (tenant_id, item_code, unit_cost) values (@t, @i, @c)
                on conflict (tenant_id, item_code) do update set unit_cost = excluded.unit_cost;
            """, conn);
        cmd.Parameters.AddWithValue("t", Config.Tenant); cmd.Parameters.AddWithValue("i", item);
        cmd.Parameters.AddWithValue("h", onHand); cmd.Parameters.AddWithValue("p", price);
        cmd.Parameters.AddWithValue("c", cost);
        await cmd.ExecuteNonQueryAsync();
    }

    public static async Task RunAsync()
    {
        Proof.Section("(6) التعارضات — القاعدة، والتنفيذ، والإثبات، وما يبقى لقرار بشري");
        var server = new SyncServer(Config.Admin);

        await Proof.RunAsync("6-أ", "آخر قطعتين تُباعان على جهازين دون اتصال: كلاهما يُرحَّل، والعجز يظهر لا يُخفى", async () =>
        {
            const string item = "ITM-LAST";
            await SeedAsync(item, onHand: 2m, price: 50.0000m, cost: 30.0000m);
            var basket = new[] { new SaleItem(item, 2m, 50.0000m, 0.15m) };

            using var d1 = await NewAsync(server, "D6A1");
            using var d2 = await NewAsync(server, "D6A2");
            var s1 = d1.RecordSale(basket);      // كلاهما يرى «متاح 2»
            var s2 = d2.RecordSale(basket);      // ويبيعه فعلياً
            await new SyncClient(d1, server).RunAsync();
            await new SyncClient(d2, server).RunAsync();

            var onHand = await Sql.ScalarAsync<decimal>(Config.Admin,
                $"select on_hand from pos.stock where item_code = '{item}'");
            var posted = await Sql.ScalarAsync<long>(Config.Admin,
                "select count(*) from pos.sale_inbox where device_id in ('D6A1','D6A2') and status = 'posted'");
            var exq = await Sql.TableAsync(Config.Admin, """
                select device_id, kind, severity, detail->>'item_code' as item, detail->>'on_hand' as on_hand
                from pos.exception_queue where kind = 'STOCK_OVERSELL' and resolved_at is null
                  and detail->>'item_code' = 'ITM-LAST' order by raised_at
                """);
            var count = await Sql.ScalarAsync<long>(Config.Admin, $"""
                select count(*) from pos.exception_queue
                where kind = 'STOCK_OVERSELL' and resolved_at is null and detail->>'item_code' = '{item}'
                """);
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);
            var ok = posted == 2 && onHand == -2m && count == 1 && tb.Balanced;
            return (ok,
                $"available = 2; device 1 sold 2 (invoice {s1.InvoiceNo}), device 2 sold 2 (invoice {s2.InvoiceNo}) — both offline\n" +
                $"posted entries = {posted}/2 (neither is rejected), stock on_hand = {Money.Canonical(onHand)}\n{exq}\n" +
                $"open STOCK_OVERSELL exceptions = {count}; trial balance {(tb.Balanced ? "exact" : "OUT")}\n" +
                "القاعدة: البيع دون اتصال لا يُرفض. المخزون يُدفَع إلى السالب عمداً لأن السالب حقيقة\n" +
                "مادية وقعت، ويُرفع استثناء بدرجة block يتطلّب تسوية مخزنية أو طلب توريد.\n" +
                "البديل — رفض الفاتورة الثانية — يعني إلغاء بيع تمّ فعلاً، وهو أسوأ محاسبياً وقانونياً.");
        });

        await Proof.RunAsync("6-ب", "سعر تغيّر مركزياً والأجهزة غير متصلة: الفاتورة المسلَّمة هي الحُجّة، والفرق يُسجَّل", async () =>
        {
            const string item = "ITM-PRICED";
            await SeedAsync(item, onHand: 500m, price: 40.0000m, cost: 25.0000m);
            using var d = await NewAsync(server, "D6B");
            var basket = new[] { new SaleItem(item, 3m, 40.0000m, 0.15m) };
            var s = d.RecordSale(basket);                       // بِيع بالسعر القديم

            // بينما الجهاز غير متصل، يرفع المكتب الرئيسي السعر
            await using (var conn = await Sql.OpenAsync(Config.Admin))
            {
                await using var cmd = new NpgsqlCommand("""
                    insert into pos.price (tenant_id, item_code, effective_from, unit_price)
                    values (@t, @i, now() - interval '1 hour', 47.5000)
                    """, conn);
                cmd.Parameters.AddWithValue("t", Config.Tenant); cmd.Parameters.AddWithValue("i", item);
                await cmd.ExecuteNonQueryAsync();
            }
            await new SyncClient(d, server).RunAsync();

            var revenue = await Sql.ScalarAsync<decimal>(Config.Admin, $"""
                select coalesce(sum(l.credit),0) from ledger.journal_line l
                join ledger.journal_entry e on e.entry_id = l.entry_id
                where e.book_id = 'POS:D6B' and l.account_code = '{PosDevice.AccRevenue}'
                """);
            var variance = await Sql.TableAsync(Config.Admin, """
                select severity, detail->>'device_price' as device_price, detail->>'central_price' as central_price,
                       detail->>'variance' as variance, detail->>'qty' as qty
                from pos.exception_queue where kind = 'PRICE_VARIANCE' and resolved_at is null
                  and detail->>'item_code' = 'ITM-PRICED'
                """);
            var n = await Sql.ScalarAsync<long>(Config.Admin, $"""
                select count(*) from pos.exception_queue
                where kind = 'PRICE_VARIANCE' and resolved_at is null and detail->>'item_code' = '{item}'
                """);
            var ok = revenue == 120.0000m && n == 1;
            return (ok,
                $"sold 3 × 40.0000 offline (invoice {s.InvoiceNo}); central price is now 47.5000\n" +
                $"revenue posted = {Money.Canonical(revenue)} ⇒ the DEVICE price, not the central one\n{variance}\n" +
                $"open PRICE_VARIANCE exceptions = {n}\n" +
                "القاعدة: الفاتورة الضريبية المسلَّمة للعميل مستند نهائي لا يُعاد تسعيره بأثر رجعي.\n" +
                "الإيراد يُرحَّل بسعر الفاتورة، والفارق (7.5 × 3 = 22.5) يُسجَّل كانحراف تسعير كي يبقى\n" +
                "هامش الربح قابلاً للتفسير. وفوق العتبة المضبوطة يُصعَّد لمراجعة بشرية.\n" +
                "ملاحظة: هذا يفترض أن الجهاز حصل على السعر شرعاً قبل الانقطاع؛ التمييز بين ذلك\n" +
                "وبين تلاعب الكاشير بالسعر يحتاج صلاحيات وسجلاً على الجهاز — سؤال عمل مفتوح.");
        });

        await Proof.RunAsync("6-ج", "مرتجع دون اتصال على بيع لم يُزامَن أصلاً: يُحجَز، ثم يُطلَق عند وصول أصله", async () =>
        {
            const string item = "ITM-RET";
            await SeedAsync(item, onHand: 100m, price: 60.0000m, cost: 35.0000m);
            using var d = await NewAsync(server, "D6C");
            var basket = new[] { new SaleItem(item, 1m, 60.0000m, 0.15m) };
            var sale = d.RecordSale(basket);
            var ret = d.RecordSale(basket, "RETURN", sale.IdemKey);

            var client = new SyncClient(d, server);
            var all = client.NextBatch(10);
            var retEntry = all.First(e => e.DocType == "RETURN");
            var saleEntry = all.First(e => e.DocType == "SALE");

            // المرتجع يصل أولاً (ترتيب طبيعي تماماً في المزامنة دون اتصال)
            var r1 = await server.SyncAsync(new SyncBatch(Config.Tenant, "D6C", "B1", DateTime.UtcNow, [retEntry]));
            var quarantined = r1.Acks[0].Outcome;
            var postedAfterRet = await Sql.ScalarAsync<long>(Config.Admin,
                "select count(*) from ledger.journal_entry where book_id = 'POS:D6C'");
            var orphanEx = await Verifier.OpenExceptionsAsync(Config.Admin, "ORPHAN_RETURN");

            // ثم يصل البيع الأصلي
            await server.SyncAsync(new SyncBatch(Config.Tenant, "D6C", "B2", DateTime.UtcNow, [saleEntry]));
            var released = await Sql.ScalarAsync<string>(Config.Admin,
                $"select pos.release_orphan('{retEntry.IdemKey}')");
            // ومحاولة ثانية للإطلاق: يجب ألا تنتج قيداً ثانياً
            var again = await Sql.ScalarAsync<string>(Config.Admin,
                $"select pos.release_orphan('{retEntry.IdemKey}')");

            var status = await Sql.TableAsync(Config.Admin, """
                select doc_type, invoice_no, status, coalesce(note,'') as note
                from pos.sale_inbox where device_id = 'D6C' order by invoice_no
                """);
            var retEntries = await Sql.ScalarAsync<long>(Config.Admin,
                $"select count(*) from ledger.journal_entry where source_idem_key = '{retEntry.IdemKey}'");
            var cash = await Verifier.AccountBalanceAsync(Config.Admin, PosDevice.AccCash);
            var openOrphan = await Verifier.OpenExceptionsAsync(Config.Admin, "ORPHAN_RETURN");
            var chain = await Verifier.VerifyBookAsync(Config.Admin, "POS:D6C");
            var tb = await Verifier.TrialBalanceAsync(Config.Admin);

            var ok = quarantined == EntryOutcome.Quarantined && postedAfterRet == 0 && orphanEx == 1
                     && released == "released" && again == "not_quarantined" && retEntries == 1
                     && openOrphan == 0 && chain.Ok && tb.Balanced;
            return (ok,
                $"the RETURN arrived first → outcome = {quarantined}; ledger entries at that moment = {postedAfterRet}\n" +
                $"ORPHAN_RETURN exceptions raised = {orphanEx} (severity block, visible, not auto-resolved)\n" +
                $"then the original SALE arrived → release_orphan = '{released}'; second attempt = '{again}'\n{status}\n" +
                $"ledger entries carrying the return's idempotency key = {retEntries} (must be 1)\n" +
                $"chain: {chain.Reason}; trial balance {(tb.Balanced ? "exact" : "OUT")}\n" +
                "القاعدة الافتراضية Quarantine هي الأكثر تحفّظاً: ترحيل ردّ نقدي مقابل فاتورة\n" +
                "غير موجودة هو بالضبط شكل الاحتيال الداخلي الأشيع في نقاط البيع. الحجز يجعله مرئياً.");
        });

        await Proof.RunAsync("6-د", "مرتجع يتيم لا يصل أصله أبداً: يبقى محجوزاً ومرئياً، ولا يُحلّ بصمت", async () =>
        {
            const string item = "ITM-ORPHAN";
            await SeedAsync(item, onHand: 100m, price: 15.0000m, cost: 9.0000m);
            using var d = await NewAsync(server, "D6D");
            var basket = new[] { new SaleItem(item, 1m, 15.0000m, 0.15m) };
            d.RecordSale(basket);
            var ret = d.RecordSale(basket, "RETURN", "T-001|D6-GHOST|SALE|999999");  // أصله على جهاز مفقود

            var client = new SyncClient(d, server);
            var batch = client.NextBatch(10).Where(e => e.DocType == "RETURN").ToList();
            var r = await server.SyncAsync(new SyncBatch(Config.Tenant, "D6D", "B", DateTime.UtcNow, batch));
            var rel = await Sql.ScalarAsync<string>(Config.Admin, $"select pos.release_orphan('{batch[0].IdemKey}')");
            var open = await Sql.TableAsync(Config.Admin, """
                select kind, severity, device_id, detail->>'original_idem_key' as original, detail->>'gross' as gross
                from pos.exception_queue where kind = 'ORPHAN_RETURN' and resolved_at is null
                """);
            var entries = await Sql.ScalarAsync<long>(Config.Admin,
                $"select count(*) from ledger.journal_entry where source_idem_key = '{batch[0].IdemKey}'");

            // والسياسة البديلة، القابلة للضبط، تُظهر أن الخيار قرار عمل موثّق
            server.OrphanPolicy = "PostWithAlarm";
            using var d2 = await NewAsync(server, "D6D2");
            d2.RecordSale(basket);
            var ret2 = d2.RecordSale(basket, "RETURN", "T-001|D6-GHOST|SALE|999998");
            var b2 = new SyncClient(d2, server).NextBatch(10).Where(e => e.DocType == "RETURN").ToList();
            var r2 = await server.SyncAsync(new SyncBatch(Config.Tenant, "D6D2", "B", DateTime.UtcNow, b2));
            server.OrphanPolicy = "Quarantine";
            var entries2 = await Sql.ScalarAsync<long>(Config.Admin,
                $"select count(*) from ledger.journal_entry where source_idem_key = '{b2[0].IdemKey}'");

            var ok = r.Acks[0].Outcome == EntryOutcome.Quarantined && rel == "original_still_missing"
                     && entries == 0 && r2.Acks[0].Outcome == EntryOutcome.Posted && entries2 == 1;
            return (ok,
                $"policy = Quarantine     → outcome {r.Acks[0].Outcome}, release attempt = '{rel}', ledger entries = {entries}\n" +
                $"policy = PostWithAlarm  → outcome {r2.Acks[0].Outcome}, ledger entries = {entries2}, exception raised anyway\n" +
                $"still-open orphan returns:\n{open}\n" +
                "الخياران كلاهما مُنفَّذ، والافتراضي هو الأكثر تحفّظاً. أيّهما يُعتمد قرار عمل\n" +
                "(من يملك صلاحية الإفراج؟ وخلال كم يوماً؟) — مُدرَج في الأسئلة المفتوحة.");
        });

        await Proof.RunAsync("6-هـ", "طابور الاستثناءات نفسه وضع فشل: صنف واحد سيّئ الإعداد يغرقه", async () =>
        {
            const string item = "ITM-FLOOD";
            await SeedAsync(item, onHand: 0m, price: 10.0000m, cost: 6.0000m);
            using var d = await NewAsync(server, "D6E");
            var basket = new[] { new SaleItem(item, 1m, 10.0000m, 0.15m) };
            const int sales = 150;
            for (int i = 0; i < sales; i++) d.RecordSale(basket);
            await new SyncClient(d, server).RunAsync();

            var rows = await Sql.ScalarAsync<long>(Config.Admin, $"""
                select count(*) from pos.exception_queue
                where kind = 'STOCK_OVERSELL' and resolved_at is null and detail->>'item_code' = '{item}'
                """);
            var occ = await Sql.ScalarAsync<long>(Config.Admin, $"""
                select coalesce(sum(occurrences),0) from pos.exception_queue
                where kind = 'STOCK_OVERSELL' and resolved_at is null and detail->>'item_code' = '{item}'
                """);
            var onHand = await Sql.ScalarAsync<decimal>(Config.Admin,
                $"select on_hand from pos.stock where item_code = '{item}'");
            var posted = await Sql.ScalarAsync<long>(Config.Admin,
                "select count(*) from pos.sale_inbox where device_id = 'D6E' and status = 'posted'");
            var ok = posted == sales && rows == 1 && occ == sales;
            return (ok,
                $"{sales} sales of an item whose central stock record says 0 — every one of them oversells\n" +
                $"posted entries = {posted}/{sales} (none rejected, as the rule requires); on_hand = {Money.Canonical(onHand)}\n" +
                $"exception rows = {rows}, aggregated occurrences = {occ}\n" +
                "وضع فشل لم يكن في القائمة: بلا تجميع، ينتج صنف واحد سيّئ الإعداد صفّاً لكل سطر\n" +
                $"لكل عملية — أي {sales} صفاً هنا، و~3,000 صفّاً يومياً لجهاز واحد في الإنتاج.\n" +
                "الطابور الذي يُفترض أن يقرأه إنسان يصير ضجيجاً خلال يوم واحد، فيُتجاهَل كلياً،\n" +
                "فيضيع معه التعارضُ النادر الحقيقي. العلاج مُنفَّذ: فهرس فريد جزئي على\n" +
                "(مستأجر × نوع × جهاز × صنف) للصفوف المفتوحة، وعدّاد تكرار بدل صفٍّ جديد.\n" +
                "الدرس الأعمّ: «ارفع استثناءً للإنسان» ليس حلاً ما لم يُصمَّم معدّلُ الاستثناءات أيضاً.");
        });

        await Proof.RunAsync("6-ز", "لا شيء يُحلّ بصمت: كل تعارض له صف مرئي في طابور الاستثناءات", async () =>
        {
            var table = await Sql.TableAsync(Config.Admin, """
                select kind, severity, count(*) as open_count
                from pos.exception_queue where resolved_at is null group by kind, severity order by kind
                """);
            var resolved = await Sql.TableAsync(Config.Admin, """
                select kind, count(*) as auto_resolved, max(coalesce(resolved_by,'')) as by
                from pos.exception_queue where resolved_at is not null group by kind
                """);
            var silent = await Sql.ScalarAsync<long>(Config.Admin, """
                select count(*) from pos.sale_inbox where status <> 'posted'
                  and idem_key not in (select idem_key from pos.exception_queue where idem_key is not null)
                """);
            var ok = silent == 0;
            return (ok,
                $"open exceptions by kind:\n{table}\n\nauto-resolved (only where a machine fact resolved it):\n{resolved}\n\n" +
                $"non-posted inbox rows with NO exception row = {silent} (must be 0)\n" +
                "كل قيد لم يُرحَّل له سبب مرئي وصف في طابور يملكه إنسان. لا يوجد مسار يُسقط قيداً\n" +
                "بصمت — وهذا بالضبط ما يميّز هذا التصميم عن حارس التسلسل المحظور في الإثبات (3-هـ).");
        });
    }
}
