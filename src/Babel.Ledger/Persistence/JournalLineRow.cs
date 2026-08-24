namespace Babel.Ledger.Persistence;

/// <summary>
/// سطر القيد. كل مبلغ <c>decimal</c> بمقياس 4 — مفروض ببناء في Rule04 وبنوع
/// <c>numeric(19,4)</c> في المخطّط. لا <c>float</c> ولا <c>double</c> في أي موضع.
/// </summary>
internal sealed class JournalLineRow
{
    public Guid LineId { get; set; }
    public Guid EntryId { get; set; }
    public JournalEntryRow? Entry { get; set; }
    public int LineNo { get; set; }
    public Guid CompanyId { get; set; }

    /// <summary>رمز الحساب الذي حلّه المحرك من الدور — لا الذي سمّته وحدة.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>الدور الذي وُلِّد منه السطر. يُحفظ كي تُفرَض GR-RE-001 في قاعدة البيانات.</summary>
    public string RoleCode { get; set; } = string.Empty;

    public string Qualifier { get; set; } = "*";

    /// <summary>المبلغ بعملة الحركة.</summary>
    public decimal Debit { get; set; }

    /// <summary>المبلغ بعملة الحركة.</summary>
    public decimal Credit { get; set; }

    public string Currency { get; set; } = string.Empty;
    public decimal FxRate { get; set; } = 1m;

    /// <summary>بعملة الشركة — وهو ما يفحصه المشغّل المؤجَّل عند COMMIT.</summary>
    public decimal DebitCompany { get; set; }

    /// <summary>بعملة الشركة — وهو ما يفحصه المشغّل المؤجَّل عند COMMIT.</summary>
    public decimal CreditCompany { get; set; }

    public string? BranchId { get; set; }
    public string? CostCenterId { get; set; }
    public string? ProjectId { get; set; }
    public string? PropertyId { get; set; }
    public string? UnitId { get; set; }

    /// <summary>
    /// المستودع. عمود أظهره <b>أول تنفيذ فعلي</b> للمصفوفة: 14 سطر ترحيل تعلن البُعد
    /// <c>warehouse</c> وثلاثة حسابات تفرضه في <c>required_dimensions</c> — وبلا عمود
    /// له كان GR-COA-002 يرفض كل سطر مخزون رفضاً لا مخرج منه.
    /// </summary>
    public string? WarehouseId { get; set; }

    /// <summary>بند جدول الكميات. البُعد الثاني الذي أظهره التنفيذ (سطران في مصفوفة المشاريع).</summary>
    public string? BoqItemId { get; set; }
    public string SubledgerKind { get; set; } = "none";
    public string? SubledgerPartyId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string DescriptionAr { get; set; } = string.Empty;
    public string DescriptionArSearch { get; set; } = string.Empty;
}
