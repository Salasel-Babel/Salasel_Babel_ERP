using Babel.Tests.Shared;
using System.Collections.Immutable;
using Babel.Contracts.Posting;
using Babel.Core.CapabilityProfile;
using Babel.Inventory.Application;
using Babel.Ledger;
using Babel.Ledger.Posting;
using Babel.Purchasing;
using Babel.Purchasing.Application;
using Babel.Sales;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// التجميعة كلها في مجموعة واحدة: الاختبارات تتشارك أربع قواعد بيانات حقيقية وعدّاد
/// ترقيم حقيقياً ودفتراً مساعداً واحداً، وتوازيها يجعل «انحراف في المطابقة» تعني
/// «اختباران تسابقا» لا «الدفتر المساعد ينحرف».
/// </summary>
[CollectionDefinition("inventory", DisableParallelization = true)]
public sealed class InventoryTestGroup;

/// <summary>
/// تركيب الاختبار بلا حاوية اعتماديات — وهو <b>الجذر التركيبي مكتوباً بيده</b>:
/// وحدة المخزون تنفّذ منفذ التقييم، ووحدة المبيعات تستهلكه، ولا تعرف إحداهما الأخرى.
/// <para>
/// و<b>لا بديل عن أي منهما هنا</b>: التقييم الحقيقي، والترحيل الحقيقي، ودفتر أستاذ
/// حقيقي بمخطّطه وبياناته المرجعية. الرقم الذي يُثبته هذا الملف هو رقم ينتجه المنتج.
/// </para>
/// </summary>
internal sealed class Harness : IDisposable
{
    private static LedgerRuntime? _ledger;
    private static readonly Lock LedgerGate = new();

    private Harness(
        InventoryRuntime inventory,
        PurchasingRuntime purchasing,
        SalesRuntime sales,
        LedgerRuntime ledger)
    {
        InventoryRuntime = inventory;
        PurchasingRuntime = purchasing;
        SalesRuntime = sales;
        LedgerRuntime = ledger;

        AlwaysEntitled enforcer = new();
        Posting = new PostingService(enforcer, ledger);
        Profiles = new InMemoryCapabilityProfileStore();

        Stock = new StockMovementService(enforcer, inventory);
        Items = new ItemCatalogueService(enforcer, inventory);
        StockDocuments = new StockDocumentService(enforcer, inventory, Posting, Stock);
        Valuation = new InventoryValuationService(
            enforcer,
            inventory,
            new LedgerControlPointReader(InventoryTestEnvironment.Ledger.AppConnectionString));

        Suppliers = new SupplierService(enforcer, purchasing);
        Orders = new PurchaseOrderService(enforcer, purchasing);
        Receipts = new GoodsReceiptService(enforcer, purchasing, Posting, Profiles, Stock);

        Customers = new CustomerService(enforcer, sales);

        // ‏**هنا يقع الربط كلّه**: خدمة الفواتير تأخذ منفذ التقييم، وتنفيذه هو خدمة
        // المخزون نفسها. لا بديل، ولا رقم يُملى من مستدعٍ.
        Invoices = new SalesInvoiceService(enforcer, sales, Posting, Profiles, Stock);
        CreditNotes = new CreditNoteService(enforcer, sales, Posting, Profiles, Stock);
    }

    public InventoryRuntime InventoryRuntime { get; }

    public PurchasingRuntime PurchasingRuntime { get; }

    public SalesRuntime SalesRuntime { get; }

    public LedgerRuntime LedgerRuntime { get; }

    public IPostingService Posting { get; }

    public InMemoryCapabilityProfileStore Profiles { get; }

    /// <summary>خدمة المخزون — وهي تنفيذ منفذ التقييم في العقود.</summary>
    public StockMovementService Stock { get; }

    /// <summary>كتالوج الأصناف — وحدة الأساس ومعاملات التحويل.</summary>
    public ItemCatalogueService Items { get; }

    /// <summary>مستندات حركة المخزون القائمة بذاتها: تسوية الجرد والرصيد الافتتاحي.</summary>
    public StockDocumentService StockDocuments { get; }

    public InventoryValuationService Valuation { get; }

    public SupplierService Suppliers { get; }

    public PurchaseOrderService Orders { get; }

    public GoodsReceiptService Receipts { get; }

    public CustomerService Customers { get; }

    public SalesInvoiceService Invoices { get; }

    public CreditNoteService CreditNotes { get; }

    public static UserId Actor { get; } = new(new Guid("00000000-0000-4000-8000-0000000000c1"));

    public static async Task<Harness> CreateAsync(CancellationToken cancellationToken = default)
    {
        await InventoryTestEnvironment.EnsureAsync(cancellationToken).ConfigureAwait(false);

        lock (LedgerGate)
        {
            _ledger ??= new LedgerRuntime(InventoryTestEnvironment.Ledger);
        }

        // المنشآت مؤسَّسة قبل أول ترحيل: البوّابة تسأل النواة عن مركز التكلفة، ومنشأةٌ
        // لم تُؤسَّس لا مركز لها أصلاً (ADR-0026).
        Harness harness = new(
            new InventoryRuntime(
                InventoryTestEnvironment.Inventory,
                FoundedTenants.ResolverFor(InventoryTestEnvironment.AllTenants)),
            new PurchasingRuntime(
                InventoryTestEnvironment.Purchasing,
                FoundedTenants.ResolverFor(InventoryTestEnvironment.AllTenants)),
            new SalesRuntime(
                InventoryTestEnvironment.Sales,
                FoundedTenants.ResolverFor(InventoryTestEnvironment.AllTenants)),
            _ledger!);

        foreach (TenantId tenant in InventoryTestEnvironment.AllTenants)
        {
            await harness.SaveProfilesAsync(tenant, cancellationToken).ConfigureAwait(false);
        }

        return harness;
    }

    /// <summary>
    /// يكتب ملفّ قدرات المستأجر لنوعَي المستند اللذين يمسّان المخزون.
    /// <para>
    /// ويمرّ بـ<see cref="ValidatedCapabilityProfile.Create"/> نفسها التي يمرّ بها
    /// الإنتاج: ملفٌّ لم يُطابَق بالمصفوفة لا يدخل المخزن أصلاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async Task SaveProfilesAsync(TenantId tenant, CancellationToken cancellationToken = default)
    {
        CapabilityProfileDraft draft = new(
            new Dictionary<string, DocumentProfileDraft>(StringComparer.Ordinal)
            {
                ["sales.invoice"] = new DocumentProfileDraft(
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["advance"] = true,
                        ["cost_of_sales"] = true,
                    },
                    ImmutableSortedDictionary<string, string>.Empty),
                ["purchasing.supplier_bill"] = new DocumentProfileDraft(
                    new Dictionary<string, bool>(StringComparer.Ordinal)
                    {
                        ["three_way_match"] = true,
                        ["landed_cost"] = true,
                    },
                    ImmutableSortedDictionary<string, string>.Empty),
            });

        Result<ValidatedCapabilityProfile> profile =
            ValidatedCapabilityProfile.Create(draft, EmbeddedPostingEventDirectory.Default);

        if (profile.IsFailure)
        {
            throw new InvalidOperationException(
                "تعذّر بناء ملفّ قدرات صالح: " + string.Join(" | ", profile.Errors.Select(static e => e.ToString())));
        }

        await Profiles.SaveAsync(tenant, profile.Value, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يرحّل استلام بضاعة حقيقياً: مورد، وأمر شراء، واستلام، وترحيله.
    /// <para>
    /// والقيد الناتج يُدين <c>inventory_control</c> على الدفتر المساعد <c>item</c> —
    /// وهو الطرف الذي تُطابَق به وحدة المخزون. ولا سطر واحد من هذا مكتوب هنا: الأرقام
    /// تأتي من ترحيل حقيقي عبر مصفوفة حقيقية.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="itemId">الصنف.</param>
    /// <param name="warehouseId">المستودع.</param>
    /// <param name="quantity">الكمية.</param>
    /// <param name="unitPrice">سعر الوحدة.</param>
    /// <param name="receivedOn">تاريخ الاستلام.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public async Task<ReceiptFacts> PostGoodsReceiptAsync(
        TenantId tenant,
        string itemId,
        string warehouseId,
        decimal quantity,
        decimal unitPrice,
        DateOnly receivedOn,
        CancellationToken token)
    {
        string code = Next("SUP");

        Result<SupplierView> supplier = await Suppliers.CreateAsync(
            tenant,
            Actor,
            new SupplierDraft(code, new LocalizedName("مورد " + code, "Supplier " + code), Money.Of(0m, CurrencyCode.Sar), 30),
            token);

        Require(supplier);

        Result<PurchasingDocumentView> order = await Orders.CreateOrderAsync(
            tenant,
            Actor,
            new PurchaseOrderDraft(
                Next("PO"),
                supplier.Value.Id,
                receivedOn,
                warehouseId,
                FoundedTenants.DefaultCode,
                [
                    new PurchaseLineDraft(
                        itemId,
                        "*",
                        new LocalizedName("صنف اختبار", "Test item"),
                        quantity,
                        Babel.Contracts.Inventory.InventoryUnits.Each,
                        Money.Of(unitPrice, CurrencyCode.Sar),
                        "standard",
                        0.15m,
                        true),
                ]),
            null,
            token);

        Require(order);

        Result<IReadOnlyList<PurchaseLineView>> orderLines =
            await Orders.GetOrderLinesAsync(tenant, Actor, order.Value.Id, token);
        Require(orderLines);

        Result<PurchasingDocumentView> receipt = await Receipts.RecordAsync(
            tenant,
            Actor,
            new GoodsReceiptDraft(
                Next("GRN"),
                order.Value.Id,
                receivedOn,
                [new GoodsReceiptLineDraft(orderLines.Value[0].Id, quantity)]),
            token);

        Require(receipt);

        Result<PurchasingDocumentView> posted = await Receipts.PostAsync(tenant, Actor, receipt.Value.Id, token);
        Require(posted);

        Result<IReadOnlyList<PurchaseLineView>> receiptLines =
            await Receipts.GetLinesAsync(tenant, Actor, receipt.Value.Id, token);
        Require(receiptLines);

        // صافي السطر = الكمية × سعر الوحدة، وهو بالضبط ما دخل حساب مراقبة المخزون
        // في القيد (`receipt_cost` على سطر `inventory_control`).
        return new ReceiptFacts(receipt.Value.Id, receiptLines.Value[0].Id, Money.Of(quantity * unitPrice, CurrencyCode.Sar));
    }

    /// <summary>ينشئ فاتورة مبيعات بسطر واحد ويرحّلها — الإيراد وحده، بلا قيد تكلفة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="customerId">العميل.</param>
    /// <param name="unitPrice">سعر البيع.</param>
    /// <param name="issuedOn">تاريخ الفاتورة.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public Task<Guid> PostedInvoiceAsync(
        TenantId tenant, Guid customerId, decimal unitPrice, DateOnly issuedOn, CancellationToken token)
        => PostedInvoiceAsync(tenant, customerId, [unitPrice], issuedOn, token);

    /// <summary>
    /// ينشئ فاتورة مبيعات <b>بعدد السطور المطلوب</b> ويرحّلها.
    /// <para>
    /// وسطران يعنيان صنفين: وهو المشهد الذي كان يُرفض قبل أن يصير قيد التكلفة
    /// بحبيبيّة السطر.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="customerId">العميل.</param>
    /// <param name="unitPrices">سعر بيع كل سطر.</param>
    /// <param name="issuedOn">تاريخ الفاتورة.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public async Task<Guid> PostedInvoiceAsync(
        TenantId tenant,
        Guid customerId,
        IReadOnlyList<decimal> unitPrices,
        DateOnly issuedOn,
        CancellationToken token)
    {
        ArgumentNullException.ThrowIfNull(unitPrices);

        Result<SalesDocumentView> created = await Invoices.CreateInvoiceAsync(
            tenant,
            Actor,
            new SalesDocumentDraft(
                Next("INV"),
                customerId,
                issuedOn,
                "BR-01",
                [.. unitPrices.Select(static price => new SalesLineDraft(
                    "*",
                    new LocalizedName("صنف اختبار", "Test item"),
                    1m,
                    Money.Of(price, CurrencyCode.Sar),
                    Money.Of(0m, CurrencyCode.Sar),
                    "standard",
                    0.15m))]),
            null,
            token);

        Require(created);

        Result<SalesDocumentView> posted = await Invoices.PostInvoiceAsync(tenant, Actor, created.Value.Id, token);
        Require(posted);

        return created.Value.Id;
    }

    /// <summary>معرّفات سطور فاتورة بترتيبها — ومعرّف السطر معرّف مستند قيد تكلفته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="invoiceId">الفاتورة.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public async Task<IReadOnlyList<Guid>> InvoiceLineIdsAsync(
        TenantId tenant, Guid invoiceId, CancellationToken token)
    {
        Result<IReadOnlyList<SalesLineView>> lines =
            await Invoices.GetInvoiceLinesAsync(tenant, Actor, invoiceId, token);

        Require(lines);
        return [.. lines.Value.Select(static line => line.Id)];
    }

    /// <summary>ينشئ عميلاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="token">رمز الإلغاء.</param>
    public async Task<Guid> CustomerAsync(TenantId tenant, CancellationToken token)
    {
        string code = Next("CUS");
        Result<CustomerView> created = await Customers.CreateAsync(
            tenant,
            Actor,
            new CustomerDraft(code, new LocalizedName("عميل " + code, "Customer " + code), Money.Of(0m, CurrencyCode.Sar), 30),
            token);

        Require(created);
        return created.Value.Id;
    }

    public static Money Sar(decimal value) => Money.Of(value, CurrencyCode.Sar);

    /// <summary>معرّف فريد داخل هذه العملية — لا مشترك بين تشغيلين.</summary>
    /// <param name="prefix">البادئة.</param>
    public static string Next(string prefix)
        => prefix + "-" + Guid.CreateVersion7().ToString("N")[^10..];

    public void Dispose()
    {
        InventoryRuntime.Dispose();
        PurchasingRuntime.Dispose();
        SalesRuntime.Dispose();
    }

    private static void Require<T>(Result<T> result)
    {
        if (result.IsFailure)
        {
            throw new InvalidOperationException(
                "تعذّرت تهيئة مستند الاختبار: " + string.Join(" | ", result.Errors.Select(static e => e.ToString())));
        }
    }
}

/// <summary>وقائع استلام مُرحَّل تحتاجها الإثباتات.</summary>
/// <param name="ReceiptId">معرّف الاستلام.</param>
/// <param name="LineId">معرّف سطر الاستلام — <b>وهو معرّف المستند في الدفتر</b>.</param>
/// <param name="LineNet">صافي السطر — وهو المبلغ الذي دخل حساب مراقبة المخزون.</param>
internal sealed record ReceiptFacts(Guid ReceiptId, Guid LineId, Money LineNet);
