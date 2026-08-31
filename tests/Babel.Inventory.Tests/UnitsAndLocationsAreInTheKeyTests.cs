using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Inventory.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>وحدة القياس جزءٌ من كل كمّية، والموقع ضلعٌ في مفتاح الرصيد.</b>
/// <para>
/// وما تُثبته هذه المجموعة ليس أن الحقلين موجودان — ذلك يراه المصرّف — بل أن
/// <b>خلط وحدتين بلا معامل يُرفض</b>، وأن <b>تحويلاً لا يقع بلا باقٍ يُرفض ولا
/// يُقرَّب</b>، وأن <b>موقعين في مستودعٍ واحد رصيدان لا رصيد</b>. وهذه ثلاثة أعطال
/// تفشل بصمت لو لم تُمنع: القيد المبنيّ عليها متوازن تماماً.
/// </para>
/// </summary>
[Collection("inventory")]
public sealed class UnitsAndLocationsAreInTheKeyTests : IAsyncLifetime
{
    private static readonly DateOnly March = new(2026, 3, 10);
    private const string Warehouse = "WH-UOM";
    private const string Piece = "EA";
    private const string Carton = "CTN";
    private const string Box = "BOX";

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · الكرتون يدخل الرصيد اثنتي عشرة حبّة — والمعامل نسبةٌ لا عدد عائم
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task الوحدة_الأكبر_تُحوَّل_بمعاملها_والرصيد_يُمسَك_بوحدة_أساس_واحدة()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitsTenant;
        string item = Harness.Next("ITEM");

        await RegisterAsync(tenant, item, token);

        // ── ٢ كرتون بـ240 ريالاً ⇒ 24 حبّة، ومتوسط الحبّة 10 ────────────────
        Result<InventoryMovementCost> received = await ReceiveAsync(
            tenant, item, Location(), new InventoryQuantity(2m, Carton), 240.0000m, token);

        Assert.True(received.IsSuccess, Describe(received));

        Proof.Require(
            received.Value.Quantity.Magnitude == 24m
            && UnitConversion.SameUnit(received.Value.Quantity.Unit, Piece),
            "الكرتونان دخلا الرصيد أربعاً وعشرين حبّة — بمعامل 12/1 لا بتقريب",
            "المُسجَّل=" + Quantity(received.Value.Quantity.Magnitude) + " " + received.Value.Quantity.Unit);

        // ── والصرف بالحبّة على الرصيد نفسه: وحدة الأساس واحدة ────────────────
        Result<InventoryMovementCost> issued = await IssueAsync(
            tenant, item, Location(), new InventoryQuantity(6m, Piece), token);

        Assert.True(issued.IsSuccess, Describe(issued));

        Proof.Require(
            issued.Value.Cost.Amount == 60.0000m,
            "وستّ حبّات صُرفت بستّين — 6 × متوسط 10.000000، لا بستّ وحدات من الكرتون",
            "تكلفة الصرف=" + Proof.Money(issued.Value.Cost.Amount));

        Result<StockBalanceView> balance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, InventoryLocations.Default, token);

        Assert.True(balance.IsSuccess, Describe(balance));

        Proof.Require(
            balance.Value.Quantity.Magnitude == 18m
            && UnitConversion.SameUnit(balance.Value.Quantity.Unit, Piece)
            && balance.Value.Value.Amount == 180.0000m,
            "والرصيد ثماني عشرة حبّة بمئة وثمانين — مقياسٌ واحد للحركتين",
            "الرصيد=" + Quantity(balance.Value.Quantity.Magnitude) + " " + balance.Value.Quantity.Unit
            + " / " + Proof.Money(balance.Value.Value.Amount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · وحدة بلا معامل تُرفض باسمها — ولا تُقرَّب ولا تُفترض
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task وحدة_بلا_معامل_تُرفض_باسمها_ولا_تُخلَط_بالرصيد()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitsTenant;
        string item = Harness.Next("ITEM");

        await RegisterAsync(tenant, item, token);

        Result<InventoryMovementCost> seeded = await ReceiveAsync(
            tenant, item, Location(), new InventoryQuantity(10m, Piece), 100.0000m, token);

        Assert.True(seeded.IsSuccess, Describe(seeded));

        // ‏PAL وحدة لا معامل لها على هذا الصنف: لا تحويل، فلا حركة.
        Result<InventoryMovementCost> unknown = await ReceiveAsync(
            tenant, item, Location(), new InventoryQuantity(1m, "PAL"), 500.0000m, token);

        Assert.True(unknown.IsFailure, "وحدة بلا معامل مرّت وخُلطت بالرصيد.");

        Proof.Require(
            unknown.Errors[0].Code == "inventory.unit_not_convertible"
            && unknown.Errors[0].MessageAr.Contains("PAL", StringComparison.Ordinal)
            && unknown.Errors[0].MessageEn.Length > 0,
            "الوحدة المجهولة تُرفض باسمها وبلغتين، وتُسمّي وحدة الأساس التي لا معامل إليها",
            unknown.Errors[0].Code);

        // ── والكمّية بلا وحدة أصلاً تُرفض قبل أي حساب ────────────────────────
        Result<InventoryMovementCost> bare = await ReceiveAsync(
            tenant, item, Location(), new InventoryQuantity(3m, string.Empty), 30.0000m, token);

        Assert.True(bare.IsFailure, "كمّية بلا وحدة مرّت.");

        Proof.Require(
            bare.Errors[0].Code == "inventory.unit_missing",
            "وكمّية بلا وحدة ليست كمّية — تُرفض قبل أن تبلغ حساباً",
            bare.Errors[0].Code);

        // والرصيد لم يتحرّك بأيٍّ من الرفضين.
        Result<StockBalanceView> balance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, InventoryLocations.Default, token);

        Assert.True(balance.IsSuccess, Describe(balance));

        Proof.Require(
            balance.Value.Quantity.Magnitude == 10m && balance.Value.Value.Amount == 100.0000m,
            "والرفض ترك الحالة كما كانت — لا نصف حركة",
            "الرصيد=" + Quantity(balance.Value.Quantity.Magnitude) + " / " + Proof.Money(balance.Value.Value.Amount));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · التحويل الذي لا يقع بلا باقٍ يُرفض — والمعامل نسبةٌ لا عدد عائم
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task التحويل_غير_التام_يُرفض_ولا_يُقرَّب()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitsTenant;
        string item = Harness.Next("ITEM");

        // «العلبة ثلاث حبّات» ⇒ والحبّة ثلث علبة. المعامل هنا معكوس عمداً: نسجّل
        // أن الوحدة `BOX` تساوي **حبّةً واحدة من كل ثلاث** — أي 1/3 — كي يقع
        // التحويل بلا باقٍ على المضاعفات وحدها.
        Result<ItemView> registered = await _harness.Items.CreateAsync(
            tenant,
            Harness.Actor,
            new ItemDraft(
                item,
                new LocalizedName("صنف بمعامل كسري", "Item with a fractional factor"),
                "*",
                Piece,
                [new ItemUnitDraft(Box, 1L, 3L)]),
            token);

        Assert.True(registered.IsSuccess, Describe(registered));

        // ‏3 علب × (1/3) = 1 حبّة بالضبط ⇒ يمرّ.
        Result<InventoryMovementCost> exact = await ReceiveAsync(
            tenant, item, Location(), new InventoryQuantity(3m, Box), 30.0000m, token);

        Assert.True(exact.IsSuccess, Describe(exact));

        Proof.Require(
            exact.Value.Quantity.Magnitude == 1m,
            "ثلاث علب بمعامل 1/3 تساوي حبّةً واحدة بالضبط — فيمرّ التحويل",
            "المُسجَّل=" + Quantity(exact.Value.Quantity.Magnitude) + " " + exact.Value.Quantity.Unit);

        // ‏1 علبة × (1/3) = 0.333… ⇒ لا يقع بلا باقٍ، فيُرفض ولا يُقرَّب.
        Result<InventoryMovementCost> inexact = await ReceiveAsync(
            tenant, item, Location(), new InventoryQuantity(1m, Box), 10.0000m, token);

        Assert.True(inexact.IsFailure, "تحويل غير تامّ مرّ فقُرِّب في الخفاء.");

        Proof.Require(
            inexact.Errors[0].Code == "inventory.unit_conversion_not_exact",
            "والتحويل غير التامّ يُرفض باسمه — التقريب في كمّية تُضرب في تكلفة الوحدة يصل إلى المال",
            inexact.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · موقعان في مستودعٍ واحد: رصيدان لا رصيد
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task الموقع_ضلعٌ_في_المفتاح_فموقعان_رصيدان_مستقلّان()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitsTenant;
        string item = Harness.Next("ITEM");

        await RegisterAsync(tenant, item, token);

        Result<InventoryMovementCost> shelfA = await ReceiveAsync(
            tenant, item, Location("A-01"), new InventoryQuantity(10m, Piece), 100.0000m, token);

        Result<InventoryMovementCost> shelfB = await ReceiveAsync(
            tenant, item, Location("B-02"), new InventoryQuantity(4m, Piece), 80.0000m, token);

        Assert.True(shelfA.IsSuccess, Describe(shelfA));
        Assert.True(shelfB.IsSuccess, Describe(shelfB));

        Result<StockBalanceView> a = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, "A-01", token);

        Result<StockBalanceView> b = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, Warehouse, "B-02", token);

        Assert.True(a.IsSuccess, Describe(a));
        Assert.True(b.IsSuccess, Describe(b));

        Proof.Require(
            a.Value.Quantity.Magnitude == 10m && a.Value.UnitCost == 10.000000m
            && b.Value.Quantity.Magnitude == 4m && b.Value.UnitCost == 20.000000m,
            "الموقعان رصيدان مستقلّان بمتوسطين مختلفين — ولو كان الموقع وصفاً لاختلطا في متوسط واحد",
            "A-01=" + Quantity(a.Value.Quantity.Magnitude) + " بمتوسط " + Quantity(a.Value.UnitCost)
            + " · B-02=" + Quantity(b.Value.Quantity.Magnitude) + " بمتوسط " + Quantity(b.Value.UnitCost));

        // والصرف من موقعٍ فارغ يُرفض بلا أساس تكلفة، ولو كان الصنف مملوءاً في موقع آخر.
        Result<InventoryMovementCost> elsewhere = await IssueAsync(
            tenant, item, Location("C-03"), new InventoryQuantity(1m, Piece), token);

        Assert.True(elsewhere.IsFailure, "صرفٌ من موقع لم يرد إليه شيء مرّ.");

        Proof.Require(
            elsewhere.Errors[0].Code == "inventory.no_cost_basis",
            "والصرف من موقعٍ لم يرد إليه شيء يُرفض — الرصيد بالموقع لا بالمستودع",
            elsewhere.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · الصرف بما يتجاوز الرصيد: **يمرّ ويُوسَم** ما دام هناك أساس تكلفة
    // ═══════════════════════════════════════════════════════════════════════
    //
    // القرار مكتوب في ADR-0039 §3.1 وفي قرار هذا التسليم: بيعُ بضاعة قبل إدخال
    // استلامها واقعةٌ يومية في منشأة عاملة لا حالة خطأ، ومنعُها يمنع تسجيل الواقع
    // فيلتفّ عليه المستخدم بمستندٍ مخترَع. فيمرّ ويُوسَم `DrewOnNegativeStock`،
    // **ويمنع إقفال الفترة** — وهو الثمن المُعلَن.
    [Fact]
    public async Task الصرف_بما_يتجاوز_الرصيد_يمرّ_ويُوسَم_ويمنع_الإقفال()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.UnitsTenant;
        string item = Harness.Next("ITEM");

        await RegisterAsync(tenant, item, token);

        Result<InventoryMovementCost> seeded = await ReceiveAsync(
            tenant, item, Location(), new InventoryQuantity(5m, Piece), 50.0000m, token);

        Assert.True(seeded.IsSuccess, Describe(seeded));

        Result<InventoryMovementCost> overdrawn = await IssueAsync(
            tenant, item, Location(), new InventoryQuantity(8m, Piece), token);

        Assert.True(overdrawn.IsSuccess, Describe(overdrawn));

        Proof.Require(
            overdrawn.Value.DrewOnNegativeStock
            && overdrawn.Value.Cost.Amount == 80.0000m
            && overdrawn.Value.QuantityAfter.Magnitude == -3m,
            "الصرف على المكشوف يمرّ بمتوسط اللحظة ويُوسَم — ثمان وحدات × 10.000000",
            "التكلفة=" + Proof.Money(overdrawn.Value.Cost.Amount)
            + " · الرصيد بعده=" + Quantity(overdrawn.Value.QuantityAfter.Magnitude)
            + " · موسومة=" + overdrawn.Value.DrewOnNegativeStock.ToString(CultureInfo.InvariantCulture));

        Result<IReadOnlyList<Babel.Inventory.Subledger.CloseObstacle>> readiness = await _harness.Valuation
            .CloseReadinessAsync(tenant, Harness.Actor, "2026-03", token);

        Assert.True(readiness.IsFailure, "الفترة أُعلنت قابلة للإقفال فوق كمّية سالبة.");

        Proof.Require(
            readiness.Errors[0].Code == "inventory.period_not_closeable"
            && readiness.Errors[0].MessageAr.Contains(item, StringComparison.Ordinal),
            "والفترة لا تُقفَل عليه، والرفض يُسمّي الصنف",
            readiness.Errors[0].Code);
    }

    // ────────────────────────────────────────────────────────────────────────

    private async Task RegisterAsync(TenantId tenant, string item, CancellationToken token)
    {
        Result<ItemView> registered = await _harness.Items.CreateAsync(
            tenant,
            Harness.Actor,
            new ItemDraft(
                item,
                new LocalizedName("صنف بوحدات", "Item with units"),
                "*",
                Piece,
                [new ItemUnitDraft(Carton, 12L, 1L)]),
            token);

        Assert.True(registered.IsSuccess, Describe(registered));
    }

    private static InventoryItemLocation Location(string location = InventoryLocations.Default) =>
        new(string.Empty, Warehouse, location, "*");

    private Task<Result<InventoryMovementCost>> ReceiveAsync(
        TenantId tenant,
        string item,
        InventoryItemLocation location,
        InventoryQuantity quantity,
        decimal cost,
        CancellationToken token)
        => _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source("OpeningBalance", "inventory.count_adjustment.posted"),
                Location = location with { ItemId = item },
                Quantity = quantity,
                Cost = Harness.Sar(cost),
                OccurredOn = March,
            },
            token).AsTask();

    private Task<Result<InventoryMovementCost>> IssueAsync(
        TenantId tenant,
        string item,
        InventoryItemLocation location,
        InventoryQuantity quantity,
        CancellationToken token)
        => _harness.Stock.IssueAsync(
            new InventoryIssue
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source("SalesInvoiceLine", "sales.invoice.cost_of_sales"),
                Location = location with { ItemId = item },
                Quantity = quantity,
                OccurredOn = March,
            },
            token).AsTask();

    private static InventoryMovementSource Source(string documentType, string eventCode) => new(
        BabelModule.Inventory,
        documentType,
        Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
        PostingTrigger.OnApproval.ToString(),
        1,
        eventCode);

    private static string Quantity(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.ToString()));
}
