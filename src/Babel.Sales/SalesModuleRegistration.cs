using Babel.Sales.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Sales;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class SalesModuleRegistration
{
    /// <summary>يسجّل الوحدة.</summary>
    public static IServiceCollection AddBabelSales(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<SalesInvoiceService>();
        return services;
    }
}
