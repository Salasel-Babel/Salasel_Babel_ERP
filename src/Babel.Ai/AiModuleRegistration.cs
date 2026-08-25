using Babel.Ai.Capture;
using Babel.Ai.Extraction;
using Babel.Ai.Suggestions;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Ai;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعيها ولا يعرف أنواعها الداخلية.
/// <para>
/// <b>ولاحظ ما لا تسجّله:</b> لا <c>IAttestedQrReader</c> ولا <c>ICapturedInvoiceReceiver</c>.
/// الأول وصلةٌ إلى مزوّد الالتزام، والثاني وصلةٌ إلى الوحدة المالكة للمستند — وكلاهما
/// <b>يعبر حدّ وحدة</b>، فموضعه الجذر التركيبي وحده. وحدةٌ تسجّل وصلاتها بنفسها تكون قد
/// عرفت جيرانها، وهو ما تمنعه القاعدة 3.
/// </para>
/// </summary>
public static class AiModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelAi(this IServiceCollection services)
        => services.AddBabelAi(static _ => { });

    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelAi(this IServiceCollection services, Action<AiOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        AiOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IPostingVocabulary>(MatrixPostingVocabulary.Default);
        services.AddSingleton<ICapturedDraftStore, InMemoryCapturedDraftStore>();
        services.AddScoped<InvoiceCaptureService>();
        return services;
    }

    /// <summary>
    /// يسجّل مزوّد الاستخراج الحتمي — للتشغيل بلا شبكة وللعرض التوضيحي.
    /// يُستبدل بمزوّد حقيقي بسطر واحد، وهو معنى الحدّ (‏ADR-0015).
    /// </summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="provider">المزوّد المبذور.</param>
    public static IServiceCollection AddDeterministicInvoiceExtractor(
        this IServiceCollection services,
        DeterministicExtractionProvider provider)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(provider);
        services.AddSingleton<IInvoiceExtractionProvider>(provider);
        return services;
    }
}
