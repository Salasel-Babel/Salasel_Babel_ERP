namespace Babel.Ledger.Persistence;

/// <summary>سطر القيد. كل مبلغ <c>decimal</c> ومقياسه 4 — مفروض ببناء في Rule04.</summary>
internal sealed class JournalLineRow
{
    public Guid Id { get; set; }

    public Guid JournalEntryId { get; set; }

    public JournalEntryRow? Entry { get; set; }

    public Guid AccountId { get; set; }

    public decimal DebitAmount { get; set; }

    public decimal CreditAmount { get; set; }

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal ExchangeRate { get; set; }
}
