using Babel.Ai.Tests.Support;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Ai.Tests.Voice;

/// <summary>
/// <b>الصحّةُ التي كانت حادثةً صارت خاصّية — ويُقاس ذلك بتقليب المصفوفة نفسها.</b>
/// <para>
/// شريحة <c>customer</c> تُعلن أربعة دلائل: «العميل» و«عميل» و«من» و«لصالح». وكان القارئ
/// يجرّب مواضعها <b>بترتيب إعلانها</b> ويعود بأوّل ما أنتج كلمات — فكان جوابُه على الجملة
/// ذات الاسمين صحيحاً <b>لأن «العميل» كُتب قبل «لصالح» في مصفوفة</b>. وهذا الإثبات يقلّب
/// المصفوفة على تباديلها الأربعة والعشرين ويقيس أنّ المقطع <b>واحدٌ حرفاً بحرف</b> في
/// جميعها، على الجملتين معاً. وهو يسقط على القارئ السابق.
/// </para>
/// </summary>
public sealed class CueOrderDoesNotDecideTheParty
{
    private static readonly string[] Cues = ["العميل", "عميل", "من", "لصالح"];

    private static readonly (string Transcript, string Span)[] Sentences =
    [
        ("سجل سند قبض من العميل شركة النور الاولى للمقاولات لصالح مؤسسة الرياض بمبلغ الف ريال نقد اليوم",
         "شركة النور الاولي للمقاولات لصالح مؤسسة الرياض"),
        ("سجل سند قبض من العميل، مؤسسة الرياض بمبلغ ألف ريال نقد اليوم",
         "مؤسسة الرياض"),
    ];

    [Fact]
    public void التباديل_الأربعة_والعشرون_تُنتج_المقطع_نفسه_حرفاً_بحرف()
    {
        VoiceIntent receipt = VoiceHarness.Registry.Find("accounting.customer_receipt.record")!;
        VoiceSlot declared = receipt.Slots.Single(slot => slot.Name == "customer");

        // ‏**حارس لا فراغ**: تقليبٌ على مصفوفةٍ غير المصفوفة الحقيقية لا يقيس شيئاً.
        Assert.Equal(Cues, declared.Cues);

        List<string[]> permutations = [.. Permute(Cues)];
        Assert.Equal(24, permutations.Count);

        foreach ((string transcript, string expected) in Sentences)
        {
            IReadOnlyList<string> words = VoiceText.Words(transcript);

            foreach (string[] order in permutations)
            {
                VoiceSlot shuffled = declared with { Cues = order };
                VoiceIntent intent = receipt with
                {
                    Slots = [.. receipt.Slots.Select(slot => slot.Name == "customer" ? shuffled : slot)],
                };

                SpokenSpan? span = SpokenSpans.Locate(
                    shuffled, words, SpokenSpans.ForeignCues(intent, shuffled));

                Assert.NotNull(span);
                Assert.Equal(expected, span.Text);
            }
        }
    }

    /// <summary>
    /// <b>والموضعُ الأبكر إن لم يُنتج شيئاً لا يُسقط الشريحة</b> — يُجرَّب الذي يليه
    /// <b>بترتيب الجملة</b>.
    /// <para>
    /// في «سجل سند قبض <b>من</b> ألف ريال <b>العميل</b> مؤسسة الرياض نقد اليوم» يقع
    /// الدليل «من» أوّلاً ويليه عددٌ مباشرةً — أي <b>بدايةُ حقلٍ آخر</b> — فلا يُنتج
    /// مقطعاً. ولو وقف التحديد عند الأبكر وحده لقيل «ينقصني العميل» وقد قيل.
    /// </para>
    /// <para>
    /// <b>وهذا ليس عودةً إلى «جرّب حتى ينجح أحدها»:</b> الترتيب هنا <b>خاصّيةُ الجملة</b>
    /// — مواضعُ الكلمات — لا خاصّيةُ مصفوفةٍ يكتبها مؤلّف. ولذلك يبقى التقليب أعلاه
    /// أخضر: أربعةٌ وعشرون تبديلاً تُنتج المقطع نفسه حرفاً بحرف.
    /// </para>
    /// </summary>
    [Fact]
    public void الموضع_الأبكر_إن_لم_يُنتج_شيئاً_يُجرَّب_الذي_يليه_بترتيب_الجملة()
    {
        const string transcript = "سجل سند قبض من ألف ريال العميل مؤسسة الرياض نقد اليوم";

        Result<VoiceResolution> read = SpokenCommandReader.Read(
            transcript, VoiceHarness.Registry, VoiceHarness.Options);

        Assert.True(read.IsSuccess);

        SlotReading.Pending pending = Assert.IsType<SlotReading.Pending>(read.Value.Readings["customer"]);
        Assert.Equal("مؤسسة الرياض", pending.Span.Text);
        Assert.DoesNotContain("customer", read.Value.MissingSlots);
    }

    /// <summary>
    /// <b>الضابط الموجب — وبدونه يمرّ هذا الملفّ على قاعدةٍ لم تكن قابلةً للكسر أصلاً.</b>
    /// <para>
    /// يُعاد بناءُ القاعدة القديمة هنا حرفياً — <b>حدودٌ واحدة تُطوى فيها دلائلُ كل
    /// الشرائح</b> (فيصير «لصالح» حدّاً على شريحته هو)، ثم <b>أوّلُ موضعٍ أنتج كلمات
    /// بترتيب إعلان الدلائل</b> — ويُقاس أنّ التباديل تُنتج بها <b>مقطعين مختلفين</b>.
    /// فالخُضرة أعلاه ليست خُضرةَ اختبارٍ لا يقيس شيئاً.
    /// </para>
    /// </summary>
    [Fact]
    public void القاعدة_القديمة_كانت_تُنتج_مقطعين_مختلفين_باختلاف_الترتيب()
    {
        IReadOnlyList<string> words = VoiceText.Words(Sentences[0].Transcript);
        VoiceIntent receipt = VoiceHarness.Registry.Find("accounting.customer_receipt.record")!;

        HashSet<string> everyCue = new(StringComparer.Ordinal);
        foreach (VoiceSlot slot in receipt.Slots)
        {
            foreach (string cue in slot.Cues)
            {
                foreach (string word in VoiceText.Words(cue))
                {
                    everyCue.Add(VoiceText.Fold(word));
                }
            }
        }

        HashSet<string> produced = new(StringComparer.Ordinal);

        foreach (string[] order in Permute(Cues))
        {
            string? old = OldRule(order, words, everyCue);
            if (old is not null)
            {
                produced.Add(old);
            }
        }

        // ‏**والمقطعان اسمان صحيحا الشكل لطرفين مختلفين** — وهو العطل بعينه.
        Assert.True(produced.Count > 1, string.Join(" | ", produced));
        Assert.Contains("شركة النور الاولي للمقاولات", produced);
        Assert.Contains("مؤسسة الرياض", produced);
    }

    /// <summary>القاعدة المحذوفة: أوّلُ موضعٍ أنتج كلمات، والحدودُ مطويّةٌ لكل الشرائح.</summary>
    private static string? OldRule(string[] cues, IReadOnlyList<string> words, HashSet<string> boundaries)
    {
        foreach (string cue in cues)
        {
            IReadOnlyList<string> parts = VoiceText.Words(cue);

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

                if (!hit)
                {
                    continue;
                }

                List<string> collected = [];
                for (int at = index + parts.Count; at < words.Count; at++)
                {
                    if (boundaries.Contains(VoiceText.Fold(words[at])) || ArabicSpokenNumber.CanRead(words[at]))
                    {
                        break;
                    }

                    collected.Add(words[at]);
                }

                if (collected.Count > 0)
                {
                    return string.Join(' ', collected);
                }
            }
        }

        return null;
    }

    private static IEnumerable<string[]> Permute(string[] source)
    {
        if (source.Length <= 1)
        {
            yield return source;
            yield break;
        }

        for (int index = 0; index < source.Length; index++)
        {
            string[] rest = [.. source.Take(index), .. source.Skip(index + 1)];

            foreach (string[] tail in Permute(rest))
            {
                yield return [source[index], .. tail];
            }
        }
    }
}
