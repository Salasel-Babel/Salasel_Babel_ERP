#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   العقد المنشور يكبر ولا ينقص — فحصٌ بنيوي لا بالعين.
   ───────────────────────────────────────────────────────────────────────────
       node tools/contract/additive-check.mjs [<مرجع الأساس>]
       node tools/contract/additive-check.mjs origin/develop        (الافتراضي)

   يقارن `contracts/openapi/v1.json` في العمل الحالي بنسخته في مرجع الأساس،
   ويخرج بالرمز 1 إن **اختفى** معرّف عملية أو اسم مخطّط أو مسار.

   ‏**ولماذا سكربت لا مراجعة:** العقد المنشور فيه اليوم أكثر من ‎180 عملية و‎199
   مخطّطاً، وعينٌ بشرية تُقارن قائمتين بهذا الطول تقول «لم يتغيّر شيء» بثقةٍ
   لا تملكها. والاختفاء الصامت لعملية يكسر كل عميل مُولَّد قائم — وهو بالضبط
   صنف العطل الذي دفع هذا المستودع ثمنه في الطرف الآخر من العقد
   (‏traps.md#fakh-a-two-sided-contract-guarded-on-one-side-only).

   ‏**والزيادة مسموحة والنقص ممنوع**: العقد نافذةٌ تُغلق قبل أول نشر
   (‏ADR-0029)، وما بعدها يُضاف ولا يُحذف.
   ═══════════════════════════════════════════════════════════════════════════ */
import { execFileSync } from "node:child_process";
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const ROOT = path.resolve(HERE, "../..");
const CONTRACT = "contracts/openapi/v1.json";
const base = process.argv[2] ?? "origin/develop";

/** يقرأ العقد من مرجع git، أو null إن لم يكن هناك. */
function atRef(ref) {
  try {
    return JSON.parse(
      execFileSync("git", ["show", `${ref}:${CONTRACT}`], { cwd: ROOT, encoding: "utf8", maxBuffer: 64 << 20 })
    );
  } catch {
    return null;
  }
}

/** يستخرج المعرّفات الثلاثة التي يعتمد عليها أي مستهلك. */
function surfaceOf(doc) {
  const paths = Object.keys(doc.paths ?? {}).sort();
  const operations = [];
  for (const [route, item] of Object.entries(doc.paths ?? {})) {
    for (const [method, operation] of Object.entries(item ?? {})) {
      if (operation && typeof operation === "object" && "operationId" in operation) {
        operations.push(`${operation.operationId} ${method.toUpperCase()} ${route}`);
      }
    }
  }
  const schemas = Object.keys(doc.components?.schemas ?? {}).sort();
  return { paths, operations: operations.sort(), schemas };
}

const head = JSON.parse(fs.readFileSync(path.join(ROOT, CONTRACT), "utf8"));
const old = atRef(base);

if (old === null) {
  console.error(`✗ تعذّر قراءة ${CONTRACT} من «${base}». حدّث المراجع: git fetch origin`);
  process.exit(2);
}

const before = surfaceOf(old);
const after = surfaceOf(head);

/* حارس لا فراغ: مقارنةٌ على سطحٍ فارغ تمرّ دائماً وهي لا تفحص شيئاً (فخ-43). */
if (before.operations.length < 50 || before.schemas.length < 50) {
  console.error(
    `✗ سطح الأساس ضامر: ${before.operations.length} عملية · ${before.schemas.length} مخطّط. الفحص لا يعني شيئاً.`
  );
  process.exit(2);
}

const removed = {
  paths: before.paths.filter((x) => !after.paths.includes(x)),
  operations: before.operations.filter((x) => !after.operations.includes(x)),
  schemas: before.schemas.filter((x) => !after.schemas.includes(x)),
};
const added = {
  paths: after.paths.filter((x) => !before.paths.includes(x)),
  operations: after.operations.filter((x) => !before.operations.includes(x)),
  schemas: after.schemas.filter((x) => !before.schemas.includes(x)),
};

const line = (label, b, a) => `  ${label}: ${b} → ${a}`;
console.log(`العقد · contract  (الأساس · base: ${base})`);
console.log(line("مسارات · paths     ", before.paths.length, after.paths.length));
console.log(line("عمليات · operations", before.operations.length, after.operations.length));
console.log(line("مخطّطات · schemas  ", before.schemas.length, after.schemas.length));

for (const [kind, list] of Object.entries(added)) {
  if (list.length) {
    console.log(`  + مُضاف · added ${kind} (${list.length}):`);
    for (const x of list) console.log("      + " + x);
  }
}

let failed = 0;
for (const [kind, list] of Object.entries(removed)) {
  if (list.length) {
    failed += list.length;
    console.error(`  ✗ محذوف · removed ${kind} (${list.length}):`);
    for (const x of list) console.error("      − " + x);
  }
}

if (failed) {
  console.error(`\n✗ العقد نقص في ${failed} موضعاً. والعقد يُضاف إليه ولا يُحذف منه (ADR-0029).`);
  process.exit(1);
}

console.log("\n✔ إضافيّ بالكامل: لا معرّف عملية ولا اسم مخطّط ولا مسار اختفى.");
console.log("✔ fully additive: no operationId, schema name or path disappeared.");
