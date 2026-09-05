using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>إجراءُ طرفٍ ثالث في سير عمل يُثبَّت على بصمة التزام، لا على وسمٍ متحرّك.</b>
/// <para>
/// <b>ما الخطر بالضبط:</b> <c>uses: docker/build-push-action@v6</c> ليس إصداراً بل
/// <b>مؤشّرٌ متحرّك</b> يملك مالكُ الإجراء أن يُعيد توجيهه إلى أي التزام في أي لحظة.
/// ووظيفةُ النشر في هذا المستودع تحمل <c>secrets.GITHUB_TOKEN</c> بصلاحية
/// <c>packages: write</c> وتدفع الصور التي يسحبها الخادم. فمن يملك ذلك الحساب —
/// أو من يستولي عليه — يُشغّل شيفرته داخل تلك الوظيفة <b>بلا سطرٍ يتغيّر هنا وبلا
/// مراجعةٍ واحدة</b>. والبصمة تُثبّت البايتات فيصير التحديث إيداعاً يُقرأ.
/// </para>
/// <para>
/// <b>وما استُثني، وقد قيل لماذا:</b> منظّمة <c>actions</c> مالكُها GitHub نفسه —
/// صاحبُ العدّاء ومُصدِرُ الرمز وحالُّ البصمة معاً — فتثبيتُها لا يضيف حدَّ ثقةٍ
/// جديداً: من يستطيع تبديل <c>actions/checkout</c> يستطيع تبديل العدّاء تحته.
/// <b>والحدّ الحقيقي هو المالك الآخر</b>، وهو ما يُفرَض هنا.
/// </para>
/// </summary>
public sealed partial class ThirdPartyActionsArePinnedToACommit
{
    /// <summary>مجلّد سيَر العمل.</summary>
    private const string WorkflowFolder = ".github/workflows";

    /// <summary>
    /// المالكون الذين لا تُطلب منهم بصمة — ومعهم سببُهم مكتوبٌ في صدر هذا الصنف.
    /// <b>وهي قائمةُ مالكين لا قائمةُ إجراءات</b>: إجراءٌ جديد من GitHub يدخل بلا
    /// تعديلٍ هنا، وإجراءٌ من مالكٍ آخر لا يدخل أبداً.
    /// </summary>
    private static readonly string[] FirstPartyOwners = ["actions", "github"];

    /// <summary>‏<c>uses: owner/repo@ref</c> — والإجراءات المحلّية (<c>./…</c>) ليست منها.</summary>
    [GeneratedRegex(@"^\s*-?\s*uses:\s*(?<owner>[A-Za-z0-9][A-Za-z0-9-]*)/(?<repo>[A-Za-z0-9._/-]+)@(?<ref>\S+)", RegexOptions.CultureInvariant | RegexOptions.Multiline)]
    private static partial Regex UsesLine();

    /// <summary>بصمةُ التزامٍ كاملة: أربعون محرفاً ست‌عشرياً صغيراً، لا أقلّ.</summary>
    [GeneratedRegex(@"^[0-9a-f]{40}$", RegexOptions.CultureInvariant)]
    private static partial Regex CommitSha();

    /// <summary>كل إجراءٍ من مالكٍ غير GitHub مثبَّتٌ على بصمة.</summary>
    [Fact]
    public void EveryActionFromAnotherOwnerIsPinnedToAFullCommitSha()
    {
        List<string> offenders = [];
        int thirdParty = 0;
        int firstParty = 0;

        foreach ((string file, Match use) in Uses())
        {
            string owner = use.Groups["owner"].Value;
            string reference = use.Groups["ref"].Value;

            if (FirstPartyOwners.Contains(owner, StringComparer.Ordinal))
            {
                firstParty++;
                continue;
            }

            thirdParty++;

            if (!CommitSha().IsMatch(reference))
            {
                offenders.Add(
                    file + ": " + owner + "/" + use.Groups["repo"].Value + "@" + reference
                    + " — وسمٌ متحرّك من مالكٍ آخر");
            }
        }

        // شاهدان موجبان: القارئ رأى إجراءات من الصنفين، فالخُضرة ليست خُضرة قائمةٍ فارغة.
        Assert.True(thirdParty >= 4, "لم يُقرأ إجراءُ طرفٍ ثالث واحد — النطاق أو النمط انكسر: " + thirdParty.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(firstParty >= 5, "لم تُقرأ إجراءات GitHub — النمط انكسر: " + firstParty.ToString(System.Globalization.CultureInfo.InvariantCulture));

        Assert.True(
            offenders.Count == 0,
            "إجراءُ طرفٍ ثالث على وسمٍ متحرّك:\n" + string.Join('\n', offenders.Order(StringComparer.Ordinal))
            + "\n\nوالوسم يُعاد توجيهه بلا سطرٍ يتغيّر هنا وبلا مراجعة، والوظيفة التي يعمل\n"
            + "فيها تحمل أسراراً. ثبّته على بصمة التزامٍ كاملة، واترك الوسم تعليقاً للقارئ.");
    }

    /// <summary>
    /// <b>الشاهد الموجب على النمطين:</b> يُثبَت أن قارئ <c>uses</c> يلتقط الشكلين،
    /// وأن فحص البصمة يرفض الوسم ويقبل البصمة — على عيّناتٍ مُركَّبة هنا.
    /// </summary>
    [Fact]
    public void ThePatternsActuallyTellATagFromACommit()
    {
        Match tagged = UsesLine().Match("      - uses: docker/build-push-action@v6\n");
        Assert.True(tagged.Success);
        Assert.Equal("docker", tagged.Groups["owner"].Value);
        Assert.DoesNotMatch(CommitSha(), tagged.Groups["ref"].Value);

        Match pinned = UsesLine().Match(
            "        uses: docker/login-action@c94ce9fb468520275223c153574b00df6fe4bcc9  # v3.7.0\n");
        Assert.True(pinned.Success);
        Assert.Matches(CommitSha(), pinned.Groups["ref"].Value);

        // وبصمةٌ مقصوصة ليست بصمة: `@c94ce9f` يقبله GitHub ويُحلّ إلى المرجع الأطول.
        Assert.DoesNotMatch(CommitSha(), "c94ce9f");
    }

    private static IEnumerable<(string File, Match Use)> Uses()
    {
        string folder = Path.Combine(RepositoryLayout.Root, WorkflowFolder);

        foreach (string path in Directory.EnumerateFiles(folder, "*.yml", SearchOption.TopDirectoryOnly)
            .Order(StringComparer.Ordinal))
        {
            string relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');

            foreach (Match match in UsesLine().Matches(File.ReadAllText(path)))
            {
                yield return (relative, match);
            }
        }
    }
}
