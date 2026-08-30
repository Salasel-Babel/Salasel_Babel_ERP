using Babel.Projects.Application;
using Babel.SharedKernel;

namespace Babel.Projects.Surface;

/// <summary>
/// <b>السطح المنشور لوحدة المقاولات</b> — وهو ما يجوز للجذر التركيبي أن يسمّيه، ولا شيء غيره.
/// <para>
/// القاعدة 13 (البند ب) تمنع <c>Babel.Api</c> من ذكر أي نوع من فضاء اسم داخلي لوحدة —
/// و<c>Persistence</c> و<c>Application</c> منها بالاسم. فالباب الوحيد المشروع هو
/// <b>سطحٌ منشور مسمّى خارج فضاءات الداخل</b>، بالشكل المُودَع في المبيعات والمشتريات
/// والمخزون.
/// </para>
/// <para>
/// <b>وما لا يفعله هذا الملفّ — عمداً:</b> لا يُنفِذ استحقاقاً، ولا يقرّر شيئاً محاسبياً،
/// ولا يقرأ جدولاً. كل دالّة هنا تُترجم نوعاً منشوراً إلى مسوّدة الوحدة وتنادي خدمة
/// التطبيق التي تحمل سمة الاستحقاق وتنادي المنفِّذ أوّل شيء.
/// </para>
/// <para>
/// <b>والمال يعبر هذا الحدّ <c>decimal</c> لا <c>Money</c>:</b> ‏<c>Money</c> يحمل عملةً،
/// وعملةُ المنشأة إعدادُ وحدةٍ لا معلومةُ نقل — فلو أخذ هذا السطح <c>Money</c> لاضطرّ
/// سطح HTTP أن <b>يختار عملة</b>، وهو قرار أعمال في طبقة نقل.
/// </para>
/// </summary>
public sealed class ProjectsSurface
{
    private readonly ProjectRegistryService _registry;
    private readonly SubcontractorRegistryService _subcontractors;
    private readonly ClientCertificateService _clientCertificates;
    private readonly SubcontractorCertificateService _subcontractorCertificates;
    private readonly SubcontractorAdvanceService _advances;
    private readonly RetentionService _retention;
    private readonly ProjectsReconciliationService _reconciliation;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ السطح فوق خدمات الوحدة.</summary>
    /// <param name="registry">سجلّ المشاريع والعقود.</param>
    /// <param name="subcontractors">سجلّ المقاولين وعقودهم.</param>
    /// <param name="clientCertificates">مستخلصات العملاء.</param>
    /// <param name="subcontractorCertificates">مستخلصات الباطن.</param>
    /// <param name="advances">دفعات المقاولين المقدمة.</param>
    /// <param name="retention">المحتجزات.</param>
    /// <param name="reconciliation">كشف المقاولين ومطابقته.</param>
    /// <param name="options">إعدادات الوحدة — ومنها عملة المنشأة.</param>
    public ProjectsSurface(
        ProjectRegistryService registry,
        SubcontractorRegistryService subcontractors,
        ClientCertificateService clientCertificates,
        SubcontractorCertificateService subcontractorCertificates,
        SubcontractorAdvanceService advances,
        RetentionService retention,
        ProjectsReconciliationService reconciliation,
        ProjectsOptions options)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(subcontractors);
        ArgumentNullException.ThrowIfNull(clientCertificates);
        ArgumentNullException.ThrowIfNull(subcontractorCertificates);
        ArgumentNullException.ThrowIfNull(advances);
        ArgumentNullException.ThrowIfNull(retention);
        ArgumentNullException.ThrowIfNull(reconciliation);
        ArgumentNullException.ThrowIfNull(options);

        _registry = registry;
        _subcontractors = subcontractors;
        _clientCertificates = clientCertificates;
        _subcontractorCertificates = subcontractorCertificates;
        _advances = advances;
        _retention = retention;
        _reconciliation = reconciliation;
        _currency = CurrencyCode.FromString(options.CompanyCurrency);
    }

    /// <summary>يسجّل مشروعاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsProject>> AddProjectAsync(
        TenantId tenant,
        UserId actor,
        ProjectsProjectRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<ProjectView> result = await _registry
            .CreateProjectAsync(
                tenant,
                actor,
                new ProjectDraft(request.Code, Name(request.NameAr, request.NameTranslations), request.StartedOn),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsProject>.Failure(result.Errors)
            : Result<ProjectsProject>.Success(Project(result.Value));
    }

    /// <summary>يقرأ مشروعاً بعقوده.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="projectId">المشروع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsProject>> ReadProjectAsync(
        TenantId tenant,
        UserId actor,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        Result<ProjectView> result = await _registry
            .GetProjectAsync(tenant, actor, projectId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsProject>.Failure(result.Errors)
            : Result<ProjectsProject>.Success(Project(result.Value));
    }

    /// <summary>يقرأ قائمة المشاريع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<ProjectsProject>>> ListProjectsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<ProjectView>> result = await _registry
            .ListProjectsAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<ProjectsProject>>.Failure(result.Errors)
            : Result<IReadOnlyList<ProjectsProject>>.Success([.. result.Value.Select(Project)]);
    }

    /// <summary>يُنشئ عقد مقاولة ببنوده.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsContract>> AddContractAsync(
        TenantId tenant,
        UserId actor,
        ProjectsContractRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<ContractView> result = await _registry
            .CreateContractAsync(
                tenant,
                actor,
                new ContractDraft(
                    request.Number,
                    request.ProjectId,
                    request.CustomerPartyId,
                    request.SignedOn,
                    request.RetentionRate,
                    request.GuaranteeMonths,
                    [.. request.Items.Select(BoqDraft)]),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsContract>.Failure(result.Errors)
            : Result<ProjectsContract>.Success(Contract(result.Value));
    }

    /// <summary>يقرأ عقداً ومعه بنوده المعلَّقة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsContract>> ReadContractAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result<ContractView> result = await _registry
            .GetContractAsync(tenant, actor, contractId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsContract>.Failure(result.Errors)
            : Result<ProjectsContract>.Success(Contract(result.Value));
    }

    /// <summary>يقرأ بنود جدول الكميات بمعرّفاتها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<ProjectsBoqItem>>> ReadBoqItemsAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<BoqItemView>> result = await _registry
            .ListBoqItemsAsync(tenant, actor, contractId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<ProjectsBoqItem>>.Failure(result.Errors)
            : Result<IReadOnlyList<ProjectsBoqItem>>.Success([.. result.Value.Select(BoqItem)]);
    }

    /// <summary>يسجّل أمراً تغييرياً — التزامٌ تعاقدي لا يُرحَّل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsChangeOrder>> AddChangeOrderAsync(
        TenantId tenant,
        UserId actor,
        ProjectsChangeOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<ChangeOrderView> result = await _registry
            .CreateChangeOrderAsync(
                tenant,
                actor,
                new ChangeOrderDraft(
                    request.Number,
                    request.ContractId,
                    request.IssuedOn,
                    request.ReasonAr,
                    request.ApprovedBy,
                    [.. request.AddedItems.Select(BoqDraft)]),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsChangeOrder>.Failure(result.Errors)
            : Result<ProjectsChangeOrder>.Success(ChangeOrder(result.Value));
    }

    /// <summary>يقرأ أمراً تغييرياً ببنوده الجديدة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="changeOrderId">الأمر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsChangeOrder>> ReadChangeOrderAsync(
        TenantId tenant,
        UserId actor,
        Guid changeOrderId,
        CancellationToken cancellationToken = default)
    {
        Result<ChangeOrderView> result = await _registry
            .GetChangeOrderAsync(tenant, actor, changeOrderId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsChangeOrder>.Failure(result.Errors)
            : Result<ProjectsChangeOrder>.Success(ChangeOrder(result.Value));
    }

    /// <summary>يقرأ أوامر عقدٍ التغييرية.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<ProjectsChangeOrder>>> ReadChangeOrdersAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<ChangeOrderView>> result = await _registry
            .ListChangeOrdersAsync(tenant, actor, contractId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<ProjectsChangeOrder>>.Failure(result.Errors)
            : Result<IReadOnlyList<ProjectsChangeOrder>>.Success([.. result.Value.Select(ChangeOrder)]);
    }

    /// <summary>يسجّل مقاولاً من الباطن.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsSubcontractor>> AddSubcontractorAsync(
        TenantId tenant,
        UserId actor,
        ProjectsSubcontractorRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<SubcontractorView> result = await _subcontractors
            .CreateSubcontractorAsync(
                tenant,
                actor,
                new SubcontractorDraft(
                    request.Code, Name(request.NameAr, request.NameTranslations), request.VatNumber),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsSubcontractor>.Failure(result.Errors)
            : Result<ProjectsSubcontractor>.Success(Subcontractor(result.Value));
    }

    /// <summary>يقرأ مقاولاً من الباطن.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="subcontractorId">المقاول.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsSubcontractor>> ReadSubcontractorAsync(
        TenantId tenant,
        UserId actor,
        Guid subcontractorId,
        CancellationToken cancellationToken = default)
    {
        Result<SubcontractorView> result = await _subcontractors
            .GetSubcontractorAsync(tenant, actor, subcontractorId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsSubcontractor>.Failure(result.Errors)
            : Result<ProjectsSubcontractor>.Success(Subcontractor(result.Value));
    }

    /// <summary>يُنشئ عقد باطن ببنوده.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsSubcontract>> AddSubcontractAsync(
        TenantId tenant,
        UserId actor,
        ProjectsSubcontractRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<SubcontractView> result = await _subcontractors
            .CreateSubcontractAsync(
                tenant,
                actor,
                new SubcontractDraft(
                    request.Number,
                    request.ProjectId,
                    request.SubcontractorId,
                    request.SignedOn,
                    request.RetentionRate,
                    request.GuaranteeMonths,
                    [
                        .. request.Lines.Select(line => new SubcontractLineDraft(
                            line.Code,
                            line.DescriptionAr,
                            new ProjectQuantity(line.ContractQuantity.Magnitude, line.ContractQuantity.Unit),
                            Money.Of(line.UnitRate, _currency))),
                    ]),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsSubcontract>.Failure(result.Errors)
            : Result<ProjectsSubcontract>.Success(Subcontract(result.Value));
    }

    /// <summary>يقرأ عقد باطن.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="subcontractId">عقد الباطن.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsSubcontract>> ReadSubcontractAsync(
        TenantId tenant,
        UserId actor,
        Guid subcontractId,
        CancellationToken cancellationToken = default)
    {
        Result<SubcontractView> result = await _subcontractors
            .GetSubcontractAsync(tenant, actor, subcontractId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsSubcontract>.Failure(result.Errors)
            : Result<ProjectsSubcontract>.Success(Subcontract(result.Value));
    }

    /// <summary>يقرأ بنود عقد الباطن بمعرّفاتها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="subcontractId">عقد الباطن.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<ProjectsSubcontractLine>>> ReadSubcontractLinesAsync(
        TenantId tenant,
        UserId actor,
        Guid subcontractId,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<SubcontractLineView>> result = await _subcontractors
            .ListSubcontractLinesAsync(tenant, actor, subcontractId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<ProjectsSubcontractLine>>.Failure(result.Errors)
            : Result<IReadOnlyList<ProjectsSubcontractLine>>.Success(
            [
                .. result.Value.Select(static line => new ProjectsSubcontractLine(
                    line.Id,
                    line.Code,
                    line.LineNo,
                    line.DescriptionAr,
                    new ProjectsMeasure(line.ContractQuantity.Magnitude, line.ContractQuantity.Unit),
                    line.UnitRate.Amount)),
            ]);
    }

    /// <summary>يُنشئ مستخلص عميل <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsCertificate>> DraftClientCertificateAsync(
        TenantId tenant,
        UserId actor,
        ProjectsCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<CertificateView> result = await _clientCertificates
            .DraftAsync(tenant, actor, CertificateDraftOf(request), cancellationToken).ConfigureAwait(false);

        return Certificate(result);
    }

    /// <summary>يقرأ مستخلص عميل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="certificateId">المستخلص.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsCertificate>> ReadClientCertificateAsync(
        TenantId tenant,
        UserId actor,
        Guid certificateId,
        CancellationToken cancellationToken = default)
        => Certificate(await _clientCertificates
            .GetAsync(tenant, actor, certificateId, cancellationToken).ConfigureAwait(false));

    /// <summary>يقرأ مستخلصات العقد.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<ProjectsCertificate>>> ReadClientCertificatesAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<CertificateView>> result = await _clientCertificates
            .ListAsync(tenant, actor, contractId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<ProjectsCertificate>>.Failure(result.Errors)
            : Result<IReadOnlyList<ProjectsCertificate>>.Success([.. result.Value.Select(CertificateOf)]);
    }

    /// <summary>يرحّل مستخلص عميل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="certificateId">المستخلص.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsCertificate>> PostClientCertificateAsync(
        TenantId tenant,
        UserId actor,
        Guid certificateId,
        CancellationToken cancellationToken = default)
        => Certificate(await _clientCertificates
            .PostAsync(tenant, actor, certificateId, cancellationToken).ConfigureAwait(false));

    /// <summary>يُنشئ مستخلص باطن <b>مسوّدة</b> بسطوره، ومنها الغرامات مستقلّةً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsCertificate>> DraftSubcontractorCertificateAsync(
        TenantId tenant,
        UserId actor,
        ProjectsCertificateRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Certificate(await _subcontractorCertificates
            .DraftAsync(tenant, actor, CertificateDraftOf(request), cancellationToken).ConfigureAwait(false));
    }

    /// <summary>يقرأ مستخلص باطن.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="certificateId">المستخلص.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsCertificate>> ReadSubcontractorCertificateAsync(
        TenantId tenant,
        UserId actor,
        Guid certificateId,
        CancellationToken cancellationToken = default)
        => Certificate(await _subcontractorCertificates
            .GetAsync(tenant, actor, certificateId, cancellationToken).ConfigureAwait(false));

    /// <summary>يرحّل مستخلص باطن.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="certificateId">المستخلص.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsCertificate>> PostSubcontractorCertificateAsync(
        TenantId tenant,
        UserId actor,
        Guid certificateId,
        CancellationToken cancellationToken = default)
        => Certificate(await _subcontractorCertificates
            .PostAsync(tenant, actor, certificateId, cancellationToken).ConfigureAwait(false));

    /// <summary>يُنشئ صرف دفعة مقدمة لمقاول <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> DraftSubcontractorAdvanceAsync(
        TenantId tenant,
        UserId actor,
        ProjectsAdvanceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Document(await _advances
            .DraftAsync(
                tenant,
                actor,
                new SubcontractorAdvanceDraft(
                    request.Number,
                    request.SubcontractId,
                    request.PaidOn,
                    Money.Of(request.Amount, _currency),
                    request.SettlementMethod,
                    request.TreasuryPartyId,
                    request.GuaranteeId),
                cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>يقرأ صرف دفعة مقدمة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> ReadSubcontractorAdvanceAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        CancellationToken cancellationToken = default)
        => Document(await _advances.GetAsync(tenant, actor, advanceId, cancellationToken).ConfigureAwait(false));

    /// <summary>يرحّل صرف دفعة مقدمة — حصينٌ ضد التكرار.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> PostSubcontractorAdvanceAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        CancellationToken cancellationToken = default)
        => Document(await _advances.PostAsync(tenant, actor, advanceId, cancellationToken).ConfigureAwait(false));

    /// <summary>يُنشئ إفراجاً عن محتجز دائن <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> DraftRetentionReleaseAsync(
        TenantId tenant,
        UserId actor,
        ProjectsRetentionReleaseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Document(await _retention
            .DraftReleaseAsync(
                tenant,
                actor,
                new RetentionReleaseDraft(
                    request.Number,
                    request.RetentionMovementId,
                    request.ReleasedOn,
                    Money.Of(request.Amount, _currency),
                    request.ApprovedBy),
                cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>يقرأ مستند إفراج.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="releaseId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> ReadRetentionReleaseAsync(
        TenantId tenant,
        UserId actor,
        Guid releaseId,
        CancellationToken cancellationToken = default)
        => Document(await _retention.GetReleaseAsync(tenant, actor, releaseId, cancellationToken).ConfigureAwait(false));

    /// <summary>يرحّل الإفراج — قيدٌ مستقلّ لا تعديل لقيد المستخلص.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="releaseId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> PostRetentionReleaseAsync(
        TenantId tenant,
        UserId actor,
        Guid releaseId,
        CancellationToken cancellationToken = default)
        => Document(await _retention.PostReleaseAsync(tenant, actor, releaseId, cancellationToken).ConfigureAwait(false));

    /// <summary>يُنشئ تحصيل محتجز من العميل <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> DraftRetentionCollectionAsync(
        TenantId tenant,
        UserId actor,
        ProjectsRetentionCollectionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        return Document(await _retention
            .DraftCollectionAsync(
                tenant,
                actor,
                new RetentionCollectionDraft(
                    request.Number,
                    request.RetentionMovementId,
                    request.CollectedOn,
                    Money.Of(request.Amount, _currency),
                    request.SettlementMethod,
                    request.TreasuryPartyId),
                cancellationToken)
            .ConfigureAwait(false));
    }

    /// <summary>يقرأ مستند تحصيل محتجز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="collectionId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> ReadRetentionCollectionAsync(
        TenantId tenant,
        UserId actor,
        Guid collectionId,
        CancellationToken cancellationToken = default)
        => Document(await _retention.GetCollectionAsync(tenant, actor, collectionId, cancellationToken).ConfigureAwait(false));

    /// <summary>يرحّل تحصيل المحتجز — وهو المسار الذي يمارس قدرة في هذه الوحدة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="collectionId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsDocument>> PostRetentionCollectionAsync(
        TenantId tenant,
        UserId actor,
        Guid collectionId,
        CancellationToken cancellationToken = default)
        => Document(await _retention.PostCollectionAsync(tenant, actor, collectionId, cancellationToken).ConfigureAwait(false));

    /// <summary>يسجّل خطاب ضمان — سجلٌّ لا يُرحَّل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsGuarantee>> AddGuaranteeAsync(
        TenantId tenant,
        UserId actor,
        ProjectsGuaranteeRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<GuaranteeView> result = await _registry
            .CreateGuaranteeAsync(
                tenant,
                actor,
                new GuaranteeDraft(
                    request.Number,
                    request.Kind,
                    request.ContractId,
                    request.SubcontractId,
                    request.IssuerNameAr,
                    Money.Of(request.Amount, _currency),
                    request.EffectiveFrom,
                    request.ExpiresOn,
                    request.AttachmentId),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsGuarantee>.Failure(result.Errors)
            : Result<ProjectsGuarantee>.Success(Guarantee(result.Value));
    }

    /// <summary>يقرأ خطاب ضمان.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="guaranteeId">الضمان.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsGuarantee>> ReadGuaranteeAsync(
        TenantId tenant,
        UserId actor,
        Guid guaranteeId,
        CancellationToken cancellationToken = default)
    {
        Result<GuaranteeView> result = await _registry
            .GetGuaranteeAsync(tenant, actor, guaranteeId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<ProjectsGuarantee>.Failure(result.Errors)
            : Result<ProjectsGuarantee>.Success(Guarantee(result.Value));
    }

    /// <summary>يقرأ سجلّ المحتجزات مدينةً ودائنة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsRetentionRegister>> ReadRetentionRegisterAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result<RetentionRegister> result = await _retention
            .ReadRegisterAsync(tenant, actor, asOf, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<ProjectsRetentionRegister>.Failure(result.Errors);
        }

        RetentionRegister register = result.Value;

        return Result<ProjectsRetentionRegister>.Success(new ProjectsRetentionRegister(
            register.AsOf,
            [
                .. register.Rows.Select(static row => new ProjectsRetentionEntry(
                    row.MovementId,
                    row.Side,
                    row.PartyKind,
                    row.PartyId,
                    row.ProjectCode,
                    row.DocumentType,
                    row.DocumentId,
                    row.Amount.Amount,
                    row.Outstanding.Amount,
                    row.MovedOn,
                    row.DueOn)),
            ],
            register.ReceivableTotal.Amount,
            register.PayableTotal.Amount));
    }

    /// <summary>يقرأ كشف المقاولين ومطابقته بنقطة ضبطه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">التاريخ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsSubcontractorStatement>> ReadSubcontractorStatementAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result<SubcontractorStatement> result = await _reconciliation
            .ReadSubcontractorStatementAsync(tenant, actor, asOf, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<ProjectsSubcontractorStatement>.Failure(result.Errors);
        }

        SubcontractorStatement statement = result.Value;

        return Result<ProjectsSubcontractorStatement>.Success(new ProjectsSubcontractorStatement(
            statement.AsOf,
            [
                .. statement.Rows.Select(static row => new ProjectsStatementLine(
                    row.SubcontractorId,
                    row.Code,
                    row.Name.Arabic,
                    Translations(row.Name),
                    row.Effect.Amount)),
            ],
            statement.SubledgerTotal.Amount,
            statement.ControlTotal.Amount,
            statement.Divergence.Amount,
            statement.IsReconciled));
    }

    /// <summary>يقرأ موقف العقد مشتقّاً من المُرحَّل وحده.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<ProjectsContractPosition>> ReadContractPositionAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result<ContractPosition> result = await _registry
            .GetContractPositionAsync(tenant, actor, contractId, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<ProjectsContractPosition>.Failure(result.Errors);
        }

        ContractPosition position = result.Value;

        return Result<ProjectsContractPosition>.Success(new ProjectsContractPosition(
            position.ContractId,
            position.ContractNumber,
            position.PostedCertificateCount,
            position.RetentionOutstanding.Amount,
            position.AdvanceOutstanding.Amount,
            Pending(position.PendingPolicy)));
    }

    // ── تحويلات ──────────────────────────────────────────────────────────────

    private static TranslatedName Name(string arabic, IReadOnlyList<ProjectsNameValue> translations)
        => new(arabic, translations.ToDictionary(static t => t.Name, static t => t.Value, StringComparer.Ordinal));

    private static IReadOnlyList<ProjectsNameValue> Translations(TranslatedName name)
        => [.. name.Translations.Select(static pair => new ProjectsNameValue(pair.Key, pair.Value))];

    private static IReadOnlyList<ProjectsPendingItem> Pending(IReadOnlyList<PendingPolicyItem> items)
        => [.. items.Select(static item => new ProjectsPendingItem(item.Code, item.TitleAr, item.TitleEn, item.SourceRef))];

    private static ProjectsProject Project(ProjectView view) => new(
        view.Id,
        view.Code,
        view.Name.Arabic,
        Translations(view.Name),
        view.StartedOn,
        view.IsActive,
        [.. view.Contracts.Select(static c => new ProjectsContractSummary(c.Id, c.Number, c.CurrencyCode))]);

    private static ProjectsContract Contract(ContractView view) => new(
        view.Id,
        view.Number,
        view.ProjectId,
        view.ProjectCode,
        view.CustomerPartyId,
        view.CurrencyCode,
        view.SignedOn,
        view.RetentionRate,
        view.GuaranteeMonths,
        Pending(view.PendingPolicy));

    private static ProjectsSubcontract Subcontract(SubcontractView view) => new(
        view.Id,
        view.Number,
        view.ProjectId,
        view.ProjectCode,
        view.SubcontractorId,
        view.CurrencyCode,
        view.SignedOn,
        view.RetentionRate,
        view.GuaranteeMonths,
        Pending(view.PendingPolicy));

    private static ProjectsSubcontractor Subcontractor(SubcontractorView view) => new(
        view.Id, view.Code, view.Name.Arabic, Translations(view.Name), view.VatNumber, view.IsActive);

    private static ProjectsBoqItem BoqItem(BoqItemView view) => new(
        view.Id,
        view.Code,
        view.LineNo,
        view.DescriptionAr,
        new ProjectsMeasure(view.ContractQuantity.Magnitude, view.ContractQuantity.Unit),
        view.UnitRate.Amount,
        view.ChangeOrderId);

    private static ProjectsChangeOrder ChangeOrder(ChangeOrderView view) => new(
        view.Id,
        view.Number,
        view.ContractId,
        view.IssuedOn,
        view.ReasonAr,
        view.ApprovedBy,
        [.. view.AddedItems.Select(BoqItem)]);

    private static ProjectsGuarantee Guarantee(GuaranteeView view) => new(
        view.Id,
        view.Number,
        view.Kind,
        view.ContractId,
        view.SubcontractId,
        view.IssuerNameAr,
        view.Amount.Amount,
        view.EffectiveFrom,
        view.ExpiresOn,
        view.AttachmentId);

    private static ProjectsCertificate CertificateOf(CertificateView view) => new(
        view.Id,
        view.Number,
        view.OwnerId,
        view.SequenceNo,
        view.PeriodFrom,
        view.PeriodTo,
        view.State,
        view.FrozenRetentionRate,
        [
            .. view.Lines.Select(static line => new ProjectsCertificateLine(
                line.Id,
                line.LineNo,
                line.LineKind,
                line.ItemId,
                line.ItemCode,
                line.DescriptionAr,
                new ProjectsMeasure(line.CumulativeQuantity.Magnitude, line.CumulativeQuantity.Unit),
                new ProjectsMeasure(line.PreviousQuantity.Magnitude, line.PreviousQuantity.Unit),
                line.Amount.Amount)),
        ],
        Pending(view.PendingPolicy),
        view.EntryId,
        view.AlreadyPosted);

    private static Result<ProjectsCertificate> Certificate(Result<CertificateView> result)
        => result.IsFailure
            ? Result<ProjectsCertificate>.Failure(result.Errors)
            : Result<ProjectsCertificate>.Success(CertificateOf(result.Value));

    private static Result<ProjectsDocument> Document(Result<ProjectsDocumentView> result)
        => result.IsFailure
            ? Result<ProjectsDocument>.Failure(result.Errors)
            : Result<ProjectsDocument>.Success(new ProjectsDocument(
                result.Value.Id,
                result.Value.Number,
                result.Value.State,
                result.Value.Amount.Amount,
                result.Value.EntryId,
                result.Value.AlreadyPosted));

    private BoqItemDraft BoqDraft(ProjectsBoqItemRequest request) => new(
        request.Code,
        request.DescriptionAr,
        new ProjectQuantity(request.ContractQuantity.Magnitude, request.ContractQuantity.Unit),
        Money.Of(request.UnitRate, _currency));

    private CertificateDraft CertificateDraftOf(ProjectsCertificateRequest request) => new(
        request.Number,
        request.OwnerId,
        request.SequenceNo,
        request.PeriodFrom,
        request.PeriodTo,
        [
            .. request.Lines.Select(line => new CertificateLineDraft(
                line.ItemId,
                line.LineKind,
                line.DescriptionAr,
                new ProjectQuantity(line.CumulativeQuantity.Magnitude, line.CumulativeQuantity.Unit),
                Money.Of(line.Amount, _currency))),
        ]);
}
