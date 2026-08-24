using System.Globalization;
using System.Text;

namespace Babel.ControlPlane.Proofs;

public enum Verdict { Pass, Fail }

public sealed record ProofResult(string Id, string Name, Verdict Verdict, string Detail);

/// <summary>
/// يجمع نتائج الإثباتات ويطبع جدول PASS/FAIL — نفس أسلوب
/// <c>spikes/relational-stack</c>: أمر واحد، وجدول واحد، ورمز خروج ذو معنى.
/// </summary>
public sealed class Recorder
{
    private readonly List<ProofResult> _results = [];
    private readonly List<string> _notes = [];
    private readonly List<string> _measurements = [];

    public IReadOnlyList<ProofResult> Results => _results;
    public IReadOnlyList<string> Measurements => _measurements;
    public bool AllPassed => _results.All(r => r.Verdict == Verdict.Pass);

    public void Check(string id, string name, bool ok, string detail = "") =>
        Record(id, name, ok ? Verdict.Pass : Verdict.Fail, detail);

    public void Pass(string id, string name, string detail = "") =>
        Record(id, name, Verdict.Pass, detail);

    public void Fail(string id, string name, string detail = "") =>
        Record(id, name, Verdict.Fail, detail);

    public void Record(string id, string name, Verdict v, string detail)
    {
        _results.Add(new ProofResult(id, name, v, detail));
        Console.WriteLine($"  [{(v == Verdict.Pass ? "PASS" : "FAIL")}] {id}  {name}");
        Emit(detail);
    }

    public void Note(string note)
    {
        _notes.Add(note);
        Console.WriteLine($"  [note] {note}");
    }

    public void Measure(string line)
    {
        _measurements.Add(line);
        Console.WriteLine($"  [meas] {line}");
    }

    public static void Evidence(string text) => Emit(text);

    public static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title} " + new string('-', Math.Max(0, 74 - title.Length)));
    }

    private static void Emit(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return;
        foreach (var line in detail.Replace("\r", "").Split('\n'))
            Console.WriteLine($"         {line}");
    }

    public void PrintSummary()
    {
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("=========================================================================");
        sb.AppendLine("  PASS/FAIL SUMMARY  -  ملخص إثباتات مستوى التحكّم");
        sb.AppendLine("=========================================================================");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  {"ID",-8} {"RESULT",-7} PROOF");
        sb.AppendLine("  " + new string('-', 69));
        foreach (var g in _results.GroupBy(r => r.Id[..1]).OrderBy(g => g.Key))
        {
            foreach (var r in g)
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {r.Id,-8} {(r.Verdict == Verdict.Pass ? "PASS" : "FAIL"),-7} {r.Name}");
            sb.AppendLine("  " + new string('-', 69));
        }

        var passed = _results.Count(r => r.Verdict == Verdict.Pass);
        sb.AppendLine(CultureInfo.InvariantCulture, $"  {passed}/{_results.Count} proofs passed");
        foreach (var s in new[] { "A", "B", "C", "D", "E", "F" })
        {
            var items = _results.Where(r => r.Id.StartsWith(s, StringComparison.Ordinal)).ToList();
            if (items.Count == 0) continue;
            sb.AppendLine(CultureInfo.InvariantCulture, $"  ({s})  {(items.All(r => r.Verdict == Verdict.Pass) ? "PASS" : "FAIL")}"
                + $"   {items.Count(r => r.Verdict == Verdict.Pass)}/{items.Count}   {Title(s)}");
        }
        sb.AppendLine("=========================================================================");

        if (_measurements.Count > 0)
        {
            sb.AppendLine("  MEASUREMENTS / القياسات");
            foreach (var m in _measurements) sb.AppendLine(CultureInfo.InvariantCulture, $"   * {m}");
            sb.AppendLine("=========================================================================");
        }
        if (_notes.Count > 0)
        {
            sb.AppendLine("  NOTES / ملاحظات");
            foreach (var n in _notes) sb.AppendLine(CultureInfo.InvariantCulture, $"   * {n}");
            sb.AppendLine("=========================================================================");
        }
        Console.Write(sb.ToString());
    }

    private static string Title(string s) => s switch
    {
        "A" => "التزويد المُحكَم والأرشفة بدل الحذف",
        "B" => "ترحيل الأسطول: دفعات واستئناف بعد قتل العملية",
        "C" => "التوسيع/الانكماش: إصداران من الشيفرة × إصداران من المخطط",
        "D" => "إدارة الاتصالات: سقف وإخلاء وقاطع دارة، مع قياس",
        "E" => "الاستحقاق بثلاث حالات، ورسم الاعتماديات، ورفض الأرشفة",
        "F" => "القياس: لا عدّ مزدوج، ولا فقدان عند الانهيار",
        _ => ""
    };
}
