/* ═══════════════════════════════════════════════════════════════════════════
   ما تحمله شاشات الموارد البشرية بعضها إلى بعض  ·  What the HR screens hand on
   ───────────────────────────────────────────────────────────────────────────
   شاشاتُ هذا القسم أربع، ويعبر بينها **معرّفٌ واحد**: مسيّرٌ فُتح، أو قسيمةٌ
   نُقر عليها، أو علاقةُ عملٍ انتهت. ولا يُحمَل في الرابط لسببٍ محدَّد:

     · العقد يقول إن معرّف القسيمة هو `DocumentId` في هوية الإحكام، فهو
       **مفتاحٌ محاسبي** لا وسيط عرض. ووضعُه في شريط العنوان يجعله يُنسَخ
       ويُلصَق ويُشارَك، ويُحفَظ في سجلّ المتصفّح وفي وكيل الشبكة.
     · **ولا معرّف شخصي يُحمَل هنا إطلاقاً** — ولا الرمز المعتم: ما يعبر بين
       الشاشات معرّفُ مستندٍ لا معرّفُ إنسان.

   والمخزن في الذاكرة وحدها: يُفقَد بإعادة التحميل، وكل شاشة تقبل المعرّف
   مكتوباً بيد فتبقى صالحةً وحدها. و`useSyncExternalStore` لأن المخزن خارج
   React، وقراءته أثناء العرض بلا اشتراكٍ تُعطي قيمةً بائتة في الوضع المتزامن.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useSyncExternalStore } from "react";

/** ما يعبر بين شاشات القسم — معرّفات مستندات لا غير. */
export interface HrFocus {
  /** مسيّر الرواتب المفتوح. */
  readonly runId: string;
  /** القسيمة المفتوحة. */
  readonly payslipId: string;
  /** علاقة العمل المعنيّة بنهاية الخدمة. */
  readonly employmentId: string;
  /** الموظف المقروء في السجلّ. */
  readonly employeeId: string;
}

const EMPTY: HrFocus = { runId: "", payslipId: "", employmentId: "", employeeId: "" };

let current: HrFocus = EMPTY;
const listeners = new Set<() => void>();

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function snapshot(): HrFocus {
  return current;
}

/**
 * يثبّت معرّفاً لتقرأه شاشةٌ أخرى في القسم.
 * @param patch ما تغيّر من المعرّفات.
 */
export function setHrFocus(patch: Partial<HrFocus>): void {
  const next: HrFocus = { ...current, ...patch };
  if (
    next.runId === current.runId &&
    next.payslipId === current.payslipId &&
    next.employmentId === current.employmentId &&
    next.employeeId === current.employeeId
  ) {
    return;
  }
  current = next;
  for (const listener of listeners) listener();
}

/** المعرّفات المحمولة بين شاشات القسم. */
export function useHrFocus(): readonly [HrFocus, (patch: Partial<HrFocus>) => void] {
  const value = useSyncExternalStore(subscribe, snapshot, snapshot);
  const set = useCallback((patch: Partial<HrFocus>) => setHrFocus(patch), []);
  return [value, set] as const;
}

/** يُفرغ المخزن — للاختبارات، فلا تتسرّب حالةُ اختبارٍ إلى الذي بعده. */
export function resetHrFocus(): void {
  current = EMPTY;
  for (const listener of listeners) listener();
}
