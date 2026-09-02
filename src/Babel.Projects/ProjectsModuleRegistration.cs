using Babel.Contracts.Voice;
using Babel.Projects.Application;
using Babel.Projects.Surface;
using Babel.Projects.Voice;
using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;
using Babel.Projects.NameRegister;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Projects;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class ProjectsModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعداداتها الافتراضية.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    public static IServiceCollection AddBabelProjects(this IServiceCollection services)
        => services.AddBabelProjects(static _ => { });

    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات.</param>
    public static IServiceCollection AddBabelProjects(this IServiceCollection services, Action<ProjectsOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        ProjectsOptions options = new();
        configure(options);

        services.AddSingleton(options);
        services.AddScoped<ProjectsRuntime>();
        services.AddScoped<ProjectRegistryService>();
        services.AddScoped<SubcontractorRegistryService>();
        services.AddScoped<ClientCertificateService>();
        services.AddScoped<SubcontractorCertificateService>();
        services.AddScoped<SubcontractorAdvanceService>();
        services.AddScoped<RetentionService>();
        services.AddScoped<ProjectsReconciliationService>();

        // السطح المنشور: النوع الوحيد من هذه الوحدة الذي يجوز لسطح HTTP أن يسمّيه
        // (القاعدة 13 البند ب). ونطاقُه نطاق الخدمات التي يلفّها — سياق واحد للطلب.
        services.AddScoped<ProjectsSurface>();

        // ‏**النيّات المنطوقة تُسجَّل من هنا، لا من مشروع الذكاء.**
        // الوحدة تُعلن ما تُنطَق به، ووحدةُ الذكاء تجمع ما وجدته في الحاوية عبر
        // ‏<c>IVoiceIntentCatalogue</c> في العقد. ولا تعرف إحداهما الأخرى في أي اتجاه —
        // وهو ما تفرضه القاعدة 3، وما يجعل إضافة نيّةٍ لا تمسّ مشروع الذكاء بسطر.
        services.AddSingleton<IVoiceIntentCatalogue, ProjectsVoiceIntents>();

        // ‏**سجلّات الأسماء تُسجَّل من هنا كذلك، وللسبب نفسه.** الوحدة تصف جدولها
        // وتُسجّل محوّله؛ ووحدةُ الذكاء تجمع ما وجدته عبر <c>INameCandidateSource</c>
        // في العقد، ولا تعرف اسمَ جدولٍ واحد ولا تستطيع (القاعدة 3).
        //
        // ‏**والإعلان منفصل عن المحوّل عمداً**: المحوّل يحتاج قاعدةً حيّة، والحارس
        // المعماريّ يحتاج المفاتيح وحدها — فلا يشترط قاعدةً كي يعمل، ولا يصير حدُّ
        // «كلُّ شريحةِ طرفٍ تسمّي سجلّاً يخدمه أحد» موصىً به بدل أن يكون مُنفَّذاً.
        services.AddSingleton<INameRegisterCatalogue, ProjectsNameRegisters>();

        // ‏**والمحوّلان يُبنيان عند الحلّ لا عند التسجيل.** بناؤهما هنا يجعل *تسجيل*
        // الوحدة يطلب نصَّ اتصالٍ صالحاً — فيسقط توليدُ العقد المنشور، وهو مسارٌ لا
        // قاعدة فيه أصلاً. والسقوط يقع حين **يُستعمل** السجلّ، وهو موضعه الصحيح.
        foreach (NameRegisterTable table in ProjectsNameRegisters.Tables)
        {
            NameRegisterTable described = table;

            services.AddSingleton<INameCandidateSource>(provider => new PostgresNameRegister(
                provider.GetRequiredService<ProjectsOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold));

            // ‏والجَرد كائنٌ آخر بمنفذٍ آخر — يُعيد أسماءً، ولا يُنادى في بناء رسالةٍ لنموذج.
            services.AddSingleton<INameCandidateSheetSource>(provider => new PostgresNameSheet(
                provider.GetRequiredService<ProjectsOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold,
                NameRegisterDefaults.QuestionSheetCap));
        }

        return services;
    }
}
