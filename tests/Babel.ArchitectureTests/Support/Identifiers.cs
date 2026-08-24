using System.Text.RegularExpressions;

namespace Babel.ArchitectureTests.Support;

/// <summary>تقطيع المعرّفات إلى كلمات، لأن مطابقة النص الخام تعطي إيجابيات كاذبة.</summary>
internal static partial class Identifiers
{
    [GeneratedRegex(@"[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+", RegexOptions.CultureInvariant)]
    private static partial Regex WordPattern { get; }

    /// <summary>يقطّع معرّفاً (PascalCase أو snake_case أو مساراً بنقاط) إلى كلماته.</summary>
    public static IEnumerable<string> Words(string identifier)
    {
        if (string.IsNullOrEmpty(identifier))
        {
            yield break;
        }

        foreach (Match match in WordPattern.Matches(identifier))
        {
            yield return match.Value;
        }
    }

    /// <summary>هل يحتوي المعرّف على إحدى الكلمات المعطاة ككلمة كاملة؟</summary>
    public static bool ContainsWord(string identifier, IReadOnlySet<string> words) =>
        Words(identifier).Any(word => words.Contains(word));
}
