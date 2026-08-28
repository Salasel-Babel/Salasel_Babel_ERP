/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     63c3b477e2e6dbcf9ca20df58b2cb06a6f649c6754d096b4a261c9544948c1f6
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

export interface AddCustomerArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.CustomerRequest;
}

/**
 * تسجيل عميل / Register a customer
 * 
 * يسجّل عميلاً جديداً: رمزه، واسمه ثنائي اللغة، وحدّ ائتمانه، ومهلة سداده. والرمز **هوية** تحملها مستنداته المُرحَّلة، والاسم عرضٌ يتغيّر.
 * 
 * **ولا حقل vatNumber هنا**: رقم التسجيل الضريبي حقل مورد لا حقل عميل على هذا السطح، وإرساله يُرفض به الطلب كلّه — التجاهل الصامت يجعل المُرسِل يظنّ أنه سجّل رقماً لم يصل.
 * 
 * Registers a customer: its code, its bilingual name, its credit limit, and its payment terms. The code is an **identity** its posted documents carry; the name is display that changes.
 * 
 * **There is no vatNumber here**: the VAT registration number is a supplier field, not a customer field on this surface, and sending it fails the whole request — silently ignoring it would make the sender believe a number was recorded that never arrived.
 */
export async function addCustomer(transport: Transport, args: AddCustomerArgs, signal?: AbortSignal): Promise<T.Party> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/customers";
  const url = path;
  const body = encodeSchema(SCHEMAS, "CustomerRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Party", response.json) as T.Party;
}

export interface AddSupplierArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.SupplierRequest;
}

/**
 * تسجيل مورد / Register a supplier
 * 
 * يسجّل مورداً جديداً. وvatNumber **اختياري لأن غيابه واقع لا نقص**: المورد دون حدّ التسجيل، وغير المقيم، والمُنشأ قبل هذا الحقل — ثلاثتهم بلا رقم. وحين يُرسل يُتحقّق من شكله كاملاً ولا يُقبل «تقريباً صحيح».
 * 
 * Registers a supplier. vatNumber is **optional because its absence is a fact, not a gap**: the supplier below the registration threshold, the non-resident supplier, and the supplier created before this field all have none. When it is sent, its full shape is verified and 'nearly right' is not accepted.
 */
export async function addSupplier(transport: Transport, args: AddSupplierArgs, signal?: AbortSignal): Promise<T.Party> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/suppliers";
  const url = path;
  const body = encodeSchema(SCHEMAS, "SupplierRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Party", response.json) as T.Party;
}

export interface AdmitDocumentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** رمز نوع المستند من المجموعة المغلقة. / The document type code from the closed set. */
  documentType: "projects.client_certificate" | "purchasing.supplier_bill" | "sales.invoice";
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

export interface DraftCreditNoteArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.CreditNoteRequest;
}

/**
 * إنشاء إشعار دائن مسوّدة / Draft a credit note
 * 
 * يُنشئ إشعاراً دائناً في حالة **DRAFT** على فاتورة **مُرحَّلة**. وهذا هو الطريق الوحيد إلى تصحيح فاتورة مُرحَّلة: لا تعديل ولا حذف على هذا السطح ولا في هذا النظام (ADR-0002).
 * 
 * والوحدة ترفض الإشعار على فاتورة ليست في حالة POSTED، وترفض ما يتجاوز المتبقّي منها.
 * 
 * وسطرٌ يحمل originalInvoiceLineId هو **ردّ بضاعة** يُقيَّم بتكلفة صرفه الأصلي؛ وسطرٌ بلا هذا الحقل **تخفيض قيمة** لا يُحرّك مخزوناً. والفرق قرار تجاري لا يُخمَّن.
 * 
 * Creates a credit note in state **DRAFT** against a **posted** invoice. This is the only route to correcting a posted invoice: there is no edit and no delete on this surface, and none in this system (ADR-0002).
 * 
 * The module refuses a note against an invoice that is not POSTED, and refuses an amount beyond what is outstanding.
 * 
 * A line carrying originalInvoiceLineId is a **goods return**, valued at the cost of its original issue; a line without that field is a **value reduction** that moves no stock. The difference is a commercial decision, never guessed.
 */
export async function draftCreditNote(transport: Transport, args: DraftCreditNoteArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/credit-notes";
  const url = path;
  const body = encodeSchema(SCHEMAS, "CreditNoteRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface DraftExpenseBillArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.ExpenseBillRequest;
}

/**
 * إنشاء فاتورة مصروف مسوّدة / Draft an expense bill
 * 
 * يُنشئ فاتورة مورد **مصروفية** في حالة DRAFT — بلا مخزون ولا مطابقة ثلاثية. ومركز التكلفة **إلزامي** عليها: المصروف بلا مركز تكلفة رقمٌ لا يُبوَّب.
 * 
 * **ولاحظ ما ليس على هذا المورد: لا فاتورة مخزنية.** الفاتورة المخزنية تُطابَق بثلاثية (أمر شراء · استلام · فاتورة)، والاستلام لا يُرحَّل إلا عبر حدّ تقييم المخزون — أي أن نشرها يجرّ وحدة المخزون كاملةً إلى هذا السطح. وهذا **نقص مُعلَن**: انظر ADR سطح المستندات.
 * 
 * Creates an **expense** supplier bill in state DRAFT — no stock, no three-way match. A cost centre is **mandatory** on it: an expense without a cost centre is a number that cannot be grouped.
 * 
 * **Note what this resource does not carry: no stock bill.** A stock bill is three-way matched (purchase order, goods receipt, bill), and a goods receipt posts only through the inventory valuation port — publishing it drags the whole inventory module onto this surface. This is a **declared gap**: see the document-surface ADR.
 */
export async function draftExpenseBill(transport: Transport, args: DraftExpenseBillArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/supplier-bills";
  const url = path;
  const body = encodeSchema(SCHEMAS, "ExpenseBillRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface DraftSalesInvoiceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.SalesInvoiceRequest;
}

/**
 * إنشاء فاتورة مبيعات مسوّدة / Draft a sales invoice
 * 
 * يُنشئ فاتورة مبيعات في حالة **DRAFT**. ولا قيد ولا أثر في الدفتر: الترحيل مورد فرعي مستقلّ يُنادى بعده. والضريبة تُحسب وتُقرَّب **على السطر**، ومجموع المستند مجموع سطور مقرَّبة — والحساب كلّه في الوحدة لا في هذا السطح.
 * 
 * **ولا رمز حساب في الحمولة ولا رمز حدث**: السطر يحمل itemGroup — مؤهّل دور — ومصفوفة الترحيل وحدها تُحوّله إلى حساب (القاعدة 2).
 * 
 * Creates a sales invoice in state **DRAFT**. No entry and no effect on the ledger: posting is a separate sub-resource called afterwards. Tax is computed and rounded **per line**, and the document total is the sum of rounded lines — all of it computed in the module, none of it on this surface.
 * 
 * **No account code and no event code appear in the payload**: a line carries an itemGroup — a role qualifier — and the posting matrix alone turns it into an account (Rule 2).
 */
export async function draftSalesInvoice(transport: Transport, args: DraftSalesInvoiceArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/sales-invoices";
  const url = path;
  const body = encodeSchema(SCHEMAS, "SalesInvoiceRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
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

export interface PostCreditNoteArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف الإشعار الدائن. / The credit note identifier. */
  creditNoteId: string;
}

/**
 * ترحيل إشعار دائن / Post a credit note
 * 
 * يرحّل إشعاراً دائناً مسوّدة ويخصّصه على فاتورته الأصلية. حصين ضد التكرار بالشكل نفسه الذي يسلكه ترحيل الفاتورة: الوصول الثاني يُرجع المستند ذاته وalreadyPosted = true ورمز 200.
 * 
 * Posts a draft credit note and allocates it against its original invoice. Idempotent in exactly the same shape as posting an invoice: a second arrival returns the same document with alreadyPosted = true and status 200.
 */
export async function postCreditNote(transport: Transport, args: PostCreditNoteArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/credit-notes/" + encodeURIComponent(args.creditNoteId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
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

export interface PostSalesInvoiceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف فاتورة المبيعات. / The sales invoice identifier. */
  invoiceId: string;
}

/**
 * ترحيل فاتورة مبيعات / Post a sales invoice
 * 
 * يرحّل فاتورة مسوّدة فتصير **واقعة محاسبية**. مورد فرعي مستقلّ لا PUT على المستند: الترحيل فعلٌ يُنشئ قيداً، لا حقلٌ يُعدَّل.
 * 
 * **وحصين ضد التكرار بهوية الترحيل** (شركة · نوع المستند · معرّفه · رمز الإطلاق · الجيل · رمز الحدث): الوصول الثاني بالهوية نفسها يُرجع المستند ذاته وalreadyPosted = true ورمز 200 بدل 201، ولا يُنشئ قيداً ثانياً — **مهما كان ترتيب الوصول**. والحكم حكم بوّابة الوحدة لا مقارنةَ حالةٍ قُرئت قبل النداء: نداءان متزامنان يجتازان فحص «مسوّدة» معاً، ويلتقيان عند الهوية الواحدة، فيكتب أحدهما ويعود الآخر موسوماً.
 * 
 * ولا جسم لهذا الطلب: كل ما يحتاجه الترحيل موجود على المستند، ومفتاح الحصانة تشتقّه الوحدة من هويته ولا يُرسله العميل — فلا يستطيع عميلان أن يختارا مفتاحين لواقعة واحدة.
 * 
 * Posts a draft invoice, turning it into an **accounting fact**. A separate sub-resource, not a PUT on the document: posting is an act that creates an entry, not a field that is edited.
 * 
 * **Idempotent by the posting identity** (company, source document type, source document id, trigger, generation, event code): a second arrival with the same identity returns the same document with alreadyPosted = true and status 200 instead of 201, and never creates a second entry — **whatever the arrival order**. The verdict is the module gateway's, not a comparison against a state read before the call: two concurrent calls both pass the 'is it a draft' check, meet at the one identity, and one writes while the other returns marked.
 * 
 * This request has no body: everything posting needs is on the document, and the idempotency key is derived by the module from that identity rather than sent by the client — so two clients cannot choose two keys for one fact.
 */
export async function postSalesInvoice(transport: Transport, args: PostSalesInvoiceArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/sales-invoices/" + encodeURIComponent(args.invoiceId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface PostSupplierBillArgs {
  /** معرّف فاتورة المورد. / The supplier bill identifier. */
  billId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * ترحيل فاتورة مورد / Post a supplier bill
 * 
 * يرحّل فاتورة مورد مسوّدة. مورد فرعي مستقلّ، وحصين ضد التكرار بهوية الترحيل نفسها وبالسلوك نفسه: الوصول الثاني يُرجع المستند ذاته وalreadyPosted = true ورمز 200 بدل 201، بلا قيد ثانٍ.
 * 
 * Posts a draft supplier bill. A separate sub-resource, idempotent by the same posting identity with the same behaviour: a second arrival returns the same document with alreadyPosted = true and status 200 instead of 201, with no second entry.
 */
export async function postSupplierBill(transport: Transport, args: PostSupplierBillArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/supplier-bills/" + encodeURIComponent(args.billId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
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

export interface ReadChartOfAccountsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * دليل الحسابات بشروط الترحيل / The chart of accounts with its posting requirements
 * 
 * يقرأ دليل حسابات الشركة، وكل مدخل يحمل **ما يطلبه الحساب قبل أن يقبل سطراً**: نوع طرف الأستاذ المساعد، والأبعاد الإلزامية، ونمط العملة، وهل يقبل الترحيل أصلاً. وهذه هي المعلومة التي كانت **معلومة للخادم ومجهولة للعميل**: الدفتر يرفض بـ ledger.posting.missing_subledger و guard.GR-COA-002 برسالتين تسمّيان الحساب والطرف والبُعد، لكنّ العميل كان لا يبلغهما إلا **بأن يُرحِّل فيُرفَض**. فشاشةُ قيدٍ يدوية تُبنى من هذا المسار تمنع القيد الناقص قبل إرساله بدل أن تعرض رفضاً بعده. والدليل يُرجَع كاملاً — بآبائه التجميعية — وكل مدخل يحمل postable فيرشّح العميل بلا طلبٍ ثانٍ.
 * 
 * Reads the company's chart of accounts, each entry carrying **what the account requires before it will accept a line**: the subledger party type, the mandatory dimensions, the currency mode, and whether it is postable at all. This is the fact that was known to the server and unknown to the client: the ledger refuses with ledger.posting.missing_subledger and guard.GR-COA-002 in messages that name the account, the party, and the dimension, yet a client could reach those requirements only **by posting and being refused**. A manual voucher screen built from this path stops an incomplete entry before it is sent rather than showing a refusal after. The whole chart is returned — its non-postable parents included — and every entry carries postable, so the client filters with no second request.
 */
export async function readChartOfAccounts(transport: Transport, args: ReadChartOfAccountsArgs, signal?: AbortSignal): Promise<T.PostingChart> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/chart-of-accounts";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "PostingChart", response.json) as T.PostingChart;
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

export interface ReadCustomerArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف العميل. / The customer identifier. */
  customerId: string;
}

/**
 * قراءة عميل / Read one customer
 * 
 * يقرأ عميلاً واحداً داخل نطاق الشركة.
 * 
 * **ولاحظ ما ليس على هذا المورد: لا PUT ولا DELETE ولا مورد إيقاف.** غياب الحذف بنيوي كغيابه على القيود ومراكز التكلفة: عميلٌ تشير إليه قيود سنة سابقة لا يُحذف، وحذفه يكسر كل تقرير مُرحَّل. أمّا غياب الإيقاف فهو **إعلان نقص لا قرار منع**: وحدة المبيعات لا تملك اليوم إيقافاً — العمود is_active يُكتب مرّةً عند الإنشاء ولا يقرؤه مسار ترحيل واحد — وبابٌ اسمه «إيقاف» لا يمنع فاتورةً واحدة أسوأ من غيابه: يبدو ضابطاً وليس كذلك.
 * 
 * Reads a single customer within the company scope.
 * 
 * **Note what this resource does not carry: no PUT, no DELETE, and no suspension sub-resource.** The absence of delete is structural, as it is on entries and cost centres: a customer referenced by last year's entries is never deleted, and deleting it breaks every posted report. The absence of suspension is instead a **declared gap, not a prohibition**: the sales module has no suspension today — the is_active column is written once at creation and read by no posting path — and a door labelled 'suspend' that stops not one invoice is worse than no door: it looks like a control and is not one.
 */
export async function readCustomer(transport: Transport, args: ReadCustomerArgs, signal?: AbortSignal): Promise<T.Party> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/customers/" + encodeURIComponent(args.customerId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Party", response.json) as T.Party;
}

/**
 * صفحة استعراض العقد / The contract browser page
 * 
 * صفحة HTML **قائمة بذاتها بالكامل**: لا خطّ خارجي ولا نصّ برمجي من شبكة توصيل ولا صورة بعيدة. تقرأ /openapi/v1.json من الخادم نفسه وتعرض مساراته ومخطّطاته، وفيها زرّ «جرّب» يُصدر طلباً حقيقياً. والاعتماد الذي يُكتب فيها يبقى في ذاكرة الصفحة وحدها — لا في localStorage ولا في عنوان ولا في ملفّ ارتباط — و**لا رمز مطبوع فيها**: تُخدَم بايتاتها نفسها لكل طالب. والطلب الذي تُصدره يمرّ بالمصادقة والنطاق والاستحقاق كأي عميل، فشركةٌ خارج نطاق الاعتماد تُرفض بـ403 tenancy.company_out_of_scope منها كما تُرفض من curl.
 * 
 * A **fully self-contained** HTML page: no external font, no script from a delivery network, no remote image. It reads /openapi/v1.json from this same server and renders its paths and schemas, with a Try-it button that issues a real request. A credential typed into it stays in the page's memory alone — not in localStorage, not in a URL, not in a cookie — and **no token is baked into it**: the same bytes are served to every caller. The request it issues passes through authentication, scope, and entitlement like any client, so a company outside the credential's scope is refused with 403 tenancy.company_out_of_scope from the page exactly as it is from curl.
 */
export async function readDocsPage(transport: Transport, signal?: AbortSignal): Promise<void> {
  const path = "/docs";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
}

export interface ReadDocumentShapeArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** رمز نوع المستند من المجموعة المغلقة. / The document type code from the closed set. */
  documentType: "projects.client_certificate" | "purchasing.supplier_bill" | "sales.invoice";
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

export interface ReadPayablesAgingArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** تاريخ التقرير الميلادي. / The Gregorian report date. */
  asOf: string;
}

/**
 * أعمار الذمم الدائنة / Payables aging
 * 
 * أعمار ذمم الموردين في تاريخ معلوم، بالشرائح نفسها وبالشكل نفسه الذي تُقرأ به الذمم المدينة — شكلٌ واحد لا شكلان: تقريران بشرائح مختلفة يجعلان المقارنة بينهما عملاً يدوياً.
 * 
 * Supplier payables aged at a given date, in the same bands and the same shape as receivables — one shape, not two: two reports with different bands make comparing them manual work.
 */
export async function readPayablesAging(transport: Transport, args: ReadPayablesAgingArgs, signal?: AbortSignal): Promise<T.AgingReport> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payables-aging";
  const query = new URLSearchParams();
  query.set("asOf", args.asOf);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "AgingReport", response.json) as T.AgingReport;
}

/**
 * العقد المنشور نفسه / The published contract itself
 * 
 * يخدم بايتات contracts/openapi/v1.json **كما أُودعت** — مضمَّنةً في التجميعة وقت البناء. ولا تُبنى وثيقة وقت التشغيل: الوثيقة تُولَّد بـ--emit-openapi وتُودَع ويحرسها PublishedContractTests بايتاً بايت، ويحرس Rule18 العميلَ المُولَّد مقابلها. وخادمٌ يبني وثيقةً ثالثة عند كل طلب يضع طرفاً خارج الحارسَين، وواجهةٌ تعرض عقداً لم يولّده أحد تبدو مرجعاً وهي خطأ — وهو فخ-84 من بابه الثالث. وهذا الباب بلا اعتماد: محتواه ملفٌّ مُودَع في المستودع، ولا يحمل بيانات مستأجر واحد.
 * 
 * Serves the bytes of contracts/openapi/v1.json **exactly as committed**, embedded into the assembly at build time. No document is built at runtime: the document is generated by --emit-openapi, committed, guarded byte for byte by PublishedContractTests, and the generated client is guarded against it by Rule18. A server that builds a third document per request puts a side outside both guards, and a docs page showing a contract nobody generated looks authoritative and is wrong — فخ-84 by its third door. This path is anonymous: its content is a file committed in the repository, and it carries no tenant data whatsoever.
 */
export async function readPublishedContract(transport: Transport, signal?: AbortSignal): Promise<unknown> {
  const path = "/openapi/v1.json";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return response.json as unknown;
}

export interface ReadReceivablesAgingArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** تاريخ التقرير الميلادي. / The Gregorian report date. */
  asOf: string;
}

/**
 * أعمار الذمم المدينة / Receivables aging
 * 
 * أعمار ذمم العملاء في تاريخ معلوم، بشرائح: لم يستحق · 1–30 · 31–60 · 61–90 · فوق 90، ومجموعٍ هو مجموع الشرائح بالضبط. نقطة قراءة: تعمل والاشتراك للقراءة فقط.
 * 
 * Customer receivables aged at a given date, in bands: not due, 1-30, 31-60, 61-90, over 90, with a total that is exactly the sum of the bands. A read point: it works while the subscription is read-only.
 */
export async function readReceivablesAging(transport: Transport, args: ReadReceivablesAgingArgs, signal?: AbortSignal): Promise<T.AgingReport> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/receivables-aging";
  const query = new URLSearchParams();
  query.set("asOf", args.asOf);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "AgingReport", response.json) as T.AgingReport;
}

export interface ReadSalesInvoiceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف فاتورة المبيعات. / The sales invoice identifier. */
  invoiceId: string;
}

/**
 * قراءة فاتورة مبيعات / Read one sales invoice
 * 
 * يقرأ فاتورة بحالتها ومجاميعها ومعرّف قيدها إن رُحّلت. ونقطة قراءة: تعمل والاشتراك للقراءة فقط.
 * 
 * Reads an invoice with its state, its totals, and its entry identifier if posted. A read point: it works while the subscription is read-only.
 */
export async function readSalesInvoice(transport: Transport, args: ReadSalesInvoiceArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/sales-invoices/" + encodeURIComponent(args.invoiceId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

/**
 * الجلسة: الهوية والشركات المبلوغة / The session: identity and reachable companies
 * 
 * يُرجع من يقف خلف الاعتماد — المستأجر والمستخدم — و**الشركات التي يبلغها هذا الاعتماد بأسمائها**. وهذه أول نقطة يناديها عميل: معرّف الشركة إلزامي في كل مسار آخر وهو معرّف بصيغة 8-4-4-4-12، ولا يستطيع إنسان أن يكتبه — فيُختار من هنا ولا يُكتب.
 * 
 * والاسم العربي هو السجلّ وnameTranslations ترجماته أيّاً كان عددها (ADR-0021)؛ ولا حقل ثابت للإنجليزية هنا كما لا حقل لها في صفّ ميزان المراجعة.
 * 
 * **والفشل مغلق:** اعتماد لا يبلغ أي شركة يُرفض بـ403 وsession.no_reachable_company ولا يُسلَّم قائمة فارغة — القائمة الفارغة تُقرأ «لا بيانات بعد» فينتظر المستخدم شيئاً لن يأتي، والناقص ربطُ الاعتماد بمنشأة. ومنشأةٌ لم تُؤسَّس بعد **تظهر** في القائمة بـstate = NotSetUp ولا تُخفى: إخفاؤها يجعل صاحب الاعتماد الوحيد يرى قائمة فارغة ويقرؤها «اعتمادي لا يصلح».
 * 
 * وهذا المسار خارج نطاق الشركة عمداً — وهو الوحيد كذلك بعد نقطة الصحّة — ومع ذلك لا يخرج منه شيء عن مستأجر آخر: القائمة هي مجموعة الاعتماد نفسها، لا استعلام على جدول شركات بمرشِّح.
 * 
 * Returns who stands behind the credential — tenant and user — and **the companies this credential reaches, by name**. This is the first call any client makes: the company identifier is mandatory on every other path and is an 8-4-4-4-12 identifier no human can type — so it is chosen here, never typed.
 * 
 * The Arabic name is the record and nameTranslations are its translations, however many (ADR-0021); there is no fixed English field here, just as there is none on a trial-balance row.
 * 
 * **Fail closed:** a credential that reaches no company is refused with 403 and session.no_reachable_company rather than handed an empty list — an empty list reads as 'no data yet' and leaves the user waiting for something that will never arrive, when what is missing is the credential's link to a company. A company not yet set up **appears** in the list with state = NotSetUp and is not hidden: hiding it makes the holder of a single-company credential see an empty list and read it as 'my credential is broken'.
 * 
 * This path is outside company scope deliberately — the only one after health — and still nothing about another tenant crosses it: the list is the credential's own set, not a filtered query over a companies table.
 */
export async function readSession(transport: Transport, signal?: AbortSignal): Promise<T.Session> {
  const path = "/api/v1/session";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Session", response.json) as T.Session;
}

export interface ReadSupplierArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف المورد. / The supplier identifier. */
  supplierId: string;
}

/**
 * قراءة مورد / Read one supplier
 * 
 * يقرأ مورداً واحداً. وما غاب عن مورد العميل غائب هنا وللسبب نفسه: لا حذف بنيوياً، ولا إيقاف بعد.
 * 
 * Reads a single supplier. What is absent from the customer resource is absent here for the same reasons: no delete, structurally, and no suspension yet.
 */
export async function readSupplier(transport: Transport, args: ReadSupplierArgs, signal?: AbortSignal): Promise<T.Party> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/suppliers/" + encodeURIComponent(args.supplierId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Party", response.json) as T.Party;
}

export interface ReadSupplierBillArgs {
  /** معرّف فاتورة المورد. / The supplier bill identifier. */
  billId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * قراءة فاتورة مورد / Read one supplier bill
 * 
 * يقرأ فاتورة مورد بحالتها ومجاميعها ومعرّف قيدها إن رُحّلت.
 * 
 * وكانت هذه القراءة **غير موجودة في الوحدة أصلاً**: تُنشأ الفاتورة وتُرحَّل ولا توجد جملة تقول «ما حالها الآن؟». فمن أنشأ مسوّدةً ثم انقطع اتصاله لم يكن أمامه إلا أن **يعيد الترحيل ليعرف**.
 * 
 * Reads a supplier bill with its state, its totals, and its entry identifier if posted.
 * 
 * This read **did not exist in the module at all**: a bill could be created and posted, and there was no sentence for 'what state is it in now?'. Whoever created a draft and then lost their connection had no option but to **post again in order to find out**.
 */
export async function readSupplierBill(transport: Transport, args: ReadSupplierBillArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/supplier-bills/" + encodeURIComponent(args.billId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
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
