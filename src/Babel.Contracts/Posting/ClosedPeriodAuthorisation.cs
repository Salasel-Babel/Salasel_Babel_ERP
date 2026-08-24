using Babel.SharedKernel;

namespace Babel.Contracts.Posting;

/// <summary>
/// إذن استثنائي بالترحيل في فترة مالية <b>مقفلة</b>.
/// <para>
/// الترحيل في فترة مقفلة مرفوض افتراضاً. الاستثناء ليس علماً منطقياً (‏<c>bool force</c>)
/// بل <b>إذن موثَّق</b>: من أذن، وبأي صلاحية، ولأي سبب. ويُكتب في سجل التدقيق قبل
/// أن يُكتب القيد — إذنٌ لا أثر له في السجل ليس إذناً، بل ثغرة.
/// </para>
/// <para>
/// وفترة مقفلة نهائياً (‏<c>permanently_closed</c>) لا يفتحها هذا الإذن ولا غيره.
/// </para>
/// </summary>
/// <param name="PermissionCode">رمز الصلاحية الاستثنائية الممنوحة.</param>
/// <param name="AuthorisedBy">من أذن — مستخدم حقيقي، لا فاعل نظام.</param>
/// <param name="Reason">سبب الاستثناء ثنائي اللغة، يُحفظ مع القيد وفي سجل التدقيق.</param>
public sealed record ClosedPeriodAuthorisation(string PermissionCode, UserId AuthorisedBy, LocalizedName Reason);
