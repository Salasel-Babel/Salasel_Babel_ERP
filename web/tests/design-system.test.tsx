/* ═══════════════════════════════════════════════════════════════════════════
   طبقة التصميم — الحرّاس التي تمنع انحرافها عن عقدها
   ───────────────────────────────────────────────────────────────────────────
   خمسةُ وكلاءٍ سيبنون فوق هذه الطبقة. والانحراف الذي يكلّف ليس عطلاً في
   الرسم — بل **اسمٌ يعد بشيء ولا يوجد**: مفردةُ حركةٍ في الشيفرة بلا قاعدةٍ
   في CSS تُطبَّق صامتةً ولا تفعل شيئاً، ومفتاحُ لغةٍ ناقصٌ يسقط إلى العربية
   بصمت. وهذان بالضبط ما تفحصه هذه المجموعة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { render, screen, within } from "@testing-library/react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { MOTION, MOTION_DWELL_MS, LedgerTable, ProvenanceMark, ConfidenceMeter, bandOf } from "../src/ui";
import { MOTIONS, PALETTE, PROVENANCES } from "../src/screens/design/catalogue";
import { SCREENS, SECTIONS, sectionOf } from "../src/app/shell/sections";
import { DesignScreen } from "../src/screens/design/DesignScreen";
import { demoRows, DEMO_TOTAL_CREDIT, DEMO_TOTAL_DEBIT } from "../src/screens/design/data";

/* الجذر من `process.cwd()` كما في `voice-number.test.ts` — وأنواع Node
   مقصورةٌ على `tests/node-shims.d.ts` عمداً، فلا يستدعي مكوّنٌ `fs`. */
const SRC = path.resolve(process.cwd(), "src");
const read = (rel: string) => readFileSync(path.resolve(SRC, rel), "utf8");

function Wrap(props: { children: React.ReactNode; locale?: string }) {
  return (
    <LocaleProvider i18n={createI18n()} initial={props.locale ?? "ar"}>
      {props.children}
    </LocaleProvider>
  );
}

describe("مفردات الحركة", () => {
  const css = read("styles/motion.css");

  it("لكل مفردةٍ في الشيفرة قاعدةٌ في CSS — فلا صنفٌ يَعِد ولا يفعل", () => {
    /* حارس لافراغ: قائمةٌ فارغة تمرّ على كل شيء. */
    expect(Object.keys(MOTION).length).toBeGreaterThanOrEqual(7);
    for (const cls of Object.values(MOTION)) {
      expect(css, "لا قاعدة للصنف " + cls).toContain("." + cls);
    }
  });

  it("لكل مفردةٍ مدّةٌ معلومة، ولا مدّة سالبة", () => {
    for (const name of Object.keys(MOTION) as (keyof typeof MOTION)[]) {
      expect(MOTION_DWELL_MS[name]).toBeGreaterThanOrEqual(0);
    }
  });

  it("لا translateX في لغة الحركة — الأفقي فيزيائيٌّ لا يعرف الاتجاه", () => {
    expect(css).not.toContain("translateX(");
  });

  it("التفضيل المخفَّض يُبقي الحالة ولا يُلغي الرسالة", () => {
    expect(css).toContain("prefers-reduced-motion");
    /* الرفض يبقى مرئياً بحدٍّ أحمر حين تُغلق الحركة. */
    const reduced = css.slice(css.indexOf("prefers-reduced-motion"));
    expect(reduced).toContain("--glow-refusal");
  });
});

describe("طبقة الرموز", () => {
  const tokens = read("styles/cinematic.css");

  it("كل رمزٍ يعرضه الفهرس معرَّفٌ في الطبقة", () => {
    expect(PALETTE.length).toBeGreaterThanOrEqual(15);
    const defined = new Set(
      [...tokens.matchAll(/(--[a-z0-9-]+)\s*:/g)].map((m) => m[1])
    );
    const semantic = read("styles/tokens.css");
    const theme = read("styles/theme/theme-default.css");
    for (const entry of PALETTE) {
      const known =
        defined.has(entry.token) ||
        semantic.includes(entry.token + ":") ||
        theme.includes(entry.token.replace("--color-", "--") + ":");
      expect(known, "رمزٌ معروضٌ وغير معرَّف: " + entry.token).toBe(true);
    }
  });

  it("الفاتح مُعرَّف على الجذر المجرّد، والداكن يُعاد تعريفه مرّتين", () => {
    expect(tokens).toContain(":root{");
    expect(tokens).toContain('@media (prefers-color-scheme:dark)');
    expect(tokens).toContain(':root[data-theme="dark"]');
    expect(tokens).toContain(':root:not([data-theme="light"])');
  });
});

describe("عقد الأقسام الخمسة", () => {
  it("خمسةٌ لا أقلّ ولا أكثر، ولكلٍّ لونٌ ومفتاح اسم", () => {
    expect(SECTIONS).toHaveLength(5);
    for (const s of SECTIONS) {
      expect(s.labelKey).toMatch(/^app\.section\./);
      expect(s.tint).toMatch(/^var\(--section-/);
      /* القسم غير المبنيّ **بلا مسار** — لا رابطٌ ميت. */
      if (!s.built) expect(s.path).toBeNull();
    }
  });

  it("كل مفتاح اسمٍ معرَّفٌ في اللغات الأربع", () => {
    const i18n = createI18n();
    for (const code of ["ar", "en", "ur", "hi"]) {
      i18n.use(code);
      for (const s of SECTIONS) {
        const text = i18n.t(s.labelKey);
        expect(text, code + " ← " + s.labelKey).not.toBe(s.labelKey);
        expect(text.length).toBeGreaterThan(1);
      }
      for (const m of MOTIONS) {
        expect(i18n.t(m.whenKey), code + " ← " + m.whenKey).not.toBe(m.whenKey);
      }
    }
  });

  it("كل شاشةٍ مبنيّة تقع في قسمٍ معروف", () => {
    for (const s of SCREENS) {
      expect(SECTIONS.some((x) => x.id === s.section)).toBe(true);
      expect(sectionOf(s.path).id).toBe(s.section);
    }
  });
});

describe("الجدول المالي", () => {
  const labels = {
    caption: "ميزان",
    code: "الحساب",
    account: "الاسم",
    debit: "مدين",
    credit: "دائن",
    total: "المجموع",
  };

  it("نصّ المال على السلك يبقى في الصفحة بايتاً ببايت، ولا يصير عائماً", () => {
    const { container } = render(
      <Wrap>
        <LedgerTable
          rows={demoRows(false)}
          labels={labels}
          totalDebit={DEMO_TOTAL_DEBIT}
          totalCredit={DEMO_TOTAL_CREDIT}
        />
      </Wrap>
    );
    const cells = container.querySelectorAll("td .amt");
    expect(cells.length).toBeGreaterThan(10);
    /* المعروض مقرَّب، والأصل باقٍ في السمة — وهو نصٌّ لا رقم. */
    const titles = [...cells].map((c) => c.getAttribute("title"));
    expect(titles).toContain("318940.7500");
    expect(container.querySelector("tfoot .amt")?.getAttribute("title")).toBe("802871.2500");
  });

  it("الصفّ الواصل يحمل مفردة الوصول، والمُستنتَج يحمل وسمه", () => {
    const { container } = render(
      <Wrap>
        <LedgerTable rows={demoRows(true)} labels={labels} />
      </Wrap>
    );
    expect(container.querySelectorAll("tr." + MOTION.arrive).length).toBeGreaterThan(5);
    expect(container.querySelectorAll('tr[data-inferred="true"]').length).toBe(1);
  });

  it("الحالات الثلاث الأخرى تعرض بديلها ولا تعرض صفوفاً", () => {
    const { container } = render(
      <Wrap>
        <LedgerTable
          rows={demoRows(false)}
          labels={labels}
          state="empty"
          placeholder={<p>لا شيء</p>}
        />
      </Wrap>
    );
    expect(container.querySelector("tbody")).toBeNull();
    expect(container.querySelector('[data-state="empty"]')).not.toBeNull();
  });
});

describe("الحضور الذكي", () => {
  it("العتبات الثلاث تُصنَّف من نصٍّ بلا عائم", () => {
    expect(bandOf("100")).toBe("high");
    expect(bandOf("96")).toBe("high");
    expect(bandOf("80")).toBe("high");
    expect(bandOf("71")).toBe("medium");
    expect(bandOf("60")).toBe("medium");
    expect(bandOf("43")).toBe("low");
    expect(bandOf("7")).toBe("low");
  });

  it("درجة الثقة تُبلَّغ لقارئ الشاشة بقيمتها ومداها", () => {
    render(
      <Wrap>
        <ConfidenceMeter percent="88" label="ثقة" />
      </Wrap>
    );
    const meter = screen.getByRole("meter");
    expect(meter.getAttribute("aria-valuetext")).toBe("88");
    expect(meter.getAttribute("aria-valuemax")).toBe("100");
  });

  it("المصادر الستّة كلّها موسومة، وما يملكه الإنسان محايد", () => {
    const i18n = createI18n();
    i18n.use("ar");
    const { container } = render(
      <Wrap>
        <div>
          {PROVENANCES.map((p) => (
            <ProvenanceMark key={p} source={p} label={i18n.t("screen.voice.provenance." + p)} />
          ))}
        </div>
      </Wrap>
    );
    const marks = container.querySelectorAll(".prov");
    expect(marks).toHaveLength(6);
    const typed = container.querySelector('.prov[data-source="typed"]');
    expect(typed).not.toBeNull();
    expect(within(typed as HTMLElement).getByText(/أدخلتَه/)).toBeTruthy();
  });
});

describe("صفحة العرض /design", () => {
  it("تُرسم في اللغات الأربع بلا سقوط، وبأقسامها الخمسة", () => {
    for (const locale of ["ar", "en", "ur", "hi"]) {
      const view = render(
        <Wrap locale={locale}>
          <DesignScreen />
        </Wrap>
      );
      const root = view.container.querySelector('[data-testid="design-screen"]');
      expect(root, locale).not.toBeNull();
      for (const id of ["palette", "motion", "presence", "primitives", "ledger"]) {
        expect(
          view.container.querySelector('[data-testid="section-' + id + '"]'),
          locale + " ← " + id
        ).not.toBeNull();
      }
      /* حارس لافراغ: صفحةٌ تُرسم فارغةً تمرّ على كل تأكيد لا يقيس حجمها. */
      expect((root?.textContent ?? "").length, locale).toBeGreaterThan(2000);
      view.unmount();
    }
  });

  it("لا نصّ مفتاحٍ خام يتسرّب إلى الشاشة في أي لغة", () => {
    for (const locale of ["ar", "en", "ur", "hi"]) {
      const view = render(
        <Wrap locale={locale}>
          <DesignScreen />
        </Wrap>
      );
      const text = view.container.textContent ?? "";
      expect(text, locale).not.toMatch(/screen\.design\./);
      expect(text, locale).not.toMatch(/app\.section\./);
      view.unmount();
    }
  });
});
