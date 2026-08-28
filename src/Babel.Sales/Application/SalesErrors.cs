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

    /// <summary>
    /// سطرٌ لا وجود له تحت هذا المستند.
    /// <para>
    /// <b>ولماذا يُرفض بدل أن يُخترع له معرّف:</b> معرّف السطر هو معرّف المستند في هوية
    /// الترحيل بعد أن صار قيد التكلفة بحبيبيّة السطر. فمعرّفٌ لا يقابله صفٌّ حقيقي
    /// مملوكٌ لهذه الفاتورة يجعل «كل قيود هذه الفاتورة» سؤالاً بلا جواب، ويُنتج قيداً
    /// معلّقاً تحت مستندٍ لا وجود له — وهو <c>docs/evidence/traps.md#fakh-49</c> حرفياً.
    /// </para>
    /// </summary>
    /// <param name="documentType">نوع المستند المالك.</param>
    /// <param name="ownerId">معرّف المستند المالك.</param>
    /// <param name="lineId">معرّف السطر المطلوب.</param>
    public static Error LineNotFound(string documentType, Guid ownerId, Guid lineId) => new(
        "sales.line_not_found",
        "لا سطر بالمعرّف " + lineId.ToString("D", CultureInfo.InvariantCulture) + " تحت المستند "
        + documentType + "/" + ownerId.ToString("D", CultureInfo.InvariantCulture)
        + ". ومعرّف السطر هو معرّف المستند في هوية ترحيل قيد التكلفة، فلا يُقبل معرّف لا يقابله سطر.",
        "No line with identifier " + lineId.ToString("D", CultureInfo.InvariantCulture) + " under document "
        + documentType + "/" + ownerId.ToString("D", CultureInfo.InvariantCulture)
        + ". The line identifier is the document identifier in the cost entry's posting identity, "
        + "so an identifier with no matching line is refused.");

    /// <summary>
    /// سطر إشعار دائن يردّ بضاعة على سطر فاتورة <b>لم يُرحَّل له قيد تكلفة قط</b>.
    /// <para>
    /// المصفوفة تقول عن تكلفة المرتجع: «بنفس تكلفة قيد البيع الأصلي لا بتكلفة اليوم».
    /// فبلا صرفٍ أصلي لا توجد تكلفةٌ تُقال — ولا يُخترع لها رقم. والمخرج المشروع:
    /// إمّا أن يُرحَّل قيد تكلفة الفاتورة أولاً، وإمّا أن يكون هذا الإشعار
    /// <b>تخفيض قيمة</b> فيُترك سطره بلا سطر أصلي.
    /// </para>
    /// </summary>
    /// <param name="invoiceLineId">سطر الفاتورة المشار إليه.</param>
    public static Error OriginalCostEntryNotFound(Guid invoiceLineId) => new(
        "sales.original_cost_entry_not_found",
        "سطر الفاتورة " + invoiceLineId.ToString("D", CultureInfo.InvariantCulture)
        + " لا قيد تكلفة مُرحَّلاً له، فلا تكلفة صرفٍ أصلي يُقيَّم بها المرتجع. "
        + "والمرتجع يُقيَّم بتكلفة صرفه الأصلي لا بمتوسط اليوم، ولا يُخترع له رقم — "
        + "فإمّا أن يُرحَّل قيد تكلفة الفاتورة أولاً، وإمّا أن يكون هذا الإشعار تخفيض قيمة لا ردّ بضاعة.",
        "Invoice line " + invoiceLineId.ToString("D", CultureInfo.InvariantCulture)
        + " has no posted cost of sales entry, so there is no original issue cost to value the return at. "
        + "A return is valued at the cost of its original issue, never at today's average, and no number is invented — "
        + "either post the invoice's cost entry first, or this credit note is a value allowance and not a goods return.");

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

    /// <summary>
    /// نية ترحيل بلا رمز حدث. رمز الحدث حقل في هوية الإحكام، ورمزٌ فارغ يجعل حدثين
    /// مختلفين من المستند نفسه وعند الإطلاق نفسه هويةً واحدة — فيُبتلع الثاني بصمت
    /// (ADR-0016 · ADR-0017). والمحرك يرفضه بـ<c>ledger.posting.missing_event_code</c>،
    /// والبوابة ترفضه هنا قبل أن تكتب صفّ محاولة بهوية ناقصة.
    /// </summary>
    public static Error MissingEventCode(string documentType, Guid documentId) => new(
        "sales.posting.missing_event_code",
        "نية ترحيل بلا رمز حدث للمستند " + documentType + "/" + documentId.ToString("D", CultureInfo.InvariantCulture)
        + ". رمز الحدث جزء من هوية الإحكام، ورمزٌ فارغ يجعل حدثين مختلفين هويةً واحدة فيُبتلع الثاني بصمت.",
        "A posting intent without an event code for document " + documentType + "/"
        + documentId.ToString("D", CultureInfo.InvariantCulture)
        + ". The event code is part of the posting identity; an empty code collapses two different events "
        + "into one identity and the second is swallowed silently.");

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

    // ═══════════════════════════════════════════════════════════════════════
    // القبول مقابل ملفّ قدرات المستأجر
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// مستأجر بلا ملفّ قدرات. <b>رفض لا فتح</b>: غياب الملفّ يعني «لم يُقرَّر بعد ما
    /// اشتراه»، والفتح عنده يجعل تعطيل أي قدرة قابلاً للالتفاف بألّا يُحفظ ملفّ أصلاً.
    /// </summary>
    public static Error CapabilityProfileMissing(TenantId tenant) => new(
        "sales.capability_profile_missing",
        "لا ملفّ قدرات لهذا المستأجر (" + tenant.Value.ToString("D", CultureInfo.InvariantCulture)
        + ")، والمستند لا يُقبل بلا ملفّ. غياب الملفّ ليس «بلا قيود» بل «لم يُقرَّر بعد ما اشتراه»، "
        + "والفتح عنده يجعل إطفاء أي قدرة قابلاً للالتفاف بترك الملفّ غير محفوظ.",
        "This tenant (" + tenant.Value.ToString("D", CultureInfo.InvariantCulture)
        + ") has no capability profile, and no document is admitted without one. A missing profile does not mean "
        + "'unrestricted'; it means 'what this tenant bought has not been decided yet', and opening the gate there "
        + "would let any disabled capability be bypassed simply by never saving a profile.");

    /// <summary>
    /// قبولٌ لا يغطّي الحقل الذي يمارسه هذا المسار — تذكرة مستند آخر أُعيد استعمالها.
    /// </summary>
    public static Error AdmissionDoesNotCoverField(string documentType, string field) => new(
        "sales.admission_does_not_cover_field",
        "المستند المقبول («" + documentType + "») لا يحمل الحقل «" + field + "» الذي يمارسه هذا المسار. "
        + "قبولٌ نُشئ لمستند آخر ليس قبولاً لهذا المستند.",
        "The admitted document ('" + documentType + "') does not carry the field '" + field
        + "' that this path exercises. An admission issued for another document is not an admission for this one.");

    private static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
