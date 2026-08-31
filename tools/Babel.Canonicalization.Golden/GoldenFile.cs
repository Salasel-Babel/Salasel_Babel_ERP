using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Babel.Canonicalization.Schemas;

namespace Babel.Canonicalization.Golden;

/// <summary>
/// قراءة وكتابة ملف المتجهات الذهبية.
///
/// ملاحظة مقصودة: <c>System.Text.Json</c> يُستخدم هنا وحده — في الأداة، لا في
/// المكتبة. الملف الذهبي <b>وثيقة توقّع</b>، لا مُدخل للتجزئة. لا يجوز أبداً أن
/// يقترب مُسلسِل JSON من البايتات القانونية.
/// </summary>
/// <summary>
/// هوية مجموعة متجهات: أي إصدار، وأي ترويسة سلكية، وأي بصمة مخطّط، وأي ملف.
/// <para>
/// وجودها نوعاً مستقلاً هو ما يمنع الخطأ الصامت الوحيد المهمّ هنا: <b>فحص مجموعة
/// إصدار بترويسة إصدار آخر</b>. بلا ذلك يكفي سهو في نداء واحد ليقارن ملف v2
/// ببصمة مخطّط v1 ويمرّ.
/// </para>
/// </summary>
public sealed record GoldenSetIdentity(
    string CanonVersion,
    string WireMagic,
    string SchemaFingerprint,
    string FileName)
{
    /// <summary>هوية مجموعة v1 — <b>مجمّدة</b>.</summary>
    public static GoldenSetIdentity V1 { get; } = new(
        Canonicalizer.CurrentVersion,
        Canonicalizer.Magic,
        JournalEntrySchema.V1.Fingerprint,
        "golden-vectors.v1.json");

    /// <summary>هوية مجموعة v2.</summary>
    public static GoldenSetIdentity V2 { get; } = new(
        CanonicalV2.Version,
        CanonicalV2.Magic,
        JournalEntrySchema.V2.Fingerprint,
        "golden-vectors.v2.json");

    /// <summary>كل المجموعات، بترتيب الإصدارات.</summary>
    public static IReadOnlyList<GoldenSetIdentity> All { get; } = [V1, V2];

    /// <summary>المتجهات التي تخصّ هذه الهوية.</summary>
    public IReadOnlyList<GoldenVector> Vectors => CanonVersion == "v1" ? GoldenVectorSet.All : GoldenVectorSetV2.All;
}

public static class GoldenFile
{
    private static readonly UTF8Encoding Utf8 = new(false);

    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true,
        Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
    };

    /// <summary>يُنفّذ كل المتجهات ويبني وثيقة JSON.</summary>
    public static string Emit(GoldenSetIdentity identity, IReadOnlyList<GoldenVector> vectors)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var array = new JsonArray();
        foreach (var v in vectors)
        {
            var r = v.Execute();
            var o = new JsonObject
            {
                ["id"] = r.Id,
                ["ar"] = r.DescriptionAr,
                ["kind"] = r.Kind.ToString().ToLowerInvariant()
            };
            if (r.CanonicalText is not null) o["canonical_text"] = r.CanonicalText;
            if (r.CanonicalBytesHex is not null) o["canonical_bytes_hex"] = r.CanonicalBytesHex;
            if (r.CanonicalSha256 is not null) o["sha256"] = r.CanonicalSha256;
            if (r.ErrorCode is not null) o["error_code"] = r.ErrorCode;
            if (r.Value is not null) o["value"] = r.Value;
            if (r.Hashes is not null)
            {
                var h = new JsonArray();
                foreach (var x in r.Hashes) h.Add(x);
                o["hashes"] = h;
            }
            if (r.Note is not null) o["note"] = r.Note;
            array.Add(o);
        }

        var vectorsJson = array.ToJsonString(Options);
        var manifest = Convert.ToHexString(SHA256.HashData(Utf8.GetBytes(vectorsJson))).ToLowerInvariant();

        var root = new JsonObject
        {
            ["_"] = "متجهات ذهبية — لا تُحرَّر يدوياً. تُولَّد بـ --emit وتُفحص بـ --verify.",
            ["canon_version"] = identity.CanonVersion,
            ["wire_magic"] = identity.WireMagic,
            ["schema_kind"] = JournalEntrySchema.Kind,
            ["schema_fingerprint"] = identity.SchemaFingerprint,
            ["hash_algorithm"] = "SHA-256",
            ["encoding"] = "UTF-8 without BOM",
            ["vector_count"] = vectors.Count,
            ["manifest_sha256"] = manifest,
            ["vectors"] = JsonNode.Parse(vectorsJson)
        };

        return root.ToJsonString(Options) + "\n";
    }

    /// <summary>نتيجة مقارنة متجه واحد.</summary>
    public sealed record Drift(string Id, string Field, string Expected, string Actual);

    /// <summary>يقارن المُخرَج الحالي بالملف المخزَّن ويعيد كل الانحرافات.</summary>
    public static IReadOnlyList<Drift> Verify(
        GoldenSetIdentity identity, string storedJson, IReadOnlyList<GoldenVector> vectors)
    {
        ArgumentNullException.ThrowIfNull(identity);
        var drifts = new List<Drift>();
        var root = JsonNode.Parse(storedJson)!.AsObject();

        void Cmp(string id, string field, string? expected, string? actual)
        {
            if (!string.Equals(expected, actual, StringComparison.Ordinal))
                drifts.Add(new Drift(id, field, expected ?? "(none)", actual ?? "(none)"));
        }

        Cmp("_meta", "canon_version", root["canon_version"]?.GetValue<string>(), identity.CanonVersion);
        Cmp("_meta", "wire_magic", root["wire_magic"]?.GetValue<string>(), identity.WireMagic);
        Cmp("_meta", "schema_fingerprint", root["schema_fingerprint"]?.GetValue<string>(),
            identity.SchemaFingerprint);
        Cmp("_meta", "vector_count",
            root["vector_count"]?.GetValue<int>().ToString(CultureInfo.InvariantCulture),
            vectors.Count.ToString(CultureInfo.InvariantCulture));

        // البصمة المُودَعة على مصفوفة المتجهات تُعاد حسابها من **الملف نفسه**:
        // تحرير يدوي يغيّر متجهاً ويترك manifest_sha256 كما هو يُكشف هنا، لا عند
        // أول ترحيل. (وإعادة الترتيب وحدها تكفي لتغيير البصمة.)
        var storedManifest = root["manifest_sha256"]?.GetValue<string>();
        var recomputedManifest = Convert.ToHexString(
            SHA256.HashData(Utf8.GetBytes(root["vectors"]!.ToJsonString(Options)))).ToLowerInvariant();
        Cmp("_meta", "manifest_sha256", storedManifest, recomputedManifest);

        var stored = root["vectors"]!.AsArray();
        var byId = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        var order = new List<string>();
        foreach (var n in stored)
        {
            var o = n!.AsObject();
            var id = o["id"]!.GetValue<string>();
            byId[id] = o;
            order.Add(id);
        }

        for (var i = 0; i < vectors.Count; i++)
        {
            var v = vectors[i];
            if (i < order.Count && order[i] != v.Id)
                drifts.Add(new Drift(v.Id, "order", order[i], v.Id));

            if (!byId.TryGetValue(v.Id, out var expected))
            {
                drifts.Add(new Drift(v.Id, "missing", "(not in golden file)", "present in code"));
                continue;
            }

            GoldenResult actual;
            try
            {
                actual = v.Execute();
            }
            catch (Exception ex)
            {
                drifts.Add(new Drift(v.Id, "threw", "(a result)", ex.GetType().Name + ": " + ex.Message));
                continue;
            }

            Cmp(v.Id, "kind", expected["kind"]?.GetValue<string>(), actual.Kind.ToString().ToLowerInvariant());
            Cmp(v.Id, "sha256", expected["sha256"]?.GetValue<string>(), actual.CanonicalSha256);
            Cmp(v.Id, "canonical_bytes_hex", expected["canonical_bytes_hex"]?.GetValue<string>(), actual.CanonicalBytesHex);
            Cmp(v.Id, "canonical_text", expected["canonical_text"]?.GetValue<string>(), actual.CanonicalText);
            Cmp(v.Id, "error_code", expected["error_code"]?.GetValue<string>(), actual.ErrorCode);
            Cmp(v.Id, "value", expected["value"]?.GetValue<string>(), actual.Value);

            var eh = expected["hashes"]?.AsArray().Select(x => x!.GetValue<string>()).ToList();
            var ah = actual.Hashes?.ToList();
            if (eh is not null || ah is not null)
                Cmp(v.Id, "hashes",
                    eh is null ? null : string.Join(",", eh),
                    ah is null ? null : string.Join(",", ah));
        }

        foreach (var id in order)
            if (!vectors.Any(v => v.Id == id))
                drifts.Add(new Drift(id, "removed", "present in golden file", "(not in code)"));

        return drifts;
    }

    /// <summary>فحوص بنيوية على المجموعة نفسها، مستقلة عن الملف المخزَّن.</summary>
    public static IReadOnlyList<string> StructuralChecks(IReadOnlyList<GoldenVector> vectors)
    {
        var problems = new List<string>();

        var dupes = vectors.GroupBy(v => v.Id, StringComparer.Ordinal).Where(g => g.Count() > 1);
        foreach (var g in dupes) problems.Add($"معرّف متجه مكرّر: {g.Key}");

        foreach (var v in vectors)
        {
            GoldenResult r;
            try { r = v.Execute(); }
            catch (Exception ex) { problems.Add($"{v.Id}: رمى {ex.GetType().Name}: {ex.Message}"); continue; }

            switch (r.Kind)
            {
                case GoldenKind.Reject when r.ErrorCode == "NOT-REJECTED":
                    problems.Add($"{v.Id}: كان يجب أن يُرفض ولم يُرفض");
                    break;
                case GoldenKind.SameHash when r.CanonicalSha256 == "DIVERGED":
                    problems.Add($"{v.Id}: كان يجب أن تتطابق البصمات ولم تتطابق: {string.Join(" ", r.Hashes ?? [])}");
                    break;
                case GoldenKind.DifferentHash when r.CanonicalSha256 == "COLLISION":
                    problems.Add($"{v.Id}: كان يجب أن تختلف البصمات ووقع تصادم: {string.Join(" ", r.Hashes ?? [])}");
                    break;
                case GoldenKind.Bytes when r.CanonicalSha256 is null || r.CanonicalBytesHex is null:
                    problems.Add($"{v.Id}: متجه بايتات بلا بايتات");
                    break;
            }
        }

        // فحص خاص: تطبيع البحث يجب أن يطوي كل أشكال الألف إلى مفتاح واحد
        try
        {
            var folded = vectors.FirstOrDefault(v => v.Id == "search.normalisation.folds.all.five.to.one.key");
            if (folded is not null && folded.Execute().Value?.Contains('|') == true)
                problems.Add("search.normalisation: أشكال الألف لم تُطوَ إلى مفتاح واحد");
        }
        catch (Exception ex)
        {
            problems.Add("search.normalisation: رمى " + ex.GetType().Name + ": " + ex.Message);
        }

        return problems;
    }

    /// <summary>يقرأ الملف الذهبي من القرص، أو يعيد <c>null</c>.</summary>
    public static string? TryRead(string path) => File.Exists(path) ? File.ReadAllText(path, Utf8) : null;

    /// <summary>يكتب الملف الذهبي بترميز UTF-8 بلا BOM ونهايات أسطر LF.</summary>
    public static void Write(string path, string json)
        => File.WriteAllBytes(path, Utf8.GetBytes(json.Replace("\r\n", "\n", StringComparison.Ordinal)));
}
