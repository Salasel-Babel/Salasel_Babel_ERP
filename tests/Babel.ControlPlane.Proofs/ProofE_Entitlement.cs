using Babel.ControlPlane.Entitlement;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Babel.ControlPlane.TenantSide;

namespace Babel.ControlPlane.Proofs;

/// <summary>
/// (هـ) الاستحقاق بثلاث حالات، ورسم الاعتماديات، وسجل التدقيق،
/// و<b>رفض الأرشفة حين يكون الرصيد غير صفري</b>.
/// </summary>
public static class ProofE_Entitlement
{
    public static async Task RunAsync(ControlPlaneOptions o, Recorder rec)
    {
        Recorder.Section("(هـ) الاستحقاق — ثلاث حالات ورسم اعتماديات");

        var registry = new TenantRegistry(o);
        var entitlements = new EntitlementService(o, registry);
        var guard = new EntitlementGuard(entitlements);

        var tenant = (await Harness.SimulateFleetAsync(o, registry, "ent", 1, 1))[0];
        await using (var c = await Db.OpenAsync(o.ControlConnectionString))
            await PlanCatalog.SubscribeAsync(c, tenant.TenantId, "RETAIL", new DateOnly(2026, 1, 1));

        // ---- رسم الاعتماديات بلا حلقات --------------------------------------
        var cycles = ModuleCatalog.DetectCycles();
        rec.Check("E1", "رسم اعتماديات الوحدات بلا حلقات", cycles.Count == 0,
            "POS ⇒ {" + string.Join(", ", ModuleCatalog.TransitiveDependencies("POS")) + "} · "
            + "PRJ ⇒ {" + string.Join(", ", ModuleCatalog.TransitiveDependencies("PRJ")) + "}");

        // ---- مجموعة غير متماسكة تُرفض ---------------------------------------
        var authority = new ChangeAuthority("sales.ops", "CONTRACT-2026-0044",
            "تفعيل حزمة التجزئة بعد توقيع العقد");

        var incoherent = false;
        var violationText = "";
        try
        {
            await entitlements.ApplyAsync(tenant.TenantId,
                [new EntitlementChange("POS", EntitlementState.Entitled)], authority);
        }
        catch (IncoherentEntitlementSetException ex)
        {
            incoherent = true;
            violationText = string.Join("\n", ex.Violations.Select(v => "  " + v.MessageAr));
        }
        rec.Check("E2", "مجموعة استحقاق غير متماسكة مرفوضة: POS بلا INV/AR",
            incoherent, violationText);

        // ---- الخطة تُطبَّق مع إغلاق الاعتماديات -------------------------------
        await entitlements.ApplyPlanAsync(tenant.TenantId, "RETAIL", authority);
        var set = await entitlements.GetSetAsync(tenant.TenantId);
        var coherent = EntitlementValidator.Validate(set);
        rec.Check("E3", "تطبيق الخطة يُنتج مجموعة متماسكة مغلقة على اعتمادياتها",
            coherent.Count == 0 && set["POS"] == EntitlementState.Entitled
                && set["INV"] == EntitlementState.Entitled,
            string.Join("، ", set.OrderBy(k => k.Key)
                .Select(k => $"{k.Key}={EntitlementValidator.Ar(k.Value)}")));

        // ---- الحالات الثلاث عند حدّ الخدمة -----------------------------------
        var live = (await registry.FindByCodeAsync(tenant.TenantCode))!;

        var writeOk = await guard.RequireWriteAsync(live, "POS", "cashier1", "pos.sale");
        rec.Check("E4", "Entitled: القراءة والكتابة مسموحتان",
            writeOk == EntitlementState.Entitled, $"POS = {EntitlementValidator.Ar(writeOk)}");

        // خفض إلى قراءة فقط — مع اعتمادياتها، وإلا رُفضت المجموعة.
        await entitlements.ApplyAsync(tenant.TenantId,
            [new EntitlementChange("POS", EntitlementState.ReadOnly)],
            new ChangeAuthority("billing.dunning", "PAYMENT-FAILED-2026-03-01",
                "توقّف السداد — خفض وحدة نقاط البيع إلى قراءة فقط"));

        var readOk = await guard.RequireReadAsync(live, "POS", "cashier1", "pos.report");
        var writeDenied = false;
        var deniedMessage = "";
        try { await guard.RequireWriteAsync(live, "POS", "cashier1", "pos.sale"); }
        catch (EntitlementDeniedException ex) { writeDenied = true; deniedMessage = ex.Message; }

        rec.Check("E5", "ReadOnly: القراءة والتقارير متاحة، والإدخال والترحيل موقوفان",
            readOk == EntitlementState.ReadOnly && writeDenied,
            $"القراءة: مسموحة ({EntitlementValidator.Ar(readOk)})\nالكتابة: {deniedMessage}");

        var notEntitledDenied = false;
        try { await guard.RequireReadAsync(live, "PAY", "hr1", "payroll.read"); }
        catch (EntitlementDeniedException ex)
        { notEntitledDenied = ex.State == EntitlementState.NotEntitled; }

        var visible = await guard.VisibleModulesAsync(tenant.TenantId);
        rec.Check("E6", "NotEntitled: مرفوضة عند الخدمة ومخفيّة من القائمة",
            notEntitledDenied && visible.All(v => v.Module.Code != "PAY"),
            "الوحدات الظاهرة: " + string.Join("، ",
                visible.Select(v => $"{v.Module.Code}({EntitlementValidator.Ar(v.State)})")));

        // ---- الإنفاذ عند الخدمة لا عند الواجهة -------------------------------
        var log = new OperationLog(o.ControlConnectionString);
        var refusedWrites = await log.CountAsync("pos.sale", OperationOutcome.Refused);
        var refusedReads = await log.CountAsync("payroll.read", OperationOutcome.Refused);
        rec.Check("E7", "كل رفض كُتب في سِرد العمليات قبل إرجاعه (فخ-08)",
            refusedWrites >= 1 && refusedReads >= 1,
            $"رفض كتابة POS = {refusedWrites}؛ رفض قراءة PAY = {refusedReads}");

        // ---- سجل التدقيق ------------------------------------------------------
        var audit = await entitlements.ReadAuditAsync(tenant.TenantId);
        var allHaveAuthority = audit.All(a => !string.IsNullOrWhiteSpace(a.Authority));
        var noAuthorityRejected = false;
        try
        {
            await entitlements.ApplyAsync(tenant.TenantId,
                [new EntitlementChange("REP", EntitlementState.Entitled)],
                new ChangeAuthority("someone", "   ", "بلا سند"));
        }
        catch (ArgumentException) { noAuthorityRejected = true; }

        rec.Check("E8", "كل تغيير استحقاق يحمل من ومتى وبأي سند — ولا تغيير بلا سند",
            audit.Count > 0 && allHaveAuthority && noAuthorityRejected,
            $"سطور التدقيق = {audit.Count}\n" + string.Join("\n", audit.Take(6).Select(a =>
                $"  {a.At:yyyy-MM-dd HH:mm:ss} {a.Module,-5} {a.Old,-11} ⇒ {a.New,-11} "
                + $"بواسطة {a.Actor} بسند «{a.Authority}»")));

        await ArchiveRefusalAsync(o, registry, entitlements, rec);
    }

    // =======================================================================

    /// <summary>
    /// الإثبات المركزي في ADR-0014: <b>الفحص السابق للأرشفة يرفض</b> حين يكون
    /// رصيد حساب تابع للوحدة غير صفري.
    /// </summary>
    private static async Task ArchiveRefusalAsync(ControlPlaneOptions o, TenantRegistry registry,
        EntitlementService entitlements, Recorder rec)
    {
        Recorder.Section("(هـ) أرشفة الوحدة — الفحص يرفض، ولا يُحذّر");

        var t = (await Harness.SimulateFleetAsync(o, registry, "arc", 1, 1))[0];
        var archiveSvc = new ModuleArchiveService(o, entitlements);

        await using var tc = await Db.OpenAsync(o.TenantOwnerConnectionString(t.DatabaseName));
        await Provisioning.SeedData.SeedChartOfAccountsAsync(tc);
        await Provisioning.SeedData.SeedRolesAsync(tc);
        await Provisioning.SeedData.SeedPeriodsAsync(tc, 2026);

        await using (var c = await Db.OpenAsync(o.ControlConnectionString))
            await PlanCatalog.SubscribeAsync(c, t.TenantId, "GROWTH", new DateOnly(2026, 1, 1));
        await entitlements.ApplyPlanAsync(t.TenantId, "GROWTH",
            new ChangeAuthority("sales.ops", "CONTRACT-2026-0099", "حزمة النمو"));

        // قيد يترك رصيداً في المخزون (1300) وفي الذمم الدائنة (2100).
        var posted = await Ledger.PostAsync(tc, "MAIN", 2026, "2026-02", new DateOnly(2026, 2, 20),
            "INV", "استلام بضاعة", [("1300", 25000.0000m, 0m), ("2100", 0m, 25000.0000m)]);
        var invNet = await Ledger.NetBalanceAsync(tc, "INV");

        rec.Check("E9", "قيد مُرحَّل ترك رصيداً غير صفري في وحدة المخزون",
            invNet != 0m, $"قيد رقم {posted.EntryNo}؛ صافي أرصدة INV = {Canon.Amount(invNet)} ريال");

        // محاولة أرشفة INV مع اعتماد كامل — يجب أن تُرفَض بسبب الرصيد.
        var refused = await archiveSvc.RequestArchiveAsync(t, "INV", "ops.lifecycle",
            new ArchiveApproval("مدير مالي", "APPROVAL-2026-0007"));

        rec.Check("E10", "⭐ الأرشفة مرفوضة لأن رصيداً غير صفري — الفحص يرفض ولا يُحذّر",
            !refused.Approved && refused.Failed.Any(f => f.Code == "gl.zero_balance"),
            refused.SummaryAr + "\n" + string.Join("\n",
                refused.Checks.Select(c => $"  [{(c.Passed ? "ok " : "REFUSE")}] {c.Code,-28} {c.DetailAr}")));

        // نُصفّر الرصيد بقيد عكسي، ونُغلق الفترات، ونُغلق المستندات، ونُلغي التابع.
        await Ledger.PostAsync(tc, "MAIN", 2026, "2026-02", new DateOnly(2026, 2, 28),
            "INV", "عكس استلام البضاعة", [("1300", 0m, 25000.0000m), ("2100", 25000.0000m, 0m)]);
        var invNetAfter = await Ledger.NetBalanceAsync(tc, "INV");

        // مستند مفتوح: يجب أن يمنع الأرشفة وحده.
        await Ledger.OpenDocumentAsync(tc, "INV", "GRN-0001");
        var stillRefused = await archiveSvc.RequestArchiveAsync(t, "INV", "ops.lifecycle",
            new ArchiveApproval("مدير مالي", "APPROVAL-2026-0008"));
        rec.Check("E11", "الرصيد صفر لكن مستند مفتوح — ما تزال مرفوضة",
            !stillRefused.Approved && stillRefused.Failed.Any(f => f.Code == "docs.none_open"),
            $"صافي INV = {Canon.Amount(invNetAfter)}؛ "
            + string.Join(" ؛ ", stillRefused.Failed.Select(f => f.DetailAr)));

        await Db.ExecAsync(tc, "update app.document set state = 'Posted' where module_code = 'INV'");
        await Ledger.ClosePeriodAsync(tc, "2026-02");

        // بلا اعتماد مُسمّى: ما تزال مرفوضة.
        var noApproval = await archiveSvc.RequestArchiveAsync(t, "INV", "ops.lifecycle", null);
        rec.Check("E12", "كل الفحوص الفنية اجتازت، لكن بلا اعتماد مُسمّى — مرفوضة",
            !noApproval.Approved && noApproval.Failed.Count == 1
                && noApproval.Failed[0].Code == "approval.named",
            string.Join(" ؛ ", noApproval.Failed.Select(f => f.DetailAr)));

        // الوحدات التابعة (POS وPRJ) ليست في خطة GROWTH فهي NotEntitled أصلاً.
        var approved = await archiveSvc.RequestArchiveAsync(t, "INV", "ops.lifecycle",
            new ArchiveApproval("مدير مالي", "APPROVAL-2026-0009"));

        var after = await entitlements.GetSetAsync(t.TenantId);
        rec.Check("E13", "بعد اجتياز الفحوص كلها: الوحدة تُؤرشَف — أي تصير ReadOnly، لا تُحذف",
            approved.Approved && after["INV"] == EntitlementState.ReadOnly,
            approved.SummaryAr + $"\n  حالة INV بعد الأرشفة = {EntitlementValidator.Ar(after["INV"])}"
            + "\n  البيانات والقيود باقية ومقروءة ومصدَّرة — «إلغاء التركيب» غير موجود في هذا المنتج.");

        // القيود التي رحّلتها الوحدة ما تزال موجودة بعد الأرشفة.
        var entries = await Db.ScalarAsync<long>(tc,
            "select count(*) from ledger.journal_entry where module_code = 'INV'");
        rec.Check("E14", "قيود الوحدة المؤرشفة ما تزال في الدفتر",
            entries == 2, $"قيود INV بعد الأرشفة = {entries}");

        // وحدة لا تُرحّل قيوداً: الاستثناء المُوثَّق في ADR-0014.
        rec.Check("E15", "وحدة لا تُرحّل قيوداً مُعلَّمة كذلك في الكتالوج (استثناء ADR-0014)",
            !ModuleCatalog.Require("REP").PostsJournal
            && ModuleCatalog.All.Where(m => m.Code != "REP").All(m => m.PostsJournal),
            "REP (التقارير التحليلية) لا تُرحّل قيوداً ⇒ إلغاء تركيبها ممكن فعلاً، "
            + "وهو الاستثناء الوحيد المذكور في «ولا ينقضه» بـADR-0014.");
    }
}
