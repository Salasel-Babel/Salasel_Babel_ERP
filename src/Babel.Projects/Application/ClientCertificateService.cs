using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// مستخلص العميل — ثلاثة أبواب لا اثنان: أنشئ · اقرأ · رحّل.
/// <para>
/// <b>والمسوّدة تُحفظ والترحيل يُرفض اليوم — وهذا هو المطلوب لا نقصٌ فيه.</b> المصفوفة
/// تفرض أن نسبة المحتجز «تأتي من العقد لا من قيمة ثابتة في الكود»، ولا تقول على أي وعاء
/// تُضرب ولا بأي قاعدة تُستردّ الدفعة المقدمة ولا أين يقع التقريب ولا على أي مستوى
/// يُقرَّر التصنيف الضريبي. والمبالغ الأربعة التي يسمّيها القالب يجب أن يكون
/// <b>لكلٍّ حاسبٌ في هذه الوحدة</b> لا معامِلٌ يُملى من المستدعي. فبلا هذه الأجوبة
/// يصير الترحيل قيداً متوازناً بأرقامٍ اخترعها من نادى الباب — ولا يمسكه توازنٌ ولا
/// حارس.
/// </para>
/// <para>
/// فالوحدة تُخزّن ما هو <b>واقعة مُقاسة</b>: الكمّيات التراكمية والسابقة بوحدتيهما،
/// ونسبة المحتجز مجمَّدةً من العقد لحظة المسوّدة. وترفض الترحيل برمزٍ مستقرّ يسمّي
/// البند المعلَّق. <b>ولا قيمة افتراضية واحدة.</b>
/// </para>
/// </summary>
public sealed class ClientCertificateService : IApplicationService
{
    /// <summary>نوع المستند في هوية الترحيل — لا في الكتالوج.</summary>
    internal const string CertificateDocument = "ProjectsClientCertificate";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly ProjectsDbContext _database;
    private readonly ProjectsAdmission _admission;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="profiles">
    /// مخزن ملفّات القدرات. <b>ويُبنى منه القبول هنا لا يُحقن</b>: البوّابة
    /// <c>internal</c> بحكم القاعدة 5، فلا يجوز أن تظهر في مُنشئٍ عام — وهو الشكل
    /// نفسه المُودَع في وحدة المبيعات وللسبب نفسه.
    /// </param>
    public ClientCertificateService(
        IEntitlementEnforcer enforcer,
        ProjectsRuntime runtime,
        ICapabilityProfileStore profiles)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(profiles);
        _enforcer = enforcer;
        _database = runtime.Database;
        _admission = new ProjectsAdmission(profiles);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// يُنشئ مستخلص عميل <b>مسوّدة</b>: لا قيد ولا أثر في الدفتر.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة بكمّياتها التراكمية.</param>
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
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.ClientCertificate.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CertificateView>.Failure(gate.Errors);
        }

        // ‏**القبول أوّلاً، وغياب الملفّ رفضٌ لا فتح** (ADR-0023 · ADR-0025). والحقلان
        // المعروضان هما الأساسيان في الكتالوج؛ ولا يُعرض حقلُ محتجزٍ ولا استردادِ دفعة
        // لأن الوحدة لا تحمل مبلغاً لأيٍّ منهما بعد — وعرضُ حقلٍ لا يحمله المستند
        // يستهلك قدرةً لم تُمارَس.
        Result<AdmittedDocument> admitted = await _admission
            .AdmitCertificateAsync(
                tenant,
                [ProjectsAdmission.ContractField, ProjectsAdmission.WorkValueField],
                cancellationToken)
            .ConfigureAwait(false);

        if (admitted.IsFailure)
        {
            return Result<CertificateView>.Failure(admitted.Errors);
        }

        Result covers = ProjectsAdmission.EnsureCovers(admitted.Value, ProjectsAdmission.WorkValueField);
        if (covers.IsFailure)
        {
            return Result<CertificateView>.Failure(covers.Errors);
        }

        if (draft.Lines.Count == 0)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NoLines());
        }

        ProjectContractRow? contract = await _database.Contracts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.OwnerId, cancellationToken)
            .ConfigureAwait(false);

        if (contract is null)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NotFound("project_contract", draft.OwnerId));
        }

        if (await _database.ClientCertificates
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<CertificateView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        if (await _database.ClientCertificates
                .AnyAsync(
                    row => row.TenantId == tenant.Value
                           && row.ContractId == draft.OwnerId
                           && row.SequenceNo == draft.SequenceNo,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<CertificateView>.Failure(
                ProjectsErrors.DuplicateSequence(draft.OwnerId, draft.SequenceNo));
        }

        Dictionary<Guid, MeasuredItem> items = await _database.BoqItems
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.ContractId == draft.OwnerId)
            .Select(row => new MeasuredItem(row.Id, row.Code, row.Unit))
            .ToDictionaryAsync(static item => item.Id, cancellationToken)
            .ConfigureAwait(false);

        // الأساس من **المُرحَّل وحده** — لا من آخر مسوّدة (فخ-44).
        List<Guid> posted = await _database.ClientCertificates
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.ContractId == draft.OwnerId
                          && row.State == ProjectsDocumentState.Posted)
            .Select(row => row.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, decimal> previous = await CumulativeLines
            .PreviousQuantitiesAsync(_database, tenant.Value, CertificateOwner.Client, posted, cancellationToken)
            .ConfigureAwait(false);

        ClientCertificateRow certificate = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            ContractId = draft.OwnerId,
            Number = draft.Number,
            SequenceNo = draft.SequenceNo,
            PeriodFrom = draft.PeriodFrom,
            PeriodTo = draft.PeriodTo,
            State = ProjectsDocumentState.Draft,
            CurrencyCode = contract.CurrencyCode,

            // ‏**تُجمَّد لحظة المسوّدة** من العقد: بدونها يُغيّر تعديلٌ على العقد أرقام
            // مستخلصٍ راجعه إنسان.
            FrozenRetentionRate = contract.RetentionRate,
        };

        Result<List<CertificateLineRow>> lines = CumulativeLines.Build(
            tenant.Value, CertificateOwner.Client, certificate.Id, draft.Lines, items, previous);

        if (lines.IsFailure)
        {
            return Result<CertificateView>.Failure(lines.Errors);
        }

        _database.ClientCertificates.Add(certificate);
        _database.CertificateLines.AddRange(lines.Value);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PendingPolicyItem> pending = await ContractPolicyGate
            .PendingAsync(_database, tenant.Value, contract.Id, cancellationToken)
            .ConfigureAwait(false);

        return Result<CertificateView>.Success(
            View(certificate, lines.Value, items, pending, alreadyPosted: false));
    }

    /// <summary>يقرأ مستخلصاً بحالته وسطوره وبنوده المعلَّقة ومعرّف قيده إن رُحّل.</summary>
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
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.ClientCertificate.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CertificateView>.Failure(gate.Errors);
        }

        ClientCertificateRow? certificate = await _database.ClientCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == certificateId, cancellationToken)
            .ConfigureAwait(false);

        if (certificate is null)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NotFound(CertificateDocument, certificateId));
        }

        return Result<CertificateView>.Success(
            await ReadAsync(tenant, certificate, alreadyPosted: false, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// يقرأ مستخلصات العقد. <b>لازمٌ لأن الأساس المطروح منه هو آخر مستخلص مُرحَّل</b>،
    /// وبلا هذا الباب لا يعرفه العميل إلا بمعرّفٍ يملكه سلفاً.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="contractId">العقد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<CertificateView>>> ListAsync(
        TenantId tenant,
        UserId actor,
        Guid contractId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.ClientCertificate.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<CertificateView>>.Failure(gate.Errors);
        }

        if (!await _database.Contracts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == contractId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<IReadOnlyList<CertificateView>>.Failure(
                ProjectsErrors.NotFound("project_contract", contractId));
        }

        List<ClientCertificateRow> rows = await _database.ClientCertificates
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.ContractId == contractId)
            .OrderBy(row => row.SequenceNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<CertificateView> views = [];

        foreach (ClientCertificateRow row in rows)
        {
            views.Add(await ReadAsync(tenant, row, alreadyPosted: false, cancellationToken).ConfigureAwait(false));
        }

        return Result<IReadOnlyList<CertificateView>>.Success(views);
    }

    /// <summary>
    /// يرحّل مستخلصاً — <b>ويُرفض اليوم برمزٍ مستقرّ يسمّي البند المعلَّق</b>.
    /// <para>
    /// والرفض هنا يقع <b>قبل</b> بوّابة الترحيل عمداً: البند المعلَّق شرطٌ على المستند
    /// لا عطلٌ في المحرّك، وكتابة صفّ محاولةٍ لطلبٍ لن يُبنى تُلوّث سجلّ المحاولات
    /// بمحاولات لم تقع.
    /// </para>
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
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.ClientCertificate.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CertificateView>.Failure(gate.Errors);
        }

        ClientCertificateRow? certificate = await _database.ClientCertificates
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == certificateId, cancellationToken)
            .ConfigureAwait(false);

        if (certificate is null)
        {
            return Result<CertificateView>.Failure(ProjectsErrors.NotFound(CertificateDocument, certificateId));
        }

        Result settled = await ContractPolicyGate
            .EnsureSettledAsync(_database, tenant.Value, certificate.ContractId, cancellationToken)
            .ConfigureAwait(false);

        return Result<CertificateView>.Failure(settled.Errors);
    }

    // ── مشترك ────────────────────────────────────────────────────────────────

    private async Task<CertificateView> ReadAsync(
        TenantId tenant,
        ClientCertificateRow certificate,
        bool alreadyPosted,
        CancellationToken cancellationToken)
    {
        List<CertificateLineRow> lines = await _database.CertificateLines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.OwnerType == CertificateOwner.Client
                          && row.OwnerId == certificate.Id)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<Guid, MeasuredItem> items = await _database.BoqItems
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.ContractId == certificate.ContractId)
            .Select(row => new MeasuredItem(row.Id, row.Code, row.Unit))
            .ToDictionaryAsync(static item => item.Id, cancellationToken)
            .ConfigureAwait(false);

        IReadOnlyList<PendingPolicyItem> pending = await ContractPolicyGate
            .PendingAsync(_database, tenant.Value, certificate.ContractId, cancellationToken)
            .ConfigureAwait(false);

        return View(certificate, lines, items, pending, alreadyPosted);
    }

    private CertificateView View(
        ClientCertificateRow certificate,
        IReadOnlyList<CertificateLineRow> lines,
        IReadOnlyDictionary<Guid, MeasuredItem> items,
        IReadOnlyList<PendingPolicyItem> pending,
        bool alreadyPosted) => new(
        certificate.Id,
        certificate.Number,
        certificate.ContractId,
        certificate.SequenceNo,
        certificate.PeriodFrom,
        certificate.PeriodTo,
        certificate.State,
        certificate.FrozenRetentionRate,
        [.. lines.Select(line => Line(line, items, _currency))],
        pending,
        certificate.PostedEntryId,
        alreadyPosted);

    internal static CertificateLineView Line(
        CertificateLineRow line,
        IReadOnlyDictionary<Guid, MeasuredItem> items,
        CurrencyCode currency) => new(
        line.Id,
        line.LineNo,
        line.LineKind,
        line.ItemId,
        line.ItemId is { } id && items.TryGetValue(id, out MeasuredItem? item) ? item.Code : string.Empty,
        line.DescriptionAr,
        new ProjectQuantity(line.CumulativeQuantity, line.Unit),
        new ProjectQuantity(line.PreviousQuantity, line.Unit),
        Money.Of(line.Amount, currency));
}
