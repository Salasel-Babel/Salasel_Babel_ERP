using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// سجلّ المشاريع والعقود وجداول الكميات وأوامر التغيير وخطابات الضمان.
/// <para>
/// <b>ولا واقعة محاسبية في هذه الخدمة كلّها:</b> لا حدث، ولا قيد، ولا مساس بحساب.
/// المشروع بُعدٌ يتعايش مع مركز التكلفة لا مركز تكلفة، والعقد التزامٌ تعاقدي، والأمر
/// التغييري التزامٌ تعاقدي، وخطاب الضمان سجلّ. ولذلك <b>لا مورد ترحيل لأيٍّ منها</b>
/// على السطح المنشور، ولا حقل <c>entryId</c> في أي مخطّط جواب هنا.
/// </para>
/// </summary>
public sealed class ProjectRegistryService : IApplicationService
{
    /// <summary>صنف الكيان في جدول الترجمات — المشروع.</summary>
    internal const string ProjectEntityKind = "project";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly ProjectsDbContext _database;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public ProjectRegistryService(IEntitlementEnforcer enforcer, ProjectsRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// يسجّل مشروعاً. <b>ورمزه هو ما يدخل بُعد المشروع على سطر القيد</b>، فهو هوية
    /// لا اسم عرض — ولا تعديل له ولا حذف بعد أن تحمله قيود.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ProjectView>> CreateProjectAsync(
        TenantId tenant,
        UserId actor,
        ProjectDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.Project.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectView>.Failure(gate.Errors);
        }

        if (await _database.Projects
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ProjectView>.Failure(ProjectsErrors.DuplicateNumber(draft.Code));
        }

        ProjectRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            StartedOn = draft.StartedOn,
            IsActive = true,
        };

        _database.Projects.Add(row);
        WriteTranslations(tenant, ProjectEntityKind, row.Id, draft.Name);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ProjectView>.Success(
            new ProjectView(row.Id, row.Code, draft.Name, row.StartedOn, row.IsActive, []));
    }

    /// <summary>يقرأ مشروعاً بحالته وعقوده.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="projectId">المشروع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<ProjectView>> GetProjectAsync(
        TenantId tenant,
        UserId actor,
        Guid projectId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.Project.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectView>.Failure(gate.Errors);
        }

        ProjectRow? row = await _database.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == projectId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<ProjectView>.Failure(ProjectsErrors.NotFound(ProjectEntityKind, projectId));
        }

        List<ContractSummary> contracts =
        [
            .. (await _database.Contracts
                .AsNoTracking()
                .Where(entity => entity.TenantId == tenant.Value && entity.ProjectId == projectId)
                .OrderBy(entity => entity.Number)
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false))
                .Select(static entity => new ContractSummary(entity.Id, entity.Number, entity.CurrencyCode)),
        ];

        TranslatedName name = await NameOfAsync(tenant, ProjectEntityKind, row.Id, row.NameAr, cancellationToken)
            .ConfigureAwait(false);

        return Result<ProjectView>.Success(
            new ProjectView(row.Id, row.Code, name, row.StartedOn, row.IsActive, contracts));
    }

    /// <summary>
    /// يقرأ قائمة المشاريع. <b>وبابٌ لا يوصل إليه بابٌ آخر اعتراضٌ مكتوب في ADR-0044</b>:
    /// باب العقد يحتاج معرّف مشروع، ولا سبيل إليه إلا من هنا.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<ProjectView>>> ListProjectsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.Project.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ProjectView>>.Failure(gate.Errors);
        }

        List<ProjectRow> rows = await _database.Projects
            .AsNoTracking()
            .Where(entity => entity.TenantId == tenant.Value)
            .OrderBy(entity => entity.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ProjectView> views = [];

        foreach (ProjectRow row in rows)
        {
            TranslatedName name = await NameOfAsync(tenant, ProjectEntityKind, row.Id, row.NameAr, cancellationToken)
                .ConfigureAwait(false);
            views.Add(new ProjectView(row.Id, row.Code, name, row.StartedOn, row.IsActive, []));
        }

        return Result<IReadOnlyList<ProjectView>>.Success(views);
    }

    /// <summary>
    /// يُنشئ عقد مقاولة ببنود جدول الكميات ونسبة المحتجز وفترة الضمان والعملة.
    /// <para>
    /// <b>ولاحظ ما لا يحمله العقد: وعاء نسبة المحتجز ولا قاعدة استرداد الدفعة المقدمة.</b>
    /// موضعُهما نفسه قرار مالك — حقلٌ على العقد؟ أم جدول قواعد بتاريخ سريان؟ — وكتابةُ
    /// أحدهما هنا اختيارٌ لجوابٍ لم يقله أحد. وهما بندٌ معلَّق يرفض الترحيل حتى يُحسم.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ContractView>> CreateContractAsync(
        TenantId tenant,
        UserId actor,
        ContractDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.Contract.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ContractView>.Failure(gate.Errors);
        }

        if (draft.RetentionRate < 0m)
        {
            return Result<ContractView>.Failure(ProjectsErrors.NegativeAmount(nameof(draft.RetentionRate)));
        }

        ProjectRow? project = await _database.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.ProjectId, cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
        {
            return Result<ContractView>.Failure(ProjectsErrors.NotFound(ProjectEntityKind, draft.ProjectId));
        }

        if (await _database.Contracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ContractView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        ProjectContractRow contract = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            ProjectId = draft.ProjectId,
            Number = draft.Number,
            CustomerPartyId = draft.CustomerPartyId,
            CurrencyCode = _currency.Value,
            SignedOn = draft.SignedOn,
            RetentionRate = draft.RetentionRate,
            GuaranteeMonths = draft.GuaranteeMonths,
            IsActive = true,
        };

        _database.Contracts.Add(contract);

        int lineNo = 0;
        foreach (BoqItemDraft item in draft.Items)
        {
            lineNo++;
            _database.BoqItems.Add(new BoqItemRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                ContractId = contract.Id,
                Code = item.Code,
                LineNo = lineNo,
                DescriptionAr = item.DescriptionAr,
                Unit = item.ContractQuantity.Unit,
                ContractQuantity = item.ContractQuantity.Magnitude,
                UnitRate = item.UnitRate.Amount,
                ChangeOrderId = null,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ContractView>.Success(Contract(contract, project.Code, PendingPolicyItems.All));
    }

    /// <summary>يقرأ عقداً ومعه بنوده المعلَّقة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<ContractView>> GetContractAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.Contract.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ContractView>.Failure(gate.Errors);
        }

        ProjectContractRow? contract = await _database.Contracts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == contractId, cancellationToken)
            .ConfigureAwait(false);

        if (contract is null)
        {
            return Result<ContractView>.Failure(ProjectsErrors.NotFound("project_contract", contractId));
        }

        string projectCode = await ProjectCodeAsync(tenant, contract.ProjectId, cancellationToken).ConfigureAwait(false);
        IReadOnlyList<PendingPolicyItem> pending =
            await ContractPolicyGate.PendingAsync(_database, tenant.Value, contractId, cancellationToken).ConfigureAwait(false);

        return Result<ContractView>.Success(Contract(contract, projectCode, pending));
    }

    /// <summary>
    /// يقرأ بنود جدول الكميات <b>بمعرّفاتها</b> — وهي مدخل سطور المستخلص.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<BoqItemView>>> ListBoqItemsAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.BoqItem.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<BoqItemView>>.Failure(gate.Errors);
        }

        if (!await _database.Contracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == contractId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<IReadOnlyList<BoqItemView>>.Failure(ProjectsErrors.NotFound("project_contract", contractId));
        }

        List<BoqItemRow> rows = await _database.BoqItems
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.ContractId == contractId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<BoqItemView>>.Success([.. rows.Select(BoqItem)]);
    }

    /// <summary>
    /// يسجّل أمراً تغييرياً ببنوده الجديدة. <b>التزامٌ تعاقدي لا واقعة محاسبية</b>:
    /// لا حدث له في المصفوفة، فلا مورد ترحيل له ولا حقل قيد في جوابه.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ChangeOrderView>> CreateChangeOrderAsync(
        TenantId tenant,
        UserId actor,
        ChangeOrderDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.ChangeOrder.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ChangeOrderView>.Failure(gate.Errors);
        }

        if (!await _database.Contracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == draft.ContractId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ChangeOrderView>.Failure(ProjectsErrors.NotFound("project_contract", draft.ContractId));
        }

        if (await _database.ChangeOrders
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ChangeOrderView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        ChangeOrderRow order = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            ContractId = draft.ContractId,
            Number = draft.Number,
            IssuedOn = draft.IssuedOn,
            ReasonAr = draft.ReasonAr,
            ApprovedBy = draft.ApprovedBy,
        };

        _database.ChangeOrders.Add(order);

        int lineNo = await _database.BoqItems
            .Where(row => row.TenantId == tenant.Value && row.ContractId == draft.ContractId)
            .MaxAsync(row => (int?)row.LineNo, cancellationToken)
            .ConfigureAwait(false) ?? 0;

        List<BoqItemRow> added = [];

        foreach (BoqItemDraft item in draft.AddedItems)
        {
            lineNo++;
            BoqItemRow row = new()
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                ContractId = draft.ContractId,
                Code = item.Code,
                LineNo = lineNo,
                DescriptionAr = item.DescriptionAr,
                Unit = item.ContractQuantity.Unit,
                ContractQuantity = item.ContractQuantity.Magnitude,
                UnitRate = item.UnitRate.Amount,
                ChangeOrderId = order.Id,
            };

            added.Add(row);
            _database.BoqItems.Add(row);
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ChangeOrderView>.Success(new ChangeOrderView(
            order.Id, order.Number, order.ContractId, order.IssuedOn, order.ReasonAr, order.ApprovedBy,
            [.. added.Select(BoqItem)]));
    }

    /// <summary>يقرأ أمراً تغييرياً ببنوده الجديدة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="changeOrderId">الأمر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<ChangeOrderView>> GetChangeOrderAsync(
        TenantId tenant,
        UserId actor,
        Guid changeOrderId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.ChangeOrder.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ChangeOrderView>.Failure(gate.Errors);
        }

        ChangeOrderRow? order = await _database.ChangeOrders
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == changeOrderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result<ChangeOrderView>.Failure(ProjectsErrors.NotFound("change_order", changeOrderId));
        }

        List<BoqItemRow> added = await _database.BoqItems
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.ChangeOrderId == changeOrderId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<ChangeOrderView>.Success(new ChangeOrderView(
            order.Id, order.Number, order.ContractId, order.IssuedOn, order.ReasonAr, order.ApprovedBy,
            [.. added.Select(BoqItem)]));
    }

    /// <summary>يقرأ أوامر عقدٍ التغييرية.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<ChangeOrderView>>> ListChangeOrdersAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.ChangeOrder.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ChangeOrderView>>.Failure(gate.Errors);
        }

        if (!await _database.Contracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == contractId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<IReadOnlyList<ChangeOrderView>>.Failure(ProjectsErrors.NotFound("project_contract", contractId));
        }

        List<ChangeOrderRow> orders = await _database.ChangeOrders
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.ContractId == contractId)
            .OrderBy(row => row.Number)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<BoqItemRow> items = await _database.BoqItems
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.ContractId == contractId && row.ChangeOrderId != null)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<ChangeOrderView>>.Success(
        [
            .. orders.Select(order => new ChangeOrderView(
                order.Id, order.Number, order.ContractId, order.IssuedOn, order.ReasonAr, order.ApprovedBy,
                [.. items.Where(item => item.ChangeOrderId == order.Id).Select(BoqItem)])),
        ]);
    }

    /// <summary>
    /// يسجّل خطاب ضمان بمرفقه. <b>ولا يُرحَّل أبداً</b>: لا حدث له في المصفوفة،
    /// والمرفق يُودَع على السطح المنشور للمرفقات ويُشار إليه بمعرّف — لا عمود ثنائي.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<GuaranteeView>> CreateGuaranteeAsync(
        TenantId tenant,
        UserId actor,
        GuaranteeDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.Guarantee.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<GuaranteeView>.Failure(gate.Errors);
        }

        if (draft.Amount.Amount < 0m)
        {
            return Result<GuaranteeView>.Failure(ProjectsErrors.NegativeAmount(nameof(draft.Amount)));
        }

        if (await _database.Guarantees
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<GuaranteeView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        if (draft.ContractId is { } contractId && !await _database.Contracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == contractId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<GuaranteeView>.Failure(ProjectsErrors.NotFound("project_contract", contractId));
        }

        if (draft.SubcontractId is { } subcontractId && !await _database.Subcontracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == subcontractId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<GuaranteeView>.Failure(ProjectsErrors.NotFound("subcontract", subcontractId));
        }

        GuaranteeRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            ContractId = draft.ContractId,
            SubcontractId = draft.SubcontractId,
            Kind = draft.Kind,
            Number = draft.Number,
            IssuerNameAr = draft.IssuerNameAr,
            CurrencyCode = _currency.Value,
            Amount = draft.Amount.Amount,
            EffectiveFrom = draft.EffectiveFrom,
            ExpiresOn = draft.ExpiresOn,
            AttachmentId = draft.AttachmentId,
        };

        _database.Guarantees.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<GuaranteeView>.Success(Guarantee(row));
    }

    /// <summary>يقرأ خطاب ضمان.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="guaranteeId">الضمان.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<GuaranteeView>> GetGuaranteeAsync(
        TenantId tenant,
        UserId actor,
        Guid guaranteeId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.Guarantee.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<GuaranteeView>.Failure(gate.Errors);
        }

        GuaranteeRow? row = await _database.Guarantees
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == guaranteeId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<GuaranteeView>.Failure(ProjectsErrors.NotFound("guarantee", guaranteeId))
            : Result<GuaranteeView>.Success(Guarantee(row));
    }

    /// <summary>
    /// موقف العقد <b>مشتقّاً من المُرحَّل وحده</b> — وهو بديلٌ لتقرير ربحية المشروع
    /// لا نسخةٌ منه.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<ContractPosition>> GetContractPositionAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.Contract.Position", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ContractPosition>.Failure(gate.Errors);
        }

        ProjectContractRow? contract = await _database.Contracts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == contractId, cancellationToken)
            .ConfigureAwait(false);

        if (contract is null)
        {
            return Result<ContractPosition>.Failure(ProjectsErrors.NotFound("project_contract", contractId));
        }

        int posted = await _database.ClientCertificates
            .CountAsync(
                row => row.TenantId == tenant.Value
                       && row.ContractId == contractId
                       && row.State == ProjectsDocumentState.Posted,
                cancellationToken)
            .ConfigureAwait(false);

        string projectCode = await ProjectCodeAsync(tenant, contract.ProjectId, cancellationToken).ConfigureAwait(false);

        decimal retention = await _database.RetentionMovements
            .Where(row => row.TenantId == tenant.Value
                          && row.ProjectCode == projectCode
                          && row.Side == RetentionSide.Receivable)
            .SumAsync(row => (decimal?)row.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        decimal advance = await _database.AdvanceMovements
            .Where(row => row.TenantId == tenant.Value && row.ContractId == contractId)
            .SumAsync(row => (decimal?)row.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        IReadOnlyList<PendingPolicyItem> pending =
            await ContractPolicyGate.PendingAsync(_database, tenant.Value, contractId, cancellationToken).ConfigureAwait(false);

        return Result<ContractPosition>.Success(new ContractPosition(
            contract.Id,
            contract.Number,
            posted,
            Money.Of(retention, _currency),
            Money.Of(advance, _currency),
            pending));
    }

    // ── مشترك ────────────────────────────────────────────────────────────────

    /// <summary>يكتب ترجمات الاسم صفوفاً. والعربي عمودٌ على الكيان لأنه السجلّ.</summary>
    private void WriteTranslations(TenantId tenant, string entityKind, Guid entityId, TranslatedName name)
    {
        foreach (KeyValuePair<string, string> translation in name.Translations)
        {
            _database.NameTranslations.Add(new NameTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                EntityKind = entityKind,
                EntityId = entityId,
                LanguageTag = translation.Key,
                Name = translation.Value,
            });
        }
    }

    /// <summary>يقرأ الاسم بسجلّه العربي وترجماته الصفوف.</summary>
    private async Task<TranslatedName> NameOfAsync(
        TenantId tenant,
        string entityKind,
        Guid entityId,
        string arabic,
        CancellationToken cancellationToken)
    {
        List<NameTranslationRow> rows = await _database.NameTranslations
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.EntityKind == entityKind && row.EntityId == entityId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TranslatedName(
            arabic,
            rows.ToDictionary(static row => row.LanguageTag, static row => row.Name, StringComparer.Ordinal));
    }

    private async Task<string> ProjectCodeAsync(TenantId tenant, Guid projectId, CancellationToken cancellationToken)
        => await _database.Projects
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.Id == projectId)
            .Select(row => row.Code)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

    private static ContractView Contract(
        ProjectContractRow row,
        string projectCode,
        IReadOnlyList<PendingPolicyItem> pending) => new(
        row.Id,
        row.Number,
        row.ProjectId,
        projectCode,
        row.CustomerPartyId,
        row.CurrencyCode,
        row.SignedOn,
        row.RetentionRate,
        row.GuaranteeMonths,
        pending);

    private BoqItemView BoqItem(BoqItemRow row) => new(
        row.Id,
        row.Code,
        row.LineNo,
        row.DescriptionAr,
        new ProjectQuantity(row.ContractQuantity, row.Unit),
        Money.Of(row.UnitRate, _currency),
        row.ChangeOrderId);

    private GuaranteeView Guarantee(GuaranteeRow row) => new(
        row.Id,
        row.Number,
        row.Kind,
        row.ContractId,
        row.SubcontractId,
        row.IssuerNameAr,
        Money.Of(row.Amount, _currency),
        row.EffectiveFrom,
        row.ExpiresOn,
        row.AttachmentId);
}
