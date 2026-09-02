using System.Globalization;
using System.Security.Cryptography;
using System.Text.Json;
using Babel.Ai.Agent;
using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Xunit;

namespace Babel.Ai.Tests.Agent;

/// <summary>
/// <b>الكتالوج مُولَّدٌ من العقد المنشور، ولا يُكتب بيد — والانحراف يُحمِّر البناء.</b>
/// <para>
/// <b>تقنيةُ القاعدة 18 نفسها:</b> الملفّ المُولَّد يحمل <c>sha256</c> مصدره في ترويسته،
/// وهذا الحارس يقارنها بالعقد <b>على القرص الآن</b>. فمن غيّر العقد ولم يُعِد التوليد
/// يسقط في بوّابة الخلفية، لا في شاشةٍ ترى النموذج يقترح حقلاً لم يعد موجوداً.
/// </para>
/// <para>
/// <b>ولماذا هذا يهمّ عملياً:</b> النموذج لا يرى العقد — يرى الكتالوج. فمخطّطٌ ينحرف
/// يُنتج جسماً يردّه الخادم <c>400</c>، والنموذج لا يعلم لماذا فيعيد المحاولة بالشكل
/// نفسه حتى ينفد السقف.
/// </para>
/// </summary>
public sealed class TheCatalogueIsBornOfThePublishedContract
{
    private static string ContractPath { get; } = RepositoryRoot.At("contracts/openapi/v1.json");

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

    /// <summary>حارسٌ لا فراغ: عقدٌ لا يُقرأ يجعل كل ما تحته يمرّ على لا شيء (فخ-43).</summary>
    [Fact]
    public void العقد_المنشور_ليس_ضامراً_ولا_الكتالوج()
    {
        Assert.True(Operations().Count >= 150, "العمليات المقروءة: " + Count(Operations().Count));
        Assert.True(Catalogue.Tools.Count >= 20, "الأدوات المقروءة: " + Count(Catalogue.Tools.Count));
    }

    /// <summary>البصمة المكتوبة في الكتالوج هي بصمة العقد على القرص الآن.</summary>
    [Fact]
    public void بصمة_الكتالوج_هي_بصمة_العقد_على_القرص()
    {
        string onDisk = Convert.ToHexStringLower(SHA256.HashData(File.ReadAllBytes(ContractPath)));

        Assert.Equal(onDisk, Catalogue.ContractSha256);
    }

    /// <summary>
    /// مجموعة الأدوات = كل عملية <c>draft…</c> منشورة، زائد أداتَي البروتوكول. لا واحدة أقلّ ولا أكثر.
    /// </summary>
    [Fact]
    public void الأدوات_هي_عمليات_المسوّدات_وحدها_مع_أداتي_البروتوكول()
    {
        string[] drafts = [.. Operations().Keys
            .Where(static name => name.StartsWith("draft", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)];

        string[] expected = [.. new[] { AgentProtocolTools.AskQuestion, AgentProtocolTools.LookupEntity }
            .Concat(drafts).Order(StringComparer.Ordinal)];

        string[] actual = [.. Catalogue.Tools.Select(static tool => tool.Name)];
        Assert.Equal<string>(expected, actual);
        // ‏**23 ⇒ 24** بنشر `draftStockTransfer` — نقلٌ بين موقعين، مسوّدة لا تُحرّك
        // رصيداً ولا تكتب قيداً. **وتنفيذُه `moveStockTransfer` ليس هنا** ولا يجوز أن
        // يكون: المولّد يقبل الفعل `draft…` وحده، فبابُ التنفيذ — وهو يكتب حركتين في
        // الدفتر المساعد لا تُحذفان — خارجٌ بالبناء.
        Assert.Equal(24, drafts.Length);
    }

    /// <summary>ولا اسم عمليةِ ترحيلٍ واحد في الكتالوج — شاهدٌ سلبي على الإغلاق.</summary>
    [Fact]
    public void لا_عملية_ترحيلٍ_واحدة_في_الكتالوج()
    {
        string[] posting = [.. Operations().Keys
            .Where(static name => name.StartsWith("post", StringComparison.Ordinal))];

        Assert.True(posting.Length >= 20, "أسماء الترحيل المقروءة: " + Count(posting.Length));

        foreach (string name in posting)
        {
            Assert.Null(Catalogue.Resolve(name));
        }
    }

    /// <summary>كل أداةٍ تسمّي عمليةً موجودةً في العقد، ومسارُها هو مسارُها فيه.</summary>
    [Fact]
    public void كل_أداة_تسمي_عمليةً_موجودةً_ومسارُها_مسارُها()
    {
        Dictionary<string, string> operations = Operations();
        int measured = 0;

        foreach (AgentTool tool in Catalogue.Tools.Where(static tool => tool.IsDraftOperation))
        {
            measured++;
            Assert.True(operations.ContainsKey(tool.OperationId!), tool.OperationId + " ليست في العقد المنشور.");
            Assert.Equal(operations[tool.OperationId!], tool.Path);
            Assert.Null(VoiceOperationGuard.Refuse(tool.OperationId));
        }

        Assert.Equal(24, measured);
    }

    /// <summary>ولا مسارَ أداةٍ ينتهي بمقطعٍ لا يُعكَس — يُقرأ من المسار لا من الاسم.</summary>
    [Fact]
    public void لا_مسارَ_أداةٍ_ينتهي_بمقطعٍ_لا_يُعكَس()
    {
        foreach (AgentTool tool in Catalogue.Tools)
        {
            foreach (string segment in AgentToolCatalogue.IrreversibleSegments)
            {
                Assert.False(
                    tool.Path?.EndsWith(segment, StringComparison.Ordinal) == true,
                    tool.Name + " يبلغ «" + tool.Path + "».");
            }
        }

        Assert.Equal(7, AgentToolCatalogue.IrreversibleSegments.Count);
    }

    /// <summary>
    /// كل حقلٍ ينتهي اسمه بـ«‏Id» مُعلَنٌ في <c>IdFields</c> ووصفُه أُعيدت كتابته إلى
    /// «مِقبض». <b>وحقلٌ يُنسى هنا يعني معرّفاً خاماً يعبر</b>.
    /// </summary>
    [Fact]
    public void كل_حقلٍ_شكلُه_معرّف_مُعلَنٌ_ووصفُه_مِقبض()
    {
        int measured = 0;

        foreach (AgentTool tool in Catalogue.Tools.Where(static tool => tool.IsDraftOperation))
        {
            using JsonDocument schema = JsonDocument.Parse(tool.InputSchemaJson);
            List<string> found = [];
            Walk(schema.RootElement, string.Empty, found);

                string[] expectedFields = [.. found.Order(StringComparer.Ordinal)];
            string[] declaredFields = [.. tool.IdFields.Order(StringComparer.Ordinal)];
            Assert.Equal<string>(expectedFields, declaredFields);
            measured += found.Count;
        }

        // حارسٌ لا فراغ: قياسٌ على صفر حقلٍ يمرّ على كل شيء.
        Assert.True(measured >= 25, "حقول المعرّفات المقيسة: " + Count(measured));
    }

    private static void Walk(JsonElement node, string path, List<string> found)
    {
        if (node.ValueKind != JsonValueKind.Object)
        {
            return;
        }

        if (node.TryGetProperty("items", out JsonElement items))
        {
            Walk(items, path + "[].", found);
        }

        if (!node.TryGetProperty("properties", out JsonElement properties))
        {
            return;
        }

        foreach (JsonProperty property in properties.EnumerateObject())
        {
            if (property.Name.Length > 2 && property.Name.EndsWith("Id", StringComparison.Ordinal))
            {
                found.Add(path + property.Name);

                Assert.Contains(
                    "مِقبض",
                    property.Value.GetProperty("description").GetString()!,
                    StringComparison.Ordinal);
            }
            else
            {
                Walk(property.Value, path + property.Name + ".", found);
            }
        }
    }

    /// <summary>مفردة السجلّات مغلقة، ومكتوبة في الكتالوج لا مشتقّة من التسجيل.</summary>
    [Fact]
    public void مفردة_السجلّات_مغلقة()
    {
        string[] keys = [.. Catalogue.RegisterKeys];
        Assert.Equal<string>(["customer", "employee", "inventory_item", "project", "property_unit", "supplier"], keys);

        using JsonDocument schema = JsonDocument.Parse(
            Catalogue.Resolve(AgentProtocolTools.LookupEntity)!.InputSchemaJson);

        string[] declared = [.. schema.RootElement
            .GetProperty("properties").GetProperty("kind").GetProperty("enum")
            .EnumerateArray().Select(static value => value.GetString()!)];

        Assert.Equal<string>([.. Catalogue.RegisterKeys], declared);
    }

    /// <summary>
    /// <c>ask_question</c> لا تُسهم بعنوانٍ ولا بخيارٍ ولا بعدد — معرّف الورقة وحده،
    /// و<c>additionalProperties:false</c>.
    /// </summary>
    [Fact]
    public void أداة_السؤال_لا_تحمل_إلا_معرّف_الورقة()
    {
        using JsonDocument schema = JsonDocument.Parse(
            Catalogue.Resolve(AgentProtocolTools.AskQuestion)!.InputSchemaJson);

        JsonElement root = schema.RootElement;

        Assert.False(root.GetProperty("additionalProperties").GetBoolean());
        string[] properties = [.. root.GetProperty("properties").EnumerateObject().Select(static p => p.Name)];
        string[] required = [.. root.GetProperty("required").EnumerateArray().Select(static v => v.GetString()!)];

        Assert.Equal<string>(["questionId"], properties);
        Assert.Equal<string>(["questionId"], required);
    }

    /// <summary>كل مخطّطٍ مغلق: <c>additionalProperties:false</c> على جذره.</summary>
    [Fact]
    public void كل_مخطّطٍ_مغلقٌ_على_جذره()
    {
        foreach (AgentTool tool in Catalogue.Tools)
        {
            using JsonDocument schema = JsonDocument.Parse(tool.InputSchemaJson);
            Assert.False(
                schema.RootElement.GetProperty("additionalProperties").GetBoolean(),
                tool.Name + " مخطّطُه مفتوح.");
        }
    }
}
