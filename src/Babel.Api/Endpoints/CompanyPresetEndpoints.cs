using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.Presets;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق ثوابت الشركة وترقيم مستنداتها.
/// <para>
/// <b>ولا قرار واحد في هذا الملف:</b> الكتالوج المغلق وأنواع القيم وتركيب الرقم كلّها في
/// النواة؛ وما هنا قراءة نطاق، وقراءة جسم، ونقل، وترجمة نتيجة (القاعدة 13).
/// </para>
/// </summary>
internal static class CompanyPresetEndpoints
{
    /// <summary>يسجّل نقاط نهاية ثوابت الشركة.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapCompanyPresetApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ApiRoutes.CompanyPresets, ReadAsync);
        app.MapPut(ApiRoutes.CompanyPresets, ReplaceAsync);
        app.MapPost(ApiRoutes.CompanyPresetNumbers, AllocateNumberAsync);
    }

    private static async Task<IResult> ReadAsync(
        HttpContext context,
        CompanyPresetService presets,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<CompanyPresets> result = await presets
            .GetAsync(new TenantId(companyId), RequestPrincipal.Of(context).User, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CompanyPresetWire.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReplaceAsync(
        HttpContext context,
        CompanyPresetService presets,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (ReplaceCompanyPresetsRequestDto? dto, IResult? refused) =
            await ReadBodyAsync<ReplaceCompanyPresetsRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        IReadOnlyList<KeyValuePair<string, string>> values;
        try
        {
            values = CompanyPresetWire.ToValues(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<CompanyPresets> result = await presets
            .ReplaceAsync(
                new CompanyPresetReplacement(new TenantId(companyId), RequestPrincipal.Of(context).User, values),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CompanyPresetWire.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AllocateNumberAsync(
        HttpContext context,
        CompanyPresetService presets,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TrySeries(context, out string series, out IResult? malformed))
        {
            return malformed!;
        }

        (AllocateNumberRequestDto? dto, IResult? refused) =
            await ReadBodyAsync<AllocateNumberRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        DateOnly on;
        try
        {
            on = CompanyPresetWire.ToDate(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<AllocatedNumber> result = await presets
            .AllocateNumberAsync(new TenantId(companyId), RequestPrincipal.Of(context).User, series, on, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CompanyPresetWire.ToDto(result.Value), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

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
