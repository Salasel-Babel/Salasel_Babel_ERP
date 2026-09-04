using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Babel.Ai.Agent;
using Babel.Ai.Voice;
using Babel.ArchitectureTests.Support;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>لا أداةَ وكيلٍ تبلغ ترحيلاً — مقيساً على العقد المنشور نفسه، لا على قائمة أدوات.</b>
/// <para>
/// شقيقُ <see cref="NoVoiceIntentReachesAPostingOperation"/> على المسار الثاني، وبالمنهج
/// نفسه: <b>ثلاثة مصادر تُقرأ ويُقاس بينها</b> —
/// </para>
/// <list type="number">
///   <item><b>الكتالوج المضمَّن</b> كما يراه النموذج فعلاً — لا كما يصفه تعليق.</item>
///   <item><c>contracts/openapi/v1.json</c> — كل أداةٍ عمليةٌ موجودة فيه، ومسارُها ليس
///         باب أثرٍ لا يُعكَس. <b>ويُقرأ من المسار لا من الاسم</b>: عمليةٌ تُسمّى غداً
///         بأيّ اسمٍ ومسارُها «…/posting» تبقى ممنوعة.</item>
///   <item><b>مصادر الخادم ومرآة المتصفّح</b> — لا اسمَ عملية ترحيلٍ في أيٍّ منهما،
///         لأن الاسم المكتوب بيدٍ في ملفّ هو الطريق الذي لا يراه حارسُ الكتالوج.</item>
/// </list>
/// <para>
/// <b>وثلاث طبقاتٍ خلف هذا الحارس، وكلٌّ منها كافية وحدها:</b>
/// ‏(١) الكتالوج يُرشَّح عند التركيب بـ<c>VoiceOperationGuard.Permits</c> ويُسقط التركيب
/// إن بقي فيه ما ترفضه البوّابة؛ ‏(٢) <c>AgentToolGate</c> يُعيد الفحص قبل كل تنفيذ؛
/// ‏(٣) وكلّ ما يُنتَج مسوّدة، والترحيل فعلٌ بصريّ يدويّ على الشاشة.
/// </para>
/// </summary>
public sealed partial class NoAgentToolReachesAPostingOperation
{
    /// <summary>مقطعُ بابِ الترحيل في مسارٍ منشور — لا «data/posting-matrix» في تعليق.</summary>
    [GeneratedRegex("/posting(?![-\\w])", RegexOptions.CultureInvariant)]
    private static partial Regex PostingSegment();

    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    private static string CataloguePath { get; } =
        Path.Combine(RepositoryLayout.Root, "data", "agent", "tool-catalogue.json");

    private static AgentToolCatalogue Catalogue { get; } = AgentToolCatalogue.Embedded;

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

    /// <summary>حارسٌ لا فراغ (فخ-43): عقدٌ أو كتالوجٌ ضامر يمرّ على كل شيء.</summary>
    [Fact]
    public void العقد_المنشور_ليس_ضامراً_ولا_الكتالوج()
    {
        Assert.True(Operations().Count >= 150, "العمليات المقروءة: " + Count(Operations().Count));
        Assert.True(Catalogue.Tools.Count >= 20, "الأدوات المقروءة: " + Count(Catalogue.Tools.Count));
    }

    /// <summary>الكتالوج المضمَّن هو الملفّ المُودَع، والملفّ مُولَّدٌ من العقد على القرص.</summary>
    [Fact]
    public void الكتالوج_المضمَّن_هو_المُودَع_وبصمتُه_بصمةُ_العقد()
    {
        using JsonDocument onDisk = JsonDocument.Parse(File.ReadAllText(CataloguePath));

        Assert.Equal(
            Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ContractPath))),
            onDisk.RootElement.GetProperty("contractSha256").GetString());

        Assert.Equal(onDisk.RootElement.GetProperty("contractSha256").GetString(), Catalogue.ContractSha256);

        Assert.Equal(
            onDisk.RootElement.GetProperty("tools").GetArrayLength(),
            Catalogue.Tools.Count);
    }

    /// <summary>كل أداةٍ تسمّي عمليةً موجودةً، ومسارُها ليس باب أثرٍ لا يُعكَس.</summary>
    [Fact]
    public void لا_أداة_تبلغ_ترحيلاً_ولا_توقيعاً_ولا_اعتماداً()
    {
        Dictionary<string, string> operations = Operations();

        string[] irreversibleSegments =
            ["/posting", "/activation", "/approval", "/termination", "/revocation", "/reversal", "/lapse"];

        string[] declaredSegments = [.. AgentToolCatalogue.IrreversibleSegments];
        Assert.Equal<string>(irreversibleSegments, declaredSegments);

        int measured = 0;

        foreach (AgentTool tool in Catalogue.Tools)
        {
            if (tool.OperationId is null)
            {
                Assert.True(AgentProtocolTools.Contains(tool.Name), tool.Name + " أداةٌ بلا عملية وليست من البروتوكول.");
                continue;
            }

            measured++;

            Assert.Null(VoiceOperationGuard.Refuse(tool.OperationId));
            Assert.True(operations.ContainsKey(tool.OperationId), tool.OperationId + " ليست في العقد المنشور.");
            Assert.Equal(operations[tool.OperationId], tool.Path);

            foreach (string segment in irreversibleSegments)
            {
                Assert.False(
                    tool.Path!.EndsWith(segment, StringComparison.Ordinal),
                    "الأداة «" + tool.Name + "» تبلغ «" + tool.Path + "» — وهو بابُ أثرٍ لا يُعكَس.");
            }
        }

        // ‏**العدد تِبيانٌ لا خاصّية**: الخصائص هي التأكيدات أعلاه — لا عملية يرفضها
        // الحارس المنطوق، ولا مسار خارج العقد، ولا بابَ أثرٍ لا يُعكَس. والعدد يمنع
        // **النموّ الصامت** وحده، ويُرفَع حين تُنشَر عملية `draft…` جديدة يجتاز بابُها
        // تلك التأكيدات.
        //
        // ‏**23 ⇒ 24** بنشر `draftStockTransfer` (نقلٌ بين موقعين، مسوّدة لا تُحرّك
        // رصيداً). **وتنفيذُه `moveStockTransfer` ليس في الكتالوج** ولا يجوز أن يكون:
        // المولّد يقبل الفعل `draft…` وحده، فبابُ التنفيذ — وهو يكتب حركتين في الدفتر
        // المساعد لا تُحذفان — خارجٌ بالبناء.
        Assert.Equal(24, measured);
    }

    /// <summary>
    /// <b>وكل عملية <c>draft…</c> منشورة موجودةٌ في الكتالوج</b> — لا واحدة تسقط بصمت.
    /// وحارسٌ يمنع الزيادة ولا يفرض التمام يُخفي غياب نصف السطح.
    /// </summary>
    [Fact]
    public void كل_عملية_مسوّدةٍ_منشورة_في_الكتالوج()
    {
        string[] drafts = [.. Operations().Keys
            .Where(static name => name.StartsWith("draft", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

        string[] inCatalogue = [.. Catalogue.Tools
            .Where(static tool => tool.IsDraftOperation)
            .Select(static tool => tool.OperationId!)
            .Order(StringComparer.Ordinal)];

        Assert.Equal<string>(drafts, inCatalogue);
    }

    /// <summary>
    /// <b>ولا اسمَ عملية ترحيلٍ واحد في مصادر الحلقة ولا في مرآة المتصفّح.</b>
    /// <para>
    /// والاسم المكتوب بيدٍ في ملفّ هو الطريق الذي لا يراه حارسُ الكتالوج: سطرٌ واحد
    /// يُركّب مسار <c>…/posting</c> ويُرسله لا يمرّ بأي فحصٍ آخر.
    /// </para>
    /// <para>
    /// <b>وما لا يغطّيه هذا البند — يُقال ولا يُخفى:</b> مجلّد لوحة المتصفّح
    /// (<c>web/src/agent/</c>) يملكه سطحٌ آخر وقد لا يكون قد هبط بعد. فإن وُجد فُحص
    /// بكامله ووُثِّق عددُ ملفّاته؛ وإن لم يوجد فُحصت مصادر الخادم وحدها — والرسالة
    /// تقول أيّهما جرى، فلا تُقرأ خُضرةٌ أوسع ممّا تغطّي (فخ-80).
    /// </para>
    /// </summary>
    [Fact]
    public void لا_اسمَ_ترحيلٍ_في_مصادر_الحلقة_ولا_في_مرآة_المتصفّح()
    {
        Dictionary<string, string> operations = Operations();

        string[] posting =
        [
            .. operations.Keys.Where(static name => name.StartsWith("post", StringComparison.Ordinal)),
            "approveLeaseRegistrationForBilling",
            "terminateEmployee",
            "reverseJournalEntry",
            "revokeMembership",
        ];

        Assert.True(posting.Length >= 20, "أسماء الترحيل المقروءة: " + Count(posting.Length));

        List<string> scanned = [];

        // ‏(أ) مصادر الحلقة في الخادم — وهي موجودةٌ دائماً.
        string server = Path.Combine(RepositoryLayout.Root, "src", "Babel.Ai", "Agent");
        Assert.True(Directory.Exists(server), "مجلّد حلقة الوكيل غير موجود: " + server);

        string[] serverSources = [.. Directory.EnumerateFiles(server, "*.cs", SearchOption.AllDirectories)];

        // ‏**استثناءان مُعلَنان، ولهما سببٌ واحد:** هذان الملفّان هما اللذان *يعرّفان*
        // المقاطع الممنوعة، فوجودُ «/posting» فيهما هو الحارس لا خرقُه. وحارسٌ يشتكي من
        // تعريف نفسه يُدفَع صاحبُه إلى حذف التعريف — وهو أسوأ ما قد يفعله.
        // ‏**ويُتبَع الاستثناء بشاهدٍ موجب أدناه**: لو خلا الملفّان من المقطع لصار
        // الاستثناء تغطيةً لغياب الحارس، لا لوجوده.
        string[] defining = ["AgentToolCatalogue.cs", "AgentToolGate.cs"];

        scanned.AddRange(serverSources.Where(file =>
            !defining.Contains(Path.GetFileName(file), StringComparer.Ordinal)));

        int serverFiles = scanned.Count;
        Assert.True(serverFiles >= 10, "ملفّات الحلقة المقروءة: " + Count(serverFiles));

        // الشاهد الموجب على الاستثناء نفسه: المستثنيان يحملان المقاطع لأنهما يعرّفانها.
        foreach (string name in defining)
        {
            string path = Assert.Single(serverSources, file =>
                string.Equals(Path.GetFileName(file), name, StringComparison.Ordinal));

            Assert.Contains("/posting", File.ReadAllText(path), StringComparison.Ordinal);
        }

        // ‏(ب) مرآة المتصفّح إن وُجدت — يملكها سطحٌ آخر.
        string panel = Path.Combine(RepositoryLayout.Root, "web", "src", "agent");
        int panelFiles = 0;

        if (Directory.Exists(panel))
        {
            string[] found = [.. Directory.EnumerateFiles(panel, "*.*", SearchOption.AllDirectories)
                .Where(static file => file.EndsWith(".ts", StringComparison.Ordinal)
                                   || file.EndsWith(".tsx", StringComparison.Ordinal))];

            Assert.True(found.Length > 0, "مجلّد اللوحة موجودٌ وفارغ: " + panel);
            scanned.AddRange(found);
            panelFiles = found.Length;
        }

        foreach (string file in scanned)
        {
            string text = File.ReadAllText(file);

            foreach (string name in posting)
            {
                Assert.False(
                    text.Contains(name, StringComparison.Ordinal),
                    Path.GetFileName(file) + " يسمّي «" + name + "» — ومسار الوكيل لا يبلغ ترحيلاً.");
            }

            Assert.False(
                PostingSegment().IsMatch(text),
                Path.GetFileName(file) + " يحمل مقطع «/posting» — وهو بابُ الترحيل بعينه.");
        }

        // ‏**والتغطية تُعلَن بعددها**: الخُضرة هنا تعني ما فُحص، لا ما لم يُفحص.
        Assert.True(
            serverFiles + panelFiles >= 10,
            "المفحوص: " + Count(serverFiles) + " من الخادم و" + Count(panelFiles) + " من اللوحة.");
    }

    /// <summary>
    /// <b>والأفعال الممنوعة في المولّد هي الأفعال الممنوعة في الحارس المنطوق — نصّاً.</b>
    /// <para>
    /// المولّد سكربت ‎node ولا يقرأ ‎C#، فالقائمة مكرَّرةٌ فيه بالضرورة. والتكرار الذي
    /// لا يُقاس ينحرف: فعلٌ يُضاف إلى الحارس ولا يُضاف إلى المولّد يعني كتالوجاً يحمل
    /// ما لا تحمله البوّابة.
    /// </para>
    /// </summary>
    [Fact]
    public void قائمة_الأفعال_في_المولّد_تطابق_الحارس_المنطوق()
    {
        string generator = File.ReadAllText(
            Path.Combine(RepositoryLayout.Root, "tools", "agent", "build-tool-catalogue.mjs"));

        foreach (string verb in VoiceOperationGuard.ForbiddenVerbs.Keys)
        {
            Assert.Contains("\"" + verb + "\"", generator, StringComparison.Ordinal);
        }

        foreach (string segment in AgentToolCatalogue.IrreversibleSegments)
        {
            Assert.Contains("\"" + segment + "\"", generator, StringComparison.Ordinal);
        }

        Assert.Equal(11, VoiceOperationGuard.ForbiddenVerbs.Count);
    }
}
