using System.Text.RegularExpressions;
using Babel.SharedKernel;

namespace Babel.Ai.Boundary;

/// <summary>
/// شكلُ معرّفٍ واحد: نمطُه، وجملةُ رفضه، وموضعُه من الترتيب. الأشكال مُعدَّدة في
/// <see cref="AgentIdentifierShapes"/> ولا تُنشأ من خارج هذه الوحدة — منشئها داخلي.
/// </summary>
public sealed class AgentIdentifierShape
{
    private readonly Regex _pattern;

    internal AgentIdentifierShape(
        string key,
        Regex pattern,
        Error refusal,
        AgentSplitTolerance tolerance,
        bool isCatchAll)
    {
        Key = key;
        _pattern = pattern;
        Refusal = refusal;
        Tolerance = tolerance;
        IsCatchAll = isCatchAll;
    }

    /// <summary>المفتاح الثابت: <c>national_id</c> · <c>iban</c> · <c>vat</c> …</summary>
    public string Key { get; }

    /// <summary>جملة الرفض العربية ورمزها.</summary>
    public Error Refusal { get; }

    /// <summary>رمز الرفض — مختصرٌ لـ<c>Refusal.Code</c>.</summary>
    public string Code => Refusal.Code;

    /// <summary>
    /// كم يحتمل هذا الشكل من القطع — ولماذا يختلف عن أخيه. الشرح كاملاً في
    /// <see cref="AgentSplitTolerance"/>.
    /// </summary>
    public AgentSplitTolerance Tolerance { get; }

    /// <summary>
    /// هل هذا هو الشكل الشامل؟ الشامل لا يُبلَّغ عنه إلا حين يقع خارج ما طالب به شكلٌ
    /// مُسمّى — فرقم هويةٍ واحد يُنتج <b>جملةً واحدة</b> لا جملتين.
    /// </summary>
    public bool IsCatchAll { get; }

    /// <summary>
    /// هل يطابق هذا الشكلُ النصَّ؟ يطوي النصّ أوّلاً ثم يفحصه متّصلاً ومقطوعاً.
    /// <b>للاستعمال المباشر من حرّاس أخرى</b>؛ والمسار الكامل هو
    /// <see cref="AgentOutboundScrubber.Inspect(string)"/>.
    /// </summary>
    /// <param name="text">النصّ كما ورد.</param>
    public bool Matches(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string folded = AgentBoundaryText.Fold(text);
        if (_pattern.IsMatch(folded))
        {
            return true;
        }

        foreach (AgentSplitTolerance pass in AgentOutboundScrubber.JoinPasses)
        {
            if (Tolerance < pass)
            {
                continue;
            }

            string joined = AgentBoundaryText.Join(folded, pass);
            if (!string.Equals(joined, folded, StringComparison.Ordinal) && _pattern.IsMatch(joined))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>مواضع المطابقات في نصٍّ <b>مطويٍّ سلفاً</b>.</summary>
    /// <param name="foldedText">نصّ مرّ بـ<c>AgentBoundaryText.Fold</c>.</param>
    internal List<AgentIdentifierSpan> Spans(string foldedText)
    {
        List<AgentIdentifierSpan> spans = [];

        foreach (Match match in _pattern.Matches(foldedText))
        {
            spans.Add(new AgentIdentifierSpan(match.Index, match.Index + match.Length));
        }

        return spans;
    }
}

/// <summary>مدى مطابقة: البداية والنهاية المفتوحة.</summary>
/// <param name="Start">فهرس البداية.</param>
/// <param name="End">فهرس النهاية غير الشامل.</param>
internal readonly record struct AgentIdentifierSpan(int Start, int End)
{
    /// <summary>هل يقع هذا المدى داخل مدى آخر؟</summary>
    /// <param name="outer">المدى الخارجي.</param>
    public bool IsInside(AgentIdentifierSpan outer) => outer.Start <= Start && End <= outer.End;
}
