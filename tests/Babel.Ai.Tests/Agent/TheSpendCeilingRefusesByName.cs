using Babel.Ai.Agent;
using Babel.Contracts.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>الإنفاق يُقاس لكل منشأة على حدة، ويُرفض عند السقف بجملةٍ مسمّاة قبل النداء.</b>
/// <para>
/// <b>و«قبل النداء» هي كل الفائدة:</b> سقفٌ يُفحص بعد الجواب يكون قد أنفق ما جاء
/// يمنعه. فالفحص أوّل ما يجري في الدور، وقبل أن يُختَم ظرف.
/// </para>
/// <para>
/// <b>والوحدة رموزٌ لا ريالات، وذلك قرارٌ لا كسل:</b> الرمز واقعةٌ يُعيدها المزوّد؛
/// والريال يحتاج جدول أسعارٍ ليس في هذا المستودع، وسعرٌ يُكتب في الشيفرة يتجمّد بينما
/// يتحرّك عند المزوّد. و<c>ai.agent.price_list_missing</c> تقول ذلك صراحةً.
/// </para>
/// </summary>
public sealed class TheSpendCeilingRefusesByName
{
    private static readonly TenantId Owned = new(new Guid("5be40000-0000-4000-8000-000000000001"));
    private static readonly TenantId BringsItsOwn = new(new Guid("5be40000-0000-4000-8000-000000000002"));
    private static readonly Guid Company = new("5be40000-0000-4000-8000-0000000000c1");
    private static readonly Guid Session = new("5be40000-0000-4000-8000-0000000000f1");

    private static AgentTurnService Service(
        MovableClock clock,
        RecordedAgentGateway gateway,
        IAgentSpendLedger ledger,
        AgentTenantBilling billing,
        Action<AgentOptions>? configure = null) =>
        new(gateway,
            AgentToolCatalogue.Embedded,
            AgentHarness.Options(configure),
            AgentHarness.Lookup(clock, new ScriptedCandidateSource("customer", static _ => NameCandidateProbe.None)),
            AgentHarness.Handles(clock),
            new ScriptedQuestionSheets((_, _) => Result<string>.Success("x")),
            new RecordingDraftSubmitter(),
            ledger,
            new ScriptedBilling(billing));

    private static AgentTurnRequest Turn(TenantId tenant) => new(
        AgentHarness.Caller(tenant, Company, Session, "draftSalesInvoice"),
        "سجّل فاتورة",
        "2026-03-01");

    /// <summary>
    /// منشأةٌ بلغت سقفها تُرفض بالرمز المسمّى، <b>ولا يُنادى المزوّد ولا مرّة</b>.
    /// </summary>
    [Fact]
    public async Task منشأةٌ_بلغت_سقفها_تُرفض_ولا_يُنادى_المزوّد()
    {
        MovableClock clock = new();
        InMemoryAgentSpendLedger ledger = new(clock);
        RecordedAgentGateway gateway = new();

        await ledger.RecordAsync(Owned, new AgentModelUsage(600, 100, 0, 0), TimeSpan.FromDays(1), TestContext.Current.CancellationToken);

        List<AgentTurnEvent> events = await AgentHarness.RunAsync(
            Service(clock, gateway, ledger, AgentTenantBilling.OwnerKey,
                options => options.DefaultTenantTokenCeiling = 500),
            Turn(Owned));

        Assert.Equal(0, gateway.Calls);

        AgentTurnEvent refused = Assert.Single(events);
        Assert.Equal(AgentTurnEventKind.Refused, refused.Kind);
        Assert.Equal("ai.agent.spend_ceiling_reached", Assert.Single(refused.Errors).Code);
        Assert.Contains("سقفَ إنفاقها", refused.Errors[0].MessageAr, StringComparison.Ordinal);
    }

    /// <summary>ومنشأةٌ دون السقف تمرّ، ويُسجَّل ما استهلكته فعلاً.</summary>
    [Fact]
    public async Task منشأةٌ_دون_السقف_تمرّ_ويُسجَّل_استهلاكها()
    {
        MovableClock clock = new();
        InMemoryAgentSpendLedger ledger = new(clock);
        RecordedAgentGateway gateway = RecordedAgentGateway.Fixed([AgentModelEvent.TextBlock("تمّ"), AgentHarness.Done(input: 700, output: 40)]);

        await AgentHarness.RunAsync(
            Service(clock, gateway, ledger, AgentTenantBilling.OwnerKey,
                options => options.DefaultTenantTokenCeiling = 5_000),
            Turn(Owned));

        Assert.Equal(1, gateway.Calls);

        AgentTenantSpend spend = await ledger.ReadAsync(Owned, TimeSpan.FromDays(1), TestContext.Current.CancellationToken);
        Assert.Equal(740, spend.Usage.Billable);
        Assert.Equal(1, spend.Turns);
    }

    /// <summary>
    /// <b>ومنشأةٌ جاءت بمفتاحها لا يُسقَف إنفاقُها بسقف المالك</b> — لأنها تدفعه.
    /// ويبقى إنفاقُها <b>مقيساً</b>: الإعفاء من السقف ليس إعفاءً من القياس.
    /// </summary>
    [Fact]
    public async Task منشأةٌ_بمفتاحها_لا_تُسقَف_بسقف_المالك_ويبقى_إنفاقُها_مقيساً()
    {
        MovableClock clock = new();
        InMemoryAgentSpendLedger ledger = new(clock);
        RecordedAgentGateway gateway = RecordedAgentGateway.Fixed([AgentModelEvent.TextBlock("تمّ"), AgentHarness.Done(input: 900, output: 10)]);

        await ledger.RecordAsync(BringsItsOwn, new AgentModelUsage(9_000, 0, 0, 0), TimeSpan.FromDays(1), TestContext.Current.CancellationToken);

        AgentTenantBilling billing = new("BABEL_AGENT_KEY_TENANT_B", null);

        List<AgentTurnEvent> events = await AgentHarness.RunAsync(
            Service(clock, gateway, ledger, billing, options => options.DefaultTenantTokenCeiling = 500),
            Turn(BringsItsOwn));

        Assert.Equal(1, gateway.Calls);
        Assert.DoesNotContain(events, static e => e.Kind == AgentTurnEventKind.Refused);

        // ‏**واسم متغيّرها هو ما يصل الناقل** — لا مفتاحها.
        Assert.Equal("BABEL_AGENT_KEY_TENANT_B", gateway.Requests[0].ApiKeyVariable);

        AgentTenantSpend spend = await ledger.ReadAsync(BringsItsOwn, TimeSpan.FromDays(1), TestContext.Current.CancellationToken);
        Assert.Equal(9_910, spend.Usage.Billable);
    }

    /// <summary>ومنشأةٌ بمفتاحها وسقفٍ صرّحت به تُسقَف بسقفها هي.</summary>
    [Fact]
    public async Task منشأةٌ_بمفتاحها_وسقفٍ_خاصّ_تُسقَف_بسقفها()
    {
        MovableClock clock = new();
        InMemoryAgentSpendLedger ledger = new(clock);
        RecordedAgentGateway gateway = new();

        await ledger.RecordAsync(BringsItsOwn, new AgentModelUsage(200, 0, 0, 0), TimeSpan.FromDays(1), TestContext.Current.CancellationToken);

        List<AgentTurnEvent> events = await AgentHarness.RunAsync(
            Service(clock, gateway, ledger, new AgentTenantBilling("BABEL_AGENT_KEY_TENANT_B", 100)),
            Turn(BringsItsOwn));

        Assert.Equal(0, gateway.Calls);
        Assert.Equal("ai.agent.spend_ceiling_reached", Assert.Single(Assert.Single(events).Errors).Code);
    }

    /// <summary>والنافذة تُطوى: ما انقضى لا يُجمَّع بلا حدّ.</summary>
    [Fact]
    public async Task النافذةُ_تُطوى_ولا_يُجمَّع_ما_انقضى()
    {
        MovableClock clock = new();
        InMemoryAgentSpendLedger ledger = new(clock);

        await ledger.RecordAsync(Owned, new AgentModelUsage(9_000, 0, 0, 0), TimeSpan.FromDays(1), TestContext.Current.CancellationToken);
        Assert.True((await ledger.AdmitAsync(Owned, 500, TimeSpan.FromDays(1), TestContext.Current.CancellationToken)).IsFailure);

        clock.Advance(TimeSpan.FromDays(1) + TimeSpan.FromMinutes(1));

        Assert.True((await ledger.AdmitAsync(Owned, 500, TimeSpan.FromDays(1), TestContext.Current.CancellationToken)).IsSuccess);
        Assert.Equal(0, (await ledger.ReadAsync(Owned, TimeSpan.FromDays(1), TestContext.Current.CancellationToken)).Usage.Billable);
    }

    /// <summary>
    /// <b>والمنشأتان لا تتقاسمان دفتراً:</b> إنفاق إحداهما لا يُغلق باب الأخرى.
    /// </summary>
    [Fact]
    public async Task إنفاقُ_منشأةٍ_لا_يُغلق_باب_الأخرى()
    {
        MovableClock clock = new();
        InMemoryAgentSpendLedger ledger = new(clock);

        await ledger.RecordAsync(Owned, new AgentModelUsage(9_000, 0, 0, 0), TimeSpan.FromDays(1), TestContext.Current.CancellationToken);

        Assert.True((await ledger.AdmitAsync(Owned, 500, TimeSpan.FromDays(1), TestContext.Current.CancellationToken)).IsFailure);
        Assert.True((await ledger.AdmitAsync(BringsItsOwn, 500, TimeSpan.FromDays(1), TestContext.Current.CancellationToken)).IsSuccess);
    }

    /// <summary>ولا سعرٌ يُخمَّن: طلبُ مبلغٍ بلا جدول أسعارٍ يُرفض بجملةٍ تقول ما ينقص.</summary>
    [Fact]
    public void لا_سعرٌ_يُخمَّن_للرمز()
    {
        Error refusal = AgentErrors.PriceListMissing;

        Assert.Equal("ai.agent.price_list_missing", refusal.Code);
        Assert.Contains("لا يُخمَّن سعر رمز", refusal.MessageAr, StringComparison.Ordinal);
    }
}
