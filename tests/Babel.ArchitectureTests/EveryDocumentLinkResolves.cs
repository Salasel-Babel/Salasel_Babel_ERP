using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>كل إحالة نسبية في الوثائق تصل إلى ملف موجود.</b>
/// <para>
/// <b>شقيقة <c>AdrRegisterIsSelfConsistent</c>، لا تكرارٌ لها.</b> تلك تحرس <b>الأرقام</b>:
/// أن يكون التسلسل بلا فجوة، وأن يكون المفتاح فريداً، وأن تطابق الترويسةُ اسمَ الملف، وأن
/// يوجد كل رقم <b>يُذكر</b> في نصّ. وهذه تحرس ما لا تحرسه تلك: أن يوجد الملف الذي
/// <b>يُشار إليه بمساره</b>.
/// </para>
/// <para>
/// <b>وقد وقع، وعاش أياماً أخضر:</b> أُنزل فرعان فخُصّص لهما <c>ADR-0027</c> و<c>ADR-0029</c>
/// وأُعيدت تسمية ملفّيهما — ولم تُصحَّح الإحالات إليهما. فبقيت <b>سبع</b> إحالات في
/// <c>ADR-0018</c> و<c>ADR-0021</c> و<c>ADR-0029</c> و<c>measurements.md</c>
/// و<c>CONTRIBUTING.md</c> تشير إلى <c>ADR-جديد-…‏.md</c>، وهي أسماء ملفات <b>لم تعد
/// موجودة</b>. ولم يحمرّ شيء: القاعدة الرقمية لا ترى المسارات، ولا حارس يفتح رابطاً.
/// </para>
/// <para>
/// <b>ولماذا هذا العطل صامتٌ بامتياز:</b> السجلّ كلّه مبنيّ على أن يستطيع مهندس بلا سياق أن
/// يتتبّع قراراً إلى مصدره. <b>وإحالةٌ ميتة لا تبدو ميتة</b> — تبدو مرجعاً — فيقرأ القارئ
/// اسم القرار ويظنّ أنه اطّلع عليه. الوثيقة تفقد قيمتها وهي تحتفظ بمظهرها كاملاً، وهو
/// المعنى نفسه الذي يجعل مسباراً لا يُبنى أخطرَ من مسبارٍ محذوف.
/// </para>
/// <para>
/// <b>والنائب الوحيد المعفى</b> هو القالب التعليمي الحرفي <c>ADR-جديد-اسم-المفتاح.md</c>،
/// وهو مكتوب في <c>decisions/README.md</c> و<c>CONTRIBUTING.md</c> ليُنسخ لا ليُفتح.
/// <b>ولا يُعفى</b> <c>ADR-جديد-&lt;مفتاح-لاتيني&gt;.md</c>: ذلك بالضبط شكل الإحالة التي
/// بقيت بعد الإنزال، وهي التي يجب أن تحمرّ.
/// </para>
/// </summary>
public sealed partial class EveryDocumentLinkResolves
{
    /// <summary>القالب التعليمي الحرفي — يُنسخ ولا يُفتح.</summary>
    private const string InstructionalPlaceholder = "ADR-جديد-اسم-المفتاح.md";

    /// <summary>
    /// عددٌ أدنى يمنع القاعدة من المرور فراغاً. لو ضمر المُحلِّل فلم يجد إحالات، فالخُضرة
    /// تعني «لم أفحص» لا «كل شيء سليم» — وهي الحالة التي تجعل الحارس زينة.
    /// </summary>
    private const int MinimumLinksScanned = 400;

    [GeneratedRegex(@"\[[^\]]*\]\((?<target>[^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex MarkdownLink();

    [Fact]
    public void NoRelativeLinkInAnyMarkdownFilePointsAtSomethingThatIsNotThere()
    {
        List<string> problems = [];
        int scanned = 0;

        foreach (string path in MarkdownFiles())
        {
            string relativeSource = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');

            // الملفّ قد يزول بين التعداد والقراءة: اختبارٌ آخر يزرع ملفّاً ويحذفه.
            if (!File.Exists(path))
            {
                continue;
            }

            string? directory = Path.GetDirectoryName(path);
            if (directory is null)
            {
                continue;
            }

            foreach (Match match in MarkdownLink().Matches(File.ReadAllText(path)))
            {
                string target = match.Groups["target"].Value;

                if (target.StartsWith("http://", StringComparison.Ordinal)
                    || target.StartsWith("https://", StringComparison.Ordinal)
                    || target.StartsWith("mailto:", StringComparison.Ordinal)
                    || target.StartsWith('#'))
                {
                    continue;
                }

                // المرساة داخل الملف ليست جزءاً من المسار.
                string withoutAnchor = target.Split('#')[0];
                if (withoutAnchor.Length == 0)
                {
                    continue;
                }

                if (string.Equals(withoutAnchor, InstructionalPlaceholder, StringComparison.Ordinal))
                {
                    continue;
                }

                scanned++;

                string resolved = Path.GetFullPath(Path.Combine(directory, withoutAnchor));
                if (File.Exists(resolved) || Directory.Exists(resolved))
                {
                    continue;
                }

                problems.Add(
                    $"{relativeSource}: الإحالة «{withoutAnchor}» لا تصل إلى شيء.\n"
                    + "  → إن كان القرار قد أُنزل فقد صار له رقم: صحّح المسار والاسم الظاهر معاً.");
            }
        }

        Assert.True(
            scanned >= MinimumLinksScanned,
            FormattableString.Invariant(
                $"فُحصت {scanned} إحالة نسبية فقط — المُحلِّل ضامر والقاعدة تمرّ فراغاً."));

        Assert.True(
            problems.Count == 0,
            $"إحالاتٌ ميتة في الوثائق ({problems.Count}) — وإحالةٌ ميتة تبدو مرجعاً:\n"
            + string.Join('\n', problems));
    }

    /// <summary>
    /// نفس استثناءات <c>AdrRegisterIsSelfConsistent</c>: مُخرَجات البناء والتوزيع ليست وثائق،
    /// وقراءتها تُدخل الحارس في سباقٍ مع اختبارات تزرع ملفّات تحت <c>web/dist</c>.
    /// </summary>
    private static IEnumerable<string> MarkdownFiles()
    {
        foreach (string path in Directory.EnumerateFiles(RepositoryLayout.Root, "*.md", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');

            if (relative.StartsWith(".git/", StringComparison.Ordinal)
                || relative.Contains("/bin/", StringComparison.Ordinal)
                || relative.Contains("/obj/", StringComparison.Ordinal)
                || relative.Contains("/dist/", StringComparison.Ordinal)
                || relative.Contains("/node_modules/", StringComparison.Ordinal))
            {
                continue;
            }

            yield return path;
        }
    }
}
