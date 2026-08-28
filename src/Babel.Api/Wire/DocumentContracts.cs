namespace Babel.Api.Wire;

/// <summary>
/// طلب تسجيل طرف — عميل أو مورد. <b>ولا حقل مستأجر ولا حقل شركة فيه</b>: النطاق من
/// الاعتماد ومن المسار، وحقلٌ في الجسم اسمه <c>tenantId</c> كان سيصير أول ثغرة عبور
/// بين المستأجرين. وأي حقل غير معروف يُرفض الطلب كلّه بسببه.
/// </summary>
internal sealed record PartyRequestDto
{
    /// <summary>رمز الطرف داخل المستأجر — هوية يحملها تاريخه المُرحَّل، لا نصّ معروض.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم ثنائي اللغة. الطرفان إلزاميان: العربية سجلّ لا ترجمة ثانية.</summary>
    public required LocalizedTextDto Name { get; init; }

    /// <summary>حدّ الائتمان أو سقف الالتزام — <b>نصّاً</b> لا رمزاً رقمياً.</summary>
    public required WireDecimal CreditLimit { get; init; }

    /// <summary>مهلة السداد بالأيام.</summary>
    public required int PaymentTermsDays { get; init; }

    /// <summary>رقم التسجيل الضريبي — على المورد وحده، وغيابه واقع لا نقص.</summary>
    public string? VatNumber { get; init; }
}

/// <summary>طرف كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف الذي تُبنى عليه المستندات.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="CreditLimit">الحدّ نصّاً بمقياس أربع خانات.</param>
/// <param name="PaymentTermsDays">مهلة السداد.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي، أو فراغ على من لا رقم له، أو <c>null</c> على العميل.</param>
internal sealed record PartyDto(
    string Id,
    string Code,
    LocalizedTextDto Name,
    string CreditLimit,
    int PaymentTermsDays,
    string? VatNumber);

/// <summary>
/// سطر مستند مبيعات على السلك.
/// <para>
/// <b>ولاحظ ما ليس هنا: لا حساب ولا رمز حساب</b> — القاعدة 2 مطبَّقة على السلك أيضاً.
/// السطر يحمل مؤهّل دور (<c>itemGroup</c>)، والمصفوفة وحدها تُحوّله إلى حساب.
/// </para>
/// </summary>
internal sealed record SalesLineDto
{
    /// <summary>مجموعة الصنف — مؤهّل الدور.</summary>
    public required string ItemGroup { get; init; }

    /// <summary>بيان السطر ثنائي اللغة.</summary>
    public required LocalizedTextDto Description { get; init; }

    /// <summary>الكمية نصّاً.</summary>
    public required WireDecimal Quantity { get; init; }

    /// <summary>سعر الوحدة نصّاً.</summary>
    public required WireDecimal UnitPrice { get; init; }

    /// <summary>خصم السطر نصّاً.</summary>
    public required WireDecimal Discount { get; init; }

    /// <summary>التصنيف الضريبي: <c>standard</c> · <c>zero</c> · <c>exempt</c>.</summary>
    public required string TaxClassification { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً نصّاً.</summary>
    public required WireDecimal TaxRate { get; init; }

    /// <summary>
    /// على سطر الإشعار الدائن وحده: سطر الفاتورة الذي تُردّ بضاعته. وغيابه يعني
    /// <b>تخفيض قيمة لا ردّ بضاعة</b> — وهو فرقٌ تجاري لا يُخمَّن.
    /// </summary>
    public string? OriginalInvoiceLineId { get; init; }
}

/// <summary>طلب إنشاء فاتورة مبيعات <b>مسوّدة</b>. لا ترحيل ولا قيد: الترحيل مورد مستقلّ.</summary>
internal sealed record SalesInvoiceRequestDto
{
    /// <summary>رقم الفاتورة — فريد داخل المستأجر.</summary>
    public required string Number { get; init; }

    /// <summary>معرّف العميل.</summary>
    public required string CustomerId { get; init; }

    /// <summary>تاريخ الإصدار الميلادي بصيغة yyyy-MM-dd.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>الفرع — بُعد تحليلي إلزامي على الإيراد.</summary>
    public required string BranchId { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<SalesLineDto> Lines { get; init; }
}

/// <summary>
/// طلب إنشاء إشعار دائن <b>مسوّدة</b> على فاتورة مُرحَّلة.
/// <para>ولا عميل فيه: عميله عميل الفاتورة الأصلية، وإعادةُ ذكره تفتح باب انحراف.</para>
/// </summary>
internal sealed record CreditNoteRequestDto
{
    /// <summary>رقم الإشعار.</summary>
    public required string Number { get; init; }

    /// <summary>الفاتورة الأصلية — الإشعار لا يوجد بلا أصل.</summary>
    public required string InvoiceId { get; init; }

    /// <summary>تاريخ الإصدار الميلادي.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>سطور المرتجع أو التخفيض.</summary>
    public required IReadOnlyList<SalesLineDto> Lines { get; init; }
}

/// <summary>سطر فاتورة مصروف على السلك.</summary>
internal sealed record PurchaseLineDto
{
    /// <summary>الصنف أو البند في دفتره المساعد.</summary>
    public required string ItemId { get; init; }

    /// <summary>مجموعة الصنف — مؤهّل الدور.</summary>
    public required string ItemGroup { get; init; }

    /// <summary>البيان ثنائي اللغة.</summary>
    public required LocalizedTextDto Description { get; init; }

    /// <summary>الكمية نصّاً.</summary>
    public required WireDecimal Quantity { get; init; }

    /// <summary>سعر الوحدة نصّاً.</summary>
    public required WireDecimal UnitPrice { get; init; }

    /// <summary>التصنيف الضريبي.</summary>
    public required string TaxClassification { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً نصّاً.</summary>
    public required WireDecimal TaxRate { get; init; }

    /// <summary>هل ضريبة هذا السطر قابلة للاسترداد؟</summary>
    public required bool TaxRecoverable { get; init; }
}

/// <summary>طلب إنشاء فاتورة مصروف <b>مسوّدة</b> — بلا مخزون ولا مطابقة ثلاثية.</summary>
internal sealed record ExpenseBillRequestDto
{
    /// <summary>رقم الفاتورة.</summary>
    public required string Number { get; init; }

    /// <summary>معرّف المورد.</summary>
    public required string SupplierId { get; init; }

    /// <summary>تاريخ الفاتورة الميلادي.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>تصنيف المصروف — مؤهّل الدور.</summary>
    public required string ExpenseCategory { get; init; }

    /// <summary>مركز التكلفة — بُعد إلزامي على المصروف.</summary>
    public required string CostCenterId { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<PurchaseLineDto> Lines { get; init; }
}

/// <summary>
/// مستند تجاري كما يخرج على السلك — فاتورة مبيعات، أو إشعار دائن، أو فاتورة مورد.
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> · <c>POSTED</c> وما إليهما.</param>
/// <param name="Net">الصافي قبل الضريبة نصّاً.</param>
/// <param name="Tax">الضريبة نصّاً.</param>
/// <param name="Gross">الإجمالي شامل الضريبة نصّاً.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل، وإلا <c>null</c>.</param>
/// <param name="AlreadyPosted">
/// هل كانت هذه الهوية مُرحَّلة قبل هذا الطلب؟ <b>مُعلَن في الجسم كما في رمز الحالة</b>:
/// رمز الحالة وحده يضيع خلف أي وسيط يعيد التوجيه، وعميلٌ أعاد المحاولة بعد انقطاع شبكة
/// يحتاج أن يعرف أيّ النداءين رحّل.
/// </param>
internal sealed record CommercialDocumentDto(
    string Id,
    string Number,
    string State,
    string Net,
    string Tax,
    string Gross,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>شرائح أعمار الديون على السلك — كلها نصوص.</summary>
/// <param name="NotDue">لم يستحق بعد.</param>
/// <param name="Days1To30">متأخر 1–30 يوماً.</param>
/// <param name="Days31To60">متأخر 31–60 يوماً.</param>
/// <param name="Days61To90">متأخر 61–90 يوماً.</param>
/// <param name="Over90">متأخر أكثر من 90 يوماً.</param>
/// <param name="Total">المجموع — مجموع الشرائح بالضبط.</param>
internal sealed record AgingBandsDto(
    string NotDue,
    string Days1To30,
    string Days31To60,
    string Days61To90,
    string Over90,
    string Total);

/// <summary>أعمار ديون طرف واحد.</summary>
/// <param name="PartyId">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="Bands">الشرائح.</param>
internal sealed record AgingPartyDto(string PartyId, string Code, LocalizedTextDto Name, AgingBandsDto Bands);

/// <summary>تقرير أعمار الديون.</summary>
/// <param name="AsOf">تاريخ التقرير.</param>
/// <param name="Parties">الأطراف.</param>
/// <param name="Totals">المجاميع.</param>
internal sealed record AgingReportDto(string AsOf, IReadOnlyList<AgingPartyDto> Parties, AgingBandsDto Totals);

/// <summary>
/// سطر مستند مشتريات <b>مخزني</b> على السلك — ومعه وحدة قياسه.
/// <para>
/// <b>والفرق عن سطر المصروف هو الوحدة</b>: كمّية هذا السطر تصل إلى دفتر المخزون
/// فتُضرب في تكلفة الوحدة، و«عشرة» بلا وحدة ليست معلومة.
/// </para>
/// </summary>
internal sealed record StockLineDto
{
    /// <summary>رمز الصنف كما هو في كتالوج المخزون.</summary>
    public required string ItemId { get; init; }

    /// <summary>مجموعة الصنف — مؤهّل الدور.</summary>
    public required string ItemGroup { get; init; }

    /// <summary>البيان ثنائي اللغة.</summary>
    public required LocalizedTextDto Description { get; init; }

    /// <summary>الكمية نصّاً.</summary>
    public required WireDecimal Quantity { get; init; }

    /// <summary>رمز وحدة القياس.</summary>
    public required string Unit { get; init; }

    /// <summary>سعر الوحدة نصّاً.</summary>
    public required WireDecimal UnitPrice { get; init; }

    /// <summary>التصنيف الضريبي.</summary>
    public required string TaxClassification { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً نصّاً.</summary>
    public required WireDecimal TaxRate { get; init; }
}

/// <summary>طلب إنشاء أمر شراء.</summary>
internal sealed record PurchaseOrderRequestDto
{
    /// <summary>رقم الأمر.</summary>
    public required string Number { get; init; }

    /// <summary>معرّف المورد.</summary>
    public required string SupplierId { get; init; }

    /// <summary>تاريخ الأمر الميلادي.</summary>
    public required string OrderedOn { get; init; }

    /// <summary>المستودع المستقبِل.</summary>
    public required string WarehouseId { get; init; }

    /// <summary>مركز التكلفة.</summary>
    public required string CostCenterId { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<StockLineDto> Lines { get; init; }
}

/// <summary>سطر استلام على السلك: أي سطر أمر، وبأي كمية.</summary>
internal sealed record ReceiptLineDto
{
    /// <summary>معرّف سطر الأمر.</summary>
    public required string OrderLineId { get; init; }

    /// <summary>الكمية المستلمة نصّاً.</summary>
    public required WireDecimal Quantity { get; init; }
}

/// <summary>طلب تسجيل استلام بضاعة <b>مسوّدة</b>.</summary>
internal sealed record GoodsReceiptRequestDto
{
    /// <summary>رقم الاستلام.</summary>
    public required string Number { get; init; }

    /// <summary>أمر الشراء.</summary>
    public required string OrderId { get; init; }

    /// <summary>تاريخ الاستلام الميلادي.</summary>
    public required string ReceivedOn { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<ReceiptLineDto> Lines { get; init; }
}

/// <summary>سطر فاتورة مورد مخزنية على السلك — ضلع المطابقة الثالث.</summary>
internal sealed record StockBillLineDto
{
    /// <summary>معرّف سطر الاستلام.</summary>
    public required string ReceiptLineId { get; init; }

    /// <summary>الكمية المفوترة نصّاً.</summary>
    public required WireDecimal Quantity { get; init; }

    /// <summary>سعر الوحدة على الفاتورة نصّاً.</summary>
    public required WireDecimal UnitPrice { get; init; }

    /// <summary>التصنيف الضريبي.</summary>
    public required string TaxClassification { get; init; }

    /// <summary>نسبة الضريبة كسراً عشرياً نصّاً.</summary>
    public required WireDecimal TaxRate { get; init; }
}

/// <summary>طلب إنشاء فاتورة مورد <b>مخزنية</b> مسوّدة تُطابَق ثلاثياً.</summary>
internal sealed record StockBillRequestDto
{
    /// <summary>رقم الفاتورة.</summary>
    public required string Number { get; init; }

    /// <summary>الاستلام.</summary>
    public required string ReceiptId { get; init; }

    /// <summary>تاريخ الفاتورة الميلادي.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<StockBillLineDto> Lines { get; init; }
}

/// <summary>
/// طلب إنشاء <b>مرتجع مشتريات</b> مسوّدة.
/// <para>
/// <b>ولا صافي فيه:</b> المصفوفة تقول إن صافي المرتجع «بتكلفة الاستلام الأصلي لا
/// بتكلفة اليوم»، وتلك التكلفة يملكها دفتر المخزون وحده. فالطلب يحمل الكمّية.
/// </para>
/// </summary>
internal sealed record PurchaseReturnRequestDto
{
    /// <summary>رقم المرتجع.</summary>
    public required string Number { get; init; }

    /// <summary>الفاتورة المخزنية الأصلية.</summary>
    public required string BillId { get; init; }

    /// <summary>سطر الاستلام الذي تُردّ بضاعته — به يُقيَّم المرتجع.</summary>
    public required string ReceiptLineId { get; init; }

    /// <summary>تاريخ المرتجع الميلادي.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>الكمية المرتجعة نصّاً بوحدة الاستلام.</summary>
    public required WireDecimal Quantity { get; init; }

    /// <summary>ضريبة المرتجع نصّاً — بتصنيف الفاتورة الأصلية.</summary>
    public required WireDecimal Tax { get; init; }
}

/// <summary>سطر مستند مشتريات كما يخرج على السلك — معرّفه مدخل المستند التالي.</summary>
/// <param name="Id">معرّف السطر.</param>
/// <param name="LineNo">رقمه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="Quantity">الكمية نصّاً.</param>
/// <param name="Unit">وحدة القياس.</param>
/// <param name="UnitPrice">سعر الوحدة نصّاً.</param>
internal sealed record PurchaseDocumentLineDto(
    string Id, int LineNo, string ItemId, string Quantity, string Unit, string UnitPrice);

/// <summary>مستند مشتريات ومعه سطوره.</summary>
/// <param name="Document">المستند.</param>
/// <param name="Lines">سطوره.</param>
internal sealed record PurchaseDocumentWithLinesDto(
    CommercialDocumentDto Document, IReadOnlyList<PurchaseDocumentLineDto> Lines);

/// <summary>معامل تحويل وحدةٍ إلى وحدة أساس الصنف على السلك.</summary>
/// <param name="UnitCode">رمز الوحدة الأكبر.</param>
/// <param name="Numerator">البسط.</param>
/// <param name="Denominator">المقام.</param>
internal sealed record UnitFactorDto(string UnitCode, long Numerator, long Denominator);

/// <summary>طلب تسجيل صنف.</summary>
internal sealed record ItemRequestDto
{
    /// <summary>رمز الصنف داخل المنشأة.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم ثنائي اللغة.</summary>
    public required LocalizedTextDto Name { get; init; }

    /// <summary>مجموعة الصنف — مؤهّل دور، لا رقم حساب.</summary>
    public required string ItemGroup { get; init; }

    /// <summary>وحدة الأساس.</summary>
    public required string BaseUnit { get; init; }

    /// <summary>الوحدات الأكبر ومعاملاتها — قد تكون فارغة.</summary>
    public required IReadOnlyList<UnitFactorDto> Units { get; init; }
}

/// <summary>صنف كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="BaseUnit">وحدة الأساس.</param>
/// <param name="Units">الوحدات الأكبر ومعاملاتها.</param>
internal sealed record ItemDto(
    string Id,
    string Code,
    LocalizedTextDto Name,
    string ItemGroup,
    string BaseUnit,
    IReadOnlyList<UnitFactorDto> Units);

/// <summary>
/// كمّية بوحدتها على السلك — <b>ولا كمّية مجرّدة تعبر هذا الحدّ</b>.
/// </summary>
/// <param name="Magnitude">المقدار نصّاً.</param>
/// <param name="Unit">رمز الوحدة.</param>
internal sealed record MeasureDto(string Magnitude, string Unit);

/// <summary>كمّية بوحدتها كما تصل من العميل.</summary>
internal sealed record MeasureRequestDto
{
    /// <summary>المقدار نصّاً.</summary>
    public required WireDecimal Magnitude { get; init; }

    /// <summary>رمز الوحدة.</summary>
    public required string Unit { get; init; }
}

/// <summary>طلب إنشاء مستند حركة مخزون <b>مسوّدة</b>.</summary>
internal sealed record StockMovementRequestDto
{
    /// <summary>رقم المستند.</summary>
    public required string Number { get; init; }

    /// <summary>الاتجاه: <c>IN</c> زيادة أو رصيد افتتاحي · <c>OUT</c> عجز أو إعدام.</summary>
    public required string Direction { get; init; }

    /// <summary>رمز الصنف.</summary>
    public required string ItemId { get; init; }

    /// <summary>المستودع.</summary>
    public required string WarehouseId { get; init; }

    /// <summary>الموقع داخل المستودع.</summary>
    public required string LocationId { get; init; }

    /// <summary>مجموعة الصنف — مؤهّل الدور.</summary>
    public required string ItemGroup { get; init; }

    /// <summary>الكمّية بوحدتها.</summary>
    public required MeasureRequestDto Quantity { get; init; }

    /// <summary>
    /// تكلفة الكمّية الواردة كلّها نصّاً — <b>على الوارد وحده</b>. والصادر تُحسب
    /// تكلفته في وحدة المخزون ولا تُملى، فتُرسَل عليه <c>"0"</c>.
    /// </summary>
    public required WireDecimal Cost { get; init; }

    /// <summary>تاريخ الحركة الميلادي.</summary>
    public required string OccurredOn { get; init; }
}

/// <summary>مستند حركة مخزون كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> · <c>POSTED</c>.</param>
/// <param name="Direction">الاتجاه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="Quantity">الكمّية بوحدتها.</param>
/// <param name="Cost">قيمة الحركة نصّاً — المُسلَّمة على الوارد، والمحسوبة على الصادر بعد الترحيل.</param>
/// <param name="OccurredOn">تاريخ الحركة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل كانت هذه الهوية مُرحَّلة قبل هذا الطلب؟</param>
internal sealed record StockMovementDto(
    string Id,
    string Number,
    string State,
    string Direction,
    string ItemId,
    string WarehouseId,
    string LocationId,
    string ItemGroup,
    MeasureDto Quantity,
    string Cost,
    string OccurredOn,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>رصيد صنف في موقعٍ من مستودع على السلك.</summary>
/// <param name="ItemId">الصنف.</param>
/// <param name="WarehouseId">المستودع.</param>
/// <param name="LocationId">الموقع.</param>
/// <param name="Quantity">الكمّية بوحدة أساسها — قد تكون سالبة.</param>
/// <param name="Value">القيمة نصّاً.</param>
/// <param name="UnitCost">متوسط تكلفة الوحدة نصّاً بمقياس ستّ خانات.</param>
/// <param name="HasCostBasis">هل ورد هذا الصنف إلى هذا الموقع مرّةً بتكلفة؟</param>
internal sealed record StockBalanceDto(
    string ItemId,
    string WarehouseId,
    string LocationId,
    MeasureDto Quantity,
    string Value,
    string UnitCost,
    bool HasCostBasis);

/// <summary>مستندٌ منحرف بين دفتر المخزون المساعد وحسابه الضابط.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="SubledgerEffect">أثره في دفتر المخزون نصّاً.</param>
/// <param name="ControlEffect">أثره في نقطة الضبط نصّاً.</param>
/// <param name="Divergence">الفارق نصّاً.</param>
/// <param name="ReasonCode">سبب الانحراف.</param>
internal sealed record InventoryDivergenceDto(
    string DocumentType,
    string DocumentId,
    string ItemId,
    string SubledgerEffect,
    string ControlEffect,
    string Divergence,
    string ReasonCode);

/// <summary>تقييم المخزون ومطابقته — ثلاثة طرق مستقلّة إلى الرقم نفسه.</summary>
/// <param name="AsOf">تاريخ التقييم.</param>
/// <param name="SubledgerTotal">مجموع دفتر المخزون من حركاته نصّاً.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط نصّاً.</param>
/// <param name="BalanceTotal">مجموع أرصدة الأصناف نصّاً.</param>
/// <param name="Divergence">الفارق نصّاً.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟</param>
/// <param name="Divergences">المستندات المسؤولة عن الفارق.</param>
internal sealed record InventoryValuationDto(
    string AsOf,
    string SubledgerTotal,
    string ControlTotal,
    string BalanceTotal,
    string Divergence,
    bool IsReconciled,
    IReadOnlyList<InventoryDivergenceDto> Divergences);

/// <summary>
/// أصناف المنشأة، مرتَّبة بالرمز ترتيباً حرفياً ثابتاً.
/// <para>
/// <b>وغلافٌ لا مصفوفة عارية</b>: مصفوفةٌ في جذر الاستجابة لا موضع فيها لعدّاد ولا
/// لصفحة، فأول حاجة إليهما تكسر العقد. والشكل هو شكل <c>MembershipList</c> نفسه.
/// </para>
/// </summary>
/// <param name="ItemCount">عدد الأصناف.</param>
/// <param name="Items">الأصناف.</param>
internal sealed record ItemListDto(int ItemCount, IReadOnlyList<ItemDto> Items);

/// <summary>مستندات حركة المخزون، مرتَّبة بالتاريخ ثم بالرقم.</summary>
/// <param name="MovementCount">عدد المستندات.</param>
/// <param name="Movements">المستندات.</param>
internal sealed record StockMovementListDto(int MovementCount, IReadOnlyList<StockMovementDto> Movements);

/// <summary>أرصدة المخزون، مرتَّبة بالصنف ثم المستودع ثم الموقع.</summary>
/// <param name="BalanceCount">عدد الأرصدة.</param>
/// <param name="Balances">الأرصدة.</param>
internal sealed record StockBalanceListDto(int BalanceCount, IReadOnlyList<StockBalanceDto> Balances);
