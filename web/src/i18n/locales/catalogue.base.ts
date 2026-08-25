/* منقول آلياً من design/i18n/locales/manifest.js — لا تُحرِّره بيدك.
   Ported from design/i18n/locales/manifest.js — do not edit by hand. */
import type { CatalogueEntry } from "../types";

export const CATALOGUE: readonly CatalogueEntry[] = [
  {
    "code": "ar",
    "native": "العربية",
    "english": "Arabic",
    "dir": "rtl"
  },
  {
    "code": "en",
    "native": "English",
    "english": "English",
    "dir": "ltr"
  },
  {
    "code": "ur",
    "native": "اردو",
    "english": "Urdu",
    "dir": "rtl"
  },
  {
    "code": "hi",
    "native": "हिन्दी",
    "english": "Hindi",
    "dir": "ltr"
  }
] as const;
