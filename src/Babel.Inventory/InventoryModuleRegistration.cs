using Babel.Contracts.Inventory;
using Babel.Contracts.Voice;
using Babel.Inventory.Application;
using Babel.Inventory.Surface;
using Babel.Inventory.Voice;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Inventory;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class InventoryModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelInventory(this IServiceCollection services)
        => services.AddBabelInventory(static _ => { });

    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelInventory(this IServiceCollection services, Action<InventoryOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        InventoryOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddScoped<InventoryRuntime>();
        services.AddScoped<StockMovementService>();
        services.AddScoped<InventoryValuationService>();
        services.AddScoped<UnitOfMeasureService>();
        services.AddScoped<ItemCatalogueService>();
        services.AddScoped<StockDocumentService>();
        services.AddScoped<StoragePlaceService>();
        services.AddScoped<StockTransferService>();

        // السطح المنشور: النوع الوحيد من هذه الوحدة الذي يجوز لسطح HTTP أن يسمّيه
        // (القاعدة 13 البند ب). ونطاقُه نطاق الخدمات التي يلفّها — سياق واحد للطلب.
        services.AddScoped<InventorySurface>();

        // ── منفذ التقييم: الوحدة **المالكة للمخزون** تسجّل تنفيذها له ─────────────
        // والمنفذ يعيش في Babel.Contracts، فلا تكتسب وحدة المبيعات بتسجيله معرفةً
        // بجارتها: ترى الواجهة وحدها، والحاوية توصلها بهذا التنفيذ. وهو الشكل نفسه
        // المعتمد في ICapturedInvoiceReceiver.
        services.AddScoped<IInventoryValuation>(
            static provider => provider.GetRequiredService<StockMovementService>());

        // ‏**النيّات المنطوقة تُسجَّل من هنا، لا من مشروع الذكاء.**
        // الوحدة تُعلن ما تُنطَق به، ووحدةُ الذكاء تجمع ما وجدته في الحاوية عبر
        // ‏<c>IVoiceIntentCatalogue</c> في العقد. ولا تعرف إحداهما الأخرى في أي اتجاه —
        // وهو ما تفرضه القاعدة 3، وما يجعل إضافة نيّةٍ لا تمسّ مشروع الذكاء بسطر.
        services.AddSingleton<IVoiceIntentCatalogue, InventoryVoiceIntents>();

        return services;
    }
}
