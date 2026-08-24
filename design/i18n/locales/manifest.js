/* ═══════════════════════════════════════════════════════════════════════════
   فهرس اللغات · Locale catalogue
   ───────────────────────────────────────────────────────────────────────────
   إضافة لغة خامسة = ملفٌّ واحد في هذا المجلد + سطرٌ واحد هنا. لا شيء غير ذلك:
   لا خطوة بناء، ولا تعديل مكوّن، ولا تعديل صفحة.
   الترتيب هنا هو ترتيب ظهورها في مبدّل اللغة.
   ═══════════════════════════════════════════════════════════════════════════ */
(function (global) {
  "use strict";
  var SB = global.SB || (global.SB = {});
  SB.I18N = SB.I18N || {};
  SB.I18N.catalog = [
    { code: "ar", file: "locales/ar.js", native: "العربية",  english: "Arabic",   dir: "rtl" },
    { code: "en", file: "locales/en.js", native: "English",  english: "English",  dir: "ltr" },
    { code: "ur", file: "locales/ur.js", native: "اردو",     english: "Urdu",     dir: "rtl" },
    { code: "hi", file: "locales/hi.js", native: "हिन्दी",     english: "Hindi",    dir: "ltr" }
  ];
})(window);
