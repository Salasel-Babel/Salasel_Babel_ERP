/* ═══════════════════════════════════════════════════════════════════════════
   طبقة التدويل — ما نُقل من design/ يبقى صحيحاً بعد النقل
   ═══════════════════════════════════════════════════════════════════════════ */
import { beforeEach, describe, expect, it, vi } from "vitest";
import { createI18n } from "../src/i18n/setup";
import { Display, INVISIBLE_RE, machine } from "../src/i18n/display";
import { moneyText, toLatinDigits } from "../src/i18n/decimal-text";
import { LOCALES } from "../src/i18n/locales";

const CODES = ["ar", "en", "ur", "hi"] as const;

describe("Display — الحدّ المفروض بالسلوك لا بالتعليق", () => {
  const i18n = createI18n();
  i18n.use("ar");

  it("لا يصير نصّاً بأي طريق", () => {
    const d = i18n.amount("1234.5");
    expect(() => `${d as unknown as string}`).toThrow(TypeError);
    expect(() => String(d)).toThrow(TypeError);
    expect(() => JSON.stringify(d)).toThrow(TypeError);
    expect(() => JSON.stringify({ v: d })).toThrow(TypeError);
    expect(() => (d as unknown as number) + 1).toThrow(TypeError);
    expect(() => d.toString()).toThrow(TypeError);
    expect(() => d.valueOf()).toThrow(TypeError);
    expect(() => d.localeCompare()).toThrow(TypeError);
    expect(() => [d].join(",")).toThrow(TypeError);
    expect(() => [d, d].sort()).toThrow(TypeError);
  });

  it("into(el) هو المصرف الوحيد، و machine هو المخرج الوحيد إلى السلك", () => {
    const d = i18n.amount("1234.5");
    const el = document.createElement("span");
    d.into(el);
    expect(el.textContent).toBe("1,234.50");
    expect(el.getAttribute("dir")).toBe("ltr");
    expect(d.machine).toBe("1234.50");
    expect(machine(d)).toBe("1234.50");
    /* machine دائماً ASCII مهما كانت لغة العرض. */
    i18n.use("hi");
    const hindi = i18n.amount("1234.5");
    const el2 = document.createElement("span");
    hindi.into(el2);
    expect(hindi.machine).toBe("1234.50");
    expect(/^[\x20-\x7E]*$/.test(hindi.machine)).toBe(true);
    expect(el2.textContent).not.toBe(hindi.machine);
    i18n.use("ar");
  });

  it("يرفض النصّ الذي يحمل محرف تحكّم غير مرئي — وهو ما تحقنه Intl", () => {
    /* المحرف مكتوب بالهروب الصريح: لا يجوز أن يحمل ملفّ مصدر محرفاً غير مرئي
       (يفرضه scripts/audit.mjs §٨)، والاختبار نفسه ليس استثناءً. */
    expect(() => new Display("1\u200F234", "1234", "amount")).toThrow(/تحكّم|control/);
    /* وهذا قياسٌ حيّ لما تُخرجه Intl تحت العربية: إن توقّفت عن حقنها يوماً
       سقط هذا التوقّع ولفت النظر — وهو المطلوب. */
    const intl = new Intl.NumberFormat("ar", { minimumFractionDigits: 2 }).format(-1250.5);
    expect(INVISIBLE_RE.test(intl)).toBe(true);
  });

  it("machine يرفض تمرير غلاف HTML", () => {
    const html = i18n.amountHtml("10");
    expect(() => machine(html)).toThrow(TypeError);
  });
});

describe("سياسة المفتاح الناقص", () => {
  let i18n = createI18n();
  beforeEach(() => {
    i18n = createI18n();
  });

  it("تسقط إلى العربية، وتُعلن ذلك أربع مرّات لا مرّة", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const seen: string[] = [];
    i18n.onMissing = (m) => seen.push(m.locale + "|" + m.key);
    i18n.define("xx", { ...i18n.meta("en"), lang: "xx", dir: "ltr", native: "XX", english: "XX", fallback: null }, {
      app: { name: "XX" },
    });
    i18n.use("xx");

    const value = i18n.t("acct.debitTotal");
    /* ١ · لا فراغ ولا مفتاح خام: النصّ العربي المصدر. */
    expect(value).toBe(i18n.tIn("ar", "acct.debitTotal"));
    expect(value).not.toBe("acct.debitTotal");
    expect(value.trim()).not.toBe("");
    /* ٢ · قائمة مسجَّلة. */
    expect(i18n.missing.map((m) => m.key)).toContain("acct.debitTotal");
    /* ٣ · تحذير مرّة واحدة لكل مفتاح، لا مرّة لكل استدعاء. */
    i18n.t("acct.debitTotal");
    i18n.t("acct.debitTotal");
    expect(warn.mock.calls.filter((c) => String(c[0]).includes("acct.debitTotal"))).toHaveLength(1);
    /* ٤ · مُخطِر يشبكه الفحص فيخرج بغير صفر. */
    expect(seen).toContain("xx|acct.debitTotal");
    warn.mockRestore();
  });

  it("المفتاح المفقود من كل اللغات يعود باسمه لا بفراغ", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    i18n.use("ar");
    expect(i18n.t("screen.nothing.at.all")).toBe("screen.nothing.at.all");
    warn.mockRestore();
  });

  it("في الوضع الصارم يرمي — وهو وضع الاختبارات", () => {
    i18n.use("ar");
    i18n.strict = true;
    expect(() => i18n.t("screen.nothing.at.all")).toThrow(/غير معرَّف|undefined key/);
  });
});

describe("الجمع — Intl.PluralRules لا شرطاً على الواحد", () => {
  const i18n = createI18n();

  it("العربية ستّ فئات وتستعملها كلها", () => {
    expect(i18n.pluralCategories("ar")).toEqual(["zero", "one", "two", "few", "many", "other"]);
    i18n.use("ar");
    const forms = [0, 1, 2, 3, 11, 100].map((n) => i18n.tPlural("common.count.accounts", n));
    expect(new Set(forms).size).toBe(6);
    expect(forms[0]).toBe("لا حسابات");
    expect(forms[1]).toBe("حساب واحد");
    expect(forms[2]).toBe("حسابان");
  });

  it("الهندية تضع الصفر في فئة one — فالشرط على الواحد خطأ هنا", () => {
    expect(new Intl.PluralRules("hi").select(0)).toBe("one");
    i18n.use("hi");
    const zero = i18n.tPlural("common.count.accounts", 0);
    const one = i18n.tPlural("common.count.accounts", 1);
    /* لولا صيغة "=0" لقالت الهندية «١ خاता» عن الصفر. */
    expect(zero).not.toBe(one);
  });

  it("كل لغة تُنتج صيغة لكل فئة تعرفها — بلا صيغة ميتة", () => {
    for (const code of CODES) {
      const cats = i18n.pluralCategories(code);
      expect(cats.length).toBeGreaterThan(0);
      i18n.use(code);
      for (const n of [0, 1, 2, 3, 11, 100]) {
        const text = i18n.tPlural("common.count.accounts", n);
        expect(text, code + "/" + n).not.toBe("common.count.accounts");
        expect(text.trim(), code + "/" + n).not.toBe("");
      }
    }
    i18n.use("ar");
  });
});

describe("الأرقام — من ملفّ اللغة لا من Intl", () => {
  const i18n = createI18n();

  it("يطبّع العربية-الهندية والفارسية والديفاناغرية عند الحدّ", () => {
    expect(toLatinDigits("١٢٣٤٥")).toBe("12345");
    expect(toLatinDigits("۱۲۳۴۵")).toBe("12345");
    /* ⚠ الديفاناغري كان ناقصاً في النموذج المعتمد — لصقٌ من لوحة هندية. */
    expect(toLatinDigits("१२३४५")).toBe("12345");
    expect(moneyText("१०००.५")).toBe("1,000.50");
    expect(moneyText("١٬٠٠٠٫٥")).toBe("1,000.50");
  });

  it("الفواصل وأشكال الأرقام تتبع اللغة، والقيمة الآلية لا تتبعها", () => {
    const shapes: Record<string, string> = {};
    for (const code of CODES) {
      i18n.use(code);
      const d = i18n.amount("1234567.891");
      const el = document.createElement("span");
      d.into(el);
      shapes[code] = el.textContent ?? "";
      expect(d.machine).toBe("1234567.89");
    }
    /* أربع لغات، وأربع نتائج عرض — ولا فرق واحد في القيمة الآلية. */
    expect(Object.keys(shapes)).toHaveLength(4);
    expect(shapes.ar).toBe("1,234,567.89");
    i18n.use("ar");
  });

  it("التقريب نصفي بعيداً عن الصفر، ونصّي بلا عائم", () => {
    expect(moneyText("-3.005")).toBe("-3.01");
    expect(moneyText("2.675")).toBe("2.68");
    /* على العائم: 2.675 هي 2.67499999999999982236431605997495353221893310546875 */
    expect((2.675).toFixed(2)).toBe("2.67");
  });
});

describe("الفهرس والتغطية", () => {
  const i18n = createI18n();

  it("أربع لغات محمّلة، ولكلٍّ منها كل مفاتيح المصدر", () => {
    expect(i18n.loaded().sort()).toEqual(["ar", "en", "hi", "ur"]);
    const source = Object.keys(i18n.messages("ar"));
    expect(source.length).toBeGreaterThan(600);
    for (const code of CODES) {
      const keys = Object.keys(i18n.messages(code));
      const missing = source.filter((k) => !keys.includes(k));
      expect(missing, code + " ينقصه: " + missing.slice(0, 5).join(" · ")).toHaveLength(0);
    }
  });

  it("الفهرس يذكر اللغات الأربع باتجاهيها", () => {
    expect(LOCALES).toHaveLength(4);
    expect(i18n.catalogue.map((c) => c.code)).toEqual(["ar", "en", "ur", "hi"]);
    expect(i18n.catalogue.filter((c) => c.dir === "rtl").map((c) => c.code)).toEqual(["ar", "ur"]);
    expect(i18n.catalogue.filter((c) => c.dir === "ltr").map((c) => c.code)).toEqual(["en", "hi"]);
  });

  it("الطلب الصريح ?lang= يسبق المحفوظ", () => {
    expect(i18n.preferred("?lang=hi", "ar", ["en"])).toBe("hi");
    expect(i18n.preferred("", "ur", ["en"])).toBe("ur");
    expect(i18n.preferred("", null, ["en-GB"])).toBe("en");
    expect(i18n.preferred("", null, ["fr"])).toBe("ar");
  });
});

describe("التاريخ — من ملفّ اللغة، و ISO يبقى ASCII", () => {
  const i18n = createI18n();

  it("يبني النصّ بأسماء اللغة ويُبقي القيمة الآلية ميلادية ASCII", () => {
    i18n.use("ar");
    const d = i18n.date("2026-08-24", "long");
    const el = document.createElement("span");
    d.into(el);
    expect(d.machine).toBe("2026-08-24");
    expect(el.textContent).toContain("أغسطس");
    i18n.use("hi");
    const hi = i18n.date("2026-08-24", "long");
    const el2 = document.createElement("span");
    hi.into(el2);
    expect(hi.machine).toBe("2026-08-24");
    expect(el2.textContent).not.toContain("أغسطس");
    i18n.use("ar");
  });

  it("الهجري عرضٌ محض بلا قيمة آلية", () => {
    i18n.use("ar");
    const h = i18n.hijri("2026-08-24");
    expect(h).not.toBeNull();
    expect(h?.machine).toBe("");
    expect(h?.kind).toBe("hijri-display-only");
  });
});
