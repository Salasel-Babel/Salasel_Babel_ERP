import { existsSync } from "node:fs";
import { defineConfig, devices } from "@playwright/test";

/* الخادم الوهمي يُقلع مع الاختبارات، والواجهة تُقدَّم من بناء الإنتاج نفسه
   الذي يُشحن — لا من خادم تطوير بسلوك مختلف. */
const MOCK_PORT = 5099;
const WEB_PORT = 5174;

const CHROMIUM = [process.env.PLAYWRIGHT_CHROMIUM, "/opt/pw-browsers/chromium"].find(
  (candidate): candidate is string => !!candidate && existsSync(candidate)
);

export default defineConfig({
  testDir: "./e2e",
  fullyParallel: false,
  forbidOnly: !!process.env.CI,
  retries: 0,
  workers: 1,
  reporter: process.env.CI ? [["list"], ["html", { open: "never" }]] : [["list"]],
  timeout: 60_000,
  use: {
    baseURL: `http://127.0.0.1:${WEB_PORT}`,
    trace: "off",
    screenshot: "off",
    /* المتصفّح: يُستعمل المثبَّت مسبقاً إن وُجد (بيئة التطوير هنا لا تُنزّل
       متصفّحات)، وإلا يُترك لـPlaywright ليختار ما نصّبه في التكامل المستمر. */
    launchOptions: CHROMIUM ? { executablePath: CHROMIUM } : {},
  },
  projects: [{ name: "chromium", use: { ...devices["Desktop Chrome"] } }],
  webServer: [
    {
      command: `node scripts/mock-api.mjs --port ${MOCK_PORT}`,
      port: MOCK_PORT,
      reuseExistingServer: !process.env.CI,
      stdout: "ignore",
    },
    {
      /* ‏`--host 127.0.0.1` ليس زينة: بلا مضيف صريح يربط vite على `localhost`،
         وهو اسمٌ يُحلّ على `::1` قبل `127.0.0.1` على مُشغّلات التكامل المستمر
         (‏/etc/hosts فيها السطران، وNode يُرجعهما بترتيب DNS الحرفي). فيربط
         الخادم على IPv6 وحدها، ويجتاز فحصُ الجاهزية عبرها، ثم **يُرفض كل طلب**
         من الاختبارات لأن `baseURL` عنوانٌ رابع الإصدار — وهو ما وقع فعلاً:
         103 اختباراً بـERR_CONNECTION_REFUSED على 127.0.0.1:5174 في أول تشغيل
         حقيقي للمصفوفة. والخادم الوهمي يربط 127.0.0.1 صراحةً منذ البداية،
         فهذا يُطابقه.

         Without an explicit host, vite binds `localhost`, which resolves to
         `::1` before `127.0.0.1` on CI runners: the readiness probe passes
         over IPv6 while every test is refused on IPv4. */
      command: `npx vite preview --host 127.0.0.1 --port ${WEB_PORT} --strictPort`,
      port: WEB_PORT,
      reuseExistingServer: !process.env.CI,
      stdout: "ignore",
    },
  ],
});
