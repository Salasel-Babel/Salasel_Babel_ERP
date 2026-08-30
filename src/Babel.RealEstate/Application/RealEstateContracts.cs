using Babel.SharedKernel;

namespace Babel.RealEstate.Application;

/// <summary>
/// مسوّدة عقار. <b>ونموذج الملكية إلزامي بلا افتراضي</b>: عليه تُقيَّم قاعدة الحجب
/// GR-RE-001، وقيمةٌ افتراضية هنا تختار للعميل أي الحسابات تُدان.
/// </summary>
/// <param name="Code">رمز العقار داخل المنشأة — وهو ما يظهر بُعداً على سطر القيد.</param>
/// <param name="Name">الاسم: سجلُّه عربي وترجماته صفوف.</param>
/// <param name="OwnershipModel">‏<c>own_property</c> أو <c>managed_for_others</c>.</param>
/// <param name="OwnerId">
/// المالك في نموذج الإدارة. إلزامي هناك ومعدومٌ في الملكية الذاتية: سطر أمانات الملاك
/// يحمل طرفاً في دفتر <c>property_owner</c>، وطرفٌ غائب يُرفض عند الترحيل لا عند العرض.
/// </param>
public sealed record PropertyDraft(string Code, TranslatedName Name, string OwnershipModel, Guid? OwnerId);

/// <summary>عقارٌ كما تراه الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم بترجماته.</param>
/// <param name="OwnershipModel">نموذج الملكية المُسجَّل في الدفتر.</param>
/// <param name="OwnerId">المالك إن وُجد.</param>
/// <param name="OwnerShareNumerator">بسط حصّة المالك.</param>
/// <param name="OwnerShareDenominator">مقام حصّة المالك.</param>
public sealed record PropertyView(
    Guid Id,
    string Code,
    TranslatedName Name,
    string OwnershipModel,
    Guid? OwnerId,
    long OwnerShareNumerator,
    long OwnerShareDenominator);

/// <summary>مسوّدة وحدة داخل عقار.</summary>
/// <param name="Code">رمز الوحدة.</param>
/// <param name="Name">الاسم.</param>
/// <param name="Usage">‏<c>residential</c> أو <c>commercial</c> — يُدخَل ولا يُشتقّ.</param>
/// <param name="VatTreatment">‏<c>standard</c> أو <c>exempt</c> — يُدخَل ولا يُشتقّ.</param>
public sealed record UnitDraft(string Code, TranslatedName Name, string Usage, string VatTreatment);

/// <summary>وحدةٌ كما تراها الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="PropertyId">العقار المالك.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم بترجماته.</param>
/// <param name="Usage">الاستعمال.</param>
/// <param name="VatTreatment">المعاملة الضريبية.</param>
public sealed record UnitView(Guid Id, Guid PropertyId, string Code, TranslatedName Name, string Usage, string VatTreatment);

/// <summary>مسوّدة طرف عقاري.</summary>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي، وفراغٌ لمن لا رقم له.</param>
/// <param name="TaxResidency">‏<c>resident</c> أو <c>non_resident</c>.</param>
public sealed record PartyDraft(string Code, TranslatedName Name, string VatNumber, string TaxResidency);

/// <summary>طرفٌ كما تراه الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Role">دوره: <c>lessee</c> · <c>owner</c> · <c>broker</c>.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم بترجماته.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي.</param>
/// <param name="TaxResidency">الإقامة الضريبية.</param>
public sealed record PartyView(Guid Id, string Role, string Code, TranslatedName Name, string VatNumber, string TaxResidency);

/// <summary>
/// قسطٌ مُصرَّح به في مسوّدة العقد.
/// <para>
/// <b>ولماذا يصل القسط من المُستدعي بدل أن تُوزّع الوحدة قيمة العقد:</b> التوزيع يستلزم
/// <b>سياسة تقريب</b> — أين يقع فائض الهللات — وهي قرار مالك مفتوح (ق-ع-3) لا يُحسم في
/// شيفرة. والثابتة المكتوبة في المصفوفة («مجموع الأقساط = قيمة العقد بالضبط دون هللات
/// ضائعة») <b>تُفرض هنا</b> فحصاً على المجموع، ويُرفض ما لا يستوي برمزٍ يسمّي البند.
/// </para>
/// </summary>
/// <param name="PeriodFrom">بداية الفترة المستحقّة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="DueOn">تاريخ الاستحقاق.</param>
/// <param name="Amount">مبلغ القسط.</param>
public sealed record InstalmentDraft(DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly DueOn, Money Amount);

/// <summary>مسوّدة عقد إيجار.</summary>
/// <param name="ContractNo">رقم العقد داخل المنشأة.</param>
/// <param name="UnitId">الوحدة المؤجَّرة.</param>
/// <param name="LesseeId">المستأجر.</param>
/// <param name="StartsOn">بداية المدّة.</param>
/// <param name="EndsOn">نهايتها.</param>
/// <param name="TotalRent">
/// قيمة العقد كما اتُّفق عليها. <b>وتُصرَّح مستقلّةً عن الأقساط عمداً</b>: الثابتة
/// «مجموع الأقساط = قيمة العقد بالضبط» لا تُفحَص إن كانت القيمة مشتقّةً من الأقساط —
/// تصير صحيحةً بحكم البناء ولا تمسك توزيعاً خاطئاً. وتصريحُ الطرفين هو ما يجعل
/// الفحص فحصاً.
/// </param>
/// <param name="Instalments">الأقساط بفتراتها ومبالغها.</param>
public sealed record LeaseDraft(
    string ContractNo,
    Guid UnitId,
    Guid LesseeId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    Money TotalRent,
    IReadOnlyList<InstalmentDraft> Instalments);

/// <summary>عقدٌ كما تراه الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="ContractNo">رقم العقد.</param>
/// <param name="PropertyId">العقار.</param>
/// <param name="UnitId">الوحدة.</param>
/// <param name="LesseeId">المستأجر.</param>
/// <param name="StartsOn">بداية المدّة.</param>
/// <param name="EndsOn">نهايتها.</param>
/// <param name="TotalRent">قيمة العقد.</param>
/// <param name="State">‏<c>DRAFT</c> أو <c>ACTIVE</c>.</param>
public sealed record LeaseView(
    Guid Id,
    string ContractNo,
    Guid PropertyId,
    Guid UnitId,
    Guid LesseeId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    Money TotalRent,
    string State);

/// <summary>
/// سطر جدول الدفعات <b>بمعرّفه</b> — وهو مدخل الفوترة.
/// <para>بلا نشر المعرّف يصير باب الفوترة باباً لا يوصل إليه بابٌ آخر (ADR-0047).</para>
/// </summary>
/// <param name="Id">معرّف السطر — يُرسَل في طلب الفاتورة.</param>
/// <param name="Seq">تسلسله في العقد.</param>
/// <param name="PeriodFrom">بداية الفترة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="DueOn">تاريخ الاستحقاق.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="IsInvoiced">هل فُوتر؟</param>
public sealed record ScheduleLineView(
    Guid Id,
    int Seq,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    DateOnly DueOn,
    Money Amount,
    bool IsInvoiced);

/// <summary>
/// مسوّدة فاتورة إيجار.
/// <para>
/// <b>ولا رمز حدث فيها ولا نموذج ملكية:</b> الوحدة تقرأ نموذج ملكية العقار
/// <b>المُسجَّل</b> وتختار الحدث منه، فلا يستطيع عميل HTTP أن يطلب «فاتورة ملكية ذاتية»
/// على عقارٍ مُدار.
/// </para>
/// </summary>
/// <param name="Number">رقم الفاتورة.</param>
/// <param name="LeaseId">العقد.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="ScheduleLineIds">أقساط جدول الدفعات المفوترة.</param>
/// <param name="TaxRate">
/// نسبة الضريبة كسراً عشرياً — <b>تصل مع الطلب ولا تُكتب في شيفرة</b> (لا نسبة نظامية
/// في هذا المستودع)، ولا تُطبَّق إلا على وحدةٍ معاملتها <c>standard</c>.
/// </param>
public sealed record RentInvoiceDraft(
    string Number,
    Guid LeaseId,
    DateOnly IssuedOn,
    IReadOnlyList<Guid> ScheduleLineIds,
    decimal TaxRate);

/// <summary>فاتورة إيجار كما تخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Net">الصافي.</param>
/// <param name="Tax">الضريبة.</param>
/// <param name="Gross">الإجمالي.</param>
/// <param name="EventCode">الحدث الذي اختارته الوحدة.</param>
/// <param name="VatTreatment">معاملة الوحدة الضريبية وقت الإصدار.</param>
/// <param name="ExemptionReasonCode">رمز سبب الإعفاء، وفراغٌ ما دام غير معروف.</param>
/// <param name="EntryId">معرّف القيد إن رُحّلت.</param>
/// <param name="AlreadyPosted">هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟</param>
public sealed record RentInvoiceView(
    Guid Id,
    string Number,
    string State,
    Money Net,
    Money Tax,
    Money Gross,
    string EventCode,
    string VatTreatment,
    string ExemptionReasonCode,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>مسوّدة سند قبض من مستأجر.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="LesseeId">المستأجر، أو <c>null</c> فالمبلغ بلا مرجع.</param>
/// <param name="ReceivedOn">تاريخ القبض.</param>
/// <param name="SettlementMethod">‏<c>cash</c> · <c>bank</c> · <c>card_clearing</c> …</param>
/// <param name="TreasuryPartyId">الخزينة أو الحساب البنكي في دفتره المساعد.</param>
/// <param name="Received">المبلغ المقبوض.</param>
public sealed record TenantReceiptDraft(
    string Number,
    Guid? LesseeId,
    DateOnly ReceivedOn,
    string SettlementMethod,
    string TreasuryPartyId,
    Money Received);

/// <summary>سند قبض كما يخرج من الوحدة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Received">المقبوض.</param>
/// <param name="EventCode">الحدث المُرحَّل.</param>
/// <param name="EntryId">قيد الترحيل.</param>
/// <param name="IsAllocated">هل خُصِّص؟</param>
/// <param name="AllocationEntryId">قيد التخصيص المستقل إن وُجد.</param>
/// <param name="AlreadyPosted">هل كانت الهوية مُرحَّلة قبل النداء؟</param>
public sealed record TenantReceiptView(
    Guid Id,
    string Number,
    string State,
    Money Received,
    string EventCode,
    Guid? EntryId,
    bool IsAllocated,
    Guid? AllocationEntryId,
    bool AlreadyPosted);

/// <summary>شرائح أعمار المتأخرات.</summary>
/// <param name="NotDue">لم يستحق بعد.</param>
/// <param name="Days1To30">متأخر 1–30 يوماً.</param>
/// <param name="Days31To60">متأخر 31–60 يوماً.</param>
/// <param name="Days61To90">متأخر 61–90 يوماً.</param>
/// <param name="Over90">متأخر أكثر من 90 يوماً.</param>
/// <param name="Total">المجموع — مجموع الشرائح بالضبط.</param>
public sealed record ArrearsBuckets(
    Money NotDue,
    Money Days1To30,
    Money Days31To60,
    Money Days61To90,
    Money Over90,
    Money Total);

/// <summary>متأخرات مستأجر واحد.</summary>
/// <param name="PartyId">المستأجر.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه بترجماته.</param>
/// <param name="Buckets">شرائحه.</param>
public sealed record PartyArrears(Guid PartyId, string Code, TranslatedName Name, ArrearsBuckets Buckets);

/// <summary>تقرير أعمار متأخرات المستأجرين.</summary>
/// <param name="AsOf">تاريخ التقرير.</param>
/// <param name="Parties">المستأجرون.</param>
/// <param name="Totals">المجاميع.</param>
public sealed record ArrearsReport(DateOnly AsOf, IReadOnlyList<PartyArrears> Parties, ArrearsBuckets Totals);
