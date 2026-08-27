/* ═══════════════════════════════════════════════════════════════════════════
   مِنصّة القيادة — المتجر الذي يقوده سكربت التسجيل من خارج الصفحة
   ───────────────────────────────────────────────────────────────────────────
   العرض **يُقاد** لا يُشغَّل من تلقاء نفسه: سكربت Playwright يستدعي
   `window.__demo.set({...})` فتتحرّك الشاشة. والسبب أن التسجيل يجب أن يكون
   **قابلاً لإعادة الإنتاج** — نفس التوقيت ونفس الترتيب في كل تشغيلة — لا لقطةً
   محظوظة. ولذلك لا مؤقّت داخل الصفحة يقرّر متى ينتقل المشهد.

   ولذلك أيضاً تصل نتائج `psql` و`curl` **الحقيقية** إلى الشاشة من هنا: السكربت
   ينفّذها فعلاً على القاعدة والخادم، ويحقن مُخرَجها الحرفي. ولا نصّ مُخرَج
   مكتوب في هذا المستودع بيدٍ.
   ═══════════════════════════════════════════════════════════════════════════ */

/** وسم الصدق أعلى الشاشة: ما يُرى الآن حقيقي أم مُحاكى. */
export type Truth = "real" | "sim" | "mixed";

/** حالة العرض كاملةً. */
export interface DemoState {
  /** معرّف المشهد الظاهر. */
  readonly scene: string;
  /** خطوة داخل المشهد — كل مشهد يفسّر رقمه بنفسه. */
  readonly step: number;
  /** التعليق العربي أعلى الشاشة. */
  readonly caption: string;
  /** سطر ثانٍ أصغر تحت التعليق. */
  readonly captionSub: string;
  /** وسم الصدق. */
  readonly truth: Truth;
  /** بيانات يحقنها السكربت: مُخرَجات حقيقية، ومؤشّرات، وما شابه. */
  readonly bag: Readonly<Record<string, unknown>>;
}

const INITIAL: DemoState = { scene: "title", step: 0, caption: "", captionSub: "", truth: "real", bag: {} };

let state: DemoState = INITIAL;
const listeners = new Set<() => void>();

function emit(): void {
  for (const listener of listeners) listener();
}

/** يشترك في التغيّر. */
export function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

/** يقرأ اللقطة الحالية. */
export function snapshotState(): DemoState {
  return state;
}

/** يدمج تعديلاً جزئياً على الحالة. */
export function patch(next: Partial<DemoState>): void {
  state = { ...state, ...next, bag: { ...state.bag, ...(next.bag ?? {}) } };
  emit();
}

/** واجهة القيادة المعروضة على النافذة. */
export interface DemoBridge {
  set: (next: Partial<DemoState>) => void;
  get: () => DemoState;
  reset: () => void;
}

declare global {
   
  var __demo: DemoBridge | undefined;
}

/** يركّب جسر القيادة على النافذة. يُستدعى مرّة عند تركيب المِنصّة. */
export function installBridge(): void {
  globalThis.__demo = {
    set: patch,
    get: snapshotState,
    reset: () => {
      state = INITIAL;
      emit();
    },
  };
}
