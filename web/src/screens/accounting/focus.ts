/* ═══════════════════════════════════════════════════════════════════════════
   ما تحمله شاشات دورة المستندات بعضها إلى بعض  ·  What the screens hand on
   ───────────────────────────────────────────────────────────────────────────
   الدورة سلسلةٌ لا شاشاتٌ متجاورة: **أمرُ شراءٍ يُنشئ معرّفاً هو مدخلُ
   الاستلام**، واستلامٌ يُرحَّل تصير فاتورةُ مورّده هي التالية، وفاتورةٌ
   تُرحَّل يُخصَّص عليها سندُ صرف. فالمعرّف يعبر بين الشاشات ولا يُكتب بيد
   مرّتين — ومع ذلك **كل شاشةٍ تقبله مكتوباً بيد** فتبقى صالحةً وحدها.

   ولا يُحمَل في الرابط: معرّفُ المستند **مفتاحٌ محاسبي** هو `DocumentId` في
   هوية الإحكام، لا وسيطُ عرض. ووضعُه في شريط العنوان يجعله يُنسَخ ويُلصَق
   ويُحفَظ في سجلّ المتصفّح وفي وكيل الشبكة.

   والمخزن في الذاكرة وحدها ويُفقَد بإعادة التحميل، و`useSyncExternalStore`
   لأنه خارج React فقراءتُه أثناء العرض بلا اشتراكٍ تُعطي قيمةً بائتة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useSyncExternalStore } from "react";

/** ما يعبر بين شاشات القسم — معرّفات مستندات لا غير. */
export interface AccountingFocus {
  /** أمر الشراء المفتوح — ومعرّفات سطوره مدخلُ الاستلام. */
  readonly orderId: string;
  /** إشعار الاستلام المفتوح. */
  readonly goodsReceiptId: string;
  /** فاتورة المبيعات المفتوحة. */
  readonly invoiceId: string;
  /** فاتورة المورّد المفتوحة. */
  readonly billId: string;
}

const EMPTY: AccountingFocus = { orderId: "", goodsReceiptId: "", invoiceId: "", billId: "" };

let current: AccountingFocus = EMPTY;
const listeners = new Set<() => void>();

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function snapshot(): AccountingFocus {
  return current;
}

/**
 * يثبّت معرّفاً لتقرأه شاشةٌ أخرى في القسم.
 * @param patch ما تغيّر من المعرّفات.
 */
export function setAccountingFocus(patch: Partial<AccountingFocus>): void {
  const next: AccountingFocus = { ...current, ...patch };
  if (
    next.orderId === current.orderId &&
    next.goodsReceiptId === current.goodsReceiptId &&
    next.invoiceId === current.invoiceId &&
    next.billId === current.billId
  ) {
    return;
  }
  current = next;
  for (const listener of listeners) listener();
}

/** المعرّفات المحمولة بين شاشات القسم. */
export function useAccountingFocus(): readonly [
  AccountingFocus,
  (patch: Partial<AccountingFocus>) => void,
] {
  const value = useSyncExternalStore(subscribe, snapshot, snapshot);
  const set = useCallback((patch: Partial<AccountingFocus>) => setAccountingFocus(patch), []);
  return [value, set] as const;
}

/** يُفرغ المخزن — للاختبارات، فلا تتسرّب حالةُ اختبارٍ إلى الذي بعده. */
export function resetAccountingFocus(): void {
  current = EMPTY;
  for (const listener of listeners) listener();
}
