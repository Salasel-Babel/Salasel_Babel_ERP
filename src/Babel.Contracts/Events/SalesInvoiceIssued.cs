using Babel.SharedKernel;

namespace Babel.Contracts.Events;

/// <summary>صدرت فاتورة مبيعات. مثال على شكل عقد الحدث — لا منطق فيه.</summary>
public sealed record SalesInvoiceIssued : IBusinessEvent
{
    /// <inheritdoc />
    public required TenantId Tenant { get; init; }

    /// <inheritdoc />
    public BabelModule Origin => BabelModule.Sales;

    /// <inheritdoc />
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>معرّف الفاتورة داخل وحدة المبيعات.</summary>
    public required string InvoiceId { get; init; }

    /// <summary>معرّف العميل داخل وحدة المبيعات.</summary>
    public required string CustomerId { get; init; }

    /// <summary>إجمالي الفاتورة شامل الضريبة.</summary>
    public required Money GrossTotal { get; init; }

    /// <summary>مبلغ ضريبة القيمة المضافة.</summary>
    public required Money TaxTotal { get; init; }
}
