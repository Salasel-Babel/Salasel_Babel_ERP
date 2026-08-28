using System.Globalization;
using Babel.Api.Errors;
using Babel.Api.Hosting;
using Babel.Api.Security;
using Babel.Api.Wire;
using Babel.Inventory.Surface;
using Babel.Purchasing.Surface;
using Babel.Sales.Surface;
using Babel.SharedKernel;

namespace Babel.Api.Endpoints;

/// <summary>
/// سطح HTTP فوق مستندات المبيعات والمشتريات.
/// <para>
/// <b>ما يفعله كل معالج هنا، بالترتيب، ولا شيء غيره:</b> يقرأ نطاق الشركة من المسار
/// ويتحقق أن الاعتماد يبلغه · يقرأ الجسم بالقواعد الصارمة · ينقل الحقول إلى السطح
/// المنشور للوحدة · ينادي · يترجم النتيجة. <b>لا قرار محاسبي واحد يقع في هذا الملف:</b>
/// لا اختيار دور، ولا اختيار حدث، ولا حساب مبلغ، ولا قاعدة توازن، ولا اسم حساب.
/// المصفوفة تقرّر، والوحدة تصف، والسطح ينقل.
/// </para>
/// <para>
/// <b>والاستحقاق ليس هنا — عمداً.</b> كل نقطة دخول في الوحدتين تحمل
/// <c>[RequiresEntitlement]</c> وتنادي <c>IEntitlementEnforcer</c> قبل أي عمل، والقاعدة 6
/// تفرض ذلك على IL. وفحصٌ ثانٍ هنا كان سيكون آليةَ تصريحٍ موازية: تُصان إحداهما وتُنسى
/// الأخرى، ولا يظهر الفارق إلا يوم يتجاوزه أحدهم.
/// </para>
/// <para>
/// <b>ولاحظ ما ليس في هذا الملفّ: لا <c>PUT</c> ولا <c>PATCH</c> ولا <c>DELETE</c> على
/// مستند.</b> والمستند المُرحَّل واقعةٌ لا تُعدَّل: تصحيحه إشعارٌ دائن يُنشئ قيداً جديداً
/// (‏ADR-0002 · ADR-0003). أما <b>المسوّدة</b> فهي قابلة للتعديل مبدئياً — ولا مسار
/// تعديل لها هنا بعد، وذلك <b>نقصُ سطحٍ مُعلَن</b> لا قرارُ منع: انظر ADR سطح المستندات.
/// </para>
/// </summary>
internal static class DocumentEndpoints
{
    /// <summary>أقصى طول لوسيط تاريخ في الاستعلام.</summary>
    private const int DateQueryLength = 10;

    /// <summary>يسجّل سطح المبيعات والمشتريات.</summary>
    /// <param name="app">مُنشئ المسارات.</param>
    public static void MapDocumentApi(this IEndpointRouteBuilder app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.MapPost(ApiRoutes.Customers, AddCustomerAsync);
        app.MapGet(ApiRoutes.Customer, ReadCustomerAsync);
        app.MapPost(ApiRoutes.SalesInvoices, DraftInvoiceAsync);
        app.MapGet(ApiRoutes.SalesInvoice, ReadInvoiceAsync);
        app.MapPost(ApiRoutes.SalesInvoicePosting, PostInvoiceAsync);
        app.MapPost(ApiRoutes.CreditNotes, DraftCreditNoteAsync);
        app.MapPost(ApiRoutes.CreditNotePosting, PostCreditNoteAsync);
        app.MapGet(ApiRoutes.ReceivablesAging, ReceivablesAgingAsync);

        app.MapPost(ApiRoutes.Suppliers, AddSupplierAsync);
        app.MapGet(ApiRoutes.Supplier, ReadSupplierAsync);
        app.MapPost(ApiRoutes.SupplierBills, DraftExpenseBillAsync);
        app.MapGet(ApiRoutes.SupplierBill, ReadBillAsync);
        app.MapPost(ApiRoutes.SupplierBillPosting, PostBillAsync);
        app.MapGet(ApiRoutes.PayablesAging, PayablesAgingAsync);

        // ── سلسلة المشتريات المخزنية: أمر ← استلام ← فاتورة مخزنية ← مرتجع ────
        // وكلّها منشورة معاً عمداً: بابٌ لا يوصل إليه بابٌ آخر على هذا السطح يعطي
        // عقداً يَعِد بدورة لا تكتمل — وهو نصّ ADR-0044 في رفضه نشر المرتجع وحده.
        app.MapPost(ApiRoutes.PurchaseOrders, DraftOrderAsync);
        app.MapGet(ApiRoutes.PurchaseOrder, ReadOrderAsync);
        app.MapPost(ApiRoutes.GoodsReceipts, DraftReceiptAsync);
        app.MapGet(ApiRoutes.GoodsReceipt, ReadReceiptAsync);
        app.MapPost(ApiRoutes.GoodsReceiptPosting, PostReceiptAsync);
        app.MapPost(ApiRoutes.StockBills, DraftStockBillAsync);
        app.MapPost(ApiRoutes.PurchaseReturns, DraftPurchaseReturnAsync);
        app.MapGet(ApiRoutes.PurchaseReturn, ReadPurchaseReturnAsync);
        app.MapPost(ApiRoutes.PurchaseReturnPosting, PostPurchaseReturnAsync);

        // ── المخزون ──────────────────────────────────────────────────────────
        app.MapPost(ApiRoutes.Items, AddItemAsync);
        app.MapGet(ApiRoutes.Items, ListItemsAsync);
        app.MapGet(ApiRoutes.Item, ReadItemAsync);
        app.MapPost(ApiRoutes.StockMovements, DraftStockMovementAsync);
        app.MapGet(ApiRoutes.StockMovements, ListStockMovementsAsync);
        app.MapPost(ApiRoutes.StockMovementPosting, PostStockMovementAsync);
        app.MapGet(ApiRoutes.StockBalances, ReadStockBalancesAsync);
        app.MapGet(ApiRoutes.InventoryValuation, ReadInventoryValuationAsync);
    }

    // ── المبيعات ─────────────────────────────────────────────────────────────

    private static async Task<IResult> AddCustomerAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (PartyRequestDto? dto, IResult? refused) = await BodyAsync<PartyRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        SalesPartyRequest request;
        try
        {
            request = DocumentMapping.ToCustomerRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<SalesParty> result = await sales
            .AddCustomerAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.Customer, companyId, "customerId", result.Value.Id));
    }

    private static async Task<IResult> ReadCustomerAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "customerId", out Guid customerId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<SalesParty> result = await sales
            .ReadCustomerAsync(new TenantId(companyId), Actor(context), customerId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DraftInvoiceAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (SalesInvoiceRequestDto? dto, IResult? refused) =
            await BodyAsync<SalesInvoiceRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        SalesInvoiceRequest request;
        try
        {
            request = DocumentMapping.ToInvoiceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<SalesDocument> result = await sales
            .DraftInvoiceAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.SalesInvoice, companyId, "invoiceId", result.Value.Id));
    }

    private static async Task<IResult> ReadInvoiceAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "invoiceId", out Guid invoiceId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<SalesDocument> result = await sales
            .ReadInvoiceAsync(new TenantId(companyId), Actor(context), invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostInvoiceAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "invoiceId", out Guid invoiceId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<SalesDocument> result = await sales
            .PostInvoiceAsync(new TenantId(companyId), Actor(context), invoiceId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.SalesInvoice, companyId, "invoiceId", invoiceId));
    }

    private static async Task<IResult> DraftCreditNoteAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (CreditNoteRequestDto? dto, IResult? refused) =
            await BodyAsync<CreditNoteRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        SalesCreditNoteRequest request;
        try
        {
            request = DocumentMapping.ToCreditNoteRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<SalesDocument> result = await sales
            .DraftCreditNoteAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), null);
    }

    private static async Task<IResult> PostCreditNoteAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "creditNoteId", out Guid creditNoteId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<SalesDocument> result = await sales
            .PostCreditNoteAsync(new TenantId(companyId), Actor(context), creditNoteId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), null);
    }

    private static async Task<IResult> ReceivablesAgingAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        DateOnly asOf;
        try
        {
            asOf = WireMapping.ReadDate(Scope.Query(context, "asOf", required: true, DateQueryLength), "asOf");
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<SalesAging> result = await sales
            .ReadReceivablesAgingAsync(new TenantId(companyId), Actor(context), asOf, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── المشتريات ────────────────────────────────────────────────────────────

    private static async Task<IResult> AddSupplierAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (PartyRequestDto? dto, IResult? refused) = await BodyAsync<PartyRequestDto>(context, cancellationToken)
            .ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        PurchasingPartyRequest request;
        try
        {
            request = DocumentMapping.ToSupplierRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingParty> result = await purchasing
            .AddSupplierAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.Supplier, companyId, "supplierId", result.Value.Id));
    }

    private static async Task<IResult> ReadSupplierAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "supplierId", out Guid supplierId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingParty> result = await purchasing
            .ReadSupplierAsync(new TenantId(companyId), Actor(context), supplierId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DraftExpenseBillAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (ExpenseBillRequestDto? dto, IResult? refused) =
            await BodyAsync<ExpenseBillRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        PurchasingExpenseBillRequest request;
        try
        {
            request = DocumentMapping.ToExpenseBillRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingDocument> result = await purchasing
            .DraftExpenseBillAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.SupplierBill, companyId, "billId", result.Value.Id));
    }

    private static async Task<IResult> ReadBillAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "billId", out Guid billId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocument> result = await purchasing
            .ReadBillAsync(new TenantId(companyId), Actor(context), billId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostBillAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "billId", out Guid billId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocument> result = await purchasing
            .PostBillAsync(new TenantId(companyId), Actor(context), billId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.SupplierBill, companyId, "billId", billId));
    }

    private static async Task<IResult> PayablesAgingAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        DateOnly asOf;
        try
        {
            asOf = WireMapping.ReadDate(Scope.Query(context, "asOf", required: true, DateQueryLength), "asOf");
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingAging> result = await purchasing
            .ReadPayablesAgingAsync(new TenantId(companyId), Actor(context), asOf, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── سلسلة المشتريات المخزنية ─────────────────────────────────────────────

    private static async Task<IResult> DraftOrderAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (PurchaseOrderRequestDto? dto, IResult? refused) =
            await BodyAsync<PurchaseOrderRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        PurchasingOrderRequest request;
        try
        {
            request = DocumentMapping.ToOrderRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingDocument> result = await purchasing
            .DraftOrderAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.PurchaseOrder, companyId, "orderId", result.Value.Id));
    }

    private static async Task<IResult> ReadOrderAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "orderId", out Guid orderId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocumentWithLines> result = await purchasing
            .ReadOrderAsync(new TenantId(companyId), Actor(context), orderId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DraftReceiptAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (GoodsReceiptRequestDto? dto, IResult? refused) =
            await BodyAsync<GoodsReceiptRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        PurchasingReceiptRequest request;
        try
        {
            request = DocumentMapping.ToReceiptRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingDocument> result = await purchasing
            .DraftReceiptAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.GoodsReceipt, companyId, "receiptId", result.Value.Id));
    }

    private static async Task<IResult> ReadReceiptAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "receiptId", out Guid receiptId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocumentWithLines> result = await purchasing
            .ReadReceiptAsync(new TenantId(companyId), Actor(context), receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostReceiptAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "receiptId", out Guid receiptId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocument> result = await purchasing
            .PostReceiptAsync(new TenantId(companyId), Actor(context), receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.GoodsReceipt, companyId, "receiptId", receiptId));
    }

    private static async Task<IResult> DraftStockBillAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (StockBillRequestDto? dto, IResult? refused) =
            await BodyAsync<StockBillRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        PurchasingStockBillRequest request;
        try
        {
            request = DocumentMapping.ToStockBillRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingDocument> result = await purchasing
            .DraftStockBillAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        // والعنوان مورد **فاتورة المورد**: مستندٌ واحد وعنوانٌ واحد، تُقرأ وتُرحَّل منه.
        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.SupplierBill, companyId, "billId", result.Value.Id));
    }

    private static async Task<IResult> DraftPurchaseReturnAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (PurchaseReturnRequestDto? dto, IResult? refused) =
            await BodyAsync<PurchaseReturnRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        PurchasingReturnRequest request;
        try
        {
            request = DocumentMapping.ToPurchaseReturnRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingDocument> result = await purchasing
            .DraftReturnAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.PurchaseReturn, companyId, "returnId", result.Value.Id));
    }

    private static async Task<IResult> ReadPurchaseReturnAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "returnId", out Guid returnId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocument> result = await purchasing
            .ReadReturnAsync(new TenantId(companyId), Actor(context), returnId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostPurchaseReturnAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "returnId", out Guid returnId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocument> result = await purchasing
            .PostReturnAsync(new TenantId(companyId), Actor(context), returnId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.PurchaseReturn, companyId, "returnId", returnId));
    }

    // ── المخزون ──────────────────────────────────────────────────────────────

    private static async Task<IResult> AddItemAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (ItemRequestDto? dto, IResult? refused) =
            await BodyAsync<ItemRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryItemRequest request;
        try
        {
            request = DocumentMapping.ToItemRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryItem> result = await inventory
            .AddItemAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.Item, companyId, "itemId", result.Value.Id));
    }

    private static async Task<IResult> ReadItemAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "itemId", out Guid itemId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryItem> result = await inventory
            .ReadItemAsync(new TenantId(companyId), Actor(context), itemId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ListItemsAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryItem>> result = await inventory
            .ListItemsAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<ItemDto> items = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new ItemListDto(items.Count, items), ApiJson.Options);
    }

    private static async Task<IResult> DraftStockMovementAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (StockMovementRequestDto? dto, IResult? refused) =
            await BodyAsync<StockMovementRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryStockMovementRequest request;
        try
        {
            request = DocumentMapping.ToStockMovementRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryStockMovement> result = await inventory
            .DraftMovementAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), null);
    }

    private static async Task<IResult> ListStockMovementsAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryStockMovement>> result = await inventory
            .ListMovementsAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<StockMovementDto> movements = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new StockMovementListDto(movements.Count, movements), ApiJson.Options);
    }

    private static async Task<IResult> PostStockMovementAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "movementId", out Guid movementId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryStockMovement> result = await inventory
            .PostMovementAsync(new TenantId(companyId), Actor(context), movementId, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        StockMovementDto dto = DocumentMapping.ToDto(result.Value);

        return Results.Json(
            dto, ApiJson.Options, statusCode: dto.AlreadyPosted ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    private static async Task<IResult> ReadStockBalancesAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryBalance>> result = await inventory
            .ReadBalancesAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<StockBalanceDto> balances = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new StockBalanceListDto(balances.Count, balances), ApiJson.Options);
    }

    private static async Task<IResult> ReadInventoryValuationAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        DateOnly asOf;
        try
        {
            asOf = WireMapping.ReadDate(Scope.Query(context, "asOf", required: true, DateQueryLength), "asOf");
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryValuationReport> result = await inventory
            .ReadValuationAsync(new TenantId(companyId), Actor(context), asOf, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── مشترك ────────────────────────────────────────────────────────────────

    /// <summary>الفاعل من الاعتماد وحده — لا من ترويسة ولا من حقل في الجسم.</summary>
    private static UserId Actor(HttpContext context) => RequestPrincipal.Of(context).User;

    private static async Task<(T? Dto, IResult? Refused)> BodyAsync<T>(
        HttpContext context,
        CancellationToken cancellationToken)
        where T : class
    {
        T? dto;
        try
        {
            dto = await context.Request
                .ReadFromJsonAsync<T>(ApiJson.Options, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (System.Text.Json.JsonException exception)
        {
            return (null, Scope.BadJson(context, exception));
        }

        return dto is null
            ? (null, HttpProblemResults.Code(
                context, "wire.body.missing", "جسم الطلب مفقود.", "The request body is missing."))
            : (dto, null);
    }

    private static IResult Created(HttpContext context, object dto, string? location)
    {
        if (location is not null)
        {
            context.Response.Headers.Location = location;
        }

        return Results.Json(dto, ApiJson.Options, statusCode: StatusCodes.Status201Created);
    }

    /// <summary>
    /// ‏201 للترحيل الأول و200 للوصول الثاني بالهوية نفسها.
    /// <para>
    /// <b>والفارق مُعلن في الجسم أيضاً</b> بـ<c>alreadyPosted</c>، بالشكل نفسه الذي
    /// يسلكه ترحيل القيد: رمز الحالة وحده يضيع خلف أي وسيط يعيد التوجيه، وعميلٌ أعاد
    /// المحاولة بعد انقطاع شبكة يحتاج أن يعرف أيّ النداءين رحّل.
    /// </para>
    /// </summary>
    private static IResult Posted(HttpContext context, CommercialDocumentDto dto, string? location)
    {
        if (location is not null)
        {
            context.Response.Headers.Location = location;
        }

        return Results.Json(
            dto,
            ApiJson.Options,
            statusCode: dto.AlreadyPosted ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }

    private static string Location(string template, Guid companyId, string idName, Guid id) => template
        .Replace("{companyId}", companyId.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal)
        .Replace("{" + idName + "}", id.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal);
}
