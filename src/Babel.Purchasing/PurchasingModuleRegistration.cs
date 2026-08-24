using Babel.Purchasing.Application;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Purchasing;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class PurchasingModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelPurchasing(this IServiceCollection services)
        => services.AddBabelPurchasing(static _ => { });

    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelPurchasing(this IServiceCollection services, Action<PurchasingOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        PurchasingOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddScoped<PurchasingRuntime>();
        services.AddScoped<SupplierService>();
        services.AddScoped<PurchaseOrderService>();
        services.AddScoped<GoodsReceiptService>();
        services.AddScoped<SupplierBillService>();
        services.AddScoped<SupplierPaymentService>();
        services.AddScoped<PayablesService>();
        return services;
    }
}
