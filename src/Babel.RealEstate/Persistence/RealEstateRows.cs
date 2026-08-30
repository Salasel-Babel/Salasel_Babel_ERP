namespace Babel.RealEstate.Persistence;

/// <summary>حالة مستند عقاري.</summary>
internal static class RealEstateDocumentState
{
    public const string Draft = "DRAFT";
    public const string Posted = "POSTED";
}

/// <summary>حالة عقد الإيجار.</summary>
internal static class LeaseState
{
    /// <summary>مسوّدة: لا جدول دفعات ولا أثر.</summary>
    public const string Draft = "DRAFT";

    /// <summary>سارٍ: جدول الدفعات مولَّد، والمدّة تدخل قيد الاستبعاد الزمني.</summary>
    public const string Active = "ACTIVE";
}

/// <summary>حالة محاولة ترحيل مستند — منسوخة عن وحدة المبيعات لأن العقد واحد.</summary>
internal static class PostingAttemptState
{
    public const string Attempting = "ATTEMPTING";
    public const string Posted = "POSTED";
    public const string Refused = "REFUSED";
}

/// <summary>دور الطرف في هذه الوحدة.</summary>
internal static class PartyRoles
{
    /// <summary>المستأجر. <b>ولا يُسمّى <c>tenant</c> على السطح</b>: الكلمة مأخوذة لمستأجر النظام.</summary>
    public const string Lessee = "lessee";

    /// <summary>مالك العقار.</summary>
    public const string Owner = "owner";

    /// <summary>الوسيط.</summary>
    public const string Broker = "broker";
}

/// <summary>
/// العقار. <c>internal</c> — لا يعبر حدّ الوحدة (القاعدة 5).
/// <para>
/// <b>والمفتاح ثلاثي <c>(tenant_id, company_id, code)</c> لا ثنائي:</b> صفّ العقار في
/// سجلّ أبعاد الدفتر مفتاحه <c>(company_id, property_id)</c>، فكيانٌ بلا منشأة لا
/// يُشتقّ منه صفّ ذلك السجلّ أصلاً.
/// </para>
/// </summary>
internal sealed class PropertyRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم العربي — <b>السجلّ</b>. والترجمات صفوف (ADR-0021 · القاعدة 14).</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>نموذج الملكية كما سُجِّل في الدفتر. <b>لا يُعدَّل بعد الإنشاء.</b></summary>
    public string OwnershipModel { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

/// <summary>ترجمة اسم عقار — صفٌّ لا عمود.</summary>
internal sealed class PropertyTranslationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string PropertyCode { get; set; } = string.Empty;

    public string LanguageTag { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// حصّة مالك في عقار — <b>الشكل الذي يحتمل الحصص من اليوم</b>.
/// <para>
/// <b>ولماذا يوجد هذا الجدول وفي كل عقار صفٌّ واحد:</b> مفتاحه
/// <c>(tenant_id, company_id, property_id, owner_id)</c>. ولو كان المالك عموداً على
/// العقار لكان إدخال الحصص لاحقاً <b>هجرةً على مفتاح</b> تُعيد توزيع أرصدة أمانات
/// مُرحَّلة على ملّاك لا يعرفهم أحد. وهو الدرس نفسه الذي أخذه المخزون حين أدخل الموقع
/// في المفتاح بقيمة واحدة (ADR-0049): لا رقم يتغيّر اليوم، ولا هجرة تُستحقّ غداً.
/// </para>
/// <para>
/// <b>والحصّة نسبةٌ بصورة كسر لا عدد عشري</b> — بسطٌ ومقام صحيحان، بشكل معامل تحويل
/// الوحدة في المخزون بالضبط. والسبب مقيس: <b>لا مقياس عشري معلَن لأي نسبة في هذا
/// المستودع</b> (المقياس 4 مقيسٌ للمال وحده)، وحصص الصكوك تُقاس بأسهم لا بكسر من مئة.
/// فاختيار مقياسٍ هنا كان سيكون رقماً نظامياً مكتوباً في مخطّط — وهو ما لا يُكتب.
/// </para>
/// </summary>
internal sealed class PropertyOwnerShareRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid PropertyId { get; set; }

    public Guid OwnerId { get; set; }

    /// <summary>بسط الحصّة.</summary>
    public long ShareNumerator { get; set; }

    /// <summary>مقام الحصّة.</summary>
    public long ShareDenominator { get; set; }
}

/// <summary>
/// الوحدة داخل عقار.
/// <para>
/// <b>و<c>Usage</c> و<c>VatTreatment</c> حقلان صريحان لا يُشتقّ أحدهما من الآخر ولا من
/// نوع العقار</b>: العقار المختلط يولّد توريداً خاضعاً ومعفى في آنٍ واحد، والاشتقاق
/// الآلي يُنتج آلاف العقود بتصنيفٍ واحد خاطئ (م-3).
/// </para>
/// </summary>
internal sealed class UnitRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public Guid PropertyId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    /// <summary>الاستعمال المُدخَل والمُراجَع: <c>residential</c> · <c>commercial</c>.</summary>
    public string Usage { get; set; } = string.Empty;

    /// <summary>المعاملة الضريبية المُدخَلة: <c>standard</c> · <c>exempt</c>.</summary>
    public string VatTreatment { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

/// <summary>طرف عقاري: مستأجر أو مالك أو وسيط.</summary>
internal sealed class PartyRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string PartyRole { get; set; } = string.Empty;

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    /// <summary>رقم التسجيل الضريبي، وفراغٌ على من لا رقم له — والغياب واقعة لا نقص.</summary>
    public string VatNumber { get; set; } = string.Empty;

    /// <summary>
    /// الإقامة الضريبية: <c>resident</c> · <c>non_resident</c>. تُقرأ في توريد المالك،
    /// وهي البند الذي يُغلق أو يفتح سطر الاستقطاع (م-7).
    /// </summary>
    public string TaxResidency { get; set; } = string.Empty;
}

/// <summary>ترجمة اسم طرف — صفٌّ لا عمود.</summary>
internal sealed class PartyTranslationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string PartyRole { get; set; } = string.Empty;

    public string PartyCode { get; set; } = string.Empty;

    public string LanguageTag { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;
}

/// <summary>عقد إيجار.</summary>
internal sealed class LeaseContractRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string ContractNo { get; set; } = string.Empty;

    public Guid PropertyId { get; set; }

    public Guid UnitId { get; set; }

    public Guid LesseeId { get; set; }

    public DateOnly StartsOn { get; set; }

    public DateOnly EndsOn { get; set; }

    /// <summary>قيمة العقد — مجموع الأقساط بالضبط، مفحوصاً لا مُقرَّباً.</summary>
    public decimal TotalRent { get; set; }

    public string State { get; set; } = LeaseState.Draft;
}

/// <summary>
/// سطر جدول الدفعات.
/// <para>
/// <b>وحقلا الفترة قبل تاريخ الاستحقاق:</b> أساس الاعتراف مدى الفترة لا يوم السداد،
/// وقسطٌ بلا فترته لا يُنسب إلى شهرٍ في قائمة دخل.
/// </para>
/// </summary>
internal sealed class PaymentScheduleLineRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid LeaseId { get; set; }

    public int Seq { get; set; }

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public DateOnly DueOn { get; set; }

    public decimal Amount { get; set; }

    /// <summary>هل فُوتر هذا القسط؟ فاتورةٌ ثانية على القسط نفسه تُرفض.</summary>
    public bool IsInvoiced { get; set; }
}

/// <summary>فاتورة إيجار.</summary>
internal sealed class RentInvoiceRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid LeaseId { get; set; }

    public Guid PropertyId { get; set; }

    public Guid UnitId { get; set; }

    public Guid LesseeId { get; set; }

    public DateOnly IssuedOn { get; set; }

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrossTotal { get; set; }

    /// <summary>المعاملة الضريبية المنسوخة عن الوحدة وقت الإصدار.</summary>
    public string VatTreatment { get; set; } = string.Empty;

    /// <summary>
    /// رمز سبب الإعفاء الضريبي — <b>عمودٌ موجود وقيمته فارغة</b> حتى يُعرف الرمز من
    /// القائمة الرسمية السارية. وحقلٌ إلزامي بقيمة مُختلَقة أسوأ من حقلٍ فارغ (م-8).
    /// </summary>
    public string ExemptionReasonCode { get; set; } = string.Empty;

    /// <summary>الحدث الذي اختارته الوحدة من نموذج الملكية <b>المُسجَّل</b>.</summary>
    public string EventCode { get; set; } = string.Empty;

    public string State { get; set; } = RealEstateDocumentState.Draft;

    public Guid? EntryId { get; set; }
}

/// <summary>سطر فاتورة إيجار — قسطٌ واحد من جدول الدفعات.</summary>
internal sealed class RentInvoiceLineRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid InvoiceId { get; set; }

    public Guid ScheduleLineId { get; set; }

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public decimal Net { get; set; }

    public decimal Tax { get; set; }
}

/// <summary>سند قبض من مستأجر.</summary>
internal sealed class TenantReceiptRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid CompanyId { get; set; }

    public string Number { get; set; } = string.Empty;

    /// <summary>المستأجر، أو <c>null</c> فالمبلغ وارد بلا مرجع يربطه بأحد.</summary>
    public Guid? LesseeId { get; set; }

    public DateOnly ReceivedOn { get; set; }

    /// <summary>طريقة التسوية — مؤهِّل الدور الذي يقرؤه <c>qualifier_source</c>.</summary>
    public string SettlementMethod { get; set; } = string.Empty;

    /// <summary>الخزينة أو الحساب البنكي في دفتره المساعد.</summary>
    public string TreasuryPartyId { get; set; } = string.Empty;

    public decimal Received { get; set; }

    public string State { get; set; } = RealEstateDocumentState.Draft;

    /// <summary>الحدث المُرحَّل: <c>collection.received</c> أو <c>collection.unallocated</c>.</summary>
    public string EventCode { get; set; } = string.Empty;

    public Guid? EntryId { get; set; }

    /// <summary>هل خُصِّص المبلغ بعد ترحيله غير مخصَّص؟ التخصيص قيدٌ مستقل لا عكس.</summary>
    public bool IsAllocated { get; set; }

    public Guid? AllocationEntryId { get; set; }
}

/// <summary>سجلّ محاولة ترحيل مستند — الهوية السداسية بعينها.</summary>
internal sealed class DocumentPostingRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string TriggerCode { get; set; } = string.Empty;

    public int Generation { get; set; } = 1;

    public string EventCode { get; set; } = string.Empty;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string PartyId { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public decimal ControlEffect { get; set; }

    public string State { get; set; } = PostingAttemptState.Attempting;

    public int AttemptCount { get; set; }

    public DateTime? LastAttemptAt { get; set; }

    public Guid? EntryId { get; set; }

    /// <summary>رقم القيد من العدّاد بلا فجوات — عددٌ لا نصّ، كما يعيده الإيصال.</summary>
    public long EntryNumber { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessageAr { get; set; } = string.Empty;

    public string FailureMessageEn { get; set; } = string.Empty;
}
