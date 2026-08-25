using Babel.Core.Application;
using Babel.Core.Audit;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Core.Metering;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Core;

/// <summary>
/// نقطة تركيب النواة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف شيئاً عن أنواعها الداخلية.
/// </summary>
public static class CoreModuleRegistration
{
    /// <summary>يسجّل النواة بتنفيذاتها في الذاكرة (موجة الهيكل).</summary>
    public static IServiceCollection AddBabelCore(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

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
        services.AddSingleton<ICapabilityProfileStore, InMemoryCapabilityProfileStore>();
        services.AddScoped<CapabilityProfileService>();

        return services;
    }
}
