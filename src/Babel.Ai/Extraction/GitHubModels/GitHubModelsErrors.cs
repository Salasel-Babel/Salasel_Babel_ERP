using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction.GitHubModels;

/// <summary>
/// <b>أعطال مزوّد GitHub Models — كلّها مُسمّاة، ولكلٍّ سلوك مُقرَّر.</b>
/// <para>
/// والقائمة مكتوبة كاملةً عمداً لا واحداً واحداً عند وقوعه: <b>العرض يموت على العطل
/// الذي لم يُعالَج</b>، والعطل الذي لم يُعالَج هو دائماً الذي لم يُسمَّ. وكل رسالة تقول
/// <b>ما العمل</b> لا «حدث خطأ»: مَن يقرؤها واقفٌ أمام جمهور.
/// </para>
/// </summary>
public static class GitHubModelsErrors
{
    /// <summary>لا عنوان في الإعداد.</summary>
    public static readonly Error EndpointMissing = new(
        "ai.provider.endpoint_missing",
        "لا عنوان لنقطة الاستدلال في الإعداد. العنوان يُقرأ من الإعداد ولا يُكتب في الشيفرة.",
        "No inference endpoint in configuration; the endpoint is read from configuration, never written in code.");

    /// <summary>العنوان ليس HTTPS.</summary>
    public static Error EndpointNotHttps(string endpoint) => new(
        "ai.provider.endpoint_not_https",
        "العنوان «" + endpoint + "» ليس HTTPS مطلقاً. ورمزُ وصولٍ على قناة غير مشفّرة رمزٌ محروق.",
        "The endpoint '" + endpoint + "' is not an absolute HTTPS URI.");

    /// <summary>لا معرّف نموذج.</summary>
    public static readonly Error ModelIdMissing = new(
        "ai.provider.model_id_missing",
        "لا معرّف نموذج في الإعداد. ومعرّفٌ افتراضي مكتوب في الشيفرة يجعل ترقية الكتالوج تغييرَ شيفرة.",
        "No model id in configuration.");

    /// <summary>لا اسم لمتغيّر الرمز.</summary>
    public static readonly Error TokenVariableMissing = new(
        "ai.provider.token_variable_missing",
        "لا اسم لمتغيّر البيئة الحامل للرمز.",
        "No environment-variable name for the access token.");

    /// <summary>
    /// كُتب رمزٌ في حقل اسم المتغيّر. <b>يُرفض ولا يُقبل تساهلاً</b> — ولا يُذكر النصّ
    /// في الرسالة، لأن رسالة الخطأ تُكتب في سجل.
    /// </summary>
    public static readonly Error TokenVariableLooksLikeAToken = new(
        "ai.provider.token_variable_looks_like_a_token",
        "حقل «اسم متغيّر الرمز» يحمل ما يشبه الرمز نفسه لا اسمه. "
        + "والحقل يحمل اسماً يُقرأ منه السرّ وقت النداء؛ ورمزٌ في كائن إعدادات ينتهي في سجل وفي تتبّع ولقطة ذاكرة. "
        + "ولم تُذكر القيمة في هذه الرسالة عمداً.",
        "The token-variable field carries something shaped like the token itself; the value is deliberately not echoed.");

    /// <summary>مهلة غير موجبة.</summary>
    public static readonly Error TimeoutNotPositive = new(
        "ai.provider.timeout_not_positive",
        "المهلة يجب أن تكون موجبة. ومهلة صفرية تُنتج إلغاءً فورياً يُقرأ «النموذج لا يستجيب».",
        "The timeout must be positive.");

    /// <summary>سقف مُخرَج صغير.</summary>
    public static Error OutputBudgetTooSmall(int budget) => new(
        "ai.provider.output_budget_too_small",
        "سقف رموز المُخرَج " + Num(budget) + " صغير. "
        + "والسقف الصغير يقطع JSON في منتصفه، فيصل الحدَّ «نصّاً ليس JSON صالحاً» — وهي رسالة ترسل المُصلِح إلى المكان الخطأ.",
        "The output token budget " + Num(budget) + " is too small and truncates JSON mid-object.");

    /// <summary>لا رمز في البيئة.</summary>
    public static Error TokenNotInEnvironment(string variable) => new(
        "ai.provider.token_not_in_environment",
        "لا قيمة للمتغيّر «" + variable + "» في بيئة العملية. "
        + "الأسرار تُقرأ من البيئة ولا تُودَع في المستودع ولا في ملف إعداد. "
        + "وحتى يُضبط المتغيّر يعمل النظام بالمزوّد الحتمي بلا شبكة.",
        "No value for '" + variable + "' in the process environment; until it is set, the deterministic provider is used.");

    /// <summary>رُفض الرمز.</summary>
    public static Error TokenRejected(int status, string variable) => new(
        "ai.provider.token_rejected",
        "رفضت الخدمة الرمز (" + Num(status) + "). "
        + "والرمز في المتغيّر «" + variable + "»: تحقّق أن له صلاحية «models» وأنه لم تنتهِ مدّته. "
        + "ولم تُذكر قيمته في هذه الرسالة.",
        "The service rejected the token (" + Num(status) + "); check that it carries the 'models' scope and has not expired.");

    /// <summary>تجاوز الحدّ المسموح.</summary>
    public static Error RateLimited(int? retryAfterSeconds) => new(
        "ai.provider.rate_limited",
        "تجاوزنا حدّ الطلبات على الكتالوج المجاني"
        + (retryAfterSeconds is { } seconds ? "، والخدمة تطلب الانتظار " + Num(seconds) + " ثانية." : " ولم تُعلن الخدمة مدّة انتظار.")
        + " ولا يُعاد الطلب تلقائياً: إعادةٌ صامتة تحت الحدّ تُطيل الصمت أمام الجمهور بدل أن تُنهيه. "
        + "والمخرج الفوري: المزوّد الحتمي يملأ المسوّدة نفسها بلا شبكة.",
        "The free catalogue rate limit was reached"
        + (retryAfterSeconds is { } wait ? "; the service asks for " + Num(wait) + " seconds." : " with no announced wait.")
        + " No automatic retry is performed.");

    /// <summary>انتهت المهلة.</summary>
    public static Error TimedOut(TimeSpan timeout) => new(
        "ai.provider.timed_out",
        "لم يجب النموذج خلال " + Show(timeout) + " ثانية. "
        + "والاستخراج قراءةٌ لا تكتب شيئاً عند الطرف الآخر، فإعادة المحاولة آمنة تماماً — "
        + "بخلاف إرسال مستند ضريبي حيث المهلة تعني «لا أدري».",
        "The model did not answer within " + Show(timeout) + " seconds; extraction writes nothing remotely, so retrying is entirely safe.");

    /// <summary>النموذج غير متاح.</summary>
    public static Error ModelUnavailable(string modelId, int status) => new(
        "ai.provider.model_unavailable",
        "النموذج «" + modelId + "» غير متاح الآن (" + Num(status) + "). "
        + "وكتالوج GitHub Models يُضيف ويُخرج نماذج بلا إشعار، فمعرّف النموذج إعدادٌ يُبدَّل بلا نشر.",
        "Model '" + modelId + "' is unavailable (" + Num(status) + ").");

    /// <summary>تعذّر الوصول إلى الخدمة.</summary>
    public static Error Unreachable(string host, string detail) => new(
        "ai.provider.unreachable",
        "تعذّر الوصول إلى «" + host + "»: " + detail + ". "
        + "وأشهر أسبابه شبكةٌ مغلقة على مضيف الخدمة — يُفحص المنفذ الصادر قبل أي تغيير في الشيفرة. "
        + "والمزوّد الحتمي يعمل بلا شبكة أصلاً.",
        "Could not reach '" + host + "': " + detail + ".");

    /// <summary>ردّ غير متوقّع.</summary>
    public static Error UnexpectedStatus(int status, string excerpt) => new(
        "ai.provider.unexpected_status",
        "ردّ غير متوقّع من الخدمة (" + Num(status) + "): " + excerpt,
        "Unexpected response from the service (" + Num(status) + "): " + excerpt);

    /// <summary>غلاف الردّ نفسه لا يُقرأ.</summary>
    public static Error EnvelopeUnreadable(string detail) => new(
        "ai.provider.envelope_unreadable",
        "غلاف ردّ الخدمة نفسه ليس JSON مقروءاً: " + detail + ". "
        + "وهذا عطلٌ في الوسيط أو في بوّابة تعترض الطلب، لا في مُخرَج النموذج.",
        "The service response envelope is not readable JSON: " + detail + ".");

    /// <summary>لا محتوى في الردّ.</summary>
    public static readonly Error NoContent = new(
        "ai.provider.no_content",
        "ردّت الخدمة بلا محتوى. لا اقتراح، ولا مسوّدة — والفراغ يُعلَن ولا يُملأ.",
        "The service answered with no content; emptiness is declared, not filled in.");

    /// <summary>
    /// امتنع النموذج. <b>عطلٌ قائم بذاته لا «مُخرَج مشوَّه»</b>: الامتناع جوابٌ مفهوم
    /// من النموذج، ورسالته للمستخدم مختلفة تماماً عن رسالة الخلل.
    /// </summary>
    public static Error ModelRefused(string reason) => new(
        "ai.provider.model_refused",
        "امتنع النموذج عن الإجابة" + (reason.Length > 0 ? ": " + reason : ".")
        + " والامتناع ليس عطلاً في النظام: يُعرَض للمستخدم كما هو، وتُملأ المسوّدة بيد إنسان.",
        "The model refused to answer" + (reason.Length > 0 ? ": " + reason : ".") + " This is not a system fault.");

    /// <summary>قُطع المُخرَج قبل تمامه.</summary>
    public static Error OutputTruncated(int budget) => new(
        "ai.provider.output_truncated",
        "قُطع مُخرَج النموذج عند سقف " + Num(budget) + " رمزاً قبل أن يكتمل. "
        + "ولو مرّ لَوصل الحدَّ «ليس JSON صالحاً» — وهي رسالة صحيحة عن عَرَض لا عن سبب.",
        "The model output was truncated at the " + Num(budget) + "-token budget before completing.");

    /// <summary>سؤالٌ عن حساب. يُرفض قبل أن يُرسَل.</summary>
    public static Error PromptNamesLedgerCode(string detail) => new(
        "ai.provider.prompt_names_a_ledger_code",
        "نصّ التوجيه المُرسَل إلى النموذج يحمل ما يشبه رمز حساب: " + detail + ". "
        + "والنموذج لا يرى دليل الحسابات بحال (‏ADR-0024)، والرفض هنا يقع قبل الإرسال لا بعده.",
        "The outgoing prompt carries something shaped like a ledger code; refused before it is sent.");

    /// <summary>المفردات ضامرة.</summary>
    public static Error VocabularyTooSmall(int events, int roles) => new(
        "ai.provider.vocabulary_too_small",
        "المفردات المغلقة ضامرة: " + Num(events) + " حدثاً و" + Num(roles) + " دوراً. "
        + "وقائمةٌ فارغة تُرسَل إلى النموذج تجعله يخترع رمزاً حتماً، ثم يرفضه الحارس فيبدو النظام معطلاً.",
        "The closed vocabulary is too small: " + Num(events) + " events and " + Num(roles) + " roles.");

    private static string Num(int value) => value.ToString(CultureInfo.InvariantCulture);

    private static string Show(TimeSpan value) => value.TotalSeconds.ToString("0.#", CultureInfo.InvariantCulture);
}
