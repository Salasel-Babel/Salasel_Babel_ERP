using Babel.SharedKernel;

namespace Babel.Projects.Application;

/// <summary>
/// كمّية بوحدتها داخل الوحدة.
/// <para>
/// <b>ولماذا نوعٌ هنا لا نوعٌ مستورَد:</b> لا نوع «مقدار ووحدة» في <c>Babel.Contracts</c>
/// ولا في <c>SharedKernel</c>؛ ونظيره في المخزون يعيش في سطح تلك الوحدة ولا تبلغه
/// المقاولات — خريطة الوحدات تعطي كل وحدة أفقية <c>{SharedKernel, Contracts, Core}</c>
/// بالضبط. <b>ولا تحويل وحدات هنا</b>: السطر يُرفض إن خالفت وحدتُه وحدةَ بنده، ولا
/// تُبنى نسخةٌ ثانية من قاعدةٍ يملكها موضعٌ آخر.
/// </para>
/// </summary>
/// <param name="Magnitude">المقدار.</param>
/// <param name="Unit">رمز الوحدة كما كُتب في العقد.</param>
public sealed record ProjectQuantity(decimal Magnitude, string Unit);

/// <summary>مسوّدة مشروع. رمزه هوية تدخل بُعد <c>project</c>، لا اسم عرض.</summary>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم — سجلٌّ عربي وترجماتٌ صفوف.</param>
/// <param name="StartedOn">تاريخ البدء.</param>
public sealed record ProjectDraft(string Code, TranslatedName Name, DateOnly StartedOn);

/// <summary>عقدٌ في قائمة مختصرة تحت مشروعه.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">رقم العقد.</param>
/// <param name="CurrencyCode">عملته.</param>
public sealed record ContractSummary(Guid Id, string Number, string CurrencyCode);

/// <summary>مشروع كما يُقرأ.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="StartedOn">تاريخ البدء.</param>
/// <param name="IsActive">هل هو عامل؟</param>
/// <param name="Contracts">عقوده.</param>
public sealed record ProjectView(
    Guid Id,
    string Code,
    TranslatedName Name,
    DateOnly StartedOn,
    bool IsActive,
    IReadOnlyList<ContractSummary> Contracts);

/// <summary>مسوّدة بند جدول كميات.</summary>
/// <param name="Code">رمز البند داخل العقد.</param>
/// <param name="DescriptionAr">بيانه.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
public sealed record BoqItemDraft(
    string Code,
    string DescriptionAr,
    ProjectQuantity ContractQuantity,
    Money UnitRate);

/// <summary>مسوّدة عقد مقاولة.</summary>
/// <param name="Number">رقمه — يرسله العميل ويُتحقَّق من تفرّده.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="CustomerPartyId">معرّف العميل في دفتره المساعد.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز كما نصّ عليها العقد.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="Items">بنود جدول الكميات.</param>
public sealed record ContractDraft(
    string Number,
    Guid ProjectId,
    string CustomerPartyId,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<BoqItemDraft> Items);

/// <summary>بند جدول كميات كما يُقرأ — <b>بمعرّفه</b>، وهو مدخل سطور المستخلص.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="LineNo">ترتيبه.</param>
/// <param name="DescriptionAr">بيانه.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
/// <param name="ChangeOrderId">الأمر التغييري الذي أدخله، أو <c>null</c>.</param>
public sealed record BoqItemView(
    Guid Id,
    string Code,
    int LineNo,
    string DescriptionAr,
    ProjectQuantity ContractQuantity,
    Money UnitRate,
    Guid? ChangeOrderId);

/// <summary>
/// عقد كما يُقرأ، <b>ومعه بنوده المعلَّقة</b>.
/// <para>
/// وإظهار البنود المعلَّقة على العقد نفسه مقصود: من يقرأ العقد قبل أن يُنشئ مستخلصاً
/// يعرف سلفاً ما الذي سيرفضه الترحيل ولماذا، بدل أن يكتشفه عند أول محاولة مالية.
/// </para>
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="ProjectCode">رمز المشروع — وهو ما يدخل بُعد القيد.</param>
/// <param name="CustomerPartyId">العميل.</param>
/// <param name="CurrencyCode">العملة.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز من العقد.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="PendingPolicy">البنود المعلَّقة التي تمنع الترحيل اليوم.</param>
public sealed record ContractView(
    Guid Id,
    string Number,
    Guid ProjectId,
    string ProjectCode,
    string CustomerPartyId,
    string CurrencyCode,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<PendingPolicyItem> PendingPolicy);

/// <summary>مسوّدة أمر تغييري ببنوده الجديدة.</summary>
/// <param name="Number">رقمه.</param>
/// <param name="ContractId">العقد.</param>
/// <param name="IssuedOn">تاريخ إصداره.</param>
/// <param name="ReasonAr">سببه.</param>
/// <param name="ApprovedBy">من اعتمده.</param>
/// <param name="AddedItems">البنود التي يُدخلها على جدول الكميات.</param>
public sealed record ChangeOrderDraft(
    string Number,
    Guid ContractId,
    DateOnly IssuedOn,
    string ReasonAr,
    string ApprovedBy,
    IReadOnlyList<BoqItemDraft> AddedItems);

/// <summary>
/// أمر تغييري كما يُقرأ. <b>ولا <c>entryId</c> ولا <c>alreadyPosted</c> فيه</b>: حقلٌ
/// فارغ لهما يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً».
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ContractId">العقد.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="ReasonAr">السبب.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="AddedItems">بنوده الجديدة.</param>
public sealed record ChangeOrderView(
    Guid Id,
    string Number,
    Guid ContractId,
    DateOnly IssuedOn,
    string ReasonAr,
    string ApprovedBy,
    IReadOnlyList<BoqItemView> AddedItems);

/// <summary>مسوّدة مقاول من الباطن.</summary>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه.</param>
/// <param name="VatNumber">رقم تسجيله الضريبي، أو فراغ.</param>
public sealed record SubcontractorDraft(string Code, TranslatedName Name, string VatNumber);

/// <summary>مقاول من الباطن كما يُقرأ.</summary>
/// <param name="Id">المعرّف — وهو الطرف في دفتر <c>subcontractor</c>.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي.</param>
/// <param name="IsActive">هل هو عامل؟</param>
public sealed record SubcontractorView(Guid Id, string Code, TranslatedName Name, string VatNumber, bool IsActive);

/// <summary>مسوّدة بند عقد باطن.</summary>
/// <param name="Code">رمزه.</param>
/// <param name="DescriptionAr">بيانه.</param>
/// <param name="ContractQuantity">كمّيته التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر وحدته.</param>
public sealed record SubcontractLineDraft(
    string Code,
    string DescriptionAr,
    ProjectQuantity ContractQuantity,
    Money UnitRate);

/// <summary>مسوّدة عقد باطن.</summary>
/// <param name="Number">رقمه.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="SubcontractorId">المقاول.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة محتجزه.</param>
/// <param name="GuaranteeMonths">فترة ضمانه بالأشهر.</param>
/// <param name="Lines">بنوده.</param>
public sealed record SubcontractDraft(
    string Number,
    Guid ProjectId,
    Guid SubcontractorId,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<SubcontractLineDraft> Lines);

/// <summary>عقد باطن كما يُقرأ.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="ProjectCode">رمز المشروع.</param>
/// <param name="SubcontractorId">المقاول.</param>
/// <param name="CurrencyCode">العملة.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="PendingPolicy">البنود المعلَّقة التي تمنع ترحيل مستخلصاته.</param>
public sealed record SubcontractView(
    Guid Id,
    string Number,
    Guid ProjectId,
    string ProjectCode,
    Guid SubcontractorId,
    string CurrencyCode,
    DateOnly SignedOn,
    decimal RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<PendingPolicyItem> PendingPolicy);

/// <summary>بند عقد باطن كما يُقرأ — بمعرّفه، وهو مدخل سطور مستخلصه.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
public sealed record SubcontractLineView(
    Guid Id,
    string Code,
    int LineNo,
    string DescriptionAr,
    ProjectQuantity ContractQuantity,
    Money UnitRate);

/// <summary>
/// مسوّدة سطر مستخلص.
/// <para>
/// <b>والكمّيتان صريحتان بوحدتيهما</b>: التراكمية والسابقة معاً. وقيمة الفترة تُشتقّ
/// طرحاً، والأساس المطروح منه <b>آخر مستخلصٍ مُرحَّل</b> — لا آخر مسوّدة.
/// </para>
/// </summary>
/// <param name="ItemId">بند جدول الكميات أو بند عقد الباطن، أو <c>null</c> على سطر غرامة أو خصم.</param>
/// <param name="LineKind">صنف السطر: عمل · غرامة · خصم.</param>
/// <param name="DescriptionAr">بيان السطر.</param>
/// <param name="CumulativeQuantity">الكمّية التراكمية بوحدتها — أو صفراً على سطر غرامة.</param>
/// <param name="Amount">مبلغ الغرامة أو الخصم — أو صفراً على سطر عمل.</param>
public sealed record CertificateLineDraft(
    Guid? ItemId,
    string LineKind,
    string DescriptionAr,
    ProjectQuantity CumulativeQuantity,
    Money Amount);

/// <summary>سطر مستخلص كما يُقرأ.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="LineKind">الصنف.</param>
/// <param name="ItemId">البند.</param>
/// <param name="ItemCode">رمز البند، أو فراغ.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="CumulativeQuantity">الكمّية التراكمية بوحدتها.</param>
/// <param name="PreviousQuantity">الكمّية السابقة من آخر مستخلص مُرحَّل، بوحدتها.</param>
/// <param name="Amount">مبلغ الغرامة أو الخصم.</param>
public sealed record CertificateLineView(
    Guid Id,
    int LineNo,
    string LineKind,
    Guid? ItemId,
    string ItemCode,
    string DescriptionAr,
    ProjectQuantity CumulativeQuantity,
    ProjectQuantity PreviousQuantity,
    Money Amount);

/// <summary>مسوّدة مستخلص — عميلٍ كان أو باطن.</summary>
/// <param name="Number">الرقم المرئي.</param>
/// <param name="OwnerId">العقد أو عقد الباطن.</param>
/// <param name="SequenceNo">التسلسل داخل العقد.</param>
/// <param name="PeriodFrom">بداية الفترة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="Lines">السطور.</param>
public sealed record CertificateDraft(
    string Number,
    Guid OwnerId,
    int SequenceNo,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    IReadOnlyList<CertificateLineDraft> Lines);

/// <summary>
/// مستخلص كما يُقرأ.
/// <para>
/// <b>ولاحظ ما ليس فيه: مبالغ محسوبة.</b> الأربعة التي تسمّيها المصفوفة —
/// قيمة الأعمال والضريبة والمحتجز واسترداد الدفعة — لكلٍّ منها حاسبٌ يجب أن يعيش في
/// هذه الوحدة، <b>ولم يُبنَ أيٌّ منها</b>: موضع التقريب ومستوى التصنيف الضريبي ووعاء
/// المحتجز وقاعدة الاسترداد بنودٌ معلَّقة على المالك. وعرضُ رقمٍ قبل أن يُحسم أساسه
/// أسوأ من غيابه.
/// </para>
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="OwnerId">العقد أو عقد الباطن.</param>
/// <param name="SequenceNo">التسلسل.</param>
/// <param name="PeriodFrom">بداية الفترة.</param>
/// <param name="PeriodTo">نهايتها.</param>
/// <param name="State">الحالة.</param>
/// <param name="FrozenRetentionRate">نسبة المحتجز المجمَّدة لحظة المسوّدة.</param>
/// <param name="Lines">السطور بكمّياتها.</param>
/// <param name="PendingPolicy">البنود المعلَّقة التي تمنع ترحيله.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل ردّ هذا النداءُ ترحيلاً سابقاً؟</param>
public sealed record CertificateView(
    Guid Id,
    string Number,
    Guid OwnerId,
    int SequenceNo,
    DateOnly PeriodFrom,
    DateOnly PeriodTo,
    string State,
    decimal FrozenRetentionRate,
    IReadOnlyList<CertificateLineView> Lines,
    IReadOnlyList<PendingPolicyItem> PendingPolicy,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>مسوّدة صرف دفعة مقدمة لمقاول من الباطن.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="SubcontractId">عقد الباطن.</param>
/// <param name="PaidOn">تاريخ الصرف.</param>
/// <param name="Amount">المبلغ — يُدخله المستخدم ولا يشتقّه حاسب.</param>
/// <param name="SettlementMethod">طريقة التسوية — مؤهّل الدور.</param>
/// <param name="TreasuryPartyId">طرف الخزينة في دفترها المساعد.</param>
/// <param name="GuaranteeId">خطاب ضمان الدفعة المقدمة.</param>
public sealed record SubcontractorAdvanceDraft(
    string Number,
    Guid SubcontractId,
    DateOnly PaidOn,
    Money Amount,
    string SettlementMethod,
    string TreasuryPartyId,
    Guid? GuaranteeId);

/// <summary>مستند مالي بسيط كما يُقرأ: حالته ومبلغه ومعرّف قيده إن رُحّل.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل ردّ هذا النداءُ ترحيلاً سابقاً؟</param>
public sealed record ProjectsDocumentView(
    Guid Id,
    string Number,
    string State,
    Money Amount,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>مسوّدة إفراج عن محتجز دائن على دفعة محتجزٍ مُسمّاة.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="RetentionMovementId">حركة المحتجز المُفرَج عنها.</param>
/// <param name="ReleasedOn">تاريخ الإفراج.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="ApprovedBy">الاعتماد الصريح الذي يشترطه نصّ الإطلاق.</param>
public sealed record RetentionReleaseDraft(
    string Number,
    Guid RetentionMovementId,
    DateOnly ReleasedOn,
    Money Amount,
    string ApprovedBy);

/// <summary>مسوّدة تحصيل محتجز مدين من العميل.</summary>
/// <param name="Number">الرقم.</param>
/// <param name="RetentionMovementId">حركة المحتجز المُحصَّلة.</param>
/// <param name="CollectedOn">تاريخ التحصيل.</param>
/// <param name="Amount">المبلغ.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">طرف الخزينة.</param>
public sealed record RetentionCollectionDraft(
    string Number,
    Guid RetentionMovementId,
    DateOnly CollectedOn,
    Money Amount,
    string SettlementMethod,
    string TreasuryPartyId);

/// <summary>مسوّدة خطاب ضمان — سجلٌّ لا يُرحَّل.</summary>
/// <param name="Number">رقم الخطاب.</param>
/// <param name="Kind">صنفه.</param>
/// <param name="ContractId">عقد العميل، أو <c>null</c>.</param>
/// <param name="SubcontractId">عقد الباطن، أو <c>null</c>.</param>
/// <param name="IssuerNameAr">اسم المُصدِر.</param>
/// <param name="Amount">مبلغه.</param>
/// <param name="EffectiveFrom">بدء سريانه.</param>
/// <param name="ExpiresOn">انتهاؤه.</param>
/// <param name="AttachmentId">معرّف المرفق على السطح المنشور للمرفقات.</param>
public sealed record GuaranteeDraft(
    string Number,
    string Kind,
    Guid? ContractId,
    Guid? SubcontractId,
    string IssuerNameAr,
    Money Amount,
    DateOnly EffectiveFrom,
    DateOnly ExpiresOn,
    string AttachmentId);

/// <summary>خطاب ضمان كما يُقرأ — <b>بابان لا ثلاثة</b>، فلا <c>entryId</c> فيه.</summary>
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
public sealed record GuaranteeView(
    Guid Id,
    string Number,
    string Kind,
    Guid? ContractId,
    Guid? SubcontractId,
    string IssuerNameAr,
    Money Amount,
    DateOnly EffectiveFrom,
    DateOnly ExpiresOn,
    string AttachmentId);

/// <summary>صفٌّ في سجلّ المحتجزات — مشتقٌّ من المُرحَّل وحده.</summary>
/// <param name="MovementId">معرّف الحركة.</param>
/// <param name="Side">الجانب: مدينٌ لدى العميل أو دائنٌ على المقاول.</param>
/// <param name="PartyKind">نوع الدفتر المساعد للطرف.</param>
/// <param name="PartyId">الطرف.</param>
/// <param name="ProjectCode">رمز المشروع.</param>
/// <param name="DocumentType">المستند الذي أنشأها.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="Amount">المبلغ الأصلي.</param>
/// <param name="Outstanding">الرصيد القائم بعد ما أُفرِج عنه أو حُصِّل.</param>
/// <param name="MovedOn">تاريخ الحركة.</param>
/// <param name="DueOn">تاريخ استحقاق الإفراج.</param>
public sealed record RetentionRegisterRow(
    Guid MovementId,
    string Side,
    string PartyKind,
    string PartyId,
    string ProjectCode,
    string DocumentType,
    string DocumentId,
    Money Amount,
    Money Outstanding,
    DateOnly MovedOn,
    DateOnly DueOn);

/// <summary>سجلّ المحتجزات مدينةً ودائنة، وهو ما تُطابَق به نقطتا الضبط.</summary>
/// <param name="AsOf">تاريخ القراءة.</param>
/// <param name="Rows">الصفوف.</param>
/// <param name="ReceivableTotal">مجموع المحتجز المدين القائم.</param>
/// <param name="PayableTotal">مجموع المحتجز الدائن القائم.</param>
public sealed record RetentionRegister(
    DateOnly AsOf,
    IReadOnlyList<RetentionRegisterRow> Rows,
    Money ReceivableTotal,
    Money PayableTotal);

/// <summary>صفّ في كشف المقاولين: الطرف وأثره المُرحَّل.</summary>
/// <param name="SubcontractorId">المقاول.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه.</param>
/// <param name="Effect">أثره على الحساب الضابط بمنطق «مدين ناقص دائن».</param>
public sealed record SubcontractorStatementRow(
    Guid SubcontractorId,
    string Code,
    TranslatedName Name,
    Money Effect);

/// <summary>
/// كشف المقاولين، وهو المطابقة المُعلَنة نصّاً في بيانات الدفاتر المساعدة:
/// «كشف المقاولين = رصيد الحساب».
/// </summary>
/// <param name="AsOf">تاريخ الكشف.</param>
/// <param name="Rows">الأطراف بآثارها.</param>
/// <param name="SubledgerTotal">مجموع الدفتر المساعد المحسوب من مستنداته.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط في دفتر الأستاذ.</param>
/// <param name="Divergence">الفارق: الدفتر المساعد ناقص نقطة الضبط.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟ لا «قريب من الصفر».</param>
public sealed record SubcontractorStatement(
    DateOnly AsOf,
    IReadOnlyList<SubcontractorStatementRow> Rows,
    Money SubledgerTotal,
    Money ControlTotal,
    Money Divergence,
    bool IsReconciled);

/// <summary>
/// موقف العقد — <b>مشتقٌّ من المُرحَّل وحده</b>.
/// <para>
/// وهو <b>بديلٌ لتقرير ربحية المشروع لا نسخةٌ منه</b>: قاعدة تحميل تكلفة الموظف
/// والمعدّة على المشروع غير محسومة، وثلاثة حسابات تكلفة مشاريع قائمة في الدليل
/// <b>بلا كاتب</b> — فرقمُ ربحيةٍ مقنعٌ بلا قاعدة معلنة أسوأ من غيابه.
/// </para>
/// </summary>
/// <param name="ContractId">العقد.</param>
/// <param name="ContractNumber">رقمه.</param>
/// <param name="PostedCertificateCount">عدد مستخلصاته المُرحَّلة.</param>
/// <param name="RetentionOutstanding">المحتجز القائم عليه.</param>
/// <param name="AdvanceOutstanding">الدفعة المقدمة غير المستنفَدة.</param>
/// <param name="PendingPolicy">البنود المعلَّقة التي تمنع ترحيل مستخلصاته.</param>
public sealed record ContractPosition(
    Guid ContractId,
    string ContractNumber,
    int PostedCertificateCount,
    Money RetentionOutstanding,
    Money AdvanceOutstanding,
    IReadOnlyList<PendingPolicyItem> PendingPolicy);
