/* ═══════════════════════════════════════════════════════════════════════════
   سلاسل بابل ERP — سلوكيات المكوّنات · Component behaviours
   ───────────────────────────────────────────────────────────────────────────
   تحسين تدريجي بلا اعتماديات. كل شيء يعمل بدون هذا الملف؛ هو يضيف التفاعل فقط.
   يُحمَّل بـ <script src="design/components/behaviors.js" defer></script>
   ويعرّف عنصراً عاماً واحداً: window.SB
   ═══════════════════════════════════════════════════════════════════════════ */
(function (global) {
  "use strict";

  var SB = {};

  /* ───────────────────────────────────────────────────── 1 · أدوات صغيرة */
  function $(sel, root) { return (root || document).querySelector(sel); }
  function $$(sel, root) { return Array.prototype.slice.call((root || document).querySelectorAll(sel)); }
  SB.$ = $; SB.$$ = $$;

  /* ══════════════════════════════════════════════════════ 2 · المبالغ
     المبلغ في هذا النظام decimal لا float. الخادم يرسله **نصّاً** بمقياس ثابت
     ("10000.0000")، والواجهة تضيف فواصل الآلاف فقط. لا Intl، ولا parseFloat،
     ولا أي تنسيق يعتمد على لغة الجهاز — راجع docs/evidence/traps.md فخ-18.     */

  /* تقريب نصّي (نصف بعيداً عن الصفر) بلا أي عملية عائمة */
  function roundDigits(intPart, fracPart, scale) {
    if (fracPart.length <= scale) {
      while (fracPart.length < scale) fracPart += "0";
      return [intPart, fracPart];
    }
    var keep = fracPart.slice(0, scale);
    var next = fracPart.charCodeAt(scale) - 48;
    if (next < 5) return [intPart, keep];
    var digits = (intPart + keep).split("");
    var i = digits.length - 1;
    while (i >= 0) {
      if (digits[i] === "9") { digits[i] = "0"; i--; }
      else { digits[i] = String(Number(digits[i]) + 1); break; }
    }
    if (i < 0) digits.unshift("1");
    var all = digits.join("");
    var cut = all.length - scale;
    return [all.slice(0, cut) || "0", all.slice(cut)];
  }

  function group(intPart) {
    var out = "", n = 0;
    for (var i = intPart.length - 1; i >= 0; i--) {
      out = intPart[i] + out;
      if (++n % 3 === 0 && i > 0) out = "," + out;
    }
    return out;
  }

  /* SB.money("10000.5", 2) → "10,000.50"   ·   SB.money("-3.005") → "-3.01" */
  SB.money = function (value, scale) {
    if (scale === undefined || scale === null) scale = 2;
    if (value === null || value === undefined) return "";
    var s = String(value).trim();
    if (s === "" || s === "-" || s === "–") return "";
    s = s.replace(/[\u066B\u066C]/g, function (c) { return c === "\u066B" ? "." : ","; });
    s = SB.toLatinDigits(s).replace(/[,\s\u00A0\u202F\u066C]/g, "");
    var neg = false;
    if (s.charAt(0) === "-") { neg = true; s = s.slice(1); }
    else if (s.charAt(0) === "+") { s = s.slice(1); }
    if (!/^\d*(\.\d*)?$/.test(s) || s === "" || s === ".") return null;
    var parts = s.split(".");
    var ip = (parts[0] || "0").replace(/^0+(?=\d)/, "") || "0";
    var fp = parts[1] || "";
    var r = roundDigits(ip, fp, scale);
    var text = group(r[0]) + (scale > 0 ? "." + r[1] : "");
    if (neg && /[1-9]/.test(r[0] + r[1])) text = "-" + text;
    return text;
  };

  /* تحويل الأرقام العربية-الهندية والشرقية إلى لاتينية — عند الحدّ فقط.
     العرض بالأرقام العربية مسؤولية طبقة العرض ولا يعود إلى التخزين أبداً.
     راجع docs/evidence/traps.md فخ-25. */
  SB.toLatinDigits = function (s) {
    return String(s).replace(/[٠-٩۰-۹]/g, function (d) {
      var c = d.charCodeAt(0);
      return String(c >= 0x06F0 ? c - 0x06F0 : c - 0x0660);
    });
  };

  /* للترتيب فقط — لا تُستعمل نتيجته في أي حساب مالي */
  SB.sortValue = function (text) {
    var s = SB.toLatinDigits(String(text || "")).replace(/[^\d.\-]/g, "");
    var n = parseFloat(s);
    return isFinite(n) ? n : Number.NEGATIVE_INFINITY;
  };

  /* يملأ كل عنصر يحمل data-amount بالقيمة منسّقة، ويضبط حالته */
  SB.renderAmounts = function (root) {
    $$("[data-amount]", root).forEach(function (el) {
      var raw = el.getAttribute("data-amount");
      var scale = Number(el.getAttribute("data-scale") || 2);
      var dash = el.getAttribute("data-dash") !== "false";
      var out = SB.money(raw, scale);
      var zero = out === null || out === "" || /^0(\.0+)?$/.test(out.replace(/,/g, ""));
      if (zero && dash) {
        el.textContent = "–";
        el.classList.add("amt--zero");
      } else {
        el.textContent = out === null ? "؟" : out;
        el.classList.toggle("amt--neg", /^-/.test(out || ""));
        el.classList.remove("amt--zero");
      }
      if (!el.hasAttribute("dir")) el.setAttribute("dir", "ltr");
    });
  };

  /* ═════════════════════════════════════ 3 · حارس المحارف غير المرئية
     ⚠ هذا المشروع يسلسل قيوده بالتجزئة. محرف U+200F واحد يدخل مع لصق أو تحقنه
     طبقة واجهة **يغيّر البصمة ويكسر التحقق** (traps.md فخ-23).
     الواجهة لا تُدخل هذه المحارف أبداً، وتُنبّه المستخدم إن وصلت مع اللصق.
     التنظيف الفعلي مسؤولية حدّ التطبيق، لا المتصفّح.                       */
  /* مكتوبة بالهروب الصريح عمداً: لا يجوز أن يحمل هذا الملف نفسه محرفاً غير مرئي. */
  var INVISIBLE = /[\u200B-\u200F\u061C\u202A-\u202E\u2066-\u2069\uFEFF]/g;
  var INVISIBLE_NAMES = {
    "200B": "مسافة صفرية العرض", "200C": "فاصل عديم العرض", "200D": "واصل عديم العرض",
    "200E": "علامة اليسار إلى اليمين", "200F": "علامة اليمين إلى اليسار",
    "061C": "علامة الحرف العربي", "202A": "تضمين LTR", "202B": "تضمين RTL",
    "202C": "إنهاء التضمين", "202D": "إلغاء LTR", "202E": "إلغاء RTL",
    "2066": "عزل LTR", "2067": "عزل RTL", "2068": "عزل تلقائي", "2069": "إنهاء العزل",
    "FEFF": "علامة ترتيب البايت"
  };

  SB.scanInvisible = function (text) {
    var found = [], m;
    INVISIBLE.lastIndex = 0;
    while ((m = INVISIBLE.exec(String(text || ""))) !== null) {
      var hex = m[0].charCodeAt(0).toString(16).toUpperCase();
      while (hex.length < 4) hex = "0" + hex;
      if (found.indexOf(hex) === -1) found.push(hex);
    }
    return found.map(function (h) { return { code: "U+" + h, name: INVISIBLE_NAMES[h] || "محرف تحكّم" }; });
  };

  SB.guardInvisible = function (root) {
    $$("[data-guard-invisible]", root || document).forEach(function (el) {
      if (el.__sbGuarded) return;
      el.__sbGuarded = true;
      var check = function () {
        var hits = SB.scanInvisible(el.value);
        var box = document.getElementById(el.getAttribute("data-guard-invisible"));
        if (hits.length) {
          el.setAttribute("aria-invalid", "true");
          if (box) {
            box.hidden = false;
            box.textContent = "النصّ يحتوي محارف تحكّم غير مرئية (" +
              hits.map(function (h) { return h.code + " " + h.name; }).join("، ") +
              ") — سيرفضها الخادم لأنها تغيّر بصمة القيد. احذفها وأعد الكتابة يدوياً.";
          }
        } else {
          el.removeAttribute("aria-invalid");
          if (box) { box.hidden = true; box.textContent = ""; }
        }
      };
      el.addEventListener("input", check);
      el.addEventListener("blur", check);
      el.addEventListener("paste", function () { setTimeout(check, 0); });
    });
  };

  /* ═══════════════════════════════════════════════════════ 4 · التواريخ
     التخزين ميلادي دائماً. الهجري عرض فقط ولا يعود إلى التخزين. */
  var MONTHS_AR = ["يناير","فبراير","مارس","أبريل","مايو","يونيو",
                   "يوليو","أغسطس","سبتمبر","أكتوبر","نوفمبر","ديسمبر"];
  var MONTHS_HIJRI = ["محرم","صفر","ربيع الأول","ربيع الآخر","جمادى الأولى","جمادى الآخرة",
                      "رجب","شعبان","رمضان","شوال","ذو القعدة","ذو الحجة"];
  var WEEKDAYS_AR = ["الأحد","الإثنين","الثلاثاء","الأربعاء","الخميس","الجمعة","السبت"];
  SB.MONTHS_AR = MONTHS_AR; SB.MONTHS_HIJRI = MONTHS_HIJRI;

  SB.parseDate = function (text) {
    var s = SB.toLatinDigits(String(text || "")).trim().replace(/[-.]/g, "/");
    var m = /^(\d{4})\/(\d{1,2})\/(\d{1,2})$/.exec(s);
    if (!m) return null;
    var d = new Date(Date.UTC(+m[1], +m[2] - 1, +m[3]));
    return isNaN(d.getTime()) ? null : d;
  };

  /* "٢٤ مايو ٢٠٢٦" ممنوع — الأرقام تبقى لاتينية دائماً */
  SB.gregorianAr = function (date) {
    if (!date) return "—";
    return WEEKDAYS_AR[date.getUTCDay()] + "، " + date.getUTCDate() + " " +
           MONTHS_AR[date.getUTCMonth()] + " " + date.getUTCFullYear() + " م";
  };

  SB.hijriAr = function (date) {
    if (!date) return null;
    try {
      var f = new Intl.DateTimeFormat("en-u-ca-islamic-umalqura-nu-latn",
        { day: "numeric", month: "numeric", year: "numeric", timeZone: "UTC" });
      var parts = {}, list = f.formatToParts(date);
      for (var i = 0; i < list.length; i++) parts[list[i].type] = list[i].value;
      if (!parts.year || !parts.month || !parts.day) return null;
      var mi = parseInt(parts.month, 10) - 1;
      if (!(mi >= 0 && mi < 12)) return null;
      return parts.day + " " + MONTHS_HIJRI[mi] + " " + parts.year + " هـ";
    } catch (e) { return null; }
  };

  SB.bindDateFields = function (root) {
    $$(".datefield", root || document).forEach(function (f) {
      var input = $("input", f), greg = $(".greg", f), hijri = $(".hijri", f);
      if (!input) return;
      var render = function () {
        var d = SB.parseDate(input.value);
        if (greg) greg.textContent = SB.gregorianAr(d);
        if (hijri) {
          var h = SB.hijriAr(d);
          hijri.hidden = !h;
          if (h) hijri.textContent = h;
        }
      };
      input.addEventListener("input", render);
      input.addEventListener("change", render);
      render();
    });
  };

  /* ═════════════════════════════════════════════════════ 5 · التفقيط
     منقول كما هو من النموذج المعتمد — لا يُعاد تصميمه. */
  var ONES  = ["","واحد","اثنان","ثلاثة","أربعة","خمسة","ستة","سبعة","ثمانية","تسعة"];
  var TEENS = ["عشرة","أحد عشر","اثنا عشر","ثلاثة عشر","أربعة عشر","خمسة عشر","ستة عشر","سبعة عشر","ثمانية عشر","تسعة عشر"];
  var TENS  = ["","","عشرون","ثلاثون","أربعون","خمسون","ستون","سبعون","ثمانون","تسعون"];
  var HUNS  = ["","مائة","مائتان","ثلاثمائة","أربعمائة","خمسمائة","ستمائة","سبعمائة","ثمانمائة","تسعمائة"];

  function under1000(n) {
    var out = [], h = Math.floor(n / 100), r = n % 100;
    if (h) out.push(HUNS[h]);
    if (r < 10 && r > 0) out.push(ONES[r]);
    else if (r >= 10 && r < 20) out.push(TEENS[r - 10]);
    else if (r >= 20) {
      var t = Math.floor(r / 10), o = r % 10;
      out.push(o ? ONES[o] + " و" + TENS[t] : TENS[t]);
    }
    return out.join(" و");
  }
  function groupWord(count, forms) {
    if (count === 1) return forms[0];
    if (count === 2) return forms[1];
    if (count <= 10) return under1000(count) + " " + forms[2];
    return under1000(count) + " " + forms[3];
  }
  SB.tafqeet = function (amount) {
    var n = Math.floor(Math.abs(amount));
    var halalas = Math.round((Math.abs(amount) - n) * 100);
    if (n === 0 && halalas === 0) return "صفر ريال";
    var parts = [];
    var scales = [
      [1e9, ["مليار","ملياران","مليارات","ملياراً"]],
      [1e6, ["مليون","مليونان","ملايين","مليوناً"]],
      [1e3, ["ألف","ألفان","آلاف","ألفاً"]]
    ];
    for (var i = 0; i < scales.length; i++) {
      var c = Math.floor(n / scales[i][0]);
      if (c) { parts.push(groupWord(c, scales[i][1])); n -= c * scales[i][0]; }
    }
    if (n) parts.push(under1000(n));
    var words = parts.join(" و") + " ريال";
    if (halalas) words += " و" + under1000(halalas) + " هللة";
    return (amount < 0 ? "سالب " : "") + words;
  };

  /* ═══════════════════════════════════════════════════════ 6 · السمة */
  var STORE_KEY = "sb-theme";
  SB.getTheme = function () {
    var root = document.documentElement;
    return root.getAttribute("data-theme") ||
      (global.matchMedia && global.matchMedia("(prefers-color-scheme: dark)").matches ? "dark" : "light");
  };
  SB.setTheme = function (t) {
    document.documentElement.setAttribute("data-theme", t);
    try { localStorage.setItem(STORE_KEY, t); } catch (e) { /* وضع خاص أو تخزين محجوب */ }
    $$("[data-theme-toggle]").forEach(function (b) {
      b.setAttribute("aria-pressed", String(t === "dark"));
      var lbl = b.querySelector("[data-theme-label]");
      if (lbl) lbl.textContent = t === "dark" ? "المظهر الداكن" : "المظهر الفاتح";
    });
  };
  SB.toggleTheme = function () { SB.setTheme(SB.getTheme() === "dark" ? "light" : "dark"); };
  SB.restoreTheme = function () {
    var t = null;
    try { t = localStorage.getItem(STORE_KEY); } catch (e) { t = null; }
    if (t === "dark" || t === "light") SB.setTheme(t);
    else SB.setTheme(SB.getTheme());
  };

  /* ══════════════════════════════════════════════ 7 · تبويبات وقوائم */
  SB.bindTabs = function (root) {
    $$("[role='tablist']", root || document).forEach(function (list) {
      var tabs = $$("[role='tab']", list);
      function select(tab) {
        tabs.forEach(function (t) {
          var on = t === tab;
          t.setAttribute("aria-selected", String(on));
          t.tabIndex = on ? 0 : -1;
          var panel = document.getElementById(t.getAttribute("aria-controls") || t.dataset.panel || "");
          if (panel) panel.hidden = !on;
        });
      }
      tabs.forEach(function (tab, i) {
        tab.addEventListener("click", function () { select(tab); });
        tab.addEventListener("keydown", function (e) {
          var dir = e.key === "ArrowLeft" ? 1 : e.key === "ArrowRight" ? -1 : 0; /* RTL */
          if (!dir) return;
          e.preventDefault();
          var next = tabs[(i + dir + tabs.length) % tabs.length];
          next.focus(); select(next);
        });
      });
    });
  };

  SB.bindMenus = function (root) {
    (root || document).addEventListener("click", function (e) {
      var trig = e.target.closest ? e.target.closest("[data-menu-trigger]") : null;
      $$("[data-menu]").forEach(function (m) {
        if (!trig || m !== document.getElementById(trig.getAttribute("data-menu-trigger"))) {
          m.dataset.open = "false";
          var owner = $("[data-menu-trigger='" + m.id + "']");
          if (owner) owner.setAttribute("aria-expanded", "false");
        }
      });
      if (trig) {
        var menu = document.getElementById(trig.getAttribute("data-menu-trigger"));
        if (menu) {
          var open = menu.dataset.open === "true";
          menu.dataset.open = open ? "false" : "true";
          trig.setAttribute("aria-expanded", open ? "false" : "true");
        }
      }
    });
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape") $$("[data-menu]").forEach(function (m) { m.dataset.open = "false"; });
    });
  };

  /* ═════════════════════════════════════ 8 · نوافذ وأدراج وشريط جانبي */
  var lastFocus = null;
  function trapFocus(container, e) {
    var f = $$("a[href],button:not([disabled]),input:not([disabled]),select:not([disabled]),textarea:not([disabled]),[tabindex]:not([tabindex='-1'])", container)
      .filter(function (el) { return el.offsetParent !== null; });
    if (!f.length) return;
    var first = f[0], last = f[f.length - 1];
    if (e.shiftKey && document.activeElement === first) { e.preventDefault(); last.focus(); }
    else if (!e.shiftKey && document.activeElement === last) { e.preventDefault(); first.focus(); }
  }

  SB.openDialog = function (id) {
    var el = document.getElementById(id); if (!el) return;
    lastFocus = document.activeElement;
    el.dataset.open = "true";
    document.body.style.overflow = "hidden";
    var focusable = $("[autofocus],button,input,select,textarea,a[href]", el);
    if (focusable) focusable.focus();
  };
  SB.closeDialog = function (id) {
    var el = document.getElementById(id); if (!el) return;
    el.dataset.open = "false";
    document.body.style.overflow = "";
    if (lastFocus && lastFocus.focus) lastFocus.focus();
  };
  SB.bindDialogs = function () {
    document.addEventListener("click", function (e) {
      var open = e.target.closest ? e.target.closest("[data-open-dialog]") : null;
      if (open) { SB.openDialog(open.getAttribute("data-open-dialog")); return; }
      var close = e.target.closest ? e.target.closest("[data-close-dialog]") : null;
      if (close) { SB.closeDialog(close.getAttribute("data-close-dialog")); return; }
      if (e.target.classList && e.target.classList.contains("overlay") && e.target.dataset.open === "true") {
        SB.closeDialog(e.target.id);
      }
    });
    document.addEventListener("keydown", function (e) {
      if (e.key === "Escape") {
        $$("[data-open='true']").forEach(function (el) {
          if (el.classList.contains("overlay") || el.classList.contains("drawer")) SB.closeDialog(el.id);
        });
      }
      if (e.key === "Tab") {
        var open = $(".overlay[data-open='true'] .sheet") || $(".drawer[data-open='true']");
        if (open) trapFocus(open, e);
      }
    });
  };

  SB.bindNav = function () {
    var nav = $(".sidenav"), scrim = $(".navscrim");
    $$("[data-nav-toggle]").forEach(function (b) {
      b.addEventListener("click", function () {
        if (!nav) return;
        var open = nav.dataset.open === "true";
        nav.dataset.open = open ? "false" : "true";
        if (scrim) scrim.dataset.open = open ? "false" : "true";
        b.setAttribute("aria-expanded", String(!open));
      });
    });
    if (scrim) scrim.addEventListener("click", function () {
      if (nav) nav.dataset.open = "false";
      scrim.dataset.open = "false";
      $$("[data-nav-toggle]").forEach(function (b) { b.setAttribute("aria-expanded", "false"); });
    });
    /* القوائم القابلة للطيّ داخل التنقّل */
    $$("[data-subnav]").forEach(function (b) {
      b.addEventListener("click", function () {
        var panel = document.getElementById(b.getAttribute("data-subnav"));
        if (!panel) return;
        var open = b.getAttribute("aria-expanded") === "true";
        b.setAttribute("aria-expanded", String(!open));
        panel.hidden = open;
      });
    });
  };

  /* ═══════════════════════════════════════════ 9 · الإشعارات العابرة */
  SB.toast = function (msg, variant, ms) {
    var host = $(".toasts");
    if (!host) { host = document.createElement("div"); host.className = "toasts"; document.body.appendChild(host); }
    var t = document.createElement("div");
    t.className = "toast" + (variant ? " toast--" + variant : "");
    t.setAttribute("role", "status");
    t.textContent = msg;
    host.appendChild(t);
    requestAnimationFrame(function () { t.dataset.show = "true"; });
    setTimeout(function () {
      t.dataset.show = "false";
      setTimeout(function () { if (t.parentNode) t.parentNode.removeChild(t); }, 260);
    }, ms || 2600);
    return t;
  };

  /* ══════════════════════════════════════════════ 10 · فرز الجداول */
  SB.bindSortableTables = function (root) {
    $$("table[data-sortable]", root || document).forEach(function (table) {
      var headers = $$("thead th.sortable", table);
      headers.forEach(function (th, index) {
        var btn = $("button", th);
        if (!btn) return;
        var col = th.hasAttribute("data-col") ? Number(th.getAttribute("data-col")) : index;
        btn.addEventListener("click", function () {
          var dir = th.getAttribute("aria-sort") === "ascending" ? "descending" : "ascending";
          headers.forEach(function (h) { h.setAttribute("aria-sort", "none"); });
          th.setAttribute("aria-sort", dir);
          var tbody = $("tbody", table);
          var numeric = th.classList.contains("n");
          /* الفرز يقع **داخل كل مجموعة**: صفوف العناوين والمجاميع الفرعية
             تحمل data-fixed وتبقى في مكانها، فلا تنهار الهرمية عند الفرز. */
          var all = $$("tr", tbody), seg = [], order = [];
          function flush() {
            if (!seg.length) return;
            seg.sort(function (a, b) {
              var av = a.cells[col] ? a.cells[col].textContent.trim() : "";
              var bv = b.cells[col] ? b.cells[col].textContent.trim() : "";
              var r = numeric ? SB.sortValue(av) - SB.sortValue(bv) : av.localeCompare(bv, "ar");
              return dir === "ascending" ? r : -r;
            });
            order = order.concat(seg); seg = [];
          }
          all.forEach(function (r) {
            if (r.hasAttribute("data-fixed")) { flush(); order.push(r); }
            else seg.push(r);
          });
          flush();
          order.forEach(function (r) { tbody.appendChild(r); });
          SB.toast("رُتّب حسب: " + (btn.textContent || "").trim(), null, 1600);
        });
      });
    });
  };

  /* ══════════════════════════════════════════ 11 · منتقي الحساب */
  SB.bindPickers = function (root) {
    $$(".picker", root || document).forEach(function (p) {
      var input = $("input", p), list = $(".picker-list", p);
      if (!input || !list) return;
      var opts = $$(".picker-opt", list);
      var empty = $(".picker-empty", list);
      function filter() {
        var q = SB.toLatinDigits(input.value.trim().toLowerCase());
        var shown = 0;
        opts.forEach(function (o) {
          var hay = SB.toLatinDigits(o.textContent.toLowerCase());
          var hit = !q || hay.indexOf(q) !== -1;
          o.hidden = !hit;
          if (hit) shown++;
        });
        if (empty) empty.hidden = shown > 0;
      }
      input.addEventListener("focus", function () { list.dataset.open = "true"; filter(); });
      input.addEventListener("input", function () { list.dataset.open = "true"; filter(); });
      input.addEventListener("keydown", function (e) {
        if (e.key === "Escape") { list.dataset.open = "false"; }
        if (e.key === "ArrowDown") {
          var first = opts.filter(function (o) { return !o.hidden; })[0];
          if (first) { e.preventDefault(); first.focus(); }
        }
      });
      list.addEventListener("click", function (e) {
        var opt = e.target.closest(".picker-opt"); if (!opt) return;
        opts.forEach(function (o) { o.setAttribute("aria-selected", String(o === opt)); });
        var code = $(".code", opt), label = $(".label", opt);
        input.value = (code ? code.textContent.trim() + " — " : "") + (label ? label.textContent.trim() : "");
        list.dataset.open = "false";
        input.focus();
      });
      document.addEventListener("click", function (e) {
        if (!p.contains(e.target)) list.dataset.open = "false";
      });
    });
  };

  /* ══════════════════════════════════════════════════ 12 · التشغيل */
  SB.init = function (root) {
    SB.restoreTheme();
    $$("[data-theme-toggle]").forEach(function (b) {
      if (b.__sbBound) return; b.__sbBound = true;
      b.addEventListener("click", SB.toggleTheme);
    });
    SB.bindTabs(root); SB.bindMenus(root); SB.bindDialogs(); SB.bindNav();
    SB.bindSortableTables(root); SB.bindPickers(root); SB.bindDateFields(root);
    SB.guardInvisible(root); SB.renderAmounts(root);
  };

  if (document.readyState === "loading") {
    document.addEventListener("DOMContentLoaded", function () { SB.init(); });
  } else { SB.init(); }

  global.SB = SB;
})(window);
