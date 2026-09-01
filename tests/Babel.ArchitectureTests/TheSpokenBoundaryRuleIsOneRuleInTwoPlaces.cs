using System.Globalization;
using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>قاعدةُ حدِّ المقطع المنطوق واحدةٌ في موضعين — تُقاس، ولا يُوثَق بها.</b>
/// <para>
/// المسار المنطوق يعمل <b>بلا شبكة</b>، فله تنفيذان: <c>SpokenCommandReader</c> في
/// الخادم و<c>web/src/voice/command.ts</c> في المتصفّح. و<b>المتصفّح هو المسار الحيّ</b>:
/// على عنوانٍ غير مؤمَّن لا يفتح المتصفّح الميكروفون، فالتفريغ المكتوب هو كلُّ ما يصل.
/// فإصلاحُ حدٍّ في الخادم وحده انحرافٌ <b>لا يظهر إلا على شاشة صاحب المصلحة</b>.
/// </para>
/// <para>
/// <b>والأهمّ ممّا يطابقه هذا الحارس هو ما يمنعه:</b> أن تتحوّل قائمةُ كلمات الإيقاف
/// إلى <b>قائمةِ حظرٍ لأدوات الشرط</b>. وهو العلاج الذي يبدو علاجاً وليس به: إحصاءُ
/// «ما ليس في الاسم» إحصاءٌ لمتمّمة مجموعةٍ مفتوحة — اللغةُ كلُّها إلا صفّاً واحداً —
/// فأوّلُ أداةٍ لم تُكتب («لين»، «عشان»، «ولا») تُعيد العطل صامتاً، والقائمةُ تُقرأ
/// دليلاً على أن المشكلة عولجت. وحدُّ الاسم يقرّره السجلّ، أو يُرفض.
/// </para>
/// </summary>
public sealed partial class TheSpokenBoundaryRuleIsOneRuleInTwoPlaces
{
    private const string ServerPath = "src/Babel.Ai/Voice/SpokenCommandReader.cs";
    private const string BrowserPath = "web/src/voice/command.ts";
    private const string TextPath = "src/Babel.Ai/Voice/VoiceText.cs";

    /// <summary>
    /// أدواتُ الشرط والاستئناف — فصحى وعامّية خليجية. <b>ولا واحدة منها تجوز في قائمة
    /// كلمات الإيقاف</b>، لأن وجودها هناك يدّعي أن حدّ الاسم مسألةُ نحوٍ لا مسألةُ سجلّ.
    /// </summary>
    private static readonly string[] ForbiddenParticles =
    [
        "فان", "فإن", "إن", "لم", "لو", "إذا", "اذا", "حتى", "حتي", "بعد", "عندما",
        "لكن", "أو", "او", "بشرط", "إلا", "الا", "لين", "عشان", "ولا", "وش", "بعدين", "طالما",
    ];

    [GeneratedRegex(@"NameWordLimit\s*=\s*(?<value>[0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ServerNameLimit();

    [GeneratedRegex(@"NAME_WORD_LIMIT\s*=\s*(?<value>[0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex BrowserNameLimit();

    [GeneratedRegex(@"CodeWordLimit\s*=\s*(?<value>[0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex ServerCodeLimit();

    [GeneratedRegex(@"CODE_WORD_LIMIT\s*=\s*(?<value>[0-9]+)", RegexOptions.CultureInvariant)]
    private static partial Regex BrowserCodeLimit();

    /// <summary>الحرفيّات النصّية داخل كتلة — ما بين علامتَي اقتباس مزدوجتين.</summary>
    [GeneratedRegex("\"(?<word>[^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex TextLiteral();

    /// <summary>حرفيّات المحارف — ما بين علامتَي اقتباس مفردتين.</summary>
    [GeneratedRegex(@"'(?<word>[^'])'", RegexOptions.CultureInvariant)]
    private static partial Regex CharLiteral();

    private static string Read(string relative) => File.ReadAllText(Path.Combine(RepositoryLayout.Root, relative));

    private static int Number(Regex pattern, string source, string what)
    {
        Match match = pattern.Match(source);
        Assert.True(match.Success, "لم يُعثر على «" + what + "» — والحارس لا يمرّ على قراءةٍ فارغة (فخ-43).");
        return int.Parse(match.Groups["value"].Value, CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// يقرأ الحرفيّات بين مرساةٍ ونهايةٍ مُسمّاة. <b>والنهاية تُسمّى ولا تُخمَّن</b>:
    /// حارسٌ يبحث عن أوّل قوسٍ مغلق يقرأ <c>new[]</c> كتلةً فارغة ويمرّ (فخ-43).
    /// </summary>
    private static List<string> Block(string source, string anchor, string terminator, Regex literal, string what)
    {
        int start = source.IndexOf(anchor, StringComparison.Ordinal);
        Assert.True(start >= 0, "المرساة «" + anchor + "» غير موجودة — " + what);

        int close = source.IndexOf(terminator, start, StringComparison.Ordinal);
        Assert.True(close > start, "نهاية الكتلة «" + terminator + "» غير موجودة — " + what);

        List<string> words = [];
        foreach (Match match in literal.Matches(source[start..close]))
        {
            words.Add(match.Groups["word"].Value);
        }

        Assert.True(words.Count > 0, "الكتلة فارغة — " + what + " (فخ-43)");
        return words;
    }

    private static List<string> StopWordsOf(string path) => path == ServerPath
        ? Block(Read(ServerPath), "HashSet<string> StopWords", "}.Select(", TextLiteral(), "كلمات الإيقاف في الخادم")
        : Block(Read(BrowserPath), "const STOP_WORDS = new Set(", "].map(fold)", TextLiteral(), "كلمات الإيقاف في المتصفّح");

    [Fact]
    public void TheNameAndCodeWidthFloorsAreTheSameNumberOnBothSides()
    {
        string server = Read(ServerPath);
        string browser = Read(BrowserPath);

        Assert.Equal(
            Number(ServerNameLimit(), server, "NameWordLimit في الخادم"),
            Number(BrowserNameLimit(), browser, "NAME_WORD_LIMIT في المتصفّح"));

        Assert.Equal(
            Number(ServerCodeLimit(), server, "CodeWordLimit في الخادم"),
            Number(BrowserCodeLimit(), browser, "CODE_WORD_LIMIT في المتصفّح"));
    }

    [Fact]
    public void TheStopWordListIsTheSameSetOnBothSides()
    {
        Assert.Equal(
            StopWordsOf(ServerPath).Order(StringComparer.Ordinal),
            StopWordsOf(BrowserPath).Order(StringComparer.Ordinal));
    }

    /// <summary>
    /// <b>الحارس الذي يمنع العلاج الخاطئ.</b> أداةُ شرطٍ تُضاف إلى كلمات الإيقاف تُغلق
    /// حالةً واحدة وتُقنع القارئ أن الصنف كلَّه عولج، والحالةُ التالية تسقط بصمت.
    /// </summary>
    [Fact]
    public void NoConditionalParticleIsSmuggledIntoTheStopWordList()
    {
        string[] lists = [ServerPath, BrowserPath];

        List<string> smuggled = [];
        int scanned = 0;

        foreach (string path in lists)
        {
            List<string> words = StopWordsOf(path);
            scanned += words.Count;

            foreach (string word in words)
            {
                if (Array.Exists(ForbiddenParticles, particle => string.Equals(particle, word, StringComparison.Ordinal)))
                {
                    smuggled.Add(path + ": «" + word + "»");
                }
            }
        }

        Assert.True(scanned >= 40, "الكلمات الممسوحة: " + scanned.ToString(CultureInfo.InvariantCulture));
        Assert.True(
            smuggled.Count == 0,
            "أداةُ شرطٍ في قائمة كلمات الإيقاف — وهي العلاج الذي يبدو علاجاً وليس به:\n"
            + string.Join('\n', smuggled)
            + "\nحدُّ الاسم يقرّره سجلُّ الأسماء، أو يُرفض. ولا يُحصى ما ليس في الاسم.");
    }

    /// <summary>
    /// علاماتُ الوقف تُبقى رموزَ فصلٍ صريحة على الطرفين — وهي الإشارة الوحيدة الرخيصة
    /// التي يحملها التفريغ <b>المكتوب</b>، وهو المسار الوحيد العامل على عنوانٍ غير مؤمَّن.
    /// </summary>
    [Fact]
    public void SentencePunctuationIsKeptAsABreakTokenOnBothSides()
    {
        List<string> server = Block(
            Read(TextPath), "private static readonly char[] Breaks", "];", CharLiteral(), "علامات الوقف في الخادم");
        List<string> browser = Block(
            Read(BrowserPath), "const BREAKS = new Set(", "]);", TextLiteral(), "علامات الوقف في المتصفّح");

        Assert.Equal(server.Order(StringComparer.Ordinal), browser.Order(StringComparer.Ordinal));
        Assert.DoesNotContain(browser, static word => word.Length != 1);
    }
}
