namespace Babel.Purchasing.Persistence;

/// <summary>حالة مستند مشتريات.</summary>
internal static class PurchasingDocumentState
{
    public const string Draft = "DRAFT";
    public const string Approved = "APPROVED";
    public const string Posted = "POSTED";
    public const string Reversed = "REVERSED";
    public const string Rejected = "REJECTED";
}

/// <summary>حالة محاولة ترحيل مستند.</summary>
internal static class PostingAttemptState
{
    public const string Attempting = "ATTEMPTING";
    public const string Posted = "POSTED";
    public const string Refused = "REFUSED";
}

/// <summary>نوع مالك السطر.</summary>
internal static class LineOwner
{
    public const string Request = "REQUEST";
    public const string Order = "ORDER";
    public const string Receipt = "RECEIPT";
    public const string Bill = "BILL";
    public const string DebitNote = "DEBIT_NOTE";
}

/// <summary>المورد. <c>internal</c> — لا يعبر حدّ وحدة المشتريات (Rule05).</summary>
internal sealed class SupplierRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>
    /// رقم التسجيل الضريبي للمورد — <b>معرّف مطابقة لا حقل عرض</b>.
    /// <para>
    /// الفراغ يعني «لم يُسجَّل»، <b>ولا قيمة معدومة في هذا العمود إطلاقاً</b>: العمود
    /// <c>not null default ''</c>. والسبب ليس ذوقاً — الفهرس الجزئي مشروط بـ
    /// <c>&lt;&gt; ''</c>، وقيمةٌ معدومة لا تساوي الفراغ ولا تُقارَن به، فاصطلاحان
    /// للخواء يعنيان صفوفاً تظنّ أنها في الفهرس وليست فيه.
    /// </para>
    /// </summary>
    public string VatNumber { get; set; } = string.Empty;

    /// <summary>سقف الالتزام مع المورد. <c>decimal</c> لا <c>double</c> (Rule04).</summary>
    public decimal CreditLimit { get; set; }

    public int PaymentTermsDays { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>طلب شراء داخلي.</summary>
internal sealed class PurchaseRequestRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public DateOnly RequestedOn { get; set; }

    public string CostCenterId { get; set; } = string.Empty;

    public string State { get; set; } = PurchasingDocumentState.Draft;

    public decimal EstimatedTotal { get; set; }
}

/// <summary>أمر شراء.</summary>
internal sealed class PurchaseOrderRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Guid? RequestId { get; set; }

    public DateOnly OrderedOn { get; set; }

    public string State { get; set; } = PurchasingDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    public string CostCenterId { get; set; } = string.Empty;

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrossTotal { get; set; }
}

/// <summary>استلام بضاعة — الشقّ الثاني من المطابقة الثلاثية.</summary>
internal sealed class GoodsReceiptRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Guid OrderId { get; set; }

    public DateOnly ReceivedOn { get; set; }

    public string State { get; set; } = PurchasingDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    /// <summary>قيمة المستلَم بتكلفة الاستلام — هي ما يُقيَّد بضاعةً مستلمة لم تُفوتر.</summary>
    public decimal ReceiptCost { get; set; }

    /// <summary>
    /// ما حُجز منها بفواتير الموردين — <b>بما فيها المسوّدات</b>: هذا حجز للمطابقة
    /// الثلاثية يمنع تفويتر الاستلام نفسه مرّتين، لا إعفاءً محاسبياً. البند المفتوح
    /// في الدفتر المساعد لا يُعفى إلا بالفواتير المُرحَّلة، وإلا انحرف عن حسابه الضابط.
    /// </summary>
    public decimal BilledValue { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>فاتورة مورد.</summary>
internal sealed class SupplierBillRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Guid? OrderId { get; set; }

    public Guid? ReceiptId { get; set; }

    public DateOnly IssuedOn { get; set; }

    public DateOnly DueOn { get; set; }

    public string State { get; set; } = PurchasingDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    public string CostCenterId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = "*";

    public string ExpenseCategory { get; set; } = "*";

    /// <summary>‏<c>STOCK</c> فاتورة مخزنية تُطابَق ثلاثياً · <c>EXPENSE</c> فاتورة مصروف مباشر.</summary>
    public string BillKind { get; set; } = "STOCK";

    public bool HasTaxableLine { get; set; }

    /// <summary>قيمة المستلَم المُستهلَكة من رصيد البضاعة المستلمة غير المفوترة.</summary>
    public decimal ReceiptValue { get; set; }

    /// <summary>فرق السعر بين الفاتورة والاستلام.</summary>
    public decimal PriceVariance { get; set; }

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    /// <summary>ضريبة قابلة للاسترداد.</summary>
    public decimal RecoverableTax { get; set; }

    /// <summary>ضريبة غير قابلة للاسترداد — تُحمَّل على المصروف لا على المطالبة بالاسترداد.</summary>
    public decimal NonRecoverableTax { get; set; }

    public decimal GrossTotal { get; set; }

    public decimal AllocatedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>إشعار مدين — مرتجع مشتريات.</summary>
internal sealed class DebitNoteRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Guid BillId { get; set; }

    public DateOnly IssuedOn { get; set; }

    public string State { get; set; } = PurchasingDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = "*";

    public string ItemId { get; set; } = string.Empty;

    public bool OriginalWasTaxable { get; set; }

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrossTotal { get; set; }

    public decimal AllocatedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>سند صرف لمورد.</summary>
internal sealed class SupplierPaymentRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public DateOnly PaidOn { get; set; }

    public string State { get; set; } = PurchasingDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string SettlementMethod { get; set; } = "bank";

    public string TreasuryPartyId { get; set; } = string.Empty;

    public decimal PaidAmount { get; set; }

    /// <summary>رسوم التحويل — مصروف بنكي لا يُنقص ذمة المورد.</summary>
    public decimal BankFee { get; set; }

    public decimal AllocatedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>تكلفة استيراد مُحمَّلة على المخزون.</summary>
internal sealed class LandedCostRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid SupplierId { get; set; }

    public Guid ReceiptId { get; set; }

    public DateOnly IncurredOn { get; set; }

    public string State { get; set; } = PurchasingDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string WarehouseId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = "*";

    public string ItemId { get; set; } = string.Empty;

    /// <summary>‏<c>supplier_invoice</c> أو <c>direct_payment</c> — يقرّر أي سطر يُدان.</summary>
    public string Source { get; set; } = "supplier_invoice";

    public string SettlementMethod { get; set; } = "bank";

    public string TreasuryPartyId { get; set; } = string.Empty;

    public decimal CostAmount { get; set; }

    public decimal AllocatedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>سطر مستند مشتريات — يخدم الطلب والأمر والاستلام والفاتورة والإشعار المدين.</summary>
internal sealed class PurchaseLineRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string OwnerType { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }

    public int LineNo { get; set; }

    /// <summary>سطر الأمر الذي يرجع إليه هذا السطر — عمود المطابقة الثلاثية الأول.</summary>
    public Guid? OrderLineId { get; set; }

    /// <summary>سطر الاستلام الذي يرجع إليه هذا السطر — عمود المطابقة الثاني.</summary>
    public Guid? ReceiptLineId { get; set; }

    public string ItemId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = "*";

    public string DescriptionAr { get; set; } = string.Empty;

    public string DescriptionEn { get; set; } = string.Empty;

    public decimal Quantity { get; set; }

    /// <summary>ما استُلم من هذا السطر (على سطور الأمر).</summary>
    public decimal ReceivedQuantity { get; set; }

    /// <summary>ما فُوتر من هذا السطر (على سطور الاستلام).</summary>
    public decimal BilledQuantity { get; set; }

    public decimal UnitPrice { get; set; }

    public string TaxClassification { get; set; } = "standard";

    public decimal TaxRate { get; set; }

    /// <summary>هل ضريبة هذا السطر قابلة للاسترداد؟ توزيع الاسترداد قرار سطر لا مستند.</summary>
    public bool TaxRecoverable { get; set; } = true;

    public decimal LineNet { get; set; }

    public decimal LineTax { get; set; }
}

/// <summary>تخصيص مبلغ من مستند مدين (سند صرف · إشعار مدين) على فاتورة مورد.</summary>
internal sealed class PayableAllocationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>‏<c>PAYMENT</c> · <c>DEBIT_NOTE</c>.</summary>
    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public Guid BillId { get; set; }

    public int LineNo { get; set; }

    public decimal AllocatedAmount { get; set; }

    public DateOnly AllocatedOn { get; set; }
}

/// <summary>
/// محاولة ترحيل مستند — سجلّ الوحدة عن نيّتها ومصيرها.
/// <para>
/// هوية الإحكام خماسية: (نوع المستند · معرّفه · رمز الإطلاق · الجيل · <b>رمز الحدث</b>).
/// </para>
/// </summary>
internal sealed class DocumentPostingRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string DocumentType { get; set; } = string.Empty;

    public string DocumentId { get; set; } = string.Empty;

    public string TriggerCode { get; set; } = string.Empty;

    public int Generation { get; set; } = 1;

    public string IdempotencyKey { get; set; } = string.Empty;

    /// <summary>
    /// رمز الحدث — <b>حقل في هوية الإحكام</b>، إلزامي وغير فارغ (قيد تحقّق في القاعدة).
    /// كان يُكتب ولا يُقرأ ضمن الهوية، وهذا بالضبط ما جعل الحدث الثاني للمستند الواحد
    /// يُبتلع بصمت (ADR-0017).
    /// </summary>
    public string EventCode { get; set; } = string.Empty;

    public string PartyId { get; set; } = string.Empty;

    public DateOnly DocumentDate { get; set; }

    public string State { get; set; } = PostingAttemptState.Attempting;

    /// <summary>الأثر المتوقَّع على نقطة الضبط بمنطق «دائن ناقص مدين» — الذمم الدائنة موجبة.</summary>
    public decimal ControlEffect { get; set; }

    public Guid? EntryId { get; set; }

    public long EntryNumber { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessageAr { get; set; } = string.Empty;

    public string FailureMessageEn { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTime LastAttemptAt { get; set; }
}
