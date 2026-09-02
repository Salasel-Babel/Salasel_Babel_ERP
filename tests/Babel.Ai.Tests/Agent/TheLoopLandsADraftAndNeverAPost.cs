using System.Text.Json;
using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.Contracts.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>الحلقة كاملةً على ناقلٍ مسجَّل: بحثٌ، فسؤالٌ، فمسوّدةٌ تهبط على شاشتها.</b>
/// <para>
/// <b>ولا نداء شبكةٍ واحد ولا ريالٌ واحد.</b> المزوّد خلف منفذ، والشريط مكتوبٌ في
/// الاختبار. ومجموعةُ اختباراتٍ تنفق على كل تشغيل تُطفأ خلال شهر، ثم يبقى الحارس
/// مكتوباً ولا يعمل.
/// </para>
/// </summary>
public sealed class TheLoopLandsADraftAndNeverAPost
{
    private static readonly TenantId Tenant = new(new Guid("100b0000-0000-4000-8000-000000000001"));
    private static readonly Guid Company = new("100b0000-0000-4000-8000-0000000000c1");
    private static readonly Guid Session = new("100b0000-0000-4000-8000-0000000000f1");
    private static readonly Guid CustomerRow = new("100b0000-0000-4000-8000-0000000000d1");
    private static readonly Guid BranchRow = new("100b0000-0000-4000-8000-0000000000b1");

    private static AgentTurnService Service(
        MovableClock clock,
        RecordedAgentGateway gateway,
        RecordingDraftSubmitter drafts,
        ScriptedQuestionSheets questions,
        params INameCandidateSource[] sources) =>
        new(gateway,
            AgentToolCatalogue.Embedded,
            AgentHarness.Options(),
            AgentHarness.Lookup(clock, sources),
            AgentHarness.Handles(clock),
            questions,
            drafts,
            new InMemoryAgentSpendLedger(clock),
            new ScriptedBilling(AgentTenantBilling.OwnerKey));

    private static AgentTurnRequest Turn() => new(
        AgentHarness.Caller(Tenant, Company, Session, "draftSalesInvoice"),
        "سجّل فاتورة مبيعات لشركة المسار الامثل بمبلغ 1500 ريال",
        "2026-03-01");

    private static object Invoice(string customer, string branch) => new
    {
        branchId = branch,
        customerId = customer,
        issuedOn = "2026-03-01",
        lines = new[] { new { description = new { ar = "بند", en = "line" }, discount = "0", itemGroup = "GOODS", quantity = "1", taxClassification = "standard", taxRate = "0.15", unitPrice = "1500" } },
        number = "INV-1",
    };

    /// <summary>
    /// <b>الطريق كاملاً:</b> يبحث فيَغمض، فيُسأل المستخدم، فيعود مِقبض، فتُنشأ مسوّدة
    /// وتهبط على شاشتها. <b>ولا ترحيل في السطر الأخير</b>.
    /// </summary>
    [Fact]
    public async Task غموضٌ_فسؤالٌ_فمسوّدةٌ_تهبط_على_شاشتها()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);

        string customer = handles
            .Issue(LookupHandlePurpose.Entity, Tenant, Company, Session, CustomerRow, TimeSpan.FromMinutes(10)).Value;
        string branch = handles
            .Issue(LookupHandlePurpose.Entity, Tenant, Company, Session, BranchRow, TimeSpan.FromMinutes(10)).Value;

        ScriptedCandidateSource customers = new("customer", static _ => NameCandidateProbe.Many);
        ScriptedQuestionSheets questions = new((_, _) => Result<string>.Success(customer));
        RecordingDraftSubmitter drafts = new();

        RecordedAgentGateway gateway = new(
            _ => AgentHarness.Script(
                AgentHarness.Call("tu_1", AgentProtocolTools.LookupEntity,
                    new { kind = "customer", text = "شركة المسار الامثل" }),
                AgentHarness.DoneWithTools()),

            // ‏**معرّف الورقة يُقرأ من نتيجة البحث كما أصدرها الخادم** — لا يُخترع في
            // الشريط. ومِقبضٌ مخترَع كان سيسقط عند التوقيع فيُثبت غير المقصود.
            request => AgentHarness.Script(
                AgentHarness.Call("tu_2", AgentProtocolTools.AskQuestion,
                    new { questionId = QuestionIdOf(RecordedAgentGateway.LastToolResult(request)!) }),
                AgentHarness.DoneWithTools(cacheRead: 900)),
            _ => AgentHarness.Script(
                AgentHarness.Call("tu_3", "draftSalesInvoice", Invoice(customer, branch)),
                AgentHarness.DoneWithTools(cacheRead: 900)),
            _ => AgentHarness.Script(
                AgentModelEvent.TextBlock("أنشأتُ مسوّدة الفاتورة. راجعها على شاشتها ثم رحّلها بنفسك."),
                AgentHarness.Done(cacheRead: 900)));

        AgentTurnService service = Service(clock, gateway, drafts, questions, customers);
        List<AgentTurnEvent> events = await AgentHarness.RunAsync(service, Turn());

        Assert.Single(questions.Asked);

        Assert.Contains(events, static e => e.Kind == AgentTurnEventKind.QuestionRaised);
        Assert.Contains(events, static e => e.Kind == AgentTurnEventKind.DraftLanded);
        Assert.DoesNotContain(events, static e => e.Kind == AgentTurnEventKind.Refused);

        AgentDispatch landed = Assert.Single(drafts.Submitted);
        Assert.Equal("draftSalesInvoice", landed.Tool.Name);
        Assert.Equal("/api/v1/companies/{companyId}/sales-invoices", landed.Tool.Path);
        Assert.Contains(landed.Redeemed, field => field.Subject == CustomerRow);

        // ‏**وما يعود إلى النموذج «مسوّدة» وحدها** — لا معرّف ولا مسار ولا اسم.
        AgentModelRequest last = gateway.Requests[^1];
        string[] results = [.. last.Blocks
            .Where(static block => block.Kind == AgentWireBlockKind.ToolResult)
            .Select(last.TextOf)];

        Assert.Contains("{\"state\":\"draft\"}", results);
        Assert.DoesNotContain(results, text => text.Contains(CustomerRow.ToString(), StringComparison.Ordinal));
        Assert.DoesNotContain(results, text => text.Contains("المسار", StringComparison.Ordinal));
    }

    /// <summary>
    /// <b>جوابُ الغموض لا يقول كم كانوا ولا مَن كانوا</b> — ثلاثة مفاتيح لا رابع لها،
    /// ومجموعتها واحدة في الحالات الثلاث.
    /// </summary>
    [Fact]
    public async Task جوابُ_الغموض_لا_يحمل_عدداً_ولا_اسماً()
    {
        MovableClock clock = new();
        ScriptedCandidateSource customers = new("customer", static _ => NameCandidateProbe.Many);
        ScriptedQuestionSheets questions = new((_, _) => Result<string>.Failure(
            new Error("test.no_answer", "لم يُجب", "no answer")));

        RecordedAgentGateway gateway = RecordedAgentGateway.Fixed(
            [
                AgentHarness.Call("tu_1", AgentProtocolTools.LookupEntity,
                    new { kind = "customer", text = "محمد القحطاني" }),
                AgentHarness.DoneWithTools(),
            ],
            [AgentModelEvent.TextBlock("سأنتظر اختيارك."), AgentHarness.Done()]);

        AgentTurnService service = Service(clock, gateway, new RecordingDraftSubmitter(), questions, customers);
        await AgentHarness.RunAsync(service, Turn());

        AgentModelRequest second = gateway.Requests[1];
        string result = second.Blocks
            .Where(static block => block.Kind == AgentWireBlockKind.ToolResult)
            .Select(second.TextOf)
            .Single();

        using JsonDocument parsed = JsonDocument.Parse(result);
        string[] keys = [.. parsed.RootElement.EnumerateObject().Select(static property => property.Name)];
        Assert.Equal<string>(["outcome", "handle", "questionId"], keys);

        Assert.Equal("needs_question", parsed.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(JsonValueKind.Null, parsed.RootElement.GetProperty("handle").ValueKind);

        // ولا اسم مرشّحٍ واحد في أي جزءٍ يعبر إلى النموذج.
        foreach (Babel.Ai.Boundary.AgentOutboundPart part in second.Envelope.Parts)
        {
            Assert.DoesNotContain("القحطان", part.Text.Replace("محمد القحطاني", string.Empty, StringComparison.Ordinal), StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// <b>نموذجٌ ينادي عمليةَ ترحيلٍ يُرفض قبل التنفيذ</b>، ويعود إليه الرفض
    /// <c>tool_result</c> بنصّه العربي فيُصحّح — لا استثناءً يقتل الدور.
    /// </summary>
    [Fact]
    public async Task نداءُ_ترحيلٍ_يُرفض_ويعود_رفضاً_يُقرأ()
    {
        MovableClock clock = new();
        RecordingDraftSubmitter drafts = new();

        RecordedAgentGateway gateway = RecordedAgentGateway.Fixed(
            [
                AgentHarness.Call("tu_1", "postSalesInvoice", new { invoiceId = "x" }),
                AgentHarness.DoneWithTools(),
            ],
            [AgentModelEvent.TextBlock("لا أبلغ الترحيل."), AgentHarness.Done()]);

        AgentTurnService service = Service(clock, gateway, drafts,
            new ScriptedQuestionSheets((_, _) => Result<string>.Success("x")));

        List<AgentTurnEvent> events = await AgentHarness.RunAsync(service, Turn());

        AgentTurnEvent refused = Assert.Single(events, static e => e.Kind == AgentTurnEventKind.ToolRefused);
        Assert.Equal("ai.agent.tool_unknown", Assert.Single(refused.Errors).Code);
        Assert.Empty(drafts.Submitted);

        AgentModelRequest second = gateway.Requests[1];
        AgentWireBlock result = Assert.Single(
            second.Blocks, static block => block.Kind == AgentWireBlockKind.ToolResult);

        Assert.True(result.IsError);
        Assert.Contains("الكتالوج مغلق", second.TextOf(result), StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ودورٌ فيه ما شكلُه معرّف لا يُرسَل أصلاً</b> — لا نداء واحد إلى المزوّد،
    /// ورسالةٌ تسمّي الشكل باسمه.
    /// </summary>
    [Fact]
    public async Task دورٌ_فيه_رقمُ_هويةٍ_لا_يبلغ_المزوّد()
    {
        MovableClock clock = new();
        RecordedAgentGateway gateway = new();

        AgentTurnService service = Service(clock, gateway, new RecordingDraftSubmitter(),
            new ScriptedQuestionSheets((_, _) => Result<string>.Success("x")));

        List<AgentTurnEvent> events = await AgentHarness.RunAsync(service, new AgentTurnRequest(
            AgentHarness.Caller(Tenant, Company, Session, "draftSalesInvoice"),
            "سجّل سلفة للموظف رقم هويته 1012345678",
            "2026-03-01"));

        Assert.Equal(0, gateway.Calls);

        AgentTurnEvent refused = Assert.Single(events);
        Assert.Equal(AgentTurnEventKind.Refused, refused.Kind);
        Assert.Contains(refused.Errors, static e => e.Code == "ai.agent.turn_refused_at_boundary");
        Assert.Contains(refused.Errors, static e => e.Code == "ai.agent.identifier_refused.national_id");
    }

    /// <summary>ودورةٌ لا تنتهي تُوقَف بسقفٍ معلَن لا بانتظارٍ صامت.</summary>
    [Fact]
    public async Task دورةٌ_لا_تنتهي_تُوقَف_بسقفٍ_معلَن()
    {
        MovableClock clock = new();
        ScriptedCandidateSource customers = new("customer", static _ => NameCandidateProbe.None);

        List<IReadOnlyList<AgentModelEvent>> script = [];
        for (int index = 0; index < 12; index++)
        {
            script.Add([
                AgentHarness.Call("tu_" + index, AgentProtocolTools.LookupEntity,
                    new { kind = "customer", text = "اسمٌ رقم " + index }),
                AgentHarness.DoneWithTools(),
            ]);
        }

        RecordedAgentGateway gateway = RecordedAgentGateway.Fixed([.. script]);

        AgentTurnService service = new(
            gateway,
            AgentToolCatalogue.Embedded,
            AgentHarness.Options(options => options.MaxToolIterations = 3),
            AgentHarness.Lookup(clock, customers),
            AgentHarness.Handles(clock),
            new ScriptedQuestionSheets((_, _) => Result<string>.Success("x")),
            new RecordingDraftSubmitter(),
            new InMemoryAgentSpendLedger(clock),
            new ScriptedBilling(AgentTenantBilling.OwnerKey));

        List<AgentTurnEvent> events = await AgentHarness.RunAsync(service, Turn());

        Assert.Equal(3, gateway.Calls);
        Assert.Equal("ai.agent.tool_iterations_exhausted", Assert.Single(events[^1].Errors).Code);
    }

    /// <summary>يقرأ معرّف الورقة من جواب البحث كما كتبه الخادم.</summary>
    private static string QuestionIdOf(string toolResult)
    {
        using JsonDocument parsed = JsonDocument.Parse(toolResult);
        return parsed.RootElement.GetProperty("questionId").GetString()!;
    }
}
