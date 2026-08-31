using Babel.Ai.Capture;
using Babel.Ai.Extraction;
using Babel.Ai.Suggestions;
using Babel.Ai.Voice;
using Babel.Contracts.Voice;
using Babel.SharedKernel;
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

        // ‏**سجلّ النيّات المنطوقة — يُجمَع ولا يُكتب هنا.**
        // ما يصل هذه الدالة هو ما سجّلته الوحدات بنفسها من <c>IVoiceIntentCatalogue</c>،
        // وهو نوعٌ في العقد لا في هذا المشروع. فإضافة نيّةٍ للمخزون أو للعقارات
        // **لا تفتح هذا الملف**، ولا يظهر اسم وحدةٍ واحدة في هذا المشروع (القاعدة 3).
        //
        // ‏**والبناء يُسقط التركيب إن كان السجلّ معتلّاً** — رمزَ حدثٍ ليس في المصفوفة،
        // أو معرّفاً مكرّراً، أو ترحيلاً بلا حدث. وسجلٌّ نصفُه صالح يعمل تسعاً وتسعين
        // مرّة ثم يُرحّل مرّةً إلى حدثٍ لا وجود له، وذلك أسوأ من أن يرفض أن يُبنى.
        services.AddSingleton(static provider =>
        {
            Result<VoiceIntentRegistry> registry = VoiceIntentRegistry.Build(
                provider.GetServices<IVoiceIntentCatalogue>(),
                provider.GetRequiredService<IPostingVocabulary>());

            return registry.IsSuccess
                ? registry.Value
                : throw new InvalidOperationException(
                    "سجلّ النيّات المنطوقة معتلّ فلا يُركَّب: "
                    + string.Join(" · ", registry.Errors.Select(static error => error.MessageAr)));
        });

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
