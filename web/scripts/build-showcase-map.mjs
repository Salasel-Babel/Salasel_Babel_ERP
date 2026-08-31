#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   خريطة العمليات لطبقة العرض — مُشتقّة من العقد لا مكتوبة بيد
   ───────────────────────────────────────────────────────────────────────────
   تقرأ contracts/openapi/v1.json وتُخرج، لكل عملية: قالب مسارها، ورمز نجاحها،
   واسم مخطّط جوابها. وطبقة العرض تبني عليها أجوبتها، فلا مسار ولا اسم مخطّط
   مكتوب بيد في ملفّات الثوابت — وأي بابٍ يُضاف إلى العقد يظهر هنا بإعادة
   التوليد لا بتحرير.

       node scripts/build-showcase-map.mjs
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, resolve } from "node:path";

const here = dirname(fileURLToPath(import.meta.url));
const contractPath = resolve(here, "../../contracts/openapi/v1.json");
const outPath = resolve(here, "../src/showcase/operations.ts");

const contract = JSON.parse(readFileSync(contractPath, "utf8"));
const METHODS = ["get", "post", "put", "patch", "delete"];

const rows = [];
for (const [path, item] of Object.entries(contract.paths)) {
  for (const method of METHODS) {
    const op = item[method];
    if (!op) continue;
    const status = Object.keys(op.responses).find((s) => /^2\d\d$/.test(s));
    if (!status) continue;
    const json = op.responses[status].content?.["application/json"];
    const schema = json?.schema?.$ref?.split("/").pop() ?? null;
    rows.push({ key: method.toUpperCase() + " " + path, id: op.operationId, status: Number(status), schema });
  }
}
rows.sort((a, b) => (a.key < b.key ? -1 : a.key > b.key ? 1 : 0));

/* أنواع الحقول البدائية. `runtime-schema` المُولَّد يقول أي حقلٍ مالٌ وأيّه
   مخطّطٌ آخر، ولا يقول أعددٌ هو أم منطقيّ أم نصّ — وطبقة العرض تحتاج ذلك كي
   يخرج «rowCount» عدداً و«balanced» منطقياً، لا نصّاً يُعرض فارغاً. */
const deref = (node) =>
  node?.$ref ? contract.components.schemas[node.$ref.split("/").pop()] : node;
const plain = {};
for (const [name, schema] of Object.entries(contract.components?.schemas ?? {})) {
  const props = schema.properties;
  if (!props) continue;
  const fields = {};
  for (const [field, raw] of Object.entries(props)) {
    const node = deref(raw) ?? raw;
    const type = Array.isArray(node.type) ? node.type.find((t) => t !== "null") : node.type;
    if (type === "integer" || type === "number" || type === "boolean") fields[field] = type;
  }
  if (Object.keys(fields).length > 0) plain[name] = fields;
}

const lines = rows.map(
  (r) =>
    `  ${JSON.stringify(r.key)}: { id: ${JSON.stringify(r.id)}, status: ${r.status}, schema: ${
      r.schema === null ? "null" : JSON.stringify(r.schema)
    } },`
);

writeFileSync(
  outPath,
  `/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد من العقد بـ\`scripts/build-showcase-map.mjs\` — لا تُحرِّره بيدك.
   المصدر · source: contracts/openapi/v1.json (${contract.info?.version ?? "v1"})
   العمليات · operations: ${rows.length}

   وهذا **ليس** من ملفّات العميل المُولَّد: بوّابة الانحراف \`gen:check\`
   لا تعرفه ولا يعرفها، وهو يخصّ طبقة العرض وحدها.
   ═══════════════════════════════════════════════════════════════════════ */

/** عمليةٌ في العقد: رمز نجاحها ومخطّط جوابها. */
export interface Operation {
  /** معرّف العملية كما ينشره العقد. */ readonly id: string;
  /** رمز الحالة عند النجاح. */ readonly status: number;
  /** اسم مخطّط الجواب، أو null حين لا يكون JSON. */ readonly schema: string | null;
}

/** كل عمليات العقد، بمفتاح «الفعل ثمّ قالب المسار». */
export const OPERATIONS: Readonly<Record<string, Operation>> = {
${lines.join("\n")}
};

/**
 * أنواع الحقول غير النصّية في كل مخطّط، كما ينشرها العقد. ما ليس هنا نصٌّ أو
 * مخطّطٌ آخر — و\`runtime-schema\` المُولَّد يقول أيّهما.
 */
export const PLAIN_TYPES: Readonly<Record<string, Readonly<Record<string, "integer" | "number" | "boolean">>>> = {
${Object.entries(plain)
  .sort(([a], [b]) => (a < b ? -1 : 1))
  .map(([name, fields]) => "  " + name + ": " + JSON.stringify(fields) + ",")
  .join("\n")}
};
`,
  "utf8"
);
console.log("عمليات · operations:", rows.length, "→", outPath);
