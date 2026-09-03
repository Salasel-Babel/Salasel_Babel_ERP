#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   مِسبارُ الاستقامة لشاشات المرفقات وحالِ الصنف
   The alignment probe for the attachment screens and the item-lifecycle screen
   ───────────────────────────────────────────────────────────────────────────
   **لماذا وُجد.** `scripts/align-audit.mjs` يزور كل مسارٍ في `SCREENS` كما
   يفتحه زائرٌ أول، والخادمُ الوهمي **لا ينشر أبواب المرفقات ولا أبواب دورة
   حياة الصنف**. فثلاثةُ ألواحٍ في هذا التسليم لا تُرسَم في تلك الحال —
   وصفُ المرفق، ولوحا تصحيحِه وسحبِه، ونموذجُ تصحيح الصنف — فيقرأ المسحُ
   الساكن **صفّين اثنين لا غير**، و«صفران من ثلاث شاشات» ليس دليلاً على
   شيء، وهو بالضبط عطلُ «مسحٌ لا يقرأ شيئاً يمرّ دائماً» (فخ-43).

   فهذا المسبار — على نمط `tests/align-probe.mjs` الذي سبقه حرفاً — يفتح
   المسار، **ويجعل الشاشة تصل إلى حال النموذج** بأجوبةٍ مطابقةٍ للعقد
   يعترضها عن الشبكة، ثم يقيس **بدالّة القياس نفسها** التي يستعملها المقياس
   الحاكم: تُقرأ من `scripts/align-audit.mjs` نصّاً ولا تُنسَخ هنا، فلا
   تنحرف نسخةٌ ثانية عن الأصل.

   **والشاهد السلبي في المسبار نفسه، لا في وثيقة.** بعلَم `--strip-hints`
   يُزال من الصفحة — في المتصفّح، بعد الرسم — صندوقُ وصفِ **أوّلِ حقلٍ في كل
   صفّ** لا الأوصافُ كلُّها: العطل الذي وصفه ADR-0078 هو **جارٌ بلا وصفٍ
   بجانب جارٍ بوصف**، ونزعُها كلِّها يُبقي الصفّ مستوياً فلا يُثبت شيئاً
   (مقيس: 0.00px عند النزع الشامل، و61.17px عند نزع واحدٍ في الصفّ).
   فإن لم يرتفع `inkBottom` بهذا النزع، فالمقياس لا يقيس شيئاً — ولذلك
   **يسقط المسبار في وضع الشاهد السلبي حين يكون الانكسار صفراً**.

       node tests/align-probe-attachments.mjs --web-port 5473 --mock-port 5474
       node tests/align-probe-attachments.mjs --strip-hints        # الشاهد السلبي
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
const flag = (n) => argv.includes("--" + n);
const WEB_PORT = Number(opt("web-port", "5473"));
const MOCK_PORT = Number(opt("mock-port", "5474"));
const STRIP = flag("strip-hints");
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

/* ── أجوبةٌ مطابقةٌ للعقد: تمرّ بفاكّ الترميز المُولَّد نفسه في المتصفّح ──
   ولا بيانات شخصية ولا اسم مضيفٍ ولا اعتماد في أيٍّ منها: أسماءُ ملفّاتٍ
   وصفيّة، ومعرّفاتٌ اصطناعية، وبصمةٌ من ستّينَ وأربعةِ أصفارٍ وأرقام. */
const ATTACH_ID = "dddddddd-dddd-4ddd-8ddd-ddddddddddd1";
const ATTACH_ID_2 = "dddddddd-dddd-4ddd-8ddd-ddddddddddd2";
const ITEM_ID = "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1";
const DIGEST = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

const ATTACHMENT = {
  id: ATTACH_ID,
  fileName: "فاتورة-مورّد-ممسوحة.pdf",
  mediaType: "application/pdf",
  byteLength: 184320,
  contentHash: DIGEST,
  contentPath: "/api/v1/companies/" + COMPANY + "/attachments/" + ATTACH_ID + "/content",
  sourceDocumentType: "purchasing.supplier_bill",
  sourceDocumentId: "ffffffff-ffff-4fff-8fff-fffffffffff1",
  storedAt: "2026-05-04T09:12:31.0000000Z",
  storedBy: "99999999-9999-4999-8999-999999999991",
  supersedes: null,
  supersededBy: null,
  version: 1,
  withdrawal: null,
};

const WITHDRAWN = {
  ...ATTACHMENT,
  id: ATTACH_ID_2,
  fileName: "صورة-مكرّرة.png",
  mediaType: "image/png",
  byteLength: 51204,
  version: 2,
  supersedes: ATTACH_ID,
  withdrawal: {
    reasonKey: "duplicate",
    withdrawnAt: "2026-05-06T11:02:00.0000000Z",
    withdrawnBy: "99999999-9999-4999-8999-999999999992",
  },
};

const ATTACHMENT_PAGE = { items: [ATTACHMENT, WITHDRAWN], skip: 0, take: 50, total: 2 };

const ITEM = {
  id: ITEM_ID,
  code: "ITM-0001",
  itemGroup: "RAW",
  baseUnit: "PCE",
  name: { ar: "حديد تسليح قطر اثني عشر", en: "Rebar, twelve millimetre" },
  units: [{ unitCode: "BOX", numerator: 12, denominator: 1 }],
};

const ITEM_LIST = { itemCount: 1, items: [ITEM] };
const LIFECYCLE = {
  id: ITEM_ID,
  code: "ITM-0001",
  isActive: true,
  holdsStock: false,
  placementsWithStock: 0,
};

/** المسارات التي يعترضها المسبار، وجوابُ كلٍّ منها. */
const ROUTES = [
  [/\/attachments$/, ATTACHMENT_PAGE],
  [/\/attachments\/[^/]+$/, ATTACHMENT],
  [/\/items$/, ITEM_LIST],
  [/\/items\/[^/]+\/lifecycle$/, LIFECYCLE],
  [/\/items\/[^/]+$/, ITEM],
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

/** ما يُفعَل على كل شاشة كي تصل ألواحُها إلى الرسم. */
const PROBES = [
  {
    path: "/attachments",
    async reach(page) {
      await page.waitForSelector('[data-testid="attach-table"]');
      await page.locator('[data-testid="attach-pick"]').first().click();
      await page.waitForSelector('[data-testid="attach-ticket-seconds"]');
      await page.waitForSelector('[data-testid="attach-source-type"]');
    },
  },
  {
    path: "/attachments/custody",
    async reach(page) {
      await page.fill('[data-testid="attach-lookup-id"]', ATTACH_ID);
      await page.click('[data-testid="attach-read"]');
      await page.waitForSelector('[data-testid="attach-revise-type"]');
      await page.waitForSelector('[data-testid="attach-reason"]');
    },
  },
  {
    path: "/inventory/item-lifecycle",
    async reach(page) {
      await page.waitForSelector('[data-testid="life-table"]');
      await page.locator('[data-testid="life-pick"]').first().click();
      await page.waitForSelector('[data-testid="life-unit-code"]');
      await page.waitForSelector('[data-testid="life-name-ar"]');
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
  const report = { generatedAt: new Date().toISOString(), stripHints: STRIP, passes: [] };
  let worstInk = 0;
  let brokenInk = 0;
  let brokenTop = 0;
  let rowsSeen = 0;
  let fieldsSeen = 0;

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
          /* ── الشاهد السلبي: نزعُ صناديق الوصف بعد الرسم ─────────────────
             لا تعديلَ ملفٍّ ولا بناءٍ ثانٍ: الحقلُ نفسه يبقى، ويُنزَع منه
             ما تفرضه القاعدة، فيُقرأ الفرق على البنية عينها. */
          if (STRIP) {
            await page.evaluate(() => {
              /* **حقلٌ واحد في كل صفّ بلا وصف** — لا الصفُّ كلُّه. ونزعُ
                 الأوصاف كلِّها **لا يُنتج تسنيناً**: تسويةُ الصندوق تبقى
                 قائمة حين تخلو الخانةُ عند الجميع. والعطلُ الذي وصفه
                 ADR-0078 هو **الجار بلا وصفٍ بجانب جارٍ بوصف**، وهذا ما
                 يُعيد إنتاجه هذا الشاهد حرفاً. */
              for (const row of document.querySelectorAll("#main .grid, #main .filterbar")) {
                const first = row.querySelector(":scope > .field .field__desc");
                if (first) first.remove();
              }
            });
          }
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
          descTop: metric("descTop"), gutter: metric("gutter"), inkBottom: metric("inkBottom"),
        };
        rowsSeen += rows.length;
        fieldsSeen += measured.pageUnits || 0;
        brokenInk += m.inkBottom.broken;
        brokenTop += m.controlTop.broken;
        worstInk = Math.max(worstInk, m.inkBottom.max);
        out.screens.push({ path: probe.path, error, rows: rows.length, fields: measured.pageUnits || 0, ...m });
        process.stdout.write(
          "  " + tag.padEnd(9) + " " + probe.path.padEnd(28) +
          " حقول=" + String(measured.pageUnits || 0).padStart(2) +
          " صفوف=" + String(rows.length).padStart(2) +
          " ctrlTop=" + m.controlTop.broken + "/" + m.controlTop.of +
          " gutter=" + m.gutter.broken + "/" + m.gutter.of +
          " ink=" + m.inkBottom.broken + "/" + m.inkBottom.of +
          " أقصى=" + m.inkBottom.max.toFixed(2) +
          (error ? "  ⚠ " + error : "") + "\n"
        );
      }
      report.passes.push(out);
      await ctx.close();
    }
  }
  await browser.close();
  const file = path.join(OUT, STRIP ? "align-probe-attachments-stripped.json" : "align-probe-attachments.json");
  writeFileSync(file, JSON.stringify(report, null, 2));

  /* **ولا مسحَ فارغٍ يمرّ**: صفرٌ من صفرِ صفوفٍ ليس نجاحاً بل عمى (فخ-43). */
  if (rowsSeen === 0 || fieldsSeen === 0) {
    console.error("لم يُقَس صفٌّ واحد — المسبار أعمى. / no row measured: the probe is blind.");
    process.exit(2);
  }
  console.log(
    "\n· حقولٌ مقيسة: " + fieldsSeen + "  ·  صفوفٌ مقيسة: " + rowsSeen +
    "  ·  قاعُ الحبر منكسر: " + brokenInk + "  ·  حافّة أعلى التحكّم منكسرة: " + brokenTop +
    "  ·  أقصى قاعِ حبر: " + worstInk.toFixed(2) + "px" + (STRIP ? "   [شاهدٌ سلبي: الأوصاف منزوعة]" : "")
  );
  console.log("· التقرير: " + file);
  /* في التشغيل العادي: أي انكسارٍ أحمر. وفي الشاهد السلبي: **العكس** —
     انكسارٌ صفريّ يعني أن المقياس لا يقيس شيئاً، فيسقط. */
  if (STRIP) process.exit(brokenInk > 0 ? 0 : 3);
  process.exit(brokenInk + brokenTop > 0 ? 1 : 0);
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
