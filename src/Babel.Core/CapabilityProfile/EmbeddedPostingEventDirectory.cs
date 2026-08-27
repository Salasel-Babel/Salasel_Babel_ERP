using System.Collections.Frozen;
using System.Collections.Immutable;
using System.Reflection;
using System.Text.Json;
using Babel.Contracts.Posting;

namespace Babel.Core.CapabilityProfile;

/// <summary>
/// الفهرس مقروءاً من ملفات المصفوفة نفسها، مضمَّنة في التجميعة.
/// <para>
/// <b>المصدر واحد والقارئ اثنان — وهذا مقصود ومكتوب:</b> ملفات
/// <c>data/posting-matrix/events/*.json</c> هي المصدر الوحيد، ويضمّنها الدفتر ليقرأ منها
/// <b>القالب</b>، وتضمّنها النواة لتقرأ منها <b>الوجود</b> وحده. ولا تستطيع النواة أن
/// تسأل الدفتر: اتجاه الاعتماد إلى الأسفل والنواة تحته (القاعدة 3). ولو نُسخت الرموز
/// إلى قائمة مكتوبة بيد هنا لانحرفت عن الملفات عند أول إضافة — وذلك بالضبط صنف العطل
/// الذي يحرسه هذا التصميم كلّه.
/// </para>
/// <para>
/// <b>ولا يُصلح ولا يُخمّن:</b> ملفٌّ بلا مصفوفة <c>events</c>، أو حدثٌ بلا
/// <c>event_code</c>، أو رمز مكرَّر — كلها ترفع استثناءً عند أول قراءة، لا تُتجاوَز بصمت.
/// </para>
/// </summary>
public sealed class EmbeddedPostingEventDirectory : IPostingEventDirectory
{
    private const string ResourcePrefix = "Babel.Core.Matrix.Events.";

    private static readonly Lazy<EmbeddedPostingEventDirectory> Shared =
        new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly FrozenSet<string> _codes;

    private EmbeddedPostingEventDirectory(FrozenSet<string> codes, ImmutableArray<PostingEventCode> ordered)
    {
        _codes = codes;
        Codes = ordered;
    }

    /// <summary>الفهرس المشترك — يُقرأ مرّة واحدة لكل عملية.</summary>
    public static EmbeddedPostingEventDirectory Default => Shared.Value;

    /// <inheritdoc />
    public int Count => _codes.Count;

    /// <inheritdoc />
    public IReadOnlyList<PostingEventCode> Codes { get; }

    /// <inheritdoc />
    public bool Contains(PostingEventCode code) => code.IsAssigned && _codes.Contains(code.Value);

    private static EmbeddedPostingEventDirectory Load()
    {
        Assembly assembly = typeof(EmbeddedPostingEventDirectory).Assembly;
        HashSet<string> codes = new(StringComparer.Ordinal);

        foreach (string name in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            if (!name.StartsWith(ResourcePrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream stream = assembly.GetManifestResourceStream(name)!;
            using JsonDocument document = JsonDocument.Parse(stream);

            if (!document.RootElement.TryGetProperty("events", out JsonElement events))
            {
                throw new InvalidOperationException(
                    $"ملفّ مصفوفة بلا مصفوفة events: {name}. / A matrix file without an events array: {name}.");
            }

            foreach (JsonElement element in events.EnumerateArray())
            {
                if (!element.TryGetProperty("event_code", out JsonElement code)
                    || code.GetString() is not { Length: > 0 } value)
                {
                    throw new InvalidOperationException(
                        $"حدث بلا رمز في {name}. / An event without a code in {name}.");
                }

                if (!codes.Add(value))
                {
                    throw new InvalidOperationException(
                        $"رمز حدث مكرّر في المصفوفة: {value}. / Duplicate matrix event code: {value}.");
                }
            }
        }

        if (codes.Count == 0)
        {
            // فهرس فارغ يجعل كل قدرة «غير مخدومة» — أو، لو قُلبت المقارنة يوماً، يجعل
            // كل قدرة مقبولة. الحالتان كارثة صامتة، والإقلاع يتوقّف قبل أيّهما.
            throw new InvalidOperationException(
                "لم يُقرأ رمز حدث واحد من المصفوفة المضمَّنة — الفهرس ضامر. / "
                + "Not one event code was read from the embedded matrix; the directory is empty.");
        }

        ImmutableArray<PostingEventCode> ordered =
            [.. codes.Order(StringComparer.Ordinal).Select(static value => new PostingEventCode(value))];

        return new EmbeddedPostingEventDirectory(codes.ToFrozenSet(StringComparer.Ordinal), ordered);
    }
}
