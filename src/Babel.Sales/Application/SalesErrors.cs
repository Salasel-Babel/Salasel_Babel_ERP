using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Sales.Application;

/// <summary>أخطاء وحدة المبيعات. كل خطأ برمز ثابت ورسالتين — الرفض يُقرأ في تدقيق.</summary>
internal static class SalesErrors
{
    public static Error CustomerNotFound(Guid id) => new(
        "sales.customer_not_found",
        "لا عميل بهذا المعرّف: " + id.ToString("D", CultureInfo.InvariantCulture),
        "No customer with this identifier: " + id.ToString("D", CultureInfo.InvariantCulture));

    public static Error DocumentNotFound(string type, Guid id) => new(
        "sales.document_not_found",
        "لا مستند " + type + " بهذا المعرّف: " + id.ToString("D", CultureInfo.InvariantCulture),
        "No " + type + " document with this identifier: " + id.ToString("D", CultureInfo.InvariantCulture));

    public static Error DuplicateNumber(string number) => new(
        "sales.duplicate_number",
        "رقم مستند مستعمل من قبل: " + number,
        "Document number already used: " + number);

    public static readonly Error NoLines = new(
        "sales.no_lines",
        "مستند بلا سطور لا يُصدَر.",
        "A document without lines is not issued.");

    public static readonly Error CurrencyMismatch = new(
        "sales.currency_mismatch",
        "عملة واحدة للمستند كله. الخلط بلا سعر صرف صريح مرفوض.",
        "One currency per document; mixing without an explicit exchange rate is refused.");

    public static Error NotInState(string number, string state, string expected) => new(
        "sales.wrong_state",
        "المستند " + number + " في الحالة " + state + " والمطلوب " + expected + ".",
        "Document " + number + " is in state " + state + " while " + expected + " is required.");

    public static readonly Error PostedDocumentIsNotEdited = new(
        "sales.posted_document_is_immutable",
        "المستند المُرحَّل لا يُعدَّل ولا يُحذف. التصحيح بإشعار دائن أو بقيد عكسي (ADR-0002).",
        "A posted document is never edited or deleted; correction is by credit note or reversal (ADR-0002).");

    public static Error OverAllocation(string number, decimal attempted, decimal available) => new(
        "sales.over_allocation",
        "تخصيص زائد على " + number + ": المطلوب " + Format(attempted) + " والمتاح " + Format(available) + ".",
        "Over-allocation on " + number + ": attempted " + Format(attempted) + " against " + Format(available) + " available.");

    public static Error CreditLimitExceeded(string code, decimal exposure, decimal limit) => new(
        "sales.credit_limit_exceeded",
        "تجاوز حد ائتمان العميل " + code + ": الانكشاف " + Format(exposure) + " والحد " + Format(limit) + ".",
        "Credit limit exceeded for customer " + code + ": exposure " + Format(exposure) + " against limit " + Format(limit) + ".");

    public static readonly Error NegativeAmount = new(
        "sales.negative_amount",
        "مبلغ سالب على مستند. الاتجاه يُعبَّر عنه بنوع المستند لا بإشارة المبلغ.",
        "A negative amount on a document; direction is expressed by the document type, not by the sign.");

    public static Error PostingRefused(IReadOnlyList<Error> errors) => new(
        "sales.posting_refused",
        "رفض محرك الترحيل الطلب: " + string.Join(" | ", errors.Select(static e => e.MessageAr)),
        "The posting engine refused the request: " + string.Join(" | ", errors.Select(static e => e.MessageEn)));

    public static Error ControlPointUnavailable(IReadOnlyList<Error> errors) => new(
        "sales.control_point_unavailable",
        "تعذّرت قراءة نقطة الضبط، والمطابقة بلا نقطة ضبط ليست مطابقة: "
        + string.Join(" | ", errors.Select(static e => e.MessageAr)),
        "The control point could not be read, and a reconciliation without one is not a reconciliation: "
        + string.Join(" | ", errors.Select(static e => e.MessageEn)));

    private static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
