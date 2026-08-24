using System.Globalization;
using System.Text;
using Babel.Canonicalization;
using Babel.Canonicalization.Golden;

Console.OutputEncoding = Encoding.UTF8;

// المتجهات الذهبية يجب أن تعطي نفس البايتات تحت أي ثقافة نظام. نُثبّت ثقافة
// «عدائية» افتراضياً عند التوليد والفحص، حتى لا يمرّ تسرّب ثقافي بصمت.
var hostile = Environment.GetEnvironmentVariable("BABEL_GOLDEN_CULTURE") ?? "ar-SA";
try
{
    var ci = new CultureInfo(hostile);
    CultureInfo.DefaultThreadCurrentCulture = ci;
    CultureInfo.DefaultThreadCurrentUICulture = ci;
    CultureInfo.CurrentCulture = ci;
}
catch (CultureNotFoundException)
{
    Console.Error.WriteLine($"تعذّر ضبط الثقافة {hostile} — قد تكون البيئة في وضع العولمة الثابتة.");
}

var goldenPath = Path.Combine(RepoRoot(), "tests", "golden", "golden-vectors.v1.json");
var vectors = GoldenVectorSet.All;
var mode = args.Length > 0 ? args[0] : "--verify";

Console.WriteLine($"Babel.Canonicalization — المتجهات الذهبية ({Canonicalizer.Magic})");
Console.WriteLine($"  الثقافة المحيطة أثناء التنفيذ : {CultureInfo.CurrentCulture.Name}");
Console.WriteLine($"  فحص بيئة التشغيل             : {(CanonicalRuntime.SelfTest().Ok ? "سليمة" : "معطوبة")}");
Console.WriteLine($"  عدد المتجهات                 : {vectors.Count}");
Console.WriteLine($"  الملف                        : {goldenPath}");
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

switch (mode)
{
    case "--emit":
        {
            var problems = GoldenFile.StructuralChecks(vectors);
            if (problems.Count > 0)
            {
                Console.Error.WriteLine("فحوص بنيوية فاشلة — لن يُكتب الملف:");
                foreach (var p in problems) Console.Error.WriteLine("  ✗ " + p);
                return 2;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(goldenPath)!);
            GoldenFile.Write(goldenPath, GoldenFile.Emit(vectors));
            Console.WriteLine($"✓ كُتب {vectors.Count} متجهاً.");
            return 0;
        }

    case "--verify":
        {
            var stored = GoldenFile.TryRead(goldenPath);
            if (stored is null)
            {
                Console.Error.WriteLine("الملف الذهبي غير موجود. شغّل --emit مرّة واحدة وأودِعه في المستودع.");
                return 3;
            }

            var problems = GoldenFile.StructuralChecks(vectors);
            var drifts = GoldenFile.Verify(stored, vectors);

            foreach (var p in problems) Console.Error.WriteLine("  ✗ بنيوي: " + p);
            foreach (var d in drifts)
            {
                Console.Error.WriteLine($"  ✗ انحراف [{d.Id}] في {d.Field}");
                Console.Error.WriteLine($"      المتوقّع : {Trim(d.Expected)}");
                Console.Error.WriteLine($"      الفعلي   : {Trim(d.Actual)}");
            }

            if (problems.Count == 0 && drifts.Count == 0)
            {
                Console.WriteLine($"✓ كل المتجهات الـ{vectors.Count} مطابقة. لا انحراف.");
                return 0;
            }

            Console.Error.WriteLine();
            Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
            Console.Error.WriteLine("انحراف في الشكل القانوني.");
            Console.Error.WriteLine("إن كان مقصوداً فهو **إصدار جديد v2**، لا تعديل على v1:");
            Console.Error.WriteLine("  1. أبقِ CanonicalizerV1 كما هو، حرفاً بحرف.");
            Console.Error.WriteLine("  2. أضف CanonicalizerV2 وسجّله في CanonRegistry بجواره.");
            Console.Error.WriteLine("  3. أبقِ golden-vectors.v1.json كما هو، وأضف golden-vectors.v2.json.");
            Console.Error.WriteLine("راجع src/Babel.Canonicalization/SPEC.md قسم «إجراء إدخال v2».");
            Console.Error.WriteLine("═══════════════════════════════════════════════════════════════");
            return 1;
        }

    case "--print":
        {
            foreach (var v in vectors)
            {
                var r = v.Execute();
                Console.WriteLine($"── {r.Id}  [{r.Kind}]");
                Console.WriteLine($"   {r.DescriptionAr}");
                if (r.CanonicalSha256 is not null) Console.WriteLine($"   sha256 = {r.CanonicalSha256}");
                if (r.ErrorCode is not null) Console.WriteLine($"   error  = {r.ErrorCode}");
                if (r.Value is not null) Console.WriteLine($"   value  = {r.Value.Replace("\n", "\\n", StringComparison.Ordinal)}");
            }
            return 0;
        }

    case "--schema":
        Console.WriteLine(Babel.Canonicalization.Schemas.JournalEntrySchema.V1.Describe());
        return 0;

    default:
        Console.Error.WriteLine("الاستخدام: --emit | --verify | --print | --schema");
        return 64;
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
