namespace Babel.Contracts.Posting;

/// <summary>نوع الدفتر المساعد الذي ينتمي إليه السطر (03-accounting-core.md §8).</summary>
public enum SubledgerKind
{
    /// <summary>لا دفتر مساعد.</summary>
    None = 0,

    /// <summary>عميل.</summary>
    Customer = 1,

    /// <summary>مورد.</summary>
    Supplier = 2,

    /// <summary>موظف.</summary>
    Employee = 3,

    /// <summary>أصل ثابت.</summary>
    Asset = 4,

    /// <summary>حساب بنكي أو صندوق.</summary>
    Treasury = 5,
}
