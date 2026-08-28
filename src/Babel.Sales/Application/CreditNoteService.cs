using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Sales.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Application;

/// <summary>
/// الإشعارات الدائنة — الأثر التجاري العكسي لفاتورة مُرحَّلة.
/// <para>
/// <b>الإشعار الدائن ليس تعديلاً للفاتورة ولا حذفاً لها.</b> الفاتورة الأصلية تبقى كما
/// هي بقيدها وبرقمها وبموضعها في سلسلة التجزئة، والإشعار مستند مستقلّ بقيده الخاص
/// يُخصَّص عليها. وهذا ما تفرضه المصفوفة نفسها: مردودات المبيعات حساب مقابل، ولا
/// تُخصم من الإيراد مباشرة.
/// </para>
/// <para>
/// <b>وردّ البضاعة أثرٌ ثانٍ لا يقع في الأثر التجاري:</b> الإشعار الدائن يُنقص الذمة
/// والإيراد، و<c>sales.credit_note.cost_of_sales</c> يُعيد البضاعة إلى المخزون
/// <b>بتكلفة صرفها الأصلي</b>. وكان هذا الحدث موجوداً في المصفوفة و<b>لا يُطلقه
/// شيء</b>: وحدة المخزون تعرف كيف تُقيّم المرتجع بهوية صرفه، ولم يكن في المبيعات
/// حقلٌ يحمل تلك الهوية. فصار على سطر الإشعار
/// <see cref="SalesLineDraft.OriginalInvoiceLineId"/>.
/// </para>
/// <para>
/// و<b>سطرٌ بلا سطر أصلي تخفيضُ قيمة لا ردُّ بضاعة</b>: لا حركة مخزون له ولا قيد
/// تكلفة. والفرق قرارٌ تجاري يُصرَّح به على السطر، ولا يُخمَّن من المبلغ.
/// </para>
/// </summary>
public sealed class CreditNoteService : IApplicationService
{
    /// <summary>نوع مستند الإشعار الدائن في هوية الإحكام.</summary>
    internal const string CreditNoteDocument = "SalesCreditNote";

    /// <summary>
    /// نوع مستند <b>سطر</b> الإشعار الدائن — حامل قيد تكلفة المرتجع.
    /// <para>
    /// بحبيبيّة السطر للسبب الذي جعل قيد تكلفة الفاتورة كذلك: إشعارٌ يردّ صنفين
    /// واقعتا صرفٍ معكوستان لا واحدة. ومعرّفه معرّف صفّ قائم في
    /// <c>sales.sales_line</c> مملوك للإشعار — لا نوعاً مُختلَقاً
    /// (<c>docs/evidence/traps.md#fakh-49</c>).
    /// </para>
    /// </summary>
    internal const string CreditNoteLineDocument = "SalesCreditNoteLine";

    /// <summary>رمز حدث الأثر التجاري للإشعار.</summary>
    internal const string CreditNotePostedEvent = "sales.credit_note.posted";

    /// <summary>رمز حدث قيد تكلفة المرتجع.</summary>
    internal const string CostOfReturnEvent = "sales.credit_note.cost_of_sales";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly SalesDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly IInventoryValuation _valuation;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل.</param>
    /// <param name="valuation">
    /// حدّ تقييم المخزون — <b>الجهة الوحيدة التي تقول بكم يرجع المرتجع</b>، وتقوله
    /// بتكلفة الصرف الأصلي لا بمتوسط اليوم. ومنفذٌ في <c>Babel.Contracts</c> لا مرجعٌ
    /// إلى وحدة المخزون (القاعدة 3)، وإلزاميٌّ لا اختياري.
    /// </param>
    public CreditNoteService(
        IEntitlementEnforcer enforcer,
        SalesRuntime runtime,
        IPostingService posting,
        IInventoryValuation valuation)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(valuation);
        _enforcer = enforcer;
        _database = runtime.Database;
        _valuation = valuation;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
        _gateway = new SubledgerPostingGateway(_database, posting, runtime.CostCenters);
    }

    /// <summary>يُصدر إشعاراً دائناً مسوّدة مرتبطاً بفاتورة أصلية.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        CreditNoteDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.CreditNote.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        SalesInvoiceRow? invoice = await _database.Invoices
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.InvoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return Result<SalesDocumentView>.Failure(
                SalesErrors.DocumentNotFound(SalesInvoiceService.InvoiceDocument, draft.InvoiceId));
        }

        if (invoice.State != SalesDocumentState.Posted)
        {
            return Result<SalesDocumentView>.Failure(
                SalesErrors.NotInState(invoice.Number, invoice.State, SalesDocumentState.Posted));
        }

        Result<SalesInvoiceService.Totals> totals = SalesInvoiceService.Validate(
            new SalesDocumentDraft(draft.Number, invoice.CustomerId, draft.IssuedOn, invoice.BranchId, draft.Lines));

        if (totals.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(totals.Errors);
        }

        decimal outstanding = invoice.GrossTotal - invoice.AllocatedAmount - invoice.AdvanceApplied;
        if (totals.Value.Gross > outstanding)
        {
            return Result<SalesDocumentView>.Failure(
                SalesErrors.OverAllocation(invoice.Number, totals.Value.Gross, outstanding));
        }

        if (await _database.CreditNotes
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DuplicateNumber(draft.Number));
        }

        // ── سطور الردّ تُتحقَّق **قبل** الكتابة ────────────────────────────────
        // سطرٌ يشير إلى سطر فاتورة لا وجود له تحت هذه الفاتورة يُرفض هنا لا عند
        // الترحيل: معرّف السطر الأصلي هوية صرفٍ، وهويةٌ لا تقابل شيئاً تُنتج مستنداً
        // يتعذّر ترحيله بعد أن صار واقعاً في المخزن.
        foreach (SalesLineDraft line in draft.Lines)
        {
            if (line.OriginalInvoiceLineId is not { } originalLineId)
            {
                continue;
            }

            bool exists = await _database.Lines
                .AnyAsync(
                    candidate => candidate.TenantId == tenant.Value
                                 && candidate.OwnerType == LineOwner.Invoice
                                 && candidate.OwnerId == invoice.Id
                                 && candidate.Id == originalLineId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (!exists)
            {
                return Result<SalesDocumentView>.Failure(
                    SalesErrors.LineNotFound(SalesInvoiceService.InvoiceDocument, invoice.Id, originalLineId));
            }
        }

        CreditNoteRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            CustomerId = invoice.CustomerId,
            InvoiceId = invoice.Id,
            IssuedOn = draft.IssuedOn,
            State = SalesDocumentState.Draft,
            CurrencyCode = _currency.Value,
            BranchId = invoice.BranchId,
            OriginalWasTaxable = invoice.HasTaxableLine,
            NetTotal = totals.Value.Net,
            TaxTotal = totals.Value.Tax,
            GrossTotal = totals.Value.Gross,
        };

        _database.CreditNotes.Add(row);

        // ‏**وسطور الإشعار تُكتب.** كانت تُحسب مجاميعها ثم تُرمى، فلم يكن للسطر معرّف
        // ولا موضع. وقيد تكلفة المرتجع يُرحَّل بمعرّف سطره، فالسطر صار كياناً.
        SalesInvoiceService.AddLines(_database, tenant, LineOwner.CreditNote, row.Id, draft.Lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(new SalesDocumentView(
            row.Id,
            row.Number,
            row.State,
            new DocumentTotals(
                Money.Of(row.NetTotal, _currency),
                Money.Of(row.TaxTotal, _currency),
                Money.Of(row.GrossTotal, _currency)),
            null));
    }

    /// <summary>يرحّل الإشعار الدائن ويخصّصه على فاتورته الأصلية.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="creditNoteId">الإشعار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid creditNoteId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.CreditNote.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        CreditNoteRow? note = await _database.CreditNotes
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == creditNoteId, cancellationToken)
            .ConfigureAwait(false);

        if (note is null)
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DocumentNotFound(CreditNoteDocument, creditNoteId));
        }

        if (note.State == SalesDocumentState.Posted)
        {
            return Result<SalesDocumentView>.Success(ViewOf(note));
        }

        if (note.State != SalesDocumentState.Draft)
        {
            return Result<SalesDocumentView>.Failure(
                SalesErrors.NotInState(note.Number, note.State, SalesDocumentState.Draft));
        }

        CustomerRow customer = await _database.Customers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == note.CustomerId, cancellationToken)
            .ConfigureAwait(false);

        SalesInvoiceRow invoice = await _database.Invoices
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == note.InvoiceId, cancellationToken)
            .ConfigureAwait(false);

        decimal outstanding = invoice.GrossTotal - invoice.AllocatedAmount - invoice.AdvanceApplied;
        if (note.GrossTotal > outstanding)
        {
            return Result<SalesDocumentView>.Failure(
                SalesErrors.OverAllocation(invoice.Number, note.GrossTotal, outstanding));
        }

        // ── ردّ البضاعة أولاً، ثم الأثر التجاري ───────────────────────────────
        // والترتيب مقصود: لو رُفض الردّ (‏ردٌّ يتجاوز ما صُرف، أو سطر أصلي بلا قيد
        // تكلفة) لم يُكتب في الدفتر شيء. ولو نجح الردّ ثم سقط ما بعده، فكلّ خطوة
        // مُحكَمة بهويتها — إعادة المحاولة تُكمل ولا تُضاعف.
        Result returned = await PostCostOfReturnedGoodsAsync(tenant, actor, note, cancellationToken)
            .ConfigureAwait(false);

        if (returned.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(returned.Errors);
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = CreditNoteDocument,
            DocumentId = note.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode(CreditNotePostedEvent),
            DocumentDate = note.IssuedOn,
            Narration = new LocalizedName("إشعار دائن " + note.Number, "Credit note " + note.Number),
            Amounts =
            [
                new PostingAmount("net", Money.Of(note.NetTotal, _currency)),
                new PostingAmount("tax", Money.Of(note.TaxTotal, _currency)),
            ],
            Facts =
            [
                new PostingFact("condition.original_was_taxable", SalesInvoiceService.Boolean(note.OriginalWasTaxable)),
                new PostingFact("source_invoice.line.tax_classification", note.OriginalWasTaxable ? "standard" : "exempt"),
                new PostingFact("subledger.customer", customer.Code),
            ],
            Dimensions = [new PostingDimension("branch", note.BranchId)],
            PartyId = customer.Code,
            ControlEffect = -note.GrossTotal,
            Currency = _currency,
            Actor = actor,
            Generation = note.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(posted.Errors);
        }

        note.State = SalesDocumentState.Posted;
        note.PostedEntryId = posted.Value.JournalEntryId;
        note.AllocatedAmount = note.GrossTotal;
        invoice.AllocatedAmount += note.GrossTotal;

        _database.Allocations.Add(new ReceivableAllocationRow
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            SourceType = "CREDIT_NOTE",
            SourceId = note.Id,
            InvoiceId = invoice.Id,
            LineNo = 1,
            AllocatedAmount = note.GrossTotal,
            AllocatedOn = note.IssuedOn,
        });

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<SalesDocumentView>.Success(ViewOf(note));
    }


    /// <summary>
    /// يردّ بضاعة كل سطر يحمل سطراً أصلياً، ويُرحّل قيد تكلفته.
    /// <para>
    /// <b>ولا يُسلَّم مبلغ.</b> يُسلَّم الكمية وهوية الصرف الأصلي، ويُعيد المخزون
    /// التكلفةَ والصنفَ ومستودعه — فالقيد يُسمّي الصنف الذي تحرّك فعلاً في الدفتر
    /// المساعد لا صنفاً سمّته المبيعات من عندها.
    /// </para>
    /// </summary>
    private async ValueTask<Result> PostCostOfReturnedGoodsAsync(
        TenantId tenant,
        UserId actor,
        CreditNoteRow note,
        CancellationToken cancellationToken)
    {
        List<SalesLineRow> lines = await _database.Lines
            .Where(row => row.TenantId == tenant.Value
                          && row.OwnerType == LineOwner.CreditNote
                          && row.OwnerId == note.Id
                          && row.OriginalInvoiceLineId != null)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        foreach (SalesLineRow line in lines)
        {
            Guid originalLineId = line.OriginalInvoiceLineId!.Value;

            // الجيل الذي رُحّل عليه قيد تكلفة الفاتورة — يُقرأ ولا يُفترض. فاتورةٌ
            // عُكست ثم أُعيدت تحمل قيد تكلفتها على جيلها الأول، والبحث بالجيل
            // الجاري كان سيطلب صرفاً لا وجود له (فخ-45 بشكله المعكوس).
            string originalId = originalLineId.ToString("D", CultureInfo.InvariantCulture);
            string trigger = PostingTrigger.OnApproval.ToString();

            DocumentPostingRow? original = await _database.Postings
                .Where(row => row.TenantId == tenant.Value
                              && row.DocumentType == SalesInvoiceService.InvoiceLineDocument
                              && row.DocumentId == originalId
                              && row.TriggerCode == trigger
                              && row.EventCode == SalesInvoiceService.CostOfSalesEvent
                              && row.State == PostingAttemptState.Posted)
                .OrderByDescending(row => row.Generation)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            if (original is null)
            {
                return Result.Failure(SalesErrors.OriginalCostEntryNotFound(originalLineId));
            }

            Result<InventoryMovementCost> cost = await _valuation.ReturnAsync(
                new InventoryReturn
                {
                    Tenant = tenant,
                    Actor = actor,
                    Source = new InventoryMovementSource(
                        BabelModule.Sales,
                        CreditNoteLineDocument,
                        line.Id.ToString("D", CultureInfo.InvariantCulture),
                        trigger,
                        note.PostingGeneration,
                        CostOfReturnEvent),
                    OriginalIssue = new InventoryMovementSource(
                        BabelModule.Sales,
                        SalesInvoiceService.InvoiceLineDocument,
                        originalId,
                        trigger,
                        original.Generation,
                        SalesInvoiceService.CostOfSalesEvent),
                    Quantity = line.Quantity,
                    OccurredOn = note.IssuedOn,
                },
                cancellationToken).ConfigureAwait(false);

            if (cost.IsFailure)
            {
                return Result.Failure(cost.Errors);
            }

            PostingIntent intent = new()
            {
                Tenant = tenant,
                DocumentType = CreditNoteLineDocument,
                DocumentId = line.Id,
                Trigger = PostingTrigger.OnApproval,
                Event = new PostingEventCode(CostOfReturnEvent),
                DocumentDate = note.IssuedOn,
                Narration = new LocalizedName(
                    "تكلفة مرتجع " + note.Number, "Cost of returned goods " + note.Number),
                Amounts = [new PostingAmount("cost", cost.Value.Cost)],
                Facts =
                [
                    new PostingFact("subledger.item", cost.Value.Location.ItemId),
                    new PostingFact("line.item_group", cost.Value.Location.ItemGroup),
                ],
                Dimensions =
                [
                    new PostingDimension("branch", note.BranchId),
                    new PostingDimension("warehouse", cost.Value.Location.WarehouseId),
                ],

                // قيد تكلفة المرتجع لا يمسّ نقطة ضبط العملاء إطلاقاً — أثره صفر عليها.
                PartyId = cost.Value.Location.ItemId,
                ControlEffect = 0m,
                Currency = _currency,
                Actor = actor,
                Generation = note.PostingGeneration,
            };

            Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
            if (posted.IsFailure)
            {
                return Result.Failure(posted.Errors);
            }
        }

        return Result.Success();
    }

    private SalesDocumentView ViewOf(CreditNoteRow note) => new(
        note.Id,
        note.Number,
        note.State,
        new DocumentTotals(
            Money.Of(note.NetTotal, _currency),
            Money.Of(note.TaxTotal, _currency),
            Money.Of(note.GrossTotal, _currency)),
        note.PostedEntryId);
}
