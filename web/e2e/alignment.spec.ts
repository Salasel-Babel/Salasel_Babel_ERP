/* ═══════════════════════════════════════════════════════════════════════════
   حارسُ الاستقامة — يفتح كل شاشة في متصفّحٍ حقيقي ويقيس كل صفّ
   the alignment guard — every screen, in a real browser, every row measured
   ───────────────────────────────────────────────────────────────────────────
   **لماذا حارسٌ لا مراجعةَ لقطات:** العطل الذي أُصلح («الصفُّ يملك المسارات»
   في `components.css`) عطلٌ **يعود في أول مرّةٍ يُضاف فيها تلميح**. لا شيء في
   المصرّف ولا في اختبارات الوحدة يرى بكسلاً؛ ومراجعةُ لقطةٍ بالعين تمرّ على
   20px ولا تراها. فالحكم هنا رقميّ: يُفتح كل مسار في `SCREENS`، وتُشتقّ الصفوف
   من الهندسة، ويسقط البناء باسم الشاشة والوعاء والفرق بالبكسل.

   **المسارات تُقرأ من `src/app/shell/sections.ts` نفسه** — لا من نسخةٍ ثانية
   هنا — فلا تنجو شاشةٌ جديدة من الحارس لأن أحداً نسي تسجيلها في مصفوفة.

   **السماحية، ولماذا ليست صفراً.** قيمتان:
     · حافّة أعلى عنصر التحكّم: **0.5px**. عناصرُ الصفّ الواحد تقف اليوم على
       *مسارٍ واحد* في الشبكة، فالفرق البنيوي صفر؛ وما قد يبقى هو تقريبُ
       عرضٍ كسريّ (`1fr` لا يقسم على عددٍ صحيح). ونصفُ بكسلٍ منطقيّ = بكسلٌ
       ماديٌّ واحد عند deviceScaleFactor=2، أي أصغرُ ما يمكن أن يُرى أصلاً.
       المقيس بعد الإصلاح على 21 شاشة × 4 لغات: **0.00px**.
     · خطُّ التسمية وخطُّ الوصف: **1.0px**. صفٌّ واحد قد يخلط عائلتَي خطّ
       عمداً — الخطّ العربي للنصّ و`--font-mono` للمعرّفات — ولعائلتين
       صندوقا سطرٍ مختلفان بكسورٍ من البكسل. أكبرُ فرقٍ من هذا النوع قِيس في
       المنتج: **0.67px** بين «موقعٌ مُسمّى» و«DEFAULT …» في
       `/inventory/movements`. سماحيةُ صفرٍ هنا تُسقط البناء على مقاييس خطٍّ
       لا على تخطيط، وحارسٌ يسقط للسبب الخطأ يُعطَّل في أول أسبوع.
   والعطل الذي يحرسه هذان الرقمان قِيس بين 42.78px و99.30px — فالهامش واسع.

   التشغيل: `npx playwright test alignment.spec.ts`
   ولقطاتُ الإثبات: `ALIGN_SHOTS=1 npx playwright test alignment.spec.ts`
   والمصفوفة الكاملة (خمسة عروض): `ALIGN_FULL=1 …`
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync, mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { test, expect, type Page } from "@playwright/test";
import {
  measureAlignment,
  markMisalignment,
  unmarkMisalignment,
  type MeasuredRow,
  type PageMeasure,
} from "./align-measure";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = path.resolve(HERE, "..");
const OUT_DIR = path.resolve(WEB_ROOT, "..", "artifacts", "align");

const MOCK = "http://127.0.0.1:5099";
const COMPANY = "11111111-1111-4111-8111-111111111111";

/* ── السماحيتان ─────────────────────────────────────────────────────────── */
const CONTROL_TOLERANCE_PX = 0.5;
const TEXT_TOLERANCE_PX = 1;

const SHOTS = process.env.ALIGN_SHOTS === "1";
const FULL = process.env.ALIGN_FULL === "1";

/** المسارات من `sections.ts` نفسه، فلا تنحرف نسخةٌ ثانية عن الأصل. */
function screenPaths(): string[] {
  const src = readFileSync(path.join(WEB_ROOT, "src/app/shell/sections.ts"), "utf8");
  const block = src.slice(src.indexOf("export const SCREENS"));
  const paths = [...block.matchAll(/\{\s*path:\s*"([^"]+)"/g)].map((m) => m[1]);
  if (paths.length === 0) throw new Error("لم يُقرأ أي مسار من SCREENS — الحارس أعمى، لا ناجح.");
  return paths;
}
const PATHS = screenPaths();

/* ── المصفوفة ───────────────────────────────────────────────────────────────
   اتّجاهان × سمتان × عرضا المالك (1440 مكتبيّ · 390 هاتف) ، ثم 1024 للغات
   الأربع لأنها **العرض الذي ينكسر عنده كل شيء**: التسميات تلتفّ والتلميحات
   تصير خمسة أسطر. `ALIGN_FULL=1` يفتح 1280 و1180 معهما. */
interface Pass {
  readonly locale: string;
  readonly dir: "rtl" | "ltr";
  readonly theme: "dark" | "light";
  readonly width: number;
  readonly height: number;
}
const BASE: readonly Pass[] = [
  { locale: "ar", dir: "rtl", theme: "dark", width: 1440, height: 900 },
  { locale: "ar", dir: "rtl", theme: "light", width: 1024, height: 800 },
  { locale: "ar", dir: "rtl", theme: "dark", width: 390, height: 844 },
  { locale: "en", dir: "ltr", theme: "dark", width: 1440, height: 900 },
  { locale: "en", dir: "ltr", theme: "light", width: 1024, height: 800 },
  { locale: "en", dir: "ltr", theme: "dark", width: 390, height: 844 },
  { locale: "ur", dir: "rtl", theme: "dark", width: 1024, height: 800 },
  { locale: "hi", dir: "ltr", theme: "dark", width: 1024, height: 800 },
];
const EXTRA: readonly Pass[] = [
  { locale: "ar", dir: "rtl", theme: "dark", width: 1280, height: 900 },
  { locale: "ar", dir: "rtl", theme: "dark", width: 1180, height: 900 },
  { locale: "en", dir: "ltr", theme: "dark", width: 1280, height: 900 },
  { locale: "en", dir: "ltr", theme: "dark", width: 1180, height: 900 },
  { locale: "ur", dir: "rtl", theme: "dark", width: 1440, height: 900 },
  { locale: "hi", dir: "ltr", theme: "dark", width: 1440, height: 900 },
];
const PASSES = FULL ? [...BASE, ...EXTRA] : BASE;

function urlOf(p: string, locale: string): string {
  const q = new URLSearchParams({
    lang: locale,
    baseUrl: MOCK,
    companyId: COMPANY,
    book: "MAIN",
    period: "2026-05",
  });
  return p + "?" + q.toString();
}

/** يصف صفّاً منحرفاً بلغةٍ يفهمها من سيُصلحه. */
function describeRow(screen: string, row: MeasuredRow, metric: string, delta: number): string {
  const worst = [...row.detail].sort((a, b) => a.controlTop - b.controlTop);
  const lo = worst[0];
  const hi = worst[worst.length - 1];
  return (
    `${screen} · [${row.parentClass}] · ${metric} ${delta.toFixed(2)}px بين ` +
    `«${lo.label || lo.testId}» (وصف ${lo.descLines} سطراً) و«${hi.label || hi.testId}» ` +
    `(وصف ${hi.descLines} سطراً) — ${row.members} حقولاً في الصفّ`
  );
}

async function settle(page: Page): Promise<void> {
  await page.waitForSelector("#main", { timeout: 20_000 });
  await page.evaluate(() => document.fonts.ready);
  await page.evaluate(
    () => new Promise<void>((r) => requestAnimationFrame(() => requestAnimationFrame(() => r())))
  );
  await page.waitForTimeout(120);
}

for (const pass of PASSES) {
  const tag = `${pass.locale}-${pass.width}-${pass.theme}`;
  test(`استقامةُ الصفوف · ${tag} (${pass.dir})`, async ({ page }) => {
    test.setTimeout(180_000);
    await page.setViewportSize({ width: pass.width, height: pass.height });
    await page.addInitScript(
      ([loc, th]) => {
        try {
          localStorage.setItem("sb-locale", loc);
          localStorage.setItem("sb-theme", th);
          localStorage.setItem("sb-palette", "default");
        } catch {
          /* تصفّح خاص */
        }
      },
      [pass.locale, pass.theme]
    );

    const faults: string[] = [];
    const report: { path: string; measure: PageMeasure }[] = [];
    let pageUnits = 0;
    let pageRows = 0;
    let worstControl = 0;

    for (const p of PATHS) {
      await page.goto(urlOf(p, pass.locale));
      await settle(page);
      expect(await page.locator("html").getAttribute("dir"), `اتجاه الجذر على ${p}`).toBe(pass.dir);

      const measure: PageMeasure = await page.evaluate(measureAlignment);
      report.push({ path: p, measure });

      for (const f of measure.slotFaults) {
        faults.push(
          `${p} · حقل «${f.label}» فيه ${f.descChildren} عناصر وصفٍ مباشرة — ` +
            `الخانة الثالثة تحمل ساكناً واحداً وإلّا تراكبا وأُخفي نصّ`
        );
      }

      if (measure.overflowX > 1) {
        faults.push(`${p} · انزلاقٌ أفقي ${measure.overflowX}px — الصفحة أعرض من نافذتها`);
      }

      const rows = measure.rows.filter((r) => r.scope === "page");
      pageUnits += measure.pageUnits;
      pageRows += rows.length;
      for (const row of rows) {
        if (row.controlTop) worstControl = Math.max(worstControl, row.controlTop.max);
        if (row.controlTop && row.controlTop.max > CONTROL_TOLERANCE_PX) {
          faults.push(describeRow(p, row, "حافّة أعلى التحكّم", row.controlTop.max));
        }
        if (!row.mixedLabelFont && row.labelFirstLineTop && row.labelFirstLineTop.max > TEXT_TOLERANCE_PX) {
          faults.push(describeRow(p, row, "خطّ التسمية الأول", row.labelFirstLineTop.max));
        }
        if (!row.mixedDescFont && row.descTop && row.descTop.max > TEXT_TOLERANCE_PX) {
          faults.push(describeRow(p, row, "أعلى كتلة الوصف", row.descTop.max));
        }
      }

      /* الهاتف: لا صفوف أصلاً (كل شبكةٍ تنهار إلى عمود واحد دون 520px)، فما
         يُحرَس هناك هو **إيقاع العمود**: فجوةٌ واحدة بين كل حقلٍ وتاليه. */
      for (const r of measure.rhythms) {
        if (r.scope !== "page") continue;
        if (r.spread > TEXT_TOLERANCE_PX) {
          faults.push(
            `${p} · [${r.parentClass}] · إيقاعٌ رأسي متفاوت ${r.spread.toFixed(2)}px — ` +
              `الفجوات بين حقولٍ متجاورة: ${r.gaps.map((g) => g.toFixed(1)).join(" · ")}px`
          );
        }
      }

      if (SHOTS) {
        mkdirSync(OUT_DIR, { recursive: true });
        const base = path.join(OUT_DIR, tag + "__" + (p === "/" ? "root" : p.replace(/^\//, "").replace(/\//g, "-")));
        await page.screenshot({ path: base + ".png", fullPage: true });
        await page.evaluate(markMisalignment, measure.rows);
        await page.screenshot({ path: base + ".marked.png", fullPage: true });
        await page.evaluate(unmarkMisalignment);
      }
    }

    if (SHOTS) {
      mkdirSync(OUT_DIR, { recursive: true });
      writeFileSync(path.join(OUT_DIR, "guard-" + tag + ".json"), JSON.stringify(report, null, 1));
    }

    /* حارسٌ على الحارس: ممرٌّ لم يقس حقلاً واحداً ليس نجاحاً بل مقياسٌ أعمى.
       **والعدّ بالحقول لا بالصفوف عمداً:** عند 390px تنهار كل شبكةٍ إلى عمودٍ
       واحد، فلا وجود لصفٍّ متعدّد الحقول أصلاً — وهي حقيقةٌ مقيسة لا افتراض.
       فلو حُكم بالصفوف لسقط ممرّ الهاتف على لا شيء، أو — أسوأ — لمرّ ممرٌّ
       مكتبيّ لم يُرسم فيه شيء. */
    expect(pageUnits, `${tag}: لم يُقَس أي حقلٍ في الصفحة — المقياس أعمى لا ناجح`).toBeGreaterThan(0);
    if (pass.width >= 1024) {
      expect(pageRows, `${tag}: لا صفوف متعدّدة الحقول عند ${pass.width}px — المقياس أعمى`).toBeGreaterThan(0);
    }
    expect(faults.join("\n"), `${tag}: أقصى انحرافٍ في حافّة التحكّم ${worstControl.toFixed(2)}px`).toBe("");
  });
}
