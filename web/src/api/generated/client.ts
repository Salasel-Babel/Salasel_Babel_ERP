/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     5737bbd9884cd9f40d0e211f37afd938f378a36c5a1c72e40286dd6fa0a90952
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

export interface ActivateLeaseContractArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف عقد الإيجار. / The lease contract identifier. */
  leaseId: string;
}

/**
 * تفعيل عقد إيجار / Activate a lease contract
 * 
 * يُفعّل العقد: يفحص أن مجموع الأقساط يساوي قيمة العقد بالضبط، ثم يجعل جدول الدفعات قابلاً للفوترة، ويُدخل المدّة **قيد الاستبعاد الزمني** في قاعدة البيانات.
 * 
 * **والقيد في القاعدة لا في الواجهة**: «مدّة سارية واحدة لكل وحدة» شرط **تقاطع مدى** لا شرط تساوٍ، فلا يعبّر عنه فهرس فريد مهما اتّسع؛ وفحصٌ في الخدمة يقرأ ثم يكتب، وبين القراءة والكتابة يمرّ نداءٌ آخر فتُؤجَّر الوحدة مرّتين.
 * 
 * **ولا يُرحّل قيداً**، ومخطّط جوابه بلا معرّف قيد.
 * 
 * Activates the contract: it checks that the instalments sum exactly to the contract value, makes the payment schedule billable, and enters the term into a **temporal exclusion constraint** in the database.
 * 
 * **The constraint is in the database, not the interface**: 'one live term per unit' is a **range-overlap** condition, not an equality condition, so no unique index however wide can express it; and a check in the service reads then writes, and between the read and the write another call slips through and the unit is let twice.
 * 
 * **It posts no entry**, and its response schema carries no entry identifier.
 */
export async function activateLeaseContract(transport: Transport, args: ActivateLeaseContractArgs, signal?: AbortSignal): Promise<T.Lease> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/lease-contracts/" + encodeURIComponent(args.leaseId) + "/activation";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Lease", response.json) as T.Lease;
}

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

export interface AddItemArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.ItemRequest;
}

/**
 * تسجيل صنف / Register an item
 * 
 * يسجّل صنفاً: رمزه، واسمه ثنائي اللغة، ومجموعته، و**وحدة أساسه ومعاملات تحويل وحداته الأكبر**.
 * 
 * **ووحدة الأساس أصغر وحدة يُمسَك بها الصنف**، وإليها تُحوَّل البقية. والمعامل **بسطٌ ومقام صحيحان لا عددٌ عشري**: «الكرتون اثنتا عشرة حبّة» هو 12/1، و«الحبّة ثلث علبة» هو 1/3 — والثاني لا يُكتب عشرياً بلا خسارة، وخسارةٌ في كمّية تُضرب في تكلفة الوحدة تصل إلى المال. والتحويل الذي لا يقع بلا باقٍ **يُرفض باسمه** ولا يُقرَّب.
 * 
 * **ولا رمز حساب هنا**: الصنف يحمل itemGroup — مؤهّل دور — ومصفوفة الترحيل وحدها تُحوّله إلى حساب.
 * 
 * **ولاحظ ما ليس على هذا المورد: لا تعديل ولا حذف.** رمزُ الصنف هوية تحملها قيود سنةٍ مضت، وحذفُه يكسر كل تقرير مُرحَّل؛ وتغييرُ وحدة أساسه بعد أن كُتبت عليه حركات يجعل مجموع حركاته جمعَ أعدادٍ بمقاييس مختلفة. وذلك **نقص سطحٍ مُعلَن** لا قرار منع.
 * 
 * Registers an item: its code, its bilingual name, its group, and **its base unit with the conversion factors of its larger units**.
 * 
 * **The base unit is the smallest unit the item is held in**, and everything else converts into it. A factor is **an integer numerator and denominator, not a decimal**: 'a carton is twelve pieces' is 12/1 and 'a piece is a third of a box' is 1/3 — the second cannot be written decimally without loss, and loss in a quantity that gets multiplied by a unit cost reaches the money. A conversion that does not divide exactly is **refused by name** rather than rounded.
 * 
 * **No account code appears here**: an item carries an itemGroup — a role qualifier — and the posting matrix alone turns it into an account.
 * 
 * **Note what this resource does not carry: no update and no delete.** The item code is an identity carried by last year's entries; deleting it breaks every posted report, and changing its base unit after movements have been written against it makes the sum of those movements an addition of numbers on different scales. That is a **declared surface gap**, not a prohibition.
 */
export async function addItem(transport: Transport, args: AddItemArgs, signal?: AbortSignal): Promise<T.Item> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/items";
  const url = path;
  const body = encodeSchema(SCHEMAS, "ItemRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Item", response.json) as T.Item;
}

export interface AddPayComponentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrPayComponentRequest;
}

/**
 * تعريف مكوّن أجر / Define a pay component
 * 
 * يعرّف مكوّن أجر بوسمَيه: هل يدخل **وعاء اشتراك التأمينات**، وهل يدخل **وعاء مكافأة نهاية الخدمة**.
 * 
 * **وهذا هو الباب الذي يجعل الأثر التنظيمي بياناتٍ لا شيفرة.** الوسمان يملؤهما المحاسب، والوحدة تشتقّ منهما الوعاء ولا تعرف أي بدل يدخل وأيّه لا يدخل — وذلك سؤالٌ نظامي **غير محسوم** في هذا المستودع ولا يُخترع في شيفرة.
 * 
 * **ولا يحمل المكوّن مبلغاً ولا نسبة**: القيمة تُسنَد بتاريخ سريان على مورد آخر، والنسبة لا تدخل إلا من مورد الإعدادات.
 * 
 * Defines a pay component with its two flags: does it enter the **social insurance contributory wage**, and does it enter the **end-of-service benefit base**.
 * 
 * **This is the door that makes the regulatory effect data rather than code.** The accountant fills the flags, and the module derives the base from them; it does not know which allowance enters and which does not — that is a regulatory question **left undecided** in this repository and never invented in code.
 * 
 * **A component carries neither an amount nor a rate**: a value is assigned with an effective date on another resource, and a rate enters only through the settings resource.
 */
export async function addPayComponent(transport: Transport, args: AddPayComponentArgs, signal?: AbortSignal): Promise<T.HrPayComponent> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/pay-components";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrPayComponentRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayComponent", response.json) as T.HrPayComponent;
}

export interface AddPayElementArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف الموظف. وهو **غير** رمزه المعتم: الرمز هو ما يعبر إلى الدفتر المساعد، والمعرّف عنوانٌ على هذا السطح. / The employee identifier. It is **not** the opaque code: the code is what crosses into the subledger, while the identifier is an address on this surface. */
  employeeId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrPayElementRequest;
}

/**
 * إسناد قيمة مكوّن أجر بتاريخ سريان / Assign a pay component value with an effective date
 * 
 * يُسند قيمة مكوّن إلى علاقة عمل الموظف بتاريخ سريانها. **إنشاءٌ لا تعديل**: الزيادة صفٌّ جديد بتاريخ سريان جديد، ولا PUT على صفّ قائم.
 * 
 * **ولماذا هذا شرطٌ لا اصطلاح:** مسيّرٌ ماضٍ رُحِّل قيده يجب أن يُعاد حسابه فيطابق قيده حرفاً بحرف. وتعديلُ صفّ الأجر في مكانه يجعل ذلك مستحيلاً — يُقرأ الأجر الجديد ويُقارَن بقيدٍ بُني على القديم، فيصير كل مسيّر ماضٍ «منحرفاً» بلا سبب يُقرأ.
 * 
 * Assigns a component value to the employee's employment from an effective date. **A creation, not an edit**: an increase is a new row with a new effective date, and there is no PUT on an existing row.
 * 
 * **Why this is a requirement and not a convention:** a past run whose entry was posted must be recomputable and match that entry exactly. Editing the pay row in place makes that impossible — the new pay is read and compared against an entry built on the old one, so every past run becomes 'divergent' with no readable cause.
 */
export async function addPayElement(transport: Transport, args: AddPayElementArgs, signal?: AbortSignal): Promise<T.HrPayElement> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employees/" + encodeURIComponent(args.employeeId) + "/pay-elements";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrPayElementRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayElement", response.json) as T.HrPayElement;
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

export interface AllocateTenantReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند القبض من المستأجر. / The tenant receipt identifier. */
  receiptId: string;
  /** جسم الطلب. / The request body. */
  body: T.AllocationRequest;
}

/**
 * تخصيص سند قبض ورد بلا مرجع / Allocate a tenant receipt that arrived without a reference
 * 
 * يخصّص سنداً رُحّل غير مخصَّص على مستأجر تبيّن أنه صاحبه — **بقيدٍ مستقلّ لا عكسٍ للقيد السابق**.
 * 
 * **ولماذا لا عكس:** المال وصل فعلاً، وعكسُ قيد التحصيل يمحو واقعةً وقعت ويترك الدفتر يقول إنها لم تقع. والتخصيص ينقل من حساب التحصيلات غير المخصَّصة إلى ذمم المستأجرين، وهو حدثٌ قائم بذاته في مصفوفة الترحيل.
 * 
 * ورمز الحدث **داخل هوية الترحيل**، فقيدا التحصيل والتخصيص على المستند نفسه لا يتصادمان ولا يبتلع أحدهما الآخر.
 * 
 * Allocates a receipt that was posted unallocated to the tenant it turns out to belong to — **as a separate entry, never as a reversal of the earlier one**.
 * 
 * **Why not a reversal:** the money genuinely arrived, and reversing the collection entry erases a fact that occurred and leaves the ledger saying it did not. Allocation moves from unallocated collections to the tenant receivable, and is an event in its own right in the posting matrix.
 * 
 * The event code sits **inside the posting identity**, so the collection and allocation entries on the same document never collide and neither swallows the other.
 */
export async function allocateTenantReceipt(transport: Transport, args: AllocateTenantReceiptArgs, signal?: AbortSignal): Promise<T.TenantReceipt> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/tenant-receipts/" + encodeURIComponent(args.receiptId) + "/allocation";
  const url = path;
  const body = encodeSchema(SCHEMAS, "AllocationRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "TenantReceipt", response.json) as T.TenantReceipt;
}

export interface ChangeMembershipRoleArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف العضوية — **وهو معرّف عضوها**: هوية العضوية (المنشأة، العضو)، والمنشأة في المسار سلفاً. / The membership identifier — **which is its member's identifier**: a membership's identity is (company, member), and the company is already in the path. */
  membershipId: string;
  /** جسم الطلب. / The request body. */
  body: T.ChangeMembershipRoleRequest;
}

/**
 * تغيير دور عضوية / Change a membership's role
 * 
 * يغيّر دور عضوٍ في المنشأة. مورد فرعي مستقلّ على نمط plan-changes: الدور **صلاحيةُ وصول**، وتغييرُه حدثٌ يُكتب في سجلّ التدقيق لا حقلٌ يُعدَّل بتحديث جزئي.
 * 
 * والدور يُطابَق حرفياً من مجموعة مغلقة، ودورٌ لا أثر له زينة: Reader يقرأ ولا يكتب — وكل فعل غير آمن في منشأةٍ دورُه فيها Reader يُردّ بـ403 وmembership.read_only — وOwner يدعو ويسحب ويغيّر الأدوار.
 * 
 * **ورمز الدور يفترق عن رمز الاستحقاق عمداً**: ذاك يقول «جدّد اشتراكك» وهذا يقول «اطلب صلاحية»، وخلطهما يجعل قارئاً يتّصل بالمحاسبة بلا سبب.
 * 
 * **ولا يُخفَض آخر مالك** (409 وmembership.last_owner)، و**الدور الذي هو الدور القائم يُرفض** (409 وmembership.role_unchanged): ردُّ «تمّ» على فعلٍ لم يقع أسوأ من الرفض.
 * 
 * Changes a member's role in the company. A subresource in the plan-changes shape: a role is **an access grant**, and changing it is an event written to the audit log, not a field patched in place.
 * 
 * The role is matched literally against a closed set, and a role with no effect is decoration: Reader reads and writes nothing — every unsafe method in a company where the role is Reader is refused with 403 and membership.read_only — and Owner invites, revokes, and changes roles.
 * 
 * **The role code differs from the entitlement code deliberately**: that one says 'renew your subscription', this one says 'ask for a permission'; conflating them sends a reader to call accounting for no reason.
 * 
 * **The last owner is not demoted** (409, membership.last_owner), and **a role equal to the current role is refused** (409, membership.role_unchanged): answering 'done' to an act that did not happen is worse than refusing.
 */
export async function changeMembershipRole(transport: Transport, args: ChangeMembershipRoleArgs, signal?: AbortSignal): Promise<T.MembershipRoleChange> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/memberships/" + encodeURIComponent(args.membershipId) + "/role-changes";
  const url = path;
  const body = encodeSchema(SCHEMAS, "ChangeMembershipRoleRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "MembershipRoleChange", response.json) as T.MembershipRoleChange;
}

export interface ChangeSubscriptionPlanArgs {
  /** معرّف المستأجر. يُطابَق بمستأجر الاعتماد ويُرفض إن اختلف؛ ولا يُفرَّق في الرفض بين «لا وجود له» و«ليس مستأجرك». / The tenant identifier. It is matched against the credential's tenant and refused when it differs; the refusal does not distinguish 'does not exist' from 'not yours'. */
  tenantId: string;
  /** جسم الطلب. / The request body. */
  body: T.ChangePlanRequest;
}

/**
 * تغيير الخطّة / Change the plan
 * 
 * مورد فرعي مستقلّ لا PUT على الاشتراك: تغيير الخطّة **حدثٌ** له سندٌ وفاعلٌ ولحظة، ويُغلق صفّ اشتراك ويفتح آخر — فيبقى تاريخ الاشتراك مقروءاً.
 * 
 * وما تغطّيه الخطّة الجديدة يصير مستحقّاً، و**ما خرج منها يهبط إلى أرضيته لا إلى العدم**: وحدةٌ بلغ عملُها الدفتر تبقى مقروءةً كاملةً بعد خروجها من الحزمة.
 * 
 * **والسند إلزامي** — رقم عقد، أو حدث سداد، أو تذكرة، أو قرار مُوثَّق — لأن الاستحقاق يحكم أي بيانات مالية يجوز إنشاؤها، فتغييره حدث تدقيقي لا إعداد واجهة.
 * 
 * **وهو فعل مشغِّل** يُطلب باعتماد التزويد وحده: لا قناة سداد في هذا المنتَج بعد، فبابٌ يرفع به صاحبُ الاشتراك خطّته هو ترقيةٌ بلا ثمن. الرمز الثابت عند الرفض: subscription.operator_credential_required.
 * 
 * A subresource, not a PUT on the subscription: a plan change is an **event** with authority, an actor, and an instant; it closes one subscription row and opens another, so the subscription's history stays readable.
 * 
 * What the new plan covers becomes entitled, and **what falls outside it drops to its floor, not to nothing**: a module whose work reached the ledger stays fully readable after leaving the package.
 * 
 * **Authority is mandatory** — a contract number, a payment event, a ticket, or a documented decision — because entitlement governs which financial data may be created, so changing it is an audit event, not a UI setting.
 * 
 * **It is an operator act** requested with the provisioning credential alone: this product has no payment channel yet, so a door letting a subscriber raise their own plan is a free upgrade. Stable refusal code: subscription.operator_credential_required.
 */
export async function changeSubscriptionPlan(transport: Transport, args: ChangeSubscriptionPlanArgs, signal?: AbortSignal): Promise<T.Subscription> {
  const path = "/api/v1/tenants/" + encodeURIComponent(args.tenantId) + "/subscription/plan-changes";
  const url = path;
  const body = encodeSchema(SCHEMAS, "ChangePlanRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Subscription", response.json) as T.Subscription;
}

export interface CreateLesseeArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.RealEstatePartyRequest;
}

/**
 * تسجيل مستأجر عقاري / Register a lessee
 * 
 * يسجّل مستأجراً عقارياً.
 * 
 * **ولماذا اسم المورد lessees لا tenants:** المسار /api/v1/tenants ومسارات الاشتراك تحته منشورةٌ في هذا العقد نفسه لمستأجر **النظام**، ونشرُ الكلمة بمعنيين يجعل العقد يكذب على قارئه قبل أن يتصادم التوجيه.
 * 
 * **وtaxResidency بلا افتراضي**: عليها يتوقّف سطر الاستقطاع في توريد المالك، وقيمةٌ افتراضية «مقيم» تُسقط الاستقطاع بصمت عن كل طرفٍ لم يُملأ حقله.
 * 
 * Registers a real-estate lessee.
 * 
 * **Why the resource is lessees and not tenants:** the path /api/v1/tenants and the subscription paths beneath it are published in this very contract for the **system** tenant, and publishing the word with two meanings makes the contract lie to its reader before routing ever collides.
 * 
 * **taxResidency has no default**: the withholding line on an owner payout depends on it, and a default of 'resident' silently drops withholding for every party whose field was left unfilled.
 */
export async function createLessee(transport: Transport, args: CreateLesseeArgs, signal?: AbortSignal): Promise<T.RealEstateParty> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/lessees";
  const url = path;
  const body = encodeSchema(SCHEMAS, "RealEstatePartyRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RealEstateParty", response.json) as T.RealEstateParty;
}

export interface CreatePropertyArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.PropertyRequest;
}

/**
 * تسجيل عقار / Register a property
 * 
 * يسجّل عقاراً **ويسجّل بُعده في دفتر الأستاذ في العملية نفسها**.
 * 
 * **ولماذا العمليتان واحدة:** قاعدة الحجب GR-RE-001 تُقيَّم على واقعة نموذج ملكية العقار، ومصدرها الوحيد المُعتمَد صفُّ العقار في سجلّ أبعاد الدفتر. وحين يغيب ذلك الصفّ تعود القاعدة **غير قابلة للتقييم**، والقاعدة التي لا تُقيَّم لا تُتجاوَز — فيُرفض القيد كاملاً. أي أن عقاراً غير مسجَّل يُعطّل دورته كلها بصوت عالٍ ولا يمرّ صامتاً.
 * 
 * **وownershipModel بلا قيمة افتراضية**: هو ما يقرّر أدائنُ الأجرة إيرادَ الشركة أم أمانةً لمالكها، و**لا يُعدَّل بعد التسجيل** — تغييره يُعيد تفسير قيودٍ مُرحَّلة بأثر رجعي، ودورُ التطبيق لا يملك update ولا delete على ذلك السجلّ أصلاً.
 * 
 * وفي نموذج الإدارة **المالك إلزامي**: سطر أمانات الملاك يحمل طرفاً في دفتره المساعد.
 * 
 * Registers a property **and registers its dimension in the ledger within the same operation**.
 * 
 * **Why the two are one act:** guard rule GR-RE-001 is evaluated on the property ownership-model fact, whose only authoritative source is the property row in the ledger dimension register. When that row is missing the rule becomes **undecidable**, and a rule that cannot be evaluated is never bypassed — so the whole entry is refused. An unregistered property therefore disables its entire cycle loudly rather than passing silently.
 * 
 * **ownershipModel has no default**: it decides whether the credit of rent is company revenue or a liability to an owner, and it **is never edited after registration** — changing it reinterprets already-posted entries retroactively, and the application role holds neither update nor delete on that register.
 * 
 * Under the managed model an **owner is mandatory**: the owner trust line carries a party in its subledger.
 */
export async function createProperty(transport: Transport, args: CreatePropertyArgs, signal?: AbortSignal): Promise<T.Property> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/properties";
  const url = path;
  const body = encodeSchema(SCHEMAS, "PropertyRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Property", response.json) as T.Property;
}

export interface CreatePropertyOwnerArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.RealEstatePartyRequest;
}

/**
 * تسجيل مالك عقار / Register a property owner
 * 
 * يسجّل مالك عقار بحقلَي التسجيل الضريبي والإقامة الضريبية. وفي نموذج الإدارة تُقيَّد أجرةُ عقاره **أمانةً له** لا إيراداً للشركة، وإيرادُ الشركة هو العمولة وحدها.
 * 
 * Registers a property owner with its VAT registration and tax residency. Under the managed model the rent of its property is booked **as a liability to it** rather than as company revenue; the company's revenue is the commission alone.
 */
export async function createPropertyOwner(transport: Transport, args: CreatePropertyOwnerArgs, signal?: AbortSignal): Promise<T.RealEstateParty> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/property-owners";
  const url = path;
  const body = encodeSchema(SCHEMAS, "RealEstatePartyRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RealEstateParty", response.json) as T.RealEstateParty;
}

export interface CreatePurchaseOrderArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.PurchaseOrderRequest;
}

/**
 * إنشاء أمر شراء / Create a purchase order
 * 
 * يُنشئ أمر شراء ويُرجعه **بسطوره ومعرّفاتها** — وهي مدخل الاستلام: سطر الاستلام يشير إلى سطر الأمر بمعرّفه، فمن أراد أن يستلم قرأ أمره أولاً أو استعمل جواب هذا الطلب.
 * 
 * **ولاحظ ما ليس على هذا المورد — ولا يجوز أن يوجد: لا مورد /posting.** أمر الشراء **التزام تعاقدي لا حدث محاسبي**: لا يُنشئ قيداً، ولا يمسّ حساباً، ولا يُثبَت في الدفتر. والقيد الأول في دورة الشراء هو **الاستلام**، لأن البضاعة عنده دخلت والالتزام نشأ فعلاً بينما فاتورة المورد لم تصل بعد. وبابُ ترحيلٍ هنا كان سيكون خطأً محاسبياً مكتوباً في عقد منشور — وهو ما يُقرأ من شكل السطح نفسه لا من تعليق.
 * 
 * ولذلك أيضاً مخطّط الجواب PurchaseOrder **لا يحمل entryId ولا alreadyPosted**: حقلٌ فارغ لهما كان سيُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل أبداً».
 * 
 * **ولا ربط بطلب شراء داخلي هنا**: طلب الشراء مستند داخلي لم يُنشر على هذا السطح، وحقلٌ يشير إلى ما لا يستطيع العميل إنشاؤه زينةٌ لا سبيل إلى ملئها. وهو نقصٌ مُعلَن.
 * 
 * Creates a purchase order and returns it **with its lines and their identifiers** — the input a goods receipt needs: a receipt line refers to an order line by its identifier, so whoever receives goods reads the order first, or uses this response.
 * 
 * **Note what this resource does not carry, and must not: no /posting sub-resource.** A purchase order is a **contractual commitment, not an accounting event**: it creates no entry, touches no account, and is never recorded in the ledger. The first entry in the purchasing cycle is the **goods receipt**, because that is when the goods arrive and the obligation actually exists while the supplier's invoice has not yet come. A posting door here would be an accounting error written into a published contract — and its absence is read from the shape of the surface itself, not from a comment.
 * 
 * For the same reason the PurchaseOrder response schema **carries neither entryId nor alreadyPosted**: an empty field for either would read as 'not posted yet' rather than 'never posted'.
 * 
 * **And there is no link to an internal purchase request here**: the purchase request is an internal document not published on this surface, and a field pointing at something the client cannot create is decoration with no way to fill it. This is a declared gap.
 */
export async function createPurchaseOrder(transport: Transport, args: CreatePurchaseOrderArgs, signal?: AbortSignal): Promise<T.PurchaseOrder> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/purchase-orders";
  const url = path;
  const body = encodeSchema(SCHEMAS, "PurchaseOrderRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "PurchaseOrder", response.json) as T.PurchaseOrder;
}

export interface CreateUnitArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف العقار. / The property identifier. */
  propertyId: string;
  /** جسم الطلب. / The request body. */
  body: T.UnitRequest;
}

/**
 * تسجيل وحدة داخل عقار / Register a unit within a property
 * 
 * يسجّل وحدةً **مورداً فرعياً للعقار**: بيانات الأبعاد تشترط العقار مع الوحدة، وقيدُ تحقّقٍ على سطر القيد يفرض الاقتران على أي كاتب. فالاشتراط **بنيةٌ في العنوان** لا تحقّقٌ في الجسم.
 * 
 * **وusage وvatTreatment حقلان صريحان لا يُشتقّ أحدهما من الآخر ولا من نوع العقار**: العقار المختلط يولّد توريداً خاضعاً ومعفى في آنٍ واحد، والاشتقاق الآلي يُنتج آلاف العقود بتصنيفٍ واحد خاطئ.
 * 
 * Registers a unit as a **sub-resource of the property**: the dimension data requires the property alongside the unit, and a check constraint on the journal line enforces the pairing against any writer. The requirement is therefore **structure in the address**, not validation in the body.
 * 
 * **usage and vatTreatment are explicit fields, neither derived from the other nor from the property type**: a mixed-use property produces taxable and exempt supplies at once, and automatic derivation produces thousands of contracts under one wrong classification.
 */
export async function createUnit(transport: Transport, args: CreateUnitArgs, signal?: AbortSignal): Promise<T.Unit> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/properties/" + encodeURIComponent(args.propertyId) + "/units";
  const url = path;
  const body = encodeSchema(SCHEMAS, "UnitRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Unit", response.json) as T.Unit;
}

export interface DepositAttachmentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** حمولة multipart: جزءٌ اسمه content يحمل البايتات. / The multipart payload: a part named content carries the bytes. */
  body: FormData;
}

/**
 * إيداع مرفق / Deposit an attachment
 * 
 * يودِع بايتات مرفق ويُرجع وصفه: المعرّف والبصمة والحجم والنوع **المشموم**.
 * 
 * **والحمولة multipart/form-data لا JSON**: جسم JSON يعني ترميز البايتات نصّاً — انتفاخ الثلث، وصورةً كاملة في سجلّ الطلب، وحمولةً لا يستطيع وسيطٌ أن يتخطّاها بتدفّق.
 * 
 * **والنوع يأتي من البايتات لا من الاسم ولا من الترويسة.** الأرقام السحرية وحدها تُقرأ، وما لا يُتعرَّف عليه **يُرفض** بـ415 ولا يُخزَّن بنوع محايد — فالمحايد يُقدَّم غداً إلى متصفّح بترويسة يخترعها القارئ. وإعلانٌ يخالف المشموم **رفضٌ باسمه لا تصحيحٌ صامت**: التصحيح الصامت يجعل العميل يظنّ أن ما أرسله قُبل كما أرسله.
 * 
 * **واسم الملفّ بيانات لا مسار.** لا يشارك في بناء أي مسار على الإطلاق — مفتاح الكائن يولّده المخزن من 256 بتّاً معمّى — ويُطهَّر بقائمة **سماح** لا منع: يُسقط منه فاصل المسار ومحارف التحكّم والمحارف الاتجاهية غير المرئية (U+202E يقلب gpj.exe فيُقرأ exe.jpg). والاسم العربي يبقى عربياً كاملاً، واسمٌ لا يبقى منه محرف مقبول واحد يُرفض بـ400.
 * 
 * **والحجم يُفحص قبل الشمّ وقبل أي تخصيص**، ويُرفض بـ413 بجسم مشكلة بالعربية والإنجليزية — لا باستثناء ولا باتصالٍ يُقطع.
 * 
 * Deposits attachment bytes and returns its descriptor: identifier, digest, length, and the **sniffed** type.
 * 
 * **The payload is multipart/form-data, not JSON**: a JSON body means text-encoding the bytes — a third larger, a whole image in the request log, and a payload no proxy can stream past.
 * 
 * **The type comes from the bytes, not from the name and not from the header.** Only magic numbers are read, and anything unrecognised is **refused** with 415 rather than stored as a neutral type — a neutral type is handed to a browser tomorrow with a header its reader invents. A declaration that contradicts the sniffed type is a **named refusal, not a silent correction**: silent correction makes the client believe what it sent was accepted as sent.
 * 
 * **A file name is data, never a path.** It takes no part in building any path — the object key is generated by the store from 256 encrypted-random bits — and it is sanitised by an **allow** list, not a deny list: path separators, control characters, and invisible directional characters are dropped (U+202E turns gpj.exe into exe.jpg on screen). An Arabic name stays fully Arabic, and a name from which not one acceptable character survives is refused with 400.
 * 
 * **Size is checked before sniffing and before any allocation**, and refused with 413 and a bilingual problem body — never an exception and never a dropped connection.
 */
export async function depositAttachment(transport: Transport, args: DepositAttachmentArgs, signal?: AbortSignal): Promise<T.Attachment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/attachments";
  const url = path;
  const body = args.body;
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Attachment", response.json) as T.Attachment;
}

export interface DepositPayrollSettingsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrPayrollSettingsRequest;
}

/**
 * إيداع إصدار من نِسَب التأمينات وحدودها / Deposit a version of the social insurance rates and limits
 * 
 * **هذا هو الموضع الوحيد الذي تدخل منه نسبة إلى هذا النظام.** ولا نسبة اشتراك، ولا حدّ أجرٍ خاضع، ولا فرقٌ بين السعودي وغيره، ولا معادلة مكافأة، ولا مدّة إشعار مكتوبةٌ في شيفرة هذا المنتج ولا في اختبار من اختباراته — كلّها موسومة **غير متحقَّق منها** في البند م-14 من docs/evidence/verification-debt.md، والسبب مقيس: تسعة منافذ مصادر تنظيمية سعودية كلّها محجوبة.
 * 
 * **والجدول يُسلَّم فارغاً.** ومسيّرٌ لفترة لا يغطّيها صفٌّ سارٍ معتمد **يُرفض رفضاً صريحاً** بالرمز الثابت hr.payroll_settings_missing، برسالة تسمّي البند المعلَّق — **لا قيمة افتراضية واحدة، ولا صفر صامت**. ونسبةٌ تُكتب «مؤقّتاً» في اختبار تُنسخ إلى إنتاج بعد شهرين.
 * 
 * و**POST لا PUT**: نسبة فترةٍ ماضية لا تُعدَّل، والزيادة إصدارٌ جديد بتاريخ سريانه. وكل صفّ يحمل **معتمِده ومصدره**: sourceRef غير فارغ بقيدٍ في قاعدة البيانات، فنسبةٌ بلا مصدر مرفوضة عند الكتابة لا عند المراجعة.
 * 
 * **والنِّسَب TaxRate لا Money**: كسرٌ عشري بمقياس ثمانٍ — خمسة عشر بالمئة تُكتب 0.15 لا 15 — والحدود Money بمقياس أربع. وكلّها **نصوص** على السلك.
 * 
 * **This is the only place a rate enters this system.** No contribution rate, no contributory wage limit, no Saudi-versus-non-Saudi distinction, no benefit formula, and no notice period is written in this product's code or in any of its tests — all of them are tagged **unverified** under item م-14 in docs/evidence/verification-debt.md, and the reason is measured: nine Saudi regulatory source endpoints are all blocked.
 * 
 * **The table ships empty.** A run for a period not covered by an approved effective row is **explicitly refused** under the stable code hr.payroll_settings_missing, with a message naming the pending item — **not one default value, and no silent zero**. A rate written 'temporarily' in a test is copied into production two months later.
 * 
 * **POST, not PUT**: a past period's rate is never edited; an increase is a new version with its own effective date. Every row carries **its approver and its source**: sourceRef is non-empty by a database constraint, so a rate without a source is refused at write time rather than at review time.
 * 
 * **Rates are TaxRate, not Money**: a decimal fraction at scale eight — fifteen percent is 0.15, never 15 — while the limits are Money at scale four. All of them cross the wire as **strings**.
 */
export async function depositPayrollSettings(transport: Transport, args: DepositPayrollSettingsArgs, signal?: AbortSignal): Promise<T.HrPayrollSettings> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-settings";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrPayrollSettingsRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayrollSettings", response.json) as T.HrPayrollSettings;
}

export interface DownloadAttachmentArgs {
  /** معرّف المرفق — غامضٌ عمداً: لا يُشتقّ من اسم ملفّ ولا من مسار، ولا يُقرأ منه شيء عن صاحبه. / The attachment identifier — deliberately opaque: derived from no file name and no path, and telling nothing about its owner. */
  attachmentId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** التذكرة الموقّعة كما سكّها باب download-tickets. تُقدَّم كما وصلت ولا يُحلَّل منها شيء. / The signed ticket as the download-tickets door minted it. Presented exactly as received; nothing in it is parsed. */
  ticket: string;
}

/**
 * تنزيل بايتات مرفق بتذكرة / Download attachment bytes with a ticket
 * 
 * يُرجع بايتات المرفق **بعد أن يُعيد المخزن حساب البصمة ويقارنها بالمُسجَّلة**. ملفٌّ بُدِّل تحت المسار نفسه **يُرفض هنا ولا يُسلَّم**: مخزنٌ يسلّم ثم يخبرك أنها لا تطابق قد سلّمها بالفعل.
 * 
 * **والترتيب مُعلَن:** يُتحقَّق من توقيع التذكرة، ثم من انتهائها، ثم **يُقارَن مستأجرها بمستأجر الجلسة**، ثم يُنادى المخزن بمستأجر **الجلسة** لا بمستأجر التذكرة. فلو سُرّبت تذكرة كاملة واستُعملت في جلسة شركة أخرى سقطت عند المقارنة بـ**404 لا 403** — لأن 403 تُثبت وجود الملفّ؛ ولو سقطت المقارنة سهواً سقط النداء عند المخزن لأن المستأجر جزء من المفتاح هناك.
 * 
 * **وتذكرةٌ منتهية أو توقيعٌ لا يصحّ ⇒ 401**، كأي اعتماد مرفوض ولا كشف وجود فيه: الرمزان storage.ticket_expired و storage.ticket_signature_invalid لا يقولان شيئاً عن وجود المرفق.
 * 
 * **وترويسة Content-Type من النوع المشموم وحده** — لا من شيء أرسله العميل يوماً — وContent-Disposition بـattachment لا inline، والاسم فيها مرّتين: نسخة ASCII ونسخة UTF-8 مُرمَّزة بـRFC 5987 كي يبقى الاسم العربي عربياً.
 * 
 * Returns the attachment bytes **after the store recomputes the digest and compares it with the recorded one**. A file swapped under the same path is **refused here and never served**: a store that serves and then tells you it did not match has already served it.
 * 
 * **The order is declared:** the ticket signature is verified, then its expiry, then **its tenant is compared with the session's tenant**, and only then is the store called with the **session's** tenant rather than the ticket's. A whole ticket leaked and used in another company's session falls at that comparison with **404, not 403** — 403 would prove the file exists; and if that comparison were dropped by mistake, the call would still fall at the store, where the tenant is part of the key.
 * 
 * **An expired ticket or an invalid signature gives 401**, like any refused credential, and discloses nothing: the codes storage.ticket_expired and storage.ticket_signature_invalid say nothing about whether the attachment exists.
 * 
 * **The Content-Type header comes from the sniffed type alone** — never from anything a client ever sent — and Content-Disposition is attachment, not inline, carrying the name twice: an ASCII copy and an RFC 5987 UTF-8 copy so an Arabic name stays Arabic.
 */
export async function downloadAttachment(transport: Transport, args: DownloadAttachmentArgs, signal?: AbortSignal): Promise<Blob> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/attachments/" + encodeURIComponent(args.attachmentId) + "/content";
  const query = new URLSearchParams();
  query.set("ticket", args.ticket);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, binary: true, signal });
  if (!response.ok) throw ProblemError.from(response);
  if (!response.bytes) {
    throw new TypeError("استجابة ناجحة بلا بايتات · a successful response carried no bytes: " + url);
  }
  return response.bytes;
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

export interface DraftCustomerReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.CustomerReceiptRequest;
}

/**
 * تسجيل سند قبض مسوّدة / Draft a customer receipt
 * 
 * يسجّل سند قبض من عميل في حالة **DRAFT** بتخصيصاته على فواتير **مُرحَّلة**. ولا أثر على ذمّة العميل ولا على الفواتير قبل الترحيل: التخصيص يُنزَل مع القيد لا قبله، فمسوّدةٌ لم تُرحَّل لا تُنقص متبقّي فاتورة واحدة.
 * 
 * **والتخصيص الزائد مرفوض على الطرفين** برمز sales.over_allocation يُسمّي الرقمين: مجموع التخصيصات لا يتجاوز (received + settlementDiscount)، وتخصيص كل فاتورة لا يتجاوز المتبقّي عليها بعد ما سبق من تخصيصات ودفعات مقدّمة.
 * 
 * **ومقبوضٌ يتجاوز المستحقّ يُرفض ولا يصير دفعةً مقدّمة**: الدفعة المقدّمة **مستندٌ آخر وحدثٌ آخر في مصفوفة الترحيل** يُنشئ التزاماً على المنشأة بدل أن يُسقط ذمّةً لها، فتحويلُ الفائض إليها ضمناً كان سيرحّل حدثاً لم يطلبه أحد إلى حساب لم يقصده أحد. ونيّةُ المُرسِل لا تُخمَّن.
 * 
 * و**التصنيف الضريبي لا يظهر هنا**: القبض تسويةٌ لا توريد، ولا ضريبة على تحصيل دينٍ سبق أن فُوتر. أمّا خصم تعجيل السداد فمعالجته الضريبية **بند مفتوح في المصفوفة** ينتظر تأكيد المستشار الضريبي — وهو مسجَّل في دَين التحقّق ولا يُبنى عليه وعد.
 * 
 * Records a customer receipt in state **DRAFT** with its allocations against **posted** invoices. Nothing touches the customer's balance or the invoices before posting: allocations are applied with the entry, never before it, so an unposted draft reduces no invoice's outstanding amount.
 * 
 * **Over-allocation is refused on both sides** under sales.over_allocation, naming both numbers: the sum of allocations may not exceed (received + settlementDiscount), and each invoice's allocation may not exceed what remains outstanding on it after earlier allocations and advances.
 * 
 * **A receipt beyond what is owed is refused rather than turned into an advance**: a customer advance is a **different document and a different matrix event** that creates a liability on the company instead of clearing a receivable, so silently converting the excess would post an event nobody asked for to an account nobody intended. The sender's intent is never guessed.
 * 
 * **No tax classification appears here**: a collection is a settlement, not a supply, and there is no VAT on collecting a debt that was already invoiced. The VAT treatment of an early-settlement discount is an **open item in the matrix** awaiting a tax adviser's confirmation; it is recorded in the verification debt and no promise is built on it.
 */
export async function draftCustomerReceipt(transport: Transport, args: DraftCustomerReceiptArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/customer-receipts";
  const url = path;
  const body = encodeSchema(SCHEMAS, "CustomerReceiptRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface DraftEmployeeAdvanceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrAdvanceRequest;
}

/**
 * إنشاء سلفة موظف مسوّدة / Draft an employee advance
 * 
 * يُنشئ سلفة في حالة **DRAFT** بجدول أقساطها، ومجموعُ الأقساط يساوي المبلغ بالضبط — والفارق لا يُسوَّى ضمناً.
 * 
 * **ولاحظ ما ليس على هذا المورد اليوم: لا …/posting — والغياب مُعلَن لا مسكوتٌ عنه.** حدث صرف السلفة (hr.employee_advance.paid) **غير موجود في مصفوفة الترحيل**، والمحرك يرفض رمزاً لا يعرفه بـUnknownEvent ولا يخترع قالباً. وبابٌ يَعِد بدورة لا تكتمل أسوأ من غيابه — وهو المعيار نفسه الذي مُنع به مورد ترحيل أمر الشراء.
 * 
 * **وثمنُ ذلك عطلٌ محاسبي حقيقي يُقال صراحةً**: المقيس أن دور سلف الموظفين له سطرٌ دائن واحد في المصفوفة كلّها ولا سطر مدين واحد — فالسلفة **تُقسَّط ولا تُصرَف**، ورصيد حسابها يصير دائناً بالتصميم أي دَيناً يُسدَّد ولم يُنشأ. وهو صنف الانحراف الذي لا يُظهره ميزان مراجعة. والإضافة نفسها **بيانات لا شيفرة**، لكن قبول أي مدخل جديد في المصفوفة **مراجعةٌ بشرية إلزامية**، فرخصُ التغيير ليس ملكيةً للقرار.
 * 
 * Creates an advance in state **DRAFT** with its instalment schedule; the instalments sum exactly to the amount, and any difference is never settled implicitly.
 * 
 * **Note what this resource does not carry today: no …/posting — and the absence is declared, not silent.** The advance disbursement event (hr.employee_advance.paid) **does not exist in the posting matrix**, and the engine refuses an unknown code with UnknownEvent rather than inventing a template. A door promising a cycle that does not complete is worse than no door — the same criterion that withheld a posting resource from the purchase order.
 * 
 * **The price of that is a real accounting defect, stated plainly**: it is measured that the employee advances role has exactly one credit line in the whole matrix and not one debit line — so an advance is **instalmented but never disbursed**, and its account balance becomes credit by design, that is, a debt being repaid that was never created. That is the class of divergence a trial balance does not reveal. The addition itself is **data, not code**, but accepting any new matrix entry is **a mandatory human review**, so the cheapness of the change is not ownership of the decision.
 */
export async function draftEmployeeAdvance(transport: Transport, args: DraftEmployeeAdvanceArgs, signal?: AbortSignal): Promise<T.HrAdvance> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employee-advances";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrAdvanceRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrAdvance", response.json) as T.HrAdvance;
}

export interface DraftEndOfServiceProvisionArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrProvisionRequest;
}

/**
 * إنشاء استحقاق مخصص نهاية الخدمة مسوّدة / Draft an end-of-service provision accrual
 * 
 * يُنشئ مستند استحقاق في حالة **DRAFT** بحصّةٍ لكل علاقة عمل.
 * 
 * **والوحدة لا تقيس المخصص ولا تعرف معادلته.** طريقة القياس ومدخلاتها — وأيّ أجرٍ يدخل الوعاء، وكيف تُعامَل الاستقالة مقابل الإنهاء، وهل يُخصم زمنياً — كلّها **تحتاج اعتماد المحاسب القانوني**، ونصّ المصفوفة على المبلغ صريح: «بطريقة القياس المعتمدة — لا تُخترع في هذا التسليم». فالمبلغ يصل من معتمِد المستند ومعه measurementRef يسمّي أساسه، **والمرجع غير فارغ بقيدٍ في قاعدة البيانات**: مبلغٌ بلا أساسٍ مكتوب تقديرٌ بلا مصدر.
 * 
 * **ومستندٌ يُنشئه نداءٌ صريح لا مهمّة مجدولة.** لا مُشغّل دوري ولا جدول عمل ولا مجدوِل في هذه الوحدة: النمط محجوزٌ للانتزاع من وحدة الالتزام ولا يُخترع مرّتين (ADR-0048 §2.3)، ومَن يبني للرواتب مُشغّلاً خاصاً يخلق مصدر حقيقة ثانياً. **وثمنُ ذلك مُعلَن**: نسيان الاستحقاق شهراً لا يُصدر خطأً ولا سطر سجل، ويظهر عند أول مخالصة بعجزٍ يُحمَّل كاملاً على مصروف شهرٍ واحد.
 * 
 * Creates an accrual document in state **DRAFT** with one share per employment.
 * 
 * **The module does not measure the provision and does not know its formula.** The measurement method and its inputs — which pay enters the base, how resignation is treated against dismissal, whether it is discounted — all **require a chartered accountant's approval**, and the matrix text on the amount is explicit: 'by the approved measurement method — not invented in this deliverable'. So the amount arrives from the document's approver together with a measurementRef naming its basis, **and the reference is non-empty by a database constraint**: an amount with no written basis is an estimate with no source.
 * 
 * **And it is a document created by an explicit call, not a scheduled job.** There is no periodic runner, no work queue, and no scheduler in this module: the pattern is reserved for extraction from the compliance module and is not invented twice (ADR-0048 §2.3), and building a payroll-specific runner would create a second source of truth. **The price is declared**: forgetting the accrual for a month raises no error and writes no log line, and surfaces at the first settlement as a shortfall charged wholly to one month's expense.
 */
export async function draftEndOfServiceProvision(transport: Transport, args: DraftEndOfServiceProvisionArgs, signal?: AbortSignal): Promise<T.HrProvision> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/end-of-service-provisions";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrProvisionRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrProvision", response.json) as T.HrProvision;
}

export interface DraftEndOfServiceSettlementArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrSettlementRequest;
}

/**
 * إنشاء مخالصة نهاية خدمة مسوّدة / Draft an end-of-service settlement
 * 
 * يُنشئ مخالصة في حالة **DRAFT** على علاقة عمل **منتهية**، **والسيناريو المنطبق مُسمّى في الجواب** (exact · short · excess) لا مستنتَجاً من فرق مبلغين عند القارئ.
 * 
 * **والمستحقّ يصل من معتمِد المستند**: معادلة المكافأة وشرائحها **غير متحقَّق منها** ولا تُخترع هنا. وما تحسبه الوحدة من عندها هو **رصيد المخصص** وحده — مجموع حركات هذه العلاقة المُرحَّلة ناقص ما استُنفد في مخالصات مُرحَّلة — ثم العجز والزيادة والمخصص المستنفَد، وكلّها اشتقاقٌ حسابي من رقمين لا اجتهادٌ محاسبي.
 * 
 * والمتطابقة المعلَنة في المصفوفة مفروضة **في قاعدة البيانات**: provision_utilised = amount_paid − shortfall + excess.
 * 
 * Creates a settlement in state **DRAFT** against a **terminated** employment, **with the applicable scenario named in the response** (exact, short, excess) rather than inferred by the reader from the difference of two amounts.
 * 
 * **The amount due arrives from the document's approver**: the benefit formula and its bands are **unverified** and are not invented here. What the module computes itself is **the provision balance** alone — the sum of this employment's posted movements less what posted settlements have consumed — and then the shortfall, the excess, and the provision utilised, all of which are arithmetic derivations from two numbers, not accounting judgement.
 * 
 * The identity declared in the matrix is enforced **in the database**: provision_utilised = amount_paid − shortfall + excess.
 */
export async function draftEndOfServiceSettlement(transport: Transport, args: DraftEndOfServiceSettlementArgs, signal?: AbortSignal): Promise<T.HrSettlement> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/end-of-service-settlements";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrSettlementRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrSettlement", response.json) as T.HrSettlement;
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

export interface DraftGoodsReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.GoodsReceiptRequest;
}

/**
 * تسجيل استلام بضاعة مسوّدة / Draft a goods receipt
 * 
 * يسجّل استلام بضاعة على أمر شراء في حالة **DRAFT**. و**الضلع الأول من المطابقة الثلاثية**: كمية مستلمة تتجاوز المطلوب على سطر الأمر تُرفض **هنا** لا عند الفاتورة، برمز purchasing.receipt_exceeds_order يُسمّي الصنف والرقمين.
 * 
 * وتكلفة كل سطر تُحسب في الوحدة **بسعر أمر الشراء للكمية المستلمة فعلاً** — لا يرسلها العميل، فمبلغٌ يرسله المستدعي كان سيصير مصدر حقيقة ثانياً يستطيع أن ينحرف عن الأمر.
 * 
 * ولا مخزون ولا قيد قبل الترحيل: المسوّدة تحجز الكمية على سطر الأمر ولا تُحرّك رصيد صنف.
 * 
 * Records a goods receipt against a purchase order in state **DRAFT**. It is the **first leg of the three-way match**: a received quantity beyond what remains on the order line is refused **here**, not at the invoice, under purchasing.receipt_exceeds_order, naming the item and both numbers.
 * 
 * Each line's cost is computed in the module **at the purchase-order price for the quantity actually received** — the client never sends it, since an amount sent by the caller would be a second source of truth able to diverge from the order.
 * 
 * No stock and no entry before posting: the draft consumes quantity on the order line and moves no item balance.
 */
export async function draftGoodsReceipt(transport: Transport, args: DraftGoodsReceiptArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/goods-receipts";
  const url = path;
  const body = encodeSchema(SCHEMAS, "GoodsReceiptRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface DraftLeaseContractArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.LeaseRequest;
}

/**
 * إنشاء عقد إيجار مسوّدة / Draft a lease contract
 * 
 * يُنشئ عقد إيجار في حالة **DRAFT**.
 * 
 * **ولاحظ ما ليس على هذا المورد ولا يجوز أن يوجد: لا مورد posting.** حدث توقيع العقد مُعلَنٌ في مصفوفة الترحيل بأنه **لا يُنشئ قيداً**: العقد التزام متبادل مستقبلي لم ينفّذه أي طرف بعد. وغيابُ الباب هو ما يجعل «العقد لا يُرحّل» مقروءاً من شكل السطح لا من تعليق.
 * 
 * **والأقساط تصل مصرَّحاً بها ولا تُوزَّع من قيمة العقد**: التوزيع يستلزم سياسة تقريب — أين يقع فائض الهللات — وهي **قرار مالك مفتوح** لا يُحسم في شيفرة. والنظام يفحص عند التفعيل أن مجموع الأقساط يساوي قيمة العقد بالضبط، ويرفض بخلاف ذلك برمزٍ يسمّي البند المعلَّق.
 * 
 * Creates a lease contract in state **DRAFT**.
 * 
 * **Note what this resource does not carry and must never carry: no posting sub-resource.** The lease-signature event is declared in the posting matrix as posting **no entry**: the contract is a future mutual obligation neither party has yet performed. The absence of the door is what makes 'a lease posts nothing' readable from the shape of the surface rather than from a comment.
 * 
 * **Instalments arrive declared and are never spread from a contract value**: spreading requires a rounding policy — where the halala surplus lands — which is an **open owner decision**, not something settled in code. On activation the system checks that the instalments sum exactly to the contract value and refuses otherwise under a code that names the pending item.
 */
export async function draftLeaseContract(transport: Transport, args: DraftLeaseContractArgs, signal?: AbortSignal): Promise<T.Lease> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/lease-contracts";
  const url = path;
  const body = encodeSchema(SCHEMAS, "LeaseRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Lease", response.json) as T.Lease;
}

export interface DraftPayrollPaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrPayrollPaymentRequest;
}

/**
 * إنشاء سند صرف رواتب مسوّدة / Draft a payroll payment
 * 
 * يُنشئ سند صرف في حالة **DRAFT** على مسيّر **مُرحَّل**، بسطرٍ لكل قسيمة.
 * 
 * **وحقلا settlementMethod و treasuryPartyId إلزاميان.** وسطر التسوية معلَنٌ في المصفوفة subledger: "resolved"، **والمحرك يطويه إلى النوع none** ثم يبحث عن الواقعة subledger.none — والاسم مضلّل. وحسابُ التسوية الافتراضي حسابٌ **ضابط**، فبلا طرف الخزينة يُرفض كل نداء بـledger.posting.missing_subledger عند المحرك بعد أن يُكتب صفّ محاولة.
 * 
 * **وطرق التسوية المقبولة هنا ضيّقة عمداً**: نقداً أو تحويلاً مصرفياً. والمؤهّل in_transit موجودٌ في خريطة الأدوار ويقصد حساباً وسيطاً، **وقبولُه يفترض جواب سؤال مفتوح على المالك**: متى يقع قيد صرف الرواتب بالضبط — عند توليد ملفّ حماية الأجور أم عند تأكيد المصرف؟ ونصّ المصفوفة نفسه يحمل «أو» صريحة. فيُفتح الثالث حين يُغلق البند.
 * 
 * Creates a payment in state **DRAFT** against a **posted** run, with one line per payslip.
 * 
 * **settlementMethod and treasuryPartyId are both mandatory.** The settlement line is declared in the matrix as subledger: "resolved", **and the engine folds that to the kind none** and then looks for the subledger.none fact — the name is misleading. The default settlement account is a **control** account, so without the treasury party every call is refused with ledger.posting.missing_subledger at the engine, after an attempt row is written.
 * 
 * **The accepted settlement methods here are deliberately narrow**: cash or bank transfer. The in_transit qualifier exists in the role map and means a clearing account, **and accepting it would assume the answer to an open owner question**: when exactly does the payroll payment entry fall — at wage protection file generation, or at the bank's confirmation? The matrix text itself carries an explicit 'or'. The third is opened when that item closes.
 */
export async function draftPayrollPayment(transport: Transport, args: DraftPayrollPaymentArgs, signal?: AbortSignal): Promise<T.HrPayrollPayment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-payments";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrPayrollPaymentRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayrollPayment", response.json) as T.HrPayrollPayment;
}

export interface DraftPayrollRunArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrPayrollRunRequest;
}

/**
 * إنشاء مسيّر رواتب مسوّدة / Draft a payroll run
 * 
 * يبني مسيّراً في حالة **DRAFT** بقسيمة لكل علاقة عمل سارية في الفترة. **والوحدة تحسب، ولا مجاميع في الطلب**: مجموعٌ يرسله العميل مصدرُ حقيقةٍ ثانٍ ينحرف عن الأول ولا يُظهره شيء.
 * 
 * والمبالغ الستّة كلّها لها حاسبٌ في الوحدة، والمتطابقة المعلَنة في المصفوفة مفروضة **في قاعدة البيانات** لا في الشيفرة وحدها: net_payable = gross_entitlements − employee_social_insurance − advance_installment − deductions.
 * 
 * **ويُرفض المسيّر رفضاً صريحاً إن لم يوجد صفّ نِسَبٍ معتمد يغطّي الفترة** — hr.payroll_settings_missing — ولا قيمة افتراضية واحدة تُخترع لتمريره.
 * 
 * ووعاء الاشتراك يُشتقّ من **مكوّنات القسيمة الموسومة** ثم يُقصّ بحدَّي الصفّ المعتمد؛ ولا وسم يُملأ هنا ولا حدّ يُخترع.
 * 
 * **ولا يُسمح بمسيّر ثانٍ للفترة الواحدة اليوم**، والمنع في خدمة التطبيق لا في فهرس: «هل يُسمح بأكثر من مسيّر مُرحَّل للفترة؟» سؤالٌ مفتوح على المالك (خارج الدورة · مكافآت · دفعة تصحيحية)، وفهرسٌ اليوم بشرط نوع المسيّر **يفترض جوابه في مفتاح** على جدول لا يُحذف منه شيء.
 * 
 * Builds a run in state **DRAFT** with a payslip for every employment active in the period. **The module computes; no totals appear in the request**: a total sent by a client is a second source of truth that drifts from the first with nothing to reveal it.
 * 
 * All six amounts have a calculator in the module, and the identity declared in the matrix is enforced **in the database**, not in code alone: net_payable = gross_entitlements − employee_social_insurance − advance_installment − deductions.
 * 
 * **The run is explicitly refused when no approved rate row covers the period** — hr.payroll_settings_missing — and not one default value is invented to let it through.
 * 
 * The contributory wage is derived from the **flagged payslip components** and then clamped by the approved row's floor and ceiling; no flag is filled here and no limit is invented.
 * 
 * **A second run for one period is not allowed today**, and the prohibition lives in the application service rather than an index: whether more than one posted run per period is permitted (off-cycle, bonus, corrective) is an open owner question, and an index today conditioned on a run kind **assumes its answer in a key** on a table nothing is deleted from.
 */
export async function draftPayrollRun(transport: Transport, args: DraftPayrollRunArgs, signal?: AbortSignal): Promise<T.HrPayrollRun> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-runs";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrPayrollRunRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayrollRun", response.json) as T.HrPayrollRun;
}

export interface DraftPurchaseReturnArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.PurchaseReturnRequest;
}

/**
 * إنشاء مرتجع مشتريات مسوّدة / Draft a purchase return
 * 
 * يُنشئ **مرتجع مشتريات** (إشعاراً مديناً) في حالة DRAFT على فاتورة مخزنية **مُرحَّلة**.
 * 
 * **ولاحظ ما ليس في الحمولة: صافي المرتجع.** مصفوفة الترحيل تقول على purchasing.debit_note.posted إن الصافي «بتكلفة الاستلام الأصلي لا بتكلفة اليوم»، وتلك التكلفة يملكها دفتر المخزون وحده. فالطلب يحمل **الكمّية** ومعرّف سطر الاستلام، ويُحسب المبلغ لحظة الترحيل ولا يُملى — وهو مبدأ ADR-0039 نفسه مطبَّقاً على الطرف الآخر من الدورة. ولذلك تخرج المسوّدة بصافٍ صفر: الرقم لم يُحسب بعد، ولا يُخترَع ليملأ خانة.
 * 
 * والضريبة **تُسلَّم**: هي بتصنيف الفاتورة الأصلية وواقعةٌ تجارية لا يملكها المخزون.
 * 
 * Creates a **purchase return** (a supplier debit note) in state DRAFT against a **posted** stock bill.
 * 
 * **Note what the payload does not carry: the return net.** The posting matrix says of purchasing.debit_note.posted that the net is 'at the original receipt cost, not today's cost', and only the inventory subledger owns that cost. So the request carries the **quantity** and the goods receipt line identifier, and the amount is computed at posting time rather than dictated — the same principle as ADR-0039, applied to the other end of the cycle. That is why the draft comes back with a net of zero: the number has not been computed yet, and nothing is invented to fill the field.
 * 
 * The tax **is** supplied: it follows the original invoice's classification and is a commercial fact the inventory module does not own.
 */
export async function draftPurchaseReturn(transport: Transport, args: DraftPurchaseReturnArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/purchase-returns";
  const url = path;
  const body = encodeSchema(SCHEMAS, "PurchaseReturnRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface DraftRentInvoiceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.RentInvoiceRequest;
}

/**
 * إنشاء فاتورة إيجار مسوّدة / Draft a rent invoice
 * 
 * يُنشئ فاتورة إيجار في حالة **DRAFT** على أقساط مُسمّاة من جدول دفعات عقدٍ سارٍ.
 * 
 * **ولا رمز حدث في الحمولة ولا نموذج ملكية**: الوحدة تقرأ نموذج ملكية العقار **المُسجَّل في الدفتر** وتختار الحدث منه — فلا يستطيع عميل HTTP أن يطلب «فاتورة ملكية ذاتية» على عقارٍ مُدار. والفرق يظهر في **دائن الفاتورة**: إيرادُ إيجار مؤجَّل للشركة في الملكية الذاتية، وأماناتُ مالكٍ في الإدارة.
 * 
 * **وtaxRate تصل مع الطلب ولا تُكتب في شيفرة** (لا نسبة نظامية في هذا المنتج)، ولا تُطبَّق إلا على وحدةٍ معاملتها standard. وعلى الوحدة المعفاة يبقى exemptionReasonCode **فارغاً بعلامة ظاهرة** حتى يُعرف الرمز من القائمة الرسمية السارية — وحقلٌ إلزامي بقيمة مُختلَقة أسوأ من حقلٍ فارغ.
 * 
 * **والقسط لا يُفوتَر مرّتين**: فهرس فريد على (المستأجر، القسط) لا فحصٌ في الخدمة.
 * 
 * Creates a rent invoice in state **DRAFT** over named instalments from an active lease's payment schedule.
 * 
 * **No event code and no ownership model appear in the payload**: the module reads the property's ownership model **as registered in the ledger** and selects the event from it — so an HTTP client cannot request an 'own-property invoice' against a managed property. The difference shows in the **credit of the invoice**: deferred rental income of the company under own property, and owner trust under management.
 * 
 * **taxRate arrives with the request and is never written in code** (no regulatory rate lives in this product), and it applies only to a unit whose treatment is standard. On an exempt unit exemptionReasonCode stays **empty with a visible flag** until the code is known from the official list in force — a mandatory field holding an invented value is worse than an empty one.
 * 
 * **An instalment is never invoiced twice**: a unique index on (tenant, instalment), not a check in the service.
 */
export async function draftRentInvoice(transport: Transport, args: DraftRentInvoiceArgs, signal?: AbortSignal): Promise<T.RentInvoice> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/rent-invoices";
  const url = path;
  const body = encodeSchema(SCHEMAS, "RentInvoiceRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RentInvoice", response.json) as T.RentInvoice;
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

export interface DraftSocialInsurancePaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrSocialInsurancePaymentRequest;
}

/**
 * إنشاء سند سداد تأمينات مسوّدة / Draft a social insurance payment
 * 
 * يُنشئ سند سداد اشتراك التأمينات لفترة، في حالة **DRAFT**.
 * 
 * **والمبلغ يصل من المستدعي ولا تُمليه الوحدة**: فاتورة الجهة قد تخالف ما استحقّته المسيّرات لأسباب مشروعة. والقراءة تُرجع accruedForPeriod إلى جانبه **للمقارنة لا للإملاء**، فيُرى الفارق قبل الاعتماد بدل أن يُكتشف عند التسوية.
 * 
 * **والرواتب تُرحَّل بالريال السعودي حصراً** — قيدٌ مفروض بحكم البيانات لا باختيار: حساب التأمينات المستحقة معلَنٌ في دليل الحسابات currency_mode=company_only بعملة SAR، فأي عملة أخرى يرفضها المخطِّط بـledger.posting.currency_not_allowed. ويُقال هنا كي لا يُكتشف عند أول نداء.
 * 
 * Creates a social insurance settlement document for a period, in state **DRAFT**.
 * 
 * **The amount comes from the caller; the module does not dictate it**: the authority's invoice may legitimately differ from what the runs accrued. The read returns accruedForPeriod beside it **for comparison, not for dictation**, so the difference is seen before approval rather than discovered at reconciliation.
 * 
 * **Payroll posts in Saudi riyals only** — a constraint imposed by the data rather than chosen: the social insurance payable account is declared in the chart with currency_mode=company_only and currency SAR, so any other currency is refused by the planner with ledger.posting.currency_not_allowed. It is said here so that it is not discovered at the first call.
 */
export async function draftSocialInsurancePayment(transport: Transport, args: DraftSocialInsurancePaymentArgs, signal?: AbortSignal): Promise<T.HrSocialInsurancePayment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/social-insurance-payments";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrSocialInsurancePaymentRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrSocialInsurancePayment", response.json) as T.HrSocialInsurancePayment;
}

export interface DraftStockBillArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.StockBillRequest;
}

/**
 * إنشاء فاتورة مورد مخزنية مسوّدة / Draft a stock supplier bill
 * 
 * يُنشئ فاتورة مورد **مخزنية** في حالة DRAFT — الضلع الثالث: ما طولِبنا به. وكل سطر يرجع إلى سطر استلام بعينه، وكميةٌ مفوترة تتجاوز المستلَم غير المفوتَر **تُرفض**: من غير هذا الضلع تُدفَع بضاعة لم تصل، ولا يُكتشف ذلك إلا في الجرد السنوي.
 * 
 * **وتُقرأ وتُرحَّل من مورد فاتورة المورد نفسه** — /supplier-bills/{billId} و…/posting: مستندٌ واحد وعنوانٌ واحد. وعنوانان يقرآن الصفّ نفسه كانا سيجعلان «أيّهما الصحيح؟» سؤالاً يُطرح على كل عميل.
 * 
 * Creates a **stock** supplier bill in state DRAFT — the third side: what we were billed for. Each line refers to a specific goods receipt line, and a billed quantity beyond the received-but-unbilled remainder is **refused**: without this side, goods that never arrived get paid for and nobody finds out until the annual count.
 * 
 * **It is read and posted through the supplier bill resource** — /supplier-bills/{billId} and …/posting: one document, one address. Two addresses onto the same row would make 'which one is right?' a question every client has to ask.
 */
export async function draftStockBill(transport: Transport, args: DraftStockBillArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/stock-bills";
  const url = path;
  const body = encodeSchema(SCHEMAS, "StockBillRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface DraftStockMovementArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.StockMovementRequest;
}

/**
 * إنشاء حركة مخزون مسوّدة / Draft a stock movement
 * 
 * يُنشئ مستند حركة مخزون في حالة **DRAFT**: تسوية جرد، أو رصيد افتتاحي، أو إعدام. ولا حركة ولا قيد: الترحيل مورد فرعي مستقلّ.
 * 
 * **ولاحظ ما ليس من اختصاص هذا المورد: استلام المشتريات وصرف المبيعات.** تلك مستنداتٌ في وحدتيهما وحركتُها أثرٌ لها، وبابٌ ثانٍ لها هنا كان سيكتب الحركة مرّتين بهويتين مختلفتين — وهو انحراف لا يُظهره توازن.
 * 
 * **والكمّية تحمل وحدتها دائماً**، والتكلفة **على الوارد وحده**: الصادر تُحسب تكلفته في وحدة المخزون بالمتوسط المرجّح المتحرّك ولا تُملى (ADR-0039)، فتُرسَل عليه "0".
 * 
 * والحدث في المصفوفة inventory.count_adjustment.posted بسيناريوَيه — عجزٌ وزيادة — وهما بالضبط اتجاها هذا المستند. ولا حدث جديد اختُرع.
 * 
 * Creates a stock movement document in state **DRAFT**: a count adjustment, an opening balance, or a write-off. No movement and no entry: posting is a separate sub-resource.
 * 
 * **Note what this resource is not for: purchase receipts and sales issues.** Those are documents in their own modules and their stock movement is an effect of them; a second door here would write the movement twice under two identities — a divergence no balance check reveals.
 * 
 * **A quantity always carries its unit**, and cost is **for inbound only**: an outbound movement is valued by the inventory module at the moving weighted average and is never dictated (ADR-0039), so send "0" for it.
 * 
 * The matrix event is inventory.count_adjustment.posted with its two scenarios — shortage and surplus — which are exactly this document's two directions. No new event was invented.
 */
export async function draftStockMovement(transport: Transport, args: DraftStockMovementArgs, signal?: AbortSignal): Promise<T.StockMovement> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/stock-movements";
  const url = path;
  const body = encodeSchema(SCHEMAS, "StockMovementRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "StockMovement", response.json) as T.StockMovement;
}

export interface DraftSupplierPaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.SupplierPaymentRequest;
}

/**
 * تسجيل سند صرف مسوّدة / Draft a supplier payment
 * 
 * يسجّل سند صرف لمورد في حالة **DRAFT** بتخصيصاته على فواتيره **المُرحَّلة**. ولا أثر على ذمّة المورد قبل الترحيل.
 * 
 * **ورسوم التحويل ليست ذمّة مورد**: السند يُخصم من الخزينة بـ(paid + bankFee) ويُنقص ذمّة المورد بـpaid وحده — والرسوم مصروف بنكي على المنشأة. وخلطهما يجعل رصيد المورد أقلّ ممّا هو، فتظهر مطالبةٌ لا يعرف أحد مصدرها بعد أشهر. ولذلك مجموع التخصيصات يُقاس على paid لا على مجموعهما.
 * 
 * والتخصيص الزائد مرفوض على الطرفين برمز purchasing.over_allocation يُسمّي الرقمين.
 * 
 * Records a supplier payment in state **DRAFT** with its allocations against that supplier's **posted** bills. Nothing touches the supplier's balance before posting.
 * 
 * **A transfer fee is not a supplier balance**: the payment takes (paid + bankFee) out of the treasury and reduces the supplier's balance by paid alone — the fee is a bank charge borne by the company. Mixing the two makes the supplier's balance smaller than it is, and a claim nobody can trace surfaces months later. That is why the sum of allocations is measured against paid, not against their total.
 * 
 * Over-allocation is refused on both sides under purchasing.over_allocation, naming both numbers.
 */
export async function draftSupplierPayment(transport: Transport, args: DraftSupplierPaymentArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/supplier-payments";
  const url = path;
  const body = encodeSchema(SCHEMAS, "SupplierPaymentRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface DraftTenantReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.TenantReceiptRequest;
}

/**
 * تسجيل سند قبض من مستأجر / Draft a tenant receipt
 * 
 * يُنشئ سند قبض في حالة **DRAFT**.
 * 
 * **وحدثان لا حدث واحد، والفارق مرجعٌ لا مبلغ:** سندٌ يحمل lesseeId يُرحَّل تحصيلاً يُسقط من ذمّة ذلك المستأجر؛ وسندٌ بلا lesseeId — مبلغٌ ورد في الحساب بلا مرجع يربطه بأحد — يُرحَّل إلى حساب **التحصيلات غير المخصَّصة**، ولا يُنسب إلى مستأجر بالتخمين. والاختيار من غياب الحقل أو حضوره، لا من حقلٍ يختاره العميل.
 * 
 * Creates a tenant receipt in state **DRAFT**.
 * 
 * **Two events, not one, and the difference is a reference not an amount:** a receipt carrying lesseeId posts a collection that reduces that tenant's receivable; a receipt without lesseeId — an amount that arrived in the account with no reference tying it to anyone — posts to the **unallocated collections** account and is never attributed to a tenant by guesswork. The choice comes from the absence or presence of the field, not from a field the client selects.
 */
export async function draftTenantReceipt(transport: Transport, args: DraftTenantReceiptArgs, signal?: AbortSignal): Promise<T.TenantReceipt> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/tenant-receipts";
  const url = path;
  const body = encodeSchema(SCHEMAS, "TenantReceiptRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "TenantReceipt", response.json) as T.TenantReceipt;
}

export interface GrantMembershipArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.GrantMembershipRequest;
}

/**
 * دعوة عضو إلى المنشأة / Invite a member into the company
 * 
 * يسكّ للمدعوّ **معرّف مستخدم**، ويمنحه دوره في هذه المنشأة، ويُصدر له **اعتماد انتساب يُسلَّم مرّة واحدة** يبدّله هو بجلسة عبر POST /api/v1/access/sessions. وهذا هو التسجيل: لا كلمة مرور تُخزَّن، ولا اعتماد قابل للاستعمال يُودَع في جدول.
 * 
 * **والدعوة فعلُ مالك:** من يستطيع أن يدعو يستطيع أن يمنح نفسه ما شاء عبر عضوٍ يدعوه، فالحدّ عند الدعوة لا عند ما بعدها — وغيرُ المالك يُرفض بـ403 وmembership.inviter_is_not_an_owner.
 * 
 * **والدور ليس زينة:** جلسةُ Reader تُرفض على كل فعلٍ غير آمن في منشأتها بـ403 وmembership.read_only — وهو رمز يفترق عن entitlement.read_only عمداً: الأول يقول «اطلب صلاحية» والثاني يقول «جدّد اشتراكك»، وخلطهما يجعل قارئاً يتّصل بالمحاسبة بلا سبب.
 * 
 * ودعوةٌ ثانية لعضوٍ قائم تُرفض بـ409 وmembership.already_granted: تغييرُ دورٍ فعلٌ آخر يُطلب باسمه، لا دعوةٌ تُنتج اعتماد انتساب جديداً لمن يملك جلسة.
 * 
 * Mints the invited person a **user identifier**, grants their role in this company, and issues them an **enrolment credential handed over exactly once**, which they exchange for a session at POST /api/v1/access/sessions. This is registration: no password is stored, and no usable credential is ever written to a table.
 * 
 * **Inviting is an owner's act:** whoever can invite can grant themselves anything through the member they invite, so the limit sits at the invitation rather than after it — a non-owner is refused with 403 and membership.inviter_is_not_an_owner.
 * 
 * **The role is not decoration:** a Reader's session is refused on every unsafe method in its company with 403 and membership.read_only — a code deliberately distinct from entitlement.read_only: the first says 'ask for permission', the second says 'renew your subscription', and conflating them has a reader calling accounts payable for no reason.
 * 
 * A second invitation for an existing member is refused with 409 and membership.already_granted: changing a role is a different act asked for by its own name, not an invitation minting a fresh enrolment credential for someone who already holds a session.
 */
export async function grantMembership(transport: Transport, args: GrantMembershipArgs, signal?: AbortSignal): Promise<T.GrantedMembership> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/memberships";
  const url = path;
  const body = encodeSchema(SCHEMAS, "GrantMembershipRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "GrantedMembership", response.json) as T.GrantedMembership;
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

export interface IssueAttachmentDownloadTicketArgs {
  /** معرّف المرفق — غامضٌ عمداً: لا يُشتقّ من اسم ملفّ ولا من مسار، ولا يُقرأ منه شيء عن صاحبه. / The attachment identifier — deliberately opaque: derived from no file name and no path, and telling nothing about its owner. */
  attachmentId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.IssueAttachmentTicketRequest;
}

/**
 * سكّ تذكرة تنزيل / Mint a download ticket
 * 
 * يسكّ تذكرة موقّعة قصيرة الأجل تفتح بايتات مرفق واحد. **وهي ما يُعطى للمتصفّح، لا المسار ولا المعرّف.**
 * 
 * والتذكرة تحمل المستأجر والمرفق والحامل ولحظة الانتهاء، وعليها جميعاً HMAC-SHA256؛ **والحقول داخل البايتات الموقّعة لا بجانبها** — حقلٌ خارج التوقيع يُبدَّل بلا أن يبطل التوقيع. وقلبُ خانةٍ واحدة في أي بايت منها يُبطلها.
 * 
 * **وما لا تفعله، مُعلَناً: لا تُبطَل قبل انتهائها** — لا قائمة إبطال ولا حالة في القاعدة. وذلك ثمن كونها بلا حالة، ولذلك السقف الافتراضي **خمس دقائق**: نافذةُ ضررٍ تُقاس بالدقائق لا بالساعات. وعمرٌ يتجاوز السقف **يُرفض ولا يُقصّ**: القصّ الصامت يجعل المستدعي يظنّ أنه أصدر ساعةً وقد أصدر خمس دقائق.
 * 
 * **والوجود يُتحقَّق منه أولاً داخل الشركة**: تذكرةٌ تُسَكّ لمعرّفٍ لا وجود له كانت ستُنتج بابين يقولان قولين — سكٌّ ناجح ثم تنزيلٌ يردّ 404 — فيتعلّم السائل من الفرق بينهما شيئاً عن شركة أخرى.
 * 
 * Mints a short-lived signed ticket that opens the bytes of one attachment. **It is what a browser is given — not the path and not the identifier.**
 * 
 * The ticket carries the tenant, the attachment, the bearer, and the expiry instant, all under one HMAC-SHA256; **the fields are inside the signed bytes, not beside them** — a field outside the signature is changed without invalidating it. Flipping a single bit anywhere in it invalidates it.
 * 
 * **What it does not do, declared: it is never revoked before it expires** — no revocation list and no state in the database. That is the price of being stateless, and it is why the default cap is **five minutes**: a damage window measured in minutes, not hours. A lifetime beyond the cap is **refused, not truncated**: silent truncation makes the caller believe it issued an hour when it issued five minutes.
 * 
 * **Existence is checked first, within the company**: minting a ticket for an identifier that does not exist would make two doors say two things — a successful mint and then a 404 download — and the difference teaches the asker something about another company.
 */
export async function issueAttachmentDownloadTicket(transport: Transport, args: IssueAttachmentDownloadTicketArgs, signal?: AbortSignal): Promise<T.AttachmentTicket> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/attachments/" + encodeURIComponent(args.attachmentId) + "/download-tickets";
  const url = path;
  const body = encodeSchema(SCHEMAS, "IssueAttachmentTicketRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "AttachmentTicket", response.json) as T.AttachmentTicket;
}

export interface LapseSubscriptionArgs {
  /** معرّف المستأجر. يُطابَق بمستأجر الاعتماد ويُرفض إن اختلف؛ ولا يُفرَّق في الرفض بين «لا وجود له» و«ليس مستأجرك». / The tenant identifier. It is matched against the credential's tenant and refused when it differs; the refusal does not distinguish 'does not exist' from 'not yours'. */
  tenantId: string;
  /** جسم الطلب. / The request body. */
  body: T.SubscriptionTransitionRequest;
}

/**
 * انقطاع الاشتراك / Lapse the subscription
 * 
 * يُنهي الاشتراك ويهبط بكل وحدة إلى **أرضيتها**. ولا يحجب قراءةً ولا يُنتزع سجلّاً: من انقطع اشتراكه **يدخل ويقرأ** — يفتح جلسته، ويقرأ ميزان المراجعة والتقارير، ويصدّر بياناته كاملةً — و**يُردّ عند أول كتابة** بـ403 وentitlement.read_only ورسالةٍ تُسمّي السبب بالعربية والإنجليزية.
 * 
 * **والحجّة ليست تجارية أولاً:** حفظ السجلات المحاسبية وإبرازها التزامٌ على المنشأة، ونزاعٌ تجاري بيننا وبين عميل لا يجوز أن يضعه في مخالفة نظامية.
 * 
 * **وهو فعل مشغِّل** يُطلب باعتماد التزويد وحده، وسندُه إلزامي.
 * 
 * Ends the subscription and drops every module to its **floor**. It blocks no reading and takes no record away: a lapsed tenant **signs in and reads** — opens a session, reads the trial balance and reports, and exports its own data in full — and is **refused at the first write** with 403, entitlement.read_only, and a message naming the cause in Arabic and English.
 * 
 * **The argument is not commercial first:** keeping and producing accounting records is an obligation on the company, and a commercial dispute between us and a customer must not put them in breach.
 * 
 * **It is an operator act** requested with the provisioning credential alone, and its authority is mandatory.
 */
export async function lapseSubscription(transport: Transport, args: LapseSubscriptionArgs, signal?: AbortSignal): Promise<T.Subscription> {
  const path = "/api/v1/tenants/" + encodeURIComponent(args.tenantId) + "/subscription/lapse";
  const url = path;
  const body = encodeSchema(SCHEMAS, "SubscriptionTransitionRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Subscription", response.json) as T.Subscription;
}

export interface ListAttachmentsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** عدد الصفوف المتخطّاة. الافتراضي صفر. / Rows skipped. Defaults to zero. */
  skip?: string;
  /** معرّف المستند المصدر — يُرسل مع نوعه أو لا يُرسل. / The source document identifier — sent with its type or not at all. */
  sourceDocumentId?: string;
  /** رمز نوع المستند المصدر — يُرسل مع معرّفه أو لا يُرسل. / The source document type code — sent with its identifier or not at all. */
  sourceDocumentType?: string;
  /** حجم الصفحة. الافتراضي خمسون والسقف مئة. / Page size. Defaults to fifty, capped at one hundred. */
  take?: string;
}

/**
 * جرد المرفقات / List attachments
 * 
 * يجرد مرفقات الشركة، مرشَّحةً على المستند المصدر ومصفَّحةً. **ولا بايتة تعبر منه**: الجرد وصفٌ لا محتوى.
 * 
 * **والترشيح بحقلين معاً أو بلا حقل**: نوعُ مستندٍ وحده يعني «كل مرفقات فواتير المبيعات في هذه الشركة»، وهو استعلامٌ لا يخدم شاشةً واحدة ويكلّف مسحاً — فيُرفض بـ400 ولا يُنفَّذ.
 * 
 * والترتيب **الأحدث أولاً** دائماً وصراحةً: صفحةٌ بلا ترتيب مُعلن تُعيد صفوفاً مكرّرة وأخرى مفقودة بين طلبين. وحجم الصفحة الافتراضي خمسون وسقفه مئة، وما تجاوزه **يُرفض ولا يُقصّ**: القصّ الصامت يجعل المستدعي يقرأ صفحةً واحدة ويحسب أن الجرد انتهى.
 * 
 * Lists the company's attachments, filtered by source document and paged. **No byte crosses it**: an inventory is description, not content.
 * 
 * **Filtering is by both fields together or by neither**: a document type alone means 'every attachment of every sales invoice in this company', a query that serves no screen and costs a scan — so it is refused with 400 and never run.
 * 
 * Ordering is **newest first**, always and explicitly: a page without a declared order returns duplicated and missing rows between two requests. The default page size is fifty and the cap is one hundred; beyond it the request is **refused, not truncated** — silent truncation makes the caller read one page and conclude the inventory ended.
 */
export async function listAttachments(transport: Transport, args: ListAttachmentsArgs, signal?: AbortSignal): Promise<T.AttachmentPage> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/attachments";
  const query = new URLSearchParams();
  if (args.skip !== undefined && args.skip !== null) query.set("skip", args.skip);
  if (args.sourceDocumentId !== undefined && args.sourceDocumentId !== null) query.set("sourceDocumentId", args.sourceDocumentId);
  if (args.sourceDocumentType !== undefined && args.sourceDocumentType !== null) query.set("sourceDocumentType", args.sourceDocumentType);
  if (args.take !== undefined && args.take !== null) query.set("take", args.take);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "AttachmentPage", response.json) as T.AttachmentPage;
}

export interface ListItemsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * قراءة الأصناف / List the items
 * 
 * يقرأ أصناف المنشأة مرتَّبةً بالرمز **ترتيباً حرفياً ثابتاً** — لا بترتيب الإدخال، ولا بترتيبٍ ثقافي يختلف بين tr-TR و en-US على الحروف نفسها. نقطة قراءة: تعمل والاشتراك للقراءة فقط.
 * 
 * Lists the company's items ordered by code in a **stable ordinal order** — not by insertion order, and not by a cultural order that differs between tr-TR and en-US on the same letters. A read point: it works while the subscription is read-only.
 */
export async function listItems(transport: Transport, args: ListItemsArgs, signal?: AbortSignal): Promise<T.ItemList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/items";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "ItemList", response.json) as T.ItemList;
}

export interface ListPayComponentsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * قراءة تصنيفات مكوّنات الأجر / Read the pay component classifications
 * 
 * يقرأ مكوّنات الأجر بوسومها. ومراجعٌ خارجي يجب أن يرى **على أي أساس** تكوّن وعاء الاشتراك، لا أن يُخبَر بالنتيجة وحدها.
 * 
 * Reads the pay components with their flags. An external reviewer must see **on what basis** the contributory wage was formed, not merely be told the result.
 */
export async function listPayComponents(transport: Transport, args: ListPayComponentsArgs, signal?: AbortSignal): Promise<T.HrPayComponentList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/pay-components";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayComponentList", response.json) as T.HrPayComponentList;
}

export interface ListPayElementsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف الموظف. وهو **غير** رمزه المعتم: الرمز هو ما يعبر إلى الدفتر المساعد، والمعرّف عنوانٌ على هذا السطح. / The employee identifier. It is **not** the opaque code: the code is what crosses into the subledger, while the identifier is an address on this surface. */
  employeeId: string;
}

/**
 * قراءة أجر الموظف بسريانه / Read the employee's pay by effective date
 * 
 * يقرأ **كل** صفوف أجر الموظف بتواريخ سريانها — لا الساري اليوم وحده. ومراجعةُ مسيّرٍ ماضٍ تحتاج الصفّ الذي كان سارياً حينها لا الصفّ الجاري.
 * 
 * Reads **all** of the employee's pay rows with their effective dates — not merely the one in force today. Reviewing a past run needs the row that was in force then, not the current one.
 */
export async function listPayElements(transport: Transport, args: ListPayElementsArgs, signal?: AbortSignal): Promise<T.HrPayElementList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employees/" + encodeURIComponent(args.employeeId) + "/pay-elements";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayElementList", response.json) as T.HrPayElementList;
}

export interface ListPayrollSettingsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * قراءة إصدارات النِّسَب بسريانها / Read the rate versions by effective date
 * 
 * يقرأ إصدارات النِّسَب كلّها بتواريخ سريانها ومعتمِديها ومصادرها. ومن لا يستطيع قراءة النسبة السارية لتاريخٍ لا يستطيع مراجعة مسيّرٍ رُحِّل به.
 * 
 * **وقائمة فارغة جوابٌ صحيح لا عطل**: هي حال المنشأة قبل أن يعتمد محاسبها أول إصدار، وهي الحال التي يُرفض فيها كل مسيّر.
 * 
 * Reads every rate version with its effective date, its approver, and its source. Whoever cannot read the rate in force on a date cannot review a run posted under it.
 * 
 * **An empty list is a correct answer, not a fault**: it is the company's state before its accountant approves a first version, and it is the state in which every run is refused.
 */
export async function listPayrollSettings(transport: Transport, args: ListPayrollSettingsArgs, signal?: AbortSignal): Promise<T.HrPayrollSettingsList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-settings";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayrollSettingsList", response.json) as T.HrPayrollSettingsList;
}

export interface ListPayslipsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مسيّر الرواتب. / The payroll run identifier. */
  runId: string;
}

/**
 * قراءة قسائم مسيّر بمعرّفاتها / Read a run's payslips with their identifiers
 * 
 * يقرأ قسائم المسيّر **بمعرّفاتها ومعرّفات قيودها**، وهي مدخل باب الدفع. وبلا نشر هذه المعرّفات يصير POST …/payroll-payments باباً لا يوصل إليه بابٌ آخر على هذا السطح — وهو الاعتراض الذي كتبه ADR-0044 بنصّه على مرتجع المشتريات.
 * 
 * Reads the run's payslips **with their identifiers and their entry identifiers**, which are the input to the payment door. Without publishing them, POST …/payroll-payments becomes a door no other door on this surface leads to — the objection ADR-0044 wrote verbatim about the purchase return.
 */
export async function listPayslips(transport: Transport, args: ListPayslipsArgs, signal?: AbortSignal): Promise<T.HrPayslipList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-runs/" + encodeURIComponent(args.runId) + "/payslips";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayslipList", response.json) as T.HrPayslipList;
}

export interface ListStockMovementsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * قراءة حركات المخزون / List the stock movements
 * 
 * يقرأ مستندات حركة المخزون مرتَّبةً بالتاريخ ثم بالرقم ترتيباً حرفياً ثابتاً. نقطة قراءة.
 * 
 * Lists the stock movement documents ordered by date then by number in a stable ordinal order. A read point.
 */
export async function listStockMovements(transport: Transport, args: ListStockMovementsArgs, signal?: AbortSignal): Promise<T.StockMovementList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/stock-movements";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "StockMovementList", response.json) as T.StockMovementList;
}

export interface OpenSessionArgs {
  /** جسم الطلب. / The request body. */
  body: T.OpenSessionRequest;
}

/**
 * فتح جلسة باعتماد انتساب / Open a session with an enrolment credential
 * 
 * يبدّل **اعتماد انتساب** — وهو ما يُسلَّم للمدعوّ مرّة واحدة عند دعوته — بجلسة كاملة: اعتماد فاعل قصير العمر، واعتماد تجديد يدور، ومعرّف عائلة هو **ما يُبطَل** لاحقاً.
 * 
 * **والاعتماد في الجسم لا في الترويسة، ولذلك هذا الباب بلا مصادقة:** من يطلب اعتماداً لا يملك اعتماداً، وبابٌ يُصدر جلسةً ويشترط جلسةً بابٌ لا يُفتح أبداً. ومع ذلك لا يخرج منه شيء لمن لا يقدّم انتساباً صحيحاً، والرفض 401 لا 403: الفرق بينهما هو الفرق بين «لم تُصادِق» و«صادقتَ ومُنعت» (RFC 9110 §15.5.4).
 * 
 * **واعتماد الانتساب يُقبل مرّة واحدة**، ذرّياً عند قاعدة البيانات لا بانضباط المستدعي: الوصول الثاني — ولو تزامن مع الأول — يُرفض بـaccess.enrolment_consumed. والرمز يفترق عن access.credential_rejected عمداً: «استُعملت دعوتك» جوابٌ يُخبر صاحبها أن شيئاً وقع فيسأل عنه، و«اعتماد غير مقبول» جوابٌ لا يتعلّم منه مختلِقٌ شيئاً.
 * 
 * **ولا يُخزَّن اعتماد قابل للاستعمال:** المُودَع بصمة SHA-256، والنصّان يخرجان في هذه الاستجابة وحدها ولا يُعادان. **والاستحقاق لا يمنع الدخول:** اشتراكٌ منقطع يُخفَّض إلى القراءة ولا يُنتزَع به السجلّ (ADR-0034)، ومن مُنع الدخول لا يستطيع أن يقرأ.
 * 
 * Exchanges an **enrolment credential** — handed to an invited member once, at invitation — for a whole session: a short-lived access credential, a rotating refresh credential, and a family identifier which is what gets revoked later.
 * 
 * **The credential travels in the body, not the header, and that is why this door is unauthenticated:** whoever asks for a credential has none, and a door that issues a session while demanding a session never opens. Even so nothing crosses it without a valid enrolment, and the refusal is 401 rather than 403: that distinction is the distinction between 'you did not authenticate' and 'you authenticated and were refused' (RFC 9110 §15.5.4).
 * 
 * **An enrolment is accepted exactly once**, atomically at the database rather than by a disciplined caller: a second arrival — even one concurrent with the first — is refused with access.enrolment_consumed. That code differs from access.credential_rejected on purpose: 'your invitation was already used' tells its owner something happened so they ask about it, while 'the credential was rejected' teaches a forger nothing.
 * 
 * **No usable credential is ever stored:** what is persisted is a SHA-256 digest; both texts leave the server in this response alone and are never re-issued. **And entitlement never blocks sign-in:** a lapsed subscription degrades to read-only and never strips the record (ADR-0034), and whoever cannot sign in cannot read.
 */
export async function openSession(transport: Transport, args: OpenSessionArgs, signal?: AbortSignal): Promise<T.AccessSession> {
  const path = "/api/v1/access/sessions";
  const url = path;
  const body = encodeSchema(SCHEMAS, "OpenSessionRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "AccessSession", response.json) as T.AccessSession;
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

export interface PostCustomerReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند القبض. / The customer receipt identifier. */
  receiptId: string;
}

/**
 * ترحيل سند قبض / Post a customer receipt
 * 
 * يرحّل سند قبض مسوّدة فتصير **واقعة محاسبية**: تُدين الخزينة أو البنك بالمقبوض، ويُدين حساب خصم التعجيل بالخصم إن وُجد، و**يُدان دائناً حساب مراقبة ذمم العملاء بمجموعهما** — أي أنّ المقبوض **يُسقط من ذمّة العميل**. ثم تُنزَل تخصيصاته على فواتيره فينقص متبقّي كلٍّ منها.
 * 
 * **وحصين ضد التكرار بهوية الترحيل** (شركة · نوع المستند المصدر · معرّف المستند · المُحفِّز · الجيل · رمز الحدث): الوصول الثاني بالهوية نفسها يُرجع السند ذاته وalreadyPosted = true ورمز 200 بدل 201 و**معرّف القيد نفسه**، ولا يُنشئ قيداً ثانياً — **ولا يُنزل التخصيص مرّة ثانية**. والثاني هو الأخطر: البوّابة تحرس القيد، وأثرُ التخصيص على الفواتير أثرٌ جانبي بعدها؛ ولو وقع بلا شرط لأُنقص متبقّي الفاتورة بضعف ما سُدِّد **بلا قيد ثانٍ يدلّ عليه**.
 * 
 * والحكم حكم بوّابة الوحدة لا مقارنةَ حالةٍ قُرئت قبل النداء: نداءان متزامنان يجتازان فحص «مسوّدة» معاً ويلتقيان عند الهوية الواحدة، فيكتب أحدهما ويعود الآخر موسوماً.
 * 
 * ولا جسم لهذا الطلب، ولا مفتاح حصانة يرسله العميل: تشتقّه الوحدة من هوية السند نفسه.
 * 
 * Posts a draft receipt, turning it into an **accounting fact**: the cash box or bank is debited with the amount collected, the settlement-discount account is debited with the discount if there is one, and **the accounts receivable control account is credited with their sum** — that is, the collection **comes off the customer's balance**. Its allocations are then applied to that customer's invoices, reducing what remains on each.
 * 
 * **Idempotent by the posting identity** (company, source document type, source document id, trigger, generation, event code): a second arrival with the same identity returns the same receipt with alreadyPosted = true, status 200 instead of 201, and **the same entry identifier**; it creates no second entry — **and applies no second allocation**. The second is the more dangerous: the gateway guards the entry, while applying allocations to invoices is a side effect after it; done unconditionally it would cut an invoice's outstanding amount by twice what was paid, **with no second entry to point at it**.
 * 
 * The verdict is the module gateway's, not a comparison against a state read before the call: two concurrent calls both pass the 'is it a draft' check, meet at the one identity, and one writes while the other returns marked.
 * 
 * This request has no body, and no idempotency key is sent by the client: the module derives it from the receipt's own identity.
 */
export async function postCustomerReceipt(transport: Transport, args: PostCustomerReceiptArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/customer-receipts/" + encodeURIComponent(args.receiptId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface PostEndOfServiceProvisionArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مستند استحقاق المخصص. / The provision accrual document identifier. */
  provisionId: string;
}

/**
 * ترحيل استحقاق المخصص / Post the provision accrual
 * 
 * يرحّل الاستحقاق: **قيدٌ لكل علاقة عمل**، لكلٍّ هويّته وطرفه ومركز تكلفته. وتغيير التقدير **قيدٌ مستقلّ لا تعديلٌ للسابق**، بنصّ المصفوفة.
 * 
 * **ورمز الإطلاق OnApproval لا Periodic — ويُقال ذلك صراحةً.** نصّ المصفوفة يصف الواقعة بأنها دورية، لكن القيمة Periodic في العقد **بلا كاتب واحد في الإنتاج** اليوم، ومنحُها كاتباً قبل أن يُغلق البند المُسجَّل عن حبيبيّة الاستحقاق ونمط الجدولة قراءةٌ يملكها المالك ومعمار المستودع لا هذا التسليم. فالمُطلِق هنا يصف **ما وقع فعلاً**: اعتمادُ إنسانٍ لمستند.
 * 
 * Posts the accrual: **one entry per employment**, each with its own identity, party, and cost centre. Changing the estimate is **a separate entry, never an edit of the previous one**, per the matrix text.
 * 
 * **The trigger is OnApproval, not Periodic — and this is said plainly.** The matrix describes the occurrence as periodic, but the Periodic value in the contract has **not one production writer** today, and giving it one before the recorded item on accrual grain and scheduling pattern closes is a reading owned by the owner and the repository's architecture, not by this deliverable. So the trigger here describes **what actually happened**: a human approving a document.
 */
export async function postEndOfServiceProvision(transport: Transport, args: PostEndOfServiceProvisionArgs, signal?: AbortSignal): Promise<T.HrProvision> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/end-of-service-provisions/" + encodeURIComponent(args.provisionId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrProvision", response.json) as T.HrProvision;
}

export interface PostEndOfServiceSettlementArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف المخالصة. / The final settlement identifier. */
  settlementId: string;
}

/**
 * ترحيل مخالصة نهاية الخدمة / Post the end-of-service settlement
 * 
 * يرحّل المخالصة بسيناريوهاتها الثلاثة: المخصص مطابق، أو ناقص فيُحمَّل العجز على مصروف الفترة، أو زائد فتُردّ الزيادة إليه.
 * 
 * **ويُمرَّر في قاموس المبالغ — إضافةً إلى الأربعة المعلَنة على الحدث — رصيدُ المخصص والمستحقّ**: تعبيرا الشرط في المصفوفة يستعملانهما وهما **ليسا في كتلة amounts**، وغيابُهما يُنتج UndecidableCondition — رفضاً لا نجاحاً ناقصاً. وهذا موضعٌ يسهل أن يُنسى ويستحيل أن يُكتشف بقراءة الحدث وحده.
 * 
 * Posts the settlement under its three scenarios: the provision matches, or falls short and the shortfall is charged to the period expense, or exceeds and the excess is released back to it.
 * 
 * **The amounts dictionary carries — beyond the four declared on the event — the provision balance and the amount due**: the matrix's two condition expressions use them and they are **not in the amounts block**, and their absence yields UndecidableCondition — a refusal, not a partial success. This is a place easy to forget and impossible to discover by reading the event alone.
 */
export async function postEndOfServiceSettlement(transport: Transport, args: PostEndOfServiceSettlementArgs, signal?: AbortSignal): Promise<T.HrSettlement> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/end-of-service-settlements/" + encodeURIComponent(args.settlementId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrSettlement", response.json) as T.HrSettlement;
}

export interface PostGoodsReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف استلام البضاعة. / The goods receipt identifier. */
  receiptId: string;
}

/**
 * ترحيل استلام بضاعة / Post a goods receipt
 * 
 * يرحّل استلاماً مسوّدة **سطراً سطراً**. وهو الباب الوحيد على هذا السطح الذي **يمسّ دفتراً مساعداً غير دفتر الأطراف**: كل سطر يُسجَّل أولاً في **دفتر المخزون المساعد** بتكلفته الفعلية فيصير أساس تكلفة الصنف، **ثم** يُدين حساب مراقبة المخزون بالمبلغ نفسه ويُنشئ التزام «بضاعة مستلمة لم تُفوتر» على المورد.
 * 
 * **وترتيب النداءين ليس تفصيلاً:** الحركة تُسجَّل أولاً، فإن رُفضت لم يُكتب في الدفتر شيء ولم ينحرف طرفٌ عن طرف. وهوية الحركة هي هوية الترحيل حرفاً بحرف، فالوصول الثاني لا يصرف كميةً ثانية ولا يُنشئ قيداً ثانياً.
 * 
 * **ولذلك يشترط هذا الباب شيئين لا يشترطهما غيره على هذا السطح:** استحقاق وحدة **المخزون** للمنشأة — ومنشأةٌ لم تشترِها تُرفض بـ403 وentitlement.not_entitled، وهو رفضٌ صحيح لا عطل — وقدرة **المطابقة الثلاثية** (three_way_match) مُشغَّلةً في ملفّ قدراتها، وإلا رُفض بـ422.
 * 
 * و**gross** على الجواب هو تكلفة الاستلام كاملةً، و**tax** صفر دائماً: الاستلام لا ضريبة عليه — الضريبة تقع عند فاتورة المورد لا عند دخول البضاعة.
 * 
 * Posts a draft goods receipt **line by line**. It is the only door on this surface that **touches a subledger other than the party subledger**: each line is first recorded in the **inventory subledger** at its actual cost, becoming the item's cost basis, and **only then** is the inventory control account debited with the same amount and a 'goods received not invoiced' liability raised against the supplier.
 * 
 * **The order of the two calls is not a detail:** the movement is recorded first, so that if it is refused nothing is written to the ledger and neither side drifts from the other. The movement's identity is the posting identity letter for letter, so a second arrival issues no second quantity and creates no second entry.
 * 
 * **This door therefore requires two things no other door on this surface requires:** the company's entitlement to the **Inventory** module — a company that has not bought it is refused with 403 and entitlement.not_entitled, which is a correct refusal and not a fault — and the **three-way match** capability enabled in its capability profile, failing which it is refused with 422.
 * 
 * In the response **gross** is the full receipt cost and **tax** is always zero: a receipt carries no VAT — tax arises at the supplier's invoice, not when the goods arrive.
 */
export async function postGoodsReceipt(transport: Transport, args: PostGoodsReceiptArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/goods-receipts/" + encodeURIComponent(args.receiptId) + "/posting";
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

export interface PostPayrollPaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند صرف الرواتب. / The payroll payment identifier. */
  paymentId: string;
}

/**
 * ترحيل صرف الرواتب / Post the payroll payment
 * 
 * يرحّل الصرف: **قيدٌ لكل سطر**، سطرُه الأول يُطفئ التزام الرواتب بطرف الموظف، وسطرُه الثاني يمسّ حساب التسوية بطرف الخزينة واقعةً نصّية.
 * 
 * **والحبيبيّة هنا هي الحبيبيّة نفسها في الاستحقاق** — ولا يجوز أن تختلف: طرفا المطابقة يجب أن يتساويا، وقيدٌ واحد للسند مقابل قيدٍ لكل قسيمة في الاستحقاق كان سيجعل المطابقة مستنداً بمستند مستحيلة.
 * 
 * **ولحظة وقوع هذا القيد بالضبط معلَّقة على حكم المالك**: نصّ مُطلِق الحدث في المصفوفة يقول «عند تنفيذ ملفّ حماية الأجور **أو** التحويل البنكي»، والمصفوفة لا تحسم بين شقّي «أو» كتبتها هي. وما يفعله هذا الباب هو الشقّ الأول صراحةً: الترحيل عند اعتماد الصرف.
 * 
 * Posts the payment: **one entry per line**, its first line clearing the salaries payable against the employee's party, and its second touching the settlement account with the treasury party carried as a textual fact.
 * 
 * **The grain here is the same grain as the accrual** — and may not differ: the two sides of the reconciliation must match, and one entry for the document against one entry per payslip in the accrual would make document-by-document reconciliation impossible.
 * 
 * **The exact moment this entry falls is pending the owner's judgement**: the event's trigger text in the matrix reads 'on execution of the wage protection file **or** the bank transfer', and the matrix does not settle between the two halves of its own 'or'. What this door does is explicitly the first half: posting on approval of the payment.
 */
export async function postPayrollPayment(transport: Transport, args: PostPayrollPaymentArgs, signal?: AbortSignal): Promise<T.HrPayrollPayment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-payments/" + encodeURIComponent(args.paymentId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayrollPayment", response.json) as T.HrPayrollPayment;
}

export interface PostPayrollRunArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مسيّر الرواتب. / The payroll run identifier. */
  runId: string;
}

/**
 * ترحيل استحقاق الرواتب / Post the payroll accrual
 * 
 * يرحّل استحقاق المسيّر. **نداءٌ واحد يُصدر قيداً لكل قسيمة**، لكلٍّ هويّته السداسية (شركة · نوع المستند · **معرّف القسيمة** · رمز الإطلاق · الجيل · رمز الحدث). ولذلك **الجواب قائمة قسائم** لكلٍّ معرّف قيدها وalreadyPosted الخاصّ بها في الجسم، لا مستنداً واحداً بمعرّف قيد واحد.
 * 
 * **ولماذا القسيمة لا المسيّر — وهذا مفروضٌ بنيوياً لا مختار:** مسار القالب في محرّك الترحيل يحلّ طرف الدفتر المساعد من واقعةٍ **واحدة** لكل طلب، ويقرأ مركز التكلفة وكل الأبعاد من قاموسٍ **واحد على مستوى الطلب**. فطلبٌ واحد لا يستطيع بنيةً أن يحمل طرفين ولا مركزين. وقيدٌ واحد لثلاثمئة موظف كان سيكتب ذمّة الجميع على **طرفٍ واحد ومركزٍ واحد** — **والقيد الناتج متوازن تماماً، والسلسلة سليمة، وميزان المراجعة صحيح**، ولا شيء يُظهر أن ذمم ثلاثمئة موظف صارت ذمّة موظفٍ واحد.
 * 
 * **والمسار الصريح مقفول أصلاً**: مخطّط PostingLine.role مجموعة مغلقة منشورة من أربع عشرة قيمة ليس فيها دور رواتب واحد، فبلوغُ الرواتب منه يكسر مخطّطاً منشوراً.
 * 
 * **وحصين ضد التكرار**: الوصول الثاني بالهوية نفسها يُرجع القسائم ذاتها موسومةً ولا يُنشئ قيداً ثانياً — مهما كان ترتيب الوصول. ورمز 200 حين كانت **كلّها** مُرحَّلة من قبل، و201 حين كتب هذا النداء واحدة منها على الأقل.
 * 
 * ولا جسم لهذا الطلب: مفتاح الحصانة تشتقّه الوحدة من هوية المستند ولا يرسله العميل.
 * 
 * Posts the run's accrual. **One call issues one entry per payslip**, each under its own six-part identity (company, document type, **payslip id**, trigger, generation, event code). Hence **the response is a list of payslips**, each with its own entry identifier and its own alreadyPosted in the body — not one document with one entry id.
 * 
 * **Why the payslip and not the run — this is structurally forced, not chosen:** the template path in the posting engine resolves the subledger party from a **single** fact per request, and reads the cost centre and every dimension from a **single request-level** dictionary. One request structurally cannot carry two parties or two centres. One entry for three hundred employees would write everyone's balance against **one party and one centre** — **and the resulting entry balances perfectly, the hash chain is intact, and the trial balance is correct**, with nothing to show that three hundred employees' balances became one employee's.
 * 
 * **The explicit path is closed anyway**: the PostingLine.role schema is a published closed set of fourteen values with no payroll role among them, so reaching payroll through it breaks a published schema.
 * 
 * **Idempotent**: a second arrival with the same identity returns the same payslips marked and never creates a second entry — whatever the arrival order. Status 200 when **all** of them were already posted, 201 when this call wrote at least one.
 * 
 * This request has no body: the idempotency key is derived by the module from the document identity rather than sent by the client.
 */
export async function postPayrollRun(transport: Transport, args: PostPayrollRunArgs, signal?: AbortSignal): Promise<T.HrPayslipList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-runs/" + encodeURIComponent(args.runId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayslipList", response.json) as T.HrPayslipList;
}

export interface PostPurchaseReturnArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مرتجع المشتريات. / The purchase return identifier. */
  returnId: string;
}

/**
 * ترحيل مرتجع مشتريات / Post a purchase return
 * 
 * يرحّل مرتجع مشتريات: **البضاعة تخرج من المخزون بتكلفة استلامها الأصلي، ثم تنقص ذمّة المورد**. والدفتر المساعد أوّلاً ثم القيد، فرفضٌ من المخزون — كردٍّ يتجاوز ما استُلم — يترك الدفتر نظيفاً.
 * 
 * **وهنا يُملأ صافي المرتجع**: بالرقم الذي حسبته وحدة المخزون، لا برقمٍ سلّمه المستدعي. وكان هذا المسار قبل اليوم يُدين الحساب الضابط للمخزون بمبلغٍ من المستدعي **ولا يكتب حركة واحدة في الدفتر المساعد** — أي حسابٌ ضابط يتحرّك ودفترٌ مساعد ساكن، وهو الانحراف الذي أُنشئت المطابقة لكشفه.
 * 
 * وحصين ضد التكرار بالشكل نفسه: 201 أوّلاً و200 ثانياً ومعرّف القيد نفسه.
 * 
 * Posts a purchase return: **the goods leave inventory at their original receipt cost, then the supplier's payable is reduced**. The subledger is written first and the entry second, so a refusal from inventory — a return beyond what was received, say — leaves the ledger clean.
 * 
 * **This is where the return net is filled in**: with the number the inventory module computed, not one the caller supplied. Until today this path debited the inventory control account with a caller-supplied amount **and wrote not one movement in the subledger** — a control account moving while its subledger stands still, which is precisely the divergence reconciliation exists to catch.
 * 
 * Idempotent in the same shape: 201 first, 200 second, same entry identifier.
 */
export async function postPurchaseReturn(transport: Transport, args: PostPurchaseReturnArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/purchase-returns/" + encodeURIComponent(args.returnId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface PostRentInvoiceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف فاتورة الإيجار. / The rent invoice identifier. */
  invoiceId: string;
}

/**
 * ترحيل فاتورة إيجار / Post a rent invoice
 * 
 * يرحّل فاتورة مسوّدة فتصير **واقعة محاسبية**. مورد فرعي مستقلّ لا PUT على المستند.
 * 
 * **وحصين ضد التكرار بهوية الترحيل** (شركة · نوع المستند · معرّفه · رمز الإطلاق · الجيل · رمز الحدث): الوصول الثاني بالهوية نفسها يُرجع المستند ذاته وalreadyPosted = true ورمز 200 بدل 201 **ومعرّف القيد نفسه**، ولا يُنشئ قيداً ثانياً مهما كان ترتيب الوصول. والحكم حكمُ بوّابة الوحدة لا مقارنةَ حالةٍ قُرئت قبل النداء.
 * 
 * ولا جسم لهذا الطلب: مفتاح الحصانة تشتقّه الوحدة من هوية المستند ولا يُرسله العميل.
 * 
 * **والرفض حين لا يكون العقار مسجَّلاً في الدفتر رفضٌ كامل** لا سطرٌ ناقص: قاعدة الحجب التي لا تُقيَّم لا تُتجاوَز.
 * 
 * Posts a draft invoice, turning it into an **accounting fact**. A separate sub-resource, not a PUT on the document.
 * 
 * **Idempotent by the posting identity** (company, source document type, source document id, trigger, generation, event code): a second arrival with the same identity returns the same document with alreadyPosted = true, status 200 instead of 201, **and the same entry identifier**, and never creates a second entry whatever the arrival order. The verdict belongs to the module gateway, not to a state read before the call.
 * 
 * This request has no body: the idempotency key is derived by the module from the document identity rather than sent by the client.
 * 
 * **When the property is not registered in the ledger the refusal is total**, not a missing line: a guard rule that cannot be evaluated is never bypassed.
 */
export async function postRentInvoice(transport: Transport, args: PostRentInvoiceArgs, signal?: AbortSignal): Promise<T.RentInvoice> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/rent-invoices/" + encodeURIComponent(args.invoiceId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RentInvoice", response.json) as T.RentInvoice;
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

export interface PostSocialInsurancePaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند سداد التأمينات. / The social insurance payment identifier. */
  paymentId: string;
}

/**
 * ترحيل سداد التأمينات / Post the social insurance payment
 * 
 * يرحّل السداد **قيداً واحداً للفترة — وهو المستند الوحيد في هذه الوحدة الذي يجوز فيه ذلك**.
 * 
 * **ولماذا يجوز هنا وحده:** سطره الأول على حساب الالتزام معلَنٌ في دليل الحسابات **بلا دفتر مساعد**، فلا طرفَ يُفقد بالتجميع أصلاً. أمّا سطره الثاني فيحمل **طرف الخزينة** كسائر مستندات الدفع.
 * 
 * Posts the settlement as **one entry for the period — the only document in this module where that is permissible**.
 * 
 * **Why only here:** its first line's liability account is declared in the chart with **no subledger**, so no party is lost by aggregation at all. Its second line carries **the treasury party**, like every other payment document.
 */
export async function postSocialInsurancePayment(transport: Transport, args: PostSocialInsurancePaymentArgs, signal?: AbortSignal): Promise<T.HrSocialInsurancePayment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/social-insurance-payments/" + encodeURIComponent(args.paymentId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrSocialInsurancePayment", response.json) as T.HrSocialInsurancePayment;
}

export interface PostStockMovementArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مستند حركة المخزون. / The stock movement document identifier. */
  movementId: string;
}

/**
 * ترحيل حركة مخزون / Post a stock movement
 * 
 * يرحّل مستند حركة مسوّدة: **حركةٌ في دفتر المخزون المساعد أوّلاً ثم قيدٌ في الدفتر**، بهوية ترحيل واحدة على الطرفين. والترتيب مقصود (ADR-0041): رفضٌ من المخزون — كصرفٍ بلا أساس تكلفة — يترك الدفتر نظيفاً؛ ولو وقع القيد أوّلاً لترك حساباً ضابطاً تحرّك بلا حركةٍ تقابله.
 * 
 * **وقيمة الصادر تُحسب هنا ولا تُملى**: تخرج في الحقل cost بعد الترحيل.
 * 
 * **وحصين ضد التكرار**: الوصول الثاني بالهوية نفسها يُرجع المستند ذاته وalreadyPosted = true ورمز 200 بدل 201، بلا حركة ثانية وبلا قيد ثانٍ. والحكم حكمُ بوّابة الترحيل لا مقارنةَ حالةٍ قُرئت قبل النداء: نداءان متزامنان يجتازان فحص «مسوّدة» معاً ويلتقيان عند الهوية الواحدة.
 * 
 * Posts a draft stock movement: **a movement in the inventory subledger first, then an entry in the ledger**, under one posting identity on both sides. The order is deliberate (ADR-0041): a refusal from inventory — an issue with no cost basis, say — leaves the ledger clean, whereas an entry written first would leave a control account that moved with no movement facing it.
 * 
 * **An outbound movement's value is computed here, never dictated**: it comes back in the cost field after posting.
 * 
 * **Idempotent**: a second arrival with the same identity returns the same document with alreadyPosted = true and status 200 instead of 201, with no second movement and no second entry. The verdict is the posting gateway's, not a comparison against a state read before the call: two concurrent calls both pass the 'is it a draft' check and meet at the one identity.
 */
export async function postStockMovement(transport: Transport, args: PostStockMovementArgs, signal?: AbortSignal): Promise<T.StockMovement> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/stock-movements/" + encodeURIComponent(args.movementId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "StockMovement", response.json) as T.StockMovement;
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

export interface PostSupplierPaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند الصرف. / The supplier payment identifier. */
  paymentId: string;
}

/**
 * ترحيل سند صرف / Post a supplier payment
 * 
 * يرحّل سند صرف مسوّدة: **يُدان مديناً حساب مراقبة ذمم الموردين بالمدفوع** — أي أنّ المدفوع **يُسقط من ذمّة المورد** — ويُدان حساب المصاريف البنكية بالرسوم إن وُجدت، ويُدان دائناً حساب التسوية بمجموعهما. ثم تُنزَل تخصيصاته على فواتير المورد.
 * 
 * وحصين ضد التكرار بهوية الترحيل نفسها وبالسلوك نفسه: الوصول الثاني يُرجع السند ذاته وalreadyPosted = true ورمز 200 ومعرّف القيد نفسه، بلا قيد ثانٍ **وبلا تخصيص ثانٍ**.
 * 
 * Posts a draft supplier payment: **the accounts payable control account is debited with the amount paid** — that is, the payment **comes off the supplier's balance** — the bank charges account is debited with the fee if there is one, and the settlement account is credited with their sum. Its allocations are then applied to that supplier's bills.
 * 
 * Idempotent by the same posting identity with the same behaviour: a second arrival returns the same payment with alreadyPosted = true, status 200, and the same entry identifier, with no second entry **and no second allocation**.
 */
export async function postSupplierPayment(transport: Transport, args: PostSupplierPaymentArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/supplier-payments/" + encodeURIComponent(args.paymentId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface PostTenantReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند القبض من المستأجر. / The tenant receipt identifier. */
  receiptId: string;
}

/**
 * ترحيل سند قبض / Post a tenant receipt
 * 
 * يرحّل السند بالحدث الذي اختاره حضور المرجع أو غيابه، وبالحصانة نفسها: الوصول الثاني بالهوية نفسها يُرجع 200 وalreadyPosted = true ومعرّف القيد نفسه.
 * 
 * Posts the receipt under the event chosen by the presence or absence of the reference, with the same idempotency: a second arrival with the same identity returns 200, alreadyPosted = true, and the same entry identifier.
 */
export async function postTenantReceipt(transport: Transport, args: PostTenantReceiptArgs, signal?: AbortSignal): Promise<T.TenantReceipt> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/tenant-receipts/" + encodeURIComponent(args.receiptId) + "/posting";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "TenantReceipt", response.json) as T.TenantReceipt;
}

export interface ReadAttachmentArgs {
  /** معرّف المرفق — غامضٌ عمداً: لا يُشتقّ من اسم ملفّ ولا من مسار، ولا يُقرأ منه شيء عن صاحبه. / The attachment identifier — deliberately opaque: derived from no file name and no path, and telling nothing about its owner. */
  attachmentId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * قراءة وصف مرفق / Read one attachment descriptor
 * 
 * يقرأ وصف مرفق **بلا بايتة**: البصمة والحجم والنوع المشموم والمُودِع والزمن، وسلسلة الإصدارات (سلفه وخلَفه)، وعلامة السحب إن سُحب، ومسارَ تنزيل بايتاته.
 * 
 * **ولاحظ ما ليس في الجواب: مفتاح الكائن في المخزن.** هو مسارٌ فيزيائي يفهمه المحوّل ويعيش في القاعدة وحدها، ونشرُه يجعل عميلاً يبني عليه ثم ينكسر يوم يصير المحوّل مخزناً كائنياً. والمسار الذي يحتاجه العميل هو contentPath.
 * 
 * **ولاحظ ما ليس على هذا المورد: لا PUT ولا PATCH ولا DELETE.** والغياب بنيوي لا اتفاقي: دور التطبيق في PostgreSQL بلا صلاحية UPDATE ولا DELETE (42501)، ومشغّلٌ يرفض الاثنين على **كل** دور والمالك منهم (23001). والتصحيح إصدارٌ على /revisions، والإزالة علامةٌ على /withdrawal.
 * 
 * Reads an attachment's descriptor **without a byte**: digest, length, sniffed type, depositor, instant, the version chain (predecessor and successor), the withdrawal marker if withdrawn, and the path its bytes download from.
 * 
 * **Note what the answer does not carry: the object key in the store.** It is a physical path the adapter understands and it lives in the database alone; publishing it makes a client build on it and break the day the adapter becomes object storage. The path a client needs is contentPath.
 * 
 * **Note what this resource does not carry: no PUT, no PATCH, no DELETE.** The absence is structural, not conventional: the application role in PostgreSQL holds neither UPDATE nor DELETE (42501), and a trigger refuses both for **every** role including the owner (23001). Correction is a version at /revisions; removal is a marker at /withdrawal.
 */
export async function readAttachment(transport: Transport, args: ReadAttachmentArgs, signal?: AbortSignal): Promise<T.Attachment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/attachments/" + encodeURIComponent(args.attachmentId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Attachment", response.json) as T.Attachment;
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

export interface ReadCustomerReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند القبض. / The customer receipt identifier. */
  receiptId: string;
}

/**
 * قراءة سند قبض / Read one customer receipt
 * 
 * يقرأ سند قبض بحالته ومجاميعه ومعرّف قيده إن رُحّل. و**net** هو المقبوض و**tax** هو خصم تعجيل السداد و**gross** مجموعهما — أي ما سقط عن ذمّة العميل.
 * 
 * وكانت هذه القراءة **غير موجودة في الوحدة أصلاً**: يُسجَّل السند ويُرحَّل ولا جملة تقول «ما حاله الآن؟». فمن أنشأ مسوّدةً ثم انقطع اتصاله لم يكن أمامه إلا أن **يعيد الترحيل ليعرف** — والحصانة تجعل ذلك غير مؤذٍ، لا تجعله مقبولاً.
 * 
 * Reads a customer receipt with its state, its totals, and its entry identifier if posted. Here **net** is the amount collected, **tax** is the early-settlement discount, and **gross** is their sum — that is, what came off the customer's balance.
 * 
 * This read **did not exist in the module at all**: a receipt could be recorded and posted with no sentence for 'what state is it in now?'. Whoever created a draft and then lost their connection had no option but to **post again in order to find out** — which idempotency makes harmless, not acceptable.
 */
export async function readCustomerReceipt(transport: Transport, args: ReadCustomerReceiptArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/customer-receipts/" + encodeURIComponent(args.receiptId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
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

export interface ReadEmployeeArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف الموظف. وهو **غير** رمزه المعتم: الرمز هو ما يعبر إلى الدفتر المساعد، والمعرّف عنوانٌ على هذا السطح. / The employee identifier. It is **not** the opaque code: the code is what crosses into the subledger, while the identifier is an address on this surface. */
  employeeId: string;
}

/**
 * قراءة موظف / Read one employee
 * 
 * يقرأ موظفاً واحداً بعلاقة عمله الجارية وهويته **مقنَّعة**.
 * 
 * **ولاحظ ما ليس على هذا المورد: لا PUT ولا DELETE.** الموظف تشير إليه قيود سنة سابقة، وحذفه يكسر كل تقرير مُرحَّل؛ والإنهاء **مورد فرعي** (…/termination) لا حقل حالة يُعدَّل.
 * 
 * Reads a single employee with the current employment and a **masked** identity.
 * 
 * **Note what this resource does not carry: no PUT and no DELETE.** An employee is referenced by last year's entries and deleting them breaks every posted report; termination is a **sub-resource** (…/termination), not a status field that is edited.
 */
export async function readEmployee(transport: Transport, args: ReadEmployeeArgs, signal?: AbortSignal): Promise<T.HrEmployee> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employees/" + encodeURIComponent(args.employeeId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrEmployee", response.json) as T.HrEmployee;
}

export interface ReadEmployeeAdvanceArgs {
  /** معرّف سلفة الموظف. / The employee advance identifier. */
  advanceId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * قراءة سلفة موظف / Read one employee advance
 * 
 * يقرأ السلفة بجدول سدادها **والمتبقّي منها مشتقّاً من الأقساط المستقطَعة فعلاً وحدها** — لا من مرور الزمن ولا من الجدول المخطَّط. وقسطٌ لم يدخل قسيمة لم يُستقطع.
 * 
 * Reads the advance with its repayment schedule and **the outstanding amount derived from instalments actually deducted alone** — not from elapsed time and not from the planned schedule. An instalment that never entered a payslip was never deducted.
 */
export async function readEmployeeAdvance(transport: Transport, args: ReadEmployeeAdvanceArgs, signal?: AbortSignal): Promise<T.HrAdvance> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employee-advances/" + encodeURIComponent(args.advanceId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrAdvance", response.json) as T.HrAdvance;
}

export interface ReadEmployeeDeductionArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف قيد الجزاء. / The deduction record identifier. */
  deductionId: string;
}

/**
 * قراءة جزاء / Read one deduction
 * 
 * يقرأ جزاءً واحداً ومعه القسيمة التي استُقطع فيها إن استُقطع. **وبلا entryId وبلا alreadyPosted** — وذلك بنيةٌ لا سهو.
 * 
 * Reads a single deduction along with the payslip it was consumed by, if any. **With no entryId and no alreadyPosted** — by construction, not by omission.
 */
export async function readEmployeeDeduction(transport: Transport, args: ReadEmployeeDeductionArgs, signal?: AbortSignal): Promise<T.HrDeduction> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employee-deductions/" + encodeURIComponent(args.deductionId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrDeduction", response.json) as T.HrDeduction;
}

export interface ReadEndOfServiceProvisionArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مستند استحقاق المخصص. / The provision accrual document identifier. */
  provisionId: string;
}

/**
 * قراءة مستند استحقاق المخصص / Read one provision accrual document
 * 
 * يقرأ المستند بحركاته لكل علاقة عمل ومعرّفات قيودها. **وحبيبيّة الحركة علاقة عمل لا موظف**: من يعود بعد انقطاع يبدأ استحقاقاً جديداً، ومخالصة الأولى لا تُخصم من رصيد الثانية.
 * 
 * Reads the document with its per-employment movements and their entry identifiers. **The movement's grain is the employment, not the employee**: someone who returns after a break starts a new entitlement, and the first employment's settlement is not deducted from the second's balance.
 */
export async function readEndOfServiceProvision(transport: Transport, args: ReadEndOfServiceProvisionArgs, signal?: AbortSignal): Promise<T.HrProvision> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/end-of-service-provisions/" + encodeURIComponent(args.provisionId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrProvision", response.json) as T.HrProvision;
}

export interface ReadEndOfServiceSettlementArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف المخالصة. / The final settlement identifier. */
  settlementId: string;
}

/**
 * قراءة مخالصة نهاية خدمة / Read one end-of-service settlement
 * 
 * يقرأ المخالصة بأرقامها الستّة وسيناريوها ومرجع أساس حسابها. **وهي أكثر مستند في هذه الوحدة عرضةً للنزاع**، فقراءتها قبل ترحيلها شرط.
 * 
 * Reads the settlement with its six amounts, its scenario, and the reference to its calculation basis. **It is the most disputable document in this module**, so reading it before posting is a requirement.
 */
export async function readEndOfServiceSettlement(transport: Transport, args: ReadEndOfServiceSettlementArgs, signal?: AbortSignal): Promise<T.HrSettlement> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/end-of-service-settlements/" + encodeURIComponent(args.settlementId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrSettlement", response.json) as T.HrSettlement;
}

export interface ReadGoodsReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف استلام البضاعة. / The goods receipt identifier. */
  receiptId: string;
}

/**
 * قراءة استلام بضاعة / Read one goods receipt
 * 
 * يقرأ استلاماً بحالته وتكلفته ومعرّف قيده إن رُحّل. و**entryId عليه هو قيد آخر سطر رُحّل**، لا قيداً واحداً للاستلام: كل سطر يُرحَّل قيداً مستقلاً لأن قالب المصفوفة يحمل مرجع صنف واحداً ومستودعاً واحداً على مستوى الطلب، فقيدٌ واحد لاستلام متعدد الأصناف كان سيحمل مرجع صنف واحد لأصناف عدّة ويفسد الدفتر المساعد للأصناف بصمت.
 * 
 * Reads a goods receipt with its state, its cost, and its entry identifier if posted. **Its entryId is the entry of the last posted line**, not one entry for the receipt: each line posts its own entry because the matrix template carries a single item reference and a single warehouse at request level, so one entry for a multi-item receipt would carry one item reference for several items and silently corrupt the item subledger.
 */
export async function readGoodsReceipt(transport: Transport, args: ReadGoodsReceiptArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/goods-receipts/" + encodeURIComponent(args.receiptId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface ReadGoodsReceiptLinesArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف استلام البضاعة. / The goods receipt identifier. */
  receiptId: string;
}

/**
 * قراءة سطور استلام / Read the lines of one goods receipt
 * 
 * يقرأ سطور استلامٍ بمعرّفاتها ووحداتها — **ومعرّف السطر هو مدخل الفاتورة المخزنية ومدخل المرتجع**.
 * 
 * **ولماذا مورد فرعي لا حقلٌ يُضاف إلى قراءة الاستلام:** شكلُ جواب GET /goods-receipts/{receiptId} منشورٌ في العقد، وتغليفُه في مغلَّفٍ جديد يكسر كل عميل بُني عليه — أي v2 لا نموّاً. والنموّ إضافةٌ محضة: مسارٌ جديد لا مسارٌ مُعاد كتابته.
 * 
 * **وكل سطر يحمل وحدة قياسه**: كمّيته تصل إلى دفتر المخزون فتُضرب في تكلفة الوحدة، و«عشرة» بلا وحدة ليست معلومة — عشر حبّات أم عشر كراتين؟ والفرق يصل إلى المال بقيدٍ متوازن تماماً.
 * 
 * Reads the lines of a goods receipt with their identifiers and their units — **a line identifier is the input to a stock bill and to a purchase return**.
 * 
 * **Why a sub-resource and not a field added to reading the receipt:** the response shape of GET /goods-receipts/{receiptId} is already published in this contract, and wrapping it in a new envelope breaks every client built on it — that is v2, not growth. Growth is pure addition: a new path, not a rewritten one.
 * 
 * **Every line carries its unit of measure**: its quantity reaches the inventory subledger and is multiplied by a unit cost, and 'ten' without a unit is not information — ten pieces or ten cartons? The difference reaches the money inside a perfectly balanced entry.
 */
export async function readGoodsReceiptLines(transport: Transport, args: ReadGoodsReceiptLinesArgs, signal?: AbortSignal): Promise<T.PurchaseDocumentLineList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/goods-receipts/" + encodeURIComponent(args.receiptId) + "/lines";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "PurchaseDocumentLineList", response.json) as T.PurchaseDocumentLineList;
}

export interface ReadInventoryValuationArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** تاريخ التقييم الميلادي. / The Gregorian valuation date. */
  asOf: string;
}

/**
 * تقييم المخزون ومطابقته / Inventory valuation and reconciliation
 * 
 * يقرأ تقييم المخزون في تاريخ معلوم، و**يطابقه بحسابه الضابط بثلاثة طرق مستقلّة إلى الرقم نفسه**: مجموع الحركات، ومجموع أرصدة الأصناف، ورصيد نقطة الضبط في دفتر الأستاذ.
 * 
 * **واثنان يكفيان لكشف انحراف بين الوحدة والدفتر؛ والثالث يكشف انحراف الوحدة عن نفسها** — رصيدٌ لا يساوي مجموع حركاته — وهو عطلٌ لا يراه أي فحص يقارن طرفين فقط.
 * 
 * وisReconciled يعني **الفارق صفر بالضبط**، لا «قريب من الصفر». وكل مستند منحرف يُسمّى بنوعه ومعرّفه وصنفه وسبب انحرافه، فلا يُقال «هناك مشكلة» بلا «أين».
 * 
 * Reads the inventory valuation at a given date and **reconciles it against its control account by three independent routes to the same number**: the sum of movements, the sum of item balances, and the control point balance in the general ledger.
 * 
 * **Two are enough to reveal a divergence between the module and the ledger; the third reveals the module diverging from itself** — a balance that does not equal the sum of its own movements — a failure no two-sided check can see.
 * 
 * isReconciled means **the difference is exactly zero**, not 'close to zero'. Every diverging document is named by its type, its identifier, its item, and the reason, so the report never says 'there is a problem' without saying where.
 */
export async function readInventoryValuation(transport: Transport, args: ReadInventoryValuationArgs, signal?: AbortSignal): Promise<T.InventoryValuation> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/inventory-valuation";
  const query = new URLSearchParams();
  query.set("asOf", args.asOf);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "InventoryValuation", response.json) as T.InventoryValuation;
}

export interface ReadItemArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف الصنف. / The item identifier. */
  itemId: string;
}

/**
 * قراءة صنف / Read one item
 * 
 * يقرأ صنفاً واحداً بوحدة أساسه ومعاملات تحويله.
 * 
 * Reads a single item with its base unit and its conversion factors.
 */
export async function readItem(transport: Transport, args: ReadItemArgs, signal?: AbortSignal): Promise<T.Item> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/items/" + encodeURIComponent(args.itemId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Item", response.json) as T.Item;
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

export interface ReadLeaseContractArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف عقد الإيجار. / The lease contract identifier. */
  leaseId: string;
}

/**
 * قراءة عقد إيجار / Read one lease contract
 * 
 * يقرأ العقد بحالته ومدّته وقيمته.
 * 
 * Reads the contract with its state, its term, and its value.
 */
export async function readLeaseContract(transport: Transport, args: ReadLeaseContractArgs, signal?: AbortSignal): Promise<T.Lease> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/lease-contracts/" + encodeURIComponent(args.leaseId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Lease", response.json) as T.Lease;
}

export interface ReadLeaseScheduleArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف عقد الإيجار. / The lease contract identifier. */
  leaseId: string;
}

/**
 * قراءة جدول دفعات العقد بمعرّفات سطوره / Read the lease payment schedule with its line identifiers
 * 
 * يُرجع جدول الدفعات **بمعرّفات سطوره** — وهي مدخل الفوترة: طلب الفاتورة يحمل معرّفات الأقساط المفوترة ولا يحمل مبالغ.
 * 
 * **وبلا نشر هذه المعرّفات يصير باب الفوترة باباً لا يوصل إليه بابٌ آخر على هذا السطح** — وهو الاعتراض المكتوب حرفياً في قرار نشر المستندات النقدية.
 * 
 * Returns the payment schedule **with its line identifiers** — the entry point to invoicing: the invoice request carries the identifiers of the instalments being billed and carries no amounts.
 * 
 * **Without publishing these identifiers the invoicing door becomes a door no other door on this surface can reach** — the objection written literally in the cash-documents publication decision.
 */
export async function readLeaseSchedule(transport: Transport, args: ReadLeaseScheduleArgs, signal?: AbortSignal): Promise<T.LeaseSchedule> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/lease-contracts/" + encodeURIComponent(args.leaseId) + "/schedule";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "LeaseSchedule", response.json) as T.LeaseSchedule;
}

export interface ReadLesseeArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف المستأجر العقاري — **لا مستأجر النظام**: هذا مورد داخل نطاق منشأة. / The lessee identifier — **not the system tenant**: this resource lives inside a company scope. */
  lesseeId: string;
}

/**
 * قراءة مستأجر عقاري / Read one lessee
 * 
 * يقرأ مستأجراً بتسجيله الضريبي وإقامته الضريبية.
 * 
 * Reads a lessee with its VAT registration and tax residency.
 */
export async function readLessee(transport: Transport, args: ReadLesseeArgs, signal?: AbortSignal): Promise<T.RealEstateParty> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/lessees/" + encodeURIComponent(args.lesseeId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RealEstateParty", response.json) as T.RealEstateParty;
}

export interface ReadMembershipsArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * أعضاء المنشأة / The company's members
 * 
 * يقرأ من يعمل في هذه المنشأة وبأي دور. **ولا اعتماد واحد يخرج من هنا**: القائمة أسماء وأدوار ولحظاتُ منح، واعتماد الانتساب يخرج مرّة واحدة في استجابة الدعوة ولا يُعاد أبداً.
 * 
 * وهو داخل نطاق المنشأة كأي مسار آخر: اعتمادٌ لا يبلغها يُرفض بـ403 وtenancy.company_out_of_scope، ولا يخرج من الرفض شيء عنها — لا عدد أعضائها ولا وجودها.
 * 
 * Reads who works in this company and in what role. **Not one credential leaves here**: the list is names, roles, and grant instants; an enrolment credential leaves once, in the invitation response, and is never re-issued.
 * 
 * It sits inside company scope like every other path: a credential that does not reach the company is refused with 403 and tenancy.company_out_of_scope, and nothing about that company crosses the refusal — not its member count, not its existence.
 */
export async function readMemberships(transport: Transport, args: ReadMembershipsArgs, signal?: AbortSignal): Promise<T.MembershipList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/memberships";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "MembershipList", response.json) as T.MembershipList;
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

export interface ReadPayrollPaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند صرف الرواتب. / The payroll payment identifier. */
  paymentId: string;
}

/**
 * قراءة سند صرف رواتب / Read one payroll payment
 * 
 * يقرأ السند بحالته وسطوره **ومعرّفات قيودها** — قيدٌ لكل سطر لا قيدٌ واحد للسند.
 * 
 * Reads the payment with its state, its lines, and **their entry identifiers** — one entry per line, not one entry for the document.
 */
export async function readPayrollPayment(transport: Transport, args: ReadPayrollPaymentArgs, signal?: AbortSignal): Promise<T.HrPayrollPayment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-payments/" + encodeURIComponent(args.paymentId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayrollPayment", response.json) as T.HrPayrollPayment;
}

export interface ReadPayrollRunArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مسيّر الرواتب. / The payroll run identifier. */
  runId: string;
}

/**
 * قراءة مسيّر رواتب / Read one payroll run
 * 
 * يقرأ المسيّر بحالته ومجاميعه وعدد قسائمه. ونقطة قراءة: تعمل والاشتراك للقراءة فقط.
 * 
 * وغياب هذه القراءة كان سيدفع العميل إلى **إعادة الترحيل ليعرف** — والحصانة تجعل ذلك غير مؤذٍ، لا تجعله مقبولاً.
 * 
 * Reads the run with its state, its totals, and its payslip count. A read point: it works while the subscription is read-only.
 * 
 * Without this read a client would have to **post again in order to find out** — which idempotency makes harmless, not acceptable.
 */
export async function readPayrollRun(transport: Transport, args: ReadPayrollRunArgs, signal?: AbortSignal): Promise<T.HrPayrollRun> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payroll-runs/" + encodeURIComponent(args.runId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayrollRun", response.json) as T.HrPayrollRun;
}

export interface ReadPayslipArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف القسيمة — **وهو DocumentId في هوية الإحكام**، لا معرّف المسيّر. / The payslip identifier — **which is the DocumentId in the posting identity**, not the run's identifier. */
  payslipId: string;
}

/**
 * قراءة قسيمة راتب / Read one payslip
 * 
 * يقرأ قسيمة واحدة بمكوّناتها ووعاء اشتراكها ومعرّف قيدها. **وهي مستند الترحيل**: معرّفها هو DocumentId في هوية الإحكام، فقراءتها مفردةً شرطٌ لا زينة.
 * 
 * و**contributoryWage محفوظٌ ليُراجَع لا ليُعاد حسابه**: من يراجع مسيّراً بعد سنة يحتاج الوعاء كما كان بعد القصّ بحدَّي الصفّ الذي كان سارياً حينها.
 * 
 * Reads a single payslip with its components, its contributory wage, and its entry identifier. **It is the posting document**: its identifier is the DocumentId in the idempotency identity, so reading it individually is a requirement, not an ornament.
 * 
 * **contributoryWage is stored to be reviewed, not recomputed**: whoever reviews a run a year later needs the base as it stood after clamping by the row that was in force then.
 */
export async function readPayslip(transport: Transport, args: ReadPayslipArgs, signal?: AbortSignal): Promise<T.HrPayslip> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/payslips/" + encodeURIComponent(args.payslipId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrPayslip", response.json) as T.HrPayslip;
}

export interface ReadPropertyArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف العقار. / The property identifier. */
  propertyId: string;
}

/**
 * قراءة عقار / Read one property
 * 
 * يقرأ عقاراً بنموذج ملكيته وحصّة مالكه. ونقطة قراءة: تعمل والاشتراك للقراءة فقط.
 * 
 * و**الحصّة كسرٌ ببسطٍ ومقام** لا عدد عشري: لا مقياس عشري معلَن لأي نسبة في هذا المنتج، واختيار مقياسٍ هنا كتابةُ رقم نظامي في عقد منشور.
 * 
 * Reads a property with its ownership model and its owner share. A read point: it works while the subscription is read-only.
 * 
 * The **share is a fraction with a numerator and a denominator**, not a decimal: no decimal scale is declared for any ratio in this product, and picking one here would write a regulatory number into a published contract.
 */
export async function readProperty(transport: Transport, args: ReadPropertyArgs, signal?: AbortSignal): Promise<T.Property> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/properties/" + encodeURIComponent(args.propertyId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Property", response.json) as T.Property;
}

export interface ReadPropertyOwnerArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مالك العقار. / The property owner identifier. */
  ownerId: string;
}

/**
 * قراءة مالك عقار / Read one property owner
 * 
 * يقرأ مالكاً بتسجيله الضريبي وإقامته الضريبية.
 * 
 * Reads an owner with its VAT registration and tax residency.
 */
export async function readPropertyOwner(transport: Transport, args: ReadPropertyOwnerArgs, signal?: AbortSignal): Promise<T.RealEstateParty> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/property-owners/" + encodeURIComponent(args.ownerId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RealEstateParty", response.json) as T.RealEstateParty;
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

export interface ReadPurchaseOrderArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف أمر الشراء. / The purchase order identifier. */
  orderId: string;
}

/**
 * قراءة أمر شراء / Read one purchase order
 * 
 * يقرأ أمر شراء بحالته ومجاميعه و**سطوره بمعرّفاتها**. ولا معرّف قيد له ولا سيكون: أمر الشراء لا يُرحَّل.
 * 
 * Reads a purchase order with its state, its totals, and **its lines with their identifiers**. It carries no entry identifier and never will: a purchase order is not posted.
 */
export async function readPurchaseOrder(transport: Transport, args: ReadPurchaseOrderArgs, signal?: AbortSignal): Promise<T.PurchaseOrder> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/purchase-orders/" + encodeURIComponent(args.orderId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "PurchaseOrder", response.json) as T.PurchaseOrder;
}

export interface ReadPurchaseReturnArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف مرتجع المشتريات. / The purchase return identifier. */
  returnId: string;
}

/**
 * قراءة مرتجع مشتريات / Read one purchase return
 * 
 * يقرأ مرتجع مشتريات بحالته ومجاميعه ومعرّف قيده إن رُحّل. وكانت هذه القراءة **غير موجودة في الوحدة أصلاً**: يُنشأ المرتجع ويُرحَّل ولا توجد جملة تقول «ما حاله الآن؟».
 * 
 * Reads a purchase return with its state, its totals, and its entry identifier if posted. This read **did not exist in the module at all**: a return could be created and posted with no sentence for 'what state is it in now?'.
 */
export async function readPurchaseReturn(transport: Transport, args: ReadPurchaseReturnArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/purchase-returns/" + encodeURIComponent(args.returnId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
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

export interface ReadRentInvoiceArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف فاتورة الإيجار. / The rent invoice identifier. */
  invoiceId: string;
}

/**
 * قراءة فاتورة إيجار / Read one rent invoice
 * 
 * يقرأ الفاتورة بحالتها ومجاميعها وحدثها ومعرّف قيدها إن رُحّلت. ونقطة قراءة: تعمل والاشتراك للقراءة فقط.
 * 
 * Reads the invoice with its state, totals, event, and entry identifier if posted. A read point: it works while the subscription is read-only.
 */
export async function readRentInvoice(transport: Transport, args: ReadRentInvoiceArgs, signal?: AbortSignal): Promise<T.RentInvoice> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/rent-invoices/" + encodeURIComponent(args.invoiceId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RentInvoice", response.json) as T.RentInvoice;
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

export interface ReadSocialInsurancePaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند سداد التأمينات. / The social insurance payment identifier. */
  paymentId: string;
}

/**
 * قراءة سند سداد تأمينات / Read one social insurance payment
 * 
 * يقرأ السند ومعه **ما استُحقّ في فترته من مسيّرات مُرحَّلة** — حصّة المنشأة وحصّة الموظف معاً. والرقمان يُعرضان جنباً إلى جنب ولا يُطرح أحدهما من الآخر هنا: الفرق قرارٌ يقرؤه محاسب.
 * 
 * Reads the document together with **what was accrued in its period by posted runs** — the employer share and the employee share together. The two numbers are shown side by side and neither is subtracted from the other here: the difference is a judgement an accountant reads.
 */
export async function readSocialInsurancePayment(transport: Transport, args: ReadSocialInsurancePaymentArgs, signal?: AbortSignal): Promise<T.HrSocialInsurancePayment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/social-insurance-payments/" + encodeURIComponent(args.paymentId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrSocialInsurancePayment", response.json) as T.HrSocialInsurancePayment;
}

export interface ReadStockBalancesArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
}

/**
 * أرصدة المخزون / Stock balances
 * 
 * يقرأ أرصدة المخزون: الصنف في **موقعه من مستودعه**، بكمّيته ووحدة أساسها وقيمتها ومتوسط تكلفة وحدتها.
 * 
 * **ومفتاح الرصيد أربعة أبعاد**: المنشأة والصنف والمستودع **والموقع**. والموقع بُعدٌ في المفتاح منذ اليوم ولو لم يُسكَّن شيء بعد: إضافته إلى مفتاح رصيدٍ قائم لاحقاً هجرةٌ تُعيد توزيع كل رصيد على مواقع لا يعرفها أحد — أي إعادة كتابة واقعةٍ مضت.
 * 
 * **والكمّية قد تكون سالبة**: البيع قبل إدخال الاستلام واقعة يومية في منشأة عاملة لا حالة خطأ، وتُوسَم ولا تُمنع — لكنها تمنع إقفال الفترة.
 * 
 * وhasCostBasis حقلٌ مستقلّ عن unitCost عمداً: بدونه لا يُفرَّق بين «تكلفة الوحدة صفر لأن الصنف لم يُستلم قط» و«تكلفته صفر فعلاً».
 * 
 * Reads the stock balances: an item in **its location within its warehouse**, with its quantity, that quantity's base unit, its value, and its moving average unit cost.
 * 
 * **The balance key has four dimensions**: company, item, warehouse, **and location**. The location is in the key from today even though nothing is binned yet: adding it to an existing balance key later is a migration that redistributes every balance across locations nobody knows — that is, rewriting a fact that has already happened.
 * 
 * **A quantity may be negative**: selling before the receipt has been entered is a daily occurrence in a working business, not an error state; it is flagged rather than blocked — but it blocks the period close.
 * 
 * hasCostBasis is a field separate from unitCost on purpose: without it there is no way to tell 'the unit cost is zero because the item was never received' from 'its cost really is zero'.
 */
export async function readStockBalances(transport: Transport, args: ReadStockBalancesArgs, signal?: AbortSignal): Promise<T.StockBalanceList> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/stock-balances";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "StockBalanceList", response.json) as T.StockBalanceList;
}

export interface ReadSubscriptionArgs {
  /** معرّف المستأجر. يُطابَق بمستأجر الاعتماد ويُرفض إن اختلف؛ ولا يُفرَّق في الرفض بين «لا وجود له» و«ليس مستأجرك». / The tenant identifier. It is matched against the credential's tenant and refused when it differs; the refusal does not distinguish 'does not exist' from 'not yours'. */
  tenantId: string;
}

/**
 * اشتراك المستأجر: الخطّة والحالة والوحدات وتاريخ التجديد / The tenant's subscription: plan, state, modules, and renewal date
 * 
 * يُرجع الاشتراك الجاري كاملاً في طلب واحد: الخطّة وسعرها نصّاً، وحالة الاشتراك، و**حالة كل وحدة**، وتاريخ التجديد التالي.
 * 
 * وحالةُ الوحدة ثلاث لا أكثر: Entitled تقرأ وتكتب، وReadOnly تقرأ كاملاً ولا تكتب — وهي حالة **الاشتراك المنقطع** — وNotEntitled لم تُشترَ قط. وpostsJournal على كل وحدة يقول إن عملها يبلغ الدفتر، وهو ما يجعل أرضيتها قراءةً لا نزعاً: منشأةٌ رحّلت قيداً واحداً لها دفتر، ولا يُنتزع منها بسبب سداد.
 * 
 * وrenewsOn معدومٌ حين لا يكون الاشتراك فعّالاً: تاريخٌ يُعرض على اشتراك منقطع يُقرأ وعداً بأن الخدمة ستعود من تلقاء نفسها، وهي لا تعود.
 * 
 * **والقراءة حقُّ صاحب الاشتراك**: يبلغها اعتماد المستأجر نفسه بلا شرط دور.
 * 
 * Returns the whole current subscription in one request: the plan and its price as text, the subscription state, **each module's state**, and the next renewal date.
 * 
 * A module's state is one of three: Entitled reads and writes; ReadOnly reads fully and writes nothing — this is the **lapsed subscription** state; NotEntitled was never purchased. postsJournal on each module says its work reaches the ledger, which is what makes its floor read-only rather than removal: a company that posted a single entry has a ledger, and it is not taken away over payment.
 * 
 * renewsOn is null when the subscription is not active: a date shown on a lapsed subscription reads as a promise that service will return by itself, and it does not.
 * 
 * **Reading is the subscriber's own right**: the tenant's own credential reaches it with no role condition.
 */
export async function readSubscription(transport: Transport, args: ReadSubscriptionArgs, signal?: AbortSignal): Promise<T.Subscription> {
  const path = "/api/v1/tenants/" + encodeURIComponent(args.tenantId) + "/subscription";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Subscription", response.json) as T.Subscription;
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

export interface ReadSupplierPaymentArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند الصرف. / The supplier payment identifier. */
  paymentId: string;
}

/**
 * قراءة سند صرف / Read one supplier payment
 * 
 * يقرأ سند صرف بحالته ومجاميعه ومعرّف قيده إن رُحّل. و**net** هو المدفوع و**tax** هو رسوم التحويل و**gross** مجموعهما — أي ما خرج من الخزينة. ولاحظ أنّ ما سقط عن ذمّة المورد هو **net وحده** لا gross.
 * 
 * Reads a supplier payment with its state, its totals, and its entry identifier if posted. Here **net** is the amount paid, **tax** is the transfer fee, and **gross** is their sum — what left the treasury. Note that what came off the supplier's balance is **net alone**, not gross.
 */
export async function readSupplierPayment(transport: Transport, args: ReadSupplierPaymentArgs, signal?: AbortSignal): Promise<T.CommercialDocument> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/supplier-payments/" + encodeURIComponent(args.paymentId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "CommercialDocument", response.json) as T.CommercialDocument;
}

export interface ReadTenantArrearsAgingArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** تاريخ التقرير الميلادي بصيغة yyyy-MM-dd. / The report date in yyyy-MM-dd. */
  asOf: string;
}

/**
 * أعمار متأخرات المستأجرين ومطابقتها / Tenant arrears ageing and its reconciliation
 * 
 * يُرجع أعمار متأخرات المستأجرين حتى تاريخ، **ومعها مطابقتها بنقطة ضبطها في دفتر الأستاذ** في الجواب نفسه.
 * 
 * **ولماذا في الجواب نفسه لا في مورد ثانٍ:** تقريرٌ لا يُفتح لا يكشف انحرافاً، ودفترٌ مساعد ينحرف بصمت عن نقطة ضبطه أشيع عيب في الأنظمة المحاسبية ولا يُكتشف إلا بعد شهور.
 * 
 * **ووجود هذا التقرير أصلاً نتيجةٌ لقرار محاسبي**: القيد يُثبت عند الفوترة لا عند التحصيل، فالذمّة موجودة في الدفتر ويمكن تقادمها. ولو اعتُمدت السياسة البديلة لصار التقرير فارغاً من معناه ووجب سحبه.
 * 
 * Returns tenant arrears ageing as at a date, **together with its reconciliation against its control point in the general ledger**, in the same response.
 * 
 * **Why in the same response rather than a second resource:** a report nobody opens reveals no divergence, and a subledger drifting silently from its control point is the commonest defect in accounting systems and is found only months later.
 * 
 * **That this report exists at all follows from an accounting decision**: the entry is recognised at invoicing rather than at collection, so the receivable exists in the ledger and can age. Had the alternative policy been adopted the report would be void of meaning and would have to be withdrawn.
 */
export async function readTenantArrearsAging(transport: Transport, args: ReadTenantArrearsAgingArgs, signal?: AbortSignal): Promise<T.TenantArrears> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/tenant-arrears-aging";
  const query = new URLSearchParams();
  query.set("asOf", args.asOf);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "TenantArrears", response.json) as T.TenantArrears;
}

export interface ReadTenantReceiptArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف سند القبض من المستأجر. / The tenant receipt identifier. */
  receiptId: string;
}

/**
 * قراءة سند قبض / Read one tenant receipt
 * 
 * يقرأ السند بحالته وحدثه ومعرّف قيده، ومعرّف قيد تخصيصه إن وقع.
 * 
 * Reads the receipt with its state, its event, its entry identifier, and its allocation entry identifier if one occurred.
 */
export async function readTenantReceipt(transport: Transport, args: ReadTenantReceiptArgs, signal?: AbortSignal): Promise<T.TenantReceipt> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/tenant-receipts/" + encodeURIComponent(args.receiptId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "TenantReceipt", response.json) as T.TenantReceipt;
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

export interface ReadUnitArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف الوحدة. / The unit identifier. */
  unitId: string;
}

/**
 * قراءة وحدة / Read one unit
 * 
 * يقرأ وحدةً بتصنيفها — وهو ما يقود شرط خضوع الإيجار للضريبة في قالب الفاتورة.
 * 
 * Reads a unit with its classification — which drives the taxability condition in the invoice template.
 */
export async function readUnit(transport: Transport, args: ReadUnitArgs, signal?: AbortSignal): Promise<T.Unit> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/units/" + encodeURIComponent(args.unitId) + "";
  const url = path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Unit", response.json) as T.Unit;
}

export interface ReconcileEmployeeSubledgerArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** تاريخ المطابقة: تُقرأ الحركة حتى نهايته. / The reconciliation date: movement is read up to and including it. */
  asOf: string;
}

/**
 * مطابقة دفتر الموظف مستنداً بمستند / Reconcile the employee subledger document by document
 * 
 * يطابق دفتر الموظف المساعد بنقطة ضبطه **مستنداً بمستند وطرفاً بطرف**، ويُرجع ما اختلف وحده مع سببه.
 * 
 * **ولا يُنشر فيه رقمٌ واحد اسمه «رصيد الموظف» — وهذا قرارٌ لا نقص.** قارئ نقطة الضبط يجمّع **بلا تفصيل بالحساب** ويعيد صافياً واحداً، ودفتر الموظف يمتدّ على **أصلٍ واحد وثلاثة خصوم**: سلفة، وراتب مستحق، واستقطاع محتجَز، ومخصص نهاية خدمة. فصافٍ واحد يقاصّ سلفةً بمخصص خدمة براتب مستحق، **ويعلن التطابق وهو أعمى** — انحرافان متقابلان يُلغيان بعضهما.
 * 
 * **وهذه المطابقة ممكنة أصلاً لأن الطرفين متساويا الحبيبيّة**: قيدٌ لكل قسيمة يعني حركةً واحدة في نقطة الضبط لكل قسيمة وصفَّ محاولةٍ واحداً في جدول الوحدة لكل قسيمة. ولو رُحِّل المسيّر قيداً واحداً لصار الطرفان بحبيبيّتين مختلفتين ولاستحال هذا الباب.
 * 
 * وسؤال «كم على هذا الموظف من سلفة؟» يُجاب من جداول الوحدة لا من الدفتر.
 * 
 * Reconciles the employee subledger against its control point **document by document and party by party**, returning only what differs, with its reason.
 * 
 * **No single number called 'the employee's balance' is published here — a decision, not a gap.** The control point reader aggregates **without account detail** and returns one net, while the employee subledger spans **one asset and three liabilities**: an advance, a salary payable, a withheld deduction, and an end-of-service provision. One net therefore offsets an advance against a service provision against a salary payable, **and declares agreement while blind** — two opposite divergences cancelling.
 * 
 * **This reconciliation is possible at all because the two sides share one grain**: one entry per payslip means one control-point movement per payslip and one attempt row in the module's table per payslip. Had the run been posted as a single entry, the two sides would carry different grains and this door would be impossible.
 * 
 * The question 'how much advance does this employee owe?' is answered from the module's tables, not from the ledger.
 */
export async function reconcileEmployeeSubledger(transport: Transport, args: ReconcileEmployeeSubledgerArgs, signal?: AbortSignal): Promise<T.HrReconciliation> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employee-subledger-reconciliation";
  const query = new URLSearchParams();
  query.set("asOf", args.asOf);
  const url = query.size > 0 ? path + "?" + query.toString() : path;
  const response = await transport({ method: "GET", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrReconciliation", response.json) as T.HrReconciliation;
}

export interface RecordEmployeeDeductionArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrDeductionRequest;
}

/**
 * قيد جزاء في السجلّ المعتمد / Record a deduction in the approved register
 * 
 * يقيّد جزاءً بفئة سببه ومعتمِده، فيُستقطع داخل مسيّر فترته.
 * 
 * **ولاحظ ما ليس على هذا المورد ولا يجوز أن يوجد: لا …/posting.** الاستقطاع يُرحَّل **داخل المسيّر** لا بذاته، وبابٌ يوحي بغير ذلك يبني عليه العميل شاشةً بزرّ ترحيل لا وجود له — وهو نصّ ما عالجه ADR-0047 في أمر الشراء. ولذلك لا يحمل جوابه entryId ولا alreadyPosted: حقلٌ فارغ يُقرأ «لم يُرحَّل بعد» بدل «لا يُرحَّل».
 * 
 * **ولا حدّ أقصى لنسبة الاستقطاع يُفرَض هنا**: الحدّ النظامي للاستقطاع من الأجر **غير متحقَّق منه**، وحدٌّ مخترَع يرفض مسيّرات مشروعة ويُدرّب المستخدم على الالتفاف. والبند مفتوح على المالك.
 * 
 * **وأيّ الاستقطاعات يُورَّد إلى طرف ثالث وأيّها يبقى للمنشأة سؤالٌ مفتوح كذلك**، والأثر المقيس أن دور الاستقطاعات المستحقّة له سطرٌ دائن واحد في المصفوفة كلّها **ولا سطر مدين واحد** — أي أن حسابه يتراكم ولا يُطفئه شيء. وذلك مكتوبٌ في دَين التحقّق ولم يُبتلع.
 * 
 * Records a deduction with its reason category and its approver, to be deducted inside its period's run.
 * 
 * **Note what this resource does not carry and may not: no …/posting.** A deduction is posted **inside the run**, not by itself, and a door suggesting otherwise leads a client to build a screen with a posting button that does not exist — exactly what ADR-0047 addressed on the purchase order. Hence its response carries neither entryId nor alreadyPosted: an empty field reads as 'not posted yet' instead of 'never posted'.
 * 
 * **No maximum deduction ratio is enforced here**: the regulatory ceiling on deductions from wages is **unverified**, and an invented ceiling refuses legitimate runs and trains users to work around it. The item is open to the owner.
 * 
 * **Which deductions are remitted to a third party and which stay with the company is likewise open**, and the measured effect is that the payroll deductions payable role has exactly one credit line in the whole matrix **and not one debit line** — meaning its account accumulates and nothing ever clears it. That is written into the verification debt rather than swallowed.
 */
export async function recordEmployeeDeduction(transport: Transport, args: RecordEmployeeDeductionArgs, signal?: AbortSignal): Promise<T.HrDeduction> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employee-deductions";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrDeductionRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrDeduction", response.json) as T.HrDeduction;
}

export interface RegisterEmployeeArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrEmployeeRequest;
}

/**
 * تسجيل موظف / Register an employee
 * 
 * يسجّل موظفاً وعلاقة عمله الأولى. **ولا رمز في الحمولة**: الخادم يولّد رمزاً **معتماً** لا يُشتقّ من هوية ولا من آيبان ولا من اسم ولا من تسلسل، وهو **وحده** ما يُكتب في الدفتر المساعد.
 * 
 * **ولماذا معتماً — وهذا قرارٌ بنيوي لا احتياط:** كل ما يدخل ledger.* يدخل البايتات المُجزَّأة في الشكل القانوني v2، وREVOKE UPDATE, DELETE على دور التطبيق يجعل ما دخلها **غير قابل للإزالة**، وعلاجُ المحو الموعود في ADR-0046 — تعميةٌ بمفتاح يُتلَف — لا يبلغ بايتات دخلت سلسلة تجزئة لأن تغييرها يكسر السلسلة. وanتبه أن description_ar_search عمودٌ **مفهرس نصّياً**، فرقمٌ شخصي لا يدخل غيرَ ممحوٍّ فحسب بل **قابلَ البحث** غير ممحوّ.
 * 
 * والهوية والآيبان يسكنان جدولاً منفصلاً واحداً لواحد، **ولا يعودان في أي جواب إلا مقنَّعين**: آخر أربعة محارف وحدها. وقراءة القيمة الكاملة تحتاج استحقاقاً **على مستوى الحقل** لا وجود له في النواة اليوم، وذلك **نقصُ سطحٍ مُعلَن** لا قرارُ منع.
 * 
 * Registers an employee and their first employment. **No code appears in the payload**: the server mints an **opaque** code derived from no national id, no IBAN, no name, and no sequence — and it alone is written into the subledger.
 * 
 * **Why opaque, and this is structural rather than caution:** everything that enters ledger.* enters the hashed bytes of canonical form v2, REVOKE UPDATE, DELETE on the application role makes what entered **unremovable**, and the erasure remedy promised in ADR-0046 — encryption under a destroyable key — cannot reach bytes that entered a hash chain, because changing them breaks the chain. Note also that description_ar_search is a **text-indexed** column, so a personal number enters not merely unerasable but **searchable** and unerasable.
 * 
 * The national id and the IBAN live in a separate one-to-one table and **never come back from any read except masked**: the last four characters only. Reading the full value would need **field-level** entitlement, which the core does not have today; that is a **declared surface gap**, not a prohibition.
 */
export async function registerEmployee(transport: Transport, args: RegisterEmployeeArgs, signal?: AbortSignal): Promise<T.HrEmployee> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employees";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrEmployeeRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrEmployee", response.json) as T.HrEmployee;
}

export interface RegisterTenantArgs {
  /** جسم الطلب. / The request body. */
  body: T.RegisterTenantRequest;
}

/**
 * التسجيل الأول: مستأجر جديد وأول مالك / First registration: a new tenant and its first owner
 * 
 * ينشئ مستأجراً، ويفتح اشتراكه على **خطّة الدخول**، وينشئ أول منشأة له وأول عضوية مالكة فيها، ويردّ **اعتماد انتساب** يُفتح به أول جلسة على POST /api/v1/access/sessions.
 * 
 * **وهذا الباب بلا اعتماد، وذلك بنيوي:** من ليس عنده حساب هو بالضبط من يستعمله. وما لا يُفتح بفتحه: لا يقرأ بيانات مستأجرٍ قائم، ولا يكشف وجود مستأجر آخر — لا برسالة ولا بفارق زمن — ولا يقبل اسم مستأجر مكرَّراً أو فريداً لأن الأسماء ليست هوية أصلاً.
 * 
 * **والخطّة لا تُختار من هنا.** الاشتراك يُفتح على خطّة الدخول وحدها: حقلُ خطّة في جسم طلبٍ بلا اعتماد يمنح الحزمة الشاملة لمن كتب اسمها. وتغييرُ الخطّة فعلٌ آخر باعتماد.
 * 
 * **وحصينٌ ضد التكرار بمفتاح الطلب:** كل معرّفاته — المستأجر ورمزه والمنشأة والمالك — مشتقّة حتمياً من requestKey، فإعادةُ الإرسال به تردّ **المستأجر نفسه** بـ200 وalreadyRegistered = true ولا تُنشئ ثانياً. و**اعتماد الانتساب لا يُعاد في الإعادة**: السرّ يُسكّ مرّة ويُسلَّم مرّة (المُودَع بصمته)، وسكُّ سرٍّ ثانٍ عند كل إعادة إرسال يجعل الباب المفتوح مصنعَ اعتمادات. فمن فقد الاستجابة الأولى يطلب من مالك المنشأة دعوةً جديدة.
 * 
 * **وعليه حدّ معدّل** لكل عنوان ولكل مفتاح طلب: التجاوز يردّ 429 ومعه ترويسة Retry-After.
 * 
 * Creates a tenant, opens its subscription on the **entry plan**, creates its first company and the first owner membership in it, and returns an **enrolment credential** that opens the first session at POST /api/v1/access/sessions.
 * 
 * **This door is unauthenticated, and structurally so:** whoever has no account is exactly who uses it. What it does not open: it reads no existing tenant's data and reveals no other tenant's existence — neither by message nor by measurable timing — and it neither requires nor rejects a duplicate name, because names are not identity here.
 * 
 * **The plan is not chosen here.** The subscription opens on the entry plan alone: a plan field in an unauthenticated body hands the full package to whoever types its name. Changing the plan is a different act, with a credential.
 * 
 * **Idempotent by request key:** every identifier — tenant, tenant code, company, owner — is derived deterministically from requestKey, so resending it returns **the same tenant** with 200 and alreadyRegistered = true and creates no second one. **The enrolment credential is not re-issued on a repeat**: the secret is minted once and handed over once (only its digest is stored), and minting a second secret on every resend turns an open door into a credential factory. Whoever lost the first response asks the company's owner for a fresh invitation.
 * 
 * **It carries a rate limit** per address and per request key: exceeding it returns 429 with a Retry-After header.
 */
export async function registerTenant(transport: Transport, args: RegisterTenantArgs, signal?: AbortSignal): Promise<T.RegisteredTenant> {
  const path = "/api/v1/tenants";
  const url = path;
  const body = encodeSchema(SCHEMAS, "RegisterTenantRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "RegisteredTenant", response.json) as T.RegisteredTenant;
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

export interface RenewSessionArgs {
  /** جسم الطلب. / The request body. */
  body: T.RenewSessionRequest;
}

/**
 * تجديد جلسة بتدوير اعتمادها / Renew a session by rotating its credential
 * 
 * يستهلك اعتماد التجديد الجاري ويُصدر **زوجاً جديداً كاملاً** في الدورة التالية من العائلة نفسها. ومعرّف العائلة لا يتغيّر: هو ما يُبطَل، فلا يهرب المُبطَل بتجديدٍ لاحق.
 * 
 * **وتقديم اعتماد التجديد مرّتين سرقة، والجواب إسقاط العائلة كلّها.** فاعتمادٌ يدور ثم يعود هو اعتمادٌ في يد اثنين — أحدهما ليس صاحبه — ولا يوجد ما يميّز أيّهما. فلا يُخدَم الطلب الثاني، ولا يُخدَم الأول بعده: تُبطَل الجلسة فوراً بـaccess.refresh_replayed ويعود الاثنان إلى الدخول من جديد. والبديل — خدمةُ الثاني — يترك سارقاً بجلسة حيّة ولا يعلم بذلك أحد.
 * 
 * **والكشف يقع قبل الاستحقاق وقبل الانقضاء وقبل الإبطال:** هو إجراء أمنٍ لا امتياز اشتراك، ويجب أن يقع ولو كان المستأجر منقطعاً — بل لا سيّما حينئذ. والصفّ المستهلَك يبقى في قاعدة البيانات ولا يُحذف: **هو الشاهد الوحيد** على إعادة الاستعمال، وحذفُه يجعل اعتماداً مسروقاً يُقرأ «مختلَقاً» فيُرفض الطلب وحده وتبقى الجلسة حيّة.
 * 
 * واعتمادٌ فاعل قُدِّم في موضع اعتماد التجديد لا يُميَّز عن مختلَق: تمييزه كان سيجعل السطح يقول لمن يجرّب «هذا اعتماد موجود ولكن نوعه غير المطلوب».
 * 
 * Consumes the current refresh credential and issues a **whole new pair** in the next generation of the same family. The family identifier does not change: it is what gets revoked, so a revoked session cannot escape by renewing.
 * 
 * **Presenting a refresh credential twice is theft, and the answer is dropping the whole family.** A credential that rotates and then comes back is a credential in two hands — one of them is not its owner's — and nothing distinguishes which. So the second request is not served, and neither is the first afterwards: the session is revoked immediately with access.refresh_replayed and both parties must sign in again. The alternative — serving the second — leaves a thief holding a live session with nobody the wiser.
 * 
 * **Detection happens before entitlement, before expiry, and before revocation:** it is a security act, not a subscription privilege, and it must happen even for a lapsed tenant — especially then. The consumed row stays in the database and is never deleted: **it is the only witness** to reuse, and deleting it makes a stolen credential read as 'forged', refusing the one request while the session stays alive.
 * 
 * An access credential presented where a refresh credential belongs is indistinguishable from a forged one: distinguishing it would have the surface tell a prober 'this credential exists but is of the wrong kind'.
 */
export async function renewSession(transport: Transport, args: RenewSessionArgs, signal?: AbortSignal): Promise<T.AccessSession> {
  const path = "/api/v1/access/sessions/renewal";
  const url = path;
  const body = encodeSchema(SCHEMAS, "RenewSessionRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "AccessSession", response.json) as T.AccessSession;
}

export interface ResumeSubscriptionArgs {
  /** معرّف المستأجر. يُطابَق بمستأجر الاعتماد ويُرفض إن اختلف؛ ولا يُفرَّق في الرفض بين «لا وجود له» و«ليس مستأجرك». / The tenant identifier. It is matched against the credential's tenant and refused when it differs; the refusal does not distinguish 'does not exist' from 'not yours'. */
  tenantId: string;
  /** جسم الطلب. / The request body. */
  body: T.SubscriptionTransitionRequest;
}

/**
 * استئناف الاشتراك / Resume the subscription
 * 
 * يفتح صفّ اشتراك فعّالاً جديداً على الخطّة نفسها، ويُعيد وحداتها إلى الاستحقاق — فتعود الكتابة كما كانت، ولم تُفقد بيانةٌ واحدة أثناء الانقطاع.
 * 
 * **وهو فعل مشغِّل** يُطلب باعتماد التزويد وحده: بابٌ يستأنف به صاحبُ الاشتراك اشتراكه المنقطع هو إلغاءٌ للانقطاع نفسه.
 * 
 * Opens a new active subscription row on the same plan and returns its modules to entitlement — writing resumes exactly as before, and not one datum was lost during the lapse.
 * 
 * **It is an operator act** requested with the provisioning credential alone: a door letting a subscriber resume their own lapsed subscription undoes the lapse itself.
 */
export async function resumeSubscription(transport: Transport, args: ResumeSubscriptionArgs, signal?: AbortSignal): Promise<T.Subscription> {
  const path = "/api/v1/tenants/" + encodeURIComponent(args.tenantId) + "/subscription/resumption";
  const url = path;
  const body = encodeSchema(SCHEMAS, "SubscriptionTransitionRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Subscription", response.json) as T.Subscription;
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

export interface ReviseAttachmentArgs {
  /** معرّف المرفق — غامضٌ عمداً: لا يُشتقّ من اسم ملفّ ولا من مسار، ولا يُقرأ منه شيء عن صاحبه. / The attachment identifier — deliberately opaque: derived from no file name and no path, and telling nothing about its owner. */
  attachmentId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** حمولة multipart: جزءٌ اسمه content يحمل البايتات. / The multipart payload: a part named content carries the bytes. */
  body: FormData;
}

/**
 * إصدار جديد على مرفق / Revise an attachment
 * 
 * يودِع **إصداراً جديداً** يشير إلى سلفه. مورد فرعي مستقلّ لا PUT على المرفق: السجلّ يُضاف إليه ولا يُعدَّل، والتصحيح صفٌّ يشير إلى ما قبله. والسلف **يبقى مقروءاً ببايتاته الأصلية** إلى الأبد.
 * 
 * **والسلسلة خطّية ولا تتفرّع، وتفرضها القاعدة لا الشيفرة**: فهرس فريد جزئي على عمود السلف يرفض الثاني. فتصحيحان متزامنان على السلف نفسه يُنتجان فائزاً واحداً وخاسراً بـ409 ورمزٍ ثابت storage.attachment_already_superseded — **لا 500**، لأن الفحص «هل صُحِّح من قبل؟» لا يمسك السباق والقاعدة وحدها تمسكه.
 * 
 * ومرفقٌ **مسحوب** لا يُصحَّح: السحب حكمٌ نهائي على ذلك الإصدار.
 * 
 * والحمولة هي حمولة الإيداع نفسها وبالشروط نفسها.
 * 
 * Deposits a **new version** that references its predecessor. A separate sub-resource, not a PUT on the attachment: the register is appended to, never modified, and a correction is a row pointing at what came before it. The predecessor **stays readable with its original bytes** forever.
 * 
 * **The chain is linear and does not fork, and the database enforces that, not the code**: a partial unique index on the predecessor column refuses the second. Two concurrent corrections of the same predecessor produce exactly one winner and a loser with 409 and the stable code storage.attachment_already_superseded — **not 500**, because the 'was it already superseded?' check does not catch the race and only the database does.
 * 
 * A **withdrawn** attachment is never corrected: withdrawal is a final verdict on that version.
 * 
 * The payload is the deposit payload, under the same conditions.
 */
export async function reviseAttachment(transport: Transport, args: ReviseAttachmentArgs, signal?: AbortSignal): Promise<T.Attachment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/attachments/" + encodeURIComponent(args.attachmentId) + "/revisions";
  const url = path;
  const body = args.body;
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Attachment", response.json) as T.Attachment;
}

export interface RevokeMembershipArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف العضوية — **وهو معرّف عضوها**: هوية العضوية (المنشأة، العضو)، والمنشأة في المسار سلفاً. / The membership identifier — **which is its member's identifier**: a membership's identity is (company, member), and the company is already in the path. */
  membershipId: string;
}

/**
 * سحب عضوية / Revoke a membership
 * 
 * يسحب عضوية عضوٍ من المنشأة. مورد فرعي مستقلّ لا DELETE على العضوية: السحب **فعلٌ له فاعل ولحظة ويُكتب في سجلّ التدقيق**، وDELETE كان سيقوله «أزل صفّاً».
 * 
 * **ومعرّف العضوية هو معرّف عضوها**: هوية العضوية (المنشأة، العضو) والمنشأة في المسار سلفاً، فلم يبقَ منها ما يُعنون غير العضو. واختراعُ مفتاحٍ بديل كان سيُنتج هويتين لصفٍّ واحد.
 * 
 * **والأثر فوري**: ما تبلغه الجلسة يُقرأ من العضويات في كل طلب، فالصفّ المسحوب يختفي من مجموعة الاعتماد عند الطلب التالي بلا انتظار انقضاء.
 * 
 * **ولا يُسحب آخر مالك** (409 وmembership.last_owner): منشأةٌ بلا مالك لا يستطيع أحد أن يدعو إليها ولا أن يُصلح أدوارها — أي بيانات محبوسة عن أصحابها بفعلٍ يبدو إدارياً.
 * 
 * **وهو فعل مالك** في المنشأة: من يستطيع أن يسحب عضويةً يستطيع أن يُخلي المنشأة لنفسه.
 * 
 * Revokes a member's membership in the company. A subresource, not a DELETE on the membership: revocation is **an act with an actor and an instant, written to the audit log**; DELETE would have called it 'remove a row'.
 * 
 * **A membership's identifier is its member's identifier**: its identity is (company, member) and the company is already in the path, so nothing but the member is left to address. Inventing a surrogate key would give one row two identities.
 * 
 * **The effect is immediate**: what a session reaches is read from memberships on every request, so the revoked row leaves the credential's set on the next request without waiting for an expiry.
 * 
 * **The last owner is not revoked** (409, membership.last_owner): a company without an owner is one nobody can invite into or repair roles in — data locked away from its owners by an act that looks administrative.
 * 
 * **It is an owner's act** in the company: whoever can revoke a membership can empty the company for themselves.
 */
export async function revokeMembership(transport: Transport, args: RevokeMembershipArgs, signal?: AbortSignal): Promise<T.MembershipRevocation> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/memberships/" + encodeURIComponent(args.membershipId) + "/revocation";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "MembershipRevocation", response.json) as T.MembershipRevocation;
}

/**
 * إبطال الجلسة الجارية / Revoke the current session
 * 
 * يُبطل العائلة التي أُصدر منها الاعتماد المُقدَّم — **فوراً**. والإبطال يُقرأ في استعلام حلّ الاعتماد نفسه على الطلب التالي مباشرة، ولا يُنتظر به انقضاء: «سُحب هذا الاعتماد» جملةٌ إمّا أن تكون صحيحة الآن أو لا تكون.
 * 
 * **ويقع على العائلة لا على الاعتماد المفرد:** إبطالُ اعتمادٍ واحد يترك اعتماد تجديده حيّاً فيُصدر بديلاً بعد ثوانٍ — أي أن «أبطلتُ الجلسة» تكون قد كذبت.
 * 
 * ولا جسم لهذا الطلب: الاعتماد المُقدَّم هو الذي يسمّي ما يُبطَل، والسبب رمزٌ مغلق (signed_out) لا نصّاً يكتبه أحد. ونداءٌ ثانٍ على جلسة مُبطَلة لا يقع أصلاً — الاعتماد نفسه يُرفض عند الحدّ بـauth.credential_revoked.
 * 
 * **واعتماد التزويد المُهيَّأ من الإعداد لا عائلة له**، فيُرفض هنا بـ409 وaccess.session_not_issued_here: قولُ ذلك برمزه أصدق من ردّ «تمّ» على فعلٍ لم يقع.
 * 
 * Revokes the family the presented credential was issued from — **immediately**. Revocation is read in the credential resolution query itself on the very next request and never waits for an expiry: 'this credential is withdrawn' is a sentence that is either true now or not true at all.
 * 
 * **It applies to the family, not to a single credential:** revoking one credential leaves its refresh credential alive to mint a replacement seconds later — which would make 'I revoked the session' a lie.
 * 
 * This request has no body: the presented credential names what is revoked, and the reason is a closed code (signed_out) rather than prose anyone writes. A second call on a revoked session never happens — the credential itself is refused at the boundary with auth.credential_revoked.
 * 
 * **The configured provisioning credential has no family**, so it is refused here with 409 and access.session_not_issued_here: saying so by its code is more honest than answering 'done' to an act that did not happen.
 */
export async function revokeSession(transport: Transport, signal?: AbortSignal): Promise<T.SessionRevocation> {
  const path = "/api/v1/access/sessions/revocation";
  const url = path;
  const response = await transport({ method: "POST", url, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "SessionRevocation", response.json) as T.SessionRevocation;
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

export interface TerminateEmployeeArgs {
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** معرّف الموظف. وهو **غير** رمزه المعتم: الرمز هو ما يعبر إلى الدفتر المساعد، والمعرّف عنوانٌ على هذا السطح. / The employee identifier. It is **not** the opaque code: the code is what crosses into the subledger, while the identifier is an address on this surface. */
  employeeId: string;
  /** جسم الطلب. / The request body. */
  body: T.HrTerminationRequest;
}

/**
 * إنهاء خدمة موظف / Terminate an employee
 * 
 * يُنهي علاقة العمل السارية بتاريخها وبمفتاح سببها. **مورد فرعي مستقلّ لا PUT بحقل حالة**، بنفس سابقة …/suspension على مركز التكلفة و…/reversal على القيد.
 * 
 * **وهو ما يفتح المخالصة**: مخالصة على علاقة عمل ما تزال سارية تُرفض بـhr.employment_not_terminated.
 * 
 * ومفتاح السبب **رمزٌ يقرؤه برنامج** من مجموعة يملكها المستدعي، لا نصٌّ يُعرض؛ ولا يُصنَّف هنا إلى «استقالة» و«إنهاء» لأن أثر التمييز على الاستحقاق **بندٌ مفتوح على المالك**، وتصنيفٌ يفترض جوابه يُبنى عليه حساب.
 * 
 * Ends the active employment with its date and a reason key. **A separate sub-resource, not a PUT on a status field**, following the same precedent as …/suspension on a cost centre and …/reversal on an entry.
 * 
 * **It is what opens the final settlement**: a settlement against an employment that is still active is refused with hr.employment_not_terminated.
 * 
 * The reason key is a **code a program reads**, from a set the caller owns — not displayed text; and it is not classified here into 'resignation' and 'dismissal', because the effect of that distinction on the entitlement is an **open owner question**, and a classification that assumes its answer gets computed upon.
 */
export async function terminateEmployee(transport: Transport, args: TerminateEmployeeArgs, signal?: AbortSignal): Promise<T.HrEmployee> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/employees/" + encodeURIComponent(args.employeeId) + "/termination";
  const url = path;
  const body = encodeSchema(SCHEMAS, "HrTerminationRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "HrEmployee", response.json) as T.HrEmployee;
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

export interface WithdrawAttachmentArgs {
  /** معرّف المرفق — غامضٌ عمداً: لا يُشتقّ من اسم ملفّ ولا من مسار، ولا يُقرأ منه شيء عن صاحبه. / The attachment identifier — deliberately opaque: derived from no file name and no path, and telling nothing about its owner. */
  attachmentId: string;
  /** معرّف الشركة. النطاق يُشتق من المسار ويُطابَق بالاعتماد؛ ولا يوجد حقل شركة في الجسم. / The company identifier. Scope comes from the path and is matched against the credential; there is no company field in any body. */
  companyId: string;
  /** جسم الطلب. / The request body. */
  body: T.WithdrawAttachmentRequest;
}

/**
 * سحب مرفق / Withdraw an attachment
 * 
 * يضع علامة سحب على مرفق. مورد فرعي مستقلّ لا DELETE: **لا بايتة تُحذف ولا بصمة تُمحى** — الاحتفاظ بسند القيد واجب نظامي، والسحب إعلانُ حالة لا محو.
 * 
 * والعلامة **صفٌّ في جدول ثانٍ** مفتاحه المرفق نفسه، فلا يُسحب مرّتين — والقاعدة تقولها لا الشيفرة. ولا صلاحية DELETE على جدول العلامات أيضاً: «سحبٌ ثم تراجعٌ عن السحب» عمليةٌ بلا أثر، وهي بالضبط الرواية التي يمنعها السجلّ.
 * 
 * **والسبب مفتاحٌ من مجموعة يملكها المستدعي، لا نصّ حرّ**: نصٌّ حرّ يُكتب بلغة كاتبه ثم يُقرأ في تقرير بلغة أخرى، ولا يُرشَّح عليه ولا يُترجَم.
 * 
 * Marks an attachment as withdrawn. A separate sub-resource, not a DELETE: **no byte is deleted and no digest is erased** — retaining the evidence behind an entry is a regulatory duty, and a withdrawal declares a state rather than erasing one.
 * 
 * The marker is **a row in a second table** keyed by the attachment itself, so it is never withdrawn twice — and the database says so, not the code. There is no DELETE grant on the marker table either: 'withdraw then un-withdraw' is an operation that leaves no trace, which is exactly the story this register exists to prevent.
 * 
 * **The reason is a key from a set the caller owns, not free text**: free text is written in its author's language and read in a report in another, is never filtered on, and is never translated.
 */
export async function withdrawAttachment(transport: Transport, args: WithdrawAttachmentArgs, signal?: AbortSignal): Promise<T.Attachment> {
  const path = "/api/v1/companies/" + encodeURIComponent(args.companyId) + "/attachments/" + encodeURIComponent(args.attachmentId) + "/withdrawal";
  const url = path;
  const body = encodeSchema(SCHEMAS, "WithdrawAttachmentRequest", args.body as unknown);
  const response = await transport({ method: "POST", url, body, signal });
  if (!response.ok) throw ProblemError.from(response);
  return decodeSchema(SCHEMAS, "Attachment", response.json) as T.Attachment;
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
