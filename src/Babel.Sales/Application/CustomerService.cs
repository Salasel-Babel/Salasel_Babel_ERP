using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Sales.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Sales.Application;

/// <summary>
/// بيانات العملاء الأساسية: الاسم ثنائي اللغة، وحد الائتمان، وشروط السداد.
/// <para>شكل كل خدمة تطبيق: علامة الخدمة، وسمة استحقاق على كل نقطة دخول، ونداء المنفِّذ أولاً.</para>
/// </summary>
public sealed class CustomerService : IApplicationService
{
    private readonly IEntitlementEnforcer _enforcer;
    private readonly SalesDbContext _database;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public CustomerService(IEntitlementEnforcer enforcer, SalesRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
    }

    /// <summary>يسجّل عميلاً جديداً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">مسوّدة العميل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Write)]
    public async ValueTask<Result<CustomerView>> CreateAsync(
        TenantId tenant,
        UserId actor,
        CustomerDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Write, "Sales.Customer.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CustomerView>.Failure(gate.Errors);
        }

        if (await _database.Customers
                .AnyAsync(row => row.TenantId == tenant.Value && row.Code == draft.Code, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<CustomerView>.Failure(SalesErrors.DuplicateNumber(draft.Code));
        }

        CustomerRow row = new()
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

        _database.Customers.Add(row);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<CustomerView>.Success(
            new CustomerView(row.Id, row.Code, draft.Name, draft.CreditLimit, row.PaymentTermsDays));
    }

    /// <summary>يقرأ عميلاً. نقطة قراءة: تعمل عند <see cref="EntitlementState.ReadOnly"/> أيضاً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="customerId">معرّف العميل.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Sales, EntitlementAccess.Read)]
    public async ValueTask<Result<CustomerView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid customerId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Sales, EntitlementAccess.Read, "Sales.Customer.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<CustomerView>.Failure(gate.Errors);
        }

        CustomerRow? row = await _database.Customers
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == customerId, cancellationToken)
            .ConfigureAwait(false);

        return row is null
            ? Result<CustomerView>.Failure(SalesErrors.CustomerNotFound(customerId))
            : Result<CustomerView>.Success(new CustomerView(
                row.Id,
                row.Code,
                new LocalizedName(row.NameAr, row.NameEn),
                Money.Of(row.CreditLimit, CurrencyCode.Sar),
                row.PaymentTermsDays));
    }
}
