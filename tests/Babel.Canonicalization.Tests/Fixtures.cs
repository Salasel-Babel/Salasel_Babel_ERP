using System.Globalization;
using Babel.Canonicalization.Schemas;

namespace Babel.Canonicalization.Tests;

/// <summary>قيد مرجعي مشترك بين الاختبارات.</summary>
internal static class Fixtures
{
    public const string Tenant = "acme";
    public const string Book = "MAIN";
    public const int Year = 2026;

    public static readonly byte[] Genesis = JournalEntrySchema.Genesis(Tenant, Book, Year);

    public static readonly DateTime Posted =
        new DateTime(2026, 5, 1, 9, 30, 0, DateTimeKind.Utc).AddTicks(1234560);

    public static CanonicalDocument Entry(
        string? memoAr = "قيد إثبات إيراد مبيعات - فرع الرياض",
        string? memo = "revenue recognition",
        decimal amount = 1500.0000m,
        long entryNo = 42,
        Guid? entryId = null,
        DateTime? postedAt = null,
        string status = "POSTED",
        int lineCount = 2,
        string idempotencyKey = "pos-2026-05-01-000042")
    {
        var b = JournalEntrySchema.V1.NewDocument()
            .Set("tenant_id", CanonicalValue.Text(Tenant))
            .Set("book_id", CanonicalValue.Text(Book))
            .Set("fiscal_year", CanonicalValue.Integer(Year))
            .Set("entry_id", CanonicalValue.Uuid(entryId ?? Guid.Parse("0192f3c8-0000-7000-8000-000000000001")))
            .Set("entry_no", CanonicalValue.Integer(entryNo))
            .Set("entry_date", CanonicalValue.Date(new DateOnly(2026, 5, 1)))
            .Set("posted_at", CanonicalValue.Instant(postedAt ?? Posted))
            .Set("status", CanonicalValue.Token(status))
            .Set("actor", CanonicalValue.Text("muhasib@acme.sa"))
            .Set("memo", CanonicalValue.TextOrNull(memo))
            .Set("memo_ar", CanonicalValue.TextOrNull(memoAr))
            .Set("source_ref", CanonicalValue.Null())
            .Set("idempotency_key", CanonicalValue.Text(idempotencyKey))
            .Set("currency", CanonicalValue.Token("SAR"));

        var items = new List<Action<CanonicalItemBuilder>>();
        if (lineCount == 2)
        {
            items.Add(i => i.Set("line_no", CanonicalValue.Integer(1))
                            .Set("account_code", CanonicalValue.Text("1010"))
                            .Set("debit", CanonicalValue.Amount(amount))
                            .Set("credit", CanonicalValue.Amount(0m))
                            .Set("cost_center", CanonicalValue.Null())
                            .Set("description", CanonicalValue.Text("النقدية")));
            items.Add(i => i.Set("line_no", CanonicalValue.Integer(2))
                            .Set("account_code", CanonicalValue.Text("4010"))
                            .Set("debit", CanonicalValue.Amount(0m))
                            .Set("credit", CanonicalValue.Amount(amount))
                            .Set("cost_center", CanonicalValue.Null())
                            .Set("description", CanonicalValue.Text("المبيعات")));
        }
        else
        {
            for (var k = 1; k <= lineCount; k++)
            {
                var n = k;
                items.Add(i => i.Set("line_no", CanonicalValue.Integer(n))
                                .Set("account_code", CanonicalValue.Text((1000 + n).ToString(CultureInfo.InvariantCulture)))
                                .Set("debit", CanonicalValue.Amount(n % 2 == 1 ? 10.0000m : 0m))
                                .Set("credit", CanonicalValue.Amount(n % 2 == 1 ? 0m : 10.0000m))
                                .Set("cost_center", CanonicalValue.Null())
                                .Set("description", CanonicalValue.Text($"سطر {n.ToString(CultureInfo.InvariantCulture)}")));
            }
        }

        return b.SetGroup("lines", items).Build();
    }
}
