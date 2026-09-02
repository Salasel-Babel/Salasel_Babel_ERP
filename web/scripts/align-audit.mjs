#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   مقياس الاستقامة — يقيس انحراف الحقول عن سطرٍ واحد، ولا يُصلحه
   The alignment instrument — it measures how far fields drift off one line
   ───────────────────────────────────────────────────────────────────────────
   **لماذا قياسٌ لا قائمةُ مُحدِّدات مكتوبة بيد:** الشاشة التي لم يخطر لأحدٍ
   أن يكتب مُحدِّدها هي بالضبط الشاشة التي ينكسر فيها الصفّ. فـ«ما هو صفٌّ
   واحد» يُشتقّ هنا من **الهندسة والشجرة معاً** لا من اسمٍ في مصفوفة:

     ١ · الوحدة (حقل)  = أبعدُ سلفٍ لعنصر تحكّمٍ مرئي **لا يزال يحوي عنصر
         تحكّمٍ واحداً**. فيلتقط `.field` المكتوب باليد و`<Field>` الأوّليّة
         و`.check` و`.switch` سواءً — بلا اسمٍ واحد مكتوب هنا.
     ٢ · وعاء الصفّ    = أقربُ سلفٍ للوحدة **له ابنان اثنان على الأقلّ يحوي
         كلٌّ منهما وحدة**. فيتسلّق الأغلفة ذات الابن الواحد ولا يتوقّف عندها.
     ٣ · الصفّ         = وحداتٌ في الوعاء نفسه **تتقاطع نطاقاتها الرأسية**،
         في خانتين مختلفتين على الأقلّ.

   ثم يُقاس في كل صفّ: حافّةُ أعلى **عنصر التحكّم** (لا الصندوق)، وخطُّ قاعدة
   السطر الأول من **التسمية**، وحافّةُ أعلى **كتلة الوصف**. والانحراف بالبكسل.

       node scripts/align-audit.mjs [--no-build] [--shots] [--web-port 5311]

   والناتج: JSON في `artifacts/align/align-report.json` ولقطاتٌ إلى جانبه.
   ═══════════════════════════════════════════════════════════════════════════ */
import { spawn } from "node:child_process";
import { existsSync, mkdirSync, writeFileSync } from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { chromium } from "@playwright/test";

const WEB_ROOT = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const REPO_ROOT = path.resolve(WEB_ROOT, "..");

const argv = process.argv.slice(2);
const flag = (name) => argv.includes("--" + name);
const opt = (name, fallback) => {
  const i = argv.indexOf("--" + name);
  return i >= 0 && argv[i + 1] ? argv[i + 1] : fallback;
};

const WEB_PORT = Number(opt("web-port", "5311"));
const MOCK_PORT = Number(opt("mock-port", "5312"));
const OUT_DIR = path.resolve(REPO_ROOT, opt("out", "artifacts/align"));
const SHOTS = !flag("no-shots");
const CHROMIUM = [process.env.PLAYWRIGHT_CHROMIUM, "/opt/pw-browsers/chromium"].find(
  (c) => !!c && existsSync(c)
);

/* المسارات من `src/app/shell/sections.ts` — تُقرأ من الملفّ نفسه فلا تنحرف نسخةٌ
   ثانية عن الأصل. */
async function screenPaths() {
  const src = await import("node:fs/promises").then((fs) =>
    fs.readFile(path.join(WEB_ROOT, "src/app/shell/sections.ts"), "utf8")
  );
  const block = src.slice(src.indexOf("export const SCREENS"));
  const paths = [...block.matchAll(/\{\s*path:\s*"([^"]+)"/g)].map((m) => m[1]);
  if (paths.length === 0) throw new Error("لم يُقرأ أي مسار من SCREENS. / no SCREENS paths read.");
  return paths;
}

const COMPANY = "11111111-1111-4111-8111-111111111111";

/* عروضٌ خمسة × لغتان. **والعروض ليست زينة**: العطل بنيويّ يظهر متى التفّ نصّ،
   والالتفاف دالّةُ عرض العمود. فالمسح يُري متى ينكسر الصفّ لا أنه ينكسر. */
const VIEWPORTS = [
  { width: 1440, height: 900, name: "1440" },
  { width: 1280, height: 900, name: "1280" },
  { width: 1180, height: 900, name: "1180" },
  { width: 1024, height: 800, name: "1024" },
  { width: 390, height: 844, name: "390" },
];
const ALL_LOCALES = [
  { locale: "ar", dir: "rtl" },
  { locale: "en", dir: "ltr" },
  { locale: "ur", dir: "rtl" },
  { locale: "hi", dir: "ltr" },
];
/* الافتراض لغتان (اتّجاهان)، و`--locales ur,hi` يفتح الأربع — والأردية بخطّ
   النستعليق أطولُ سطراً وأعلى، فهي الحدّ الأقصى لا الحالة النادرة. */
const WANT_LOCALES = (opt("locales", "ar,en") || "").split(",").filter(Boolean);
const WANT_WIDTHS = (opt("widths", "") || "").split(",").filter(Boolean);
const LOCALES = ALL_LOCALES.filter((l) => WANT_LOCALES.includes(l.locale));
const SHOT_AT = new Set(["ar-1440", "ar-390"]);
const PASSES = LOCALES.flatMap((l) =>
  VIEWPORTS.filter((v) => WANT_WIDTHS.length === 0 || WANT_WIDTHS.includes(v.name)).map((v) => ({
    locale: l.locale,
    dir: l.dir,
    viewport: { width: v.width, height: v.height },
    tag: l.locale + "-" + v.name,
    shots: SHOT_AT.has(l.locale + "-" + v.name),
  }))
);

/* ══════════════════════════════════════════ ما يُنفَّذ داخل الصفحة (متصفّح) */

/**
 * يقيس صفحةً واحدة. **كل ما هنا يُسلسَل إلى المتصفّح** فلا يستعمل شيئاً من خارجه.
 * @returns {{units:number, rows:Array}} الوحدات والصفوف بقياساتها.
 */
function measureInPage() {
  const CONTROL_SEL =
    'input:not([type="hidden"]), select, textarea, [role="combobox"], [role="spinbutton"], [contenteditable="true"]';

  const sy = window.scrollY;
  const sx = window.scrollX;
  const box = (el) => {
    const r = el.getBoundingClientRect();
    return { top: r.top + sy, bottom: r.bottom + sy, left: r.left + sx, right: r.right + sx, w: r.width, h: r.height };
  };
  const visible = (el) => {
    const r = el.getBoundingClientRect();
    if (r.width <= 0 || r.height <= 0) return false;
    const cs = getComputedStyle(el);
    return cs.visibility !== "hidden" && cs.display !== "none" && Number(cs.opacity) > 0.01;
  };

  /* ── ١ · الوحدات ─────────────────────────────────────────────────────── */
  const controls = [...document.querySelectorAll(CONTROL_SEL)].filter(visible);
  const visibleControlCount = (el) =>
    [...el.querySelectorAll(CONTROL_SEL)].filter((c) => controls.includes(c)).length;

  const units = [];
  for (const c of controls) {
    let unit = c;
    let node = c;
    while (node.parentElement && node.parentElement !== document.body) {
      const p = node.parentElement;
      if (visibleControlCount(p) > 1) break;
      unit = p;
      node = p;
    }
    if (!units.some((u) => u.el === unit)) units.push({ el: unit, control: c });
  }
  units.forEach((u, i) => u.el.setAttribute("data-align-unit", String(i)));

  /* ── ٢ · وعاء الصفّ: أقرب سلفٍ له خانتان تحويان وحدةً ─────────────────── */
  const hasUnit = (el) => el.hasAttribute("data-align-unit") || !!el.querySelector("[data-align-unit]");
  const rowParentOf = (unitEl) => {
    let child = unitEl;
    let p = child.parentElement;
    while (p && p !== document.documentElement) {
      const slots = [...p.children].filter(hasUnit);
      if (slots.length >= 2) return { parent: p, slot: child };
      child = p;
      p = p.parentElement;
    }
    return null;
  };

  /* ── ٣ · التسمية والوصف داخل الوحدة ──────────────────────────────────── */
  const rectsOf = (el) => {
    const r = document.createRange();
    r.selectNodeContents(el);
    return [...r.getClientRects()].filter((x) => x.width > 0.5 && x.height > 0.5);
  };
  /** عدد الأسطر = عدد قيم `top` المتمايزة بين مستطيلات المدى. */
  const lineCount = (el) => {
    const rs = rectsOf(el);
    return rs.length === 0 ? 0 : new Set(rs.map((r) => Math.round(r.top))).size;
  };
  /** مستطيل السطر الأول — أعلاه وأسفله (وأسفلُه بديلٌ ثابت عن خطّ القاعدة). */
  const firstLine = (el) => {
    const rs = rectsOf(el).sort((a, b) => a.top - b.top || a.left - b.left);
    if (rs.length === 0) return null;
    const t = rs[0].top;
    const line = rs.filter((r) => Math.abs(r.top - t) < 1.5);
    return { top: t + sy, bottom: Math.max(...line.map((r) => r.bottom)) + sy };
  };

  const labelOf = (u) => {
    if (u.el.tagName === "LABEL") return u.el;
    const byFor = u.control.id ? u.el.querySelector('label[for="' + CSS.escape(u.control.id) + '"]') : null;
    return byFor ?? u.el.querySelector("label") ?? null;
  };
  /* الوصف: أوّل عنصرٍ **بعد** عنصر التحكّم في ترتيب المستند، فيه نصّ، ولا
     عنصر تحكّمٍ فيه. فيلتقط `.hint` و`.field-error` وأي وصفٍ لم يُسمَّ بعد. */
  const descOf = (u) => {
    const all = [...u.el.querySelectorAll("*")];
    const ci = all.indexOf(u.control);
    for (let i = ci + 1; i < all.length; i += 1) {
      const e = all[i];
      if (e.contains(u.control) || u.control.contains(e)) continue;
      if (e.querySelector(CONTROL_SEL) || e.matches(CONTROL_SEL)) continue;
      if (e.tagName === "OPTION" || e.tagName === "SVG" || e.tagName === "PATH") continue;
      if (!(e.textContent || "").trim()) continue;
      if (!visible(e)) continue;
      const pos = getComputedStyle(e).position;
      if (pos === "absolute" || pos === "fixed") continue; /* زينةٌ خارج التدفّق لا وصف */
      return e;
    }
    return null;
  };

  const scopeOf = (el) =>
    el.closest(".app-topbar, .app-side") ? "shell" : el.closest("dialog, [role='dialog']") ? "dialog" : "page";

  /* مسطرةٌ خفيّة تقيس عرض النصّ **لو لم يلتفّ**: الفرق بينه وبين العرض المتاح
     هو «المتّسع» — كم بكسلاً يفصل هذا الحقل عن أن ينكسر صفّه. والعطل البنيوي
     يُقاس بهذا لا بلقطةٍ واحدة. */
  const ruler = document.createElement("span");
  ruler.style.cssText = "position:absolute;visibility:hidden;white-space:pre;inset-block-start:-9999px";
  document.body.appendChild(ruler);
  const headroom = (el) => {
    if (!el) return null;
    const cs = getComputedStyle(el);
    ruler.style.font = cs.font;
    ruler.style.letterSpacing = cs.letterSpacing;
    ruler.textContent = (el.textContent || "").replace(/\s+/g, " ").trim();
    const natural = ruler.getBoundingClientRect().width;
    const avail = el.getBoundingClientRect().width;
    return Math.round((avail - natural) * 10) / 10;
  };

  const pathOf = (el) => {
    const bits = [];
    let n = el;
    for (let i = 0; n && i < 4; i += 1, n = n.parentElement) {
      const cls = (n.getAttribute("class") || "").trim().split(/\s+/).filter(Boolean).slice(0, 3).join(".");
      bits.unshift(n.tagName.toLowerCase() + (cls ? "." + cls : ""));
      if (n.parentElement === document.body) break;
    }
    return bits.join(" > ");
  };

  /* ── ٤ · التجميع في صفوف ─────────────────────────────────────────────── */
  const groups = new Map();
  for (const u of units) {
    const rp = rowParentOf(u.el);
    if (!rp) continue;
    u.slot = rp.slot;
    u.rect = box(u.el);
    if (!groups.has(rp.parent)) groups.set(rp.parent, []);
    groups.get(rp.parent).push(u);
  }

  const rows = [];
  const rhythms = [];
  for (const [parent, list] of groups) {
    list.sort((a, b) => a.rect.top - b.rect.top);
    const bands = [];
    for (const u of list) {
      const band = bands.find((b) => {
        const overlap = Math.min(b.bottom, u.rect.bottom) - Math.max(b.top, u.rect.top);
        return overlap > 0.5 * Math.min(b.bottom - b.top, u.rect.h);
      });
      if (band) {
        band.items.push(u);
        band.top = Math.min(band.top, u.rect.top);
        band.bottom = Math.max(band.bottom, u.rect.bottom);
      } else {
        bands.push({ top: u.rect.top, bottom: u.rect.bottom, items: [u] });
      }
    }
    if (bands.length >= 2) {
      const tops = bands
        .map((b) => Math.min(...b.items.map((u) => box(u.control).top)))
        .sort((a, b) => a - b);
      const deltas = tops.slice(1).map((t, i) => Math.round((t - tops[i]) * 100) / 100);
      const lo = Math.min(...deltas);
      const hi = Math.max(...deltas);
      rhythms.push({
        parent: pathOf(parent),
        parentClass: parent.getAttribute("class") || "",
        scope: scopeOf(parent),
        bands: bands.length,
        singleColumn: bands.every((b) => b.items.length === 1),
        deltas,
        spread: Math.round((hi - lo) * 100) / 100,
      });
    }
    for (const band of bands) {
      const slots = new Set(band.items.map((u) => u.slot));
      if (band.items.length < 2 || slots.size < 2) continue;

      const cs = getComputedStyle(parent);
      const members = band.items.map((u) => {
        const lab = labelOf(u);
        const desc = descOf(u);
        const cbox = box(u.control);
        return {
          control: {
            tag: u.control.tagName.toLowerCase(),
            type: u.control.getAttribute("type") || "",
            id: u.control.id || "",
            testId: u.control.getAttribute("data-testid") || "",
            top: cbox.top,
            bottom: cbox.bottom,
            h: cbox.h,
            w: cbox.w,
          },
          label: lab
            ? {
                text: (lab.textContent || "").trim().slice(0, 48),
                lines: lineCount(lab),
                ...(firstLine(lab) || { top: null, bottom: null }),
                boxTop: box(lab).top,
              }
            : null,
          desc: desc
            ? {
                cls: desc.getAttribute("class") || "",
                text: (desc.textContent || "").trim().slice(0, 48),
                lines: lineCount(desc),
                top: box(desc).top,
                h: box(desc).h,
              }
            : null,
          unitTop: u.rect.top,
          unitBottom: u.rect.bottom,
          /* «القاع المسنَّن»: ما بين أسفل التحكّم وأسفل صندوق الحقل — وهو ما
             يشغله الوصف. تفاوتُه هو ما يُرى فجوةً تحت حقلٍ وازدحاماً تحت آخر. */
          gutter: Math.round((u.rect.bottom - cbox.bottom) * 100) / 100,
          /* «قاع الحبر»: أسفلُ آخر ما يُرى في الحقل — التحكّم أو وصفه. صندوقُ
             الحقل يتمدّد فيتساوى، والحبر لا يتمدّد، فهذا هو ما تراه العين. */
          inkBottom: Math.max(cbox.bottom, desc ? box(desc).bottom : cbox.bottom),
          labelHeadroom: lab ? headroom(lab) : null,
          descHeadroom: desc ? headroom(desc) : null,
        };
      });

      const spread = (values) => {
        const v = values.filter((x) => typeof x === "number" && Number.isFinite(x));
        if (v.length < 2) return null;
        const sorted = [...v].sort((a, b) => a - b);
        const mid = sorted.length % 2 ? sorted[(sorted.length - 1) / 2] : (sorted[sorted.length / 2 - 1] + sorted[sorted.length / 2]) / 2;
        return {
          n: v.length,
          max: Math.round((sorted[sorted.length - 1] - sorted[0]) * 100) / 100,
          mad: Math.round((v.reduce((s, x) => s + Math.abs(x - mid), 0) / v.length) * 100) / 100,
        };
      };

      rows.push({
        parent: pathOf(parent),
        parentClass: parent.getAttribute("class") || "",
        scope: scopeOf(parent),
        alignItems: cs.alignItems,
        display: cs.display,
        members: members.length,
        band: { top: Math.round(band.top), bottom: Math.round(band.bottom) },
        left: Math.round(Math.min(...band.items.map((u) => u.rect.left))),
        right: Math.round(Math.max(...band.items.map((u) => u.rect.right))),
        controlTop: spread(members.map((m) => m.control.top)),
        controlBottom: spread(members.map((m) => m.control.bottom)),
        labelFirstLineTop: spread(members.map((m) => m.label && m.label.top)),
        labelBaseline: spread(members.map((m) => m.label && m.label.bottom)),
        descTop: spread(members.map((m) => m.desc && m.desc.top)),
        unitBottom: spread(members.map((m) => m.unitBottom)),
        gutter: spread(members.map((m) => m.gutter)),
        inkBottom: spread(members.map((m) => m.inkBottom)),
        raggedDesc: new Set(members.map((m) => (m.desc ? m.desc.lines : 0))).size > 1,
        minLabelHeadroom: Math.min(...members.map((m) => (m.labelHeadroom === null ? 9999 : m.labelHeadroom))),
        minDescHeadroom: Math.min(...members.map((m) => (m.descHeadroom === null ? 9999 : m.descHeadroom))),
        labelLines: members.map((m) => (m.label ? m.label.lines : null)),
        descLines: members.map((m) => (m.desc ? m.desc.lines : null)),
        withDesc: members.filter((m) => !!m.desc).length,
        detail: members,
      });
    }
  }

  /* ══ عائلةٌ ثانية: أكوامُ النصّ (بطاقة الإحصاء وأخواتها) ══════════════════
     ليست حقولاً — لا عنصر تحكّم فيها — لكنها **التركيب نفسه**: مفتاحٌ فوق
     قيمةٍ فوق شرح، مرصوصةٌ في صفّ. ومفتاحٌ يلتفّ سطرين يُنزل قيمته وحدها.
     والوحدة تُشتقّ هنا أيضاً من البنية: ابنٌ مباشر لوعاءٍ شبكيّ أو مرن، فيه
     سطران على الأقلّ، ولا عنصر تحكّم فيه ولا جدول حوله. */
  const stackRows = [];
  {
    const containers = new Set();
    for (const el of document.querySelectorAll("body *")) {
      const cs = getComputedStyle(el);
      if (!/grid|flex/.test(cs.display)) continue;
      if (el.closest("table, .app-side, .app-topbar, nav")) continue;
      if (el.querySelector("[data-align-unit]")) continue;
      if (el.children.length < 2) continue;
      containers.add(el);
    }
    const lineTops = (el) => {
      const r = document.createRange();
      r.selectNodeContents(el);
      const rs = [...r.getClientRects()].filter((x) => x.width > 0.5 && x.height > 0.5);
      return [...new Set(rs.map((x) => Math.round(x.top)))].sort((a, b) => a - b);
    };
    /* «القيمة» = أكبرُ خطٍّ داخل الوحدة، وهو ما تقفز إليه العين عبر الصفّ. */
    const valueOf = (el) => {
      let best = null;
      let size = -1;
      for (const e of [el, ...el.querySelectorAll("*")]) {
        if (!(e.textContent || "").trim()) continue;
        if (!visible(e)) continue;
        if (e.children.length > 0 && ![...e.childNodes].some((n) => n.nodeType === 3 && n.textContent.trim())) continue;
        const fs = parseFloat(getComputedStyle(e).fontSize);
        if (fs > size) {
          size = fs;
          best = e;
        }
      }
      return best;
    };
    for (const parent of containers) {
      const kids = [...parent.children].filter((k) => {
        if (k.querySelector(CONTROL_SEL) || k.matches(CONTROL_SEL)) return false;
        if (!visible(k)) return false;
        const b = k.getBoundingClientRect();
        if (b.width < 70 || b.height < 34) return false;
        return lineTops(k).length >= 2;
      });
      if (kids.length < 2) continue;
      const rects = kids.map((k) => ({ el: k, r: box(k) }));
      rects.sort((a, b) => a.r.top - b.r.top);
      const bands = [];
      for (const k of rects) {
        const band = bands.find(
          (b) => Math.min(b.bottom, k.r.bottom) - Math.max(b.top, k.r.top) > 0.5 * Math.min(b.bottom - b.top, k.r.h)
        );
        if (band) {
          band.items.push(k);
          band.top = Math.min(band.top, k.r.top);
          band.bottom = Math.max(band.bottom, k.r.bottom);
        } else bands.push({ top: k.r.top, bottom: k.r.bottom, items: [k] });
      }
      for (const band of bands) {
        if (band.items.length < 2) continue;
        const members = band.items.map((k) => {
          const v = valueOf(k.el);
          const tops = lineTops(k.el);
          return {
            firstLineTop: tops.length ? tops[0] + sy : null,
            lastLineTop: tops.length ? tops[tops.length - 1] + sy : null,
            lines: tops.length,
            valueTop: v ? firstLine(v)?.top ?? null : null,
            valueText: v ? (v.textContent || "").trim().slice(0, 24) : "",
            headroom: headroom(k.el.firstElementChild || k.el),
            boxBottom: k.r.bottom,
          };
        });
        const sp = (vals) => {
          const v = vals.filter((x) => typeof x === "number" && Number.isFinite(x));
          if (v.length < 2) return null;
          const so = [...v].sort((a, b) => a - b);
          const mid = so.length % 2 ? so[(so.length - 1) / 2] : (so[so.length / 2 - 1] + so[so.length / 2]) / 2;
          return {
            n: v.length,
            max: Math.round((so[so.length - 1] - so[0]) * 100) / 100,
            mad: Math.round((v.reduce((a, x) => a + Math.abs(x - mid), 0) / v.length) * 100) / 100,
          };
        };
        stackRows.push({
          parent: pathOf(parent),
          parentClass: parent.getAttribute("class") || "",
          scope: scopeOf(parent),
          members: members.length,
          valueTop: sp(members.map((m) => m.valueTop)),
          lastLineTop: sp(members.map((m) => m.lastLineTop)),
          lines: members.map((m) => m.lines),
          detail: members,
        });
      }
    }
  }

  ruler.remove();
  const scopeCount = { page: 0, shell: 0, dialog: 0 };
  for (const u of units) scopeCount[scopeOf(u.el)] += 1;

  return {
    units: units.length,
    pageUnits: scopeCount.page,
    shellUnits: scopeCount.shell,
    controls: controls.length,
    docHeight: document.documentElement.scrollHeight,
    rows,
    rhythms,
    stackRows,
  };
}

/**
 * يرسم علاماتٍ حمراء على حوافّ التحكّم المنحرفة — للقطة «قبل».
 * @param rows الصفوف المقيسة.
 */
function markInPage(rows) {
  const layer = document.createElement("div");
  layer.id = "align-marks";
  layer.style.cssText = "position:absolute;inset:0;pointer-events:none;z-index:2147483647";
  document.body.style.position = document.body.style.position || "relative";
  for (const row of rows) {
    if (!row.controlTop || row.controlTop.max < 1) continue;
    const tops = row.detail.map((m) => m.control.top);
    const lo = Math.min(...tops);
    for (const m of row.detail) {
      const off = m.control.top - lo;
      const line = document.createElement("div");
      const bad = off > 0.5;
      line.style.cssText =
        "position:absolute;left:" + row.left + "px;width:" + (row.right - row.left) + "px;top:" +
        m.control.top + "px;height:0;border-top:1.5px " + (bad ? "solid #ff2d55" : "dashed #00d0a0") + ";opacity:.9";
      layer.appendChild(line);
      if (bad) {
        const tagEl = document.createElement("div");
        tagEl.textContent = "+" + off.toFixed(1) + "px";
        tagEl.style.cssText =
          "position:absolute;left:" + (row.right - 54) + "px;top:" + (m.control.top - 14) +
          "px;font:700 11px/1 monospace;color:#fff;background:#ff2d55;padding:2px 4px;border-radius:3px";
        layer.appendChild(tagEl);
      }
    }
  }
  document.body.appendChild(layer);
}

/** يزيل العلامات. */
function unmarkInPage() {
  document.getElementById("align-marks")?.remove();
}

/* ══════════════════════════════════════════════════════════ التشغيل (Node) */

function waitForPort(port, label, timeoutMs = 120_000) {
  const start = Date.now();
  return new Promise((resolve, reject) => {
    const tick = async () => {
      try {
        const res = await fetch("http://127.0.0.1:" + port + "/", { method: "GET" });
        if (res.status < 500) return resolve();
      } catch {
        /* لم يُقلع بعد */
      }
      if (Date.now() - start > timeoutMs) return reject(new Error("لم يُقلع " + label + " على " + port));
      setTimeout(tick, 300);
    };
    tick();
  });
}

function run(cmd, args, cwd) {
  return new Promise((resolve, reject) => {
    const p = spawn(cmd, args, { cwd, stdio: "inherit" });
    p.on("exit", (code) => (code === 0 ? resolve() : reject(new Error(cmd + " خرج بـ" + code))));
  });
}

async function main() {
  mkdirSync(OUT_DIR, { recursive: true });
  const paths = await screenPaths();

  if (!flag("no-build")) {
    console.log("· بناء الواجهة …");
    await run("npm", ["run", "build"], WEB_ROOT);
  }

  const children = [];
  const stop = () => {
    for (const c of children) {
      try {
        process.kill(-c.pid, "SIGTERM");
      } catch {
        try {
          c.kill("SIGTERM");
        } catch {
          /* انتهى */
        }
      }
    }
  };
  process.on("exit", stop);
  process.on("SIGINT", () => {
    stop();
    process.exit(130);
  });

  const mock = spawn("node", ["scripts/mock-api.mjs", "--port", String(MOCK_PORT)], {
    cwd: WEB_ROOT,
    stdio: "ignore",
    detached: true,
  });
  children.push(mock);
  const web = spawn(
    "npx",
    ["vite", "preview", "--host", "127.0.0.1", "--port", String(WEB_PORT), "--strictPort"],
    { cwd: WEB_ROOT, stdio: "ignore", detached: true }
  );
  children.push(web);

  await waitForPort(MOCK_PORT, "الخادم الوهمي");
  await waitForPort(WEB_PORT, "خادم العرض");
  console.log("· الخادمان يعملان: web=" + WEB_PORT + " mock=" + MOCK_PORT);

  const browser = await chromium.launch({ executablePath: CHROMIUM, args: ["--font-render-hinting=none"] });
  const report = { generatedAt: new Date().toISOString(), passes: [] };

  for (const pass of PASSES) {
    const ctx = await browser.newContext({
      viewport: pass.viewport,
      deviceScaleFactor: 2,
      locale: { ar: "ar-SA", en: "en-US", ur: "ur-PK", hi: "hi-IN" }[pass.locale] ?? "en-US",
      colorScheme: "dark",
      reducedMotion: "reduce",
    });
    await ctx.addInitScript(
      ([loc]) => {
        try {
          localStorage.setItem("sb-locale", loc);
          localStorage.setItem("sb-theme", "dark");
          localStorage.setItem("sb-palette", "default");
        } catch {
          /* تصفّح خاص */
        }
      },
      [pass.locale]
    );
    const page = await ctx.newPage();
    const passOut = { tag: pass.tag, locale: pass.locale, dir: pass.dir, viewport: pass.viewport, screens: [] };

    for (const p of paths) {
      const q = new URLSearchParams({
        lang: pass.locale,
        baseUrl: "http://127.0.0.1:" + MOCK_PORT,
        companyId: COMPANY,
        book: "MAIN",
        period: "2026-05",
      });
      const url = "http://127.0.0.1:" + WEB_PORT + p + "?" + q.toString();
      let error = null;
      let measured = { units: 0, pageUnits: 0, controls: 0, rows: [], rhythms: [], stackRows: [], docHeight: 0 };
      try {
        await page.goto(url, { waitUntil: "load", timeout: 45_000 });
        await page.waitForSelector("#main", { timeout: 20_000 });
        await page.evaluate(() => document.fonts.ready);
        await page.waitForTimeout(700);
        const dir = await page.evaluate(() => document.documentElement.getAttribute("dir"));
        if (dir !== pass.dir) error = "اتجاه غير متوقّع: " + dir;
        measured = await page.evaluate(measureInPage);
      } catch (e) {
        error = String(e && e.message ? e.message : e);
      }

      if (SHOTS && pass.shots && !error) {
        const base = path.join(OUT_DIR, pass.tag + "__" + (p === "/" ? "root" : p.replace(/^\//, "").replace(/\//g, "-")));
        await page.screenshot({ path: base + ".png", fullPage: true });
        await page.evaluate(markInPage, measured.rows);
        await page.screenshot({ path: base + ".marked.png", fullPage: true });
        await page.evaluate(unmarkInPage);
      }

      passOut.screens.push({ path: p, error, ...measured });
      const pageRows = (measured.rows || []).filter((r) => r.scope === "page");
      const badRows = pageRows.filter((r) => r.controlTop && r.controlTop.max > 0.5);
      process.stdout.write(
        "  " + pass.tag.padEnd(9) + " " + p.padEnd(28) +
        " pageUnits=" + String(measured.pageUnits ?? 0).padStart(3) +
        " rows=" + String(pageRows.length).padStart(3) +
        " broken=" + String(badRows.length).padStart(3) +
        " worst=" + String(badRows.length ? Math.max(...badRows.map((r) => r.controlTop.max)).toFixed(1) : "0").padStart(6) +
        (error ? "  ⚠ " + error : "") + "\n"
      );
    }
    report.passes.push(passOut);
    await ctx.close();
  }

  await browser.close();
  stop();

  const file = path.join(OUT_DIR, "align-report.json");
  writeFileSync(file, JSON.stringify(report, null, 1));
  console.log("\n· التقرير: " + file);
  summarise(report);
}

/** يطبع خلاصةً مقروءة من التقرير. */
function summarise(report) {
  const T = 0.5; /* أقلّ من نصف بكسل = تقريبُ عرضٍ لا انحراف */
  const num = (x) => (x === null || x === undefined ? "—" : String(x));
  for (const pass of report.passes) {
    const rows = pass.screens.flatMap((s) => s.rows.filter((r) => r.scope === "page").map((r) => ({ s: s.path, r })));
    const stacks = pass.screens.flatMap((s) => (s.stackRows || []).filter((r) => r.scope === "page").map((r) => ({ s: s.path, r })));
    const pick = (key) => rows.filter((x) => x.r[key] && x.r[key].max > T);
    const ctl = pick("controlTop");
    const lab = pick("labelBaseline");
    const des = pick("descTop");
    const gut = pick("gutter");
    const bot = pick("unitBottom");
    const stk = stacks.filter((x) => x.r.valueTop && x.r.valueTop.max > T);
    const ink = pick("inkBottom");
    const ragged = rows.filter((x) => x.r.raggedDesc);
    const rhy = pass.screens.flatMap((s) => (s.rhythms || []).filter((r) => r.scope === "page").map((r) => ({ s: s.path, r })));
    const rhyBad = rhy.filter((x) => x.r.spread > T);
    const rhyCol = rhy.filter((x) => x.r.singleColumn);
    const rhyColBad = rhyCol.filter((x) => x.r.spread > T);
    const worst = ctl.slice().sort((a, b) => b.r.controlTop.max - a.r.controlTop.max)[0];
    const meanOf = (list, key) =>
      list.length ? (list.reduce((a, x) => a + x.r[key].max, 0) / list.length).toFixed(2) : "—";
    const fragile = rows.filter((x) => x.r.minLabelHeadroom < 24 || x.r.minDescHeadroom < 24);

    console.log(
      "\n══ " + pass.tag + "  (" + pass.dir + ", " + pass.viewport.width + "×" + pass.viewport.height + ")" +
      "\n   وحدات الصفحة: " + pass.screens.reduce((a, s) => a + (s.pageUnits || 0), 0) +
      "  ·  صفوف الصفحة: " + rows.length +
      "\n   حافّة أعلى التحكّم منحرفة:  " + ctl.length + "/" + rows.length +
      "  أقصى " + (worst ? worst.r.controlTop.max + "px" : "—") + "  متوسّط " + meanOf(ctl, "controlTop") + "px" +
      (worst ? "  @ " + worst.s + " [" + worst.r.parentClass + "]" : "") +
      "\n   خطّ قاعدة التسمية منحرف:   " + lab.length + "/" + rows.length + "  أقصى " +
      (lab.length ? Math.max(...lab.map((x) => x.r.labelBaseline.max)).toFixed(2) + "px" : "—") +
      "\n   كتلة الوصف منحرفة:         " + des.length + "/" + rows.length + "  أقصى " +
      (des.length ? Math.max(...des.map((x) => x.r.descTop.max)).toFixed(2) + "px" : "—") +
      "\n   قاعُ الحقل مسنَّن (gutter):  " + gut.length + "/" + rows.length + "  أقصى " +
      (gut.length ? Math.max(...gut.map((x) => x.r.gutter.max)).toFixed(2) + "px" : "—") +
      "  ·  أسفل الصندوق: " + bot.length + "/" + rows.length +
      "\n   قاعُ الحبر مسنَّن (ما تراه العين): " + ink.length + "/" + rows.length + "  أقصى " +
      (ink.length ? Math.max(...ink.map((x) => x.r.inkBottom.max)).toFixed(2) + "px" : "—") +
      "  ·  أوصافٌ متفاوتة الأسطر: " + ragged.length + "/" + rows.length +
      "\n   إيقاعٌ رأسي متفاوت بين صفوف الوعاء: " + rhyBad.length + "/" + rhy.length +
      "  أقصى " + (rhyBad.length ? Math.max(...rhyBad.map((x) => x.r.spread)).toFixed(2) + "px" : "—") +
      "  ·  منها أعمدة مفردة (الهاتف): " + rhyColBad.length + "/" + rhyCol.length +
      "  أقصى " + (rhyColBad.length ? Math.max(...rhyColBad.map((x) => x.r.spread)).toFixed(2) + "px" : "—") +
      "\n   صفوفٌ على حافّة الالتفاف (<24px متّسع): " + fragile.length + "/" + rows.length +
      "\n   صفوف أكوام النصّ (بطاقات): " + stacks.length + "  منحرفة القيمة: " + stk.length +
      (stk.length ? "  أقصى " + Math.max(...stk.map((x) => x.r.valueTop.max)).toFixed(2) + "px" : "")
    );
    const byScreen = new Map();
    for (const x of ctl) {
      const cur = byScreen.get(x.s) || { n: 0, max: 0 };
      byScreen.set(x.s, { n: cur.n + 1, max: Math.max(cur.max, x.r.controlTop.max) });
    }
    for (const [sc, v] of [...byScreen.entries()].sort((a, b) => b[1].max - a[1].max)) {
      console.log("     " + String(v.n).padStart(3) + " صفّاً · أقصى " + v.max.toFixed(1).padStart(6) + "px · " + sc);
    }
    void num;
  }
  /* الشاشات التي لم تُقَس: لا حقول فيها لأن سطحها يحتاج بيانات لا يعطيها الوهمي. */
  const first = report.passes[0];
  const empty = first.screens.filter((s) => (s.pageUnits || 0) === 0);
  if (empty.length) {
    console.log("\n══ شاشاتٌ بلا أي عنصر تحكّم في الصفحة (لم تُقَس صفوفُها):");
    for (const s of empty) console.log("     " + s.path + (s.error ? "  ⚠ " + s.error : ""));
  }
}

main().catch((e) => {
  console.error(e);
  process.exit(1);
});
