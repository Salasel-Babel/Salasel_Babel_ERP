using Babel.SharedKernel;

namespace Babel.RealEstate.Surface;

/// <summary>
/// طلب تسجيل عقار. <b>ولا حقل مستأجر فيه</b>: النطاق من الاعتماد ومن المسار.
/// </summary>
/// <param name="Code">رمز العقار — وهو ما يظهر بُعداً على سطر القيد.</param>
/// <param name="Name">الاسم: سجلُّه عربي وترجماته صفوف.</param>
/// <param name="OwnershipModel">‏<c>own_property</c> أو <c>managed_for_others</c> — بلا افتراضي.</param>
/// <param name="OwnerId">المالك في نموذج الإدارة، ومعدومٌ في الملكية الذاتية.</param>
public sealed record RealEstatePropertyRequest(string Code, TranslatedName Name, string OwnershipModel, Guid? OwnerId);

/// <summary>عقارٌ كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم بترجماته.</param>
/// <param name="OwnershipModel">نموذج الملكية المُسجَّل في الدفتر.</param>
/// <param name="OwnerId">المالك إن وُجد.</param>
/// <param name="OwnerShareNumerator">بسط حصّة المالك — والحصّة كسر لا عدد عشري.</param>
/// <param name="OwnerShareDenominator">مقام حصّة المالك.</param>
public sealed record RealEstateProperty(
    Guid Id,
    string Code,
    TranslatedName Name,
    string OwnershipModel,
    Guid? OwnerId,
    long OwnerShareNumerator,
    long OwnerShareDenominator);

/// <summary>طلب تسجيل وحدة داخل عقار.</summary>
/// <param name="Code">رمز الوحدة.</param>
/// <param name="Name">الاسم.</param>
/// <param name="Usage">‏<c>residential</c> أو <c>commercial</c> — يُدخَل ويُراجَع ولا يُشتقّ.</param>
/// <param name="VatTreatment">‏<c>standard</c> أو <c>exempt</c> — يُدخَل ولا يُشتقّ.</param>
public sealed record RealEstateUnitRequest(string Code, TranslatedName Name, string Usage, string VatTreatment);

/// <summary>وحدةٌ كما تخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="PropertyId">العقار المالك.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم بترجماته.</param>
/// <param name="Usage">الاستعمال.</param>
/// <param name="VatTreatment">المعاملة الضريبية.</param>
public sealed record RealEstateUnit(
    Guid Id,
    Guid PropertyId,
    string Code,
    TranslatedName Name,
    string Usage,
    string VatTreatment);

/// <summary>طلب تسجيل طرف عقاري — مستأجر أو مالك.</summary>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي، وفراغٌ لمن لا رقم له.</param>
/// <param name="TaxResidency">‏<c>resident</c> أو <c>non_resident</c> — بلا افتراضي.</param>
public sealed record RealEstatePartyRequest(string Code, TranslatedName Name, string VatNumber, string TaxResidency);

/// <summary>طرفٌ كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Role">الدور.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم بترجماته.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي.</param>
/// <param name="TaxResidency">الإقامة الضريبية.</param>
public sealed record RealEstateParty(
    Guid Id,
    string Role,
    string Code,
    TranslatedName Name,
    string VatNumber,
    string TaxResidency);

/// <summary>قسطٌ مُصرَّح به في طلب العقد.</summary>
/// <param name="PeriodFrom">بداية الفترة المستحقّة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="DueOn">تاريخ الاستحقاق.</param>
/// <param name="Amount">مبلغ القسط.</param>
public sealed record RealEstateInstalmentRequest(DateOnly PeriodFrom, DateOnly PeriodTo, DateOnly DueOn, decimal Amount);

/// <summary>
/// طلب <b>تسجيل</b> عقد إيجار مُحرَّر في منصّة إيجار — <b>مسوّدة قيد أرشيفي</b>.
/// <para>
/// <b>ولا يُحرَّر العقد هنا</b>: منصّة إيجار الحكومية هي الطرف المخوَّل بتحريره، وما
/// يُنشئه هذا الطلب قيدٌ عندنا مرجعُه رقمُ عقد إيجار. ولا تكامل مع المنصّة.
/// </para>
/// <para>
/// <b>والأقساط تصل مصرَّحاً بها ولا تُوزَّع من قيمة العقد:</b> التوزيع يستلزم سياسة
/// تقريب لم يحسمها المالك بعد، والنظام يفحص أن مجموعها يساوي قيمة العقد بالضبط.
/// </para>
/// </summary>
/// <param name="EjarContractNumber">رقم عقد إيجار — مرجع العقد المُحرَّر في المنصّة، ولا يولّده هذا النظام.</param>
/// <param name="UnitId">الوحدة المؤجَّرة — ومنها يُشتقّ العقار.</param>
/// <param name="LesseeId">المستأجر.</param>
/// <param name="StartsOn">بداية المدّة.</param>
/// <param name="EndsOn">نهايتها.</param>
/// <param name="TotalRent">قيمة العقد المتَّفق عليها — تُصرَّح مستقلّةً كي يبقى فحص المجموع فحصاً.</param>
/// <param name="Instalments">الأقساط بفتراتها ومبالغها.</param>
public sealed record RealEstateLeaseRequest(
    string EjarContractNumber,
    Guid UnitId,
    Guid LesseeId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    decimal TotalRent,
    IReadOnlyList<RealEstateInstalmentRequest> Instalments);

/// <summary>قيدُ تسجيلِ عقدٍ كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="EjarContractNumber">رقم عقد إيجار — مرجع العقد المُحرَّر في المنصّة.</param>
/// <param name="PropertyId">العقار.</param>
/// <param name="UnitId">الوحدة.</param>
/// <param name="LesseeId">المستأجر.</param>
/// <param name="StartsOn">بداية المدّة.</param>
/// <param name="EndsOn">نهايتها.</param>
/// <param name="TotalRent">قيمة العقد.</param>
/// <param name="State">‏<c>DRAFT</c> أو <c>BILLABLE</c> — حالة القيد لا حالة العقد.</param>
public sealed record RealEstateLease(
    Guid Id,
    string EjarContractNumber,
    Guid PropertyId,
    Guid UnitId,
    Guid LesseeId,
    DateOnly StartsOn,
    DateOnly EndsOn,
    decimal TotalRent,
    string State);

/// <summary>سطر جدول الدفعات <b>بمعرّفه</b> — وهو مدخل الفوترة.</summary>
/// <param name="Id">معرّف السطر.</param>
/// <param name="Seq">تسلسله.</param>
/// <param name="PeriodFrom">بداية الفترة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="DueOn">تاريخ الاستحقاق.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="IsInvoiced">هل فُوتر؟</param>
public sealed record RealEstateScheduleLine(
    Guid Id,
    int Seq,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    DateOnly DueOn,
    decimal Amount,
    bool IsInvoiced);

/// <summary>
/// طلب إنشاء فاتورة إيجار <b>مسوّدة</b>.
/// <para><b>ولا رمز حدث فيه ولا نموذج ملكية</b>: الوحدة تقرؤه من السجلّ.</para>
/// </summary>
/// <param name="Number">رقم الفاتورة.</param>
/// <param name="LeaseId">العقد.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="ScheduleLineIds">أقساط جدول الدفعات المفوترة.</param>
/// <param name="TaxRate">نسبة الضريبة كسراً عشرياً — تصل مع الطلب ولا تُكتب في شيفرة.</param>
public sealed record RealEstateRentInvoiceRequest(
    string Number,
    Guid LeaseId,
    DateOnly IssuedOn,
    IReadOnlyList<Guid> ScheduleLineIds,
    decimal TaxRate);

/// <summary>فاتورة إيجار كما تخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Net">الصافي.</param>
/// <param name="Tax">الضريبة.</param>
/// <param name="Gross">الإجمالي.</param>
/// <param name="EventCode">الحدث الذي اختارته الوحدة من نموذج الملكية المُسجَّل.</param>
/// <param name="VatTreatment">معاملة الوحدة الضريبية وقت الإصدار.</param>
/// <param name="ExemptionReasonCode">رمز سبب الإعفاء، وفراغٌ ما دام غير معروف.</param>
/// <param name="ExemptionReasonPending">
/// ‏<c>true</c> على فاتورةٍ معفاة بلا رمز سبب — <b>علامة ظاهرة لا تعليق في شيفرة</b>:
/// الرمز يُؤخذ من القائمة الرسمية السارية ولا يُخترع (م-8).
/// </param>
/// <param name="EntryId">معرّف القيد إن رُحّلت.</param>
/// <param name="AlreadyPosted">هل كانت الهوية مُرحَّلة قبل هذا النداء؟</param>
public sealed record RealEstateRentInvoice(
    Guid Id,
    string Number,
    string State,
    decimal Net,
    decimal Tax,
    decimal Gross,
    string EventCode,
    string VatTreatment,
    string ExemptionReasonCode,
    bool ExemptionReasonPending,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>طلب تسجيل سند قبض من مستأجر.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="LesseeId">المستأجر، ومعدومٌ فالمبلغ ورد بلا مرجع.</param>
/// <param name="ReceivedOn">تاريخ القبض.</param>
/// <param name="SettlementMethod">طريقة التسوية — مؤهِّل الدور.</param>
/// <param name="TreasuryPartyId">الخزينة أو الحساب البنكي في دفتره المساعد.</param>
/// <param name="Received">المبلغ المقبوض.</param>
public sealed record RealEstateReceiptRequest(
    string Number,
    Guid? LesseeId,
    DateOnly ReceivedOn,
    string SettlementMethod,
    string TreasuryPartyId,
    decimal Received);

/// <summary>طلب تخصيص سند قبض ورد بلا مرجع.</summary>
/// <param name="LesseeId">المستأجر الذي تبيّن أن المبلغ له.</param>
public sealed record RealEstateAllocationRequest(Guid LesseeId);

/// <summary>سند قبض كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Received">المقبوض.</param>
/// <param name="EventCode">الحدث المُرحَّل.</param>
/// <param name="EntryId">قيد الترحيل.</param>
/// <param name="IsAllocated">هل خُصِّص؟</param>
/// <param name="AllocationEntryId">قيد التخصيص المستقلّ إن وقع.</param>
/// <param name="AlreadyPosted">هل كانت الهوية مُرحَّلة قبل النداء؟</param>
public sealed record RealEstateReceipt(
    Guid Id,
    string Number,
    string State,
    decimal Received,
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
/// <param name="Total">المجموع.</param>
public sealed record RealEstateArrearsBands(
    decimal NotDue,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total);

/// <summary>متأخرات مستأجر واحد.</summary>
/// <param name="PartyId">المستأجر.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه بترجماته.</param>
/// <param name="Bands">شرائحه.</param>
public sealed record RealEstateArrearsParty(Guid PartyId, string Code, TranslatedName Name, RealEstateArrearsBands Bands);

/// <summary>
/// تقرير أعمار المتأخرات <b>ومطابقته بنقطة ضبطه</b>.
/// <para>
/// والمطابقة في الجواب نفسه لا في مورد ثانٍ: تقريرٌ لا يُفتح لا يكشف انحرافاً.
/// </para>
/// </summary>
/// <param name="AsOf">تاريخ التقرير.</param>
/// <param name="Parties">المستأجرون.</param>
/// <param name="Totals">المجاميع.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط في الدفتر.</param>
/// <param name="Divergence">الفارق: مجموع الدفتر المساعد ناقص نقطة الضبط.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟</param>
public sealed record RealEstateArrears(
    DateOnly AsOf,
    IReadOnlyList<RealEstateArrearsParty> Parties,
    RealEstateArrearsBands Totals,
    decimal ControlTotal,
    decimal Divergence,
    bool IsReconciled);
