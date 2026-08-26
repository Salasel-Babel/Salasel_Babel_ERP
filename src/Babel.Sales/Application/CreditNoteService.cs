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
/// </summary>
public sealed class CreditNoteService : IApplicationService
{
    /// <summary>نوع مستند الإشعار الدائن في هوية الإحكام.</summary>
    internal const string CreditNoteDocument = "SalesCreditNote";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly SalesDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل.</param>
    public CreditNoteService(IEntitlementEnforcer enforcer, SalesRuntime runtime, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
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

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = CreditNoteDocument,
            DocumentId = note.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode("sales.credit_note.posted"),
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
