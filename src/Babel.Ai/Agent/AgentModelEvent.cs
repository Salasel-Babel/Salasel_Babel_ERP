using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>قياس استهلاك نداءٍ واحد — واقعةٌ يُعيدها المزوّد لا رقمٌ نقدّره.</summary>
/// <param name="InputTokens">رموز المُدخَل التي حُوسب عليها.</param>
/// <param name="OutputTokens">رموز المُخرَج.</param>
/// <param name="CacheReadInputTokens">
/// <b>رموزٌ قُرئت من الذاكرة.</b> صفرٌ عبر نداءَين متتالِيَين يعني <b>مُبطِلاً صامتاً</b>
/// في البادئة، لا ذاكرةً باردة — وهو ما يفحصه اختبارٌ في هذه الوحدة.
/// </param>
/// <param name="CacheCreationInputTokens">رموزٌ كُتبت في الذاكرة.</param>
public sealed record AgentModelUsage(
    long InputTokens,
    long OutputTokens,
    long CacheReadInputTokens,
    long CacheCreationInputTokens)
{
    /// <summary>لا استهلاك.</summary>
    public static AgentModelUsage Zero { get; } = new(0, 0, 0, 0);

    /// <summary>مجموع الرموز المحاسَب عليها — وهي وحدة السقف.</summary>
    public long Billable => InputTokens + OutputTokens + CacheReadInputTokens + CacheCreationInputTokens;

    /// <summary>يجمع قياسين.</summary>
    /// <param name="other">القياس الآخر.</param>
    public AgentModelUsage Plus(AgentModelUsage other)
    {
        ArgumentNullException.ThrowIfNull(other);
        return new AgentModelUsage(
            InputTokens + other.InputTokens,
            OutputTokens + other.OutputTokens,
            CacheReadInputTokens + other.CacheReadInputTokens,
            CacheCreationInputTokens + other.CacheCreationInputTokens);
    }
}

/// <summary>شكل الحدث المتدفّق.</summary>
public enum AgentModelEventKind
{
    /// <summary>جزءٌ من تفكيرٍ مُلخَّص — يُعرض في اللوحة تقدّماً.</summary>
    ThinkingDelta = 1,

    /// <summary>جزءٌ من نصّ.</summary>
    TextDelta = 2,

    /// <summary>كتلة تفكيرٍ اكتملت — تُعاد بتوقيعها في نسخة المحادثة.</summary>
    ThinkingBlock = 3,

    /// <summary>كتلة نصٍّ اكتملت.</summary>
    TextBlock = 4,

    /// <summary>نداء أداةٍ اكتمل.</summary>
    ToolCall = 5,

    /// <summary>انتهى النداء: سببُ التوقّف والقياس.</summary>
    Completed = 6,
}

/// <summary>
/// <b>حدثٌ من النموذج — نوعٌ محايد لا نوع مزوّد.</b>
/// <para>
/// والحياد هو ما يجعل الاختبارات لا تنفق مالاً: الناقل خلف منفذ، والاختبار يعيد تشغيل
/// شريطٍ مسجَّل من هذه الأحداث. ومجموعةُ اختباراتٍ تُنفق على كل تشغيل تُطفأ خلال شهر.
/// </para>
/// </summary>
public sealed record AgentModelEvent
{
    private AgentModelEvent(
        AgentModelEventKind kind,
        string? text,
        string? signature,
        AgentToolCall? call,
        string? stopReason,
        AgentModelUsage? usage)
    {
        Kind = kind;
        Text = text;
        Signature = signature;
        Call = call;
        StopReason = stopReason;
        Usage = usage;
    }

    /// <summary>شكل الحدث.</summary>
    public AgentModelEventKind Kind { get; }

    /// <summary>النصّ — للأجزاء والكتل.</summary>
    public string? Text { get; }

    /// <summary>توقيع كتلة التفكير كما ورد.</summary>
    public string? Signature { get; }

    /// <summary>نداء الأداة.</summary>
    public AgentToolCall? Call { get; }

    /// <summary>سبب التوقّف كما أعلنه المزوّد.</summary>
    public string? StopReason { get; }

    /// <summary>القياس.</summary>
    public AgentModelUsage? Usage { get; }

    /// <summary>جزء تفكير.</summary>
    /// <param name="text">النصّ.</param>
    public static AgentModelEvent ThinkingDelta(string text) =>
        new(AgentModelEventKind.ThinkingDelta, text, null, null, null, null);

    /// <summary>جزء نصّ.</summary>
    /// <param name="text">النصّ.</param>
    public static AgentModelEvent TextDelta(string text) =>
        new(AgentModelEventKind.TextDelta, text, null, null, null, null);

    /// <summary>كتلة تفكيرٍ كاملة.</summary>
    /// <param name="text">النصّ.</param>
    /// <param name="signature">التوقيع كما ورد.</param>
    public static AgentModelEvent ThinkingBlock(string text, string signature) =>
        new(AgentModelEventKind.ThinkingBlock, text, signature, null, null, null);

    /// <summary>كتلة نصٍّ كاملة.</summary>
    /// <param name="text">النصّ.</param>
    public static AgentModelEvent TextBlock(string text) =>
        new(AgentModelEventKind.TextBlock, text, null, null, null, null);

    /// <summary>نداء أداة.</summary>
    /// <param name="call">النداء.</param>
    public static AgentModelEvent ToolCall(AgentToolCall call) =>
        new(AgentModelEventKind.ToolCall, null, null, call, null, null);

    /// <summary>نهاية النداء.</summary>
    /// <param name="stopReason">سبب التوقّف.</param>
    /// <param name="usage">القياس.</param>
    public static AgentModelEvent Completed(string stopReason, AgentModelUsage usage) =>
        new(AgentModelEventKind.Completed, null, null, null, stopReason, usage);
}

/// <summary>
/// <b>الباب الوحيد إلى المزوّد.</b> لا يقبل نصّاً ولا رسائل: يقبل
/// <see cref="AgentModelRequest"/> وحده، وهو لا يُبنى إلا من ظرفٍ مختوم.
/// </summary>
public interface IAgentModelGateway
{
    /// <summary>يُرسل ويُعيد الأحداث بترتيبها.</summary>
    /// <param name="request">الطلب — ولا نوع آخر يُقبل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    IAsyncEnumerable<AgentModelEvent> StreamAsync(AgentModelRequest request, CancellationToken cancellationToken);
}

/// <summary>قياس دورٍ كامل — يُسجَّل ويُقرأ، ولا يُشتقّ منه عددُ مرشّحين ولا اسم.</summary>
public sealed class AgentTurnMetrics
{
    private readonly List<AgentModelUsage> _calls = [];

    /// <summary>قياس كل نداء بترتيبه.</summary>
    public IReadOnlyList<AgentModelUsage> Calls => _calls;

    /// <summary>المجموع.</summary>
    public AgentModelUsage Total { get; private set; } = AgentModelUsage.Zero;

    /// <summary>عدد نداءات النموذج في هذا الدور.</summary>
    public int ModelCalls => _calls.Count;

    /// <summary>يسجّل قياس نداء.</summary>
    /// <param name="usage">القياس.</param>
    public void Record(AgentModelUsage usage)
    {
        ArgumentNullException.ThrowIfNull(usage);
        _calls.Add(usage);
        Total = Total.Plus(usage);
    }

    /// <summary>
    /// هل قُرئت الذاكرة في نداءٍ بعد الأول؟ <b>وهذا هو الفحص الذي يكشف المُبطِل الصامت</b>:
    /// صفرٌ هنا عبر نداءات متكرّرة يعني أن شيئاً في البادئة يتغيّر، لا أن الذاكرة باردة.
    /// </summary>
    public bool CacheWasReadAfterTheFirstCall =>
        _calls.Skip(1).Any(static call => call.CacheReadInputTokens > 0);
}

/// <summary>ما يُعيده منفّذ المسوّدة — <b>إلى الشاشة لا إلى النموذج</b>.</summary>
/// <param name="ScreenRoute">
/// مسار شاشة المستند في المتصفّح. <b>ولا يعبر إلى النموذج</b>: النموذج يقرأ
/// «‏draft» ولا يقرأ معرّفاً — فلا يتعلّم من دورٍ إلى دور أن مستنداً بعينه وُجد.
/// </param>
public sealed record AgentDraftLanding(string ScreenRoute);

/// <summary>
/// منفّذ المسوّدات. <b>لا يقبل إلا <see cref="AgentDispatch"/></b> — ومنشئُه داخليّ
/// و<see cref="AgentToolGate.Authorise"/> موضعُ إنشائه الوحيد.
/// </summary>
public interface IAgentDraftSubmitter
{
    /// <summary>يُنشئ المسوّدة في الوحدة المالكة.</summary>
    /// <param name="dispatch">الأمر الذي اجتاز البوابة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<Result<AgentDraftLanding>> SubmitAsync(AgentDispatch dispatch, CancellationToken cancellationToken);
}

/// <summary>
/// ورقة السؤال — <b>يملكها الخادم والمتصفّح، ولا يسهم فيها النموذج إلا بمعرّفها</b>.
/// <para>
/// وتنفيذها ينتظر إنساناً: يُعرض السؤال، ويختار المستخدم، ويعود <b>مِقبضٌ واحد</b>.
/// وشكل الجواب واحدٌ سواء اختار قائماً أو أنشأ جديداً — فلا يتعلّم النموذج حتى
/// <b>أنّ</b> طرفاً أُنشئ.
/// </para>
/// </summary>
public interface IAgentQuestionSheets
{
    /// <summary>يعرض الورقة وينتظر اختيار المستخدم، ثم يعيد مِقبض الكِيان.</summary>
    /// <param name="questionId">معرّف الورقة كما فُكّ من مِقبض الغرض «سؤال».</param>
    /// <param name="caller">المتكلّم ونطاقه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<Result<string>> AwaitAnswerAsync(Guid questionId, AgentCaller caller, CancellationToken cancellationToken);
}
