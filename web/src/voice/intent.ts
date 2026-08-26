/* ═══════════════════════════════════════════════════════════════════════════
   قراءة النيّة من كلام عربي — حتمية، بلا شبكة، وبلا نموذج.
   ───────────────────────────────────────────────────────────────────────────
   لماذا حتمية أولاً والنموذج ثانياً: لأن هذا المسار هو **ما يعمل حين لا يعمل
   شيء** — بلا مفتاح، وبلا إنترنت، وفي قاعة عرض شبكتُها مغلقة. والنموذج يُضيف
   الحالة العامّة فوقه، ولا يصير شرطاً لظهور الأثر.

   وما تُنتجه هذه الدالّة **مسوّدة لا قيد**. كل قيمة تحمل مصدرها، ومصدر المنطوق
   واجبه «يراجع» — لا «يلمح» (ADR-0024).

   ⚠ رموز الأحداث أدناه **مغلقة ومحروسة عبر الحدّ**: اختبار في
   tests/Babel.Ai.Tests يقرأ هذا الملف نفسه ويتحقّق أن كل رمز فيه موجود في
   data/posting-matrix. ورمزٌ مخترَع قيس في هذا المستودع وهو يُنتج ترحيلاً
   مكرَّراً صامتاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readArabicNumber, strip } from "./arabic-number";

/** مصدر القيمة — نظير FieldProvenance في الخادم، بالأسماء نفسها. */
export type Provenance = "attested" | "read" | "inferred" | "defaulted" | "typed" | "spoken";

/** قيمة ملتقَطة ومعها مصدرها. لا قيمة بلا مصدر في هذا المسار. */
export interface SpokenValue {
  /** مفتاح الحقل — يطابق ثوابت CapturedInvoiceDraft في الخادم. */
  readonly field: string;
  /** القيمة نصّاً دائماً. المال نصّ، ولا عائمة في هذا المسار كلّه. */
  readonly text: string;
  /** المصدر. */
  readonly provenance: Provenance;
  /** درجة الثقة، وللمقروء والمُستنتَج والمنطوق وحدها. */
  readonly confidence?: number;
  /** المقطع من الكلام الذي أنتج القيمة — يُعرض كي يرى الإنسان **لماذا**. */
  readonly heard?: string;
}

/** نتيجة القراءة. */
export interface SpokenIntent {
  readonly values: readonly SpokenValue[];
  /** رموز أعطال مُسمّاة وقعت أثناء القراءة — تُعرض ولا تُبتلع. */
  readonly faults: readonly string[];
}

/** رمز حدث مقترح ومفاتيحه الكلامية. */
interface EventRule {
  readonly code: string;
  readonly keywords: readonly string[];
}

/**
 * القائمة المغلقة. **الترتيب معنوي**: الأخصّ أولاً، فـ«فاتورة مصروف» لا تُلتقط
 * بقاعدة «فاتورة» العامّة.
 */
export const SPOKEN_EVENT_RULES: readonly EventRule[] = [
  { code: "purchasing.invoice.expense.posted", keywords: ["مصروف", "مصاريف", "فاتورة مصروف", "بلا مخزون"] },
  { code: "purchasing.invoice.stock.posted", keywords: ["فاتورة مشتريات", "مشتريات", "بضاعة", "مخزنية", "شراء بضاعة"] },
  { code: "purchasing.payment.posted", keywords: ["سند صرف", "دفعت للمورد", "سددت للمورد"] },
  { code: "assets.purchase.posted", keywords: ["أصل ثابت", "اصل ثابت", "شراء أصل", "شراء اصل"] },
  { code: "treasury.bank_charge.posted", keywords: ["رسوم بنكية", "مصاريف بنكية", "عمولة بنك"] },
];

/** كل رمز حدث تنطق به الواجهة. يقرؤها حارسٌ في الخادم ويطابقها بالمصفوفة. */
export const SPOKEN_EVENT_CODES: readonly string[] = SPOKEN_EVENT_RULES.map((rule) => rule.code);

/** مفاتيح الحقول — نفس ثوابت CapturedInvoiceDraft حرفياً. */
export const FIELD = {
  sellerName: "seller_name",
  invoiceNumber: "invoice_number",
  issuedOn: "issued_on",
  taxRate: "tax_rate",
  grossTotal: "gross_total",
  suggestedEvent: "suggested_event",
} as const;

const CURRENCY_WORDS = ["ريال", "ريالا", "ريالاً", "ريالات", "ريالين"].map(strip);
const AMOUNT_MARKERS = ["بمبلغ", "مبلغ", "بقيمة", "قيمتها", "قيمته", "الاجمالي", "اجمالي", "المجموع"].map(strip);
const PERCENT_WORDS = ["بالمئة", "بالمائة", "المئة", "المائة", "٪", "%"].map(strip);
const SUPPLIER_MARKERS = ["من", "المورد", "مورد", "لصالح"].map(strip);
const NUMBER_MARKERS = ["رقم", "رقمها", "برقم"].map(strip);
const STOP_WORDS = [
  ...AMOUNT_MARKERS, ...CURRENCY_WORDS, ...PERCENT_WORDS, ...NUMBER_MARKERS,
  ...["بتاريخ", "تاريخ", "اليوم", "امس", "ضريبة", "الضريبة", "وضريبة"].map(strip),
];

const TODAY_WORDS = ["اليوم"].map(strip);
const YESTERDAY_WORDS = ["امس", "البارحة"].map(strip);

function words(text: string): string[] {
  return text
    .split(/[\s،,.]+/)
    .map(strip)
    .filter((w) => w.length > 0);
}

function isNumberish(word: string): boolean {
  return readArabicNumber(word).ok || word === "و";
}

/** يجمع أطول مقطع عددي يبدأ عند الموضع. */
function numberSpan(tokens: string[], from: number): { text: string; next: number } | null {
  let end = from;
  while (end < tokens.length && isNumberish(tokens[end] ?? "")) end++;
  while (end > from && tokens[end - 1] === "و") end--;
  if (end === from) return null;
  return { text: tokens.slice(from, end).join(" "), next: end };
}

/**
 * يقرأ نيّة فاتورة مورد من كلام عربي.
 * @param transcript التفريغ كما ورد من المتصفّح.
 * @param options خيارات — النسبة النظامية وتاريخ اليوم، وكلاهما يُحقن كي يكون الاختبار حتمياً.
 */
export function readInvoiceIntent(
  transcript: string,
  options: { statutoryTaxRate?: string; today?: string } = {}
): SpokenIntent {
  const values: SpokenValue[] = [];
  const faults: string[] = [];
  const tokens = words(transcript ?? "");
  if (tokens.length === 0) return { values, faults: ["ai.voice.transcript_empty"] };

  let amount: { text: string; heard: string } | null = null;
  let rate: string | null = null;
  let invoiceNumber: string | null = null;
  let seller: string | null = null;
  let issuedOn: string | null = null;

  for (let i = 0; i < tokens.length; i++) {
    const token = tokens[i] ?? "";

    /* النسبة: عدد يليه «بالمئة». يُفحص قبل المبلغ كي لا يبتلعه. */
    const span = numberSpan(tokens, i);
    if (span) {
      const after = tokens[span.next];
      const reading = readArabicNumber(span.text);

      if (!reading.ok) {
        if (!faults.includes(reading.code)) faults.push(reading.code);
      } else if (after && PERCENT_WORDS.includes(after)) {
        rate = divideByHundred(reading.text);
        i = span.next;
        continue;
      } else if ((after && CURRENCY_WORDS.includes(after)) || (i > 0 && AMOUNT_MARKERS.includes(tokens[i - 1] ?? ""))) {
        amount = { text: reading.text, heard: span.text };
        i = span.next - 1;
        continue;
      } else if (i > 0 && NUMBER_MARKERS.includes(tokens[i - 1] ?? "")) {
        invoiceNumber = span.text;
        i = span.next - 1;
        continue;
      }
    }

    /* اسم المورد: ما بين علامة المورد وأول كلمة إيقاف. */
    if (SUPPLIER_MARKERS.includes(token) && seller === null) {
      const parts: string[] = [];
      let j = i + 1;
      while (j < tokens.length) {
        const next = tokens[j] ?? "";
        if (STOP_WORDS.includes(next) || isNumberish(next)) break;
        parts.push(next);
        j++;
      }
      if (parts.length > 0) {
        seller = parts.join(" ");
        i = j - 1;
      }
      continue;
    }

    if (TODAY_WORDS.includes(token) && options.today) issuedOn = options.today;
    else if (YESTERDAY_WORDS.includes(token) && options.today) issuedOn = shiftDays(options.today, -1);
  }

  if (seller) values.push({ field: FIELD.sellerName, text: seller, provenance: "spoken", confidence: 0.8, heard: seller });
  if (invoiceNumber) values.push({ field: FIELD.invoiceNumber, text: invoiceNumber, provenance: "spoken", confidence: 0.8, heard: invoiceNumber });
  if (amount) values.push({ field: FIELD.grossTotal, text: amount.text, provenance: "spoken", confidence: 0.85, heard: amount.heard });
  else faults.push("ai.voice.no_amount_heard");

  if (rate) values.push({ field: FIELD.taxRate, text: rate, provenance: "spoken", confidence: 0.85 });
  else if (options.statutoryTaxRate) values.push({ field: FIELD.taxRate, text: options.statutoryTaxRate, provenance: "defaulted" });

  if (issuedOn) values.push({ field: FIELD.issuedOn, text: issuedOn, provenance: "spoken", confidence: 0.9 });
  else if (options.today) values.push({ field: FIELD.issuedOn, text: options.today, provenance: "defaulted" });

  const event = matchEvent(transcript ?? "");
  if (event) values.push({ field: FIELD.suggestedEvent, text: event, provenance: "inferred", confidence: 0.7 });
  else faults.push("ai.voice.no_event_heard");

  return { values, faults };
}

/** يطابق رمز حدث من القائمة المغلقة، أو لا شيء. **لا يخترع رمزاً بحال**. */
export function matchEvent(transcript: string): string | null {
  const text = strip(transcript ?? "");
  for (const rule of SPOKEN_EVENT_RULES) {
    for (const keyword of rule.keywords) {
      if (text.includes(strip(keyword))) return rule.code;
    }
  }
  return null;
}

/* قسمة نصّية على مئة: النسبة كسر عشري، و«خمسة عشر بالمئة» = 0.15 لا 15. */
function divideByHundred(text: string): string {
  const negative = text.startsWith("-");
  const digits = (negative ? text.slice(1) : text).replace(".", "");
  const dot = text.indexOf(".");
  const fracLength = (dot < 0 ? 0 : text.length - dot - 1) + 2;
  const padded = digits.padStart(fracLength + 1, "0");
  const cut = padded.length - fracLength;
  const out = padded.slice(0, cut) + "." + padded.slice(cut);
  return (negative ? "-" : "") + out.replace(/(\.\d*?)0+$/, "$1").replace(/\.$/, "");
}

function shiftDays(iso: string, days: number): string {
  const parts = iso.split("-").map(Number);
  const date = new Date(Date.UTC(parts[0] ?? 1970, (parts[1] ?? 1) - 1, (parts[2] ?? 1) + days));
  return date.toISOString().slice(0, 10);
}
