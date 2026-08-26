using Babel.Ai.Suggestions;
using Babel.SharedKernel;

namespace Babel.Ai.Extraction.GitHubModels;

/// <summary>
/// من أين يُقرأ السرّ. <b>مقبضٌ إلى سرّ لا السرّ نفسه</b> — نفس شكل حدّ الالتزام،
/// ونفس السبب: سرٌّ في كائن ينتهي في سجل وفي تتبّع ولقطة ذاكرة.
/// </summary>
public interface ISecretReader
{
    /// <summary>يقرأ قيمة السرّ باسم متغيّره، أو <c>null</c> إن لم يكن مضبوطاً.</summary>
    /// <param name="variable">اسم المتغيّر.</param>
    string? Read(string variable);
}

/// <summary>
/// يقرأ السرّ من بيئة العملية. <b>ولا مسار «اقرأ من ملف» في هذا المستودع</b>: كل مسار
/// كهذا يُغري بإيداع سرّ اختبار «مؤقتاً»، وقد تسرّب اعتماد هنا مرّتين.
/// </summary>
public sealed class EnvironmentSecretReader : ISecretReader
{
    /// <inheritdoc />
    public string? Read(string variable)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(variable);

        string? value = Environment.GetEnvironmentVariable(variable);
        return string.IsNullOrWhiteSpace(value) ? null : value;
    }
}

/// <summary>
/// <b>مزوّد استخراج فوق كتالوج GitHub Models</b>، خلف الحدّ القائم
/// <see cref="IInvoiceExtractionProvider"/> ولا شيء غيره (‏ADR-0015).
/// <para>
/// <b>وما يجعله صالحاً للعرض ليس نجاحه بل تصرّفه عند كل عطل:</b> ستّة أصناف مُسمّاة،
/// لكلٍّ رسالة عربية تقول ما العمل، وكلّها <b>قيمةٌ مُعادة لا استثناء مرمي</b>. والعرض
/// يموت على العطل الذي لم يُسمَّ.
/// </para>
/// <para>
/// <b>ولا يُعلَن حتمياً</b>: <c>IsDeterministic = false</c>. وحرارة صفر تُقرّب ولا تضمن،
/// وإعلانُ الحتمية على حدٍّ احتمالي يجعل الفارق يظهر أول مرّة أمام عميل.
/// </para>
/// <para>
/// <b>وإقامة البيانات <c>Offshore</c> صراحةً</b>: الخدمة خارج المملكة، وذلك قرار مالك
/// مكتوب لا تفصيلة تركيب (‏ADR-0024 دليل 7 · ADR-0010). ومن يقرأ القدرات وقت التركيب
/// يعرف أن بايتات المستند ستغادر المنشأة <b>قبل</b> أول صورة لا بعدها.
/// </para>
/// </summary>
public sealed class GitHubModelsExtractionProvider : IInvoiceExtractionProvider
{
    /// <summary>معرّف المزوّد كما يُسجَّل على كل مسوّدة.</summary>
    public const string Id = "github.models.extractor.v1";

    private readonly GitHubModelsOptions _options;
    private readonly IModelWire _wire;
    private readonly ISecretReader _secrets;
    private readonly IPostingVocabulary _vocabulary;

    /// <summary>ينشئ المزوّد. <b>الإعداد يُتحقَّق منه هنا لا عند أول نداء</b>.</summary>
    /// <param name="options">الإعدادات.</param>
    /// <param name="wire">السلك.</param>
    /// <param name="secrets">قارئ الأسرار.</param>
    /// <param name="vocabulary">المفردات المغلقة.</param>
    /// <exception cref="ArgumentException">إعدادٌ ناقص أو معطوب.</exception>
    public GitHubModelsExtractionProvider(
        GitHubModelsOptions options,
        IModelWire wire,
        ISecretReader secrets,
        IPostingVocabulary vocabulary)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(wire);
        ArgumentNullException.ThrowIfNull(secrets);
        ArgumentNullException.ThrowIfNull(vocabulary);

        Result valid = options.Validate();
        if (valid.IsFailure)
        {
            throw new ArgumentException(
                string.Join(" · ", valid.Errors.Select(static error => error.MessageAr)), nameof(options));
        }

        _options = options;
        _wire = wire;
        _secrets = secrets;
        _vocabulary = vocabulary;

        Capabilities = new ExtractionProviderCapabilities(
            ProviderId: Id,
            DisplayNameKey: "ai.capture.provider.github_models",
            Residency: ExtractionResidency.Offshore,
            ReadsLineItems: true,
            IsDeterministic: false,
            Timeout: options.Timeout);
    }

    /// <inheritdoc />
    public ExtractionProviderCapabilities Capabilities { get; }

    /// <inheritdoc />
    public async ValueTask<Result<ExtractionOutput>> ExtractAsync(
        ExtractionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        // ── 1 · السرّ. غيابُه ليس عطلاً غامضاً بل حالةٌ مُسمّاة لها مخرج مُعلَن ──────
        string? token = _secrets.Read(_options.TokenVariable);
        if (token is null)
        {
            return Result<ExtractionOutput>.Failure(GitHubModelsErrors.TokenNotInEnvironment(_options.TokenVariable));
        }

        // ── 2 · التوجيه، وحارسه قبل الخروج لا بعد العودة ────────────────────────
        Result<string> systemPrompt = ExtractionPrompt.System(_vocabulary);
        if (systemPrompt.IsFailure)
        {
            return Result<ExtractionOutput>.Failure(systemPrompt.Errors);
        }

        string body = ExtractionPrompt.Body(_options, systemPrompt.Value, request);

        // ── 3 · السلك. كل ما يقع عليه يُصنَّف عند هذا الحدّ لا داخل تنفيذ السلك ────
        ModelWireResponse response;
        try
        {
            response = await _wire
                .SendAsync(_options.Endpoint!, token, body, _options.Timeout, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (Exception exception) when (exception is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            return Result<ExtractionOutput>.Failure(
                ModelFaultClassifier.ClassifyException(exception, _options.Endpoint!, _options));
        }

        // ── 4 · رمز الحالة ────────────────────────────────────────────────────
        if (ModelFaultClassifier.Classify(response, _options) is { } fault)
        {
            return Result<ExtractionOutput>.Failure(fault);
        }

        // ── 5 · الغلاف: امتناعٌ، أو قطعٌ، أو فراغٌ، أو محتوى ───────────────────
        Result<string> content = ChatEnvelope.Content(response.Body, _options.MaxOutputTokens);
        if (content.IsFailure)
        {
            return Result<ExtractionOutput>.Failure(content.Errors);
        }

        // ── 6 · ولا تحقّق من المخطط هنا. المُخرَج يعود **خاماً** كما يفرض الحدّ، ──
        //        ويتحقّق منه ExtractionSchema عند حدّ الالتقاط — الموضع الذي يمرّ به
        //        كل مزوّد. تحقّقٌ داخل مزوّد واحد يعني مزوّداً ثانياً بلا تحقّق.
        return Result<ExtractionOutput>.Success(new ExtractionOutput(Id, Strip(content.Value)));
    }

    /// <summary>
    /// يزيل سياج الشيفرة إن أحاط النموذج جوابه به. <b>وهذا كل التساهل المسموح</b>:
    /// ‏<c>```json</c> ظاهرة مقيسة في نماذج كثيرة وليست مُخرَجاً مشوَّهاً، أما ما بداخله
    /// فيمرّ على المخطط كاملاً بلا تليين واحد.
    /// </summary>
    private static string Strip(string content)
    {
        string text = content.Trim();

        if (!text.StartsWith("```", StringComparison.Ordinal))
        {
            return text;
        }

        int firstBreak = text.IndexOf('\n');
        int lastFence = text.LastIndexOf("```", StringComparison.Ordinal);

        return firstBreak > 0 && lastFence > firstBreak ? text[(firstBreak + 1)..lastFence].Trim() : text;
    }
}
