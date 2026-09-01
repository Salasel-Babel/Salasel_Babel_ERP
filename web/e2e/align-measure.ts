/* ═══════════════════════════════════════════════════════════════════════════
   مقياسُ استقامة الصفّ — ما يُنفَّذ **داخل الصفحة**
   the row-alignment measure — what runs inside the page
   ───────────────────────────────────────────────────────────────────────────
   **لماذا قياسٌ لا قائمةَ مُحدِّدات مكتوبةً بيد:** الشاشة التي لم يخطر لأحدٍ أن
   يكتب مُحدِّدها هي بالضبط الشاشة التي ينكسر فيها الصفّ. فـ«ما هو صفٌّ واحد»
   يُشتقّ هنا من **الهندسة والشجرة معاً**، لا من اسمٍ في مصفوفة:

     ١ · الوحدة (حقل) = أبعدُ سلفٍ لعنصر تحكّمٍ مرئي **لا يزال يحوي عنصر تحكّمٍ
         واحداً**. فتُلتقط `<Field>` الأوّليّة و`div.field` المكتوبة باليد
         و`.check` و`.switch` و`.ctl-wrap` سواءً — بلا اسمٍ واحد مكتوب هنا.
     ٢ · وعاء الصفّ = أقربُ سلفٍ للوحدة **له ابنان اثنان على الأقلّ يحوي كلٌّ
         منهما وحدة**. فيتسلّق الأغلفة ذات الابن الواحد ولا يتوقّف عندها.
     ٣ · الصفّ = وحداتٌ في الوعاء نفسه **تتقاطع نطاقاتها الرأسية**، في خانتين
         مختلفتين على الأقلّ.

   **وكلّ ما في `measureAlignment` يُسلسَل إلى المتصفّح**، فلا يستعمل شيئاً من
   خارج نطاقه: لا استيراداً ولا ثابتاً من الوحدة. من يعدّله فليُبقِه مغلقاً.
   ═══════════════════════════════════════════════════════════════════════════ */

/** انحرافُ قيمةٍ واحدة عبر أعضاء الصفّ. */
export interface Spread {
  readonly n: number;
  readonly max: number;
}

/** عضوٌ في صفّ — بما يكفي لتسميته في رسالة السقوط. */
export interface RowMember {
  readonly label: string;
  readonly testId: string;
  readonly controlTop: number;
  readonly descLines: number;
  readonly labelLines: number;
}

/** صفٌّ مقيس. */
export interface MeasuredRow {
  readonly parentClass: string;
  readonly scope: "page" | "shell" | "dialog";
  readonly members: number;
  readonly controlTop: Spread | null;
  readonly labelFirstLineTop: Spread | null;
  readonly descTop: Spread | null;
  readonly inkBottom: Spread | null;
  /** صفٌّ خلط عائلتَي خطّ عمداً (نصّ + `--font-mono`): صندوقا سطرٍ مختلفان،
      فيُقاس خطّ النصّ فيه ولا يُحاكم عليه. حافّةُ التحكّم تُحاكَم دائماً. */
  readonly mixedLabelFont: boolean;
  readonly mixedDescFont: boolean;
  readonly detail: readonly RowMember[];
}

/** إيقاعُ عمودٍ مفرد: الفجوات بين حقولٍ **متجاورة في الشجرة** داخل وعاءٍ واحد.
    هذا هو ما يُرى على الهاتف، حيث لا صفوف أصلاً — كل حقلٍ وحده في سطره. */
export interface MeasuredRhythm {
  readonly parentClass: string;
  readonly scope: "page" | "shell" | "dialog";
  readonly gaps: readonly number[];
  readonly spread: number;
}

/** حقلٌ خالف بنيةَ الخانات الثلاث. */
export interface SlotFault {
  readonly cls: string;
  readonly label: string;
  readonly descChildren: number;
}

/** ناتجُ قياس صفحةٍ واحدة. */
export interface PageMeasure {
  readonly units: number;
  readonly pageUnits: number;
  readonly rows: readonly MeasuredRow[];
  readonly rhythms: readonly MeasuredRhythm[];
  readonly slotFaults: readonly SlotFault[];
  readonly overflowX: number;
}

/**
 * يقيس صفحةً واحدة. **يُنفَّذ داخل المتصفّح** عبر `page.evaluate`.
 * @returns الوحدات والصفوف ومخالفات البنية وانزلاق الصفحة أفقياً.
 */
export function measureAlignment(): PageMeasure {
  const CONTROL_SEL =
    'input:not([type="hidden"]), select, textarea, [role="combobox"], [role="spinbutton"], [contenteditable="true"]';
  /* أدوارُ الخانة الثالثة — الوصف. تُعرَف بالاسم هنا لأن **الاسم هو العقد**:
     من يُضيف صنفَ وصفٍ جديداً عليه أن يُضيفه إلى القاعدة في components.css
     وإلى هذه القائمة معاً، وإلّا سقط الحارس وأخبره. */
  const DESC_SEL = ".field__desc, .hint, .field-error, .field-ok, .dateline";

  const sy = window.scrollY;
  const sx = window.scrollX;
  const box = (el: Element) => {
    const r = el.getBoundingClientRect();
    return { top: r.top + sy, bottom: r.bottom + sy, left: r.left + sx, right: r.right + sx, w: r.width, h: r.height };
  };
  const visible = (el: Element) => {
    const r = el.getBoundingClientRect();
    if (r.width <= 0 || r.height <= 0) return false;
    const cs = getComputedStyle(el);
    return cs.visibility !== "hidden" && cs.display !== "none" && Number(cs.opacity) > 0.01;
  };

  /* ── ١ · الوحدات ─────────────────────────────────────────────────────── */
  const controls = [...document.querySelectorAll(CONTROL_SEL)].filter(visible);
  const controlCount = (el: Element) =>
    [...el.querySelectorAll(CONTROL_SEL)].filter((c) => controls.includes(c)).length;

  interface Unit {
    el: Element;
    control: Element;
    slot?: Element;
    rect?: ReturnType<typeof box>;
  }
  const units: Unit[] = [];
  for (const c of controls) {
    let unit: Element = c;
    let node: Element = c;
    while (node.parentElement && node.parentElement !== document.body) {
      const p: HTMLElement = node.parentElement;
      if (controlCount(p) > 1) break;
      unit = p;
      node = p;
    }
    if (!units.some((u) => u.el === unit)) units.push({ el: unit, control: c });
  }
  units.forEach((u, i) => u.el.setAttribute("data-align-unit", String(i)));

  /* ── ٢ · وعاء الصفّ: أقربُ سلفٍ له خانتان تحويان وحدة ─────────────────── */
  const hasUnit = (el: Element) =>
    el.hasAttribute("data-align-unit") || !!el.querySelector("[data-align-unit]");
  const rowParentOf = (unitEl: Element): { parent: Element; slot: Element } | null => {
    let child: Element = unitEl;
    let p = child.parentElement;
    while (p && p !== document.documentElement) {
      if ([...p.children].filter(hasUnit).length >= 2) return { parent: p, slot: child };
      child = p;
      p = p.parentElement;
    }
    return null;
  };

  /* ── ٣ · التسمية والوصف داخل الوحدة ──────────────────────────────────── */
  const rectsOf = (el: Element) => {
    const r = document.createRange();
    r.selectNodeContents(el);
    return [...r.getClientRects()].filter((x) => x.width > 0.5 && x.height > 0.5);
  };
  const lineCount = (el: Element) => {
    const rs = rectsOf(el);
    return rs.length === 0 ? 0 : new Set(rs.map((r) => Math.round(r.top))).size;
  };
  /** أعلى مستطيلِ السطر الأول — وهو ما تراه العين خطَّ التسمية. */
  const firstLineTop = (el: Element): number | null => {
    const rs = rectsOf(el).sort((a, b) => a.top - b.top || a.left - b.left);
    return rs.length === 0 ? null : rs[0].top + sy;
  };
  const labelOf = (u: Unit): Element | null => {
    if (u.el.tagName === "LABEL") return u.el;
    const id = (u.control as HTMLElement).id;
    const byFor = id ? u.el.querySelector('label[for="' + CSS.escape(id) + '"]') : null;
    return byFor ?? u.el.querySelector("label") ?? null;
  };
  /* الوصف: أوّل عنصرٍ **بعد** عنصر التحكّم في ترتيب المستند، فيه نصّ، ولا عنصر
     تحكّمٍ فيه. فيلتقط `.field__desc` و`.hint` وأي وصفٍ لم يُسمَّ بعد. */
  const descOf = (u: Unit): Element | null => {
    const all = [...u.el.querySelectorAll("*")];
    const ci = all.indexOf(u.control);
    for (let i = ci + 1; i < all.length; i += 1) {
      const e = all[i];
      if (e.contains(u.control) || u.control.contains(e)) continue;
      if (e.querySelector(CONTROL_SEL) || e.matches(CONTROL_SEL)) continue;
      if (e.tagName === "OPTION" || e.tagName === "SVG" || e.tagName === "PATH") continue;
      if (!(e.textContent ?? "").trim()) continue;
      if (!visible(e)) continue;
      const pos = getComputedStyle(e).position;
      if (pos === "absolute" || pos === "fixed") continue;
      return e;
    }
    return null;
  };
  /* بصمةُ الخطّ: عائلاتُ الخطّ في العنصر وكل ما يحمل نصّاً تحته، مرتّبةً.
     التسمية نفسها قد ترث الخطّ العام بينما يحمل ابنُها `.mono` خطّاً آخر —
     والفرق في صندوق السطر يأتي من الابن لا من الأمّ. */
  const familyOf = (el: Element | null): string => {
    if (!el) return "";
    const fams = new Set<string>();
    for (const e of [el, ...el.querySelectorAll("*")]) {
      if (!(e.textContent ?? "").trim()) continue;
      fams.add(getComputedStyle(e).fontFamily);
    }
    return [...fams].sort().join(" | ");
  };
  const scopeOf = (el: Element): "page" | "shell" | "dialog" =>
    el.closest(".app-topbar, .app-side") ? "shell" : el.closest("dialog, [role='dialog']") ? "dialog" : "page";

  /* ── ٤ · مخالفاتُ البنية: خانةُ وصفٍ فيها أكثر من ساكن ─────────────────
     الصفّ يملك ثلاثة مسارات، والحقل يستعيرها. فحقلٌ فيه وصفان مباشران يضع
     ساكنين في مسارٍ واحد **فيتراكبان** — وهو عطلٌ أسوأ من الانحراف لأنه يُخفي
     نصّاً. هذا هو الحارس على العقد الذي يقوم عليه الإصلاح كلّه. */
  const slotFaults: SlotFault[] = [];
  for (const f of document.querySelectorAll(".field")) {
    if (!visible(f)) continue;
    const kids = [...f.children].filter((k) => k.matches(DESC_SEL) && visible(k));
    if (kids.length > 1) {
      slotFaults.push({
        cls: f.getAttribute("class") ?? "",
        label: (f.querySelector("label")?.textContent ?? "").trim().slice(0, 40),
        descChildren: kids.length,
      });
    }
  }

  /* ── ٥ · التجميع في صفوف ─────────────────────────────────────────────── */
  const groups = new Map<Element, Unit[]>();
  for (const u of units) {
    const rp = rowParentOf(u.el);
    if (!rp) continue;
    u.slot = rp.slot;
    u.rect = box(u.el);
    const list = groups.get(rp.parent);
    if (list) list.push(u);
    else groups.set(rp.parent, [u]);
  }

  const spread = (values: readonly (number | null)[]): Spread | null => {
    const v = values.filter((x): x is number => typeof x === "number" && Number.isFinite(x));
    if (v.length < 2) return null;
    const sorted = [...v].sort((a, b) => a - b);
    return { n: v.length, max: Math.round((sorted[sorted.length - 1] - sorted[0]) * 100) / 100 };
  };

  const rows: MeasuredRow[] = [];
  for (const [parent, list] of groups) {
    list.sort((a, b) => a.rect!.top - b.rect!.top);
    const bands: { top: number; bottom: number; items: Unit[] }[] = [];
    for (const u of list) {
      const r = u.rect!;
      const band = bands.find(
        (b) => Math.min(b.bottom, r.bottom) - Math.max(b.top, r.top) > 0.5 * Math.min(b.bottom - b.top, r.h)
      );
      if (band) {
        band.items.push(u);
        band.top = Math.min(band.top, r.top);
        band.bottom = Math.max(band.bottom, r.bottom);
      } else bands.push({ top: r.top, bottom: r.bottom, items: [u] });
    }
    for (const band of bands) {
      if (band.items.length < 2) continue;
      if (new Set(band.items.map((u) => u.slot)).size < 2) continue;
      const members = band.items.map((u) => {
        const lab = labelOf(u);
        const desc = descOf(u);
        const cb = box(u.control);
        return {
          labelFamily: familyOf(lab),
          descFamily: familyOf(desc),
          labelText: (lab?.textContent ?? "").trim().slice(0, 40),
          testId: (u.control as HTMLElement).getAttribute("data-testid") ?? (u.control as HTMLElement).id,
          controlTop: cb.top,
          labelTop: lab ? firstLineTop(lab) : null,
          descTop: desc ? box(desc).top : null,
          inkBottom: Math.max(cb.bottom, desc ? box(desc).bottom : cb.bottom),
          descLines: desc ? lineCount(desc) : 0,
          labelLines: lab ? lineCount(lab) : 0,
        };
      });
      rows.push({
        parentClass: parent.getAttribute("class") ?? parent.tagName.toLowerCase(),
        scope: scopeOf(parent),
        members: members.length,
        controlTop: spread(members.map((m) => m.controlTop)),
        labelFirstLineTop: spread(members.map((m) => m.labelTop)),
        descTop: spread(members.map((m) => m.descTop)),
        inkBottom: spread(members.map((m) => m.inkBottom)),
        mixedLabelFont: new Set(members.filter((m) => m.labelTop !== null).map((m) => m.labelFamily)).size > 1,
        mixedDescFont: new Set(members.filter((m) => m.descTop !== null).map((m) => m.descFamily)).size > 1,
        detail: members.map((m) => ({
          label: m.labelText,
          testId: m.testId,
          controlTop: Math.round(m.controlTop * 100) / 100,
          descLines: m.descLines,
          labelLines: m.labelLines,
        })),
      });
    }
  }

  /* ── ٦ · إيقاعُ العمود المفرد ──────────────────────────────────────────
     تُقاس الفجوة بين حقلين **متجاورين في الشجرة** فقط (`nextElementSibling`)،
     كي لا تُحسَب لوحةُ رفضٍ أو عنوانٌ بينهما فجوةً متفاوتة. وفي وعاءٍ واحد
     يجب أن تتساوى هذه الفجوات كلّها: هي `row-gap` الوعاء ولا شيء غيرها. */
  const rhythms: MeasuredRhythm[] = [];
  for (const [parent, list] of groups) {
    const bySlot = new Map<Element, Unit[]>();
    for (const u of list) {
      const arr = bySlot.get(u.slot!);
      if (arr) arr.push(u);
      else bySlot.set(u.slot!, [u]);
    }
    const gaps: number[] = [];
    for (const [slot, owned] of bySlot) {
      const next = slot.nextElementSibling;
      if (!next) continue;
      const nOwned = bySlot.get(next);
      if (!nOwned || owned.length !== 1 || nOwned.length !== 1) continue;
      const a = box(slot);
      const b = box(next);
      if (b.top < a.bottom - 0.5) continue; /* جنباً إلى جنب لا فوق بعضهما */
      gaps.push(Math.round((b.top - a.bottom) * 100) / 100);
    }
    if (gaps.length >= 2) {
      const sorted = [...gaps].sort((x, y) => x - y);
      rhythms.push({
        parentClass: parent.getAttribute("class") ?? parent.tagName.toLowerCase(),
        scope: scopeOf(parent),
        gaps,
        spread: Math.round((sorted[sorted.length - 1] - sorted[0]) * 100) / 100,
      });
    }
  }

  for (const u of units) u.el.removeAttribute("data-align-unit");
  const scroller = document.scrollingElement ?? document.documentElement;
  return {
    units: units.length,
    pageUnits: units.filter((u) => scopeOf(u.el) === "page").length,
    rows,
    rhythms,
    slotFaults,
    overflowX: Math.round((scroller.scrollWidth - scroller.clientWidth) * 100) / 100,
  };
}

/**
 * يرسم مسطرةً حمراء على كل حافّة تحكّمٍ منحرفة، ويكتب فرقها بالبكسل.
 * **يُنفَّذ داخل المتصفّح** — للقطات الإثبات وحدها، لا للحُكم.
 * @param rows الصفوف المقيسة.
 */
export function markMisalignment(rows: readonly MeasuredRow[]): void {
  const layer = document.createElement("div");
  layer.id = "align-marks";
  layer.style.cssText = "position:absolute;inset:0;pointer-events:none;z-index:2147483647";
  for (const row of rows) {
    if (!row.controlTop || row.controlTop.max < 0.5) continue;
    const tops = row.detail.map((m) => m.controlTop);
    const lo = Math.min(...tops);
    for (const m of row.detail) {
      const off = m.controlTop - lo;
      const line = document.createElement("div");
      line.style.cssText =
        "position:absolute;inset-inline:0;top:" + m.controlTop + "px;height:0;border-top:1.5px " +
        (off > 0.5 ? "solid #ff2d55" : "dashed #00d0a0") + ";opacity:.9";
      layer.appendChild(line);
      if (off > 0.5) {
        const tag = document.createElement("div");
        tag.textContent = "+" + off.toFixed(1) + "px";
        tag.style.cssText =
          "position:absolute;inset-inline-end:8px;top:" + (m.controlTop - 15) +
          "px;font:700 11px/1 monospace;color:#fff;background:#ff2d55;padding:2px 4px;border-radius:3px";
        layer.appendChild(tag);
      }
    }
  }
  document.body.appendChild(layer);
}

/** يمسح المساطر. */
export function unmarkMisalignment(): void {
  document.getElementById("align-marks")?.remove();
}
