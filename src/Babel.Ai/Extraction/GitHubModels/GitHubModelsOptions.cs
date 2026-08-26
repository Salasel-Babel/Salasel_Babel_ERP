using Babel.SharedKernel;

namespace Babel.Ai.Extraction.GitHubModels;

/// <summary>
/// إعدادات مزوّد GitHub Models. <b>ثلاثتها من الإعداد، ولا واحدة منها في الشيفرة</b>:
/// اسم السرّ، والعنوان، ومعرّف النموذج.
/// <para>
/// <b>ولاحظ ما ليس هنا:</b> لا حقل يحمل الرمز نفسه. الحقل يحمل <b>اسم</b> المتغيّر الذي
/// يُقرأ منه وقت النداء — نفس شكل <c>SecretRef</c> في حدّ الالتزام وللسبب نفسه: رمزٌ في
/// كائن إعدادات يظهر في سجل، وفي تتبّع، وفي رسالة استثناء، وفي لقطة ذاكرة. وقد تسرّب
/// اعتمادٌ في هذا المستودع مرّتين.
/// </para>
/// </summary>
public sealed class GitHubModelsOptions
{
    /// <summary>القيمة الافتراضية لاسم متغيّر البيئة الحامل للرمز.</summary>
    public const string DefaultTokenVariable = "BABEL_AI_GITHUB_TOKEN";

    /// <summary>
    /// <b>اسم</b> متغيّر البيئة الذي يحمل رمز GitHub — لا الرمز.
    /// </summary>
    public string TokenVariable { get; set; } = DefaultTokenVariable;

    /// <summary>عنوان نقطة الاستدلال. يُقرأ من الإعداد ولا يُكتب في الشيفرة.</summary>
    public Uri? Endpoint { get; set; }

    /// <summary>معرّف النموذج في كتالوج GitHub Models.</summary>
    public string ModelId { get; set; } = string.Empty;

    /// <summary>
    /// المهلة المُعلنة. <b>وهي مهلة عرضٍ لا مهلة سلامة:</b> الاستخراج قراءةٌ لا تكتب
    /// شيئاً عند الطرف الآخر، فإعادة المحاولة بعدها آمنة تماماً — وهذا هو الفرق الجوهري
    /// عن حدّ الالتزام حيث المهلة تعني «لا أدري» ولا يجوز بعدها إرسال أعمى.
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromSeconds(20);

    /// <summary>سقف رموز المُخرَج. سقفٌ منخفض يقطع JSON في منتصفه، وهو عطل مُعالَج بذاته.</summary>
    public int MaxOutputTokens { get; set; } = 2_000;

    /// <summary>هل الإعداد مكتمل؟ ناقصُه يعني الارتداد إلى المزوّد الحتمي.</summary>
    public bool IsConfigured =>
        Endpoint is not null && !string.IsNullOrWhiteSpace(ModelId) && !string.IsNullOrWhiteSpace(TokenVariable);

    /// <summary>
    /// يتحقق من الإعداد ويعيد كل عيوبه. <b>ومنها عيبٌ لا يخطر على بال:</b> أن يُكتب
    /// الرمز نفسه في حقل «اسم المتغيّر». الفحص هنا يقع قبل أن يصل ذلك إلى سجل.
    /// </summary>
    public Result Validate()
    {
        List<Error> errors = [];

        if (Endpoint is null)
        {
            errors.Add(GitHubModelsErrors.EndpointMissing);
        }
        else if (!Endpoint.IsAbsoluteUri || Endpoint.Scheme != Uri.UriSchemeHttps)
        {
            errors.Add(GitHubModelsErrors.EndpointNotHttps(Endpoint.ToString()));
        }

        if (string.IsNullOrWhiteSpace(ModelId))
        {
            errors.Add(GitHubModelsErrors.ModelIdMissing);
        }

        if (string.IsNullOrWhiteSpace(TokenVariable))
        {
            errors.Add(GitHubModelsErrors.TokenVariableMissing);
        }
        else if (LooksLikeASecret(TokenVariable))
        {
            errors.Add(GitHubModelsErrors.TokenVariableLooksLikeAToken);
        }

        if (Timeout <= TimeSpan.Zero)
        {
            errors.Add(GitHubModelsErrors.TimeoutNotPositive);
        }

        if (MaxOutputTokens < 256)
        {
            errors.Add(GitHubModelsErrors.OutputBudgetTooSmall(MaxOutputTokens));
        }

        return errors.Count == 0 ? Result.Success() : Result.Failure(errors);
    }

    /// <summary>
    /// هل يشبه هذا النصّ رمزاً لا اسمَ متغيّر؟ البوادئ المعروفة لرموز GitHub، أو طولٌ
    /// لا يكون لاسم متغيّر بيئة.
    /// </summary>
    /// <param name="value">النصّ.</param>
    public static bool LooksLikeASecret(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        string[] prefixes = ["ghp_", "gho_", "ghu_", "ghs_", "ghr_", "github_pat_", "Bearer "];

        return prefixes.Any(prefix => value.StartsWith(prefix, StringComparison.Ordinal)) || value.Length > 80;
    }
}
