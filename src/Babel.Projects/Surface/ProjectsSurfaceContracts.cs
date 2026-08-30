namespace Babel.Projects.Surface;

/// <summary>
/// ثنائية اسم وقيمة على حدّ السطح — تُستعمل لترجمات الاسم بوسم BCP-47.
/// <para>
/// <b>ولا حقل إنجليزي ثابت في أي نوع هنا:</b> الاسم العربي هو السجلّ، والإنجليزية
/// واحدة من N (ADR-0021 بند 2). وزوجٌ ثابت <c>ar</c>/<c>en</c> عاجزٌ بنيوياً عن الثالثة.
/// </para>
/// </summary>
/// <param name="Name">وسم اللغة.</param>
/// <param name="Value">النصّ المترجَم.</param>
public sealed record ProjectsNameValue(string Name, string Value);

/// <summary>
/// كمّية بوحدتها على حدّ السطح — <b>ولا كمّية مجرّدة تعبر هذا الحدّ</b>.
/// <para>
/// ومعرَّفةٌ هنا لا مستورَدة: لا نوع «مقدار ووحدة» في العقود ولا في النواة المشتركة،
/// ونظيره في المخزون يعيش في سطح تلك الوحدة ولا تبلغه المقاولات.
/// </para>
/// </summary>
/// <param name="Magnitude">المقدار.</param>
/// <param name="Unit">رمز الوحدة.</param>
public sealed record ProjectsMeasure(decimal Magnitude, string Unit);

/// <summary>بندٌ معلَّق على قرار مالك، كما يخرج على السطح.</summary>
/// <param name="Code">رمزه الثابت.</param>
/// <param name="TitleAr">عنوانه بالعربية.</param>
/// <param name="TitleEn">عنوانه بالإنجليزية.</param>
/// <param name="SourceRef">الموضع الذي يحمل السؤال كاملاً.</param>
public sealed record ProjectsPendingItem(string Code, string TitleAr, string TitleEn, string SourceRef);

/// <summary>طلب تسجيل مشروع.</summary>
/// <param name="Code">الرمز — وهو ما يدخل بُعد المشروع على سطر القيد.</param>
/// <param name="NameAr">الاسم العربي — السجلّ.</param>
/// <param name="NameTranslations">ترجماته.</param>
/// <param name="StartedOn">تاريخ البدء.</param>
public sealed record ProjectsProjectRequest(
    string Code,
    string NameAr,
    IReadOnlyList<ProjectsNameValue> NameTranslations,
    DateOnly StartedOn);

/// <summary>عقدٌ مختصر تحت مشروعه.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="CurrencyCode">العملة.</param>
public sealed record ProjectsContractSummary(Guid Id, string Number, string CurrencyCode);

/// <summary>مشروع كما يخرج على السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">الترجمات.</param>
/// <param name="StartedOn">تاريخ البدء.</param>
/// <param name="IsActive">هل هو عامل؟</param>
/// <param name="Contracts">عقوده.</param>
public sealed record ProjectsProject(
    Guid Id,
    string Code,
    string NameAr,
    IReadOnlyList<ProjectsNameValue> NameTranslations,
    DateOnly StartedOn,
    bool IsActive,
    IReadOnlyList<ProjectsContractSummary> Contracts);

/// <summary>طلب بند جدول كميات.</summary>
/// <param name="Code">رمزه.</param>
/// <param name="DescriptionAr">بيانه.</param>
/// <param name="ContractQuantity">كمّيته التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر وحدته.</param>
public sealed record ProjectsBoqItemRequest(
    string Code,
    string DescriptionAr,
    ProjectsMeasure ContractQuantity,
    decimal UnitRate);

/// <summary>طلب إنشاء عقد مقاولة.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="CustomerPartyId">العميل.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز كما نصّ عليها العقد.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="Items">بنود جدول الكميات.</param>
public sealed record ProjectsContractRequest(
    string Number,
    Guid ProjectId,
    string CustomerPartyId,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<ProjectsBoqItemRequest> Items);

/// <summary>عقد كما يخرج على السطح، ومعه بنوده المعلَّقة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="ProjectCode">رمز المشروع.</param>
/// <param name="CustomerPartyId">العميل.</param>
/// <param name="CurrencyCode">العملة.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="PendingPolicy">البنود المعلَّقة التي تمنع الترحيل.</param>
public sealed record ProjectsContract(
    Guid Id,
    string Number,
    Guid ProjectId,
    string ProjectCode,
    string CustomerPartyId,
    string CurrencyCode,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<ProjectsPendingItem> PendingPolicy);

/// <summary>بند جدول كميات كما يخرج على السطح — بمعرّفه، وهو مدخل سطور المستخلص.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
/// <param name="ChangeOrderId">الأمر التغييري الذي أدخله، أو <c>null</c>.</param>
public sealed record ProjectsBoqItem(
    Guid Id,
    string Code,
    int LineNo,
    string DescriptionAr,
    ProjectsMeasure ContractQuantity,
    decimal UnitRate,
    Guid? ChangeOrderId);

/// <summary>طلب أمر تغييري.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="ContractId">العقد.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="ReasonAr">السبب.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="AddedItems">البنود الجديدة.</param>
public sealed record ProjectsChangeOrderRequest(
    string Number,
    Guid ContractId,
    DateOnly IssuedOn,
    string ReasonAr,
    string ApprovedBy,
    IReadOnlyList<ProjectsBoqItemRequest> AddedItems);

/// <summary>
/// أمر تغييري كما يخرج على السطح. <b>ولا <c>entryId</c> ولا <c>alreadyPosted</c> فيه</b>:
/// حقلٌ فارغ لهما يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً».
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ContractId">العقد.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="ReasonAr">السبب.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="AddedItems">البنود الجديدة.</param>
public sealed record ProjectsChangeOrder(
    Guid Id,
    string Number,
    Guid ContractId,
    DateOnly IssuedOn,
    string ReasonAr,
    string ApprovedBy,
    IReadOnlyList<ProjectsBoqItem> AddedItems);

/// <summary>طلب تسجيل مقاول من الباطن.</summary>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">الترجمات.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي.</param>
public sealed record ProjectsSubcontractorRequest(
    string Code,
    string NameAr,
    IReadOnlyList<ProjectsNameValue> NameTranslations,
    string VatNumber);

/// <summary>مقاول من الباطن كما يخرج على السطح.</summary>
/// <param name="Id">المعرّف — وهو الطرف في دفتره المساعد.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">الترجمات.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي.</param>
/// <param name="IsActive">هل هو عامل؟</param>
public sealed record ProjectsSubcontractor(
    Guid Id,
    string Code,
    string NameAr,
    IReadOnlyList<ProjectsNameValue> NameTranslations,
    string VatNumber,
    bool IsActive);

/// <summary>طلب بند عقد باطن.</summary>
/// <param name="Code">الرمز.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
public sealed record ProjectsSubcontractLineRequest(
    string Code,
    string DescriptionAr,
    ProjectsMeasure ContractQuantity,
    decimal UnitRate);

/// <summary>طلب إنشاء عقد باطن.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="SubcontractorId">المقاول.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="Lines">البنود.</param>
public sealed record ProjectsSubcontractRequest(
    string Number,
    Guid ProjectId,
    Guid SubcontractorId,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<ProjectsSubcontractLineRequest> Lines);

/// <summary>عقد باطن كما يخرج على السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="ProjectCode">رمز المشروع.</param>
/// <param name="SubcontractorId">المقاول.</param>
/// <param name="CurrencyCode">العملة.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="PendingPolicy">البنود المعلَّقة.</param>
public sealed record ProjectsSubcontract(
    Guid Id,
    string Number,
    Guid ProjectId,
    string ProjectCode,
    Guid SubcontractorId,
    string CurrencyCode,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<ProjectsPendingItem> PendingPolicy);

/// <summary>بند عقد باطن كما يخرج على السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
public sealed record ProjectsSubcontractLine(
    Guid Id,
    string Code,
    int LineNo,
    string DescriptionAr,
    ProjectsMeasure ContractQuantity,
    decimal UnitRate);

/// <summary>طلب سطر مستخلص — بكمّيته التراكمية أو بمبلغ غرامته.</summary>
/// <param name="ItemId">البند، أو <c>null</c> على سطر غرامة أو خصم.</param>
/// <param name="LineKind">الصنف: <c>WORK</c> · <c>PENALTY</c> · <c>DEDUCTION</c>.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="CumulativeQuantity">الكمّية التراكمية بوحدتها.</param>
/// <param name="Amount">مبلغ الغرامة أو الخصم.</param>
public sealed record ProjectsCertificateLineRequest(
    Guid? ItemId,
    string LineKind,
    string DescriptionAr,
    ProjectsMeasure CumulativeQuantity,
    decimal Amount);

/// <summary>طلب إنشاء مستخلص <b>مسوّدة</b>.</summary>
/// <param name="Number">الرقم المرئي.</param>
/// <param name="OwnerId">العقد أو عقد الباطن.</param>
/// <param name="SequenceNo">التسلسل داخل العقد.</param>
/// <param name="PeriodFrom">بداية الفترة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="Lines">السطور.</param>
public sealed record ProjectsCertificateRequest(
    string Number,
    Guid OwnerId,
    int SequenceNo,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<ProjectsCertificateLineRequest> Lines);

/// <summary>سطر مستخلص كما يخرج على السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="LineKind">الصنف.</param>
/// <param name="ItemId">البند.</param>
/// <param name="ItemCode">رمزه.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="CumulativeQuantity">الكمّية التراكمية بوحدتها.</param>
/// <param name="PreviousQuantity">الكمّية السابقة من آخر مستخلص مُرحَّل.</param>
/// <param name="Amount">مبلغ الغرامة أو الخصم.</param>
public sealed record ProjectsCertificateLine(
    Guid Id,
    int LineNo,
    string LineKind,
    Guid? ItemId,
    string ItemCode,
    string DescriptionAr,
    ProjectsMeasure CumulativeQuantity,
    ProjectsMeasure PreviousQuantity,
    decimal Amount);

/// <summary>
/// مستخلص كما يخرج على السطح.
/// <para>
/// <b>ولاحظ ما ليس فيه: مبالغ محسوبة.</b> قيمة الأعمال والضريبة والمحتجز واسترداد
/// الدفعة أربعةٌ لكلٍّ منها حاسبٌ يجب أن يعيش في الوحدة، ولم يُبنَ أيٌّ منها لأن أساسه
/// بندٌ معلَّق. وعرضُ رقمٍ قبل أن يُحسم أساسه أسوأ من غيابه.
/// </para>
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="OwnerId">العقد أو عقد الباطن.</param>
/// <param name="SequenceNo">التسلسل.</param>
/// <param name="PeriodFrom">بداية الفترة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="State">الحالة.</param>
/// <param name="RetentionRate">نسبة المحتجز المجمَّدة لحظة المسوّدة.</param>
/// <param name="Lines">السطور.</param>
/// <param name="PendingPolicy">البنود المعلَّقة التي تمنع ترحيله.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل ردّ هذا النداءُ ترحيلاً سابقاً؟</param>
public sealed record ProjectsCertificate(
    Guid Id,
    string Number,
    Guid OwnerId,
    int SequenceNo,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string State,
    decimal RetentionRate,
    IReadOnlyList<ProjectsCertificateLine> Lines,
    IReadOnlyList<ProjectsPendingItem> PendingPolicy,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>طلب صرف دفعة مقدمة لمقاول من الباطن.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="SubcontractId">عقد الباطن.</param>
/// <param name="PaidOn">تاريخ الصرف.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
/// <param name="GuaranteeId">خطاب ضمان الدفعة المقدمة.</param>
public sealed record ProjectsAdvanceRequest(
    string Number,
    Guid SubcontractId,
    DateOnly PaidOn,
    decimal Amount,
    string SettlementMethod,
    string TreasuryPartyId,
    Guid? GuaranteeId);

/// <summary>مستند مالي بسيط كما يخرج على السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل ردّ هذا النداءُ ترحيلاً سابقاً؟</param>
public sealed record ProjectsDocument(
    Guid Id,
    string Number,
    string State,
    decimal Amount,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>طلب إفراج عن محتجز دائن.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="RetentionMovementId">دفعة المحتجز.</param>
/// <param name="ReleasedOn">تاريخ الإفراج.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="ApprovedBy">الاعتماد الصريح.</param>
public sealed record ProjectsRetentionReleaseRequest(
    string Number,
    Guid RetentionMovementId,
    DateOnly ReleasedOn,
    decimal Amount,
    string ApprovedBy);

/// <summary>طلب تحصيل محتجز مدين من العميل.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="RetentionMovementId">دفعة المحتجز.</param>
/// <param name="CollectedOn">تاريخ التحصيل.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
public sealed record ProjectsRetentionCollectionRequest(
    string Number,
    Guid RetentionMovementId,
    DateOnly CollectedOn,
    decimal Amount,
    string SettlementMethod,
    string TreasuryPartyId);

/// <summary>طلب تسجيل خطاب ضمان.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="Kind">الصنف.</param>
/// <param name="ContractId">عقد العميل، أو <c>null</c>.</param>
/// <param name="SubcontractId">عقد الباطن، أو <c>null</c>.</param>
/// <param name="IssuerNameAr">المُصدِر.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="EffectiveFrom">بدء السريان.</param>
/// <param name="ExpiresOn">الانتهاء.</param>
/// <param name="AttachmentId">معرّف المرفق على السطح المنشور للمرفقات.</param>
public sealed record ProjectsGuaranteeRequest(
    string Number,
    string Kind,
    Guid? ContractId,
    Guid? SubcontractId,
    string IssuerNameAr,
    decimal Amount,
    DateOnly EffectiveFrom,
    DateOnly ExpiresOn,
    string AttachmentId);

/// <summary>خطاب ضمان كما يخرج على السطح — <b>بلا حقل قيد</b>، لأنه لا يُرحَّل أبداً.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="Kind">الصنف.</param>
/// <param name="ContractId">عقد العميل.</param>
/// <param name="SubcontractId">عقد الباطن.</param>
/// <param name="IssuerNameAr">المُصدِر.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="EffectiveFrom">بدء السريان.</param>
/// <param name="ExpiresOn">الانتهاء.</param>
/// <param name="AttachmentId">المرفق.</param>
public sealed record ProjectsGuarantee(
    Guid Id,
    string Number,
    string Kind,
    Guid? ContractId,
    Guid? SubcontractId,
    string IssuerNameAr,
    decimal Amount,
    DateOnly EffectiveFrom,
    DateOnly ExpiresOn,
    string AttachmentId);

/// <summary>مدخلٌ في سجلّ المحتجزات — دفعة محتجزٍ واحدة برصيدها القائم.</summary>
/// <param name="MovementId">معرّف الحركة.</param>
/// <param name="Side">الجانب.</param>
/// <param name="PartyKind">نوع الدفتر المساعد للطرف.</param>
/// <param name="PartyId">الطرف.</param>
/// <param name="ProjectCode">رمز المشروع.</param>
/// <param name="DocumentType">المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="Amount">المبلغ الأصلي.</param>
/// <param name="Outstanding">الرصيد القائم.</param>
/// <param name="MovedOn">تاريخ الحركة.</param>
/// <param name="DueOn">تاريخ استحقاق الإفراج.</param>
public sealed record ProjectsRetentionEntry(
    Guid MovementId,
    string Side,
    string PartyKind,
    string PartyId,
    string ProjectCode,
    string DocumentType,
    string DocumentId,
    decimal Amount,
    decimal Outstanding,
    DateOnly MovedOn,
    DateOnly DueOn);

/// <summary>سجلّ المحتجزات مدينةً ودائنة — مشتقٌّ من المُرحَّل وحده.</summary>
/// <param name="AsOf">تاريخ القراءة.</param>
/// <param name="Rows">الصفوف.</param>
/// <param name="ReceivableTotal">مجموع المحتجز المدين القائم.</param>
/// <param name="PayableTotal">مجموع المحتجز الدائن القائم.</param>
public sealed record ProjectsRetentionRegister(
    DateOnly AsOf,
    IReadOnlyList<ProjectsRetentionEntry> Rows,
    decimal ReceivableTotal,
    decimal PayableTotal);

/// <summary>طرفٌ في كشف المقاولين وأثره المُرحَّل.</summary>
/// <param name="SubcontractorId">المقاول.</param>
/// <param name="Code">رمزه.</param>
/// <param name="NameAr">اسمه العربي.</param>
/// <param name="NameTranslations">ترجمات اسمه.</param>
/// <param name="Effect">أثره على الحساب الضابط.</param>
public sealed record ProjectsStatementLine(
    Guid SubcontractorId,
    string Code,
    string NameAr,
    IReadOnlyList<ProjectsNameValue> NameTranslations,
    decimal Effect);

/// <summary>كشف المقاولين ومطابقته بنقطة ضبطه.</summary>
/// <param name="AsOf">التاريخ.</param>
/// <param name="Rows">الأطراف.</param>
/// <param name="SubledgerTotal">مجموع الدفتر المساعد.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط.</param>
/// <param name="Divergence">الفارق.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟</param>
public sealed record ProjectsSubcontractorStatement(
    DateOnly AsOf,
    IReadOnlyList<ProjectsStatementLine> Rows,
    decimal SubledgerTotal,
    decimal ControlTotal,
    decimal Divergence,
    bool IsReconciled);

/// <summary>
/// موقف العقد — مشتقٌّ من المُرحَّل وحده، و<b>بديلٌ لتقرير ربحية المشروع لا نسخةٌ منه</b>.
/// </summary>
/// <param name="ContractId">العقد.</param>
/// <param name="ContractNumber">رقمه.</param>
/// <param name="PostedCertificateCount">عدد مستخلصاته المُرحَّلة.</param>
/// <param name="RetentionOutstanding">المحتجز القائم.</param>
/// <param name="AdvanceOutstanding">الدفعة غير المستنفَدة.</param>
/// <param name="PendingPolicy">البنود المعلَّقة.</param>
public sealed record ProjectsContractPosition(
    Guid ContractId,
    string ContractNumber,
    int PostedCertificateCount,
    decimal RetentionOutstanding,
    decimal AdvanceOutstanding,
    IReadOnlyList<ProjectsPendingItem> PendingPolicy);
