using Babel.SharedKernel;

namespace Babel.Sales.Application;

/// <summary>مسوّدة عميل. <c>name_ar</c> و<c>name_en</c> إلزاميان على كل بيانات أساسية.</summary>
/// <param name="Code">رمز العميل داخل المستأجر.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="CreditLimit">حد الائتمان.</param>
/// <param name="PaymentTermsDays">مهلة السداد بالأيام.</param>
public sealed record CustomerDraft(string Code, LocalizedName Name, Money CreditLimit, int PaymentTermsDays);

/// <summary>العميل كما يراه المستدعي.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Code">الرمز.</param>
/// <param name="Name">الاسم ثنائي اللغة.</param>
/// <param name="CreditLimit">حد الائتمان.</param>
/// <param name="PaymentTermsDays">مهلة السداد.</param>
public sealed record CustomerView(Guid Id, string Code, LocalizedName Name, Money CreditLimit, int PaymentTermsDays);

/// <summary>
/// سطر مستند مبيعات.
/// <para>
/// الضريبة تُحسب وتُقرَّب <b>على السطر</b>، ومجموع المستند هو مجموع سطور مقرَّبة —
/// ولا يُعاد تقريب المجموع. العكس يُنتج خلافات هللة واحدة على نطاق واسع.
/// </para>
/// </summary>
/// <param name="ItemGroup">مجموعة الصنف — مؤهّل الدور، لا حساب.</param>
/// <param name="Description">بيان السطر ثنائي اللغة.</param>
/// <param name="Quantity">الكمية.</param>
/// <param name="UnitPrice">سعر الوحدة.</param>
/// <param name="Discount">خصم السطر.</param>
/// <param name="TaxClassification">التصنيف الضريبي: <c>standard</c> · <c>zero</c> · <c>exempt</c>.</param>
/// <param name="TaxRate">نسبة الضريبة كسراً عشرياً.</param>
public sealed record SalesLineDraft(
    string ItemGroup,
    LocalizedName Description,
    decimal Quantity,
    Money UnitPrice,
    Money Discount,
    string TaxClassification,
    decimal TaxRate);

/// <summary>مسوّدة مستند مبيعات بسطوره.</summary>
/// <param name="Number">رقم المستند.</param>
/// <param name="CustomerId">العميل.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="BranchId">الفرع — بُعد تحليلي إلزامي على الإيراد.</param>
/// <param name="Lines">السطور.</param>
public sealed record SalesDocumentDraft(
    string Number,
    Guid CustomerId,
    DateOnly IssuedOn,
    string BranchId,
    IReadOnlyList<SalesLineDraft> Lines);

/// <summary>مجاميع مستند: صافٍ وضريبة وإجمالي.</summary>
/// <param name="Net">الصافي قبل الضريبة.</param>
/// <param name="Tax">الضريبة.</param>
/// <param name="Gross">الإجمالي شامل الضريبة.</param>
public sealed record DocumentTotals(Money Net, Money Tax, Money Gross);

/// <summary>مستند مبيعات كما يراه المستدعي.</summary>
/// <param name="Id">المعرّف.</param>
/// <param name="Number">الرقم.</param>
/// <param name="State">الحالة.</param>
/// <param name="Totals">المجاميع.</param>
/// <param name="EntryId">معرّف القيد إن رُحّل.</param>
public sealed record SalesDocumentView(Guid Id, string Number, string State, DocumentTotals Totals, Guid? EntryId);

/// <summary>تخصيص مبلغ على فاتورة.</summary>
/// <param name="InvoiceId">الفاتورة.</param>
/// <param name="Amount">المبلغ المخصَّص.</param>
public sealed record AllocationDraft(Guid InvoiceId, Money Amount);

/// <summary>مسوّدة سند قبض.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="CustomerId">العميل.</param>
/// <param name="ReceivedOn">تاريخ القبض.</param>
/// <param name="SettlementMethod">طريقة التسوية: <c>cash</c> · <c>bank</c> · <c>card_clearing</c>.</param>
/// <param name="TreasuryPartyId">معرّف الخزينة أو الحساب البنكي في دفترها المساعد.</param>
/// <param name="Received">المبلغ المقبوض فعلاً.</param>
/// <param name="SettlementDiscount">خصم تعجيل السداد الممنوح.</param>
/// <param name="Allocations">التخصيصات على الفواتير.</param>
public sealed record CustomerReceiptDraft(
    string Number,
    Guid CustomerId,
    DateOnly ReceivedOn,
    string SettlementMethod,
    string TreasuryPartyId,
    Money Received,
    Money SettlementDiscount,
    IReadOnlyList<AllocationDraft> Allocations);

/// <summary>مسوّدة دفعة مقدمة من عميل.</summary>
/// <param name="Number">رقم السند.</param>
/// <param name="CustomerId">العميل.</param>
/// <param name="ReceivedOn">تاريخ القبض.</param>
/// <param name="SettlementMethod">طريقة التسوية.</param>
/// <param name="TreasuryPartyId">معرّف الخزينة.</param>
/// <param name="Net">صافي الدفعة.</param>
/// <param name="Tax">ضريبة الدفعة إن استحقت.</param>
/// <param name="TaxDueOnCollection">هل الضريبة مستحقة عند القبض؟ ⚠️ بند مفتوح في المصفوفة.</param>
public sealed record CustomerAdvanceDraft(
    string Number,
    Guid CustomerId,
    DateOnly ReceivedOn,
    string SettlementMethod,
    string TreasuryPartyId,
    Money Net,
    Money Tax,
    bool TaxDueOnCollection);

/// <summary>مسوّدة إشعار دائن مرتبط بفاتورة أصلية.</summary>
/// <param name="Number">رقم الإشعار.</param>
/// <param name="InvoiceId">الفاتورة الأصلية — الإشعار لا يوجد بلا أصل.</param>
/// <param name="IssuedOn">تاريخ الإصدار.</param>
/// <param name="Lines">سطور المرتجع أو التخفيض.</param>
public sealed record CreditNoteDraft(
    string Number,
    Guid InvoiceId,
    DateOnly IssuedOn,
    IReadOnlyList<SalesLineDraft> Lines);

/// <summary>شرائح أعمار الديون.</summary>
/// <param name="NotDue">لم يستحق بعد.</param>
/// <param name="Days1To30">متأخر 1–30 يوماً.</param>
/// <param name="Days31To60">متأخر 31–60 يوماً.</param>
/// <param name="Days61To90">متأخر 61–90 يوماً.</param>
/// <param name="Over90">متأخر أكثر من 90 يوماً.</param>
/// <param name="Total">المجموع — مجموع الشرائح بالضبط.</param>
public sealed record AgingBuckets(
    Money NotDue,
    Money Days1To30,
    Money Days31To60,
    Money Days61To90,
    Money Over90,
    Money Total);

/// <summary>أعمار ديون طرف واحد.</summary>
/// <param name="PartyId">معرّف الطرف.</param>
/// <param name="Code">رمزه.</param>
/// <param name="Name">اسمه ثنائي اللغة.</param>
/// <param name="Buckets">شرائحه.</param>
public sealed record PartyAging(Guid PartyId, string Code, LocalizedName Name, AgingBuckets Buckets);

/// <summary>تقرير أعمار الديون.</summary>
/// <param name="AsOf">تاريخ التقرير.</param>
/// <param name="Parties">الأطراف.</param>
/// <param name="Totals">المجاميع.</param>
public sealed record AgingReport(DateOnly AsOf, IReadOnlyList<PartyAging> Parties, AgingBuckets Totals);

/// <summary>سطر كشف حساب طرف.</summary>
/// <param name="Date">تاريخ الحركة.</param>
/// <param name="DocumentType">نوع المستند.</param>
/// <param name="Number">رقم المستند.</param>
/// <param name="Description">البيان ثنائي اللغة.</param>
/// <param name="Debit">مدين.</param>
/// <param name="Credit">دائن.</param>
/// <param name="RunningBalance">الرصيد المتحرّك.</param>
public sealed record StatementLine(
    DateOnly Date,
    string DocumentType,
    string Number,
    LocalizedName Description,
    Money Debit,
    Money Credit,
    Money RunningBalance);

/// <summary>كشف حساب طرف.</summary>
/// <param name="PartyId">الطرف.</param>
/// <param name="From">من تاريخ.</param>
/// <param name="To">إلى تاريخ.</param>
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
