using System.Collections.ObjectModel;
using Babel.SharedKernel;

namespace Babel.Ai.Boundary;

/// <summary>
/// <b>الحدّ — آخر ما يمرّ به شيءٌ قبل النموذج، بنيةً لا اصطلاحاً.</b>
/// <para>
/// <b>لماذا نوعٌ لا قاعدةُ مراجعة:</b> «نادِ المِصفاة قبل الإرسال» جملةٌ تُنسى في سطرٍ
/// واحد، ونسيانُها لا يُنتج خطأ ترجمة ولا اختباراً أحمر — يُنتج تسريباً صامتاً. أمّا
/// <see cref="AgentOutboundEnvelope"/> فمنشئه داخليّ وموضع إنشائه واحد، و
/// <see cref="IAgentModelTransport{TReply}"/> لا يقبل غيره: فالكاتب الذي لم يمرّ من هنا
/// <b>لا يملك ما يمرّره</b>. وهو الشكل الذي كتبه هذا المستودع مرّتين قبل اليوم —
/// <c>AccountCode</c> في القاعدة 2، و<c>VoiceDispatch</c> خلف <c>VoiceConfirmationGate</c>.
/// </para>
/// <para>
/// <b>وترتيب الفحص:</b> الظرف الفارغ أوّلاً، ثم كل جزء بترتيبه. والأخطاء تُجمع من
/// <b>كل</b> الأجزاء لا من أوّلها، وتُوحَّد بالرمز — فالمستخدم الذي كتب هويةً وآيباناً
/// في جملة واحدة يقرأ الجملتين معاً ويصحّح مرّة واحدة.
/// </para>
/// </summary>
public static class AgentOutboundBoundary
{
    /// <summary>
    /// يفحص كل جزء ويختم الظرف، أو يرفض ويسمّي. <b>وهذا هو موضع الإنشاء الوحيد</b>
    /// لـ<see cref="AgentOutboundEnvelope"/> في المستودع، ويفرضه حارسٌ يقرأ المصدر.
    /// </summary>
    /// <param name="drafts">الأجزاء المقترحة بترتيبها.</param>
    public static Result<AgentOutboundEnvelope> Seal(IReadOnlyList<AgentOutboundDraft> drafts)
    {
        ArgumentNullException.ThrowIfNull(drafts);

        if (drafts.Count == 0)
        {
            return Result<AgentOutboundEnvelope>.Failure(AgentBoundaryErrors.OutboundEmpty);
        }

        List<Error> errors = [];
        HashSet<string> seen = new(StringComparer.Ordinal);
        List<AgentOutboundPart> parts = new(drafts.Count);

        foreach (AgentOutboundDraft draft in drafts)
        {
            ArgumentNullException.ThrowIfNull(draft);

            AgentScrubVerdict verdict = AgentOutboundScrubber.Inspect(draft.Text);

            if (verdict.IsClean)
            {
                parts.Add(new AgentOutboundPart(draft.Kind, draft.Text));
                continue;
            }

            foreach (Error error in verdict.Errors)
            {
                if (seen.Add(error.Code))
                {
                    errors.Add(error);
                }
            }
        }

        return errors.Count > 0
            ? Result<AgentOutboundEnvelope>.Failure(errors)
            : Result<AgentOutboundEnvelope>.Success(
                new AgentOutboundEnvelope(new ReadOnlyCollection<AgentOutboundPart>(parts)));
    }

    /// <summary>يختم ظرفاً من جزءٍ واحد — الشكل الأكثر شيوعاً في اختبارٍ أو نداءٍ بسيط.</summary>
    /// <param name="kind">موضع الجزء.</param>
    /// <param name="text">النصّ.</param>
    public static Result<AgentOutboundEnvelope> Seal(AgentOutboundPartKind kind, string text) =>
        Seal([new AgentOutboundDraft(kind, text)]);
}
