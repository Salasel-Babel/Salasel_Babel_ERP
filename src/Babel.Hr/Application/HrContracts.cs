using Babel.SharedKernel;

namespace Babel.Hr.Application;

/// <summary>
/// البيانات الشخصية عند التسجيل — <b>تُكتب في جدولها المنفصل ولا تعود من أي قراءة إلا
/// مقنَّعة</b>، ولا تعبر إلى ترحيل ولا إلى بيان قيد بحال.
/// </summary>
/// <param name="NationalId">رقم الهوية أو الإقامة.</param>
/// <param name="Iban">الآيبان.</param>
/// <param name="BirthDate">تاريخ الميلاد الميلادي.</param>
public sealed record EmployeeIdentityDraft(string NationalId, string Iban, DateOnly BirthDate);

/// <summary>
/// البيانات الشخصية <b>مقنَّعة</b>: آخر أربعة محارف وحدها، وما قبلها نجوم.
/// <para>
/// <b>ولا تعبر القيمة الكاملة هذا الحدّ في هذا التسليم إطلاقاً</b> — وذلك نقصُ سطحٍ
/// مُعلَن لا قرارُ منع: قراءةُ الهوية الكاملة تحتاج استحقاقاً <b>على مستوى الحقل</b>،
/// و<c>IEntitlementEnforcer</c> اليوم يحكم بوحدةٍ ونوع وصول لا بحقل. وبابٌ يُرجع الآيبان
/// كاملاً لكل من يقرأ ميزان مراجعة أسوأ من غياب الباب.
/// </para>
/// </summary>
/// <param name="NationalIdMask">قناع رقم الهوية.</param>
/// <param name="IbanMask">قناع الآيبان.</param>
public sealed record MaskedIdentityView(string NationalIdMask, string IbanMask);

/// <summary>مسوّدة تسجيل موظف. <b>ولا رمز فيها</b>: الرمز المعتم يولّده الخادم.</summary>
/// <param name="Name">الاسم — عربيّه سجلٌّ وترجماته صفوف (ADR-0021).</param>
/// <param name="ClassCode">تصنيف الاشتراك — مؤهّل صفّ الإعدادات، لا نسبة.</param>
/// <param name="CostCenterId">مركز التكلفة، أو فراغٌ فالافتراضي.</param>
/// <param name="HiredOn">تاريخ بدء علاقة العمل الأولى.</param>
/// <param name="Identity">البيانات الشخصية.</param>
public sealed record EmployeeDraft(
    TranslatedName Name,
    string ClassCode,
    string CostCenterId,
    DateOnly HiredOn,
    EmployeeIdentityDraft Identity);

/// <summary>الموظف كما يراه المستدعي — <b>بلا قيمة شخصية واحدة غير مقنَّعة</b>.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز المعتم — وهو ما يُكتب في الدفتر المساعد.</param>
/// <param name="Name">الاسم بسجلّه وترجماته.</param>
/// <param name="ClassCode">تصنيف الاشتراك.</param>
/// <param name="CostCenterId">مركز التكلفة كما سُجّل.</param>
/// <param name="EmploymentId">علاقة العمل الجارية أو الأخيرة.</param>
/// <param name="StartedOn">بدء علاقة العمل.</param>
/// <param name="EndedOn">انتهاؤها، أو <c>null</c> لعلاقة سارية.</param>
/// <param name="State">حالة علاقة العمل.</param>
/// <param name="Identity">الهوية مقنَّعة.</param>
public sealed record EmployeeView(
    Guid Id,
    string Code,
    TranslatedName Name,
    string ClassCode,
    string CostCenterId,
    Guid EmploymentId,
    DateOnly StartedOn,
    DateOnly? EndedOn,
    string State,
    MaskedIdentityView Identity);

/// <summary>مسوّدة مكوّن أجر — <b>تصنيفٌ لا مبلغ</b>.</summary>
/// <param name="Code">رمز المكوّن.</param>
/// <param name="Name">اسمه بسجلّه وترجماته.</param>
/// <param name="Kind">‏<c>earning</c> أو <c>deduction</c>.</param>
/// <param name="EntersContributoryWage">هل يدخل وعاء الاشتراك؟ يملؤه المحاسب.</param>
/// <param name="EntersEndOfServiceBase">هل يدخل وعاء نهاية الخدمة؟ يملؤه المحاسب.</param>
public sealed record PayComponentDraft(
    string Code, TranslatedName Name, string Kind, bool EntersContributoryWage, bool EntersEndOfServiceBase);

/// <summary>مكوّن أجر كما يُقرأ.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="Kind">النوع.</param>
/// <param name="EntersContributoryWage">وسم وعاء الاشتراك.</param>
/// <param name="EntersEndOfServiceBase">وسم وعاء نهاية الخدمة.</param>
public sealed record PayComponentView(
    Guid Id, string Code, TranslatedName Name, string Kind, bool EntersContributoryWage, bool EntersEndOfServiceBase);

/// <summary>إسناد قيمة مكوّن بتاريخ سريان — <b>إنشاءٌ لا تعديل</b>.</summary>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="Amount">القيمة.</param>
public sealed record PayElementDraft(string ComponentCode, DateOnly EffectiveFrom, Money Amount);

/// <summary>قيمة مكوّن كما تُقرأ بسريانها.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="Amount">القيمة.</param>
public sealed record PayElementView(Guid Id, string ComponentCode, DateOnly EffectiveFrom, Money Amount);

/// <summary>
/// إصدارٌ من نِسَب الاشتراك وحدوده — <b>الموضع الوحيد الذي تدخل منه نسبة إلى النظام</b>.
/// </summary>
/// <param name="ClassCode">تصنيف الاشتراك.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="EmployerRate">نسبة المنشأة كسراً عشرياً.</param>
/// <param name="EmployeeRate">نسبة الموظف كسراً عشرياً.</param>
/// <param name="MinimumContributoryWage">أدنى أجر خاضع.</param>
/// <param name="MaximumContributoryWage">أقصى أجر خاضع، أو صفر فلا سقف.</param>
/// <param name="ApprovedBy">من اعتمد — إنسان لا نظام.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد.</param>
/// <param name="SourceRef">مرجع المصدر النظامي — غير فارغ بقيد في القاعدة.</param>
public sealed record PayrollSettingsDraft(
    string ClassCode,
    DateOnly EffectiveFrom,
    decimal EmployerRate,
    decimal EmployeeRate,
    Money MinimumContributoryWage,
    Money MaximumContributoryWage,
    string ApprovedBy,
    DateOnly ApprovedOn,
    string SourceRef);

/// <summary>إصدار نِسَبٍ كما يُقرأ.</summary>
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
public sealed record PayrollSettingsView(
    Guid Id,
    string ClassCode,
    DateOnly EffectiveFrom,
    decimal EmployerRate,
    decimal EmployeeRate,
    Money MinimumContributoryWage,
    Money MaximumContributoryWage,
    string ApprovedBy,
    DateOnly ApprovedOn,
    string SourceRef);

/// <summary>مسوّدة مسيّر رواتب. <b>ولا مجاميع فيها</b>: الوحدة تحسب، والعميل لا يُملي.</summary>
/// <param name="Number">رقم المسيّر.</param>
/// <param name="PeriodCode">رمز الفترة <c>yyyy-MM</c>.</param>
/// <param name="PeriodStart">بداية الفترة.</param>
/// <param name="PeriodEnd">نهايتها — وهي التاريخ الذي يُرحَّل به الاستحقاق.</param>
public sealed record PayrollRunDraft(string Number, string PeriodCode, DateOnly PeriodStart, DateOnly PeriodEnd);

/// <summary>المبالغ الستّة على قسيمة أو مسيّر — بأسماء مفردات المصفوفة نفسها.</summary>
/// <param name="GrossEntitlements">إجمالي المستحقات.</param>
/// <param name="EmployerSocialInsurance">حصة المنشأة.</param>
/// <param name="EmployeeSocialInsurance">حصة الموظف.</param>
/// <param name="AdvanceInstalment">قسط السلفة المستقطع.</param>
/// <param name="Deductions">الخصومات والجزاءات.</param>
/// <param name="NetPayable">الصافي المستحق.</param>
public sealed record PayrollAmounts(
    Money GrossEntitlements,
    Money EmployerSocialInsurance,
    Money EmployeeSocialInsurance,
    Money AdvanceInstalment,
    Money Deductions,
    Money NetPayable);

/// <summary>مسيّر رواتب كما يُقرأ.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="PeriodStart">بدايتها.</param>
/// <param name="PeriodEnd">نهايتها.</param>
/// <param name="State">الحالة.</param>
/// <param name="Amounts">المجاميع.</param>
/// <param name="PayslipCount">عدد القسائم.</param>
public sealed record PayrollRunView(
    Guid Id,
    string Number,
    string PeriodCode,
    DateOnly PeriodStart,
    DateOnly PeriodEnd,
    string State,
    PayrollAmounts Amounts,
    int PayslipCount);

/// <summary>مكوّن على قسيمة كما يُقرأ.</summary>
/// <param name="LineNo">رقم السطر.</param>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="Kind">نوعه.</param>
/// <param name="EntersContributoryWage">هل دخل وعاء الاشتراك؟</param>
/// <param name="Amount">المبلغ.</param>
public sealed record PayslipComponentView(
    int LineNo, string ComponentCode, string Kind, bool EntersContributoryWage, Money Amount);

/// <summary>
/// قسيمة كما تُقرأ — <b>وهي مستند الترحيل</b>، فمعرّفها هو <c>DocumentId</c> في هوية
/// الإحكام و<c>EntryId</c> قيدُها هي وحدها.
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
public sealed record PayslipView(
    Guid Id,
    Guid RunId,
    Guid EmployeeId,
    Guid EmploymentId,
    string EmployeeCode,
    string CostCenterId,
    Money ContributoryWage,
    PayrollAmounts Amounts,
    IReadOnlyList<PayslipComponentView> Components,
    string State,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>مسوّدة سند صرف الرواتب على مسيّر مُرحَّل.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="RunId">المسيّر المُرحَّل.</param>
/// <param name="PaidOn">تاريخ الصرف.</param>
/// <param name="SettlementMethod">طريقة التسوية — مؤهّل دور.</param>
/// <param name="TreasuryPartyId">طرف الخزينة — <b>إلزامي</b>.</param>
public sealed record PayrollPaymentDraft(
    string Number, Guid RunId, DateOnly PaidOn, string SettlementMethod, string TreasuryPartyId);

/// <summary>سطر سند صرف كما يُقرأ — واحدٌ لكل قسيمة، ومعرّف قيده معه.</summary>
/// <param name="LineNo">رقم السطر.</param>
/// <param name="PayslipId">القسيمة.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="EntryId">معرّف قيد هذا السطر إن رُحّل.</param>
public sealed record PayrollPaymentLineView(
    int LineNo, Guid PayslipId, string EmployeeCode, Money Amount, Guid? EntryId);

/// <summary>سند صرف الرواتب كما يُقرأ.</summary>
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
public sealed record PayrollPaymentView(
    Guid Id,
    string Number,
    Guid RunId,
    DateOnly PaidOn,
    string SettlementMethod,
    string TreasuryPartyId,
    Money NetPayable,
    string State,
    IReadOnlyList<PayrollPaymentLineView> Lines,
    bool AlreadyPosted);

/// <summary>مسوّدة سداد التأمينات للفترة.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="PeriodCode">الفترة المسدَّدة.</param>
/// <param name="PaidOn">تاريخ السداد.</param>
/// <param name="Amount">المبلغ المسدَّد.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة — <b>إلزامي</b>.</param>
public sealed record SocialInsurancePaymentDraft(
    string Number, string PeriodCode, DateOnly PaidOn, Money Amount, string SettlementMethod, string TreasuryPartyId);

/// <summary>سداد تأمينات كما يُقرأ.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="PaidOn">تاريخ السداد.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="AccruedForPeriod">ما استُحقّ في هذه الفترة من مسيّرات مُرحَّلة — للمقارنة لا للإملاء.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="State">الحالة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل كان مُرحَّلاً قبل هذا النداء؟</param>
public sealed record SocialInsurancePaymentView(
    Guid Id,
    string Number,
    string PeriodCode,
    DateOnly PaidOn,
    Money Amount,
    Money AccruedForPeriod,
    string SettlementMethod,
    string TreasuryPartyId,
    string State,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>قيدٌ في سجلّ الجزاءات المعتمد. <b>ولا مورد ترحيل له.</b></summary>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="PeriodCode">فترة الاستقطاع.</param>
/// <param name="CategoryKey">مفتاح فئة السبب.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد.</param>
public sealed record EmployeeDeductionDraft(
    Guid EmployeeId, string PeriodCode, string CategoryKey, Money Amount, string ApprovedBy, DateOnly ApprovedOn);

/// <summary>
/// جزاءٌ كما يُقرأ. <b>ولا حقل <c>entryId</c> ولا <c>alreadyPosted</c> عليه</b> — حقلٌ
/// فارغ يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل»، فيبني عليه العميل شاشةً بزرّ ترحيل لا وجود له.
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
public sealed record EmployeeDeductionView(
    Guid Id,
    Guid EmployeeId,
    string EmployeeCode,
    string PeriodCode,
    string CategoryKey,
    Money Amount,
    string ApprovedBy,
    DateOnly ApprovedOn,
    Guid? ConsumedByPayslipId);

/// <summary>قسط سداد سلفة.</summary>
/// <param name="PeriodCode">الفترة التي يُستقطع فيها.</param>
/// <param name="Amount">المبلغ.</param>
public sealed record AdvanceInstalmentDraft(string PeriodCode, Money Amount);

/// <summary>مسوّدة سلفة موظف بجدول أقساطها.</summary>
/// <param name="Number">رقم السلفة.</param>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="IssuedOn">تاريخ المنح.</param>
/// <param name="Amount">مبلغ السلفة.</param>
/// <param name="SettlementMethod">طريقة الصرف — مؤهّل دور.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="Instalments">جدول الأقساط — مجموعه يساوي المبلغ بالضبط.</param>
public sealed record EmployeeAdvanceDraft(
    string Number,
    Guid EmployeeId,
    DateOnly IssuedOn,
    Money Amount,
    string SettlementMethod,
    string TreasuryPartyId,
    IReadOnlyList<AdvanceInstalmentDraft> Instalments);

/// <summary>قسط سلفة كما يُقرأ.</summary>
/// <param name="LineNo">رقم القسط.</param>
/// <param name="PeriodCode">فترته.</param>
/// <param name="Amount">مبلغه.</param>
/// <param name="ConsumedByPayslipId">القسيمة التي استُقطع فيها، أو <c>null</c>.</param>
public sealed record AdvanceInstalmentView(int LineNo, string PeriodCode, Money Amount, Guid? ConsumedByPayslipId);

/// <summary>
/// سلفة كما تُقرأ. <b>ولا حقل قيد عليها في هذا التسليم</b>: حدث صرف السلفة غير موجود
/// في مصفوفة الترحيل، فبابُ ترحيلها غير منشور — وحقلٌ فارغ كان سيَعِد بدورة لا تكتمل.
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="EmployeeId">الموظف.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="IssuedOn">تاريخ المنح.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="SettlementMethod">طريقة الصرف.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="OutstandingAmount">المتبقّي — مشتقٌّ من الأقساط <b>المستقطعة فعلاً</b>.</param>
/// <param name="State">الحالة.</param>
/// <param name="Instalments">جدول الأقساط.</param>
public sealed record EmployeeAdvanceView(
    Guid Id,
    string Number,
    Guid EmployeeId,
    string EmployeeCode,
    DateOnly IssuedOn,
    Money Amount,
    string SettlementMethod,
    string TreasuryPartyId,
    Money OutstandingAmount,
    string State,
    IReadOnlyList<AdvanceInstalmentView> Instalments);

/// <summary>حصّة علاقة عمل من مخصص الفترة — <b>مبلغٌ يُدخله معتمِد المستند</b>.</summary>
/// <param name="EmploymentId">علاقة العمل.</param>
/// <param name="PeriodShare">حصّة الفترة كما قاسها المحاسب.</param>
public sealed record ProvisionShareDraft(Guid EmploymentId, Money PeriodShare);

/// <summary>
/// مسوّدة استحقاق مخصص نهاية الخدمة لفترة.
/// <para>
/// <b>والوحدة لا تحسب حصّة الفترة ولا تعرف معادلتها</b>: طريقة قياس المخصص ومدخلاتها
/// بندٌ مفتوح يحتاج اعتماد المحاسب القانوني، ونصُّ المصفوفة على المبلغ صريح — «بطريقة
/// القياس المعتمدة، لا تُخترع في هذا التسليم». فالمبلغ يصل من معتمِد المستند ومعه
/// <paramref name="MeasurementRef"/> يسمّي الأساس، والوحدة تُثبت الحركة ولا تُقدّرها.
/// </para>
/// </summary>
/// <param name="Number">رقم المستند.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="AccruedOn">تاريخ الاستحقاق.</param>
/// <param name="MeasurementRef">مرجع أساس القياس المعتمد — غير فارغ بقيد في القاعدة.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="Shares">حصص علاقات العمل.</param>
public sealed record EndOfServiceProvisionDraft(
    string Number,
    string PeriodCode,
    DateOnly AccruedOn,
    string MeasurementRef,
    string ApprovedBy,
    IReadOnlyList<ProvisionShareDraft> Shares);

/// <summary>حركة مخصص كما تُقرأ.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="EmploymentId">علاقة العمل.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="PeriodShare">حصّة الفترة.</param>
/// <param name="EntryId">معرّف قيد هذه الحركة إن رُحّلت.</param>
public sealed record ProvisionMovementView(
    Guid Id, Guid EmploymentId, string EmployeeCode, Money PeriodShare, Guid? EntryId);

/// <summary>مستند استحقاق المخصص كما يُقرأ.</summary>
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
public sealed record EndOfServiceProvisionView(
    Guid Id,
    string Number,
    string PeriodCode,
    DateOnly AccruedOn,
    string MeasurementRef,
    string ApprovedBy,
    Money PeriodShare,
    string State,
    IReadOnlyList<ProvisionMovementView> Movements,
    bool AlreadyPosted);

/// <summary>
/// مسوّدة مخالصة نهاية خدمة.
/// <para>
/// <b>والمستحقّ يصل من معتمِد المستند</b> — معادلة المكافأة وشرائحها بندٌ مفتوح لا
/// يُخترع هنا. والوحدة تحسب من عندها <b>رصيد المخصص</b> وحده، وهو مجموع حركات هذه
/// العلاقة المُرحَّلة، ثم تشتقّ العجز والزيادة والسيناريو حسابياً.
/// </para>
/// </summary>
/// <param name="Number">رقم المخالصة.</param>
/// <param name="EmploymentId">علاقة العمل المنتهية.</param>
/// <param name="SettledOn">تاريخ المخالصة.</param>
/// <param name="SettlementDue">المستحقّ بحساب المخالصة المعتمد.</param>
/// <param name="MeasurementRef">مرجع أساس الحساب المعتمد.</param>
/// <param name="SettlementMethod">طريقة الصرف.</param>
/// <param name="TreasuryPartyId">طرف الخزينة — <b>إلزامي</b>.</param>
public sealed record EndOfServiceSettlementDraft(
    string Number,
    Guid EmploymentId,
    DateOnly SettledOn,
    Money SettlementDue,
    string MeasurementRef,
    string SettlementMethod,
    string TreasuryPartyId);

/// <summary>مخالصة كما تُقرأ — <b>وسيناريوها مُسمّى</b> لا مستنتَجاً من فرق مبلغين.</summary>
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
public sealed record EndOfServiceSettlementView(
    Guid Id,
    string Number,
    Guid EmploymentId,
    string EmployeeCode,
    DateOnly SettledOn,
    Money SettlementDue,
    Money ProvisionBalance,
    Money AmountPaid,
    Money Shortfall,
    Money Excess,
    Money ProvisionUtilised,
    string ScenarioCode,
    string MeasurementRef,
    string SettlementMethod,
    string TreasuryPartyId,
    string State,
    Guid? EntryId,
    bool AlreadyPosted);
