using System.Text;
using System.Text.RegularExpressions;
using Babel.Ai.Agent;
using Babel.Contracts.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>بادئةُ الطلب واحدةٌ بايتاً ببايت — بين نداءٍ ونداء، وبين مستخدمٍ ومستخدم.</b>
/// <para>
/// ترتيب العرض <c>tools ← system ← messages</c>، والذاكرة مطابقةُ <b>بادئة</b>: أيّ
/// بايتٍ يتغيّر في المقدّمة يُبطل كل ما بعده. والقاتلان الصامتان المعروفان اثنان —
/// <c>tools = build(user)</c>، و<b>تاريخُ اليوم في نصّ النظام</b>. وهذا الملفّ يقيس
/// غيابهما على الطلب الذي وصل الناقل فعلاً، لا على النيّة.
/// </para>
/// <para>
/// <b>و<c>usage.cache_read_input_tokens</c> ليس زينةً بل الفحص:</b> صفرٌ عبر نداءات
/// متكرّرة يعني مُبطِلاً صامتاً لا ذاكرةً باردة. فالقياس يُسجَّل في
/// <see cref="AgentTurnMetrics"/> ويُقرأ.
/// </para>
/// </summary>
public sealed class ThePrefixIsByteStableAndTheCacheIsRead
{
    private static readonly TenantId Tenant = new(new Guid("caac0000-0000-4000-8000-000000000001"));
    private static readonly Guid Company = new("caac0000-0000-4000-8000-0000000000c1");
    private static readonly Guid Session = new("caac0000-0000-4000-8000-0000000000f1");

    private static AgentTurnService Service(MovableClock clock, RecordedAgentGateway gateway) =>
        new(gateway,
            AgentToolCatalogue.Embedded,
            AgentHarness.Options(),
            AgentHarness.Lookup(clock, new ScriptedCandidateSource("customer", static _ => NameCandidateProbe.None)),
            AgentHarness.Handles(clock),
            new ScriptedQuestionSheets((_, _) => Result<string>.Success("x")),
            new RecordingDraftSubmitter(),
            new InMemoryAgentSpendLedger(clock),
            new ScriptedBilling(AgentTenantBilling.OwnerKey));

    /// <summary>
    /// نصّ النظام <b>ثابتٌ في التجميعة</b> ولا يحمل تاريخاً ولا اسم شركةٍ ولا معرّفاً
    /// ولا عدداً — وهذه هي الحقول التي يُحقنها الناس فيه فتُبطل الذاكرة كل يوم.
    /// </summary>
    [Fact]
    public void نصّ_النظام_لا_يحمل_تاريخاً_ولا_اسماً_ولا_عدداً()
    {
        string text = AgentSystemPrompt.Text;

        Assert.DoesNotMatch(new Regex(@"\d{4}-\d{2}-\d{2}", RegexOptions.CultureInvariant), text);
        Assert.DoesNotContain("{", text, StringComparison.Ordinal);
        Assert.DoesNotContain(Tenant.Value.ToString(), text, StringComparison.Ordinal);

        // ‏و**الحدّ الأدنى للبادئة القابلة للذاكرة** لا يُبلغ بنصٍّ من سطرين.
        Assert.True(text.Length > 800, "طول نصّ النظام: " + text.Length);
    }

    /// <summary>
    /// <b>الكتالوج لا يتأثّر بالمتصل.</b> متكلّمان باستحقاقين مختلفين تماماً يُنتجان
    /// أدواتٍ متطابقة بايتاً ببايت — والتصفية تقع في البوّابة بعد أن ينطق النموذج.
    /// </summary>
    [Fact]
    public async Task متكلّمان_باستحقاقين_مختلفين_يُنتجان_أدواتٍ_متطابقة()
    {
        MovableClock clock = new();

        RecordedAgentGateway wide = RecordedAgentGateway.Fixed([AgentModelEvent.TextBlock("تمّ"), AgentHarness.Done()]);
        RecordedAgentGateway narrow = RecordedAgentGateway.Fixed([AgentModelEvent.TextBlock("تمّ"), AgentHarness.Done()]);

        await AgentHarness.RunAsync(Service(clock, wide), new AgentTurnRequest(
            AgentHarness.Caller(Tenant, Company, Session,
                "draftSalesInvoice", "draftCreditNote", "draftPayrollRun", "draftLeaseContract"),
            "اعرض ما تستطيع", "2026-03-01"));

        await AgentHarness.RunAsync(Service(clock, narrow), new AgentTurnRequest(
            AgentHarness.Caller(Tenant, Company, Session, "draftStockMovement"),
            "اعرض ما تستطيع", "2026-03-01"));

        Assert.Equal(Rendered(wide.Requests[0]), Rendered(narrow.Requests[0]));

        // شاهدٌ موجب: البادئة ليست فارغة.
        Assert.True(Rendered(wide.Requests[0]).Length > 50_000, "طول البادئة المعروضة.");
    }

    /// <summary>
    /// <b>والبادئة نفسها بين نداءٍ ونداء داخل الدور الواحد</b> — فما يتقلّب يقع بعدها
    /// في الرسائل لا فيها.
    /// </summary>
    [Fact]
    public async Task البادئة_نفسها_في_كل_نداءات_الدور()
    {
        MovableClock clock = new();

        RecordedAgentGateway gateway = new(
            _ => AgentHarness.Script(
                AgentHarness.Call("tu_1", AgentProtocolTools.LookupEntity,
                    new { kind = "customer", text = "شركة المسار الامثل" }),
                AgentHarness.DoneWithTools(input: 12_000, cacheRead: 0)),
            _ => AgentHarness.Script(
                AgentModelEvent.ThinkingBlock("راجعتُ السجلّ.", "sig-1"),
                AgentModelEvent.TextBlock("لا اسم مطابق."),
                AgentHarness.Done(input: 300, cacheRead: 11_800)));

        AgentTurnService service = Service(clock, gateway);
        List<AgentTurnEvent> events = await AgentHarness.RunAsync(service, new AgentTurnRequest(
            AgentHarness.Caller(Tenant, Company, Session, "draftSalesInvoice"),
            "من هي شركة المسار الامثل؟", "2026-03-01"));

        Assert.Equal(2, gateway.Calls);
        Assert.Equal(Rendered(gateway.Requests[0]), Rendered(gateway.Requests[1]));

        // ‏**والذاكرة قُرئت فعلاً في النداء الثاني** — والصفر هنا كان سيعني مُبطِلاً صامتاً.
        AgentTurnMetrics metrics = Assert.Single(events, static e => e.Kind == AgentTurnEventKind.Completed).Metrics!;
        Assert.True(metrics.CacheWasReadAfterTheFirstCall);
        Assert.Equal(11_800, metrics.Total.CacheReadInputTokens);
        Assert.Equal(2, metrics.ModelCalls);
    }

    /// <summary>
    /// <b>وشاهدٌ سالب على القياس نفسه:</b> ناقلٌ يُعيد صفراً في كل نداء يجعل
    /// <c>CacheWasReadAfterTheFirstCall</c> كاذباً. فالفحص يميّز فعلاً، ولا يقول «نعم» دائماً.
    /// </summary>
    [Fact]
    public async Task صفرٌ_في_كل_نداء_يُقرأ_مُبطِلاً_لا_ذاكرةً_باردة()
    {
        MovableClock clock = new();

        RecordedAgentGateway gateway = new(
            _ => AgentHarness.Script(
                AgentHarness.Call("tu_1", AgentProtocolTools.LookupEntity,
                    new { kind = "customer", text = "شركة المسار الامثل" }),
                AgentHarness.DoneWithTools(cacheRead: 0)),
            _ => AgentHarness.Script(
                AgentModelEvent.TextBlock("لا اسم مطابق."),
                AgentHarness.Done(cacheRead: 0)));

        List<AgentTurnEvent> events = await AgentHarness.RunAsync(
            Service(clock, gateway),
            new AgentTurnRequest(
                AgentHarness.Caller(Tenant, Company, Session, "draftSalesInvoice"),
                "من هي شركة المسار الامثل؟", "2026-03-01"));

        AgentTurnMetrics metrics = Assert.Single(events, static e => e.Kind == AgentTurnEventKind.Completed).Metrics!;
        Assert.False(metrics.CacheWasReadAfterTheFirstCall);
    }

    /// <summary>
    /// <b>وتاريخُ اليوم واسمُ الشركة يقعان في الرسائل لا في نصّ النظام</b> — وهو موضعهما
    /// الصحيح: رسالةُ نظامٍ في وسط المحادثة تتلو دور المستخدم ولا تسبقه.
    /// </summary>
    [Fact]
    public async Task التاريخُ_واسمُ_الشركة_في_الرسائل_بعد_دور_المستخدم()
    {
        MovableClock clock = new();
        RecordedAgentGateway gateway = RecordedAgentGateway.Fixed([AgentModelEvent.TextBlock("تمّ"), AgentHarness.Done()]);

        await AgentHarness.RunAsync(Service(clock, gateway), new AgentTurnRequest(
            AgentHarness.Caller(Tenant, Company, Session, "draftSalesInvoice"),
            "سجّل فاتورة", "2026-03-01"));

        AgentModelRequest request = gateway.Requests[0];

        Assert.DoesNotContain("2026-03-01", request.SystemPrompt, StringComparison.Ordinal);
        Assert.DoesNotContain("شركة سلاسل بابل", request.SystemPrompt, StringComparison.Ordinal);

        AgentWireBlock system = Assert.Single(
            request.Blocks, static block => block.Role == AgentWireRole.System);

        Assert.Contains("2026-03-01", request.TextOf(system), StringComparison.Ordinal);
        Assert.Contains("شركة سلاسل بابل", request.TextOf(system), StringComparison.Ordinal);

        // ‏**قيدُ بروتوكول لا ذوق:** رسالة النظام في وسط المحادثة تتلو دور مستخدم،
        // ولا تكون أوّل الرسائل.
        int index = request.Blocks.ToList().IndexOf(system);
        Assert.True(index > 0);
        Assert.Equal(AgentWireRole.User, request.Blocks[index - 1].Role);
    }

    /// <summary>
    /// <b>وكل نصٍّ في الطلب مصدرُه الظرف المختوم</b> — لا نسخة ثانية تنحرف. عددُ الكتل
    /// يساوي عدد الأجزاء، وكلٌّ تشير إلى موضعها.
    /// </summary>
    [Fact]
    public async Task كل_نصٍّ_في_الطلب_مصدرُه_الظرف()
    {
        MovableClock clock = new();
        RecordedAgentGateway gateway = RecordedAgentGateway.Fixed([AgentModelEvent.TextBlock("تمّ"), AgentHarness.Done()]);

        await AgentHarness.RunAsync(Service(clock, gateway), new AgentTurnRequest(
            AgentHarness.Caller(Tenant, Company, Session, "draftSalesInvoice"),
            "سجّل فاتورة", "2026-03-01"));

        AgentModelRequest request = gateway.Requests[0];

        Assert.Equal(request.Envelope.Parts.Count, request.Blocks.Count);

        for (int index = 0; index < request.Blocks.Count; index++)
        {
            Assert.Equal(index, request.Blocks[index].PartIndex);
        }

        Assert.All(request.Blocks, block => Assert.Same(
            request.Envelope.Parts[block.PartIndex].Text, request.TextOf(block)));
    }

    /// <summary>البادئة المعروضة: الأدوات بترتيبها ثم نصّ النظام — وهي ما يُذاكَر.</summary>
    private static string Rendered(AgentModelRequest request)
    {
        StringBuilder prefix = new();

        foreach (AgentTool tool in request.Catalogue.Tools)
        {
            prefix.Append(tool.Name).Append('\n')
                  .Append(tool.Description).Append('\n')
                  .Append(tool.InputSchemaJson).Append('\n');
        }

        prefix.Append(request.SystemPrompt);
        return prefix.ToString();
    }
}
