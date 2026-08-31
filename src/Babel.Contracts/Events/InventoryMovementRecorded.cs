using Babel.SharedKernel;

namespace Babel.Contracts.Events;

/// <summary>
/// سُجّلت حركة مخزون. يستهلكه من يحتاجه — المبيعات لا تستدعي المخزون مباشرة أبداً.
/// </summary>
public sealed record InventoryMovementRecorded : IBusinessEvent
{
    /// <inheritdoc />
    public required TenantId Tenant { get; init; }

    /// <inheritdoc />
    public BabelModule Origin => BabelModule.Inventory;

    /// <inheritdoc />
    public required DateTimeOffset OccurredAt { get; init; }

    /// <summary>معرّف الحركة داخل وحدة المخزون.</summary>
    public required string MovementId { get; init; }

    /// <summary>معرّف الصنف داخل وحدة المخزون.</summary>
    public required string ItemId { get; init; }

    /// <summary>الكمية. موجبة للوارد وسالبة للصادر.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>قيمة الحركة بتكلفتها.</summary>
    public required Money MovementValue { get; init; }
}
