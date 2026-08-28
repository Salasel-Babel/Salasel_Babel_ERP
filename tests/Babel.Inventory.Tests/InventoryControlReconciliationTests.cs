using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Inventory.Application;
using Babel.Inventory.Subledger;
using Babel.Sales.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>دفتر المخزون المساعد يطابق حسابه الضابط — بمستندات حقيقية.</b>
/// <para>
/// لا محاكاة في هذا الملف: استلام بضاعة يُرحَّل من وحدة المشتريات، وفاتورة مبيعات
/// تُرحَّل من وحدة المبيعات ومعها قيد تكلفتها، والدفتر PostgreSQL حقيقي بمخطّطه
/// وبمصفوفته وبمشغّل التوازن المؤجَّل. والرقم الذي يُقارَن هو الرقم الذي يُنتجه المنتج.
/// </para>
/// <para>
/// و<b>المستأجرون ثلاثة لأن الأسئلة ثلاثة</b>: منشأةٌ كلّ ما فيها يطابق، ومنشأةٌ يقع
/// فيها البيع على المكشوف ثم تهبط عليها التكلفة متأخّرة، ومنشأةٌ تُقاس فيها الهوية
/// والإحكام بلا ترحيل. وخلطُها يجعل كل إثبات يُفسد الآخر.
/// </para>
/// </summary>
[Collection("inventory")]
public sealed class InventoryControlReconciliationTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 10);
    private static readonly DateOnly AsOf = new(2026, 12, 31);
    private const string Warehouse = "WH-01";

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · دورة كاملة: استلام حقيقي، ثم بيع بقيد تكلفة محسوب — والدفتران متطابقان
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task دفتر_المخزون_يطابق_حسابه_الضابط_بعد_استلام_وبيع_حقيقيين()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.Tenant;
        string item = Harness.Next("ITEM");

        // ── ١ · استلام بضاعة يُرحَّل فعلاً: 100 وحدة بعشرة ريالات ─────────────
        //     **ولا نداء ثانٍ بعده.** الاستلام نفسه يبلغ الدفتر المساعد؛ وكان
        //     ذلك النداء مكتوباً هنا في تجهيزة الاختبار وحدها، فكان الحساب الضابط
        //     يتحرّك في أي نشرٍ حقيقي والدفتر المساعد لا يتحرّك.
        ReceiptFacts receipt = await _harness.PostGoodsReceiptAsync(
            tenant, item, Warehouse, 100m, 10m, March, token);

        Result<StockBalanceView> afterReceipt = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(afterReceipt.IsSuccess, Describe(afterReceipt));

        Proof.Require(
            afterReceipt.Value.Quantity == 100m
            && afterReceipt.Value.Value.Amount == 1_000.0000m
            && afterReceipt.Value.HasCostBasis,
            "الاستلام وحده أنشأ أساس التكلفة في الدفتر المساعد — بلا نداء منفصل",
            "الكمية=" + Quantity(afterReceipt.Value.Quantity)
            + " والقيمة=" + Proof.Money(afterReceipt.Value.Value.Amount));

        // والحركة تحت هوية سطر الاستلام نفسها التي رُحّل بها القيد، لا هوية أخرى.
        string receiptLineId = receipt.LineId.ToString("D", CultureInfo.InvariantCulture);
        decimal receiptControl = await DocumentNetOnItemControlAsync(
            tenant, "GoodsReceiptLine", receiptLineId, token);

        Proof.Require(
            receiptControl == 1_000.0000m,
            "وحساب مراقبة المخزون تحرّك بالمبلغ نفسه تحت المستند نفسه",
            "أثر GoodsReceiptLine على نقطة الضبط=" + Proof.Money(receiptControl));

        // ── ٢ · فاتورة مبيعات مُرحَّلة، ثم قيد تكلفتها ────────────────────────
        //     والمستدعي يُسلّم **كمية** لا مبلغاً. المبلغ يأتي من حدّ التقييم.
        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, 900m, March, token);
        Guid invoiceLine = (await _harness.InvoiceLineIdsAsync(tenant, invoice, token))[0];

        Result<PostingReceipt> cost = await _harness.Invoices.PostCostOfSalesAsync(
            tenant,
            Harness.Actor,
            invoice,
            new Babel.Sales.Application.CostOfSalesDraft(invoiceLine, item, Warehouse, "*", 30m),
            token);

        Assert.True(cost.IsSuccess, Describe(cost));

        // ── ٣ · الرقم في الدفتر هو الرقم المحسوب، لا رقمٌ سلّمه أحد ───────────
        string invoiceLineId = invoiceLine.ToString("D", CultureInfo.InvariantCulture);
        decimal relieved = -await DocumentNetOnItemControlAsync(tenant, "SalesInvoiceLine", invoiceLineId, token);

        Proof.Require(
            relieved == 300.0000m,
            "قيد التكلفة حمل 30 وحدة × متوسط 10.000000 — ولا مستدعٍ سلّم هذا الرقم",
            "المُنزَّل من مراقبة المخزون=" + Proof.Money(relieved));

        // ── ٤ · المطابقة: ثلاثة طرق مستقلّة إلى الرقم نفسه ────────────────────
        Result<ControlReconciliationReport> report = await _harness.Valuation
            .ReconcileAsync(tenant, Harness.Actor, AsOf, token);

        Assert.True(report.IsSuccess, Describe(report));

        Proof.Require(
            report.Value.IsReconciled
            && report.Value.Divergences.Count == 0
            && report.Value.Divergence.Amount == 0.0000m
            && report.Value.SubledgerTotal.Amount == report.Value.ControlTotal.Amount
            && report.Value.BalanceTotal.Amount == report.Value.ControlTotal.Amount,
            "دفتر المخزون المساعد ومجموع أرصدته وحسابه الضابط ثلاثتها رقم واحد",
            "الحركات=" + Proof.Money(report.Value.SubledgerTotal.Amount)
            + " · الأرصدة=" + Proof.Money(report.Value.BalanceTotal.Amount)
            + " · نقطة الضبط=" + Proof.Money(report.Value.ControlTotal.Amount)
            + " · الانحراف=" + Proof.Money(report.Value.Divergence.Amount)
            + " · مستندات منحرفة=" + report.Value.Divergences.Count.ToString(CultureInfo.InvariantCulture));

        // ── ٥ · ورصيد الصنف نفسه بعد الدورة ──────────────────────────────────
        Result<StockBalanceView> balance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(balance.IsSuccess, Describe(balance));

        Proof.Require(
            balance.Value.Quantity == 70m && balance.Value.Value.Amount == 700.0000m,
            "رصيد الصنف بعد الاستلام والبيع",
            "الكمية=" + Quantity(balance.Value.Quantity)
            + " والقيمة=" + Proof.Money(balance.Value.Value.Amount)
            + " ومتوسط الوحدة=" + Quantity(balance.Value.UnitCost));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1-ب · فاتورة **بصنفين** — وهي التي كانت لا تُباع
    // ═══════════════════════════════════════════════════════════════════════
    //
    // كان قيد التكلفة بحبيبيّة المستند، فالصنف الثاني يصطدم بهوية الأول ويُرفض
    // بـinventory.movement_identity_conflict. والرفض كان صادقاً — والحدّ حقيقياً:
    // فاتورةٌ بصنفين لا تُباع. وبعد أن صار القيد بحبيبيّة السطر (SalesInvoiceLine)
    // صار لكل سطر هويته، والمطابقة تبقى صفراً لأن **الطرفين** اتّسعا معاً.
    [Fact]
    public async Task فاتورة_بصنفين_تُرحّل_قيدي_تكلفة_والمطابقة_تبقى_صفراً()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.Tenant;
        string first = Harness.Next("ITEM");
        string second = Harness.Next("ITEM");

        // استلامان حقيقيان: 40 × 5 = 200 · 60 × 3 = 180
        await _harness.PostGoodsReceiptAsync(tenant, first, Warehouse, 40m, 5m, March, token);
        await _harness.PostGoodsReceiptAsync(tenant, second, Warehouse, 60m, 3m, March, token);

        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, [700m, 500m], March, token);
        IReadOnlyList<Guid> lines = await _harness.InvoiceLineIdsAsync(tenant, invoice, token);

        Assert.Equal(2, lines.Count);

        Result<PostingReceipt> costOfFirst = await _harness.Invoices.PostCostOfSalesAsync(
            tenant, Harness.Actor, invoice,
            new Babel.Sales.Application.CostOfSalesDraft(lines[0], first, Warehouse, "*", 10m), token);

        Result<PostingReceipt> costOfSecond = await _harness.Invoices.PostCostOfSalesAsync(
            tenant, Harness.Actor, invoice,
            new Babel.Sales.Application.CostOfSalesDraft(lines[1], second, Warehouse, "*", 20m), token);

        Assert.True(costOfFirst.IsSuccess, Describe(costOfFirst));
        Assert.True(costOfSecond.IsSuccess, Describe(costOfSecond));

        decimal relievedFirst = -await DocumentNetOnItemControlAsync(
            tenant, "SalesInvoiceLine", lines[0].ToString("D", CultureInfo.InvariantCulture), token);

        decimal relievedSecond = -await DocumentNetOnItemControlAsync(
            tenant, "SalesInvoiceLine", lines[1].ToString("D", CultureInfo.InvariantCulture), token);

        Proof.Require(
            relievedFirst == 50.0000m && relievedSecond == 60.0000m,
            "صنفان على فاتورة واحدة: قيدان بمبلغين محسوبين (10×5.000000 و20×3.000000)",
            "السطر الأول=" + Proof.Money(relievedFirst) + " · السطر الثاني=" + Proof.Money(relievedSecond));

        // ولا يُلتفّ على الهوية: سطرٌ لا وجود له تحت هذه الفاتورة يُرفض باسمه.
        Result<PostingReceipt> invented = await _harness.Invoices.PostCostOfSalesAsync(
            tenant, Harness.Actor, invoice,
            new Babel.Sales.Application.CostOfSalesDraft(Guid.CreateVersion7(), first, Warehouse, "*", 1m), token);

        Assert.True(invented.IsFailure, "معرّف سطر مخترَع مرّ.");

        Proof.Require(
            invented.Errors[0].Code == "sales.line_not_found"
            && invented.Errors[0].MessageAr.Length > 0
            && invented.Errors[0].MessageEn.Length > 0,
            "ومعرّف سطر مخترَع يُرفض باسمه بلغتين — لا يُقبل مستنداً لا يقابله صفّ",
            invented.Errors[0].Code);

        // والصنف الثاني تحت هوية السطر الأول ما يزال تصادماً يُرفض: الحبيبيّة
        // اتّسعت، ولم يُفتَح باب حركتين تحت هوية واحدة.
        Result<PostingReceipt> collision = await _harness.Invoices.PostCostOfSalesAsync(
            tenant, Harness.Actor, invoice,
            new Babel.Sales.Application.CostOfSalesDraft(lines[0], second, Warehouse, "*", 1m), token);

        Assert.True(collision.IsFailure, "صنفان تحت هوية سطر واحد مرّا.");

        Proof.Require(
            collision.Errors[0].Code == "inventory.movement_identity_conflict",
            "وصنفان تحت هوية سطر واحد ما زالا يُرفضان — الحبيبيّة اتّسعت ولم يسقط الحارس",
            collision.Errors[0].Code);

        Result<ControlReconciliationReport> report = await _harness.Valuation
            .ReconcileAsync(tenant, Harness.Actor, AsOf, token);

        Assert.True(report.IsSuccess, Describe(report));

        Proof.Require(
            report.Value.IsReconciled
            && report.Value.Divergences.Count == 0
            && report.Value.SubledgerTotal.Amount == report.Value.ControlTotal.Amount
            && report.Value.BalanceTotal.Amount == report.Value.ControlTotal.Amount,
            "والمستأجر كلّه ما يزال مطابقاً بثلاثة طرق بعد فاتورة الصنفين",
            "الحركات=" + Proof.Money(report.Value.SubledgerTotal.Amount)
            + " · الأرصدة=" + Proof.Money(report.Value.BalanceTotal.Amount)
            + " · نقطة الضبط=" + Proof.Money(report.Value.ControlTotal.Amount)
            + " · الانحراف=" + Proof.Money(report.Value.Divergence.Amount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1-ج · إشعار دائن يردّ بضاعة — بتكلفة صرفها الأصلي لا بمتوسط اليوم
    // ═══════════════════════════════════════════════════════════════════════
    //
    // sales.credit_note.cost_of_sales كان في المصفوفة ولا يُطلقه شيء. القصة:
    //   استلام  20 × 4 = 80        ⇒ متوسط 4
    //   بيع     10 بتكلفة 40       ⇒ رصيد 10 وقيمة 40
    //   استلام  20 × 10 = 200      ⇒ رصيد 30 وقيمة 240 ومتوسط 8
    //   ردّ      10                 ⇒ **40** لا 80: تكلفة الصرف الأصلي
    [Fact]
    public async Task الإشعار_الدائن_يردّ_البضاعة_بتكلفة_صرفها_الأصلي_والمطابقة_تبقى_صفراً()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.Tenant;
        string item = Harness.Next("ITEM");

        await _harness.PostGoodsReceiptAsync(tenant, item, Warehouse, 20m, 4m, March, token);

        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, 900m, March, token);
        Guid invoiceLine = (await _harness.InvoiceLineIdsAsync(tenant, invoice, token))[0];

        Result<PostingReceipt> cost = await _harness.Invoices.PostCostOfSalesAsync(
            tenant, Harness.Actor, invoice,
            new Babel.Sales.Application.CostOfSalesDraft(invoiceLine, item, Warehouse, "*", 10m), token);

        Assert.True(cost.IsSuccess, Describe(cost));

        // استلام ثانٍ يرفع المتوسط إلى 8 قبل الردّ: 10 وحدات بـ40 + 20 بـ200.
        await _harness.PostGoodsReceiptAsync(tenant, item, Warehouse, 20m, 10m, March, token);

        Result<StockBalanceView> beforeReturn = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(beforeReturn.IsSuccess, Describe(beforeReturn));

        // ── الإشعار الدائن يحمل هوية الصرف الأصلي على سطره ───────────────────
        Result<SalesDocumentView> note = await _harness.CreditNotes.CreateAsync(
            tenant,
            Harness.Actor,
            new CreditNoteDraft(
                Harness.Next("CN"),
                invoice,
                March,
                [
                    new SalesLineDraft(
                        "*",
                        new LocalizedName("مرتجع", "Return"),
                        10m,
                        Harness.Sar(20m),
                        Harness.Sar(0m),
                        "standard",
                        0.15m,
                        invoiceLine),
                ]),
            token);

        Assert.True(note.IsSuccess, Describe(note));

        Result<SalesDocumentView> posted = await _harness.CreditNotes
            .PostAsync(tenant, Harness.Actor, note.Value.Id, token);

        Assert.True(posted.IsSuccess, Describe(posted));

        Result<StockBalanceView> afterReturn = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(afterReturn.IsSuccess, Describe(afterReturn));

        decimal returnedValue = afterReturn.Value.Value.Amount - beforeReturn.Value.Value.Amount;

        Proof.Require(
            beforeReturn.Value.UnitCost == 8.000000m && returnedValue == 40.0000m,
            "المرتجع رجع بـ40.0000 — تكلفة صرفه الأصلي — لا بـ80.0000 وهي عشر وحدات بمتوسط اليوم 8",
            "متوسط اليوم قبل الردّ=" + Quantity(beforeReturn.Value.UnitCost)
            + " · القيمة المضافة بالردّ=" + Proof.Money(returnedValue));

        // والقيد رُحّل بحبيبيّة سطر الإشعار، وأثره على مراقبة المخزون **مدين**.
        Guid creditLine = await CreditNoteLineIdAsync(tenant, note.Value.Id, token);
        decimal control = await DocumentNetOnItemControlAsync(
            tenant, "SalesCreditNoteLine", creditLine.ToString("D", CultureInfo.InvariantCulture), token);

        Proof.Require(
            control == 40.0000m,
            "وقيد sales.credit_note.cost_of_sales رُحّل بالمبلغ نفسه تحت سطر الإشعار",
            "أثر SalesCreditNoteLine على نقطة الضبط=" + Proof.Money(control));

        // ── والإعادة بالهوية نفسها لا تفعل شيئاً ولا تُعدّ خطأ ────────────────
        // ردٌّ **كامل** يُعاد: صفّه نفسه داخل مجموع ما رُدّ على الصرف، فلو سُئل
        // «هل الردّ زائد؟» قبل «هل هو مُسجَّل؟» لعاد 10 + 10 > 10 ورُفض بـ
        // inventory.return_exceeds_issue — رفضٌ لواقعة وقعت مرّة واحدة. وهذا
        // المسار حقيقي: PostAsync يُعاد كلّما سقط ترحيلٌ بعد كتابة الحركة.
        Result<InventoryMovementCost> replay = await _harness.Stock.ReturnAsync(
            new InventoryReturn
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = new InventoryMovementSource(
                    BabelModule.Sales,
                    "SalesCreditNoteLine",
                    creditLine.ToString("D", CultureInfo.InvariantCulture),
                    PostingTrigger.OnApproval.ToString(),
                    1,
                    "sales.credit_note.cost_of_sales"),
                OriginalIssue = new InventoryMovementSource(
                    BabelModule.Sales,
                    "SalesInvoiceLine",
                    invoiceLine.ToString("D", CultureInfo.InvariantCulture),
                    PostingTrigger.OnApproval.ToString(),
                    1,
                    "sales.invoice.cost_of_sales"),
                Quantity = 10m,
                OccurredOn = March,
            },
            token);

        Assert.True(replay.IsSuccess, Describe(replay));

        Result<StockBalanceView> afterReplay = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(afterReplay.IsSuccess, Describe(afterReplay));

        Proof.Require(
            replay.Value.WasAlreadyRecorded
            && replay.Value.Cost.Amount == 40.0000m
            && afterReplay.Value.Value.Amount == afterReturn.Value.Value.Amount
            && afterReplay.Value.Quantity == afterReturn.Value.Quantity,
            "وإعادة الردّ بالهوية نفسها تُعيد الرقم ولا تردّ مرّة ثانية — ولا تُرفض ردّاً زائداً",
            "الإعادة=" + Proof.Money(replay.Value.Cost.Amount)
            + " · مُسجَّلة سلفاً=" + replay.Value.WasAlreadyRecorded.ToString(CultureInfo.InvariantCulture)
            + " · الرصيد بعدها=" + Proof.Money(afterReplay.Value.Value.Amount));

        Result<ControlReconciliationReport> report = await _harness.Valuation
            .ReconcileAsync(tenant, Harness.Actor, AsOf, token);

        Assert.True(report.IsSuccess, Describe(report));

        Proof.Require(
            report.Value.IsReconciled
            && report.Value.Divergences.Count == 0
            && report.Value.SubledgerTotal.Amount == report.Value.ControlTotal.Amount
            && report.Value.BalanceTotal.Amount == report.Value.ControlTotal.Amount,
            "والمستأجر كلّه ما يزال مطابقاً بثلاثة طرق بعد المرتجع وبعد إعادته",
            "الحركات=" + Proof.Money(report.Value.SubledgerTotal.Amount)
            + " · الأرصدة=" + Proof.Money(report.Value.BalanceTotal.Amount)
            + " · نقطة الضبط=" + Proof.Money(report.Value.ControlTotal.Amount)
            + " · الانحراف=" + Proof.Money(report.Value.Divergence.Amount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1-د · إشعار دائن على سطر بلا قيد تكلفة: يُرفض باسمه، ولا يُخترع له رقم
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task ردّ_بضاعة_على_سطر_بلا_قيد_تكلفة_يُرفض_ولا_يُخترع_له_رقم()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.Tenant;

        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, 600m, March, token);
        Guid invoiceLine = (await _harness.InvoiceLineIdsAsync(tenant, invoice, token))[0];

        Result<SalesDocumentView> note = await _harness.CreditNotes.CreateAsync(
            tenant,
            Harness.Actor,
            new CreditNoteDraft(
                Harness.Next("CN"),
                invoice,
                March,
                [
                    new SalesLineDraft(
                        "*",
                        new LocalizedName("مرتجع", "Return"),
                        1m,
                        Harness.Sar(50m),
                        Harness.Sar(0m),
                        "standard",
                        0.15m,
                        invoiceLine),
                ]),
            token);

        Assert.True(note.IsSuccess, Describe(note));

        Result<SalesDocumentView> posted = await _harness.CreditNotes
            .PostAsync(tenant, Harness.Actor, note.Value.Id, token);

        Assert.True(posted.IsFailure, "ردّ بضاعة على سطر لم يُصرَف مرّ.");

        Error refusal = posted.Errors[0];

        Proof.Require(
            refusal.Code == "sales.original_cost_entry_not_found"
            && refusal.MessageAr.Contains("تكلفة صرف", StringComparison.Ordinal)
            && refusal.MessageEn.Contains("original issue cost", StringComparison.Ordinal),
            "الردّ بلا صرفٍ أصلي يُرفض باسمه بالعربية والإنجليزية، ولا يُقيَّم بمتوسط اليوم",
            refusal.Code);

        // والرفض يترك المستند على حاله: لا قيد تجاري رُحّل، ولا حركة كُتبت.
        long entries = await EntryCountAsync(tenant, "SalesCreditNote", note.Value.Id, token);

        Proof.Require(
            entries == 0,
            "ولا قيد واحد رُحّل للإشعار المرفوض — الرفض قبل الكتابة لا بعدها",
            "قيود الإشعار=" + entries.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · بيعٌ لصنف لم يُستلم قط: رفضٌ مكتوب بلغتين، لا رقم مخترَع
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task البيع_بلا_أساس_تكلفة_يُرفض_ولا_يُخترَع_له_رقم()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.NegativeStockTenant;
        string item = Harness.Next("ITEM");

        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, 400m, March, token);

        Result<PostingReceipt> cost = await _harness.Invoices.PostCostOfSalesAsync(
            tenant,
            Harness.Actor,
            invoice,
            new Babel.Sales.Application.CostOfSalesDraft(
                (await _harness.InvoiceLineIdsAsync(tenant, invoice, token))[0], item, Warehouse, "*", 10m),
            token);

        Assert.True(cost.IsFailure, "قيد التكلفة نجح على صنف بلا أساس تكلفة.");

        Error error = cost.Errors[0];

        Proof.Require(
            error.Code == "inventory.no_cost_basis"
            && error.MessageAr.Contains(item, StringComparison.Ordinal)
            && error.MessageEn.Contains(item, StringComparison.Ordinal)
            && error.MessageAr.Contains("استلاماً", StringComparison.Ordinal)
            && error.MessageEn.Contains("receipt", StringComparison.Ordinal),
            "الرفض يُسمّي الصنف وما الذي يجعله يمرّ، بالعربية والإنجليزية",
            error.Code + " · " + error.MessageAr[..Math.Min(70, error.MessageAr.Length)]);

        // ولا حركة كُتبت، ولا قيد رُحّل: الرفض يترك الحالة كما كانت.
        string invoiceId = invoice.ToString("D", CultureInfo.InvariantCulture);
        decimal net = await DocumentNetOnItemControlAsync(tenant, "SalesInvoice", invoiceId, token);

        Proof.Require(net == 0m, "لا أثر على الحساب الضابط بعد الرفض", "الصافي=" + Proof.Money(net));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · البيع على المكشوف ثم التكلفة المتأخّرة — والمطابقة تبقى صفراً بالضبط
    // ═══════════════════════════════════════════════════════════════════════
    //
    // القصة كاملةً بأرقامها:
    //   استلام  10 × 12 = 120      ⇒ رصيد 10 وقيمة 120 ومتوسط 12
    //   بيع     15 × 12 = 180      ⇒ رصيد −5 وقيمة −60      (على المكشوف)
    //   استلام   5 × 14 =  70      ⇒ رصيد  0 وقيمة  10      (التكلفة المتأخّرة)
    //
    // والعشرة الباقية ليست خطأ حساب: هي بالضبط 5 وحدات × (14 − 12) — ما نُقص من
    // تكلفة المبيعات لأن السعر الحقيقي وصل بعد البيع. والقيد المُرحَّل لا يُعاد كتابته
    // (ADR-0002)، فالفارق **يظهر** على كمية صفرية ويمنع الإقفال.
    [Fact]
    public async Task المكشوف_ثم_التكلفة_المتأخّرة_تُبقيان_المطابقة_صفراً_وتمنعان_الإقفال()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.NegativeStockTenant;
        string item = Harness.Next("ITEM");

        // ── ١ · استلام أول: 10 × 12 ──────────────────────────────────────────
        await _harness.PostGoodsReceiptAsync(tenant, item, Warehouse, 10m, 12m, March, token);

        // ── ٢ · بيع 15 وحدة والرصيد 10 ───────────────────────────────────────
        Guid customer = await _harness.CustomerAsync(tenant, token);
        Guid invoice = await _harness.PostedInvoiceAsync(tenant, customer, 500m, March, token);

        Result<PostingReceipt> cost = await _harness.Invoices.PostCostOfSalesAsync(
            tenant,
            Harness.Actor,
            invoice,
            new Babel.Sales.Application.CostOfSalesDraft(
                (await _harness.InvoiceLineIdsAsync(tenant, invoice, token))[0], item, Warehouse, "*", 15m),
            token);

        Assert.True(cost.IsSuccess, Describe(cost));

        Result<StockBalanceView> afterSale = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(afterSale.IsSuccess, Describe(afterSale));

        Proof.Require(
            afterSale.Value.Quantity == -5m && afterSale.Value.Value.Amount == -60.0000m,
            "البيع على المكشوف وقع وسُجّل ولم يُرفض",
            "الكمية=" + Quantity(afterSale.Value.Quantity) + " والقيمة=" + Proof.Money(afterSale.Value.Value.Amount));

        // ── ٣ · استلام متأخّر بسعر أعلى: 5 × 14 ──────────────────────────────
        await _harness.PostGoodsReceiptAsync(tenant, item, Warehouse, 5m, 14m, March, token);

        Result<StockBalanceView> afterLate = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(afterLate.IsSuccess, Describe(afterLate));

        Proof.Require(
            afterLate.Value.Quantity == 0m && afterLate.Value.Value.Amount == 10.0000m,
            "التكلفة المتأخّرة تركت 10.0000 على كمية صفر — وهي 5 وحدات × فرق السعر 2",
            "الكمية=" + Quantity(afterLate.Value.Quantity) + " والقيمة=" + Proof.Money(afterLate.Value.Value.Amount));

        // ── ٤ · والمطابقة **صفر بالضبط** رغم كل ذلك ──────────────────────────
        Result<ControlReconciliationReport> report = await _harness.Valuation
            .ReconcileAsync(tenant, Harness.Actor, AsOf, token);

        Assert.True(report.IsSuccess, Describe(report));

        Proof.Require(
            report.Value.IsReconciled && report.Value.Divergence.Amount == 0.0000m,
            "المكشوف والتكلفة المتأخّرة لا يُنتجان انحرافاً: كلاهما حركةٌ لها قيدها",
            "الحركات=" + Proof.Money(report.Value.SubledgerTotal.Amount)
            + " · نقطة الضبط=" + Proof.Money(report.Value.ControlTotal.Amount)
            + " · الانحراف=" + Proof.Money(report.Value.Divergence.Amount));

        // ── ٥ · والفترة **لا تُقفل**، والرفض يُسمّي الصنف وسببه ───────────────
        Result<IReadOnlyList<CloseObstacle>> readiness = await _harness.Valuation
            .CloseReadinessAsync(tenant, Harness.Actor, "2026-03", token);

        Assert.True(readiness.IsFailure, "الفترة قُفلت فوق قيمةٍ بلا كمية.");

        Error refusal = readiness.Errors[0];

        Proof.Require(
            refusal.Code == "inventory.period_not_closeable"
            && refusal.MessageAr.Contains(item, StringComparison.Ordinal)
            && refusal.MessageAr.Contains(CloseObstacleReason.ValueWithoutQuantity, StringComparison.Ordinal)
            && refusal.MessageEn.Contains(item, StringComparison.Ordinal),
            "إقفال الفترة مرفوض باسمه، ويُسمّي الصنف والسبب بلغتين",
            refusal.Code + " · " + CloseObstacleReason.ValueWithoutQuantity);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · هوية الحركة هي هوية الترحيل — والإعادة تُعيد الرقم، والاختلاف يُرفض
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task الإعادة_بالهوية_نفسها_لا_تفعل_شيئاً_والاختلاف_تحتها_يُرفض()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.ValuationTenant;
        string item = Harness.Next("ITEM");
        string other = Harness.Next("ITEM");

        InventoryMovementSource opening = new(
            BabelModule.Inventory, "OpeningBalance", Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
            PostingTrigger.OnApproval.ToString(), 1, "inventory.count_adjustment.posted");

        Result<InventoryMovementCost> seed = await _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = opening,
                Location = new InventoryItemLocation(item, Warehouse, "*"),
                Quantity = 10m,
                Cost = Harness.Sar(50.0000m),
                OccurredOn = March,
            },
            token);

        Assert.True(seed.IsSuccess, Describe(seed));

        InventoryMovementSource issueSource = new(
            BabelModule.Sales, "SalesInvoice", Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
            PostingTrigger.OnApproval.ToString(), 1, "sales.invoice.cost_of_sales");

        Result<InventoryMovementCost> firstIssue = await IssueAsync(tenant, item, issueSource, 4m, token);
        Assert.True(firstIssue.IsSuccess, Describe(firstIssue));

        // الوصول الثاني بالهوية نفسها وبالمحتوى نفسه: الرقم نفسه، ويقول إنه أُعيد.
        Result<InventoryMovementCost> replay = await IssueAsync(tenant, item, issueSource, 4m, token);
        Assert.True(replay.IsSuccess, Describe(replay));

        Proof.Require(
            firstIssue.Value.Cost.Amount == 20.0000m
            && replay.Value.Cost.Amount == 20.0000m
            && !firstIssue.Value.WasAlreadyRecorded
            && replay.Value.WasAlreadyRecorded,
            "الإحكام مستقلّ عن الترتيب: الإعادة تُعيد الرقم الأول ولا تصرف كميةً ثانية",
            "الأولى=" + Proof.Money(firstIssue.Value.Cost.Amount)
            + " والإعادة=" + Proof.Money(replay.Value.Cost.Amount));

        // صنفٌ آخر تحت الهوية نفسها: تصادمٌ يُرفض باسمه لا يُلتفّ عليه.
        Result<InventoryMovementCost> conflict = await IssueAsync(tenant, other, issueSource, 4m, token);

        Assert.True(conflict.IsFailure, "حركة لصنف آخر مرّت تحت هوية مستند سبق تسجيله.");

        Proof.Require(
            conflict.Errors[0].Code == "inventory.movement_identity_conflict"
            && conflict.Errors[0].MessageAr.Contains(item, StringComparison.Ordinal)
            && conflict.Errors[0].MessageAr.Contains(other, StringComparison.Ordinal),
            "مستندٌ بصنفين تحت هوية واحدة يُرفض ويُسمّي الصنفين",
            conflict.Errors[0].Code);

        // وكميةٌ مختلفة تحت الهوية نفسها: تصحيحٌ، والتصحيح بعكسٍ ثم بجيل تالٍ.
        Result<InventoryMovementCost> quantityConflict = await IssueAsync(tenant, item, issueSource, 7m, token);

        Assert.True(quantityConflict.IsFailure, "كمية مختلفة مرّت تحت الهوية نفسها.");

        Proof.Require(
            quantityConflict.Errors[0].Code == "inventory.movement_quantity_conflict",
            "كمية مختلفة تحت الهوية نفسها تُرفض ولا تُصرَف مرّتين",
            quantityConflict.Errors[0].Code);

        // والرصيد لم يتحرّك إلا بالصرف الأول.
        Result<StockBalanceView> balance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, token);

        Assert.True(balance.IsSuccess, Describe(balance));

        Proof.Require(
            balance.Value.Quantity == 6m && balance.Value.Value.Amount == 30.0000m,
            "الرصيد تحرّك مرّة واحدة رغم أربع محاولات",
            "الكمية=" + Quantity(balance.Value.Quantity) + " والقيمة=" + Proof.Money(balance.Value.Value.Amount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · المرتجع بتكلفة صرفه الأصلي لا بتكلفة اليوم
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task المرتجع_يُقيَّم_بتكلفة_الصرف_الأصلي_لا_بمتوسط_اليوم()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.ValuationTenant;
        string item = Harness.Next("ITEM");

        // ‏10 وحدات بخمسة ريالات ⇒ متوسط 5.
        await SeedAsync(tenant, item, 10m, 50.0000m, token);

        InventoryMovementSource issueSource = Source(BabelModule.Sales, "SalesInvoice", "sales.invoice.cost_of_sales");
        Result<InventoryMovementCost> issue = await IssueAsync(tenant, item, issueSource, 4m, token);
        Assert.True(issue.IsSuccess, Describe(issue));

        // ثم استلام يرفع المتوسط: 6 وحدات بقيمة 30 + 10 وحدات بـ150 ⇒ متوسط 11.25.
        await SeedAsync(tenant, item, 10m, 150.0000m, token);

        Result<InventoryMovementCost> returned = await _harness.Stock.ReturnAsync(
            new InventoryReturn
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source(BabelModule.Sales, "SalesCreditNote", "sales.credit_note.cost_of_sales"),
                OriginalIssue = issueSource,
                Quantity = 4m,
                OccurredOn = March,
            },
            token);

        Assert.True(returned.IsSuccess, Describe(returned));

        Proof.Require(
            returned.Value.Cost.Amount == 20.0000m,
            "المرتجع رجع بـ20.0000 — تكلفة صرفه الأصلي — لا بـ45.0000 وهي أربع وحدات بمتوسط اليوم",
            "المرتجع=" + Proof.Money(returned.Value.Cost.Amount)
            + " ومتوسط اليوم قبل الردّ=11.250000");

        // وردٌّ ثانٍ على الصرف نفسه يتجاوز ما صُرف: يُرفض.
        Result<InventoryMovementCost> excess = await _harness.Stock.ReturnAsync(
            new InventoryReturn
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source(BabelModule.Sales, "SalesCreditNote", "sales.credit_note.cost_of_sales"),
                OriginalIssue = issueSource,
                Quantity = 1m,
                OccurredOn = March,
            },
            token);

        Assert.True(excess.IsFailure, "رُدّ أكثر ممّا صُرف.");

        Proof.Require(
            excess.Errors[0].Code == "inventory.return_exceeds_issue",
            "الردّ الزائد يُرفض باسمه",
            excess.Errors[0].Code);
    }

    // ────────────────────────────────────────────────────────────────────────

    private async Task SeedAsync(TenantId tenant, string item, decimal quantity, decimal cost, CancellationToken token)
    {
        Result<InventoryMovementCost> seeded = await _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source(BabelModule.Inventory, "OpeningBalance", "inventory.count_adjustment.posted"),
                Location = new InventoryItemLocation(item, Warehouse, "*"),
                Quantity = quantity,
                Cost = Harness.Sar(cost),
                OccurredOn = March,
            },
            token);

        Assert.True(seeded.IsSuccess, Describe(seeded));
    }

    private Task<Result<InventoryMovementCost>> IssueAsync(
        TenantId tenant, string item, InventoryMovementSource source, decimal quantity, CancellationToken token)
        => _harness.Stock.IssueAsync(
            new InventoryIssue
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = source,
                Location = new InventoryItemLocation(item, Warehouse, "*"),
                Quantity = quantity,
                OccurredOn = March,
            },
            token).AsTask();

    private static InventoryMovementSource Source(BabelModule module, string documentType, string eventCode) => new(
        module,
        documentType,
        Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
        PostingTrigger.OnApproval.ToString(),
        1,
        eventCode);

    /// <summary>صافي حركة مستند بعينه على الحساب الضابط للمخزون، مقروءاً من الدفتر.</summary>
    private static async Task<decimal> DocumentNetOnItemControlAsync(
        TenantId tenant, string documentType, string documentId, CancellationToken token)
    {
        await using Npgsql.NpgsqlConnection connection = new(InventoryTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token);

        await using Npgsql.NpgsqlCommand command = new(
            """
            select coalesce(sum(l.debit_company - l.credit_company), 0)
              from ledger.journal_line l
              join ledger.journal_entry e on e.entry_id = l.entry_id
             where l.company_id = $1 and l.subledger_kind = 'item'
               and e.source_doc_type = $2 and e.source_doc_id = $3
            """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(documentType);
        command.Parameters.AddWithValue(documentId);

        return (decimal)(await command.ExecuteScalarAsync(token))!;
    }

    /// <summary>معرّف أول سطر في إشعار دائن — معرّف مستند قيد تكلفة مرتجعه.</summary>
    private static async Task<Guid> CreditNoteLineIdAsync(TenantId tenant, Guid noteId, CancellationToken token)
    {
        await using Npgsql.NpgsqlConnection connection = new(InventoryTestEnvironment.Sales.ConnectionString);
        await connection.OpenAsync(token);

        await using Npgsql.NpgsqlCommand command = new(
            """
            select "Id" from sales.sales_line
             where "TenantId" = $1 and "OwnerType" = 'CREDIT_NOTE' and "OwnerId" = $2
             order by "LineNo" limit 1
            """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(noteId);
        return (Guid)(await command.ExecuteScalarAsync(token))!;
    }

    /// <summary>عدد القيود المكتوبة لمستند بعينه.</summary>
    private static async Task<long> EntryCountAsync(
        TenantId tenant, string documentType, Guid documentId, CancellationToken token)
    {
        await using Npgsql.NpgsqlConnection connection = new(InventoryTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token);

        await using Npgsql.NpgsqlCommand command = new(
            """
            select count(*) from ledger.journal_entry
             where company_id = $1 and source_doc_type = $2 and source_doc_id = $3
            """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        command.Parameters.AddWithValue(documentType);
        command.Parameters.AddWithValue(documentId.ToString("D", CultureInfo.InvariantCulture));
        return (long)(await command.ExecuteScalarAsync(token))!;
    }

    private static string Quantity(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.ToString()));
}
