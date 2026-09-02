using System.Text.Json;
using Babel.Ai.Agent;
using Babel.Ai.Lookup;
using Babel.Contracts.Lookup;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>ناقلٌ مسجَّل — يُعيد شريطاً من الأحداث ولا يلمس شبكةً ولا ينفق ريالاً.</b>
/// <para>
/// وهذا هو ثمن وضع المزوّد خلف منفذ: <c>IAgentModelGateway</c> نوعٌ محايد، فمجموعة
/// الاختبارات تُشغَّل على كل إيداع بلا مفتاح وبلا فاتورة. <b>ومجموعةٌ تنفق على كل تشغيل
/// تُطفأ خلال شهر</b>، ثم يبقى الحارس مكتوباً ولا يعمل.
/// </para>
/// <para>
/// ويسجّل كل طلبٍ وصله كما هو — وعليه تُقاس ثباتُ بادئة الذاكرة: الأدوات ونصّ النظام
/// بايتاً ببايت بين نداءٍ ونداء وبين مستخدمٍ ومستخدم.
/// </para>
/// </summary>
internal sealed class RecordedAgentGateway : IAgentModelGateway
{
    private readonly Queue<Func<AgentModelRequest, IReadOnlyList<AgentModelEvent>>> _script;

    /// <summary>شريطٌ ثابت — لا يقرأ ما وصله.</summary>
    public static RecordedAgentGateway Fixed(params IReadOnlyList<AgentModelEvent>[] turns) =>
        new([.. turns.Select(static turn => new Func<AgentModelRequest, IReadOnlyList<AgentModelEvent>>(_ => turn))]);

    /// <summary>
    /// شريطٌ يقرأ ما وصله. <b>ولا بدّ منه</b>: معرّف الورقة يصدره الخادم وقت التشغيل،
    /// فنداءُ <c>ask_question</c> لا يمكن أن يُكتب في شريطٍ ثابت — ولو كُتب لاختُبر
    /// مِقبضٌ مخترَع لا مِقبضٌ صادر.
    /// </summary>
    public RecordedAgentGateway(params Func<AgentModelRequest, IReadOnlyList<AgentModelEvent>>[] turns)
    {
        _script = new Queue<Func<AgentModelRequest, IReadOnlyList<AgentModelEvent>>>(turns);
    }

    /// <summary>آخر نتيجة أداةٍ وصلت — يُقرأ منها معرّف الورقة.</summary>
    public static string? LastToolResult(AgentModelRequest request) =>
        request.Blocks
            .Where(static block => block.Kind == AgentWireBlockKind.ToolResult)
            .Select(request.TextOf)
            .LastOrDefault();

    /// <summary>الطلبات كما وصلت، بترتيبها.</summary>
    public List<AgentModelRequest> Requests { get; } = [];

    /// <summary>كم مرّةً نُودي المزوّد فعلاً.</summary>
    public int Calls => Requests.Count;

    public async IAsyncEnumerable<AgentModelEvent> StreamAsync(
        AgentModelRequest request,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken)
    {
        Requests.Add(request);

        IReadOnlyList<AgentModelEvent> turn = _script.Count > 0
            ? _script.Dequeue()(request)
            : [AgentModelEvent.TextBlock("انتهى الشريط."), AgentModelEvent.Completed("end_turn", AgentModelUsage.Zero)];

        foreach (AgentModelEvent modelEvent in turn)
        {
            await Task.Yield();
            yield return modelEvent;
        }
    }
}

/// <summary>سجلّ أسماءٍ مُحاكى: يجيب بما يُملى عليه — صفرٌ، أو واحد، أو أكثر.</summary>
internal sealed class ScriptedCandidateSource : INameCandidateSource
{
    private readonly Func<string, NameCandidateProbe> _answer;

    public ScriptedCandidateSource(string registerKey, Func<string, NameCandidateProbe> answer)
    {
        RegisterKey = registerKey;
        _answer = answer;
    }

    public string RegisterKey { get; }

    /// <summary>النصوص التي سُئل عنها — تُقاس عليها قواعدُ منع السبر.</summary>
    public List<string> Asked { get; } = [];

    public Task<NameCandidateProbe> ProbeAsync(
        NameCandidateRequest request,
        CancellationToken cancellationToken = default)
    {
        Asked.Add(request.Text);
        return Task.FromResult(_answer(request.Text));
    }
}

/// <summary>ورقة سؤالٍ مُحاكاة: تعيد مِقبض كِيانٍ كما يفعل الخادم بعد اختيار المستخدم.</summary>
internal sealed class ScriptedQuestionSheets : IAgentQuestionSheets
{
    private readonly Func<Guid, AgentCaller, Result<string>> _answer;

    public ScriptedQuestionSheets(Func<Guid, AgentCaller, Result<string>> answer) => _answer = answer;

    public List<Guid> Asked { get; } = [];

    public Task<Result<string>> AwaitAnswerAsync(
        Guid questionId,
        AgentCaller caller,
        CancellationToken cancellationToken)
    {
        Asked.Add(questionId);
        return Task.FromResult(_answer(questionId, caller));
    }
}

/// <summary>منفّذُ مسوّداتٍ يسجّل ما وصله ولا يكتب شيئاً.</summary>
internal sealed class RecordingDraftSubmitter : IAgentDraftSubmitter
{
    public List<AgentDispatch> Submitted { get; } = [];

    public Task<Result<AgentDraftLanding>> SubmitAsync(
        AgentDispatch dispatch,
        CancellationToken cancellationToken)
    {
        Submitted.Add(dispatch);
        return Task.FromResult(Result<AgentDraftLanding>.Success(
            new AgentDraftLanding("/companies/" + dispatch.Caller.CompanyId + "/sales-invoices/draft")));
    }
}

/// <summary>إعدادُ إنفاقٍ يُملى في الاختبار.</summary>
internal sealed class ScriptedBilling : IAgentTenantBillingSource
{
    private readonly AgentTenantBilling _billing;

    public ScriptedBilling(AgentTenantBilling billing) => _billing = billing;

    public Task<AgentTenantBilling> ReadAsync(TenantId tenant, CancellationToken cancellationToken) =>
        Task.FromResult(_billing);
}

/// <summary>ساعةٌ تتحرّك باليد — نافذة المحاسبة تُختبَر بالطيّ لا بالانتظار.</summary>
internal sealed class MovableClock : TimeProvider
{
    private DateTimeOffset _now = new(2026, 3, 1, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => _now;

    public void Advance(TimeSpan by) => _now += by;
}

/// <summary>أدواتٌ مشتركة لإثباتات الوكيل.</summary>
internal static class AgentHarness
{
    /// <summary>مفتاح توقيعٍ للاختبار — لا علاقة له بأي سرٍّ في المستودع.</summary>
    public static byte[] SigningKey { get; } = System.Text.Encoding.UTF8.GetBytes(
        "babel-agent-loop-test-signing-key-0123456789");

    /// <summary>مفتاحٌ ثانٍ: يُثبت أن مِقبض مُصدِرٍ لا يُفكّ عند مُصدِرٍ آخر.</summary>
    public static byte[] OtherSigningKey { get; } = System.Text.Encoding.UTF8.GetBytes(
        "babel-agent-loop-other-signing-key-987654321");

    public static LookupOptions LookupOptions { get; } = new();

    public static SignedLookupHandles Handles(TimeProvider clock) =>
        new(SigningKey, LookupOptions, clock);

    public static AgentOptions Options(Action<AgentOptions>? configure = null)
    {
        AgentOptions options = new();
        configure?.Invoke(options);
        return options;
    }

    /// <summary>الإنسان الذي تُنسب إليه كل مسوّدة في هذه الإثباتات.</summary>
    public static UserId Human { get; } = new(new Guid("11111111-1111-4111-8111-111111111111"));

    public static AgentCaller Caller(
        TenantId tenant,
        Guid companyId,
        Guid sessionId,
        params string[] permitted) =>
        new(tenant, companyId, sessionId, Human, "شركة سلاسل بابل",
            new HashSet<string>(permitted, StringComparer.Ordinal));

    public static NameRegisterLookup Lookup(TimeProvider clock, params INameCandidateSource[] sources) =>
        new(sources, Handles(clock), LookupOptions);

    /// <summary>يجمع دور الوكيل كلّه في قائمة — الأحداث تُقرأ بعد انتهائه.</summary>
    public static async Task<List<AgentTurnEvent>> RunAsync(
        AgentTurnService service,
        AgentTurnRequest request)
    {
        List<AgentTurnEvent> events = [];
        await foreach (AgentTurnEvent turnEvent in service.RunAsync(request, TestContext.Current.CancellationToken))
        {
            events.Add(turnEvent);
        }

        return events;
    }

    /// <summary>
    /// شريطُ نداءٍ واحد. <b>ولا يُكتب تعبيرَ مجموعةٍ في جسم لامدا</b>:
    /// <c>IReadOnlyList</c> ليس نوعاً يُنشَأ منها، والمصرّف يقولها بـ‏CS9174.
    /// </summary>
    public static IReadOnlyList<AgentModelEvent> Script(params AgentModelEvent[] events) => events;

    /// <summary>نداءُ أداةٍ بوسائط تُكتب كائناً.</summary>
    public static AgentModelEvent Call(string id, string name, object arguments) =>
        AgentModelEvent.ToolCall(new AgentToolCall(id, name, JsonSerializer.Serialize(arguments)));

    /// <summary>نهاية نداءٍ بقياسٍ مُعطى.</summary>
    public static AgentModelEvent Done(long input = 100, long output = 20, long cacheRead = 0, long cacheCreated = 0) =>
        AgentModelEvent.Completed("end_turn", new AgentModelUsage(input, output, cacheRead, cacheCreated));

    /// <summary>نهاية نداءٍ توقّف عند نداء أداة.</summary>
    public static AgentModelEvent DoneWithTools(long input = 100, long output = 20, long cacheRead = 0) =>
        AgentModelEvent.Completed("tool_use", new AgentModelUsage(input, output, cacheRead, 0));
}
