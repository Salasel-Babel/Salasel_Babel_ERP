namespace Babel.Ai.Workspace;

/// <summary>
/// <b>أين تهبط المسوّدة — مسارُ الشاشة التي يفتحها زرُّ اللوح.</b>
/// <para>
/// <b>ولماذا خريطةٌ مغلقة لا اشتقاقٌ من المسار المنشور:</b> مسارُ الباب
/// (<c>/api/v1/companies/{companyId}/supplier-bills</c>) ليس مسارَ شاشة
/// (<c>/purchasing/bill</c>)، ولا قاعدةَ نصٍّ تحوّل أحدهما إلى الآخر. واشتقاقٌ
/// «ذكيّ» كان سينتج مساراً لا وجود له، فيفتح الزرُّ لا شيء — وهو أسوأ من زرٍّ لا يُعرض.
/// </para>
/// <para>
/// <b>ولا مُعرِّف صفٍّ في المسار.</b> الشاشة تُفتح على قائمتها والمسوّدة أعلاها، ومعرّفُ
/// المستند لا يُكتب في شريط العنوان: هو المعلومة نفسها التي تُقنَّع في بطاقة التأكيد،
/// وكتفُ من يقف خلف المستخدم هو الكتف نفسه.
/// </para>
/// <para>
/// <b>وحارسٌ يقرأ هذا الجدول من طرفيه:</b>
/// <c>EveryDraftOperationHasAScreenToLandOn</c> يفرض أن <b>كل</b> عملية مسوّدة في العقد
/// المنشور لها صفٌّ هنا، وأن <b>كل</b> مسارٍ يسمّيه هذا الجدول موجودٌ في
/// <c>web/src/app/shell/sections.ts</c> حرفاً بحرف، وأن لا صفَّ زائداً لعمليةٍ لا وجود
/// لها. فعمليةٌ تُنشر غداً بلا شاشة تُحمِّر البناء، ومسارٌ يُعاد تسميته في الواجهة
/// يُحمِّره كذلك.
/// </para>
/// <para>
/// <b>ونقصٌ مُعلَن لا مكتوم:</b> ستُّ عمليات ليس لها اليوم شاشةُ مستندٍ خاصّة بها
/// (إشعار دائن · مرتجع مشتريات · فاتورة إيجار · سند قبض مستأجر · سلفة موظف · سداد
/// تأمينات)، فتُسمّى لها <b>شاشةُ مجموعتها</b> — وهي الشاشة التي يجد عندها الإنسان
/// المستند ويرحّله بيده. وذلك مكتوبٌ في ‏ADR ومعدودٌ في الحارس، فلا يمرّ بوصفه اكتمالاً.
/// </para>
/// </summary>
public static class AgentDraftScreens
{
    /// <summary>
    /// الخريطة: معرّف العملية المنشورة ← مسار الشاشة في المتصفّح.
    /// <b>مرتَّبةٌ بترتيب الدورة لا بترتيب الحروف</b>، كما تُقرأ في الملاحة.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, string> Routes =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            // ── المبيعات ──────────────────────────────────────────────────────
            ["draftSalesInvoice"] = "/sales/invoice",
            ["draftCreditNote"] = "/sales/invoice",
            ["draftCustomerReceipt"] = "/sales/receipt",

            // ── المشتريات ─────────────────────────────────────────────────────
            ["draftGoodsReceipt"] = "/purchasing/goods-receipt",
            ["draftExpenseBill"] = "/purchasing/bill",
            ["draftPurchaseReturn"] = "/purchasing/bill",
            ["draftSupplierPayment"] = "/purchasing/payment",

            // ── المخزون ───────────────────────────────────────────────────────
            ["draftStockBill"] = "/inventory/movements",
            ["draftStockMovement"] = "/inventory/movements",
            ["draftStockTransfer"] = "/inventory/movements",

            // ── الموارد البشرية ───────────────────────────────────────────────
            ["draftPayrollRun"] = "/hr/payroll",
            ["draftPayrollPayment"] = "/hr/payroll",
            ["draftEmployeeAdvance"] = "/hr/payroll",
            ["draftSocialInsurancePayment"] = "/hr/payroll",
            ["draftEndOfServiceProvision"] = "/hr/end-of-service",
            ["draftEndOfServiceSettlement"] = "/hr/end-of-service",

            // ── المقاولات ─────────────────────────────────────────────────────
            ["draftClientCertificate"] = "/contracting/certificate",
            ["draftSubcontractorCertificate"] = "/contracting/subcontracting",
            ["draftSubcontractorAdvance"] = "/contracting/subcontracting",
            ["draftRetentionCollection"] = "/contracting/retention",
            ["draftRetentionRelease"] = "/contracting/retention",

            // ── العقارات ──────────────────────────────────────────────────────
            ["draftLeaseRegistration"] = "/realestate/lease",
            ["draftRentInvoice"] = "/realestate/arrears",
            ["draftTenantReceipt"] = "/realestate/arrears",
        };

    /// <summary>عددُ الصفوف — يقرؤه الحارس ويقارنه بعدد عمليات المسوّدات في العقد.</summary>
    public static int Count => Routes.Count;

    /// <summary>معرّفات العمليات التي لها شاشة، بترتيبٍ ثابت.</summary>
    public static IReadOnlyCollection<string> OperationIds => [.. Routes.Keys.Order(StringComparer.Ordinal)];

    /// <summary>
    /// مسارُ شاشة هذه العملية، أو <c>null</c> إن لم يكن لها صفّ.
    /// <b>ولا مسارٌ افتراضي يُخترع عند الغياب</b>: زرٌّ يفتح لا شيء يُعلّم المستخدم
    /// ألّا يثق باللوح كلّه، والرفض المُسمّى أرخص منه.
    /// </summary>
    /// <param name="operationId">معرّف العملية المنشورة.</param>
    public static string? RouteFor(string operationId)
    {
        ArgumentNullException.ThrowIfNull(operationId);
        return Routes.TryGetValue(operationId, out string? route) ? route : null;
    }
}
