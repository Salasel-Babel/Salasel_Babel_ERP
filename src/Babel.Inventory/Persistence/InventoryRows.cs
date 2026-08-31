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

    /// <summary>
    /// الموقع داخل المستودع — <b>بُعد في المفتاح لا وصف على الصفّ</b>.
    /// <para>
    /// وقيمته اليوم <c>DEFAULT</c> في كل حركة، لأن التسكين لم يُبنَ بعد. ودخولُه الآن
    /// هو الفرق بين إضافة عمود غداً وبين هجرةٍ تُعيد حساب كل رصيد وتُعيد كتابة كل
    /// حركة — وهي هجرةٌ لا يقبلها دفترٌ يُضاف إليه فقط.
    /// </para>
    /// </summary>
    public string LocationId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = string.Empty;

    public string Direction { get; set; } = MovementDirection.In;

    /// <summary>الكمية <b>بوحدة أساس الرصيد</b> — موجبة دائماً؛ الاتجاه في <see cref="Direction"/> لا في الإشارة.</summary>
    public decimal Quantity { get; set; }

    /// <summary>وحدة أساس الرصيد التي كُتبت بها <see cref="Quantity"/>.</summary>
    public string BaseUnit { get; set; } = string.Empty;

    /// <summary>الوحدة التي سلّمها المستدعي — تُحفظ كما وردت، ولا تُنسى بالتحويل.</summary>
    public string EnteredUnit { get; set; } = string.Empty;

    /// <summary>المقدار كما سلّمه المستدعي بوحدته، قبل التحويل إلى وحدة الأساس.</summary>
    public decimal EnteredMagnitude { get; set; }

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

    /// <summary>الموقع داخل المستودع — الضلع الرابع في مفتاح الرصيد.</summary>
    public string LocationId { get; set; } = string.Empty;

    /// <summary>
    /// وحدة الأساس التي يُمسَك بها هذا الرصيد — <b>تُثبَّت بأول حركة ولا تتغيّر</b>.
    /// <para>
    /// ورصيدٌ يتغيّر أساسه بعد أن كُتبت عليه حركات هو رصيدٌ لا يُجمَع: مجموع حركاته
    /// يصير جمعاً لأعداد بمقاييس مختلفة. والوحدةُ الأخرى تدخل بمعامل تحويل، لا
    /// باستبدال الأساس.
    /// </para>
    /// </summary>
    public string BaseUnit { get; set; } = string.Empty;

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

/// <summary>
/// صنفٌ في كتالوج المستأجر: رمزه، واسمه بلغتين، ومجموعته، و<b>وحدة أساسه</b>.
/// <para>
/// <b>ولا رصيد هنا ولا تكلفة:</b> الصنف تعريفٌ، والرصيد واقعةٌ في
/// <see cref="ItemBalanceRow"/>. وخلطهما يجعل «احذف الصنف» سؤالاً يمسّ دفتراً.
/// </para>
/// </summary>
internal sealed class ItemRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>رمز الصنف داخل المستأجر — <b>هو المعرّف الذي تحمله الحركات والقيود</b>.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// الاسم العربي — <b>وهو السجلّ لا ترجمةٌ ثانية</b> (‏ADR-0021).
    /// <para>
    /// <b>ولا عمود للإنجليزية بجانبه:</b> الترجمات <b>صفوف</b> في
    /// <see cref="ItemTranslationRow"/> لا أعمدةٌ تُضاف لغةً بعد لغة (القاعدة 14).
    /// وعمودٌ ثابت للإنجليزية يجعل اللغة الثالثة هجرةَ مخطّط، ويجعل «لا ترجمة»
    /// و«ترجمةٌ فارغة» حالتين لا يُفرَّق بينهما.
    /// </para>
    /// </summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>مجموعة الصنف — مؤهّل الدور عند المصفوفة، لا رقم حساب.</summary>
    public string ItemGroup { get; set; } = string.Empty;

    /// <summary>وحدة الأساس: أصغر وحدة يُمسَك بها هذا الصنف، وإليها تُحوَّل البقية.</summary>
    public string BaseUnit { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// معامل تحويل وحدةٍ إلى وحدة أساس الصنف — <b>عددٌ نسبيّ دقيق: بسطٌ ومقام</b>.
/// <para>
/// <b>ولماذا لا عدد عشري واحد:</b> «الكرتون ثلث دستة» لا يُكتب عشرياً بلا خسارة،
/// و<c>0.333333</c> تضرب في الكمية فتُنتج كسراً يُقرَّب — والتقريب يدخل <b>القيمة</b>
/// لأن الكمية تُضرب في تكلفة الوحدة. والبسط والمقام يجعلان التحويل قابلاً للفحص:
/// إمّا أن يقع بلا باقٍ فيُقبل، أو يُرفض باسمه ولا يُقرَّب في الخفاء.
/// </para>
/// </summary>
internal sealed class ItemUnitRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>رمز الصنف.</summary>
    public string ItemCode { get; set; } = string.Empty;

    /// <summary>رمز الوحدة الأكبر (علبة · كرتون · دستة…).</summary>
    public string UnitCode { get; set; } = string.Empty;

    /// <summary>بسط المعامل: كم وحدةَ أساسٍ في <see cref="Denominator"/> من هذه الوحدة.</summary>
    public long Numerator { get; set; }

    /// <summary>مقام المعامل — موجب دائماً.</summary>
    public long Denominator { get; set; } = 1;
}

/// <summary>حالات مستند حركة المخزون.</summary>
internal static class StockDocumentState
{
    /// <summary>مسوّدة: لا حركة ولا قيد.</summary>
    public const string Draft = "DRAFT";

    /// <summary>مُرحَّل: حركة في الدفتر المساعد وقيدٌ في الدفتر.</summary>
    public const string Posted = "POSTED";
}

/// <summary>
/// <b>مستند حركة مخزون قائم بذاته</b> — تسوية جرد، أو رصيد افتتاحي، أو إعدام.
/// <para>
/// وهو <b>غير</b> حركة الاستلام من المشتريات ولا حركة الصرف من المبيعات: تلك مستنداتٌ
/// في وحدتيها، وحركتها أثرٌ لها. وهذا مستندُ المخزون نفسه، وحدثه في المصفوفة
/// <c>inventory.count_adjustment.posted</c> بسيناريوَيه — عجزٌ وزيادة.
/// </para>
/// <para>
/// <b>ومسوّدةٌ ثم ترحيل</b> بالشكل الذي يفرضه ADR-0044 على كل مستند: الإنشاء لا يمسّ
/// دفتراً، والترحيل موردٌ فرعي مستقلّ يُنشئ القيد والحركة معاً.
/// </para>
/// </summary>
internal sealed class StockDocumentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>رقم المستند — فريد داخل المستأجر.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>الاتجاه: <c>IN</c> زيادة و<c>OUT</c> عجز.</summary>
    public string Direction { get; set; } = MovementDirection.In;

    public string ItemCode { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    public string LocationId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = string.Empty;

    /// <summary>المقدار كما سُلّم، بوحدته.</summary>
    public decimal Magnitude { get; set; }

    /// <summary>وحدة المقدار المُسلَّم.</summary>
    public string UnitCode { get; set; } = string.Empty;

    /// <summary>
    /// تكلفة الكمية الواردة كلّها — <b>على الوارد وحده</b>. والصادر تُحسب تكلفته في
    /// الوحدة ولا تُملى (‏ADR-0039)، فيبقى هذا الحقل صفراً عليه.
    /// </summary>
    public decimal CostAmount { get; set; }

    public DateOnly OccurredOn { get; set; }

    public string State { get; set; } = StockDocumentState.Draft;

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
}

/// <summary>حالة محاولة ترحيل مستند مخزون.</summary>
internal static class InventoryPostingAttemptState
{
    public const string Attempting = "ATTEMPTING";

    public const string Posted = "POSTED";

    public const string Refused = "REFUSED";
}

/// <summary>
/// سجلّ محاولة ترحيل — <b>يُكتب قبل النداء ويُغلق بعده</b>، فالرفض يترك المستند على
/// حاله ومعه سببٌ مكتوب. نظير <c>sales.document_posting</c> حرفاً بحرف.
/// </summary>
internal sealed class InventoryPostingRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string TriggerCode { get; set; } = string.Empty;

    public int Generation { get; set; } = 1;

    /// <summary>رمز الحدث — مكوّن أصيل في الهوية لا وصفٌ للقيد (‏ADR-0016).</summary>
    public string EventCode { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string PartyId { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public string State { get; set; } = InventoryPostingAttemptState.Attempting;

    public Guid? EntryId { get; set; }

    public long EntryNumber { get; set; }

    public int AttemptCount { get; set; }

    public DateTime LastAttemptAt { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessageAr { get; set; } = string.Empty;

    public string FailureMessageEn { get; set; } = string.Empty;
}

/// <summary>
/// ترجمة اسم صنف — <b>صفٌّ لا عمود</b> (‏ADR-0021 · القاعدة 14).
/// <para>
/// اللغة الثالثة تُضاف بصفٍّ لا بهجرة مخطّط، و«لا ترجمة» تُقرأ من <b>غياب الصفّ</b>
/// لا من نصٍّ فارغ في عمود.
/// </para>
/// </summary>
internal sealed class ItemTranslationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>رمز الصنف المُترجَم اسمه.</summary>
    public string ItemCode { get; set; } = string.Empty;

    /// <summary>رمز اللغة بصيغة BCP-47 المختصرة: <c>en</c>، <c>ur</c>…</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>النصّ المُترجَم.</summary>
    public string Text { get; set; } = string.Empty;
}
