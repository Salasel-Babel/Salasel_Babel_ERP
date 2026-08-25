/* بناء طبقة التدويل للتطبيق: نسخة واحدة محمّلة باللغات الأربع. */
import { I18n } from "./engine";
import { CATALOGUE, LOCALES } from "./locales";

/**
 * ينشئ طبقة تدويل محمّلة باللغات الأربع.
 * الاختبارات تنادي هذه الدالّة أيضاً فلا تختبر نسخة مختلفة عمّا يشحن.
 */
export function createI18n(): I18n {
  const i18n = new I18n();
  for (const bundle of LOCALES) i18n.define(bundle.code, bundle.meta, bundle.messages);
  i18n.catalogue = CATALOGUE;
  return i18n;
}

/** النسخة التي يستعملها التطبيق. */
export const i18n = createI18n();
