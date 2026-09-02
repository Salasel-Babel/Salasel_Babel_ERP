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
    private readonly UnitOfMeasureService _units;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="units">
    /// سجلّ وحدات القياس — يُسأل عن <b>صنف كمّية</b> وحدتَي الصنف عند تسجيله.
    /// </param>
    public ItemCatalogueService(IEntitlementEnforcer enforcer, InventoryRuntime runtime, UnitOfMeasureService units)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(units);
        _enforcer = enforcer;
        _database = runtime.Database;
        _units = units;
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

        // ── فحصُ الوحدات مشتركٌ مع التعديل ────────────────────────────────────
        // معاملٌ موجب · ولا وحدةَ تُسمّى باسم الأساس (تعريفٌ يقبل رقمين متناقضين
        // لشيء واحد) · وصنفُ كمّيةٍ واحد حين تكون الوحدتان مسجَّلتين — فـ«كجم ← م»
        // ليس معاملاً بل كثافة. والفحص مشروطٌ بالتسجيل لأن سجلّ الوحدات **يصف ولا
        // يُبطل**، وما لا يُعرَف صنفه لا يُرفض؛ **ويبقى عليه أن التحويل بلا معامل مرفوض**.
        Result unitRules = await ValidateUnitsAsync(
            tenant, draft.Code, draft.BaseUnit, draft.Units, cancellationToken).ConfigureAwait(false);

        if (unitRules.IsFailure)
        {
            return Result<ItemView>.Failure(unitRules.Errors);
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

    /// <summary>
    /// يعدّل صنفاً: اسمه ومجموعته ووحداته — <b>ولا رمزه</b>.
    /// <para>
    /// <b>ووحدة الأساس تتغيّر ما لم تُكتب على الصنف حركة.</b> والشرط ليس تشدّداً: رصيدٌ
    /// يتغيّر أساسه بعد أن كُتبت عليه حركات لا يُجمَع أصلاً — مجموعُ حركاته جمعُ أعدادٍ
    /// بمقاييس مختلفة. أمّا صنفٌ سُجّل قبل قليل بخطأ كتابة ولم يتحرّك بعد، فتصحيحُه
    /// تصحيحُ تعريفٍ لا إعادةُ كتابة واقعة — <b>ومنعُه كان يُلزم المستخدم بتسجيل صنفٍ
    /// ثانٍ برمزٍ ثانٍ ليصحّح حرفاً</b>.
    /// </para>
    /// <para>
    /// <b>ومجموعة الصنف تتغيّر بلا شرط، ولا تمسّ ما مضى:</b> كل حركة تحمل مجموعتها
    /// على صفّها هي (‏<c>StockMovementRow.ItemGroup</c>)، فالقيود المُرحَّلة تبقى على
    /// حسابها الضابط، والحركات التالية تذهب إلى ما تقرّره المصفوفة للمجموعة الجديدة.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="itemId">معرّف الصنف.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<ItemView>> ReviseAsync(
        TenantId tenant,
        UserId actor,
        Guid itemId,
        ItemRevisionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Item.Revise", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ItemView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(draft.BaseUnit))
        {
            return Result<ItemView>.Failure(InventoryErrors.UnitMissing());
        }

        if (string.IsNullOrWhiteSpace(draft.Name.Arabic))
        {
            return Result<ItemView>.Failure(InventoryErrors.NameMissing());
        }

        ItemRow? row = await _database.Items
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == itemId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<ItemView>.Failure(InventoryErrors.ItemNotFound(
                itemId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)));
        }

        Result units = await ValidateUnitsAsync(tenant, row.Code, draft.BaseUnit, draft.Units, cancellationToken)
            .ConfigureAwait(false);

        if (units.IsFailure)
        {
            return Result<ItemView>.Failure(units.Errors);
        }

        // ── وحدة الأساس: تُقفَل بالتاريخ لا بالمبدأ ─────────────────────────
        if (!UnitConversion.SameUnit(row.BaseUnit, draft.BaseUnit))
        {
            long movements = await _database.Movements
                .CountAsync(movement => movement.TenantId == tenant.Value && movement.ItemId == row.Code, cancellationToken)
                .ConfigureAwait(false);

            long balances = await _database.Balances
                .CountAsync(balance => balance.TenantId == tenant.Value && balance.ItemId == row.Code, cancellationToken)
                .ConfigureAwait(false);

            if (movements > 0L || balances > 0L)
            {
                return Result<ItemView>.Failure(
                    InventoryErrors.BaseUnitLockedByHistory(row.Code, row.BaseUnit, movements));
            }

            row.BaseUnit = draft.BaseUnit;
        }

        row.NameAr = draft.Name.Arabic;
        row.ItemGroup = draft.ItemGroup;

        // ── الترجمة صفٌّ لا عمود: يُحدَّث الصفّ أو يُنشأ ────────────────────
        ItemTranslationRow? translation = await _database.ItemNames
            .FirstOrDefaultAsync(
                name => name.TenantId == tenant.Value && name.ItemCode == row.Code && name.Locale == EnglishLocale,
                cancellationToken)
            .ConfigureAwait(false);

        if (translation is null)
        {
            _database.ItemNames.Add(new ItemTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                ItemCode = row.Code,
                Locale = EnglishLocale,
                Text = draft.Name.English,
            });
        }
        else
        {
            translation.Text = draft.Name.English;
        }

        // ── الوحدات الأكبر: القائمة الجديدة تحلّ محلّ السابقة كلّها ─────────
        // **ولا يمسّ ذلك حركةً مضت**: كل حركة تحمل مقدارها المُسلَّم ومقدارها بوحدة
        // الأساس معاً على صفّها، فلا شيء فيها يُعاد حسابه بمعاملٍ تغيّر بعدها.
        List<ItemUnitRow> existing = await _database.ItemUnits
            .Where(unit => unit.TenantId == tenant.Value && unit.ItemCode == row.Code)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        _database.ItemUnits.RemoveRange(existing);

        foreach (ItemUnitDraft unit in draft.Units)
        {
            _database.ItemUnits.Add(new ItemUnitRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                ItemCode = row.Code,
                UnitCode = unit.UnitCode,
                Numerator = unit.Numerator,
                Denominator = unit.Denominator,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<ItemView>.Success(new ItemView(
            row.Id, row.Code, draft.Name, row.ItemGroup, row.BaseUnit, draft.Units));
    }

    /// <summary>
    /// يعطّل صنفاً — <b>ويُقبل التعطيل وله رصيد</b>.
    /// <para>
    /// <b>وهذا يخالف عمداً حكمَ موضع التسكين</b>، الذي يُرفض تعطيله فوق رصيد. والفرق
    /// أن إيقاف صنفٍ عن التداول <b>يعني بالضبط</b>: توقّف عن شرائه وبِع ما بقي. ورفضُه
    /// فوق رصيدٍ يصنع دائرةً مغلقة — لا يُعطَّل حتى ينفد، ولا ينفد إلا ببيعٍ يقتضي أن
    /// يكون عاملاً — فلا يُوقَف صنفٌ أبداً ما دامت منه حبّة في مستودعٍ منسيّ، ويلتفّ
    /// عليه المستخدم بإعدامٍ مخترَع يدخل مصروفَ عجزٍ لواقعةٍ لم تقع.
    /// </para>
    /// <para>
    /// <b>وليس صامتاً:</b> الجواب يحمل الرصيد المتبقّي وعدد المواضع التي فيه، فلا يظنّ
    /// أحدٌ أن البضاعة ذهبت مع الإيقاف. والوارد الجديد يُرفض بعده، والصادر يبقى.
    /// </para>
    /// <para>
    /// <b>وإعادة تعطيل مُعطَّلٍ تنجح</b>: الحالة المطلوبة قائمة.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="itemId">معرّف الصنف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<ItemLifecycleView>> DeactivateAsync(
        TenantId tenant,
        UserId actor,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.Item.Deactivate", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ItemLifecycleView>.Failure(gate.Errors);
        }

        ItemRow? row = await _database.Items
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == itemId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<ItemLifecycleView>.Failure(InventoryErrors.ItemNotFound(
                itemId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (row.IsActive)
        {
            row.IsActive = false;
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        return Result<ItemLifecycleView>.Success(await LifecycleOfAsync(tenant, row, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// يقرأ حالة صنفٍ في دورة حياته ورصيده المتبقّي. نقطة قراءة.
    /// <para>
    /// <b>ومورد فرعي مستقلّ لا حقلٌ على <c>Item</c>:</b> إضافةُ حقلٍ إلى شكل الصنف كانت
    /// ستُغيّر استجابة ثلاث عمليات منشورة يستهلكها عملاء اليوم. فالحالة تُقرأ من هنا،
    /// والشكل القائم لا يُمَسّ.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="itemId">معرّف الصنف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<ItemLifecycleView>> LifecycleAsync(
        TenantId tenant,
        UserId actor,
        Guid itemId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.Item.Lifecycle", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ItemLifecycleView>.Failure(gate.Errors);
        }

        ItemRow? row = await _database.Items
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == itemId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<ItemLifecycleView>.Failure(InventoryErrors.ItemNotFound(
                itemId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)))
            : Result<ItemLifecycleView>.Success(
                await LifecycleOfAsync(tenant, row, cancellationToken).ConfigureAwait(false));
    }

    /// <summary>
    /// حالة الصنف ورصيده المتبقّي — <b>و«غير صفري» لا «موجب»</b>: رصيدٌ سالب واقعةٌ
    /// تقع، وإخفاؤها هنا يجعل الجواب يقول «لا رصيد» على صنفٍ عليه عجزٌ مفتوح.
    /// </summary>
    private async ValueTask<ItemLifecycleView> LifecycleOfAsync(
        TenantId tenant, ItemRow row, CancellationToken cancellationToken)
    {
        int placements = await _database.Balances
            .AsNoTracking()
            .CountAsync(
                balance => balance.TenantId == tenant.Value && balance.ItemId == row.Code && balance.Quantity != 0m,
                cancellationToken)
            .ConfigureAwait(false);

        return new ItemLifecycleView(row.Id, row.Code, row.IsActive, placements > 0, placements);
    }

    /// <summary>
    /// يفحص وحدات الصنف: معاملٌ موجب، ولا وحدةَ تُسمّى باسم الأساس، وصنفُ كمّيةٍ واحد.
    /// <para>
    /// <b>ومشتركةٌ بين التسجيل والتعديل عمداً</b>: قاعدةٌ مكتوبة مرّتين تنحرف إحداهما
    /// عند أول تعديل، ولا حارس يقول — وهو نمط <c>fakh-81</c> بعينه.
    /// </para>
    /// </summary>
    private async ValueTask<Result> ValidateUnitsAsync(
        TenantId tenant,
        string itemCode,
        string baseUnit,
        IReadOnlyList<ItemUnitDraft> units,
        CancellationToken cancellationToken)
    {
        foreach (ItemUnitDraft unit in units)
        {
            if (unit.Numerator <= 0L || unit.Denominator <= 0L)
            {
                return Result.Failure(InventoryErrors.UnitRatioNotPositive(
                    new UnitRatio(unit.Numerator, unit.Denominator).ToString()));
            }

            if (UnitConversion.SameUnit(unit.UnitCode, baseUnit))
            {
                return Result.Failure(InventoryErrors.UnitNotConvertible(itemCode, unit.UnitCode, baseUnit));
            }

            Result classes = await SameQuantityClassAsync(tenant, baseUnit, unit.UnitCode, cancellationToken)
                .ConfigureAwait(false);

            if (classes.IsFailure)
            {
                return classes;
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// يتحقّق أن وحدتين من صنف كمّيةٍ واحد — <b>حين تكون كلتاهما مسجَّلة</b>.
    /// <para>
    /// وحين لا تكون إحداهما مسجَّلة <b>يمرّ</b>: سجلّ الوحدات يصف ولا يُبطل، وصنفٌ
    /// سُجّل قبل وجوده يحمل رموزاً لا صفَّ لها. والتساهل هنا ثمنُ التوافق مع ما مضى،
    /// <b>ولا يُدفع على التحويل نفسه</b>: خلطُ وحدتين بلا معامل يبقى رفضاً في كل حال.
    /// </para>
    /// </summary>
    private async ValueTask<Result> SameQuantityClassAsync(
        TenantId tenant, string baseUnit, string otherUnit, CancellationToken cancellationToken)
    {
        UnitOfMeasureRow? registeredBase = await _units
            .RegisteredAsync(tenant, baseUnit, cancellationToken).ConfigureAwait(false);

        if (registeredBase is null)
        {
            return Result.Success();
        }

        UnitOfMeasureRow? registeredOther = await _units
            .RegisteredAsync(tenant, otherUnit, cancellationToken).ConfigureAwait(false);

        if (registeredOther is null)
        {
            return Result.Success();
        }

        return string.Equals(registeredBase.Class, registeredOther.Class, StringComparison.Ordinal)
            ? Result.Success()
            : Result.Failure(InventoryErrors.UnitClassMismatch(
                otherUnit, registeredOther.Class, baseUnit, registeredBase.Class));
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
