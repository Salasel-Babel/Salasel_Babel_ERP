using Babel.Ai.Capture;
using Babel.Ai.Extraction;
using Babel.Ai.Suggestions;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Ai;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعيها ولا يعرف أنواعها الداخلية.
/// <para>
/// <b>ولاحظ ما لا تسجّله:</b> لا <c>IAttestedQrReader</c>، ولا
/// <c>Babel.Contracts.Storage.IAttachmentStore</c>، ولا
/// <c>Babel.Contracts.Capture.ICapturedInvoiceReceiver</c>. الأول وصلةٌ إلى مزوّد الالتزام
/// يركّبها الجذر التركيبي؛ والثاني <b>تسجّله الوحدة المالكة للمستند</b> عند تسجيل نفسها
/// (<c>AddBabelPurchasing</c>). وفي الحالتين: وحدةٌ تسجّل وصلاتها بنفسها تكون قد عرفت
/// جيرانها، وهو ما تمنعه القاعدة 3.
/// </para>
/// <para>
/// <b>ومخزن المرفقات منفذٌ في <c>Babel.Contracts</c> ومحوّله في <c>Babel.Storage</c>.</b>
/// الجذر التركيبي يركّبه بـ<c>AddBabelStorage()</c>، ووحدة الالتقاط لا تعرف ذلك المشروع
/// ولا تستطيع (القاعدة 3). فمن يركّب هذه الوحدة بلا محوّل مرفقات يسقط عند حلّ
/// <c>InvoiceCaptureService</c> — <b>وذلك مقصود</b>: التقاطٌ بلا مكان يُحفظ فيه المستند
/// هو التقاطٌ يقرأ صورةً ثم يفقدها.
/// </para>
/// <para>
/// وتسجيل المستقبِل من جهة المالك <b>لا يكسر الحدّ</b> لأن المنفذ يعيش في
/// <c>Babel.Contracts</c>: المالك يعرف العقد ولا يعرف وحدة الالتقاط، ووحدة الالتقاط تعرف
/// العقد ولا تعرف مالك المستند. ولا يعرف أحدهما الآخر في أي اتجاه.
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
