using Babel.Compliance.Pipeline;
using Babel.Core.Parameters;
using Babel.SharedKernel;
using Xunit;
using TenantId = Babel.Compliance.Abstractions.TenantId;

namespace Babel.Compliance.Tests;

/// <summary>
/// <b>سياسةُ الإبلاغ تأتي من خدمة المعامِلات لكلّ منشأة (ADR-0090)</b> — لا من إعدادٍ مفرد.
/// </summary>
public sealed class CompliancePolicyTests
{
    [Fact]
    public async Task منشأةٌ_بمعرّفها_تقرأ_افتراضَ_المنصّة_المشحون_بالساعات_والمحاولات()
    {
        ManualClock clock = new(new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero));
        ParameterCompliancePolicySource source = new(new ParameterDirectory(new InMemoryParameterStore(clock)), clock);

        Result<CompliancePolicy> policy = await source.ResolveAsync(
            new TenantId(Guid.NewGuid().ToString("D")), TestContext.Current.CancellationToken);

        Assert.True(policy.IsSuccess, string.Join(" | ", policy.Errors.Select(static e => e.Code)));

        ParameterVersionView shipped = PlatformDefaults.All.Single(v => v.SetCode == ParameterCatalogue.ComplianceReporting);
        decimal Shipped(string key) => shipped.Values.Single(v => v.Key == key).Value;

        Assert.Equal(TimeSpan.FromHours((double)Shipped(ParameterCatalogue.ComplianceReportingWindowHours)), policy.Value.ReportingWindow);
        Assert.Equal(TimeSpan.FromHours((double)Shipped(ParameterCatalogue.ComplianceQueueAgeAlarmHours)), policy.Value.QueueAgeAlarm);
        Assert.Equal((int)Shipped(ParameterCatalogue.ComplianceMaxResolutionAttempts), policy.Value.MaxResolutionAttempts);
    }

    [Fact]
    public async Task منشأةٌ_ليست_معرّفاً_تُرفض_باسمها_ولا_تُملأ_بافتراض()
    {
        ManualClock clock = new(new DateTimeOffset(2026, 9, 6, 9, 0, 0, TimeSpan.Zero));
        ParameterCompliancePolicySource source = new(new ParameterDirectory(new InMemoryParameterStore(clock)), clock);

        Result<CompliancePolicy> refused = await source.ResolveAsync(new TenantId("acme"), TestContext.Current.CancellationToken);

        Assert.True(refused.IsFailure);
        Assert.Equal("compliance.policy.tenant_not_a_company", Assert.Single(refused.Errors).Code);
    }

    [Fact]
    public void لقطةٌ_ناقصةٌ_مفتاحاً_خطأٌ_برمجي_لا_افتراض()
    {
        ParameterVersionView shipped = PlatformDefaults.All.Single(v => v.SetCode == ParameterCatalogue.ComplianceReporting);
        Babel.Contracts.Parameters.ParameterSnapshot partial = new()
        {
            VersionId = shipped.Id,
            SetCode = shipped.SetCode,
            Scope = shipped.Scope,
            EffectiveFrom = shipped.EffectiveFrom,
            Approval = shipped.Approval,
            SourceRef = shipped.SourceRef,
            Values = shipped.Values
                .Where(v => v.Key != ParameterCatalogue.ComplianceQueueAgeAlarmHours)
                .ToDictionary(v => v.Key, v => v.Value, StringComparer.Ordinal),
        };

        InvalidOperationException thrown = Assert.Throws<InvalidOperationException>(() => CompliancePolicy.From(partial));
        Assert.Contains(ParameterCatalogue.ComplianceQueueAgeAlarmHours, thrown.Message, StringComparison.Ordinal);
    }
}
