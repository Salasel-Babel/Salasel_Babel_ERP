using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Sales.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Application;

/// <summary>
/// سندات القبض والدفعات المقدمة، وتخصيصها على الفواتير.
/// <para>
/// <b>التخصيص الزائد مرفوض على الطرفين:</b> مجموع تخصيصات السند لا يتجاوز ما قُبض
/// (‏المقبوض + خصم التعجيل)، وتخصيص كل فاتورة لا يتجاوز المتبقّي عليها. سندٌ يُخصَّص
/// بأكثر مما فيه يُنتج رصيداً سالباً على عميل لا يقابله شيء في الدفتر.
/// </para>
/// </summary>
public sealed class CustomerReceiptService : IApplicationService
{
    /// <summary>نوع مستند سند القبض في هوية الإحكام.</summary>
    internal const string ReceiptDocument = "CustomerReceipt";

    /// <summary>نوع مستند الدفعة المقدمة.</summary>
    internal const string AdvanceDocument = "CustomerAdvance";

    /// <summary>نوع مستند استنفاد دفعة مقدمة — مستند مستقلّ لكل استنفاد.</summary>
    internal const string AdvanceApplicationDocument = "CustomerAdvanceApplication";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly SalesDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly SalesAdmission _admission;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل.</param>
    /// <param name="profiles">مخزن ملفّات القدرات — بوابة القبول (ADR-0023).</param>
    public CustomerReceiptService(
        IEntitlementEnforcer enforcer,
        SalesRuntime runtime,
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
        _gateway = new SubledgerPostingGateway(_database, posting);
        _admission = new SalesAdmission(profiles);
    }

    /// <summary>يسجّل سند قبض بتخصيصاته. الترحيل خطوة مستقلة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> RecordReceiptAsync(
        TenantId tenant,
        UserId actor,
        CustomerReceiptDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Receipt.Record", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        if (draft.Received.Amount < 0m || draft.SettlementDiscount.Amount < 0m)
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.NegativeAmount);
        }

        decimal settled = draft.Received.Amount + draft.SettlementDiscount.Amount;
        decimal requested = draft.Allocations.Sum(static allocation => allocation.Amount.Amount);

        if (requested > settled)
        {
            return Result<SalesDocumentView>.Failure(
                SalesErrors.OverAllocation(draft.Number, requested, settled));
        }

        if (await _database.Receipts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DuplicateNumber(draft.Number));
        }

        Guid receiptId = Guid.CreateVersion7();
        int lineNo = 0;

        // الفحص كاملاً قبل أي كتابة: متعقّب EF يحتفظ بما كُتب قبل الرفض، فأول
        // SaveChanges لاحق يُثبّته. والترتيب صريح بمعرّف الفاتورة: صفوف متعددة
        // تُكتب دائماً بترتيب كلي ثابت (فخ-10).
        List<AllocationDraft> accepted = [];

        foreach (AllocationDraft allocation in draft.Allocations.OrderBy(static a => a.InvoiceId))
        {
            SalesInvoiceRow? invoice = await _database.Invoices
                .FirstOrDefaultAsync(
                    row => row.TenantId == tenant.Value && row.Id == allocation.InvoiceId, cancellationToken)
                .ConfigureAwait(false);

            if (invoice is null)
            {
                return Result<SalesDocumentView>.Failure(
                    SalesErrors.DocumentNotFound(SalesInvoiceService.InvoiceDocument, allocation.InvoiceId));
            }

            if (invoice.State != SalesDocumentState.Posted)
            {
                return Result<SalesDocumentView>.Failure(
                    SalesErrors.NotInState(invoice.Number, invoice.State, SalesDocumentState.Posted));
            }

            decimal outstanding = invoice.GrossTotal - invoice.AllocatedAmount - invoice.AdvanceApplied;
            if (allocation.Amount.Amount > outstanding)
            {
                return Result<SalesDocumentView>.Failure(
                    SalesErrors.OverAllocation(invoice.Number, allocation.Amount.Amount, outstanding));
            }

            accepted.Add(allocation);
        }

        foreach (AllocationDraft allocation in accepted)
        {
            lineNo++;
            _database.Allocations.Add(new ReceivableAllocationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                SourceType = "RECEIPT",
                SourceId = receiptId,
                InvoiceId = allocation.InvoiceId,
                LineNo = lineNo,
                AllocatedAmount = allocation.Amount.Amount,
                AllocatedOn = draft.ReceivedOn,
            });
        }

        CustomerReceiptRow receipt = new()
        {
            Id = receiptId,
            TenantId = tenant.Value,
            Number = draft.Number,
            CustomerId = draft.CustomerId,
            ReceivedOn = draft.ReceivedOn,
            State = SalesDocumentState.Draft,
            CurrencyCode = _currency.Value,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            ReceivedAmount = draft.Received.Amount,
            DiscountAmount = draft.SettlementDiscount.Amount,
            AllocatedAmount = requested,
        };

        _database.Receipts.Add(receipt);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(ViewOfReceipt(receipt));
    }

    /// <summary>يرحّل سند القبض ويُنزل تخصيصاته على الفواتير.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="receiptId">السند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> PostReceiptAsync(
        TenantId tenant,
        UserId actor,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Receipt.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        CustomerReceiptRow? receipt = await _database.Receipts
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == receiptId, cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DocumentNotFound(ReceiptDocument, receiptId));
        }

        if (receipt.State == SalesDocumentState.Posted)
        {
            return Result<SalesDocumentView>.Success(ViewOfReceipt(receipt));
        }

        CustomerRow customer = await _database.Customers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == receipt.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        decimal settled = receipt.ReceivedAmount + receipt.DiscountAmount;

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = ReceiptDocument,
            DocumentId = receipt.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode("sales.receipt.posted"),
            DocumentDate = receipt.ReceivedOn,
            Narration = new LocalizedName("سند قبض " + receipt.Number, "Customer receipt " + receipt.Number),
            Amounts =
            [
                new PostingAmount("received", Money.Of(receipt.ReceivedAmount, _currency)),
                new PostingAmount("discount", Money.Of(receipt.DiscountAmount, _currency)),
            ],
            Facts =
            [
                new PostingFact("condition.has_settlement_discount", SalesInvoiceService.Boolean(receipt.DiscountAmount > 0m)),
                new PostingFact("document.settlement_method", receipt.SettlementMethod),
                new PostingFact("subledger.customer", customer.Code),

                // ⚠️ سطر التسوية معلَن في المصفوفة بـ subledger: "resolved"، والمحرك
                // يحوّله إلى النوع "none" ثم يبحث عن الواقعة subledger.none. الاسم
                // مضلّل والسلوك موثَّق في تقرير هذا التسليم.
                new PostingFact("subledger.none", receipt.TreasuryPartyId),
            ],
            PartyId = customer.Code,
            ControlEffect = -settled,
            Currency = _currency,
            Actor = actor,
            Generation = receipt.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(posted.Errors);
        }

        receipt.State = SalesDocumentState.Posted;
        receipt.PostedEntryId = posted.Value.JournalEntryId;

        List<ReceivableAllocationRow> allocations = await _database.Allocations
            .Where(row => row.SourceType == "RECEIPT" && row.SourceId == receipt.Id)
            .OrderBy(row => row.InvoiceId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (ReceivableAllocationRow allocation in allocations)
        {
            SalesInvoiceRow invoice = await _database.Invoices
                .FirstAsync(row => row.Id == allocation.InvoiceId, cancellationToken)
                .ConfigureAwait(false);
            invoice.AllocatedAmount += allocation.AllocatedAmount;
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<SalesDocumentView>.Success(ViewOfReceipt(receipt));
    }

    /// <summary>يسجّل دفعة مقدمة من عميل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> RecordAdvanceAsync(
        TenantId tenant,
        UserId actor,
        CustomerAdvanceDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Advance.Record", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        // القدرة وحدة واحدة: القبض والاستنفاد حدثان تفتحهما القدرة نفسها. ومسوّدةُ
        // دفعةٍ لمستأجرٍ أطفأ القدرة رصيدٌ لا سبيل إلى تخليصه — ترفض من أوّلها.
        Result<AdmittedDocument> admitted = await AdmitAdvanceAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (admitted.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(admitted.Errors);
        }

        if (draft.Net.Amount < 0m || draft.Tax.Amount < 0m)
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.NegativeAmount);
        }

        if (await _database.Advances
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DuplicateNumber(draft.Number));
        }

        CustomerAdvanceRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            CustomerId = draft.CustomerId,
            ReceivedOn = draft.ReceivedOn,
            State = SalesDocumentState.Draft,
            CurrencyCode = _currency.Value,
            SettlementMethod = draft.SettlementMethod,
            TreasuryPartyId = draft.TreasuryPartyId,
            TaxDueOnAdvance = draft.TaxDueOnCollection,
            NetAmount = draft.Net.Amount,
            TaxAmount = draft.TaxDueOnCollection ? draft.Tax.Amount : 0m,
        };

        _database.Advances.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(ViewOfAdvance(row));
    }

    /// <summary>يرحّل دفعة مقدمة عبر <c>sales.advance.received</c>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">الدفعة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> PostAdvanceAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Advance.Post", cancellationToken)
            .ConfigureAwait(false);

        Result<AdmittedDocument> admittedAdvance = await AdmitAdvanceAsync(tenant, cancellationToken).ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        if (admittedAdvance.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(admittedAdvance.Errors);
        }

        Result covers = SalesAdmission.EnsureCovers(admittedAdvance.Value, SalesAdmission.AdvanceAppliedField);
        if (covers.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(covers.Errors);
        }

        CustomerAdvanceRow? advance = await _database.Advances
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == advanceId, cancellationToken)
            .ConfigureAwait(false);

        if (advance is null)
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DocumentNotFound(AdvanceDocument, advanceId));
        }

        if (advance.State == SalesDocumentState.Posted)
        {
            return Result<SalesDocumentView>.Success(ViewOfAdvance(advance));
        }

        CustomerRow customer = await _database.Customers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == advance.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = AdvanceDocument,
            DocumentId = advance.Id,
            Trigger = PostingTrigger.OnSettlement,
            Event = new PostingEventCode("sales.advance.received"),
            DocumentDate = advance.ReceivedOn,
            Narration = new LocalizedName("دفعة مقدمة " + advance.Number, "Customer advance " + advance.Number),
            Amounts =
            [
                new PostingAmount("net", Money.Of(advance.NetAmount, _currency)),
                new PostingAmount("tax", Money.Of(advance.TaxAmount, _currency)),
            ],
            Facts =
            [
                new PostingFact("condition.vat_due_on_advance", SalesInvoiceService.Boolean(advance.TaxDueOnAdvance)),
                new PostingFact("tax_policy.vat_due_on_advance", SalesInvoiceService.Boolean(advance.TaxDueOnAdvance)),
                new PostingFact("document.settlement_method", advance.SettlementMethod),
                new PostingFact("subledger.customer", customer.Code),
                new PostingFact("subledger.none", advance.TreasuryPartyId),
            ],
            PartyId = customer.Code,

            // الدفعة المقدمة التزام على العميل: أثرها على نقطة ضبطه دائن بالصافي.
            ControlEffect = -advance.NetAmount,
            Currency = _currency,
            Actor = actor,
            Generation = advance.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(posted.Errors);
        }

        advance.State = SalesDocumentState.Posted;
        advance.PostedEntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(ViewOfAdvance(advance));
    }

    /// <summary>يستنفد جزءاً من دفعة مقدمة مقابل فاتورة، ويرحّل الاستنفاد.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="advanceId">الدفعة.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="amount">المبلغ المستنفَد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<PostingReceipt>> ApplyAdvanceAsync(
        TenantId tenant,
        UserId actor,
        Guid advanceId,
        Guid invoiceId,
        Money amount,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Advance.Apply", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PostingReceipt>.Failure(gate.Errors);
        }

        // القبول قبل أي قراءة أو كتابة: استنفاد دفعة مقدمة يجعل الفاتورة تحمل الحقل
        // ‏advanceApplied، وهو حقل قدرة «دفعة مقدمة من العميل». ومستأجرٌ أطفأها لا
        // يمارسها بإرسال الحقل — وإلا فهي زينة لا قدرة.
        Result<AdmittedDocument> admitted = await AdmitAdvanceAsync(tenant, cancellationToken).ConfigureAwait(false);

        if (admitted.IsFailure)
        {
            return Result<PostingReceipt>.Failure(admitted.Errors);
        }

        return await ApplyAdmittedAdvanceAsync(
            tenant, actor, admitted.Value, advanceId, invoiceId, amount, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// <b>الكاتب الوحيد لاستنفاد الدفعة المقدمة — ويطلب <see cref="AdmittedDocument"/> في توقيعه.</b>
    /// <para>
    /// وهذا هو موضع الإنفاذ: النوع لا يُبنى إلا بالمرور من قبول ملفّ المستأجر، فمن أراد
    /// أن يستنفد دفعة مقدمة وجب عليه أن يحمل قبولاً — لا أن يتذكّر أن يستدعي فحصاً.
    /// </para>
    /// </summary>
    private async ValueTask<Result<PostingReceipt>> ApplyAdmittedAdvanceAsync(
        TenantId tenant,
        UserId actor,
        AdmittedDocument admitted,
        Guid advanceId,
        Guid invoiceId,
        Money amount,
        CancellationToken cancellationToken)
    {
        // وتذكرة مستند آخر ليست تذكرة هذا المستند.
        Result covers = SalesAdmission.EnsureCovers(admitted, SalesAdmission.AdvanceAppliedField);
        if (covers.IsFailure)
        {
            return Result<PostingReceipt>.Failure(covers.Errors);
        }

        CustomerAdvanceRow? advance = await _database.Advances
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == advanceId, cancellationToken)
            .ConfigureAwait(false);

        if (advance is null)
        {
            return Result<PostingReceipt>.Failure(SalesErrors.DocumentNotFound(AdvanceDocument, advanceId));
        }

        if (advance.State != SalesDocumentState.Posted)
        {
            return Result<PostingReceipt>.Failure(
                SalesErrors.NotInState(advance.Number, advance.State, SalesDocumentState.Posted));
        }

        SalesInvoiceRow? invoice = await _database.Invoices
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return Result<PostingReceipt>.Failure(
                SalesErrors.DocumentNotFound(SalesInvoiceService.InvoiceDocument, invoiceId));
        }

        decimal available = advance.NetAmount - advance.AppliedAmount;
        if (amount.Amount > available)
        {
            return Result<PostingReceipt>.Failure(SalesErrors.OverAllocation(advance.Number, amount.Amount, available));
        }

        decimal outstanding = invoice.GrossTotal - invoice.AllocatedAmount - invoice.AdvanceApplied;
        if (amount.Amount > outstanding)
        {
            return Result<PostingReceipt>.Failure(SalesErrors.OverAllocation(invoice.Number, amount.Amount, outstanding));
        }

        CustomerRow customer = await _database.Customers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == advance.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        Guid applicationId = Guid.CreateVersion7();

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = AdvanceApplicationDocument,
            DocumentId = applicationId,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode("sales.advance.applied"),
            DocumentDate = invoice.IssuedOn,
            Narration = new LocalizedName(
                "استنفاد دفعة مقدمة " + advance.Number, "Advance application " + advance.Number),
            Amounts = [new PostingAmount("applied", amount)],
            Facts = [new PostingFact("subledger.customer", customer.Code)],
            PartyId = customer.Code,

            // الاستنفاد ينقل بين حسابين ضابطين لنفس الطرف: صافي أثره على الدفتر
            // المساعد صفر، وهذا بالضبط ما يجب أن تراه المطابقة.
            ControlEffect = 0m,
            Currency = _currency,
            Actor = actor,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return posted;
        }

        int lineNo = 1 + await _database.Allocations
            .CountAsync(row => row.SourceType == "ADVANCE" && row.SourceId == advance.Id, cancellationToken)
            .ConfigureAwait(false);

        advance.AppliedAmount += amount.Amount;
        invoice.AdvanceApplied += amount.Amount;

        _database.Allocations.Add(new ReceivableAllocationRow
        {
            Id = applicationId,
            TenantId = tenant.Value,
            SourceType = "ADVANCE",
            SourceId = advance.Id,
            InvoiceId = invoice.Id,
            LineNo = lineNo,
            AllocatedAmount = amount.Amount,
            AllocatedOn = invoice.IssuedOn,
        });

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return posted;
    }

    /// <summary>
    /// يعرض «فاتورة تحمل استنفاد دفعة مقدمة» على ملفّ المستأجر. مسار واحد لكل أفعال
    /// القدرة الثلاثة — القبض والترحيل والاستنفاد — لأن القدرة واحدة والأحداث اثنان.
    /// </summary>
    private ValueTask<Result<AdmittedDocument>> AdmitAdvanceAsync(TenantId tenant, CancellationToken cancellationToken)
        => _admission.AdmitInvoiceAsync(
            tenant,
            [SalesAdmission.CustomerField, SalesAdmission.LinesField, SalesAdmission.AdvanceAppliedField],
            cancellationToken);

    private SalesDocumentView ViewOfReceipt(CustomerReceiptRow receipt) => new(
        receipt.Id,
        receipt.Number,
        receipt.State,
        new DocumentTotals(
            Money.Of(receipt.ReceivedAmount, _currency),
            Money.Of(receipt.DiscountAmount, _currency),
            Money.Of(receipt.ReceivedAmount + receipt.DiscountAmount, _currency)),
        receipt.PostedEntryId);

    private SalesDocumentView ViewOfAdvance(CustomerAdvanceRow advance) => new(
        advance.Id,
        advance.Number,
        advance.State,
        new DocumentTotals(
            Money.Of(advance.NetAmount, _currency),
            Money.Of(advance.TaxAmount, _currency),
            Money.Of(advance.NetAmount + advance.TaxAmount, _currency)),
        advance.PostedEntryId);
}
