using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Inventory.Application;
using Babel.Inventory.Subledger;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>عكس فاتورة المبيعات يعكس أثرها المادي، لا أثرها المالي وحده.</b>
/// <para>
/// العطل الذي يُثبته هذا الملفّ قبل أن يُصلحه: <c>ReverseInvoiceAsync</c> كانت تعكس
/// <b>قيد الإيراد وحده</b>. فقيد تكلفة المبيعات يبقى مُرحَّلاً، والبضاعة تبقى خارج
/// المستودع، و<b>الدفتر يتوازن</b> — لأن قيد التكلفة متوازن بذاته وسلسلة البصمات
/// سليمة وميزان المراجعة يقفل. أي أن الرقم المالي صحيح والحقيقة المادية خاطئة، وهو
/// الصنف الذي يفشل بصمت تامّ: لا استثناء، ولا رسالة، ولا سطر سجلّ.
/// </para>
/// <para>
/// <b>والأرقام الثلاثة هي الحكم</b>: رصيد الصنف، وأثر المستند على حساب تكلفة
/// المبيعات، وأثره على حساب مراقبة المخزون. تُقرأ قبل العكس وبعده، ولا يُقبل «قريب».
/// </para>
/// </summary>
[Collection("inventory")]
public sealed class InvoiceReversalReturnsTheGoodsTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 10);
    private static readonly DateOnly AsOf = new(2026, 12, 31);
    private const string Warehouse = "WH-01";

    /// <summary>الموقع داخل المستودع — قيمة صريحة في كل نداء، كما في الإنتاج.</summary>
    private const string Location = InventoryLocations.Default;

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task عكس_الفاتورة_يعكس_تكلفة_المبيعات_ويُعيد_البضاعة_إلى_المخزون()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.Tenant;
        string item = Harness.Next("ITEM");

        // ── ١ · استلام حقيقي: 100 وحدة بعشرة ريالات ──────────────────────────
        await _harness.PostGoodsReceiptAsync(tenant, item, Warehouse, 100m, 10m, March, token);

        // ── ٢ · فاتورة مُرحَّلة ومعها قيد تكلفتها: 30 وحدة بمتوسط 10 ⇒ 300 ────
        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, 900m, March, token);
        Guid line = (await _harness.InvoiceLineIdsAsync(tenant, invoice, token))[0];
        string lineId = line.ToString("D", CultureInfo.InvariantCulture);

        Result<PostingReceipt> cost = await _harness.Invoices.PostCostOfSalesAsync(
            tenant,
            Harness.Actor,
            invoice,
            new Babel.Sales.Application.CostOfSalesDraft(line, item, Warehouse, Location, "*", Sold(30m)),
            token);

        Assert.True(cost.IsSuccess, Describe(cost));

        // ── ٣ · الأرقام الثلاثة **قبل** العكس ────────────────────────────────
        Result<StockBalanceView> before = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, Location, token);

        Assert.True(before.IsSuccess, Describe(before));

        decimal cogsBefore = await DocumentNetOnRoleAsync(tenant, "SalesInvoiceLine", lineId, "cogs", token);
        decimal controlBefore = await DocumentNetOnRoleAsync(tenant, "SalesInvoiceLine", lineId, "inventory_control", token);

        Proof.Require(
            before.Value.Quantity.Magnitude == 70m
            && before.Value.Value.Amount == 700.0000m
            && cogsBefore == 300.0000m
            && controlBefore == -300.0000m,
            "قبل العكس: البضاعة خرجت والتكلفة سُجّلت",
            "رصيد الصنف=" + Quantity(before.Value.Quantity.Magnitude) + " وحدة / " + Proof.Money(before.Value.Value.Amount)
            + " · تكلفة المبيعات=" + Proof.Money(cogsBefore)
            + " · مراقبة المخزون=" + Proof.Money(controlBefore));

        // ── ٤ · العكس ────────────────────────────────────────────────────────
        Result<PostingReceipt> reversal = await _harness.Invoices.ReverseInvoiceAsync(
            tenant,
            Harness.Actor,
            invoice,
            new LocalizedName("عكس فاتورة للاختبار", "Invoice reversal under test"),
            token);

        Assert.True(reversal.IsSuccess, Describe(reversal));

        // ── ٥ · الأرقام الثلاثة **بعد** العكس ────────────────────────────────
        Result<StockBalanceView> after = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, Location, token);

        Assert.True(after.IsSuccess, Describe(after));

        decimal cogsAfter = await DocumentNetOnRoleAsync(tenant, "SalesInvoiceLine", lineId, "cogs", token);
        decimal controlAfter = await DocumentNetOnRoleAsync(tenant, "SalesInvoiceLine", lineId, "inventory_control", token);

        Proof.Require(
            after.Value.Quantity.Magnitude == 100m
            && after.Value.Value.Amount == 1_000.0000m,
            "بعد العكس: البضاعة عادت إلى المستودع بكميتها وقيمتها",
            "رصيد الصنف=" + Quantity(after.Value.Quantity.Magnitude) + " وحدة / " + Proof.Money(after.Value.Value.Amount));

        Proof.Require(
            cogsAfter == 0.0000m,
            "وتكلفة المبيعات عن هذا المستند عادت صفراً — القيد عُكس ولم يُمحَ",
            "تكلفة المبيعات بعد العكس=" + Proof.Money(cogsAfter));

        Proof.Require(
            controlAfter == 0.0000m,
            "وأثر المستند على مراقبة المخزون عاد صفراً",
            "مراقبة المخزون بعد العكس=" + Proof.Money(controlAfter));

        // ── ٦ · والمطابقة تبقى صفراً: الطرفان تحرّكا معاً ────────────────────
        Result<ControlReconciliationReport> report = await _harness.Valuation
            .ReconcileAsync(tenant, Harness.Actor, AsOf, token);

        Assert.True(report.IsSuccess, Describe(report));

        Proof.Require(
            report.Value.IsReconciled && report.Value.Divergences.Count == 0,
            "ودفتر المخزون المساعد ما يزال مطابقاً لحسابه الضابط بعد العكس",
            "الانحراف=" + Proof.Money(report.Value.Divergence.Amount)
            + " · مستندات منحرفة=" + report.Value.Divergences.Count.ToString(CultureInfo.InvariantCulture));
    }

    /// <summary>
    /// إعادة العكس بالهوية نفسها لا تُعيد البضاعة مرّتين.
    /// <para>
    /// الدفتر يحرس نفسه (<c>ledger.entry.already_reversed</c>)، والدفتر المساعد يحرس
    /// نفسه بهوية الحركة. والاثنان معاً هما ما يجعل «أعد المحاولة» جواباً آمناً بعد
    /// انقطاع شبكة في منتصف العكس.
    /// </para>
    /// </summary>
    [Fact]
    public async Task إعادة_العكس_لا_تُعيد_البضاعة_مرّتين()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.Tenant;
        string item = Harness.Next("ITEM");

        await _harness.PostGoodsReceiptAsync(tenant, item, Warehouse, 50m, 4m, March, token);

        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, 900m, March, token);
        Guid line = (await _harness.InvoiceLineIdsAsync(tenant, invoice, token))[0];

        Result<PostingReceipt> cost = await _harness.Invoices.PostCostOfSalesAsync(
            tenant, Harness.Actor, invoice,
            new Babel.Sales.Application.CostOfSalesDraft(line, item, Warehouse, Location, "*", Sold(20m)), token);

        Assert.True(cost.IsSuccess, Describe(cost));

        LocalizedName reason = new("عكس مُعاد", "Replayed reversal");

        Result<PostingReceipt> first = await _harness.Invoices
            .ReverseInvoiceAsync(tenant, Harness.Actor, invoice, reason, token);

        Assert.True(first.IsSuccess, Describe(first));

        Result<StockBalanceView> afterFirst = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, Location, token);

        Assert.True(afterFirst.IsSuccess, Describe(afterFirst));

        // المحاولة الثانية تُرفض من الدفتر: الفاتورة لم تعد POSTED.
        Result<PostingReceipt> second = await _harness.Invoices
            .ReverseInvoiceAsync(tenant, Harness.Actor, invoice, reason, token);

        Assert.True(second.IsFailure, "العكس الثاني مرّ على فاتورة معكوسة سلفاً.");

        Result<StockBalanceView> afterSecond = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, Location, token);

        Assert.True(afterSecond.IsSuccess, Describe(afterSecond));

        Proof.Require(
            afterFirst.Value.Quantity.Magnitude == 50m
            && afterSecond.Value.Quantity.Magnitude == 50m
            && afterFirst.Value.Value.Amount == afterSecond.Value.Value.Amount,
            "الرصيد عاد مرّة واحدة رغم محاولتَي عكس",
            "بعد الأولى=" + Quantity(afterFirst.Value.Quantity.Magnitude)
            + " · بعد الثانية=" + Quantity(afterSecond.Value.Quantity.Magnitude)
            + " · رمز رفض الثانية=" + second.Errors[0].Code);
    }

    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// صافي «مدين ناقص دائن» على دورٍ بعينه، لمستندٍ بعينه، مقروءاً من الدفتر.
    /// <para>
    /// والقراءة بالدور لا برقم الحساب: القاعدة 2 تمنع الوحدة من تسمية حساب، ومجموعة
    /// الاختبار وحدة. والدور هو ما تسمّيه المصفوفة، وهو ما يُقارَن.
    /// </para>
    /// </summary>
    private static async Task<decimal> DocumentNetOnRoleAsync(
        TenantId tenant, string documentType, string documentId, string role, CancellationToken token)
    {
        await using Npgsql.NpgsqlConnection connection = new(InventoryTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token);

        await using Npgsql.NpgsqlCommand command = new(
            """
            select coalesce(sum(l.debit_company - l.credit_company), 0)
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where l.company_id = $1 and l.role_code = $4
               and e.source_doc_type = $2 and e.source_doc_id = $3
            """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(documentType);
        command.Parameters.AddWithValue(documentId);
        command.Parameters.AddWithValue(role);

        return (decimal)(await command.ExecuteScalarAsync(token))!;
    }

    /// <summary>كمّية بوحدة العدّ — ولا كمّية مجرّدة تعبر حدّ المخزون.</summary>
    private static InventoryQuantity Sold(decimal magnitude) => new(magnitude, InventoryUnits.Each);

    private static string Quantity(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.ToString()));
}
