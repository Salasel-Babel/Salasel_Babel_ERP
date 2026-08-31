using System.Text;

namespace Babel.Ai.Voice;

/// <summary>
/// تجريد النصّ العربي — <b>على مرتبتين، وللفرق بينهما ثمن يُدفع في الشاشة</b>.
/// <list type="number">
///   <item>
///     <b><see cref="Strip"/> — التجريد الأمين</b>: تشكيلٌ وتطويلٌ وتوحيدُ الألف والألف
///     المقصورة، بالقواعد نفسها التي يجرّد بها قاموس الأعداد المنطوقة ونظيرُه في
///     المتصفّح. وهو ما <b>يُحفَظ ويُعرَض</b>: اسم موردٍ يخرج من هنا يبقى «مؤسسة النور»
///     ولا يصير «موسسه النور».
///   </item>
///   <item>
///     <b><see cref="Fold"/> — التجريد للمطابقة وحدها</b>: يضيف التاء المربوطة إلى الهاء،
///     لأن التفريغ الصوتي يكتب «فاتوره» و«فاتورة» بلا قاعدة. وهو <b>لا يُعرَض أبداً</b>.
///   </item>
/// </list>
/// <para>
/// <b>ولماذا مرتبتان لا واحدة:</b> مرتبةٌ واحدة أمينة تُسقط نصف المطابقات؛ ومرتبةٌ
/// واحدة عدوانية تكتب في المستند اسماً لم يقله أحد. والقاعدة: <b>يُطابَق بالمطوي،
/// ويُحفَظ الأمين</b>.
/// </para>
/// </summary>
public static class VoiceText
{
    /// <summary>التجريد الأمين — ما يُعرض ويُحفَظ.</summary>
    /// <param name="word">الكلمة أو الجملة.</param>
    public static string Strip(string word)
    {
        ArgumentNullException.ThrowIfNull(word);

        StringBuilder output = new(word.Length);

        foreach (char character in word)
        {
            if (character is >= 'ً' and <= 'ْ' or 'ـ' or 'ٰ')
            {
                continue;
            }

            output.Append(character switch
            {
                'أ' or 'إ' or 'آ' or 'ٱ' => 'ا',
                'ى' => 'ي',
                _ => character,
            });
        }

        return output.ToString();
    }

    /// <summary>التجريد للمطابقة — يُقارَن به ولا يُعرض.</summary>
    /// <param name="word">الكلمة أو الجملة.</param>
    public static string Fold(string word)
    {
        string stripped = Strip(word);
        StringBuilder output = new(stripped.Length);

        foreach (char character in stripped)
        {
            output.Append(character == 'ة' ? 'ه' : character);
        }

        return output.ToString();
    }

    /// <summary>يقطّع جملةً إلى كلمات <b>مُجرَّدةً تجريداً أميناً</b>، ويُسقط الترقيم.</summary>
    /// <param name="text">النصّ.</param>
    public static IReadOnlyList<string> Words(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<string> words = [];

        foreach (string raw in text.Split(
            [' ', '\t', '\n', '\r', '،', ',', '.', '؟', '?', '!', '؛', ';', ':'],
            StringSplitOptions.RemoveEmptyEntries))
        {
            string word = Strip(raw);
            if (word.Length > 0)
            {
                words.Add(word);
            }
        }

        return words;
    }

    /// <summary>هل تظهر العبارة — <b>مطويّةً</b> — داخل النصّ المطويّ؟</summary>
    /// <param name="text">النصّ.</param>
    /// <param name="phrase">العبارة.</param>
    public static bool Contains(string text, string phrase) =>
        Fold(text ?? string.Empty).Contains(Fold(phrase ?? string.Empty), StringComparison.Ordinal);

    /// <summary>هل الكلمتان واحدة بعد الطيّ؟</summary>
    /// <param name="left">الأولى.</param>
    /// <param name="right">الثانية.</param>
    public static bool Same(string left, string right) =>
        string.Equals(Fold(left ?? string.Empty), Fold(right ?? string.Empty), StringComparison.Ordinal);
}
