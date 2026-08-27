/* ═══════════════════════════════════════════════════════════════════════════
   إعداد Playwright الخاصّ بالتصوير — منفصل عن إعداد الاختبارات عمداً.
   ───────────────────────────────────────────────────────────────────────────
   إعداد `web/playwright.config.ts` يُقلع خادماً وهمياً ويقيس ضدّه؛ والتصوير
   يجب أن يقع على **الحزمة الحقيقية** التي أقامها `deploy/up.sh`: خادمٌ يقرأ من
   PostgreSQL، لا محاكاة. ولذلك إعدادان لا إعداد بمفتاح بيئة — مفتاحٌ يُنسى.
   ═══════════════════════════════════════════════════════════════════════════ */
import { existsSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { defineConfig, devices } from "@playwright/test";

const here = path.dirname(fileURLToPath(import.meta.url));
const root = path.resolve(here, "../..");

const CHROMIUM = [process.env.PLAYWRIGHT_CHROMIUM, "/opt/pw-browsers/chromium"].find(
  (candidate): candidate is string => !!candidate && existsSync(candidate)
);

export default defineConfig({
  testDir: path.join(root, "web/e2e"),
  testMatch: "demo-film.spec.ts",
  outputDir: path.join(root, "demo/showcase/.film"),
  fullyParallel: false,
  workers: 1,
  retries: 0,
  timeout: 25 * 60_000,
  reporter: [["list"]],
  use: {
    trace: "off",
    screenshot: "off",
    launchOptions: CHROMIUM ? { executablePath: CHROMIUM } : {},
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
});
