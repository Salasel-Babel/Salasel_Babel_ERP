namespace Babel.Api.Wire;

/// <summary>
/// خطوةٌ في خطّة الوكيل كما تُعرَض على اللوحة.
/// <para>
/// <b>ولا حالة اسمها <c>posted</c> في <see cref="State"/>، ولا يجوز أن توجد.</b> أبعد ما
/// تبلغه خطوةٌ <c>landed</c> — مسوّدةٌ هبطت على شاشتها — والترحيل فعلٌ بصريّ يدويّ هناك.
/// </para>
/// </summary>
/// <param name="StepId">معرّف الخطوة — وهو ما يُكتب في مسار التأكيد.</param>
/// <param name="Ordinal">ترتيبها بدءاً من واحد.</param>
/// <param name="TitleAr">عنوانها العربي كما أعلنه النموذج، أو اسم أداتها إن نفّذ بلا خطّة.</param>
/// <param name="State">حالها.</param>
/// <param name="ToolName">اسم العملية المنشورة التي تناديها الخطوة.</param>
/// <param name="ScreenRoute">مسار شاشة المسوّدة بعد هبوطها.</param>
/// <param name="Refusals">أسباب السقوط بالعربية والإنجليزية، أو قائمة فارغة.</param>
internal sealed record AgentPlanStepDto(
    string StepId,
    int Ordinal,
    string TitleAr,
    string State,
    string? ToolName,
    string? ScreenRoute,
    IReadOnlyList<ApiErrorDto> Refusals);

/// <summary>
/// حقلٌ واحد في بطاقة التأكيد.
/// <para>
/// <b>وقيمةُ ما شكلُه معرّف لا تُعرض:</b> <see cref="Masked"/> صحيحة و<see cref="Value"/>
/// معدومة. والحدّ الذي حُفظ أمام النموذج يُحفظ أمام الكتف الذي يقف خلف المستخدم.
/// </para>
/// </summary>
/// <param name="Path">مسار الحقل داخل الجسم كما ينشره العقد.</param>
/// <param name="Value">القيمة المعروضة، أو <c>null</c> حين تُقنَّع.</param>
/// <param name="Masked">هل قُنِّعت؟</param>
internal sealed record AgentDraftFieldDto(string Path, string? Value, bool Masked);

/// <summary>
/// ما ينتظر تأكيد الإنسان الآن.
/// <para>
/// <b>ومعنى التأكيد واحدٌ لا ثانيَ له: «أقبل شكل هذه البيانات».</b> ولا يعني «رحّلها».
/// </para>
/// </summary>
/// <param name="StepId">الخطوة المنتظِرة.</param>
/// <param name="OperationId">معرّف العملية المنشورة — وفعلُها <c>draft</c> دائماً.</param>
/// <param name="ScreenRoute">مسار الشاشة التي ستهبط عليها المسوّدة.</param>
/// <param name="Fields">حقول الجسم بترتيبٍ ثابت.</param>
internal sealed record AgentConfirmationDto(
    string StepId,
    string OperationId,
    string ScreenRoute,
    IReadOnlyList<AgentDraftFieldDto> Fields);

/// <summary>
/// خيارٌ على ورقة السؤال. <b>نصُّه محلّي ورمزُه هو ما يعبر.</b>
/// </summary>
/// <param name="OptionToken">الرمز الموقَّع المعمّى — وهو وحده ما يعود إلى الخادم.</param>
/// <param name="Label">الاسم كما هو في سجلّ المستخدم. <b>ولا يبلغ النموذج.</b></param>
/// <param name="Subtitle">سطرٌ فارق — قناعٌ لا معرّف.</param>
internal sealed record AgentQuestionOptionDto(string OptionToken, string Label, string? Subtitle);

/// <summary>
/// ورقة السؤال كما رسمها الخادم من بياناتٍ محلّية.
/// <para>
/// <b>ولا يبلغ النموذجَ منها شيء:</b> لا الأسماء، ولا عددُها، ولا موضعُ ما اختير، ولا
/// أنّ اختياراً وقع. وما يعود إليه بعد الاختيار شكلٌ واحد في كل الحالات.
/// </para>
/// </summary>
/// <param name="QuestionId">معرّف الورقة المعتِم — وهو ما يُكتب في جواب الورقة.</param>
/// <param name="Kind">مفتاح السجلّ: <c>customer</c> · <c>supplier</c> · …</param>
/// <param name="SubjectText">كلام المستخدم كما بحث به الوكيل — منه يُركَّب العنوان بلغة القارئ.</param>
/// <param name="Options">الخيارات المرسومة من السجلّ المحلّي.</param>
/// <param name="AllowsCreate">هل يُتاح «جديد»؟</param>
internal sealed record AgentQuestionSheetDto(
    string QuestionId,
    string Kind,
    string SubjectText,
    IReadOnlyList<AgentQuestionOptionDto> Options,
    bool AllowsCreate);

/// <summary>
/// حال مساحة العمل كلُّه في جسمٍ واحد — <b>وهو ما تقرؤه اللوحة حين تُعيد الاتصال</b>.
/// </summary>
/// <param name="AgentSessionId">معرّف الجلسة.</param>
/// <param name="Phase">طور الدور: <c>running</c> · <c>awaitingHuman</c> · <c>completed</c> · <c>refused</c>.</param>
/// <param name="TurnId">الدور الجاري أو آخر دور، أو <c>null</c> إن لم يبدأ دورٌ بعد.</param>
/// <param name="LastSequence">مؤشّر آخر حدثٍ في السجلّ — تبدأ منه اللوحة قراءتها.</param>
/// <param name="Plan">خطوات الخطّة بحالها الآن.</param>
/// <param name="PendingConfirmation">ما ينتظر تأكيداً، أو <c>null</c>.</param>
/// <param name="PendingQuestion">ورقة السؤال المعلَّقة، أو <c>null</c>.</param>
internal sealed record AgentSessionDto(
    string AgentSessionId,
    string Phase,
    string? TurnId,
    int LastSequence,
    IReadOnlyList<AgentPlanStepDto> Plan,
    AgentConfirmationDto? PendingConfirmation,
    AgentQuestionSheetDto? PendingQuestion);

/// <summary>دورٌ بدأ. <b>ولا ينتظر هذا الجواب انتهاءه</b> — الأحداث تُقرأ بمؤشّرها.</summary>
/// <param name="TurnId">معرّف الدور.</param>
/// <param name="After">المؤشّر الذي تبدأ منه اللوحة قراءة أحداث هذا الدور.</param>
internal sealed record AgentTurnDto(string TurnId, int After);

/// <summary>
/// حدثٌ واحد في سجلّ المساحة.
/// <para>
/// <b>ولا يحمل معرّف صفٍّ ولا اسمَ طرفٍ ولا عدد مرشّحين:</b> ما يعبر إلى الشاشة مسارُ
/// شاشةٍ أو مِقبضٌ معتِم، وما يعبر إلى النموذج أقلّ من ذلك.
/// </para>
/// </summary>
/// <param name="Sequence">رقمه في الجلسة — يُمرَّر <c>after</c> في الطلب التالي.</param>
/// <param name="TurnId">الدور الذي أنتجه.</param>
/// <param name="Kind">شكله.</param>
/// <param name="Text">النصّ المعروض، أو كلام البحث في حدث ورقة السؤال.</param>
/// <param name="ToolName">اسم الأداة.</param>
/// <param name="QuestionId">معرّف الورقة المعتِم.</param>
/// <param name="RegisterKey">مفتاح السجلّ في حدث ورقة السؤال.</param>
/// <param name="ScreenRoute">مسار شاشة المسوّدة.</param>
/// <param name="StepId">الخطوة المرتبطة، إن وُجدت.</param>
/// <param name="Steps">عناوين الخطوات في حدث الخطّة.</param>
/// <param name="Refusals">أسباب الرفض.</param>
internal sealed record AgentTurnEventDto(
    int Sequence,
    string TurnId,
    string Kind,
    string? Text,
    string? ToolName,
    string? QuestionId,
    string? RegisterKey,
    string? ScreenRoute,
    string? StepId,
    IReadOnlyList<string> Steps,
    IReadOnlyList<ApiErrorDto> Refusals);

/// <summary>
/// صفحةُ أحداثٍ بعد مؤشّر. <b>وقائمةٌ فارغة ليست نهاية</b>: هي «لا جديد بعدُ»، ويُعاد
/// الطلب بالمؤشّر نفسه — والطور يقول هل ما زال هناك ما يُنتظَر.
/// </summary>
/// <param name="Events">الأحداث بترتيبها.</param>
/// <param name="LastSequence">آخر مؤشّرٍ في هذه الصفحة — أو المُمرَّر إن كانت فارغة.</param>
/// <param name="Phase">طور الدور لحظةَ الجواب.</param>
internal sealed record AgentTurnEventPageDto(
    IReadOnlyList<AgentTurnEventDto> Events,
    int LastSequence,
    string Phase);

/// <summary>
/// إنفاق المنشأة في نافذتها الجارية.
/// <para>
/// <b>والوحدة رموزٌ لا ريالات، وذلك قرارٌ لا كسل:</b> الرمز واقعةٌ يُعيدها المزوّد ونقيسها؛
/// والريال يحتاج جدول أسعارٍ ليس في هذا المستودع، وسعرٌ يُكتب في الشيفرة يتجمّد بينما
/// يتحرّك عند المزوّد.
/// </para>
/// </summary>
/// <param name="Billable">مجموع الرموز المحاسَب عليها في النافذة، نصّاً.</param>
/// <param name="Ceiling">السقف نصّاً، أو <c>null</c> لمنشأةٍ تعمل بمفتاحها.</param>
/// <param name="Turns">عدد الأدوار المُحاسَبة في النافذة.</param>
/// <param name="WindowSeconds">طول نافذة المحاسبة بالثواني.</param>
/// <param name="BringsItsOwnKey">هل تعمل هذه المنشأة على مفتاحها؟ ومن جاء بمفتاحه لا يُسقَف بسقف المالك.</param>
internal sealed record AgentSpendDto(
    string Billable,
    string? Ceiling,
    int Turns,
    int WindowSeconds,
    bool BringsItsOwnKey);

/// <summary>رسالةُ المستخدم إلى الوكيل. <b>حقلٌ واحد لا ثانيَ له.</b></summary>
/// <param name="Text">
/// كلام المستخدم بأسمائه. <b>ولا حقل «نموذج» ولا «مفتاح» ولا «تعليمات نظام»</b>: الثلاثة
/// إعدادُ خادمٍ لا حقلُ طلب، وحقلٌ يختار منه الطالب نموذجَه يجعل عميلاً يبدّل النموذج في
/// وسط محادثةٍ فيُبطل ذاكرة البادئة بلا أن يعلم.
/// </param>
internal sealed record AgentMessageRequestDto(string? Text);

/// <summary>
/// تأكيد شكل بيانات خطوة — <b>أو رفضه</b>.
/// </summary>
/// <param name="Accepted">
/// ‏<c>true</c> إن قَبِل المستخدم <b>شكل</b> البيانات. <b>ولا يعني الترحيل</b>: الناتج
/// بعده مسوّدةٌ كما كان قبله. و<c>false</c> يوقف الخطوة ولا يقتل الدور.
/// </param>
internal sealed record AgentStepConfirmationRequestDto(bool? Accepted);

/// <summary>
/// جواب ورقة السؤال. <b>مفتاحان لا ثالث لهما</b> — وهو الشكل نفسه الذي يبنيه المتصفّح.
/// </summary>
/// <param name="QuestionId">معرّف الورقة المعتِم كما ورد في حالها.</param>
/// <param name="OptionToken">
/// رمز الخيار المختار. <b>ولا موضعَ ولا نصَّ ولا عدد</b>: الموضع يُعدّ، ومن يرى «الثالث»
/// يعلم أن الخيارات كانت ثلاثةً على الأقل.
/// </param>
internal sealed record AgentAnswerRequestDto(string? QuestionId, string? OptionToken);
