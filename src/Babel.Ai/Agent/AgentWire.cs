using Babel.Ai.Boundary;

namespace Babel.Ai.Agent;

/// <summary>دور الكتلة في نسخة المحادثة.</summary>
public enum AgentWireRole
{
    /// <summary>المستخدم.</summary>
    User = 1,

    /// <summary>المساعد.</summary>
    Assistant = 2,

    /// <summary>
    /// رسالة نظامٍ <b>في وسط الرسائل</b> — لا نصّ النظام العلوي. وهنا يوضع ما يتغيّر
    /// بين منشأةٍ وأخرى (اسم الشركة المفتوحة، تاريخ اليوم) كي يبقى العلويّ واحداً
    /// بايتاً ببايت فتُقرأ الذاكرة.
    /// </summary>
    System = 3,
}

/// <summary>شكل الكتلة.</summary>
public enum AgentWireBlockKind
{
    /// <summary>نصّ.</summary>
    Text = 1,

    /// <summary>تفكيرٌ يُعاد بتوقيعه كما ورد.</summary>
    Thinking = 2,

    /// <summary>نداء أداةٍ نطق به النموذج، ويُعاد في نسخة المحادثة.</summary>
    ToolUse = 3,

    /// <summary>نتيجة أداة.</summary>
    ToolResult = 4,
}

/// <summary>
/// <b>كتلةٌ في نسخة المحادثة — وهي بنيةٌ بلا نصّ.</b>
/// <para>
/// النصّ ليس هنا: هو في <see cref="AgentOutboundEnvelope.Parts"/> عند
/// <see cref="PartIndex"/>. <b>ولذلك سببٌ واحد</b> — لو حملت الكتلة نصّها لصار في الطلب
/// مصدرا حقيقةٍ للنصّ: ما خُتم، وما يُرسَل. ولانحرفا يوماً بلا أن يُحمِّر شيء. فالبنية
/// تشير والظرف يحمل، والظرف لا يُصنَع إلا خلف المِصفاة.
/// </para>
/// </summary>
public sealed record AgentWireBlock
{
    internal AgentWireBlock(
        AgentWireRole role,
        AgentWireBlockKind kind,
        int partIndex,
        string? toolUseId,
        string? toolName,
        string? signature,
        bool isError)
    {
        Role = role;
        Kind = kind;
        PartIndex = partIndex;
        ToolUseId = toolUseId;
        ToolName = toolName;
        Signature = signature;
        IsError = isError;
    }

    /// <summary>دور الكتلة.</summary>
    public AgentWireRole Role { get; }

    /// <summary>شكلها.</summary>
    public AgentWireBlockKind Kind { get; }

    /// <summary>موضع نصّها في الظرف المختوم.</summary>
    public int PartIndex { get; }

    /// <summary>معرّف نداء الأداة — لكتل الأداة ونتائجها.</summary>
    public string? ToolUseId { get; }

    /// <summary>اسم الأداة — لكتل الأداة.</summary>
    public string? ToolName { get; }

    /// <summary>
    /// توقيع كتلة التفكير كما ورد. <b>يُعاد كما هو بلا تغيير حرف</b> — والتوقيع الذي
    /// يُعدَّل يُبطل الكتلة عند المزوّد.
    /// </summary>
    public string? Signature { get; }

    /// <summary>هل نتيجة الأداة رفض؟ يُقرأ النموذج الرفض فيُصحّح.</summary>
    public bool IsError { get; }
}

/// <summary>
/// <b>طلبٌ إلى النموذج — ولا يُبنى إلا من ظرفٍ مختوم.</b>
/// <para>
/// منشئه داخليّ، ومعامله الأول <see cref="AgentOutboundEnvelope"/> الذي <b>لا يُنشَأ إلا
/// من <c>AgentOutboundBoundary.Seal</c></b> (وحارسٌ قائم يفرض أن موضع إنشائه واحد).
/// فالخاصّية تنتقل بالتركيب: من لم يمرّ بالمِصفاة لا يملك ظرفاً، ومن لا ظرف له لا يملك
/// طلباً، ومن لا طلب له لا يبلغ النموذج.
/// </para>
/// </summary>
public sealed record AgentModelRequest
{
    internal AgentModelRequest(
        AgentOutboundEnvelope envelope,
        IReadOnlyList<AgentWireBlock> blocks,
        AgentToolCatalogue catalogue,
        string systemPrompt,
        string modelId,
        int maxOutputTokens,
        string apiKeyVariable)
    {
        Envelope = envelope;
        Blocks = blocks;
        Catalogue = catalogue;
        SystemPrompt = systemPrompt;
        ModelId = modelId;
        MaxOutputTokens = maxOutputTokens;
        ApiKeyVariable = apiKeyVariable;
    }

    /// <summary>الظرف المختوم — مصدر كل نصٍّ في هذا الطلب، ولا مصدر ثانٍ.</summary>
    public AgentOutboundEnvelope Envelope { get; }

    /// <summary>بنية المحادثة، بترتيبها.</summary>
    public IReadOnlyList<AgentWireBlock> Blocks { get; }

    /// <summary>الكتالوج — واحدٌ للجميع بايتاً ببايت.</summary>
    public AgentToolCatalogue Catalogue { get; }

    /// <summary>نصّ النظام المُجمَّد — بلا تاريخ ولا اسم شركة ولا معرّف مستخدم ولا عدد.</summary>
    public string SystemPrompt { get; }

    /// <summary>معرّف النموذج.</summary>
    public string ModelId { get; }

    /// <summary>سقف رموز المُخرَج.</summary>
    public int MaxOutputTokens { get; }

    /// <summary><b>اسم</b> متغيّر البيئة الحامل للمفتاح — لا المفتاح.</summary>
    public string ApiKeyVariable { get; }

    /// <summary>نصّ كتلةٍ — يُقرأ من الظرف وحده.</summary>
    /// <param name="block">الكتلة.</param>
    public string TextOf(AgentWireBlock block)
    {
        ArgumentNullException.ThrowIfNull(block);
        return Envelope.Parts[block.PartIndex].Text;
    }
}
