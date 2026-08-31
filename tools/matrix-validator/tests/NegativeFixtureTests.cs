using System.Text.Json;
using System.Text.Json.Nodes;
using SalaselBabel.MatrixValidator.Rules;
using Xunit;

namespace SalaselBabel.MatrixValidator.Tests;

/// <summary>
/// A validator nobody has broken on purpose is a validator nobody knows works. Each test here
/// corrupts one thing in a copy of the real seed and asserts the matching rule fires.
/// أداة تحقق لم يكسرها أحد عمداً هي أداة لا يعرف أحد أنها تعمل.
/// </summary>
public class NegativeFixtureTests : IDisposable
{
    private readonly string _root = SeedData.CopyToTemp();

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, recursive: true);
        GC.SuppressFinalize(this);
    }

    private IReadOnlyList<Finding> Validate() => Program.Validate(_root, out _);

    private void AssertFires(string ruleId)
    {
        var findings = Validate();
        Assert.True(findings.Any(f => f.RuleId == ruleId && f.Severity == Severity.Error),
            $"expected rule {ruleId} to fire, got: " + string.Join(" | ", findings.Select(f => f.RuleId).Distinct()));
    }

    private string Accounts => Path.Combine(_root, "chart-of-accounts", "accounts.csv");
    private string Roles => Path.Combine(_root, "posting-matrix", "account-roles.csv");
    private string RoleMap => Path.Combine(_root, "posting-matrix", "role-map.default.csv");
    private string SalesEvents => Path.Combine(_root, "posting-matrix", "events", "sales.json");

    private void EditSalesInvoice(Action<JsonObject> edit)
    {
        var doc = JsonNode.Parse(File.ReadAllText(SalesEvents))!.AsObject();
        var ev = doc["events"]!.AsArray()
            .First(e => e!["event_code"]!.GetValue<string>() == "sales.invoice.posted")!.AsObject();
        edit(ev);
        File.WriteAllText(SalesEvents, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    // --- V01: a role with no mapping -------------------------------------------------
    [Fact]
    public void V01_fires_when_a_role_loses_its_default_mapping()
    {
        var lines = File.ReadAllLines(RoleMap)
            .Where(l => !l.StartsWith("__default__,vat_output,*,", StringComparison.Ordinal))
            .ToArray();
        File.WriteAllLines(RoleMap, lines);
        AssertFires("V01");
    }

    // --- V02: a matrix line referencing an unknown role -------------------------------
    [Fact]
    public void V02_fires_when_a_line_names_a_role_that_does_not_exist()
    {
        EditSalesInvoice(ev => ev["lines"]!.AsArray()[1]!["role"] = "revenue_account_that_does_not_exist");
        AssertFires("V02");
    }

    // --- V03: lines that cannot balance ----------------------------------------------
    [Fact]
    public void V03_fires_when_a_line_amount_no_longer_balances()
    {
        EditSalesInvoice(ev => ev["lines"]!.AsArray()[0]!["amount"] = "net");   // was net + tax
        AssertFires("V03");
    }

    /// <summary>
    /// The real-world defect: an author gates the VAT line on a condition but forgets that the
    /// receivable line still carries the tax in the exempt scenario. The books would be out by
    /// exactly the VAT on every exempt invoice.
    /// </summary>
    [Fact]
    public void V03_fires_when_the_exempt_scenario_forgets_to_zero_the_tax_it_no_longer_charges()
    {
        EditSalesInvoice(ev =>
        {
            var exempt = ev["scenarios"]!.AsArray()
                .First(s => s!["code"]!.GetValue<string>() == "exempt_or_zero")!.AsObject();
            exempt["zero_amounts"] = new JsonArray();
        });
        AssertFires("V03");
    }

    // --- V04: posting to a rollup ----------------------------------------------------
    [Fact]
    public void V04_fires_when_a_role_is_pointed_at_a_rollup_account()
    {
        var lines = File.ReadAllLines(RoleMap)
            .Select(l => l.StartsWith("__default__,sales_revenue,*,4101,", StringComparison.Ordinal)
                ? "__default__,sales_revenue,*,410,drafted,," : l)
            .ToArray();
        File.WriteAllLines(RoleMap, lines);
        AssertFires("V04");
    }

    // --- V05: a missing mandatory dimension -------------------------------------------
    [Fact]
    public void V05_fires_when_a_line_drops_a_dimension_its_account_requires()
    {
        EditSalesInvoice(ev => ev["lines"]!.AsArray()[1]!["dimensions"] = new JsonArray());
        AssertFires("V05");
    }

    // --- V06: a missing Arabic or English name -----------------------------------------
    [Fact]
    public void V06_fires_when_an_account_loses_its_arabic_name()
    {
        var lines = File.ReadAllLines(Accounts);
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("4301,", StringComparison.Ordinal))
            {
                var f = lines[i].Split(',');
                f[1] = "";
                lines[i] = string.Join(",", f);
            }
        File.WriteAllLines(Accounts, lines);
        AssertFires("V06");
    }

    [Fact]
    public void V06_fires_when_an_event_loses_its_english_name()
    {
        EditSalesInvoice(ev => ev["name_en"] = "");
        AssertFires("V06");
    }

    [Fact]
    public void V06_fires_when_an_amount_variable_loses_its_derivation()
    {
        EditSalesInvoice(ev => ev["amounts"]!["net"]!["derivation_ar"] = "");
        AssertFires("V06");
    }

    // --- V07: a conditional rule that can never fire -------------------------------------
    [Fact]
    public void V07_fires_when_no_scenario_ever_makes_a_condition_true()
    {
        EditSalesInvoice(ev =>
        {
            foreach (var s in ev["scenarios"]!.AsArray())
                s!["true_conditions"] = new JsonArray();
        });
        AssertFires("V07");
    }

    [Fact]
    public void V07_fires_when_a_condition_is_true_in_every_scenario()
    {
        EditSalesInvoice(ev =>
        {
            foreach (var s in ev["scenarios"]!.AsArray())
                s!["true_conditions"] = new JsonArray("is_taxable_supply");
        });
        AssertFires("V07");
    }

    [Fact]
    public void V07_fires_when_an_event_has_conditional_lines_but_declares_no_scenarios()
    {
        EditSalesInvoice(ev => ev["scenarios"] = new JsonArray());
        AssertFires("V07");
    }

    // --- V08 / V11 / V15: role map integrity ---------------------------------------------
    [Fact]
    public void V08_fires_when_a_role_points_at_an_account_that_is_not_in_the_chart()
    {
        var lines = File.ReadAllLines(RoleMap)
            .Select(l => l.StartsWith("__default__,sales_revenue,*,4101,", StringComparison.Ordinal)
                ? "__default__,sales_revenue,*,9999,drafted,," : l)
            .ToArray();
        File.WriteAllLines(RoleMap, lines);
        AssertFires("V08");
    }

    [Fact]
    public void V11_fires_when_a_revenue_role_is_pointed_at_an_expense_account()
    {
        var lines = File.ReadAllLines(RoleMap)
            .Select(l => l.StartsWith("__default__,sales_revenue,*,4101,", StringComparison.Ordinal)
                ? "__default__,sales_revenue,*,5901,drafted,," : l)
            .ToArray();
        File.WriteAllLines(RoleMap, lines);
        AssertFires("V11");
    }

    [Fact]
    public void V15_fires_when_a_role_mapped_account_is_left_deletable()
    {
        var lines = File.ReadAllLines(Accounts);
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("4301,", StringComparison.Ordinal))
            {
                var f = lines[i].Split(',');
                f[14] = "false";   // is_protected
                lines[i] = string.Join(",", f);
            }
        File.WriteAllLines(Accounts, lines);
        AssertFires("V15");
    }

    // --- V09 / V10: chart structure -------------------------------------------------------
    [Fact]
    public void V09_fires_when_an_account_is_reparented_away_from_its_code()
    {
        var lines = File.ReadAllLines(Accounts);
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("4301,", StringComparison.Ordinal))
            {
                var f = lines[i].Split(',');
                f[3] = "410";
                lines[i] = string.Join(",", f);
            }
        File.WriteAllLines(Accounts, lines);
        AssertFires("V09");
    }

    [Fact]
    public void V10_fires_on_a_duplicate_account_code()
    {
        var lines = File.ReadAllLines(Accounts).ToList();
        lines.Add(lines.First(l => l.StartsWith("4301,", StringComparison.Ordinal)));
        File.WriteAllLines(Accounts, lines);
        AssertFires("V10");
    }

    // --- V12: an undeclared amount variable -------------------------------------------------
    [Fact]
    public void V12_fires_when_a_line_uses_an_amount_nobody_declared()
    {
        EditSalesInvoice(ev => ev["lines"]!.AsArray()[0]!["amount"] = "net + tax + mystery_amount");
        AssertFires("V12");
    }

    // --- V13: a subledger the reconciliation needs ------------------------------------------
    [Fact]
    public void V13_fires_when_a_control_account_line_drops_its_subledger()
    {
        EditSalesInvoice(ev => ev["lines"]!.AsArray()[0]!["subledger"] = null);
        AssertFires("V13");
    }

    // --- V14: a guard rule pointing at a role that no longer exists ---------------------------
    [Fact]
    public void V14_fires_when_a_guard_rule_names_an_unknown_role()
    {
        var path = Path.Combine(_root, "posting-matrix", "guard-rules.json");
        var doc = JsonNode.Parse(File.ReadAllText(path))!.AsObject();
        doc["rules"]!.AsArray()[0]!["applies_to"]!["role"] = "no_such_role";
        File.WriteAllText(path, doc.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
        AssertFires("V14");
    }

    // --- V18: an event that cannot make an entry ----------------------------------------------
    [Fact]
    public void V18_fires_when_a_scenario_leaves_fewer_than_two_lines()
    {
        EditSalesInvoice(ev =>
        {
            var arr = ev["lines"]!.AsArray();
            while (arr.Count > 1) arr.RemoveAt(arr.Count - 1);
            ev["conditions"] = new JsonObject();
            ev["scenarios"] = new JsonArray();
            ev["amounts"] = new JsonObject
            {
                ["net"] = new JsonObject
                {
                    ["name_ar"] = "صافي", ["name_en"] = "net",
                    ["derivation_ar"] = "من الفاتورة", ["derivation_en"] = "from the invoice"
                }
            };
            arr[0]!["amount"] = "net";
        });
        AssertFires("V18");
    }

    // --- V26: a caveat citing an account that was renumbered away -------------------------------
    [Fact]
    public void V26_fires_when_a_caveat_cites_an_account_code_that_does_not_exist()
    {
        EditSalesInvoice(ev => ev["caveats"] = new JsonArray(new JsonObject
        {
            ["ref"] = "test",
            ["text_ar"] = "⚠️ راجع الحساب 4999 قبل الاعتماد",
            ["text_en"] = "⚠️ review account 4999 before approval"
        }));
        AssertFires("V26");
    }

    // --- V24: a contra account whose natural side stops being inverted --------------------------
    [Fact]
    public void V24_fires_when_accumulated_depreciation_is_given_a_debit_nature()
    {
        var lines = File.ReadAllLines(Accounts);
        for (var i = 0; i < lines.Length; i++)
            if (lines[i].StartsWith("1502,", StringComparison.Ordinal))
            {
                var f = lines[i].Split(',');
                f[6] = "debit";
                lines[i] = string.Join(",", f);
            }
        File.WriteAllLines(Accounts, lines);
        AssertFires("V24");
    }
}
