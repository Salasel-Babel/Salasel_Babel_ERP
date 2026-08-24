using Babel.SharedKernel;

namespace Babel.Ledger.Posting;

/// <summary>
/// أخطاء الترحيل. كل خطأ برسالتين — عربية وإنجليزية — ورمز ثابت يصلح للتسجيل
/// وللمطابقة في الاختبارات.
/// <para>
/// ولا يوجد هنا خطأ واحد معناه «تابع بقيمة افتراضية». في المسار المالي يُختار
/// الفشل الصاخب دائماً: الرقم الخاطئ الصامت يُكتشف عند التدقيق بعد شهور، وكلفته
/// إعادة بناء بيانات تاريخية وفقدان ثقة (<c>traps.md</c> §0).
/// </para>
/// </summary>
internal static class PostingErrors
{
    public static Error Invalid(string code, string ar, string en) => new("ledger.posting." + code, ar, en);

    public static Error MissingIdempotencyKey => Invalid(
        "missing_idempotency_key",
        "طلب الترحيل بلا مفتاح حصانة ضد التكرار — والتسليم مرة واحدة على الأقل يعني وصولاً مكرّراً حتماً.",
        "The posting request carries no idempotency key; at-least-once delivery guarantees a duplicate arrival.");

    public static Error MissingTenant => Invalid(
        "missing_tenant",
        "طلب الترحيل بلا مستأجر.",
        "The posting request carries no tenant.");

    public static Error NoLines => Invalid(
        "no_lines",
        "طلب الترحيل بلا سطور وبلا حدث في المصفوفة — لا مصدر للقيد.",
        "The posting request has neither lines nor a matrix event; there is no source for the entry.");

    public static Error UnknownEvent(string code) => Invalid(
        "unknown_event",
        $"الحدث «{code}» ليس في مصفوفة الترحيل. الوحدة تسمّي حدثاً معرَّفاً، ولا تخترع رمزاً.",
        $"Event '{code}' is not in the posting matrix. A module names a defined event; it does not invent a code.");

    public static Error EventPostsNoEntry(string code) => Invalid(
        "event_posts_no_entry",
        $"الحدث «{code}» مُعلَن بـ posts_entry = false: هذا بيان سياسة محاسبية لا إغفال، ولا يُولَّد منه قيد.",
        $"Event '{code}' declares posts_entry = false: that is a deliberate accounting policy statement, not an omission; no entry is generated.");

    public static Error UnsupportedLineKind(string code, int lineNo, string kind) => Invalid(
        "unsupported_line_kind",
        $"السطر {lineNo} في الحدث «{code}» من النوع «{kind}»، وهو يحتاج تعداد حسابات أو ملفاً مستورداً "
        + "لا يُشتق من الطلب. الترحيل مرفوض ولا يُولَّد قيد ناقص السطور.",
        $"Line {lineNo} of event '{code}' is of kind '{kind}', which needs an account enumeration or an imported file "
        + "that the request does not carry. The posting is refused rather than producing an entry with missing lines.");

    public static Error UndecidableCondition(string code, int lineNo, string reason) => Invalid(
        "undecidable_condition",
        $"شرط السطر {lineNo} في الحدث «{code}» لا يمكن تقييمه: {reason} "
        + "والشرط غير المُقيَّم لا يُعامل معاملة «خطأ»: قيد ناقص سطراً يمرّ صامتاً، والرفض يُرى.",
        $"The condition on line {lineNo} of event '{code}' cannot be evaluated: {reason} "
        + "An unevaluated condition is not treated as false: an entry missing a line passes silently, a refusal is seen.");

    public static Error UnknownAmount(string code, int lineNo, string variable) => Invalid(
        "unknown_amount",
        $"تعبير مبلغ السطر {lineNo} في الحدث «{code}» يقرأ «{variable}» ولم تُسلَّم قيمته.",
        $"The amount expression on line {lineNo} of event '{code}' reads '{variable}' and no value was supplied.");

    public static Error MissingQualifier(string role, string source) => Invalid(
        "missing_qualifier",
        $"مؤهّل الدور «{role}» يأتي من «{source}» ولم تُسلَّم قيمته — والوقوع على المؤهّل الافتراضي هنا "
        + "يختار حساباً آخر بصمت.",
        $"The qualifier for role '{role}' comes from '{source}' and no value was supplied; falling back to the default "
        + "qualifier here would silently pick a different account.");

    public static Error UnresolvedRole(string role, string qualifier) => Invalid(
        "unresolved_role",
        $"الدور «{role}» بالمؤهّل «{qualifier}» لا يُحلّ إلى حساب في خريطة هذه الشركة. "
        + "لكل دور صفّ بالمؤهّل * إلزاماً، وغيابه يوقف المحرك ولا يجعله يخمّن.",
        $"Role '{role}' with qualifier '{qualifier}' resolves to no account in this company's map. "
        + "Every role must have a '*' row; its absence stops the engine rather than making it guess.");

    public static Error UnknownAccount(string account) => Invalid(
        "unknown_account",
        $"الحساب «{account}» غير موجود في دليل حسابات هذه الشركة.",
        $"Account '{account}' does not exist in this company's chart of accounts.");

    public static Error InactiveAccount(string account) => Invalid(
        "inactive_account",
        $"الحساب «{account}» معطَّل. الحساب لا يُحذف أبداً وعليه حركة، بل يُعطَّل — والترحيل عليه مرفوض.",
        $"Account '{account}' is disabled. An account with movement is never deleted, only disabled — and posting to it is refused.");

    public static Error RollupAccount(string account) => Invalid(
        "guard.GR-COA-001",
        $"يُمنع الترحيل على حساب تجميعي «{account}» — الترحيل على الحساب التفصيلي فقط.",
        $"Posting to rollup account '{account}' is refused — posting is only ever on a detail account.");

    public static Error MissingDimension(string account, string dimension) => Invalid(
        "guard.GR-COA-002",
        $"يُمنع الترحيل على الحساب «{account}» دون بُعده الإلزامي «{dimension}» — "
        + "بُعد ناقص يعني تقرير ربحية ناقصاً لا يُصلَح لاحقاً إلا بعكس القيد.",
        $"Posting to account '{account}' without its mandatory dimension '{dimension}' is refused — "
        + "a missing dimension means an incomplete profitability report that cannot be fixed without reversing the entry.");

    public static Error MissingSubledger(string account, string kind) => Invalid(
        "missing_subledger",
        $"الحساب «{account}» حساب ضابط لدفتر «{kind}» والسطر بلا مرجع طرف — تنكسر المطابقة اليومية.",
        $"Account '{account}' is a control account for the '{kind}' subledger and the line carries no party reference; daily reconciliation breaks.");

    public static Error CurrencyNotAllowed(string account, string currency) => Invalid(
        "currency_not_allowed",
        $"الحساب «{account}» لا يقبل العملة «{currency}» بحكم نمط عملته.",
        $"Account '{account}' does not accept currency '{currency}' under its currency mode.");

    public static Error Guard(string ruleId, string messageAr, string messageEn) => Invalid(
        "guard." + ruleId, messageAr, messageEn);

    public static Error Unbalanced(decimal debit, decimal credit) => Invalid(
        "unbalanced",
        $"القيد غير متوازن بعملة الشركة: مدين {debit.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)} "
        + $"ودائن {credit.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}.",
        $"The entry does not balance in company currency: debit {debit.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)} "
        + $"credit {credit.ToString("0.0000", System.Globalization.CultureInfo.InvariantCulture)}.");

    public static Error TooFewLines(int count) => Invalid(
        "too_few_lines",
        $"القيد يحتاج سطرين على الأقل، ووُجد {count.ToString(System.Globalization.CultureInfo.InvariantCulture)}.",
        $"A journal entry needs at least two lines; found {count.ToString(System.Globalization.CultureInfo.InvariantCulture)}.");

    public static Error NoFiscalPeriod(DateOnly date) => Invalid(
        "no_fiscal_period",
        $"لا فترة مالية تحوي التاريخ {date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.",
        $"No fiscal period contains the date {date.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)}.");

    public static Error ClosedPeriod(string period) => Invalid(
        "closed_period",
        $"الفترة «{period}» مقفلة: الترحيل فيها مرفوض إلا بإذن استثنائي موثَّق يُسجَّل في سجل التدقيق.",
        $"Period '{period}' is closed: posting into it is refused except under a documented exceptional permission recorded in the audit log.");

    public static Error PermanentlyClosedPeriod(string period) => Invalid(
        "permanently_closed_period",
        $"الفترة «{period}» مقفلة نهائياً ولا يفتحها إذن.",
        $"Period '{period}' is permanently closed and no permission reopens it.");

    public static Error EntryNotFound(Guid entryId) => Invalid(
        "entry_not_found",
        $"لا قيد بالمعرّف {entryId}.",
        $"No entry with id {entryId}.");

    public static Error AlreadyReversed(Guid entryId) => Invalid(
        "already_reversed",
        $"القيد {entryId} معكوس من قبل — والعكس مرّة واحدة.",
        $"Entry {entryId} has already been reversed; a reversal happens once.");

    public static Error CannotReverseAReversal(Guid entryId) => Invalid(
        "cannot_reverse_a_reversal",
        $"القيد {entryId} قيد عكس، ولا يُعكس قيد العكس.",
        $"Entry {entryId} is itself a reversal; a reversal is not reversed.");

    public static Error Database(string sqlState, string message) => Invalid(
        "database." + sqlState,
        $"رفضت قاعدة البيانات الترحيل (SQLSTATE {sqlState}): {message}",
        $"The database refused the posting (SQLSTATE {sqlState}): {message}");
}
