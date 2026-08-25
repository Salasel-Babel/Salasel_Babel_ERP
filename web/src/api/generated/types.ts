/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     441b7394f6f083778c30895d44cae3b74a36bda25aadc43e7ce4a9d28fb44ecd
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   أنواع العقد — مخطّطاً واحداً لكل مخطّط في components.schemas.
   ═══════════════════════════════════════════════════════════════════════ */

import type { Money } from "../money";
import type { ExchangeRate, Int64String } from "./brands";

/* المال يصل هنا **مغلّفاً**: Money كائن يرمي عند أي تحويل ضمني إلى نصّ أو رقم.
   وبقيّة الصيغ النصّية المنشورة أنواع محتجزة (ExchangeRate · Int64String).
   ولا حقل مالي واحد نوعه number — لا هنا ولا في أي ملف مكتوب بيد.
   Money is an object whose implicit coercions throw; the other published string
   formats are branded types. No monetary field is ever typed `number`. */

/** أسماء حقول المستند لا قيمها: القبول حكمٌ على الشكل، ولا يعبر منه مبلغ. / The document's field names, not its values: admission is a verdict on shape and no amount crosses it. */
export interface AdmitDocumentRequest {
  /** أسماء الحقول الموجودة على المستند. / The names of the fields present on the document. */
  fields: string[];
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
  capability: "advance" | "cost_of_sales" | "retention";
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

export interface DocumentAdmission {
  /** مقبول. والرفض يخرج مشكلةً بالرمز 422 لا حكماً في هذا الحقل. / Admitted. A refusal leaves as a 422 problem, never as a verdict in this field. */
  admitted: boolean;
  /** رمز نوع المستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The document type code. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  documentType: "projects.client_certificate" | "sales.invoice";
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
  documentType: "projects.client_certificate" | "sales.invoice";
}

/** شكل مستند **مُشتقّاً** من (هذه الوثيقة × الملفّ). ولاحظ ما ليس فيه: لا تخطيط، ولا ترتيب بصري، ولا شرط، ولا تعبير — تلك أبواب «المنصّة داخل المنصّة» التي رُفضت. / A document's shape **derived** from (this document x the profile). Note what is absent: no layout, no visual order, no condition, no expression — those are the inner-platform doors that were rejected. */
export interface DocumentShape {
  /** كل قدرات هذا النوع في الكتالوج المغلق. / Every capability of this type in the closed catalogue. */
  availableCapabilities: ("advance" | "cost_of_sales" | "retention")[];
  /** القيم الافتراضية. / The defaults. */
  defaults: NameValue[];
  /** رمز نوع المستند. يُطابَق حرفياً وبحساسية حالة الأحرف؛ ولا يُقبل رقم مكان الاسم. / The document type code. Matched literally and case-sensitively; a number is never accepted in place of a name. */
  documentType: "projects.client_certificate" | "sales.invoice";
  /** المُشغَّل منها لهذه الشركة. / Those enabled for this company. */
  enabledCapabilities: ("advance" | "cost_of_sales" | "retention")[];
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

export interface PutCapabilityProfileRequest {
  /** أنواع المستندات. / The document types. */
  documents: DocumentProfile[];
  /** سبب سحب قدرة. إلزامي متى أطفأ الطلب قدرةً كانت مُشغَّلة، ومهمَل فيما عدا ذلك؛ وثمانية محارف على الأقل — «لا سبب» ليس سبباً. / The reason for withdrawing a capability. Required whenever the request disables a previously enabled capability, ignored otherwise; at least eight characters — 'no reason' is not a reason. */
  withdrawalReason?: string | null;
}

export interface ReverseJournalEntryRequest {
  closedPeriodAuthorisation?: ClosedPeriodAuthorisation;
  reason: LocalizedText;
  /** تاريخ قيد العكس، أو غيابه فيُتخذ تاريخ القيد الأصلي. ميلادي بصيغة yyyy-MM-dd حصراً وبأرقام لاتينية؛ أي تقويم آخر يُقرأ فترة مالية مختلفة. / The reversing entry's date; omit to take the original entry's date. Gregorian, yyyy-MM-dd only, Latin digits; any other calendar reads as a different fiscal period. */
  reversalDate?: string;
}

export interface Scope {
  /** الفرع. / The branch. */
  branchId?: string | null;
  /** مركز التكلفة. / The cost centre. */
  costCenterId?: string | null;
  /** المشروع. / The project. */
  projectId?: string | null;
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

export interface SuspendCostCenterRequest {
  /** السبب المكتوب للإيقاف — ثمانية محارف على الأقل. «لا سبب» ليس سبباً، والإيقاف حالة عملٍ يضبطها إنسان ويُسجَّل بمن فعلها. / The written reason for the suspension — at least eight characters. 'No reason' is not a reason; suspension is a business state a person sets and it is recorded with its actor. */
  reason: string;
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
  /** الاسم العربي. / The Arabic name. */
  nameAr: string;
  /** الاسم الإنجليزي. / The English name. */
  nameEn: string;
}
