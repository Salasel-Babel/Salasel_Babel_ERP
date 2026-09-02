#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   الأرقام الجدولية — حارسٌ يُمسك الخاصّية، لا الأصناف
   Tabular numerals — a guard keyed on the CSS property, never on a class list
   ───────────────────────────────────────────────────────────────────────────
   ‏**لماذا هذا الملفّ موجود.** صفحة العرض تَعِد بالحرف: «كل رقم في الواجهة يحمل
   ‏tabular-nums بلا استثناء». وقِيس أنّ الوعد كان دعوى: خمسون تصريحاً تكتب
   القيمة **حرفيةً** في أحد عشر ملفّاً، فتبديلُ رمزٍ وقت التشغيل حرّك ما يشير
   إليه وحده وترك الباقي. ‏(traps.md#fakh-a-remedy-that-is-a-list-is-not-a-remedy)

   ‏**والعلاج ليس قائمةَ أصناف.** مجموعةُ الأصناف التي تحمل أرقاماً **مفتوحة**
   وتكبر كل جولة؛ ومجموعةُ **الخصائص** التي تحكم رسم الأرقام في CSS **مغلقة
   وصغيرة**: ‏`font-variant-numeric` و`font-variant` و`font-feature-settings`،
   ومعها المختصر `font` لأنه **يُصفّر** الخاصّية الأولى بصمت. فالمسح يبدأ من
   أسماء الخصائص الأربعة — لا من صنفٍ يذكره أحد — والصنفُ الذي يُضاف الشهر
   القادم يُفحَص بلا أن يُسجَّل هنا اسمُه.

   ‏**والقطب مقلوب عمداً.** لا قائمةَ محظورات يبدأ الجديد خارجها، بل قائمةُ
   **مسموحات** ضيّقة: قيمةُ أي تصريحٍ من الأربعة يجب أن تكون `var(--font-numeric)`
   أو `var(--font-numeric-off)` حرفاً بحرف — وما لم يُصنَّف يسقط. وحتى القيمة
   الحرفية نفسها (`tabular-nums` وأخواتها ووسوم OpenType) ممنوعةٌ في كل قيمة CSS
   في المستودع إلا في **تعريف الرمزين**. فمقولة «تبديلُ الرمز يبدّل كل سطحٍ رقمي»
   تصير صحيحةً **بالبناء**، لأن لا قاعدةَ أخرى يُسمح لها بالتعبير عن القيمة.

   ‏**ونطاق المسح مُشتَقّ لا معدود.** كلّ ملفٍّ في `web/` أو `design/` داخل النطاق.
   وما وُجد خارجهما وفيه تصريحُ أرقام يجب أن يكون **مُصنَّفاً** في `FROZEN_PROTOTYPES`
   أدناه — نماذجُ أوّلية مجمّدة لا تُحمّل طبقة الرموز أصلاً. وملفٌّ جديدٌ خارج
   النطاق يحمل تصريحاً **يُفشِل** حتى يُصنَّف: المجموعة مغلقة، ومن يدخلها يُعلن.

   الاستعمال:
     node scripts/numerals.mjs            # من web/ — يخرج 0 أو 1
     node scripts/numerals.mjs --json     # الحصيلة كاملةً لمن يقرأها آلياً
   ويُستورَد من `web/tests/design-system.test.tsx`، فيُشغَّل في البوّابة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readdirSync, readFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

/** الخصائص الأربع التي تحكم رسم الأرقام. مجموعة **مغلقة** — وهذا كلّ الأمر. */
export const NUMERAL_PROPERTIES = Object.freeze([
  "font-variant-numeric",
  "font-variant",
  "font-feature-settings",
  /* المختصر `font` ليس زينة: `font:700 14px/1.2 sans-serif` **يُعيد**
     font-variant-numeric إلى normal بصمت، فيقتل الأرقام الجدولية على العنصر
     كلِّه بلا أن يُكتب اسمُها. ولذلك لا يُسمح منه إلا ما يمرّر الوراثة. */
  "font",
]);

/**
 * ‏**والخاصّية الخامسة: `font-family`.** وهي ليست إضافةً إلى قائمة، بل تصحيحُ
 * خطأٍ في تعريف المجموعة المغلقة نفسها. الأربع أعلاه تحمل **الطلب**؛ ومن يرسم
 * الرقم هو **المِحرف**. و`tabular-nums` طلبٌ لا أمر: المِحرف الذي لا يحمل
 * الوجه الجدولي يتجاهله بصمت، ووجهان مختلفان على سطحين رقميّين يرسمان الرقم
 * نفسه بعرضين مختلفين **و`getComputedStyle().fontVariantNumeric` يقول
 * `tabular-nums` في كليهما**. مقيس: إلحاق
 * `tbody tr:nth-child(2n) .acct-code{font-family:"DejaVu Sans",sans-serif}`
 * كان يُبقي هذا الفحص وفحصَ العرض أخضرين بينما رمزُ الحساب السباعيّ نفسه
 * يُرسم **58.81px و68.20px**.
 *
 * ‏**وحكمُها مشروطٌ لا مُطلَق، وهذا هو الفرق كلُّه:** الطلب يُحكَم أينما كُتب،
 * لأن كتابته إعلانٌ عن نيّةٍ رقمية. أمّا `font-family` فتُكتب في كل مكان ولا
 * علاقة لأكثرها بالأرقام. فتُحكَم **حيث تبلغ سطحاً رقمياً**، والسطحُ الرقميّ
 * **مشتقٌّ لا معدود**: كل صنفٍ يظهر في مُحدِّد قاعدةٍ تُعلن خاصّيةً من الأربع.
 * فالصنف الذي يُضاف الشهر القادم يدخل الحكم بمجرّد أن يُعلن أرقامه.
 */
export const NUMERAL_FACE_PROPERTY = "font-family";

/** الرمزان — وهما الموضع الوحيد الذي يجوز أن تُكتب فيه قيمةٌ حرفية. */
export const NUMERAL_TOKENS = Object.freeze(["--font-numeric", "--font-numeric-off"]);

/** رمز **المِحرف** — غير رمز الطلب، ولا يجوز خلطهما (فخ-135). */
export const NUMERAL_FACE_TOKEN = "--font-numeric-face";

/** المسموح لـ`font-family` على سطحٍ رقمي. مجموعةٌ من عنصرٍ واحد عمداً. */
export const ALLOWED_FACES = Object.freeze(["var(--font-numeric-face)"]);

/**
 * جذورُ الوراثة. عندها **لا يُطبَّق حكمُ الرمز** بل حكمٌ أقوى منه: وجهُ الجذر
 * يجب أن يكون **هو نفسه** قيمةَ `--font-numeric-face` حرفاً بحرف. وهذا ليس
 * إعفاءً: الجذر هو من يُورِّث الوجه لكل سطحٍ رقميّ لا يختار وجهه، فمساواتُه
 * بالرمز هي **بالضبط** ما يجعل «وجهٌ واحد للرقم» صحيحاً بالبناء. ولو كُتب عنده
 * `var(--font-numeric-face)` لصار كلُّ نثرٍ في المنتج محكوماً برمز الأرقام،
 * وذلك اسمٌ يكذب على ما يفعله.
 */
export const INHERITANCE_ROOTS = Object.freeze(["html", "body", ":root", "html,body", "body,html"]);

/** المسموح لقيم الخصائص الثلاث الحقيقية. لا `var(--x, fallback)`: الاحتياط بابٌ خلفي. */
export const ALLOWED_VALUES = Object.freeze(["var(--font-numeric)", "var(--font-numeric-off)"]);

/** المسموح للمختصر `font`: ما يُبقي الوراثة سليمة ولا يُصفّر شيئاً. */
export const ALLOWED_FONT_SHORTHAND = Object.freeze(["inherit", "unset", "revert", "revert-layer"]);

/** كلماتُ CSS التي ترسم الأرقام — تُطابَق **داخل قيم التصريحات وحدها**، لا في
    المُحدِّدات: صنفٌ اسمه `.taxval.zero` ليس وسم OpenType. */
export const NUMERAL_KEYWORDS = Object.freeze([
  "tabular-nums", "proportional-nums", "oldstyle-nums", "lining-nums",
  "slashed-zero", "ordinal", "diagonal-fractions", "stacked-fractions",
]);

/** وسومُ OpenType — لا تُكتب في CSS إلا **بين علامتَي اقتباس**، فتُطابَق كذلك. */
export const NUMERAL_FEATURE_TAGS = Object.freeze([
  "tnum", "pnum", "onum", "lnum", "zero", "ordn", "frac", "afrc",
]);

/** الاثنان معاً — لمن يقرأ الحصيلة. */
export const NUMERAL_LITERALS = Object.freeze([...NUMERAL_KEYWORDS, ...NUMERAL_FEATURE_TAGS]);

/** الأشكال المكافئة في الشيفرة (خصائص النمط السطري بصيغة الجمل). */
export const NUMERAL_STYLE_KEYS = Object.freeze([
  "fontVariantNumeric", "fontVariant", "fontFeatureSettings",
]);

/**
 * ما خرج من النطاق **بتصنيفٍ صريح**: نماذج أوّلية مجمّدة لا تُحمّل
 * `tokens.css` إطلاقاً، فلا تستطيع أن تُحلّ `var(--font-numeric)` أصلاً.
 * وهي ليست استثناءً مفتوحاً: أي ملفٍّ آخر خارج `web/` و`design/` يحمل تصريح
 * أرقام **يُفشِل الفحص** حتى يُصنَّف هنا أو يُنقل إلى النطاق.
 */
export const FROZEN_PROTOTYPES = Object.freeze([
  "demo/vertical-slice/wwwroot/index.html",
  "docs/prototypes/journal-entry/index.html",
]);

/** جذور النطاق — التطبيق المشحون والمعرض الذي نُقل عنه. */
const IN_SCOPE_ROOTS = Object.freeze(["web", "design"]);

/**
 * ‏**نطاق مسح الشيفرة أضيق من نطاق مسح الأوراق، ولسببٍ لا لراحة:** الأوراق
 * تُحمَّل كما هي، أمّا الشيفرة فما يبلغ المتصفّح منها هو ما تحزمه أداة البناء —
 * أي `web/src/` وما يستورده، و`design/` للمعرض. وملفّات الاختبار **لا تُشحن**؛
 * وهي فوق ذلك المواضع التي **تقيس** هذه الخصائص، فلا بدّ أن تُسمّيها بحرفها:
 * حارسٌ يمنع اختبارَه من كتابة القيمة التي يمنعها يُدفع صاحبُه إلى تمويهها،
 * وتمويهُ الشاهد أوّلُ خطوةٍ في تعطيل الحارس.
 */
const CODE_SCOPE_ROOTS = Object.freeze(["web/src", "design"]);
const inCodeScope = (rel) =>
  CODE_SCOPE_ROOTS.some((root) => rel === root || rel.startsWith(root + "/"));

/** ما لا يُمسح أبداً: مخرجات بناءٍ ونسخُ اعتماديات. */
const SKIP_DIRECTORIES = new Set([
  "node_modules", "dist", "build", "coverage", "test-results",
  "playwright-report", ".git", "bin", "obj", "TestResults",
]);

const CSS_EXTENSIONS = new Set([".css"]);
const HTML_EXTENSIONS = new Set([".html", ".htm"]);
const CODE_EXTENSIONS = new Set([".ts", ".tsx", ".js", ".jsx", ".mjs"]);

/** ‏يستبدل كل تعليق بفراغٍ بنفس الطول — فتبقى أرقام الأسطر والمواضع صحيحة. */
function blankComments(text) {
  return text.replace(/\/\*[\s\S]*?\*\//g, (m) => m.replace(/[^\n]/g, " "));
}

/** رقم السطر (١-أساس) لموضعٍ في نصّ. */
function lineOf(text, index) {
  let line = 1;
  for (let i = 0; i < index && i < text.length; i += 1) if (text[i] === "\n") line += 1;
  return line;
}

/** يُطبّع قيمة تصريح: فراغٌ واحد، بلا `!important`، بلا فراغ داخل var(). */
function normalizeValue(raw) {
  return raw
    .replace(/!\s*important/gi, "")
    .replace(/\s+/g, " ")
    .replace(/\(\s+/g, "(")
    .replace(/\s+\)/g, ")")
    .replace(/\s*,\s*/g, ",")
    .trim();
}

/**
 * ‏يمشي نصّ CSS **تصريحاً تصريحاً**، لا بمطابقة نمطٍ على النصّ كلّه.
 * والفرق ليس أناقة: المطابقة الساذجة تخلط المُحدِّد بالقيمة، فتقرأ `.taxval.zero`
 * وسمَ OpenType، وتقرأ `a:hover` تصريحاً. المشي على الأقواس يفصل الاثنين.
 * ‏`bare` لقوائم التصريحات بلا أقواس — أي سمة `style="…"` في HTML.
 */
function* declarationsOf(source, { bare = false } = {}) {
  let depth = bare ? 1 : 0;
  let buffer = "";
  let bufferStart = 0;
  let quote = null;
  /* ‏**مكدّس المُحدِّدات.** التصريح وحده لا يكفي بعد أن صار `font-family` من
     الخصائص الحاكمة: الطلب (`font-variant-numeric`) قاعدةٌ عالمية تُحكم أينما
     كُتبت، أمّا المِحرف فحكمُه **مشروطٌ بمن يصله**. فلا بدّ أن يعرف الماسحُ
     المُحدِّد الذي يعيش التصريحُ تحته. وقواعد `@` تُحفظ في المكدّس ولا تُخلط
     بالمحدِّد، فـ`@media` ليست مُحدِّداً وقاعدةٌ داخلها تحمل محدِّدها هي. */
  const stack = [];
  const currentSelector = () => {
    for (let i = stack.length - 1; i >= 0; i -= 1) {
      if (!stack[i].startsWith("@")) return stack[i];
    }
    return "";
  };
  const flush = function* (endIndex) {
    if (!buffer.trim()) return;
    const colon = buffer.indexOf(":");
    if (colon < 0) return;
    const property = buffer.slice(0, colon).trim().toLowerCase();
    const value = buffer.slice(colon + 1);
    yield {
      property,
      value,
      selector: currentSelector(),
      propertyIndex: bufferStart + (buffer.length - buffer.trimStart().length),
      valueIndex: bufferStart + colon + 1,
      endIndex,
    };
  };
  if (bare) stack.push("");
  for (let i = 0; i < source.length; i += 1) {
    const c = source[i];
    if (quote) {
      buffer += c;
      if (c === "\\") { buffer += source[i + 1] ?? ""; i += 1; continue; }
      if (c === quote) quote = null;
      continue;
    }
    if (c === '"' || c === "'") { if (buffer === "") bufferStart = i; quote = c; buffer += c; continue; }
    if (c === "{") {
      stack.push(buffer.trim().replace(/\s+/g, " "));
      depth += 1; buffer = ""; bufferStart = i + 1; continue;
    }
    if (c === "}") {
      yield* flush(i);
      stack.pop();
      depth = Math.max(0, depth - 1); buffer = ""; bufferStart = i + 1; continue;
    }
    if (c === ";") { if (depth > 0) yield* flush(i); buffer = ""; bufferStart = i + 1; continue; }
    if (depth > 0) { if (buffer === "") bufferStart = i; buffer += c; }
    else { if (buffer === "" && !/\s/.test(c)) bufferStart = i; buffer += c; }
  }
  /* ‏والدفقة الأخيرة **مشروطةٌ بالعمق**: بعد أن صار المُحدِّد يُجمَّع عند العمق
     صفر، صار في المُخزَّن عند نهاية الملفّ نصُّ مُحدِّدٍ لا تصريح — و`a:hover`
     فيه نقطتان، فيُقرأ تصريحاً وهماً. */
  if (depth > 0) yield* flush(source.length);
}

/**
 * ‏يمسح نصّ CSS واحداً. `origin` هو النصّ الكامل للملفّ و`offset` موضع بداية
 * هذا المقطع فيه — كي يخرج رقم السطر صحيحاً من داخل `<style>` أو من سمة `style`.
 */
export function scanCssText(cssText, { file = "<نصّ>", origin = null, offset = 0, bare = false } = {}) {
  const whole = origin ?? cssText;
  const source = blankComments(cssText);
  const declarations = [];
  const violations = [];
  const tokenDefinitions = [];
  /* المِحرف يُجمَع ولا يُحكَم هنا: حكمُه يحتاج **كلَّ** المستودع، لأن القاعدة
     التي تُعلن الأرقام قد تكون في ملفٍّ والقاعدة التي تختار الوجه في آخر —
     وهو بالضبط شكل الطفرة التي هزمت النسخة السابقة. */
  const faceDeclarations = [];
  const numeralSelectors = [];
  const faceTokenDefinitions = [];
  const at = (index) => lineOf(whole, offset + index);

  const keywordRe = new RegExp("(?<![-\\w])(" + NUMERAL_KEYWORDS.join("|") + ")(?![-\\w])", "gi");
  const tagRe = new RegExp("[\"'](" + NUMERAL_FEATURE_TAGS.join("|") + ")[\"']", "gi");

  for (const d of declarationsOf(source, { bare })) {
    const line = at(d.propertyIndex);
    const value = normalizeValue(d.value);
    const isToken = NUMERAL_TOKENS.includes(d.property);

    if (isToken) {
      tokenDefinitions.push({ file, line, token: d.property, value });
      continue; /* الموضع الوحيد الذي تجوز فيه القيمة الحرفية. */
    }

    if (d.property === NUMERAL_FACE_TOKEN) {
      faceTokenDefinitions.push({ file, line, token: d.property, value, selector: d.selector });
      continue;
    }

    /* ⓪ المِحرف — يُجمَع بمُحدِّده، ويُحكَم في `scanRepository` بعد أن تُعرف
       الأسطح الرقمية كلُّها. */
    if (d.property === NUMERAL_FACE_PROPERTY) {
      faceDeclarations.push({ file, line, selector: d.selector, value });
    }

    /* ① الخاصّية نفسها — المجموعة المغلقة. */
    if (NUMERAL_PROPERTIES.includes(d.property)) {
      /* المُحدِّد يُسجَّل هنا: هو تعريفُ «سطحٍ رقميّ» ومنه تُشتقّ الأصناف. */
      if (d.selector) numeralSelectors.push({ file, line, selector: d.selector });
      const record = { file, line, property: d.property, value };
      if (d.property === "font") {
        if (!ALLOWED_FONT_SHORTHAND.includes(value.toLowerCase())) {
          violations.push({
            ...record,
            kind: "font-shorthand-resets-numerals",
            why:
              "المختصر `font` يُصفّر font-variant-numeric بصمت. اكتب الخصائص مفردةً، " +
              "أو استعمل `font:inherit`.",
          });
        }
      } else {
        declarations.push(record);
        if (!ALLOWED_VALUES.includes(value)) {
          violations.push({
            ...record,
            kind: "numeral-value-is-not-the-token",
            why:
              "قيمة خاصّيةٍ حاكمةٍ للأرقام يجب أن تكون " +
              ALLOWED_VALUES.map((v) => "`" + v + "`").join(" أو ") +
              " حرفاً بحرف — لا قيمةً حرفية ولا `var()` باحتياط.",
          });
        }
      }
    }

    /* ② القيمة الحرفية — ممنوعة في **قيمة أي تصريح** مهما كانت خاصّيته، كي لا
       يُهرَّب المعنى عبر رمزٍ ثالثٍ يُعرَّف في مكوّن ثم يُشار إليه. */
    for (const re of [keywordRe, tagRe]) {
      re.lastIndex = 0;
      for (let m = re.exec(d.value); m; m = re.exec(d.value)) {
        violations.push({
          file,
          line: at(d.valueIndex + m.index),
          property: d.property,
          value: m[1],
          kind: "numeral-literal-outside-the-token",
          why:
            "القيمة الحرفية `" + m[1] + "` لا تُكتب إلا في تعريف " +
            NUMERAL_TOKENS.join(" أو ") + " داخل `tokens.css`.",
        });
      }
    }
  }

  return { declarations, violations, tokenDefinitions, faceDeclarations, numeralSelectors, faceTokenDefinitions };
}

/** ‏يستخرج مقاطع CSS من ملفّ HTML: كتل `<style>` وسمات `style="…"`. */
function cssSpansOfHtml(html) {
  const spans = [];
  const styleRe = /<style\b[^>]*>([\s\S]*?)<\/style>/gi;
  for (let m = styleRe.exec(html); m; m = styleRe.exec(html)) {
    spans.push({ text: m[1], offset: m.index + m[0].indexOf(m[1]) });
  }
  const attrRe = /\sstyle\s*=\s*"([^"]*)"/gi;
  for (let m = attrRe.exec(html); m; m = attrRe.exec(html)) {
    spans.push({ text: m[1], offset: m.index + m[0].indexOf(m[1]), bare: true });
  }
  return spans;
}

/** ‏يمسح شيفرة TS/JS بحثاً عن ضبطٍ سطريٍّ للخصائص نفسها بصيغة الجمل. */
export function scanCodeText(code, { file = "<شيفرة>" } = {}) {
  /* التعليقات تُفرَّغ أوّلاً — وإلا صار هذا الملفّ نفسه، وهو يشرح ما يمنعه،
     أوّلَ من يخالفه. والتفريغ يحفظ الأسطر فتبقى المواضع صحيحة. */
  code = blankComments(code).replace(/(^|[^:\\])\/\/[^\n]*/g, (m, lead) => lead + " ".repeat(m.length - lead.length));
  const violations = [];
  const declarations = [];
  const keys = NUMERAL_STYLE_KEYS.join("|");
  const shapes = [
    /* كائن نمط: { fontVariantNumeric: "…" } — والقراءة `s.fontVariantNumeric` مستثناة
       باللحاق الخلفي، لأنها لا تُتبَع بنقطتين. */
    { re: new RegExp("(?<![\\w$.])(" + keys + ")\\s*:\\s*([^,;}\\n]+)", "g"), group: 2 },
    /* إسناد مباشر: el.style.fontVariantNumeric = "…" */
    { re: new RegExp("\\.(" + keys + ")\\s*=\\s*([^;\\n]+)", "g"), group: 2 },
    /* setProperty("font-variant-numeric", "…") */
    {
      re: new RegExp(
        "setProperty\\s*\\(\\s*[\"'](" + NUMERAL_PROPERTIES.slice(0, 3).join("|") + ")[\"']\\s*,\\s*([^)]*)\\)",
        "g"
      ),
      group: 2,
    },
  ];
  /* والقيمة الحرفية ممنوعةٌ في الشيفرة المشحونة **مهما كان شكل كتابتها**: لا في
     كائن نمط، ولا في نصٍّ يُركَّب ثم يُسنَد إلى سمة `style`، ولا في قالبٍ يُحقن.
     وقِيس على هذا الفرع أن `web/src/` و`design/` **خاليان منها تماماً** بعد تفريغ
     التعليقات — فالقاعدة لا تُكلّف أحداً شيئاً، وتُغلق الطريق الذي تعجز عنه
     مطابقةُ أسماء الخصائص: `el.setAttribute("style", "font-variant-numeric:" + v)`. */
  const literalRe = new RegExp("(?<![-\\w])(" + NUMERAL_KEYWORDS.join("|") + ")(?![-\\w])", "gi");
  for (let m = literalRe.exec(code); m; m = literalRe.exec(code)) {
    violations.push({
      file,
      line: lineOf(code, m.index),
      property: "(نصّ في الشيفرة)",
      value: m[1],
      kind: "numeral-literal-in-shipped-code",
      why:
        "القيمة الحرفية `" + m[1] + "` لا تُكتب في شيفرةٍ تُشحن — لا في كائن نمط ولا " +
        "في نصٍّ يُركَّب. الرمز وحده، ومن `tokens.css` وحده.",
    });
  }

  for (const shape of shapes) {
    for (let m = shape.re.exec(code); m; m = shape.re.exec(code)) {
      const raw = m[shape.group].trim().replace(/^["'`]|["'`]$/g, "").trim();
      const line = lineOf(code, m.index);
      const record = { file, line, property: m[1], value: raw };
      declarations.push(record);
      if (!ALLOWED_VALUES.includes(normalizeValue(raw))) {
        violations.push({
          ...record,
          kind: "inline-numeral-style-is-not-the-token",
          why: "ضبطُ خاصّية أرقامٍ من الشيفرة يجب أن يمرّ من الرمز نفسه لا من قيمةٍ حرفية.",
        });
      }
    }
  }
  return { declarations, violations };
}

/** يمشي الشجرة ويُعيد كل ملفّ ذي امتداد يهمّنا. */
function* walk(dir) {
  let entries;
  try {
    entries = readdirSync(dir, { withFileTypes: true });
  } catch {
    return;
  }
  for (const entry of entries) {
    if (entry.name.startsWith(".") && entry.name !== ".github") continue;
    const full = path.join(dir, entry.name);
    if (entry.isDirectory()) {
      if (SKIP_DIRECTORIES.has(entry.name)) continue;
      yield* walk(full);
    } else if (entry.isFile()) {
      const ext = path.extname(entry.name).toLowerCase();
      if (CSS_EXTENSIONS.has(ext) || HTML_EXTENSIONS.has(ext) || CODE_EXTENSIONS.has(ext)) {
        yield full;
      }
    }
  }
}

const inScope = (rel) => IN_SCOPE_ROOTS.some((root) => rel === root || rel.startsWith(root + "/"));

/**
 * يمسح المستودع كلّه. النطاق مُشتَقّ من موضع الملفّ، والخارجُ عنه يجب أن يكون
 * مُصنَّفاً — فلا يُهرَّب سطحٌ رقميّ جديد بوضعه خارج الشجرتين.
 */
export function scanRepository(root) {
  const declarations = [];
  const violations = [];
  const tokenDefinitions = [];
  const scannedFiles = [];
  const unclassified = [];
  const faceDeclarations = [];
  const numeralSelectors = [];
  const faceTokenDefinitions = [];

  for (const full of walk(root)) {
    const rel = path.relative(root, full).split(path.sep).join("/");
    const ext = path.extname(full).toLowerCase();
    let text;
    try {
      text = readFileSync(full, "utf8");
    } catch {
      continue;
    }
    const outside = !inScope(rel);

    if (CSS_EXTENSIONS.has(ext) || HTML_EXTENSIONS.has(ext)) {
      const spans = CSS_EXTENSIONS.has(ext)
        ? [{ text, offset: 0 }]
        : cssSpansOfHtml(text);
      let found = 0;
      const fileViolations = [];
      for (const span of spans) {
        const r = scanCssText(span.text, {
          file: rel, origin: text, offset: span.offset, bare: span.bare === true,
        });
        found += r.declarations.length + r.violations.length;
        if (outside) {
          fileViolations.push(...r.violations);
          continue;
        }
        declarations.push(...r.declarations);
        violations.push(...r.violations);
        tokenDefinitions.push(...r.tokenDefinitions);
        faceDeclarations.push(...r.faceDeclarations);
        numeralSelectors.push(...r.numeralSelectors);
        faceTokenDefinitions.push(...r.faceTokenDefinitions);
      }
      if (!outside) scannedFiles.push(rel);
      else if (found > 0 && !FROZEN_PROTOTYPES.includes(rel)) {
        unclassified.push(rel);
        violations.push({
          file: rel,
          line: 1,
          property: "(ملفّ خارج النطاق)",
          value: String(found) + " تصريحاً",
          kind: "numeral-surface-outside-any-declared-scope",
          why:
            "ملفٌّ خارج `web/` و`design/` يحمل تصريحَ أرقام. إمّا أن ينتقل إلى النطاق " +
            "ويستعمل الرمز، وإمّا أن يُصنَّف صراحةً في FROZEN_PROTOTYPES مع سبب تجميده.",
        });
      }
      continue;
    }

    if (CODE_EXTENSIONS.has(ext) && inCodeScope(rel)) {
      const r = scanCodeText(text, { file: rel });
      declarations.push(...r.declarations);
      violations.push(...r.violations);
      if (r.declarations.length > 0) scannedFiles.push(rel);
    }
  }

  /* ── حكمُ المِحرف: بعد أن صار المستودع كلُّه معروفاً ───────────────────── */
  const numericClasses = classesOf(numeralSelectors.map((s) => s.selector));
  violations.push(...judgeFaces({ faceDeclarations, numericClasses, faceTokenDefinitions }));

  return {
    declarations, violations, tokenDefinitions, scannedFiles, unclassified,
    faceDeclarations, numericClasses: [...numericClasses].sort(), faceTokenDefinitions,
  };
}

/** الأصنافُ المذكورة في مجموعة مُحدِّدات — مشتقّةٌ بالمطابقة لا مكتوبة. */
export function classesOf(selectors) {
  const classes = new Set();
  for (const selector of selectors) {
    for (const m of selector.matchAll(/\.(-?[A-Za-z_][\w-]*)/g)) classes.add(m[1]);
  }
  return classes;
}

/** هل يبلغ هذا المُحدِّد سطحاً رقمياً؟ — أي هل يذكر صنفاً رقمياً واحداً على الأقلّ. */
export function reachesNumericSurface(selector, numericClasses) {
  for (const m of selector.matchAll(/\.(-?[A-Za-z_][\w-]*)/g)) {
    if (numericClasses.has(m[1])) return m[1];
  }
  return null;
}

/** هل المُحدِّد جذرُ وراثة؟ يُطبَّع بحذف الفراغ حول الفواصل. */
export function isInheritanceRoot(selector) {
  const normal = selector.replace(/\s*,\s*/g, ",").trim().toLowerCase();
  return INHERITANCE_ROOTS.includes(normal);
}

/**
 * ‏**الحكم على `font-family`.** ثلاث حالات، لا رابعة:
 *   · جذرُ وراثة  ⇐ وجهُه **يساوي تعريف** `--font-numeric-face` حرفاً بحرف.
 *     (وهذا أقوى من اشتراط الرمز: هو ما يجعل كلَّ سطحٍ لا يختار وجهه يرث
 *      الوجهَ الرقميّ نفسه.)
 *   · يبلغ صنفاً رقمياً ⇐ وجهُه `var(--font-numeric-face)` حرفاً بحرف.
 *   · غير ذلك ⇐ لا شأن للأرقام به.
 */
export function judgeFaces({ faceDeclarations, numericClasses, faceTokenDefinitions }) {
  const violations = [];
  const rootFace = faceTokenDefinitions.length > 0 ? faceTokenDefinitions[0].value : null;

  for (const d of faceDeclarations) {
    if (isInheritanceRoot(d.selector)) {
      if (rootFace === null) {
        violations.push({
          file: d.file, line: d.line, property: NUMERAL_FACE_PROPERTY, value: d.value,
          kind: "numeral-face-token-is-undefined",
          why:
            "جذرُ وراثةٍ يختار وجهاً و`" + NUMERAL_FACE_TOKEN + "` غير معرَّف في هذا النطاق — " +
            "فلا شيء يقول إنّ وجه الأرقام هو وجهُ الجذر.",
        });
        continue;
      }
      if (normalizeValue(d.value) !== normalizeValue(rootFace)) {
        violations.push({
          file: d.file, line: d.line, property: NUMERAL_FACE_PROPERTY, value: normalizeValue(d.value),
          kind: "root-face-differs-from-the-numeral-face",
          why:
            "وجهُ جذر الوراثة «" + normalizeValue(d.value) + "» و`" + NUMERAL_FACE_TOKEN +
            "` = «" + normalizeValue(rootFace) + "» — وكلُّ سطحٍ رقميّ لا يختار وجهه يرث الأول " +
            "بينما من يختار يأخذ الثاني، فيصير للرقم وجهان.",
        });
      }
      continue;
    }

    const via = reachesNumericSurface(d.selector, numericClasses);
    if (via === null) continue;

    if (!ALLOWED_FACES.includes(normalizeValue(d.value))) {
      violations.push({
        file: d.file, line: d.line, property: NUMERAL_FACE_PROPERTY, value: normalizeValue(d.value),
        kind: "numeral-face-is-not-the-token",
        why:
          "المُحدِّد «" + d.selector + "» يبلغ الصنف الرقميّ `." + via + "`، فوجهُه يجب أن يكون " +
          ALLOWED_FACES.map((v) => "`" + v + "`").join(" أو ") + " — " +
          "و`tabular-nums` طلبٌ إلى المِحرف لا أمر: وجهان يرسمان الرقم نفسه بعرضين، " +
          "و`getComputedStyle` يقول `tabular-nums` في كليهما.",
      });
    }
  }

  return violations;
}

/** جذر المستودع من موضع هذا الملفّ: `web/scripts/` ⇐ صعودان. */
export function repositoryRoot() {
  return path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..", "..");
}

/** الحدّ الأدنى لعدد التصريحات — حارس لافراغ: حذفُ القواعد لا يجعل الفحص يمرّ. */
export const DECLARATION_FLOOR = 52;

/**
 * أرضيتا حكم المِحرف. وهما ليستا زينةً: حكمُ `font-family` **مشتقّ** من
 * الأصناف الرقمية، ونمطُ اشتقاقٍ يتوقّف عن المطابقة يجعل المجموعة فارغة —
 * فيمرّ كلُّ وجهٍ ثانٍ صامتاً والفحص يُعلن نجاحاً. وهو فخ-43 داخل الحارس نفسه.
 */
export const NUMERIC_CLASS_FLOOR = 30;
export const FACE_DECLARATION_FLOOR = 20;

/**
 * ‏**عدد مواضع الخروج المسموح بها، مثبَّتاً بالتساوي لا بحدٍّ أعلى.**
 * ‏`--font-numeric-off` بابٌ شرعيّ — قائمة اللغات تعرض أسماءً لا أعمدة — لكنه
 * **باب**: من يريد أن يُطفئ الأرقام الجدولية على عمودٍ ماليّ يستطيع أن يمرّ منه.
 * فيُثبَّت العدد بالتساوي: أيُّ خروجٍ جديد **يُحمِّر الفحص** حتى يرفع أحدٌ الرقم
 * عمداً في هذا السطر — وهو إقرارٌ مكتوب، لا صمت. (‏وهذا هو الثقب المُعلَن.)
 */
export const OFF_TOKEN_USES = 2;

/** ما يجب أن يكون عليه تعريف كل رمز — قيمةً وموضعاً. */
export const EXPECTED_TOKEN_VALUES = Object.freeze({
  "--font-numeric": "tabular-nums",
  "--font-numeric-off": "normal",
});

/**
 * ‏**رمز المِحرف يُفحَص كما يُفحص رمز الطلب.** ولا يُثبَّت على قيمةٍ بعينها:
 * اختيارُ وجهٍ آخر للأرقام قرارٌ مشروع ويكفي أن يقع في موضعٍ واحد. المفروض
 * أنه **معرَّف، في ملفّ رموز، ومعرَّف مرّةً واحدة لكل نطاق** — فتعريفان
 * بقيمتين هما «وجهان» بثوبٍ آخر.
 */
export function auditFaceToken(faceTokenDefinitions) {
  const problems = [];
  if (faceTokenDefinitions.length === 0) {
    problems.push(`الرمز ${NUMERAL_FACE_TOKEN} غير معرَّف في أي ملفّ — ولا وجهَ واحد بلا رمزٍ يحمله.`);
  }
  for (const d of faceTokenDefinitions) {
    if (!d.file.endsWith("tokens.css")) {
      problems.push(`${d.file}:${d.line} · ${NUMERAL_FACE_TOKEN} يُعرَّف خارج ملفّ رموز.`);
    }
  }
  const values = new Set(faceTokenDefinitions.map((d) => normalizeValue(d.value)));
  if (values.size > 1) {
    problems.push(
      `${NUMERAL_FACE_TOKEN} معرَّفٌ بقيمتين مختلفتين (${[...values].join(" · ")}) — ` +
        "وذلك وجهان للرقم في ملفّين، وهو العطل نفسه في موضع التعريف."
    );
  }
  return problems;
}

/**
 * يفحص الرمزين نفسيهما: مُعرَّفان، وفي ملفّ رموز لا في مكوّن، وبالقيمة المتوقّعة.
 * وبدون هذا يستطيع أحدٌ أن يُبقي كل الإشارات سليمة ويجعل الرمز نفسه `normal`.
 */
export function auditTokens(tokenDefinitions) {
  const problems = [];
  for (const token of NUMERAL_TOKENS) {
    const defs = tokenDefinitions.filter((d) => d.token === token);
    if (defs.length === 0) problems.push(`الرمز ${token} غير معرَّف في أي ملفّ.`);
    for (const d of defs) {
      if (!d.file.endsWith("tokens.css")) {
        problems.push(`${d.file}:${d.line} · ${token} يُعرَّف خارج ملفّ رموز.`);
      }
      if (d.value !== EXPECTED_TOKEN_VALUES[token]) {
        problems.push(
          `${d.file}:${d.line} · ${token} = «${d.value}» والمتوقّع «${EXPECTED_TOKEN_VALUES[token]}».`
        );
      }
    }
  }
  return problems;
}

/** عدد التصريحات التي تخرج من الحكم عبر الرمز المُطفَأ. */
export const offTokenUses = (declarations) =>
  declarations.filter((d) => d.value === "var(--font-numeric-off)").length;

function main() {
  const root = repositoryRoot();
  const result = scanRepository(root);
  if (process.argv.includes("--json")) {
    process.stdout.write(JSON.stringify(result, null, 2) + "\n");
  }
  const total = result.declarations.length;
  const tokenProblems = [
    ...auditTokens(result.tokenDefinitions),
    ...auditFaceToken(result.faceTokenDefinitions),
  ];
  for (const problem of tokenProblems) process.stderr.write(`✗ ${problem}\n`);
  const offUses = offTokenUses(result.declarations);
  if (offUses !== OFF_TOKEN_USES) {
    process.stderr.write(
      `✗ مواضع --font-numeric-off = ${offUses} والمثبَّت ${OFF_TOKEN_USES}. ` +
        "كل خروجٍ من حكم الأرقام يُقَرّ بالاسم في numerals.mjs.\n"
    );
  }
  for (const v of result.violations) {
    process.stderr.write(`✗ ${v.file}:${v.line} · ${v.property}: ${v.value}\n   ${v.why}\n`);
  }
  const emptiness = [];
  if (total < DECLARATION_FLOOR) {
    emptiness.push(`تصريحات الطلب ${total} والحدّ الأدنى ${DECLARATION_FLOOR}`);
  }
  if (result.numericClasses.length < NUMERIC_CLASS_FLOOR) {
    emptiness.push(
      `الأصناف الرقمية المشتقّة ${result.numericClasses.length} والحدّ الأدنى ${NUMERIC_CLASS_FLOOR} — ` +
        "ومجموعةٌ فارغة تُمرِّر كلَّ وجهٍ ثانٍ"
    );
  }
  if (result.faceDeclarations.length < FACE_DECLARATION_FLOOR) {
    emptiness.push(
      `تصريحات المِحرف الممسوحة ${result.faceDeclarations.length} والحدّ الأدنى ${FACE_DECLARATION_FLOOR}`
    );
  }
  if (emptiness.length > 0) {
    process.stderr.write(
      "✗ حارس اللافراغ: " + emptiness.join("؛ ") + ".\n" +
        "   فحصٌ لا يجد شيئاً يمرّ على كل شيء.\n"
    );
    process.exit(1);
  }
  if (result.violations.length > 0 || tokenProblems.length > 0 || offUses !== OFF_TOKEN_USES) {
    process.stderr.write(`\n✗ ${result.violations.length + tokenProblems.length} مخالفة في رسم الأرقام.\n`);
    process.exit(1);
  }
  process.stdout.write(
    `✔ الأرقام الجدولية: ${total} تصريحاً في ${result.scannedFiles.length} ملفّاً، ` +
      `كلُّها تمرّ من ${NUMERAL_TOKENS.join(" أو ")}.\n` +
      `✔ المِحرف: ${result.faceDeclarations.length} تصريح \`font-family\` مفحوصاً مقابل ` +
      `${result.numericClasses.length} صنفاً رقمياً مشتقّاً — ولا وجهَ ثانٍ على سطحٍ رقميّ.\n`
  );
}

if (process.argv[1] && path.resolve(process.argv[1]) === fileURLToPath(import.meta.url)) main();
