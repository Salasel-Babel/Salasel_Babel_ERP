using System.Globalization;
using Babel.SharedKernel;

namespace Babel.RealEstate.Application;

/// <summary>
/// أخطاء وحدة العقارات. كل خطأ برمز ثابت ورسالتين — الرفض يُقرأ في تدقيق.
/// <para>
/// <b>وصنفٌ منها له سببٌ واحد يجمعه: بندٌ معلَّق على قرار مالك.</b> جدول الإعدادات
/// يُبنى فارغاً، وما لا صفَّ إعداداتٍ له يُرفض <b>رفضاً صريحاً يسمّي البند</b> — لا
/// يُملأ بقيمة افتراضية ولا يمرّ بصفر. وقيمةٌ افتراضية في موضع كهذا ليست تسهيلاً بل
/// قرارٌ يتخذه المطوّر باسم المالك ثم لا يعرف أحد أنه اتُّخذ.
/// </para>
/// </summary>
internal static class RealEstateErrors
{
    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    private static string Amount(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

    public static Error PropertyNotFound(Guid id) => new(
        "realestate.property_not_found",
        "لا عقار بهذا المعرّف: " + Id(id),
        "No property with this identifier: " + Id(id));

    public static Error UnitNotFound(Guid id) => new(
        "realestate.unit_not_found",
        "لا وحدة بهذا المعرّف: " + Id(id),
        "No unit with this identifier: " + Id(id));

    public static Error PartyNotFound(string role, Guid id) => new(
        "realestate.party_not_found",
        "لا طرف بدور «" + role + "» بهذا المعرّف: " + Id(id),
        "No party in role '" + role + "' with this identifier: " + Id(id));

    public static Error LeaseNotFound(Guid id) => new(
        "realestate.lease_not_found",
        "لا عقد بهذا المعرّف: " + Id(id),
        "No lease contract with this identifier: " + Id(id));

    public static Error DocumentNotFound(string type, Guid id) => new(
        "realestate.document_not_found",
        "لا مستند " + type + " بهذا المعرّف: " + Id(id),
        "No " + type + " document with this identifier: " + Id(id));

    public static Error DuplicateCode(string code) => new(
        "realestate.duplicate_code",
        "الرمز «" + code + "» مستعمل في هذه المنشأة. والرمز هوية يحملها تاريخٌ مُرحَّل، فلا يُعاد.",
        "Code '" + code + "' is already used in this company. A code is an identity carried by posted history; it is not reused.");

    public static Error UnknownOwnershipModel(string value) => new(
        "realestate.unknown_ownership_model",
        "نموذج ملكية غير معروف «" + value + "». المقبول: own_property أو managed_for_others.",
        "Unknown ownership model '" + value + "'. Accepted: own_property or managed_for_others.");

    /// <summary>
    /// نموذج الإدارة بلا مالك. سطر أمانات الملاك يحمل طرفاً في دفتر
    /// <c>property_owner</c>، وطرفٌ غائب يُرفض من المحرك عند الترحيل — أي بعد أن يكون
    /// المستخدم قد أنشأ عقاراً وعقداً وفاتورة. والرفض هنا أرحم وأصدق.
    /// </summary>
    public static Error ManagedPropertyNeedsAnOwner { get; } = new(
        "realestate.managed_property_needs_an_owner",
        "عقارٌ بنموذج «مُدار لصالح الغير» بلا مالك مسجَّل. الأجرة في هذا النموذج التزام تجاه المالك، "
        + "وسطر أمانات الملاك يحمل طرفاً في دفتره المساعد — فبلا مالكٍ لا يُرحَّل قيدٌ واحد.",
        "A property in the managed_for_others model with no registered owner. Rent under that model is a liability to the "
        + "owner, and the owner trust line carries a party in its subledger — with no owner not one entry can be posted.");

    public static Error OwnedPropertyTakesNoOwner { get; } = new(
        "realestate.owned_property_takes_no_owner",
        "عقارٌ بنموذج الملكية الذاتية لا يحمل مالكاً خارجياً: المنشأة هي المالك، ولا أمانات ملاك عليه.",
        "A property in the own_property model carries no external owner: the company is the owner and it bears no owner trust.");

    /// <summary>
    /// أكثر من مالك على عقار واحد — <b>الشكل يحتمله والقرار لم يُتَّخذ</b>.
    /// <para>
    /// المفتاح رباعي من اليوم فلا هجرة تنتظر، لكن <b>قسمة سطور النموذج المُدار بالحصص</b>
    /// تُدخل سياسة تقريب على كل قسمة وتضيف بُعد المالك إلى مفتاح الفوترة. وهو قرار مالك
    /// مفتوح (ق-ع-18)، ونسبة الحصّة ومقياسها بندُ دَينِ تحقّقٍ لا قرارُ مصمّم.
    /// </para>
    /// </summary>
    public static Error OwnerShareSplitNotDecided { get; } = new(
        "realestate.owner_share_split_not_decided",
        "لهذا العقار أكثر من مالك، وقسمة سطور النموذج المُدار بالحصص بندٌ معلَّق على قرار المالك (ق-ع-18): "
        + "الشكل يحتمل الحصص من اليوم — المفتاح رباعي والحصّة كسر — ولم تُحسم سياسة القسمة ولا تقريبها. "
        + "لا يُرحَّل بقسمةٍ يخترعها النظام.",
        "This property has more than one owner, and splitting managed-model lines by share is a pending owner decision "
        + "(Q-RE-18): the shape carries shares from today — a four-part key and a fractional share — but neither the split "
        + "policy nor its rounding is settled. Nothing is posted on a split the system invents.");

    /// <summary>مجموع الأقساط لا يساوي قيمة العقد — والثابتة نصّ المصفوفة لا اجتهاد.</summary>
    public static Error InstalmentsDoNotSumToTheContract(decimal instalments, decimal contract) => new(
        "realestate.instalments_do_not_sum_to_the_contract",
        "مجموع الأقساط " + Amount(instalments) + " وقيمة العقد " + Amount(contract)
        + ". والثابتة المكتوبة في مصفوفة الترحيل: «مجموع الأقساط = قيمة العقد بالضبط دون هللات ضائعة». "
        + "والنظام لا يوزّع الفرق من عنده: سياسة التقريب — أين يقع فائض الهللات — قرار مالك مفتوح (ق-ع-3).",
        "The instalments sum to " + Amount(instalments) + " and the contract value is " + Amount(contract)
        + ". The invariant written in the posting matrix is: 'the instalments sum exactly to the contract value with no lost "
        + "halalas'. The system does not spread the difference itself: the rounding policy — where the halala surplus lands — "
        + "is an open owner decision (Q-RE-3).");

    /// <summary>عقدٌ بلا أقساط. والتوليد الآلي هو ما يستلزم سياسة التقريب المعلَّقة.</summary>
    public static Error ScheduleIsNotGenerated { get; } = new(
        "realestate.schedule_is_not_generated",
        "العقد بلا أقساط مُصرَّح بها. والنظام لا يولّد جدول الدفعات من قيمة العقد وعدد الأقساط: "
        + "التوزيع يستلزم سياسة تقريب لم يحسمها المالك بعد (ق-ع-3)، والأقساط تصل مصرَّحاً بها بفتراتها.",
        "The lease carries no declared instalments. The system does not generate the payment schedule from a contract value "
        + "and an instalment count: the split requires a rounding policy the owner has not settled (Q-RE-3), so instalments "
        + "arrive declared with their periods.");

    public static Error LeaseIsNotActive(Guid id) => new(
        "realestate.lease_is_not_active",
        "العقد " + Id(id) + " ليس سارياً. والفوترة على عقدٍ لم يُفعَّل تُنشئ ذمّةً بلا مدّة يستند إليها الاعتراف.",
        "Lease " + Id(id) + " is not active. Invoicing an unactivated lease creates a receivable with no term to recognise against.");

    public static Error LeaseIsAlreadyActive(Guid id) => new(
        "realestate.lease_is_already_active",
        "العقد " + Id(id) + " مُفعَّل سلفاً، والتفعيل فعلٌ يقع مرّة: إعادته تولّد جدول دفعات ثانياً.",
        "Lease " + Id(id) + " is already active; activation happens once — repeating it would generate a second schedule.");

    /// <summary>
    /// مدّتان ساريتان متداخلتان على وحدة واحدة — <b>الحكم من قاعدة البيانات</b>.
    /// <para>
    /// و«مدّة واحدة» شرط <b>تقاطع مدى</b> لا شرط تساوٍ، فلا يعبّر عنه فهرس فريد مهما
    /// اتّسع؛ وفحصٌ في الخدمة يقرأ ثم يكتب فيمرّ بينهما نداءٌ آخر.
    /// </para>
    /// </summary>
    public static Error LeaseTermOverlaps(string contractNo) => new(
        "realestate.lease_term_overlaps",
        "مدّة العقد «" + contractNo + "» تتداخل مع مدّة عقدٍ سارٍ آخر على الوحدة نفسها. "
        + "والوحدة لا تُؤجَّر مرّتين في يوم واحد، والحكم من قيد الاستبعاد الزمني في قاعدة البيانات "
        + "لا من فحصٍ في الخدمة يمرّ بينه وبين الكتابة نداءٌ آخر.",
        "The term of lease '" + contractNo + "' overlaps a live lease on the same unit. A unit is not let twice on the "
        + "same day, and the verdict comes from a temporal exclusion constraint in the database rather than from a service "
        + "check with a window between it and the write.");

    public static Error ScheduleLineNotFound(Guid leaseId, Guid lineId) => new(
        "realestate.schedule_line_not_found",
        "لا قسط بالمعرّف " + Id(lineId) + " تحت العقد " + Id(leaseId) + ".",
        "No instalment with identifier " + Id(lineId) + " under lease " + Id(leaseId) + ".");

    public static Error ScheduleLineAlreadyInvoiced(Guid lineId) => new(
        "realestate.schedule_line_already_invoiced",
        "القسط " + Id(lineId) + " مفوترٌ سلفاً. وفاتورتان على قسطٍ واحد تُنتجان ذمّةً مضاعفة على المستأجر.",
        "Instalment " + Id(lineId) + " is already invoiced. Two invoices for one instalment double the tenant receivable.");

    public static Error InvoiceHasNoLines { get; } = new(
        "realestate.invoice_has_no_lines",
        "فاتورة بلا قسط واحد. والفاتورة الفارغة قيدٌ بلا مبلغ.",
        "An invoice with not one instalment. An empty invoice is an entry with no amount.");

    public static Error DocumentIsNotADraft(string type, Guid id) => new(
        "realestate.document_is_not_a_draft",
        "المستند " + type + " " + Id(id) + " ليس مسوّدة، والمستند المُرحَّل واقعةٌ لا تُعاد.",
        "Document " + type + " " + Id(id) + " is not a draft, and a posted document is a fact that is not replayed.");

    public static Error ReceiptIsNotPosted(Guid id) => new(
        "realestate.receipt_is_not_posted",
        "سند القبض " + Id(id) + " لم يُرحَّل بعد، والتخصيص قيدٌ مستقلّ يقع بعد الترحيل لا قبله.",
        "Receipt " + Id(id) + " is not posted yet; allocation is a separate entry that follows posting, never precedes it.");

    public static Error ReceiptIsAlreadyAllocated(Guid id) => new(
        "realestate.receipt_is_already_allocated",
        "سند القبض " + Id(id) + " مخصَّص سلفاً، والتخصيص يقع مرّة.",
        "Receipt " + Id(id) + " is already allocated; allocation happens once.");

    /// <summary>تخصيص سندٍ رُحّل بحدث «مقبوض من مستأجر معلوم» — لا شيء يُنقل.</summary>
    public static Error ReceiptWasNotUnallocated(Guid id) => new(
        "realestate.receipt_was_not_unallocated",
        "سند القبض " + Id(id) + " رُحّل على مستأجر معلوم، فلا رصيد غير مخصَّص يُنقل. "
        + "والتخصيص حدثٌ ينقل من حساب التحصيلات غير المخصَّصة إلى ذمم المستأجرين.",
        "Receipt " + Id(id) + " was posted against a known tenant, so there is no unallocated balance to move. "
        + "Allocation is the event that moves from unallocated collections to the tenant receivable.");

    public static Error AllocationNeedsALessee { get; } = new(
        "realestate.allocation_needs_a_lessee",
        "التخصيص يحتاج مستأجراً — والسند الذي لا يُعرف صاحبه لا يُخصَّص بتخمين.",
        "Allocation needs a tenant — a receipt whose owner is unknown is not allocated by guesswork.");

    public static Error MissingEventCode(string documentType, Guid documentId) => new(
        "realestate.missing_event_code",
        "محاولة ترحيل بلا رمز حدث على المستند " + documentType + "/" + Id(documentId)
        + ". ورمزٌ فارغ يجعل حدثين هويةً واحدة فيُبتلع الثاني بصمت.",
        "A posting attempt with no event code on document " + documentType + "/" + Id(documentId)
        + ". An empty code makes two events one identity and the second is swallowed silently.");

    /// <summary>رفض المحرك — يُنقل بنصّه ولا يُترجَم إلى «فشل داخلي».</summary>
    public static IReadOnlyList<Error> PostingRefused(IReadOnlyList<Error> errors)
    {
        ArgumentNullException.ThrowIfNull(errors);
        return errors;
    }
}
