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

/// <summary>
/// <b>كمّية بوحدتها — ولا كمّية مجرّدة في هذا النظام إطلاقاً.</b>
/// <para>
/// «عشرة» ليست معلومة: عشر حبّات أم عشر كراتين؟ والفرق بينهما في دفترٍ يمسك قيمةً هو
/// الفرق بين رقمٍ صحيح ورقمٍ أكبر منه اثني عشر ضعفاً — <b>ولا يُظهره توازنٌ ولا سلسلة</b>،
/// لأن القيد المبنيّ عليه متوازن تماماً. ولذلك تعبر الكمّية هذا الحدّ ومعها وحدتها
/// دائماً، ولا يوجد في العقد موضعٌ يقبل <c>decimal</c> عارياً بوصفه كمّية.
/// </para>
/// <para>
/// <b>و<c>Magnitude</c> لا <c>Amount</c>:</b> الاسم الثاني كلمةُ مبلغ في حارس
/// <c>InventoryValuationIsTheOnlySourceOfCostOfSales</c>، وحقلٌ عشري يحمله يُقرأ مبلغاً
/// فيُرفض. والرفض في محلّه: <b>هذه ليست مبلغاً</b>، وتسميتها باسم المبلغ تُضعف حارساً
/// وُجد ليمنع المبالغ المُملاة.
/// </para>
/// </summary>
/// <param name="Magnitude">المقدار العددي — بوحدة <paramref name="Unit"/> لا بغيرها.</param>
/// <param name="Unit">
/// رمز وحدة القياس كما سجّله المستأجر (حبّة · علبة · كرتون · كجم…). معرّف لا نصّ معروض:
/// لا يُترجَم ولا يُطابَق بلا حساسية حالة.
/// </param>
public sealed record InventoryQuantity(decimal Magnitude, string Unit);

/// <summary>وحدات القياس المعلومة للمنتج قبل أن يسجّل المستأجر وحداته.</summary>
public static class InventoryUnits
{
    /// <summary>
    /// وحدة القياس الافتراضية: الوحدة المعدودة.
    /// <para>
    /// <b>وهي رمزٌ صريح لا غياب.</b> مستندٌ لا يذكر وحدته يُكتب بهذه، فيُقرأ من الصفّ
    /// أنه مُمسَك بالعدّ — لا أن وحدته مجهولة. والفرق بينهما هو الفرق بين رصيدٍ يُجمَع
    /// ورصيدٍ يُجمَع ولا يُدرى بأي مقياس.
    /// </para>
    /// </summary>
    public const string Each = "EACH";
}

/// <summary>
/// المواقع الافتراضية داخل المستودع.
/// <para>
/// <b>الموقع بُعدٌ في المفتاح منذ اليوم، ولو لم يُسكَّن شيء بعد.</b> إضافة بُعدٍ إلى
/// مفتاح رصيدٍ قائم هجرةٌ تُعيد حساب كل رصيد وتُعيد كتابة كل حركة — وهي بالضبط
/// الهجرة التي يمنعها <c>ADR-0002</c> على الدفتر المساعد، لأن الصفّ الذي يُعاد
/// حسابه لا يُطابَق بمستنداته بعد ذلك. فالبُعد يدخل الآن بقيمةٍ واحدة، ويتفرّع لاحقاً
/// بلا أن يُمسّ صفٌّ مضى.
/// </para>
/// </summary>
public static class InventoryLocations
{
    /// <summary>
    /// الموقع الافتراضي في كل مستودع — «المستودع كلّه موقعٌ واحد».
    /// <para>
    /// وهو قيمة صريحة يكتبها المستدعي، <b>لا افتراضٌ صامت في التوقيع</b>: مستدعٍ لا
    /// يعرف أنه يُسكِّن في موقعٍ افتراضي سيكتشف ذلك يوم يصير للمستودع مواقع.
    /// </para>
    /// </summary>
    public const string Default = "DEFAULT";
}

/// <summary>
/// الصنف في مستودعه وموقعه — <b>مفتاح رصيد التقييم بأبعاده الأربعة</b>.
/// </summary>
/// <param name="ItemId">معرّف الصنف داخل وحدة المخزون.</param>
/// <param name="WarehouseId">المستودع — بُعد تحليلي إلزامي على مراقبة المخزون.</param>
/// <param name="LocationId">
/// الموقع داخل المستودع (التسكين). جزءٌ من المفتاح لا وصفٌ عليه — انظر
/// <see cref="InventoryLocations"/> لسبب دخوله اليوم.
/// </param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور عند المصفوفة.</param>
public sealed record InventoryItemLocation(
    string ItemId, string WarehouseId, string LocationId, string ItemGroup);

/// <summary>وارد إلى المخزون بتكلفته الفعلية.</summary>
public sealed record InventoryReceipt
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>الفاعل — معامِل إلزامي في كل نداء يعبر بوابة استحقاق (‏فخ-58).</summary>
    public required UserId Actor { get; init; }

    /// <summary>هوية المستند المصدر.</summary>
    public required InventoryMovementSource Source { get; init; }

    /// <summary>الصنف ومستودعه وموقعه.</summary>
    public required InventoryItemLocation Location { get; init; }

    /// <summary>الكمية الواردة بوحدتها — موجبة دائماً.</summary>
    public required InventoryQuantity Quantity { get; init; }

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

    /// <summary>الصنف ومستودعه وموقعه.</summary>
    public required InventoryItemLocation Location { get; init; }

    /// <summary>الكمية المنصرفة بوحدتها — موجبة دائماً.</summary>
    public required InventoryQuantity Quantity { get; init; }

    /// <summary>تاريخ الحركة الميلادي.</summary>
    public required DateOnly OccurredOn { get; init; }
}

/// <summary>
/// مرتجع على حركة مخزون سابقة — <b>بتكلفتها هي لا بتكلفة اليوم</b>، كما يقول
/// <c>sales.credit_note.cost_of_sales</c> نصّاً في المصفوفة.
/// <para>
/// <b>واتجاهه عكس اتجاه أصله ولا يُسلّمه المستدعي:</b> مرتجعٌ على صرفٍ بضاعةٌ تعود إلى
/// المستودع، ومرتجعٌ على استلامٍ بضاعةٌ تخرج منه إلى المورد. والوحدة تقرأ اتجاه الأصل
/// من الحركة المُسجَّلة — فلا يستطيع مستدعٍ أن يُدخل بضاعةً باسم إخراجها.
/// </para>
/// </summary>
public sealed record InventoryReturn
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>الفاعل.</summary>
    public required UserId Actor { get; init; }

    /// <summary>هوية مستند المرتجع.</summary>
    public required InventoryMovementSource Source { get; init; }

    /// <summary>
    /// هوية الحركة الأصلية التي يُرَدّ عليها — صرفاً كانت أو استلاماً. لا تُخمَّن ولا
    /// تُترك فارغة: منها يُقرأ الموضع والوحدة والتكلفة <b>والاتجاه</b>.
    /// </summary>
    public required InventoryMovementSource OriginalMovement { get; init; }

    /// <summary>الكمية المرتجعة بوحدتها — موجبة، ولا تتجاوز كمية الحركة الأصلية.</summary>
    public required InventoryQuantity Quantity { get; init; }

    /// <summary>تاريخ الحركة الميلادي.</summary>
    public required DateOnly OccurredOn { get; init; }
}

/// <summary>
/// إلغاء حركة مخزون مُسجَّلة — <b>أثرُ عكسِ مستندها في الدفتر، مرويّاً في الدفتر المساعد</b>.
/// <para>
/// <b>ولاحظ ما ليس فيه: كمّية.</b> العكس ليس ردّاً يختار المستدعي مقداره؛ هو إبطال
/// واقعةٍ مُسجَّلة بكاملها. فالكمّية والقيمة تُقرآن من الحركة الملغاة نفسها، ولا يُسلّمهما
/// أحد — وهو المبدأ نفسه الذي جعل تكلفة المبيعات تُحسب ولا تُملى (‏ADR-0039).
/// </para>
/// <para>
/// و<see cref="Source"/> هو <b>هوية قيد العكس حرفاً بحرف</b>: هوية الأصل ورمزُ إطلاقها
/// مسبوقاً بـ<c>REVERSAL:</c> من
/// <see cref="Babel.Contracts.Posting.ReversalIdentity"/>. وبذلك يُجمَع الطرفان — الحركة
/// والقيد — تحت مفتاح المستند نفسه، فيصير صافي المطابقة صفراً بالبناء لا بالمصادفة.
/// </para>
/// </summary>
public sealed record InventoryMovementReversal
{
    /// <summary>المستأجر.</summary>
    public required TenantId Tenant { get; init; }

    /// <summary>الفاعل.</summary>
    public required UserId Actor { get; init; }

    /// <summary>هوية حركة العكس نفسها.</summary>
    public required InventoryMovementSource Source { get; init; }

    /// <summary>هوية الحركة المُلغاة — لا تُخمَّن ولا تُترك فارغة.</summary>
    public required InventoryMovementSource ReversedMovement { get; init; }

    /// <summary>تاريخ حركة العكس الميلادي.</summary>
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
/// <param name="Location">
/// الصنف ومستودعه وموقعه ومجموعته <b>كما استقرّت في الحركة المُسجَّلة</b>.
/// <para>
/// وهي معلومة على الوارد والصادر لأن المستدعي سلّمها؛ أمّا على <b>المرتجع</b> فلا
/// يعرفها إلا المخزون: المستدعي يُسلّم هوية الصرف الأصلي وحدها، والصنف والمستودع
/// يُقرآن من تلك الحركة. وبدون إعادتها هنا كان على وحدة المبيعات أن تُسمّي الصنف
/// في وقائع القيد <b>من عندها</b> — فتُنتج قيداً يقول «الصنف س» ودفتراً مساعداً
/// حرّك «الصنف ص»، وهو انحراف لا يُظهره توازنٌ ولا سلسلة.
/// </para>
/// </param>
/// <param name="Quantity">
/// ما تحرّك فعلاً <b>بوحدة أساس الرصيد</b> — لا بالوحدة التي سُلّمت بها. مَن سلّم
/// كرتوناً يقرأ هنا اثنتي عشرة حبّة، ويعرف بأي وحدة يُمسك الدفترُ رصيدَه.
/// </param>
/// <param name="QuantityAfter">رصيد الكمية بعد الحركة، بوحدة الأساس نفسها.</param>
/// <param name="ValueAfter">رصيد القيمة بعد الحركة.</param>
/// <param name="DrewOnNegativeStock">هل صُرفت الكمية من رصيد لا يغطيها؟</param>
/// <param name="WasAlreadyRecorded">هل سبق تسجيل هذه الحركة بهويتها نفسها؟</param>
public sealed record InventoryMovementCost(
    Money Cost,
    string Method,
    InventoryItemLocation Location,
    InventoryQuantity Quantity,
    InventoryQuantity QuantityAfter,
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

    /// <summary>
    /// يسجّل مرتجعاً بتكلفة حركته الأصلية، و<b>باتجاهٍ معاكسٍ لها</b> يُقرأ منها.
    /// </summary>
    /// <param name="movement">المرتجع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<InventoryMovementCost>> ReturnAsync(
        InventoryReturn movement, CancellationToken cancellationToken = default);

    /// <summary>
    /// يُلغي حركة مُسجَّلة <b>بكاملها وبقيمتها هي</b> — لا بقيمة اليوم ولا بكمية يختارها
    /// المستدعي. وهو ما يقابل عكسَ قيدٍ في الدفتر، ويُكتب بهويته نفسها.
    /// </summary>
    /// <param name="movement">طلب الإلغاء: هويته وهوية ما يُلغيه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<InventoryMovementCost>> ReverseMovementAsync(
        InventoryMovementReversal movement, CancellationToken cancellationToken = default);

    /// <summary>
    /// يقرأ حركةً مُسجَّلة بهويتها: <b>موضعها، وكمّيتها بوحدة أساسها، وقيمتها</b>.
    /// <para>
    /// <b>ولماذا هي في العقد لا في الوحدة وحدها:</b> مَن يردّ بضاعةً على صرفٍ سابق
    /// يحتاج أن يعرف <b>بأي وحدة كان ذلك الصرف</b> — و«عشرة» بلا وحدة ليست معلومة.
    /// والوحدة المستدعية لا تملك الجواب: الرصيد وأساسه مِلكُ المخزون. فبدون هذه
    /// القراءة كان على المبيعات أن <b>تخترع</b> وحدةً للمرتجع، وهو الشكل نفسه الذي
    /// كانت تخترع به مبلغ تكلفة المبيعات قبل ADR-0039.
    /// </para>
    /// <para>ونقطة قراءة: تعمل والاشتراك «للقراءة فقط».</para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="source">هوية الحركة المطلوبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    ValueTask<Result<InventoryMovementCost>> ReadMovementAsync(
        TenantId tenant,
        UserId actor,
        InventoryMovementSource source,
        CancellationToken cancellationToken = default);
}
