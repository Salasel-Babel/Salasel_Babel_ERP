using Babel.Ai.Extraction.GitHubModels;
using Babel.SharedKernel;

namespace Babel.Ai.Agent;

/// <summary>
/// إعدادات حلقة الوكيل.
/// <para>
/// <b>ولا مفتاح في هذا النوع — اسمُ متغيّر البيئة وحده.</b> نفس ما يفعله
/// <c>GitHubModelsOptions.TokenVariable</c> في هذا المستودع وللسبب نفسه المكتوب هناك:
/// «رمزٌ في كائن إعدادات يظهر في سجل، وفي تتبّع، وفي رسالة استثناء، وفي لقطة ذاكرة».
/// والفحص أدناه يستدعي <see cref="GitHubModelsOptions.LooksLikeASecret"/> نفسها لا نسخةً
/// منها — فالسابقة تُعاد لا تُقلَّد.
/// </para>
/// <para>
/// <b>وما لا يتغيّر بين نداءَين عمداً:</b> النموذج، والتفكير، والجهد. تغييرُ أيٍّ منها في
/// وسط محادثةٍ يُبطل ذاكرة الرسائل — وهي أشهر قاتلٍ صامت للذاكرة بعد حقن التاريخ في
/// نصّ النظام. ولذلك تُثبَّت هنا لا تُمرَّر في كل طلب.
/// </para>
/// </summary>
public sealed class AgentOptions
{
    /// <summary>القيمة الافتراضية لاسم متغيّر البيئة الحامل لمفتاح النموذج.</summary>
    public const string DefaultApiKeyVariable = "ANTHROPIC_API_KEY";

    /// <summary><b>اسم</b> متغيّر البيئة الحامل للمفتاح — لا المفتاح.</summary>
    public string ApiKeyVariable { get; set; } = DefaultApiKeyVariable;

    /// <summary>
    /// معرّف النموذج. مثبَّت لا مُمرَّر: نموذجٌ يتغيّر بين نداءَين يفتح فضاء ذاكرةٍ ثانياً،
    /// والذاكرة مربوطةٌ بالنموذج.
    /// </summary>
    public string ModelId { get; set; } = "claude-opus-5";

    /// <summary>
    /// سقف رموز المُخرَج. مرتفعٌ لأن المسار <b>متدفّق</b>: التدفّق يرفع مهلة HTTP عن الطريق،
    /// واللوحة تُري تقدّماً بدل صمتٍ طويل.
    /// </summary>
    public int MaxOutputTokens { get; set; } = 64_000;

    /// <summary>سقف نداءات البحث في الدور الواحد — حاجزُ السبر الأول.</summary>
    public int LookupBudgetPerTurn { get; set; } = 4;

    /// <summary>
    /// سقف دورات «نداء نموذج ← تنفيذ أدوات ← نداء نموذج» في الدور الواحد.
    /// دورةٌ بلا سقف تُنفق مالاً ولا تُنتج مسوّدة.
    /// </summary>
    public int MaxToolIterations { get; set; } = 8;

    /// <summary>
    /// سقف إنفاق المنشأة الافتراضي <b>بالرموز</b> في نافذة المحاسبة.
    /// <para>
    /// <b>وبالرموز لا بالمال عمداً:</b> الرمز واقعةٌ يُعيدها المزوّد ونقيسها؛ والمبلغ يحتاج
    /// جدول أسعارٍ ليس في هذا المستودع. وتحويلُه بسعرٍ مكتوب في الشيفرة يجعل رقماً يتغيّر
    /// عند المزوّد ثابتاً عندنا — <see cref="AgentErrors.PriceListMissing"/> ترفض ولا تُخمّن.
    /// </para>
    /// </summary>
    public long DefaultTenantTokenCeiling { get; set; } = 5_000_000;

    /// <summary>نافذة المحاسبة. الافتراضي يوم واحد.</summary>
    public TimeSpan SpendWindow { get; set; } = TimeSpan.FromDays(1);

    /// <summary>مهلة نداء النموذج الواحد.</summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(10);

    /// <summary>
    /// يتحقق من الإعداد ويعيد كل عيوبه — <b>ومنها أن يُكتب المفتاح نفسه في حقل «اسم المتغيّر»</b>.
    /// والفحص هنا يقع قبل أن يصل ذلك إلى سجل.
    /// </summary>
    public Result Validate()
    {
        List<Error> errors = [];

        if (string.IsNullOrWhiteSpace(ApiKeyVariable))
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "api_key_variable_missing",
                "اسم متغيّر مفتاح النموذج غائب.",
                "the model key variable name is missing."));
        }
        else if (GitHubModelsOptions.LooksLikeASecret(ApiKeyVariable))
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "api_key_variable_looks_like_a_key",
                "حقل «اسم متغيّر المفتاح» يحمل ما يشبه المفتاح نفسه — والاسم يُسجَّل والمفتاح لا يُسجَّل.",
                "the key-variable field carries what looks like the key itself."));
        }

        if (string.IsNullOrWhiteSpace(ModelId))
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "model_missing",
                "معرّف النموذج غائب — ولا يُختار نموذجٌ افتراضي في الشيفرة.",
                "the model id is missing and no default is chosen in code."));
        }

        if (MaxOutputTokens < 1_024)
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "output_budget_too_small",
                "سقف رموز المُخرَج أصغر من أن يسع جسم مسوّدة — وسقفٌ منخفض يقطع JSON في منتصفه.",
                "the output token ceiling is too small to hold a draft body."));
        }

        if (LookupBudgetPerTurn < 1)
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "lookup_budget_not_positive",
                "سقف البحث في الدور يجب أن يكون واحداً فأكثر.",
                "the per-turn lookup budget must be at least one."));
        }

        if (MaxToolIterations < 1)
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "tool_iterations_not_positive",
                "سقف دورات الأدوات يجب أن يكون واحداً فأكثر.",
                "the tool-iteration ceiling must be at least one."));
        }

        if (DefaultTenantTokenCeiling < 1)
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "spend_ceiling_not_positive",
                "سقف الإنفاق يجب أن يكون موجباً — وسقفٌ صفر يمنع كل شيء ويُقرأ عطلاً.",
                "the spend ceiling must be positive."));
        }

        if (SpendWindow <= TimeSpan.Zero)
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "spend_window_not_positive",
                "نافذة المحاسبة يجب أن تكون موجبة.",
                "the accounting window must be positive."));
        }

        if (Timeout <= TimeSpan.Zero)
        {
            errors.Add(new Error(
                AgentErrors.CodePrefix + "timeout_not_positive",
                "مهلة نداء النموذج يجب أن تكون موجبة.",
                "the model call timeout must be positive."));
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }
}
