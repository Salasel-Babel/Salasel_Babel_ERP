using Babel.Compliance.Abstractions;
using Babel.Compliance.Canonical;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Reconciliation;
using Babel.Compliance.Store;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Babel.Compliance;

/// <summary>
/// التركيب. <b>هنا وهنا وحده يُعرف شكل حيازة المفتاح</b>: يُحقن مزوّد واحد،
/// ومعه خاتمه المتوافق. المُنسِّق نفسه لا يتفرّع على الشكل أبداً.
/// </summary>
public static class ComplianceRegistration
{
    public static IServiceCollection AddBabelCompliance(
        this IServiceCollection services,
        ComplianceSettings? settings = null,
        TimeProvider? clock = null)
    {
        services.TryAddSingleton(clock ?? TimeProvider.System);
        services.TryAddSingleton(settings ?? new ComplianceSettings());

        services.TryAddSingleton<IXmlCanonicaliser, DeterministicXmlSerialiser>();
        services.TryAddSingleton<IDocumentRenderer>(sp =>
            new ProvisionalDocumentRenderer(sp.GetRequiredService<IXmlCanonicaliser>()));

        services.TryAddSingleton<IIssuingUnitRegistry, InMemoryIssuingUnitRegistry>();

        services.TryAddScoped<ComplianceDocumentFactory>();
        services.TryAddScoped<ClearanceCoordinator>();
        services.TryAddScoped<ReportingWorker>();
        services.TryAddScoped<ComplianceService>();
        services.TryAddScoped<Reconciler>();

        return services;
    }

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

    public static IServiceCollection AddInMemoryComplianceStore(this IServiceCollection services)
    {
        services.TryAddSingleton<IComplianceStore>(sp =>
            new InMemoryComplianceStore { Clock = sp.GetRequiredService<TimeProvider>() });
        return services;
    }
}
