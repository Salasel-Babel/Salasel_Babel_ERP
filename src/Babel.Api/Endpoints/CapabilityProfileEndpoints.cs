using System.Collections.Immutable;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Core.CapabilityProfile;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق ملفّ قدرات المستأجر.
/// <para>
/// <b>ولماذا هذا السطح موجود أصلاً:</b> الشاشة دالّةٌ في (العقد المنشور × الملفّ). فريق
/// الواجهة يقرأ الشكل من هنا ويبني عليه، ولا يؤلّف شاشةً بـJSON حرّ — لأن شاشةً مؤلَّفة
/// باستقلال عن العقد تُرسل حقلاً يرفضه الخادم أو تُسقط حقلاً يطلبه، وهو صنف العطل نفسه
/// الذي استُهلك عليه هذا الشهر: <b>الموضع الذي يجيب عن السؤال ليس الموضع الذي أُصلح</b>.
/// </para>
/// <para>
/// <b>ولا قرار واحد في هذا الملف:</b> لا كتالوج، ولا مطابقة بمصفوفة الترحيل، ولا حكم
/// قبول. كلها في النواة؛ وما هنا قراءة نطاق، وقراءة جسم، ونقل، وترجمة نتيجة.
/// </para>
/// </summary>
internal static class CapabilityProfileEndpoints
{
    /// <summary>يسجّل نقاط نهاية ملفّ القدرات.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapCapabilityProfileApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapGet(ApiRoutes.CapabilityProfile, ReadProfileAsync);
        app.MapPut(ApiRoutes.CapabilityProfile, WriteProfileAsync);
        app.MapGet(ApiRoutes.DocumentShape, ReadShapeAsync);
        app.MapPost(ApiRoutes.DocumentAdmission, AdmitAsync);
    }

    private static async Task<IResult> ReadProfileAsync(
        HttpContext context,
        CapabilityProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<ValidatedCapabilityProfile> result = await profiles
            .GetAsync(new TenantId(companyId), RequestPrincipal.Of(context).User, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CapabilityProfileWire.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> WriteProfileAsync(
        HttpContext context,
        CapabilityProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        PutCapabilityProfileRequestDto? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<PutCapabilityProfileRequestDto>(ApiJson.Options, cancellationToken)
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

        CapabilityProfileDraft draft;
        try
        {
            draft = CapabilityProfileWire.ToDraft(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ValidatedCapabilityProfile> result = await profiles
            .SaveAsync(
                new CapabilityProfileSaveRequest(
                    new TenantId(companyId),
                    RequestPrincipal.Of(context).User,
                    draft,
                    dto.WithdrawalReason),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CapabilityProfileWire.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadShapeAsync(
        HttpContext context,
        CapabilityProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryDocumentType(context, out string documentType, out IResult? malformed))
        {
            return malformed!;
        }

        Result<DocumentShape> result = await profiles
            .GetShapeAsync(
                new TenantId(companyId),
                RequestPrincipal.Of(context).User,
                new DocumentTypeCode(documentType),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CapabilityProfileWire.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AdmitAsync(
        HttpContext context,
        CapabilityProfileService profiles,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryDocumentType(context, out string documentType, out IResult? malformed))
        {
            return malformed!;
        }

        AdmitDocumentRequestDto? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<AdmitDocumentRequestDto>(ApiJson.Options, cancellationToken)
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

        ImmutableArray<string> fields;
        try
        {
            fields = CapabilityProfileWire.ToFields(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<AdmittedDocument> result = await profiles
            .AdmitAsync(
                new TenantId(companyId),
                RequestPrincipal.Of(context).User,
                new DocumentSubmission(new DocumentTypeCode(documentType), fields),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(CapabilityProfileWire.ToDto(result.Value), ApiJson.Options);
    }
}
