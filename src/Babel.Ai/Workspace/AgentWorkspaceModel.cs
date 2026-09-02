using Babel.Ai.Agent;
using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// حال خطوةٍ واحدة في مساحة العمل.
/// <para>
/// <b>ولا حالة اسمها «مُرحَّلة»، ولا يجوز أن توجد.</b> أبعد ما تبلغه خطوةٌ
/// <see cref="Landed"/> — مسوّدةٌ هبطت على شاشتها — والترحيل فعلٌ بصريّ يدويّ هناك.
/// </para>
/// </summary>
public enum AgentStepState
{
    /// <summary>مُعلَنةٌ في الخطّة ولم تبدأ.</summary>
    Planned = 1,

    /// <summary>تنفّذ الآن.</summary>
    Running = 2,

    /// <summary>تنتظر أن يقبل الإنسان <b>شكل بياناتها</b> — ولا تعني الترحيل.</summary>
    AwaitingConfirmation = 3,

    /// <summary>تنتظر اختيار الإنسان على ورقة السؤال.</summary>
    AwaitingAnswer = 4,

    /// <summary>هبطت مسوّدتها على شاشتها.</summary>
    Landed = 5,

    /// <summary>سقطت، ومعها سببُها بالعربية.</summary>
    Refused = 6,
}

/// <summary>طورُ الدور كما تقرؤه اللوحة.</summary>
public enum AgentTurnPhase
{
    /// <summary>النموذج يفكّر أو ينفّذ.</summary>
    Running = 1,

    /// <summary>يقف عند إنسان: تأكيدٌ أو ورقة سؤال.</summary>
    AwaitingHuman = 2,

    /// <summary>انتهى الدور.</summary>
    Completed = 3,

    /// <summary>رُفض الدور — ولم يُرسَل، أو تُوقّف بعد الإرسال.</summary>
    Refused = 4,
}

/// <summary>خطوةٌ في الخطّة، بعنوانها العربيّ وحالها.</summary>
/// <param name="StepId">معرّفها في هذه المساحة — يُستعمل في مسار التأكيد.</param>
/// <param name="Order">ترتيبها، بدءاً من واحد.</param>
/// <param name="TitleAr">عنوانها كما أعلنه النموذج أو كما اشتُقّ من اسم أداتها.</param>
/// <param name="State">حالها.</param>
/// <param name="ToolName">اسم الأداة حين تُنفَّذ.</param>
/// <param name="ScreenRoute">مسار شاشة المسوّدة حين تهبط.</param>
/// <param name="Errors">أسباب السقوط.</param>
public sealed record AgentWorkspaceStep(
    Guid StepId,
    int Order,
    string TitleAr,
    AgentStepState State,
    string? ToolName,
    string? ScreenRoute,
    IReadOnlyList<Error> Errors);

/// <summary>
/// حقلٌ في بطاقة التأكيد. <b>وقيمةُ ما شكلُه معرّف لا تُعرض</b> — الحقل يُقنَّع ويُقال
/// إنه مُقنَّع، فلا يقرأ الإنسان صفّاً ولا يقرؤه من يقف خلف كتفه.
/// </summary>
/// <param name="Path">مسار الحقل داخل الجسم كما ينشره العقد.</param>
/// <param name="Value">القيمة المعروضة، أو <c>null</c> حين تُقنَّع.</param>
/// <param name="Masked">هل قُنِّعت؟</param>
public sealed record AgentDraftField(string Path, string? Value, bool Masked);

/// <summary>
/// طلبُ تأكيدٍ معلَّق. <b>ومعناه واحدٌ لا ثانٍ له: «أقبل شكل هذه البيانات».</b>
/// ولا يعني «رحّلها» — والناتج بعده مسوّدةٌ كما كان قبله.
/// </summary>
/// <param name="StepId">الخطوة التي تنتظر.</param>
/// <param name="ToolName">اسم العملية المنشورة.</param>
/// <param name="ScreenRoute">مسار الشاشة التي ستهبط عليها المسوّدة.</param>
/// <param name="Fields">حقول الجسم بترتيبٍ ثابت.</param>
public sealed record AgentWorkspaceConfirmation(
    Guid StepId,
    string ToolName,
    string ScreenRoute,
    IReadOnlyList<AgentDraftField> Fields);

/// <summary>خيارٌ على ورقة السؤال — نصُّه محلّي ورمزُه هو ما يعبر.</summary>
/// <param name="OptionToken">الرمز الموقَّع المعمّى.</param>
/// <param name="LabelAr">الاسم كما هو في السجلّ المحلّي.</param>
/// <param name="SubtitleAr">سطرٌ فارق — قناعٌ لا معرّف.</param>
public sealed record AgentSheetOption(string OptionToken, string LabelAr, string? SubtitleAr);

/// <summary>
/// ورقة سؤالٍ معلَّقة كما رسمها الخادم من بياناتٍ محلّية.
/// <para>
/// <b>ولا يبلغ النموذج منها شيء</b>: لا الأسماء، ولا عددُها، ولا موضعُ ما اختير. وما
/// يعود إليه بعد الاختيار <c>{"handle":"…"}</c> — شكلٌ واحد في كل الحالات.
/// </para>
/// </summary>
/// <param name="QuestionId">معرّف الورقة المعتِم.</param>
/// <param name="RegisterKey">مفتاح السجلّ.</param>
/// <param name="SubjectText">كلام المستخدم كما بحث به النموذج.</param>
/// <param name="Options">الخيارات المرسومة محلّياً.</param>
/// <param name="AllowsCreate">هل يُتاح «جديد»؟</param>
public sealed record AgentWorkspaceQuestion(
    string QuestionId,
    string RegisterKey,
    string SubjectText,
    IReadOnlyList<AgentSheetOption> Options,
    bool AllowsCreate);

/// <summary>
/// حدثٌ في سجلّ المساحة، بترتيبه. <b>والترتيب هو المؤشّر</b>: اللوحة تقرأ «ما بعد ن»،
/// فانقطاعُ الشبكة يُستأنف من حيث وقف بلا تكرارٍ ولا فجوة.
/// </summary>
/// <param name="Sequence">رقم الحدث في هذه الجلسة، بدءاً من واحد.</param>
/// <param name="TurnId">الدور الذي أنتجه.</param>
/// <param name="Kind">شكل الحدث.</param>
/// <param name="Text">النصّ المعروض، أو كلام البحث في حدث ورقة السؤال.</param>
/// <param name="ToolName">اسم الأداة.</param>
/// <param name="QuestionId">معرّف الورقة المعتِم.</param>
/// <param name="RegisterKey">مفتاح السجلّ في حدث ورقة السؤال.</param>
/// <param name="ScreenRoute">مسار شاشة المسوّدة.</param>
/// <param name="StepId">الخطوة المرتبطة بالحدث، إن وُجدت.</param>
/// <param name="Errors">أسباب الرفض.</param>
/// <param name="Steps">عناوين الخطوات في حدث الخطّة.</param>
public sealed record AgentWorkspaceEvent(
    long Sequence,
    Guid TurnId,
    AgentTurnEventKind Kind,
    string? Text,
    string? ToolName,
    string? QuestionId,
    string? RegisterKey,
    string? ScreenRoute,
    Guid? StepId,
    IReadOnlyList<Error> Errors,
    IReadOnlyList<string> Steps);

/// <summary>
/// إنفاق المنشأة في نافذتها الجارية — <b>بالرموز لا بالريالات</b>.
/// وسببُه مكتوبٌ في <c>AgentOptions.DefaultTenantTokenCeiling</c>: الرمز واقعةٌ يُعيدها
/// المزوّد ونقيسها، والريال يحتاج جدول أسعارٍ ليس في هذا المستودع.
/// </summary>
/// <param name="Billable">مجموع الرموز المحاسَب عليها في النافذة.</param>
/// <param name="Ceiling">السقف، أو <c>null</c> لمنشأةٍ تعمل بمفتاحها.</param>
/// <param name="Turns">عدد الأدوار المُحاسَبة في النافذة.</param>
/// <param name="WindowSeconds">طول نافذة المحاسبة بالثواني.</param>
/// <param name="BringsItsOwnKey">هل تعمل هذه المنشأة على مفتاحها؟</param>
public sealed record AgentWorkspaceSpend(
    long Billable,
    long? Ceiling,
    int Turns,
    long WindowSeconds,
    bool BringsItsOwnKey);
