using Babel.Sales.Application;
using Babel.Sales.Surface;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Sales;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class SalesModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelSales(this IServiceCollection services)
        => services.AddBabelSales(static _ => { });

    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelSales(this IServiceCollection services, Action<SalesOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        SalesOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddScoped<SalesRuntime>();
        services.AddScoped<CustomerService>();
        services.AddScoped<SalesInvoiceService>();
        services.AddScoped<CreditNoteService>();
        services.AddScoped<CustomerReceiptService>();
        services.AddScoped<ReceivablesService>();

        // السطح المنشور: النوع الوحيد من هذه الوحدة الذي يجوز لسطح HTTP أن يسمّيه
        // (القاعدة 13 البند ب). ونطاقُه نطاق الخدمات التي يلفّها — سياق واحد للطلب.
        services.AddScoped<SalesSurface>();
        return services;
    }
}
