namespace Babel.Inventory.Persistence;

/// <summary>اتجاه حركة المخزون.</summary>
internal static class MovementDirection
{
    /// <summary>وارد — يزيد الكمية والقيمة.</summary>
    public const string In = "IN";

    /// <summary>صادر — ينقص الكمية والقيمة.</summary>
    public const string Out = "OUT";
}

/// <summary>
/// حركة مخزون واحدة — <b>جدول يُضاف إليه فقط</b>.
/// <para>
/// التصحيح بحركة مضادّة لا بتعديل صفّ، للسبب الذي يجعل الدفتر كذلك
/// (‏<c>ADR-0002</c>): الدفتر المساعد الذي يُعدَّل صفُّه لا يُطابَق بمستنداته بعد ذلك،
/// والمطابقة هي الوظيفة كلّها.
/// </para>
/// <para><c>internal</c> — لا يعبر حدّ الوحدة (القاعدة 5).</para>
/// </summary>
internal sealed class StockMovementRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    // ── هوية الحركة: هي هوية الترحيل حرفاً بحرف ─────────────────────────────
    public string SourceModule { get; set; } = string.Empty;

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string TriggerCode { get; set; } = string.Empty;

    public int Generation { get; set; } = 1;

    /// <summary>رمز الحدث — الحقل الذي بدونه يُبتلع الحدث الثاني بصمت (‏فخ-45).</summary>
    public string EventCode { get; set; } = string.Empty;

    // ── ما تحرّك ─────────────────────────────────────────────────────────────
    public string ItemId { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = string.Empty;

    public string Direction { get; set; } = MovementDirection.In;

    /// <summary>الكمية — موجبة دائماً؛ الاتجاه في <see cref="Direction"/> لا في الإشارة.</summary>
    public decimal Quantity { get; set; }

    /// <summary>قيمة الحركة — موجبة دائماً، بمقياس 4 (‏فخ-17).</summary>
    public decimal ValueAmount { get; set; }

    /// <summary>تكلفة الوحدة التي أُنتجت بها هذه الحركة، بمقياس 6.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>رمز طريقة التقييم التي أنتجت القيمة — مكتوب على الحركة لا مفترَض.</summary>
    public string Method { get; set; } = string.Empty;

    /// <summary>هل صُرفت من رصيد لا يغطيها؟ سؤال إقفال الفترة يقرأ هذا الحقل.</summary>
    public bool DrewOnNegativeStock { get; set; }

    /// <summary>
    /// الحركة التي تَرُدّ عليها هذه الحركة — هويةُ الصرف الأصلي مُرمَّزةً، أو نصّ فارغ.
    /// <para>
    /// <b>عمودٌ صريح لا اصطلاحُ تسمية:</b> «المرتجع يُقيَّم بتكلفة صرفه الأصلي» يقتضي
    /// أن يُقال أيّ صرف، وأن يُعَدّ ما رُدّ منه. واستنتاجُ ذلك من نوع المستند أو من
    /// «آخر صرف للصنف» اختيارٌ لا يقرّره أحد ولا يُراجَع.
    /// </para>
    /// <para>
    /// والترميز مسبوقٌ بالطول لكل مكوّن، فلا فاصل «آمن» يُفترض أنه لا يظهر في البيانات
    /// — وهو الشكل الذي لُدغ به هذا المستودع في <c>source_ref</c> المدموج.
    /// </para>
    /// </summary>
    public string AgainstKey { get; set; } = string.Empty;

    public decimal QuantityAfter { get; set; }

    public decimal ValueAfter { get; set; }

    public DateOnly OccurredOn { get; set; }

    public DateTime RecordedAt { get; set; }

    public string ActorId { get; set; } = string.Empty;
}

/// <summary>
/// رصيد التقييم لكل (منشأة × صنف × مستودع).
/// <para>
/// <b>صفٌّ واحد يُحدَّث بـ<c>INSERT … ON CONFLICT DO UPDATE</c> وحده</b>، ولا
/// <c>UPDATE</c> مجرّد عليه أبداً: العبارة المجرّدة على صفٍّ لم يُنشأ بعد تُصيب صفر
/// صفوف و<b>تُعدّ نجاحاً</b> (‏فخ-09).
/// </para>
/// </summary>
internal sealed class ItemBalanceRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>الكمية — قد تكون سالبة: البيع قبل إدخال الاستلام واقعة يومية لا خطأ.</summary>
    public decimal Quantity { get; set; }

    /// <summary>القيمة بمقياس 4.</summary>
    public decimal ValueAmount { get; set; }

    /// <summary>متوسط تكلفة الوحدة المتحرّك، بمقياس 6.</summary>
    public decimal UnitCost { get; set; }

    /// <summary>
    /// هل ورد هذا الصنف إلى هذا المستودع مرّةً بتكلفة؟
    /// <para>
    /// حقلٌ مستقلّ عن <see cref="UnitCost"/> عمداً: بدونه لا يُفرَّق بين «تكلفة الوحدة
    /// صفر لأن الصنف لم يُستلم قط» و«تكلفته صفر فعلاً» — والفرق هو الفرق بين رفضٍ
    /// مكتوب ورقمٍ مخترَع.
    /// </para>
    /// </summary>
    public bool HasCostBasis { get; set; }

    public DateTime UpdatedAt { get; set; }
}
