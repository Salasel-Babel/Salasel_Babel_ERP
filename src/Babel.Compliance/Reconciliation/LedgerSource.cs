using Babel.Compliance.Abstractions;

namespace Babel.Compliance.Reconciliation;

/// <summary>
/// مستند خاضع للضريبة كما رحّله الدفتر. <b>قراءة فقط، وفي اتجاه واحد:</b>
/// الالتزام يقرأ من الدفتر ليطابق؛ والدفتر لا يقرأ من الالتزام شيئاً أبداً.
/// كل المبالغ <c>decimal</c>.
/// </summary>
public sealed record PostedTaxableDocument(
    JournalEntryRef JournalEntry,
    TenantId Tenant,
    IssuingUnitId IssuingUnit,
    string DocumentNumber,
    DateTimeOffset PostedAt,
    decimal NetTotal,
    decimal TaxTotal,
    decimal GrossTotal,
    ComplianceDocumentId? ComplianceDocument);

/// <summary>
/// حدّ القراءة من الدفتر لغرض المطابقة وحده. تنفيذه يعيش في وحدة الدفتر،
/// لا هنا — كي لا يستورد الالتزام نموذج المحاسبة كله.
/// </summary>
public interface ILedgerTaxableDocumentSource
{
    Task<IReadOnlyList<PostedTaxableDocument>> ListAsync(
        TenantId tenant, DateTimeOffset from, DateTimeOffset to, CancellationToken ct);
}
