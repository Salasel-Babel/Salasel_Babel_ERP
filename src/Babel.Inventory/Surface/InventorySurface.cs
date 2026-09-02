using Babel.Contracts.Inventory;
using Babel.Inventory.Application;
using Babel.Inventory.Subledger;
using Babel.SharedKernel;

namespace Babel.Inventory.Surface;

/// <summary>
/// <b>السطح المنشور لوحدة المخزون</b> — وهو ما يجوز للجذر التركيبي أن يسمّيه، ولا شيء غيره.
/// <para>
/// <b>لماذا يوجد هذا الملفّ أصلاً:</b> القاعدة 13 (البند ب) تمنع <c>Babel.Api</c> من أن
/// يذكر أيّ نوع من فضاء اسم داخلي لوحدة — و<c>Application</c> و<c>Persistence</c>
/// و<c>Subledger</c> منها بالاسم، <b>ولو أُضيف النوع إلى قائمة السطح المنشور</b>. والشكل
/// مأخوذ حرفياً من <c>SalesSurface</c> و<c>PurchasingSurface</c>، وقبلهما
/// <c>Babel.Ledger.Audit</c>.
/// </para>
/// <para>
/// <b>وما لا يفعله هذا الملفّ — عمداً:</b> لا يُنفِذ استحقاقاً، ولا يقرّر شيئاً محاسبياً،
/// ولا يقرأ جدولاً. كلّ دالّة هنا تُترجم نوعاً منشوراً إلى مسوّدة الوحدة وتنادي خدمة
/// التطبيق التي تحمل <c>[RequiresEntitlement]</c> وتنادي المنفِّذ أوّل شيء (القاعدة 6).
/// </para>
/// <para>
/// <b>والمال يعبر هذا الحدّ <c>decimal</c> لا <c>Money</c>:</b> ‏<c>Money</c> يحمل عملةً،
/// وعملةُ المنشأة إعدادُ وحدةٍ لا معلومةُ نقل. <b>والكمّية تعبره ومعها وحدتها دائماً</b>
/// (<see cref="InventoryMeasure"/>): «عشرة» بلا وحدة ليست معلومة.
/// </para>
/// </summary>
public sealed class InventorySurface
{
    private readonly ItemCatalogueService _items;
    private readonly WarehouseCatalogueService _places;
    private readonly StockDocumentService _documents;
    private readonly StockMovementService _stock;
    private readonly InventoryValuationService _valuation;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ السطح فوق خدمات الوحدة.</summary>
    /// <param name="items">كتالوج الأصناف.</param>
    /// <param name="places">كتالوج المستودعات والمواقع.</param>
    /// <param name="documents">مستندات حركة المخزون.</param>
    /// <param name="stock">دفتر المخزون المساعد — الأرصدة.</param>
    /// <param name="valuation">المطابقة وجاهزية الإقفال.</param>
    /// <param name="options">إعدادات الوحدة — ومنها عملة المنشأة.</param>
    public InventorySurface(
        ItemCatalogueService items,
        WarehouseCatalogueService places,
        StockDocumentService documents,
        StockMovementService stock,
        InventoryValuationService valuation,
        InventoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(valuation);
        ArgumentNullException.ThrowIfNull(options);

        _items = items;
        _places = places;
        _documents = documents;
        _stock = stock;
        _valuation = valuation;
        _currency = CurrencyCode.FromString(options.CompanyCurrency);
    }

    /// <summary>يسجّل صنفاً جديداً بوحدة أساسه ومعاملات تحويله.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryItem>> AddItemAsync(
        TenantId tenant,
        UserId actor,
        InventoryItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<ItemView> result = await _items
            .CreateAsync(
                tenant,
                actor,
                new ItemDraft(
                    request.Code,
                    request.Name,
                    request.ItemGroup,
                    request.BaseUnit,
                    [.. request.Units.Select(static unit => new ItemUnitDraft(unit.UnitCode, unit.Numerator, unit.Denominator))]),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure ? Result<InventoryItem>.Failure(result.Errors) : Result<InventoryItem>.Success(Item(result.Value));
    }

    /// <summary>يقرأ صنفاً واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="itemId">معرّف الصنف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryItem>> ReadItemAsync(
        TenantId tenant,
        UserId actor,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        Result<ItemView> result = await _items.GetAsync(tenant, actor, itemId, cancellationToken).ConfigureAwait(false);
        return result.IsFailure ? Result<InventoryItem>.Failure(result.Errors) : Result<InventoryItem>.Success(Item(result.Value));
    }

    /// <summary>يقرأ أصناف المنشأة مرتَّبةً بالرمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryItem>>> ListItemsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<ItemView>> result = await _items.ListAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryItem>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryItem>>.Success([.. result.Value.Select(Item)]);
    }

    // ── كتالوج المستودعات والمواقع ───────────────────────────────────────────

    /// <summary>يسجّل مستودعاً جديداً بمؤهّل دوره.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryWarehouse>> AddWarehouseAsync(
        TenantId tenant,
        UserId actor,
        InventoryWarehouseRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<WarehouseView> result = await _places
            .CreateWarehouseAsync(
                tenant, actor, new WarehouseDraft(request.Code, request.Name, request.Qualifier), cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<InventoryWarehouse>.Failure(result.Errors)
            : Result<InventoryWarehouse>.Success(Warehouse(result.Value));
    }

    /// <summary>يقرأ مستودعاً واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryWarehouse>> ReadWarehouseAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        Result<WarehouseView> result = await _places
            .GetWarehouseAsync(tenant, actor, warehouseId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<InventoryWarehouse>.Failure(result.Errors)
            : Result<InventoryWarehouse>.Success(Warehouse(result.Value));
    }

    /// <summary>يقرأ مستودعات المنشأة — العاملة والمعطَّلة — مرتَّبةً بالرمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryWarehouse>>> ListWarehousesAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<WarehouseView>> result = await _places
            .ListWarehousesAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryWarehouse>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryWarehouse>>.Success([.. result.Value.Select(Warehouse)]);
    }

    /// <summary>يُعطّل مستودعاً أو يُعيد تفعيله — ولا يمسّ رصيداً ولا تاريخاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع.</param>
    /// <param name="active">الحالة المطلوبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryWarehouse>> SetWarehouseActiveAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        Result<WarehouseView> result = await _places
            .SetWarehouseActiveAsync(tenant, actor, warehouseId, active, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<InventoryWarehouse>.Failure(result.Errors)
            : Result<InventoryWarehouse>.Success(Warehouse(result.Value));
    }

    /// <summary>يسجّل موقعاً داخل مستودع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع المالك.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryLocation>> AddLocationAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        InventoryLocationRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<LocationView> result = await _places
            .CreateLocationAsync(
                tenant, actor, warehouseId, new LocationDraft(request.Code, request.Name), cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<InventoryLocation>.Failure(result.Errors)
            : Result<InventoryLocation>.Success(Location(result.Value));
    }

    /// <summary>يقرأ مواقع مستودعٍ واحد مرتَّبةً بالرمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryLocation>>> ListLocationsAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<LocationView>> result = await _places
            .ListLocationsAsync(tenant, actor, warehouseId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryLocation>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryLocation>>.Success([.. result.Value.Select(Location)]);
    }

    /// <summary>يُعطّل موقعاً داخل مستودعه أو يُعيد تفعيله.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع المالك.</param>
    /// <param name="locationId">معرّف الموقع.</param>
    /// <param name="active">الحالة المطلوبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryLocation>> SetLocationActiveAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        Guid locationId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        Result<LocationView> result = await _places
            .SetLocationActiveAsync(tenant, actor, warehouseId, locationId, active, cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<InventoryLocation>.Failure(result.Errors)
            : Result<InventoryLocation>.Success(Location(result.Value));
    }

    /// <summary>يُنشئ مستند حركة مخزون <b>مسوّدة</b>. لا حركة ولا قيد.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStockMovement>> DraftMovementAsync(
        TenantId tenant,
        UserId actor,
        InventoryStockMovementRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<StockDocumentView> result = await _documents
            .CreateAsync(
                tenant,
                actor,
                new StockDocumentDraft(
                    request.Number,
                    request.Direction,
                    request.ItemId,
                    request.WarehouseId,
                    request.LocationId,
                    request.ItemGroup,
                    new InventoryQuantity(request.Quantity.Magnitude, request.Quantity.Unit),
                    Money.Of(request.Cost, _currency),
                    request.OccurredOn),
                cancellationToken)
            .ConfigureAwait(false);

        return Movement(result);
    }

    /// <summary>يقرأ مستند حركة مخزون واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="movementId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStockMovement>> ReadMovementAsync(
        TenantId tenant,
        UserId actor,
        Guid movementId,
        CancellationToken cancellationToken = default)
    {
        Result<StockDocumentView> result = await _documents
            .GetAsync(tenant, actor, movementId, cancellationToken).ConfigureAwait(false);

        return Movement(result);
    }

    /// <summary>يقرأ مستندات حركة المخزون مرتَّبةً بالتاريخ ثم بالرقم.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryStockMovement>>> ListMovementsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<StockDocumentView>> result = await _documents
            .ListAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryStockMovement>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryStockMovement>>.Success([.. result.Value.Select(Movement)]);
    }

    /// <summary>
    /// يرحّل مستند حركة مسوّدة فيصير <b>واقعة</b>: حركةٌ في الدفتر المساعد وقيدٌ في الدفتر.
    /// حصين ضدّ التكرار: الوصول الثاني بالهوية نفسها يُرجع المستند ذاته و<c>AlreadyPosted = true</c>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="movementId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStockMovement>> PostMovementAsync(
        TenantId tenant,
        UserId actor,
        Guid movementId,
        CancellationToken cancellationToken = default)
    {
        Result<StockDocumentView> result = await _documents
            .PostAsync(tenant, actor, movementId, cancellationToken).ConfigureAwait(false);

        return Movement(result);
    }

    /// <summary>يقرأ أرصدة المخزون كلّها. نقطة قراءة بحتة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryBalance>>> ReadBalancesAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<StockBalanceView>> result = await _stock
            .ListStockAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryBalance>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryBalance>>.Success(
                [
                    .. result.Value.Select(static balance => new InventoryBalance(
                        balance.ItemId,
                        balance.WarehouseId,
                        balance.LocationId,
                        new InventoryMeasure(balance.Quantity.Magnitude, balance.Quantity.Unit),
                        balance.Value.Amount,
                        balance.UnitCost,
                        balance.HasCostBasis)),
                ]);
    }

    /// <summary>يقرأ تقييم المخزون ومطابقته بحسابه الضابط في تاريخ معلوم.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="asOf">تاريخ التقييم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryValuationReport>> ReadValuationAsync(
        TenantId tenant,
        UserId actor,
        DateOnly asOf,
        CancellationToken cancellationToken = default)
    {
        Result<ControlReconciliationReport> result = await _valuation
            .ReconcileAsync(tenant, actor, asOf, cancellationToken).ConfigureAwait(false);

        if (result.IsFailure)
        {
            return Result<InventoryValuationReport>.Failure(result.Errors);
        }

        ControlReconciliationReport report = result.Value;

        return Result<InventoryValuationReport>.Success(new InventoryValuationReport(
            report.AsOf,
            report.SubledgerTotal.Amount,
            report.ControlTotal.Amount,
            report.BalanceTotal.Amount,
            report.Divergence.Amount,
            report.IsReconciled,
            [
                .. report.Divergences.Select(static divergence => new InventoryDivergence(
                    divergence.DocumentType,
                    divergence.DocumentId,
                    divergence.ItemId,
                    divergence.SubledgerEffect.Amount,
                    divergence.ControlEffect.Amount,
                    divergence.Divergence.Amount,
                    divergence.ReasonCode)),
            ]));
    }

    private static InventoryWarehouse Warehouse(WarehouseView view) => new(
        view.Id, view.Code, view.Name, view.Qualifier, view.Origin, view.IsActive);

    private static InventoryLocation Location(LocationView view) => new(
        view.Id, view.WarehouseCode, view.Code, view.Name, view.Origin, view.IsActive);

    private static InventoryItem Item(ItemView view) => new(
        view.Id,
        view.Code,
        view.Name,
        view.ItemGroup,
        view.BaseUnit,
        [.. view.Units.Select(static unit => new InventoryUnitFactor(unit.UnitCode, unit.Numerator, unit.Denominator))]);

    private static Result<InventoryStockMovement> Movement(Result<StockDocumentView> result)
        => result.IsFailure
            ? Result<InventoryStockMovement>.Failure(result.Errors)
            : Result<InventoryStockMovement>.Success(Movement(result.Value));

    private static InventoryStockMovement Movement(StockDocumentView view) => new(
        view.Id,
        view.Number,
        view.State,
        view.Direction,
        view.ItemId,
        view.WarehouseId,
        view.LocationId,
        view.ItemGroup,
        new InventoryMeasure(view.Quantity.Magnitude, view.Quantity.Unit),
        view.Cost.Amount,
        view.OccurredOn,
        view.EntryId,
        view.AlreadyPosted);
}
