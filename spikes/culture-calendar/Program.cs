// ---------------------------------------------------------------------------
// مقياس فخّ التقويم الثقافي — يُنتج الأرقام المستشهد بها في
// docs/evidence/measurements.md §3.8.
//
//   dotnet run --project spikes/culture-calendar
//
// لا قاعدة بيانات، ولا شبكة، ولا اعتمادية واحدة. الفخّ كلّه في وقت التشغيل.
// ---------------------------------------------------------------------------
using System.Globalization;

var at = new DateTimeOffset(2026, 8, 24, 12, 0, 0, TimeSpan.Zero);
const decimal amount = 1234.5m;
const string identifier = "ID";

Console.OutputEncoding = System.Text.Encoding.UTF8;
Console.WriteLine($"runtime                = {Environment.Version}");
Console.WriteLine($"InvariantGlobalization = {AppContext.TryGetSwitch("System.Globalization.Invariant", out var inv) && inv}");
Console.WriteLine($"ICU cultures available = {CultureInfo.GetCultures(CultureTypes.AllCultures).Length}");
Console.WriteLine();

// الصيغة الصحيحة: ثقافة ثابتة صريحة. هذا هو خطّ الأساس الذي يُقارَن به كل ما تحته.
Console.WriteLine($"invariant  →  {at.UtcDateTime.ToString("yyyy-MM", CultureInfo.InvariantCulture)}   "
                  + $"{amount.ToString("0.0000", CultureInfo.InvariantCulture)}   {identifier.ToLowerInvariant()}");
Console.WriteLine();
Console.WriteLine($"{"culture",-8} {"period key",-12} {"calendar",-22} {"amount 0.0000",-16} {"\"ID\".ToLower()",-16}");
Console.WriteLine(new string('-', 78));

foreach (var name in new[] { "ar-SA", "fa-IR", "th-TH", "tr-TR", "de-DE", "en-US" })
{
    CultureInfo.CurrentCulture = new CultureInfo(name);

    // هذا بالضبط هو الشكل المعيب: سلسلة مُستكمَلة بمُحدِّد تنسيق، بلا مزوّد.
    // الشكل هنا مقصود ومعزول في مقياس؛ وهو ممنوع في src/ بالقاعدة 10.
    var periodKey = $"{at.UtcDateTime:yyyy-MM}";
    var money = $"{amount:0.0000}";
    var lowered = identifier.ToLower();
    var calendar = CultureInfo.CurrentCulture.Calendar.GetType().Name;

    Console.WriteLine($"{name,-8} {periodKey,-12} {calendar,-22} {money,-16} {lowered,-16}");
}

Console.WriteLine();
Console.WriteLine("قراءة الجدول: أي خانة تخالف سطر invariant هي مفتاح فوترة، أو مبلغ مُجزَّأ،");
Console.WriteLine("أو معرّف مقارَن، يختلف بين خادم وخادم — بلا استثناء وبلا سطر سجل.");
