using Babel.Core.Access;
using Babel.Core.Application;
using Babel.Core.Audit;
using Babel.Core.CapabilityProfile;
using Babel.Core.CompanySetup;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
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

        return services.AddBabelCoreShared();
    }

    private static IServiceCollection AddBabelCoreShared(this IServiceCollection services)
    {
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<InMemoryUsageStore>();
        services.AddSingleton<IUsageStore>(sp => sp.GetRequiredService<InMemoryUsageStore>());
        services.AddSingleton<IUsageMeter>(sp => sp.GetRequiredService<InMemoryUsageStore>());
        services.AddSingleton<IUsageReader>(sp => sp.GetRequiredService<InMemoryUsageStore>());
        services.AddSingleton<IAuditLog, InMemoryAuditLog>();
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
        services.AddScoped<AccessService>();
        services.AddSingleton<AccessResolver>();

        // حلّ مركز التكلفة: يقرأ المخزن ولا يحمل حالة، فهو مفردة واحدة تكفي الجميع.
        // وهو ما تسأله كل بوّابة ترحيل قبل أن تبني طلباً (ADR-0026).
        services.AddSingleton<ICostCenterResolver, CostCenterResolver>();

        return services;
    }
}
