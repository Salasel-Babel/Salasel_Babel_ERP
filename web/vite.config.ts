import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";

/* الخلفية تُخاطَب عبر وسيط في التطوير، فالواجهة لا تعرف منفذها ولا تُبنى عليه.
   BABEL_API غيّرها إن أقلعت الخلفية على منفذ آخر. */
const api = process.env.BABEL_API ?? "http://127.0.0.1:5080";

export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      "/api": { target: api, changeOrigin: true },
      "/health": { target: api, changeOrigin: true },
    },
  },
  build: { target: "es2023", sourcemap: true },
});
