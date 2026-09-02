namespace Babel.Ai.Voice;

/// <summary>
/// مَن يتكلّم وفي أي منشأة وبأي صلاحيات. <b>ولا افتراض في أيٍّ من الثلاثة.</b>
/// <para>
/// <b>ولم يعد فيه اسمُ الشركة.</b> كان يُحمَل كي يُقارَن بتساوي نصّين باسمٍ يُحلَّل من
/// الكلام — <b>حكمٌ على الهوية بالتخمين</b>، وهو الشكل نفسه الذي حُذف من قراءة الأطراف.
/// والمنشأةُ في <see cref="CompanyId"/> ولا تُنطق في مسوّدة قطّ.
/// </para>
/// </summary>
/// <param name="CompanyId">المنشأة المفتوحة.</param>
/// <param name="PermittedIntentIds">
/// النيّات المسموح بها لهذا المتكلّم. <b>مجموعة مغلقة</b>: المسار المنطوق مدخلٌ آخر
/// إلى الصلاحيات نفسها، لا باب أوسع منها.
/// </param>
public sealed record VoiceCaller(
    Guid CompanyId,
    IReadOnlySet<string> PermittedIntentIds);
