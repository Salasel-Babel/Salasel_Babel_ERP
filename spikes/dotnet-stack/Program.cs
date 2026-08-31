using BabelSpike;
using JasperFx;
using JasperFx.Events.Projections;
using Marten;
using Wolverine;
using Wolverine.Http;
using Wolverine.Marten;

// ---------------------------------------------------------------------------
// Connection string. NEVER hard-code credentials here. Override with:
//     export BABEL_SPIKE_DB="Host=...;Port=5432;Database=babel_spike;Username=...;Password=..."
// The default assumes a local dev Postgres reachable without a password
// (see README.md for the one-time local setup).
// ---------------------------------------------------------------------------
var connectionString = Environment.GetEnvironmentVariable("BABEL_SPIKE_DB")
    ?? "Host=127.0.0.1;Port=5432;Database=babel_spike;Username=postgres";

var builder = WebApplication.CreateBuilder(args);
builder.Logging.SetMinimumLevel(LogLevel.Warning);

builder.Services.AddMarten(opts =>
    {
        opts.Connection(connectionString);
        opts.DatabaseSchemaName = ProofDecimal.Schema;
        opts.AutoCreateSchemaObjects = AutoCreate.All;

        // (c) event store projection
        opts.Projections.Add<LedgerStateProjection>(ProjectionLifecycle.Inline);

        // (e) conjoined multi-tenancy: one table, a tenant_id column
        opts.Policies.ForAllDocuments(m => { });
        opts.Schema.For<TenantScopedDoc>().MultiTenanted();

        // (a) duplicated column so money can also be summed as real NUMERIC
        opts.Schema.For<LedgerLine>()
            .Duplicate(x => x.Amount, pgType: "numeric(19,4)")
            .Duplicate(x => x.Account);
    })
    // (d) Wolverine's message storage lives in the same Postgres database,
    // which is what makes the outbox share the document transaction.
    .IntegrateWithWolverine();

builder.Services.AddWolverineHttp();

builder.Host.UseWolverine(opts =>
{
    // durable local queues => outgoing envelopes are persisted in Postgres
    opts.UseRuntimeCompilation();
    opts.Policies.UseDurableLocalQueues();
    opts.Durability.Mode = DurabilityMode.Solo;
});

var app = builder.Build();
app.MapGet("/", () => "Salasel Babel ERP - .NET 10 / Marten / Wolverine spike");
app.MapWolverineEndpoints();

// --serve keeps the web host running; the default runs the proofs and exits.
if (args.Contains("--serve"))
{
    await app.RunAsync();
    return 0;
}

Console.WriteLine("=================================================================");
Console.WriteLine("  Salasel Babel ERP - PostgreSQL + Marten + Wolverine on .NET 10");
Console.WriteLine("=================================================================");
Console.WriteLine($"  .NET runtime : {Environment.Version} ({System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription})");

await app.StartAsync();

var store = app.Services.GetRequiredService<IDocumentStore>();
var recorder = new ProofRecorder();

// Make the spike re-runnable: clear any data left by a previous run so the
// control totals below are computed over exactly what this run wrote.
await store.Advanced.ResetAllData();

await using (var conn = new Npgsql.NpgsqlConnection(connectionString))
{
    await conn.OpenAsync();
    await using var cmd = new Npgsql.NpgsqlCommand("select version()", conn);
    Console.WriteLine($"  PostgreSQL   : {await cmd.ExecuteScalarAsync()}");
}
Console.WriteLine($"  Npgsql       : {typeof(Npgsql.NpgsqlConnection).Assembly.GetName().Version}");
Console.WriteLine($"  Marten       : {typeof(IDocumentStore).Assembly.GetName().Version}");
Console.WriteLine($"  Wolverine    : {typeof(IMessageBus).Assembly.GetName().Version}");
Console.WriteLine();

var exitCode = 0;
try
{
    Console.WriteLine("--- (a) decimal precision ---------------------------------------");
    await ProofDecimal.RunAsync(store, recorder, connectionString);

    Console.WriteLine("--- (b) balanced journal entry ----------------------------------");
    await ProofLedger.ProveBalancedEntryAsync(store, recorder);

    Console.WriteLine("--- (c) event store + projection rebuild ------------------------");
    await ProofLedger.ProveEventStoreAsync(store, recorder);

    Console.WriteLine("--- (d) wolverine transactional outbox --------------------------");
    await ProofLedger.ProveOutboxAsync(app.Services, store, recorder, connectionString);

    Console.WriteLine("--- (e) multi-tenancy + row level security ----------------------");
    await ProofLedger.ProveMultiTenancyAsync(store, recorder, connectionString);
}
catch (Exception ex)
{
    Console.WriteLine();
    Console.WriteLine("!!! the spike aborted with an unhandled exception:");
    Console.WriteLine(ex);
    exitCode = 2;
}

recorder.PrintSummary();
await app.StopAsync();

return exitCode != 0 ? exitCode : (recorder.AllPassed ? 0 : 1);
