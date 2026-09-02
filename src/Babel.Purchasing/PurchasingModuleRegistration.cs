using Babel.Contracts.Capture;
using Babel.Contracts.Voice;
using Babel.Purchasing.Application;
using Babel.Purchasing.Surface;
using Babel.Purchasing.Voice;
using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;
using Babel.Purchasing.NameRegister;
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

        // السطح المنشور: النوع الوحيد من هذه الوحدة الذي يجوز لسطح HTTP أن يسمّيه
        // (القاعدة 13 البند ب).
        services.AddScoped<PurchasingSurface>();

        // ── منفذ الترقية: الوحدة **المالكة للمستند** تسجّل تنفيذها له ────────────
        // والمنفذ يعيش في Babel.Contracts، فلا تكتسب أي وحدة بتسجيله معرفةً بجارتها:
        // وحدة الالتقاط ترى الواجهة وحدها، والحاوية توصلها بهذا التنفيذ. وهو الطرف
        // الذي كان مفقوداً — منفذٌ معلن لا ينفّذه أحد.
        services.AddScoped<PurchasingCapturedInvoiceReceiver>();
        services.AddScoped<ICapturedInvoiceReceiver>(
            static provider => provider.GetRequiredService<PurchasingCapturedInvoiceReceiver>());

        // ‏**النيّات المنطوقة تُسجَّل من هنا، لا من مشروع الذكاء.**
        // الوحدة تُعلن ما تُنطَق به، ووحدةُ الذكاء تجمع ما وجدته في الحاوية عبر
        // ‏<c>IVoiceIntentCatalogue</c> في العقد. ولا تعرف إحداهما الأخرى في أي اتجاه —
        // وهو ما تفرضه القاعدة 3، وما يجعل إضافة نيّةٍ لا تمسّ مشروع الذكاء بسطر.
        services.AddSingleton<IVoiceIntentCatalogue, PurchasingVoiceIntents>();

        // ‏**سجلّات الأسماء تُسجَّل من هنا كذلك، وللسبب نفسه.** الوحدة تصف جدولها
        // وتُسجّل محوّله؛ ووحدةُ الذكاء تجمع ما وجدته عبر <c>INameCandidateSource</c>
        // في العقد، ولا تعرف اسمَ جدولٍ واحد ولا تستطيع (القاعدة 3).
        //
        // ‏**والإعلان منفصل عن المحوّل عمداً**: المحوّل يحتاج قاعدةً حيّة، والحارس
        // المعماريّ يحتاج المفاتيح وحدها — فلا يشترط قاعدةً كي يعمل، ولا يصير حدُّ
        // «كلُّ شريحةِ طرفٍ تسمّي سجلّاً يخدمه أحد» موصىً به بدل أن يكون مُنفَّذاً.
        services.AddSingleton<INameRegisterCatalogue, PurchasingNameRegisters>();

        // ‏**والمحوّلان يُبنيان عند الحلّ لا عند التسجيل.** بناؤهما هنا يجعل *تسجيل*
        // الوحدة يطلب نصَّ اتصالٍ صالحاً — فيسقط توليدُ العقد المنشور، وهو مسارٌ لا
        // قاعدة فيه أصلاً. والسقوط يقع حين **يُستعمل** السجلّ، وهو موضعه الصحيح.
        foreach (NameRegisterTable table in PurchasingNameRegisters.Tables)
        {
            NameRegisterTable described = table;

            services.AddSingleton<INameCandidateSource>(provider => new PostgresNameRegister(
                provider.GetRequiredService<PurchasingOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold));

            // ‏والجَرد كائنٌ آخر بمنفذٍ آخر — يُعيد أسماءً، ولا يُنادى في بناء رسالةٍ لنموذج.
            services.AddSingleton<INameCandidateSheetSource>(provider => new PostgresNameSheet(
                provider.GetRequiredService<PurchasingOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold,
                NameRegisterDefaults.QuestionSheetCap));
        }

        return services;
    }
}
