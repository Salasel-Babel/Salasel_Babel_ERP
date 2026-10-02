/* ═══════════════════════════════════════════════════════════════════════════
   الواجهة المبسّطة — ما يُرى في القوائم افتراضياً، وما يُطلب صراحةً
   ───────────────────────────────────────────────────────────────────────────
   **طلبُ المالك:** واجهاتٌ محاسبيةٌ ومخزنيةٌ وموارد بشرية تخدم الأغراض
   المبسّطة. فالشاشاتُ المتقدّمة (`advanced: true` في `sections.ts`) **تُخفى من
   القوائم لا من النظام**: مساراتها قائمة، وروابطُها تعمل، ولوحةُ الأوامر تجدها،
   وزرٌّ واحد في القائمة الجانبية يُظهرها. فلا شيء يُحذف، والتبسيطُ يُعكس بنقرة.

   والاختيارُ يُحفظ في المتصفّح وحده: هو تفضيلُ عرضٍ لا صلاحية.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useSyncExternalStore } from "react";
import { SCREENS } from "./sections";

const KEY = "sb-show-advanced";
const listeners = new Set<() => void>();

function read(): boolean {
  try {
    return globalThis.localStorage?.getItem(KEY) === "1";
  } catch {
    return false;
  }
}

/** يضبط إظهار الشاشات المتقدّمة ويُعلم كل من يستمع. */
export function setShowAdvanced(show: boolean): void {
  try {
    if (show) globalThis.localStorage?.setItem(KEY, "1");
    else globalThis.localStorage?.removeItem(KEY);
  } catch {
    /* التصفّح الخاص: يبقى الاختيار لهذه الجلسة وحدها عبر المستمعين. */
  }
  listeners.forEach((listener) => listener());
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** هل يُظهر المستخدم الشاشات المتقدّمة؟ الافتراض: لا — الواجهة مبسّطة. */
export function useShowAdvanced(): boolean {
  return useSyncExternalStore(subscribe, read, () => false);
}

const ADVANCED = new Set(SCREENS.filter((screen) => screen.advanced === true).map((screen) => screen.path));

/** هل هذا المسار شاشةٌ متقدّمة؟ */
export function isAdvanced(path: string): boolean {
  return ADVANCED.has(path);
}

/**
 * هل تُعرض الشاشة في القوائم؟
 * <p>
 * <b>والشاشةُ القائمة تُعرض دائماً</b> ولو كانت متقدّمة: من فتحها برابطٍ أو من
 * لوحة الأوامر يجب أن يرى أين هو، لا قائمةً لا تعرفه.
 * </p>
 * @param path مسار الشاشة.
 * @param showAdvanced اختيارُ المستخدم.
 * @param current المسار القائم.
 */
export function shownInMenus(path: string, showAdvanced: boolean, current: string): boolean {
  return showAdvanced || path === current || !ADVANCED.has(path);
}
