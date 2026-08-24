namespace Babel.Sales.Persistence;

/// <summary>حالة مستند مبيعات.</summary>
internal static class SalesDocumentState
{
    public const string Draft = "DRAFT";
    public const string Approved = "APPROVED";
    public const string Posted = "POSTED";
    public const string Reversed = "REVERSED";
    public const string Cancelled = "CANCELLED";
}

/// <summary>حالة محاولة ترحيل مستند.</summary>
internal static class PostingAttemptState
{
    /// <summary>سُجّلت النية ولم يُعرف مصير النداء بعد — الحالة القابلة لإعادة المحاولة.</summary>
    public const string Attempting = "ATTEMPTING";

    /// <summary>رُحّل فعلاً وللمحرك إيصال.</summary>
    public const string Posted = "POSTED";

    /// <summary>رفضه المحرك، والسبب محفوظ. المستند باقٍ على حاله ويُعاد بلا ازدواج.</summary>
    public const string Refused = "REFUSED";
}

/// <summary>العميل. <c>internal</c> — لا يعبر حدّ وحدة المبيعات (Rule05).</summary>
internal sealed class CustomerRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string NameAr { get; set; } = string.Empty;

    public string NameEn { get; set; } = string.Empty;

    /// <summary>حد الائتمان. <c>decimal</c> لا <c>double</c> — مفروض ببناء في Rule04.</summary>
    public decimal CreditLimit { get; set; }

    /// <summary>مهلة السداد بالأيام. منها يُشتقّ تاريخ الاستحقاق وأعمار الديون.</summary>
    public int PaymentTermsDays { get; set; }

    public bool IsActive { get; set; }
}

/// <summary>عرض سعر.</summary>
internal sealed class QuotationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public DateOnly IssuedOn { get; set; }

    public DateOnly ValidUntil { get; set; }

    public string State { get; set; } = SalesDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrossTotal { get; set; }
}

/// <summary>أمر بيع.</summary>
internal sealed class SalesOrderRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid? QuotationId { get; set; }

    public DateOnly OrderedOn { get; set; }

    public string State { get; set; } = SalesDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string BranchId { get; set; } = string.Empty;

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrossTotal { get; set; }
}

/// <summary>فاتورة مبيعات.</summary>
internal sealed class SalesInvoiceRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid? OrderId { get; set; }

    public DateOnly IssuedOn { get; set; }

    public DateOnly DueOn { get; set; }

    public string State { get; set; } = SalesDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string BranchId { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = "*";

    public bool HasTaxableLine { get; set; }

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrossTotal { get; set; }

    /// <summary>ما استُنفد من دفعات مقدمة مقابل هذه الفاتورة.</summary>
    public decimal AdvanceApplied { get; set; }

    /// <summary>ما خُصّص عليها من سندات قبض وإشعارات دائنة.</summary>
    public decimal AllocatedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>سطر مستند مبيعات — يخدم عرض السعر وأمر البيع والفاتورة والإشعار الدائن.</summary>
internal sealed class SalesLineRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>نوع المستند المالك: <c>QUOTATION</c> · <c>ORDER</c> · <c>INVOICE</c> · <c>CREDIT_NOTE</c>.</summary>
    public string OwnerType { get; set; } = string.Empty;

    public Guid OwnerId { get; set; }

    public int LineNo { get; set; }

    public string DescriptionAr { get; set; } = string.Empty;

    public string DescriptionEn { get; set; } = string.Empty;

    public string ItemGroup { get; set; } = "*";

    public decimal Quantity { get; set; }

    public decimal UnitPrice { get; set; }

    public decimal DiscountAmount { get; set; }

    /// <summary>التصنيف الضريبي للسطر: <c>standard</c> · <c>zero</c> · <c>exempt</c>.</summary>
    public string TaxClassification { get; set; } = "standard";

    /// <summary>نسبة الضريبة كسراً عشرياً (0.15 لا 15).</summary>
    public decimal TaxRate { get; set; }

    /// <summary>صافي السطر بعد الخصم، <b>مقرَّباً على مستوى السطر</b>.</summary>
    public decimal LineNet { get; set; }

    /// <summary>ضريبة السطر، محسوبة ومقرَّبة على السطر لا على المستند.</summary>
    public decimal LineTax { get; set; }
}

/// <summary>إشعار دائن.</summary>
internal sealed class CreditNoteRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public Guid InvoiceId { get; set; }

    public DateOnly IssuedOn { get; set; }

    public string State { get; set; } = SalesDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string BranchId { get; set; } = string.Empty;

    public bool OriginalWasTaxable { get; set; }

    public decimal NetTotal { get; set; }

    public decimal TaxTotal { get; set; }

    public decimal GrossTotal { get; set; }

    /// <summary>ما خُصّص من الإشعار على فواتير.</summary>
    public decimal AllocatedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>سند قبض من عميل.</summary>
internal sealed class CustomerReceiptRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public DateOnly ReceivedOn { get; set; }

    public string State { get; set; } = SalesDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    /// <summary>طريقة التسوية: <c>cash</c> · <c>bank</c> · <c>card_clearing</c>. مؤهّل الدور، لا حساب.</summary>
    public string SettlementMethod { get; set; } = "bank";

    /// <summary>معرّف الخزينة أو الحساب البنكي في دفترها المساعد — معرّف مبهم، لا رقم حساب.</summary>
    public string TreasuryPartyId { get; set; } = string.Empty;

    public decimal ReceivedAmount { get; set; }

    public decimal DiscountAmount { get; set; }

    public decimal AllocatedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>دفعة مقدمة من عميل.</summary>
internal sealed class CustomerAdvanceRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public string Number { get; set; } = string.Empty;

    public Guid CustomerId { get; set; }

    public DateOnly ReceivedOn { get; set; }

    public string State { get; set; } = SalesDocumentState.Draft;

    public string CurrencyCode { get; set; } = string.Empty;

    public string SettlementMethod { get; set; } = "bank";

    public string TreasuryPartyId { get; set; } = string.Empty;

    public bool TaxDueOnAdvance { get; set; }

    public decimal NetAmount { get; set; }

    public decimal TaxAmount { get; set; }

    public decimal AppliedAmount { get; set; }

    public Guid? PostedEntryId { get; set; }

    public int PostingGeneration { get; set; } = 1;
}

/// <summary>
/// تخصيص مبلغ من مستند دائن (سند قبض · إشعار دائن · دفعة مقدمة) على فاتورة.
/// <para>
/// صفّ التخصيص هو ما يجعل «سند واحد يُسدّد فاتورتين» قابلاً للتدقيق سطراً سطراً،
/// وهو أيضاً ما يمنع التخصيص الزائد: المجموع مفحوص عند الكتابة على الطرفين.
/// </para>
/// </summary>
internal sealed class ReceivableAllocationRow
{
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    /// <summary>نوع المصدر: <c>RECEIPT</c> · <c>CREDIT_NOTE</c> · <c>ADVANCE</c>.</summary>
    public string SourceType { get; set; } = string.Empty;

    public Guid SourceId { get; set; }

    public Guid InvoiceId { get; set; }

    public int LineNo { get; set; }

    public decimal AllocatedAmount { get; set; }

    public DateOnly AllocatedOn { get; set; }
}

/// <summary>
/// محاولة ترحيل مستند — سجلّ الوحدة عن نيّتها ومصيرها.
/// <para>
/// هوية الإحكام في المحرك هي الخماسية
/// (نوع المستند · معرّفه · رمز الإطلاق · الجيل · <b>رمز الحدث</b>)، وهذا الصف يحملها بالضبط مع
/// المفتاح الذي أُرسل والأثر المتوقَّع على الحساب الضابط. وجوده هو ما يجعل الرفض
/// حالةً <b>متّسقة قابلة لإعادة المحاولة</b> لا نصف كتابة.
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

    /// <summary>تاريخ المستند — به تُقطع المطابقة عند تاريخ بعينه.</summary>
    public DateOnly DocumentDate { get; set; }

    public string State { get; set; } = PostingAttemptState.Attempting;

    /// <summary>الأثر المتوقَّع على الحساب الضابط بمنطق «مدين ناقص دائن».</summary>
    public decimal ControlEffect { get; set; }

    public Guid? EntryId { get; set; }

    public long EntryNumber { get; set; }

    public string FailureCode { get; set; } = string.Empty;

    public string FailureMessageAr { get; set; } = string.Empty;

    public string FailureMessageEn { get; set; } = string.Empty;

    public int AttemptCount { get; set; }

    public DateTime LastAttemptAt { get; set; }
}
