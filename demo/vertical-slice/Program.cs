using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using BabelDemo.Db;
using BabelDemo.Support;

// ---------------------------------------------------------------------------
// عرض الشريحة الرأسية لنظام سلاسل بابل ERP.
// ASP.NET Core minimal API على net10.0 + EF Core 10 + Npgsql. بلا Marten.
// ---------------------------------------------------------------------------

var setupOnly = args.Contains("--setup-only");
var skipSetup = args.Contains("--no-setup");
var port = Environment.GetEnvironmentVariable("BABEL_DEMO_PORT") ?? "5099";

if (!skipSetup)
{
    Console.WriteLine("── تهيئة قاعدة بيانات العرض ─────────────────────────────");
    await Seed.RunAsync(Console.Out);
    Console.WriteLine("── تمّت التهيئة ─────────────────────────────────────────");
}
if (setupOnly) return 0;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://0.0.0.0:{port}");
builder.Logging.AddFilter("Microsoft", LogLevel.Warning);
builder.Services.ConfigureHttpJsonOptions(o =>
{
    // المال نصّ بمقياس ثابت في JSON: لا يمرّ أبداً عبر double في المتصفح
    o.SerializerOptions.Converters.Add(new MoneyJsonConverter());
    o.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.Never;
    o.SerializerOptions.Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping;
});

var app = builder.Build();
app.UseDefaultFiles();
app.UseStaticFiles();

// ── بيانات مرجعية ─────────────────────────────────────────────────────────
app.MapGet("/api/meta", async () => new
{
    runtime = Environment.Version.ToString(),
    framework = System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription,
    postgres = await Sql.ScalarAsync<string>(Config.App, "select version()"),
    database = Config.Database,
    book = Config.BookId,
    tenant = Config.TenantId,
    appConnection = Config.Describe(Config.App),
    ownerConnection = Config.Describe(Config.Owner),
    appRole = Config.AppRole,
    grants = await DangerOps.GrantsAsync(),
    serverTime = DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss'Z'", CultureInfo.InvariantCulture)
});

app.MapGet("/api/accounts", async (CancellationToken ct) => await LedgerQueries.AccountsAsync(ct));
app.MapGet("/api/entries", async (CancellationToken ct) => await LedgerQueries.EntriesAsync(ct));
app.MapGet("/api/trial-balance", async (string? period, CancellationToken ct)
    => await LedgerQueries.TrialBalanceAsync(period, ct));
app.MapGet("/api/verify", async (CancellationToken ct) => await LedgerQueries.VerifyAsync(ct));
app.MapGet("/api/bidi", (string? text) => DangerOps.Bidi(string.IsNullOrWhiteSpace(text)
    ? "مصروف خدمات تقنية" : text));

// ── الترحيل: نداء خادم واحد، معاملة واحدة ────────────────────────────────
app.MapPost("/api/entries", async (PostEntryRequest req, CancellationToken ct) =>
{
    if (!DateOnly.TryParseExact(req.EntryDate, "yyyy-MM-dd", CultureInfo.InvariantCulture,
                                DateTimeStyles.None, out var date))
        return Results.BadRequest(new { message = "تاريخ غير صالح، الصيغة المتوقّعة yyyy-MM-dd" });

    if (req.Lines is null || req.Lines.Count == 0)
        return Results.BadRequest(new { message = "لا توجد سطور في القيد" });

    var lines = req.Lines
        .Select(l => new LineInput(l.AccountCode?.Trim() ?? "", l.Description?.Trim() ?? "", l.Debit, l.Credit))
        .Where(l => l.AccountCode.Length > 0)
        .ToList();

    var outcome = await PostingService.PostAsync(new PostRequest(
        date,
        string.IsNullOrWhiteSpace(req.MemoAr) ? "—" : req.MemoAr.Trim(),
        string.IsNullOrWhiteSpace(req.Memo) ? "—" : req.Memo.Trim(),
        string.IsNullOrWhiteSpace(req.Actor) ? "مستخدم العرض" : req.Actor.Trim(),
        lines), ct);

    return outcome.Ok ? Results.Ok(outcome) : Results.Json(outcome, statusCode: 422);
});

app.MapPost("/api/entries/{no:long}/reverse", async (long no, CancellationToken ct) =>
{
    var outcome = await PostingService.ReverseAsync(no, "مستخدم العرض", ct);
    return outcome.Ok ? Results.Ok(outcome) : Results.Json(outcome, statusCode: 422);
});

// ── الإجراءات الخطرة ─────────────────────────────────────────────────────
app.MapPost("/api/danger/update", async (DangerRequest req, CancellationToken ct)
    => await DangerOps.TryUpdateAsync(req.EntryNo, req.NewAmount == 0m ? 999_999.0000m : req.NewAmount, ct));

app.MapPost("/api/danger/delete", async (DangerRequest req, CancellationToken ct)
    => await DangerOps.TryDeleteAsync(req.EntryNo, ct));

app.MapPost("/api/danger/tamper", async (TamperRequest req, CancellationToken ct)
    => await DangerOps.TamperAsync(req.EntryNo == 0 ? 2 : req.EntryNo,
                                   req.Delta == 0m ? 30_000.0000m : req.Delta, ct));

app.MapPost("/api/danger/restore", async (CancellationToken ct) => await DangerOps.RestoreAsync(ct));

// ── إعادة ضبط العرض بالكامل ──────────────────────────────────────────────
app.MapPost("/api/reset", async () =>
{
    var log = new StringWriter();
    await Seed.RunAsync(log);
    return Results.Ok(new { ok = true, log = log.ToString() });
});

var url = $"http://localhost:{port}/";
Console.WriteLine();
Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
Console.WriteLine("║  عرض الشريحة الرأسية — نظام سلاسل بابل ERP                   ║");
Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
Console.WriteLine($"  افتح المتصفح على / open:   {url}");
Console.WriteLine($"  دور التطبيق / app role:    {Config.Describe(Config.App)}");
Console.WriteLine($"  حساب المالك / owner:       {Config.Describe(Config.Owner)}");
Console.WriteLine("  أوقف الخادم بـ Ctrl+C / stop with Ctrl+C");
Console.WriteLine();

app.Run();
return 0;

// ── عقود الطلب ───────────────────────────────────────────────────────────
public sealed record PostLineRequest(string? AccountCode, string? Description, decimal Debit, decimal Credit);
public sealed record PostEntryRequest(string EntryDate, string? MemoAr, string? Memo, string? Actor,
                                      List<PostLineRequest>? Lines);
public sealed record DangerRequest(long EntryNo, decimal NewAmount);
public sealed record TamperRequest(long EntryNo, decimal Delta);
