using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.CompanySetup;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق تأسيس المنشأة ومراكز تكلفتها.
/// <para>
/// <b>ولا قرار واحد في هذا الملف:</b> لا حكم على اسم، ولا مدى لعدد الخانات، ولا ثابتة
/// «مركز تكلفة واحد على الأقل»، ولا منعُ تأسيسٍ ثانٍ. كلها في النواة؛ وما هنا قراءة
/// نطاق، وقراءة جسم، ونقل، وترجمة نتيجة (القاعدة 13).
/// </para>
/// <para>
/// <b>ولاحظ ما ليس هنا: فعل حذف.</b> لا على المنشأة ولا على مركز تكلفة — وغيابه بنيوي
/// لا اتفاقي: لا دالة حذف على <c>CostCenterRegister</c> أصلاً.
/// </para>
/// </summary>
internal static class CompanySetupEndpoints
{
    /// <summary>يسجّل نقاط نهاية تأسيس المنشأة.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapCompanySetupApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ApiRoutes.CompanySetup, ReadAsync);
        app.MapPut(ApiRoutes.CompanySetup, InitialiseAsync);
        app.MapPost(ApiRoutes.CostCenters, AddCostCenterAsync);
        app.MapPut(ApiRoutes.CostCenter, RenameCostCenterAsync);
        app.MapPost(ApiRoutes.CostCenterSuspension, SuspendCostCenterAsync);
    }

    private static async Task<IResult> ReadAsync(
        HttpContext context,
        CompanySetupService setups,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<FoundedCompany> result = await setups
            .GetAsync(new TenantId(companyId), RequestPrincipal.Of(context).User, cancellationToken)
            .ConfigureAwait(false);

        return Translate(context, result, StatusCodes.Status200OK);
    }

    private static async Task<IResult> InitialiseAsync(
        HttpContext context,
        CompanySetupService setups,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (InitialiseCompanySetupRequestDto? dto, IResult? refused) =
            await ReadBodyAsync<InitialiseCompanySetupRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        CompanySetupDraft draft;
        try
        {
            draft = CompanySetupWire.ToDraft(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<FoundedCompany> result = await setups
            .InitialiseAsync(
                new CompanyInitialisationRequest(new TenantId(companyId), RequestPrincipal.Of(context).User, draft),
                cancellationToken)
            .ConfigureAwait(false);

        return Translate(context, result, StatusCodes.Status201Created);
    }

    private static async Task<IResult> AddCostCenterAsync(
        HttpContext context,
        CompanySetupService setups,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (CostCenterNameRequestDto? dto, IResult? refused) = await ReadBodyAsync<CostCenterNameRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        IReadOnlyDictionary<string, string> translations;
        try
        {
            translations = CompanySetupWire.ToTranslations(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<FoundedCompany> result = await setups
            .AddCostCenterAsync(
                new TenantId(companyId), RequestPrincipal.Of(context).User, dto.NameAr, translations, cancellationToken)
            .ConfigureAwait(false);

        return Translate(context, result, StatusCodes.Status201Created);
    }

    private static async Task<IResult> RenameCostCenterAsync(
        HttpContext context,
        CompanySetupService setups,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryCostCenterCode(context, out string code, out IResult? malformed))
        {
            return malformed!;
        }

        (CostCenterNameRequestDto? dto, IResult? refused) = await ReadBodyAsync<CostCenterNameRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        IReadOnlyDictionary<string, string> translations;
        try
        {
            translations = CompanySetupWire.ToTranslations(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<FoundedCompany> result = await setups
            .RenameCostCenterAsync(
                new TenantId(companyId),
                RequestPrincipal.Of(context).User,
                new CostCenterCode(code),
                dto.NameAr,
                translations,
                cancellationToken)
            .ConfigureAwait(false);

        return Translate(context, result, StatusCodes.Status200OK);
    }

    private static async Task<IResult> SuspendCostCenterAsync(
        HttpContext context,
        CompanySetupService setups,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryCostCenterCode(context, out string code, out IResult? malformed))
        {
            return malformed!;
        }

        (SuspendCostCenterRequestDto? dto, IResult? refused) = await ReadBodyAsync<SuspendCostCenterRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        Result<FoundedCompany> result = await setups
            .SuspendCostCenterAsync(
                new TenantId(companyId),
                RequestPrincipal.Of(context).User,
                new CostCenterCode(code),
                dto.Reason,
                cancellationToken)
            .ConfigureAwait(false);

        return Translate(context, result, StatusCodes.Status201Created);
    }

    private static IResult Translate(HttpContext context, Result<FoundedCompany> result, int success)
        => result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CompanySetupWire.ToDto(result.Value), ApiJson.Options, statusCode: success);

    private static async Task<(TBody? Body, IResult? Refused)> ReadBodyAsync<TBody>(
        HttpContext context,
        CancellationToken cancellationToken)
        where TBody : class
    {
        TBody? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<TBody>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            return (null, Scope.BadJson(context, exception));
        }

        return dto is null
            ? (null, HttpProblemResults.Code(context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing."))
            : (dto, null);
    }
}
