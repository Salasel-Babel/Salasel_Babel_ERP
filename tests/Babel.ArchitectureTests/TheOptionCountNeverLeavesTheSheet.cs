using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Babel.Ai.Workspace;
using Babel.ArchitectureTests.Support;
using Babel.Contracts.Lookup;
using Xunit;

namespace Babel.ArchitectureTests;

/// <summary>
/// <b>عددُ الخيارات لا يعبر — لا إلى النموذج ولا إلى العقد المنشور.</b>
/// <para>
/// <b>ولماذا العدد بالذات:</b> من يعرف أن «محمد» أعطى أربعة و«محمد ع» أعطى اثنين
/// و«محمد عل» أعطى واحداً يكون قد بحث في دفتر العملاء <b>بحثاً ثنائياً</b> واستخرج
/// أسماءه اسماً اسماً — بلا أن يخرج اسمٌ واحد في أي رسالة. والعدد ليس حقلاً يُحذف من
/// استجابة؛ <b>هو ما يجب ألّا يُحسب أصلاً</b>.
/// </para>
/// <para>
/// <b>وأربع طبقاتٍ تُقاس هنا، كلٌّ منها من مصدرٍ مستقلّ:</b>
/// </para>
/// <list type="number">
///   <item><b>النوع لا يملك حقلاً يحمل عدداً:</b> <see cref="NameCandidateProbe"/> ثلاثُ
///         حالاتٍ وقيمةٌ واحدة، ولا خاصّية عدديةً فيه — فما لا يوجد لا يُسرَّب سهواً.</item>
///   <item><b>وجوابُ الورقة مفتاحان لا ثالث لهما</b> في العقد المنشور: لا موضع، ولا
///         نصّ، ولا عدد، ولا «هل كان جديداً».</item>
///   <item><b>ولا مخطّطَ وكيلٍ منشور يحمل حقلاً يقول كم:</b> يُمسح كل مخطّط
///         <c>Agent*</c> على مفرداتٍ مغلقة (‏count · total · index · position · of …).</item>
///   <item><b>وحدثُ الدور لا يحمل الورقةَ نفسها:</b> مجموعةُ حقوله مُقاسةٌ بالضبط،
///         فحقلٌ يُضاف غداً يحمل خياراتٍ أو عددَها يُحمِّر هذا الحارس قبل أن يُنشر.</item>
/// </list>
/// <para>
/// <b>والشاهد الموجب (فخ-43):</b> كلمةُ العدّ المحظورة تُثبَت أوّلاً أنها <b>تُمسَك</b>
/// في نصٍّ يحملها، قبل أن يُثبَت أنها غائبة عن المخطّطات. وماسحٌ لا يُثبت أنه يرى
/// يمرّ على كل شيء.
/// </para>
/// </summary>
public sealed class TheOptionCountNeverLeavesTheSheet
{
    /// <summary>مفرداتُ العدّ الممنوعة في أسماء حقول سطح الوكيل — مجموعةٌ مغلقة.</summary>
    private static IReadOnlyList<string> CountingWords { get; } =
        ["count", "total", "index", "position", "ordinal", "cardinality", "matches", "candidates", "howMany"];

    private static string ContractPath { get; } =
        Path.Combine(RepositoryLayout.Root, "contracts", "openapi", "v1.json");

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static JsonElement Schemas(JsonDocument document) =>
        document.RootElement.GetProperty("components").GetProperty("schemas");

    /// <summary>هل في هذا الاسم كلمةُ عدٍّ؟ — مقارنةٌ بلا حساسية حالة الأحرف.</summary>
    private static string? CountingWordIn(string name) =>
        CountingWords.FirstOrDefault(word => name.Contains(word, StringComparison.OrdinalIgnoreCase));

    /// <summary><b>الشاهد الموجب أولاً</b>: الماسح يُمسك كلمة العدّ حيث توجد.</summary>
    [Fact]
    public void الماسحُ_يُمسك_كلمةَ_العدّ_حين_تكون_موجودة()
    {
        Assert.Equal("count", CountingWordIn("optionCount"));
        Assert.Equal("index", CountingWordIn("chosenIndex"));
        Assert.Equal("position", CountingWordIn("Position"));
        Assert.Null(CountingWordIn("optionToken"));
        Assert.Null(CountingWordIn("questionId"));
    }

    /// <summary>
    /// <b>نوعُ السبر لا يملك حقلاً يحمل عدداً</b> — ولا منشئاً عامّاً يستطيع أن يُعبّر
    /// عن «سبعة مرشّحين» حتى لو أراد محوّلٌ ذلك.
    /// </summary>
    [Fact]
    public void نوعُ_السبر_لا_يملك_ما_يحمل_عدداً()
    {
        PropertyInfo[] properties = typeof(NameCandidateProbe)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance);

        foreach (PropertyInfo property in properties)
        {
            Assert.False(
                property.PropertyType == typeof(int)
                || property.PropertyType == typeof(long)
                || property.PropertyType == typeof(int?)
                || property.PropertyType == typeof(long?),
                "‏NameCandidateProbe يحمل حقلاً عددياً: " + property.Name);

            // ‏**واستثناءٌ واحد بالاسم ومعه سببه:** `Cardinality` هي **المفردة المغلقة
            // نفسها** — «لا شيء · واحد · أكثر» — لا عدداً. وهي الحقل الذي يجعل العدّ
            // مستحيلاً لا الذي يسرّبه، وحارسٌ يشتكي منها يُدفع صاحبُه إلى حذفها.
            // ويُتبَع بشاهدٍ موجب أدناه: نوعُها تعدادٌ بثلاثة أعضاء لا رابع لها.
            if (!string.Equals(property.Name, nameof(NameCandidateProbe.Cardinality), StringComparison.Ordinal))
            {
                Assert.Null(CountingWordIn(property.Name));
            }
        }

        // الشاهد الموجب على الاستثناء: الحقل المستثنى تعدادٌ مغلق لا عدد.
        Assert.Equal(
            typeof(NameCandidateCardinality),
            typeof(NameCandidateProbe).GetProperty(nameof(NameCandidateProbe.Cardinality))!.PropertyType);

        // ولا منشئ عامّاً: ثلاث دوالّ مصنعية لا رابع لها.
        Assert.Empty(typeof(NameCandidateProbe).GetConstructors(BindingFlags.Public | BindingFlags.Instance));

        // والحالات ثلاث، ولا رابعة.
        Assert.Equal(3, Enum.GetValues<NameCandidateCardinality>().Length);
    }

    /// <summary>
    /// <b>جوابُ الورقة مفتاحان لا ثالث لهما</b> — مقروءاً من العقد المنشور نفسه.
    /// </summary>
    [Fact]
    public void جوابُ_الورقة_مفتاحان_في_العقد_المنشور()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));

        JsonElement answer = Schemas(document).GetProperty("AgentAnswerRequest");

        string[] properties = [.. answer.GetProperty("properties").EnumerateObject()
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)];

        Assert.Equal<string>(["optionToken", "questionId"], properties);
        Assert.False(answer.GetProperty("additionalProperties").GetBoolean());

        string[] required = [.. answer.GetProperty("required").EnumerateArray()
            .Select(static value => value.GetString()!)
            .Order(StringComparer.Ordinal)];

        Assert.Equal<string>(["optionToken", "questionId"], required);
    }

    /// <summary>
    /// <b>ولا مخطّطَ وكيلٍ منشور يحمل حقلاً يقول «كم»</b> — لا في الورقة ولا في
    /// خيارها ولا في الحال ولا في الحدث.
    /// </summary>
    [Fact]
    public void لا_حقلَ_يقول_كم_في_مخطّطات_الوكيل()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));

        int inspected = 0;
        int fields = 0;

        foreach (JsonProperty schema in Schemas(document).EnumerateObject())
        {
            if (!schema.Name.StartsWith("Agent", StringComparison.Ordinal)
                || !schema.Value.TryGetProperty("properties", out JsonElement properties))
            {
                continue;
            }

            inspected++;

            foreach (JsonProperty property in properties.EnumerateObject())
            {
                fields++;

                // ‏`order` على الخطوة ترتيبُ **خطوةٍ في خطّةٍ أعلنها النموذج بنفسه**، لا
                // موضعُ خيارٍ على ورقةٍ لا يراها. ولذلك يُستثنى بالاسم ومعه سببه، ثمّ
                // يُتبَع بشاهدٍ موجب: الورقة وخيارها لا يحملان `order` ولا نظيراً له.
                if (string.Equals(schema.Name, "AgentPlanStep", StringComparison.Ordinal)
                    && string.Equals(property.Name, "order", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.Null(CountingWordIn(property.Name));
            }
        }

        Assert.True(inspected >= 9, "مخطّطات الوكيل المفحوصة: " + Count(inspected));
        Assert.True(fields >= 40, "الحقول المفحوصة: " + Count(fields));

        // الشاهد الموجب على الاستثناء: الورقة وخيارها بلا ترتيبٍ ولا موضع.
        foreach (string sheetSchema in new[] { "AgentQuestionSheet", "AgentQuestionOption" })
        {
            foreach (JsonProperty property in Schemas(document)
                .GetProperty(sheetSchema).GetProperty("properties").EnumerateObject())
            {
                Assert.NotEqual("order", property.Name);
                Assert.Null(CountingWordIn(property.Name));
            }
        }
    }

    /// <summary>
    /// <b>وحدثُ الدور لا يحمل الورقةَ ولا خياراتها</b>: مجموعةُ حقوله مقيسةٌ بالضبط،
    /// فحقلٌ يُضاف غداً يحمل خياراتٍ أو عددَها يُحمِّر هذا الحارس قبل أن يُنشر.
    /// </summary>
    [Fact]
    public void حدثُ_الدور_لا_يحمل_خياراً_ولا_عددَه()
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(ContractPath));

        string[] properties = [.. Schemas(document).GetProperty("AgentTurnEvent")
            .GetProperty("properties").EnumerateObject()
            .Select(static property => property.Name)
            .Order(StringComparer.Ordinal)];

        Assert.Equal<string>(
            [
                "kind", "questionId", "refusals", "registerKey", "screenRoute",
                "sequence", "stepId", "steps", "text", "toolName", "turnId",
            ],
            properties);
    }

    /// <summary>
    /// <b>ونوعُ الورقة في الخادم لا يقول كم</b> — الخيارات قائمة، ولا حقلَ يحمل طولَها
    /// ولا موضعَ ما اختير، ولا حقلَ يقول «أُنشئ جديد».
    /// </summary>
    [Fact]
    public void نوعُ_الورقة_في_الخادم_لا_يحمل_عدداً()
    {
        foreach (PropertyInfo property in typeof(AgentWorkspaceQuestion)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.Null(CountingWordIn(property.Name));

            Assert.False(
                property.PropertyType == typeof(int) || property.PropertyType == typeof(long),
                "ورقةُ السؤال تحمل حقلاً عددياً: " + property.Name);
        }

        foreach (PropertyInfo property in typeof(AgentSheetOption)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            Assert.Null(CountingWordIn(property.Name));
        }
    }
}
