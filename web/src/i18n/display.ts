/* ═══════════════════════════════════════════════════════════════════════════
   نوع العرض — منقول حرفياً من design/i18n/i18n.js §١
   The Display type — ported verbatim from design/i18n/i18n.js §1
   ───────────────────────────────────────────────────────────────────────────
   Display قيمةٌ منسّقة للعرض. ليست نصّاً، ولا يمكن أن تصير نصّاً:

       input.value = d      → TypeError
       `${d}`               → TypeError
       JSON.stringify(d)    → TypeError
       d.localeCompare(x)   → TypeError
       hash(d)              → TypeError

   المصرف الوحيد إلى الشاشة  d.into(el)
   المخرَج الوحيد إلى السلك  d.machine  (ASCII دائماً)

   TypeScript يُمحى وقت التشغيل. هذه الرميات هي الحارس الحقيقي، ولذلك نُقلت
   كما هي بلا تخفيف: الحماية سلوكٌ لا تعليق.
   ═══════════════════════════════════════════════════════════════════════════ */

/* محارف التحكّم غير المرئية — مكتوبة بالهروب الصريح عمداً.
   لا يجوز أن يحمل هذا الملف نفسه محرفاً غير مرئي واحداً. */
export const INVISIBLE_RE =
  /[\u200B-\u200F\u061C\u202A-\u202E\u2066-\u2069\uFEFF]/;

const TEXT: WeakMap<object, string> = new WeakMap();

function refuse(what: string): never {
  throw new TypeError(
    "Display: قيمة معروضة محلّياً لا تُحوَّل إلى نصّ (" +
      what +
      "). استعمل d.into(element) للعرض، أو d.machine للقيمة القابلة للإرسال/التجزئة. " +
      "[Display is display-only; use .into(el) to render or .machine to submit.]"
  );
}

/** ما يصف قيمة معروضة: هل تُرسم من اليسار إلى اليمين، وبأي مقياس. */
export interface DisplayMeta {
  /** الرقم يُعزل ويُرسم LTR داخل النصّ العربي. */
  ltr?: boolean;
  /** عدد الخانات العشرية المعروضة. */
  scale?: number;
}

/** نوع القيمة المعروضة — يظهر في أدوات الفحص. */
export type DisplayKind = "amount" | "integer" | "percent" | "date" | "hijri-display-only";

/** قيمة منسّقة للعرض وحده. */
export class Display {
  /** القيمة الآلية: ASCII دائماً، وهي وحدها ما يُرسَل أو يُجزَّأ. */
  readonly machine: string;
  /** نوع القيمة. */
  readonly kind: DisplayKind;
  /** وصفها. */
  readonly meta: Readonly<DisplayMeta>;

  /**
   * @param text النصّ المنسّق — محبوس ولا يخرج إلا عبر {@link Display.into}.
   * @param machine القيمة الآلية.
   * @param kind نوع القيمة.
   * @param meta وصفها.
   */
  constructor(text: string, machine: string, kind: DisplayKind, meta?: DisplayMeta) {
    if (INVISIBLE_RE.test(text)) {
      /* الحارس البنيوي: أي مُنسِّق سرّب محرف تحكّم (وهو ما تفعله Intl تحت ar/ur)
         يفشل هنا بصوت عالٍ بدل أن يصل إلى الشاشة ثم إلى حقل ثم إلى بصمة. */
      throw new Error(
        "Display: النصّ المنسّق يحمل محرف تحكّم غير مرئي — مصدره على الأرجح Intl. " +
          "[formatted text contains a bidi control character]"
      );
    }
    TEXT.set(this, String(text));
    this.machine = String(machine);
    this.kind = kind;
    this.meta = Object.freeze({ ...(meta ?? {}) });
    Object.freeze(this);
  }

  /**
   * المصرف الوحيد: يكتب النصّ في عنصر ويضبط عزله الاتجاهي.
   * @param el عنصر DOM.
   */
  into<E extends { nodeType?: number; textContent: string | null; hasAttribute(n: string): boolean; setAttribute(n: string, v: string): void }>(
    el: E
  ): E {
    if (!el || !el.nodeType) throw new TypeError("Display.into(el): يحتاج عنصر DOM.");
    el.textContent = TEXT.get(this) as string;
    if (this.meta.ltr) {
      /* العزل بالـCSS والسمة، لا بحقن محرف تحكّم — design/README §٣٫٧ */
      if (!el.hasAttribute("dir")) el.setAttribute("dir", "ltr");
    }
    return el;
  }

  /** للمعاينة في أدوات الفحص وحدها — مُسمّاة صراحةً لتظهر في المراجعة. */
  unsafeTextForAudit(): string {
    return TEXT.get(this) as string;
  }

  /* ── الحدود ─────────────────────────────────────────────────────────── */
  /** يرمي دائماً. */
  toString(): never {
    return refuse("toString");
  }
  /** يرمي دائماً. */
  valueOf(): never {
    return refuse("valueOf");
  }
  /** يرمي دائماً. */
  toJSON(): never {
    return refuse("toJSON");
  }
  /** يرمي دائماً. */
  localeCompare(): never {
    return refuse("localeCompare");
  }
  /** يرمي دائماً — يمنع `${d}` و `+d` و `d + ""`. */
  [Symbol.toPrimitive](): never {
    return refuse("implicit coercion");
  }
}

/** هل القيمة قيمةَ عرض؟ */
export function isDisplay(value: unknown): value is Display {
  return value instanceof Display;
}

/** غلاف HTML للعرض — نفس المنطق: كائن لا نصّ. */
export class Html {
  /** الوسم. لا يُقرأ إلا من مصرف DOM. */
  readonly __html: string;
  /** @param markup الوسم. */
  constructor(markup: string) {
    this.__html = String(markup);
    Object.freeze(this);
  }
  /** يرمي دائماً. */
  toString(): never {
    return refuse("toString (Html)");
  }
  /** يرمي دائماً. */
  [Symbol.toPrimitive](): never {
    return refuse("implicit coercion (Html)");
  }
}

/**
 * الحدّ في الاتجاه المعاكس: أي شيء يُرسَل أو يُجزَّأ يمرّ من هنا.
 * @param value القيمة.
 */
export function machine(value: unknown): string {
  if (value instanceof Display) return value.machine;
  if (value instanceof Html) throw new TypeError("machine: قيمة عرض (HTML) لا تُرسَل.");
  if (value === null || value === undefined) return "";
  if (typeof value === "string") return value;
  if (typeof value === "number" || typeof value === "boolean") return String(value);
  throw new TypeError("machine: قيمة لا تُحوَّل إلى نصّ آلي بأمان. / not safely stringifiable.");
}
