using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Babel.Ai.Workspace;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>لكلّ مسوّدةٍ شاشةٌ تهبط عليها — وكلُّ شاشةٍ تُسمّى موجودةٌ في الملاحة.</b>
/// <para>
/// وزرُّ «افتحها في شاشتها» في لوح الوكيل يفتح ما يعطيه إياه الخادم. فإن كان المسار
/// خطأً، أو غائباً، أو أُعيدت تسميته في الواجهة، <b>فتح الزرُّ لا شيء</b> — وهو أسوأ من
/// زرٍّ لا يُعرض: يُعلّم المستخدم ألّا يثق باللوح كلّه. وهذا الحارس يجعل ذلك مستحيلاً
/// من ثلاث جهات، وكلٌّ منها من مصدرٍ مختلف:
/// <list type="number">
///   <item><b>العقد المنشور</b>: كلّ عمليةٍ فعلُها <c>draft</c> لها صفٌّ في الخريطة.</item>
///   <item><b>الخريطة</b>: لا صفَّ لعمليةٍ لا ينشرها العقد — صفٌّ يبقى بعد حذف عمليةٍ
///         يجعل الجدول يقول ما ليس موجوداً.</item>
///   <item><b>الملاحة نفسها</b>: كلّ مسارٍ تسمّيه الخريطة موجودٌ حرفاً بحرف في
///         <c>web/src/app/shell/sections.ts</c> — وهو الملفّ الذي يقرؤه المتصفّح.</item>
/// </list>
/// </para>
/// <para>
/// <b>ولماذا يُقرأ ملفُّ الواجهة نصّاً:</b> لا سبيل من تجميعة .NET إلى جدول مسارات
/// TypeScript إلا قراءته. والبديل — ثقةٌ بأن الاثنين متّفقان — هو بالضبط ما ينحرف عند
/// أول إعادة تسمية، ولا يظهر إلا حين يضغط عميلٌ زرّاً.
/// </para>
/// </summary>
public sealed partial class EveryDraftOperationHasAScreenToLandOn
{
    /// <summary>صفُّ شاشةٍ في جدول الملاحة: <c>path: "/sales/invoice"</c>.</summary>
    [GeneratedRegex("path:\\s*\"(?<path>/[^\"]*)\"", RegexOptions.CultureInvariant)]
    private static partial Regex ScreenPath();

    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    private static string SectionsPath { get; } =
        Path.Combine(RepositoryLayout.Root, "web", "src", "app", "shell", "sections.ts");

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>معرّفات عمليات المسوّدات كما ينشرها العقد.</summary>
    private static List<string> PublishedDrafts()
    {
        List<string> found = [];
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));

        foreach (JsonProperty path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind == JsonValueKind.Object
                    && operation.Value.TryGetProperty("operationId", out JsonElement id)
                    && id.GetString() is string name
                    && name.StartsWith(AgentDraftConfirmationGate.PermittedVerb, StringComparison.Ordinal))
                {
                    found.Add(name);
                }
            }
        }

        found.Sort(StringComparer.Ordinal);
        return found;
    }

    /// <summary>مسارات الشاشات كما يعلنها جدول الملاحة في الواجهة.</summary>
    private static HashSet<string> NavigationPaths()
    {
        string text = File.ReadAllText(SectionsPath);

        return ScreenPath()
            .Matches(text)
            .Select(static match => match.Groups["path"].Value)
            .ToHashSet(StringComparer.Ordinal);
    }

    /// <summary><b>كلّ عملية مسوّدةٍ منشورة لها شاشة — ولا واحدة بلا.</b></summary>
    [Fact]
    public void كلُّ_عملية_مسوّدةٍ_منشورة_لها_شاشةٌ_مُعلَنة()
    {
        List<string> published = PublishedDrafts();

        // حارسٌ لا فراغ: عقدٌ لا يُقرأ منه شيء كان سيمرّ على لا شيء.
        Assert.True(published.Count >= 20, "عمليات المسوّدات المقروءة: " + Count(published.Count));

        List<string> orphans = [.. published.Where(static name => AgentDraftScreens.RouteFor(name) is null)];

        Assert.True(
            orphans.Count == 0,
            "عمليات مسوّدةٍ بلا شاشةٍ تهبط عليها: " + string.Join(" · ", orphans));
    }

    /// <summary>
    /// <b>ولا صفَّ زائداً في الخريطة.</b> صفٌّ لعمليةٍ حُذفت من العقد يبقى ساكناً ولا
    /// يقول شيئاً، ثمّ يُقرأ يوماً على أنه تغطية.
    /// </summary>
    [Fact]
    public void لا_صفَّ_في_الخريطة_لعمليةٍ_لا_ينشرها_العقد()
    {
        HashSet<string> published = [.. PublishedDrafts()];

        List<string> ghosts = [.. AgentDraftScreens.OperationIds.Where(name => !published.Contains(name))];

        Assert.True(ghosts.Count == 0, "صفوفٌ لعملياتٍ غير منشورة: " + string.Join(" · ", ghosts));
        Assert.Equal(published.Count, AgentDraftScreens.Count);
    }

    /// <summary>
    /// <b>وكلّ مسارٍ تسمّيه الخريطة شاشةٌ في الملاحة</b> — يُقرأ من ملفّ الواجهة نفسه،
    /// لا من قائمةٍ ثانية تُكتب هنا وتنحرف عنه.
    /// </summary>
    [Fact]
    public void كلُّ_مسارٍ_في_الخريطة_شاشةٌ_موجودة_في_الملاحة()
    {
        Assert.True(File.Exists(SectionsPath), "جدول الملاحة غير موجود: " + SectionsPath);

        HashSet<string> navigation = NavigationPaths();

        // شاهدٌ موجب على القراءة نفسها: جدولٌ لم يُقرأ منه شيء يجعل كل مسارٍ «مفقوداً»،
        // وجدولٌ يُقرأ منه سطرٌ واحد يجعل الفحص يمرّ على لا شيء.
        Assert.True(navigation.Count >= 20, "مسارات الملاحة المقروءة: " + Count(navigation.Count));

        List<string> dangling =
        [
            .. AgentDraftScreens.OperationIds
                .Select(static name => AgentDraftScreens.RouteFor(name)!)
                .Distinct(StringComparer.Ordinal)
                .Where(route => !navigation.Contains(route))
                .Order(StringComparer.Ordinal),
        ];

        Assert.True(
            dangling.Count == 0,
            "مساراتٌ تسمّيها الخريطة ولا وجود لها في الملاحة: " + string.Join(" · ", dangling));
    }

    /// <summary>
    /// <b>وعمليةٌ ليست في الخريطة لا يُخترع لها مسار.</b> الجواب <c>null</c>، ومنه
    /// يُبنى رفضٌ مُسمّى — لا مسارٌ افتراضي يفتح شاشةً خاطئة.
    /// </summary>
    [Theory]
    [InlineData("draftSomethingNobodyPublished")]
    [InlineData("postSalesInvoice")]
    [InlineData("")]
    public void ما_ليس_في_الخريطة_لا_يُخترع_له_مسار(string operationId) =>
        Assert.Null(AgentDraftScreens.RouteFor(operationId));
}
