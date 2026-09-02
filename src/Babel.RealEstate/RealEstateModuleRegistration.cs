using Babel.Contracts.Voice;
using Babel.RealEstate.Application;
using Babel.RealEstate.Surface;
using Babel.RealEstate.Voice;
using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;
using Babel.RealEstate.NameRegister;
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

        // ‏**سجلّات الأسماء تُسجَّل من هنا كذلك، وللسبب نفسه.** الوحدة تصف جدولها
        // وتُسجّل محوّله؛ ووحدةُ الذكاء تجمع ما وجدته عبر <c>INameCandidateSource</c>
        // في العقد، ولا تعرف اسمَ جدولٍ واحد ولا تستطيع (القاعدة 3).
        //
        // ‏**والإعلان منفصل عن المحوّل عمداً**: المحوّل يحتاج قاعدةً حيّة، والحارس
        // المعماريّ يحتاج المفاتيح وحدها — فلا يشترط قاعدةً كي يعمل، ولا يصير حدُّ
        // «كلُّ شريحةِ طرفٍ تسمّي سجلّاً يخدمه أحد» موصىً به بدل أن يكون مُنفَّذاً.
        services.AddSingleton<INameRegisterCatalogue, RealEstateNameRegisters>();

        // ‏**والمحوّلان يُبنيان عند الحلّ لا عند التسجيل.** بناؤهما هنا يجعل *تسجيل*
        // الوحدة يطلب نصَّ اتصالٍ صالحاً — فيسقط توليدُ العقد المنشور، وهو مسارٌ لا
        // قاعدة فيه أصلاً. والسقوط يقع حين **يُستعمل** السجلّ، وهو موضعه الصحيح.
        foreach (NameRegisterTable table in RealEstateNameRegisters.Tables)
        {
            NameRegisterTable described = table;

            services.AddSingleton<INameCandidateSource>(provider => new PostgresNameRegister(
                provider.GetRequiredService<RealEstateOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold));

            // ‏والجَرد كائنٌ آخر بمنفذٍ آخر — يُعيد أسماءً، ولا يُنادى في بناء رسالةٍ لنموذج.
            services.AddSingleton<INameCandidateSheetSource>(provider => new PostgresNameSheet(
                provider.GetRequiredService<RealEstateOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold,
                NameRegisterDefaults.QuestionSheetCap));
        }

        return services;
    }
}
