using System.Globalization;
using Babel.Api.Hosting;
using Babel.Api.OpenApi;

// ─────────────────────────────────────────────────────────────────────────────
// نقطة الدخول.
//
// وضعان لا ثالث لهما:
//
//   1. `dotnet Babel.Api.dll` — يشغّل الخادم.
//   2. `dotnet Babel.Api.dll --emit-openapi <مسار>` — يولّد العقد المنشور ويخرج.
//
// والوضع الثاني ليس أداة جانبية: هو الطريق **الوحيد** الذي يُنتَج به
// contracts/openapi/v1.json، وحارس الانحراف في مجموعة الاختبارات يعيد تشغيله
// ويقارن البايتات. عقدٌ يُحرَّر بيد ينفصل عن الشيفرة في أول أسبوع.
//
// وملاحظتان تشغيليتان مثبَّتتان من موجة الهيكل:
//   • Wolverine يُهيَّأ بالتوليد الساكن (TypeLoadMode.Static) مع خطوة `codegen write`.
//   • WolverineFx.RuntimeCompilation ممنوعة في الإنتاج — تجرّ Roslyn إلى العملية،
//     والمنع مفروض ببناء في Rule08.
// ─────────────────────────────────────────────────────────────────────────────

const string EmitSwitch = "--emit-openapi";

int emitIndex = Array.IndexOf(args, EmitSwitch);

if (emitIndex >= 0)
{
    if (emitIndex + 1 >= args.Length)
    {
        await Console.Error
            .WriteLineAsync($"الاستعمال: {EmitSwitch} <مسار الملف> / usage: {EmitSwitch} <file path>")
            .ConfigureAwait(false);
        return 2;
    }

    string target = args[emitIndex + 1];

    WebApplication generator = BabelApiHost.Build([]);

    // ما سجّله التطبيق فعلاً — لا ما نظنّ أننا سجّلناه. المولّد يقارن الاثنين ويتوقّف
    // على أي اختلاف، فلا يُودَع عقد يصف باباً غير موجود ولا يُترك باب بلا وصف.
    List<(string Path, string Method)> registered = [];

    foreach (Endpoint endpoint in ((IEndpointRouteBuilder)generator).DataSources.SelectMany(static d => d.Endpoints))
    {
        if (endpoint is not RouteEndpoint route)
        {
            continue;
        }

        string pattern = "/" + route.RoutePattern.RawText?.TrimStart('/');
        IReadOnlyList<string> methods = route.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods ?? [];

        foreach (string method in methods)
        {
            registered.Add((pattern, method.ToLowerInvariant()));
        }
    }

    byte[] document = OpenApiEmitter.Emit(registered);

    string? directory = Path.GetDirectoryName(Path.GetFullPath(target));
    if (!string.IsNullOrEmpty(directory))
    {
        Directory.CreateDirectory(directory);
    }

    await File.WriteAllBytesAsync(target, document).ConfigureAwait(false);

    Console.WriteLine(string.Create(
        CultureInfo.InvariantCulture,
        $"openapi {OpenApiEmitter.SpecVersion} -> {target} ({document.Length} bytes, {registered.Count} operations)"));

    return 0;
}

WebApplication app = BabelApiHost.Build(args);
await BabelApiHost.SeedEntitlementsAsync(app).ConfigureAwait(false);
await app.RunAsync().ConfigureAwait(false);
return 0;
