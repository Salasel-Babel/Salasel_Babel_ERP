using Babel.Core.Access;
using Babel.Core.Application;
using Babel.Core.Audit;
using Babel.Core.CapabilityProfile;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Babel.Core.Parameters;
using Babel.Contracts.Parameters;
using Babel.Core.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Core;

/// <summary>
/// نقطة تركيب النواة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف شيئاً عن أنواعها الداخلية.
/// </summary>
public static class CoreModuleRegistration
{
    /// <summary>
    /// يسجّل النواة <b>بمخزنَي حالةٍ في الذاكرة</b> — عمرهما عمر العملية.
    /// <para>
    /// <b>وهذا التحميل الزائد ليس تنفيذ الخادم:</b> الجذر التركيبي يستدعي
    /// <see cref="AddBabelCore(IServiceCollection, Action{CoreOptions})"/> ويحصل على
    /// مخزنين فوق PostgreSQL. وما هنا لمن لا قاعدة بيانات له — اختبارات الوحدة —
    /// ولا يُستعمل في مسار تشغيل. ويحرس ذلك اختبارٌ يبني الجذر التركيبي ويسأل الحاوية
    /// عن نوع المخزن الفعلي، فلا يعود «الخادم يستعمل الذاكرة» شيئاً يُكتشَف في عرض.
    /// </para>
    /// </summary>
    /// <param name="services">مجموعة الخدمات.</param>
    public static IServiceCollection AddBabelCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ICapabilityProfileStore, InMemoryCapabilityProfileStore>();
        services.AddSingleton<ICompanySetupStore, InMemoryCompanySetupStore>();
        services.AddSingleton<IAccessDirectory, InMemoryAccessDirectory>();

        // الأثر والقياس في الذاكرة **هنا وحدها** — في التحميل الزائد الذي لا قاعدة له.
        // وكانا في `AddBabelCoreShared`، أي في مسار PostgreSQL نفسه: فكانت كل نشرة تمحو
        // «من فعل ماذا ومتى» على الخادم الحقيقي، ويُتجاوَز سقفُ الإنفاق بإعادة تشغيل.
        services.AddSingleton<InMemoryUsageStore>();
        services.AddSingleton<IUsageStore>(provider => provider.GetRequiredService<InMemoryUsageStore>());
        services.AddSingleton<IUsageMeter>(provider => provider.GetRequiredService<InMemoryUsageStore>());
        services.AddSingleton<IUsageReader>(provider => provider.GetRequiredService<InMemoryUsageStore>());
        services.AddSingleton<IAuditLog, InMemoryAuditLog>();

        // ومخزن المعامِلات كذلك: افتراضات المنصّة تُقرأ من الملفّ المضمَّن نفسه، فلا
        // يختلف جوابُ «ما النسبة السارية؟» بين اختبار وحدةٍ وخادمٍ إلّا في الاستمرارية.
        services.AddSingleton<IParameterStore>(provider =>
            new InMemoryParameterStore(provider.GetRequiredService<TimeProvider>()));

        return services.AddBabelCoreShared();
    }

    /// <summary>
    /// يسجّل النواة <b>بمخزنَين فوق PostgreSQL</b> — وهو ما يجعل خادماً أُعيد إقلاعه
    /// يعرف منشآته ومقاييس عرضها ومراكز تكلفتها.
    /// <para>
    /// وهذه الدالّة <b>لا تنشر مخطّطاً ولا تحمل اتصال المالك إلى الحاوية</b>: النشر
    /// بدور المالك في <see cref="CoreSchema.DeployAsync"/> ويقع في خطوة الترحيل وحدها،
    /// وما يدخل هنا هو اتصال دور التطبيق فقط (ADR-0003).
    /// </para>
    /// </summary>
    /// <param name="services">مجموعة الخدمات.</param>
    /// <param name="configure">إعدادات النواة.</param>
    public static IServiceCollection AddBabelCore(this IServiceCollection services, Action<CoreOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        CoreOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton<ICapabilityProfileStore>(provider => new PostgresCapabilityProfileStore(
            provider.GetRequiredService<CoreOptions>(),
            provider.GetRequiredService<IPostingEventDirectory>()));
        services.AddSingleton<ICompanySetupStore>(provider => new PostgresCompanySetupStore(
            provider.GetRequiredService<CoreOptions>(),
            provider.GetRequiredService<TimeProvider>()));

        // دليل المصادقة فوق PostgreSQL: جلسةٌ في ذاكرة العملية تعني أن كل مستخدم يخرج
        // عند كل نشر، وأن «أُبطلت جلسته» جملةٌ صحيحة على خادمٍ واحد من ثلاثة.
        services.AddSingleton<IAccessDirectory>(provider => new PostgresAccessDirectory(
            provider.GetRequiredService<CoreOptions>()));

        // سجلّ التدقيق فوق PostgreSQL: سجلٌّ في ذاكرة العملية يعني أن **كل نشرة تمحو
        // أثر من فعل ماذا ومتى** — في نظامٍ محاسبي — وأن خادمين خلف موزّع يريان سجلّين
        // مختلفين. وهو العطل الذي أُصلح حين انتقل مخزن التأسيس من الذاكرة إلى القاعدة.
        services.AddSingleton<IAuditLog>(provider => new PostgresAuditLog(
            provider.GetRequiredService<CoreOptions>()));

        // ومخزن الاستخدام كذلك: عدّادٌ في الذاكرة يُصفَّر عند كل إقلاع، فيُتجاوَز سقفُ
        // الإنفاق بإعادة تشغيل، ويعدّ خادمان نصفين لا يجمعهما أحد. والمثيل **واحد**
        // تُشير إليه الواجهات الثلاث، كما في نظيره في الذاكرة تماماً.
        services.AddSingleton(provider => new PostgresUsageStore(provider.GetRequiredService<CoreOptions>()));
        services.AddSingleton<IUsageStore>(provider => provider.GetRequiredService<PostgresUsageStore>());
        services.AddSingleton<IUsageMeter>(provider => provider.GetRequiredService<PostgresUsageStore>());
        services.AddSingleton<IUsageReader>(provider => provider.GetRequiredService<PostgresUsageStore>());

        // ومخزن المعامِلات فوق PostgreSQL: نسبةٌ في ذاكرة العملية تعني أن كل نشرة تُرجع
        // كل منشأةٍ إلى افتراض المنصّة، وأن مستنداً رُحِّل بإصدارٍ لا يجد سجلَّ استعماله
        // بعد ساعة. وهي علّةُ سجلّ التدقيق نفسها لا علّةٌ ثانية.
        services.AddSingleton<IParameterStore>(provider => new PostgresParameterStore(
            provider.GetRequiredService<CoreOptions>(),
            provider.GetRequiredService<TimeProvider>()));

        return services.AddBabelCoreShared();
    }

    /// <summary>
    /// ما يشترك فيه التحميلان — <b>وما لا يشترك فيه مكتوبٌ هنا لأنه ما كان معطوباً</b>.
    /// <para>
    /// كان سجلّ التدقيق ومخزن الاستخدام مسجَّلين هنا، وهذه الدالّة تُنادى من مسار
    /// PostgreSQL نفسه — فكان الخادم الحقيقي يحمل سجلَّ تدقيقٍ وعدّادَ استخدامٍ في
    /// ذاكرة العملية. وأثرُ ذلك بالترتيب: كلُّ نشرة تمحو الأثر، وسقفُ الإنفاق يُتجاوَز
    /// بإعادة تشغيل، وخادمان خلف موزّع يريان سجلَّين. فانتقل الاثنان إلى التحميلين
    /// كلٍّ بنظيره، وبقي هنا ما لا حالة له أو ما حالتُه عمرُ العملية بحقّ.
    /// </para>
    /// <para>
    /// و<see cref="IEntitlementService"/> ما زال في الذاكرة: هو نقصُ استمراريةٍ
    /// <b>مُعلَن</b> لا مُغفَل، ونقلُه يقتضي جدولاً وقراراً في التسعير لا يُحسمان في
    /// إيداع الأثر. وما يهمّ هنا أن تغييراته <b>تُدوَّن</b> — وهي تُدوَّن، في سجلّ
    /// تدقيقٍ صار دائماً.
    /// </para>
    /// </summary>
    /// <param name="services">مجموعة الخدمات.</param>
    private static IServiceCollection AddBabelCoreShared(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IEntitlementService, InMemoryEntitlementService>();
        services.AddSingleton<IEntitlementEnforcer, EntitlementEnforcer>();
        services.AddScoped<EntitlementAdministrationService>();

        // ملفّ القدرات: الفهرس يُقرأ مرّة لكل عملية، والمخزن حالة المستأجر، والخدمة نطاق طلب.
        services.AddSingleton<IPostingEventDirectory>(_ => EmbeddedPostingEventDirectory.Default);
        services.AddScoped<CapabilityProfileService>();

        // تأسيس المنشأة: المخزن حالة المستأجر، والخدمة نطاق طلب — كملفّ القدرات تماماً.
        services.AddScoped<CompanySetupService>();

        // المصادقة: الخدمة نطاق طلب كسائر خدمات التطبيق، والحالّ مفردة لأنه يقرأ ولا يحمل
        // حالة — وهو يُنادى **قبل** المصادقة في كل طلب، فلا يجوز أن يعتمد على نطاقها.
        // ‏**سياسةُ مُدَد الاعتمادات مفردة تُقرأ من البيئة** — لا ثوابتُ شيفرة. وهي
        // أوّل ما يُشدَّد لحظةَ حادثة، والسقفُ يُفحص عند الإقلاع لا عند أوّل جلسة.
        services.AddSingleton<AccessPolicy>();
        services.AddScoped<AccessService>();
        services.AddSingleton<AccessResolver>();

        // المعامِلات: لوحةُ التحكّم خدمةُ تطبيقٍ بنطاق طلب، والدليلُ مفردةٌ تقرأ ولا
        // تحمل حالة — وهو يُنادى **داخل** عمليةٍ فحصَت استحقاقها، كحالّ مركز التكلفة
        // تماماً. والمنفذان في العقود يُشيران إلى المثيل نفسه: مصدرُ القيمة ومسجّلُ
        // الاستعمال شيءٌ واحد، وفصلُهما مثيلين يجعل اختباراً يُبدّل أحدهما ويقرأ الآخر.
        services.AddScoped<ParameterSettingsService>();
        services.AddSingleton<ParameterDirectory>();
        services.AddSingleton<IParameterSource>(provider => provider.GetRequiredService<ParameterDirectory>());
        services.AddSingleton<IParameterUsageRecorder>(provider => provider.GetRequiredService<ParameterDirectory>());

        // حلّ مركز التكلفة: يقرأ المخزن ولا يحمل حالة، فهو مفردة واحدة تكفي الجميع.
        // وهو ما تسأله كل بوّابة ترحيل قبل أن تبني طلباً (ADR-0026).
        services.AddSingleton<ICostCenterResolver, CostCenterResolver>();

        return services;
    }
}
