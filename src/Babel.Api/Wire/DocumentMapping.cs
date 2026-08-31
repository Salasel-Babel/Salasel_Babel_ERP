using System.Globalization;
using Babel.Inventory.Surface;
using Babel.Purchasing.Surface;
using Babel.Sales.Surface;
using Babel.SharedKernel;

namespace Babel.Api.Wire;

/// <summary>
/// نقل مستندات المبيعات والمشتريات بين السلك والسطح المنشور للوحدتين.
/// <para>
/// <b>نقلٌ لا حساب.</b> لا مجموع ولا فرق ولا تقريب في هذا الملفّ: المجاميع تصل محسوبةً
/// من الوحدة، والمبالغ تُنسَّق بتمثيلها القانوني وحده. والقاعدة 13 (البند أ) تفرض ذلك
/// بمسح IL لا بالمراجعة.
/// </para>
/// <para>
/// <b>وكل تاريخ ورقم يمرّ من هنا يمرّ بماسح واحد</b> — هو ماسح <see cref="WireMapping"/>
/// نفسه لا نسخة ثانية منه. ونسختان من ماسح تاريخ تفترقان عند أول تعديل، فيقبل أحد
/// المسارين ما يرفضه الآخر (فخ-40).
/// </para>
/// </summary>
internal static class DocumentMapping
{
    /// <summary>أقصى عدد سطور في مستند واحد — حدٌّ معلن لا مفاجأة عند أول حمولة كبيرة.</summary>
    public const int MaxLines = 1000;

    /// <summary>أقصى مهلة سداد بالأيام: عشر سنوات. وما فوقها خطأ إدخال لا شرط تجاري.</summary>
    public const int MaxPaymentTermsDays = 3650;

    /// <summary>أقصى حدّ لبسط معامل التحويل أو مقامه — حدٌّ معلن لا مفاجأة.</summary>
    public const long MaxUnitFactor = 1_000_000_000L;

    private const int CodeLength = 64;
    private const int ClassificationLength = 32;
    private const int UnitLength = 32;

    /// <summary>يقرأ طلب عميل من السلك.</summary>
    /// <param name="dto">الحمولة.</param>
    public static SalesPartyRequest ToCustomerRequest(PartyRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.VatNumber is not null)
        {
            // العميل لا يحمل رقم تسجيل ضريبي على هذا السطح. والتجاهل الصامت يجعل
            // المُرسِل يظنّ أنه سجّل رقماً لم يصل — وهو صمت في بيانات أساسية.
            throw WireNumbers.Reject(
                "wire.field.not_on_this_resource",
                "vatNumber",
                "رقم التسجيل الضريبي حقل مورد لا حقل عميل على هذا السطح.",
                "The VAT number is a supplier field, not a customer field on this surface.");
        }

        return new SalesPartyRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            WireMapping.ReadLocalized(dto.Name, "name"),
            WireNumbers.ParseStrict(dto.CreditLimit.Raw, WireNumbers.MoneyScale, "creditLimit"),
            ReadTerms(dto.PaymentTermsDays));
    }

    /// <summary>يقرأ طلب مورد من السلك.</summary>
    /// <param name="dto">الحمولة.</param>
    public static PurchasingPartyRequest ToSupplierRequest(PartyRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PurchasingPartyRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            WireMapping.ReadLocalized(dto.Name, "name"),
            WireNumbers.ParseStrict(dto.CreditLimit.Raw, WireNumbers.MoneyScale, "creditLimit"),
            ReadTerms(dto.PaymentTermsDays),

            // فراغٌ لا null: «لم يُسجَّل» حالةٌ واحدة، ولها تمثيل واحد في الوحدة.
            WireMapping.ReadOptional(dto.VatNumber, "vatNumber", CodeLength) ?? string.Empty);
    }

    /// <summary>يقرأ طلب فاتورة مبيعات مسوّدة.</summary>
    /// <param name="dto">الحمولة.</param>
    public static SalesInvoiceRequest ToInvoiceRequest(SalesInvoiceRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new SalesInvoiceRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.CustomerId, "customerId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            WireMapping.ReadRequiredText(dto.BranchId, "branchId", CodeLength),
            SalesLines(dto.Lines));
    }

    /// <summary>يقرأ طلب إشعار دائن مسوّدة.</summary>
    /// <param name="dto">الحمولة.</param>
    public static SalesCreditNoteRequest ToCreditNoteRequest(CreditNoteRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new SalesCreditNoteRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.InvoiceId, "invoiceId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            SalesLines(dto.Lines));
    }

    /// <summary>يقرأ طلب فاتورة مصروف مسوّدة.</summary>
    /// <param name="dto">الحمولة.</param>
    public static PurchasingExpenseBillRequest ToExpenseBillRequest(ExpenseBillRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PurchasingExpenseBillRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.SupplierId, "supplierId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            WireMapping.ReadRequiredText(dto.ExpenseCategory, "expenseCategory", CodeLength),
            WireMapping.ReadRequiredText(dto.CostCenterId, "costCenterId", CodeLength),
            PurchaseLines(dto.Lines));
    }

    /// <summary>يقرأ طلب سند قبض مسوّدة.</summary>
    /// <param name="dto">الحمولة.</param>
    public static SalesReceiptRequest ToCustomerReceiptRequest(CustomerReceiptRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new SalesReceiptRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.CustomerId, "customerId"),
            WireMapping.ReadDate(dto.ReceivedOn, "receivedOn"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", ClassificationLength),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", CodeLength),
            WireNumbers.ParseStrict(dto.Received.Raw, WireNumbers.MoneyScale, "received"),
            WireNumbers.ParseStrict(dto.SettlementDiscount.Raw, WireNumbers.MoneyScale, "settlementDiscount"),
            ReceiptAllocations(dto.Allocations));
    }

    /// <summary>يقرأ طلب سند صرف مسوّدة.</summary>
    /// <param name="dto">الحمولة.</param>
    public static PurchasingPaymentRequest ToSupplierPaymentRequest(SupplierPaymentRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PurchasingPaymentRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.SupplierId, "supplierId"),
            WireMapping.ReadDate(dto.PaidOn, "paidOn"),
            WireMapping.ReadRequiredText(dto.SettlementMethod, "settlementMethod", ClassificationLength),
            WireMapping.ReadRequiredText(dto.TreasuryPartyId, "treasuryPartyId", CodeLength),
            WireNumbers.ParseStrict(dto.Paid.Raw, WireNumbers.MoneyScale, "paid"),
            WireNumbers.ParseStrict(dto.BankFee.Raw, WireNumbers.MoneyScale, "bankFee"),
            PaymentAllocations(dto.Allocations));
    }

    /// <summary>يقرأ طلب أمر شراء.</summary>
    /// <param name="dto">الحمولة.</param>
    public static PurchasingOrderRequest ToPurchaseOrderRequest(PurchaseOrderRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PurchasingOrderRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.SupplierId, "supplierId"),
            WireMapping.ReadDate(dto.OrderedOn, "orderedOn"),
            WireMapping.ReadRequiredText(dto.WarehouseId, "warehouseId", CodeLength),
            WireMapping.ReadRequiredText(dto.CostCenterId, "costCenterId", CodeLength),
            PurchaseLines(dto.Lines));
    }

    /// <summary>يقرأ طلب استلام بضاعة مسوّدة.</summary>
    /// <param name="dto">الحمولة.</param>
    public static PurchasingGoodsReceiptRequest ToGoodsReceiptRequest(GoodsReceiptRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PurchasingGoodsReceiptRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.OrderId, "orderId"),
            WireMapping.ReadDate(dto.ReceivedOn, "receivedOn"),
            ReceiptLines(dto.Lines));
    }

    /// <summary>ينقل أمر شراء بسطوره إلى السلك.</summary>
    /// <param name="order">الأمر.</param>
    public static PurchaseOrderDto ToDto(PurchasingOrder order)
    {
        ArgumentNullException.ThrowIfNull(order);

        return new PurchaseOrderDto(
            Id(order.Id),
            order.Number,
            order.State,
            WireNumbers.FormatMoney(order.Net),
            WireNumbers.FormatMoney(order.Tax),
            WireNumbers.FormatMoney(order.Gross),
            [.. order.Lines.Select(static line => new PurchaseOrderLineDto(
                Id(line.Id),
                line.LineNo,
                line.ItemId,
                WireNumbers.FormatMoney(line.Quantity),
                WireNumbers.FormatMoney(line.UnitPrice)))]);
    }

    /// <summary>ينقل عميلاً إلى السلك.</summary>
    /// <param name="party">العميل.</param>
    public static PartyDto ToDto(SalesParty party)
    {
        ArgumentNullException.ThrowIfNull(party);

        return new PartyDto(
            Id(party.Id),
            party.Code,
            Text(party.Name),
            WireNumbers.FormatMoney(party.CreditLimit),
            party.PaymentTermsDays,

            // ‏null لا فراغ: العميل لا يحمل هذا الحقل أصلاً، والفراغ كان سيعني
            // «مورد بلا رقم» — وهي حالة أخرى.
            null);
    }

    /// <summary>ينقل مورداً إلى السلك.</summary>
    /// <param name="party">المورد.</param>
    public static PartyDto ToDto(PurchasingParty party)
    {
        ArgumentNullException.ThrowIfNull(party);

        return new PartyDto(
            Id(party.Id),
            party.Code,
            Text(party.Name),
            WireNumbers.FormatMoney(party.CreditLimit),
            party.PaymentTermsDays,
            party.VatNumber);
    }

    /// <summary>ينقل مستند مبيعات إلى السلك.</summary>
    /// <param name="document">المستند.</param>
    public static CommercialDocumentDto ToDto(SalesDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new CommercialDocumentDto(
            Id(document.Id),
            document.Number,
            document.State,
            WireNumbers.FormatMoney(document.Net),
            WireNumbers.FormatMoney(document.Tax),
            WireNumbers.FormatMoney(document.Gross),
            document.EntryId is { } entry ? Id(entry) : null,
            document.AlreadyPosted);
    }

    /// <summary>ينقل مستند مشتريات إلى السلك.</summary>
    /// <param name="document">المستند.</param>
    public static CommercialDocumentDto ToDto(PurchasingDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);

        return new CommercialDocumentDto(
            Id(document.Id),
            document.Number,
            document.State,
            WireNumbers.FormatMoney(document.Net),
            WireNumbers.FormatMoney(document.Tax),
            WireNumbers.FormatMoney(document.Gross),
            document.EntryId is { } entry ? Id(entry) : null,
            document.AlreadyPosted);
    }

    /// <summary>ينقل أعمار الذمم المدينة إلى السلك.</summary>
    /// <param name="report">التقرير.</param>
    public static AgingReportDto ToDto(SalesAging report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new AgingReportDto(
            Date(report.AsOf),
            [.. report.Parties.Select(static party => new AgingPartyDto(
                Id(party.PartyId), party.Code, Text(party.Name), Bands(party.Bands)))],
            Bands(report.Totals));
    }

    /// <summary>ينقل أعمار الذمم الدائنة إلى السلك.</summary>
    /// <param name="report">التقرير.</param>
    public static AgingReportDto ToDto(PurchasingAging report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new AgingReportDto(
            Date(report.AsOf),
            [.. report.Parties.Select(static party => new AgingPartyDto(
                Id(party.PartyId), party.Code, Text(party.Name), Bands(party.Bands)))],
            Bands(report.Totals));
    }

    /// <summary>يقرأ طلب فاتورة مورد مخزنية من السلك.</summary>
    /// <param name="dto">الحمولة.</param>
    public static PurchasingStockBillRequest ToStockBillRequest(StockBillRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        Guard(dto.Lines, out IReadOnlyList<StockBillLineDto> present);
        List<PurchasingStockBillLineRequest> mapped = [];

        for (int index = 0; index < present.Count; index++)
        {
            StockBillLineDto line = present[index];
            string at = Field(index);

            mapped.Add(new PurchasingStockBillLineRequest(
                WireMapping.ReadGuid(line.ReceiptLineId, at + ".receiptLineId"),
                WireNumbers.ParseStrict(line.Quantity.Raw, WireNumbers.QuantityScale, at + ".quantity"),
                WireNumbers.ParseStrict(line.UnitPrice.Raw, WireNumbers.MoneyScale, at + ".unitPrice"),
                WireMapping.ReadRequiredText(line.TaxClassification, at + ".taxClassification", ClassificationLength),
                WireNumbers.ParseStrict(line.TaxRate.Raw, WireNumbers.RateScale, at + ".taxRate")));
        }

        return new PurchasingStockBillRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.ReceiptId, "receiptId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            mapped);
    }

    /// <summary>يقرأ طلب مرتجع مشتريات من السلك.</summary>
    /// <param name="dto">الحمولة.</param>
    public static PurchasingReturnRequest ToPurchaseReturnRequest(PurchaseReturnRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        return new PurchasingReturnRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadGuid(dto.BillId, "billId"),
            WireMapping.ReadGuid(dto.ReceiptLineId, "receiptLineId"),
            WireMapping.ReadDate(dto.IssuedOn, "issuedOn"),
            WireNumbers.ParseStrict(dto.Quantity.Raw, WireNumbers.QuantityScale, "quantity"),
            WireNumbers.ParseStrict(dto.Tax.Raw, WireNumbers.MoneyScale, "tax"));
    }

    /// <summary>يقرأ طلب تسجيل صنف من السلك.</summary>
    /// <param name="dto">الحمولة.</param>
    public static InventoryItemRequest ToItemRequest(ItemRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);

        if (dto.Units is null)
        {
            throw WireNumbers.Reject("wire.text.missing", "units", "الوحدات مفقودة.", "The units are missing.");
        }

        if (dto.Units.Count > MaxLines)
        {
            throw WireNumbers.Reject(
                "wire.list.too_long",
                "units",
                FormattableString.Invariant($"عدد الوحدات يتجاوز الحدّ المعلن {MaxLines}."),
                FormattableString.Invariant($"The number of units exceeds the published limit of {MaxLines}."));
        }

        List<InventoryUnitFactor> units = [];

        for (int index = 0; index < dto.Units.Count; index++)
        {
            UnitFactorDto unit = dto.Units[index];
            string at = string.Create(CultureInfo.InvariantCulture, $"units[{index}]");

            units.Add(new InventoryUnitFactor(
                WireMapping.ReadRequiredText(unit.UnitCode, at + ".unitCode", UnitLength),
                ReadFactor(unit.Numerator, at + ".numerator"),
                ReadFactor(unit.Denominator, at + ".denominator")));
        }

        return new InventoryItemRequest(
            WireMapping.ReadRequiredText(dto.Code, "code", CodeLength),
            WireMapping.ReadLocalized(dto.Name, "name"),
            WireMapping.ReadRequiredText(dto.ItemGroup, "itemGroup", CodeLength),
            WireMapping.ReadRequiredText(dto.BaseUnit, "baseUnit", UnitLength),
            units);
    }

    /// <summary>يقرأ طلب مستند حركة مخزون من السلك.</summary>
    /// <param name="dto">الحمولة.</param>
    public static InventoryStockMovementRequest ToStockMovementRequest(StockMovementRequestDto dto)
    {
        ArgumentNullException.ThrowIfNull(dto);
        ArgumentNullException.ThrowIfNull(dto.Quantity);

        return new InventoryStockMovementRequest(
            WireMapping.ReadRequiredText(dto.Number, "number", CodeLength),
            WireMapping.ReadRequiredText(dto.Direction, "direction", ClassificationLength),
            WireMapping.ReadRequiredText(dto.ItemId, "itemId", CodeLength),
            WireMapping.ReadRequiredText(dto.WarehouseId, "warehouseId", CodeLength),
            WireMapping.ReadRequiredText(dto.LocationId, "locationId", CodeLength),
            WireMapping.ReadRequiredText(dto.ItemGroup, "itemGroup", CodeLength),
            new InventoryMeasure(
                WireNumbers.ParseStrict(dto.Quantity.Magnitude.Raw, WireNumbers.QuantityScale, "quantity.magnitude"),
                WireMapping.ReadRequiredText(dto.Quantity.Unit, "quantity.unit", UnitLength)),
            WireNumbers.ParseStrict(dto.Cost.Raw, WireNumbers.MoneyScale, "cost"),
            WireMapping.ReadDate(dto.OccurredOn, "occurredOn"));
    }

    /// <summary>ينقل سطور مستند مشتريات إلى السلك.</summary>
    /// <param name="lines">السطور.</param>
    public static PurchaseDocumentLineListDto ToDto(IReadOnlyList<PurchasingDocumentLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);

        return new PurchaseDocumentLineListDto(
            lines.Count,
            [
                .. lines.Select(static line => new PurchaseDocumentLineDto(
                    Id(line.Id),
                    line.LineNo,
                    line.ItemId,
                    WireNumbers.FormatQuantity(line.Quantity),
                    line.Unit,
                    WireNumbers.FormatMoney(line.UnitPrice))),
            ]);
    }

    /// <summary>ينقل صنفاً إلى السلك.</summary>
    /// <param name="item">الصنف.</param>
    public static ItemDto ToDto(InventoryItem item)
    {
        ArgumentNullException.ThrowIfNull(item);

        return new ItemDto(
            Id(item.Id),
            item.Code,
            Text(item.Name),
            item.ItemGroup,
            item.BaseUnit,
            [.. item.Units.Select(static unit => new UnitFactorDto(unit.UnitCode, unit.Numerator, unit.Denominator))]);
    }

    /// <summary>ينقل مستند حركة مخزون إلى السلك.</summary>
    /// <param name="movement">المستند.</param>
    public static StockMovementDto ToDto(InventoryStockMovement movement)
    {
        ArgumentNullException.ThrowIfNull(movement);

        return new StockMovementDto(
            Id(movement.Id),
            movement.Number,
            movement.State,
            movement.Direction,
            movement.ItemId,
            movement.WarehouseId,
            movement.LocationId,
            movement.ItemGroup,
            Measure(movement.Quantity),
            WireNumbers.FormatMoney(movement.Cost),
            Date(movement.OccurredOn),
            movement.EntryId is { } entry ? Id(entry) : null,
            movement.AlreadyPosted);
    }

    /// <summary>ينقل رصيد مخزون إلى السلك.</summary>
    /// <param name="balance">الرصيد.</param>
    public static StockBalanceDto ToDto(InventoryBalance balance)
    {
        ArgumentNullException.ThrowIfNull(balance);

        return new StockBalanceDto(
            balance.ItemId,
            balance.WarehouseId,
            balance.LocationId,
            Measure(balance.Quantity),
            WireNumbers.FormatMoney(balance.Value),
            WireNumbers.FormatQuantity(balance.UnitCost),
            balance.HasCostBasis);
    }

    /// <summary>ينقل تقييم المخزون إلى السلك.</summary>
    /// <param name="report">التقرير.</param>
    public static InventoryValuationDto ToDto(InventoryValuationReport report)
    {
        ArgumentNullException.ThrowIfNull(report);

        return new InventoryValuationDto(
            Date(report.AsOf),
            WireNumbers.FormatMoney(report.SubledgerTotal),
            WireNumbers.FormatMoney(report.ControlTotal),
            WireNumbers.FormatMoney(report.BalanceTotal),
            WireNumbers.FormatMoney(report.Divergence),
            report.IsReconciled,
            [
                .. report.Divergences.Select(static divergence => new InventoryDivergenceDto(
                    divergence.DocumentType,
                    divergence.DocumentId,
                    divergence.ItemId,
                    WireNumbers.FormatMoney(divergence.SubledgerEffect),
                    WireNumbers.FormatMoney(divergence.ControlEffect),
                    WireNumbers.FormatMoney(divergence.Divergence),
                    divergence.ReasonCode)),
            ]);
    }

    private static MeasureDto Measure(InventoryMeasure measure) =>
        new(WireNumbers.FormatQuantity(measure.Magnitude), measure.Unit);

    /// <summary>
    /// يقرأ حدّ معامل تحويل: عددٌ صحيح موجب.
    /// <para>
    /// <b>والمقام الصفري ليس معاملاً</b>، والبسط غير الموجب يقلب اتجاه الكمّية بصمت.
    /// والرفض هنا شكليٌّ قبل أن يبلغ الوحدة، فيُسمّي الحقل بموضعه في القائمة.
    /// </para>
    /// </summary>
    private static long ReadFactor(long value, string field)
    {
        if (value is <= 0L or > MaxUnitFactor)
        {
            throw WireNumbers.Reject(
                "wire.number.out_of_range",
                field,
                FormattableString.Invariant($"حدّ معامل التحويل بين واحد و{MaxUnitFactor}."),
                FormattableString.Invariant($"A conversion factor term is between one and {MaxUnitFactor}."));
        }

        return value;
    }

    private static AgingBandsDto Bands(SalesAgingBands bands) => new(
        WireNumbers.FormatMoney(bands.NotDue),
        WireNumbers.FormatMoney(bands.Days1To30),
        WireNumbers.FormatMoney(bands.Days31To60),
        WireNumbers.FormatMoney(bands.Days61To90),
        WireNumbers.FormatMoney(bands.Over90),
        WireNumbers.FormatMoney(bands.Total));

    private static AgingBandsDto Bands(PurchasingAgingBands bands) => new(
        WireNumbers.FormatMoney(bands.NotDue),
        WireNumbers.FormatMoney(bands.Days1To30),
        WireNumbers.FormatMoney(bands.Days31To60),
        WireNumbers.FormatMoney(bands.Days61To90),
        WireNumbers.FormatMoney(bands.Over90),
        WireNumbers.FormatMoney(bands.Total));

    private static List<SalesReceiptAllocationRequest> ReceiptAllocations(
        IReadOnlyList<ReceiptAllocationDto>? allocations)
    {
        Guard(allocations, "allocations", out IReadOnlyList<ReceiptAllocationDto> present);

        List<SalesReceiptAllocationRequest> mapped = [];

        for (int index = 0; index < present.Count; index++)
        {
            ReceiptAllocationDto allocation = present[index];
            string at = Field("allocations", index);

            mapped.Add(new SalesReceiptAllocationRequest(
                WireMapping.ReadGuid(allocation.InvoiceId, at + ".invoiceId"),
                WireNumbers.ParseStrict(allocation.Amount.Raw, WireNumbers.MoneyScale, at + ".amount")));
        }

        return mapped;
    }

    private static List<PurchasingPaymentAllocationRequest> PaymentAllocations(
        IReadOnlyList<PaymentAllocationDto>? allocations)
    {
        Guard(allocations, "allocations", out IReadOnlyList<PaymentAllocationDto> present);

        List<PurchasingPaymentAllocationRequest> mapped = [];

        for (int index = 0; index < present.Count; index++)
        {
            PaymentAllocationDto allocation = present[index];
            string at = Field("allocations", index);

            mapped.Add(new PurchasingPaymentAllocationRequest(
                WireMapping.ReadGuid(allocation.BillId, at + ".billId"),
                WireNumbers.ParseStrict(allocation.Amount.Raw, WireNumbers.MoneyScale, at + ".amount")));
        }

        return mapped;
    }

    private static List<PurchasingGoodsReceiptLineRequest> ReceiptLines(IReadOnlyList<GoodsReceiptLineDto>? lines)
    {
        Guard(lines, "lines", out IReadOnlyList<GoodsReceiptLineDto> present);

        List<PurchasingGoodsReceiptLineRequest> mapped = [];

        for (int index = 0; index < present.Count; index++)
        {
            GoodsReceiptLineDto line = present[index];
            string at = Field("lines", index);

            mapped.Add(new PurchasingGoodsReceiptLineRequest(
                WireMapping.ReadGuid(line.OrderLineId, at + ".orderLineId"),
                WireNumbers.ParseStrict(line.Quantity.Raw, WireNumbers.MoneyScale, at + ".quantity")));
        }

        return mapped;
    }

    private static List<SalesLineRequest> SalesLines(IReadOnlyList<SalesLineDto>? lines)
    {
        Guard(lines, out IReadOnlyList<SalesLineDto> present);

        List<SalesLineRequest> mapped = [];

        for (int index = 0; index < present.Count; index++)
        {
            SalesLineDto line = present[index];
            string at = Field(index);

            mapped.Add(new SalesLineRequest(
                WireMapping.ReadRequiredText(line.ItemGroup, at + ".itemGroup", CodeLength),
                WireMapping.ReadLocalized(line.Description, at + ".description"),
                WireNumbers.ParseStrict(line.Quantity.Raw, WireNumbers.MoneyScale, at + ".quantity"),
                WireNumbers.ParseStrict(line.UnitPrice.Raw, WireNumbers.MoneyScale, at + ".unitPrice"),
                WireNumbers.ParseStrict(line.Discount.Raw, WireNumbers.MoneyScale, at + ".discount"),
                WireMapping.ReadRequiredText(line.TaxClassification, at + ".taxClassification", ClassificationLength),
                WireNumbers.ParseStrict(line.TaxRate.Raw, WireNumbers.RateScale, at + ".taxRate"),
                line.OriginalInvoiceLineId is null
                    ? null
                    : WireMapping.ReadGuid(line.OriginalInvoiceLineId, at + ".originalInvoiceLineId")));
        }

        return mapped;
    }

    private static List<PurchasingLineRequest> PurchaseLines(IReadOnlyList<PurchaseLineDto>? lines)
    {
        Guard(lines, out IReadOnlyList<PurchaseLineDto> present);

        List<PurchasingLineRequest> mapped = [];

        for (int index = 0; index < present.Count; index++)
        {
            PurchaseLineDto line = present[index];
            string at = Field(index);

            mapped.Add(new PurchasingLineRequest(
                WireMapping.ReadRequiredText(line.ItemId, at + ".itemId", CodeLength),
                WireMapping.ReadRequiredText(line.ItemGroup, at + ".itemGroup", CodeLength),
                WireMapping.ReadLocalized(line.Description, at + ".description"),
                WireNumbers.ParseStrict(line.Quantity.Raw, WireNumbers.MoneyScale, at + ".quantity"),
                WireNumbers.ParseStrict(line.UnitPrice.Raw, WireNumbers.MoneyScale, at + ".unitPrice"),
                WireMapping.ReadRequiredText(line.TaxClassification, at + ".taxClassification", ClassificationLength),
                WireNumbers.ParseStrict(line.TaxRate.Raw, WireNumbers.RateScale, at + ".taxRate"),
                line.TaxRecoverable));
        }

        return mapped;
    }

    /// <summary>
    /// يفحص وجود السطور وعددها — <b>ولا يفحص «صفر سطور»</b>: ذلك حكم الوحدة
    /// (<c>sales.no_lines</c> · <c>purchasing.no_lines</c>) برسالته التي تسمّي المستند،
    /// ونسخُه هنا كان سيجعل الرفض نفسه يخرج برمزين مختلفين حسب الباب.
    /// </summary>
    private static void Guard<T>(IReadOnlyList<T>? lines, out IReadOnlyList<T> present)
        => Guard(lines, "lines", out present);

    /// <summary>
    /// نفس الفحص لأي مجموعة على مستند — <b>وباسمها في الرفض</b>: «السطور مفقودة» على
    /// حقلٍ اسمه <c>allocations</c> رسالةٌ تدلّ على الحقل الخطأ.
    /// </summary>
    private static void Guard<T>(IReadOnlyList<T>? items, string field, out IReadOnlyList<T> present)
    {
        if (items is null)
        {
            throw WireNumbers.Reject(
                "wire.text.missing",
                field,
                "الحقل «" + field + "» مفقود.",
                "The field '" + field + "' is missing.");
        }

        if (items.Count > MaxLines)
        {
            throw WireNumbers.Reject(
                "wire.list.too_long",
                field,
                FormattableString.Invariant($"عدد عناصر «{field}» يتجاوز الحدّ المعلن {MaxLines}."),
                FormattableString.Invariant($"The number of items in '{field}' exceeds the published limit of {MaxLines}."));
        }

        present = items;
    }

    private static int ReadTerms(int days)
    {
        if (days is < 0 or > MaxPaymentTermsDays)
        {
            throw WireNumbers.Reject(
                "wire.number.out_of_range",
                "paymentTermsDays",
                FormattableString.Invariant($"مهلة السداد بالأيام بين صفر و{MaxPaymentTermsDays}."),
                FormattableString.Invariant($"The payment terms in days are between zero and {MaxPaymentTermsDays}."));
        }

        return days;
    }

    private static string Field(int index) => Field("lines", index);

    private static string Field(string collection, int index) =>
        string.Create(CultureInfo.InvariantCulture, $"{collection}[{index}]");

    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static LocalizedTextDto Text(LocalizedName name) => new(name.Arabic, name.English);
}
