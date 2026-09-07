using System.Text.RegularExpressions;
using Babel.ArchitectureTests.Support;
using Babel.Core;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>حدودُ ما يكتبه إنسان مصدرُها واحد، والواجهةُ تقرؤها من العقد (ADR-0091).</b>
/// <para>
/// <b>العطل الذي وقع فعلاً:</b> <c>MaximumReasonLength = 512</c> في نوعين، وعمودُ
/// <c>suspension_reason</c> بطول 400، و<c>const MINIMUM_REASON = 8</c> في شاشتين. فسببٌ من
/// 401 محرفاً يمرّ من النوع ويسقط في القاعدة، ورقمُ الشاشة يفترق عن رقم الخادم يوم يتغيّر
/// أحدهما. والحارسُ هنا يمنع الشكلين: رقمٌ حرفيّ لحدٍّ مُسمّى خارج <see cref="InputLimits"/>،
/// وحدٌّ أدنى مكتوبٌ بيدٍ في الواجهة.
/// </para>
/// </summary>
public sealed partial class InputLimitsHaveOneSource
{
    private const string TheOneSource = "src/Babel.Core/InputLimits.cs";

    /// <summary>حدٌّ مُسمّى يُسنَد رقماً حرفياً — لا مرجعاً إلى المصدر الواحد.</summary>
    [GeneratedRegex(@"\b(Maximum|Minimum)(Name|Reason)Length\s*=\s*[0-9]+\s*;", RegexOptions.CultureInvariant)]
    private static partial Regex NamedLimitAssignedALiteral();

    /// <summary>الحدّ الأدنى للسبب مكتوباً بيدٍ في الواجهة.</summary>
    [GeneratedRegex(@"MINIMUM_REASON\s*=\s*[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex FrontEndMinimumReasonLiteral();

    [Fact]
    public void لا_حدَّ_مُسمّى_يُسنَد_رقماً_خارج_المصدر_الواحد()
    {
        Assert.Matches(NamedLimitAssignedALiteral(), "    public const int MaximumReasonLength = 512;");
        Assert.Matches(NamedLimitAssignedALiteral(), "public const int MinimumReasonLength=8;");
        Assert.DoesNotMatch(NamedLimitAssignedALiteral(), "public const int MaximumReasonLength = InputLimits.MaximumReasonLength;");

        List<string> offenders = [];
        int visited = 0;

        foreach (string path in Directory.EnumerateFiles(Path.Combine(RepositoryLayout.Root, "src"), "*.cs", SearchOption.AllDirectories)
                     .Where(static p => !p.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}", StringComparison.Ordinal)))
        {
            visited++;
            string relative = Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/');
            if (relative == TheOneSource)
            {
                continue;
            }

            if (NamedLimitAssignedALiteral().IsMatch(File.ReadAllText(path)))
            {
                offenders.Add(relative);
            }
        }

        Assert.True(visited > 100, "المسح لم يزر ملفّات: " + visited);
        Assert.True(offenders.Count == 0,
            "حدُّ إدخالٍ مُسمّى أُسند رقماً خارج " + TheOneSource + " — أحِل إلى InputLimits (ADR-0091):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void الواجهةُ_لا_تكتب_أدنى_طول_السبب_بيدها()
    {
        Assert.Matches(FrontEndMinimumReasonLiteral(), "const MINIMUM_REASON = 8;");

        List<string> offenders = [];
        int visited = 0;

        foreach (string path in Directory.EnumerateFiles(Path.Combine(RepositoryLayout.Root, "web", "src"), "*.tsx", SearchOption.AllDirectories))
        {
            visited++;
            if (FrontEndMinimumReasonLiteral().IsMatch(File.ReadAllText(path)))
            {
                offenders.Add(Path.GetRelativePath(RepositoryLayout.Root, path).Replace('\\', '/'));
            }
        }

        Assert.True(visited > 50, "المسح لم يزر شاشات: " + visited);
        Assert.True(offenders.Count == 0,
            "شاشةٌ تكتب أدنى طول السبب رقماً — يُقرأ من العقد عبر minLengthOf (ADR-0091):\n" + string.Join("\n", offenders));
    }

    [Fact]
    public void العقدُ_المنشور_يعلن_الحدَّ_الأدنى_للسبب_بقيمة_المصدر_الواحد()
    {
        using System.Text.Json.JsonDocument contract = System.Text.Json.JsonDocument.Parse(
            File.ReadAllText(Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json")));
        System.Text.Json.JsonElement schemas = contract.RootElement.GetProperty("components").GetProperty("schemas");

        System.Text.Json.JsonElement reason = schemas.GetProperty("SuspendCostCenterRequest").GetProperty("properties").GetProperty("reason");
        Assert.Equal(InputLimits.MinimumReasonLength, reason.GetProperty("minLength").GetInt32());
        Assert.Equal(InputLimits.MaximumReasonLength, reason.GetProperty("maxLength").GetInt32());

        System.Text.Json.JsonElement withdrawal = schemas.GetProperty("PutCapabilityProfileRequest").GetProperty("properties").GetProperty("withdrawalReason");
        Assert.Equal(InputLimits.MinimumReasonLength, withdrawal.GetProperty("minLength").GetInt32());
    }
}
