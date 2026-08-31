using Babel.Contracts.Storage;
using Babel.Storage.Surface;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Storage;

/// <summary>
/// نقطة تركيب المخزن. <b>الجذر التركيبي وحده يستدعيها</b> — لا وحدة أفقية تعرف هذا
/// المشروع، وكلها تعرف <see cref="IAttachmentStore"/> في العقد.
/// <para>
/// <b>والتسجيل صريح <c>AddSingleton</c> لا <c>TryAdd</c></b>، وهو نفس الدرس المدفوع
/// ثمنه في ‏ADR-0042: ‏<c>TryAdd</c> فوق <c>TryAdd</c> يجعل سطر تركيب صحيح الظاهر
/// لا-عملية صامتة، فيحلّ نائبٌ محلّ المحوّل الحقيقي ولا يقول أحد شيئاً.
/// </para>
/// </summary>
public static class StorageRegistration
{
    /// <summary>يركّب المخزن بإعداداته الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelStorage(this IServiceCollection services)
        => services.AddBabelStorage(static _ => { });

    /// <summary>يركّب المخزن بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelStorage(this IServiceCollection services, Action<StorageOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        StorageOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IAttachmentStore>(provider =>
            new FileSystemAttachmentStore(options, provider.GetRequiredService<TimeProvider>()));

        return services;
    }

    /// <summary>
    /// يركّب <b>السطح المنشور للمرفقات</b> — وهو ما يناديه سطح HTTP، ولا شيء غيره.
    /// <para>
    /// <b>ويحتاج المخزن والتذاكر معاً</b>، فمن يركّبه يكون قد ضبط مفتاح التوقيع: سطحُ
    /// تنزيلٍ بلا مفتاح ليس سطحاً منقوصاً بل سطحٌ يردّ عطلاً عند أول طلب.
    /// </para>
    /// </summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelAttachmentSurface(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton(provider => new AttachmentSurface(
            provider.GetRequiredService<IAttachmentStore>(),
            provider.GetRequiredService<IAttachmentTickets>(),
            provider.GetRequiredService<StorageOptions>()));

        return services;
    }

    /// <summary>
    /// يركّب مُصدِر التذاكر. <b>مفصول عن تركيب المخزن عمداً</b>: تركيبٌ بلا مفتاح
    /// توقيع يرمي عند البناء، ونشرٌ لا يقدّم تنزيلاً موقّعاً لا يحتاج المفتاح أصلاً.
    /// فمن يريد التنزيل يطلبه بسطر، ويدفع ثمنه — وهو أن يضبط المفتاح.
    /// </summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelStorageTickets(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IAttachmentTickets>(provider => new SignedAttachmentTickets(
            provider.GetRequiredService<StorageOptions>(),
            provider.GetRequiredService<TimeProvider>()));

        return services;
    }
}
