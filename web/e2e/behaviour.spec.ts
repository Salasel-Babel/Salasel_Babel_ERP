/* ═══════════════════════════════════════════════════════════════════════════
   السلوك: لوحة المفاتيح، والفرز، والمرشّحات، وسطح الخطأ، وتبديل اللغة حيّاً
   ═══════════════════════════════════════════════════════════════════════════ */
import { test, expect, type Page } from "@playwright/test";

const MOCK = "http://127.0.0.1:5099";
const COMPANY = "11111111-1111-4111-8111-111111111111";
const PROBLEM_COMPANY = "00000000-0000-4000-8000-0000000000ff";

async function open(page: Page, options: { company?: string; lang?: string; rows?: number } = {}) {
  const rows = options.rows ?? 40;
  await page.route("**/trial-balance*", async (route) => {
    const target = new URL(route.request().url());
    target.searchParams.set("rows", String(rows));
    await route.continue({ url: target.toString() });
  });
  const q = new URLSearchParams({
    lang: options.lang ?? "ar",
    baseUrl: MOCK,
    companyId: options.company ?? COMPANY,
    book: "MAIN",
    period: "2026-05",
  });
  await page.goto("/?" + q.toString());
  await page.waitForSelector('[data-testid="trial-balance-screen"]');
}

async function activeCode(page: Page): Promise<string> {
  return page
    .locator('table.tb tbody tr[data-active="true"] .acct-code')
    .first()
    .innerText();
}

test("لوحة المفاتيح تحرّك السطر النشط بلا فأرة", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await open(page);
  await page.waitForSelector("table.tb tbody tr");

  const first = await activeCode(page);
  expect(first).not.toBe("");

  await page.keyboard.press("ArrowDown");
  const second = await activeCode(page);
  expect(second).not.toBe(first);

  await page.keyboard.press("j");
  const third = await activeCode(page);
  expect(third).not.toBe(second);

  await page.keyboard.press("ArrowUp");
  expect(await activeCode(page)).toBe(second);

  await page.keyboard.press("End");
  const last = await activeCode(page);
  await page.keyboard.press("Home");
  expect(await activeCode(page)).toBe(first);
  expect(last).not.toBe(first);

  await page.keyboard.press("PageDown");
  const paged = await activeCode(page);
  expect(paged).not.toBe(first);
});

test("«/» ينقل التركيز إلى البحث، و Escape يمسحه", async ({ page }) => {
  await open(page);
  await page.waitForSelector("table.tb tbody tr");
  await page.keyboard.press("/");
  await expect(page.locator('[data-testid="filter-search"]')).toBeFocused();
  await page.keyboard.type("1010115");
  await expect(page.locator("table.tb tbody tr")).toHaveCount(1);
  await page.keyboard.press("Escape");
  await expect(page.locator('[data-testid="filter-search"]')).toHaveValue("");
  const rows = await page.locator("table.tb tbody tr").count();
  expect(rows).toBeGreaterThan(1);
});

test("البحث يجد الحساب بأرقام عربية-هندية وديفاناغرية", async ({ page }) => {
  await open(page);
  await page.waitForSelector("table.tb tbody tr");
  const search = page.locator('[data-testid="filter-search"]');
  await search.fill("١٠١٠١١٥");
  await expect(page.locator("table.tb tbody tr")).toHaveCount(1);
  await search.fill("१०१०११५");
  await expect(page.locator("table.tb tbody tr")).toHaveCount(1);
});

test("«v» تُدوِّر العرض بالهوية لا بنصّ الزرّ", async ({ page }) => {
  await open(page, { lang: "hi" });
  await page.waitForSelector("table.tb tbody tr");
  await expect(page.locator("table.tb")).toHaveAttribute("data-view", "all");
  await page.keyboard.press("v");
  await expect(page.locator("table.tb")).toHaveAttribute("data-view", "debit");
  await page.keyboard.press("v");
  await expect(page.locator("table.tb")).toHaveAttribute("data-view", "credit");
  await page.keyboard.press("v");
  await expect(page.locator("table.tb")).toHaveAttribute("data-view", "all");
});

test("الفرز على عمود المال عشريّ لا عائم", async ({ page }) => {
  await open(page, { rows: 40 });
  await page.waitForSelector("table.tb tbody tr");
  await page.locator('[data-testid="sort-debit"]').click();
  await expect(page.locator("th.h-debit")).toHaveAttribute("aria-sort", "ascending");
  const ascending = await page.locator("table.tb tbody td.n:nth-child(3) span").evaluateAll(
    (nodes) => nodes.map((n) => n.getAttribute("title") ?? "")
  );
  expect(ascending.length).toBeGreaterThan(10);
  await page.locator('[data-testid="sort-debit"]').click();
  await expect(page.locator("th.h-debit")).toHaveAttribute("aria-sort", "descending");
  const descending = await page.locator("table.tb tbody td.n:nth-child(3) span").evaluateAll(
    (nodes) => nodes.map((n) => n.getAttribute("title") ?? "")
  );
  /* أكبر قيمة هي التي يفقدها Number — فلو مرّ الفرز عليه لتساوت بغيرها. */
  expect(descending[0]).toBe("1000000000000.4013");
  expect(ascending[ascending.length - 1]).toBe("1000000000000.4013");
});

test("سطح الخطأ يعرض المشكلة بالعربية والإنجليزية وكل أخطائها", async ({ page }) => {
  await open(page, { company: PROBLEM_COMPANY });
  const panel = page.locator('[data-testid="problem-panel"]');
  await expect(panel).toBeVisible();
  await expect(panel).toHaveAttribute("data-code", "auth.company_out_of_scope");
  await expect(page.locator('[data-testid="problem-code"]')).toHaveText("auth.company_out_of_scope");
  await expect(page.locator('[data-testid="problem-trace"]')).not.toBeEmpty();
  await expect(panel).toContainText("الاعتماد لا يبلغ هذه الشركة");
  await expect(panel).toContainText("The credential does not reach this company");
  /* كل الأخطاء لا أوّلها: العقد يرسل اثنين هنا. */
  await expect(panel.locator("ul li")).toHaveCount(2);
  await expect(panel).toContainText("entitlement.module_not_licensed");
});

test("تبديل اللغة يقلب الاتجاه والنصوص حيّاً بلا إعادة تحميل", async ({ page }) => {
  await page.setViewportSize({ width: 1440, height: 900 });
  await open(page);
  await page.waitForSelector("table.tb tbody tr");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  const before = await page.locator("h1").innerText();

  await page.locator('[data-testid="locale-switcher"]').selectOption("hi");
  await expect(page.locator("html")).toHaveAttribute("dir", "ltr");
  await expect(page.locator("html")).toHaveAttribute("lang", "hi");
  const after = await page.locator("h1").innerText();
  expect(after).not.toBe(before);

  await page.locator('[data-testid="locale-switcher"]').selectOption("ur");
  await expect(page.locator("html")).toHaveAttribute("dir", "rtl");
  /* الأردية لغة ثانية تُثبت أن الاتجاه خاصية اللغة لا خاصية العربية. */
  await expect(page.locator("html")).toHaveAttribute("lang", "ur");
});

test("رمز فترة مخالف للعقد يُرفض قبل مغادرة المتصفّح", async ({ page }) => {
  await open(page);
  await page.waitForSelector("table.tb tbody tr");
  let requests = 0;
  page.on("request", (r) => {
    if (r.url().includes("trial-balance")) requests++;
  });
  await page.locator('[data-testid="filter-period"]').fill("2026-13");
  await expect(page.locator('[data-testid="period-error"]')).toBeVisible();
  await page.waitForTimeout(400);
  expect(requests, "لا طلب يغادر المتصفّح برمز فترة مخالف").toBe(0);
});

test("قائمة الاختصارات تُفتَح بـ ؟ وتُغلَق بـ Escape", async ({ page }) => {
  await open(page);
  await page.waitForSelector("table.tb tbody tr");
  await page.keyboard.press("?");
  await expect(page.locator('[data-testid="keyboard-help"]')).toBeVisible();
  await page.keyboard.press("Escape");
  await expect(page.locator('[data-testid="keyboard-help"]')).toHaveCount(0);
});

test("المجموعان يصلان من الخادم ولا يُحسبان في المتصفّح", async ({ page }) => {
  await open(page, { rows: 40 });
  await page.waitForSelector("table.tb tbody tr");
  const totalDebit = await page.locator('[data-testid="total-debit"] span').getAttribute("title");
  const response = await page.request.get(
    `${MOCK}/api/v1/companies/${COMPANY}/trial-balance?book=MAIN&period=2026-05&rows=40`
  );
  const body = (await response.json()) as { totalDebit: string; balanced: boolean };
  expect(totalDebit).toBe(body.totalDebit);
  await expect(page.locator('[data-testid="balanced-pill"]')).toHaveAttribute(
    "data-balanced",
    String(body.balanced)
  );
});
