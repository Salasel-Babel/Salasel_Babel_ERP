using Babel.SharedKernel;

namespace Babel.Purchasing.Surface;

/// <summary>طلب تسجيل مورد. ولا حقل مستأجر فيه: النطاق من الاعتماد ومن المسار.</summary>
/// <param name="Code">رمز المورد داخل المستأجر.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="CreditLimit">سقف الالتزام معه بعملة المنشأة.</param>
/// <param name="PaymentTermsDays">مهلة السداد بالأيام.</param>
/// <param name="VatNumber">
/// رقم التسجيل الضريبي، أو فراغ إن لم يُسجَّل — <b>وغيابه واقع لا نقص</b>: المورد دون حدّ
/// التسجيل، وغير المقيم، والمُنشأ قبل هذا الحقل. وحين يُرسل يُتحقّق من شكله كاملاً.
/// </param>
public sealed record PurchasingPartyRequest(
    string Code,
    LocalizedName Name,
    decimal CreditLimit,
    int PaymentTermsDays,
    string VatNumber = "");

/// <summary>مورد كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="CreditLimit">السقف.</param>
/// <param name="PaymentTermsDays">مهلة السداد.</param>
/// <param name="VatNumber">رقم التسجيل الضريبي، أو فراغ.</param>
public sealed record PurchasingParty(
    Guid Id,
    string Code,
    LocalizedName Name,
    decimal CreditLimit,
    int PaymentTermsDays,
    string VatNumber);

/// <summary>
/// سطر فاتورة مصروف. <b>ولا حساب فيه ولا رمز حساب</b>: يحمل مؤهّلات دور، والمصفوفة
/// وحدها تُحوّلها إلى حسابات.
/// </summary>
/// <param name="ItemId">الصنف أو البند في دفتره المساعد.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="Description">البيان ثنائي اللغة.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="TaxClassification">التصنيف الضريبي: <c>standard</c> · <c>zero</c> · <c>exempt</c>.</param>
/// <param name="TaxRate">نسبة الضريبة كسراً عشرياً.</param>
/// <param name="TaxRecoverable">هل ضريبة هذا السطر قابلة للاسترداد؟</param>
public sealed record PurchasingLineRequest(
    string ItemId,
    string ItemGroup,
    LocalizedName Description,
    decimal Quantity,
    decimal UnitPrice,
    string TaxClassification,
    decimal TaxRate,
    bool TaxRecoverable = true);

/// <summary>
/// طلب إنشاء فاتورة مصروف <b>مسوّدة</b> — فاتورة مورد مباشرة بلا مخزون ولا مطابقة ثلاثية.
/// </summary>
/// <param name="Number">رقم الفاتورة — فريد داخل المستأجر.</param>
/// <param name="SupplierId">المورد.</param>
/// <param name="IssuedOn">تاريخ الفاتورة الميلادي.</param>
/// <param name="ExpenseCategory">تصنيف المصروف — مؤهّل الدور.</param>
/// <param name="CostCenterId">مركز التكلفة — بُعد إلزامي على المصروف.</param>
/// <param name="Lines">السطور. فاتورة بلا سطر تُرفض.</param>
public sealed record PurchasingExpenseBillRequest(
    string Number,
    Guid SupplierId,
    DateOnly IssuedOn,
    string ExpenseCategory,
    string CostCenterId,
    IReadOnlyList<PurchasingLineRequest> Lines);

/// <summary>مستند مشتريات كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> · <c>POSTED</c> وما إليهما.</param>
/// <param name="Net">الصافي قبل الضريبة.</param>
/// <param name="Tax">الضريبة.</param>
/// <param name="Gross">الإجمالي شامل الضريبة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل، وإلا غياب.</param>
/// <param name="AlreadyPosted">هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟</param>
public sealed record PurchasingDocument(
    Guid Id,
    string Number,
    string State,
    decimal Net,
    decimal Tax,
    decimal Gross,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>شرائح أعمار الذمم الدائنة.</summary>
/// <param name="NotDue">لم يستحق بعد.</param>
/// <param name="Days1To30">متأخر 1–30 يوماً.</param>
/// <param name="Days31To60">متأخر 31–60 يوماً.</param>
/// <param name="Days61To90">متأخر 61–90 يوماً.</param>
/// <param name="Over90">متأخر أكثر من 90 يوماً.</param>
/// <param name="Total">المجموع — مجموع الشرائح بالضبط.</param>
public sealed record PurchasingAgingBands(
    decimal NotDue,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total);

/// <summary>أعمار ذمم مورد واحد.</summary>
/// <param name="PartyId">معرّف المورد.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه ثنائي اللغة.</param>
/// <param name="Bands">شرائحه.</param>
public sealed record PurchasingAgingParty(Guid PartyId, string Code, LocalizedName Name, PurchasingAgingBands Bands);

/// <summary>تقرير أعمار الذمم الدائنة.</summary>
/// <param name="AsOf">تاريخ التقرير.</param>
/// <param name="Parties">الموردون.</param>
/// <param name="Totals">المجاميع.</param>
public sealed record PurchasingAging(
    DateOnly AsOf,
    IReadOnlyList<PurchasingAgingParty> Parties,
    PurchasingAgingBands Totals);

/// <summary>تخصيص مبلغ من سند صرف على فاتورة مورد مُرحَّلة.</summary>
/// <param name="BillId">الفاتورة المُرحَّلة.</param>
/// <param name="Amount">المبلغ المخصَّص عليها — لا يتجاوز المتبقّي منها.</param>
public sealed record PurchasingPaymentAllocationRequest(Guid BillId, decimal Amount);

/// <summary>
/// طلب تسجيل سند صرف <b>مسوّدة</b> بتخصيصاته.
/// <para>
/// <b>ورسوم التحويل ليست ذمّة مورد:</b> السند يُخصم من الخزينة بـ
/// (<paramref name="Paid"/> + <paramref name="BankFee"/>) ويُنقص ذمّة المورد بـ
/// <paramref name="Paid"/> وحده. وخلطهما يجعل رصيد المورد أقلّ ممّا هو، فتظهر مطالبةٌ
/// لا يعرف أحد مصدرها بعد أشهر.
/// </para>
/// <para>
/// ومجموع التخصيصات لا يتجاوز <paramref name="Paid"/>، وتخصيص كل فاتورة لا يتجاوز
/// المتبقّي عليها — والرفض <c>purchasing.over_allocation</c> يُسمّي الرقمين.
/// </para>
/// </summary>
/// <param name="Number">رقم السند — فريد داخل المستأجر.</param>
/// <param name="SupplierId">المورد المدفوع له.</param>
/// <param name="PaidOn">تاريخ الصرف الميلادي.</param>
/// <param name="SettlementMethod">طريقة التسوية: <c>cash</c> · <c>bank</c> · <c>card_clearing</c>.</param>
/// <param name="TreasuryPartyId">الخزينة أو الحساب البنكي في دفترها المساعد.</param>
/// <param name="Paid">المبلغ المدفوع للمورد.</param>
/// <param name="BankFee">رسوم التحويل التي تتحمّلها المنشأة، وصفرٌ إن لم توجد.</param>
/// <param name="Allocations">التخصيصات على فواتير المورد المُرحَّلة.</param>
public sealed record PurchasingPaymentRequest(
    string Number,
    Guid SupplierId,
    DateOnly PaidOn,
    string SettlementMethod,
    string TreasuryPartyId,
    decimal Paid,
    decimal BankFee,
    IReadOnlyList<PurchasingPaymentAllocationRequest> Allocations);

/// <summary>
/// طلب إنشاء أمر شراء.
/// <para>
/// <b>وأمر الشراء ليس حدثاً محاسبياً:</b> لا يُرحَّل، ولا مورد ترحيل له، ولا قيد ينشأ
/// عنه. القيد الأول في دورة الشراء هو <b>الاستلام</b>.
/// </para>
/// </summary>
/// <param name="Number">رقم الأمر — فريد داخل المستأجر.</param>
/// <param name="SupplierId">المورد.</param>
/// <param name="OrderedOn">تاريخ الأمر الميلادي.</param>
/// <param name="WarehouseId">المستودع المستقبِل — بُعدُ سطر الاستلام لاحقاً.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="Lines">السطور. أمرٌ بلا سطر يُرفض.</param>
public sealed record PurchasingOrderRequest(
    string Number,
    Guid SupplierId,
    DateOnly OrderedOn,
    string WarehouseId,
    string CostCenterId,
    IReadOnlyList<PurchasingLineRequest> Lines);

/// <summary>
/// سطر أمر شراء كما يخرج من السطح — <b>ومعرّفه هو مدخل الاستلام</b>.
/// <para>
/// سطر الاستلام يشير إلى سطر الأمر بمعرّفه، فمن أراد أن يستلم قرأ أمره أولاً. وبدون
/// نشر هذه المعرّفات يصير باب الاستلام باباً لا يوصل إليه بابٌ آخر على هذا السطح.
/// </para>
/// </summary>
/// <param name="Id">معرّف السطر.</param>
/// <param name="LineNo">رقمه داخل الأمر.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="Quantity">الكمية المطلوبة.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
public sealed record PurchasingOrderLine(Guid Id, int LineNo, string ItemId, decimal Quantity, decimal UnitPrice);

/// <summary>
/// أمر شراء كما يخرج من السطح.
/// <para>
/// <b>ولاحظ ما ليس فيه: لا <c>EntryId</c> ولا <c>AlreadyPosted</c>.</b> وذلك ليس نقصاً
/// بل هو الفرق نفسه: أمر الشراء التزامٌ تعاقدي لا واقعةٌ محاسبية، فلا قيد له ولا هوية
/// ترحيل — وحقلٌ فارغ لهما كان سيُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً».
/// </para>
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Net">الصافي قبل الضريبة.</param>
/// <param name="Tax">الضريبة.</param>
/// <param name="Gross">الإجمالي شامل الضريبة.</param>
/// <param name="Lines">السطور بمعرّفاتها.</param>
public sealed record PurchasingOrder(
    Guid Id,
    string Number,
    string State,
    decimal Net,
    decimal Tax,
    decimal Gross,
    IReadOnlyList<PurchasingOrderLine> Lines);

/// <summary>
/// سطر استلام: أي سطر أمر، وبأي كمية.
/// <para>الكمية المستلمة تتجاوز المطلوب تُرفض <b>هنا</b> لا عند الفاتورة.</para>
/// </summary>
/// <param name="OrderLineId">سطر الأمر المستلَم عليه.</param>
/// <param name="Quantity">الكمية المستلمة.</param>
public sealed record PurchasingGoodsReceiptLineRequest(Guid OrderLineId, decimal Quantity);

/// <summary>
/// طلب تسجيل استلام بضاعة <b>مسوّدة</b> على أمر شراء.
/// <para>
/// <b>والاستلام يمسّ المخزون:</b> ترحيله يُسجّل الوارد في دفتر المخزون المساعد بتكلفته
/// الفعلية <b>قبل</b> أن يُدين الحساب الضابط، بهوية الترحيل نفسها حرفاً بحرف. ولذلك
/// يشترط ترحيلُه استحقاق وحدة المخزون، وقدرةَ المطابقة الثلاثية في ملفّ المستأجر.
/// </para>
/// </summary>
/// <param name="Number">رقم الاستلام — فريد داخل المستأجر.</param>
/// <param name="OrderId">أمر الشراء.</param>
/// <param name="ReceivedOn">تاريخ الاستلام الميلادي.</param>
/// <param name="Lines">السطور. استلامٌ بلا سطر يُرفض.</param>
public sealed record PurchasingGoodsReceiptRequest(
    string Number,
    Guid OrderId,
    DateOnly ReceivedOn,
    IReadOnlyList<PurchasingGoodsReceiptLineRequest> Lines);
