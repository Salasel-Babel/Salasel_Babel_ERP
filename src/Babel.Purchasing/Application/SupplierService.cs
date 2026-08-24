using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>بيانات الموردين الأساسية: الاسم ثنائي اللغة، والسقف، وشروط السداد.</summary>
public sealed class SupplierService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public SupplierService(IEntitlementEnforcer enforcer, PurchasingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>يسجّل مورداً جديداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<SupplierView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        SupplierDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Supplier.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SupplierView>.Failure(gate.Errors);
        }

        if (await _database.Suppliers
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<SupplierView>.Failure(PurchasingErrors.DuplicateNumber(draft.Code));
        }

        SupplierRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Code = draft.Code,
            NameAr = draft.Name.Arabic,
            NameEn = draft.Name.English,
            CreditLimit = draft.CreditLimit.Amount,
            PaymentTermsDays = draft.PaymentTermsDays,
            IsActive = true,
        };

        _database.Suppliers.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<SupplierView>.Success(
            new SupplierView(row.Id, row.Code, draft.Name, draft.CreditLimit, row.PaymentTermsDays));
    }

    /// <summary>يقرأ مورداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="supplierId">المورد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<SupplierView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid supplierId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Supplier.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<SupplierView>.Failure(gate.Errors);
        }

        SupplierRow? row = await _database.Suppliers
            .AsNoTracking()
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == supplierId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<SupplierView>.Failure(PurchasingErrors.SupplierNotFound(supplierId))
            : Result<SupplierView>.Success(new SupplierView(
                row.Id,
                row.Code,
                new LocalizedName(row.NameAr, row.NameEn),
                Money.Of(row.CreditLimit, CurrencyCode.Sar),
                row.PaymentTermsDays));
    }
}
