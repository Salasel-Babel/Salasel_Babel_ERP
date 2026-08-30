namespace Babel.Hr.Persistence;

/// <summary>حالة مستند موارد بشرية. النصّ لا التعداد: القيمة تُقرأ في تقرير SQL مباشرةً.</summary>
internal static class HrDocumentState
{
    /// <summary>مسوّدة — لا قيد ولا أثر في الدفتر.</summary>
    public const string Draft = "DRAFT";

    /// <summary>مُرحَّل — واقعة محاسبية لها قيد.</summary>
    public const string Posted = "POSTED";
}

/// <summary>حالة محاولة ترحيل مستند — نسخة حرفية من الشكل المعتمد في الوحدات الثلاث.</summary>
internal static class PostingAttemptState
{
    /// <summary>سُجّلت النية ولم يُعرف مصير النداء بعد — الحالة القابلة لإعادة المحاولة.</summary>
    public const string Attempting = "ATTEMPTING";

    /// <summary>رُحّل فعلاً وللمحرك إيصال.</summary>
    public const string Posted = "POSTED";

    /// <summary>رفضه المحرك، والسبب محفوظ. المستند باقٍ على حاله ويُعاد بلا ازدواج.</summary>
    public const string Refused = "REFUSED";
}

/// <summary>حالة علاقة العمل.</summary>
internal static class EmploymentState
{
    /// <summary>سارية — تدخل المسيّر وتستحقّ المخصص.</summary>
    public const string Active = "ACTIVE";

    /// <summary>منتهية — لا تدخل مسيّراً بعد تاريخ انتهائها، وتفتح المخالصة.</summary>
    public const string Terminated = "TERMINATED";
}

/// <summary>
/// الموظف — <b>ولا حقل شخصي واحد على هذا الصفّ</b>.
/// <para>
/// <b>و<see cref="Code"/> بديلٌ معتم يولّده الخادم</b>، وهو وحده ما يُكتب في
/// <c>subledger_party_id</c> في دفتر الأستاذ. ولا يُشتقّ من هوية وطنية ولا من رقم
/// وظيفي ولا من اسم ولا من تسلسل، ولا يُقرأ منه شيء عن صاحبه: كل ما يدخل
/// <c>ledger.*</c> يدخل <b>البايتات المُجزَّأة</b> وتمنع <c>REVOKE UPDATE, DELETE</c>
/// إزالته، وعلاجُ المحو الموعود في ADR-0046 — تعميةٌ بمفتاح يُتلَف — لا يبلغ بايتات
/// دخلت سلسلة تجزئة أصلاً (تغييرها يكسر السلسلة).
/// </para>
/// </summary>
internal sealed class EmployeeRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>الرمز المعتم — الطرف في الدفتر المساعد. يولّده الخادم ولا يرسله العميل.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// الاسم العربي — <b>السجلّ</b> (ADR-0021). والترجمات صفوفٌ في
    /// <see cref="EmployeeNameTranslationRow"/> لا عمودٌ ثابت للإنجليزية: عمودٌ ثابت
    /// يجعل اللغة الثالثة هجرةَ مخطّط، ويخلط «لا ترجمة» بـ«ترجمةٌ فارغة».
    /// <b>ولا يعبر هذا الحقل إلى الدفتر بحال</b>.
    /// </summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>
    /// مركز التكلفة الذي يُحمَّل عليه أجر هذا الموظف. <b>واحدٌ لا أكثر</b>: مسار القالب
    /// يقرأ الأبعاد من قاموسٍ واحد لكل طلب، فتوزيعُ موظفٍ واحد على وعاءين لا يمثّله.
    /// وفارغٌ يعني «الافتراضي» ويحلّه <c>ICostCenterResolver</c>.
    /// </summary>
    public string CostCenterId { get; set; } = string.Empty;

    /// <summary>تصنيف الاشتراك — مؤهّل صفّ الإعدادات، لا نسبة ولا سقف.</summary>
    public string ClassCode { get; set; } = string.Empty;

    public bool IsActive { get; set; }

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// البيانات الشخصية للموظف — <b>جدولٌ منفصل واحدٌ لواحد، ولا يخرج منه شيء إلى ترحيل
/// ولا إلى بيان قيد ولا إلى وصف سطر</b>.
/// <para>
/// وفصلُه ليس ترتيباً: هو ما يجعل نقلَه أو تعميتَه أو محوَه — حين يُحسم ADR-0009 و ح-4 —
/// عمليةً على جدولٍ واحد لا على الوحدة كلّها.
/// </para>
/// </summary>
internal sealed class EmployeeIdentityRow
{
    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    /// <summary>رقم الهوية أو الإقامة. لا يعبر حدّ هذا الجدول إلا مقنَّعاً.</summary>
    public string NationalId { get; set; } = string.Empty;

    /// <summary>الآيبان. لا يعبر حدّ هذا الجدول إلا مقنَّعاً.</summary>
    public string Iban { get; set; } = string.Empty;

    public DateOnly BirthDate { get; set; }
}

/// <summary>ترجمة اسم موظف — صفٌّ لا عمود (القاعدة 14 · ADR-0021).</summary>
internal sealed class EmployeeNameTranslationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>الرمز المعتم للموظف المُترجَم اسمه.</summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>وسم اللغة BCP-47.</summary>
    public string Locale { get; set; } = string.Empty;

    /// <summary>النصّ المترجَم.</summary>
    public string Text { get; set; } = string.Empty;
}

/// <summary>
/// علاقة عمل. <b>وحبيبيّة مخصص نهاية الخدمة هي هذه لا الموظف</b>: من يعود بعد انقطاع
/// يبدأ استحقاقاً جديداً، ومخالصة الأولى لا تُخصم من رصيد الثانية.
/// </summary>
internal sealed class EmploymentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    public DateOnly StartedOn { get; set; }

    /// <summary>تاريخ الانتهاء، أو <c>null</c> لعلاقة سارية.</summary>
    public DateOnly? EndedOn { get; set; }

    /// <summary>
    /// مفتاح سبب الإنهاء — رمزٌ يقرؤه برنامج من مجموعة يملكها المستدعي، لا نصّ يُعرض.
    /// <b>ولا يُصنَّف هنا إلى «استقالة» و«إنهاء»</b>: أثر التمييز على الاستحقاق بندٌ
    /// مفتوح على المالك، وتصنيفٌ يفترض جوابه يُبنى عليه حساب.
    /// </summary>
    public string TerminationReasonKey { get; set; } = string.Empty;

    public string State { get; set; } = EmploymentState.Active;
}

/// <summary>
/// مكوّن أجر — <b>تصنيفٌ لا مبلغ ولا نسبة</b>.
/// <para>
/// والوسمان هما الموضع الذي يصير فيه الأثر التنظيمي <b>بياناتٍ يملؤها المحاسب</b> لا
/// شيفرةً: أي المكوّنات تدخل وعاء الاشتراك، وأيّها يدخل وعاء نهاية الخدمة.
/// </para>
/// </summary>
internal sealed class PayComponentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    /// <summary>الاسم العربي — السجلّ.</summary>
    public string NameAr { get; set; } = string.Empty;

    /// <summary>‏<c>earning</c> استحقاق أو <c>deduction</c> استقطاع.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>هل يدخل وعاء اشتراك التأمينات؟ يملؤه المحاسب لا المبرمج.</summary>
    public bool EntersContributoryWage { get; set; }

    /// <summary>هل يدخل وعاء مكافأة نهاية الخدمة؟ يملؤه المحاسب لا المبرمج.</summary>
    public bool EntersEndOfServiceBase { get; set; }
}

/// <summary>
/// إسناد قيمة مكوّن أجر بتاريخ سريان — <b>يُضاف إليه ولا يُعدَّل</b>.
/// <para>
/// والزيادة صفٌّ جديد بتاريخ سريان جديد، وإلا استحال إعادةُ حساب مسيّرٍ ماضٍ ليطابق
/// قيده المُرحَّل — وهو الفرق بين نظامٍ يُراجَع ونظامٍ يُصدَّق.
/// </para>
/// </summary>
internal sealed class PayElementRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid EmploymentId { get; set; }

    public string ComponentCode { get; set; } = string.Empty;

    public DateOnly EffectiveFrom { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// إعدادات اشتراك التأمينات — <b>الموضع الوحيد الذي تدخل منه نسبة إلى هذا النظام،
/// ويُسلَّم فارغاً</b>.
/// <para>
/// ولا قيمة افتراضية واحدة، ولا صفر صامت: مسيّرٌ لفترةٍ لا يغطّيها صفٌّ معتمد
/// <b>يُرفض رفضاً صريحاً</b> برمز <c>hr.payroll_settings_missing</c> يسمّي البند م-14.
/// والصفّ يُضاف ولا يُعدَّل: نسبة فترةٍ ماضية لا تُغيَّر، وإلا تعذّر إثبات مسيّر مُرحَّل.
/// </para>
/// </summary>
internal sealed class PayrollSettingsRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>تصنيف الاشتراك كما يسمّيه المحاسب — مؤهّلٌ لا معنى محاسبياً في الشيفرة.</summary>
    public string ClassCode { get; set; } = string.Empty;

    public DateOnly EffectiveFrom { get; set; }

    /// <summary>نسبة المنشأة كسراً عشرياً بمقياس ثمانٍ. لا تُخترع ولا تُكتب في شيفرة.</summary>
    public decimal EmployerRate { get; set; }

    /// <summary>نسبة الموظف كسراً عشرياً بمقياس ثمانٍ. لا تُخترع ولا تُكتب في شيفرة.</summary>
    public decimal EmployeeRate { get; set; }

    /// <summary>أدنى أجر خاضع.</summary>
    public decimal MinimumContributoryWage { get; set; }

    /// <summary>أقصى أجر خاضع.</summary>
    public decimal MaximumContributoryWage { get; set; }

    /// <summary>من اعتمد الصفّ — إنسان، لا نظام.</summary>
    public string ApprovedBy { get; set; } = string.Empty;

    public DateOnly ApprovedOn { get; set; }

    /// <summary>مرجع المصدر النظامي الذي أُخذت منه هذه القيم — نصٌّ يقرؤه مراجع.</summary>
    public string SourceRef { get; set; } = string.Empty;
}

/// <summary>مسيّر رواتب لفترة. <b>ولا فهرس فريد على الفترة</b> — انظر <c>HrDbContext</c>.</summary>
internal sealed class PayrollRunRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    /// <summary>رمز الفترة <c>yyyy-MM</c> ميلادياً.</summary>
    public string PeriodCode { get; set; } = string.Empty;

    public DateOnly PeriodStart { get; set; }

    public DateOnly PeriodEnd { get; set; }

    public string State { get; set; } = HrDocumentState.Draft;

    public decimal GrossEntitlements { get; set; }

    public decimal EmployerSocialInsurance { get; set; }

    public decimal EmployeeSocialInsurance { get; set; }

    public decimal AdvanceInstalment { get; set; }

    public decimal Deductions { get; set; }

    public decimal NetPayable { get; set; }
}

/// <summary>
/// قسيمة موظف — <b>وهي مستند الترحيل</b>: <c>DocumentId</c> في هوية الإحكام السداسية
/// هو معرّف هذا الصفّ لا معرّف المسيّر.
/// </summary>
internal sealed class PayslipRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid RunId { get; set; }

    public Guid EmployeeId { get; set; }

    public Guid EmploymentId { get; set; }

    /// <summary>الرمز المعتم — يُنسخ هنا كي يبقى مطابقاً لما كُتب في الدفتر وقت الترحيل.</summary>
    public string EmployeeCode { get; set; } = string.Empty;

    /// <summary>مركز التكلفة كما كان وقت بناء القسيمة.</summary>
    public string CostCenterId { get; set; } = string.Empty;

    public decimal GrossEntitlements { get; set; }

    public decimal EmployerSocialInsurance { get; set; }

    public decimal EmployeeSocialInsurance { get; set; }

    public decimal AdvanceInstalment { get; set; }

    public decimal Deductions { get; set; }

    public decimal NetPayable { get; set; }

    /// <summary>وعاء الاشتراك بعد تطبيق حدَّي الصفّ المعتمد — يُحفظ ليُراجَع لا ليُعاد حسابه.</summary>
    public decimal ContributoryWage { get; set; }

    public string State { get; set; } = HrDocumentState.Draft;

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>مكوّن على قسيمة — تفصيل ما بُني منه المبلغ، ليُراجَع.</summary>
internal sealed class PayslipComponentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PayslipId { get; set; }

    public int LineNo { get; set; }

    public string ComponentCode { get; set; } = string.Empty;

    public string Kind { get; set; } = string.Empty;

    public bool EntersContributoryWage { get; set; }

    public decimal Amount { get; set; }
}

/// <summary>
/// سند صرف الرواتب على مسيّر مُرحَّل. <b>وطرف الخزينة إلزامي</b>: سطر التسوية معلَن
/// <c>subledger: "resolved"</c> والمحرك يطويه إلى <c>none</c> ثم يبحث عن الواقعة
/// <c>subledger.none</c> — وبدونها يُرفض كل نداء بـ<c>ledger.posting.missing_subledger</c>.
/// </summary>
internal sealed class PayrollPaymentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid RunId { get; set; }

    public DateOnly PaidOn { get; set; }

    /// <summary>طريقة التسوية — مؤهّل دور، لا رمز حساب.</summary>
    public string SettlementMethod { get; set; } = string.Empty;

    /// <summary>الخزينة أو الحساب البنكي في دفترها المساعد.</summary>
    public string TreasuryPartyId { get; set; } = string.Empty;

    public string State { get; set; } = HrDocumentState.Draft;

    public decimal NetPayable { get; set; }
}

/// <summary>سطر سند صرف — واحدٌ لكل قسيمة، وهو حبيبيّة القيد.</summary>
internal sealed class PayrollPaymentLineRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid PaymentId { get; set; }

    public Guid PayslipId { get; set; }

    public int LineNo { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string CostCenterId { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public Guid? PostedEntryId { get; set; }
}

/// <summary>
/// سداد التأمينات للفترة — <b>وهو المستند الوحيد في الوحدة الذي يُرحَّل قيداً واحداً
/// للفترة</b>، لأن سطره على حساب الالتزام بلا دفتر مساعد فلا طرفَ يُفقد بالتجميع.
/// </summary>
internal sealed class SocialInsurancePaymentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public string PeriodCode { get; set; } = string.Empty;

    public DateOnly PaidOn { get; set; }

    public decimal Amount { get; set; }

    public string SettlementMethod { get; set; } = string.Empty;

    public string TreasuryPartyId { get; set; } = string.Empty;

    public string State { get; set; } = HrDocumentState.Draft;

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>
/// قيدٌ في سجلّ الجزاءات المعتمد. <b>ولا مورد ترحيل له</b>: الاستقطاع يُرحَّل داخل
/// المسيّر لا بذاته، وبابٌ يوحي بغير ذلك يُبنى عليه عميل.
/// </summary>
internal sealed class EmployeeDeductionRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid EmployeeId { get; set; }

    /// <summary>الفترة التي يُستقطع فيها.</summary>
    public string PeriodCode { get; set; } = string.Empty;

    /// <summary>مفتاح فئة السبب — رمزٌ يملكه المستدعي لا نصٌّ يُعرض.</summary>
    public string CategoryKey { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string ApprovedBy { get; set; } = string.Empty;

    public DateOnly ApprovedOn { get; set; }

    /// <summary>هل استُهلك في قسيمة؟ يُملأ عند بناء المسيّر ولا يُعاد.</summary>
    public Guid? ConsumedByPayslipId { get; set; }
}

/// <summary>
/// سلفة موظف بجدول أقساطها. <b>ولا مورد ترحيل لها في هذا التسليم</b>: حدث صرف
/// السلفة غير موجود في مصفوفة الترحيل، والمحرك يرفض رمزاً غير معروف ولا يخترع قالباً.
/// </summary>
internal sealed class EmployeeAdvanceRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid EmployeeId { get; set; }

    public DateOnly IssuedOn { get; set; }

    public decimal Amount { get; set; }

    public string SettlementMethod { get; set; } = string.Empty;

    public string TreasuryPartyId { get; set; } = string.Empty;

    public string State { get; set; } = HrDocumentState.Draft;
}

/// <summary>قسط سداد سلفة — يُستقطع داخل مسيّر الفترة التي يحملها.</summary>
internal sealed class AdvanceInstalmentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid AdvanceId { get; set; }

    public int LineNo { get; set; }

    public string PeriodCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>القسيمة التي استُقطع فيها، أو <c>null</c> فلم يُستقطع بعد.</summary>
    public Guid? ConsumedByPayslipId { get; set; }
}

/// <summary>مستند استحقاق مخصص نهاية الخدمة لفترة — يحمل حركات علاقات العمل السارية.</summary>
internal sealed class EndOfServiceProvisionRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public string PeriodCode { get; set; } = string.Empty;

    public DateOnly AccruedOn { get; set; }

    /// <summary>
    /// مرجع أساس القياس المعتمد — <b>نصٌّ يكتبه معتمِد المستند</b>. والوحدة لا تحسب
    /// حصّة الفترة ولا تعرف معادلتها: طريقة القياس بندٌ مفتوح على المالك.
    /// </summary>
    public string MeasurementRef { get; set; } = string.Empty;

    public string ApprovedBy { get; set; } = string.Empty;

    public string State { get; set; } = HrDocumentState.Draft;

    public decimal PeriodShare { get; set; }
}

/// <summary>
/// حركة مخصص لعلاقة عمل في فترة — <b>يُضاف ولا يُعدَّل</b>، وهو حبيبيّة الطرف المساعد
/// على حساب المخصص ومصدر الرصيد الذي تقرأه المخالصة.
/// </summary>
internal sealed class EndOfServiceMovementRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProvisionId { get; set; }

    public Guid EmploymentId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string CostCenterId { get; set; } = string.Empty;

    public string PeriodCode { get; set; } = string.Empty;

    public decimal PeriodShare { get; set; }

    public Guid? PostedEntryId { get; set; }
}

/// <summary>
/// مخالصة نهاية خدمة. <b>والسيناريو مُسمّى في الصفّ</b> لا مستنتَجاً من فرق مبلغين
/// عند القارئ.
/// </summary>
internal sealed class EndOfServiceSettlementRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid EmploymentId { get; set; }

    public string EmployeeCode { get; set; } = string.Empty;

    public string CostCenterId { get; set; } = string.Empty;

    public DateOnly SettledOn { get; set; }

    /// <summary>المستحقّ بحساب المخالصة المعتمد — <b>يُدخله معتمِد المستند</b>.</summary>
    public decimal SettlementDue { get; set; }

    /// <summary>رصيد المخصص لهذه العلاقة، محسوباً من حركاتها المُرحَّلة.</summary>
    public decimal ProvisionBalance { get; set; }

    public decimal AmountPaid { get; set; }

    public decimal Shortfall { get; set; }

    public decimal Excess { get; set; }

    public decimal ProvisionUtilised { get; set; }

    /// <summary>‏<c>exact</c> · <c>short</c> · <c>excess</c> — بأسماء المصفوفة نفسها.</summary>
    public string ScenarioCode { get; set; } = string.Empty;

    public string MeasurementRef { get; set; } = string.Empty;

    public string SettlementMethod { get; set; } = string.Empty;

    public string TreasuryPartyId { get; set; } = string.Empty;

    public string State { get; set; } = HrDocumentState.Draft;

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>
/// سجلّ محاولات الترحيل بهويّتها السداسية — <b>النسخة الرابعة</b> من هذه البوّابة في
/// هذا المستودع (المبيعات · المشتريات · المخزون)، وزيادتها مُسجَّلة ديناً في البند ت-10.
/// </summary>
internal sealed class DocumentPostingRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string TriggerCode { get; set; } = string.Empty;

    public int Generation { get; set; } = 1;

    public string IdempotencyKey { get; set; } = string.Empty;

    public string EventCode { get; set; } = string.Empty;

    /// <summary>الطرف في الدفتر المساعد — <b>الرمز المعتم</b>، أو فراغٌ لمستندٍ بلا طرف.</summary>
    public string PartyId { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public string State { get; set; } = PostingAttemptState.Attempting;

    public Guid? EntryId { get; set; }

    public long EntryNumber { get; set; }

    public decimal ControlEffect { get; set; }

    public int AttemptCount { get; set; }

    public DateTime LastAttemptAt { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessageAr { get; set; } = string.Empty;

    public string FailureMessageEn { get; set; } = string.Empty;
}
