using System.Globalization;
using BabelDemoCompany;

// ─────────────────────────────────────────────────────────────────────────────
// مُنشئ الشركة التجريبية — أربع خطوات بترتيب لا يجوز أن ينقلب، وكلٌّ منها بدورها:
//
//   bootstrap  قواعد البيانات ودور التطبيق ......... باتصال الصيانة (دور خارق)
//   migrate    المخطّطات والصلاحيات والبيانات المرجعية  بدور المالك
//   seed       ثمانية أشهر من النشاط ودورةُ إيجارٍ كاملة  بدور التطبيق، عبر محرّك الترحيل
//   verify     ميزان + سلسلة + أعمار ................ بدور التطبيق، قراءةً محضة
//
//   all        الأربع بالترتيب — وهو ما تستدعيه حاوية الترحيل عند كل نشر.
//
// وكلّها تُعاد بلا أثر: النشر يقع مرّتين عند أول انقطاع شبكة، ونصٌّ لا يحتمل ذلك
// يترك الخادم بقاعدة نصف مبنيّة.
// ─────────────────────────────────────────────────────────────────────────────

string command = args.Length > 0 ? args[0] : "all";

Settings settings = Settings.FromEnvironment();

Console.WriteLine("══ سلاسل بابل · بناء الشركة التجريبية ══════════════════════");
Console.WriteLine("   المنشأة : " + Company.NameArabic);
Console.WriteLine("   المعرّف  : " + settings.Company.ToString("D", CultureInfo.InvariantCulture));
Console.WriteLine("   الدفتر   : " + Settings.Book + FormattableString.Invariant($" · السنة المالية {settings.FiscalYear}"));
Console.WriteLine("   الأمر    : " + command);

using CancellationTokenSource cancellation = new();
Console.CancelKeyPress += (_, eventArgs) =>
{
    eventArgs.Cancel = true;
    cancellation.Cancel();
};

try
{
    switch (command)
    {
        case "bootstrap":
            await Bootstrap.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            break;

        case "migrate":
            await Schema.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            break;

        case "seed":
            await Seed.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            await RealEstateSeed.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            break;

        case "verify":
            await Verify.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            break;

        case "all":
            await Bootstrap.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            await ApplicationPasswordAsync(settings, cancellation.Token).ConfigureAwait(false);
            await Schema.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            await Seed.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            await RealEstateSeed.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            await Verify.RunAsync(settings, cancellation.Token).ConfigureAwait(false);
            break;

        default:
            await Console.Error
                .WriteLineAsync("أمر غير معروف: " + command + " — المقبول: bootstrap · migrate · seed · verify · all")
                .ConfigureAwait(false);
            return 2;
    }
}
catch (OperationCanceledException)
{
    await Console.Error.WriteLineAsync("أُلغي التشغيل.").ConfigureAwait(false);
    return 130;
}
#pragma warning disable CA1031 // أداة تشغيل: أي عطل يجب أن يخرج برمز غير صفري ورسالة كاملة، لا بأثر مكدّس عارٍ.
catch (Exception failure)
#pragma warning restore CA1031
{
    await Console.Error.WriteLineAsync("\n✘ توقّف البناء: " + failure.Message).ConfigureAwait(false);
    await Console.Error.WriteLineAsync(failure.ToString()).ConfigureAwait(false);
    return 1;
}

Console.WriteLine("\n══ اكتمل ══════════════════════════════════════════════════");
return 0;

static async Task ApplicationPasswordAsync(Settings settings, CancellationToken cancellationToken)
{
    string? password = Environment.GetEnvironmentVariable("BABEL_LEDGER_APP_PASSWORD");
    if (!string.IsNullOrWhiteSpace(password))
    {
        await Bootstrap.SetApplicationPasswordAsync(settings, password, cancellationToken).ConfigureAwait(false);
    }
}
