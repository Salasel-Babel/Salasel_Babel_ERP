using Babel.Contracts.Inventory;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Inventory.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory.Application;

/// <summary>
/// <b>سجلّ التسكين</b>: أين تقع القطعة — مستودعٌ ← موقعٌ ← رفّ.
/// <para>
/// <b>وهو سجلٌّ يصف ولا يُبطل.</b> الحركات والأرصدة القائمة تحمل رموز مواضع كُتبت
/// قبل أن يوجد هذا السجلّ (‏<c>DEFAULT</c> وما شابهه، هجرة 001)، ولا مفتاح خارجي
/// منها إليه. فرمزٌ غير مسجَّل <b>يبقى عاملاً ويُوسَم عند القراءة</b>، ولا تُعاد
/// كتابة حركةٍ مضت لتوافق سجلّاً وُلد بعدها — وهي إعادةُ الكتابة نفسها التي يمنعها
/// ‏ADR-0002 على الدفتر المساعد.
/// </para>
/// <para>
/// <b>ومستوى الرصيد هو الموقع، والرفّ ليس بُعد تقييم</b> (‏ADR تسكين المخزون):
/// المتوسط المرجّح المتحرّك مثبَّتٌ عند (منشأة × صنف × مستودع) بـADR-0039، وتفريعُ
/// القيمة على الأرفف يجعل نقل كرتونٍ بين رفّين حدثاً ذا قيمة، ومجموعَ متوسطات
/// الأرفف لا يساوي متوسط المستودع.
/// </para>
/// <para>
/// <b>ولا حذف — تعطيلٌ فقط.</b> الرمز محمولٌ على حركات مضت، وحذفُه يجعل كل حركة
/// عليه بلا موضع يُقرأ. وهو الحدّ نفسه الذي يمنع حذف الصنف.
/// </para>
/// </summary>
public sealed class StoragePlaceService : IApplicationService
{
    /// <summary>رمز اللغة الإنجليزية في جدول الترجمات — <c>en</c> بصيغة BCP-47 المختصرة.</summary>
    private const string EnglishLocale = "en";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public StoragePlaceService(IEntitlementEnforcer enforcer, InventoryRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
    }

    /// <summary>
    /// يسجّل موضعاً في مستواه تحت أبيه.
    /// <para>
    /// <b>والأب يُتحقَّق منه ويُتحقَّق أنه عامل</b>: تسجيلُ رفٍّ تحت موقعٍ مُعطَّل
    /// إحياءٌ للموقع من الباب الخلفي — يصير فيه ما يُسكَّن وهو مُعلَنٌ خارج الخدمة.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="level">المستوى.</param>
    /// <param name="parentId">معرّف الأب — <c>null</c> للمستودع.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<StoragePlaceView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        string level,
        Guid? parentId,
        StoragePlaceDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.StoragePlace.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StoragePlaceView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(draft.Name.Arabic))
        {
            return Result<StoragePlaceView>.Failure(InventoryErrors.NameMissing());
        }

        string parentLevel = PlacementLevel.ParentOf(level);
        string parentCode = string.Empty;

        if (parentLevel.Length > 0)
        {
            Result<StoragePlaceRow> found = await ParentAsync(tenant, parentLevel, parentId, cancellationToken)
                .ConfigureAwait(false);

            if (found.IsFailure)
            {
                return Result<StoragePlaceView>.Failure(found.Errors);
            }

            if (!found.Value.IsActive)
            {
                return Result<StoragePlaceView>.Failure(
                    InventoryErrors.ParentPlaceInactive(parentLevel, found.Value.Code));
            }

            parentCode = found.Value.Code;
        }

        if (await _database.Places
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.Level == level && row.Code == draft.Code,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<StoragePlaceView>.Failure(InventoryErrors.DuplicatePlaceCode(level, draft.Code));
        }

        StoragePlaceRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Level = level,
            Code = draft.Code,
            ParentCode = parentCode,
            NameAr = draft.Name.Arabic,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _database.Places.Add(row);

        // الترجمة صفٌّ لا عمود (‏ADR-0021 · القاعدة 14): اللغة الثالثة تدخل بصفٍّ لا
        // بهجرة مخطّط، و«لا ترجمة» تُقرأ من غياب الصفّ لا من نصٍّ فارغ في عمود.
        _database.PlaceNames.Add(new StoragePlaceTranslationRow
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Level = level,
            Code = draft.Code,
            Locale = EnglishLocale,
            Text = draft.Name.English,
        });

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<StoragePlaceView>.Success(
            new StoragePlaceView(row.Id, row.Level, row.Code, draft.Name, row.ParentCode, row.IsActive));
    }

    /// <summary>يقرأ موضعاً واحداً بمعرّفه. نقطة قراءة: تعمل عند «للقراءة فقط» أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="level">المستوى المتوقَّع — يُتحقَّق منه فلا يُقرأ رفٌّ من باب موقع.</param>
    /// <param name="parentId">معرّف الأب كما في المسار — <c>null</c> للمستودع.</param>
    /// <param name="placeId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<StoragePlaceView>> GetAsync(
        TenantId tenant,
        UserId actor,
        string level,
        Guid? parentId,
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.StoragePlace.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StoragePlaceView>.Failure(gate.Errors);
        }

        Result<StoragePlaceRow> found = await LoadAsync(
            tenant, level, parentId, placeId, tracked: false, cancellationToken).ConfigureAwait(false);

        if (found.IsFailure)
        {
            return Result<StoragePlaceView>.Failure(found.Errors);
        }

        StoragePlaceRow row = found.Value;
        LocalizedName name = await NamedAsync(tenant, row, cancellationToken).ConfigureAwait(false);
        return Result<StoragePlaceView>.Success(
            new StoragePlaceView(row.Id, row.Level, row.Code, name, row.ParentCode, row.IsActive));
    }

    /// <summary>
    /// يقرأ مواضع مستوىً، مرتَّبةً بالرمز <b>ترتيباً حرفياً ثابتاً</b> (القاعدة 10).
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="level">المستوى.</param>
    /// <param name="parentId">
    /// أب القائمة بمعرّفه — <c>null</c> للمستودعات. <b>ومعرّفٌ لا رمز</b>: المسار يحمل
    /// معرّف الأب، وترجمتُه إلى رمزه هنا تتحقّق من وجوده فلا تُرجع قائمةً فارغة عن أبٍ
    /// لا وجود له وكأنها إفادةٌ صادقة بأنه خالٍ.
    /// </param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<StoragePlaceView>>> ListAsync(
        TenantId tenant,
        UserId actor,
        string level,
        Guid? parentId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.StoragePlace.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<StoragePlaceView>>.Failure(gate.Errors);
        }

        string parentLevel = PlacementLevel.ParentOf(level);
        string parentCode = string.Empty;

        if (parentLevel.Length > 0)
        {
            Result<StoragePlaceRow> parent = await ParentAsync(tenant, parentLevel, parentId, cancellationToken)
                .ConfigureAwait(false);

            if (parent.IsFailure)
            {
                return Result<IReadOnlyList<StoragePlaceView>>.Failure(parent.Errors);
            }

            parentCode = parent.Value.Code;
        }

        List<StoragePlaceRow> rows = await _database.Places
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.Level == level && row.ParentCode == parentCode)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StoragePlaceTranslationRow> names = await _database.PlaceNames
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.Level == level && row.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StoragePlaceView> views =
        [
            .. rows
                .OrderBy(static row => row.Code, StringComparer.Ordinal)
                .Select(row => new StoragePlaceView(
                    row.Id, row.Level, row.Code, Named(row, names), row.ParentCode, row.IsActive)),
        ];

        return Result<IReadOnlyList<StoragePlaceView>>.Success(views);
    }

    /// <summary>
    /// يعيد تسمية موضع — <b>الاسم وحده</b>.
    /// <para>
    /// <b>ولا يُغيَّر الرمز أبداً:</b> الرمز محمولٌ على كل حركة ورصيد، وتغييرُه يقطع
    /// كل حركة مضت عن موضعها. والاسم نصُّ عرضٍ لا هوية، فتغييره لا يمسّ رقماً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="level">المستوى.</param>
    /// <param name="parentId">معرّف الأب كما في المسار — <c>null</c> للمستودع.</param>
    /// <param name="placeId">المعرّف.</param>
    /// <param name="name">الاسم الجديد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<StoragePlaceView>> RenameAsync(
        TenantId tenant,
        UserId actor,
        string level,
        Guid? parentId,
        Guid placeId,
        LocalizedName name,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.StoragePlace.Rename", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StoragePlaceView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(name.Arabic))
        {
            return Result<StoragePlaceView>.Failure(InventoryErrors.NameMissing());
        }

        Result<StoragePlaceRow> found = await LoadAsync(
            tenant, level, parentId, placeId, tracked: true, cancellationToken).ConfigureAwait(false);

        if (found.IsFailure)
        {
            return Result<StoragePlaceView>.Failure(found.Errors);
        }

        StoragePlaceRow row = found.Value;
        row.NameAr = name.Arabic;

        StoragePlaceTranslationRow? translation = await _database.PlaceNames
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value
                          && entity.Level == level
                          && entity.Code == row.Code
                          && entity.Locale == EnglishLocale,
                cancellationToken)
            .ConfigureAwait(false);

        if (translation is null)
        {
            _database.PlaceNames.Add(new StoragePlaceTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                Level = level,
                Code = row.Code,
                Locale = EnglishLocale,
                Text = name.English,
            });
        }
        else
        {
            translation.Text = name.English;
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<StoragePlaceView>.Success(
            new StoragePlaceView(row.Id, row.Level, row.Code, name, row.ParentCode, row.IsActive));
    }

    /// <summary>
    /// يعطّل موضعاً — <b>ولا يحذفه</b>.
    /// <para>
    /// <b>والتعطيل يُرفض إن كان في الموضع رصيدٌ غير صفري.</b> الموضع المُعطَّل لا
    /// يُنقَل منه ولا يُصرف، فالبضاعة تبقى فيه بقيمتها في الحساب الضابط <b>بلا بابٍ
    /// تخرج منه</b> — رقمٌ في الميزانية لا يقابله واقعٌ يُبلغ. والعلاج نقلُ ما فيه أو
    /// إخراجُه بمستند، ثم التعطيل: الطريقان يتركان أثراً يُقرأ، والتعطيلُ فوق رصيدٍ لا
    /// يترك شيئاً.
    /// </para>
    /// <para>
    /// <b>ولا تعطيل متسلسل:</b> موضعٌ تحته مواضع عاملة يُرفض تعطيله. والتسلسل يُخفي ما
    /// عُطّل تبعاً عمّن عطّله، فلا يُعرف عند التراجع ما كان مُعطَّلاً أصلاً وما عُطّل
    /// بالضربة نفسها.
    /// </para>
    /// <para>
    /// <b>وإعادة تعطيل موضعٍ مُعطَّل تنجح ولا تفشل</b>: الحالة المطلوبة قائمة، والفشل
    /// عليها يجعل كل مستدعٍ يقرأ قبل أن يكتب لينجو من خطأ لا يصف عطلاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="level">المستوى.</param>
    /// <param name="parentId">معرّف الأب كما في المسار — <c>null</c> للمستودع.</param>
    /// <param name="placeId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<StoragePlaceView>> DeactivateAsync(
        TenantId tenant,
        UserId actor,
        string level,
        Guid? parentId,
        Guid placeId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.StoragePlace.Deactivate", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<StoragePlaceView>.Failure(gate.Errors);
        }

        Result<StoragePlaceRow> found = await LoadAsync(
            tenant, level, parentId, placeId, tracked: true, cancellationToken).ConfigureAwait(false);

        if (found.IsFailure)
        {
            return Result<StoragePlaceView>.Failure(found.Errors);
        }

        StoragePlaceRow row = found.Value;

        if (row.IsActive)
        {
            string childLevel = level switch
            {
                PlacementLevel.Warehouse => PlacementLevel.Location,
                PlacementLevel.Location => PlacementLevel.Bin,
                _ => string.Empty,
            };

            if (childLevel.Length > 0)
            {
                int children = await _database.Places
                    .CountAsync(
                        child => child.TenantId == tenant.Value
                                 && child.Level == childLevel
                                 && child.ParentCode == row.Code
                                 && child.IsActive,
                        cancellationToken)
                    .ConfigureAwait(false);

                if (children > 0)
                {
                    return Result<StoragePlaceView>.Failure(
                        InventoryErrors.PlaceStillHasActiveChildren(level, row.Code, children));
                }
            }

            Result held = await RefuseIfStockRemainsAsync(tenant, row, cancellationToken).ConfigureAwait(false);
            if (held.IsFailure)
            {
                return Result<StoragePlaceView>.Failure(held.Errors);
            }

            row.IsActive = false;
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        LocalizedName name = await NamedAsync(tenant, row, cancellationToken).ConfigureAwait(false);
        return Result<StoragePlaceView>.Success(
            new StoragePlaceView(row.Id, row.Level, row.Code, name, row.ParentCode, row.IsActive));
    }

    /// <summary>
    /// يقرأ الأرصدة <b>بتسكينها</b>: الرصيد ومعه اسم مستودعه واسم موقعه من السجلّ.
    /// <para>
    /// <b>ورمزٌ غير مسجَّل يخرج ويُوسَم، ولا يُحذف من القائمة ولا يُخترَع له اسم.</b>
    /// حذفُه كان سيجعل مجموع الأرصدة المقروءة أقلّ من مجموعها الفعلي — انحرافٌ لا
    /// يُظهره أي فحص توازن — واختراعُ اسمٍ له كان سيجعل السجلّ يبدو أشمل ممّا هو.
    /// </para>
    /// <para>
    /// <b>ومستوى القراءة هو مستوى الرصيد: الموقع.</b> ولا رصيد رفٍّ يُقرأ من هنا لأنه
    /// لا يُمسَك أصلاً (‏ADR تسكين المخزون) — والقائمة التي تُرجع صفراً عن رفٍّ فيه
    /// بضاعة أسوأ من قائمة لا تذكره.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<PlacementBalanceView>>> ListPlacementBalancesAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.PlacementBalances.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PlacementBalanceView>>.Failure(gate.Errors);
        }

        List<ItemBalanceRow> balances = await _database.Balances
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StoragePlaceRow> places = await _database.Places
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<StoragePlaceTranslationRow> names = await _database.PlaceNames
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<PlacementBalanceView> views = [];

        foreach (ItemBalanceRow balance in balances
                     .OrderBy(static row => row.ItemId, StringComparer.Ordinal)
                     .ThenBy(static row => row.WarehouseId, StringComparer.Ordinal)
                     .ThenBy(static row => row.LocationId, StringComparer.Ordinal))
        {
            StoragePlaceRow? warehouse = places.FirstOrDefault(place =>
                string.Equals(place.Level, PlacementLevel.Warehouse, StringComparison.Ordinal)
                && string.Equals(place.Code, balance.WarehouseId, StringComparison.Ordinal));

            // ‏**والموقع يُطابَق بأبيه لا برمزه وحده**: موقعان بالرمز نفسه في مستودعين
            // شيئان مختلفان، ومطابقةٌ بالرمز وحده كانت ستُعلّق اسم أحدهما على الآخر.
            StoragePlaceRow? location = places.FirstOrDefault(place =>
                string.Equals(place.Level, PlacementLevel.Location, StringComparison.Ordinal)
                && string.Equals(place.Code, balance.LocationId, StringComparison.Ordinal)
                && string.Equals(place.ParentCode, balance.WarehouseId, StringComparison.Ordinal));

            views.Add(new PlacementBalanceView(
                balance.ItemId,
                balance.WarehouseId,
                warehouse is null
                    ? new LocalizedName(balance.WarehouseId, balance.WarehouseId)
                    : Named(warehouse, names),
                warehouse is not null,
                balance.LocationId,
                location is null
                    ? new LocalizedName(balance.LocationId, balance.LocationId)
                    : Named(location, names),
                location is not null,
                new InventoryQuantity(balance.Quantity, balance.BaseUnit),
                Money.Of(balance.ValueAmount, _currency),
                balance.UnitCost,
                balance.HasCostBasis));
        }

        return Result<IReadOnlyList<PlacementBalanceView>>.Success(views);
    }

    /// <summary>
    /// يترجم معرّف أبٍ إلى رمزه، ويتحقّق من وجوده.
    /// <para>
    /// <b>ووجودُه يُتحقَّق منه ولو كانت النتيجة قائمةً فارغة</b>: «لا مواقع في هذا
    /// المستودع» و«لا مستودع بهذا المعرّف» جوابان مختلفان تماماً، وردُّ الأول على
    /// الثاني يُرسل قارئه يبحث عن بيانات مفقودة بدل عنوانٍ خاطئ.
    /// </para>
    /// </summary>
    private async ValueTask<Result<StoragePlaceRow>> ParentAsync(
        TenantId tenant, string parentLevel, Guid? parentId, CancellationToken cancellationToken)
    {
        if (parentId is not { } id)
        {
            return Result<StoragePlaceRow>.Failure(InventoryErrors.PlaceNotFound(parentLevel, string.Empty));
        }

        StoragePlaceRow? parent = await _database.Places
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value && row.Level == parentLevel && row.Id == id,
                cancellationToken)
            .ConfigureAwait(false);

        return parent is null
            ? Result<StoragePlaceRow>.Failure(InventoryErrors.PlaceIdNotFound(parentLevel, id))
            : Result<StoragePlaceRow>.Success(parent);
    }

    /// <summary>
    /// يحمّل موضعاً بمعرّفه <b>ويتحقّق أنه يقع تحت الأب المذكور في المسار</b>.
    /// <para>
    /// <b>والتحقّق ليس زينةً في العنوان:</b> بدونه يُقرأ رفٌّ من الموقع «‏A» عبر مسار
    /// الموقع «‏B» ويخرج وكأنه فيه — فيُبنى على العنوان معنىً لا يحمله. والفحص هنا
    /// يجعل المسار إفادةً تُصدَّق.
    /// </para>
    /// </summary>
    private async ValueTask<Result<StoragePlaceRow>> LoadAsync(
        TenantId tenant, string level, Guid? parentId, Guid placeId, bool tracked, CancellationToken cancellationToken)
    {
        string parentLevel = PlacementLevel.ParentOf(level);
        string parentCode = string.Empty;

        if (parentLevel.Length > 0)
        {
            Result<StoragePlaceRow> parent = await ParentAsync(tenant, parentLevel, parentId, cancellationToken)
                .ConfigureAwait(false);

            if (parent.IsFailure)
            {
                return Result<StoragePlaceRow>.Failure(parent.Errors);
            }

            parentCode = parent.Value.Code;
        }

        IQueryable<StoragePlaceRow> query = tracked ? _database.Places : _database.Places.AsNoTracking();

        StoragePlaceRow? row = await query
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.Level == level && entity.Id == placeId,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<StoragePlaceRow>.Failure(InventoryErrors.PlaceIdNotFound(level, placeId));
        }

        return parentLevel.Length > 0 && !string.Equals(row.ParentCode, parentCode, StringComparison.Ordinal)
            ? Result<StoragePlaceRow>.Failure(
                InventoryErrors.PlaceNotUnderParent(row.Code, row.ParentCode, parentCode))
            : Result<StoragePlaceRow>.Success(row);
    }

    /// <summary>
    /// يرفض التعطيل إن بقي في الموضع رصيدٌ غير صفري — <b>ويُسمّي الصنف والكمّية</b>.
    /// <para>
    /// <b>و«غير صفري» لا «موجب»:</b> رصيدٌ سالب في موضعٍ واقعةٌ تقع (بيعٌ قبل إدخال
    /// استلامه)، وتعطيلُ موضعه يُغلق الباب الذي يُصحَّح منه — فيبقى العجز مفتوحاً بلا
    /// طريق إلى إقفال الفترة.
    /// </para>
    /// <para>
    /// <b>والرفّ لا رصيد له يُفحَص</b>: ليس بُعداً في مفتاح الرصيد، فلا صفّ يُقرأ عنه
    /// (‏ADR تسكين المخزون). وفحصٌ يبحث عنه كان سيُرجع «لا رصيد» دائماً ويبدو حارساً.
    /// </para>
    /// </summary>
    private async ValueTask<Result> RefuseIfStockRemainsAsync(
        TenantId tenant, StoragePlaceRow row, CancellationToken cancellationToken)
    {
        IQueryable<ItemBalanceRow> query = _database.Balances
            .AsNoTracking()
            .Where(balance => balance.TenantId == tenant.Value && balance.Quantity != 0m);

        query = row.Level switch
        {
            PlacementLevel.Warehouse => query.Where(balance => balance.WarehouseId == row.Code),
            PlacementLevel.Location => query.Where(
                balance => balance.LocationId == row.Code && balance.WarehouseId == row.ParentCode),
            _ => query.Where(static _ => false),
        };

        ItemBalanceRow? held = await query
            .OrderBy(balance => balance.ItemId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return held is null
            ? Result.Success()
            : Result.Failure(InventoryErrors.PlaceStillHoldsStock(
                row.Level, row.Code, held.ItemId, held.Quantity, held.BaseUnit));
    }

    private async ValueTask<LocalizedName> NamedAsync(
        TenantId tenant, StoragePlaceRow row, CancellationToken cancellationToken)
    {
        List<StoragePlaceTranslationRow> names = await _database.PlaceNames
            .AsNoTracking()
            .Where(name => name.TenantId == tenant.Value
                           && name.Level == row.Level
                           && name.Code == row.Code
                           && name.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Named(row, names);
    }

    /// <summary>
    /// الاسم ثنائي اللغة: العربية من الصفّ، والإنجليزية من صفّ ترجمة.
    /// <para>
    /// <b>وغياب الصفّ يُرجع العربية</b> ولا يُرجع فراغاً: «لا ترجمة» ليست «ترجمةٌ
    /// فارغة»، وصفٌّ فارغ في شاشة أسوأ من اسمٍ بلغة السجلّ.
    /// </para>
    /// </summary>
    private static LocalizedName Named(StoragePlaceRow row, IReadOnlyList<StoragePlaceTranslationRow> names)
    {
        string? translated = names
            .FirstOrDefault(name => string.Equals(name.Level, row.Level, StringComparison.Ordinal)
                                    && string.Equals(name.Code, row.Code, StringComparison.Ordinal))?.Text;

        return new LocalizedName(row.NameAr, string.IsNullOrWhiteSpace(translated) ? row.NameAr : translated);
    }
}
