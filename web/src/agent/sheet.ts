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
 * @param sheet الورقة الواردة.
 */
export function agentSheetFaults(sheet: AgentQuestionSheet): readonly AgentSheetFault[] {
  const faults: AgentSheetFault[] = [];
  if (!sheet.questionId.trim()) faults.push("noQuestion");
  if (!isAgentEntityKind(sheet.kind)) faults.push("unknownKind");
  if (sheet.options.length === 0) faults.push("noChoice");
  if (sheet.options.some((option) => !option.optionToken.trim())) faults.push("emptyToken");
  const seen = new Set(sheet.options.map((option) => option.optionToken));
  if (seen.size !== sheet.options.length) faults.push("duplicateToken");
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
