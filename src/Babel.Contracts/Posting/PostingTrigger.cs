namespace Babel.Contracts.Posting;

/// <summary>الحدث الذي أطلق الترحيل. مدخل لمصفوفة الترحيل (03-accounting-core.md §4).</summary>
public enum PostingTrigger
{
    /// <summary>عند اعتماد المستند.</summary>
    OnApproval = 1,

    /// <summary>عند الاستلام الفعلي.</summary>
    OnReceipt = 2,

    /// <summary>عند الدفع أو التحصيل.</summary>
    OnSettlement = 3,

    /// <summary>ترحيل دوري (استحقاق، إهلاك، إعادة تقييم).</summary>
    Periodic = 4,

    /// <summary>عكس مستند سبق ترحيله.</summary>
    Reversal = 5,
}
