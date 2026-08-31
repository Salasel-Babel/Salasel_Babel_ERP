using Babel.Compliance.Abstractions;
using Babel.Compliance.Application;
using Babel.Compliance.Canonical;
using Babel.Contracts.Compliance;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Reconciliation;
using Babel.Compliance.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Babel.Compliance;

/// <summary>
/// التركيب. <b>هنا وهنا وحده يُعرف شكل حيازة المفتاح</b>: يُحقن مزوّد واحد،
/// ومعه خاتمه المتوافق. المُنسِّق نفسه لا يتفرّع على الشكل أبداً.
/// <para/>
/// <b>و<see cref="AddBabelCompliance"/> هنا هي نقطة تركيب الوحدة الوحيدة.</b> كانت
/// إلى جانبها دالة امتداد ثانية بالاسم نفسه وفي فضاء الاسم نفسه، لا تسجّل إلا خدمة
/// التطبيق؛ وبقواعد اختيار الحِمل الزائد في C# كان النداء بلا وسائط —
/// <c>services.AddBabelCompliance()</c> في الجذر التركيبي — يذهب إليها هي، فيُركَّب
/// المستودع كله بلا مسار التزام واحد بينما يبدو أن الوحدة مركَّبة
/// (‏<c>docs/evidence/traps.md#fakh-two-registrations-one-name</c>).
/// </summary>
public static class ComplianceModuleRegistration
{
    /// <summary>
    /// نقطة تركيب الوحدة — <b>بلا معاملات اختيارية عمداً</b>. الساعة والإعدادات تُركَّبان
    /// بـ<c>TryAdd</c>، فمن أراد غير الافتراضي سجّله <b>قبل</b> هذا النداء. ومعاملٌ اختياري
    /// هنا كان يجعل الجذر التركيبي يسمّي نوعاً داخلياً للوحدة في موضع النداء (القاعدة 13).
    /// </summary>
    public static IServiceCollection AddBabelCompliance(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddSingleton(new ComplianceSettings());

        services.TryAddSingleton<IXmlCanonicaliser, DeterministicXmlSerialiser>();
        services.TryAddSingleton<IDocumentRenderer>(sp =>
            new ProvisionalDocumentRenderer(sp.GetRequiredService<IXmlCanonicaliser>()));

        services.TryAddSingleton<IIssuingUnitRegistry, InMemoryIssuingUnitRegistry>();

        services.TryAddScoped<ComplianceDocumentFactory>();
        services.TryAddScoped<ClearanceCoordinator>();
        services.TryAddScoped<ReportingWorker>();
        services.TryAddScoped<ComplianceService>();
        services.TryAddScoped<Reconciler>();

        // نقطة الدخول العامة للوحدة، وهي أيضاً تنفيذ المنفذ الذي تستدعيه وحدة المصدر.
        // النوع نفسه خلف الاثنين عمداً: مسار إرسال واحد لا اثنان.
        services.TryAddScoped<EInvoiceSubmissionService>();
        services.TryAddScoped<IElectronicDocumentIntake>(
            static sp => sp.GetRequiredService<EInvoiceSubmissionService>());

        // ما لا تملكه الوحدة ويجب أن يأتي من التركيب: المزوّد، والمخزن، وسياسة المسار.
        // غيابها ليس عطلاً صامتاً ولا رسالة حقن اعتمادية غامضة، بل رفضٌ يسمّي نفسه
        // بالعربية والإنجليزية ويسمّي النداء الذي كان يجب أن يقع.
        //
        // ونوّاب يرمون — لا نوّاب صامتون: نائبٌ يعمل بلا ضجيج يجعل مستودعاً بلا التزام
        // يبدو مركَّباً، وهو الفخّ الذي فُتح هذا الفرع لإغلاقه. ولذلك أيضاً **كل** دالّة
        // تركيب صريحة أدناه تستعمل AddSingleton لا TryAdd: النائب مسجَّل سلفاً، فـTryAdd
        // كان يجعل النداء الصريح لا-عملية صامتة.
        services.TryAddSingleton<IComplianceProvider>(static _ => throw NotComposed(
            "مزوّد الالتزام", "the compliance provider", "AddComplianceProvider<T>()"));
        services.TryAddSingleton<IComplianceStore>(static _ => throw NotComposed(
            "مخزن الالتزام", "the compliance store", "AddInMemoryComplianceStore() أو مخزن علائقي"));
        services.TryAddSingleton<IFlowPolicy>(static _ => throw NotComposed(
            "سياسة اختيار المسار", "the flow policy", "AddComplianceFlowPolicy<T>()"));

        return services;
    }

    /// <summary>
    /// يركّب سياسة اختيار المسار. <b>لا افتراضي هنا عمداً</b>: معيار «مبسّطة أم قياسية»
    /// خاصية مزوّد ومواصفة، وكتابة نسخة افتراضية منه في هذا المشروع تُنشئ مصدر حقيقة
    /// ثانياً للمسار — وهو بالضبط ما تمنعه بنية المسارين.
    /// </summary>
    public static IServiceCollection AddComplianceFlowPolicy<TPolicy>(this IServiceCollection services)
        where TPolicy : class, IFlowPolicy
    {
        services.AddSingleton<IFlowPolicy, TPolicy>();
        return services;
    }

    /// <summary>يركّب سياسة مسار جاهزة.</summary>
    public static IServiceCollection AddComplianceFlowPolicy(this IServiceCollection services, IFlowPolicy policy)
    {
        services.AddSingleton(policy);
        return services;
    }

    private static InvalidOperationException NotComposed(string whatAr, string whatEn, string call) =>
        new($"{whatAr} غير مركَّب. وحدة الالتزام لا تختار مزوّدها ولا مخزنها ولا سياستها بنفسها — " +
            $"الجذر التركيبي يفعل ذلك عبر {call}. / " +
            $"{whatEn} is not composed. The compliance module does not choose these for itself; " +
            $"the composition root does, via {call}.");

    /// <summary>
    /// تركيب مزوّد. <b>الاستدعاء الواحد يجلب المزوّد وخاتمه معاً</b> —
    /// لأن الخاتم والقناة زوج متوافق، وفصلهما في التركيب هو أسرع طريق إلى
    /// خاتم «نحن نحوز» يتكلّم مع قناة «المزوّد يحوز».
    /// </summary>
    public static IServiceCollection AddComplianceProvider<TProvider>(this IServiceCollection services)
        where TProvider : class, IComplianceProvider
    {
        services.AddSingleton<IComplianceProvider, TProvider>();
        return services;
    }

    public static IServiceCollection AddComplianceProvider(
        this IServiceCollection services, IComplianceProvider provider)
    {
        services.AddSingleton(provider);
        return services;
    }

    /// <summary>
    /// مخزن في الذاكرة. <b>‏<c>AddSingleton</c> لا <c>TryAdd</c> عمداً</b>: التركيب يسجّل
    /// نائباً يرمي لكل ما لا تملكه الوحدة، فـ<c>TryAdd</c> هنا كان يُصبح لا-عملية ويبقى
    /// النائب هو المحلول — أي أن نداء التركيب الصريح يُبتلع بصمت. والتسجيل الصريح يغلب
    /// النائب مهما كان ترتيب النداءين، لأن آخر تسجيل هو ما يحلّه <c>GetRequiredService</c>.
    /// </summary>
    public static IServiceCollection AddInMemoryComplianceStore(this IServiceCollection services)
    {
        services.AddSingleton<IComplianceStore>(sp =>
            new InMemoryComplianceStore { Clock = sp.GetRequiredService<TimeProvider>() });
        return services;
    }
}
