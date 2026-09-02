namespace Babel.Ai.Agent;

/// <summary>
/// أداةٌ كما يراها النموذج. <b>منشئها داخليّ</b>: لا تُصنع أداةٌ خارج الكتالوج المُولَّد
/// من العقد المنشور، فلا يُضاف سطحٌ بسطرٍ في ملفّ.
/// </summary>
public sealed record AgentTool
{
    internal AgentTool(
        string name,
        string? operationId,
        string? path,
        string? method,
        string description,
        IReadOnlyList<string> idFields,
        string inputSchemaJson)
    {
        Name = name;
        OperationId = operationId;
        Path = path;
        Method = method;
        Description = description;
        IdFields = idFields;
        InputSchemaJson = inputSchemaJson;
    }

    /// <summary>اسم الأداة كما يناديها النموذج. لعمليات المسوّدات هو معرّف العملية نفسه.</summary>
    public string Name { get; }

    /// <summary>
    /// معرّف العملية المنشورة، أو <c>null</c> لأداتَي البروتوكول (البحث والسؤال) —
    /// وهما لا تبلغان باباً منشوراً أصلاً.
    /// </summary>
    public string? OperationId { get; }

    /// <summary>المسار المنشور — يُفحص مقطعُه الأخير في البوابة، فلا يكفي الاسم.</summary>
    public string? Path { get; }

    /// <summary>الفعل الشبكي المنشور.</summary>
    public string? Method { get; }

    /// <summary>وصفٌ عربي مأخوذ من ملخّص العملية في العقد.</summary>
    public string Description { get; }

    /// <summary>
    /// مسارات الحقول التي شكلُها معرّف — <c>customerId</c>، <c>lines.[].itemId</c> …
    /// كلٌّ منها <b>يجب</b> أن يصل مِقبضاً موقَّعاً، ومعرّفٌ خام يُرفض قبل التنفيذ.
    /// </summary>
    public IReadOnlyList<string> IdFields { get; }

    /// <summary>
    /// مخطّط المُدخَل <b>بنصّه كما وُلِّد</b> — لا كائناً يُعاد تسلسله. والبايتات هي ما
    /// يُذاكَر، فإعادةُ التسلسل تُغيّر ترتيب المفاتيح فتُبطل بادئة الذاكرة بلا أن يتغيّر معنى.
    /// </summary>
    public string InputSchemaJson { get; }

    /// <summary>هل هي أداة مسوّدة (لا أداة بروتوكول)؟</summary>
    public bool IsDraftOperation => OperationId is not null;
}
