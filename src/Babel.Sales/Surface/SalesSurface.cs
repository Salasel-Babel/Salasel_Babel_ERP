using Babel.Sales.Application;
using Babel.SharedKernel;

namespace Babel.Sales.Surface;

/// <summary>
/// <b>السطح المنشور لوحدة المبيعات</b> — وهو ما يجوز للجذر التركيبي أن يسمّيه، ولا شيء غيره.
/// <para>
/// <b>لماذا يوجد هذا الملفّ أصلاً:</b> القاعدة 13 (البند ب) تمنع <c>Babel.Api</c> من أن يذكر
/// أيّ نوع من فضاء اسم داخلي لوحدة — و<c>Persistence</c> و<c>Application</c> منها بالاسم،
/// <b>ولو أُضيف النوع إلى قائمة السطح المنشور</b>. فكلّ خدمات المبيعات تسكن
/// <c>Babel.Sales.Application</c>، ولا يستطيع سطح HTTP أن يناديها مباشرة. والباب الوحيد
/// المشروع هو ما تفعله وحدة الدفتر بالضبط: <b>سطحٌ منشور مسمّى خارج فضاءات الداخل</b>
/// (‏<c>Babel.Ledger.Audit</c> هناك، وهذا هنا).
/// </para>
/// <para>
/// <b>وما لا يفعله هذا الملفّ — عمداً:</b> لا يُنفِذ استحقاقاً، ولا يقرّر شيئاً محاسبياً،
/// ولا يقرأ جدولاً. كلّ دالّة هنا تُترجم نوعاً منشوراً إلى مسوّدة الوحدة وتنادي خدمة
/// التطبيق التي <b>تحمل سمة الاستحقاق وتنادي المنفِّذ أوّل شيء</b>. وفحصٌ ثانٍ هنا كان
/// سيكون آليةَ تصريحٍ موازية — تُصان إحداهما وتُنسى الأخرى — وهو ما ترفضه القاعدة 6
/// بنصّها: <b>جدول القرار موضعٌ واحد</b>.
/// </para>
/// <para>
/// <b>والمال يعبر هذا الحدّ <c>decimal</c> لا <c>Money</c>:</b> ‏<c>Money</c> يحمل عملةً،
/// وعملةُ المنشأة إعدادُ وحدةٍ لا معلومةُ نقل. فلو أخذ هذا السطح <c>Money</c> لاضطرّ سطح
/// HTTP أن <b>يختار عملة</b> — وهو قرار أعمال في طبقة نقل. القيمة تعبر رقماً عشرياً،
/// والوحدة وحدها تُلبسه عملتها.
/// </para>
/// </summary>
public sealed class SalesSurface
{
    private readonly CustomerService _customers;
    private readonly SalesInvoiceService _invoices;
    private readonly CreditNoteService _creditNotes;
    private readonly ReceivablesService _receivables;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ السطح فوق خدمات الوحدة.</summary>
    /// <param name="customers">خدمة العملاء.</param>
    /// <param name="invoices">خدمة فواتير المبيعات.</param>
    /// <param name="creditNotes">خدمة الإشعارات الدائنة.</param>
    /// <param name="receivables">خدمة الذمم المدينة.</param>
    /// <param name="options">إعدادات الوحدة — ومنها عملة المنشأة.</param>
    public SalesSurface(
        CustomerService customers,
        SalesInvoiceService invoices,
        CreditNoteService creditNotes,
        ReceivablesService receivables,
        SalesOptions options)
    {
        ArgumentNullException.ThrowIfNull(customers);
        ArgumentNullException.ThrowIfNull(invoices);
        ArgumentNullException.ThrowIfNull(creditNotes);
        ArgumentNullException.ThrowIfNull(receivables);
        ArgumentNullException.ThrowIfNull(options);

        _customers = customers;
        _invoices = invoices;
        _creditNotes = creditNotes;
        _receivables = receivables;
        _currency = CurrencyCode.FromString(options.CompanyCurrency);
    }

    /// <summary>يسجّل عميلاً جديداً. بيانات أساسية، لا مستند ولا ترحيل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesParty>> AddCustomerAsync(
        TenantId tenant,
        UserId actor,
        SalesPartyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<CustomerView> result = await _customers
            .CreateAsync(
                tenant,
                actor,
                new CustomerDraft(request.Code, request.Name, Money.Of(request.CreditLimit, _currency), request.PaymentTermsDays),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure ? Result<SalesParty>.Failure(result.Errors) : Result<SalesParty>.Success(Party(result.Value));
    }

    /// <summary>يقرأ عميلاً واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="customerId">معرّف العميل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesParty>> ReadCustomerAsync(
        TenantId tenant,
        UserId actor,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        Result<CustomerView> result = await _customers
            .GetAsync(tenant, actor, customerId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure ? Result<SalesParty>.Failure(result.Errors) : Result<SalesParty>.Success(Party(result.Value));
    }

    /// <summary>
    /// يُنشئ فاتورة مبيعات <b>مسوّدة</b>. لا قيد ولا أثر في الدفتر: الترحيل خطوة مستقلّة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesDocument>> DraftInvoiceAsync(
        TenantId tenant,
        UserId actor,
        SalesInvoiceRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<SalesDocumentView> result = await _invoices
            .CreateInvoiceAsync(
                tenant,
                actor,
                new SalesDocumentDraft(request.Number, request.CustomerId, request.IssuedOn, request.BranchId, Lines(request.Lines)),
                orderId: null,
                cancellationToken)
            .ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>يقرأ فاتورة مبيعات بحالتها ومجاميعها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesDocument>> ReadInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        Result<SalesDocumentView> result = await _invoices
            .GetInvoiceAsync(tenant, actor, invoiceId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>
    /// يرحّل فاتورة مسوّدة فتصير <b>واقعة محاسبية</b>. حصين ضدّ التكرار: الوصول الثاني
    /// بالهوية نفسها يُرجع المستند ذاته و<c>AlreadyPosted = true</c> ولا يُنشئ قيداً ثانياً.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesDocument>> PostInvoiceAsync(
        TenantId tenant,
        UserId actor,
        Guid invoiceId,
        CancellationToken cancellationToken = default)
    {
        Result<SalesDocumentView> result = await _invoices
            .PostInvoiceAsync(tenant, actor, invoiceId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>
    /// يُنشئ إشعاراً دائناً <b>مسوّدة</b> على فاتورة <b>مُرحَّلة</b>.
    /// <para>
    /// وهذا هو الطريق الوحيد إلى تصحيح فاتورة مُرحَّلة: لا تعديل ولا حذف. والوحدة نفسها
    /// ترفض الإشعار على فاتورة ليست في حالة <c>POSTED</c>.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesDocument>> DraftCreditNoteAsync(
        TenantId tenant,
        UserId actor,
        SalesCreditNoteRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<SalesDocumentView> result = await _creditNotes
            .CreateAsync(
                tenant,
                actor,
                new CreditNoteDraft(request.Number, request.InvoiceId, request.IssuedOn, Lines(request.Lines)),
                cancellationToken)
            .ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>يرحّل إشعاراً دائناً مسوّدة. حصين ضدّ التكرار بالشكل نفسه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="creditNoteId">الإشعار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesDocument>> PostCreditNoteAsync(
        TenantId tenant,
        UserId actor,
        Guid creditNoteId,
        CancellationToken cancellationToken = default)
    {
        Result<SalesDocumentView> result = await _creditNotes
            .PostAsync(tenant, actor, creditNoteId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>يقرأ أعمار الذمم المدينة في تاريخ معلوم. نقطة قراءة بحتة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">تاريخ التقرير.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<SalesAging>> ReadReceivablesAgingAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result<AgingReport> result = await _receivables
            .AgingAsync(tenant, actor, asOf, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<SalesAging>.Failure(result.Errors);
        }

        AgingReport report = result.Value;

        return Result<SalesAging>.Success(new SalesAging(
            report.AsOf,
            [.. report.Parties.Select(static party => new SalesAgingParty(party.PartyId, party.Code, party.Name, Bands(party.Buckets)))],
            Bands(report.Totals)));
    }

    private static SalesParty Party(CustomerView view) =>
        new(view.Id, view.Code, view.Name, view.CreditLimit.Amount, view.PaymentTermsDays);

    private static Result<SalesDocument> Document(Result<SalesDocumentView> result)
    {
        if (result.IsFailure)
        {
            return Result<SalesDocument>.Failure(result.Errors);
        }

        SalesDocumentView view = result.Value;

        return Result<SalesDocument>.Success(new SalesDocument(
            view.Id,
            view.Number,
            view.State,
            view.Totals.Net.Amount,
            view.Totals.Tax.Amount,
            view.Totals.Gross.Amount,
            view.EntryId,
            view.AlreadyPosted));
    }

    private static SalesAgingBands Bands(AgingBuckets buckets) => new(
        buckets.NotDue.Amount,
        buckets.Days1To30.Amount,
        buckets.Days31To60.Amount,
        buckets.Days61To90.Amount,
        buckets.Over90.Amount,
        buckets.Total.Amount);

    private List<SalesLineDraft> Lines(IReadOnlyList<SalesLineRequest> lines) =>
    [
        .. lines.Select(line => new SalesLineDraft(
            line.ItemGroup,
            line.Description,
            line.Quantity,
            Money.Of(line.UnitPrice, _currency),
            Money.Of(line.Discount, _currency),
            line.TaxClassification,
            line.TaxRate,
            line.OriginalInvoiceLineId)),
    ];
}
