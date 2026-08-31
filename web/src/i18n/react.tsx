/* ═══════════════════════════════════════════════════════════════════════════
   ربط طبقة التدويل بـReact
   ───────────────────────────────────────────────────────────────────────────
   الحدّ الحاكم يبقى كما هو: القيمة المنسّقة **لا تصير نصّاً**. ولذلك لا يوجد
   في هذا الملف مسارٌ يكتب Display داخل JSX كأنها نصّ — المكوّن <Amount> يمسك
   العنصر بمرجع ويستدعي d.into(el)، وهو المصرف الوحيد. ومحاولة كتابة
   {fmt.amount(x)} داخل JSX ترمي وقت التشغيل قبل أن تصل إلى الشاشة.
   ═══════════════════════════════════════════════════════════════════════════ */
import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  useState,
  type ReactNode,
} from "react";
import type { I18n } from "./engine";
import type { Display } from "./display";
import type { LocaleMeta } from "./types";
import { i18n as sharedI18n } from "./setup";
import { Money } from "../api/money";

interface LocaleContextValue {
  i18n: I18n;
  locale: string;
  meta: LocaleMeta;
  setLocale: (code: string) => void;
}

const LocaleContext = createContext<LocaleContextValue | null>(null);

const STORE_KEY = "sb-locale";

function readStored(): string | null {
  try {
    return globalThis.localStorage?.getItem(STORE_KEY) ?? null;
  } catch {
    return null;
  }
}
function writeStored(code: string): void {
  try {
    globalThis.localStorage?.setItem(STORE_KEY, code);
  } catch {
    /* وضع التصفّح الخاص: التفضيل لا يُحفظ، والصفحة تعمل. */
  }
}

/**
 * يضبط اللغة على الجذر ويوفّرها للشجرة.
 * @param props الأبناء واللغة الابتدائية الاختيارية.
 */
export function LocaleProvider(props: { children: ReactNode; initial?: string; i18n?: I18n }): ReactNode {
  const engine = props.i18n ?? sharedI18n;
  const [locale, setLocaleState] = useState<string>(() => {
    if (props.initial && engine.has(props.initial)) return props.initial;
    return engine.preferred(
      globalThis.location?.search ?? "",
      readStored(),
      globalThis.navigator?.languages ?? []
    );
  });

  const meta = useMemo(() => {
    engine.use(locale);
    return engine.meta(locale);
  }, [engine, locale]);

  useLayoutEffect(() => {
    const root = document.documentElement;
    root.setAttribute("lang", meta.lang);
    root.setAttribute("dir", meta.dir);
    root.setAttribute("data-locale", locale);
    /* الخطّ رمزٌ كالألوان: يُكتب على :root فتتبعه كل المكوّنات، ولا اسم خطّ
       واحد مكتوب في أي مكوّن. ويُمسَح إن لم تُعلنه اللغة، فلا ترث لغةٌ خطّ
       التي قبلها. */
    const style = root.style;
    const set = (name: string, value?: string | number) => {
      if (value) style.setProperty(name, String(value));
      else style.removeProperty(name);
    };
    set("--font-sans", meta.font?.ui);
    set("--font-display", meta.font?.display ?? meta.font?.ui);
    set("--line-display", meta.font?.displayLineHeight);
    /* نصوص CSS (content:) لا تقرأ سمة، فتُضخّ خصائصَ مخصّصة من مفاتيح اللغة. */
    for (const name of meta.cssStrings ?? []) {
      style.setProperty("--i18n-" + name, JSON.stringify(engine.t("css." + name)));
    }
    root.removeAttribute("data-i18n-pending");
  }, [engine, locale, meta]);

  const setLocale = useCallback(
    (code: string) => {
      if (!engine.has(code)) return;
      writeStored(code);
      setLocaleState(code);
    },
    [engine]
  );

  const value = useMemo<LocaleContextValue>(
    () => ({ i18n: engine, locale, meta, setLocale }),
    [engine, locale, meta, setLocale]
  );

  return <LocaleContext.Provider value={value}>{props.children}</LocaleContext.Provider>;
}

/** اللغة النشطة وأدواتها. */
export function useLocale(): LocaleContextValue {
  const value = useContext(LocaleContext);
  if (!value) throw new Error("useLocale: خارج LocaleProvider. / outside LocaleProvider.");
  return value;
}

/** دالّة الترجمة مربوطةً باللغة النشطة. */
export function useT(): {
  t: (key: string, params?: Record<string, unknown>) => string;
  tp: (key: string, count: number, params?: Record<string, unknown>) => string;
} {
  const { i18n, locale } = useLocale();
  return useMemo(
    () => ({
      t: (key: string, params?: Record<string, unknown>) => {
        void locale;
        return i18n.t(key, params);
      },
      tp: (key: string, count: number, params?: Record<string, unknown>) => {
        void locale;
        return i18n.tPlural(key, count, params);
      },
    }),
    [i18n, locale]
  );
}

/**
 * يكتب قيمة معروضة في عنصر عبر المصرف الوحيد d.into(el).
 * @param props القيمة والصنف والوسم.
 */
export function Rendered(props: {
  display: Display;
  className?: string;
  as?: "span" | "td" | "div" | "strong";
  title?: string;
}): ReactNode {
  const ref = useRef<HTMLElement | null>(null);
  const { display } = props;
  useLayoutEffect(() => {
    const node = ref.current;
    if (node) display.into(node);
  }, [display]);
  const Tag = (props.as ?? "span") as "span";
  return <Tag ref={ref} className={props.className} title={props.title} />;
}

/**
 * مبلغ معروضاً بلغة الواجهة. القيمة تبقى Money، ولا تصير رقماً ولا نصّاً حرّاً.
 * @param props المبلغ والصنف.
 */
export function Amount(props: { value: Money; className?: string; as?: "span" | "td" | "div" }): ReactNode {
  const { i18n, locale } = useLocale();
  const { value } = props;
  const display = useMemo(() => {
    void locale;
    return i18n.amount(value.text);
  }, [i18n, locale, value]);
  const zero = value.isZero;
  const negative = value.isNegative;
  /* المعروض مقرَّب إلى منزلتين (وهو مقياس الريال)، والقيمة على السلك قد تحمل
     أربعاً. فالنصّ الأصلي يبقى في السمة title: العرض تقريبٌ مُعلَن، لا قيمة
     بديلة — والمحاسب يبلغ الأصل بحركة واحدة. */
  return (
    <Rendered
      display={display}
      title={value.text}
      as={props.as}
      className={
        (props.className ? props.className + " " : "") +
        "amt" +
        (zero ? " amt-zero" : "") +
        (negative ? " amt-neg" : "")
      }
    />
  );
}

/* ═══════════════════════════════════════════════════════════════════════════
   عددٌ عشري نصّي — الكمّية والنسبة، لا المال
   ───────────────────────────────────────────────────────────────────────────
   المال يمرّ بـ<Amount> لأن نوعه Money ومقياسه المعروض منزلتان. والكمّية
   (Magnitude، مقياسها ستّ) والنسبة التعاقدية (Rate، مقياسها ثمانٍ) نصّان
   محتجزان لا Money، ومقياسهما **ليس منزلتين**. فعرضُهما بـ<Amount> كان
   يقرّب الكمّية إلى الهللة ويُسقط خانات النسبة صامتاً.

   والمقياس هنا **يُقرأ من النصّ الواصل** لا يُفترض: عدد ما بعد الفاصلة
   كما كتبه الخادم. فلا خانة تُزاد ولا خانة تُحذف، ولا يمرّ الرقم بعائم في
   أي خطوة (الحساب كلّه نصّي في decimal-text.ts).
   ═══════════════════════════════════════════════════════════════════════════ */

/** مقياسُ نصٍّ عشري: عدد خاناته بعد الفاصلة، بلا تحويل إلى رقم. */
function scaleOfText(text: string): number {
  const dot = text.indexOf(".");
  return dot < 0 ? 0 : text.length - dot - 1;
}

/**
 * عدد عشري نصّي معروضاً بأرقام اللغة **بمقياسه كما وصل** — بلا تقريب وبلا عائم.
 * @param props النصّ العشري والصنف.
 */
export function Decimal(props: { value: string; className?: string; title?: string }): ReactNode {
  const { i18n, locale } = useLocale();
  const { value } = props;
  const display = useMemo(() => {
    void locale;
    return i18n.amount(value, { scale: scaleOfText(value) });
  }, [i18n, locale, value]);
  return (
    <Rendered
      display={display}
      title={props.title ?? value}
      className={(props.className ? props.className + " " : "") + "num"}
    />
  );
}

/**
 * عدد صحيح معروضاً بأرقام اللغة.
 * @param props القيمة والصنف.
 */
export function Num(props: { value: number | string; className?: string }): ReactNode {
  const { i18n, locale } = useLocale();
  const display = useMemo(() => {
    void locale;
    return i18n.integer(props.value);
  }, [i18n, locale, props.value]);
  return <Rendered display={display} className={(props.className ?? "") + " num"} />;
}

/** يستدعي دالّة عند تغيّر اللغة — للمكوّنات التي تُمسك DOM بنفسها. */
export function useOnLocaleChange(fn: (code: string) => void): void {
  const { locale } = useLocale();
  const ref = useRef(fn);
  useEffect(() => {
    ref.current = fn;
  }, [fn]);
  useEffect(() => {
    ref.current(locale);
  }, [locale]);
}
