using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace Babel.Ledger.PostingMatrix;

/// <summary>
/// المصفوفة كما تُقرأ من <c>data/posting-matrix</c>، مضمَّنة في التجميعة.
/// <para>
/// <b>لماذا بيانات لا كود:</b> تعديل قاعدة ترحيل يصير تعديل صفّ، لا تعديل كود
/// ونشر إصدار (<c>data/README.md</c> §0). والوحدة تسمّي الحدث؛ ولا تسمّي حساباً
/// ولا جانباً ولا مبلغ سطر.
/// </para>
/// <para>
/// <b>وما لا يفعله المُحمِّل:</b> لا يُصلح ولا يُخمّن. حقل ناقص أو نوع سطر غير
/// مدعوم يرفع استثناءً عند التحميل — أي عند الإقلاع — لا يُتجاوَز بصمت.
/// </para>
/// </summary>
internal sealed class MatrixCatalog
{
    private const string EventPrefix = "Babel.Ledger.Matrix.Events.";
    private const string GuardResource = "Babel.Ledger.Matrix.guard-rules.json";

    private static readonly Lazy<MatrixCatalog> Shared = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly FrozenDictionary<string, MatrixEvent> _events;

    private MatrixCatalog(FrozenDictionary<string, MatrixEvent> events, IReadOnlyList<GuardRule> guards)
    {
        _events = events;
        GuardRules = guards;
    }

    /// <summary>المصفوفة المشتركة — تُقرأ مرّة واحدة لكل عملية.</summary>
    public static MatrixCatalog Default => Shared.Value;

    /// <summary>قواعد الحجب مرتّبة كما وردت في الملف.</summary>
    public IReadOnlyList<GuardRule> GuardRules { get; }

    /// <summary>عدد الأحداث المحمّلة — يُستخدم في اختبار «القاعدة ليست فارغة».</summary>
    public int EventCount => _events.Count;

    /// <summary>كل رموز الأحداث.</summary>
    public IEnumerable<string> EventCodes => _events.Keys;

    /// <summary>يجلب قالب حدث، أو <c>null</c> إن لم يكن في المصفوفة.</summary>
    public MatrixEvent? Find(string eventCode)
        => _events.TryGetValue(eventCode, out MatrixEvent? found) ? found : null;

    private static MatrixCatalog Load()
    {
        Assembly assembly = typeof(MatrixCatalog).Assembly;
        Dictionary<string, MatrixEvent> events = new(StringComparer.Ordinal);

        foreach (string name in assembly.GetManifestResourceNames())
        {
            if (!name.StartsWith(EventPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream stream = assembly.GetManifestResourceStream(name)!;
            using JsonDocument document = JsonDocument.Parse(stream);

            foreach (JsonElement element in document.RootElement.GetProperty("events").EnumerateArray())
            {
                MatrixEvent parsed = ReadEvent(element);
                if (!events.TryAdd(parsed.EventCode, parsed))
                {
                    throw new InvalidOperationException(
                        $"رمز حدث مكرّر في المصفوفة: {parsed.EventCode}. / Duplicate matrix event code.");
                }
            }
        }

        List<GuardRule> guards = [];
        using (Stream stream = assembly.GetManifestResourceStream(GuardResource)!)
        {
            using JsonDocument document = JsonDocument.Parse(stream);
            foreach (JsonElement element in document.RootElement.GetProperty("rules").EnumerateArray())
            {
                guards.Add(ReadGuard(element));
            }
        }

        return new MatrixCatalog(events.ToFrozenDictionary(StringComparer.Ordinal), guards);
    }

    private static MatrixEvent ReadEvent(JsonElement element)
    {
        Dictionary<string, MatrixAmount> amounts = new(StringComparer.Ordinal);
        if (element.TryGetProperty("amounts", out JsonElement amountsElement))
        {
            foreach (JsonProperty property in amountsElement.EnumerateObject())
            {
                amounts[property.Name] = new MatrixAmount(
                    property.Name,
                    Text(property.Value, "name_ar"),
                    Text(property.Value, "name_en"),
                    Text(property.Value, "derivation_ar"));
            }
        }

        Dictionary<string, MatrixCondition> conditions = new(StringComparer.Ordinal);
        if (element.TryGetProperty("conditions", out JsonElement conditionsElement)
            && conditionsElement.ValueKind == JsonValueKind.Object)
        {
            foreach (JsonProperty property in conditionsElement.EnumerateObject())
            {
                conditions[property.Name] = new MatrixCondition(
                    property.Name,
                    Text(property.Value, "name_ar"),
                    Text(property.Value, "name_en"),
                    Text(property.Value, "expression"));
            }
        }

        List<MatrixLine> lines = [];
        if (element.TryGetProperty("lines", out JsonElement linesElement))
        {
            foreach (JsonElement line in linesElement.EnumerateArray())
            {
                lines.Add(new MatrixLine
                {
                    LineNo = line.GetProperty("line_no").GetInt32(),
                    LineKind = Text(line, "line_kind"),
                    Role = Text(line, "role"),
                    QualifierSource = TextOrNull(line, "qualifier_source"),
                    Side = Text(line, "side"),
                    Amount = Text(line, "amount"),
                    Dimensions = ReadStrings(line, "dimensions"),
                    Subledger = TextOrNull(line, "subledger"),
                    When = TextOrNull(line, "when"),
                    NoteAr = Text(line, "note_ar"),
                    NoteEn = Text(line, "note_en"),
                });
            }
        }

        List<string> caveats = [];
        if (element.TryGetProperty("caveats", out JsonElement caveatsElement)
            && caveatsElement.ValueKind == JsonValueKind.Array)
        {
            // التحفّظ يُنقل كما هو — بما فيه علامة ⚠️. نقل حكم من وثيقة تقول
            // «هذا فهم أولي يحتاج تحققاً» إلى بيانات تُنفَّذ دون التحفّظ معه هو
            // تحويل شكّ إلى يقين زائف (data/README.md §6).
            caveats.AddRange(caveatsElement.EnumerateArray().Select(static c => Text(c, "text_ar")));
        }

        return new MatrixEvent
        {
            EventCode = Text(element, "event_code"),
            NameAr = Text(element, "name_ar"),
            NameEn = Text(element, "name_en"),
            Module = Text(element, "module"),
            Status = Text(element, "status"),
            SourceRef = Text(element, "source_ref"),
            PostsEntry = element.TryGetProperty("posts_entry", out JsonElement posts) && posts.GetBoolean(),
            Amounts = amounts,
            Conditions = conditions,
            Lines = lines,
            Caveats = caveats,
        };
    }

    private static GuardRule ReadGuard(JsonElement element)
    {
        JsonElement applies = element.GetProperty("applies_to");
        return new GuardRule
        {
            RuleId = Text(element, "rule_id"),
            NameAr = Text(element, "name_ar"),
            NameEn = Text(element, "name_en"),
            Severity = Text(element, "severity"),
            AppliesTo = new GuardApplicability(
                Text(applies, "kind"),
                TextOrNull(applies, "role"),
                TextOrNull(applies, "property"),
                applies.TryGetProperty("equals", out JsonElement equals)
                    ? equals.ValueKind switch
                    {
                        JsonValueKind.True => "true",
                        JsonValueKind.False => "false",
                        _ => equals.ToString(),
                    }
                    : null,
                applies.TryGetProperty("non_empty", out JsonElement nonEmpty) && nonEmpty.GetBoolean()),
            Condition = Text(element, "condition"),
            MessageAr = Text(element, "message_ar"),
            MessageEn = Text(element, "message_en"),
        };
    }

    private static IReadOnlyList<string> ReadStrings(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement array) && array.ValueKind == JsonValueKind.Array
            ? [.. array.EnumerateArray().Select(static item => item.GetString() ?? string.Empty)]
            : [];

    private static string Text(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static string? TextOrNull(JsonElement element, string name)
        => element.TryGetProperty(name, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
}
