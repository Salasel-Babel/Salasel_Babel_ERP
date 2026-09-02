namespace Babel.Ai.Agent;

/// <summary>
/// أسماء أدوات البروتوكول. <b>أربعة أشكال لا خامس لها</b>: خطّةٌ، وبحثٌ، وسؤالٌ،
/// وعمليةُ مسوّدة. ولا أداة قراءة، ولا بحث ويب، ولا صدفة، ولا تنفيذ شيفرة.
/// </summary>
public static class AgentProtocolTools
{
    /// <summary>يسأل الخادم عن اسمٍ في سجلٍّ محلّي.</summary>
    public const string LookupEntity = "lookup_entity";

    /// <summary>يعرض ورقة السؤال التي رسمها الخادم ويعود بمِقبض.</summary>
    public const string AskQuestion = "ask_question";

    /// <summary>
    /// يُعلن خطوات الطلب المركَّب <b>قبل تنفيذ أوّلها</b>.
    /// <para>
    /// <b>ولماذا وُجدت:</b> الطلب المتداخل — «أنشئ الشركة ثمّ حسابها ثمّ سند القبض» —
    /// كان يُحاوَل محاولةً واحدة فيسقط عند أوّل حقلٍ ناقص، ويُقرأ السقوط عجزاً عن الطلب
    /// كلّه. والخطّةُ المُعلَنة تجعل الطلبَ سلسلةً يُرى موضعُها: ما تمّ، وما ينتظر
    /// تأكيداً، وما سقط ولماذا. <b>وهي إعلانٌ لا سلطة</b>: لا تُنفّذ خطوةً ولا تفتح
    /// باباً — كلّ خطوةٍ تمرّ بالبوّابة نفسها حين يحين دورُها.
    /// </para>
    /// </summary>
    public const string ProposePlan = "propose_plan";

    /// <summary>هل هذا اسم أداة بروتوكول؟</summary>
    /// <param name="name">اسم الأداة.</param>
    public static bool Contains(string name) =>
        string.Equals(name, LookupEntity, StringComparison.Ordinal)
        || string.Equals(name, AskQuestion, StringComparison.Ordinal)
        || string.Equals(name, ProposePlan, StringComparison.Ordinal);
}
