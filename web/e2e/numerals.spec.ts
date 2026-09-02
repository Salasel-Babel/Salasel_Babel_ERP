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

/**
 * خصائصُ **التقدُّم** — المجموعة المغلقة التي تقرّر عرض مِحرف الرقم.
 * وهي نفسها التي يملكها `web/scripts/numerals.mjs` ساكنةً، ومعها ما يضربه
 * في المقاس: القياس والوزن والنمط والتتبّع. ومن يضيف خاصّيةً تُغيّر التقدُّم
 * يضيفها هنا وهناك معاً، لا في أحدهما.
 */
const ADVANCE_PROPERTIES = [
  "fontFamily",
  "fontSize",
  "fontWeight",
  "fontStyle",
  "fontStretch",
  "fontVariantNumeric",
  "fontFeatureSettings",
  "fontVariationSettings",
  "letterSpacing",
] as const;

type NodeMetric = {
  signature: string;
  face: string;
  size: string;
  variant: string;
  widths: Record<string, number[]>;
  sample: string;
};

/**
 * ‏**يقيس كل عقدةٍ رقمية في سياقها هي.**
 *
 * ‏والنسخة السابقة كانت تُلحق مسبارها بـ`document.body` — فكانت تقيس **وجه
 * الجسم** أبداً، لا الوجه الذي تستعمله العقدة الرقمية. وأثرُ ذلك مقيس: رمزُ
 * حسابٍ سباعيّ يُرسم **58.81px في صفٍّ و68.20px في الذي يليه**، والمسبار
 * يقرأ رقماً واحداً في الحالين، و`getComputedStyle().fontVariantNumeric`
 * يقول `tabular-nums` في الحالين. فصار المسبار ينسخ خصائص التقدّم **من
 * العقدة نفسها**، ويعمل مع `<input>` والعناصر المستبدَلة أيضاً لأنه ينسخ
 * ولا يتطفّل على شجرتها.
 */
async function faceMetrics(page: import("@playwright/test").Page, sets: Record<string, [number, number]>) {
  return page.evaluate(
    ({ SETS, ADVANCE }) => {
      const DIGIT = /[0-9٠-٩۰-۹०-९]/;
      const SKIP = new Set(["SCRIPT", "STYLE", "TEMPLATE", "NOSCRIPT"]);

      /* العقد الرقمية — أوراقٌ ترسم رقماً. لا قائمةَ أصنافٍ هنا كذلك. */
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_ELEMENT);
      const all: Element[] = [document.body];
      while (walker.nextNode()) all.push(walker.currentNode as Element);

      const numeric: HTMLElement[] = [];
      const rendered = new Set<string>();
      for (const el of all) {
        if (SKIP.has(el.tagName)) continue;
        const value = (el as HTMLInputElement).value;
        const text = (el.textContent ?? "") + (typeof value === "string" ? value : "");
        for (const ch of text) {
          const c = ch.codePointAt(0) ?? 0;
          for (const [name, range] of Object.entries(SETS)) {
            if (c >= range[0] && c <= range[1]) rendered.add(name);
          }
        }
        if (el.children.length === 0 && DIGIT.test(text)) numeric.push(el as HTMLElement);
      }

      const host = document.createElement("div");
      host.style.position = "absolute";
      host.style.top = "0";
      host.style.left = "-99999px";
      host.style.whiteSpace = "pre";
      host.style.visibility = "hidden";
      document.body.appendChild(host);

      const advanceOf = (cs: CSSStyleDeclaration, text: string) => {
        const span = document.createElement("span");
        for (const key of ADVANCE) {
          const v = (cs as unknown as Record<string, string>)[key];
          if (v) (span.style as unknown as Record<string, string>)[key] = v;
        }
        span.textContent = text;
        host.appendChild(span);
        const w = Math.round(span.getBoundingClientRect().width * 100) / 100;
        span.remove();
        return w;
      };

      const metrics: {
        signature: string; face: string; size: string; variant: string;
        widths: Record<string, number[]>; sample: string;
      }[] = [];
      for (const el of numeric) {
        const cs = getComputedStyle(el);
        const widths: Record<string, number[]> = {};
        for (const name of rendered) {
          const range = SETS[name];
          widths[name] = Array.from({ length: 10 }, (_, i) =>
            advanceOf(cs, String.fromCodePoint(range[0] + i))
          );
        }
        const classes = typeof el.className === "string" ? el.className.trim().split(/\s+/).filter(Boolean).sort() : [];
        metrics.push({
          signature: el.tagName.toLowerCase() + (classes.length ? "." + classes.join(".") : ""),
          face: cs.fontFamily,
          size: cs.fontSize,
          variant: cs.fontVariantNumeric,
          widths,
          sample: (el.textContent ?? "").replace(/\s+/g, " ").trim().slice(0, 40),
        });
      }

      /* والوجهُ الذي كان يُقاس قبلاً — يُعاد هنا **مسمّى** كي لا يُخلط بغيره. */
      const bodyFace = getComputedStyle(document.body).fontFamily;
      host.remove();
      return { rendered: [...rendered], metrics, bodyFace };
    },
    { SETS: sets, ADVANCE: ADVANCE_PROPERTIES as unknown as string[] }
  );
}

const LOCALES = ["ar", "en", "ur", "hi"] as const;

/* أرضية اللافراغ للقياس المرسوم: صفحةٌ بلا عقدٍ رقمية لا تُثبت شيئاً. */
const METRIC_FLOOR = 5;

/**
 * ‏**بابُ الخروج الوحيد من وجه الأرقام — مُعلَنٌ ومغلق.**
 *
 * ‏«وجهٌ واحد لكل رقم في المنتج» دعوى أقوى مما يحتمله المنتج، وADR ثوابت
 * التصميم يقولها بنفسه: «حاجةُ عمودٍ إلى وجهٍ أحاديّ حقيقي ⇒ يصير الشرط
 * وجهٌ واحد لكل عمود». والنصّ الآليّ المعزول — رمزُ دفتر، ومفتاح لاتكرار،
 * ومعرّف تتبّع — ليس عموداً يقرؤه محاسب، وهو مكتوبٌ بصنفٍ واحد اسمه `.mono`
 * وغرضُه هذا بالضبط.
 *
 * ‏**فالقاعدة:** كل عقدةٍ ترسم رقماً تُرسم بوجه الجذر، **إلّا** ما حمل صنفاً
 * من هذه المجموعة — وهي **مغلقة ومسمّاة**، لا حدّاً أعلى ولا استثناء مسار.
 * وصنفٌ جديد يُخرج عقدةً من الوجه الواحد **يُحمِّر الاختبار** حتى يُكتب هنا،
 * وذلك إقرارٌ مقروء لا صمت — وهو منوال `OFF_TOKEN_USES` نفسه في `numerals.mjs`.
 */
const MACHINE_TEXT_CLASSES = ["mono"];

test.describe("الأرقام المرسومة تصطفّ فعلاً — لا الخاصّية مطلوبةً فحسب", () => {
  for (const locale of LOCALES) {
    test(`وجهٌ واحد وعرضٌ واحد لكل سطحٍ رقميّ · ${locale}`, async ({ page }) => {
      await page.goto(urlFor("/", locale), { waitUntil: "domcontentloaded" });
      await page.waitForLoadState("networkidle").catch(() => undefined);
      await page.waitForTimeout(250);
      const m = await faceMetrics(page, DIGIT_SETS);
      const metrics = m.metrics as NodeMetric[];

      expect(m.rendered.length, "مجموعات أرقامٍ رسمتها الصفحة").toBeGreaterThan(0);
      expect(metrics.length, "عقدٌ رقمية قِيست في سياقها").toBeGreaterThanOrEqual(METRIC_FLOOR);

      /* ① **وجهُ الجذر هو وجه الرقم، والخروج منه بصنفٍ مُعلَن وحده.**
         وهذا ما لم يكن يُقاس إطلاقاً: المسبار القديم كان يُلحق مِقياسه
         بـ`document.body`، فكان يقرأ **وجه الجسم** مهما كان وجه العقدة —
         فلا يرى `input.ctl.mono` ولا `tbody tr:nth-child(2n) .acct-code`.
         والحارس الساكن يُثبت أن وجه الجذر هو `--font-numeric-face` نفسه،
         فمقارنةُ العقد بوجه الجذر هنا مقارنةٌ برمز الأرقام بالتركيب. */
      const strays = metrics
        .filter((x) => x.face !== m.bodyFace)
        .filter((x) => !MACHINE_TEXT_CLASSES.some((c) => x.signature.split(".").includes(c)));
      expect(
        strays.map((x) => `${x.signature} ⟵ ${x.face} « ${x.sample} »`),
        "عقدٌ رقمية بوجهٍ غير وجه الجذر ولا تحمل صنفاً آلياً مُعلَناً في " + locale
      ).toEqual([]);

      /* وحارسُ لافراغ للباب نفسه: لو اختفى `.mono` من المنتج لصار الاستثناء
         نائماً ومرّ كل شيء — فيُقاس أنه **مستعمَل** لا أنه مكتوب. */
      const machine = metrics.filter((x) =>
        MACHINE_TEXT_CLASSES.some((c) => x.signature.split(".").includes(c))
      );
      const faceCount = new Set(metrics.map((x) => x.face)).size;
      expect(
        faceCount,
        `أوجهُ الأسطح الرقمية في ${locale} = ${faceCount}؛ والمُقَرّ وجهان: وجه الجذر ` +
          `و${MACHINE_TEXT_CLASSES.join("/")}. العقد الآلية المقيسة: ${machine.length}.\n` +
          [...new Map(metrics.map((x) => [x.face, x])).values()]
            .map((x) => `   ${x.face}  ⟵ ${x.signature} « ${x.sample} »`)
            .join("\n")
      ).toBeLessThanOrEqual(1 + MACHINE_TEXT_CLASSES.length);

      /* ② **العقدتان بالتوقيع نفسه ترسمان الرقم بالعرض نفسه.** وهو الشكل الذي
         هزم النسخة السابقة: قاعدةٌ على صفوفٍ زوجية تُبدّل الوجه، فيُرسم رمزُ
         الحساب السباعيّ نفسه بعرضين في الجدول الواحد. */
      const groups = new Map<string, NodeMetric[]>();
      for (const x of metrics) {
        const list = groups.get(x.signature) ?? [];
        list.push(x);
        groups.set(x.signature, list);
      }
      const split: string[] = [];
      for (const [signature, list] of groups) {
        /* الوجه أوّلاً — وهو ما يهزم الفحص الساكن حين تُبدّله قاعدةٌ أخرى. */
        if (new Set(list.map((x) => x.face)).size > 1) {
          split.push(
            `${signature} (${list.length} عقدة) ⟵ وجهان: ` +
              [...new Map(list.map((x) => [x.face, x])).values()]
                .map((x) => `${x.face} « ${x.sample} »`)
                .join("  ✧  ")
          );
          continue;
        }
        /* ثم العرض **عند المقاس نفسه**: مقاسان لصنفٍ واحد مشروعان، وعرضان
           عند مقاسٍ واحد ليسا كذلك. */
        const bySize = new Map<string, NodeMetric[]>();
        for (const x of list) {
          const kin = bySize.get(x.size) ?? [];
          kin.push(x);
          bySize.set(x.size, kin);
        }
        for (const [size, kin] of bySize) {
          const shapes = new Set(kin.map((x) => JSON.stringify(x.widths)));
          if (shapes.size > 1) {
            split.push(
              `${signature} @${size} (${kin.length} عقدة) ⟵ عروضٌ مختلفة: ` +
                [...new Map(kin.map((x) => [JSON.stringify(x.widths), x])).values()]
                  .map((x) => `${JSON.stringify(x.widths.latin ?? [])} « ${x.sample} »`)
                  .join("  ✧  ")
            );
          }
        }
      }
      expect(split, "عقدٌ بالتوقيع نفسه تُرسم بوجهين أو بمقاسين:\n" + split.join("\n")).toEqual([]);

      /* ③ **وداخل كل عقدة، الأرقام العشرة بعرضٍ واحد** — وهو ما تعنيه الجدولية. */
      const ragged: string[] = [];
      for (const x of metrics) {
        for (const [name, widths] of Object.entries(x.widths)) {
          if (name !== "latin") continue; /* الثقب المُعلَن أدناه يخصّ البقيّة. */
          if (new Set(widths).size !== 1) {
            ragged.push(`${x.signature} · ${name} · ${widths.join(", ")} ⟵ ${x.face}`);
          }
        }
      }
      expect(ragged, "أرقامٌ لاتينية بعروضٍ مختلفة داخل العقدة نفسها:\n" + ragged.join("\n")).toEqual([]);
    });
  }

  /* ── الثقب المُعلَن، مقيساً لا محكيّاً ──────────────────────────────────────
     الرمز شرطٌ لا كافٍ. مقيس على هذا الفرع: أرقام العربية-الهندية (٠-٩) في
     "IBM Plex Sans Arabic" لها **عروضٌ متمايزة** رغم `tabular-nums`، لأن الوجه
     لا يحمل `tnum` لها. والمنتج اليوم يرسم الأرقام اللاتينية في اللغات الأربع
     جميعاً، فالوعد قائم — **لكنه قائمٌ بالمِحرف لا بالخاصّية**. */
  test("الثقب المُعلَن: العربية-الهندية لا تصطفّ في الوجه المشحون، والرمز لا يُصلح مِحرفاً", async ({ page }) => {
    await page.goto(urlFor("/", "ar"), { waitUntil: "domcontentloaded" });
    await page.waitForLoadState("networkidle").catch(() => undefined);
    await page.waitForTimeout(250);
    const m = await faceMetrics(page, DIGIT_SETS);
    const metrics = m.metrics as NodeMetric[];

    /* ① المنتج يرسم اللاتينية وحدها — فالوعد المُعلَن للمستخدم صحيحٌ اليوم. */
    expect(m.rendered).toEqual(["latin"]);

    /* ② ولو رسم العربية-الهندية لانكسر العمود، ولا CSS يُنقذه. والقياس يقع
       في سياق عقدةٍ رقمية حقيقية، لا في سياق الجسم. */
    const context = metrics[0];
    expect(context, "عقدةٌ رقمية واحدة على الأقلّ").toBeTruthy();
    const arabicIndic = await page.evaluate(
      ({ signature, ADVANCE }) => {
        const el = [...document.querySelectorAll<HTMLElement>("*")].find((n) => {
          const classes = typeof n.className === "string" ? n.className.trim().split(/\s+/).filter(Boolean).sort() : [];
          return n.tagName.toLowerCase() + (classes.length ? "." + classes.join(".") : "") === signature;
        });
        if (!el) return null;
        const cs = getComputedStyle(el);
        const host = document.createElement("div");
        host.style.cssText = "position:absolute;top:0;left:-99999px;white-space:pre;visibility:hidden";
        document.body.appendChild(host);
        const widths = Array.from({ length: 10 }, (_, i) => {
          const span = document.createElement("span");
          for (const key of ADVANCE) {
            const v = (cs as unknown as Record<string, string>)[key];
            if (v) (span.style as unknown as Record<string, string>)[key] = v;
          }
          span.textContent = String.fromCodePoint(0x660 + i);
          host.appendChild(span);
          const w = Math.round(span.getBoundingClientRect().width * 100) / 100;
          span.remove();
          return w;
        });
        host.remove();
        return { widths, face: cs.fontFamily };
      },
      { signature: context.signature, ADVANCE: ADVANCE_PROPERTIES as unknown as string[] }
    );

    expect(arabicIndic, "سياقُ القياس وُجد").not.toBeNull();
    expect(
      new Set(arabicIndic!.widths).size,
      "إن صارت هذه ١ فقد تغيّر الوجه المشحون: الثقب أُغلق — حدِّث هذا الاختبار ونصّ الفخّ. " +
        `الوجه ${arabicIndic!.face} · العروض ${arabicIndic!.widths.join(", ")}`
    ).toBeGreaterThan(1);
  });
});
