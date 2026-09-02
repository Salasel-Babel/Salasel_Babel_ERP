using System.Globalization;
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

    /// <summary>
    /// لا شاشةَ مُعلَنة لهذه العملية — <b>ولا يُخترع مسار</b>.
    /// <para>
    /// وحارسُ <c>EveryDraftOperationHasAScreenToLandOn</c> يجعل هذا الرفض غير قابلٍ
    /// للوقوع في بناءٍ أخضر: عمليةٌ تُنشر بلا صفٍّ في الخريطة تُحمِّر البناء قبل أن
    /// تصل إلى مستخدم. وبقاؤه هنا لأن الشيفرة لا تعتمد على حارسٍ لتكون صحيحة.
    /// </para>
    /// </summary>
    /// <param name="operationId">معرّف العملية.</param>
    public static Error DraftHasNoScreenToLandOn(string operationId) => new(
        CodePrefix + "draft_has_no_screen",
        "المسوّدة «" + operationId + "» لا شاشةَ مُعلَنة لها في هذا الإصدار، فلا يُفتح لها زرّ. "
        + "وهذا نقصُ خريطةٍ مُعلَن لا عطل: أنشئ المستند من شاشته.",
        "the draft '" + operationId + "' has no declared screen in this build, so no button opens it.");

    /// <summary>مسارُ الباب فيه وسيطٌ لا يملك مسارُ الوكيل ما يملؤه به.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="path">المسار المنشور.</param>
    /// <param name="parameter">اسم الوسيط الذي بقي فارغاً.</param>
    public static Error DraftPathParameterIsUnfilled(string operationId, string path, string parameter) => new(
        CodePrefix + "draft_path_parameter_unfilled",
        "مسارُ «" + operationId + "» — " + path + " — يحتاج وسيط «" + parameter
        + "» ولا يملك مسار الوكيل ما يملؤه به. والوكيل لا يخترع معرّفاً في مسار.",
        "the path of '" + operationId + "' needs the '" + parameter
        + "' parameter, which the agent's path cannot fill.");

    /// <summary>لا بابَ منشوراً بهذا المسار وهذا الفعل في جدول مسارات هذا الخادم.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="path">المسار المنشور.</param>
    public static Error DraftDoorIsNotOnThisServer(string operationId, string path) => new(
        CodePrefix + "draft_door_not_on_this_server",
        "لا بابَ مسجَّلاً على هذا الخادم بمسار «" + path + "» لعملية «" + operationId
        + "». وهذا انحرافُ تركيبٍ بين العقد المنشور وما رُكِّب فعلاً، لا خطأٌ في طلبك.",
        "no endpoint is mapped on this server at '" + path + "' for '" + operationId + "'.");

    /// <summary>ردّ الباب برمز حالةٍ رافض وجسمُه لا يحمل رفضاً مُسمّى.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="status">رمز الحالة.</param>
    public static Error DraftRefusalIsUnreadable(string operationId, int status) => new(
        CodePrefix + "draft_refusal_unreadable",
        "ردّ بابُ «" + operationId + "» بالحالة " + status.ToString(CultureInfo.InvariantCulture)
        + " وجسمُه لا يحمل رفضاً مُسمّى. ولم تهبط مسوّدة.",
        "the door of '" + operationId + "' answered with status "
        + status.ToString(CultureInfo.InvariantCulture) + " and a body that carries no named refusal.");

    /// <summary>انقطع نداءُ الباب بعطلٍ برمجي — ولا مسوّدة.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="fault">اسم نوع العطل — <b>ولا نصّه</b>: نصُّ استثناءٍ يسرّب داخل الخادم.</param>
    public static Error DraftCallBroke(string operationId, string fault) => new(
        CodePrefix + "draft_call_broke",
        "انقطع نداءُ «" + operationId + "» بعطلٍ في الخادم: " + fault + ". ولم تهبط مسوّدة. "
        + "أعِد المحاولة، وإن تكرّر فالعطل ليس في طلبك.",
        "the call to '" + operationId + "' broke with a server fault: " + fault + "; no draft landed.");

    /// <summary>لا هويّةَ إنسانٍ محفوظة لهذه الجلسة، فلا يُنسب إليها مستند.</summary>
    public static Error DraftHasNoHumanToAttributeTo { get; } = new(
        CodePrefix + "draft_has_no_human",
        "لا هويّةَ إنسانٍ محفوظة لهذه الجلسة على هذا الخادم، والمسوّدة تُنسب إلى إنسانٍ لا إلى وكيل. "
        + "أعِد فتح مساحة العمل وأرسل طلبك من جديد.",
        "no human identity is held for this session on this server, and a draft is attributed to a human, "
        + "never to an agent.");

    /// <summary>لا منفّذ مسوّداتٍ مركَّب على هذا الخادم.</summary>
    public static Error DraftDestinationUnavailable { get; } = new(
        CodePrefix + "draft_destination_unavailable",
        "لا منفّذ مسوّداتٍ مركَّب على هذا الخادم، فلا تهبط مسوّدةٌ على شاشتها. "
        + "وهذا نقصُ تركيبٍ مُعلَن: الحلقة والبوّابة وورقة السؤال تعمل، ووصلُ كلّ عملية مسوّدةٍ بوحدتها المالكة لم ينزل بعد.",
        "no draft destination is composed on this server, so no draft lands on its screen; this is a declared composition gap.");
}
