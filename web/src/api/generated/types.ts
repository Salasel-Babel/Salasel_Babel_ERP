/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     48db5a1817e2b9c661acd08d337e1185199319ea29f25b749dbbce3f00386290
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   أنواع العقد — مخطّطاً واحداً لكل مخطّط في components.schemas.
   ═══════════════════════════════════════════════════════════════════════ */

import type { Money } from "../money";
import type { ExchangeRate, Int64String, Magnitude, Quantity, Rate, TaxRate, UnitCost } from "./brands";

/* المال يصل هنا **مغلّفاً**: Money كائن يرمي عند أي تحويل ضمني إلى نصّ أو رقم.
   وبقيّة الصيغ النصّية المنشورة أنواع محتجزة (ExchangeRate · Int64String · Magnitude · Quantity · Rate · TaxRate · UnitCost).
   ولا حقل مالي واحد نوعه number — لا هنا ولا في أي ملف مكتوب بيد.
   Money is an object whose implicit coercions throw; the other published string
   formats are branded types. No monetary field is ever typed `number`. */

/** عضوية صاحب الجلسة في منشأة واحدة. / One membership of the session's holder in a single company. */
export interface AccessMembership {
  /** المنشأة كما تُكتب في المسار. / The company as written in the path. */
  companyId: string;
  /** الدور في هذه المنشأة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The role in this company. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  role: "Reader" | "Contributor" | "Owner";
}

/** جلسة مفتوحة كما تُسلَّم لصاحبها — **ومرّة واحدة**. الاعتمادان يخرجان من الخادم في هذه الاستجابة وحدها ولا يُخزَّنان في أي جدول: المُودَع بصمتهما SHA-256. فمن فقد الاستجابة فقد الاعتماد، ولا يوجد في الخادم من يعيده إليه — وهذا هو المقصود. وsessionId هو معرّف **العائلة**: يبقى ثابتاً عبر كل تجديد، وهو ما يُبطَل، فلا يهرب المُبطَل بتجديدٍ لاحق. / An opened session as handed to its holder — **once**. Both credentials leave the server in this response alone and are stored in no table: what is persisted is their SHA-256 digest. Whoever loses the response has lost the credential and nobody on the server can return it — which is the point. sessionId identifies the **family**: it stays constant across every renewal and it is what gets revoked, so a revoked session cannot escape by renewing. */
export interface AccessSession {
  /** الاعتماد الفاعل — يُقدَّم في ترويسة Authorization: Bearer. نصٌّ مبهم لا بنية فيه: لا يُحلَّل ولا يُشتقّ منه شيء، ويُقدَّم كما وصل. / The access credential — presented in the Authorization: Bearer header. An opaque string with no structure: never parsed, nothing derived from it, presented exactly as received. */
  accessCredential: string;
  /** لحظة انقضاء الاعتماد الفاعل. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / When the access credential expires. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  accessExpiresAt: string;
  /** رقم الدورة. يبدأ من 1 ويزيد بواحد عند كل تجديد، فقفزةٌ فيه بلا تجديدٍ من هذا العميل تعني أن غيره جدّد. / The generation. It starts at 1 and increments by one on every renewal, so a jump without a renewal from this client means someone else renewed. */
  generation: number;
  /** عضويات صاحب الجلسة، مرتَّبة بمعرّف المنشأة ترتيباً حرفياً ثابتاً. / The holder's memberships, ordered by company identifier in a stable ordinal order. */
  memberships: AccessMembership[];
  /** اعتماد التجديد — يُقدَّم مرّة واحدة، ثم يصير تقديمه سرقةً تُسقط العائلة. نصٌّ مبهم لا بنية فيه: لا يُحلَّل ولا يُشتقّ منه شيء، ويُقدَّم كما وصل. / The refresh credential — presented once; presenting it again is theft and drops the family. An opaque string with no structure: never parsed, nothing derived from it, presented exactly as received. */
  refreshCredential: string;
  /** لحظة انقضاء اعتماد التجديد. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / When the refresh credential expires. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  refreshExpiresAt: string;
  /** معرّف العائلة — وهو ما يُبطَل. / The family identifier — the thing that gets revoked. */
  sessionId: string;
  /** المستأجر خلف الجلسة. / The tenant behind the session. */
  tenantId: string;
  /** المستخدم خلف الجلسة. / The user behind the session. */
  userId: string;
  /** true حين تكون كل عضويات صاحب الجلسة Reader — أي أن هذه الجلسة لا تكتب في أي منشأة. تقرؤها الواجهة فتبني شاشة قراءة بدل أن تعرض أزراراً يرفضها الخادم. / true when every membership of the holder is Reader — this session writes in no company. A client reads it and builds a read-only screen instead of showing buttons the server will refuse. */
  writeReachesNothing: boolean;
}

/** أسماء حقول المستند لا قيمها: القبول حكمٌ على الشكل، ولا يعبر منه مبلغ. / The document's field names, not its values: admission is a verdict on shape and no amount crosses it. */
export interface AdmitDocumentRequest {
  /** أسماء الحقول الموجودة على المستند. / The names of the fields present on the document. */
  fields: string[];
}

/** جواب ورقة السؤال. **مفتاحان لا ثالث لهما** — وهذا هو الحدّ كلّه في مخطّط: لا موضعُ الخيار، ولا نصُّه، ولا عددُ الخيارات، ولا «هل كان جديداً». والموضع يُعدّ. / The question sheet's answer. **Two keys and no third** — this schema is the whole boundary: not the option's position, not its text, not the option count, not whether it was new. Position counts. */
export interface AgentAnswerRequest {
  /** رمز الخيار المختار كما ورد في الورقة، حرفاً بحرف. / The chosen option's token exactly as it appeared on the sheet. */
  optionToken: string;
  /** معرّف الورقة المعتِم كما ورد في حالها. / The sheet's opaque identifier as it appeared in the state. */
  questionId: string;
}

/** ما ينتظر تأكيد الإنسان الآن. **ومعنى التأكيد واحد: «أقبل شكل هذه البيانات»** — ولا يعني الترحيل، والناتج بعده مسوّدةٌ كما كان قبله. / What awaits the human's confirmation. **Confirmation means one thing: 'I accept the shape of this data'** — not posting; what follows is a draft exactly as before. */
export interface AgentConfirmation {
  /** حقول الجسم بترتيبٍ ثابت. / The body's fields in a stable order. */
  fields: AgentDraftField[];
  /** معرّف العملية المنشورة — وفعلُها draft دائماً، ويُفرض ذلك بنيوياً قبل أن يُسأل إنسان. / The published operation; its verb is always draft, structurally enforced before any human is asked. */
  operationId: string;
  /** مسار الشاشة التي ستهبط عليها المسوّدة. / The screen route the draft will land on. */
  screenRoute: string;
  /** الخطوة المنتظِرة. / The waiting step. */
  stepId: string;
}

/** حقلٌ في بطاقة التأكيد. **وقيمةُ ما شكلُه معرّف لا تُعرض**: masked صحيحة وvalue معدومة. والحدّ الذي حُفظ أمام النموذج يُحفظ أمام الكتف الذي يقف خلف المستخدم. / A field on the confirmation card. **Identifier-shaped values are not shown**: masked is true and value is null. The boundary kept from the model is kept from the shoulder behind the user too. */
export interface AgentDraftField {
  /** هل قُنِّعت القيمة لأن شكلها معرّف؟ / Was the value masked because its shape is an identifier? */
  masked: boolean;
  /** مسار الحقل داخل جسم العملية كما ينشره العقد. / The field's path inside the operation body as the contract publishes it. */
  path: string;
  /** القيمة المعروضة، أو null حين تُقنَّع. / The displayed value, or null when masked. */
  value: string | null;
}

/** رسالةُ المستخدم إلى الوكيل. **حقلٌ واحد لا ثانيَ له**: لا نموذج، ولا مفتاح، ولا تعليمات نظام — الثلاثة إعدادُ خادمٍ لا حقلُ طلب. وحقلٌ يختار منه الطالب نموذجَه يجعل عميلاً يبدّله في وسط محادثةٍ فيُبطل ذاكرة البادئة بلا أن يعلم. / The user's message to the agent. **One field and no second**: no model, no key, no system instructions — all three are server configuration. */
export interface AgentMessageRequest {
  /** كلام المستخدم بأسمائه. ولا تُكتب فيه أرقام هويةٍ ولا آيبان ولا تسجيلٍ ضريبي: الدور يُرفض قبل إرساله إن حملها. / The user's own words. Identity, IBAN, and VAT numbers must not appear: the turn is refused before it is sent if they do. */
  text: string;
}

/** خطوةٌ في خطّة الوكيل. **ولا حالة اسمها posted في state ولا يجوز أن توجد**: أبعد ما تبلغه خطوةٌ landed — مسوّدةٌ هبطت على شاشتها — والترحيل فعلٌ بصريّ يدويّ هناك. / A step in the agent's plan. **There is no 'posted' state and there must not be**: the furthest a step reaches is landed — a draft that arrived on its screen — and posting is a manual act there. */
export interface AgentPlanStep {
  /** ترتيب الخطوة بدءاً من واحد. / The step's order, starting at one. */
  order: number;
  /** أسباب سقوط الخطوة بلغتيها، أو قائمة فارغة. / Why the step was refused, in both languages, or an empty list. */
  refusals: ApiError[];
  /** مسار شاشة المسوّدة بعد هبوطها — وهو ما يفتحه الزرّ في اللوحة. ولا يعبر هذا المسار إلى النموذج. / The draft's screen route once it has landed; the panel's button opens it. This route never crosses to the model. */
  screenRoute: string | null;
  /** حال الخطوة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The step's state. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "awaitingAnswer" | "awaitingConfirmation" | "landed" | "planned" | "refused" | "running";
  /** معرّف الخطوة — وهو ما يُكتب في مسار التأكيد. / The step identifier, written into the confirmation path. */
  stepId: string;
  /** عنوان الخطوة بالعربية كما أعلنه الوكيل، أو اسم أداتها إن نفّذ بلا خطّة مُعلَنة. / The step's Arabic title as the agent declared it, or its tool name when it acted without a declared plan. */
  titleAr: string;
  /** اسم العملية المنشورة التي تناديها الخطوة — وفعلُها draft دائماً. / The published operation the step calls; its verb is always draft. */
  toolName: string | null;
}

/** خيارٌ على ورقة السؤال. **نصُّه محلّي ورمزُه هو ما يعبر**: الاسم يُعرَض على المستخدم ولا يبلغ النموذج أبداً، والرمز معمّى بطولٍ ثابت فلا يُعدّ ولا يُزوَّر ولا يُستعمل في محادثةٍ أخرى. / An option on the question sheet. **Its text is local and its token is what crosses**: the name is shown to the user and never reaches the model, and the token is encrypted at fixed length so it cannot be counted, forged, or reused in another conversation. */
export interface AgentQuestionOption {
  /** الاسم كما هو في سجلّ المستخدم. **ولا يبلغ النموذج.** / The name as it stands in the user's own register. **It never reaches the model.** */
  label: string;
  /** الرمز الموقَّع المعمّى — وهو وحده ما يعود إلى الخادم. / The encrypted signed token; it alone returns to the server. */
  optionToken: string;
  /** سطرٌ فارق تحت الاسم — قناعٌ لا معرّف. / A distinguishing line under the name — a mask, never an identifier. */
  subtitle: string | null;
}

/**
 * ورقة السؤال كما رسمها الخادم من بياناتٍ محلّية حين التبس اسم. **ولا يبلغ النموذجَ منها شيء**: لا الأسماء، ولا عددُها، ولا موضعُ ما اختير، ولا أنّ اختياراً وقع أصلاً — وما يعود إليه بعده شكلٌ واحد في كل الحالات.
 * 
 * **وallowsCreate تقول ما إذا كان «جديد» متاحاً**، ولا يُستنتَج من فراغ القائمة. / The question sheet the server drew from local data when a name was ambiguous. **Nothing in it reaches the model**: not the names, not their number, not which was chosen, not even that a choice happened.
 */
export interface AgentQuestionSheet {
  /** هل يُتاح خيار «جديد»؟ ويُقال صراحةً ولا يُستنتَج من فراغ القائمة. / Is a 'new' option offered? Stated explicitly, never inferred from an empty list. */
  allowsCreate: boolean;
  /** مفتاح السجلّ المسؤول عنه: customer · supplier · … وهو من مفردة lookup_entity المغلقة. / The register key concerned: customer, supplier, … from lookup_entity's closed vocabulary. */
  kind: string;
  /** الخيارات المرسومة من السجلّ المحلّي. / The options drawn from the local register. */
  options: AgentQuestionOption[];
  /** معرّف الورقة المعتِم — وهو ما يُكتب في جواب الورقة، ولا يُقرأ منه شيء. / The sheet's opaque identifier, written into the answer; nothing is readable from it. */
  questionId: string;
  /** كلام المستخدم كما بحث به الوكيل — منه يُركَّب عنوان الورقة بلغة القارئ. والعنوان يُركَّب في المتصفّح لا هنا: الخادم يعرف العربية وحدها والواجهة أربع لغات. / The user's own words as the agent searched them; the sheet's title is composed from them in the reader's language — in the browser, not here. */
  subjectText: string;
}

/**
 * حال مساحة العمل كلُّه في جسمٍ واحد — وهو ما تقرؤه اللوحة حين تُعيد الاتصال، ثم تُكمل من lastSequence.
 * 
 * **وphase هو ما يقول هل ما زال هناك ما يُنتظَر**: running يفكّر أو ينفّذ، وawaitingHuman يقف عند تأكيدٍ أو ورقة، وcompleted انتهى، وrefused سقط. **ولا قيمة اسمها posted**. / The whole workspace state in one body — what the panel reads on reconnect before continuing from lastSequence.
 * 
 * phase says whether anything is still awaited. **There is no 'posted' value.**
 */
export interface AgentSession {
  /** معرّف الجلسة. / The session identifier. */
  agentSessionId: string;
  /** مؤشّر آخر حدثٍ في السجلّ — تبدأ منه اللوحة قراءتها. / The cursor of the last event in the log; the panel reads onward from it. */
  lastSequence: number;
  /** ما ينتظر تأكيداً، أو null. / What awaits confirmation, or null. */
  pendingConfirmation: AgentConfirmation | null;
  /** ورقة السؤال المعلَّقة، أو null. / The pending question sheet, or null. */
  pendingQuestion: AgentQuestionSheet | null;
  /** طور الدور. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The turn's phase. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  phase: "awaitingHuman" | "completed" | "refused" | "running";
  /** خطوات الدور الجاري أو الأخير بحالها الآن. وتُستبدل كاملةً حين يُعلن الوكيل خطّةً جديدة. / The current or last turn's steps with their states; replaced wholesale when the agent declares a new plan. */
  plan: AgentPlanStep[];
  /** الدور الجاري أو آخر دور، أو null إن لم يبدأ دورٌ بعد. / The current or last turn, or null when no turn has begun. */
  turnId: string | null;
}

/**
 * إنفاق المنشأة على الوكيل في نافذة المحاسبة الجارية. **والوحدة رموزٌ لا ريالات** — والسبب في وصف العملية.
 * 
 * **وbillable وceiling نصّان لا رمزان رقميّان**: عدّاد رموزٍ يتجاوز مدى الصحيح 32 بت في نافذةٍ طويلة، وقصُّه إلى المدى كذبةٌ صامتة. / The tenant's agent spend in the current accounting window. **The unit is tokens, not riyals.**
 * 
 * billable and ceiling are strings, not JSON numbers: a token counter outgrows 32-bit range in a long window, and clamping it to that range is a silent lie.
 */
export interface AgentSpend {
  /** مجموع الرموز المحاسَب عليها في النافذة، نصّاً. / The billable token total in the window, as a string. */
  billable: string;
  /** هل تعمل هذه المنشأة على مفتاحها؟ ومن جاء بمفتاحه يُقاس إنفاقه ولا يُسقَف بسقف المالك. / Does this tenant run on its own key? One that does is measured and not capped by the owner's ceiling. */
  bringsItsOwnKey: boolean;
  /** السقف بالرموز نصّاً، أو null لمنشأةٍ تعمل بمفتاحها فلا يَسقُفها سقف المالك. / The token ceiling as a string, or null for a tenant on its own key. */
  ceiling: string | null;
  /** عدد الأدوار المُحاسَبة في النافذة. / The number of billed turns in the window. */
  turns: number;
  /** طول نافذة المحاسبة بالثواني. / The accounting window's length in seconds. */
  windowSeconds: number;
}

/** حكم الإنسان على **شكل** بيانات خطوة. **ولا يعني الترحيل** — والناتج بعده مسوّدةٌ كما كان قبله. وaccepted إلزامي ولا يُفترَض عند غيابه: التأكيد فعلٌ يُقال لا يُستنتَج من صمت. / The human's verdict on a step's **data shape**. **It does not mean posting.** accepted is required and never assumed when absent: confirmation is stated, not inferred from silence. */
export interface AgentStepConfirmationRequest {
  /** true إن قَبِل المستخدم شكل البيانات. وfalse يوقف الخطوة ولا يقتل الدور. / true when the user accepts the data's shape. false stops the step without killing the turn. */
  accepted: boolean;
}

/** دورٌ بدأ. **ولا ينتظر هذا الجواب انتهاءه** — الأحداث تُقرأ بمؤشّرها، وafter هو المؤشّر الذي تبدأ منه اللوحة قراءة أحداث هذا الدور. / A turn that has begun. **This response does not wait for it to finish**; events are read by cursor, and after is where the panel starts reading this turn's events. */
export interface AgentTurn {
  /** المؤشّر الذي تبدأ منه اللوحة قراءة أحداث هذا الدور. / The cursor the panel starts this turn's event reading from. */
  after: number;
  /** معرّف الدور. / The turn identifier. */
  turnId: string;
}

/**
 * حدثٌ واحد في سجلّ المساحة. **ولا يحمل معرّف صفٍّ ولا اسمَ طرفٍ ولا عددَ مرشّحين**: ما يعبر إلى الشاشة مسارُ شاشةٍ أو مِقبضٌ معتِم بطولٍ ثابت، وما يعبر إلى النموذج أقلّ من ذلك.
 * 
 * **وthinking جزءٌ من تفكيرٍ مُلخَّص يُعرَض تقدّماً**، لا سلسلة استدلالٍ تُخزَّن ولا تُبنى عليها قرارات. / One event in the workspace log. **It carries no row identifier, no party name, and no candidate count.**
 */
export interface AgentTurnEvent {
  /** شكل الحدث. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The event's kind. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  kind: "completed" | "draftLanded" | "planProposed" | "questionRaised" | "refused" | "text" | "thinking" | "toolRefused" | "toolStarted";
  /** معرّف ورقة السؤال المعتِم حين يلتبس اسم. / The opaque question-sheet identifier when a name is ambiguous. */
  questionId: string | null;
  /** أسباب الرفض بلغتيها، أو قائمة فارغة. / Why it was refused, in both languages, or an empty list. */
  refusals: ApiError[];
  /** مفتاح السجلّ في حدث ورقة السؤال — وهو معلومٌ للنموذج سلفاً لأنه هو من نطق به. / The register key on a question event; the model already knows it because it named it. */
  registerKey: string | null;
  /** مسار شاشة المسوّدة حين تهبط. **ولا يعبر إلى النموذج**: هو يقرأ «مسوّدة» ولا يقرأ معرّفاً. / The draft's screen route when it lands. **It never crosses to the model**, which reads 'draft' and no identifier. */
  screenRoute: string | null;
  /** رقم الحدث في الجلسة — يُمرَّر after في الطلب التالي. / The event's number in the session; passed as after on the next request. */
  sequence: number;
  /** الخطوة المرتبطة بالحدث، إن وُجدت. / The step this event belongs to, when there is one. */
  stepId: string | null;
  /** عناوين الخطوات في حدث الخطّة، بترتيبها. وفارغة في كل حدثٍ آخر. / The step titles on a plan event, in order; empty on every other event. */
  steps: string[];
  /** النصّ المعروض — جزءُ تفكيرٍ أو جزءُ نصّ — أو كلامُ البحث في حدث ورقة السؤال. / The displayed text — a thinking or text fragment — or the search words on a question event. */
  text: string | null;
  /** اسم الأداة في أحداث الأدوات. / The tool's name on tool events. */
  toolName: string | null;
  /** الدور الذي أنتج الحدث. / The turn that produced it. */
  turnId: string;
}

/** صفحةُ أحداثٍ بعد مؤشّر. **وقائمةٌ فارغة ليست نهاية**: هي «لا جديد بعدُ»، ويُعاد الطلب بالمؤشّر نفسه — والذي يقول هل انتهى الدور هو phase لا فراغُ القائمة. / A page of events after a cursor. **An empty list is not the end**: it means 'nothing new yet' and the request is retried with the same cursor; what says the turn is over is phase, not an empty list. */
export interface AgentTurnEventPage {
  /** الأحداث بترتيبها. / The events in order. */
  events: AgentTurnEvent[];
  /** آخر مؤشّرٍ في هذه الصفحة، أو المُمرَّر إن كانت فارغة. / The last cursor in this page, or the one passed in when it is empty. */
  lastSequence: number;
  /** طور الدور لحظةَ الجواب. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The turn's phase at the moment of reply. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  phase: "awaitingHuman" | "completed" | "refused" | "running";
}

/** شرائح أعمار الديون. وtotal مجموع الشرائح بالضبط — يُرسَل محسوباً ولا يُترك لكل عميل أن يجمعه فيختلف تقريران عن الرقم نفسه. / Debt aging bands. total is exactly the sum of the bands — sent computed rather than left for each client to add up, which is how two reports come to disagree about one number. */
export interface AgingBands {
  days1To30: Money;
  days31To60: Money;
  days61To90: Money;
  notDue: Money;
  over90: Money;
  total: Money;
}

/** أعمار ديون طرف واحد. / One party's aged debt. */
export interface AgingParty {
  bands: AgingBands;
  /** رمز الطرف. / The party code. */
  code: string;
  name: LocalizedText;
  /** معرّف الطرف. / The party identifier. */
  partyId: string;
}

/** تقرير أعمار ديون — **بالشكل نفسه للمدينة والدائنة**. شكلان مختلفان كانا سيجعلان مقارنة الذمم بالذمم عملاً يدوياً عند كل عميل. / A debt aging report — **the same shape for receivables and payables**. Two different shapes would make comparing one against the other manual work in every client. */
export interface AgingReport {
  /** تاريخ التقرير. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The report date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  asOf: string;
  /** الأطراف. / The parties. */
  parties: AgingParty[];
  totals: AgingBands;
}

/** طلب تخصيص سند ورد بلا مرجع على مستأجر تبيّن أنه صاحبه. / A request to allocate a receipt that arrived without a reference to the tenant it turns out to belong to. */
export interface AllocationRequest {
  /** المستأجر. / The lessee. */
  lesseeId: string;
}

export interface ApiError {
  /** الرمز الثابت — نقطة الاعتماد البرمجية الوحيدة. لا يُقرأ نصّ رسالة لاتخاذ قرار أبداً. / The stable code — the only thing to program against. Message text is never parsed to make a decision. */
  code: string;
  /** الحقل المعنيّ على السلك. / The wire field concerned. */
  field: string | null;
  /** الرسالة العربية. / The Arabic message. */
  messageAr: string;
  /** الرسالة الإنجليزية. / The English message. */
  messageEn: string;
}

/** شرائح أعمار المتأخرات. والمجموع مجموع الشرائح بالضبط. / The arrears ageing bands. The total is exactly the sum of the bands. */
export interface ArrearsBands {
  days1To30: Money;
  days31To60: Money;
  days61To90: Money;
  notDue: Money;
  over90: Money;
  total: Money;
}

/** متأخرات مستأجر واحد. / One tenant's arrears. */
export interface ArrearsParty {
  bands: ArrearsBands;
  /** رمز المستأجر. / The lessee's code. */
  code: string;
  /** اسمه العربي — السجلّ. / Its Arabic name — the record. */
  nameAr: string;
  /** ترجمات الاسم. / The name's translations. */
  nameTranslations: NameValue[];
  /** معرّف المستأجر. / The lessee's identifier. */
  partyId: string;
}

/** وصف مرفق — البايتات في المخزن وهذا ما في القاعدة. والبصمة هي ما يجعل المسار وحده غير كافٍ: ملفٌّ بُدِّل تحت المسار نفسه يُكتشف. ولا مفتاح كائن هنا: هو مسارٌ فيزيائي يعيش في القاعدة وحدها، والمسار الذي يحتاجه العميل هو contentPath. / An attachment descriptor — the bytes are in the store and this is what is in the database. The digest is what makes a path alone insufficient: a file swapped under the same path is detected. No object key appears here: it is a physical path living in the database alone, and the path a client needs is contentPath. */
export interface Attachment {
  /** عدد البايتات كما كُتبت. / The byte count as written. */
  byteLength: number;
  /** SHA-256 للبايتات، ستّعشرياً صغيراً. ويُعاد حسابه ويُقارَن **قبل** تسليم أي بايتة. / SHA-256 of the bytes, lower-case hex. Recomputed and compared **before** a single byte is served. */
  contentHash: string;
  /** مسار تنزيل البايتات على هذا السطح — ويحتاج تذكرة موقّعة في سلسلة استعلامه. / The path these bytes download from on this surface — it needs a signed ticket in its query string. */
  contentPath: string;
  /** اسم العرض بعد التطهير. للعرض وحده، ولا يدخل أي مسار، وامتداده من البايتات لا من الاسم المُرسَل. / The sanitised display name. For display alone, never part of any path, and its extension comes from the bytes rather than from the name sent. */
  fileName: string;
  /** المعرّف الغامض — لا يُشتقّ من اسم ولا من مسار، ولا يُقرأ منه شيء عن صاحبه. / The opaque identifier — derived from no name and no path, and telling nothing about its owner. */
  id: string;
  /** النوع **المشموم من البايتات**، لا المُعلَن. ومنه وحده تُبنى ترويسة التنزيل. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The type **sniffed from the bytes**, not the declared one. The download header is built from it alone. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  mediaType: "application/pdf" | "image/heic" | "image/jpeg" | "image/png" | "image/tiff" | "image/webp";
  /** معرّف المستند المصدر، أو null لمرفقٍ لا مستند له. / The source document identifier, or null for an attachment with no document. */
  sourceDocumentId: string | null;
  /** رمز نوع المستند المصدر، أو null. رمزٌ لا نصٌّ معروض: يُرشَّح به الجرد ولا يُترجَم. / The source document type code, or null. A code, not displayed text: the inventory filters on it and it is never translated. */
  sourceDocumentType: string | null;
  /** لحظة الإيداع. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / The instant of deposit. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  storedAt: string;
  /** من أودع — إنسان، لا نظام. / Who deposited it — a human, not the system. */
  storedBy: string;
  /** خلَفُ هذا الإصدار إن صُحِّح، أو null. والسلسلة خطّية ولا تتفرّع. / The successor of this version if it was corrected, or null. The chain is linear and does not fork. */
  supersededBy: string | null;
  /** سلفُ هذا الإصدار، أو null للإصدار الأول. / The predecessor of this version, or null for the first. */
  supersedes: string | null;
  /** رقم الإصدار — يبدأ بواحد ويزيد مع كل تصحيح. / The version number — starts at one and rises with each correction. */
  version: number;
  /** علامة السحب إن سُحب، أو null. **والبايتات والبصمة باقيتان في الحالتين.** / The withdrawal marker if withdrawn, or null. **The bytes and the digest remain in both cases.** */
  withdrawal: AttachmentWithdrawal | null;
}

/** صفحة من جرد المرفقات ومعها المجموع الكلّي — لا «هل بعدها المزيد؟» وحدها: من يبني ترقيم صفحات يحتاج العدد ليعرف كم صفحة، ولا يستطيع اشتقاقه من صفحةٍ واحدة. / A page of the attachment inventory with the overall total — not merely 'is there more?': building pagination needs the count to know how many pages, and that cannot be derived from a single page. */
export interface AttachmentPage {
  /** الصفوف، الأحدث أولاً. / The rows, newest first. */
  items: Attachment[];
  /** عدد الصفوف المتخطّاة كما نُفِّذت. / Rows skipped, as executed. */
  skip: number;
  /** حجم الصفحة كما نُفِّذ. / The page size, as executed. */
  take: number;
  /** مجموع ما يطابق الترشيح داخل هذه الشركة. / The total matching the filter within this company. */
  total: number;
}

/** تذكرة تنزيل موقّعة. وهي ما يُعطى للمتصفّح لا المسار ولا المعرّف، ومستأجرها **داخل** البايتات الموقّعة لا بجانبها. ولا تُبطَل قبل انتهائها. / A signed download ticket. It is what a browser is given — not the path and not the identifier — and its tenant is **inside** the signed bytes rather than beside them. It is never revoked before it expires. */
export interface AttachmentTicket {
  /** المرفق الذي تفتحه هذه التذكرة، ولا تفتح غيره. / The attachment this ticket opens, and no other. */
  attachmentId: string;
  /** المسار الكامل الذي تُنزَّل به البايتات بهذه التذكرة. / The complete path the bytes download from with this ticket. */
  contentPath: string;
  /** لحظة الانتهاء. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / The expiry instant. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  expiresAt: string;
  /** الرمز الموقّع. نصٌّ مبهم لا بنية فيه: لا يُحلَّل ولا يُشتقّ منه شيء، ويُقدَّم كما وصل. / The signed token. An opaque string with no structure: never parsed, nothing derived from it, presented exactly as received. */
  token: string;
}

/** علامة سحب — لا حذف. صفٌّ في جدول ثانٍ، والبايتات باقية والبصمة باقية: الاحتفاظ بسند القيد واجب نظامي. / A withdrawal marker, not a deletion. A row in a second table; the bytes and the digest remain, because retaining the evidence behind an entry is a regulatory duty. */
export interface AttachmentWithdrawal {
  /** مفتاح السبب: رمزٌ يقرؤه برنامج من مجموعة يملكها المستدعي، لا نصٌّ يُعرض. / The reason key: a code a program reads, from a set the caller owns — not text for display. */
  reasonKey: string;
  /** لحظة السحب. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / The instant of withdrawal. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  withdrawnAt: string;
  /** من سحبه — إنسان، لا نظام. / Who withdrew it — a human, not the system. */
  withdrawnBy: string;
}

/** بند جدول كميات **بمعرّفه** — وهو مدخل سطر المستخلص. / A bill-of-quantities line **with its identifier** — the input to a certificate line. */
export interface BoqItem {
  /** الأمر التغييري الذي أدخل هذا البند، أو null لبنود العقد الأصلي. / The change order that added this line, or null for the original contract's lines. */
  changeOrderId: string | null;
  /** الرمز. / The code. */
  code: string;
  contractQuantity: Measure;
  /** البيان. / The description. */
  descriptionAr: string;
  /** المعرّف — وهو ما يُرسَل في سطر المستخلص. / The identifier — what a certificate line sends. */
  id: string;
  /** ترتيب البند. / The line's ordinal. */
  lineNo: number;
  unitRate: Money;
}

/** بنود جدول الكميات بمعرّفاتها. / The bill-of-quantities lines with their identifiers. */
export interface BoqItemList {
  /** عدد البنود. / The number of lines. */
  itemCount: number;
  /** البنود مرتَّبة بترتيبها. / The lines in their order. */
  items: BoqItem[];
}

/** بند جدول كميات في طلب. **ولا رمز حساب فيه**: البند وحدة تسعير داخل المشروع، والمصفوفة وحدها تقرّر الحساب (القاعدة 2). / A bill-of-quantities line in a request. **No account code appears in it**: the line is a pricing unit inside the project, and the matrix alone decides the account (Rule 2). */
export interface BoqItemRequest {
  /** رمز البند داخل العقد. / The line's code within the contract. */
  code: string;
  contractQuantity: Measure;
  /** بيان البند بالعربية. / The line's Arabic description. */
  descriptionAr: string;
  unitRate: Money;
}

export interface CapabilityProfile {
  /** الأشكال مرتَّبة بنوع المستند. / The shapes ordered by document type. */
  documents: DocumentShape[];
}

/** مفتاح قدرة واحد. / One capability switch. */
export interface CapabilitySwitch {
  /** رمز القدرة من المجموعة المغلقة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The capability code from the closed set. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  capability: "advance" | "cost_of_sales" | "landed_cost" | "retention" | "three_way_match";
  /** مُشغَّلة أم لا. / Enabled or not. */
  enabled: boolean;
}

/** مستخلص بحالته وسطوره وبنوده المعلَّقة. **ولا مبالغ محسوبة فيه**: قيمة الأعمال والضريبة والمحتجز واسترداد الدفعة أربعةٌ لكلٍّ منها حاسبٌ يجب أن يعيش في الوحدة، ولم يُبنَ أيٌّ منها لأن أساسه بندٌ معلَّق — وعرضُ رقمٍ قبل أن يُحسم أساسه أسوأ من غيابه. / A certificate with its state, its lines, and its pending items. **It carries no computed amounts**: works value, tax, retention, and advance recovery each need a calculator living in the module, and none has been built because each rests on a pending decision — showing a figure before its basis is settled is worse than its absence. */
export interface Certificate {
  /** true حين ردّ هذا النداءُ ترحيلاً سابقاً بالهوية نفسها، ومعه رمز 200 بدل 201. / true when this call returned an earlier posting with the same identity, alongside 200 instead of 201. */
  alreadyPosted: boolean;
  /** معرّف القيد إن رُحّل، وnull قبل ذلك. / The entry identifier if posted, and null before that. */
  entryId: string | null;
  /** المعرّف. / The identifier. */
  id: string;
  /** السطور بترتيبها. / The lines in their order. */
  lines: CertificateLine[];
  /** الرقم المرئي. / The visible number. */
  number: string;
  /** العقد أو عقد الباطن. / The contract or subcontract. */
  ownerId: string;
  /** البنود المعلَّقة التي تمنع ترحيله. / The pending items that block posting it. */
  pendingPolicy: PendingPolicyItem[];
  /** بداية الفترة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The period's start. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodFrom: string;
  /** نهاية الفترة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The period's end. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodTo: string;
  retentionRate: Rate;
  /** التسلسل داخل العقد. / The sequence within the contract. */
  sequenceNo: number;
  /** حالة المستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The document state. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "DRAFT" | "POSTED";
}

/** سطر مستخلص بكمّيتيه: التراكمية والسابقة، **وكلٌّ بوحدتها**. والسابقة من آخر مستخلصٍ مُرحَّل لا من آخر مسوّدة — ومسوّدةٌ تُزيح الأساس تُنتج إيراداً مضاعفاً أو ناقصاً بلا رسالة. / A certificate line with both quantities: cumulative and previous, **each with its unit**. The previous one comes from the last posted certificate, never from the last draft — a draft that shifts the base produces doubled or missing revenue with no message. */
export interface CertificateLine {
  amount: Money;
  cumulativeQuantity: Measure;
  /** البيان. / The description. */
  descriptionAr: string;
  /** معرّف السطر. / The line identifier. */
  id: string;
  /** رمز البند، أو نصّ فارغ على سطر غرامة أو خصم. / The item's code, or an empty string on a penalty or deduction line. */
  itemCode: string;
  /** معرّف البند، أو null. / The item identifier, or null. */
  itemId: string | null;
  /** صنف السطر. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The line kind. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  lineKind: "WORK" | "PENALTY" | "DEDUCTION";
  /** الترتيب. / The ordinal. */
  lineNo: number;
  previousQuantity: Measure;
}

/** سطر مستخلص في طلب. **والكمّية تراكمية**: ما نُفِّذ حتى نهاية الفترة لا ما نُفِّذ فيها، وقيمة الفترة تُشتقّ طرحاً من آخر مستخلصٍ **مُرحَّل**. وسطر الغرامة أو الخصم يحمل مبلغه وحده بلا بند وبلا كمّية. / A certificate line in a request. **The quantity is cumulative**: what has been executed to the end of the period, not what was executed within it, and the period's value is derived by subtracting the last **posted** certificate. A penalty or deduction line carries only its amount, with no item and no quantity. */
export interface CertificateLineRequest {
  amount: Money;
  cumulativeQuantity: Measure;
  /** بيان السطر بالعربية. / The line's Arabic description. */
  descriptionAr: string;
  /** بند جدول الكميات أو بند عقد الباطن، أو null على سطر غرامة أو خصم. / The bill-of-quantities line or subcontract line, or null on a penalty or deduction line. */
  itemId: string | null;
  /** صنف السطر: WORK عملٌ منفَّذ · PENALTY غرامة تأخير · DEDUCTION خصم آخر. والغرامة سطرٌ مستقلّ لا خصمٌ من قيمة الأعمال. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The line kind: WORK for executed work, PENALTY for a delay penalty, DEDUCTION for another deduction. A penalty is an independent line, never netted against the works value. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  lineKind: "WORK" | "PENALTY" | "DEDUCTION";
}

/** مستخلصات عقدٍ بتسلسلها. / A contract's certificates in sequence. */
export interface CertificateList {
  /** عددها. / Their count. */
  certificateCount: number;
  /** المستخلصات مرتَّبة بتسلسلها. / The certificates ordered by sequence. */
  certificates: Certificate[];
}

/** طلب إنشاء مستخلص **مسوّدة** — عميلٍ كان أو باطن. ورقمه المرئي يرسله العميل ويُتحقَّق من تفرّده؛ **ولا SEQUENCE ولا IDENTITY لأي رقم يراه مستخدم أو مدقّق**. / A request to create a **draft** certificate, client or subcontractor. Its visible number is sent by the client and checked for uniqueness; **no SEQUENCE and no IDENTITY backs any number a user or auditor reads**. */
export interface CertificateRequest {
  /** سطور المستخلص. / The certificate's lines. */
  lines: CertificateLineRequest[];
  /** الرقم المرئي. / The visible number. */
  number: string;
  /** العقد أو عقد الباطن الذي يقع تحته المستخلص. / The contract or subcontract this certificate falls under. */
  ownerId: string;
  /** بداية فترة المستخلص ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The certificate period's start. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodFrom: string;
  /** نهاية فترة المستخلص ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The certificate period's end. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodTo: string;
  /** تسلسل المستخلص داخل عقده — وهو التفرّد الذي يقوم عليه الاشتقاق التراكمي. / The certificate's sequence within its contract — the uniqueness the cumulative derivation rests on. */
  sequenceNo: number;
}

/** حكم إعادة التحقق. ولماذا «أول تسلسل منحرف» لا «هل السلسلة سليمة»: المدقّق يسأل أين ومتى وما الذي بعده يجب أن يُراجَع؛ وإجابة منطقية واحدة لا تصلح تقريراً. / The re-verification verdict. Why the first divergent sequence rather than a boolean: an auditor asks where, when, and what after it must be reviewed — a single boolean is not a report. */
export interface ChainVerification {
  /** عدد السجلات المفحوصة، بما فيها السجل المنحرف. / The number of records checked, including the divergent one. */
  checked: number;
  /** تفاصيل فنّية: البصمات المتوقّعة والمخزَّنة. / Technical detail: the expected and stored hashes. */
  detail: string | null;
  /** أول رقم تسلسل منحرف، أو null. / The first divergent sequence number, or null. */
  firstDivergentSequence: Int64String | null;
  /** هل النطاق سليم كاملاً؟ / Is the whole scope intact? */
  ok: boolean;
  /** شرح عربي صالح للعرض في تقرير تدقيق. / An Arabic explanation fit for an audit report. */
  reasonAr: string;
  /** رمز الحكم الثابت. / The stable verdict code. */
  verdict: string;
}

/** طلب تغيير دور عضوية. ودورٌ لا أثر له زينة: Reader يقرأ ولا يكتب، وOwner يدعو ويسحب ويغيّر الأدوار. / A membership role-change request. A role with no effect is decoration: Reader reads and writes nothing, and Owner invites, revokes, and changes roles. */
export interface ChangeMembershipRoleRequest {
  /** الدور المطلوب. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The requested role. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  role: "Reader" | "Contributor" | "Owner";
}

/** أمر تغييري ببنوده الجديدة. **ولا entryId ولا alreadyPosted فيه** — لأنه لا يُرحَّل أبداً، وحقلٌ فارغ لهما يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً». / A change order with the lines it added. **No entryId and no alreadyPosted** — it never posts, and an empty value for either reads as 'not posted yet' rather than 'never posted'. */
export interface ChangeOrder {
  /** البنود الجديدة بمعرّفاتها. / The added lines with their identifiers. */
  addedItems: BoqItem[];
  /** المعتمِد. / The approver. */
  approvedBy: string;
  /** العقد. / The contract. */
  contractId: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** تاريخ الإصدار ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The issue date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** الرقم. / The number. */
  number: string;
  /** السبب. / The reason. */
  reasonAr: string;
}

/** أوامر عقدٍ التغييرية. / A contract's change orders. */
export interface ChangeOrderList {
  /** عددها. / Their count. */
  changeOrderCount: number;
  /** الأوامر مرتَّبة برقمها. / The orders ordered by number. */
  changeOrders: ChangeOrder[];
}

/** طلب تسجيل أمر تغييري ببنوده الجديدة. / A request to register a change order with the lines it adds. */
export interface ChangeOrderRequest {
  /** البنود التي يُدخلها الأمر على جدول الكميات. / The lines this order adds to the bill of quantities. */
  addedItems: BoqItemRequest[];
  /** من اعتمد الأمر — والاعتماد فعلٌ يُنسب لا حقلٌ يُملأ. / Who approved the order — approval is an act that is attributed, not a field that is filled. */
  approvedBy: string;
  /** العقد. / The contract. */
  contractId: string;
  /** تاريخ إصدار الأمر ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The order's issue date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** رقم الأمر. / The order number. */
  number: string;
  /** سبب التغيير بالعربية. / The Arabic reason for the change. */
  reasonAr: string;
}

/** طلب تغيير الخطّة. والسند إلزامي: الاستحقاق يحكم أي بيانات مالية يجوز إنشاؤها، فتغييره حدث تدقيقي لا إعداد واجهة. / A plan-change request. Authority is mandatory: entitlement governs which financial data may be created, so changing it is an audit event, not a UI setting. */
export interface ChangePlanRequest {
  /** السند: رقم عقد، أو حدث سداد، أو تذكرة دعم، أو قرار مُوثَّق. **ولا تغيير استحقاق بلا سند**: الاستحقاق يحكم أي بيانات مالية يجوز إنشاؤها، فتغييره حدث تدقيقي. / The authority: a contract number, a payment event, a support ticket, or a documented decision. **No entitlement change without authority**: entitlement governs which financial data may be created, so changing it is an audit event. */
  authority: string;
  /** رمز الخطّة الجديدة من مجموعة الخطط المعروفة؛ ورمزٌ غير معروف يُرفض بـsubscription.plan_unknown ورسالةٍ تُسمّي المعروف. / The new plan's code from the known set; an unknown code is refused with subscription.plan_unknown and a message naming what is known. */
  planCode: string;
  /** سبب التغيير بالعربية — يُكتب في سجلّ تدقيق الاستحقاق. / The change's reason in Arabic — written to the entitlement audit log. */
  reasonAr: string;
}

/** إذن استثنائي بالترحيل في فترة مقفلة. ليس علماً منطقياً بل إذن موثَّق: من أذن وبأي صلاحية ولأي سبب. والفترة المقفلة نهائياً لا يفتحها هذا الإذن ولا غيره. / A documented exceptional permission to post into a closed period — who authorised it, under which permission, and why. A permanently closed period is opened by no permission. */
export interface ClosedPeriodAuthorisation {
  /** معرّف المُصرِّح — مستخدم حقيقي، لا فاعل نظام. / The authoriser — a real user, never a system actor. */
  authorisedBy: string;
  /** رمز الصلاحية الاستثنائية. / The exceptional permission code. */
  permissionCode: string;
  reason: LocalizedText;
}

/** مستند تجاري كما يخرج على السلك — فاتورة مبيعات، أو إشعار دائن، أو فاتورة مورد. / A commercial document as it leaves on the wire — a sales invoice, a credit note, or a supplier bill. */
export interface CommercialDocument {
  /** هل كانت هذه الهوية مُرحَّلة **قبل** هذا الطلب؟ ولا تُشتقّ من state: المستند بعد أي ترحيل ناجح حالته POSTED — الأول والثاني سواء. ورمز الحالة وحده لا يكفي: 200 يضيع خلف أي وسيط يعيد التوجيه. / Was this identity already posted **before** this request? It is not derivable from state: after any successful post the document is POSTED, first arrival and second alike. And the status code alone is not enough: a 200 is lost behind any proxy that redirects. */
  alreadyPosted: boolean;
  /** معرّف القيد إن رُحّل المستند، وnull إن كان مسوّدة. / The journal entry identifier if the document is posted, and null while it is a draft. */
  entryId: string | null;
  gross: Money;
  /** معرّف المستند. / The document identifier. */
  id: string;
  net: Money;
  /** رقم المستند. / The document number. */
  number: string;
  /** الحالة: DRAFT · APPROVED · POSTED · REVERSED · CANCELLED. / The state: DRAFT, APPROVED, POSTED, REVERSED, CANCELLED. */
  state: string;
  tax: Money;
}

/** تأسيس المنشأة كما يُقرأ. **defaultCostCenter غير فارغ أبداً** وcostCenters لا تكون فارغة أبداً — الثابتة مفروضة في النواة بغياب عملية حذف، لا بفحص عند مستدعٍ. / The company setup as read. **defaultCostCenter is never empty** and costCenters is never empty — the invariant is enforced in the core by the absence of any delete operation, not by a caller-side check. */
export interface CompanySetup {
  /** مراكز التكلفة كلّها — العاملة والموقوفة — مرتَّبة برمزها. / All cost centres — active and suspended — ordered by code. */
  costCenters: CostCenter[];
  /** عدد الخانات العشرية المعروضة. عرضٌ وإدخالٌ بشري فقط: المبالغ على السلك تبقى بمقياس Money، والتخزين بأربع خانات. / The number of displayed decimal places. Display and human input only: amounts on the wire keep the Money scale, and storage stays at four places. */
  decimalPlaces: number;
  /** رمز المركز الافتراضي. / The default centre's code. */
  defaultCostCenter: string;
  /** اسم المنشأة بالعربية. / The company's Arabic name. */
  nameAr: string;
  /** ترجمات اسم المنشأة. / The company name's translations. */
  nameTranslations: NameValue[];
}

/** موقف العقد مشتقّاً من المُرحَّل وحده. **وهو بديلٌ لتقرير ربحية المشروع لا نسخةٌ منه**: قاعدة تحميل تكلفة الموظف والمعدّة على المشروع لم تُحسم، وثلاثة حسابات تكلفة مشاريع قائمة في الدليل بلا كاتب — فرقمُ ربحيةٍ مقنع بلا قاعدة معلنة أسوأ من غيابه. / The contract's position derived from posted entries alone. **It replaces a project profitability report rather than being one**: the rule for charging labour and equipment cost to a project is unsettled, and three project cost accounts stand in the chart with no writer — a convincing profitability figure with no published basis is worse than its absence. */
export interface ContractPosition {
  advanceOutstanding: Money;
  /** العقد. / The contract. */
  contractId: string;
  /** رقمه. / Its number. */
  contractNumber: string;
  /** البنود المعلَّقة. / The pending items. */
  pendingPolicy: PendingPolicyItem[];
  /** عدد المستخلصات المُرحَّلة على هذا العقد. / The number of posted certificates on this contract. */
  postedCertificateCount: number;
  retentionOutstanding: Money;
}

/** نتيجة تحويلٍ **وقع بلا باقٍ**. والمعامل يخرج معها بسطاً ومقاماً كي يُراجَع الناتج بلا استعلامٍ ثانٍ، ولا يُقرأ رقمٌ بلا الطريق الذي أنتجه. / The result of a conversion **that divided exactly**. The factor comes back with it as numerator and denominator so the result can be checked without a second call, and no number is read without the route that produced it. */
export interface ConversionResult {
  /** مقام المعامل المُستعمَل. / The denominator of the factor used. */
  denominator: number;
  from: Measure;
  /** بسط المعامل المُستعمَل. / The numerator of the factor used. */
  numerator: number;
  /** صنف الكمّية المشترك. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The shared quantity class. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  quantityClass: "COUNT" | "WEIGHT" | "VOLUME" | "LENGTH" | "AREA";
  to: Measure;
}

/** طلب تجربة تحويل — **مسبارٌ لا يكتب شيئاً**. يُجيب بالناتج الدقيق أو بالرفض المُسمّى، ولا يُقرّب في الحالتين. / A conversion trial request — **a probe that writes nothing**. It answers with the exact result or with a named refusal, and rounds in neither case. */
export interface ConversionTrialRequest {
  quantity: Measure;
  /** الوحدة المطلوب التحويل إليها. / The unit to convert into. */
  toUnit: string;
}

export interface CostCenter {
  /** الرمز — الهوية الثابتة التي تحملها سطور القيود. لا يتغيّر بإعادة التسمية ولا يُترجَم. / The code — the stable identity that journal lines carry. Unchanged by renaming and never translated. */
  code: string;
  /** هل هو المركز الافتراضي؟ واحد فقط يحمل true، وواحد دائماً — ولا يُوقَف ما دام كذلك. / Is this the default centre? Exactly one carries true, and always one — and it is never suspended while it is. */
  isDefault: boolean;
  /** الاسم بالعربية — الارتداد المضمون. / The Arabic name — the guaranteed fallback. */
  nameAr: string;
  /** الترجمات مرتَّبة بالوسم. / The translations ordered by tag. */
  nameTranslations: NameValue[];
  /** الحالة: Active يُختار على مستند جديد · Suspended لا يُختار ويبقى مقروءاً ومُبوَّباً في التقارير السابقة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The state: Active is selectable on a new document; Suspended is not, and stays readable and a grouping key in earlier reports. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "Active" | "Suspended";
  /** سبب الإيقاف مكتوباً، أو نصّ فارغ. / The written suspension reason, or an empty string. */
  suspensionReason: string;
}

/** اسم مركز تكلفة — للإضافة أو لإعادة التسمية. ولا رمز فيه: الرمز يسكّه الخادم ولا يتغيّر. / A cost centre's name — to add one or to rename one. It carries no code: the server mints the code and the code never changes. */
export interface CostCenterNameRequest {
  /** الاسم بالعربية. **إلزامي وهو الارتداد المضمون** حين لا تتوفّر ترجمة. / The Arabic name. **Mandatory, and the guaranteed fallback** when no translation is available. */
  nameAr: string;
  /** الترجمات، مفاتيحها أوسمة BCP-47. / The translations, keyed by BCP-47 tags. */
  nameTranslations?: NameValue[];
}

/** طلب إنشاء إشعار دائن مسوّدة. **ولا عميل فيه**: عميله عميل الفاتورة الأصلية، وإعادةُ ذكره تفتح باباً لإشعارٍ على عميل غير عميل فاتورته. / A request to draft a credit note. **It carries no customer**: the customer is the original invoice's customer, and repeating it would open a door to a note against a customer other than its invoice's. */
export interface CreditNoteRequest {
  /** الفاتورة الأصلية — الإشعار لا يوجد بلا أصل، والأصل يجب أن يكون مُرحَّلاً. / The original invoice — a note does not exist without one, and the original must be posted. */
  invoiceId: string;
  /** تاريخ الإصدار. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The issue date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** سطور المرتجع أو التخفيض. / The return or reduction lines. */
  lines: SalesLine[];
  /** رقم الإشعار — فريد داخل المستأجر. / The note number — unique within the tenant. */
  number: string;
}

/** طلب تسجيل سند قبض مسوّدة. ولا مجاميع فيه: المجموع هو received + settlementDiscount وتحسبه الوحدة. **ولا حساب ولا رمز حساب**: settlementMethod مؤهّل دور تحلّه المصفوفة إلى حساب خزينة أو بنك، وtreasuryPartyId طرفٌ في دفتره المساعد لا رقم حساب. / A request to draft a customer receipt. It carries no totals: the total is received + settlementDiscount and the module computes it. **No account and no account code**: settlementMethod is a role qualifier the matrix resolves into a cash or bank account, and treasuryPartyId is a party in its subledger, not an account number. */
export interface CustomerReceiptRequest {
  /** التخصيصات على فواتير مُرحَّلة لهذا العميل. وقائمةٌ فارغة مقبولة شكلاً — سندٌ يُقبض على الحساب — ومجموعها لا يتجاوز received + settlementDiscount. / Allocations against this customer's posted invoices. An empty list is structurally accepted — a receipt on account — and their sum may not exceed received + settlementDiscount. */
  allocations: ReceiptAllocation[];
  /** العميل المقبوض منه. / The customer the amount was collected from. */
  customerId: string;
  /** رقم السند — فريد داخل المستأجر. / The receipt number — unique within the tenant. */
  number: string;
  received: Money;
  /** تاريخ القبض. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The collection date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  receivedOn: string;
  settlementDiscount: Money;
  /** طريقة التسوية — مؤهّل دور تحلّه المصفوفة إلى حساب. المستعمَل اليوم: cash · bank · card_clearing، ويُنشر نصّاً لا مجموعةً مغلقة كما يُنشر التصنيف الضريبي وللسبب نفسه. / The settlement method — a role qualifier the matrix resolves into an account. In use today: cash, bank, card_clearing; published as text rather than a closed set, as the tax classification is and for the same reason. */
  settlementMethod: string;
  /** الخزينة أو الحساب البنكي في دفترها المساعد — **طرفٌ لا رقم حساب**. / The cash box or bank account in its subledger — **a party, not an account number**. */
  treasuryPartyId: string;
}

/** طلب تسجيل عميل. ولا حقل مستأجر ولا حقل شركة فيه — النطاق من الاعتماد ومن المسار. **ولا vatNumber**: حقل مورد لا حقل عميل، وإرساله يُفشل الطلب كلّه. / A customer registration request. No tenant field and no company field — scope comes from the credential and the path. **And no vatNumber**: that is a supplier field, not a customer field, and sending it fails the whole request. */
export interface CustomerRequest {
  /** رمز العميل داخل المستأجر — هوية تحملها مستنداته، لا نصّاً معروضاً. / The customer code within the tenant — an identity its documents carry, not displayed text. */
  code: string;
  creditLimit: Money;
  name: LocalizedText;
  /** مهلة السداد بالأيام — منها يُشتقّ تاريخ الاستحقاق. / The payment terms in days; the due date is derived from them. */
  paymentTermsDays: number;
}

export interface DocumentAdmission {
  /** مقبول. والرفض يخرج مشكلةً بالرمز 422 لا حكماً في هذا الحقل. / Admitted. A refusal leaves as a 422 problem, never as a verdict in this field. */
  admitted: boolean;
  /** رمز نوع المستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The document type code. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  documentType: "projects.client_certificate" | "purchasing.supplier_bill" | "sales.invoice";
  /** الحقول المقبولة مرتَّبة. / The admitted fields, ordinally sorted. */
  fields: string[];
}

/** ملفّ نوع مستند واحد كما يُرسله العميل. **قائمة مفاتيح لا خريطة حرّة**: المفتاح من تعداد معلن، فلا يمرّ اسم لم يقصده أحد ولا يُقرأ مفتاحان بالاسم نفسه. / One document type's profile as the client sends it. **A list of switches, not a free-form map**: the key comes from a declared enumeration, so no unintended name passes and no two keys share a name. */
export interface DocumentProfile {
  /** مفاتيح القدرات. / The capability switches. */
  capabilities: CapabilitySwitch[];
  /** القيم الافتراضية، ومفاتيحها حقول من شكل المستند حصراً. / The defaults; their keys are fields of the document shape only. */
  defaults?: NameValue[];
  /** رمز نوع المستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The document type code. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  documentType: "projects.client_certificate" | "purchasing.supplier_bill" | "sales.invoice";
}

/** شكل مستند **مُشتقّاً** من (هذه الوثيقة × الملفّ). ولاحظ ما ليس فيه: لا تخطيط، ولا ترتيب بصري، ولا شرط، ولا تعبير — تلك أبواب «المنصّة داخل المنصّة» التي رُفضت. / A document's shape **derived** from (this document x the profile). Note what is absent: no layout, no visual order, no condition, no expression — those are the inner-platform doors that were rejected. */
export interface DocumentShape {
  /** كل قدرات هذا النوع في الكتالوج المغلق. / Every capability of this type in the closed catalogue. */
  availableCapabilities: ("advance" | "cost_of_sales" | "landed_cost" | "retention" | "three_way_match")[];
  /** القيم الافتراضية. / The defaults. */
  defaults: NameValue[];
  /** رمز نوع المستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The document type code. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  documentType: "projects.client_certificate" | "purchasing.supplier_bill" | "sales.invoice";
  /** المُشغَّل منها لهذه الشركة. / Those enabled for this company. */
  enabledCapabilities: ("advance" | "cost_of_sales" | "landed_cost" | "retention" | "three_way_match")[];
  /** حقول المستند بهذا الملفّ — الأساسية وحقول القدرات المُشغَّلة، مرتَّبة حرفياً. / The document's fields under this profile — the base fields plus the fields of enabled capabilities, ordinally sorted. */
  fields: string[];
  /** الوحدة المالكة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The owning module. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  module: "Core" | "Ledger" | "Sales" | "Purchasing" | "Compliance" | "Inventory" | "Pos" | "Hr" | "Projects" | "RealEstate" | "Assets" | "Portals" | "Ai";
  /** الاسم بالعربية. **إلزامي وهو الارتداد المضمون**: العربية شكل السجلّ لا تفضيل عرض، والنظام السعودي يوجب مسك الدفاتر بها. وحين لا تتوفّر ترجمة يُعرض هذا النصّ — لا المفتاح ولا الفراغ. / The Arabic name. **Mandatory, and the guaranteed fallback**: Arabic is the form of the record, not a display preference, and Saudi law requires the books to be kept in it. When no translation is available this text is displayed — never the key and never a blank. */
  nameAr: string;
  /** مفتاح الترجمة. تعدّد اللغات هنا يعني الترجمة إلى **أيّ عدد** من اللغات، لا ثنائية عربي/إنجليزي — فلا حقل لغة ثانية في هذا المخطّط. / The translation key. Multilingualism here means translation into **any number** of languages, not an Arabic/English pair — so this schema carries no second-language field. */
  nameKey: string;
}

/** سعر صرف نصّاً بمقياس لا يتجاوز ثمانياً، بالقواعد نفسها التي تحكم المبالغ. / An exchange rate as a string with at most eight decimal places, under the same rules as amounts. */
/* ExchangeRate مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** طلب إنشاء فاتورة مصروف مسوّدة — بلا مخزون ولا مطابقة ثلاثية. / A request to draft an expense bill — no stock, no three-way match. */
export interface ExpenseBillRequest {
  /** مركز التكلفة — بُعد إلزامي على المصروف: مصروفٌ بلا مركز رقمٌ لا يُبوَّب. / The cost centre — mandatory on an expense: an expense without one is a number that cannot be grouped. */
  costCenterId: string;
  /** تصنيف المصروف — مؤهّل الدور. / The expense category — the role qualifier. */
  expenseCategory: string;
  /** تاريخ الفاتورة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The bill date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** السطور. / The lines. */
  lines: PurchaseLine[];
  /** رقم الفاتورة — فريد داخل المستأجر. / The bill number — unique within the tenant. */
  number: string;
  /** معرّف المورد. / The supplier identifier. */
  supplierId: string;
}

/** سطر استلام: أي سطر أمر، وبأي كمية. **ولا سعر فيه**: التكلفة تُحسب في الوحدة بسعر أمر الشراء للكمية المستلمة، وسعرٌ يرسله العميل كان سيصير مصدر حقيقة ثانياً ينحرف عن الأمر. / A goods receipt line: which order line, and how much. **It carries no price**: the cost is computed in the module at the purchase-order price for the quantity received, and a price sent by the client would be a second source of truth able to diverge from the order. */
export interface GoodsReceiptLine {
  /** سطر الأمر المستلَم عليه — معرّفه من مخطّط PurchaseOrderLine. / The order line being received against — its identifier from the PurchaseOrderLine schema. */
  orderLineId: string;
  quantity: Quantity;
}

/** طلب تسجيل استلام بضاعة مسوّدة على أمر شراء. ولا مورد فيه: مورده مورد الأمر، ولا مستودع: مستودعه مستودع الأمر — وإعادة ذكرهما تفتح باب انحراف عن الأمر الذي يُطابَق به لاحقاً. / A request to draft a goods receipt against a purchase order. It carries no supplier — its supplier is the order's — and no warehouse — its warehouse is the order's; repeating either would open a door to drifting from the very order it is later matched against. */
export interface GoodsReceiptRequest {
  /** السطور. استلامٌ بلا سطر يُرفض في الوحدة برمزه. / The lines. A receipt with no line is refused in the module under its own code. */
  lines: GoodsReceiptLine[];
  /** رقم الاستلام — فريد داخل المستأجر. / The receipt number — unique within the tenant. */
  number: string;
  /** أمر الشراء المستلَم عليه. / The purchase order being received against. */
  orderId: string;
  /** تاريخ الاستلام. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The receipt date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  receivedOn: string;
}

/** طلب دعوة عضو. ولا حقل معرّف مستخدم فيه: المعرّف يسكّه الخادم — ومعرّفٌ يرسله العميل يجعل الدعوة طريقاً إلى ربط اعتمادٍ بمستخدمٍ قائم في مستأجرٍ آخر. / An invitation request. It carries no user identifier: the server mints it — a client-sent identifier would make an invitation a route to binding a credential to an existing user in another tenant. */
export interface GrantMembershipRequest {
  /** اسم المدعوّ بالعربية — السجلّ لا ترجمةً أولى (ADR-0021). / The invited person's Arabic name — the record, not a first translation (ADR-0021). */
  displayNameAr: string;
  /** الدور المطلوب. وReader يقرأ ولا يكتب: جلسته تُرفض على كل فعل غير آمن بـmembership.read_only. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The requested role. Reader reads and never writes: its session is refused on every unsafe method with membership.read_only. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  role: "Reader" | "Contributor" | "Owner";
}

/** عضوية مُنحت للتوّ ومعها اعتماد انتسابها. وهذه هي الاستجابة **الوحيدة** التي يخرج فيها اعتماد انتساب، ويخرج فيها **مرّة واحدة**: المُودَع بصمته. فمن دعا عضواً يسلّمه هذا النصّ بنفسه، ولا يوجد في الخادم من يعيده. / A membership just granted together with its enrolment credential. This is the **only** response in which an enrolment credential appears, and it appears **once**: what is persisted is its digest. Whoever invited the member hands the text over themselves; nobody on the server can reproduce it. */
export interface GrantedMembership {
  /** المنشأة. / The company. */
  companyId: string;
  /** اعتماد الانتساب — يُقبل مرّة واحدة ثم يُستهلك. نصٌّ مبهم لا بنية فيه: لا يُحلَّل ولا يُشتقّ منه شيء، ويُقدَّم كما وصل. / The enrolment credential — accepted once, then consumed. An opaque string with no structure: never parsed, nothing derived from it, presented exactly as received. */
  enrolmentCredential: string;
  /** لحظة انقضاء الدعوة. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / When the invitation expires. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  enrolmentExpiresAt: string;
  member: Membership;
}

/** خطاب ضمان — **سجلٌّ لا يُرحَّل أبداً**، ولذلك لا entryId ولا alreadyPosted فيه. / A guarantee — **a record that never posts**, which is why it carries no entryId and no alreadyPosted. */
export interface Guarantee {
  amount: Money;
  /** معرّف المرفق. / The attachment identifier. */
  attachmentId: string;
  /** عقد العميل، أو null. / The client contract, or null. */
  contractId: string | null;
  /** بدء السريان ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The effective date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  effectiveFrom: string;
  /** الانتهاء ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The expiry date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  expiresOn: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** المُصدِر. / The issuer. */
  issuerNameAr: string;
  /** الصنف. / The kind. */
  kind: string;
  /** الرقم. / The number. */
  number: string;
  /** عقد الباطن، أو null. / The subcontract, or null. */
  subcontractId: string | null;
}

/** طلب تسجيل خطاب ضمان. **والمرفق معرّفٌ على السطح المنشور للمرفقات لا بايتات هنا**: خطاب الضمان سندُ إثبات، فيُودَع حيث تُحرَس البصمة والإصدار والسحب. / A request to register a guarantee. **The attachment is an identifier on the published attachment surface, not bytes here**: a guarantee is evidence, so it is deposited where the digest, the revision chain, and the withdrawal are guarded. */
export interface GuaranteeRequest {
  amount: Money;
  /** معرّف المرفق كما أرجعه باب الإيداع. / The attachment identifier as the deposit door returned it. */
  attachmentId: string;
  /** عقد العميل الذي يخصّه الضمان، أو null. / The client contract the guarantee belongs to, or null. */
  contractId: string | null;
  /** بدء سريان الضمان ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The guarantee's effective date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  effectiveFrom: string;
  /** انتهاء الضمان ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The guarantee's expiry date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  expiresOn: string;
  /** اسم الجهة المُصدِرة بالعربية. / The issuing party's Arabic name. */
  issuerNameAr: string;
  /** صنف الضمان: ابتدائي أو حسن تنفيذ أو دفعة مقدمة، برمز يختاره المستأجر. / The guarantee kind — bid, performance, or advance payment — by a code the tenant chooses. */
  kind: string;
  /** رقم الخطاب. / The guarantee number. */
  number: string;
  /** عقد الباطن الذي يخصّه الضمان، أو null. وواحدٌ من الاثنين إلزامي. / The subcontract the guarantee belongs to, or null. One of the two is required. */
  subcontractId: string | null;
}

export interface HealthResponse {
  /** إصدار السطح. / The surface version. */
  apiVersion: string;
  /** التقويم الافتراضي لتلك الثقافة. GregorianCalendar هو المتوقّع؛ UmAlQuraCalendar يعني أن أي تنسيق تاريخ ضمني على هذا الخادم يكتب هجرياً. / The default calendar of that culture. GregorianCalendar is expected; UmAlQuraCalendar means any implicit date formatting on this server writes Hijri. */
  calendar: string;
  /** ثقافة العملية الفعلية. / The actual process culture. */
  culture: string;
  /** الحالة. / The status. */
  status: string;
}

/** سلفة كما تخرج من السطح — **بلا حقل قيد**: باب ترحيلها غير منشور لأن حدثها غير موجود في مصفوفة الترحيل، وحقلٌ فارغ كان سيَعِد بدورة لا تكتمل. و`outstandingAmount` مشتقٌّ من الأقساط **المستقطَعة فعلاً** وحدها لا من مرور الزمن. / An advance as the surface returns it — **with no entry field**: its posting door is unpublished because its event does not exist in the posting matrix, and an empty field would promise a cycle that does not complete. `outstandingAmount` is derived from instalments **actually deducted** alone, not from elapsed time. */
export interface HrAdvance {
  amount: Money;
  /** الرمز المعتم. / The opaque code. */
  employeeCode: string;
  /** الموظف. / The employee. */
  employeeId: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** جدول الأقساط. / The instalment schedule. */
  instalments: HrInstalment[];
  /** تاريخ المنح ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The grant date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** الرقم. / The number. */
  number: string;
  outstandingAmount: Money;
  /** طريقة الصرف. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The disbursement method. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  /** الحالة. / The state. */
  state: string;
  /** طرف الخزينة. / The treasury party. */
  treasuryPartyId: string;
}

/** طلب إنشاء سلفة مسوّدة بجدول أقساطها، **ومجموع الأقساط يساوي المبلغ بالضبط** — والفارق لا يُسوَّى ضمناً: قسطٌ يُخترع أو يُقصّ يجعل رصيد السلفة رقماً لا يقابله جدول. / An advance draft request with its instalment schedule, **whose instalments sum exactly to the amount** — the difference is never settled implicitly: an invented or truncated instalment makes the advance balance a number with no schedule behind it. */
export interface HrAdvanceRequest {
  amount: Money;
  /** الموظف المستلف. / The employee taking the advance. */
  employeeId: string;
  /** جدول الأقساط. / The instalment schedule. */
  instalments: HrInstalmentRequest[];
  /** تاريخ منح السلفة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The date the advance was granted. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** رقم السلفة. / The advance number. */
  number: string;
  /** طريقة الصرف — مؤهّل دور. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The disbursement method — a role qualifier. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  /** طرف الخزينة. / The treasury party. */
  treasuryPartyId: string;
}

/** جزاءٌ كما يخرج من السطح — **بلا entryId وبلا alreadyPosted**. الاستقطاع يُرحَّل داخل المسيّر لا بذاته، وحقلٌ فارغ يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل»، فيبني عليه العميل شاشةً بزرّ ترحيل لا وجود له. / A deduction as the surface returns it — **with no entryId and no alreadyPosted**. A deduction is posted inside the run rather than by itself, and an empty field reads as 'not posted yet' instead of 'never posted', leading a client to build a screen with a posting button that does not exist. */
export interface HrDeduction {
  amount: Money;
  /** المعتمِد. / The approver. */
  approvedBy: string;
  /** تاريخ الاعتماد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The approval date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  approvedOn: string;
  /** مفتاح فئة السبب. / The reason category key. */
  categoryKey: string;
  /** القسيمة التي استُقطع فيها، أو null فلم يُستقطع بعد. / The payslip it was consumed by, or null while it has not been deducted. */
  consumedByPayslipId: string | null;
  /** الرمز المعتم. / The opaque code. */
  employeeCode: string;
  /** الموظف. / The employee. */
  employeeId: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
}

/** قيد جزاء في السجلّ المعتمد، يُستقطع داخل مسيّر فترته. **ولا حدّ أقصى لنسبة الاستقطاع يُفرَض**: الحدّ النظامي غير متحقَّق منه، وحدٌّ مخترَع يرفض مسيّرات مشروعة ويُدرّب المستخدم على الالتفاف. / A deduction recorded in the approved register, deducted inside its period's run. **No maximum deduction ratio is enforced**: the regulatory ceiling is unverified, and an invented ceiling refuses legitimate runs and trains users to work around it. */
export interface HrDeductionRequest {
  amount: Money;
  /** المعتمِد — إنسان، لا نظام. / The approver — a human, not the system. */
  approvedBy: string;
  /** تاريخ الاعتماد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The approval date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  approvedOn: string;
  /** مفتاح فئة السبب — رمزٌ يملكه المستدعي لا نصٌّ يُعرض. / The reason category key — a code the caller owns, not displayed text. */
  categoryKey: string;
  /** الموظف المستقطَع منه. / The employee being deducted from. */
  employeeId: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
}

/** الموظف كما يخرج من السطح — **بلا قيمة شخصية واحدة غير مقنَّعة**. و`code` رمزٌ **معتم** لا يُشتقّ من شيء ولا يُقرأ منه شيء عن صاحبه، وهو ما يظهر في `partyId` عند مطابقة الدفتر المساعد. / The employee as the surface returns it — **with not one unmasked personal value**. `code` is an **opaque** code derived from nothing and telling nothing about its bearer, and it is what appears as `partyId` in the subledger reconciliation. */
export interface HrEmployee {
  /** تصنيف الاشتراك. / The contribution class. */
  classCode: string;
  /** الرمز المعتم — وهو وحده ما يعبر إلى الدفتر المساعد. / The opaque code — the only thing that crosses into the subledger. */
  code: string;
  /** مركز التكلفة كما سُجّل. / The cost centre as recorded. */
  costCenterId: string;
  /** علاقة العمل الجارية أو الأخيرة — وهي حبيبيّة مخصص نهاية الخدمة. / The current or latest employment — the grain of the end-of-service provision. */
  employmentId: string;
  /** تاريخ انتهاء علاقة العمل، أو null لعلاقة سارية. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية. / The employment end date, or null while it is active. Gregorian, yyyy-MM-dd only, Latin digits. */
  endedOn: string | null;
  /** المعرّف على هذا السطح. / The identifier on this surface. */
  id: string;
  identity: HrMaskedIdentity;
  /** الاسم العربي — السجلّ. / The Arabic name — the record. */
  nameAr: string;
  /** الترجمات، مرتَّبة ترتيباً حرفياً ثابتاً. / The translations, in a stable ordinal order. */
  nameTranslations: NameValue[];
  /** تاريخ بدء علاقة العمل ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The employment start date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  startedOn: string;
  /** حالة علاقة العمل: ACTIVE أو TERMINATED. / The employment state: ACTIVE or TERMINATED. */
  state: string;
}

/** طلب تسجيل موظف. **ولا رمز فيه**: الخادم يولّد رمزاً معتماً ولا يقبل واحداً من العميل، لأن الرمز هو ما يُكتب في دفتر أستاذ لا يُمحى منه شيء. والاسم العربي **سجلّ** وترجماته صفوف. / An employee registration request. **It carries no code**: the server mints an opaque one and accepts none from the client, because the code is what gets written into a ledger nothing is erased from. The Arabic name is the **record**, and its translations are rows. */
export interface HrEmployeeRequest {
  /** تصنيف الاشتراك — مؤهّل صفّ الإعدادات، لا نسبة ولا سقف. / The contribution class — a qualifier for the settings row, not a rate and not a ceiling. */
  classCode: string;
  /** مركز التكلفة الذي يُحمَّل عليه أجر هذا الموظف، أو فراغٌ فالافتراضي. وواحدٌ لا أكثر. / The cost centre this employee's pay is charged to, or empty for the default. One, and no more. */
  costCenterId: string;
  /** تاريخ بدء علاقة العمل ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The employment start date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  hiredOn: string;
  identity: HrIdentityRequest;
  /** الاسم العربي — السجلّ، لا ترجمة ثانية. / The Arabic name — the record, not a second translation. */
  nameAr: string;
  /** ترجمات الاسم بوسم BCP-47. والعربية سجلٌّ فلا تدخل هنا. / The name's translations by BCP-47 tag. Arabic is the record and never appears here. */
  nameTranslations?: NameValue[];
}

/** البيانات الشخصية عند التسجيل — **تدخل ولا تعود**. تسكن جدولاً منفصلاً واحداً لواحد، ولا يخرج منها شيء في أي جواب إلا مقنَّعاً، ولا يعبر منها حرفٌ واحد إلى دفتر الأستاذ. / Personal data at registration — **it goes in and does not come back**. It lives in a separate one-to-one table, nothing of it leaves in any response except masked, and not one character of it crosses into the ledger. */
export interface HrIdentityRequest {
  /** تاريخ الميلاد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The date of birth. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  birthDate: string;
  /** الآيبان. لا يعبر إلى الدفتر بحال، ولا يعود إلا مقنَّعاً. / The IBAN. It never crosses into the ledger and never returns except masked. */
  iban: string;
  /** رقم الهوية أو الإقامة. لا يعبر إلى الدفتر بحال، ولا يعود إلا مقنَّعاً. / The national or residence identity number. It never crosses into the ledger and never returns except masked. */
  nationalId: string;
}

/** قسط سلفة كما يخرج من السطح، ومعه القسيمة التي استُقطع فيها إن استُقطع. / An advance instalment as the surface returns it, with the payslip it was deducted in, if any. */
export interface HrInstalment {
  amount: Money;
  /** القسيمة التي استُقطع فيها، أو null. / The payslip it was deducted in, or null. */
  consumedByPayslipId: string | null;
  /** رقم القسط. / The instalment number. */
  lineNo: number;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
}

/** قسط سداد سلفة في الطلب: فترته ومبلغه. / An advance repayment instalment in the request: its period and its amount. */
export interface HrInstalmentRequest {
  amount: Money;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
}

/** الهوية مقنَّعة: آخر أربعة محارف وحدها وما قبلها نجومٌ **بعدد ثابت**. وعددٌ يساوي طول الأصل يُسرّب الطول، وطولُ الآيبان يُميّز بلد إصداره. / A masked identity: the last four characters only, preceded by a **fixed** number of dots. A count matching the original's length would leak the length, and an IBAN's length distinguishes its issuing country. */
export interface HrMaskedIdentity {
  /** قناع الآيبان. / The IBAN mask. */
  ibanMask: string;
  /** قناع رقم الهوية. / The identity number mask. */
  nationalIdMask: string;
}

/** مكوّن أجر كما يخرج من السطح. / A pay component as the surface returns it. */
export interface HrPayComponent {
  /** الرمز. / The code. */
  code: string;
  /** وسم وعاء الاشتراك. / The contributory wage flag. */
  entersContributoryWage: boolean;
  /** وسم وعاء نهاية الخدمة. / The end-of-service base flag. */
  entersEndOfServiceBase: boolean;
  /** المعرّف. / The identifier. */
  id: string;
  /** نوع المكوّن. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The component kind. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  kind: "deduction" | "earning";
  /** الاسم العربي — السجلّ. / The Arabic name — the record. */
  nameAr: string;
  /** الترجمات. / The translations. */
  nameTranslations: NameValue[];
}

/** تصنيفات مكوّنات الأجر، مرتَّبة بالرمز. **وغلافٌ لا مصفوفة عارية**: مصفوفةٌ في جذر الاستجابة لا موضع فيها لعدّاد ولا لصفحة. / The pay component classifications, ordered by code. **An envelope, not a bare array**: an array at the response root has no place for a count or a page. */
export interface HrPayComponentList {
  /** عدد المكوّنات. / The number of components. */
  itemCount: number;
  /** المكوّنات. / The components. */
  items: HrPayComponent[];
}

/** طلب تعريف مكوّن أجر — **تصنيفٌ لا مبلغ ولا نسبة**. والوسمان هما الموضع الذي يصير فيه الأثر التنظيمي بياناتٍ يملؤها المحاسب بدل شيفرة يكتبها مبرمج. / A pay component definition request — **a classification, not an amount and not a rate**. The two flags are where the regulatory effect becomes data an accountant fills instead of code a programmer writes. */
export interface HrPayComponentRequest {
  /** رمز المكوّن داخل المنشأة. / The component code within the company. */
  code: string;
  /** هل يدخل وعاء اشتراك التأمينات؟ يملؤه المحاسب. / Does it enter the social insurance contributory wage? The accountant fills it. */
  entersContributoryWage: boolean;
  /** هل يدخل وعاء مكافأة نهاية الخدمة؟ يملؤه المحاسب. / Does it enter the end-of-service benefit base? The accountant fills it. */
  entersEndOfServiceBase: boolean;
  /** نوع المكوّن: استحقاق أو استقطاع. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The component kind: an earning or a deduction. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  kind: "deduction" | "earning";
  /** الاسم العربي — السجلّ. / The Arabic name — the record. */
  nameAr: string;
  /** ترجمات الاسم. / The name's translations. */
  nameTranslations?: NameValue[];
}

/** قيمة مكوّن كما تخرج من السطح، بتاريخ سريانها. / A component value as the surface returns it, with its effective date. */
export interface HrPayElement {
  amount: Money;
  /** رمز المكوّن. / The component code. */
  componentCode: string;
  /** تاريخ السريان ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The effective date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  effectiveFrom: string;
  /** المعرّف. / The identifier. */
  id: string;
}

/** أجر الموظف بسريانه — **كل الصفوف لا الساري اليوم وحده**، لأن مراجعة مسيّرٍ ماضٍ تحتاج ما كان سارياً حينها. / The employee's pay by effective date — **every row, not only the one in force today**, because reviewing a past run needs what was in force then. */
export interface HrPayElementList {
  /** عدد الصفوف. / The number of rows. */
  itemCount: number;
  /** الصفوف، مرتَّبة بالمكوّن ثم بتاريخ السريان. / The rows, ordered by component then by effective date. */
  items: HrPayElement[];
}

/** طلب إسناد قيمة مكوّن بتاريخ سريان — **إنشاءٌ لا تعديل**. والزيادة صفٌّ جديد، لأن مسيّراً ماضياً رُحِّل قيده يجب أن يُعاد حسابه فيطابقه. / A request to assign a component value from an effective date — **a creation, not an edit**. An increase is a new row, because a past run whose entry was posted must be recomputable and match it. */
export interface HrPayElementRequest {
  amount: Money;
  /** رمز المكوّن المُسنَدة قيمته. / The code of the component being valued. */
  componentCode: string;
  /** تاريخ سريان القيمة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The date the value takes effect. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  effectiveFrom: string;
}

/** المبالغ الستّة **بأسماء مفردات مصفوفة الترحيل نفسها**، فما يُقرأ هنا هو ما يُمرَّر إلى المحرك حرفاً بحرف. والمتطابقة المعلَنة في المصفوفة مفروضة في قاعدة البيانات: netPayable = grossEntitlements − employeeSocialInsurance − advanceInstalment − deductions. / The six amounts **under the posting matrix's own vocabulary names**, so what is read here is what is passed to the engine verbatim. The identity declared in the matrix is enforced in the database: netPayable = grossEntitlements − employeeSocialInsurance − advanceInstalment − deductions. */
export interface HrPayrollAmounts {
  advanceInstalment: Money;
  deductions: Money;
  employeeSocialInsurance: Money;
  employerSocialInsurance: Money;
  grossEntitlements: Money;
  netPayable: Money;
}

/** سند صرف الرواتب كما يخرج من السطح، بسطوره ومعرّفات قيودها. / The payroll payment as the surface returns it, with its lines and their entry identifiers. */
export interface HrPayrollPayment {
  /** هل كانت سطوره كلّها مُرحَّلة قبل هذا النداء؟ / Were all of its lines already posted before this call? */
  alreadyPosted: boolean;
  /** المعرّف. / The identifier. */
  id: string;
  /** السطور — واحدٌ لكل قسيمة. / The lines — one per payslip. */
  lines: HrPayrollPaymentLine[];
  netPayable: Money;
  /** الرقم. / The number. */
  number: string;
  /** تاريخ الصرف ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The payment date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  paidOn: string;
  /** المسيّر. / The run. */
  runId: string;
  /** طريقة التسوية. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The settlement method. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  /** الحالة: DRAFT أو POSTED. / The state: DRAFT or POSTED. */
  state: string;
  /** طرف الخزينة. / The treasury party. */
  treasuryPartyId: string;
}

/** سطر سند صرف — **واحدٌ لكل قسيمة، وهو حبيبيّة القيد**. ولو كان القيد واحداً للسند لاختلفت حبيبيّة طرفَي المطابقة واستحالت. / A payment line — **one per payslip, and it is the entry's grain**. Were the entry one per document, the two sides of the reconciliation would carry different grains and it would be impossible. */
export interface HrPayrollPaymentLine {
  amount: Money;
  /** الرمز المعتم — طرف الدفتر المساعد. / The opaque code — the subledger party. */
  employeeCode: string;
  /** معرّف قيد هذا السطر إن رُحّل، أو null. / This line's entry identifier if posted, or null. */
  entryId: string | null;
  /** رقم السطر. / The line number. */
  lineNo: number;
  /** القسيمة التي يُصرف صافيها. / The payslip whose net is being paid. */
  payslipId: string;
}

/** طلب إنشاء سند صرف رواتب مسوّدة على مسيّر مُرحَّل. و`treasuryPartyId` **إلزامي**: سطر التسوية معلَنٌ في المصفوفة subledger: "resolved"، والمحرك يطويه إلى النوع none ثم يبحث عن الواقعة subledger.none — وحسابُ التسوية الافتراضي حسابٌ ضابط، فبلا الطرف يُرفض الترحيل كلّه. / A payroll payment draft request against a posted run. `treasuryPartyId` is **mandatory**: the settlement line is declared in the matrix as subledger: "resolved", the engine folds that to kind none and then looks for the subledger.none fact — and the default settlement account is a control account, so without the party the whole posting is refused. */
export interface HrPayrollPaymentRequest {
  /** رقم السند — فريد داخل المنشأة. / The document number — unique within the company. */
  number: string;
  /** تاريخ الصرف ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The payment date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  paidOn: string;
  /** المسيّر المُرحَّل الذي يُصرف. / The posted run being paid. */
  runId: string;
  /** طريقة التسوية — **مؤهّل دور لا رمز حساب**. والمجموعة ضيّقة عمداً: قبولُ حساب وسيط يفترض جواب سؤال مفتوح عن لحظة وقوع قيد الصرف. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The settlement method — **a role qualifier, not an account code**. The set is deliberately narrow: accepting a clearing account would assume the answer to an open question about when the payment entry falls. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  /** طرف الخزينة أو الحساب المصرفي في دفترها المساعد. / The treasury or bank party within its own subledger. */
  treasuryPartyId: string;
}

/** مسيّر رواتب كما يخرج من السطح. **ولا معرّف قيد عليه**: القيود على قسائمه لا عليه — قيدٌ لكل قسيمة. / A payroll run as the surface returns it. **It carries no entry identifier**: the entries belong to its payslips, not to it — one entry per payslip. */
export interface HrPayrollRun {
  amounts: HrPayrollAmounts;
  /** المعرّف. / The identifier. */
  id: string;
  /** الرقم. / The number. */
  number: string;
  /** عدد القسائم — وهو عدد القيود التي يُصدرها الترحيل. / The payslip count — which is the number of entries posting issues. */
  payslipCount: number;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
  /** نهاية الفترة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The period end. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodEnd: string;
  /** بداية الفترة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The period start. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodStart: string;
  /** الحالة: DRAFT أو POSTED. / The state: DRAFT or POSTED. */
  state: string;
}

/** طلب إنشاء مسيّر رواتب مسوّدة. **ولا مجاميع فيه**: مجموعٌ يرسله العميل مصدرُ حقيقةٍ ثانٍ ينحرف عن الأول ولا يُظهره شيء. / A payroll run draft request. **It carries no totals**: a total sent by a client is a second source of truth that drifts from the first with nothing to reveal it. */
export interface HrPayrollRunRequest {
  /** رقم المسيّر — فريد داخل المنشأة. / The run number — unique within the company. */
  number: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
  /** نهاية الفترة — وهي تاريخ قيد الاستحقاق ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The period end — the accrual entry's date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodEnd: string;
  /** بداية الفترة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The period start. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodStart: string;
}

/** إصدار نِسَبٍ كما يخرج من السطح، بمعتمِده ومصدره وتاريخ سريانه. / A rate version as the surface returns it, with its approver, its source, and its effective date. */
export interface HrPayrollSettings {
  /** المعتمِد. / The approver. */
  approvedBy: string;
  /** تاريخ الاعتماد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The approval date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  approvedOn: string;
  /** التصنيف. / The class. */
  classCode: string;
  /** تاريخ السريان ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The effective date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  effectiveFrom: string;
  employeeRate: TaxRate;
  employerRate: TaxRate;
  /** المعرّف. / The identifier. */
  id: string;
  maximumContributoryWage: Money;
  minimumContributoryWage: Money;
  /** مرجع المصدر النظامي. / The regulatory source reference. */
  sourceRef: string;
}

/** إصدارات النِّسَب بسريانها. **وقائمة فارغة جوابٌ صحيح**: هي حال المنشأة قبل أن يعتمد محاسبها أول إصدار، وهي الحال التي يُرفض فيها كل مسيّر. / The rate versions by effective date. **An empty list is a correct answer**: it is the company's state before its accountant approves a first version, and the state in which every run is refused. */
export interface HrPayrollSettingsList {
  /** عدد الإصدارات. / The number of versions. */
  itemCount: number;
  /** الإصدارات، مرتَّبة بالتصنيف ثم بتاريخ السريان. / The versions, ordered by class then by effective date. */
  items: HrPayrollSettings[];
}

/** إيداع إصدار من نِسَب الاشتراك وحدودها — **وهذا هو الموضع الوحيد الذي تدخل منه نسبة إلى هذا النظام**. والنِّسَب TaxRate بمقياس ثمانٍ لا Money: خمسة عشر بالمئة تُكتب 0.15 لا 15. و`sourceRef` **غير فارغ بقيدٍ في قاعدة البيانات**: نسبةٌ بلا مصدر مكتوب مرفوضة عند الكتابة لا عند المراجعة. / Depositing a version of the contribution rates and their limits — **the only place a rate enters this system**. Rates are TaxRate at scale eight, not Money: fifteen percent is 0.15, never 15. `sourceRef` is **non-empty by a database constraint**: a rate with no written source is refused at write time, not at review time. */
export interface HrPayrollSettingsRequest {
  /** من اعتمد الإصدار — إنسان، لا نظام. / Who approved the version — a human, not the system. */
  approvedBy: string;
  /** تاريخ اعتماد هذا الإصدار ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The date this version was approved. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  approvedOn: string;
  /** تصنيف الاشتراك الذي تسري عليه هذه النِّسَب. / The contribution class these rates apply to. */
  classCode: string;
  /** تاريخ سريان الإصدار ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The date the version takes effect. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  effectiveFrom: string;
  employeeRate: TaxRate;
  employerRate: TaxRate;
  maximumContributoryWage: Money;
  minimumContributoryWage: Money;
  /** مرجع المصدر النظامي الذي أُخذت منه هذه القيم — نصٌّ يقرؤه مراجع، وغير فارغ. / The reference to the regulatory source these values came from — text a reviewer reads, and non-empty. */
  sourceRef: string;
}

/** القسيمة — **وهي مستند الترحيل**: معرّفها هو DocumentId في هوية الإحكام السداسية، و`entryId` قيدُها هي وحدها. و`employeeCode` هو الرمز المعتم الذي كُتب في الدفتر المساعد وقت الترحيل، محفوظاً على الصفّ كي يبقى مطابقاً لما في الدفتر مهما تغيّر بعده. / The payslip — **which is the posting document**: its identifier is the DocumentId in the six-part idempotency identity, and `entryId` is its own entry. `employeeCode` is the opaque code written into the subledger at posting time, stored on the row so that it keeps matching the ledger whatever changes afterwards. */
export interface HrPayslip {
  /** هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟ **معلومة لا تُشتقّ من الحالة**: قسيمةٌ حالتها POSTED بعد النداء لا تقول أيُّ النداءين رحّلها. / Was this identity already posted before this call? **Not derivable from the state**: a payslip in state POSTED after the call does not say which call posted it. */
  alreadyPosted: boolean;
  amounts: HrPayrollAmounts;
  /** تفصيل المكوّنات — فارغٌ في القوائم، ومملوءٌ عند قراءة القسيمة مفردةً. / The component breakdown — empty in listings, populated when the payslip is read on its own. */
  components: HrPayslipComponent[];
  contributoryWage: Money;
  /** مركز التكلفة كما كان وقت بناء القسيمة. / The cost centre as it stood when the payslip was built. */
  costCenterId: string;
  /** الرمز المعتم — وهو طرف الدفتر المساعد. / The opaque code — the subledger party. */
  employeeCode: string;
  /** معرّف الموظف على هذا السطح. / The employee identifier on this surface. */
  employeeId: string;
  /** علاقة العمل. / The employment. */
  employmentId: string;
  /** معرّف قيد هذه القسيمة إن رُحّلت، أو null. / This payslip's entry identifier if posted, or null. */
  entryId: string | null;
  /** المعرّف — وهو DocumentId في هوية الإحكام. / The identifier — the DocumentId in the posting identity. */
  id: string;
  /** المسيّر الذي بُنيت فيه. / The run it was built in. */
  runId: string;
  /** الحالة: DRAFT أو POSTED. / The state: DRAFT or POSTED. */
  state: string;
}

/** مكوّن على قسيمة — تفصيل ما بُني منه المبلغ، ليُراجَع. ووسم دخوله الوعاء محفوظٌ على السطر نفسه: من يراجع بعد سنة يحتاج الوسم كما كان لا كما صار. / A component on a payslip — the breakdown the amount was built from, for review. Its contributory flag is stored on the line itself: whoever reviews a year later needs the flag as it was, not as it became. */
export interface HrPayslipComponent {
  amount: Money;
  /** رمز المكوّن. / The component code. */
  componentCode: string;
  /** هل دخل هذا المكوّن وعاء الاشتراك وقت بناء القسيمة؟ / Did this component enter the contributory wage when the payslip was built? */
  entersContributoryWage: boolean;
  /** نوع المكوّن. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The component kind. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  kind: "deduction" | "earning";
  /** رقم السطر داخل القسيمة. / The line number within the payslip. */
  lineNo: number;
}

/** قسائم مسيّر — **وهو أيضاً جواب باب الترحيل**: نداءٌ واحد يُصدر قيداً لكل قسيمة، فالجواب قائمة قسائم لكلٍّ معرّف قيدها وحصانتها، لا مستنداً واحداً بمعرّف قيد واحد. / A run's payslips — **and also the posting door's response**: one call issues one entry per payslip, so the response is a list of payslips each with its own entry identifier and its own idempotency flag, not one document with one entry id. */
export interface HrPayslipList {
  /** عدد القسائم. / The number of payslips. */
  itemCount: number;
  /** القسائم، مرتَّبة بالرمز المعتم. / The payslips, ordered by opaque code. */
  items: HrPayslip[];
}

/** مستند استحقاق المخصص كما يخرج من السطح، بحركاته ومعرّفات قيودها — قيدٌ لكل علاقة عمل. / The provision accrual document as the surface returns it, with its movements and their entry identifiers — one entry per employment. */
export interface HrProvision {
  /** تاريخ الاستحقاق ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The accrual date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  accruedOn: string;
  /** هل كانت حركاته كلّها مُرحَّلة قبل هذا النداء؟ / Were all of its movements already posted before this call? */
  alreadyPosted: boolean;
  /** المعتمِد. / The approver. */
  approvedBy: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** مرجع أساس القياس المعتمد. / The approved measurement basis reference. */
  measurementRef: string;
  /** الحركات، مرتَّبة بالرمز المعتم. / The movements, ordered by opaque code. */
  movements: HrProvisionMovement[];
  /** الرقم. / The number. */
  number: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
  periodShare: Money;
  /** الحالة. / The state. */
  state: string;
}

/** حركة مخصص لعلاقة عمل في فترة — **تُضاف ولا تُعدَّل**، وهي حبيبيّة الطرف المساعد ومصدر الرصيد الذي تقرأه المخالصة. / A provision movement for one employment in one period — **appended, never edited** — and it is the subledger party's grain and the source of the balance the settlement reads. */
export interface HrProvisionMovement {
  /** الرمز المعتم. / The opaque code. */
  employeeCode: string;
  /** علاقة العمل. / The employment. */
  employmentId: string;
  /** معرّف قيد هذه الحركة إن رُحّلت، أو null. / This movement's entry identifier if posted, or null. */
  entryId: string | null;
  /** المعرّف — وهو DocumentId في هوية إحكام هذه الحركة. / The identifier — the DocumentId in this movement's posting identity. */
  id: string;
  periodShare: Money;
}

/** طلب إنشاء مستند استحقاق مخصص نهاية الخدمة. و`measurementRef` **غير فارغ بقيدٍ في قاعدة البيانات**: مبلغٌ بلا أساسٍ مكتوب تقديرٌ بلا مصدر. **ومستندٌ يُنشئه نداءٌ صريح لا مهمّة مجدولة**: لا مُشغّل دوري في هذه الوحدة، والنمط محجوزٌ للانتزاع ولا يُخترع مرّتين. / A request to create an end-of-service provision accrual document. `measurementRef` is **non-empty by a database constraint**: an amount with no written basis is an estimate with no source. **It is a document created by an explicit call, not a scheduled job**: there is no periodic runner in this module, and the pattern is reserved for extraction rather than invented twice. */
export interface HrProvisionRequest {
  /** تاريخ الاستحقاق ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The accrual date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  accruedOn: string;
  /** المعتمِد. / The approver. */
  approvedBy: string;
  /** مرجع أساس القياس المعتمد — نصٌّ يقرؤه مراجع، وغير فارغ. / The reference to the approved measurement basis — text a reviewer reads, and non-empty. */
  measurementRef: string;
  /** رقم المستند. / The document number. */
  number: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
  /** حصص علاقات العمل. / The per-employment shares. */
  shares: HrProvisionShareRequest[];
}

/** حصّة علاقة عمل من مخصص الفترة — **مبلغٌ يُدخله معتمِد المستند، والوحدة لا تقيسه**. طريقة قياس المخصص ومدخلاتها تحتاج اعتماد المحاسب القانوني، ونصّ المصفوفة صريح: «بطريقة القياس المعتمدة — لا تُخترع في هذا التسليم». / One employment's share of the period provision — **an amount the document's approver enters; the module does not measure it**. The provision's measurement method and inputs require a chartered accountant's approval, and the matrix text is explicit: 'by the approved measurement method — not invented in this deliverable'. */
export interface HrProvisionShareRequest {
  /** علاقة العمل — وهي حبيبيّة المخصص لا الموظف. / The employment — the provision's grain, not the employee. */
  employmentId: string;
  periodShare: Money;
}

/** تقرير مطابقة دفتر الموظف — **ولا رقم فيه اسمه «رصيد الموظف»، وهذا قرارٌ لا نقص**. قارئ نقطة الضبط يجمّع بلا تفصيل بالحساب ويعيد صافياً واحداً، ودفتر الموظف يمتدّ على أصلٍ واحد وثلاثة خصوم — فصافٍ واحد يقاصّ سلفةً بمخصص خدمة براتب مستحق ويعلن التطابق وهو أعمى. / The employee subledger reconciliation report — **and it publishes no single number called 'the employee's balance'; a decision, not a gap**. The control point reader aggregates without account detail and returns one net, while the employee subledger spans one asset and three liabilities — so one net offsets an advance against a service provision against a salary payable and declares agreement while blind. */
export interface HrReconciliation {
  /** تاريخ المطابقة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The reconciliation date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  asOf: string;
  /** المستندات المسؤولة عن الفارق وحدها، مرتَّبة ترتيباً ثابتاً. / Only the documents responsible for the difference, in a stable order. */
  divergences: HrReconciliationDivergence[];
  /** هل خلا التقرير من أي انحراف؟ **لا «قريب من الصفر»**. / Is the report free of any divergence? **Not 'close to zero'**. */
  isReconciled: boolean;
  /** عدد المستندات التي تطابق طرفاها بالضبط — وهو ما يمنع «صفر انحراف» من أن يعني «لم يُفحص شيء». / The number of documents whose two sides matched exactly — which is what stops 'zero divergences' from meaning 'nothing was checked'. */
  matchedDocuments: number;
}

/** سطر انحراف واحد — **بحبيبيّة المستند والطرف معاً**. و`documentId` على قيود الاستحقاق هو معرّف القسيمة، وهو ما يجعل الطرفين متساويي الحبيبيّة فتمكن المقارنة أصلاً. / A single divergence row — **at the grain of the document and the party together**. On accrual entries `documentId` is the payslip's identifier, which is what makes the two sides share one grain so the comparison is possible at all. */
export interface HrReconciliationDivergence {
  controlEffect: Money;
  divergence: Money;
  /** معرّف المستند كما أرسلته الوحدة إلى الدفتر. / The document identifier as the module sent it to the ledger. */
  documentId: string;
  /** نوع المستند كما أرسلته الوحدة. / The document type as the module sent it. */
  documentType: string;
  /** الرمز المعتم للموظف — وهو كل ما يعرفه الدفتر عنه. / The employee's opaque code — all the ledger knows of them. */
  partyId: string;
  /** سبب الانحراف. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The divergence reason. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  reasonCode: "amount_mismatch" | "missing_in_control" | "missing_in_subledger" | "posting_unresolved";
  subledgerEffect: Money;
}

/** المخالصة كما تخرج من السطح، **والسيناريو المنطبق مُسمّى** لا مستنتَجاً من فرق مبلغين. والمتطابقة المعلَنة في المصفوفة مفروضة في قاعدة البيانات: provisionUtilised = amountPaid − shortfall + excess. / The settlement as the surface returns it, **with the applicable scenario named** rather than inferred from the difference of two amounts. The identity declared in the matrix is enforced in the database: provisionUtilised = amountPaid − shortfall + excess. */
export interface HrSettlement {
  /** هل كانت مُرحَّلة قبل هذا النداء؟ / Was it already posted before this call? */
  alreadyPosted: boolean;
  amountPaid: Money;
  /** الرمز المعتم. / The opaque code. */
  employeeCode: string;
  /** علاقة العمل. / The employment. */
  employmentId: string;
  /** معرّف القيد إن رُحّلت، أو null. / The entry identifier if posted, or null. */
  entryId: string | null;
  excess: Money;
  /** المعرّف. / The identifier. */
  id: string;
  /** مرجع أساس الحساب المعتمد. / The approved calculation basis reference. */
  measurementRef: string;
  /** الرقم. / The number. */
  number: string;
  provisionBalance: Money;
  provisionUtilised: Money;
  /** السيناريو المنطبق بأسماء المصفوفة نفسها: مطابق، أو ناقص فيُحمَّل العجز على مصروف الفترة، أو زائد فتُردّ الزيادة إليه. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The applicable scenario under the matrix's own names: exact, short with the shortfall charged to the period expense, or excess with the surplus released back to it. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  scenarioCode: "exact" | "excess" | "short";
  /** تاريخ المخالصة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The settlement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  settledOn: string;
  settlementDue: Money;
  /** طريقة الصرف. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The disbursement method. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  shortfall: Money;
  /** الحالة. / The state. */
  state: string;
  /** طرف الخزينة. / The treasury party. */
  treasuryPartyId: string;
}

/** طلب إنشاء مخالصة نهاية خدمة على علاقة عمل منتهية. **والمستحقّ يصل من معتمِد المستند**: معادلة المكافأة وشرائحها غير متحقَّق منها ولا تُخترع هنا. وما تحسبه الوحدة هو رصيد المخصص وحده ثم العجز والزيادة، وكلاهما اشتقاقٌ حسابي من رقمين. / A final settlement draft request against a terminated employment. **The amount due arrives from the document's approver**: the benefit formula and its bands are unverified and are not invented here. What the module computes is the provision balance alone, and then the shortfall and the excess — both arithmetic derivations from two numbers. */
export interface HrSettlementRequest {
  /** علاقة العمل المنتهية. / The terminated employment. */
  employmentId: string;
  /** مرجع أساس الحساب المعتمد. / The approved calculation basis reference. */
  measurementRef: string;
  /** رقم المخالصة. / The settlement number. */
  number: string;
  /** تاريخ المخالصة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The settlement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  settledOn: string;
  settlementDue: Money;
  /** طريقة الصرف. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The disbursement method. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  /** طرف الخزينة — إلزامي. / The treasury party — mandatory. */
  treasuryPartyId: string;
}

/** سداد التأمينات كما يخرج من السطح، ومعه ما استُحقّ في فترته من مسيّرات مُرحَّلة **للمقارنة لا للإملاء**. وهو المستند الوحيد في هذه الوحدة الذي يُرحَّل قيداً واحداً للفترة، لأن سطره الأول على حساب الالتزام بلا دفتر مساعد. / The social insurance settlement as the surface returns it, together with what its period accrued from posted runs **for comparison, not for dictation**. It is the only document in this module posted as a single entry per period, because its first line's liability account has no subledger. */
export interface HrSocialInsurancePayment {
  accruedForPeriod: Money;
  /** هل كان مُرحَّلاً قبل هذا النداء؟ / Was it already posted before this call? */
  alreadyPosted: boolean;
  amount: Money;
  /** معرّف القيد إن رُحّل، أو null. / The entry identifier if posted, or null. */
  entryId: string | null;
  /** المعرّف. / The identifier. */
  id: string;
  /** الرقم. / The number. */
  number: string;
  /** تاريخ السداد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The settlement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  paidOn: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
  /** طريقة التسوية. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The settlement method. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  /** الحالة. / The state. */
  state: string;
  /** طرف الخزينة. / The treasury party. */
  treasuryPartyId: string;
}

/** طلب إنشاء سند سداد اشتراك التأمينات لفترة. **والمبلغ يصل من المستدعي ولا تُمليه الوحدة**: فاتورة الجهة قد تخالف ما استحقّته المسيّرات لأسباب مشروعة. / A social insurance settlement draft request for a period. **The amount comes from the caller; the module does not dictate it**: the authority's invoice may legitimately differ from what the runs accrued. */
export interface HrSocialInsurancePaymentRequest {
  amount: Money;
  /** رقم السند. / The document number. */
  number: string;
  /** تاريخ السداد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The settlement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  paidOn: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
  /** طريقة التسوية — مؤهّل دور. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The settlement method — a role qualifier. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  settlementMethod: "bank" | "cash";
  /** طرف الخزينة — إلزامي على كل مستند دفع. / The treasury party — mandatory on every payment document. */
  treasuryPartyId: string;
}

/** طلب إنهاء خدمة. والسبب **مفتاحٌ يقرؤه برنامج** من مجموعة يملكها المستدعي لا نصّاً يُعرض؛ ولا تصنيف هنا إلى «استقالة» و«إنهاء» لأن أثر التمييز على الاستحقاق بندٌ مفتوح على المالك. / A termination request. The reason is a **key a program reads**, from a set the caller owns, not displayed text; and there is no classification here into 'resignation' and 'dismissal', because the effect of that distinction on the entitlement is an open owner question. */
export interface HrTerminationRequest {
  /** تاريخ انتهاء الخدمة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The service end date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  endedOn: string;
  /** مفتاح سبب الإنهاء — رمزٌ لا نصّ. / The termination reason key — a code, not text. */
  reasonKey: string;
}

/** تأسيس المنشأة كما يصل من العميل. **يُقبل مرّة واحدة**، والثانية 409. / The company setup as the client sends it. **Accepted once**; a second attempt is 409. */
export interface InitialiseCompanySetupRequest {
  /** اسم المنشأة بالعربية. **إلزامي وهو السجلّ** لا ترجمةً أولى (ADR-0021) — ومع الجواب One يصير هو اسم مركز التكلفة الافتراضي بعينه. / The company's Arabic name. **Mandatory, and it is the record** rather than a first translation (ADR-0021) — with the One answer it becomes the default cost centre's own name. */
  companyNameAr: string;
  /** ترجمات اسم المنشأة، مفاتيحها أوسمة BCP-47. صفوف لا أعمدة: إضافة لغة إدخالُ مدخل لا هجرةُ مخطّط. / The company name's translations, keyed by BCP-47 tags. Rows, not columns: adding a language is an entry, not a schema migration. */
  companyNameTranslations?: NameValue[];
  /** الجواب عن سؤال مراكز التكلفة: One = مركز واحد يحمل اسم المنشأة · Multiple = عدّة، واسم الأول إلزامي. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The answer to the cost-centre question: One = a single centre carrying the company name; Multiple = several, and the first one's name is mandatory. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  costCenters: "One" | "Multiple";
  /** عدد الخانات العشرية المعروضة. يُسنَد هنا ولا يُعدَّل بعدها. ويحكم العرض والإدخال البشري وحدهما — لا التخزين ولا الحساب. / The number of displayed decimal places. Assigned here and never editable afterwards. It governs display and human input only — never storage and never arithmetic. */
  decimalPlaces: number;
  /** اسم أول مركز تكلفة بالعربية. إلزامي مع Multiple، ومرفوض مع One لأن اسمه هناك اسم المنشأة بعينه. / The first cost centre's Arabic name. Required with Multiple, refused with One because its name there is the company's own. */
  firstCostCenterNameAr?: string | null;
  /** ترجمات اسم أول مركز. / The first centre name's translations. */
  firstCostCenterTranslations?: NameValue[];
}

/** قسطٌ مُصرَّح به. **وحقلا الفترة قبل تاريخ الاستحقاق**: أساس الاعتراف مدى الفترة لا يوم السداد، وقسطٌ بلا فترته لا يُنسب إلى شهرٍ في قائمة دخل. / A declared instalment. **The period fields come before the due date**: recognition rests on the period range, not the payment day, and an instalment without its period belongs to no month in an income statement. */
export interface Instalment {
  amount: Money;
  /** تاريخ استحقاق القسط. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The instalment's due date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  dueOn: string;
  /** بداية الفترة المستحقّة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The start of the period covered. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodFrom: string;
  /** نهاية الفترة المستحقّة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The end of the period covered. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodTo: string;
}

/** عدد صحيح 64 بت نصّاً: Number في JavaScript يفقد الدقّة فوق 2^53، ورقم القيد معرّف لا كمّية. / A 64-bit integer as a string: JavaScript Number loses precision above 2^53, and an entry number is an identifier, not a quantity. */
/* Int64String مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** مستندٌ منحرف بين دفتر المخزون المساعد وحسابه الضابط — **يُسمّى بنوعه ومعرّفه وصنفه وسببه**، فلا يُقال «هناك مشكلة» بلا «أين». / A document diverging between the inventory subledger and its control account — **named by its type, its identifier, its item, and the reason**, so the report never says 'there is a problem' without saying where. */
export interface InventoryDivergence {
  controlEffect: Money;
  divergence: Money;
  /** معرّف المستند. / The document identifier. */
  documentId: string;
  /** نوع المستند. / The document type. */
  documentType: string;
  /** الصنف. / The item. */
  itemId: string;
  /** سبب الانحراف: حركةٌ بلا نظير في نقطة الضبط، أو نظيرٌ بلا حركة، أو مبلغان مختلفان. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The reason: a movement with no counterpart at the control point, a counterpart with no movement, or two different amounts. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  reasonCode: "amount_mismatch" | "missing_in_control" | "missing_in_subledger";
  subledgerEffect: Money;
}

/** تقييم المخزون ومطابقته — **ثلاثة طرق مستقلّة إلى الرقم نفسه**: مجموع الحركات، ومجموع أرصدة الأصناف، ونقطة الضبط في الدفتر. واثنان يكفيان لكشف انحراف بين الوحدة والدفتر؛ والثالث يكشف انحراف الوحدة عن نفسها. وisReconciled يعني الفارق **صفر بالضبط** لا «قريب من الصفر». / The inventory valuation and its reconciliation — **three independent routes to the same number**: the sum of movements, the sum of item balances, and the ledger's control point. Two are enough to reveal a divergence between the module and the ledger; the third reveals the module diverging from itself. isReconciled means the difference is **exactly zero**, not 'close to zero'. */
export interface InventoryValuation {
  /** تاريخ التقييم. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The valuation date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  asOf: string;
  balanceTotal: Money;
  controlTotal: Money;
  divergence: Money;
  /** المستندات المسؤولة عن الفارق. / The documents responsible for the difference. */
  divergences: InventoryDivergence[];
  /** هل الفارق صفر بالضبط؟ / Is the difference exactly zero? */
  isReconciled: boolean;
  subledgerTotal: Money;
}

/** طلب سكّ تذكرة تنزيل. والعمر بالثواني عدداً صحيحاً لا كسراً عشرياً: مدّةٌ تعبر السلك بفاصلة عائمة تُقارَن يوماً بمدّة أخرى فتختلفان في الخانة السابعة عشرة. وما تجاوز السقف يُرفض ولا يُقصّ. / A request to mint a download ticket. The lifetime is whole seconds, not a decimal: a duration crossing the wire as a float is one day compared with another and they differ in the seventeenth digit. Beyond the cap it is refused, never truncated. */
export interface IssueAttachmentTicketRequest {
  /** عمر التذكرة بالثواني. السقف الافتراضي خمس دقائق — نافذةُ ضررٍ تُقاس بالدقائق لا بالساعات. / The ticket lifetime in seconds. The default cap is five minutes — a damage window measured in minutes, not hours. */
  lifetimeSeconds: number;
}

/** صنف كما يخرج على السلك. / An item as it leaves on the wire. */
export interface Item {
  /** وحدة الأساس. / The base unit. */
  baseUnit: string;
  /** الرمز. / The code. */
  code: string;
  /** المعرّف الذي تُبنى عليه القراءة. / The identifier reads are built on. */
  id: string;
  /** مجموعة الصنف. / The item group. */
  itemGroup: string;
  name: LocalizedText;
  /** الوحدات الأكبر ومعاملاتها. / The larger units and their factors. */
  units: UnitFactor[];
}

/** حالة صنفٍ في دورة حياته ورصيده المتبقّي. **ونوعٌ مستقلّ لا حقلٌ على Item**: إضافةُ isActive إلى شكل الصنف كانت ستُغيّر استجابة ثلاث عمليات منشورة يستهلكها عملاء اليوم. / An item's lifecycle state and remaining stock. **A separate type, not a field on Item**: adding isActive to the item shape would change the response of three published operations that today's clients consume. */
export interface ItemLifecycle {
  /** رمز الصنف. / The item code. */
  code: string;
  /** هل بقي للصنف رصيد **غير صفري** في أي موضع؟ و«غير صفري» لا «موجب»: رصيدٌ سالب واقعةٌ تقع، وإخفاؤها يجعل الجواب يقول «لا رصيد» على صنفٍ عليه عجزٌ مفتوح. / Does the item still hold a **non-zero** balance anywhere? Non-zero, not positive: a negative balance is a real occurrence, and hiding it would make the answer say 'no stock' about an item carrying an open shortage. */
  holdsStock: boolean;
  /** معرّف الصنف. / The item identifier. */
  id: string;
  /** هل الصنف متداوَل؟ **والتعطيل حالةٌ لا حذف**: الرمز محمولٌ على قيود سنةٍ مضت. والمُعطَّل يُرفض عليه الوارد الجديد ويبقى الصادر حتى ينفد. / Is the item in circulation? **Deactivation is a state, not a deletion**: the code is carried by last year's entries. A deactivated item refuses new inbound stock and keeps issuing until it runs out. */
  isActive: boolean;
  /** عدد المواضع التي بقي فيها رصيد غير صفري — فلا يُقال «بقي رصيد» بلا «أين». / The number of places still holding a non-zero balance — so the answer never says 'stock remains' without saying where. */
  placementsWithStock: number;
}

/** أصناف المنشأة، مرتَّبة بالرمز ترتيباً حرفياً ثابتاً. **وغلافٌ لا مصفوفة عارية**: مصفوفةٌ في جذر الاستجابة لا موضع فيها لعدّاد ولا لصفحة، فأول حاجة إليهما تكسر العقد. / The company's items, ordered by code in a stable ordinal order. **An envelope, not a bare array**: an array at the response root has no place for a count or a page, so the first need for either breaks the contract. */
export interface ItemList {
  /** عدد الأصناف. / The number of items. */
  itemCount: number;
  /** الأصناف. / The items. */
  items: Item[];
}

/** طلب تسجيل صنف. **ولا رمز حساب فيه**: الصنف يحمل itemGroup — مؤهّل دور — ومصفوفة الترحيل وحدها تُحوّله إلى حساب (القاعدة 2). / An item registration request. **No account code appears in it**: an item carries an itemGroup — a role qualifier — and the posting matrix alone turns it into an account (Rule 2). */
export interface ItemRequest {
  /** وحدة الأساس: أصغر وحدة يُمسَك بها الصنف، وإليها تُحوَّل البقية. ولا تتغيّر بعد أن تُكتب على الصنف حركات. / The base unit: the smallest unit the item is held in, into which everything else converts. It does not change once movements have been written against the item. */
  baseUnit: string;
  /** رمز الصنف داخل المنشأة — هوية تحملها حركاته وقيوده، لا نصّاً معروضاً. / The item code within the company — an identity carried by its movements and entries, not displayed text. */
  code: string;
  /** مجموعة الصنف — مؤهّل الدور عند المصفوفة. / The item group — a role qualifier for the posting matrix. */
  itemGroup: string;
  name: LocalizedText;
  /** الوحدات الأكبر ومعاملاتها — قائمة فارغة إن كان الصنف يُمسَك بوحدة أساسه وحدها. / The larger units and their factors — an empty list if the item is held in its base unit alone. */
  units: UnitFactor[];
}

/** طلب تعديل صنف. **ولا رمز فيه**: الرمز هوية تحملها قيود سنةٍ مضت، ويُقرأ من المسار ولا يُقبل في الجسم. / An item update request. **It carries no code**: the code is an identity carried by last year's entries; it is read from the path and never accepted in the body. */
export interface ItemRevisionRequest {
  /** وحدة الأساس. **ولا تتغيّر بعد أن تُكتب على الصنف حركة أو يُمسَك له رصيد** — وإلا رُفضت بـ inventory.base_unit_locked_by_history. / The base unit. **It does not change once a movement has been written against the item or a balance is held for it** — otherwise it is refused with inventory.base_unit_locked_by_history. */
  baseUnit: string;
  /** مجموعة الصنف — مؤهّل الدور. **وتغييرها لا يمسّ ما مضى**: كل حركة تحمل مجموعتها على صفّها هي. / The item group — a role qualifier. **Changing it touches nothing past**: every movement carries its own group on its own row. */
  itemGroup: string;
  name: LocalizedText;
  /** الوحدات الأكبر ومعاملاتها — **تحلّ محلّ القائمة السابقة كلّها**، ولا تمسّ حركةً مضت. / The larger units and their factors — **they replace the previous list entirely**, and touch no past movement. */
  units: UnitFactor[];
}

export interface JournalEntry {
  /** الدفتر. / The book. */
  book: string;
  chainSequence: Int64String;
  /** رمز عملة ISO 4217 بثلاثة محارف لاتينية كبيرة. واللاتينية هنا شرط سلامة سلسلة التجزئة لا تفضيل عرض. / An ISO 4217 currency code, three upper-case ASCII letters. ASCII here is a hash-chain safety requirement, not a display preference. */
  currency: string;
  /** تاريخ القيد الميلادي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The Gregorian entry date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  entryDate: string;
  /** بصمة القيد. / The entry hash. */
  entryHash: string;
  /** معرّف القيد. / The entry identifier. */
  entryId: string;
  entryNumber: Int64String;
  /** سطور القيد. / The entry's lines. */
  lines: JournalLine[];
  /** البيان بالعربية. / The memo in Arabic. */
  memoAr: string;
  /** البيان بالإنجليزية. / The memo in English. */
  memoEn: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
  /** القيد الذي يعكسه هذا القيد، إن كان قيد عكس. / The entry this one reverses, when it is a reversal. */
  reversesEntryId: string | null;
  /** حالة القيد. / The entry status. */
  status: string;
}

export interface JournalLine {
  credit: Money;
  /** رمز عملة ISO 4217 بثلاثة محارف لاتينية كبيرة. واللاتينية هنا شرط سلامة سلسلة التجزئة لا تفضيل عرض. / An ISO 4217 currency code, three upper-case ASCII letters. ASCII here is a hash-chain safety requirement, not a display preference. */
  currency: string;
  debit: Money;
  /** بيان السطر بالعربية. / The line narration in Arabic. */
  descriptionAr: string;
  /** بيان السطر بالإنجليزية. / The line narration in English. */
  descriptionEn: string;
  /** رقم السطر. / The line number. */
  lineNo: number;
  /** مؤهّل الدور. / The role qualifier. */
  qualifier: string;
  /** رمز الدور كما خُزِّن. / The role code as stored. */
  role: string;
}

/** **قيدُ تسجيلِ** عقد إيجار مُحرَّر في منصّة إيجار، بحالة القيد. **والحالة حالةُ القيد لا حالةُ العقد**: نفاذ العقد يُقرَّر في المنصّة لا هنا. **ولا معرّف قيد محاسبي فيه**: توقيع العقد لا يُنشئ قيداً، ولا مورد ترحيل على هذا المستند. / The **registration record** of a lease contract issued on the Ejar platform, with the record's state. **The state is the record's, not the contract's**: whether the contract is in force is settled on the platform, not here. **It carries no accounting entry identifier**: signing a lease creates no entry, and the resource has no posting sub-resource. */
export interface LeaseRegistration {
  /** رقم عقد إيجار — مرجع العقد المُحرَّر في المنصّة. / The Ejar contract number — the reference to the contract issued on the platform. */
  ejarContractNumber: string;
  /** نهاية المدّة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The end of the term. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  endsOn: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** المستأجر. / The lessee. */
  lesseeId: string;
  /** العقار المشتقّ من الوحدة. / The property derived from the unit. */
  propertyId: string;
  /** بداية المدّة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The start of the term. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  startsOn: string;
  /** حالة **القيد** لا حالة العقد. وBILLABLE وحدها تدخل قيد الاستبعاد الزمني وتُتيح الفوترة، ومعناها «معتمَدٌ للفوترة» لا «سارٍ»: النفاذ من المنصّة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The state of the **record**, not of the contract. Only BILLABLE enters the temporal exclusion constraint and permits invoicing; it means 'approved for billing', never 'in force' — force comes from the platform. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "BILLABLE" | "DRAFT";
  totalRent: Money;
  /** الوحدة. / The unit. */
  unitId: string;
}

/**
 * طلب **تسجيل** عقد إيجار مُحرَّر في منصّة إيجار — مسوّدة قيد أرشيفي لا عقد. **والنظام لا يُحرّر عقداً ولا يُعدّله ولا يُنهيه**: منصّة إيجار الحكومية هي الطرف المخوَّل بذلك، وما يُنشَأ هنا قيدٌ مرجعُه رقم عقد إيجار. **ولا تكامل مع المنصّة**: الرقم يُقيَّد كما يصل ولا يُتحقَّق منه.
 * 
 * **وقيمة العقد والأقساط تُصرَّحان معاً**: النظام لا يوزّع القيمة على الأقساط — التوزيع يستلزم سياسة تقريب هي قرار مالك مفتوح — بل **يفحص** أن مجموع الأقساط يساوي قيمة العقد بالضبط ويرفض بخلافه. ولو اشتُقّت القيمة من الأقساط لصارت الثابتة صحيحةً بحكم البناء ولم تمسك توزيعاً خاطئاً. / A request to **register** a lease contract issued on the Ejar platform — a draft archival record, not a contract. **The system issues no contract, amends none, and terminates none**: the government Ejar platform is the party authorised to do that, and what is created here is a record whose reference is the Ejar contract number. **There is no integration with the platform**: the number is recorded as received and never verified.
 * 
 * **The contract value and the instalments are both declared**: the system does not spread the value across the instalments — spreading requires a rounding policy that is an open owner decision — it **checks** that the instalments sum exactly to the contract value and refuses otherwise. Had the value been derived from the instalments the invariant would hold by construction and would catch no wrong split.
 */
export interface LeaseRegistrationRequest {
  /** رقم عقد إيجار — مرجع العقد المُحرَّر في المنصّة، ولا يولّده هذا النظام. وتفرّده مفروضٌ داخل المنشأة وحدها. / The Ejar contract number — the reference to the contract issued on the platform; this system does not mint it. Its uniqueness is enforced within the company alone. */
  ejarContractNumber: string;
  /** نهاية المدّة — داخلة في المدى. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The end of the term — inclusive. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  endsOn: string;
  /** الأقساط بفتراتها ومبالغها. ومجموعها يُفحص عند الاعتماد للفوترة ولا يُصلَح. / The instalments with their periods and amounts. Their sum is checked at billing approval and never corrected. */
  instalments: Instalment[];
  /** المستأجر. / The lessee. */
  lesseeId: string;
  /** بداية المدّة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The start of the term. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  startsOn: string;
  totalRent: Money;
  /** الوحدة المؤجَّرة — ومنها يُشتقّ العقار، فلا يُذكر العقار مرّتين فينحرف. / The unit being let — the property is derived from it, so the property is never stated twice and cannot drift. */
  unitId: string;
}

/** جدول دفعات قيدٍ بمعرّفات سطوره. / A lease registration's payment schedule with its line identifiers. */
export interface LeaseSchedule {
  /** قيد التسجيل. / The lease registration. */
  leaseId: string;
  /** السطور بترتيب تسلسلها. / The lines in sequence order. */
  lines: LeaseScheduleLine[];
}

/** سطر جدول الدفعات **بمعرّفه** — وهو ما يُرسَل في طلب الفاتورة. / A payment schedule line **with its identifier** — what is sent in the invoice request. */
export interface LeaseScheduleLine {
  amount: Money;
  /** تاريخ الاستحقاق — وهو ما تُقاس عليه أعمار المتأخرات لا تاريخ الإصدار. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The due date — what arrears ageing is measured against, not the issue date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  dueOn: string;
  /** معرّف السطر — مدخل الفوترة. / The line identifier — the entry point to invoicing. */
  id: string;
  /** هل فُوتر هذا القسط؟ والقسط لا يُفوتَر مرّتين. / Has this instalment been invoiced? An instalment is never invoiced twice. */
  isInvoiced: boolean;
  /** بداية الفترة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The start of the period. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodFrom: string;
  /** نهاية الفترة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The end of the period. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  periodTo: string;
  /** تسلسل القسط في القيد. / The instalment's sequence within the registration. */
  seq: number;
}

/** نصّ ثنائي اللغة. الطرفان إلزاميان — العربية أساسية لا ترجمة ثانية. / Bilingual text; both sides are mandatory. */
export interface LocalizedText {
  /** النصّ العربي. / The Arabic text. */
  ar: string;
  /** النصّ الإنجليزي. / The English text. */
  en: string;
}

/** مقدار كمّية نصّاً بمقياس لا يتجاوز **ستّاً**. والكمّية ليست مبلغاً — ولذلك لها مقياسها — لكنها تُضرب في تكلفة الوحدة، فأي دقّة تُفقد فيها تصل إلى المال. والكيلوغرامات واللترات والأمتار تُكسَر إلى ما دون الهللة، ومقياسٌ مالي عليها يُنتج تقريباً صامتاً يتراكم على كل حركة. / A quantity magnitude as a string with at most **six** decimal places. A quantity is not an amount — hence its own scale — but it is multiplied by a unit cost, so any precision lost in it reaches the money. Kilograms, litres, and metres divide below the halala, and a money scale over them produces a silent rounding that accumulates on every movement. */
/* Magnitude مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** كمّية **بوحدتها** — ولا كمّية مجرّدة تعبر هذا السطح. و«عشرة» ليست معلومة: عشر حبّات أم عشر كراتين؟ والفرق بينهما في دفترٍ يمسك قيمةً هو الفرق بين رقمٍ صحيح ورقمٍ أكبر منه اثني عشر ضعفاً، **ولا يُظهره توازنٌ ولا سلسلة** لأن القيد المبنيّ عليه متوازن تماماً. / A quantity **with its unit** — no bare quantity crosses this surface. 'Ten' is not information: ten pieces or ten cartons? In a ledger that holds value the difference between them is the difference between a correct number and one twelve times larger, and **no balance check and no hash chain reveals it**, because the entry built on it balances perfectly. */
export interface Measure {
  magnitude: Magnitude;
  /** رمز وحدة القياس كما سجّله المستأجر. معرّف لا نصّ معروض: لا يُترجَم ولا يُطابَق بلا حساسية حالة. / The unit-of-measure code as the tenant registered it. An identifier, not displayed text: never translated and never matched case-insensitively. */
  unit: string;
}

/** عضو في منشأة كما يُعرض في قائمة الأعضاء. **ولا اعتماد فيه**: اعتماد الانتساب يخرج مرّة واحدة في استجابة الدعوة ولا يُعاد أبداً. / A member of a company as shown in the member list. **It carries no credential**: an enrolment credential leaves once, in the invitation response, and is never re-issued. */
export interface Membership {
  /** الاسم العربي. / The Arabic name. */
  displayNameAr: string;
  /** لحظة منح العضوية. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / When the membership was granted. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  grantedAt: string;
  /** الدور في هذه المنشأة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The role in this company. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  role: "Reader" | "Contributor" | "Owner";
  /** معرّف المستخدم كما سكّه الخادم. / The user identifier as the server minted it. */
  userId: string;
}

/** أعضاء منشأة واحدة، مرتَّبين بمعرّف المستخدم ترتيباً حرفياً ثابتاً — قائمةٌ يتغيّر ترتيبها بين نداءين تجعل «العضو الثاني» يعني شخصين في دقيقتين. / The members of one company, ordered by user identifier in a stable ordinal order — a list whose order changes between calls makes 'the second member' mean two different people two minutes apart. */
export interface MembershipList {
  /** المنشأة. / The company. */
  companyId: string;
  /** عدد الأعضاء. / The number of members. */
  memberCount: number;
  /** الأعضاء. / The members. */
  members: Membership[];
}

/** عضويةٌ سُحبت: من كان، وبأي دور، ومتى. والصفّ لا يبقى «موقوفاً»: العضوية صلاحيةُ وصولٍ جارية لا سجلّ محاسبي، وأثرُها التاريخي في سجلّ التدقيق — وصلاحيةٌ موقوفة تبقى في جدول وصول هي الشكل الذي يُنسى فيه أحدهم مُفعَّلاً. / A revoked membership: who it was, in which role, and when. The row is not left 'suspended': a membership is a live access grant rather than an accounting record, and its history lives in the audit log — a suspended grant left in an access table is exactly how somebody stays enabled by being forgotten. */
export interface MembershipRevocation {
  /** المنشأة. / The company. */
  companyId: string;
  member: Membership;
  /** لحظة السحب. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / The instant of revocation. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  revokedAt: string;
}

/** دورٌ تغيّر: العضوية بدورها الجديد، والدور السابق، ولحظة التغيير. وpreviousRole يجعل العميل يعرف اتجاه التغيير بلا طلبٍ ثانٍ. / A changed role: the membership in its new role, the previous role, and the instant of the change. previousRole lets a client see the direction of the change without a second request. */
export interface MembershipRoleChange {
  /** لحظة التغيير. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / The instant of the change. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  changedAt: string;
  /** المنشأة. / The company. */
  companyId: string;
  member: Membership;
  /** الدور السابق. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The previous role. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  previousRole: "Reader" | "Contributor" | "Owner";
}

/** مبلغ نصّاً، بمقياس لا يتجاوز أربع خانات عشرية. النحو المقبول كاملاً: -?(0|[1-9][0-9]*)(\.[0-9]{1,4})? — فتُرفض الصيغة الأسّية، والصفر البادئ، والإشارة الموجبة الصريحة، والفراغ، والأرقام العربية-الهندية والديفاناغارية، وكل ما زاد على أربع خانات. ورمزٌ رقمي في هذا الحقل يُرفض الطلب بسببه: JSON لا يملك نوعاً عشرياً، وأغلب العملاء يمرّرون الرمز الرقمي على فاصلة عائمة ثنائية فيقع فقدان الدقّة قبل أن يصل الطلب. / An amount as a string with at most four decimal places. The full accepted grammar is -?(0|[1-9][0-9]*)(\.[0-9]{1,4})? — exponent notation, leading zeros, an explicit plus sign, whitespace, Arabic-Indic and Devanagari digits, and any fifth decimal are all refused. A JSON number token in this field fails the request: JSON has no decimal type, and most clients route a number token through a binary double, so precision is lost before the request arrives. */
/* Money مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

export interface NameValue {
  /** الاسم. / The name. */
  name: string;
  /** القيمة. / The value. */
  value: string;
}

export interface NamedAmount {
  /** اسم المبلغ كما تعرّفه مصفوفة الترحيل. / The amount name as the posting matrix defines it. */
  name: string;
  value: Money;
}

/** طلب فتح جلسة. ولا حقل مستأجر فيه ولا حقل مستخدم: الهوية تُشتقّ من الاعتماد كما في كل مسار آخر، وحقلٌ يقول «أنا فلان» في جسم طلبِ دخول ادّعاءٌ لا مصادقة. / A request to open a session. It carries no tenant field and no user field: identity is derived from the credential as on every other path, and a field saying 'I am so-and-so' in a sign-in body is a claim, not authentication. */
export interface OpenSessionRequest {
  /** اعتماد الانتساب كما سُلِّم مرّة واحدة عند الدعوة. نصٌّ مبهم لا بنية فيه: لا يُحلَّل ولا يُشتقّ منه شيء، ويُقدَّم كما وصل. / The enrolment credential exactly as handed over once at invitation. An opaque string with no structure: never parsed, nothing derived from it, presented exactly as received. */
  enrolmentCredential: string;
}

/** طرف كما يخرج على السلك — عميل أو مورد. / A party as it leaves on the wire — a customer or a supplier. */
export interface Party {
  /** الرمز. / The code. */
  code: string;
  creditLimit: Money;
  /** المعرّف الذي تُبنى عليه المستندات. / The identifier documents are built on. */
  id: string;
  name: LocalizedText;
  /** مهلة السداد بالأيام. / The payment terms in days. */
  paymentTermsDays: number;
  /** رقم التسجيل الضريبي على المورد؛ وفراغٌ على مورد بلا رقم؛ وnull على العميل — فالحقل لا يوجد عليه أصلاً. والحالات الثلاث مختلفة ولا تُجمع في تمثيل واحد. / The VAT number on a supplier; empty on a supplier without one; and null on a customer, where the field does not exist at all. The three states differ and are not collapsed into one spelling. */
  vatNumber: string | null;
}

/** تخصيص مبلغ من سند صرف على فاتورة مورد **مُرحَّلة**. / An allocation of part of a payment against a **posted** supplier bill. */
export interface PaymentAllocation {
  amount: Money;
  /** فاتورة المورد المُرحَّلة. / The posted supplier bill. */
  billId: string;
}

/** بندٌ معلَّق على قرار محاسب — **وهو ما يمنع الترحيل اليوم**. ووجوده في الجواب مقصود: من يقرأ العقد أو المستخلص يعرف سلفاً ما الذي سيرفضه الترحيل ولماذا، بدل أن يكتشفه عند أول محاولة مالية. و**الرمز هو نقطة الاعتماد البرمجية**، والعنوانان للعرض. / An item pending an accountant's decision — **what blocks posting today**. Its presence in the response is deliberate: whoever reads a contract or a certificate learns in advance what posting will refuse and why, instead of discovering it at the first financial attempt. **The code is the programmatic anchor**; the two titles are for display. */
export interface PendingPolicyItem {
  /** رمز البند الثابت. / The item's stable code. */
  code: string;
  /** الموضع الذي يحمل السؤال كاملاً بخياراته. / Where the full question and its options live. */
  sourceRef: string;
  /** عنوان البند بالعربية. / The item's Arabic title. */
  titleAr: string;
  /** عنوانه بالإنجليزية — تشخيصيٌّ يصحبه رمز ثابت، لا نصّ عرض. / Its English title — diagnostic text accompanied by a stable code, not display text. */
  titleEn: string;
}

/** طلب إعادة تسمية موضع — **الاسم وحده، ولا رمز فيه**. والرمز محمولٌ على كل حركة ورصيد، وتغييرُه يقطع كل حركة مضت عن موضعها. / A request to rename a place — **the name only, with no code in it**. The code is carried on every movement and balance, and changing it severs every past movement from its place. */
export interface PlaceNameRequest {
  name: LocalizedText;
}

/** رصيدٌ بتسكينه: الرصيد نفسه ومعه اسما مستودعه وموقعه من سجلّ التسكين. **ورمزٌ غير مسجَّل يخرج ويُوسَم** بـ warehouseRegistered أو locationRegistered كاذبة، ويخرج اسمه مساوياً لرمزه — لا يُحذف من القائمة ولا يُخترَع له اسم. / A balance with its placement: the balance itself plus its warehouse and location names from the placement register. **An unregistered code is returned and flagged** with warehouseRegistered or locationRegistered false, and its name comes back equal to its code — never dropped from the list and never given an invented name. */
export interface PlacementBalance {
  /** هل ورد هذا الصنف إلى هذا الموضع مرّةً بتكلفة؟ / Has this item ever been received into this place with a cost? */
  hasCostBasis: boolean;
  /** الصنف. / The item. */
  itemId: string;
  /** رمز الموقع. / The location code. */
  locationId: string;
  locationName: LocalizedText;
  /** هل رمز الموقع مسجَّل في سجلّ التسكين؟ فإن كان false فاسمه مساوٍ لرمزه، وهو رمزٌ كُتب على حركة قبل أن يوجد السجلّ. / Is the location code registered in the placement register? When false its name equals its code — a code written onto a movement before the register existed. */
  locationRegistered: boolean;
  quantity: Measure;
  unitCost: UnitCost;
  value: Money;
  /** رمز المستودع. / The warehouse code. */
  warehouseId: string;
  warehouseName: LocalizedText;
  /** هل رمز المستودع مسجَّل في سجلّ التسكين؟ / Is the warehouse code registered in the placement register? */
  warehouseRegistered: boolean;
}

/** الأرصدة بتسكينها، مرتَّبة بالصنف ثم المستودع ثم الموقع. / The balances with their placement, ordered by item then warehouse then location. */
export interface PlacementBalanceList {
  /** عدد الأرصدة. / The number of balances. */
  balanceCount: number;
  /** الأرصدة. / The balances. */
  balances: PlacementBalance[];
}

/** طلب ترحيل. ولاحظ ما ليس فيه: لا حقل مستأجر ولا حقل شركة — النطاق من الاعتماد ومن المسار. وأي حقل غير معروف يُرفض الطلب كلّه بسببه. / A posting request. Note what is absent: no tenant field and no company field — scope comes from the credential and the path. Any unknown field fails the whole request. */
export interface PostJournalEntryRequest {
  /** مفردات المبالغ التي يقرؤها قالب الحدث. / The amount vocabulary the event template reads. */
  amounts?: NamedAmount[];
  /** الدفتر داخل الشركة. الافتراضي MAIN. / The book within the company. Default MAIN. */
  book?: string;
  closedPeriodAuthorisation?: ClosedPeriodAuthorisation;
  /** رمز عملة ISO 4217 بثلاثة محارف لاتينية كبيرة. واللاتينية هنا شرط سلامة سلسلة التجزئة لا تفضيل عرض. / An ISO 4217 currency code, three upper-case ASCII letters. ASCII here is a hash-chain safety requirement, not a display preference. */
  currency?: string;
  /** الأبعاد التحليلية على مستوى الطلب. / Analytical dimensions at request level. */
  dimensions?: NameValue[];
  /** تاريخ المستند الميلادي. الفترة المالية تُشتق منه داخل الدفتر. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The Gregorian document date; the ledger derives the fiscal period from it. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  documentDate: string;
  /** رمز الحدث في مصفوفة الترحيل بصيغة <وحدة>.<كيان>.<فعل>. **إلزامي على المسارين معاً**: الرمز يعطي القيد هويّته، والسطور — إن وُجدت — تعطيه محتواه. ورمزٌ غائب أو فارغ يجعل حدثين مختلفين من المستند نفسه عند الإطلاق نفسه هويةً واحدة، فيُبتلع الثاني بصمت بلا خطأ ولا اختلال توازن. والقيد اليدوي ليس استثناءً: له حدثه المعرَّف في المصفوفة. / The posting-matrix event code, shaped <module>.<entity>.<action>. **Mandatory on both paths**: the code gives the entry its identity, and the lines — where present — give it its content. A missing or blank code collapses two different events of the same document at the same trigger into one identity, and the second is swallowed silently, with no error and no imbalance. A manual voucher is no exception: it has its own defined event in the matrix. */
  event: string;
  exchangeRate?: ExchangeRate;
  /** وقائع السياق التي تُقيَّم عليها الشروط وقواعد الحجب. / Context facts against which conditions and guard rules are evaluated. */
  facts?: NameValue[];
  /** جيل الترحيل. يبدأ من 1 ولا يزيد إلا بعد عكس مشروع. / The posting generation. Starts at 1 and increases only after a legitimate reversal. */
  generation?: number;
  /** مفتاح الحصانة ضد التكرار، محارف [0-9A-Za-z-_:.] فقط. مستقلّ عن الترتيب. / The idempotency key, characters [0-9A-Za-z-_:.] only. Order-independent. */
  idempotencyKey: string;
  /** سطور الطلب — تُرسَل في المسار الصريح (قيد يدوي) وتُترك فارغة في مسار القالب. وهي وحدها ما يختار المسار؛ وevent إلزامي في الحالتين. / The request lines: sent on the explicit path (a manual voucher) and left empty on the template path. They alone select the path; event is mandatory either way. */
  lines?: PostingLine[];
  narration: LocalizedText;
  source: SourceDocument;
  /** الحدث الذي أطلق الترحيل. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / What triggered the posting. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  trigger: "OnApproval" | "OnReceipt" | "OnSettlement" | "Periodic" | "Reversal";
}

/** دليل الحسابات كاملاً بشروط الترحيل على كل حساب. ويُرجَع كاملاً لا مقتصراً على ما يقبل الترحيل: الشجرة تُعرَض بآبائها، وقائمةُ الأوراق وحدها تدفع العميل إلى اختراع تجميعٍ من بادئات الرموز. والعدّادان يصلان محسوبَين كي يُرى النقص: عميلٌ يعدّ بنفسه لا يملك ما يقارن به حين تصل الاستجابة ناقصة. / The whole chart of accounts with each account's posting requirements. It is returned in full rather than restricted to postable accounts: the tree is displayed with its parents, and a list of leaves alone pushes the client into inventing a grouping from code prefixes. The two counts arrive computed so that a shortfall is visible: a client that counts for itself has nothing to compare against when a response arrives incomplete. */
export interface PostingChart {
  /** عدد الحسابات كلّها — يُقارَن بطول accounts فيُرى النقص. / The total number of accounts; compare it with the length of accounts to see a shortfall. */
  accountCount: number;
  /** الحسابات مرتّبة برمزها ترتيباً حرفياً ثابتاً. / The accounts ordered by code with a stable ordinal sort. */
  accounts: PostingChartEntry[];
  /** عدد ما يقبل الترحيل منها — وهو ما تعرضه شاشة القيد اليدوي. / How many of them are postable — which is what a manual voucher screen offers. */
  postableCount: number;
}

/** مدخل واحد في دليل الحسابات، ومعه شروط الترحيل عليه. ولا حقل مالي فيه إطلاقاً، فلا يُطرح سؤال شكل المال على السلك أصلاً؛ والعدد الوحيد level صحيحٌ محدود بين 1 و4 يفرضه قيد تحقّق في المخطّط، لا مبلغاً ولا صحيحاً 64 بت، فيعبر رمزاً رقمياً كما يعبر rowCount. / One entry in the chart of accounts together with what posting to it requires. It carries no monetary field at all, so the money-on-the-wire question does not arise; its only number, level, is a bounded integer between 1 and 4 enforced by a schema check constraint — not an amount and not a 64-bit integer — so it crosses as a JSON number, as rowCount does. */
export interface PostingChartEntry {
  /** رمز الحساب كما هو في دليل حسابات هذه الشركة — معرّف لا نصّ، فلا يُترجَم. / The account code as it stands in this company's chart of accounts; an identifier rather than text, so it is never translated. */
  accountCode: string;
  /** نوع الحساب. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The account type. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  accountType: "asset" | "liability" | "equity" | "revenue" | "expense";
  /** هل الحساب مستعمَل؟ المعطَّل لا يُعرَض للاختيار، ولو كان قابلاً للترحيل شكلاً. / Is the account in use? A deactivated account is not offered for selection even when it is structurally postable. */
  active: boolean;
  /** هل هو حساب مقابل يقف على غير جانبه الطبيعي؟ / Is it a contra account, standing on the side opposite its natural one? */
  contra: boolean;
  /** العملة المثبَّتة حين يكون currencyMode = fixed، و null فيما عدا ذلك. / The pinned currency when currencyMode is fixed, and null otherwise. */
  currencyCode: string | null;
  /** نمط العملة: any يقبل أي عملة، و company_only يقبل عملة الشركة وحدها، و fixed يقبل currencyCode وحده. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The currency mode: any accepts any currency, company_only accepts the company currency alone, and fixed accepts only currencyCode. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  currencyMode: "any" | "company_only" | "fixed";
  /** مستوى الحساب في الشجرة. والقابل للترحيل في المستوى الرابع دائماً (GR-COA-001). / The account's level in the tree. A postable account is always at level four (GR-COA-001). */
  level: number;
  /** الاسم العربي — وهو السجلّ لا ترجمةً أولى، وغير فارغ أبداً (ADR-0021). / The Arabic name; it is the record rather than a first translation, and is never blank (ADR-0021). */
  nameAr: string;
  /** ترجمات اسم الحساب: الاسم وسم لغة BCP-47 والقيمة النصّ المترجَم، مرتَّبةً بالوسم ترتيباً حرفياً ثابتاً. وقد تكون فارغة — والعرض يرتدّ حينها إلى الاسم العربي، وهو ارتداد **يُعلَن** لا يقع صامتاً. و**الإنجليزية واحدة من هذه الترجمات لا حقلاً مستقلاً** (ADR-0021 بند 2). / The account name's translations: the name is a BCP-47 language tag and the value is the translated text, ordered by tag with a stable ordinal sort. It may be empty, in which case display falls back to the Arabic name — a fallback that is declared, never silent. **English is one of these translations rather than a field of its own** (ADR-0021 clause 2). */
  nameTranslations: NameValue[];
  /** الجانب الطبيعي للحساب. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The account's natural side. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  naturalSide: "debit" | "credit";
  /** رمز الحساب الأب، و null للجذر. الشجرة تُبنى من هذا الحقل لا من بادئة الرمز: البادئة تصدق على هذا الدليل وتكذب على أول دليل عميل يخالفها. / The parent account's code, or null at a root. Build the tree from this field rather than from the code prefix: the prefix holds for this chart and fails on the first customer chart that departs from it. */
  parentCode: string | null;
  /** هل يقبل هذا الحساب سطراً مباشرةً؟ الحساب التجميعي لا يقبل، ويُرفض الترحيل عليه بـ GR-COA-001. / Does this account accept a line directly? A summary account does not, and posting to it is refused with GR-COA-001. */
  postable: boolean;
  /** الأبعاد الإلزامية على كل سطر يقع على هذا الحساب، وقد تكون فارغة. وغيابُ أحدها يُرفَض بـ guard.GR-COA-002 برسالة تسمّي الحساب والبُعد. والقيم المستعملة في الدليل المرفق: branch و cost_center و project و property و warehouse — و**هي نصوص لا قائمة مغلقة عمداً**: دليلُ عميلٍ يُدخل بُعداً سادساً لا يجوز أن يجعل الخادم يخالف عقده المنشور. / The dimensions mandatory on every line posted to this account; it may be empty. A missing one is refused with guard.GR-COA-002 in a message naming the account and the dimension. The values used in the shipped chart are branch, cost_center, project, property, and warehouse — and they are **deliberately strings rather than a closed list**: a customer chart that introduces a sixth dimension must not put the server in breach of its own published contract. */
  requiredDimensions: string[];
  /** نوع طرف الأستاذ المساعد الذي يطلبه الحساب، و none إن لم يطلب شيئاً. وغيابُ الطرف على حساب يطلبه يُرفَض بـ ledger.posting.missing_subledger برسالة تسمّي الحساب والنوع المطلوب. والقيم في الدليل المرفق أربع عشرة، منها bank_account و customer و employee و item و property و supplier و tenant — و**هي نصّ لا قائمة مغلقة للسبب نفسه**: الدليل بيانات المستأجر، ونوعٌ جديد فيه لا يجوز أن يكسر العقد. / The subledger party type the account requires, or none when it requires nothing. A missing party on an account that requires one is refused with ledger.posting.missing_subledger in a message naming the account and the required type. The shipped chart uses fourteen values, among them bank_account, customer, employee, item, property, supplier, and tenant — and this is **a string rather than a closed list for the same reason**: the chart is tenant data, and a new type in it must not break the contract. */
  subledgerType: string;
}

/** سطر ترحيل. ولاحظ ما ليس فيه: لا حساب ولا رقم حساب. السطر يحمل دوراً، والدور يُحلّ إلى حساب داخل الدفتر عبر خريطة هذه الشركة — فتعديل دليل الحسابات صفٌّ في جدول، لا نشرُ إصدار. / A posting line. Note what is absent: no account, no account code. A line carries a role; the ledger resolves the role to an account through this company's map, so changing the chart of accounts is a table row, not a release. */
export interface PostingLine {
  amount: Money;
  /** أبعاد هذا السطر فوق أبعاد الطلب. / Dimensions for this line on top of the request dimensions. */
  dimensions?: NameValue[];
  narration?: LocalizedText;
  /** مؤهّل الدور حين يُحلّ الدور الواحد إلى حسابات متعددة. / The role qualifier when one role resolves to several accounts. */
  qualifier?: string;
  /** دور السطر في الحدث التجاري — لا حساباً. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The line's role in the business event — never an account. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  role: "NetAmount" | "OutputTax" | "InputTax" | "GrossAmount" | "Discount" | "Retention" | "AdvanceSettlement" | "CostOfGoodsSold" | "InventoryMovement" | "Settlement" | "RoundingDifference" | "ExchangeDifference" | "Accrual" | "Depreciation";
  scope?: Scope;
  /** الجانب. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The side. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  side: "Debit" | "Credit";
  subledger?: Subledger;
}

export interface PostingReceipt {
  /** هل كان مفتاح الحصانة مُرحَّلاً من قبل؟ الوصول الثاني لا يفعل شيئاً ولا يُعدّ خطأ. / Was the idempotency key already posted? A second arrival does nothing and is not an error. */
  alreadyPosted: boolean;
  chainSequence: Int64String;
  /** بصمة القيد في السلسلة، hex صغير. / The entry hash in the chain, lower-case hex. */
  entryHash: string;
  /** معرّف القيد. / The entry identifier. */
  entryId: string;
  entryNumber: Int64String;
  /** جيل الترحيل. / The posting generation. */
  generation: number;
  /** عدد السطور الناتجة بعد تقييم الشروط. / The number of resulting lines after conditions were evaluated. */
  lineCount: number;
  /** رمز الفترة المالية yyyy-MM ميلادياً دائماً — لا يتغيّر بثقافة الخادم ولا بثقافة العميل. / The fiscal period code yyyy-MM, always Gregorian — unaffected by server or client culture. */
  periodCode: string;
}

/** تفاصيل المشكلة بصيغة RFC 9457 بامتدادين: رمز ثابت، ورسالة عربية إلى جانب الإنجليزية. ولا يعبر منها أبداً: نصّ خطأ قاعدة بيانات، أو أثر مكدّس، أو شذرة SQL. / RFC 9457 problem details with two extensions: a stable code and an Arabic message alongside the English one. Never crossing: database error text, a stack trace, or a SQL fragment. */
export interface Problem {
  /** رمز أول خطأ — نقطة الاعتماد البرمجية. / The first error's code — the programmatic contract. */
  code: string;
  /** شرح بالإنجليزية. / The English explanation. */
  detail: string;
  /** شرح بالعربية. / The Arabic explanation. */
  detailAr: string;
  /** كل الأخطاء لا أوّلها فقط: قيد يخالف ثلاث قواعد يُرجعها الثلاث في نداء واحد. / Every error, not just the first: an entry that breaks three rules returns all three in one call. */
  errors: ApiError[];
  /** مسار الطلب. / The request path. */
  instance: string;
  /** رمز حالة HTTP. / The HTTP status code. */
  status: number;
  /** عنوان قصير بالإنجليزية. / A short English title. */
  title: string;
  /** عنوان قصير بالعربية. / A short Arabic title. */
  titleAr: string;
  /** معرّف التتبّع — الرابط الوحيد مع سجلّ الخادم. / The trace id — the only link to the server log. */
  traceId: string;
  /** المرجع الذي يُعرّف نوع المشكلة. / The reference that identifies the problem type. */
  type: string;
}

/** مشروع بحالته وعقوده. / A project with its state and its contracts. */
export interface Project {
  /** الرمز — وهو ما يدخل بُعد المشروع على سطر القيد. / The code — what enters the project dimension on a journal line. */
  code: string;
  /** عقود هذا المشروع. / This project's contracts. */
  contracts: ProjectContractSummary[];
  /** المعرّف الذي تُبنى عليه العقود. / The identifier contracts are built on. */
  id: string;
  /** هل المشروع عامل؟ / Is the project active? */
  isActive: boolean;
  /** الاسم العربي — السجلّ. / The Arabic name — the record. */
  nameAr: string;
  /** الترجمات مرتَّبة بالوسم. / The translations ordered by tag. */
  nameTranslations: NameValue[];
  /** تاريخ البدء ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The start date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  startedOn: string;
}

/** عقد مقاولة **ومعه بنوده المعلَّقة** التي تمنع ترحيل مستخلصاته. / A project contract **together with the pending items** that block posting its certificates. */
export interface ProjectContract {
  /** عملة العقد — عملة المنشأة. / The contract currency — the company currency. */
  currencyCode: string;
  /** العميل. / The customer. */
  customerPartyId: string;
  /** فترة الضمان بالأشهر. / The guarantee period in months. */
  guaranteeMonths: number;
  /** المعرّف. / The identifier. */
  id: string;
  /** الرقم. / The number. */
  number: string;
  /** البنود المعلَّقة التي يرفض بها بابُ الترحيل اليوم. وقائمة فارغة تعني أن كل بند اعتُمد. / The pending items the posting door refuses on today. An empty list means every item has been approved. */
  pendingPolicy: PendingPolicyItem[];
  /** رمز المشروع — وهو ما يدخل بُعد القيد. / The project code — what enters the journal dimension. */
  projectCode: string;
  /** المشروع. / The project. */
  projectId: string;
  retentionRate: Rate;
  /** تاريخ التوقيع ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The signature date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  signedOn: string;
}

/** طلب إنشاء عقد مقاولة. **ولا وعاء لنسبة المحتجز فيه ولا قاعدة استرداد**: موضعُهما نفسه قرارُ محاسبٍ لم يُحسم، ونشرُ أحدهما هنا اختيارٌ لجوابٍ لم يقله أحد. / A request to create a project contract. **It carries no base for the retention rate and no advance recovery rule**: where they belong is itself an unsettled accounting decision, and publishing either here chooses an answer nobody has given. */
export interface ProjectContractRequest {
  /** معرّف العميل في دفتره المساعد — معرّف مبهم لا رقم حساب. / The customer's identifier in its subledger — an opaque identifier, not an account number. */
  customerPartyId: string;
  /** فترة الضمان بالأشهر كما نصّ عليها العقد. / The guarantee period in months as the contract states it. */
  guaranteeMonths: number;
  /** بنود جدول الكميات. / The bill-of-quantities lines. */
  items: BoqItemRequest[];
  /** رقم العقد — يرسله العميل ويُتحقَّق من تفرّده. / The contract number — sent by the client and checked for uniqueness. */
  number: string;
  /** المشروع الذي يقع تحته العقد. / The project this contract falls under. */
  projectId: string;
  retentionRate: Rate;
  /** تاريخ توقيع العقد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The contract signature date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  signedOn: string;
}

/** عقدٌ مختصر تحت مشروعه. / A contract in brief under its project. */
export interface ProjectContractSummary {
  /** عملة العقد. / The contract currency. */
  currencyCode: string;
  /** معرّف العقد. / The contract identifier. */
  id: string;
  /** رقم العقد. / The contract number. */
  number: string;
}

/** قائمة مشاريع المنشأة. / The company's projects. */
export interface ProjectList {
  /** عدد المشاريع. / The number of projects. */
  projectCount: number;
  /** المشاريع مرتَّبة برمزها. / The projects ordered by code. */
  projects: Project[];
}

/** طلب تسجيل مشروع. **ورمزه هوية لا اسم عرض**: هو القيمة الحرفية التي تدخل بُعد المشروع على سطر القيد، فلا تعديل له ولا حذف بعد أن تحمله قيود. / A project registration request. **Its code is an identity, not a display name**: it is the literal value that enters the project dimension on a journal line, so it is never edited and never deleted once entries carry it. */
export interface ProjectRequest {
  /** رمز المشروع داخل المنشأة. / The project code within the company. */
  code: string;
  /** اسم المشروع بالعربية — وهو السجلّ لا ترجمته. / The project's Arabic name — the record itself, not a translation of it. */
  nameAr: string;
  /** ترجمات الاسم، مفاتيحها أوسمة BCP-47. ولا حقل إنجليزي ثابت: الإنجليزية واحدة من N. / The name's translations, keyed by BCP-47 tags. There is no fixed English field: English is one of N. */
  nameTranslations: NameValue[];
  /** تاريخ بدء المشروع ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The project start date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  startedOn: string;
}

/** مستند مقاولات مالي بحالته ومبلغه ومعرّف قيده. و**alreadyPosted معلنٌ في الجسم** لا في رمز الحالة وحده: الرمز يضيع خلف أي وسيط يعيد التوجيه، وعميلٌ أعاد المحاولة بعد انقطاع شبكة يحتاج أن يعرف أيّ النداءين رحّل. / A financial contracting document with its state, its amount, and its entry identifier. **alreadyPosted is declared in the body**, not only in the status code: the code is lost behind any proxy that redirects, and a client retrying after a network cut needs to know which of the two calls posted. */
export interface ProjectsDocument {
  /** true حين ردّ هذا النداءُ ترحيلاً سابقاً بالهوية نفسها. / true when this call returned an earlier posting with the same identity. */
  alreadyPosted: boolean;
  amount: Money;
  /** معرّف القيد إن رُحّل، وnull قبل ذلك. / The entry identifier if posted, and null before that. */
  entryId: string | null;
  /** المعرّف. / The identifier. */
  id: string;
  /** الرقم. / The number. */
  number: string;
  /** حالة المستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The document state. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "DRAFT" | "POSTED";
}

/** عقارٌ كما سُجِّل — **وصفُّه في سجلّ أبعاد الدفتر مكتوبٌ في العملية نفسها**، فبلا ذلك الصفّ تُرفض كل قيوده رفضاً كاملاً لا صامتاً. / A property as registered — **its row in the ledger dimension register is written in the same operation**, and without that row every entry of its is refused totally rather than silently. */
export interface Property {
  /** الرمز. / The code. */
  code: string;
  /** المعرّف الذي تُبنى عليه الوحدات والعقود. / The identifier units and leases are built on. */
  id: string;
  /** الاسم العربي — السجلّ. / The Arabic name — the record. */
  nameAr: string;
  /** ترجمات الاسم. / The name's translations. */
  nameTranslations: NameValue[];
  /** المالك إن وُجد. / The owner if any. */
  ownerId: string | null;
  ownerShareDenominator: Int64String;
  ownerShareNumerator: Int64String;
  /** نموذج الملكية المُسجَّل في الدفتر. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The ownership model as registered in the ledger. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  ownershipModel: "managed_for_others" | "own_property";
}

/** طلب تسجيل عقار. **وownershipModel بلا افتراضي ولا يُعدَّل بعد التسجيل**: هو ما يقرّر أدائنُ الأجرة إيرادَ الشركة أم أمانةً لمالكها، وتغييره بعد الترحيل يُعيد تفسير قيودٍ ماضية بأثر رجعي. وفي نموذج الإدارة المالك إلزامي، وفي الملكية الذاتية إرسالُه يُفشل الطلب. / A property registration request. **ownershipModel has no default and is never edited after registration**: it decides whether the credit of rent is company revenue or a liability to its owner, and changing it after posting reinterprets past entries retroactively. Under the managed model the owner is mandatory; under own property, sending one fails the request. */
export interface PropertyRequest {
  /** رمز العقار — وهو ما يظهر بُعداً على سطر القيد. / The property code — what appears as a dimension on the journal line. */
  code: string;
  /** اسم العقار بالعربية — وهو السجلّ لا ترجمته. / The property's Arabic name — the record itself, not a translation of it. */
  nameAr: string;
  /** ترجمات الاسم بأوسمة BCP-47. ولا حقل إنجليزي ثابت: الإنجليزية واحدة من N. / The name's translations keyed by BCP-47 tags. There is no fixed English field: English is one of N. */
  nameTranslations?: NameValue[];
  /** المالك في نموذج الإدارة، أو null في الملكية الذاتية. / The owner under the managed model, or null under own property. */
  ownerId?: string | null;
  /** نموذج الملكية. own_property: العقار أصلٌ للمنشأة والأجرة إيرادها. managed_for_others: الأجرة المحصَّلة التزام تجاه المالك، وإيراد الشركة هو العمولة وحدها. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The ownership model. own_property: the property is a company asset and the rent is its revenue. managed_for_others: collected rent is a liability to the owner and the company's revenue is the commission alone. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  ownershipModel: "managed_for_others" | "own_property";
}

/** سطر مستند مشتريات كما يخرج على السلك — **معرّفه مدخل المستند التالي في الدورة**. / A purchasing document line as it leaves on the wire — **its identifier is the input to the next document in the cycle**. */
export interface PurchaseDocumentLine {
  /** معرّف السطر. / The line identifier. */
  id: string;
  /** الصنف. / The item. */
  itemId: string;
  /** رقم السطر داخل مستنده. / The line number within its document. */
  lineNo: number;
  quantity: Magnitude;
  /** وحدة القياس. / The unit of measure. */
  unit: string;
  unitPrice: Money;
}

/** سطور مستند مشتريات، مرتَّبة برقم السطر — **وغلافٌ لا مصفوفة عارية**: مصفوفةٌ في جذر الاستجابة لا موضع فيها لعدّاد ولا لصفحة، فأول حاجة إليهما تكسر العقد. / The lines of a purchasing document, ordered by line number — **an envelope, not a bare array**: a root-level array has nowhere to put a count or a page, so the first need for either breaks the contract. */
export interface PurchaseDocumentLineList {
  /** عدد السطور. / The number of lines. */
  lineCount: number;
  /** السطور بمعرّفاتها. / The lines with their identifiers. */
  lines: PurchaseDocumentLine[];
}

/** سطر فاتورة مصروف. ولا حساب فيه ولا رمز حساب، كسطر المبيعات وللسبب نفسه. / An expense bill line. No account and no account code, as on a sales line and for the same reason. */
export interface PurchaseLine {
  description: LocalizedText;
  /** مجموعة الصنف — مؤهّل الدور. / The item group — the role qualifier. */
  itemGroup: string;
  /** الصنف أو البند في دفتره المساعد. / The item or line in its subledger. */
  itemId: string;
  quantity: Quantity;
  /** التصنيف الضريبي. المستعمَل اليوم: standard · zero · exempt. / The tax classification. In use today: standard, zero, exempt. */
  taxClassification: string;
  taxRate: TaxRate;
  /** هل ضريبة هذا السطر قابلة للاسترداد؟ وهي واقعة ضريبية عن السطر لا تُشتقّ من التصنيف. / Is this line's tax recoverable? A tax fact about the line, not derived from its classification. */
  taxRecoverable: boolean;
  unitPrice: Money;
}

/** أمر شراء كما يخرج على السلك. **ولاحظ ما ليس فيه: لا entryId ولا alreadyPosted** — وذلك ليس نقصاً بل هو الفرق نفسه: أمر الشراء لا يُرحَّل أبداً، وحقلٌ فارغ لهما كان سيُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً». وهو المخطّط الوحيد لمستند على هذا السطح بلا هذين الحقلين، والفرق مقصود ومقروء. / A purchase order as it leaves on the wire. **Note what it does not carry: neither entryId nor alreadyPosted** — not a gap but the distinction itself: a purchase order is never posted, and an empty field for either would read as 'not posted yet' rather than 'never posted'. It is the only document schema on this surface without those two fields, and the difference is deliberate and readable. */
export interface PurchaseOrder {
  gross: Money;
  /** معرّف الأمر. / The order identifier. */
  id: string;
  /** السطور بمعرّفاتها — مدخل الاستلام. / The lines with their identifiers — the input a goods receipt needs. */
  lines: PurchaseOrderLine[];
  net: Money;
  /** رقم الأمر. / The order number. */
  number: string;
  /** الحالة: DRAFT · APPROVED · CANCELLED. ولا POSTED عليه أبداً. / The state: DRAFT, APPROVED, CANCELLED. Never POSTED. */
  state: string;
  tax: Money;
}

/** سطر أمر شراء كما يخرج على السلك — **ومعرّفه هو مدخل الاستلام**: سطر الاستلام يشير إليه بمعرّفه هذا. وبدون نشر هذه المعرّفات يصير باب الاستلام باباً لا يوصل إليه بابٌ آخر. / A purchase order line as it leaves on the wire — **and its identifier is the input a goods receipt needs**: a receipt line refers to it by this identifier. Without publishing these identifiers the goods receipt door would be a door no other door on this surface leads to. */
export interface PurchaseOrderLine {
  /** معرّف السطر. / The line identifier. */
  id: string;
  /** الصنف في دفتره المساعد. / The item in its subledger. */
  itemId: string;
  /** رقم السطر داخل الأمر. / The line number within the order. */
  lineNo: number;
  quantity: Quantity;
  unitPrice: Money;
}

/** طلب إنشاء أمر شراء. **ولا حالة فيه ولا ترحيل**: أمر الشراء التزام تعاقدي لا حدث محاسبي، ولا مورد posting له. ولا حقل يشير إلى طلب شراء داخلي: طلب الشراء غير منشور على هذا السطح. / A request to create a purchase order. **It carries no state and no posting**: a purchase order is a contractual commitment, not an accounting event, and has no posting sub-resource. It carries no reference to an internal purchase request either: the purchase request is not published on this surface. */
export interface PurchaseOrderRequest {
  /** مركز التكلفة. / The cost centre. */
  costCenterId: string;
  /** السطور. أمرٌ بلا سطر يُرفض في الوحدة برمزه. / The lines. An order with no line is refused in the module under its own code. */
  lines: PurchaseLine[];
  /** رقم الأمر — فريد داخل المستأجر. / The order number — unique within the tenant. */
  number: string;
  /** تاريخ الأمر. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The order date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  orderedOn: string;
  /** المورد. / The supplier. */
  supplierId: string;
  /** المستودع المستقبِل — يصير بُعد سطر الاستلام حين يُرحَّل. / The receiving warehouse — it becomes the receipt line's dimension when that posts. */
  warehouseId: string;
}

/** طلب إنشاء **مرتجع مشتريات** مسوّدة. **ولا صافي فيه**: المصفوفة تقول إن صافي المرتجع «بتكلفة الاستلام الأصلي لا بتكلفة اليوم»، وتلك التكلفة يملكها دفتر المخزون وحده — فيُسلَّم ما يملكه المستدعي: الكمّية وسطر الاستلام والضريبة. / A **purchase return** draft request. **It carries no net**: the matrix says the return net is 'at the original receipt cost, not today's cost', and only the inventory subledger owns that cost — so what the caller owns is what is sent: the quantity, the receipt line, and the tax. */
export interface PurchaseReturnRequest {
  /** الفاتورة المخزنية الأصلية. / The original stock bill. */
  billId: string;
  /** تاريخ المرتجع الميلادي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The Gregorian return date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** رقم المرتجع. / The return number. */
  number: string;
  quantity: Quantity;
  /** سطر الاستلام الذي تُردّ بضاعته — به يُقيَّم المرتجع. / The goods receipt line whose goods are being returned — the return is valued by it. */
  receiptLineId: string;
  tax: Money;
}

export interface PutCapabilityProfileRequest {
  /** أنواع المستندات. / The document types. */
  documents: DocumentProfile[];
  /** سبب سحب قدرة. إلزامي متى أطفأ الطلب قدرةً كانت مُشغَّلة، ومهمَل فيما عدا ذلك؛ وثمانية محارف على الأقل — «لا سبب» ليس سبباً. / The reason for withdrawing a capability. Required whenever the request disables a previously enabled capability, ignored otherwise; at least eight characters — 'no reason' is not a reason. */
  withdrawalReason?: string | null;
}

/** كمّية نصّاً بمقياس لا يتجاوز أربعاً، بالنحو الذي تخضع له المبالغ. وهي ليست مبلغاً — ولذلك لها مخطّطها — لكنها تُضرب في مبلغ، فأي فقدان دقّة فيها يصل إلى المال. / A quantity as a string with at most four decimal places, under the grammar that governs amounts. It is not an amount — hence its own schema — but it is multiplied by one, so any precision lost in it reaches the money. */
/* Quantity مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** نسبة تعاقدية **كسراً عشرياً لا نسبة مئوية**: عشرة بالمئة تُكتب 0.10 لا 10. والمقياس ثمانٍ لا أربع: النسبة ليست مبلغاً ولا تُقرَّب إلى الهللة. **وهي تأتي من العقد لا من قيمة ثابتة في الكود** — نصّ مصفوفة الترحيل على المحتجز بحرفه. / A contractual rate as a **decimal fraction, not a percentage**: ten percent is written 0.10, never 10. The scale is eight, not four: a rate is not an amount and is not rounded to the halala. **It comes from the contract, never from a constant in code** — the posting matrix's text on retention, verbatim. */
/* Rate مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** طرفٌ عقاري كما سُجِّل. / A real-estate party as registered. */
export interface RealEstateParty {
  /** الرمز. / The code. */
  code: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** الاسم العربي. / The Arabic name. */
  nameAr: string;
  /** ترجمات الاسم. / The name's translations. */
  nameTranslations: NameValue[];
  /** دور الطرف في هذه الوحدة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The party's role in this module. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  role: "broker" | "lessee" | "owner";
  /** الإقامة الضريبية. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The tax residency. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  taxResidency: "non_resident" | "resident";
  /** رقم التسجيل الضريبي. / The VAT registration number. */
  vatNumber: string;
}

/** طلب تسجيل طرف عقاري — مستأجر أو مالك. **وtaxResidency بلا افتراضي**: عليها يتوقّف سطر الاستقطاع في توريد المالك، وقيمةٌ افتراضية «مقيم» تُسقطه بصمت عمّن لم يُملأ حقله. / A real-estate party registration request — a lessee or an owner. **taxResidency has no default**: the withholding line on an owner payout depends on it, and a default of 'resident' silently drops it for whoever's field was left unfilled. */
export interface RealEstatePartyRequest {
  /** رمز الطرف داخل المنشأة — هوية يحملها تاريخه المُرحَّل. / The party code within the company — an identity its posted history carries. */
  code: string;
  /** الاسم بالعربية — السجلّ. / The Arabic name — the record. */
  nameAr: string;
  /** ترجمات الاسم. / The name's translations. */
  nameTranslations?: NameValue[];
  /** الإقامة الضريبية. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The tax residency. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  taxResidency: "non_resident" | "resident";
  /** رقم التسجيل الضريبي، أو نصّ فارغ لمن لا رقم له — والغياب واقعة لا نقص. / The VAT registration number, or an empty string for a party without one — its absence is a fact, not a gap. */
  vatNumber: string;
}

/** تخصيص مبلغ من سند قبض على فاتورة مبيعات **مُرحَّلة**. ولا عميل فيه: عميلُه عميل السند، والفاتورة تُفحص أنها له — وإعادة ذكره كانت ستفتح باباً لتخصيصٍ على فاتورة عميل آخر. / An allocation of part of a receipt against a **posted** sales invoice. It carries no customer: the customer is the receipt's, and the invoice is checked to be theirs — repeating it would open a door to allocating against another customer's invoice. */
export interface ReceiptAllocation {
  amount: Money;
  /** الفاتورة المُرحَّلة التي يُنزَل عليها المبلغ. / The posted invoice the amount is applied to. */
  invoiceId: string;
}

/** طلب التسجيل الأول. **ولا حقل خطّة فيه**: هذا بابٌ يُخدَم بلا اعتماد، وحقلٌ يختار منه الطالب حزمته يمنح الحزمة الشاملة لمن كتب اسمها. **ولا حقل مستأجر ولا معرّف منشأة**: كلاهما مشتقٌّ حتمياً من requestKey، وهو ما يجعل إعادة الإرسال تصل إلى المستأجر نفسه لا إلى ثانٍ. / The first-registration request. **It carries no plan field**: this door is served without a credential, and a field letting the caller pick their package hands the full one to whoever types its name. **It carries no tenant and no company identifier**: both are derived deterministically from requestKey, which is what makes a resend reach the same tenant rather than a second one. */
export interface RegisterTenantRequest {
  /** اسم المنشأة بالعربية — وهو السجلّ لا ترجمته. / The company's Arabic name — the record itself, not a translation of it. */
  companyNameAr: string;
  /** ترجمات اسم المنشأة، مفاتيحها أوسمة BCP-47. ولا حقل إنجليزي ثابت: الإنجليزية واحدة من N. وسجل الأسطول يقرأ منها الوسم en لتقاريره، ويرتدّ إلى العربية إن غاب. / The company name's translations, keyed by BCP-47 tags. There is no fixed English field: English is one of N. The fleet registry reads the en tag from here for its reporting and falls back to Arabic when absent. */
  nameTranslations?: NameValue[];
  /** اسم أول مالك بالعربية — يظهر في قائمة الأعضاء وفي سجلّ التدقيق. / The first owner's Arabic name — it appears in the member list and the audit log. */
  ownerNameAr: string;
  /** مفتاح الطلب: قيمة **عشوائية** يولّدها العميل ويحتفظ بها، ومنها تُشتقّ كل معرّفات التسجيل اشتقاقاً حتمياً. فإعادةُ الإرسال به تردّ المستأجر نفسه ولا تُنشئ ثانياً — ومفتاحٌ قصير يصير تخمينُه ممكناً. / The request key: a **random** value the client generates and keeps, from which every registration identifier is derived deterministically. Resending it returns the same tenant rather than creating a second one — and a short key becomes guessable. */
  requestKey: string;
}

/** مستأجرٌ سُجِّل، ومعه ما يفتح به مالكُه جلسته. وenrolmentCredential يخرج **مرّة واحدة** — المُودَع بصمته — وهو معدومٌ عند إعادة الإرسال بالمفتاح نفسه: النتيجة هي هي، والسرّ لا يُسكّ مرّتين. / A registered tenant with what opens its owner's session. enrolmentCredential leaves the server **once** — only its digest is stored — and it is null on a resend with the same key: the result is the same, and the secret is not minted twice. */
export interface RegisteredTenant {
  /** true حين ردّ هذا الطلبُ تسجيلاً سابقاً بالمفتاح نفسه، ومعه رمز 200 بدل 201. / true when this request returned an earlier registration with the same key, alongside 200 instead of 201. */
  alreadyRegistered: boolean;
  /** أول منشأة للمستأجر — وهي التي تُؤسَّس ويُرحَّل فيها. / The tenant's first company — the one that is set up and posted into. */
  companyId: string;
  /** اعتماد الانتساب، أو null عند إعادة الإرسال. / The enrolment credential, or null on a resend. */
  enrolmentCredential: string | null;
  /** لحظة انقضاء الدعوة بصيغة ISO 8601 الدوّارة، أو null عند إعادة الإرسال. / The invitation's expiry in round-trip ISO 8601, or null on a resend. */
  enrolmentExpiresAt: string | null;
  owner: Membership;
  subscription: Subscription;
  /** رمز المستأجر القصير في سجل الأسطول — مشتقٌّ من معرّفه ولا يختاره العميل. / The tenant's short code in the fleet registry — derived from its identifier, never chosen by the client. */
  tenantCode: string;
  /** المستأجر المُنشأ. / The created tenant. */
  tenantId: string;
}

/** طلب تجديد جلسة. واعتماد التجديد يُستهلك بهذا النداء ولا يُقبل ثانيةً — وتقديمه مرّتين يُسقط العائلة كلّها. / A request to renew a session. The refresh credential is consumed by this call and never accepted again — presenting it twice drops the whole family. */
export interface RenewSessionRequest {
  /** اعتماد التجديد الجاري. نصٌّ مبهم لا بنية فيه: لا يُحلَّل ولا يُشتقّ منه شيء، ويُقدَّم كما وصل. / The current refresh credential. An opaque string with no structure: never parsed, nothing derived from it, presented exactly as received. */
  refreshCredential: string;
}

/** فاتورة إيجار بحالتها ومجاميعها وحدثها. وexemptionReasonPending **علامة ظاهرة** على إعفاءٍ بلا رمز سبب: الرمز يُؤخذ من القائمة الرسمية السارية ولا يُخترع، وغيابه يُرى في التقرير لا في تعليق. / A rent invoice with its state, totals, and event. exemptionReasonPending is a **visible flag** on an exempt invoice with no reason code: the code comes from the official list in force and is never invented, and its absence is seen in the report rather than in a comment. */
export interface RentInvoice {
  /** هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟ **معلومة لا تُشتقّ من الحالة**: مستندٌ حالته POSTED بعد النداء لا يقول وحده أيُّ النداءين رحّله. / Was this identity already posted before this call? **Not derivable from the state**: a document that is POSTED after the call does not by itself say which call posted it. */
  alreadyPosted: boolean;
  /** معرّف القيد إن رُحّلت، وإلا null. / The entry identifier if posted, otherwise null. */
  entryId: string | null;
  /** الحدث الذي اختارته الوحدة من نموذج الملكية المُسجَّل — لا من الطلب. / The event the module selected from the registered ownership model — never from the request. */
  eventCode: string;
  /** رمز سبب الإعفاء، ونصٌّ فارغ ما دام غير معروف. / The exemption reason code, an empty string while it is unknown. */
  exemptionReasonCode: string;
  /** إعفاءٌ بلا رمز سبب — علامة ظاهرة لا تعليق في شيفرة. / An exemption with no reason code — a visible flag, not a comment in code. */
  exemptionReasonPending: boolean;
  gross: Money;
  /** المعرّف. / The identifier. */
  id: string;
  net: Money;
  /** الرقم. / The number. */
  number: string;
  /** حالة الفاتورة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The invoice state. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "DRAFT" | "POSTED";
  tax: Money;
  /** معاملة الوحدة الضريبية المنسوخة وقت الإصدار. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The unit's VAT treatment as copied at issue time. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  vatTreatment: "exempt" | "standard";
}

/** طلب إنشاء فاتورة إيجار مسوّدة. **ولا رمز حدث فيه ولا نموذج ملكية ولا مبالغ سطور**: المبالغ من جدول الدفعات، والحدث من نموذج الملكية المُسجَّل في الدفتر. وtaxRate تصل مع الطلب ولا تُكتب في شيفرة، ولا تُطبَّق إلا على وحدةٍ معاملتها standard. / A request to draft a rent invoice. **It carries no event code, no ownership model, and no line amounts**: amounts come from the payment schedule and the event from the ownership model registered in the ledger. taxRate arrives with the request rather than being written in code, and applies only to a unit whose treatment is standard. */
export interface RentInvoiceRequest {
  /** تاريخ الإصدار الميلادي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The issue date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** العقد — ويجب أن يكون سارياً. / The lease — it must be active. */
  leaseId: string;
  /** رقم الفاتورة — فريد داخل المنشأة. / The invoice number — unique within the company. */
  number: string;
  /** معرّفات الأقساط المفوترة كما نشرها مورد جدول الدفعات. / The identifiers of the instalments being billed, as published by the schedule resource. */
  scheduleLineIds: string[];
  taxRate: Money;
}

/** طلب تحصيل محتجزٍ مدين من العميل. / A request to collect debit retention from the client. */
export interface RetentionCollectionRequest {
  amount: Money;
  /** تاريخ التحصيل ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The collection date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  collectedOn: string;
  /** رقم المستند. / The document number. */
  number: string;
  /** حركة المحتجز المُحصَّلة. / The retention movement being collected. */
  retentionMovementId: string;
  /** طريقة التسوية — مؤهّل دور لا حساب. / The settlement method — a role qualifier, not an account. */
  settlementMethod: string;
  /** طرف الخزينة في دفترها المساعد. / The treasury party in its subledger. */
  treasuryPartyId: string;
}

/** سجلّ المحتجزات مدينةً ودائنة، **مشتقّاً من المُرحَّل وحده**. ولا عمود رصيدٍ يُنقَص في أي مكان: كل رصيدٍ قابل لإعادة الاشتقاق من أسطر الدفتر. / The retention register, debit and credit, **derived from posted entries alone**. No balance column is decremented anywhere: every balance is re-derivable from the ledger's lines. */
export interface RetentionRegister {
  /** تاريخ القراءة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The as-of date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  asOf: string;
  payableTotal: Money;
  receivableTotal: Money;
  /** الدفعات بتاريخها. / The movements by date. */
  rows: RetentionRegisterRow[];
}

/** دفعة محتجزٍ واحدة برصيدها القائم. **ومعرّف الحركة هو ما يُفرَج عنه أو يُحصَّل** — لا رصيد مجمَّع. / One retention movement with its outstanding balance. **The movement identifier is what a release or a collection acts upon** — never an aggregated balance. */
export interface RetentionRegisterRow {
  amount: Money;
  /** معرّف المستند الذي أنشأ الحركة. / The identifier of the document that created the movement. */
  documentId: string;
  /** نوع ذلك المستند. / That document's type. */
  documentType: string;
  /** تاريخ استحقاق الإفراج، مشتقّاً من فترة الضمان في العقد ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The release due date, derived from the contract's guarantee period. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  dueOn: string;
  /** تاريخ الحركة ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The movement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  movedOn: string;
  /** معرّف الحركة. / The movement identifier. */
  movementId: string;
  outstanding: Money;
  /** الطرف. / The party. */
  partyId: string;
  /** نوع الدفتر المساعد للطرف: customer أو subcontractor. / The party's subledger kind: customer or subcontractor. */
  partyKind: string;
  /** رمز المشروع. / The project code. */
  projectCode: string;
  /** الجانب: RECEIVABLE محتجزٌ مدين لدى العميل · PAYABLE محتجزٌ دائن على المقاول. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The side: RECEIVABLE for debit retention held by the client, PAYABLE for credit retention owed to the subcontractor. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  side: "RECEIVABLE" | "PAYABLE";
}

/** طلب إفراج عن محتجزٍ دائن على **دفعة محتجزٍ مُسمّاة** لا على رصيد، باعتمادٍ صريح يشترطه نصّ الإطلاق. / A request to release credit retention against **a named retention movement** rather than a balance, with the explicit approval the trigger text requires. */
export interface RetentionReleaseRequest {
  amount: Money;
  /** المعتمِد — وقيدُ تحقّق في قاعدة البيانات يرفض اعتماداً فارغاً. / The approver — a database check constraint refuses an empty approver. */
  approvedBy: string;
  /** رقم المستند. / The document number. */
  number: string;
  /** تاريخ الإفراج ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The release date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  releasedOn: string;
  /** حركة المحتجز المُفرَج عنها، كما يُرجعها سجلّ المحتجزات. / The retention movement being released, as the retention register returns it. */
  retentionMovementId: string;
}

export interface ReverseJournalEntryRequest {
  closedPeriodAuthorisation?: ClosedPeriodAuthorisation;
  reason: LocalizedText;
  /** تاريخ قيد العكس، أو غيابه فيُتخذ تاريخ القيد الأصلي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The reversing entry's date; omit to take the original entry's date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  reversalDate?: string;
}

/** طلب إنشاء فاتورة مبيعات مسوّدة. ولا مجاميع فيه: المجاميع تُحسب في الوحدة على السطر ثم تُجمع، ومجموعٌ يرسله العميل كان سيصير مصدر حقيقة ثانياً يستطيع أن ينحرف. / A request to draft a sales invoice. It carries no totals: totals are computed in the module per line and then summed, and a total sent by the client would be a second source of truth able to diverge. */
export interface SalesInvoiceRequest {
  /** الفرع — بُعد تحليلي إلزامي على الإيراد. / The branch — a mandatory analytical dimension on revenue. */
  branchId: string;
  /** معرّف العميل. / The customer identifier. */
  customerId: string;
  /** تاريخ الإصدار. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The issue date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** السطور. فاتورة بلا سطر تُرفض في الوحدة برمزها. / The lines. An invoice with no line is refused in the module under its own code. */
  lines: SalesLine[];
  /** رقم الفاتورة — فريد داخل المستأجر. / The invoice number — unique within the tenant. */
  number: string;
}

/** سطر مستند مبيعات. **ولا حساب فيه ولا رمز حساب**: يحمل itemGroup — مؤهّل دور — والمصفوفة وحدها تُحوّله إلى حساب (القاعدة 2 ممتدّةً إلى السلك). / A sales document line. **No account and no account code**: it carries an itemGroup — a role qualifier — and the matrix alone turns it into an account (Rule 2 extended to the wire). */
export interface SalesLine {
  description: LocalizedText;
  discount: Money;
  /** مجموعة الصنف — مؤهّل الدور. / The item group — the role qualifier. */
  itemGroup: string;
  /** على سطر الإشعار الدائن وحده: سطر الفاتورة الذي تُردّ بضاعته. وnull تعني **تخفيض قيمة لا ردّ بضاعة** — إشعارٌ لا يُحرّك مخزوناً ولا يُرحّل قيد تكلفة. والفرق قرار تجاري لا يُخمَّن. / On a credit note line only: the invoice line whose goods are returned. null means a **value reduction, not a goods return** — a note that moves no stock and posts no cost entry. The difference is a commercial decision, never guessed. */
  originalInvoiceLineId?: string | null;
  quantity: Quantity;
  /** التصنيف الضريبي. المستعمَل اليوم: standard · zero · exempt — ويُنشر نصّاً لا مجموعةً مغلقة، فلا قيد تحقّق واحد يُغلقه، وتضييقُه بعد نشره يفرض v2. / The tax classification. In use today: standard, zero, exempt — published as text rather than a closed set, since no check constraint closes it, and narrowing it after publication would force v2. */
  taxClassification: string;
  taxRate: TaxRate;
  unitPrice: Money;
}

/** النطاق التحليلي للسطر. و costCenterId **اختياري وغير قابل لأن يكون null**: حذف الحقل يعني «المركز الافتراضي لهذه المنشأة»، وهو افتراض معلن لا صمت. أما القيمة null فلا معنى لها — لكل منشأة مركز تكلفة واحد على الأقل، ولا سطر بلا مركز، ورمزٌ يُرسَل null كان يقول «بلا مركز» وهي حالة لا وجود لها في النظام. / The line's analytical scope. costCenterId is **optional but never null**: omitting the field means 'this company's default centre', a published default rather than silence. The value null has no meaning — every company has at least one cost centre and no line is without one, so a null said 'no centre', a state that does not exist in the system. */
export interface Scope {
  /** الفرع. / The branch. */
  branchId?: string | null;
  /** مركز التكلفة. اتركه محذوفاً ليُرحَّل السطر على المركز الافتراضي للمنشأة، أو سمِّ مركزاً عاملاً. والمركز المُسمّى غير الموجود يُرفض بـcost_center.not_found، والموقوف بـcost_center.already_suspended — ولا يرتدّ أيّهما إلى الافتراضي بصمت. / The cost centre. Omit it to post the line on the company's default centre, or name an active centre. A named centre that does not exist is refused with cost_center.not_found and a suspended one with cost_center.already_suspended — neither falls back to the default silently. */
  costCenterId?: string;
  /** المشروع. / The project. */
  projectId?: string | null;
}

/** الهوية خلف الاعتماد والشركات التي يبلغها. ولا شيء منها من جسم الطلب ولا من ترويسة يكتبها العميل. وcompanyCount لا يكون صفراً أبداً: الصفر رفضٌ بـsession.no_reachable_company لا قائمة فارغة. / The identity behind the credential and the companies it reaches. None of it comes from a request body or a client-written header. companyCount is never zero: zero is a refusal with session.no_reachable_company, not an empty list. */
export interface Session {
  /** الشركات مرتَّبة بمعرّفها ترتيباً حرفياً ثابتاً. / The companies, ordered by identifier in a stable ordinal order. */
  companies: SessionCompany[];
  /** عدد الشركات المبلوغة. لا يكون صفراً أبداً. / The number of reachable companies. Never zero. */
  companyCount: number;
  /** المستأجر خلف الاعتماد. / The tenant behind the credential. */
  tenantId: string;
  /** المستخدم خلف الاعتماد. / The user behind the credential. */
  userId: string;
}

/** شركة يبلغها الاعتماد. والاسم العربي هو السجلّ، وnameTranslations ترجماته بوسم اللغة — ولا حقل ثابت للإنجليزية: هي واحدة من N (ADR-0021). والحقول المشتقّة من التأسيس تصل null حين state = NotSetUp، لأنها تُسنَد عند التأسيس ولا يُخترَع لها قيمة قبله. / A company the credential reaches. The Arabic name is the record and nameTranslations are its translations by language tag — there is no fixed English field: English is one of N (ADR-0021). The setup-derived fields arrive null when state = NotSetUp, because they are assigned at setup and no value is invented before it. */
export interface SessionCompany {
  /** معرّف الشركة كما يُكتب في المسار. / The company identifier as written in the path. */
  companyId: string;
  /** عدد الخانات العشرية المعروضة لهذه المنشأة. / This company's displayed decimal places. */
  decimalPlaces: number | null;
  /** رمز مركز التكلفة الافتراضي. / The default cost centre code. */
  defaultCostCenter: string | null;
  /** الاسم العربي — السجلّ، لا ترجمةً أولى. / The Arabic name — the record, not a first translation. */
  nameAr: string | null;
  /** ترجمات الاسم بوسم اللغة BCP-47، مرتَّبة ترتيباً حرفياً ثابتاً. / The name's translations by BCP-47 language tag, in a stable ordinal order. */
  nameTranslations: NameValue[];
  /** Ready لمنشأة مؤسَّسة، وNotSetUp لمنشأة يبلغها الاعتماد ولم تُؤسَّس بعد. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / Ready for a company that is set up, NotSetUp for one the credential reaches that has not been set up yet. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "NotSetUp" | "Ready";
}

/** إبطال جلسة: ما أُبطل، ومتى، ولماذا برمزٍ من مجموعة مغلقة يقرؤها العميل ولا يفسّر نصّاً. / A session revocation: what was revoked, when, and why — by a code from a closed set the client reads rather than prose it interprets. */
export interface SessionRevocation {
  /** signed_out حين يطلبه صاحب الجلسة، وrefresh_replayed حين يُسقطها كشفُ إعادة استعمال اعتماد تجديد. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / signed_out when the holder asks for it, refresh_replayed when refresh-reuse detection drops it. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  reason: "refresh_replayed" | "signed_out";
  /** لحظة الإبطال. بصيغة ISO 8601 الدوّارة بتوقيت UTC وبأرقام لاتينية — الصيغة نفسها التي يقرأ بها الخادم صلاحية اعتماده من إعداده، فلا وقتٌ يُكتب بشكل ويُقرأ بآخر. / When the revocation took effect. In round-trip ISO 8601, UTC, Latin digits — the same spelling the server reads a credential expiry with from its own configuration, so no instant is written one way and read another. */
  revokedAt: string;
  /** الجلسة المُبطَلة. / The revoked session. */
  sessionId: string;
}

export interface SourceDocument {
  /** معرّف المستند داخل تلك الوحدة. / The document identifier within that module. */
  documentId: string;
  /** نوع المستند داخل تلك الوحدة. / The document type within that module. */
  documentType: string;
  /** الوحدة المالكة للمستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The module that owns the document. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  module: "Core" | "Ledger" | "Sales" | "Purchasing" | "Compliance" | "Inventory" | "Pos" | "Hr" | "Projects" | "RealEstate" | "Assets" | "Portals" | "Ai";
}

/** رصيد صنف في موقعٍ من مستودع — **مفتاحه أربعة أبعاد**: المنشأة والصنف والمستودع والموقع. / The balance of an item in a location within a warehouse — **its key has four dimensions**: company, item, warehouse, and location. */
export interface StockBalance {
  /** هل ورد هذا الصنف إلى هذا الموقع مرّةً بتكلفة؟ **حقلٌ مستقلّ عن unitCost عمداً**: بدونه لا يُفرَّق بين «تكلفة الوحدة صفر لأن الصنف لم يُستلم قط» و«تكلفته صفر فعلاً». / Has this item ever been received into this location with a cost? **A field separate from unitCost on purpose**: without it there is no telling 'the unit cost is zero because it was never received' from 'its cost really is zero'. */
  hasCostBasis: boolean;
  /** الصنف. / The item. */
  itemId: string;
  /** الموقع داخل المستودع. / The location within the warehouse. */
  locationId: string;
  quantity: Measure;
  unitCost: UnitCost;
  value: Money;
  /** المستودع. / The warehouse. */
  warehouseId: string;
}

/** أرصدة المخزون، مرتَّبة بالصنف ثم المستودع ثم الموقع. / The stock balances, ordered by item then warehouse then location. */
export interface StockBalanceList {
  /** عدد الأرصدة. / The number of balances. */
  balanceCount: number;
  /** الأرصدة. / The balances. */
  balances: StockBalance[];
}

/** سطر فاتورة مورد مخزنية — يرجع إلى سطر استلام بعينه، وهو ضلع المطابقة الثالث. / A stock supplier bill line — it refers to a specific goods receipt line, the third side of the match. */
export interface StockBillLine {
  quantity: Quantity;
  /** معرّف سطر الاستلام. / The goods receipt line identifier. */
  receiptLineId: string;
  /** التصنيف الضريبي. / The tax classification. */
  taxClassification: string;
  taxRate: TaxRate;
  unitPrice: Money;
}

/** طلب إنشاء فاتورة مورد **مخزنية** مسوّدة تُطابَق ثلاثياً. / A **stock** supplier bill draft request, three-way matched. */
export interface StockBillRequest {
  /** تاريخ الفاتورة الميلادي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The Gregorian bill date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  issuedOn: string;
  /** السطور. / The lines. */
  lines: StockBillLine[];
  /** رقم الفاتورة. / The bill number. */
  number: string;
  /** الاستلام الذي تُطابَق به. / The goods receipt it is matched against. */
  receiptId: string;
}

/** مستند حركة مخزون كما يخرج على السلك. / A stock movement document as it leaves on the wire. */
export interface StockMovement {
  /** هل كانت هذه الهوية مُرحَّلة **قبل** هذا الطلب؟ ولا تُشتقّ من state: المستند بعد أي ترحيل ناجح حالته POSTED — الأول والثاني سواء. / Was this identity already posted **before** this request? It is not derivable from state: after any successful post the document is POSTED, first arrival and second alike. */
  alreadyPosted: boolean;
  cost: Money;
  /** الاتجاه. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The direction. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  direction: "IN" | "OUT";
  /** معرّف القيد إن رُحّل، وnull إن كان مسوّدة. / The journal entry identifier if posted, and null while a draft. */
  entryId: string | null;
  /** المعرّف. / The identifier. */
  id: string;
  /** مجموعة الصنف. / The item group. */
  itemGroup: string;
  /** الصنف. / The item. */
  itemId: string;
  /** الموقع داخل المستودع. / The location within the warehouse. */
  locationId: string;
  /** الرقم. / The number. */
  number: string;
  /** تاريخ الحركة. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The movement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  occurredOn: string;
  quantity: Measure;
  /** الحالة: DRAFT · POSTED. / The state: DRAFT or POSTED. */
  state: string;
  /** المستودع. / The warehouse. */
  warehouseId: string;
}

/** مستندات حركة المخزون، مرتَّبة بالتاريخ ثم بالرقم. / The stock movement documents, ordered by date then by number. */
export interface StockMovementList {
  /** عدد المستندات. / The number of documents. */
  movementCount: number;
  /** المستندات. / The documents. */
  movements: StockMovement[];
}

/** طلب إنشاء مستند حركة مخزون **مسوّدة**: تسوية جرد، أو رصيد افتتاحي، أو إعدام. والتكلفة **على الوارد وحده** — الصادر تُحسب تكلفته في وحدة المخزون ولا تُملى، فتُرسَل عليه "0". / A request to create a **draft** stock movement document: a count adjustment, an opening balance, or a write-off. Cost is **for inbound only** — an outbound movement is valued by the inventory module and never dictated, so send "0" for it. */
export interface StockMovementRequest {
  cost: Money;
  /** IN زيادة جرد أو رصيد افتتاحي · OUT عجز أو إعدام. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / IN for a count surplus or opening balance; OUT for a shortage or write-off. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  direction: "IN" | "OUT";
  /** مجموعة الصنف — مؤهّل الدور. / The item group — a role qualifier. */
  itemGroup: string;
  /** رمز الصنف. / The item code. */
  itemId: string;
  /** الموقع داخل المستودع — بُعدٌ في مفتاح الرصيد لا وصفٌ عليه. و DEFAULT للمستودع الذي لم يُسكَّن بعد. / The location within the warehouse — a dimension in the balance key, not a description on it. Use DEFAULT for a warehouse that is not binned yet. */
  locationId: string;
  /** رقم المستند — فريد داخل المنشأة. / The document number — unique within the company. */
  number: string;
  /** تاريخ الحركة الميلادي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The Gregorian movement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  occurredOn: string;
  quantity: Measure;
  /** المستودع. / The warehouse. */
  warehouseId: string;
}

/** مستند نقلٍ بين موقعين كما يخرج على السلك. **ولا حقل entryId فيه** — بخلاف StockMovement: هذا المستند لا يُرحَّل إلى دفتر الأستاذ أبداً، وحقلٌ لمعرّف قيدٍ لا يُملأ قطّ يجعل كل قارئ يسأل متى يُملأ. / A transfer document between two locations as it leaves on the wire. **It has no entryId field** — unlike StockMovement: this document is never posted to the general ledger, and a field for an entry identifier that is never filled makes every reader ask when it would be. */
export interface StockTransfer {
  /** هل كانت هذه الهوية مُنفَّذة **قبل** هذا الطلب؟ ولا تُشتقّ من state: المستند بعد أي تنفيذ ناجح حالته MOVED — الأول والثاني سواء. / Was this identity already executed **before** this request? It is not derivable from state: after any successful execution the document is MOVED, first arrival and second alike. */
  alreadyMoved: boolean;
  /** موقع المصدر. / The source location. */
  fromLocationId: string;
  /** مستودع المصدر. / The source warehouse. */
  fromWarehouseId: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** مجموعة الصنف. / The item group. */
  itemGroup: string;
  /** الصنف. / The item. */
  itemId: string;
  /** الرقم. / The number. */
  number: string;
  /** تاريخ النقل. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The transfer date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  occurredOn: string;
  quantity: Measure;
  /** الحالة: DRAFT مسوّدة · MOVED مُنفَّذ. **و MOVED لا POSTED عمداً**: الثانية تعني في هذا العقد «صار له قيد»، وحالةٌ تحمل الاسم بلا قيد كانت ستجعل كل قارئ يبحث عن قيدٍ لا وجود له. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The state: DRAFT or MOVED. **MOVED, not POSTED, deliberately**: POSTED means 'it has an entry' in this contract, and a state carrying that name with no entry would send every reader hunting for one that does not exist. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "DRAFT" | "MOVED";
  /** موقع الوجهة. / The destination location. */
  toLocationId: string;
  /** مستودع الوجهة. / The destination warehouse. */
  toWarehouseId: string;
  value: Money;
}

/** مستندات النقل، مرتَّبة بالتاريخ ثم بالرقم. / The transfer documents, ordered by date then by number. */
export interface StockTransferList {
  /** عدد المستندات. / The number of documents. */
  transferCount: number;
  /** المستندات. / The documents. */
  transfers: StockTransfer[];
}

/** طلب إنشاء مستند نقلٍ بين موقعين **مسوّدة**. **ولا حقل تكلفة فيه**: المنقول يخرج بتكلفة مصدره المتحرّكة لحظة النقل، وتُحسب في وحدة المخزون ولا تُملى (ADR-0039). وحقلُ تكلفةٍ هنا كان سيسمح بنقلٍ يُعيد تسعير البضاعة وهو ينقلها — أي بجعل حركة مكانٍ حركةَ قيمة. / A request to create a **draft** transfer document between two locations. **It carries no cost field**: what moves leaves at its source's moving average cost at the moment of transfer, computed by the inventory module and never dictated (ADR-0039). A cost field here would allow a transfer to reprice goods while relocating them — turning a movement of place into a movement of value. */
export interface StockTransferRequest {
  /** موقع المصدر. / The source location. */
  fromLocationId: string;
  /** مستودع المصدر. / The source warehouse. */
  fromWarehouseId: string;
  /** مجموعة الصنف — مؤهّل الدور، وهي **واحدة على الطرفين** لأن الصنف واحد. / The item group — a role qualifier, and **the same on both sides** because the item is the same. */
  itemGroup: string;
  /** رمز الصنف — **واحدٌ على الطرفين**: النقل يحرّك صنفاً لا يبدّله. / The item code — **the same on both sides**: a transfer relocates an item, it does not substitute it. */
  itemId: string;
  /** رقم المستند — فريد داخل المنشأة. / The document number — unique within the company. */
  number: string;
  /** تاريخ النقل الميلادي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The Gregorian transfer date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  occurredOn: string;
  quantity: Measure;
  /** موقع الوجهة. / The destination location. */
  toLocationId: string;
  /** مستودع الوجهة. / The destination warehouse. */
  toWarehouseId: string;
}

/** موضعٌ في هرم التسكين كما يخرج على السلك. / A place in the placement hierarchy as it leaves on the wire. */
export interface StoragePlace {
  /** الرمز. / The code. */
  code: string;
  /** المعرّف الذي تُبنى عليه القراءة. / The identifier reads are built on. */
  id: string;
  /** هل هو عامل؟ **والتعطيل حالةٌ تُقرأ لا غياب**: المُعطَّل يبقى في القوائم بهذا الحقل false، لأن رمزه محمولٌ على حركات مضت ولا يُحذف. / Is it active? **Deactivation is a readable state, not an absence**: a deactivated place stays in the lists with this field false, because its code is carried by past movements and is never deleted. */
  isActive: boolean;
  /** المستوى في الهرم. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The level in the hierarchy. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  level: "WAREHOUSE" | "LOCATION" | "BIN";
  name: LocalizedText;
  /** رمز الأب — **نصّ فارغ للمستودع** لأنه أعلى الهرم. ورمز المستودع للموقع، ورمز الموقع للرفّ. / The parent's code — **an empty string for a warehouse**, which is the top of the hierarchy. The warehouse code for a location, and the location code for a bin. */
  parentCode: string;
}

/** مواضع مستوىً، مرتَّبة بالرمز ترتيباً حرفياً ثابتاً. **وغلافٌ لا مصفوفة عارية.** / The places of one level, ordered by code in a stable ordinal order. **An envelope, not a bare array.** */
export interface StoragePlaceList {
  /** عدد المواضع. / The number of places. */
  placeCount: number;
  /** المواضع. / The places. */
  places: StoragePlace[];
}

/** طلب تسجيل موضعٍ في هرم التسكين — مستودعاً أو موقعاً أو رفّاً. **ولا مستوى فيه ولا رمز أب**: المستوى يقرأه المسار الذي وصل الطلب إليه، والأب معرّفٌ في المسار. وحقلٌ للأب في الجسم كان سيقبل رمزاً يخالف المسار، فيصير للمولود أبوان مُعلَنان. / A request to register a place in the placement hierarchy — a warehouse, a location, or a bin. **It carries neither a level nor a parent code**: the level is read from the path the request arrived on, and the parent is an identifier in that path. A parent field in the body would accept a code contradicting the path, giving the child two declared parents. */
export interface StoragePlaceRequest {
  /** رمز الموضع داخل مستواه — **هوية تحملها الحركات والأرصدة، لا نصّاً معروضاً**. لا يُترجَم ولا يُطابَق بلا حساسية حالة، ولا يتغيّر بعد التسجيل. / The place code within its level — **an identity carried by movements and balances, not displayed text**. Never translated, never matched case-insensitively, and never changed after registration. */
  code: string;
  name: LocalizedText;
}

/** عقد باطن ومعه بنوده المعلَّقة. / A subcontract together with its pending items. */
export interface Subcontract {
  /** العملة. / The currency. */
  currencyCode: string;
  /** فترة الضمان بالأشهر. / The guarantee period in months. */
  guaranteeMonths: number;
  /** المعرّف. / The identifier. */
  id: string;
  /** الرقم. / The number. */
  number: string;
  /** البنود المعلَّقة التي تمنع ترحيل مستخلصاته. / The pending items that block posting its certificates. */
  pendingPolicy: PendingPolicyItem[];
  /** رمز المشروع. / The project code. */
  projectCode: string;
  /** المشروع. / The project. */
  projectId: string;
  retentionRate: Rate;
  /** تاريخ التوقيع ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The signature date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  signedOn: string;
  /** المقاول. / The subcontractor. */
  subcontractorId: string;
}

/** بند عقد باطن بمعرّفه — مدخل سطر مستخلصه. / A subcontract line with its identifier — the input to its certificate line. */
export interface SubcontractLine {
  /** الرمز. / The code. */
  code: string;
  contractQuantity: Measure;
  /** البيان. / The description. */
  descriptionAr: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** الترتيب. / The ordinal. */
  lineNo: number;
  unitRate: Money;
}

/** بنود عقد الباطن. / A subcontract's lines. */
export interface SubcontractLineList {
  /** عددها. / Their count. */
  lineCount: number;
  /** البنود بترتيبها. / The lines in their order. */
  lines: SubcontractLine[];
}

/** بند عقد باطن في طلب. / A subcontract line in a request. */
export interface SubcontractLineRequest {
  /** رمز البند داخل عقد الباطن. / The line's code within the subcontract. */
  code: string;
  contractQuantity: Measure;
  /** البيان بالعربية. / The Arabic description. */
  descriptionAr: string;
  unitRate: Money;
}

/** طلب إنشاء عقد باطن. / A request to create a subcontract. */
export interface SubcontractRequest {
  /** فترة الضمان بالأشهر. / The guarantee period in months. */
  guaranteeMonths: number;
  /** بنود عقد الباطن. / The subcontract's lines. */
  lines: SubcontractLineRequest[];
  /** رقم العقد. / The subcontract number. */
  number: string;
  /** المشروع. / The project. */
  projectId: string;
  retentionRate: Rate;
  /** تاريخ التوقيع ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The signature date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  signedOn: string;
  /** المقاول. / The subcontractor. */
  subcontractorId: string;
}

/** مقاول من الباطن — **ومعرّفه هو الطرف في دفتر subcontractor المساعد**. / A subcontractor — **its identifier is the party in the subcontractor subledger**. */
export interface Subcontractor {
  /** الرمز. / The code. */
  code: string;
  /** المعرّف — وهو الطرف في الدفتر المساعد. / The identifier — the party in the subledger. */
  id: string;
  /** هل هو عامل؟ / Is it active? */
  isActive: boolean;
  /** الاسم العربي. / The Arabic name. */
  nameAr: string;
  /** الترجمات. / The translations. */
  nameTranslations: NameValue[];
  /** رقم التسجيل الضريبي. / The VAT registration number. */
  vatNumber: string;
}

/** طلب صرف دفعة مقدمة لمقاول من الباطن. **ومبلغه واقعةٌ يُدخلها المستخدم** — ما صُرف — لا رقمٌ يشتقّه حاسبٌ من نسبةٍ ووعاءٍ وقاعدةِ تقريب، ولذلك يُرحَّل هذا المستند اليوم بينما يُرفض المستخلص. / A request to pay a subcontractor advance. **Its amount is a fact the user enters** — what was paid — not a figure a calculator derives from a rate, a base, and a rounding rule, which is why this document posts today while the certificate is refused. */
export interface SubcontractorAdvanceRequest {
  amount: Money;
  /** خطاب ضمان الدفعة المقدمة الذي يشترطه نصّ إطلاق الحدث، أو null. / The advance-payment guarantee the event's trigger text requires, or null. */
  guaranteeId: string | null;
  /** رقم المستند. / The document number. */
  number: string;
  /** تاريخ الصرف ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The payment date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  paidOn: string;
  /** طريقة التسوية — مؤهّل دور لا حساب. والمصفوفة وحدها تُحوّلها إلى حساب. / The settlement method — a role qualifier, not an account. The matrix alone turns it into an account. */
  settlementMethod: string;
  /** عقد الباطن. / The subcontract. */
  subcontractId: string;
  /** معرّف الخزينة أو الحساب البنكي في دفترها المساعد — معرّف مبهم لا رقم حساب. / The treasury or bank account identifier in its subledger — an opaque identifier, not an account number. */
  treasuryPartyId: string;
}

/** طلب تسجيل مقاول من الباطن. / A request to register a subcontractor. */
export interface SubcontractorRequest {
  /** رمز المقاول داخل المنشأة. / The subcontractor's code within the company. */
  code: string;
  /** الاسم بالعربية — السجلّ. / The Arabic name — the record. */
  nameAr: string;
  /** ترجمات الاسم بأوسمة BCP-47. / The name's translations, keyed by BCP-47 tags. */
  nameTranslations: NameValue[];
  /** رقم التسجيل الضريبي، أو نصّ فارغ لمن لا رقم له — والغياب واقعٌ لا نقص. / The VAT registration number, or an empty string for a party without one — the absence is a fact, not a gap. */
  vatNumber: string;
}

/** كشف المقاولين ومطابقته بنقطة ضبطه — «كشف المقاولين = رصيد الحساب». و**isReconciled صفرٌ بالضبط لا «قريب من الصفر»**: الفارق بريال واحد فارقٌ يُسمّى. / The subcontractor statement and its reconciliation against its control point — 'the subcontractor statement equals the account balance'. **isReconciled means exactly zero, not 'close to zero'**: a one-riyal difference is a difference that gets named. */
export interface SubcontractorStatement {
  /** تاريخ الكشف ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The statement date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  asOf: string;
  controlTotal: Money;
  divergence: Money;
  /** هل الفارق صفر بالضبط؟ / Is the divergence exactly zero? */
  isReconciled: boolean;
  /** الأطراف مرتَّبة برمزها. / The parties ordered by code. */
  rows: SubcontractorStatementRow[];
  subledgerTotal: Money;
}

/** طرفٌ في كشف المقاولين وأثره المُرحَّل. / A party in the subcontractor statement and its posted effect. */
export interface SubcontractorStatementRow {
  /** رمز المقاول. / The subcontractor's code. */
  code: string;
  effect: Money;
  /** الاسم العربي. / The Arabic name. */
  nameAr: string;
  /** الترجمات. / The translations. */
  nameTranslations: NameValue[];
  /** المقاول. / The subcontractor. */
  subcontractorId: string;
}

export interface Subledger {
  /** نوع الدفتر المساعد. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The subledger kind. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  kind: "None" | "Customer" | "Supplier" | "Employee" | "Asset" | "Treasury";
  /** معرّف الطرف داخل الوحدة المالكة له. / The party identifier within its owning module. */
  partyId: string;
}

/**
 * الاشتراك الجاري كاملاً: الخطّة وسعرها نصّاً، والحالة، وحالة كل وحدة، وتاريخ التجديد. **وكل مبلغ نصّ** بأربع خانات — لا رمز رقمي في JSON.
 * 
 * **والأسماء عربيةٌ وحدها هنا، ولا ترجمات معها بعد.** والسبب مُعلَن: سجلّ الأسطول يحمل عمودين منذ الموجة الأولى ولا يحمل **جدول ترجمات**، وصفٌّ واحد يُصطنع من عمودٍ لاتيني ليس ترجمةً صفّاً بل النصفَ الإنجليزي الثابت في ثوب قائمة — وهو ما يمنعه ADR-0021 بند 2. فحين يصير لمستوى التحكّم جدولُ ترجماته تُضاف nameTranslations إضافةً تبقى v1. / The whole current subscription: the plan and its price as text, the state, each module's state, and the renewal date. **Every amount is a string** with four decimals — never a JSON number token.
 */
export interface Subscription {
  /** رمز عملة ISO 4217 بثلاثة محارف لاتينية كبيرة. واللاتينية هنا شرط سلامة سلسلة التجزئة لا تفضيل عرض. / An ISO 4217 currency code, three upper-case ASCII letters. ASCII here is a hash-chain safety requirement, not a display preference. */
  currency: string;
  /** تاريخ انتهاء الاشتراك بصيغة yyyy-MM-dd، أو null لاشتراك جارٍ بلا نهاية معلومة. / The subscription's end date as yyyy-MM-dd, or null for a running subscription with no known end. */
  endsOn: string | null;
  /** عدد المستخدمين المُضمَّنين في السعر الشهري. / The number of users included in the monthly price. */
  includedUsers: number;
  /** الوحدات وحالاتها، مرتَّبةً برمزها ترتيباً حرفياً ثابتاً. / The modules and their states, ordered by code in a fixed ordinal order. */
  modules: SubscriptionModule[];
  monthlyPrice: Money;
  /** اسم المستأجر بالعربية — السجلّ. / The tenant's Arabic name — the record. */
  nameAr: string;
  perUserPrice: Money;
  /** رمز الخطّة. / The plan code. */
  planCode: string;
  /** اسم الخطّة بالعربية. / The plan's Arabic name. */
  planNameAr: string;
  /** تاريخ التجديد التالي بصيغة yyyy-MM-dd، أو null لاشتراك ليس فعّالاً — وتاريخٌ يُعرض على اشتراك منقطع يُقرأ وعداً بعودةٍ لا تقع. / The next renewal date as yyyy-MM-dd, or null when the subscription is not active — a date shown on a lapsed subscription reads as a promise of a return that does not happen. */
  renewsOn: string | null;
  /** تاريخ بدء الاشتراك الجاري. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The current subscription's start date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  startedOn: string;
  /** حالة الاشتراك. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The subscription's state. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "Active" | "Lapsed" | "Cancelled";
  /** معرّف الاشتراك الجاري. / The current subscription's identifier. */
  subscriptionId: string;
  /** رمز المستأجر القصير. / The tenant's short code. */
  tenantCode: string;
  /** المستأجر. / The tenant. */
  tenantId: string;
  /** حالة المستأجر في سجل الأسطول. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The tenant's status in the fleet registry. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  tenantStatus: "Provisioning" | "Active" | "Suspended" | "Archived";
}

/** وحدةٌ في الاشتراك وحالتها. وpostsJournal يقول إن عملها يبلغ الدفتر، وهو ما يجعل أرضيتها قراءةً لا نزعاً عند الانقطاع. / A module in the subscription and its state. postsJournal says its work reaches the ledger, which is what makes its floor read-only rather than removal when the subscription lapses. */
export interface SubscriptionModule {
  /** رمز الوحدة في كتالوج مستوى التحكّم. / The module code in the control-plane catalogue. */
  code: string;
  /** اسمها بالعربية — السجلّ. / Its Arabic name — the record. */
  nameAr: string;
  /** هل يبلغ عملُها الدفتر؟ ووحدةٌ تُرحّل قيوداً لا تُنتزَع بسبب سداد. / Does its work reach the ledger? A module that posts entries is not taken away over payment. */
  postsJournal: boolean;
  /** حالة الوحدة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The module's state. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "NotEntitled" | "ReadOnly" | "Entitled";
}

/** طلب انقطاع أو استئناف — بالسند نفسه وللسبب نفسه. / A lapse or resumption request — with the same authority and for the same reason. */
export interface SubscriptionTransitionRequest {
  /** السند: رقم عقد، أو حدث سداد، أو تذكرة دعم، أو قرار مُوثَّق. **ولا تغيير استحقاق بلا سند**: الاستحقاق يحكم أي بيانات مالية يجوز إنشاؤها، فتغييره حدث تدقيقي. / The authority: a contract number, a payment event, a support ticket, or a documented decision. **No entitlement change without authority**: entitlement governs which financial data may be created, so changing it is an audit event. */
  authority: string;
  /** السبب بالعربية — يُكتب في سجلّ تدقيق الاستحقاق. / The reason in Arabic — written to the entitlement audit log. */
  reasonAr: string;
}

/** طلب تسجيل سند صرف مسوّدة. و**bankFee ليست ذمّة مورد**: يخرج من الخزينة paid + bankFee وينقص من ذمّة المورد paid وحده، ومجموع التخصيصات يُقاس على paid لا على مجموعهما. / A request to draft a supplier payment. **bankFee is not a supplier balance**: paid + bankFee leaves the treasury while only paid comes off the supplier's balance, and the sum of allocations is measured against paid rather than against their total. */
export interface SupplierPaymentRequest {
  /** التخصيصات على فواتير هذا المورد المُرحَّلة. ومجموعها لا يتجاوز paid. / Allocations against this supplier's posted bills. Their sum may not exceed paid. */
  allocations: PaymentAllocation[];
  bankFee: Money;
  /** رقم السند — فريد داخل المستأجر. / The payment number — unique within the tenant. */
  number: string;
  paid: Money;
  /** تاريخ الصرف. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The payment date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  paidOn: string;
  /** طريقة التسوية — مؤهّل دور. المستعمَل اليوم: cash · bank · card_clearing. / The settlement method — a role qualifier. In use today: cash, bank, card_clearing. */
  settlementMethod: string;
  /** المورد المدفوع له. / The supplier being paid. */
  supplierId: string;
  /** الخزينة أو الحساب البنكي في دفترها المساعد — طرفٌ لا رقم حساب. / The cash box or bank account in its subledger — a party, not an account number. */
  treasuryPartyId: string;
}

/** طلب تسجيل مورد — كطلب العميل ومعه رقم التسجيل الضريبي اختياراً. / A supplier registration request — the customer request plus an optional VAT registration number. */
export interface SupplierRequest {
  /** رمز المورد داخل المستأجر. / The supplier code within the tenant. */
  code: string;
  creditLimit: Money;
  name: LocalizedText;
  /** مهلة السداد بالأيام. / The payment terms in days. */
  paymentTermsDays: number;
  /** رقم التسجيل الضريبي، أو غيابه فالمورد غير مسجَّل — وغيابه واقع لا نقص. وحين يُرسل يُتحقّق من شكله كاملاً. / The VAT registration number, or omit it for an unregistered supplier — its absence is a fact, not a gap. When sent, its full shape is verified. */
  vatNumber?: string;
}

export interface SuspendCostCenterRequest {
  /** السبب المكتوب للإيقاف — ثمانية محارف على الأقل. «لا سبب» ليس سبباً، والإيقاف حالة عملٍ يضبطها إنسان ويُسجَّل بمن فعلها. / The written reason for the suspension — at least eight characters. 'No reason' is not a reason; suspension is a business state a person sets and it is recorded with its actor. */
  reason: string;
}

/** نسبة الضريبة **كسراً عشرياً لا نسبة مئوية**: خمسة عشر بالمئة تُكتب 0.15 لا 15. والمقياس ثمانٍ لا أربع: النسبة ليست مبلغاً ولا تُقرَّب إلى الهللة. / The tax rate as a **decimal fraction, not a percentage**: fifteen percent is written 0.15, never 15. The scale is eight, not four: a rate is not an amount and is not rounded to the halala. */
/* TaxRate مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** أعمار متأخرات المستأجرين **ومطابقتها بنقطة ضبطها** في الجواب نفسه: isReconciled صحيح حين يكون الفارق **صفراً بالضبط** لا «قريباً من الصفر». / Tenant arrears ageing **together with its reconciliation against its control point** in the same response: isReconciled is true when the divergence is **exactly zero**, never 'close to zero'. */
export interface TenantArrears {
  /** تاريخ التقرير. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The report date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  asOf: string;
  controlTotal: Money;
  divergence: Money;
  /** هل الفارق صفر بالضبط؟ / Is the divergence exactly zero? */
  isReconciled: boolean;
  /** المستأجرون الذين عليهم متأخرات. / The tenants carrying arrears. */
  parties: ArrearsParty[];
  totals: ArrearsBands;
}

/** سند قبض بحالته وحدثه. و**قيدان لا قيد واحد** حين يُخصَّص: قيد التحصيل باقٍ كما وقع، وقيد التخصيص مستقلٌّ عنه — والعكس كان سيمحو واقعةً وقعت. / A tenant receipt with its state and event. **Two entries, not one** once allocated: the collection entry stands as it occurred and the allocation entry is separate — a reversal would have erased a fact that happened. */
export interface TenantReceipt {
  /** قيد التخصيص المستقلّ إن وقع، وإلا null. / The separate allocation entry if it occurred, otherwise null. */
  allocationEntryId: string | null;
  /** هل كانت هذه الهوية مُرحَّلة قبل هذا النداء؟ / Was this identity already posted before this call? */
  alreadyPosted: boolean;
  /** قيد الترحيل إن وقع. / The posting entry if it occurred. */
  entryId: string | null;
  /** الحدث المُرحَّل — اختاره حضور المرجع أو غيابه. / The posted event — chosen by the presence or absence of the reference. */
  eventCode: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** هل خُصِّص؟ والتخصيص يقع مرّة. / Has it been allocated? Allocation happens once. */
  isAllocated: boolean;
  /** الرقم. / The number. */
  number: string;
  received: Money;
  /** حالة السند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The receipt state. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  state: "DRAFT" | "POSTED";
}

/** طلب تسجيل سند قبض من مستأجر. **وغياب lesseeId ليس نقصاً بل واقعة**: مبلغٌ ورد بلا مرجع يُرحَّل إلى حساب التحصيلات غير المخصَّصة ولا يُنسب إلى أحد بالتخمين، ثم يُخصَّص بقيدٍ مستقل حين يُعرف صاحبه. / A tenant receipt request. **A missing lesseeId is a fact, not a gap**: an amount that arrived without a reference posts to the unallocated collections account and is never attributed by guesswork, then is allocated by a separate entry once its owner is known. */
export interface TenantReceiptRequest {
  /** المستأجر، أو null فالمبلغ ورد بلا مرجع. / The lessee, or null when the amount arrived without a reference. */
  lesseeId?: string | null;
  /** رقم السند — فريد داخل المنشأة. / The receipt number — unique within the company. */
  number: string;
  received: Money;
  /** تاريخ القبض الميلادي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The collection date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  receivedOn: string;
  /** طريقة التسوية — مؤهّل دور تقرؤه المصفوفة. المستعمَل اليوم: cash · bank · card_clearing. / The settlement method — a role qualifier the matrix reads. In use today: cash, bank, card_clearing. */
  settlementMethod: string;
  /** الخزينة أو الحساب البنكي في دفتره المساعد — **طرفٌ لا رقم حساب**. / The cash box or bank account in its subledger — **a party, not an account number**. */
  treasuryPartyId: string;
}

/** ميزان المراجعة بمجموعيه. والمجموعان محسوبان بـ sum() على numeric داخل PostgreSQL في الاستعلام نفسه الذي أنتج الصفوف: الجمع هناك مضبوط بلا فاصلة عائمة في أي خطوة. ولا يُجمع العمود في طبقة HTTP (حسابٌ على المال)، ولا في المتصفّح (Number فاصلة عائمة ثنائية). و balanced يصل محسوماً كذلك، وميزانٌ غير متوازن يُرى ولا يُقرَّب. / The trial balance with its totals. Both are computed by sum() over numeric inside PostgreSQL in the same query that produced the rows, where summation is exact with no floating point at any step. The column is never summed in the HTTP layer (that is money arithmetic) nor in the browser (Number is a binary float). The balanced flag arrives decided too, and a trial balance that does not balance is visible, never rounded away. */
export interface TrialBalance {
  /** هل تساوى المجموعان؟ محسوم في الدفتر لا عند العميل. / Do the two totals match? Decided in the ledger, not at the client. */
  balanced: boolean;
  /** الدفتر. / The book. */
  book: string;
  /** رمز الفترة المالية yyyy-MM ميلادياً، أو null حين يشمل الطلب كل الفترات. / The fiscal period code yyyy-MM, or null when the request spans all periods. */
  periodCode: string | null;
  /** عدد الصفوف. / The number of rows. */
  rowCount: number;
  /** الصفوف مرتَّبة برمز الحساب. / The rows ordered by account code. */
  rows: TrialBalanceRow[];
  totalCredit: Money;
  totalDebit: Money;
}

export interface TrialBalanceRow {
  /** رمز الحساب كما هو في دليل حسابات هذه الشركة. / The account code as it stands in this company's chart of accounts. */
  accountCode: string;
  credit: Money;
  debit: Money;
  /** الاسم العربي — وهو السجلّ لا ترجمةً أولى، وغير فارغ أبداً (ADR-0021). / The Arabic name; it is the record rather than a first translation, and is never blank (ADR-0021). */
  nameAr: string;
  /** ترجمات اسم الحساب: الاسم وسم لغة BCP-47 والقيمة النصّ المترجَم، مرتَّبةً بالوسم ترتيباً حرفياً ثابتاً. وقد تكون فارغة — والعرض يرتدّ حينها إلى الاسم العربي، وهو ارتداد **يُعلَن** لا يقع صامتاً. و**الإنجليزية واحدة من هذه الترجمات لا حقلاً مستقلاً**: من أرادها قرأ المدخل ذا الوسم en، وغيابه غيابُ ترجمة إنجليزية لا غيابُ اسم. / The account name's translations: the name is a BCP-47 language tag and the value is the translated text, ordered by tag with a stable ordinal sort. It may be empty, in which case display falls back to the Arabic name — a fallback that is declared, never silent. **English is one of these translations rather than a field of its own**: read the entry tagged 'en', whose absence means there is no English translation, not that there is no name. */
  nameTranslations: NameValue[];
}

/** وحدةٌ داخل عقار، بتصنيفها الذي يقود شرط خضوع الإيجار للضريبة. / A unit within a property, with the classification that drives the letting's taxability condition. */
export interface Unit {
  /** الرمز. / The code. */
  code: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** الاسم العربي. / The Arabic name. */
  nameAr: string;
  /** ترجمات الاسم. / The name's translations. */
  nameTranslations: NameValue[];
  /** العقار المالك — والوحدة لا تقف بلا عقارها على أي سطر قيد. / The owning property — a unit never stands without its property on any journal line. */
  propertyId: string;
  /** الاستعمال. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The use. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  usage: "commercial" | "residential";
  /** المعاملة الضريبية. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The VAT treatment. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  vatTreatment: "exempt" | "standard";
}

/** معامل تحويل بين وحدتين كما يخرج على السلك. / A conversion factor between two units as it leaves on the wire. */
export interface UnitConversion {
  /** المقام. / The denominator. */
  denominator: number;
  /** الوحدة المُحوَّل منها. / The source unit. */
  fromUnit: string;
  /** المعرّف. / The identifier. */
  id: string;
  /** البسط. / The numerator. */
  numerator: number;
  /** صنف الكمّية المشترك بين الوحدتين — ولا معامل بين صنفين. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The quantity class shared by both units — there is no factor across two classes. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  quantityClass: "COUNT" | "WEIGHT" | "VOLUME" | "LENGTH" | "AREA";
  /** الوحدة المُحوَّل إليها. / The destination unit. */
  toUnit: string;
}

/** معاملات التحويل، مرتَّبة بالوحدة المُحوَّل منها ثم إليها. / The conversion factors, ordered by source unit then destination unit. */
export interface UnitConversionList {
  /** عدد المعاملات. / The number of factors. */
  conversionCount: number;
  /** المعاملات. / The factors. */
  conversions: UnitConversion[];
}

/** طلب تسجيل معامل تحويل بين وحدتين **على مستوى المنشأة** — وهو غير ItemRequest.units: ذاك خاصّية تعبئةٍ لصنف، وهذا واقعةٌ فيزيائية تصلح للجميع. **ويُرفض ما بين صنفَي كمّية مختلفين.** / A request to register a conversion factor between two units **at company level** — not the same as ItemRequest.units, which is a packing property of one item, while this is a physical fact true for all. **One across two quantity classes is refused.** */
export interface UnitConversionRequest {
  /** المقام — موجب. / The denominator; positive. */
  denominator: number;
  /** الوحدة المُحوَّل منها — يجب أن تكون مسجَّلة وعاملة. / The source unit; it must be registered and active. */
  fromUnit: string;
  /** البسط: كم وحدةً من toUnit في «المقام» من fromUnit. / The numerator: how many toUnit are in 'denominator' of fromUnit. */
  numerator: number;
  /** الوحدة المُحوَّل إليها — يجب أن تكون مسجَّلة وعاملة ومن صنف الكمّية نفسه. / The destination unit; it must be registered, active, and of the same quantity class. */
  toUnit: string;
}

/** متوسط تكلفة الوحدة نصّاً بمقياس **ستّ خانات لا أربع**: صنفٌ يُشترى بألف حبّة بمئة ريال تكلفة وحدته 0.100000، وبمقياس أربعة تصير 0.1000 والفرق لا يظهر — لكنه يتراكم على كل صرف حتى ينحرف رصيد القيمة عن مجموع حركاته. / The moving average unit cost as a string with **six** decimal places rather than four: an item bought at a thousand pieces for a hundred riyals has a unit cost of 0.100000, which at scale four becomes 0.1000 and the difference disappears — yet it accumulates on every issue until the value balance no longer equals the sum of its movements. */
/* UnitCost مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** معامل تحويل وحدةٍ إلى وحدة أساس الصنف — **بسطٌ ومقام صحيحان، لا عددٌ عشري**. «الكرتون اثنتا عشرة حبّة» هو 12/1، و«الحبّة ثلث علبة» هو 1/3 — والثاني لا يُمثَّل عشرياً بلا خسارة، وخسارةٌ في كمّية تُضرب في تكلفة الوحدة تصل إلى المال. والتحويل الذي لا يقع بلا باقٍ يُرفض بـinventory.unit_conversion_not_exact ولا يُقرَّب. / A factor converting a unit into the item's base unit — **an integer numerator and denominator, not a decimal**. 'A carton is twelve pieces' is 12/1; 'a piece is a third of a box' is 1/3 — and the second cannot be represented decimally without loss, while loss in a quantity that gets multiplied by a unit cost reaches the money. A conversion that does not divide exactly is refused with inventory.unit_conversion_not_exact rather than rounded. */
export interface UnitFactor {
  /** المقام — موجب. / The denominator; positive. */
  denominator: number;
  /** البسط: كم وحدةَ أساسٍ في «المقام» من هذه الوحدة. / The numerator: how many base units are in 'denominator' of this unit. */
  numerator: number;
  /** رمز الوحدة الأكبر. / The larger unit's code. */
  unitCode: string;
}

/** وحدة قياس كما تخرج على السلك. / A unit of measure as it leaves on the wire. */
export interface UnitOfMeasure {
  /** الرمز. / The code. */
  code: string;
  /** المعرّف الذي تُبنى عليه القراءة. / The identifier reads are built on. */
  id: string;
  /** هل هي عاملة؟ **والتعطيل حالةٌ تُقرأ لا غياب**: المُعطَّلة تبقى في القوائم بهذا الحقل false، لأن رمزها محمولٌ على حركات مضت. / Is it active? **Deactivation is a readable state, not an absence**: a deactivated unit stays in the lists with this field false, because its code is carried by past movements. */
  isActive: boolean;
  name: LocalizedText;
  /** صنف الكمّية. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The quantity class. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  quantityClass: "COUNT" | "WEIGHT" | "VOLUME" | "LENGTH" | "AREA";
}

/** وحدات قياس المنشأة، مرتَّبة بالرمز ترتيباً حرفياً ثابتاً. **وغلافٌ لا مصفوفة عارية.** / The company's units of measure, ordered by code in a stable ordinal order. **An envelope, not a bare array.** */
export interface UnitOfMeasureList {
  /** عدد الوحدات. / The number of units. */
  unitCount: number;
  /** الوحدات. / The units. */
  units: UnitOfMeasure[];
}

/** طلب تسجيل وحدة قياس. **وصنف الكمّية إلزاميّ**: هو الحقل الوحيد الذي يجعل «كجم ← م» خطأً يُرفض بدل أن يكون معاملاً يكتبه أحدهم بحسن نيّة. / A request to register a unit of measure. **The quantity class is required**: it is the only field that makes 'kg to m' a refused error rather than a factor somebody writes in good faith. */
export interface UnitOfMeasureRequest {
  /** رمز الوحدة — **هوية تحملها كل حركة، لا نصّاً معروضاً**. لا يُترجَم ولا يُطابَق بلا حساسية حالة. / The unit code — **an identity carried by every movement, not displayed text**. Never translated and never matched case-insensitively. */
  code: string;
  name: LocalizedText;
  /** صنف الكمّية. **والقائمة مغلقة**: صنفٌ سادس يدخل بهجرة لا بقيمةٍ حرّة. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The quantity class. **The list is closed**: a sixth class arrives by migration, not by a free value. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  quantityClass: "COUNT" | "WEIGHT" | "VOLUME" | "LENGTH" | "AREA";
}

/** طلب تسجيل وحدة داخل عقار. **وusage وvatTreatment حقلان صريحان لا يُشتقّ أحدهما من الآخر ولا من نوع العقار**: العقار المختلط يولّد توريداً خاضعاً ومعفى معاً. / A unit registration request within a property. **usage and vatTreatment are explicit fields, neither derived from the other nor from the property type**: a mixed-use property produces taxable and exempt supplies at once. */
export interface UnitRequest {
  /** رمز الوحدة — وهو ما يظهر بُعداً على سطر القيد مع بُعد عقاره. / The unit code — what appears as a dimension on the journal line alongside its property dimension. */
  code: string;
  /** اسم الوحدة بالعربية — السجلّ. / The unit's Arabic name — the record. */
  nameAr: string;
  /** ترجمات الاسم. / The name's translations. */
  nameTranslations?: NameValue[];
  /** استعمال الوحدة، يُدخَل ويُراجَع ولا يُشتقّ. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The unit's use, entered and reviewed, never derived. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  usage: "commercial" | "residential";
  /** المعاملة الضريبية للوحدة، تُدخَل ولا تُشتقّ. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The unit's VAT treatment, entered and never derived. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  vatTreatment: "exempt" | "standard";
}

/** طلب سحب مرفق. والسبب مفتاحٌ من مجموعة يملكها المستدعي لا نصّ حرّ: نصٌّ حرّ يُكتب بلغة كاتبه ثم يُقرأ في تقرير بلغة أخرى، ولا يُرشَّح عليه ولا يُترجَم. / A request to withdraw an attachment. The reason is a key from a set the caller owns, not free text: free text is written in its author's language and read in a report in another, is never filtered on, and is never translated. */
export interface WithdrawAttachmentRequest {
  /** مفتاح السبب: أحرف لاتينية صغيرة وأرقام ونقطة وشرطة سفلية. / The reason key: lower-case Latin letters, digits, dots, and underscores. */
  reasonKey: string;
}
