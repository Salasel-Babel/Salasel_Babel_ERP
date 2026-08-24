using Babel.SharedKernel;

namespace Babel.Contracts.Events;

/// <summary>اعتُمدت فاتورة مشتريات.</summary>
public sealed record PurchaseBillApproved : IBusinessEvent
{
    /// <inheritdoc />
    public required TenantId Tenant { get; init; }

    /// <inheritdoc />
    public BabelModule Origin => BabelModule.Purchasing;

    /// <inheritdoc />
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>معرّف الفاتورة داخل وحدة المشتريات.</summary>
    public required string BillId { get; init; }

    /// <summary>معرّف المورد داخل وحدة المشتريات.</summary>
    public required string SupplierId { get; init; }

    /// <summary>إجمالي الفاتورة شامل الضريبة.</summary>
    public required Money GrossTotal { get; init; }
}
