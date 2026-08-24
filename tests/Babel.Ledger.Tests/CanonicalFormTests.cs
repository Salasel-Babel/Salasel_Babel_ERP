using System.Globalization;
using Babel.Canonicalization;
using Babel.Canonicalization.Schemas;
using Babel.Ledger.Posting;
using Babel.Ledger.PostingMatrix;
using Xunit;

namespace Babel.Ledger.Tests;

/// <summary>
/// التكافؤ البايتي بين ما يُركّبه <c>ledger.post_entry</c> وما تُنتجه المكتبة المختومة.
/// <para>
/// هذا الاختبار هو الثمن الذي يُدفع مقابل «مكالمة خادم واحدة»: البايتات تُقطع في
/// C# وتُلحَم في SQL، فلولا إثباتٌ بايتيّ لصارت السلسلة قائمة على ثقة بقالب نصّي.
/// أي انحراف في المواصفة يُسقط البناء هنا قبل أن يُنتج بصمة لا يمكن التحقق منها.
/// </para>
/// </summary>
public sealed class CanonicalFormTests
{
    [Theory]
    [InlineData(1, 1, 0)]
    [InlineData(2, 7, 1)]
    [InlineData(9, 9, 2)]
    [InlineData(10, 42, 3)]
    [InlineData(99, 100, 5)]
    [InlineData(1_000, 999, 12)]
    [InlineData(123_456_789, 987_654_321, 40)]
    [InlineData(9_007_199_254_740_993, 1_000_000, 7)]
    public void The_split_reassembles_byte_for_byte_to_what_the_library_produces(long sequence, long entryNo, int lineCount)
    {
        byte[] previous = new byte[32];
        System.Security.Cryptography.RandomNumberGenerator.Fill(previous);

        CanonicalDocument document = Build(entryNo, Math.Max(2, lineCount));
        CanonicalSplit split = CanonicalSplit.Of(document);

        byte[] reassembled = split.Reassemble(sequence, previous, entryNo);
        byte[] authoritative = Canonicalizer.Compute(
            Build(entryNo, Math.Max(2, lineCount)), sequence, previous).CanonicalBytes;

        Assert.Equal(Convert.ToHexString(authoritative), Convert.ToHexString(reassembled));
    }

    [Fact]
    public void The_chain_link_is_inside_the_hashed_bytes_not_beside_them()
    {
        CanonicalDocument document = Build(5, 2);
        byte[] bytes = Canonicalizer.Compute(document, 17, new byte[32]).CanonicalBytes;
        string text = System.Text.Encoding.UTF8.GetString(bytes);

        Assert.Contains("chain_seq\tI\t2\t17\n", text, StringComparison.Ordinal);
        Assert.Contains("prev_hash\tB\t64\t" + new string('0', 64) + "\n", text, StringComparison.Ordinal);
        Assert.Contains("entry_no\tI\t1\t5\n", text, StringComparison.Ordinal);
    }

    private static CanonicalDocument Build(long entryNo, int lines)
    {
        CanonicalDocumentBuilder builder = JournalEntrySchema.V1.NewDocument();
        builder.Set("tenant_id", CanonicalValue.Text("aaaaaaaa-0000-4000-8000-000000000001"));
        builder.Set("book_id", CanonicalValue.Text("MAIN"));
        builder.Set("fiscal_year", CanonicalValue.Integer(2026));
        builder.Set("entry_id", CanonicalValue.Uuid(new Guid("01234567-89ab-4cde-8f01-23456789abcd")));
        builder.Set("entry_no", CanonicalValue.Integer(entryNo));
        builder.Set("entry_date", CanonicalValue.Date(new DateOnly(2026, 3, 15)));
        builder.Set("posted_at", CanonicalValue.Instant(new DateTime(2026, 3, 15, 9, 30, 15, DateTimeKind.Utc)));
        builder.Set("status", CanonicalValue.Token("POSTED"));
        builder.Set("actor", CanonicalValue.Text("محمد العبدالله"));
        builder.Set("memo", CanonicalValue.Text("Rent invoice"));
        builder.Set("memo_ar", CanonicalValue.Text("فاتورة إيجار\nسطر ثانٍ في البيان"));
        builder.Set("source_ref", CanonicalValue.Text("RentInvoice/INV-1"));
        builder.Set("idempotency_key", CanonicalValue.Text("rent-invoice:INV-1"));
        builder.Set("currency", CanonicalValue.Token("SAR"));

        builder.SetGroup("lines", Enumerable.Range(1, lines).Select(index => new Action<CanonicalItemBuilder>(item =>
        {
            item.Set("line_no", CanonicalValue.Integer(index));
            item.Set("account_code", CanonicalValue.Text((1300 + index).ToString(CultureInfo.InvariantCulture)));
            item.Set("debit", CanonicalValue.Amount(index % 2 == 1 ? 1_234.5678m : 0m));
            item.Set("credit", CanonicalValue.Amount(index % 2 == 1 ? 0m : 1_234.5678m));
            item.Set("cost_center", index % 3 == 0 ? CanonicalValue.Null() : CanonicalValue.Text("CC-01"));
            item.Set("description", CanonicalValue.Text("سطر رقم " + index.ToString(CultureInfo.InvariantCulture)));
        })));

        return builder.Build();
    }
}

/// <summary>تعابير المبالغ والشروط: ما يُفهَم يُحسب، وما لا يُفهَم <b>يوقف</b>.</summary>
public sealed class ExpressionTests
{
    [Theory]
    [InlineData("net + tax", 11500)]
    [InlineData("net", 10000)]
    [InlineData("tax", 1500)]
    [InlineData("net - tax", 8500)]
    [InlineData("net + tax - tax", 10000)]
    public void Linear_amount_expressions_evaluate_in_decimal(string expression, int expected)
    {
        Dictionary<string, decimal> amounts = new(StringComparer.Ordinal)
        {
            ["net"] = 10_000.0000m,
            ["tax"] = 1_500.0000m,
        };

        Assert.True(LinearExpression.TryEvaluate(expression, amounts, out decimal value, out _));
        Assert.Equal(expected, value);
    }

    [Fact]
    public void An_unknown_amount_variable_stops_rather_than_evaluating_to_zero()
    {
        Dictionary<string, decimal> amounts = new(StringComparer.Ordinal) { ["net"] = 1m };
        Assert.False(LinearExpression.TryEvaluate("net + retention", amounts, out _, out string? unknown));
        Assert.Equal("retention", unknown);
    }

    [Fact]
    public void A_condition_the_engine_cannot_evaluate_is_undecidable_not_false()
    {
        // «خطأ» و«لا أعرف» ليسا الشيء نفسه: الأول يُسقط سطر ضريبة بصمت،
        // والثاني يرفض القيد ويُرى.
        ConditionOutcome outcome = ConditionEvaluator.Evaluate(
            "is_taxable_supply",
            "document.has_any_line_with(tax_classification == 'standard')",
            new Dictionary<string, string>(StringComparer.Ordinal),
            new Dictionary<string, decimal>(StringComparer.Ordinal));

        Assert.False(outcome.Evaluated);
        Assert.False(outcome.Value);
        Assert.NotNull(outcome.Reason);
    }

    [Fact]
    public void An_explicit_condition_fact_settles_what_the_engine_cannot_parse()
    {
        ConditionOutcome outcome = ConditionEvaluator.Evaluate(
            "is_taxable_supply",
            "document.has_any_line_with(tax_classification == 'standard')",
            new Dictionary<string, string>(StringComparer.Ordinal) { ["condition.is_taxable_supply"] = "true" },
            new Dictionary<string, decimal>(StringComparer.Ordinal));

        Assert.True(outcome.Evaluated);
        Assert.True(outcome.Value);
    }

    [Theory]
    [InlineData("unit.vat_treatment == 'standard'", true)]
    [InlineData("unit.vat_treatment == 'exempt'", false)]
    [InlineData("unit.vat_treatment != 'exempt'", true)]
    [InlineData("owner.vat_registered == true and unit.vat_treatment == 'standard'", true)]
    [InlineData("owner.vat_registered == false or unit.vat_treatment == 'standard'", true)]
    public void Comparisons_and_conjunctions_evaluate_against_supplied_facts(string expression, bool expected)
    {
        Dictionary<string, string> facts = new(StringComparer.Ordinal)
        {
            ["unit.vat_treatment"] = "standard",
            ["owner.vat_registered"] = "true",
        };

        ConditionOutcome outcome = ConditionEvaluator.EvaluateExpression(
            expression, facts, new Dictionary<string, decimal>(StringComparer.Ordinal));

        Assert.True(outcome.Evaluated, outcome.Reason);
        Assert.Equal(expected, outcome.Value);
    }

    [Fact]
    public void Numeric_comparisons_read_the_event_amounts()
    {
        Dictionary<string, decimal> amounts = new(StringComparer.Ordinal)
        {
            ["net"] = 1_000m,
            ["tax"] = 150m,
            ["owner_trust_balance"] = 2_000m,
        };

        ConditionOutcome outcome = ConditionEvaluator.EvaluateExpression(
            "owner_trust_balance >= net + tax",
            new Dictionary<string, string>(StringComparer.Ordinal),
            amounts);

        Assert.True(outcome.Evaluated, outcome.Reason);
        Assert.True(outcome.Value);
    }

    [Fact]
    public void The_matrix_loads_every_committed_event_and_guard_rule()
    {
        MatrixCatalog catalog = MatrixCatalog.Default;
        Assert.Equal(89, catalog.EventCount);
        Assert.Equal(6, catalog.GuardRules.Count);
        Assert.Contains(catalog.GuardRules, rule => rule.RuleId == "GR-RE-001" && rule.Severity == "block");
    }
}
