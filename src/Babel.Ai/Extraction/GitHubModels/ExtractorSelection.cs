using Babel.Ai.Suggestions;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Ai.Extraction.GitHubModels;

/// <summary>لماذا وقع الاختيار على هذا المزوّد. مفتاح مورد يُحلّ في الواجهة (‏ADR-0021).</summary>
public enum ExtractorChoiceReason
{
    /// <summary>الإعداد مكتمل والسرّ موجود.</summary>
    Configured = 1,

    /// <summary>لا إعداد — النظام يعمل بالمزوّد الحتمي.</summary>
    NotConfigured = 2,

    /// <summary>الإعداد موجود ومعطوب — والعطب يُعلَن ولا يُتجاوَز بصمت.</summary>
    ConfigurationInvalid = 3,

    /// <summary>الإعداد مكتمل والسرّ غير مضبوط في البيئة.</summary>
    SecretMissing = 4,
}

/// <summary>
/// <b>أي مزوّد رُكِّب ولماذا — سؤالٌ يُجاب وقت التركيب لا وقت النداء.</b>
/// <para>
/// وهو نفس المبدأ الذي تقوم عليه <see cref="ExtractionProviderCapabilities"/>: القدرة
/// تُقرأ عند التركيب، ولا يكتشف المستخدم <b>بعد</b> أن غادرت الصورة أن مزوّداً بعيداً
/// كان مُركَّباً. والسبب مُعلَن قيمةً كي تستطيع شاشةٌ أن تقول «يعمل بلا نموذج» بدل أن
/// تبدو ذكيةً وهي حتمية.
/// </para>
/// </summary>
/// <param name="ProviderId">معرّف المزوّد المُركَّب.</param>
/// <param name="Reason">سبب الاختيار.</param>
/// <param name="MessageKey">مفتاح مورد للرسالة المعروضة.</param>
public sealed record ExtractorChoice(string ProviderId, ExtractorChoiceReason Reason, string MessageKey)
{
    /// <summary>هل المُركَّب هو المزوّد البعيد؟</summary>
    public bool IsRemote => Reason == ExtractorChoiceReason.Configured;
}

/// <summary>تركيب مزوّد الاستخراج مع ارتداده المُعلَن.</summary>
public static class ExtractorSelection
{
    /// <summary>
    /// يختار المزوّد: البعيد إن اكتمل إعداده ووُجد سرّه، <b>وإلا الحتمي</b> — فيبقى
    /// النظام كله قابلاً للتشغيل والاختبار بلا شبكة وبلا مفتاح.
    /// <para>
    /// <b>والارتداد وقت التركيب لا وقت النداء عمداً:</b> ارتدادٌ في منتصف نداء يعني أن
    /// مستنداً يُقرأ حتمياً ومستنداً يُقرأ بنموذج <b>في التشغيلة نفسها</b>، فيصير
    /// <c>ExtractionProviderId</c> على المسوّدة غير قابل للتفسير.
    /// </para>
    /// </summary>
    /// <param name="options">الإعدادات.</param>
    /// <param name="secrets">قارئ الأسرار.</param>
    public static ExtractorChoice Choose(GitHubModelsOptions options, ISecretReader secrets)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(secrets);

        if (!options.IsConfigured)
        {
            return new ExtractorChoice(
                DeterministicExtractionProvider.Id,
                ExtractorChoiceReason.NotConfigured,
                "ai.capture.extractor.not_configured");
        }

        if (options.Validate().IsFailure)
        {
            return new ExtractorChoice(
                DeterministicExtractionProvider.Id,
                ExtractorChoiceReason.ConfigurationInvalid,
                "ai.capture.extractor.configuration_invalid");
        }

        return secrets.Read(options.TokenVariable) is null
            ? new ExtractorChoice(
                DeterministicExtractionProvider.Id,
                ExtractorChoiceReason.SecretMissing,
                "ai.capture.extractor.secret_missing")
            : new ExtractorChoice(GitHubModelsExtractionProvider.Id, ExtractorChoiceReason.Configured, "ai.capture.extractor.configured");
    }

    /// <summary>
    /// يسجّل مزوّد الاستخراج: البعيد إن أمكن، والحتمي المبذور فيما عدا ذلك.
    /// <b>سطرٌ واحد يبدّل المزوّد، وهو معنى الحدّ (‏ADR-0015).</b>
    /// </summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="options">الإعدادات.</param>
    /// <param name="fallback">المزوّد الحتمي المستعمَل عند الارتداد.</param>
    /// <param name="secrets">قارئ الأسرار — يُحقن كي يُختبَر الارتداد بلا لمس البيئة.</param>
    public static IServiceCollection AddGitHubModelsInvoiceExtractor(
        this IServiceCollection services,
        GitHubModelsOptions options,
        DeterministicExtractionProvider fallback,
        ISecretReader? secrets = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(fallback);

        ISecretReader reader = secrets ?? new EnvironmentSecretReader();
        ExtractorChoice choice = Choose(options, reader);

        services.AddSingleton(choice);
        services.AddSingleton(options);
        services.AddSingleton(reader);

        if (!choice.IsRemote)
        {
            services.AddSingleton<IInvoiceExtractionProvider>(fallback);
            return services;
        }

        /* عميل HTTP واحد طويل العمر لمضيف واحد — ولا حزمة Microsoft.Extensions.Http.
           والوحدة الأفقية لا تُضيف حزمة إلا لسبب لا يُغني عنه سطران، وهذا ليس منه.
           و PooledConnectionLifetime مضبوطة صراحةً: عميلٌ ساكن بلا هذا الضبط يُثبِّت
           عنوان الخدمة إلى الأبد فلا يرى تغيّر DNS — وهي مصيدة معروفة لا اجتهاد. */
        services.AddSingleton<IModelWire>(_ => new HttpModelWire(new HttpClient(new SocketsHttpHandler
        {
            PooledConnectionLifetime = TimeSpan.FromMinutes(5),
        })));

        services.AddSingleton<IInvoiceExtractionProvider>(provider => new GitHubModelsExtractionProvider(
            options,
            provider.GetRequiredService<IModelWire>(),
            reader,
            provider.GetRequiredService<IPostingVocabulary>()));

        return services;
    }
}
