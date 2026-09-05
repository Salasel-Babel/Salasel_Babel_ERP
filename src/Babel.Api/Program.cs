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

// ‏**مفتاح توقيع تذاكر المرفقات يُطلب هنا عمداً — عند الإقلاع لا عند أول تنزيل.**
// التسجيل مصنعٌ كسول، فبلا هذا السطر يقلع خادمٌ سليم الظاهر ثم يردّ 500 على أول طلب
// تنزيل. وغيابُ المفتاح **عطلٌ يُعلَن عند التركيب ولا مفتاحَ يُخترع** (ADR-0046 دليل 14):
// مُصدِرٌ يولّد لنفسه مفتاحاً عند الإقلاع يجعل كل تذكرة صالحةً قبل إعادة التشغيل ومرفوضةً
// بعدها، والفشل يُقرأ «انتهت الصلاحية» لا «لا مفتاح».
// ويُضبط بـBABEL_STORAGE_TICKET_KEY أو Babel__Storage__TicketSigningKey، ستّ‌عشرياً
// بـ32 بايتاً فأكثر — ولا مفتاح في هذا المستودع ولا في أي ملفّ إعداد مُودَع فيه.
_ = app.Services.GetRequiredService<Babel.Contracts.Storage.IAttachmentTickets>();

// ‏**واتصالُ كلّ وحدةٍ يُطلب هنا — عند الإقلاع لا عند أوّل نداء.**
//
// وكان هذا السطر يخصّ الموارد البشرية وحدها: بقيّة الوحدات كانت تحمل في شيفرتها نصّ
// اتصالٍ افتراضياً على المضيف المحلي **بالمستخدم الفائق للعنقود**، فنشرةٌ ينقصها
// المتغيّر لم تكن تتعطّل بل **تعمل بصلاحيةٍ كاملة على قاعدةٍ لم يقصدها أحد، بصمت**.
// فصار الغياب فراغاً، وصار الفراغ **وقفةً برسالةٍ تسمّي المتغيّر** — كما يفعل
// `deploy/compose.yml` بصيغة ‎${VAR:?رسالة}‎ بالضبط.
//
// وللتطوير على جهازٍ محلّي متغيّرٌ واحد يقول ذلك صراحةً: ‏`BABEL_LOCAL_DEV=1`.
// (‏ADR-جديد · docs/evidence/traps.md#fakh-a-missing-connection-variable-silently-grants-the-cluster-superuser)
BabelApiHost.EnsureDeploymentConfigured(app);

await BabelApiHost.SeedEntitlementsAsync(app).ConfigureAwait(false);
await app.RunAsync().ConfigureAwait(false);
return 0;
