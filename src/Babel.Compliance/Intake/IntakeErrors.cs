using Babel.SharedKernel;

namespace Babel.Compliance.Intake;

/// <summary>
/// أخطاء بوّابة الاستقبال. <b>كل رفض هنا رفضٌ مُعلَن</b> برمز ثابت ورسالتين — عربية
/// وإنجليزية — ولا يوجد في هذا المسار مسار «لا شيء يحدث بصمت».
/// <para>Intake refusals. Every one carries a stable code and both languages.</para>
/// </summary>
public static class IntakeErrors
{
    /// <summary>المستند لم يُرحَّل: لا قيد. هذا رفض بنيوي لا نقص بيانات.</summary>
    public static Error NotPosted { get; } = new(
        "compliance.intake.not_posted",
        "المستند لم يُرحَّل: لا قيد محاسبي مصاحب. الترحيل يسبق الالتزام دائماً، ولا يُبنى مستند التزام لشيء خارج الدفتر.",
        "the document is not posted: no journal entry accompanies it. Posting always precedes compliance; no compliance document is built for anything outside the ledger.");

    /// <summary>وحدة الإصدار فارغة. لا سلسلة بلا وحدة إصدار.</summary>
    public static Error IssuingUnitMissing { get; } = new(
        "compliance.intake.issuing_unit_missing",
        "وحدة الإصدار غير محدّدة. العدّاد والسلسلة والشهادة كلها بنطاق وحدة الإصدار، فلا مستند بلا وحدة.",
        "the issuing unit is not specified. Counter, chain and certificate are all scoped to the issuing unit, so no document exists without one.");

    /// <summary>هوية مستند المصدر ناقصة — وهي مفتاح الحصانة.</summary>
    public static Error SourceIdentityMissing { get; } = new(
        "compliance.intake.source_identity_missing",
        "هوية مستند المصدر ناقصة (النوع أو المعرّف). وهي مفتاح الحصانة: بدونها يُنتج النداء المكرّر مستنداً مكرّراً.",
        "the source document identity is incomplete (type or id). It is the idempotency key: without it a repeated call produces a duplicate document.");

    /// <summary>رقم المستند فارغ.</summary>
    public static Error DocumentNumberMissing { get; } = new(
        "compliance.intake.document_number_missing",
        "رقم المستند فارغ. الرقم يظهر للمشتري وعلى المستند المُرسَل، فلا يجوز أن يكون فارغاً.",
        "the document number is empty. It appears to the buyer and on the submitted document, so it may not be empty.");

    /// <summary>مستند بلا سطور.</summary>
    public static Error NoLines { get; } = new(
        "compliance.intake.no_lines",
        "مستند بلا سطور. لا يُبنى مستند خاضع للضريبة من إجماليات وحدها.",
        "a document with no lines. A taxable document is not built from totals alone.");

    /// <summary>عملة سطر تخالف عملة المستند.</summary>
    public static Error CurrencyMismatch { get; } = new(
        "compliance.intake.currency_mismatch",
        "عملة أحد المبالغ تخالف عملة إجمالي المستند. المستند بعملة واحدة، وخلط العملات في مستند واحد يُنتج إجمالياً بلا معنى.",
        "one of the amounts is in a currency other than the document total's. A document carries one currency; mixing them yields a meaningless total.");

    /// <summary>الصافي والضريبة لا يساويان الإجمالي.</summary>
    public static Error TotalsInconsistent { get; } = new(
        "compliance.intake.totals_inconsistent",
        "إجماليات المستند غير متسقة: الصافي زائد الضريبة لا يساوي الإجمالي. هذا فحص داخلي بحت ولا علاقة له بقواعد الجهة.",
        "the document totals are inconsistent: net plus tax does not equal gross. This is a purely internal check and has nothing to do with authority rules.");

    /// <summary>مجموع السطور لا يساوي الإجماليات.</summary>
    public static Error LinesDoNotSum { get; } = new(
        "compliance.intake.lines_do_not_sum",
        "مجموع السطور لا يساوي إجماليات المستند. التقريب قرار محاسبي يقع في وحدة المصدر قبل هذا الحدّ، لا داخله.",
        "the lines do not sum to the document totals. Rounding is an accounting decision taken in the originating module before this boundary, never inside it.");

    /// <summary>إشعار تصحيحي بلا مستند أصلي أو بلا سبب.</summary>
    public static Error CorrectionIncomplete { get; } = new(
        "compliance.intake.correction_incomplete",
        "إشعار دائن أو مدين بلا مستند أصلي أو بلا سبب تصحيح بالعربية والإنجليزية. الإشعار يشير إلى ما يصحّحه دائماً.",
        "a credit or debit note without an original document or without a correction reason in both Arabic and English. A note always names what it corrects.");

    /// <summary>مستند عادي يحمل بيانات تصحيح.</summary>
    public static Error CorrectionOnPlainInvoice { get; } = new(
        "compliance.intake.correction_on_plain_invoice",
        "فاتورة عادية تحمل مستنداً أصلياً أو سبب تصحيح. الفاتورة لا تصحّح شيئاً؛ التصحيح إشعار دائن أو مدين.",
        "a plain invoice carrying an original document or a correction reason. An invoice corrects nothing; a correction is a credit or debit note.");

    /// <summary>المستأجر غير مخصّص.</summary>
    public static Error TenantMissing { get; } = new(
        "compliance.intake.tenant_missing",
        "المستأجر غير مخصّص. العزل بالمستأجر شرط لكل شيء في هذا الحدّ.",
        "the tenant is not assigned. Tenant isolation is a precondition for everything in this boundary.");

    /// <summary>وحدة الإصدار غير مسجّلة أو غير قادرة على الإصدار.</summary>
    public static Error IssuingUnitNotReady { get; } = new(
        "compliance.intake.issuing_unit_not_ready",
        "وحدة الإصدار غير مسجّلة لدى الجهة أو لم تكتمل دورة تسجيلها، فلا تستطيع إصدار مستند.",
        "the issuing unit is not registered with the authority, or its onboarding is incomplete, so it cannot issue a document.");

    /// <summary>المزوّد لا يدعم المسار الذي تختاره السياسة لهذا المستند.</summary>
    public static Error FlowNotSupported { get; } = new(
        "compliance.intake.flow_not_supported",
        "المزوّد المركَّب لا يدعم المسار الذي تختاره السياسة لهذا المستند. هذا عطل تركيب لا عطل بيانات.",
        "the composed provider does not support the flow the policy selects for this document. This is a composition fault, not a data fault.");
}
