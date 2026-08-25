import { defineConfig } from "vitest/config";
import react from "@vitejs/plugin-react";

export default defineConfig({
  plugins: [react()],
  test: {
    environment: "jsdom",
    include: ["tests/**/*.test.ts", "tests/**/*.test.tsx"],
    globals: false,
    /* حارس اللافراغ على المجموعة نفسها: تشغيلٌ لا يجمع اختباراً واحداً يفشل. */
    passWithNoTests: false,
  },
});
