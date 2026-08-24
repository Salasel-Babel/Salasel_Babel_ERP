using System.Globalization;
using System.Text;
using Babel.Canonicalization;
using Babel.Canonicalization.Golden;

Console.OutputEncoding = Encoding.UTF8;

// ═════════════════════════════════════════════════════════════════════════════
//  الوسائط
//
//  ⚠ المصيدة التي أُصلحت هنا: النسخة السابقة كانت تقرأ الثقافة من متغيّر بيئة
//  وحده و**تتجاهل بصمت** كل وسيط بعد الأول. أي أن وظيفة تكامل مستمر تشغّل الأداة
//  ثلاث مرّات بـ‏--culture ar-SA ثم de-DE ثم tr-TR كانت تفحص **تحت ar-SA ثلاث
//  مرّات** وتُبلّغ ثلاثة نجاحات. تحقّقٌ لا يتحقّق ممّا يظنّه المشغّل أسوأ من لا تحقّق:
//  الأول يُنتج ثقة كاذبة موثَّقة في سجلّ البناء.
//
//  ولذلك هنا ثلاث قواعد:
//    1. ‏--culture يعمل فعلاً.
//    2. وسيط غير معروف **يُسقط الأداة** بدل أن يُتجاهل.
//    3. والأداة تطبع الثقافة **الفعلية** وتُفشل نفسها إن لم تطابق المطلوبة،
//       ومعها قيمة مُنسَّقة تنسيقاً واعياً بالثقافة بوصفها دليلاً حيّاً على أن
//       الثقافة سارية فعلاً في هذه العملية.
// ═════════════════════════════════════════════════════════════════════════════

string mode = "--verify";
string sets = "all";
string? requestedCulture = null;
bool modeSeen = false;

for (var i = 0; i < args.Length; i++)
{
    var arg = args[i];

    if (arg is "--emit" or "--verify" or "--print" or "--schema")
    {
        if (modeSeen)
        {
            Console.Error.WriteLine($"وضعان في نداء واحد: «{mode}» و«{arg}».");
            return 64;
        }
        mode = arg;
        modeSeen = true;
        continue;
    }

    if (TryValue(arg, "--culture", args, ref i, out var culture))
    {
        requestedCulture = culture;
        continue;
    }

    if (TryValue(arg, "--set", args, ref i, out var set))
    {
        if (set is not ("v1" or "v2" or "all"))
        {
            Console.Error.WriteLine($"‏--set يقبل v1 أو v2 أو all، ووصل «{set}».");
            return 64;
        }
        sets = set;
        continue;
    }

    Console.Error.WriteLine($"وسيط غير معروف: «{arg}».");
    Console.Error.WriteLine("الاستخدام: [--emit|--verify|--print|--schema] [--set v1|v2|all] [--culture <اسم>]");
    Console.Error.WriteLine("وسيطٌ يُتجاهَل بصمت هو أخطر من وسيطٍ يُرفض: التحقّق يبدو أنه جرى ولم يجرِ.");
    return 64;
}

// الثقافة: الوسيط يسبق متغيّر البيئة، والافتراضي ثقافة «عدائية» حتى لا يمرّ
// تسرّب ثقافي بصمت.
var wanted = requestedCulture
             ?? Environment.GetEnvironmentVariable("BABEL_GOLDEN_CULTURE")
             ?? "ar-SA";
var source = requestedCulture is not null ? "--culture" : "BABEL_GOLDEN_CULTURE أو الافتراضي";

// ‏.NET يقبل اسم ثقافة مجهولاً ويصنع «ثقافة مُصطنعة» بلا خطأ حين يكون
// PredefinedCulturesOnly=false — أي أن خطأ مطبعياً في وظيفة CI (‏de-ED بدل de-DE)
// كان سيُشغّل الفحص تحت ثقافة لا وجود لها ويُبلّغ نجاحاً. نُطابق الاسم على ما
// تعرفه ICU فعلاً قبل أي شيء.
if (wanted.Length > 0 &&
    !CultureInfo.GetCultures(CultureTypes.AllCultures)
        .Any(c => string.Equals(c.Name, wanted, StringComparison.OrdinalIgnoreCase)))
{
    Console.Error.WriteLine($"الثقافة «{wanted}» (المصدر: {source}) ليست ثقافة تعرفها ICU على هذه البيئة.");
    Console.Error.WriteLine(".NET كان سيصنع ثقافة مُصطنعة بلا خطأ، فيمرّ الفحص تحت ثقافة لا وجود لها.");
    return 5;
}

try
{
    var ci = wanted.Length == 0 ? CultureInfo.InvariantCulture : new CultureInfo(wanted);
    CultureInfo.DefaultThreadCurrentCulture = ci;
    CultureInfo.DefaultThreadCurrentUICulture = ci;
    CultureInfo.CurrentCulture = ci;
    CultureInfo.CurrentUICulture = ci;
}
catch (CultureNotFoundException ex)
{
    Console.Error.WriteLine($"تعذّر ضبط الثقافة «{wanted}» (المصدر: {source}): {ex.Message}");
    Console.Error.WriteLine("قد تكون البيئة في وضع العولمة الثابتة أو بلا ICU. لا تُشغَّل المتجهات تحت ثقافة غير المطلوبة.");
    return 5;
}

var effective = CultureInfo.CurrentCulture.Name;
if (!string.Equals(effective, wanted, StringComparison.Ordinal))
{
    Console.Error.WriteLine(
        $"الثقافة المطلوبة «{wanted}» والفعلية «{effective}» — لا تُشغَّل المتجهات تحت ثقافة غير المطلوبة.");
    return 5;
}

// دليل حيّ على أن الثقافة سارية في هذه العملية بالذات: تنسيق واعٍ باللغة عمداً.
// هذا بالضبط ما يجب ألّا يقترب من البايتات المُجزَّأة — وطباعته هنا هي البرهان
// على أن العملية تعمل فعلاً تحت الثقافة المُعلنة، لا أنها ادّعت ذلك.
// ثقافة-عرض: هذه القيمة تُطبَع على الشاشة وحدها ولا تُحفظ ولا تُجزَّأ ولا تُقارَن.
// وهي **مقصودة** واعيةً بالثقافة: تحت ar-SA تُطبع بفاصلة U+066B وتحت de-DE بفاصلة
// لاتينية — فتكون برهاناً منظوراً على أن الثقافة المُعلنة سارية فعلاً في هذه العملية.
#pragma warning disable CA1305 // Specify IFormatProvider — التنسيق الواعي بالثقافة هو موضوع الإثبات نفسه
var cultureWitness = 100.5m.ToString("0.0000"); // ثقافة-عرض: شاهد على أن الثقافة سارية، للطباعة وحدها
#pragma warning restore CA1305

var repoRoot = RepoRoot();
var chosen = sets switch
{
    "v1" => new[] { GoldenSetIdentity.V1 },
    "v2" => [GoldenSetIdentity.V2],
    _ => [.. GoldenSetIdentity.All]
};

Console.WriteLine("Babel.Canonicalization — المتجهات الذهبية");
Console.WriteLine($"  الوضع                        : {mode}");
Console.WriteLine($"  الثقافة المطلوبة             : {wanted}   (المصدر: {source})");
Console.WriteLine($"  الثقافة الفعلية أثناء التنفيذ : {(effective.Length == 0 ? "(الثابتة)" : effective)}");
Console.WriteLine($"  شاهد الثقافة (100.5 بتنسيقها) : {cultureWitness}");
Console.WriteLine($"  فحص بيئة التشغيل             : {(CanonicalRuntime.SelfTest().Ok ? "سليمة" : "معطوبة")}");
Console.WriteLine($"  الإصدارات المسجَّلة            : {string.Join(", ", CanonRegistry.Versions.OrderBy(v => v, StringComparer.Ordinal))}");
foreach (var identity in chosen)
{
    Console.WriteLine($"  مجموعة {identity.CanonVersion}                   : "
        + $"{identity.Vectors.Count} متجهاً · {identity.FileName}");
}
Console.WriteLine();

// حارس البيئة أولاً: لا معنى لأي متجه إن كان التطبيع معطوباً.
// انظر SPEC.md §8.2 — وضع العولمة الثابتة يجعل String.Normalize لا-شيء بصمت.
if (!CanonicalRuntime.SelfTest().Ok)
{
    var r = CanonicalRuntime.SelfTest();
    Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
    Console.Error.WriteLine("بيئة التشغيل لا تصلح للتوحيد القياسي. لا بصمة تُحسب هنا.");
    Console.Error.WriteLine($"  framework                        = {r.FrameworkDescription}");
    Console.Error.WriteLine($"  nfc_composes_arabic              = {r.NfcComposesArabic}");
    Console.Error.WriteLine($"  is_normalized_detects_decomposed = {r.IsNormalizedDetectsDecomposed}");
    Console.Error.WriteLine($"  invariant_switch_claimed         = {r.InvariantSwitchClaimed}");
    Console.Error.WriteLine($"  arabic_culture_available         = {r.ArabicCultureAvailable}");
    Console.Error.WriteLine($"  invariant_decimal_format_stable  = {r.InvariantDecimalFormatStable}");
    Console.Error.WriteLine();
    Console.Error.WriteLine("السبب شبه المؤكّد: DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=1 أو صورة حاوية بلا ICU.");
    Console.Error.WriteLine("راجع src/Babel.Canonicalization/SPEC.md §8.2.");
    Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
    return 4;
}

var exit = 0;

foreach (var identity in chosen)
{
    var goldenPath = Path.Combine(repoRoot, "tests", "golden", identity.FileName);
    var vectors = identity.Vectors;

    switch (mode)
    {
        case "--emit":
            {
                var problems = GoldenFile.StructuralChecks(vectors);
                if (problems.Count > 0)
                {
                    Console.Error.WriteLine($"[{identity.CanonVersion}] فحوص بنيوية فاشلة — لن يُكتب الملف:");
                    foreach (var p in problems) Console.Error.WriteLine("  ✗ " + p);
                    exit = Math.Max(exit, 2);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
                GoldenFile.Write(goldenPath, GoldenFile.Emit(identity, vectors));
                Console.WriteLine($"✓ [{identity.CanonVersion}] كُتب {vectors.Count} متجهاً في {identity.FileName}.");
                break;
            }

        case "--verify":
            {
                var stored = GoldenFile.TryRead(goldenPath);
                if (stored is null)
                {
                    Console.Error.WriteLine(
                        $"[{identity.CanonVersion}] الملف الذهبي غير موجود: {goldenPath}. " +
                        "شغّل --emit مرّة واحدة وأودِعه في المستودع.");
                    exit = Math.Max(exit, 3);
                    continue;
                }

                var problems = GoldenFile.StructuralChecks(vectors);
                var drifts = GoldenFile.Verify(identity, stored, vectors);

                foreach (var p in problems) Console.Error.WriteLine($"  ✗ [{identity.CanonVersion}] بنيوي: " + p);
                foreach (var d in drifts)
                {
                    Console.Error.WriteLine($"  ✗ [{identity.CanonVersion}] انحراف [{d.Id}] في {d.Field}");
                    Console.Error.WriteLine($"      المتوقّع : {Trim(d.Expected)}");
                    Console.Error.WriteLine($"      الفعلي   : {Trim(d.Actual)}");
                }

                if (problems.Count == 0 && drifts.Count == 0)
                {
                    Console.WriteLine(
                        $"✓ [{identity.CanonVersion}] كل المتجهات الـ{vectors.Count} مطابقة تحت الثقافة "
                        + $"{(effective.Length == 0 ? "(الثابتة)" : effective)}. لا انحراف.");
                    break;
                }

                Console.Error.WriteLine();
                Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
                Console.Error.WriteLine($"انحراف في الشكل القانوني {identity.CanonVersion}.");
                Console.Error.WriteLine("إن كان مقصوداً فهو **إصدار جديد**، لا تعديل على إصدار قائم:");
                Console.Error.WriteLine("  1. أبقِ المُوحِّد القائم كما هو، حرفاً بحرف.");
                Console.Error.WriteLine("  2. أضف مُوحِّداً جديداً وسجّله في CanonRegistry بجواره.");
                Console.Error.WriteLine("  3. أبقِ ملفات المتجهات القائمة كما هي، وأضف ملفاً للإصدار الجديد.");
                Console.Error.WriteLine("راجع src/Babel.Canonicalization/SPEC.md قسم «إجراء إدخال v2».");
                Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
                exit = Math.Max(exit, 1);
                break;
            }

        case "--print":
            {
                Console.WriteLine($"── مجموعة {identity.CanonVersion} ──");
                foreach (var v in vectors)
                {
                    var r = v.Execute();
                    Console.WriteLine($"── {r.Id}  [{r.Kind}]");
                    Console.WriteLine($"   {r.DescriptionAr}");
                    if (r.CanonicalSha256 is not null) Console.WriteLine($"   sha256 = {r.CanonicalSha256}");
                    if (r.ErrorCode is not null) Console.WriteLine($"   error  = {r.ErrorCode}");
                    if (r.Value is not null) Console.WriteLine($"   value  = {r.Value.Replace("\n", "\\n", StringComparison.Ordinal)}");
                }
                break;
            }

        case "--schema":
            Console.WriteLine(identity.CanonVersion == "v1"
                ? Babel.Canonicalization.Schemas.JournalEntrySchema.V1.Describe()
                : Babel.Canonicalization.Schemas.JournalEntrySchema.V2.Describe());
            break;

        default:
            Console.Error.WriteLine("الاستخدام: [--emit|--verify|--print|--schema] [--set v1|v2|all] [--culture <اسم>]");
            return 64;
    }
}

return exit;

static bool TryValue(string arg, string name, string[] all, ref int index, out string value)
{
    if (arg.StartsWith(name + "=", StringComparison.Ordinal))
    {
        value = arg[(name.Length + 1)..];
        return true;
    }

    if (string.Equals(arg, name, StringComparison.Ordinal))
    {
        if (index + 1 >= all.Length)
        {
            throw new ArgumentException($"الوسيط {name} بلا قيمة.", nameof(arg));
        }
        value = all[++index];
        return true;
    }

    value = string.Empty;
    return false;
}

static string Trim(string s)
{
    var one = s.Replace("\n", "\\n", StringComparison.Ordinal).Replace("\t", "\\t", StringComparison.Ordinal);
    return one.Length <= 160 ? one : one[..160] + $"... ({one.Length} محرفاً)";
}

// جذر المستودع: ".git" قد يكون مجلداً أو ملفاً (worktree).
static string RepoRoot()
{
    var d = new DirectoryInfo(AppContext.BaseDirectory);
    while (d is not null)
    {
        var git = Path.Combine(d.FullName, ".git");
        if (Directory.Exists(git) || File.Exists(git)) return d.FullName;
        d = d.Parent;
    }
    return Directory.GetCurrentDirectory();
}
