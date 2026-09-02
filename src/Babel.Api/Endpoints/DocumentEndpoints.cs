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
        app.MapPost(ApiRoutes.CustomerReceipts, DraftCustomerReceiptAsync);
        app.MapGet(ApiRoutes.CustomerReceipt, ReadCustomerReceiptAsync);
        app.MapPost(ApiRoutes.CustomerReceiptPosting, PostCustomerReceiptAsync);
        app.MapGet(ApiRoutes.ReceivablesAging, ReceivablesAgingAsync);

        app.MapPost(ApiRoutes.Suppliers, AddSupplierAsync);
        app.MapGet(ApiRoutes.Supplier, ReadSupplierAsync);
        app.MapPost(ApiRoutes.SupplierBills, DraftExpenseBillAsync);
        app.MapGet(ApiRoutes.SupplierBill, ReadBillAsync);
        app.MapPost(ApiRoutes.SupplierBillPosting, PostBillAsync);
        app.MapPost(ApiRoutes.SupplierPayments, DraftSupplierPaymentAsync);
        app.MapGet(ApiRoutes.SupplierPayment, ReadSupplierPaymentAsync);
        app.MapPost(ApiRoutes.SupplierPaymentPosting, PostSupplierPaymentAsync);

        // ‏**وأمر الشراء بابان لا ثلاثة.** لا `MapPost(… + "/posting")` هنا، ولا يجوز
        // أن يوجد: أمر الشراء التزام تعاقدي لا حدث محاسبي، والقيد الأول في دورة الشراء
        // هو الاستلام. وغيابُ السطر مقروءٌ في العقد المنشور نفسه، لا في تعليق وحده.
        app.MapPost(ApiRoutes.PurchaseOrders, CreatePurchaseOrderAsync);
        app.MapGet(ApiRoutes.PurchaseOrder, ReadPurchaseOrderAsync);

        app.MapPost(ApiRoutes.GoodsReceipts, DraftGoodsReceiptAsync);
        app.MapGet(ApiRoutes.GoodsReceipt, ReadGoodsReceiptAsync);
        app.MapPost(ApiRoutes.GoodsReceiptPosting, PostGoodsReceiptAsync);
        app.MapGet(ApiRoutes.GoodsReceiptLines, ReadGoodsReceiptLinesAsync);

        app.MapGet(ApiRoutes.PayablesAging, PayablesAgingAsync);

        // ── تتمّة سلسلة المشتريات المخزنية: فاتورة مخزنية ← مرتجع ────────────
        // وأوّلا أضلاعها — الأمر والاستلام — منشوران أعلاه منذ ADR-0047 ولا يُنشران
        // مرّتين. وسطورُ الاستلام تُقرأ من موردها الفرعي، فلا يبقى بابٌ لا يوصل إليه
        // بابٌ آخر على هذا السطح — وهو نصّ ADR-0044 في رفضه نشر المرتجع وحده.
        app.MapPost(ApiRoutes.StockBills, DraftStockBillAsync);
        app.MapPost(ApiRoutes.PurchaseReturns, DraftPurchaseReturnAsync);
        app.MapGet(ApiRoutes.PurchaseReturn, ReadPurchaseReturnAsync);
        app.MapPost(ApiRoutes.PurchaseReturnPosting, PostPurchaseReturnAsync);

        // ── المخزون ──────────────────────────────────────────────────────────
        app.MapPost(ApiRoutes.Items, AddItemAsync);
        app.MapGet(ApiRoutes.Items, ListItemsAsync);
        app.MapGet(ApiRoutes.Item, ReadItemAsync);
        app.MapPost(ApiRoutes.ItemRevision, ReviseItemAsync);
        app.MapPost(ApiRoutes.ItemDeactivation, DeactivateItemAsync);
        app.MapGet(ApiRoutes.ItemLifecycle, ReadItemLifecycleAsync);
        app.MapPost(ApiRoutes.StockMovements, DraftStockMovementAsync);
        app.MapGet(ApiRoutes.StockMovements, ListStockMovementsAsync);
        app.MapPost(ApiRoutes.StockMovementPosting, PostStockMovementAsync);
        app.MapGet(ApiRoutes.StockBalances, ReadStockBalancesAsync);
        app.MapGet(ApiRoutes.InventoryValuation, ReadInventoryValuationAsync);

        // ── تسكين المخزون ────────────────────────────────────────────────────
        // خمس عمليات لكل مستوى من الهرم الثلاثي، **والشكل واحد**: تسجيل · قائمة ·
        // قراءة · إعادة تسمية · تعطيل. ولا `PUT` ولا `PATCH` ولا `DELETE` على موضع:
        // الرمز هوية تحملها حركات مضت.
        app.MapPost(ApiRoutes.Warehouses, AddWarehouseAsync);
        app.MapGet(ApiRoutes.Warehouses, ListWarehousesAsync);
        app.MapGet(ApiRoutes.Warehouse, ReadWarehouseAsync);
        app.MapPost(ApiRoutes.WarehouseName, RenameWarehouseAsync);
        app.MapPost(ApiRoutes.WarehouseDeactivation, DeactivateWarehouseAsync);

        app.MapPost(ApiRoutes.StorageLocations, AddStorageLocationAsync);
        app.MapGet(ApiRoutes.StorageLocations, ListStorageLocationsAsync);
        app.MapGet(ApiRoutes.StorageLocation, ReadStorageLocationAsync);
        app.MapPost(ApiRoutes.StorageLocationName, RenameStorageLocationAsync);
        app.MapPost(ApiRoutes.StorageLocationDeactivation, DeactivateStorageLocationAsync);

        app.MapPost(ApiRoutes.StorageBins, AddStorageBinAsync);
        app.MapGet(ApiRoutes.StorageBins, ListStorageBinsAsync);
        app.MapGet(ApiRoutes.StorageBin, ReadStorageBinAsync);
        app.MapPost(ApiRoutes.StorageBinName, RenameStorageBinAsync);
        app.MapPost(ApiRoutes.StorageBinDeactivation, DeactivateStorageBinAsync);

        app.MapPost(ApiRoutes.StockTransfers, DraftStockTransferAsync);
        app.MapGet(ApiRoutes.StockTransfers, ListStockTransfersAsync);
        app.MapGet(ApiRoutes.StockTransfer, ReadStockTransferAsync);
        app.MapPost(ApiRoutes.StockTransferMovement, MoveStockTransferAsync);

        app.MapGet(ApiRoutes.PlacementBalances, ReadPlacementBalancesAsync);

        // ── وحدات القياس ─────────────────────────────────────────────────────
        app.MapPost(ApiRoutes.UnitsOfMeasure, AddUnitOfMeasureAsync);
        app.MapGet(ApiRoutes.UnitsOfMeasure, ListUnitsOfMeasureAsync);
        app.MapGet(ApiRoutes.UnitOfMeasure, ReadUnitOfMeasureAsync);
        app.MapPost(ApiRoutes.UnitOfMeasureDeactivation, DeactivateUnitOfMeasureAsync);
        app.MapPost(ApiRoutes.UnitConversions, AddUnitConversionAsync);
        app.MapGet(ApiRoutes.UnitConversions, ListUnitConversionsAsync);
        app.MapPost(ApiRoutes.UnitConversionTrials, ConvertQuantityAsync);
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

    private static async Task<IResult> DraftCustomerReceiptAsync(
        HttpContext context,
        SalesSurface sales,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (CustomerReceiptRequestDto? dto, IResult? refused) =
            await BodyAsync<CustomerReceiptRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        SalesReceiptRequest request;
        try
        {
            request = DocumentMapping.ToCustomerReceiptRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<SalesDocument> result = await sales
            .DraftCustomerReceiptAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.CustomerReceipt, companyId, "receiptId", result.Value.Id));
    }

    private static async Task<IResult> ReadCustomerReceiptAsync(
        HttpContext context,
        SalesSurface sales,
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

        Result<SalesDocument> result = await sales
            .ReadCustomerReceiptAsync(new TenantId(companyId), Actor(context), receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostCustomerReceiptAsync(
        HttpContext context,
        SalesSurface sales,
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

        Result<SalesDocument> result = await sales
            .PostCustomerReceiptAsync(new TenantId(companyId), Actor(context), receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.CustomerReceipt, companyId, "receiptId", receiptId));
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

    private static async Task<IResult> DraftSupplierPaymentAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (SupplierPaymentRequestDto? dto, IResult? refused) =
            await BodyAsync<SupplierPaymentRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        PurchasingPaymentRequest request;
        try
        {
            request = DocumentMapping.ToSupplierPaymentRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingDocument> result = await purchasing
            .DraftSupplierPaymentAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.SupplierPayment, companyId, "paymentId", result.Value.Id));
    }

    private static async Task<IResult> ReadSupplierPaymentAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "paymentId", out Guid paymentId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocument> result = await purchasing
            .ReadSupplierPaymentAsync(new TenantId(companyId), Actor(context), paymentId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostSupplierPaymentAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "paymentId", out Guid paymentId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<PurchasingDocument> result = await purchasing
            .PostSupplierPaymentAsync(new TenantId(companyId), Actor(context), paymentId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.SupplierPayment, companyId, "paymentId", paymentId));
    }

    private static async Task<IResult> CreatePurchaseOrderAsync(
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
            request = DocumentMapping.ToPurchaseOrderRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingOrder> result = await purchasing
            .CreatePurchaseOrderAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.PurchaseOrder, companyId, "orderId", result.Value.Id));
    }

    private static async Task<IResult> ReadPurchaseOrderAsync(
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

        Result<PurchasingOrder> result = await purchasing
            .ReadPurchaseOrderAsync(new TenantId(companyId), Actor(context), orderId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DraftGoodsReceiptAsync(
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

        PurchasingGoodsReceiptRequest request;
        try
        {
            request = DocumentMapping.ToGoodsReceiptRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<PurchasingDocument> result = await purchasing
            .DraftGoodsReceiptAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.GoodsReceipt, companyId, "receiptId", result.Value.Id));
    }

    private static async Task<IResult> ReadGoodsReceiptAsync(
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
            .ReadGoodsReceiptAsync(new TenantId(companyId), Actor(context), receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> PostGoodsReceiptAsync(
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
            .PostGoodsReceiptAsync(new TenantId(companyId), Actor(context), receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Posted(context, DocumentMapping.ToDto(result.Value), Location(ApiRoutes.GoodsReceipt, companyId, "receiptId", receiptId));
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

    /// <summary>يقرأ سطور استلامٍ بمعرّفاتها — مدخل الفاتورة المخزنية والمرتجع.</summary>
    private static async Task<IResult> ReadGoodsReceiptLinesAsync(
        HttpContext context,
        PurchasingSurface purchasing,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<PurchasingDocumentLine>> result = await purchasing
            .ReadGoodsReceiptLinesAsync(new TenantId(companyId), Actor(context), receiptId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── سلسلة المشتريات المخزنية ─────────────────────────────────────────────

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

    // ── تسكين المخزون ────────────────────────────────────────────────────────
    // وكل معالج هنا يفعل الشيء نفسه بالترتيب نفسه: نطاقُ الشركة · معرّفات المسار ·
    // الجسم بالقواعد الصارمة · نداءُ السطح المنشور · ترجمةُ النتيجة. **ولا قرار
    // واحد يقع هنا**: الهرم والانتماء والرصيد المتبقّي كلّها أحكام الوحدة.

    private static async Task<IResult> AddWarehouseAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (StoragePlaceRequestDto? dto, IResult? refused) =
            await BodyAsync<StoragePlaceRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryStoragePlaceRequest request;
        try
        {
            request = DocumentMapping.ToStoragePlaceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryStoragePlace> result = await inventory
            .AddWarehouseAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                DocumentMapping.ToDto(result.Value),
                Location(ApiRoutes.Warehouse, companyId, "warehouseId", result.Value.Id));
    }

    private static async Task<IResult> ListWarehousesAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryStoragePlace>> result = await inventory
            .ListWarehousesAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        return PlaceList(context, result);
    }

    private static async Task<IResult> ReadWarehouseAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .ReadWarehouseAsync(new TenantId(companyId), Actor(context), warehouseId, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> RenameWarehouseAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        (InventoryPlaceNameRequest? request, IResult? refused) =
            await NameAsync(context, cancellationToken).ConfigureAwait(false);

        if (request is null)
        {
            return refused!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .RenameWarehouseAsync(new TenantId(companyId), Actor(context), warehouseId, request, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> DeactivateWarehouseAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .DeactivateWarehouseAsync(new TenantId(companyId), Actor(context), warehouseId, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> AddStorageLocationAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        (StoragePlaceRequestDto? dto, IResult? refused) =
            await BodyAsync<StoragePlaceRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryStoragePlaceRequest request;
        try
        {
            request = DocumentMapping.ToStoragePlaceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryStoragePlace> result = await inventory
            .AddStorageLocationAsync(new TenantId(companyId), Actor(context), warehouseId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                DocumentMapping.ToDto(result.Value),
                Location(ApiRoutes.StorageLocation, companyId, "warehouseId", warehouseId, "locationId", result.Value.Id));
    }

    private static async Task<IResult> ListStorageLocationsAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<IReadOnlyList<InventoryStoragePlace>> result = await inventory
            .ListStorageLocationsAsync(new TenantId(companyId), Actor(context), warehouseId, cancellationToken)
            .ConfigureAwait(false);

        return PlaceList(context, result);
    }

    private static async Task<IResult> ReadStorageLocationAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .ReadStorageLocationAsync(new TenantId(companyId), Actor(context), warehouseId, locationId, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> RenameStorageLocationAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        (InventoryPlaceNameRequest? request, IResult? refused) =
            await NameAsync(context, cancellationToken).ConfigureAwait(false);

        if (request is null)
        {
            return refused!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .RenameStorageLocationAsync(
                new TenantId(companyId), Actor(context), warehouseId, locationId, request, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> DeactivateStorageLocationAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .DeactivateStorageLocationAsync(
                new TenantId(companyId), Actor(context), warehouseId, locationId, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> AddStorageBinAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        (StoragePlaceRequestDto? dto, IResult? refused) =
            await BodyAsync<StoragePlaceRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryStoragePlaceRequest request;
        try
        {
            request = DocumentMapping.ToStoragePlaceRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryStoragePlace> result = await inventory
            .AddStorageBinAsync(new TenantId(companyId), Actor(context), warehouseId, locationId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                DocumentMapping.ToDto(result.Value),
                Location(
                    ApiRoutes.StorageBin, companyId, "warehouseId", warehouseId, "locationId", locationId, "binId", result.Value.Id));
    }

    private static async Task<IResult> ListStorageBinsAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        Result<IReadOnlyList<InventoryStoragePlace>> result = await inventory
            .ListStorageBinsAsync(new TenantId(companyId), Actor(context), warehouseId, locationId, cancellationToken)
            .ConfigureAwait(false);

        return PlaceList(context, result);
    }

    private static async Task<IResult> ReadStorageBinAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        if (!Scope.TryRouteId(context, "binId", out Guid binId, out IResult? badBin))
        {
            return badBin!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .ReadStorageBinAsync(new TenantId(companyId), Actor(context), warehouseId, locationId, binId, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> RenameStorageBinAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        if (!Scope.TryRouteId(context, "binId", out Guid binId, out IResult? badBin))
        {
            return badBin!;
        }

        (InventoryPlaceNameRequest? request, IResult? refused) =
            await NameAsync(context, cancellationToken).ConfigureAwait(false);

        if (request is null)
        {
            return refused!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .RenameStorageBinAsync(
                new TenantId(companyId), Actor(context), warehouseId, locationId, binId, request, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> DeactivateStorageBinAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "warehouseId", out Guid warehouseId, out IResult? malformed))
        {
            return malformed!;
        }

        if (!Scope.TryRouteId(context, "locationId", out Guid locationId, out IResult? badLocation))
        {
            return badLocation!;
        }

        if (!Scope.TryRouteId(context, "binId", out Guid binId, out IResult? badBin))
        {
            return badBin!;
        }

        Result<InventoryStoragePlace> result = await inventory
            .DeactivateStorageBinAsync(
                new TenantId(companyId), Actor(context), warehouseId, locationId, binId, cancellationToken)
            .ConfigureAwait(false);

        return Place(context, result);
    }

    private static async Task<IResult> DraftStockTransferAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (StockTransferRequestDto? dto, IResult? refused) =
            await BodyAsync<StockTransferRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryStockTransferRequest request;
        try
        {
            request = DocumentMapping.ToStockTransferRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryStockTransfer> result = await inventory
            .DraftTransferAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                DocumentMapping.ToDto(result.Value),
                Location(ApiRoutes.StockTransfer, companyId, "transferId", result.Value.Id));
    }

    private static async Task<IResult> ListStockTransfersAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryStockTransfer>> result = await inventory
            .ListTransfersAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<StockTransferDto> transfers = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new StockTransferListDto(transfers.Count, transfers), ApiJson.Options);
    }

    private static async Task<IResult> ReadStockTransferAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "transferId", out Guid transferId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryStockTransfer> result = await inventory
            .ReadTransferAsync(new TenantId(companyId), Actor(context), transferId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> MoveStockTransferAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "transferId", out Guid transferId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryStockTransfer> result = await inventory
            .MoveTransferAsync(new TenantId(companyId), Actor(context), transferId, cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        StockTransferDto dto = DocumentMapping.ToDto(result.Value);
        return Moved(context, dto, Location(ApiRoutes.StockTransfer, companyId, "transferId", result.Value.Id));
    }

    private static async Task<IResult> ReadPlacementBalancesAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryPlacementBalance>> result = await inventory
            .ReadPlacementBalancesAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<PlacementBalanceDto> balances = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new PlacementBalanceListDto(balances.Count, balances), ApiJson.Options);
    }

    /// <summary>يقرأ جسم إعادة التسمية — <b>الاسم وحده</b> — أو يُرجع رفضه.</summary>
    private static async Task<(InventoryPlaceNameRequest? Request, IResult? Refused)> NameAsync(
        HttpContext context, CancellationToken cancellationToken)
    {
        (PlaceNameRequestDto? dto, IResult? refused) =
            await BodyAsync<PlaceNameRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return (null, refused!);
        }

        try
        {
            return (DocumentMapping.ToPlaceNameRequest(dto), null);
        }
        catch (WireFormatException wire)
        {
            return (null, HttpProblemResults.Wire(context, wire));
        }
    }

    private static IResult Place(HttpContext context, Result<InventoryStoragePlace> result)
        => result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);

    private static IResult PlaceList(HttpContext context, Result<IReadOnlyList<InventoryStoragePlace>> result)
    {
        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<StoragePlaceDto> places = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new StoragePlaceListDto(places.Count, places), ApiJson.Options);
    }

    // ── دورة حياة الصنف ──────────────────────────────────────────────────────

    private static async Task<IResult> ReviseItemAsync(
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

        (ItemRevisionRequestDto? dto, IResult? refused) =
            await BodyAsync<ItemRevisionRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryItemRevisionRequest request;
        try
        {
            request = DocumentMapping.ToItemRevisionRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryItem> result = await inventory
            .ReviseItemAsync(new TenantId(companyId), Actor(context), itemId, request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DeactivateItemAsync(
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

        Result<InventoryItemLifecycle> result = await inventory
            .DeactivateItemAsync(new TenantId(companyId), Actor(context), itemId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> ReadItemLifecycleAsync(
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

        Result<InventoryItemLifecycle> result = await inventory
            .ReadItemLifecycleAsync(new TenantId(companyId), Actor(context), itemId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    // ── وحدات القياس ─────────────────────────────────────────────────────────

    private static async Task<IResult> AddUnitOfMeasureAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (UnitOfMeasureRequestDto? dto, IResult? refused) =
            await BodyAsync<UnitOfMeasureRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryUnitRequest request;
        try
        {
            request = DocumentMapping.ToUnitRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryUnit> result = await inventory
            .AddUnitAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(
                context,
                DocumentMapping.ToDto(result.Value),
                Location(ApiRoutes.UnitOfMeasure, companyId, "unitId", result.Value.Id));
    }

    private static async Task<IResult> ListUnitsOfMeasureAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryUnit>> result = await inventory
            .ListUnitsAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<UnitOfMeasureDto> units = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new UnitOfMeasureListDto(units.Count, units), ApiJson.Options);
    }

    private static async Task<IResult> ReadUnitOfMeasureAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "unitId", out Guid unitId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryUnit> result = await inventory
            .ReadUnitAsync(new TenantId(companyId), Actor(context), unitId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> DeactivateUnitOfMeasureAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        if (!Scope.TryRouteId(context, "unitId", out Guid unitId, out IResult? malformed))
        {
            return malformed!;
        }

        Result<InventoryUnit> result = await inventory
            .DeactivateUnitAsync(new TenantId(companyId), Actor(context), unitId, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
    }

    private static async Task<IResult> AddUnitConversionAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (UnitConversionRequestDto? dto, IResult? refused) =
            await BodyAsync<UnitConversionRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryUnitConversionRequest request;
        try
        {
            request = DocumentMapping.ToUnitConversionRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryUnitConversion> result = await inventory
            .AddUnitConversionAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Created(context, DocumentMapping.ToDto(result.Value), null);
    }

    private static async Task<IResult> ListUnitConversionsAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        Result<IReadOnlyList<InventoryUnitConversion>> result = await inventory
            .ListUnitConversionsAsync(new TenantId(companyId), Actor(context), cancellationToken)
            .ConfigureAwait(false);

        if (result.IsFailure)
        {
            return HttpProblemResults.Domain(context, result.Errors);
        }

        List<UnitConversionDto> conversions = [.. result.Value.Select(DocumentMapping.ToDto)];
        return Results.Json(new UnitConversionListDto(conversions.Count, conversions), ApiJson.Options);
    }

    private static async Task<IResult> ConvertQuantityAsync(
        HttpContext context,
        InventorySurface inventory,
        CancellationToken cancellationToken)
    {
        if (!Scope.TryCompany(context, out Guid companyId, out IResult? denied))
        {
            return denied!;
        }

        (ConversionTrialRequestDto? dto, IResult? refused) =
            await BodyAsync<ConversionTrialRequestDto>(context, cancellationToken).ConfigureAwait(false);

        if (dto is null)
        {
            return refused!;
        }

        InventoryConversionTrialRequest request;
        try
        {
            request = DocumentMapping.ToConversionTrialRequest(dto);
        }
        catch (WireFormatException wire)
        {
            return HttpProblemResults.Wire(context, wire);
        }

        Result<InventoryConversionResult> result = await inventory
            .ConvertQuantityAsync(new TenantId(companyId), Actor(context), request, cancellationToken)
            .ConfigureAwait(false);

        // ‏**والمسبار لا يكتب شيئاً، فجوابه 200 لا 201.** ورمزُ إنشاءٍ على مورد
        // ‏«محاولات» كان سيَعِد بموردٍ يُقرأ لاحقاً، ولا مورد.
        return result.IsFailure
            ? HttpProblemResults.Domain(context, result.Errors)
            : Results.Json(DocumentMapping.ToDto(result.Value), ApiJson.Options);
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

    /// <summary>عنوان مورد متداخل — أبٌ ومولود، وكلاهما في المسار.</summary>
    private static string Location(
        string template, Guid companyId, string parentName, Guid parentId, string idName, Guid id)
        => Location(template, companyId, parentName, parentId)
            .Replace("{" + idName + "}", id.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>عنوان مورد متداخل بمستويين — جدٌّ وأبٌ ومولود.</summary>
    private static string Location(
        string template,
        Guid companyId,
        string grandparentName,
        Guid grandparentId,
        string parentName,
        Guid parentId,
        string idName,
        Guid id)
        => Location(template, companyId, grandparentName, grandparentId, parentName, parentId)
            .Replace("{" + idName + "}", id.ToString("D", CultureInfo.InvariantCulture), StringComparison.Ordinal);

    /// <summary>
    /// ‏201 للتنفيذ الأول و200 للوصول الثاني بالهوية نفسها — <b>والفارق في الجسم أيضاً</b>
    /// بـ<c>alreadyMoved</c>. ورمز الحالة وحده يضيع خلف أي وسيط يعيد التوجيه.
    /// </summary>
    private static IResult Moved(HttpContext context, StockTransferDto dto, string? location)
    {
        if (location is not null)
        {
            context.Response.Headers.Location = location;
        }

        return Results.Json(
            dto,
            ApiJson.Options,
            statusCode: dto.AlreadyMoved ? StatusCodes.Status200OK : StatusCodes.Status201Created);
    }
}
