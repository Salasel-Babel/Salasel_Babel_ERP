using Babel.ControlPlane.Migration;
using Babel.ControlPlane.Proofs;
using Babel.ControlPlane.Registry;
using Babel.ControlPlane.Support;

Console.OutputEncoding = System.Text.Encoding.UTF8;

var only = args.FirstOrDefault(a => a.StartsWith("--only=", StringComparison.Ordinal))?["--only=".Length..];
var role = args.FirstOrDefault(a => a.StartsWith("--role=", StringComparison.Ordinal))?["--role=".Length..];
var keep = args.Contains("--keep");

// ---------------------------------------------------------------------------
//  دور «عامل الأسطول»: عملية منفصلة تُقتل بـSIGKILL في الإثبات (ب).
// ---------------------------------------------------------------------------
if (role == "fleet-worker")
{
    var wo = Harness.Options();
    var wid = args.FirstOrDefault(a => a.StartsWith("--worker=", StringComparison.Ordinal))?["--worker=".Length..] ?? "worker";
    var mid = Guid.Parse(args.First(a => a.StartsWith("--migration=", StringComparison.Ordinal))["--migration=".Length..]);
    var delay = int.TryParse(Environment.GetEnvironmentVariable("BABEL_CP_PROOF_DELAY_MS"), out var d)
        ? d : 0;

    var wreg = new TenantRegistry(wo);
    var wrunner = new FleetMigrationRunner(wo, wreg);
    if (delay > 0) wrunner.AfterEach = async _ => await Task.Delay(delay);

    var report = await wrunner.RunAsync(mid, wid);
    Console.WriteLine($"worker {wid}: processed={report.Processed} failed={report.Failed}");
    return 0;
}

// ---------------------------------------------------------------------------
Console.WriteLine("=========================================================================");
Console.WriteLine("  سلاسل بابل — مستوى التحكّم (Control Plane)");
Console.WriteLine("  إثباتات: التزويد · ترحيل الأسطول · الاتصالات · الاستحقاق · القياس");
Console.WriteLine("=========================================================================");
Console.WriteLine($"  .NET runtime : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"  CPU cores    : {Environment.ProcessorCount}");

var o = Harness.Options();
Console.WriteLine($"  control db   : {o.ControlDatabase}   (app role: {o.AppRole})");

await using (var probe = await Db.OpenAsync(o.MaintenanceConnectionString))
    Console.WriteLine($"  PostgreSQL   : {await Db.ScalarAsync<string>(probe, "select version()")}");

Console.WriteLine();
Console.WriteLine("  تهيئة نظيفة: تُحذف كل قواعد الاختبار وتُعاد قاعدة التحكّم …");
await Harness.ResetAsync(o);

var rec = new Recorder();
var exit = 0;
bool Run(string id) => only is null || only.Contains(id, StringComparison.OrdinalIgnoreCase);

try
{
    if (Run("A")) await ProofA_Provisioning.RunAsync(o, rec);
    if (Run("B")) await ProofB_FleetMigration.RunAsync(o, rec);
    if (Run("C")) await ProofC_ExpandContract.RunAsync(o, rec);
    if (Run("D")) await ProofD_Connections.RunAsync(o, rec);
    if (Run("E")) await ProofE_Entitlement.RunAsync(o, rec);
    if (Run("F")) await ProofF_Metering.RunAsync(o, rec);
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("!!! توقّفت الإثباتات باستثناء غير معالَج:");
    Console.WriteLine(ex);
    exit = 2;
}

rec.PrintSummary();

if (!keep)
{
    Console.WriteLine();
    Console.WriteLine("  تنظيف قواعد الاختبار … (‏--keep للإبقاء عليها)");
    try { await Harness.DropAllTestDatabasesAsync(o); }
    catch (Exception ex) { Console.WriteLine("  تعذّر التنظيف: " + ex.Message); }
}

return exit != 0 ? exit : (rec.AllPassed ? 0 : 1);
