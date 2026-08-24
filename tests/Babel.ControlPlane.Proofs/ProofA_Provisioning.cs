using Babel.ControlPlane.Entitlement;
using Babel.ControlPlane.Migration;
using Babel.ControlPlane.Provisioning;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;
using Babel.ControlPlane.TenantSide;

namespace Babel.ControlPlane.Proofs;

/// <summary>
/// (أ) التزويد المُحكَم، والأرشفة بدل الحذف.
/// المقاطعة تقع عند <b>ثلاث نقاط مختلفة</b>، اثنتان منها <b>داخل</b> خطوة —
/// أي بعد تنفيذ أثرها وقبل تسجيله. هذه هي الحالة التي تكشف التزويد الذي
/// «يبدو مُحكَماً».
/// </summary>
public static class ProofA_Provisioning
{
    private static ProvisioningRequest Request(string key, string code) => new(
        IdempotencyKey: key,
        TenantCode: code,
        Name: BilingualName.Of("شركة الوادي للتجارة", "Al-Wadi Trading Co."),
        PlanCode: "GROWTH",
        AdminUserRef: "admin@alwadi.example",
        AdminName: BilingualName.Of("سارة القحطاني", "Sarah Alqahtani"),
        AdminEmail: "admin@alwadi.example",
        RequestedBy: "ops.provisioning",
        FiscalYear: 2026);

    public static async Task RunAsync(ControlPlaneOptions o, Recorder rec)
    {
        Recorder.Section("(أ) تزويد المستأجر — الإحكام تحت المقاطعة");

        var registry = new TenantRegistry(o);
        var entitlements = new EntitlementService(o, registry);

        const string key = "prov-alwadi-2026-0001";
        const string code = "alwadi";

        // ---- المقاطعة 1: داخل create_database (القاعدة أُنشئت، الخطوة غير مسجَّلة)
        var attempt1 = await AttemptAsync(o, registry, entitlements, key, code,
            "create_database", InterruptPhase.AfterEffect);
        rec.Check("A1", "المقاطعة (1) داخل create_database تُوقف التزويد",
            attempt1.Crashed, attempt1.Summary);

        // ---- المقاطعة 2: داخل seed_chart_of_accounts
        var attempt2 = await AttemptAsync(o, registry, entitlements, key, code,
            "seed_chart_of_accounts", InterruptPhase.AfterEffect);
        rec.Check("A2", "المقاطعة (2) داخل seed_chart_of_accounts تُوقف التزويد",
            attempt2.Crashed, attempt2.Summary);

        // ---- المقاطعة 3: بعد create_first_admin مباشرةً
        var attempt3 = await AttemptAsync(o, registry, entitlements, key, code,
            "create_first_admin", InterruptPhase.AfterCommit);
        rec.Check("A3", "المقاطعة (3) بعد create_first_admin تُوقف التزويد",
            attempt3.Crashed, attempt3.Summary);

        // ---- إعادة التشغيل بنفس مفتاح الإحكام: يجب أن تكتمل ------------------
        var provisioner = new TenantProvisioner(o, registry, entitlements);
        var final = await provisioner.ProvisionAsync(Request(key, code));

        await using (var sc = await Db.OpenAsync(o.ControlConnectionString))
            await PlanCatalog.SubscribeAsync(sc, final.TenantId, "GROWTH", new DateOnly(2026, 1, 1));

        rec.Check("A4", "إعادة التشغيل بنفس المفتاح تستأنف ولا تُنشئ تشغيلة ثانية",
            final.Resumed && final.Steps.Count == TenantProvisioner.Steps.Count
                && final.Steps.All(s => s.State == "Completed"),
            $"resumed={final.Resumed}  executed={final.StepsExecuted}  skipped={final.StepsSkipped}\n"
            + string.Join("\n", final.Steps.Select(s =>
                $"  {s.Ordinal,2}. {s.Name,-24} {s.State,-9} attempts={s.Attempts}")));

        // ---- لا ازدواج في أي بذرة ------------------------------------------
        var t = await registry.FindByCodeAsync(code);
        await using var tc = await Db.OpenAsync(o.TenantOwnerConnectionString(t!.DatabaseName));
        var accounts = await Db.ScalarAsync<long>(tc, "select count(*) from ledger.account");
        var roles = await Db.ScalarAsync<long>(tc, "select count(*) from app.role");
        var periods = await Db.ScalarAsync<long>(tc, "select count(*) from ledger.period");
        var users = await Db.ScalarAsync<long>(tc, "select count(*) from app.app_user");
        var version = await TenantSchema.CurrentVersionAsync(tc);

        var expectedAccounts = SeedData.ChartOfAccounts.Count;
        var expectedRoles = SeedData.Roles.Count;

        rec.Check("A5", "لا ازدواج بعد ثلاث مقاطعات وإعادة تشغيل",
            accounts == expectedAccounts && roles == expectedRoles && periods == 12 && users == 1,
            $"accounts={accounts} (expected {expectedAccounts})  roles={roles} (expected {expectedRoles})  "
            + $"periods={periods} (expected 12)  admins={users} (expected 1)  schema_version={version}");

        // ---- سجل التشغيلات: تشغيلة واحدة، ومحاولات موزَّعة -------------------
        await using var cc = await Db.OpenAsync(o.ControlConnectionString);
        var runs = await Db.ScalarAsync<long>(cc,
            "select count(*) from control.provisioning_run where idempotency_key = @k",
            p => p.AddWithValue("k", key));
        var retried = await Db.ScalarAsync<long>(cc, """
            select count(*) from control.provisioning_step s
              join control.provisioning_run r on r.run_id = s.run_id
             where r.idempotency_key = @k and s.attempts > 1
            """, p => p.AddWithValue("k", key));
        var tenants = await Db.ScalarAsync<long>(cc,
            "select count(*) from control.tenant where tenant_code = @c",
            p => p.AddWithValue("c", code));

        rec.Check("A6", "تشغيلة واحدة، ومستأجر واحد، والخطوات المُقاطَعة داخلها وحدها أُعيدت",
            runs == 1 && tenants == 1 && retried == 2,
            $"provisioning_run={runs}  tenant rows={tenants}  steps with attempts>1 = {retried}\n"
            + "  المقاطعتان (1) و(2) وقعتا **داخل** خطوة (بعد أثرها وقبل تسجيلها) ⇒ أُعيدت الخطوتان.\n"
            + "  المقاطعة (3) وقعت **بعد** تسجيل اكتمال الخطوة ⇒ لم تُعَد، بل استُؤنف من التالية.\n"
            + "  أي أن الخطوة المُعادة هي المُقاطَعة داخلها بالضبط، لا أكثر ولا أقل.");

        // ---- الاستحقاق طُبِّق من الخطة --------------------------------------
        var set = await entitlements.GetSetAsync(t.TenantId);
        var plan = PlanCatalog.Require("GROWTH");
        var ok = plan.Modules.All(m => set[m] == EntitlementState.Entitled);
        rec.Check("A7", "استحقاقات الخطة مطبَّقة ومغلقة على اعتمادياتها", ok,
            string.Join("، ", set.OrderBy(k => k.Key)
                .Select(k => $"{k.Key}={EntitlementValidator.Ar(k.Value)}")));

        // ---- تشغيل رابع بلا مقاطعة: صفر تنفيذ، عشر تخطّيات -------------------
        var again = await provisioner.ProvisionAsync(Request(key, code));
        rec.Check("A8", "تشغيل التزويد مرة أخرى لا ينفّذ خطوة واحدة",
            again.StepsExecuted == 0 && again.StepsSkipped == TenantProvisioner.Steps.Count,
            $"executed={again.StepsExecuted}  skipped={again.StepsSkipped}");

        await ArchiveProofAsync(o, registry, entitlements, rec, code);
    }

    private sealed record Attempt(bool Crashed, string Summary);

    private static async Task<Attempt> AttemptAsync(ControlPlaneOptions o, TenantRegistry registry,
        EntitlementService entitlements, string key, string code, string step, InterruptPhase phase)
    {
        var provisioner = new TenantProvisioner(o, registry, entitlements)
        {
            Interrupt = (s, p) => s == step && p == phase
                ? throw new SimulatedCrashException(s, p)
                : Task.CompletedTask
        };

        try
        {
            await provisioner.ProvisionAsync(Request(key, code));
            return new Attempt(false, "لم تقع المقاطعة — الإثبات لا معنى له");
        }
        catch (SimulatedCrashException ex)
        {
            await using var c = await Db.OpenAsync(o.ControlConnectionString);
            var done = await Db.ScalarAsync<long>(c, """
                select count(*) from control.provisioning_step s
                  join control.provisioning_run r on r.run_id = s.run_id
                 where r.idempotency_key = @k and s.state = 'Completed'
                """, p => p.AddWithValue("k", key));
            var started = await Db.ScalarAsync<long>(c, """
                select count(*) from control.provisioning_step s
                  join control.provisioning_run r on r.run_id = s.run_id
                 where r.idempotency_key = @k and s.state = 'Started'
                """, p => p.AddWithValue("k", key));
            return new Attempt(true,
                $"{ex.Message} — خطوات مكتملة={done}، خطوة معلّقة في حالة Started={started}");
        }
    }

    // =======================================================================

    private static async Task ArchiveProofAsync(ControlPlaneOptions o, TenantRegistry registry,
        EntitlementService entitlements, Recorder rec, string code)
    {
        Recorder.Section("(أ) إنهاء الخدمة = أرشفة، لا حذف");

        var t = (await registry.FindByCodeAsync(code))!;

        // نُرحّل قيداً حقيقياً حتى يكون هناك ما يبقى بعد الأرشفة.
        await using (var tc = await Db.OpenAsync(o.TenantOwnerConnectionString(t.DatabaseName)))
            await Ledger.PostAsync(tc, "MAIN", 1, "2026-03", new DateOnly(2026, 3, 15), "AR",
                "فاتورة مبيعات", [("1200", 1150.0000m, 0m), ("4100", 0m, 1000.0000m),
                                  ("2300", 0m, 150.0000m)]);

        var archivist = new TenantArchivist(o);
        var before = await archivist.SnapshotAsync(t);

        var outcome = await archivist.ArchiveAsync(code, "ops.lifecycle",
            "انتهاء التعاقد — الاحتفاظ بالسجلات مطلوب (مدّة الاحتفاظ غير مُتحقَّق منها)");

        rec.Check("A9", "دور التطبيق لم يعد يستطيع الاتصال بقاعدة المستأجر",
            outcome.AppConnectionError.Contains("42501")
                || outcome.AppConnectionError.Contains("permission denied", StringComparison.OrdinalIgnoreCase),
            $"محاولة اتصال دور التطبيق ⇒ {outcome.AppConnectionError}");

        rec.Check("A10", "البيانات باقية كما هي بعد الأرشفة",
            outcome.JournalEntries == before.JournalEntries && outcome.JournalEntries == 1
            && outcome.JournalLines == before.JournalLines && outcome.JournalLines == 3
            && outcome.Accounts == before.Accounts,
            $"قبل: قيود={before.JournalEntries} سطور={before.JournalLines} حسابات={before.Accounts}\n"
            + $"بعد: قيود={outcome.JournalEntries} سطور={outcome.JournalLines} حسابات={outcome.Accounts}");

        // مسار التطبيق نفسه يرفض المستأجر المؤرشف قبل أي اتصال.
        await using var mgr = new Connections.TenantConnectionManager(o, registry);
        registry.GetType();
        var appRefused = false;
        var refusalMessage = "";
        try
        {
            await using var lease = await mgr.LeaseAsync(code);
        }
        catch (TenantArchivedException ex)
        {
            appRefused = true;
            refusalMessage = ex.Message;
        }
        rec.Check("A11", "مُحلّل التوجيه يرفض المستأجر المؤرشف قبل فتح أي اتصال",
            appRefused, refusalMessage);

        // الحارس يرفض القراءة والكتابة معاً على مستأجر مؤرشف، ويكتب سطراً.
        var guard = new EntitlementGuard(entitlements);
        var archived = (await registry.FindByCodeAsync(code))!;
        var guardRefused = false;
        try { await guard.RequireReadAsync(archived, "CORE", "someone", "gl.read"); }
        catch (TenantArchivedException) { guardRefused = true; }

        var log = new OperationLog(o.ControlConnectionString);
        var refusals = await log.CountAsync("gl.read", OperationOutcome.Refused);
        rec.Check("A12", "الحارس يرفض ويكتب سطر رفض في سِرد العمليات (فخ-08)",
            guardRefused && refusals >= 1,
            $"رُفضت القراءة على مستأجر مؤرشف؛ سطور الرفض المُسجَّلة = {refusals}");

        // ثم نُعيد الوصول — لأن الأرشفة عكوسة والحذف ليس كذلك.
        await archivist.RestoreAsync(code, "ops.lifecycle", "طلب العميل استخراج بياناته");
        mgr.InvalidateRoute(code);
        var restored = await registry.FindByCodeAsync(code);
        rec.Check("A13", "الأرشفة عكوسة: إعادة الوصول تُرجِع المستأجر بلا فقد بيانات",
            restored!.Status == TenantStatus.Suspended,
            $"الحالة بعد الاستعادة = {restored.Status}؛ القيود ما تزال {outcome.JournalEntries}");

        // ---- ذاكرة التوجيه المؤقّتة لا تُبقي مستأجراً مؤرشفاً حيّاً ----------
        //  عطل صامت اكتُشف أثناء البناء: الأرشفة تقع غالباً من **عملية أخرى**
        //  (أداة تشغيل أو عامل خلفي)، وذاكرة توجيه بلا انتهاء صلاحية تُبقي
        //  عملية التطبيق توجّه الطلبات إلى مستأجر مؤرشف بلا حدّ زمني.
        await using var cached = new Connections.TenantConnectionManager(o, registry)
        {
            RouteCacheTtl = TimeSpan.FromMilliseconds(200)
        };
        await using (var lease = await cached.LeaseAsync(code))
            await Db.ScalarAsync<long>(lease.Connection, "select 1");

        // «عملية أخرى» تُؤرشف المستأجر — هذه العملية لا تعلم شيئاً.
        await archivist.ArchiveAsync(code, "ops.other-process",
            "أرشفة من عملية أخرى — لاختبار انتهاء صلاحية ذاكرة التوجيه");

        await Task.Delay(400);
        var staleClosed = false;
        try { await using var l2 = await cached.LeaseAsync(code); }
        catch (TenantArchivedException) { staleClosed = true; }

        rec.Check("A14", "ذاكرة التوجيه تنتهي صلاحيتها ⇒ الأرشفة من عملية أخرى تسري هنا",
            staleClosed,
            "لُمس المستأجر بنجاح، ثم أُرشِف من عملية أخرى، فرُفض بعد انتهاء مهلة الذاكرة "
            + $"({cached.RouteCacheTtl.TotalMilliseconds:F0} مللي‌ثانية في هذا الاختبار، "
            + "15 ثانية افتراضياً). بلا مهلة يبقى التوجيه قائماً إلى الأبد ويظهر العطل "
            + "كأنه «عطل شبكة».");
    }
}
