namespace Babel.Contracts.Posting;

/// <summary>
/// <b>دور</b> السطر في الحدث التجاري — لا حساباً.
/// <para>
/// هذه هي النقطة التي تجعل «الوحدة لا تستطيع تسمية حساب» قاعدة بنيوية لا اتفاقاً:
/// الوحدة تصف ما حدث (صافٍ، ضريبة مخرجات، محتجز)، ومصفوفة الترحيل داخل Babel.Ledger
/// هي وحدها من يحوّل الدور إلى رقم حساب. المجموعة مغلقة عمداً: إضافة دور جديد تعديلٌ
/// في العقد يمرّ بمراجعة، لا نصٌّ حرّ يخترعه كل مطوّر.
/// </para>
/// المرجع: docs/reference/posting-matrix.md · docs/analysis/03-accounting-core.md §4
/// </summary>
public enum PostingRole
{
    /// <summary>الصافي قبل الضريبة.</summary>
    NetAmount = 1,

    /// <summary>ضريبة المخرجات (على المبيعات).</summary>
    OutputTax = 2,

    /// <summary>ضريبة المدخلات (على المشتريات).</summary>
    InputTax = 3,

    /// <summary>الإجمالي شامل الضريبة.</summary>
    GrossAmount = 4,

    /// <summary>خصم.</summary>
    Discount = 5,

    /// <summary>محتجز ضمان.</summary>
    Retention = 6,

    /// <summary>استرداد دفعة مقدمة.</summary>
    AdvanceSettlement = 7,

    /// <summary>تكلفة البضاعة المباعة.</summary>
    CostOfGoodsSold = 8,

    /// <summary>حركة مخزون.</summary>
    InventoryMovement = 9,

    /// <summary>تسوية نقدية أو بنكية (طريقة الدفع).</summary>
    Settlement = 10,

    /// <summary>فرق تقريب.</summary>
    RoundingDifference = 11,

    /// <summary>فرق عملة.</summary>
    ExchangeDifference = 12,

    /// <summary>مصروف مستحق أو إيراد مستحق (استحقاق دوري).</summary>
    Accrual = 13,

    /// <summary>إهلاك.</summary>
    Depreciation = 14,
}
