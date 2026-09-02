using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// ما فهمه المحرّك من جملةٍ واحدة: النيّة، و<b>قراءةٌ واحدة لكل شريحة معلَنة</b>،
/// والملخّص المرتدّ، ورمزه.
/// <para>
/// <b>وهي ليست إذناً بالتنفيذ.</b> القراءة تنجح ومعها شرائح ناقصة ومعلَّقة عمداً — كي
/// تمتلئ الشاشة أمام المستخدم وهو يتكلّم، وكي يرى <b>ما نقص باسمه</b>. والرفضُ يقع في
/// <see cref="VoiceConfirmationGate"/>، وهو الباب الوحيد إلى التنفيذ.
/// </para>
/// <para>
/// <b>وثلاث القوائم المتوازية انطوت في واحدة.</b> كانت <c>Slots</c> و<c>MissingSlots</c>
/// و<c>Faults</c> ثلاث قوائم <b>يستطيع بعضها أن يناقض بعضاً</b>: شريحةٌ في «الممتلئة»
/// وفي «الناقصة» معاً، أو عطلٌ على شريحةٍ ممتلئة. وصارت <see cref="Readings"/> مجموعةً
/// <b>مفتاحُها اسمُ الشريحة</b>، فيها مدخلٌ واحد لكل شريحةٍ أعلنتها النيّة — لا أقلّ ولا
/// أكثر — وما عداها <b>مشتقٌّ منها</b>. والتناقض صار غير قابلٍ للتعبير.
/// </para>
/// </summary>
public sealed record VoiceResolution
{
    /// <summary>ينشئ النتيجة من قراءاتٍ واحدةٍ لكل شريحة.</summary>
    /// <param name="intent">النيّة المطابَقة.</param>
    /// <param name="readings">القراءات — مدخلٌ لكل شريحةٍ معلَنة.</param>
    /// <param name="companyCueHeard">هل نُطق دليلُ شركة؟</param>
    /// <param name="readbackAr">الملخّص المرتدّ.</param>
    /// <param name="confirmationToken">رمز التأكيد.</param>
    /// <exception cref="ArgumentException">
    /// إن لم تكن القراءات مدخلاً لكل شريحةٍ معلَنة ولا شيء سواها — <b>وذلك يرتفع ولا يُصحَّح</b>:
    /// نتيجةٌ ينقصها مدخلٌ تجعل شريحةً لا تُقرأ ولا تُرفض، فتمرّ صامتة.
    /// </exception>
    public VoiceResolution(
        VoiceIntent intent,
        IReadOnlyDictionary<string, SlotReading> readings,
        bool companyCueHeard,
        string readbackAr,
        string confirmationToken)
    {
        ArgumentNullException.ThrowIfNull(intent);
        ArgumentNullException.ThrowIfNull(readings);

        if (readings.Count != intent.Slots.Count
            || intent.Slots.Any(slot => !readings.ContainsKey(slot.Name)))
        {
            throw new ArgumentException(
                "القراءات ليست مدخلاً واحداً لكل شريحةٍ في النيّة «" + intent.Id + "». "
                + "/ readings must carry exactly one entry per declared slot of intent '" + intent.Id + "'.",
                nameof(readings));
        }

        Intent = intent;
        Readings = readings;
        CompanyCueHeard = companyCueHeard;
        ReadbackAr = readbackAr;
        ConfirmationToken = confirmationToken;
    }

    /// <summary>النيّة المطابَقة.</summary>
    public VoiceIntent Intent { get; }

    /// <summary>
    /// قراءةٌ واحدة لكل شريحةٍ معلَنة — <b>دائماً</b>، حتى للصامتة. مفتاحُها اسم الشريحة.
    /// </summary>
    public IReadOnlyDictionary<string, SlotReading> Readings { get; }

    /// <summary>
    /// هل نُطق دليلُ شركةٍ داخل الأمر؟ <b>والدليل هو الإشارة، لا الاسمُ المُحلَّل</b> —
    /// فلا يُقارَن نصٌّ بنصٍّ حكماً على الهوية.
    /// </summary>
    public bool CompanyCueHeard { get; }

    /// <summary>
    /// الملخّص المرتدّ — يُقرأ ويُعرض معاً. <b>وواحدٌ بالعربية لا اثنان</b> (‏ADR-0021 · القاعدة 14).
    /// </summary>
    public string ReadbackAr { get; }

    /// <summary>
    /// رمز التأكيد: صورةٌ نصّية حتمية للأمر بعينه. تأكيدٌ برمزٍ آخر يُرفض.
    /// <b>وحلُّ شريحةٍ يغيّره</b> — فتأكيدٌ قيل بينما كان السؤال مفتوحاً يُرفض، وهو الصواب.
    /// </summary>
    public string ConfirmationToken { get; }

    /// <summary>الشرائح الممتلئة بقيمةٍ مقروءة — <b>مشتقّة</b>.</summary>
    public IReadOnlyList<SpokenSlotValue> Slots =>
        [.. Intent.Slots
            .Select(slot => Readings[slot.Name])
            .OfType<SlotReading.Filled>()
            .Select(static filled => filled.Value)];

    /// <summary>الشرائح المعلَّقة على السجلّ — <b>مشتقّة</b>.</summary>
    public IReadOnlyList<SlotReading.Pending> Pending =>
        [.. Intent.Slots.Select(slot => Readings[slot.Name]).OfType<SlotReading.Pending>()];

    /// <summary>الأطراف التي وُرِقت لها ورقةُ سؤال — <b>مشتقّة</b>.</summary>
    public IReadOnlyList<SlotReading.Asked> Asked =>
        [.. Intent.Slots.Select(slot => Readings[slot.Name]).OfType<SlotReading.Asked>()];

    /// <summary>الأطراف التي حُلّت في السجلّ — <b>مشتقّة</b>.</summary>
    public IReadOnlyList<SlotReading.Resolved> ResolvedEntities =>
        [.. Intent.Slots.Select(slot => Readings[slot.Name]).OfType<SlotReading.Resolved>()];

    /// <summary>
    /// أسماء الشرائح اللازمة التي لم يُسمع لها شيء — <b>مشتقّة</b>: الصامتة واللازمة معاً.
    /// </summary>
    public IReadOnlyList<string> MissingSlots =>
        [.. Intent.Slots
            .Where(slot => slot.Required && Readings[slot.Name] is SlotReading.Silent)
            .Select(static slot => slot.Name)];

    /// <summary>
    /// الأعطال — <b>مشتقّة</b> من القراءات المرفوضة، كاملةً برسائلها: من يعمل بيدين
    /// مشغولتين يسمع الجملة ولا يقرأ رمزاً.
    /// </summary>
    public IReadOnlyList<Error> Faults =>
        [.. Intent.Slots
            .Select(slot => Readings[slot.Name])
            .OfType<SlotReading.Refused>()
            .Select(static refused => refused.Error)];

    /// <summary>هل امتلأت كل الشرائح اللازمة ولم يبقَ طرفٌ معلَّق؟</summary>
    public bool IsComplete =>
        MissingSlots.Count == 0 && Pending.Count == 0 && Asked.Count == 0 && Faults.Count == 0;
}
