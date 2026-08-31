/* ═══════════════════════════════════════════════════════════════════════════
   تركيب طبقة العرض — ما يقع قبل أول رسم
   ───────────────────────────────────────────────────────────────────────────
   ثلاثة أشياء، ولا رابع:

   ١ · الإعداد: معرّف المنشأة والدفتر والفترة. الشاشات تُعطِّل استعلاماتها حين
       لا منشأة، وهو السلوك الصحيح — فالعرض يُسلّمها منشأةً كما يسلّمها الدخول.
   ٢ · حارسُ الشبكة: `fetch` يُستبدَل بجوابٍ من العرض. الفتحة المُعلَنة هي
       `Transport`، وهي المُستبدَلة أصلاً — لكن شاشةَ الدخول تبني نقلَها بنفسها
       لتجرّب اعتماداً، فبلا الحارس يخرج طلبٌ من الصفحة. والمُضيف يحجب ذلك
       **صامتاً**، فيُقرأ الحجب عطلاً في المنتج. الحارس يجعل الرفض ناطقاً.
   ٣ · لا شيء آخر: لا شاشة تُعدَّل، ولا ميزة تُطفَأ، ولا حالة «قيد البناء»
       تُخفى. ما لم يُبنَ يبقى معلَناً كما هو في المنتج.
   ═══════════════════════════════════════════════════════════════════════════ */

import { answer } from "./transport";
import { BOOK, COMPANY_ID, PERIOD } from "./seed";

const CONFIG_KEY = "sb-api-config";

/** يكتب إعداد العرض ما لم يكن مكتوباً — فاختيار المستخدم يبقى بين الجلسات. */
function seedConfig(): void {
  try {
    const store = globalThis.localStorage;
    if (!store) return;
    const raw = store.getItem(CONFIG_KEY);
    const current = raw ? (JSON.parse(raw) as Record<string, unknown>) : {};
    if (typeof current.companyId === "string" && current.companyId !== "") return;
    store.setItem(
      CONFIG_KEY,
      JSON.stringify({ ...current, baseUrl: "", token: "showcase", companyId: COMPANY_ID, book: BOOK, period: PERIOD })
    );
  } catch {
    /* تصفّحٌ خاص: الإعداد لا يُحفظ، والصفحة تعمل بالافتراض. */
  }
}

/** يمنع أي طلبٍ من مغادرة الصفحة، ويجيب عنه من العرض. */
function guardFetch(): void {
  const original = globalThis.fetch?.bind(globalThis);
  globalThis.fetch = async (input: RequestInfo | URL, init?: RequestInit): Promise<Response> => {
    const url =
      typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
    const method = (init?.method ?? (typeof input === "object" && "method" in input ? input.method : "GET")) || "GET";
    /* أصلٌ خارجيّ لا يخصّ العقد: يُترك لـ`fetch` الأصلي إن وُجد — ولا يقع
       هذا في هذه الصفحة أصلاً، فكلّ أصولها مُضمَّنة. */
    const path = url.startsWith("http") ? new URL(url).pathname + new URL(url).search : url;
    if (!path.startsWith("/api/") && path !== "/health") {
      return original ? original(input, init) : Promise.reject(new Error("لا شبكة في صفحة العرض."));
    }
    let body: unknown = null;
    if (typeof init?.body === "string") {
      try {
        body = JSON.parse(init.body);
      } catch {
        body = null;
      }
    }
    const result = answer(method, path, body);
    return new Response(result.json === null ? null : JSON.stringify(result.json), {
      status: result.status,
      headers: { "Content-Type": result.ok ? "application/json" : "application/problem+json" },
    });
  };
}

/** يركّب طبقة العرض. يُستدعى مرّة، قبل أول رسم. */
export function installShowcase(): void {
  seedConfig();
  guardFetch();
}
