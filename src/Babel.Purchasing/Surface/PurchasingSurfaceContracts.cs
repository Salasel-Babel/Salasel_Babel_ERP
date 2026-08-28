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

/// <summary>
/// سطر مستند مشتريات <b>مخزني</b> — ومعه وحدة قياسه.
/// <para>
/// <b>والفرق عن سطر المصروف هو الوحدة</b>: هذا السطر تصل كمّيته إلى دفتر المخزون
/// المساعد فتُضرب في تكلفة الوحدة، و«عشرة» بلا وحدة ليست معلومة. أمّا سطر المصروف
/// فلا يُحرّك مخزوناً، فوحدته العدّ دائماً.
/// </para>
/// </summary>
/// <param name="ItemId">رمز الصنف كما هو في كتالوج المخزون.</param>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="Description">البيان ثنائي اللغة.</param>
/// <param name="Quantity">الكمية بوحدتها.</param>
/// <param name="Unit">رمز وحدة القياس.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="TaxClassification">التصنيف الضريبي.</param>
/// <param name="TaxRate">نسبة الضريبة كسراً عشرياً.</param>
public sealed record PurchasingStockLineRequest(
    string ItemId,
    string ItemGroup,
    LocalizedName Description,
    decimal Quantity,
    string Unit,
    decimal UnitPrice,
    string TaxClassification,
    decimal TaxRate);

/// <summary>طلب إنشاء أمر شراء.</summary>
/// <param name="Number">رقم الأمر.</param>
/// <param name="SupplierId">المورد.</param>
/// <param name="OrderedOn">تاريخ الأمر.</param>
/// <param name="WarehouseId">المستودع المستقبِل.</param>
/// <param name="CostCenterId">مركز التكلفة.</param>
/// <param name="Lines">السطور.</param>
public sealed record PurchasingOrderRequest(
    string Number,
    Guid SupplierId,
    DateOnly OrderedOn,
    string WarehouseId,
    string CostCenterId,
    IReadOnlyList<PurchasingStockLineRequest> Lines);

/// <summary>سطر مستند مشتريات كما يخرج من السطح — ومعرّفه مدخل المستند التالي في الدورة.</summary>
/// <param name="Id">معرّف السطر.</param>
/// <param name="LineNo">رقمه.</param>
/// <param name="ItemId">الصنف.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="Unit">وحدة القياس.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
public sealed record PurchasingLine(
    Guid Id, int LineNo, string ItemId, decimal Quantity, string Unit, decimal UnitPrice);

/// <summary>مستند مشتريات ومعه سطوره — مورد واحد لا موردان.</summary>
/// <param name="Document">المستند بحالته ومجاميعه.</param>
/// <param name="Lines">سطوره بمعرّفاتها.</param>
public sealed record PurchasingDocumentWithLines(
    PurchasingDocument Document, IReadOnlyList<PurchasingLine> Lines);

/// <summary>سطر استلام: أي سطر أمر، وبأي كمية.</summary>
/// <param name="OrderLineId">سطر الأمر.</param>
/// <param name="Quantity">الكمية المستلمة.</param>
public sealed record PurchasingReceiptLineRequest(Guid OrderLineId, decimal Quantity);

/// <summary>طلب تسجيل استلام بضاعة <b>مسوّدة</b>.</summary>
/// <param name="Number">رقم الاستلام.</param>
/// <param name="OrderId">أمر الشراء.</param>
/// <param name="ReceivedOn">تاريخ الاستلام.</param>
/// <param name="Lines">السطور.</param>
public sealed record PurchasingReceiptRequest(
    string Number,
    Guid OrderId,
    DateOnly ReceivedOn,
    IReadOnlyList<PurchasingReceiptLineRequest> Lines);

/// <summary>سطر فاتورة مورد مخزنية — يرجع إلى سطر استلام بعينه، وهو ضلع المطابقة الثالث.</summary>
/// <param name="ReceiptLineId">سطر الاستلام.</param>
/// <param name="Quantity">الكمية المفوترة.</param>
/// <param name="UnitPrice">سعر الوحدة على الفاتورة.</param>
/// <param name="TaxClassification">التصنيف الضريبي.</param>
/// <param name="TaxRate">نسبة الضريبة كسراً عشرياً.</param>
public sealed record PurchasingStockBillLineRequest(
    Guid ReceiptLineId,
    decimal Quantity,
    decimal UnitPrice,
    string TaxClassification,
    decimal TaxRate);

/// <summary>طلب إنشاء فاتورة مورد <b>مخزنية</b> تُطابَق ثلاثياً.</summary>
/// <param name="Number">رقم الفاتورة.</param>
/// <param name="ReceiptId">الاستلام.</param>
/// <param name="IssuedOn">تاريخ الفاتورة.</param>
/// <param name="Lines">السطور.</param>
public sealed record PurchasingStockBillRequest(
    string Number,
    Guid ReceiptId,
    DateOnly IssuedOn,
    IReadOnlyList<PurchasingStockBillLineRequest> Lines);

/// <summary>
/// طلب إنشاء <b>مرتجع مشتريات</b> (إشعار مدين) <b>مسوّدة</b> على فاتورة مخزنية مُرحَّلة.
/// <para>
/// <b>ولاحظ ما ليس فيه: صافي المرتجع.</b> المصفوفة تقول إن الصافي «بتكلفة الاستلام
/// الأصلي لا بتكلفة اليوم»، وتلك التكلفة يملكها دفتر المخزون وحده — فالطلب يحمل
/// الكمّية، والمبلغ يُحسب لحظة الترحيل ولا يُملى.
/// </para>
/// </summary>
/// <param name="Number">رقم المرتجع.</param>
/// <param name="BillId">الفاتورة المخزنية الأصلية.</param>
/// <param name="ReceiptLineId">سطر الاستلام الذي تُردّ بضاعته — به يُقيَّم المرتجع.</param>
/// <param name="IssuedOn">تاريخ المرتجع.</param>
/// <param name="Quantity">الكمية المرتجعة بوحدة الاستلام.</param>
/// <param name="Tax">ضريبة المرتجع — بتصنيف الفاتورة الأصلية.</param>
public sealed record PurchasingReturnRequest(
    string Number,
    Guid BillId,
    Guid ReceiptLineId,
    DateOnly IssuedOn,
    decimal Quantity,
    decimal Tax);
