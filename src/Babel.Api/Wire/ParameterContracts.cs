namespace Babel.Api.Wire;

/// <summary>قيمةٌ في إيداع: مفتاحها وقيمتها <b>نصّاً</b>.</summary>
/// <param name="Key">المفتاح — من مفاتيح المجموعة المعلَنة.</param>
/// <param name="Value">القيمة نصّاً. والنسبة كسرٌ عشري لا مئوية: 0.15 لا 15.</param>
internal sealed record ParameterValueRequestDto(string Key, WireDecimal Value);

/// <summary>
/// طلب إيداع إصدارٍ من مجموعة معامِلات — <b>المجموعة كاملةً لا بعضها</b>.
/// </summary>
internal sealed record ParameterVersionRequestDto
{
    /// <summary>رمز المجموعة.</summary>
    public required string SetCode { get; init; }

    /// <summary>تاريخ السريان.</summary>
    public required string EffectiveFrom { get; init; }

    /// <summary>حالة الاعتماد: <c>tenant_approved</c> أو <c>auditor_signed</c>.</summary>
    public required string Approval { get; init; }

    /// <summary>من اعتمد — <b>إنسان لا نظام</b>.</summary>
    public required string ApprovedBy { get; init; }

    /// <summary>تاريخ الاعتماد.</summary>
    public required string ApprovedOn { get; init; }

    /// <summary>مرجع المصدر — غير فارغ بقيدٍ في القاعدة.</summary>
    public required string SourceRef { get; init; }

    /// <summary>القيم — كلُّ مفاتيح المجموعة.</summary>
    public required IReadOnlyList<ParameterValueRequestDto> Values { get; init; }
}

/// <summary>قيمةٌ كما تخرج على السلك.</summary>
/// <param name="Key">المفتاح.</param>
/// <param name="Kind">‏<c>rate</c> · <c>money</c> · <c>count</c>.</param>
/// <param name="Value">القيمة نصّاً.</param>
internal sealed record ParameterValueDto(string Key, string Kind, string Value);

/// <summary>إصدارٌ كما يخرج على السلك.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="SetCode">المجموعة.</param>
/// <param name="Scope">‏<c>platform</c> · <c>tenant</c>.</param>
/// <param name="EffectiveFrom">السريان.</param>
/// <param name="Approval">حالة الاعتماد.</param>
/// <param name="ApprovedBy">المعتمِد — فارغٌ لافتراض المنصّة وحده.</param>
/// <param name="ApprovedOn">تاريخ الاعتماد، أو فراغ لافتراض المنصّة.</param>
/// <param name="SourceRef">مرجع المصدر.</param>
/// <param name="Values">القيم.</param>
internal sealed record ParameterVersionDto(
    string Id,
    string SetCode,
    string Scope,
    string EffectiveFrom,
    string Approval,
    string ApprovedBy,
    string ApprovedOn,
    string SourceRef,
    IReadOnlyList<ParameterValueDto> Values);

/// <summary>الإصدارات التي تراها المنشأة.</summary>
/// <param name="ItemCount">العدد.</param>
/// <param name="Items">الإصدارات.</param>
internal sealed record ParameterVersionListDto(int ItemCount, IReadOnlyList<ParameterVersionDto> Items);

/// <summary>مستندٌ مُرحَّل استعمل إصداراً.</summary>
/// <param name="Module">الوحدة المالكة.</param>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="DocumentId">معرّفه.</param>
/// <param name="PostedOn">تاريخ الترحيل.</param>
internal sealed record ParameterUsageDto(string Module, string DocumentType, string DocumentId, string PostedOn);

/// <summary>صفٌّ في قائمة مراجعة المحاسب.</summary>
/// <param name="Version">الإصدار غير الموقَّع.</param>
/// <param name="UsageCount">عدد المستندات التي استعملته.</param>
/// <param name="Usages">المستندات.</param>
internal sealed record ParameterReviewEntryDto(
    ParameterVersionDto Version, int UsageCount, IReadOnlyList<ParameterUsageDto> Usages);

/// <summary>قائمة مراجعة المحاسب القانوني.</summary>
/// <param name="ItemCount">عدد الإصدارات غير الموقَّعة.</param>
/// <param name="Items">الصفوف.</param>
internal sealed record ParameterReviewListDto(int ItemCount, IReadOnlyList<ParameterReviewEntryDto> Items);
