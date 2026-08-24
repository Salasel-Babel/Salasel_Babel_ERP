using System.Diagnostics;
using System.Globalization;
using Babel.Contracts.Posting;
using Babel.Purchasing.Application;
using Babel.Purchasing.Subledger;
using Babel.SharedKernel;
using Npgsql;
using Xunit;

namespace Babel.Purchasing.Tests;

/// <summary>
/// إثبات الذمم الدائنة على PostgreSQL <b>حقيقية</b> ودفتر أستاذ <b>حقيقي</b>.
/// <para>كل مشهد يقابل بنداً في مهمة الإثبات، ويطبع حكمه ودليله.</para>
/// </summary>
[Collection("payables")]
public sealed class PayablesIntegrationTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 10);
    private static int _sequence;

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    private static string Next(string prefix)
        => prefix + "-" + Interlocked.Increment(ref _sequence).ToString("D5", CultureInfo.InvariantCulture);

    private static string Ledger => PurchasingTestEnvironment.Ledger.AppConnectionString;

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · الاستلام يُنشئ الالتزام قبل الفاتورة، والفاتورة تستهلكه
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Goods_receipt_creates_the_obligation_and_the_bill_consumes_it()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        decimal start = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Cycle cycle = await OrderAndReceiptAsync(10m, 100m, token);

        decimal afterReceipt = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<PurchasingDocumentView> bill = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 10m, Harness.Sar(100m), "standard", 0.15m)]),
            token);
        Assert.True(bill.IsSuccess, Describe(bill.Errors));

        Result<PurchasingDocumentView> posted = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, bill.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal afterBill = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Proof.Require(
            start - afterReceipt == 1_000.0000m && afterReceipt - afterBill == 150.0000m
            && start - afterBill == 1_150.0000m,
            "الاستلام يُنشئ التزام «بضاعة مستلمة لم تُفوتر»، والفاتورة تستهلكه وتترك ذمة إجمالية",
            "أثر الاستلام=" + Proof.Money(start - afterReceipt)
            + " وأثر الفاتورة=" + Proof.Money(afterReceipt - afterBill)
            + " والإجمالي=" + Proof.Money(start - afterBill));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · المطابقة الثلاثية ترفض فاتورة بكمية تتجاوز الاستلام
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Three_way_matching_refuses_a_bill_whose_quantity_exceeds_the_receipt()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        Cycle cycle = await OrderAndReceiptAsync(10m, 50m, token, orderedQuantity: 20m);

        Result<PurchasingDocumentView> tooMuch = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 12m, Harness.Sar(50m), "standard", 0.15m)]),
            token);

        Result<PurchasingDocumentView> exact = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 10m, Harness.Sar(50m), "standard", 0.15m)]),
            token);

        Proof.Require(
            tooMuch.IsFailure && tooMuch.Errors[0].Code == "purchasing.bill_exceeds_receipt" && exact.IsSuccess,
            "المطابقة الثلاثية ترفض فاتورة بكمية تتجاوز المستلَم، وتقبل الكمية المطابقة",
            "المرفوضة=" + tooMuch.Errors[0].Code + " · المقبولة=" + exact.Value.Number);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · الاستلام لا يتجاوز أمر الشراء
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_receipt_beyond_the_purchase_order_is_refused()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));

        Result<PurchasingDocumentView> order = await _harness.Orders.CreateOrderAsync(
            tenant,
            Harness.Actor,
            new PurchaseOrderDraft(
                Next("PO"), supplier, March, "WH-01", "CC-01", [Harness.Line("ITEM-A", 5m, 20m)]),
            null,
            token);
        Assert.True(order.IsSuccess, Describe(order.Errors));

        Result<IReadOnlyList<PurchaseLineView>> lines = await _harness.Orders
            .GetOrderLinesAsync(tenant, Harness.Actor, order.Value.Id, token);

        Result<PurchasingDocumentView> tooMuch = await _harness.Receipts.RecordAsync(
            tenant,
            Harness.Actor,
            new GoodsReceiptDraft(Next("GRN"), order.Value.Id, March, [new GoodsReceiptLineDraft(lines.Value[0].Id, 7m)]),
            token);

        Proof.Require(
            tooMuch.IsFailure && tooMuch.Errors[0].Code == "purchasing.receipt_exceeds_order",
            "الضلع الأول من المطابقة: استلام يتجاوز أمر الشراء مرفوض عند الاستلام لا عند الفاتورة",
            tooMuch.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · سند صرف يُخصَّص على فاتورتين ويترك المتبقّي الصحيح، والرسوم لا تمسّ الذمة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Payment_allocates_across_two_bills_and_the_bank_fee_never_touches_the_payable()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));

        Guid first = await PostedBillAsync(supplier, 4m, 100m, token);
        Guid second = await PostedBillAsync(supplier, 2m, 300m, token);

        decimal before = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<PurchasingDocumentView> payment = await _harness.Payments.RecordPaymentAsync(
            tenant,
            Harness.Actor,
            new SupplierPaymentDraft(
                Next("PAY"), supplier, March, "bank", "BANK-01",
                Harness.Sar(500m), Harness.Sar(25m),
                [
                    new PayableAllocationDraft(first, Harness.Sar(460m)),
                    new PayableAllocationDraft(second, Harness.Sar(40m)),
                ]),
            token);
        Assert.True(payment.IsSuccess, Describe(payment.Errors));

        Result<PurchasingDocumentView> posted = await _harness.Payments
            .PostPaymentAsync(tenant, Harness.Actor, payment.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal after = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<AgingReport> aging = await _harness.Payables.AgingAsync(tenant, Harness.Actor, March, token);
        PartyAging party = Assert.Single(aging.Value.Parties, p => p.PartyId == supplier);

        // 460 + 690 = 1,150 مستحقة، ودُفع 500 ⇒ المتبقّي 650 بالضبط، والرسوم 25 خارجها.
        Proof.Require(
            party.Buckets.Total.Amount == 650.0000m && after - before == 500.0000m,
            "سند صرف واحد يُخصَّص على فاتورتين، والرسوم البنكية لا تنقص ذمة المورد",
            "المتبقّي=" + Proof.Money(party.Buckets.Total.Amount)
            + " وحركة نقطة الضبط=" + Proof.Money(after - before));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · التخصيص الزائد مرفوض
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Over_allocation_is_refused()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));
        Guid bill = await PostedBillAsync(supplier, 1m, 100m, token);

        Result<PurchasingDocumentView> beyondBill = await _harness.Payments.RecordPaymentAsync(
            tenant,
            Harness.Actor,
            new SupplierPaymentDraft(
                Next("PAY"), supplier, March, "bank", "BANK-01",
                Harness.Sar(1_000m), Harness.Sar(0m),
                [new PayableAllocationDraft(bill, Harness.Sar(500m))]),
            token);

        Result<PurchasingDocumentView> beyondPayment = await _harness.Payments.RecordPaymentAsync(
            tenant,
            Harness.Actor,
            new SupplierPaymentDraft(
                Next("PAY"), supplier, March, "bank", "BANK-01",
                Harness.Sar(10m), Harness.Sar(0m),
                [new PayableAllocationDraft(bill, Harness.Sar(50m))]),
            token);

        Proof.Require(
            beyondBill.IsFailure && beyondBill.Errors[0].Code == "purchasing.over_allocation"
            && beyondPayment.IsFailure && beyondPayment.Errors[0].Code == "purchasing.over_allocation",
            "التخصيص الزائد مرفوض على الطرفين: أكثر مما على الفاتورة، وأكثر مما في السند",
            beyondBill.Errors[0].Code + " · " + beyondPayment.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6 · إشعار مدين يعكس الأثر، والفاتورة الأصلية وقيدها لا يُمسّان
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Debit_note_reverses_the_effect_and_the_original_is_untouched()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        Cycle cycle = await OrderAndReceiptAsync(4m, 100m, token);

        Result<PurchasingDocumentView> bill = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 4m, Harness.Sar(100m), "standard", 0.15m)]),
            token);
        Assert.True(bill.IsSuccess, Describe(bill.Errors));

        Result<PurchasingDocumentView> postedBill = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, bill.Value.Id, token);
        Assert.True(postedBill.IsSuccess, Describe(postedBill.Errors));

        Guid billEntry = postedBill.Value.EntryId!.Value;
        (string statusBefore, long linesBefore) = await LedgerProbe.EntryAsync(Ledger, billEntry, token);

        decimal before = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<PurchasingDocumentView> note = await _harness.Bills.CreateDebitNoteAsync(
            tenant,
            Harness.Actor,
            new DebitNoteDraft(Next("DBN"), bill.Value.Id, March, "ITEM-A", Harness.Sar(100m), Harness.Sar(15m)),
            token);
        Assert.True(note.IsSuccess, Describe(note.Errors));

        Result<PurchasingDocumentView> posted = await _harness.Bills
            .PostDebitNoteAsync(tenant, Harness.Actor, note.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal after = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);
        (string statusAfter, long linesAfter) = await LedgerProbe.EntryAsync(Ledger, billEntry, token);

        Result<AgingReport> aging = await _harness.Payables.AgingAsync(tenant, Harness.Actor, March, token);
        PartyAging party = Assert.Single(aging.Value.Parties, p => p.PartyId == cycle.SupplierId);

        Proof.Require(
            after - before == 115.0000m
            && party.Buckets.Total.Amount == 345.0000m
            && statusBefore == statusAfter && linesBefore == linesAfter,
            "إشعار مدين ينقص ذمة المورد بقيد مستقلّ، والفاتورة الأصلية وقيدها لم يُمسّا",
            "حركة نقطة الضبط=" + Proof.Money(after - before)
            + " ورصيد المورد=" + Proof.Money(party.Buckets.Total.Amount)
            + " · قيد الأصل قبل=" + statusBefore + "/" + linesBefore.ToString(CultureInfo.InvariantCulture)
            + " بعد=" + statusAfter + "/" + linesAfter.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 6-ب · إشعار مدين على فاتورة مصروف لا يعبّر عنه قالب المصفوفة — رفض بصوت عالٍ
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_debit_note_on_an_expense_bill_is_refused_loudly()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));
        Guid bill = await PostedBillAsync(supplier, 1m, 300m, token);

        Result<PurchasingDocumentView> note = await _harness.Bills.CreateDebitNoteAsync(
            tenant,
            Harness.Actor,
            new DebitNoteDraft(Next("DBN"), bill, March, "SRV-1", Harness.Sar(50m), Harness.Sar(7.50m)),
            token);

        Proof.Require(
            note.IsFailure && note.Errors[0].Code == "purchasing.debit_note_on_expense_bill_not_expressible",
            "مرتجع فاتورة مصروف لا قالب له في المصفوفة، فيُرفض في الوحدة بصوت عالٍ لا يُخترَع له قيد",
            note.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 7 · تكلفة الاستيراد تُحمَّل على المخزون وتُنشئ ذمة للمورّد
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Landed_cost_is_capitalised_and_creates_a_payable_when_billed_by_the_supplier()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Cycle cycle = await OrderAndReceiptAsync(2m, 400m, token);
        Guid freight = await _harness.SupplierAsync(Next("SUP"));

        decimal before = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<PurchasingDocumentView> cost = await _harness.Payments.RecordLandedCostAsync(
            tenant,
            Harness.Actor,
            new LandedCostDraft(
                Next("LC"), freight, cycle.ReceiptId, March, "ITEM-A", "*",
                "supplier_invoice", "bank", "BANK-01", Harness.Sar(220m)),
            token);
        Assert.True(cost.IsSuccess, Describe(cost.Errors));

        Result<PurchasingDocumentView> posted = await _harness.Payments
            .PostLandedCostAsync(tenant, Harness.Actor, cost.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal after = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Proof.Require(
            before - after == 220.0000m,
            "تكلفة الاستيراد المفوترة من المورد تُحمَّل على المخزون وتُنشئ ذمة بالمبلغ نفسه",
            "حركة نقطة الضبط=" + Proof.Money(before - after));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 8 · فاتورة مصروف مباشر: الضريبة غير المستردة تُحمَّل على المصروف
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task An_expense_bill_charges_non_recoverable_tax_to_the_expense_not_to_the_claim()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));

        decimal before = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<PurchasingDocumentView> bill = await _harness.Bills.CreateExpenseBillAsync(
            tenant,
            Harness.Actor,
            new ExpenseBillDraft(
                Next("EXP"), supplier, March, "office", "CC-01",
                [
                    Harness.Line("SRV-1", 1m, 1_000m),
                    Harness.Line("SRV-2", 1m, 500m, recoverable: false),
                ]),
            token);
        Assert.True(bill.IsSuccess, Describe(bill.Errors));

        Result<PurchasingDocumentView> posted = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, bill.Value.Id, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));

        decimal after = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Proof.Require(
            bill.Value.Totals.Gross.Amount == 1_725.0000m && before - after == 1_725.0000m,
            "فاتورة مصروف: الضريبة المستردة وغير المستردة يفترقان في القيد ويجتمعان في ذمة المورد",
            "الإجمالي=" + Proof.Money(bill.Value.Totals.Gross.Amount)
            + " وحركة نقطة الضبط=" + Proof.Money(before - after));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 9 · مستند مكرَّر يُرحَّل مرة واحدة تحت ثلاثة ترتيبات وصول
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_duplicated_document_posts_exactly_once_under_three_arrival_orders()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));

        List<Guid> bills = [];
        for (int index = 0; index < 3; index++)
        {
            bills.Add(await DraftBillAsync(supplier, 1m, 200m, token));
        }

        decimal before = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        int[][] orders =
        [
            [0, 1, 2, 0, 1, 2],
            [2, 0, 1, 2, 2, 0],
            [1, 2, 0, 1, 0, 2],
        ];

        foreach (int[] arrival in orders)
        {
            foreach (int index in arrival)
            {
                Result<PurchasingDocumentView> posted = await _harness.Bills
                    .PostBillAsync(tenant, Harness.Actor, bills[index], token);
                Assert.True(posted.IsSuccess, Describe(posted.Errors));
            }
        }

        decimal after = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        long entries = 0;
        foreach (Guid bill in bills)
        {
            entries += await LedgerProbe.EntryCountAsync(
                Ledger, tenant, "SupplierBill", bill.ToString("D", CultureInfo.InvariantCulture), token);
        }

        Proof.Require(
            entries == 3 && before - after == 3 * 230.0000m,
            "ثمانية عشر نداء ترحيل بثلاثة ترتيبات وصول تُنتج ثلاثة قيود بالضبط",
            "عدد القيود=" + entries.ToString(CultureInfo.InvariantCulture)
            + " وحركة نقطة الضبط=" + Proof.Money(before - after));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 10 · أعمار الذمم تطابق نقطة الضبط · والمطابقة تُبلّغ صفراً
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Aging_ties_exactly_to_the_control_point_and_reconciliation_reports_zero()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"), termsDays: 0);
        await PostedBillAsync(supplier, 3m, 70m, token);

        DateOnly asOf = new(2026, 5, 31);
        Result<AgingReport> aging = await _harness.Payables.AgingAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(aging.IsSuccess, Describe(aging.Errors));

        decimal control = -await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        AgingBuckets totals = aging.Value.Totals;
        decimal sumOfBuckets = totals.NotDue.Amount + totals.Days1To30.Amount
            + totals.Days31To60.Amount + totals.Days61To90.Amount + totals.Over90.Amount;

        Result<ControlReconciliationReport> reconciliation = await _harness.Payables
            .ReconcileAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(reconciliation.IsSuccess, Describe(reconciliation.Errors));

        Proof.Require(
            totals.Total.Amount == control && sumOfBuckets == totals.Total.Amount,
            "شرائح أعمار الذمم الدائنة تطابق نقطة الضبط بالضبط، وتشمل رصيد البضاعة المستلمة غير المفوترة",
            "مجموع الشرائح=" + Proof.Money(sumOfBuckets)
            + " ونقطة الضبط=" + Proof.Money(control));

        Proof.Require(
            reconciliation.Value.IsReconciled && reconciliation.Value.Divergence.Amount == 0m,
            "المطابقة على مجموعة سليمة تُبلّغ انحرافاً صفرياً بلا مستند واحد مسؤول",
            "الدفتر المساعد=" + Proof.Money(reconciliation.Value.SubledgerTotal.Amount)
            + " ونقطة الضبط=" + Proof.Money(reconciliation.Value.ControlTotal.Amount)
            + " والانحرافات=" + reconciliation.Value.Divergences.Count.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 11 · المطابقة تلتقط انحرافاً محقوناً وتُسمّي المستند المسؤول
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Reconciliation_identifies_an_injected_divergence_and_names_the_document()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.InjectedTenant;
        DateOnly asOf = new(2026, 5, 31);

        Result<ControlReconciliationReport> clean = await _harness.Payables
            .ReconcileAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(clean.IsSuccess, Describe(clean.Errors));
        Assert.True(clean.Value.IsReconciled);

        string stray = Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture);

        Result<PostingReceipt> injected = await _harness.Posting.PostAsync(
            new PostingRequest
            {
                Tenant = tenant,
                IdempotencyKey = new IdempotencyKey("manual:stray:" + stray.Replace("-", string.Empty, StringComparison.Ordinal)),
                Source = new SourceDocument(BabelModule.Purchasing, "ManualJournal", stray),
                Trigger = PostingTrigger.OnApproval,
                DocumentDate = March,
                Narration = new LocalizedName("قيد يدوي على الحساب الضابط", "Manual entry on the control account"),
                Currency = CurrencyCode.Sar,
                Event = new PostingEventCode("purchasing.goods_receipt.posted"),
                Amounts = [new PostingAmount("receipt_cost", Harness.Sar(444m))],
                Facts =
                [
                    new PostingFact("subledger.supplier", "GHOST"),
                    new PostingFact("subledger.item", "ITEM-GHOST"),
                    new PostingFact("line.item_group", "*"),
                ],
                Dimensions = [new PostingDimension("warehouse", "WH-01")],
                Lines = [],
            },
            token);

        Assert.True(injected.IsSuccess, Describe(injected.Errors));

        Result<ControlReconciliationReport> dirty = await _harness.Payables
            .ReconcileAsync(tenant, Harness.Actor, asOf, token);
        Assert.True(dirty.IsSuccess, Describe(dirty.Errors));

        ReconciliationDivergence responsible = Assert.Single(dirty.Value.Divergences);

        Proof.Require(
            !dirty.Value.IsReconciled
            && dirty.Value.Divergence.Amount == -444.0000m
            && responsible.ReasonCode == DivergenceReason.MissingInSubledger
            && responsible.DocumentId == stray
            && responsible.PartyId == "GHOST",
            "المطابقة تلتقط الانحراف المحقون وتُسمّي المستند والطرف المسؤولين",
            "الانحراف=" + Proof.Money(dirty.Value.Divergence.Amount)
            + " · السبب=" + responsible.ReasonCode
            + " · المستند=" + responsible.DocumentType + "/" + responsible.DocumentId
            + " · الطرف=" + responsible.PartyId);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 12 · فرق سعر لصالح المنشأة لا يعبّر عنه القالب — والرفض بصوت عالٍ
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_favourable_price_variance_is_refused_loudly_rather_than_posted_wrong()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Cycle cycle = await OrderAndReceiptAsync(5m, 100m, token);

        Result<PurchasingDocumentView> cheaper = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 5m, Harness.Sar(90m), "standard", 0.15m)]),
            token);

        Result<PurchasingDocumentView> dearer = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 5m, Harness.Sar(110m), "standard", 0.15m)]),
            token);

        Result<PurchasingDocumentView> postedDearer = dearer.IsSuccess
            ? await _harness.Bills.PostBillAsync(tenant, Harness.Actor, dearer.Value.Id, token)
            : dearer;

        Proof.Require(
            cheaper.IsFailure
            && cheaper.Errors[0].Code == "purchasing.favourable_price_variance_not_expressible"
            && postedDearer.IsSuccess,
            "فرق السعر الموجب يُرحَّل، والسالب يُرفض بصوت عالٍ لأن قالب المصفوفة لا يعبّر عنه",
            "السالب=" + cheaper.Errors[0].Code + " · الموجب=" + postedDearer.Value.State);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 13 · كشف حساب المورد
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task The_supplier_statement_closing_balance_matches_the_subledger()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));
        Guid bill = await PostedBillAsync(supplier, 2m, 500m, token);

        Result<PurchasingDocumentView> payment = await _harness.Payments.RecordPaymentAsync(
            tenant,
            Harness.Actor,
            new SupplierPaymentDraft(
                Next("PAY"), supplier, March, "bank", "BANK-01",
                Harness.Sar(600m), Harness.Sar(0m),
                [new PayableAllocationDraft(bill, Harness.Sar(600m))]),
            token);
        Assert.True(payment.IsSuccess, Describe(payment.Errors));
        Result<PurchasingDocumentView> postedPayment = await _harness.Payments
            .PostPaymentAsync(tenant, Harness.Actor, payment.Value.Id, token);
        Assert.True(postedPayment.IsSuccess, Describe(postedPayment.Errors));

        Result<PartyStatement> statement = await _harness.Payables.StatementAsync(
            tenant, Harness.Actor, supplier, new DateOnly(2026, 1, 1), new DateOnly(2026, 5, 31), token);
        Assert.True(statement.IsSuccess, Describe(statement.Errors));

        Result<AgingReport> aging = await _harness.Payables
            .AgingAsync(tenant, Harness.Actor, new DateOnly(2026, 5, 31), token);
        PartyAging party = Assert.Single(aging.Value.Parties, p => p.PartyId == supplier);

        Proof.Require(
            statement.Value.Closing.Amount == party.Buckets.Total.Amount
            && statement.Value.Closing.Amount == 550.0000m,
            "كشف حساب المورد: رصيده الختامي هو رصيده في أعمار الذمم بالضبط",
            "الرصيد الختامي=" + Proof.Money(statement.Value.Closing.Amount)
            + " وأعمار الذمم=" + Proof.Money(party.Buckets.Total.Amount)
            + " وعدد الحركات=" + statement.Value.Lines.Count.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 14 · ترحيل مرفوض يترك المستند متّسقاً وقابلاً لإعادة المحاولة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_refused_posting_leaves_the_document_coherent_and_retryable()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));
        DateOnly closed = new(2026, 2, 15);

        Result<PurchasingDocumentView> bill = await _harness.Bills.CreateExpenseBillAsync(
            tenant,
            Harness.Actor,
            new ExpenseBillDraft(Next("EXP"), supplier, closed, "office", "CC-01", [Harness.Line("SRV-9", 1m, 100m)]),
            token);
        Assert.True(bill.IsSuccess, Describe(bill.Errors));

        Result<PurchasingDocumentView> first = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, bill.Value.Id, token);
        Result<PurchasingDocumentView> second = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, bill.Value.Id, token);

        long entriesAfterRefusal = await LedgerProbe.EntryCountAsync(
            Ledger, tenant, "SupplierBill", bill.Value.Id.ToString("D", CultureInfo.InvariantCulture), token);

        (string state, int attempts, string failure) = await AttemptAsync(bill.Value.Id, token);

        await ReopenFebruaryAsync(tenant, token);
        _harness.LedgerRuntime.InvalidateReference(tenant.Value);

        Result<PurchasingDocumentView> retried = await _harness.Bills
            .PostBillAsync(tenant, Harness.Actor, bill.Value.Id, token);

        long entriesAfterRetry = await LedgerProbe.EntryCountAsync(
            Ledger, tenant, "SupplierBill", bill.Value.Id.ToString("D", CultureInfo.InvariantCulture), token);

        Proof.Require(
            first.IsFailure && second.IsFailure
            && entriesAfterRefusal == 0
            && state == "REFUSED" && attempts == 2 && failure.Length > 0
            && retried.IsSuccess && entriesAfterRetry == 1,
            "الترحيل المرفوض يترك فاتورة المورد مسوّدةً ومعه سبب مكتوب، وإعادة المحاولة تنتج قيداً واحداً",
            "قيود بعد الرفض=" + entriesAfterRefusal.ToString(CultureInfo.InvariantCulture)
            + " · سجلّ المحاولة=" + state + "/" + attempts.ToString(CultureInfo.InvariantCulture) + "/" + failure
            + " · قيود بعد إعادة المحاولة=" + entriesAfterRetry.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 15 · الإنتاجية — دفعة فواتير موردين
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task Throughput_of_posting_a_batch_of_supplier_bills()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));

        const int Batch = 60;
        List<Guid> bills = [];
        for (int index = 0; index < Batch; index++)
        {
            bills.Add(await DraftBillAsync(supplier, 1m, 50m, token));
        }

        Stopwatch clock = Stopwatch.StartNew();
        foreach (Guid bill in bills)
        {
            Result<PurchasingDocumentView> posted = await _harness.Bills
                .PostBillAsync(tenant, Harness.Actor, bill, token);
            Assert.True(posted.IsSuccess, Describe(posted.Errors));
        }

        clock.Stop();
        double perSecond = Batch / clock.Elapsed.TotalSeconds;

        Proof.Require(
            perSecond > 0,
            "إنتاجية ترحيل دفعة فواتير موردين عبر كامل مسار الوحدة",
            Batch.ToString(CultureInfo.InvariantCulture) + " فاتورة في "
            + clock.Elapsed.TotalSeconds.ToString("0.000", CultureInfo.InvariantCulture) + " ثانية = "
            + perSecond.ToString("0.0", CultureInfo.InvariantCulture) + " فاتورة/ث");

        Proof.Note(
            "التحفّظ: حاوية مشتركة بأربع أنوية افتراضية، وPostgreSQL على المضيف نفسه (RTT شبه صفري)، "
            + "وكاتب واحد متسلسل، ورقم يشمل كتابة الوحدة وقراءتها بـEF Core لا الترحيل وحده.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 16 · مسوّدة فاتورة مخزنية تحجز ولا تُعفي رصيد البضاعة المستلمة غير المفوترة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task A_draft_stock_bill_reserves_the_receipt_without_relieving_the_open_item()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = PurchasingTestEnvironment.Tenant;

        decimal start = await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Cycle cycle = await OrderAndReceiptAsync(4m, 125m, token);

        Result<PurchasingDocumentView> draft = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 4m, Harness.Sar(125m), "standard", 0.15m)]),
            token);
        Assert.True(draft.IsSuccess, Describe(draft.Errors));

        // ضلع المطابقة الثلاثية ما زال محجوزاً: لا فاتورة ثانية على الاستلام نفسه.
        Result<PurchasingDocumentView> again = await _harness.Bills.CreateStockBillAsync(
            tenant,
            Harness.Actor,
            new StockBillDraft(
                Next("BILL"), cycle.ReceiptId, March,
                [new SupplierBillLineDraft(cycle.ReceiptLineId, 4m, Harness.Sar(125m), "standard", 0.15m)]),
            token);

        decimal control = start - await LedgerProbe.ControlNetAsync(Ledger, tenant, "supplier", token);

        Result<AgingReport> aging = await _harness.Payables.AgingAsync(tenant, Harness.Actor, March, token);
        PartyAging party = Assert.Single(aging.Value.Parties, p => p.PartyId == cycle.SupplierId);

        Proof.Require(
            draft.Value.State == "DRAFT"
            && again.IsFailure && again.Errors[0].Code == "purchasing.bill_exceeds_receipt"
            && control == 500.0000m
            && party.Buckets.Total.Amount == 500.0000m,
            "مسوّدة فاتورة مخزنية تحجز الكمية في المطابقة ولا تُعفي البند المفتوح، "
            + "فيبقى الدفتر المساعد على نقطة ضبطه حتى الترحيل",
            "حالة المسوّدة=" + draft.Value.State
            + " · الفاتورة الثانية=" + again.Errors[0].Code
            + " · حركة نقطة الضبط=" + Proof.Money(control)
            + " · رصيد المورد في أعمار الذمم=" + Proof.Money(party.Buckets.Total.Amount));
    }

    private sealed record Cycle(Guid OrderId, Guid ReceiptId, Guid ReceiptLineId, Guid SupplierId);

    private async Task<Cycle> OrderAndReceiptAsync(
        decimal quantity,
        decimal unitPrice,
        CancellationToken token,
        decimal? orderedQuantity = null)
    {
        TenantId tenant = PurchasingTestEnvironment.Tenant;
        Guid supplier = await _harness.SupplierAsync(Next("SUP"));

        Result<PurchasingDocumentView> request = await _harness.Orders.CreateRequestAsync(
            tenant,
            Harness.Actor,
            new PurchaseRequestDraft(Next("PR"), March, "CC-01", [Harness.Line("ITEM-A", orderedQuantity ?? quantity, unitPrice)]),
            token);
        Assert.True(request.IsSuccess, Describe(request.Errors));

        Result<PurchasingDocumentView> approved = await _harness.Orders
            .ApproveRequestAsync(tenant, Harness.Actor, request.Value.Id, token);
        Assert.True(approved.IsSuccess, Describe(approved.Errors));

        Result<PurchasingDocumentView> order = await _harness.Orders.CreateOrderAsync(
            tenant,
            Harness.Actor,
            new PurchaseOrderDraft(
                Next("PO"), supplier, March, "WH-01", "CC-01",
                [Harness.Line("ITEM-A", orderedQuantity ?? quantity, unitPrice)]),
            request.Value.Id,
            token);
        Assert.True(order.IsSuccess, Describe(order.Errors));

        Result<IReadOnlyList<PurchaseLineView>> orderLines = await _harness.Orders
            .GetOrderLinesAsync(tenant, Harness.Actor, order.Value.Id, token);

        Result<PurchasingDocumentView> receipt = await _harness.Receipts.RecordAsync(
            tenant,
            Harness.Actor,
            new GoodsReceiptDraft(Next("GRN"), order.Value.Id, March, [new GoodsReceiptLineDraft(orderLines.Value[0].Id, quantity)]),
            token);
        Assert.True(receipt.IsSuccess, Describe(receipt.Errors));

        Result<PurchasingDocumentView> postedReceipt = await _harness.Receipts
            .PostAsync(tenant, Harness.Actor, receipt.Value.Id, token);
        Assert.True(postedReceipt.IsSuccess, Describe(postedReceipt.Errors));

        Result<IReadOnlyList<PurchaseLineView>> receiptLines = await _harness.Receipts
            .GetLinesAsync(tenant, Harness.Actor, receipt.Value.Id, token);

        return new Cycle(order.Value.Id, receipt.Value.Id, receiptLines.Value[0].Id, supplier);
    }

    private async Task<Guid> DraftBillAsync(Guid supplier, decimal quantity, decimal unitPrice, CancellationToken token)
    {
        Result<PurchasingDocumentView> bill = await _harness.Bills.CreateExpenseBillAsync(
            PurchasingTestEnvironment.Tenant,
            Harness.Actor,
            new ExpenseBillDraft(
                Next("EXP"), supplier, March, "office", "CC-01", [Harness.Line("SRV-1", quantity, unitPrice)]),
            token);

        Assert.True(bill.IsSuccess, Describe(bill.Errors));
        return bill.Value.Id;
    }

    private async Task<Guid> PostedBillAsync(Guid supplier, decimal quantity, decimal unitPrice, CancellationToken token)
    {
        Guid bill = await DraftBillAsync(supplier, quantity, unitPrice, token);
        Result<PurchasingDocumentView> posted = await _harness.Bills
            .PostBillAsync(PurchasingTestEnvironment.Tenant, Harness.Actor, bill, token);
        Assert.True(posted.IsSuccess, Describe(posted.Errors));
        return bill;
    }

    private static async Task<(string State, int Attempts, string Failure)> AttemptAsync(Guid documentId, CancellationToken token)
    {
        await using NpgsqlConnection connection = new(PurchasingTestEnvironment.Purchasing.ConnectionString);
        await connection.OpenAsync(token);
        await using NpgsqlCommand command = new(
            """
            select "State", "AttemptCount", "FailureCode"
              from purchasing.document_posting
             where "DocumentType" = 'SupplierBill' and "DocumentId" = $1
            """, connection);
        command.Parameters.AddWithValue(documentId.ToString("D", CultureInfo.InvariantCulture));
        await using NpgsqlDataReader reader = await command.ExecuteReaderAsync(token);
        await reader.ReadAsync(token);
        return (reader.GetString(0), reader.GetInt32(1), reader.GetString(2));
    }

    private static async Task ReopenFebruaryAsync(TenantId tenant, CancellationToken token)
    {
        await using NpgsqlConnection owner = new(PurchasingTestEnvironment.Ledger.OwnerConnectionString);
        await owner.OpenAsync(token);
        await using NpgsqlCommand command = new(
            "update ledger.fiscal_period set state = 'open' where company_id = $1 and period_code = '2026-02'", owner);
        command.Parameters.AddWithValue(tenant.Value);
        await command.ExecuteNonQueryAsync(token);
    }

    private static string Describe(IReadOnlyList<Error> errors)
        => string.Join(" | ", errors.Select(static error => error.ToString()));
}
