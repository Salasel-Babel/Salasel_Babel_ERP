using Babel.Contracts.Voice;
using Babel.Hr.Application;
using Babel.Hr.Subledger;
using Babel.Hr.Surface;
using Babel.Hr.Voice;
using Babel.Contracts.Lookup;
using Babel.Core.NameRegister;
using Babel.Hr.NameRegister;
using Microsoft.Extensions.DependencyInjection;

namespace Babel.Hr;

/// <summary>
/// نقطة تركيب الوحدة. الجذر التركيبي يستدعي هذه الدالة ولا يعرف الأنواع الداخلية للوحدة.
/// </summary>
public static class HrModuleRegistration
{
    /// <summary>يسجّل الوحدة بإعدادات صريحة.</summary>
    /// <param name="services">حاوية الخدمات.</param>
    /// <param name="configure">ضابط الإعدادات — <b>ولا نسخة بلا ضابط</b>: اتصال هذه
    /// الوحدة لا يُفترض، وغيابه يُرفض عند التركيب لا عند أول نداء.</param>
    public static IServiceCollection AddBabelHr(this IServiceCollection services, Action<HrOptions> configure)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configure);

        HrOptions options = new();
        configure(options);

        // ‏**ولا يُفحص الاتصال هنا.** التسجيل مصنعٌ كسول يُنادى في كل بناء للمضيف —
        // ومنه بناءُ المضيف الذي يولّد العقد المنشور، وهو بناءٌ لا يمسّ قاعدة بيانات
        // واحدة. ففحصٌ هنا كان سيجعل توليد العقد يتطلّب اتصالاً، فيُربط شكلُ السطح
        // بوجود قاعدة. والفحص في موضعين لا يُنسى أيّهما: مُنشئ HrRuntime — أي قبل أول
        // استعلام — ونداءٌ صريح عند إقلاع الخادم في Program.cs، بالشكل نفسه الذي
        // يُطلب به مفتاح توقيع تذاكر المرفقات هناك.
        services.AddSingleton(options);
        services.AddScoped<HrRuntime>();
        services.AddScoped<EmployeeService>();
        services.AddScoped<PayrollSettingsService>();
        services.AddScoped<PayrollRunService>();
        services.AddScoped<PayrollPaymentService>();
        services.AddScoped<SocialInsurancePaymentService>();
        services.AddScoped<EmployeeLedgerService>();
        services.AddScoped<EndOfServiceService>();
        services.AddScoped<EmployeeReconciliationService>();

        // السطح المنشور: النوع الوحيد من هذه الوحدة الذي يجوز لسطح HTTP أن يسمّيه
        // (القاعدة 13 البند ب). ونطاقُه نطاق الخدمات التي يلفّها — سياق واحد للطلب.
        services.AddScoped<HrSurface>();

        // ‏**النيّات المنطوقة تُسجَّل من هنا، لا من مشروع الذكاء.**
        // الوحدة تُعلن ما تُنطَق به، ووحدةُ الذكاء تجمع ما وجدته في الحاوية عبر
        // ‏<c>IVoiceIntentCatalogue</c> في العقد. ولا تعرف إحداهما الأخرى في أي اتجاه —
        // وهو ما تفرضه القاعدة 3، وما يجعل إضافة نيّةٍ لا تمسّ مشروع الذكاء بسطر.
        services.AddSingleton<IVoiceIntentCatalogue, HrVoiceIntents>();

        // ‏**سجلّات الأسماء تُسجَّل من هنا كذلك، وللسبب نفسه.** الوحدة تصف جدولها
        // وتُسجّل محوّله؛ ووحدةُ الذكاء تجمع ما وجدته عبر <c>INameCandidateSource</c>
        // في العقد، ولا تعرف اسمَ جدولٍ واحد ولا تستطيع (القاعدة 3).
        //
        // ‏**والإعلان منفصل عن المحوّل عمداً**: المحوّل يحتاج قاعدةً حيّة، والحارس
        // المعماريّ يحتاج المفاتيح وحدها — فلا يشترط قاعدةً كي يعمل، ولا يصير حدُّ
        // «كلُّ شريحةِ طرفٍ تسمّي سجلّاً يخدمه أحد» موصىً به بدل أن يكون مُنفَّذاً.
        services.AddSingleton<INameRegisterCatalogue, HrNameRegisters>();

        // ‏**والمحوّلان يُبنيان عند الحلّ لا عند التسجيل.** بناؤهما هنا يجعل *تسجيل*
        // الوحدة يطلب نصَّ اتصالٍ صالحاً — فيسقط توليدُ العقد المنشور، وهو مسارٌ لا
        // قاعدة فيه أصلاً. والسقوط يقع حين **يُستعمل** السجلّ، وهو موضعه الصحيح.
        foreach (NameRegisterTable table in HrNameRegisters.Tables)
        {
            NameRegisterTable described = table;

            services.AddSingleton<INameCandidateSource>(provider => new PostgresNameRegister(
                provider.GetRequiredService<HrOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold));

            // ‏والجَرد كائنٌ آخر بمنفذٍ آخر — يُعيد أسماءً، ولا يُنادى في بناء رسالةٍ لنموذج.
            services.AddSingleton<INameCandidateSheetSource>(provider => new PostgresNameSheet(
                provider.GetRequiredService<HrOptions>().ConnectionString,
                described,
                NameRegisterDefaults.SimilarityThreshold,
                NameRegisterDefaults.QuestionSheetCap));
        }

        return services;
    }
}
