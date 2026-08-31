using System.Globalization;
using System.Text;

namespace BabelRelationalSpike.Support;

public enum Verdict { Pass, Fail }

public sealed record ProofResult(string Id, string Name, Verdict Verdict, string Detail);

/// <summary>
/// Collects PASS/FAIL results and prints the single summary table that this
/// spike exists to produce.
/// يجمع نتائج الإثباتات ويطبع جدول النجاح/الفشل النهائي.
/// </summary>
public sealed class ProofRecorder
{
    private readonly List<ProofResult> _results = [];
    private readonly List<string> _notes = [];

    public IReadOnlyList<ProofResult> Results => _results;
    public bool AllPassed => _results.All(r => r.Verdict == Verdict.Pass);

    public void Pass(string id, string name, string detail = "") => Record(id, name, Verdict.Pass, detail);
    public void Fail(string id, string name, string detail = "") => Record(id, name, Verdict.Fail, detail);
    public void Check(string id, string name, bool ok, string detail = "") =>
        Record(id, name, ok ? Verdict.Pass : Verdict.Fail, detail);

    public void Record(string id, string name, Verdict verdict, string detail)
    {
        _results.Add(new ProofResult(id, name, verdict, detail));
        Console.WriteLine($"  [{(verdict == Verdict.Pass ? "PASS" : "FAIL")}] {id}  {name}");
        Emit(detail);
    }

    public void Note(string note)
    {
        _notes.Add(note);
        Console.WriteLine($"  [note] {note}");
    }

    public void Evidence(string text) => Emit(text);

    private static void Emit(string detail)
    {
        if (string.IsNullOrWhiteSpace(detail)) return;
        foreach (var line in detail.Replace("\r", "").Split('\n'))
            Console.WriteLine($"         {line}");
    }

    public void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine($"--- {title} " + new string('-', Math.Max(0, 72 - title.Length)));
    }

    public void PrintSummary()
    {
        var groups = _results.GroupBy(r => r.Id[..1]).OrderBy(g => g.Key);
        var sb = new StringBuilder();
        sb.AppendLine();
        sb.AppendLine("=========================================================================");
        sb.AppendLine("  PASS/FAIL SUMMARY  -  ملخص الإثباتات (بدون Marten)");
        sb.AppendLine("=========================================================================");
        sb.AppendLine(CultureInfo.InvariantCulture, $"  {"ID",-7} {"RESULT",-7} PROOF");
        sb.AppendLine("  " + new string('-', 69));
        foreach (var g in groups)
        {
            foreach (var r in g)
                sb.AppendLine(CultureInfo.InvariantCulture, $"  {r.Id,-7} {(r.Verdict == Verdict.Pass ? "PASS" : "FAIL"),-7} {r.Name}");
            sb.AppendLine("  " + new string('-', 69));
        }
        var passed = _results.Count(r => r.Verdict == Verdict.Pass);
        sb.AppendLine(CultureInfo.InvariantCulture, $"  {passed}/{_results.Count} proofs passed");

        foreach (var section in new[] { "A", "B", "C", "D", "E" })
        {
            var items = _results.Where(r => r.Id.StartsWith(section, StringComparison.Ordinal)).ToList();
            if (items.Count == 0) continue;
            var ok = items.All(r => r.Verdict == Verdict.Pass);
            sb.AppendLine(CultureInfo.InvariantCulture, $"  ({section})  {(ok ? "PASS" : "FAIL")}   {items.Count(r => r.Verdict == Verdict.Pass)}/{items.Count}   {SectionTitle(section)}");
        }
        sb.AppendLine("=========================================================================");
        if (_notes.Count > 0)
        {
            sb.AppendLine("  NOTES / ملاحظات");
            foreach (var n in _notes) sb.AppendLine(CultureInfo.InvariantCulture, $"   * {n}");
            sb.AppendLine("=========================================================================");
        }
        Console.Write(sb.ToString());
    }

    private static string SectionTitle(string s) => s switch
    {
        "A" => "Wolverine durable transactional outbox WITHOUT Marten",
        "B" => "EF Core 10 append-only ledger with revoked grants",
        "C" => "Relational append-only event log (process narrative)",
        "D" => "Per-tenant flexible JSONB documents",
        "E" => "Hash chain + gapless counter + tamper detection",
        _ => ""
    };
}
