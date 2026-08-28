using System.Text.Json;
using System.Text.RegularExpressions;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>كل رمزٍ في قالب المسار له وسيطٌ مُعلَن، ووصفُه يخصّ مورده هو.</b>
/// <para>
/// <b>ولماذا وُجد هذا الحارس:</b> كانت أوصاف وسائط المسار تُقرأ من قائمة <b>مفتاحها الاسم
/// وحده</b>، وذلك يفترض أن الاسم فريدٌ عبر الموارد — وليس كذلك. فـ<c>receiptId</c> يخدم
/// موردين: <c>goods-receipts</c> و<c>customer-receipts</c>، فنُشر في العقد أنّ وسيط
/// <b>سند القبض</b> هو «معرّف استلام البضاعة»؛ و<c>paymentId</c> غاب عن القائمة فخرج
/// مساران بلا وسيطٍ مُعلَن أصلاً. مقيس على العقد المنشور وقتها: <b>عمليتان موصوفتان
/// خطأً وعمليتان بلا وسيط</b>.
/// </para>
/// <para>
/// <b>وتعليقُ القائمة كان يقول إنها تمنع ذلك بعينه</b> — وهو الدرس: الوصفُ في تعليقٍ ليس
/// حارساً، لأنه لا يُفشل شيئاً. فما يحرسه البناء وحده يجب أن يُبنى.
/// </para>
/// </summary>
public sealed class EveryPathTemplateDeclaresItsParameter
{
    private static readonly Regex Token = new(@"\{(?<name>\w+)\}", RegexOptions.CultureInvariant);

    [Fact]
    public void NoPublishedPathCarriesATemplateTokenWithoutADeclaredParameter()
    {
        using JsonDocument contract = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepositoryPaths.Root, "contracts", "openapi", "v1.json")));
        JsonElement paths = contract.RootElement.GetProperty("paths");

        List<string> offenders = [];
        int scanned = 0;

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            HashSet<string> template =
                [.. Token.Matches(path.Name).Select(static m => m.Groups["name"].Value)];

            if (template.Count == 0)
            {
                continue;
            }

            scanned++;

            HashSet<string> declared = [];
            if (path.Value.TryGetProperty("parameters", out JsonElement parameters))
            {
                foreach (JsonElement parameter in parameters.EnumerateArray())
                {
                    if (parameter.TryGetProperty("in", out JsonElement wherein)
                        && wherein.GetString() == "path"
                        && parameter.TryGetProperty("name", out JsonElement name))
                    {
                        declared.Add(name.GetString() ?? string.Empty);
                    }
                }
            }

            foreach (string missing in template.Except(declared).Order(StringComparer.Ordinal))
            {
                offenders.Add($"{path.Name}: الرمز «{{{missing}}}» بلا وسيطٍ مُعلَن");
            }
        }

        Assert.True(
            scanned >= 20,
            $"المسارات ذات القوالب المفحوصة {scanned} — أقلّ من أن يكون الفحص ذا معنى. "
            + "حارسٌ لا يفحص شيئاً يمرّ دائماً.");

        Assert.True(
            offenders.Count == 0,
            "مسارٌ منشور فيه رمزٌ بلا وسيطٍ مُعلَن — والعميل المولَّد لا يعرف كيف يملؤه:\n"
            + string.Join('\n', offenders));
    }

    [Fact]
    public void EveryPathParameterDescriptionNamesItsOwnResource()
    {
        // الوصف يخصّ المورد لا الاسم: مساران مختلفان يتقاسمان اسم رمزٍ واحد يجب أن
        // يحملا وصفين مختلفين. والشاهد الذي أوقع الخطأ أولاً: goods-receipts
        // وcustomer-receipts كلاهما {receiptId}.
        using JsonDocument contract = JsonDocument.Parse(File.ReadAllBytes(Path.Combine(RepositoryPaths.Root, "contracts", "openapi", "v1.json")));
        JsonElement paths = contract.RootElement.GetProperty("paths");

        Dictionary<string, HashSet<string>> byResourceAndToken = [];

        foreach (JsonProperty path in paths.EnumerateObject())
        {
            if (!path.Value.TryGetProperty("parameters", out JsonElement parameters))
            {
                continue;
            }

            string[] segments = path.Name.Split('/', StringSplitOptions.RemoveEmptyEntries);

            foreach (JsonElement parameter in parameters.EnumerateArray())
            {
                if (!parameter.TryGetProperty("in", out JsonElement wherein)
                    || wherein.GetString() != "path"
                    || !parameter.TryGetProperty("name", out JsonElement name))
                {
                    continue;
                }

                string token = name.GetString() ?? string.Empty;
                if (token == "companyId")
                {
                    continue;
                }

                int at = Array.IndexOf(segments, "{" + token + "}");
                string resource = at > 0 ? segments[at - 1] : path.Name;
                string description = parameter.TryGetProperty("description", out JsonElement d)
                    ? d.GetString() ?? string.Empty
                    : string.Empty;

                (byResourceAndToken.TryGetValue(resource + " " + token, out HashSet<string>? seen)
                    ? seen
                    : byResourceAndToken[resource + " " + token] = []).Add(description);
            }
        }

        // موردان مختلفان يتقاسمان رمزاً واحداً ⇒ وصفان مختلفان. والعكس عطلٌ حقيقي وقع.
        List<string> collisions =
        [
            .. byResourceAndToken
                .GroupBy(static pair => pair.Key.Split(' ')[1], StringComparer.Ordinal)
                .Where(static group => group.Count() > 1)
                .Where(static group => group
                    .SelectMany(static pair => pair.Value)
                    .Distinct(StringComparer.Ordinal)
                    .Count() == 1)
                .Select(static group =>
                    $"الرمز «{group.Key}» يخدم {group.Count()} موارد بوصفٍ واحد: "
                    + string.Join(" · ", group.Select(static pair => pair.Key.Split(' ')[0]))),
        ];

        Assert.True(
            collisions.Count == 0,
            "رمزُ مسارٍ واحد على موردين بوصفٍ واحد — فأحدهما موصوفٌ بوصف الآخر:\n"
            + string.Join('\n', collisions));
    }
}
