/* دمج ملفّ اللغة المنقول من design/ مع مفاتيح الواجهة الجديدة.
   Merges the catalogue ported from design/ with the web-app keys.
   لغة خامسة = ملفّان + سطر هنا. لا شيء غير ذلك. */
import type { CatalogueEntry, LocaleMeta, MessageTree } from "../types";

import { meta as arMeta, messages as arBase } from "./ar.base";
import { meta as enMeta, messages as enBase } from "./en.base";
import { meta as urMeta, messages as urBase } from "./ur.base";
import { meta as hiMeta, messages as hiBase } from "./hi.base";
import { messages as arWeb } from "./ar.web";
import { messages as enWeb } from "./en.web";
import { messages as urWeb } from "./ur.web";
import { messages as hiWeb } from "./hi.web";
import { CATALOGUE as PORTED_CATALOGUE } from "./catalogue.base";

function merge(base: MessageTree, extra: MessageTree): MessageTree {
  const out: MessageTree = { ...base };
  for (const [key, value] of Object.entries(extra)) {
    const current = out[key];
    if (
      current &&
      typeof current === "object" &&
      !Array.isArray(current) &&
      value &&
      typeof value === "object" &&
      !Array.isArray(value) &&
      !("other" in value)
    ) {
      out[key] = merge(current as MessageTree, value);
    } else {
      out[key] = value;
    }
  }
  return out;
}

/** لغة جاهزة للتعريف. */
export interface LocaleBundle {
  code: string;
  meta: LocaleMeta;
  messages: MessageTree;
}

/** اللغات الأربع. الترتيب ترتيب الفهرس. */
export const LOCALES: readonly LocaleBundle[] = [
  { code: "ar", meta: arMeta, messages: merge(arBase, arWeb) },
  { code: "en", meta: enMeta, messages: merge(enBase, enWeb) },
  { code: "ur", meta: urMeta, messages: merge(urBase, urWeb) },
  { code: "hi", meta: hiMeta, messages: merge(hiBase, hiWeb) },
];

/** فهرس اللغات كما نُقل من design/i18n/locales/manifest.js. */
export const CATALOGUE: readonly CatalogueEntry[] = PORTED_CATALOGUE;
