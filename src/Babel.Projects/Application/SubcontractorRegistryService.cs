using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// سجلّ المقاولين من الباطن وعقودهم.
/// <para>
/// <b>ولماذا مالكُه هذه الوحدة:</b> دفتر <c>subcontractor</c> المساعد مُعلَنٌ في بيانات
/// الدفاتر بحساباته الثلاثة و<b>بلا مالكٍ في المستودع اليوم</b> — لا وحدة تسجّل طرفه
/// ولا تكتب حركته. وأدوارُه كلّها تُحرَّك من هنا، ومن يُحرّك الحساب الضابط يكتب دفتره
/// المساعد في المسار نفسه (ADR-0041).
/// </para>
/// <para>
/// <b>ولا حذف ولا إيقاف</b>، لما غاب عن سجلّ العملاء وللسبب نفسه: طرفٌ تشير إليه قيود
/// سنةٍ مضت لا يُحذف، وبابٌ اسمه «إيقاف» لا يمنع مستخلصاً واحداً أسوأ من غيابه.
/// </para>
/// </summary>
public sealed class SubcontractorRegistryService : IApplicationService
{
    /// <summary>صنف الكيان في جدول الترجمات — المقاول من الباطن.</summary>
    internal const string SubcontractorEntityKind = "subcontractor";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly ProjectsDbContext _database;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public SubcontractorRegistryService(IEntitlementEnforcer enforcer, ProjectsRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يسجّل مقاولاً من الباطن — طرفاً في دفتره المساعد.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<SubcontractorView>> CreateSubcontractorAsync(
        TenantId tenant,
        UserId actor,
        SubcontractorDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.Subcontractor.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SubcontractorView>.Failure(gate.Errors);
        }

        if (await _database.Subcontractors
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SubcontractorView>.Failure(ProjectsErrors.DuplicateNumber(draft.Code));
        }

        SubcontractorRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            VatNumber = draft.VatNumber,
            IsActive = true,
        };

        _database.Subcontractors.Add(row);

        foreach (KeyValuePair<string, string> translation in draft.Name.Translations)
        {
            _database.NameTranslations.Add(new NameTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                EntityKind = SubcontractorEntityKind,
                EntityId = row.Id,
                LanguageTag = translation.Key,
                Name = translation.Value,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SubcontractorView>.Success(
            new SubcontractorView(row.Id, row.Code, draft.Name, row.VatNumber, row.IsActive));
    }

    /// <summary>يقرأ مقاولاً من الباطن.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="subcontractorId">المقاول.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<SubcontractorView>> GetSubcontractorAsync(
        TenantId tenant,
        UserId actor,
        Guid subcontractorId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.Subcontractor.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SubcontractorView>.Failure(gate.Errors);
        }

        SubcontractorRow? row = await _database.Subcontractors
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == subcontractorId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<SubcontractorView>.Failure(ProjectsErrors.NotFound(SubcontractorEntityKind, subcontractorId));
        }

        TranslatedName name = await NameOfAsync(tenant, row.Id, row.NameAr, cancellationToken).ConfigureAwait(false);

        return Result<SubcontractorView>.Success(
            new SubcontractorView(row.Id, row.Code, name, row.VatNumber, row.IsActive));
    }

    /// <summary>يُنشئ عقد باطن بنسبة محتجزه وفترة ضمانه وبنوده.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<SubcontractView>> CreateSubcontractAsync(
        TenantId tenant,
        UserId actor,
        SubcontractDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.Subcontract.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SubcontractView>.Failure(gate.Errors);
        }

        if (draft.RetentionRate < 0m)
        {
            return Result<SubcontractView>.Failure(ProjectsErrors.NegativeAmount(nameof(draft.RetentionRate)));
        }

        ProjectRow? project = await _database.Projects
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.ProjectId, cancellationToken)
            .ConfigureAwait(false);

        if (project is null)
        {
            return Result<SubcontractView>.Failure(
                ProjectsErrors.NotFound(ProjectRegistryService.ProjectEntityKind, draft.ProjectId));
        }

        if (!await _database.Subcontractors
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == draft.SubcontractorId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SubcontractView>.Failure(
                ProjectsErrors.NotFound(SubcontractorEntityKind, draft.SubcontractorId));
        }

        if (await _database.Subcontracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SubcontractView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        SubcontractRow subcontract = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            ProjectId = draft.ProjectId,
            SubcontractorId = draft.SubcontractorId,
            Number = draft.Number,
            CurrencyCode = _currency.Value,
            SignedOn = draft.SignedOn,
            RetentionRate = draft.RetentionRate,
            GuaranteeMonths = draft.GuaranteeMonths,
            IsActive = true,
        };

        _database.Subcontracts.Add(subcontract);

        int lineNo = 0;
        foreach (SubcontractLineDraft line in draft.Lines)
        {
            lineNo++;
            _database.SubcontractLines.Add(new SubcontractLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                SubcontractId = subcontract.Id,
                Code = line.Code,
                LineNo = lineNo,
                DescriptionAr = line.DescriptionAr,
                Unit = line.ContractQuantity.Unit,
                ContractQuantity = line.ContractQuantity.Magnitude,
                UnitRate = line.UnitRate.Amount,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SubcontractView>.Success(
            View(subcontract, project.Code, PendingPolicyItems.All));
    }

    /// <summary>يقرأ عقد باطن ومعه بنوده المعلَّقة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="subcontractId">عقد الباطن.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<SubcontractView>> GetSubcontractAsync(
        TenantId tenant,
        UserId actor,
        Guid subcontractId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.Subcontract.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SubcontractView>.Failure(gate.Errors);
        }

        SubcontractRow? row = await _database.Subcontracts
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == subcontractId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<SubcontractView>.Failure(ProjectsErrors.NotFound("subcontract", subcontractId));
        }

        string projectCode = await _database.Projects
            .AsNoTracking()
            .Where(entity => entity.TenantId == tenant.Value && entity.Id == row.ProjectId)
            .Select(entity => entity.Code)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        IReadOnlyList<PendingPolicyItem> pending = await ContractPolicyGate
            .PendingAsync(_database, tenant.Value, subcontractId, cancellationToken)
            .ConfigureAwait(false);

        return Result<SubcontractView>.Success(View(row, projectCode, pending));
    }

    /// <summary>يقرأ بنود عقد الباطن <b>بمعرّفاتها</b> — مدخل سطور مستخلصه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="subcontractId">عقد الباطن.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<SubcontractLineView>>> ListSubcontractLinesAsync(
        TenantId tenant,
        UserId actor,
        Guid subcontractId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.SubcontractLine.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<SubcontractLineView>>.Failure(gate.Errors);
        }

        if (!await _database.Subcontracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == subcontractId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<IReadOnlyList<SubcontractLineView>>.Failure(
                ProjectsErrors.NotFound("subcontract", subcontractId));
        }

        List<SubcontractLineRow> rows = await _database.SubcontractLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.SubcontractId == subcontractId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<SubcontractLineView>>.Success(
        [
            .. rows.Select(row => new SubcontractLineView(
                row.Id,
                row.Code,
                row.LineNo,
                row.DescriptionAr,
                new ProjectQuantity(row.ContractQuantity, row.Unit),
                Money.Of(row.UnitRate, _currency))),
        ]);
    }

    private async Task<TranslatedName> NameOfAsync(
        TenantId tenant,
        Guid entityId,
        string arabic,
        CancellationToken cancellationToken)
    {
        List<NameTranslationRow> rows = await _database.NameTranslations
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.EntityKind == SubcontractorEntityKind
                          && row.EntityId == entityId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new TranslatedName(
            arabic,
            rows.ToDictionary(static row => row.LanguageTag, static row => row.Name, StringComparer.Ordinal));
    }

    private static SubcontractView View(
        SubcontractRow row,
        string projectCode,
        IReadOnlyList<PendingPolicyItem> pending) => new(
        row.Id,
        row.Number,
        row.ProjectId,
        projectCode,
        row.SubcontractorId,
        row.CurrencyCode,
        row.SignedOn,
        row.RetentionRate,
        row.GuaranteeMonths,
        pending);
}
