using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Projects.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Projects.Application;

/// <summary>
/// دفعة مقدمة تُصرف لمقاول من الباطن — أنشئ · اقرأ · رحّل.
/// <para>
/// <b>وهذا هو المستند الوحيد في هذه الوحدة الذي يُرحَّل فعلاً اليوم</b>، ولسببٍ واحد:
/// مبلغُه <b>واقعةٌ يُدخلها المستخدم</b> — ما صُرف — لا رقمٌ يشتقّه حاسبٌ من نسبةٍ ووعاءٍ
/// وقاعدةِ تقريب. فلا بند معلَّق فيه، فلا شيء يمنع قيده.
/// </para>
/// <para>
/// و<b>أصلٌ لا مصروف</b> بنصّ الحدث: «يُستنفَد باستقطاعات المستخلصات». وقيدُه سطران —
/// دورٌ يُدين ودورٌ يُدان بمؤهّلٍ من طريقة التسوية — <b>ولا تسمّي الوحدة حساباً</b>.
/// </para>
/// </summary>
public sealed class SubcontractorAdvanceService : IApplicationService
{
    /// <summary>نوع المستند في هوية الترحيل.</summary>
    internal const string AdvanceDocument = "ProjectsSubcontractorAdvance";

    /// <summary>نوع الدفتر المساعد الذي يتحرّك بهذا المستند.</summary>
    internal const string SubcontractorSubledger = "subcontractor";

    /// <summary>
    /// واقعة طرف الخزينة.
    /// <para>
    /// <b>والاسم مضلّل والسلوك موثَّق:</b> سطر التسوية في القالب <c>subledger: resolved</c>،
    /// والمُخطِّط يترجمه إلى <c>none</c> ثم يبحث عن الواقعة <c>subledger.none</c>. وحسابا
    /// النقد والبنك يحملان نوع دفترٍ مساعد غير <c>none</c>، فبلا هذه الواقعة يُرفض السطر
    /// بـ<c>MissingSubledger</c> بلا طرف — وهي السابقة الحرفية في مسار سند القبض.
    /// </para>
    /// </summary>
    private const string TreasurySubledgerFact = "subledger.none";

    /// <summary>واقعة طريقة التسوية — <b>مصدر مؤهّل</b> سطر التسوية، والوقوعُ على الافتراضي مرفوض.</summary>
    private const string SettlementMethodFact = "document.settlement_method";

    /// <summary>واقعة طرف المقاول في دفتره المساعد.</summary>
    private const string SubcontractorFact = "subledger.subcontractor";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly ProjectsDbContext _database;
    private readonly ProjectsPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرّك الترحيل — يصله الجذر التركيبي بالدفتر.</param>
    public SubcontractorAdvanceService(
        IEntitlementEnforcer enforcer,
        ProjectsRuntime runtime,
        IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
        _gateway = new ProjectsPostingGateway(runtime.Database, posting, runtime.CostCenters);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>يُنشئ صرف دفعة مقدمة <b>مسوّدة</b>: لا قيد ولا أثر في الدفتر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ProjectsDocumentView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        SubcontractorAdvanceDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.SubcontractorAdvance.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        if (draft.Amount.Amount <= 0m)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NegativeAmount(nameof(draft.Amount)));
        }

        SubcontractRow? subcontract = await _database.Subcontracts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.SubcontractId, cancellationToken)
            .ConfigureAwait(false);

        if (subcontract is null)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound("subcontract", draft.SubcontractId));
        }

        if (!string.Equals(subcontract.CurrencyCode, draft.Amount.Currency.Value, StringComparison.Ordinal))
        {
            return Result<ProjectsDocumentView>.Failure(
                ProjectsErrors.CurrencyMismatch(draft.Amount.Currency.Value, subcontract.CurrencyCode));
        }

        if (await _database.SubcontractorAdvances
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.DuplicateNumber(draft.Number));
        }

        if (draft.GuaranteeId is { } guaranteeId && !await _database.Guarantees
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == guaranteeId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound("guarantee", guaranteeId));
        }

        SubcontractorAdvanceRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            SubcontractId = draft.SubcontractId,
            Number = draft.Number,
            PaidOn = draft.PaidOn,
            State = ProjectsDocumentState.Draft,
            CurrencyCode = subcontract.CurrencyCode,
            Amount = draft.Amount.Amount,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            GuaranteeId = draft.GuaranteeId,
        };

        _database.SubcontractorAdvances.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ProjectsDocumentView>.Success(View(row, alreadyPosted: false));
    }

    /// <summary>
    /// يقرأ الدفعة المقدمة. <b>والرصيد مشتقٌّ من المُرحَّل وحده</b> لا عمودٌ يُنقَص.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Read)]
    public async ValueTask<Result<ProjectsDocumentView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Read, "Projects.SubcontractorAdvance.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        SubcontractorAdvanceRow? row = await _database.SubcontractorAdvances
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == advanceId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound(AdvanceDocument, advanceId))
            : Result<ProjectsDocumentView>.Success(View(row, alreadyPosted: false));
    }

    /// <summary>
    /// يرحّل صرف الدفعة المقدمة.
    /// <para>
    /// <b>حصينٌ ضد التكرار، والحكم من بوّابة الترحيل لا من مقارنة حالةٍ على المستند:</b>
    /// الوصول الثاني بالهوية نفسها يُرجع <b>معرّف القيد الأول</b> و<c>alreadyPosted = true</c>
    /// ولا يُنشئ قيداً ثانياً — حتى لو تغيّرت حالة المستند بغير هذا المسار.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Projects, EntitlementAccess.Write)]
    public async ValueTask<Result<ProjectsDocumentView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Projects, EntitlementAccess.Write, "Projects.SubcontractorAdvance.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(gate.Errors);
        }

        SubcontractorAdvanceRow? advance = await _database.SubcontractorAdvances
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == advanceId, cancellationToken)
            .ConfigureAwait(false);

        if (advance is null)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound(AdvanceDocument, advanceId));
        }

        SubcontractRow? subcontract = await _database.Subcontracts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == advance.SubcontractId, cancellationToken)
            .ConfigureAwait(false);

        if (subcontract is null)
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.NotFound("subcontract", advance.SubcontractId));
        }

        // ‏**رمز المشروع يُتحقَّق منه قبل بناء الطلب.** قيمة بُعد المشروع في الدفتر نصٌّ
        // حرّ بلا سجلّ ولا مفتاح أجنبي، ومشغّل الدليل يفحص الغياب وحده — فسلسلةٌ فارغة
        // أو رمزٌ مخطوء يعبران إلى القيد ولا يمسكهما شيء هناك.
        string projectCode = await _database.Projects
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.Id == subcontract.ProjectId)
            .Select(row => row.Code)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false) ?? string.Empty;

        if (string.IsNullOrWhiteSpace(projectCode))
        {
            return Result<ProjectsDocumentView>.Failure(ProjectsErrors.ProjectCodeNotRegistered(projectCode));
        }

        string subcontractorId = subcontract.SubcontractorId.ToString("D", CultureInfo.InvariantCulture);
        Money amount = Money.Of(advance.Amount, _currency);

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = AdvanceDocument,
            DocumentId = advance.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode("projects.subcontractor_advance.paid"),
            DocumentDate = advance.PaidOn,
            Narration = new LocalizedName(
                "دفعة مقدمة لمقاول من الباطن " + advance.Number,
                "Subcontractor advance " + advance.Number),
            Amounts = [new PostingAmount("amount", amount)],
            Facts =
            [
                // طرف المقاول في دفتره المساعد — سطر الأصل يحمله.
                new PostingFact(SubcontractorFact, subcontractorId),

                // وطرف الخزينة تحت الاسم المضلّل `subledger.none`، وإلا رُفض سطر
                // التسوية بـMissingSubledger بلا طرف.
                new PostingFact(TreasurySubledgerFact, advance.TreasuryPartyId),

                // ومؤهّل سطر التسوية، وإلا رُفض بـMissingQualifier: الوقوع على المؤهّل
                // الافتراضي يختار حساباً آخر بصمت.
                new PostingFact(SettlementMethodFact, advance.SettlementMethod),
            ],

            // بُعد المشروع على الطلب — والمُخطِّط يستنسخه على سطور القيد كلّها، وحساب
            // الدفعة المقدمة للمقاولين يفرضه. ولا بُعد بند هنا: القيد بمبالغ المستند.
            Dimensions = [new PostingDimension(ProjectsPostingGateway.ProjectDimension, projectCode)],
            PartyId = subcontractorId,
            SubledgerKind = SubcontractorSubledger,

            // مدينٌ على دفتر المقاول: «مدين ناقص دائن» موجبٌ بمبلغ الدفعة.
            ControlEffect = advance.Amount,
            Currency = _currency,
            Actor = actor,
            Generation = advance.PostingGeneration,
        };

        Result<PostingReceipt> receipt = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (receipt.IsFailure)
        {
            return Result<ProjectsDocumentView>.Failure(receipt.Errors);
        }

        if (!receipt.Value.WasAlreadyPosted)
        {
            advance.State = ProjectsDocumentState.Posted;
            advance.PostedEntryId = receipt.Value.JournalEntryId;

            _database.AdvanceMovements.Add(new AdvanceMovementRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                PartyKind = SubcontractorSubledger,
                PartyId = subcontractorId,
                ContractId = advance.SubcontractId,
                DocumentType = AdvanceDocument,
                DocumentId = advance.Id.ToString("D", CultureInfo.InvariantCulture),
                EventCode = intent.Event.Value,
                Amount = advance.Amount,
                MovedOn = advance.PaidOn,
            });

            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<ProjectsDocumentView>.Success(
            View(advance, receipt.Value.WasAlreadyPosted, receipt.Value.JournalEntryId));
    }

    private ProjectsDocumentView View(SubcontractorAdvanceRow row, bool alreadyPosted, Guid? entryId = null) => new(
        row.Id,
        row.Number,
        row.State,
        Money.Of(row.Amount, _currency),
        entryId ?? row.PostedEntryId,
        alreadyPosted);
}
