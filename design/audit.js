#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   سلاسل بابل — فحص نظام التصميم  ·  Design-system audit
   ───────────────────────────────────────────────────────────────────────────
       node design/audit.js            تقرير كامل
       node design/audit.js --quiet    الأخطاء فقط
   بلا خطوة بناء، وبلا اعتماديات، وبلا شبكة. يخرج بالرمز 1 إن وُجدت مخالفة
   حاكمة، فيصلح لبوّابة في أي خطّ تكامل لاحقاً.

   يفحص خمسة أشياء، وهي بالضبط ما لا يمكن لعينٍ بشرية أن تتعقّبه:
     ١ · مفاتيح ناقصة في أي لغة        (تسقط إلى العربية بصمت — سلامةٌ لا صحّة)
     ٢ · مفاتيح يتيمة                   (ترجمة تُدفَع ثمنها ولا تُعرَض أبداً)
     ٣ · فئات جمع ناقصة أو ميتة         (‏zero في الإنجليزية صيغة لا تُختار أبداً)
     ٤ · نصّ مكتوب في الشيفرة           (‏HTML و JS و CSS ‏content:)
     ٥ · لون حرفي خارج ملفّات السمة     (الرمز هو العقد، والقيمة تخصّ السمة)
   ═══════════════════════════════════════════════════════════════════════════ */
"use strict";
const fs = require("fs");
const path = require("path");
const vm = require("vm");

const DESIGN = __dirname;
const QUIET = process.argv.includes("--quiet");

/* ── تحميل طبقة التدويل في سياق شبيه بالمتصفّح ─────────────────────────── */
const sandbox = { console, Intl, Date, JSON, Math, RegExp, String, Number, Object, Array, Error, TypeError };
sandbox.window = sandbox;
sandbox.document = {
  documentElement: { style: { setProperty() {}, removeProperty() {} },
                     setAttribute() {}, removeAttribute() {}, hasAttribute() { return false; } },
  createElement() { return { setAttribute() {}, appendChild() {}, style: {} }; },
  head: { appendChild() {} },
  querySelectorAll() { return []; },
  addEventListener() {}, dispatchEvent() {}
};
sandbox.localStorage = { getItem() { return null; }, setItem() {} };
sandbox.location = { search: "" };
sandbox.navigator = { languages: ["ar"] };
sandbox.CustomEvent = function () {};
vm.createContext(sandbox);
function load(rel) { vm.runInContext(fs.readFileSync(path.join(DESIGN, rel), "utf8"), sandbox, { filename: rel }); }
load("i18n/i18n.js");
load("i18n/locales/manifest.js");
const SB = sandbox.SB;
SB.I18N.catalog.forEach(e => load("i18n/" + e.file));

/* ── أدوات التقرير ───────────────────────────────────────────────────────── */
let fatal = 0, warn = 0;
const out = [];
function head(t) { out.push("", "─".repeat(74), t, "─".repeat(74)); }
function ok(t) { out.push("  ✓ " + t); }
function bad(t, list, isFatal) {
  if (isFatal) fatal += list.length; else warn += list.length;
  out.push("  " + (isFatal ? "✗" : "!") + " " + t + " (" + list.length + ")");
  list.slice(0, 40).forEach(x => out.push("      " + x));
  if (list.length > 40) out.push("      … +" + (list.length - 40));
}
function walk(dir, hit) {
  for (const name of fs.readdirSync(dir)) {
    const full = path.join(dir, name);
    const st = fs.statSync(full);
    if (st.isDirectory()) walk(full, hit);
    else hit(full);
  }
}
const rel = f => path.relative(path.dirname(DESIGN), f);

/* ═══════════════════════════════ ١ · تغطية المفاتيح ══════════════════════ */
const keys = SB.audit.keys();
head("١ · تغطية المفاتيح  ·  key coverage");
out.push("  الاتحاد · union: " + keys.union.length);
for (const code of SB.I18N.loaded()) {
  const L = keys.locales[code];
  out.push("  " + code + ": " + L.count + " مفتاحاً · keys, plural categories = " + L.categories.join("/"));
}
for (const code of SB.I18N.loaded()) {
  if (keys.missing[code].length) bad("ناقص · missing in " + code, keys.missing[code], true);
  if (keys.orphans[code].length) bad("يتيم · orphan in " + code, keys.orphans[code], false);
  if (keys.plural[code].length) bad("جمع · plural in " + code, keys.plural[code], true);
  if (keys.params[code].length) bad("معاملات · params in " + code, keys.params[code], true);
}
if (!fatal && !warn) ok("الأربع متطابقة تماماً · all locales identical");

const conv = SB.audit.convention();
if (conv.length) bad("اصطلاح التسمية · naming convention", conv.map(b => b.key + " — " + b.why), true);
else ok("اصطلاح التسمية · naming convention");

/* ═══════════════════════════ ٢ · نصّ مكتوب في الشيفرة ════════════════════ */
/* لماذا لا نستعمل محلّل HTML؟ لأن هذا الملفّ يجب أن يعمل بـnode وحده بلا
   اعتمادية واحدة. التحليل هنا نصّي متحفّظ: يبلّغ أكثر مما ينبغي لا أقلّ. */
const LETTERS = /[؀-ۿݐ-ݿऀ-ॿ]{2,}/;   /* عربي · أردي · ديفاناغري */
const SKIP_EL = /^(script|style|svg|symbol|defs|path|circle|rect|line|polyline|use|noscript|code|kbd)$/i;

function scanHtml(file) {
  const src = fs.readFileSync(file, "utf8");
  const hits = [];
  const toks = src.match(/<!--[\s\S]*?-->|<[^>]*>|[^<]+/g) || [];
  const stack = [];
  let line = 1;
  for (const t of toks) {
    const nl = (t.match(/\n/g) || []).length;
    if (t.startsWith("<!--")) { line += nl; continue; }
    if (t.startsWith("</")) { stack.pop(); line += nl; continue; }
    if (t.startsWith("<")) {
      const m = /^<\s*([A-Za-z0-9:-]+)/.exec(t);
      /* سمات مرئية بلا مفتاح */
      for (const a of ["placeholder", "aria-label", "title", "alt"]) {
        const am = new RegExp('\\s' + a + '="([^"]*)"').exec(t);
        if (am && LETTERS.test(am[1]) &&
            !(new RegExp('data-i18n-attr="[^"]*' + a + ':')).test(t) &&
            !/data-i18n-exempt/.test(t))
          hits.push(rel(file) + ":" + line + "  @" + a + '="' + am[1].slice(0, 60) + '"');
      }
      if (m && !/\/>\s*$/.test(t)) stack.push({ n: m[1].toLowerCase(), t });
      line += nl; continue;
    }
    const txt = t.trim();
    if (txt && LETTERS.test(txt)) {
      const parent = stack[stack.length - 1];
      const skip = stack.some(e => SKIP_EL.test(e.n) || /data-i18n-exempt/.test(e.t));
      const guarded = parent && /data-i18n(=|-html=)/.test(parent.t);
      if (!skip && !guarded)
        hits.push(rel(file) + ":" + line + "  «" + txt.replace(/\s+/g, " ").slice(0, 70) + "»");
    }
    line += nl;
  }
  return hits;
}

/* نصّ في JS: سلسلة حرفية تحمل حروفاً عربية/أردية/هندية خارج التعليقات.
   ملفّات اللغة نفسها مستثناة بداهةً، و behaviors.js يُستثنى منه مولّد التفقيط
   لأنه معجم لغويّ عربي لا نصّ واجهة (وموثّق في مكانه). */
function stripComments(src) {
  return src.replace(/\/\*[\s\S]*?\*\//g, m => m.replace(/[^\n]/g, " "))
            .replace(/(^|[^:])\/\/[^\n]*/g, (m, p) => p + m.slice(p.length).replace(/[^\n]/g, " "));
}
function scanJs(file) {
  const src = stripComments(fs.readFileSync(file, "utf8"));
  const hits = [];
  const TAFQEET = /^(ONES|TEENS|TENS|HUNS|scales)\b/;
  src.split("\n").forEach((l, i) => {
    if (!LETTERS.test(l)) return;
    if (/^\s*(var\s+)?(ONES|TEENS|TENS|HUNS)\s*=/.test(l)) return;      /* معجم التفقيط */
    if (/\[1e[369],\s*\[/.test(l)) return;                               /* سلالم التفقيط */
    if (/under1000|groupWord|words \+=|parts\.join|return \(amount < 0/.test(l)) return;
    if (/halalas === 0/.test(l)) return;                                 /* حالة الصفر في التفقيط */
    if (/console\.(warn|error|log)/.test(l)) return;                     /* رسائل مطوّر */
    if (/throw new (TypeError|Error)/.test(l)) return;                   /* رسائل مطوّر */
    if (/^\s*(arab|arabext|deva)\s*:/.test(l)) return;                   /* مجموعات أرقام */
    if (/plu\.push|bad\.push|\bwhy:/.test(l)) return;                    /* مخرَج الفحص */
    hits.push(rel(file) + ":" + (i + 1) + "  " + l.trim().slice(0, 80));
  });
  return hits;
}

/* نصّ في CSS: content:"..." بحروف لغة. المسموح var(--i18n-*). */
function scanCssText(file) {
  const src = fs.readFileSync(file, "utf8").replace(/\/\*[\s\S]*?\*\//g, m => m.replace(/[^\n]/g, " "));
  const hits = [];
  src.split("\n").forEach((l, i) => {
    const m = /content\s*:\s*("([^"]*)"|'([^']*)')/.exec(l);
    if (m && LETTERS.test(m[2] || m[3] || ""))
      hits.push(rel(file) + ":" + (i + 1) + "  " + l.trim().slice(0, 80));
  });
  return hits;
}

const htmlFiles = [], jsFiles = [], cssFiles = [];
walk(DESIGN, f => {
  if (/[\\/]i18n[\\/]/.test(f)) return;
  if (f.endsWith(".html")) htmlFiles.push(f);
  else if (f.endsWith(".js")) jsFiles.push(f);
  else if (f.endsWith(".css")) cssFiles.push(f);
});

head("٢ · نصّ مكتوب في الشيفرة  ·  hard-coded strings");
const hard = [].concat(
  ...htmlFiles.map(scanHtml),
  ...jsFiles.filter(f => !/audit\.js$/.test(f)).map(scanJs),
  ...cssFiles.map(scanCssText)
);
if (hard.length) bad("نصّ بلا مفتاح · text without a key", hard, true);
else ok("لا نصّ مرئياً خارج ملفّات اللغة · no visible text outside locale files");

/* ═══════════════════════════ ٣ · الألوان ═════════════════════════════════ */
/* العقد: القيمة الحرفية تعيش في design/theme/*.css وحدها. أي #hex أو rgb()
   أو hsl() في مكوّن أو شاشة يكسر «سمة عميل بملفّ واحد». */
const COLOR = /#[0-9a-fA-F]{3,8}\b|\brgba?\(|\bhsla?\(/g;
const THEME_DIR = path.join(DESIGN, "theme");
head("٣ · الألوان  ·  colour literals");
const strays = [], palette = new Map();
function countColours(file) {
  const src = fs.readFileSync(file, "utf8");
  const clean = src.replace(/\/\*[\s\S]*?\*\//g, m => m.replace(/[^\n]/g, " "));
  const inTheme = file.startsWith(THEME_DIR);
  clean.split("\n").forEach((l, i) => {
    const found = l.match(COLOR);
    if (!found) return;
    if (inTheme) {
      for (const c of found) if (c.startsWith("#")) {
        const k = c.toLowerCase();
        palette.set(k, (palette.get(k) || 0) + 1);
      }
    } else {
      strays.push(rel(file) + ":" + (i + 1) + "  " + l.trim().slice(0, 80));
    }
  });
}
cssFiles.forEach(countColours);
htmlFiles.forEach(f => {                       /* ألوان داخل <style> في الصفحات */
  const src = fs.readFileSync(f, "utf8");
  const styles = src.match(/<style[^>]*>[\s\S]*?<\/style>/g) || [];
  styles.forEach(block => {
    block.split("\n").forEach((l, i) => {
      if (COLOR.test(l)) strays.push(rel(f) + " <style>:" + (i + 1) + "  " + l.trim().slice(0, 70));
      COLOR.lastIndex = 0;
    });
  });
});
out.push("  ألوان متمايزة في ملفّ السمة · distinct hex in theme: " + palette.size);
if (strays.length) bad("لون حرفي خارج ملفّ السمة · colour literal outside theme/", strays, true);
else ok("لا لون حرفي خارج design/theme/ · none outside design/theme/");

/* ═══════════════════════ ٤ · الاتجاه: خصائص فيزيائية ════════════════════ */
head("٤ · الاتجاه  ·  direction");
const PHYS = /(^|[^-\w])(margin-left|margin-right|padding-left|padding-right|border-left|border-right|left|right)\s*:/;
const DIRHARD = /(^|[^-\w])direction\s*:\s*(rtl|ltr)/;
const dirHits = [];
cssFiles.concat(htmlFiles).forEach(f => {
  const src = fs.readFileSync(f, "utf8").replace(/\/\*[\s\S]*?\*\//g, m => m.replace(/[^\n]/g, " "));
  src.split("\n").forEach((l, i) => {
    if (PHYS.test(l)) dirHits.push(rel(f) + ":" + (i + 1) + "  فيزيائي · physical: " + l.trim().slice(0, 70));
    /* direction مسموحة فقط داخل صندوق معزول (‏.ltr/.num/.amt) أو مع :not([dir]) */
    if (DIRHARD.test(l) && !/unicode-bidi|:not\(\[dir\]\)|\.ltr|\.rtl|\.num|\.amt|\.acct-code|\.taxval|\.cell|\.stat|\.demo-hd|\.server-text|\.pager|table\.doc-table|\.n\{/.test(l))
      dirHits.push(rel(f) + ":" + (i + 1) + "  direction مثبّتة · hard-coded: " + l.trim().slice(0, 70));
  });
});
/* transform اتجاهي بلا --flip-x */
cssFiles.concat(htmlFiles).forEach(f => {
  const src = fs.readFileSync(f, "utf8").replace(/\/\*[\s\S]*?\*\//g, m => m.replace(/[^\n]/g, " "));
  src.split("\n").forEach((l, i) => {
    if (/translateX\(|rotate\(/.test(l) && !/--flip-x/.test(l) && !/translateX\(0\)/.test(l) && !/rotate\(180deg\)|rotate\(360deg\)/.test(l))
      dirHits.push(rel(f) + ":" + (i + 1) + "  transform بلا --flip-x · without --flip-x: " + l.trim().slice(0, 70));
  });
});
if (dirHits.length) bad("مخالفات اتجاه · direction violations", dirHits, true);
else ok("كل التخطيط بخصائص منطقية · layout is fully logical");

/* ═══════════════════ ٥ · المفاتيح المستعملة فعلاً في الصفحات ════════════ */
/* الفحص الأول يثبت أن اللغات متطابقة. هذا يثبت أن ما تطلبه الصفحات موجود:
   مفتاحٌ مكتوب خطأً في data-i18n لا يظهر في أي مقارنة بين ملفّات اللغة. */
head("٥ · المفاتيح المستعملة في الصفحات  ·  keys referenced by pages");
const used = new Map();          /* key → [مواضع] */
function note(k, where) { if (!used.has(k)) used.set(k, []); used.get(k).push(where); }
htmlFiles.forEach(f => {
  const src = fs.readFileSync(f, "utf8");
  let m;
  const rx = /data-i18n(?:-html)?="([^"]+)"/g;
  while ((m = rx.exec(src))) note(m[1], rel(f));
  const ra = /data-i18n-attr="([^"]+)"/g;
  while ((m = ra.exec(src))) m[1].split(";").forEach(pair => {
    const bits = pair.split(":"); if (bits.length > 1) note(bits.slice(1).join(":").trim(), rel(f));
  });
  const rp = /data-i18n-params='([^']+)'/g;
  while ((m = rp.exec(src))) {
    try { const o = JSON.parse(m[1]);
      Object.keys(o).forEach(kk => { if (typeof o[kk] === "string" && o[kk][0] === "@") note(o[kk].slice(1), rel(f)); });
    } catch (e) { out.push("  ! JSON غير صالح · invalid data-i18n-params in " + rel(f) + ": " + m[1]); warn++; }
  }
  /* SB.t("…") داخل السكربتات المضمّنة */
  const rt = /SB\.t(?:\.plural|\.in)?\(\s*["']([a-zA-Z0-9.]+)["']/g;
  while ((m = rt.exec(src))) note(m[1], rel(f));
});
jsFiles.filter(f => !/audit\.js$/.test(f)).forEach(f => {
  const src = fs.readFileSync(f, "utf8");
  let m; const rt = /\bT\(\s*["']([a-zA-Z0-9.]+)["']|SB\.t(?:\.plural|\.in)?\(\s*["']([a-zA-Z0-9.]+)["']/g;
  while ((m = rt.exec(src))) note(m[1] || m[2], rel(f));
});
out.push("  مفاتيح مطلوبة من الصفحات · referenced: " + used.size);
const undef = [], notPlural = [], shouldBePlural = [];
const srcMsgs = SB.I18N.messages("ar");
for (const [k, where] of used) {
  const missingIn = SB.I18N.loaded().filter(c => !Object.prototype.hasOwnProperty.call(SB.I18N.messages(c), k));
  if (missingIn.length) undef.push(k + "  ← " + where[0] + "  (" + missingIn.join(",") + ")");
}
/* data-i18n-count على مفتاح ليس كيس جمع، والعكس */
htmlFiles.forEach(f => {
  const src = fs.readFileSync(f, "utf8");
  const rx = /<[^>]*data-i18n="([^"]+)"[^>]*data-i18n-count="[^"]*"|<[^>]*data-i18n-count="[^"]*"[^>]*data-i18n="([^"]+)"/g;
  let m;
  while ((m = rx.exec(src))) {
    const k = m[1] || m[2], v = srcMsgs[k];
    if (v !== undefined && !SB.I18N.isPluralBag(v)) notPlural.push(k + "  ← " + rel(f));
  }
});
for (const [k, where] of used) {
  const v = srcMsgs[k];
  if (v !== undefined && SB.I18N.isPluralBag(v)) {
    const f = where[0];
    const src = fs.readFileSync(path.join(path.dirname(DESIGN), f), "utf8");
    const usedWithCount = new RegExp('data-i18n="' + k.replace(/\./g, "\\.") + '"[^>]*data-i18n-count|data-i18n-count[^>]*data-i18n="' + k.replace(/\./g, "\\.") + '"').test(src)
      || new RegExp('t\\.plural\\(\\s*["\']' + k.replace(/\./g, "\\.") + '["\']').test(src)
      || new RegExp('SB\\.t\\.plural\\(\\s*["\']' + k.replace(/\./g, "\\.") + '["\']').test(src);
    if (!usedWithCount) shouldBePlural.push(k + "  ← " + f);
  }
}
if (undef.length) bad("مفتاح مطلوب غير معرَّف · referenced but undefined", undef, true);
else ok("كل مفتاح تطلبه الصفحات معرَّف في اللغات الأربع · every referenced key exists in all four");
if (notPlural.length) bad("data-i18n-count على مفتاح ليس كيس جمع · count on a non-plural key", notPlural, true);
if (shouldBePlural.length) bad("كيس جمع مستعمَل بلا عدد · plural bag used without a count", shouldBePlural, false);

/* ═══════════════════════════════ الخلاصة ════════════════════════════════ */
head("الخلاصة · summary");
out.push("  مخالفات حاكمة · fatal:   " + fatal);
out.push("  ملاحظات · warnings:        " + warn);
out.push("");
if (!QUIET || fatal) console.log(out.join("\n"));
process.exit(fatal ? 1 : 0);
