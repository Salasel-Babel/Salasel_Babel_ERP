using Babel.Contracts.Inventory;
using Babel.Inventory.Application;
using Babel.SharedKernel;
using Xunit;

namespace Babel.Inventory.Tests;

/// <summary>
/// <b>المكان صار شيئاً موجوداً، والمسوّدة لا تُقبل على مكانٍ لا يعرفه الكتالوج.</b>
/// <para>
/// وما تُثبته هذه المجموعة ليس أن الجدولين موجودان — ذلك يراه المصرّف — بل ثلاثة أشياء
/// تفشل بصمت لو لم تُمنع:
/// </para>
/// <para>
/// ‏<b>١ · خطأ إملائي في رمز مستودع يفتح رصيداً خامساً يُطابَق تماماً.</b> المطابقة
/// الثلاثية تجمع الحركات والأرصدة على المفتاح الرباعي نفسه، فرصيد «WH-O1» بحرف O
/// يتوازن مع حركاته توازناً كاملاً، ويحمل قيمةً حقيقية لا يعرف أحدٌ أين هي.
/// <b>ولا فحصٌ يقارن طرفين يراه.</b>
/// </para>
/// <para>
/// ‏<b>٢ · رمز موقعٍ واحد في مستودعين موقعان لا موقع.</b> وهو ما يقوله مفتاح الرصيد
/// الرباعي حرفياً، فكتالوجٌ يجعل الرمز فريداً عبر المنشأة يخالف مفتاحه هو.
/// </para>
/// <para>
/// ‏<b>٣ · تعطيل مكانٍ فيه بضاعة يترك قيمةً في الميزانية بلا مستندٍ يُخرجها</b>، لأن
/// التعطيل يغلق كل باب مسوّدة عليه.
/// </para>
/// </summary>
[Collection("inventory")]
public sealed class WarehouseCatalogueTests : IAsyncLifetime
{
    private static readonly DateOnly April = new(2026, 4, 12);
    private const string Piece = "EACH";

    private Harness _harness = null!;

    public async ValueTask InitializeAsync()
        => _harness = await Harness.CreateAsync(TestContext.Current.CancellationToken);

    public ValueTask DisposeAsync()
    {
        _harness.Dispose();
        return ValueTask.CompletedTask;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 1 · الرمز هوية، وهوية الموقع زوجٌ لا رمزٌ مفرد
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task رمز_المستودع_لا_يتكرّر_وهوية_الموقع_زوجٌ_لا_رمزٌ_مفرد()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.WarehouseCatalogueTenant;

        string first = Harness.Next("WH");
        string second = Harness.Next("WH");

        Result<WarehouseView> registered = await _harness.Places.CreateWarehouseAsync(
            tenant,
            Harness.Actor,
            new WarehouseDraft(first, new TranslatedName("المستودع الرئيسي", new Dictionary<string, string>
            {
                ["en"] = "Main warehouse",
            }), "dry_goods"),
            token);

        Assert.True(registered.IsSuccess, Describe(registered));

        Proof.Require(
            registered.Value.Origin == "DECLARED"
            && registered.Value.IsActive
            && registered.Value.Name.Arabic == "المستودع الرئيسي"
            && registered.Value.Name.Translations["en"] == "Main warehouse",
            "ما يكتبه إنسان يُولد DECLARED عاملاً، واسمه سجلٌّ عربي وترجماته صفوف",
            registered.Value.Origin + " · " + registered.Value.Name.Arabic
            + " · ترجمات=" + registered.Value.Name.TranslationCount.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // ── الرمز المكرّر يُرفض برمزٍ ثابت لا بانفجار فريدٍ من القاعدة ────────
        Result<WarehouseView> duplicate = await _harness.Places.CreateWarehouseAsync(
            tenant, Harness.Actor, new WarehouseDraft(first, new TranslatedName("مستودع آخر"), string.Empty), token);

        Assert.True(duplicate.IsFailure, "رمز مستودع مكرّر مرّ.");

        Proof.Require(
            duplicate.Errors[0].Code == "inventory.duplicate_warehouse_code"
            && duplicate.Errors[0].MessageAr.Contains(first, StringComparison.Ordinal)
            && duplicate.Errors[0].MessageEn.Length > 0,
            "والرمز المكرّر يُرفض باسمه وبلغتين — لا بانفجار فهرسٍ فريد",
            duplicate.Errors[0].Code);

        // ── والرمز الفارغ يُرفض قبل أن يُبحث عنه: هوية لا وصف ────────────────
        Result<WarehouseView> blank = await _harness.Places.CreateWarehouseAsync(
            tenant, Harness.Actor, new WarehouseDraft("  ", new TranslatedName("مستودع بلا رمز"), string.Empty), token);

        Assert.True(blank.IsFailure, "رمز مستودع فارغ مرّ.");

        Proof.Require(
            blank.Errors[0].Code == "inventory.code_missing",
            "ورمزٌ فارغ ليس «افتراضياً»: يُكتب نصّاً فارغاً في كل حركةٍ تليه فيصير رصيداً في مكانٍ اسمه لا شيء",
            blank.Errors[0].Code);

        // ── مستودعٌ ثانٍ، ورمز موقعٍ واحد فيهما: موقعان لا موقع ──────────────
        Result<WarehouseView> other = await _harness.Places.CreateWarehouseAsync(
            tenant, Harness.Actor, new WarehouseDraft(second, new TranslatedName("مستودع الفرع"), string.Empty), token);

        Assert.True(other.IsSuccess, Describe(other));

        Result<LocationView> here = await _harness.Places.CreateLocationAsync(
            tenant, Harness.Actor, registered.Value.Id, new LocationDraft("A-01", new TranslatedName("الرفّ الأول")), token);

        Result<LocationView> there = await _harness.Places.CreateLocationAsync(
            tenant, Harness.Actor, other.Value.Id, new LocationDraft("A-01", new TranslatedName("رفّ الفرع")), token);

        Assert.True(here.IsSuccess, Describe(here));
        Assert.True(there.IsSuccess, Describe(there));

        Proof.Require(
            here.Value.Id != there.Value.Id
            && here.Value.WarehouseCode == first
            && there.Value.WarehouseCode == second,
            "و«A-01» في مستودعين موقعان مستقلّان — وهو ما يقوله المفتاح الرباعي حرفياً",
            here.Value.WarehouseCode + "/A-01 ≠ " + there.Value.WarehouseCode + "/A-01");

        // ── وتكراره داخل المستودع الواحد يُرفض ──────────────────────────────
        Result<LocationView> repeated = await _harness.Places.CreateLocationAsync(
            tenant, Harness.Actor, registered.Value.Id, new LocationDraft("A-01", new TranslatedName("رفٌّ ثانٍ")), token);

        Assert.True(repeated.IsFailure, "رمز موقع مكرّر داخل مستودعه مرّ.");

        Proof.Require(
            repeated.Errors[0].Code == "inventory.duplicate_location_code"
            && repeated.Errors[0].MessageAr.Contains(first, StringComparison.Ordinal),
            "والتكرار داخل المستودع الواحد وحده هو الممنوع، والرفض يُسمّي المستودع",
            repeated.Errors[0].Code);

        // ── والقائمة مرتَّبة بالرمز ترتيباً حرفياً ثابتاً ────────────────────
        Result<IReadOnlyList<WarehouseView>> listed =
            await _harness.Places.ListWarehousesAsync(tenant, Harness.Actor, token);

        Assert.True(listed.IsSuccess, Describe(listed));

        string[] codes = [.. listed.Value.Select(static row => row.Code)];

        Proof.Require(
            codes.SequenceEqual(codes.Order(StringComparer.Ordinal)),
            "والقائمة مرتَّبة بالرمز ترتيباً حرفياً ثابتاً — لا بترتيب الإدخال ولا بترتيبٍ ثقافي",
            string.Join(" · ", codes));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 2 · بوّابة المكان: الرفض **قبل** أن يُكتب صفّ، لا عند الترحيل
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task مسوّدة_على_مكانٍ_لا_يعرفه_الكتالوج_تُرفض_قبل_أن_يُكتب_صفّ()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.PlaceGateTenant;

        string item = Harness.Next("ITEM");
        await RegisterItemAsync(tenant, item, token);

        string warehouse = Harness.Next("WH");
        Guid warehouseId = await _harness.RegisterPlaceAsync(tenant, warehouse, "A-01", token);

        int before = await CountDocumentsAsync(tenant, token);

        // ── ١ · مستودع لا وجود له — والخطأ الإملائي هو المشهد الحقيقي ────────
        Result<StockDocumentView> unknownWarehouse = await DraftAsync(
            tenant, item, warehouse + "X", "A-01", token);

        Assert.True(unknownWarehouse.IsFailure, "مسوّدة على مستودعٍ مجهول مرّت.");

        Proof.Require(
            unknownWarehouse.Errors[0].Code == "inventory.warehouse_not_found"
            && unknownWarehouse.Errors[0].MessageAr.Contains(warehouse + "X", StringComparison.Ordinal),
            "مستودعٌ لا يعرفه الكتالوج يُرفض باسمه — ولا يفتح رصيداً خامساً يُطابَق تماماً",
            unknownWarehouse.Errors[0].Code);

        // ── ٢ · موقعٌ موجود في مستودعٍ آخر ليس موجوداً هنا: الزوج هو المفتاح ──
        Result<StockDocumentView> foreignLocation = await DraftAsync(
            tenant, item, warehouse, "Z-99", token);

        Assert.True(foreignLocation.IsFailure, "مسوّدة على موقعٍ ليس في مستودعها مرّت.");

        Proof.Require(
            foreignLocation.Errors[0].Code == "inventory.location_not_in_warehouse"
            && foreignLocation.Errors[0].MessageAr.Contains("Z-99", StringComparison.Ordinal)
            && foreignLocation.Errors[0].MessageAr.Contains(warehouse, StringComparison.Ordinal),
            "وهوية الموقع زوجٌ: الرفض يُسمّي المستودع والرمز معاً",
            foreignLocation.Errors[0].Code);

        // ── ٣ · مكانٌ معطَّل يُرفض، ولا يُبتلع كأنه غير موجود ─────────────────
        Result<LocationView> location = Assert.Single(
            (await _harness.Places.ListLocationsAsync(tenant, Harness.Actor, warehouseId, token)).Value
                .Select(Result<LocationView>.Success));

        Result<LocationView> deactivated = await _harness.Places.SetLocationActiveAsync(
            tenant, Harness.Actor, warehouseId, location.Value.Id, active: false, token);

        Assert.True(deactivated.IsSuccess, Describe(deactivated));

        Result<StockDocumentView> onDeadLocation = await DraftAsync(tenant, item, warehouse, "A-01", token);

        Assert.True(onDeadLocation.IsFailure, "مسوّدة على موقع معطَّل مرّت.");

        Proof.Require(
            onDeadLocation.Errors[0].Code == "inventory.location_inactive",
            "والموقع المعطَّل يُرفض برمزٍ يخصّه — لا بـ«غير موجود»، فالفرق هو الفرق بين «فعّله» و«سجّله»",
            onDeadLocation.Errors[0].Code);

        Result<WarehouseView> closed = await _harness.Places.SetWarehouseActiveAsync(
            tenant, Harness.Actor, warehouseId, active: false, token);

        Assert.True(closed.IsSuccess, Describe(closed));

        Result<StockDocumentView> onDeadWarehouse = await DraftAsync(tenant, item, warehouse, "A-01", token);

        Assert.True(onDeadWarehouse.IsFailure, "مسوّدة على مستودع معطَّل مرّت.");

        Proof.Require(
            onDeadWarehouse.Errors[0].Code == "inventory.warehouse_inactive",
            "والمستودع المعطَّل كذلك",
            onDeadWarehouse.Errors[0].Code);

        // ── والرفوض الأربعة لم تكتب صفّاً واحداً ─────────────────────────────
        int after = await CountDocumentsAsync(tenant, token);

        Proof.Require(
            before == after,
            "وأربعة رفوض لم تترك مستنداً واحداً: الفحص قبل الكتابة لا بعدها — فلا مسوّدة عالقة بلا مخرج",
            "قبل=" + before.ToString(System.Globalization.CultureInfo.InvariantCulture)
            + " · بعد=" + after.ToString(System.Globalization.CultureInfo.InvariantCulture));

        // ── وإعادة التفعيل تُعيد الباب، فالتعطيل ليس باباً ذا اتجاه واحد ─────
        Assert.True(
            (await _harness.Places.SetWarehouseActiveAsync(tenant, Harness.Actor, warehouseId, active: true, token))
                .IsSuccess);
        Assert.True(
            (await _harness.Places.SetLocationActiveAsync(
                tenant, Harness.Actor, warehouseId, location.Value.Id, active: true, token)).IsSuccess);

        Result<StockDocumentView> accepted = await DraftAsync(tenant, item, warehouse, "A-01", token);

        Assert.True(accepted.IsSuccess, Describe(accepted));

        Proof.Require(
            accepted.Value.State == "DRAFT" && accepted.Value.WarehouseId == warehouse,
            "وبعد إعادة التفعيل تُقبل المسوّدة على المكان نفسه — والتعطيل باب يُفتح ويُغلق لا قرارٌ نهائي",
            accepted.Value.Number + " · " + accepted.Value.State);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // 3 · تعطيل مكانٍ فيه بضاعة يُرفض — **مُسمّياً الصفوف التي تحملها**
    // ═══════════════════════════════════════════════════════════════════════
    [Fact]
    public async Task تعطيل_مكانٍ_فيه_بضاعة_يُرفض_ويُسمّي_الصفوف_التي_تحملها()
    {
        CancellationToken token = TestContext.Current.CancellationToken;
        TenantId tenant = InventoryTestEnvironment.DeactivationTenant;

        string item = Harness.Next("ITEM");
        await RegisterItemAsync(tenant, item, token);

        string warehouse = Harness.Next("WH");
        Guid warehouseId = await _harness.RegisterPlaceAsync(tenant, warehouse, "A-01", token);

        // ── يُملأ الرصيد بمستندٍ حقيقي مُرحَّل: لا كتابة يدوية في جدول ────────
        Result<StockDocumentView> drafted = await DraftAsync(tenant, item, warehouse, "A-01", token, quantity: 10m, cost: 100m);
        Assert.True(drafted.IsSuccess, Describe(drafted));

        Result<StockDocumentView> posted = await _harness.StockDocuments
            .PostAsync(tenant, Harness.Actor, drafted.Value.Id, token);

        Assert.True(posted.IsSuccess, Describe(posted));

        Guid locationId = (await _harness.Places.ListLocationsAsync(tenant, Harness.Actor, warehouseId, token))
            .Value.Single().Id;

        // ── تعطيل الموقع يُرفض، والرفض يُسمّي الصنف والمكان والكمّية والقيمة ──
        Result<LocationView> refusedLocation = await _harness.Places.SetLocationActiveAsync(
            tenant, Harness.Actor, warehouseId, locationId, active: false, token);

        Assert.True(refusedLocation.IsFailure, "تعطيل موقعٍ فيه بضاعة مرّ.");

        Proof.Require(
            refusedLocation.Errors[0].Code == "inventory.location_has_stock"
            && refusedLocation.Errors[0].MessageAr.Contains(item, StringComparison.Ordinal)
            && refusedLocation.Errors[0].MessageAr.Contains(warehouse + "/A-01", StringComparison.Ordinal)
            && refusedLocation.Errors[0].MessageEn.Contains(item, StringComparison.Ordinal),
            "والرفض يُسمّي الصفّ الذي يحمل البضاعة بالشكل «صنف @ مستودع/موقع» — الشكل نفسه الذي يطبعه رفض الإقفال",
            refusedLocation.Errors[0].MessageAr.Split('\n')[1]);

        Result<WarehouseView> refusedWarehouse = await _harness.Places.SetWarehouseActiveAsync(
            tenant, Harness.Actor, warehouseId, active: false, token);

        Assert.True(refusedWarehouse.IsFailure, "تعطيل مستودعٍ فيه بضاعة مرّ.");

        Proof.Require(
            refusedWarehouse.Errors[0].Code == "inventory.warehouse_has_stock"
            && refusedWarehouse.Errors[0].MessageAr.Contains(item, StringComparison.Ordinal),
            "وكذلك المستودع — فالتعطيل يغلق كل باب مستندٍ يُخرجها، فتبقى قيمةٌ في الميزانية بلا مخرج",
            refusedWarehouse.Errors[0].Code);

        // ── والرصيد لم يتحرّك بأيٍّ من الرفضين ──────────────────────────────
        Result<StockBalanceView> balance = await _harness.Stock
            .ReadStockAsync(tenant, Harness.Actor, item, warehouse, "A-01", token);

        Assert.True(balance.IsSuccess, Describe(balance));

        Proof.Require(
            balance.Value.Quantity.Magnitude == 10m && balance.Value.Value.Amount == 100.0000m,
            "والرفض لم يُخرج بضاعةً ولم يُصفّر قيمة — التعطيل لا يمسّ رصيداً حتى حين ينجح",
            Quantity(balance.Value.Quantity.Magnitude) + " / " + Proof.Money(balance.Value.Value.Amount));

        // ── ثم يُفرَغ بمستند، فيمرّ التعطيل ─────────────────────────────────
        Result<StockDocumentView> emptied = await DraftAsync(
            tenant, item, warehouse, "A-01", token, quantity: 10m, cost: 0m, direction: "OUT");

        Assert.True(emptied.IsSuccess, Describe(emptied));
        Assert.True((await _harness.StockDocuments.PostAsync(tenant, Harness.Actor, emptied.Value.Id, token)).IsSuccess);

        Result<LocationView> allowed = await _harness.Places.SetLocationActiveAsync(
            tenant, Harness.Actor, warehouseId, locationId, active: false, token);

        Assert.True(allowed.IsSuccess, Describe(allowed));

        Proof.Require(
            !allowed.Value.IsActive,
            "وبعد إفراغه بمستند يمرّ التعطيل — والمخرج مستند لا استثناء",
            allowed.Value.WarehouseCode + "/" + allowed.Value.Code + " · عامل=" + allowed.Value.IsActive.ToString());
    }

    // ═══════════════════════════════════════════════════════════════════════
    // مساعدات
    // ═══════════════════════════════════════════════════════════════════════

    private async Task RegisterItemAsync(TenantId tenant, string item, CancellationToken token)
    {
        Result<ItemView> registered = await _harness.Items.CreateAsync(
            tenant,
            Harness.Actor,
            new ItemDraft(item, new LocalizedName("صنف اختبار", "Test item"), "*", Piece, []),
            token);

        Assert.True(registered.IsSuccess, Describe(registered));
    }

    private async Task<Result<StockDocumentView>> DraftAsync(
        TenantId tenant,
        string item,
        string warehouse,
        string location,
        CancellationToken token,
        decimal quantity = 1m,
        decimal cost = 10m,
        string direction = "IN")
        => await _harness.StockDocuments.CreateAsync(
            tenant,
            Harness.Actor,
            new StockDocumentDraft(
                Harness.Next("STK"),
                direction,
                item,
                warehouse,
                location,
                "*",
                new InventoryQuantity(quantity, Piece),
                Harness.Sar(cost),
                April),
            token);

    /// <summary>عدد مستندات المنشأة — <b>مقروءاً من السطح المنشور لا من الجدول</b>.</summary>
    private async Task<int> CountDocumentsAsync(TenantId tenant, CancellationToken token)
    {
        Result<IReadOnlyList<StockDocumentView>> listed =
            await _harness.StockDocuments.ListAsync(tenant, Harness.Actor, token);

        Assert.True(listed.IsSuccess, Describe(listed));
        return listed.Value.Count;
    }

    private static string Describe<T>(Result<T> result)
        => result.IsSuccess ? "نجح" : string.Join(" | ", result.Errors.Select(static e => e.ToString()));

    private static string Quantity(decimal value)
        => value.ToString("0.000000", System.Globalization.CultureInfo.InvariantCulture);
}
