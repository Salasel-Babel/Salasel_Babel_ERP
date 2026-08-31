using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Inventory.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory.Application;

/// <summary>
/// كتالوج الأصناف: الرمز، والاسم بلغتين، والمجموعة، و<b>وحدة الأساس ومعاملات التحويل</b>.
/// <para>
/// <b>ولا رصيد هنا ولا تكلفة:</b> الصنف تعريف، والرصيد واقعةٌ في دفتر المخزون المساعد.
/// وخلطُهما يجعل «عدّل الصنف» فعلاً يمسّ دفتراً مُرحَّلاً.
/// </para>
/// <para>
/// <b>ولا حذف ولا تعديل على هذا السطح</b> — للسبب الذي يمنعهما على العميل والمورد:
/// رمزُ الصنف هوية تحملها قيود سنةٍ مضت، وحذفُه يكسر كل تقرير مُرحَّل، وتغييرُ وحدة
/// أساسه بعد أن كُتبت عليه حركات يجعل مجموع حركاته جمعَ أعدادٍ بمقاييس مختلفة.
/// وذلك <b>نقصُ سطحٍ مُعلَن</b>، مكتوبٌ في القرار لا متروك ليُكتشف.
/// </para>
/// </summary>
public sealed class ItemCatalogueService : IApplicationService
{
    /// <summary>رمز اللغة الإنجليزية في جدول الترجمات — <c>en</c> بصيغة BCP-47 المختصرة.</summary>
    private const string EnglishLocale = "en";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public ItemCatalogueService(IEntitlementEnforcer enforcer, InventoryRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>يسجّل صنفاً جديداً بوحداته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<ItemView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        ItemDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Item.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ItemView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(draft.BaseUnit))
        {
            return Result<ItemView>.Failure(InventoryErrors.UnitMissing());
        }

        foreach (ItemUnitDraft unit in draft.Units)
        {
            if (unit.Numerator <= 0L || unit.Denominator <= 0L)
            {
                return Result<ItemView>.Failure(InventoryErrors.UnitRatioNotPositive(
                    new UnitRatio(unit.Numerator, unit.Denominator).ToString()));
            }

            // وحدةٌ أكبر تُسمّى باسم وحدة الأساس معاملُها إلى نفسها — وهو تعريفٌ
            // يقبل رقمين متناقضين لشيء واحد. والرفض هنا أرخص من رصيدٍ يُقرأ بمقياسين.
            if (UnitConversion.SameUnit(unit.UnitCode, draft.BaseUnit))
            {
                return Result<ItemView>.Failure(
                    InventoryErrors.UnitNotConvertible(draft.Code, unit.UnitCode, draft.BaseUnit));
            }
        }

        if (await _database.Items
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<ItemView>.Failure(InventoryErrors.DuplicateItemCode(draft.Code));
        }

        ItemRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            ItemGroup = draft.ItemGroup,
            BaseUnit = draft.BaseUnit,
            CreatedAt = DateTime.UtcNow,
        };

        _database.Items.Add(row);

        // ── الترجمة صفٌّ لا عمود ──────────────────────────────────────────────
        // الاسم العربي على الكيان لأنه **السجلّ**، والإنجليزية صفٌّ في جدول الترجمات.
        // فاللغة الثالثة تدخل بصفٍّ لا بهجرة مخطّط (‏ADR-0021 · القاعدة 14).
        _database.ItemNames.Add(new ItemTranslationRow
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            ItemCode = draft.Code,
            Locale = EnglishLocale,
            Text = draft.Name.English,
        });

        foreach (ItemUnitDraft unit in draft.Units)
        {
            _database.ItemUnits.Add(new ItemUnitRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                ItemCode = draft.Code,
                UnitCode = unit.UnitCode,
                Numerator = unit.Numerator,
                Denominator = unit.Denominator,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<ItemView>.Success(new ItemView(
            row.Id, row.Code, draft.Name, row.ItemGroup, row.BaseUnit, draft.Units));
    }

    /// <summary>يقرأ صنفاً واحداً بوحداته. نقطة قراءة: تعمل عند «للقراءة فقط» أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="itemId">معرّف الصنف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<ItemView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.Item.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ItemView>.Failure(gate.Errors);
        }

        ItemRow? row = await _database.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == itemId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<ItemView>.Failure(InventoryErrors.ItemNotFound(
                itemId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)));
        }

        return Result<ItemView>.Success(await ViewOfAsync(tenant, row, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// يقرأ أصناف المنشأة كلّها، <b>مرتَّبةً بالرمز ترتيباً حرفياً ثابتاً</b> — لا بترتيب
    /// الإدخال، ولا بترتيبٍ ثقافي يختلف بين <c>tr-TR</c> و<c>en-US</c> (القاعدة 10).
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<ItemView>>> ListAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.Item.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<ItemView>>.Failure(gate.Errors);
        }

        List<ItemRow> rows = await _database.Items
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ItemUnitRow> units = await _database.ItemUnits
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ItemTranslationRow> names = await _database.ItemNames
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ItemView> views =
        [
            .. rows
                .OrderBy(static row => row.Code, StringComparer.Ordinal)
                .Select(row => new ItemView(
                    row.Id,
                    row.Code,
                    Named(row, names),
                    row.ItemGroup,
                    row.BaseUnit,
                    [
                        .. units
                            .Where(unit => string.Equals(unit.ItemCode, row.Code, StringComparison.Ordinal))
                            .OrderBy(static unit => unit.UnitCode, StringComparer.Ordinal)
                            .Select(static unit => new ItemUnitDraft(unit.UnitCode, unit.Numerator, unit.Denominator)),
                    ])),
        ];

        return Result<IReadOnlyList<ItemView>>.Success(views);
    }

    private async Task<ItemView> ViewOfAsync(TenantId tenant, ItemRow row, CancellationToken cancellationToken)
    {
        List<ItemUnitDraft> units = await _database.ItemUnits
            .AsNoTracking()
            .Where(unit => unit.TenantId == tenant.Value && unit.ItemCode == row.Code)
            .OrderBy(unit => unit.UnitCode)
            .Select(unit => new ItemUnitDraft(unit.UnitCode, unit.Numerator, unit.Denominator))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<ItemTranslationRow> names = await _database.ItemNames
            .AsNoTracking()
            .Where(name => name.TenantId == tenant.Value && name.ItemCode == row.Code && name.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return new ItemView(row.Id, row.Code, Named(row, names), row.ItemGroup, row.BaseUnit, units);
    }

    /// <summary>
    /// الاسم ثنائي اللغة كما يخرج: العربية من الكيان، والإنجليزية من صفّ ترجمة.
    /// <para>
    /// <b>وغياب الصفّ يُرجع العربية</b> ولا يُرجع فراغاً: «لا ترجمة» ليست «ترجمةٌ
    /// فارغة»، وصفٌّ فارغ في شاشة أسوأ من اسمٍ بلغة السجلّ.
    /// </para>
    /// </summary>
    private static LocalizedName Named(ItemRow row, IReadOnlyList<ItemTranslationRow> names)
    {
        string? translated = names
            .FirstOrDefault(name => string.Equals(name.ItemCode, row.Code, StringComparison.Ordinal))?.Text;

        return new LocalizedName(row.NameAr, string.IsNullOrWhiteSpace(translated) ? row.NameAr : translated);
    }
}
