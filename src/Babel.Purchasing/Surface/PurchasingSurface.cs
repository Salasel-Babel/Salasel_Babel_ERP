using Babel.Contracts.Inventory;
using Babel.Purchasing.Application;
using Babel.SharedKernel;

namespace Babel.Purchasing.Surface;

/// <summary>
/// <b>السطح المنشور لوحدة المشتريات</b> — وهو ما يجوز للجذر التركيبي أن يسمّيه، ولا شيء غيره.
/// <para>
/// نفس سبب <c>Babel.Sales.Surface.SalesSurface</c> ونفس شكله: القاعدة 13 (البند ب) تمنع
/// <c>Babel.Api</c> من ذكر أي نوع من <c>Babel.Purchasing.Application</c>، ولو أُضيف إلى
/// قائمة السطح المنشور. فالباب المشروع سطحٌ مسمّى خارج فضاءات الداخل.
/// </para>
/// <para>
/// <b>ولا استحقاق يُنفَّذ هنا:</b> كل دالّة تنادي خدمة تطبيق تحمل سمة الاستحقاق وتنادي
/// المنفِّذ أوّل شيء. جدول القرار موضعٌ واحد (القاعدة 6).
/// </para>
/// <para>
/// <b>وما ليس على هذا السطح — وهو مقصود ومكتوب:</b> لا إشعار مدين. الإشعار المدين لا
/// يُقبل إلا على فاتورة <c>STOCK</c>، والفاتورة المخزنية لا توجد إلا عن استلام، والاستلام
/// لا يُرحَّل إلا عبر <c>IInventoryValuation</c> — أي أن <b>مسار مرتجع المشتريات يفرض
/// وحدة المخزون</b>. ونشرُ بابٍ لا يوصل إليه بابٌ آخر على هذا السطح كان سيعطي عقداً
/// يَعِد بدورة لا تكتمل.
/// </para>
/// </summary>
public sealed class PurchasingSurface
{
    private readonly SupplierService _suppliers;
    private readonly SupplierBillService _bills;
    private readonly PayablesService _payables;
    private readonly PurchaseOrderService _orders;
    private readonly GoodsReceiptService _receipts;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ السطح فوق خدمات الوحدة.</summary>
    /// <param name="suppliers">خدمة الموردين.</param>
    /// <param name="bills">خدمة فواتير الموردين والمرتجعات.</param>
    /// <param name="payables">خدمة الذمم الدائنة.</param>
    /// <param name="orders">خدمة أوامر الشراء — أول أضلاع المطابقة الثلاثية.</param>
    /// <param name="receipts">خدمة استلام البضاعة — ثاني الأضلاع، وهي التي تُدخل المخزون.</param>
    /// <param name="options">إعدادات الوحدة — ومنها عملة المنشأة.</param>
    public PurchasingSurface(
        SupplierService suppliers,
        SupplierBillService bills,
        PayablesService payables,
        PurchaseOrderService orders,
        GoodsReceiptService receipts,
        PurchasingOptions options)
    {
        ArgumentNullException.ThrowIfNull(suppliers);
        ArgumentNullException.ThrowIfNull(bills);
        ArgumentNullException.ThrowIfNull(payables);
        ArgumentNullException.ThrowIfNull(orders);
        ArgumentNullException.ThrowIfNull(receipts);
        ArgumentNullException.ThrowIfNull(options);

        _suppliers = suppliers;
        _bills = bills;
        _payables = payables;
        _orders = orders;
        _receipts = receipts;
        _currency = CurrencyCode.FromString(options.CompanyCurrency);
    }

    /// <summary>يسجّل مورداً جديداً. بيانات أساسية، لا مستند ولا ترحيل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingParty>> AddSupplierAsync(
        TenantId tenant,
        UserId actor,
        PurchasingPartyRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<SupplierView> result = await _suppliers
            .CreateAsync(
                tenant,
                actor,
                new SupplierDraft(
                    request.Code,
                    request.Name,
                    Money.Of(request.CreditLimit, _currency),
                    request.PaymentTermsDays,
                    request.VatNumber),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<PurchasingParty>.Failure(result.Errors)
            : Result<PurchasingParty>.Success(Party(result.Value));
    }

    /// <summary>يقرأ مورداً واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="supplierId">معرّف المورد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingParty>> ReadSupplierAsync(
        TenantId tenant,
        UserId actor,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        Result<SupplierView> result = await _suppliers
            .GetAsync(tenant, actor, supplierId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<PurchasingParty>.Failure(result.Errors)
            : Result<PurchasingParty>.Success(Party(result.Value));
    }

    /// <summary>
    /// يُنشئ فاتورة مصروف <b>مسوّدة</b>. لا قيد ولا أثر في الدفتر: الترحيل خطوة مستقلّة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> DraftExpenseBillAsync(
        TenantId tenant,
        UserId actor,
        PurchasingExpenseBillRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PurchasingDocumentView> result = await _bills
            .CreateExpenseBillAsync(
                tenant,
                actor,
                new ExpenseBillDraft(
                    request.Number,
                    request.SupplierId,
                    request.IssuedOn,
                    request.ExpenseCategory,
                    request.CostCenterId,
                    Lines(request.Lines)),
                cancellationToken)
            .ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>يقرأ فاتورة مورد بحالتها ومجاميعها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="billId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> ReadBillAsync(
        TenantId tenant,
        UserId actor,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        Result<PurchasingDocumentView> result = await _bills
            .GetBillAsync(tenant, actor, billId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>
    /// يرحّل فاتورة مورد مسوّدة فتصير <b>واقعة محاسبية</b>. حصين ضدّ التكرار: الوصول
    /// الثاني بالهوية نفسها يُرجع المستند ذاته و<c>AlreadyPosted = true</c> بلا قيد ثانٍ.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="billId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> PostBillAsync(
        TenantId tenant,
        UserId actor,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        Result<PurchasingDocumentView> result = await _bills
            .PostBillAsync(tenant, actor, billId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>يقرأ أعمار الذمم الدائنة في تاريخ معلوم. نقطة قراءة بحتة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">تاريخ التقرير.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingAging>> ReadPayablesAgingAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result<AgingReport> result = await _payables
            .AgingAsync(tenant, actor, asOf, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<PurchasingAging>.Failure(result.Errors);
        }

        AgingReport report = result.Value;

        return Result<PurchasingAging>.Success(new PurchasingAging(
            report.AsOf,
            [.. report.Parties.Select(static party =>
                new PurchasingAgingParty(party.PartyId, party.Code, party.Name, Bands(party.Buckets)))],
            Bands(report.Totals)));
    }

    private static PurchasingParty Party(SupplierView view) =>
        new(view.Id, view.Code, view.Name, view.CreditLimit.Amount, view.PaymentTermsDays, view.VatNumber);

    /// <summary>
    /// يُنشئ أمر شراء — <b>أول أضلاع المطابقة الثلاثية</b>: ما طُلب.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> DraftOrderAsync(
        TenantId tenant,
        UserId actor,
        PurchasingOrderRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PurchasingDocumentView> result = await _orders
            .CreateOrderAsync(
                tenant,
                actor,
                new PurchaseOrderDraft(
                    request.Number,
                    request.SupplierId,
                    request.OrderedOn,
                    request.WarehouseId,
                    request.CostCenterId,
                    StockLines(request.Lines)),
                requestId: null,
                cancellationToken)
            .ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>
    /// يقرأ أمر شراء <b>بسطوره</b> — ومعرّفات السطور هي مدخل الاستلام.
    /// <para>
    /// مورد واحد لا موردان: «ما حال الأمر؟» و«ما سطوره؟» سؤالان يُجابان بطلبٍ واحد،
    /// فلا يفترقان عند أول تعديل.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="orderId">الأمر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocumentWithLines>> ReadOrderAsync(
        TenantId tenant,
        UserId actor,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        Result<PurchasingDocumentView> order = await _orders
            .GetOrderAsync(tenant, actor, orderId, cancellationToken).ConfigureAwait(false);

        if (order.IsFailure)
        {
            return Result<PurchasingDocumentWithLines>.Failure(order.Errors);
        }

        Result<IReadOnlyList<PurchaseLineView>> lines = await _orders
            .GetOrderLinesAsync(tenant, actor, orderId, cancellationToken).ConfigureAwait(false);

        return lines.IsFailure
            ? Result<PurchasingDocumentWithLines>.Failure(lines.Errors)
            : Result<PurchasingDocumentWithLines>.Success(WithLines(order.Value, lines.Value));
    }

    /// <summary>يسجّل استلام بضاعة <b>مسوّدة</b>. لا مخزون ولا قيد: الترحيل خطوة مستقلّة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> DraftReceiptAsync(
        TenantId tenant,
        UserId actor,
        PurchasingReceiptRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PurchasingDocumentView> result = await _receipts
            .RecordAsync(
                tenant,
                actor,
                new GoodsReceiptDraft(
                    request.Number,
                    request.OrderId,
                    request.ReceivedOn,
                    [.. request.Lines.Select(static line => new GoodsReceiptLineDraft(line.OrderLineId, line.Quantity))]),
                cancellationToken)
            .ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>يقرأ استلاماً <b>بسطوره</b> — ومعرّفات السطور مدخل الفاتورة والمرتجع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="receiptId">الاستلام.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocumentWithLines>> ReadReceiptAsync(
        TenantId tenant,
        UserId actor,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result<PurchasingDocumentView> receipt = await _receipts
            .GetReceiptAsync(tenant, actor, receiptId, cancellationToken).ConfigureAwait(false);

        if (receipt.IsFailure)
        {
            return Result<PurchasingDocumentWithLines>.Failure(receipt.Errors);
        }

        Result<IReadOnlyList<PurchaseLineView>> lines = await _receipts
            .GetLinesAsync(tenant, actor, receiptId, cancellationToken).ConfigureAwait(false);

        return lines.IsFailure
            ? Result<PurchasingDocumentWithLines>.Failure(lines.Errors)
            : Result<PurchasingDocumentWithLines>.Success(WithLines(receipt.Value, lines.Value));
    }

    /// <summary>
    /// يرحّل استلام بضاعة: <b>حركةٌ في دفتر المخزون المساعد ثم قيدٌ في الدفتر</b>.
    /// حصين ضدّ التكرار بهوية الترحيل.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="receiptId">الاستلام.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> PostReceiptAsync(
        TenantId tenant,
        UserId actor,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result<PurchasingDocumentView> result = await _receipts
            .PostAsync(tenant, actor, receiptId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>
    /// يُنشئ فاتورة مورد <b>مخزنية</b> مسوّدة — الضلع الثالث من المطابقة الثلاثية.
    /// <para>
    /// وتُقرأ وتُرحَّل من مورد فاتورة المورد نفسه: مستندٌ واحد وعنوانٌ واحد.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> DraftStockBillAsync(
        TenantId tenant,
        UserId actor,
        PurchasingStockBillRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PurchasingDocumentView> result = await _bills
            .CreateStockBillAsync(
                tenant,
                actor,
                new StockBillDraft(
                    request.Number,
                    request.ReceiptId,
                    request.IssuedOn,
                    [
                        .. request.Lines.Select(line => new SupplierBillLineDraft(
                            line.ReceiptLineId,
                            line.Quantity,
                            Money.Of(line.UnitPrice, _currency),
                            line.TaxClassification,
                            line.TaxRate)),
                    ]),
                cancellationToken)
            .ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>
    /// يُنشئ <b>مرتجع مشتريات</b> مسوّدة على فاتورة مخزنية مُرحَّلة.
    /// <para>
    /// والمبلغ لا يُسلَّم: يُحسب لحظة الترحيل بتكلفة الاستلام الأصلي في وحدة المخزون.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> DraftReturnAsync(
        TenantId tenant,
        UserId actor,
        PurchasingReturnRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<PurchasingDocumentView> result = await _bills
            .CreateDebitNoteAsync(
                tenant,
                actor,
                new DebitNoteDraft(
                    request.Number,
                    request.BillId,
                    request.IssuedOn,
                    request.ReceiptLineId,
                    request.Quantity,
                    Money.Of(request.Tax, _currency)),
                cancellationToken)
            .ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>يقرأ مرتجع مشتريات بحالته ومجاميعه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="returnId">المرتجع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> ReadReturnAsync(
        TenantId tenant,
        UserId actor,
        Guid returnId,
        CancellationToken cancellationToken = default)
    {
        Result<PurchasingDocumentView> result = await _bills
            .GetDebitNoteAsync(tenant, actor, returnId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    /// <summary>
    /// يرحّل مرتجع مشتريات: <b>البضاعة تخرج من المخزون بتكلفة استلامها، ثم يُنقص
    /// الحساب الضابط للمورد</b>. حصين ضدّ التكرار بالشكل نفسه.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="returnId">المرتجع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<PurchasingDocument>> PostReturnAsync(
        TenantId tenant,
        UserId actor,
        Guid returnId,
        CancellationToken cancellationToken = default)
    {
        Result<PurchasingDocumentView> result = await _bills
            .PostDebitNoteAsync(tenant, actor, returnId, cancellationToken).ConfigureAwait(false);

        return Document(result);
    }

    private static PurchasingDocumentWithLines WithLines(
        PurchasingDocumentView view, IReadOnlyList<PurchaseLineView> lines) => new(
        new PurchasingDocument(
            view.Id,
            view.Number,
            view.State,
            view.Totals.Net.Amount,
            view.Totals.Tax.Amount,
            view.Totals.Gross.Amount,
            view.EntryId,
            view.AlreadyPosted),
        [
            .. lines.Select(static line => new PurchasingLine(
                line.Id, line.LineNo, line.ItemId, line.Quantity, line.Unit, line.UnitPrice.Amount)),
        ]);

    private List<PurchaseLineDraft> StockLines(IReadOnlyList<PurchasingStockLineRequest> lines) =>
    [
        .. lines.Select(line => new PurchaseLineDraft(
            line.ItemId,
            line.ItemGroup,
            line.Description,
            line.Quantity,
            line.Unit,
            Money.Of(line.UnitPrice, _currency),
            line.TaxClassification,
            line.TaxRate)),
    ];

    private static Result<PurchasingDocument> Document(Result<PurchasingDocumentView> result)
    {
        if (result.IsFailure)
        {
            return Result<PurchasingDocument>.Failure(result.Errors);
        }

        PurchasingDocumentView view = result.Value;

        return Result<PurchasingDocument>.Success(new PurchasingDocument(
            view.Id,
            view.Number,
            view.State,
            view.Totals.Net.Amount,
            view.Totals.Tax.Amount,
            view.Totals.Gross.Amount,
            view.EntryId,
            view.AlreadyPosted));
    }

    private static PurchasingAgingBands Bands(AgingBuckets buckets) => new(
        buckets.NotDue.Amount,
        buckets.Days1To30.Amount,
        buckets.Days31To60.Amount,
        buckets.Days61To90.Amount,
        buckets.Over90.Amount,
        buckets.Total.Amount);

    private List<PurchaseLineDraft> Lines(IReadOnlyList<PurchasingLineRequest> lines) =>
    [
        .. lines.Select(line => new PurchaseLineDraft(
            line.ItemId,
            line.ItemGroup,
            line.Description,
            line.Quantity,

            // سطر المصروف لا يُحرّك مخزوناً، فوحدته **العدّ صراحةً**. وسطر الأمر
            // المخزني يحمل وحدته من الطلب — انظر `StockLines`.
            InventoryUnits.Each,
            Money.Of(line.UnitPrice, _currency),
            line.TaxClassification,
            line.TaxRate,
            line.TaxRecoverable)),
    ];
}
