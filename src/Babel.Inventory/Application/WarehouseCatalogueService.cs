using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Inventory.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory.Application;

/// <summary>
/// كتالوج المستودعات والمواقع — <b>المكان يصير شيئاً موجوداً بدل أن يكون نصّاً حرّاً</b>.
/// <para>
/// <b>لماذا وُجد هذا الملفّ:</b> كان <c>WarehouseId</c> و<c>LocationId</c> عمودَي
/// <c>varchar(64)</c> بلا كتالوج ولا تحقّق ولا مفتاح خارجي. وخطأ إملائي واحد —
/// <c>WH-O1</c> بحرف O مكان الصفر — كان يفتح <b>رصيداً خامساً</b> يُطابَق تماماً
/// ويحمل قيمةً حقيقية لا يعرف أحدٌ أين هي، لأن المطابقة تجمع الحركات والأرصدة على
/// المفتاح نفسه فيتوازن الخطأ مع نفسه.
/// </para>
/// <para>
/// <b>وما لا يفعله هذا الكتالوج — عمداً:</b> لا يُعدّل رمزاً ولا يحذف صفّاً. الرمز هو
/// النصّ المكتوب في كل حركةٍ ورصيدٍ مضى، وتغييرُه يُيتّم كل صفٍّ يحمله على جدولٍ لا
/// مسار <c>UPDATE</c> إليه. والاسم والمؤهّل يُعدَّلان — <b>ولا مسار لهما في هذه
/// الدفعة</b>: نقصُ سطحٍ مُعلَن، لا قرار منع.
/// </para>
/// <para>
/// <b>ولا رقم حساب هنا</b> (القاعدة 3): المستودع يحمل <c>Qualifier</c> — مؤهّل دور —
/// ومصفوفة الترحيل وخريطة أدوارها وحدهما تُحوّلانه إلى حساب.
/// </para>
/// </summary>
public sealed class WarehouseCatalogueService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public WarehouseCatalogueService(IEntitlementEnforcer enforcer, InventoryRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // المستودعات
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>يسجّل مستودعاً جديداً — <c>Origin = DECLARED</c>: كتبه إنسان.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<WarehouseView>> CreateWarehouseAsync(
        TenantId tenant,
        UserId actor,
        WarehouseDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Warehouse.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<WarehouseView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(draft.Code))
        {
            return Result<WarehouseView>.Failure(InventoryErrors.CodeMissing("code"));
        }

        if (await _database.Warehouses
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<WarehouseView>.Failure(InventoryErrors.DuplicateWarehouseCode(draft.Code));
        }

        WarehouseRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            Qualifier = draft.Qualifier,
            Origin = CatalogueOrigin.Declared,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _database.Warehouses.Add(row);

        foreach (KeyValuePair<string, string> translation in draft.Name.Translations)
        {
            _database.WarehouseNames.Add(new WarehouseTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                WarehouseCode = draft.Code,
                LanguageTag = translation.Key,
                Text = translation.Value,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<WarehouseView>.Success(View(row, draft.Name));
    }

    /// <summary>يقرأ مستودعاً واحداً. نقطة قراءة: تعمل عند «للقراءة فقط» أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<WarehouseView>> GetWarehouseAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.Warehouse.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<WarehouseView>.Failure(gate.Errors);
        }

        WarehouseRow? row = await _database.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == warehouseId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<WarehouseView>.Failure(InventoryErrors.WarehouseNotFound(Identifier(warehouseId)))
            : Result<WarehouseView>.Success(View(row, await WarehouseNameAsync(tenant, row, cancellationToken).ConfigureAwait(false)));
    }

    /// <summary>
    /// يقرأ مستودعات المنشأة — <b>العاملة والمعطَّلة معاً</b>، مرتَّبةً بالرمز ترتيباً
    /// حرفياً ثابتاً لا ثقافياً (القاعدة 10).
    /// <para>
    /// والمعطَّل يخرج في القائمة موسوماً: إخفاؤه يجعل رصيداً قائماً بلا مستودعٍ يُفسّره
    /// في الشاشة، وهو أسوأ من صفٍّ مكتوب عليه «معطَّل».
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<WarehouseView>>> ListWarehousesAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.Warehouse.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<WarehouseView>>.Failure(gate.Errors);
        }

        List<WarehouseRow> rows = await _database.Warehouses
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<WarehouseTranslationRow> names = await _database.WarehouseNames
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<WarehouseView> views =
        [
            .. rows
                .OrderBy(static row => row.Code, StringComparer.Ordinal)
                .Select(row => View(row, Named(
                    row.NameAr,
                    names.Where(name => string.Equals(name.WarehouseCode, row.Code, StringComparison.Ordinal))))),
        ];

        return Result<IReadOnlyList<WarehouseView>>.Success(views);
    }

    /// <summary>
    /// يُعطّل مستودعاً أو يُعيد تفعيله.
    /// <para>
    /// <b>والتعطيل يمنع المسوّدات الجديدة ولا يمسّ التاريخ:</b> الأرصدة تُقرأ وتُطابَق
    /// وتُقفَل كما كانت. ويُرفض إن بقيت فيه بضاعة — <b>مُسمّياً الصفوف التي تحملها</b> —
    /// لأن التعطيل يغلق كل باب مستندٍ يُخرجها، فتبقى قيمةٌ في الميزانية بلا مخرج.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع.</param>
    /// <param name="active">الحالة المطلوبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<WarehouseView>> SetWarehouseActiveAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Warehouse.SetActive", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<WarehouseView>.Failure(gate.Errors);
        }

        WarehouseRow? row = await _database.Warehouses
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == warehouseId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<WarehouseView>.Failure(InventoryErrors.WarehouseNotFound(Identifier(warehouseId)));
        }

        if (!active)
        {
            IReadOnlyList<string> holdings = await HoldingsAsync(
                tenant, row.Code, location: null, cancellationToken).ConfigureAwait(false);

            if (holdings.Count > 0)
            {
                return Result<WarehouseView>.Failure(InventoryErrors.WarehouseHasStock(row.Code, holdings));
            }
        }

        row.IsActive = active;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<WarehouseView>.Success(
            View(row, await WarehouseNameAsync(tenant, row, cancellationToken).ConfigureAwait(false)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // المواقع — <b>مورد فرعي</b>: رمزُ موقعٍ بلا مستودعه ليس هوية
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>يسجّل موقعاً داخل مستودع.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع المالك.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<LocationView>> CreateLocationAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        LocationDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Location.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<LocationView>.Failure(gate.Errors);
        }

        WarehouseRow? warehouse = await _database.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == warehouseId, cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is null)
        {
            return Result<LocationView>.Failure(InventoryErrors.WarehouseNotFound(Identifier(warehouseId)));
        }

        if (!warehouse.IsActive)
        {
            return Result<LocationView>.Failure(InventoryErrors.WarehouseInactive(warehouse.Code));
        }

        if (string.IsNullOrWhiteSpace(draft.Code))
        {
            return Result<LocationView>.Failure(InventoryErrors.CodeMissing("code"));
        }

        if (await _database.Locations
                .AnyAsync(
                    row => row.TenantId == tenant.Value
                        && row.WarehouseCode == warehouse.Code
                        && row.Code == draft.Code,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<LocationView>.Failure(
                InventoryErrors.DuplicateLocationCode(warehouse.Code, draft.Code));
        }

        LocationRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            WarehouseCode = warehouse.Code,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            Origin = CatalogueOrigin.Declared,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _database.Locations.Add(row);

        foreach (KeyValuePair<string, string> translation in draft.Name.Translations)
        {
            _database.LocationNames.Add(new LocationTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                WarehouseCode = warehouse.Code,
                LocationCode = draft.Code,
                LanguageTag = translation.Key,
                Text = translation.Value,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<LocationView>.Success(View(row, draft.Name));
    }

    /// <summary>يقرأ مواقع مستودعٍ واحد مرتَّبةً بالرمز — العاملة والمعطَّلة معاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<LocationView>>> ListLocationsAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.Location.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<LocationView>>.Failure(gate.Errors);
        }

        WarehouseRow? warehouse = await _database.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == warehouseId, cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is null)
        {
            return Result<IReadOnlyList<LocationView>>.Failure(
                InventoryErrors.WarehouseNotFound(Identifier(warehouseId)));
        }

        List<LocationRow> rows = await _database.Locations
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.WarehouseCode == warehouse.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<LocationTranslationRow> names = await _database.LocationNames
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.WarehouseCode == warehouse.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<LocationView> views =
        [
            .. rows
                .OrderBy(static row => row.Code, StringComparer.Ordinal)
                .Select(row => View(row, Named(
                    row.NameAr,
                    names.Where(name => string.Equals(name.LocationCode, row.Code, StringComparison.Ordinal))))),
        ];

        return Result<IReadOnlyList<LocationView>>.Success(views);
    }

    /// <summary>يُعطّل موقعاً داخل مستودعه أو يُعيد تفعيله.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="warehouseId">معرّف المستودع المالك.</param>
    /// <param name="locationId">معرّف الموقع.</param>
    /// <param name="active">الحالة المطلوبة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<LocationView>> SetLocationActiveAsync(
        TenantId tenant,
        UserId actor,
        Guid warehouseId,
        Guid locationId,
        bool active,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Location.SetActive", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<LocationView>.Failure(gate.Errors);
        }

        WarehouseRow? warehouse = await _database.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == warehouseId, cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is null)
        {
            return Result<LocationView>.Failure(InventoryErrors.WarehouseNotFound(Identifier(warehouseId)));
        }

        // ‏**الزوج هو الهوية**: موقعٌ بهذا المعرّف في مستودعٍ آخر ليس موقع هذا المسار،
        // والرفض يقول ذلك بدل أن يعمل على الصفّ الخطأ.
        LocationRow? row = await _database.Locations
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value
                       && entity.Id == locationId
                       && entity.WarehouseCode == warehouse.Code,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<LocationView>.Failure(
                InventoryErrors.LocationNotInWarehouse(warehouse.Code, Identifier(locationId)));
        }

        if (!active)
        {
            IReadOnlyList<string> holdings = await HoldingsAsync(
                tenant, warehouse.Code, row.Code, cancellationToken).ConfigureAwait(false);

            if (holdings.Count > 0)
            {
                return Result<LocationView>.Failure(
                    InventoryErrors.LocationHasStock(warehouse.Code, row.Code, holdings));
            }
        }

        row.IsActive = active;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<LocationView>.Success(
            View(row, await LocationNameAsync(tenant, row, cancellationToken).ConfigureAwait(false)));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // البوّابة التي يناديها إنشاء المسوّدة — <b>عند الإنشاء لا عند الترحيل</b>
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// يتحقّق أن (مستودع، موقع) زوجٌ مسجَّل وعامل — <b>قبل أن يُكتب صفّ المسوّدة</b>.
    /// <para>
    /// <b>ولماذا هنا لا عند الترحيل ولا في القاعدة:</b> الترحيل يرفض مستنداً كُتب سلفاً
    /// فيتركه عالقاً بلا مخرج؛ والمفتاح الخارجي يُصادق تاريخاً حرّاً كُتب قبل وجود
    /// الكتالوج فيُسقط الهجرة على خطأٍ إملائي لا شيء يُصلحه على دفترٍ يُضاف إليه فقط.
    /// </para>
    /// <para>
    /// <b>ولا استحقاق يُنفَّذ هنا:</b> المنادي خدمةُ تطبيقٍ نفَّذته أوّل شيء (القاعدة 6)،
    /// وتنفيذٌ ثانٍ داخل المسار الواحد يُوهم أن الفحص في موضعين وهو في واحد.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="warehouseCode">رمز المستودع كما سُلّم.</param>
    /// <param name="locationCode">رمز الموقع كما سُلّم.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    internal async ValueTask<Result> EnsurePlaceIsRegisteredAsync(
        TenantId tenant,
        string warehouseCode,
        string locationCode,
        CancellationToken cancellationToken = default)
    {
        // ‏**والفارغ يُرفض قبل أن يُبحث عنه:** لولا ذلك لكان نصٌّ فارغ في المسوّدة يبحث
        // عن صفٍّ برمزٍ فارغ — وهو صفٌّ لا تُنتجه الهجرة ولا هذا السطح، فيُرفض بـ«غير
        // موجود» ويقرؤه المستخدم «سجّل مستودعاً اسمه فراغ».
        if (string.IsNullOrWhiteSpace(warehouseCode))
        {
            return Result.Failure(InventoryErrors.CodeMissing("warehouseId"));
        }

        if (string.IsNullOrWhiteSpace(locationCode))
        {
            return Result.Failure(InventoryErrors.CodeMissing("locationId"));
        }

        WarehouseRow? warehouse = await _database.Warehouses
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.Code == warehouseCode, cancellationToken)
            .ConfigureAwait(false);

        if (warehouse is null)
        {
            return Result.Failure(InventoryErrors.WarehouseNotFound(warehouseCode));
        }

        if (!warehouse.IsActive)
        {
            return Result.Failure(InventoryErrors.WarehouseInactive(warehouse.Code));
        }

        LocationRow? location = await _database.Locations
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value
                    && row.WarehouseCode == warehouseCode
                    && row.Code == locationCode,
                cancellationToken)
            .ConfigureAwait(false);

        if (location is null)
        {
            return Result.Failure(InventoryErrors.LocationNotInWarehouse(warehouseCode, locationCode));
        }

        return location.IsActive
            ? Result.Success()
            : Result.Failure(InventoryErrors.LocationInactive(warehouseCode, locationCode));
    }

    // ═══════════════════════════════════════════════════════════════════════
    // مشترَك
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// صفوف الرصيد التي تحمل كمّيةً أو قيمةً في هذا المستودع — وفي هذا الموقع إن سُمّي.
    /// <para>
    /// <b>والشرط «أو» لا «و»:</b> رصيدٌ بكمّية صفر وقيمةٍ غير صفرية واقعةٌ مسجّلة
    /// (صرفٌ زائد قيّمه متوسّطٌ ناقضته حركةٌ لاحقة)، وتعطيلُ مكانه يُخفي رقماً في
    /// الميزانية لا مستند يُخرجه.
    /// </para>
    /// </summary>
    private async ValueTask<IReadOnlyList<string>> HoldingsAsync(
        TenantId tenant, string warehouseCode, string? location, CancellationToken cancellationToken)
    {
        List<ItemBalanceRow> rows = await _database.Balances
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                       && row.WarehouseId == warehouseCode
                       && (location == null || row.LocationId == location)
                       && (row.Quantity != 0m || row.ValueAmount != 0m))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return
        [
            .. rows
                .OrderBy(static row => row.ItemId, StringComparer.Ordinal)
                .ThenBy(static row => row.LocationId, StringComparer.Ordinal)
                .Select(static row => InventoryErrors.Holding(
                    row.ItemId, row.WarehouseId, row.LocationId, row.Quantity, row.ValueAmount)),
        ];
    }

    private async Task<TranslatedName> WarehouseNameAsync(
        TenantId tenant, WarehouseRow row, CancellationToken cancellationToken)
    {
        List<WarehouseTranslationRow> names = await _database.WarehouseNames
            .AsNoTracking()
            .Where(name => name.TenantId == tenant.Value && name.WarehouseCode == row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Named(row.NameAr, names);
    }

    private async Task<TranslatedName> LocationNameAsync(
        TenantId tenant, LocationRow row, CancellationToken cancellationToken)
    {
        List<LocationTranslationRow> names = await _database.LocationNames
            .AsNoTracking()
            .Where(name => name.TenantId == tenant.Value
                        && name.WarehouseCode == row.WarehouseCode
                        && name.LocationCode == row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Named(row.NameAr, names);
    }

    /// <summary>
    /// الاسم العربي سجلّاً وترجماته صفوفاً — <b>ولا عمود لغةٍ ثابت</b> (ADR-0021 ·
    /// القاعدة 14). و«لا ترجمة» تُقرأ من غياب الصفّ، لا من نصٍّ فارغ في عمود.
    /// </summary>
    private static TranslatedName Named(string arabic, IEnumerable<WarehouseTranslationRow> names)
    {
        Dictionary<string, string> translations = new(StringComparer.Ordinal);
        foreach (WarehouseTranslationRow name in names)
        {
            translations[name.LanguageTag] = name.Text;
        }

        return new TranslatedName(arabic, translations);
    }

    private static TranslatedName Named(string arabic, IEnumerable<LocationTranslationRow> names)
    {
        Dictionary<string, string> translations = new(StringComparer.Ordinal);
        foreach (LocationTranslationRow name in names)
        {
            translations[name.LanguageTag] = name.Text;
        }

        return new TranslatedName(arabic, translations);
    }

    private static WarehouseView View(WarehouseRow row, TranslatedName name)
        => new(row.Id, row.Code, name, row.Qualifier, row.Origin, row.IsActive);

    private static LocationView View(LocationRow row, TranslatedName name)
        => new(row.Id, row.WarehouseCode, row.Code, name, row.Origin, row.IsActive);

    private static string Identifier(Guid value)
        => value.ToString("D", System.Globalization.CultureInfo.InvariantCulture);
}
