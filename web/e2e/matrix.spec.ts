/* ═══════════════════════════════════════════════════════════════════════════
   مصفوفة العرض: ٤ لغات × ٢ مظهر × ٣ عروض  +  ٤ لغات × ٢ لوحة عند 1920
   The rendering matrix: 4 locales × 2 themes × 3 widths, plus palettes
   ───────────────────────────────────────────────────────────────────────────
   ما يُثبَت في كل تركيبة:
     · اتجاه الجذر يساوي ما تعلنه اللغة — لا ما يعلنه المنتج.
     · لا انزلاق أفقي لجسم الصفحة إطلاقاً. الجدول العريض يُمرَّر داخل حاويته.
     · كل خانة مالية معزولة ومحاذاة إلى النهاية بأرقام جدولية — فالفاصلة
       العشرية تقع تحت أختها في اللغات الأربع.
     · نصّ المال على السلك يبقى في الصفحة بايتاً ببايت.
   وكل تأكيد يبدأ بحارس لافراغ: تركيبةٌ لم تُرسم شيئاً تفشل، ولا تمرّ صامتة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { test, expect, type Page } from "@playwright/test";

const MOCK = "http://127.0.0.1:5099";
const COMPANY = "11111111-1111-4111-8111-111111111111";
const LOCALES = [
  { code: "ar", dir: "rtl" },
  { code: "en", dir: "ltr" },
  { code: "ur", dir: "rtl" },
  { code: "hi", dir: "ltr" },
] as const;
const THEMES = ["light", "dark"] as const;
const WIDTHS = [360, 768, 1920] as const;

function url(locale: string, rows = 40): string {
  const q = new URLSearchParams({
    lang: locale,
    baseUrl: MOCK,
    companyId: COMPANY,
    book: "MAIN",
    period: "2026-05",
  });
  return "/?" + q.toString() + "&rows=" + rows;
}

async function gotoScreen(page: Page, locale: string, theme: string, palette = "default") {
  await page.addInitScript(
    ([t, p]) => {
      try {
        localStorage.setItem("sb-theme", t);
        localStorage.setItem("sb-palette", p);
      } catch {
        /* ignore */
      }
    },
    [theme, palette]
  );
  /* عدد الصفوف يُمرَّر إلى الخادم الوهمي عبر المسار نفسه. */
  await page.route("**/trial-balance*", async (route) => {
    const target = new URL(route.request().url());
    target.searchParams.set("rows", "40");
    await route.continue({ url: target.toString() });
  });
  await page.goto(url(locale));
  await page.waitForSelector('[data-testid="trial-balance-screen"]');
  await page.waitForSelector("table.tb tbody tr");
}

/** يقيس انزلاق جسم الصفحة أفقياً. */
async function horizontalOverflow(page: Page): Promise<number> {
  return page.evaluate(() => {
    const el = document.scrollingElement ?? document.documentElement;
    return el.scrollWidth - el.clientWidth;
  });
}

test.describe("مصفوفة اللغات والمظاهر والعروض", () => {
  for (const locale of LOCALES) {
    for (const theme of THEMES) {
      for (const width of WIDTHS) {
        test(`${locale.code} · ${theme} · ${width}px`, async ({ page }) => {
          await page.setViewportSize({ width, height: 900 });
          await gotoScreen(page, locale.code, theme);

          /* الاتجاه خاصية اللغة. */
          await expect(page.locator("html")).toHaveAttribute("dir", locale.dir);
          await expect(page.locator("html")).toHaveAttribute("lang", locale.code);
          await expect(page.locator("html")).toHaveAttribute("data-theme", theme);

          /* حارس اللافراغ: تركيبةٌ بلا صفوف لا تُثبت شيئاً. */
          const rows = page.locator("table.tb tbody tr");
          const rowCount = await rows.count();
          expect(rowCount, "عدد الصفوف المرسومة").toBeGreaterThanOrEqual(40);

          /* لا انزلاق أفقي للصفحة. */
          expect(await horizontalOverflow(page), "انزلاق أفقي لجسم الصفحة").toBeLessThanOrEqual(1);

          /* الخانة المالية: اتجاه ثابت وعزل ومحاذاة ونهاية وأرقام جدولية. */
          const cellStyles = await page.evaluate(() => {
            const cells = [...document.querySelectorAll("table.tb td.n")];
            return cells.slice(0, 8).map((c) => {
              const s = getComputedStyle(c);
              return {
                direction: s.direction,
                unicodeBidi: s.unicodeBidi,
                textAlign: s.textAlign,
                variant: s.fontVariantNumeric,
              };
            });
          });
          expect(cellStyles.length, "خانات مالية مفحوصة").toBeGreaterThan(4);
          for (const s of cellStyles) {
            expect(s.direction).toBe("ltr");
            expect(s.unicodeBidi).toBe("isolate");
            expect(s.textAlign).toBe("end");
            expect(s.variant).toContain("tabular-nums");
          }

          /* المبالغ تصطفّ فعلاً: الحافة اليمنى للخانات في العمود واحدة. */
          const rightEdges = await page.evaluate(() => {
            const cells = [...document.querySelectorAll("table.tb tbody td.n:nth-child(3)")];
            return cells.slice(0, 20).map((c) => Math.round(c.getBoundingClientRect().right));
          });
          expect(rightEdges.length).toBeGreaterThan(10);
          expect(new Set(rightEdges).size, "حواف العمود المالي").toBe(1);

          /* المال على السلك يبقى بايتاً ببايت داخل الصفحة. */
          const exact = page.locator('[title="1000000000000.4013"]');
          expect(await exact.count(), "القيمة الخطرة موجودة بنصّها").toBeGreaterThan(0);

          await page.screenshot({
            path: `test-results/matrix/${locale.code}-${theme}-${width}.png`,
            fullPage: false,
          });
        });
      }
    }
  }
});

test.describe("اللوحتان عند 1920", () => {
  for (const locale of LOCALES) {
    for (const palette of ["default", "accessible"] as const) {
      test(`${locale.code} · ${palette}`, async ({ page }) => {
        await page.setViewportSize({ width: 1920, height: 900 });
        await gotoScreen(page, locale.code, "light", palette);
        await expect(page.locator("html")).toHaveAttribute("data-palette", palette);
        expect(await horizontalOverflow(page)).toBeLessThanOrEqual(1);
        const rows = await page.locator("table.tb tbody tr").count();
        expect(rows).toBeGreaterThanOrEqual(40);
        await page.screenshot({ path: `test-results/matrix/${locale.code}-${palette}-1920.png` });
      });
    }
  }
});
