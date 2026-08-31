using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>البوابة — الباب الوحيد من كلامٍ مفهوم إلى أمرٍ يُنفَّذ.</b>
/// <para>
/// وقاعدتها سطرٌ واحد لا استثناء فيه: <b>كل ما يكتب في الدفتر، أو يحرّك مخزوناً، أو
/// يصرف لإنسان، أو يوقّع عقداً — يُقرأ على قائله ثم يُؤكَّد صراحةً قبل أن يمرّ.</b>
/// والاستعلام والانتقال وحدهما يمرّان بلا تأكيد، لأنهما لا يتركان أثراً يُعكَس.
/// </para>
/// <para>
/// <b>ولماذا بوابةٌ لا رايةٌ داخل النتيجة:</b> رايةٌ تُقرأ تُنسى — سطرٌ واحد ينسى أن
/// يسألها فيمرّ كل شيء. والبوابة تُعيد <see cref="VoiceDispatch"/> ومنشِئُه داخلي،
/// فمن لم يمرّ بها <b>لا يملك ما يمرّره</b> إلى الوحدة المالكة. الحارس بنيوي لا انضباطي،
/// على مثال <c>AccountCode</c> في القاعدة 2.
/// </para>
/// <para>
/// <b>وترتيب الرفض مقصود:</b> الصلاحية أولاً — فلا يُقرأ على مستخدمٍ ملخّصُ عمليةٍ لا
/// يملكها فيتعلّم صياغتها؛ ثم القرار المعلَّق؛ ثم الشرائح الناقصة؛ ثم الشركة؛ ثم التأكيد.
/// </para>
/// </summary>
public static class VoiceConfirmationGate
{
    /// <summary>الكلمة المنطوقة التي تُقرأ تأكيداً. مغلقة: ما عداها ليس تأكيداً.</summary>
    public static IReadOnlyList<string> ConfirmWordsAr { get; } = ["تأكيد", "أكّد", "اعتمد", "تمام", "نعم"];

    /// <summary>الكلمة المنطوقة التي تُقرأ إلغاءً.</summary>
    public static IReadOnlyList<string> CancelWordsAr { get; } = ["إلغاء", "ألغِ", "لا", "تراجع"];

    /// <summary>هل هذه الجملة تأكيدٌ منطوق؟ <b>ولا يُقارَب بأقرب شبيه</b>.</summary>
    /// <param name="utterance">ما قيل بعد الملخّص.</param>
    public static bool IsSpokenConfirmation(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return false;
        }

        IReadOnlyList<string> words = VoiceText.Words(utterance);
        return ConfirmWordsAr.Any(word => words.Any(spoken => VoiceText.Same(spoken, word)));
    }

    /// <summary>هل هذه الجملة إلغاء؟</summary>
    /// <param name="utterance">ما قيل.</param>
    public static bool IsSpokenCancellation(string utterance)
    {
        if (string.IsNullOrWhiteSpace(utterance))
        {
            return false;
        }

        IReadOnlyList<string> words = VoiceText.Words(utterance);
        return CancelWordsAr.Any(word => words.Any(spoken => VoiceText.Same(spoken, word)));
    }

    /// <summary>
    /// يأذن — أو يرفض ويسمّي. <b>ويُعيد كل أسباب الرفض لا أوّلها</b>: من يعمل بيدين
    /// مشغولتين لا يعيد المحاولة خمس مرّات ليكتشف خمسة نواقص.
    /// </summary>
    /// <param name="resolution">ما فهمه القارئ.</param>
    /// <param name="caller">المتكلّم ومنشأته وصلاحياته.</param>
    /// <param name="confirmationToken">
    /// رمز التأكيد كما عاد من الإنسان — <c>null</c> حين لم يُؤكَّد شيء. ويجب أن يطابق
    /// <see cref="VoiceResolution.ConfirmationToken"/> حرفاً بحرف.
    /// </param>
    public static Result<VoiceDispatch> Authorise(
        VoiceResolution resolution,
        VoiceCaller caller,
        string? confirmationToken)
    {
        ArgumentNullException.ThrowIfNull(resolution);
        ArgumentNullException.ThrowIfNull(caller);

        VoiceIntent intent = resolution.Intent;
        List<Error> errors = [];

        if (!caller.PermittedIntentIds.Contains(intent.Id))
        {
            return Result<VoiceDispatch>.Failure(VoiceRefusals.NotPermitted(intent));
        }

        if (intent.Status == VoiceIntentStatus.AwaitingOwnerDecision)
        {
            return Result<VoiceDispatch>.Failure(VoiceRefusals.OwnerDecisionPending(intent));
        }

        foreach (string name in resolution.MissingSlots)
        {
            VoiceSlot? slot = intent.Slots.FirstOrDefault(candidate => candidate.Name == name);
            if (slot is not null)
            {
                errors.Add(VoiceRefusals.SlotMissing(intent, slot));
            }
        }

        errors.AddRange(resolution.Faults);

        if (resolution.SpokenCompany is not null
            && !VoiceText.Same(resolution.SpokenCompany, caller.CompanyNameAr))
        {
            errors.Add(VoiceRefusals.CompanyNotSwitched(resolution.SpokenCompany, caller.CompanyNameAr));
        }

        if (intent.RequiresConfirmation)
        {
            if (confirmationToken is null)
            {
                errors.Add(VoiceRefusals.ConfirmationRequired(intent));
            }
            else if (!string.Equals(confirmationToken, resolution.ConfirmationToken, StringComparison.Ordinal))
            {
                errors.Add(VoiceRefusals.ConfirmationMismatch);
            }
        }

        return errors.Count > 0
            ? Result<VoiceDispatch>.Failure(errors)
            : Result<VoiceDispatch>.Success(new VoiceDispatch(
                intent,
                resolution.Slots,
                caller.CompanyId,
                intent.RequiresConfirmation));
    }
}
