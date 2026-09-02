namespace Babel.Ai.Agent;

/// <summary>
/// أسماء أدوات البروتوكول. <b>ثلاثة أشكال لا رابع لها</b>: بحثٌ، وسؤالٌ، وعمليةُ مسوّدة.
/// ولا أداة قراءة، ولا بحث ويب، ولا صدفة، ولا تنفيذ شيفرة.
/// </summary>
public static class AgentProtocolTools
{
    /// <summary>يسأل الخادم عن اسمٍ في سجلٍّ محلّي.</summary>
    public const string LookupEntity = "lookup_entity";

    /// <summary>يعرض ورقة السؤال التي رسمها الخادم ويعود بمِقبض.</summary>
    public const string AskQuestion = "ask_question";

    /// <summary>هل هذا اسم أداة بروتوكول؟</summary>
    /// <param name="name">اسم الأداة.</param>
    public static bool Contains(string name) =>
        string.Equals(name, LookupEntity, StringComparison.Ordinal)
        || string.Equals(name, AskQuestion, StringComparison.Ordinal);
}
