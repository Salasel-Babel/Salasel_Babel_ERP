/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     e2ba8112a2421c49813d9475f8849856d96e8af4f94d9cc0d071498442a6e2ca
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
