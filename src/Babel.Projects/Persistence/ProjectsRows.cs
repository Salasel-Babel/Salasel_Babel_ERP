namespace Babel.Projects.Persistence;

/// <summary>حالة مستند مقاولات.</summary>
internal static class ProjectsDocumentState
{
    public const string Draft = "DRAFT";
    public const string Posted = "POSTED";
}

/// <summary>حالة محاولة ترحيل مستند — الشكل نفسه المُودَع في وحدة المبيعات.</summary>
internal static class PostingAttemptState
{
    /// <summary>سُجّلت النية ولم يُعرف مصير النداء بعد — الحالة القابلة لإعادة المحاولة.</summary>
    public const string Attempting = "ATTEMPTING";

    /// <summary>رُحّل فعلاً وللمحرك إيصال.</summary>
    public const string Posted = "POSTED";

    /// <summary>رفضه المحرك، والسبب محفوظ. المستند باقٍ على حاله ويُعاد بلا ازدواج.</summary>
    public const string Refused = "REFUSED";
}

/// <summary>
/// نوع المستند المالك لسطر مستخلص.
/// <para>
/// قيمتان لا حقل تمييز على جدول واحد: مستخلص العميل ومستخلص الباطن <b>جدولان</b>،
/// وهذه القيم هي ما يربط السطر بمالكه في <c>uq_projects_certificate_line_owner</c>.
/// </para>
/// </summary>
internal static class CertificateOwner
{
    public const string Client = "CLIENT";
    public const string Subcontractor = "SUBCONTRACTOR";
}

/// <summary>
/// صنف سطر المستخلص.
/// <para>
/// <b>والغرامة صنفٌ مستقلّ لا خصمٌ من قيمة الأعمال</b> — تحفّظ المصفوفة بنصّه:
/// «الغرامات والخصومات تُسجَّل كسطور مستقلة تخفّض المستحق ولا تُخصم من قيمة الأعمال».
/// وصنفُها هنا هو ما يجعل الرفض ممكناً بدل الخصم الصامت.
/// </para>
/// </summary>
internal static class CertificateLineKind
{
    /// <summary>عمل منفَّذ بكمّية تراكمية على بند.</summary>
    public const string Work = "WORK";

    /// <summary>غرامة تأخير — تُخزَّن ولا تُرحَّل، ووجودها يرفض الترحيل.</summary>
    public const string Penalty = "PENALTY";

    /// <summary>خصم آخر — يُعامَل معاملة الغرامة للسبب نفسه.</summary>
    public const string Deduction = "DEDUCTION";
}

/// <summary>جانب المحتجز: مدينٌ لدى العميل، أو دائنٌ على المقاول.</summary>
internal static class RetentionSide
{
    public const string Receivable = "RECEIVABLE";
    public const string Payable = "PAYABLE";
}

/// <summary>
/// مشروع. <b>رمزه هو القيمة الحرفية التي تدخل عمود <c>project_id</c> على سطر القيد</b>،
/// فهو هوية لا اسم عرض: لا تعديل ولا حذف لرمزٍ تحمله قيود سنةٍ مضت.
/// </summary>
internal sealed class ProjectRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>الرمز — وهو ما يُكتب في بُعد <c>project</c> على كل سطر قيد.</summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// الاسم العربي — <b>السجلّ لا ترجمته</b>. ولا عمود ثانٍ للإنجليزية: الترجمات
    /// صفوفٌ في <c>projects.name_translation</c> (ADR-0021 بند 2 · القاعدة 14).
    /// </summary>
    public string NameAr { get; set; } = string.Empty;

    public DateOnly StartedOn { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// ترجمة اسم كيانٍ في هذه الوحدة، صفّاً لا عموداً.
/// <para>
/// المفتاح وسم BCP-47، والإنجليزية <b>واحدة من N</b> لا نصف اثنين. وصفٌّ غائب يعني
/// ارتداد العرض إلى العربية، لا نصّاً فارغاً.
/// </para>
/// </summary>
internal sealed class NameTranslationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>صنف الكيان: <c>project</c> · <c>subcontractor</c> · <c>boq_item</c>.</summary>
    public string EntityKind { get; set; } = string.Empty;

    public Guid EntityId { get; set; }

    /// <summary>وسم اللغة بصيغة BCP-47.</summary>
    public string LanguageTag { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;
}

/// <summary>عقد مقاولة مع عميل.</summary>
internal sealed class ProjectContractRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public string Number { get; set; } = string.Empty;

    /// <summary>معرّف العميل في دفتره المساعد — معرّف مبهم، لا صفّ يُضمّ من وحدة أخرى.</summary>
    public string CustomerPartyId { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public DateOnly SignedOn { get; set; }

    /// <summary>
    /// نسبة المحتجز كسراً عشرياً <b>كما نصّ عليها العقد</b> — لا نسبة في الكود.
    /// <para>
    /// <b>ولاحظ ما ليس هنا: وعاء النسبة ولا قاعدة استرداد الدفعة المقدمة.</b> موضعُهما
    /// نفسه قرار مالك (حقلٌ على العقد؟ أم جدول قواعد بتاريخ سريان؟)، فكتابتهما هنا
    /// اختيارٌ لأحد الجوابين بلا أن يقوله أحد. وهما بندان معلَّقان في
    /// <c>projects.contract_policy</c>، والمستخلص يُرفض حتى يُحسما.
    /// </para>
    /// </summary>
    public decimal RetentionRate { get; set; }

    /// <summary>فترة الضمان بالأشهر كما نصّ عليها العقد.</summary>
    public int GuaranteeMonths { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>
/// <b>بند معلَّق على عقد — قرارُ مالكٍ لم يُحسم بعد، وصفُّه هو ما يفتح الترحيل.</b>
/// <para>
/// الجدول <b>يُبنى فارغاً</b> ولا باب على السطح يكتب فيه: هذه إجاباتُ محاسبٍ لا
/// إعداداتُ مستخدم. والمستخلص لعقدٍ ينقصه صفٌّ معتمد لأي بند مطلوب <b>يُرفض رفضاً
/// صريحاً</b> برمزٍ مستقرّ يسمّي البند — لا قيمة افتراضية واحدة، ولا تخمين.
/// </para>
/// <para>
/// و<c>Resolution</c> نصٌّ <b>مبهم على الشيفرة عمداً</b>: هو ما كتبه المحاسب، ولا
/// تشتقّ منه الوحدة حساباً لم يُبنَ بعد. فوجودُ الصفّ يرفع الحجب الأول، ويبقى الحجب
/// الثاني — «قرارٌ اعتُمد ولا حاسب له» — حتى يُبنى حاسبُه بتوقيعه.
/// </para>
/// </summary>
internal sealed class ContractPolicyRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ContractId { get; set; }

    /// <summary>رمز البند المعلَّق كما تُعلنه <c>PendingPolicyItems</c>.</summary>
    public string ItemCode { get; set; } = string.Empty;

    /// <summary>نصّ القرار كما اعتمده المحاسب. مبهمٌ على الشيفرة حتى يُبنى حاسبه.</summary>
    public string Resolution { get; set; } = string.Empty;

    /// <summary>من اعتمده — والاعتماد فعلٌ يُنسب، لا حقلٌ يُملأ.</summary>
    public string ApprovedBy { get; set; } = string.Empty;

    public DateOnly ApprovedOn { get; set; }
}

/// <summary>
/// بند جدول الكميات — <b>بند التسعير داخل المشروع</b>، وهو ما يدخل بُعد <c>boq_item</c>.
/// </summary>
internal sealed class BoqItemRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ContractId { get; set; }

    /// <summary>
    /// رمز البند، فريدٌ داخل العقد. ولا تفرّد على مستوى المنشأة: البُعد مُعرَّف بأنه
    /// «بند التسعير <b>داخل المشروع</b>»، وكل سطر مصفوفة يكتب <c>boq_item</c> يكتب
    /// <c>project</c> معه.
    /// </summary>
    public string Code { get; set; } = string.Empty;

    public int LineNo { get; set; }

    public string DescriptionAr { get; set; } = string.Empty;

    /// <summary>وحدة القياس كما كُتبت في العقد. لا تحويل وحدات في هذه الوحدة.</summary>
    public string Unit { get; set; } = string.Empty;

    public decimal ContractQuantity { get; set; }

    public decimal UnitRate { get; set; }

    /// <summary>الأمر التغييري الذي أدخل هذا البند، أو <c>null</c> لبنود العقد الأصلي.</summary>
    public Guid? ChangeOrderId { get; set; }
}

/// <summary>
/// أمر تغييري: <b>التزام تعاقدي لا واقعة محاسبية</b> — لا يُرحَّل، ولا مورد ترحيل له.
/// </summary>
internal sealed class ChangeOrderRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ContractId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly IssuedOn { get; set; }

    public string ReasonAr { get; set; } = string.Empty;

    public string ApprovedBy { get; set; } = string.Empty;
}

/// <summary>مقاول من الباطن — طرفٌ في دفتر <c>subcontractor</c> المساعد.</summary>
internal sealed class SubcontractorRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string VatNumber { get; set; } = string.Empty;

    public bool IsActive { get; set; }
}

/// <summary>عقد باطن.</summary>
internal sealed class SubcontractRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ProjectId { get; set; }

    public Guid SubcontractorId { get; set; }

    public string Number { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public DateOnly SignedOn { get; set; }

    /// <summary>نسبة المحتجز في عقد الباطن — من العقد لا من الكود.</summary>
    public decimal RetentionRate { get; set; }

    public int GuaranteeMonths { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>بند عقد باطن — مدخل سطور مستخلصه.</summary>
internal sealed class SubcontractLineRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubcontractId { get; set; }

    public string Code { get; set; } = string.Empty;

    public int LineNo { get; set; }

    public string DescriptionAr { get; set; } = string.Empty;

    public string Unit { get; set; } = string.Empty;

    public decimal ContractQuantity { get; set; }

    public decimal UnitRate { get; set; }
}

/// <summary>
/// مستخلص عميل — <b>تراكميّ</b>: السطر يحمل الكمّية التراكمية والسابقة صراحةً،
/// وقيمة الفترة تُشتقّ طرحاً، والأساس المطروح منه <b>آخر مستخلص مُرحَّل</b>.
/// </summary>
internal sealed class ClientCertificateRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid ContractId { get; set; }

    /// <summary>الرقم المرئي — <b>يرسله العميل ويُتحقَّق من تفرّده</b>، ولا عدّاد ولا تسلسل.</summary>
    public string Number { get; set; } = string.Empty;

    /// <summary>
    /// تسلسل المستخلص داخل العقد — التفرّد الوظيفي الذي يقوم عليه الاشتقاق التراكمي.
    /// </summary>
    public int SequenceNo { get; set; }

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public string State { get; set; } = ProjectsDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>
    /// نسبة المحتجز <b>مجمَّدةً لحظة المسوّدة</b> من العقد. وبدونها يُغيّر تعديلٌ على
    /// العقد أرقامَ مستخلصٍ راجعه إنسان.
    /// </summary>
    public decimal FrozenRetentionRate { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>مستخلص مقاول من الباطن، بالشكل التراكمي نفسه.</summary>
internal sealed class SubcontractorCertificateRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubcontractId { get; set; }

    public string Number { get; set; } = string.Empty;

    public int SequenceNo { get; set; }

    public DateOnly PeriodFrom { get; set; }

    public DateOnly PeriodTo { get; set; }

    public string State { get; set; } = ProjectsDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal FrozenRetentionRate { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>سطر مستخلص — يخدم مستخلص العميل ومستخلص الباطن معاً.</summary>
internal sealed class CertificateLineRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>نوع المستند المالك: <c>CLIENT</c> · <c>SUBCONTRACTOR</c>.</summary>
    public string OwnerType { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }

    public int LineNo { get; set; }

    /// <summary>صنف السطر: عمل · غرامة · خصم.</summary>
    public string LineKind { get; set; } = CertificateLineKind.Work;

    /// <summary>بند جدول الكميات أو بند عقد الباطن — <c>null</c> على سطر غرامة أو خصم.</summary>
    public Guid? ItemId { get; set; }

    public string DescriptionAr { get; set; } = string.Empty;

    /// <summary>وحدة الكمّية — <b>تُرفض إن خالفت وحدة بندها</b>، ولا تُحوَّل هنا.</summary>
    public string Unit { get; set; } = string.Empty;

    /// <summary>الكمّية التراكمية المُنجَزة حتى نهاية هذه الفترة.</summary>
    public decimal CumulativeQuantity { get; set; }

    /// <summary>
    /// الكمّية السابقة كما جاءت من <b>آخر مستخلص مُرحَّل</b> — مكتوبةً صراحةً على السطر
    /// كي يبقى المستند مقروءاً بذاته بعد سنوات.
    /// </summary>
    public decimal PreviousQuantity { get; set; }

    /// <summary>مبلغ سطر الغرامة أو الخصم كما أدخله المستخدم. صفرٌ على سطر العمل.</summary>
    public decimal Amount { get; set; }
}

/// <summary>
/// دفعة مقدمة مصروفة لمقاول من الباطن — <b>أصلٌ لا مصروف</b>، بنصّ الحدث.
/// </summary>
internal sealed class SubcontractorAdvanceRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid SubcontractId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly PaidOn { get; set; }

    public string State { get; set; } = ProjectsDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>طريقة التسوية — مؤهّل الدور، لا حساب: <c>cash</c> · <c>bank</c> · …</summary>
    public string SettlementMethod { get; set; } = string.Empty;

    /// <summary>معرّف الخزينة أو الحساب البنكي في دفترها المساعد — معرّف مبهم لا رقم حساب.</summary>
    public string TreasuryPartyId { get; set; } = string.Empty;

    /// <summary>خطاب ضمان الدفعة المقدمة الذي يشترطه نصّ الإطلاق.</summary>
    public Guid? GuaranteeId { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>
/// حركة محتجز — <b>تُضاف ولا تُعدَّل</b>، ومصدرها ترحيلٌ وقع. والرصيد يُشتقّ منها،
/// ولا عمود رصيدٍ يُنقَص في هذه الوحدة.
/// </summary>
internal sealed class RetentionMovementRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>الجانب: <c>RECEIVABLE</c> لدى العميل · <c>PAYABLE</c> على المقاول.</summary>
    public string Side { get; set; } = string.Empty;

    /// <summary>نوع الدفتر المساعد للطرف: <c>customer</c> · <c>subcontractor</c>.</summary>
    public string PartyKind { get; set; } = string.Empty;

    public string PartyId { get; set; } = string.Empty;

    /// <summary>رمز المشروع كما دخل بُعد <c>project</c> — لا معرّف داخلي.</summary>
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>
    /// العقد الذي نشأ عنه المحتجز — عقدُ عميلٍ أو عقدُ باطن.
    /// <para>
    /// وهو ما يُستشار به جدول البنود المعلَّقة عند الإفراج والتحصيل: فرعُ المحتجز
    /// المدين يتوقّف على البند نفسه الذي يتوقّف عليه المستخلص، فلا يُفتح من باب ثانٍ.
    /// </para>
    /// </summary>
    public Guid ContractId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string EventCode { get; set; } = string.Empty;

    /// <summary>المبلغ موجباً عند الاحتجاز وسالباً عند الإفراج أو التحصيل.</summary>
    public decimal Amount { get; set; }

    public DateOnly MovedOn { get; set; }

    /// <summary>تاريخ استحقاق الإفراج، مشتقٌّ من فترة الضمان في العقد.</summary>
    public DateOnly DueOn { get; set; }
}

/// <summary>حركة دفعة مقدمة — تُضاف ولا تُعدَّل، ومصدرها ترحيلٌ وقع.</summary>
internal sealed class AdvanceMovementRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string PartyKind { get; set; } = string.Empty;

    public string PartyId { get; set; } = string.Empty;

    public Guid ContractId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string EventCode { get; set; } = string.Empty;

    /// <summary>موجبٌ عند الصرف وسالبٌ عند الاسترداد.</summary>
    public decimal Amount { get; set; }

    public DateOnly MovedOn { get; set; }
}

/// <summary>
/// إفراج عن محتجز دائن — <b>مستندٌ مستقلّ لا تعديلٌ لقيد المستخلص</b> (نصّ الحدث).
/// </summary>
internal sealed class RetentionReleaseRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>دفعة المحتجز التي يُفرَج عنها — والإفراج على حركةٍ مُسمّاة لا على رصيد.</summary>
    public Guid RetentionMovementId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly ReleasedOn { get; set; }

    public string State { get; set; } = ProjectsDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    /// <summary>الاعتماد الصريح الذي يشترطه نصّ الإطلاق.</summary>
    public string ApprovedBy { get; set; } = string.Empty;

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>تحصيل محتجز مدين من العميل بطريقة تسوية مُسمّاة.</summary>
internal sealed class RetentionCollectionRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid RetentionMovementId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly CollectedOn { get; set; }

    public string State { get; set; } = ProjectsDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public string SettlementMethod { get; set; } = string.Empty;

    public string TreasuryPartyId { get; set; } = string.Empty;

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>
/// خطاب ضمان — <b>سجلٌّ لا يُرحَّل أبداً</b>: لا حدث له في المصفوفة، ولا مورد ترحيل له
/// على السطح، ولا <c>entryId</c> في مخطّط جوابه.
/// </summary>
internal sealed class GuaranteeRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>عقد العميل الذي يخصّه الضمان، أو <c>null</c> إن كان على عقد باطن.</summary>
    public Guid? ContractId { get; set; }

    /// <summary>عقد الباطن الذي يخصّه الضمان، أو <c>null</c>.</summary>
    public Guid? SubcontractId { get; set; }

    /// <summary>صنفه: ابتدائي · حسن تنفيذ · دفعة مقدمة.</summary>
    public string Kind { get; set; } = string.Empty;

    public string Number { get; set; } = string.Empty;

    public string IssuerNameAr { get; set; } = string.Empty;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal Amount { get; set; }

    public DateOnly EffectiveFrom { get; set; }

    public DateOnly ExpiresOn { get; set; }

    /// <summary>
    /// معرّف المرفق في السطح المنشور للمرفقات — <b>لا عمود ثنائي ولا نظام ملفّات جانبي</b>.
    /// </summary>
    public string AttachmentId { get; set; } = string.Empty;
}

/// <summary>
/// محاولة ترحيل مستند — سجلّ الوحدة عن نيّتها ومصيرها، بالشكل المُودَع في وحدة المبيعات.
/// <para>
/// هوية الإحكام خمسة مكوّنات (نوع المستند · معرّفه · رمز الإطلاق · الجيل · <b>رمز
/// الحدث</b>)، والفهرس الفريد ستّة أعمدة بإضافة المستأجر. والصفّ يحمل فوق الهوية
/// <c>PartyId</c> و<c>ControlEffect</c> و<c>DocumentDate</c> — لأن هذه بالضبط ما تقرؤه
/// المطابقة.
/// </para>
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

    /// <summary>رمز الحدث — حقلٌ في الهوية، إلزامي وغير فارغ (قيد تحقّق في القاعدة).</summary>
    public string EventCode { get; set; } = string.Empty;

    public string PartyId { get; set; } = string.Empty;

    /// <summary>نوع الدفتر المساعد الذي يتحرّك بهذا المستند — به تُقسَّم المطابقة.</summary>
    public string SubledgerKind { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public string State { get; set; } = PostingAttemptState.Attempting;

    /// <summary>الأثر المتوقَّع على الحساب الضابط بمنطق «مدين ناقص دائن».</summary>
    public decimal ControlEffect { get; set; }

    public Guid? EntryId { get; set; }

    public long EntryNumber { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessageAr { get; set; } = string.Empty;

    public string FailureMessageEn { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTime LastAttemptAt { get; set; }
}
