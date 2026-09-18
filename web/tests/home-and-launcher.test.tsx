/* ═══════════════════════════════════════════════════════════════════════════
   صفحةُ البداية ومُشغّلُ الأنظمة
   ───────────────────────────────────────────────────────────────────────────
   **العطل الذي يمنعه هذا الملفّ هو العطل الذي رآه المالك على الشاشة الحيّة:**
   الأنظمةُ والشاشاتُ في عمودٍ واحد، فيُقرأ «المخزني» و«ميزان المراجعة»
   بنداً إلى بند — وهما نظامٌ يُشترى وشاشةٌ تُفتح داخله. فصار للأنظمة
   مُشغّلُها، وصارت لها صفحةُ بدايةٍ بمربّعات.

   **وثلاثةُ أشياء تُقاس هنا لأنها تنكسر صامتةً:**
     ١ · أن الأنظمةَ **خرجت** من القائمة الجانبية فعلاً — ولو عادت يوماً
         بسطرٍ واحد لعاد الخلطُ بلا أن يحمرّ شيء.
     ٢ · أن المُشغّل يفتح ويغلق ويحمل الأنظمةَ الخمسة كلَّها.
     ٣ · أن مربّعات البداية تقود إلى مسارات `SECTIONS` نفسها — فمربّعٌ
         يقود إلى غير نظامه عطلٌ لا يشتكي منه أحد قبل البيع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SECTIONS } from "../src/app/shell/sections";
import type { RawResponse, Transport } from "../src/api/transport";

/** ناقلٌ صامت: هذان المقيسان ملاحةٌ لا بيانات، فيردّ 503 على كل باب. */
const silent: Transport = ({ url }) =>
  Promise.resolve<RawResponse>({ ok: false, status: 503, json: null, url });

/** يُمهل الدورةَ التالية: نقرةٌ تُبدّل حالةً أو تُبحر لا تكتمل في اللحظة نفسها. */
function settled(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

/* ‏**واعتمادٌ محفوظٌ قبل كل رسمة** — وإلّا حجبت البوّابةُ الأمامية القشرةَ كلَّها
   فلا قائمةَ جانبية تُقاس. والمقيسُ هنا الملاحةُ لا المصادقة، فالجلسةُ شرطُ
   وصولٍ إليه لا موضوعُه. */
beforeEach(() => {
  globalThis.localStorage.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: "", book: "MAIN", period: "" })
  );
});

afterEach(() => {
  globalThis.localStorage.clear();
});

async function mount(initialPath: string): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath });
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
}

describe("مُشغّلُ الأنظمة", () => {
  it("الأنظمةُ ليست في القائمة الجانبية — وهذا هو الخلط الذي أُخرج منها", async () => {
    await mount("/hr");
    const side = document.querySelector(".app-side");
    expect(side).not.toBeNull();
    expect(
      side?.querySelector("[data-testid='section-nav']"),
      "ملاحةُ الأنظمة عادت إلى القائمة الجانبية — والشاشاتُ والأنظمةُ تُقرآن مستوىً واحداً"
    ).toBeNull();
    /* وفي مكانها شارةُ النظام المفتوح: «أين أنا» بلا عمودِ أنظمةٍ فوق الشجرة. */
    expect(side?.querySelector("[data-testid='system-badge']")).not.toBeNull();
    cleanup();
  });

  it("اللوحُ مغلقٌ حتى يُفتح، ويحمل الأنظمة الخمسة برموزها", async () => {
    await mount("/");
    expect(screen.queryByTestId("launch-panel"), "لوحٌ مفتوحٌ بلا طلب").toBeNull();

    const button = screen.getByTestId("open-launcher");
    expect(button.getAttribute("aria-expanded")).toBe("false");
    await act(async () => {
      button.click();
      await settled();
    });
    expect(button.getAttribute("aria-expanded")).toBe("true");

    const panel = screen.getByTestId("launch-panel");
    for (const system of SECTIONS) {
      const tile = panel.querySelector("[data-section='" + system.id + "']");
      expect(tile, "النظام «" + system.id + "» ليس في المُشغّل").not.toBeNull();
      /* ورمزٌ في كل مربّع — وهو ما طلبه المالك: أيقونةٌ لكل نظام. */
      expect(tile?.querySelector("svg"), "نظامٌ بلا رمز: " + system.id).not.toBeNull();
      if (system.built && system.path) {
        expect(tile?.getAttribute("href")).toBe(system.path);
      }
    }
    cleanup();
  });

  it("واختيارُ نظامٍ يُغلق اللوح — فلا يبقى معلّقاً فوق ما فُتح", async () => {
    await mount("/");
    await act(async () => {
      screen.getByTestId("open-launcher").click();
      await settled();
    });
    expect(screen.queryByTestId("launch-panel")).not.toBeNull();
    await act(async () => {
      screen.getByTestId("section-inventory").click();
      await settled();
    });
    expect(screen.queryByTestId("launch-panel")).toBeNull();
    cleanup();
  });
});

describe("صفحةُ البداية", () => {
  it("خمسةُ مربّعاتٍ بأسماء الأنظمة ومساراتها — لا نسخةٌ ثانية منها", async () => {
    await mount("/home");
    const tiles = screen.getByTestId("home-tiles");
    const anchors = [...tiles.querySelectorAll("a[href]")];
    expect(anchors.length).toBe(SECTIONS.filter((s) => s.built && s.path).length);
    for (const system of SECTIONS) {
      if (!system.built || !system.path) continue;
      const tile = screen.getByTestId("home-tile-" + system.id);
      expect(tile.getAttribute("href")).toBe(system.path);
      expect(tile.querySelector("svg"), "مربّعٌ بلا رمز: " + system.id).not.toBeNull();
    }
    cleanup();
  });

  it("والعلامةُ في رأس القائمة بابٌ إلى البداية", async () => {
    await mount("/inventory/items");
    expect(screen.getByTestId("brand-home").getAttribute("href")).toBe("/home");
    cleanup();
  });
});
