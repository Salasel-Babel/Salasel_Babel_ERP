using System.Diagnostics;
using System.Globalization;
using BabelPosOffline.Device;
using BabelPosOffline.Proofs;
using BabelPosOffline.Server;
using BabelPosOffline.Support;

var argv = args.ToList();
string Arg(string name, string dflt = "") =>
    argv.FirstOrDefault(a => a.StartsWith($"--{name}=", StringComparison.Ordinal))?.Split('=', 2)[1] ?? dflt;

Directory.CreateDirectory(Config.DeviceDir);

// ── أوضاع العملية الابن: تُقتل عمداً في منتصف العمل ─────────────────────────
var child = Arg("child");
if (child.Length > 0) return await RunChildAsync(child);

// ── التشغيل العادي ───────────────────────────────────────────────────────────
var only = Arg("only", "1234567").ToUpperInvariant();
var exePath = System.Reflection.Assembly.GetEntryAssembly()!.Location;

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine("سلاسل بابل — تجربة إثبات: نقطة بيع تعمل دون اتصال وتُصالِح دفتر الأستاذ عند العودة");
Console.WriteLine("Salasel Babel — spike: an offline-capable POS that reconciles exactly on reconnect");
Console.WriteLine(new string('─', 100));
Console.WriteLine($"runtime : .NET {Environment.Version}, {Environment.ProcessorCount} logical CPUs, {Environment.OSVersion}");

await ServerBootstrap.EnsureDatabaseAsync();
Console.WriteLine($"database: {await ServerBootstrap.ServerFactsAsync()}");
Console.WriteLine($"devices : {Config.DeviceDir}");
Console.WriteLine($"sqlite  : {SqliteVersion()}");
await ServerBootstrap.ApplyAsync();
Console.WriteLine("schema  : applied (ledger + pos, dropped and recreated)");

// بيانات مرجعية للسلّة القياسية المستعملة في الأقسام غير المتعلّقة بالمخزون:
// رصيد وافر وسعر مطابق، كي لا تُنتج أقسامُ الأداء ضجيجاً في طابور الاستثناءات.
await Sql.ExecAsync(Config.Admin, $"""
    insert into pos.stock (tenant_id, item_code, on_hand) values
        ('{Config.Tenant}', 'ITM-COFFEE', 10000000), ('{Config.Tenant}', 'ITM-CAKE', 10000000)
        on conflict (tenant_id, item_code) do nothing;
    insert into pos.price (tenant_id, item_code, effective_from, unit_price) values
        ('{Config.Tenant}', 'ITM-COFFEE', now() - interval '90 days', 12.5000),
        ('{Config.Tenant}', 'ITM-CAKE',   now() - interval '90 days', 23.0000)
        on conflict do nothing;
    """);

var sw = Stopwatch.StartNew();
if (only.Contains('1')) await P1_LocalStore.RunAsync(exePath);
if (only.Contains('2')) await P2_SyncProtocol.RunAsync(exePath);
if (only.Contains('3')) await P3_Idempotency.RunAsync();
if (only.Contains('4')) await P4_Ranges.RunAsync();
if (only.Contains('5')) await P5_ClockSkew.RunAsync();
if (only.Contains('6')) await P6_Conflicts.RunAsync();
if (only.Contains('7')) await P7_Measurements.RunAsync();
sw.Stop();

var failed = Proof.Summary();
Console.WriteLine($"  total wall time {sw.Elapsed.TotalSeconds:F1} s");
return failed == 0 ? 0 : 1;

static string SqliteVersion()
{
    using var c = new Microsoft.Data.Sqlite.SqliteConnection("Data Source=:memory:");
    c.Open();
    using var cmd = c.CreateCommand();
    cmd.CommandText = "select sqlite_version()";
    return $"SQLite {cmd.ExecuteScalar()}";
}

async Task<int> RunChildAsync(string mode)
{
    var db = Arg("db");
    var deviceId = Arg("device");
    // وسائط سطر الأوامر بروتوكول بين عمليتين لا نصّ عرض: تُقرأ بالثقافة الثابتة
    // كي تُطابق ما كتبته العملية الأمّ مهما كانت ثقافة الجهاز.
    // CLI arguments are an inter-process protocol, not display text: parsed invariantly.
    var count = int.Parse(Arg("count", "100"), CultureInfo.InvariantCulture);
    var rangeStart = long.Parse(Arg("rangestart", "1"), CultureInfo.InvariantCulture);
    var rangeSize = long.Parse(Arg("rangesize", "20000"), CultureInfo.InvariantCulture);

    using var device = PosDevice.Open(db, deviceId, Config.Tenant);
    device.InstallRange($"R-{deviceId}", rangeStart, rangeStart + rangeSize - 1);
    device.OpenShift($"SH-{deviceId}");
    var basket = P1_LocalStore.Basket;

    switch (mode)
    {
        case "write":
            for (int i = 0; i < count; i++)
            {
                var s = device.RecordSale(basket);
                Console.WriteLine(FormattableString.Invariant($"WROTE {s.InvoiceNo}"));
                Console.Out.Flush();
            }
            return 0;

        case "syncrun":
            {
                for (int i = 0; i < count; i++) device.RecordSale(basket);
                var server = new SyncServer(Config.Admin);
                var client = new SyncClient(device, server);
                client.RecoverInflight();
                Console.WriteLine($"READY {count}");
                Console.Out.Flush();
                // مزامنة بدفعات صغيرة مع طباعة التقدّم كي يستطيع الأب القتل في المنتصف
                while (true)
                {
                    var batch = client.NextBatch(20);
                    if (batch.Count == 0) break;
                    foreach (var e in batch)
                        device.Store.Exec("update sale set sync_state='inflight' where idem_key=$k", ("$k", e.IdemKey));
                    var resp = await server.SyncAsync(new SyncBatch(Config.Tenant, deviceId,
                        Guid.CreateVersion7().ToString("N"), device.Clock.WallUtcNow, batch));
                    foreach (var a in resp.Acks)
                        device.Store.Exec("update sale set sync_state='acked' where idem_key=$k", ("$k", a.IdemKey));
                    Console.WriteLine(FormattableString.Invariant($"SYNCED {batch.Count}"));
                    Console.Out.Flush();
                }
                return 0;
            }

        default:
            Console.Error.WriteLine($"unknown child mode '{mode}'");
            return 2;
    }
}
