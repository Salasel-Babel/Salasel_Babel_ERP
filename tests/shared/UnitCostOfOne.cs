using Babel.Contracts.Inventory;
using Babel.SharedKernel;

namespace Babel.Tests.Shared;

/// <summary>
/// حدّ تقييم بديل لهذه المجموعة وحدها: تكلفة الوحدة <b>واحد</b> دائماً.
/// <para>
/// <b>وما لا يُثبته هذا النوع يُقال صراحةً:</b> لا يُثبت شيئاً عن المتوسط المرجّح، ولا
/// عن الرصيد السالب، ولا عن التكلفة المتأخّرة. تلك كلّها مُثبَتة على قاعدة بيانات
/// حقيقية في <c>Babel.Inventory.Tests</c>، حيث يعمل <c>StockMovementService</c> نفسه.
/// </para>
/// <para>
/// <b>وملفٌّ واحد تتشاركه مجموعتا المبيعات والمشتريات بالربط لا بالنسخ</b> — فالبديل
/// الذي يُنسخ ينحرف عن نسخته الأخرى عند أول تعديل، وهو
/// <c>docs/evidence/traps.md#fakh-81</c> بعينه.
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

        // ‏**الوارد يُسجَّل بتكلفته الحقيقية** لا بعدد وحداته: مرتجع المشتريات يُقيَّم
        // بتكلفة استلامه الأصلي، فبديلٌ يُسجّل الوارد بالعدّ كان سيجعل الاختبار يمرّ
        // على رقمٍ لا وجود له في الإنتاج. و«تكلفة الوحدة واحد» تبقى على **الصادر**
        // وحده — وهو ما تقيسه مجموعة المبيعات.
        return ValueTask.FromResult(Record(
            receipt.Source, receipt.Location, receipt.Quantity, receipt.Cost.Amount, receipt.Cost.Currency));
    }

    public ValueTask<Result<InventoryMovementCost>> IssueAsync(
        InventoryIssue issue, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return ValueTask.FromResult(Record(
            issue.Source, issue.Location, issue.Quantity, issue.Quantity.Magnitude, CurrencyCode.FromString("SAR")));
    }

    /// <summary>
    /// المرتجع يقرأ صنفه ومستودعه <b>من الحركة الأصلية</b> كما يفعل المخزون الحقيقي:
    /// المستدعي يُسلّم هويتها وحدها، والبديل الذي يخترع موضعاً كان سيُخفي أن
    /// وحدة المبيعات لا تعرف الصنف في هذا المسار.
    /// </summary>
    /// <param name="movement">المرتجع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryMovementCost>> ReturnAsync(
        InventoryReturn movement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movement);

        if (!_recorded.TryGetValue(KeyOf(movement.OriginalMovement), out InventoryMovementCost? original))
        {
            return ValueTask.FromResult(Result<InventoryMovementCost>.Failure(new Error(
                "inventory.original_issue_not_found",
                "لا حركة بهذه الهوية: " + movement.OriginalMovement.DocumentType + "/" + movement.OriginalMovement.DocumentId,
                "No movement with this identity: "
                + movement.OriginalMovement.DocumentType + "/" + movement.OriginalMovement.DocumentId)));
        }

        // الردّ يُقيَّم بحصّته من قيمة الحركة الأصلية — كما يفعل المخزون الحقيقي:
        // ردٌّ كامل يستعيد القيمة بالضبط، وردٌّ جزئي حصّته منها.
        decimal value = movement.Quantity.Magnitude == original.Quantity.Magnitude
            ? original.Cost.Amount
            : decimal.Round(
                original.Cost.Amount * movement.Quantity.Magnitude / original.Quantity.Magnitude,
                4,
                MidpointRounding.ToEven);

        return ValueTask.FromResult(
            Record(movement.Source, original.Location, movement.Quantity, value, original.Cost.Currency));
    }

    /// <summary>
    /// إلغاء حركة: يقرأ كمّيتها وقيمتها <b>من الحركة نفسها</b> كما يفعل المخزون الحقيقي —
    /// فالبديل الذي يقبل كمّيةً من المستدعي كان سيُخفي أن العكس لا يختار مقداره.
    /// </summary>
    /// <param name="movement">طلب الإلغاء.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryMovementCost>> ReverseMovementAsync(
        InventoryMovementReversal movement, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(movement);

        if (!_recorded.TryGetValue(KeyOf(movement.ReversedMovement), out InventoryMovementCost? annulled))
        {
            return ValueTask.FromResult(Result<InventoryMovementCost>.Failure(new Error(
                "inventory.original_movement_not_found",
                "لا حركة بهذه الهوية: " + movement.ReversedMovement.DocumentType + "/" + movement.ReversedMovement.DocumentId,
                "No movement with this identity: "
                + movement.ReversedMovement.DocumentType + "/" + movement.ReversedMovement.DocumentId)));
        }

        return ValueTask.FromResult(Record(
            movement.Source, annulled.Location, annulled.Quantity, annulled.Cost.Amount, annulled.Cost.Currency));
    }

    /// <summary>قراءة حركة مُسجَّلة — بالمفتاح نفسه الذي كُتبت به.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="source">هوية الحركة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryMovementCost>> ReadMovementAsync(
        TenantId tenant,
        UserId actor,
        InventoryMovementSource source,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(source);

        return ValueTask.FromResult(_recorded.TryGetValue(KeyOf(source), out InventoryMovementCost? found)
            ? Result<InventoryMovementCost>.Success(found with { WasAlreadyRecorded = true })
            : Result<InventoryMovementCost>.Failure(new Error(
                "inventory.original_movement_not_found",
                "لا حركة بهذه الهوية: " + source.DocumentType + "/" + source.DocumentId,
                "No movement with this identity: " + source.DocumentType + "/" + source.DocumentId)));
    }

    /// <summary>
    /// والإحكام محفوظ حتى في البديل: الوصول الثاني بالهوية نفسها يُعيد الرقم الأول
    /// ويقول إنه أُعيد. بديلٌ لا يحفظ ذلك يجعل الاختبار يمرّ على سلوك لا وجود له.
    /// </summary>
    private Result<InventoryMovementCost> Record(
        InventoryMovementSource source,
        InventoryItemLocation location,
        InventoryQuantity quantity,
        decimal value,
        CurrencyCode currency)
    {
        (string, string, string, string, int, string) key = KeyOf(source);

        if (_recorded.TryGetValue(key, out InventoryMovementCost? seen))
        {
            return Result<InventoryMovementCost>.Success(seen with { WasAlreadyRecorded = true });
        }

        InventoryMovementCost cost = new(
            Money.Of(value, currency),
            "unit_cost_of_one_test_double",
            location,
            quantity,
            quantity,
            Money.Of(value, currency),
            DrewOnNegativeStock: false,
            WasAlreadyRecorded: false);

        _recorded[key] = cost;
        return Result<InventoryMovementCost>.Success(cost);
    }

    /// <summary>
    /// المفتاح صفٌّ من الحقول لا سلسلة موصولة: الوصل على فاصل قد يحتويه أحد
    /// المكوّنات عطبُ تصادم بذاته، ولا داعي له حيث تكفي الصفّية.
    /// </summary>
    private static (string, string, string, string, int, string) KeyOf(InventoryMovementSource source) => (
        source.Module.ToString(),
        source.DocumentType,
        source.DocumentId,
        source.TriggerCode,
        source.Generation,
        source.EventCode);
}
