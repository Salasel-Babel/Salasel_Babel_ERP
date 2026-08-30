using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.Access;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق دورة حياة الجلسة والعضوية.
/// <para>
/// <b>ولا قرار واحد في هذا الملف</b> (القاعدة 13): لا سكّ اعتماد، ولا مقارنة بصمة، ولا
/// حكمٌ على إعادة استعمال، ولا عمرُ اعتماد. كلّها في <see cref="AccessService"/>؛ وما هنا
/// قراءةُ نطاق، وقراءةُ جسم، ونقلٌ، وترجمةُ نتيجة.
/// </para>
/// <para>
/// <b>وبابان بلا مصادقة</b> — فتحُ الجلسة وتجديدها — وهما الوحيدان اللذان يُضافان إلى
/// قائمة الأبواب المفتوحة منذ نقطة الصحّة. وسببُ فتحهما بنيوي: من يطلب اعتماداً لا يملك
/// اعتماداً. ومع ذلك <b>لا يخرج منهما شيء لمن لا يقدّم اعتماد انتساب أو تجديد صحيحاً</b>،
/// والرفض واحدٌ لا يُفرَّق فيه المختلَق عن غيره.
/// </para>
/// </summary>
internal static class AccessEndpoints
{
    /// <summary>يسجّل نقاط نهاية المصادقة والعضوية.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapAccessApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(AccessRoutes.Sessions, OpenSessionAsync);
        app.MapPost(AccessRoutes.SessionRenewal, RenewSessionAsync);
        app.MapPost(AccessRoutes.SessionRevocation, RevokeSessionAsync);
        app.MapGet(AccessRoutes.Memberships, ListMembersAsync);
        app.MapPost(AccessRoutes.Memberships, GrantMembershipAsync);
        app.MapPost(AccessRoutes.MembershipRevocation, RevokeMembershipAsync);
        app.MapPost(AccessRoutes.MembershipRoleChanges, ChangeMembershipRoleAsync);
    }

    private static async Task<IResult> OpenSessionAsync(
        HttpContext context,
        AccessService access,
        CancellationToken cancellationToken)
    {
        (OpenSessionRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<OpenSessionRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        Result<OpenedSession> result = await access
            .OpenSessionAsync(dto.EnrolmentCredential ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        return Translate(context, result);
    }

    private static async Task<IResult> RenewSessionAsync(
        HttpContext context,
        AccessService access,
        CancellationToken cancellationToken)
    {
        (RenewSessionRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<RenewSessionRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        Result<OpenedSession> result = await access
            .RefreshSessionAsync(dto.RefreshCredential ?? string.Empty, cancellationToken)
            .ConfigureAwait(false);

        return Translate(context, result);
    }

    private static async Task<IResult> RevokeSessionAsync(
        HttpContext context,
        AccessService access,
        CancellationToken cancellationToken)
    {
        ApiPrincipal principal = RequestPrincipal.Of(context);

        // اعتمادٌ مُهيَّأ من الإعداد لا عائلة له، فلا شيء يُبطَل: الإبطال يقع على جلسة
        // أُصدرت من هذا السطح. وقولُ ذلك برمزه أصدق من ردّ «تمّ» على فعلٍ لم يقع.
        if (principal.Session is not { } sessionId)
        {
            return HttpProblemResults.Code(
                context,
                "access.session_not_issued_here",
                "هذا الاعتماد لم يُصدره سطح الجلسات، فلا عائلة له تُبطَل. وهو اعتماد التزويد الذي يُنشئ أول "
                + "مالك ثم لا يُستعمل بعده — وسحبُه يقع في إعداد الخادم لا من هنا.",
                "This credential was not issued by the session surface, so there is no family to revoke. It is the "
                + "provisioning credential that creates the first owner and is not used afterwards; withdrawing it "
                + "happens in server configuration, not here.",
                status: StatusCodes.Status409Conflict);
        }

        Result<SessionRevocation> result = await access
            .RevokeSessionAsync(principal.Tenant, principal.User, sessionId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(
                new SessionRevocationDto(
                    Identifier(result.Value.SessionId), Instant(result.Value.RevokedAt), result.Value.Reason),
                ApiJson.Options,
                statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ListMembersAsync(
        HttpContext context,
        AccessService access,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        Result<IReadOnlyList<Membership>> result = await access
            .ListMembershipsAsync(principal.Tenant, principal.User, companyId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(
                new MembershipListDto(
                    Identifier(companyId),
                    result.Value.Count,
                    [.. result.Value.Select(ToDto)]),
                ApiJson.Options);
    }

    private static async Task<IResult> GrantMembershipAsync(
        HttpContext context,
        AccessService access,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (GrantMembershipRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<GrantMembershipRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        // الدور يُقرأ هنا شكلياً وحده — والرمز الذي يخرج على المجهول من النواة، لأنها هي
        // التي تملك الكتالوج وتسمّي المعروف في رسالتها (القاعدة 13).
        if (!Enum.TryParse(dto.Role, ignoreCase: false, out MembershipRole role) || !Enum.IsDefined(role))
        {
            return HttpProblemResults.Domain(context, [AccessErrors.RoleUnknown(dto.Role ?? string.Empty)]);
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        Result<GrantedMembership> result = await access
            .GrantMembershipAsync(
                new MembershipGrantRequest(
                    principal.Tenant, companyId, principal.User, dto.DisplayNameAr ?? string.Empty, role),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(
                new GrantedMembershipDto(
                    Identifier(companyId),
                    ToDto(result.Value.Membership),
                    result.Value.Enrolment.Value,
                    Instant(result.Value.Enrolment.ExpiresAt)),
                ApiJson.Options,
                statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> RevokeMembershipAsync(
        HttpContext context,
        AccessService access,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!TryMember(context, out Guid memberId, out IResult? malformed))
        {
            return malformed!;
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        Result<MembershipRevocation> result = await access
            .RevokeMembershipAsync(principal.Tenant, principal.User, companyId, new UserId(memberId), cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(
                new MembershipRevocationDto(
                    Identifier(companyId), ToDto(result.Value.Membership!), Instant(result.Value.RevokedAt)),
                ApiJson.Options,
                statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ChangeMembershipRoleAsync(
        HttpContext context,
        AccessService access,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!TryMember(context, out Guid memberId, out IResult? malformed))
        {
            return malformed!;
        }

        (ChangeMembershipRoleRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<ChangeMembershipRoleRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        // الدور يُقرأ شكلياً وحده هنا، والرمز على المجهول من النواة — هي التي تملك
        // الكتالوج وتسمّي المعروف في رسالتها (القاعدة 13).
        if (!Enum.TryParse(dto.Role, ignoreCase: false, out MembershipRole role) || !Enum.IsDefined(role))
        {
            return HttpProblemResults.Domain(context, [AccessErrors.RoleUnknown(dto.Role ?? string.Empty)]);
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        Result<MembershipRoleChange> result = await access
            .ChangeMembershipRoleAsync(
                principal.Tenant, principal.User, companyId, new UserId(memberId), role, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(
                new MembershipRoleChangeDto(
                    Identifier(companyId),
                    ToDto(result.Value.Membership!),
                    result.Value.PreviousRole.ToString(),
                    Instant(result.Value.ChangedAt)),
                ApiJson.Options,
                statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// يقرأ معرّف العضوية من المسار بفحص شكلي وحده.
    /// <para>
    /// <b>ومعرّف العضوية هو معرّف عضوها</b> — هويتها <c>(المنشأة، العضو)</c> والمنشأة في
    /// المسار سلفاً. وعضويةٌ لا وجود لها تُرفض في النواة برمزها لا هنا: التمييز بين
    /// «شكلٌ خاطئ» و«لا وجود له» يقع حيث تُقرأ الحقيقة.
    /// </para>
    /// </summary>
    private static bool TryMember(HttpContext context, out Guid memberId, out IResult? malformed)
    {
        malformed = null;

        string raw = context.Request.RouteValues.TryGetValue("membershipId", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (Guid.TryParseExact(raw, "D", out memberId) && memberId != Guid.Empty)
        {
            return true;
        }

        malformed = HttpProblemResults.Code(
            context,
            "membership.id_malformed",
            "معرّف العضوية في المسار ليس معرّفاً صالحاً بصيغة 8-4-4-4-12. وهو معرّف العضو نفسه، "
            + "كما تُرجعه قائمة الأعضاء.",
            "The membership identifier in the path is not a valid 8-4-4-4-12 identifier. It is the member's own "
            + "identifier, exactly as the member list returns it.",
            "membershipId",
            StatusCodes.Status400BadRequest);
        return false;
    }

    private static IResult Translate(HttpContext context, Result<OpenedSession> result)
    {
        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        OpenedSession session = result.Value;

        return Results.Json(
            new AccessSessionDto(
                Identifier(session.SessionId),
                Identifier(session.Tenant.Value),
                Identifier(session.User.Value),
                session.Generation,
                session.Access.Value,
                Instant(session.Access.ExpiresAt),
                session.Refresh.Value,
                Instant(session.Refresh.ExpiresAt),
                session.WriteReachesNothing,
                [.. session.Memberships.Select(static membership =>
                    new AccessMembershipDto(Identifier(membership.Company), membership.Role.ToString()))]),
            ApiJson.Options,
            statusCode: StatusCodes.Status201Created);
    }

    private static MembershipDto ToDto(Membership membership) => new(
        Identifier(membership.User.Value),
        membership.DisplayNameAr,
        membership.Role.ToString(),
        Instant(membership.GrantedAt));

    private static string Identifier(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    /// <summary>
    /// لحظةٌ على السلك: ‏ISO 8601 الدوّارة بتوقيت UTC وبثقافة ثابتة.
    /// <para>
    /// وهي الصيغة نفسها التي يقرأ بها الخادمُ <c>NotAfter</c> من إعداده — صيغةٌ واحدة
    /// للحظة واحدة، فلا يوجد وقتٌ يُكتب بشكل ويُقرأ بآخر (فخ-38 منقولاً إلى الصلاحية).
    /// </para>
    /// </summary>
    private static string Instant(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);

}
