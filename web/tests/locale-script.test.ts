/* ═══════════════════════════════════════════════════════════════════════════
   خطّ اللغة والترميز — الحارس مُثبَتٌ بالطفرة، وثقبُه مُثبَتٌ بالسلوك
   Script and encoding — the guard proved by mutation, its hole proved by behaviour
   ───────────────────────────────────────────────────────────────────────────
   ‏العطل الذي يوجب هذا الملفّ: الفحص ١ في `scripts/audit.mjs` يقول «كل مفتاح
   موجود في اللغات الأربع»، ويُقرأ «اللغات الأربع مترجَمة». وقد وقعت قيمةُ رفضٍ
   في الهندية والأردية رُمِّزت UTF-8 وقُرئت Latin-1 — 444 من 488 محرفاً هنديّاً
   في كتلة لاتينية، والمسحُ أخضر، والقيمة تصل قارئاً هنديّاً حقيقياً.

   ‏ولا يُكتب في هذا الملفّ نصٌّ مشوَّهٌ منسوخ: التشويه يُولَّد بـ`mangle` — وهي
   آليّةُ العطل نفسها — من **نصوص ملفّات اللغة الحيّة**. فلو تغيّر النصّ غداً
   تغيّرت الطفرة معه، ولا يبقى ثابتٌ مشوَّه يصدّق كاشفاً عمي.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readdirSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  census,
  foreignRuns,
  hasOwnScript,
  isDiagnostic,
  junkChars,
  mangle,
  mangleUnder,
  mojibakeRuns,
  proseWords,
  prose,
  scriptOf,
  valueTexts,
  witnessesOf,
} from "../scripts/locale-script.mjs";
import { LOCALES } from "../src/i18n/locales";

/* الأنواع مكتوبةٌ هنا لا مُستنتَجة من ملفّ .mjs: `checkJs` مطفأة. */
const scriptFor = scriptOf as (code: string) => string;
const diagnostic = isDiagnostic as (script: string) => boolean;
const witnesses = witnessesOf as (code: string, codes: readonly string[]) => string[];
const mangled = mangle as (text: string) => string;
const runs = mojibakeRuns as (text: string) => { run: string; decoded: string }[];
const ownScript = hasOwnScript as (text: string, code: string) => boolean;
const letters = census as (text: string, code: string) => { letters: number; inScript: number; foreign: number };
const words = proseWords as (text: string) => number;
const stripped = prose as (text: string) => string;
const textsOf = valueTexts as (value: unknown) => string[];
const foreign = foreignRuns as (text: string, code: string) => string[];
const junk = junkChars as (text: string) => { ch: string; at: number; code: number }[];
const mangledAs = mangleUnder as (label: string, text: string) => string;

const CODES = LOCALES.map((l) => l.code);
const SOURCE = "ar";

/** كل نصوص لغةٍ ما، مسطَّحةً بمفاتيحها. */
function flatTexts(code: string): { key: string; text: string }[] {
  const bundle = LOCALES.find((l) => l.code === code);
  if (!bundle) throw new Error("لا لغة بالرمز " + code);
  const out: { key: string; text: string }[] = [];
  const walk = (node: unknown, prefix: string): void => {
    if (node === null || typeof node !== "object" || Array.isArray(node)) {
      for (const t of textsOf(node)) out.push({ key: prefix, text: t });
      return;
    }
    const entries = Object.entries(node as Record<string, unknown>);
    if (entries.some(([k]) => k === "other")) {
      for (const t of textsOf(node)) out.push({ key: prefix, text: t });
      return;
    }
    for (const [k, v] of entries) walk(v, prefix ? prefix + "." + k : k);
  };
  walk(bundle.messages, "");
  return out;
}

const ALL: Record<string, { key: string; text: string }[]> = Object.fromEntries(
  CODES.map((c) => [c, flatTexts(c)])
);

/** أوّل نصٍّ نثريّ معتبر في لغةٍ ما — عيّنةٌ حيّة لا ثابتٌ مكتوب. */
function probe(code: string): string {
  const found = ALL[code]?.find((v) => ownScript(v.text, code) && letters(v.text, code).letters >= 12);
  if (!found) throw new Error("لا نصّ نثريّ في " + code);
  return found.text;
}

describe("الخطّ يُشتقّ من رمز اللغة، لا من جدول", () => {
  it("‏Intl يعطي خطّ كل لغة، ولغةٌ خامسة تعمل بلا تعديل", () => {
    expect(scriptFor("ar")).toBe("Arab");
    expect(scriptFor("ur")).toBe("Arab");
    expect(scriptFor("hi")).toBe("Deva");
    expect(scriptFor("en")).toBe("Latn");
    /* لغةٌ ليست في المستودع: القاعدة تجيب عنها بلا أن يُكتب لها سطر. */
    expect(scriptFor("ru")).toBe("Cyrl");
    expect(scriptFor("el")).toBe("Grek");
  });

  it("خطّ المعرّفات هو الخطّ الذي فيه A، فلا يشهد بنثر", () => {
    expect(diagnostic("Latn")).toBe(false);
    expect(diagnostic("Arab")).toBe(true);
    expect(diagnostic("Deva")).toBe(true);
    expect(diagnostic("Cyrl")).toBe(true);
  });

  it("لكل لغةٍ شاهدان على الأقلّ، والإنجليزية مستبعَدة من الشهادة", () => {
    for (const code of CODES) {
      const w = witnesses(code, CODES);
      expect(w.length).toBeGreaterThanOrEqual(2);
      expect(w).not.toContain("en");
      expect(w).not.toContain(code);
    }
    /* ولا يُشترط اختلاف الخطّ: العربية تشهد للأردية وإن اشتركا في الخطّ —
       وبدونها يبقى للأردية شاهدٌ واحد يسقط معها في إنزالٍ واحد. */
    expect(witnesses("ur", CODES)).toContain("ar");
  });
});

describe("التشويه يُكشف بفكّ الترميز، لا بمعرفة أشكال المحارف", () => {
  it("يلتقط تشويه كل لغةٍ خطُّها دليل، ويفكّه إلى أصله بالضبط", () => {
    let proved = 0;
    for (const code of CODES) {
      if (!diagnostic(scriptFor(code))) continue;
      const original = probe(code);
      const broken = mangled(original);
      expect(broken).not.toBe(original);
      const hits = runs(broken);
      expect(hits.length).toBeGreaterThan(0);
      expect(hits.map((h) => h.decoded).join("")).toContain(original.slice(0, 12));
      proved++;
    }
    expect(proved).toBe(3);
  });

  it("يلتقط الجزء المشوَّه من قيمةٍ سليمة بقيّتها — لا يشترط أن تكون كلُّها فاسدة", () => {
    const hindi = probe("hi");
    const half = hindi.slice(0, 10) + mangled(hindi.slice(10));
    expect(ownScript(half, "hi")).toBe(true); /* القاعدة (ب) لا تراها */
    expect(runs(half).length).toBeGreaterThan(0); /* والقاعدة (أ) تراها */
  });

  it("ولا يُنذَر على نصّ سليم: ASCII، ولا على · و« »", () => {
    expect(runs("Journal Voucher").length).toBe(0);
    expect(runs("balance · net — 1,250.00").length).toBe(0);
    expect(runs("«unit» · PDF · SAR").length).toBe(0);
    expect(runs(probe("ar")).length).toBe(0);
    expect(runs(probe("hi")).length).toBe(0);
  });

  it("‏**الحصيلة على الملفّات الحيّة صفر** — وليست صفراً لأن المسح فارغ", () => {
    let scanned = 0;
    const found: string[] = [];
    for (const code of CODES) {
      for (const { key, text } of ALL[code] ?? []) {
        scanned++;
        for (const hit of runs(text)) found.push(code + " ← " + key + " : " + hit.decoded.slice(0, 40));
      }
    }
    expect(scanned).toBeGreaterThan(4000);
    expect(found).toEqual([]);
  });
});

describe("كل قيمة نثرٍ بخطّ لغتها — بشهادة لغةٍ أخرى", () => {
  it("‏الحصيلة على الملفّات الحيّة صفر، والمقارنات ليست ضامرة", () => {
    let compared = 0;
    const wrong: string[] = [];
    for (const code of CODES) {
      const w = witnesses(code, CODES);
      const byKey = new Map(ALL[code]?.map((v) => [v.key, v.text]) ?? []);
      for (const [key, text] of byKey) {
        const witnessed = w.some((other) =>
          (ALL[other] ?? []).some((v) => v.key === key && ownScript(v.text, other))
        );
        if (!witnessed || letters(text, code).letters === 0) continue;
        compared++;
        if (!ownScript(text, code)) wrong.push(code + " ← " + key + " : " + text.slice(0, 40));
      }
    }
    expect(compared).toBeGreaterThan(1500);
    expect(wrong).toEqual([]);
  });

  it("‏والقاعدة تحمرّ على المفتاح الذي انكسر فعلاً — طفرةً على قيمته الحيّة", () => {
    const key = "screen.voice.refusal.nameNotBounded";
    for (const code of ["hi", "ur"]) {
      const value = ALL[code]?.find((v) => v.key === key)?.text;
      expect(value, key + " غير موجود في " + code).toBeTruthy();
      expect(ownScript(value as string, code)).toBe(true);
      const broken = mangled(value as string);
      expect(ownScript(broken, code)).toBe(false);
      expect(runs(broken).length).toBeGreaterThan(0);
    }
  });

  it("‏الرمز الآلي داخل النثر لا يُحمِّر: النصّ يبقى بخطّ لغته", () => {
    for (const code of CODES) {
      if (!diagnostic(scriptFor(code))) continue;
      const withToken = probe(code) + " BANK-0001 PDF {currency}";
      expect(ownScript(withToken, code)).toBe(true);
      expect(letters(withToken, code).foreign).toBeGreaterThan(0);
    }
  });

  it("المعاملات والوسوم تُنزَع قبل القياس، فلا تُحسَب حروفاً أجنبية", () => {
    /* يُنزَع المعامل والوسم والكيان، ويبقى النثر الذي بينها — وهو المقصود. */
    expect(stripped("{count} — <span dir=\"ltr\">x</span> &nbsp;").replace(/\s+/gu, " ").trim()).toBe("— x");
    expect(stripped("{count} {amount} <b></b>").replace(/\s+/gu, " ").trim()).toBe("");
    expect(words("{count} كلمتان")).toBe(1);
  });
});

describe("النسخ عن المصدر ليس ترجمة — والثقب الباقي مُعلَن ومُقاس", () => {
  const arabicByKey = new Map((ALL[SOURCE] ?? []).map((v) => [v.key, v.text]));

  it("لا عبارة (ثلاث كلمات فأكثر) منسوخة عن العربية في اللغات الثلاث", () => {
    const copied: string[] = [];
    for (const code of CODES) {
      if (code === SOURCE) continue;
      for (const { key, text } of ALL[code] ?? []) {
        const src = arabicByKey.get(key);
        if (src === undefined || src !== text) continue;
        if (!ownScript(src, SOURCE) || words(src) < 3) continue;
        copied.push(code + " ← " + key);
      }
    }
    expect(copied).toEqual([]);
  });

  it("‏**الثقب**: شرط الثلاث كلمات ليس ذوقاً — تضييقه إلى واحدة يُنتج ستّ حمراوات كاذبة", () => {
    const identical: string[] = [];
    for (const { key, text } of ALL["ur"] ?? []) {
      const src = arabicByKey.get(key);
      if (src === undefined || src !== text || !ownScript(src, SOURCE)) continue;
      identical.push(key);
    }
    /* ستّ قيم أردية تطابق العربية حرفاً بحرف، وكلُّها مصطلحٌ واحد أو اسمُ عَلَم:
       فالتضييق إلى كلمةٍ واحدة يرفض ترجماتٍ صحيحة. العدد مقيسٌ لا مفترَض. */
    expect(identical.length).toBe(6);
    for (const key of identical) {
      expect(words(arabicByKey.get(key) as string)).toBeLessThan(3);
    }
  });

  it("‏**الثقب، سلوكاً لا تعليقاً**: عربيةٌ في الأردية تمرّ إن قصُرت أو أُعيدت صياغتها", () => {
    const arabicSentence = probe(SOURCE);
    /* (ب) عاجزة بنيوياً: خطّ الأردية هو خطّ العربية. */
    expect(scriptFor("ur")).toBe(scriptFor(SOURCE));
    expect(ownScript(arabicSentence, "ur")).toBe(true);
    /* (ج) تلتقط النسخ الحرفي الطويل وحده. */
    expect(words(arabicSentence)).toBeGreaterThanOrEqual(3);
    /* وما يهزم الفحص كلَّه، مكتوباً صريحاً: قِصَرٌ، أو إعادةُ صياغة. */
    const shortCopy = arabicSentence.split(/\s+/u).slice(0, 2).join(" ");
    expect(words(shortCopy)).toBeLessThan(3);
    expect(ownScript(shortCopy, "ur")).toBe(true);
    expect(runs(shortCopy).length).toBe(0);
    const reworded = arabicSentence + " ";
    expect(reworded).not.toBe(arabicSentence);
    expect(ownScript(reworded, "ur")).toBe(true);
    expect(runs(reworded).length).toBe(0);
  });
});

/* ═══════════════════════════════════════════════════════════════════════════
   الهجوم على الحارس نفسه — كل صنفٍ هنا هُزم به قياسٌ سابق، وقُيس علاجُه
   Attacks on the guard: each class below defeated an earlier rule, measured.
   ═══════════════════════════════════════════════════════════════════════════ */
describe("ما يهزم فكَّ الترميز لا يهزم الإذن المغلق", () => {
  const hindi = ALL["hi"]?.find((v) => v.key === "screen.voice.refusal.nameNotBounded")?.text as string;

  it("‏تشويهٌ بترميزٍ يرفع بايتاتٍ فوق U+00FF **يفلت** من القاعدة (أ) — مقيس، لا مفترَض", () => {
    for (const label of ["koi8-r", "macintosh", "iso-8859-7", "windows-1251"]) {
      const broken = mangledAs(label, hindi);
      expect(broken).not.toBe(hindi);
      expect(runs(broken).length, label + " كان يجب أن يفلت من فكّ الترميز").toBe(0);
      /* ويسقط في الإذن المغلق: مقاطع أجنبية غير آلية لا تصدّقها لغةٌ أخرى. */
      expect(foreign(broken, "hi").length).toBeGreaterThan(0);
    }
  });

  it("‏والتشويه **الجزئي** بأيٍّ من الترميزين يفلت من القاعدة (ب) ويسقط في (د)", () => {
    for (const label of ["koi8-r", "windows-1252"]) {
      const half = hindi.slice(0, 60) + mangledAs(label, hindi.slice(60));
      expect(ownScript(half, "hi")).toBe(true); /* (ب) عمياء: بقيت ديفاناغارية */
      expect(foreign(half, "hi").length).toBeGreaterThan(0);
    }
  });

  it("‏وبايتات UTF-16 تُنتج محارف تحكّم لا حروفاً، فتفلت من (أ) و(د) وتسقط في (هـ)", () => {
    const bytes = [...hindi].map((c) => String.fromCharCode(c.charCodeAt(0) & 0xff, c.charCodeAt(0) >> 8)).join("");
    expect(runs(bytes).length).toBe(0);
    expect(foreign(bytes, "hi").length).toBe(0);
    expect(junk(bytes).length).toBeGreaterThan(0);
    const half = hindi.slice(0, 60) + bytes.slice(120);
    expect(ownScript(half, "hi")).toBe(true);
    expect(junk(half).length).toBeGreaterThan(0);
  });

  it("‏الإذن مغلق: الرمز الآلي ASCII يمرّ، والمقطع الأجنبي يمرّ **إن صدّقته لغةٌ أخرى** فقط", () => {
    expect(foreign("PDF SAR BANK-0001 INV-2026-0587 " + hindi, "hi")).toEqual([]);
    /* المقاطع الحيّة الأجنبية غير الآلية قليلة، وكلُّها مصدَّقة — والعدد مقيس. */
    let seen = 0;
    const unlicensed: string[] = [];
    for (const code of CODES) {
      for (const { key, text } of ALL[code] ?? []) {
        for (const run of foreign(text, code)) {
          seen++;
          const elsewhere = CODES.some(
            (o) => o !== code && (ALL[o] ?? []).some((v) => v.key === key && v.text.includes(run))
          );
          if (!elsewhere) unlicensed.push(code + " ← " + key + " «" + run + "»");
        }
      }
    }
    expect(seen).toBe(6);
    expect(unlicensed).toEqual([]);
  });

  it("‏ولا محرف حشوٍ في أي قيمة حيّة — والمسح ليس فارغاً", () => {
    let scanned = 0;
    const found: string[] = [];
    for (const code of CODES) {
      for (const { key, text } of ALL[code] ?? []) {
        scanned++;
        if (junk(text).length) found.push(code + " ← " + key);
      }
    }
    expect(scanned).toBeGreaterThan(4000);
    expect(found).toEqual([]);
  });
});

describe("نطاق الفحص هو المجلّد — لا قائمةٌ في الحارس", () => {
  it("اللغات الأربع المُسجَّلة هي بعينها ملفّات المجلّد", () => {
    /* ‏نفس ما يقيسه `scripts/audit.mjs` §١٠: المجلّد هو المجموعة المغلقة،
       وملفُّ لغةٍ خامسة لا يُسجَّل يخرج من الفحوص كلِّها بصمت. */
    const names = readdirSync(path.resolve(process.cwd(), "src/i18n/locales"));
    const onDisk = names
      .filter((n) => n.endsWith(".base.ts"))
      .map((n) => n.slice(0, -".base.ts".length))
      .filter((code) => names.includes(code + ".web.ts"));
    expect(onDisk.length).toBeGreaterThanOrEqual(4);
    expect([...onDisk].sort()).toEqual([...CODES].sort());
  });
});
