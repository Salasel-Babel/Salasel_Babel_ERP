using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Purchasing.Application;

/// <summary>أخطاء وحدة المشتريات. كل خطأ برمز ثابت ورسالتين — الرفض يُقرأ في تدقيق.</summary>
internal static class PurchasingErrors
{
    public static Error SupplierNotFound(Guid id) => new(
        "purchasing.supplier_not_found",
        "لا مورد بهذا المعرّف: " + id.ToString("D", CultureInfo.InvariantCulture),
        "No supplier with this identifier: " + id.ToString("D", CultureInfo.InvariantCulture));

    public static Error DocumentNotFound(string type, Guid id) => new(
        "purchasing.document_not_found",
        "لا مستند " + type + " بهذا المعرّف: " + id.ToString("D", CultureInfo.InvariantCulture),
        "No " + type + " document with this identifier: " + id.ToString("D", CultureInfo.InvariantCulture));

    public static Error DuplicateNumber(string number) => new(
        "purchasing.duplicate_number",
        "رقم مستند مستعمل من قبل: " + number,
        "Document number already used: " + number);

    public static readonly Error NoLines = new(
        "purchasing.no_lines",
        "مستند بلا سطور لا يُصدَر.",
        "A document without lines is not issued.");

    public static Error NotInState(string number, string state, string expected) => new(
        "purchasing.wrong_state",
        "المستند " + number + " في الحالة " + state + " والمطلوب " + expected + ".",
        "Document " + number + " is in state " + state + " while " + expected + " is required.");

    public static Error LineNotFound(Guid id) => new(
        "purchasing.line_not_found",
        "لا سطر بهذا المعرّف: " + id.ToString("D", CultureInfo.InvariantCulture),
        "No line with this identifier: " + id.ToString("D", CultureInfo.InvariantCulture));

    /// <summary>الضلع الأول من المطابقة الثلاثية: المستلَم لا يتجاوز المطلوب.</summary>
    public static Error ReceiptExceedsOrder(string item, decimal attempted, decimal available) => new(
        "purchasing.receipt_exceeds_order",
        "استلام يتجاوز أمر الشراء للصنف " + item + ": المستلَم " + Format(attempted) + " والمتبقّي " + Format(available) + ".",
        "Receipt exceeds the purchase order for item " + item + ": " + Format(attempted) + " received against " + Format(available) + " outstanding.");

    /// <summary>الضلع الثاني: المفوتَر لا يتجاوز المستلَم. أشهر باب لدفع ما لم يُستلم.</summary>
    public static Error BillExceedsReceipt(string item, decimal attempted, decimal available) => new(
        "purchasing.bill_exceeds_receipt",
        "فاتورة تتجاوز الاستلام للصنف " + item + ": المفوتَر " + Format(attempted) + " والمستلَم المتبقّي " + Format(available) + ".",
        "The bill exceeds the goods receipt for item " + item + ": billed " + Format(attempted) + " against " + Format(available) + " received and unbilled.");

    /// <summary>
    /// فرق سعر لصالح المنشأة: المصفوفة تُثبت سطر فرق السعر <b>مديناً</b> ولا تحمل
    /// سطراً دائناً مقابلاً، فالتعبير عنه مستحيل داخل العقد القائم — والرفض أولى من قيد
    /// غير متوازن أو من قيد يقلب إشارة بصمت.
    /// </summary>
    public static Error FavourablePriceVarianceNotExpressible(decimal variance) => new(
        "purchasing.favourable_price_variance_not_expressible",
        "فرق سعر سالب (" + Format(variance) + ") لا يعبّر عنه قالب فاتورة المشتريات المخزنية: "
        + "سطر فرق السعر مدين دائماً ولا مقابل دائن له في المصفوفة. الترحيل مرفوض.",
        "A negative price variance (" + Format(variance) + ") cannot be expressed by the stock purchase invoice template: "
        + "the variance line is always a debit and the matrix carries no credit counterpart. Posting refused.");

    /// <summary>
    /// المصفوفة تحمل قالب إشعار مدين <b>للمرتجع المخزني وحده</b>: سطره الدائن
    /// مراقبة مخزون بمرجع صنف وبُعد مستودع. ولا قالب لمرتجع فاتورة مصروف، ولا
    /// يجوز اختراع واحد داخل الوحدة — فالرفض بصوت عالٍ هو الجواب.
    /// </summary>
    public static Error DebitNoteOnExpenseBillNotExpressible(string number) => new(
        "purchasing.debit_note_on_expense_bill_not_expressible",
        "الفاتورة " + number + " فاتورة مصروف، وقالب الإشعار المدين في المصفوفة مرتجع مخزني "
        + "يُدين ذمة المورد ويُدان مراقبة المخزون. لا قالب لمرتجع مصروف، والترحيل مرفوض.",
        "Bill " + number + " is an expense bill, and the matrix debit note template is a stock return that "
        + "debits the supplier control and credits inventory control. There is no expense-return template; posting is refused.");

    public static Error OverAllocation(string number, decimal attempted, decimal available) => new(
        "purchasing.over_allocation",
        "تخصيص زائد على " + number + ": المطلوب " + Format(attempted) + " والمتاح " + Format(available) + ".",
        "Over-allocation on " + number + ": attempted " + Format(attempted) + " against " + Format(available) + " available.");

    public static readonly Error NegativeAmount = new(
        "purchasing.negative_amount",
        "مبلغ سالب على مستند. الاتجاه يُعبَّر عنه بنوع المستند لا بإشارة المبلغ.",
        "A negative amount on a document; direction is expressed by the document type, not by the sign.");

    /// <summary>
    /// نية ترحيل بلا رمز حدث. رمز الحدث حقل في هوية الإحكام، ورمزٌ فارغ يجعل حدثين
    /// مختلفين من المستند نفسه وعند الإطلاق نفسه هويةً واحدة — فيُبتلع الثاني بصمت
    /// (ADR-0016 · ADR-0017). والمحرك يرفضه بـ<c>ledger.posting.missing_event_code</c>،
    /// والبوابة ترفضه هنا قبل أن تكتب صفّ محاولة بهوية ناقصة.
    /// </summary>
    public static Error MissingEventCode(string documentType, Guid documentId) => new(
        "purchasing.posting.missing_event_code",
        "نية ترحيل بلا رمز حدث للمستند " + documentType + "/" + documentId.ToString("D", CultureInfo.InvariantCulture)
        + ". رمز الحدث جزء من هوية الإحكام، ورمزٌ فارغ يجعل حدثين مختلفين هويةً واحدة فيُبتلع الثاني بصمت.",
        "A posting intent without an event code for document " + documentType + "/"
        + documentId.ToString("D", CultureInfo.InvariantCulture)
        + ". The event code is part of the posting identity; an empty code collapses two different events "
        + "into one identity and the second is swallowed silently.");

    public static Error PostingRefused(IReadOnlyList<Error> errors) => new(
        "purchasing.posting_refused",
        "رفض محرك الترحيل الطلب: " + string.Join(" | ", errors.Select(static e => e.MessageAr)),
        "The posting engine refused the request: " + string.Join(" | ", errors.Select(static e => e.MessageEn)));

    public static Error ControlPointUnavailable(IReadOnlyList<Error> errors) => new(
        "purchasing.control_point_unavailable",
        "تعذّرت قراءة نقطة الضبط، والمطابقة بلا نقطة ضبط ليست مطابقة: "
        + string.Join(" | ", errors.Select(static e => e.MessageAr)),
        "The control point could not be read, and a reconciliation without one is not a reconciliation: "
        + string.Join(" | ", errors.Select(static e => e.MessageEn)));

    private static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
