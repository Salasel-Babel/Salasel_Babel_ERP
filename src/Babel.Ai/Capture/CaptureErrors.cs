using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Ai.Capture;

/// <summary>
/// أخطاء الالتقاط. كل خطأ برمز ثابت ورسالتين، على اصطلاح <see cref="Error"/> القائم في
/// المستودع: <b>الرمز</b> هو ما تعتمد عليه الشيفرة، والنصّان للعرض.
/// <para>
/// <b>والرفض هنا لا التطبيع.</b> نفس مبدأ حدّ HTTP مع المال: مُخرَجٌ مشوَّه من مزوّد
/// يُرفض بصوت عالٍ ويُسمّى ما فيه، ولا يُصلَح بهدوء فيُنتج مسوّدةً معقولة وخاطئة.
/// </para>
/// </summary>
public static class CaptureErrors
{
    /// <summary>الحمولة ليست JSON صالحاً.</summary>
    public static Error PayloadNotJson(string detail) => new(
        "ai.capture.payload_not_json",
        "مُخرَج المزوّد ليس JSON صالحاً: " + detail,
        "The provider output is not valid JSON: " + detail);

    /// <summary>الجذر ليس كائناً.</summary>
    public static readonly Error PayloadNotAnObject = new(
        "ai.capture.payload_not_an_object",
        "مُخرَج المزوّد ليس كائناً في جذره — المخطط يتوقّع كائناً واحداً.",
        "The provider output root is not an object; the schema expects a single object.");

    /// <summary>إصدار مخطط غير معروف.</summary>
    public static Error SchemaVersionUnknown(string found, string expected) => new(
        "ai.capture.schema_version_unknown",
        "إصدار المخطط «" + found + "» غير معروف، والمتوقّع «" + expected + "». "
        + "مخرَجٌ بإصدار آخر قد تختلف دلالة حقوله بلا أن يتغيّر شكلها.",
        "Schema version '" + found + "' is unknown; '" + expected + "' was expected.");

    /// <summary>حقل لا يعرفه المخطط.</summary>
    public static Error UnknownField(string section, string field) => new(
        "ai.capture.unknown_field",
        "حقل لا يعرفه المخطط في «" + section + "»: «" + field + "». "
        + "الحقل المجهول يُرفض ولا يُتجاهل: تجاهله يُخفي تغيّراً في مُخرَج المزوّد.",
        "Unknown field in '" + section + "': '" + field + "'. Unknown fields are refused, not ignored.");

    /// <summary>
    /// حقل يسمّي حساباً. مرفوض برمز مستقلّ لا برمز «حقل مجهول»، لأن هذا ليس انحرافاً
    /// في المخطط بل <b>محاولة لتسمية حساب من خارج المصفوفة</b>.
    /// </summary>
    public static Error FieldNamesLedgerCode(string section, string field) => new(
        "ai.capture.field_names_a_ledger_code",
        "الحقل «" + field + "» في «" + section + "» يسمّي حساباً. "
        + "النموذج لا يرى دليل الحسابات ولا يسمّي حساباً: يسمّي حدثاً أو دوراً، والمصفوفة تحلّه إلى حساب هذا المستأجر.",
        "Field '" + field + "' in '" + section + "' names a ledger code. The model names an event or a role; the matrix resolves it.");

    /// <summary>حقل إلزامي غائب.</summary>
    public static Error MissingField(string section, string field) => new(
        "ai.capture.missing_field",
        "حقل إلزامي غائب في «" + section + "»: «" + field + "».",
        "A mandatory field is missing in '" + section + "': '" + field + "'.");

    /// <summary>نوع JSON خاطئ.</summary>
    public static Error WrongJsonKind(string field, string expected, string found) => new(
        "ai.capture.field_wrong_json_kind",
        "الحقل «" + field + "» مطلوب " + expected + " وورد " + found + ".",
        "Field '" + field + "' must be " + expected + " but was " + found + ".");

    /// <summary>قيمة لا تُقرأ عدداً عشرياً بثقافة ثابتة.</summary>
    public static Error NotADecimal(string field, string value) => new(
        "ai.capture.field_not_a_decimal",
        "الحقل «" + field + "» ليس عدداً عشرياً بثقافة ثابتة: «" + value + "». "
        + "المال نصّ على الحدّ ويُقرأ بثقافة ثابتة — والفاصلة العربية أو فاصل الآلاف يُنتجان رقماً آخر.",
        "Field '" + field + "' is not an invariant decimal: '" + value + "'.");

    /// <summary>درجة ثقة خارج المدى.</summary>
    public static Error ConfidenceOutOfRange(string field, decimal value) => new(
        "ai.capture.confidence_out_of_range",
        "درجة الثقة على «" + field + "» تساوي " + Show(value) + " وهي خارج المدى [0، 1].",
        "The confidence on '" + field + "' is " + Show(value) + ", outside [0, 1].");

    /// <summary>تاريخ لا يطابق الصيغة الميلادية الموحّدة.</summary>
    public static Error DateNotIso(string field, string value) => new(
        "ai.capture.date_not_iso",
        "الحقل «" + field + "» لا يطابق «yyyy-MM-dd» ميلادياً: «" + value + "». "
        + "والتقويم الميلادي هو تقويم السنة المالية، والهجري عرضٌ فقط.",
        "Field '" + field + "' does not match the Gregorian 'yyyy-MM-dd': '" + value + "'.");

    /// <summary>رمز عملة غير مقبول.</summary>
    public static Error CurrencyNotAcceptable(string value) => new(
        "ai.capture.currency_not_acceptable",
        "رمز العملة «" + value + "» ليس ثلاثة محارف لاتينية كبيرة على ISO 4217.",
        "Currency code '" + value + "' is not three upper-case ISO 4217 letters.");

    /// <summary>لا سطور في المُخرَج.</summary>
    public static readonly Error NoLines = new(
        "ai.capture.no_lines",
        "مُخرَج بلا سطور. الرمز يحمل الإجماليات؛ والسطور هي ما تُقرأ ضوئياً، فغيابها ليس نجاحاً.",
        "An extraction with no lines. The QR carries the totals; the lines are what optical reading is for.");

    /// <summary>عنصر في مصفوفة السطور ليس كائناً.</summary>
    public static Error LineNotAnObject(int index) => new(
        "ai.capture.line_not_an_object",
        "العنصر " + Count(index) + " في «lines» ليس كائناً.",
        "Element " + Count(index) + " of 'lines' is not an object.");

    /// <summary>رمز حدث بشكل غير صالح.</summary>
    public static Error EventCodeMalformed(string value) => new(
        "ai.capture.event_code_malformed",
        "رمز الحدث «" + value + "» لا يطابق الشكل «وحدة.كيان.فعل» بحروف لاتينية صغيرة.",
        "Event code '" + value + "' does not match '<module>.<entity>.<verb>' in lower-case Latin.");

    /// <summary>اقتراح يحمل رمز حساب.</summary>
    public static Error SuggestionNamesLedgerCode(string value) => new(
        "ai.capture.suggestion_names_a_ledger_code",
        "الاقتراح «" + value + "» يحمل مقطعاً رقمياً يشبه رمز حساب. "
        + "الاقتراح يسمّي حدثاً أو دوراً من مفردات مغلقة، ولا يسمّي حساباً بحال.",
        "Suggestion '" + value + "' carries a numeric segment shaped like a ledger code.");

    /// <summary>رمز حدث خارج المصفوفة.</summary>
    public static Error EventCodeNotInMatrix(string value) => new(
        "ai.capture.event_code_not_in_matrix",
        "رمز الحدث «" + value + "» ليس في مصفوفة الترحيل. "
        + "ورمزٌ مخترَع قيس وهو يُنتج ترحيلاً مكرَّراً صامتاً، فالرفض هنا يقع قبل أن يراه أحد.",
        "Event code '" + value + "' is not in the posting matrix.");

    /// <summary>رمز دور خارج المصفوفة.</summary>
    public static Error RoleCodeNotInMatrix(string value) => new(
        "ai.capture.role_code_not_in_matrix",
        "رمز الدور «" + value + "» ليس في مصفوفة الترحيل.",
        "Role code '" + value + "' is not in the posting matrix.");

    /// <summary>لا مسوّدة بهذا المعرّف.</summary>
    public static Error DraftNotFound(Guid id) => new(
        "ai.capture.draft_not_found",
        "لا مسوّدة التقاط بهذا المعرّف: " + id.ToString("D", CultureInfo.InvariantCulture),
        "No captured draft with this identifier: " + id.ToString("D", CultureInfo.InvariantCulture));

    /// <summary>ملاحظات مطابقة مفتوحة تمنع الترقية.</summary>
    public static Error DraftHasOpenFindings(int count) => new(
        "ai.capture.draft_has_open_findings",
        "المسوّدة تحمل " + Count(count) + " ملاحظة مطابقة مفتوحة. "
        + "لا تُرقّى مسوّدة لا يتّسق حسابها: كلٌّ من الملاحظات تسمّي رقمها وفرقه.",
        "The draft carries " + Count(count) + " open reconciliation findings and cannot be promoted.");

    /// <summary>حقل يحتاج قراراً بشرياً ولم يُؤكَّد.</summary>
    public static Error FieldNotConfirmed(string field) => new(
        "ai.capture.field_not_confirmed",
        "الحقل «" + field + "» مصدره يوجب مراجعة أو قراراً بشرياً ولم يُؤكَّد. "
        + "الذكاء الاصطناعي يقترح ولا يعتمد.",
        "Field '" + field + "' requires human review or decision and was not confirmed.");

    /// <summary>محاولة إعادة كتابة حقل مُصدَّق بيد إنسان.</summary>
    public static Error AttestedFieldCannotBeRetyped(string field) => new(
        "ai.capture.attested_field_cannot_be_retyped",
        "الحقل «" + field + "» مُصدَّق من رمز الفاتورة ولا يُعاد كتابته بيد. "
        + "الكتابة فوقه تُزيل أقوى ضمانة على المسوّدة؛ وإن كان الرمز نفسه مشكوكاً فيه فالمسوّدة تُرفض ولا تُعدَّل.",
        "Field '" + field + "' is attested by the invoice QR and cannot be retyped; if the QR itself is in doubt, reject the draft.");

    /// <summary>مسوّدة في حالة لا تقبل الترقية.</summary>
    public static Error NotPromotable(DraftState state) => new(
        "ai.capture.draft_not_promotable",
        "المسوّدة في الحالة " + state.ToString() + " ولا تُرقّى إلا من الحالة " + DraftState.Reconciled.ToString() + ".",
        "The draft is in state " + state.ToString() + "; only " + DraftState.Reconciled.ToString() + " may be promoted.");

    /// <summary>لا اقتراح ترحيل، والترقية بلا حدث مستحيلة.</summary>
    public static readonly Error NoSuggestion = new(
        "ai.capture.no_suggestion",
        "لا رمز حدث على المسوّدة. ورمز الحدث إلزام على مسارَي الترحيل معاً، فالترقية بلا حدث مستحيلة.",
        "The draft carries no event code; the event code is mandatory on both posting paths.");

    /// <summary>المزوّد لا يملك جواباً عن هذا المستند.</summary>
    public static Error ProviderHasNoAnswer(string documentId) => new(
        "ai.capture.provider_has_no_answer",
        "المزوّد لا يحمل مُخرَجاً لهذا المستند: " + documentId + ". "
        + "والمزوّد الحتمي لا يخترع جواباً — عدم المعرفة يُعلن ولا يُملأ.",
        "The provider holds no extraction for document " + documentId + "; a deterministic provider never invents one.");

    private static string Show(decimal value) => value.ToString("0.####", CultureInfo.InvariantCulture);

    private static string Count(int value) => value.ToString(CultureInfo.InvariantCulture);
}
