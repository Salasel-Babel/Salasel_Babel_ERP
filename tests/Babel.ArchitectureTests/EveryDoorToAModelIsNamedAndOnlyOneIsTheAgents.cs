using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>الظرف يُثبت أنّ ما مرّ به نُقّي؛ ولا يُثبت أنّ كلّ ما يُرسَل يمرّ به.</b>
/// <para>
/// كُتب عن الحدّ الخارج إنّه «آخر ما يمرّ به شيء قبل النموذج، بلا استثناء». والجملة
/// صحيحة على <b>مسار الوكيل</b> وكاذبة على التجميعة: <c>GitHubModelsExtractionProvider</c>
/// يرسل نصّ تعليماتٍ وصورةَ مستندٍ إلى نموذجٍ بعيد ولا يمرّ بـ<c>AgentOutboundBoundary</c>
/// إطلاقاً. وهو مسارٌ مشروع له قراره وحارسه (‏<c>ExtractionResidency</c> و
/// <c>ExtractionPrompt.RefuseLedgerCodes</c>) — <b>لكنّ وجوده يعني أنّ «بلا استثناء» ادّعاءٌ
/// عن الوحدة لا عن الحدّ</b>.
/// </para>
/// <para>
/// <b>فالعلاج ليس تضييق الجملة وحدها، بل أن يصير عددُ الأبواب مُعدَّداً ومحروساً.</b>
/// بابٌ ثالث يُضاف غداً — سلكٌ آخر، أو <c>HttpClient</c> في ملفٍّ جديد — يُحمِّر هذا
/// الحارس قبل أن يُرسل بايتاً واحدة، فيُقرَّر عندها: يمرّ بالظرف، أو يُعلَن باباً ثالثاً
/// بقراره الخاصّ. وهو نمط <c>CaptureCarriesAReferenceNotTheBytes</c> نفسه: قائمةُ إعفاءٍ
/// مكتوبة، وكلّ بندٍ فيها معه سببُه.
/// </para>
/// </summary>
public sealed class EveryDoorToAModelIsNamedAndOnlyOneIsTheAgents
{
    /// <summary>الوحدة الوحيدة التي تكلّم نماذج.</summary>
    private const string AiSourcePath = "src/Babel.Ai/";

    /// <summary>
    /// الملفّات التي يجوز أن تحمل سلكاً إلى الخارج، ومعها سببُ كلٍّ منها.
    /// <b>وقائمةٌ لا يرافق بندَها سبب هي قائمةُ تجاهل لا قائمة إعفاء.</b>
    /// </summary>
    private static readonly (string Path, string Why)[] MayReachAModel =
    [
        ("src/Babel.Ai/Extraction/GitHubModels/ModelWire.cs",
            "سلك الاستخراج: ينقل بايتات ولا يصنّف. مسارٌ مستقلّ قراره ExtractionResidency وحارسه RefuseLedgerCodes"),

        ("src/Babel.Ai/Extraction/GitHubModels/ExtractorSelection.cs",
            "تركيب سلك الاستخراج وحده — لا يبني رسالةً ولا يرسلها"),

        ("src/Babel.Ai/Extraction/GitHubModels/GitHubModelsExtractionProvider.cs",
            "مستدعي سلك الاستخراج: مستندٌ وصورتُه، لا كلام مستخدمٍ ولا صفٌّ من دفتر"),

        ("src/Babel.Ai/Agent/Anthropic/AnthropicAgentGateway.cs",
            "بابُ الوكيل — ولا يقبل إلا AgentModelRequest، وهو لا يُبنى إلا من ظرفٍ مختوم"),

        ("src/Babel.Ai/Boundary/AgentOutboundEnvelope.cs",
            "إعلان الناقل نفسه: لا يقبل إلا الظرف"),
    ];

    /// <summary>ما يدلّ على سلكٍ خارج في مصدر C#.</summary>
    private static readonly Regex OutboundWire = new(
        @"\bHttpClient\b|\bHttpRequestMessage\b|\bHttpResponseMessage\b",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    /// <summary>لا ملفّ في وحدة الذكاء يحمل سلكاً خارج قائمة الإعفاء.</summary>
    [Fact]
    public void NoFileInTheAiModuleOpensAnUndeclaredDoorToAModel()
    {
        HashSet<string> allowed = new(
            MayReachAModel.Select(static entry => entry.Path.Replace('/', Path.DirectorySeparatorChar)),
            StringComparer.Ordinal);

        List<string> offenders = [];

        foreach (string file in SourceFiles())
        {
            string relative = Path.GetRelativePath(RepositoryLayout.Root, file);

            if (allowed.Contains(relative))
            {
                continue;
            }

            if (OutboundWire.IsMatch(File.ReadAllText(file)))
            {
                offenders.Add(relative);
            }
        }

        Assert.Empty(offenders);
    }

    /// <summary>
    /// <b>وقائمةُ الإعفاء تصف ملفّاتٍ موجودة</b> — وإلّا لصارت زينةً تُقرأ ولا تحرس.
    /// </summary>
    [Fact]
    public void EveryExemptionNamesAFileThatExistsAndCarriesItsReason()
    {
        foreach ((string path, string why) in MayReachAModel)
        {
            string full = Path.Combine(RepositoryLayout.Root, path.Replace('/', Path.DirectorySeparatorChar));

            Assert.True(File.Exists(full), "إعفاءٌ لملفٍّ لا وجود له: " + path);
            Assert.True(why.Length >= 30, "إعفاءٌ بلا سببٍ مكتوب: " + path);
        }
    }

    /// <summary>
    /// <b>وبابُ الوكيل لا يقبل نصّاً</b>: مدخله <c>AgentModelRequest</c>، وذاك لا يُبنى
    /// إلا من ظرفٍ مختوم. فمن نسي المِصفاة لا يجد ما يمرّره — بنيةً لا اصطلاحاً.
    /// </summary>
    [Fact]
    public void TheAgentsOwnDoorTakesASealedRequestAndNothingElse()
    {
        string gateway = File.ReadAllText(Path.Combine(
            RepositoryLayout.Root,
            "src/Babel.Ai/Agent/Anthropic/AnthropicAgentGateway.cs".Replace('/', Path.DirectorySeparatorChar)));

        Assert.Contains("AgentModelRequest request", gateway, StringComparison.Ordinal);
        Assert.DoesNotContain("string prompt", gateway, StringComparison.Ordinal);
    }

    private static IEnumerable<string> SourceFiles()
    {
        string root = Path.Combine(
            RepositoryLayout.Root, AiSourcePath.Replace('/', Path.DirectorySeparatorChar));

        foreach (string file in Directory.EnumerateFiles(root, "*.cs", SearchOption.AllDirectories))
        {
            if (file.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)
                || file.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}", StringComparison.Ordinal))
            {
                continue;
            }

            yield return file;
        }
    }
}
