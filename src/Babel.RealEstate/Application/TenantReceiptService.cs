using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.RealEstate.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.RealEstate.Application;

/// <summary>
/// التحصيل من المستأجرين: سند القبض، وترحيله، وتخصيصه.
/// <para>
/// <b>وحدثان لا حدث واحد، والفارق مرجعٌ لا مبلغ:</b> سندٌ يحمل مستأجراً معلوماً يُرحَّل
/// <c>realestate.collection.received</c> فيُسقط من ذمّته؛ ومبلغٌ ورد في الحساب البنكي
/// بلا مرجع يربطه بأحد يُرحَّل <c>realestate.collection.unallocated</c> إلى حساب
/// التحصيلات غير المخصَّصة — <b>ولا يُنسب إلى مستأجر بالتخمين</b>.
/// </para>
/// <para>
/// <b>والتخصيص قيدٌ مستقلّ لا عكسٌ للقيد السابق</b> (‏<c>realestate.collection.allocated</c>):
/// ينقل من التحصيلات غير المخصَّصة إلى ذمم المستأجرين. والعكس كان سيمحو واقعةً وقعت —
/// المال وصل فعلاً — ويترك الدفتر يقول إنه لم يصل.
/// </para>
/// </summary>
public sealed class TenantReceiptService : IApplicationService
{
    /// <summary>نوع المستند في هوية الترحيل.</summary>
    internal const string DocumentType = "realestate.tenant_receipt";

    private const string ReceivedEvent = "realestate.collection.received";
    private const string UnallocatedEvent = "realestate.collection.unallocated";
    private const string AllocatedEvent = "realestate.collection.allocated";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly RealEstateDbContext _database;
    private readonly RealEstatePostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل عبر العقد.</param>
    public TenantReceiptService(IEntitlementEnforcer enforcer, RealEstateRuntime runtime, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
        _gateway = new RealEstatePostingGateway(runtime.Database, posting, runtime.CostCenters);
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>ينشئ سند قبض <b>مسوّدة</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<TenantReceiptView>> DraftAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        TenantReceiptDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.TenantReceipt.Draft", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<TenantReceiptView>.Failure(gate.Errors);
        }

        if (draft.LesseeId is { } lesseeId
            && !await _database.Parties
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId
                           && row.Id == lesseeId && row.PartyRole == PartyRoles.Lessee,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Lessee, lesseeId));
        }

        if (await _database.TenantReceipts
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Number == draft.Number,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.DuplicateCode(draft.Number));
        }

        TenantReceiptRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            CompanyId = companyId,
            Number = draft.Number,
            LesseeId = draft.LesseeId,
            ReceivedOn = draft.ReceivedOn,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            Received = draft.Received.Amount,
            State = RealEstateDocumentState.Draft,

            // ‏**الحدث من غياب المرجع لا من حقلٍ يختاره العميل**.
            EventCode = draft.LesseeId is null ? UnallocatedEvent : ReceivedEvent,
        };

        _database.TenantReceipts.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<TenantReceiptView>.Success(View(row, alreadyPosted: false));
    }

    /// <summary>يقرأ سند قبض.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="receiptId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public async ValueTask<Result<TenantReceiptView>> ReadAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, "RealEstate.TenantReceipt.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<TenantReceiptView>.Failure(gate.Errors);
        }

        TenantReceiptRow? row = await _database.TenantReceipts
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId && entity.Id == receiptId,
                cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<TenantReceiptView>.Failure(RealEstateErrors.DocumentNotFound(DocumentType, receiptId))
            : Result<TenantReceiptView>.Success(View(row, alreadyPosted: false));
    }

    /// <summary>يُرحّل سند القبض بالحدث الذي اختاره غيابُ المرجع أو حضوره.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="receiptId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<TenantReceiptView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.TenantReceipt.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<TenantReceiptView>.Failure(gate.Errors);
        }

        TenantReceiptRow? receipt = await _database.TenantReceipts
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Id == receiptId,
                cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.DocumentNotFound(DocumentType, receiptId));
        }

        string lesseeCode = string.Empty;
        if (receipt.LesseeId is { } lesseeId)
        {
            PartyRow? lessee = await _database.Parties
                .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == lesseeId, cancellationToken)
                .ConfigureAwait(false);

            if (lessee is null)
            {
                return Result<TenantReceiptView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Lessee, lesseeId));
            }

            lesseeCode = lessee.Code;
        }

        bool allocatedToATenant = string.Equals(receipt.EventCode, ReceivedEvent, StringComparison.Ordinal);
        string amountName = allocatedToATenant ? "collected" : "amount";

        List<PostingFact> facts =
        [
            new PostingFact("document.settlement_method", receipt.SettlementMethod),

            // ── دَينٌ تقني مُعلَن يُدفَع هنا بواقعتين لا بواحدة ────────────────────
            // مسار القالب يحوّل `subledger: "resolved"` إلى `"none"` ثم يطلب الواقعة
            // `subledger.none`، بينما السطر المصرَّح بدفتر `bank_account` يطلب
            // `subledger.bank_account` — **على حساب الرقابة نفسه** (1201). فتُسلَّم
            // الواقعتان بالقيمة نفسها، وإلا رُفض أحد الحدثين بـ«دفتر مساعد بلا طرف».
            // وإصلاحه قرارٌ هندسي في الدفتر لا في هذه الوحدة (فخ مُسجَّل).
            new PostingFact("subledger.none", receipt.TreasuryPartyId),
            new PostingFact("subledger.bank_account", receipt.TreasuryPartyId),
        ];

        if (allocatedToATenant)
        {
            facts.Add(new PostingFact("subledger.tenant", lesseeCode));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = DocumentType,
            DocumentId = receipt.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode(receipt.EventCode),
            DocumentDate = receipt.ReceivedOn,
            Narration = new LocalizedName(
                "سند قبض " + receipt.Number,
                "Tenant receipt " + receipt.Number),
            Amounts = [new PostingAmount(amountName, Money.Of(receipt.Received, _currency))],
            Facts = facts,
            PartyId = lesseeCode,
            ControlEffect = allocatedToATenant ? -receipt.Received : 0m,
            Currency = _currency,
            Actor = actor,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (posted.IsFailure)
        {
            return Result<TenantReceiptView>.Failure(posted.Errors);
        }

        receipt.State = RealEstateDocumentState.Posted;
        receipt.EntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<TenantReceiptView>.Success(View(receipt, posted.Value.WasAlreadyPosted));
    }

    /// <summary>
    /// يخصّص سنداً رُحّل غير مخصَّص على مستأجر — <b>بقيدٍ مستقلّ</b>.
    /// <para>
    /// ورمز الحدث داخل هوية الترحيل، فالقيدان — التحصيل والتخصيص — على المستند نفسه
    /// وعند إطلاقين مختلفين لا يتصادمان ولا يبتلع أحدهما الآخر.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="receiptId">السند.</param>
    /// <param name="lesseeId">المستأجر الذي تبيّن أن المبلغ له.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<TenantReceiptView>> AllocateAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid receiptId,
        Guid lesseeId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.TenantReceipt.Allocate", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<TenantReceiptView>.Failure(gate.Errors);
        }

        TenantReceiptRow? receipt = await _database.TenantReceipts
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Id == receiptId,
                cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.DocumentNotFound(DocumentType, receiptId));
        }

        if (!string.Equals(receipt.State, RealEstateDocumentState.Posted, StringComparison.Ordinal))
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.ReceiptIsNotPosted(receiptId));
        }

        if (!string.Equals(receipt.EventCode, UnallocatedEvent, StringComparison.Ordinal))
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.ReceiptWasNotUnallocated(receiptId));
        }

        if (receipt.IsAllocated)
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.ReceiptIsAlreadyAllocated(receiptId));
        }

        if (lesseeId == Guid.Empty)
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.AllocationNeedsALessee);
        }

        PartyRow? lessee = await _database.Parties
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.CompanyId == companyId
                       && row.Id == lesseeId && row.PartyRole == PartyRoles.Lessee,
                cancellationToken)
            .ConfigureAwait(false);

        if (lessee is null)
        {
            return Result<TenantReceiptView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Lessee, lesseeId));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = DocumentType,
            DocumentId = receipt.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode(AllocatedEvent),
            DocumentDate = receipt.ReceivedOn,
            Narration = new LocalizedName(
                "تخصيص سند قبض " + receipt.Number,
                "Allocation of tenant receipt " + receipt.Number),
            Amounts = [new PostingAmount("amount", Money.Of(receipt.Received, _currency))],
            Facts = [new PostingFact("subledger.tenant", lessee.Code)],
            PartyId = lessee.Code,
            ControlEffect = -receipt.Received,
            Currency = _currency,
            Actor = actor,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);

        if (posted.IsFailure)
        {
            return Result<TenantReceiptView>.Failure(posted.Errors);
        }

        receipt.IsAllocated = true;
        receipt.LesseeId = lesseeId;
        receipt.AllocationEntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<TenantReceiptView>.Success(View(receipt, posted.Value.WasAlreadyPosted));
    }

    private TenantReceiptView View(TenantReceiptRow row, bool alreadyPosted) => new(
        row.Id,
        row.Number,
        row.State,
        Money.Of(row.Received, _currency),
        row.EventCode,
        row.EntryId,
        row.IsAllocated,
        row.AllocationEntryId,
        alreadyPosted);
}
