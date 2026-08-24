using Babel.Contracts.Posting;
using Babel.Ledger.Posting;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Ledger;

/// <summary>
/// نقطة تركيب الدفتر. الجذر التركيبي يسجّل <see cref="IPostingService"/> ولا يرى
/// <c>LedgerDbContext</c> ولا <c>AccountCode</c> — كلاهما <c>internal</c>.
/// <para>
/// ولاحظ أي اتصال يُحقن: <b>اتصال دور التطبيق وحده</b>. اتصال المالك لا يدخل
/// حاوية الاعتماديات إطلاقاً، فلا يوجد مسار يجعل مسار الترحيل يعمل بصلاحيات
/// تسمح بـ<c>UPDATE</c> على قيد (ADR-0003).
/// </para>
/// </summary>
public static class LedgerModuleRegistration
{
    /// <summary>يسجّل الدفتر.</summary>
    public static IServiceCollection AddBabelLedger(this IServiceCollection services)
        => services.AddBabelLedger(static _ => { });

    /// <summary>يسجّل الدفتر بإعدادات صريحة.</summary>
    public static IServiceCollection AddBabelLedger(this IServiceCollection services, Action<LedgerOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        LedgerOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddSingleton(provider => new LedgerRuntime(provider.GetRequiredService<LedgerOptions>()));
        services.AddScoped<IPostingService, PostingService>();
        services.AddScoped<Audit.LedgerAuditService>();
        return services;
    }
}
