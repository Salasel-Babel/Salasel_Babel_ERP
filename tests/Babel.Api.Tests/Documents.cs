using System.Globalization;
using System.Net;
using System.Text.Json;
using Xunit;

namespace Babel.Api.Tests;

/// <summary>
/// مسارات سطح المستندات وحمولاته — <b>نصّاً خامّاً</b>، للسبب نفسه الذي في
/// <see cref="Payloads"/>: نصف ما يُفحص هنا لا يستطيع مُسلسِل سليم أن يُنتجه.
/// </summary>
internal static class Documents
{
    private static int _counter;

    /// <summary>رقم مستند فريد داخل هذه العملية — لا يتصادم مع تشغيل متوازٍ.</summary>
    /// <param name="prefix">بادئة تُقرأ في السجلّ.</param>
    public static string Number(string prefix) => string.Create(
        CultureInfo.InvariantCulture,
        $"{prefix}-{Environment.ProcessId}-{Interlocked.Increment(ref _counter)}");

    /// <summary>مسار العملاء.</summary>
    /// <param name="company">الشركة.</param>
    public static string Customers(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/customers");

    /// <summary>مسار عميل واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="customerId">العميل.</param>
    public static string Customer(Guid company, string customerId) =>
        Customers(company) + "/" + customerId;

    /// <summary>مسار فواتير المبيعات.</summary>
    /// <param name="company">الشركة.</param>
    public static string SalesInvoices(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/sales-invoices");

    /// <summary>مسار فاتورة مبيعات.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    public static string SalesInvoice(Guid company, string invoiceId) => SalesInvoices(company) + "/" + invoiceId;

    /// <summary>مسار ترحيل فاتورة مبيعات.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    public static string SalesInvoicePosting(Guid company, string invoiceId) =>
        SalesInvoice(company, invoiceId) + "/posting";

    /// <summary>مسار الإشعارات الدائنة.</summary>
    /// <param name="company">الشركة.</param>
    public static string CreditNotes(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/credit-notes");

    /// <summary>مسار ترحيل إشعار دائن.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="creditNoteId">الإشعار.</param>
    public static string CreditNotePosting(Guid company, string creditNoteId) =>
        CreditNotes(company) + "/" + creditNoteId + "/posting";

    /// <summary>مسار سندات القبض.</summary>
    /// <param name="company">الشركة.</param>
    public static string CustomerReceipts(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/customer-receipts");

    /// <summary>مسار سند قبض واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="receiptId">السند.</param>
    public static string CustomerReceipt(Guid company, string receiptId) =>
        CustomerReceipts(company) + "/" + receiptId;

    /// <summary>مسار ترحيل سند قبض.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="receiptId">السند.</param>
    public static string CustomerReceiptPosting(Guid company, string receiptId) =>
        CustomerReceipt(company, receiptId) + "/posting";

    /// <summary>مسار سندات الصرف.</summary>
    /// <param name="company">الشركة.</param>
    public static string SupplierPayments(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/supplier-payments");

    /// <summary>مسار سند صرف واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="paymentId">السند.</param>
    public static string SupplierPayment(Guid company, string paymentId) =>
        SupplierPayments(company) + "/" + paymentId;

    /// <summary>مسار ترحيل سند صرف.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="paymentId">السند.</param>
    public static string SupplierPaymentPosting(Guid company, string paymentId) =>
        SupplierPayment(company, paymentId) + "/posting";

    /// <summary>مسار أوامر الشراء.</summary>
    /// <param name="company">الشركة.</param>
    public static string PurchaseOrders(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/purchase-orders");

    /// <summary>مسار أمر شراء واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="orderId">الأمر.</param>
    public static string PurchaseOrder(Guid company, string orderId) => PurchaseOrders(company) + "/" + orderId;

    /// <summary>مسار استلامات البضاعة.</summary>
    /// <param name="company">الشركة.</param>
    public static string GoodsReceipts(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/goods-receipts");

    /// <summary>مسار استلام واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="receiptId">الاستلام.</param>
    public static string GoodsReceipt(Guid company, string receiptId) => GoodsReceipts(company) + "/" + receiptId;

    /// <summary>مسار ترحيل استلام.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="receiptId">الاستلام.</param>
    public static string GoodsReceiptPosting(Guid company, string receiptId) =>
        GoodsReceipt(company, receiptId) + "/posting";

    /// <summary>مسار ملفّ القدرات.</summary>
    /// <param name="company">الشركة.</param>
    public static string CapabilityProfile(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/capability-profile");

    /// <summary>مسار أعمار الذمم المدينة.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="asOf">التاريخ.</param>
    public static string ReceivablesAging(Guid company, string asOf) => string.Create(
        CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/receivables-aging?asOf={Uri.EscapeDataString(asOf)}");

    /// <summary>مسار الموردين.</summary>
    /// <param name="company">الشركة.</param>
    public static string Suppliers(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/suppliers");

    /// <summary>مسار مورد واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="supplierId">المورد.</param>
    public static string Supplier(Guid company, string supplierId) => Suppliers(company) + "/" + supplierId;

    /// <summary>مسار فواتير الموردين.</summary>
    /// <param name="company">الشركة.</param>
    public static string SupplierBills(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/supplier-bills");

    /// <summary>مسار فاتورة مورد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="billId">الفاتورة.</param>
    public static string SupplierBill(Guid company, string billId) => SupplierBills(company) + "/" + billId;

    /// <summary>مسار ترحيل فاتورة مورد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="billId">الفاتورة.</param>
    public static string SupplierBillPosting(Guid company, string billId) => SupplierBill(company, billId) + "/posting";

    /// <summary>مسار أعمار الذمم الدائنة.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="asOf">التاريخ.</param>
    public static string PayablesAging(Guid company, string asOf) => string.Create(
        CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/payables-aging?asOf={Uri.EscapeDataString(asOf)}");

    /// <summary>حمولة عميل.</summary>
    /// <param name="code">الرمز.</param>
    public static string Customer(string code) => $$"""
        {"code":"{{code}}","name":{"ar":"عميل اختبار السطح","en":"Surface test customer"},
         "creditLimit":"5000000.0000","paymentTermsDays":30}
        """;

    /// <summary>حمولة مورد.</summary>
    /// <param name="code">الرمز.</param>
    public static string Supplier(string code) => $$"""
        {"code":"{{code}}","name":{"ar":"مورد اختبار السطح","en":"Surface test supplier"},
         "creditLimit":"5000000.0000","paymentTermsDays":45,"vatNumber":"300000000000003"}
        """;

    /// <summary>
    /// حمولة فاتورة مبيعات: عشر وحدات بمئة، وضريبة 15٪ — صافٍ 1000 وضريبة 150.
    /// <para>و<c>itemGroup</c> هو <c>*</c>: مؤهّل الدور الشامل الذي تحلّه المصفوفة.</para>
    /// </summary>
    /// <param name="number">رقم الفاتورة.</param>
    /// <param name="customerId">العميل.</param>
    /// <param name="issuedOn">تاريخ الإصدار.</param>
    public static string Invoice(string number, string customerId, string issuedOn = "2026-03-10") => $$"""
        {"number":"{{number}}","customerId":"{{customerId}}","issuedOn":"{{issuedOn}}","branchId":"BR-01",
         "lines":[{"itemGroup":"*","description":{"ar":"صنف","en":"Item"},"quantity":"10","unitPrice":"100.0000",
                   "discount":"0","taxClassification":"standard","taxRate":"0.15","originalInvoiceLineId":null}]}
        """;

    /// <summary>حمولة إشعار دائن: وحدة واحدة بمئة — صافٍ 100 وضريبة 15.</summary>
    /// <param name="number">رقم الإشعار.</param>
    /// <param name="invoiceId">الفاتورة الأصلية.</param>
    /// <param name="issuedOn">تاريخ الإصدار.</param>
    public static string CreditNote(string number, string invoiceId, string issuedOn = "2026-03-20") => $$"""
        {"number":"{{number}}","invoiceId":"{{invoiceId}}","issuedOn":"{{issuedOn}}",
         "lines":[{"itemGroup":"*","description":{"ar":"مرتجع","en":"Return"},"quantity":"1","unitPrice":"100.0000",
                   "discount":"0","taxClassification":"standard","taxRate":"0.15","originalInvoiceLineId":null}]}
        """;

    /// <summary>حمولة فاتورة مصروف: خمس وحدات بمئة — صافٍ 500.</summary>
    /// <param name="number">الرقم.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="costCenterId">مركز التكلفة.</param>
    /// <param name="issuedOn">التاريخ.</param>
    public static string ExpenseBill(string number, string supplierId, string costCenterId, string issuedOn = "2026-03-12") => $$"""
        {"number":"{{number}}","supplierId":"{{supplierId}}","issuedOn":"{{issuedOn}}",
         "expenseCategory":"office","costCenterId":"{{costCenterId}}",
         "lines":[{"itemId":"SRV-1","itemGroup":"*","description":{"ar":"خدمة","en":"Service"},"quantity":"5",
                   "unitPrice":"100.0000","taxClassification":"standard","taxRate":"0.15","taxRecoverable":true}]}
        """;

    /// <summary>
    /// حمولة سند قبض بتخصيص واحد على فاتورة.
    /// </summary>
    /// <param name="number">رقم السند.</param>
    /// <param name="customerId">العميل.</param>
    /// <param name="invoiceId">الفاتورة المُرحَّلة.</param>
    /// <param name="amount">المبلغ المقبوض والمخصَّص نصّاً.</param>
    /// <param name="receivedOn">تاريخ القبض.</param>
    public static string Receipt(
        string number, string customerId, string invoiceId, string amount, string receivedOn = "2026-03-15") => $$"""
        {"number":"{{number}}","customerId":"{{customerId}}","receivedOn":"{{receivedOn}}",
         "settlementMethod":"bank","treasuryPartyId":"BANK-01",
         "received":"{{amount}}","settlementDiscount":"0",
         "allocations":[{"invoiceId":"{{invoiceId}}","amount":"{{amount}}"}]}
        """;

    /// <summary>حمولة سند صرف بتخصيص واحد على فاتورة مورد.</summary>
    /// <param name="number">رقم السند.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="billId">الفاتورة المُرحَّلة.</param>
    /// <param name="amount">المبلغ المدفوع والمخصَّص نصّاً.</param>
    /// <param name="paidOn">تاريخ الصرف.</param>
    public static string Payment(
        string number, string supplierId, string billId, string amount, string paidOn = "2026-03-16") => $$"""
        {"number":"{{number}}","supplierId":"{{supplierId}}","paidOn":"{{paidOn}}",
         "settlementMethod":"bank","treasuryPartyId":"BANK-01",
         "paid":"{{amount}}","bankFee":"0",
         "allocations":[{"billId":"{{billId}}","amount":"{{amount}}"}]}
        """;

    /// <summary>حمولة أمر شراء: عشر وحدات بمئة على صنف واحد.</summary>
    /// <param name="number">رقم الأمر.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="costCenterId">مركز التكلفة.</param>
    /// <param name="orderedOn">تاريخ الأمر.</param>
    public static string PurchaseOrder(
        string number, string supplierId, string costCenterId, string orderedOn = "2026-03-05") => $$"""
        {"number":"{{number}}","supplierId":"{{supplierId}}","orderedOn":"{{orderedOn}}",
         "warehouseId":"WH-01","costCenterId":"{{costCenterId}}",
         "lines":[{"itemId":"ITEM-A","itemGroup":"*","description":{"ar":"صنف","en":"Item"},"quantity":"10",
                   "unitPrice":"100.0000","taxClassification":"standard","taxRate":"0.15","taxRecoverable":true}]}
        """;

    /// <summary>حمولة استلام بضاعة على سطر أمر.</summary>
    /// <param name="number">رقم الاستلام.</param>
    /// <param name="orderId">الأمر.</param>
    /// <param name="orderLineId">سطر الأمر.</param>
    /// <param name="quantity">الكمية المستلمة نصّاً.</param>
    /// <param name="receivedOn">تاريخ الاستلام.</param>
    public static string GoodsReceipt(
        string number, string orderId, string orderLineId, string quantity, string receivedOn = "2026-03-08") => $$"""
        {"number":"{{number}}","orderId":"{{orderId}}","receivedOn":"{{receivedOn}}",
         "lines":[{"orderLineId":"{{orderLineId}}","quantity":"{{quantity}}"}]}
        """;


    // ── المخزون وسلسلة المشتريات المخزنية ────────────────────────────────────

    /// <summary>مسار الأصناف.</summary>
    /// <param name="company">الشركة.</param>
    public static string Items(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/items");

    /// <summary>مسار صنف واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="itemId">الصنف.</param>
    public static string Item(Guid company, string itemId) => Items(company) + "/" + itemId;

    /// <summary>مسار حركات المخزون.</summary>
    /// <param name="company">الشركة.</param>
    public static string StockMovements(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/stock-movements");

    /// <summary>مسار ترحيل حركة مخزون.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="movementId">الحركة.</param>
    public static string StockMovementPosting(Guid company, string movementId) =>
        StockMovements(company) + "/" + movementId + "/posting";

    /// <summary>مسار المستودعات.</summary>
    /// <param name="company">الشركة.</param>
    public static string Warehouses(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/warehouses");

    /// <summary>مسار مستودع واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="warehouseId">المستودع.</param>
    public static string Warehouse(Guid company, string warehouseId) =>
        Warehouses(company) + "/" + warehouseId;

    /// <summary>مسار تعطيل مستودع.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="warehouseId">المستودع.</param>
    public static string WarehouseDeactivation(Guid company, string warehouseId) =>
        Warehouse(company, warehouseId) + "/deactivation";

    /// <summary>مسار إعادة تفعيل مستودع.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="warehouseId">المستودع.</param>
    public static string WarehouseActivation(Guid company, string warehouseId) =>
        Warehouse(company, warehouseId) + "/activation";

    /// <summary>مسار مواقع مستودع — <b>مورد فرعي</b>: رمز الموقع بلا مستودعه ليس هوية.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="warehouseId">المستودع.</param>
    public static string WarehouseLocations(Guid company, string warehouseId) =>
        Warehouse(company, warehouseId) + "/locations";

    /// <summary>مسار تعطيل موقع.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="warehouseId">المستودع.</param>
    /// <param name="locationId">الموقع.</param>
    public static string WarehouseLocationDeactivation(Guid company, string warehouseId, string locationId) =>
        WarehouseLocations(company, warehouseId) + "/" + locationId + "/deactivation";

    /// <summary>مسار أرصدة المخزون.</summary>
    /// <param name="company">الشركة.</param>
    public static string StockBalances(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/stock-balances");

    /// <summary>مسار تقييم المخزون في تاريخ.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="asOf">التاريخ.</param>
    public static string InventoryValuation(Guid company, string asOf) => string.Create(
        CultureInfo.InvariantCulture,
        $"/api/v1/companies/{company:D}/inventory-valuation?asOf={Uri.EscapeDataString(asOf)}");

    /// <summary>مسار سطور استلامٍ واحد — مدخل الفاتورة المخزنية والمرتجع.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="receiptId">الاستلام.</param>
    public static string GoodsReceiptLines(Guid company, string receiptId) =>
        GoodsReceipt(company, receiptId) + "/lines";

    /// <summary>مسار الفواتير المخزنية.</summary>
    /// <param name="company">الشركة.</param>
    public static string StockBills(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/stock-bills");

    /// <summary>مسار مرتجعات المشتريات.</summary>
    /// <param name="company">الشركة.</param>
    public static string PurchaseReturns(Guid company) =>
        string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/purchase-returns");

    /// <summary>مسار مرتجع مشتريات واحد.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="returnId">المرتجع.</param>
    public static string PurchaseReturn(Guid company, string returnId) => PurchaseReturns(company) + "/" + returnId;

    /// <summary>مسار ترحيل مرتجع مشتريات.</summary>
    /// <param name="company">الشركة.</param>
    /// <param name="returnId">المرتجع.</param>
    public static string PurchaseReturnPosting(Guid company, string returnId) =>
        PurchaseReturn(company, returnId) + "/posting";

    /// <summary>
    /// حمولة صنف: وحدة أساسه الحبّة، والكرتون اثنتا عشرة حبّة — <b>معاملٌ نسبيّ لا عائم</b>.
    /// </summary>
    /// <param name="code">رمز الصنف.</param>
    public static string Item(string code) => $$"""
        {"code":"{{code}}","name":{"ar":"صنف اختبار المخزون","en":"Inventory surface test item"},
         "itemGroup":"*","baseUnit":"EACH",
         "units":[{"unitCode":"CTN","numerator":12,"denominator":1}]}
        """;

    /// <summary>حمولة حركة مخزون واردة: كرتونان بـ240 ⇒ 24 حبّة بمتوسط 10.</summary>
    /// <param name="number">رقم المستند.</param>
    /// <param name="itemCode">الصنف.</param>
    /// <param name="occurredOn">التاريخ.</param>
    public static string StockMovementIn(string number, string itemCode, string occurredOn = "2026-03-05") => $$"""
        {"number":"{{number}}","direction":"IN","itemId":"{{itemCode}}","warehouseId":"WH-01",
         "locationId":"DEFAULT","itemGroup":"*","quantity":{"magnitude":"2","unit":"CTN"},
         "cost":"240.0000","occurredOn":"{{occurredOn}}"}
        """;

    /// <summary>حمولة أمر شراء: أربع وحدات بمئة، وضريبة 15٪.</summary>
    /// <param name="number">الرقم.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="itemCode">الصنف.</param>
    /// <param name="costCenterId">مركز التكلفة.</param>
    /// <param name="orderedOn">التاريخ.</param>
    public static string StockPurchaseOrder(
        string number, string supplierId, string itemCode, string costCenterId, string orderedOn = "2026-03-06") => $$"""
        {"number":"{{number}}","supplierId":"{{supplierId}}","orderedOn":"{{orderedOn}}",
         "warehouseId":"WH-01","costCenterId":"{{costCenterId}}",
         "lines":[{"itemId":"{{itemCode}}","itemGroup":"*","description":{"ar":"بضاعة","en":"Goods"},
                   "quantity":"4","unitPrice":"100.0000",
                   "taxClassification":"standard","taxRate":"0.15","taxRecoverable":true}]}
        """;

    /// <summary>حمولة استلام: أربع وحدات من سطر الأمر.</summary>
    /// <param name="number">الرقم.</param>
    /// <param name="orderId">الأمر.</param>
    /// <param name="orderLineId">سطر الأمر.</param>
    /// <param name="receivedOn">التاريخ.</param>
    public static string StockGoodsReceipt(
        string number, string orderId, string orderLineId, string receivedOn = "2026-03-07") => $$"""
        {"number":"{{number}}","orderId":"{{orderId}}","receivedOn":"{{receivedOn}}",
         "lines":[{"orderLineId":"{{orderLineId}}","quantity":"4"}]}
        """;

    /// <summary>حمولة فاتورة مخزنية على سطر استلام.</summary>
    /// <param name="number">الرقم.</param>
    /// <param name="receiptId">الاستلام.</param>
    /// <param name="receiptLineId">سطر الاستلام.</param>
    /// <param name="issuedOn">التاريخ.</param>
    public static string StockBill(
        string number, string receiptId, string receiptLineId, string issuedOn = "2026-03-08") => $$"""
        {"number":"{{number}}","receiptId":"{{receiptId}}","issuedOn":"{{issuedOn}}",
         "lines":[{"receiptLineId":"{{receiptLineId}}","quantity":"4","unitPrice":"100.0000",
                   "taxClassification":"standard","taxRate":"0.15"}]}
        """;

    /// <summary>
    /// حمولة مرتجع مشتريات: وحدة واحدة — <b>بلا صافٍ</b>، فالمبلغ يُحسب لحظة الترحيل.
    /// </summary>
    /// <param name="number">الرقم.</param>
    /// <param name="billId">الفاتورة المخزنية.</param>
    /// <param name="receiptLineId">سطر الاستلام.</param>
    /// <param name="issuedOn">التاريخ.</param>
    public static string PurchaseReturn(
        string number, string billId, string receiptLineId, string issuedOn = "2026-03-09") => $$"""
        {"number":"{{number}}","billId":"{{billId}}","receiptLineId":"{{receiptLineId}}",
         "issuedOn":"{{issuedOn}}","quantity":"1","tax":"15.0000"}
        """;

    /// <summary>يسجّل صنفاً ويُعيد رمزه ومعرّفه.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    public static async Task<(string Code, string Id)> AddItemAsync(
        ApiProcess api, Guid company, TestCredential credential)
    {
        string code = Number("ITEM");

        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Post, Items(company), credential, Item(code)));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, "تسجيل الصنف: " + text);
        return (code, body.GetProperty("id").GetString()!);
    }

    /// <summary>حمولة تسجيل مستودع — والمؤهّل مؤهّل دور لا رقم حساب.</summary>
    /// <param name="code">الرمز.</param>
    /// <param name="qualifier">مؤهّل الدور.</param>
    public static string WarehouseBody(string code, string qualifier = "") => $$"""
        {"code":"{{code}}","nameAr":"مستودع {{code}}",
         "nameTranslations":[{"name":"en","value":"Warehouse {{code}}"}],
         "qualifier":"{{qualifier}}"}
        """;

    /// <summary>حمولة تسجيل موقع — ولا حقل مستودع فيها: المستودع في المسار.</summary>
    /// <param name="code">الرمز.</param>
    public static string LocationBody(string code) => $$"""
        {"code":"{{code}}","nameAr":"موقع {{code}}",
         "nameTranslations":[{"name":"en","value":"Location {{code}}"}]}
        """;

    /// <summary>
    /// يضمن وجود مستودع <c>WH-01</c> وموقعه <c>DEFAULT</c> في هذه الشركة، ويُعيد معرّف
    /// المستودع.
    /// <para>
    /// <b>ولماذا صار هذا لازماً:</b> مسوّدة حركة المخزون لم تعد تُقبل على مكانٍ لا يعرفه
    /// كتالوج الوحدة — فرمزٌ يُكتب بخطأ إملائي كان يفتح رصيداً خامساً يُطابَق تماماً.
    /// </para>
    /// <para>
    /// <b>وهو مُحصَّن ضدّ التكرار</b>: 409 على الرمز المكرّر يُقرأ «موجود سلفاً»، فمجموعتان
    /// تتقاسمان الشركة نفسها لا تُفسد إحداهما الأخرى.
    /// </para>
    /// </summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    /// <param name="code">رمز المستودع.</param>
    /// <param name="location">رمز الموقع.</param>
    public static async Task<string> EnsurePlaceAsync(
        ApiProcess api,
        Guid company,
        TestCredential credential,
        string code = "WH-01",
        string location = "DEFAULT")
    {
        using HttpResponseMessage created = await api.Call(
            Http.Request(HttpMethod.Post, Warehouses(company), credential, WarehouseBody(code)));

        (string createdText, _) = await Http.BodyAsync(created);

        Assert.True(
            created.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            "تسجيل المستودع: " + createdText);

        using HttpResponseMessage listed = await api.Call(
            Http.Request(HttpMethod.Get, Warehouses(company), credential));

        (string listText, JsonElement list) = await Http.BodyAsync(listed);
        Assert.True(listed.StatusCode == HttpStatusCode.OK, "قراءة المستودعات: " + listText);

        string warehouseId = list.GetProperty("warehouses").EnumerateArray()
            .Single(entry => string.Equals(entry.GetProperty("code").GetString(), code, StringComparison.Ordinal))
            .GetProperty("id").GetString()!;

        using HttpResponseMessage placed = await api.Call(Http.Request(
            HttpMethod.Post, WarehouseLocations(company, warehouseId), credential, LocationBody(location)));

        (string placedText, _) = await Http.BodyAsync(placed);

        Assert.True(
            placed.StatusCode is HttpStatusCode.Created or HttpStatusCode.Conflict,
            "تسجيل الموقع: " + placedText);

        return warehouseId;
    }

    /// <summary>يقرأ رصيد صنف من قائمة الأرصدة، أو يرمي إن لم يوجد.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    /// <param name="itemCode">الصنف.</param>
    public static async Task<JsonElement> BalanceOfAsync(
        ApiProcess api, Guid company, TestCredential credential, string itemCode)
    {
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Get, StockBalances(company), credential));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "قراءة الأرصدة: " + text);

        foreach (JsonElement balance in body.GetProperty("balances").EnumerateArray())
        {
            if (string.Equals(balance.GetProperty("itemId").GetString(), itemCode, StringComparison.Ordinal))
            {
                return balance.Clone();
            }
        }

        throw new InvalidOperationException("لا رصيد للصنف " + itemCode + " في: " + text);
    }

    /// <summary>يسجّل عميلاً ويُعيد معرّفه.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    public static async Task<string> AddCustomerAsync(ApiProcess api, Guid company, TestCredential credential)
    {
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Post, Customers(company), credential, Customer(Number("CUST"))));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, "تسجيل العميل: " + text);
        return body.GetProperty("id").GetString()!;
    }

    /// <summary>يسجّل مورداً ويُعيد معرّفه.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    public static async Task<string> AddSupplierAsync(ApiProcess api, Guid company, TestCredential credential)
    {
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Post, Suppliers(company), credential, Supplier(Number("SUPP"))));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, "تسجيل المورد: " + text);
        return body.GetProperty("id").GetString()!;
    }

    /// <summary>يُنشئ فاتورة مسوّدة ويُعيد معرّفها.</summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    /// <param name="customerId">العميل.</param>
    public static async Task<string> DraftInvoiceAsync(
        ApiProcess api, Guid company, TestCredential credential, string customerId)
    {
        using HttpResponseMessage response = await api.Call(
            Http.Request(HttpMethod.Post, SalesInvoices(company), credential, Invoice(Number("INV"), customerId)));

        (string text, JsonElement body) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.Created, "إنشاء المسوّدة: " + text);
        return body.GetProperty("id").GetString()!;
    }

    /// <summary>
    /// رمز مركز التكلفة الافتراضي للمنشأة — <b>مقروءاً من التأسيس لا مكتوباً بيد</b>:
    /// الخادم هو من يسكّه، واختبارٌ يكتبه بيده يفحص تخمينه لا سلوك الخادم.
    /// </summary>
    /// <param name="api">الخادم.</param>
    /// <param name="company">الشركة.</param>
    /// <param name="credential">الاعتماد.</param>
    public static async Task<string> DefaultCostCenterAsync(ApiProcess api, Guid company, TestCredential credential)
    {
        using HttpResponseMessage response = await api.Call(Http.Request(
            HttpMethod.Get,
            string.Create(CultureInfo.InvariantCulture, $"/api/v1/companies/{company:D}/setup"),
            credential));

        (string text, JsonElement setup) = await Http.BodyAsync(response);
        Assert.True(response.StatusCode == HttpStatusCode.OK, "قراءة التأسيس: " + text);

        foreach (JsonElement centre in setup.GetProperty("costCenters").EnumerateArray())
        {
            if (centre.GetProperty("isDefault").GetBoolean())
            {
                return centre.GetProperty("code").GetString()!;
            }
        }

        throw new InvalidOperationException("لا مركز تكلفة افتراضي في تأسيس المنشأة: " + text);
    }
}
