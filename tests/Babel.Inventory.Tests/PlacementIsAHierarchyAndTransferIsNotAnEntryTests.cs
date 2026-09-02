using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Inventory.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>التسكين هرمٌ يُتحقَّق منه، والنقل بين موقعين ليس قيداً.</b>
/// <para>
/// وما تُثبته هذه المجموعة أربعة أشياء لا يراها مصرّف ولا يكشفها توازن:
/// </para>
/// <list type="number">
///   <item><description>
///     <b>النقل لا يكتب قيداً واحداً</b> — ويُعدّ ذلك على دفتر أستاذ حقيقي، لا يُدَّعى.
///     ومجموع قيمة المخزون قبل النقل وبعده واحدٌ بالضبط، وهو ما يجعل الامتناع صحيحاً
///     لا اختصاراً.
///   </description></item>
///   <item><description>
///     <b>تعطيل موضعٍ فيه رصيد يُرفض</b> — والرسالة تُسمّي الصنف والكمّية. ولو مرّ
///     لبقيت البضاعة بقيمتها في الحساب الضابط بلا بابٍ تخرج منه.
///   </description></item>
///   <item><description>
///     <b>الهرم يُتحقَّق منه في المسار</b>: موقعٌ في مستودع آخر لا يُقرأ من باب هذا،
///     وأبٌ مُعطَّل لا يُسجَّل تحته ابن.
///   </description></item>
///   <item><description>
///     <b>الرمز غير المسجَّل يُوسَم ولا يُحذف</b> من قراءة الأرصدة بتسكينها — وحذفُه
///     كان سيجعل المجموع المقروء أقلّ من الفعلي، وهو انحرافٌ لا يُظهره توازن.
///   </description></item>
/// </list>
/// </summary>
[Collection("inventory")]
public sealed class PlacementIsAHierarchyAndTransferIsNotAnEntryTests : IAsyncLifetime
{
    private static readonly DateOnly April = new(2026, 4, 12);
    private const string Piece = "EA";
    private const string Carton = "CTN";

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · الهرم ثلاثة مستويات، والأب يُتحقَّق منه — ولا دورة ممكنة
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task الهرم_ثلاثة_مستويات_والأب_يُتحقَّق_منه_في_المسار()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.PlacementTenant;

        string warehouseCode = Harness.Next("WH");
        string otherCode = Harness.Next("WH");
        string locationCode = Harness.Next("LOC");
        string binCode = Harness.Next("BIN");

        Result<StoragePlaceView> warehouse = await CreateAsync(tenant, PlacementLevels.Warehouse, null, warehouseCode, token);
        Result<StoragePlaceView> other = await CreateAsync(tenant, PlacementLevels.Warehouse, null, otherCode, token);

        Assert.True(warehouse.IsSuccess, Describe(warehouse));
        Assert.True(other.IsSuccess, Describe(other));

        Proof.Require(
            warehouse.Value.ParentCode.Length == 0 && warehouse.Value.IsActive,
            "المستودع أعلى الهرم فلا أب له — ورمز أبٍ عليه كان يجعل «هل هذا جذر؟» سؤالاً بلا جواب",
            "الأب=«" + warehouse.Value.ParentCode + "» · عامل=" + warehouse.Value.IsActive.ToString(CultureInfo.InvariantCulture));

        Result<StoragePlaceView> location = await CreateAsync(
            tenant, PlacementLevels.Location, warehouse.Value.Id, locationCode, token);

        Assert.True(location.IsSuccess, Describe(location));

        Result<StoragePlaceView> bin = await CreateAsync(
            tenant, PlacementLevels.Bin, location.Value.Id, binCode, token);

        Assert.True(bin.IsSuccess, Describe(bin));

        Proof.Require(
            string.Equals(location.Value.ParentCode, warehouseCode, StringComparison.Ordinal)
            && string.Equals(bin.Value.ParentCode, locationCode, StringComparison.Ordinal),
            "والموقع تحت مستودعه والرفّ تحت موقعه — وأب كل مستوى هو المستوى السابق حتماً، فلا دورة ممكنة بالبناء",
            "الموقع تحت=" + location.Value.ParentCode + " · الرفّ تحت=" + bin.Value.ParentCode);

        // ── الموقع لا يُقرأ من باب مستودعٍ ليس أباه ──────────────────────────
        Result<StoragePlaceView> misread = await _harness.Places.GetAsync(
            tenant, Harness.Actor, PlacementLevels.Location, other.Value.Id, location.Value.Id, token);

        Assert.True(misread.IsFailure, "موقعٌ في مستودعٍ آخر قُرئ من باب هذا وخرج وكأنه فيه.");

        Proof.Require(
            misread.Errors[0].Code == "inventory.storage_place_not_under_parent",
            "وموقعٌ في مستودعٍ آخر يُرفض باسمه — المسار إفادةٌ تُصدَّق لا زينة",
            misread.Errors[0].Code);

        // ── رمزٌ مكرّر داخل مستواه يُرفض ─────────────────────────────────────
        Result<StoragePlaceView> duplicate = await CreateAsync(
            tenant, PlacementLevels.Location, warehouse.Value.Id, locationCode, token);

        Assert.True(duplicate.IsFailure, "رمز موقعٍ مكرّر مرّ.");

        Proof.Require(
            duplicate.Errors[0].Code == "inventory.duplicate_storage_place_code",
            "ورمزٌ مكرّر داخل مستواه يُرفض — الرمز هوية تحملها الحركات فلا يتكرّر",
            duplicate.Errors[0].Code);

        // ── والرمز نفسه في مستوىً آخر يمرّ: المستوى في المفتاح ──────────────
        Result<StoragePlaceView> sameCodeOtherLevel = await CreateAsync(
            tenant, PlacementLevels.Bin, location.Value.Id, locationCode, token);

        Assert.True(sameCodeOtherLevel.IsSuccess, Describe(sameCodeOtherLevel));

        Proof.Require(
            string.Equals(sameCodeOtherLevel.Value.Level, PlacementLevels.Bin, StringComparison.Ordinal),
            "والرمز نفسه يجوز في مستوىً آخر — المستوى ضلعٌ في المفتاح الفريد، فرفٌّ ومستودعٌ باسم واحد لا يتصادمان",
            "المستوى=" + sameCodeOtherLevel.Value.Level + " · الرمز=" + sameCodeOtherLevel.Value.Code);

        // ── إعادة التسمية تغيّر الاسم ولا تمسّ الرمز ────────────────────────
        Result<StoragePlaceView> renamed = await _harness.Places.RenameAsync(
            tenant,
            Harness.Actor,
            PlacementLevels.Warehouse,
            null,
            warehouse.Value.Id,
            new LocalizedName("المستودع الرئيسي", "Main warehouse"),
            token);

        Assert.True(renamed.IsSuccess, Describe(renamed));

        Proof.Require(
            string.Equals(renamed.Value.Code, warehouseCode, StringComparison.Ordinal)
            && renamed.Value.Name.Arabic == "المستودع الرئيسي"
            && renamed.Value.Name.English == "Main warehouse",
            "وإعادة التسمية تغيّر الاسم بلغتيه ولا تمسّ الرمز — والرمز محمولٌ على كل حركة ورصيد",
            "الرمز=" + renamed.Value.Code + " · الاسم=" + renamed.Value.Name.Arabic + " / " + renamed.Value.Name.English);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · **النقل بين موقعين لا يكتب قيداً واحداً** — والقيمة تنتقل كاملةً
    // ═══════════════════════════════════════════════════════════════════════
    //
    // وهذا أهمّ إثبات في الملفّ. القرار: النقل داخل المنشأة نفسها لا يُغيّر قيمة
    // المخزون؛ والصنف واحدٌ على الطرفين فمجموعته واحدة، ومؤهّل دور
    // `inventory_control` هو مجموعة الصنف — فالحساب المدين هو الحساب الدائن بالمبلغ
    // نفسه. والقيد الذي لا أثر له يبقى إلى الأبد في دفترٍ يُضاف إليه ولا يُحذف منه.
    [Fact]
    public async Task النقل_بين_موقعين_ينقل_الكمّية_وقيمتها_ولا_يكتب_قيداً()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.PlacementTenant;

        (string warehouse, string source, string destination) = await HierarchyAsync(tenant, token);
        string item = Harness.Next("ITEM");
        await RegisterItemAsync(tenant, item, token);

        // ── عشر حبّات بمئة ريال في المصدر: متوسط الحبّة عشرة ─────────────────
        Result<InventoryMovementCost> seeded = await ReceiveAsync(
            tenant, item, warehouse, source, new InventoryQuantity(10m, Piece), 100.0000m, token);

        Assert.True(seeded.IsSuccess, Describe(seeded));

        long entriesBefore = await EntryCountAsync(tenant, token);

        Result<StockTransferView> draft = await _harness.Transfers.CreateAsync(
            tenant,
            Harness.Actor,
            new StockTransferDraft(
                Harness.Next("TRF"),
                item,
                "*",
                warehouse,
                source,
                warehouse,
                destination,
                new InventoryQuantity(4m, Piece),
                April),
            token);

        Assert.True(draft.IsSuccess, Describe(draft));

        Proof.Require(
            string.Equals(draft.Value.State, "DRAFT", StringComparison.Ordinal)
            && draft.Value.Value.Amount == 0m,
            "المسوّدة لا تحمل قيمة ولا تُحرّك رصيداً — القيمة تُحسب عند التنفيذ ولا تُملى",
            "الحالة=" + draft.Value.State + " · القيمة=" + Proof.Money(draft.Value.Value.Amount));

        Result<StockTransferView> moved = await _harness.Transfers
            .MoveAsync(tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(moved.IsSuccess, Describe(moved));

        Proof.Require(
            moved.Value.Value.Amount == 40.0000m && !moved.Value.AlreadyMoved
            && string.Equals(moved.Value.State, "MOVED", StringComparison.Ordinal),
            "أربع حبّات خرجت بأربعين — 4 × متوسط 10.000000 — والقيمة محسوبةٌ في الدفتر المساعد لا مُملاة",
            "القيمة=" + Proof.Money(moved.Value.Value.Amount) + " · الحالة=" + moved.Value.State);

        // ── **العدّ على دفتر أستاذ حقيقي: ولا قيد واحد** ────────────────────
        long entriesAfter = await EntryCountAsync(tenant, token);

        Proof.Require(
            entriesAfter == entriesBefore,
            "ولا قيدٌ واحد كُتب في دفتر الأستاذ — النقل داخل المنشأة لا يغيّر قيمة المخزون، والمدين هو الدائن بالمبلغ نفسه",
            "القيود قبل=" + entriesBefore.ToString(CultureInfo.InvariantCulture)
            + " · بعد=" + entriesAfter.ToString(CultureInfo.InvariantCulture));

        // ── والقيمة انتقلت كاملةً: المجموع قبل وبعد واحد بالضبط ─────────────
        Result<StockBalanceView> from = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, warehouse, source, token);

        Result<StockBalanceView> to = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, warehouse, destination, token);

        Assert.True(from.IsSuccess, Describe(from));
        Assert.True(to.IsSuccess, Describe(to));

        Proof.Require(
            from.Value.Quantity.Magnitude == 6m && from.Value.Value.Amount == 60.0000m
            && to.Value.Quantity.Magnitude == 4m && to.Value.Value.Amount == 40.0000m
            && from.Value.Value.Amount + to.Value.Value.Amount == 100.0000m,
            "ومجموع القيمة قبل النقل وبعده مئةٌ بالضبط — وهو ما يجعل الامتناع عن القيد صحيحاً لا اختصاراً",
            "المصدر=" + Quantity(from.Value.Quantity.Magnitude) + " بـ" + Proof.Money(from.Value.Value.Amount)
            + " · الوجهة=" + Quantity(to.Value.Quantity.Magnitude) + " بـ" + Proof.Money(to.Value.Value.Amount));

        // ── الإعادة بالهوية نفسها لا تُحرّك شيئاً ────────────────────────────
        Result<StockTransferView> replay = await _harness.Transfers
            .MoveAsync(tenant, Harness.Actor, draft.Value.Id, token);

        Assert.True(replay.IsSuccess, Describe(replay));

        Result<StockBalanceView> afterReplay = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, warehouse, destination, token);

        Assert.True(afterReplay.IsSuccess, Describe(afterReplay));

        Proof.Require(
            replay.Value.AlreadyMoved
            && afterReplay.Value.Quantity.Magnitude == 4m
            && afterReplay.Value.Value.Amount == 40.0000m
            && await EntryCountAsync(tenant, token) == entriesBefore,
            "والإعادة بالهوية نفسها تُرجع alreadyMoved ولا تكتب حركة ثالثة ولا قيداً — الحكم حكمُ الحركة لا حالةَ الصفّ",
            "alreadyMoved=" + replay.Value.AlreadyMoved.ToString(CultureInfo.InvariantCulture)
            + " · الوجهة=" + Quantity(afterReplay.Value.Quantity.Magnitude));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · النقل بوحدةٍ أكبر يُحوَّل بمعامله، وما يتجاوز الرصيد يُرفض
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task النقل_يقبل_وحدةً_أكبر_ويرفض_ما_يتجاوز_رصيد_المصدر()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.PlacementTenant;

        (string warehouse, string source, string destination) = await HierarchyAsync(tenant, token);
        string item = Harness.Next("ITEM");
        await RegisterItemAsync(tenant, item, token);

        // ‏٢٤ حبّة في المصدر.
        Result<InventoryMovementCost> seeded = await ReceiveAsync(
            tenant, item, warehouse, source, new InventoryQuantity(24m, Piece), 240.0000m, token);

        Assert.True(seeded.IsSuccess, Describe(seeded));

        // ── نقلُ كرتونٍ واحد = اثنتا عشرة حبّة، بمعامل 12/1 لا بتقريب ────────
        Result<StockTransferView> byCarton = await TransferAsync(
            tenant, item, warehouse, source, destination, new InventoryQuantity(1m, Carton), token);

        Assert.True(byCarton.IsSuccess, Describe(byCarton));

        Result<StockTransferView> movedCarton = await _harness.Transfers
            .MoveAsync(tenant, Harness.Actor, byCarton.Value.Id, token);

        Assert.True(movedCarton.IsSuccess, Describe(movedCarton));

        Result<StockBalanceView> destinationBalance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, warehouse, destination, token);

        Assert.True(destinationBalance.IsSuccess, Describe(destinationBalance));

        Proof.Require(
            destinationBalance.Value.Quantity.Magnitude == 12m
            && movedCarton.Value.Value.Amount == 120.0000m
            && string.Equals(movedCarton.Value.Quantity.Unit, Carton, StringComparison.Ordinal),
            "الكرتون وصل الوجهة اثنتي عشرة حبّة، والمستند يحتفظ بالوحدة المُسلَّمة — فلا تُنسى بالتحويل",
            "الوجهة=" + Quantity(destinationBalance.Value.Quantity.Magnitude)
            + " · وحدة المستند=" + movedCarton.Value.Quantity.Unit
            + " · القيمة=" + Proof.Money(movedCarton.Value.Value.Amount));

        // ── والنقل بما يتجاوز رصيد المصدر يُرفض — بخلاف الصرف الذي يُوسَم ────
        Result<StockTransferView> tooMuch = await TransferAsync(
            tenant, item, warehouse, source, destination, new InventoryQuantity(999m, Piece), token);

        Assert.True(tooMuch.IsSuccess, Describe(tooMuch));

        Result<StockTransferView> refused = await _harness.Transfers
            .MoveAsync(tenant, Harness.Actor, tooMuch.Value.Id, token);

        Assert.True(refused.IsFailure, "نُقلت بضاعةٌ ليست على الرفّ.");

        Proof.Require(
            refused.Errors[0].Code == "inventory.transfer_exceeds_balance",
            "ولا يُنقَل من رفٍّ ما ليس عليه — والصرف يقبل المكشوف لأن البيع قبل الاستلام واقعة، أمّا النقل فيحرّك بضاعةً فعلياً",
            refused.Errors[0].Code);

        // ── والنقل إلى الموضع نفسه ليس نقلاً ────────────────────────────────
        Result<StockTransferView> same = await TransferAsync(
            tenant, item, warehouse, source, source, new InventoryQuantity(1m, Piece), token);

        Assert.True(same.IsFailure, "نقلٌ إلى الموضع نفسه مرّ.");

        Proof.Require(
            same.Errors[0].Code == "inventory.transfer_to_same_place",
            "ونقلٌ إلى الموضع نفسه يُرفض — حركتان تُلغيان بعضهما وتُحدّثان صفّ رصيدٍ واحد مرّتين",
            same.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 4 · تعطيل موضعٍ فيه رصيد **يُرفض**، وتعطيل ما تحته عاملٌ يُرفض
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task تعطيل_موضعٍ_فيه_رصيد_يُرفض_ويُسمّي_الصنف_وكمّيته()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.PlacementTenant;

        string warehouseCode = Harness.Next("WH");
        string locationCode = Harness.Next("LOC");
        string emptyCode = Harness.Next("LOC");

        Result<StoragePlaceView> warehouse = await CreateAsync(tenant, PlacementLevels.Warehouse, null, warehouseCode, token);
        Assert.True(warehouse.IsSuccess, Describe(warehouse));

        Result<StoragePlaceView> filled = await CreateAsync(
            tenant, PlacementLevels.Location, warehouse.Value.Id, locationCode, token);

        Result<StoragePlaceView> empty = await CreateAsync(
            tenant, PlacementLevels.Location, warehouse.Value.Id, emptyCode, token);

        Assert.True(filled.IsSuccess, Describe(filled));
        Assert.True(empty.IsSuccess, Describe(empty));

        string item = Harness.Next("ITEM");
        await RegisterItemAsync(tenant, item, token);

        Result<InventoryMovementCost> seeded = await ReceiveAsync(
            tenant, item, warehouseCode, locationCode, new InventoryQuantity(7m, Piece), 70.0000m, token);

        Assert.True(seeded.IsSuccess, Describe(seeded));

        // ── الموقع المملوء لا يُعطَّل ────────────────────────────────────────
        Result<StoragePlaceView> refused = await _harness.Places.DeactivateAsync(
            tenant, Harness.Actor, PlacementLevels.Location, warehouse.Value.Id, filled.Value.Id, token);

        Assert.True(refused.IsFailure, "عُطّل موقعٌ فيه بضاعة، فبقيت بلا بابٍ تخرج منه.");

        Proof.Require(
            refused.Errors[0].Code == "inventory.storage_place_still_holds_stock"
            && refused.Errors[0].MessageAr.Contains(item, StringComparison.Ordinal)
            && refused.Errors[0].MessageEn.Length > 0,
            "تعطيل موقعٍ فيه رصيد يُرفض، والرسالة تُسمّي الصنف — والموضع المُعطَّل لا يُنقَل منه ولا يُصرف",
            refused.Errors[0].Code + " · " + refused.Errors[0].MessageAr[..Math.Min(80, refused.Errors[0].MessageAr.Length)]);

        // ── والمستودع فوقه لا يُعطَّل: تحته موقعان عاملان ────────────────────
        Result<StoragePlaceView> hasChildren = await _harness.Places.DeactivateAsync(
            tenant, Harness.Actor, PlacementLevels.Warehouse, null, warehouse.Value.Id, token);

        Assert.True(hasChildren.IsFailure, "عُطّل مستودعٌ تحته مواقع عاملة — تعطيلٌ متسلسل بلا أثر يُقرأ.");

        Proof.Require(
            hasChildren.Errors[0].Code == "inventory.storage_place_has_active_children",
            "ومستودعٌ تحته مواقع عاملة لا يُعطَّل — والتسلسل يُخفي ما عُطّل تبعاً عمّن عطّله",
            hasChildren.Errors[0].Code);

        // ── والموقع الخالي يُعطَّل، وإعادة تعطيله تنجح ──────────────────────
        Result<StoragePlaceView> deactivated = await _harness.Places.DeactivateAsync(
            tenant, Harness.Actor, PlacementLevels.Location, warehouse.Value.Id, empty.Value.Id, token);

        Assert.True(deactivated.IsSuccess, Describe(deactivated));

        Result<StoragePlaceView> again = await _harness.Places.DeactivateAsync(
            tenant, Harness.Actor, PlacementLevels.Location, warehouse.Value.Id, empty.Value.Id, token);

        Assert.True(again.IsSuccess, Describe(again));

        Proof.Require(
            !deactivated.Value.IsActive && !again.Value.IsActive,
            "والموقع الخالي يُعطَّل، وإعادة تعطيله تنجح — الحالة المطلوبة قائمة، والفشل عليها يُلزم كل مستدعٍ بقراءةٍ قبل الكتابة",
            "الأول=" + deactivated.Value.IsActive.ToString(CultureInfo.InvariantCulture)
            + " · الثاني=" + again.Value.IsActive.ToString(CultureInfo.InvariantCulture));

        // ── ولا يُسجَّل رفٌّ تحت موقعٍ مُعطَّل ───────────────────────────────
        Result<StoragePlaceView> underDead = await CreateAsync(
            tenant, PlacementLevels.Bin, empty.Value.Id, Harness.Next("BIN"), token);

        Assert.True(underDead.IsFailure, "سُجّل رفٌّ تحت موقعٍ مُعطَّل، فأُحيي الموقع من الباب الخلفي.");

        Proof.Require(
            underDead.Errors[0].Code == "inventory.storage_place_parent_inactive",
            "ولا يُسجَّل ابنٌ تحت أبٍ مُعطَّل — وإلا صار فيه ما يُسكَّن وهو مُعلَنٌ خارج الخدمة",
            underDead.Errors[0].Code);

        // ── والنقل إلى موضعٍ مُعطَّل يُرفض ───────────────────────────────────
        Result<StockTransferView> intoDead = await TransferAsync(
            tenant, item, warehouseCode, locationCode, emptyCode, new InventoryQuantity(1m, Piece), token);

        Assert.True(intoDead.IsFailure, "نُقلت بضاعةٌ إلى موضعٍ مُعطَّل.");

        Proof.Require(
            intoDead.Errors[0].Code == "inventory.storage_place_inactive",
            "ولا تُنقَل بضاعة إلى موضعٍ مُعطَّل",
            intoDead.Errors[0].Code);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 5 · الأرصدة بتسكينها: المسجَّل باسمه، وغير المسجَّل **يُوسَم ولا يُحذف**
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task الرصيد_يُقرأ_بتسكينه_وغير_المسجَّل_يُوسَم_ولا_يُحذف()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.PlacementTenant;

        (string warehouse, string location, _) = await HierarchyAsync(tenant, token);

        string registeredItem = Harness.Next("ITEM");
        string strayItem = Harness.Next("ITEM");
        string strayWarehouse = Harness.Next("WH");

        await RegisterItemAsync(tenant, registeredItem, token);
        await RegisterItemAsync(tenant, strayItem, token);

        Result<InventoryMovementCost> inside = await ReceiveAsync(
            tenant, registeredItem, warehouse, location, new InventoryQuantity(3m, Piece), 30.0000m, token);

        // ‏**رمزٌ لا صفَّ له في السجلّ** — كما تفعل كل حركة كُتبت قبل أن يوجد السجلّ.
        Result<InventoryMovementCost> outside = await ReceiveAsync(
            tenant, strayItem, strayWarehouse, "DEFAULT", new InventoryQuantity(5m, Piece), 50.0000m, token);

        Assert.True(inside.IsSuccess, Describe(inside));
        Assert.True(outside.IsSuccess, Describe(outside));

        Proof.Require(
            outside.IsSuccess,
            "الحركة على رمزٍ غير مسجَّل تمرّ — السجلّ يصف ولا يُبطل، وإلزامُ التسجيل بأثر رجعي يُوقف مستأجراً عاملاً",
            "المستودع غير المسجَّل=" + strayWarehouse);

        Result<IReadOnlyList<PlacementBalanceView>> balances = await _harness.Places
            .ListPlacementBalancesAsync(tenant, Harness.Actor, token);

        Assert.True(balances.IsSuccess, Describe(balances));

        PlacementBalanceView? named = balances.Value.FirstOrDefault(
            row => string.Equals(row.ItemId, registeredItem, StringComparison.Ordinal));

        PlacementBalanceView? stray = balances.Value.FirstOrDefault(
            row => string.Equals(row.ItemId, strayItem, StringComparison.Ordinal));

        Assert.NotNull(named);
        Assert.NotNull(stray);

        Proof.Require(
            named.WarehouseRegistered && named.LocationRegistered
            && named.WarehouseName.Arabic.Length > 0
            && !string.Equals(named.WarehouseName.Arabic, named.WarehouseId, StringComparison.Ordinal),
            "الموضع المسجَّل يخرج باسمه من السجلّ وبوسم «مسجَّل»",
            "المستودع=" + named.WarehouseId + " باسم «" + named.WarehouseName.Arabic + "»");

        Proof.Require(
            !stray.WarehouseRegistered
            && string.Equals(stray.WarehouseName.Arabic, stray.WarehouseId, StringComparison.Ordinal)
            && stray.Quantity.Magnitude == 5m,
            "وغير المسجَّل يخرج ويُوسَم واسمه رمزُه — لا يُحذف من القائمة فيصير المجموع المقروء أقلّ من الفعلي، ولا يُخترَع له اسم",
            "المستودع=" + stray.WarehouseId + " · مسجَّل=" + stray.WarehouseRegistered.ToString(CultureInfo.InvariantCulture)
            + " · الكمّية=" + Quantity(stray.Quantity.Magnitude));
    }

    // ────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// مستويات الهرم كما تراها الاختبارات — <b>نصوصٌ مكتوبة هنا عمداً</b>.
    /// <para>
    /// <c>PlacementLevel</c> نوعٌ <c>internal</c> في <c>Persistence</c>، وهو مبلوغٌ من
    /// هذه المجموعة بـ<c>InternalsVisibleTo</c>. لكنّ كتابته نصّاً هنا تجعل الإثبات
    /// يفشل إن تغيّرت القيمة المُخزَّنة — وهي قيمةٌ في عمودٍ مكتوب في قاعدة عملاء، لا
    /// ثابتٌ يُعاد تسميته بلا أثر.
    /// </para>
    /// </summary>
    private static class PlacementLevels
    {
        public const string Warehouse = "WAREHOUSE";

        public const string Location = "LOCATION";

        public const string Bin = "BIN";
    }

    private Task<Result<StoragePlaceView>> CreateAsync(
        TenantId tenant, string level, Guid? parentId, string code, CancellationToken token)
        => _harness.Places.CreateAsync(
            tenant,
            Harness.Actor,
            level,
            parentId,
            new StoragePlaceDraft(code, new LocalizedName("موضع " + code, "Place " + code)),
            token).AsTask();

    /// <summary>مستودعٌ وموقعان تحته — الهيكل الذي يتكرّر في أكثر من إثبات.</summary>
    private async Task<(string Warehouse, string Source, string Destination)> HierarchyAsync(
        TenantId tenant, CancellationToken token)
    {
        string warehouseCode = Harness.Next("WH");
        string sourceCode = Harness.Next("LOC");
        string destinationCode = Harness.Next("LOC");

        Result<StoragePlaceView> warehouse = await CreateAsync(tenant, PlacementLevels.Warehouse, null, warehouseCode, token);
        Assert.True(warehouse.IsSuccess, Describe(warehouse));

        Result<StoragePlaceView> source = await CreateAsync(
            tenant, PlacementLevels.Location, warehouse.Value.Id, sourceCode, token);

        Result<StoragePlaceView> destination = await CreateAsync(
            tenant, PlacementLevels.Location, warehouse.Value.Id, destinationCode, token);

        Assert.True(source.IsSuccess, Describe(source));
        Assert.True(destination.IsSuccess, Describe(destination));

        return (warehouseCode, sourceCode, destinationCode);
    }

    private async Task RegisterItemAsync(TenantId tenant, string item, CancellationToken token)
    {
        Result<ItemView> registered = await _harness.Items.CreateAsync(
            tenant,
            Harness.Actor,
            new ItemDraft(
                item,
                new LocalizedName("صنف تسكين", "Placement item"),
                "*",
                Piece,
                [new ItemUnitDraft(Carton, 12L, 1L)]),
            token);

        Assert.True(registered.IsSuccess, Describe(registered));
    }

    private Task<Result<StockTransferView>> TransferAsync(
        TenantId tenant,
        string item,
        string warehouse,
        string source,
        string destination,
        InventoryQuantity quantity,
        CancellationToken token)
        => _harness.Transfers.CreateAsync(
            tenant,
            Harness.Actor,
            new StockTransferDraft(
                Harness.Next("TRF"), item, "*", warehouse, source, warehouse, destination, quantity, April),
            token).AsTask();

    private Task<Result<InventoryMovementCost>> ReceiveAsync(
        TenantId tenant,
        string item,
        string warehouse,
        string location,
        InventoryQuantity quantity,
        decimal cost,
        CancellationToken token)
        => _harness.Stock.ReceiveAsync(
            new InventoryReceipt
            {
                Tenant = tenant,
                Actor = Harness.Actor,
                Source = new InventoryMovementSource(
                    BabelModule.Inventory,
                    "OpeningBalance",
                    Guid.CreateVersion7().ToString("D", CultureInfo.InvariantCulture),
                    PostingTrigger.OnApproval.ToString(),
                    1,
                    "inventory.count_adjustment.posted"),
                Location = new InventoryItemLocation(item, warehouse, location, "*"),
                Quantity = quantity,
                Cost = Harness.Sar(cost),
                OccurredOn = April,
            },
            token).AsTask();

    /// <summary>
    /// عدد قيود هذه المنشأة كلّها في دفتر أستاذ <b>حقيقي</b>.
    /// <para>
    /// <b>وعلى المنشأة كلّها لا على مستند النقل وحده:</b> عدٌّ مقصورٌ على نوع مستند
    /// النقل كان سيُرجع صفراً حتى لو كتب النقلُ قيداً <b>باسم مستندٍ آخر</b> — أي أن
    /// الفحص كان سيمرّ على العطل الذي وُجد ليمنعه.
    /// </para>
    /// </summary>
    private static async Task<long> EntryCountAsync(TenantId tenant, CancellationToken token)
    {
        await using Npgsql.NpgsqlConnection connection = new(InventoryTestEnvironment.Ledger.AppConnectionString);
        await connection.OpenAsync(token);

        await using Npgsql.NpgsqlCommand command = new(
            """ select count(*) from ledger.journal_entry where company_id = $1 """, connection);

        command.Parameters.AddWithValue(tenant.Value);
        return (long)(await command.ExecuteScalarAsync(token))!;
    }

    private static string Quantity(decimal value) => value.ToString("0.######", CultureInfo.InvariantCulture);

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static error => error.ToString()));
}
