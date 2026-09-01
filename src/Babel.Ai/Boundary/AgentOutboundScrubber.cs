using System.Collections.ObjectModel;
using Babel.SharedKernel;

namespace Babel.Ai.Boundary;

/// <summary>
/// <b>المِصفاة الخارجة — تعمل على كل نصٍّ يغادر الخادم إلى النموذج، بلا استثناء.</b>
/// <para>
/// ما يعبر: <b>كلام المستخدم نفسه بأسمائه</b>، والبنية (نيّات، خطوات، أسماء شرائح،
/// مقابض معتِمة). وما لا يعبر: <b>كل ما شكلُه معرّف</b> — ولو كتبه المستخدم بيده، ولو
/// كان مقنَّعاً.
/// </para>
/// <para>
/// <b>ولا يكفي أن تعمل على دور المستخدم:</b> تعمل على دور المستخدم، وعلى <b>جسم كل
/// <c>tool_result</c></b>، وعلى كل رسالة نظامٍ في وسط المحادثة، وعلى صدى القراءة.
/// وأخطر هذه المواضع هو الثاني: نتيجةُ أداةٍ تُبنى من بيانات محلّية، وهي بالضبط الطريق
/// الذي يعبر منه سجلٌّ كامل لو نُسي.
/// </para>
/// <para>
/// <b>وترتيب الفحص طبقتان:</b> الأولى على النصّ المطويّ كما هو، والثانية على النصّ بعد
/// <b>لمّ الفواصل بين الخانات</b> — لأن المعرّف المقطوع بمسافة معرّفٌ كامل عند من يقرؤه.
/// والشكلُ الشامل لا يُعاد في الطبقة الثانية: بعد اللمّ يصير طولُ السلسلة دليلاً كاذباً
/// يُنتجه النثر نفسه.
/// </para>
/// </summary>
public static class AgentOutboundScrubber
{
    private static readonly AgentScrubVerdict CleanVerdict =
        new(AgentScrubOutcome.Clean, ReadOnlyCollection<Error>.Empty);

    /// <summary>الأشكال المفحوصة، بترتيبها.</summary>
    public static IReadOnlyList<AgentIdentifierShape> Shapes => AgentIdentifierShapes.All;

    /// <summary>
    /// طبقتا اللمّ، بترتيبهما — والشكل يدخل الطبقة إن كان احتماله لها أو أوسع.
    /// </summary>
    public static IReadOnlyList<AgentSplitTolerance> JoinPasses { get; } =
    [
        AgentSplitTolerance.Whitespace, AgentSplitTolerance.WhitespaceAndDashes,
    ];

    /// <summary>
    /// يفحص نصّاً واحداً. <b>ولا يُعيد نصّاً</b>: النصّ الذي يخرج هو الأصل حرفاً بحرف،
    /// وهذه الدالّة تقول «يعبر» أو «لا يعبر ولهذا السبب».
    /// </summary>
    /// <param name="text">النصّ المُزمَع إرساله.</param>
    public static AgentScrubVerdict Inspect(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        string folded = AgentBoundaryText.Fold(text);
        SortedSet<int> fired = [];

        // الطبقة الأولى: النصّ المطويّ كما هو — كلّ الأشكال، والشامل معها.
        Collect(folded, AgentSplitTolerance.None, fired);

        // ثم طبقة لكلّ درجة قطع، على الأشكال التي تحتملها وحدها.
        foreach (AgentSplitTolerance pass in JoinPasses)
        {
            string joined = AgentBoundaryText.Join(folded, pass);
            if (!string.Equals(joined, folded, StringComparison.Ordinal))
            {
                Collect(joined, pass, fired);
            }
        }

        if (fired.Count == 0)
        {
            return CleanVerdict;
        }

        List<Error> errors = [.. fired.Select(static index => AgentIdentifierShapes.All[index].Refusal)];
        return new AgentScrubVerdict(AgentScrubOutcome.Refused, new ReadOnlyCollection<Error>(errors));
    }

    /// <summary>
    /// يجمع الأشكال التي نطقت في نصٍّ واحد.
    /// <para>
    /// المُسمّاة أوّلاً كي <b>تطالب بمداها</b>، ثم الشامل — فلا يُبلَّغ عنه إلا إن وقع
    /// خارج كل مدى مُطالَبٍ به. ولهذا يُنتج رقم هوية واحد <b>جملةً واحدة</b>: مدى
    /// الشامل نفسه هو مدى الهوية، فيسقط.
    /// </para>
    /// </summary>
    /// <param name="text">النصّ المطويّ — أصلاً أو ملموماً.</param>
    /// <param name="pass">درجة اللمّ التي أنتجته؛ <c>None</c> للنصّ غير الملموم.</param>
    /// <param name="fired">مواضع الأشكال التي نطقت.</param>
    private static void Collect(string text, AgentSplitTolerance pass, SortedSet<int> fired)
    {
        List<AgentIdentifierSpan> claimed = [];

        for (int index = 0; index < AgentIdentifierShapes.All.Count; index++)
        {
            AgentIdentifierShape shape = AgentIdentifierShapes.All[index];
            if (shape.IsCatchAll || shape.Tolerance < pass)
            {
                continue;
            }

            List<AgentIdentifierSpan> spans = shape.Spans(text);
            if (spans.Count == 0)
            {
                continue;
            }

            claimed.AddRange(spans);
            fired.Add(index);
        }

        for (int index = 0; index < AgentIdentifierShapes.All.Count; index++)
        {
            AgentIdentifierShape shape = AgentIdentifierShapes.All[index];
            if (!shape.IsCatchAll || shape.Tolerance < pass)
            {
                continue;
            }

            if (shape.Spans(text).Exists(span => !claimed.Exists(span.IsInside)))
            {
                fired.Add(index);
            }
        }
    }
}
