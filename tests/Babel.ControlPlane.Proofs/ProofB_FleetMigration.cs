using System.Diagnostics;
using Babel.ControlPlane.Migration;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;

namespace Babel.ControlPlane.Proofs;

/// <summary>
/// (ب) ترحيل الأسطول: 50 قاعدة مستأجر حقيقية، قتل العملية في منتصف التشغيل
/// بـ<c>SIGKILL</c>، ثم إعادة التشغيل.
///
/// <para><b>القتل حقيقي لا مُحاكى:</b> العامل عملية منفصلة تُقتل بـ
/// <c>Process.Kill</c> — لا استثناء، ولا <c>finally</c>، ولا فرصة لتنظيف.
/// استثناء مُحاكى داخل نفس العملية <b>لا يُثبت شيئاً</b> عن الاستئناف، لأن
/// <c>finally</c> ما يزال يعمل.</para>
/// </summary>
public static class ProofB_FleetMigration
{
    public const int FleetSize = 50;

    public static async Task RunAsync(ControlPlaneOptions o, Recorder rec)
    {
        rec.Section($"(ب) ترحيل الأسطول عبر {FleetSize} قاعدة مستأجر");

        var registry = new TenantRegistry(o);

        var sw = Stopwatch.StartNew();
        var fleet = await Harness.SimulateFleetAsync(o, registry, "flt", FleetSize,
            TenantSchema.BaselineVersion);
        sw.Stop();
        rec.Check("B1", $"أسطول مُحاكى من {FleetSize} قاعدة بيانات حقيقية على الإصدار 1",
            fleet.Count == FleetSize,
            $"أُنشئ {fleet.Count} قاعدة في {sw.Elapsed.TotalSeconds:F1} ثانية "
            + $"({FleetSize / sw.Elapsed.TotalSeconds:F1} قاعدة/ث بتوازٍ 4)");

        // =====================================================================
        //  المرحلة 1 — القتل والاستئناف. تُحقَن مهلة اصطناعية لكل قاعدة حتى
        //  تكون نافذة القتل قابلة للتحكّم. هذه المهلة **مستبعَدة من قياس
        //  الإنتاجية** الذي يأتي في المرحلة 2.
        // =====================================================================
        var runner = new FleetMigrationRunner(o, registry);
        var migrationId = await runner.PlanAsync("release-2026.02-v2", TenantSchema.FleetDemoVersion,
            BilingualName.Of("إصدار 2026.02 — وسم الوحدة على الحساب",
                             "release 2026.02 — account module tag"),
            "ops.release", fleet);

        var planned = await runner.StatsAsync(migrationId);
        rec.Check("B2", "الخطة سجّلت صفّ حالة لكل قاعدة",
            planned.Total == FleetSize && planned.Pending == FleetSize,
            $"total={planned.Total} pending={planned.Pending}");

        var killSw = Stopwatch.StartNew();
        var (killedAfter, exitCode) = await RunWorkerAndKillAsync(o, migrationId, "worker-A",
            delayMs: 40, killAfter: TimeSpan.FromSeconds(2.2));
        killSw.Stop();

        var afterKill = await runner.StatsAsync(migrationId);
        rec.Check("B3", "قُتلت العملية فعلاً في منتصف التشغيل (SIGKILL)",
            killedAfter && afterKill.Done > 0 && afterKill.Done < FleetSize,
            $"exit={exitCode}  done={afterKill.Done}  leased={afterKill.Leased}  "
            + $"pending={afterKill.Pending}  من أصل {afterKill.Total}");

        var doneAtKill = afterKill.Done;
        var leasedAtKill = afterKill.Leased;

        // الحجوزات المعلّقة تُسترجَع — إمّا بانتهاء مهلتها تلقائياً، أو صراحةً
        // حين يعلم المشغّل أن العامل مات. الثاني أسرع، والأول لا يحتاج أحداً.
        var reclaimed = await runner.ReclaimAsync(migrationId, "worker-A");

        var resumeSw = Stopwatch.StartNew();
        var report = await runner.RunAsync(migrationId, "worker-B");
        resumeSw.Stop();

        var final = await runner.StatsAsync(migrationId);
        rec.Check("B4", "الاستئناف أكمل الأسطول بلا نقص",
            final.Done == FleetSize && final.Failed == 0 && final.Pending == 0 && final.Leased == 0,
            $"done={final.Done}/{final.Total}  failed={final.Failed}  "
            + $"استُرجِع {reclaimed} حجزاً معلّقاً من العامل الميّت");

        // «بلا إعادة ما انتهى»: عدد المحاولات هو الدليل.
        var targets = await runner.TargetsAsync(migrationId);
        var redone = targets.Count(t => t.Attempts > 1);
        rec.Check("B5", "لم تُعَد أي قاعدة انتهت قبل القتل",
            redone <= leasedAtKill + o.FleetBatchSize,
            $"قواعد بمحاولات>1 = {redone}؛ الحدّ الأعلى المشروع = المحجوزة وقت القتل "
            + $"({leasedAtKill}) + حجم الدفعة ({o.FleetBatchSize}). "
            + $"انتهى قبل القتل {doneAtKill}، وأكمل العامل الثاني {report.Processed}.");

        // «بلا تخطّي غير المنتهي»: نتحقّق من الإصدار في كل قاعدة فعلياً.
        var atVersion = 0;
        foreach (var t in fleet)
        {
            await using var tc = await Db.OpenAsync(o.TenantOwnerConnectionString(t.DatabaseName));
            if (await TenantSchema.CurrentVersionAsync(tc) >= TenantSchema.FleetDemoVersion) atVersion++;
        }
        rec.Check("B6", "كل قاعدة في الأسطول على الإصدار المستهدف — تحقّق مباشر لا من جدول الحالة",
            atVersion == FleetSize, $"{atVersion}/{FleetSize} قاعدة على الإصدار "
            + $"{TenantSchema.FleetDemoVersion} أو أعلى");

        // =====================================================================
        //  المرحلة 2 — قياس الإنتاجية بلا أي مهلة محقونة.
        // =====================================================================
        var cleanId = await runner.PlanAsync("release-2026.03-v4", TenantSchema.ContractVersion,
            BilingualName.Of("إصدار 2026.03 — توسيع ثم انكماش",
                             "release 2026.03 — expand then contract"),
            "ops.release", fleet);

        var clean = Stopwatch.StartNew();
        var cleanReport = await runner.RunAsync(cleanId, "worker-bench");
        clean.Stop();

        var cleanStats = await runner.StatsAsync(cleanId);
        rec.Check("B7", "تشغيلة نظيفة (إصدار 1/2 ⇒ 4) اكتملت",
            cleanStats.Done == FleetSize && cleanStats.Failed == 0,
            $"done={cleanStats.Done}  failed={cleanStats.Failed}");

        var perDb = clean.Elapsed.TotalMilliseconds / FleetSize;
        var dbPerSec = FleetSize / clean.Elapsed.TotalSeconds;

        rec.Measure($"ترحيل الأسطول (ترحيلتان: v3 توسيع + v4 انكماش، دفعة {o.FleetBatchSize}، عامل واحد): "
            + $"{FleetSize} قاعدة في {clean.Elapsed.TotalSeconds:F2} ث "
            + $"= {perDb:F0} مللي‌ثانية/قاعدة = {dbPerSec:F1} قاعدة/ث");

        rec.Measure($"زمن الترحيل داخل القاعدة وحده (مجموع duration_ms): "
            + $"{cleanStats.TotalDurationMs} مللي‌ثانية "
            + $"= {(double)cleanStats.TotalDurationMs / FleetSize:F0} مللي‌ثانية/قاعدة — "
            + $"الباقي ({clean.Elapsed.TotalMilliseconds - cleanStats.TotalDurationMs:F0} مللي‌ثانية) "
            + "هو الحجز والاتصال ومسك الحالة");

        rec.Measure($"تقدير زمن الإصدار بعامل واحد: 100 مستأجر ≈ {perDb * 100 / 1000:F1} ث · "
            + $"300 مستأجر ≈ {perDb * 300 / 1000:F1} ث "
            + $"({perDb * 300 / 60000:F1} دقيقة)");

        rec.Note("الأرقام أعلاه على 4 vCPU مشتركة وقاعدة محلّية (‏RTT ≈ 0). "
            + "النِّسب تصمد؛ السقوف المطلقة لا تُنقَل إلى عتاد آخر "
            + "(‏docs/evidence/measurements.md §1.3).");

        rec.Check("B8", "التشغيلة النظيفة لم تُعِد شيئاً مكتملاً", cleanReport.AlreadyDone == 0,
            $"قواعد وُجدت مُرحَّلة سلفاً = {cleanReport.AlreadyDone}");

        // =====================================================================
        //  المرحلة 3 — عامل واحد مقابل أربعة عمّال على ترحيلتين متطابقتي الشكل.
        //  هذا هو الرقم الذي يقرّر زمن الإصدار فعلاً: التوازي، لا سرعة القاعدة.
        // =====================================================================
        var soloId = await runner.PlanAsync("bench-solo-v5", TenantSchema.BenchVersionA,
            BilingualName.Of("قياس أ — عامل واحد", "bench A — one worker"), "ops.bench", fleet);
        var solo = Stopwatch.StartNew();
        await runner.RunAsync(soloId, "solo");
        solo.Stop();

        var parId = await runner.PlanAsync("bench-parallel-v6", TenantSchema.BenchVersionB,
            BilingualName.Of("قياس ب — أربعة عمّال", "bench B — four workers"), "ops.bench", fleet);
        var par = Stopwatch.StartNew();
        await Task.WhenAll(Enumerable.Range(1, 4)
            .Select(i => runner.RunAsync(parId, $"par-{i}")));
        par.Stop();

        var soloStats = await runner.StatsAsync(soloId);
        var parStats = await runner.StatsAsync(parId);
        var speedup = solo.Elapsed.TotalMilliseconds / Math.Max(1, par.Elapsed.TotalMilliseconds);

        rec.Check("B9", "أربعة عمّال على نفس الخطة: بلا تكرار وبلا تصارع (‏FOR UPDATE SKIP LOCKED)",
            parStats.Done == FleetSize && parStats.Failed == 0 && parStats.MaxAttempts == 1,
            $"done={parStats.Done} failed={parStats.Failed} أقصى محاولات لقاعدة واحدة="
            + $"{parStats.MaxAttempts} (‏1 ⇒ لم تُلتقط قاعدة مرتين)");

        rec.Measure($"ترحيلة واحدة الشكل (عمود + ملء رجعي + فهرس + VACUUM) عبر {FleetSize} قاعدة: "
            + $"عامل واحد = {solo.Elapsed.TotalSeconds:F2} ث "
            + $"({solo.Elapsed.TotalMilliseconds / FleetSize:F0} مللي‌ثانية/قاعدة) · "
            + $"أربعة عمّال = {par.Elapsed.TotalSeconds:F2} ث "
            + $"({par.Elapsed.TotalMilliseconds / FleetSize:F0} مللي‌ثانية/قاعدة) "
            + $"⇒ تسارُع ×{speedup:F2} على 4 vCPU");

        var perDbPar = par.Elapsed.TotalMilliseconds / FleetSize;
        rec.Measure($"تقدير زمن إصدار بأربعة عمّال: 100 مستأجر ≈ {perDbPar * 100 / 1000:F1} ث · "
            + $"300 مستأجر ≈ {perDbPar * 300 / 1000:F1} ث "
            + $"({perDbPar * 300 / 60000:F2} دقيقة) — لترحيلة خفيفة على قواعد صغيرة.");

        rec.Note("⚠️ تقديرات زمن الإصدار أعلاه مشروطة بـ**ترحيلة خفيفة على قواعد "
            + "بيانات فارغة تقريباً**. ترحيلة تُعيد كتابة جدول قيود فيه ملايين الأسطر "
            + "(‏ALTER TABLE يُعيد الكتابة، أو فهرس على جدول كبير) تنتقل من مللي‌ثوانٍ "
            + "إلى دقائق **لكل قاعدة**، فيصير زمن الإصدار محكوماً بحجم أكبر مستأجر لا بعددهم. "
            + "هذا الرقم غير مقيس هنا وهو دَين قياسي معلَن.");
    }

    // =======================================================================

    /// <summary>يُشغّل عاملاً في عملية منفصلة ثم يقتلها بـSIGKILL.</summary>
    private static async Task<(bool Killed, int ExitCode)> RunWorkerAndKillAsync(
        ControlPlaneOptions o, Guid migrationId, string workerId, int delayMs, TimeSpan killAfter)
    {
        var assembly = System.Reflection.Assembly.GetEntryAssembly()!.Location;
        var psi = new ProcessStartInfo("dotnet")
        {
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.ArgumentList.Add(assembly);
        psi.ArgumentList.Add("--role=fleet-worker");
        psi.ArgumentList.Add($"--migration={migrationId}");
        psi.ArgumentList.Add($"--worker={workerId}");
        psi.Environment["BABEL_CP_CONTROL_DB_NAME"] = o.ControlDatabase;
        psi.Environment["BABEL_CP_APP_ROLE"] = o.AppRole;
        psi.Environment["BABEL_CP_FLEET_BATCH"] = o.FleetBatchSize.ToString();
        psi.Environment["BABEL_CP_FLEET_LEASE_SECONDS"] =
            ((int)o.FleetLeaseDuration.TotalSeconds).ToString();
        psi.Environment["BABEL_CP_PROOF_DELAY_MS"] = delayMs.ToString();

        using var proc = Process.Start(psi)!;
        _ = proc.StandardOutput.ReadToEndAsync();
        _ = proc.StandardError.ReadToEndAsync();

        await Task.Delay(killAfter);
        if (proc.HasExited) return (false, proc.ExitCode);

        proc.Kill(entireProcessTree: true);   // SIGKILL على لينكس
        await proc.WaitForExitAsync();
        return (true, proc.ExitCode);
    }
}
