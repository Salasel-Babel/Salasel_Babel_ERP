using Babel.Contracts.Inventory;
using Babel.Inventory.Application;
using Babel.Inventory.Persistence;
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
    private readonly StockDocumentService _documents;
    private readonly StockMovementService _stock;
    private readonly InventoryValuationService _valuation;
    private readonly StoragePlaceService _places;
    private readonly StockTransferService _transfers;
    private readonly UnitOfMeasureService _units;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ السطح فوق خدمات الوحدة.</summary>
    /// <param name="items">كتالوج الأصناف.</param>
    /// <param name="documents">مستندات حركة المخزون.</param>
    /// <param name="stock">دفتر المخزون المساعد — الأرصدة.</param>
    /// <param name="valuation">المطابقة وجاهزية الإقفال.</param>
    /// <param name="places">سجلّ التسكين — المستودع والموقع والرفّ.</param>
    /// <param name="transfers">النقل بين موقعين.</param>
    /// <param name="units">سجلّ وحدات القياس ومعاملات التحويل.</param>
    /// <param name="options">إعدادات الوحدة — ومنها عملة المنشأة.</param>
    public InventorySurface(
        ItemCatalogueService items,
        StockDocumentService documents,
        StockMovementService stock,
        InventoryValuationService valuation,
        StoragePlaceService places,
        StockTransferService transfers,
        UnitOfMeasureService units,
        InventoryOptions options)
    {
        ArgumentNullException.ThrowIfNull(items);
        ArgumentNullException.ThrowIfNull(documents);
        ArgumentNullException.ThrowIfNull(stock);
        ArgumentNullException.ThrowIfNull(valuation);
        ArgumentNullException.ThrowIfNull(places);
        ArgumentNullException.ThrowIfNull(transfers);
        ArgumentNullException.ThrowIfNull(units);
        ArgumentNullException.ThrowIfNull(options);

        _items = items;
        _documents = documents;
        _stock = stock;
        _valuation = valuation;
        _places = places;
        _transfers = transfers;
        _units = units;
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

    // ── التسكين: مستودع ← موقع ← رفّ ─────────────────────────────────────────
    // و**الثلاثة شكلٌ واحد بخمس عمليات**: تسجيل · قراءة · سرد · إعادة تسمية · تعطيل.
    // ولا `PUT` ولا `DELETE` على أيٍّ منها: الرمز محمولٌ على حركات مضت، وحذفُه يجعل
    // كل حركة عليه بلا موضع يُقرأ. والتعطيل حالةٌ تُقرأ، لا غياب.

    /// <summary>يسجّل مستودعاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> AddWarehouseAsync(
        TenantId tenant, UserId actor, InventoryStoragePlaceRequest request, CancellationToken cancellationToken = default)
        => AddPlaceAsync(tenant, actor, PlacementLevel.Warehouse, null, request, cancellationToken);

    /// <summary>يقرأ مستودعاً واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> ReadWarehouseAsync(
        TenantId tenant, UserId actor, Guid warehouseId, CancellationToken cancellationToken = default)
        => ReadPlaceAsync(tenant, actor, PlacementLevel.Warehouse, null, warehouseId, cancellationToken);

    /// <summary>يقرأ مستودعات المنشأة مرتَّبةً بالرمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<IReadOnlyList<InventoryStoragePlace>>> ListWarehousesAsync(
        TenantId tenant, UserId actor, CancellationToken cancellationToken = default)
        => ListPlacesAsync(tenant, actor, PlacementLevel.Warehouse, null, cancellationToken);

    /// <summary>يعيد تسمية مستودع — الاسم وحده، ولا رمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع.</param>
    /// <param name="request">الاسم الجديد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> RenameWarehouseAsync(
        TenantId tenant, UserId actor, Guid warehouseId, InventoryPlaceNameRequest request, CancellationToken cancellationToken = default)
        => RenamePlaceAsync(tenant, actor, PlacementLevel.Warehouse, null, warehouseId, request, cancellationToken);

    /// <summary>يعطّل مستودعاً — ويُرفض التعطيل إن بقي فيه رصيد أو موقعٌ عامل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> DeactivateWarehouseAsync(
        TenantId tenant, UserId actor, Guid warehouseId, CancellationToken cancellationToken = default)
        => DeactivatePlaceAsync(tenant, actor, PlacementLevel.Warehouse, null, warehouseId, cancellationToken);

    /// <summary>يسجّل موقعاً داخل مستودع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الأب.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> AddStorageLocationAsync(
        TenantId tenant, UserId actor, Guid warehouseId, InventoryStoragePlaceRequest request, CancellationToken cancellationToken = default)
        => AddPlaceAsync(tenant, actor, PlacementLevel.Location, warehouseId, request, cancellationToken);

    /// <summary>يقرأ موقعاً واحداً — ويُتحقَّق أنه يقع في المستودع المذكور.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الأب.</param>
    /// <param name="locationId">الموقع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> ReadStorageLocationAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, CancellationToken cancellationToken = default)
        => ReadPlaceAsync(tenant, actor, PlacementLevel.Location, warehouseId, locationId, cancellationToken);

    /// <summary>يقرأ مواقع مستودع مرتَّبةً بالرمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الأب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<IReadOnlyList<InventoryStoragePlace>>> ListStorageLocationsAsync(
        TenantId tenant, UserId actor, Guid warehouseId, CancellationToken cancellationToken = default)
        => ListPlacesAsync(tenant, actor, PlacementLevel.Location, warehouseId, cancellationToken);

    /// <summary>يعيد تسمية موقع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الأب.</param>
    /// <param name="locationId">الموقع.</param>
    /// <param name="request">الاسم الجديد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> RenameStorageLocationAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, InventoryPlaceNameRequest request, CancellationToken cancellationToken = default)
        => RenamePlaceAsync(tenant, actor, PlacementLevel.Location, warehouseId, locationId, request, cancellationToken);

    /// <summary>يعطّل موقعاً — ويُرفض التعطيل إن بقي فيه رصيد أو رفٌّ عامل.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الأب.</param>
    /// <param name="locationId">الموقع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public ValueTask<Result<InventoryStoragePlace>> DeactivateStorageLocationAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, CancellationToken cancellationToken = default)
        => DeactivatePlaceAsync(tenant, actor, PlacementLevel.Location, warehouseId, locationId, cancellationToken);

    // ── الأرفف: ثلاثة أضلاع في المسار، **وثلاثتها تُتحقَّق** ──────────────────
    // مسار الرفّ يذكر المستودع والموقع والرفّ. والتحقّق من الضلعين الأولين معاً ليس
    // ترفاً: بدونه يُقرأ رفٌّ من الموقع «‏A» عبر مسارٍ يذكر المستودع «‏B» فيخرج وكأنه
    // فيه. ولذلك يُتحقَّق أوّلاً أن الموقع في مستودعه، ثم يُعمَل على الرفّ تحت موقعه.

    /// <summary>يسجّل رفّاً داخل موقع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الجدّ — يُتحقَّق أن الموقع فيه.</param>
    /// <param name="locationId">الموقع الأب.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStoragePlace>> AddStorageBinAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, InventoryStoragePlaceRequest request, CancellationToken cancellationToken = default)
    {
        Result chain = await InLocationAsync(tenant, actor, warehouseId, locationId, cancellationToken).ConfigureAwait(false);
        return chain.IsFailure
            ? Result<InventoryStoragePlace>.Failure(chain.Errors)
            : await AddPlaceAsync(tenant, actor, PlacementLevel.Bin, locationId, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يقرأ رفّاً واحداً — ويُتحقَّق من سلسلة مستودعه وموقعه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الجدّ.</param>
    /// <param name="locationId">الموقع الأب.</param>
    /// <param name="binId">الرفّ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStoragePlace>> ReadStorageBinAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, Guid binId, CancellationToken cancellationToken = default)
    {
        Result chain = await InLocationAsync(tenant, actor, warehouseId, locationId, cancellationToken).ConfigureAwait(false);
        return chain.IsFailure
            ? Result<InventoryStoragePlace>.Failure(chain.Errors)
            : await ReadPlaceAsync(tenant, actor, PlacementLevel.Bin, locationId, binId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يقرأ أرفف موقعٍ مرتَّبةً بالرمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الجدّ.</param>
    /// <param name="locationId">الموقع الأب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryStoragePlace>>> ListStorageBinsAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, CancellationToken cancellationToken = default)
    {
        Result chain = await InLocationAsync(tenant, actor, warehouseId, locationId, cancellationToken).ConfigureAwait(false);
        return chain.IsFailure
            ? Result<IReadOnlyList<InventoryStoragePlace>>.Failure(chain.Errors)
            : await ListPlacesAsync(tenant, actor, PlacementLevel.Bin, locationId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يعيد تسمية رفّاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الجدّ.</param>
    /// <param name="locationId">الموقع الأب.</param>
    /// <param name="binId">الرفّ.</param>
    /// <param name="request">الاسم الجديد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStoragePlace>> RenameStorageBinAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, Guid binId, InventoryPlaceNameRequest request, CancellationToken cancellationToken = default)
    {
        Result chain = await InLocationAsync(tenant, actor, warehouseId, locationId, cancellationToken).ConfigureAwait(false);
        return chain.IsFailure
            ? Result<InventoryStoragePlace>.Failure(chain.Errors)
            : await RenamePlaceAsync(tenant, actor, PlacementLevel.Bin, locationId, binId, request, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// يعطّل رفّاً.
    /// <para>
    /// <b>ولا فحص رصيدٍ عليه</b>: الرفّ ليس بُعداً في مفتاح الرصيد (‏ADR تسكين
    /// المخزون)، فلا صفَّ رصيدٍ يُقرأ عنه. وفحصٌ يبحث عنه كان يُرجع «لا رصيد» دائماً
    /// ويبدو حارساً وهو لا يحرس شيئاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">المستودع الجدّ.</param>
    /// <param name="locationId">الموقع الأب.</param>
    /// <param name="binId">الرفّ.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStoragePlace>> DeactivateStorageBinAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, Guid binId, CancellationToken cancellationToken = default)
    {
        Result chain = await InLocationAsync(tenant, actor, warehouseId, locationId, cancellationToken).ConfigureAwait(false);
        return chain.IsFailure
            ? Result<InventoryStoragePlace>.Failure(chain.Errors)
            : await DeactivatePlaceAsync(tenant, actor, PlacementLevel.Bin, locationId, binId, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>يتحقّق أن الموقع المذكور في المسار يقع في المستودع المذكور فيه.</summary>
    private async ValueTask<Result> InLocationAsync(
        TenantId tenant, UserId actor, Guid warehouseId, Guid locationId, CancellationToken cancellationToken)
    {
        Result<StoragePlaceView> location = await _places
            .GetAsync(tenant, actor, PlacementLevel.Location, warehouseId, locationId, cancellationToken)
            .ConfigureAwait(false);

        return location.IsFailure ? Result.Failure(location.Errors) : Result.Success();
    }

    /// <summary>
    /// يقرأ الأرصدة <b>بتسكينها</b>: الرصيد ومعه اسم مستودعه واسم موقعه من السجلّ،
    /// ووسمُ ما ليس مسجَّلاً.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryPlacementBalance>>> ReadPlacementBalancesAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<PlacementBalanceView>> result = await _places
            .ListPlacementBalancesAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryPlacementBalance>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryPlacementBalance>>.Success(
                [
                    .. result.Value.Select(static balance => new InventoryPlacementBalance(
                        balance.ItemId,
                        balance.WarehouseId,
                        balance.WarehouseName,
                        balance.WarehouseRegistered,
                        balance.LocationId,
                        balance.LocationName,
                        balance.LocationRegistered,
                        new InventoryMeasure(balance.Quantity.Magnitude, balance.Quantity.Unit),
                        balance.Value.Amount,
                        balance.UnitCost,
                        balance.HasCostBasis)),
                ]);
    }

    /// <summary>يُنشئ مستند نقلٍ بين موقعين <b>مسوّدة</b>. لا حركة ولا رصيد يتغيّر.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStockTransfer>> DraftTransferAsync(
        TenantId tenant,
        UserId actor,
        InventoryStockTransferRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<StockTransferView> result = await _transfers
            .CreateAsync(
                tenant,
                actor,
                new StockTransferDraft(
                    request.Number,
                    request.ItemId,
                    request.ItemGroup,
                    request.FromWarehouseId,
                    request.FromLocationId,
                    request.ToWarehouseId,
                    request.ToLocationId,
                    new InventoryQuantity(request.Quantity.Magnitude, request.Quantity.Unit),
                    request.OccurredOn),
                cancellationToken)
            .ConfigureAwait(false);

        return Transfer(result);
    }

    /// <summary>يقرأ مستند نقلٍ واحداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="transferId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStockTransfer>> ReadTransferAsync(
        TenantId tenant,
        UserId actor,
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        Result<StockTransferView> result = await _transfers
            .GetAsync(tenant, actor, transferId, cancellationToken).ConfigureAwait(false);

        return Transfer(result);
    }

    /// <summary>يقرأ مستندات النقل مرتَّبةً بالتاريخ ثم بالرقم.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryStockTransfer>>> ListTransfersAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<StockTransferView>> result = await _transfers
            .ListAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryStockTransfer>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryStockTransfer>>.Success([.. result.Value.Select(Transfer)]);
    }

    /// <summary>
    /// ينفّذ النقل: صادرٌ من المصدر ثم واردٌ إلى الوجهة بالقيمة نفسها — <b>ولا قيد</b>.
    /// حصينٌ ضد التكرار: الوصول الثاني بالهوية نفسها يُعيد المستند ذاته و<c>AlreadyMoved = true</c>.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="transferId">المستند.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryStockTransfer>> MoveTransferAsync(
        TenantId tenant,
        UserId actor,
        Guid transferId,
        CancellationToken cancellationToken = default)
    {
        Result<StockTransferView> result = await _transfers
            .MoveAsync(tenant, actor, transferId, cancellationToken).ConfigureAwait(false);

        return Transfer(result);
    }

    private async ValueTask<Result<InventoryStoragePlace>> AddPlaceAsync(
        TenantId tenant,
        UserId actor,
        string level,
        Guid? parentId,
        InventoryStoragePlaceRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<StoragePlaceView> result = await _places
            .CreateAsync(tenant, actor, level, parentId, new StoragePlaceDraft(request.Code, request.Name), cancellationToken)
            .ConfigureAwait(false);

        return Place(result);
    }

    private async ValueTask<Result<InventoryStoragePlace>> ReadPlaceAsync(
        TenantId tenant, UserId actor, string level, Guid? parentId, Guid placeId, CancellationToken cancellationToken)
    {
        Result<StoragePlaceView> result = await _places
            .GetAsync(tenant, actor, level, parentId, placeId, cancellationToken).ConfigureAwait(false);

        return Place(result);
    }

    private async ValueTask<Result<IReadOnlyList<InventoryStoragePlace>>> ListPlacesAsync(
        TenantId tenant, UserId actor, string level, Guid? parentId, CancellationToken cancellationToken)
    {
        Result<IReadOnlyList<StoragePlaceView>> result = await _places
            .ListAsync(tenant, actor, level, parentId, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryStoragePlace>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryStoragePlace>>.Success([.. result.Value.Select(Place)]);
    }

    private async ValueTask<Result<InventoryStoragePlace>> RenamePlaceAsync(
        TenantId tenant,
        UserId actor,
        string level,
        Guid? parentId,
        Guid placeId,
        InventoryPlaceNameRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<StoragePlaceView> result = await _places
            .RenameAsync(tenant, actor, level, parentId, placeId, request.Name, cancellationToken)
            .ConfigureAwait(false);

        return Place(result);
    }

    private async ValueTask<Result<InventoryStoragePlace>> DeactivatePlaceAsync(
        TenantId tenant, UserId actor, string level, Guid? parentId, Guid placeId, CancellationToken cancellationToken)
    {
        Result<StoragePlaceView> result = await _places
            .DeactivateAsync(tenant, actor, level, parentId, placeId, cancellationToken).ConfigureAwait(false);

        return Place(result);
    }

    private static Result<InventoryStoragePlace> Place(Result<StoragePlaceView> result)
        => result.IsFailure
            ? Result<InventoryStoragePlace>.Failure(result.Errors)
            : Result<InventoryStoragePlace>.Success(Place(result.Value));

    private static InventoryStoragePlace Place(StoragePlaceView view) => new(
        view.Id, view.Level, view.Code, view.Name, view.ParentCode, view.IsActive);

    private static Result<InventoryStockTransfer> Transfer(Result<StockTransferView> result)
        => result.IsFailure
            ? Result<InventoryStockTransfer>.Failure(result.Errors)
            : Result<InventoryStockTransfer>.Success(Transfer(result.Value));

    private static InventoryStockTransfer Transfer(StockTransferView view) => new(
        view.Id,
        view.Number,
        view.State,
        view.ItemId,
        view.ItemGroup,
        view.FromWarehouseId,
        view.FromLocationId,
        view.ToWarehouseId,
        view.ToLocationId,
        new InventoryMeasure(view.Quantity.Magnitude, view.Quantity.Unit),
        view.Value.Amount,
        view.OccurredOn,
        view.AlreadyMoved);

    // ── وحدات القياس ومعاملات التحويل ────────────────────────────────────────

    /// <summary>يسجّل وحدة قياس بصنف كمّيتها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryUnit>> AddUnitAsync(
        TenantId tenant,
        UserId actor,
        InventoryUnitRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<UnitOfMeasureView> result = await _units
            .CreateAsync(
                tenant,
                actor,
                new UnitOfMeasureDraft(request.Code, request.Name, request.QuantityClass),
                cancellationToken)
            .ConfigureAwait(false);

        return Unit(result);
    }

    /// <summary>يقرأ وحدة قياس واحدة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="unitId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryUnit>> ReadUnitAsync(
        TenantId tenant,
        UserId actor,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        Result<UnitOfMeasureView> result = await _units
            .GetAsync(tenant, actor, unitId, cancellationToken).ConfigureAwait(false);

        return Unit(result);
    }

    /// <summary>يقرأ وحدات المنشأة مرتَّبةً بالرمز.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryUnit>>> ListUnitsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<UnitOfMeasureView>> result = await _units
            .ListAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryUnit>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryUnit>>.Success([.. result.Value.Select(Unit)]);
    }

    /// <summary>يعطّل وحدة قياس — ولا يحذفها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="unitId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryUnit>> DeactivateUnitAsync(
        TenantId tenant,
        UserId actor,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        Result<UnitOfMeasureView> result = await _units
            .DeactivateAsync(tenant, actor, unitId, cancellationToken).ConfigureAwait(false);

        return Unit(result);
    }

    /// <summary>يسجّل معامل تحويل بين وحدتين — ويرفض ما بين صنفين مختلفين.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryUnitConversion>> AddUnitConversionAsync(
        TenantId tenant,
        UserId actor,
        InventoryUnitConversionRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<UnitConversionView> result = await _units
            .CreateConversionAsync(
                tenant,
                actor,
                new UnitConversionDraft(request.FromUnit, request.ToUnit, request.Numerator, request.Denominator),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<InventoryUnitConversion>.Failure(result.Errors)
            : Result<InventoryUnitConversion>.Success(Conversion(result.Value));
    }

    /// <summary>يقرأ معاملات التحويل مرتَّبةً بالوحدتين.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<IReadOnlyList<InventoryUnitConversion>>> ListUnitConversionsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result<IReadOnlyList<UnitConversionView>> result = await _units
            .ListConversionsAsync(tenant, actor, cancellationToken).ConfigureAwait(false);

        return result.IsFailure
            ? Result<IReadOnlyList<InventoryUnitConversion>>.Failure(result.Errors)
            : Result<IReadOnlyList<InventoryUnitConversion>>.Success([.. result.Value.Select(Conversion)]);
    }

    /// <summary>
    /// <b>مسبار التحويل</b>: يحوّل كمّيةً ولا يكتب شيئاً — الناتج الدقيق أو الرفض المُسمّى.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="request">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    public async ValueTask<Result<InventoryConversionResult>> ConvertQuantityAsync(
        TenantId tenant,
        UserId actor,
        InventoryConversionTrialRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        Result<UnitConversionResult> result = await _units
            .ConvertAsync(
                tenant,
                actor,
                new UnitConversionTrial(
                    new InventoryQuantity(request.Quantity.Magnitude, request.Quantity.Unit), request.ToUnit),
                cancellationToken)
            .ConfigureAwait(false);

        return result.IsFailure
            ? Result<InventoryConversionResult>.Failure(result.Errors)
            : Result<InventoryConversionResult>.Success(new InventoryConversionResult(
                new InventoryMeasure(result.Value.From.Magnitude, result.Value.From.Unit),
                new InventoryMeasure(result.Value.To.Magnitude, result.Value.To.Unit),
                result.Value.Numerator,
                result.Value.Denominator,
                result.Value.QuantityClass));
    }

    private static Result<InventoryUnit> Unit(Result<UnitOfMeasureView> result)
        => result.IsFailure
            ? Result<InventoryUnit>.Failure(result.Errors)
            : Result<InventoryUnit>.Success(Unit(result.Value));

    private static InventoryUnit Unit(UnitOfMeasureView view) =>
        new(view.Id, view.Code, view.Name, view.QuantityClass, view.IsActive);

    private static InventoryUnitConversion Conversion(UnitConversionView view) =>
        new(view.Id, view.FromUnit, view.ToUnit, view.QuantityClass, view.Numerator, view.Denominator);

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
