using Babel.Purchasing.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Purchasing;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class PurchasingModuleRegistration
{
    /// <summary>يسجّل الوحدة.</summary>
    public static IServiceCollection AddBabelPurchasing(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<PurchaseBillService>();
        return services;
    }
}
