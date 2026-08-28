using Babel.SharedKernel;

namespace Babel.Sales.Surface;

/// <summary>
/// طلب تسجيل عميل. <b>ولا حقل مستأجر فيه</b>: النطاق يأتي من الاعتماد ومن المسار،
/// لا من الجسم.
/// </summary>
/// <param name="Code">رمز العميل داخل المستأجر — هوية يحملها تاريخه، لا نصّ معروض.</param>
/// <param name="Name">الاسم ثنائي اللغة. الطرفان إلزاميان على كل بيانات أساسية.</param>
/// <param name="CreditLimit">حدّ الائتمان بعملة المنشأة.</param>
/// <param name="PaymentTermsDays">مهلة السداد بالأيام — منها يُشتقّ تاريخ الاستحقاق.</param>
public sealed record SalesPartyRequest(string Code, LocalizedName Name, decimal CreditLimit, int PaymentTermsDays);

/// <summary>عميل كما يخرج من السطح.</summary>
/// <param name="Id">المعرّف الذي تُبنى عليه المستندات.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="CreditLimit">حدّ الائتمان.</param>
/// <param name="PaymentTermsDays">مهلة السداد.</param>
public sealed record SalesParty(Guid Id, string Code, LocalizedName Name, decimal CreditLimit, int PaymentTermsDays);

/// <summary>
/// سطر مستند مبيعات على السطح المنشور.
/// <para>
/// <b>ولاحظ ما ليس فيه: لا حساب ولا رمز حساب.</b> السطر يحمل <c>ItemGroup</c> — مؤهّل دور —
/// ومصفوفة الترحيل وحدها تُحوّله إلى حساب. القاعدة 2 ممتدّةً إلى السطح.
/// </para>
/// </summary>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور.</param>
/// <param name="Description">بيان السطر ثنائي اللغة.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="Discount">خصم السطر.</param>
/// <param name="TaxClassification">التصنيف الضريبي: <c>standard</c> · <c>zero</c> · <c>exempt</c>.</param>
/// <param name="TaxRate">نسبة الضريبة كسراً عشرياً.</param>
/// <param name="OriginalInvoiceLineId">
/// على سطر الإشعار الدائن وحده: سطر الفاتورة الذي تُردّ بضاعته، أو غيابه فالإشعار
/// <b>تخفيض قيمة لا ردّ بضاعة</b>. والفرق بينهما قرار تجاري لا يُخمَّن.
/// </param>
public sealed record SalesLineRequest(
    string ItemGroup,
    LocalizedName Description,
    decimal Quantity,
    decimal UnitPrice,
    decimal Discount,
    string TaxClassification,
    decimal TaxRate,
    Guid? OriginalInvoiceLineId = null);

/// <summary>طلب إنشاء فاتورة مبيعات <b>مسوّدة</b>.</summary>
/// <param name="Number">رقم الفاتورة — فريد داخل المستأجر.</param>
/// <param name="CustomerId">العميل.</param>
/// <param name="IssuedOn">تاريخ الإصدار الميلادي.</param>
/// <param name="BranchId">الفرع — بُعد تحليلي إلزامي على الإيراد.</param>
/// <param name="Lines">السطور. فاتورة بلا سطر تُرفض.</param>
public sealed record SalesInvoiceRequest(
    string Number,
    Guid CustomerId,
    DateOnly IssuedOn,
    string BranchId,
    IReadOnlyList<SalesLineRequest> Lines);

/// <summary>
/// طلب إنشاء إشعار دائن <b>مسوّدة</b> على فاتورة مُرحَّلة.
/// <para>ولا عميل فيه: العميل هو عميل الفاتورة الأصلية، ولا يُعاد ذكره فينحرف.</para>
/// </summary>
/// <param name="Number">رقم الإشعار.</param>
/// <param name="InvoiceId">الفاتورة الأصلية — الإشعار لا يوجد بلا أصل.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="Lines">سطور المرتجع أو التخفيض.</param>
public sealed record SalesCreditNoteRequest(
    string Number,
    Guid InvoiceId,
    DateOnly IssuedOn,
    IReadOnlyList<SalesLineRequest> Lines);

/// <summary>
/// مستند مبيعات كما يخرج من السطح.
/// </summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة: <c>DRAFT</c> · <c>POSTED</c> وما إليهما.</param>
/// <param name="Net">الصافي قبل الضريبة.</param>
/// <param name="Tax">الضريبة.</param>
/// <param name="Gross">الإجمالي شامل الضريبة.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل، وإلا غياب.</param>
/// <param name="AlreadyPosted">
/// هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟ <b>معلومة لا تُشتقّ من الحالة</b>: مستندٌ
/// حالته <c>POSTED</c> بعد النداء لا يقول وحده أيُّ النداءين رحّله.
/// </param>
public sealed record SalesDocument(
    Guid Id,
    string Number,
    string State,
    decimal Net,
    decimal Tax,
    decimal Gross,
    Guid? EntryId,
    bool AlreadyPosted);

/// <summary>شرائح أعمار الذمم.</summary>
/// <param name="NotDue">لم يستحق بعد.</param>
/// <param name="Days1To30">متأخر 1–30 يوماً.</param>
/// <param name="Days31To60">متأخر 31–60 يوماً.</param>
/// <param name="Days61To90">متأخر 61–90 يوماً.</param>
/// <param name="Over90">متأخر أكثر من 90 يوماً.</param>
/// <param name="Total">المجموع — مجموع الشرائح بالضبط.</param>
public sealed record SalesAgingBands(
    decimal NotDue,
    decimal Days1To30,
    decimal Days31To60,
    decimal Days61To90,
    decimal Over90,
    decimal Total);

/// <summary>أعمار ذمم عميل واحد.</summary>
/// <param name="PartyId">معرّف العميل.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه ثنائي اللغة.</param>
/// <param name="Bands">شرائحه.</param>
public sealed record SalesAgingParty(Guid PartyId, string Code, LocalizedName Name, SalesAgingBands Bands);

/// <summary>تقرير أعمار الذمم المدينة.</summary>
/// <param name="AsOf">تاريخ التقرير.</param>
/// <param name="Parties">العملاء.</param>
/// <param name="Totals">المجاميع.</param>
public sealed record SalesAging(DateOnly AsOf, IReadOnlyList<SalesAgingParty> Parties, SalesAgingBands Totals);
