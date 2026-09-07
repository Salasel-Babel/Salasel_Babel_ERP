using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Ports;
using Babel.Api.Security;
using Babel.Api.Wire;

namespace Babel.Api.Endpoints;

/// <summary>
/// <b>سطحُ كتالوج الخطط — بياناتُ المنصّة لا شيفرتُها (ADR-0092).</b>
/// <para>
/// بابان: قراءةُ الخطط <b>المنشورة</b> لكلّ مصادَق — كي تختار شاشةُ الاشتراك من قائمة الخادم
/// لا من قائمةٍ تكتبها — وسطحُ <b>مشغّل المنصّة</b> الذي يرى الكتالوج كلّه ويُنشئ الخطّة
/// ويُسعّرها وينشرها بسندٍ وسبب، وكلُّ تغييرٍ يُلحَق بسجلّ <c>control.plan_change</c>.
/// ومشغّلُ المنصّة اعتمادٌ يُعلَن في الإعداد (<c>Platform=true</c>) لا دورٌ يُستنتج.
/// </para>
/// </summary>
internal static class PlatformEndpoints
{
    /// <summary>يسجّل المسارات.</summary>
    /// <param name="app">الموجّه.</param>
    public static void MapPlatformApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(TenantRoutes.Plans, ReadPublishedPlansAsync);
        app.MapGet(TenantRoutes.PlatformPlans, ReadAllPlansAsync);
        app.MapPut(TenantRoutes.PlatformPlan, PutPlanAsync);
    }

    private static async Task<IResult> ReadPublishedPlansAsync(
        HttpContext context, IFleetDirectory fleet, CancellationToken cancellationToken)
    {
        if (Unavailable(context, fleet) is { } offline)
        {
            return offline;
        }

        IReadOnlyList<FleetPlan> plans = await fleet.PlansAsync(includeUnpublished: false, cancellationToken).ConfigureAwait(false);
        return Results.Json(new PlanListDto([.. plans.Select(ToDto)]), ApiJson.Options);
    }

    private static async Task<IResult> ReadAllPlansAsync(
        HttpContext context, IFleetDirectory fleet, CancellationToken cancellationToken)
    {
        if (Unavailable(context, fleet) is { } offline)
        {
            return offline;
        }

        if (NotOperator(context) is { } denied)
        {
            return denied;
        }

        IReadOnlyList<FleetPlan> plans = await fleet.PlansAsync(includeUnpublished: true, cancellationToken).ConfigureAwait(false);
        return Results.Json(new PlanListDto([.. plans.Select(ToDto)]), ApiJson.Options);
    }

    private static async Task<IResult> PutPlanAsync(
        HttpContext context, IFleetDirectory fleet, CancellationToken cancellationToken)
    {
        if (Unavailable(context, fleet) is { } offline)
        {
            return offline;
        }

        if (NotOperator(context) is { } denied)
        {
            return denied;
        }

        string code = (context.Request.RouteValues["planCode"]?.ToString() ?? string.Empty).Trim();
        if (code.Length is 0 or > 32 || !code.All(static ch => ch is >= 'A' and <= 'Z' or >= '0' and <= '9' or '_'))
        {
            return HttpProblemResults.Code(
                context,
                "wire.path.malformed",
                "رمز الخطّة في المسار يُكتب بحروفٍ لاتينية كبيرة وأرقام وشرطة سفلية، حتى 32 محرفاً.",
                "The plan code in the path is upper-case ASCII letters, digits and underscore, up to 32 characters.",
                "planCode",
                StatusCodes.Status400BadRequest);
        }

        (PutPlanRequestDto? dto, IResult? refused) =
            await Bodies.ReadAsync<PutPlanRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        if (string.IsNullOrWhiteSpace(dto.Authority) || string.IsNullOrWhiteSpace(dto.ReasonAr))
        {
            return HttpProblemResults.Code(
                context,
                "platform.plan_authority_missing",
                "السند والسبب إلزامان على كلّ تغييرٍ في كتالوج الخطط: سعرٌ يتغيّر بلا سندٍ سعرٌ لا يُحتجّ به.",
                "Authority and reason are mandatory on every plan-catalogue change: a price changed without authority is a price nobody can stand on.",
                "authority",
                StatusCodes.Status422UnprocessableEntity);
        }

        decimal monthly;
        decimal perUser;
        try
        {
            monthly = WireNumbers.ParseStrict(dto.MonthlyPrice, WireNumbers.MoneyScale, "monthlyPrice");
            perUser = WireNumbers.ParseStrict(dto.PerUserPrice, WireNumbers.MoneyScale, "perUserPrice");
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        try
        {
            FleetPlan written = await fleet.PutPlanAsync(
                    new FleetPlanRequest(
                        code,
                        (dto.NameAr ?? string.Empty).Trim(),
                        [.. (dto.NameTranslations ?? []).Select(static entry => new FleetNameTranslation(entry.Name, entry.Value))],
                        monthly,
                        perUser,
                        dto.IncludedUsers,
                        dto.Modules ?? [],
                        dto.Published),
                    ActorOf(context),
                    dto.Authority.Trim(),
                    dto.ReasonAr.Trim(),
                    cancellationToken)
                .ConfigureAwait(false);

            return Results.Json(ToDto(written), ApiJson.Options);
        }
        catch (ArgumentException refusedPlan)
        {
            // ‏رفضُ المجال باسمه: وحدةٌ مجهولة، أو خطّةٌ بلا وحدات، أو سعرٌ سالب.
            return HttpProblemResults.Code(
                context,
                "platform.plan_refused",
                refusedPlan.Message,
                "The plan was refused: " + refusedPlan.Message,
                status: StatusCodes.Status422UnprocessableEntity);
        }
    }

    private static IResult? NotOperator(HttpContext context) =>
        RequestPrincipal.Of(context).IsPlatformOperator
            ? null
            : HttpProblemResults.Code(
                context,
                "platform.operator_required",
                "هذا السطح لمشغّل المنصّة وحده: اعتمادٌ مُعلَنٌ مشغّلاً في الإعداد (Babel:Api:Tokens:N:Platform=true). "
                + "واعتمادُ منشأةٍ — مهما اتّسع — لا يبلغه.",
                "This surface is for the platform operator alone: a credential declared as operator in configuration "
                + "(Babel:Api:Tokens:N:Platform=true). A company credential, however wide, does not reach it.",
                status: StatusCodes.Status403Forbidden);

    private static IResult? Unavailable(HttpContext context, IFleetDirectory fleet) =>
        fleet.IsAvailable
            ? null
            : HttpProblemResults.Code(
                context,
                "fleet.unavailable",
                "مستوى التحكّم غير مُهيَّأ لهذا الخادم، فلا كتالوجَ خطط. وتهيئتُه إعدادُ نشرٍ يُضبط في البيئة.",
                "The control plane is not configured for this server, so there is no plan catalogue. Configuring it is a deployment setting.",
                status: StatusCodes.Status503ServiceUnavailable);

    private static string ActorOf(HttpContext context) =>
        RequestPrincipal.Of(context).User.Value.ToString("D", CultureInfo.InvariantCulture);

    private static PlanDto ToDto(FleetPlan plan) => new(
        plan.Code,
        plan.NameAr,
        [.. plan.Translations.Select(static row => new NameValueDto(row.Tag, row.Name))],
        plan.MonthlyPrice,
        plan.PerUserPrice,
        plan.Currency,
        plan.IncludedUsers,
        plan.Modules,
        plan.Published);
}
