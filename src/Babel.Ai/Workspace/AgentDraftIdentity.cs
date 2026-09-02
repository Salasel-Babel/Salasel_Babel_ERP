using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Ai.Agent;

namespace Babel.Ai.Workspace;

/// <summary>
/// <b>هويّةُ المسوّدة — نظيرُ هويّة الترحيل، وبالحجّة نفسها.</b>
/// <para>
/// القاعدة 4 في هذا المستودع تجعل إعادةَ الترحيل بالمفتاح نفسه <b>تُعيد الإيصال نفسه</b>
/// بـ<c>WasAlreadyPosted = true</c> ولا تكتب قيداً ثانياً. وهذا نظيرُه للمسوّدة:
/// <b>تأكيدٌ مكرَّر على الشكل نفسه يُعيد المسوّدة التي هبطت، ولا تهبط ثانية.</b>
/// </para>
/// <para>
/// <b>وممّ تُبنى:</b> المنشأة · الشركة · الجلسة · معرّف العملية · <b>جسمُ الطلب مُقنَّناً</b>.
/// وكلٌّ منها لسبب: الجلسة تجعل محادثةً ثانية للإنسان نفسه تُنشئ مستنداً ثانياً — وهو ما
/// يريده حين يفتح محادثةً جديدة ويطلبه من جديد؛ والجسمُ يجعل تصحيحَ مبلغٍ ثمّ إعادةَ
/// التأكيد <b>مسوّدةً جديدة</b> لا إعادةَ القديمة.
/// </para>
/// <para>
/// <b>ولماذا تُقنَّن البايتات قبل أن تُجزَّأ:</b> نموذجٌ احتماليّ يكتب المفاتيح بترتيبٍ
/// مختلف بين نداءٍ ونداء وهو يعني الشيء نفسه. وجزءٌ على النصّ الخام كان سيجعل
/// <c>{"a":1,"b":2}</c> و<c>{"b":2,"a":1}</c> مسوّدتين — أي أن الحصانة تعمل في
/// الاختبار وتسقط عند العميل. فالمفاتيح تُرتَّب، والمصفوفات <b>لا تُرتَّب</b>: ترتيبُ
/// السطور معنى لا تفصيل.
/// </para>
/// </summary>
public static class AgentDraftIdentity
{
    /// <summary>فاصلٌ لا يقع في نصّ JSON ولا في معرّف عملية — فلا تلتحم حقلان.</summary>
    private const char Separator = '\u001F';

    /// <summary>يحسب هويّة مسوّدة هذا النداء — نصّاً ستّينيّاً عشرياً صغيراً.</summary>
    /// <param name="dispatch">الأمر الذي اجتاز البوّابة.</param>
    public static string Of(AgentDispatch dispatch)
    {
        ArgumentNullException.ThrowIfNull(dispatch);

        StringBuilder material = new();
        material.Append(dispatch.Caller.Tenant.Value.ToString("D", CultureInfo.InvariantCulture)).Append(Separator);
        material.Append(dispatch.Caller.CompanyId.ToString("D", CultureInfo.InvariantCulture)).Append(Separator);
        material.Append(dispatch.Caller.SessionId.ToString("D", CultureInfo.InvariantCulture)).Append(Separator);
        material.Append(dispatch.Tool.OperationId ?? string.Empty).Append(Separator);
        material.Append(Canonical(dispatch.Body));

        byte[] digest = SHA256.HashData(Encoding.UTF8.GetBytes(material.ToString()));
        return Convert.ToHexStringLower(digest);
    }

    /// <summary>
    /// يُقنّن جسم الطلب: مفاتيحُ الكائنات مرتَّبةٌ ترتيباً ثابتاً، والمصفوفات بترتيبها.
    /// <b>وجسمٌ لا يُقرأ JSON يُقنَّن إلى نفسه</b> — فلا يُبتلع بصمتٍ ولا يُساوي غيره.
    /// </summary>
    /// <param name="body">الجسم كما وصل.</param>
    internal static string Canonical(string body)
    {
        ArgumentNullException.ThrowIfNull(body);

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(body);
        }
        catch (JsonException)
        {
            return body;
        }

        StringBuilder written = new();
        Write(root, written, 0);
        return written.ToString();
    }

    private static void Write(JsonNode? node, StringBuilder into, int depth)
    {
        if (depth > 32)
        {
            into.Append("...");
            return;
        }

        switch (node)
        {
            case JsonObject entry:
                into.Append('{');
                bool first = true;
                foreach (KeyValuePair<string, JsonNode?> property in
                    entry.OrderBy(static pair => pair.Key, StringComparer.Ordinal))
                {
                    if (!first)
                    {
                        into.Append(',');
                    }

                    first = false;
                    into.Append(JsonSerializer.Serialize(property.Key)).Append(':');
                    Write(property.Value, into, depth + 1);
                }

                into.Append('}');
                break;

            case JsonArray array:
                into.Append('[');
                for (int index = 0; index < array.Count; index++)
                {
                    if (index > 0)
                    {
                        into.Append(',');
                    }

                    Write(array[index], into, depth + 1);
                }

                into.Append(']');
                break;

            case null:
                into.Append("null");
                break;

            default:
                into.Append(node.ToJsonString());
                break;
        }
    }
}
