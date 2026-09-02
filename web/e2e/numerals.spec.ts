/* ═══════════════════════════════════════════════════════════════════════════
   الأرقام الجدولية — طفرةُ الرمز على الصفحة المرسومة
   Tabular numerals — mutating the token against the rendered page
   ───────────────────────────────────────────────────────────────────────────
   ‏`web/scripts/numerals.mjs` يحرس **الشيفرة**: لا تصريحَ يرسم رقماً إلا عبر
   الرمز. وهو لا يُثبت أن **الصفحة** تطيع: قد يبقى سطحٌ رقميّ محكوماً بشيء آخر
   لا يمرّ من CSS أصلاً. فهذا الملفّ يُشغّل القياس الذي **هزم** الدعوى الأصلية،
   اختباراً لا يدوياً: يُبدَّل الرمز وقت التشغيل، ويجب أن **يتحرّك كل رقم**.

   ‏**ولا قائمةَ أصنافٍ في هذا الملفّ إطلاقاً.** العقدة تُكتشَف بما **ترسمه**:
   كل عنصرٍ ورقيّ نصُّه يحمل رقماً بأيّ من مجموعات الأرقام الأربع التي يشحنها
   المنتج (لاتينية، عربية-هندية، فارسية، ديفاناغرية)، ومعه محتوى `::before`
   و`::after` وقيم حقول الإدخال. فالصنف الذي يُضاف الشهر القادم يُقاس لأنه
   **يرسم رقماً**، لا لأن أحداً كتب اسمه هنا.

   ‏**والمسارات تُقرأ من `router.tsx` نفسه** لا من قائمةٍ في هذا الملفّ: شاشةٌ
   جديدة تدخل القياس بمجرّد أن تدخل المُوجِّه.
   ‏(traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy)
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { test, expect } from "@playwright/test";

const MOCK = "http://127.0.0.1:5099";
const TOKEN = "mock-token";
const COMPANY = "11111111-1111-4111-8111-111111111111";

/** المسارات من المُوجِّه — مصدرٌ واحد، فلا تنحرف قائمةٌ ثانية عنه. */
function routesFromRouter(): string[] {
  const here = path.dirname(fileURLToPath(import.meta.url));
  const source = readFileSync(path.resolve(here, "../src/app/router.tsx"), "utf8");
  const found = [...source.matchAll(/\bpath:\s*"([^"]+)"/g)].map((m) => m[1]);
  return [...new Set(found)].sort();
}

const ROUTES = routesFromRouter();

/** كل ما قد تطلبه أيّ شاشة، مرّةً واحدة — الشاشة تأخذ ما يعنيها وتتجاهل الباقي. */
function urlFor(route: string, locale: string): string {
  const q = new URLSearchParams({
    lang: locale,
    baseUrl: MOCK,
    token: TOKEN,
    companyId: COMPANY,
    book: "MAIN",
    period: "2026-05",
  });
  return route + "?" + q.toString();
}

/* أرضيات اللافراغ: تشغيلٌ لا يجد أرقاماً يمرّ على كل شيء، ولا يُثبت شيئاً. */
const ROUTE_FLOOR = 20;
const NODE_FLOOR = 200;

/**
 * تُجمع العقد، وتُقرأ قيمتها، ثم يُطفَّر الرمزان معاً، ثم تُقرأ ثانيةً.
 * الطفرة على **الرمزين**: `--font-numeric-off` بابُ خروجٍ شرعيّ، ولو تُرك بلا
 * طفرة لبدت العقدةُ الخارجةُ منه كأنها مثبَّتةٌ بقيمةٍ حرفية.
 */
async function measure(page: import("@playwright/test").Page) {
  return page.evaluate(() => {
    const DIGIT = /[0-9٠-٩۰-۹०-९]/;
    const SKIP = new Set(["SCRIPT", "STYLE", "TEMPLATE", "NOSCRIPT"]);
    type Probe = { el: Element; pseudo: string | null };
    const probes: Probe[] = [];

    const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_ELEMENT);
    const all: Element[] = [document.body];
    while (walker.nextNode()) all.push(walker.currentNode as Element);

    for (const el of all) {
      if (SKIP.has(el.tagName)) continue;
      /* ورقة: لا عنصرَ ابن. فالقياس يقع حيث يُرسم الرقم فعلاً. */
      if (el.children.length === 0) {
        const value = (el as HTMLInputElement).value;
        const text = (el.textContent ?? "") + (typeof value === "string" ? value : "");
        if (DIGIT.test(text)) probes.push({ el, pseudo: null });
      }
      for (const pseudo of ["::before", "::after"]) {
        const content = getComputedStyle(el, pseudo).content;
        if (content && content !== "none" && DIGIT.test(content)) probes.push({ el, pseudo });
      }
    }

    const read = (p: Probe) => getComputedStyle(p.el, p.pseudo ?? undefined).fontVariantNumeric;
    const before = probes.map(read);

    const root = document.documentElement;
    root.style.setProperty("--font-numeric", "oldstyle-nums");
    root.style.setProperty("--font-numeric-off", "slashed-zero");
    const after = probes.map(read);
    root.style.removeProperty("--font-numeric");
    root.style.removeProperty("--font-numeric-off");

    const describe = (p: Probe) => {
      const el = p.el as HTMLElement;
      const text = (el.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 48);
      return (
        el.tagName.toLowerCase() +
        (el.className && typeof el.className === "string" ? "." + el.className.trim().replace(/\s+/g, ".") : "") +
        (p.pseudo ?? "") +
        " « " + text + " »"
      );
    };

    const frozen = probes
      .map((p, i) => ({ p, i }))
      .filter(({ i }) => before[i] === after[i])
      .map(({ p, i }) => describe(p) + "  ⟵ " + before[i]);

    return { total: probes.length, frozen, sampleBefore: before.slice(0, 3) };
  });
}

test.describe("كل رقم مرسوم يتحرّك حين يتحرّك الرمز", () => {
  const collected: { route: string; total: number; frozen: string[] }[] = [];

  for (const route of ROUTES) {
    test(`المسار ${route}`, async ({ page }) => {
      await page.setViewportSize({ width: 1440, height: 900 });
      const failures: string[] = [];
      page.on("pageerror", (e) => failures.push(String(e)));
      await page.goto(urlFor(route, "ar"), { waitUntil: "domcontentloaded" });
      /* الشاشات تجلب بياناتها بعد الرسم الأول؛ تُنتظر السكينة لا عنصرٌ بعينه،
         كي لا يعود اسمُ عنصرٍ قائمةً أخرى تُصان بيد. */
      await page.waitForLoadState("networkidle").catch(() => undefined);
      await page.waitForTimeout(250);

      const result = await measure(page);
      collected.push({ route, total: result.total, frozen: result.frozen });

      expect(
        result.frozen,
        `عقدٌ لم تتحرّك مع الرمز في ${route} — قيمتها مثبَّتة خارج الرمز:\n` +
          result.frozen.join("\n")
      ).toEqual([]);
    });
  }

  test.afterAll(() => {
    const nodes = collected.reduce((sum, c) => sum + c.total, 0);
    const withDigits = collected.filter((c) => c.total > 0).length;
    /* الحصيلة تُطبع: «مرّ» بلا عددٍ منظور هو ما يجعل فحصاً فارغاً يبدو فحصاً. */
    const frozen = collected.reduce((sum, c) => sum + c.frozen.length, 0);
    process.stdout.write(
      `\nالأرقام الجدولية · ${collected.length} مساراً · ${withDigits} منها رسمت رقماً · ` +
        `${nodes} عقدةً رقمية قِيست · ${nodes - frozen} تحرّكت مع الرمز · ${frozen} جامدة\n` +
        collected.map((c) => `   ${c.route} → ${c.total}`).join("\n") + "\n"
    );
    /* الأرضيتان تُفحصان هنا لا داخل مسارٍ واحد: مسارٌ بلا أرقام مشروع،
       وتشغيلٌ كامل بلا أرقام ليس مشروعاً — هو فحصٌ لا يفحص. */
    expect(collected.length, "مساراتٌ زُورت").toBeGreaterThanOrEqual(ROUTE_FLOOR);
    expect(nodes, "عقدٌ رقمية قِيست").toBeGreaterThanOrEqual(NODE_FLOOR);
    expect(withDigits, "مساراتٌ رسمت رقماً").toBeGreaterThanOrEqual(8);
  });
});

/* ═══════════════════════════════════════════════════════════════════════════
   والنتيجة لا السبب: هل تصطفّ الأرقام فعلاً؟
   ───────────────────────────────────────────────────────────────────────────
   ما سبق يُثبت أن الخاصّية **تُطلَب** من الرمز. وهذا شرطٌ لا كافٍ: `tabular-nums`
   طلبٌ إلى المِحرف، والمِحرف الذي لا يحمل الوجه الجدولي **يتجاهله بصمت** — ولا
   يظهر ذلك في `getComputedStyle` إطلاقاً، لأن القيمة المحسوبة تبقى `tabular-nums`
   بينما الأعمدة تتعرّج. والمحاسب لا يقرأ خاصّيةً، يقرأ عموداً.

   فيُقاس هنا **ما يراه**: عرضُ مِحرف كل رقمٍ في الوجه الذي تستعمله الصفحة فعلاً.
   ومجموعاتُ الأرقام المفحوصة **تُكتشَف مما رسمته الصفحة**، لا من قائمة.
   ═══════════════════════════════════════════════════════════════════════════ */
const DIGIT_SETS: Record<string, [number, number]> = {
  latin: [0x30, 0x39],
  arabicIndic: [0x660, 0x669],
  eastern: [0x6f0, 0x6f9],
  devanagari: [0x966, 0x96f],
};

async function digitMetrics(page: import("@playwright/test").Page, sets: Record<string, [number, number]>) {
  return page.evaluate((SETS) => {
    /* ما رسمته الصفحة فعلاً — لا ما قد ترسمه يوماً. */
    const rendered = new Set<string>();
    const walk = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT);
    while (walk.nextNode()) {
      for (const ch of walk.currentNode.nodeValue ?? "") {
        const c = ch.codePointAt(0) ?? 0;
        for (const [name, range] of Object.entries(SETS)) {
          if (c >= range[0] && c <= range[1]) rendered.add(name);
        }
      }
    }
    /* المسبار داخل الجسم نفسه، فيرث الوجه والخاصّية كما هما على الشاشة. */
    const host = document.createElement("div");
    host.style.position = "absolute";
    host.style.visibility = "hidden";
    host.style.whiteSpace = "pre";
    document.body.appendChild(host);
    const widthsOf = (range: [number, number]) =>
      Array.from({ length: 10 }, (_, i) => {
        const span = document.createElement("span");
        span.textContent = String.fromCodePoint(range[0] + i);
        host.appendChild(span);
        const w = Math.round(span.getBoundingClientRect().width * 100) / 100;
        span.remove();
        return w;
      });
    const measured: Record<string, { widths: number[]; distinct: number }> = {};
    for (const [name, range] of Object.entries(SETS)) {
      const widths = widthsOf(range);
      measured[name] = { widths, distinct: new Set(widths).size };
    }
    host.remove();
    return { rendered: [...rendered], measured, face: getComputedStyle(document.body).fontFamily };
  }, sets);
}

const LOCALES = ["ar", "en", "ur", "hi"] as const;

test.describe("الأرقام المرسومة تصطفّ فعلاً — لا الخاصّية مطلوبةً فحسب", () => {
  for (const locale of LOCALES) {
    test(`عرضُ المِحرف واحدٌ لكل مجموعةٍ ترسمها الصفحة · ${locale}`, async ({ page }) => {
      await page.goto(urlFor("/", locale), { waitUntil: "domcontentloaded" });
      await page.waitForLoadState("networkidle").catch(() => undefined);
      await page.waitForTimeout(250);
      const m = await digitMetrics(page, DIGIT_SETS);

      /* حارس اللافراغ: صفحةٌ بلا أرقام لا تُثبت شيئاً. */
      expect(m.rendered.length, "مجموعات أرقامٍ رسمتها الصفحة").toBeGreaterThan(0);
      for (const name of m.rendered) {
        const set = m.measured[name];
        expect(
          set.distinct,
          `المجموعة ${name} مرسومةٌ في ${locale} بعروضٍ مختلفة (${set.widths.join(", ")}) — ` +
            `الوجه ${m.face} لا ينفّذ الطلب، والعمود يتعرّج`
        ).toBe(1);
      }
    });
  }

  /* ── الثقب المُعلَن، مقيساً لا محكيّاً ──────────────────────────────────────
     الرمز شرطٌ لا كافٍ. مقيس على هذا الفرع: أرقام العربية-الهندية (٠-٩) في
     "IBM Plex Sans Arabic" لها **تسعةُ عروضٍ متمايزة** رغم `tabular-nums`، لأن
     الوجه لا يحمل `tnum` لها. والمنتج اليوم يرسم الأرقام اللاتينية في اللغات
     الأربع جميعاً، فالوعد قائم — **لكنه قائمٌ بالمِحرف لا بالخاصّية**.
     ومقيسٌ أيضاً أن اللاتينية في الأوجه الأربعة متساوية العرض **حتى مع
     `proportional-nums`**: أي أن هذه الأوجه لا تنفّذ الميزة أصلاً، فتبديلُ الرمز
     لا يُغيّر منظراً اليوم. وهذا الاختبار يُثبّت الحالتين معاً: فمن يبدّل مجموعة
     الأرقام المعروضة أو الوجه سيراهما تحمرّان، ويعرف أن عليه اختيار وجهٍ يحمل
     الوجه الجدولي — لا كتابة قاعدة CSS أخرى. */
  test("الثقب المُعلَن: العربية-الهندية لا تصطفّ في الوجه المشحون، والرمز لا يُصلح مِحرفاً", async ({ page }) => {
    await page.goto(urlFor("/", "ar"), { waitUntil: "domcontentloaded" });
    await page.waitForLoadState("networkidle").catch(() => undefined);
    await page.waitForTimeout(250);
    const m = await digitMetrics(page, DIGIT_SETS);

    /* ① المنتج يرسم اللاتينية وحدها — فالوعد المُعلَن للمستخدم صحيحٌ اليوم. */
    expect(m.rendered).toEqual(["latin"]);

    /* ② ولو رسم العربية-الهندية لانكسر العمود، ولا CSS يُنقذه. */
    expect(
      m.measured.arabicIndic.distinct,
      "إن صارت هذه ١ فقد تغيّر الوجه المشحون: الثقب أُغلق — حدِّث هذا الاختبار ونصّ الفخّ."
    ).toBeGreaterThan(1);
  });
});
