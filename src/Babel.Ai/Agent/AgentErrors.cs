using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// <b>مفردات رفض حلقة الوكيل — جُملٌ تُقال لا رموزٌ تُسجَّل.</b>
/// <para>
/// وكلّها تعود إلى النموذج داخل <c>tool_result { is_error: true }</c> لا كاستثناءٍ يقتل
/// الدور: النموذج يقرأ سبب الرفض <b>فيُصحّح</b>. ونصٌّ يقول «فشل» ولا يقول أيّ شيء فشل
/// يجعله يعيد المحاولة نفسها إلى أن ينفد سقف الدورات.
/// </para>
/// </summary>
public static class AgentErrors
{
    /// <summary>صدر رموز الحلقة.</summary>
    public const string CodePrefix = "ai.agent.";

    /// <summary>اسمُ أداةٍ ليس في الكتالوج المغلق.</summary>
    /// <param name="name">الاسم كما ورد من النموذج.</param>
    public static Error UnknownTool(string name) => new(
        CodePrefix + "tool_unknown",
        "لا أداة بهذا الاسم «" + name + "». والكتالوج مغلق: ما ليس فيه لا يُنفَّذ ولا يُقارَب بأقرب شبيه.",
        "there is no tool named '" + name + "'; the catalogue is closed.");

    /// <summary>فعلٌ ممنوع أو غير مصنَّف — الحارس المنطوق نفسه يُنادى حرفياً.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="why">سبب الرفض كما كتبه <c>VoiceOperationGuard</c>.</param>
    public static Error OperationRefused(string operationId, string why) => new(
        CodePrefix + "operation_refused",
        "العملية «" + operationId + "» لا يبلغها الوكيل: " + why + ".",
        "the operation '" + operationId + "' is out of the agent's reach: " + why + ".");

    /// <summary>عمليةٌ ليست في العقد المنشور — بابٌ لا وجود له.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    public static Error OperationNotPublished(string operationId) => new(
        CodePrefix + "operation_not_published",
        "العملية «" + operationId + "» ليست في العقد المنشور. وبابٌ لا وجود له يُنتج مسوّدةً لا تُحفَظ.",
        "the operation '" + operationId + "' is not in the published contract.");

    /// <summary>مسارٌ ينتهي بمقطعٍ لا يُعكَس — يُقرأ من المسار لا من الاسم وحده.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    /// <param name="path">المسار المنشور.</param>
    public static Error OperationIsIrreversible(string operationId, string path) => new(
        CodePrefix + "operation_irreversible",
        "العملية «" + operationId + "» تبلغ «" + path + "» — وهو بابُ أثرٍ لا يُعكَس. "
        + "والوكيل يبلغ المسوّدة وحدها، والترحيل فعلٌ بصريّ يدويّ على الشاشة.",
        "the operation '" + operationId + "' reaches '" + path + "', an irreversible door.");

    /// <summary>الاستحقاق لا يبلغ وحدة العملية بالكتابة.</summary>
    /// <param name="operationId">معرّف العملية.</param>
    public static Error NotEntitled(string operationId) => new(
        CodePrefix + "not_entitled",
        "الاستحقاق لا يبلغ «" + operationId + "» لهذا المستخدم. والوكيل مدخلٌ آخر إلى الصلاحيات نفسها، لا بابٌ أوسع منها.",
        "entitlement does not reach '" + operationId + "' for this caller.");

    /// <summary>حقلٌ شكلُه معرّف ولم يصل مِقبضاً.</summary>
    /// <param name="field">مسار الحقل داخل الجسم.</param>
    public static Error RawIdentifierInsteadOfHandle(string field) => new(
        CodePrefix + "handle_required",
        "الحقل «" + field + "» يقبل مِقبضاً معتِماً من lookup_entity أو ask_question، لا معرّفاً خاماً. "
        + "ومعرّفٌ يكتبه النموذج من عنده يشير إلى صفٍّ لم يطلبه أحد.",
        "the field '" + field + "' takes an opaque handle, never a raw identifier.");

    /// <summary>جسم الأداة ليس ‎JSON صالحاً — أو ليس كائناً.</summary>
    /// <param name="name">اسم الأداة.</param>
    public static Error ToolArgumentsNotAnObject(string name) => new(
        CodePrefix + "tool_arguments_malformed",
        "وسائط الأداة «" + name + "» ليست كائن JSON. ولا تُقرأ نصّاً ولا تُصلَح بالتخمين.",
        "the arguments of tool '" + name + "' are not a JSON object.");

    /// <summary>سقف نداءات البحث في الدور الواحد.</summary>
    /// <param name="budget">السقف.</param>
    public static Error LookupBudgetSpent(int budget) => new(
        CodePrefix + "lookup_budget_spent",
        "بلغ البحث سقفه في هذا الدور (" + budget.ToString(CultureInfo.InvariantCulture)
        + "). واسأل المستخدم بدل أن تُجرّب صياغةً أخرى.",
        "the per-turn lookup budget is spent.");

    /// <summary>بحثٌ ثانٍ في السجلّ نفسه بعد غموضٍ لم يُسأل عنه.</summary>
    /// <param name="registerKey">مفتاح السجلّ.</param>
    public static Error AskBeforeLookingAgain(string registerKey) => new(
        CodePrefix + "ask_before_lookup_again",
        "سبق أن غمض اسمٌ في سجلّ «" + registerKey + "» في هذا الدور. والخطوة الوحيدة المشروعة "
        + "هي ask_question بمعرّف تلك الورقة — لا بحثٌ ثانٍ بصياغةٍ أضيق.",
        "a name in register '" + registerKey + "' is already ambiguous this turn; ask_question is the only legal next step.");

    /// <summary>بحثان مفتاح أحدهما بادئةٌ صارمة للآخر — سبرٌ يُضيّق حتى يعدّ.</summary>
    public static Error LookupProbing => new(
        CodePrefix + "lookup_probing_refused",
        "بحثان في هذا الدور أحدهما بادئةُ الآخر. وتضييقُ الصياغة مرّةً بعد مرّة يعدّ السجلّ عدّاً — ولا يمرّ.",
        "two lookups this turn where one key is a strict prefix of the other; that binary-searches the register.");

    /// <summary>سقف دورات الأداة في نداءٍ واحد.</summary>
    /// <param name="limit">السقف.</param>
    public static Error ToolIterationsExhausted(int limit) => new(
        CodePrefix + "tool_iterations_exhausted",
        "بلغ الدور سقف دوراته (" + limit.ToString(CultureInfo.InvariantCulture) + ") ولم يصل إلى نتيجة. "
        + "ولا يُمدَّد السقف تلقائياً: دورةٌ لا تنتهي تُنفق مالاً ولا تُنتج مسوّدة.",
        "the turn reached its tool-iteration ceiling without concluding.");

    /// <summary>سقف الإنفاق للمنشأة.</summary>
    /// <param name="tenant">المنشأة — لا يُكتب معرّفها في الرسالة.</param>
    /// <param name="ceiling">السقف بالرموز.</param>
    public static Error SpendCeilingReached(TenantId tenant, long ceiling)
    {
        _ = tenant;
        return new Error(
            CodePrefix + "spend_ceiling_reached",
            "بلغت هذه المنشأة سقفَ إنفاقها على النموذج في هذه النافذة ("
            + ceiling.ToString(CultureInfo.InvariantCulture) + " رمزاً). "
            + "والسقف يُرفع بقرارٍ من مالك الاشتراك، لا بإعادة المحاولة.",
            "this tenant has reached its model-spend ceiling for the window.");
    }

    /// <summary>سقفٌ مطلوب بالمال ولا جدول أسعارٍ مضبوط.</summary>
    public static Error PriceListMissing => new(
        CodePrefix + "price_list_missing",
        "لا جدول أسعارٍ مضبوط، فلا يُحوَّل الإنفاق إلى مبلغ — ولا يُخمَّن سعر رمز. "
        + "اضبط السعر في الإعدادات أو اجعل السقف بالرموز.",
        "no price list is configured, so token spend is not converted to money and no rate is guessed.");

    /// <summary>الحلقة نُوديت بلا مفتاح — عطلٌ يُعلَن عند التركيب لا يُصحَّح بمفتاحٍ مخترَع.</summary>
    /// <param name="variable">اسم متغيّر البيئة المطلوب.</param>
    public static Error ApiKeyMissing(string variable) => new(
        CodePrefix + "api_key_missing",
        "لا مفتاح للنموذج في «" + variable + "». والمفتاح يُقرأ من البيئة عند النداء ولا يُكتب في إعدادٍ ولا في سجلّ.",
        "no model key in '" + variable + "'.");

    /// <summary>نصّ المستخدم رُفض عند الحدّ الخارج، فلم يُرسَل شيء.</summary>
    public static Error TurnRefusedAtTheBoundary => new(
        CodePrefix + "turn_refused_at_boundary",
        "لم يُرسَل هذا الدور إلى النموذج: فيه ما شكلُه معرّف. اكتب الرقم في حقله على الشاشة، والباقي يبقى كما هو.",
        "this turn was not sent to the model: it carries an identifier-shaped value.");
}
