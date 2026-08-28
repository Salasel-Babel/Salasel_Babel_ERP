using Babel.Core.Access;

namespace Babel.Api.Security;

/// <summary>
/// دليل الاعتمادات كاملاً: <b>ما أصدره سطح الجلسات، ثم اعتماد التزويد المُهيَّأ من الإعداد</b>.
/// <para>
/// <b>ولماذا دليل واحد لا آليتان:</b> آليتا تصريح متوازيتان تعنيان أن إحداهما تُصان
/// وتُنسى الأخرى، ولا يظهر الفارق إلا يوم يتجاوزه أحد. فالوسيط ينادي
/// <see cref="IApiPrincipalResolver"/> وحده كما كان، وهذا النوع هو التنفيذ المسجَّل —
/// لا فرعٌ في الوسيط ولا نداءٌ ثانٍ منه.
/// </para>
/// <para>
/// <b>والترتيب مقصود:</b> الجلسات المُصدَرة أولاً لأنها الحالة الغالبة، ثم دليل الإعداد.
/// ودليل الإعداد <b>باب إقلاع معلَن</b>: هو الاعتماد الوحيد الذي لا يُصدره هذا السطح
/// ولا يدور ولا يُبطَل من HTTP، ووظيفته أن يُنشئ أوّل مالك في منشأةٍ زُوِّدت للتوّ. وسحبُه
/// إعدادٌ يُغيَّر ونشرٌ يُعاد — وهذا مكتوب لا مُخفى.
/// </para>
/// </summary>
internal sealed class IssuedSessionResolver : IApiPrincipalResolver
{
    private readonly AccessResolver _sessions;
    private readonly IApiPrincipalResolver _configured;

    /// <summary>ينشئ الدليل.</summary>
    /// <param name="sessions">حالّ الجلسات المُصدَرة.</param>
    /// <param name="configured">دليل الإقلاع المُهيَّأ من الإعداد.</param>
    public IssuedSessionResolver(AccessResolver sessions, IApiPrincipalResolver configured)
    {
        ArgumentNullException.ThrowIfNull(sessions);
        ArgumentNullException.ThrowIfNull(configured);
        _sessions = sessions;
        _configured = configured;
    }

    /// <summary>عدد اعتمادات الإقلاع المُهيّأة. والجلسات المُصدَرة لا تُعدّ هنا: عددُها حالةُ قاعدة بيانات لا إعداد.</summary>
    public int Count => _configured.Count;

    /// <summary>
    /// المسار المتزامن يبقى على دليل الإعداد وحده.
    /// <para>
    /// <b>وهذا ليس نصفَ تنفيذ:</b> الجلسة المُصدَرة تحتاج بلوغَ مخزنٍ مشترك كي يُقرأ
    /// إبطالها فوراً، وبلوغٌ متزامن داخل وسيط يعني حجزَ خيط على كل طلب. فالوسيط ينادي
    /// <see cref="ResolveAsync"/>، وهذا العضو باقٍ لمن لا يملك سياقاً لا متزامناً.
    /// </para>
    /// </summary>
    /// <param name="presentedToken">النصّ المقدَّم.</param>
    public ApiPrincipal? Resolve(string presentedToken) => _configured.Resolve(presentedToken);

    /// <inheritdoc />
    public async ValueTask<CredentialVerdict> ResolveAsync(string presentedToken, CancellationToken cancellationToken)
    {
        AccessResolution resolution = await _sessions
            .ResolveAsync(presentedToken, cancellationToken)
            .ConfigureAwait(false);

        switch (resolution.Verdict)
        {
            case ResolutionVerdict.Accepted when resolution.Access is { } access:
                return CredentialVerdict.Accepted(new ApiPrincipal(
                    access.Tenant,
                    access.User,
                    access.Companies,
                    access.ExpiresAt,
                    access.SessionId,
                    access.ReadOnlyCompanies));

            case ResolutionVerdict.Expired:
                return CredentialVerdict.Expired;

            case ResolutionVerdict.Revoked:
                return CredentialVerdict.Revoked;

            default:
                // ليس جلسةً مُصدَرة: يُجرَّب دليل الإقلاع. والرفض النهائي واحدٌ لا يقول
                // في أي الدليلين لم يُوجَد — وإلا صار السطح عدّاد وجودٍ للاعتمادات.
                return _configured.Resolve(presentedToken) is { } principal
                    ? CredentialVerdict.Accepted(principal)
                    : CredentialVerdict.Rejected;
        }
    }
}
