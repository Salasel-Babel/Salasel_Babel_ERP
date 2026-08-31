/* أنواع طبقة التدويل. / i18n layer types. */

/** كيس جمع على طريقة CLDR مع صيغ عددية صريحة ("=0"). */
export interface PluralBag {
  zero?: string;
  one?: string;
  two?: string;
  few?: string;
  many?: string;
  other: string;
  [exact: string]: string | undefined;
}

/** شجرة الرسائل كما تُكتب في ملفّ اللغة. */
export interface MessageTree {
  [key: string]: string | PluralBag | MessageTree;
}

/** الرسائل بعد التسطيح: "a.b.c" → نصّ أو كيس جمع. */
export type FlatMessages = Record<string, string | PluralBag>;

/** إعدادات الأرقام المعلَنة في ملفّ اللغة — لا تأتي من Intl أبداً. */
export interface NumberOptions {
  group: string;
  decimal: string;
  groupSizes: number[];
  digits: "latn" | "arab" | "arabext" | "deva";
  minus?: string;
  percentSuffix?: string;
  currency?: string;
  currencyCode?: string;
}

/** إعدادات التاريخ المعلَنة في ملفّ اللغة. */
export interface DateOptions {
  shortPattern?: string;
  longPattern?: string;
  eraGregorian?: string;
  eraHijri?: string;
  emptyDash?: string;
  months?: string[];
  weekdays?: string[];
  hijriMonths?: string[];
}

/** خطّ اللغة. الخط رمز كالألوان — لا يُكتب اسمه في أي مكوّن. */
export interface FontOptions {
  /** رابط خارجي، بيانات فقط: لا يُحقن وقت التشغيل (web/README.md §الخطوط). */
  href?: string;
  ui: string;
  display?: string;
  displayLineHeight?: string | number;
}

/** ما تعلنه اللغة عن نفسها. */
export interface LocaleMeta {
  lang: string;
  dir: "rtl" | "ltr";
  native: string;
  english: string;
  fallback?: string | null;
  source?: boolean;
  translation?: string;
  pluralLocale?: string;
  numbers: NumberOptions;
  dates?: DateOptions;
  font?: FontOptions;
  amountInWords?: boolean;
  cssStrings?: string[];
}

/** سطر في فهرس اللغات. لغة خامسة = ملفّ + سطر. */
export interface CatalogueEntry {
  code: string;
  native: string;
  english: string;
  dir: "rtl" | "ltr";
}
