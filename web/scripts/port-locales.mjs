#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   نقل ملفّات اللغة من design/ إلى TypeScript
   Ports the locale catalogues from design/i18n/locales/*.js to TypeScript
   ───────────────────────────────────────────────────────────────────────────
   المصدر design/ **يُقرأ ولا يُكتب**. هذا السكربت يحمّل ملفّات اللغة في سياق
   شبيه بالمتصفّح — كما يفعل design/audit.js تماماً — ويلتقط ما مُرِّر إلى
   I18N.define بنصّه، فلا تُعاد كتابة ٦٥٠ مفتاحاً بيد ولا تُترجَم مرّة ثانية.

       node scripts/port-locales.mjs            يكتب src/i18n/locales/*.base.ts
       node scripts/port-locales.mjs --check    يقارن ولا يكتب

   وما يتغيّر في النقل شيء واحد فقط، مُعلَن: font.href (رابط خطوط خارجي) يُنقل
   كبيانات ولا يُحقن وقت التشغيل — انظر web/README.md §الخطوط.
   ═══════════════════════════════════════════════════════════════════════════ */
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB = path.resolve(HERE, "..");
const DESIGN = path.resolve(WEB, "..", "design", "i18n");
const OUT = path.join(WEB, "src", "i18n", "locales");
const CHECK = process.argv.includes("--check");

const captured = [];
const sandbox = { console, Intl, Date, JSON, Math, RegExp, String, Number, Object, Array, Error, TypeError };
sandbox.window = sandbox;
sandbox.document = {
  documentElement: {
    style: { setProperty() {}, removeProperty() {} },
    setAttribute() {},
    removeAttribute() {},
    hasAttribute() { return false; },
  },
  createElement() { return { setAttribute() {}, appendChild() {}, style: {} }; },
  head: { appendChild() {} },
  querySelectorAll() { return []; },
  addEventListener() {},
  dispatchEvent() {},
};
sandbox.localStorage = { getItem() { return null; }, setItem() {} };
sandbox.location = { search: "" };
sandbox.navigator = { languages: ["ar"] };
sandbox.CustomEvent = function () {};
vm.createContext(sandbox);

function load(rel) {
  vm.runInContext(fs.readFileSync(path.join(DESIGN, rel), "utf8"), sandbox, { filename: rel });
}
load("i18n.js");
load("locales/manifest.js");

/* اعتراض التعريف: نريد الشجرة الأصلية لا المُسطَّحة — أكياس الجمع تبقى كما كُتبت. */
const define = sandbox.SB.I18N.define;
sandbox.SB.I18N.define = function (code, meta, messages) {
  captured.push({ code, meta, messages });
  return define.call(this, code, meta, messages);
};
const catalogue = sandbox.SB.I18N.catalog;
catalogue.forEach((entry) => load(entry.file));

if (captured.length === 0) {
  console.error("✗ لم يُلتقط أي ملفّ لغة — النقل ضامر. / captured zero locales.");
  process.exit(2);
}

const files = {};
for (const { code, meta, messages } of captured) {
  const keys = countLeaves(messages);
  files[code + ".base.ts"] = [
    "/* منقول آلياً من design/i18n/locales/" + code + ".js — لا تُحرِّره بيدك.",
    "   Ported from design/i18n/locales/" + code + ".js — do not edit by hand.",
    "   أعِد النقل: node scripts/port-locales.mjs   ·   مفاتيح · keys: " + keys,
    "   المصدر design/ للقراءة فقط. / design/ is read-only. */",
    'import type { LocaleMeta, MessageTree } from "../types";',
    "",
    "export const meta: LocaleMeta = " + JSON.stringify(meta, null, 2) + ";",
    "",
    "export const messages: MessageTree = " + JSON.stringify(messages, null, 2) + ";",
    "",
  ].join("\n");
}
files["catalogue.base.ts"] = [
  "/* منقول آلياً من design/i18n/locales/manifest.js — لا تُحرِّره بيدك.",
  "   Ported from design/i18n/locales/manifest.js — do not edit by hand. */",
  'import type { CatalogueEntry } from "../types";',
  "",
  "export const CATALOGUE: readonly CatalogueEntry[] = " +
    JSON.stringify(
      catalogue.map((e) => ({ code: e.code, native: e.native, english: e.english, dir: e.dir })),
      null,
      2
    ) +
    " as const;",
  "",
].join("\n");

function countLeaves(tree) {
  let n = 0;
  const walk = (node) => {
    for (const value of Object.values(node)) {
      if (value && typeof value === "object" && !Array.isArray(value)) {
        if (sandbox.SB.I18N.isPluralBag(value)) n++;
        else walk(value);
      } else n++;
    }
  };
  walk(tree);
  return n;
}

if (CHECK) {
  let bad = 0;
  for (const [name, content] of Object.entries(files)) {
    const target = path.join(OUT, name);
    if (!fs.existsSync(target) || fs.readFileSync(target, "utf8") !== content) {
      console.error("✗ انحراف عن design/ في " + name + " — أعِد: node scripts/port-locales.mjs");
      bad++;
    }
  }
  console.log("لغات مُقارَنة · locales compared: " + captured.length);
  process.exit(bad ? 1 : 0);
}

fs.mkdirSync(OUT, { recursive: true });
for (const [name, content] of Object.entries(files)) {
  fs.writeFileSync(path.join(OUT, name), content, "utf8");
}
console.log(
  "نُقلت " + captured.length + " لغات: " + captured.map((c) => c.code).join(" · ")
);
for (const { code, messages } of captured) {
  console.log("  " + code + ": " + countLeaves(messages) + " مفتاحاً · keys");
}
