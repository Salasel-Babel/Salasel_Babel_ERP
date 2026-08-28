/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     8aae61c612441715b5f507e25d0685fb307582f7e5c4f4b837bd94672b1ee09e
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   أنواع العقد — مخطّطاً واحداً لكل مخطّط في components.schemas.
   ═══════════════════════════════════════════════════════════════════════ */

import type { Money } from "../money";
import type { ExchangeRate, Int64String, Quantity, TaxRate } from "./brands";

/* المال يصل هنا **مغلّفاً**: Money كائن يرمي عند أي تحويل ضمني إلى نصّ أو رقم.
   وبقيّة الصيغ النصّية المنشورة أنواع محتجزة (ExchangeRate · Int64String · Quantity · TaxRate).
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

/** عدد صحيح 64 بت نصّاً: Number في JavaScript يفقد الدقّة فوق 2^53، ورقم القيد معرّف لا كمّية. / A 64-bit integer as a string: JavaScript Number loses precision above 2^53, and an entry number is an identifier, not a quantity. */
/* Int64String مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

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

/** نصّ ثنائي اللغة. الطرفان إلزاميان — العربية أساسية لا ترجمة ثانية. / Bilingual text; both sides are mandatory. */
export interface LocalizedText {
  /** النصّ العربي. / The Arabic text. */
  ar: string;
  /** النصّ الإنجليزي. / The English text. */
  en: string;
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

export interface PutCapabilityProfileRequest {
  /** أنواع المستندات. / The document types. */
  documents: DocumentProfile[];
  /** سبب سحب قدرة. إلزامي متى أطفأ الطلب قدرةً كانت مُشغَّلة، ومهمَل فيما عدا ذلك؛ وثمانية محارف على الأقل — «لا سبب» ليس سبباً. / The reason for withdrawing a capability. Required whenever the request disables a previously enabled capability, ignored otherwise; at least eight characters — 'no reason' is not a reason. */
  withdrawalReason?: string | null;
}

/** كمّية نصّاً بمقياس لا يتجاوز أربعاً، بالنحو الذي تخضع له المبالغ. وهي ليست مبلغاً — ولذلك لها مخطّطها — لكنها تُضرب في مبلغ، فأي فقدان دقّة فيها يصل إلى المال. / A quantity as a string with at most four decimal places, under the grammar that governs amounts. It is not an amount — hence its own schema — but it is multiplied by one, so any precision lost in it reaches the money. */
/* Quantity مُعرَّف في ../money كنوع محتجز وقت التشغيل. */

/** تخصيص مبلغ من سند قبض على فاتورة مبيعات **مُرحَّلة**. ولا عميل فيه: عميلُه عميل السند، والفاتورة تُفحص أنها له — وإعادة ذكره كانت ستفتح باباً لتخصيصٍ على فاتورة عميل آخر. / An allocation of part of a receipt against a **posted** sales invoice. It carries no customer: the customer is the receipt's, and the invoice is checked to be theirs — repeating it would open a door to allocating against another customer's invoice. */
export interface ReceiptAllocation {
  amount: Money;
  /** الفاتورة المُرحَّلة التي يُنزَل عليها المبلغ. / The posted invoice the amount is applied to. */
  invoiceId: string;
}

/** طلب تجديد جلسة. واعتماد التجديد يُستهلك بهذا النداء ولا يُقبل ثانيةً — وتقديمه مرّتين يُسقط العائلة كلّها. / A request to renew a session. The refresh credential is consumed by this call and never accepted again — presenting it twice drops the whole family. */
export interface RenewSessionRequest {
  /** اعتماد التجديد الجاري. نصٌّ مبهم لا بنية فيه: لا يُحلَّل ولا يُشتقّ منه شيء، ويُقدَّم كما وصل. / The current refresh credential. An opaque string with no structure: never parsed, nothing derived from it, presented exactly as received. */
  refreshCredential: string;
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

export interface Subledger {
  /** نوع الدفتر المساعد. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The subledger kind. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  kind: "None" | "Customer" | "Supplier" | "Employee" | "Asset" | "Treasury";
  /** معرّف الطرف داخل الوحدة المالكة له. / The party identifier within its owning module. */
  partyId: string;
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
