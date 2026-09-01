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

    /// <summary>
    /// علاماتُ الوقف التي <b>تُبقى رموزَ فصلٍ صريحة</b> ولا تُحذف.
    /// <para>
    /// <b>ولماذا تُبقى:</b> النقطةُ والفاصلة أرخصُ إشارةٍ في اللغة على أن هذا المقطع
    /// انتهى، و<b>التفريغُ المكتوب — وهو المسار الوحيد العامل على عنوانٍ غير مؤمَّن —
    /// يحملها فعلاً</b>. وقاطعٌ يحذفها يُتلف المعلومة قبل أن تصل المطابقة، فيصير
    /// «مؤسسة النور، وأضف لها حساباً» مقطعاً واحداً لا مقطعين. وحذفُ إشارةٍ موجودة
    /// أسوأ من عدم فهمها: الأولى تُتلف، والثانية تُترك للحدود الأخرى.
    /// </para>
    /// </summary>
    private static readonly char[] Breaks = ['،', ',', '.', '؟', '?', '!', '؛', ';', ':'];

    /// <summary>الفواصل البيضاء وحدها.</summary>
    private static readonly char[] Blanks = [' ', '\t', '\n', '\r'];

    /// <summary>هل هذه الكلمة علامةَ وقفٍ لا كلمةً؟ <b>وهي حدٌّ في كل مقطعٍ حرّ</b>.</summary>
    /// <param name="word">الرمز المقروء.</param>
    public static bool IsBreak(string word) =>
        word is { Length: 1 } && Array.IndexOf(Breaks, word[0]) >= 0;

    /// <summary>
    /// يقطّع جملةً إلى كلمات <b>مُجرَّدةً تجريداً أميناً</b>، و<b>يُبقي علامات الوقف
    /// رموزاً قائمة بذاتها</b> لا يبتلعها مقطعٌ حرّ.
    /// </summary>
    /// <param name="text">النصّ.</param>
    public static IReadOnlyList<string> Words(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        List<string> words = [];

        foreach (string chunk in text.Split(Blanks, StringSplitOptions.RemoveEmptyEntries))
        {
            int start = 0;

            for (int index = 0; index <= chunk.Length; index++)
            {
                bool atBreak = index < chunk.Length && Array.IndexOf(Breaks, chunk[index]) >= 0;
                if (index < chunk.Length && !atBreak)
                {
                    continue;
                }

                if (index > start)
                {
                    string word = Strip(chunk[start..index]);
                    if (word.Length > 0)
                    {
                        words.Add(word);
                    }
                }

                if (atBreak)
                {
                    words.Add(chunk[index].ToString());
                }

                start = index + 1;
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
