using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Sales.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Application;

/// <summary>مسوّدة قيد تكلفة المبيعات المصاحب.</summary>
/// <param name="ItemId">الصنف في دفتره المساعد — معرّف مبهم لا رقم حساب.</param>
/// <param name="WarehouseId">المستودع — بُعد تحليلي إلزامي على مراقبة المخزون.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="Cost">تكلفة الأصناف المباعة بطريقة التكلفة المعتمدة لحظة البيع.</param>
public sealed record CostOfSalesDraft(string ItemId, string WarehouseId, string ItemGroup, Money Cost);

/// <summary>
/// دورة مستند البيع: عرض سعر ← أمر بيع ← فاتورة ← ترحيل.
/// <para>
/// <b>ولا رقم حساب في هذا الملف كله.</b> الوحدة تسمّي الحدث
/// (<c>sales.invoice.posted</c>) وتُسلّم مبالغه ووقائعه وأبعاده، والمصفوفة داخل الدفتر
/// هي التي تقرّر أي دور يُدين وأيّه يُدان.
/// </para>
/// <para>
/// و<b>المستند المُرحَّل لا يُعدَّل ولا يُحذف</b>: التصحيح بإشعار دائن للأثر التجاري،
/// أو بقيد عكسي للخطأ المحاسبي، ثم إعادة ترحيل بجيل تالٍ (ADR-0002 · ADR-0003).
/// </para>
/// </summary>
public sealed class SalesInvoiceService : IApplicationService
{
    /// <summary>نوع مستند الفاتورة في هوية الإحكام لدى المحرك.</summary>
    internal const string InvoiceDocument = "SalesInvoice";

    /// <summary>رمز حدث الاعتراف بالإيراد — أحد شقّي هوية إحكام الفاتورة.</summary>
    internal const string InvoicePostedEvent = "sales.invoice.posted";

    /// <summary>
    /// رمز حدث قيد التكلفة المصاحب.
    /// <para>
    /// <b>ولاحظ ما لم يعد هنا:</b> كان لهذا القيد «نوع مستند» مُختلَق
    /// (<c>SalesInvoiceCostOfSales</c>) اختُرع للهروب من تصادم هوية رباعية. وكانت
    /// كلفته أن «كل قيود هذه الفاتورة» سؤال يستحيل طرحه: قيدا الفاتورة الواحدة
    /// تحت نوعَي مستند مختلفين. وبعد أن صار رمز الحدث في الهوية، عاد القيدان إلى
    /// نوعهما الصادق <c>SalesInvoice</c> ويفترقان برمز الحدث (ADR-0017).
    /// </para>
    /// </summary>
    internal const string CostOfSalesEvent = "sales.invoice.cost_of_sales";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly SalesDbContext _database;
    private readonly IPostingService _posting;
    private readonly SubledgerPostingGateway _gateway;
    private readonly SalesAdmission _admission;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل — الطريق الوحيد إلى دفتر الأستاذ.</param>
    /// <param name="profiles">مخزن ملفّات القدرات — بوابة القبول (ADR-0023).</param>
    public SalesInvoiceService(
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
        _posting = posting;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
        _gateway = new SubledgerPostingGateway(_database, posting, runtime.CostCenters);
        _admission = new SalesAdmission(profiles);
    }

    /// <summary>يُنشئ عرض سعر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="validUntil">آخر يوم صلاحية.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> CreateQuotationAsync(
        TenantId tenant,
        UserId actor,
        SalesDocumentDraft draft,
        DateOnly validUntil,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Quotation.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        Result<CustomerRow> customer = await CustomerAsync(tenant, draft.CustomerId, cancellationToken).ConfigureAwait(false);
        if (customer.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(customer.Errors);
        }

        Result<Totals> totals = Validate(draft);
        if (totals.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(totals.Errors);
        }

        if (await _database.Quotations
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DuplicateNumber(draft.Number));
        }

        QuotationRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            CustomerId = draft.CustomerId,
            IssuedOn = draft.IssuedOn,
            ValidUntil = validUntil,
            State = SalesDocumentState.Draft,
            CurrencyCode = _currency.Value,
            NetTotal = totals.Value.Net,
            TaxTotal = totals.Value.Tax,
            GrossTotal = totals.Value.Gross,
        };

        _database.Quotations.Add(row);
        AddLines(tenant, "QUOTATION", row.Id, draft.Lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(View(row.Id, row.Number, row.State, totals.Value, null));
    }

    /// <summary>يُنشئ أمر بيع، اختيارياً من عرض سعر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="quotationId">عرض السعر المصدر إن وُجد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> CreateOrderAsync(
        TenantId tenant,
        UserId actor,
        SalesDocumentDraft draft,
        Guid? quotationId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Order.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        Result<CustomerRow> customer = await CustomerAsync(tenant, draft.CustomerId, cancellationToken).ConfigureAwait(false);
        if (customer.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(customer.Errors);
        }

        Result<Totals> totals = Validate(draft);
        if (totals.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(totals.Errors);
        }

        if (await _database.Orders
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DuplicateNumber(draft.Number));
        }

        SalesOrderRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            CustomerId = draft.CustomerId,
            QuotationId = quotationId,
            OrderedOn = draft.IssuedOn,
            State = SalesDocumentState.Approved,
            CurrencyCode = _currency.Value,
            BranchId = draft.BranchId,
            NetTotal = totals.Value.Net,
            TaxTotal = totals.Value.Tax,
            GrossTotal = totals.Value.Gross,
        };

        _database.Orders.Add(row);
        AddLines(tenant, "ORDER", row.Id, draft.Lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(View(row.Id, row.Number, row.State, totals.Value, null));
    }

    /// <summary>يُصدر فاتورة مبيعات مسوّدة. الترحيل خطوة مستقلة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="orderId">أمر البيع المصدر إن وُجد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> CreateInvoiceAsync(
        TenantId tenant,
        UserId actor,
        SalesDocumentDraft draft,
        Guid? orderId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Invoice.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        Result<CustomerRow> customer = await CustomerAsync(tenant, draft.CustomerId, cancellationToken).ConfigureAwait(false);
        if (customer.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(customer.Errors);
        }

        Result<Totals> totals = Validate(draft);
        if (totals.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(totals.Errors);
        }

        if (await _database.Invoices
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DuplicateNumber(draft.Number));
        }

        SalesInvoiceRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            CustomerId = draft.CustomerId,
            OrderId = orderId,
            IssuedOn = draft.IssuedOn,
            DueOn = draft.IssuedOn.AddDays(customer.Value.PaymentTermsDays),
            State = SalesDocumentState.Draft,
            CurrencyCode = _currency.Value,
            BranchId = draft.BranchId,
            ItemGroup = draft.Lines[0].ItemGroup,
            HasTaxableLine = totals.Value.HasTaxableLine,
            NetTotal = totals.Value.Net,
            TaxTotal = totals.Value.Tax,
            GrossTotal = totals.Value.Gross,
        };

        _database.Invoices.Add(row);
        AddLines(tenant, "INVOICE", row.Id, draft.Lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(View(row.Id, row.Number, row.State, totals.Value, null));
    }

    /// <summary>
    /// يرحّل فاتورة عبر الحدث <c>sales.invoice.posted</c>.
    /// <para>
    /// الوصول الثاني بالهوية نفسها لا يُرحّل مرّة ثانية ولا يُعدّ خطأ، <b>مهما كان
    /// ترتيب الوصول</b>. والرفض يترك الفاتورة مسوّدةً ومعها سبب مكتوب.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<SalesDocumentView>> PostInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Invoice.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        SalesInvoiceRow? invoice = await _database.Invoices
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return Result<SalesDocumentView>.Failure(SalesErrors.DocumentNotFound(InvoiceDocument, invoiceId));
        }

        if (invoice.State == SalesDocumentState.Posted)
        {
            return Result<SalesDocumentView>.Success(ViewOf(invoice));
        }

        if (invoice.State != SalesDocumentState.Draft)
        {
            return Result<SalesDocumentView>.Failure(
                SalesErrors.NotInState(invoice.Number, invoice.State, SalesDocumentState.Draft));
        }

        CustomerRow customer = (await CustomerAsync(tenant, invoice.CustomerId, cancellationToken).ConfigureAwait(false)).Value;

        Result creditGate = await EnsureCreditLimitAsync(tenant, customer, invoice.GrossTotal, cancellationToken)
            .ConfigureAwait(false);
        if (creditGate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(creditGate.Errors);
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = InvoiceDocument,
            DocumentId = invoice.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode(InvoicePostedEvent),
            DocumentDate = invoice.IssuedOn,
            Narration = new LocalizedName("فاتورة مبيعات " + invoice.Number, "Sales invoice " + invoice.Number),
            Amounts =
            [
                new PostingAmount("net", Money.Of(invoice.NetTotal, _currency)),
                new PostingAmount("tax", Money.Of(invoice.TaxTotal, _currency)),
            ],
            Facts =
            [
                // ‏document.has_any_line_with(...) دالة لا يقيّمها المحرك، والوحدة تُصرّح بنتيجتها.
                new PostingFact("condition.is_taxable_supply", Boolean(invoice.HasTaxableLine)),
                new PostingFact("subledger.customer", customer.Code),
                new PostingFact("line.item_group", invoice.ItemGroup),
            ],
            Dimensions = [new PostingDimension("branch", invoice.BranchId)],
            PartyId = customer.Code,
            ControlEffect = invoice.GrossTotal,
            Currency = _currency,
            Actor = actor,
            Generation = invoice.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(posted.Errors);
        }

        invoice.State = SalesDocumentState.Posted;
        invoice.PostedEntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SalesDocumentView>.Success(ViewOf(invoice));
    }

    /// <summary>يرحّل قيد تكلفة المبيعات المصاحب عبر <c>sales.invoice.cost_of_sales</c>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="draft">مسوّدة التكلفة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<PostingReceipt>> PostCostOfSalesAsync(
        TenantId tenant,
        UserId actor,
        Guid invoiceId,
        CostOfSalesDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Invoice.PostCostOfSales", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PostingReceipt>.Failure(gate.Errors);
        }

        // قيد تكلفة المبيعات يجعل الفاتورة تحمل بُعد المستودع، وهو حقل قدرة «تكلفة
        // المبيعات بالجرد المستمر». ومستأجرٌ على الجرد الدوري لا قيد تكلفة عنده لحظة
        // البيع أصلاً — فالحقل مُطفأ والمسار مرفوض.
        Result<AdmittedDocument> admitted = await _admission
            .AdmitInvoiceAsync(
                tenant,
                [SalesAdmission.CustomerField, SalesAdmission.LinesField, SalesAdmission.WarehouseField],
                cancellationToken)
            .ConfigureAwait(false);

        if (admitted.IsFailure)
        {
            return Result<PostingReceipt>.Failure(admitted.Errors);
        }

        return await PostAdmittedCostOfSalesAsync(tenant, actor, admitted.Value, invoiceId, draft, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>الكاتب الوحيد لقيد تكلفة المبيعات — ويطلب <see cref="AdmittedDocument"/> في توقيعه.</b>
    /// <para>
    /// النوع لا يُبنى إلا بالمرور من قبول ملفّ المستأجر، فمن أراد ترحيل قيد تكلفة وجب
    /// عليه أن يحمل قبولاً — لا أن يتذكّر أن يستدعي فحصاً.
    /// </para>
    /// </summary>
    private async ValueTask<Result<PostingReceipt>> PostAdmittedCostOfSalesAsync(
        TenantId tenant,
        UserId actor,
        AdmittedDocument admitted,
        Guid invoiceId,
        CostOfSalesDraft draft,
        CancellationToken cancellationToken)
    {
        Result covers = SalesAdmission.EnsureCovers(admitted, SalesAdmission.WarehouseField);
        if (covers.IsFailure)
        {
            return Result<PostingReceipt>.Failure(covers.Errors);
        }

        SalesInvoiceRow? invoice = await _database.Invoices
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return Result<PostingReceipt>.Failure(SalesErrors.DocumentNotFound(InvoiceDocument, invoiceId));
        }

        if (invoice.State != SalesDocumentState.Posted)
        {
            return Result<PostingReceipt>.Failure(
                SalesErrors.NotInState(invoice.Number, invoice.State, SalesDocumentState.Posted));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = InvoiceDocument,
            DocumentId = invoice.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode(CostOfSalesEvent),
            DocumentDate = invoice.IssuedOn,
            Narration = new LocalizedName("تكلفة مبيعات " + invoice.Number, "Cost of sales " + invoice.Number),
            Amounts = [new PostingAmount("cost", draft.Cost)],
            Facts =
            [
                new PostingFact("subledger.item", draft.ItemId),
                new PostingFact("line.item_group", draft.ItemGroup),
            ],
            Dimensions =
            [
                new PostingDimension("branch", invoice.BranchId),
                new PostingDimension("warehouse", draft.WarehouseId),
            ],

            // قيد التكلفة لا يمسّ نقطة ضبط العملاء إطلاقاً — أثره صفر عليها.
            PartyId = draft.ItemId,
            ControlEffect = 0m,
            Currency = _currency,
            Actor = actor,
        };

        return await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يعكس قيد فاتورة مُرحَّلة. <b>القيد الأصلي والفاتورة الأصلية لا يُمسّان</b>:
    /// العكس قيد جديد مرتبط، ويرفع جيل الفاتورة كي تُعاد مصحَّحة بمفتاح إحكام مختلف.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="reason">سبب العكس ثنائي اللغة — إلزامي.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<PostingReceipt>> ReverseInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid invoiceId,
        LocalizedName reason,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Invoice.Reverse", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PostingReceipt>.Failure(gate.Errors);
        }

        SalesInvoiceRow? invoice = await _database.Invoices
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false);

        if (invoice is null)
        {
            return Result<PostingReceipt>.Failure(SalesErrors.DocumentNotFound(InvoiceDocument, invoiceId));
        }

        if (invoice.State != SalesDocumentState.Posted || invoice.PostedEntryId is not { } entryId)
        {
            return Result<PostingReceipt>.Failure(
                SalesErrors.NotInState(invoice.Number, invoice.State, SalesDocumentState.Posted));
        }

        Result<PostingReceipt> reversal = await _posting.ReverseAsync(
            new ReversalRequest
            {
                Tenant = tenant,
                EntryId = entryId,
                Reason = reason,
                Actor = actor,
            },
            cancellationToken).ConfigureAwait(false);

        if (reversal.IsFailure)
        {
            return Result<PostingReceipt>.Failure(SalesErrors.PostingRefused(reversal.Errors));
        }

        invoice.State = SalesDocumentState.Reversed;
        invoice.PostingGeneration++;

        DocumentPostingRow? attempt = await FindAttemptAsync(
            tenant,
            InvoiceDocument,
            invoice.Id,
            PostingTrigger.OnApproval,
            invoice.PostingGeneration - 1,
            InvoicePostedEvent,
            cancellationToken).ConfigureAwait(false);

        if (attempt is not null)
        {
            // العكس يُلغي أثر المستند على نقطة الضبط، ولا يمحو سجلّ ما حدث.
            attempt.ControlEffect = 0m;
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return reversal;
    }

    /// <summary>يقرأ فاتورة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Read)]
    public async ValueTask<Result<SalesDocumentView>> GetInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Read, "Sales.Invoice.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SalesDocumentView>.Failure(gate.Errors);
        }

        SalesInvoiceRow? invoice = await _database.Invoices
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return invoice is null
            ? Result<SalesDocumentView>.Failure(SalesErrors.DocumentNotFound(InvoiceDocument, invoiceId))
            : Result<SalesDocumentView>.Success(ViewOf(invoice));
    }

    internal static string Boolean(bool value) => value ? "true" : "false";

    internal readonly record struct Totals(decimal Net, decimal Tax, decimal Gross, bool HasTaxableLine);

    /// <summary>
    /// يقرأ صفّ محاولة بعينه <b>بهويته الخماسية كاملةً</b>.
    /// <para>
    /// رمز الحدث في الشرط ليس تجميلاً: المستند الواحد صار له صفّا محاولة عند
    /// الإطلاق نفسه (الإيراد والتكلفة)، وقراءةٌ بأربعة حقول تُعيد <b>أيّهما اتّفق</b>.
    /// ولو أُسقط رمز الحدث هنا لصار عكس الفاتورة يُصفّر أثر الصفّ الخطأ على نقطة
    /// الضبط، فيبقى أثر الإيراد قائماً بعد عكسه — انحراف صامت في المطابقة.
    /// </para>
    /// </summary>
    internal async Task<DocumentPostingRow?> FindAttemptAsync(
        TenantId tenant,
        string documentType,
        Guid documentId,
        PostingTrigger trigger,
        int generation,
        string eventCode,
        CancellationToken cancellationToken)
    {
        string id = documentId.ToString("D", CultureInfo.InvariantCulture);
        string code = trigger.ToString();
        return await _database.Postings
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value
                       && row.DocumentType == documentType
                       && row.DocumentId == id
                       && row.TriggerCode == code
                       && row.Generation == generation
                       && row.EventCode == eventCode,
                cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<Result> EnsureCreditLimitAsync(
        TenantId tenant,
        CustomerRow customer,
        decimal addition,
        CancellationToken cancellationToken)
    {
        if (customer.CreditLimit <= 0m)
        {
            return Result.Success();
        }

        decimal outstanding = await _database.Invoices
            .Where(row => row.TenantId == tenant.Value
                          && row.CustomerId == customer.Id
                          && row.State == SalesDocumentState.Posted)
            .SumAsync(row => row.GrossTotal - row.AllocatedAmount - row.AdvanceApplied, cancellationToken)
            .ConfigureAwait(false);

        decimal exposure = outstanding + addition;
        return exposure > customer.CreditLimit
            ? Result.Failure(SalesErrors.CreditLimitExceeded(customer.Code, exposure, customer.CreditLimit))
            : Result.Success();
    }

    private async Task<Result<CustomerRow>> CustomerAsync(TenantId tenant, Guid customerId, CancellationToken cancellationToken)
    {
        CustomerRow? row = await _database.Customers
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == customerId, cancellationToken)
            .ConfigureAwait(false);

        return row is null ? Result<CustomerRow>.Failure(SalesErrors.CustomerNotFound(customerId)) : Result<CustomerRow>.Success(row);
    }

    internal static Result<Totals> Validate(SalesDocumentDraft draft)
    {
        if (draft.Lines.Count == 0)
        {
            return Result<Totals>.Failure(SalesErrors.NoLines);
        }

        decimal net = 0m;
        decimal tax = 0m;
        bool taxable = false;

        foreach (SalesLineDraft line in draft.Lines)
        {
            if (line.Quantity < 0m || line.UnitPrice.Amount < 0m || line.Discount.Amount < 0m)
            {
                return Result<Totals>.Failure(SalesErrors.NegativeAmount);
            }

            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, line.Discount.Amount, line.TaxRate, line.TaxClassification);

            // المجموع = مجموع سطور **مقرَّبة**، ولا يُعاد تقريبه بعد الجمع.
            net += lineNet;
            tax += lineTax;
            taxable |= string.Equals(line.TaxClassification, "standard", StringComparison.Ordinal);
        }

        return Result<Totals>.Success(new Totals(net, tax, net + tax, taxable));
    }

    internal void AddLines(TenantId tenant, string ownerType, Guid ownerId, IReadOnlyList<SalesLineDraft> lines)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            SalesLineDraft line = lines[index];
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, line.Discount.Amount, line.TaxRate, line.TaxClassification);

            _database.Lines.Add(new SalesLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                OwnerType = ownerType,
                OwnerId = ownerId,
                LineNo = index + 1,
                DescriptionAr = line.Description.Arabic,
                DescriptionEn = line.Description.English,
                ItemGroup = line.ItemGroup,
                Quantity = line.Quantity,
                UnitPrice = line.UnitPrice.Amount,
                DiscountAmount = line.Discount.Amount,
                TaxClassification = line.TaxClassification,
                TaxRate = line.TaxRate,
                LineNet = lineNet,
                LineTax = lineTax,
            });
        }
    }

    private SalesDocumentView ViewOf(SalesInvoiceRow invoice) => new(
        invoice.Id,
        invoice.Number,
        invoice.State,
        new DocumentTotals(
            Money.Of(invoice.NetTotal, _currency),
            Money.Of(invoice.TaxTotal, _currency),
            Money.Of(invoice.GrossTotal, _currency)),
        invoice.PostedEntryId);

    private SalesDocumentView View(Guid id, string number, string state, Totals totals, Guid? entryId) => new(
        id,
        number,
        state,
        new DocumentTotals(
            Money.Of(totals.Net, _currency),
            Money.Of(totals.Tax, _currency),
            Money.Of(totals.Gross, _currency)),
        entryId);
}
