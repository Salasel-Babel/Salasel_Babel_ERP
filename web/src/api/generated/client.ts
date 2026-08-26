/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     90d076ad3cc6c558ce905171467482b90038f42e9a77b8c4fe5a9aa8eaa99366
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   العميل: دالّة واحدة لكل عملية في العقد. لا مسار مكتوب بيد، ولا اسم حقل
   مكتوب بيد، ولا رمز حالة مكتوب بيد.
   ═══════════════════════════════════════════════════════════════════════ */

import type * as T from "./types";
import { SCHEMAS } from "./runtime-schema";
import { decodeSchema, encodeSchema, type Transport, ProblemError } from "../transport";

export type { Transport } from "../transport";

export interface AddCostCenterArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.CostCenterNameRequest;
}

/**
 * إضافة مركز تكلفة / Add a cost centre
 * 
 * يضيف مركز تكلفة عاملاً ويُعيد التأسيس كاملاً. الرمز يُسكّه الخادم ولا يُرسله العميل: الرمز هوية تحملها سطور القيود، والاسم عرضٌ يتغيّر.
 * 
 * Adds an active cost centre and returns the whole setup. The server mints the code; the client never sends one: the code is the identity that journal lines carry, and the name is display that changes.
 */
export async function addCostCenter(transport: Transport, args: AddCostCenterArgs, signal?: AbortSignal): Promise<T.CompanySetup> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/cost-centers";
  const url = path;
  const body = encodeSchema(SCHEMAS, "CostCenterNameRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CompanySetup", response.json) as T.CompanySetup;
}

export interface AdmitDocumentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** رمز نوع المستند من المجموعة المغلقة. / The document type code from the closed set. */
  documentType: "projects.client_certificate" | "sales.invoice";
  /** جسم الطلب. / The request body. */
  body: T.AdmitDocumentRequest;
}

/**
 * عرض مستند على الملفّ / Present a document against the profile
 * 
 * يعرض **أسماء حقول** مستند على ملفّ الشركة فيقبله أو يرفضه. لا قيم ولا مبالغ ولا أثر: هذا حكمٌ لا كتابة. وحقلٌ ترخّصه قدرة مُطفأة يُرفض به المستند كلّه — لأن قدرةً يمكن ممارستها بإرسال الحقل رغم إطفائها ليست قدرة بل زينة.
 * 
 * Presents a document's **field names** against the company profile and admits or refuses it. No values, no amounts, no effect: this is a verdict, not a write. A field licensed by a disabled capability fails the whole document — a capability that can still be exercised by sending the field anyway is decoration, not a capability.
 */
export async function admitDocument(transport: Transport, args: AdmitDocumentArgs, signal?: AbortSignal): Promise<T.DocumentAdmission> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/document-shapes/" + encodeURIComponent(args.documentType) + "/admissions";
  const url = path;
  const body = encodeSchema(SCHEMAS, "AdmitDocumentRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "DocumentAdmission", response.json) as T.DocumentAdmission;
}

/**
 * حالة الخدمة وثقافتها / Service health and culture
 * 
 * تُرجع حالة الخدمة، وثقافة العملية وتقويمها الافتراضي. خارج المصادقة وخارج نطاق الشركة.
 * 
 * Returns service status plus the process culture and its default calendar. Unauthenticated and outside company scope.
 */
export async function health(transport: Transport, signal?: AbortSignal): Promise<T.HealthResponse> {
  const path = "/health";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HealthResponse", response.json) as T.HealthResponse;
}

export interface InitialiseCompanySetupArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.InitialiseCompanySetupRequest;
}

/**
 * تأسيس المنشأة مرّة واحدة / Set the company up, once
 * 
 * يؤسّس المنشأة. **يُقبل مرّة واحدة فقط**: الوصول الثاني يُرفض بـ409 وcompany_setup.already_initialised مهما تغيّرت حمولته — وبالأخصّ decimalPlaces، فعدد الخانات يُسنَد عند أول تأسيس ولا يُعدَّل بعده، وتوحيده داخل دفاتر المنشأة الواحدة أهمّ من أي قيمة بعينها.
 * 
 * وسؤال مراكز التكلفة يُطرح هنا وحده: costCenters = One يجعل **اسم المنشأة نفسه** هو المركز الافتراضي فلا يرى صاحبُ هذا الجواب المفهوم أبداً؛ وcostCenters = Multiple يجعل firstCostCenterNameAr **إلزامياً** — من أعلن أن لديه أكثر من واحد لا يُخترَع له اسم نيابةً عنه. وفي الحالتين تخرج المنشأة من هنا وبها مركز تكلفة واحد على الأقل.
 * 
 * وdecimalPlaces يحكم **العرض والإدخال البشري وحدهما**: التخزين يبقى بأربع خانات، والمبالغ المحسوبة (ضريبة 15٪ على صافٍ فردي مثلاً) لا يقيّدها هذا العدد ولا تُقرَّب عنده — وإلا لصارت الفاتورة العادية مستحيلة.
 * 
 * Sets the company up. **Accepted exactly once**: a second arrival is refused with 409 and company_setup.already_initialised whatever its payload — decimalPlaces above all, since the number of places is assigned at first setup and is never editable afterwards; its consistency inside one entity's books matters more than any particular value.
 * 
 * The cost-centre question is asked here and only here: costCenters = One makes **the company's own name** the default centre, so whoever answers that never sees the concept again; costCenters = Multiple makes firstCostCenterNameAr **mandatory** — no name is invented on behalf of someone who declared they have more than one. Either way the company leaves this call with at least one cost centre.
 * 
 * decimalPlaces governs **display and human input only**: storage stays at four places, and computed amounts (15% VAT on an odd net, say) are neither constrained nor rounded by it — otherwise an ordinary invoice would be impossible.
 */
export async function initialiseCompanySetup(transport: Transport, args: InitialiseCompanySetupArgs, signal?: AbortSignal): Promise<T.CompanySetup> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/setup";
  const url = path;
  const body = encodeSchema(SCHEMAS, "InitialiseCompanySetupRequest", args.body as unknown);
  const response = await transport({ method: "PUT", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CompanySetup", response.json) as T.CompanySetup;
}

export interface PostJournalEntryArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.PostJournalEntryRequest;
}

/**
 * ترحيل قيد / Post a journal entry
 * 
 * يرحّل قيداً عبر محرّك الترحيل. حصين ضد التكرار بمفتاح idempotencyKey: الوصول الثاني بالمفتاح نفسه يُرجع الإيصال ذاته وalreadyPosted = true ورمز 200 بدل 201، ولا يُنشئ قيداً ثانياً — مهما كان ترتيب الوصول.
 * 
 * Posts an entry through the posting engine. Idempotent by idempotencyKey: a second arrival with the same key returns the same receipt with alreadyPosted = true and status 200 instead of 201, and never creates a second entry — whatever the arrival order.
 */
export async function postJournalEntry(transport: Transport, args: PostJournalEntryArgs, signal?: AbortSignal): Promise<T.PostingReceipt> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/journal-entries";
  const url = path;
  const body = encodeSchema(SCHEMAS, "PostJournalEntryRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "PostingReceipt", response.json) as T.PostingReceipt;
}

export interface ReadCapabilityProfileArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * ملفّ القدرات وأشكال مستنداته / The capability profile and its document shapes
 * 
 * يقرأ ملفّ قدرات الشركة، ومعه **شكل كل مستند مُشتقّاً**: الحقول القائمة، والقدرات المتاحة والمُشغَّلة، والقيم الافتراضية. وهذا ما تُبنى عليه الشاشة: الشاشة دالّة في (هذه الوثيقة × الملفّ)، ولا تُؤلَّف بـJSON حرّ عند العميل — شاشةٌ مؤلَّفة باستقلال عن العقد تُرسل حقلاً يرفضه الخادم أو تُسقط حقلاً يطلبه.
 * 
 * Reads the company's capability profile together with **each document's derived shape**: the fields that exist, the available and enabled capabilities, and the defaults. This is what a screen is built from: the screen is a function of (this document x the profile) and is never authored as free-form JSON on the client — a screen authored independently of the contract sends a field the server refuses or omits one it requires.
 */
export async function readCapabilityProfile(transport: Transport, args: ReadCapabilityProfileArgs, signal?: AbortSignal): Promise<T.CapabilityProfile> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/capability-profile";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CapabilityProfile", response.json) as T.CapabilityProfile;
}

export interface ReadCompanySetupArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * تأسيس المنشأة / The company setup
 * 
 * يقرأ تأسيس المنشأة: اسمها، و**عدد الخانات العشرية المعروضة**، و**مراكز تكلفتها كلّها** — العاملة والموقوفة معاً. والموقوف يبقى في القائمة عمداً: تقاريرُ الفترات السابقة تُبوَّب عليه، والدفتر إضافي لا يُحذف منه شيء.
 * 
 * Reads the company setup: its name, the **number of displayed decimal places**, and **all of its cost centres** — active and suspended alike. A suspended centre stays in the list on purpose: earlier periods are still grouped by it, and the ledger is append-only.
 */
export async function readCompanySetup(transport: Transport, args: ReadCompanySetupArgs, signal?: AbortSignal): Promise<T.CompanySetup> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/setup";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CompanySetup", response.json) as T.CompanySetup;
}

export interface ReadDocumentShapeArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** رمز نوع المستند من المجموعة المغلقة. / The document type code from the closed set. */
  documentType: "projects.client_certificate" | "sales.invoice";
}

/**
 * شكل مستند واحد / One document shape
 * 
 * شكل نوع مستند واحد مُشتقّاً من الملفّ. مُشتقّ لا مؤلَّف: لا تخطيط، ولا ترتيب بصري، ولا شرط، ولا تعبير.
 * 
 * One document type's shape derived from the profile. Derived, never authored: no layout, no visual order, no condition, no expression.
 */
export async function readDocumentShape(transport: Transport, args: ReadDocumentShapeArgs, signal?: AbortSignal): Promise<T.DocumentShape> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/document-shapes/" + encodeURIComponent(args.documentType) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "DocumentShape", response.json) as T.DocumentShape;
}

export interface ReadJournalEntryArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف القيد. / The entry identifier. */
  entryId: string;
}

/**
 * قراءة قيد بسطوره / Read one entry with its lines
 * 
 * يقرأ قيداً واحداً بسطوره داخل نطاق الشركة.
 * 
 * Reads a single entry with its lines within the company scope.
 */
export async function readJournalEntry(transport: Transport, args: ReadJournalEntryArgs, signal?: AbortSignal): Promise<T.JournalEntry> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/journal-entries/" + encodeURIComponent(args.entryId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "JournalEntry", response.json) as T.JournalEntry;
}

export interface ReadTrialBalanceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** الدفتر داخل الشركة. / The book within the company. */
  book: string;
  /** رمز الفترة yyyy-MM ميلادياً، أو غيابه فكل الفترات. / Gregorian period code yyyy-MM, or omit for all periods. */
  period?: string;
}

/**
 * ميزان المراجعة / Trial balance
 * 
 * ميزان المراجعة مبنيّاً من سطور القيود غير القابلة للتعديل — لا من جدول الأرصدة. ويحمل مجموعَي المدين والدائن محسوبَين بـ sum() على numeric في الاستعلام نفسه، ومعهما حكم التوازن.
 * 
 * The trial balance built from the immutable journal lines — not from the balance table. It carries the debit and credit totals computed by sum() over numeric in the same query, plus the balanced verdict.
 */
export async function readTrialBalance(transport: Transport, args: ReadTrialBalanceArgs, signal?: AbortSignal): Promise<T.TrialBalance> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/trial-balance";
  const query = new URLSearchParams();
  query.set("book", args.book);
  if (args.period !== undefined && args.period !== null) query.set("period", args.period);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "TrialBalance", response.json) as T.TrialBalance;
}

export interface RenameCostCenterArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** رمز مركز التكلفة كما سكّه الخادم. معرّف لا نصّ معروض: لا يُترجَم ولا يتغيّر بإعادة التسمية. / The cost centre code as the server minted it. An identifier, not displayed text: never translated, and unchanged by renaming. */
  costCenterCode: string;
  /** جسم الطلب. / The request body. */
  body: T.CostCenterNameRequest;
}

/**
 * إعادة تسمية مركز تكلفة / Rename a cost centre
 * 
 * يعيد تسمية مركز تكلفة. **الرمز لا يتغيّر**، فسطور القيود المُرحَّلة عليه تبقى مربوطة به وتُعرض بالاسم الجاري — وهو سلوك الحساب المعطَّل نفسه لا نمطٌ ثانٍ (ADR-0006).
 * 
 * Renames a cost centre. **The code does not change**, so journal lines already posted against it stay tied to it and display under the current name — the same behaviour as a locked account, not a second pattern (ADR-0006).
 */
export async function renameCostCenter(transport: Transport, args: RenameCostCenterArgs, signal?: AbortSignal): Promise<T.CompanySetup> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/cost-centers/" + encodeURIComponent(args.costCenterCode) + "";
  const url = path;
  const body = encodeSchema(SCHEMAS, "CostCenterNameRequest", args.body as unknown);
  const response = await transport({ method: "PUT", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CompanySetup", response.json) as T.CompanySetup;
}

export interface ReverseJournalEntryArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف القيد. / The entry identifier. */
  entryId: string;
  /** جسم الطلب. / The request body. */
  body: T.ReverseJournalEntryRequest;
}

/**
 * عكس قيد / Reverse an entry
 * 
 * ينشئ قيد عكس مرتبطاً بالقيد الأصلي. القيد الأصلي لا يُمسّ ولا يُحذف ولا يُعدَّل — ولا يوجد على هذا السطح فعل حذف أصلاً.
 * 
 * Creates a reversing entry linked to the original. The original is never touched, deleted, or amended — and no delete verb exists on this surface at all.
 */
export async function reverseJournalEntry(transport: Transport, args: ReverseJournalEntryArgs, signal?: AbortSignal): Promise<T.PostingReceipt> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/journal-entries/" + encodeURIComponent(args.entryId) + "/reversal";
  const url = path;
  const body = encodeSchema(SCHEMAS, "ReverseJournalEntryRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "PostingReceipt", response.json) as T.PostingReceipt;
}

export interface SuspendCostCenterArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** رمز مركز التكلفة كما سكّه الخادم. معرّف لا نصّ معروض: لا يُترجَم ولا يتغيّر بإعادة التسمية. / The cost centre code as the server minted it. An identifier, not displayed text: never translated, and unchanged by renaming. */
  costCenterCode: string;
  /** جسم الطلب. / The request body. */
  body: T.SuspendCostCenterRequest;
}

/**
 * إيقاف مركز تكلفة عن الترحيل / Suspend a cost centre from posting
 * 
 * يوقف مركز تكلفة عن الاستعمال على مستند جديد، **بسبب مكتوب** يُسجَّل في سجلّ التدقيق مع من فعله ومتى. ولا يُحذف شيء: المركز يبقى مقروءاً ومُبوَّباً في تقارير الفترات السابقة إلى الأبد.
 * 
 * و**المركز الافتراضي لا يُوقَف**: يُرفض بـ409 وcost_center.default_cannot_be_suspended، لأن المنشأة لا تخلو من مركز تكلفة أبداً. ومن أراد إيقافه ينقل الافتراضي إلى مركز عامل آخر أولاً.
 * 
 * Suspends a cost centre from use on new documents, **with a written reason** recorded in the audit log along with who did it and when. Nothing is deleted: the centre stays readable and stays a grouping key in earlier periods forever.
 * 
 * **The default centre is never suspended**: it is refused with 409 and cost_center.default_cannot_be_suspended, because a company is never without a cost centre. To suspend it, move the default to another active centre first.
 */
export async function suspendCostCenter(transport: Transport, args: SuspendCostCenterArgs, signal?: AbortSignal): Promise<T.CompanySetup> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/cost-centers/" + encodeURIComponent(args.costCenterCode) + "/suspension";
  const url = path;
  const body = encodeSchema(SCHEMAS, "SuspendCostCenterRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CompanySetup", response.json) as T.CompanySetup;
}

export interface VerifyLedgerChainArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** الدفتر داخل الشركة. / The book within the company. */
  book: string;
  /** السنة المالية الميلادية بأربعة أرقام لاتينية. / The Gregorian fiscal year, four Latin digits. */
  fiscalYear: string;
}

/**
 * إعادة التحقق من سلسلة البصمات / Verify the hash chain
 * 
 * يعيد بناء كل مستند من الحقيقة المجالية المخزَّنة ويقارن بصمته، ويسمّي أول تسلسل منحرف إن وُجد.
 * 
 * Rebuilds every document from the stored domain truth, compares its hash, and names the first divergent sequence if any.
 */
export async function verifyLedgerChain(transport: Transport, args: VerifyLedgerChainArgs, signal?: AbortSignal): Promise<T.ChainVerification> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/ledger-chain/verification";
  const query = new URLSearchParams();
  query.set("book", args.book);
  query.set("fiscalYear", args.fiscalYear);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "ChainVerification", response.json) as T.ChainVerification;
}

export interface WriteCapabilityProfileArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.PutCapabilityProfileRequest;
}

/**
 * حفظ ملفّ القدرات / Save the capability profile
 * 
 * يستبدل الملفّ كلّه بعد **مطابقة كل قدرة مُشغَّلة بمصفوفة الترحيل**: قدرةٌ لا يقابلها حدث تُرفض هنا برمز capability_profile.capability_not_served_by_matrix وباسمها وبالأحداث الناقصة — لا تُكتشف بعد شهر دفترَ أستاذ مساعد لا يُطابَق. والاتجاه الخطر هو الإطفاء لا التشغيل: إطفاء قدرة كانت مُشغَّلة يجعل مستنداً مفتوحاً يحملها غير مقبول، ويجعل حدث المتابعة الذي يُخلي رصيد الدفتر المساعد غير قابل للوقوع — فيُرفض بلا withdrawalReason مكتوب، ويُسجَّل السبب في سجل التدقيق حين يُكتب.
 * 
 * Replaces the whole profile after **matching every enabled capability against the posting matrix**: a capability with no event is refused here with capability_profile.capability_not_served_by_matrix, named, with its missing events — not discovered a month later as a subledger that will not tie. The dangerous direction is off, not on: disabling a capability that was enabled makes an open document carrying it inadmissible and makes the follow-on event that relieves the subledger balance unreachable, so it is refused without a written withdrawalReason, and the reason is recorded in the audit log.
 */
export async function writeCapabilityProfile(transport: Transport, args: WriteCapabilityProfileArgs, signal?: AbortSignal): Promise<T.CapabilityProfile> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/capability-profile";
  const url = path;
  const body = encodeSchema(SCHEMAS, "PutCapabilityProfileRequest", args.body as unknown);
  const response = await transport({ method: "PUT", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CapabilityProfile", response.json) as T.CapabilityProfile;
}
