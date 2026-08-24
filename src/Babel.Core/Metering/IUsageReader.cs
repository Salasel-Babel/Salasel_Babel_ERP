using Babel.SharedKernel;

namespace Babel.Core.Metering;

/// <summary>قراءة الاستخدام لأغراض الفوترة. محورا التسعير كلاهما مقروء.</summary>
public interface IUsageReader
{
    /// <summary>عدد الاستدعاءات المقيسة لكل وحدة في شهر فوترة.</summary>
    ValueTask<IReadOnlyDictionary<BabelModule, long>> GetModuleUsageAsync(
        TenantId tenant,
        BillingPeriod period,
        CancellationToken cancellationToken = default);

    /// <summary>المستخدمون الذين ظهر لهم نشاط في شهر فوترة.</summary>
    ValueTask<IReadOnlyCollection<UserId>> GetActiveUsersAsync(
        TenantId tenant,
        BillingPeriod period,
        CancellationToken cancellationToken = default);
}
