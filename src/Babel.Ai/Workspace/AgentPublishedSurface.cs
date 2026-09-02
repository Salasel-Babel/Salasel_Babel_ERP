using Babel.Ai.Agent;
using Babel.SharedKernel;

namespace Babel.Ai.Workspace;

/// <summary>
/// نداءٌ واحد على <b>السطح المنشور نفسه الذي يستعمله المتصفّح</b> — بمعرّف عمليته
/// وقالبِ مساره وفعلِه ومسارِه المُركَّب وجسمِه، وبالمتكلّم الذي يُنسب إليه.
/// </summary>
/// <param name="OperationId">معرّف العملية المنشورة — يُقال في الرفض حين يسقط شيء.</param>
/// <param name="Method">الفعل الشبكي كما ينشره العقد.</param>
/// <param name="Template">
/// قالب المسار كما هو في العقد وفي جدول المسارات — <b>وهو مفتاح إيجاد الباب</b>، لا
/// المسارُ المُركَّب: جدول المسارات مفهرسٌ بالقالب.
/// </param>
/// <param name="Path">المسار <b>بعد ملء وسائطه</b> — لا قالباً فيه أقواس.</param>
/// <param name="Body">جسم الطلب كما اجتاز البوّابة.</param>
/// <param name="Caller">
/// المتكلّم ونطاقه. <b>وهو إنسانُ الجلسة لا الوكيل</b>: المسوّدة تُنسب إليه، وصلاحياتُه
/// هي التي تُفحص عند الباب — لا صلاحيةٌ ثانية تُخترع لمسار الوكيل.
/// </param>
public sealed record AgentSurfaceCall(
    string OperationId,
    string Method,
    string Template,
    string Path,
    string Body,
    AgentCaller Caller);

/// <summary>
/// جواب السطح كما عاد: رمزُ حالته وجسمُه. <b>ولا يُترجَم هنا</b> — من يقرؤه هو الذي
/// يعرف كيف يُقرأ <c>application/problem+json</c>.
/// </summary>
/// <param name="Status">رمز الحالة.</param>
/// <param name="Body">جسم الجواب نصّاً.</param>
public sealed record AgentSurfaceAnswer(int Status, string Body);

/// <summary>
/// <b>منفذُ السطح المنشور — الطريق الوحيد الذي يبلغ به مسارُ الوكيل وحدةً مالكة.</b>
/// <para>
/// <b>ولماذا منفذ لا نداءٌ مباشر:</b> القاعدة 3 تمنع <c>Babel.Ai</c> من الإشارة إلى
/// <c>Babel.Api</c> وإلى أي وحدة أفقية. فلو نادى هذا المسار خدمةَ وحدةٍ بنفسه لكان
/// <b>مساراً جانبياً</b> بمعنيين: مساراً لا يمرّ بالباب الذي يمرّ به المتصفّح، ومساراً
/// يكسر اتجاه الاعتماد. والمنفذ يجعل التنفيذ في الجذر التركيبي — حيث يعيش جدولُ
/// المسارات المنشور نفسه — ويبقى القرار هنا.
/// </para>
/// <para>
/// <b>ورفضُه مُسمّى لا استثناء:</b> بابٌ غير مسجَّل على هذا الخادم، أو جلسةٌ بلا إنسانٍ
/// تُنسب إليه، حالتان يفصل بينهما القارئ بفعلٍ مختلف — فتعودان <c>Result</c> ساقطاً
/// بأسمائهما، لا استثناءً يُجمَع في «انقطع النداء».
/// </para>
/// <para>
/// <b>وما لا يستطيع هذا المنفذ أن يفعله:</b> لا يبلغ مورداً لا ينشره العقد، ولا يُنشئ
/// نداءً بفعلٍ ليس <c>draft</c> — <see cref="AgentDraftConfirmationGate.Refuse"/> يقف
/// قبله، و<c>TheAgentSurfaceEndsAtTheDraft</c> يقيس ذلك على العقد كلّه.
/// </para>
/// </summary>
public interface IAgentPublishedSurface
{
    /// <summary>ينادي الباب المنشور ويعيد جوابه كما هو.</summary>
    /// <param name="call">النداء.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    Task<Result<AgentSurfaceAnswer>> CallAsync(AgentSurfaceCall call, CancellationToken cancellationToken);
}
