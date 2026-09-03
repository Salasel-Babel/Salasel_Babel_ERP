using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Babel.Ai.Agent;
using Babel.Ai.Workspace;
using Babel.ArchitectureTests.Support;
using Babel.SharedKernel;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>مسار الوكيل ينتهي عند المسوّدة — مفروضاً بالبناء لا مكتوباً في تعليق.</b>
/// <para>
/// <c>NoAgentToolReachesAPostingOperation</c> يحرس <b>ما يُعرَض على النموذج</b>: كتالوجُ
/// الأدوات. وهذا الحارس يحرس <b>ما يُنفَّذ</b> و<b>ما يُنشَر</b>: آخرَ بابٍ قبل أن تهبط
/// مسوّدة (<see cref="AgentDraftConfirmationGate"/>)، والسطحَ المنشور لمساحة العمل،
/// ومفرداتِ الحال التي تخرج منه.
/// </para>
/// <para>
/// <b>ولماذا حارسٌ ثانٍ لا اكتفاءٌ بالأول:</b> الكتالوج يُبنى من العقد ويُرشَّح عند
/// التركيب — فهو يحرس <b>النموذج</b>. ومنفّذٌ يُركَّب غداً ويقبل ما يُعطى، أو سطحٌ
/// يُنشر غداً بمورد <c>…/posting</c> تحت <c>/agent/</c>، لا يمرّان بالكتالوج أصلاً.
/// <b>وثقبٌ لا يمرّ بالحارس ليس محروساً بأربع طبقات؛ هو غير محروس.</b>
/// </para>
/// <para>
/// <b>وستّة تُقاس، كلٌّ منها من مصدرٍ مختلف:</b> الباب الأخير على كل عملياتِ العقد ·
/// مسارات <c>/agent/</c> المنشورة · مخطّطات السطح · مفردات الحال · مصدر نقاط النهاية ·
/// وأنّ العمليات المسموح بها ليست فارغة (وإلّا مرّ كل شيء على لا شيء).
/// </para>
/// </summary>
public sealed partial class TheAgentSurfaceEndsAtTheDraft
{
    /// <summary>مقطعُ بابِ الترحيل في مسارٍ منشور.</summary>
    [GeneratedRegex("/posting(?![-\\w])", RegexOptions.CultureInvariant)]
    private static partial Regex PostingSegment();

    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    /// <summary>العمليات المنشورة: المعرّف ← المسار.</summary>
    private static Dictionary<string, string> Operations()
    {
        Dictionary<string, string> found = new(StringComparer.Ordinal);
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));

        foreach (JsonProperty path in document.RootElement.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty operation in path.Value.EnumerateObject())
            {
                if (operation.Value.ValueKind == JsonValueKind.Object
                    && operation.Value.TryGetProperty("operationId", out JsonElement id)
                    && id.GetString() is string name)
                {
                    found[name] = path.Name;
                }
            }
        }

        return found;
    }

    /// <summary>
    /// <b>الباب الأخير يرفض كل ما ليس مسوّدة — مقيساً على العقد المنشور كلّه.</b>
    /// <para>
    /// ولا قائمةَ عملياتٍ مكتوبةٌ هنا: تُقرأ العمليات من العقد نفسه، فعمليةٌ تُنشر غداً
    /// تدخل هذا الفحص من نفسها.
    /// </para>
    /// </summary>
    [Fact]
    public void البابُ_الأخير_يرفض_كل_عمليةٍ_ليست_مسوّدة()
    {
        Dictionary<string, string> operations = Operations();

        Assert.True(operations.Count >= 150, "العمليات المقروءة: " + Count(operations.Count));

        int drafts = 0;
        int refused = 0;

        foreach ((string operationId, string path) in operations)
        {
            Error? verdict = AgentDraftConfirmationGate.Refuse(operationId, path);

            if (operationId.StartsWith("draft", StringComparison.Ordinal)
                && !PostingSegment().IsMatch(path))
            {
                drafts++;
                Assert.Null(verdict);
                continue;
            }

            refused++;
            Assert.NotNull(verdict);
        }

        Assert.True(drafts >= 20, "عمليات المسوّدات المقروءة: " + Count(drafts));
        Assert.True(refused >= 100, "العمليات المرفوضة: " + Count(refused));

        // ‏**وأداةُ بروتوكولٍ بلا معرّف عملية تُرفض كذلك**: المنفّذ لا يُسلَّم إليه إلا
        // عمليةٌ منشورة، ونداءُ بروتوكولٍ يبلغه يعني أن الحلقة تسرّبت.
        Assert.NotNull(AgentDraftConfirmationGate.Refuse(null, null));
    }

    /// <summary>
    /// <b>ومسارٌ لا يُعكَس يُرفض ولو كان فعلُه <c>draft</c></b> — يُقرأ من المسار لا من
    /// الاسم، فعمليةٌ تُسمّى غداً <c>draftX</c> ومسارُها «…/posting» تبقى ممنوعة.
    /// </summary>
    [Theory]
    [InlineData("draftAnything", "/api/v1/companies/{companyId}/sales-invoices/{invoiceId}/posting")]
    [InlineData("draftAnything", "/api/v1/companies/{companyId}/x/posting/confirm")]
    [InlineData("draftAnything", "/api/v1/companies/{companyId}/lease-contracts/{leaseId}/activation")]
    [InlineData("draftAnything", "/api/v1/companies/{companyId}/employees/{employeeId}/termination")]
    [InlineData("postSalesInvoice", "/api/v1/companies/{companyId}/sales-invoices")]
    [InlineData("addCustomer", "/api/v1/companies/{companyId}/customers")]
    public void الفعلُ_والمسار_يُفحصان_معاً_ويكفي_أحدهما_للرفض(string operationId, string path) =>
        Assert.NotNull(AgentDraftConfirmationGate.Refuse(operationId, path));

    /// <summary>
    /// <b>ولا مورد ترحيلٍ واحد تحت <c>/agent/</c> في العقد المنشور</b> — ولا عمليةَ
    /// <c>post…</c> واحدة على مساراته.
    /// </summary>
    [Fact]
    public void لا_بابَ_ترحيلٍ_تحت_مسارات_الوكيل()
    {
        Dictionary<string, string> operations = Operations();

        List<KeyValuePair<string, string>> agent = [.. operations
            .Where(static entry => entry.Value.Contains("/agent/", StringComparison.Ordinal)
                                || entry.Value.EndsWith("/agent", StringComparison.Ordinal))
            .OrderBy(static entry => entry.Key, StringComparer.Ordinal)];

        // حارسٌ لا فراغ: سطحٌ ضامر يمرّ على كل شيء.
        Assert.True(agent.Count >= 7, "أبواب الوكيل المقروءة: " + Count(agent.Count));

        foreach ((string operationId, string path) in agent)
        {
            Assert.Null(AgentToolCatalogue.IrreversibleSegmentIn(path));
            Assert.False(
                operationId.StartsWith("post", StringComparison.Ordinal),
                "بابٌ تحت /agent/ فعلُه ترحيل: " + operationId);
        }
    }

    /// <summary>
    /// <b>ولا قيمةَ اسمها <c>posted</c> في مفردات الحال المنشورة</b> — فالعقد
    /// <b>عاجزٌ</b> عن وصف ترحيلٍ في هذا المسار، لا ساكتٌ عنه.
    /// </summary>
    [Fact]
    public void لا_حالةَ_ترحيلٍ_في_مفردات_السطح()
    {
        foreach (string member in Enum.GetNames<AgentStepState>())
        {
            Assert.False(
                member.Contains("Post", StringComparison.OrdinalIgnoreCase),
                "حالُ خطوةٍ اسمه «" + member + "».");
        }

        foreach (string member in Enum.GetNames<AgentTurnPhase>())
        {
            Assert.False(
                member.Contains("Post", StringComparison.OrdinalIgnoreCase),
                "طورُ دورٍ اسمه «" + member + "».");
        }

        // والمفردة المنشورة نفسها تُقرأ من العقد لا من التعداد وحده.
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));

        JsonElement schemas = document.RootElement.GetProperty("components").GetProperty("schemas");
        JsonElement state = schemas.GetProperty("AgentPlanStep").GetProperty("properties")
            .GetProperty("state").GetProperty("enum");

        List<string> members = [.. state.EnumerateArray().Select(static value => value.GetString()!)];

        Assert.Equal(Enum.GetNames<AgentStepState>().Length, members.Count);
        Assert.DoesNotContain("posted", members, StringComparer.OrdinalIgnoreCase);
        Assert.Contains("landed", members, StringComparer.Ordinal);
    }

    /// <summary>
    /// <b>ولا اسمَ عملية ترحيلٍ في مصدر سطح الوكيل ولا في مساحة العمل.</b>
    /// <para>
    /// والاسم المكتوب بيدٍ في ملفّ هو الطريق الذي لا يراه حارسُ الكتالوج ولا حارسُ
    /// العقد: سطرٌ واحد يُركّب مسار <c>…/posting</c> ويُرسله لا يمرّ بأي فحصٍ آخر.
    /// </para>
    /// </summary>
    [Fact]
    public void لا_اسمَ_ترحيلٍ_في_مصدر_سطح_الوكيل()
    {
        string[] posting = [.. Operations().Keys
            .Where(static name => name.StartsWith("post", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

        Assert.True(posting.Length >= 20, "أسماء الترحيل المقروءة: " + Count(posting.Length));

        // ‏**والمجلّد يُعدّ لا يُسمّى ملفّاً ملفّاً.** كانت هنا أربعةُ مسارات مكتوبة
        // بيد، وأحدُها في `src/Babel.Api/Agent/`؛ فملفٌّ خامس يُضاف إلى ذلك المجلّد —
        // وهو حيث نزل منفّذُ المسوّدات وجدولُ هويّات أصحاب الجلسات — كان يخرج من هذا
        // الفحص بلا أن يقول أحدٌ شيئاً. والمجلّد كلّه يُعدّ الآن، ويُتبَع بشاهدٍ موجب
        // على أنّ العدّ وجد ما يعدّه.
        string desk = Path.Combine(RepositoryLayout.Root, "src", "Babel.Api", "Agent");

        string[] files =
        [
            Path.Combine(RepositoryLayout.Root, "src", "Babel.Api", "Endpoints", "AgentEndpoints.cs"),
            Path.Combine(RepositoryLayout.Root, "src", "Babel.Api", "Endpoints", "AgentRoutes.cs"),
            Path.Combine(RepositoryLayout.Root, "src", "Babel.Api", "Wire", "AgentContracts.cs"),
            .. Directory.EnumerateFiles(desk, "*.cs", SearchOption.AllDirectories).Order(StringComparer.Ordinal),
        ];

        Assert.True(files.Length >= 6, "ملفّات سطح الوكيل المفحوصة: " + Count(files.Length));

        foreach (string file in files)
        {
            Assert.True(File.Exists(file), "ملفٌّ محروسٌ غير موجود: " + file);
            string text = File.ReadAllText(file);

            foreach (string name in posting)
            {
                Assert.False(
                    text.Contains(name, StringComparison.Ordinal),
                    Path.GetFileName(file) + " يسمّي «" + name + "».");
            }

            Assert.False(
                PostingSegment().IsMatch(text),
                Path.GetFileName(file) + " يحمل مقطع «/posting».");

            Assert.DoesNotContain("IPostingService", text, StringComparison.Ordinal);
        }

        // ‏**والمساحة نفسها كذلك، عدا الملفّ الذي يُعرّف المنع** — ويُتبَع بشاهدٍ موجب.
        string workspace = Path.Combine(RepositoryLayout.Root, "src", "Babel.Ai", "Workspace");
        string defining = Path.Combine(workspace, "AgentDraftConfirmationGate.cs");

        int scanned = 0;

        foreach (string file in Directory.EnumerateFiles(workspace, "*.cs", SearchOption.AllDirectories))
        {
            if (string.Equals(file, defining, StringComparison.Ordinal))
            {
                continue;
            }

            scanned++;
            string text = File.ReadAllText(file);

            foreach (string name in posting)
            {
                Assert.False(
                    text.Contains(name, StringComparison.Ordinal),
                    Path.GetFileName(file) + " يسمّي «" + name + "».");
            }
        }

        Assert.True(scanned >= 5, "ملفّات المساحة المفحوصة: " + Count(scanned));

        // الشاهد الموجب على الاستثناء: المستثنى يعرّف المنع فعلاً.
        Assert.Contains("PermittedVerb", File.ReadAllText(defining), StringComparison.Ordinal);
        Assert.Equal("draft", AgentDraftConfirmationGate.PermittedVerb);
    }
}
