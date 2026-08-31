using Babel.Contracts.Voice;
using Babel.RealEstate.Application;
using Babel.RealEstate.Surface;
using Babel.RealEstate.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.RealEstate;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class RealEstateModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelRealEstate(this IServiceCollection services)
        => services.AddBabelRealEstate(static _ => { });

    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelRealEstate(this IServiceCollection services, Action<RealEstateOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        RealEstateOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddScoped<RealEstateRuntime>();
        services.AddScoped<PropertyService>();
        services.AddScoped<PartyService>();
        services.AddScoped<LeaseContractService>();
        services.AddScoped<RentInvoiceService>();
        services.AddScoped<TenantReceiptService>();
        services.AddScoped<TenantArrearsService>();

        // السطح المنشور: النوع الوحيد من هذه الوحدة الذي يجوز لسطح HTTP أن يسمّيه
        // (القاعدة 13 البند ب). ونطاقُه نطاق الخدمات التي يلفّها — سياق واحد للطلب.
        services.AddScoped<RealEstateSurface>();

        // ‏**النيّات المنطوقة تُسجَّل من هنا، لا من مشروع الذكاء.**
        // الوحدة تُعلن ما تُنطَق به، ووحدةُ الذكاء تجمع ما وجدته في الحاوية عبر
        // ‏<c>IVoiceIntentCatalogue</c> في العقد. ولا تعرف إحداهما الأخرى في أي اتجاه —
        // وهو ما تفرضه القاعدة 3، وما يجعل إضافة نيّةٍ لا تمسّ مشروع الذكاء بسطر.
        services.AddSingleton<IVoiceIntentCatalogue, RealEstateVoiceIntents>();

        return services;
    }
}
