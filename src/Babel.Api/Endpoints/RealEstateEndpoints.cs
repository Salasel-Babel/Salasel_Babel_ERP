using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.RealEstate.Surface;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق دورة العقارات: العقار · الوحدة · الطرف · العقد · الفاتورة · التحصيل.
/// <para>
/// <b>ما يفعله كل معالج هنا، بالترتيب، ولا شيء غيره:</b> يقرأ نطاق الشركة من المسار
/// ويتحقق أن الاعتماد يبلغه · يقرأ الجسم بالقواعد الصارمة · ينقل الحقول إلى السطح
/// المنشور للوحدة · ينادي · يترجم النتيجة. <b>ولا قرار محاسبي واحد يقع في هذا الملف:</b>
/// لا اختيار حدث، ولا اختيار دور، ولا حساب مبلغ، ولا اسم حساب. وبخاصةٍ هنا: <b>نموذج
/// ملكية العقار لا يُقرأ من الطلب</b> — الوحدة تقرؤه من سجلّ الدفتر وتختار الحدث منه.
/// </para>
/// <para>
/// <b>ولاحظ ما ليس في هذا الملفّ:</b> لا <c>PUT</c> ولا <c>PATCH</c> ولا <c>DELETE</c>،
/// و<b>لا بوّابة ترحيل على عقد الإيجار</b> — والغياب هنا قرارٌ مقروء من شكل السطح:
/// حدث توقيع العقد مُعلَنٌ في المصفوفة بـ<c>posts_entry=false</c>.
/// </para>
/// </summary>
internal static class RealEstateEndpoints
{
    /// <summary>أقصى طول لوسيط تاريخ في الاستعلام.</summary>
    private const int DateQueryLength = 10;

    /// <summary>يسجّل سطح العقارات.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapRealEstateApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ApiRoutes.Properties, AddPropertyAsync);
        app.MapGet(ApiRoutes.Property, ReadPropertyAsync);
        app.MapPost(ApiRoutes.PropertyUnits, AddUnitAsync);
        app.MapGet(ApiRoutes.Unit, ReadUnitAsync);

        app.MapPost(ApiRoutes.Lessees, AddLesseeAsync);
        app.MapGet(ApiRoutes.Lessee, ReadLesseeAsync);
        app.MapPost(ApiRoutes.PropertyOwners, AddOwnerAsync);
        app.MapGet(ApiRoutes.PropertyOwner, ReadOwnerAsync);

        // ‏**عقد الإيجار ثلاثة أبواب لا أربعة**: لا `…/posting` عليه ولا يجوز أن يوجد.
        app.MapPost(ApiRoutes.LeaseContracts, DraftLeaseAsync);
        app.MapGet(ApiRoutes.LeaseContract, ReadLeaseAsync);
        app.MapGet(ApiRoutes.LeaseContractSchedule, ReadScheduleAsync);
        app.MapPost(ApiRoutes.LeaseContractActivation, ActivateLeaseAsync);

        app.MapPost(ApiRoutes.RentInvoices, DraftRentInvoiceAsync);
        app.MapGet(ApiRoutes.RentInvoice, ReadRentInvoiceAsync);
        app.MapPost(ApiRoutes.RentInvoicePosting, PostRentInvoiceAsync);

        app.MapPost(ApiRoutes.TenantReceipts, DraftReceiptAsync);
        app.MapGet(ApiRoutes.TenantReceipt, ReadReceiptAsync);
        app.MapPost(ApiRoutes.TenantReceiptPosting, PostReceiptAsync);
        app.MapPost(ApiRoutes.TenantReceiptAllocation, AllocateReceiptAsync);

        app.MapGet(ApiRoutes.TenantArrearsAging, ArrearsAsync);
    }

    private static async Task<IResult> AddPropertyAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (PropertyRequestDto? dto, IResult? refused) =
            await BodyAsync<PropertyRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        RealEstatePropertyRequest request;
        try
        {
            request = RealEstateMapping.ToPropertyRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<RealEstateProperty> result = await realEstate
            .AddPropertyAsync(new TenantId(companyId), Actor(context), companyId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, RealEstateMapping.ToDto(result.Value),
                Location(ApiRoutes.Property, companyId, "propertyId", result.Value.Id));
    }

    private static async Task<IResult> ReadPropertyAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "propertyId", out Guid propertyId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateProperty> result = await realEstate
            .ReadPropertyAsync(new TenantId(companyId), Actor(context), companyId, propertyId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddUnitAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "propertyId", out Guid propertyId, out IResult? malformed))
        {
            return malformed!;
        }

        (UnitRequestDto? dto, IResult? refused) =
            await BodyAsync<UnitRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        RealEstateUnitRequest request;
        try
        {
            request = RealEstateMapping.ToUnitRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<RealEstateUnit> result = await realEstate
            .AddUnitAsync(new TenantId(companyId), Actor(context), companyId, propertyId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, RealEstateMapping.ToDto(result.Value),
                Location(ApiRoutes.Unit, companyId, "unitId", result.Value.Id));
    }

    private static async Task<IResult> ReadUnitAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "unitId", out Guid unitId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateUnit> result = await realEstate
            .ReadUnitAsync(new TenantId(companyId), Actor(context), companyId, unitId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static Task<IResult> AddLesseeAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
        => AddPartyAsync(context, realEstate, ApiRoutes.Lessee, "lesseeId", lessee: true, cancellationToken);

    private static Task<IResult> AddOwnerAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
        => AddPartyAsync(context, realEstate, ApiRoutes.PropertyOwner, "ownerId", lessee: false, cancellationToken);

    private static async Task<IResult> AddPartyAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        string template,
        string idName,
        bool lessee,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (RealEstatePartyRequestDto? dto, IResult? refused) =
            await BodyAsync<RealEstatePartyRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        RealEstatePartyRequest request;
        try
        {
            request = RealEstateMapping.ToPartyRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        TenantId tenant = new(companyId);
        UserId actor = Actor(context);

        Result<RealEstateParty> result = lessee
            ? await realEstate.AddLesseeAsync(tenant, actor, companyId, request, cancellationToken).ConfigureAwait(false)
            : await realEstate.AddOwnerAsync(tenant, actor, companyId, request, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, RealEstateMapping.ToDto(result.Value),
                Location(template, companyId, idName, result.Value.Id));
    }

    private static Task<IResult> ReadLesseeAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
        => ReadPartyAsync(context, realEstate, "lesseeId", lessee: true, cancellationToken);

    private static Task<IResult> ReadOwnerAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
        => ReadPartyAsync(context, realEstate, "ownerId", lessee: false, cancellationToken);

    private static async Task<IResult> ReadPartyAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        string idName,
        bool lessee,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, idName, out Guid partyId, out IResult? malformed))
        {
            return malformed!;
        }

        TenantId tenant = new(companyId);
        UserId actor = Actor(context);

        Result<RealEstateParty> result = lessee
            ? await realEstate.ReadLesseeAsync(tenant, actor, companyId, partyId, cancellationToken).ConfigureAwait(false)
            : await realEstate.ReadOwnerAsync(tenant, actor, companyId, partyId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DraftLeaseAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (LeaseRequestDto? dto, IResult? refused) =
            await BodyAsync<LeaseRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        RealEstateLeaseRequest request;
        try
        {
            request = RealEstateMapping.ToLeaseRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<RealEstateLease> result = await realEstate
            .DraftLeaseAsync(new TenantId(companyId), Actor(context), companyId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, RealEstateMapping.ToDto(result.Value),
                Location(ApiRoutes.LeaseContract, companyId, "leaseId", result.Value.Id));
    }

    private static async Task<IResult> ReadLeaseAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "leaseId", out Guid leaseId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateLease> result = await realEstate
            .ReadLeaseAsync(new TenantId(companyId), Actor(context), companyId, leaseId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadScheduleAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "leaseId", out Guid leaseId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<RealEstateScheduleLine>> result = await realEstate
            .ReadScheduleAsync(new TenantId(companyId), Actor(context), companyId, leaseId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(leaseId, result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ActivateLeaseAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "leaseId", out Guid leaseId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateLease> result = await realEstate
            .ActivateLeaseAsync(new TenantId(companyId), Actor(context), companyId, leaseId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, RealEstateMapping.ToDto(result.Value), location: null);
    }

    private static async Task<IResult> DraftRentInvoiceAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (RentInvoiceRequestDto? dto, IResult? refused) =
            await BodyAsync<RentInvoiceRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        RealEstateRentInvoiceRequest request;
        try
        {
            request = RealEstateMapping.ToRentInvoiceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<RealEstateRentInvoice> result = await realEstate
            .DraftRentInvoiceAsync(new TenantId(companyId), Actor(context), companyId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, RealEstateMapping.ToDto(result.Value),
                Location(ApiRoutes.RentInvoice, companyId, "invoiceId", result.Value.Id));
    }

    private static async Task<IResult> ReadRentInvoiceAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "invoiceId", out Guid invoiceId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateRentInvoice> result = await realEstate
            .ReadRentInvoiceAsync(new TenantId(companyId), Actor(context), companyId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostRentInvoiceAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "invoiceId", out Guid invoiceId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateRentInvoice> result = await realEstate
            .PostRentInvoiceAsync(new TenantId(companyId), Actor(context), companyId, invoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        RentInvoiceDto dto = RealEstateMapping.ToDto(result.Value);
        return Posted(context, dto, dto.AlreadyPosted);
    }

    private static async Task<IResult> DraftReceiptAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (TenantReceiptRequestDto? dto, IResult? refused) =
            await BodyAsync<TenantReceiptRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        RealEstateReceiptRequest request;
        try
        {
            request = RealEstateMapping.ToReceiptRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<RealEstateReceipt> result = await realEstate
            .DraftReceiptAsync(new TenantId(companyId), Actor(context), companyId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, RealEstateMapping.ToDto(result.Value),
                Location(ApiRoutes.TenantReceipt, companyId, "receiptId", result.Value.Id));
    }

    private static async Task<IResult> ReadReceiptAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "receiptId", out Guid receiptId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateReceipt> result = await realEstate
            .ReadReceiptAsync(new TenantId(companyId), Actor(context), companyId, receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostReceiptAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "receiptId", out Guid receiptId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<RealEstateReceipt> result = await realEstate
            .PostReceiptAsync(new TenantId(companyId), Actor(context), companyId, receiptId, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        TenantReceiptDto dto = RealEstateMapping.ToDto(result.Value);
        return Posted(context, dto, dto.AlreadyPosted);
    }

    private static async Task<IResult> AllocateReceiptAsync(
        HttpContext context,
        RealEstateSurface realEstate,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "receiptId", out Guid receiptId, out IResult? malformed))
        {
            return malformed!;
        }

        (AllocationRequestDto? dto, IResult? refused) =
            await BodyAsync<AllocationRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        RealEstateAllocationRequest request;
        try
        {
            request = RealEstateMapping.ToAllocationRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<RealEstateReceipt> result = await realEstate
            .AllocateReceiptAsync(new TenantId(companyId), Actor(context), companyId, receiptId, request, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        TenantReceiptDto dto2 = RealEstateMapping.ToDto(result.Value);
        return Posted(context, dto2, dto2.AlreadyPosted);
    }

    private static async Task<IResult> ArrearsAsync(
        HttpContext context,
        RealEstateSurface realEstate,
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

        Result<RealEstateArrears> result = await realEstate
            .ReadArrearsAsync(new TenantId(companyId), Actor(context), companyId, asOf, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(RealEstateMapping.ToDto(result.Value), ApiJson.Options);
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
    /// أيّ النداءين رحّل.
    /// </para>
    /// </summary>
    private static IResult Posted(HttpContext context, object dto, bool alreadyPosted)
    {
        ArgumentNullException.ThrowIfNull(context);

        return Results.Json(
            dto,
            ApiJson.Options,
            statusCode: alreadyPosted ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    private static string Location(string template, Guid companyId, string idName, Guid id) => template
        .Replace("{companyId}", companyId.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{" + idName + "}", id.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
