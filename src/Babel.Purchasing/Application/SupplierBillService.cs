using System.Globalization;
using Babel.Contracts.Inventory;
using Babel.Contracts.Parameters;
using Babel.Contracts.Posting;
using Babel.Core.Application;
using Babel.Core.CapabilityProfile;
using Babel.Core.Entitlement;
using Babel.Purchasing.Persistence;
using Babel.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Babel.Purchasing.Application;

/// <summary>
/// فواتير الموردين والإشعارات المدينة — و<b>المطابقة الثلاثية</b>.
/// <para>
/// الأضلاع الثلاثة: أمر الشراء (ما طُلب)، والاستلام (ما وصل)، والفاتورة (ما طولِبنا به).
/// الضلع الذي يمنع أكبر خسارة هو الثاني في الثالث: <b>فاتورة بكمية تتجاوز المستلَم
/// تُرفض</b>. ومن غيره تُدفَع بضاعة لم تصل، ولا يُكتشف ذلك إلا في الجرد السنوي.
/// </para>
/// </summary>
public sealed class SupplierBillService : IApplicationService
{
    /// <summary>نوع مستند فاتورة المورد في هوية الإحكام.</summary>
    internal const string BillDocument = "SupplierBill";

    /// <summary>نوع مستند الإشعار المدين.</summary>
    internal const string DebitNoteDocument = "SupplierDebitNote";

    /// <summary>رمز حدث مرتجع المشتريات في المصفوفة.</summary>
    internal const string DebitNotePostedEvent = "purchasing.debit_note.posted";

    private readonly IEntitlementEnforcer _enforcer;
    private readonly PurchasingDbContext _database;
    private readonly SubledgerPostingGateway _gateway;
    private readonly PurchasingAdmission _admission;
    private readonly IInventoryValuation _valuation;
    private readonly IParameterUsageRecorder _parameterUsage;
    private readonly CurrencyCode _currency;

    /// <summary>ينشئ الخدمة.</summary>
    /// <param name="enforcer">منفِّذ الاستحقاق.</param>
    /// <param name="runtime">موارد الوحدة.</param>
    /// <param name="posting">محرك الترحيل.</param>
    /// <param name="profiles">مخزن ملفّات القدرات — بوابة القبول (‏ADR-0023).</param>
    /// <param name="valuation">
    /// حدّ تقييم المخزون — <b>الجهة الوحيدة التي تُقيّم مرتجع المشتريات</b>. والمصفوفة
    /// تقول إن صافي المرتجع «بتكلفة الاستلام الأصلي»، وتلك التكلفة يملكها دفتر المخزون
    /// وحده. وكان هذا المسار يُدين حساب مراقبة المخزون بمبلغٍ يُسلّمه المستدعي
    /// <b>ولا يكتب حركة واحدة في الدفتر المساعد</b> — أي حسابٌ ضابط يتحرّك ودفترٌ
    /// مساعد ساكن، وهو الانحراف الذي أُنشئت له المطابقة (‏ADR-0041).
    /// </param>
    /// <param name="parameterUsage">
    /// مسجّل استعمال المعامِلات — <b>يُنادى لحظة الترحيل</b> فيصير «أيُّ مستندٍ استعمل
    /// هذا الإصدار؟» استعلاماً واحداً في قاعدة النواة بدل مسحٍ على تسع قواعد. وهو
    /// <b>فهرسٌ لا سجلّ</b>: السجلّ لقطةٌ على الفاتورة نفسها.
    /// </param>
    public SupplierBillService(
        IEntitlementEnforcer enforcer,
        PurchasingRuntime runtime,
        IPostingService posting,
        ICapabilityProfileStore profiles,
        IInventoryValuation valuation,
        IParameterUsageRecorder parameterUsage)
    {
        ArgumentNullException.ThrowIfNull(enforcer);
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentNullException.ThrowIfNull(posting);
        ArgumentNullException.ThrowIfNull(profiles);
        ArgumentNullException.ThrowIfNull(valuation);
        ArgumentNullException.ThrowIfNull(parameterUsage);
        _valuation = valuation;
        _parameterUsage = parameterUsage;
        _enforcer = enforcer;
        _database = runtime.Database;
        _currency = CurrencyCode.FromString(runtime.Options.CompanyCurrency);
        _gateway = new SubledgerPostingGateway(_database, posting, runtime.CostCenters);
        _admission = new PurchasingAdmission(profiles);
    }

    /// <summary>
    /// يسجّل فاتورة مورد مخزنية بعد مطابقتها ثلاثياً.
    /// <para>كمية مفوترة تتجاوز المستلَم غير المفوتَر <b>تُرفض</b> ولا تُقبل بتحفّظ.</para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> CreateStockBillAsync(
        TenantId tenant,
        UserId actor,
        StockBillDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Bill.CreateStock", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        if (draft.Lines.Count == 0)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.NoLines);
        }

        // الفاتورة المخزنية تحمل حقل «الاستلام» — وهو حقل قدرة «المطابقة الثلاثية».
        // ومستأجرٌ بلا هذه القدرة لا استلام عنده أصلاً، فالمسار مرفوض من بابه.
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

        return await CreateAdmittedStockBillAsync(tenant, admitted.Value, draft, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <summary>
    /// <b>الكاتب الوحيد للفاتورة المخزنية — ويطلب <see cref="AdmittedDocument"/> في توقيعه.</b>
    /// <para>
    /// النوع لا يُبنى إلا بالمرور من قبول ملفّ المستأجر، فمن أراد فاتورة مخزنية وجب عليه
    /// أن <b>يحمل</b> قبولاً — لا أن يتذكّر أن يستدعي فحصاً في أعلى الدالّة.
    /// </para>
    /// </summary>
    private async ValueTask<Result<PurchasingDocumentView>> CreateAdmittedStockBillAsync(
        TenantId tenant,
        AdmittedDocument admitted,
        StockBillDraft draft,
        CancellationToken cancellationToken)
    {
        Result covers = PurchasingAdmission.EnsureCovers(admitted, PurchasingAdmission.ReceiptField);
        if (covers.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(covers.Errors);
        }

        GoodsReceiptRow? receipt = await _database.Receipts
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.ReceiptId, cancellationToken)
            .ConfigureAwait(false);

        if (receipt is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound("GoodsReceipt", draft.ReceiptId));
        }

        if (receipt.State != PurchasingDocumentState.Posted)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.NotInState(receipt.Number, receipt.State, PurchasingDocumentState.Posted));
        }

        if (await _database.Bills
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        SupplierRow supplier = await _database.Suppliers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == receipt.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        Guid billId = Guid.CreateVersion7();
        decimal receiptValue = 0m;
        decimal net = 0m;
        decimal tax = 0m;
        bool taxable = false;
        int lineNo = 0;
        string itemGroup = "*";
        HashSet<string> varianceItems = new(StringComparer.Ordinal);

        // ── مرّتان لا مرّة: يُتحقَّق من كل السطور أولاً، ثم تُكتب ───────────────
        // متعقّب EF يحتفظ بأي تعديل أُجري قبل الرفض، فأول SaveChanges لاحق —
        // ولو من نداء آخر على النطاق نفسه — يُثبّته. الفحص ثم الكتابة هو الشكل
        // الوحيد الذي يجعل الرفض بلا أثر.
        List<PendingBillLine> pending = [];

        foreach (SupplierBillLineDraft line in draft.Lines.OrderBy(static l => l.ReceiptLineId))
        {
            PurchaseLineRow? receiptLine = await _database.Lines
                .FirstOrDefaultAsync(
                    row => row.TenantId == tenant.Value && row.OwnerType == LineOwner.Receipt && row.Id == line.ReceiptLineId,
                    cancellationToken)
                .ConfigureAwait(false);

            if (receiptLine is null)
            {
                return Result<PurchasingDocumentView>.Failure(PurchasingErrors.LineNotFound(line.ReceiptLineId));
            }

            // ── الضلع الثالث: المفوتَر ≤ المستلَم غير المفوتَر ────────────────
            decimal available = receiptLine.Quantity - receiptLine.BilledQuantity;
            if (line.Quantity > available)
            {
                return Result<PurchasingDocumentView>.Failure(
                    PurchasingErrors.BillExceedsReceipt(receiptLine.ItemId, line.Quantity, available));
            }

            decimal lineReceiptValue = LineMath.Round(line.Quantity * receiptLine.UnitPrice);
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, 0m, line.TaxRate, line.TaxClassification);

            receiptValue += lineReceiptValue;
            net += lineNet;
            tax += lineTax;
            taxable |= string.Equals(line.TaxClassification, "standard", StringComparison.Ordinal);
            itemGroup = receiptLine.ItemGroup;

            if (lineNet != lineReceiptValue)
            {
                varianceItems.Add(receiptLine.ItemId);
            }

            pending.Add(new PendingBillLine(receiptLine, line, lineNet, lineTax));
        }

        decimal variance = net - receiptValue;

        if (variance < 0m)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.FavourablePriceVarianceNotExpressible(variance));
        }

        if (varianceItems.Count > 1)
        {
            return Result<PurchasingDocumentView>.Failure(new Error(
                "purchasing.price_variance_spans_items",
                "فرق سعر على أكثر من صنف في فاتورة واحدة: قالب المصفوفة يحمل مرجع صنف واحداً "
                + "على مستوى الطلب، فلا سبيل لنسبة الفرق إلى أصنافه. افصل الفاتورة.",
                "A price variance across more than one item in a single bill: the matrix template carries one "
                + "item reference at request level, so the variance cannot be attributed. Split the bill."));
        }

        foreach (PendingBillLine entry in pending)
        {
            lineNo++;
            _database.Lines.Add(new PurchaseLineRow
            {
                Id = Guid.CreateVersion7(),
                TenantId = tenant.Value,
                OwnerType = LineOwner.Bill,
                OwnerId = billId,
                LineNo = lineNo,
                OrderLineId = entry.ReceiptLine.OrderLineId,
                ReceiptLineId = entry.ReceiptLine.Id,
                ItemId = entry.ReceiptLine.ItemId,
                ItemGroup = entry.ReceiptLine.ItemGroup,
                DescriptionAr = entry.ReceiptLine.DescriptionAr,
                DescriptionEn = entry.ReceiptLine.DescriptionEn,
                Quantity = entry.Draft.Quantity,
                Unit = entry.ReceiptLine.Unit,
                UnitPrice = entry.Draft.UnitPrice.Amount,
                TaxClassification = entry.Draft.TaxClassification,
                TaxRate = entry.Draft.TaxRate,
                LineNet = entry.Net,
                LineTax = entry.Tax,
            });

            entry.ReceiptLine.BilledQuantity += entry.Draft.Quantity;
        }

        SupplierBillRow bill = new()
        {
            Id = billId,
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = receipt.SupplierId,
            OrderId = receipt.OrderId,
            ReceiptId = receipt.Id,
            IssuedOn = draft.IssuedOn,
            DueOn = draft.IssuedOn.AddDays(supplier.PaymentTermsDays),
            State = PurchasingDocumentState.Draft,
            CurrencyCode = _currency.Value,
            WarehouseId = receipt.WarehouseId,
            ItemGroup = itemGroup,
            BillKind = "STOCK",
            HasTaxableLine = taxable,
            ReceiptValue = receiptValue,
            PriceVariance = variance,
            NetTotal = net,
            TaxTotal = tax,
            RecoverableTax = tax,
            NonRecoverableTax = 0m,
            GrossTotal = net + tax,
        };

        _database.Bills.Add(bill);
        receipt.BilledValue += receiptValue;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(ViewOf(bill));
    }

    /// <summary>يسجّل فاتورة مصروف مباشر بلا مخزون ولا مطابقة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> CreateExpenseBillAsync(
        TenantId tenant,
        UserId actor,
        ExpenseBillDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Bill.CreateExpense", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        if (draft.Lines.Count == 0)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.NoLines);
        }

        SupplierRow? supplier = await _database.Suppliers
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        if (supplier is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.SupplierNotFound(draft.SupplierId));
        }

        if (await _database.Bills
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        decimal net = 0m;
        decimal recoverable = 0m;
        decimal nonRecoverable = 0m;
        bool taxable = false;

        foreach (PurchaseLineDraft line in draft.Lines)
        {
            (decimal lineNet, decimal lineTax) = LineMath.Line(
                line.Quantity, line.UnitPrice.Amount, 0m, line.TaxRate, line.TaxClassification);
            net += lineNet;

            // نسبة الاسترداد قرار سطر لا مستند: خلطهما يجعل ضريبة غير مستردة تُطالَب بها.
            if (line.TaxRecoverable)
            {
                recoverable += lineTax;
            }
            else
            {
                nonRecoverable += lineTax;
            }

            taxable |= string.Equals(line.TaxClassification, "standard", StringComparison.Ordinal);
        }

        Guid billId = Guid.CreateVersion7();

        SupplierBillRow bill = new()
        {
            Id = billId,
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = supplier.Id,
            IssuedOn = draft.IssuedOn,
            DueOn = draft.IssuedOn.AddDays(supplier.PaymentTermsDays),
            State = PurchasingDocumentState.Draft,
            CurrencyCode = _currency.Value,
            CostCenterId = draft.CostCenterId,
            ExpenseCategory = draft.ExpenseCategory,
            BillKind = "EXPENSE",
            HasTaxableLine = taxable,
            NetTotal = net,
            TaxTotal = recoverable + nonRecoverable,
            RecoverableTax = recoverable,
            NonRecoverableTax = nonRecoverable,
            GrossTotal = net + recoverable + nonRecoverable,

            // ‏**اللقطة تُكتب مع الفاتورة لا عند ترحيلها**: ما دخل الحساب دخله لحظة
            // الإنشاء، وفاتورةٌ مسوّدة تُقرأ بعد شهر يجب أن تقول بأي رقمٍ حُسبت.
            ParameterSnapshot = draft.Parameters?.Canonical() ?? string.Empty,
        };

        _database.Bills.Add(bill);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(ViewOf(bill));
    }

    /// <summary>يرحّل فاتورة المورد — المخزنية عبر قالبها والمصروفية عبر قالبه.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="billId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> PostBillAsync(
        TenantId tenant,
        UserId actor,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.Bill.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        SupplierBillRow? bill = await _database.Bills
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == billId, cancellationToken)
            .ConfigureAwait(false);

        if (bill is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(BillDocument, billId));
        }

        if (bill.State == PurchasingDocumentState.Posted)
        {
            // وصولٌ ثانٍ بعد أن اكتمل الأول: لا يُمسّ شيء، والحقيقة تُقال صراحةً
            // بدل أن تُترك لتُشتقّ من حالةٍ لا تفرّق بين النداءين.
            //
            // ‏**ويُعاد تسجيل الاستعمال هنا عمداً**: السجلّ آمنُ التكرار، والنداء الثاني
            // هو ما يُبرئ فهرسَ المراجعة إن سقط تسجيلُ النداء الأول بعطلٍ عابر. وهو
            // فهرسٌ لا سجلّ، فترميمُه بإعادة نداءٍ لا يمسّ رقماً محاسبياً.
            await RecordParameterUsageAsync(tenant, bill, cancellationToken).ConfigureAwait(false);
            return Result<PurchasingDocumentView>.Success(ViewOf(bill) with { AlreadyPosted = true });
        }

        SupplierRow supplier = await _database.Suppliers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == bill.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        // ── الحدث المخزني تفتحه قدرة، والمصروفي هو الحدث الأساسي ──────────────
        // فالترحيل المخزني يمرّ بالقبول ويحمل تذكرته إلى كاتبه؛ والمصروفي لا يمارس
        // قدرة فلا تذكرة له. وهذا هو الفارق الذي يعبّر عنه الكتالوج بالضبط.
        PostingIntent intent;

        if (string.Equals(bill.BillKind, "STOCK", StringComparison.Ordinal))
        {
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

            Result<PostingIntent> stock = await AdmittedStockIntentAsync(
                tenant, actor, admitted.Value, bill, supplier, cancellationToken).ConfigureAwait(false);

            if (stock.IsFailure)
            {
                return Result<PurchasingDocumentView>.Failure(stock.Errors);
            }

            intent = stock.Value;
        }
        else
        {
            intent = ExpenseIntent(tenant, actor, bill, supplier);
        }

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(posted.Errors);
        }

        bill.State = PurchasingDocumentState.Posted;
        bill.PostedEntryId = posted.Value.JournalEntryId;
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        await RecordParameterUsageAsync(tenant, bill, cancellationToken).ConfigureAwait(false);

        // حكم البوّابة لا حكمنا: نداءان متزامنان يجتازان فحص الحالة معاً ويلتقيان عند
        // هوية إحكام واحدة، فأحدهما يكتب والآخر يعود بإيصاله موسوماً.
        return Result<PurchasingDocumentView>.Success(
            ViewOf(bill) with { AlreadyPosted = posted.Value.WasAlreadyPosted });
    }

    /// <summary>يسجّل إشعاراً مديناً على فاتورة مُرحَّلة.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="draft">المسوّدة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> CreateDebitNoteAsync(
        TenantId tenant,
        UserId actor,
        DebitNoteDraft draft,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(draft);

        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.DebitNote.Create", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        SupplierBillRow? bill = await _database.Bills
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == draft.BillId, cancellationToken)
            .ConfigureAwait(false);

        if (bill is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(BillDocument, draft.BillId));
        }

        if (bill.State != PurchasingDocumentState.Posted)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.NotInState(bill.Number, bill.State, PurchasingDocumentState.Posted));
        }

        if (!string.Equals(bill.BillKind, "STOCK", StringComparison.Ordinal))
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.DebitNoteOnExpenseBillNotExpressible(bill.Number));
        }

        if (draft.Quantity <= 0m)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.NegativeAmount);
        }

        // ── سطر الاستلام يُتحقَّق أنه صفٌّ قائم يخصّ فاتورة هذا المرتجع ───────────
        // ومعرّفٌ مخترَع يُرفض باسمه: به يُقيَّم المرتجع، فقبولُه بلا تحقّق يعني
        // تقييماً بحركة صنفٍ آخر — بقيدٍ متوازن تماماً.
        PurchaseLineRow? billLine = await _database.Lines
            .AsNoTracking()
            .FirstOrDefaultAsync(
                row => row.TenantId == tenant.Value
                       && row.OwnerType == LineOwner.Bill
                       && row.OwnerId == bill.Id
                       && row.ReceiptLineId == draft.ReceiptLineId,
                cancellationToken)
            .ConfigureAwait(false);

        if (billLine is null)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.LineNotFound(draft.ReceiptLineId));
        }

        if (draft.Quantity > billLine.Quantity)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.ReturnExceedsBilled(bill.Number, billLine.Quantity, draft.Quantity));
        }

        if (await _database.DebitNotes
                .AnyAsync(row => row.TenantId == tenant.Value && row.Number == draft.Number, cancellationToken)
                .ConfigureAwait(false))
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DuplicateNumber(draft.Number));
        }

        DebitNoteRow note = new()
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            Number = draft.Number,
            SupplierId = bill.SupplierId,
            BillId = bill.Id,
            IssuedOn = draft.IssuedOn,
            State = PurchasingDocumentState.Draft,
            CurrencyCode = _currency.Value,
            WarehouseId = bill.WarehouseId,
            ItemGroup = billLine.ItemGroup,
            ItemId = billLine.ItemId,
            ReceiptLineId = draft.ReceiptLineId,
            Quantity = draft.Quantity,
            OriginalWasTaxable = bill.HasTaxableLine,

            // الصافي صفرٌ ما دام مسوّدة: يُحسب لحظة الترحيل في وحدة المخزون ولا يُملى.
            NetTotal = 0m,
            TaxTotal = draft.Tax.Amount,
            GrossTotal = draft.Tax.Amount,
        };

        _database.DebitNotes.Add(note);
        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return Result<PurchasingDocumentView>.Success(new PurchasingDocumentView(
            note.Id,
            note.Number,
            note.State,
            new DocumentTotals(
                Money.Of(note.NetTotal, _currency),
                Money.Of(note.TaxTotal, _currency),
                Money.Of(note.GrossTotal, _currency)),
            null));
    }

    /// <summary>يرحّل الإشعار المدين ويخصّصه على فاتورته.</summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="debitNoteId">الإشعار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Write)]
    public async ValueTask<Result<PurchasingDocumentView>> PostDebitNoteAsync(
        TenantId tenant,
        UserId actor,
        Guid debitNoteId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Write, "Purchasing.DebitNote.Post", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        // ── القبول: المرتجع يمارس قدرة المطابقة الثلاثية نفسها ─────────────────
        // وهو الشقّ العكسي منها: البضاعة التي دخلت باستلامٍ تخرج بمرتجع، ويُقيَّم
        // كلاهما بحركة الاستلام نفسها. ومستأجرٌ لا يملك القدرة لا استلام عنده أصلاً،
        // فلا مرتجع — والرفض هنا أصدق من مسارٍ يعمل على وحدةٍ لم تُشترَ.
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

        Result covers = PurchasingAdmission.EnsureCovers(admitted.Value, PurchasingAdmission.ReceiptField);
        if (covers.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(covers.Errors);
        }

        DebitNoteRow? note = await _database.DebitNotes
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == debitNoteId, cancellationToken)
            .ConfigureAwait(false);

        if (note is null)
        {
            return Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(DebitNoteDocument, debitNoteId));
        }

        if (note.State == PurchasingDocumentState.Posted)
        {
            return Result<PurchasingDocumentView>.Success(ViewOfNote(note) with { AlreadyPosted = true });
        }

        SupplierRow supplier = await _database.Suppliers
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == note.SupplierId, cancellationToken)
            .ConfigureAwait(false);

        SupplierBillRow bill = await _database.Bills
            .FirstAsync(row => row.TenantId == tenant.Value && row.Id == note.BillId, cancellationToken)
            .ConfigureAwait(false);

        // ── ١ · الدفتر المساعد أوّلاً — ومنه يأتي **صافي المرتجع** ──────────────
        // «بتكلفة الاستلام الأصلي لا بتكلفة اليوم» — نصّ المصفوفة على هذا الحدث.
        // وهوية الحركة الأصلية هي هوية ترحيل سطر الاستلام حرفاً بحرف، وجيلُها يُقرأ
        // من سجلّ المحاولات ولا يُفترَض: استلامٌ عُكس ثم أُعيد يحمل حركته على جيله.
        string receiptLineId = note.ReceiptLineId.ToString("D", CultureInfo.InvariantCulture);
        string receiptTrigger = PostingTrigger.OnReceipt.ToString();

        DocumentPostingRow? receiptPosting = await _database.Postings
            .AsNoTracking()
            .Where(row => row.TenantId == tenant.Value
                          && row.DocumentType == GoodsReceiptService.ReceiptLineDocument
                          && row.DocumentId == receiptLineId
                          && row.TriggerCode == receiptTrigger
                          && row.EventCode == GoodsReceiptService.ReceiptPostedEvent
                          && row.State == PostingAttemptState.Posted)
            .OrderByDescending(row => row.Generation)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (receiptPosting is null)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.OriginalReceiptMovementNotFound(note.ReceiptLineId));
        }

        InventoryMovementSource receiptMovement = new(
            BabelModule.Purchasing,
            GoodsReceiptService.ReceiptLineDocument,
            receiptLineId,
            receiptTrigger,
            receiptPosting.Generation,
            GoodsReceiptService.ReceiptPostedEvent);

        // وحدة المرتجع هي وحدة الاستلام — تُقرأ ولا تُخترَع.
        Result<InventoryMovementCost> received = await _valuation
            .ReadMovementAsync(tenant, actor, receiptMovement, cancellationToken).ConfigureAwait(false);

        if (received.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(received.Errors);
        }

        Result<InventoryMovementCost> returned = await _valuation.ReturnAsync(
            new InventoryReturn
            {
                Tenant = tenant,
                Actor = actor,
                Source = new InventoryMovementSource(
                    BabelModule.Purchasing,
                    DebitNoteDocument,
                    note.Id.ToString("D", CultureInfo.InvariantCulture),
                    PostingTrigger.OnApproval.ToString(),
                    note.PostingGeneration,
                    DebitNotePostedEvent),
                OriginalMovement = receiptMovement,
                Quantity = new InventoryQuantity(note.Quantity, received.Value.Quantity.Unit),
                OccurredOn = note.IssuedOn,
            },
            cancellationToken).ConfigureAwait(false);

        if (returned.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(returned.Errors);
        }

        // ── ٢ · المبالغ تُبنى على الرقم المحسوب، ثم يُفحص التخصيص عليه ─────────
        note.NetTotal = returned.Value.Cost.Amount;
        note.GrossTotal = note.NetTotal + note.TaxTotal;

        decimal outstanding = bill.GrossTotal - bill.AllocatedAmount;

        if (note.GrossTotal > outstanding)
        {
            return Result<PurchasingDocumentView>.Failure(
                PurchasingErrors.OverAllocation(bill.Number, note.GrossTotal, outstanding));
        }

        PostingIntent intent = new()
        {
            Tenant = tenant,
            DocumentType = DebitNoteDocument,
            DocumentId = note.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode(DebitNotePostedEvent),
            DocumentDate = note.IssuedOn,
            Narration = new LocalizedName("إشعار مدين " + note.Number, "Debit note " + note.Number),
            Amounts =
            [
                new PostingAmount("net", Money.Of(note.NetTotal, _currency)),
                new PostingAmount("tax", Money.Of(note.TaxTotal, _currency)),
            ],
            Facts =
            [
                new PostingFact("condition.original_was_taxable", note.OriginalWasTaxable ? "true" : "false"),
                new PostingFact("source_invoice.line.tax_classification", note.OriginalWasTaxable ? "standard" : "exempt"),
                new PostingFact("subledger.supplier", supplier.Code),
                new PostingFact("subledger.item", note.ItemId),
                new PostingFact("line.item_group", note.ItemGroup),
            ],
            Dimensions = [new PostingDimension("warehouse", note.WarehouseId)],
            PartyId = supplier.Code,
            ControlEffect = -note.GrossTotal,
            Currency = _currency,
            Actor = actor,
            Generation = note.PostingGeneration,
        };

        Result<PostingReceipt> posted = await _gateway.PostAsync(intent, cancellationToken).ConfigureAwait(false);
        if (posted.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(posted.Errors);
        }

        note.State = PurchasingDocumentState.Posted;
        note.PostedEntryId = posted.Value.JournalEntryId;
        note.AllocatedAmount = note.GrossTotal;
        bill.AllocatedAmount += note.GrossTotal;

        _database.Allocations.Add(new PayableAllocationRow
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenant.Value,
            SourceType = "DEBIT_NOTE",
            SourceId = note.Id,
            BillId = bill.Id,
            LineNo = 1,
            AllocatedAmount = note.GrossTotal,
            AllocatedOn = note.IssuedOn,
        });

        await _database.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return Result<PurchasingDocumentView>.Success(ViewOfNote(note));
    }

    /// <summary>سطر فاتورة مُتحقَّق منه ولم يُكتب بعد.</summary>
    private sealed record PendingBillLine(
        PurchaseLineRow ReceiptLine,
        SupplierBillLineDraft Draft,
        decimal Net,
        decimal Tax);

    /// <summary>
    /// <b>الكاتب الوحيد لقيد الفاتورة المخزنية — ويطلب <see cref="AdmittedDocument"/> في توقيعه.</b>
    /// <para>
    /// وهو الموضع الذي يُذكر فيه <c>purchasing.invoice.stock.posted</c> نصّاً. فالحارس
    /// الذي يقرأ اللغة الوسيطة يرى أن كل نقطة دخول تبلغ هذا النصّ تبلغ القبول أيضاً.
    /// </para>
    /// </summary>
    private async Task<Result<PostingIntent>> AdmittedStockIntentAsync(
        TenantId tenant,
        UserId actor,
        AdmittedDocument admitted,
        SupplierBillRow bill,
        SupplierRow supplier,
        CancellationToken cancellationToken)
    {
        Result covers = PurchasingAdmission.EnsureCovers(admitted, PurchasingAdmission.ReceiptField);
        if (covers.IsFailure)
        {
            return Result<PostingIntent>.Failure(covers.Errors);
        }

        PurchaseLineRow first = await _database.Lines
            .AsNoTracking()
            .Where(row => row.OwnerType == LineOwner.Bill && row.OwnerId == bill.Id)
            .OrderBy(row => row.LineNo)
            .FirstAsync(cancellationToken)
            .ConfigureAwait(false);

        return Result<PostingIntent>.Success(new PostingIntent
        {
            Tenant = tenant,
            DocumentType = BillDocument,
            DocumentId = bill.Id,
            Trigger = PostingTrigger.OnApproval,
            Event = new PostingEventCode("purchasing.invoice.stock.posted"),
            DocumentDate = bill.IssuedOn,
            Narration = new LocalizedName("فاتورة مورد " + bill.Number, "Supplier bill " + bill.Number),
            Amounts =
            [
                new PostingAmount("receipt_value", Money.Of(bill.ReceiptValue, _currency)),
                new PostingAmount("price_variance", Money.Of(bill.PriceVariance, _currency)),
                new PostingAmount("tax", Money.Of(bill.TaxTotal, _currency)),
            ],
            Facts =
            [
                // ‏abs(...) و has_any_line_with(...) دالتان لا يقيّمهما المحرك:
                // الوحدة تُصرّح بنتيجتيهما، ولا يُخمَّن شيء.
                new PostingFact("condition.has_price_variance", bill.PriceVariance != 0m ? "true" : "false"),
                new PostingFact("condition.is_taxable_purchase", bill.HasTaxableLine ? "true" : "false"),
                new PostingFact("subledger.supplier", supplier.Code),
                new PostingFact("subledger.item", first.ItemId),
                new PostingFact("line.item_group", bill.ItemGroup),
            ],
            Dimensions = [new PostingDimension("warehouse", bill.WarehouseId)],
            PartyId = supplier.Code,

            // الفاتورة تستهلك رصيد البضاعة المستلمة غير المفوترة وتُنشئ ذمة كاملة:
            // صافي أثرها على نقطة ضبط الموردين هو الفرق والضريبة.
            ControlEffect = bill.GrossTotal - bill.ReceiptValue,
            Currency = _currency,
            Actor = actor,
            Generation = bill.PostingGeneration,
        });
    }

    private PostingIntent ExpenseIntent(TenantId tenant, UserId actor, SupplierBillRow bill, SupplierRow supplier) => new()
    {
        Tenant = tenant,
        DocumentType = BillDocument,
        DocumentId = bill.Id,
        Trigger = PostingTrigger.OnApproval,
        Event = new PostingEventCode("purchasing.invoice.expense.posted"),
        DocumentDate = bill.IssuedOn,
        Narration = new LocalizedName("فاتورة مصروف " + bill.Number, "Expense bill " + bill.Number),
        Amounts =
        [
            new PostingAmount("net", Money.Of(bill.NetTotal, _currency)),
            new PostingAmount("recoverable_tax", Money.Of(bill.RecoverableTax, _currency)),
            new PostingAmount("non_recoverable_tax", Money.Of(bill.NonRecoverableTax, _currency)),
        ],
        Facts =
        [
            new PostingFact("subledger.supplier", supplier.Code),
            new PostingFact("line.expense_category", bill.ExpenseCategory),
        ],
        Dimensions = [new PostingDimension("cost_center", bill.CostCenterId)],
        PartyId = supplier.Code,
        ControlEffect = bill.GrossTotal,
        Currency = _currency,
        Actor = actor,
        Generation = bill.PostingGeneration,
    };

    /// <summary>
    /// يقرأ فاتورة مورد بحالتها ومجاميعها. <b>نقطة قراءة</b>: تعمل عند
    /// <see cref="EntitlementState.ReadOnly"/> أيضاً.
    /// <para>
    /// وكانت غائبة: الوحدة تُنشئ الفاتورة وتُرحّلها ولا تملك جملةً تقول بها «ما حال
    /// هذه الفاتورة الآن؟». فمن أنشأ مسوّدةً ثم انقطع اتصاله لم يكن أمامه إلا أن
    /// <b>يعيد الترحيل ليعرف</b> — وهو أسوأ ما يُطلب من عميل في مسار مالي.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="billId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<PurchasingDocumentView>> GetBillAsync(
        TenantId tenant,
        UserId actor,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Bill.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        SupplierBillRow? bill = await _database.Bills
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == billId, cancellationToken)
            .ConfigureAwait(false);

        return bill is null
            ? Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(BillDocument, billId))
            : Result<PurchasingDocumentView>.Success(ViewOf(bill));
    }

    /// <summary>
    /// يقرأ إشعاراً مديناً (مرتجع مشتريات) بحالته ومجاميعه.
    /// <para>
    /// <b>وكانت هذه القراءة غير موجودة</b>: يُنشأ المرتجع ويُرحَّل ولا توجد جملة تقول
    /// «ما حاله الآن؟» — فمن انقطع اتصاله بعد الإنشاء لم يكن أمامه إلا أن يُعيد
    /// الترحيل ليعرف، وهو أسوأ ما يُطلب من عميل في مسار مالي.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="debitNoteId">الإشعار.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<PurchasingDocumentView>> GetDebitNoteAsync(
        TenantId tenant,
        UserId actor,
        Guid debitNoteId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.DebitNote.Get", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<PurchasingDocumentView>.Failure(gate.Errors);
        }

        DebitNoteRow? note = await _database.DebitNotes
            .AsNoTracking()
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == debitNoteId, cancellationToken)
            .ConfigureAwait(false);

        return note is null
            ? Result<PurchasingDocumentView>.Failure(PurchasingErrors.DocumentNotFound(DebitNoteDocument, debitNoteId))
            : Result<PurchasingDocumentView>.Success(ViewOfNote(note));
    }

    /// <summary>
    /// <b>ما تتذكّره الفاتورة من معامِلات — أو غيابُه.</b>
    /// <para>
    /// وهذه هي الإجابة على «بأي رقمٍ حُسب هذا؟» بعد سنتين: تُقرأ من الفاتورة نفسها،
    /// في قاعدة وحدتها، <b>بلا نداءٍ إلى قاعدةٍ أخرى</b> ولو تغيّر الإصدار الجاري
    /// عشر مرّات بعدها.
    /// </para>
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    /// <param name="actor">الفاعل.</param>
    /// <param name="billId">الفاتورة.</param>
    /// <param name="cancellationToken">رمز الإلغاء.</param>
    [RequiresEntitlement(BabelModule.Purchasing, EntitlementAccess.Read)]
    public async ValueTask<Result<ParameterSnapshot?>> ReadBillParametersAsync(
        TenantId tenant,
        UserId actor,
        Guid billId,
        CancellationToken cancellationToken = default)
    {
        Result gate = await _enforcer
            .EnsureAsync(tenant, actor, BabelModule.Purchasing, EntitlementAccess.Read, "Purchasing.Bill.ReadParameters", cancellationToken)
            .ConfigureAwait(false);

        if (gate.IsFailure)
        {
            return Result<ParameterSnapshot?>.Failure(gate.Errors);
        }

        SupplierBillRow? bill = await _database.Bills
            .FirstOrDefaultAsync(row => row.TenantId == tenant.Value && row.Id == billId, cancellationToken)
            .ConfigureAwait(false);

        if (bill is null)
        {
            return Result<ParameterSnapshot?>.Failure(PurchasingErrors.DocumentNotFound(BillDocument, billId));
        }

        return Result<ParameterSnapshot?>.Success(
            bill.ParameterSnapshot.Length == 0 ? null : Babel.Contracts.Parameters.ParameterSnapshot.Parse(bill.ParameterSnapshot));
    }

    /// <summary>
    /// يسجّل أن هذه الفاتورة استعملت إصدار المعامِلات المحفوظ عليها. آمنُ التكرار،
    /// و<b>لا يفعل شيئاً</b> حين لا لقطة على الفاتورة — فما لم يُستعمل لا يُسجَّل.
    /// </summary>
    private async Task RecordParameterUsageAsync(
        TenantId tenant, SupplierBillRow bill, CancellationToken cancellationToken)
    {
        if (bill.ParameterSnapshot.Length == 0)
        {
            return;
        }

        ParameterSnapshot snapshot = Babel.Contracts.Parameters.ParameterSnapshot.Parse(bill.ParameterSnapshot);

        await _parameterUsage
            .RecordAsync(
                tenant,
                new ParameterUsage(
                    snapshot.VersionId, BabelModule.Purchasing, "SUPPLIER_BILL", bill.Id, bill.IssuedOn),
                cancellationToken)
            .ConfigureAwait(false);
    }

    private PurchasingDocumentView ViewOf(SupplierBillRow bill) => new(
        bill.Id,
        bill.Number,
        bill.State,
        new DocumentTotals(
            Money.Of(bill.NetTotal, _currency),
            Money.Of(bill.TaxTotal, _currency),
            Money.Of(bill.GrossTotal, _currency)),
        bill.PostedEntryId);

    private PurchasingDocumentView ViewOfNote(DebitNoteRow note) => new(
        note.Id,
        note.Number,
        note.State,
        new DocumentTotals(
            Money.Of(note.NetTotal, _currency),
            Money.Of(note.TaxTotal, _currency),
            Money.Of(note.GrossTotal, _currency)),
        note.PostedEntryId);
}
