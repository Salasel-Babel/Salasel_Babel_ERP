namespace Babel.Core.Access;

/// <summary>حكم حلّ اعتماد فاعل كما يقرؤه حدّ HTTP.</summary>
public enum ResolutionVerdict
{
    /// <summary>لا يقابله شيء — ولا يُفرَّق عن أي اعتماد غير مقبول.</summary>
    Rejected = 0,

    /// <summary>انقضى. اعتمادٌ <b>يملكه صاحبه</b>، فإخباره لا يكشف له شيئاً لا يعرفه.</summary>
    Expired = 1,

    /// <summary>أُبطلت عائلته. ويُقرأ الآن لا عند الانقضاء.</summary>
    Revoked = 2,

    /// <summary>حيّ، والهوية في <see cref="ResolvedAccess"/>.</summary>
    Accepted = 3,
}

/// <summary>نتيجة حلّ اعتماد.</summary>
/// <param name="Verdict">الحكم.</param>
/// <param name="Access">الهوية عند <see cref="ResolutionVerdict.Accepted"/> وحدها.</param>
public sealed record AccessResolution(ResolutionVerdict Verdict, ResolvedAccess? Access);

/// <summary>
/// حلّ الاعتماد الفاعل إلى هوية — <b>وهو مصادقة، فلا يمرّ بالاستحقاق</b>.
/// <para>
/// <b>ولماذا لا يمرّ:</b> ‏ADR-0034 يقرّر أن الاشتراك المنقطع <b>يُخفَّض إلى القراءة ولا
/// يُنتزَع به السجلّ</b> — لأن حفظ السجلات المحاسبية وإبرازها التزامٌ على المنشأة، فنزاعٌ
/// تجاري بيننا وبين عميل لا يجوز أن يضعه في مخالفة. وجعلُ <b>الدخول نفسه</b> مشروطاً
/// بالاستحقاق يُبطل ذلك القرار من بابه الخلفي: مستأجرٌ يُخفَّض إلى القراءة لا يستطيع أن
/// يقرأ إن لم يستطع أن يدخل. فالحدّ هنا يسأل «أهذا الاعتماد حيّ؟» وحدها، والاستحقاق
/// يُسأل بعد ذلك عند كل عملية، في موضعه الواحد.
/// </para>
/// <para>
/// وهو ليس <c>IApplicationService</c> عمداً: نقاط الدخول التي تحرسها القاعدة 6 عملياتُ
/// <b>مستأجرٍ معلوم</b>، والمستأجر هنا <b>ناتجُ</b> النداء لا مدخلُه.
/// </para>
/// </summary>
public sealed class AccessResolver
{
    private readonly IAccessDirectory _directory;
    private readonly TimeProvider _clock;

    /// <summary>ينشئ الحالّ.</summary>
    /// <param name="directory">دليل المصادقة.</param>
    /// <param name="clock">مصدر الوقت.</param>
    public AccessResolver(IAccessDirectory directory, TimeProvider clock)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(clock);
        _directory = directory;
        _clock = clock;
    }

    /// <summary>يحلّ اعتماداً فاعلاً مُقدَّماً.</summary>
    /// <param name="presented">النصّ المُقدَّم بعد <c>Bearer</c>.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<AccessResolution> ResolveAsync(string presented, CancellationToken cancellationToken = default)
    {
        // ‏**الرفض قبل التجزئة**: نصٌّ أطول من أي اعتماد يُصدره هذا السطح لا يستحقّ دورة
        // تجزئة ولا بحثاً في جدول. وحدٌّ معلن هنا يجعل الطلب المُلفَّق أرخص على الخادم
        // من الطلب الشرعي، لا أغلى.
        if (string.IsNullOrEmpty(presented) || presented.Length > AccessLimits.MaximumPresentedLength)
        {
            return new AccessResolution(ResolutionVerdict.Rejected, null);
        }

        DateTimeOffset now = _clock.GetUtcNow();
        AccessLookup lookup = await _directory
            .LookupAccessAsync(AccessCredentials.Digest(presented), now, cancellationToken)
            .ConfigureAwait(false);

        switch (lookup.Outcome)
        {
            case AccessOutcome.Rejected:
                return new AccessResolution(ResolutionVerdict.Rejected, null);
            case AccessOutcome.Expired:
                return new AccessResolution(ResolutionVerdict.Expired, null);
            case AccessOutcome.Revoked:
                return new AccessResolution(ResolutionVerdict.Revoked, null);
            default:
                break;
        }

        IReadOnlyList<Membership> memberships = await _directory
            .MembershipsOfAsync(lookup.Tenant, lookup.User, cancellationToken)
            .ConfigureAwait(false);

        // النطاق **يُشتقّ من العضويات لا من جسم الطلب ولا من ترويسة**: الاعتماد يبلغ ما
        // يبلغه صاحبُه اليوم، لا ما كان يبلغه يوم فُتحت الجلسة. فسحبُ عضوية يُقرأ عند
        // الطلب التالي مباشرة، ولا ينتظر انقضاء اعتماد.
        HashSet<Guid> companies = [.. memberships.Select(static membership => membership.Company)];
        HashSet<Guid> readOnly =
        [
            .. memberships
                .Where(static membership => membership.Role == MembershipRole.Reader)
                .Select(static membership => membership.Company),
        ];

        return new AccessResolution(
            ResolutionVerdict.Accepted,
            new ResolvedAccess(lookup.SessionId, lookup.Tenant, lookup.User, companies, readOnly, lookup.ExpiresAt));
    }
}
