using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// التسعير بالوحدة <b>وبالمستخدم</b>، فالمحوران يُلتقطان معاً أو لا معنى للقياس.
/// وما لا يُلتقط اليوم لا يُستعاد غداً — لا يوجد استعلام يستخرج ما لم يُكتب.
/// </summary>
public sealed class MeteringTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("33333333-3333-3333-3333-333333333333"));
    private static readonly UserId Accountant = new(Guid.Parse("44444444-4444-4444-4444-444444444444"));
    private static readonly DateTimeOffset Now = new(2026, 8, 24, 9, 30, 0, TimeSpan.Zero);

    private static (EntitlementEnforcer Enforcer, InMemoryUsageStore Usage, InMemoryEntitlementService Entitlements) Build()
    {
        FixedTimeProvider clock = new(Now);
        InMemoryUsageStore usage = new();
        InMemoryEntitlementService entitlements = new(new InMemoryAuditLog(), clock);
        return (new EntitlementEnforcer(entitlements, usage, clock), usage, entitlements);
    }

    [Fact]
    public async Task AnAllowedCall_IsMeteredOnBothAxes()
    {
        (EntitlementEnforcer enforcer, InMemoryUsageStore usage, _) = Build();

        Result result = await enforcer.EnsureAsync(
            Tenant, Accountant, BabelModule.Sales, EntitlementAccess.Write, "Sales.IssueInvoiceAsync", TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);

        BillingPeriod period = BillingPeriod.FromInstant(Now);
        IReadOnlyDictionary<BabelModule, long> moduleUsage =
            await usage.GetModuleUsageAsync(Tenant, period, TestContext.Current.CancellationToken);
        IReadOnlyCollection<UserId> activeUsers =
            await usage.GetActiveUsersAsync(Tenant, period, TestContext.Current.CancellationToken);

        Assert.Equal(1, moduleUsage[BabelModule.Sales]);
        Assert.Contains(Accountant, activeUsers);
    }

    [Fact]
    public async Task ARefusedCall_IsNotMetered()
    {
        // لا يُفوتَر العميل على نداء رُفض.
        (EntitlementEnforcer enforcer, InMemoryUsageStore usage, _) = Build();

        Result result = await enforcer.EnsureAsync(
            Tenant, Accountant, BabelModule.Pos, EntitlementAccess.Write, "Pos.SellAsync", TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);
        Assert.Equal("entitlement.not_entitled", result.Errors[0].Code);
        Assert.Empty(await usage.GetModuleUsageAsync(Tenant, BillingPeriod.FromInstant(Now), TestContext.Current.CancellationToken));
    }

    [Fact]
    public async Task ReadOnlyModule_AllowsReadsAndRefusesWritesAtTheServiceBoundary()
    {
        // الإنفاذ عند حدّ الخدمة لا عند الواجهة: إخفاء عنصر من القائمة لا يمنع نداء HTTP.
        (EntitlementEnforcer enforcer, _, InMemoryEntitlementService entitlements) = Build();

        Result<EntitlementSet> applied = await entitlements.ApplyAsync(
            new EntitlementChangeRequest(
                Tenant,
                new Dictionary<BabelModule, EntitlementState> { [BabelModule.Hr] = EntitlementState.ReadOnly },
                Accountant,
                "lapsed"),
            TestContext.Current.CancellationToken);
        Assert.True(applied.IsSuccess);

        Result read = await enforcer.EnsureAsync(
            Tenant, Accountant, BabelModule.Hr, EntitlementAccess.Read, "Hr.ReadPayrollAsync", TestContext.Current.CancellationToken);
        Result write = await enforcer.EnsureAsync(
            Tenant, Accountant, BabelModule.Hr, EntitlementAccess.Write, "Hr.RunPayrollAsync", TestContext.Current.CancellationToken);

        Assert.True(read.IsSuccess);
        Assert.True(write.IsFailure);
        Assert.Equal("entitlement.read_only", write.Errors[0].Code);
    }
}
