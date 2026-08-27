/* ═══════════════════════════════════════════════════════════════════════════
   حلّ اسم مُترجَم إلى لغة العرض — قاعدة واحدة، في موضع واحد
   Resolving a translated name into the display language — one rule, one place
   ───────────────────────────────────────────────────────────────────────────
   العقد يحمل **سجلّاً عربياً** وخريطة ترجمات بوسم اللغة (ADR-0021). والارتداد
   حين لا ترجمة هو **إلى السجلّ العربي** لا إلى الفراغ ولا إلى المفتاح ولا إلى
   الإنجليزية: الإنجليزية واحدة من N، ولا شرط عليها في أي سطر هنا.

   وكانت هذه القاعدة مكتوبة داخل جدول ميزان المراجعة وحده. ولمّا صار اسم
   **المنشأة** يُعرض في شاشة الدخول بالقاعدة نفسها، كان أمامنا نسخُها ثانيةً —
   ونسختان تنحرفان عند أول تعديل، فيرى المحاسب الهندي اسم حسابه بلغته واسم
   منشأته بالعربية. فرُفعت إلى هنا وتقرأ منها الشاشتان.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { NameValue } from "../api/generated/types";
import { SOURCE } from "../i18n/engine";

/** وسم لغة السجلّ — يُقرأ من الطبقة لا من نصّ مكتوب في هذا الملف. */
export const RECORD_TAG: string = SOURCE;

/** ما حُلّ إليه الاسم في لغةٍ بعينها، ومن أي وسم جاء. */
export interface ResolvedName {
  /** النصّ المعروض. */
  text: string;
  /** الوسم الذي أعطاه فعلاً — وسم السجلّ عند الارتداد. */
  tag: string;
  /** هل ارتدّ إلى السجلّ لغياب ترجمة مطابقة؟ */
  fallback: boolean;
}

/**
 * يحلّ اسماً مُترجَماً إلى لغة العرض: مطابقة تامّة، ثم الوسم الأوّلي
 * (ur-PK ⇒ ur)، ثم ارتداداً إلى السجلّ العربي.
 * @param recordAr الاسم في السجلّ — عربيّ وغير فارغ.
 * @param translations الترجمات بوسم اللغة كما وصلت من العقد.
 * @param locale وسم لغة الواجهة.
 * @returns النصّ ووسمه وهل كان ارتداداً.
 */
export function resolveTranslatedName(
  recordAr: string,
  translations: readonly NameValue[],
  locale: string
): ResolvedName {
  const record: ResolvedName = { text: recordAr, tag: RECORD_TAG, fallback: false };
  if (!locale || locale === RECORD_TAG || locale.startsWith(RECORD_TAG + "-")) return record;

  const exact = translations.find((entry) => entry.name === locale);
  if (exact) return { text: exact.value, tag: exact.name, fallback: false };

  const dash = locale.indexOf("-");
  if (dash > 0) {
    const primary = locale.slice(0, dash);
    const broader = translations.find((entry) => entry.name === primary);
    if (broader) return { text: broader.value, tag: broader.name, fallback: false };
  }

  return { ...record, fallback: true };
}
