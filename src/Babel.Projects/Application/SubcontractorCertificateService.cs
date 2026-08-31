using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// مستخلص المقاول من الباطن — بالشكل نفسه: أنشئ · اقرأ · رحّل.
/// <para>
/// <b>وسطور الغرامات والخصومات مستقلّة لا صافٍ محسوب</b>، بتحفّظ المصفوفة نصّه:
/// «تُسجَّل كسطور مستقلة تخفّض المستحق ولا تُخصم من قيمة الأعمال». والقالب لا يملك
/// سطراً لها — مبالغه أربعة ليس فيها غرامة، وتعبير سطر الدائن لا يحتمل طرفاً خامساً.
/// فالسطور <b>تُخزَّن ولا تُرحَّل</b>، ويُرفض الترحيل إن وُجدت: <b>رفضٌ مُعلَن خيرٌ من
/// خصمٍ صامت من قيمة الأعمال</b> — والخصم يُنقص التكلفة المعترف بها للمشروع بمبلغ
/// غرامة، فتنحرف ربحيته وتكلفةُ بنده معاً.
/// </para>
/// </summary>
public sealed class SubcontractorCertificateService : IApplicationService
{
    /// <summary>نوع المستند في هوية الترحيل.</summary>
    internal const string CertificateDocument = "ProjectsSubcontractorCertificate";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly ProjectsDbContext _database;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public SubcontractorCertificateService(IEntitlementEnforcer enforcer, ProjectsRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يُنشئ مستخلص باطن <b>مسوّدة</b> بسطوره، ومنها الغرامات مستقلّةً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<CertificateView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        CertificateDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.SubcontractorCertificate.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CertificateView>.Failure(gate.Errors);
        }

        if (draft.Lines.Count == 0)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NoLines());
        }

        SubcontractRow? subcontract = await _database.Subcontracts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (subcontract is null)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NotFound("subcontract", draft.OwnerId));
        }

        if (await _database.SubcontractorCertificates
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<CertificateView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        if (await _database.SubcontractorCertificates
                .AnyAsync(
                    row => row.TenantId == tenant.Value
                           && row.SubcontractId == draft.OwnerId
                           && row.SequenceNo == draft.SequenceNo,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<CertificateView>.Failure(
                ProjectsErrors.DuplicateSequence(draft.OwnerId, draft.SequenceNo));
        }

        Dictionary<Guid, MeasuredItem> items = await _database.SubcontractLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.SubcontractId == draft.OwnerId)
            .Select(row => new MeasuredItem(row.Id, row.Code, row.Unit))
            .ToDictionaryAsync(static item => item.Id, cancellationToken)
            .ConfigureAwait(false);

        List<Guid> posted = await _database.SubcontractorCertificates
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.SubcontractId == draft.OwnerId
                          && row.State == ProjectsDocumentState.Posted)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> previous = await CumulativeLines
            .PreviousQuantitiesAsync(_database, tenant.Value, CertificateOwner.Subcontractor, posted, cancellationToken)
            .ConfigureAwait(false);

        SubcontractorCertificateRow certificate = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            SubcontractId = draft.OwnerId,
            Number = draft.Number,
            SequenceNo = draft.SequenceNo,
            PeriodFrom = draft.PeriodFrom,
            PeriodTo = draft.PeriodTo,
            State = ProjectsDocumentState.Draft,
            CurrencyCode = subcontract.CurrencyCode,
            FrozenRetentionRate = subcontract.RetentionRate,
        };

        Result<List<CertificateLineRow>> lines = CumulativeLines.Build(
            tenant.Value, CertificateOwner.Subcontractor, certificate.Id, draft.Lines, items, previous);

        if (lines.IsFailure)
        {
            return Result<CertificateView>.Failure(lines.Errors);
        }

        _database.SubcontractorCertificates.Add(certificate);
        _database.CertificateLines.AddRange(lines.Value);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PendingPolicyItem> pending = await ContractPolicyGate
            .PendingAsync(_database, tenant.Value, subcontract.Id, cancellationToken)
            .ConfigureAwait(false);

        return Result<CertificateView>.Success(View(certificate, lines.Value, items, pending));
    }

    /// <summary>يقرأ مستخلص باطن بحالته وسطوره.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="certificateId">المستخلص.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<CertificateView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid certificateId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.SubcontractorCertificate.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CertificateView>.Failure(gate.Errors);
        }

        SubcontractorCertificateRow? certificate = await _database.SubcontractorCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == certificateId, cancellationToken)
            .ConfigureAwait(false);

        if (certificate is null)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NotFound(CertificateDocument, certificateId));
        }

        List<CertificateLineRow> lines = await LinesAsync(tenant, certificate.Id, cancellationToken).ConfigureAwait(false);

        Dictionary<Guid, MeasuredItem> items = await _database.SubcontractLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.SubcontractId == certificate.SubcontractId)
            .Select(row => new MeasuredItem(row.Id, row.Code, row.Unit))
            .ToDictionaryAsync(static item => item.Id, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PendingPolicyItem> pending = await ContractPolicyGate
            .PendingAsync(_database, tenant.Value, certificate.SubcontractId, cancellationToken)
            .ConfigureAwait(false);

        return Result<CertificateView>.Success(View(certificate, lines, items, pending));
    }

    /// <summary>
    /// يرحّل مستخلص باطن — <b>ويُرفض بمشكلةٍ مُسمّاة</b>: بسطر الغرامة إن وُجد، وإلا
    /// بالبند المعلَّق على العقد.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="certificateId">المستخلص.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<CertificateView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid certificateId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.SubcontractorCertificate.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CertificateView>.Failure(gate.Errors);
        }

        SubcontractorCertificateRow? certificate = await _database.SubcontractorCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == certificateId, cancellationToken)
            .ConfigureAwait(false);

        if (certificate is null)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NotFound(CertificateDocument, certificateId));
        }

        // ‏**سطر الغرامة يُرفض باسمه أوّلاً** — لأنه الرفض الأخصّ، ولأن المخرج السهل
        // أمام المنفِّذ (خصمُه من قيمة الأعمال) هو بالضبط ما يمنعه التحفّظ.
        int penalties = await _database.CertificateLines
            .CountAsync(
                row => row.TenantId == tenant.Value
                       && row.OwnerType == CertificateOwner.Subcontractor
                       && row.OwnerId == certificateId
                       && row.LineKind != CertificateLineKind.Work,
                cancellationToken)
            .ConfigureAwait(false);

        if (penalties > 0)
        {
            return Result<CertificateView>.Failure(
                ProjectsErrors.PenaltyLinesHaveNoTemplate(certificateId, penalties));
        }

        Result settled = await ContractPolicyGate
            .EnsureSettledAsync(_database, tenant.Value, certificate.SubcontractId, cancellationToken)
            .ConfigureAwait(false);

        return Result<CertificateView>.Failure(settled.Errors);
    }

    private async Task<List<CertificateLineRow>> LinesAsync(
        TenantId tenant,
        Guid certificateId,
        CancellationToken cancellationToken)
        => await _database.CertificateLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.OwnerType == CertificateOwner.Subcontractor
                          && row.OwnerId == certificateId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

    private CertificateView View(
        SubcontractorCertificateRow certificate,
        IReadOnlyList<CertificateLineRow> lines,
        IReadOnlyDictionary<Guid, MeasuredItem> items,
        IReadOnlyList<PendingPolicyItem> pending) => new(
        certificate.Id,
        certificate.Number,
        certificate.SubcontractId,
        certificate.SequenceNo,
        certificate.PeriodFrom,
        certificate.PeriodTo,
        certificate.State,
        certificate.FrozenRetentionRate,
        [.. lines.Select(line => ClientCertificateService.Line(line, items, _currency))],
        pending,
        certificate.PostedEntryId,
        AlreadyPosted: false);
}
