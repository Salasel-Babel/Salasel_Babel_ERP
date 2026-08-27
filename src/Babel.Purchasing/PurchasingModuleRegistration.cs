using Babel.Contracts.Capture;
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

        // ── منفذ الترقية: الوحدة **المالكة للمستند** تسجّل تنفيذها له ────────────
        // والمنفذ يعيش في Babel.Contracts، فلا تكتسب أي وحدة بتسجيله معرفةً بجارتها:
        // وحدة الالتقاط ترى الواجهة وحدها، والحاوية توصلها بهذا التنفيذ. وهو الطرف
        // الذي كان مفقوداً — منفذٌ معلن لا ينفّذه أحد.
        services.AddScoped<PurchasingCapturedInvoiceReceiver>();
        services.AddScoped<ICapturedInvoiceReceiver>(
            static provider => provider.GetRequiredService<PurchasingCapturedInvoiceReceiver>());

        return services;
    }
}
