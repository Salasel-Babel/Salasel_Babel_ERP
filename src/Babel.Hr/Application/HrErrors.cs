using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Hr.Application;

/// <summary>
/// أخطاء وحدة الموارد البشرية. كل خطأ برمز ثابت ورسالتين — الرفض يُقرأ في تدقيق،
/// و<b>الرسالة تُسمّي البند المعلَّق حين يكون الرفض بسبب قرارٍ لم يُحسم</b>.
/// </summary>
internal static class HrErrors
{
    /// <summary>
    /// <b>الرفض الحاكم في هذه الوحدة</b>: مسيّرٌ لفترةٍ لا يغطّيها صفُّ نِسَبٍ معتمد.
    /// <para>
    /// ولا قيمة افتراضية واحدة، ولا صفر صامت: نسبة الاشتراك وحدَّا الأجر الخاضع
    /// <b>غير متحقَّق منهما</b> (البند م-14)، ولا يُكتب واحدٌ منها في شيفرة ولا في
    /// اختبار — فرقمٌ في اختبار يُنسخ إلى إنتاج بعد شهرين. والوقاية الوحيدة جدولٌ
    /// فارغ يرفض الترحيل برسالة تسمّي البند.
    /// </para>
    /// </summary>
    /// <param name="classCode">تصنيف الاشتراك المطلوب.</param>
    /// <param name="on">التاريخ الذي طُلبت له النِّسَب.</param>
    public static Error PayrollSettingsMissing(string classCode, DateOnly on) => new(
        "hr.payroll_settings_missing",
        "لا صفَّ نِسَبٍ معتمداً يغطّي التصنيف «" + classCode + "» في " + Date(on)
        + ". ونسبةُ اشتراك التأمينات وحدَّا الأجر الخاضع **غير محسومَين** — البند م-14 في "
        + "docs/evidence/verification-debt.md — ولا يُخترع منها شيء هنا ولا يُكتب في شيفرة. "
        + "أودِع إصداراً في hr.payroll_settings بمصدره ومعتمِده وتاريخ سريانه، ثم أعد المحاولة.",
        "No approved rate row covers class '" + classCode + "' on " + Date(on)
        + ". The social insurance contribution rate and the contributory wage floor and ceiling are **undecided** — "
        + "item م-14 in docs/evidence/verification-debt.md — and none of them is invented here or written in code. "
        + "Deposit a version in hr.payroll_settings with its source, its approver, and its effective date, then retry.");

    /// <summary>مسيّر ثانٍ لفترةٍ لها مسيّر قائم — والمنع في الخدمة لا في فهرس.</summary>
    /// <param name="periodCode">رمز الفترة.</param>
    /// <param name="existing">رقم المسيّر القائم.</param>
    public static Error PeriodAlreadyHasARun(string periodCode, string existing) => new(
        "hr.period_already_has_a_run",
        "للفترة " + periodCode + " مسيّرٌ قائم بالرقم " + existing
        + ". و«هل يُسمح بأكثر من مسيّر مُرحَّل للفترة الواحدة؟» سؤالٌ مفتوح على المالك "
        + "(مسيّر خارج الدورة · مكافآت · دفعة تصحيحية)، فالمنع اليوم في الخدمة ولا فهرس "
        + "على الفترة يفترض جوابه في مفتاح.",
        "Period " + periodCode + " already has run " + existing
        + ". Whether more than one posted run per period is allowed is an open owner question "
        + "(off-cycle, bonus, corrective), so the prohibition lives in the service today and no index "
        + "on the period assumes its answer in a key.");

    /// <summary>مستند غير موجود داخل هذه المنشأة.</summary>
    /// <param name="type">نوع المستند.</param>
    /// <param name="id">معرّفه.</param>
    public static Error DocumentNotFound(string type, Guid id) => new(
        "hr.document_not_found",
        "لا مستند " + type + " بهذا المعرّف: " + Id(id),
        "No " + type + " document with this identifier: " + Id(id));

    /// <summary>موظف غير موجود داخل هذه المنشأة.</summary>
    /// <param name="id">المعرّف.</param>
    public static Error EmployeeNotFound(Guid id) => new(
        "hr.employee_not_found",
        "لا موظف بهذا المعرّف: " + Id(id),
        "No employee with this identifier: " + Id(id));

    /// <summary>علاقة عمل غير موجودة.</summary>
    /// <param name="id">المعرّف.</param>
    public static Error EmploymentNotFound(Guid id) => new(
        "hr.employment_not_found",
        "لا علاقة عمل بهذا المعرّف: " + Id(id),
        "No employment with this identifier: " + Id(id));

    /// <summary>مكوّن أجر غير معرَّف — ولا يُخترع تصنيفه.</summary>
    /// <param name="code">الرمز.</param>
    public static Error PayComponentNotFound(string code) => new(
        "hr.pay_component_not_found",
        "لا مكوّن أجر بالرمز «" + code + "». ووسمُ دخوله وعاء الاشتراك يملؤه المحاسب، فلا يُخترع هنا.",
        "No pay component with code '" + code + "'. Its contributory-wage flag is filled by the accountant and is never invented here.");

    /// <summary>رقم مستند مستعمل من قبل داخل هذه المنشأة.</summary>
    /// <param name="number">الرقم.</param>
    public static Error DuplicateNumber(string number) => new(
        "hr.duplicate_number",
        "رقم مستند مستعمل من قبل: " + number,
        "Document number already used: " + number);

    /// <summary>حالة المستند لا تسمح بهذا الفعل.</summary>
    /// <param name="number">رقم المستند.</param>
    /// <param name="state">حالته.</param>
    /// <param name="expected">الحالة المطلوبة.</param>
    public static Error NotInState(string number, string state, string expected) => new(
        "hr.wrong_state",
        "المستند " + number + " في الحالة " + state + " والمطلوب " + expected + ".",
        "Document " + number + " is in state " + state + " while " + expected + " is required.");

    /// <summary>المستند المُرحَّل لا يُعدَّل ولا يُحذف.</summary>
    public static readonly Error PostedDocumentIsNotEdited = new(
        "hr.posted_document_is_immutable",
        "المستند المُرحَّل لا يُعدَّل ولا يُحذف. التصحيح عكسٌ ثم جيلٌ ثانٍ (ADR-0002 · ADR-0003).",
        "A posted document is never edited or deleted; correction is a reversal followed by a second generation (ADR-0002, ADR-0003).");

    /// <summary>مبلغ سالب على مستند.</summary>
    public static readonly Error NegativeAmount = new(
        "hr.negative_amount",
        "مبلغ سالب على مستند. الاتجاه يُعبَّر عنه بنوع المستند لا بإشارة المبلغ.",
        "A negative amount on a document; direction is expressed by the document type, not by the sign.");

    /// <summary>مستند بلا سطور.</summary>
    public static readonly Error NoLines = new(
        "hr.no_lines",
        "مستند بلا سطور لا يُصدَر.",
        "A document without lines is not issued.");

    /// <summary>مسيّر بلا قسيمة واحدة — ولا يُصدَر.</summary>
    public static readonly Error NoPayslips = new(
        "hr.no_payslips",
        "لا علاقة عمل سارية تدخل هذا المسيّر، فلا قسيمة تُبنى. ومسيّرٌ بلا قسيمة لا يُصدَر.",
        "No active employment enters this run, so no payslip is built. A run without a payslip is not issued.");

    /// <summary>
    /// <b>طرف الخزينة غائب على مستند دفع.</b>
    /// <para>
    /// سطر التسوية معلَنٌ في المصفوفة <c>subledger: "resolved"</c>، <b>والمحرك يطويه إلى
    /// <c>none</c></b> ثم يبحث عن الواقعة <c>subledger.none</c>. وحساب التسوية الافتراضي
    /// حسابٌ ضابط، فبلا الواقعة يُرفض كل نداء بـ<c>ledger.posting.missing_subledger</c>
    /// — رفضٌ يقع عند الدفتر بعد أن كُتب صفّ محاولة، والرفض هنا أرحم وأوضح.
    /// </para>
    /// </summary>
    /// <param name="number">رقم المستند.</param>
    public static Error TreasuryPartyMissing(string number) => new(
        "hr.treasury_party_missing",
        "المستند " + number + " بلا طرف خزينة. وسطر التسوية يُحلّ طرفه من واقعة subledger.none، "
        + "وحسابُ التسوية حسابٌ ضابط — فبلا الطرف يرفض المحرك الترحيل كلّه.",
        "Document " + number + " carries no treasury party. The settlement line resolves its party from the "
        + "subledger.none fact, and the settlement account is a control account — without the party the engine "
        + "refuses the whole posting.");

    /// <summary>طريقة تسوية غير معروفة — مؤهّل دور لا يعرفه الدليل يختار حساباً آخر بصمت.</summary>
    /// <param name="method">ما وصل.</param>
    /// <param name="known">المؤهّلات المعروفة.</param>
    public static Error UnknownSettlementMethod(string method, IReadOnlyList<string> known) => new(
        "hr.unknown_settlement_method",
        "طريقة تسوية غير معروفة: «" + method + "». والمعروف: " + string.Join(" · ", known)
        + ". ومؤهّلٌ لا تعرفه خريطة الأدوار يقع على المؤهّل الافتراضي فيختار حساباً آخر بصمت.",
        "Unknown settlement method: '" + method + "'. Known: " + string.Join(" · ", known)
        + ". A qualifier the role map does not know falls back to the default qualifier and silently selects another account.");

    /// <summary>عملة غير عملة المنشأة على مستند رواتب.</summary>
    /// <param name="expected">عملة المنشأة.</param>
    /// <param name="found">ما وصل.</param>
    /// <param name="field">الحقل.</param>
    public static Error CurrencyMismatch(CurrencyCode expected, CurrencyCode found, string field) => new(
        "hr.currency_mismatch",
        "الرواتب تُرحَّل بعملة المنشأة " + expected.Value + " حصراً، والحقل «" + field + "» بعملة "
        + found.Value + ". وحساب التأمينات المستحقة معلَنٌ بعملة واحدة في الدليل، فأي عملة أخرى يرفضها المخطِّط.",
        "Payroll posts in the company currency " + expected.Value + " only, and the field '" + field
        + "' is in " + found.Value + ". The social insurance payable account is declared single-currency in the chart, "
        + "so any other currency is refused by the planner.");

    /// <summary>مبلغ يتجاوز ما يسعه المقياس القانوني.</summary>
    /// <param name="field">الحقل.</param>
    public static Error AmountOutOfRange(string field) => new(
        "hr.amount_out_of_range",
        "المبلغ في الحقل «" + field + "» خارج مدى المقياس القانوني (‏19,4).",
        "The amount in field '" + field + "' is outside the canonical numeric(19,4) range.");

    /// <summary>نية ترحيل بلا رمز حدث.</summary>
    /// <param name="documentType">نوع المستند.</param>
    /// <param name="documentId">معرّفه.</param>
    public static Error MissingEventCode(string documentType, Guid documentId) => new(
        "hr.posting.missing_event_code",
        "نية ترحيل بلا رمز حدث للمستند " + documentType + "/" + Id(documentId)
        + ". رمز الحدث جزء من هوية الإحكام، ورمزٌ فارغ يجعل حدثين مختلفين هويةً واحدة فيُبتلع الثاني بصمت.",
        "A posting intent without an event code for document " + documentType + "/" + Id(documentId)
        + ". The event code is part of the posting identity; an empty code collapses two different events into one "
        + "identity and the second is swallowed silently.");

    /// <summary>رفض المحرك الطلب — بأسبابه كلّها لا بأوّلها.</summary>
    /// <param name="errors">أخطاء المحرك.</param>
    public static Error PostingRefused(IReadOnlyList<Error> errors) => new(
        "hr.posting_refused",
        "رفض محرك الترحيل الطلب: " + string.Join(" | ", errors.Select(static e => e.MessageAr)),
        "The posting engine refused the request: " + string.Join(" | ", errors.Select(static e => e.MessageEn)));

    /// <summary>تعذّرت قراءة نقطة الضبط — ومطابقةٌ بلا نقطة ضبط ليست مطابقة.</summary>
    /// <param name="errors">الأسباب.</param>
    public static Error ControlPointUnavailable(IReadOnlyList<Error> errors) => new(
        "hr.control_point_unavailable",
        "تعذّرت قراءة نقطة الضبط، والمطابقة بلا نقطة ضبط ليست مطابقة: "
        + string.Join(" | ", errors.Select(static e => e.MessageAr)),
        "The control point could not be read, and a reconciliation without one is not a reconciliation: "
        + string.Join(" | ", errors.Select(static e => e.MessageEn)));

    /// <summary>مخالصة على علاقة عمل ما تزال سارية.</summary>
    /// <param name="employmentId">علاقة العمل.</param>
    public static Error EmploymentNotTerminated(Guid employmentId) => new(
        "hr.employment_not_terminated",
        "علاقة العمل " + Id(employmentId) + " ما تزال سارية، والمخالصة لا تُبنى قبل إنهائها.",
        "Employment " + Id(employmentId) + " is still active; a final settlement is not built before it ends.");

    /// <summary>جدول أقساط لا يساوي مجموعه مبلغ السلفة — ولا يُسوَّى الفارق ضمناً.</summary>
    /// <param name="number">رقم المستند.</param>
    /// <param name="scheduled">مجموع الأقساط.</param>
    /// <param name="total">مبلغ السلفة.</param>
    public static Error OverAllocation(string number, decimal scheduled, decimal total) => new(
        "hr.schedule_does_not_match",
        "جدول أقساط " + number + " مجموعه " + Amount(scheduled) + " ومبلغ السلفة " + Amount(total)
        + ". والفارق لا يُسوَّى ضمناً: قسطٌ يُخترع أو يُقصّ يجعل رصيد السلفة رقماً لا يقابله جدول.",
        "The instalment schedule of " + number + " totals " + Amount(scheduled) + " against an advance of "
        + Amount(total) + ". The difference is never settled implicitly: an invented or truncated instalment makes "
        + "the advance balance a number with no schedule behind it.");

    /// <summary>قسط أو استقطاع استُهلك من قبل.</summary>
    /// <param name="what">ما استُهلك.</param>
    public static Error AlreadyConsumed(string what) => new(
        "hr.already_consumed",
        what + " استُهلك في قسيمة سابقة، ولا يُستقطع مرّتين.",
        what + " was already consumed by an earlier payslip and is never deducted twice.");

    private static string Amount(decimal value) => value.ToString("0.0000", CultureInfo.InvariantCulture);

    private static string Date(DateOnly value) => value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);
}
