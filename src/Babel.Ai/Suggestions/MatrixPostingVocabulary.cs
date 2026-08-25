using System.Collections.Frozen;
using System.Reflection;
using System.Text.Json;

namespace Babel.Ai.Suggestions;

/// <summary>
/// المفردات مقروءةً من <c>data/posting-matrix</c> مضمَّنةً في التجميعة.
/// <para>
/// <b>ما يُقرأ هنا وما لا يُقرأ:</b> تُقرأ <b>رموز</b> الأحداث و<b>رموز</b> الأدوار وحدها.
/// ولا تُقرأ <c>role-map.default.csv</c> ولا أي خريطة مستأجر — <b>لأنها وحدها التي تحمل
/// أرقام الحسابات</b>. أي أن هذه الوحدة لا تملك في تجميعتها ما تُسمّي به حساباً حتى لو
/// أرادت، وهذا إنفاذ بنيوي للقاعدة المعمارية 2 لا اتفاق.
/// </para>
/// <para>
/// والتحميل يرمي عند أول عيب: مورد ناقص أو حدث بلا رمز يُسقط الإقلاع ولا يُتجاوَز بصمت،
/// لأن مفرداتٍ ناقصة تجعل الرفض «رمز غير معروف» يقع على رمز صحيح.
/// </para>
/// </summary>
public sealed class MatrixPostingVocabulary : IPostingVocabulary
{
    private const string EventPrefix = "Babel.Ai.Matrix.Events.";
    private const string RolesResource = "Babel.Ai.Matrix.roles.csv";

    private static readonly Lazy<MatrixPostingVocabulary> Shared = new(Load, LazyThreadSafetyMode.ExecutionAndPublication);

    private readonly FrozenSet<string> _events;
    private readonly FrozenSet<string> _roles;

    private MatrixPostingVocabulary(FrozenSet<string> events, FrozenSet<string> roles)
    {
        _events = events;
        _roles = roles;
    }

    /// <summary>المفردات المشتركة — تُقرأ مرّة واحدة لكل عملية.</summary>
    public static MatrixPostingVocabulary Default => Shared.Value;

    /// <inheritdoc />
    public int EventCount => _events.Count;

    /// <inheritdoc />
    public int RoleCount => _roles.Count;

    /// <inheritdoc />
    public bool KnowsEvent(string eventCode) => eventCode is not null && _events.Contains(eventCode);

    /// <inheritdoc />
    public bool KnowsRole(string roleCode) => roleCode is not null && _roles.Contains(roleCode);

    private static MatrixPostingVocabulary Load()
    {
        Assembly assembly = typeof(MatrixPostingVocabulary).Assembly;
        HashSet<string> events = new(StringComparer.Ordinal);

        foreach (string name in assembly.GetManifestResourceNames().Order(StringComparer.Ordinal))
        {
            if (!name.StartsWith(EventPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            using Stream stream = assembly.GetManifestResourceStream(name)!;
            using JsonDocument document = JsonDocument.Parse(stream);

            foreach (JsonElement element in document.RootElement.GetProperty("events").EnumerateArray())
            {
                string code = element.GetProperty("event_code").GetString()
                    ?? throw new InvalidOperationException("حدث بلا رمز في المصفوفة. / A matrix event without a code.");
                events.Add(code);
            }
        }

        HashSet<string> roles = new(StringComparer.Ordinal);
        using (Stream stream = assembly.GetManifestResourceStream(RolesResource)
            ?? throw new InvalidOperationException("مورد الأدوار غير مضمَّن. / The roles resource is not embedded."))
        {
            using StreamReader reader = new(stream);
            bool header = true;

            while (reader.ReadLine() is { } line)
            {
                if (header)
                {
                    header = false;
                    continue;
                }

                int comma = line.IndexOf(',', StringComparison.Ordinal);
                if (comma > 0)
                {
                    roles.Add(line[..comma]);
                }
            }
        }

        if (events.Count == 0 || roles.Count == 0)
        {
            throw new InvalidOperationException(
                FormattableString.Invariant($"المفردات ضامرة: {events.Count} حدثاً و{roles.Count} دوراً. ")
                + "ومفرداتٌ فارغة تجعل كل اقتراح صحيح مرفوضاً. / "
                + FormattableString.Invariant($"the vocabulary is empty: {events.Count} events and {roles.Count} roles."));
        }

        return new MatrixPostingVocabulary(
            events.ToFrozenSet(StringComparer.Ordinal),
            roles.ToFrozenSet(StringComparer.Ordinal));
    }
}
