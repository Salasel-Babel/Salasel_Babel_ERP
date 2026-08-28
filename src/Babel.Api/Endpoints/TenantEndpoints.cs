using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Fleet;
using Babel.Api.Hosting;
using Babel.Api.Ports;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.Access;
using Babel.Core.Entitlement;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق التسجيل الأول ودورة حياة الاشتراك.
/// <para>
/// <b>وهذا الملفّ هو الترجمة، لا القرار</b> (القاعدة 13): لا يقرّر خطّةً، ولا يحسب
/// سعراً، ولا يفرّع على حالة استحقاق. ما هنا: قراءةُ نطاق، وقراءةُ جسم، ونداءٌ على
/// منفذ الأسطول، ونداءٌ على استحقاق المنتَج، وترجمةُ نتيجة. والقرارات كلّها في
/// <c>Babel.ControlPlane</c> و<c>Babel.Core</c>، والخريطة بينهما في
/// <see cref="PlaneTranslation"/>.
/// </para>
/// <para>
/// <b>وثلاثة أبواب من الخمسة أفعالُ مشغِّل لا أفعالُ مستأجر:</b> تغييرُ الخطّة
/// والانقطاعُ والاستئناف تُطلَب باعتماد التزويد وحده — وهو الاعتماد الذي لا عائلة له
/// (ADR-0045 §٣٫٣). والسبب صريح: <b>لا قناة سداد في هذا المنتَج بعد</b>، فبابٌ يستطيع
/// مالكُ المستأجر أن يرفع به خطّته هو بابُ ترقيةٍ مجانية، وبابٌ يستأنف به اشتراكه
/// المنقطع هو إلغاءٌ للانقطاع نفسه. وحين تُبنى قناة السداد يصير تغييرُ الخطّة فعلَ
/// مالكٍ يمرّ بها، ويبقى الانقطاعُ فعلَ مشغِّل.
/// </para>
/// </summary>
internal static class TenantEndpoints
{
    /// <summary>يسجّل نقاط نهاية التسجيل والاشتراك.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapTenantApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(TenantRoutes.Tenants, RegisterTenantAsync);
        app.MapGet(TenantRoutes.Subscription, ReadSubscriptionAsync);
        app.MapPost(TenantRoutes.SubscriptionPlanChanges, ChangePlanAsync);
        app.MapPost(TenantRoutes.SubscriptionLapse, LapseAsync);
        app.MapPost(TenantRoutes.SubscriptionResumption, ResumeAsync);
    }

    // ── التسجيل الأول ────────────────────────────────────────────────────────

    private static async Task<IResult> RegisterTenantAsync(
        HttpContext context,
        IFleetDirectory fleet,
        IEntitlementService entitlements,
        AccessService access,
        OpenDoorRateGuard rate,
        CancellationToken cancellationToken)
    {
        if (Unavailable(context, fleet) is { } offline)
        {
            return offline;
        }

        (RegisterTenantRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<RegisterTenantRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        string key = (dto.RequestKey ?? string.Empty).Trim();

        if (key.Length < SignupIdentity.MinimumKeyLength || key.Length > SignupIdentity.MaximumKeyLength)
        {
            string least = SignupIdentity.MinimumKeyLength.ToString(CultureInfo.InvariantCulture);
            string most = SignupIdentity.MaximumKeyLength.ToString(CultureInfo.InvariantCulture);

            return HttpProblemResults.Code(
                context,
                "signup.request_key_invalid",
                "مفتاح الطلب يجب أن يكون بين " + least + " و" + most + " محرفاً، وأن يكون قيمةً عشوائية "
                + "يولّدها العميل ويحتفظ بها: به وحده تُعاد المحاولة فتردّ المستأجر نفسه بدل أن تُنشئ ثانياً، "
                + "وبقِصَره يصير تخمينُه ممكناً.",
                "The request key must be between " + least + " and " + most + " characters and must be a random "
                + "value the client generates and keeps: it alone makes a retry return the same tenant rather than "
                + "create a second one, and a short one becomes guessable.",
                "requestKey",
                StatusCodes.Status400BadRequest);
        }

        // ── حدّ المعدّل بالمعرّف — بعد قراءة الجسم وقبل أي كتابة ──────────────────
        // والعنوان حُوسب في الوسيط قبل التوجيه. والمفتاحان معاً لا أحدهما: العنوان
        // وحده يُلتَفّ عليه بشبكة عناوين، والمعرّف وحده بتغييره في كل محاولة.
        if (!rate.TryAcquire(TenantRoutes.Tenants, key, out int retryAfter))
        {
            await OpenDoorRateLimiting.RefuseAsync(context, retryAfter).ConfigureAwait(false);
            return Results.Empty;
        }

        string nameAr = (dto.CompanyNameAr ?? string.Empty).Trim();
        string ownerAr = (dto.OwnerNameAr ?? string.Empty).Trim();

        if (nameAr.Length == 0 || ownerAr.Length == 0)
        {
            return HttpProblemResults.Code(
                context,
                "signup.name_missing",
                "اسم المنشأة بالعربية واسم المالك بالعربية إلزامان: العربية هي السجلّ، واسم المالك يظهر في "
                + "قائمة الأعضاء وفي سجلّ التدقيق.",
                "The company's Arabic name and the owner's Arabic name are both mandatory: Arabic is the record, and "
                + "the owner's name appears in the member list and the audit log.",
                "companyNameAr");
        }

        // وترجمات الاسم تعبر **صفوفاً** كما وصلت، ولا يُشتقّ منها نصفٌ ثابت هنا: من
        // يحتاج اسماً لاتينياً هو سجلّ الأسطول وحده، ويقرؤه محوّله عند حدّه (ADR-0021 بند ٢).
        IReadOnlyList<FleetNameTranslation> translations = Rows(dto.NameTranslations);

        SignupIdentity identity = SignupIdentity.Of(key);

        FleetSubscription subscription = await fleet
            .OpenAsync(identity.TenantId, identity.TenantCode, nameAr, translations, cancellationToken)
            .ConfigureAwait(false);

        // الاستحقاق في المنتَج **قبل** فتح العضوية: منح العضوية نداءٌ محروسٌ باستحقاق
        // النواة، ومستأجرٌ لا استحقاق له في هذه العملية يُرفض عنده — فيسقط التسجيل عند
        // خطوته الأخيرة بعد أن كتب في مستوى التحكّم. الترتيب يجعل الإعادة تكمل لا تفشل.
        // ومفتاحان لا واحد: المستأجر، وأول منشأة له. فمسارات المنشأة تسأل الاستحقاق
        // بمعرّف المنشأة، ومسارات المصادقة بمعرّف المستأجر — والكتابة على واحدٍ منهما
        // تُنتج استحقاقاً يبدو مطبَّقاً ونصفُه لا يُقرأ.
        Result applied = await PlaneTranslation
            .ApplyAsync(
                entitlements, subscription, [identity.TenantId, identity.CompanyId],
                UserId.SystemActor, SignupReason, cancellationToken)
            .ConfigureAwait(false);

        if (applied.IsFailure)
        {
            return HttpProblemResults.Domain(context, applied.Errors);
        }

        Result<GrantedMembership> granted = await access
            .GrantMembershipAsync(
                new MembershipGrantRequest(
                    new TenantId(identity.TenantId), identity.CompanyId,
                    new UserId(identity.OwnerId), ownerAr, MembershipRole.Owner,
                    Member: new UserId(identity.OwnerId)),
                cancellationToken)
            .ConfigureAwait(false);

        // ── إعادة الإرسال بالمفتاح نفسه ──────────────────────────────────────────
        // العضوية مُحكَمة بـ(المنشأة، المستخدم)، والمعرّفان مشتقّان من المفتاح — فرفضُ
        // «عضويةٌ قائمة» هو **بعينه** الجواب «سُجِّل هذا المفتاح من قبل». ولا جدول
        // إحكامٍ ثالث يُكتب، ولا نافذة بين كتابتين تُنتج مستأجراً ثانياً.
        if (granted.IsFailure)
        {
            return granted.Errors.Any(static error =>
                string.Equals(error.Code, "membership.already_granted", StringComparison.Ordinal))
                ? await ReplayAsync(context, access, identity, subscription, cancellationToken).ConfigureAwait(false)
                : HttpProblemResults.Domain(context, granted.Errors);
        }

        return Results.Json(
            new RegisteredTenantDto(
                Identifier(identity.TenantId),
                subscription.TenantCode,
                Identifier(identity.CompanyId),
                AlreadyRegistered: false,
                ToDto(granted.Value.Membership),
                granted.Value.Enrolment.Value,
                Instant(granted.Value.Enrolment.ExpiresAt),
                ToDto(subscription)),
            ApiJson.Options,
            statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// يردّ تسجيلاً سابقاً بالمفتاح نفسه: <c>200</c> لا <c>201</c>، والمعرّفات نفسها،
    /// و<b>بلا اعتماد ثانٍ</b>.
    /// <para>
    /// وهو الشكل نفسه الذي يأخذه ترحيلٌ مُحكَم على هذا السطح: الإيصال ذاته و<c>200</c>
    /// بدل <c>201</c>، ولا مورد ثانٍ يُنشَأ.
    /// </para>
    /// </summary>
    private static async Task<IResult> ReplayAsync(
        HttpContext context,
        AccessService access,
        SignupIdentity identity,
        FleetSubscription subscription,
        CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<Membership>> members = await access
            .ListMembershipsAsync(
                new TenantId(identity.TenantId), new UserId(identity.OwnerId), identity.CompanyId, cancellationToken)
            .ConfigureAwait(false);

        if (members.IsFailure)
        {
            return HttpProblemResults.Domain(context, members.Errors);
        }

        Membership? owner = members.Value.FirstOrDefault(membership => membership.User.Value == identity.OwnerId);

        if (owner is null)
        {
            // العضوية موجودة بحكم الرفض الذي قادنا إلى هنا، فغيابها من القائمة يعني
            // أنها سُحبت بينهما. والصدق أولى من اختراع مالك: يُقال ذلك برمزه.
            return HttpProblemResults.Domain(context, [AccessErrors.MembershipNotFound]);
        }

        return Results.Json(
            new RegisteredTenantDto(
                Identifier(identity.TenantId),
                subscription.TenantCode,
                Identifier(identity.CompanyId),
                AlreadyRegistered: true,
                ToDto(owner),
                EnrolmentCredential: null,
                EnrolmentExpiresAt: null,
                ToDto(subscription)),
            ApiJson.Options,
            statusCode: StatusCodes.Status200OK);
    }

    // ── الاشتراك ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ReadSubscriptionAsync(
        HttpContext context,
        IFleetDirectory fleet,
        CancellationToken cancellationToken)
    {
        if (Unavailable(context, fleet) is { } offline)
        {
            return offline;
        }

        if (!TryTenant(context, out Guid tenantId, out IResult? denied))
        {
            return denied!;
        }

        FleetSubscription? subscription = await fleet.FindAsync(tenantId, cancellationToken).ConfigureAwait(false);

        return subscription is null
            ? NotSubscribed(context)
            : Results.Json(ToDto(subscription), ApiJson.Options);
    }

    private static async Task<IResult> ChangePlanAsync(
        HttpContext context,
        IFleetDirectory fleet,
        IEntitlementService entitlements,
        AccessService access,
        CancellationToken cancellationToken)
    {
        if (!TryOperator(context, fleet, out Guid tenantId, out IResult? denied))
        {
            return denied!;
        }

        (ChangePlanRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<ChangePlanRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        string plan = (dto.PlanCode ?? string.Empty).Trim();

        if (!fleet.KnownPlans.Contains(plan, StringComparer.Ordinal))
        {
            return HttpProblemResults.Code(
                context,
                "subscription.plan_unknown",
                $"الخطّة «{plan}» ليست من الخطط المعروفة. المعروف: {string.Join(" · ", fleet.KnownPlans)}.",
                $"The plan '{plan}' is not one of the known plans. Known: {string.Join(", ", fleet.KnownPlans)}.",
                "planCode");
        }

        if (Incomplete(context, dto.Authority, dto.ReasonAr) is { } missing)
        {
            return missing;
        }

        return await TransitionAsync(
                context,
                entitlements,
                access,
                () => fleet.ChangePlanAsync(
                    tenantId, plan, ActorOf(context), dto.Authority!.Trim(), dto.ReasonAr!.Trim(), cancellationToken),
                "تغيير الخطّة من سطح الاشتراك / plan change from the subscription surface",
                cancellationToken)
            .ConfigureAwait(false);
    }

    private static async Task<IResult> LapseAsync(
        HttpContext context,
        IFleetDirectory fleet,
        IEntitlementService entitlements,
        AccessService access,
        CancellationToken cancellationToken) =>
        await TransitionWithAuthorityAsync(
                context,
                fleet,
                entitlements,
                access,
                (tenantId, dto) => fleet.LapseAsync(
                    tenantId, ActorOf(context), dto.Authority!.Trim(), dto.ReasonAr!.Trim(), cancellationToken),
                "انقطاع الاشتراك من سطح الاشتراك / subscription lapse from the subscription surface",
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<IResult> ResumeAsync(
        HttpContext context,
        IFleetDirectory fleet,
        IEntitlementService entitlements,
        AccessService access,
        CancellationToken cancellationToken) =>
        await TransitionWithAuthorityAsync(
                context,
                fleet,
                entitlements,
                access,
                (tenantId, dto) => fleet.ResumeAsync(
                    tenantId, ActorOf(context), dto.Authority!.Trim(), dto.ReasonAr!.Trim(), cancellationToken),
                "استئناف الاشتراك من سطح الاشتراك / subscription resumption from the subscription surface",
                cancellationToken)
            .ConfigureAwait(false);

    private static async Task<IResult> TransitionWithAuthorityAsync(
        HttpContext context,
        IFleetDirectory fleet,
        IEntitlementService entitlements,
        AccessService access,
        Func<Guid, SubscriptionTransitionRequestDto, Task<FleetSubscription>> act,
        string reason,
        CancellationToken cancellationToken)
    {
        if (!TryOperator(context, fleet, out Guid tenantId, out IResult? denied))
        {
            return denied!;
        }

        (SubscriptionTransitionRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<SubscriptionTransitionRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        if (Incomplete(context, dto.Authority, dto.ReasonAr) is { } missing)
        {
            return missing;
        }

        return await TransitionAsync(
                context, entitlements, access, () => act(tenantId, dto), reason, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// ينفّذ انتقالاً على الاشتراك ثم <b>يُنزله على استحقاق المنتَج في العملية نفسها</b>.
    /// <para>
    /// والنداءان معاً لا أحدهما: بلا الثاني يبقى صفّ الاشتراك يقول <c>Lapsed</c>
    /// والخادم يقبل الترحيل — أي انقطاعٌ <b>يبدو منفَّذاً</b>، وهو أسوأ من غيابه.
    /// </para>
    /// </summary>
    private static async Task<IResult> TransitionAsync(
        HttpContext context,
        IEntitlementService entitlements,
        AccessService access,
        Func<Task<FleetSubscription>> act,
        string reason,
        CancellationToken cancellationToken)
    {
        FleetSubscription subscription = await act().ConfigureAwait(false);

        ApiPrincipal principal = RequestPrincipal.Of(context);
        TenantId tenant = new(subscription.TenantId);

        // منشآت المستأجر تُقرأ **بعد** التغيير وقبل إنزاله: مفتاحا الاستحقاق مفتاحان،
        // والكتابة عليهما معاً هي ما يجعل الانقطاع فعلاً لا صفّاً.
        Result<IReadOnlyList<Guid>> companies = await access
            .CompaniesOfAsync(tenant, principal.User, cancellationToken)
            .ConfigureAwait(false);

        if (companies.IsFailure)
        {
            return HttpProblemResults.Domain(context, companies.Errors);
        }

        Result applied = await PlaneTranslation
            .ApplyAsync(
                entitlements, subscription, [subscription.TenantId, .. companies.Value],
                principal.User, reason, cancellationToken)
            .ConfigureAwait(false);

        return applied.IsFailure
            ? HttpProblemResults.Domain(context, applied.Errors)
            : Results.Json(ToDto(subscription), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    // ── النطاق والصلاحية ─────────────────────────────────────────────────────

    /// <summary>
    /// يقرأ نطاق المستأجر من المسار ويطابقه بالاعتماد.
    /// <para>
    /// <b>واعتماد التزويد يبلغ كل مستأجر</b> — وهو الاعتماد الذي لا عائلة له، ودورُه
    /// مُسمّى منذ ADR-0045 §٣٫٣: بابُ الإقلاع الأسطولي. ومن عداه لا يبلغ إلا مستأجره،
    /// والرفض <b>واحدٌ لا يُفرَّق فيه</b> «لا وجود له» عن «ليس مستأجرك»: التمييز بينهما
    /// يجعل السطح عدّاد وجود لمستأجرين آخرين.
    /// </para>
    /// </summary>
    private static bool TryTenant(HttpContext context, out Guid tenantId, out IResult? denied)
    {
        denied = null;
        tenantId = Guid.Empty;

        string raw = context.Request.RouteValues.TryGetValue("tenantId", out object? value)
            ? value?.ToString() ?? string.Empty
            : string.Empty;

        if (!Guid.TryParseExact(raw, "D", out tenantId) || tenantId == Guid.Empty)
        {
            denied = HttpProblemResults.Code(
                context,
                "tenancy.tenant_id_malformed",
                "معرّف المستأجر في المسار ليس معرّفاً صالحاً بصيغة 8-4-4-4-12.",
                "The tenant identifier in the path is not a valid 8-4-4-4-12 identifier.",
                "tenantId",
                StatusCodes.Status400BadRequest);
            return false;
        }

        ApiPrincipal principal = RequestPrincipal.Of(context);

        if (principal.Session is null || principal.Tenant.Value == tenantId)
        {
            return true;
        }

        denied = HttpProblemResults.Code(
            context,
            "tenancy.tenant_out_of_scope",
            "هذا الاعتماد لا يبلغ المستأجر المطلوب.",
            "This credential does not reach the requested tenant.",
            "tenantId");
        return false;
    }

    /// <summary>
    /// يشترط <b>اعتماد التزويد</b> على أفعال الاشتراك الثلاثة.
    /// <para>
    /// والسبب مكتوبٌ في ترويسة هذا الملفّ: لا قناة سداد بعد، فتغييرُ خطّةٍ أو استئنافُ
    /// اشتراكٍ منقطع بيد صاحبه هو منحُ نفسه ما لم يُدفع ثمنه. وردُّ <c>403</c> برمز
    /// مستقلّ يقول ذلك، فلا يُقرأ عطلاً في الاعتماد.
    /// </para>
    /// </summary>
    private static bool TryOperator(
        HttpContext context, IFleetDirectory fleet, out Guid tenantId, out IResult? denied)
    {
        tenantId = Guid.Empty;

        if (Unavailable(context, fleet) is { } offline)
        {
            denied = offline;
            return false;
        }

        if (!TryTenant(context, out tenantId, out denied))
        {
            return false;
        }

        if (RequestPrincipal.Of(context).Session is null)
        {
            return true;
        }

        denied = HttpProblemResults.Code(
            context,
            "subscription.operator_credential_required",
            "تغييرُ الخطّة والانقطاعُ والاستئناف أفعالُ مشغِّل، وتُطلَب باعتماد التزويد وحده. ولا قناة سداد "
            + "في هذا المنتَج بعد، فبابٌ يرفع به صاحبُ الاشتراك خطّته هو ترقيةٌ بلا ثمن، وبابٌ يستأنف به "
            + "اشتراكه المنقطع هو إلغاءٌ للانقطاع نفسه. واشتراكك يُقرأ من هنا بلا قيد.",
            "Changing the plan, lapsing, and resuming are operator acts requested with the provisioning credential "
            + "alone. This product has no payment channel yet, so a door letting a subscriber raise their own plan is "
            + "a free upgrade, and one letting them resume their own lapsed subscription undoes the lapse itself. "
            + "Reading your subscription from here is unrestricted.",
            status: StatusCodes.Status403Forbidden);
        return false;
    }

    private static IResult? Unavailable(HttpContext context, IFleetDirectory fleet) =>
        fleet.IsAvailable
            ? null
            : HttpProblemResults.Code(
                context,
                "fleet.unavailable",
                "مستوى التحكّم غير مُهيَّأ لهذا الخادم، فلا يُقرأ اشتراك ولا يُفتح. وسائر السطح يعمل: "
                + "هذا البابُ وحده معطّل، لا الخدمة. وتهيئتُه إعدادُ نشرٍ يُضبط في البيئة.",
                "The control plane is not configured for this server, so no subscription can be read or opened. The "
                + "rest of the surface works: this door alone is disabled, not the service. Configuring it is a "
                + "deployment setting read from the environment.",
                status: StatusCodes.Status503ServiceUnavailable);

    private static IResult NotSubscribed(HttpContext context) => HttpProblemResults.Code(
        context,
        "subscription.not_found",
        "لا اشتراك لهذا المستأجر في سجل الأسطول. ومستأجرٌ يعمل بلا صفّ اشتراك حالةٌ ممكنة على نشرٍ "
        + "بُذر استحقاقه من الإعداد؛ وقولُ ذلك أصدق من اختراع خطّة.",
        "No subscription exists for this tenant in the fleet registry. A tenant running with entitlement seeded from "
        + "configuration and no subscription row is a possible state; saying so is more truthful than inventing a plan.",
        status: StatusCodes.Status404NotFound);

    private static IResult? Incomplete(HttpContext context, string? authority, string? reasonAr) =>
        string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(reasonAr)
            ? HttpProblemResults.Code(
                context,
                "subscription.authority_missing",
                "السند والسبب إلزامان على كل تغيير اشتراك: رقم عقد، أو حدث سداد، أو تذكرة، أو قرار مُوثَّق. "
                + "والاستحقاق يحكم أي بيانات مالية يجوز إنشاؤها، فتغييره حدثٌ تدقيقي لا إعداد واجهة.",
                "Authority and reason are mandatory on every subscription change: a contract number, a payment event, "
                + "a ticket, or a documented decision. Entitlement governs which financial data may be created, so "
                + "changing it is an audit event, not a UI setting.",
                "authority")
            : null;

    /// <summary>يقرأ صفوف الترجمة الواصلة على السلك إلى مفردات المنفذ — صفّاً بصفّ.</summary>
    /// <param name="entries">الصفوف كما وصلت، وقد تغيب.</param>
    private static IReadOnlyList<FleetNameTranslation> Rows(IReadOnlyList<NameValueDto>? entries) =>
        [.. (entries ?? []).Select(static entry => new FleetNameTranslation(entry.Name, entry.Value))];


    private static string ActorOf(HttpContext context) =>
        Identifier(RequestPrincipal.Of(context).User.Value);

    private const string SignupReason =
        "استحقاق خطّة الدخول عند التسجيل الأول / entry-plan entitlement at first registration";

    // ── الترجمة إلى السلك ────────────────────────────────────────────────────

    private static SubscriptionDto ToDto(FleetSubscription subscription) => new(
        Identifier(subscription.TenantId),
        subscription.TenantCode,
        subscription.NameAr,
        subscription.TenantStatus,
        subscription.SubscriptionId,
        subscription.PlanCode,
        subscription.PlanNameAr,
        subscription.MonthlyPrice,
        subscription.PerUserPrice,
        subscription.IncludedUsers,
        subscription.Currency,
        subscription.StartedOn,
        subscription.EndsOn,
        subscription.State,
        subscription.RenewsOn,
        [.. subscription.Modules.Select(static module =>
            new SubscriptionModuleDto(module.Code, module.NameAr, module.State, module.PostsJournal))]);

    private static MembershipDto ToDto(Membership membership) => new(
        Identifier(membership.User.Value),
        membership.DisplayNameAr,
        membership.Role.ToString(),
        Instant(membership.GrantedAt));

    private static string Identifier(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string Instant(DateTimeOffset value) =>
        value.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture);
}
