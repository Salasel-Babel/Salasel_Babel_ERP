/* ═══════════════════════════════════════════════════════════════════════════
   سلاسل بابل ERP — طبقة التدويل  ·  Salasel Babel ERP — i18n runtime
   ───────────────────────────────────────────────────────────────────────────
   بلا إطار عمل، وبلا خطوة بناء، وبلا شبكة.
   No framework, no build step, no network. Load BEFORE behaviors.js.

     <script src="design/i18n/i18n.js" defer></script>
     <script src="design/i18n/locales/manifest.js" defer></script>
     <script src="design/i18n/locales/ar.js" defer></script>   <!-- … -->
     <script src="design/components/behaviors.js" defer></script>

   يعرّف: window.SB.I18N · window.SB.t · window.SB.fmt · window.SB.dom
   ───────────────────────────────────────────────────────────────────────────
   ⚠ الحدّ الحاكم في هذا الملف: **التنسيق المحلّي للعرض فقط.**
     القيمة المنسّقة محلّياً لا تُكتب في حقل، ولا تُرسَل، ولا تُقارَن، ولا تُجزَّأ.
     هذا مفروض بالنوع لا بالتعليق: كل تنسيق يعيد كائن Display لا نصّاً، و Display
     يرمي استثناءً عند أي تحويل ضمني إلى نصّ. المخرج الوحيد إلى الشاشة هو
     Display.into(el)، والمخرج الوحيد إلى الخادم هو Display.machine (ASCII دائماً).
     المرجع المقيس: docs/evidence/traps.md — فخ-18 · فخ-23 · فخ-25 · فخ-38.
   ═══════════════════════════════════════════════════════════════════════════ */
(function (global) {
  "use strict";

  var SB = global.SB || (global.SB = {});

  /* ─────────────────────────────────────────────────────────── 0 · أدوات */
  function $$(sel, root) {
    return Array.prototype.slice.call((root || document).querySelectorAll(sel));
  }
  function own(o, k) { return Object.prototype.hasOwnProperty.call(o, k); }

  /* محارف التحكّم غير المرئية — مكتوبة بالهروب الصريح عمداً.
     لا يجوز أن يحمل هذا الملف نفسه محرفاً غير مرئي واحداً. */
  var INVISIBLE_RE = /[\u200B-\u200F\u061C\u202A-\u202E\u2066-\u2069\uFEFF]/;

  /* ═══════════════════════════════════════════════════ 1 · نوع العرض
     Display — القيمة المنسّقة للعرض. ليست نصّاً، ولا يمكن أن تصير نصّاً.

     لماذا كائن لا نصّ؟ لأن المنع بالتعليق لا يمنع. المطوّر الذي يكتب
       input.value = fmt(x)   أو   hash(fmt(x))   أو   `${fmt(x)}`
     يحصل على TypeError فوراً — لا على قيمة خاطئة تمرّ بصمت إلى سلسلة التجزئة.
     النصّ محبوس في WeakMap ولا يخرج إلا عبر Display.into(el) وهو مصرف DOM.  */
  var TEXT = new WeakMap();

  function refuse(what) {
    return function () {
      throw new TypeError(
        "SB.Display: قيمة معروضة محلّياً لا تُحوَّل إلى نصّ (" + what + "). " +
        "استعمل d.into(element) للعرض، أو d.machine للقيمة القابلة للإرسال/التجزئة. " +
        "[SB.Display is display-only; use .into(el) to render or .machine to submit.]"
      );
    };
  }

  function Display(text, machine, kind, meta) {
    if (INVISIBLE_RE.test(text)) {
      /* الحارس البنيوي: أي مُنسِّق سرّب محرف تحكّم (وهو ما تفعله Intl تحت ar/ur)
         يفشل هنا بصوت عالٍ بدل أن يصل إلى الشاشة ثم إلى حقل ثم إلى بصمة. */
      throw new Error(
        "SB.Display: النصّ المنسّق يحمل محرف تحكّم غير مرئي — مصدره على الأرجح Intl. " +
        "راجع docs/evidence/traps.md فخ-23. [formatted text contains a bidi control character]"
      );
    }
    TEXT.set(this, String(text));
    Object.defineProperty(this, "machine", { value: String(machine), enumerable: true });
    Object.defineProperty(this, "kind", { value: kind, enumerable: true });
    Object.defineProperty(this, "meta", { value: Object.freeze(meta || {}), enumerable: true });
    Object.freeze(this);
  }
  Display.prototype.toString = refuse("toString");
  Display.prototype.valueOf = refuse("valueOf");
  Display.prototype.toJSON = refuse("toJSON");
  Display.prototype[Symbol.toPrimitive] = refuse("implicit coercion");
  Display.prototype.localeCompare = refuse("localeCompare");

  /* المصرف الوحيد: يكتب النصّ في عنصر ويضبط عزله الاتجاهي. */
  Display.prototype.into = function (el) {
    if (!el || !el.nodeType) throw new TypeError("SB.Display.into(el): يحتاج عنصر DOM.");
    el.textContent = TEXT.get(this);
    if (this.meta.ltr) {
      /* العزل بالـCSS والسمة، لا بحقن محرف تحكّم — README §٣٫٧ */
      if (!el.hasAttribute("dir")) el.setAttribute("dir", "ltr");
    }
    return el;
  };
  /* للمعاينة في أدوات التطوير وصفحة الفحص فقط — مُسمّاة صراحةً لتظهر في المراجعة. */
  Display.prototype.unsafeTextForAudit = function () { return TEXT.get(this); };

  SB.Display = Display;
  SB.isDisplay = function (v) { return v instanceof Display; };

  /* الحدّ في الاتجاه المعاكس: أي شيء يُرسَل أو يُجزَّأ يمرّ من هنا. */
  SB.machine = function (v) {
    if (v instanceof Display) return v.machine;
    if (v && typeof v === "object" && v.__html)
      throw new TypeError("SB.machine: قيمة عرض (HTML) لا تُرسَل.");
    return String(v == null ? "" : v);
  };

  /* غلاف HTML للعرض — نفس المنطق: كائن لا نصّ. */
  function Html(markup) {
    Object.defineProperty(this, "__html", { value: String(markup), enumerable: false });
    Object.freeze(this);
  }
  Html.prototype.toString = refuse("toString (Html)");
  Html.prototype[Symbol.toPrimitive] = refuse("implicit coercion (Html)");
  SB.Html = Html;

  /* ═══════════════════════════════════════════════════ 2 · سجلّ اللغات */
  var locales = {};        /* code → {meta, messages} */
  var active = null;       /* code */
  var SOURCE = "ar";       /* لغة المصدر والاحتياط النهائي */
  var listeners = [];

  var I18N = {
    SOURCE: SOURCE,
    catalog: [],           /* يملؤه manifest.js */
    missing: [],           /* [{locale, key}] */
    debug: false,
    strict: false          /* true ⇒ المفتاح الناقص يرمي (للاختبارات) */
  };
  SB.I18N = I18N;

  I18N.define = function (code, meta, messages) {
    locales[code] = { code: code, meta: meta || {}, messages: flatten(messages || {}) };
    return locales[code];
  };
  I18N.has = function (code) { return own(locales, code); };
  I18N.loaded = function () { return Object.keys(locales); };
  I18N.meta = function (code) { return (locales[code || active] || { meta: {} }).meta; };
  I18N.active = function () { return active; };
  I18N.messages = function (code) { return (locales[code] || { messages: {} }).messages; };

  /* تسطيح {a:{b:"x"}} → {"a.b":"x"}. كائنات الجمع تُترك كما هي (تُعرَف بمفتاح other). */
  function flatten(obj, prefix, out) {
    out = out || {}; prefix = prefix || "";
    for (var k in obj) {
      if (!own(obj, k)) continue;
      var v = obj[k], key = prefix ? prefix + "." + k : k;
      if (v && typeof v === "object" && !Array.isArray(v) && !isPluralBag(v)) flatten(v, key, out);
      else out[key] = v;
    }
    return out;
  }
  var CLDR = ["zero", "one", "two", "few", "many", "other"];
  var EXACT = /^=\d+$/;              /* صيغة العدد الصريح على طريقة ICU: "=0" */
  function isPluralBag(v) {
    if (!v || typeof v !== "object" || Array.isArray(v)) return false;
    for (var k in v) if (own(v, k) && CLDR.indexOf(k) === -1 && !EXACT.test(k)) return false;
    return own(v, "other");
  }
  I18N.isPluralBag = isPluralBag;
  I18N.CLDR_CATEGORIES = CLDR;

  /* ═════════════════════════════════ 3 · سياسة المفتاح الناقص
     سلسلة الاحتياط: اللغة النشطة → احتياط اللغة المُعلَن في ملفها → ar (المصدر).
     ولا شيء بعدها إلا اسم المفتاح نفسه.

     لماذا الاحتياط إلى العربية لا إظهار المفتاح؟
       لأن رأس عمود فارغ أو مفتاح خام في ميزان مراجعة أسوأ من كلمة بلغة أخرى:
       المحاسب يقرأ الرقم تحت رأس لا يفهمه فيقرأه خطأً. الكلمة العربية تُفهَم أو
       تُسأل؛ «screen.trialBalance.col.debit» لا يُفهَم ولا يُسأل عنه.
     ولماذا لا يُخفى الأمر؟
       لأنه يُسجَّل دائماً في I18N.missing، ويُطبع تحذيراً مرة واحدة لكل مفتاح،
       ويُعلَّم العنصر بـ data-i18n-missing فيظهر مؤطّراً في وضع الفحص،
       ويُحصى في design/audit.html و node design/audit.js.                     */
  var warned = {};
  function record(code, key) {
    var sig = code + "|" + key;
    if (!warned[sig]) {
      warned[sig] = true;
      I18N.missing.push({ locale: code, key: key });
      if (global.console && console.warn)
        console.warn("[SB.i18n] مفتاح ناقص · missing key: " + key + " (" + code + ")");
    }
  }

  function chain(code) {
    var out = [], seen = {}, c = code;
    while (c && !seen[c]) { seen[c] = 1; out.push(c); c = (locales[c] && locales[c].meta.fallback) || null; }
    if (!seen[SOURCE]) out.push(SOURCE);
    return out;
  }
  I18N.chain = chain;

  function lookup(key, code) {
    var link = chain(code || active);
    for (var i = 0; i < link.length; i++) {
      var L = locales[link[i]];
      if (L && own(L.messages, key)) return { value: L.messages[key], from: link[i] };
    }
    return null;
  }
  I18N.lookup = lookup;

  /* ═══════════════════════════════════════════ 4 · الاستبدال والترجمة */
  /* معاملات محيطية: قيمٌ تخصّ اللغة النشطة ولا معنى لكتابتها في كل موضع نداء.
     ‏{currency} و{currencyCode} أكثر ما يتكرّر — رأس عمود «مدين (ر.س)» يصير
     «Debit (SAR)» بلا سطر شيفرة واحد في الصفحة. */
  function ambient() {
    var n = (I18N.meta() || {}).numbers || {};
    return { currency: n.currency || "", currencyCode: n.currencyCode || "" };
  }

  function interpolate(text, params, depth) {
    var amb = ambient();
    return String(text).replace(/\{(\w+)\}/g, function (m, name) {
      var v;
      if (params && own(params, name)) v = params[name];
      else if (own(amb, name)) v = amb[name];
      else return m;
      if (v instanceof Display)
        throw new TypeError("SB.t: لا تُمرَّر قيمة Display كمعامل نصّي — استعمل SB.dom.amount() داخل data-i18n-html.");
      /* مرجع مفتاح داخل معامل: "@acct.class.assets" يُترجَم بدوره.
         يسمح بتركيب «إجمالي {class}» من مفتاحين بدل ضرب المفاتيح في التصنيفات.
         العمق محدود بواحد فلا تنشأ حلقة. */
      if (typeof v === "string" && v.charAt(0) === "@" && !(depth > 0))
        return translate(v.slice(1), null, active, 1);
      return String(v);
    });
  }

  function translate(key, params, code, depth) {
    var hit = lookup(key, code);
    if (!hit) {
      record(code || active, key);
      if (I18N.strict) throw new Error("SB.t: مفتاح غير معرَّف · undefined key: " + key);
      return I18N.debug ? "⟦" + key + "⟧" : key;
    }
    if (hit.from !== (code || active)) record(code || active, key);
    var v = hit.value;
    if (isPluralBag(v)) v = v.other;     /* استعمال مفتاح جمع بلا عدد — يُصلحه الفحص */
    return interpolate(v, params, depth);
  }

  /* SB.t("common.action.save") */
  var t = function (key, params) { return translate(key, params, active); };
  t.in = function (code, key, params) { return translate(key, params, code); };
  t.has = function (key, code) { return !!lookup(key, code); };
  t.raw = function (key, code) { var h = lookup(key, code); return h ? h.value : null; };

  /* ═══════════════════════════════════ 5 · الجمع — Intl.PluralRules
     العربية ستّ فئات (zero · one · two · few · many · other)، والإنجليزية اثنتان،
     والأردية اثنتان، والهندية اثنتان — **لكن الهندية تضع الصفر في فئة one**.
     ولهذا `count === 1 ? a : b` خطأ في أربع لغات من أربع، لا في واحدة.        */
  var pluralCache = {};
  function rules(code) {
    var lc = (locales[code] && locales[code].meta.pluralLocale) || code;
    if (!pluralCache[lc]) {
      try { pluralCache[lc] = new Intl.PluralRules(lc); }
      catch (e) { pluralCache[lc] = { select: function (n) { return n === 1 ? "one" : "other"; } }; }
    }
    return pluralCache[lc];
  }
  I18N.pluralCategories = function (code) {
    try { return new Intl.PluralRules((locales[code] && locales[code].meta.pluralLocale) || code)
      .resolvedOptions().pluralCategories.slice().sort(function (a, b) { return CLDR.indexOf(a) - CLDR.indexOf(b); }); }
    catch (e) { return ["one", "other"]; }
  };
  I18N.pluralCategory = function (n, code) { return rules(code || active).select(Number(n)); };

  /* SB.t.plural("screen.trialBalance.rowCount", 24) */
  t.plural = function (key, count, params, code) {
    code = code || active;
    var hit = lookup(key, code);
    if (!hit) {
      record(code, key);
      return I18N.debug ? "⟦" + key + " #" + count + "⟧" : key;
    }
    if (hit.from !== code) record(code, key);
    var bag = hit.value;
    if (!isPluralBag(bag)) return interpolate(bag, merge(params, count));
    /* الأسبقية للصيغة الصريحة "=N" (كما في ICU) ثم لفئة CLDR. الصفر خاصّةً:
       العربية تملك فئة zero حقيقية، والإنجليزية والأردية والهندية لا تملكها،
       فتحتاج "=0" لتقول «لا شيء» بدل «0 عنصر». */
    var exact = "=" + Number(count);
    if (own(bag, exact)) return interpolate(bag[exact], merge(params, count));
    var cat = rules(hit.from).select(Number(count));
    var form = own(bag, cat) ? bag[cat] : bag.other;
    return interpolate(form, merge(params, count));
  };
  function merge(params, count) {
    var p = {}; for (var k in params) if (own(params, k)) p[k] = params[k];
    /* العدد يدخل النصّ **بأرقام العرض** الخاصّة باللغة، وهو عرض محض. */
    p.count = digitsFor(String(count));
    p.countRaw = String(count);
    return p;
  }
  SB.t = t;

  /* ═══════════════════════════════ 6 · الأرقام والتواريخ — عرض فقط
     لا Intl.NumberFormat ولا toLocaleString ولا Intl.DateTimeFormat للعرض:
     كلها تحقن محارف تحكّم غير مرئية تحت ar و ur (مقيس — انظر design/audit.html).
     الفواصل وأشكال الأرقام وأسماء الشهور تأتي من **ملف اللغة** صراحةً،
     فالمُخرَج محدَّد بالكامل ولا يتغيّر بتغيّر إصدار ICU.                      */

  function numOpts() {
    var m = I18N.meta() || {};
    return m.numbers || { group: ",", decimal: ".", groupSizes: [3], digits: "latn", minus: "-" };
  }

  var DIGIT_SETS = {
    latn: "0123456789",
    arab: "٠١٢٣٤٥٦٧٨٩",
    arabext: "۰۱۲۳۴۵۶۷۸۹",
    deva: "०१२३४५६७८९"
  };
  I18N.DIGIT_SETS = DIGIT_SETS;

  /* تحويل شكل الرقم — **عرض فقط**، ولا يعود إلى أي قيمة تُرسَل (README §٣٫٨). */
  function digitsFor(ascii, set) {
    set = set || numOpts().digits || "latn";
    var map = DIGIT_SETS[set];
    if (!map || set === "latn") return ascii;
    return String(ascii).replace(/[0-9]/g, function (d) { return map.charAt(+d); });
  }
  I18N.shapeDigits = digitsFor;

  /* تجميع بأحجام مجموعات معلنة في ملف اللغة: [3] غربي، [3,2] هندي (لكh/كرور). */
  function groupInt(intPart, sep, sizes) {
    if (!sep) return intPart;
    sizes = (sizes && sizes.length) ? sizes : [3];
    var out = "", i = intPart.length, s = 0;
    while (i > 0) {
      var size = sizes[Math.min(s, sizes.length - 1)];
      var start = Math.max(0, i - size);
      out = intPart.slice(start, i) + (out ? sep + out : "");
      i = start; s++;
    }
    return out;
  }

  var fmt = {};
  SB.fmt = fmt;

  /* المبلغ: يبقى نصّاً decimal من الخادم. SB.money() يحسب بالنصّ بلا عائم،
     ثم نُطبّق فواصل اللغة وشكل أرقامها — على العرض وحده. */
  fmt.amount = function (raw, opts) {
    opts = opts || {};
    var scale = opts.scale === undefined ? 2 : opts.scale;
    var canonical = SB.money ? SB.money(raw, scale) : null;   /* "-1,234,567.89" ASCII */
    if (canonical === null || canonical === undefined) canonical = "";
    var n = numOpts();
    var machine = canonical.replace(/,/g, "");                 /* ASCII صرف: "-1234567.89" */
    var text = canonical;
    if (text) {
      var neg = text.charAt(0) === "-";
      if (neg) text = text.slice(1);
      var parts = text.split(".");
      text = groupInt(parts[0].replace(/,/g, ""), n.group, n.groupSizes) +
             (parts.length > 1 ? n.decimal + parts[1] : "");
      text = digitsFor(text, n.digits);
      if (neg) text = (n.minus || "-") + text;
    }
    return new Display(text, machine, "amount", { ltr: true, scale: scale });
  };

  fmt.integer = function (raw) {
    var s = SB.toLatinDigits ? SB.toLatinDigits(String(raw)) : String(raw);
    s = s.replace(/[^\d-]/g, "");
    var n = numOpts(), neg = s.charAt(0) === "-";
    if (neg) s = s.slice(1);
    var text = digitsFor(groupInt(s || "0", n.group, n.groupSizes), n.digits);
    return new Display((neg ? (n.minus || "-") : "") + text, (neg ? "-" : "") + (s || "0"),
      "integer", { ltr: true });
  };

  fmt.percent = function (raw) {
    var n = numOpts();
    var s = String(raw).replace(/[^\d.-]/g, "");
    return new Display(digitsFor(s, n.digits) + (n.percentSuffix || "%"), s, "percent", { ltr: true });
  };

  /* التاريخ: مبني من أسماء ملف اللغة وترتيبه، لا من ICU.
     ISO يبقى ASCII دائماً وهو ما يُرسَل. */
  function pad(x) { return (x < 10 ? "0" : "") + x; }
  fmt.date = function (value, style) {
    var d = value instanceof Date ? value : (SB.parseDate ? SB.parseDate(value) : null);
    var m = I18N.meta() || {}, dts = m.dates || {};
    if (!d) return new Display(dts.emptyDash || "—", "", "date", { ltr: true });
    var iso = d.getUTCFullYear() + "-" + pad(d.getUTCMonth() + 1) + "-" + pad(d.getUTCDate());
    var Y = String(d.getUTCFullYear()), M = pad(d.getUTCMonth() + 1), D = pad(d.getUTCDate());
    var text;
    if (style === "long") {
      var wd = (dts.weekdays || [])[d.getUTCDay()] || "";
      var mo = (dts.months || [])[d.getUTCMonth()] || M;
      text = (dts.longPattern || "{weekday}, {day} {month} {year}")
        .replace("{weekday}", wd).replace("{day}", digitsFor(String(d.getUTCDate())))
        .replace("{month}", mo).replace("{year}", digitsFor(Y))
        .replace("{era}", dts.eraGregorian || "").trim();
    } else {
      text = (dts.shortPattern || "{year}/{month}/{day}")
        .replace("{year}", digitsFor(Y)).replace("{month}", digitsFor(M)).replace("{day}", digitsFor(D));
    }
    return new Display(text, iso, "date", { ltr: true });
  };

  /* هجري أم القرى — تحويل عرض محض، ولا يعود إلى التخزين أبداً (README §٣٫٤).
     نستعمل Intl **بأرقام لاتينية صريحة** ثم نبني النصّ بأسماء ملف اللغة،
     فلا يتسرّب محرف تحكّم من ICU إلى المخرَج. */
  fmt.hijri = function (value) {
    var d = value instanceof Date ? value : (SB.parseDate ? SB.parseDate(value) : null);
    if (!d) return null;
    var dts = (I18N.meta() || {}).dates || {};
    if (!dts.hijriMonths) return null;
    try {
      var f = new Intl.DateTimeFormat("en-u-ca-islamic-umalqura-nu-latn",
        { day: "numeric", month: "numeric", year: "numeric", timeZone: "UTC" });
      var parts = {}, list = f.formatToParts(d);
      for (var i = 0; i < list.length; i++) parts[list[i].type] = list[i].value;
      if (!parts.year || !parts.month || !parts.day) return null;
      var mi = parseInt(parts.month, 10) - 1;
      if (!(mi >= 0 && mi < 12)) return null;
      var text = digitsFor(parts.day) + " " + dts.hijriMonths[mi] + " " +
                 digitsFor(parts.year) + " " + (dts.eraHijri || "");
      /* لا machine: التاريخ الهجري لا يُخزَّن ولا يُرسَل — فلا قيمة آلية له. */
      return new Display(text.trim(), "", "hijri-display-only", { ltr: false });
    } catch (e) { return null; }
  };

  /* المقارنة والفرز — ترتيب اللغة النشطة، لا "ar" مثبّتة. */
  var collators = {};
  I18N.collator = function (code) {
    code = code || active || SOURCE;
    if (!collators[code]) {
      try { collators[code] = new Intl.Collator(code, { numeric: true, sensitivity: "base" }); }
      catch (e) { collators[code] = { compare: function (a, b) { return a < b ? -1 : a > b ? 1 : 0; } }; }
    }
    return collators[code];
  };

  /* ══════════════════════════════════════════════ 7 · مصارف الـDOM */
  var dom = {};
  SB.dom = dom;

  dom.put = function (el, display) { return display.into(el); };

  /* مبلغ داخل جملة: يعيد Html معزولاً — المصرف الوحيد للمبلغ داخل نصّ. */
  dom.amount = function (raw, cls) {
    var d = fmt.amount(raw);
    var span = document.createElement("span");
    span.className = "amt" + (cls ? " " + cls : "");
    d.into(span);
    return new Html(span.outerHTML);
  };
  dom.ltr = function (text) {
    var span = document.createElement("span");
    span.className = "ltr";
    span.textContent = String(text);
    return new Html(span.outerHTML);
  };

  /* ═════════════════════════════════════ 8 · ربط الصفحة (data-i18n) */
  function paramsOf(el) {
    var raw = el.getAttribute("data-i18n-params");
    if (!raw) return null;
    try { return JSON.parse(raw); } catch (e) { return null; }
  }
  function countOf(el) {
    if (el.hasAttribute("data-i18n-count")) return Number(el.getAttribute("data-i18n-count"));
    var sel = el.getAttribute("data-i18n-count-from");
    if (sel) { var src = document.querySelector(sel); if (src) return Number(src.textContent.replace(/[^\d.-]/g, "")); }
    return null;
  }
  function mark(el, key) {
    var had = I18N.missing.length;
    return function () {
      if (I18N.missing.length > had) el.setAttribute("data-i18n-missing", key);
      else el.removeAttribute("data-i18n-missing");
    };
  }

  function applyOne(el) {
    var key = el.getAttribute("data-i18n") || el.getAttribute("data-i18n-html");
    var isHtml = el.hasAttribute("data-i18n-html");
    if (key) {
      var done = mark(el, key);
      var params = paramsOf(el) || {};
      /* معاملات العرض المطلوبة داخل النصّ (مبالغ، رموز لاتينية) */
      var inject = el.getAttribute("data-i18n-amount");
      if (inject) params.amount = dom.amount(inject).__html;
      var ltr = el.getAttribute("data-i18n-ltr");
      if (ltr) params.code = dom.ltr(ltr).__html;
      var c = countOf(el);
      var text = (c === null || isNaN(c)) ? t(key, params) : t.plural(key, c, params);
      if (isHtml) el.innerHTML = text; else el.textContent = text;
      done();
    }
    var attrs = el.getAttribute("data-i18n-attr");
    if (attrs) {
      attrs.split(";").forEach(function (pair) {
        var bits = pair.split(":");
        if (bits.length < 2) return;
        var name = bits[0].trim(), k = bits.slice(1).join(":").trim();
        if (!name || !k) return;
        el.setAttribute(name, t(k, paramsOf(el)));
      });
    }
  }

  I18N.apply = function (root) {
    $$("[data-i18n],[data-i18n-html],[data-i18n-attr]", root || document).forEach(applyOne);
    if (SB.renderAmounts) SB.renderAmounts(root);
    if (SB.bindDateFields) SB.bindDateFields(root);
    return I18N;
  };

  /* نصوص CSS (::before/::after content) — لا يمكن أن تأتي من data-i18n،
     فتُضخّ خصائصَ مخصّصة على :root من مفاتيح اللغة. */
  function applyCssStrings() {
    var m = I18N.meta() || {}, keys = m.cssStrings || [];
    var style = document.documentElement.style;
    keys.forEach(function (name) {
      style.setProperty("--i18n-" + name, JSON.stringify(t("css." + name)));
    });
  }

  /* خط اللغة — يُحقن مرة واحدة من ملف اللغة نفسه، فلا تُعدَّل الصفحات عند إضافة لغة. */
  var fontsDone = {};
  function ensureFont(code) {
    var f = (locales[code] && locales[code].meta.font) || null;
    if (!f) return;
    if (f.href && !fontsDone[f.href]) {
      fontsDone[f.href] = true;
      var link = document.createElement("link");
      link.rel = "stylesheet"; link.href = f.href;
      document.head.appendChild(link);
    }
    /* الخط رمزٌ كالألوان: يُكتب على :root فتتبعه كل المكوّنات بلا اسم خطّ واحد
       مكتوب في أي مكوّن. النستعليق الأردي يحتاج ارتفاع سطر أكبر وإلا تراكبت
       المدّات على السطر الذي فوقه — ولهذا line هنا حقلٌ في ملفّ اللغة. */
    var st = document.documentElement.style;
    /* تُكتب أو تُمسَح دائماً: لغة بلا حقل لا ترث قيمة اللغة التي قبلها. */
    function set(name, v) { if (v) st.setProperty(name, String(v)); else st.removeProperty(name); }
    set("--font-sans", f.ui);
    set("--font-display", f.display || f.ui);
    set("--line-display", f.displayLineHeight);
  }

  /* ═══════════════════════════════════════════ 9 · تفعيل لغة */
  var STORE_KEY = "sb-locale";

  I18N.use = function (code, opts) {
    if (!locales[code]) { if (global.console) console.warn("[SB.i18n] لغة غير محمّلة: " + code); return I18N; }
    active = code;
    var m = locales[code].meta;
    var root = document.documentElement;
    root.setAttribute("lang", m.lang || code);
    root.setAttribute("dir", m.dir || "rtl");
    root.setAttribute("data-locale", code);
    /* إشارة الاتجاه للتحويلات التي لا تنقلب تلقائياً (transform) — انظر tokens.css */
    ensureFont(code);
    if (!(opts && opts.silent)) { try { localStorage.setItem(STORE_KEY, code); } catch (e) {} }
    I18N.apply(document);
    applyCssStrings();
    listeners.forEach(function (fn) { try { fn(code, m); } catch (e) {} });
    document.dispatchEvent(new CustomEvent("sb:localechange", { detail: { locale: code, meta: m } }));
    root.removeAttribute("data-i18n-pending");
    return I18N;
  };

  I18N.onChange = function (fn) { listeners.push(fn); return I18N; };

  /* الأسبقية: ‏?lang= الصريح ← المحفوظ ← لغة المتصفّح ← لغة المصدر.
     ⚠ كان المحفوظ يسبق ?lang=، فلا يستطيع أحد إرسال رابطٍ بلغة بعينها إلى
     مراجعٍ زار الصفحة من قبل: يفتحه فيراها بلغته المخزّنة ويظنّ الرابط معطوباً.
     الطلب الصريح يسبق الذاكرة دائماً، ويُحفَظ بدوره. */
  I18N.preferred = function () {
    var q = /[?&]lang=([\w-]+)/.exec(global.location ? global.location.search : "");
    if (q && locales[q[1]]) return q[1];
    var stored = null;
    try { stored = localStorage.getItem(STORE_KEY); } catch (e) {}
    if (stored && locales[stored]) return stored;
    var navs = (global.navigator && (navigator.languages || [navigator.language])) || [];
    for (var i = 0; i < navs.length; i++) {
      var base = String(navs[i] || "").split("-")[0];
      if (locales[base]) return base;
    }
    return SOURCE;
  };

  /* تحميل ملفّ لغة عند الطلب — بحقن <script>، فيعمل من file:// بلا خادم.
     (fetch على JSON يفشل تحت file:// بسياسة CORS — ولهذا ملفات اللغة .js لا .json.) */
  I18N.load = function (code, done) {
    if (locales[code]) { if (done) done(code); return; }
    var entry = null;
    for (var i = 0; i < I18N.catalog.length; i++) if (I18N.catalog[i].code === code) entry = I18N.catalog[i];
    if (!entry) { if (done) done(null); return; }
    var s = document.createElement("script");
    s.src = (I18N.base || "") + entry.file;
    s.onload = function () { if (done) done(code); };
    s.onerror = function () { if (done) done(null); };
    document.head.appendChild(s);
  };

  I18N.boot = function (code) {
    I18N.debug = /[?&]i18n-debug=1/.test(global.location ? global.location.search : "");
    I18N.use(code || I18N.preferred(), { silent: !!code });
    return I18N;
  };

  /* ══════════════════════════════════════════ 10 · فحص داخل الصفحة
     يُشغَّل من المعرض ومن design/audit.html — بلا خادم وبلا خطوة بناء. */
  SB.audit = SB.audit || {};

  SB.audit.keys = function () {
    var codes = I18N.loaded();
    var union = {}, out = { locales: {}, union: [], orphans: {}, missing: {}, plural: {}, params: {} };
    codes.forEach(function (c) { for (var k in I18N.messages(c)) union[k] = 1; });
    out.union = Object.keys(union).sort();
    var src = I18N.messages(SOURCE);
    codes.forEach(function (c) {
      var msgs = I18N.messages(c), miss = [], orph = [], plu = [], par = [];
      out.union.forEach(function (k) { if (!own(msgs, k)) miss.push(k); });
      for (var k in msgs) if (!own(src, k)) orph.push(k);
      var cats = I18N.pluralCategories(c);
      for (var k2 in msgs) {
        var v = msgs[k2];
        if (isPluralBag(v)) {
          cats.forEach(function (cat) { if (!own(v, cat)) plu.push(k2 + " ← ناقص · missing: " + cat); });
          /* صيغة موجودة لا تستطيع قواعد هذه اللغة اختيارها أبداً = صيغة ميتة */
          for (var cc in v) {
            if (!own(v, cc) || EXACT.test(cc)) continue;
            if (cats.indexOf(cc) === -1) plu.push(k2 + " ← ميتة · dead form: " + cc);
          }
        }
        /* تطابق معاملات الاستبدال مع لغة المصدر */
        if (own(src, k2) && typeof v === "string" && typeof src[k2] === "string") {
          var a = (src[k2].match(/\{\w+\}/g) || []).sort().join(",");
          var b = (v.match(/\{\w+\}/g) || []).sort().join(",");
          if (a !== b) par.push(k2 + " : ar{" + a + "} ≠ " + c + "{" + b + "}");
        }
      }
      out.locales[c] = { count: Object.keys(msgs).length, categories: cats };
      out.missing[c] = miss.sort(); out.orphans[c] = orph.sort();
      out.plural[c] = plu.sort(); out.params[c] = par.sort();
    });
    return out;
  };

  /* اصطلاح التسمية — يُفرَض آلياً، لا يُترك للانضباط الشخصي. */
  var NAMESPACES = ["app", "common", "acct", "field", "screen", "gallery", "css", "audit"];
  var SUFFIXES = ["label", "hint", "error", "ok", "ph", "aria", "title", "sub", "note", "body", "desc"];
  SB.audit.convention = function () {
    var bad = [];
    Object.keys(I18N.messages(SOURCE)).forEach(function (k) {
      var seg = k.split(".");
      if (!/^[a-zA-Z0-9.]+$/.test(k)) bad.push({ key: k, why: "محارف غير ASCII أو فاصل غير النقطة" });
      else if (NAMESPACES.indexOf(seg[0]) === -1) bad.push({ key: k, why: "مجال غير معتمد: " + seg[0] });
      else if (seg.length < 2 || seg.length > 5) bad.push({ key: k, why: "عدد المقاطع " + seg.length + " (المسموح ٢–٥)" });
    });
    return bad;
  };

  /* مسح الصفحة الحيّة بحثاً عن نصّ **بلا مصدر**: نصٌّ ظاهر لا يعود إلى مفتاح.
     ثلاثة أنواع من النصّ ليست كذلك ويجب استثناؤها، وإلا امتلأ التقرير ضجيجاً
     يجعله بلا فائدة:
       ١ · معرّفات وقيم آلية (رقم قيد، بريد، بصمة، اسم رمز CSS، رقم منسّق) —
           وتُعرَف بأصنافها: .ltr .num .amt .acct-code .tag .server-text و<code>.
       ٢ · نصٌّ يكتبه JS من مفاتيح اللغة (عيّنات الألوان، جدول الجمع، التسمية
           المرافقة، التفقيط) — ويُعرَف بسمة data-i18n-managed على الحاوية،
           أو بسمة الملء نفسها على العنصر (data-alt · data-words · data-idx …).
       ٣ · استثناء صريح: data-i18n-exempt.
     وما تبقّى بعد ذلك نصٌّ مطبوع في الشيفرة فعلاً. أمّا الوسم الساكن فيفحصه
     `node design/audit.js` على الملفّات نفسها، وهو الأدقّ لأنه لا يرى إلا ما
     كُتب باليد. */
  var SKIP_TAGS = { SCRIPT: 1, STYLE: 1, CODE: 1, KBD: 1, SVG: 1, NOSCRIPT: 1, TEMPLATE: 1, OPTION: 1 };
  /* ‏.code معرّف آلي، و.toast يبنيه SB.toast من مفتاح دائماً. */
  var MACHINE_CLASS = /(^|\s)(ltr|num|amt|acct-code|tag|code|server-text|name|vals|hash|greg|hijri|dateline|alt-lbl|alt|toast|toasts)(\s|$)/;
  var FILLED_ATTR = ["data-i18n-managed", "data-i18n-exempt", "data-alt", "data-alt-for",
                     "data-alt-sib", "data-words", "data-idx", "data-amount", "data-date",
                     "data-date-short", "data-date-hijri", "data-locale-select"];
  function machineish(el) {
    if (!el || !el.getAttribute) return false;
    for (var i = 0; i < FILLED_ATTR.length; i++) if (el.hasAttribute(FILLED_ATTR[i])) return true;
    var cn = (typeof el.className === "string") ? el.className : "";
    return MACHINE_CLASS.test(cn);
  }
  var LETTERS = /[\u0600-\u06FF\u0900-\u097F\u0750-\u077FA-Za-z]{2,}/;
  var IGNORABLE = /^[\s–—•|/·—–…0-9.,:%+\-()[\]]*$/;
  SB.audit.hardcodedStrings = function (root) {
    var out = [], walker = document.createTreeWalker(root || document.body, NodeFilter.SHOW_TEXT, null);
    var n;
    while ((n = walker.nextNode())) {
      var txt = n.nodeValue.trim();
      if (!txt || IGNORABLE.test(txt) || !LETTERS.test(txt)) continue;
      var el = n.parentElement, guarded = false, tagSkip = false;
      for (var p = el; p && p !== document.body; p = p.parentElement) {
        if (SKIP_TAGS[p.tagName] || machineish(p)) { tagSkip = true; break; }
        if (p.hasAttribute("data-i18n") || p.hasAttribute("data-i18n-html")) { guarded = true; break; }
      }
      if (tagSkip || guarded) continue;
      out.push({ text: txt.slice(0, 90), path: pathOf(el) });
    }
    /* السمات المرئية أيضاً */
    ["placeholder", "aria-label", "title", "alt"].forEach(function (a) {
      $$("[" + a + "]", root || document.body).forEach(function (el) {
        var v = (el.getAttribute(a) || "").trim();
        if (!v || !LETTERS.test(v)) return;
        var da = el.getAttribute("data-i18n-attr") || "";
        if (da.indexOf(a + ":") !== -1 || machineish(el)) return;
        out.push({ text: "@" + a + "=" + v.slice(0, 70), path: pathOf(el) });
      });
    });
    return out;
  };
  function pathOf(el) {
    var bits = [];
    for (var p = el; p && p.tagName && bits.length < 4; p = p.parentElement) {
      bits.unshift(p.tagName.toLowerCase() + (p.id ? "#" + p.id : (p.className && typeof p.className === "string" ? "." + p.className.trim().split(/\s+/)[0] : "")));
    }
    return bits.join(" > ");
  }

  /* قياس حيّ: ماذا تُخرِج Intl فعلاً تحت كل لغة؟ يُعرض في صفحة الفحص. */
  SB.audit.intlHazards = function () {
    var d = new Date(Date.UTC(2026, 7, 24)), rows = [];
    ["ar", "ar-SA", "en", "ur", "hi"].forEach(function (l) {
      var row = { locale: l, number: "", date: "", marks: [] };
      try { row.number = new Intl.NumberFormat(l, { minimumFractionDigits: 2 }).format(-1250.5); } catch (e) {}
      try { row.date = new Intl.DateTimeFormat(l, { dateStyle: "short", timeZone: "UTC" }).format(d); } catch (e) {}
      (row.number + row.date).split("").forEach(function (ch) {
        if (INVISIBLE_RE.test(ch)) {
          var h = "U+" + ch.charCodeAt(0).toString(16).toUpperCase();
          if (row.marks.indexOf(h) === -1) row.marks.push(h);
        }
      });
      rows.push(row);
    });
    return rows;
  };

  global.SB = SB;
})(window);
