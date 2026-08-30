using Babel.Contracts.Posting;
using Babel.Contracts.Subledger;
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

        // ── منفذ نقطة الضبط: تنفيذٌ في الخادم، لا في تجهيزات الاختبار وحدها ──────
        // كان `IControlPointReader` معلَناً في العقود بلا تنفيذ واحد في `src/`، فكانت
        // ‏`ReceivablesService` و`PayablesService` و`InventoryValuationService` —
        // ومعها `SalesInvoiceService` عبر `IInventoryValuation` — **غير قابلة للبناء
        // في الخادم**. ولم يظهر ذلك لأن لا باب HTTP كان يبلغها: الحاوية لا تتحقّق من
        // رسم بياني لا يطلبه أحد.
        services.AddScoped<IControlPointReader, Subledger.ControlPointReader>();

        // ── منفذ تسجيل بُعد العقار: هنا لا في الجذر التركيبي ────────────────────
        // الدرس الحرفي من `IControlPointReader` أعلاه: منفذٌ في العقد بلا تنفيذ
        // مسجَّل في `src/` **عطلٌ صامت تحت اختبارات خضراء** — الحاوية لا تتحقّق من
        // رسم بياني لا يطلبه أحد، فلا يظهر النقص إلا يوم يُفتح له باب. والوحدة
        // المالكة للجدول هي التي تسجّل تنفيذه، فلا تكتسب الوحدة العقارية بتسجيله
        // معرفةً بالدفتر: ترى الواجهة وحدها، والحاوية توصلها بهذا التنفيذ.
        services.AddScoped<Babel.Contracts.RealEstate.IPropertyDimensionRegistrar,
                           RealEstate.PropertyDimensionRegistrar>();
        return services;
    }
}
