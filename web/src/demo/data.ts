/* ═══════════════════════════════════════════════════════════════════════════
   بيانات العرض — **لقطة من قاعدة الشركة التجريبية المبذورة، لا رقم مُختلَق**
   ───────────────────────────────────────────────────────────────────────────
   يُنتج الملفّ `data/snapshot.json` سكربتٌ واحد يقرأ PostgreSQL:
       demo/showcase/export-snapshot.sh > web/src/demo/data/snapshot.json
   وكلّ رقم يظهر في العرض يعود إلى صفّ في `ledger.journal_line` أو
   `sales.sales_invoice` أو `purchasing.supplier_bill`.

   والحساب على المال هنا **بأعداد صحيحة بمقياس ١٠٠٠٠** (BigInt) لا بفاصلة
   عائمة: القاعدة تخزّن `NUMERIC(19,4)`، والمتصفّح يجمع أعداداً صحيحة ثم يعيد
   بناء النصّ. ولا تمرّ قيمة مالية على `Number` في أي خطوة (القاعدة 4).
   ═══════════════════════════════════════════════════════════════════════════ */
import raw from "./data/snapshot.json?raw";
import { Money } from "../api/money";

/** سطر في قيد يومية كما هو في `ledger.journal_line`. */
export interface SnapLine {
  readonly lineNo: number;
  readonly accountCode: string;
  readonly accountName: string;
  readonly roleCode: string;
  readonly debit: string;
  readonly credit: string;
  readonly costCenter: string | null;
  readonly branch: string | null;
  readonly party: string | null;
  readonly descriptionAr: string;
}

/** قيد يومية مُرحَّل، ومعه حلقته في سلسلة البصمات. */
export interface SnapEntry {
  readonly entryNo: number;
  readonly entryId: string;
  readonly entryDate: string;
  readonly periodCode: string;
  readonly postedAt: string;
  readonly status: string;
  readonly memoAr: string;
  readonly sourceModule: string;
  readonly sourceDocType: string;
  readonly sourceDocId: string;
  readonly eventCode: string;
  readonly triggerCode: string;
  readonly currency: string;
  readonly chainSeq: number | null;
  readonly entryHash: string | null;
  readonly prevHash: string | null;
  readonly canonVersion: string | null;
  readonly lines: readonly SnapLine[];
}

/** سطر في مستند المصدر. */
export interface SnapDocLine {
  readonly lineNo: number;
  readonly descriptionAr: string;
  readonly quantity: string;
  readonly unitPrice: string;
  readonly taxRate: string;
  readonly lineNet: string;
  readonly lineTax: string;
}

/** مستند مصدر: فاتورة مبيعات أو فاتورة مورّد. */
export interface SnapDoc {
  readonly docId: string;
  readonly number: string;
  readonly partyCode: string;
  readonly partyNameAr: string;
  readonly partyVat?: string;
  readonly issuedOn: string;
  readonly dueOn: string;
  readonly state: string;
  readonly currency: string;
  readonly expenseCategory?: string;
  readonly netTotal: string;
  readonly taxTotal: string;
  readonly grossTotal: string;
  readonly lines: readonly SnapDocLine[];
}

/** حساب بحركة. */
export interface SnapAccount {
  readonly accountCode: string;
  readonly nameAr: string;
  /** كل ترجمة موجودة، صفوفاً لا عمودَي ar/en (ADR-0021). */
  readonly translations: Readonly<Record<string, string>> | null;
  readonly nature: string;
  readonly accountType: string;
  readonly statementSection: string;
}

/** إجماليات اللقطة، مقروءة من `sum()` في PostgreSQL لا من جمع في المتصفّح. */
export interface SnapTotals {
  readonly entryCount: number;
  readonly lineCount: number;
  readonly totalDebit: string;
  readonly totalCredit: string;
  readonly accountCount: number;
  readonly chartSize: number;
  readonly roleCount: number;
  readonly mapRows: number;
}

/** اللقطة كاملةً. */
export interface Snapshot {
  readonly companyId: string;
  readonly generatedFrom: string;
  readonly totals: SnapTotals;
  readonly accounts: readonly SnapAccount[];
  readonly entries: readonly SnapEntry[];
  readonly salesInvoices: readonly SnapDoc[];
  readonly supplierBills: readonly SnapDoc[];
}

/** اللقطة المُحمَّلة مرّة واحدة. */
export const snapshot: Snapshot = JSON.parse(raw) as Snapshot;

const SCALE = 10_000n;

/**
 * يحوّل نصّاً عشرياً بمقياس ≤٤ إلى عدد صحيح بمقياس ١٠٠٠٠.
 * @param text نصّ الرقم كما ورد من القاعدة.
 */
export function toScaled(text: string): bigint {
  const negative = text.startsWith("-");
  const body = negative ? text.slice(1) : text;
  const dot = body.indexOf(".");
  const whole = dot < 0 ? body : body.slice(0, dot);
  const frac = (dot < 0 ? "" : body.slice(dot + 1)).padEnd(4, "0").slice(0, 4);
  const value = BigInt(whole === "" ? "0" : whole) * SCALE + BigInt(frac === "" ? "0" : frac);
  return negative ? -value : value;
}

/**
 * يعيد بناء النصّ العشري من عدد صحيح بمقياس ١٠٠٠٠.
 * @param value القيمة المقيسة.
 */
export function fromScaled(value: bigint): string {
  const negative = value < 0n;
  const abs = negative ? -value : value;
  const whole = abs / SCALE;
  const frac = (abs % SCALE).toString().padStart(4, "0");
  return (negative ? "-" : "") + whole.toString() + "." + frac;
}

/**
 * مبلغٌ للعرض من قيمة مقيسة.
 * @param value القيمة المقيسة.
 */
export function money(value: bigint): Money {
  return Money.wire(fromScaled(value));
}

/**
 * مبلغٌ للعرض من نصّ السلك كما هو.
 * @param text النصّ.
 */
export function wire(text: string): Money {
  return Money.wire(text);
}

/** كل مستندات المصدر مفهرسةً بمعرّفها. */
export const documentsById: ReadonlyMap<string, SnapDoc> = new Map(
  [...snapshot.salesInvoices, ...snapshot.supplierBills].map((d) => [d.docId, d])
);

/** القيود مرتّبةً بالتاريخ ثم بالرقم — وهو ترتيب إعادة تشغيل الدفتر. */
export const entriesByDate: readonly SnapEntry[] = [...snapshot.entries].sort((a, b) =>
  a.entryDate === b.entryDate ? a.entryNo - b.entryNo : a.entryDate < b.entryDate ? -1 : 1
);

/** كل يوم فيه حركة، مرتّباً. */
export const activeDays: readonly string[] = [...new Set(entriesByDate.map((e) => e.entryDate))];

/** رصيد حساب في لحظة إعادة التشغيل. */
export interface ReplayRow {
  readonly accountCode: string;
  readonly nameAr: string;
  readonly debit: bigint;
  readonly credit: bigint;
}

/** حالة الدفتر عند يوم بعينه. */
export interface ReplayState {
  readonly day: string;
  readonly rows: readonly ReplayRow[];
  readonly totalDebit: bigint;
  readonly totalCredit: bigint;
  readonly entryCount: number;
  readonly balanced: boolean;
}

/**
 * يعيد تشغيل الدفتر يوماً بيوم. دفترٌ يُضاف إليه فقط **هو** سلسلة زمنية:
 * لا حالة سابقة تُعاد كتابتها، فالتراكم كافٍ ولا حاجة إلى لقطات محفوظة.
 * @param days الأيام المطلوبة مرتّبة.
 */
export function replay(days: readonly string[]): readonly ReplayState[] {
  const debit = new Map<string, bigint>();
  const credit = new Map<string, bigint>();
  const names = new Map(snapshot.accounts.map((a) => [a.accountCode, a.nameAr]));
  const states: ReplayState[] = [];
  let cursor = 0;
  let entryCount = 0;

  for (const day of days) {
    while (cursor < entriesByDate.length && entriesByDate[cursor]!.entryDate <= day) {
      const entry = entriesByDate[cursor]!;
      entryCount += 1;
      for (const line of entry.lines) {
        debit.set(line.accountCode, (debit.get(line.accountCode) ?? 0n) + toScaled(line.debit));
        credit.set(line.accountCode, (credit.get(line.accountCode) ?? 0n) + toScaled(line.credit));
      }
      cursor += 1;
    }

    const codes = [...new Set([...debit.keys(), ...credit.keys()])].sort();
    let totalDebit = 0n;
    let totalCredit = 0n;
    const rows: ReplayRow[] = [];
    for (const code of codes) {
      const d = debit.get(code) ?? 0n;
      const c = credit.get(code) ?? 0n;
      totalDebit += d;
      totalCredit += c;
      rows.push({ accountCode: code, nameAr: names.get(code) ?? code, debit: d, credit: c });
    }
    states.push({ day, rows, totalDebit, totalCredit, entryCount, balanced: totalDebit === totalCredit });
  }

  return states;
}

/**
 * كل الأيام بين أول حركة وآخرها — لا أيام الحركة وحدها، كي يتحرّك المؤشّر
 * بسرعة زمنية ثابتة لا بسرعة تعتمد على كثافة القيود.
 */
export function calendarDays(): readonly string[] {
  const first = activeDays[0]!;
  const last = activeDays[activeDays.length - 1]!;
  const out: string[] = [];
  const cursor = new Date(first + "T00:00:00Z");
  const end = new Date(last + "T00:00:00Z");
  while (cursor.getTime() <= end.getTime()) {
    out.push(cursor.toISOString().slice(0, 10));
    cursor.setUTCDate(cursor.getUTCDate() + 1);
  }
  return out;
}
