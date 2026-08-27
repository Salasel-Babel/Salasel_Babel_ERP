using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>
/// سندات الصرف وتخصيصها، وتكاليف الاستيراد المُحمَّلة على المخزون.
/// <para>
/// <b>رسوم التحويل ليست ذمة مورد.</b> سندٌ يُخصم من الخزينة بالمبلغ زائد الرسوم،
/// وينقص ذمة المورد بالمبلغ وحده — وخلطهما يجعل رصيد المورد أقلّ مما هو، فتظهر
/// مطالبة لا يعرف أحد مصدرها بعد أشهر.
/// </para>
/// </summary>
public sealed class SupplierPaymentService : IApplicationService
{
    /// <summary>نوع مستند سند الصرف في هوية الإحكام.</summary>
    internal const string PaymentDocument = "SupplierPayment";

    /// <summary>نوع مستند تكلفة الاستيراد.</summary>
    internal const string LandedCostDocument = "LandedCost";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly PurchasingAdmission _admission;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل.</param>
    /// <param name="profiles">مخزن ملفّات القدرات — بوابة القبول (‏ADR-0023).</param>
    public SupplierPaymentService(
        IEntitlementEnforcer enforcer,
        PurchasingRuntime runtime,
        IPostingService posting,
        ICapabilityProfileStore profiles)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(profiles);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
        _gateway = new SubledgerPostingGateway(_database, posting, runtime.CostCenters);
        _admission = new PurchasingAdmission(profiles);
    }

    /// <summary>يسجّل سند صرف بتخصيصاته على فواتير الموردين.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> RecordPaymentAsync(
        TenantId tenant,
        UserId actor,
        SupplierPaymentDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Payment.Record", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        if (draft.Paid.Amount < 0m || draft.BankFee.Amount < 0m)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.NegativeAmount);
        }

        decimal requested = draft.Allocations.Sum(static allocation => allocation.Amount.Amount);
        if (requested > draft.Paid.Amount)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.OverAllocation(draft.Number, requested, draft.Paid.Amount));
        }

        if (await _database.Payments
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        Guid paymentId = Guid.CreateVersion7();
        int lineNo = 0;

        // الفحص كاملاً قبل أي كتابة: متعقّب EF يحتفظ بما كُتب قبل الرفض.
        List<PayableAllocationDraft> accepted = [];

        foreach (PayableAllocationDraft allocation in draft.Allocations.OrderBy(static a => a.BillId))
        {
            SupplierBillRow? bill = await _database.Bills
                .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == allocation.BillId, cancellationToken)
                .ConfigureAwait(false);

            if (bill is null)
            {
                return Result<PurchasingDocumentView>.Failure(
                    PurchasingErrors.DocumentNotFound(SupplierBillService.BillDocument, allocation.BillId));
            }

            if (bill.State != PurchasingDocumentState.Posted)
            {
                return Result<PurchasingDocumentView>.Failure(
                    PurchasingErrors.NotInState(bill.Number, bill.State, PurchasingDocumentState.Posted));
            }

            decimal outstanding = bill.GrossTotal - bill.AllocatedAmount;
            if (allocation.Amount.Amount > outstanding)
            {
                return Result<PurchasingDocumentView>.Failure(
                    PurchasingErrors.OverAllocation(bill.Number, allocation.Amount.Amount, outstanding));
            }

            accepted.Add(allocation);
        }

        foreach (PayableAllocationDraft allocation in accepted)
        {
            lineNo++;
            _database.Allocations.Add(new PayableAllocationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                SourceType = "PAYMENT",
                SourceId = paymentId,
                BillId = allocation.BillId,
                LineNo = lineNo,
                AllocatedAmount = allocation.Amount.Amount,
                AllocatedOn = draft.PaidOn,
            });
        }

        SupplierPaymentRow payment = new()
        {
            Id = paymentId,
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = draft.SupplierId,
            PaidOn = draft.PaidOn,
            State = PurchasingDocumentState.Draft,
            CurrencyCode = _currency.Value,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            PaidAmount = draft.Paid.Amount,
            BankFee = draft.BankFee.Amount,
            AllocatedAmount = requested,
        };

        _database.Payments.Add(payment);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(ViewOfPayment(payment));
    }

    /// <summary>يرحّل سند الصرف ويُنزل تخصيصاته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="paymentId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> PostPaymentAsync(
        TenantId tenant,
        UserId actor,
        Guid paymentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Payment.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        SupplierPaymentRow? payment = await _database.Payments
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == paymentId, cancellationToken)
            .ConfigureAwait(false);

        if (payment is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(PaymentDocument, paymentId));
        }

        if (payment.State == PurchasingDocumentState.Posted)
        {
            return Result<PurchasingDocumentView>.Success(ViewOfPayment(payment));
        }

        SupplierRow supplier = await _database.Suppliers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == payment.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = PaymentDocument,
            DocumentId = payment.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode("purchasing.payment.posted"),
            DocumentDate = payment.PaidOn,
            Narration = new LocalizedName("سند صرف " + payment.Number, "Supplier payment " + payment.Number),
            Amounts =
            [
                new PostingAmount("paid", Money.Of(payment.PaidAmount, _currency)),
                new PostingAmount("fee", Money.Of(payment.BankFee, _currency)),
            ],
            Facts =
            [
                new PostingFact("condition.has_bank_fee", payment.BankFee > 0m ? "true" : "false"),
                new PostingFact("document.settlement_method", payment.SettlementMethod),
                new PostingFact("subledger.supplier", supplier.Code),
                new PostingFact("subledger.none", payment.TreasuryPartyId),
            ],
            PartyId = supplier.Code,
            ControlEffect = -payment.PaidAmount,
            Currency = _currency,
            Actor = actor,
            Generation = payment.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(posted.Errors);
        }

        payment.State = PurchasingDocumentState.Posted;
        payment.PostedEntryId = posted.Value.JournalEntryId;

        List<PayableAllocationRow> allocations = await _database.Allocations
            .Where(row => row.SourceType == "PAYMENT" && row.SourceId == payment.Id)
            .OrderBy(row => row.BillId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (PayableAllocationRow allocation in allocations)
        {
            SupplierBillRow bill = await _database.Bills
                .FirstAsync(row => row.Id == allocation.BillId, cancellationToken)
                .ConfigureAwait(false);
            bill.AllocatedAmount += allocation.AllocatedAmount;
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<PurchasingDocumentView>.Success(ViewOfPayment(payment));
    }

    /// <summary>يسجّل تكلفة استيراد مُحمَّلة على استلام.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> RecordLandedCostAsync(
        TenantId tenant,
        UserId actor,
        LandedCostDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.LandedCost.Record", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        GoodsReceiptRow? receipt = await _database.Receipts
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.ReceiptId, cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound("GoodsReceipt", draft.ReceiptId));
        }

        if (await _database.LandedCosts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        LandedCostRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = draft.SupplierId,
            ReceiptId = receipt.Id,
            IncurredOn = draft.IncurredOn,
            State = PurchasingDocumentState.Draft,
            CurrencyCode = _currency.Value,
            WarehouseId = receipt.WarehouseId,
            ItemGroup = draft.ItemGroup,
            ItemId = draft.ItemId,
            Source = draft.Source,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            CostAmount = draft.Cost.Amount,
        };

        _database.LandedCosts.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(ViewOfLandedCost(row));
    }

    /// <summary>يرحّل تكلفة الاستيراد عبر <c>purchasing.landed_cost.allocated</c>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="landedCostId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> PostLandedCostAsync(
        TenantId tenant,
        UserId actor,
        Guid landedCostId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.LandedCost.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        // تكلفة الاستيراد المحمَّلة قدرةٌ قائمة بذاتها، وحدثها تفتحه وحدها.
        Result<AdmittedDocument> admitted = await _admission
            .AdmitBillAsync(
                tenant,
                [PurchasingAdmission.SupplierField, PurchasingAdmission.LinesField, PurchasingAdmission.LandedCostField],
                cancellationToken)
            .ConfigureAwait(false);

        if (admitted.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(admitted.Errors);
        }

        return await PostAdmittedLandedCostAsync(tenant, actor, admitted.Value, landedCostId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>الكاتب الوحيد لقيد تكلفة الاستيراد — ويطلب <see cref="AdmittedDocument"/> في توقيعه.</b>
    /// </summary>
    private async ValueTask<Result<PurchasingDocumentView>> PostAdmittedLandedCostAsync(
        TenantId tenant,
        UserId actor,
        AdmittedDocument admitted,
        Guid landedCostId,
        CancellationToken cancellationToken)
    {
        Result covers = PurchasingAdmission.EnsureCovers(admitted, PurchasingAdmission.LandedCostField);
        if (covers.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(covers.Errors);
        }

        LandedCostRow? cost = await _database.LandedCosts
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == landedCostId, cancellationToken)
            .ConfigureAwait(false);

        if (cost is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(LandedCostDocument, landedCostId));
        }

        if (cost.State == PurchasingDocumentState.Posted)
        {
            return Result<PurchasingDocumentView>.Success(ViewOfLandedCost(cost));
        }

        SupplierRow supplier = await _database.Suppliers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == cost.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        bool billed = string.Equals(cost.Source, "supplier_invoice", StringComparison.Ordinal);

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = LandedCostDocument,
            DocumentId = cost.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode("purchasing.landed_cost.allocated"),
            DocumentDate = cost.IncurredOn,
            Narration = new LocalizedName("تكلفة استيراد " + cost.Number, "Landed cost " + cost.Number),
            Amounts = [new PostingAmount("landed_cost", Money.Of(cost.CostAmount, _currency))],
            Facts =
            [
                new PostingFact("document.landed_cost_source", cost.Source),
                new PostingFact("document.settlement_method", cost.SettlementMethod),
                new PostingFact("subledger.supplier", supplier.Code),
                new PostingFact("subledger.item", cost.ItemId),
                new PostingFact("subledger.none", cost.TreasuryPartyId),
                new PostingFact("line.item_group", cost.ItemGroup),
            ],
            Dimensions = [new PostingDimension("warehouse", cost.WarehouseId)],
            PartyId = supplier.Code,
            ControlEffect = billed ? cost.CostAmount : 0m,
            Currency = _currency,
            Actor = actor,
            Generation = cost.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(posted.Errors);
        }

        cost.State = PurchasingDocumentState.Posted;
        cost.PostedEntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(ViewOfLandedCost(cost));
    }

    private PurchasingDocumentView ViewOfPayment(SupplierPaymentRow payment) => new(
        payment.Id,
        payment.Number,
        payment.State,
        new DocumentTotals(
            Money.Of(payment.PaidAmount, _currency),
            Money.Of(payment.BankFee, _currency),
            Money.Of(payment.PaidAmount + payment.BankFee, _currency)),
        payment.PostedEntryId);

    private PurchasingDocumentView ViewOfLandedCost(LandedCostRow cost) => new(
        cost.Id,
        cost.Number,
        cost.State,
        new DocumentTotals(
            Money.Of(cost.CostAmount, _currency),
            Money.Of(0m, _currency),
            Money.Of(cost.CostAmount, _currency)),
        cost.PostedEntryId);
}
