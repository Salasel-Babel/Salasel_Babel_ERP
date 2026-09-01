using Babel.SharedKernel;

namespace Babel.Ai.Voice;

/// <summary>
/// أعطال <b>بناء</b> سجلّ الخطط — لا أعطال كلام، على نفس اصطلاح
/// <see cref="VoiceCatalogueErrors"/> ونفس سببه: خطّةٌ معتلّة تُسقط التركيب مرّةً عند
/// الإقلاع، <b>لا في يد مستخدمٍ نطق جملةً مركّبة</b>.
/// </summary>
public static class VoicePlanErrors
{
    /// <summary>معرّف خطّة تكرّر.</summary>
    /// <param name="planId">المعرّف.</param>
    public static Error DuplicatePlanId(string planId) => new(
        "ai.voice.catalogue.plan_duplicate_id",
        "معرّف الخطّة «" + planId + "» مُعلَن مرّتين. وتغليبُ إحداهما بصمت يجعل جملةً واحدة تُنفّذ خطّة وحدةٍ أخرى.",
        "Plan id '" + planId + "' is declared twice.");

    /// <summary>شكل المعرّف مخالف.</summary>
    /// <param name="planId">المعرّف.</param>
    public static Error MalformedPlanId(string planId) => new(
        "ai.voice.catalogue.plan_malformed_id",
        "معرّف الخطّة «" + planId + "» ليس على الشكل المُعلَن: مقاطع لاتينية صغيرة تفصلها نقاط.",
        "Plan id '" + planId + "' does not match the declared shape.");

    /// <summary>خطّةٌ بلا خطوة واحدة.</summary>
    /// <param name="planId">المعرّف.</param>
    public static Error NoSteps(string planId) => new(
        "ai.voice.catalogue.plan_empty",
        "الخطّة «" + planId + "» بلا خطوةٍ واحدة. وخطّةٌ فارغة تُطابق كلاماً ثم لا تفعل شيئاً، "
        + "فيظنّ قائلُها أن أمره وصل.",
        "Plan '" + planId + "' declares no steps.");

    /// <summary>خطّةٌ بلا عبارة إطلاق.</summary>
    /// <param name="planId">المعرّف.</param>
    public static Error NoPhrases(string planId) => new(
        "ai.voice.catalogue.plan_no_phrases",
        "الخطّة «" + planId + "» ينقصها طلبٌ أو شرط. والخطّة تُعرَف باجتماعهما: "
        + "بلا شرطٍ تسرق جملةَ نيّتها المفردة، وبلا طلبٍ تُطابق كلَّ شرطٍ في أي كلام.",
        "Plan '" + planId + "' declares no trigger phrase or no condition phrase.");

    /// <summary>خطوات أكثر من السقف.</summary>
    /// <param name="planId">المعرّف.</param>
    /// <param name="count">العدد.</param>
    /// <param name="limit">السقف.</param>
    public static Error TooManySteps(string planId, int count, int limit) => new(
        "ai.voice.catalogue.plan_too_many_steps",
        "الخطّة «" + planId + "» فيها " + count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + " خطوات والسقف " + limit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ". وخطّةٌ أطول من ذلك برنامجٌ يُملى بالصوت، ولا يتذكّر إنسانٌ ما وافق عليه في أوّلها.",
        "Plan '" + planId + "' declares more steps than the limit.");

    /// <summary>معرّف خطوة تكرّر داخل خطّة.</summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="stepId">الخطوة.</param>
    public static Error DuplicateStepId(string planId, string stepId) => new(
        "ai.voice.catalogue.plan_duplicate_step",
        "الخطّة «" + planId + "» تُعلن الخطوة «" + stepId + "» مرّتين، والروابط تشير إليها بالاسم.",
        "Plan '" + planId + "' declares step '" + stepId + "' twice.");

    /// <summary><b>خطوةٌ تسمّي نيّةً ليست في السجلّ.</b></summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="stepId">الخطوة.</param>
    /// <param name="intentId">النيّة.</param>
    public static Error StepIntentUnknown(string planId, string stepId, string intentId) => new(
        "ai.voice.catalogue.plan_step_unknown",
        "الخطوة «" + stepId + "» في الخطّة «" + planId + "» تسمّي النيّة «" + intentId
        + "» وهي ليست في السجلّ. <b>وهذا هو الباب الذي لا يُفتح</b>: الخطوة تسمّي نيّةً لا عملية، "
        + "وكلُّ نيّةٍ في السجلّ اجتازت حارسَ العمليات — فنيّةٌ مخترَعة تُسقط البناء بدل أن تُهرّب باباً.",
        "Step '" + stepId + "' of plan '" + planId + "' names intent '" + intentId + "', which is not in the registry.");

    /// <summary>خطوةٌ تسمّي نيّةً تنتظر قرار المالك.</summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="stepId">الخطوة.</param>
    /// <param name="intentId">النيّة.</param>
    public static Error StepAwaitsOwner(string planId, string stepId, string intentId) => new(
        "ai.voice.catalogue.plan_step_awaiting_owner",
        "الخطوة «" + stepId + "» في الخطّة «" + planId + "» تسمّي النيّة «" + intentId
        + "» وهي تنتظر قرار المالك، فلا عمليةَ لها بالبناء. وخطوةٌ لا تنتهي إلى شاشةٍ تقف بالخطّة عند لا شيء.",
        "Step '" + stepId + "' of plan '" + planId + "' names an intent awaiting an owner decision.");

    /// <summary>خطوةٌ في قسمٍ غير قسم الخطّة.</summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="stepId">الخطوة.</param>
    /// <param name="stepSection">قسم النيّة.</param>
    /// <param name="planSection">قسم الخطّة.</param>
    public static Error StepLeavesSection(string planId, string stepId, string stepSection, string planSection) => new(
        "ai.voice.catalogue.plan_step_leaves_section",
        "الخطوة «" + stepId + "» في الخطّة «" + planId + "» نيّتُها في قسم «" + stepSection
        + "» والخطّة في «" + planSection + "». وخطّةٌ يراها المستخدم في قسمٍ وتُنشئ مستنداً في قسمٍ آخر "
        + "تُنتج أثراً في مكانٍ لم ينظر إليه أحد.",
        "Step '" + stepId + "' of plan '" + planId + "' resolves to a different section.");

    /// <summary><b>خطّةٌ تحمل أكثر من مستندٍ واحد يُرحَّل.</b></summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="count">العدد.</param>
    /// <param name="limit">السقف.</param>
    public static Error PostsMoreThanOnce(string planId, int count, int limit) => new(
        "ai.voice.catalogue.plan_posts_more_than_once",
        "الخطّة «" + planId + "» تحمل " + count.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + " مستندات تُرحَّل والسقف " + limit.ToString(System.Globalization.CultureInfo.InvariantCulture)
        + ". وخطّةٌ تُنشئ مسوّدتين تُرحَّلان دفعةٌ، والدفعةُ المؤكَّدة بالصوت هي عطلُ «عدّة نعم» بعينه: "
        + "من قال «نعم» مرّتين يقولها الثالثة بلا أن يقرأ.",
        "Plan '" + planId + "' carries more than one postable document.");

    /// <summary>قراءةُ بيانٍ شخصي في خطوةٍ وسطى.</summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="stepId">الخطوة.</param>
    /// <param name="intentId">النيّة.</param>
    public static Error PersonalDataMidPlan(string planId, string stepId, string intentId) => new(
        "ai.voice.catalogue.plan_personal_data_mid_plan",
        "الخطوة «" + stepId + "» في الخطّة «" + planId + "» تقرأ بياناً شخصياً («" + intentId
        + "») وليست الأخيرة. وجوابُها يُقرأ داخل ملخّصٍ أكبر يُنطَق في غرفةٍ فيها غيرُ صاحبه — "
        + "وآخرَ الخطّة جوابٌ يقف عنده الكلام.",
        "Step '" + stepId + "' of plan '" + planId + "' reads personal data yet is not the final step.");

    /// <summary>ربطٌ يسمّي شريحةً لا تُعلنها النيّة.</summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="stepId">الخطوة.</param>
    /// <param name="slotName">الشريحة.</param>
    /// <param name="intentId">النيّة.</param>
    public static Error BindingUnknownSlot(string planId, string stepId, string slotName, string intentId) => new(
        "ai.voice.catalogue.plan_binding_unknown_slot",
        "الخطوة «" + stepId + "» في الخطّة «" + planId + "» تربط الشريحة «" + slotName
        + "» ولا تُعلنها النيّة «" + intentId + "». وربطٌ إلى شريحةٍ لا وجود لها يُملأ في الفراغ.",
        "Step '" + stepId + "' of plan '" + planId + "' binds slot '" + slotName + "', undeclared by its intent.");

    /// <summary>شريحةٌ لازمة بلا ربطٍ يقول من أين تأتي.</summary>
    /// <param name="planId">الخطّة.</param>
    /// <param name="stepId">الخطوة.</param>
    /// <param name="slotName">الشريحة.</param>
    /// <param name="intentId">النيّة.</param>
    public static Error RequiredSlotNotBound(string planId, string stepId, string slotName, string intentId) => new(
        "ai.voice.catalogue.plan_required_slot_not_bound",
        "الشريحة «" + slotName + "» لازمةٌ في النيّة «" + intentId + "» ولا تربطها الخطوة «"
        + stepId + "» في الخطّة «" + planId + "». وخطّةٌ لا تقول من أين تأتي شريحةٌ لازمة "
        + "تكتشف نقصَها في يد المستخدم لا عند البناء.",
        "Required slot '" + slotName + "' of intent '" + intentId + "' is not bound by step '" + stepId + "'.");

    /// <summary>سجلُّ خططٍ فارغ رغم وجود مجموعات.</summary>
    public static readonly Error CatalogueEmpty = new(
        "ai.voice.catalogue.plan_catalogue_empty",
        "مجموعةُ خططٍ مُسجَّلة بلا خطّةٍ واحدة. ومجموعةٌ فارغة تُسجَّل ثم لا يلاحظ أحد أن الخطط سقطت.",
        "A registered plan catalogue declares no plans at all.");
}
