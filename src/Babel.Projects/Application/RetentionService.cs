using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// المحتجزات: الإفراج عن الدائن، وتحصيل المدين، وسجلّهما.
/// <para>
/// <b>والإفراج مستندٌ مستقلّ لا تعديلٌ لقيد المستخلص</b> — نصّ الحدث بحرفه. ومعرّفُه هو
/// ما يجعل حدثاً بلا مستندٍ بطبعه <b>حصيناً ضد التكرار</b>: بلا مستندٍ يحمل هوية، تكون
/// إعادةُ نداءٍ إفراجاً ثانياً.
/// </para>
/// <para>
/// <b>وكلاهما يقع على دفعة محتجزٍ مُسمّاة لا على رصيد</b>، وحركاتُ المحتجز تُشتقّ من
/// <b>المُرحَّل وحده</b>. فما دام أول مستخلص محجوباً ببندٍ معلَّق، لا حركة تُفرَج ولا
/// حركة تُحصَّل — وذلك أثرٌ مباشر للبند المعلَّق، لا نقصٌ في هذا المسار.
/// </para>
/// </summary>
public sealed class RetentionService : IApplicationService
{
    /// <summary>نوع مستند الإفراج في هوية الترحيل.</summary>
    internal const string ReleaseDocument = "ProjectsRetentionRelease";

    /// <summary>نوع مستند التحصيل في هوية الترحيل.</summary>
    internal const string CollectionDocument = "ProjectsRetentionCollection";

    /// <summary>دفتر العميل المساعد.</summary>
    internal const string CustomerSubledger = "customer";

    private const string TreasurySubledgerFact = "subledger.none";
    private const string SettlementMethodFact = "document.settlement_method";
    private const string SubcontractorFact = "subledger.subcontractor";
    private const string CustomerFact = "subledger.customer";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly ProjectsDbContext _database;
    private readonly ProjectsPostingGateway _gateway;
    private readonly ProjectsAdmission _admission;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرّك الترحيل.</param>
    /// <param name="profiles">
    /// مخزن ملفّات القدرات — يُبنى منه القبول هنا: تحصيلُ المحتجز يمارس قدرةً مُرخِّصة،
    /// والبوّابة <c>internal</c> فلا تظهر في مُنشئٍ عام.
    /// </param>
    public RetentionService(
        IEntitlementEnforcer enforcer,
        ProjectsRuntime runtime,
        IPostingService posting,
        ICapabilityProfileStore profiles)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(profiles);
        _enforcer = enforcer;
        _database = runtime.Database;
        _gateway = new ProjectsPostingGateway(runtime.Database, posting, runtime.CostCenters);
        _admission = new ProjectsAdmission(profiles);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يُنشئ إفراجاً عن محتجزٍ دائن <b>مسوّدة</b>، باعتمادٍ صريح.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ProjectsDocumentView>> DraftReleaseAsync(
        TenantId tenant,
        UserId actor,
        RetentionReleaseDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.RetentionRelease.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        if (draft.Amount.Amount <= 0m)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NegativeAmount(nameof(draft.Amount)));
        }

        RetentionMovementRow? movement = await MovementAsync(
            tenant, draft.RetentionMovementId, RetentionSide.Payable, cancellationToken).ConfigureAwait(false);

        if (movement is null)
        {
            return Result<ProjectsDocumentView>.Failure(
                ProjectsErrors.RetentionMovementNotFound(draft.RetentionMovementId));
        }

        if (await _database.RetentionReleases
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        decimal released = await _database.RetentionReleases
            .Where(row => row.TenantId == tenant.Value
                          && row.RetentionMovementId == draft.RetentionMovementId
                          && row.State == ProjectsDocumentState.Posted)
            .SumAsync(row => (decimal?)row.Amount, cancellationToken)
            .ConfigureAwait(false) ?? 0m;

        if (released + draft.Amount.Amount > movement.Amount)
        {
            return Result<ProjectsDocumentView>.Failure(
                ProjectsErrors.ReleaseExceedsMovement(draft.RetentionMovementId));
        }

        RetentionReleaseRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            RetentionMovementId = draft.RetentionMovementId,
            Number = draft.Number,
            ReleasedOn = draft.ReleasedOn,
            State = ProjectsDocumentState.Draft,
            CurrencyCode = _currency.Value,
            Amount = draft.Amount.Amount,
            ApprovedBy = draft.ApprovedBy,
        };

        _database.RetentionReleases.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ProjectsDocumentView>.Success(
            new ProjectsDocumentView(row.Id, row.Number, row.State, Money.Of(row.Amount, _currency), null, false));
    }

    /// <summary>يقرأ مستند إفراج.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="releaseId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<ProjectsDocumentView>> GetReleaseAsync(
        TenantId tenant,
        UserId actor,
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.RetentionRelease.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        RetentionReleaseRow? row = await _database.RetentionReleases
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == releaseId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound(ReleaseDocument, releaseId))
            : Result<ProjectsDocumentView>.Success(new ProjectsDocumentView(
                row.Id, row.Number, row.State, Money.Of(row.Amount, _currency), row.PostedEntryId, false));
    }

    /// <summary>
    /// يرحّل الإفراج بحدثه — <b>قيدٌ مستقلّ لا تعديل لقيد المستخلص</b>.
    /// <para>
    /// وأثره على نقطة ضبط المقاول <b>صفر</b> بحكم القالب: الحركة داخل الدفتر المساعد
    /// نفسه — يُدين المحتجز الدائن ويُدين به مستحقّ المقاول — فالمجموع لا يتغيّر.
    /// وكتابةُ أثرٍ غير صفري هنا كانت ستُنتج انحرافاً على مستندٍ سليم.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="releaseId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ProjectsDocumentView>> PostReleaseAsync(
        TenantId tenant,
        UserId actor,
        Guid releaseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.RetentionRelease.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        RetentionReleaseRow? release = await _database.RetentionReleases
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == releaseId, cancellationToken)
            .ConfigureAwait(false);

        if (release is null)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound(ReleaseDocument, releaseId));
        }

        RetentionMovementRow? movement = await MovementAsync(
            tenant, release.RetentionMovementId, RetentionSide.Payable, cancellationToken).ConfigureAwait(false);

        if (movement is null)
        {
            return Result<ProjectsDocumentView>.Failure(
                ProjectsErrors.RetentionMovementNotFound(release.RetentionMovementId));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = ReleaseDocument,
            DocumentId = release.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode("projects.retention.released"),
            DocumentDate = release.ReleasedOn,
            Narration = new LocalizedName(
                "إفراج عن محتجز " + release.Number,
                "Retention release " + release.Number),
            Amounts = [new PostingAmount("amount", Money.Of(release.Amount, _currency))],
            Facts = [new PostingFact(SubcontractorFact, movement.PartyId)],
            Dimensions = [new PostingDimension(ProjectsPostingGateway.ProjectDimension, movement.ProjectCode)],
            PartyId = movement.PartyId,
            SubledgerKind = SubcontractorAdvanceService.SubcontractorSubledger,
            ControlEffect = 0m,
            Currency = _currency,
            Actor = actor,
            Generation = release.PostingGeneration,
        };

        Result<PostingReceipt> receipt = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (receipt.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(receipt.Errors);
        }

        if (!receipt.Value.WasAlreadyPosted)
        {
            release.State = ProjectsDocumentState.Posted;
            release.PostedEntryId = receipt.Value.JournalEntryId;
            AddRetentionMovement(tenant, movement, ReleaseDocument, release.Id, intent.Event.Value, -release.Amount, release.ReleasedOn);
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<ProjectsDocumentView>.Success(new ProjectsDocumentView(
            release.Id,
            release.Number,
            release.State,
            Money.Of(release.Amount, _currency),
            receipt.Value.JournalEntryId,
            receipt.Value.WasAlreadyPosted));
    }

    /// <summary>يُنشئ تحصيل محتجزٍ مدين من العميل <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ProjectsDocumentView>> DraftCollectionAsync(
        TenantId tenant,
        UserId actor,
        RetentionCollectionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.RetentionCollection.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        if (draft.Amount.Amount <= 0m)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NegativeAmount(nameof(draft.Amount)));
        }

        RetentionMovementRow? movement = await MovementAsync(
            tenant, draft.RetentionMovementId, RetentionSide.Receivable, cancellationToken).ConfigureAwait(false);

        if (movement is null)
        {
            return Result<ProjectsDocumentView>.Failure(
                ProjectsErrors.RetentionMovementNotFound(draft.RetentionMovementId));
        }

        if (await _database.RetentionCollections
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        RetentionCollectionRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            RetentionMovementId = draft.RetentionMovementId,
            Number = draft.Number,
            CollectedOn = draft.CollectedOn,
            State = ProjectsDocumentState.Draft,
            CurrencyCode = _currency.Value,
            Amount = draft.Amount.Amount,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
        };

        _database.RetentionCollections.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ProjectsDocumentView>.Success(
            new ProjectsDocumentView(row.Id, row.Number, row.State, Money.Of(row.Amount, _currency), null, false));
    }

    /// <summary>يقرأ مستند تحصيل محتجز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="collectionId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<ProjectsDocumentView>> GetCollectionAsync(
        TenantId tenant,
        UserId actor,
        Guid collectionId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.RetentionCollection.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        RetentionCollectionRow? row = await _database.RetentionCollections
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == collectionId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound(CollectionDocument, collectionId))
            : Result<ProjectsDocumentView>.Success(new ProjectsDocumentView(
                row.Id, row.Number, row.State, Money.Of(row.Amount, _currency), row.PostedEntryId, false));
    }

    /// <summary>
    /// يرحّل تحصيل المحتجز من العميل.
    /// <para>
    /// <b>وهذا هو المسار الذي يمارس قدرةً في هذه الوحدة</b>: حدثه هو ما تفتحه قدرة
    /// <c>retention</c> في الكتالوج، فيمرّ من بوّابة القبول أوّلاً — وغياب ملفّ القدرات
    /// رفضٌ لا فتح.
    /// </para>
    /// <para>
    /// و<b>البند المعلَّق يحجبه أيضاً</b>: أثرُ هذا القيد على نقطة ضبط العميل يتوقّف على
    /// ما إذا كان المحتجز المدين جزءاً من ضبط العميل أصلاً — وهو تناقضٌ قائم بين ملفَّي
    /// بيانات لم يُغلَق بعد. فكتابةُ أثرٍ هنا اختيارٌ لأحد جوابيه بلا أن يقوله أحد.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="collectionId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ProjectsDocumentView>> PostCollectionAsync(
        TenantId tenant,
        UserId actor,
        Guid collectionId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.RetentionCollection.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        Result<AdmittedDocument> admitted = await _admission
            .AdmitCertificateAsync(
                tenant,
                [ProjectsAdmission.ContractField, ProjectsAdmission.WorkValueField, ProjectsAdmission.RetentionField],
                cancellationToken)
            .ConfigureAwait(false);

        if (admitted.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(admitted.Errors);
        }

        Result covers = ProjectsAdmission.EnsureCovers(admitted.Value, ProjectsAdmission.RetentionField);
        if (covers.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(covers.Errors);
        }

        RetentionCollectionRow? collection = await _database.RetentionCollections
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == collectionId, cancellationToken)
            .ConfigureAwait(false);

        if (collection is null)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound(CollectionDocument, collectionId));
        }

        RetentionMovementRow? movement = await MovementAsync(
            tenant, collection.RetentionMovementId, RetentionSide.Receivable, cancellationToken).ConfigureAwait(false);

        if (movement is null)
        {
            return Result<ProjectsDocumentView>.Failure(
                ProjectsErrors.RetentionMovementNotFound(collection.RetentionMovementId));
        }

        Result settled = await ContractPolicyGate
            .EnsureSettledAsync(_database, tenant.Value, movement.ContractId, cancellationToken)
            .ConfigureAwait(false);

        if (settled.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(settled.Errors);
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = CollectionDocument,
            DocumentId = collection.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode("projects.client_retention.collected"),
            DocumentDate = collection.CollectedOn,
            Narration = new LocalizedName(
                "تحصيل محتجز من العميل " + collection.Number,
                "Client retention collection " + collection.Number),
            Amounts = [new PostingAmount("amount", Money.Of(collection.Amount, _currency))],
            Facts =
            [
                new PostingFact(CustomerFact, movement.PartyId),
                new PostingFact(TreasurySubledgerFact, collection.TreasuryPartyId),
                new PostingFact(SettlementMethodFact, collection.SettlementMethod),
            ],
            Dimensions = [new PostingDimension(ProjectsPostingGateway.ProjectDimension, movement.ProjectCode)],
            PartyId = movement.PartyId,
            SubledgerKind = CustomerSubledger,
            ControlEffect = -collection.Amount,
            Currency = _currency,
            Actor = actor,
            Generation = collection.PostingGeneration,
        };

        Result<PostingReceipt> receipt = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (receipt.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(receipt.Errors);
        }

        if (!receipt.Value.WasAlreadyPosted)
        {
            collection.State = ProjectsDocumentState.Posted;
            collection.PostedEntryId = receipt.Value.JournalEntryId;
            AddRetentionMovement(
                tenant, movement, CollectionDocument, collection.Id, intent.Event.Value,
                -collection.Amount, collection.CollectedOn);
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<ProjectsDocumentView>.Success(new ProjectsDocumentView(
            collection.Id,
            collection.Number,
            collection.State,
            Money.Of(collection.Amount, _currency),
            receipt.Value.JournalEntryId,
            receipt.Value.WasAlreadyPosted));
    }

    /// <summary>
    /// سجلّ المحتجزات مدينةً ودائنة — <b>مشتقٌّ من المُرحَّل</b>، وهو ما تُطابَق به
    /// نقطتا الضبط على الجانبين.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">تاريخ القراءة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<RetentionRegister>> ReadRegisterAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.RetentionRegister.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<RetentionRegister>.Failure(gate.Errors);
        }

        List<RetentionMovementRow> movements = await _database.RetentionMovements
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.MovedOn <= asOf)
            .OrderBy(row => row.MovedOn)
            .ThenBy(row => row.DocumentId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // الرصيد القائم لكل دفعةٍ أصلية = مبلغُها ناقص ما خرج عنها. والحركات السالبة
        // هي الخروج، ومفتاح الضمّ هو الدفعة الأصلية بمشروعها وطرفها وجانبها.
        List<RetentionRegisterRow> rows = [];

        foreach (RetentionMovementRow movement in movements.Where(static row => row.Amount > 0m))
        {
            decimal consumed = movements
                .Where(other => other.Amount < 0m
                                && other.Side == movement.Side
                                && string.Equals(other.PartyId, movement.PartyId, StringComparison.Ordinal)
                                && other.ContractId == movement.ContractId)
                .Sum(static other => -other.Amount);

            decimal outstanding = movement.Amount - consumed;

            rows.Add(new RetentionRegisterRow(
                movement.Id,
                movement.Side,
                movement.PartyKind,
                movement.PartyId,
                movement.ProjectCode,
                movement.DocumentType,
                movement.DocumentId,
                Money.Of(movement.Amount, _currency),
                Money.Of(outstanding < 0m ? 0m : outstanding, _currency),
                movement.MovedOn,
                movement.DueOn));
        }

        decimal receivable = rows
            .Where(static row => string.Equals(row.Side, RetentionSide.Receivable, StringComparison.Ordinal))
            .Sum(static row => row.Outstanding.Amount);

        decimal payable = rows
            .Where(static row => string.Equals(row.Side, RetentionSide.Payable, StringComparison.Ordinal))
            .Sum(static row => row.Outstanding.Amount);

        return Result<RetentionRegister>.Success(new RetentionRegister(
            asOf, rows, Money.Of(receivable, _currency), Money.Of(payable, _currency)));
    }

    // ── مشترك ────────────────────────────────────────────────────────────────

    private async Task<RetentionMovementRow?> MovementAsync(
        TenantId tenant,
        Guid movementId,
        string side,
        CancellationToken cancellationToken)
        => await _database.RetentionMovements
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.Id == movementId && row.Side == side,
                cancellationToken)
            .ConfigureAwait(false);

    private void AddRetentionMovement(
        TenantId tenant,
        RetentionMovementRow origin,
        string documentType,
        Guid documentId,
        string eventCode,
        decimal amount,
        DateOnly movedOn)
        => _database.RetentionMovements.Add(new RetentionMovementRow
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Side = origin.Side,
            PartyKind = origin.PartyKind,
            PartyId = origin.PartyId,
            ProjectCode = origin.ProjectCode,
            ContractId = origin.ContractId,
            DocumentType = documentType,
            DocumentId = documentId.ToString("D", CultureInfo.InvariantCulture),
            EventCode = eventCode,
            Amount = amount,
            MovedOn = movedOn,
            DueOn = origin.DueOn,
        });
}
