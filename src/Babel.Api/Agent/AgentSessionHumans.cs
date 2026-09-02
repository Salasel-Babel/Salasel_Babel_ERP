using System.Collections.Concurrent;
using Babel.Ai.Workspace;
using Babel.Api.Security;

namespace Babel.Api.Agent;

/// <summary>
/// <b>مَن الإنسان الذي يجري هذا الدور باسمه — محفوظاً للحظة التي يمضي فيها الدور خلف الطلب.</b>
/// <para>
/// ودورُ الوكيل يجري في مهمّةٍ خلفية بعد أن يعود الطلب (‏<c>AgentWorkspaceService.Send</c>)،
/// فلا <c>HttpContext</c> ولا اعتماد في اللحظة التي تُنشأ فيها المسوّدة. <b>والمسوّدة
/// تُنسب إلى إنسان</b>، فلا بدّ أن تُحفظ هويّته المحلولة من اعتماده في الطلب الذي بدأ
/// الدور — <b>لا أن تُبنى من جديد</b>: بناءُ هويّةٍ من معرّفَي مستخدمٍ وشركة يُسقط
/// «دورك في هذه المنشأة قراءةٌ فقط»، فيصير مسار الوكيل باباً أوسع من الباب الذي يفتحه
/// المتصفّح لصاحبه نفسه. وذلك تصعيدُ صلاحية لا تفصيلَ تنفيذ.
/// </para>
/// <para>
/// <b>وما يُحفظ ليس اعتماداً:</b> لا رمز ولا مفتاح ولا شيء يُقدَّم إلى بابٍ ليُصادَق به —
/// هي <see cref="ApiPrincipal"/> بعد الحلّ: منشأةٌ ومستخدمٌ ومجموعةُ شركاتٍ يبلغها.
/// وتُكتب في كل رسالة، فما يُقرأ عند الإنشاء هو حالُ الصلاحيات لحظةَ طلبَ الإنسانُ لا قبلها.
/// </para>
/// <para>
/// <b>وتُنسى بانقضاء الجلسة:</b> كل كتابةٍ تكنس ما تجاوز عمرَ الجلسة الخاملة، فلا ينمو
/// الجدول بعدد الجلسات التي مرّت على الخادم منذ إقلاعه.
/// </para>
/// </summary>
internal sealed class AgentSessionHumans
{
    private readonly ConcurrentDictionary<Guid, (ApiPrincipal Principal, DateTimeOffset At)> _held = new();
    private readonly TimeProvider _clock;
    private readonly TimeSpan _life;

    /// <summary>يركّب الجدول بعمر الجلسة الخاملة نفسه.</summary>
    /// <param name="clock">مصدر الوقت.</param>
    /// <param name="options">إعدادات المساحة.</param>
    public AgentSessionHumans(TimeProvider clock, AgentWorkspaceOptions options)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(options);

        _clock = clock;
        _life = options.IdleSessionLife;
    }

    /// <summary>يقيّد هويّة صاحب هذه الجلسة كما حُلّت من اعتماده الآن.</summary>
    /// <param name="sessionId">جلسة مساحة العمل.</param>
    /// <param name="principal">الهوية المحلولة.</param>
    public void Hold(Guid sessionId, ApiPrincipal principal)
    {
        ArgumentNullException.ThrowIfNull(principal);

        Sweep();
        _held[sessionId] = (principal, _clock.GetUtcNow());
    }

    /// <summary>يقرأ هويّة صاحب هذه الجلسة، أو <c>null</c> إن لم تُحفظ أو انقضت.</summary>
    /// <param name="sessionId">جلسة مساحة العمل.</param>
    public ApiPrincipal? Of(Guid sessionId) =>
        _held.TryGetValue(sessionId, out (ApiPrincipal Principal, DateTimeOffset At) found)
        && !Expired(found.At)
            ? found.Principal
            : null;

    private bool Expired(DateTimeOffset at) => _clock.GetUtcNow() - at >= _life;

    private void Sweep()
    {
        foreach (KeyValuePair<Guid, (ApiPrincipal Principal, DateTimeOffset At)> entry in _held)
        {
            if (Expired(entry.Value.At))
            {
                _held.TryRemove(entry.Key, out _);
            }
        }
    }
}
