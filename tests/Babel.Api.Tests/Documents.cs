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
