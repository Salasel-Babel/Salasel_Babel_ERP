using BabelRelationalSpike.Db;
using BabelRelationalSpike.Proofs;
using BabelRelationalSpike.Support;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;

// ---------------------------------------------------------------------------
//  Salasel Babel ERP - "can we drop Marten?" spike
//  .NET 10 + PostgreSQL 16 + EF Core 10 + WolverineFx, with NO Marten anywhere.
//  اختبار استكشافي: هل يمكن الاستغناء عن Marten؟
// ---------------------------------------------------------------------------

var only = args.FirstOrDefault(a => a.StartsWith("--only=", StringComparison.Ordinal))?["--only=".Length..];
var skipBench = args.Contains("--no-bench");

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("=========================================================================");
Console.WriteLine("  Salasel Babel ERP  -  relational stack spike (NO MARTEN)");
Console.WriteLine("  .NET 10 + PostgreSQL 16 + EF Core 10 + WolverineFx");
Console.WriteLine("=========================================================================");
Console.WriteLine($"  .NET runtime : {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");
Console.WriteLine($"  CPU cores    : {Environment.ProcessorCount}");

await Bootstrap.EnsureDatabaseAndRoleAsync();
Console.WriteLine($"  PostgreSQL   : {await Bootstrap.ServerFactsAsync()}");
await Bootstrap.ApplyDdlAsync();
await Ledger.EnsureBookAsync("MAIN");
await Ledger.EnsureBookAsync("OUTBOX");
await Ledger.EnsureBookAsync("BENCH");
await Ledger.EnsureBookAsync("TAMPER");

var builder = Host.CreateApplicationBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

// Wolverine's durable message store lives in plain PostgreSQL tables created
// by WolverineFx.Postgresql. No Marten, no IDocumentStore, no mt_events.
builder.UseWolverine(opts =>
{
    opts.UseRuntimeCompilation();
    opts.PersistMessagesWithPostgresql(Config.Admin, Config.WolverineSchema);
    opts.Policies.UseDurableLocalQueues();
    opts.Durability.Mode = DurabilityMode.Solo;
});

// EF Core 10 is the transaction owner that the outbox enrols into.
builder.Services.AddDbContextWithWolverineIntegration<LedgerDbContext>(
    o => o.UseNpgsql(Config.App).EnableSensitiveDataLogging(false),
    Config.WolverineSchema);

using var host = builder.Build();
await host.StartAsync();
await Bootstrap.GrantWolverineToAppRoleAsync();

Console.Write(Versions.Render());



var rec = new ProofRecorder();
var exitCode = 0;
bool Run(string id) => only is null || only.Contains(id, StringComparison.OrdinalIgnoreCase);

try
{
    if (Run("A")) await ProofA_Outbox.RunAsync(host.Services, rec);
    if (Run("B")) await ProofB_Ledger.RunAsync(host.Services, rec);
    if (Run("C")) await ProofC_EventLog.RunAsync(host.Services, rec);
    if (Run("D")) await ProofD_TenantDocs.RunAsync(host.Services, rec);
    if (Run("E")) await ProofE_HashChain.RunAsync(host.Services, rec);
    if (!skipBench) await Benchmark.RunAsync(rec);
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("!!! the spike aborted with an unhandled exception:");
    Console.WriteLine(ex);
    exitCode = 2;
}

rec.PrintSummary();
await host.StopAsync();
return exitCode != 0 ? exitCode : (rec.AllPassed ? 0 : 1);
