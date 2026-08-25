// إعداد ESLint — قواعد الحدّ لا قواعد الذوق.
import js from "@eslint/js";
import globals from "globals";
import tseslint from "typescript-eslint";
import reactHooks from "eslint-plugin-react-hooks";

export default tseslint.config(
  {
    /* الملفّات المُولَّدة خارج نطاق ESLint عمداً: بوّابتها هي إعادة التوليد
       (npm run gen:check) و tsc، لا مصلِّح أسلوب. و`eslint --fix` عليها كان
       يُحدث انحرافاً عن العقد — التقطه الحارس فعلاً، وهذا سببه. */
    ignores: [
      "dist",
      "node_modules",
      "test-results",
      "playwright-report",
      "coverage",
      "src/api/generated/**",
    ],
  },
  {
    files: ["**/*.{ts,tsx}"],
    extends: [js.configs.recommended, ...tseslint.configs.recommendedTypeChecked],
    languageOptions: {
      ecmaVersion: 2023,
      globals: { ...globals.browser, ...globals.node },
      parserOptions: {
        project: ["./tsconfig.app.json", "./tsconfig.node.json", "./tsconfig.e2e.json"],
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: { "react-hooks": reactHooks },
    rules: {
      ...reactHooks.configs.recommended.rules,

      /* ══ الحدّ المالي ══════════════════════════════════════════════════
         لا Number ولا parseFloat ولا toFixed على شيء. الحساب على المال يقع
         في SQL، والعرض يمرّ بطبقة التدويل. هذه ليست قاعدة ذوق. */
      "no-restricted-globals": [
        "error",
        { name: "parseFloat", message: "لا parseFloat في هذا المشروع: المال نصّ. استعمل Money." },
        { name: "parseInt", message: "لا parseInt على قيمة قادمة من الخادم." },
      ],
      "no-restricted-properties": [
        "error",
        {
          object: "Number",
          property: "parseFloat",
          message: "لا Number.parseFloat: المال نصّ.",
        },
      ],
      /* ‏toLocaleString و Intl.NumberFormat تحقنان محارف تحكّم تحت ar و ur. */
      "no-restricted-syntax": [
        "error",
        {
          selector: "MemberExpression[property.name='toLocaleString']",
          message: "لا toLocaleString: التنسيق من ملفّ اللغة عبر i18n.amount/integer/date.",
        },
        {
          selector: "NewExpression[callee.object.name='Intl'][callee.property.name='NumberFormat']",
          message: "لا Intl.NumberFormat للعرض: تحقن محارف تحكّم تحت ar و ur.",
        },
        {
          selector: "NewExpression[callee.object.name='Intl'][callee.property.name='DateTimeFormat'][arguments.0.value!='en-u-ca-islamic-umalqura-nu-latn']",
          message: "لا Intl.DateTimeFormat للعرض: التاريخ من ملفّ اللغة.",
        },
      ],

      "@typescript-eslint/no-unused-vars": ["error", { argsIgnorePattern: "^_" }],
      "@typescript-eslint/no-explicit-any": "error",
      "@typescript-eslint/no-unsafe-assignment": "off",
      "@typescript-eslint/no-unsafe-member-access": "off",
      "@typescript-eslint/no-unsafe-call": "off",
      "@typescript-eslint/no-unsafe-return": "off",
      "@typescript-eslint/no-unsafe-argument": "off",
      "@typescript-eslint/restrict-template-expressions": "off",
      "@typescript-eslint/no-misused-promises": ["error", { checksVoidReturn: false }],
    },
  },
  {
    /* طبقة التدويل هي الموضع الوحيد المسموح فيه بـIntl — وهي تعرف لماذا. */
    files: ["src/i18n/engine.ts"],
    rules: { "no-restricted-syntax": "off" },
  },
  {
    files: ["e2e/**", "tests/**"],
    rules: {
      "no-restricted-syntax": "off",
      "@typescript-eslint/no-unsafe-assignment": "off",
    },
  }
);
