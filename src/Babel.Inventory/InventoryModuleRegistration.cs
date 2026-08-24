using Babel.Inventory.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Inventory;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class InventoryModuleRegistration
{
    /// <summary>يسجّل الوحدة.</summary>
    public static IServiceCollection AddBabelInventory(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddScoped<StockMovementService>();
        return services;
    }
}
