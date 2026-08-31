namespace Babel.Ai.Voice;

/// <summary>
/// مَن يتكلّم وفي أي منشأة وبأي صلاحيات. <b>ولا افتراض في أيٍّ من الثلاثة.</b>
/// </summary>
/// <param name="CompanyId">المنشأة المفتوحة.</param>
/// <param name="CompanyNameAr">اسمها كما يقوله المستخدم — تُقارَن به الشركة المنطوقة.</param>
/// <param name="PermittedIntentIds">
/// النيّات المسموح بها لهذا المتكلّم. <b>مجموعة مغلقة</b>: المسار المنطوق مدخلٌ آخر
/// إلى الصلاحيات نفسها، لا باب أوسع منها.
/// </param>
public sealed record VoiceCaller(
    Guid CompanyId,
    string CompanyNameAr,
    IReadOnlySet<string> PermittedIntentIds);
