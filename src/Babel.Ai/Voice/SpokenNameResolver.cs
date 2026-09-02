using Babel.Ai.Lookup;
using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>الحالُّ — يسأل السجلَّ المحلّي عن كل مقطعٍ معلَّق، مرّةً واحدة، ثم يقفل الحالة.</b>
/// <para>
/// وهو الطرف الثاني من التحويل: القارئ يحمل ما سمع، <b>وهذا يحمله إلى السجلّ</b>. ولا
/// طريق ثالث: لا مطابقةٌ نصّية، ولا «أقرب شبيه»، ولا قاعدةُ فضِّ تعادل. الجواب واحدٌ من
/// ثلاثة كما يعرّفها <see cref="NameRegisterLookup"/> — لا شيء · واحدٌ ومعه مِقبض · سؤال —
/// وكلٌّ منها يصير حالةً <b>نهائية</b> في <see cref="SlotReading"/>.
/// </para>
/// <para>
/// <b>وبنية الحلقة هي الحارس:</b> تُبنى مجموعةٌ جديدة بـ<c>Add</c>، مدخلاً لكل شريحة،
/// <b>ولا تُكتب شريحةٌ مرّتين</b>. فلا يوجد شكلٌ نحويّ يُكتب فيه «إن رُفض فجرّب مقطعاً
/// آخر»: المقطع مفردٌ، والجواب مفرد، والمدخل يُكتب مرّةً ثم يُقفل.
/// </para>
/// <para>
/// <b>وما لا يفعله:</b> لا يسمّي الصفّ المطابَق. المنفذ الذي يُعيد أسماءً
/// (المنفذ الذي يُعيد أسماءً) محظورٌ على هذا المشروع بحارسٍ قائم، وتسميةُ الصفّ
/// على الشاشة فعلُ طبقة التركيب لا فعلُ وحدة الذكاء.
/// </para>
/// </summary>
public static class SpokenNameResolver
{
    /// <summary>
    /// يحلّ كل الأطراف المعلَّقة في نتيجةٍ واحدة، ويعيد نتيجةً جديدة بملخّصٍ ورمزٍ محدَّثَين.
    /// </summary>
    /// <param name="resolution">ما فهمه القارئ.</param>
    /// <param name="lookup">البحث في السجلّات المحلّية.</param>
    /// <param name="session">الجلسة — منشأتها وشركتها من بيانات الاعتماد لا من الكلام.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public static async Task<Result<VoiceResolution>> ResolveAsync(
        VoiceResolution resolution,
        NameRegisterLookup lookup,
        LookupSession session,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(lookup);
        ArgumentNullException.ThrowIfNull(session);

        VoiceIntent intent = resolution.Intent;
        Dictionary<string, SlotReading> answered = new(StringComparer.Ordinal);

        foreach (VoiceSlot slot in intent.Slots)
        {
            SlotReading reading = resolution.Readings[slot.Name];

            if (reading is not SlotReading.Pending pending)
            {
                answered.Add(slot.Name, reading);
                continue;
            }

            Result<NameLookupResult> answer = await lookup
                .ResolveAsync(pending.RegisterKey, pending.Span.Text, session, cancellationToken)
                .ConfigureAwait(false);

            // ‏سجلٌّ غير مسجَّل أو نصٌّ يطوى إلى فراغ: رفضٌ مُسمّى بأخطائه كما جاءت،
            // ولا سقوطٌ إلى «لا مطابق» — الفرق بين «لم أجد» و«لم أستطع أن أبحث» فرقٌ يُقال.
            if (answer.IsFailure)
            {
                answered.Add(slot.Name, new SlotReading.Refused(answer.Errors[0]));
                continue;
            }

            answered.Add(slot.Name, answer.Value.Outcome switch
            {
                NameLookupOutcome.Resolved =>
                    new SlotReading.Resolved(slot.Name, answer.Value.Handle!, pending.Span),

                NameLookupOutcome.None =>
                    new SlotReading.Refused(VoiceRefusals.NameNotInRegister(slot, pending.Span.Text)),

                NameLookupOutcome.NeedsQuestion =>
                    new SlotReading.Asked(slot.Name, answer.Value.QuestionId!, pending.Span),

                _ => throw new ArgumentOutOfRangeException(
                    nameof(resolution), answer.Value.Outcome, "حالةُ بحثٍ خارج المفردات المغلقة."),
            });
        }

        IReadOnlyList<SpokenSlotValue> values =
            [.. answered.Values.OfType<SlotReading.Filled>().Select(static filled => filled.Value)];

        string readbackAr = VoiceReadback.Arabic(intent, values, answered);

        Result disclosure = VoiceDisclosure.Guard(readbackAr);
        if (disclosure.IsFailure)
        {
            return Result<VoiceResolution>.Failure(disclosure.Errors);
        }

        return Result<VoiceResolution>.Success(new VoiceResolution(
            intent,
            answered,
            resolution.CompanyCueHeard,
            readbackAr,
            VoiceReadback.Token(intent, answered)));
    }
}
