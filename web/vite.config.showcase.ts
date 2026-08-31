/* ═══════════════════════════════════════════════════════════════════════════
   بناء صفحة العرض — ملفٌّ واحد، بلا أصلٍ خارجيّ واحد
   ───────────────────────────────────────────────────────────────────────────
   المُضيف يحجب كل أصلٍ خارجيّ **صامتاً**: لا خطأ ولا سجلّ، فقط خطٌّ ناقص أو
   خطٌّ لا يُحمَّل. ولذلك يُضمَّن كل شيء: النصوص البرمجية والأنماط والخطوط
   الأربعة صوراً بترميز `data:`. وحارسٌ بعد البناء يفحص أن لا مرجع خارجيّاً
   ولا مسار نسبيّ بقي (`scripts/check-showcase.mjs`).

       VITE_BABEL_DEMO=1 npx vite build --config vite.config.showcase.ts
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import { defineConfig, type Plugin } from "vite";
import react from "@vitejs/plugin-react";
import { viteSingleFile } from "vite-plugin-singlefile";

/* لوحةُ الوصولية تُربَط **ملفّاً** لا صنفاً (`Switchers.tsx`) — وهي القصّة
   نفسها التي ترويها `design/theme/theme-accessible.css`. وفي ملفٍّ واحد لا
   ملفّ يُربَط، فيصير المرجع مساراً نسبياً معلَّقاً يحجبه المُضيف صامتاً.
   فيُحوَّل إلى `data:` هنا: الرابط يبقى رابطاً، ويبقى مُعطَّلاً حتى تُختار. */
function inlineUrlAssets(): Plugin {
  return {
    name: "showcase:inline-url-assets",
    enforce: "pre",
    load(id) {
      if (!id.endsWith(".css?url")) return null;
      const file = id.slice(0, -"?url".length);
      const base64 = readFileSync(file).toString("base64");
      return "export default \"data:text/css;base64," + base64 + "\";";
    },
  };
}

export default defineConfig({
  plugins: [inlineUrlAssets(), react(), viteSingleFile({ removeViteModuleLoader: true })],
  define: { "import.meta.env.VITE_BABEL_DEMO": JSON.stringify("1") },
  build: {
    target: "es2023",
    sourcemap: false,
    outDir: "dist-showcase",
    emptyOutDir: true,
    cssCodeSplit: false,
    /* كل أصلٍ يُضمَّن مهما كبر: الخطوط العربية وحدها ١٫١ ميغابايت، وبقاء
       واحدٍ منها ملفّاً يعني صفحةً بلا خطٍّ عربي على جهاز المالك. */
    assetsInlineLimit: 100 * 1024 * 1024,
    reportCompressedSize: false,
  },
});
