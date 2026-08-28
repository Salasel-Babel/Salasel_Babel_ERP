using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
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
/// <para>
/// <b>والسطر نفسه يبلغ دفتر المخزون المساعد قبل أن يبلغ الدفتر.</b> كان الاستلام
/// يُدين حساب مراقبة المخزون ثم <b>يُنتظر من الجذر التركيبي</b> أن يسجّل الحركة
/// بنداءٍ منفصل — وهو نداء لم يكن مكتوباً إلا في تجهيزة الاختبار. فكان الاستلام في
/// أي نشرٍ حقيقي يُحرّك الحساب الضابط ولا يُحرّك الدفتر المساعد: بضاعةٌ في الميزانية
/// بلا رصيد صنف يقابلها، وبيعُها بعدها يُرفض بـ<c>inventory.no_cost_basis</c>.
/// </para>
/// <para>
/// <b>وترتيب النداءين ليس تفصيلاً:</b> الحركة تُسجَّل أولاً، فإن رُفضت لم يُكتب في
/// الدفتر شيء ولم ينحرف طرفٌ عن طرف. وإن نجحت ثم سقط الترحيل، بقي انحراف
/// <c>missing_in_control</c> <b>تُظهره المطابقة باسم المستند</b>، وإعادةُ المحاولة
/// تتقارب: هوية الحركة هي هوية الترحيل، والوصول الثاني بها لا يصرف كميةً ثانية
/// (<c>WasAlreadyRecorded</c>).
/// </para>
/// </summary>
public sealed class GoodsReceiptService : IApplicationService
{
    /// <summary>نوع مستند سطر الاستلام في هوية الإحكام.</summary>
    internal const string ReceiptLineDocument = "GoodsReceiptLine";

    /// <summary>
    /// نوع مستند الاستلام نفسه — يُستعمل في الرفض بالاسم وحده.
    /// <b>ولا هوية ترحيل له</b>: الترحيل بحبيبيّة السطر، ومعرّف السطر هو معرّف المستند فيها.
    /// </summary>
    internal const string ReceiptDocument = "GoodsReceipt";

    /// <summary>رمز حدث الاستلام — ثالث حقول الهوية التي يتشاركها الدفتران.</summary>
    internal const string ReceiptPostedEvent = "purchasing.goods_receipt.posted";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly PurchasingAdmission _admission;
    private readonly IInventoryValuation _valuation;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل.</param>
    /// <param name="profiles">مخزن ملفّات القدرات — بوابة القبول (‏ADR-0023).</param>
    /// <param name="valuation">
    /// حدّ تقييم المخزون — الوارد يُسجَّل فيه بتكلفته الفعلية فيصير أساس تكلفة الصنف.
    /// <para>
    /// وهو منفذ في <c>Babel.Contracts</c> لا مرجعٌ إلى وحدة المخزون: الوحدات الأفقية
    /// لا يعتمد بعضها على بعض (القاعدة 3)، والجذر التركيبي وحده يعرف الطرفين.
    /// </para>
    /// <para>
    /// <b>وهو إلزامي لا اختياري.</b> منفذٌ يُقبَل غيابه يعني استلاماً يُدين الحساب
    /// الضابط ولا يبلغ الدفتر المساعد — وهو الحال الذي أُغلق هنا بالضبط.
    /// </para>
    /// </param>
    public GoodsReceiptService(
        IEntitlementEnforcer enforcer,
        PurchasingRuntime runtime,
        IPostingService posting,
        ICapabilityProfileStore profiles,
        IInventoryValuation valuation)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(valuation);
        _valuation = valuation;
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
        _gateway = new SubledgerPostingGateway(_database, posting, runtime.CostCenters);
        _admission = new PurchasingAdmission(profiles);
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

        // الاستلام ضلعٌ من المطابقة الثلاثية، وحدثه تفتحه تلك القدرة وحدها.
        Result<AdmittedDocument> admitted = await _admission
            .AdmitBillAsync(
                tenant,
                [PurchasingAdmission.SupplierField, PurchasingAdmission.LinesField, PurchasingAdmission.ReceiptField],
                cancellationToken)
            .ConfigureAwait(false);

        if (admitted.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(admitted.Errors);
        }

        return await PostAdmittedAsync(tenant, actor, admitted.Value, receiptId, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>الكاتب الوحيد لقيد الاستلام — ويطلب <see cref="AdmittedDocument"/> في توقيعه.</b>
    /// </summary>
    private async ValueTask<Result<PurchasingDocumentView>> PostAdmittedAsync(
        TenantId tenant,
        UserId actor,
        AdmittedDocument admitted,
        Guid receiptId,
        CancellationToken cancellationToken)
    {
        Result covers = PurchasingAdmission.EnsureCovers(admitted, PurchasingAdmission.ReceiptField);
        if (covers.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(covers.Errors);
        }

        GoodsReceiptRow? receipt = await _database.Receipts
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == receiptId, cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(ReceiptDocument, receiptId));
        }

        if (receipt.State == PurchasingDocumentState.Posted)
        {
            // وصولٌ ثانٍ بعد أن اكتمل الأول: الاستلام لا يُمسّ، والحقيقة تُقال صراحةً.
            return Result<PurchasingDocumentView>.Success(ViewOf(receipt) with { AlreadyPosted = true });
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

        // ‏**حكم البوّابة على مستوى الاستلام كلّه:** الاستلام يُرحَّل سطراً سطراً، وكل
        // سطر هويةُ إحكامٍ مستقلّة. فـ«رُحّل سلفاً» عن الاستلام تعني: **لم يُنشئ هذا
        // النداء قيداً واحداً** — أي أن كل سطر عاد بإيصالٍ موسوم. واستلامٌ عاد أحد
        // سطوره بقيدٍ جديد نداءٌ رحّل فعلاً، ولو كان بقيّة سطوره مُرحَّلة من قبل.
        bool everyLineWasAlreadyPosted = true;

        foreach (PurchaseLineRow line in lines)
        {
            // ── ١ · الدفتر المساعد أولاً ───────────────────────────────────────
            // بهوية الترحيل نفسها حرفاً بحرف: نوع المستند ومعرّفه ورمز الإطلاق
            // والجيل ورمز الحدث. فحركة المخزون وقيد الاستلام واقعةٌ واحدة تُروى
            // مرّتين بمفتاح واحد — لا دفتران يعدّان بحبيبيّتين مختلفتين، ولا
            // انحراف بلا مستند مسؤول (فخ-44 · فخ-48).
            Result<InventoryMovementCost> received = await _valuation.ReceiveAsync(
                new InventoryReceipt
                {
                    Tenant = tenant,
                    Actor = actor,
                    Source = new InventoryMovementSource(
                        BabelModule.Purchasing,
                        ReceiptLineDocument,
                        line.Id.ToString("D", CultureInfo.InvariantCulture),
                        PostingTrigger.OnReceipt.ToString(),
                        receipt.PostingGeneration,
                        ReceiptPostedEvent),
                    Location = new InventoryItemLocation(line.ItemId, receipt.WarehouseId, line.ItemGroup),
                    Quantity = line.Quantity,

                    // تكلفة الوارد هي صافي السطر بالضبط — وهو المبلغ نفسه الذي
                    // يُدين حساب مراقبة المخزون في القيد أدناه. رقمان مختلفان
                    // هنا يعنيان دفترين لا يلتقيان أبداً.
                    Cost = Money.Of(line.LineNet, _currency),
                    OccurredOn = receipt.ReceivedOn,
                },
                cancellationToken).ConfigureAwait(false);

            if (received.IsFailure)
            {
                return Result<PurchasingDocumentView>.Failure(received.Errors);
            }

            // ── ٢ · ثم الحساب الضابط ───────────────────────────────────────────
            PostingIntent intent = new()
            {
                Tenant = tenant,
                DocumentType = ReceiptLineDocument,
                DocumentId = line.Id,
                Trigger = PostingTrigger.OnReceipt,
                Event = new PostingEventCode(ReceiptPostedEvent),
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
            everyLineWasAlreadyPosted &= posted.Value.WasAlreadyPosted;
        }

        receipt.State = PurchasingDocumentState.Posted;
        receipt.PostedEntryId = lastEntry;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // استلامٌ بلا سطر واحد لا يُدَّعى أنه «رُحّل سلفاً»: لا هوية له عند البوّابة
        // أصلاً، فلا حكم لها عليه. (والمسار لا يبلغه اليوم — `RecordAsync` ترفض
        // مسوّدة بلا سطور — والشرط مكتوب كي لا يصير الحياد الابتدائي حكماً.)
        return Result<PurchasingDocumentView>.Success(
            ViewOf(receipt) with { AlreadyPosted = lines.Count > 0 && everyLineWasAlreadyPosted });
    }

    /// <summary>
    /// يقرأ استلاماً بحالته وتكلفته ومعرّف قيده إن رُحّل.
    /// <para>
    /// و<c>EntryId</c> عليه هو قيد <b>آخر سطر</b> رُحّل، لا قيداً واحداً للاستلام:
    /// كل سطر يُرحَّل قيداً مستقلاً لأن قالب المصفوفة يحمل مرجع صنف واحداً ومستودعاً
    /// واحداً على مستوى الطلب. ومن أراد قيود الاستلام كلها قرأ سطوره.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="receiptId">الاستلام.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<PurchasingDocumentView>> GetAsync(
        TenantId tenant,
        UserId actor,
        Guid receiptId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Receipt.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        GoodsReceiptRow? receipt = await _database.Receipts
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == receiptId, cancellationToken)
            .ConfigureAwait(false);

        return receipt is null
            ? Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(ReceiptDocument, receiptId))
            : Result<PurchasingDocumentView>.Success(ViewOf(receipt));
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
