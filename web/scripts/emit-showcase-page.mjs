#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   إخراج صفحة العرض — قصاصةٌ لا وثيقة، وحارسٌ عليها
   ───────────────────────────────────────────────────────────────────────────
   المُضيف يلفّ الملفّ بـ`<!doctype html><head>…</head><body>` من عنده، فما
   يُكتب هنا **محتوىً مباشر**: عنوانٌ وأنماطٌ ونصوصٌ برمجية، بلا وسوم الوثيقة.

   وحارسٌ يفحص قبل الكتابة، لأن كل مخالفةٍ من هذه تفشل **صامتة** عند المُضيف:
     ١ · لا مرجع خارجيّ ولا مسار نسبيّ — كل شيء `data:` أو مُضمَّن.
     ٢ · لا وسوم وثيقة.
     ٣ · الحجم دون السقف.

       node scripts/emit-showcase-page.mjs [<هدف>]
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync, writeFileSync, mkdirSync } from "node:fs";
import { dirname, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const here = dirname(fileURLToPath(import.meta.url));
const source = resolve(here, "../dist-showcase/index.html");
const target = resolve(process.argv[2] ?? resolve(here, "../../artifacts/babel-demo.html"));
const CEILING = 16 * 1024 * 1024;

const html = readFileSync(source, "utf8");

/* ─────────────────────────────────────────── ١ · القصّ إلى قصاصة ─────── */

const headMatch = /<head>([\s\S]*?)<\/head>/.exec(html);
const bodyMatch = /<body>([\s\S]*?)<\/body>/.exec(html);
if (!headMatch || !bodyMatch) throw new Error("لم يُعثر على <head> و<body> في ناتج البناء.");

/* من الرأس: العنوان والأنماط والنصوص وحدها. وسوم `meta` يكتبها المُضيف. */
const headKept = [...headMatch[1].matchAll(/<(title|style|script)[\s\S]*?<\/\1>/g)].map((m) => m[0]);
const title = headKept.find((tag) => tag.startsWith("<title"));
if (!title) throw new Error("لا <title> في ناتج البناء.");

/* الجذر واللغة يُكتبان قبل أول رسم: الوثيقة التي يلفّها المُضيف لا تحمل
   `lang` ولا `dir`، وطبقة التدويل تكتبهما في أثرٍ بعد أول رسم — والفرق
   ومضةٌ من نصٍّ مقلوب. سطران يمنعانها، ولا يغيّران سلوك التطبيق. */
const preface =
  '<script>document.documentElement.setAttribute("lang","ar");' +
  'document.documentElement.setAttribute("dir","rtl");</script>';

const fragment = [
  "<!-- سلاسل بابل — عرضُ واجهةٍ ببياناتٍ ثابتة. لا خادم، ولا دفتر حقيقي. -->",
  title,
  preface,
  ...headKept.filter((tag) => tag !== title),
  bodyMatch[1].trim(),
  "",
].join("\n");

/* ───────────────────────────────────────────────── ٢ · الحرّاس ───────── */

const problems = [];

for (const tag of ["<!doctype", "<!DOCTYPE", "<html", "</html", "<head", "</head", "<body", "</body"]) {
  if (fragment.includes(tag)) problems.push("وسم وثيقة باقٍ · document tag left: " + tag);
}

/* مرجعٌ خارجيّ أو نسبيّ في سمة. `data:` وحدها تمرّ، و`#` مرساة داخلية. */
for (const match of fragment.matchAll(/\s(?:src|href)=(["'])([^"']*)\1/g)) {
  const value = match[2];
  if (value.startsWith("data:") || value.startsWith("#") || value === "") continue;
  problems.push("مرجع غير مُضمَّن · un-inlined reference: " + value.slice(0, 120));
}
for (const match of fragment.matchAll(/url\(\s*(["']?)(?!data:)([^)"']+)\1\s*\)/g)) {
  problems.push("‏url() غير مُضمَّن · un-inlined url(): " + match[2].slice(0, 120));
}
const bytes = Buffer.byteLength(fragment, "utf8");
if (bytes > CEILING) problems.push("الحجم فوق السقف · above ceiling: " + bytes + " > " + CEILING);

if (problems.length > 0) {
  console.error("✗ صفحة العرض لم تُكتب — " + problems.length + " مخالفة:");
  for (const problem of problems) console.error("    · " + problem);
  process.exit(1);
}

mkdirSync(dirname(target), { recursive: true });
writeFileSync(target, fragment, "utf8");

/* نسخةٌ للفحص وحدها: القصاصة داخل غلافٍ يحاكي ما يلفّها به المُضيف — الترميز
   ونافذة العرض وتصفيرُ الهوامش. لا تُنشَر ولا تُودَع؛ هي ما يفتحه المتصفّح
   في المشي على الشاشات. */
const preview = target.replace(/\.html$/, ".preview.html");
writeFileSync(
  preview,
  '<!doctype html><html><head><meta charset="utf-8">' +
    '<meta name="viewport" content="width=device-width, initial-scale=1">' +
    "<style>:root{color-scheme:light}body{margin:0;font:14px system-ui,sans-serif;background:#faf9f7}" +
    "img{max-width:100%}[hidden]{display:none!important}</style></head><body>\n" +
    fragment +
    "\n</body></html>",
  "utf8"
);
console.log("  نسخة الفحص · preview:", preview);
console.log("✓ صفحة العرض · showcase page:", target);
console.log("  بايتات · bytes:", bytes, "(" + (bytes / 1024 / 1024).toFixed(2) + " ميغابايت)");
console.log("  السقف · ceiling:", CEILING);
