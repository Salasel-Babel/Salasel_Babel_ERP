using System.Collections.ObjectModel;
using Babel.Ai.Boundary;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// كتلةٌ في نسخة المحادثة <b>قبل</b> الختم — نصٌّ خام مع بنيته. إنشاؤها مباح لأنها
/// لا تبلغ النموذج: لا يقبلها ناقل.
/// </summary>
/// <param name="Role">الدور.</param>
/// <param name="Kind">الشكل.</param>
/// <param name="Text">النصّ كما هو.</param>
/// <param name="ToolUseId">معرّف نداء الأداة.</param>
/// <param name="ToolName">اسم الأداة.</param>
/// <param name="Signature">توقيع كتلة التفكير كما ورد.</param>
/// <param name="IsError">هل نتيجة الأداة رفض؟</param>
public sealed record AgentTranscriptEntry(
    AgentWireRole Role,
    AgentWireBlockKind Kind,
    string Text,
    string? ToolUseId = null,
    string? ToolName = null,
    string? Signature = null,
    bool IsError = false);

/// <summary>
/// <b>يترجم نسخة المحادثة إلى طلبٍ — عبر المِصفاة، ولا طريق ثانٍ.</b>
/// <para>
/// كل كتلةٍ تحمل نصّاً، وكل نصٍّ يصير جزءاً في الظرف، والكتلة البنيوية تشير إلى موضعه.
/// فالنصّ مصدرُه واحد: <b>ما خُتم هو ما يُرسَل</b>، ولا نسخة ثانية تنحرف.
/// </para>
/// </summary>
public static class AgentTranscript
{
    /// <summary>موضع الجزء بحسب دور الكتلة وشكلها.</summary>
    /// <param name="entry">الكتلة.</param>
    public static AgentOutboundPartKind PartKindOf(AgentTranscriptEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);

        return (entry.Role, entry.Kind) switch
        {
            (AgentWireRole.User, AgentWireBlockKind.ToolResult) => AgentOutboundPartKind.ToolResult,
            (AgentWireRole.User, _) => AgentOutboundPartKind.UserTurn,
            (AgentWireRole.System, _) => AgentOutboundPartKind.SystemMessage,
            (AgentWireRole.Assistant, _) => AgentOutboundPartKind.AssistantTurn,
            _ => throw new ArgumentOutOfRangeException(nameof(entry), entry.Role, "دورٌ خارج المفردات المغلقة."),
        };
    }

    /// <summary>
    /// يختم النسخة ويبني الطلب — أو يرفض ويسمّي، فلا يُرسَل شيء.
    /// </summary>
    /// <param name="entries">الكتل بترتيبها.</param>
    /// <param name="catalogue">الكتالوج.</param>
    /// <param name="options">الإعدادات.</param>
    /// <param name="apiKeyVariable">اسم متغيّر البيئة الحامل للمفتاح.</param>
    public static Result<AgentModelRequest> Seal(
        IReadOnlyList<AgentTranscriptEntry> entries,
        AgentToolCatalogue catalogue,
        AgentOptions options,
        string apiKeyVariable)
    {
        ArgumentNullException.ThrowIfNull(entries);
        ArgumentNullException.ThrowIfNull(catalogue);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(apiKeyVariable);

        List<AgentOutboundDraft> drafts = [.. entries.Select(
            static entry => new AgentOutboundDraft(PartKindOf(entry), entry.Text))];

        Result<AgentOutboundEnvelope> sealing = AgentOutboundBoundary.Seal(drafts);
        if (sealing.IsFailure)
        {
            return Result<AgentModelRequest>.Failure(sealing.Errors);
        }

        List<AgentWireBlock> blocks = new(entries.Count);
        for (int index = 0; index < entries.Count; index++)
        {
            AgentTranscriptEntry entry = entries[index];
            blocks.Add(new AgentWireBlock(
                entry.Role, entry.Kind, index, entry.ToolUseId, entry.ToolName, entry.Signature, entry.IsError));
        }

        return Result<AgentModelRequest>.Success(new AgentModelRequest(
            sealing.Value,
            new ReadOnlyCollection<AgentWireBlock>(blocks),
            catalogue,
            AgentSystemPrompt.Text,
            options.ModelId,
            options.MaxOutputTokens,
            apiKeyVariable));
    }
}
