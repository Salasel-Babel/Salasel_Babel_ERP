/* ═══════════════════════════════════════════════════════════════════════════
   طبقة التدويل — منقولة من design/i18n/i18n.js إلى TypeScript
   The i18n runtime — ported from design/i18n/i18n.js to TypeScript
   ───────────────────────────────────────────────────────────────────────────
   ما نُقل كما هو، عمداً:
     · سلسلة الاحتياط: اللغة النشطة ← احتياطها المعلَن ← العربية (المصدر).
     · إعلان المفتاح الناقص بأربع طرق: قائمة مسجَّلة، وتحذير مرّة واحدة لكل
       مفتاح، وسمة data-i18n-missing على العنصر، وخروج غير صفري من الفحص.
     · الجمع عبر Intl.PluralRules — لا شرطاً على الواحد. العربية ستّ فئات،
       والهندية تضع الصفر في فئة one.
     · الأرقام والتواريخ من **ملفّ اللغة** لا من Intl: Intl تحقن محارف تحكّم
       غير مرئية تحت ar و ur، فتصل إلى الشاشة ثم إلى حقل ثم إلى بصمة.
     · تطبيع الأرقام العربية-الهندية والديفاناغرية عند الحدّ.
   ═══════════════════════════════════════════════════════════════════════════ */

import { Display, Html, type DisplayKind } from "./display";
import { moneyText, toLatinDigits } from "./decimal-text";
import type {
  CatalogueEntry,
  FlatMessages,
  LocaleMeta,
  MessageTree,
  NumberOptions,
  PluralBag,
} from "./types";

/** لغة المصدر والاحتياط النهائي لكل مفتاح ناقص. */
export const SOURCE = "ar";

const CLDR = ["zero", "one", "two", "few", "many", "other"] as const;
const EXACT = /^=\d+$/; /* صيغة العدد الصريح على طريقة ICU: "=0" */

interface LoadedLocale {
  code: string;
  meta: LocaleMeta;
  messages: FlatMessages;
}

function own(o: object, k: string): boolean {
  return Object.prototype.hasOwnProperty.call(o, k);
}

/** كيس جمع؟ يُعرَف بمفاتيحه: فئات CLDR أو صيغ "=N"، ولا بدّ من other. */
export function isPluralBag(v: unknown): v is PluralBag {
  if (!v || typeof v !== "object" || Array.isArray(v)) return false;
  const bag = v as Record<string, unknown>;
  for (const k of Object.keys(bag)) {
    if (!(CLDR as readonly string[]).includes(k) && !EXACT.test(k)) return false;
  }
  return own(bag, "other");
}

/* تسطيح {a:{b:"x"}} → {"a.b":"x"}. أكياس الجمع تُترك كما هي. */
function flatten(tree: MessageTree, prefix = "", out: FlatMessages = {}): FlatMessages {
  for (const k of Object.keys(tree)) {
    const v = tree[k];
    const key = prefix ? prefix + "." + k : k;
    if (v && typeof v === "object" && !Array.isArray(v) && !isPluralBag(v)) {
      flatten(v, key, out);
    } else {
      out[key] = v as string | PluralBag;
    }
  }
  return out;
}

/** مفتاح ناقص كما سُجِّل. */
export interface MissingKey {
  locale: string;
  key: string;
}

/** مستمع تغيّر اللغة. */
export type LocaleListener = (code: string, meta: LocaleMeta) => void;

/**
 * سجلّ اللغات وطبقة الترجمة. نسخة واحدة تكفي التطبيق، والاختبارات تبني نسختها.
 */
export class I18n {
  private locales: Record<string, LoadedLocale> = {};
  private activeCode: string | null = null;
  private warned: Record<string, true> = {};
  private listeners: LocaleListener[] = [];
  private pluralCache: Record<string, Intl.PluralRules | { select(n: number): string }> = {};
  private collators: Record<string, Intl.Collator | { compare(a: string, b: string): number }> = {};

  /** فهرس اللغات — ترتيبه ترتيب ظهورها في المبدّل. */
  catalogue: readonly CatalogueEntry[] = [];
  /** كل مفتاح سقط إلى لغة أخرى أو غاب تماماً. */
  readonly missing: MissingKey[] = [];
  /** وضع التشخيص: يُظهر المفتاح بين قوسين. */
  debug = false;
  /** في الاختبارات: المفتاح الناقص يرمي بدل أن يسقط. */
  strict = false;
  /** ما يُنادى عند تسجيل مفتاح ناقص — الفحص يشبكه. */
  onMissing: ((entry: MissingKey) => void) | null = null;

  /**
   * يعرّف لغة.
   * @param code الرمز.
   * @param meta ما تعلنه اللغة.
   * @param messages شجرة الرسائل.
   */
  define(code: string, meta: LocaleMeta, messages: MessageTree): this {
    this.locales[code] = { code, meta, messages: flatten(messages) };
    return this;
  }

  /** هل اللغة محمّلة؟ @param code الرمز. */
  has(code: string): boolean {
    return own(this.locales, code);
  }
  /** اللغات المحمّلة. */
  loaded(): string[] {
    return Object.keys(this.locales);
  }
  /** اللغة النشطة. */
  active(): string {
    return this.activeCode ?? SOURCE;
  }
  /** ما تعلنه لغة عن نفسها. @param code الرمز، أو النشطة. */
  meta(code?: string): LocaleMeta {
    const l = this.locales[code ?? this.active()];
    if (!l) throw new Error("I18n.meta: لغة غير محمّلة · locale not loaded: " + (code ?? "?"));
    return l.meta;
  }
  /** رسائل لغة مُسطَّحةً. @param code الرمز. */
  messages(code: string): FlatMessages {
    return this.locales[code]?.messages ?? {};
  }

  /* ═════════════════════════════════ سياسة المفتاح الناقص ══════════════
     لماذا الاحتياط إلى العربية لا إظهار المفتاح؟ لأن رأس عمود فارغ أو مفتاح
     خام فوق عمود أرقام أسوأ من كلمة بلغة أخرى: المحاسب يقرأ الرقم تحت رأس لا
     يفهمه فيقرأه خطأً — ويُكمل الرأس من عنده ولا يبلّغ أحداً. */
  private record(code: string, key: string): void {
    const sig = code + "|" + key;
    if (this.warned[sig]) return;
    this.warned[sig] = true;
    const entry = { locale: code, key };
    this.missing.push(entry);
    if (typeof console !== "undefined" && console.warn) {
      console.warn("[i18n] مفتاح ناقص · missing key: " + key + " (" + code + ")");
    }
    this.onMissing?.(entry);
  }

  /** سلسلة الاحتياط لهذه اللغة. @param code الرمز. */
  chain(code?: string): string[] {
    const out: string[] = [];
    const seen: Record<string, 1> = {};
    let c: string | null | undefined = code ?? this.active();
    while (c && !seen[c]) {
      seen[c] = 1;
      out.push(c);
      c = this.locales[c]?.meta.fallback ?? null;
    }
    if (!seen[SOURCE]) out.push(SOURCE);
    return out;
  }

  /** يبحث عن مفتاح عبر سلسلة الاحتياط. @param key المفتاح. @param code اللغة. */
  lookup(key: string, code?: string): { value: string | PluralBag; from: string } | null {
    for (const link of this.chain(code)) {
      const L = this.locales[link];
      if (L && own(L.messages, key)) return { value: L.messages[key] as string | PluralBag, from: link };
    }
    return null;
  }

  /* ═════════════════════════════════════ الاستبدال والترجمة ═══════════ */
  /* معاملات محيطية: قيمٌ تخصّ اللغة النشطة ولا معنى لكتابتها في كل موضع نداء.
     {currency} و{currencyCode} أكثرها تكراراً — «مدين (ر.س)» تصير
     «Debit (SAR)» بلا سطر شيفرة واحد في الشاشة. */
  private ambient(): Record<string, string> {
    const n = this.meta().numbers;
    return { currency: n.currency ?? "", currencyCode: n.currencyCode ?? "" };
  }

  private interpolate(text: string, params: Record<string, unknown> | null, depth = 0): string {
    const amb = this.ambient();
    return String(text).replace(/\{(\w+)\}/g, (m, name: string) => {
      let v: unknown;
      if (params && own(params, name)) v = params[name];
      else if (own(amb, name)) v = amb[name];
      else return m;
      if (v instanceof Display) {
        throw new TypeError(
          "t: لا تُمرَّر قيمة Display كمعامل نصّي — استعمل <Amount> داخل الوسم. " +
            "[a Display must not be interpolated into a string]"
        );
      }
      /* مرجع مفتاح داخل معامل: "@acct.class.assets" يُترجَم بدوره. العمق محدود
         بواحد فلا تنشأ حلقة. */
      if (typeof v === "string" && v.charAt(0) === "@" && !(depth > 0)) {
        return this.translate(v.slice(1), null, this.active(), 1);
      }
      return String(v);
    });
  }

  private translate(
    key: string,
    params: Record<string, unknown> | null,
    code?: string,
    depth = 0
  ): string {
    const at = code ?? this.active();
    const hit = this.lookup(key, at);
    if (!hit) {
      this.record(at, key);
      if (this.strict) throw new Error("t: مفتاح غير معرَّف · undefined key: " + key);
      return this.debug ? "⟦" + key + "⟧" : key;
    }
    if (hit.from !== at) this.record(at, key);
    let v = hit.value;
    if (isPluralBag(v)) v = v.other; /* استعمال كيس جمع بلا عدد — يُصلحه الفحص */
    return this.interpolate(v, params, depth);
  }

  /** ترجمة مفتاح. @param key المفتاح. @param params معاملات الاستبدال. */
  t = (key: string, params?: Record<string, unknown>): string =>
    this.translate(key, params ?? null, this.active());

  /** ترجمة بلغة بعينها. @param code اللغة. @param key المفتاح. @param params المعاملات. */
  tIn = (code: string, key: string, params?: Record<string, unknown>): string =>
    this.translate(key, params ?? null, code);

  /** هل المفتاح معرَّف؟ @param key المفتاح. @param code اللغة. */
  tHas = (key: string, code?: string): boolean => !!this.lookup(key, code);

  /** القيمة الخام كما في ملفّ اللغة. @param key المفتاح. @param code اللغة. */
  tRaw = (key: string, code?: string): string | PluralBag | null => this.lookup(key, code)?.value ?? null;

  /* ═════════════════════════════════════ الجمع ════════════════════════
     العربية ستّ فئات، والإنجليزية اثنتان، والأردية اثنتان، والهندية اثنتان
     — لكنّ الهندية تضع الصفر في فئة one. ولهذا `count === 1 ? a : b` خطأ في
     أربع لغات من أربع، لا في واحدة. */
  private rules(code: string): { select(n: number): string } {
    const lc = this.locales[code]?.meta.pluralLocale ?? code;
    if (!this.pluralCache[lc]) {
      try {
        this.pluralCache[lc] = new Intl.PluralRules(lc);
      } catch {
        this.pluralCache[lc] = { select: (n: number) => (n === 1 ? "one" : "other") };
      }
    }
    return this.pluralCache[lc];
  }

  /** فئات الجمع التي تعرفها هذه اللغة فعلاً. @param code اللغة. */
  pluralCategories(code: string): string[] {
    try {
      const lc = this.locales[code]?.meta.pluralLocale ?? code;
      return new Intl.PluralRules(lc)
        .resolvedOptions()
        .pluralCategories.slice()
        .sort(
          (a, b) =>
            (CLDR as readonly string[]).indexOf(a) - (CLDR as readonly string[]).indexOf(b)
        );
    } catch {
      return ["one", "other"];
    }
  }

  /** فئة عدد بعينه. @param n العدد. @param code اللغة. */
  pluralCategory(n: number, code?: string): string {
    return this.rules(code ?? this.active()).select(Number(n));
  }

  /** ترجمة بالعدد. @param key المفتاح. @param count العدد. @param params المعاملات. @param code اللغة. */
  tPlural = (
    key: string,
    count: number,
    params?: Record<string, unknown>,
    code?: string
  ): string => {
    const at = code ?? this.active();
    const hit = this.lookup(key, at);
    if (!hit) {
      this.record(at, key);
      if (this.strict) throw new Error("t.plural: مفتاح غير معرَّف · undefined key: " + key);
      return this.debug ? "⟦" + key + " #" + count + "⟧" : key;
    }
    if (hit.from !== at) this.record(at, key);
    const bag = hit.value;
    if (!isPluralBag(bag)) return this.interpolate(bag, this.mergeCount(params, count));
    /* الأسبقية للصيغة الصريحة "=N" ثم لفئة CLDR. والصفر خاصّةً: العربية تملك
       فئة zero حقيقية، والإنجليزية والأردية والهندية لا تملكها، فتحتاج "=0"
       لتقول «لا شيء» بدل «0 عنصر». */
    const exact = "=" + Number(count);
    if (own(bag, exact) && bag[exact] !== undefined) {
      return this.interpolate(bag[exact], this.mergeCount(params, count));
    }
    const cat = this.rules(hit.from).select(Number(count));
    const form = (own(bag, cat) ? bag[cat] : bag.other) as string;
    return this.interpolate(form, this.mergeCount(params, count));
  };

  private mergeCount(params: Record<string, unknown> | undefined, count: number): Record<string, unknown> {
    const p: Record<string, unknown> = { ...(params ?? {}) };
    /* العدد يدخل النصّ بأرقام العرض الخاصّة باللغة، وهو عرض محض. */
    p.count = this.shapeDigits(String(count));
    p.countRaw = String(count);
    return p;
  }

  /* ═════════════════════════ الأرقام والتواريخ — عرض فقط ═════════════ */
  private numOpts(): NumberOptions {
    return this.meta().numbers;
  }

  /** أشكال الأرقام المعروفة. */
  static readonly DIGIT_SETS: Record<string, string> = {
    latn: "0123456789",
    arab: "٠١٢٣٤٥٦٧٨٩",
    arabext: "۰۱۲۳۴۵۶۷۸۹",
    deva: "०१२३४५६७८९",
  };

  /** تحويل شكل الرقم — عرض فقط، ولا يعود إلى أي قيمة تُرسَل. @param ascii الرقم لاتينياً. @param set الشكل. */
  shapeDigits(ascii: string, set?: string): string {
    const use = set ?? this.numOpts().digits ?? "latn";
    const map = I18n.DIGIT_SETS[use];
    if (!map || use === "latn") return ascii;
    return String(ascii).replace(/[0-9]/g, (d) => map.charAt(+d));
  }

  /* تجميع بأحجام مجموعات معلَنة في ملفّ اللغة: [3] غربي، [3,2] هندي (لكh/كرور). */
  private groupInt(intPart: string, sep: string, sizes: number[]): string {
    if (!sep) return intPart;
    const use = sizes && sizes.length ? sizes : [3];
    let out = "";
    let i = intPart.length;
    let s = 0;
    while (i > 0) {
      const size = use[Math.min(s, use.length - 1)] as number;
      const start = Math.max(0, i - size);
      out = intPart.slice(start, i) + (out ? sep + out : "");
      i = start;
      s++;
    }
    return out;
  }

  /**
   * المبلغ معروضاً بلغة الواجهة. القيمة تبقى نصّاً، والحساب نصّي بلا عائم.
   * @param raw المبلغ نصّاً كما وصل من الخادم.
   * @param opts المقياس.
   */
  amount(raw: string, opts?: { scale?: number }): Display {
    const scale = opts?.scale ?? 2;
    let canonical = moneyText(raw, scale);
    if (canonical === null || canonical === undefined) canonical = "";
    const n = this.numOpts();
    const machine = canonical.replace(/,/g, ""); /* ASCII صرف */
    let text = canonical;
    if (text) {
      const neg = text.charAt(0) === "-";
      if (neg) text = text.slice(1);
      const parts = text.split(".");
      text =
        this.groupInt((parts[0] as string).replace(/,/g, ""), n.group, n.groupSizes) +
        (parts.length > 1 ? n.decimal + parts[1] : "");
      text = this.shapeDigits(text, n.digits);
      if (neg) text = (n.minus ?? "-") + text;
    }
    return new Display(text, machine, "amount", { ltr: true, scale });
  }

  /** عدد صحيح معروضاً. @param raw العدد. */
  integer(raw: string | number): Display {
    let s = toLatinDigits(String(raw)).replace(/[^\d-]/g, "");
    const n = this.numOpts();
    const neg = s.charAt(0) === "-";
    if (neg) s = s.slice(1);
    const text = this.shapeDigits(this.groupInt(s || "0", n.group, n.groupSizes), n.digits);
    return new Display(
      (neg ? n.minus ?? "-" : "") + text,
      (neg ? "-" : "") + (s || "0"),
      "integer",
      { ltr: true }
    );
  }

  /** نسبة معروضةً. @param raw النسبة. */
  percent(raw: string | number): Display {
    const n = this.numOpts();
    const s = String(raw).replace(/[^\d.-]/g, "");
    return new Display(this.shapeDigits(s, n.digits) + (n.percentSuffix ?? "%"), s, "percent", {
      ltr: true,
    });
  }

  /**
   * التاريخ: مبنيّ من أسماء ملفّ اللغة وترتيبه، لا من ICU. و ISO يبقى ASCII.
   * @param value التاريخ.
   * @param style "long" أو غيره.
   */
  date(value: Date | string | null, style?: "long" | "short"): Display {
    const d = value instanceof Date ? value : parseIsoDate(value);
    const dts = this.meta().dates ?? {};
    if (!d) return new Display(dts.emptyDash ?? "—", "", "date", { ltr: true });
    const pad = (x: number) => (x < 10 ? "0" : "") + x;
    const iso = d.getUTCFullYear() + "-" + pad(d.getUTCMonth() + 1) + "-" + pad(d.getUTCDate());
    const Y = String(d.getUTCFullYear());
    const M = pad(d.getUTCMonth() + 1);
    const D = pad(d.getUTCDate());
    let text: string;
    if (style === "long") {
      const wd = (dts.weekdays ?? [])[d.getUTCDay()] ?? "";
      const mo = (dts.months ?? [])[d.getUTCMonth()] ?? M;
      text = (dts.longPattern ?? "{weekday}, {day} {month} {year}")
        .replace("{weekday}", wd)
        .replace("{day}", this.shapeDigits(String(d.getUTCDate())))
        .replace("{month}", mo)
        .replace("{year}", this.shapeDigits(Y))
        .replace("{era}", dts.eraGregorian ?? "")
        .trim();
    } else {
      text = (dts.shortPattern ?? "{year}/{month}/{day}")
        .replace("{year}", this.shapeDigits(Y))
        .replace("{month}", this.shapeDigits(M))
        .replace("{day}", this.shapeDigits(D));
    }
    return new Display(text, iso, "date", { ltr: true });
  }

  /**
   * هجري أم القرى — تحويل عرض محض لا يعود إلى التخزين أبداً. نستعمل Intl
   * بأرقام لاتينية صريحة ثم نبني النصّ بأسماء ملفّ اللغة، فلا يتسرّب محرف
   * تحكّم من ICU إلى المخرَج.
   * @param value التاريخ.
   */
  hijri(value: Date | string | null): Display | null {
    const d = value instanceof Date ? value : parseIsoDate(value);
    const dts = this.meta().dates ?? {};
    if (!d || !dts.hijriMonths) return null;
    try {
      const f = new Intl.DateTimeFormat("en-u-ca-islamic-umalqura-nu-latn", {
        day: "numeric",
        month: "numeric",
        year: "numeric",
        timeZone: "UTC",
      });
      const parts: Record<string, string> = {};
      for (const p of f.formatToParts(d)) parts[p.type] = p.value;
      if (!parts.year || !parts.month || !parts.day) return null;
      /* عدد من ICU بأرقام لاتينية صريحة، لا قيمة من الخادم. */
      const mi = Number.parseInt(parts.month, 10) - 1;
      if (!(mi >= 0 && mi < 12)) return null;
      const text =
        this.shapeDigits(parts.day) +
        " " +
        dts.hijriMonths[mi] +
        " " +
        this.shapeDigits(parts.year) +
        " " +
        (dts.eraHijri ?? "");
      /* لا machine: التاريخ الهجري لا يُخزَّن ولا يُرسَل. */
      return new Display(text.trim(), "", "hijri-display-only", { ltr: false });
    } catch {
      return null;
    }
  }

  /** المقارنة والفرز بترتيب اللغة النشطة، لا "ar" مثبّتة. @param code اللغة. */
  collator(code?: string): { compare(a: string, b: string): number } {
    const at = code ?? this.active();
    if (!this.collators[at]) {
      try {
        this.collators[at] = new Intl.Collator(at, { numeric: true, sensitivity: "base" });
      } catch {
        this.collators[at] = { compare: (a, b) => (a < b ? -1 : a > b ? 1 : 0) };
      }
    }
    return this.collators[at];
  }

  /* ═════════════════════════════════════ تفعيل لغة ════════════════════ */
  /** يفعّل لغة ويُخطر المستمعين. @param code الرمز. */
  use(code: string): this {
    if (!this.locales[code]) {
      console.warn("[i18n] لغة غير محمّلة · locale not loaded: " + code);
      return this;
    }
    this.activeCode = code;
    const m = this.locales[code].meta;
    for (const fn of this.listeners) {
      try {
        fn(code, m);
      } catch {
        /* مستمعٌ يسقط لا يمنع بقيّة المستمعين. */
      }
    }
    return this;
  }

  /** يشترك في تغيّر اللغة. @param fn المستمع. */
  onChange(fn: LocaleListener): () => void {
    this.listeners.push(fn);
    return () => {
      const i = this.listeners.indexOf(fn);
      if (i >= 0) this.listeners.splice(i, 1);
    };
  }

  /**
   * الأسبقية: ?lang= الصريح ← المحفوظ ← لغة المتصفّح ← لغة المصدر.
   * ⚠ المحفوظ كان يسبق ?lang=، فلا يستطيع أحد إرسال رابط بلغة بعينها إلى
   * مراجعٍ زار الصفحة من قبل. الطلب الصريح يسبق الذاكرة دائماً.
   * @param search نصّ الاستعلام.
   * @param stored المحفوظ.
   * @param languages لغات المتصفّح.
   */
  preferred(search: string, stored: string | null, languages: readonly string[]): string {
    const q = /[?&]lang=([\w-]+)/.exec(search);
    if (q && q[1] && this.locales[q[1]]) return q[1];
    if (stored && this.locales[stored]) return stored;
    for (const nav of languages) {
      const base = String(nav || "").split("-")[0] as string;
      if (this.locales[base]) return base;
    }
    return SOURCE;
  }

  /** مبلغ داخل جملة: يعيد Html معزولاً — المصرف الوحيد للمبلغ داخل نصّ. */
  amountHtml(raw: string, cls?: string): Html {
    const d = this.amount(raw);
    const span = document.createElement("span");
    span.className = "amt" + (cls ? " " + cls : "");
    d.into(span);
    return new Html(span.outerHTML);
  }
}

/** تحليل تاريخ ISO بأرقام لاتينية. لا Date.parse على نصّ محلّي. */
export function parseIsoDate(text: string | null | undefined): Date | null {
  if (!text) return null;
  const s = toLatinDigits(String(text)).trim();
  const m = /^(\d{4})-(\d{2})-(\d{2})/.exec(s);
  if (!m) return null;
  const y = Number(m[1]);
  const mo = Number(m[2]);
  const d = Number(m[3]);
  if (mo < 1 || mo > 12 || d < 1 || d > 31) return null;
  return new Date(Date.UTC(y, mo - 1, d));
}

export type { DisplayKind };
