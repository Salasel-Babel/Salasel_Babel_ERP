using Babel.Contracts.Voice;
using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// <b>الحدّ على ما يجوز أن تكونه خطّة — لا على ما تبلغه.</b>
/// <para>
/// ما تبلغه محسومٌ قبل أن تُكتب: الخطوةُ تسمّي <b>نيّة</b>، وكل نيّةٍ في السجلّ اجتازت
/// <see cref="VoiceOperationGuard"/> عند البناء. فلا توجد في الخطّة خانةٌ يُكتب فيها
/// <c>postCustomerReceipt</c> أصلاً. وهذا الحارس يمنع شيئاً آخر تماماً.
/// </para>
/// <para>
/// <b>الخطر الذي وُجد له:</b> الخطّة طريقةٌ للحصول على عدّة «نعم» من إنسانٍ واحد
/// <b>بثمنٍ رخيص</b>. ومن قال «نعم» مرّتين يقولها الثالثة بلا أن يقرأ. فبوابة التأكيد
/// تبقى على حالها لكلّ خطوة، <b>ويُقصَر عدد المستندات التي تُرحَّل في الخطّة على واحد</b>:
/// خطّةٌ تُنشئ مسوّدتين تُرحَّلان دفعةٌ، والدفعةُ المؤكَّدة بالصوت هي عطلُ «عدّة نعم»
/// بعينه. وطلبُ المالك يجتاز هذا: إنشاءُ العميل لا يُرحّل شيئاً، وسندُ القبض هو
/// المستند الوحيد المرحَّل.
/// </para>
/// <para>
/// <b>وما لا يُحرَس عمداً — الشرطُ على أوّل خطوة.</b> كان هنا حارسٌ يقول «شرطٌ على أوّل
/// خطوةٍ عطل، لأن الشرط جوابُ خطوةٍ سبقت»، <b>فسقط على أوّل خطّةٍ حقيقية</b>: «فإن لم
/// تجدها أنشئ لها حساباً» شرطُها على أوّل خطوة، <b>والسؤالُ الذي يجيبه يطرحه الشرطُ
/// نفسه</b> — تسأل الخطّةُ «هل وجدت العميل؟» قبل أن تبدأ. فالشرط قائمٌ بذاته لا تابعٌ
/// لما قبله، <b>وحُذف الحارس الخاطئ بدل أن تُلوى الخطّة له</b>.
/// </para>
/// <para>
/// <b>وما لم يُحرَس هنا، مكتوباً لا مطموساً:</b> «إنشاءُ مستفيدٍ ثمّ الدفعُ إليه» شكلُ
/// احتيالٍ كلاسيكي، ومنعُه مطلوب. <b>لكن اتجاه المال ليس في هذا النموذج</b>: النيّة
/// تحمل رمز حدثٍ مبهماً، و<c>IPostingVocabulary</c> يعرف وجودَ الرمز لا وجهته، ورمزُ
/// الحساب ممنوعٌ هنا بالقاعدة 2. فالخيار كان بين حارسٍ يعدّ <b>أسماء نيّات اليوم</b> —
/// وهو بالضبط ما يرفضه هذا المستودع لأنه لا يمنع خطأ الغد ويصنع ثقةً كاذبة — وبين
/// حقلِ اتجاهٍ جديد في العقد يقرّره مالكُ المنتج. <b>فلم يُخترَع أيٌّ منهما</b>، وسُجّل
/// القرار في ‏ADR-جديد-voice-multi-step-plans. والقيدُ القائم — مستندٌ مرحَّلٌ واحد —
/// يجعل «أنشئ مورداً ثم ادفع له» خطّةً بمسوّدةٍ مرحَّلةٍ واحدة لا دفعة، وهو تضييقٌ لا
/// منعٌ تامّ. <b>وهذا يُقال ولا يُدَّعى خلافُه.</b>
/// </para>
/// </summary>
public static class VoicePlanGuard
{
    /// <summary>أقصى عدد خطواتٍ في خطّة — سقفٌ كي لا تصير الخطّة برنامجاً.</summary>
    public const int StepLimit = 5;

    /// <summary>
    /// أقصى عدد مستنداتٍ تُرحَّل في خطّة واحدة. <b>وهو واحد، وهذا هو الحارس كلّه.</b>
    /// </summary>
    public const int PostingStepLimit = 1;

    /// <summary>
    /// يفحص خطّةً بنيّاتها المُحلّاة، ويعيد <b>كلّ</b> أسباب الرفض لا أوّلها.
    /// </summary>
    /// <param name="plan">الخطّة.</param>
    /// <param name="resolve">يحلّ معرّف النيّة إلى نيّةٍ في السجلّ، أو <c>null</c>.</param>
    public static IReadOnlyList<Error> Refuse(VoicePlan plan, Func<string, VoiceIntent?> resolve)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(resolve);

        List<Error> errors = [];

        if (plan.Steps.Count == 0)
        {
            errors.Add(VoicePlanErrors.NoSteps(plan.Id));
            return errors;
        }

        if (plan.Steps.Count > StepLimit)
        {
            errors.Add(VoicePlanErrors.TooManySteps(plan.Id, plan.Steps.Count, StepLimit));
        }

        if (plan.TriggerPhrases.Count == 0 || plan.ConditionPhrases.Count == 0)
        {
            // ‏**والحقلان لازمان معاً**: خطّةٌ بلا شرطٍ تُطابق كلَّ جملةٍ تُطابقها نيّتُها
            // المفردة فتسرقها منها، وخطّةٌ بلا طلبٍ تُطابق كلَّ شرطٍ في أي كلام.
            errors.Add(VoicePlanErrors.NoPhrases(plan.Id));
        }

        HashSet<string> stepIds = new(StringComparer.Ordinal);
        int posting = 0;

        for (int index = 0; index < plan.Steps.Count; index++)
        {
            VoicePlanStep step = plan.Steps[index];

            if (!stepIds.Add(step.StepId))
            {
                errors.Add(VoicePlanErrors.DuplicateStepId(plan.Id, step.StepId));
            }

            VoiceIntent? intent = resolve(step.IntentId);

            if (intent is null)
            {
                // ‏**وهذا هو ما يُغلق الباب**: خطوةٌ تسمّي ما ليس في السجلّ تُسقط البناء.
                // فلا تُهرَّب عمليةٌ ممنوعة باسمِ نيّةٍ مخترَعة — ولا نيّةَ في السجلّ إلا
                // وقد اجتازت حارسَ العمليات.
                errors.Add(VoicePlanErrors.StepIntentUnknown(plan.Id, step.StepId, step.IntentId));
                continue;
            }

            if (intent.Status == VoiceIntentStatus.AwaitingOwnerDecision)
            {
                // نيّةٌ تنتظر قراراً لا عمليةَ لها بالبناء، فخطوتُها لا تنتهي إلى شيء.
                errors.Add(VoicePlanErrors.StepAwaitsOwner(plan.Id, step.StepId, step.IntentId));
            }

            if (intent.Section != plan.Section)
            {
                errors.Add(VoicePlanErrors.StepLeavesSection(
                    plan.Id, step.StepId, intent.Section.ToString(), plan.Section.ToString()));
            }

            if (intent.LedgerEffect == VoiceLedgerEffect.Posts)
            {
                posting++;
            }

            // ‏**قراءةُ بيانٍ شخصي لا تكون خطوةً وسطى**: جوابُها يُقرأ داخل ملخّصٍ أكبر
            // يُنطَق في غرفةٍ فيها غيرُ صاحبه. وآخرَ الخطّة جوابٌ يقف عنده الكلام.
            if (intent.ReadsPersonalData && index < plan.Steps.Count - 1)
            {
                errors.Add(VoicePlanErrors.PersonalDataMidPlan(plan.Id, step.StepId, step.IntentId));
            }

            foreach (VoiceSlotBinding binding in step.Bindings)
            {
                if (!intent.Slots.Any(slot => string.Equals(slot.Name, binding.SlotName, StringComparison.Ordinal)))
                {
                    errors.Add(VoicePlanErrors.BindingUnknownSlot(
                        plan.Id, step.StepId, binding.SlotName, step.IntentId));
                }
            }

            // ‏**وكلُّ شريحةٍ لازمةٍ في النيّة لها ربطٌ يقول من أين تأتي.** لازمةٌ بلا ربط
            // تصل الخطوةَ فارغةً بلا أن يقول أحد كيف كانت ستمتلئ — وهو نقصٌ يظهر في يد
            // المستخدم بدل أن يظهر عند البناء.
            foreach (VoiceSlot required in intent.Slots.Where(static slot => slot.Required))
            {
                if (!step.Bindings.Any(binding =>
                        string.Equals(binding.SlotName, required.Name, StringComparison.Ordinal)))
                {
                    errors.Add(VoicePlanErrors.RequiredSlotNotBound(
                        plan.Id, step.StepId, required.Name, step.IntentId));
                }
            }
        }

        if (posting > PostingStepLimit)
        {
            errors.Add(VoicePlanErrors.PostsMoreThanOnce(plan.Id, posting, PostingStepLimit));
        }

        return errors;
    }

}
