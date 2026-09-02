using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>مِقبضٌ فُكّ في موضع حقلٍ من جسم المسوّدة — للأثر ولاختبارات البوابة.</summary>
/// <param name="Field">مسار الحقل داخل الجسم.</param>
/// <param name="Subject">معرّف الصفّ الذي دلّ عليه المِقبض.</param>
public sealed record AgentRedeemedField(string Field, Guid Subject);

/// <summary>
/// <b>نداءٌ اجتاز البوابة. ووجودُ هذا الكائن هو الإذن</b> — لا رايةٌ داخله يقرؤها من يشاء.
/// <para>
/// ولا سبيل إلى إنشائه إلا من <see cref="AgentToolGate.Authorise"/>: منشئُه داخليّ وله
/// موضع إنشاءٍ واحد يفرضه حارسٌ يقرأ المصدر. <b>فمن نسي أن يسأل البوابة لا يجد ما
/// يمرّره</b> إلى المنفّذ — وهو <c>VoiceDispatch</c>/<c>VoiceConfirmationGate</c> نفسه
/// المكتوب في هذا المستودع، و<c>AccountCode</c> في القاعدة 2 قبله.
/// </para>
/// <para>
/// <b>وما يحمله ليس ما نطق به النموذج حرفياً:</b> كل حقلٍ شكلُه معرّف استُبدل بما فكّه
/// المِقبض، ومعرّفٌ خام لم يكن ليصل إلى هنا أصلاً — سقط عند الخطوة الخامسة.
/// </para>
/// </summary>
public sealed record AgentDispatch
{
    internal AgentDispatch(
        AgentTool tool,
        string callId,
        string body,
        IReadOnlyList<AgentRedeemedField> redeemed,
        AgentCaller caller)
    {
        Tool = tool;
        CallId = callId;
        Body = body;
        Redeemed = redeemed;
        Caller = caller;
    }

    /// <summary>الأداة كما هي في الكتالوج المغلق.</summary>
    public AgentTool Tool { get; }

    /// <summary>معرّف النداء — يُعاد به الجواب إلى النموذج.</summary>
    public string CallId { get; }

    /// <summary>
    /// الجسم الجاهز للتنفيذ: وسائط النموذج نفسها، وقد حلّ محلَّ كل مِقبضٍ ما دلّ عليه.
    /// </summary>
    public string Body { get; }

    /// <summary>المقابض التي فُكّت وأين — أثرٌ يُقرأ، لا سلطةُ فعل.</summary>
    public IReadOnlyList<AgentRedeemedField> Redeemed { get; }

    /// <summary>المتكلّم ونطاقه — والمنفّذ ينادي الوحدة المالكة بهذا النطاق لا بما في المِقبض.</summary>
    public AgentCaller Caller { get; }

    /// <summary>
    /// <b>وحتى بعد كل ذلك: مسوّدة.</b> فالطبقة الثالثة قائمةٌ أصلاً — كل ما يُنتجه هذا
    /// المسار مسوّدة، وكلّ <c>post…</c> يحتاج الشاشة. والتأكيد في اللوحة يعني «أقبل شكل
    /// هذه البيانات»، لا «رحّلها».
    /// </summary>
    public static bool ProducesADraftOnly => true;
}

/// <summary>ما تُعيده البوابة: إذنٌ أو رفضٌ يُقال للنموذج فيُصحّح.</summary>
public static class AgentDispatchResults
{
    /// <summary>يجمع أسباب الرفض في جملةٍ عربية واحدة تُكتب في نتيجة الأداة.</summary>
    /// <param name="errors">الأسباب.</param>
    public static string RefusalTextAr(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return string.Join(" · ", errors.Select(static error => error.MessageAr));
    }
}
