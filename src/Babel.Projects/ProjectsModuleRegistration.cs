using Babel.Projects.Application;
using Babel.Projects.Surface;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Projects;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class ProjectsModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelProjects(this IServiceCollection services)
        => services.AddBabelProjects(static _ => { });

    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelProjects(this IServiceCollection services, Action<ProjectsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        ProjectsOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddScoped<ProjectsRuntime>();
        services.AddScoped<ProjectRegistryService>();
        services.AddScoped<SubcontractorRegistryService>();
        services.AddScoped<ClientCertificateService>();
        services.AddScoped<SubcontractorCertificateService>();
        services.AddScoped<SubcontractorAdvanceService>();
        services.AddScoped<RetentionService>();
        services.AddScoped<ProjectsReconciliationService>();

        // السطح المنشور: النوع الوحيد من هذه الوحدة الذي يجوز لسطح HTTP أن يسمّيه
        // (القاعدة 13 البند ب). ونطاقُه نطاق الخدمات التي يلفّها — سياق واحد للطلب.
        services.AddScoped<ProjectsSurface>();
        return services;
    }
}
