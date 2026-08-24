using System.Diagnostics;

namespace BabelPosOffline.Support;

public sealed record ProofResult(string Id, string TitleAr, bool Pass, string Evidence, TimeSpan Elapsed);

public static class Proof
{
    private static readonly List<ProofResult> _results = [];
    public static IReadOnlyList<ProofResult> Results => _results;

    public static void Section(string title)
    {
        Console.WriteLine();
        Console.WriteLine(new string('═', 100));
        Console.WriteLine($"  {title}");
        Console.WriteLine(new string('═', 100));
    }

    public static void Note(string s) => Console.WriteLine($"    · {s}");

    public static async Task RunAsync(string id, string titleAr, Func<Task<(bool, string)>> body)
    {
        var sw = Stopwatch.StartNew();
        bool ok; string ev;
        try { (ok, ev) = await body(); }
        catch (Exception ex) { ok = false; ev = $"EXCEPTION {ex.GetType().Name}: {ex.Message}"; }
        sw.Stop();
        _results.Add(new ProofResult(id, titleAr, ok, ev, sw.Elapsed));
        var tag = ok ? "PASS" : "FAIL";
        Console.WriteLine($"  [{tag}] {id,-6} {titleAr}");
        foreach (var line in ev.Split('\n'))
            if (line.Length > 0) Console.WriteLine($"         {line}");
        Console.WriteLine($"         ({sw.Elapsed.TotalMilliseconds:F0} ms)");
    }

    public static void Run(string id, string titleAr, Func<(bool, string)> body) =>
        RunAsync(id, titleAr, () => Task.FromResult(body())).GetAwaiter().GetResult();

    public static int Summary()
    {
        Console.WriteLine();
        Console.WriteLine(new string('═', 100));
        Console.WriteLine("  جدول النتائج / results");
        Console.WriteLine(new string('═', 100));
        var w = _results.Count == 0 ? 10 : _results.Max(r => r.TitleAr.Length);
        foreach (var r in _results)
            Console.WriteLine($"  {(r.Pass ? "PASS" : "FAIL")}  {r.Id,-6}  {r.TitleAr.PadRight(w)}  {r.Elapsed.TotalMilliseconds,8:F0} ms");
        var failed = _results.Count(r => !r.Pass);
        Console.WriteLine(new string('─', 100));
        Console.WriteLine($"  {_results.Count - failed} / {_results.Count} PASS" + (failed > 0 ? $"   — {failed} FAIL" : ""));
        return failed;
    }
}
