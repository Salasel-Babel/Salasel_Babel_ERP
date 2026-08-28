using Babel.SharedKernel;

namespace Babel.Purchasing.Application;

/// <summary>مسوّدة مورد. <c>name_ar</c> و<c>name_en</c> إلزاميان على كل بيانات أساسية.</summary>
/// <param name="Code">رمز المورد.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="CreditLimit">سقف الالتزام معه.</param>
/// <param name="PaymentTermsDays">مهلة السداد بالأيام.</param>
/// <param name="VatNumber">
/// رقم التسجيل الضريبي، أو فراغ إن لم يُسجَّل. <b>اختياري لأن غيابه واقع لا نقص</b>:
/// المورد دون حدّ التسجيل، والمورد غير المقيم، والمورد المُنشأ قبل هذا الحقل — ثلاثتهم
/// بلا رقم. وحين يُرسل يُتحقّق من شكله كاملاً ولا يُقبل «تقريباً صحيح».
/// </param>
public sealed record SupplierDraft(
    string Code,
    LocalizedName Name,
    Money CreditLimit,
    int PaymentTermsDays,
    string VatNumber = "");

/// <summary>المورد كما يراه المستدعي.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم.</param>
/// <param name="CreditLimit">السقف.</param>
/// <param name="PaymentTermsDays">مهلة السداد.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي، أو فراغ إن لم يُسجَّل.</param>
public sealed record SupplierView(
    Guid Id,
    string Code,
    LocalizedName Name,
    Money CreditLimit,
    int PaymentTermsDays,
    string VatNumber = "");

/// <summary>سطر مستند مشتريات.</summary>
/// <param name="ItemId">الصنف في دفتره المساعد.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="Description">البيان ثنائي اللغة.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="Unit">
/// وحدة قياس الكمية — <b>تعبر إلى المخزون مع الكمية عند الاستلام</b>. و«عشرة» بلا وحدة
/// ليست معلومة: عشر حبّات أم عشر كراتين؟ والفرق يصل إلى المال لأن الكمية تُضرب في
/// تكلفة الوحدة. ومَن لا يملك وحدةً بعد يُسلّم <c>InventoryUnits.Each</c> صراحةً.
/// </param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="TaxClassification">التصنيف الضريبي.</param>
/// <param name="TaxRate">نسبة الضريبة كسراً عشرياً.</param>
/// <param name="TaxRecoverable">هل الضريبة قابلة للاسترداد على هذا السطر؟</param>
public sealed record PurchaseLineDraft(
    string ItemId,
    string ItemGroup,
    LocalizedName Description,
    decimal Quantity,
    string Unit,
    Money UnitPrice,
    string TaxClassification,
    decimal TaxRate,
    bool TaxRecoverable = true);

/// <summary>مسوّدة طلب شراء داخلي.</summary>
/// <param name="Number">رقم الطلب.</param>
/// <param name="RequestedOn">تاريخه.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="Lines">السطور.</param>
public sealed record PurchaseRequestDraft(
    string Number,
    DateOnly RequestedOn,
    string CostCenterId,
    IReadOnlyList<PurchaseLineDraft> Lines);

/// <summary>مسوّدة أمر شراء.</summary>
/// <param name="Number">رقم الأمر.</param>
/// <param name="SupplierId">المورد.</param>
/// <param name="OrderedOn">تاريخه.</param>
/// <param name="WarehouseId">المستودع المستقبِل.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="Lines">السطور.</param>
public sealed record PurchaseOrderDraft(
    string Number,
    Guid SupplierId,
    DateOnly OrderedOn,
    string WarehouseId,
    string CostCenterId,
    IReadOnlyList<PurchaseLineDraft> Lines);

/// <summary>سطر استلام: أي سطر أمر، وبأي كمية.</summary>
/// <param name="OrderLineId">سطر الأمر.</param>
/// <param name="Quantity">الكمية المستلمة.</param>
public sealed record GoodsReceiptLineDraft(Guid OrderLineId, decimal Quantity);

/// <summary>مسوّدة استلام بضاعة.</summary>
/// <param name="Number">رقم الاستلام.</param>
/// <param name="OrderId">الأمر.</param>
/// <param name="ReceivedOn">تاريخ الاستلام.</param>
/// <param name="Lines">السطور.</param>
public sealed record GoodsReceiptDraft(
    string Number,
    Guid OrderId,
    DateOnly ReceivedOn,
    IReadOnlyList<GoodsReceiptLineDraft> Lines);

/// <summary>سطر فاتورة مورد مخزنية: يرجع إلى سطر استلام بعينه — وهذا هو ضلع المطابقة الثالث.</summary>
/// <param name="ReceiptLineId">سطر الاستلام.</param>
/// <param name="Quantity">الكمية المفوترة.</param>
/// <param name="UnitPrice">سعر الوحدة على الفاتورة.</param>
/// <param name="TaxClassification">التصنيف الضريبي.</param>
/// <param name="TaxRate">نسبة الضريبة.</param>
public sealed record SupplierBillLineDraft(
    Guid ReceiptLineId,
    decimal Quantity,
    Money UnitPrice,
    string TaxClassification,
    decimal TaxRate);

/// <summary>مسوّدة فاتورة مورد مخزنية تُطابَق ثلاثياً.</summary>
/// <param name="Number">رقم الفاتورة.</param>
/// <param name="ReceiptId">الاستلام.</param>
/// <param name="IssuedOn">تاريخ الفاتورة.</param>
/// <param name="Lines">السطور.</param>
public sealed record StockBillDraft(
    string Number,
    Guid ReceiptId,
    DateOnly IssuedOn,
    IReadOnlyList<SupplierBillLineDraft> Lines);

/// <summary>مسوّدة فاتورة مصروف مباشر بلا مخزون.</summary>
/// <param name="Number">رقم الفاتورة.</param>
/// <param name="SupplierId">المورد.</param>
/// <param name="IssuedOn">تاريخ الفاتورة.</param>
/// <param name="ExpenseCategory">تصنيف المصروف — مؤهّل الدور.</param>
/// <param name="CostCenterId">مركز التكلفة — بُعد إلزامي على المصروف.</param>
/// <param name="Lines">السطور.</param>
public sealed record ExpenseBillDraft(
    string Number,
    Guid SupplierId,
    DateOnly IssuedOn,
    string ExpenseCategory,
    string CostCenterId,
    IReadOnlyList<PurchaseLineDraft> Lines);

/// <summary>
/// مسوّدة إشعار مدين — <b>مرتجع مشتريات</b>.
/// <para>
/// <b>ولاحظ ما ليس فيه: صافي المرتجع.</b> المصفوفة تقول على
/// <c>purchasing.debit_note.posted</c> إن الصافي «بتكلفة الاستلام الأصلي لا بتكلفة
/// اليوم»، وتلك التكلفة يملكها دفتر المخزون وحده. فالمسوّدة تحمل <b>الكمّية</b>،
/// ويُحسب المبلغ لحظة الترحيل — وهو مبدأ ADR-0039 نفسه مطبَّقاً على الطرف الآخر
/// من الدورة.
/// </para>
/// </summary>
/// <param name="Number">رقم الإشعار.</param>
/// <param name="BillId">الفاتورة الأصلية.</param>
/// <param name="IssuedOn">تاريخه.</param>
/// <param name="ReceiptLineId">سطر الاستلام الذي تُردّ بضاعته — به يُقيَّم المرتجع.</param>
/// <param name="Quantity">الكمية المرتجعة بوحدة الاستلام — موجبة، ولا تتجاوز ما استُلم.</param>
/// <param name="Tax">
/// ضريبة المرتجع. <b>تُسلَّم ولا تُحسب</b>: هي بتصنيف الفاتورة الأصلية وواقعةٌ تجارية
/// لا يملكها المخزون، بخلاف الصافي.
/// </param>
public sealed record DebitNoteDraft(
    string Number,
    Guid BillId,
    DateOnly IssuedOn,
    Guid ReceiptLineId,
    decimal Quantity,
    Money Tax);

/// <summary>تخصيص مبلغ على فاتورة مورد.</summary>
/// <param name="BillId">الفاتورة.</param>
/// <param name="Amount">المبلغ.</param>
public sealed record PayableAllocationDraft(Guid BillId, Money Amount);

/// <summary>مسوّدة سند صرف.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="SupplierId">المورد.</param>
/// <param name="PaidOn">تاريخ الصرف.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">معرّف الخزينة أو الحساب البنكي.</param>
/// <param name="Paid">المبلغ المدفوع.</param>
/// <param name="BankFee">رسوم التحويل.</param>
/// <param name="Allocations">التخصيصات.</param>
public sealed record SupplierPaymentDraft(
    string Number,
    Guid SupplierId,
    DateOnly PaidOn,
    string SettlementMethod,
    string TreasuryPartyId,
    Money Paid,
    Money BankFee,
    IReadOnlyList<PayableAllocationDraft> Allocations);

/// <summary>مسوّدة تكلفة استيراد.</summary>
/// <param name="Number">رقم المستند.</param>
/// <param name="SupplierId">مورد الخدمة.</param>
/// <param name="ReceiptId">الاستلام المُحمَّل عليه.</param>
/// <param name="IncurredOn">تاريخ التحمّل.</param>
/// <param name="ItemId">الصنف المُحمَّل.</param>
/// <param name="ItemGroup">مجموعة الصنف.</param>
/// <param name="Source">‏<c>supplier_invoice</c> أو <c>direct_payment</c>.</param>
/// <param name="SettlementMethod">طريقة التسوية عند الدفع المباشر.</param>
/// <param name="TreasuryPartyId">معرّف الخزينة عند الدفع المباشر.</param>
/// <param name="Cost">المبلغ.</param>
public sealed record LandedCostDraft(
    string Number,
    Guid SupplierId,
    Guid ReceiptId,
    DateOnly IncurredOn,
    string ItemId,
    string ItemGroup,
    string Source,
    string SettlementMethod,
    string TreasuryPartyId,
    Money Cost);

/// <summary>مجاميع مستند.</summary>
/// <param name="Net">الصافي.</param>
/// <param name="Tax">الضريبة.</param>
/// <param name="Gross">الإجمالي.</param>
public sealed record DocumentTotals(Money Net, Money Tax, Money Gross);

/// <summary>مستند مشتريات كما يراه المستدعي.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Totals">المجاميع.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
/// <param name="AlreadyPosted">
/// هل كانت هوية الترحيل مُرحَّلة <b>قبل</b> هذا النداء؟ ولا تُشتقّ من
/// <paramref name="State"/>: المستند بعد أي نداء ترحيل ناجح حالته <c>POSTED</c> —
/// الأول والثاني سواء. والفارق معلومةٌ يملكها الدفتر
/// (<c>PostingReceipt.WasAlreadyPosted</c>) وكانت تُهدَر عند هذا الحدّ.
/// والقيمة الافتراضية <c>false</c> تجعل الإضافة إضافةً محضة.
/// </param>
public sealed record PurchasingDocumentView(
    Guid Id,
    string Number,
    string State,
    DocumentTotals Totals,
    Guid? EntryId,
    bool AlreadyPosted = false);

/// <summary>سطر مستند كما يراه المستدعي — معرّف السطر لازم للمطابقة الثلاثية.</summary>
/// <param name="Id">معرّف السطر.</param>
/// <param name="LineNo">رقمه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="Unit">وحدة قياس الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
public sealed record PurchaseLineView(
    Guid Id, int LineNo, string ItemId, decimal Quantity, string Unit, Money UnitPrice);

/// <summary>شرائح أعمار الذمم الدائنة.</summary>
/// <param name="NotDue">لم يستحق بعد.</param>
/// <param name="Days1To30">متأخر 1–30 يوماً.</param>
/// <param name="Days31To60">متأخر 31–60 يوماً.</param>
/// <param name="Days61To90">متأخر 61–90 يوماً.</param>
/// <param name="Over90">متأخر أكثر من 90 يوماً.</param>
/// <param name="Total">المجموع.</param>
public sealed record AgingBuckets(
    Money NotDue,
    Money Days1To30,
    Money Days31To60,
    Money Days61To90,
    Money Over90,
    Money Total);

/// <summary>أعمار ذمم طرف واحد.</summary>
/// <param name="PartyId">الطرف.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه.</param>
/// <param name="Buckets">شرائحه.</param>
public sealed record PartyAging(Guid PartyId, string Code, LocalizedName Name, AgingBuckets Buckets);

/// <summary>تقرير أعمار الذمم الدائنة.</summary>
/// <param name="AsOf">التاريخ.</param>
/// <param name="Parties">الأطراف.</param>
/// <param name="Totals">المجاميع.</param>
public sealed record AgingReport(DateOnly AsOf, IReadOnlyList<PartyAging> Parties, AgingBuckets Totals);

/// <summary>سطر كشف حساب مورد.</summary>
/// <param name="Date">التاريخ.</param>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="Number">رقمه.</param>
/// <param name="Description">البيان.</param>
/// <param name="Debit">مدين.</param>
/// <param name="Credit">دائن.</param>
/// <param name="RunningBalance">الرصيد المتحرّك — الدائن موجب في دفتر الذمم الدائنة.</param>
public sealed record StatementLine(
    DateOnly Date,
    string DocumentType,
    string Number,
    LocalizedName Description,
    Money Debit,
    Money Credit,
    Money RunningBalance);

/// <summary>كشف حساب مورد.</summary>
/// <param name="PartyId">المورد.</param>
/// <param name="From">من.</param>
/// <param name="To">إلى.</param>
/// <param name="Opening">الرصيد الافتتاحي.</param>
/// <param name="Lines">الحركات.</param>
/// <param name="Closing">الرصيد الختامي.</param>
public sealed record PartyStatement(
    Guid PartyId,
    DateOnly From,
    DateOnly To,
    Money Opening,
    IReadOnlyList<StatementLine> Lines,
    Money Closing);
