using System.Diagnostics;
using System.Globalization;
using System.Text;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>لا ناتج بناء واحد مُودَع في المستودع — مهما كُتب فاصلُ مساره.</b>
/// <para>
/// <b>العطل الذي أوجد هذا الحارس، وقد وقع فعلاً ومُقيس:</b> نداءٌ لأداة
/// <c>dotnet ef migrations add</c> ترك مضيفَ بناء MSBuild تحت مسارٍ مقطعُه
/// <c>bin\\Debug</c> — <b>بشرطة خلفية داخل اسم المجلّد لا فاصلَ مسار</b>. ثم ابتلعه
/// <c>git add -A</c>. والنتيجة <b>45 ملفّاً · 839 ميغابايت</b> من ثنائيّات مُودَعة.
/// </para>
/// <para>
/// <b>ولماذا لم يمنعه شيء:</b> نمط <c>bin/</c> في <c>.gitignore</c> يطابق مجلّداً
/// اسمه <c>bin</c>، والمجلّد هنا اسمه <c>bin\\Debug</c> كلّه. وكل إقصاء في هذا المستودع
/// يبحث عن <c>/bin/</c> — بشرطة أمامية — فلا يراه: لا <c>.gitignore</c>، ولا منهج القياس
/// في ADR-0021 §4، ولا مسح أي حارس.
/// </para>
/// <para>
/// <b>وأثره ليس تجميلياً:</b> نسخةٌ جديدة من الفرع <b>لا تُبنى</b> —
/// <c>MSB3552: Resource file "**/*.resx" cannot be found</c> على
/// <c>Babel.Ledger.csproj</c>، لأن الشرطة الخلفية تكسر توسيع الأنماط في MSBuild.
/// أي أن العطل يُخفي نفسه عمّن بنى سلفاً ويظهر عند أول من يستنسخ.
/// </para>
/// <para>
/// <b>والحارس يسأل git لا القرص</b> (‏<c>traps.md</c> «حارسٌ مجموعتُه القرص لا
/// المستودع»): السؤال هنا «ما الذي <b>أُودع</b>؟» لا «ما الذي بُني؟».
/// </para>
/// </summary>
public sealed class RepositoryContainsNoBuildOutput
{
    /// <summary>مقاطع المسار التي لا يجوز أن يُودَع تحتها ملفّ، بأي فاصل كُتبت.</summary>
    private static readonly string[] ForbiddenSegments =
        ["bin", "obj", "node_modules", "dist", "TestResults", "test-results", "playwright-report"];

    private static readonly Lazy<string[]> Tracked = new(List);

    [Fact]
    public void NoTrackedFileLivesUnderABuildOutputDirectory()
    {
        List<string> offenders = [];

        foreach (string path in Tracked.Value)
        {
            // التطبيع أوّلاً: `bin\Debug` مقطعٌ واحد فيه شرطة خلفية، ويصير هنا مقطعين.
            string[] segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            if (segments.Any(segment => ForbiddenSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)))
            {
                offenders.Add(path);
            }
        }

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} ملفّ ناتج بناء مُودَع في المستودع.\n")
            + "ناتج البناء لا يُودَع: يُضخّم كل استنساخ، ويُفسد كل مسح، وقد أفشل بناء نسخة\n"
            + "جديدة فعلاً بـMSB3552 حين حمل مساره شرطة خلفية. أزِلها بـgit rm -r --cached،\n"
            + "وتحقّق من أن .gitignore يمسك الشكل الذي أفلت:\n"
            + string.Join('\n', offenders.Take(20)));
    }

    /// <summary>
    /// ولا مسارَ فيه شرطة خلفية أصلاً. الشرطة الخلفية في اسم ملفّ على Linux محرفٌ عادي،
    /// وعلى Windows <b>غير قابلة للاستنساخ إطلاقاً</b> — فالمستودع الذي يحملها لا يُستنسخ
    /// هناك. وهي فوق ذلك تُفلت من كل نمط إقصاء مكتوب بفاصل أمامي.
    /// </summary>
    [Fact]
    public void NoTrackedPathContainsABackslash()
    {
        List<string> offenders = [.. Tracked.Value.Where(static path => path.Contains('\\', StringComparison.Ordinal))];

        Assert.True(
            offenders.Count == 0,
            FormattableString.Invariant($"‏{offenders.Count} مسارٍ مُودَع فيه شرطة خلفية.\n")
            + "مسارٌ كهذا لا يُستنسخ على Windows، ويُفلت من كل نمط إقصاء مكتوب بفاصل أمامي:\n"
            + string.Join('\n', offenders.Take(20)));
    }

    /// <summary>
    /// <b>شاهدٌ موجب على الكاشف:</b> مجموعةٌ لا تحوي مخالفة تمرّ ولا تُثبت شيئاً، فيُفحص
    /// المنطق نفسه بالمسارات التي وقعت فعلاً.
    /// </summary>
    [Fact]
    public void TheDetectorCatchesTheShapesThatSlippedThrough()
    {
        (string Label, string Path)[] violations =
        [
            ("الشكل الذي أفلت فعلاً", @"src/Babel.Ledger/bin\Debug/net10.0/BuildHost-netcore/x.deps.json"),
            ("ناتج بناء عادي", "src/Babel.Ledger/bin/Debug/net10.0/Babel.Ledger.dll"),
            ("ناتج وسيط", "src/Babel.Core/obj/Debug/project.assets.json"),
            ("حزمة الواجهة", "web/dist/assets/index-abc.js"),
            ("اعتماديات npm", "web/node_modules/react/index.js"),
        ];

        foreach ((string label, string path) in violations)
        {
            string[] segments = path.Replace('\\', '/').Split('/', StringSplitOptions.RemoveEmptyEntries);

            Assert.True(
                segments.Any(segment => ForbiddenSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)),
                "الكاشف لم يلتقط: " + label + " — " + path);
        }

        // ولا يلتقط ما ليس ناتج بناء: حارسٌ يرفض كل شيء لا يميّز شيئاً.
        foreach (string innocent in new[]
                 {
                     "src/Babel.Ledger/Persistence/LedgerRows.cs",
                     "data/posting-matrix/events/sales.json",
                     "web/src/api/generated/types.ts",
                     "tools/matrix-validator/Model/Model.cs",
                 })
        {
            string[] segments = innocent.Split('/', StringSplitOptions.RemoveEmptyEntries);

            Assert.False(
                segments.Any(segment => ForbiddenSegments.Contains(segment, StringComparer.OrdinalIgnoreCase)),
                "الكاشف التقط ما ليس ناتج بناء: " + innocent);
        }
    }

    /// <summary>والمجموعة المفحوصة مستودعٌ حقيقي، لا قائمة فارغة تمرّ دائماً.</summary>
    [Fact]
    public void TheTrackedListIsARealRepository()
    {
        Assert.True(Tracked.Value.Length > 500, "الملفّات المتعقَّبة: " + Tracked.Value.Length.ToString(CultureInfo.InvariantCulture));
        Assert.Contains(Tracked.Value, static path => path == "Babel.slnx");
        Assert.Contains(Tracked.Value, static path => path == "CONTRIBUTING.md");
    }

    private static string[] List()
    {
        ProcessStartInfo start = new("git")
        {
            WorkingDirectory = RepositoryLayout.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("-C");
        start.ArgumentList.Add(RepositoryLayout.Root);
        start.ArgumentList.Add("ls-files");
        start.ArgumentList.Add("-z");

        using Process? git = Process.Start(start)
            ?? throw new InvalidOperationException("تعذّر تشغيل git. / Could not start git.");

        string output = git.StandardOutput.ReadToEnd();
        string error = git.StandardError.ReadToEnd();
        git.WaitForExit();

        return git.ExitCode == 0
            ? output.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            : throw new InvalidOperationException("‏git ls-files أخفق: " + error);
    }
}
