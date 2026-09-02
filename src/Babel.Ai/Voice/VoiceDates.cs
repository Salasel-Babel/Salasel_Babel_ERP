using System.Globalization;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>كلماتُ التاريخ المنطوقة — مغلقة، وتُقرأ من موضعين لا من واحد.</b>
/// <para>
/// كانت هذه الكلمات مكتوبةً داخل القارئ وحده. ولمّا صار مقطعُ الاسم ينتهي عند
/// <b>بدايةِ قيمةِ حقلٍ آخر</b>، احتاجها المُحدِّد أيضاً: شريحة التاريخ في هذا السجلّ
/// كثيراً ما تُعلَن <b>بلا دليل واحد</b> (‏<c>receivedOn</c> · <c>issuedOn</c>)، فلو لم
/// تكن «اليوم» حدّاً لابتلعها اسمُ الطرف في كل جملةٍ لا مبلغ فيها.
/// </para>
/// </summary>
public static class VoiceDates
{
    /// <summary>«اليوم» مطويّةً.</summary>
    public static string TodayWord { get; } = VoiceText.Fold("اليوم");

    /// <summary>كلمات الأمس مطويّةً.</summary>
    public static IReadOnlySet<string> YesterdayWords { get; } =
        new HashSet<string>(new[] { "امس", "أمس", "البارحة" }.Select(VoiceText.Fold), StringComparer.Ordinal);

    /// <summary>هل هذه الكلمة تاريخٌ منطوق أو تاريخٌ بصيغة ISO؟</summary>
    /// <param name="word">الكلمة.</param>
    public static bool IsDateWord(string word)
    {
        if (string.IsNullOrEmpty(word))
        {
            return false;
        }

        string folded = VoiceText.Fold(word);

        return string.Equals(folded, TodayWord, StringComparison.Ordinal)
            || YesterdayWords.Contains(folded)
            || DateOnly.TryParseExact(word, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _);
    }
}
