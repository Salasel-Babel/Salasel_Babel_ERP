using Babel.SharedKernel;

namespace Babel.Inventory.Subledger;

/// <summary>سبب انحراف مستند بين دفتر المخزون المساعد ونقطة الضبط.</summary>
public static class DivergenceReason
{
    /// <summary>الوحدة سجّلت حركة مخزون ولا حركة لها في نقطة الضبط.</summary>
    public const string MissingInControl = "missing_in_control";

    /// <summary>حركة في نقطة الضبط بلا حركة مخزون مقابلة.</summary>
    public const string MissingInSubledger = "missing_in_subledger";

    /// <summary>المستند موجود على الطرفين والمبلغان مختلفان.</summary>
    public const string AmountMismatch = "amount_mismatch";
}

/// <summary>سطر انحراف واحد في تقرير المطابقة.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="SubledgerEffect">أثره كما يعرفه دفتر المخزون المساعد.</param>
/// <param name="ControlEffect">أثره كما هو في نقطة الضبط.</param>
/// <param name="Divergence">الفارق.</param>
/// <param name="ReasonCode">سبب الانحراف.</param>
public sealed record ReconciliationDivergence(
    string DocumentType,
    string DocumentId,
    string ItemId,
    Money SubledgerEffect,
    Money ControlEffect,
    Money Divergence,
    string ReasonCode);

/// <summary>
/// تقرير المطابقة بين دفتر المخزون المساعد وحسابه الضابط.
/// <para>
/// ليس تقريراً يُطبع بل <b>وظيفة</b>: انحرافٌ واحد بريال واحد يُسمّي المستندات
/// المسؤولة عنه. ودفترٌ مساعد ينحرف بصمت عن حسابه الضابط أشيعُ عيب في الأنظمة
/// المحاسبية، ولا يُكتشف إلا بعد شهور — وقد لُدغ هذا المستودع به فعلاً
/// (‏<c>docs/evidence/traps.md#fakh-44</c>).
/// </para>
/// </summary>
/// <param name="AsOf">تاريخ المطابقة.</param>
/// <param name="SubledgerTotal">مجموع دفتر المخزون المحسوب من حركاته.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط في دفتر الأستاذ.</param>
/// <param name="BalanceTotal">مجموع أرصدة الأصناف — الطريق الثالث إلى الرقم نفسه.</param>
/// <param name="Divergence">الفارق: مجموع الدفتر المساعد ناقص نقطة الضبط.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟ لا «قريب من الصفر».</param>
/// <param name="Divergences">المستندات المسؤولة عن الفارق.</param>
public sealed record ControlReconciliationReport(
    DateOnly AsOf,
    Money SubledgerTotal,
    Money ControlTotal,
    Money BalanceTotal,
    Money Divergence,
    bool IsReconciled,
    IReadOnlyList<ReconciliationDivergence> Divergences);

/// <summary>بندٌ يمنع إقفال الفترة على المخزون.</summary>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع داخل المستودع — البند يُسمّي موضعه لا مستودعه وحده.</param>
/// <param name="Quantity">الكمية بوحدة أساسها.</param>
/// <param name="Value">القيمة.</param>
/// <param name="ReasonCode">سبب المنع.</param>
public sealed record CloseObstacle(
    string ItemId,
    string WarehouseId,
    string LocationId,
    Babel.Contracts.Inventory.InventoryQuantity Quantity,
    Money Value,
    string ReasonCode);

/// <summary>أسباب منع إقفال الفترة على المخزون.</summary>
public static class CloseObstacleReason
{
    /// <summary>
    /// كمية سالبة: بيعٌ سبق إدخال استلامه. ليست خطأً في لحظة وقوعه، لكنها
    /// <b>لا تُقفَل عليها فترة</b>: الرصيد يقول إن في المستودع كميةً سالبة.
    /// </summary>
    public const string NegativeQuantity = "negative_quantity";

    /// <summary>
    /// قيمة بلا كمية: الوحدات انصرفت كلّها وبقيت قيمة.
    /// <para>
    /// وهذا هو <b>أثر التكلفة المتأخّرة</b> بعد أن يهبط الاستلام على رصيد سالب:
    /// الفرق بين ما نُزِّل على تكلفة المبيعات وما دُفع فعلاً يبقى في المخزون على
    /// كميةٍ صفرية. رقمٌ صحيح في المطابقة، وواقعٌ مستحيل في المستودع.
    /// </para>
    /// </summary>
    public const string ValueWithoutQuantity = "value_without_quantity";
}
