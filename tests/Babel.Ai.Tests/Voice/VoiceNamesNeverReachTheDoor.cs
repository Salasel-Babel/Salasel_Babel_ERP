using System.Text.Json;
using Babel.Ai.Tests.Support;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>الطبقة الثانية — والمناعة البنيوية كلّها هنا.</b>
/// <para>
/// الطبقة الأولى (حكمُ المقطع في <see cref="Babel.Ai.Voice.SpokenCommandReader"/>) مرشّحٌ
/// رخيص يحوّل أعلى الضجيج إلى رفضٍ عند الميكروفون. <b>وله ثقبٌ لا يُسدّ بلا معجم</b>:
/// ثلاث كلماتٍ عربية غير مشكولة لا يفصل بينها اسماً وبين اسمٍ يتبعه ذيلُ إسنادٍ قصير
/// أيُّ قاعدةٍ إملائية — وهو ما يوثّقه إثباتُ «الثقب المُعلَن».
/// </para>
/// <para>
/// <b>وهذا الحارس هو الجواب.</b> الصوت يحمل <b>اسماً</b>، والشاشة تملك القوائم
/// والمُنتقِيات فتحلّه إلى <b>معرّف صفٍّ واحد أمام عين الإنسان</b>. والمجموعة المغلقة
/// التي <b>لا يُتحايَل عليها بتسميةِ ما لم يخطر لأحد</b> هي صفوف المنشأة نفسها لا
/// قائمةُ كلماتٍ في شيفرة: «شركة المسار الامثل وانشئ لها حسابا» يطابق <b>صفراً</b>،
/// و«مؤسسة الرياض» يطابق <b>واحداً</b>.
/// </para>
/// <para>
/// <b>وما يقيسه هنا شكلُ ما يُنتَج لا شكلُ ما يُكتب:</b> ليس تعليقاً ولا اصطلاح تسمية،
/// بل <b>العقد المنشور نفسه</b> — لو ظهر يوماً حقلٌ نصّيّ باسم شريحةٍ منطوقة في بابٍ
/// تبلغه نيّة، لصار الاسمُ قادراً على مغادرة الشاشة، فيحمرّ هذا هنا لا عند العميل.
/// </para>
/// </summary>
public sealed class VoiceNamesNeverReachTheDoor
{
    private const string ContractPath = "contracts/openapi/v1.json";

    /// <summary>
    /// شرائح النصّ الحرّ — القائمة المغلقة الوحيدة، <b>وقطبُها آمن</b>: ما ليس فيها
    /// يلزمه حلٌّ إلى معرّف، فشريحةٌ جديدة تُضاف غداً تبدأ مطلوبةَ الحلّ افتراضاً.
    /// ونظيرتُها في المتصفّح <c>FREE_TEXT_SLOTS</c>، ويطابقها إثباتٌ هناك.
    /// </summary>
    private static readonly string[] FreeTextSlots = ["description", "reason"];

    private static JsonDocument Contract { get; } =
        JsonDocument.Parse(File.ReadAllText(RepositoryRoot.At(ContractPath)));

    [Fact]
    public void لا_باب_تبلغه_نيّة_يقبل_اسماً_منطوقاً()
    {
        Dictionary<string, HashSet<string>> surfaces = Surfaces();

        HashSet<string> mustResolve = [.. VoiceVectors.File.Intents
            .SelectMany(static intent => intent.Slots)
            .Where(static slot => string.Equals(slot.Kind, "Text", StringComparison.Ordinal))
            .Select(static slot => slot.Name)
            .Where(static name => !FreeTextSlots.Contains(name, StringComparer.Ordinal))];

        // حارس لا فراغ: مجموعةٌ فارغة تجعل الحلقة تحته تمرّ بلا أن تقرأ شيئاً (فخ-43).
        Assert.True(mustResolve.Count >= 10, mustResolve.Count.ToString(System.Globalization.CultureInfo.InvariantCulture));

        List<string> offences = [];
        int measured = 0;

        foreach (VectorIntent intent in VoiceVectors.File.Intents)
        {
            if (intent.OperationId is null)
            {
                continue;
            }

            Assert.True(surfaces.ContainsKey(intent.OperationId), intent.Id + " → " + intent.OperationId);
            HashSet<string> surface = surfaces[intent.OperationId];
            measured++;

            foreach (string name in mustResolve.Order(StringComparer.Ordinal))
            {
                if (surface.Contains(name))
                {
                    offences.Add(intent.Id + " → " + intent.OperationId + " يقبل «" + name + "» اسماً لا معرّفاً");
                }
            }

            // وكلُّ بابٍ تبلغه نيّة يقبل معرّفاً واحداً على الأقل — وإلّا فهو ليس مُعنوَناً.
            if (!surface.Any(static field => field.EndsWith("Id", StringComparison.Ordinal)))
            {
                offences.Add(intent.Id + " → " + intent.OperationId + " لا يقبل معرّفاً واحداً");
            }
        }

        Assert.True(measured >= 40, measured.ToString(System.Globalization.CultureInfo.InvariantCulture));
        Assert.True(
            offences.Count == 0,
            "الصوت يحمل أسماء، والأبواب تقبل معرّفات. وهذه تقبل اسماً:\n" + string.Join('\n', offences));
    }

    /// <summary>سطحُ كل عملية: بارامتراتها، ومتغيّرات مسارها، وحقول جسمها وسطوره.</summary>
    private static Dictionary<string, HashSet<string>> Surfaces()
    {
        Dictionary<string, HashSet<string>> surfaces = new(StringComparer.Ordinal);
        JsonElement root = Contract.RootElement;
        JsonElement schemas = root.GetProperty("components").GetProperty("schemas");

        foreach (JsonProperty path in root.GetProperty("paths").EnumerateObject())
        {
            foreach (JsonProperty verb in path.Value.EnumerateObject())
            {
                if (verb.Value.ValueKind != JsonValueKind.Object
                    || !verb.Value.TryGetProperty("operationId", out JsonElement id))
                {
                    continue;
                }

                HashSet<string> surface = new(StringComparer.Ordinal);

                foreach (string segment in path.Name.Split('/'))
                {
                    if (segment.StartsWith('{') && segment.EndsWith('}'))
                    {
                        surface.Add(segment[1..^1]);
                    }
                }

                AddParameters(path.Value, surface);
                AddParameters(verb.Value, surface);
                AddBody(verb.Value, schemas, surface);

                surfaces[id.GetString()!] = surface;
            }
        }

        return surfaces;
    }

    private static void AddParameters(JsonElement holder, HashSet<string> surface)
    {
        if (!holder.TryGetProperty("parameters", out JsonElement parameters)
            || parameters.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        foreach (JsonElement parameter in parameters.EnumerateArray())
        {
            if (parameter.TryGetProperty("name", out JsonElement name))
            {
                surface.Add(name.GetString()!);
            }
        }
    }

    private static void AddBody(JsonElement operation, JsonElement schemas, HashSet<string> surface)
    {
        if (!operation.TryGetProperty("requestBody", out JsonElement body)
            || !body.TryGetProperty("content", out JsonElement content)
            || !content.TryGetProperty("application/json", out JsonElement media)
            || !media.TryGetProperty("schema", out JsonElement schema))
        {
            return;
        }

        JsonElement resolved = Resolve(schema, schemas);
        if (!resolved.TryGetProperty("properties", out JsonElement properties))
        {
            return;
        }

        foreach (JsonProperty property in properties.EnumerateObject())
        {
            surface.Add(property.Name);

            // وسطورُ المستند جزءٌ من سطحه: معرّف الصنف يعيش هناك لا في جذر الجسم.
            if (property.Value.TryGetProperty("items", out JsonElement items))
            {
                JsonElement line = Resolve(items, schemas);
                if (line.TryGetProperty("properties", out JsonElement lineProperties))
                {
                    foreach (JsonProperty inner in lineProperties.EnumerateObject())
                    {
                        surface.Add(inner.Name);
                    }
                }
            }
        }
    }

    private static JsonElement Resolve(JsonElement schema, JsonElement schemas)
    {
        if (!schema.TryGetProperty("$ref", out JsonElement reference))
        {
            return schema;
        }

        string name = reference.GetString()!.Split('/')[^1];
        return schemas.GetProperty(name);
    }
}
