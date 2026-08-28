using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
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
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    public PurchaseOrderService(IEntitlementEnforcer enforcer, PurchasingRuntime runtime)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
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

        (decimal net, decimal tax) = Totals(draft.Lines);

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
        AddLines(tenant, LineOwner.Request, row.Id, draft.Lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(View(row.Id, row.Number, row.State, net, tax));
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
            View(row.Id, row.Number, row.State, row.EstimatedTotal, 0m));
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

        if (draft.Lines.Count == 0)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.NoLines);
        }

        // سعر الوحدة يحمل عملته، وكان يُقرأ رقماً وتُكتب عملة المنشأة فوقه.
        Result uniform = EnsureCompanyCurrency(draft.Lines);
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

        (decimal net, decimal tax) = Totals(draft.Lines);

        PurchaseOrderRow row = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = draft.SupplierId,
            RequestId = requestId,
            OrderedOn = draft.OrderedOn,
            State = PurchasingDocumentState.Approved,
            CurrencyCode = _currency.Value,
            WarehouseId = draft.WarehouseId,
            CostCenterId = draft.CostCenterId,
            NetTotal = net,
            TaxTotal = tax,
            GrossTotal = net + tax,
        };

        _database.Orders.Add(row);
        AddLines(tenant, LineOwner.Order, row.Id, draft.Lines);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(View(row.Id, row.Number, row.State, net, tax));
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

        PurchaseOrderRow? order = await _database.Orders
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == orderId, cancellationToken)
            .ConfigureAwait(false);

        return order is null
            ? Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(OrderDocument, orderId))
            : Result<PurchasingDocumentView>.Success(
                View(order.Id, order.Number, order.State, order.NetTotal, order.TaxTotal));
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

        List<PurchaseLineRow> lines = await _database.Lines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.OwnerType == LineOwner.Order && row.OwnerId == orderId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<PurchaseLineView>>.Success(
            [.. lines.Select(line => new PurchaseLineView(
                line.Id, line.LineNo, line.ItemId, line.Quantity, Money.Of(line.UnitPrice, _currency)))]);
    }

    internal static (decimal Net, decimal Tax) Totals(IReadOnlyList<PurchaseLineDraft> lines)
    {
        decimal net = 0m;
        decimal tax = 0m;

        foreach (PurchaseLineDraft line in lines)
        {
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, 0m, line.TaxRate, line.TaxClassification);
            net += lineNet;
            tax += lineTax;
        }

        return (net, tax);
    }

    internal void AddLines(TenantId tenant, string ownerType, Guid ownerId, IReadOnlyList<PurchaseLineDraft> lines)
    {
        for (int index = 0; index < lines.Count; index++)
        {
            PurchaseLineDraft line = lines[index];
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, 0m, line.TaxRate, line.TaxClassification);

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
    private Result EnsureCompanyCurrency(IReadOnlyList<PurchaseLineDraft> lines)
    {
        foreach (PurchaseLineDraft line in lines)
        {
            if (!line.UnitPrice.Currency.Equals(_currency))
            {
                return Result.Failure(
                    PurchasingErrors.CurrencyMismatch(_currency, line.UnitPrice.Currency, "lines.unitPrice"));
            }
        }

        return Result.Success();
    }

    private PurchasingDocumentView View(Guid id, string number, string state, decimal net, decimal tax) => new(
        id,
        number,
        state,
        new DocumentTotals(
            Money.Of(net, _currency),
            Money.Of(tax, _currency),
            Money.Of(net + tax, _currency)),
        null);
}
