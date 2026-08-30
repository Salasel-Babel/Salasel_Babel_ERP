namespace Babel.Api.Wire;

/// <summary>
/// البيانات الشخصية على السلك عند التسجيل — <b>وتدخل ولا تعود</b>.
/// <para>
/// ولا تعبر إلى الدفتر بحال: كل ما يدخل <c>ledger.*</c> يدخل البايتات المُجزَّأة،
/// و<c>REVOKE UPDATE, DELETE</c> يجعله غير قابل للإزالة.
/// </para>
/// </summary>
internal sealed record HrIdentityRequestDto
{
    /// <summary>رقم الهوية أو الإقامة.</summary>
    public required string NationalId { get; init; }

    /// <summary>الآيبان.</summary>
    public required string Iban { get; init; }

    /// <summary>تاريخ الميلاد الميلادي بصيغة <c>yyyy-MM-dd</c>.</summary>
    public required string BirthDate { get; init; }
}

/// <summary>الهوية <b>مقنَّعة</b> كما تخرج على السلك.</summary>
/// <param name="NationalIdMask">قناع رقم الهوية.</param>
/// <param name="IbanMask">قناع الآيبان.</param>
internal sealed record HrMaskedIdentityDto(string NationalIdMask, string IbanMask);

/// <summary>طلب تسجيل موظف. <b>ولا رمز فيه</b> — الخادم يولّده معتماً.</summary>
internal sealed record HrEmployeeRequestDto
{
    /// <summary>الاسم العربي — السجلّ.</summary>
    public required string NameAr { get; init; }

    /// <summary>ترجمات الاسم بوسم BCP-47، أو غياب.</summary>
    public IReadOnlyList<NameValueDto>? NameTranslations { get; init; }

    /// <summary>تصنيف الاشتراك — مؤهّل صفّ الإعدادات، لا نسبة.</summary>
    public required string ClassCode { get; init; }

    /// <summary>مركز التكلفة، أو فراغٌ فالافتراضي.</summary>
    public required string CostCenterId { get; init; }

    /// <summary>تاريخ بدء علاقة العمل.</summary>
    public required string HiredOn { get; init; }

    /// <summary>البيانات الشخصية.</summary>
    public required HrIdentityRequestDto Identity { get; init; }
}

/// <summary>الموظف كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز المعتم — وهو ما يُكتب في الدفتر المساعد.</param>
/// <param name="NameAr">الاسم العربي — السجلّ.</param>
/// <param name="NameTranslations">الترجمات، مرتَّبة ترتيباً حرفياً ثابتاً.</param>
/// <param name="ClassCode">تصنيف الاشتراك.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="EmploymentId">علاقة العمل الجارية أو الأخيرة.</param>
/// <param name="StartedOn">بدؤها.</param>
/// <param name="EndedOn">انتهاؤها، أو <c>null</c>.</param>
/// <param name="State">حالتها.</param>
/// <param name="Identity">الهوية مقنَّعة.</param>
internal sealed record HrEmployeeDto(
    string Id,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string ClassCode,
    string CostCenterId,
    string EmploymentId,
    string StartedOn,
    string? EndedOn,
    string State,
    HrMaskedIdentityDto Identity);

/// <summary>طلب إنهاء خدمة — مورد فرعي لا حقل حالة.</summary>
internal sealed record HrTerminationRequestDto
{
    /// <summary>تاريخ انتهاء الخدمة.</summary>
    public required string EndedOn { get; init; }

    /// <summary>مفتاح سبب الإنهاء — رمزٌ يقرؤه برنامج لا نصٌّ يُعرض.</summary>
    public required string ReasonKey { get; init; }
}

/// <summary>طلب تعريف مكوّن أجر — <b>تصنيفٌ لا مبلغ</b>.</summary>
internal sealed record HrPayComponentRequestDto
{
    /// <summary>رمز المكوّن.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم العربي — السجلّ.</summary>
    public required string NameAr { get; init; }

    /// <summary>الترجمات.</summary>
    public IReadOnlyList<NameValueDto>? NameTranslations { get; init; }

    /// <summary>‏<c>earning</c> أو <c>deduction</c>.</summary>
    public required string Kind { get; init; }

    /// <summary>هل يدخل وعاء الاشتراك؟ يملؤه المحاسب لا المبرمج.</summary>
    public required bool EntersContributoryWage { get; init; }

    /// <summary>هل يدخل وعاء نهاية الخدمة؟ يملؤه المحاسب لا المبرمج.</summary>
    public required bool EntersEndOfServiceBase { get; init; }
}

/// <summary>مكوّن أجر كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">الترجمات.</param>
/// <param name="Kind">النوع.</param>
/// <param name="EntersContributoryWage">وسم وعاء الاشتراك.</param>
/// <param name="EntersEndOfServiceBase">وسم وعاء نهاية الخدمة.</param>
internal sealed record HrPayComponentDto(
    string Id,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string Kind,
    bool EntersContributoryWage,
    bool EntersEndOfServiceBase);

/// <summary>طلب إسناد قيمة مكوّن بتاريخ سريان — إنشاءٌ لا تعديل.</summary>
internal sealed record HrPayElementRequestDto
{
    /// <summary>رمز المكوّن.</summary>
    public required string ComponentCode { get; init; }

    /// <summary>تاريخ السريان.</summary>
    public required string EffectiveFrom { get; init; }

    /// <summary>القيمة — <b>نصّاً</b> لا رمزاً رقمياً.</summary>
    public required WireDecimal Amount { get; init; }
}

/// <summary>قيمة مكوّن كما تخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="EffectiveFrom">تاريخ السريان.</param>
/// <param name="Amount">القيمة نصّاً.</param>
internal sealed record HrPayElementDto(string Id, string ComponentCode, string EffectiveFrom, string Amount);

/// <summary>طلب إيداع إصدار من نِسَب الاشتراك وحدودها.</summary>
internal sealed record HrPayrollSettingsRequestDto
{
    /// <summary>تصنيف الاشتراك.</summary>
    public required string ClassCode { get; init; }

    /// <summary>تاريخ السريان.</summary>
    public required string EffectiveFrom { get; init; }

    /// <summary>نسبة المنشأة كسراً عشرياً بمقياس ثمانٍ — <b>نصّاً</b>.</summary>
    public required WireDecimal EmployerRate { get; init; }

    /// <summary>نسبة الموظف كسراً عشرياً بمقياس ثمانٍ — <b>نصّاً</b>.</summary>
    public required WireDecimal EmployeeRate { get; init; }

    /// <summary>أدنى أجر خاضع.</summary>
    public required WireDecimal MinimumContributoryWage { get; init; }

    /// <summary>أقصى أجر خاضع، أو صفر فلا سقف.</summary>
    public required WireDecimal MaximumContributoryWage { get; init; }

    /// <summary>من اعتمد — إنسان لا نظام.</summary>
    public required string ApprovedBy { get; init; }

    /// <summary>تاريخ الاعتماد.</summary>
    public required string ApprovedOn { get; init; }

    /// <summary>مرجع المصدر النظامي — غير فارغ بقيد في القاعدة.</summary>
    public required string SourceRef { get; init; }
}

/// <summary>إصدار نِسَبٍ كما يخرج على السلك.</summary>
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
internal sealed record HrPayrollSettingsDto(
    string Id,
    string ClassCode,
    string EffectiveFrom,
    string EmployerRate,
    string EmployeeRate,
    string MinimumContributoryWage,
    string MaximumContributoryWage,
    string ApprovedBy,
    string ApprovedOn,
    string SourceRef);

/// <summary>طلب إنشاء مسيّر رواتب <b>مسوّدة</b>. <b>ولا مجاميع فيه.</b></summary>
internal sealed record HrPayrollRunRequestDto
{
    /// <summary>رقم المسيّر.</summary>
    public required string Number { get; init; }

    /// <summary>رمز الفترة <c>yyyy-MM</c>.</summary>
    public required string PeriodCode { get; init; }

    /// <summary>بداية الفترة.</summary>
    public required string PeriodStart { get; init; }

    /// <summary>نهايتها — وهي تاريخ قيد الاستحقاق.</summary>
    public required string PeriodEnd { get; init; }
}

/// <summary>المبالغ الستّة على السلك — كلّها نصوص.</summary>
/// <param name="GrossEntitlements">إجمالي المستحقات.</param>
/// <param name="EmployerSocialInsurance">حصة المنشأة.</param>
/// <param name="EmployeeSocialInsurance">حصة الموظف.</param>
/// <param name="AdvanceInstalment">قسط السلفة المستقطع.</param>
/// <param name="Deductions">الخصومات والجزاءات.</param>
/// <param name="NetPayable">الصافي المستحق.</param>
internal sealed record HrPayrollAmountsDto(
    string GrossEntitlements,
    string EmployerSocialInsurance,
    string EmployeeSocialInsurance,
    string AdvanceInstalment,
    string Deductions,
    string NetPayable);

/// <summary>مسيّر رواتب كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="PeriodCode">الفترة.</param>
/// <param name="PeriodStart">بدايتها.</param>
/// <param name="PeriodEnd">نهايتها.</param>
/// <param name="State">الحالة.</param>
/// <param name="Amounts">المجاميع.</param>
/// <param name="PayslipCount">عدد القسائم.</param>
internal sealed record HrPayrollRunDto(
    string Id,
    string Number,
    string PeriodCode,
    string PeriodStart,
    string PeriodEnd,
    string State,
    HrPayrollAmountsDto Amounts,
    int PayslipCount);

/// <summary>مكوّن على قسيمة كما يخرج على السلك.</summary>
/// <param name="LineNo">رقم السطر.</param>
/// <param name="ComponentCode">رمز المكوّن.</param>
/// <param name="Kind">نوعه.</param>
/// <param name="EntersContributoryWage">هل دخل وعاء الاشتراك؟</param>
/// <param name="Amount">المبلغ.</param>
internal sealed record HrPayslipComponentDto(
    int LineNo, string ComponentCode, string Kind, bool EntersContributoryWage, string Amount);

/// <summary>القسيمة كما تخرج على السلك — <b>وهي مستند الترحيل</b>.</summary>
/// <param name="Id">المعرّف — وهو <c>DocumentId</c> في هوية الإحكام.</param>
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
internal sealed record HrPayslipDto(
    string Id,
    string RunId,
    string EmployeeId,
    string EmploymentId,
    string EmployeeCode,
    string CostCenterId,
    string ContributoryWage,
    HrPayrollAmountsDto Amounts,
    IReadOnlyList<HrPayslipComponentDto> Components,
    string State,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>طلب إنشاء سند صرف رواتب <b>مسوّدة</b>.</summary>
internal sealed record HrPayrollPaymentRequestDto
{
    /// <summary>رقم السند.</summary>
    public required string Number { get; init; }

    /// <summary>المسيّر المُرحَّل.</summary>
    public required string RunId { get; init; }

    /// <summary>تاريخ الصرف.</summary>
    public required string PaidOn { get; init; }

    /// <summary>طريقة التسوية — مؤهّل دور لا رمز حساب.</summary>
    public required string SettlementMethod { get; init; }

    /// <summary>طرف الخزينة — <b>إلزامي</b>، وبدونه يُرفض الترحيل عند المحرك.</summary>
    public required string TreasuryPartyId { get; init; }
}

/// <summary>سطر سند صرف كما يخرج على السلك.</summary>
/// <param name="LineNo">رقم السطر.</param>
/// <param name="PayslipId">القسيمة.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="EntryId">معرّف قيد هذا السطر إن رُحّل.</param>
internal sealed record HrPayrollPaymentLineDto(
    int LineNo, string PayslipId, string EmployeeCode, string Amount, string? EntryId);

/// <summary>سند صرف الرواتب كما يخرج على السلك.</summary>
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
internal sealed record HrPayrollPaymentDto(
    string Id,
    string Number,
    string RunId,
    string PaidOn,
    string SettlementMethod,
    string TreasuryPartyId,
    string NetPayable,
    string State,
    IReadOnlyList<HrPayrollPaymentLineDto> Lines,
    bool AlreadyPosted);

/// <summary>طلب إنشاء سند سداد تأمينات <b>مسوّدة</b>.</summary>
internal sealed record HrSocialInsurancePaymentRequestDto
{
    /// <summary>رقم السند.</summary>
    public required string Number { get; init; }

    /// <summary>الفترة المسدَّدة.</summary>
    public required string PeriodCode { get; init; }

    /// <summary>تاريخ السداد.</summary>
    public required string PaidOn { get; init; }

    /// <summary>المبلغ المسدَّد — نصّاً.</summary>
    public required WireDecimal Amount { get; init; }

    /// <summary>طريقة التسوية.</summary>
    public required string SettlementMethod { get; init; }

    /// <summary>طرف الخزينة — <b>إلزامي</b>.</summary>
    public required string TreasuryPartyId { get; init; }
}

/// <summary>سداد تأمينات كما يخرج على السلك.</summary>
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
internal sealed record HrSocialInsurancePaymentDto(
    string Id,
    string Number,
    string PeriodCode,
    string PaidOn,
    string Amount,
    string AccruedForPeriod,
    string SettlementMethod,
    string TreasuryPartyId,
    string State,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>طلب قيد جزاء في السجلّ المعتمد.</summary>
internal sealed record HrDeductionRequestDto
{
    /// <summary>الموظف.</summary>
    public required string EmployeeId { get; init; }

    /// <summary>فترة الاستقطاع.</summary>
    public required string PeriodCode { get; init; }

    /// <summary>مفتاح فئة السبب — رمزٌ يملكه المستدعي لا نصٌّ يُعرض.</summary>
    public required string CategoryKey { get; init; }

    /// <summary>المبلغ — نصّاً.</summary>
    public required WireDecimal Amount { get; init; }

    /// <summary>المعتمِد.</summary>
    public required string ApprovedBy { get; init; }

    /// <summary>تاريخ الاعتماد.</summary>
    public required string ApprovedOn { get; init; }
}

/// <summary>
/// جزاء كما يخرج على السلك — <b>بلا <c>entryId</c> وبلا <c>alreadyPosted</c></b>.
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
internal sealed record HrDeductionDto(
    string Id,
    string EmployeeId,
    string EmployeeCode,
    string PeriodCode,
    string CategoryKey,
    string Amount,
    string ApprovedBy,
    string ApprovedOn,
    string? ConsumedByPayslipId);

/// <summary>قسط سداد سلفة في الطلب.</summary>
internal sealed record HrInstalmentRequestDto
{
    /// <summary>الفترة التي يُستقطع فيها.</summary>
    public required string PeriodCode { get; init; }

    /// <summary>المبلغ — نصّاً.</summary>
    public required WireDecimal Amount { get; init; }
}

/// <summary>طلب إنشاء سلفة <b>مسوّدة</b> بجدول أقساطها.</summary>
internal sealed record HrAdvanceRequestDto
{
    /// <summary>رقم السلفة.</summary>
    public required string Number { get; init; }

    /// <summary>الموظف.</summary>
    public required string EmployeeId { get; init; }

    /// <summary>تاريخ المنح.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>مبلغ السلفة — نصّاً.</summary>
    public required WireDecimal Amount { get; init; }

    /// <summary>طريقة الصرف.</summary>
    public required string SettlementMethod { get; init; }

    /// <summary>طرف الخزينة.</summary>
    public required string TreasuryPartyId { get; init; }

    /// <summary>جدول الأقساط — مجموعه يساوي المبلغ بالضبط.</summary>
    public required IReadOnlyList<HrInstalmentRequestDto> Instalments { get; init; }
}

/// <summary>قسط سلفة كما يخرج على السلك.</summary>
/// <param name="LineNo">رقم القسط.</param>
/// <param name="PeriodCode">فترته.</param>
/// <param name="Amount">مبلغه.</param>
/// <param name="ConsumedByPayslipId">القسيمة التي استُقطع فيها، أو <c>null</c>.</param>
internal sealed record HrInstalmentDto(int LineNo, string PeriodCode, string Amount, string? ConsumedByPayslipId);

/// <summary>سلفة كما تخرج على السلك — <b>بلا حقل قيد</b>.</summary>
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
internal sealed record HrAdvanceDto(
    string Id,
    string Number,
    string EmployeeId,
    string EmployeeCode,
    string IssuedOn,
    string Amount,
    string SettlementMethod,
    string TreasuryPartyId,
    string OutstandingAmount,
    string State,
    IReadOnlyList<HrInstalmentDto> Instalments);

/// <summary>حصّة علاقة عمل من مخصص الفترة — <b>مبلغٌ يُدخله معتمِد المستند</b>.</summary>
internal sealed record HrProvisionShareRequestDto
{
    /// <summary>علاقة العمل.</summary>
    public required string EmploymentId { get; init; }

    /// <summary>حصّة الفترة كما قاسها المحاسب — نصّاً.</summary>
    public required WireDecimal PeriodShare { get; init; }
}

/// <summary>طلب إنشاء مستند استحقاق مخصص نهاية الخدمة <b>مسوّدة</b>.</summary>
internal sealed record HrProvisionRequestDto
{
    /// <summary>رقم المستند.</summary>
    public required string Number { get; init; }

    /// <summary>الفترة.</summary>
    public required string PeriodCode { get; init; }

    /// <summary>تاريخ الاستحقاق.</summary>
    public required string AccruedOn { get; init; }

    /// <summary>مرجع أساس القياس المعتمد — غير فارغ.</summary>
    public required string MeasurementRef { get; init; }

    /// <summary>المعتمِد.</summary>
    public required string ApprovedBy { get; init; }

    /// <summary>حصص علاقات العمل.</summary>
    public required IReadOnlyList<HrProvisionShareRequestDto> Shares { get; init; }
}

/// <summary>حركة مخصص كما تخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="EmploymentId">علاقة العمل.</param>
/// <param name="EmployeeCode">الرمز المعتم.</param>
/// <param name="PeriodShare">حصّة الفترة.</param>
/// <param name="EntryId">معرّف قيد هذه الحركة إن رُحّلت.</param>
internal sealed record HrProvisionMovementDto(
    string Id, string EmploymentId, string EmployeeCode, string PeriodShare, string? EntryId);

/// <summary>مستند استحقاق المخصص كما يخرج على السلك.</summary>
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
internal sealed record HrProvisionDto(
    string Id,
    string Number,
    string PeriodCode,
    string AccruedOn,
    string MeasurementRef,
    string ApprovedBy,
    string PeriodShare,
    string State,
    IReadOnlyList<HrProvisionMovementDto> Movements,
    bool AlreadyPosted);

/// <summary>طلب إنشاء مخالصة نهاية خدمة <b>مسوّدة</b>.</summary>
internal sealed record HrSettlementRequestDto
{
    /// <summary>رقم المخالصة.</summary>
    public required string Number { get; init; }

    /// <summary>علاقة العمل المنتهية.</summary>
    public required string EmploymentId { get; init; }

    /// <summary>تاريخ المخالصة.</summary>
    public required string SettledOn { get; init; }

    /// <summary>المستحقّ بحساب المخالصة المعتمد — نصّاً.</summary>
    public required WireDecimal SettlementDue { get; init; }

    /// <summary>مرجع أساس الحساب المعتمد.</summary>
    public required string MeasurementRef { get; init; }

    /// <summary>طريقة الصرف.</summary>
    public required string SettlementMethod { get; init; }

    /// <summary>طرف الخزينة — <b>إلزامي</b>.</summary>
    public required string TreasuryPartyId { get; init; }
}

/// <summary>مخالصة كما تخرج على السلك — <b>وسيناريوها مُسمّى</b>.</summary>
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
internal sealed record HrSettlementDto(
    string Id,
    string Number,
    string EmploymentId,
    string EmployeeCode,
    string SettledOn,
    string SettlementDue,
    string ProvisionBalance,
    string AmountPaid,
    string Shortfall,
    string Excess,
    string ProvisionUtilised,
    string ScenarioCode,
    string MeasurementRef,
    string SettlementMethod,
    string TreasuryPartyId,
    string State,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>سطر انحراف في تقرير المطابقة كما يخرج على السلك.</summary>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="PartyId">الرمز المعتم للموظف.</param>
/// <param name="SubledgerEffect">أثره كما تعرفه الوحدة.</param>
/// <param name="ControlEffect">أثره في نقطة الضبط.</param>
/// <param name="Divergence">الفارق.</param>
/// <param name="ReasonCode">سبب الانحراف.</param>
internal sealed record HrReconciliationDivergenceDto(
    string DocumentType,
    string DocumentId,
    string PartyId,
    string SubledgerEffect,
    string ControlEffect,
    string Divergence,
    string ReasonCode);

/// <summary>
/// تقرير المطابقة كما يخرج على السلك — <b>ولا رقم فيه اسمه «رصيد الموظف»</b>.
/// </summary>
/// <param name="AsOf">تاريخ المطابقة.</param>
/// <param name="MatchedDocuments">عدد المستندات المتطابقة.</param>
/// <param name="IsReconciled">هل خلا التقرير من أي انحراف؟</param>
/// <param name="Divergences">المستندات المسؤولة عن الفارق.</param>
internal sealed record HrReconciliationDto(
    string AsOf, int MatchedDocuments, bool IsReconciled, IReadOnlyList<HrReconciliationDivergenceDto> Divergences);

/// <summary>
/// تصنيفات مكوّنات الأجر — <b>غلافٌ لا مصفوفة عارية</b>: مصفوفةٌ في جذر الاستجابة لا
/// موضع فيها لعدّاد ولا لصفحة، فأول حاجة إليهما تكسر العقد.
/// </summary>
/// <param name="ItemCount">عدد المكوّنات.</param>
/// <param name="Items">المكوّنات، مرتَّبة بالرمز.</param>
internal sealed record HrPayComponentListDto(int ItemCount, IReadOnlyList<HrPayComponentDto> Items);

/// <summary>قيم مكوّنات موظف بسريانها — غلافٌ لا مصفوفة عارية.</summary>
/// <param name="ItemCount">عدد الصفوف.</param>
/// <param name="Items">الصفوف، مرتَّبة بالمكوّن ثم بتاريخ السريان.</param>
internal sealed record HrPayElementListDto(int ItemCount, IReadOnlyList<HrPayElementDto> Items);

/// <summary>إصدارات النِّسَب بسريانها — غلافٌ لا مصفوفة عارية.</summary>
/// <param name="ItemCount">عدد الإصدارات.</param>
/// <param name="Items">الإصدارات، مرتَّبة بالتصنيف ثم بتاريخ السريان.</param>
internal sealed record HrPayrollSettingsListDto(int ItemCount, IReadOnlyList<HrPayrollSettingsDto> Items);

/// <summary>
/// قسائم مسيّر — غلافٌ لا مصفوفة عارية، <b>وهو أيضاً جواب باب الترحيل</b>: نداءٌ واحد
/// يُصدر قيداً لكل قسيمة، فالجواب قائمة قسائم لكلٍّ معرّف قيدها وحصانتها.
/// </summary>
/// <param name="ItemCount">عدد القسائم.</param>
/// <param name="Items">القسائم، مرتَّبة بالرمز المعتم.</param>
internal sealed record HrPayslipListDto(int ItemCount, IReadOnlyList<HrPayslipDto> Items);
