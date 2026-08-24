using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// <b>العقد المنشور: مُودَع، ومطابق، وحتمي.</b>
/// <para>
/// حارس الانحراف بلا حتمية <b>أسوأ من غيابه</b>: يُفشل البناء على تغييرات لم تقع، فيتعلّم
/// الفريق تجاهله، ثم يمرّ التغيير الحقيقي وسط ضجيجه. ولذلك الحتمية تُثبَت هنا أولاً —
/// بتكرار التوليد تحت ثقافات مختلفة ومع تعطيل بيانات المناطق وبدونه — قبل أن يُوثق
/// بالمقارنة أصلاً.
/// </para>
/// </summary>
public sealed class PublishedContractTests
{
    private static string CommittedPath => Path.Combine(RepositoryPaths.Root, "contracts", "openapi", "v1.json");

    [Fact]
    public void العقد_مُودَع_في_المستودع_ولا_يُبنى_من_ذاكرة_أحد()
    {
        Assert.True(File.Exists(CommittedPath), $"العقد المنشور غير موجود عند {CommittedPath}.");
        Assert.True(new FileInfo(CommittedPath).Length > 10_000, "العقد المُودَع أصغر من أن يصف سطحاً.");
    }

    [Fact]
    public async Task العقد_المُودَع_يطابق_المُولَّد_بايتاً_بايت()
    {
        string temporary = Path.Combine(Path.GetTempPath(), "babel-openapi-" + Guid.CreateVersion7().ToString("N") + ".json");

        try
        {
            await GenerateAsync(temporary, culture: "en_US.UTF-8", invariantGlobalization: false);

            byte[] generated = await Http.ReadBytesAsync(temporary);
            byte[] committed = await Http.ReadBytesAsync(CommittedPath);

            Console.WriteLine($"مُولَّد : {generated.Length} بايت · {Hash(generated)}");
            Console.WriteLine($"مُودَع  : {committed.Length} بايت · {Hash(committed)}");

            Assert.True(
                generated.AsSpan().SequenceEqual(committed),
                "العقد المُودَع في contracts/openapi/v1.json لا يطابق ما يولّده السطح الآن.\n"
                + "أعِد توليده بالأمر التالي وأودِع الناتج في نفس طلب الدمج الذي غيّر السطح:\n"
                + "  dotnet src/Babel.Api/bin/Release/net10.0/Babel.Api.dll --emit-openapi contracts/openapi/v1.json\n"
                + "وإن كان التغيير يحذف حقلاً أو يعيد تسميته أو يضيّق نوعاً، فهو v2 لا v1 — راجع سياسة الإصدار في ADR-0018.\n"
                + FormattableString.Invariant($"بصمة المُولَّد: {Hash(generated)}\nبصمة المُودَع : {Hash(committed)}"));
        }
        finally
        {
            File.Delete(temporary);
        }
    }

    [Fact]
    public async Task التوليد_حتمي_عبر_الثقافات_ومع_تعطيل_بيانات_المناطق_وبدونه()
    {
        (string Culture, bool Invariant)[] runs =
        [
            ("en_US.UTF-8", false),
            ("en_US.UTF-8", false),
            ("ar_SA.UTF-8", false),
            ("tr_TR.UTF-8", false),
            ("hi_IN.UTF-8", false),
            ("de_DE.UTF-8", false),
            ("ar_SA.UTF-8", true),
            ("tr_TR.UTF-8", true),
        ];

        List<string> hashes = [];

        foreach ((string culture, bool invariant) in runs)
        {
            string temporary = Path.Combine(
                Path.GetTempPath(), "babel-openapi-" + Guid.CreateVersion7().ToString("N") + ".json");

            try
            {
                await GenerateAsync(temporary, culture, invariant);
                string hash = Hash(await Http.ReadBytesAsync(temporary));
                hashes.Add(hash);
                Console.WriteLine($"{culture,-14} invariant={invariant,-5} → {hash}");
            }
            finally
            {
                File.Delete(temporary);
            }
        }

        Assert.Equal(8, hashes.Count);
        Assert.Single(hashes.Distinct(StringComparer.Ordinal));
        Assert.Equal(Hash(await Http.ReadBytesAsync(CommittedPath)), hashes[0]);
    }

    [Fact]
    public async Task العقد_يصف_كل_ما_يحتاجه_فريق_الواجهة_ولا_يصف_باباً_غير_موجود()
    {
        using JsonDocument document = JsonDocument.Parse(await Http.ReadTextAsync(CommittedPath));
        JsonElement root = document.RootElement;

        Assert.Equal("3.1.0", root.GetProperty("openapi").GetString());
        Assert.Equal("v1", root.GetProperty("info").GetProperty("version").GetString());

        // سياسة الإصدار مكتوبة **داخل العقد نفسه**: من يقرأ الوثيقة يعرف ما يكسره وما لا يكسره.
        string description = root.GetProperty("info").GetProperty("description").GetString()!;
        Assert.Contains("v2", description, StringComparison.Ordinal);
        Assert.Contains("Forces v2", description, StringComparison.Ordinal);

        JsonElement paths = root.GetProperty("paths");
        Assert.Equal(6, paths.EnumerateObject().SelectMany(static p => p.Value.EnumerateObject())
            .Count(static o => o.Name is "get" or "post" or "put" or "patch" or "delete"));

        // النطاق في كل مسار مُصدَّر، ولا مسار أعمال خارج شركة.
        foreach (JsonProperty path in paths.EnumerateObject())
        {
            Assert.True(
                path.Name == "/health" || path.Name.StartsWith("/api/v1/companies/{companyId}", StringComparison.Ordinal),
                $"مسار خارج نطاق الشركة: {path.Name}");
        }

        // نمط المال معلن في العقد: من يقرأ الوثيقة يعرف النحو بلا سؤال.
        string moneyPattern = root.GetProperty("components").GetProperty("schemas")
            .GetProperty("Money").GetProperty("pattern").GetString()!;
        Assert.Equal(@"^-?(0|[1-9][0-9]*)(\.[0-9]{1,4})?$", moneyPattern);

        // ولا حقل حساب في أي مخطّط طلب: القاعدة 2 مُعلنة على السلك أيضاً.
        string requestSchemas = root.GetProperty("components").GetProperty("schemas")
            .GetProperty("PostingLine").GetRawText();
        Assert.DoesNotContain("accountCode", requestSchemas, StringComparison.OrdinalIgnoreCase);
    }

    private static async Task GenerateAsync(string target, string culture, bool invariantGlobalization)
    {
        string executable = RepositoryPaths.ApiExecutable;
        Assert.True(File.Exists(executable), $"ثنائي الخادم غير موجود عند {executable} — ابنِ الحل أولاً.");

        ProcessStartInfo start = new(executable)
        {
            WorkingDirectory = RepositoryPaths.Root,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };

        start.ArgumentList.Add("--emit-openapi");
        start.ArgumentList.Add(target);
        start.Environment["LANG"] = culture;
        start.Environment["LC_ALL"] = culture;

        if (invariantGlobalization)
        {
            start.Environment["DOTNET_SYSTEM_GLOBALIZATION_INVARIANT"] = "1";
        }

        using Process process = Process.Start(start)!;
        string output = await process.StandardOutput.ReadToEndAsync();
        string error = await process.StandardError.ReadToEndAsync();
        await process.WaitForExitAsync();

        Assert.True(
            process.ExitCode == 0,
            FormattableString.Invariant($"فشل توليد العقد ({culture}, invariant={invariantGlobalization}) برمز {process.ExitCode}:\n{output}\n{error}"));
    }

    private static string Hash(byte[] bytes) =>
        Convert.ToHexStringLower(SHA256.HashData(bytes));
}
