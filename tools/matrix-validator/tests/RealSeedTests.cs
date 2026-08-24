using SalaselBabel.MatrixValidator;
using SalaselBabel.MatrixValidator.Model;
using SalaselBabel.MatrixValidator.Rules;
using Xunit;

namespace SalaselBabel.MatrixValidator.Tests;

/// <summary>
/// The test the whole deliverable stands on: the real seed data — the one that ships — loads and
/// passes every rule. If this ever fails, the chart of accounts or the posting matrix is wrong,
/// and no posting engine built on top of them can be right.
/// الاختبار الذي يقوم عليه التسليم كله: البيانات التأسيسية الحقيقية تُحمَّل وتجتاز كل القواعد.
/// </summary>
public class RealSeedTests
{
    [Fact]
    public void The_real_seed_data_loads_and_passes_every_rule()
    {
        var findings = Program.Validate(SeedData.Root, out var ds);
        var errors = findings.Where(f => f.Severity == Severity.Error).ToList();

        Assert.Empty(ds.LoadErrors);
        Assert.True(errors.Count == 0,
            "the real seed data must validate cleanly, but:\n" + string.Join("\n", errors));
    }

    [Fact]
    public void The_real_seed_data_is_not_trivially_small()
    {
        Program.Validate(SeedData.Root, out var ds);

        Assert.True(ds.Accounts.Count >= 150, "the chart of accounts is suspiciously small");
        Assert.True(ds.Accounts.Count(a => a.IsPostable) >= 90, "too few postable accounts");
        Assert.True(ds.Events.Count >= 60, "too few business events covered");
        Assert.True(ds.Roles.Count >= 60, "too few account roles");
        Assert.True(ds.Events.Sum(e => e.Lines.Count) >= 180, "too few posting lines");
    }

    [Fact]
    public void Every_account_carries_both_names()
    {
        Program.Validate(SeedData.Root, out var ds);
        Assert.All(ds.Accounts, a =>
        {
            Assert.False(string.IsNullOrWhiteSpace(a.NameAr));
            Assert.False(string.IsNullOrWhiteSpace(a.NameEn));
        });
    }

    [Fact]
    public void Every_role_resolves_to_a_postable_account()
    {
        Program.Validate(SeedData.Root, out var ds);
        Assert.All(ds.Roles, r =>
        {
            var account = ds.Resolve(r.Code);
            Assert.NotNull(account);
            Assert.True(account!.IsPostable, $"role {r.Code} resolves to rollup {account.Code}");
        });
    }

    [Fact]
    public void The_rental_revenue_block_rule_exists_and_names_a_real_role()
    {
        Program.Validate(SeedData.Root, out var ds);
        var rule = ds.GuardRules.SingleOrDefault(g => g.RuleId == "GR-RE-001");

        Assert.NotNull(rule);
        Assert.Equal("block", rule!.Severity);
        Assert.Equal("rental_revenue", rule.AppliesTo?.Role);
        Assert.Contains("managed_for_others", rule.Condition);
        Assert.True(ds.RolesByCode.ContainsKey(rule.AppliesTo!.Role!));
    }

    [Fact]
    public void No_managed_property_event_ever_touches_the_rental_revenue_role()
    {
        Program.Validate(SeedData.Root, out var ds);

        var managed = ds.Events.Where(e => e.EventCode.Contains("managed", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(managed);

        foreach (var e in managed)
            Assert.DoesNotContain(e.Lines, l => l.Role == "rental_revenue");
    }
}
