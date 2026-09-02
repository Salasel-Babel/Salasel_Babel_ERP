using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Inventory.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>للصنف دورة حياة، وليست حذفاً — والتعطيل يخالف عمداً حكم موضع التسكين.</b>
/// <para>
/// وما يُثبته هذا الملفّ ثلاثة أشياء، آخرُها هو الأدقّ:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>الرمز لا يُعدَّل، ووحدة الأساس تُقفَل بالتاريخ لا بالمبدأ</b>: تتغيّر ما لم
///     تُكتب حركة، وتُرفض بعدها باسمها.
///   </description></item>
///   <item><description>
///     <b>الصنف يُعطَّل وله رصيد</b> — بخلاف الموضع. والجواب يحمل ما بقي، فلا يظنّ أحدٌ
///     أن البضاعة ذهبت مع الإيقاف.
///   </description></item>
///   <item><description>
///     <b>والوارد وحده يُمنع بعده، والصادر وعكسُ ما مضى يعملان.</b> ولو مُنع كلّ شيء
///     لصار الإيقاف يُجمّد أخطاءً لا يمكن ردّها — وهو أسوأ من ألّا يوجد إيقاف.
///   </description></item>
/// </list>
/// </summary>
[Collection("inventory")]
public sealed class TheItemHasALifecycleTests : IAsyncLifetime
{
    private static readonly DateOnly June = new(2026, 6, 8);
    private const string Piece = "EA";
    private const string Carton = "CTN";
    private const string Warehouse = "WH-LIFE";

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · التعديل يمسّ الاسم والمجموعة والوحدات — ووحدة الأساس تُقفَل بالتاريخ
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task التعديل_يغيّر_الاسم_والمجموعة_والوحدات_ووحدةُ_الأساس_تُقفَل_بالحركة()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.ItemLifecycleTenant;
        string code = Harness.Next("ITEM");

        Result<ItemView> created = await RegisterAsync(tenant, code, "صنفٌ باسمٍ أول", "*", Piece, token);
        Assert.True(created.IsSuccess, Describe(created));

        // ── قبل أي حركة: وحدة الأساس تتغيّر، وهو تصحيح تعريفٍ لا إعادة كتابة واقعة ─
        Result<ItemView> rebased = await _harness.Items.ReviseAsync(
            tenant,
            Harness.Actor,
            created.Value.Id,
            new ItemRevisionDraft(
                new LocalizedName("صنفٌ باسمٍ مصحَّح", "Corrected item"),
                "spare_parts",
                "BOX",
                [new ItemUnitDraft(Carton, 6L, 1L)]),
            token);

        Assert.True(rebased.IsSuccess, Describe(rebased));

        Proof.Require(
            string.Equals(rebased.Value.Code, code, StringComparison.Ordinal)
            && rebased.Value.Name.Arabic == "صنفٌ باسمٍ مصحَّح"
            && string.Equals(rebased.Value.ItemGroup, "spare_parts", StringComparison.Ordinal)
            && string.Equals(rebased.Value.BaseUnit, "BOX", StringComparison.Ordinal),
            "التعديل يغيّر الاسم والمجموعة ووحدة الأساس ولا يمسّ الرمز — وصنفٌ لم يتحرّك تصحيحُه تصحيحُ تعريف",
            "الرمز=" + rebased.Value.Code + " · المجموعة=" + rebased.Value.ItemGroup
            + " · الأساس=" + rebased.Value.BaseUnit);

        Proof.Require(
            rebased.Value.Units.Count == 1
            && rebased.Value.Units[0].Numerator == 6L,
            "والوحدات الجديدة تحلّ محلّ القائمة السابقة كلّها",
            "المعامل=" + rebased.Value.Units[0].UnitCode + " "
            + rebased.Value.Units[0].Numerator.ToString(CultureInfo.InvariantCulture)
            + "/" + rebased.Value.Units[0].Denominator.ToString(CultureInfo.InvariantCulture));

        // ── ثم تُكتب حركة، فتُقفَل وحدة الأساس ──────────────────────────────
        Result<InventoryMovementCost> received = await ReceiveAsync(tenant, code, new InventoryQuantity(10m, "BOX"), 100.0000m, token);
        Assert.True(received.IsSuccess, Describe(received));

        Result<ItemView> locked = await _harness.Items.ReviseAsync(
            tenant,
            Harness.Actor,
            created.Value.Id,
            new ItemRevisionDraft(
                new LocalizedName("صنفٌ باسمٍ مصحَّح", "Corrected item"),
                "spare_parts",
                Piece,
                []),
            token);

        Assert.True(locked.IsFailure, "تغيّرت وحدة أساس صنفٍ كُتبت عليه حركات، فصار مجموع حركاته جمعَ مقاييس.");

        Proof.Require(
            locked.Errors[0].Code == "inventory.base_unit_locked_by_history"
            && locked.Errors[0].MessageAr.Contains(code, StringComparison.Ordinal),
            "ووحدة الأساس تُقفَل بالتاريخ لا بالمبدأ — والرفض يُسمّي الصنف وعدد حركاته",
            locked.Errors[0].Code);

        // ── والمجموعة تبقى قابلة للتغيير بعد الحركة: كل حركة تحمل مجموعتها ──
        Result<ItemView> regrouped = await _harness.Items.ReviseAsync(
            tenant,
            Harness.Actor,
            created.Value.Id,
            new ItemRevisionDraft(
                new LocalizedName("صنفٌ باسمٍ مصحَّح", "Corrected item"),
                "consumables",
                "BOX",
                []),
            token);

        Assert.True(regrouped.IsSuccess, Describe(regrouped));

        Proof.Require(
            string.Equals(regrouped.Value.ItemGroup, "consumables", StringComparison.Ordinal),
            "والمجموعة تتغيّر بعد الحركة ولا تمسّ ما مضى — كل حركة تحمل مجموعتها على صفّها هي",
            "المجموعة=" + regrouped.Value.ItemGroup);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · **الصنف يُعطَّل وله رصيد** — والجواب يحمل ما بقي
    // ═══════════════════════════════════════════════════════════════════════
    //
    // وهذا هو القرار المُسمّى: يخالف حكمَ موضع التسكين عمداً، لأن رفضَه فوق رصيدٍ يصنع
    // دائرةً مغلقة — لا يُعطَّل حتى ينفد، ولا ينفد إلا ببيعٍ يقتضي أن يكون عاملاً.
    [Fact]
    public async Task الصنف_يُعطَّل_وله_رصيد_والجواب_يُسمّي_ما_بقي()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.ItemLifecycleTenant;
        string code = Harness.Next("ITEM");

        Result<ItemView> created = await RegisterAsync(tenant, code, "صنفٌ يُوقَف", "*", Piece, token);
        Assert.True(created.IsSuccess, Describe(created));

        Result<InventoryMovementCost> received = await ReceiveAsync(tenant, code, new InventoryQuantity(20m, Piece), 200.0000m, token);
        Assert.True(received.IsSuccess, Describe(received));

        Result<ItemLifecycleView> before = await _harness.Items
            .LifecycleAsync(tenant, Harness.Actor, created.Value.Id, token);

        Assert.True(before.IsSuccess, Describe(before));

        Proof.Require(
            before.Value.IsActive && before.Value.HoldsStock && before.Value.PlacementsWithStock == 1,
            "قبل الإيقاف: متداوَل وله رصيد في موضعٍ واحد",
            "متداوَل=" + before.Value.IsActive.ToString(CultureInfo.InvariantCulture)
            + " · مواضع=" + before.Value.PlacementsWithStock.ToString(CultureInfo.InvariantCulture));

        // ── **يُعطَّل وله رصيد** ─────────────────────────────────────────────
        Result<ItemLifecycleView> stopped = await _harness.Items
            .DeactivateAsync(tenant, Harness.Actor, created.Value.Id, token);

        Assert.True(stopped.IsSuccess, Describe(stopped));

        Proof.Require(
            !stopped.Value.IsActive && stopped.Value.HoldsStock && stopped.Value.PlacementsWithStock == 1,
            "الصنف يُعطَّل وله رصيد — وإلّا لصارت دائرةً مغلقة: لا يُعطَّل حتى ينفد ولا ينفد إلا ببيعٍ يقتضي أن يكون عاملاً",
            "متداوَل=" + stopped.Value.IsActive.ToString(CultureInfo.InvariantCulture)
            + " · بقي رصيد=" + stopped.Value.HoldsStock.ToString(CultureInfo.InvariantCulture)
            + " في " + stopped.Value.PlacementsWithStock.ToString(CultureInfo.InvariantCulture) + " موضع");

        Proof.Require(
            stopped.Value.HoldsStock,
            "**وليس صامتاً**: الجواب يقول إن البضاعة باقية، فلا يظنّ أحدٌ أنها ذهبت مع الإيقاف",
            "holdsStock=" + stopped.Value.HoldsStock.ToString(CultureInfo.InvariantCulture));

        // ── وإعادة التعطيل تنجح ─────────────────────────────────────────────
        Result<ItemLifecycleView> again = await _harness.Items
            .DeactivateAsync(tenant, Harness.Actor, created.Value.Id, token);

        Assert.True(again.IsSuccess, Describe(again));

        Proof.Require(
            !again.Value.IsActive,
            "وإعادة تعطيل مُعطَّلٍ تنجح — الحالة المطلوبة قائمة، والفشل عليها يُلزم كل مستدعٍ بقراءةٍ قبل الكتابة",
            "متداوَل=" + again.Value.IsActive.ToString(CultureInfo.InvariantCulture));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · **الوارد وحده يُمنع** — والصادر وعكسُ ما مضى يعملان
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task المُعطَّل_يمنع_الوارد_وحده_ويبقى_الصادر_وعكسُ_ما_مضى_عاملين()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.ItemLifecycleTenant;
        string code = Harness.Next("ITEM");

        Result<ItemView> created = await RegisterAsync(tenant, code, "صنفٌ يُوقَف ثم يُصرَف", "*", Piece, token);
        Assert.True(created.IsSuccess, Describe(created));

        Result<InventoryMovementCost> seeded = await ReceiveAsync(tenant, code, new InventoryQuantity(20m, Piece), 200.0000m, token);
        Assert.True(seeded.IsSuccess, Describe(seeded));

        // صرفٌ **قبل** الإيقاف، كي يوجد ما يُعكَس بعده.
        InventoryMovementSource issueSource = Source("SalesInvoiceLine", "sales.invoice.cost_of_sales");

        Result<InventoryMovementCost> issuedBefore = await _harness.Stock.IssueAsync(
            new InventoryIssue
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = issueSource,
                Location = Place(code),
                Quantity = new InventoryQuantity(5m, Piece),
                OccurredOn = June,
            },
            token);

        Assert.True(issuedBefore.IsSuccess, Describe(issuedBefore));

        await _harness.Items.DeactivateAsync(tenant, Harness.Actor, created.Value.Id, token);

        // ── الوارد يُرفض باسمه ──────────────────────────────────────────────
        Result<InventoryMovementCost> inbound = await ReceiveAsync(tenant, code, new InventoryQuantity(3m, Piece), 30.0000m, token);

        Assert.True(inbound.IsFailure, "استُلم صنفٌ مُعطَّل.");

        Proof.Require(
            inbound.Errors[0].Code == "inventory.item_inactive"
            && inbound.Errors[0].MessageAr.Contains(code, StringComparison.Ordinal),
            "الوارد على صنفٍ مُعطَّل يُرفض باسمه ويُسمّي الصنف",
            inbound.Errors[0].Code);

        // ── والصادر يبقى: «توقّف عن شرائه وبِع ما بقي» ─────────────────────
        Result<InventoryMovementCost> outbound = await _harness.Stock.IssueAsync(
            new InventoryIssue
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source("SalesInvoiceLine", "sales.invoice.cost_of_sales"),
                Location = Place(code),
                Quantity = new InventoryQuantity(4m, Piece),
                OccurredOn = June,
            },
            token);

        Assert.True(outbound.IsSuccess, Describe(outbound));

        Proof.Require(
            outbound.Value.Cost.Amount == 40.0000m,
            "والصادر منه يبقى مسموحاً حتى ينفد — وذلك معنى إيقاف الصنف حرفياً",
            "تكلفة الصرف=" + Proof.Money(outbound.Value.Cost.Amount));

        // ── **وعكسُ صرفٍ مضى يعمل**: التصحيح لا يُمنع بحالةٍ وُلدت بعد الواقعة ──
        Result<InventoryMovementCost> reversed = await _harness.Stock.ReverseMovementAsync(
            new InventoryMovementReversal
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source("SalesCreditNote", "sales.credit_note.cost_reversal"),
                ReversedMovement = issueSource,
                OccurredOn = June,
            },
            token);

        Assert.True(reversed.IsSuccess, Describe(reversed));

        Proof.Require(
            reversed.Value.Cost.Amount == 50.0000m,
            "**وعكسُ صرفٍ مضى يعمل على صنفٍ عُطّل بعده** — ولو مُنع لصار الإيقاف يُجمّد أخطاءً لا يمكن ردّها",
            "قيمة العكس=" + Proof.Money(reversed.Value.Cost.Amount));

        // والفحص على الوارد وحده: العكس أعاد البضاعة وهو حركةٌ داخلة في أثرها.
        Result<StockBalanceView> balance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, code, Warehouse, InventoryLocations.Default, token);

        Assert.True(balance.IsSuccess, Describe(balance));

        Proof.Require(
            balance.Value.Quantity.Magnitude == 16m,
            "والرصيد بعدها ستّ عشرة: 20 − 5 − 4 + 5 — فالعكس أعاد ما صُرف ولم يمنعه الإيقاف",
            "الرصيد=" + Quantity(balance.Value.Quantity.Magnitude));
    }

    // ────────────────────────────────────────────────────────────────────────

    private Task<Result<ItemView>> RegisterAsync(
        TenantId tenant, string code, string name, string group, string baseUnit, CancellationToken token)
        => _harness.Items.CreateAsync(
            tenant,
            Harness.Actor,
            new ItemDraft(code, new LocalizedName(name, "Lifecycle item"), group, baseUnit, []),
            token).AsTask();

    private static InventoryItemLocation Place(string item) =>
        new(item, Warehouse, InventoryLocations.Default, "*");

    private Task<Result<InventoryMovementCost>> ReceiveAsync(
        TenantId tenant, string item, InventoryQuantity quantity, decimal cost, CancellationToken token)
        => _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = Source("OpeningBalance", "inventory.count_adjustment.posted"),
                Location = Place(item),
                Quantity = quantity,
                Cost = Harness.Sar(cost),
                OccurredOn = June,
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
