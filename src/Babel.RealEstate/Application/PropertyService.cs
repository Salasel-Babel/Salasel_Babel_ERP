using Babel.Contracts.RealEstate;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.RealEstate.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.RealEstate.Application;

/// <summary>
/// البيانات الأساسية للعقار والوحدة.
/// <para>
/// <b>وإنشاء العقار يُسجّل بُعده في الدفتر في العملية نفسها.</b> عقارٌ بلا صفٍّ في سجلّ
/// الأبعاد ليس عقاراً «ناقص بيانات عرض»: قاعدة الحجب GR-RE-001 تُقيَّم على واقعة
/// <c>property.ownership_model</c>، ومصدرها ذلك الصفّ. وحين يغيب، تعود القاعدة
/// <b>غير قابلة للتقييم</b> — <b>والقاعدة التي لا تُقيَّم لا تُتجاوَز</b> — فيُرفض القيد
/// كاملاً. أي أن العقار غير المسجَّل يُعطّل دورته كلها بصوت عالٍ، لا يمرّ صامتاً.
/// </para>
/// <para>
/// <b>والوحدة مورد فرعي للعقار لا كيان مستقل:</b> ‏<c>dimensions.csv</c> يشترط العقار مع
/// الوحدة، والقيد في قاعدة بيانات الدفتر يفرضه على كل سطر — فالاشتراط بنيةٌ في العنوان
/// لا تحقّقٌ في الجسم.
/// </para>
/// </summary>
public sealed class PropertyService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly RealEstateDbContext _database;
    private readonly IPropertyDimensionRegistrar _registrar;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="registrar">منفذ تسجيل بُعد العقار في الدفتر — يصله الجذر التركيبي.</param>
    public PropertyService(IEntitlementEnforcer enforcer, RealEstateRuntime runtime, IPropertyDimensionRegistrar registrar)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(registrar);
        _enforcer = enforcer;
        _database = runtime.Database;
        _registrar = registrar;
    }

    /// <summary>ينشئ عقاراً <b>ويسجّل بُعده في الدفتر</b>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="draft">مسوّدة العقار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<PropertyView>> CreatePropertyAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        PropertyDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.Property.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PropertyView>.Failure(gate.Errors);
        }

        if (!PropertyOwnershipModels.IsKnown(draft.OwnershipModel))
        {
            return Result<PropertyView>.Failure(RealEstateErrors.UnknownOwnershipModel(draft.OwnershipModel));
        }

        bool managed = string.Equals(draft.OwnershipModel, PropertyOwnershipModels.ManagedForOthers, StringComparison.Ordinal);

        if (managed && draft.OwnerId is null)
        {
            return Result<PropertyView>.Failure(RealEstateErrors.ManagedPropertyNeedsAnOwner);
        }

        if (!managed && draft.OwnerId is not null)
        {
            return Result<PropertyView>.Failure(RealEstateErrors.OwnedPropertyTakesNoOwner);
        }

        if (draft.OwnerId is { } ownerId
            && !await _database.Parties
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId
                           && row.Id == ownerId && row.PartyRole == PartyRoles.Owner,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PropertyView>.Failure(RealEstateErrors.PartyNotFound(PartyRoles.Owner, ownerId));
        }

        if (await _database.Properties
                .AnyAsync(row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PropertyView>.Failure(RealEstateErrors.DuplicateCode(draft.Code));
        }

        // ── التسجيل في الدفتر **أوّلاً** ────────────────────────────────────────
        // فرفضُه — نموذج ملكية لا تعرفه القاعدة، أو عقارٌ مسجَّل بنموذج آخر — يترك
        // الوحدة بلا صفّ عقارٍ يتيم. والعكس (كتابةٌ محليّة ثم تسجيل يفشل) يُنتج عقاراً
        // يظهر في كل شاشة ولا يستطيع أن يُرحّل قيداً واحداً، وذلك أسوأ من ألّا يوجد.
        Result registered = await _registrar
            .RegisterAsync(tenant, companyId, draft.Code, draft.OwnershipModel, draft.Name, cancellationToken)
            .ConfigureAwait(false);

        if (registered.IsFailure)
        {
            return Result<PropertyView>.Failure(registered.Errors);
        }

        PropertyRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            CompanyId = companyId,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            OwnershipModel = draft.OwnershipModel,
            IsActive = true,
        };

        _database.Properties.Add(row);

        foreach (KeyValuePair<string, string> translation in draft.Name.Translations)
        {
            _database.PropertyNames.Add(new PropertyTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                CompanyId = companyId,
                PropertyCode = draft.Code,
                LanguageTag = translation.Key,
                Text = translation.Value,
            });
        }

        // ── حصّة المالك: صفٌّ واحد بحصّة كاملة، ومفتاحٌ يحتمل الحصص من اليوم ──────
        if (draft.OwnerId is { } owner)
        {
            _database.OwnerShares.Add(new PropertyOwnerShareRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                CompanyId = companyId,
                PropertyId = row.Id,
                OwnerId = owner,
                ShareNumerator = 1,
                ShareDenominator = 1,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PropertyView>.Success(
            new PropertyView(row.Id, row.Code, draft.Name, row.OwnershipModel, draft.OwnerId, 1, 1));
    }

    /// <summary>يقرأ عقاراً. نقطة قراءة: تعمل عند <see cref="EntitlementState.ReadOnly"/> أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="propertyId">العقار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public async ValueTask<Result<PropertyView>> ReadPropertyAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid propertyId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, "RealEstate.Property.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PropertyView>.Failure(gate.Errors);
        }

        PropertyRow? row = await _database.Properties
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId && entity.Id == propertyId,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<PropertyView>.Failure(RealEstateErrors.PropertyNotFound(propertyId));
        }

        TranslatedName name = await NameOfAsync(tenant, companyId, row.Code, row.NameAr, cancellationToken).ConfigureAwait(false);

        List<PropertyOwnerShareRow> shares = await _database.OwnerShares
            .AsNoTracking()
            .Where(share => share.TenantId == tenant.Value && share.CompanyId == companyId && share.PropertyId == row.Id)
            .OrderBy(share => share.OwnerId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        PropertyOwnerShareRow? single = shares.Count == 1 ? shares[0] : null;

        return Result<PropertyView>.Success(new PropertyView(
            row.Id,
            row.Code,
            name,
            row.OwnershipModel,
            single?.OwnerId,
            single?.ShareNumerator ?? 0,
            single?.ShareDenominator ?? 0));
    }

    /// <summary>ينشئ وحدةً داخل عقار.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="propertyId">العقار المالك.</param>
    /// <param name="draft">مسوّدة الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public async ValueTask<Result<UnitView>> CreateUnitAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid propertyId,
        UnitDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, "RealEstate.Unit.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<UnitView>.Failure(gate.Errors);
        }

        PropertyRow? property = await _database.Properties
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId && entity.Id == propertyId,
                cancellationToken)
            .ConfigureAwait(false);

        if (property is null)
        {
            return Result<UnitView>.Failure(RealEstateErrors.PropertyNotFound(propertyId));
        }

        if (await _database.Units
                .AnyAsync(row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<UnitView>.Failure(RealEstateErrors.DuplicateCode(draft.Code));
        }

        UnitRow unit = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            CompanyId = companyId,
            PropertyId = propertyId,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            Usage = draft.Usage,
            VatTreatment = draft.VatTreatment,
            IsActive = true,
        };

        _database.Units.Add(unit);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<UnitView>.Success(
            new UnitView(unit.Id, unit.PropertyId, unit.Code, draft.Name, unit.Usage, unit.VatTreatment));
    }

    /// <summary>يقرأ وحدةً بتصنيفها — وهو ما يقود شرط <c>unit_is_vat_taxable</c>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="unitId">الوحدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public async ValueTask<Result<UnitView>> ReadUnitAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid unitId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, "RealEstate.Unit.Read", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<UnitView>.Failure(gate.Errors);
        }

        UnitRow? unit = await _database.Units
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId && entity.Id == unitId,
                cancellationToken)
            .ConfigureAwait(false);

        return unit is null
            ? Result<UnitView>.Failure(RealEstateErrors.UnitNotFound(unitId))
            : Result<UnitView>.Success(new UnitView(
                unit.Id, unit.PropertyId, unit.Code, new TranslatedName(unit.NameAr), unit.Usage, unit.VatTreatment));
    }

    /// <summary>يجمع الاسم العربي بترجماته صفوفاً — لا عمود إنجليزي في هذا المخطّط.</summary>
    private async Task<TranslatedName> NameOfAsync(
        TenantId tenant,
        Guid companyId,
        string code,
        string arabic,
        CancellationToken cancellationToken)
    {
        List<PropertyTranslationRow> rows = await _database.PropertyNames
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.CompanyId == companyId && row.PropertyCode == code)
            .OrderBy(row => row.LanguageTag)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, string> translations = new(StringComparer.Ordinal);
        foreach (PropertyTranslationRow row in rows)
        {
            translations[row.LanguageTag] = row.Text;
        }

        return new TranslatedName(arabic, translations);
    }
}
