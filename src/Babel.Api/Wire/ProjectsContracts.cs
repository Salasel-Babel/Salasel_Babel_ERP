namespace Babel.Api.Wire;

// ‏**ولا حقل إنجليزي ثابت في أي نوع من أنواع المقاولات في هذا الملفّ**: الاسم العربي هو
// السجلّ، وترجماته تعبر في `NameValueDto` بأوسمة BCP-47 — والإنجليزية **واحدة من N** لا
// نصف اثنين (ADR-0021 بند 2). وزوجٌ ثابت ar/en عاجزٌ بنيوياً عن الثالثة، ويجعل المحاسب
// الأردي يقرأ إنجليزيةً بدل لغته.
//
// ‏**والكمّية تعبر في `MeasureRequestDto` و`MeasureDto`** — الشكل المنشور نفسه الذي
// يستعمله المخزون، لا شكلاً ثانياً بمعنى واحد: مخطّطان متطابقان باسمين يُحرَّر أحدهما
// ويُنسى الآخر، وهو شكل فخ-81 بعينه.

/// <summary>بندٌ معلَّق على قرار مالك، كما يخرج على السلك.</summary>
/// <param name="Code">رمزه الثابت — نقطة الاعتماد البرمجية.</param>
/// <param name="TitleAr">عنوانه بالعربية.</param>
/// <param name="TitleEn">عنوانه بالإنجليزية.</param>
/// <param name="SourceRef">الموضع الذي يحمل السؤال كاملاً بخياراته.</param>
internal sealed record ProjectsPendingItemDto(string Code, string TitleAr, string TitleEn, string SourceRef);

/// <summary>طلب تسجيل مشروع.</summary>
internal sealed record ProjectRequestDto
{
    /// <summary>الرمز — وهو القيمة الحرفية التي تدخل بُعد المشروع على سطر القيد.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم العربي — السجلّ لا ترجمته.</summary>
    public required string NameAr { get; init; }

    /// <summary>ترجمات الاسم، مفاتيحها أوسمة BCP-47.</summary>
    public required IReadOnlyList<NameValueDto> NameTranslations { get; init; }

    /// <summary>تاريخ بدء المشروع بصيغة yyyy-MM-dd.</summary>
    public required string StartedOn { get; init; }
}

/// <summary>عقدٌ مختصر تحت مشروعه.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="CurrencyCode">العملة.</param>
internal sealed record ProjectContractSummaryDto(string Id, string Number, string CurrencyCode);

/// <summary>مشروع كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">ترجماته.</param>
/// <param name="StartedOn">تاريخ البدء.</param>
/// <param name="IsActive">هل هو عامل؟</param>
/// <param name="Contracts">عقوده.</param>
internal sealed record ProjectDto(
    string Id,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string StartedOn,
    bool IsActive,
    IReadOnlyList<ProjectContractSummaryDto> Contracts);

/// <summary>قائمة المشاريع.</summary>
/// <param name="ProjectCount">عددها.</param>
/// <param name="Projects">المشاريع.</param>
internal sealed record ProjectListDto(int ProjectCount, IReadOnlyList<ProjectDto> Projects);

/// <summary>طلب بند جدول كميات.</summary>
internal sealed record BoqItemRequestDto
{
    /// <summary>رمز البند داخل العقد.</summary>
    public required string Code { get; init; }

    /// <summary>بيانه بالعربية.</summary>
    public required string DescriptionAr { get; init; }

    /// <summary>الكمّية التعاقدية بوحدتها.</summary>
    public required MeasureRequestDto ContractQuantity { get; init; }

    /// <summary>سعر الوحدة نصّاً.</summary>
    public required WireDecimal UnitRate { get; init; }
}

/// <summary>طلب إنشاء عقد مقاولة.</summary>
internal sealed record ProjectContractRequestDto
{
    /// <summary>الرقم — يرسله العميل ويُتحقَّق من تفرّده.</summary>
    public required string Number { get; init; }

    /// <summary>المشروع.</summary>
    public required string ProjectId { get; init; }

    /// <summary>معرّف العميل في دفتره المساعد.</summary>
    public required string CustomerPartyId { get; init; }

    /// <summary>تاريخ التوقيع بصيغة yyyy-MM-dd.</summary>
    public required string SignedOn { get; init; }

    /// <summary>نسبة المحتجز كسراً عشرياً نصّاً — من العقد لا من الكود.</summary>
    public required WireDecimal RetentionRate { get; init; }

    /// <summary>فترة الضمان بالأشهر.</summary>
    public required int GuaranteeMonths { get; init; }

    /// <summary>بنود جدول الكميات.</summary>
    public required IReadOnlyList<BoqItemRequestDto> Items { get; init; }
}

/// <summary>عقد كما يخرج على السلك، ومعه بنوده المعلَّقة.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ProjectId">المشروع.</param>
/// <param name="ProjectCode">رمز المشروع.</param>
/// <param name="CustomerPartyId">العميل.</param>
/// <param name="CurrencyCode">العملة.</param>
/// <param name="SignedOn">تاريخ التوقيع.</param>
/// <param name="RetentionRate">نسبة المحتجز.</param>
/// <param name="GuaranteeMonths">فترة الضمان بالأشهر.</param>
/// <param name="PendingPolicy">البنود المعلَّقة التي تمنع ترحيل مستخلصاته.</param>
internal sealed record ProjectContractDto(
    string Id,
    string Number,
    string ProjectId,
    string ProjectCode,
    string CustomerPartyId,
    string CurrencyCode,
    string SignedOn,
    string RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<ProjectsPendingItemDto> PendingPolicy);

/// <summary>بند جدول كميات كما يخرج على السلك — بمعرّفه.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
/// <param name="ChangeOrderId">الأمر التغييري الذي أدخله، أو <c>null</c>.</param>
internal sealed record BoqItemDto(
    string Id,
    string Code,
    int LineNo,
    string DescriptionAr,
    MeasureDto ContractQuantity,
    string UnitRate,
    string? ChangeOrderId);

/// <summary>قائمة بنود جدول الكميات.</summary>
/// <param name="ItemCount">عددها.</param>
/// <param name="Items">البنود.</param>
internal sealed record BoqItemListDto(int ItemCount, IReadOnlyList<BoqItemDto> Items);

/// <summary>طلب أمر تغييري.</summary>
internal sealed record ChangeOrderRequestDto
{
    /// <summary>الرقم.</summary>
    public required string Number { get; init; }

    /// <summary>العقد.</summary>
    public required string ContractId { get; init; }

    /// <summary>تاريخ الإصدار بصيغة yyyy-MM-dd.</summary>
    public required string IssuedOn { get; init; }

    /// <summary>سبب التغيير بالعربية.</summary>
    public required string ReasonAr { get; init; }

    /// <summary>من اعتمده.</summary>
    public required string ApprovedBy { get; init; }

    /// <summary>البنود التي يُدخلها على جدول الكميات.</summary>
    public required IReadOnlyList<BoqItemRequestDto> AddedItems { get; init; }
}

/// <summary>
/// أمر تغييري كما يخرج على السلك. <b>ولا <c>entryId</c> ولا <c>alreadyPosted</c> فيه</b>:
/// حقلٌ فارغ لهما يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً»، فيبني عليه العميل زرّاً
/// لا وجود له.
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="ContractId">العقد.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="ReasonAr">السبب.</param>
/// <param name="ApprovedBy">المعتمِد.</param>
/// <param name="AddedItems">البنود الجديدة.</param>
internal sealed record ChangeOrderDto(
    string Id,
    string Number,
    string ContractId,
    string IssuedOn,
    string ReasonAr,
    string ApprovedBy,
    IReadOnlyList<BoqItemDto> AddedItems);

/// <summary>قائمة أوامر تغييرية.</summary>
/// <param name="ChangeOrderCount">عددها.</param>
/// <param name="ChangeOrders">الأوامر.</param>
internal sealed record ChangeOrderListDto(int ChangeOrderCount, IReadOnlyList<ChangeOrderDto> ChangeOrders);

/// <summary>طلب تسجيل مقاول من الباطن.</summary>
internal sealed record SubcontractorRequestDto
{
    /// <summary>الرمز.</summary>
    public required string Code { get; init; }

    /// <summary>الاسم العربي — السجلّ.</summary>
    public required string NameAr { get; init; }

    /// <summary>ترجمات الاسم.</summary>
    public required IReadOnlyList<NameValueDto> NameTranslations { get; init; }

    /// <summary>رقم التسجيل الضريبي، أو نصّ فارغ لمن لا رقم له.</summary>
    public required string VatNumber { get; init; }
}

/// <summary>مقاول من الباطن كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف — وهو الطرف في دفتره المساعد.</param>
/// <param name="Code">الرمز.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameTranslations">الترجمات.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي.</param>
/// <param name="IsActive">هل هو عامل؟</param>
internal sealed record SubcontractorDto(
    string Id,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string VatNumber,
    bool IsActive);

/// <summary>طلب بند عقد باطن.</summary>
internal sealed record SubcontractLineRequestDto
{
    /// <summary>الرمز.</summary>
    public required string Code { get; init; }

    /// <summary>البيان بالعربية.</summary>
    public required string DescriptionAr { get; init; }

    /// <summary>الكمّية التعاقدية بوحدتها.</summary>
    public required MeasureRequestDto ContractQuantity { get; init; }

    /// <summary>سعر الوحدة نصّاً.</summary>
    public required WireDecimal UnitRate { get; init; }
}

/// <summary>طلب إنشاء عقد باطن.</summary>
internal sealed record SubcontractRequestDto
{
    /// <summary>الرقم.</summary>
    public required string Number { get; init; }

    /// <summary>المشروع.</summary>
    public required string ProjectId { get; init; }

    /// <summary>المقاول.</summary>
    public required string SubcontractorId { get; init; }

    /// <summary>تاريخ التوقيع بصيغة yyyy-MM-dd.</summary>
    public required string SignedOn { get; init; }

    /// <summary>نسبة المحتجز كسراً عشرياً نصّاً.</summary>
    public required WireDecimal RetentionRate { get; init; }

    /// <summary>فترة الضمان بالأشهر.</summary>
    public required int GuaranteeMonths { get; init; }

    /// <summary>البنود.</summary>
    public required IReadOnlyList<SubcontractLineRequestDto> Lines { get; init; }
}

/// <summary>عقد باطن كما يخرج على السلك.</summary>
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
internal sealed record SubcontractDto(
    string Id,
    string Number,
    string ProjectId,
    string ProjectCode,
    string SubcontractorId,
    string CurrencyCode,
    string SignedOn,
    string RetentionRate,
    int GuaranteeMonths,
    IReadOnlyList<ProjectsPendingItemDto> PendingPolicy);

/// <summary>بند عقد باطن كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="ContractQuantity">الكمّية التعاقدية بوحدتها.</param>
/// <param name="UnitRate">سعر الوحدة.</param>
internal sealed record SubcontractLineDto(
    string Id,
    string Code,
    int LineNo,
    string DescriptionAr,
    MeasureDto ContractQuantity,
    string UnitRate);

/// <summary>قائمة بنود عقد باطن.</summary>
/// <param name="LineCount">عددها.</param>
/// <param name="Lines">البنود.</param>
internal sealed record SubcontractLineListDto(int LineCount, IReadOnlyList<SubcontractLineDto> Lines);

/// <summary>طلب سطر مستخلص.</summary>
internal sealed record CertificateLineRequestDto
{
    /// <summary>البند، أو <c>null</c> على سطر غرامة أو خصم.</summary>
    public required string? ItemId { get; init; }

    /// <summary>الصنف: <c>WORK</c> · <c>PENALTY</c> · <c>DEDUCTION</c>.</summary>
    public required string LineKind { get; init; }

    /// <summary>بيان السطر بالعربية.</summary>
    public required string DescriptionAr { get; init; }

    /// <summary>الكمّية التراكمية بوحدتها — صفرٌ على سطر غرامة أو خصم.</summary>
    public required MeasureRequestDto CumulativeQuantity { get; init; }

    /// <summary>مبلغ الغرامة أو الخصم نصّاً — صفرٌ على سطر عمل.</summary>
    public required WireDecimal Amount { get; init; }
}

/// <summary>طلب إنشاء مستخلص <b>مسوّدة</b>.</summary>
internal sealed record CertificateRequestDto
{
    /// <summary>الرقم المرئي — يرسله العميل ويُتحقَّق من تفرّده.</summary>
    public required string Number { get; init; }

    /// <summary>العقد أو عقد الباطن.</summary>
    public required string OwnerId { get; init; }

    /// <summary>تسلسل المستخلص داخل العقد.</summary>
    public required int SequenceNo { get; init; }

    /// <summary>بداية الفترة بصيغة yyyy-MM-dd.</summary>
    public required string PeriodFrom { get; init; }

    /// <summary>نهاية الفترة بصيغة yyyy-MM-dd.</summary>
    public required string PeriodTo { get; init; }

    /// <summary>السطور.</summary>
    public required IReadOnlyList<CertificateLineRequestDto> Lines { get; init; }
}

/// <summary>سطر مستخلص كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="LineNo">الترتيب.</param>
/// <param name="LineKind">الصنف.</param>
/// <param name="ItemId">البند.</param>
/// <param name="ItemCode">رمزه، أو نصّ فارغ.</param>
/// <param name="DescriptionAr">البيان.</param>
/// <param name="CumulativeQuantity">الكمّية التراكمية بوحدتها.</param>
/// <param name="PreviousQuantity">الكمّية السابقة من آخر مستخلص مُرحَّل.</param>
/// <param name="Amount">مبلغ الغرامة أو الخصم.</param>
internal sealed record CertificateLineDto(
    string Id,
    int LineNo,
    string LineKind,
    string? ItemId,
    string ItemCode,
    string DescriptionAr,
    MeasureDto CumulativeQuantity,
    MeasureDto PreviousQuantity,
    string Amount);

/// <summary>
/// مستخلص كما يخرج على السلك.
/// <para>
/// <b>ولاحظ ما ليس فيه: مبالغ محسوبة.</b> قيمة الأعمال والضريبة والمحتجز واسترداد
/// الدفعة أربعةٌ لكلٍّ منها حاسبٌ يجب أن يعيش في الوحدة، ولم يُبنَ أيٌّ منها لأن أساسه
/// بندٌ معلَّق على قرار محاسب. وعرضُ رقمٍ قبل أن يُحسم أساسه أسوأ من غيابه.
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
internal sealed record CertificateDto(
    string Id,
    string Number,
    string OwnerId,
    int SequenceNo,
    string PeriodFrom,
    string PeriodTo,
    string State,
    string RetentionRate,
    IReadOnlyList<CertificateLineDto> Lines,
    IReadOnlyList<ProjectsPendingItemDto> PendingPolicy,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>قائمة مستخلصات عقد.</summary>
/// <param name="CertificateCount">عددها.</param>
/// <param name="Certificates">المستخلصات.</param>
internal sealed record CertificateListDto(int CertificateCount, IReadOnlyList<CertificateDto> Certificates);

/// <summary>طلب صرف دفعة مقدمة لمقاول من الباطن.</summary>
internal sealed record SubcontractorAdvanceRequestDto
{
    /// <summary>الرقم.</summary>
    public required string Number { get; init; }

    /// <summary>عقد الباطن.</summary>
    public required string SubcontractId { get; init; }

    /// <summary>تاريخ الصرف بصيغة yyyy-MM-dd.</summary>
    public required string PaidOn { get; init; }

    /// <summary>المبلغ نصّاً — واقعةٌ يُدخلها المستخدم لا رقمٌ يشتقّه حاسب.</summary>
    public required WireDecimal Amount { get; init; }

    /// <summary>طريقة التسوية — مؤهّل الدور، لا حساب.</summary>
    public required string SettlementMethod { get; init; }

    /// <summary>معرّف الخزينة أو الحساب البنكي في دفترها المساعد.</summary>
    public required string TreasuryPartyId { get; init; }

    /// <summary>خطاب ضمان الدفعة المقدمة الذي يشترطه نصّ إطلاق الحدث، أو <c>null</c>.</summary>
    public required string? GuaranteeId { get; init; }
}

/// <summary>طلب إفراج عن محتجز دائن.</summary>
internal sealed record RetentionReleaseRequestDto
{
    /// <summary>الرقم.</summary>
    public required string Number { get; init; }

    /// <summary>دفعة المحتجز المُفرَج عنها — والإفراج على حركةٍ مُسمّاة لا على رصيد.</summary>
    public required string RetentionMovementId { get; init; }

    /// <summary>تاريخ الإفراج بصيغة yyyy-MM-dd.</summary>
    public required string ReleasedOn { get; init; }

    /// <summary>المبلغ نصّاً.</summary>
    public required WireDecimal Amount { get; init; }

    /// <summary>الاعتماد الصريح الذي يشترطه نصّ الإطلاق.</summary>
    public required string ApprovedBy { get; init; }
}

/// <summary>طلب تحصيل محتجز مدين من العميل.</summary>
internal sealed record RetentionCollectionRequestDto
{
    /// <summary>الرقم.</summary>
    public required string Number { get; init; }

    /// <summary>دفعة المحتجز المُحصَّلة.</summary>
    public required string RetentionMovementId { get; init; }

    /// <summary>تاريخ التحصيل بصيغة yyyy-MM-dd.</summary>
    public required string CollectedOn { get; init; }

    /// <summary>المبلغ نصّاً.</summary>
    public required WireDecimal Amount { get; init; }

    /// <summary>طريقة التسوية.</summary>
    public required string SettlementMethod { get; init; }

    /// <summary>طرف الخزينة.</summary>
    public required string TreasuryPartyId { get; init; }
}

/// <summary>مستند مقاولات مالي كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Amount">المبلغ نصّاً.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">هل ردّ هذا النداءُ ترحيلاً سابقاً؟</param>
internal sealed record ProjectsDocumentDto(
    string Id,
    string Number,
    string State,
    string Amount,
    string? EntryId,
    bool AlreadyPosted);

/// <summary>طلب تسجيل خطاب ضمان.</summary>
internal sealed record GuaranteeRequestDto
{
    /// <summary>رقم الخطاب.</summary>
    public required string Number { get; init; }

    /// <summary>صنفه: ابتدائي · حسن تنفيذ · دفعة مقدمة.</summary>
    public required string Kind { get; init; }

    /// <summary>عقد العميل الذي يخصّه، أو <c>null</c>.</summary>
    public required string? ContractId { get; init; }

    /// <summary>عقد الباطن الذي يخصّه، أو <c>null</c>.</summary>
    public required string? SubcontractId { get; init; }

    /// <summary>اسم الجهة المُصدِرة بالعربية.</summary>
    public required string IssuerNameAr { get; init; }

    /// <summary>المبلغ نصّاً.</summary>
    public required WireDecimal Amount { get; init; }

    /// <summary>بدء السريان بصيغة yyyy-MM-dd.</summary>
    public required string EffectiveFrom { get; init; }

    /// <summary>الانتهاء بصيغة yyyy-MM-dd.</summary>
    public required string ExpiresOn { get; init; }

    /// <summary>
    /// معرّف المرفق على السطح المنشور للمرفقات — <b>لا بايتات هنا</b>: خطاب الضمان
    /// سندُ إثبات يُودَع حيث تُحرَس البصمة والإصدار والسحب (ADR-0046 · ADR-0051).
    /// </summary>
    public required string AttachmentId { get; init; }
}

/// <summary>
/// خطاب ضمان كما يخرج على السلك — <b>بلا حقل قيد</b>، لأنه لا يُرحَّل أبداً.
/// </summary>
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
internal sealed record GuaranteeDto(
    string Id,
    string Number,
    string Kind,
    string? ContractId,
    string? SubcontractId,
    string IssuerNameAr,
    string Amount,
    string EffectiveFrom,
    string ExpiresOn,
    string AttachmentId);

/// <summary>صفٌّ في سجلّ المحتجزات.</summary>
/// <param name="MovementId">معرّف الحركة — وهو ما يُفرَج عنه أو يُحصَّل.</param>
/// <param name="Side">الجانب: <c>RECEIVABLE</c> لدى العميل · <c>PAYABLE</c> على المقاول.</param>
/// <param name="PartyKind">نوع الدفتر المساعد للطرف.</param>
/// <param name="PartyId">الطرف.</param>
/// <param name="ProjectCode">رمز المشروع.</param>
/// <param name="DocumentType">المستند الذي أنشأ الحركة.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="Amount">المبلغ الأصلي.</param>
/// <param name="Outstanding">الرصيد القائم بعد ما أُفرِج عنه أو حُصِّل.</param>
/// <param name="MovedOn">تاريخ الحركة.</param>
/// <param name="DueOn">تاريخ استحقاق الإفراج.</param>
internal sealed record RetentionRegisterRowDto(
    string MovementId,
    string Side,
    string PartyKind,
    string PartyId,
    string ProjectCode,
    string DocumentType,
    string DocumentId,
    string Amount,
    string Outstanding,
    string MovedOn,
    string DueOn);

/// <summary>سجلّ المحتجزات مدينةً ودائنة — مشتقٌّ من المُرحَّل وحده.</summary>
/// <param name="AsOf">تاريخ القراءة.</param>
/// <param name="Rows">الصفوف.</param>
/// <param name="ReceivableTotal">مجموع المحتجز المدين القائم.</param>
/// <param name="PayableTotal">مجموع المحتجز الدائن القائم.</param>
internal sealed record RetentionRegisterDto(
    string AsOf,
    IReadOnlyList<RetentionRegisterRowDto> Rows,
    string ReceivableTotal,
    string PayableTotal);

/// <summary>صفٌّ في كشف المقاولين.</summary>
/// <param name="SubcontractorId">المقاول.</param>
/// <param name="Code">رمزه.</param>
/// <param name="NameAr">اسمه العربي.</param>
/// <param name="NameTranslations">ترجمات اسمه.</param>
/// <param name="Effect">أثره على الحساب الضابط بمنطق «مدين ناقص دائن».</param>
internal sealed record SubcontractorStatementRowDto(
    string SubcontractorId,
    string Code,
    string NameAr,
    IReadOnlyList<NameValueDto> NameTranslations,
    string Effect);

/// <summary>كشف المقاولين ومطابقته بنقطة ضبطه.</summary>
/// <param name="AsOf">التاريخ.</param>
/// <param name="Rows">الأطراف بآثارها.</param>
/// <param name="SubledgerTotal">مجموع الدفتر المساعد المحسوب من مستنداته.</param>
/// <param name="ControlTotal">رصيد نقطة الضبط في دفتر الأستاذ.</param>
/// <param name="Divergence">الفارق: الدفتر المساعد ناقص نقطة الضبط.</param>
/// <param name="IsReconciled">هل الفارق صفر بالضبط؟ لا «قريب من الصفر».</param>
internal sealed record SubcontractorStatementDto(
    string AsOf,
    IReadOnlyList<SubcontractorStatementRowDto> Rows,
    string SubledgerTotal,
    string ControlTotal,
    string Divergence,
    bool IsReconciled);

/// <summary>موقف العقد — مشتقّاً من المُرحَّل وحده.</summary>
/// <param name="ContractId">العقد.</param>
/// <param name="ContractNumber">رقمه.</param>
/// <param name="PostedCertificateCount">عدد مستخلصاته المُرحَّلة.</param>
/// <param name="RetentionOutstanding">المحتجز القائم.</param>
/// <param name="AdvanceOutstanding">الدفعة المقدمة غير المستنفَدة.</param>
/// <param name="PendingPolicy">البنود المعلَّقة.</param>
internal sealed record ContractPositionDto(
    string ContractId,
    string ContractNumber,
    int PostedCertificateCount,
    string RetentionOutstanding,
    string AdvanceOutstanding,
    IReadOnlyList<ProjectsPendingItemDto> PendingPolicy);
