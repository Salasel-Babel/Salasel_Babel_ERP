using Babel.SharedKernel;

namespace Babel.Contracts.Inventory;

/// <summary>
/// المستند الذي أطلق حركة المخزون — <b>وهو هوية الحركة نفسها لا وصفٌ لها</b>.
/// <para>
/// الحقول هي حقول هوية الترحيل حرفاً بحرف (‏ADR-0016 · ADR-0017): نوع المستند ومعرّفه
/// ورمز الإطلاق والجيل ورمز الحدث. والسبب أن حركة المخزون وقيد التكلفة <b>واقعة واحدة
/// تُروى مرّتين</b>: مرّة في الدفتر المساعد ومرّة في الحساب الضابط. فلو اختلفت هويّتاهما
/// لصار الانحراف ممكناً بلا مستند مسؤول — وهو الشكل الذي وصفه
/// <c>docs/evidence/traps.md#fakh-44</c> بأنه أخبث صمتٍ ممكن.
/// </para>
/// <para>
/// ولاحظ ما ليس هنا: <b>لا رقم سطر</b>. الهوية عند الدفتر هي هوية المستند، فإن حمل
/// المستند الواحد أكثر من صنف واحد فذلك تصادمٌ يُرفض باسمه لا يُلتفّ عليه بحقل إضافي
/// يجعل الدفترين يعدّان بحبيبيّتين مختلفتين (‏فخ-48).
/// </para>
/// </summary>
/// <param name="Module">الوحدة المالكة للمستند.</param>
/// <param name="DocumentType">نوع المستند داخل تلك الوحدة.</param>
/// <param name="DocumentId">معرّف المستند داخل تلك الوحدة.</param>
/// <param name="TriggerCode">رمز الإطلاق كما يعرفه محرك الترحيل.</param>
/// <param name="Generation">جيل الترحيل.</param>
/// <param name="EventCode">رمز الحدث في مصفوفة الترحيل.</param>
public sealed record InventoryMovementSource(
    BabelModule Module,
    string DocumentType,
    string DocumentId,
    string TriggerCode,
    int Generation,
    string EventCode);

/// <summary>الصنف في مستودعه — مفتاح رصيد التقييم.</summary>
/// <param name="ItemId">معرّف الصنف داخل وحدة المخزون.</param>
/// <param name="WarehouseId">المستودع — بُعد تحليلي إلزامي على مراقبة المخزون.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور عند المصفوفة.</param>
public sealed record InventoryItemLocation(string ItemId, string WarehouseId, string ItemGroup);

/// <summary>وارد إلى المخزون بتكلفته الفعلية.</summary>
public sealed record InventoryReceipt
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>الفاعل — معامِل إلزامي في كل نداء يعبر بوابة استحقاق (‏فخ-58).</summary>
    public required UserId Actor { get; init; }

    /// <summary>هوية المستند المصدر.</summary>
    public required InventoryMovementSource Source { get; init; }

    /// <summary>الصنف ومستودعه.</summary>
    public required InventoryItemLocation Location { get; init; }

    /// <summary>الكمية الواردة — موجبة دائماً.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>تكلفة الكمية الواردة كلّها — لا تكلفة الوحدة.</summary>
    public required Money Cost { get; init; }

    /// <summary>تاريخ الحركة الميلادي.</summary>
    public required DateOnly OccurredOn { get; init; }
}

/// <summary>صادر من المخزون — <b>بلا مبلغ</b>: التكلفة تُحسب هنا ولا تُملى.</summary>
public sealed record InventoryIssue
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>الفاعل.</summary>
    public required UserId Actor { get; init; }

    /// <summary>هوية المستند المصدر.</summary>
    public required InventoryMovementSource Source { get; init; }

    /// <summary>الصنف ومستودعه.</summary>
    public required InventoryItemLocation Location { get; init; }

    /// <summary>الكمية المنصرفة — موجبة دائماً.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>تاريخ الحركة الميلادي.</summary>
    public required DateOnly OccurredOn { get; init; }
}

/// <summary>
/// مرتجع بضاعة إلى المخزون — <b>بتكلفة الصرف الأصلي لا بتكلفة اليوم</b>، كما يقول
/// <c>sales.credit_note.cost_of_sales</c> نصّاً في المصفوفة.
/// </summary>
public sealed record InventoryReturn
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>الفاعل.</summary>
    public required UserId Actor { get; init; }

    /// <summary>هوية مستند المرتجع.</summary>
    public required InventoryMovementSource Source { get; init; }

    /// <summary>هوية الصرف الأصلي الذي يُرَدّ عليه — لا تُخمَّن ولا تُترك فارغة.</summary>
    public required InventoryMovementSource OriginalIssue { get; init; }

    /// <summary>الكمية المرتجعة — موجبة دائماً، ولا تتجاوز كمية الصرف الأصلي.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>تاريخ الحركة الميلادي.</summary>
    public required DateOnly OccurredOn { get; init; }
}

/// <summary>
/// تكلفة حركة مخزون كما <b>حسبتها</b> وحدة المخزون.
/// <para>
/// <b>هذا النوع هو الجواب لا السؤال.</b> لا يبنيه إلا حدّ التقييم داخل
/// <c>Babel.Inventory</c>، ويحرس ذلك مسحُ مصدر في
/// <c>tests/Babel.ArchitectureTests/</c>: النوع لو بُني في وحدة أخرى لصارت تلك الوحدة
/// قادرة على أن تُملي على الدفتر رقماً اخترعته — وهو بالضبط ما كان قائماً قبل هذا
/// التسليم، حيث كان مستدعي <c>PostCostOfSalesAsync</c> يُسلّم المبلغ بنفسه.
/// </para>
/// </summary>
/// <param name="Cost">تكلفة الحركة بعملة الشركة.</param>
/// <param name="Method">رمز طريقة التقييم التي أنتجت الرقم.</param>
/// <param name="QuantityAfter">رصيد الكمية بعد الحركة.</param>
/// <param name="ValueAfter">رصيد القيمة بعد الحركة.</param>
/// <param name="DrewOnNegativeStock">هل صُرفت الكمية من رصيد لا يغطيها؟</param>
/// <param name="WasAlreadyRecorded">هل سبق تسجيل هذه الحركة بهويتها نفسها؟</param>
public sealed record InventoryMovementCost(
    Money Cost,
    string Method,
    decimal QuantityAfter,
    Money ValueAfter,
    bool DrewOnNegativeStock,
    bool WasAlreadyRecorded);

/// <summary>
/// حدّ تقييم المخزون: الجهة الوحيدة التي تُنتج <see cref="InventoryMovementCost"/>.
/// <para>
/// <b>موضعه في العقد لا في وحدة المخزون</b> لأن الوحدات الأفقية لا يعتمد بعضها على
/// بعض (القاعدة 3). فوحدة المبيعات ترى الواجهة وحدها، والجذر التركيبي يوصلها بالتنفيذ
/// — وهو الشكل نفسه المعتمد في <c>ICapturedInvoiceReceiver</c>.
/// </para>
/// </summary>
public interface IInventoryValuation
{
    /// <summary>يسجّل وارداً بتكلفته ويُعيد أثره على الرصيد.</summary>
    /// <param name="receipt">الوارد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<InventoryMovementCost>> ReceiveAsync(
        InventoryReceipt receipt, CancellationToken cancellationToken = default);

    /// <summary>يسجّل صادراً <b>ويحسب تكلفته</b> بطريقة التقييم المعتمدة.</summary>
    /// <param name="issue">الصادر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<InventoryMovementCost>> IssueAsync(
        InventoryIssue issue, CancellationToken cancellationToken = default);

    /// <summary>يسجّل مرتجعاً بتكلفة صرفه الأصلي.</summary>
    /// <param name="movement">المرتجع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<InventoryMovementCost>> ReturnAsync(
        InventoryReturn movement, CancellationToken cancellationToken = default);
}
