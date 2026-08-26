using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>
/// استلام البضاعة و«بضاعة مستلمة لم تُفوتر».
/// <para>
/// <b>هذا هو القيد الأول في دورة الشراء</b>: البضاعة دخلت المستودع والالتزام نشأ،
/// وفاتورة المورد لم تصل بعد. حذفُ هذه الخطوة هو ما يجعل مخزوناً موجوداً بلا التزام
/// مقابل في الميزانية حتى تصل الفاتورة — أياً كان تأخّرها.
/// </para>
/// <para>
/// وكل سطر استلام يُرحَّل <b>قيداً مستقلاً</b>: قالب المصفوفة يحمل مرجع صنف واحداً
/// ومستودعاً واحداً على مستوى الطلب، فقيدٌ واحد لاستلام متعدد الأصناف كان سيحمل
/// مرجع صنف واحد لأصناف عدة — وهو ما يفسد الدفتر المساعد للأصناف بصمت.
/// </para>
/// </summary>
public sealed class GoodsReceiptService : IApplicationService
{
    /// <summary>نوع مستند سطر الاستلام في هوية الإحكام.</summary>
    internal const string ReceiptLineDocument = "GoodsReceiptLine";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل.</param>
    public GoodsReceiptService(IEntitlementEnforcer enforcer, PurchasingRuntime runtime, IPostingService posting)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
        _gateway = new SubledgerPostingGateway(_database, posting, runtime.CostCenters);
    }

    /// <summary>
    /// يسجّل استلاماً على أمر شراء. <b>الضلع الأول من المطابقة الثلاثية</b>: كمية
    /// مستلمة تتجاوز المطلوب تُرفض هنا، لا عند الفاتورة.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> RecordAsync(
        TenantId tenant,
        UserId actor,
        GoodsReceiptDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Receipt.Record", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        if (draft.Lines.Count == 0)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.NoLines);
        }

        PurchaseOrderRow? order = await _database.Orders
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.OrderId, cancellationToken)
            .ConfigureAwait(false);

        if (order is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound("PurchaseOrder", draft.OrderId));
        }

        if (await _database.Receipts
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        Guid receiptId = Guid.CreateVersion7();
        decimal cost = 0m;
        int lineNo = 0;

        // ── مرّتان لا مرّة: يُتحقَّق من كل السطور أولاً، ثم تُكتب ───────────────
        // متعقّب EF يحتفظ بأي تعديل أُجري قبل الرفض، فأول SaveChanges لاحق يُثبّته.
        // والترتيب كلي صريح بمعرّف سطر الأمر: كتابة متعددة الصفوف بترتيب غير
        // مُعرَّف هي بالضبط شكل الجمود المقيس (فخ-10).
        List<(PurchaseLineRow OrderLine, decimal Quantity, decimal Cost)> pending = [];

        foreach (GoodsReceiptLineDraft line in draft.Lines.OrderBy(static l => l.OrderLineId))
        {
            PurchaseLineRow? orderLine = await _database.Lines
                .FirstOrDefaultAsync(
                    row => row.TenantId == tenant.Value && row.OwnerType == LineOwner.Order && row.Id == line.OrderLineId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (orderLine is null)
            {
                return Result<PurchasingDocumentView>.Failure(PurchasingErrors.LineNotFound(line.OrderLineId));
            }

            decimal outstanding = orderLine.Quantity - orderLine.ReceivedQuantity;
            if (line.Quantity > outstanding)
            {
                return Result<PurchasingDocumentView>.Failure(
                    PurchasingErrors.ReceiptExceedsOrder(orderLine.ItemId, line.Quantity, outstanding));
            }

            decimal lineCost = LineMath.Round(line.Quantity * orderLine.UnitPrice);
            cost += lineCost;
            pending.Add((orderLine, line.Quantity, lineCost));
        }

        foreach ((PurchaseLineRow orderLine, decimal quantity, decimal lineCost) in pending)
        {
            lineNo++;
            _database.Lines.Add(new PurchaseLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                OwnerType = LineOwner.Receipt,
                OwnerId = receiptId,
                LineNo = lineNo,
                OrderLineId = orderLine.Id,
                ItemId = orderLine.ItemId,
                ItemGroup = orderLine.ItemGroup,
                DescriptionAr = orderLine.DescriptionAr,
                DescriptionEn = orderLine.DescriptionEn,
                Quantity = quantity,
                UnitPrice = orderLine.UnitPrice,
                TaxClassification = orderLine.TaxClassification,
                TaxRate = orderLine.TaxRate,
                TaxRecoverable = orderLine.TaxRecoverable,
                LineNet = lineCost,
                LineTax = 0m,
            });

            orderLine.ReceivedQuantity += quantity;
        }

        GoodsReceiptRow receipt = new()
        {
            Id = receiptId,
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = order.SupplierId,
            OrderId = order.Id,
            ReceivedOn = draft.ReceivedOn,
            State = PurchasingDocumentState.Draft,
            CurrencyCode = _currency.Value,
            WarehouseId = order.WarehouseId,
            ReceiptCost = cost,
        };

        _database.Receipts.Add(receipt);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(
            new PurchasingDocumentView(
                receipt.Id,
                receipt.Number,
                receipt.State,
                new DocumentTotals(Money.Of(cost, _currency), Money.Of(0m, _currency), Money.Of(cost, _currency)),
                null));
    }

    /// <summary>يرحّل الاستلام سطراً سطراً عبر <c>purchasing.goods_receipt.posted</c>.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="receiptId">الاستلام.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> PostAsync(
        TenantId tenant,
        UserId actor,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Receipt.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        GoodsReceiptRow? receipt = await _database.Receipts
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == receiptId, cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound("GoodsReceipt", receiptId));
        }

        if (receipt.State == PurchasingDocumentState.Posted)
        {
            return Result<PurchasingDocumentView>.Success(ViewOf(receipt));
        }

        SupplierRow supplier = await _database.Suppliers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == receipt.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        List<PurchaseLineRow> lines = await _database.Lines
            .Where(row => row.OwnerType == LineOwner.Receipt && row.OwnerId == receipt.Id)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        Guid? lastEntry = null;

        foreach (PurchaseLineRow line in lines)
        {
            PostingIntent intent = new()
            {
                Tenant = tenant,
                DocumentType = ReceiptLineDocument,
                DocumentId = line.Id,
                Trigger = PostingTrigger.OnReceipt,
                Event = new PostingEventCode("purchasing.goods_receipt.posted"),
                DocumentDate = receipt.ReceivedOn,
                Narration = new LocalizedName(
                    "استلام بضاعة " + receipt.Number, "Goods receipt " + receipt.Number),
                Amounts = [new PostingAmount("receipt_cost", Money.Of(line.LineNet, _currency))],
                Facts =
                [
                    new PostingFact("subledger.supplier", supplier.Code),
                    new PostingFact("subledger.item", line.ItemId),
                    new PostingFact("line.item_group", line.ItemGroup),
                ],
                Dimensions = [new PostingDimension("warehouse", receipt.WarehouseId)],
                PartyId = supplier.Code,
                ControlEffect = line.LineNet,
                Currency = _currency,
                Actor = actor,
                Generation = receipt.PostingGeneration,
            };

            Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
            if (posted.IsFailure)
            {
                return Result<PurchasingDocumentView>.Failure(posted.Errors);
            }

            lastEntry = posted.Value.JournalEntryId;
        }

        receipt.State = PurchasingDocumentState.Posted;
        receipt.PostedEntryId = lastEntry;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(ViewOf(receipt));
    }

    /// <summary>يقرأ سطور استلام — معرّفاتها مدخل الضلع الثالث من المطابقة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="receiptId">الاستلام.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<IReadOnlyList<PurchaseLineView>>> GetLinesAsync(
        TenantId tenant,
        UserId actor,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Receipt.Lines", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<IReadOnlyList<PurchaseLineView>>.Failure(gate.Errors);
        }

        List<PurchaseLineRow> lines = await _database.Lines
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value && row.OwnerType == LineOwner.Receipt && row.OwnerId == receiptId)
            .OrderBy(row => row.LineNo)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<IReadOnlyList<PurchaseLineView>>.Success(
            [.. lines.Select(line => new PurchaseLineView(
                line.Id, line.LineNo, line.ItemId, line.Quantity, Money.Of(line.UnitPrice, _currency)))]);
    }

    private PurchasingDocumentView ViewOf(GoodsReceiptRow receipt) => new(
        receipt.Id,
        receipt.Number,
        receipt.State,
        new DocumentTotals(
            Money.Of(receipt.ReceiptCost, _currency),
            Money.Of(0m, _currency),
            Money.Of(receipt.ReceiptCost, _currency)),
        receipt.PostedEntryId);
}
