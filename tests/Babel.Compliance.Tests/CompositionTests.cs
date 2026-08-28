using Babel.Compliance.Abstractions;
using Babel.Compliance.Application;
using Babel.Compliance.FakeProvider;
using Babel.Compliance.Pipeline;
using Babel.Compliance.Zatca.Documents;
using Babel.Contracts.Compliance;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.SharedKernel;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Babel.Compliance.Tests;

/// <summary>
/// <b>ما يُركِّبه <c>AddBabelCompliance()</c> فعلاً — لا ما يُظنّ أنه يُركِّبه.</b>
/// <para>
/// كانت في فضاء الاسم <c>Babel.Compliance</c> دالّتا امتداد بهذا الاسم، فنداء الجذر
/// التركيبي بلا وسائط كان يذهب — بلا تحذير ترجمة — إلى التي لا تسجّل إلا خدمة التطبيق،
/// فلا يُركَّب المُنسِّق ولا العامل ولا المصنع
/// (‏<c>docs/evidence/traps.md#fakh-two-registrations-one-name</c>).
/// وهذه الاختبارات تُثبت أن النداء الواحد يُركِّب المسار كلّه، وأن ما لا تملكه الوحدة
/// يُرفض باسمه بلغتين بدل رسالة حقن غامضة.
/// </para>
/// </summary>
public sealed class CompositionTests
{
    private static ServiceCollection Composed(bool withProvider = true)
    {
        ServiceCollection services = new();
        services.AddBabelCompliance();
        services.AddInMemoryComplianceStore();
        services.AddComplianceFlowPolicy<ZatcaFlowPolicy>();
        services.AddSingleton<IEntitlementEnforcer, RefusingEnforcer>();

        if (withProvider)
        {
            services.AddComplianceProvider(new FakeComplianceProvider(
                KeyCustody.SelfHeld, new FakeAuthority(), TimeProvider.System));
        }

        return services;
    }

    [Fact]
    public void One_call_composes_the_whole_pipeline_not_just_the_application_service()
    {
        using ServiceProvider sp = Composed().BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        // المُنسِّق والعامل والمصنع — وهي بالضبط ما كان يسقط من التركيب.
        Assert.NotNull(scope.ServiceProvider.GetService<ComplianceDocumentFactory>());
        Assert.NotNull(scope.ServiceProvider.GetService<ClearanceCoordinator>());
        Assert.NotNull(scope.ServiceProvider.GetService<ReportingWorker>());
        Assert.NotNull(scope.ServiceProvider.GetService<ComplianceService>());
    }

    [Fact]
    public void The_published_port_and_the_application_service_are_the_same_instance()
    {
        using ServiceProvider sp = Composed().BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        var port = scope.ServiceProvider.GetRequiredService<IElectronicDocumentIntake>();
        var service = scope.ServiceProvider.GetRequiredService<EInvoiceSubmissionService>();

        // نوعٌ واحد خلف الاثنين: لا مساران للإرسال.
        Assert.Same(service, port);
        Assert.IsAssignableFrom<IApplicationService>(port);
    }

    [Fact]
    public void A_missing_provider_refuses_by_name_in_both_languages_not_with_an_opaque_injection_error()
    {
        using ServiceProvider sp = Composed(withProvider: false).BuildServiceProvider();
        using IServiceScope scope = sp.CreateScope();

        var thrown = Assert.Throws<InvalidOperationException>(
            () => scope.ServiceProvider.GetRequiredService<ComplianceService>());

        // الرسالة تسمّي ما نقص وتسمّي النداء الذي يُصلحه — بالعربية والإنجليزية.
        string message = Flatten(thrown);
        Assert.Contains("مزوّد الالتزام", message, StringComparison.Ordinal);
        Assert.Contains("AddComplianceProvider<T>()", message, StringComparison.Ordinal);
        Assert.Contains("is not composed", message, StringComparison.Ordinal);
    }

    private static string Flatten(Exception ex)
    {
        string text = ex.Message;
        for (Exception? inner = ex.InnerException; inner is not null; inner = inner.InnerException)
        {
            text += " | " + inner.Message;
        }

        return text;
    }

    /// <summary>منفِّذ استحقاق للتركيب وحده — لا يُستدعى في هذه الاختبارات.</summary>
    private sealed class RefusingEnforcer : IEntitlementEnforcer
    {
        public ValueTask<Result> EnsureAsync(
            SharedKernel.TenantId tenant, UserId actor, BabelModule module, EntitlementAccess access,
            string operation, CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(Result.Failure(new Error(
                "test.not_called", "لا يُستدعى في هذه الاختبارات", "not called in these tests")));
    }
}
