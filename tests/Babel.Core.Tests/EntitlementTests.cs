using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Core.Tests;

/// <summary>
/// الحالات الثلاث، ورفض المجموعة غير المتسقة، والتدقيق على كل تغيير.
/// </summary>
public sealed class EntitlementTests
{
    private static readonly TenantId Tenant = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly UserId Admin = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));

    [Fact]
    public void Baseline_TurnsOnEveryMandatoryModuleAndNothingElse()
    {
        EntitlementSet set = EntitlementSet.Baseline(Tenant);

        Assert.Equal(EntitlementState.Entitled, set.StateOf(BabelModule.Core));
        Assert.Equal(EntitlementState.Entitled, set.StateOf(BabelModule.Ledger));
        Assert.Equal(EntitlementState.Entitled, set.StateOf(BabelModule.Sales));
        Assert.Equal(EntitlementState.Entitled, set.StateOf(BabelModule.Purchasing));
        Assert.Equal(EntitlementState.Entitled, set.StateOf(BabelModule.Compliance));
        Assert.Equal(EntitlementState.NotEntitled, set.StateOf(BabelModule.Pos));
    }

    [Fact]
    public void ReadOnly_AllowsReadingAndRefusesWriting()
    {
        // ق-16: البيانات التاريخية والتقارير تعمل، والكتابة موقوفة.
        Result<EntitlementSet> set = EntitlementSet.Baseline(Tenant).With(new Dictionary<BabelModule, EntitlementState>
        {
            [BabelModule.Inventory] = EntitlementState.ReadOnly,
        });

        Assert.True(set.IsSuccess);
        Assert.True(set.Value.Allows(BabelModule.Inventory, EntitlementAccess.Read));
        Assert.False(set.Value.Allows(BabelModule.Inventory, EntitlementAccess.Write));
    }

    [Fact]
    public void NotEntitled_RefusesEvenReading()
    {
        EntitlementSet set = EntitlementSet.Baseline(Tenant);

        Assert.False(set.Allows(BabelModule.Pos, EntitlementAccess.Read));
        Assert.False(set.Allows(BabelModule.Pos, EntitlementAccess.Write));
    }

    [Fact]
    public void MandatoryModule_CannotBeDisabled()
    {
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(new Dictionary<BabelModule, EntitlementState>
        {
            [BabelModule.Ledger] = EntitlementState.NotEntitled,
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "entitlement.mandatory_disabled");
    }

    [Fact]
    public void Pos_WithoutInventory_IsRejectedAsIncoherent()
    {
        // رسم الاعتماد: نقاط البيع تتطلب المخزون. بيع دون حركة مخزون مجموعة غير متسقة.
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(new Dictionary<BabelModule, EntitlementState>
        {
            [BabelModule.Pos] = EntitlementState.Entitled,
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "entitlement.unsatisfied_requirement");
    }

    [Fact]
    public void Pos_EntitledOverReadOnlyInventory_IsRejected()
    {
        // القدرة لا تتجاوز قدرة ما تُعتمد عليه: مخزون للقراءة فقط تحت نقاط بيع فاعلة
        // يعني بيعاً لا يستطيع أن ينقص رصيداً.
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(new Dictionary<BabelModule, EntitlementState>
        {
            [BabelModule.Inventory] = EntitlementState.ReadOnly,
            [BabelModule.Pos] = EntitlementState.Entitled,
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "entitlement.unsatisfied_requirement");
    }

    [Fact]
    public void Pos_WithInventory_IsAccepted()
    {
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(new Dictionary<BabelModule, EntitlementState>
        {
            [BabelModule.Inventory] = EntitlementState.Entitled,
            [BabelModule.Pos] = EntitlementState.Entitled,
        });

        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void Projects_RequiresInventory()
    {
        Result<EntitlementSet> result = EntitlementSet.Baseline(Tenant).With(new Dictionary<BabelModule, EntitlementState>
        {
            [BabelModule.Projects] = EntitlementState.Entitled,
        });

        Assert.True(result.IsFailure);
        Assert.Contains(result.Errors, error => error.Code == "entitlement.unsatisfied_requirement");
    }

    [Fact]
    public void ModuleDependencyGraph_IsAcyclic()
    {
        // دورة في الرسم تجعل كل تحقق اتساق غير قابل للحسم.
        foreach (BabelModule module in ModuleDependencyGraph.All)
        {
            Assert.DoesNotContain(module, ModuleDependencyGraph.TransitiveRequirementsOf(module));
        }
    }

    [Fact]
    public async Task ApplyAsync_WritesWhoAndWhenToTheAuditLog()
    {
        // وثيقة المعمارية §14: سجل التدقيق كامل ومؤرَّخ ولا يمكن تعطيله.
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 8, 24, 9, 30, 0, TimeSpan.Zero));
        InMemoryAuditLog auditLog = new();
        InMemoryEntitlementService service = new(auditLog, clock);

        Result<EntitlementSet> result = await service.ApplyAsync(
            new EntitlementChangeRequest(
                Tenant,
                new Dictionary<BabelModule, EntitlementState> { [BabelModule.Hr] = EntitlementState.Entitled },
                Admin,
                "SUB-2026-0042"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsSuccess);

        IReadOnlyList<AuditEntry> entries = await auditLog.ReadAsync(Tenant, TestContext.Current.CancellationToken);
        AuditEntry entry = Assert.Single(entries);
        Assert.Equal("entitlement.changed", entry.Action);
        Assert.Equal(nameof(BabelModule.Hr), entry.Subject);
        Assert.Equal(Admin, entry.Actor);
        Assert.Equal(clock.GetUtcNow(), entry.OccurredAt);
        Assert.Contains("NotEntitled -> Entitled", entry.Details, StringComparison.Ordinal);
        Assert.Contains("SUB-2026-0042", entry.Details, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ApplyAsync_RejectsAnIncoherentSetWithoutPartialApplication()
    {
        FixedTimeProvider clock = new(new DateTimeOffset(2026, 8, 24, 9, 30, 0, TimeSpan.Zero));
        InMemoryAuditLog auditLog = new();
        InMemoryEntitlementService service = new(auditLog, clock);

        Result<EntitlementSet> result = await service.ApplyAsync(
            new EntitlementChangeRequest(
                Tenant,
                new Dictionary<BabelModule, EntitlementState> { [BabelModule.Pos] = EntitlementState.Entitled },
                Admin,
                "SUB-2026-0043"),
            TestContext.Current.CancellationToken);

        Assert.True(result.IsFailure);

        EntitlementSet stored = await service.GetAsync(Tenant, TestContext.Current.CancellationToken);
        Assert.Equal(EntitlementState.NotEntitled, stored.StateOf(BabelModule.Pos));
        Assert.Empty(await auditLog.ReadAsync(Tenant, TestContext.Current.CancellationToken));
    }
}
