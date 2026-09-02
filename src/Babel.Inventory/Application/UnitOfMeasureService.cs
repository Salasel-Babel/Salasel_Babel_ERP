using Babel.Contracts.Inventory;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Inventory.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Inventory.Application;

/// <summary>
/// <b>سجلّ وحدات القياس</b>: الوحدة ككيان أوّل، ومعاملات التحويل بينها.
/// <para>
/// <b>والعمود الذي يبرّر وجود هذا السجلّ كلّه هو صنف الكمّية.</b> معامل التحويل بين
/// وحدتين من صنفٍ واحد <b>واقعةٌ فيزيائية</b>: الكيلوغرام ألف غرام دائماً وفي كل مكان.
/// وبين صنفين مختلفين <b>ليس معاملاً بل كثافة</b>: «كم كيلوغراماً في اللتر؟» جوابه يختلف
/// بين الماء والزيت والرصاص، ويختلف للمادّة الواحدة بالحرارة. فبلا هذا العمود لا يملك
/// النظام ما يفرّق به بين الاثنين، ويصير «كجم ← م» معاملاً يكتبه أحدهم بحسن نيّة ولا
/// يعترض عليه شيء.
/// </para>
/// <para>
/// <b>وسجلٌّ يصف ولا يُبطل</b>، كسجلّ التسكين حرفاً بحرف: الحركات القائمة تحمل رموز
/// وحدات كُتبت قبل وجوده، ولا مفتاح خارجي منها إليه، ورمزٌ غير مسجَّل يبقى عاملاً في
/// مسار الحركة. <b>لكن المعامل لا يُسجَّل بين رمزين لا يُعرَف صنف كمّيتهما</b>: التحويل
/// بلا صنفٍ معلوم تقديرٌ لا حساب.
/// </para>
/// <para>
/// <b>ولا اشتقاق بسلسلة.</b> «غرام ← كجم» و«كجم ← طنّ» لا يُنتجان «غرام ← طنّ» تلقائياً:
/// السلسلة تُنتج تحويلاً لم يقرّه أحد، وكسرُها الوسيط يُقرَّب قبل أن يُضرب في الثاني —
/// وهو بالضبط التقريب الصامت الذي وُجد هذا السجلّ ليمنعه.
/// </para>
/// </summary>
public sealed class UnitOfMeasureService : IApplicationService
{
    /// <summary>رمز اللغة الإنجليزية في جدول الترجمات.</summary>
    private const string EnglishLocale = "en";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly InventoryDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public UnitOfMeasureService(IEntitlementEnforcer enforcer, InventoryRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>يسجّل وحدة قياس بصنف كمّيتها.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<UnitOfMeasureView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        UnitOfMeasureDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.UnitOfMeasure.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<UnitOfMeasureView>.Failure(gate.Errors);
        }

        if (string.IsNullOrWhiteSpace(draft.Code))
        {
            return Result<UnitOfMeasureView>.Failure(InventoryErrors.UnitMissing());
        }

        if (string.IsNullOrWhiteSpace(draft.Name.Arabic))
        {
            return Result<UnitOfMeasureView>.Failure(InventoryErrors.NameMissing());
        }

        if (!QuantityClass.All.Contains(draft.QuantityClass, StringComparer.Ordinal))
        {
            return Result<UnitOfMeasureView>.Failure(
                InventoryErrors.UnknownQuantityClass(draft.QuantityClass, QuantityClass.All));
        }

        if (await _database.Units
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<UnitOfMeasureView>.Failure(InventoryErrors.DuplicateUnitCode(draft.Code));
        }

        UnitOfMeasureRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            Class = draft.QuantityClass,
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
        };

        _database.Units.Add(row);

        _database.UnitNames.Add(new UnitOfMeasureTranslationRow
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            UnitCode = draft.Code,
            Locale = EnglishLocale,
            Text = draft.Name.English,
        });

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<UnitOfMeasureView>.Success(
            new UnitOfMeasureView(row.Id, row.Code, draft.Name, row.Class, row.IsActive));
    }

    /// <summary>يقرأ وحدة قياس واحدة. نقطة قراءة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="unitId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<UnitOfMeasureView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.UnitOfMeasure.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<UnitOfMeasureView>.Failure(gate.Errors);
        }

        UnitOfMeasureRow? row = await _database.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == unitId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<UnitOfMeasureView>.Failure(InventoryErrors.UnitNotRegistered(
                unitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)));
        }

        List<UnitOfMeasureTranslationRow> names = await _database.UnitNames
            .AsNoTracking()
            .Where(name => name.TenantId == tenant.Value && name.UnitCode == row.Code && name.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<UnitOfMeasureView>.Success(
            new UnitOfMeasureView(row.Id, row.Code, Named(row, names), row.Class, row.IsActive));
    }

    /// <summary>يقرأ وحدات المنشأة مرتَّبةً بالرمز <b>ترتيباً حرفياً ثابتاً</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<UnitOfMeasureView>>> ListAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.UnitOfMeasure.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<UnitOfMeasureView>>.Failure(gate.Errors);
        }

        List<UnitOfMeasureRow> rows = await _database.Units
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<UnitOfMeasureTranslationRow> names = await _database.UnitNames
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<UnitOfMeasureView> views =
        [
            .. rows
                .OrderBy(static row => row.Code, StringComparer.Ordinal)
                .Select(row => new UnitOfMeasureView(row.Id, row.Code, Named(row, names), row.Class, row.IsActive)),
        ];

        return Result<IReadOnlyList<UnitOfMeasureView>>.Success(views);
    }

    /// <summary>
    /// يعطّل وحدة قياس — <b>ولا يحذفها</b>: الرمز محمولٌ على حركات مضت.
    /// <para>
    /// <b>ولا فحص رصيدٍ هنا</b> — بخلاف موضع التسكين: الوحدة ليست بُعداً في مفتاح
    /// الرصيد بل <b>مقياسُ ما فيه</b>. وتعطيلُها لا يحبس بضاعةً: الرصيد المُمسَك بها
    /// يبقى مقروءاً ومصروفاً، لأنه مُمسَكٌ بوحدة أساسٍ ثُبِّتت بأول حركة ولا تتغيّر.
    /// والتعطيل يمنع <b>تسجيل معاملٍ جديد</b> عليها لا أكثر.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="unitId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<UnitOfMeasureView>> DeactivateAsync(
        TenantId tenant,
        UserId actor,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.UnitOfMeasure.Deactivate", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<UnitOfMeasureView>.Failure(gate.Errors);
        }

        UnitOfMeasureRow? row = await _database.Units
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == unitId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<UnitOfMeasureView>.Failure(InventoryErrors.UnitNotRegistered(
                unitId.ToString("D", System.Globalization.CultureInfo.InvariantCulture)));
        }

        if (row.IsActive)
        {
            row.IsActive = false;
            await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }

        List<UnitOfMeasureTranslationRow> names = await _database.UnitNames
            .AsNoTracking()
            .Where(name => name.TenantId == tenant.Value && name.UnitCode == row.Code && name.Locale == EnglishLocale)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<UnitOfMeasureView>.Success(
            new UnitOfMeasureView(row.Id, row.Code, Named(row, names), row.Class, row.IsActive));
    }

    /// <summary>
    /// يسجّل معامل تحويل بين وحدتين — <b>ويرفض ما بين صنفين مختلفين باسمه</b>.
    /// <para>
    /// <b>والمعامل يُسجَّل في اتجاهه المُسلَّم وحده، ولا يُقلَب تلقائياً.</b> «الكرتون
    /// اثنتا عشرة حبّة» هو <c>12/1</c>، ومقلوبه «الحبّة جزءٌ من اثني عشر كرتوناً» — وهو
    /// صحيحٌ رياضياً وبلا معنىً عملي في مستند. والقلبُ التلقائي كان سيُنتج معاملات لا
    /// يقرؤها أحد ولا يُراجعها، ويجعل قائمة المعاملات ضعف طولها الحقيقي.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Write)]
    public async ValueTask<Result<UnitConversionView>> CreateConversionAsync(
        TenantId tenant,
        UserId actor,
        UnitConversionDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Write, "Inventory.UnitConversion.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<UnitConversionView>.Failure(gate.Errors);
        }

        if (draft.Numerator <= 0L || draft.Denominator <= 0L)
        {
            return Result<UnitConversionView>.Failure(InventoryErrors.UnitRatioNotPositive(
                new UnitRatio(draft.Numerator, draft.Denominator).ToString()));
        }

        if (UnitConversion.SameUnit(draft.FromUnit, draft.ToUnit))
        {
            return Result<UnitConversionView>.Failure(
                InventoryErrors.DuplicateUnitConversion(draft.FromUnit, draft.ToUnit));
        }

        Result<(UnitOfMeasureRow From, UnitOfMeasureRow To)> pair =
            await ConvertiblePairAsync(tenant, draft.FromUnit, draft.ToUnit, cancellationToken).ConfigureAwait(false);

        if (pair.IsFailure)
        {
            return Result<UnitConversionView>.Failure(pair.Errors);
        }

        if (!pair.Value.From.IsActive)
        {
            return Result<UnitConversionView>.Failure(InventoryErrors.UnitInactive(draft.FromUnit));
        }

        if (!pair.Value.To.IsActive)
        {
            return Result<UnitConversionView>.Failure(InventoryErrors.UnitInactive(draft.ToUnit));
        }

        if (await _database.UnitConversions
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.FromUnit == draft.FromUnit && row.ToUnit == draft.ToUnit,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<UnitConversionView>.Failure(
                InventoryErrors.DuplicateUnitConversion(draft.FromUnit, draft.ToUnit));
        }

        UnitConversionRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            FromUnit = draft.FromUnit,
            ToUnit = draft.ToUnit,
            Numerator = draft.Numerator,
            Denominator = draft.Denominator,
            CreatedAt = DateTime.UtcNow,
        };

        _database.UnitConversions.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<UnitConversionView>.Success(new UnitConversionView(
            row.Id, row.FromUnit, row.ToUnit, pair.Value.From.Class, row.Numerator, row.Denominator));
    }

    /// <summary>يقرأ معاملات التحويل مرتَّبةً بالوحدتين ترتيباً حرفياً ثابتاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<UnitConversionView>>> ListConversionsAsync(
        TenantId tenant,
        UserId actor,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.UnitConversion.List", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<UnitConversionView>>.Failure(gate.Errors);
        }

        List<UnitConversionRow> rows = await _database.UnitConversions
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<UnitOfMeasureRow> units = await _database.Units
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        List<UnitConversionView> views =
        [
            .. rows
                .OrderBy(static row => row.FromUnit, StringComparer.Ordinal)
                .ThenBy(static row => row.ToUnit, StringComparer.Ordinal)
                .Select(row => new UnitConversionView(
                    row.Id,
                    row.FromUnit,
                    row.ToUnit,
                    units.FirstOrDefault(unit => string.Equals(unit.Code, row.FromUnit, StringComparison.Ordinal))?.Class
                        ?? string.Empty,
                    row.Numerator,
                    row.Denominator)),
        ];

        return Result<IReadOnlyList<UnitConversionView>>.Success(views);
    }

    /// <summary>
    /// <b>مسبار التحويل</b>: يحوّل كمّيةً ولا يكتب شيئاً.
    /// <para>
    /// وُجد كي يُجرَّب التحويل <b>قبل</b> أن يُبنى عليه مستند، وكي يكون
    /// «‏٧ حبّات ← كرتون مرفوض» <b>جواباً يُقرأ على السلك</b> لا سلوكاً يُستنتَج من
    /// فشل مستند. ويُجيب بالناتج <b>الدقيق</b> أو بالرفض المُسمّى، ولا يُقرّب في
    /// الحالتين.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="trial">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Inventory, EntitlementAccess.Read)]
    public async ValueTask<Result<UnitConversionResult>> ConvertAsync(
        TenantId tenant,
        UserId actor,
        UnitConversionTrial trial,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(trial);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Inventory, EntitlementAccess.Read, "Inventory.UnitConversion.Convert", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<UnitConversionResult>.Failure(gate.Errors);
        }

        Result valid = UnitConversion.Validate(trial.Quantity);
        if (valid.IsFailure)
        {
            return Result<UnitConversionResult>.Failure(valid.Errors);
        }

        if (string.IsNullOrWhiteSpace(trial.ToUnit))
        {
            return Result<UnitConversionResult>.Failure(InventoryErrors.UnitMissing());
        }

        Result<(UnitOfMeasureRow From, UnitOfMeasureRow To)> pair =
            await ConvertiblePairAsync(tenant, trial.Quantity.Unit, trial.ToUnit, cancellationToken).ConfigureAwait(false);

        if (pair.IsFailure)
        {
            return Result<UnitConversionResult>.Failure(pair.Errors);
        }

        // الوحدة إلى نفسها: واحدٌ على واحد، ولا يُقرأ لها صفّ — فصفٌّ كهذا ممنوع أصلاً.
        if (UnitConversion.SameUnit(trial.Quantity.Unit, trial.ToUnit))
        {
            return Result<UnitConversionResult>.Success(new UnitConversionResult(
                trial.Quantity, trial.Quantity, 1L, 1L, pair.Value.From.Class));
        }

        UnitConversionRow? factor = await _database.UnitConversions
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value
                       && row.FromUnit == trial.Quantity.Unit
                       && row.ToUnit == trial.ToUnit,
                cancellationToken)
            .ConfigureAwait(false);

        if (factor is null)
        {
            return Result<UnitConversionResult>.Failure(
                InventoryErrors.NoConversionBetween(trial.Quantity.Unit, trial.ToUnit));
        }

        Result<decimal> converted = UnitConversion.ToBase(
            trial.Quantity.Magnitude, new UnitRatio(factor.Numerator, factor.Denominator));

        return converted.IsFailure
            ? Result<UnitConversionResult>.Failure(converted.Errors)
            : Result<UnitConversionResult>.Success(new UnitConversionResult(
                trial.Quantity,
                new InventoryQuantity(converted.Value, trial.ToUnit),
                factor.Numerator,
                factor.Denominator,
                pair.Value.From.Class));
    }

    /// <summary>
    /// وحدة مسجَّلة بصنفها — أو <c>null</c> إن لم تكن مسجَّلة.
    /// <para>
    /// <c>internal</c> لأن كتالوج الأصناف يسألها عند تسجيل صنف: وحدتا الصنف المسجَّلتان
    /// يجب أن تكونا من صنف كمّيةٍ واحد.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="code">رمز الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    internal Task<UnitOfMeasureRow?> RegisteredAsync(
        TenantId tenant, string code, CancellationToken cancellationToken)
        => _database.Units
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Code == code, cancellationToken);

    /// <summary>
    /// يقرأ الوحدتين ويتحقّق أنهما مسجَّلتان و<b>من صنف كمّيةٍ واحد</b>.
    /// </summary>
    private async ValueTask<Result<(UnitOfMeasureRow From, UnitOfMeasureRow To)>> ConvertiblePairAsync(
        TenantId tenant, string fromUnit, string toUnit, CancellationToken cancellationToken)
    {
        UnitOfMeasureRow? from = await RegisteredAsync(tenant, fromUnit, cancellationToken).ConfigureAwait(false);

        if (from is null)
        {
            return Result<(UnitOfMeasureRow, UnitOfMeasureRow)>.Failure(
                InventoryErrors.UnitNotRegistered(fromUnit));
        }

        UnitOfMeasureRow? to = await RegisteredAsync(tenant, toUnit, cancellationToken).ConfigureAwait(false);

        if (to is null)
        {
            return Result<(UnitOfMeasureRow, UnitOfMeasureRow)>.Failure(
                InventoryErrors.UnitNotRegistered(toUnit));
        }

        // ‏**هنا يقع الرفض الذي وُجد صنف الكمّية من أجله.**
        return string.Equals(from.Class, to.Class, StringComparison.Ordinal)
            ? Result<(UnitOfMeasureRow, UnitOfMeasureRow)>.Success((from, to))
            : Result<(UnitOfMeasureRow, UnitOfMeasureRow)>.Failure(
                InventoryErrors.UnitClassMismatch(fromUnit, from.Class, toUnit, to.Class));
    }

    /// <summary>
    /// الاسم ثنائي اللغة: العربية من الصفّ، والإنجليزية من صفّ ترجمة.
    /// <b>وغياب الصفّ يُرجع العربية</b> ولا يُرجع فراغاً.
    /// </summary>
    private static LocalizedName Named(UnitOfMeasureRow row, IReadOnlyList<UnitOfMeasureTranslationRow> names)
    {
        string? translated = names
            .FirstOrDefault(name => string.Equals(name.UnitCode, row.Code, StringComparison.Ordinal))?.Text;

        return new LocalizedName(row.NameAr, string.IsNullOrWhiteSpace(translated) ? row.NameAr : translated);
    }
}
