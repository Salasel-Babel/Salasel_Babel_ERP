using System.Collections.Concurrent;
using System.Diagnostics;

namespace BabelRelationalSpike.Support;

/// <summary>Integration event published by the ledger. Plain record, no Marten types involved.</summary>
public record JournalPostedNotice(Guid EntryId, long EntryNo, string BookId, string Tag);

/// <summary>Wolverine discovers this handler by convention. No Marten, no IDocumentSession.</summary>
public class JournalPostedNoticeHandler
{
    public void Handle(JournalPostedNotice message) => DeliveryLog.Delivered.Add(message);
}

public static class DeliveryLog
{
    public static readonly ConcurrentBag<JournalPostedNotice> Delivered = [];

    public static bool Has(Guid entryId) => Delivered.Any(m => m.EntryId == entryId);

    public static async Task<bool> WaitForAsync(Guid entryId, TimeSpan timeout)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < timeout)
        {
            if (Has(entryId)) return true;
            await Task.Delay(100);
        }
        return Has(entryId);
    }

    /// <summary>Waits out the WHOLE window and asserts the message never showed up.</summary>
    public static async Task<bool> StaysAbsentAsync(Guid entryId, TimeSpan window)
    {
        var sw = Stopwatch.StartNew();
        while (sw.Elapsed < window)
        {
            if (Has(entryId)) return false;
            await Task.Delay(250);
        }
        return !Has(entryId);
    }
}
