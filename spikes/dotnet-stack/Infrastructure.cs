using System.Collections.Concurrent;
using System.Diagnostics;

namespace BabelSpike;

/// <summary>Records every JournalPosted message Wolverine actually delivers.</summary>
public static class MessageLog
{
    public static readonly ConcurrentBag<JournalPosted> Received = [];

    public static void Clear() => Received.Clear();

    public static bool Contains(Guid entryId) => Received.Any(m => m.EntryId == entryId);

    /// <summary>Polls until the message shows up or the timeout expires.</summary>
    public static async Task<bool> WaitForAsync(Guid entryId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (Contains(entryId)) return true;
            await Task.Delay(100);
        }
        return Contains(entryId);
    }

    /// <summary>Waits the full window and asserts the message never arrived.</summary>
    public static async Task<bool> StaysAbsentAsync(Guid entryId, TimeSpan window)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < window)
        {
            if (Contains(entryId)) return false;
            await Task.Delay(100);
        }
        return !Contains(entryId);
    }
}

/// <summary>Wolverine message handler. Discovered by convention.</summary>
public class JournalPostedHandler
{
    public void Handle(JournalPosted message) => MessageLog.Received.Add(message);
}

// ---------------------------------------------------------------------------
// PASS/FAIL result plumbing
// ---------------------------------------------------------------------------
public record ProofResult(string Id, string Name, bool Passed, string Detail);

public class ProofRecorder
{
    private readonly List<ProofResult> _results = [];
    private readonly List<string> _notes = [];

    public IReadOnlyList<ProofResult> Results => _results;
    public IReadOnlyList<string> Notes => _notes;
    public bool AllPassed => _results.All(r => r.Passed);

    public void Record(string id, string name, bool passed, string detail)
    {
        _results.Add(new ProofResult(id, name, passed, detail));
        var tag = passed ? "PASS" : "FAIL";
        Console.WriteLine($"  [{tag}] {id} {name}");
        if (!string.IsNullOrWhiteSpace(detail))
            foreach (var line in detail.Split('\n'))
                Console.WriteLine($"         {line}");
    }

    public void Note(string note)
    {
        _notes.Add(note);
        Console.WriteLine($"  [note] {note}");
    }

    public void PrintSummary()
    {
        Console.WriteLine();
        Console.WriteLine("=================================================================");
        Console.WriteLine("  SPIKE SUMMARY  /  ملخص نتائج الاختبار");
        Console.WriteLine("=================================================================");
        Console.WriteLine($"  {"PROOF",-6} {"RESULT",-8} DESCRIPTION");
        Console.WriteLine("  " + new string('-', 61));
        foreach (var r in _results)
            Console.WriteLine($"  {r.Id,-6} {(r.Passed ? "PASS" : "FAIL"),-8} {r.Name}");
        Console.WriteLine("  " + new string('-', 61));
        var passed = _results.Count(r => r.Passed);
        Console.WriteLine($"  {passed}/{_results.Count} proofs passed");
        Console.WriteLine("=================================================================");
    }
}
