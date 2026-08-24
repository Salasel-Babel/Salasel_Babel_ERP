using Babel.Compliance.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Compliance;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class ComplianceModuleRegistration
{
    /// <summary>يسجّل الوحدة.</summary>
    public static IServiceCollection AddBabelCompliance(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<EInvoiceSubmissionService>();
        return services;
    }
}
