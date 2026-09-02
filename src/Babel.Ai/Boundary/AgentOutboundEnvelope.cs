namespace Babel.Ai.Boundary;

/// <summary>موضع الجزء من نداء النموذج — والمِصفاة تعمل على الأربعة جميعاً.</summary>
public enum AgentOutboundPartKind
{
    /// <summary>دور المستخدم — كلامه بأسمائه.</summary>
    UserTurn = 1,

    /// <summary>جسم نتيجة أداة. <b>أخطر المواضع</b>: يُبنى من بيانات محلّية.</summary>
    ToolResult = 2,

    /// <summary>رسالة نظامٍ في وسط المحادثة (اسم المنشأة المفتوحة مثلاً).</summary>
    SystemMessage = 3,

    /// <summary>صدى القراءة الذي يُعاد إلى النموذج ليؤكّد ما فهمه.</summary>
    ReadbackEcho = 4,

    /// <summary>دور مساعدٍ سابق يُعاد في نسخة المحادثة.</summary>
    AssistantTurn = 5,
}

/// <summary>
/// جزءٌ <b>مُقترَح</b> للإرسال — نصٌّ خام لم يُفحص بعد. إنشاؤه مباح للجميع لأنه
/// <b>لا يبلغ النموذج</b>: لا يقبله أي ناقل.
/// </summary>
/// <param name="Kind">موضع الجزء.</param>
/// <param name="Text">النصّ كما هو.</param>
public sealed record AgentOutboundDraft(AgentOutboundPartKind Kind, string Text)
{
    /// <summary>النصّ كما هو — ولا يُنقَّح ولا يُقصّ.</summary>
    public string Text { get; } = Text ?? throw new ArgumentNullException(nameof(Text));
}

/// <summary>
/// جزءٌ اجتاز المِصفاة. منشئه داخلي، ولا يُنشَأ إلا من <see cref="AgentOutboundBoundary"/>.
/// </summary>
public sealed record AgentOutboundPart
{
    internal AgentOutboundPart(AgentOutboundPartKind kind, string text)
    {
        Kind = kind;
        Text = text;
    }

    /// <summary>موضع الجزء.</summary>
    public AgentOutboundPartKind Kind { get; }

    /// <summary>
    /// النصّ <b>الأصلي</b>. الطيّ كان للفحص وحده، والذي يخرج هو ما كتبه صاحبه حرفاً بحرف
    /// — بأسمائه وبأرقامه العربية-الهندية وبتشكيله.
    /// </summary>
    public string Text { get; }
}

/// <summary>
/// <b>الظرف — وجودُه هو الإذن.</b>
/// <para>
/// لا سبيل إلى إنشائه إلا من <see cref="AgentOutboundBoundary.Seal(IReadOnlyList{AgentOutboundDraft})"/>:
/// منشئه داخلي، وله موضع إنشاء <b>واحد</b> في المستودع كلّه يفرضه حارسٌ يقرأ المصدر.
/// و<see cref="IAgentModelTransport{TReply}"/> لا يقبل غيره — فمن نسي المِصفاة
/// <b>لا يجد ما يمرّره</b> إلى النموذج أصلاً.
/// </para>
/// <para>
/// وهو <c>VoiceDispatch</c>/<c>VoiceConfirmationGate</c> نفسه المكتوب في هذا المستودع:
/// «فمن نسي أن يسأل البوابة لا يجد ما يمرّره». حارسٌ بنيويّ لا انضباطيّ — والفرق أن
/// الانضباطيّ يُنسى في سطرٍ واحد، والبنيويّ لا يُنسى لأنه لا يُترجَم بدونه.
/// </para>
/// </summary>
public sealed record AgentOutboundEnvelope
{
    internal AgentOutboundEnvelope(IReadOnlyList<AgentOutboundPart> parts) => Parts = parts;

    /// <summary>الأجزاء بترتيبها، وكلّها مرّت بالمِصفاة.</summary>
    public IReadOnlyList<AgentOutboundPart> Parts { get; }
}

/// <summary>
/// <b>الباب الوحيد إلى النموذج.</b> لا يقبل نصّاً ولا قائمة نصوص: يقبل
/// <see cref="AgentOutboundEnvelope"/> وحده، ولا يُنشَأ الظرف إلا خلف المِصفاة.
/// </summary>
/// <typeparam name="TReply">
/// نوع الجواب كما تعرّفه الحلقة — والحدّ لا رأي له فيه: مهمّته ما يخرج لا ما يعود.
/// </typeparam>
public interface IAgentModelTransport<TReply>
{
    /// <summary>يُرسل ظرفاً مختوماً إلى النموذج.</summary>
    /// <param name="envelope">الظرف — ولا نوع آخر يُقبل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<TReply> SendAsync(AgentOutboundEnvelope envelope, CancellationToken cancellationToken);
}
