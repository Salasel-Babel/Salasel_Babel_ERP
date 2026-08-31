/* ═══════════════════════════════════════════════════════════════════════════
   الرفوض — منقولةٌ عن الخادم بنصّها ورمزها
   ───────────────────────────────────────────────────────────────────────────
   «ارفض ولا تُخمّن» ليست حالةَ خطأٍ في هذا النظام، بل أظهر ما فيه. وطبقةُ
   عرضٍ تُري النجاح وحده تُري نصف المنتج — وأسوأ نصفَيه أمانةً.

   وكل رفضٍ هنا **منقولٌ حرفاً** عن رسالة الخادم في `src/Babel.*`،
   برمزه الثابت الذي هو نقطة الاعتماد البرمجية الوحيدة، وبصيغة RFC 9457
   التي ينشرها العقد في مخطّط `Problem`.
   ═══════════════════════════════════════════════════════════════════════════ */

/** رفضٌ جاهز: رمزه ورسالتاه. */
export interface Refusal {
  readonly code: string;
  readonly status: number;
  readonly ar: string;
  readonly en: string;
}

/**
 * يبني جسم مشكلةٍ بصيغة RFC 9457 كما ينشرها العقد.
 * @param refusal الرفض.
 * @param instance مسار الطلب.
 */
export function problemBody(refusal: Refusal, instance: string): Record<string, unknown> {
  return {
    code: refusal.code,
    detail: refusal.en,
    detailAr: refusal.ar,
    errors: [{ code: refusal.code, field: null, messageAr: refusal.ar, messageEn: refusal.en }],
    instance,
    status: refusal.status,
    title: refusal.status === 422 ? "Unprocessable content" : "Request refused",
    titleAr: refusal.status === 422 ? "طلب غير قابل للتنفيذ" : "رُفض الطلب",
    /* معرّف تتبّعٍ ثابت: العرض بلا خادم، فلا سجلّ يقود إليه هذا المعرّف —
       وثباتُه يقول ذلك بدل أن يوهم برقمٍ جديد كل مرّة. */
    traceId: "00-0000000000000000000000000000cafe-0000000000000000-00",
    type: "https://salasel-babel.example/problems/" + refusal.code,
  };
}

/* ───────────────────────────── رفوضٌ منقولة عن الخادم بنصّها ─────────── */

/**
 * نِسَبٌ نظامية غير معتمدة — `Babel.Hr/Application/HrErrors.cs`.
 * وهو الرفض الذي يمنع ترحيل مسيّر رواتب بلا صفّ نِسَبٍ معتمد.
 */
export const PAYROLL_SETTINGS_MISSING: Refusal = {
  code: "hr.payroll_settings_missing",
  status: 422,
  ar:
    "لا صفَّ نِسَبٍ معتمداً يغطّي التصنيف «class-private» في 2026-06-30. ونسبةُ اشتراك " +
    "التأمينات وحدَّا الأجر الخاضع **غير محسومَين** — البند م-14 في " +
    "docs/evidence/verification-debt.md — ولا يُخترع منها شيء هنا ولا يُكتب في شيفرة. " +
    "أودِع إصداراً في hr.payroll_settings بمصدره ومعتمِده وتاريخ سريانه، ثم أعد المحاولة.",
  en:
    "No approved rate row covers class 'class-private' on 2026-06-30. The social insurance " +
    "contribution rate and the contributory wage floor and ceiling are **undecided** — item م-14 " +
    "in docs/evidence/verification-debt.md — and none of them is invented here or written in code. " +
    "Deposit a version in hr.payroll_settings with its source, its approver, and its effective date, then retry.",
};

/**
 * بنودٌ معلَّقة على العقد تمنع ترحيل المستخلص —
 * `Babel.Projects/Application/ProjectsErrors.cs`.
 */
export const CONTRACT_POLICY_PENDING: Refusal = {
  code: "projects.contract_policy.pending",
  status: 422,
  ar:
    "لا يُرحَّل مستخلصُ العقد 7c9e6679-7425-40de-944b-e07fc1f90ae7: بنودٌ معلَّقة لم يعتمدها " +
    "محاسب بعد — وعاء نسبة المحتجز · قاعدة استرداد الدفعة المقدمة · مستوى التصنيف الضريبي · " +
    "موضع التقريب. ولا قيمة افتراضية لأيٍّ منها: قيدٌ يقوم على تخمينٍ قيدٌ متوازن يقنع كل " +
    "حارس ولا يقنع مدقّقاً.",
  en:
    "Certificate posting is refused for contract 7c9e6679-7425-40de-944b-e07fc1f90ae7: items are " +
    "still pending an accountant's approval — the retention base · the advance recovery rule · " +
    "the tax classification level · the rounding site. None of them has a default: an entry built " +
    "on a guess is a balanced entry that satisfies every guard and no auditor.",
};

/**
 * قسمة حصص الملاك غير محسومة —
 * `Babel.RealEstate/Application/RealEstateErrors.cs`.
 */
export const OWNER_SHARE_SPLIT_NOT_DECIDED: Refusal = {
  code: "realestate.owner_share_split_not_decided",
  status: 422,
  ar:
    "لهذا العقار أكثر من مالك، وقسمة سطور النموذج المُدار بالحصص بندٌ معلَّق على قرار المالك " +
    "(ق-ع-18): الشكل يحتمل الحصص من اليوم — المفتاح رباعي والحصّة كسر — ولم تُحسم سياسة القسمة " +
    "ولا تقريبها. لا يُرحَّل بقسمةٍ يخترعها النظام.",
  en:
    "This property has more than one owner, and splitting managed-model lines by share is a pending " +
    "owner decision (Q-RE-18): the shape carries shares from today — a four-part key and a fractional " +
    "share — but neither the split policy nor its rounding is settled. Nothing is posted on a split " +
    "the system invents.",
};

/** قيدٌ غير متوازن — `Babel.Ledger/Posting/PostingErrors.cs`. */
export const UNBALANCED: Refusal = {
  code: "ledger.posting.unbalanced",
  status: 422,
  ar: "القيد غير متوازن بعملة الشركة: مدين 4500.0000 ودائن 4025.0000.",
  en: "The entry does not balance in company currency: debit 4500.0000 credit 4025.0000.",
};

/** تحويل وحدةٍ لا يقع بلا باقٍ — `Babel.Inventory/Application/InventoryErrors.cs`. */
export const UNIT_CONVERSION_NOT_EXACT: Refusal = {
  code: "inventory.unit_conversion_not_exact",
  status: 422,
  ar:
    "تحويل المقدار 5 بالمعامل 12/1 لا يقع بلا باقٍ، فالناتج كسرٌ يُقرَّب. والتقريب في كمّية " +
    "تُضرب في تكلفة الوحدة يدخل المال ويتراكم على كل حركة. أرسل مقداراً يقبل القسمة على مقام " +
    "المعامل، أو أرسله بوحدة الأساس.",
  en:
    "Converting magnitude 5 by factor 12/1 does not divide exactly, so the result is a rounded " +
    "fraction. Rounding a quantity that is multiplied by a unit cost reaches the money and " +
    "accumulates on every movement. Send a magnitude divisible by the factor's denominator, or " +
    "send it in the base unit.",
};

/**
 * بابٌ لا يُمثَّل بلا خادم. **لا يُلبَس نجاحاً**: عرضٌ يردّ «تمّ» على فعلٍ لا
 * يقع يكذب في أخصّ ما يبيعه هذا النظام — أن الدفتر لا يُكتب بالتخمين.
 */
export const NOT_IN_SHOWCASE: Refusal = {
  code: "showcase.no_server",
  status: 501,
  ar:
    "هذا الباب يحتاج خادماً ودفتراً، وهذه صفحةُ عرضٍ للواجهة بلا أيٍّ منهما. " +
    "الشكل والرفوض من العقد المنشور، ولا يُرحَّل هنا قيد ولا تُوقَّع سلسلة.",
  en:
    "This operation needs a server and a ledger, and this is an interface showcase with neither. " +
    "Shapes and refusals come from the published contract; no entry is posted and no chain is signed here.",
};
