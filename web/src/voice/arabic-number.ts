/* ═══════════════════════════════════════════════════════════════════════════
   قارئ الأعداد العربية المنطوقة — نظير src/Babel.Ai/Voice/ArabicSpokenNumber.cs
   Arabic spoken-number reader — the peer of the C# reader.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ التنفيذان يقرآن **ملف متجهات واحداً**:
       tests/Babel.Ai.Tests/golden/arabic-spoken-numbers.v1.json
   ولذلك لا يوجد «انحراف بين اللغتين» يُكتشف على شاشة صاحب المصلحة: يُكتشف في
   البناء، على الجانبين، بالمتجه نفسه.

   ولماذا يوجد هذا الملف في المتصفّح أصلاً: لأن الأثر المطلوب أن **يمتلئ الحقل
   والمستخدم ما زال يتكلّم**. ونداءُ خادم لكل نتيجة أوّلية يجعل الامتلاء متأخّراً
   عن الصوت، فيصير المشهد «أرسلتُ ثم انتظرتُ» لا «يكتب ما أقول».

   وما لا يفعله هذا الملف: لا يُنشئ حقيقة محاسبية. يملأ مسوّدة يؤكّدها إنسان
   (ADR-0024)، والخادم يعيد اشتقاق القيم من التفريغ النهائي وهو المرجع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { toLatinDigits } from "../i18n/decimal-text";

/** رمز عطل عربي — نفس رموز VoiceErrors في الخادم حرفياً. */
export type NumberFault =
  | "ai.voice.mixed_digit_systems"
  | "ai.voice.digits_and_words_mixed"
  | "ai.voice.unknown_number_word"
  | "ai.voice.number_not_composable";

/** نتيجة القراءة: قيمة نصّية (المال نصّ) أو عطل مُسمّى. */
export type NumberReading =
  | { readonly ok: true; readonly text: string }
  | { readonly ok: false; readonly code: NumberFault; readonly token: string };

const UNITS: Record<string, number> = {
  صفر: 0,
  واحد: 1, واحدة: 1, احد: 1, احدي: 1,
  اثنا: 2, اثني: 2, اثنان: 2, اثنين: 2, اثنتان: 2, اثنتين: 2, ثنتين: 2,
  ثلاثة: 3, ثلاث: 3, ثلاثه: 3,
  اربعة: 4, اربع: 4, اربعه: 4,
  خمسة: 5, خمس: 5, خمسه: 5,
  ستة: 6, ست: 6, سته: 6,
  سبعة: 7, سبع: 7, سبعه: 7,
  ثمانية: 8, ثمان: 8, ثماني: 8, ثمانيه: 8,
  تسعة: 9, تسع: 9, تسعه: 9,
  عشرة: 10, عشر: 10, عشره: 10,
};

const TENS: Record<string, number> = {
  عشرون: 20, عشرين: 20,
  ثلاثون: 30, ثلاثين: 30,
  اربعون: 40, اربعين: 40,
  خمسون: 50, خمسين: 50,
  ستون: 60, ستين: 60,
  سبعون: 70, سبعين: 70,
  ثمانون: 80, ثمانين: 80,
  تسعون: 90, تسعين: 90,
};

const HUNDREDS: Record<string, number> = {
  مئة: 100, مائة: 100, مية: 100,
  مئتان: 200, مئتين: 200, مائتان: 200, مائتين: 200, ميتين: 200,
  ثلاثمئة: 300, ثلاثمائة: 300,
  اربعمئة: 400, اربعمائة: 400,
  خمسمئة: 500, خمسمائة: 500,
  ستمئة: 600, ستمائة: 600,
  سبعمئة: 700, سبعمائة: 700,
  ثمانمئة: 800, ثمانمائة: 800, ثمنمئة: 800,
  تسعمئة: 900, تسعمائة: 900,
};

/* «ألفين» قيمة لا مضاعِف: من عامَلها مضاعِفاً قرأ «ألفين وخمسمئة» صحيحاً
   بالصدفة ثم قرأ «ألفين» وحدها صفراً. */
const SCALES: Record<string, number> = {
  الف: 1000, الاف: 1000,
  مليون: 1e6, ملايين: 1e6, مليونين: 1e6,
  مليار: 1e9, مليارات: 1e9,
};

const STANDALONE: Record<string, number> = { الفان: 2000, الفين: 2000 };

const FRACTIONS: Record<string, number> = {
  نص: 0.5, نصف: 0.5, النص: 0.5, النصف: 0.5,
  ربع: 0.25, الربع: 0.25,
  ثلث: 0.3333, الثلث: 0.3333,
};

const DECIMAL_MARKERS = new Set(["فاصلة", "فاصل", "فاصلا", "نقطة", "نقطه"]);

/* أنظمة الأرقام الأربعة التي تصل هذا المستودع فعلاً. */
const DIGIT_RANGES: ReadonlyArray<readonly [number, number]> = [
  [0x30, 0x39],
  [0x0660, 0x0669],
  [0x06f0, 0x06f9],
  [0x0966, 0x096f],
];

function digitSystem(ch: string): number {
  const c = ch.codePointAt(0) ?? -1;
  let index = 0;
  for (const range of DIGIT_RANGES) {
    if (c >= range[0] && c <= range[1]) return index;
    index++;
  }
  return -1;
}

/** يزيل التشكيل ويوحّد الهمزات والألف المقصورة — نظير Strip في الخادم. */
export function strip(word: string): string {
  let out = "";
  for (const ch of word) {
    const c = ch.codePointAt(0) ?? 0;
    if ((c >= 0x064b && c <= 0x0652) || c === 0x0640 || c === 0x0670) continue;
    if (ch === "أ" || ch === "إ" || ch === "آ" || ch === "ٱ") out += "ا";
    else if (ch === "ى") out += "ي";
    else out += ch;
  }
  return out;
}

/**
 * يطبّع كلمة إلى أرقام لاتينية، **ويرفض خلط نظامَي أرقام داخلها**.
 * @param token الكلمة.
 */
export function normaliseToken(token: string): NumberReading {
  let seen = -1;
  let out = "";
  for (const ch of token) {
    if (ch === "٬" || ch === "," || ch === " " || ch === " " || ch === " " || ch === "_") continue;
    if (ch === "٫") {
      out += ".";
      continue;
    }
    const system = digitSystem(ch);
    if (system < 0) {
      out += ch;
      continue;
    }
    if (seen >= 0 && seen !== system) {
      return { ok: false, code: "ai.voice.mixed_digit_systems", token };
    }
    seen = system;
    out += toLatinDigits(ch);
  }
  return { ok: true, text: out };
}

function valued(word: string): boolean {
  return (
    word in UNITS || word in TENS || word in HUNDREDS || word in SCALES ||
    word in STANDALONE || word in FRACTIONS
  );
}

function split(text: string): string[] {
  const words: string[] = [];
  for (const raw of text.split(/[\s،,]+/)) {
    let word = strip(raw);
    if (!word || word === "و") continue;
    if (
      !valued(word) && !DECIMAL_MARKERS.has(word) && word.length > 1 && word[0] === "و" &&
      (valued(word.slice(1)) || DECIMAL_MARKERS.has(word.slice(1)))
    ) {
      word = word.slice(1);
    }
    words.push(word);
  }
  return words;
}

/* حسابٌ نصّي على منزلتين: لا عائمة في المال. المُراكِمات هنا أعداد صحيحة من
   الهللات، فلا تُنتج 0.1+0.2 ما ينتجه في العائمة. */
const SCALE = 10000;

function compose(words: string[], phrase: string): NumberReading {
  let total = 0;
  let current = 0;
  let fraction = 0;
  let anything = false;

  for (const word of words) {
    const fractionValue = FRACTIONS[word];
    const standaloneValue = STANDALONE[word];
    const scaleValue = SCALES[word];
    const hundredValue = HUNDREDS[word];
    const tenValue = TENS[word];
    const unitValue = UNITS[word];

    if (fractionValue !== undefined) {
      fraction += Math.round(fractionValue * SCALE);
      anything = true;
    } else if (standaloneValue !== undefined) {
      total += current + standaloneValue * SCALE;
      current = 0;
      anything = true;
    } else if (scaleValue !== undefined) {
      total += (current === 0 ? SCALE : current) * scaleValue;
      current = 0;
      anything = true;
    } else if (hundredValue !== undefined) {
      current = hundredValue === 100 && current > 0 ? current * 100 : current + hundredValue * SCALE;
      anything = true;
    } else if (tenValue !== undefined) {
      current += tenValue * SCALE;
      anything = true;
    } else if (unitValue !== undefined) {
      current += unitValue * SCALE;
      anything = true;
    } else {
      return { ok: false, code: "ai.voice.unknown_number_word", token: word };
    }
  }

  if (!anything) return { ok: false, code: "ai.voice.number_not_composable", token: phrase };
  return { ok: true, text: fixed(total + current + fraction) };
}

function fixed(units: number): string {
  const negative = units < 0;
  const abs = Math.abs(Math.round(units));
  const whole = Math.floor(abs / SCALE);
  const frac = String(abs % SCALE).padStart(4, "0").replace(/0+$/, "");
  return (negative ? "-" : "") + whole + (frac ? "." + frac : "");
}

function composeFraction(words: string[], phrase: string): NumberReading {
  if (words.length === 0) return { ok: false, code: "ai.voice.number_not_composable", token: phrase };

  /* ما بعد «فاصلة» يُقرأ سلسلة أرقام لا عدداً: «فاصلة صفر خمسة» = 0.05،
     وقراءتها عدداً تعطي 0.5 — أي عُشر القيمة. */
  if (words.every((w) => (UNITS[w] ?? 10) <= 9)) {
    return { ok: true, text: "0." + words.map((w) => String(UNITS[w] ?? 0)).join("") };
  }
  const composed = compose(words, phrase);
  if (!composed.ok) return composed;
  return { ok: true, text: "0." + composed.text.replace(/\..*$/, "") };
}

/**
 * يقرأ عبارةً عدداً واحداً. أرقام وحدها أو كلمات وحدها — **ولا خلط بينهما**.
 * @param phrase العبارة كما نُطقت.
 */
export function readArabicNumber(phrase: string): NumberReading {
  const trimmed = (phrase ?? "").trim();
  if (!trimmed) return { ok: false, code: "ai.voice.number_not_composable", token: phrase ?? "" };

  const parts: string[] = [];
  for (const token of trimmed.split(" ")) {
    const normalised = normaliseToken(token);
    if (!normalised.ok) return normalised;
    parts.push(normalised.text);
  }

  const words = split(parts.join(" "));
  if (words.length === 0) return { ok: false, code: "ai.voice.number_not_composable", token: phrase };

  const anyDigits = words.some((w) => /[0-9]/.test(w));
  const anyWords = words.some(valued);
  if (anyDigits && anyWords) {
    return { ok: false, code: "ai.voice.digits_and_words_mixed", token: phrase };
  }

  if (anyDigits) {
    const joined = words.join("");
    if (!/^[+-]?\d+(\.\d+)?$/.test(joined)) {
      return { ok: false, code: "ai.voice.number_not_composable", token: phrase };
    }
    return { ok: true, text: joined.replace(/^\+/, "") };
  }

  const marker = words.findIndex((w) => DECIMAL_MARKERS.has(w));
  if (marker < 0) return compose(words, phrase);

  const whole = compose(words.slice(0, marker), phrase);
  if (!whole.ok) return whole;
  const frac = composeFraction(words.slice(marker + 1), phrase);
  if (!frac.ok) return frac;

  const wholeUnits = Math.round(Number(whole.text) * SCALE);
  const fracUnits = Math.round(Number(frac.text) * SCALE);
  return { ok: true, text: fixed(wholeUnits + fracUnits) };
}

/** هل تُقرأ هذه العبارة عدداً؟ سؤال بلا رمي. */
export function canReadArabicNumber(phrase: string): boolean {
  return readArabicNumber(phrase).ok;
}
