#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   فحص الواجهة — خليفة design/audit.js
   Front-end audit — successor to design/audit.js
   ───────────────────────────────────────────────────────────────────────────
       node scripts/audit.mjs            تقرير كامل
       node scripts/audit.mjs --quiet    الأخطاء فقط
   يخرج بالرمز 1 عند أي مخالفة حاكمة، فيصلح بوّابةً في خطّ التكامل.

   يفحص عشرة أشياء لا تستطيع عينٌ بشرية أن تتعقّبها:
     ١ · مفاتيح ناقصة في أي لغة، ومفاتيح يتيمة (تسقط إلى العربية بصمت)
     ٢ · فئات جمع ناقصة أو ميتة         (zero في الإنجليزية صيغة لا تُختار أبداً)
     ٣ · تطابق معاملات الاستبدال مع المصدر
     ٤ · اصطلاح تسمية المفاتيح
     ٥ · مفتاح تطلبه الشاشات وغير معرَّف
     ٦ · نصّ مرئي مكتوب في الشيفرة
     ٧ · مخالفات اتجاه في CSS
     ٨ · محارف تحكّم غير مرئية في المصدر
     ٩ · صفحة العقد قائمة بذاتها ويُحلَّل نصُّها البرمجي
     ‏١٠ · خطّ كل لغة وترميزها — لأن الفحص ١ **يرضى بالقمامة**: تكافؤ المفاتيح
          يقول «موجود» ويُقرأ «مترجَم»، وبينهما قيمةٌ مشوّهة الترميز تصل قارئاً

   ⚠ وكل فحص هنا يُعلن **حجم ما فحصه**، ويفشل إن كان صفراً. مسحٌ لا يقرأ شيئاً
   يمرّ دائماً، وهو بالضبط عطل فخ-43 في هذا المستودع.
   ═══════════════════════════════════════════════════════════════════════════ */
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
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
  scriptOf,
  valueTexts,
  witnessesOf,
} from "./locale-script.mjs";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB = path.resolve(HERE, "..");
const REPO = path.resolve(WEB, "..");
const SRC = path.join(WEB, "src");
const QUIET = process.argv.includes("--quiet");

let fatal = 0;
let warn = 0;
let debt = 0;
const out = [];

/* ═════════════════════════ الدين المعلَن · declared debt ══════════════════
   ‏**ليس إعفاءً، وليس تخطّياً لمسار.** المخالفة تُكتشَف وتُعدّ وتُطبَع كما هي؛
   وكل ما يفعله هذا الإعلان أنه يمنعها من إحمار البوّابة **ما دام عددها لم
   يتغيّر**. والفرق بين الاثنين هو الفرق بين دينٍ مقروء وبين عمى:

     · إعفاءُ مسار يجعل المخالفة **غير مرئية**، فيصير المسار المُعفى المكان
       الوحيد الذي يستطيع العطل أن يعيش فيه (فخ-43، وADR-0032 §المسابر).
     · والدين المعلَن يُبقيها **مرئية ومعدودة**، ويجعل أي زيادة حمراء فوراً.

   ‏**والسقف ينزل ولا يصعد** — ومثل القاعدة 14، إن نزل العدد فالبوّابة تحمرّ
   حتى يُنزَّل السقف معه. وهذا هو الحارس ضدّ فخ-43 بعينه: كاشفٌ عمي، أو مجلّدٌ
   حُذف، يُظهر نفسه بانخفاضٍ عن السقف بدل أن يمرّ صامتاً.

   ‏**ونطاقه ضيّق مرّتين**: مسارٌ واحد مسمّى، وفحصٌ واحد من ثمانية. وما عداه —
   بما فيه فحوص الاتجاه ومحارف التحكّم داخل المسار نفسه — يبقى حاكماً بصفر.

   ‏Declared debt — not an exemption and not a path skip. The violations are
   still detected, counted and printed; the ceiling only stops them reddening
   the gate while their number is unchanged. It may fall, never rise.
   ════════════════════════════════════════════════════════════════════════ */
const DECLARED_DEBT = {
  check: "٦ · نصّ مرئي مكتوب في الشيفرة",
  scope: "src/demo/",
  ceiling: 147,
  /* لماذا دينٌ لا إصلاح: طبقة العرض نصُّها **سردُ فيلم** لا واجهة منتج —
     ‏146 نصّاً فريداً، 82 منها شظايا جملةٍ مقطوعةٍ حول وسمٍ داخلي لا تصلح
     مفاتيح. ونقلُها إلى ملفّات اللغة يوجب — بحكم الفحص ١ نفسه — اختلاق نحو
     400 ترجمة أردية وهندية وإنجليزية لسردٍ لن يُقرأ إلا بالعربية، فيدخل
     السجلَّ نصٌّ مُختلَق لا يُميَّز عن الترجمة الحقيقية. ADR-جديد
     «طبقة العرض دينٌ معلَن لا مسارٌ مُعفى». */
};
const debtScopeFiles = [];
const head = (t) => out.push("", "─".repeat(74), t, "─".repeat(74));
const ok = (t) => out.push("  ✓ " + t);
const info = (t) => out.push("  · " + t);
/** يطبع ديناً معلَناً: مرئيٌّ ومعدود، ولا يُحمِّر ما دام عند سقفه. */
function declared(title, list) {
  debt += list.length;
  out.push("  ⓘ دين معلَن · declared debt: " + title + " (" + list.length + ")");
  for (const x of list.slice(0, 40)) out.push("      " + x);
  if (list.length > 40) out.push("      … +" + (list.length - 40));
}
function bad(title, list, isFatal) {
  if (isFatal) fatal += list.length;
  else warn += list.length;
  out.push("  " + (isFatal ? "✗" : "!") + " " + title + " (" + list.length + ")");
  for (const x of list.slice(0, 40)) out.push("      " + x);
  if (list.length > 40) out.push("      … +" + (list.length - 40));
}
/** حارس اللافراغ: يُفشل الفحص إن لم يقرأ شيئاً. */
function mustScan(what, count, minimum) {
  info(what + ": " + count);
  if (count < minimum) {
    fatal++;
    out.push(
      "  ✗ النطاق ضامر · vacuous scope: " + what + " = " + count + " (الحدّ الأدنى " + minimum + ")"
    );
    return false;
  }
  return true;
}

/** شاهد إيجابي: يزرع مخالفة معروفة ويتأكّد أن الكاشف يلتقطها.
    كاشفٌ توقّف عن الكشف يمرّ صامتاً على كل شيء — وهذا ما يمنعه هذا الحارس. */
function selfTest(what, detected) {
  if (detected) {
    info("شاهد إيجابي · positive control: " + what + " ✓");
    return true;
  }
  fatal++;
  out.push("  ✗ الكاشف لا يكشف · detector is blind: " + what);
  return false;
}

function walk(dir, hit) {
  for (const name of fs.readdirSync(dir)) {
    if (name === "node_modules" || name === "dist" || name === ".git") continue;
    const full = path.join(dir, name);
    const stat = fs.statSync(full);
    if (stat.isDirectory()) walk(full, hit);
    else hit(full);
  }
}
const rel = (f) => path.relative(WEB, f).replace(/\\/g, "/");

/** ينزع التعليقات ويُبقي عدد الأسطر كما هو، فتبقى أرقام الأسطر صحيحة. */
function stripComments(text) {
  return text
    .replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, " "))
    .replace(/(^|[^:])\/\/[^\n]*/g, (m, p1) => p1 + m.slice(p1.length).replace(/[^\n]/g, " "));
}

/* ═════════════════════════════ تحميل اللغات ═══════════════════════════════
   الملفّات TypeScript، ولا نُشغِّل مترجماً: نقرأ الكائنات الحرفية بعد نزع
   الأنواع بتحويلٍ صغير محدّد — يكفي لأن الملفّات مُولَّدة بشكل معلوم. */
const CODES = ["ar", "en", "ur", "hi"];
const SOURCE = "ar";

function loadObject(file, name) {
  const text = fs.readFileSync(file, "utf8");
  const marker = "export const " + name;
  const at = text.indexOf(marker);
  if (at < 0) throw new Error("لا يوجد " + name + " في " + rel(file));
  const open = text.indexOf("{", text.indexOf("=", at));
  if (open < 0) throw new Error("لا كائن حرفي بعد " + name + " في " + rel(file));
  /* موازنة الأقواس مع احترام النصوص — الملفّات مُولَّدة بشكل معلوم، ولا
     تعليقات داخل الكائن، فالموازنة كافية ومحدّدة. */
  let depth = 0;
  let quote = null;
  let end = -1;
  for (let i = open; i < text.length; i++) {
    const ch = text[i];
    if (quote) {
      if (ch === "\\") i++;
      else if (ch === quote) quote = null;
      continue;
    }
    if (ch === '"' || ch === "'" || ch === "`") quote = ch;
    else if (ch === "{") depth++;
    else if (ch === "}") {
      depth--;
      if (depth === 0) {
        end = i;
        break;
      }
    }
  }
  if (end < 0) throw new Error("قوس غير متوازن في " + rel(file));
  const body = text.slice(open, end + 1);
  return new Function("return (" + body + ");")();
}

const CLDR = ["zero", "one", "two", "few", "many", "other"];
const EXACT = /^=\d+$/;
function isPluralBag(v) {
  if (!v || typeof v !== "object" || Array.isArray(v)) return false;
  for (const k of Object.keys(v)) if (!CLDR.includes(k) && !EXACT.test(k)) return false;
  return Object.prototype.hasOwnProperty.call(v, "other");
}
function flatten(tree, prefix = "", outMap = {}) {
  for (const [k, v] of Object.entries(tree)) {
    const key = prefix ? prefix + "." + k : k;
    if (v && typeof v === "object" && !Array.isArray(v) && !isPluralBag(v)) flatten(v, key, outMap);
    else outMap[key] = v;
  }
  return outMap;
}
function mergeTree(base, extra) {
  const outTree = { ...base };
  for (const [k, v] of Object.entries(extra)) {
    const cur = outTree[k];
    if (cur && typeof cur === "object" && !Array.isArray(cur) && v && typeof v === "object" && !Array.isArray(v) && !("other" in v)) {
      outTree[k] = mergeTree(cur, v);
    } else outTree[k] = v;
  }
  return outTree;
}

const messages = {};
const metas = {};
/* أي ملفّ جاء منه كل مفتاح — كي يسمّي الرفضُ الملفَّ والمفتاح معاً لا المفتاح وحده. */
const origin = {};
for (const code of CODES) {
  const baseFile = path.join(SRC, "i18n/locales", code + ".base.ts");
  const webFile = path.join(SRC, "i18n/locales", code + ".web.ts");
  const base = loadObject(baseFile, "messages");
  const web = loadObject(webFile, "messages");
  metas[code] = loadObject(baseFile, "meta");
  messages[code] = flatten(mergeTree(base, web));
  origin[code] = {};
  for (const k of Object.keys(flatten(base))) origin[code][k] = rel(baseFile);
  for (const k of Object.keys(flatten(web))) origin[code][k] = rel(webFile);
}

head("١ · تغطية المفاتيح · key coverage");
const union = new Set();
for (const code of CODES) for (const k of Object.keys(messages[code])) union.add(k);
const allKeys = [...union].sort();
const scopeOk = mustScan("مفاتيح في الاتحاد · keys in union", allKeys.length, 600);
info("لغات · locales: " + CODES.length);
for (const code of CODES) info("  " + code + ": " + Object.keys(messages[code]).length);

const missingByLocale = [];
const orphanByLocale = [];
for (const code of CODES) {
  const own = messages[code];
  for (const k of allKeys) if (!(k in own)) missingByLocale.push(code + " ← " + k);
  for (const k of Object.keys(own)) if (!(k in messages[SOURCE])) orphanByLocale.push(code + " ← " + k);
}
if (missingByLocale.length) bad("مفتاح ناقص في لغة · missing in a locale", missingByLocale, true);
else if (scopeOk) ok("كل مفتاح موجود في اللغات الأربع · every key exists in all four");
if (orphanByLocale.length) bad("مفتاح يتيم (ليس في المصدر) · orphan", orphanByLocale, true);
else ok("لا مفاتيح يتيمة · no orphans");

head("٢ · الجمع · plurals");
const pluralProblems = [];
let bagsChecked = 0;
for (const code of CODES) {
  const cats = new Intl.PluralRules(metas[code].pluralLocale ?? code)
    .resolvedOptions()
    .pluralCategories.slice()
    .sort((a, b) => CLDR.indexOf(a) - CLDR.indexOf(b));
  info(code + " فئات · categories: " + cats.join(" · "));
  for (const [k, v] of Object.entries(messages[code])) {
    if (!isPluralBag(v)) continue;
    bagsChecked++;
    for (const cat of cats) {
      if (!(cat in v) && !("=0" in v && cat === "one")) {
        if (!(cat in v)) pluralProblems.push(code + " ← " + k + " ينقصه · missing: " + cat);
      }
    }
    for (const form of Object.keys(v)) {
      if (EXACT.test(form)) continue;
      if (!cats.includes(form)) pluralProblems.push(code + " ← " + k + " صيغة ميتة · dead form: " + form);
    }
  }
}
mustScan("أكياس جمع مفحوصة · plural bags checked", bagsChecked, 40);
if (pluralProblems.length) bad("فئة جمع ناقصة أو ميتة", pluralProblems, true);
else ok("كل كيس جمع يغطّي فئات لغته بلا صيغة ميتة");

head("٣ · معاملات الاستبدال · interpolation parameters");
const paramProblems = [];
let paramsChecked = 0;
/* المقارنة على **مجموعة** المعاملات لا على تكرارها: كيس الجمع العربي فيه
   ستّ صيغ والإنجليزي صيغتان، فعدّ التكرار يقول «اختلاف» حيث لا اختلاف.
   والمعاملان count و countRaw تحقنهما الطبقة نفسها فلا يُقارَنان. */
const paramsOf = (v) => {
  const text = typeof v === "string" ? v : Object.values(v).join(" ");
  const found = new Set(
    (text.match(/\{\w+\}/g) ?? []).filter((x) => x !== "{count}" && x !== "{countRaw}")
  );
  return [...found].sort().join(",");
};
for (const code of CODES) {
  if (code === SOURCE) continue;
  for (const [k, v] of Object.entries(messages[code])) {
    const src = messages[SOURCE][k];
    if (src === undefined) continue;
    paramsChecked++;
    const a = paramsOf(src);
    const b = paramsOf(v);
    if (a !== b) paramProblems.push(k + " : ar{" + a + "} ≠ " + code + "{" + b + "}");
  }
}
mustScan("مقارنات معاملات · parameter comparisons", paramsChecked, 1500);
if (paramProblems.length) bad("معاملات لا تطابق المصدر", paramProblems, true);
else ok("معاملات كل ترجمة تطابق المصدر");

head("٤ · اصطلاح التسمية · key naming convention");
/* ‏**المجالات المعتمدة، والخمسة الأخيرة مجالُ قسمٍ لكلٍّ من الأقسام الخمسة.**
   مفاتيح القسم كلّها تحت مجاله، فلا تتسرّب إلى `screen.*` المشترك ولا
   يتصادم وكيلان على مفتاحٍ واحد.

   ‏**والخمسة مكتوبةٌ كلّها منذ الآن عمداً، ولو لم تُبنَ شاشاتُ بعضها بعد.**
   والسبب اندماجيّ لا جماليّ: خمسةُ وكلاءٍ يبنون الأقسام على التوازي، ولو
   أضاف كلٌّ منهم مجالَه وحده إلى هذا السطر لصار السطرُ الواحد **تصادمَ دمجٍ
   خماسياً**، وحلُّه بأخذ قائمة أحد الطرفين يُسقِط مجالات الباقين فتحمرّ
   بوّابتهم بمفاتيح صحيحة. والقائمة الكاملة تجعل الدمج بلا أثر.

   ومجالٌ مُجازٌ لا مفتاح تحته **لا أثر له**: الفحص ٤ يرفض مفتاحاً في مجالٍ
   غير معتمد، ولا يطلب أن يكون لكل مجالٍ معتمدٍ مفتاح. والفحص ١ (الاتحاد)
   و٥ (ما تطلبه الشاشات) يبقيان حاكمَين كما هما. */
const NAMESPACES = [
  "app", "common", "acct", "field", "screen", "gallery", "css", "audit",
  "accounting", "inventory", "hr", "contracting", "realestate",
  "agent",
];
const conventionProblems = [];
for (const k of Object.keys(messages[SOURCE])) {
  const seg = k.split(".");
  if (!/^[a-zA-Z0-9.]+$/.test(k)) conventionProblems.push(k + " ← محارف غير مسموحة");
  else if (!NAMESPACES.includes(seg[0])) conventionProblems.push(k + " ← مجال غير معتمد: " + seg[0]);
  else if (seg.length < 2 || seg.length > 5) conventionProblems.push(k + " ← عدد المقاطع " + seg.length);
}
if (conventionProblems.length) bad("مخالفة اصطلاح", conventionProblems, true);
else ok("كل مفتاح يطابق الاصطلاح (" + NAMESPACES.join(" · ") + ")");

/* ═════════════ ٥ · ما تطلبه الشاشات فعلاً ══════════════════════════════ */
head("٥ · المفاتيح المطلوبة من الشاشات · keys referenced by screens");
const tsFiles = [];
walk(SRC, (f) => {
  if (/\.(ts|tsx)$/.test(f) && !/\/i18n\/locales\//.test(f)) tsFiles.push(f);
});
mustScan("ملفات مصدر ممسوحة · source files scanned", tsFiles.length, 15);

const used = new Map();
const KEY_CALL = /\bt(?:p)?\(\s*["']([a-zA-Z0-9.]+)["']/g;
const prefixes = new Map();
for (const f of tsFiles) {
  const text = stripComments(fs.readFileSync(f, "utf8"));
  let m;
  KEY_CALL.lastIndex = 0;
  while ((m = KEY_CALL.exec(text))) {
    const key = m[1];
    /* مفتاح مركّب مثل t("app.health." + state): ما بين علامتَي الاقتباس
       بادئةٌ لا مفتاح، ويُتحقَّق منها بأن تحتها مفاتيح فعلاً. */
    if (key.endsWith(".")) {
      if (!prefixes.has(key)) prefixes.set(key, rel(f));
    } else if (!used.has(key)) used.set(key, rel(f));
  }
}
mustScan("مفاتيح مطلوبة · referenced keys", used.size, 40);
info("بادئات مركّبة · composed prefixes: " + prefixes.size);
const undefinedKeys = [];
for (const [k, where] of used) {
  const absent = CODES.filter((c) => !(k in messages[c]));
  if (absent.length) undefinedKeys.push(k + "  ← " + where + "  (" + absent.join(",") + ")");
}
for (const [prefix, where] of prefixes) {
  for (const c of CODES) {
    const any = Object.keys(messages[c]).some((k) => k.startsWith(prefix));
    if (!any) undefinedKeys.push(prefix + "*  ← " + where + "  (" + c + ": لا مفتاح بهذه البادئة)");
  }
}
if (undefinedKeys.length) bad("مفتاح مطلوب غير معرَّف", undefinedKeys, true);
else ok("كل مفتاح تطلبه الشاشات معرَّف في اللغات الأربع");

/* ═════════════ ٦ · نصّ مرئي في الشيفرة ═════════════════════════════════ */
head("٦ · نصّ مرئي مكتوب في الشيفرة · hard-coded visible text");
const LETTERS = /[\u0600-\u06FF\u0900-\u097F\u0750-\u077FA-Za-z]{3,}/;
/* ما ليس نصّاً مرئياً: كل ما يحمل علامة شيفرة. الأنواع العامّة في
   TypeScript تكتب <T> و=> فتبدو للنظرة الأولى وسماً، ولذلك تُستثنى صراحةً. */
const CODEISH = /[;=(){}]|=>|\bReact\b|:\s/;
/** يستخرج عُقد النصّ المكتوبة في الوسم. */
function jsxTextNodes(source) {
  const found = [];
  const rx = /([^=\-!<])>([^<>{}]{2,}?)</gs;
  let m;
  while ((m = rx.exec(source))) {
    const value = m[2].trim();
    if (!value || CODEISH.test(value)) continue;
    found.push(value);
  }
  return found;
}
const hardcoded = [];
const declaredHits = [];
let jsxTextScanned = 0;
for (const f of tsFiles.filter((x) => x.endsWith(".tsx"))) {
  const where = rel(f);
  const inDebtScope = where.startsWith(DECLARED_DEBT.scope);
  if (inDebtScope) debtScopeFiles.push(where);
  const nodes = jsxTextNodes(stripComments(fs.readFileSync(f, "utf8")));
  jsxTextScanned += nodes.length;
  for (const value of nodes) {
    if (!LETTERS.test(value)) continue;
    const entry = where + ": «" + value.slice(0, 60) + "»";
    /* داخل النطاق المُعلَن: دينٌ مرئي. وخارجه: حاكمٌ بصفر كما كان. */
    if (inDebtScope) declaredHits.push(entry);
    else hardcoded.push(entry);
  }
}
selfTest(
  "نصّ مرئي في الوسم",
  jsxTextNodes('<p className="x">حفظ القيد</p>').some((v) => LETTERS.test(v))
);
selfTest(
  "لا يخلط النوع العامّ بالوسم",
  jsxTextNodes("const a = useRef<HTMLInputElement>(null);").length === 0
);
info("عُقد نصّ في JSX مفحوصة · JSX text nodes inspected: " + jsxTextScanned);
if (hardcoded.length) bad("نصّ مرئي غير مترجَم", hardcoded, true);
else ok("لا نصّ مرئي مكتوب في الوسم خارج النطاق المُعلَن");

/* ── السقف: ينزل ولا يصعد، ويحمرّ في الاتجاهين ─────────────────────────── */
info(
  "نطاق الدين · debt scope: " + DECLARED_DEBT.scope +
    " (" + debtScopeFiles.length + " ملفّ · files)"
);
if (declaredHits.length) declared(DECLARED_DEBT.scope + " — " + DECLARED_DEBT.check, declaredHits);
if (declaredHits.length > DECLARED_DEBT.ceiling) {
  fatal += declaredHits.length - DECLARED_DEBT.ceiling;
  out.push(
    "  ✗ الدين المعلَن ارتفع · declared debt rose: " + declaredHits.length +
      " > السقف · ceiling " + DECLARED_DEBT.ceiling +
      " — السقف لا يُرفع؛ النصّ الجديد يمرّ بطبقة اللغة." +
      " The ceiling is never raised; new text goes through the i18n layer."
  );
} else if (declaredHits.length < DECLARED_DEBT.ceiling) {
  /* ‏فخ-43 بعينه: كاشفٌ عمي أو مجلّدٌ حُذف يظهر هنا لا يمرّ صامتاً. */
  fatal++;
  out.push(
    "  ✗ الدين نزل ولم ينزل السقف · debt fell but the ceiling did not: " +
      declaredHits.length + " < " + DECLARED_DEBT.ceiling +
      " — أنزِل ceiling في scripts/audit.mjs إلى " + declaredHits.length + "." +
      " Lower the ceiling in scripts/audit.mjs to " + declaredHits.length + "."
  );
} else if (declaredHits.length) {
  ok(
    "الدين المعلَن عند سقفه بالضبط · declared debt exactly at its ceiling (" +
      DECLARED_DEBT.ceiling + ")"
  );
}
/* حارس اللافراغ على النطاق نفسه: سقفٌ غير صفري على مجلّد فارغ عمى لا دين. */
if (DECLARED_DEBT.ceiling > 0) mustScan("ملفات في نطاق الدين · files in debt scope", debtScopeFiles.length, 1);

/* ═════════════ ٧ · الاتجاه في CSS ═══════════════════════════════════════ */
head("٧ · الاتجاه في CSS · direction in CSS");
const cssFiles = [];
walk(SRC, (f) => {
  if (f.endsWith(".css")) cssFiles.push(f);
});
mustScan("ملفات CSS ممسوحة · stylesheets scanned", cssFiles.length, 4);
const dirProblems = [];
let declarations = 0;
const PHYSICAL = /(^|[^-\w])(margin|padding|border|inset|float|clear|text-align)-(left|right)\s*:/;
const HARD_DIRECTION = /(^|[^-\w])direction\s*:\s*(rtl|ltr)/;

/** يفحص سطر CSS واحداً ويعيد وصف المخالفة أو null. */
function cssLineProblem(code, neighbourhood) {
  if (PHYSICAL.test(code)) return "خاصية فيزيائية: " + code.trim();
  /* الاتجاه على html المجرّد هو العطل الحاكم: يفوز على [dir] بترتيب الأسلوب،
     فتبقى الصفحة معكوسة مهما قالت السمة (design/README §٧٫٢-١).
     والاحتياط المسموح وحده html:not([dir]) — يعمل قبل تحميل اللغة فقط. */
  if (/(^|[\s,}])html\s*(\{|,)/.test(code) && /direction\s*:/.test(code)) {
    return "direction على html المجرّد: " + code.trim();
  }
  if (HARD_DIRECTION.test(code)) {
    /* الاستثناء الوحيد: خانة رقمية أو نصّ آلي، ويجب أن يصحبه عزل. */
    if (!/unicode-bidi\s*:\s*isolate/.test(neighbourhood) && !/html:not\(\[dir\]\)/.test(code)) {
      return "اتجاه مثبّت بلا عزل: " + code.trim();
    }
  }
  if (/transform\s*:[^;]*translateX\(/.test(code) && !/--flip-x|translateX\(0\)/.test(code)) {
    return "translateX بلا --flip-x: " + code.trim();
  }
  return null;
}

for (const f of cssFiles) {
  /* التعليقات تُنزَع من الملفّ كاملاً لا سطراً سطراً: تعليقٌ متعدّد الأسطر
     يشرح عطلاً اتجاهياً كان يُقرأ سطوره على أنها شيفرة فيُبلَّغ عنه خطأً. */
  const lines = stripComments(fs.readFileSync(f, "utf8")).split("\n");
  lines.forEach((code, i) => {
    if (code.includes(":")) declarations++;
    const problem = cssLineProblem(code, lines.slice(Math.max(0, i - 4), i + 5).join(" "));
    if (problem) dirProblems.push(rel(f) + ":" + (i + 1) + " " + problem);
  });
}
selfTest("خاصية فيزيائية", cssLineProblem(".x{margin-left:8px}", "") !== null);
selfTest("direction على html", cssLineProblem("html{direction:rtl}", "") !== null);
selfTest("translateX بلا flip-x", cssLineProblem(".d{transform:translateX(100%)}", "") !== null);
selfTest(
  "لا يبلّغ عن الخانة الرقمية المعزولة",
  cssLineProblem("td.n{direction:ltr;", "unicode-bidi:isolate") === null
);
selfTest(
  "لا يبلّغ عن الاحتياط html:not([dir])",
  cssLineProblem("html:not([dir]){direction:rtl}", "") === null
);
mustScan("تصريحات CSS مفحوصة · CSS declarations inspected", declarations, 800);
if (dirProblems.length) bad("مخالفة اتجاه", dirProblems, true);
else ok("لا خاصية فيزيائية، ولا اتجاه بلا عزل، ولا transform اتجاهي بلا --flip-x");

/* ═════════════ ٨ · محارف التحكّم غير المرئية ════════════════════════════ */
head("٨ · محارف التحكّم غير المرئية · invisible control characters");
const INVISIBLE = /[\u200B-\u200F\u061C\u202A-\u202E\u2066-\u2069\uFEFF]/g;
const invisible = [];
let bytesScanned = 0;
const scanned = [];
walk(SRC, (f) => {
  if (/\.(ts|tsx|css)$/.test(f)) scanned.push(f);
});
for (const f of scanned) {
  const text = fs.readFileSync(f, "utf8");
  bytesScanned += text.length;
  let m;
  INVISIBLE.lastIndex = 0;
  while ((m = INVISIBLE.exec(text))) {
    const line = text.slice(0, m.index).split("\n").length;
    invisible.push(
      rel(f) + ":" + line + " U+" + m[0].charCodeAt(0).toString(16).toUpperCase().padStart(4, "0")
    );
  }
}
mustScan("محارف ممسوحة · characters scanned", bytesScanned, 200000);
if (invisible.length) bad("محرف تحكّم غير مرئي في المصدر", invisible, true);
else ok("لا محرف تحكّم غير مرئي في أي ملف مصدر");

/* ═════════════ ٩ · صفحة العقد قائمة بذاتها وتُحلَّل ═══════════════════════
   ولماذا هنا: صفحة /docs تُخدَم من `src/Babel.Api/OpenApi/docs.html`، وعطبان
   فيها **صامتان تماماً**: خطأ نحوي في نصّها البرمجي يجعل المتصفّح يعرض قشرةً
   فارغة بـ200 OK، وأصلٌ خارجي يجعلها تُخفق خلف خروج مقيَّد (فخ-83). والحارس
   المكافئ في .NET يمسح النصّ ولا يستطيع أن **يحلّل** JavaScript؛ وهذا الملفّ
   يعمل بـnode بلا تثبيت، فهو الموضع الذي يستطيع.
   ═══════════════════════════════════════════════════════════════════════ */
head("٩ · صفحة العقد · the contract page");

const DOCS_PAGE = path.join(REPO, "src", "Babel.Api", "OpenApi", "docs.html");

if (!fs.existsSync(DOCS_PAGE)) {
  bad("صفحة العقد غير موجودة · the contract page is missing", [rel(DOCS_PAGE)], true);
} else {
  const page = fs.readFileSync(DOCS_PAGE, "utf8");
  mustScan("محارف الصفحة · page characters", page.length, 2000);

  const externals = [];
  for (const needle of ["http://", "https://", "//cdn", "//unpkg", "//jsdelivr", "integrity=", "crossorigin"]) {
    if (page.toLowerCase().includes(needle)) externals.push(needle);
  }
  if (externals.length) bad("الصفحة تجلب أصلاً خارجياً · the page fetches an external asset", externals, true);
  else ok("لا أصل خارجي في صفحة العقد · no external asset in the contract page");

  /* النصّ البرمجي يُحلَّل فعلاً — لا يُفحَص بالنظر. */
  const scripts = [...page.matchAll(/<script>([\s\S]*?)<\/script>/g)].map((m) => m[1]);
  if (scripts.length === 0) {
    bad("لا نصّ برمجي في صفحة العقد · the contract page carries no script", [rel(DOCS_PAGE)], true);
  } else {
    let broken = null;
    for (const source of scripts) {
      try {
        new Function(source);
      } catch (e) {
        broken = e.message;
        break;
      }
    }
    if (broken) {
      bad(
        "خطأ نحوي في نصّ الصفحة — المتصفّح يعرض قشرةً فارغة بـ200 OK · " +
          "a syntax error in the page script: the browser renders an empty shell with 200 OK",
        [broken],
        true
      );
    } else {
      ok("نصّ صفحة العقد يُحلَّل بلا خطأ · the contract page script parses cleanly");
    }
  }
}

/* ═════════════ ١٠ · خطّ اللغة والترميز ══════════════════════════════════
   ‏**العطل الذي يجعله مستحيلاً.** الفحص ١ يقول «كل مفتاح موجود في اللغات
   الأربع»، ويُقرأ — من الجميع، ومن هذا الملفّ نفسه حتى اليوم — «اللغات
   الأربع مترجَمة». وهما ليسا الشيء نفسه: **تكافؤ المفاتيح يرضى بالقمامة.**
   وقد وقع: قيمةُ رفضٍ في الهندية والأردية رُمِّزت UTF-8 وقُرئت Latin-1،
   فصارت 444 من 488 محرفاً هنديّاً في كتلة لاتينية — والمسح كلُّه أخضر،
   ‏والقيمة تصل قارئاً هنديّاً حقيقياً لأن الشاشة تحلّ المفتاح وقت التشغيل.

   ‏**ولماذا ليس جدول محارف مسموحة.** لأن ذلك قائمة، والقائمة تُهزَم بأول
   محرفٍ لم يُكتب — ثلاث مرّات في يومٍ واحد في هذا المستودع
   (‏`docs/evidence/traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy`).
   فالقواعد الخمس هنا كلُّها **مقاييس على النتيجة**، وكلٌّ منها أُطلقت عليها
   طفرةٌ حقيقية وقِيس ما تلتقطه وما يفلت منها:

     ‏أ · **التشويه يُكشف بفكّ الترميز فعلاً**، لا بمعرفة أشكال المحارف.
          مقطعٌ كلُّه ≤ U+00FF، رموزُه بايتاتٌ تفكّ UTF-8 صارمةً إلى نصٍّ
          **مختلف** ⇐ ليس نصّاً، بل بايتاتُ نصٍّ آخر. يُصيب أي لغةِ مصدر،
          وأي جزءٍ مشوَّهٍ من قيمةٍ سليمة بقيّتها، بلا ذكر محرفٍ واحد.
     ‏ب · **خطُّ اللغة يُشتقّ من رمزها** عبر `Intl.Locale.maximize()` وخصائص
          يونيكود، لا من جدولٍ هنا. ومن يشهد أن المفتاح مفتاحُ نثرٍ لا رمزٍ
          آلي؟ **لغةٌ خطُّها دليل** — أي خطٌّ لا يحوي الحرف اللاتيني `A`،
          لأن خطّ `A` هو خطّ المعرّفات الآلية (`PDF`, `SAR`, `BANK-0001`)
          ويظهر داخل كل لغة. فلكلِّ لغةٍ هنا شاهدان على الأقلّ، ولا لغةَ بلا
          شاهد — والحالةُ الأخيرة **حمراء** لا صامتة (انظر `noWitness`).
     ‏ج · **نصٌّ يطابق المصدر حرفاً بحرف ليس ترجمة** — غطاءٌ جزئي على ما لا
          يستطيع الخطُّ أن يراه.
     ‏د · **إذنٌ مغلق للحرف الأجنبي.** القاعدة (أ) تفكّ البايتات، فيفلت منها ما
          قُرئ بترميزٍ يرفع بعضها فوق U+00FF (`koi8-r`, `macintosh`,
          `iso-8859-7`) — **مقيس**. فتُصنَّف حروف كل قيمة ثلاثة أقسام: بخطّ
          لغتها، أو ASCII (أبجدية المعرّفات بحكم العقد المنشور)، أو **أجنبيّ
          غير آليّ** — وهذا الأخير مسموحٌ بشرطٍ واحد: أن يظهر المقطع نفسه في
          قيمة المفتاح نفسه في **لغةٍ أخرى**. إذنٌ مغلق لا منعٌ مفتوح، فترميزٌ
          لم يخطر لأحد يسقط فيه بلا أن يعرفه الحارس.
     ‏هـ · **محارف لا تُرسم ولا تعني** — تحكّم C0/C1، وبديل، واستعمال خاصّ، وغير
          مخصَّص. فئةُ يونيكود لا قائمة، وهي ما تُنتجه بايتات UTF-16 المقروءة
          نصّاً — وهو ما يفلت من (أ) و(د) معاً. **مقيس.**

   ‏**والثقب مُعلَن، لا مسكوتٌ عنه.** خطّ الأردية هو خطّ العربية، فالقاعدة (ب)
   تلتقط قيمةً أرديةً **فقدت** خطَّها ولا تلتقط قيمةً أرديةً كُتبت بالعربية.
   ونصٌّ عربيٌّ يُنسخ حرفاً بحرف في `ur.web.ts` تلتقطه (ج) بشرطها: **ثلاث كلمات
   فأكثر**. فيبقى ما يهزم الفحص كلَّه: **عبارةٌ عربية في الأردية إمّا أقصر من
   ثلاث كلمات، وإمّا مُعادةُ الصياغة فلا تطابق المصدر حرفاً بحرف.** وشرطُ
   الثلاث مقيسٌ لا مُختار: ستّ قيم أردية اليوم تطابق العربية حرفاً بحرف وكلّها
   مصطلحٌ واحد أو اسمُ عَلَم (`سلاسل بابل`، `متوازن`، `صفر`، `مطابق`،
   `استحقاق`، `مظ`) — فتضييقه إلى كلمة يُنتج ستّ حمراوات كاذبة. ويوثّق الثقبَ
   اختبارٌ **سلوكيّ** في `tests/locale-script.test.ts` لا تعليقٌ هنا.
   **وما لا يلتقطه أي شيء**: نصٌّ بخطّ لغته الصحيح ومعناه خطأ — ولا حارس آليّ
   يقرأ المعنى.
   ═══════════════════════════════════════════════════════════════════════ */
head("١٠ · خطّ اللغة والترميز · script and encoding");

/* ‏**نطاق الفحص يُقرأ من القرص لا من `CODES`.** ملفّ لغةٍ خامسة يُودَع ولا يُكتب
   رمزُه في هذا الملفّ **يخرج من الفحوص العشرة كلِّها بصمت** — وهو حارسٌ يُصدَّق
   وهو لا يحرس. فالمجموعة المغلقة هنا هي **المجلّد**: كل `<code>.base.ts` يقابله
   `<code>.web.ts` ورمزُه صالح، ويجب أن يطابق `CODES` مطابقةً تامّة في الاتجاهين. */
const onDisk = fs
  .readdirSync(path.join(SRC, "i18n/locales"))
  .filter((n) => n.endsWith(".base.ts"))
  .map((n) => n.slice(0, -".base.ts".length))
  .filter((code) => {
    if (!fs.existsSync(path.join(SRC, "i18n/locales", code + ".web.ts"))) return false;
    try {
      return Boolean(new Intl.Locale(code).language);
    } catch {
      return false;
    }
  });
const scopeGap = [
  ...onDisk.filter((c) => !CODES.includes(c)).map((c) => "على القرص ولا يفحصه أحد · on disk, unchecked: " + c),
  ...CODES.filter((c) => !onDisk.includes(c)).map((c) => "في CODES ولا ملفّ له · in CODES, no file: " + c),
];
info("ملفّات لغة على القرص · locale files on disk: " + onDisk.join(",") + " ← CODES: " + CODES.join(","));
if (scopeGap.length) {
  bad(
    "نطاق الفحص لا يطابق المجلّد — لغةٌ خارج CODES تخرج من الفحوص العشرة كلّها · " +
      "the checked set does not match the directory",
    scopeGap,
    true
  );
} else ok("نطاق الفحص هو المجلّد بعينه · the checked set is exactly the directory");

const scripts = {};
for (const code of CODES) scripts[code] = scriptOf(code);
info("خطوط مشتقّة من رموز اللغات · scripts derived from the locale tags: " +
  CODES.map((c) => c + "=" + scripts[c] + (isDiagnostic(scripts[c]) ? " (شاهد)" : " (خطّ المعرّفات)")).join(" · "));

const witnesses = {};
const noWitness = [];
for (const code of CODES) {
  witnesses[code] = witnessesOf(code, CODES);
  info("  شاهد " + code + " · witness: " + (witnesses[code].join(",") || "—"));
  if (witnesses[code].length === 0) noWitness.push(code);
}
if (noWitness.length) {
  bad(
    "لغة بلا شاهد — القاعدة (ب) لا تراها إطلاقاً، ولا تمرّ صامتة · " +
      "a locale with no witness is invisible to rule (b) and must not pass in silence",
    noWitness,
    true
  );
}

const mojibake = [];
const wrongScript = [];
const copiedSource = [];
const unlicensedForeign = [];
const junk = [];
let valuesInspected = 0;
let witnessedComparisons = 0;
let foreignRunsSeen = 0;

for (const code of CODES) {
  for (const [key, value] of Object.entries(messages[code])) {
    const where = origin[code][key] ?? "?";
    const texts = valueTexts(value);
    const witness = witnesses[code].find((w) => {
      const wv = messages[w][key];
      return wv !== undefined && valueTexts(wv).some((t) => hasOwnScript(t, w));
    });

    for (const text of texts) {
      valuesInspected++;

      for (const hit of mojibakeRuns(text)) {
        mojibake.push(
          where + " ← " + key + "  «" + hit.run.slice(0, 34) + "» = بايتات · bytes of «" + hit.decoded.slice(0, 34) + "»"
        );
      }

      for (const ch of junkChars(text)) {
        junk.push(
          where + " ← " + key + "  U+" + ch.code.toString(16).toUpperCase().padStart(4, "0") +
            " عند المحرف · at offset " + ch.at
        );
      }

      /* الإذن المغلق: مقطعٌ أجنبيّ غير آليّ يُصدَّق بلغةٍ أخرى، أو يسقط. */
      for (const run of foreignRuns(text, code)) {
        const seenElsewhere = CODES.some(
          (other) => other !== code && valueTexts(messages[other][key]).some((t) => t.includes(run))
        );
        foreignRunsSeen++;
        if (!seenElsewhere) {
          unlicensedForeign.push(
            where + " ← " + key + "  مقطع «" + run.slice(0, 30) + "» لا يظهر في أي لغة أخرى تحت المفتاح نفسه"
          );
        }
      }

      if (!witness) continue;
      if (census(text, code).letters === 0) continue;
      witnessedComparisons++;
      if (!hasOwnScript(text, code)) {
        wrongScript.push(
          where + " ← " + key + "  ليس فيه حرف واحد بخطّ " + scripts[code] +
            " (شاهده " + witness + ") · «" + text.slice(0, 46) + "»"
        );
      }
    }

    if (code === SOURCE) continue;
    const src = messages[SOURCE][key];
    if (typeof value !== "string" || typeof src !== "string") continue;
    if (value !== src) continue;
    if (!hasOwnScript(src, SOURCE) || proseWords(src) < 3) continue;
    copiedSource.push(where + " ← " + key + "  يطابق العربية حرفاً بحرف · «" + src.slice(0, 46) + "»");
  }
}

/* ═══ شواهد إيجابية · positive controls (ADR-0056) ══════════════════════
   ‏كلُّ شاهدٍ هنا يزرع العطل **بآليّته نفسها** — `mangle` هو الترميز UTF-8 ثم
   القراءة Latin-1 حرفياً — لا بنصٍّ مشوَّهٍ منسوخ. فلو تغيّر شكل التشويه غداً
   تغيّرت الشواهد معه، ولا يبقى شاهدٌ يصدّق كاشفاً عمي. والنصّ المزروع يُؤخَذ
   **من ملفّات اللغة نفسها** فلا يُكتب في هذا الملفّ حرفُ لغةٍ واحد. */
const probeOf = (code) => {
  for (const v of Object.values(messages[code])) {
    for (const t of valueTexts(v)) {
      if (hasOwnScript(t, code) && census(t, code).letters >= 6) return t;
    }
  }
  return null;
};
for (const code of CODES) {
  if (!isDiagnostic(scripts[code])) continue; /* خطّ المعرّفات ASCII، ولا يتشوّه شكلُه */
  const probe = probeOf(code);
  selfTest(
    code + ": التشويه يُكشف ويُفكّ إلى أصله",
    probe !== null && mojibakeRuns(mangle(probe)).some((h) => h.decoded === probe)
  );
  selfTest(code + ": المشوَّه يفقد خطّ لغته", probe !== null && !hasOwnScript(mangle(probe), code));
  selfTest(code + ": والسليم لا يفقده", probe !== null && hasOwnScript(probe, code));
}
selfTest("لا إنذار على نصّ سليم", mojibakeRuns("Journal Voucher \u00b7 \u00abPDF\u00bb \u2014 1,250.00").length === 0);
/* أن **يُستبعَد** خطُّ المعرّفات من الشهادة يُقاس على اللغات الحقيقية، لا يُدَّعى. */
const identifierLocales = CODES.filter((c) => !isDiagnostic(scripts[c]));
selfTest(
  "خطّ المعرّفات مُستبعَد من شهادة كل لغة",
  identifierLocales.length > 0 &&
    identifierLocales.every((c) => CODES.every((other) => !witnesses[other].includes(c)))
);
/* والشاهد الحاسم على القاعدة (د): تشويهٌ بترميزٍ **لا تعرفه** القاعدة (أ).
   ‏`koi8-r` يرفع بايتات فوق U+00FF فينكسر مقطع الفكّ ويفلت من (أ) — مقيس —
   ولا يفلت من (د) لأنها لا تسأل عن الترميز بل عن التصديق. */
const koi8 = mangleUnder("koi8-r", probeOf("hi") ?? "");
selfTest("تشويهٌ بترميزٍ آخر يفلت من فكّ الترميز", mojibakeRuns(koi8).length === 0);
selfTest("ولا يفلت من الإذن المغلق", foreignRuns(koi8, "hi").length > 0);
selfTest("والنصّ السليم لا مقطع أجنبيّ فيه", foreignRuns(probeOf("hi") ?? "", "hi").length === 0);
selfTest("والرمز الآلي ASCII لا يُعدّ أجنبياً", foreignRuns("PDF SAR BANK-0001 INV-2026-0587", "hi").length === 0);
/* وبايتات UTF-16 تُنتج محارف تحكّم لا حروفاً، فتلتقطها (هـ) وحدها. */
const utf16 = [...(probeOf("hi") ?? "")].map((c) => String.fromCharCode(c.charCodeAt(0) & 0xff, c.charCodeAt(0) >> 8)).join("");
selfTest("بايتات UTF-16 تُلتقَط بفئة المحرف", junkChars(utf16).length > 0);
selfTest("ولا محرف حشوٍ في نصّ سليم", junkChars(probeOf("ar") ?? "").length === 0);

mustScan("نصوص مفحوصة · values inspected", valuesInspected, 4000);
mustScan("مقارنات بشاهد · witnessed comparisons", witnessedComparisons, 3000);
info("مقاطع أجنبية غير آلية · non-machine foreign runs: " + foreignRunsSeen);

if (mojibake.length) {
  bad(
    "قيمة مشوّهة الترميز — رُمِّزت UTF-8 وقُرئت Latin-1 · " +
      "a value that is UTF-8 bytes read as Latin-1",
    mojibake,
    true
  );
} else ok("لا قيمة مشوّهة الترميز في أي لغة · no value is another text's bytes");

if (wrongScript.length) {
  bad(
    "قيمة ليست بخطّ لغتها — والمفتاح مفتاح نثر بشهادة لغة أخرى · " +
      "a value not in its locale's script, where a witness locale proves the key is prose",
    wrongScript,
    true
  );
} else ok("كل قيمة نثرٍ مكتوبة بخطّ لغتها · every prose value is written in its locale's script");

if (junk.length) {
  bad(
    "محرف لا يُرسم ولا يعني داخل قيمة — تحكّمٌ أو بديلٌ أو غير مخصَّص · " +
      "a control, surrogate, private-use or unassigned character inside a value",
    junk,
    true
  );
} else ok("لا محرف حشوٍ في أي قيمة · no junk character in any value");

if (unlicensedForeign.length) {
  bad(
    "مقطع أجنبيّ غير آليّ لا تصدّقه لغةٌ أخرى — والإذن مغلق: اكتب المقطع في " +
      "المصدر إن كان رمزاً مقصوداً · an unlicensed non-machine foreign run",
    unlicensedForeign,
    true
  );
} else ok("كل مقطع أجنبيّ غير آليّ مصدَّقٌ بلغةٍ أخرى · every non-machine foreign run is corroborated");

if (copiedSource.length) {
  bad(
    "قيمة منسوخة عن المصدر حرفاً بحرف — نسخٌ لا ترجمة · " +
      "a value copied verbatim from the source is not a translation",
    copiedSource,
    true
  );
} else ok("لا عبارة (ثلاث كلمات فأكثر) منسوخة عن العربية · no phrase copied verbatim from Arabic");

/* ═════════════════════════════ الخلاصة ═════════════════════════════════ */
head("الخلاصة · summary");
out.push("  مخالفات حاكمة · fatal:   " + fatal);
out.push("  دين معلَن · declared debt: " + debt + " (السقف · ceiling " + DECLARED_DEBT.ceiling + ")");
out.push("  ملاحظات · warnings:      " + warn);
out.push("");
if (!QUIET || fatal) console.log(out.join("\n"));
process.exit(fatal ? 1 : 0);
