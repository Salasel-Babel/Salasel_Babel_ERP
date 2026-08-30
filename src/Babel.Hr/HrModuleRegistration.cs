using Babel.Hr.Application;
using Babel.Hr.Subledger;
using Babel.Hr.Surface;
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
        return services;
    }
}
