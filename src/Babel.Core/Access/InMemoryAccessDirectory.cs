using Babel.SharedKernel;

namespace Babel.Core.Access;

/// <summary>
/// دليل مصادقة في ذاكرة العملية — <b>لاختبارات الوحدة وحدها</b>.
/// <para>
/// <b>وهو ليس تنفيذ الخادم</b>: الجذر التركيبي يسجّل الدليل فوق PostgreSQL، لأن جلسةً
/// تموت مع العملية تعني أن كل مستخدم يخرج عند كل نشر — وهو بالضبط شكل العطل الذي أُصلح
/// حين انتقل مخزن التأسيس من الذاكرة إلى قاعدة البيانات.
/// </para>
/// <para>
/// والذرّية هنا بقفل واحد على كل الحالة: هذا النوع لا يُشارَك بين عمليتين أصلاً، فقفلٌ
/// واحد يعطي بالضبط ما تعطيه معاملةٌ في المسار الحقيقي — <b>فائز واحد لا فائزان</b>.
/// </para>
/// </summary>
public sealed class InMemoryAccessDirectory : IAccessDirectory
{
    private readonly Lock _gate = new();
    private readonly Dictionary<string, EnrolmentEntry> _enrolments = new(StringComparer.Ordinal);
    private readonly Dictionary<string, CredentialEntry> _credentials = new(StringComparer.Ordinal);
    private readonly Dictionary<Guid, SessionEntry> _sessions = [];
    private readonly List<MembershipEntry> _memberships = [];

    /// <inheritdoc />
    public Task<bool> TryGrantAsync(
        TenantId tenant,
        Membership membership,
        UserId grantedBy,
        string enrolmentDigest,
        DateTimeOffset enrolmentExpiresAt,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(membership);

        lock (_gate)
        {
            if (_memberships.Any(entry => entry.Membership.Company == membership.Company
                    && entry.Membership.User == membership.User))
            {
                return Task.FromResult(false);
            }

            _memberships.Add(new MembershipEntry(tenant, membership, grantedBy));
            _enrolments[enrolmentDigest] = new EnrolmentEntry(tenant, membership.User, enrolmentExpiresAt, Consumed: false);
            return Task.FromResult(true);
        }
    }

    /// <inheritdoc />
    public Task<MembershipRevocation> RevokeMembershipAsync(
        Guid company, UserId member, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            int index = _memberships.FindIndex(entry =>
                entry.Membership.Company == company && entry.Membership.User == member);

            if (index < 0)
            {
                return Task.FromResult(new MembershipRevocation(MembershipMutation.NotFound, null, now));
            }

            Membership existing = _memberships[index].Membership;

            if (existing.Role == MembershipRole.Owner && OwnerCount(company) <= 1)
            {
                return Task.FromResult(new MembershipRevocation(MembershipMutation.LastOwner, null, now));
            }

            _memberships.RemoveAt(index);
            return Task.FromResult(new MembershipRevocation(MembershipMutation.Applied, existing, now));
        }
    }

    /// <inheritdoc />
    public Task<MembershipRoleChange> ChangeRoleAsync(
        Guid company, UserId member, MembershipRole role, DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            int index = _memberships.FindIndex(entry =>
                entry.Membership.Company == company && entry.Membership.User == member);

            if (index < 0)
            {
                return Task.FromResult(new MembershipRoleChange(MembershipMutation.NotFound, null, role, now));
            }

            MembershipEntry entry = _memberships[index];
            MembershipRole previous = entry.Membership.Role;

            if (previous == role)
            {
                return Task.FromResult(new MembershipRoleChange(MembershipMutation.Unchanged, null, previous, now));
            }

            if (previous == MembershipRole.Owner && OwnerCount(company) <= 1)
            {
                return Task.FromResult(new MembershipRoleChange(MembershipMutation.LastOwner, null, previous, now));
            }

            Membership changed = entry.Membership with { Role = role };
            _memberships[index] = entry with { Membership = changed };
            return Task.FromResult(new MembershipRoleChange(MembershipMutation.Applied, changed, previous, now));
        }
    }

    /// <summary>عدد المالكين في منشأة. يُقرأ تحت القفل نفسه الذي يقع تحته الفعل.</summary>
    private int OwnerCount(Guid company) => _memberships.Count(entry =>
        entry.Membership.Company == company && entry.Membership.Role == MembershipRole.Owner);

    /// <inheritdoc />
    public Task<Membership?> FindMembershipAsync(Guid company, UserId user, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult(_memberships
                .Where(entry => entry.Membership.Company == company && entry.Membership.User == user)
                .Select(static entry => entry.Membership)
                .FirstOrDefault());
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Membership>> ListMembershipsAsync(Guid company, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Membership>>(
                [.. _memberships
                    .Where(entry => entry.Membership.Company == company)
                    .Select(static entry => entry.Membership)
                    .OrderBy(static membership => membership.User.ToString(), StringComparer.Ordinal)]);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Guid>> CompaniesOfAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Guid>>(
                [.. _memberships
                    .Where(entry => entry.Tenant == tenant)
                    .Select(static entry => entry.Membership.Company)
                    .Distinct()
                    .OrderBy(static company => company.ToString(), StringComparer.Ordinal)]);
        }
    }

    /// <inheritdoc />
    public Task<IReadOnlyList<Membership>> MembershipsOfAsync(TenantId tenant, UserId user, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            return Task.FromResult<IReadOnlyList<Membership>>(
                [.. _memberships
                    .Where(entry => entry.Tenant == tenant && entry.Membership.User == user)
                    .Select(static entry => entry.Membership)
                    .OrderBy(static membership => membership.Company.ToString(), StringComparer.Ordinal)]);
        }
    }

    /// <inheritdoc />
    public Task<EnrolmentClaim> ConsumeEnrolmentAsync(string enrolmentDigest, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!TryFind(_enrolments, enrolmentDigest, out string? key) || key is null)
            {
                return Task.FromResult(new EnrolmentClaim(EnrolmentOutcome.Rejected, TenantId.None, UserId.None));
            }

            EnrolmentEntry entry = _enrolments[key];

            if (entry.Consumed)
            {
                return Task.FromResult(new EnrolmentClaim(EnrolmentOutcome.AlreadyConsumed, entry.Tenant, entry.User));
            }

            if (now >= entry.ExpiresAt)
            {
                return Task.FromResult(new EnrolmentClaim(EnrolmentOutcome.Expired, entry.Tenant, entry.User));
            }

            _enrolments[key] = entry with { Consumed = true };
            return Task.FromResult(new EnrolmentClaim(EnrolmentOutcome.Accepted, entry.Tenant, entry.User));
        }
    }

    /// <inheritdoc />
    public Task OpenSessionAsync(
        Guid sessionId,
        TenantId tenant,
        UserId user,
        string accessDigest,
        DateTimeOffset accessExpiresAt,
        string refreshDigest,
        DateTimeOffset refreshExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            _sessions[sessionId] = new SessionEntry(tenant, user, RevokedAt: null, Reason: string.Empty, Generation: 1);
            _credentials[accessDigest] = new CredentialEntry(sessionId, CredentialKinds.Access, accessExpiresAt, Consumed: false);
            _credentials[refreshDigest] = new CredentialEntry(sessionId, CredentialKinds.Refresh, refreshExpiresAt, Consumed: false);
        }

        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public Task<RotationResult> RotateAsync(
        string refreshDigest,
        string accessDigest,
        DateTimeOffset accessExpiresAt,
        string nextRefreshDigest,
        DateTimeOffset nextRefreshExpiresAt,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!TryFind(_credentials, refreshDigest, out string? key)
                || key is null
                || _credentials[key].Kind != CredentialKinds.Refresh)
            {
                return Task.FromResult(new RotationResult(RotationOutcome.Rejected, Guid.Empty, TenantId.None, UserId.None, 0));
            }

            CredentialEntry credential = _credentials[key];
            SessionEntry session = _sessions[credential.SessionId];

            // ‏**إعادة الاستعمال أولاً وقبل كل شيء** — قبل الانقضاء وقبل الإبطال: اعتمادٌ
            // مستهلَك يعود هو اعتمادٌ في يد اثنين، والجواب إسقاط العائلة لا ترتيبُ رسائل.
            if (credential.Consumed)
            {
                _sessions[credential.SessionId] = session.RevokedAt is null
                    ? session with { RevokedAt = now, Reason = RevocationReasons.RefreshReplayed }
                    : session;

                return Task.FromResult(new RotationResult(
                    RotationOutcome.Replayed, credential.SessionId, session.Tenant, session.User, 0));
            }

            if (session.RevokedAt is not null)
            {
                return Task.FromResult(new RotationResult(
                    RotationOutcome.SessionRevoked, credential.SessionId, session.Tenant, session.User, 0));
            }

            if (now >= credential.ExpiresAt)
            {
                return Task.FromResult(new RotationResult(
                    RotationOutcome.Expired, credential.SessionId, session.Tenant, session.User, 0));
            }

            int generation = session.Generation + 1;
            _credentials[key] = credential with { Consumed = true };
            _sessions[credential.SessionId] = session with { Generation = generation };
            _credentials[accessDigest] = new CredentialEntry(credential.SessionId, CredentialKinds.Access, accessExpiresAt, Consumed: false);
            _credentials[nextRefreshDigest] = new CredentialEntry(credential.SessionId, CredentialKinds.Refresh, nextRefreshExpiresAt, Consumed: false);

            return Task.FromResult(new RotationResult(
                RotationOutcome.Rotated, credential.SessionId, session.Tenant, session.User, generation));
        }
    }

    /// <inheritdoc />
    public Task<AccessLookup> LookupAccessAsync(string accessDigest, DateTimeOffset now, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!TryFind(_credentials, accessDigest, out string? key)
                || key is null
                || _credentials[key].Kind != CredentialKinds.Access)
            {
                return Task.FromResult(new AccessLookup(AccessOutcome.Rejected, Guid.Empty, TenantId.None, UserId.None, default));
            }

            CredentialEntry credential = _credentials[key];
            SessionEntry session = _sessions[credential.SessionId];

            AccessOutcome outcome = session.RevokedAt is not null
                ? AccessOutcome.Revoked
                : now >= credential.ExpiresAt ? AccessOutcome.Expired : AccessOutcome.Live;

            return Task.FromResult(new AccessLookup(
                outcome, credential.SessionId, session.Tenant, session.User, credential.ExpiresAt));
        }
    }

    /// <inheritdoc />
    public Task<SessionRevocation?> RevokeSessionAsync(
        Guid sessionId,
        string reason,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (!_sessions.TryGetValue(sessionId, out SessionEntry session))
            {
                return Task.FromResult<SessionRevocation?>(null);
            }

            if (session.RevokedAt is { } already)
            {
                return Task.FromResult<SessionRevocation?>(new SessionRevocation(sessionId, already, session.Reason));
            }

            _sessions[sessionId] = session with { RevokedAt = now, Reason = reason };
            return Task.FromResult<SessionRevocation?>(new SessionRevocation(sessionId, now, reason));
        }
    }

    /// <summary>
    /// بحثٌ بالبصمة يقارن <b>بزمن ثابت</b> بدل أن يعتمد على تجزئة القاموس.
    /// <para>
    /// وقاموسٌ عادي كان سيكفي وظيفياً؛ لكن القاعدة «لا يُقارَن اعتماد بمشغّل المساواة»
    /// قاعدةٌ يجب أن تصمد في كل تنفيذ، وإلا صار التنفيذ الأسهل هو القدوة.
    /// </para>
    /// </summary>
    private static bool TryFind<TValue>(Dictionary<string, TValue> source, string digest, out string? key)
    {
        key = null;

        foreach (string candidate in source.Keys)
        {
            if (AccessCredentials.DigestsMatch(candidate, digest))
            {
                key = candidate;
            }
        }

        return key is not null;
    }

    private sealed record MembershipEntry(TenantId Tenant, Membership Membership, UserId GrantedBy);

    private readonly record struct EnrolmentEntry(TenantId Tenant, UserId User, DateTimeOffset ExpiresAt, bool Consumed);

    private readonly record struct CredentialEntry(Guid SessionId, string Kind, DateTimeOffset ExpiresAt, bool Consumed);

    private readonly record struct SessionEntry(TenantId Tenant, UserId User, DateTimeOffset? RevokedAt, string Reason, int Generation);
}

/// <summary>نوعا الاعتماد كما يُكتبان في العمود. مجموعة مغلقة يقابلها قيد تحقّق في المخطّط.</summary>
public static class CredentialKinds
{
    /// <summary>الاعتماد الفاعل — يُحمل في كل طلب، فعمره قصير.</summary>
    public const string Access = "access";

    /// <summary>اعتماد التجديد — يدور، ويُستهلك مرّة واحدة.</summary>
    public const string Refresh = "refresh";
}
