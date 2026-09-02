using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Inventory.Application;
using Babel.Inventory.Subledger;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>الشاهد السلبي: لا مفتاح خارجي يربط الدفتر المساعد بالكتالوج — ويجب ألّا يوجد.</b>
/// <para>
/// <b>ولماذا شاهدٌ لا اطمئنان</b> (‏ADR-0056): «لم أُنشئ مفتاحاً خارجياً» جملةٌ عن
/// نيّة الكاتب لا عن حال القاعدة. وقيدٌ يتسلّل — من هجرة، أو من نموذج EF بعلاقةٍ
/// مُستنتَجة — لا يُسقط شيئاً في اليوم الذي يُضاف فيه: يُسقط <b>ترقية قاعدة عميل</b>
/// بعد شهور، على خطأٍ إملائي في صفٍّ تاريخي لا شيء يُصلحه على جدولٍ يُضاف إليه فقط.
/// </para>
/// <para>
/// <b>والمشهد المقيس هنا واقعي لا مفتعل:</b> استلام المشتريات يكتب حركة مخزون
/// بمستودعٍ يأتي من أمر الشراء، و<b>لا يمرّ بالكتالوج</b>. فمنشأةٌ تستلم على
/// «WH-GHOST» تُنتج رصيداً حقيقياً في مستودعٍ لا يعرفه الكتالوج — ويجب أن يُقرأ،
/// وأن يُطابَق بحسابه الضابط بثلاثة طرق، وأن تُقفَل فترته.
/// </para>
/// <para>
/// <b>وحدُّ هذه الدفعة يُقاس في الجملة نفسها:</b> البوّابة عند <b>إنشاء المسوّدة</b>
/// وحدها. فالمستودع نفسه الذي قَبِله الدفتر المساعد يُرفض على باب المسوّدة —
/// وذلك <b>نقصٌ مُعلَن</b> لا سهو: فحصٌ عند الترحيل كان يترك مستند استلامٍ مكتوباً
/// عالقاً بلا مخرج، وقيدٌ في القاعدة كان يُسقط الترقية على تاريخٍ لا يُصلَح.
/// </para>
/// </summary>
[Collection("inventory")]
public sealed class NoForeignKeyBindsTheLedgerToTheCatalogueTests : IAsyncLifetime
{
    private static readonly DateOnly May = new(2026, 5, 14);
    private const string AsOf = "2026-05-31";

    /// <summary>مستودعٌ لا يُسجَّل في الكتالوج أبداً في هذا الإثبات — وهذا هو المقيس.</summary>
    private const string Ghost = "WH-GHOST";

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task حركةٌ_في_مستودعٍ_لا_يعرفه_الكتالوج_تُقرأ_وتُطابَق_وتُقفَل()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UncataloguedTenant;
        string item = Harness.Next("ITEM");

        // ── ١ · الكتالوج فارغ، ولا صفّ فيه بهذا الرمز ────────────────────────
        Result<IReadOnlyList<WarehouseView>> catalogue =
            await _harness.Places.ListWarehousesAsync(tenant, Harness.Actor, token);

        Assert.True(catalogue.IsSuccess, Describe(catalogue));

        Proof.Require(
            !catalogue.Value.Any(row => string.Equals(row.Code, Ghost, StringComparison.Ordinal)),
            "الكتالوج لا يعرف «" + Ghost + "» — وهذا هو الشرط الذي يجعل ما يليه شاهداً",
            "مستودعات مسجَّلة=" + catalogue.Value.Count.ToString(CultureInfo.InvariantCulture));

        // ── ٢ · واستلام مشترياتٍ حقيقي يكتب عليه حركةً وقيداً ────────────────
        //     مسار الإنتاج نفسه: أمر شراء، ثم استلام، ثم ترحيله. ولا نداء يدوي
        //     على الدفتر المساعد، ولا كتابة في جدول.
        ReceiptFacts receipt = await _harness.PostGoodsReceiptAsync(
            tenant, item, Ghost, 40m, 25m, May, token);

        Assert.NotEqual(Guid.Empty, receipt.ReceiptId);

        // ── ٣ · الرصيد يُقرأ كاملاً ─────────────────────────────────────────
        Result<StockBalanceView> balance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Ghost, InventoryLocations.Default, token);

        Assert.True(balance.IsSuccess, Describe(balance));

        Proof.Require(
            balance.Value.Quantity.Magnitude == 40m
            && balance.Value.Value.Amount == 1_000.0000m
            && balance.Value.HasCostBasis,
            "الرصيد في مستودعٍ لا يعرفه الكتالوج يُقرأ كاملاً — لا مفتاح خارجي يمنع كتابته ولا قراءته",
            Quantity(balance.Value.Quantity.Magnitude) + " / " + Proof.Money(balance.Value.Value.Amount));

        // ── ٤ · والمطابقة الثلاثية تصحّ بالضبط ──────────────────────────────
        Result<ControlReconciliationReport> report = await _harness.Valuation
            .ReconcileAsync(tenant, Harness.Actor, DateOnly.Parse(AsOf, CultureInfo.InvariantCulture), token);

        Assert.True(report.IsSuccess, Describe(report));

        Proof.Require(
            report.Value.IsReconciled
            && report.Value.Divergence.Amount == 0.0000m
            && report.Value.Divergences.Count == 0
            && report.Value.SubledgerTotal.Amount == 1_000.0000m
            && report.Value.BalanceTotal.Amount == 1_000.0000m
            && report.Value.ControlTotal.Amount == 1_000.0000m,
            "والطرق الثلاث تصل إلى الرقم نفسه — الكتالوج ليس طرفاً في المطابقة ولا شرطاً لها",
            "دفتر=" + Proof.Money(report.Value.SubledgerTotal.Amount)
            + " · أرصدة=" + Proof.Money(report.Value.BalanceTotal.Amount)
            + " · ضبط=" + Proof.Money(report.Value.ControlTotal.Amount)
            + " · فارق=" + Proof.Money(report.Value.Divergence.Amount));

        // ── ٥ · والفترة تُقفل ───────────────────────────────────────────────
        Result<IReadOnlyList<CloseObstacle>> readiness = await _harness.Valuation
            .CloseReadinessAsync(tenant, Harness.Actor, "2026-05", token);

        Assert.True(readiness.IsSuccess, Describe(readiness));

        Proof.Require(
            readiness.Value.Count == 0,
            "والفترة تُقفل: «مستودعٌ ليس في الكتالوج» ليس عائق إقفال، ولا يجوز أن يصير واحداً بأثرٍ رجعي",
            "عوائق=" + readiness.Value.Count.ToString(CultureInfo.InvariantCulture));

        // ── ٦ · وحدُّ الدفعة: البوّابة على باب المسوّدة وحده ─────────────────
        Result<StockDocumentView> refused = await _harness.StockDocuments.CreateAsync(
            tenant,
            Harness.Actor,
            new StockDocumentDraft(
                Harness.Next("STK"),
                "IN",
                item,
                Ghost,
                InventoryLocations.Default,
                "*",
                new InventoryQuantity(1m, InventoryUnits.Each),
                Harness.Sar(10m),
                May),
            token);

        Assert.True(refused.IsFailure, "مسوّدة على مستودعٍ لا يعرفه الكتالوج مرّت.");

        Proof.Require(
            refused.Errors[0].Code == "inventory.warehouse_not_found",
            "والمستودع نفسه الذي قَبِله الدفتر المساعد يُرفض على باب المسوّدة — الحدُّ عند الإنشاء، **مُعلَناً** لا مسكوتاً عنه",
            refused.Errors[0].Code);
    }

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static e => e.ToString()));

    private static string Quantity(decimal value)
        => value.ToString("0.000000", CultureInfo.InvariantCulture);
}
