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

    // ═══════════════════════════════════════════════════════════════════════
    // رقم التسجيل الضريبي — شكلٌ يُفرض، ومطابقةٌ لا تُخمّن
    // ═══════════════════════════════════════════════════════════════════════

    public static readonly Error VatNumberEmpty = new(
        "purchasing.supplier.vat_number_empty",
        "رقم التسجيل الضريبي فارغ. المورد بلا رقم يُسجَّل بترك الحقل غير مُسنَد، لا بإرسال فراغ.",
        "The VAT registration number is empty. A supplier without one is recorded by leaving the field unset, not by sending a blank.");

    /// <summary>
    /// محرف غير مرئي داخل الرقم. يُسمّى وحده لأن الطول يبدو صحيحاً والرقم يبدو سليماً،
    /// ورسالة «الطول خطأ» أو «ليست أرقاماً» تُرسل المحاسب إلى الاتجاه الخاطئ.
    /// </summary>
    public static Error VatNumberCarriesInvisibleControl(int length) => new(
        "purchasing.supplier.vat_number_invisible_control",
        "رقم التسجيل الضريبي يحمل محرفاً غير مرئي (تحكّم اتجاهي أو عرض صفر) وطوله "
        + length.ToString(CultureInfo.InvariantCulture) + " محرفاً. المحرف لا يُرى ويُفسد المطابقة، "
        + "ولا يُحذف بصمت: يُعاد إدخال الرقم بخمس عشرة خانة لاتينية.",
        "The VAT registration number carries an invisible character (a bidirectional or zero-width control) and is "
        + length.ToString(CultureInfo.InvariantCulture) + " characters long. It cannot be seen and it breaks matching; "
        + "it is not stripped silently — retype the number as fifteen Latin digits.");

    /// <summary>
    /// رقم يونيكودي ليس ASCII — عربي-هندي أو شرقي أو ديفاناغاري. <b>يُرفض ولا يُحوَّل</b>:
    /// الطرف الآخر من المطابقة رقمٌ مُصدَّق بمحارف لاتينية، وتحويلٌ صامت يجعل المخزَّن
    /// غير ما كتبه الإنسان وغير ما في الرمز معاً.
    /// </summary>
    public static Error VatNumberHasNonAsciiDigits(char offending) => new(
        "purchasing.supplier.vat_number_non_ascii_digits",
        "رقم التسجيل الضريبي يحمل رقماً غير لاتيني: «" + offending + "» (" + CodePoint(offending) + "). "
        + "الأرقام العربية-الهندية والشرقية والديفاناغارية تُرفض ولا تُحوَّل — الرقم المُصدَّق في رمز الفاتورة "
        + "لاتيني، وتحويلٌ صامت يجعل المخزَّن غير المكتوب وغير المُصدَّق معاً. يُعاد الإدخال بأرقام 0–9.",
        "The VAT registration number carries a non-Latin digit: '" + offending + "' (" + CodePoint(offending) + "). "
        + "Arabic-Indic, Eastern Arabic-Indic and Devanagari digits are refused, never converted — the attested number in "
        + "the invoice QR code is Latin, and a silent conversion would make the stored value match neither what was typed "
        + "nor what was attested. Retype it with 0-9.");

    public static Error VatNumberHasNonDigits(char offending) => new(
        "purchasing.supplier.vat_number_not_digits",
        "رقم التسجيل الضريبي يحمل محرفاً ليس رقماً: «" + offending + "» (" + CodePoint(offending) + "). "
        + "الرقم خمس عشرة خانة من 0 إلى 9، بلا فراغ ولا شَرطة ولا بادئة.",
        "The VAT registration number carries a non-digit character: '" + offending + "' (" + CodePoint(offending) + "). "
        + "It is fifteen digits 0-9, with no spaces, dashes or prefix.");

    public static Error VatNumberLength(int length) => new(
        "purchasing.supplier.vat_number_length",
        "رقم التسجيل الضريبي طوله " + length.ToString(CultureInfo.InvariantCulture)
        + " خانة والمطلوب " + SaudiVatNumber.Length.ToString(CultureInfo.InvariantCulture) + " خانة.",
        "The VAT registration number is " + length.ToString(CultureInfo.InvariantCulture)
        + " digits long while " + SaudiVatNumber.Length.ToString(CultureInfo.InvariantCulture) + " are required.");

    public static Error VatNumberPrefix(char first) => new(
        "purchasing.supplier.vat_number_prefix",
        "رقم التسجيل الضريبي يبدأ بـ«" + first + "» والمطلوب أن يبدأ بـ«" + SaudiVatNumber.CountryDigit
        + "» — رمز المملكة في ترقيم دول المجلس.",
        "The VAT registration number starts with '" + first + "' while '" + SaudiVatNumber.CountryDigit
        + "' is required — the Kingdom's country digit in the GCC numbering.");

    public static Error VatNumberSuffix(char last) => new(
        "purchasing.supplier.vat_number_suffix",
        "رقم التسجيل الضريبي ينتهي بـ«" + last + "» والمطلوب أن ينتهي بـ«" + SaudiVatNumber.TaxTypeDigit
        + "» — رمز ضريبة القيمة المضافة.",
        "The VAT registration number ends with '" + last + "' while '" + SaudiVatNumber.TaxTypeDigit
        + "' is required — the value added tax type digit.");

    public static Error SupplierVatNumberNotFound(string vatNumber) => new(
        "purchasing.supplier.vat_number_not_found",
        "لا مورد في هذا المستأجر يحمل رقم التسجيل الضريبي " + vatNumber + ".",
        "No supplier in this tenant carries the VAT registration number " + vatNumber + ".");

    /// <summary>
    /// الرقم موجود لكن على موردين موقوفين وحدهم. <b>لا يُقال «غير موجود»</b>: ذلك يدفع
    /// المحاسب إلى إنشاء مورد ثالث بالرقم نفسه، فيتضاعف الغموض الذي نحرسه.
    /// </summary>
    public static Error SupplierVatNumberOnlyInactive(string vatNumber, IReadOnlyList<string> codes) => new(
        "purchasing.supplier.vat_number_only_inactive",
        "رقم التسجيل الضريبي " + vatNumber + " يحمله موردون موقوفون وحدهم (" + string.Join("، ", codes)
        + "). الإسناد التلقائي مرفوض: إمّا يُعاد تفعيل المورد الصحيح، وإمّا يُسند المستند يدوياً.",
        "The VAT registration number " + vatNumber + " is carried only by deactivated suppliers ("
        + string.Join(", ", codes) + "). Automatic attachment is refused: either reactivate the right supplier "
        + "or attach the document by hand.");

    /// <summary>
    /// أكثر من مورد فعّال بالرقم نفسه — <b>وهذا واقع لا خطأ بيانات</b>: مجموعة ضريبية
    /// واحدة تضمّ منشآت عدّة برقم تسجيل واحد. والحارس هنا لا في فهرس فريد: الفهرس الفريد
    /// يمنع تسجيل الواقع، وهذا يمنع الإسناد الخاطئ ويُبقي الواقع مُسجَّلاً.
    /// </summary>
    public static Error SupplierVatNumberAmbiguous(string vatNumber, IReadOnlyList<string> codes) => new(
        "purchasing.supplier.vat_number_ambiguous",
        "رقم التسجيل الضريبي " + vatNumber + " يحمله " + codes.Count.ToString(CultureInfo.InvariantCulture)
        + " موردين فعّالين (" + string.Join("، ", codes) + "). الإسناد التلقائي مرفوض، واختيار أحدهم "
        + "قرار إنسان لا قرار نظام: فاتورة مُصدَّقة تُسند إلى المورد الخطأ أسوأ من فاتورة بلا إسناد.",
        "The VAT registration number " + vatNumber + " is carried by "
        + codes.Count.ToString(CultureInfo.InvariantCulture) + " active suppliers (" + string.Join(", ", codes)
        + "). Automatic attachment is refused; choosing one is a human decision, not a system decision: an attested "
        + "invoice attached to the wrong supplier is worse than an invoice attached to none.");

    // ═══════════════════════════════════════════════════════════════════════
    // القبول مقابل ملفّ قدرات المستأجر (‏ADR-0023 · ADR-0025)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// مستأجر بلا ملفّ قدرات. <b>رفض لا فتح</b>: غياب الملفّ يعني «لم يُقرَّر بعد ما
    /// اشتراه»، والفتح عنده يجعل تعطيل أي قدرة قابلاً للالتفاف بألّا يُحفظ ملفّ أصلاً.
    /// </summary>
    /// <param name="tenant">المستأجر.</param>
    public static Error CapabilityProfileMissing(TenantId tenant) => new(
        "purchasing.capability_profile_missing",
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
    /// <param name="documentType">نوع المستند المقبول.</param>
    /// <param name="field">الحقل الذي يمارسه المسار.</param>
    public static Error AdmissionDoesNotCoverField(string documentType, string field) => new(
        "purchasing.admission_does_not_cover_field",
        "المستند المقبول («" + documentType + "») لا يحمل الحقل «" + field + "» الذي يمارسه هذا المسار. "
        + "قبولٌ نُشئ لمستند آخر ليس قبولاً لهذا المستند.",
        "The admitted document ('" + documentType + "') does not carry the field '" + field
        + "' that this path exercises. An admission issued for another document is not an admission for this one.");

    // ═══════════════════════════════════════════════════════════════════════
    // ترقية الفاتورة الملتقَطة (‏ADR-0024 · ADR-0025)
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// وصل أمر ترقية برقم ضريبي <b>غير مُصدَّق</b>.
    /// <para>
    /// والرفض هنا لا عند البحث: رقمٌ مقروء ضوئياً بثقة 0.94 يُطابق مورداً بعينه فيُنشئ
    /// إسناداً يحمل <b>مظهر التحقّق</b> بلا التحقّق. والمطابقة الآلية امتيازٌ للمُصدَّق
    /// وحده؛ وما عداه يُسند بيد إنسان يقرأ.
    /// </para>
    /// </summary>
    /// <param name="provenance">مصدر الحقل كما وصل، أو «(غائب)».</param>
    public static Error PromotionVatNumberNotAttested(string provenance) => new(
        "purchasing.promotion.vat_number_not_attested",
        "رقم التسجيل الضريبي في أمر الترقية مصدره «" + provenance + "» لا «مُصدَّق». والمطابقة الآلية "
        + "بالمورد لا تجري إلا على رقم كتبه المُصدِر في رمز الفاتورة: رقمٌ مقروء ضوئياً يُطابق مورداً "
        + "فيُنشئ إسناداً يحمل مظهر التحقّق بلا التحقّق. يُسند المستند بيد إنسان.",
        "The VAT registration number in the promotion order has provenance '" + provenance
        + "', not 'attested'. Automatic supplier matching runs only on a number written by the issuer inside the "
        + "invoice code: an optically read number would produce an attachment that carries the appearance of "
        + "verification without the verification. Attach the document by hand.");

    /// <summary>
    /// وصل تصنيف مصروف لم يكتبه إنسان. التصنيف مؤهّل دور يختار حساباً، ولا يقترحه نموذج.
    /// </summary>
    /// <param name="provenance">مصدر الحقل كما وصل.</param>
    public static Error PromotionExpenseCategoryNotTyped(string provenance) => new(
        "purchasing.promotion.expense_category_not_typed",
        "تصنيف المصروف في أمر الترقية مصدره «" + provenance + "» لا «مكتوب بيد إنسان». والتصنيف مؤهّل "
        + "دور يختار حساب المصروف، وليس في مفردات النموذج المغلقة مؤهّلات أصلاً — فتصنيفٌ «مقترَح» سلسلة "
        + "حرّة تبلغ خريطة الأدوار بلا فحص. يُترك فارغاً فيُرحَّل بالمؤهّل العام، أو يكتبه إنسان.",
        "The expense category in the promotion order has provenance '" + provenance + "', not human-typed. The "
        + "category is a role qualifier that selects the expense account, and the model's closed vocabulary holds "
        + "no qualifiers at all — so a 'suggested' category is a free string reaching the role map unchecked. "
        + "Leave it empty to post under the wildcard qualifier, or have a human type it.");

    /// <summary>
    /// ما تحسبه الوحدة من السطور يخالف الأرقام التي حملها أمر الترقية.
    /// <para>
    /// <b>ولا يُكتب المحسوب فوق المُصدَّق:</b> الرمز يحمل إجمالياً كتبه المُصدِر، والوحدة
    /// تحسب من السطور بتقريب على مستوى السطر. واختلافٌ بهللة واحدة يُنتج فاتورة تخالف ما
    /// وقّعه المورد، ولا يظهر ذلك إلا عند مطابقة كشف حسابه بعد أشهر.
    /// </para>
    /// </summary>
    /// <param name="computedNet">الصافي المحسوب.</param>
    /// <param name="attestedNet">الصافي في الأمر.</param>
    /// <param name="computedTax">الضريبة المحسوبة.</param>
    /// <param name="attestedTax">الضريبة في الأمر.</param>
    /// <param name="computedGross">الإجمالي المحسوب.</param>
    /// <param name="attestedGross">الإجمالي في الأمر.</param>
    public static Error PromotionTotalsDisagreeWithAttested(
        decimal computedNet,
        decimal attestedNet,
        decimal computedTax,
        decimal attestedTax,
        decimal computedGross,
        decimal attestedGross) => new(
        "purchasing.promotion.totals_disagree_with_attested",
        "ما تحسبه وحدة المشتريات من سطور الأمر يخالف أرقامه: الصافي " + Format(computedNet) + " مقابل "
        + Format(attestedNet) + "، والضريبة " + Format(computedTax) + " مقابل " + Format(attestedTax)
        + "، والإجمالي " + Format(computedGross) + " مقابل " + Format(attestedGross)
        + ". ولا يُكتب رقم محسوب فوق رقم مُصدَّق: تُصحَّح السطور أو يُسجَّل المستند يدوياً.",
        "What purchasing computes from the order's lines disagrees with its figures: net " + Format(computedNet)
        + " against " + Format(attestedNet) + ", VAT " + Format(computedTax) + " against " + Format(attestedTax)
        + ", gross " + Format(computedGross) + " against " + Format(attestedGross)
        + ". A computed figure is never written over an attested one: correct the lines or enter the document by hand.");

    /// <summary>الحدث الذي اقترحه النموذج ليس حدث فاتورة مورد — الترقية لا تخترع مساراً.</summary>
    /// <param name="eventCode">الرمز كما وصل.</param>
    /// <param name="expected">الرمز المتوقَّع.</param>
    public static Error PromotionEventNotASupplierBill(string eventCode, string expected) => new(
        "purchasing.promotion.event_is_not_a_supplier_bill",
        "أمر الترقية يحمل الحدث «" + eventCode + "» ووحدة المشتريات لا تُنشئ من الترقية إلا فاتورة "
        + "مورد مصروفية بالحدث «" + expected + "». والفاتورة المخزنية تحتاج استلاماً وأمر شراء، "
        + "ولا تُخترع لها أضلاع من صورة فاتورة.",
        "The promotion order carries the event '" + eventCode + "', while promotion into purchasing creates only "
        + "an expense supplier bill on '" + expected + "'. A stock bill needs a goods receipt and a purchase "
        + "order, and those sides are never invented from a photograph of an invoice.");

    /// <summary>يعرض نقطة الشفرة — المحرف وحده لا يُميّز «٥» عن «5» في سجلّ نصّي.</summary>
    private static string CodePoint(char value)
        => "U+" + ((int)value).ToString("X4", CultureInfo.InvariantCulture);

    private static string Format(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);
}
