using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>ما يظهر في اللوحة أثناء الدور.</summary>
public enum AgentTurnEventKind
{
    /// <summary>جزء تفكيرٍ مُلخَّص — تقدّمٌ يُرى بدل صمتٍ طويل.</summary>
    Thinking = 1,

    /// <summary>جزء نصٍّ للمستخدم.</summary>
    Text = 2,

    /// <summary>بدأ تنفيذ أداة.</summary>
    ToolStarted = 3,

    /// <summary>رُفض نداء أداةٍ عند البوابة — ويعود الرفض إلى النموذج فيُصحّح.</summary>
    ToolRefused = 4,

    /// <summary>غمض اسمٌ: تُعرض ورقة السؤال.</summary>
    QuestionRaised = 5,

    /// <summary><b>هبطت مسوّدة على شاشتها.</b> ولا ترحيل — الترحيل فعلٌ بصريّ يدويّ.</summary>
    DraftLanded = 6,

    /// <summary>رُفض الدور كلّه — ولم يُرسَل شيء أو تُوقّف بعد الإرسال.</summary>
    Refused = 7,

    /// <summary>انتهى الدور.</summary>
    Completed = 8,
}

/// <summary>
/// حدثٌ في اللوحة. <b>ولا يحمل معرّفاً ولا اسم صفّ</b>: ما يعبر إلى الشاشة مسارُ شاشةٍ
/// أو معرّف ورقةٍ معتِم، وما يعبر إلى النموذج أقلّ من ذلك.
/// </summary>
public sealed record AgentTurnEvent
{
    private AgentTurnEvent(
        AgentTurnEventKind kind,
        string? text,
        string? toolName,
        string? questionId,
        string? screenRoute,
        IReadOnlyList<Error>? errors,
        AgentTurnMetrics? metrics)
    {
        Kind = kind;
        Text = text;
        ToolName = toolName;
        QuestionId = questionId;
        ScreenRoute = screenRoute;
        Errors = errors ?? [];
        Metrics = metrics;
    }

    /// <summary>شكل الحدث.</summary>
    public AgentTurnEventKind Kind { get; }

    /// <summary>النصّ المعروض.</summary>
    public string? Text { get; }

    /// <summary>اسم الأداة.</summary>
    public string? ToolName { get; }

    /// <summary>معرّف ورقة السؤال — مِقبضٌ معتِم لا فهرس.</summary>
    public string? QuestionId { get; }

    /// <summary>مسار شاشة المستند الذي هبطت عليه المسوّدة.</summary>
    public string? ScreenRoute { get; }

    /// <summary>أسباب الرفض بالعربية.</summary>
    public IReadOnlyList<Error> Errors { get; }

    /// <summary>قياس الدور — عند الانتهاء.</summary>
    public AgentTurnMetrics? Metrics { get; }

    /// <summary>تفكير.</summary>
    /// <param name="text">النصّ.</param>
    public static AgentTurnEvent Thinking(string text) =>
        new(AgentTurnEventKind.Thinking, text, null, null, null, null, null);

    /// <summary>نصّ.</summary>
    /// <param name="text">النصّ.</param>
    public static AgentTurnEvent Said(string text) =>
        new(AgentTurnEventKind.Text, text, null, null, null, null, null);

    /// <summary>بدء أداة.</summary>
    /// <param name="toolName">اسمها.</param>
    public static AgentTurnEvent ToolStarted(string toolName) =>
        new(AgentTurnEventKind.ToolStarted, null, toolName, null, null, null, null);

    /// <summary>رفضُ أداة.</summary>
    /// <param name="toolName">اسمها.</param>
    /// <param name="errors">الأسباب.</param>
    public static AgentTurnEvent ToolRefused(string toolName, IReadOnlyList<Error> errors) =>
        new(AgentTurnEventKind.ToolRefused, null, toolName, null, null, errors, null);

    /// <summary>ورقة سؤال.</summary>
    /// <param name="questionId">معرّفها المعتِم.</param>
    public static AgentTurnEvent QuestionRaised(string questionId) =>
        new(AgentTurnEventKind.QuestionRaised, null, null, questionId, null, null, null);

    /// <summary>هبوط مسوّدة.</summary>
    /// <param name="screenRoute">مسار شاشتها.</param>
    public static AgentTurnEvent DraftLanded(string screenRoute) =>
        new(AgentTurnEventKind.DraftLanded, null, null, null, screenRoute, null, null);

    /// <summary>رفض الدور.</summary>
    /// <param name="errors">الأسباب.</param>
    public static AgentTurnEvent Refused(IReadOnlyList<Error> errors) =>
        new(AgentTurnEventKind.Refused, null, null, null, null, errors, null);

    /// <summary>نهاية الدور.</summary>
    /// <param name="metrics">القياس.</param>
    public static AgentTurnEvent Completed(AgentTurnMetrics metrics) =>
        new(AgentTurnEventKind.Completed, null, null, null, null, null, metrics);
}
