using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.RealEstate.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.RealEstate.Application;

/// <summary>
/// البيانات الأساسية للأطراف العقارية: المستأجر والمالك والوسيط.
/// <para>
/// <b>ولا يُسمّى المستأجر العقاري <c>tenant</c> على أي سطح</b>: الكلمة منشورة في العقد
/// اليوم بمعنى <b>مستأجر النظام</b> — <c>/api/v1/tenants</c> وأربعة مسارات اشتراك تحته —
/// ونشرُها بمعنيين يجعل العقد يكذب على قارئه قبل أن يتصادم التوجيه. فالدور هنا
/// <c>lessee</c> واسمُ المورد <c>lessees</c>.
/// </para>
/// <para>
/// <b>والإقامة الضريبية حقلٌ إلزامي بلا افتراضي</b>: عليها يتوقّف سطر الاستقطاع في
/// توريد المالك، وهو بندٌ موقوف على مصدر نظامي (م-7). وقيمةٌ افتراضية «مقيم» تجعل
/// الاستقطاع يسقط بصمت عن كل مالك لم يُملأ حقله.
/// </para>
/// </summary>
public sealed class PartyService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly RealEstateDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public PartyService(IEntitlementEnforcer enforcer, RealEstateRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>يسجّل مستأجراً.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public ValueTask<Result<PartyView>> CreateLesseeAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        PartyDraft draft,
        CancellationToken cancellationToken = default)
        => CreateAsync(tenant, actor, companyId, PartyRoles.Lessee, draft, "RealEstate.Lessee.Create", cancellationToken);

    /// <summary>يقرأ مستأجراً.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="lesseeId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public ValueTask<Result<PartyView>> ReadLesseeAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid lesseeId,
        CancellationToken cancellationToken = default)
        => ReadAsync(tenant, actor, companyId, PartyRoles.Lessee, lesseeId, "RealEstate.Lessee.Read", cancellationToken);

    /// <summary>يسجّل مالك عقار.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Write)]
    public ValueTask<Result<PartyView>> CreateOwnerAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        PartyDraft draft,
        CancellationToken cancellationToken = default)
        => CreateAsync(tenant, actor, companyId, PartyRoles.Owner, draft, "RealEstate.Owner.Create", cancellationToken);

    /// <summary>يقرأ مالك عقار.</summary>
    /// <param name="tenant">المستأجر (نطاق النظام).</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="companyId">المنشأة.</param>
    /// <param name="ownerId">المعرّف.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.RealEstate, EntitlementAccess.Read)]
    public ValueTask<Result<PartyView>> ReadOwnerAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        Guid ownerId,
        CancellationToken cancellationToken = default)
        => ReadAsync(tenant, actor, companyId, PartyRoles.Owner, ownerId, "RealEstate.Owner.Read", cancellationToken);

    private async ValueTask<Result<PartyView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        string role,
        PartyDraft draft,
        string operation,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Write, operation, cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PartyView>.Failure(gate.Errors);
        }

        if (await _database.Parties
                .AnyAsync(
                    row => row.TenantId == tenant.Value && row.CompanyId == companyId
                           && row.PartyRole == role && row.Code == draft.Code,
                    cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PartyView>.Failure(RealEstateErrors.DuplicateCode(draft.Code));
        }

        PartyRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            CompanyId = companyId,
            PartyRole = role,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            VatNumber = draft.VatNumber,
            TaxResidency = draft.TaxResidency,
        };

        _database.Parties.Add(row);

        foreach (KeyValuePair<string, string> translation in draft.Name.Translations)
        {
            _database.PartyNames.Add(new PartyTranslationRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                CompanyId = companyId,
                PartyRole = role,
                PartyCode = draft.Code,
                LanguageTag = translation.Key,
                Text = translation.Value,
            });
        }

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PartyView>.Success(
            new PartyView(row.Id, role, row.Code, draft.Name, row.VatNumber, row.TaxResidency));
    }

    private async ValueTask<Result<PartyView>> ReadAsync(
        TenantId tenant,
        UserId actor,
        Guid companyId,
        string role,
        Guid partyId,
        string operation,
        CancellationToken cancellationToken)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.RealEstate, EntitlementAccess.Read, operation, cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PartyView>.Failure(gate.Errors);
        }

        PartyRow? row = await _database.Parties
            .FirstOrDefaultAsync(
                entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId
                          && entity.PartyRole == role && entity.Id == partyId,
                cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<PartyView>.Failure(RealEstateErrors.PartyNotFound(role, partyId));
        }

        List<PartyTranslationRow> names = await _database.PartyNames
            .AsNoTracking()
            .Where(entity => entity.TenantId == tenant.Value && entity.CompanyId == companyId
                             && entity.PartyRole == role && entity.PartyCode == row.Code)
            .OrderBy(entity => entity.LanguageTag)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Dictionary<string, string> translations = new(StringComparer.Ordinal);
        foreach (PartyTranslationRow name in names)
        {
            translations[name.LanguageTag] = name.Text;
        }

        return Result<PartyView>.Success(new PartyView(
            row.Id, role, row.Code, new TranslatedName(row.NameAr, translations), row.VatNumber, row.TaxResidency));
    }
}
