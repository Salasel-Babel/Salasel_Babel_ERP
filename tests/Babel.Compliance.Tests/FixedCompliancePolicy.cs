using Babel.Compliance.Pipeline;
using Babel.SharedKernel;
using TenantId = Babel.Compliance.Abstractions.TenantId;

namespace Babel.Compliance.Tests;

/// <summary>
/// سياسةُ إبلاغٍ ثابتة لكلّ منشأة في الاختبارات: <b>معطى اختبار</b> لا افتراضُ منتج —
/// الوحدةُ في الإنتاج تسألها من خدمة المعامِلات لكلّ منشأة (ADR-0090). والقيمُ هنا هي
/// افتراضاتُ المنصّة المشحونة نفسها كي تبقى الاختباراتُ القائمة على معناها.
/// </summary>
public sealed class FixedCompliancePolicy(CompliancePolicy policy) : ICompliancePolicySource
{
    public static CompliancePolicy Shipped { get; } =
        new(ReportingWindow: TimeSpan.FromHours(24), QueueAgeAlarm: TimeSpan.FromHours(4), MaxResolutionAttempts: 3);

    public CompliancePolicy Policy { get; } = policy;

    public ValueTask<Result<CompliancePolicy>> ResolveAsync(TenantId tenant, CancellationToken ct = default)
        => ValueTask.FromResult(Result<CompliancePolicy>.Success(Policy));
}
