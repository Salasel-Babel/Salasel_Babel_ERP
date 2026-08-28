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
