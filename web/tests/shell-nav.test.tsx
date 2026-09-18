/* ═══════════════════════════════════════════════════════════════════════════
   القائمة الجانبية تحمل كل شاشة — ولا شاشةَ تُفتح بلوحة الأوامر وحدها
   ───────────────────────────────────────────────────────────────────────────
   **العطل الذي يمنعه هذا الملفّ وقع فعلاً أكثر من مرّة، وهو عطلٌ صامت:**
   قائمةُ الملاحة في `App.tsx` مكتوبةٌ بيد — سلسلةُ `<Link>` مرتَّبةٌ بترتيب
   العمل — بينما لوحةُ الأوامر ومصفوفةُ الحراسة تُبنيان من `SCREENS` في
   `shell/sections.ts`. فمن يضيف شاشةً في أحد الموضعين وينسى الآخر لا يكسر
   بناءً ولا يُحمّر اختباراً: الشاشةُ تعمل، ومسارُها مسجَّل، وCtrl+K يفتحها —
   **ولا يراها من يقرأ الملاحة**. وذلك أسوأ من رابطٍ مكسور لأن أحداً لا يشتكي
   منه: من لا يعلم بوجود الشاشة لا يبلّغ عن غيابها.

   وقد وقع آخرُ مرّة عند إضافة `/purchasing/payables`: دخلت `SCREENS` والموجّه،
   وغابت عن القائمة. فصار هذا الحارس يقارن **ما يُعرَض في `.app-side` فعلاً بعد
   الرسم** بما تعلنه `SCREENS` — لا نصَّ الملفّ بنصّه، فقارئُ النصّ يمرّ على
   رابطٍ معلَّقٍ خلف شرط.

   **ولماذا الطرفان معاً لا طرفٌ واحد:** غيابُ شاشةٍ من القائمة يُخفيها،
   ورابطٌ في القائمة بلا صفٍّ في `SCREENS` يصنع شاشةً لا تعرف قسمَها ولا لونَه
   ولا يجدها بحثُ لوحة الأوامر. فكلاهما عطل، وكلاهما يُقاس هنا.
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import { act, render } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS } from "../src/app/shell/sections";
import type { RawResponse, Transport } from "../src/api/transport";

/** ناقلٌ صامت: هذا الحارس يقيس الملاحة لا البيانات. */
const silent: Transport = {
  async send(): Promise<RawResponse> {
    return { status: 503, headers: new Headers(), json: null, text: "" };
  },
};

async function navHrefs(): Promise<string[]> {
  const router = createAppRouter({ memory: true, initialPath: "/" });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider transport={silent}>
            <RouterProvider router={router} />
          </ApiProvider>
        </QueryClientProvider>
      </LocaleProvider>
    );
  }
  await act(async () => {
    render(<Tree />);
    await router.load();
  });
  const nav = document.querySelector(".app-side");
  expect(nav, "لا قائمةَ جانبية في القشرة").not.toBeNull();
  return [...(nav?.querySelectorAll("a[href]") ?? [])]
    .map((a) => a.getAttribute("href") ?? "")
    .filter((href) => href.startsWith("/"));
}

describe("القائمة الجانبية و SCREENS لا تنحرف إحداهما عن الأخرى", () => {
  it("شاهدٌ إيجابي: الحارس يرى روابطَ الملاحة أصلاً", async () => {
    const hrefs = await navHrefs();
    /* بلا هذا السطر يمرّ الحارسُ على قائمةٍ فارغة فيصير أخضرَ وهو أعمى. */
    expect(hrefs.length).toBeGreaterThan(20);
  });

  it("كلُّ شاشةٍ في SCREENS لها رابطٌ مرئيٌّ في القائمة", async () => {
    const hrefs = new Set(await navHrefs());
    const hidden = SCREENS.map((s) => s.path).filter((p) => !hrefs.has(p));
    expect(
      hidden,
      "شاشاتٌ تُفتح بـCtrl+K ولا يراها من يقرأ الملاحة: " + hidden.join(" · ")
    ).toEqual([]);
  });

  it("وكلُّ رابطٍ في القائمة له صفٌّ في SCREENS — فيعرف قسمَه ولونَه", async () => {
    const declared = new Set(SCREENS.map((s) => s.path));
    const stray = [...new Set(await navHrefs())].filter((h) => !declared.has(h));
    expect(
      stray,
      "روابطُ ملاحةٍ بلا صفٍّ في SCREENS — لا قسمَ لها ولا يجدها البحث: " + stray.join(" · ")
    ).toEqual([]);
  });
});
