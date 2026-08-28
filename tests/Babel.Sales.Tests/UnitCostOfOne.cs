using Babel.Contracts.Inventory;
using Babel.SharedKernel;

namespace Babel.Sales.Tests;

/// <summary>
/// حدّ تقييم بديل لهذه المجموعة وحدها: تكلفة الوحدة <b>واحد</b> دائماً.
/// <para>
/// <b>وما لا يُثبته هذا النوع يُقال صراحةً:</b> لا يُثبت شيئاً عن المتوسط المرجّح، ولا
/// عن الرصيد السالب، ولا عن التكلفة المتأخّرة. تلك كلّها مُثبَتة على قاعدة بيانات
/// حقيقية في <c>Babel.Inventory.Tests</c>، حيث يعمل <c>StockMovementService</c> نفسه.
/// </para>
/// <para>
/// <b>ووجوده هنا ليس تهرّباً بل حدّ نطاق:</b> اختبارات المبيعات تُثبت هوية الترحيل
/// وبوّابة القبول وأثر قيد التكلفة على نقطة ضبط <b>العملاء</b> — وهي أسئلة لا تتغيّر
/// إجاباتها باختلاف طريقة التقييم. وربطُها بمخزون حقيقي كان سيجعل كل واحد منها
/// يحتاج استلاماً مسبقاً، فيُقاس شيءٌ آخر غير الذي وُضع ليقيسه.
/// </para>
/// <para>
/// وتكلفة الوحدة <b>واحد</b> بالتحديد كي تبقى أرقام هذه المجموعة كما كانت حرفاً بحرف
/// قبل أن يصير الحقل كميةً: ما كان <c>Sar(500)</c> صار <c>500</c>، والقيد نفسه.
/// </para>
/// </summary>
internal sealed class UnitCostOfOne : IInventoryValuation
{
    private readonly Dictionary<(string Module, string DocumentType, string DocumentId, string TriggerCode, int Generation, string EventCode), InventoryMovementCost> _recorded = [];

    public ValueTask<Result<InventoryMovementCost>> ReceiveAsync(
        InventoryReceipt receipt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(receipt);
        return ValueTask.FromResult(Record(receipt.Source, receipt.Quantity, receipt.Cost.Currency));
    }

    public ValueTask<Result<InventoryMovementCost>> IssueAsync(
        InventoryIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return ValueTask.FromResult(Record(issue.Source, issue.Quantity, CurrencyCode.FromString("SAR")));
    }

    public ValueTask<Result<InventoryMovementCost>> ReturnAsync(
        InventoryReturn movement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movement);
        return ValueTask.FromResult(Record(movement.Source, movement.Quantity, CurrencyCode.FromString("SAR")));
    }

    /// <summary>
    /// والإحكام محفوظ حتى في البديل: الوصول الثاني بالهوية نفسها يُعيد الرقم الأول
    /// ويقول إنه أُعيد. بديلٌ لا يحفظ ذلك يجعل الاختبار يمرّ على سلوك لا وجود له.
    /// </summary>
    private Result<InventoryMovementCost> Record(InventoryMovementSource source, decimal quantity, CurrencyCode currency)
    {
        // والمفتاح صفٌّ من الحقول لا سلسلة موصولة: الوصل على فاصل قد يحتويه أحد
        // المكوّنات عطبُ تصادم بذاته، ولا داعي له حيث تكفي الصفّية.
        (string, string, string, string, int, string) key = (
            source.Module.ToString(),
            source.DocumentType,
            source.DocumentId,
            source.TriggerCode,
            source.Generation,
            source.EventCode);

        if (_recorded.TryGetValue(key, out InventoryMovementCost? seen))
        {
            return Result<InventoryMovementCost>.Success(seen with { WasAlreadyRecorded = true });
        }

        InventoryMovementCost cost = new(
            Money.Of(quantity, currency),
            "unit_cost_of_one_test_double",
            quantity,
            Money.Of(quantity, currency),
            DrewOnNegativeStock: false,
            WasAlreadyRecorded: false);

        _recorded[key] = cost;
        return Result<InventoryMovementCost>.Success(cost);
    }
}
