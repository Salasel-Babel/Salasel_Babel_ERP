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

/// <summary>
/// مستويات هرم التسكين — <b>ثلاثة لا تزيد، وكلٌّ يعرف أباه بمستواه</b>.
/// <para>
/// والمستوى عمودٌ صريح لا استنتاجٌ من وجود الأب: صفٌّ بلا أب قد يكون مستودعاً، وقد
/// يكون موقعاً فقد أباه بخطأ كتابة. والعمود يجعل الفرق بينهما مقروءاً في الصفّ نفسه.
/// </para>
/// <para>
/// <b>ولا دورات ممكنة بالبناء:</b> أب الصفّ مستواه <b>المستوى السابق حتماً</b>، فلا
/// موقع يصير أباً لمستودع، ولا رفٌّ يصير أباً لنفسه. وشجرةٌ حرّة المستويات كانت
/// ستحتاج فحص دورات على كل كتابة — وفحصٌ يُنسى مرّةً يُنتج سجلّاً لا يُجتاز.
/// </para>
/// </summary>
internal static class PlacementLevel
{
    /// <summary>المستودع — أعلى الهرم، ولا أب له.</summary>
    public const string Warehouse = "WAREHOUSE";

    /// <summary>الموقع داخل المستودع — <b>وهو مستوى الرصيد المُقيَّم</b> (‏ADR تسكين المخزون).</summary>
    public const string Location = "LOCATION";

    /// <summary>الرفّ أو الحاوية داخل الموقع — بُعد تسكينٍ لا بُعد تقييم.</summary>
    public const string Bin = "BIN";

    /// <summary>المستوى الأب لمستوىً معلوم — نصٌّ فارغ للمستودع.</summary>
    /// <param name="level">المستوى.</param>
    public static string ParentOf(string level) => level switch
    {
        Location => Warehouse,
        Bin => Location,
        _ => string.Empty,
    };
}

/// <summary>
/// موضعٌ في هرم التسكين: مستودعٌ أو موقعٌ أو رفّ — <b>جدولٌ واحد بمستوىً صريح</b>.
/// <para>
/// <b>ولماذا جدولٌ واحد لا ثلاثة:</b> المستويات الثلاثة تحمل الصفات نفسها حرفاً بحرف
/// — رمزٌ واسمٌ وأبٌ وحالةُ عمل — وتخضع للعمليات نفسها. وثلاثة جداول كانت ستكون ثلاث
/// نسخ من خمسة أعمدة وخمس عمليات، تنحرف إحداها عن أختيها عند أول تعديل. والمستوى
/// عمودٌ في المفتاح الفريد، فرمزُ «A1» يجوز أن يكون مستودعاً وموقعاً معاً بلا تصادم.
/// </para>
/// <para>
/// <b>ولا مفتاح خارجي إلى الأب:</b> الأب يُقرأ برمزه ومستواه، والتحقّق في الخدمة.
/// ومفتاحٌ خارجي كان سيمنع تسجيل موقعٍ قبل مستودعه — وهو ترتيبٌ لا يفرضه العمل —
/// ولم يكن ليمنع الحالة الوحيدة التي تهمّ: أبٌ مُعطَّل.
/// </para>
/// </summary>
internal sealed class StoragePlaceRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>المستوى: <c>WAREHOUSE</c> · <c>LOCATION</c> · <c>BIN</c>.</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// رمز الموضع داخل مستواه — <b>وهو ما تحمله الحركة والرصيد</b>، لا المعرّف.
    /// <para>
    /// والحركات القائمة تحمل رموزاً حرّة كُتبت قبل وجود هذا السجلّ (‏<c>DEFAULT</c>
    /// وغيره)، فالسجلّ <b>يصف</b> ولا يُبطل: رمزٌ غير مسجَّل يبقى عاملاً ويُوسَم عند
    /// القراءة، ولا تُعاد كتابة حركةٍ مضت لتوافق سجلّاً وُلد بعدها.
    /// </para>
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>رمز الأب — نصّ فارغ للمستودع، ورمز المستودع للموقع، ورمز الموقع للرفّ.</summary>
    public string ParentCode { get; set; } = string.Empty;

    /// <summary>الاسم العربي — <b>وهو السجلّ لا ترجمةٌ ثانية</b> (‏ADR-0021 · القاعدة 14).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// هل الموضع عامل؟ <b>والتعطيل حالةٌ لا حذف</b>: الرمز محمولٌ على حركات مضت،
    /// وحذفُه يجعل كل حركة عليه بلا موضع يُقرأ.
    /// </summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}

/// <summary>ترجمة اسم موضعٍ في هرم التسكين — <b>صفٌّ لا عمود</b> (‏ADR-0021 · القاعدة 14).</summary>
internal sealed class StoragePlaceTranslationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>مستوى الموضع المُترجَم اسمه — في المفتاح لأن الرمز وحده لا يميّز.</summary>
    public string Level { get; set; } = string.Empty;

    /// <summary>رمز الموضع.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>رمز اللغة بصيغة BCP-47 المختصرة.</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>النصّ المُترجَم.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// <b>مستند نقلٍ بين موقعين</b> — يُنشأ مسوّدةً ثم يُنفَّذ، كأي مستند في هذا المستودع.
/// <para>
/// <b>ولا يُرحَّل إلى دفتر الأستاذ إطلاقاً:</b> النقل داخل المنشأة نفسها لا يُغيّر
/// قيمة المخزون، والصنف واحدٌ على الطرفين فمجموعته واحدة، فمؤهّل دور
/// <c>inventory_control</c> واحدٌ على الطرفين، فالقيد الذي كان سيُكتب مدينٌ ودائنٌ على
/// <b>الحساب نفسه بالمبلغ نفسه</b> — أي لا شيء، مكتوباً في دفترٍ يُضاف إليه ولا
/// يُحذف منه. والمصفوفة نفسها تقول هذا في شرط
/// <c>inventory.transfer.between_warehouses</c>: «وإلا فلا قيد مالي إطلاقاً».
/// </para>
/// <para>
/// <b>وحركتان لا حركة واحدة:</b> صادرٌ من المصدر ووارد إلى الوجهة. ورصيدُ الموقع
/// مفتاحٌ مستقلّ، فحركةٌ واحدة «تنقل» كانت ستحتاج أن تُنقص مفتاحاً وتزيد آخر في صفٍّ
/// واحد — وهو ما لا يصفه عمود <c>Direction</c> ولا يُجمَع في تقرير حركة.
/// </para>
/// </summary>
internal sealed class StockTransferRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>رقم المستند — فريد داخل المستأجر.</summary>
    public string Number { get; set; } = string.Empty;

    public string ItemCode { get; set; } = string.Empty;

    /// <summary>مجموعة الصنف — مؤهّل الدور، وهي <b>واحدة على الطرفين</b> لأن الصنف واحد.</summary>
    public string ItemGroup { get; set; } = string.Empty;

    public string FromWarehouseId { get; set; } = string.Empty;

    public string FromLocationId { get; set; } = string.Empty;

    public string ToWarehouseId { get; set; } = string.Empty;

    public string ToLocationId { get; set; } = string.Empty;

    /// <summary>المقدار كما سُلّم، بوحدته.</summary>
    public decimal Magnitude { get; set; }

    /// <summary>وحدة المقدار المُسلَّم — <b>تُحفظ كما وردت</b> ولا تُنسى بالتحويل.</summary>
    public string UnitCode { get; set; } = string.Empty;

    /// <summary>
    /// قيمة المنقول كما حسبها الدفتر المساعد بتكلفة المصدر لحظة النقل.
    /// <para>
    /// <b>وهي تُقرأ ولا تُملى</b>، ولا تصل إلى دفتر الأستاذ: وجودها هنا كي يُقرأ
    /// المستند بقيمته، لا كي يُبنى عليها قيد.
    /// </para>
    /// </summary>
    public decimal ValueAmount { get; set; }

    public DateOnly OccurredOn { get; set; }

    /// <summary>الحالة: <c>DRAFT</c> أو <c>MOVED</c>.</summary>
    public string State { get; set; } = StockTransferState.Draft;

    /// <summary>جيل التنفيذ — يدخل هوية الحركة كما يدخلها في كل مستند.</summary>
    public int MovementGeneration { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
}

/// <summary>حالات مستند النقل.</summary>
internal static class StockTransferState
{
    /// <summary>مسوّدة: لا حركة.</summary>
    public const string Draft = "DRAFT";

    /// <summary>
    /// مُنفَّذ: حركتان في الدفتر المساعد <b>ولا قيد</b>.
    /// <para>
    /// والاسم <c>MOVED</c> لا <c>POSTED</c> عمداً: <c>POSTED</c> في هذا المستودع تعني
    /// «صار له قيدٌ في الدفتر»، وحالةٌ تحمل الاسم بلا قيد كانت ستجعل كل قارئ يبحث عن
    /// قيدٍ لا وجود له.
    /// </para>
    /// </summary>
    public const string Moved = "MOVED";
}

/// <summary>
/// أصناف الكمّية — <b>وهي ما يجعل التحويل ممكناً أو مستحيلاً</b>.
/// <para>
/// معامل التحويل بين وحدتين من صنفٍ واحد <b>واقعةٌ فيزيائية</b>: الكيلوغرام ألف غرام
/// دائماً وفي كل مكان. وبين صنفين مختلفين <b>ليس معاملاً بل كثافة</b>: «كم كيلوغراماً في
/// اللتر؟» سؤالٌ جوابه يختلف بين الماء والزيت والرصاص، ويختلف للمادّة الواحدة بالحرارة.
/// فالتحويل بين صنفين <b>يُرفض باسمه</b>، ولا يُقبل بمعاملٍ يبدو ثابتاً وهو خاصّيةُ مادّة.
/// </para>
/// </summary>
internal static class QuantityClass
{
    /// <summary>عدد: حبّة · علبة · كرتون · طبلية.</summary>
    public const string Count = "COUNT";

    /// <summary>وزن: غرام · كيلوغرام · طنّ.</summary>
    public const string Weight = "WEIGHT";

    /// <summary>حجم: مليلتر · لتر · متر مكعّب.</summary>
    public const string Volume = "VOLUME";

    /// <summary>طول: مليمتر · متر · كيلومتر.</summary>
    public const string Length = "LENGTH";

    /// <summary>مساحة: متر مربّع.</summary>
    public const string Area = "AREA";

    /// <summary>الأصناف الخمسة — <b>مغلقة</b>: صنفٌ سادس يدخل بهجرة لا بقيمةٍ حرّة.</summary>
    public static IReadOnlyList<string> All { get; } = [Count, Weight, Volume, Length, Area];
}

/// <summary>
/// <b>وحدة قياس مسجَّلة</b>: رمزها، واسمها، و<b>صنف كمّيتها</b>.
/// <para>
/// <b>ولماذا صنف الكمّية عمودٌ إلزاميّ لا وصفٌ اختياري:</b> هو الحقل الوحيد الذي يجعل
/// «كيلوغرام ← متر» <b>خطأً يُرفض</b> بدل أن يكون معاملاً يكتبه أحدهم بحسن نيّة. وبدونه
/// لا يملك النظام ما يفرّق به بين تحويلٍ فيزيائي وتقديرٍ مادّي.
/// </para>
/// <para>
/// <b>وهذا سجلٌّ يصف ولا يُبطل</b>، كسجلّ التسكين حرفاً بحرف: الحركات القائمة تحمل رموز
/// وحدات كُتبت قبل وجوده (‏<c>EACH</c> وغيرها، هجرة 001)، ولا مفتاح خارجي منها إليه.
/// فرمزٌ غير مسجَّل يبقى عاملاً — <b>ويبقى معه أنّ خلط وحدتين بلا معامل يُرفض</b>.
/// </para>
/// </summary>
internal sealed class UnitOfMeasureRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>رمز الوحدة — <b>هوية تحملها كل حركة</b>، لا نصّاً معروضاً.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم العربي — <b>وهو السجلّ</b> (‏ADR-0021 · القاعدة 14).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>صنف الكمّية: عدد · وزن · حجم · طول · مساحة.</summary>
    public string Class { get; set; } = QuantityClass.Count;

    /// <summary>هل هي عاملة؟ والتعطيل حالةٌ لا حذف: الرمز محمولٌ على حركات مضت.</summary>
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; }
}

/// <summary>ترجمة اسم وحدة قياس — <b>صفٌّ لا عمود</b> (‏ADR-0021 · القاعدة 14).</summary>
internal sealed class UnitOfMeasureTranslationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string UnitCode { get; set; } = string.Empty;

    public string Locale { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// معامل تحويل بين وحدتين <b>على مستوى المنشأة</b> — لا على مستوى الصنف.
/// <para>
/// <b>والفرق بينه وبين <see cref="ItemUnitRow"/> مقصود، ولا يُدمَجان:</b>
/// «الكيلوغرام ألف غرام» واقعةٌ لا تخصّ صنفاً، تُكتب مرّةً وتصلح للجميع. أمّا «الكرتون
/// اثنتا عشرة حبّة» فهي <b>خاصّية تعبئةٍ لصنفٍ بعينه</b> — كرتون هذا الصنف اثنتا عشرة
/// وكرتون ذاك أربع وعشرون. ودمجُهما في جدولٍ واحد يعني إمّا أن تُكرَّر الواقعة الفيزيائية
/// على كل صنف، أو أن تُعمَّم خاصّية التعبئة على الأصناف كلّها.
/// </para>
/// <para>
/// <b>والمعامل بسطٌ ومقام</b> للسبب نفسه الذي جعله كذلك على الصنف: «الحبّة ثلث علبة» لا
/// يُكتب عشرياً بلا خسارة، والخسارة في كمّيةٍ تُضرب في تكلفة الوحدة تصل إلى المال.
/// </para>
/// </summary>
internal sealed class UnitConversionRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>الوحدة المُحوَّل منها.</summary>
    public string FromUnit { get; set; } = string.Empty;

    /// <summary>الوحدة المُحوَّل إليها.</summary>
    public string ToUnit { get; set; } = string.Empty;

    /// <summary>البسط: كم وحدةً من <see cref="ToUnit"/> في <see cref="Denominator"/> من <see cref="FromUnit"/>.</summary>
    public long Numerator { get; set; }

    /// <summary>المقام — موجب دائماً.</summary>
    public long Denominator { get; set; } = 1;

    public DateTime CreatedAt { get; set; }
}
