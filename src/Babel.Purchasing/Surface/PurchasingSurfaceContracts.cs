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
