using System.Diagnostics;
using Babel.ControlPlane.Entitlement;
using Babel.ControlPlane.Metering;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;

namespace Babel.ControlPlane.Proofs;

/// <summary>
/// (و) القياس — البذرة التي لا يمكن زرعها بأثر رجعي.
/// </summary>
public static class ProofF_Metering
{
    private const int EventCount = 1200;
    private const string Period = "2026-04";

    public static async Task RunAsync(ControlPlaneOptions o, Recorder rec)
    {
        Recorder.Section("(و) القياس على المحورين، والإحكام تحت إعادة المحاولة");

        var registry = new TenantRegistry(o);
        var entitlements = new EntitlementService(o, registry);
        var t = (await Harness.SimulateFleetAsync(o, registry, "mtr", 1, 1))[0];

        var queries = new MeteringQueries(o);
        await queries.EnsurePeriodAsync(Period);

        await using (var c = await Db.OpenAsync(o.ControlConnectionString))
            await PlanCatalog.SubscribeAsync(c, t.TenantId, "FULL", new DateOnly(2026, 1, 1));
        await entitlements.ApplyPlanAsync(t.TenantId, "FULL",
            new ChangeAuthority("sales.ops", "CONTRACT-2026-0121", "حزمة شاملة"));

        var spoolPath = Path.Combine(Path.GetTempPath(),
            $"babel-cp-spool-{Guid.NewGuid():N}.jsonl");
        var recorder = new UsageRecorder(o, new UsageSpool(spoolPath));

        // ---- أحداث على المحورين -----------------------------------------------
        var users = Enumerable.Range(1, 25).Select(i => $"user{i:D2}@mtr.example").ToList();
        var modules = new[] { "CORE", "AR", "AP", "INV", "POS", "PAY" };
        var events = Enumerable.Range(0, EventCount).Select(i => new UsageEvent(
            TenantId: t.TenantId,
            IdempotencyKey: $"evt-{i:D6}",
            PeriodCode: Period,
            ModuleCode: modules[i % modules.Length],
            UserRef: i % 5 == 0 ? null : users[i % users.Count],
            EventKind: i % 3 == 0 ? "document.created" : "posting.created",
            Quantity: 1.0000m,
            OccurredAt: new DateTimeOffset(2026, 4, 1 + i % 28, 9, 0, 0, TimeSpan.Zero)
                        .AddMilliseconds(i),
            Source: "proof")).ToList();

        var sw = Stopwatch.StartNew();
        var first = await recorder.RecordAsync(events);
        sw.Stop();
        rec.Check("F1", $"تسجيل {EventCount} حدث قياس",
            first.Accepted == EventCount && first.Duplicates == 0,
            $"مقبول={first.Accepted} مكرَّر={first.Duplicates} "
            + $"في {sw.ElapsedMilliseconds} مللي‌ثانية "
            + $"({EventCount / sw.Elapsed.TotalSeconds:F0} حدث/ث)");

        // ---- ⭐ لا عدّ مزدوج تحت إعادة المحاولة -------------------------------
        var retries = await Task.WhenAll(Enumerable.Range(0, 6).Select(async k =>
        {
            // ترتيب مختلف في كل إعادة محاولة، وبعضها معكوس — الإحكام يجب أن
            // يكون **مستقلاً عن الترتيب** (فخ-13: حارس تصاعدي يُسقِط الوارد
            // المتأخّر بصمت).
            var shuffled = k % 2 == 0
                ? events.OrderBy(_ => Guid.NewGuid()).ToList()
                : Enumerable.Reverse(events).ToList();
            return await recorder.RecordAsync(shuffled);
        }));

        var stored = await queries.EventCountAsync(t.TenantId, Period);
        rec.Check("F2", "⭐ ست إعادات محاولة متزامنة بترتيبات مختلفة ⇒ صفر عدّ مزدوج",
            stored == EventCount && retries.All(r => r.Accepted == 0 && r.Duplicates == EventCount),
            $"الأحداث المخزَّنة = {stored} (المتوقَّع {EventCount})\n"
            + $"إعادات المحاولة: مقبول={retries.Sum(r => r.Accepted)} "
            + $"مكرَّر={retries.Sum(r => r.Duplicates)} — كلها رُفضت بمفتاح الإحكام");

        // ---- المتانة عند الانهيار ------------------------------------------
        var unreachable = new ControlPlaneOptions
        {
            ControlDatabase = "babel_cp_does_not_exist",
            TenantDatabasePrefix = o.TenantDatabasePrefix,
            AppRole = o.AppRole,
            ConnectTimeoutSeconds = 2
        };
        var crashSpool = new UsageSpool(spoolPath + ".crash");
        var offline = new UsageRecorder(unreachable, crashSpool);

        var offlineEvents = Enumerable.Range(EventCount, 300).Select(i => new UsageEvent(
            t.TenantId, $"evt-{i:D6}", Period, "POS", users[i % users.Count],
            "posting.created", 1.0000m,
            new DateTimeOffset(2026, 4, 20, 12, 0, 0, TimeSpan.Zero).AddSeconds(i), "offline")).ToList();

        var offlineOutcome = await offline.RecordAsync(offlineEvents);
        rec.Check("F3", "قاعدة التحكّم غير متاحة ⇒ الأحداث تُثبَّت على القرص لا تُسقَط",
            offlineOutcome.Spooled == 300 && crashSpool.Count == 300,
            $"مُخزَّن محلّياً = {offlineOutcome.Spooled}؛ في الملف = {crashSpool.Count}؛ "
            + $"المسار = {crashSpool.Path}");

        // «انهيار»: نتخلّى عن الكائن تماماً ونبني مُسجّلاً جديداً على نفس الملف.
        var afterCrash = new UsageRecorder(o, new UsageSpool(spoolPath + ".crash"));
        var drained = await afterCrash.DrainSpoolAsync();
        var storedAfter = await queries.EventCountAsync(t.TenantId, Period);

        rec.Check("F4", "بعد إعادة التشغيل: المخزن المحلّي يُصرَّف كاملاً بلا فقد",
            drained.Accepted == 300 && storedAfter == EventCount + 300,
            $"صُرِّف {drained.Accepted}؛ المجموع الآن {storedAfter} "
            + $"(المتوقَّع {EventCount + 300})");

        // تصريف مرتين: لا يُضاعف شيئاً.
        crashSpool.Append(offlineEvents);      // كأن التصريف قُوطع بعد الإدراج وقبل الحذف
        var second = await afterCrash.DrainSpoolAsync();
        var storedFinal = await queries.EventCountAsync(t.TenantId, Period);
        rec.Check("F5", "تصريف مُقاطَع ثم مُعاد ⇒ لا مضاعفة",
            second.Accepted == 0 && second.Duplicates == 300 && storedFinal == EventCount + 300,
            $"إعادة التصريف: مقبول={second.Accepted} مكرَّر={second.Duplicates}؛ "
            + $"المجموع ثابت عند {storedFinal}");

        // ---- المحور الثاني: التعريفات الثلاثة ---------------------------------
        await using (var c = await Db.OpenAsync(o.ControlConnectionString))
        {
            var ordered = users.OrderBy(x => x, StringComparer.Ordinal).ToList();
            for (var i = 0; i < ordered.Count; i++)
                await Db.WriteAsync(c, """
                    insert into control.tenant_user
                        (tenant_id, user_ref, name_ar, name_en, state, created_at)
                    values (@t, @u, @ar, @en, 'Active', @at)
                    on conflict (tenant_id, user_ref) do update set state = excluded.state
                    """, 1, p =>
                {
                    p.Add(Db.P("t", t.TenantId, NpgsqlTypes.NpgsqlDbType.Uuid));
                    p.AddWithValue("u", ordered[i]);
                    p.AddWithValue("ar", $"مستخدم {i + 1}");
                    p.AddWithValue("en", $"user {i + 1}");
                    p.AddWithValue("at", new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero));
                }, null, default);

            // مستخدمان مُسمّيان لم يفعلا شيئاً — هنا يفترق التعريفان.
            foreach (var ghost in new[] { "ghost01@mtr.example", "ghost02@mtr.example" })
                await Db.WriteAsync(c, """
                    insert into control.tenant_user
                        (tenant_id, user_ref, name_ar, name_en, state, created_at)
                    values (@t, @u, @ar, @en, 'Active', @at)
                    on conflict (tenant_id, user_ref) do nothing
                    """, 1, p =>
                {
                    p.Add(Db.P("t", t.TenantId, NpgsqlTypes.NpgsqlDbType.Uuid));
                    p.AddWithValue("u", ghost);
                    p.AddWithValue("ar", "مقعد غير مستعمَل");
                    p.AddWithValue("en", "unused seat");
                    p.AddWithValue("at", new DateTimeOffset(2026, 1, 5, 0, 0, 0, TimeSpan.Zero));
                }, null, default);
        }

        foreach (var n in new[] { 3, 5, 4 })
            await recorder.SampleConcurrencyAsync(t.TenantId, Period, n);

        var counts = await queries.AllUserCountsAsync(t.TenantId, Period);
        var named = counts.First(c => c.StrategyCode == "Named").Count;
        var concurrent = counts.First(c => c.StrategyCode == "Concurrent").Count;
        var active = counts.First(c => c.StrategyCode == "ActiveInPeriod").Count;

        rec.Check("F6", "التعريفات الثلاثة مُنفَّذة وتُنتج ثلاثة أرقام مختلفة من نفس البيانات",
            named == 27 && active == 25 && concurrent == 5 && named != active && active != concurrent,
            string.Join("\n", counts.Select(c => $"  {c.StrategyCode,-15} {c.NameAr,-28} = {c.Count}"))
            + "\n  ⇒ الفرق بين التعريفات مادّي في الفاتورة، ولذلك هو سؤال عمل مفتوح "
            + "لا اختيار هندسي.");

        rec.Check("F7", $"الافتراضي المُتحفَّظ = {BillableUserStrategies.Default.Code}",
            BillableUserStrategies.Default.Code == "ActiveInPeriod",
            "لا يُفوتِر مقعداً لم يُستعمل؛ ومشتقّ من أحداث نلتقطها فعلاً. "
            + "قابل للتبديل بإعداد، والحسم على المالك.");

        // ---- الفاتورة على المحورين ------------------------------------------
        var preview = await queries.PreviewAsync(t.TenantId, Period);
        var expectedUserCharge = decimal.Round(
            Math.Max(0, active - preview.IncludedUsers) * preview.PerUserPrice, 4);
        var expectedTotal = decimal.Round(preview.PlanMonthly + expectedUserCharge, 4);

        rec.Check("F8", "الفاتورة تُحسب على المحورين بـdecimal/numeric(19,4) — لا عائم",
            preview.Total == expectedTotal && preview.UserCharge == expectedUserCharge,
            $"الخطة {preview.PlanCode}: شهري {Canon.Amount(preview.PlanMonthly)} {preview.Currency} "
            + $"+ مستخدمون ({preview.BillableUsers} نشِط، {preview.IncludedUsers} مُضمَّن، "
            + $"{preview.ChargeableUsers} محاسَب × {Canon.Amount(preview.PerUserPrice)}) "
            + $"= {Canon.Amount(preview.UserCharge)}\n"
            + $"  الإجمالي = {Canon.Amount(preview.Total)} {preview.Currency}");

        // ---- الاستعلام حسب فترة الفوترة --------------------------------------
        var usage = await queries.ModuleUsageAsync(t.TenantId, Period);
        rec.Check("F9", "القياس قابل للاستعلام حسب فترة الفوترة وحسب الوحدة",
            usage.Count == modules.Length && usage.Sum(u => u.Events) == EventCount + 300,
            string.Join("\n", usage.Select(u =>
                $"  {u.ModuleCode,-6} أحداث={u.Events,5} كمّية={Canon.Amount(u.Quantity)}")));

        var otherPeriod = await queries.EventCountAsync(t.TenantId, "2026-05");
        rec.Check("F10", "فترة أخرى لا تلتقط أحداث هذه الفترة", otherPeriod == 0,
            $"أحداث 2026-05 = {otherPeriod}");

        try { File.Delete(spoolPath + ".crash"); } catch { }
    }
}
