using Babel.SharedKernel;

namespace Babel.Hr.Surface;

/// <summary>
/// البيانات الشخصية عند التسجيل. <b>ولا تعود من أي قراءة على هذا السطح إلا مقنَّعة.</b>
/// </summary>
/// <param name="NationalId">رقم الهوية أو الإقامة.</param>
/// <param name="Iban">الآيبان.</param>
/// <param name="BirthDate">تاريخ الميلاد الميلادي.</param>
public sealed record HrIdentityRequest(string NationalId, string Iban, DateOnly BirthDate);

/// <summary>الهوية مقنَّعة: آخر أربعة محارف وحدها، وما قبلها نجوم بعدد ثابت.</summary>
/// <param name="NationalIdMask">قناع رقم الهوية.</param>
/// <param name="IbanMask">قناع الآيبان.</param>
public sealed record HrMaskedIdentity(string NationalIdMask, string IbanMask);

/// <summary>
/// طلب تسجيل موظف. <b>ولا رمز فيه</b>: الرمز المعتم يولّده الخادم، وهو وحده ما يعبر
/// إلى دفتر الأستاذ.
/// </summary>
/// <param name="Name">الاسم — عربيّه سجلٌّ وترجماته صفوف.</param>
/// <param name="ClassCode">تصنيف الاشتراك — مؤهّل صفّ الإعدادات، لا نسبة.</param>
/// <param name="CostCenterId">مركز التكلفة، أو فراغٌ فالافتراضي.</param>
/// <param name="HiredOn">تاريخ بدء علاقة العمل.</param>
/// <param name="Identity">البيانات الشخصية.</param>
public sealed record HrEmployeeRequest(
    TranslatedName Name, string ClassCode, string CostCenterId, DateOnly HiredOn, HrIdentityRequest Identity);

/// <summary>الموظف كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز المعتم.</param>
/// <param name="Name">الاسم بسجلّه وترجماته.</param>
/// <param name="ClassCode">تصنيف الاشتراك.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="EmploymentId">علاقة العمل الجارية أو الأخيرة.</param>
/// <param name="StartedOn">بدؤها.</param>
/// <param name="EndedOn">انتهاؤها، أو <c>null</c>.</param>
/// <param name="State">حالتها.</param>
/// <param name="Identity">الهوية مقنَّعة.</param>
public sealed record HrEmployee(
    Guid Id,
    string Code,
    TranslatedName Name,
    string ClassCode,
    string CostCenterId,
    Guid EmploymentId,
    DateOnly StartedOn,
    DateOnly? EndedOn,
    string State,
    HrMaskedIdentity Identity);

/// <summary>طلب إنهاء خدمة — <b>مورد فرعي لا حقل حالة يُعدَّل</b>.</summary>
/// <param name="EndedOn">تاريخ انتهاء الخدمة.</param>
/// <param name="ReasonKey">مفتاح سبب الإنهاء — رمزٌ يقرؤه برنامج لا نصٌّ يُعرض.</param>
public sealed record HrTerminationRequest(DateOnly EndedOn, string ReasonKey);

/// <summary>طلب تعريف مكوّن أجر — <b>تصنيفٌ لا مبلغ ولا نسبة</b>.</summary>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="Kind">‏<c>earning</c> أو <c>deduction</c>.</param>
/// <param name="EntersContributoryWage">هل يدخل وعاء الاشتراك؟</param>
/// <param name="EntersEndOfServiceBase">هل يدخل وعاء نهاية الخدمة؟</param>
public sealed record HrPayComponentRequest(
    string Code, TranslatedName Name, string Kind, bool EntersContributoryWage, bool EntersEndOfServiceBase);

/// <summary>مكوّن أجر كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="Kind">النوع.</param>
/// <param name="EntersContributoryWage">وسم وعاء الاشتراك.</param>
/// <param name="EntersEndOfServiceBase">وسم وعاء نهاية الخدمة.</param>
public sealed record HrPayComponent(
    Guid Id, string Code, TranslatedName Name, string Kind, bool EntersContributoryWage, bool EntersEndOfServiceBase);

/// <summary>طلب إسناد قيمة مكوّن بتاريخ سريان — <b>إنشاءٌ لا تعديل</b>.</summary>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="Amount">القيمة.</param>
public sealed record HrPayElementRequest(string ComponentCode, DateOnly EffectiveFrom, decimal Amount);

/// <summary>قيمة مكوّن كما تخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="Amount">القيمة.</param>
public sealed record HrPayElement(Guid Id, string ComponentCode, DateOnly EffectiveFrom, decimal Amount);

/// <summary>
/// طلب إيداع إصدار من نِسَب الاشتراك وحدودها — <b>الموضع الوحيد الذي تدخل منه نسبة</b>.
/// </summary>
/// <param name="ClassCode">تصنيف الاشتراك.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="EmployerRate">نسبة المنشأة كسراً عشرياً.</param>
/// <param name="EmployeeRate">نسبة الموظف كسراً عشرياً.</param>
/// <param name="MinimumContributoryWage">أدنى أجر خاضع.</param>
/// <param name="MaximumContributoryWage">أقصى أجر خاضع، أو صفر فلا سقف.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد.</param>
/// <param name="SourceRef">مرجع المصدر النظامي — غير فارغ.</param>
public sealed record HrPayrollSettingsRequest(
    string ClassCode,
    DateOnly EffectiveFrom,
    decimal EmployerRate,
    decimal EmployeeRate,
    decimal MinimumContributoryWage,
    decimal MaximumContributoryWage,
    string ApprovedBy,
    DateOnly ApprovedOn,
    string SourceRef);

/// <summary>إصدار نِسَبٍ كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="ClassCode">التصنيف.</param>
/// <param name="EffectiveFrom">السريان.</param>
/// <param name="EmployerRate">نسبة المنشأة.</param>
/// <param name="EmployeeRate">نسبة الموظف.</param>
/// <param name="MinimumContributoryWage">أدنى أجر خاضع.</param>
/// <param name="MaximumContributoryWage">أقصى أجر خاضع.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد.</param>
/// <param name="SourceRef">مرجع المصدر.</param>
public sealed record HrPayrollSettings(
    Guid Id,
    string ClassCode,
    DateOnly EffectiveFrom,
    decimal EmployerRate,
    decimal EmployeeRate,
    decimal MinimumContributoryWage,
    decimal MaximumContributoryWage,
    string ApprovedBy,
    DateOnly ApprovedOn,
    string SourceRef);

/// <summary>طلب إنشاء مسيّر رواتب <b>مسوّدة</b>. <b>ولا مجاميع فيه</b>.</summary>
/// <param name="Number">رقم المسيّر.</param>
/// <param name="PeriodCode">رمز الفترة <c>yyyy-MM</c>.</param>
/// <param name="PeriodStart">بداية الفترة.</param>
/// <param name="PeriodEnd">نهايتها — وهي تاريخ قيد الاستحقاق.</param>
public sealed record HrPayrollRunRequest(string Number, string PeriodCode, DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>المبالغ الستّة بأسماء مفردات المصفوفة نفسها.</summary>
/// <param name="GrossEntitlements">إجمالي المستحقات.</param>
/// <param name="EmployerSocialInsurance">حصة المنشأة.</param>
/// <param name="EmployeeSocialInsurance">حصة الموظف.</param>
/// <param name="AdvanceInstalment">قسط السلفة المستقطع.</param>
/// <param name="Deductions">الخصومات والجزاءات.</param>
/// <param name="NetPayable">الصافي المستحق.</param>
public sealed record HrPayrollAmounts(
    decimal GrossEntitlements,
    decimal EmployerSocialInsurance,
    decimal EmployeeSocialInsurance,
    decimal AdvanceInstalment,
    decimal Deductions,
    decimal NetPayable);

/// <summary>مسيّر رواتب كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="PeriodStart">بدايتها.</param>
/// <param name="PeriodEnd">نهايتها.</param>
/// <param name="State">الحالة.</param>
/// <param name="Amounts">المجاميع.</param>
/// <param name="PayslipCount">عدد القسائم.</param>
public sealed record HrPayrollRun(
    Guid Id,
    string Number,
    string PeriodCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string State,
    HrPayrollAmounts Amounts,
    int PayslipCount);

/// <summary>مكوّن على قسيمة كما يخرج من السطح.</summary>
/// <param name="LineNo">رقم السطر.</param>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="Kind">نوعه.</param>
/// <param name="EntersContributoryWage">هل دخل وعاء الاشتراك؟</param>
/// <param name="Amount">المبلغ.</param>
public sealed record HrPayslipComponent(
    int LineNo, string ComponentCode, string Kind, bool EntersContributoryWage, decimal Amount);

/// <summary>
/// قسيمة كما تخرج من السطح — <b>وهي مستند الترحيل</b>، ومعرّفها هو <c>DocumentId</c>
/// في هوية الإحكام، و<c>EntryId</c> قيدُها هي وحدها لا قيدُ المسيّر.
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="RunId">المسيّر.</param>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="EmploymentId">علاقة العمل.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="ContributoryWage">وعاء الاشتراك بعد القصّ بحدَّي الصفّ المعتمد.</param>
/// <param name="Amounts">المبالغ الستّة.</param>
/// <param name="Components">تفصيل المكوّنات.</param>
/// <param name="State">الحالة.</param>
/// <param name="EntryId">معرّف قيد هذه القسيمة إن رُحّلت.</param>
/// <param name="AlreadyPosted">هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟</param>
public sealed record HrPayslip(
    Guid Id,
    Guid RunId,
    Guid EmployeeId,
    Guid EmploymentId,
    string EmployeeCode,
    string CostCenterId,
    decimal ContributoryWage,
    HrPayrollAmounts Amounts,
    IReadOnlyList<HrPayslipComponent> Components,
    string State,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>طلب إنشاء سند صرف رواتب <b>مسوّدة</b> على مسيّر مُرحَّل.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="RunId">المسيّر المُرحَّل.</param>
/// <param name="PaidOn">تاريخ الصرف.</param>
/// <param name="SettlementMethod">طريقة التسوية — مؤهّل دور.</param>
/// <param name="TreasuryPartyId">طرف الخزينة — <b>إلزامي</b>.</param>
public sealed record HrPayrollPaymentRequest(
    string Number, Guid RunId, DateOnly PaidOn, string SettlementMethod, string TreasuryPartyId);

/// <summary>سطر سند صرف كما يخرج من السطح.</summary>
/// <param name="LineNo">رقم السطر.</param>
/// <param name="PayslipId">القسيمة.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="EntryId">معرّف قيد هذا السطر إن رُحّل.</param>
public sealed record HrPayrollPaymentLine(
    int LineNo, Guid PayslipId, string EmployeeCode, decimal Amount, Guid? EntryId);

/// <summary>سند صرف الرواتب كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="RunId">المسيّر.</param>
/// <param name="PaidOn">تاريخ الصرف.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="NetPayable">مجموع المصروف.</param>
/// <param name="State">الحالة.</param>
/// <param name="Lines">السطور.</param>
/// <param name="AlreadyPosted">هل كانت مُرحَّلة قبل هذا النداء؟</param>
public sealed record HrPayrollPayment(
    Guid Id,
    string Number,
    Guid RunId,
    DateOnly PaidOn,
    string SettlementMethod,
    string TreasuryPartyId,
    decimal NetPayable,
    string State,
    IReadOnlyList<HrPayrollPaymentLine> Lines,
    bool AlreadyPosted);

/// <summary>طلب إنشاء سند سداد تأمينات <b>مسوّدة</b>.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="PeriodCode">الفترة المسدَّدة.</param>
/// <param name="PaidOn">تاريخ السداد.</param>
/// <param name="Amount">المبلغ المسدَّد.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة — <b>إلزامي</b>.</param>
public sealed record HrSocialInsurancePaymentRequest(
    string Number, string PeriodCode, DateOnly PaidOn, decimal Amount, string SettlementMethod, string TreasuryPartyId);

/// <summary>سداد تأمينات كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="PaidOn">تاريخ السداد.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="AccruedForPeriod">ما استُحقّ في الفترة من مسيّرات مُرحَّلة — للمقارنة لا للإملاء.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="State">الحالة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل كان مُرحَّلاً قبل هذا النداء؟</param>
public sealed record HrSocialInsurancePayment(
    Guid Id,
    string Number,
    string PeriodCode,
    DateOnly PaidOn,
    decimal Amount,
    decimal AccruedForPeriod,
    string SettlementMethod,
    string TreasuryPartyId,
    string State,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>طلب قيد جزاء في السجلّ المعتمد. <b>ولا مورد ترحيل له.</b></summary>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="PeriodCode">فترة الاستقطاع.</param>
/// <param name="CategoryKey">مفتاح فئة السبب.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد.</param>
public sealed record HrDeductionRequest(
    Guid EmployeeId, string PeriodCode, string CategoryKey, decimal Amount, string ApprovedBy, DateOnly ApprovedOn);

/// <summary>
/// جزاءٌ كما يخرج من السطح — <b>بلا <c>entryId</c> وبلا <c>alreadyPosted</c></b>: حقلٌ
/// فارغ يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل».
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="CategoryKey">فئة السبب.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد.</param>
/// <param name="ConsumedByPayslipId">القسيمة التي استُقطع فيها، أو <c>null</c>.</param>
public sealed record HrDeduction(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    string PeriodCode,
    string CategoryKey,
    decimal Amount,
    string ApprovedBy,
    DateOnly ApprovedOn,
    Guid? ConsumedByPayslipId);

/// <summary>قسط سداد سلفة في الطلب.</summary>
/// <param name="PeriodCode">الفترة التي يُستقطع فيها.</param>
/// <param name="Amount">المبلغ.</param>
public sealed record HrInstalmentRequest(string PeriodCode, decimal Amount);

/// <summary>طلب إنشاء سلفة <b>مسوّدة</b> بجدول أقساطها.</summary>
/// <param name="Number">رقم السلفة.</param>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="IssuedOn">تاريخ المنح.</param>
/// <param name="Amount">مبلغ السلفة.</param>
/// <param name="SettlementMethod">طريقة الصرف.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="Instalments">جدول الأقساط — مجموعه يساوي المبلغ بالضبط.</param>
public sealed record HrAdvanceRequest(
    string Number,
    Guid EmployeeId,
    DateOnly IssuedOn,
    decimal Amount,
    string SettlementMethod,
    string TreasuryPartyId,
    IReadOnlyList<HrInstalmentRequest> Instalments);

/// <summary>قسط سلفة كما يخرج من السطح.</summary>
/// <param name="LineNo">رقم القسط.</param>
/// <param name="PeriodCode">فترته.</param>
/// <param name="Amount">مبلغه.</param>
/// <param name="ConsumedByPayslipId">القسيمة التي استُقطع فيها، أو <c>null</c>.</param>
public sealed record HrInstalment(int LineNo, string PeriodCode, decimal Amount, Guid? ConsumedByPayslipId);

/// <summary>
/// سلفة كما تخرج من السطح — <b>بلا حقل قيد</b>: حدث صرف السلفة غير موجود في مصفوفة
/// الترحيل، فبابُ ترحيلها غير منشور، وحقلٌ فارغ كان سيَعِد بدورة لا تكتمل.
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="IssuedOn">تاريخ المنح.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="SettlementMethod">طريقة الصرف.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="OutstandingAmount">المتبقّي — من الأقساط المستقطعة فعلاً وحدها.</param>
/// <param name="State">الحالة.</param>
/// <param name="Instalments">جدول الأقساط.</param>
public sealed record HrAdvance(
    Guid Id,
    string Number,
    Guid EmployeeId,
    string EmployeeCode,
    DateOnly IssuedOn,
    decimal Amount,
    string SettlementMethod,
    string TreasuryPartyId,
    decimal OutstandingAmount,
    string State,
    IReadOnlyList<HrInstalment> Instalments);

/// <summary>حصّة علاقة عمل من مخصص الفترة — <b>مبلغٌ يُدخله معتمِد المستند</b>.</summary>
/// <param name="EmploymentId">علاقة العمل.</param>
/// <param name="PeriodShare">حصّة الفترة كما قاسها المحاسب.</param>
public sealed record HrProvisionShareRequest(Guid EmploymentId, decimal PeriodShare);

/// <summary>طلب إنشاء مستند استحقاق مخصص نهاية الخدمة <b>مسوّدة</b>.</summary>
/// <param name="Number">رقم المستند.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="AccruedOn">تاريخ الاستحقاق.</param>
/// <param name="MeasurementRef">مرجع أساس القياس المعتمد — غير فارغ.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="Shares">حصص علاقات العمل.</param>
public sealed record HrProvisionRequest(
    string Number,
    string PeriodCode,
    DateOnly AccruedOn,
    string MeasurementRef,
    string ApprovedBy,
    IReadOnlyList<HrProvisionShareRequest> Shares);

/// <summary>حركة مخصص كما تخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="EmploymentId">علاقة العمل.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="PeriodShare">حصّة الفترة.</param>
/// <param name="EntryId">معرّف قيد هذه الحركة إن رُحّلت.</param>
public sealed record HrProvisionMovement(
    Guid Id, Guid EmploymentId, string EmployeeCode, decimal PeriodShare, Guid? EntryId);

/// <summary>مستند استحقاق المخصص كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="AccruedOn">تاريخ الاستحقاق.</param>
/// <param name="MeasurementRef">مرجع أساس القياس.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="PeriodShare">مجموع الحصص.</param>
/// <param name="State">الحالة.</param>
/// <param name="Movements">الحركات.</param>
/// <param name="AlreadyPosted">هل كان مُرحَّلاً قبل هذا النداء؟</param>
public sealed record HrProvision(
    Guid Id,
    string Number,
    string PeriodCode,
    DateOnly AccruedOn,
    string MeasurementRef,
    string ApprovedBy,
    decimal PeriodShare,
    string State,
    IReadOnlyList<HrProvisionMovement> Movements,
    bool AlreadyPosted);

/// <summary>طلب إنشاء مخالصة نهاية خدمة <b>مسوّدة</b>.</summary>
/// <param name="Number">رقم المخالصة.</param>
/// <param name="EmploymentId">علاقة العمل المنتهية.</param>
/// <param name="SettledOn">تاريخ المخالصة.</param>
/// <param name="SettlementDue">المستحقّ بحساب المخالصة المعتمد.</param>
/// <param name="MeasurementRef">مرجع أساس الحساب المعتمد.</param>
/// <param name="SettlementMethod">طريقة الصرف.</param>
/// <param name="TreasuryPartyId">طرف الخزينة — <b>إلزامي</b>.</param>
public sealed record HrSettlementRequest(
    string Number,
    Guid EmploymentId,
    DateOnly SettledOn,
    decimal SettlementDue,
    string MeasurementRef,
    string SettlementMethod,
    string TreasuryPartyId);

/// <summary>مخالصة كما تخرج من السطح — <b>وسيناريوها مُسمّى</b>.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="EmploymentId">علاقة العمل.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="SettledOn">تاريخ المخالصة.</param>
/// <param name="SettlementDue">المستحقّ.</param>
/// <param name="ProvisionBalance">رصيد المخصص المُرحَّل لهذه العلاقة.</param>
/// <param name="AmountPaid">المصروف.</param>
/// <param name="Shortfall">العجز.</param>
/// <param name="Excess">الزيادة.</param>
/// <param name="ProvisionUtilised">المخصص المستنفَد.</param>
/// <param name="ScenarioCode">‏<c>exact</c> · <c>short</c> · <c>excess</c>.</param>
/// <param name="MeasurementRef">مرجع أساس الحساب.</param>
/// <param name="SettlementMethod">طريقة الصرف.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="State">الحالة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّلت.</param>
/// <param name="AlreadyPosted">هل كانت مُرحَّلة قبل هذا النداء؟</param>
public sealed record HrSettlement(
    Guid Id,
    string Number,
    Guid EmploymentId,
    string EmployeeCode,
    DateOnly SettledOn,
    decimal SettlementDue,
    decimal ProvisionBalance,
    decimal AmountPaid,
    decimal Shortfall,
    decimal Excess,
    decimal ProvisionUtilised,
    string ScenarioCode,
    string MeasurementRef,
    string SettlementMethod,
    string TreasuryPartyId,
    string State,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>سطر انحراف في تقرير المطابقة كما يخرج من السطح.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="PartyId">الرمز المعتم للموظف.</param>
/// <param name="SubledgerEffect">أثره كما تعرفه الوحدة.</param>
/// <param name="ControlEffect">أثره في نقطة الضبط.</param>
/// <param name="Divergence">الفارق.</param>
/// <param name="ReasonCode">سبب الانحراف.</param>
public sealed record HrReconciliationDivergence(
    string DocumentType,
    string DocumentId,
    string PartyId,
    decimal SubledgerEffect,
    decimal ControlEffect,
    decimal Divergence,
    string ReasonCode);

/// <summary>
/// تقرير المطابقة كما يخرج من السطح — <b>ولا رقم فيه اسمه «رصيد الموظف»</b>.
/// </summary>
/// <param name="AsOf">تاريخ المطابقة.</param>
/// <param name="MatchedDocuments">عدد المستندات المتطابقة.</param>
/// <param name="IsReconciled">هل خلا التقرير من أي انحراف؟</param>
/// <param name="Divergences">المستندات المسؤولة عن الفارق.</param>
public sealed record HrReconciliation(
    DateOnly AsOf, int MatchedDocuments, bool IsReconciled, IReadOnlyList<HrReconciliationDivergence> Divergences);
