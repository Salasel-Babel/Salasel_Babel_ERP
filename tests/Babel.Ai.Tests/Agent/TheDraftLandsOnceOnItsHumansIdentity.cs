using System.Text.Json;
using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.Ai.Workspace;
using Babel.Core.Audit;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>سطحٌ منشورٌ مُحاكى: يسجّل كل نداءٍ وصله، ويجيب بما يُملى عليه.</summary>
internal sealed class ScriptedPublishedSurface : IAgentPublishedSurface
{
    private readonly Func<AgentSurfaceCall, Result<AgentSurfaceAnswer>> _answer;

    public ScriptedPublishedSurface(Func<AgentSurfaceCall, Result<AgentSurfaceAnswer>> answer) => _answer = answer;

    /// <summary>سطحٌ يقبل كل نداءٍ ويردّ <c>201</c> بجسمٍ فيه معرّف — كما يفعل الباب.</summary>
    public static ScriptedPublishedSurface Accepting() => new(static _ =>
        Result<AgentSurfaceAnswer>.Success(new AgentSurfaceAnswer(
            201, "{\"id\":\"8f7c1c2e-0000-4000-8000-00000000abcd\",\"state\":\"draft\"}")));

    /// <summary>النداءات كما وصلت، بترتيبها.</summary>
    public List<AgentSurfaceCall> Calls { get; } = [];

    public Task<Result<AgentSurfaceAnswer>> CallAsync(AgentSurfaceCall call, CancellationToken cancellationToken)
    {
        Calls.Add(call);
        return Task.FromResult(_answer(call));
    }
}

/// <summary>
/// <b>المسوّدة تهبط — مرّةً واحدة، وعلى هويّة إنسانها، وسقوطُها يُسمّى.</b>
/// <para>
/// وهذا الملفّ يقيس أربعةً لا يُقاس شيءٌ منها بقراءة شيفرة:
/// <list type="number">
///   <item><b>الهويّة</b>: النداء يحمل مستخدم الجلسة، وسجلّ التدقيق يقيّد أنّها نشأت
///         باقتراح وكيل — <b>فلا يُخفي الانتسابُ إلى الإنسان من اقترح</b>.</item>
///   <item><b>السقوط</b>: رفضُ الخادم يعود سبباً مُسمّى برمزه ونصّه العربي، لا «تعذّر».</item>
///   <item><b>الازدواج</b>: تأكيدٌ ثانٍ على الشكل نفسه <b>لا ينادي الباب مرّةً ثانية</b>
///         ويُعيد المسوّدة نفسها.</item>
///   <item><b>الحدّ</b>: لا شيء في هذا المسار يبلغ ترحيلاً، ولو أُمر به.</item>
/// </list>
/// </para>
/// </summary>
public sealed class TheDraftLandsOnceOnItsHumansIdentity
{
    private static readonly TenantId Tenant = new(new Guid("100d0000-0000-4000-8000-000000000001"));
    private static readonly Guid Company = new("100d0000-0000-4000-8000-0000000000c1");
    private static readonly Guid CustomerRow = new("100d0000-0000-4000-8000-0000000000d1");
    private static readonly Guid BranchRow = new("100d0000-0000-4000-8000-0000000000b1");

    private static readonly AgentToolCatalogue Catalogue = AgentToolCatalogue.Embedded;

    /// <summary>يفتح جلسةً حقيقية في مخزنٍ حقيقي — والمِقبض يُسكّ بجلستها هي.</summary>
    private static (InMemoryAgentWorkspaceStore Store, AgentWorkspaceSession Session) Workspace(MovableClock clock)
    {
        AgentWorkspaceOptions options = new() { HumanWait = TimeSpan.FromSeconds(5) };
        InMemoryAgentWorkspaceStore store = new(clock, options);

        AgentWorkspaceSession session = store.Open(
            Tenant, Company, AgentHarness.Human, "شركة سلاسل بابل");

        return (store, session);
    }

    private static AgentWorkspaceOptions Options() => new() { HumanWait = TimeSpan.FromSeconds(5) };

    private static string Body(SignedLookupHandles handles, Guid session, string number) =>
        JsonSerializer.Serialize(new
        {
            branchId = handles.Issue(
                LookupHandlePurpose.Entity, Tenant, Company, session, BranchRow, TimeSpan.FromMinutes(10)).Value,
            customerId = handles.Issue(
                LookupHandlePurpose.Entity, Tenant, Company, session, CustomerRow, TimeSpan.FromMinutes(10)).Value,
            issuedOn = "2026-03-01",
            lines = new[]
            {
                new
                {
                    description = new { ar = "بند", en = "line" },
                    discount = "0",
                    itemGroup = "GOODS",
                    quantity = "1",
                    taxClassification = "standard",
                    taxRate = "0.15",
                    unitPrice = "1500",
                },
            },
            number,
        });

    private static AgentDispatch Dispatch(SignedLookupHandles handles, Guid session, string number)
    {
        Result<AgentDispatch> gated = AgentToolGate.Authorise(
            new AgentToolCall("tu_1", "draftSalesInvoice", Body(handles, session, number)),
            AgentHarness.Caller(Tenant, Company, session, "draftSalesInvoice"),
            new AgentTurnState(4),
            Catalogue,
            handles);

        Assert.True(gated.IsSuccess, string.Join(" · ", gated.Errors.Select(static e => e.ToString())));
        return gated.Value;
    }

    /// <summary>يقبل التأكيد حين تُعرض البطاقة — كما يفعل إنسانٌ على اللوح.</summary>
    private static async Task<Result<AgentDraftLanding>> ConfirmedAsync(
        AgentDraftConfirmationGate gate,
        AgentWorkspaceSession session,
        AgentDispatch dispatch)
    {
        Task<Result<AgentDraftLanding>> landing = gate.SubmitAsync(dispatch, TestContext.Current.CancellationToken);

        while (session.PendingConfirmation is null && !landing.IsCompleted)
        {
            await Task.Yield();
        }

        if (session.PendingConfirmation is { } card)
        {
            Assert.True(AgentWorkspaceService.Confirm(session, card.StepId, accepted: true).IsSuccess);
        }

        return await landing;
    }

    private static AgentDraftConfirmationGate Gate(
        IAgentDraftSubmitter destination,
        IAgentWorkspaceStore store,
        MovableClock clock) =>
        new(destination, store, Options(), clock);

    // ═══════════════════════════════════════════════ ١ · الهويّة والأثر

    /// <summary>
    /// <b>المسوّدة تُنشأ على هويّة إنسان الجلسة</b> — لا على فاعلٍ اصطناعي — ويبقى في
    /// سجلّ التدقيق أنّها <b>نشأت باقتراح وكيل</b>.
    /// </summary>
    [Fact]
    public async Task المسوّدةُ_تُنسب_إلى_إنسان_الجلسة_ويبقى_أثرُ_أنها_باقتراح_وكيل()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);
        (InMemoryAgentWorkspaceStore store, AgentWorkspaceSession session) = Workspace(clock);

        ScriptedPublishedSurface surface = ScriptedPublishedSurface.Accepting();
        InMemoryAuditLog audit = new();

        AgentDraftConfirmationGate gate = Gate(
            new PublishedSurfaceAgentDraftSubmitter(surface, audit, clock), store, clock);

        Result<AgentDraftLanding> landed = await ConfirmedAsync(
            gate, session, Dispatch(handles, session.SessionId, "INV-1"));

        Assert.True(landed.IsSuccess, string.Join(" · ", landed.Errors.Select(static e => e.ToString())));

        // ‏**الباب المنشور نفسه**: فعلُه ومسارُه من العقد، ومساره مملوءٌ بنطاق الشركة.
        AgentSurfaceCall call = Assert.Single(surface.Calls);
        Assert.Equal("draftSalesInvoice", call.OperationId);
        Assert.Equal("post", call.Method);
        Assert.Equal("/api/v1/companies/{companyId}/sales-invoices", call.Template);
        Assert.Equal("/api/v1/companies/" + Company.ToString("D") + "/sales-invoices", call.Path);

        // ‏**والفاعل إنسان**: مستخدم الجلسة، لا فاعل نظامٍ ولا معرّفٌ صفريّ.
        Assert.Equal(AgentHarness.Human, call.Caller.User);
        Assert.NotEqual(UserId.SystemActor, call.Caller.User);
        Assert.True(call.Caller.User.IsAssigned);

        // ‏**والأثر يقول من اقترح**، والفاعل فيه هو الإنسان نفسه.
        AuditEntry trace = Assert.Single(await audit.ReadAsync(Tenant, TestContext.Current.CancellationToken));
        Assert.Equal(PublishedSurfaceAgentDraftSubmitter.AuditAction, trace.Action);
        Assert.Equal(AgentHarness.Human, trace.Actor);
        Assert.Equal("draftSalesInvoice", trace.Subject);
        Assert.Contains("باقتراح وكيل", trace.Details, StringComparison.Ordinal);

        // ‏**والشاشة شاشةُ المستند** — لا مسارُ الباب، ولا معرّفُ صفٍّ في شريط العنوان.
        Assert.Equal("/sales/invoice", landed.Value.ScreenRoute);
        Assert.DoesNotContain(CustomerRow.ToString("D"), landed.Value.ScreenRoute, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════ ٢ · السقوط يُسمّى

    /// <summary>
    /// <b>رفضُ الخادم يعود بسببه المُسمّى</b> — برمزه ونصّه العربي كما كتبته الوحدة
    /// المالكة، لا برسالةٍ عامّة ولا بصمت.
    /// </summary>
    [Fact]
    public async Task رفضُ_الخادم_يعود_سبباً_مُسمّى_لا_رسالةً_عامّة()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);
        (InMemoryAgentWorkspaceStore store, AgentWorkspaceSession session) = Workspace(clock);

        ScriptedPublishedSurface surface = new(static _ => Result<AgentSurfaceAnswer>.Success(
            new AgentSurfaceAnswer(422, """
            {
              "type": "about:blank",
              "status": 422,
              "code": "sales.customer_not_found",
              "errors": [
                {
                  "code": "sales.customer_not_found",
                  "messageAr": "لا عميل بهذا المعرّف في هذه المنشأة.",
                  "messageEn": "no customer with this identifier in this company."
                }
              ]
            }
            """)));

        AgentDraftConfirmationGate gate = Gate(
            new PublishedSurfaceAgentDraftSubmitter(surface, null, clock), store, clock);

        Result<AgentDraftLanding> landed = await ConfirmedAsync(
            gate, session, Dispatch(handles, session.SessionId, "INV-2"));

        Assert.True(landed.IsFailure);

        Error refusal = Assert.Single(landed.Errors);
        Assert.Equal("sales.customer_not_found", refusal.Code);
        Assert.Equal("لا عميل بهذا المعرّف في هذه المنشأة.", refusal.MessageAr);

        // ‏**ولا إعادةَ محاولةٍ صامتة**: نداءٌ واحد وقع، ولا ثانٍ.
        Assert.Single(surface.Calls);
    }

    /// <summary>
    /// <b>وجسمُ رفضٍ لا يُقرأ يُقال إنه لا يُقرأ</b> — ولا يُخترع له معنى، ولا يُبتلع
    /// فيبدو الرفض نجاحاً.
    /// </summary>
    [Fact]
    public void جسمُ_رفضٍ_لا_يُقرأ_يُسمّى_كذلك()
    {
        IReadOnlyList<Error> read = PublishedSurfaceAgentDraftSubmitter.Refusals(
            "draftSalesInvoice", new AgentSurfaceAnswer(503, "<html>gateway</html>"));

        Error only = Assert.Single(read);
        Assert.Equal("ai.workspace.draft_refusal_unreadable", only.Code);
        Assert.Contains("503", only.MessageAr, StringComparison.Ordinal);
    }

    /// <summary>
    /// <b>ووسيطُ مسارٍ لا يملؤه الوكيل يُرفض باسمه</b> — ولا يُخترع له معرّف، ولا
    /// يُرسَل مسارٌ فيه قوسان.
    /// </summary>
    [Fact]
    public void وسيطُ_مسارٍ_لا_يُملأ_يُرفض_باسمه()
    {
        Result<string> address = PublishedSurfaceAgentDraftSubmitter.Address(
            "draftSomething", "/api/v1/companies/{companyId}/properties/{propertyId}/units", Company);

        Assert.True(address.IsFailure);
        Error refusal = Assert.Single(address.Errors);
        Assert.Equal("ai.workspace.draft_path_parameter_unfilled", refusal.Code);
        Assert.Contains("propertyId", refusal.MessageAr, StringComparison.Ordinal);
    }

    // ═══════════════════════════════════════════════ ٣ · الازدواج ممنوعٌ بالبناء

    /// <summary>
    /// <b>تأكيدٌ مكرَّرٌ على الشكل نفسه يُعيد المسوّدة نفسها ولا يُنشئ ثانية.</b>
    /// <para>
    /// وهو نظير <c>WasAlreadyPosted</c> في القاعدة 4: الوصول الثاني بالهويّة نفسها لا
    /// يفعل شيئاً ولا يُعدّ خطأ. <b>ولا يُسأل الإنسان مرّةً ثانية</b> عن شكلٍ قَبِله.
    /// </para>
    /// </summary>
    [Fact]
    public async Task تأكيدٌ_مكرَّرٌ_يُعيد_المسوّدة_نفسها_ولا_ينادي_الباب_ثانية()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);
        (InMemoryAgentWorkspaceStore store, AgentWorkspaceSession session) = Workspace(clock);

        ScriptedPublishedSurface surface = ScriptedPublishedSurface.Accepting();

        AgentDraftConfirmationGate gate = Gate(
            new PublishedSurfaceAgentDraftSubmitter(surface, null, clock), store, clock);

        Result<AgentDraftLanding> first = await ConfirmedAsync(
            gate, session, Dispatch(handles, session.SessionId, "INV-3"));

        Assert.True(first.IsSuccess);
        Assert.Single(surface.Calls);

        // النداء الثاني بالشكل نفسه — **ولا تُعرض بطاقةٌ ولا يُنتظر إنسان**.
        Result<AgentDraftLanding> second = await gate.SubmitAsync(
            Dispatch(handles, session.SessionId, "INV-3"), TestContext.Current.CancellationToken);

        Assert.True(second.IsSuccess);
        Assert.Equal(first.Value.ScreenRoute, second.Value.ScreenRoute);
        Assert.Single(surface.Calls);
        Assert.Null(session.PendingConfirmation);
    }

    /// <summary>
    /// <b>وشكلٌ مختلف مسوّدةٌ مختلفة</b> — تصحيحُ رقمٍ ثمّ إعادةُ التأكيد يُنشئ الجديد،
    /// ولا يُعيد القديم. وحصانةٌ تبتلع التصحيح أسوأ من غيابها.
    /// </summary>
    [Fact]
    public async Task شكلٌ_مختلفٌ_مسوّدةٌ_مختلفة()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);
        (InMemoryAgentWorkspaceStore store, AgentWorkspaceSession session) = Workspace(clock);

        ScriptedPublishedSurface surface = ScriptedPublishedSurface.Accepting();

        AgentDraftConfirmationGate gate = Gate(
            new PublishedSurfaceAgentDraftSubmitter(surface, null, clock), store, clock);

        Assert.True((await ConfirmedAsync(gate, session, Dispatch(handles, session.SessionId, "INV-4"))).IsSuccess);
        Assert.True((await ConfirmedAsync(gate, session, Dispatch(handles, session.SessionId, "INV-5"))).IsSuccess);

        Assert.Equal(2, surface.Calls.Count);
    }

    /// <summary>
    /// <b>وترتيبُ المفاتيح ليس شكلاً ثانياً.</b> نموذجٌ احتماليّ يكتب المفاتيح بترتيبٍ
    /// مختلف وهو يعني الشيء نفسه، وهويّةٌ على النصّ الخام كانت ستُنتج مستندين.
    /// </summary>
    [Fact]
    public void ترتيبُ_المفاتيح_لا_يُغيّر_هويّة_المسوّدة()
    {
        Assert.Equal(
            AgentDraftIdentity.Canonical("""{"b":2,"a":{"y":1,"x":[3,4]}}"""),
            AgentDraftIdentity.Canonical("""{"a":{"x":[3,4],"y":1},"b":2}"""));

        // وترتيبُ السطور معنى لا تفصيل: مصفوفةٌ معكوسة شكلٌ آخر.
        Assert.NotEqual(
            AgentDraftIdentity.Canonical("""{"a":[1,2]}"""),
            AgentDraftIdentity.Canonical("""{"a":[2,1]}"""));
    }

    /// <summary>
    /// <b>وتأكيدٌ ثانٍ على خطوةٍ هبطت مسوّدتها ليس خطأ.</b> ضغطةٌ مكرَّرة أو نقرةٌ بعد
    /// انقطاعِ شبكةٍ تُعيد الحال كما هي، ولا تُقرأ «لا شيء ينتظر تأكيدك» على خطوةٍ
    /// يراها المستخدم «هبطت».
    /// </summary>
    [Fact]
    public async Task تأكيدٌ_ثانٍ_على_خطوةٍ_هبطت_يُقبل_ولا_يُنشئ_شيئاً()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);
        (InMemoryAgentWorkspaceStore store, AgentWorkspaceSession session) = Workspace(clock);

        ScriptedPublishedSurface surface = ScriptedPublishedSurface.Accepting();

        AgentDraftConfirmationGate gate = Gate(
            new PublishedSurfaceAgentDraftSubmitter(surface, null, clock), store, clock);

        Task<Result<AgentDraftLanding>> landing = gate.SubmitAsync(
            Dispatch(handles, session.SessionId, "INV-6"), TestContext.Current.CancellationToken);

        while (session.PendingConfirmation is null && !landing.IsCompleted)
        {
            await Task.Yield();
        }

        Guid stepId = session.PendingConfirmation!.StepId;
        Assert.True(AgentWorkspaceService.Confirm(session, stepId, accepted: true).IsSuccess);
        Assert.True((await landing).IsSuccess);

        // والتأكيد الثاني على الخطوة نفسها.
        Assert.True(AgentWorkspaceService.Confirm(session, stepId, accepted: true).IsSuccess);
        Assert.Single(surface.Calls);

        // أمّا خطوةٌ لم تهبط ولا تنتظر فتبقى رفضاً مُسمّى — والحصانة لا تبتلع ذلك.
        Result unknown = AgentWorkspaceService.Confirm(session, Guid.NewGuid(), accepted: true);
        Assert.True(unknown.IsFailure);
        Assert.Equal("ai.workspace.nothing_awaits_confirmation", unknown.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════ ٤ · الحدّ باقٍ

    /// <summary>
    /// <b>والمنفّذ الحقيقي نفسه يرفض ما ليس مسوّدة</b> — ولو نُودي مباشرةً بلا الباب
    /// الملفوف. وحارسٌ يعتمد على مستدعٍ واحد ليس حارساً.
    /// </summary>
    [Fact]
    public async Task المنفّذُ_نفسه_يرفض_ما_ليس_مسوّدة()
    {
        MovableClock clock = new();
        SignedLookupHandles handles = AgentHarness.Handles(clock);
        (_, AgentWorkspaceSession session) = Workspace(clock);

        ScriptedPublishedSurface surface = ScriptedPublishedSurface.Accepting();
        PublishedSurfaceAgentDraftSubmitter submitter = new(surface, null, clock);

        // أداةُ بروتوكولٍ بلا معرّف عملية — ونداؤها المنفّذَ يعني أن الحلقة تسرّبت.
        Result<AgentDispatch> protocolCall = AgentToolGate.Authorise(
            new AgentToolCall("tu_9", AgentProtocolTools.LookupEntity,
                JsonSerializer.Serialize(new { kind = "customer", text = "شركة المسار الامثل" })),
            AgentHarness.Caller(Tenant, Company, session.SessionId, "draftSalesInvoice"),
            new AgentTurnState(4),
            Catalogue,
            handles);

        Assert.True(protocolCall.IsSuccess);

        Result<AgentDraftLanding> refused = await submitter.SubmitAsync(
            protocolCall.Value, TestContext.Current.CancellationToken);

        Assert.True(refused.IsFailure);
        Assert.Equal("ai.workspace.step_is_not_a_draft_operation", refused.Errors[0].Code);
        Assert.Empty(surface.Calls);
    }
}
