/* ═══════════════════════════════════════════════════════════════════════════
   ورقة السؤال — ما يراه الإنسان وما لا يراه النموذج
   The question sheet — what the human sees and what the model never does
   ───────────────────────────────────────────────────────────────────────────
   الوكيل يقول «هذا الاسم ملتبس، اسأل». ثم **الخادم** يبحث في السجلّ المحلّي
   ويرسم الورقة من بياناتٍ محلّية، ويختار الإنسان، ويعود إلى الوكيل **رمزٌ
   معتم** وحده. فلا يرى النموذج اسماً من الأسماء، ولا يعرف كم كانت.

   **وما يعبر من هذا الملفّ إلى السلك مفتاحان لا ثالث لهما**: `questionId`
   و`optionToken`. لا موضعُ الخيار، ولا نصُّه، ولا عددُ الخيارات، ولا «هل كان
   الاختيار جديداً». وشكلُ ما يعود إلى النموذج بعد ذلك واحدٌ في الحالين —
   `{"handle":"…"}` — فحتى **واقعةُ الإنشاء** لا تُستدلّ من الشكل.

   ولماذا رمزٌ لا موضع: الموضع يُعدّ. من يرى `{"choice":3}` يعرف أن الخيارات
   كانت أربعةً على الأقل، وثلاثُ محاولاتٍ بأسماءٍ متدرّجة تمسح السجلّ. والرمز
   موقَّع في الخادم (HMAC على غرار `SignedAttachmentTickets`)، فلا يُعدّ ولا
   يُزوَّر ولا يُعاد استعماله في محادثةٍ أخرى.
   ═══════════════════════════════════════════════════════════════════════════ */

/* ═════════════════════════════════════════ ١ · نوع الكيان — مجموعة مغلقة */

/**
 * أنواع الكيانات الستّة التي يسأل عنها الوكيل. **مجموعة مغلقة**: قيمةٌ خارجها
 * تُرفض ولا تُقسر على أقرب عضو — وهي بأسماء `AgentEntityKind` في الخادم.
 */
export const AGENT_ENTITY_KINDS = [
  "customer",
  "supplier",
  "employee",
  "inventoryItem",
  "propertyUnit",
  "project",
] as const;

/** نوع الكيان المسؤول عنه. */
export type AgentEntityKind = (typeof AGENT_ENTITY_KINDS)[number];

/**
 * هل هذا النصّ نوعُ كيانٍ معلَن؟ **الرفض لا التخمين**: نوعٌ لا نعرفه لا يُقرَّب
 * إلى أقرب معروف، لأن ورقةً تسأل عن مورّدٍ وقد سُئلت عن عميل تُنشئ الكيان في
 * السجلّ الخطأ.
 * @param value النصّ الوارد.
 */
export function isAgentEntityKind(value: string): value is AgentEntityKind {
  return (AGENT_ENTITY_KINDS as readonly string[]).includes(value);
}

/* ═════════════════════════════════════ ١ب · شكل الرمز كما يسكّه الخادم */

/**
 * طول الرمز الموقَّع بالمحارف — **وهو `SignedLookupHandles.TokenLength` نفسه**،
 * وحارسٌ في الخادم يقرأ هذا الملفّ ويطابق العددين. والطول الثابت جزءٌ من الحدّ:
 * رمزٌ يطول بطول الاسم يقول شيئاً عن الاسم.
 */
export const AGENT_TOKEN_LENGTH = 152;

/** حجم مجموعة المحارف الواحدة داخل الرمز. */
export const AGENT_TOKEN_GROUP_LENGTH = 8;

/**
 * فاصل المجموعات. **وليس زينة**: الرمز نصٌّ عشوائيّ يعبر مِصفاة الحدّ الخارجة مثل
 * أي نصّ، وقاعدةُ 64 عُشرُ أبجديتها خانات — فرمزٌ متّصل يحمل صدفةً تسع خاناتٍ
 * متتالية فيُرفض دورٌ سليم. و`~` ليست فاصلاً عند أي درجة لمّ في المِصفاة، فلا
 * يتجاوز أي مسارٍ رقميّ في الرمز ثماني خانات.
 */
export const AGENT_TOKEN_GROUP_SEPARATOR = "~";

/* ══════════════════════════════════════════════════ ٢ · شكل الورقة */

/** خيارٌ واحد على الورقة — نصُّه محلّي، ورمزُه هو ما يعبر. */
export interface AgentQuestionOption {
  /**
   * الرمز الموقَّع. **هو وحده ما يعود إلى الخادم**، ولا يُقرأ في المتصفّح ولا
   * يُشتقّ منه شيء: توقيعٌ يحمل غرضه ومستأجره وشركته وجلسته داخل بايتاته.
   */
  readonly optionToken: string;
  /** الاسم كما هو في سجلّ المستخدم. **لا يغادر المتصفّح.** */
  readonly label: string;
  /** سطرٌ فارق تحت الاسم — رمز الطرف مثلاً. أقنعةٌ لا معرّفات. */
  readonly subtitle?: string;
}

/** ورقة سؤالٍ كما يرسمها الخادم من بياناتٍ محلّية. */
export interface AgentQuestionSheet {
  /** معرّف السؤال الموقَّع — وهو ما ينطق به النموذج في `ask_question`، لا غير. */
  readonly questionId: string;
  /** نوع الكيان المسؤول عنه. */
  readonly kind: AgentEntityKind;
  /**
   * كلماتُ المستخدم نفسها — منها يُركَّب عنوان الورقة بلغته. والعنوان يُركَّب
   * في المتصفّح لا في الخادم: الخادم يعرف العربية وحدها، والواجهة أربع لغات،
   * وعنوانٌ عربيٌّ وحده يترك قارئ الأردية أمام سؤالٍ لا يقرؤه.
   */
  readonly subjectText: string;
  /** الخيارات المرسومة من السجلّ المحلّي. */
  readonly options: readonly AgentQuestionOption[];
  /**
   * هل تُتاح «جديد»؟ وحين تُتاح فهي **آخر** خيارٍ في القائمة، ورمزُها غرضُه
   * `CreateSheet` لا `Option` — فلا يُفتدى بوصفه كياناً قائماً أبداً.
   */
  readonly allowsCreate: boolean;
}

/* ═════════════════════════════ ٣ · ما يعبر إلى الخادم ثم إلى النموذج */

/**
 * جواب الورقة. **مفتاحان لا ثالث لهما** — وهذا هو الحدّ كلّه في نوع.
 * ومن أراد أن يضيف ثالثاً فليقرأ صدر هذا الملفّ أولاً.
 */
export interface AgentAnswer {
  readonly questionId: string;
  readonly optionToken: string;
}

/** مفاتيح الجواب، مُعلَنةً كي يحرسها اختبار — لا كي يقرأها عارض. */
export const AGENT_ANSWER_KEYS: readonly string[] = ["optionToken", "questionId"];

/**
 * يبني الجواب من الورقة والخيار. **لا يقرأ نصّ الخيار ولا موضعه** — يأخذ
 * الرمز ويترك الباقي في المتصفّح حيث نشأ.
 * @param sheet الورقة المعروضة.
 * @param option الخيار الذي اختاره الإنسان.
 */
export function answerOf(sheet: AgentQuestionSheet, option: AgentQuestionOption): AgentAnswer {
  return { questionId: sheet.questionId, optionToken: option.optionToken };
}

/** مسوّدة الإنشاء: الجواب نفسه ومعه القيم التي كتبها الإنسان بيده. */
export interface AgentCreateDraft {
  readonly questionId: string;
  /** رمز خيار «جديد» — غرضُه `CreateSheet`، ويفتديه الخادم فيعرف ما يُنشأ. */
  readonly optionToken: string;
  /** معرّف العملية المنشورة التي تُنشئ الكيان — مشتقٌّ من العقد لا من النموذج. */
  readonly operationId: string;
  /**
   * القيم كما كُتبت، بمفاتيح حقول العقد (`name.ar` مسارٌ لا اسمٌ مركَّب).
   * **نصوصٌ كلّها**: المال نصٌّ في هذا المستودع، ولا يُقسر عددٌ في المتصفّح.
   */
  readonly values: Readonly<Record<string, string>>;
}

/* ═══════════════════════════════════ ٤ · فحص الورقة قبل عرضها */

/** عللُ ورقةٍ لا تصلح للعرض. */
export type AgentSheetFault =
  | "noQuestion"
  | "unknownKind"
  | "noChoice"
  | "emptyToken"
  | "duplicateToken";

/**
 * يفحص ورقةً وردت من الخادم. **ورقةٌ معتلّة تُرفض ولا تُعرض**: قائمةٌ بلا
 * خيارٍ ولا «جديد» تجعل الإنسان أمام سؤالٍ بلا جواب، ورمزان متطابقان يجعلان
 * اختيارين يعودان بالشيء نفسه فيظنّ أنه اختار وهو لم يختر.
 *
 * **ولا يفترض أن المفاتيح موجودة.** كان يقرأ `sheet.questionId.trim()` و
 * `sheet.options.length` رأساً، فحمولةٌ ينقصها مفتاحٌ واحد ترمي `TypeError`
 * أثناء العرض (هو يُنادى داخل `useMemo`) **فتسقط اللوحة كلّها** بدل أن ترسم
 * الرفض الذي وُجد هذا الفحص ليرسمه. وهو «ارفض ولا تخمّن» ساقطاً على وجهه في
 * الجهة السلكية بالذات — وهي الجهة الوحيدة التي لا نملك فيها الكاتب.
 * @param sheet الورقة الواردة.
 */
export function agentSheetFaults(sheet: AgentQuestionSheet): readonly AgentSheetFault[] {
  const faults: AgentSheetFault[] = [];
  const shape = sheet as Partial<AgentQuestionSheet> | null | undefined;

  const questionId = typeof shape?.questionId === "string" ? shape.questionId : "";
  if (!questionId.trim()) faults.push("noQuestion");

  if (typeof shape?.kind !== "string" || !isAgentEntityKind(shape.kind)) faults.push("unknownKind");

  const options = Array.isArray(shape?.options) ? shape.options : null;
  if (options === null) {
    /* `options` ليست مصفوفةً أصلاً: لا خيار، ولا يُقرأ منها طولٌ ولا عنصر. */
    faults.push("noChoice");
    return faults;
  }

  if (options.length === 0) faults.push("noChoice");

  const tokens = options.map((option) =>
    typeof option?.optionToken === "string" ? option.optionToken : ""
  );

  if (tokens.some((token) => !token.trim())) faults.push("emptyToken");
  if (new Set(tokens).size !== tokens.length) faults.push("duplicateToken");

  return faults;
}

/**
 * هل الخيار عند هذا الموضع هو «جديد»؟ **عرضٌ لا هوية**: الموضع يختار المعالجة
 * البصرية وحدها، وما يعود إلى الخادم رمزُ الخيار في الحالين. والقاعدة منشورة
 * في العقد: «جديد» آخرُ عنصرٍ دائماً حين تُتاح.
 * @param sheet الورقة.
 * @param index موضع الخيار.
 */
export function isCreateOption(sheet: AgentQuestionSheet, index: number): boolean {
  return sheet.allowsCreate && index === sheet.options.length - 1;
}
