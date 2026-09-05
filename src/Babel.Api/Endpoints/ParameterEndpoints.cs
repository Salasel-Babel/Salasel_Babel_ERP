using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.Parameters;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق لوحة تحكّم المعامِلات.
/// <para>
/// <b>ثلاثةُ أبواب لا أربعة:</b> قراءةُ الإصدارات، وإيداعُ إصدار، وقائمةُ مراجعة
/// المحاسب. <b>ولا بابَ تعديلٍ ولا بابَ حذف</b> — والثابتة مفروضةٌ بغياب العملية لا
/// بفحصٍ عند مستدعٍ: نسبةُ فترةٍ ماضية لا تُعدَّل، والتغييرُ إصدارٌ جديد بتاريخ سريانه.
/// </para>
/// <para>
/// <b>ولا قرار واحد في هذا الملف:</b> لا فهرس مجموعات، ولا حارس نسبة، ولا حكم اعتماد.
/// كلّها في النواة؛ وما هنا قراءةُ نطاق، وقراءةُ جسم، ونقلٌ، وترجمةُ نتيجة (القاعدة 13).
/// </para>
/// </summary>
internal static class ParameterEndpoints
{
    /// <summary>يسجّل نقاط نهاية المعامِلات.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapParameterApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ApiRoutes.Parameters, ListAsync);
        app.MapPost(ApiRoutes.Parameters, DepositAsync);
        app.MapGet(ApiRoutes.ParameterReview, ReviewAsync);
    }

    private static async Task<IResult> ListAsync(
        HttpContext context,
        ParameterSettingsService parameters,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<ParameterVersionView>> result = await parameters
            .ListAsync(new TenantId(companyId), RequestPrincipal.Of(context).User, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ParameterMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DepositAsync(
        HttpContext context,
        ParameterSettingsService parameters,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        ParameterVersionRequestDto? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<ParameterVersionRequestDto>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            return Scope.BadJson(context, exception);
        }

        if (dto is null)
        {
            return HttpProblemResults.Code(
                context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing.");
        }

        ParameterVersionDraft draft;
        try
        {
            draft = ParameterMapping.ToDraft(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ParameterVersionView> result = await parameters
            .DepositAsync(new TenantId(companyId), RequestPrincipal.Of(context).User, draft, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ParameterMapping.ToDto(result.Value), ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReviewAsync(
        HttpContext context,
        ParameterSettingsService parameters,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<ParameterReviewView>> result = await parameters
            .ReviewAsync(new TenantId(companyId), RequestPrincipal.Of(context).User, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ParameterMapping.ToDto(result.Value), ApiJson.Options);
    }
}
