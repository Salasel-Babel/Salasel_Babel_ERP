using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
using Babel.Core.CompanySetup;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>
/// طلبات الشراء وأوامرها. لا ترحيل هنا: أمر الشراء التزام تعاقدي لا حدث محاسبي،
/// والقيد الأول في دورة الشراء هو <b>الاستلام</b> لا الأمر.
/// </summary>
public sealed class PurchaseOrderService : IApplicationService
{
    /// <summary>نوع مستند أمر الشراء — يُستعمل في الرفض بالاسم، لا في هوية ترحيل: لا ترحيل له.</summary>
    internal const string OrderDocument = "PurchaseOrder";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;
    private readonly ICompanyMoneyResolver _company;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public PurchaseOrderService(IEntitlementEnforcer enforcer, PurchasingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _company = runtime.Company;
    }

    /// <summary>يُنشئ طلب شراء داخلياً.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> CreateRequestAsync(
        TenantId tenant,
        UserId actor,
        PurchaseRequestDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Request.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        Result<CompanyMoney> money = await _company.ResolveAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (money.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(money.Errors);
        }

        if (draft.Lines.Count == 0)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.NoLines);
        }

        if (await _database.Requests
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        (decimal net, decimal tax) = Totals(draft.Lines, money.Value);

        PurchaseRequestRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            RequestedOn = draft.RequestedOn,
            CostCenterId = draft.CostCenterId,
            State = PurchasingDocumentState.Draft,
            EstimatedTotal = net + tax,
        };

        _database.Requests.Add(row);
        AddLines(tenant, LineOwner.Request, row.Id, draft.Lines, money.Value);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(View(row.Id, row.Number, row.State, net, tax, money.Value));
    }

    /// <summary>يعتمد طلب شراء.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="requestId">الطلب.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> ApproveRequestAsync(
        TenantId tenant,
        UserId actor,
        Guid requestId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Request.Approve", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        Result<CompanyMoney> money = await _company.ResolveAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (money.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(money.Errors);
        }

        PurchaseRequestRow? row = await _database.Requests
            .FirstOrDefaultAsync(entity => entity.TenantId == tenant.Value && entity.Id == requestId, cancellationToken)
            .ConfigureAwait(false);

        if (row is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound("PurchaseRequest", requestId));
        }

        if (row.State != PurchasingDocumentState.Draft)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.NotInState(row.Number, row.State, PurchasingDocumentState.Draft));
        }

        row.State = PurchasingDocumentState.Approved;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(
            View(row.Id, row.Number, row.State, row.EstimatedTotal, 0m, money.Value));
    }

    /// <summary>يُنشئ أمر شراء، اختيارياً من طلب معتمد.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="requestId">الطلب المصدر إن وُجد.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> CreateOrderAsync(
        TenantId tenant,
        UserId actor,
        PurchaseOrderDraft draft,
        Guid? requestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Order.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        Result<CompanyMoney> money = await _company.ResolveAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (money.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(money.Errors);
        }

        if (draft.Lines.Count == 0)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.NoLines);
        }

        // سعر الوحدة يحمل عملته، وكان يُقرأ رقماً وتُكتب عملة المنشأة فوقه.
        Result uniform = EnsureCompanyCurrency(draft.Lines, money.Value);
        if (uniform.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(uniform.Errors);
        }

        if (!await _database.Suppliers
                .AnyAsync(row => row.TenantId == tenant.Value && row.Id == draft.SupplierId, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.SupplierNotFound(draft.SupplierId));
        }

        if (requestId is { } id)
        {
            PurchaseRequestRow? request = await _database.Requests
                .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == id, cancellationToken)
                .ConfigureAwait(false);

            if (request is null)
            {
                return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound("PurchaseRequest", id));
            }

            if (request.State != PurchasingDocumentState.Approved)
            {
                return Result<PurchasingDocumentView>.Failure(
                    PurchasingErrors.NotInState(request.Number, request.State, PurchasingDocumentState.Approved));
            }
        }

        if (await _database.Orders
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        (decimal net, decimal tax) = Totals(draft.Lines, money.Value);

        PurchaseOrderRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = draft.SupplierId,
            RequestId = requestId,
            OrderedOn = draft.OrderedOn,
            State = PurchasingDocumentState.Approved,
            CurrencyCode = money.Value.Currency.Value,
            WarehouseId = draft.WarehouseId,
            CostCenterId = draft.CostCenterId,
            NetTotal = net,
            TaxTotal = tax,
            GrossTotal = net + tax,
        };

        _database.Orders.Add(row);
        AddLines(tenant, LineOwner.Order, row.Id, draft.Lines, money.Value);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(View(row.Id, row.Number, row.State, net, tax, money.Value));
    }

    /// <summary>
    /// يقرأ أمر شراء بحالته ومجاميعه.
    /// <para>
    /// <b>ولا معرّف قيد له ولا سيكون:</b> أمر الشراء <b>التزام تعاقدي لا حدث محاسبي</b>،
    /// والقيد الأول في دورة الشراء هو الاستلام. فالقراءة هنا تُرجع مستنداً بلا قيد،
    /// ولا مورد ترحيل عليه أصلاً.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="orderId">الأمر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<PurchasingDocumentView>> GetOrderAsync(
        TenantId tenant,
        UserId actor,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Order.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        Result<CompanyMoney> money = await _company.ResolveAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (money.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(money.Errors);
        }

        PurchaseOrderRow? order = await _database.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        return order is null
            ? Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(OrderDocument, orderId))
            : Result<PurchasingDocumentView>.Success(
                View(order.Id, order.Number, order.State, order.NetTotal, order.TaxTotal, money.Value));
    }

    /// <summary>يقرأ سطور أمر شراء — معرّفات السطور هي مدخل المطابقة الثلاثية.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="orderId">الأمر.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<PurchaseLineView>>> GetOrderLinesAsync(
        TenantId tenant,
        UserId actor,
        Guid orderId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Order.Lines", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PurchaseLineView>>.Failure(gate.Errors);
        }

        Result<CompanyMoney> money = await _company.ResolveAsync(tenant, cancellationToken).ConfigureAwait(false);
        if (money.IsFailure)
        {
            return Result<IReadOnlyList<PurchaseLineView>>.Failure(money.Errors);
        }

        List<PurchaseLineRow> lines = await _database.Lines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.OwnerType == LineOwner.Order && row.OwnerId == orderId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<PurchaseLineView>>.Success(
            [.. lines.Select(line => new PurchaseLineView(
                line.Id, line.LineNo, line.ItemId, line.Quantity, line.Unit, Money.Of(line.UnitPrice, money.Value.Currency)))]);
    }

    internal static (decimal Net, decimal Tax) Totals(IReadOnlyList<PurchaseLineDraft> lines, CompanyMoney money)
    {
        decimal net = 0m;
        decimal tax = 0m;

        foreach (PurchaseLineDraft line in lines)
        {
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, 0m, line.TaxRate, line.TaxClassification, money);
            net += lineNet;
            tax += lineTax;
        }

        return (net, tax);
    }

    internal void AddLines(TenantId tenant, string ownerType, Guid ownerId, IReadOnlyList<PurchaseLineDraft> lines, CompanyMoney money)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            PurchaseLineDraft line = lines[index];
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, 0m, line.TaxRate, line.TaxClassification, money);

            _database.Lines.Add(new PurchaseLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                OwnerType = ownerType,
                OwnerId = ownerId,
                LineNo = index + 1,
                ItemId = line.ItemId,
                ItemGroup = line.ItemGroup,
                DescriptionAr = line.Description.Arabic,
                DescriptionEn = line.Description.English,
                Quantity = line.Quantity,
                Unit = line.Unit,
                UnitPrice = line.UnitPrice.Amount,
                TaxClassification = line.TaxClassification,
                TaxRate = line.TaxRate,
                TaxRecoverable = line.TaxRecoverable,
                LineNet = lineNet,
                LineTax = lineTax,
            });
        }
    }

    /// <summary>كل سعر وحدة بعملة المنشأة، والخلط مرفوض برسالة تُسمّي العملتين.</summary>
    /// <param name="lines">السطور.</param>
    /// <param name="money">عملة المنشأة ووحدتها الصغرى (ADR-0089).</param>
    private static Result EnsureCompanyCurrency(IReadOnlyList<PurchaseLineDraft> lines, CompanyMoney money)
    {
        foreach (PurchaseLineDraft line in lines)
        {
            if (!line.UnitPrice.Currency.Equals(money.Currency))
            {
                return Result.Failure(
                    PurchasingErrors.CurrencyMismatch(money.Currency, line.UnitPrice.Currency, "lines.unitPrice"));
            }
        }

        return Result.Success();
    }

    private static PurchasingDocumentView View(Guid id, string number, string state, decimal net, decimal tax, CompanyMoney money) => new(
        id,
        number,
        state,
        new DocumentTotals(
            Money.Of(net, money.Currency),
            Money.Of(tax, money.Currency),
            Money.Of(net + tax, money.Currency)),
        null);
}
