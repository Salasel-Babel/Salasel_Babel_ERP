using Babel.Ai.Boundary;
using Babel.Ai.Tests.Support;
using Xunit;

namespace Babel.Ai.Tests.Boundary;

/// <summary>
/// <b>ثابتٌ لا يقرؤه شيء ليس مصدراً واحداً — هو ملفٌّ نصّي بجانب المصدر الواحد.</b>
/// <para>
/// كُتب <see cref="AgentBoundaryErrors.PanelRefusalAr"/> كي «تعرضه الواجهة ولا تؤلّفه»،
/// ولم يكن يشير إليه شيء: لا شيفرة منتَج، ولا اختبار. فتحريرُه أو تفريغُه أو حذفُه
/// لا يُحمِّر شيئاً، وتظلّ الواجهة تكتب عربيتها بيدها ويظنّ القارئ أن هنا مصدراً واحداً.
/// </para>
/// <para>
/// وهذا الحارس هو <c>TheBrowserCatalogueMirrorsTheServer</c> نفسه مُطبَّقاً على جملة:
/// يقرأ ملفّ اللغة العربية <b>نفسه</b> — لا وصفاً له — ويطابق نصّه بنصّ الثابت. فالعربية
/// مصدرٌ واحد <b>بالقياس</b>، والانحراف يُحمِّر بوّابةً لا شاشة.
/// </para>
/// </summary>
public sealed class ThePanelSentenceIsTheOneTheBrowserRenders
{
    private const string ArabicLocale = "web/src/i18n/locales/ar.web.ts";

    /// <summary>صدرُ اللوحة في المتصفّح هو صدرُها في الخادم، حرفاً بحرف.</summary>
    [Fact]
    public void ThePanelHeadingInTheBrowserIsTheConstantTheServerPublishes()
    {
        string source = File.ReadAllText(RepositoryRoot.At(ArabicLocale));

        Assert.Contains(
            "panelRefusal: \"" + AgentBoundaryErrors.PanelRefusalAr + "\"",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>وكذلك جملة العلاج — وهي الجزء الذي يُنهي الدورة بدل أن يُعيدها.</summary>
    [Fact]
    public void TheAmountRemedyInTheBrowserIsTheConstantTheServerPublishes()
    {
        string source = File.ReadAllText(RepositoryRoot.At(ArabicLocale));

        Assert.Contains(
            "amountRemedy: \"" + AgentBoundaryErrors.AmountRemedyAr + "\"",
            source,
            StringComparison.Ordinal);
    }

    /// <summary>
    /// والصدر <b>لا يسمّي شكلاً بعينه</b>: صدرٌ يقول «الهوية» فوق جملةِ آيبانٍ يتّهم
    /// غير الجاني، وهو العطل الذي سجّله المستودع باسمه.
    /// </summary>
    [Fact]
    public void ThePanelHeadingNamesNoSingleShape()
    {
        Assert.True(AgentBoundaryErrors.PanelRefusalAr.Length >= 40);

        foreach (AgentIdentifierShape shape in AgentOutboundScrubber.Shapes)
        {
            Assert.DoesNotContain(shape.Key, AgentBoundaryErrors.PanelRefusalAr, StringComparison.Ordinal);
        }
    }
}
