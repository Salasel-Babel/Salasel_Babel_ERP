using Babel.Contracts.Voice;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>تحديد المقطع — قاعدةٌ كُلّية لا تستطيع أن ترفض، ولا أن تختار بين مقطعين.</b>
/// <para>
/// <b>العطل الذي أغلقه هذا الملفّ، مقيساً:</b> في جملة «سجل سند قبض <b>من العميل</b> شركة
/// النور الاولى للمقاولات <b>لصالح</b> مؤسسة الرياض بمبلغ ألف ريال نقد اليوم» تُعلن شريحة
/// <c>customer</c> أربعة دلائل — «العميل» و«عميل» و«من» و«لصالح» — وكان القارئ يطوي دلائل
/// <b>كل</b> الشرائح في مجموعة حدودٍ واحدة، فصار «لصالح» — وهو دليلُ الشريحة نفسها — حدّاً
/// يقطع اسمَها. ثم يجرّب المواضعَ <b>بترتيب إعلان الدلائل</b> ويعود بأول ما أنتج كلمات.
/// فكان الجواب صحيحاً <b>بحادثة ترتيبٍ في مصفوفة</b>: أُعيد ترتيبُ المصفوفة — أو أُضيفت
/// طبقةٌ تُعيد النظر في المواضع — فخرج «مؤسسة الرياض» طرفاً للمستند، <b>بلا عطلٍ واحد
/// وبوّابةٌ تقبل</b>.
/// </para>
/// <para>
/// <b>وثلاث قواعد تُبطل ذلك بنيوياً:</b>
/// <list type="number">
///   <item><b>الأبكر بموضع الكلمة</b> لا بترتيب الإعلان — فلا يبقى للترتيب أثر.</item>
///   <item><b>دليلُ الشريحة نفسها ليس حدّاً</b> — فلا يُقطع الاسم عند كلمةٍ تدلّ عليه.</item>
///   <item><b>مقطعٌ واحد يُحمَل كما هو</b> — لا يُقارَن مقطعان ولا يُفضَّل أحدهما.</item>
/// </list>
/// </para>
/// <para>
/// <b>وما ليس حدّاً هنا، وقد كان:</b> كلماتُ الإيقاف العامّة <b>وكلماتُ التاريخ</b>. اسمٌ
/// مشروع فيه كلمةٌ منها — «مؤسسة <b>اليوم</b> للدعاية» و«شركة النور <b>على</b> البحر» — كان
/// يُقصّ عند تلك الكلمة فيخرج «مؤسسة» طرفاً. <b>ورفضُ اسمٍ حقيقي عطلٌ آخر لا عطلٌ أصغر</b>،
/// وقصُّه إلى جزئه الأول أخبثُ منه لأنه <b>يمرّ</b>.
/// </para>
/// <para>
/// <b>والكفّتان وُزنتا ولم تُفترضا.</b> مقطعٌ أطول من اللازم — «مؤسسة الرياض <b>اليوم</b>» —
/// يذهب إلى السجلّ فيطابق «مؤسسة الرياض» بتشابهٍ ثلاثيّ عالٍ، فيُحلّ صحيحاً أو يُسأل عنه؛
/// ومقطعٌ أقصر — «مؤسسة» — يذهب جذعاً عامّاً فيطابق <b>طرفاً آخر</b> بمِقبضٍ صحيح. الأول
/// يتدهور تدهوراً لطيفاً، والثاني يُعيد العطل الذي أُغلق. فالحدُّ هنا <b>دليلُ شريحةٍ
/// أخرى</b> (عبارةً كاملة) أو <b>عددٌ أو وحدة</b> — وكلاهما بدايةُ قيمةٍ لحقلٍ آخر، لا
/// كلمةٌ قد تقع داخل اسم.
/// </para>
/// </summary>
public static class SpokenSpans
{
    /// <summary>
    /// يحدّد مقطع الشريحة، أو <c>null</c> إن لم يُنطق لها دليل أو لم يبقَ بعده كلام.
    /// <b>ولا يستطيع هذا التوقيع أن يُعبّر عن رفض</b> — والرفض يعيش فوق الحلقة لا داخلها،
    /// فلا يوجد موضعٌ نحويّ يُكتب فيه «ارفض ثم تابع إلى الدليل التالي».
    /// </summary>
    /// <param name="slot">الشريحة.</param>
    /// <param name="words">كلمات الجملة، مُجرَّدةً تجريداً أميناً.</param>
    /// <param name="foreign">دلائلُ الشرائح الأخرى في النيّة — <b>عباراتٍ لا كلماتٍ مبعثرة</b>.</param>
    public static SpokenSpan? Locate(
        VoiceSlot slot,
        IReadOnlyList<string> words,
        IReadOnlyList<IReadOnlyList<string>> foreign)
    {
        ArgumentNullException.ThrowIfNull(slot);
        ArgumentNullException.ThrowIfNull(words);
        ArgumentNullException.ThrowIfNull(foreign);

        HashSet<string> own = CueWords(slot);

        // ‏**بترتيب ورود الكلمات لا بترتيب إعلان الدلائل** — وهو الفرق الذي يُبطل
        // اعتمادَ الجواب على ترتيب مصفوفة. ويُجرَّب الموضع الأبكر أوّلاً، فإن لم يبقَ
        // بعده كلامٌ يخصّ هذه الشريحة جُرِّب الذي يليه **بترتيب الجملة**.
        //
        // ‏**ولا مفاضلة بين مقطعين**: أوّل موضعٍ يُنتج مقطعاً يفوز، ولا يُقارَن مقطعُه
        // بمقطعِ موضعٍ آخر بطولٍ ولا بتشابهٍ ولا بأي قاعدة. والفرق بين هذا وبين ما
        // حُذف أنّ الترتيب هنا **خاصّيةُ الجملة** لا خاصّيةُ مصفوفةٍ يكتبها مؤلّف.
        List<int> positions = [.. Positions(slot, words).Distinct().Order()];

        foreach (int position in positions)
        {
            int start = position;

            // ‏تخطّي دلائل الشريحة نفسها في **رأس** المقطع. «من العميل، مؤسسة الرياض»:
            // الأبكر هو ما بعد «من»، وما يليه «العميل» — وهو دليلُ الشريحة نفسها، ولمّا
            // لم يعد حدّاً صار يدخل الاسم. فيُتخطّى في الرأس ولا يُقطع به في الجوف.
            while (start < words.Count && own.Contains(VoiceText.Fold(words[start])))
            {
                start++;
            }

            int end = start;
            while (end < words.Count && !Terminates(words, end, foreign))
            {
                end++;
            }

            if (end > start)
            {
                return new SpokenSpan(string.Join(' ', words.Skip(start).Take(end - start)), start, end);
            }
        }

        return null;
    }

    /// <summary>
    /// حدودُ ما سوى هذه الشريحة: دلائلُ الشرائح الأخرى في النيّة نفسها، <b>عباراتٍ كاملة</b>.
    /// <para>
    /// <b>وتُحسب لكل شريحة على حدة</b> — لا مجموعةٌ واحدة تُطوى فيها الشريحة مع جاراتها،
    /// وتلك الطيّةُ بعينها هي التي جعلت «لصالح» يقطع اسم العميل.
    /// </para>
    /// <para>
    /// <b>وعباراتٌ لا كلماتٌ مبعثرة، وذلك مقيس:</b> «على الفاتورة» دليلُ رقم الفاتورة،
    /// و«على العميل» دليلُ العميل — وكلاهما يبدأ بـ«على». وتفكيكُ الدليلين إلى كلمات
    /// يجعل «على» حدّاً لا حدّاً في آنٍ واحد، فيمتدّ اسم العميل كلمةً زائدة في
    /// «للعميل مؤسسة الرياض <b>على</b> الفاتورة رقم 3120». والمطابقةُ بالعبارة عند
    /// الموضع تُنهي المقطع حيث يبدأ حقلٌ آخر فعلاً — <b>لا حيث تصادف كلمةٌ مشتركة</b>.
    /// </para>
    /// </summary>
    /// <param name="intent">النيّة.</param>
    /// <param name="slot">الشريحة.</param>
    public static IReadOnlyList<IReadOnlyList<string>> ForeignCues(VoiceIntent intent, VoiceSlot slot)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(slot);

        List<IReadOnlyList<string>> foreign = [];

        foreach (VoiceSlot other in intent.Slots)
        {
            if (string.Equals(other.Name, slot.Name, StringComparison.Ordinal))
            {
                continue;
            }

            foreach (string cue in other.Cues)
            {
                string[] parts = [.. VoiceText.Words(cue).Select(VoiceText.Fold)];
                if (parts.Length > 0)
                {
                    foreign.Add(parts);
                }
            }
        }

        return foreign;
    }

    /// <summary>كلماتُ دلائل شريحةٍ واحدة، مطويّة.</summary>
    private static HashSet<string> CueWords(VoiceSlot slot)
    {
        HashSet<string> words = new(StringComparer.Ordinal);

        foreach (string cue in slot.Cues)
        {
            foreach (string word in VoiceText.Words(cue))
            {
                words.Add(VoiceText.Fold(word));
            }
        }

        return words;
    }

    /// <summary>
    /// هل يبدأ عند هذا الموضع <b>حقلٌ آخر</b>؟ وهو وحده ما ينهي المقطع.
    /// <b>ودلائلُ الشريحة نفسها ليست هنا إطلاقاً</b> — وهو رأس التحويل كلّه.
    /// </summary>
    private static bool Terminates(
        IReadOnlyList<string> words,
        int at,
        IReadOnlyList<IReadOnlyList<string>> foreign)
    {
        string word = words[at];

        if (ArabicSpokenNumber.CanRead(word) || VoiceUnits.IsUnit(word))
        {
            return true;
        }

        foreach (IReadOnlyList<string> phrase in foreign)
        {
            if (at + phrase.Count > words.Count)
            {
                continue;
            }

            bool hit = true;

            for (int offset = 0; offset < phrase.Count; offset++)
            {
                if (!string.Equals(VoiceText.Fold(words[at + offset]), phrase[offset], StringComparison.Ordinal))
                {
                    hit = false;
                    break;
                }
            }

            if (hit)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>مواضع ما بعد كل دليلٍ لهذه الشريحة، بموضع الكلمة.</summary>
    private static IEnumerable<int> Positions(VoiceSlot slot, IReadOnlyList<string> words)
    {
        foreach (string cue in slot.Cues)
        {
            IReadOnlyList<string> parts = VoiceText.Words(cue);
            if (parts.Count == 0)
            {
                continue;
            }

            for (int index = 0; index + parts.Count <= words.Count; index++)
            {
                bool hit = true;

                for (int offset = 0; offset < parts.Count; offset++)
                {
                    if (!VoiceText.Same(words[index + offset], parts[offset]))
                    {
                        hit = false;
                        break;
                    }
                }

                if (hit)
                {
                    yield return index + parts.Count;
                }
            }
        }
    }
}
