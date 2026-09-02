using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// <b>الوسائط تُطابَق بالمخطّط المنشور — لا تُقرأ حقولاً معروفةً ويُتجاهَل ما عداها.</b>
/// <para>
/// <b>لماذا هذا حارسٌ لا تحسين:</b> الخطوة الخامسة في البوّابة تفكّ «كل حقلٍ شكلُه معرّف»
/// بمساره المنشور — <c>lines.[].originalInvoiceLineId</c>. وهي تجد ذلك المسار <b>إن كان
/// الجسم على الشكل المنشور</b>. فلو أرسل النموذج <c>lines</c> كائناً بدل مصفوفة، أو
/// أرسل <c>"CustomerId"</c> بحرفٍ كبير، أو دسّ <c>"meta": { "customerId": … }</c>، لم
/// يجد المُحدِّد شيئاً — <b>ولم يُفكّ شيء، ولم يُرفض شيء</b> — ووصل المعرّفُ الخام إلى
/// جسم المسوّدة سالماً. مقيسٌ في أربع صور.
/// </para>
/// <para>
/// <b>والمخطّط يقول ذلك أصلاً:</b> كل مخطّطات الكتالوج تحمل
/// <c>"additionalProperties": false</c> و<c>required</c>، وقد وُلِّدت من العقد المنشور.
/// فما كان ناقصاً هو <b>أن يُقرأ</b>، لا أن يُكتب.
/// </para>
/// <para>
/// <b>وما لا يُفحص هنا عمداً:</b> <c>pattern</c> و<c>maxLength</c> على <b>مواضع
/// المقابض</b>. العقد يصف تلك الحقول كما تصل من الشاشة — معرّفاً من ست وثلاثين محرفاً —
/// ووسيطُ الوكيل فيها مِقبضٌ من مئةٍ واثنين وأربعين. ففحصُ الطول هناك يرفض كل نداءٍ
/// سليم. وحارسُ تلك المواضع أقوى من الشكل لا أضعف منه: توقيعُ HMAC لهذه الجلسة بعينها.
/// </para>
/// </summary>
internal static class AgentArgumentSchema
{
    /// <summary>سقف عمق التداخل — جسمٌ أعمق منه ليس جسم عمليةٍ منشورة.</summary>
    private const int DepthCeiling = 12;

    /// <summary>يجمع مخالفات الوسائط للمخطّط المنشور. قائمةٌ فارغة تعني الموافقة.</summary>
    /// <param name="arguments">الوسائط كما فُكّت.</param>
    /// <param name="tool">الأداة ومخطّطها ومواضع مقابضها.</param>
    public static List<Error> Violations(JsonObject arguments, AgentTool tool)
    {
        ArgumentNullException.ThrowIfNull(arguments);
        ArgumentNullException.ThrowIfNull(tool);

        List<Error> errors = [];

        using JsonDocument schema = JsonDocument.Parse(tool.InputSchemaJson);
        HashSet<string> handleSlots = new(tool.IdFields, StringComparer.Ordinal);

        Check(arguments, schema.RootElement, string.Empty, tool, handleSlots, errors, 0);

        return errors;
    }

    private static void Check(
        JsonNode? node,
        JsonElement schema,
        string path,
        AgentTool tool,
        HashSet<string> handleSlots,
        List<Error> errors,
        int depth)
    {
        if (depth > DepthCeiling)
        {
            errors.Add(AgentErrors.ArgumentTooDeep(tool.Name, DepthCeiling));
            return;
        }

        if (node is null || node.GetValueKind() == JsonValueKind.Null)
        {
            // ‏`null` مقبولٌ حيث يعلن المخطّط ‎["string","null"]، ومرفوضٌ حيث لا يعلن.
            if (!Allows(schema, "null"))
            {
                errors.Add(AgentErrors.ArgumentShapeMismatch(tool.Name, Name(path), Declared(schema)));
            }

            return;
        }

        if (Allows(schema, "object"))
        {
            CheckObject(node, schema, path, tool, handleSlots, errors, depth);
            return;
        }

        if (Allows(schema, "array"))
        {
            CheckArray(node, schema, path, tool, handleSlots, errors, depth);
            return;
        }

        CheckLeaf(node, schema, path, tool, handleSlots, errors);
    }

    private static void CheckObject(
        JsonNode node,
        JsonElement schema,
        string path,
        AgentTool tool,
        HashSet<string> handleSlots,
        List<Error> errors,
        int depth)
    {
        if (node is not JsonObject entry)
        {
            errors.Add(AgentErrors.ArgumentShapeMismatch(tool.Name, Name(path), "object"));
            return;
        }

        bool hasProperties = schema.TryGetProperty("properties", out JsonElement properties);
        bool closed = !schema.TryGetProperty("additionalProperties", out JsonElement extra)
            || extra.ValueKind != JsonValueKind.True;

        foreach (KeyValuePair<string, JsonNode?> property in entry)
        {
            string childPath = path.Length == 0 ? property.Key : path + "." + property.Key;

            if (hasProperties && properties.TryGetProperty(property.Key, out JsonElement child))
            {
                Check(property.Value, child, childPath, tool, handleSlots, errors, depth + 1);
                continue;
            }

            // ‏**حقلٌ لا يعلنه المخطّط يُرفض ولا يُتجاهَل.** التجاهل هو بعينه الباب الذي
            // عبر منه معرّفٌ خام في «meta» وفي «CustomerId» بحرفٍ كبير.
            if (closed)
            {
                errors.Add(AgentErrors.ArgumentNotInSchema(tool.Name, Name(childPath)));
            }
        }

        if (!schema.TryGetProperty("required", out JsonElement required))
        {
            return;
        }

        foreach (JsonElement name in required.EnumerateArray())
        {
            string? key = name.GetString();
            if (key is null)
            {
                continue;
            }

            // ‏**«إلزاميّ» يعني أنّ المفتاح موجود، لا أنّ قيمته ليست `null`.** والعقد
            // ينشر ثلاثة حقولٍ إلزامية نوعُها ‎["string","null"] — `guaranteeId` و
            // ‏`lines.[].itemId` في مستخلصَين — فربطُ الإلزام بعدم الفراغ كان سيرفض
            // نداءً سليماً. وحين يكون الحقل حاضراً فارغاً ولا يقبل نوعُه الفراغ يتكفّل
            // فحصُ الشكل برفضه، فلا ازدواج ولا ثغرة.
            if (!entry.ContainsKey(key))
            {
                errors.Add(AgentErrors.ArgumentRequiredMissing(
                    tool.Name, Name(path.Length == 0 ? key : path + "." + key)));
            }
        }
    }

    private static void CheckArray(
        JsonNode node,
        JsonElement schema,
        string path,
        AgentTool tool,
        HashSet<string> handleSlots,
        List<Error> errors,
        int depth)
    {
        if (node is not JsonArray array)
        {
            errors.Add(AgentErrors.ArgumentShapeMismatch(tool.Name, Name(path), "array"));
            return;
        }

        if (!schema.TryGetProperty("items", out JsonElement items))
        {
            return;
        }

        foreach (JsonNode? item in array)
        {
            Check(item, items, path + ".[]", tool, handleSlots, errors, depth + 1);
        }
    }

    private static void CheckLeaf(
        JsonNode node,
        JsonElement schema,
        string path,
        AgentTool tool,
        HashSet<string> handleSlots,
        List<Error> errors)
    {
        JsonValueKind kind = node.GetValueKind();

        bool matches = kind switch
        {
            JsonValueKind.String => Allows(schema, "string"),
            JsonValueKind.Number => Allows(schema, "number") || Allows(schema, "integer"),
            JsonValueKind.True or JsonValueKind.False => Allows(schema, "boolean"),
            _ => false,
        };

        if (!matches)
        {
            errors.Add(AgentErrors.ArgumentShapeMismatch(tool.Name, Name(path), Declared(schema)));
            return;
        }

        if (kind != JsonValueKind.String)
        {
            return;
        }

        string value = node.GetValue<string>();

        if (schema.TryGetProperty("enum", out JsonElement allowed)
            && !allowed.EnumerateArray().Any(member =>
                string.Equals(member.GetString(), value, StringComparison.Ordinal)))
        {
            errors.Add(AgentErrors.ArgumentShapeMismatch(tool.Name, Name(path), "enum"));
            return;
        }

        // موضع المِقبض يحمل مئةً واثنين وأربعين محرفاً حيث يصف العقد ستّاً وثلاثين:
        // فحصُ الطول هناك يرفض كل نداءٍ سليم، وحارسُه التوقيع لا الشكل.
        if (handleSlots.Contains(path))
        {
            return;
        }

        if (schema.TryGetProperty("maxLength", out JsonElement maxLength)
            && value.Length > maxLength.GetInt32())
        {
            errors.Add(AgentErrors.ArgumentShapeMismatch(tool.Name, Name(path), "maxLength"));
        }
    }

    /// <summary>هل يقبل المخطّط هذا النوع؟ ‏<c>type</c> نصٌّ أو قائمة نصوص.</summary>
    private static bool Allows(JsonElement schema, string type)
    {
        if (!schema.TryGetProperty("type", out JsonElement declared))
        {
            return false;
        }

        return declared.ValueKind switch
        {
            JsonValueKind.String => string.Equals(declared.GetString(), type, StringComparison.Ordinal),
            JsonValueKind.Array => declared.EnumerateArray().Any(member =>
                string.Equals(member.GetString(), type, StringComparison.Ordinal)),
            _ => false,
        };
    }

    private static string Declared(JsonElement schema)
        => schema.TryGetProperty("type", out JsonElement type) ? type.ToString() : "unknown";

    private static string Name(string path) => path.Length == 0 ? "(الجذر)" : path;
}
