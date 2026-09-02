using Babel.Contracts.Voice;

namespace Babel.Ai.Voice;

/// <summary>
/// أمرٌ اجتاز البوابة. <b>وجودُ هذا الكائن هو الإذن</b> — لا رايةٌ داخله يقرؤها من يشاء.
/// <para>
/// ولا سبيل إلى إنشائه إلا من <see cref="VoiceConfirmationGate"/>: منشِئُه داخلي.
/// فمن نسي أن يسأل البوابة لا يجد ما يمرّره إلى الوحدة المالكة أصلاً — وهو الفرق بين
/// حارسٍ يُفحَص وحارسٍ <b>لا يمكن تجاوزه</b>.
/// </para>
/// <para>
/// <b>وشرائحُه <see cref="ResolvedSlotValue"/> لا <see cref="SpokenSlotValue"/>:</b> الطرف
/// فيها <b>مِقبضٌ ولا شيء غيره</b> — لا نصّ، ولا ما سُمع، ولا اسمٌ من السجلّ. فلا يستطيع
/// أي بانٍ لجسم مسوّدة أن يجد اسماً منطوقاً ليضعه في موضع معرّف، <b>لأنه غير موجود</b>.
/// </para>
/// </summary>
public sealed record VoiceDispatch
{
    internal VoiceDispatch(
        VoiceIntent intent,
        IReadOnlyList<ResolvedSlotValue> slots,
        Guid companyId,
        bool confirmedByHuman)
    {
        Intent = intent;
        Slots = slots;
        CompanyId = companyId;
        ConfirmedByHuman = confirmedByHuman;
    }

    /// <summary>النيّة.</summary>
    public VoiceIntent Intent { get; }

    /// <summary>الشرائح الممتلئة — والطرف فيها مِقبض.</summary>
    public IReadOnlyList<ResolvedSlotValue> Slots { get; }

    /// <summary>المنشأة التي يُنفَّذ فيها.</summary>
    public Guid CompanyId { get; }

    /// <summary>
    /// هل أكّده إنسان صراحةً؟ <c>false</c> للاستعلام والانتقال وحدهما، ولا يكون
    /// <c>false</c> أبداً لعمليةٍ تُغيّر الحال.
    /// </summary>
    public bool ConfirmedByHuman { get; }
}
