/* ═══════════════════════════════════════════════════════════════════════════
   ما تقرؤه شاشات الرواتب **من العقد** لا مما تظنّه  ·  Read from the contract
   ───────────────────────────────────────────────────────────────────────────
   قائمةٌ مكتوبة بيدٍ تنحرف عند أول إضافة، فتُرسل الشاشة مؤهّلاً لا يعرفه
   الخادم — ورسالة الوحدة نفسها تقول لماذا ذلك خطر: «مؤهّلٌ لا تعرفه خريطة
   الأدوار يقع على المؤهّل الافتراضي **فيختار حساباً آخر بصمت**». فالمجموعات
   المغلقة هنا تُقرأ من `runtime-schema` المُولَّد وقت التشغيل، وعضوٌ جديد في
   العقد يظهر في الشاشة وحده، وعضوٌ اختفى **يكسر الإقلاع بصوت عالٍ** بدل أن
   يُعرض بلا اسم أو باسم جاره.

   **وما ليس مجموعةً مغلقة في العقد لا يُعامَل كأنه مغلق**: حقل `state` نصٌّ
   حرّ في العقد المنشور، فالشاشة تعرف قيمه المعروفة وتعرض ما عداها **كما وصل**
   بدل أن تسقطه أو تسمّيه باسم غيره.

   **ورموز الرفض ثوابتُ نتصرّف عليها ولا نقرأ نصّ رسالة**: الخادم يرسل الرسالة
   بلغتيها ونحن نقرأ الرمز وحده (ADR-0021).
   ═══════════════════════════════════════════════════════════════════════════ */
import { SCHEMAS } from "../../api/generated/runtime-schema";

/**
 * أعضاء مجموعةٍ مغلقة كما ينشرها العقد. ويرمي عند الإقلاع إن لم تكن مغلقة —
 * فقائمةٌ فارغة تُعرَض قائمةَ اختيارٍ لا خيار فيها، وذلك عطلٌ صامت.
 * @param schema اسم المخطّط.
 * @param field اسم الحقل.
 */
function members(schema: string, field: string): readonly string[] {
  const found = SCHEMAS[schema]?.fields?.[field]?.e;
  if (!found || found.length === 0) {
    throw new TypeError(
      "الحقل " + schema + "." + field + " ليس مجموعة مغلقة في العقد المُولَّد. " +
        "/ is not a closed set in the generated contract."
    );
  }
  for (const member of found) {
    /* العضو يصير **مقطعَ مفتاح لغة**، ومقطعٌ فيه نقطة يشقّ المفتاح شقّين. */
    if (!/^[A-Za-z0-9_]+$/.test(member)) {
      throw new TypeError(
        "عضوٌ لا يصلح مقطعَ مفتاح · a member that cannot be a key segment: " +
          schema + "." + field + " = " + member
      );
    }
  }
  return found;
}

/** طرق التسوية المنشورة — وهي مؤهّلاتُ دورٍ يقرؤها دليل الحسابات. */
export const SETTLEMENT_METHODS = members("HrSettlementRequest", "settlementMethod");

/** نوعا مكوّن الأجر: استحقاقٌ أو خصم. */
export const COMPONENT_KINDS = members("HrPayComponentRequest", "kind");

/** سيناريوهات المخالصة الثلاثة: مطابقة · عجز · زيادة. */
export const SETTLEMENT_SCENARIOS = members("HrSettlement", "scenarioCode");

/* ── الحالات: نصٌّ حرّ في العقد، ومعروفةٌ في الوحدة ────────────────────────
   `src/Babel.Hr/Persistence/HrRows.cs` — HrDocumentState و EmploymentState. */

/** مسوّدة. */
export const DRAFT = "DRAFT";
/** مُرحَّل. */
export const POSTED = "POSTED";
/** علاقة عمل سارية. */
export const ACTIVE = "ACTIVE";
/** علاقة عمل منتهية. */
export const TERMINATED = "TERMINATED";

/** الحالات التي تعرف الشاشة أسماءها؛ وما عداها يُعرض كما وصل. */
export const KNOWN_STATES: readonly string[] = [DRAFT, POSTED, ACTIVE, TERMINATED];

/* ══════════════════════════════════════════════ رموز الرفض التي نتصرّف عليها
   وكلّها من `src/Babel.Hr/Application/HrErrors.cs`. */

/** لا صفَّ نِسَبٍ معتمداً يغطّي التصنيف — **الرفض الحاكم في هذه الوحدة**. */
export const SETTINGS_MISSING = "hr.payroll_settings_missing";

/** للفترة مسيّرٌ قائم — و«هل يُسمح بثانٍ؟» سؤالٌ مفتوح على المالك. */
export const PERIOD_HAS_RUN = "hr.period_already_has_a_run";

/** لا علاقة عمل سارية تدخل المسيّر، فلا قسيمة تُبنى. */
export const NO_PAYSLIPS = "hr.no_payslips";

/** مستند دفعٍ بلا طرف خزينة — والمحرك يرفض الترحيل كلّه. */
export const TREASURY_MISSING = "hr.treasury_party_missing";

/** مخالصة على علاقة عمل ما تزال سارية. */
export const NOT_TERMINATED = "hr.employment_not_terminated";

/** رقم مستند مستعمل من قبل داخل هذه المنشأة. */
export const DUPLICATE_NUMBER = "hr.duplicate_number";
