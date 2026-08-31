using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Projects.Surface;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق مستندات المقاولات.
/// <para>
/// <b>ما يفعله كل معالج هنا، بالترتيب، ولا شيء غيره:</b> يقرأ نطاق الشركة من المسار
/// ويتحقق أن الاعتماد يبلغه · يقرأ الجسم بالقواعد الصارمة · ينقل الحقول إلى السطح
/// المنشور للوحدة · ينادي · يترجم النتيجة. <b>لا قرار محاسبي واحد يقع في هذا الملف</b>:
/// لا اختيار دور، ولا اختيار حدث، ولا حساب مبلغ، ولا قاعدة توازن، ولا اسم حساب.
/// </para>
/// <para>
/// <b>والاستحقاق ليس هنا — عمداً.</b> كل نقطة دخول في الوحدة تحمل
/// <c>[RequiresEntitlement]</c> وتنادي المنفِّذ قبل أي عمل، والقاعدة 6 تفرض ذلك على IL.
/// وفحصٌ ثانٍ هنا كان سيكون آليةَ تصريحٍ موازية تُصان إحداهما وتُنسى الأخرى.
/// </para>
/// <para>
/// <b>ولاحظ ما ليس في هذا الملفّ: لا <c>PUT</c> ولا <c>PATCH</c> ولا <c>DELETE</c>.</b>
/// ولا <c>MapPost</c> على أمرٍ تغييري أو خطاب ضمان بلاحقة <c>/posting</c> — ولا يجوز أن
/// يوجد: لا حدث لأيٍّ منهما في مصفوفة الترحيل، وبابُ ترحيلٍ على ما لا يُرحَّل خطأٌ
/// محاسبي مكتوبٌ في عقدٍ منشور.
/// </para>
/// </summary>
internal static class ProjectsEndpoints
{
    /// <summary>أقصى طول لوسيط تاريخ في الاستعلام.</summary>
    private const int DateQueryLength = 10;

    /// <summary>يسجّل سطح المقاولات.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapProjectsApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ApiRoutes.Projects, AddProjectAsync);
        app.MapGet(ApiRoutes.Projects, ListProjectsAsync);
        app.MapGet(ApiRoutes.Project, ReadProjectAsync);

        app.MapPost(ApiRoutes.ProjectContracts, AddContractAsync);
        app.MapGet(ApiRoutes.ProjectContract, ReadContractAsync);
        app.MapGet(ApiRoutes.BoqItems, ReadBoqItemsAsync);
        app.MapGet(ApiRoutes.ContractClientCertificates, ReadContractCertificatesAsync);
        app.MapGet(ApiRoutes.ContractChangeOrders, ReadContractChangeOrdersAsync);
        app.MapGet(ApiRoutes.ContractPosition, ReadContractPositionAsync);

        // ‏**بابان لا ثلاثة**: الأمر التغييري التزامٌ تعاقدي لا واقعة محاسبية.
        app.MapPost(ApiRoutes.ChangeOrders, AddChangeOrderAsync);
        app.MapGet(ApiRoutes.ChangeOrder, ReadChangeOrderAsync);

        app.MapPost(ApiRoutes.Subcontractors, AddSubcontractorAsync);
        app.MapGet(ApiRoutes.Subcontractor, ReadSubcontractorAsync);
        app.MapPost(ApiRoutes.Subcontracts, AddSubcontractAsync);
        app.MapGet(ApiRoutes.Subcontract, ReadSubcontractAsync);
        app.MapGet(ApiRoutes.SubcontractLines, ReadSubcontractLinesAsync);

        app.MapPost(ApiRoutes.ClientCertificates, DraftClientCertificateAsync);
        app.MapGet(ApiRoutes.ClientCertificate, ReadClientCertificateAsync);
        app.MapPost(ApiRoutes.ClientCertificatePosting, PostClientCertificateAsync);

        app.MapPost(ApiRoutes.SubcontractorCertificates, DraftSubcontractorCertificateAsync);
        app.MapGet(ApiRoutes.SubcontractorCertificate, ReadSubcontractorCertificateAsync);
        app.MapPost(ApiRoutes.SubcontractorCertificatePosting, PostSubcontractorCertificateAsync);

        app.MapPost(ApiRoutes.SubcontractorAdvances, DraftSubcontractorAdvanceAsync);
        app.MapGet(ApiRoutes.SubcontractorAdvance, ReadSubcontractorAdvanceAsync);
        app.MapPost(ApiRoutes.SubcontractorAdvancePosting, PostSubcontractorAdvanceAsync);

        app.MapPost(ApiRoutes.RetentionReleases, DraftRetentionReleaseAsync);
        app.MapGet(ApiRoutes.RetentionRelease, ReadRetentionReleaseAsync);
        app.MapPost(ApiRoutes.RetentionReleasePosting, PostRetentionReleaseAsync);

        app.MapPost(ApiRoutes.RetentionCollections, DraftRetentionCollectionAsync);
        app.MapGet(ApiRoutes.RetentionCollection, ReadRetentionCollectionAsync);
        app.MapPost(ApiRoutes.RetentionCollectionPosting, PostRetentionCollectionAsync);

        app.MapGet(ApiRoutes.RetentionRegister, ReadRetentionRegisterAsync);
        app.MapGet(ApiRoutes.SubcontractorStatement, ReadSubcontractorStatementAsync);

        // ‏**وبابان لا ثلاثة هنا أيضاً**: خطاب الضمان سجلٌّ لا يُرحَّل أبداً.
        app.MapPost(ApiRoutes.Guarantees, AddGuaranteeAsync);
        app.MapGet(ApiRoutes.Guarantee, ReadGuaranteeAsync);
    }

    private static async Task<IResult> AddProjectAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (ProjectRequestDto? dto, IResult? refused) = await BodyAsync<ProjectRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsProjectRequest request;
        try
        {
            request = ProjectsMapping.ToProjectRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsProject> result = await projects
            .AddProjectAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.Project, companyId, "projectId", result.Value.Id));
    }

    private static async Task<IResult> ListProjectsAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<ProjectsProject>> result = await projects
            .ListProjectsAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadProjectAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "projectId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsProject> result = await projects
            .ReadProjectAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddContractAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (ProjectContractRequestDto? dto, IResult? refused) = await BodyAsync<ProjectContractRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsContractRequest request;
        try
        {
            request = ProjectsMapping.ToContractRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsContract> result = await projects
            .AddContractAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.ProjectContract, companyId, "contractId", result.Value.Id));
    }

    private static async Task<IResult> ReadContractAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "contractId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsContract> result = await projects
            .ReadContractAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadBoqItemsAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "contractId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<ProjectsBoqItem>> result = await projects
            .ReadBoqItemsAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadContractCertificatesAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "contractId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<ProjectsCertificate>> result = await projects
            .ReadClientCertificatesAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadContractChangeOrdersAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "contractId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<ProjectsChangeOrder>> result = await projects
            .ReadChangeOrdersAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadContractPositionAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "contractId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsContractPosition> result = await projects
            .ReadContractPositionAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddChangeOrderAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (ChangeOrderRequestDto? dto, IResult? refused) = await BodyAsync<ChangeOrderRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsChangeOrderRequest request;
        try
        {
            request = ProjectsMapping.ToChangeOrderRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsChangeOrder> result = await projects
            .AddChangeOrderAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.ChangeOrder, companyId, "changeOrderId", result.Value.Id));
    }

    private static async Task<IResult> ReadChangeOrderAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "changeOrderId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsChangeOrder> result = await projects
            .ReadChangeOrderAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddSubcontractorAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (SubcontractorRequestDto? dto, IResult? refused) = await BodyAsync<SubcontractorRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsSubcontractorRequest request;
        try
        {
            request = ProjectsMapping.ToSubcontractorRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsSubcontractor> result = await projects
            .AddSubcontractorAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.Subcontractor, companyId, "subcontractorId", result.Value.Id));
    }

    private static async Task<IResult> ReadSubcontractorAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "subcontractorId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsSubcontractor> result = await projects
            .ReadSubcontractorAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddSubcontractAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (SubcontractRequestDto? dto, IResult? refused) = await BodyAsync<SubcontractRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsSubcontractRequest request;
        try
        {
            request = ProjectsMapping.ToSubcontractRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsSubcontract> result = await projects
            .AddSubcontractAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.Subcontract, companyId, "subcontractId", result.Value.Id));
    }

    private static async Task<IResult> ReadSubcontractAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "subcontractId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsSubcontract> result = await projects
            .ReadSubcontractAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadSubcontractLinesAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "subcontractId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<ProjectsSubcontractLine>> result = await projects
            .ReadSubcontractLinesAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DraftClientCertificateAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (CertificateRequestDto? dto, IResult? refused) = await BodyAsync<CertificateRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsCertificateRequest request;
        try
        {
            request = ProjectsMapping.ToCertificateRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsCertificate> result = await projects
            .DraftClientCertificateAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.ClientCertificate, companyId, "certificateId", result.Value.Id));
    }

    private static async Task<IResult> ReadClientCertificateAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "certificateId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsCertificate> result = await projects
            .ReadClientCertificateAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostClientCertificateAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "certificateId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsCertificate> result = await projects
            .PostClientCertificateAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, result.Value.AlreadyPosted, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.ClientCertificate, companyId, "certificateId", id));
    }

    private static async Task<IResult> DraftSubcontractorCertificateAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (CertificateRequestDto? dto, IResult? refused) = await BodyAsync<CertificateRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsCertificateRequest request;
        try
        {
            request = ProjectsMapping.ToCertificateRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsCertificate> result = await projects
            .DraftSubcontractorCertificateAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.SubcontractorCertificate, companyId, "certificateId", result.Value.Id));
    }

    private static async Task<IResult> ReadSubcontractorCertificateAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "certificateId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsCertificate> result = await projects
            .ReadSubcontractorCertificateAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostSubcontractorCertificateAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "certificateId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsCertificate> result = await projects
            .PostSubcontractorCertificateAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, result.Value.AlreadyPosted, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.SubcontractorCertificate, companyId, "certificateId", id));
    }

    private static async Task<IResult> DraftSubcontractorAdvanceAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (SubcontractorAdvanceRequestDto? dto, IResult? refused) = await BodyAsync<SubcontractorAdvanceRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsAdvanceRequest request;
        try
        {
            request = ProjectsMapping.ToAdvanceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsDocument> result = await projects
            .DraftSubcontractorAdvanceAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.SubcontractorAdvance, companyId, "advanceId", result.Value.Id));
    }

    private static async Task<IResult> ReadSubcontractorAdvanceAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "advanceId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsDocument> result = await projects
            .ReadSubcontractorAdvanceAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostSubcontractorAdvanceAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "advanceId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsDocument> result = await projects
            .PostSubcontractorAdvanceAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, result.Value.AlreadyPosted, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.SubcontractorAdvance, companyId, "advanceId", id));
    }

    private static async Task<IResult> DraftRetentionReleaseAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (RetentionReleaseRequestDto? dto, IResult? refused) = await BodyAsync<RetentionReleaseRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsRetentionReleaseRequest request;
        try
        {
            request = ProjectsMapping.ToReleaseRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsDocument> result = await projects
            .DraftRetentionReleaseAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.RetentionRelease, companyId, "releaseId", result.Value.Id));
    }

    private static async Task<IResult> ReadRetentionReleaseAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "releaseId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsDocument> result = await projects
            .ReadRetentionReleaseAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostRetentionReleaseAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "releaseId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsDocument> result = await projects
            .PostRetentionReleaseAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, result.Value.AlreadyPosted, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.RetentionRelease, companyId, "releaseId", id));
    }

    private static async Task<IResult> DraftRetentionCollectionAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (RetentionCollectionRequestDto? dto, IResult? refused) = await BodyAsync<RetentionCollectionRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsRetentionCollectionRequest request;
        try
        {
            request = ProjectsMapping.ToCollectionRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsDocument> result = await projects
            .DraftRetentionCollectionAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.RetentionCollection, companyId, "collectionId", result.Value.Id));
    }

    private static async Task<IResult> ReadRetentionCollectionAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "collectionId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsDocument> result = await projects
            .ReadRetentionCollectionAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostRetentionCollectionAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "collectionId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsDocument> result = await projects
            .PostRetentionCollectionAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, result.Value.AlreadyPosted, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.RetentionCollection, companyId, "collectionId", id));
    }

    private static async Task<IResult> ReadRetentionRegisterAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        DateOnly asOf;
        try
        {
            asOf = WireMapping.ReadDate(Scope.Query(context, "asOf", required: true, DateQueryLength), "asOf");
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsRetentionRegister> result = await projects
            .ReadRetentionRegisterAsync(new TenantId(companyId), Actor(context), asOf, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadSubcontractorStatementAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        DateOnly asOf;
        try
        {
            asOf = WireMapping.ReadDate(Scope.Query(context, "asOf", required: true, DateQueryLength), "asOf");
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsSubcontractorStatement> result = await projects
            .ReadSubcontractorStatementAsync(new TenantId(companyId), Actor(context), asOf, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddGuaranteeAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (GuaranteeRequestDto? dto, IResult? refused) = await BodyAsync<GuaranteeRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        ProjectsGuaranteeRequest request;
        try
        {
            request = ProjectsMapping.ToGuaranteeRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<ProjectsGuarantee> result = await projects
            .AddGuaranteeAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, ProjectsMapping.ToDto(result.Value), Location(ApiRoutes.Guarantee, companyId, "guaranteeId", result.Value.Id));
    }

    private static async Task<IResult> ReadGuaranteeAsync(
        HttpContext context,
        ProjectsSurface projects,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "guaranteeId", out Guid id, out IResult? malformed))
        {
            return malformed!;
        }

        Result<ProjectsGuarantee> result = await projects
            .ReadGuaranteeAsync(new TenantId(companyId), Actor(context), id, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(ProjectsMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── مشترك ────────────────────────────────────────────────────────────────

    /// <summary>الفاعل من الاعتماد وحده — لا من ترويسة ولا من حقل في الجسم.</summary>
    private static UserId Actor(HttpContext context) => RequestPrincipal.Of(context).User;

    private static async Task<(T? Dto, IResult? Refused)> BodyAsync<T>(
        HttpContext context,
        CancellationToken cancellationToken)
        where T : class
    {
        T? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            return (null, Scope.BadJson(context, exception));
        }

        return dto is null
            ? (null, HttpProblemResults.Code(
                context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing."))
            : (dto, null);
    }

    private static IResult Created(HttpContext context, object dto, string? location)
    {
        if (location is not null)
        {
            context.Response.Headers.Location = location;
        }

        return Results.Json(dto, ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// ‏201 للترحيل الأول و200 للوصول الثاني بالهوية نفسها.
    /// <para>
    /// <b>والفارق مُعلن في الجسم أيضاً</b> بـ<c>alreadyPosted</c>: رمز الحالة وحده يضيع
    /// خلف أي وسيط يعيد التوجيه، وعميلٌ أعاد المحاولة بعد انقطاع شبكة يحتاج أن يعرف
    /// أيّ النداءين رحّل. و<b>الحكم يأتي من بوّابة الترحيل</b> لا من مقارنة حالةٍ على
    /// المستند: المستند قد تتغيّر حالته بغير هذا المسار، والبوّابة وحدها تملك الهوية.
    /// </para>
    /// </summary>
    private static IResult Posted(
        HttpContext context,
        bool alreadyPosted,
        object dto,
        string? location)
    {
        if (location is not null)
        {
            context.Response.Headers.Location = location;
        }

        return Results.Json(
            dto,
            ApiJson.Options,
            statusCode: alreadyPosted ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    private static string Location(string template, Guid companyId, string idName, Guid id) => template
        .Replace("{companyId}", companyId.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{" + idName + "}", id.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
