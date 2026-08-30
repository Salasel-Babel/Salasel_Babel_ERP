using System.Globalization;
using Babel.SharedKernel;

namespace Babel.Projects.Application;

/// <summary>
/// أخطاء وحدة المقاولات. كل خطأ برمز ثابت ورسالتين — الرفض يُقرأ في تدقيق، ويُبنى عليه عميل.
/// </summary>
internal static class ProjectsErrors
{
    private static string Id(Guid value) => value.ToString("D", CultureInfo.InvariantCulture);

    public static Error NotFound(string type, Guid id) => new(
        "projects.not_found",
        "لا مستند " + type + " بهذا المعرّف: " + Id(id),
        "No " + type + " with this identifier: " + Id(id));

    public static Error DuplicateNumber(string number) => new(
        "projects.duplicate_number",
        "الرقم «" + number + "» مستعمل. والرقم المرئي يرسله العميل ويُتحقَّق من تفرّده — "
        + "ولا عدّاد ولا تسلسل في قاعدة البيانات لرقمٍ يراه مستخدم أو مدقّق.",
        "Number '" + number + "' is already in use. The visible number is sent by the client and checked for "
        + "uniqueness; no database sequence or identity backs a number a user or auditor reads.");

    public static Error DuplicateSequence(Guid contractId, int sequenceNo) => new(
        "projects.duplicate_sequence",
        "للعقد " + Id(contractId) + " مستخلصٌ بالتسلسل "
        + sequenceNo.ToString(CultureInfo.InvariantCulture)
        + " سلفاً. وتسلسل المستخلص داخل العقد هو ما يقوم عليه الاشتقاق التراكمي، فلا يتكرّر.",
        "Contract " + Id(contractId) + " already has a certificate with sequence "
        + sequenceNo.ToString(CultureInfo.InvariantCulture)
        + ". The certificate's sequence within its contract is what the cumulative derivation rests on, so it is unique.");

    public static Error NoLines() => new(
        "projects.no_lines",
        "المستخلص بلا سطور. ومستخلصٌ بلا سطر لا يقول شيئاً عن عملٍ نُفِّذ.",
        "The certificate has no lines. A certificate without a line says nothing about work executed.");

    public static Error WrongState(string expected, string actual) => new(
        "projects.wrong_state",
        "حالة المستند «" + actual + "» والمطلوب «" + expected + "».",
        "The document state is '" + actual + "' but '" + expected + "' is required.");

    public static Error UnitMismatch(string itemCode, string lineUnit, string itemUnit) => new(
        "projects.unit_mismatch",
        "وحدة السطر «" + lineUnit + "» تخالف وحدة البند «" + itemCode + "» وهي «" + itemUnit + "». "
        + "ولا تُحوَّل الوحدات في هذه الوحدة: قاعدة التحويل يملكها المخزون، ونسخةٌ ثانية منها هنا "
        + "تنحرف عن أصلها عند أول تعديل. فالسطر يُرفض ولا يُحوَّل.",
        "Line unit '" + lineUnit + "' differs from the unit of item '" + itemCode + "', which is '" + itemUnit + "'. "
        + "Units are not converted in this module: the conversion rule belongs to inventory, and a second copy of it "
        + "here would drift from its original at the first edit. The line is refused, not converted.");

    /// <summary>
    /// الكمّية التراكمية نقصت عن آخر مستخلصٍ <b>مُرحَّل</b>.
    /// <para>
    /// وإعادة القياس النازلة مسارُ تصحيحٍ لم يُحسم شكله بعد (إشعار دائن؟ مستخلص سالب؟
    /// منعُ النزول؟)، فهي تُرفض باسمها ولا تُبتلع.
    /// </para>
    /// </summary>
    /// <param name="itemCode">رمز البند.</param>
    public static Error CumulativeQuantityWentDown(string itemCode) => new(
        "projects.cumulative_quantity_went_down",
        "الكمّية التراكمية للبند «" + itemCode + "» أقلّ من كمّيته في آخر مستخلصٍ مُرحَّل. "
        + "وشكل التصحيح — إشعار دائن يُنشئ قيداً جديداً، أم مستخلص بقيمة سالبة، أم منع النزول — "
        + "قرارُ مالكٍ لم يُحسم بعد، فلا يُختار أحدها ضمناً.",
        "The cumulative quantity for item '" + itemCode + "' is below its quantity in the last posted certificate. "
        + "The correction shape — a credit note creating a new entry, a negative certificate, or forbidding the "
        + "decrease — is an owner decision that is not settled, so none of them is chosen implicitly.");

    /// <summary>
    /// <b>بندٌ معلَّق على العقد يمنع الترحيل — وهذا هو الرفض الصريح بدل التخمين.</b>
    /// <para>
    /// المصفوفة تفرض أن النسبة «تأتي من العقد لا من قيمة ثابتة في الكود»، ولا تقول على
    /// أي وعاء تُضرب ولا بأي قاعدة يُستردّ. والمبالغ الأربعة يجب أن يكون لكلٍّ حاسبٌ في
    /// الشيفرة، فبلا الجواب يصيران معامِلَين يُمليهما المستدعي ويُنتجان قيداً متوازناً
    /// برقمٍ اخترعه.
    /// </para>
    /// </summary>
    /// <param name="contractId">العقد.</param>
    /// <param name="pending">البنود المعلَّقة بأسمائها.</param>
    public static Error ContractPolicyPending(Guid contractId, IReadOnlyList<PendingPolicyItem> pending) => new(
        "projects.contract_policy.pending",
        "لا يُرحَّل مستخلصُ العقد " + Id(contractId) + ": بنودٌ معلَّقة لم يعتمدها محاسب بعد — "
        + string.Join(" · ", pending.Select(static item => item.TitleAr))
        + ". ولا قيمة افتراضية لأيٍّ منها: قيدٌ يقوم على تخمينٍ قيدٌ متوازن يقنع كل حارس ولا يقنع مدقّقاً.",
        "Certificate posting is refused for contract " + Id(contractId)
        + ": items are still pending an accountant's approval — "
        + string.Join(" · ", pending.Select(static item => item.TitleEn))
        + ". None of them has a default: an entry built on a guess is a balanced entry that satisfies every guard "
        + "and no auditor.");

    /// <summary>
    /// قرارٌ اعتُمد ولا حاسبَ له بعد — الحجب الثاني، وهو مقصود.
    /// <para>
    /// نصّ القرار في <c>projects.contract_policy</c> مبهمٌ على الشيفرة عمداً: من يكتبه
    /// محاسب، ومن يبني حاسبه مهندس بتوقيع ذلك المحاسب. فوجود الصفّ يرفع الحجب الأول
    /// ولا يفتح حساباً لم يُكتب.
    /// </para>
    /// </summary>
    /// <param name="itemCode">رمز البند.</param>
    /// <param name="resolution">نصّ القرار المعتمد.</param>
    public static Error PolicyResolutionNotImplemented(string itemCode, string resolution) => new(
        "projects.contract_policy.resolution_not_implemented",
        "البند «" + itemCode + "» اعتُمد بالقرار «" + resolution + "» ولا حاسبَ له في الوحدة بعد. "
        + "وبناءُ الحاسب يتبع القرار ولا يسبقه، فالترحيل مرفوض حتى يُبنى بتوقيع من اعتمده.",
        "Item '" + itemCode + "' was approved with resolution '" + resolution + "' and no calculator implements it "
        + "yet. The calculator follows the decision rather than preceding it, so posting is refused until it is "
        + "built with the approver's signature.");

    /// <summary>
    /// مستخلص باطنٍ يحمل سطر غرامة أو خصم — يُخزَّن ولا يُرحَّل.
    /// <para>
    /// تحفّظ المصفوفة يمنع خصمها من قيمة الأعمال، والقالب لا يملك سطراً لها: مبالغه
    /// أربعة ليس فيها <c>penalty</c>، وتعبير سطر الدائن لا يحتمل طرفاً خامساً. والخصم من
    /// قيمة الأعمال يُنقص التكلفة المعترف بها للمشروع بمبلغ غرامة فتنحرف ربحيته وتكلفةُ
    /// بنده معاً — <b>ورفضٌ مُعلَن خيرٌ من خصمٍ صامت</b>.
    /// </para>
    /// </summary>
    /// <param name="certificateId">المستخلص.</param>
    /// <param name="lineCount">عدد سطور الغرامة والخصم عليه.</param>
    public static Error PenaltyLinesHaveNoTemplate(Guid certificateId, int lineCount) => new(
        "projects.penalty_line_has_no_template",
        "المستخلص " + Id(certificateId) + " يحمل "
        + lineCount.ToString(CultureInfo.InvariantCulture)
        + " سطر غرامة أو خصم، ولا سطر لها في قالب الحدث: مبالغه أربعة ليس فيها غرامة. "
        + "والخصم من قيمة الأعمال ممنوع بتحفّظ المصفوفة نصّاً — فالسطور تُخزَّن، والترحيل يُرفض "
        + "حتى يهبط مبلغ الغرامة وسطرُه في المصفوفة بتوقيع محاسب.",
        "Certificate " + Id(certificateId) + " carries "
        + lineCount.ToString(CultureInfo.InvariantCulture)
        + " penalty or deduction lines, and the event template has no line for them: it has four amounts and none "
        + "is a penalty. Netting them against the works value is forbidden by the matrix caveat verbatim — so the "
        + "lines are stored and posting is refused until a penalty amount and its line land in the matrix with an "
        + "accountant's signature.");

    /// <summary>
    /// لا حركة محتجزٍ بهذا المعرّف — والإفراج والتحصيل يقعان على <b>دفعة مُسمّاة</b>.
    /// <para>
    /// وحركات المحتجز تُشتقّ من <b>المُرحَّل وحده</b>، فما دام أول مستخلص محجوباً فلا
    /// حركة تُفرَج ولا حركة تُحصَّل. وهذا أثرٌ مباشر للبند المعلَّق، لا عطلٌ في هذا المسار.
    /// </para>
    /// </summary>
    /// <param name="movementId">معرّف الحركة المطلوبة.</param>
    public static Error RetentionMovementNotFound(Guid movementId) => new(
        "projects.retention_movement_not_found",
        "لا حركة محتجز بالمعرّف " + Id(movementId) + ". وحركات المحتجز تُشتقّ من المُرحَّل وحده، "
        + "فلا يُفرَج عن رصيدٍ لم يُثبته قيد.",
        "No retention movement with identifier " + Id(movementId) + ". Retention movements are derived from posted "
        + "entries alone, so no balance is released that no entry established.");

    public static Error ReleaseExceedsMovement(Guid movementId) => new(
        "projects.release_exceeds_movement",
        "المبلغ المطلوب الإفراج عنه يتجاوز رصيد الحركة " + Id(movementId) + " بعد ما أُفرِج عنه سابقاً.",
        "The requested release exceeds the remaining balance of movement " + Id(movementId)
        + " after earlier releases.");

    public static Error CurrencyMismatch(string documentCurrency, string contractCurrency) => new(
        "projects.currency_mismatch",
        "عملة المستند «" + documentCurrency + "» تخالف عملة العقد «" + contractCurrency + "».",
        "The document currency '" + documentCurrency + "' differs from the contract currency '"
        + contractCurrency + "'.");

    public static Error NegativeAmount(string field) => new(
        "projects.negative_amount",
        "الحقل «" + field + "» لا يقبل مبلغاً سالباً.",
        "Field '" + field + "' does not accept a negative amount.");

    /// <summary>
    /// رمز مشروع لا يقابله سجلّ — <b>سدُّ الثقب داخل الوحدة</b>.
    /// <para>
    /// قيمة <c>project_id</c> في الدفتر نصٌّ حرّ بلا سجلّ ولا مفتاح أجنبي، ومشغّل
    /// ‏GR-COA-002 يفحص <c>is null</c> وحده — فسلسلةٌ فارغة أو رمزٌ مخطوء يعبران. هذه
    /// الوحدة تتحقّق من الرمز قبل أن تبني طلب الترحيل؛ ويبقى الثقب مفتوحاً على أي كاتبٍ
    /// آخر، وهو دَينٌ مُسجَّل لا مُغفَل.
    /// </para>
    /// </summary>
    /// <param name="code">الرمز المطلوب.</param>
    public static Error ProjectCodeNotRegistered(string code) => new(
        "projects.project_code_not_registered",
        "رمز المشروع «" + code + "» لا يقابله مشروع مسجَّل. والرمز هو ما يدخل بُعد المشروع على سطر القيد، "
        + "ورمزٌ مخطوء يعبر إلى الدفتر ولا يمسكه شيء هناك.",
        "Project code '" + code + "' has no registered project. The code is what enters the project dimension on a "
        + "journal line, and a mistyped code passes into the ledger where nothing catches it.");

    public static Error MissingEventCode(string documentType, Guid documentId) => new(
        "projects.missing_event_code",
        "طلب ترحيل بلا رمز حدث للمستند " + documentType + "/" + Id(documentId)
        + ". ورمز الحدث حقلٌ في هوية الإحكام، وغيابه يجعل حدثين للمستند نفسه هويةً واحدة.",
        "A posting request with no event code for document " + documentType + "/" + Id(documentId)
        + ". The event code is a field of the posting identity, and its absence makes two events of the same "
        + "document a single identity.");

    public static Error PostingRefused(IReadOnlyList<Error> errors) => new(
        "projects.posting_refused",
        "رفض محرّك الترحيل الطلب: " + string.Join(" · ", errors.Select(static e => e.MessageAr)),
        "The posting engine refused the request: " + string.Join(" · ", errors.Select(static e => e.MessageEn)));

    public static Error CapabilityProfileMissing(TenantId tenant) => new(
        "projects.capability_profile_missing",
        "لا ملفّ قدرات لهذه المنشأة (" + Id(tenant.Value) + "). وغياب الملفّ رفضٌ لا فتح: "
        + "منشأةٌ بلا ملفّ ليست بلا قيود، بل لم يُقرَّر بعد ما اشترته.",
        "This company (" + Id(tenant.Value) + ") has no capability profile. A missing profile fails closed: a "
        + "company without one is not unconstrained, it is one whose purchase has not been decided yet.");

    public static Error AdmissionDoesNotCoverField(string documentType, string field) => new(
        "projects.admission_does_not_cover_field",
        "القبول الممنوح للمستند «" + documentType + "» لا يشمل الحقل «" + field + "».",
        "The admission granted for document '" + documentType + "' does not cover field '" + field + "'.");

    public static Error ControlPointUnavailable(IReadOnlyList<Error> errors) => new(
        "projects.control_point_unavailable",
        "تعذّرت قراءة نقطة الضبط: " + string.Join(" · ", errors.Select(static e => e.MessageAr)),
        "The control point could not be read: " + string.Join(" · ", errors.Select(static e => e.MessageEn)));
}
