/* ═══════════════════════════════════════════════════════════════════════════
   قياس: ٥٠٠ صفّاً — كم يستغرق الرسم، وكم تستغرق ضغطة مفتاح بعده
   ───────────────────────────────────────────────────────────────────────────
   الرقم المطلوب ليس «سريع»: زمنٌ مقيس على هذا الجهاز، بمنهجه، ومع تشتّته.
   والقياس الثاني أهمّ من الأوّل: المحاسب يكتب، فتأخّر الضغطة أسوأ من تأخّر
   الرسم الأوّل.
   ═══════════════════════════════════════════════════════════════════════════ */
import { test, expect } from "@playwright/test";
import fs from "node:fs";

const MOCK = "http://127.0.0.1:5099";
const COMPANY = "11111111-1111-4111-8111-111111111111";
const ROWS = 500;
const REPEATS = 7;

test("٥٠٠ صفّاً: زمن الرسم وزمن الاستجابة للمفتاح", async ({ page }) => {
  test.setTimeout(180_000);
  await page.setViewportSize({ width: 1600, height: 1000 });
  await page.route("**/trial-balance*", async (route) => {
    const target = new URL(route.request().url());
    target.searchParams.set("rows", String(ROWS));
    await route.continue({ url: target.toString() });
  });

  const q = new URLSearchParams({
    lang: "ar",
    baseUrl: MOCK,
    companyId: COMPANY,
    book: "MAIN",
    period: "2026-05",
  });

  const renderTimes: number[] = [];
  for (let i = 0; i < REPEATS; i++) {
    await page.goto("about:blank");
    await page.goto("/?" + q.toString());
    /* من لحظة وصول الجسم إلى لحظة وجود الصفّ الأخير في DOM. */
    const elapsed = await page.evaluate(async (expected) => {
      const start = performance.now();
      await new Promise<void>((resolve) => {
        const tick = () => {
          const rows = document.querySelectorAll("table.tb tbody tr").length;
          if (rows >= expected) resolve();
          else requestAnimationFrame(tick);
        };
        tick();
      });
      /* ننتظر إطاراً كاملاً بعده كي يدخل زمن التخطيط والرسم في القياس. */
      await new Promise<void>((resolve) => requestAnimationFrame(() => resolve()));
      return performance.now() - start;
    }, ROWS);
    renderTimes.push(elapsed);
  }

  const rowCount = await page.locator("table.tb tbody tr").count();
  expect(rowCount, "حارس اللافراغ: القياس على جدول فارغ لا يقيس شيئاً").toBe(ROWS);

  /* استجابة المفتاح بعد أن صار في الصفحة ٥٠٠ صفّاً وألف مبلغ. */
  const keyTimes: number[] = [];
  for (let i = 0; i < 20; i++) {
    const t0 = Date.now();
    await page.keyboard.press("ArrowDown");
    await page.waitForFunction(
      (n) =>
        document.querySelector('table.tb tbody tr[data-active="true"]') ===
        document.querySelectorAll("table.tb tbody tr")[n],
      i + 1
    );
    keyTimes.push(Date.now() - t0);
  }

  /* زمن المرشّح: كتابة تُعيد ترشيح خمسمئة صفّ. */
  const t0 = Date.now();
  await page.locator('[data-testid="filter-search"]').fill("1010115");
  await page.waitForFunction(() => document.querySelectorAll("table.tb tbody tr").length === 1);
  const filterMs = Date.now() - t0;

  const stat = (xs: number[]) => {
    const s = [...xs].sort((a, b) => a - b);
    return {
      n: s.length,
      min: +(s[0] ?? 0).toFixed(1),
      median: +(s[Math.floor(s.length / 2)] ?? 0).toFixed(1),
      max: +(s[s.length - 1] ?? 0).toFixed(1),
    };
  };

  const report = {
    rows: ROWS,
    moneyCells: ROWS * 2,
    renderMs: stat(renderTimes),
    keyPressMs: stat(keyTimes),
    filterMs,
    userAgent: await page.evaluate(() => navigator.userAgent),
    measuredAt: new Date().toISOString(),
  };
  fs.mkdirSync("test-results", { recursive: true });
  fs.writeFileSync("test-results/perf-500.json", JSON.stringify(report, null, 2));
  console.log("\nقياس ٥٠٠ صفّاً · 500-row measurement\n" + JSON.stringify(report, null, 2));

  await page.locator('[data-testid="filter-search"]').fill("");
  await page.waitForFunction((n) => document.querySelectorAll("table.tb tbody tr").length === n, ROWS);
  await page.screenshot({ path: "test-results/matrix/ar-light-1600-500rows.png" });
});
