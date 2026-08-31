/* ═══════════════════════════════════════════════════════════════════════════
   الحدّ الأدنى للتباين — **مُنفَّذ لا موصى به**
   ───────────────────────────────────────────────────────────────────────────
   هذه المجموعة هي التي تجعل عتبة WCAG AA **جزءاً من البناء**. وقبلها كان
   الرقم يعيش في تعليقٍ داخل ملفّ السمة وفي ملاحظةٍ في design/README.md §٧ —
   ورقمٌ في تعليق لا يمنع أحداً من كتابة لونٍ أفتح بدرجتين بعد ستّة أشهر.

   والقياس نفسه يقع في `scripts/contrast.mjs` لا هنا، لسببٍ عملي: البوّابة
   المحلية تشغّله **بلا `npm ci`** لأنه لا يستورد إلا وحدات Node المدمجة،
   فثمنه ثوانٍ ويقع قبل أي تثبيت. وهذا الملفّ يشغّل الشيء نفسه داخل vitest
   فيسقط `npm test` أيضاً — بابان على العطل نفسه، لا بابٌ واحد يُنسى.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { audit, distinctness, PAIRS, PALETTES, THRESHOLD, MIN_DELTA_E } from "../scripts/contrast.mjs";

/* الأنواع مكتوبةٌ هنا لا مُستنتَجة من ملفّ .mjs: `checkJs` مطفأة، فالمستنتَج
   اتحادٌ فضفاض يجعل قالب النصّ يطبع [object Object] بلا أن يشتكي أحد. */
interface Spread {
  readonly palette: string;
  readonly theme: string;
  readonly a: string;
  readonly b: string;
  readonly deltaE?: number;
  readonly hue?: number;
  readonly pass: boolean;
}

const result = audit();
const spread = distinctness() as readonly Spread[];

function describeRow(r: {
  palette: string; theme: string; id: string; kind: string;
  ratio: number; need: number; fgHex: string; bgHex: string; where: string;
}): string {
  return (
    `${r.palette}/${r.theme} · ${r.id} (${r.kind}) = ${r.ratio}:1 والعتبة ${r.need}:1` +
    `\n    ${r.fgHex} على ${r.bgHex}\n    ${r.where}`
  );
}

describe("عتبة التباين", () => {
  it("الجرد ليس فارغاً ولا ضامراً — قائمةٌ فارغة تمرّ على كل شيء", () => {
    expect(PAIRS.length).toBeGreaterThanOrEqual(60);
    expect(Object.keys(PALETTES).length).toBe(2);
    expect(result.rows.length).toBe(PAIRS.length * Object.keys(PALETTES).length * 2);
  });

  it("كل زوجٍ يجتاز عتبته في اللوحتين وفي السمتين", () => {
    expect(
      result.failures.map(describeRow).join("\n  "),
      "أزواجٌ دون عتبة WCAG 2.1 AA — شغّل: node scripts/contrast.mjs"
    ).toBe("");
  });

  it("الداكن الصريح وتفضيل النظام يعرّفان نفس الرموز بنفس القيم", () => {
    expect(result.parity.join("\n  ")).toBe("");
  });

  it("العتبات هي عتبات المعيار لا عتباتٌ مخفَّضة", () => {
    expect(THRESHOLD.text).toBe(4.5);
    expect(THRESHOLD.large).toBe(3);
    expect(THRESHOLD.nontext).toBe(3);
  });
});

describe("اللون يحمل معنى، فلا يُسمح بأن يصير رمادياً", () => {
  it("المدين والدائن يبقيان متمايزين، وكلٌّ في عائلته اللونية", () => {
    const problems = spread.filter((d) => !d.pass);
    expect(
      problems
        .map((d) => `${d.palette}/${d.theme} · ${d.a} / ${d.b} = ${d.hue ?? d.deltaE}`)
        .join("\n  "),
      `ألوانٌ تحمل معنيين مختلفين تقاربت دون ΔE ${MIN_DELTA_E} أو خرجت من عائلتها`
    ).toBe("");
  });

  it("رأسا المدين والدائن يفترقان بالصبغة لا بالشدّة وحدها", () => {
    const hues = spread.filter((d) => d.hue !== undefined && d.palette === "default");
    expect(hues.length).toBe(4);
    for (const h of hues) expect(h.pass).toBe(true);
  });
});

describe("الحبر فوق الأسطح الملوّنة — الرموز الستّة التي بدأ منها هذا كلّه", () => {
  const SRC = path.resolve(process.cwd(), "src");
  const theme = readFileSync(path.resolve(SRC, "styles/theme/theme-default.css"), "utf8");

  it("كل رمز --on-* مقيسٌ في الجرد — فلا رمزَ حبرٍ يمرّ بلا قياس", () => {
    const declared = new Set(
      [...theme.matchAll(/(--on-[a-z-]+)\s*:/g)].map((m) => m[1])
    );
    expect(declared.size).toBeGreaterThanOrEqual(7);
    const measured = PAIRS.map((p) => (Array.isArray(p.fg) ? p.fg.join(" ") : p.fg)).join(" ");
    for (const token of declared) {
      expect(measured, "رمزُ حبرٍ غير مقيس: " + token).toContain(token);
    }
  });

  it("الداكن لا يكتب #fff حبراً فوق سطحٍ ملوّن — وهو العطل الأصلي بعينه", () => {
    /* من **القاعدة** لا من ذكرها في تعليق الرأس — التعليق يشرح القاعدة
       ويسبقها، فالبدء من أول ذكرٍ نصّي يبتلع كتلة الفاتح كلّها. */
    const at = theme.indexOf(':root[data-theme="dark"]{');
    expect(at, "لم تُعثر كتلة الداكن الصريحة").toBeGreaterThan(0);
    const darkBlock = theme.slice(at, theme.indexOf("}", at));
    expect(darkBlock).not.toMatch(/--on-(brand|debit|credit|danger|success|warning)\s*:\s*#fff/);
  });
});

/* ═══════════════════════════════════════════════════════════════════════════
   اللوحة المختارة هي اللوحة المطبَّقة — وهذا لم يكن مضموناً
   ───────────────────────────────────────────────────────────────────────────
   اللوحة الثانية تُربَط ملفَّ CSS يُطفَأ حين لا تُختار. وكان الإطفاء بـ
   `link.disabled` وحدها، وهي **تُفقَد** إن وصلت الورقة بعد ضبطها — فتُطبَّق
   لوحةٌ لم يخترها أحد. الفحص هنا على السلوك لا على النصّ: يُرسَم المبدّل،
   ويُقرأ `media` من الوسم الذي حُقن فعلاً.
   ═══════════════════════════════════════════════════════════════════════════ */
describe("مبدّل اللوحة", () => {
  it("اللوحة الثانية مُطفأة بـmedia حين لا تُختار، فلا يضيع الإطفاء مع ترتيب التحميل", async () => {
    const { render, waitFor } = await import("@testing-library/react");
    const { LocaleProvider } = await import("../src/i18n/react");
    const { createI18n } = await import("../src/i18n/setup");
    const { ThemeSwitcher } = await import("../src/app/shell/Switchers");

    render(
      <LocaleProvider i18n={createI18n()} initial="ar">
        <ThemeSwitcher accessiblePaletteHref="/theme-accessible.css" />
      </LocaleProvider>
    );

    await waitFor(() => {
      const link = document.head.querySelector<HTMLLinkElement>('link[data-palette="accessible"]');
      expect(link, "لم يُحقن وسم اللوحة الثانية إطلاقاً").not.toBe(null);
      expect(link?.media, "الإطفاء بـdisabled وحدها يضيع إن وصلت الورقة بعده").toBe("not all");
    });
  });
});
