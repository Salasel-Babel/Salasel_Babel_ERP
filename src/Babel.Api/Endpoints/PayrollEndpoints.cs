using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Hr.Surface;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق وحدة الموارد البشرية.
/// <para>
/// <b>ما يفعله كل معالج هنا، بالترتيب، ولا شيء غيره:</b> يقرأ نطاق الشركة من المسار
/// ويتحقق أن الاعتماد يبلغه · يقرأ الجسم بالقواعد الصارمة · ينقل الحقول إلى السطح
/// المنشور للوحدة · ينادي · يترجم النتيجة. <b>لا قرار محاسبي واحد يقع في هذا الملف</b>،
/// ولا نسبة، ولا سقف أجر، ولا معادلة، ولا اسم حساب.
/// </para>
/// <para>
/// <b>ولاحظ ما ليس في هذا الملفّ:</b> لا <c>PUT</c> ولا <c>PATCH</c> ولا <c>DELETE</c>؛
/// ولا <c>MapPost</c> على <c>/employee-advances/{advanceId}/posting</c> — حدثُه غير
/// موجود في مصفوفة الترحيل والمحرك يرفض رمزاً لا يعرفه؛ ولا باب إجازات — لا حساب لها
/// ولا دور ولا حدث في بيانات المصفوفة كلّها؛ ولا باب لملفّ حماية الأجور — مواصفته غير
/// متحقَّق منها، ومخزنُ المرفقات المنشور يقبل مجموعة أنواع محتوى مغلقة ليس فيها نوع
/// نصّي، وتوسيعُها تغييرٌ في مجموعة مغلقة منشورة.
/// </para>
/// </summary>
internal static class PayrollEndpoints
{
    /// <summary>أقصى طول لوسيط تاريخ في الاستعلام.</summary>
    private const int DateQueryLength = 10;

    /// <summary>يسجّل سطح الموارد البشرية.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapPayrollApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ApiRoutes.Employees, RegisterEmployeeAsync);
        app.MapGet(ApiRoutes.Employee, ReadEmployeeAsync);
        app.MapPost(ApiRoutes.EmployeeTermination, TerminateEmployeeAsync);

        app.MapPost(ApiRoutes.PayComponents, AddPayComponentAsync);
        app.MapGet(ApiRoutes.PayComponents, ListPayComponentsAsync);
        app.MapPost(ApiRoutes.PayElements, AddPayElementAsync);
        app.MapGet(ApiRoutes.PayElements, ListPayElementsAsync);

        app.MapPost(ApiRoutes.PayrollSettings, DepositPayrollSettingsAsync);
        app.MapGet(ApiRoutes.PayrollSettings, ListPayrollSettingsAsync);

        app.MapPost(ApiRoutes.PayrollRuns, DraftPayrollRunAsync);
        app.MapGet(ApiRoutes.PayrollRun, ReadPayrollRunAsync);
        app.MapGet(ApiRoutes.PayrollRunPayslips, ListPayslipsAsync);
        app.MapPost(ApiRoutes.PayrollRunPosting, PostPayrollRunAsync);
        app.MapGet(ApiRoutes.Payslip, ReadPayslipAsync);

        app.MapPost(ApiRoutes.PayrollPayments, DraftPayrollPaymentAsync);
        app.MapGet(ApiRoutes.PayrollPayment, ReadPayrollPaymentAsync);
        app.MapPost(ApiRoutes.PayrollPaymentPosting, PostPayrollPaymentAsync);

        app.MapPost(ApiRoutes.SocialInsurancePayments, DraftSocialInsurancePaymentAsync);
        app.MapGet(ApiRoutes.SocialInsurancePayment, ReadSocialInsurancePaymentAsync);
        app.MapPost(ApiRoutes.SocialInsurancePaymentPosting, PostSocialInsurancePaymentAsync);

        app.MapPost(ApiRoutes.EmployeeDeductions, RecordDeductionAsync);
        app.MapGet(ApiRoutes.EmployeeDeduction, ReadDeductionAsync);

        // ‏**وسلفة بلا مورد ترحيل — ولا يجوز أن يوجد اليوم.** حدث `hr.employee_advance.paid`
        // غير موجود في مصفوفة الترحيل، والمحرك يرفض رمزاً لا يعرفه بـ`UnknownEvent`
        // ولا يخترع قالباً. وغيابُ السطر مقروءٌ في العقد المنشور نفسه لا في تعليق وحده.
        app.MapPost(ApiRoutes.EmployeeAdvances, DraftAdvanceAsync);
        app.MapGet(ApiRoutes.EmployeeAdvance, ReadAdvanceAsync);

        app.MapPost(ApiRoutes.EndOfServiceProvisions, DraftProvisionAsync);
        app.MapGet(ApiRoutes.EndOfServiceProvision, ReadProvisionAsync);
        app.MapPost(ApiRoutes.EndOfServiceProvisionPosting, PostProvisionAsync);

        app.MapPost(ApiRoutes.EndOfServiceSettlements, DraftSettlementAsync);
        app.MapGet(ApiRoutes.EndOfServiceSettlement, ReadSettlementAsync);
        app.MapPost(ApiRoutes.EndOfServiceSettlementPosting, PostSettlementAsync);

        app.MapGet(ApiRoutes.EmployeeSubledgerReconciliation, ReconcileAsync);
    }

    // ── الموظفون ─────────────────────────────────────────────────────────────

    private static async Task<IResult> RegisterEmployeeAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrEmployeeRequestDto? dto, IResult? refused) =
            await BodyAsync<HrEmployeeRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrEmployeeRequest request;
        try
        {
            request = PayrollMapping.ToEmployeeRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrEmployee> result = await hr
            .RegisterEmployeeAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.Employee, companyId, "employeeId", result.Value.Id));
    }

    private static async Task<IResult> ReadEmployeeAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "employeeId", out Guid employeeId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrEmployee> result = await hr
            .ReadEmployeeAsync(new TenantId(companyId), Actor(context), employeeId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> TerminateEmployeeAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "employeeId", out Guid employeeId, out IResult? malformed))
        {
            return malformed!;
        }

        (HrTerminationRequestDto? dto, IResult? refused) =
            await BodyAsync<HrTerminationRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrTerminationRequest request;
        try
        {
            request = PayrollMapping.ToTerminationRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrEmployee> result = await hr
            .TerminateEmployeeAsync(new TenantId(companyId), Actor(context), employeeId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, PayrollMapping.ToDto(result.Value), location: null);
    }

    // ── مكوّنات الأجر وعناصره ────────────────────────────────────────────────

    private static async Task<IResult> AddPayComponentAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrPayComponentRequestDto? dto, IResult? refused) =
            await BodyAsync<HrPayComponentRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrPayComponentRequest request;
        try
        {
            request = PayrollMapping.ToPayComponentRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrPayComponent> result = await hr
            .AddPayComponentAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, PayrollMapping.ToDto(result.Value), location: null);
    }

    private static async Task<IResult> ListPayComponentsAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<HrPayComponent>> result = await hr
            .ListPayComponentsAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddPayElementAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "employeeId", out Guid employeeId, out IResult? malformed))
        {
            return malformed!;
        }

        (HrPayElementRequestDto? dto, IResult? refused) =
            await BodyAsync<HrPayElementRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrPayElementRequest request;
        try
        {
            request = PayrollMapping.ToPayElementRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrPayElement> result = await hr
            .AddPayElementAsync(new TenantId(companyId), Actor(context), employeeId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, PayrollMapping.ToDto(result.Value), location: null);
    }

    private static async Task<IResult> ListPayElementsAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "employeeId", out Guid employeeId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<HrPayElement>> result = await hr
            .ListPayElementsAsync(new TenantId(companyId), Actor(context), employeeId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── النِّسَب ─────────────────────────────────────────────────────────────

    private static async Task<IResult> DepositPayrollSettingsAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrPayrollSettingsRequestDto? dto, IResult? refused) =
            await BodyAsync<HrPayrollSettingsRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrPayrollSettingsRequest request;
        try
        {
            request = PayrollMapping.ToPayrollSettingsRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrPayrollSettings> result = await hr
            .DepositPayrollSettingsAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, PayrollMapping.ToDto(result.Value), location: null);
    }

    private static async Task<IResult> ListPayrollSettingsAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<HrPayrollSettings>> result = await hr
            .ListPayrollSettingsAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── المسيّر والقسائم ─────────────────────────────────────────────────────

    private static async Task<IResult> DraftPayrollRunAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrPayrollRunRequestDto? dto, IResult? refused) =
            await BodyAsync<HrPayrollRunRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrPayrollRunRequest request;
        try
        {
            request = PayrollMapping.ToPayrollRunRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrPayrollRun> result = await hr
            .DraftPayrollRunAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.PayrollRun, companyId, "runId", result.Value.Id));
    }

    private static async Task<IResult> ReadPayrollRunAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "runId", out Guid runId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrPayrollRun> result = await hr
            .ReadPayrollRunAsync(new TenantId(companyId), Actor(context), runId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ListPayslipsAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "runId", out Guid runId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<HrPayslip>> result = await hr
            .ListPayslipsAsync(new TenantId(companyId), Actor(context), runId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadPayslipAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "payslipId", out Guid payslipId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrPayslip> result = await hr
            .ReadPayslipAsync(new TenantId(companyId), Actor(context), payslipId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostPayrollRunAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "runId", out Guid runId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<HrPayslip>> result = await hr
            .PostPayrollRunAsync(new TenantId(companyId), Actor(context), runId, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        // ‏**الجواب قائمة قسائم لا مستنداً واحداً** — وهو انعكاس القرار الحاكم: نداءٌ
        // واحد أصدر قيداً لكل قسيمة، ولكلٍّ معرّف قيدها و`alreadyPosted` الخاصّ بها.
        // ورمز الحالة 200 حين كانت **كلّها** مُرحَّلة من قبل، و201 حين كتب هذا النداء
        // واحداً منها على الأقل.
        bool everySlipWasAlreadyPosted = result.Value.All(static slip => slip.AlreadyPosted);

        return Results.Json(
            PayrollMapping.ToDto(result.Value),
            ApiJson.Options,
            statusCode: everySlipWasAlreadyPosted ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    // ── مستندات الدفع ────────────────────────────────────────────────────────

    private static async Task<IResult> DraftPayrollPaymentAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrPayrollPaymentRequestDto? dto, IResult? refused) =
            await BodyAsync<HrPayrollPaymentRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrPayrollPaymentRequest request;
        try
        {
            request = PayrollMapping.ToPayrollPaymentRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrPayrollPayment> result = await hr
            .DraftPayrollPaymentAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.PayrollPayment, companyId, "paymentId", result.Value.Id));
    }

    private static async Task<IResult> ReadPayrollPaymentAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "paymentId", out Guid paymentId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrPayrollPayment> result = await hr
            .ReadPayrollPaymentAsync(new TenantId(companyId), Actor(context), paymentId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostPayrollPaymentAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "paymentId", out Guid paymentId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrPayrollPayment> result = await hr
            .PostPayrollPaymentAsync(new TenantId(companyId), Actor(context), paymentId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, PayrollMapping.ToDto(result.Value), result.Value.AlreadyPosted);
    }

    private static async Task<IResult> DraftSocialInsurancePaymentAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrSocialInsurancePaymentRequestDto? dto, IResult? refused) =
            await BodyAsync<HrSocialInsurancePaymentRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrSocialInsurancePaymentRequest request;
        try
        {
            request = PayrollMapping.ToSocialInsuranceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrSocialInsurancePayment> result = await hr
            .DraftSocialInsurancePaymentAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.SocialInsurancePayment, companyId, "paymentId", result.Value.Id));
    }

    private static async Task<IResult> ReadSocialInsurancePaymentAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "paymentId", out Guid paymentId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrSocialInsurancePayment> result = await hr
            .ReadSocialInsurancePaymentAsync(new TenantId(companyId), Actor(context), paymentId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostSocialInsurancePaymentAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "paymentId", out Guid paymentId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrSocialInsurancePayment> result = await hr
            .PostSocialInsurancePaymentAsync(new TenantId(companyId), Actor(context), paymentId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, PayrollMapping.ToDto(result.Value), result.Value.AlreadyPosted);
    }

    // ── الجزاءات والسلف ──────────────────────────────────────────────────────

    private static async Task<IResult> RecordDeductionAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrDeductionRequestDto? dto, IResult? refused) =
            await BodyAsync<HrDeductionRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrDeductionRequest request;
        try
        {
            request = PayrollMapping.ToDeductionRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrDeduction> result = await hr
            .RecordDeductionAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.EmployeeDeduction, companyId, "deductionId", result.Value.Id));
    }

    private static async Task<IResult> ReadDeductionAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "deductionId", out Guid deductionId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrDeduction> result = await hr
            .ReadDeductionAsync(new TenantId(companyId), Actor(context), deductionId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DraftAdvanceAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrAdvanceRequestDto? dto, IResult? refused) =
            await BodyAsync<HrAdvanceRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrAdvanceRequest request;
        try
        {
            request = PayrollMapping.ToAdvanceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrAdvance> result = await hr
            .DraftAdvanceAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.EmployeeAdvance, companyId, "advanceId", result.Value.Id));
    }

    private static async Task<IResult> ReadAdvanceAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "advanceId", out Guid advanceId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrAdvance> result = await hr
            .ReadAdvanceAsync(new TenantId(companyId), Actor(context), advanceId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── نهاية الخدمة ─────────────────────────────────────────────────────────

    private static async Task<IResult> DraftProvisionAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrProvisionRequestDto? dto, IResult? refused) =
            await BodyAsync<HrProvisionRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrProvisionRequest request;
        try
        {
            request = PayrollMapping.ToProvisionRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrProvision> result = await hr
            .DraftProvisionAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.EndOfServiceProvision, companyId, "provisionId", result.Value.Id));
    }

    private static async Task<IResult> ReadProvisionAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "provisionId", out Guid provisionId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrProvision> result = await hr
            .ReadProvisionAsync(new TenantId(companyId), Actor(context), provisionId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostProvisionAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "provisionId", out Guid provisionId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrProvision> result = await hr
            .PostProvisionAsync(new TenantId(companyId), Actor(context), provisionId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, PayrollMapping.ToDto(result.Value), result.Value.AlreadyPosted);
    }

    private static async Task<IResult> DraftSettlementAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (HrSettlementRequestDto? dto, IResult? refused) =
            await BodyAsync<HrSettlementRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        HrSettlementRequest request;
        try
        {
            request = PayrollMapping.ToSettlementRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<HrSettlement> result = await hr
            .DraftSettlementAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                PayrollMapping.ToDto(result.Value),
                Location(ApiRoutes.EndOfServiceSettlement, companyId, "settlementId", result.Value.Id));
    }

    private static async Task<IResult> ReadSettlementAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "settlementId", out Guid settlementId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrSettlement> result = await hr
            .ReadSettlementAsync(new TenantId(companyId), Actor(context), settlementId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostSettlementAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "settlementId", out Guid settlementId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<HrSettlement> result = await hr
            .PostSettlementAsync(new TenantId(companyId), Actor(context), settlementId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, PayrollMapping.ToDto(result.Value), result.Value.AlreadyPosted);
    }

    // ── المطابقة ─────────────────────────────────────────────────────────────

    private static async Task<IResult> ReconcileAsync(
        HttpContext context, HrSurface hr, CancellationToken cancellationToken)
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

        Result<HrReconciliation> result = await hr
            .ReconcileEmployeeSubledgerAsync(new TenantId(companyId), Actor(context), asOf, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(PayrollMapping.ToDto(result.Value), ApiJson.Options);
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
