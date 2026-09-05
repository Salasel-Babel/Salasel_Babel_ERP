using System.Globalization;
using Babel.Core.Application;
using Babel.Core.Audit;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Core.Access;

/// <summary>طلب دعوة عضو إلى منشأة.</summary>
/// <param name="Tenant">المستأجر.</param>
/// <param name="Company">المنشأة.</param>
/// <param name="Inviter">الداعي — ويجب أن يكون مالكاً فيها.</param>
/// <param name="DisplayNameAr">اسم المدعوّ بالعربية — السجلّ.</param>
/// <param name="Role">دوره.</param>
/// <param name="Member">
/// معرّف المدعوّ إن كان <b>مشتقّاً</b> لا مسكوكاً — و<c>null</c> في الحالة العامة.
/// <para>
/// <b>ولماذا وُجد هذا الحقل، ولمن:</b> التسجيل الأول باب مفتوح <b>حصينٌ ضد التكرار</b>،
/// وحصانتُه تقوم على أن كل معرّفاته مشتقّة حتمياً من مفتاح الطلب — فإعادةُ الإرسال تصطدم
/// بالمفتاح الفريد <c>(المنشأة، المستخدم)</c> فتُقرأ «سُجِّل من قبل». ومعرّفٌ يُسكّ
/// عشوائياً في كل محاولة كان سيجعل كل إعادة إرسال <b>عضويةً ثانية لمالكٍ ثانٍ</b>.
/// </para>
/// <para>
/// <b>ولا يصل هذا الحقل من السلك أبداً:</b> جسم <c>POST …/memberships</c> لا يحمله ولا
/// يستطيع أن يحمله (‏<c>UnmappedMemberHandling = Disallow</c>)، والجذر التركيبي وحده
/// يملؤه بقيمةٍ يشتقّها هو. ومعرّفٌ يختاره العميل هو انتحالٌ بحقل.
/// </para>
/// </param>
public sealed record MembershipGrantRequest(
    TenantId Tenant,
    Guid Company,
    UserId Inviter,
    string DisplayNameAr,
    MembershipRole Role,
    UserId? Member = null);

/// <summary>
/// دورة حياة الجلسة والعضوية: <b>تُصدَر، وتدور، وتُبطَل</b>.
/// <para>
/// <b>وما تغيّر بوجود هذه الخدمة:</b> كان الاعتماد يُحقن عند الإقلاع من الإعداد فلا يُصدر
/// ولا يدور ولا يُبطَل ولا ينقضي إلا بلحظةٍ ساكنة مكتوبة بيد. وهو شكلٌ يكفي عرضاً ولا يُباع:
/// خدمةٌ تُباع بالاشتراك لا يوجد فيها طريق لأن يُنشئ عميلٌ اعتماده، ولا لأن يسحبه حين
/// يترك موظّفٌ عمله.
/// </para>
/// <para>
/// <b>وثلاث ثوابت تحكم كل ما هنا:</b>
/// </para>
/// <list type="number">
///   <item><b>لا يُخزَّن اعتمادٌ قابل للاستعمال.</b> النصّ يُسكّ هنا ويُسلَّم مرّة، والبصمة
///         وحدها تنزل إلى الدليل (<see cref="AccessCredentials"/>).</item>
///   <item><b>اعتماد التجديد يدور، وعودتُه سرقة.</b> تقديمه مرّتين يُسقط العائلة كلّها،
///         ولا يُخدَم الطلب الثاني — لأن اعتماداً في يدين أحدُهما ليس صاحبه.</item>
///   <item><b>الإبطال فوري.</b> يُقرأ في استعلام الحلّ نفسه، ولا يُنتظر به انقضاء.</item>
/// </list>
/// <para>
/// <b>والاستحقاق يُسأل هنا بنيّة «قراءة» على فتح الجلسة وتجديدها وإبطالها عمداً</b>
/// (ADR-0034): مستأجرٌ انقطع اشتراكه <b>يدخل ويقرأ</b>، ولو مُنع الدخول لصار «التخفيض إلى
/// القراءة» حجباً باسم آخر. والنيّة «كتابة» على الدعوة وحدها — إضافة عضو نموٌّ في الاستعمال
/// لا إبرازٌ لسجلّ.
/// </para>
/// </summary>
public sealed class AccessService : IApplicationService
{
    private readonly IAccessDirectory _directory;
    private readonly IEntitlementEnforcer _enforcer;
    private readonly IAuditLog _audit;
    private readonly TimeProvider _clock;
    private readonly AccessPolicy _policy;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="directory">دليل المصادقة.</param>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="audit">سجلّ التدقيق.</param>
    /// <param name="clock">مصدر الوقت.</param>
    /// <param name="policy">
    /// مُدَد الاعتمادات. <b>مُعامِلٌ إلزامي لا ثابتٌ ساكن</b>: هذه سياسةُ أمنٍ تُشدَّد
    /// لحظةَ حادثة، ولا تُقرأ من صنفٍ لا يقبل ضبطاً.
    /// </param>
    public AccessService(
        IAccessDirectory directory,
        IEntitlementEnforcer enforcer,
        IAuditLog audit,
        TimeProvider clock,
        AccessPolicy policy)
    {
        ArgumentNullException.ThrowIfNull(directory);
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(audit);
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(policy);

        _directory = directory;
        _enforcer = enforcer;
        _audit = audit;
        _clock = clock;
        _policy = policy;
    }

    /// <summary>
    /// يفتح جلسة باعتماد انتساب. والانتساب يُستهلك ذرّياً: يُقبل مرّة واحدة ولا مرّتين.
    /// </summary>
    /// <param name="enrolmentCredential">النصّ المُقدَّم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<OpenedSession>> OpenSessionAsync(
        string enrolmentCredential,
        CancellationToken cancellationToken = default)
    {
        if (Refused(enrolmentCredential, out Error? tooLong))
        {
            return Result<OpenedSession>.Failure(tooLong!);
        }

        DateTimeOffset now = _clock.GetUtcNow();

        EnrolmentClaim claim = await _directory
            .ConsumeEnrolmentAsync(AccessCredentials.Digest(enrolmentCredential), now, cancellationToken)
            .ConfigureAwait(false);

        Error? refusal = claim.Outcome switch
        {
            EnrolmentOutcome.Rejected => AccessErrors.CredentialRejected,
            EnrolmentOutcome.Expired => AccessErrors.EnrolmentExpired,
            EnrolmentOutcome.AlreadyConsumed => AccessErrors.EnrolmentConsumed,
            _ => null,
        };

        if (refusal is not null)
        {
            return Result<OpenedSession>.Failure(refusal);
        }

        // الاستحقاق **بعد** المصادقة وبنيّة قراءة: من انقطع اشتراكه يدخل ويقرأ (ADR-0034).
        Result gate = await _enforcer
            .EnsureAsync(claim.Tenant, claim.User, BabelModule.Core, EntitlementAccess.Read, "Core.Access.OpenSession", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<OpenedSession>.Failure(gate.Errors);
        }

        Guid sessionId = Guid.CreateVersion7();
        Minted access = Mint(now, _policy.AccessLifetime);
        Minted refresh = Mint(now, _policy.RefreshLifetime);

        await _directory
            .OpenSessionAsync(
                sessionId, claim.Tenant, claim.User,
                access.Digest, access.ExpiresAt,
                refresh.Digest, refresh.ExpiresAt,
                now, cancellationToken)
            .ConfigureAwait(false);

        await RecordAsync(claim.Tenant, claim.User, "access.session_opened", sessionId, "الدورة 1", cancellationToken)
            .ConfigureAwait(false);

        return await DescribeAsync(sessionId, claim.Tenant, claim.User, generation: 1, access, refresh, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// يجدّد جلسة بتدوير اعتمادها. <b>وتقديم اعتماد تجديد مرّتين يُسقط العائلة كلّها</b>.
    /// </summary>
    /// <param name="refreshCredential">النصّ المُقدَّم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<OpenedSession>> RefreshSessionAsync(
        string refreshCredential,
        CancellationToken cancellationToken = default)
    {
        if (Refused(refreshCredential, out Error? tooLong))
        {
            return Result<OpenedSession>.Failure(tooLong!);
        }

        DateTimeOffset now = _clock.GetUtcNow();
        Minted access = Mint(now, _policy.AccessLifetime);
        Minted refresh = Mint(now, _policy.RefreshLifetime);

        // التدوير **قبل** الاستحقاق: كشفُ إعادة الاستعمال إجراءُ أمنٍ لا امتيازُ اشتراك،
        // ويجب أن يقع ولو كان المستأجر منقطعاً — بل لا سيّما حينئذ.
        RotationResult rotation = await _directory
            .RotateAsync(
                AccessCredentials.Digest(refreshCredential),
                access.Digest, access.ExpiresAt,
                refresh.Digest, refresh.ExpiresAt,
                now, cancellationToken)
            .ConfigureAwait(false);

        if (rotation.Outcome == RotationOutcome.Replayed)
        {
            await RecordAsync(
                    rotation.Tenant, rotation.User, "access.refresh_replayed", rotation.SessionId,
                    "أُبطلت العائلة كلّها / the whole family was revoked", cancellationToken)
                .ConfigureAwait(false);
        }

        Error? refusal = rotation.Outcome switch
        {
            RotationOutcome.Rejected => AccessErrors.CredentialRejected,
            RotationOutcome.Expired => AccessErrors.RefreshExpired,
            RotationOutcome.Replayed => AccessErrors.RefreshReplayed,
            RotationOutcome.SessionRevoked => AccessErrors.SessionRevoked,
            _ => null,
        };

        if (refusal is not null)
        {
            return Result<OpenedSession>.Failure(refusal);
        }

        Result gate = await _enforcer
            .EnsureAsync(rotation.Tenant, rotation.User, BabelModule.Core, EntitlementAccess.Read, "Core.Access.RefreshSession", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<OpenedSession>.Failure(gate.Errors);
        }

        return await DescribeAsync(
                rotation.SessionId, rotation.Tenant, rotation.User, rotation.Generation, access, refresh, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// يُبطل جلسة قائمة فوراً. والإبطال <b>ليس امتيازاً مشروطاً بالاشتراك</b>: من يريد
    /// أن يسحب اعتماداً يجب أن يستطيع ذلك في أسوأ يوم لا في أحسنه.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">صاحب الجلسة.</param>
    /// <param name="sessionId">الجلسة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<SessionRevocation>> RevokeSessionAsync(
        TenantId tenant,
        UserId actor,
        Guid sessionId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Read, "Core.Access.RevokeSession", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SessionRevocation>.Failure(gate.Errors);
        }

        DateTimeOffset now = _clock.GetUtcNow();

        SessionRevocation? revocation = await _directory
            .RevokeSessionAsync(sessionId, RevocationReasons.SignedOut, now, cancellationToken)
            .ConfigureAwait(false);

        if (revocation is null)
        {
            return Result<SessionRevocation>.Failure(AccessErrors.SessionRevoked);
        }

        await RecordAsync(tenant, actor, "access.session_revoked", sessionId, revocation.Reason, cancellationToken)
            .ConfigureAwait(false);

        return Result<SessionRevocation>.Success(revocation);
    }

    /// <summary>
    /// يدعو عضواً إلى منشأة: يسكّ له معرّفاً، ويمنحه دوره، ويُصدر اعتماد انتسابه <b>مرّة واحدة</b>.
    /// </summary>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<GrantedMembership>> GrantMembershipAsync(
        MembershipGrantRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result gate = await _enforcer
            .EnsureAsync(request.Tenant, request.Inviter, BabelModule.Core, EntitlementAccess.Write, "Core.Access.GrantMembership", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<GrantedMembership>.Failure(gate.Errors);
        }

        string name = (request.DisplayNameAr ?? string.Empty).Trim();

        if (name.Length == 0)
        {
            return Result<GrantedMembership>.Failure(AccessErrors.MemberNameMissing);
        }

        if (name.Length > AccessLimits.MaximumNameLength)
        {
            return Result<GrantedMembership>.Failure(AccessErrors.MemberNameTooLong);
        }

        Membership? inviter = await _directory
            .FindMembershipAsync(request.Company, request.Inviter, cancellationToken)
            .ConfigureAwait(false);

        // ‏**والاعتماد المُهيَّأ من الإعداد لا عضوية له**، فهو يمرّ من هنا مالكاً بحكم كونه
        // اعتماد التزويد نفسه — وذلك مُعلَن في القرار لا مُخفى: هو الاعتماد الوحيد الذي
        // لا يُصدره هذا السطح، وهو باب الإقلاع الذي يُنشئ أول مالك ثم لا يُستعمل بعده.
        if (inviter is not null && inviter.Role != MembershipRole.Owner)
        {
            return Result<GrantedMembership>.Failure(AccessErrors.InviterIsNotAnOwner);
        }

        DateTimeOffset now = _clock.GetUtcNow();
        Membership membership = new(
            request.Company, request.Member ?? new UserId(Guid.CreateVersion7()), request.Role, name, now);
        Minted enrolment = Mint(now, _policy.EnrolmentLifetime);

        bool granted = await _directory
            .TryGrantAsync(request.Tenant, membership, request.Inviter, enrolment.Digest, enrolment.ExpiresAt, cancellationToken)
            .ConfigureAwait(false);

        if (!granted)
        {
            return Result<GrantedMembership>.Failure(AccessErrors.MembershipAlreadyGranted);
        }

        await RecordAsync(
                request.Tenant, request.Inviter, "access.membership_granted", request.Company,
                membership.User + " · " + membership.Role, cancellationToken)
            .ConfigureAwait(false);

        return Result<GrantedMembership>.Success(
            new GrantedMembership(membership, new IssuedCredential(enrolment.Value, enrolment.ExpiresAt)));
    }

    /// <summary>
    /// <b>يسحب عضوية</b> من منشأة. فعلُ مالكٍ فيها، ولا يُترك آخرَ مالكٍ يُسحب.
    /// <para>
    /// <b>والنيّة «كتابة»</b>: سحبُ عضوٍ تغييرٌ في حالة المنشأة لا إبرازٌ لسجلّ، فيُغلق
    /// مع الوحدة المنقطعة كما تُغلق الدعوة. وهذا يفترق عن إبطال الجلسة عمداً: ذاك
    /// يسحبه صاحبُه من نفسه ويجب أن يعمل في أسوأ يوم لا في أحسنه (ADR-0045 §٤).
    /// </para>
    /// <para>
    /// <b>وأثرُه فوري بحكم البناء لا بحكم مهمّة تنظيف:</b> ما تبلغه الجلسة يُقرأ من
    /// العضويات في كل طلب، فالصفّ المسحوب يختفي من المجموعة عند الطلب التالي.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل — ويجب أن يكون مالكاً في المنشأة.</param>
    /// <param name="company">المنشأة.</param>
    /// <param name="member">العضو المطلوب سحبه.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<MembershipRevocation>> RevokeMembershipAsync(
        TenantId tenant,
        UserId actor,
        Guid company,
        UserId member,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Write, "Core.Access.RevokeMembership", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<MembershipRevocation>.Failure(gate.Errors);
        }

        Error? denied = await RefuseNonOwnerAsync(company, actor, cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return Result<MembershipRevocation>.Failure(denied);
        }

        MembershipRevocation revocation = await _directory
            .RevokeMembershipAsync(company, member, _clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        Error? refusal = revocation.Outcome switch
        {
            MembershipMutation.NotFound => AccessErrors.MembershipNotFound,
            MembershipMutation.LastOwner => AccessErrors.LastOwnerCannotBeRemoved,
            _ => null,
        };

        if (refusal is not null)
        {
            return Result<MembershipRevocation>.Failure(refusal);
        }

        await RecordAsync(
                tenant, actor, "access.membership_revoked", company,
                member.Value.ToString("D", CultureInfo.InvariantCulture) + " · " + revocation.Membership!.Role,
                cancellationToken)
            .ConfigureAwait(false);

        return Result<MembershipRevocation>.Success(revocation);
    }

    /// <summary>
    /// <b>يغيّر دور عضوية</b>. فعلُ مالكٍ في المنشأة، ولا يُخفَض به آخر مالك.
    /// <para>
    /// <b>ومورد فرعي مستقلّ عند الحدّ لا حقلٌ يُعدَّل:</b> الدور صلاحيةُ وصول، وتغييرُه
    /// حدثٌ تدقيقي بمن ومتى — لا خاصيةٌ تُكتب في تحديثٍ جزئي على العضو.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل — ويجب أن يكون مالكاً في المنشأة.</param>
    /// <param name="company">المنشأة.</param>
    /// <param name="member">العضو.</param>
    /// <param name="role">الدور المطلوب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Write)]
    public async ValueTask<Result<MembershipRoleChange>> ChangeMembershipRoleAsync(
        TenantId tenant,
        UserId actor,
        Guid company,
        UserId member,
        MembershipRole role,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Write, "Core.Access.ChangeMembershipRole", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<MembershipRoleChange>.Failure(gate.Errors);
        }

        Error? denied = await RefuseNonOwnerAsync(company, actor, cancellationToken).ConfigureAwait(false);
        if (denied is not null)
        {
            return Result<MembershipRoleChange>.Failure(denied);
        }

        MembershipRoleChange change = await _directory
            .ChangeRoleAsync(company, member, role, _clock.GetUtcNow(), cancellationToken)
            .ConfigureAwait(false);

        Error? refusal = change.Outcome switch
        {
            MembershipMutation.NotFound => AccessErrors.MembershipNotFound,
            MembershipMutation.LastOwner => AccessErrors.LastOwnerCannotBeRemoved,
            MembershipMutation.Unchanged => AccessErrors.RoleUnchanged,
            _ => null,
        };

        if (refusal is not null)
        {
            return Result<MembershipRoleChange>.Failure(refusal);
        }

        await RecordAsync(
                tenant, actor, "access.membership_role_changed", company,
                member.Value.ToString("D", CultureInfo.InvariantCulture)
                    + " · " + change.PreviousRole + " ⇐ " + change.Membership!.Role,
                cancellationToken)
            .ConfigureAwait(false);

        return Result<MembershipRoleChange>.Success(change);
    }

    /// <summary>
    /// يرفض فاعلاً ليس مالكاً في المنشأة — <b>موضعٌ واحد للفعلين</b>.
    /// <para>
    /// والاعتماد المُهيَّأ من الإعداد لا عضوية له، فيمرّ مالكاً بحكم كونه اعتماد
    /// التزويد نفسه — وهو باب الإقلاع المُعلَن في ADR-0045 §٣٫٣، لا استثناءً مُخفى.
    /// </para>
    /// </summary>
    private async ValueTask<Error?> RefuseNonOwnerAsync(Guid company, UserId actor, CancellationToken cancellationToken)
    {
        Membership? membership = await _directory
            .FindMembershipAsync(company, actor, cancellationToken)
            .ConfigureAwait(false);

        return membership is not null && membership.Role != MembershipRole.Owner
            ? AccessErrors.ActorIsNotAnOwner
            : null;
    }

    /// <summary>يقرأ أعضاء منشأة. ولا اعتماد واحد يخرج من هنا — القائمة أسماء وأدوار.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">القارئ.</param>
    /// <param name="company">المنشأة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<Membership>>> ListMembershipsAsync(
        TenantId tenant,
        UserId actor,
        Guid company,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Read, "Core.Access.ListMemberships", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<Membership>>.Failure(gate.Errors);
        }

        IReadOnlyList<Membership> members = await _directory
            .ListMembershipsAsync(company, cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<Membership>>.Success(members);
    }

    /// <summary>
    /// منشآت المستأجر — <b>كل منشأة له فيها عضوية</b>.
    /// <para>
    /// ولا اعتماد يخرج من هنا ولا اسم عضو: معرّفات منشآت، وهي بيانات المستأجر نفسه.
    /// والنيّة «قراءة» عمداً: من انقطع اشتراكه يقرأ ما يملكه.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">القارئ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Core, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<Guid>>> CompaniesOfAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Core, EntitlementAccess.Read, "Core.Access.CompaniesOf", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<Guid>>.Failure(gate.Errors);
        }

        IReadOnlyList<Guid> companies = await _directory
            .CompaniesOfAsync(tenant, cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<Guid>>.Success(companies);
    }

    private static bool Refused(string presented, out Error? error)
    {
        error = string.IsNullOrEmpty(presented) || presented.Length <= AccessLimits.MaximumPresentedLength
            ? null
            : AccessErrors.CredentialTooLong;

        return error is not null;
    }

    private static Minted Mint(DateTimeOffset now, TimeSpan lifetime)
    {
        string value = AccessCredentials.Mint();
        return new Minted(value, AccessCredentials.Digest(value), now + lifetime);
    }

    private async ValueTask<Result<OpenedSession>> DescribeAsync(
        Guid sessionId,
        TenantId tenant,
        UserId user,
        int generation,
        Minted access,
        Minted refresh,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<Membership> memberships = await _directory
            .MembershipsOfAsync(tenant, user, cancellationToken)
            .ConfigureAwait(false);

        return Result<OpenedSession>.Success(new OpenedSession(
            sessionId,
            tenant,
            user,
            generation,
            new IssuedCredential(access.Value, access.ExpiresAt),
            new IssuedCredential(refresh.Value, refresh.ExpiresAt),
            memberships));
    }

    private async ValueTask RecordAsync(
        TenantId tenant,
        UserId actor,
        string action,
        Guid subject,
        string detail,
        CancellationToken cancellationToken) =>
        await _audit
            .RecordAsync(
                new AuditEntry(
                    tenant,
                    actor,
                    _clock.GetUtcNow(),
                    action,
                    subject.ToString("D", CultureInfo.InvariantCulture),
                    detail),
                cancellationToken)
            .ConfigureAwait(false);

    /// <summary>اعتمادٌ مسكوك: نصّه وبصمته وانقضاؤه، ولا يعبر النصّ حدّ هذه الخدمة إلا إلى صاحبه.</summary>
    private readonly record struct Minted(string Value, string Digest, DateTimeOffset ExpiresAt);
}
