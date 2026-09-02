using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// مفردات رفض مساحة العمل — <b>جُملٌ تُقرأ على اللوحة</b>، وكلٌّ منها يقول ما ينقص
/// بالضبط: «الجلسة انقطعت» و«الوكيل معطَّل لهذه المنشأة» و«تجاوزتَ حدّ الإنفاق»
/// حالاتٌ يفصل بينها المستخدم بفعلٍ مختلف، فلا تُجمع في «تعذّر».
/// </summary>
public static class AgentWorkspaceErrors
{
    /// <summary>صدر رموز المساحة.</summary>
    public const string CodePrefix = "ai.workspace.";

    /// <summary>لا جلسة بهذا المعرّف في هذه المنشأة — أو انقضت.</summary>
    public static Error SessionNotFound { get; } = new(
        CodePrefix + "session_not_found",
        "جلسة الوكيل غير موجودة أو انقضت. ابدأ جلسةً جديدة — وما مضى من حديثها لا يُستعاد.",
        "the agent session does not exist or has expired; start a new one.");

    /// <summary>الوكيل غير مركَّب على هذا الخادم.</summary>
    public static Error AgentDisabled { get; } = new(
        CodePrefix + "agent_disabled",
        "الوكيل غير مُفعَّل على هذا الخادم لهذه المنشأة. وهذا إعدادُ نشرٍ لا عطل: مساحة العمل تُفتح حين يُركَّب.",
        "the agent is not enabled on this server for this tenant.");

    /// <summary>لا دور بهذا المعرّف في هذه الجلسة.</summary>
    public static Error TurnNotFound { get; } = new(
        CodePrefix + "turn_not_found",
        "لا دور بهذا المعرّف في هذه الجلسة.",
        "no turn with this identifier in this session.");

    /// <summary>دورٌ يجري: لا يُبدأ ثانٍ فوقه.</summary>
    public static Error TurnAlreadyRunning { get; } = new(
        CodePrefix + "turn_already_running",
        "دورٌ يجري في هذه الجلسة. انتظر انتهاءه أو أجب ما ينتظر جواباً — ولا يُركَّب دورٌ فوق دور.",
        "a turn is already running in this session.");

    /// <summary>لا شيء ينتظر تأكيداً بهذا المعرّف.</summary>
    public static Error NothingAwaitsConfirmation { get; } = new(
        CodePrefix + "nothing_awaits_confirmation",
        "لا خطوة تنتظر تأكيدك بهذا المعرّف. ولعلّها أُكِّدت أو سقطت قبل أن يصل هذا الطلب.",
        "no step awaits your confirmation under this identifier.");

    /// <summary>لا ورقة سؤالٍ معلَّقة بهذا المعرّف.</summary>
    public static Error NoPendingQuestion { get; } = new(
        CodePrefix + "no_pending_question",
        "لا ورقة سؤالٍ معلَّقة بهذا المعرّف في هذه الجلسة.",
        "no question sheet is pending under this identifier in this session.");

    /// <summary>رمز الخيار ليس من هذه الورقة.</summary>
    public static Error OptionNotOnThisSheet { get; } = new(
        CodePrefix + "option_not_on_this_sheet",
        "رمز الخيار ليس من هذه الورقة. ولا يُقارَب بأقرب شبيه — ورقةٌ تُجيب بغير خياراتها ليست ورقة.",
        "the option token does not belong to this sheet.");

    /// <summary>خيار «جديد» غير متاحٍ في هذا التسليم — <b>نقصٌ مُعلَن لا منعٌ مقرَّر</b>.</summary>
    public static Error CreateNotWiredYet { get; } = new(
        CodePrefix + "create_not_wired",
        "«جديد» غير موصولةٍ بعد بسجلّات الأسماء على هذا الخادم — وهذا نقصُ سطحٍ مُعلَن لا قرارُ منع. "
        + "اختر من القائمة، أو أنشئ الطرف على شاشته ثمّ أعِد الطلب.",
        "the 'new' option is not yet wired to the name registers on this server; this is a declared surface gap.");

    /// <summary>ورفضٌ لا يقع إلا إن انكسر شيء: خطوةٌ تبلغ ما لا يُعكَس.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="path">مسارها المنشور.</param>
    /// <param name="segment">المقطع الذي لا يُعكَس.</param>
    public static Error StepReachesAnIrreversibleDoor(string operationId, string path, string segment) => new(
        CodePrefix + "step_reaches_an_irreversible_door",
        "خطوةٌ من الوكيل تبلغ «" + path + "» عبر «" + operationId + "» — ومقطعُ «" + segment
        + "» لا يُعكَس. ومسار الوكيل ينتهي عند المسوّدة، والترحيل فعلٌ بصريّ يدويّ على الشاشة.",
        "an agent step reaches '" + path + "' via '" + operationId + "'; the '" + segment
        + "' segment is irreversible and the agent's path ends at a draft.");

    /// <summary>وخطوةٌ فعلُها ليس <c>draft</c>.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    public static Error StepIsNotADraftOperation(string operationId) => new(
        CodePrefix + "step_is_not_a_draft_operation",
        "خطوةٌ من الوكيل تنادي «" + operationId + "» وفعلُها ليس «draft». والوكيل لا يبلغ إلا إنشاء المسوّدات.",
        "an agent step calls '" + operationId + "', whose verb is not 'draft'.");

    /// <summary>رفض الإنسان شكل البيانات.</summary>
    public static Error ShapeRefusedByHuman { get; } = new(
        CodePrefix + "shape_refused_by_human",
        "رفض المستخدم شكل هذه البيانات، فلم تهبط المسوّدة. صحّح ما اعترض عليه ثمّ أعِد الخطوة.",
        "the user refused this data's shape, so no draft landed.");

    /// <summary>انقضى انتظار الإنسان.</summary>
    public static Error HumanDidNotAnswerInTime { get; } = new(
        CodePrefix + "human_did_not_answer",
        "انقضى انتظار جواب المستخدم فتوقّفت الخطوة. وهذا توقّفٌ لا سقوط: أعِد الطلب حين تكون حاضراً.",
        "the wait for the user's answer elapsed and the step stopped.");

    /// <summary>لا منفّذ مسوّداتٍ مركَّب على هذا الخادم.</summary>
    public static Error DraftDestinationUnavailable { get; } = new(
        CodePrefix + "draft_destination_unavailable",
        "لا منفّذ مسوّداتٍ مركَّب على هذا الخادم، فلا تهبط مسوّدةٌ على شاشتها. "
        + "وهذا نقصُ تركيبٍ مُعلَن: الحلقة والبوّابة وورقة السؤال تعمل، ووصلُ كلّ عملية مسوّدةٍ بوحدتها المالكة لم ينزل بعد.",
        "no draft destination is composed on this server, so no draft lands on its screen; this is a declared composition gap.");
}
