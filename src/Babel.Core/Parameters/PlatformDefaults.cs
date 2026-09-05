using System.Globalization;
using System.Reflection;
using System.Text.Json;
using Babel.Contracts.Parameters;

namespace Babel.Core.Parameters;

/// <summary>
/// <b>افتراضاتُ المنصّة كما تُشحن — بياناتٌ مضمَّنة في التجميعة، لا أرقامٌ في شيفرة.</b>
/// <para>
/// المصدر ملفٌّ واحد: <c>data/parameters/platform-defaults.json</c>. ويُقرأ من موضعين
/// لا ثالث لهما — بذرُ قاعدة النواة عند النشر، والمخزن في الذاكرة لمن لا قاعدة له —
/// فلا يوجد رقمٌ افتراضيّ ثانٍ يُنسى تحديثه.
/// </para>
/// <para>
/// <b>وحالتُها واحدة لا تتغيّر:</b> «افتراضُ منصّة غير مُعتمَد». ولا يحمل صفٌّ منها اسمَ
/// معتمِد، ولا يبلغ واحدٌ منها حالةَ «توقيعِ محاسبٍ قانوني» بحال — والقيد في المخطّط.
/// </para>
/// </summary>
public static class PlatformDefaults
{
    private static readonly Lazy<List<ParameterVersionView>> Loaded = new(Read);

    /// <summary>معرّف المستأجر الذي تحمله صفوف المنصّة — الصفري، لا <c>null</c>.</summary>
    public static Guid PlatformTenant => Guid.Empty;

    /// <summary>الإصدارات المشحونة، مقروءةً مرّةً لكل عملية.</summary>
    public static IReadOnlyList<ParameterVersionView> All => Loaded.Value;

    private static List<ParameterVersionView> Read()
    {
        Assembly assembly = typeof(PlatformDefaults).Assembly;
        string resource = assembly.GetManifestResourceNames()
            .Single(static candidate => candidate.EndsWith("platform-defaults.json", StringComparison.Ordinal));

        using Stream stream = assembly.GetManifestResourceStream(resource)!;
        using JsonDocument document = JsonDocument.Parse(stream);

        List<ParameterVersionView> versions = [];

        foreach (JsonElement entry in document.RootElement.GetProperty("versions").EnumerateArray())
        {
            string setCode = entry.GetProperty("setCode").GetString()!;
            ParameterSetDefinition definition = ParameterCatalogue.Find(setCode)
                ?? throw new InvalidOperationException(
                    "افتراضُ منصّةٍ لمجموعةٍ ليست في الفهرس: " + setCode
                    + " / a platform default for a set that is not in the catalogue");

            List<ParameterValueView> values = [];
            JsonElement shipped = entry.GetProperty("values");

            foreach (ParameterKeyDefinition key in definition.Keys)
            {
                // ‏**نصّاً لا رمزاً رقمياً**: الرمز الرقمي في JSON يمرّ على فاصلة عائمة
                // ثنائية في أغلب القرّاء، وهو الحدّ الذي يُغلق في `WireDecimalJsonConverter`
                // نفسه. والملفّ بياناتٌ تعبر إلى `decimal` بلا وسيط ثنائي.
                decimal value = decimal.Parse(
                    shipped.GetProperty(key.Key).GetString()!, NumberStyles.Number, CultureInfo.InvariantCulture);

                values.Add(new ParameterValueView(key.Key, key.Kind, value));
            }

            versions.Add(new ParameterVersionView(
                Guid.ParseExact(entry.GetProperty("versionId").GetString()!, "D"),
                setCode,
                ParameterScope.Platform,
                DateOnly.ParseExact(
                    entry.GetProperty("effectiveFrom").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture),
                ParameterApproval.PlatformDefault,
                string.Empty,
                null,
                entry.GetProperty("sourceRef").GetString()!,
                values));
        }

        return versions;
    }
}
