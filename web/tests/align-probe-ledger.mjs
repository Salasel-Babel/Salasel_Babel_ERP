#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   مِسبارُ الاستقامة لنموذج العكس — ما لا يبلغه المسح الساكن
   The alignment probe for the reversal form the static sweep cannot reach
   ───────────────────────────────────────────────────────────────────────────
   **لماذا وُجد.** `scripts/align-audit.mjs` يزور كل مسارٍ في `SCREENS` كما
   يفتحه زائرٌ أول: بلا معرّفٍ مُلصَق وبلا جواب. و`/ledger/entry` في تلك
   الحال تُظهر حقلاً واحداً — معرّف القيد — فيقرأها المسح `rows=0`، و**صفرٌ
   من صفر ليس دليلاً** (فخ-43). ونموذجُ العكس نفسه — وهو أخطر ما في هذا
   التسليم — لا يُرسَم إلا بعد أن يُقرأ قيد، وذلك عمدٌ لا سهو: لا نموذجَ
   عكسٍ لقيدٍ لم يُعرَض.

   فهذا المسبار يفتح المسار، **ويجعل الشاشة تصل إلى حال النموذج** بجوابٍ
   مطابقٍ للعقد يعترضه عن الشبكة، ثم يفتح لوحَ الإذن الاستثنائي، ثم يقيس
   **بدالّة القياس نفسها** التي يستعملها المقياس الحاكم: تُقرأ من
   `scripts/align-audit.mjs` نصّاً ولا تُنسَخ هنا، فلا تنحرف نسخةٌ ثانية.

       node tests/align-probe-ledger.mjs --web-port 5497 --mock-port 5498
   ═══════════════════════════════════════════════════════════════════════════ */
import { spawn } from "node:child_process";
import { existsSync, readFileSync, mkdirSync, writeFileSync } from "node:fs";
import net from "node:net";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = path.resolve(HERE, "..");
const REPO_ROOT = path.resolve(WEB_ROOT, "..");
const argv = process.argv.slice(2);
const opt = (n, d) => {
  const i = argv.indexOf("--" + n);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : d;
};
const WEB_PORT = Number(opt("web-port", "5497"));
const MOCK_PORT = Number(opt("mock-port", "5498"));
const OUT = path.resolve(REPO_ROOT, opt("out", "artifacts/align"));
const CHROMIUM = [process.env.PLAYWRIGHT_CHROMIUM, "/opt/pw-browsers/chromium"].find(
  (c) => !!c && existsSync(c)
);

/* ── دالّة القياس تُقرأ من المقياس الحاكم، ولا تُكتب هنا ثانيةً ─────────── */
const auditSrc = readFileSync(path.join(WEB_ROOT, "scripts/align-audit.mjs"), "utf8");
const at = auditSrc.indexOf("function measureInPage()");
if (at < 0) throw new Error("لم تُوجَد measureInPage في المقياس الحاكم.");
let depth = 0;
let end = -1;
for (let i = auditSrc.indexOf("{", at); i < auditSrc.length; i += 1) {
  if (auditSrc[i] === "{") depth += 1;
  else if (auditSrc[i] === "}") {
    depth -= 1;
    if (depth === 0) { end = i + 1; break; }
  }
}
const MEASURE_SRC = auditSrc.slice(at, end);

const COMPANY = "11111111-1111-4111-8111-111111111111";
const BASE = "**/api/v1/companies/*";
const ENTRY_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1";

/* ── جوابٌ مطابقٌ للعقد: يمرّ بفاكّ الترميز المُولَّد نفسه في المتصفّح ─── */
const ENTRY = {
  book: "MAIN",
  chainSequence: "412",
  currency: "SAR",
  entryDate: "2026-05-14",
  entryHash: "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  entryId: ENTRY_ID,
  entryNumber: "1042",
  lines: [
    {
      credit: "0", currency: "SAR", debit: "10.5000",
      descriptionAr: "بضاعةٌ مستلمة", descriptionEn: "Goods received",
      lineNo: 1, qualifier: "RAW", role: "inventory_control",
    },
    {
      credit: "10.5000", currency: "SAR", debit: "0",
      descriptionAr: "ذمّة المورّد", descriptionEn: "Supplier payable",
      lineNo: 2, qualifier: "TRADE", role: "accounts_payable",
    },
  ],
  memoAr: "استلامُ بضاعةٍ من المورّد",
  memoEn: "Goods received from the supplier",
  periodCode: "2026-05",
  reversesEntryId: null,
  status: "POSTED",
};

/** المسارات التي يعترضها المسبار، وجوابُ كلٍّ منها. */
const ROUTES = [[/\/journal-entries\/[^/]+$/, ENTRY]];

/** ما يُفعَل على الشاشة كي يصل نموذجُها إلى الرسم. */
const PROBES = [
  {
    path: "/ledger/entry",
    async reach(page) {
      await page.fill('[data-testid="ledger-entry-id"]', ENTRY_ID);
      await page.click('[data-testid="ledger-entry-read"]');
      await page.waitForSelector('[data-testid="ledger-rev-reason-ar"]');
      /* ولوحُ الإذن الاستثنائي صفٌّ رابعُ الحقول — يُفتح كي يُقاس هو أيضاً. */
      await page.click('[data-testid="ledger-rev-auth-open"]');
      await page.waitForSelector('[data-testid="ledger-rev-auth-by"]');
    },
  },
];

const VIEWPORTS = [
  { width: 1440, height: 900, name: "1440" },
  { width: 1024, height: 800, name: "1024" },
];
const LOCALES = [
  { locale: "ar", dir: "rtl" },
  { locale: "en", dir: "ltr" },
];

function waitForPort(port) {
  return new Promise((resolve, reject) => {
    const deadline = Date.now() + 60_000;
    const tick = () => {
      const s = net.connect(port, "127.0.0.1");
      s.on("connect", () => { s.destroy(); resolve(); });
      s.on("error", () => {
        s.destroy();
        if (Date.now() > deadline) reject(new Error("المنفذ لم يُفتح: " + port));
        else setTimeout(tick, 300);
      });
    };
    tick();
  });
}

async function main() {
  mkdirSync(OUT, { recursive: true });
  const children = [];
  const stop = () => {
    for (const c of children) {
      try { process.kill(-c.pid, "SIGTERM"); } catch { try { c.kill("SIGTERM"); } catch { /* انتهى */ } }
    }
  };
  process.on("exit", stop);

  children.push(spawn("node", ["scripts/mock-api.mjs", "--port", String(MOCK_PORT)], {
    cwd: WEB_ROOT, stdio: "ignore", detached: true,
  }));
  children.push(spawn("npx", ["vite", "preview", "--host", "127.0.0.1", "--port", String(WEB_PORT), "--strictPort"], {
    cwd: WEB_ROOT, stdio: "ignore", detached: true,
  }));
  await waitForPort(MOCK_PORT);
  await waitForPort(WEB_PORT);

  const browser = await chromium.launch({ executablePath: CHROMIUM, args: ["--font-render-hinting=none"] });
  const report = { generatedAt: new Date().toISOString(), passes: [] };
  let worst = 0;
  let broken = 0;
  let rowsSeen = 0;

  for (const l of LOCALES) {
    for (const v of VIEWPORTS) {
      const tag = l.locale + "-" + v.name;
      const ctx = await browser.newContext({
        viewport: { width: v.width, height: v.height },
        deviceScaleFactor: 2,
        locale: { ar: "ar-SA", en: "en-US" }[l.locale],
        colorScheme: "dark",
        reducedMotion: "reduce",
      });
      await ctx.addInitScript(([loc]) => {
        try {
          localStorage.setItem("sb-locale", loc);
          localStorage.setItem("sb-theme", "dark");
          localStorage.setItem("sb-palette", "default");
        } catch { /* تصفّح خاص */ }
      }, [l.locale]);
      const page = await ctx.newPage();
      await page.route(BASE + "/**", async (route) => {
        const u = new URL(route.request().url());
        for (const [re, body] of ROUTES) {
          if (re.test(u.pathname)) {
            await route.fulfill({ status: 200, contentType: "application/json", body: JSON.stringify(body) });
            return;
          }
        }
        await route.fulfill({
          status: 404,
          contentType: "application/problem+json",
          body: JSON.stringify({ type: "about:blank", title: "not found", status: 404, code: "http.not_found", detail: u.pathname }),
        });
      });

      const out = { tag, screens: [] };
      for (const probe of PROBES) {
        const q = new URLSearchParams({
          lang: l.locale, baseUrl: "http://127.0.0.1:" + MOCK_PORT, companyId: COMPANY,
          book: "MAIN", period: "2026-05",
        });
        let error = null;
        let measured = { pageUnits: 0, rows: [] };
        try {
          await page.goto("http://127.0.0.1:" + WEB_PORT + probe.path + "?" + q.toString(), { waitUntil: "load", timeout: 45_000 });
          await page.waitForSelector("#main", { timeout: 20_000 });
          await probe.reach(page);
          await page.evaluate(() => document.fonts.ready);
          await page.waitForTimeout(700);
          measured = await page.evaluate("(function(){" + MEASURE_SRC + "\nreturn measureInPage();})()");
        } catch (e) {
          error = String(e && e.message ? e.message : e);
        }
        const rows = (measured.rows || []).filter((r) => r.scope === "page");
        const metric = (k) => {
          const bad = rows.filter((r) => r[k] && r[k].max > 0.5);
          return { broken: bad.length, of: rows.length, max: bad.length ? Math.max(...bad.map((r) => r[k].max)) : 0 };
        };
        const m = {
          controlTop: metric("controlTop"), labelBaseline: metric("labelBaseline"),
          descTop: metric("descTop"), inkBottom: metric("inkBottom"),
        };
        rowsSeen += rows.length;
        broken += m.inkBottom.broken + m.controlTop.broken;
        worst = Math.max(worst, m.inkBottom.max, m.controlTop.max);
        out.screens.push({ path: probe.path, error, rows: rows.length, ...m });
        process.stdout.write(
          "  " + tag.padEnd(9) + " " + probe.path.padEnd(24) +
          " rows=" + String(rows.length).padStart(2) +
          " ctrlTop=" + m.controlTop.broken + "/" + m.controlTop.of +
          " label=" + m.labelBaseline.broken + "/" + m.labelBaseline.of +
          " desc=" + m.descTop.broken + "/" + m.descTop.of +
          " ink=" + m.inkBottom.broken + "/" + m.inkBottom.of +
          " worst=" + Math.max(m.inkBottom.max, m.controlTop.max).toFixed(2) +
          (error ? "  ⚠ " + error : "") + "\n"
        );
      }
      report.passes.push(out);
      await ctx.close();
    }
  }
  await browser.close();
  writeFileSync(path.join(OUT, "align-probe-ledger.json"), JSON.stringify(report, null, 2));
  /* **ولا مسحَ فارغٍ يمرّ**: صفرٌ من صفرِ صفوفٍ ليس نجاحاً بل عمى. */
  if (rowsSeen === 0) {
    console.error("لم يُقَس صفٌّ واحد — المسبار أعمى. / no row measured: the probe is blind.");
    process.exit(2);
  }
  console.log("\n· صفوفٌ مقيسة: " + rowsSeen + "  ·  منكسرة: " + broken + "  ·  أقصى: " + worst.toFixed(2) + "px");
  console.log("· التقرير: " + path.join(OUT, "align-probe-ledger.json"));
  process.exit(broken > 0 ? 1 : 0);
}

main().catch((e) => {
  console.error(e);
  process.exit(2);
});
