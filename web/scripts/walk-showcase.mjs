#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   المشي على كل شاشة — في متصفّح، على الملفّ نفسه الذي يُنشَر
   ───────────────────────────────────────────────────────────────────────────
   بناءٌ ينجح لا يقول شيئاً عن شاشةٍ تُرسَم. ولذلك يُفتح الملفّ بـ`file://`
   ويُمشى على كل مسارٍ في `SCREENS`، ويُفحص في كلٍّ منها:
     ١ · لا خطأ في المِعراض ولا وعدٌ مرفوض بلا التقاط.
     ٢ · محتوىً حقيقي: نصٌّ أو عنصر يخصّ هذه الشاشة وحدها، لا هيكلٌ فارغ.
     ٣ · شريط الإفصاح حاضر.
   ثم تُلتقط صورةٌ لكلٍّ منها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { chromium } from "@playwright/test";
import { mkdirSync, readFileSync, writeFileSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath, pathToFileURL } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const page_ = resolve(here, "../../artifacts/babel-demo.preview.html");
const shots = resolve(here, "../../artifacts/shots");
mkdirSync(shots, { recursive: true });

/** الشاشات تُقرأ من `sections.ts` نفسه، فلا قائمة ثانية تنحرف. */
const sections = readFileSync(resolve(here, "../src/app/shell/sections.ts"), "utf8");
const block = /export const SCREENS[^=]*=\s*\[([\s\S]*?)\n\];/.exec(sections);
if (!block) throw new Error("لم يُقرأ SCREENS من sections.ts");
const SCREENS = [...block[1].matchAll(/path: "([^"]+)"[^}]*section: "([^"]+)"/g)].map((m) => ({
  path: m[1],
  section: m[2],
}));

/** علامةٌ تخصّ كل شاشة وحدها — نصّاً ظاهراً أو مُعرِّف اختبار. */
const MARKS = JSON.parse(readFileSync(resolve(here, "showcase-marks.json"), "utf8"));

const browser = await chromium.launch({ executablePath: "/opt/pw-browsers/chromium" });
const context = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 2,
  locale: "ar-SA",
  colorScheme: "dark",
});

const results = [];
const ONLY_ACTS = process.env.SHOWCASE_ONLY === "acts";

for (const screen of ONLY_ACTS ? [] : SCREENS) {
  const errors = [];
  const page = await context.newPage();
  page.on("console", (message) => {
    if (message.type() === "error") errors.push("console: " + message.text().slice(0, 300));
  });
  page.on("pageerror", (error) => errors.push("pageerror: " + String(error).slice(0, 300)));
  /* **أي طلبٍ يغادر الصفحة عطلٌ في ذاته**: المُضيف يحجب كل أصلٍ خارجيّ
     صامتاً، فالصفحة يجب ألّا تطلب شيئاً غير ملفّها. */
  page.on("request", (request) => {
    const url = request.url();
    if (!url.startsWith("file://") && !url.startsWith("data:") && !url.startsWith("blob:")) {
      errors.push("طلبٌ غادر الصفحة · outbound request: " + url.slice(0, 200));
    }
  });
  page.on("requestfailed", (request) => {
    errors.push("طلبٌ أخفق · failed request: " + request.url().slice(0, 200));
  });

  let rendered = false;
  let noteSeen = false;
  const mark = MARKS[screen.path];
  try {
    await page.goto(pathToFileURL(page_).href + "#" + screen.path, { waitUntil: "load" });
    await page.waitForSelector('[data-testid="showcase-note"]', { timeout: 15000 });
    noteSeen = true;
    if (mark?.testId) await page.waitForSelector('[data-testid="' + mark.testId + '"]', { timeout: 15000 });
    if (mark?.text) await page.getByText(mark.text, { exact: false }).first().waitFor({ timeout: 15000 });
    /* لوحُ الحدود يُبتلع فيُقرأ هيكلاً فارغاً — يُفحَص صراحةً. */
    const boundary = await page.locator("text=/حدث خطأ غير متوقّع|Something went wrong/").count();
    if (boundary > 0) errors.push("لوح حدود ظاهر · error boundary shown");
    rendered = true;
  } catch (error) {
    errors.push("انتظار: " + String(error).split("\n")[0].slice(0, 200));
  }

  await page.waitForTimeout(400);
  const name = screen.path === "/" ? "root" : screen.path.replace(/^\//, "").replace(/\//g, "-");
  await page.screenshot({ path: resolve(shots, name + ".png"), fullPage: false });
  results.push({ path: screen.path, section: screen.section, rendered, noteSeen, errors });
  await page.close();
}

/* ───────────────────────────── تفاعلٌ في كل قسم، ورفضٌ يُبلَغ ────────── */

const interactions = JSON.parse(readFileSync(resolve(here, "showcase-interactions.json"), "utf8"));
const acted = [];
for (const step of interactions) {
  const errors = [];
  const page = await context.newPage();
  page.on("console", (m) => { if (m.type() === "error") errors.push("console: " + m.text().slice(0, 300)); });
  page.on("pageerror", (e) => errors.push("pageerror: " + String(e).slice(0, 300)));
  let ok = false;
  let current = "التحميل";
  try {
    await page.goto(pathToFileURL(page_).href + "#" + step.path, { waitUntil: "load" });
    await page.waitForSelector('[data-testid="showcase-note"]', { timeout: 15000 });
    for (const [index, action] of step.actions.entries()) {
      current = "خطوة " + (index + 1) + " · " + action.selector;
      const target = page.locator(action.selector).first();
      if (action.waitFor) await target.waitFor({ timeout: 15000 });
      if (action.fill !== undefined) await target.fill(action.fill);
      else if (action.select !== undefined) await target.selectOption({ index: action.select });
      else if (action.check) await target.check();
      else await target.click();
      await page.waitForTimeout(action.settle ?? 250);
    }
    current = "التوقُّع «" + step.expect + "»";
    /* النصّ يُفحص في نصّ الصفحة لا بـ`getByText`: بعض الخلايا تُرسَم داخل
       حاوياتٍ بلا صندوق مقاسٍ (حركةُ الظهور)، فتُقرأ «غير مرئية» وهي مقروءة. */
    await page.waitForFunction(
      (needle) => (document.body.innerText || "").includes(needle),
      step.expect,
      { timeout: 15000 }
    );
    ok = true;
    await page.screenshot({ path: resolve(shots, "act-" + step.name + ".png") });
  } catch (error) {
    errors.push(current + " ← " + String(error).split("\n")[0].slice(0, 200));
    await page.screenshot({ path: resolve(shots, "act-" + step.name + "-FAILED.png") });
  }
  acted.push({ name: step.name, path: step.path, ok, errors });
  await page.close();
}

/* ─────────────────────── المظهر الفاتح: التطبيق يدعمه، فيُفحص لا يُفترض ── */

const lightContext = await browser.newContext({
  viewport: { width: 1440, height: 900 },
  deviceScaleFactor: 2,
  locale: "ar-SA",
  colorScheme: "light",
});
const light = [];
for (const path of ["/", "/realestate/lease", "/inventory/stock", "/hr/payroll", "/design"]) {
  const page = await lightContext.newPage();
  const errors = [];
  page.on("pageerror", (e) => errors.push(String(e).slice(0, 200)));
  page.on("console", (m) => { if (m.type() === "error") errors.push(m.text().slice(0, 200)); });
  await page.goto(pathToFileURL(page_).href + "#" + path, { waitUntil: "load" });
  await page.waitForSelector('[data-testid="showcase-note"]', { timeout: 15000 });
  /* المظهر يُبدَّل من داخل التطبيق: الافتراض داكنٌ بقرار مالك، فالفاتح
     يُختار من مبدّل المظهر نفسه الذي يستعمله المستخدم. */
  await page.selectOption('[data-testid="theme-switcher"]', "light").catch(() => {});
  await page.waitForTimeout(500);
  const theme = await page.evaluate(() => document.documentElement.getAttribute("data-theme"));
  const ink = await page.evaluate(() => getComputedStyle(document.body).backgroundColor);
  const name = path === "/" ? "root" : path.replace(/^\//, "").replace(/\//g, "-");
  await page.screenshot({ path: resolve(shots, "light-" + name + ".png") });
  light.push({ path, theme, background: ink, errors });
  await page.close();
}

await browser.close();

const report = { screens: results, interactions: acted, light };
writeFileSync(resolve(here, "../../artifacts/walk.json"), JSON.stringify(report, null, 2));

let bad = 0;
console.log("\nالمسار                              | رُسمت | إفصاح | أخطاء");
console.log("────────────────────────────────────┼───────┼───────┼──────");
for (const r of results) {
  const fail = !r.rendered || !r.noteSeen || r.errors.length > 0;
  if (fail) bad++;
  console.log(
    r.path.padEnd(35) + " |  " + (r.rendered ? "✓" : "✗") + "    |  " + (r.noteSeen ? "✓" : "✗") +
      "    |  " + r.errors.length
  );
  for (const e of r.errors) console.log("      · " + e);
}
console.log("\nتفاعلات:");
for (const a of acted) {
  if (!a.ok) bad++;
  console.log("  " + (a.ok ? "✓" : "✗") + " " + a.name + " (" + a.path + ")");
  for (const e of a.errors) console.log("      · " + e);
}
console.log("\nالمظهر الفاتح:");
for (const l of light) {
  const fail = l.theme !== "light" || l.errors.length > 0;
  if (fail) bad++;
  console.log("  " + (fail ? "✗" : "✓") + " " + l.path + "  data-theme=" + l.theme + "  خلفية=" + l.background);
  for (const e of l.errors) console.log("      · " + e);
}
console.log("\n" + (bad === 0 ? "✓ كل الشاشات والتفاعلات نظيفة" : "✗ " + bad + " إخفاقاً"));
process.exit(bad === 0 ? 0 : 1);
