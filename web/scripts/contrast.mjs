#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   سلاسل بابل — مقياس التباين  ·  The contrast meter
   ───────────────────────────────────────────────────────────────────────────
       node scripts/contrast.mjs           جدولٌ كامل، ويخرج بالرمز 1 إن سقط زوج
       node scripts/contrast.mjs --quiet   السواقط وحدها
       node scripts/contrast.mjs --json    صفٌّ لكل زوج، للأدوات لا للعين

   **لماذا أداةٌ لا ملاحظة في مراجعة:** التباين رقمٌ يُقاس لا رأيٌ يُبدى، وستّة
   أرقامٍ مكتوبةً في تعليق تشيخ عند أول تعديل لون. هذا الملفّ يقرأ ملفّات
   السمة نفسها التي يقرؤها المتصفّح، ويحلّ `var()` و`color-mix()` كما يحلّهما،
   ويركّب الأسطح الشفّافة طبقةً فوق طبقة، ثم يقيس. فإن هبط رمزٌ دون العتبة
   سقطت البوّابة — والحدّ الأدنى للتباين **مُنفَّذ لا موصى به**.

   ┌─ ما يقيسه ولا يقيسه ───────────────────────────────────────────────────┐
   │ يقيس: كل زوجٍ (حبر · خلفية) **مأخوذٍ من قاعدة CSS حقيقية** — مذكورٌ     │
   │       ملفّها وسطرها في حقل `where` لكل زوج، فلا زوجَ مخترَع ولا زوجَ     │
   │       منسيّ يمرّ بصمت.                                                   │
   │ لا يقيس: النصّ فوق صورة، ولا ما يرسمه المتصفّح من تمويهٍ خلفي            │
   │       (`backdrop-filter`) — والتمويه **يقرّب** الخلفية من لونها المتوسّط  │
   │       ولا يُبعدها، فقياسنا على الطرفين هو الحالة الأسوأ لا أفضلها.       │
   └────────────────────────────────────────────────────────────────────────┘

   العتبات (WCAG 2.1):
     4.5:1  نصٌّ عادي (AA 1.4.3)
     3.0:1  نصٌّ كبير — 18.66px عريض أو 24px (AA 1.4.3)
     3.0:1  حدودُ المكوّنات ومؤشّراتها غير النصّية (AA 1.4.11)
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import path from "node:path";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const STYLES = path.resolve(HERE, "..", "src", "styles");

/* ملفّات اللوحة بترتيب تتالِيها في الصفحة: السمة أولاً، ثم الطبقة الدلالية،
   ثم الطبقة السينمائية التي توسّعها. وهو نفس ترتيب `@import` في tokens.css. */
export const THEME_FILES = ["theme/theme-default.css", "tokens.css", "cinematic.css"];

/* اللوحتان اللتان يبدّل بينهما المستخدم في الشريط العلوي. الثانية ملفٌّ يُربَط
   **بعد** الأول فيفوز بترتيب المصدر — وهي قصّة «سمة عميل بملفّ واحد» نفسها.
   وكلتاهما تُقاسان: لوحةٌ ثانية غير مقيسة تصير باباً خلفياً إلى ما دون AA. */
export const PALETTES = {
  default: THEME_FILES,
  high: [...THEME_FILES, "theme/theme-accessible.css"],
};

/* ملفّات **المكوّنات** — لا تُعرَّف فيها رموز، ومنها يُشتقّ حارس التغطية أدناه.
   وهي كل CSS في `src/styles/` عدا ملفّات الرموز والطباعة: الطباعة لوحةٌ
   مستقلّة تُقاس بأزواجها الأربعة صراحةً. */
export const COMPONENT_FILES = [
  "app.css",
  "components.css",
  "primitives.css",
  "shell.css",
  "presence.css",
  "motion.css",
];

/** كل ما يقرؤه هذا المقياس فعلاً — يُطبَع في التقرير لتكون التغطية مُدقَّقة لا مفترضة. */
export const SOURCES = [...THEME_FILES, "theme/theme-accessible.css", ...COMPONENT_FILES];

/* ═══════════════════════════════════════════════ ١ · قراءة CSS إلى قواعد */

function stripComments(css) {
  return css.replace(/\/\*[\s\S]*?\*\//g, "");
}

/** يمشي على CSS ويُخرج قواعده مسطّحةً، ومع كل قاعدة شروط @media التي تحتها. */
function walk(css, conditions, out) {
  let i = 0;
  let start = 0;
  while (i < css.length) {
    const ch = css[i];
    if (ch === "{") {
      const prelude = css.slice(start, i).trim().replace(/\s+/g, " ");
      let depth = 1;
      let j = i + 1;
      while (j < css.length && depth > 0) {
        if (css[j] === "{") depth++;
        else if (css[j] === "}") depth--;
        j++;
      }
      const body = css.slice(i + 1, j - 1);
      if (prelude.startsWith("@")) {
        /* @keyframes لا يحمل رموزاً — ولو حملها فهي ليست لوناً على سطح. */
        if (/^@(media|supports|layer)\b/i.test(prelude)) {
          walk(body, conditions.concat(prelude), out);
        }
      } else {
        out.push({ conditions, selector: prelude, body });
      }
      i = j;
      start = j;
    } else if (ch === ";") {
      i++;
      start = i;
    } else {
      i++;
    }
  }
  return out;
}

/** يفكّ جسم قاعدة إلى خريطة «اسم الخاصية ← قيمتها»، للخصائص المخصّصة وحدها. */
function customProps(body) {
  const map = new Map();
  for (const chunk of body.split(";")) {
    const at = chunk.indexOf(":");
    if (at < 0) continue;
    const name = chunk.slice(0, at).trim();
    if (!name.startsWith("--")) continue;
    map.set(name, chunk.slice(at + 1).trim());
  }
  return map;
}

const isDarkMedia = (conditions) =>
  conditions.some((c) => /prefers-color-scheme\s*:\s*dark/i.test(c));

const selectorList = (selector) => selector.split(",").map((s) => s.trim());

/**
 * يبني خرائط الرموز للسمتين — وللداكن نسختان: الاختيار الصريح، وتفضيل النظام.
 * وقاعدة السمات في هذا المستودع تقول إنهما **يجب** أن تتطابقا، ولذلك تُبنيان
 * منفصلتين ويقارنهما حارس (`themeParityProblems`) بدل أن يُفترض التطابق.
 */
export function readThemes(files = THEME_FILES, dir = STYLES) {
  const rules = [];
  for (const file of files) {
    walk(stripComments(readFileSync(path.join(dir, file), "utf8")), [], rules);
  }
  const light = new Map();
  const darkExplicit = new Map();
  const darkSystem = new Map();
  for (const rule of rules) {
    const list = selectorList(rule.selector);
    const props = customProps(rule.body);
    if (props.size === 0) continue;
    if (!isDarkMedia(rule.conditions) && list.includes(":root")) {
      for (const [k, v] of props) light.set(k, v);
    } else if (!isDarkMedia(rule.conditions) && list.includes(':root[data-theme="dark"]')) {
      for (const [k, v] of props) darkExplicit.set(k, v);
    } else if (isDarkMedia(rule.conditions) && list.includes(':root:not([data-theme="light"])')) {
      for (const [k, v] of props) darkSystem.set(k, v);
    }
  }
  const dark = new Map(light);
  for (const [k, v] of darkExplicit) dark.set(k, v);
  const darkBySystem = new Map(light);
  for (const [k, v] of darkSystem) darkBySystem.set(k, v);
  return { light, dark, darkBySystem, darkExplicit, darkSystem };
}

/**
 * حارس قاعدة السمات: الاختيار الصريح `[data-theme="dark"]` وتفضيل النظام
 * `@media` يجب أن يعرّفا **نفس** الرموز بنفس القيم. اختلافُ سطرٍ واحد يعني
 * مستخدمين اثنين يريان لونين ولا يعرف أحدٌ لماذا.
 */
export function themeParityProblems(themes = readThemes()) {
  const problems = [];
  const names = new Set([...themes.darkExplicit.keys(), ...themes.darkSystem.keys()]);
  for (const name of [...names].sort()) {
    const a = themes.darkExplicit.get(name);
    const b = themes.darkSystem.get(name);
    if (a === undefined) problems.push(`${name}: مُعرَّف في @media ولا يُعرَّف في [data-theme="dark"]`);
    else if (b === undefined) problems.push(`${name}: مُعرَّف في [data-theme="dark"] ولا يُعرَّف في @media`);
    else if (a !== b) problems.push(`${name}: "${a}" في الاختيار الصريح · "${b}" في تفضيل النظام`);
  }
  return problems;
}

/* ═════════════════════════════════════════════ ٢ · حلّ اللون كما يحلّه المتصفّح */

const NAMED = { white: "#ffffff", black: "#000000", transparent: "#00000000" };

function splitTop(text) {
  const parts = [];
  let depth = 0;
  let start = 0;
  for (let i = 0; i < text.length; i++) {
    const ch = text[i];
    if (ch === "(") depth++;
    else if (ch === ")") depth--;
    else if (ch === "," && depth === 0) {
      parts.push(text.slice(start, i).trim());
      start = i + 1;
    }
  }
  parts.push(text.slice(start).trim());
  return parts;
}

function fn(expr, name) {
  const head = name + "(";
  if (!expr.toLowerCase().startsWith(head)) return null;
  if (!expr.endsWith(")")) return null;
  /* لا بدّ أن يكون القوس الأخير هو المُغلِق للأول، وإلا فالتعبير مركّب. */
  let depth = 0;
  for (let i = head.length - 1; i < expr.length; i++) {
    if (expr[i] === "(") depth++;
    else if (expr[i] === ")") {
      depth--;
      if (depth === 0) return i === expr.length - 1 ? splitTop(expr.slice(head.length, i)) : null;
    }
  }
  return null;
}

function fromHex(hex) {
  let h = hex.slice(1);
  if (h.length === 3 || h.length === 4) h = [...h].map((c) => c + c).join("");
  const n = (i) => Number.parseInt(h.slice(i, i + 2), 16);
  return { r: n(0), g: n(2), b: n(4), a: h.length === 8 ? n(6) / 255 : 1 };
}

/** يفصل عن وسيط `color-mix` نسبتَه المئوية إن وُجدت. */
function splitPercent(arg) {
  const m = /^(.*?)\s+([0-9.]+)%$/.exec(arg) ?? /^([0-9.]+)%\s+(.*)$/.exec(arg);
  if (!m) return { color: arg, percent: null };
  return /%$/.test(arg)
    ? { color: m[1].trim(), percent: Number(m[2]) }
    : { color: m[2].trim(), percent: Number(m[1]) };
}

/**
 * يحلّ تعبير لونٍ إلى {r,g,b,a}. يفهم ما تستعمله هذه اللوحة فعلاً:
 * `var()` بسلسلتها وارتدادها · `color-mix(in srgb …)` بمزجٍ مضروبٍ مسبقاً
 * بالشفافية كما ينصّ المعيار · `#hex` بأشكاله الأربعة · `rgb()/rgba()` ·
 * `transparent`. وما لا يفهمه **يرمي** ولا يخمّن لوناً — والقياس المخمَّن أسوأ
 * من لا قياس، لأنه يبدو قياساً.
 */
export function resolveColor(expr, vars, seen = new Set()) {
  const s = String(expr).trim();
  if (Object.prototype.hasOwnProperty.call(NAMED, s.toLowerCase())) {
    return fromHex(NAMED[s.toLowerCase()]);
  }
  if (s.startsWith("#")) return fromHex(s);

  const varArgs = fn(s, "var");
  if (varArgs) {
    const name = varArgs[0].trim();
    if (seen.has(name)) throw new Error("دورة في الرموز عند " + name);
    if (vars.has(name)) return resolveColor(vars.get(name), vars, new Set(seen).add(name));
    if (varArgs.length > 1) return resolveColor(varArgs.slice(1).join(","), vars, seen);
    throw new Error("رمزٌ غير معرَّف: " + name);
  }

  const mixArgs = fn(s, "color-mix");
  if (mixArgs) {
    if (!/^in\s+srgb$/i.test(mixArgs[0].trim())) {
      throw new Error("فضاء مزجٍ غير مدعوم: " + mixArgs[0]);
    }
    const a = splitPercent(mixArgs[1]);
    const b = splitPercent(mixArgs[2]);
    let pa = a.percent;
    let pb = b.percent;
    if (pa === null && pb === null) { pa = 50; pb = 50; }
    else if (pa === null) pa = 100 - pb;
    else if (pb === null) pb = 100 - pa;
    const total = pa + pb;
    pa = pa / total;
    pb = pb / total;
    const ca = resolveColor(a.color, vars, seen);
    const cb = resolveColor(b.color, vars, seen);
    /* المعيار يمزج مضروباً مسبقاً بالشفافية — ولهذا `color-mix(X 55%, transparent)`
       يساوي X بشفافية .55 لا رمادياً مائلاً إلى السواد. */
    const alpha = ca.a * pa + cb.a * pb;
    const chan = (k) =>
      alpha === 0 ? 0 : (ca[k] * ca.a * pa + cb[k] * cb.a * pb) / alpha;
    return { r: chan("r"), g: chan("g"), b: chan("b"), a: alpha };
  }

  const rgbArgs = fn(s, "rgba") ?? fn(s, "rgb");
  if (rgbArgs) {
    const nums = (rgbArgs.length === 1 ? rgbArgs[0].split(/[\s/]+/) : rgbArgs)
      .map((x) => x.trim())
      .filter(Boolean);
    const v = (x) => (x.endsWith("%") ? (Number(x.slice(0, -1)) * 255) / 100 : Number(x));
    return {
      r: v(nums[0]),
      g: v(nums[1]),
      b: v(nums[2]),
      a: nums[3] === undefined ? 1 : Number(nums[3].endsWith("%") ? Number(nums[3].slice(0, -1)) / 100 : nums[3]),
    };
  }

  throw new Error("تعبيرُ لونٍ لا يُفهم: " + s);
}

/** يستخرج ألوان مواقف التدرّج من قيمة `*-gradient(...)`، ويتجاهل الهندسة. */
export function gradientStops(expr, vars) {
  const m = /^[a-z-]*gradient\(([\s\S]*)\)$/i.exec(String(expr).trim());
  if (!m) return null;
  const stops = [];
  for (const arg of splitTop(m[1])) {
    /* الموقف قد يحمل موضعاً بعده: «var(--x) 62%». نُسقط ذيل المواضع ثم نجرّب. */
    const candidate = arg.replace(/\s+(-?[0-9.]+(px|%|em|rem|deg))+\s*$/g, "").trim();
    try {
      stops.push(resolveColor(candidate, vars));
    } catch {
      /* وسيطُ هندسةٍ لا لون — يُتجاهل. */
    }
  }
  return stops.length ? stops : null;
}

/* ═══════════════════════════════════════ ٣ · التركيب والقياس */

/** يضع لوناً فوق آخر (source-over). */
export function over(top, bottom) {
  const a = top.a + bottom.a * (1 - top.a);
  if (a === 0) return { r: 0, g: 0, b: 0, a: 0 };
  const chan = (k) => (top[k] * top.a + bottom[k] * bottom.a * (1 - top.a)) / a;
  return { r: chan("r"), g: chan("g"), b: chan("b"), a };
}

export function relativeLuminance({ r, g, b }) {
  const f = (c) => {
    const x = c / 255;
    return x <= 0.03928 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

export function contrastRatio(fg, bg) {
  const a = relativeLuminance(fg);
  const b = relativeLuminance(bg);
  return (Math.max(a, b) + 0.05) / (Math.min(a, b) + 0.05);
}

/* ═════════════════════════════ ٤ · فرق اللون — كي لا يصير الطرفان رماديين */

/** CIE76 ΔE في فضاء Lab — يكفي لسؤال «هل ما زال اللونان مختلفين؟». */
export function deltaE(c1, c2) {
  const lab = ({ r, g, b }) => {
    const f = (c) => {
      const x = c / 255;
      return x <= 0.04045 ? x / 12.92 : ((x + 0.055) / 1.055) ** 2.4;
    };
    const [R, G, B] = [f(r), f(g), f(b)];
    const X = (R * 0.4124 + G * 0.3576 + B * 0.1805) / 0.95047;
    const Y = R * 0.2126 + G * 0.7152 + B * 0.0722;
    const Z = (R * 0.0193 + G * 0.1192 + B * 0.9505) / 1.08883;
    const g2 = (t) => (t > 0.008856 ? Math.cbrt(t) : 7.787 * t + 16 / 116);
    const [fx, fy, fz] = [g2(X), g2(Y), g2(Z)];
    return [116 * fy - 16, 500 * (fx - fy), 200 * (fy - fz)];
  };
  const [a1, b1, c1x] = lab(c1);
  const [a2, b2, c2x] = lab(c2);
  return Math.hypot(a1 - a2, b1 - b2, c1x - c2x);
}

/** زاويةُ الصبغة بالدرجات — تجيب «هل ما زال هذا اللون من عائلته؟». */
export function hueAngle({ r, g, b }) {
  const [R, G, B] = [r / 255, g / 255, b / 255];
  const max = Math.max(R, G, B);
  const min = Math.min(R, G, B);
  const d = max - min;
  if (d === 0) return null;
  let h;
  if (max === R) h = ((G - B) / d) % 6;
  else if (max === G) h = (B - R) / d + 2;
  else h = (R - G) / d + 4;
  h *= 60;
  return h < 0 ? h + 360 : h;
}

/* ═══════════════════════════════════════════════ ٥ · جرد الأزواج الحقيقية

   كل صفٍّ هنا مأخوذٌ من قاعدة CSS قائمة، ومكتوبٌ في `where` ملفُّها ومحدّدها.
   وطبقاتُ الخلفية تُكتب **من الأسفل إلى الأعلى** كما تُرسَم فعلاً؛ وحين لا
   تكون الخلفية لوناً واحداً (تدرّجُ الأرضية، تدرّجُ لوح الحضور، لونُ قسمٍ من
   خمسة) تُكتب الطبقة **قائمةَ احتمالات** ويُؤخذ أسوؤها. فلا يُدَّعى دقّةٌ
   ليست عندنا، ولا يُقاس على أحسن الأحوال.
   ═══════════════════════════════════════════════════════════════════════════ */

/** ألوان الأقسام الخمسة كما تكتبها `sections.ts` في `--section-tint`. */
const SECTION_TINTS = [
  "var(--section-accounting)",
  "var(--section-inventory)",
  "var(--section-hr)",
  "var(--section-contracting)",
  "var(--section-realestate)",
];

/* «الأرضية» ليست لوناً مسطّحاً بل تدرّجٌ شعاعي — فالطبقة احتمالان: أفتح ما
   فيه وأغمق ما فيه. ويُقرأان من الرمز نفسه لا يُكتبان هنا. */
const GROUND = "@ground";

const TEXT = "text";
const LARGE = "large";
const NONTEXT = "nontext";

export const PAIRS = [
  /* ── ١ · النصّ على الأسطح المصمتة ───────────────────────────────────── */
  { id: "text/ground", fg: "var(--color-text)", bg: [GROUND], kind: TEXT,
    where: "app.css body · shell.css .app-shell" },
  { id: "text/surface", fg: "var(--color-text)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .ctl · .card" },
  { id: "textMuted/surface", fg: "var(--color-text-muted)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .btn · .chip · .pager .pagebtn" },
  { id: "textSubtle/surface", fg: "var(--color-text-subtle)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .hint · .crumbs · .amt .cur" },
  { id: "text/sunken", fg: "var(--color-text)", bg: ["var(--color-surface-sunken)"], kind: TEXT,
    where: "components.css .iconbtn:hover · .navitem:hover" },
  { id: "textMuted/sunken", fg: "var(--color-text-muted)", bg: ["var(--color-surface-sunken)"], kind: TEXT,
    where: "primitives.css .ledger thead th · components.css table.data thead th" },
  { id: "textSubtle/sunken", fg: "var(--color-text-subtle)", bg: ["var(--color-surface-sunken)"], kind: TEXT,
    where: "components.css .tab .count · .empty .ico · presence.css .trace__step" },
  { id: "textSubtle/raised", fg: "var(--color-text-subtle)", bg: ["var(--color-surface-raised)"], kind: TEXT,
    where: "shell.css .cmdk__foot" },
  { id: "text/raised", fg: "var(--color-text)", bg: ["var(--color-surface-raised)"], kind: TEXT,
    where: "primitives.css .ledger tbody tr:hover td" },
  { id: "textMuted/panelTint", fg: "var(--color-text-muted)", bg: ["@panel"], kind: TEXT,
    where: "cinematic.css --backdrop-panel" },

  /* ── ٢ · الهيكل الزجاجي فوق الأرضية السينمائية ───────────────────────
     الرأس والجانب سطحان **شبه شفّافين** فوق تدرّجٍ — فالخلفية مركّبة لا مفردة. */
  { id: "textMuted/sideGlass", fg: "var(--color-text-muted)",
    bg: [GROUND, "color-mix(in srgb, var(--color-surface) 88%, transparent)"], kind: TEXT,
    where: "shell.css .app-side + .section" },
  { id: "textSubtle/sideGlass", fg: "var(--color-text-subtle)",
    bg: [GROUND, "color-mix(in srgb, var(--color-surface) 88%, transparent)"], kind: TEXT,
    where: "shell.css .sections__label · .section__soon" },
  { id: "textMuted/topbarGlass", fg: "var(--color-text-muted)",
    bg: [GROUND, "color-mix(in srgb, var(--color-surface) 84%, transparent)"], kind: TEXT,
    where: "shell.css .app-topbar" },
  { id: "text/currentSection", fg: "var(--color-text)",
    bg: [GROUND, "color-mix(in srgb, var(--color-surface) 88%, transparent)",
         SECTION_TINTS.map((t) => `color-mix(in srgb, ${t} 14%, transparent)`)], kind: TEXT,
    where: "shell.css .section[aria-current=page]" },

  /* ── ٣ · الحبر فوق الأسطح الملوّنة — رموز `--on-*` ────────────────────
     وهذه هي أكثر ستّة أرقامٍ تُقرأ في المنتج: رأسا «مدين» و«دائن». */
  { id: "onDebit/debit", fg: "var(--on-debit)", bg: ["var(--color-debit)"], kind: TEXT,
    where: "primitives.css .ledger thead th.h-debit · app.css table.tb thead th.h-debit" },
  { id: "onCredit/credit", fg: "var(--on-credit)", bg: ["var(--color-credit)"], kind: TEXT,
    where: "primitives.css .ledger thead th.h-credit · app.css table.tb thead th.h-credit" },
  { id: "onBrand/primary", fg: "var(--on-brand)", bg: ["var(--color-primary)"], kind: TEXT,
    where: "components.css .btn-primary · .navitem[aria-current] · .skiplink · .pager" },
  { id: "onBrand/primaryHover", fg: "var(--on-brand)", bg: ["var(--color-primary-hover)"], kind: TEXT,
    where: "components.css .btn-primary:hover" },
  { id: "onDanger/danger", fg: "var(--on-danger)", bg: ["var(--color-danger)"], kind: TEXT,
    where: "components.css .btn-danger · .toast--danger" },
  { id: "onDanger/dangerHover", fg: "var(--on-danger)",
    bg: ["color-mix(in srgb,var(--color-danger) 84%,var(--shade))"], kind: TEXT,
    where: "components.css .btn-danger:hover" },
  { id: "onSuccess/success", fg: "var(--on-success)", bg: ["var(--color-success)"], kind: TEXT,
    where: "components.css .toast--success" },
  { id: "onWarning/warning", fg: "var(--on-warning)", bg: ["var(--color-warning)"], kind: TEXT,
    where: "components.css .toast--warning" },
  { id: "onDebit/avatar", fg: "var(--on-debit)", bg: ["var(--color-debit)"], kind: TEXT,
    where: "components.css .avatar" },
  { id: "onAccentFixed/switchOff", fg: "var(--on-accent-fixed)",
    bg: ["var(--color-surface)", "var(--color-border-control)"], kind: NONTEXT,
    where: "components.css .switch .track::after فوق المسار المطفأ" },
  { id: "onBrand/switchOn", fg: "var(--on-brand)",
    bg: ["var(--color-surface)", "var(--color-primary)"], kind: NONTEXT,
    where: "components.css .switch input:checked + .track::after" },
  { id: "bg/toast", fg: "var(--color-bg)", bg: ["var(--color-text)"], kind: TEXT,
    where: "components.css .toast" },

  /* ── ٤ · اللون الدلالي حبراً على سطحه الخفيف ─────────────────────────── */
  { id: "debit/debitSoft", fg: "var(--color-debit)", bg: ["var(--color-debit-soft)"], kind: TEXT,
    where: "components.css .pill--debit" },
  { id: "credit/creditSoft", fg: "var(--color-credit)", bg: ["var(--color-credit-soft)"], kind: TEXT,
    where: "components.css .pill--credit" },
  { id: "posted/postedSoft", fg: "var(--color-posted)", bg: ["var(--color-posted-soft)"], kind: TEXT,
    where: "components.css .pill--posted · .note-ok · .alert--success" },
  { id: "pending/pendingSoft", fg: "var(--color-pending)", bg: ["var(--color-pending-soft)"], kind: TEXT,
    where: "components.css .pill--pending · .note-warn · .alert--warning" },
  { id: "rejected/rejectedSoft", fg: "var(--color-rejected)", bg: ["var(--color-rejected-soft)"], kind: TEXT,
    where: "components.css .pill--rejected · .alert--danger · .errstate .ico" },
  { id: "reversed/reversedSoft", fg: "var(--color-reversed)", bg: ["var(--color-reversed-soft)"], kind: TEXT,
    where: "components.css .pill--reversed · .alert--ai" },
  { id: "draft/draftSoft", fg: "var(--color-draft)", bg: ["var(--color-draft-soft)"], kind: TEXT,
    where: "components.css .pill--draft" },
  { id: "brandInk/primarySoft", fg: "var(--brand-ink)", bg: ["var(--color-primary-soft)"], kind: TEXT,
    where: "components.css .pill--info · .note-info · .subitem[aria-current] · .picker-opt:hover" },
  { id: "primary/primarySoft", fg: "var(--color-primary)", bg: ["var(--color-primary-soft)"], kind: TEXT,
    where: "components.css .iconbtn[aria-pressed] · .pagehead .ico · .addline:hover" },
  { id: "primary/surface", fg: "var(--color-primary)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .tab[aria-selected] · .pager .pagebtn:hover · .crumbs a:hover" },
  { id: "primary/sunken", fg: "var(--color-primary)", bg: ["var(--color-surface-sunken)"], kind: TEXT,
    where: "components.css th.sortable > button:hover · th[aria-sort] > button" },
  { id: "ai/surface", fg: "var(--color-ai)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "shell.css .voicedock · presence.css .presence__head" },
  { id: "ai/aiSoft", fg: "var(--color-ai)", bg: ["var(--color-ai-soft)"], kind: TEXT,
    where: "presence.css .prov[data-source=inferred] · components.css .alert--ai" },
  { id: "ai/aiIcon", fg: "var(--color-ai)",
    bg: ["var(--color-surface)", "color-mix(in srgb,var(--color-ai) 16%,transparent)"], kind: TEXT,
    where: "components.css .ai-card .card-hd .ico" },
  { id: "ai/presencePanel", fg: "var(--color-ai)",
    bg: [["var(--color-ai-soft)", "var(--color-surface)"]], kind: TEXT,
    where: "presence.css .presence (تدرّجٌ بين طرفين)" },
  { id: "danger/dangerHoverTint", fg: "var(--color-danger)",
    bg: ["var(--color-surface)", "color-mix(in srgb,var(--color-danger) 12%,transparent)"], kind: TEXT,
    where: "components.css .menu button.danger:hover" },
  { id: "success/successSoft", fg: "var(--color-success)", bg: ["var(--color-success-soft)"], kind: TEXT,
    where: "presence.css .prov[data-source=attested]" },
  { id: "textSubtle/archived", fg: "var(--color-text-subtle)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .pill--archived (خلفيةٌ شفّافة فوق اللوح)" },
  { id: "textMuted/dbAlertTint", fg: "var(--color-text-muted)",
    bg: ["color-mix(in srgb,var(--color-danger) 8%,var(--color-surface))"], kind: TEXT,
    where: "components.css .alert--db .server-text — التقطه حارس التغطية لا اليد" },
  { id: "text/cmdkSelected", fg: "var(--color-text)", bg: ["var(--color-primary-soft)"], kind: TEXT,
    where: "shell.css .cmdk__item[aria-selected=true] — التقطه حارس التغطية لا اليد" },

  /* ── شريط المدّة — أوّليّةٌ تهبط من فرعٍ مجاور (‏claude/screens-realestate)
     وتُقاس **قبل** أن تهبط. أزواجها تُبنى من رموزٍ قائمة، فتُحلّ اليوم؛ ولو
     تأخّر الفرع بقيت قياساً صحيحاً لتركيبةِ رموزٍ موجودة. */
  { id: "text/bandSpan", fg: "var(--color-text)",
    bg: ["color-mix(in srgb, var(--color-primary) 26%, var(--color-surface))"], kind: TEXT,
    where: "primitives.css .band__span (وارد من claude/screens-realestate)" },
  { id: "text/bandSpanDone", fg: "var(--color-text)",
    bg: ["color-mix(in srgb, var(--color-success) 26%, var(--color-surface))"], kind: TEXT,
    where: "primitives.css .band__span[data-state=done] (وارد)" },
  { id: "text/bandSpanConflict", fg: "var(--color-text)",
    bg: ["color-mix(in srgb, var(--color-danger) 30%, var(--color-surface))"], kind: TEXT,
    where: "primitives.css .band__span[data-state=conflict] (وارد)" },

  /* ── ٥ · الأرقام في الدفتر — أكثر ما يُقرأ في المنتج ─────────────────── */
  { id: "debit/surface", fg: "var(--color-debit)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .amt--debit · .amt-input.is-debit" },
  { id: "credit/surface", fg: "var(--color-credit)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .amt--credit · .amt-input.is-credit" },
  { id: "debit/ledgerEven", fg: "var(--color-debit)",
    bg: ["var(--color-surface)", "color-mix(in srgb, var(--color-surface-sunken) 42%, transparent)"], kind: TEXT,
    where: "primitives.css .ledger tbody tr:nth-child(even) td" },
  { id: "credit/ledgerEven", fg: "var(--color-credit)",
    bg: ["var(--color-surface)", "color-mix(in srgb, var(--color-surface-sunken) 42%, transparent)"], kind: TEXT,
    where: "primitives.css .ledger tbody tr:nth-child(even) td" },
  { id: "debit/ledgerFoot", fg: "var(--color-debit)", bg: ["var(--color-surface-sunken)"], kind: TEXT,
    where: "primitives.css .ledger tfoot td.d" },
  { id: "credit/ledgerFoot", fg: "var(--color-credit)", bg: ["var(--color-surface-sunken)"], kind: TEXT,
    where: "primitives.css .ledger tfoot td.c" },
  { id: "amountNegative/surface", fg: "var(--color-amount-negative)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .amt--neg · .amt-input.is-neg" },
  { id: "amountZero/surface", fg: "var(--color-amount-zero)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "components.css .amt--zero · .amt--dash" },
  { id: "codeMuted/ledgerEven", fg: "var(--color-text-muted)",
    bg: ["var(--color-surface)", "color-mix(in srgb, var(--color-surface-sunken) 42%, transparent)"], kind: TEXT,
    where: "primitives.css .ledger .code" },
  { id: "altSubtle/ledgerEven", fg: "var(--color-text-subtle)",
    bg: ["var(--color-surface)", "color-mix(in srgb, var(--color-surface-sunken) 42%, transparent)"], kind: TEXT,
    where: "primitives.css .ledger .alt" },

  /* ── ٦ · درجةُ الثقة: الرقم يحمل لون النطاق ──────────────────────────── */
  { id: "successBand/surface", fg: "var(--color-success)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "presence.css .confidence[data-band=high] .confidence__value" },
  { id: "warningBand/surface", fg: "var(--color-warning)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "presence.css .confidence[data-band=medium] .confidence__value" },
  { id: "dangerBand/surface", fg: "var(--color-danger)", bg: ["var(--color-surface)"], kind: TEXT,
    where: "presence.css .confidence[data-band=low] .confidence__value" },

  /* ── ٧ · ما ليس نصّاً — 1.4.11 عتبتها 3:1 ───────────────────────────── */
  { id: "focusRing/surface", fg: "var(--color-focus)", bg: ["var(--color-surface)"], kind: NONTEXT,
    where: "components.css :focus-visible outline" },
  { id: "focusRing/ground", fg: "var(--color-focus)", bg: [GROUND], kind: NONTEXT,
    where: "components.css :focus-visible outline فوق الأرضية" },
  { id: "focusRing/sunken", fg: "var(--color-focus)", bg: ["var(--color-surface-sunken)"], kind: NONTEXT,
    where: "primitives.css .ledger tbody tr:focus-visible" },
  { id: "controlBorder/surface", fg: "var(--color-border-control)", bg: ["var(--color-surface)"], kind: NONTEXT,
    where: "components.css .ctl · .btn · .seg button · .switch .track — حدُّ مكوّنٍ يلزم لتمييزه" },
  { id: "controlBorder/ground", fg: "var(--color-border-control)", bg: [GROUND], kind: NONTEXT,
    where: "components.css .btn فوق الأرضية السينمائية لا فوق لوح" },
  { id: "confidenceFill/rail", fg: "var(--color-success)", bg: ["var(--color-surface-sunken)"], kind: NONTEXT,
    where: "presence.css .confidence__fill داخل .confidence__rail (نطاق high)" },
  { id: "confidenceFillWarn/rail", fg: "var(--color-warning)", bg: ["var(--color-surface-sunken)"], kind: NONTEXT,
    where: "presence.css .confidence__fill (نطاق medium)" },
  { id: "confidenceFillLow/rail", fg: "var(--color-danger)", bg: ["var(--color-surface-sunken)"], kind: NONTEXT,
    where: "presence.css .confidence__fill (نطاق low)" },
  { id: "loadbarFill/track", fg: "var(--color-primary)",
    bg: [GROUND, "color-mix(in srgb,var(--color-primary) 20%,transparent)"], kind: NONTEXT,
    where: "components.css .loadbar::after داخل .loadbar" },
  { id: "sectionMark/sideGlass", fg: SECTION_TINTS,
    bg: [GROUND, "color-mix(in srgb, var(--color-surface) 88%, transparent)"], kind: NONTEXT,
    where: "shell.css .section__mark — شارةُ القسم" },
  { id: "debitHead/surface", fg: "var(--color-debit)", bg: ["var(--color-surface)"], kind: NONTEXT,
    where: "primitives.css th.h-debit — حدُّ العمود الملوّن عن اللوح" },
  { id: "creditHead/surface", fg: "var(--color-credit)", bg: ["var(--color-surface)"], kind: NONTEXT,
    where: "primitives.css th.h-credit — حدُّ العمود الملوّن عن اللوح" },

  /* ── ٨ · الورق — لوحةٌ مستقلّة عن السمة عمداً، وتُقاس مثلها ─────────── */
  { id: "printInk/paper", fg: "var(--print-ink)", bg: ["var(--print-paper)"], kind: TEXT,
    where: "print.css — الحبر على الورق" },
  { id: "printDebitInk/debitBg", fg: "var(--print-debit-ink)", bg: ["var(--print-debit-bg)"], kind: TEXT,
    where: "print.css — عمود المدين مطبوعاً" },
  { id: "printCreditInk/creditBg", fg: "var(--print-credit-ink)", bg: ["var(--print-credit-bg)"], kind: TEXT,
    where: "print.css — عمود الدائن مطبوعاً" },
  { id: "printInk3/paper", fg: "var(--print-ink-4)", bg: ["var(--print-paper)"], kind: TEXT,
    where: "print.css — أخفت درجات الحبر" },
];

/* ما **لا** يدخل الجرد، ولماذا — والصمت عن ذلك أسوأ من استثنائه:
   · `.brand .mark` — شعارٌ في عنصرٍ **فارغ** `aria-hidden`، لا نصّ فيه ولا
     معلومة؛ ولو حمل نصّاً لكان شعاراً، وشعارُ المنتج مستثنى من 1.4.3 نصّاً.
   · قرصُ المفتاح مقابل مساره **بلون الحالة**: حالة المفتاح يحملها **موضع**
     القرص، والذي تطلبه 1.4.11 هو تمييز المكوّن — وهو مسارُه على السطح،
     وهو مقيسٌ أعلاه (`controlBorder/surface`). ومع ذلك قِيس القرصان أيضاً.
   · العناصر المعطّلة (`:disabled`) — مستثناة بنصّ 1.4.3 و1.4.11. */

export const THRESHOLD = { [TEXT]: 4.5, [LARGE]: 3, [NONTEXT]: 3 };

/* ═══════════════════════════ ٥٫١ · حارس التغطية — لا زوجَ يمرّ بلا قياس

   **لماذا يوجد:** الجرد أعلاه مكتوبٌ بيد، وستّة وكلاء يبنون على هذه الطبقة
   الآن. مكوّنٌ جديد يهبط بـ`color:` و`background:` جديدين **لا يظهر في الجرد
   من نفسه** — فيمرّ بلا قياس، والمقياس يبقى أخضر وهو لا يعرف بوجوده. وحارسٌ
   يُعلن تغطيةً لا يملكها أسوأ من غياب الحارس.

   فهذا الحارس يمشي على ملفّات المكوّنات، ويلتقط **كل قاعدة تحمل حبراً وخلفية
   معاً**، ويحلّهما، ويطلب أن يكون الزوج (بلونيه المحلولين، في السمتين)
   **مقيساً في الجرد**. وما ليس فيه يُسمّى بمحدِّده وملفّه.

   وما يتخطّاه مُصرَّح به لا مسكوتٌ عنه:
     · `background:transparent` — الخلفية الفعلية سطحُ الأب، ولا يُعرف ساكناً.
     · `color:inherit` — الحبر من الأب.
     · قواعد بلا واحدٍ منهما.
   ═══════════════════════════════════════════════════════════════════════════ */

const SKIP_BG = new Set(["transparent", "none", "currentcolor", "inherit"]);

/**
 * قواعدُ **مستثناةٌ بالاسم ومعها سببها** — والاستثناء المكتوب أشرف من قاعدةٍ
 * تُسقَط بصمت من الجرد. ومن يحذف السبب يحذف الاستثناء معه.
 */
export const COVERAGE_EXEMPT = [
  {
    selector: ".brand .mark",
    why: "شعارٌ في عنصرٍ **فارغ** `aria-hidden=\"true\"` — لا نصّ فيه ولا معلومة. ولو حمل نصّاً لكان شعار المنتج، وهو مستثنى بنصّ 1.4.3.",
  },
  {
    selector: '.section[aria-current="page"]',
    why: "`--section-tint` تكتبه `sections.ts` **على العنصر وقت التشغيل** فلا يوجد في أي ملفّ سمة. وهو مقيسٌ صراحةً في `text/currentSection` على الألوان الخمسة كلّها، وتُؤخذ أسوؤها.",
  },
];

const exempt = (selector) =>
  COVERAGE_EXEMPT.some((e) => selector.includes(e.selector));

/** يلتقط من ملفّات المكوّنات كل قاعدة تحمل حبراً وخلفيةً معاً. */
export function declaredPairs(dir = STYLES, files = COMPONENT_FILES) {
  const found = [];
  for (const file of files) {
    const rules = walk(stripComments(readFileSync(path.join(dir, file), "utf8")), [], []);
    for (const rule of rules) {
      const body = rule.body;
      const fg = /(?:^|[;\s])color\s*:([^;]+)/.exec(body)?.[1]?.trim();
      const bg = /background(?:-color)?\s*:([^;]+)/.exec(body)?.[1]?.trim();
      if (!fg || !bg) continue;
      if (SKIP_BG.has(fg.toLowerCase()) || SKIP_BG.has(bg.toLowerCase())) continue;
      if (exempt(rule.selector)) continue;
      found.push({ file, selector: rule.selector, fg, bg });
    }
  }
  return found;
}

/**
 * يُخرج القواعد التي لا يغطّيها الجرد. والمقارنة **بالألوان المحلولة** لا
 * بنصّ التعبير: صياغتان مختلفتان للون نفسه زوجٌ واحد، ولا يُطلَب من أحد أن
 * يكتب المسافات كما كُتبت هنا.
 */
export function coverageProblems(palettes = PALETTES) {
  const problems = [];
  for (const [palette, files] of Object.entries(palettes)) {
    const themes = readThemes(files);
    for (const [theme, vars] of [["light", themes.light], ["dark", themes.dark]]) {
      const measured = new Set();
      for (const pair of PAIRS) {
        let fgs;
        let layers;
        try {
          fgs = expand(pair.fg, vars);
          layers = pair.bg.map((l) => expand(l, vars));
        } catch {
          continue;
        }
        for (const combo of cartesian(layers)) {
          const bg = combo.reduce((acc, layer) => (acc === null ? layer : over(layer, acc)), null);
          for (const fg of fgs) measured.add(hex(fg.a < 1 ? over(fg, bg) : fg) + "/" + hex(bg));
        }
      }
      for (const rule of declaredPairs()) {
        let fg;
        let bgs;
        try {
          fg = resolveColor(rule.fg, vars);
          bgs = gradientStops(rule.bg, vars) ?? [resolveColor(rule.bg, vars)];
        } catch {
          problems.push(
            `${palette}/${theme} · ${rule.file} · ${rule.selector}: تعذّر حلّ «${rule.fg}» أو «${rule.bg}»`
          );
          continue;
        }
        for (const bg of bgs) {
          if (bg.a < 1) continue; /* خلفيةٌ شفّافة: سطح الأب غير معلوم ساكناً. */
          const key = hex(fg.a < 1 ? over(fg, bg) : fg) + "/" + hex(bg);
          if (!measured.has(key)) {
            problems.push(
              `${palette}/${theme} · ${rule.file} · ${rule.selector}: ` +
                `«${rule.fg}» على «${rule.bg}» (${key}) ليس في الجرد — أضِف صفّاً إلى PAIRS`
            );
          }
        }
      }
    }
  }
  return [...new Set(problems)];
}

/* ═══════════════════════════════════════════════════ ٦ · تنفيذ القياس */

function expand(layerExpr, vars) {
  if (layerExpr === GROUND) {
    const stops = gradientStops(vars.get("--backdrop-cinematic"), vars);
    if (!stops) throw new Error("تعذّر قراءة مواقف --backdrop-cinematic");
    return stops;
  }
  if (layerExpr === "@panel") {
    const stops = gradientStops(vars.get("--backdrop-panel"), vars);
    if (!stops) throw new Error("تعذّر قراءة مواقف --backdrop-panel");
    return stops;
  }
  const list = Array.isArray(layerExpr) ? layerExpr : [layerExpr];
  return list.map((e) => resolveColor(e, vars));
}

function cartesian(lists) {
  return lists.reduce((acc, list) => acc.flatMap((prefix) => list.map((x) => [...prefix, x])), [[]]);
}

/** يقيس زوجاً واحداً في سمةٍ واحدة، ويأخذ **أسوأ** احتمالٍ من احتمالات الخلفية. */
export function measure(pair, vars) {
  const fgs = expand(pair.fg, vars);
  const layerOptions = pair.bg.map((l) => expand(l, vars));
  let worst = null;
  for (const combo of cartesian(layerOptions)) {
    const bg = combo.reduce((acc, layer) => (acc === null ? layer : over(layer, acc)), null);
    for (const fg of fgs) {
      const flatFg = fg.a < 1 ? over(fg, bg) : fg;
      const ratio = contrastRatio(flatFg, bg);
      if (worst === null || ratio < worst.ratio) worst = { ratio, fg: flatFg, bg };
    }
  }
  return worst;
}

const hex = ({ r, g, b }) =>
  "#" + [r, g, b].map((c) => Math.round(c).toString(16).padStart(2, "0")).join("");

/**
 * يقيس كل الأزواج في السمتين ويُخرج صفوفاً مرتّبة.
 * @returns {{rows: Array, failures: Array, parity: string[]}}
 */
export function audit(palettes = PALETTES) {
  const rows = [];
  const parity = [];
  for (const [palette, files] of Object.entries(palettes)) {
    const themes = readThemes(files);
    for (const problem of themeParityProblems(themes)) parity.push(palette + " · " + problem);
    for (const [theme, vars] of [["light", themes.light], ["dark", themes.dark]]) {
      for (const pair of PAIRS) {
        const got = measure(pair, vars);
        const need = THRESHOLD[pair.kind];
        rows.push({
          palette,
          theme,
          id: pair.id,
          kind: pair.kind,
          where: pair.where,
          fg: pair.fg,
          ratio: Math.round(got.ratio * 100) / 100,
          need,
          pass: got.ratio + 1e-9 >= need,
          fgHex: hex(got.fg),
          bgHex: hex(got.bg),
        });
      }
    }
  }
  return { rows, failures: rows.filter((r) => !r.pass), parity };
}

/* ═══════════════════════════════ ٧ · حرّاس المعنى: اللون ليس الحامل الوحيد */

/**
 * ثلاثة أسئلةٍ لا يجيبها رقم التباين وحده:
 *   ١ · هل يبقى المدين والدائن **مختلفَين** بعد التغميق أو التفتيح؟
 *   ٢ · هل تبقى كلٌّ منهما في عائلتها اللونية (سماوي/أزرق) لا رماديّين؟
 *   ٣ · هل تبقى ألوان الحالة الثلاث متمايزةً بعضها عن بعض؟
 */
/**
 * أدنى ΔE مقبول بين لونين **يحملان معنيين مختلفين**. القيمة ليست عرفاً
 * عالمياً بل حدٌّ اختير هنا: ΔE 20 في CIE76 فرقٌ يراه من يفرّق الألوان
 * بلا تردّد، ويبقى مرئياً لأشيع صور عمى الألوان حين يقع بين عائلتين
 * لونيتين لا داخل عائلة واحدة — ولذلك تُقاس **الصبغة** معه لا وحدها.
 */
export const MIN_DELTA_E = 20;

/** عائلة الصبغة التي يجب أن يبقى فيها كل طرفٍ من طرفي القيد (بالدرجات). */
export const HUE_FAMILY = {
  "--debit": { name: "فيروزي · teal", from: 150, to: 190 },
  "--credit": { name: "سماوي · sky", from: 190, to: 215 },
};

export function distinctness(palettes = PALETTES) {
  const out = [];
  for (const [palette, files] of Object.entries(palettes)) {
    const themes = readThemes(files);
    for (const [theme, vars] of [["light", themes.light], ["dark", themes.dark]]) {
      const c = (name) => resolveColor(`var(${name})`, vars);
      const pairsToCheck = [
        ["--debit", "--credit"],
        ["--credit", "--brand"],
        ["--good", "--warn"],
        ["--good", "--req"],
        ["--warn", "--req"],
      ];
      for (const [a, b] of pairsToCheck) {
        const d = Math.round(deltaE(c(a), c(b)) * 10) / 10;
        out.push({ palette, theme, a, b, deltaE: d, pass: d >= MIN_DELTA_E });
      }
      for (const [token, family] of Object.entries(HUE_FAMILY)) {
        const hue = Math.round(hueAngle(c(token)));
        out.push({
          palette, theme, a: token, b: family.name, hue,
          pass: hue >= family.from && hue <= family.to,
        });
      }
    }
  }
  return out;
}

/* ═══════════════════════════════════════════════════════ ٨ · التقرير */

function table(rows) {
  const w = (s, n) => String(s).padEnd(n).slice(0, n);
  const lines = [];
  lines.push(
    "  " + w("palette", 9) + w("theme", 6) + w("pair", 26) + w("kind", 9) + "ratio".padStart(7) +
    "  need".padStart(7) + "  " + w("fg → bg", 18) + "verdict"
  );
  lines.push("  " + "─".repeat(93));
  for (const r of rows) {
    lines.push(
      "  " + w(r.palette, 9) + w(r.theme, 6) + w(r.id, 26) + w(r.kind, 9) +
      r.ratio.toFixed(2).padStart(7) + r.need.toFixed(1).padStart(7) + "  " +
      w(r.fgHex + " → " + r.bgHex, 18) + (r.pass ? "✓" : "✗ دون العتبة")
    );
  }
  return lines.join("\n");
}

function main() {
  const argv = process.argv.slice(2);
  const quiet = argv.includes("--quiet");
  const asJson = argv.includes("--json");
  const result = audit();
  const distinct = distinctness();
  const flat = distinct.filter((d) => !d.pass);
  const uncovered = coverageProblems();
  const green =
    result.failures.length === 0 && result.parity.length === 0 &&
    flat.length === 0 && uncovered.length === 0;

  if (asJson) {
    process.stdout.write(
      JSON.stringify({ ...result, distinctness: distinct, uncovered, sources: SOURCES }, null, 2) + "\n"
    );
  } else {
    const shown = quiet ? result.failures : result.rows;
    process.stdout.write(
      "\n══ مقياس التباين — " + PAIRS.length + " زوجاً × " +
      Object.keys(PALETTES).length + " لوحة × سمتين = " + result.rows.length + " قياساً\n\n"
    );
    process.stdout.write(table(shown) + "\n\n");
    process.stdout.write("── ما قُرئ فعلاً (src/styles/):\n    " + SOURCES.join("\n    ") + "\n\n");
    if (uncovered.length) {
      process.stdout.write("✗ قواعدُ تحمل حبراً وخلفيةً ولا تُقاس:\n");
      for (const u of uncovered) process.stdout.write("    " + u + "\n");
      process.stdout.write("\n");
    } else {
      process.stdout.write(
        "✓ التغطية: كل قاعدة تحمل حبراً وخلفيةً في " + COMPONENT_FILES.length +
        " ملفّ مكوّنات مقيسةٌ في الجرد، عدا " + COVERAGE_EXEMPT.length + " استثناءً مُصرَّحاً به.\n\n"
      );
    }
    if (result.parity.length) {
      process.stdout.write("✗ الداكن الصريح وتفضيل النظام لا يتطابقان:\n");
      for (const p of result.parity) process.stdout.write("    " + p + "\n");
      process.stdout.write("\n");
    }
    process.stdout.write("── فرقُ اللون (ΔE ≥ " + MIN_DELTA_E + ") وعائلةُ الصبغة:\n");
    for (const d of quiet ? flat : distinct) {
      process.stdout.write(
        "    " + d.palette.padEnd(9) + d.theme.padEnd(6) + (d.a + " / " + d.b).padEnd(28) +
        (d.hue === undefined ? "ΔE " + String(d.deltaE).padStart(5) : "hue " + String(d.hue).padStart(4) + "°") +
        "  " + (d.pass ? "✓" : "✗ متقاربان") + "\n"
      );
    }
    process.stdout.write(
      "\n" + (green
        ? "✔ كل قياسٍ فوق عتبته، والسمتان متطابقتان، والألوان الحاملة للمعنى متمايزة.\n"
        : "✗ " + (result.failures.length + flat.length + result.parity.length + uncovered.length) +
          " مخالفة.\n")
    );
  }
  process.exitCode = green ? 0 : 1;
}

if (process.argv[1] && path.resolve(process.argv[1]) === path.resolve(fileURLToPath(import.meta.url))) {
  main();
}
