/* ═══════════════════════════════════════════════════════════════════════════
   بياناتٌ واقعية الشكل لصفحة العرض
   ───────────────────────────────────────────────────────────────────────────
   **لا تلمس هذه الأرقام خادماً ولا دفتراً**: هي أشكالُ بيانات لصفحة تعرض
   نظام التصميم. وهي مع ذلك تخضع لقاعدة المستودع كاملةً — كل مبلغ نصٌّ يمرّ
   بـ{@link Money}، ولا عائم في خطوة واحدة. ولو خالفتها هنا لتعلّم من ينسخها
   المخالفةَ في شاشةٍ حقيقية.

   والميزان **متوازن** عمداً: 802,871.25 في الطرفين. ميزانٌ غير متوازن في
   صفحة عرضٍ يعلّم العين أن الاختلال طبيعي.
   ═══════════════════════════════════════════════════════════════════════════ */
import { Money } from "../../api/money";
import type { LedgerRow } from "../../ui";

/** صفٌّ خام قبل أن يصير {@link LedgerRow}. */
interface RawRow {
  readonly code: string;
  readonly name: string;
  readonly debit: string;
  readonly credit: string;
  readonly inferred?: boolean;
}

const RAW: readonly RawRow[] = [
  { code: "110101", name: "النقد في الصندوق", debit: "42500.0000", credit: "0.0000" },
  { code: "110201", name: "البنك — الحساب الجاري", debit: "318940.7500", credit: "0.0000" },
  { code: "110301", name: "ذمم العملاء", debit: "96220.0000", credit: "0.0000" },
  { code: "120101", name: "المخزون — المستودع الرئيسي", debit: "154310.5000", credit: "0.0000" },
  { code: "210101", name: "ذمم الموردين", debit: "0.0000", credit: "88412.2500" },
  { code: "213101", name: "ضريبة القيمة المضافة — مخرجات", debit: "0.0000", credit: "21105.6000" },
  { code: "216101", name: "مصروفات مستحقة", debit: "0.0000", credit: "47473.4000", inferred: true },
  { code: "310101", name: "رأس المال", debit: "0.0000", credit: "400000.0000" },
  { code: "410101", name: "إيرادات المبيعات", debit: "0.0000", credit: "245880.0000" },
  { code: "510101", name: "تكلفة المبيعات", debit: "128900.0000", credit: "0.0000" },
  { code: "520301", name: "رواتب وأجور", debit: "62000.0000", credit: "0.0000" },
];

/**
 * صفوف الميزان لصفحة العرض.
 * @param arrived هل تُوسَم الصفوف بأنها وصلت للتوّ.
 */
export function demoRows(arrived: boolean): readonly LedgerRow[] {
  return RAW.map((r) => ({
    id: r.code,
    code: r.code,
    name: r.name,
    debit: Money.wire(r.debit),
    credit: Money.wire(r.credit),
    arrived,
    inferred: r.inferred,
  }));
}

/** مجموع المدين. محسوبٌ مرّةً بيدٍ ومكتوبٌ نصّاً — لا جمعَ عائم في المتصفّح. */
export const DEMO_TOTAL_DEBIT = Money.wire("802871.2500");
/** مجموع الدائن. */
export const DEMO_TOTAL_CREDIT = Money.wire("802871.2500");
/** مبلغٌ مفردٌ لبطاقات الإحصاء. */
export const DEMO_VAT = Money.wire("21105.6000");
/** فرقُ الميزان — صفرٌ، وهو ما يجب أن يكون. */
export const DEMO_DIFFERENCE = Money.wire("0.0000");
