using Marten.Events.Aggregation;

namespace BabelSpike;

// ---------------------------------------------------------------------------
// (a) decimal precision probe document
// ---------------------------------------------------------------------------
public class MoneyDoc
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Label { get; set; } = "";
    public decimal Amount { get; set; }
}

// Normalised-vs-jsonb benchmark document. Amount is a *duplicated* field so
// Marten also writes it to a real numeric(19,4) column alongside the jsonb body.
public class LedgerLine
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Account { get; set; } = "";
    public decimal Amount { get; set; }
}

// ---------------------------------------------------------------------------
// (b) balanced journal entry
// ---------------------------------------------------------------------------
public record JournalLine(string Account, decimal Debit, decimal Credit);

public class UnbalancedJournalEntryException(decimal debit, decimal credit)
    : Exception($"Journal entry is unbalanced: debit={debit} credit={credit} difference={debit - credit}")
{
    public decimal Debit { get; } = debit;
    public decimal Credit { get; } = credit;
}

public class JournalEntry
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Reference { get; set; } = "";
    public DateOnly PostingDate { get; set; }
    public List<JournalLine> Lines { get; set; } = [];

    public decimal TotalDebit => Lines.Sum(l => l.Debit);
    public decimal TotalCredit => Lines.Sum(l => l.Credit);
    public bool IsBalanced => TotalDebit == TotalCredit;

    /// <summary>Domain guard. Throws when debits do not equal credits exactly.</summary>
    public void AssertBalanced()
    {
        if (Lines.Count == 0)
            throw new UnbalancedJournalEntryException(0m, 0m);
        if (!IsBalanced)
            throw new UnbalancedJournalEntryException(TotalDebit, TotalCredit);
    }
}

// ---------------------------------------------------------------------------
// (c) event store + projection
// ---------------------------------------------------------------------------
public record JournalEntryPosted(Guid EntryId, string Reference, DateOnly PostingDate);
public record LineDebited(string Account, decimal Amount);
public record LineCredited(string Account, decimal Amount);

/// <summary>Read model rebuilt purely from the event stream.</summary>
public class LedgerState
{
    public Guid Id { get; set; }
    public string Reference { get; set; } = "";
    public DateOnly PostingDate { get; set; }
    public decimal TotalDebit { get; set; }
    public decimal TotalCredit { get; set; }
    public int LineCount { get; set; }
    public Dictionary<string, decimal> BalanceByAccount { get; set; } = new();
}

public class LedgerStateProjection : SingleStreamProjection<LedgerState, Guid>
{
    public static LedgerState Create(JournalEntryPosted e) => new()
    {
        Id = e.EntryId,
        Reference = e.Reference,
        PostingDate = e.PostingDate
    };

    public void Apply(LineDebited e, LedgerState s)
    {
        s.TotalDebit += e.Amount;
        s.LineCount++;
        s.BalanceByAccount[e.Account] = s.BalanceByAccount.GetValueOrDefault(e.Account) + e.Amount;
    }

    public void Apply(LineCredited e, LedgerState s)
    {
        s.TotalCredit += e.Amount;
        s.LineCount++;
        s.BalanceByAccount[e.Account] = s.BalanceByAccount.GetValueOrDefault(e.Account) - e.Amount;
    }
}

// ---------------------------------------------------------------------------
// (d) Wolverine outbox message
// ---------------------------------------------------------------------------
public record JournalPosted(Guid EntryId, string Reference, decimal Total);

// ---------------------------------------------------------------------------
// (e) multi-tenancy probe document
// ---------------------------------------------------------------------------
public class TenantScopedDoc
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = "";
    public decimal Amount { get; set; }
}
