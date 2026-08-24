using System.Text.Json.Serialization;

namespace Babel.Api.Wire;

/// <summary>اسم ثنائي اللغة على السلك. الطرفان إلزاميان — العربية ليست ترجمة ثانية.</summary>
/// <param name="Ar">النصّ العربي.</param>
/// <param name="En">النصّ الإنجليزي.</param>
internal sealed record LocalizedTextDto(string Ar, string En);

/// <summary>المستند المصدر على السلك.</summary>
/// <param name="Module">اسم الوحدة المالكة، بالضبط كما في <c>BabelModule</c>.</param>
/// <param name="DocumentType">نوع المستند داخل تلك الوحدة.</param>
/// <param name="DocumentId">معرّف المستند داخل تلك الوحدة.</param>
internal sealed record SourceDocumentDto(string Module, string DocumentType, string DocumentId);

/// <summary>النطاق التحليلي على السلك.</summary>
/// <param name="BranchId">الفرع.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="ProjectId">المشروع.</param>
internal sealed record ScopeDto(string? BranchId = null, string? CostCenterId = null, string? ProjectId = null);

/// <summary>إشارة إلى طرف في دفتر مساعد.</summary>
/// <param name="Kind">نوع الدفتر المساعد.</param>
/// <param name="PartyId">معرّف الطرف.</param>
internal sealed record SubledgerDto(string Kind, string PartyId);

/// <summary>ثنائية اسم وقيمة — للأبعاد والوقائع.</summary>
/// <param name="Name">الاسم.</param>
/// <param name="Value">القيمة.</param>
internal sealed record NameValueDto(string Name, string Value);

/// <summary>مبلغ مُسمّى في مفردات الحدث.</summary>
/// <param name="Name">اسم المبلغ كما تعرّفه المصفوفة.</param>
/// <param name="Value">القيمة نصّاً.</param>
internal sealed record NamedAmountDto(string Name, WireDecimal Value);

/// <summary>
/// سطر ترحيل على السلك.
/// <para>
/// <b>ولاحظ ما ليس هنا:</b> لا حساب ولا رقم حساب — القاعدة 2 مطبَّقة على السلك أيضاً.
/// السطر يحمل <b>دوراً</b>، والدور يُحلّ إلى حساب داخل الدفتر وحده.
/// </para>
/// </summary>
/// <param name="Role">اسم الدور، بالضبط كما في <c>PostingRole</c> وبحساسية حالة الأحرف.</param>
/// <param name="Side">الجانب: <c>Debit</c> أو <c>Credit</c>.</param>
/// <param name="Amount">المبلغ نصّاً بمقياس لا يتجاوز أربعاً.</param>
/// <param name="Scope">النطاق التحليلي.</param>
/// <param name="Subledger">الطرف في الدفتر المساعد.</param>
/// <param name="Narration">بيان السطر.</param>
/// <param name="Qualifier">مؤهّل الدور.</param>
/// <param name="Dimensions">أبعاد السطر.</param>
internal sealed record PostingLineDto(
    string Role,
    string Side,
    WireDecimal Amount,
    ScopeDto? Scope = null,
    SubledgerDto? Subledger = null,
    LocalizedTextDto? Narration = null,
    string? Qualifier = null,
    IReadOnlyList<NameValueDto>? Dimensions = null);

/// <summary>إذن استثنائي بالترحيل في فترة مقفلة.</summary>
/// <param name="PermissionCode">رمز الصلاحية الاستثنائية.</param>
/// <param name="AuthorisedBy">معرّف المُصرِّح.</param>
/// <param name="Reason">السبب ثنائي اللغة.</param>
internal sealed record ClosedPeriodAuthorisationDto(string PermissionCode, string AuthorisedBy, LocalizedTextDto Reason);

/// <summary>
/// طلب ترحيل قيد.
/// <para>
/// <b>ولاحظ ما ليس هنا أيضاً:</b> لا حقل مستأجر ولا حقل شركة. النطاق يأتي من الاعتماد
/// ومن المسار، لا من الجسم — وحقلٌ في الجسم اسمه <c>tenantId</c> كان سيصير أول ثغرة
/// عبور بين المستأجرين. وأي حقل غير معروف في الجسم يُرفض الطلب كلّه بسببه
/// (‏<c>JsonUnmappedMemberHandling.Disallow</c>).
/// </para>
/// </summary>
internal sealed record PostJournalEntryRequestDto
{
    /// <summary>مفتاح الحصانة ضد التكرار — يوفّره العميل، ومستقل عن الترتيب.</summary>
    public required string IdempotencyKey { get; init; }

    /// <summary>المستند المصدر.</summary>
    public required SourceDocumentDto Source { get; init; }

    /// <summary>الحدث الذي أطلق الترحيل: <c>OnApproval</c> · <c>OnReceipt</c> · <c>OnSettlement</c> · <c>Periodic</c> · <c>Reversal</c>.</summary>
    public required string Trigger { get; init; }

    /// <summary>تاريخ المستند الميلادي بصيغة <c>yyyy-MM-dd</c> حصراً.</summary>
    public required string DocumentDate { get; init; }

    /// <summary>بيان القيد ثنائي اللغة.</summary>
    public required LocalizedTextDto Narration { get; init; }

    /// <summary>
    /// سطور الطلب — تُرسَل في المسار الصريح وتُترك فارغة في مسار القالب، وهي وحدها ما
    /// يختار المسار. و<see cref="Event"/> إلزامي في الحالتين.
    /// </summary>
    public IReadOnlyList<PostingLineDto> Lines { get; init; } = [];

    /// <summary>
    /// رمز الحدث في مصفوفة الترحيل — <b>حقل إلزامي في العقد المنشور</b>، لأنه جزء من هوية
    /// الترحيل لا وسيلة اختيار قالب فقط. والنوع يبقى <c>string?</c> لأن السطح <b>ينقل</b>
    /// ما وصل ولا يقرّر: الغياب يمرّ إلى المحرك فيرفضه برسالة تشرح السبب.
    /// </summary>
    public string? Event { get; init; }

    /// <summary>مفردات المبالغ التي يقرؤها قالب الحدث.</summary>
    public IReadOnlyList<NamedAmountDto> Amounts { get; init; } = [];

    /// <summary>وقائع السياق التي تُقيَّم عليها الشروط وقواعد الحجب.</summary>
    public IReadOnlyList<NameValueDto> Facts { get; init; } = [];

    /// <summary>الأبعاد التحليلية على مستوى الطلب.</summary>
    public IReadOnlyList<NameValueDto> Dimensions { get; init; } = [];

    /// <summary>الدفتر داخل الشركة.</summary>
    public string Book { get; init; } = "MAIN";

    /// <summary>عملة القيد — ثلاثة محارف لاتينية كبيرة.</summary>
    public string Currency { get; init; } = "SAR";

    /// <summary>سعر الصرف إلى عملة الشركة، نصّاً بمقياس لا يتجاوز ثمانياً.</summary>
    public WireDecimal ExchangeRate { get; init; } = new("1");

    /// <summary>جيل الترحيل. يبدأ من 1 ولا يزيد إلا بعد عكس مشروع.</summary>
    public int Generation { get; init; } = 1;

    /// <summary>إذن استثنائي بالترحيل في فترة مقفلة.</summary>
    public ClosedPeriodAuthorisationDto? ClosedPeriodAuthorisation { get; init; }
}

/// <summary>
/// طلب عكس قيد. <b>ولا يوجد على هذا السطح فعلٌ اسمه حذف</b> — لا هنا ولا في أي مسار
/// آخر: القيد المُرحَّل حقيقة نهائية، والتصحيح قيدٌ جديد مرتبط به (ADR-0002).
/// </summary>
internal sealed record ReverseJournalEntryRequestDto
{
    /// <summary>سبب العكس ثنائي اللغة. إلزامي: عكسٌ بلا سبب لا يُقرأ في تدقيق.</summary>
    public required LocalizedTextDto Reason { get; init; }

    /// <summary>تاريخ قيد العكس بصيغة <c>yyyy-MM-dd</c>، أو غيابه فيُتخذ تاريخ القيد الأصلي.</summary>
    public string? ReversalDate { get; init; }

    /// <summary>إذن استثنائي إن وقع تاريخ العكس في فترة مقفلة.</summary>
    public ClosedPeriodAuthorisationDto? ClosedPeriodAuthorisation { get; init; }
}

/// <summary>إيصال الترحيل على السلك.</summary>
/// <param name="EntryId">معرّف القيد.</param>
/// <param name="EntryNumber">رقم القيد بلا فجوات — نصّاً لا رقماً.</param>
/// <param name="EntryHash">بصمة القيد في السلسلة، hex صغير.</param>
/// <param name="AlreadyPosted">هل كان مفتاح الحصانة مُرحَّلاً من قبل؟</param>
/// <param name="ChainSequence">موقع القيد في سلسلة نطاقه — نصّاً.</param>
/// <param name="PeriodCode">الفترة المالية بصيغة <c>yyyy-MM</c> ميلادية دائماً.</param>
/// <param name="Generation">جيل الترحيل.</param>
/// <param name="LineCount">عدد السطور الناتجة.</param>
internal sealed record PostingReceiptDto(
    string EntryId,
    string EntryNumber,
    string EntryHash,
    bool AlreadyPosted,
    string ChainSequence,
    string PeriodCode,
    int Generation,
    int LineCount);

/// <summary>صفّ في ميزان المراجعة.</summary>
/// <param name="AccountCode">رمز الحساب.</param>
/// <param name="NameAr">الاسم العربي.</param>
/// <param name="NameEn">الاسم الإنجليزي.</param>
/// <param name="Debit">مجموع المدين بعملة الشركة، نصّاً.</param>
/// <param name="Credit">مجموع الدائن بعملة الشركة، نصّاً.</param>
internal sealed record TrialBalanceRowDto(
    string AccountCode,
    string NameAr,
    string NameEn,
    WireDecimal Debit,
    WireDecimal Credit);

/// <summary>
/// ميزان المراجعة كاملاً.
/// <para>
/// <b>ولاحظ ما ليس هنا: المجموعان.</b> جمع عمود مالي حسابٌ على المال، والجذر التركيبي
/// لا يحسب مالاً (القاعدة 13، البند «أ» — مفروض على IL لا على المراجعة). ومكان المجموع
/// الصحيح هو <c>sum()</c> على <c>numeric</c> داخل PostgreSQL حيث الجمع مضبوط بلا فاصلة
/// عائمة أصلاً؛ وسطح الدفتر لا يكشفه اليوم، وهو تغيير مطلوب مسجَّل في ADR-0018.
/// </para>
/// <para>
/// ولا يُترك الجمع لواجهة المتصفّح: <c>Number</c> في JavaScript فاصلة عائمة ثنائية،
/// وجمع عمود مالي فيها هو الفخّ نفسه منقولاً إلى العميل.
/// </para>
/// </summary>
/// <param name="Book">الدفتر.</param>
/// <param name="PeriodCode">الفترة، أو غيابها فكل الفترات.</param>
/// <param name="RowCount">عدد الصفوف.</param>
/// <param name="Rows">الصفوف.</param>
internal sealed record TrialBalanceDto(
    string Book,
    string? PeriodCode,
    int RowCount,
    IReadOnlyList<TrialBalanceRowDto> Rows);

/// <summary>حكم إعادة التحقق من سلسلة نطاق واحد.</summary>
/// <param name="Ok">هل النطاق سليم كاملاً؟</param>
/// <param name="Checked">عدد السجلات المفحوصة.</param>
/// <param name="FirstDivergentSequence">أول رقم تسلسل منحرف — نصّاً — أو غيابه.</param>
/// <param name="Verdict">رمز الحكم الثابت.</param>
/// <param name="ReasonAr">شرح عربي صالح لتقرير تدقيق.</param>
/// <param name="Detail">تفاصيل فنّية: البصمات المتوقّعة والمخزَّنة.</param>
internal sealed record ChainVerificationDto(
    bool Ok,
    int Checked,
    string? FirstDivergentSequence,
    string Verdict,
    string ReasonAr,
    string? Detail);

/// <summary>سطر قيد كما يُقرأ.</summary>
/// <param name="LineNo">رقم السطر.</param>
/// <param name="Role">رمز الدور.</param>
/// <param name="Qualifier">مؤهّل الدور.</param>
/// <param name="Debit">المدين بعملة الحركة.</param>
/// <param name="Credit">الدائن بعملة الحركة.</param>
/// <param name="Currency">عملة الحركة.</param>
/// <param name="DescriptionAr">بيان السطر بالعربية.</param>
/// <param name="DescriptionEn">بيان السطر بالإنجليزية.</param>
internal sealed record JournalLineDto(
    int LineNo,
    string Role,
    string Qualifier,
    WireDecimal Debit,
    WireDecimal Credit,
    string Currency,
    string DescriptionAr,
    string DescriptionEn);

/// <summary>قيد كما يُقرأ، بسطوره.</summary>
/// <param name="EntryId">معرّف القيد.</param>
/// <param name="EntryNumber">رقم القيد نصّاً.</param>
/// <param name="Book">الدفتر.</param>
/// <param name="EntryDate">تاريخ القيد <c>yyyy-MM-dd</c>.</param>
/// <param name="PeriodCode">الفترة <c>yyyy-MM</c>.</param>
/// <param name="Status">حالة القيد.</param>
/// <param name="Currency">عملة القيد.</param>
/// <param name="MemoAr">البيان بالعربية.</param>
/// <param name="MemoEn">البيان بالإنجليزية.</param>
/// <param name="EntryHash">بصمة القيد.</param>
/// <param name="ChainSequence">موقعه في السلسلة نصّاً.</param>
/// <param name="ReversesEntryId">القيد الذي يعكسه، إن كان قيد عكس.</param>
/// <param name="Lines">السطور.</param>
internal sealed record JournalEntryDto(
    string EntryId,
    string EntryNumber,
    string Book,
    string EntryDate,
    string PeriodCode,
    string Status,
    string Currency,
    string MemoAr,
    string MemoEn,
    string EntryHash,
    string ChainSequence,
    string? ReversesEntryId,
    IReadOnlyList<JournalLineDto> Lines);

/// <summary>حالة الخدمة — وثقافتها وتقويمها معها.</summary>
/// <param name="Status">الحالة.</param>
/// <param name="Culture">ثقافة العملية الفعلية.</param>
/// <param name="Calendar">تقويم تلك الثقافة الافتراضي.</param>
/// <param name="ApiVersion">إصدار سطح HTTP.</param>
/// <remarks>
/// الثقافة والتقويم معلنان عمداً: خادم عربي يعمل بثقافة <c>ar-SA</c> تقويمه الافتراضي
/// أم القرى، وأي تنسيق تاريخ ضمني عليه يُنتج <c>1448-03</c> بدل <c>2026-08</c> فيفسد رمز
/// الفترة المالية (‏فخ-38). إعلانها في نقطة الصحّة يجعل السؤال «بأي ثقافة يعمل هذا الخادم؟»
/// قابلاً للإجابة من الخارج بدل أن يُخمَّن.
/// </remarks>
internal sealed record HealthDto(string Status, string Culture, string Calendar, string ApiVersion);

/// <summary>خطأ مفرد داخل تفاصيل المشكلة.</summary>
/// <param name="Code">الرمز الثابت — نقطة الاعتماد البرمجية الوحيدة.</param>
/// <param name="MessageAr">الرسالة العربية.</param>
/// <param name="MessageEn">الرسالة الإنجليزية.</param>
/// <param name="Field">الحقل المعنيّ على السلك، إن وُجد.</param>
internal sealed record ApiErrorDto(string Code, string MessageAr, string MessageEn, string? Field = null);

/// <summary>
/// تفاصيل المشكلة بصيغة <c>RFC 9457</c>، بامتدادين: رمز ثابت، ورسالة عربية إلى جانب
/// الإنجليزية.
/// </summary>
/// <param name="Type">المرجع الذي يُعرّف نوع المشكلة.</param>
/// <param name="Title">عنوان قصير بالإنجليزية.</param>
/// <param name="TitleAr">عنوان قصير بالعربية.</param>
/// <param name="Status">رمز حالة HTTP.</param>
/// <param name="Detail">شرح بالإنجليزية.</param>
/// <param name="DetailAr">شرح بالعربية.</param>
/// <param name="Instance">مسار الطلب.</param>
/// <param name="Code">الرمز الثابت الأول — نقطة الاعتماد البرمجية.</param>
/// <param name="TraceId">معرّف التتبّع، للربط مع سجلّ الخادم.</param>
/// <param name="Errors">كل الأخطاء، لا أوّلها فقط.</param>
internal sealed record ProblemDto(
    [property: JsonPropertyName("type")] string Type,
    [property: JsonPropertyName("title")] string Title,
    [property: JsonPropertyName("titleAr")] string TitleAr,
    [property: JsonPropertyName("status")] int Status,
    [property: JsonPropertyName("detail")] string Detail,
    [property: JsonPropertyName("detailAr")] string DetailAr,
    [property: JsonPropertyName("instance")] string Instance,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("traceId")] string TraceId,
    [property: JsonPropertyName("errors")] IReadOnlyList<ApiErrorDto> Errors);
