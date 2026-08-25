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
      command: `npx vite preview --port ${WEB_PORT} --strictPort`,
      port: WEB_PORT,
      reuseExistingServer: !process.env.CI,
      stdout: "ignore",
    },
  ],
});
