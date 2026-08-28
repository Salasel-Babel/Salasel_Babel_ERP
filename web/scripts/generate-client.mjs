#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   مولّد عميل الواجهة من العقد المنشور
   Generates the TypeScript client from contracts/openapi/v1.json

       node scripts/generate-client.mjs           يكتب الملفات
       node scripts/generate-client.mjs --check    يقارن ولا يكتب (بوابة الانحراف)

   ───────────────────────────────────────────────────────────────────────────
   لماذا مولّد مكتوب هنا بدل أداة جاهزة؟ لأن الحدّ الحاكم في هذا المنتج ليس
   «أنشئ أنواعاً» بل «المال نصّ ولا يصير Number أبداً». كل مولّد جاهز جرّبناه
   يخرج `debit: string` — وهو صحيح شكلاً وعديم الأثر: لا شيء يمنع
   `Number(row.debit)`. المولّد هنا يعرف الصيغ المسمّاة في العقد (Money و
   Int64String) ويُخرج لها **نوعاً محتجزاً** مع فاكّ تشفير يلفّها وقت التشغيل،
   فالحماية بنيوية لا توثيقية.

   وكل مخرَج هنا حتمي: ترتيب مستقرّ، نهايات أسطر LF، وبصمة SHA-256 للعقد
   مكتوبة في الرأس. تغيّر العقد ⇒ تغيّر البصمة ⇒ يفشل --check بصوت عالٍ.
   ═══════════════════════════════════════════════════════════════════════════ */
"use strict";
import fs from "node:fs";
import path from "node:path";
import crypto from "node:crypto";
import { fileURLToPath } from "node:url";

const HERE = path.dirname(fileURLToPath(import.meta.url));
const WEB = path.resolve(HERE, "..");
const REPO = path.resolve(WEB, "..");
const CONTRACT = path.join(REPO, "contracts", "openapi", "v1.json");
const OUT = path.join(WEB, "src", "api", "generated");
const CHECK = process.argv.includes("--check");

/* ── قراءة العقد ببايتاته: البصمة تُحسب على البايتات لا على التمثيل المُحلَّل ── */
const bytes = fs.readFileSync(CONTRACT);
const sha256 = crypto.createHash("sha256").update(bytes).digest("hex");
const doc = JSON.parse(bytes.toString("utf8"));

/* الصيغ المسمّاة: مخطّطات نصّية على المستوى الأعلى تُلَفّ بنوع محتجز.
   ليست قائمة مكتوبة بيد — تُشتقّ من العقد: كل مخطّط نصّي بنمط ويُشار إليه
   بـ$ref من حقل. ونُثبِّت المعالجة لاسمين نعرف سلوكهما وقت التشغيل. */
const schemas = doc.components.schemas;
const schemaNames = Object.keys(schemas).sort();

/* المخطّطات النصّية ذات النمط: كلّها **محتجزة بالنوع**، ولا تُمرَّر مكان نصّ عادي.
   والقائمة مُشتقّة من العقد لا مكتوبة بيد — مخطّط نصّي جديد بنمط يُحتجَز وحده.
   و Money وحده يأخذ صنفاً كامل الحراسة وقت التشغيل، لأنه الوحيد الذي يُعرَض
   ويُفرَز ويصل إلى يد المحاسب؛ وبقيّة الصيغ تعبر مروراً. */
const BRANDED = {};
for (const name of schemaNames) {
  const s = schemas[name];
  if (s.type === "string" && s.pattern) {
    BRANDED[name] = name === "Money"
      ? { kind: "money", ts: "Money", from: "../money" }
      : { kind: "brand", ts: name, from: "./brands" };
  }
}
const BRAND_NAMES = Object.keys(BRANDED).filter((n) => BRANDED[n].kind === "brand").sort();

/* ─────────────────────────────────────────────────────── أدوات نصّية ── */
const NL = "\n";
function banner(what) {
  return [
    "/* ═══════════════════════════════════════════════════════════════════════",
    "   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.",
    "   GENERATED FILE — DO NOT EDIT BY HAND.",
    "",
    "   المصدر · source:  contracts/openapi/v1.json",
    "   بصمة المصدر · source sha256:",
    "     " + sha256,
    "   المولّد · generator: web/scripts/generate-client.mjs",
    "",
    "   لإعادة التوليد:  npm run gen",
    "   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)",
    "",
    "   " + what,
    "   ═══════════════════════════════════════════════════════════════════════ */",
    "",
  ].join(NL);
}
function q(s) {
  return JSON.stringify(s);
}
/* تعليق TSDoc من وصف العقد — الوصف العربي/الإنجليزي يصل إلى محرّر الواجهة. */
/* محارف التحكّم غير المرئية تُنزَع من كل نصّ منقول من العقد.
   العقد يحمل ثلاثة منها في أوصافه، وهي غير مؤذية هناك؛ لكنّ قاعدة هذا
   المجلد أن **لا محرف غير مرئي في أي ملف مصدر** — لأن محرفاً واحداً يتسلّل
   إلى نصّ ثم إلى حقل ثم إلى بصمة تجزئة (design/README §٣٫٧). */
const INVISIBLE = /[\u200B-\u200F\u061C\u202A-\u202E\u2066-\u2069\uFEFF]/g;
function visible(text) {
  return String(text).replace(INVISIBLE, "");
}

function docComment(text, indent) {
  if (!text) return "";
  const pad = " ".repeat(indent);
  const lines = visible(text).split("\n").map((l) => l.trimEnd());
  if (lines.length === 1) return pad + "/** " + lines[0] + " */" + NL;
  return (
    pad + "/**" + NL + lines.map((l) => pad + " * " + l).join(NL) + NL + pad + " */" + NL
  );
}

/* ───────────────────────────────────────────── تحويل مخطّط إلى نوع TS ── */
function refName(ref) {
  return ref.replace("#/components/schemas/", "");
}
function tsType(schema) {
  if (schema.$ref) {
    const name = refName(schema.$ref);
    return BRANDED[name] ? BRANDED[name].ts : name;
  }
  if (schema.oneOf) {
    return schema.oneOf.map(tsType).join(" | ");
  }
  const t = schema.type;
  if (Array.isArray(t)) {
    return t.map((one) => tsType({ ...schema, type: one })).join(" | ");
  }
  switch (t) {
    case "null":
      return "null";
    case "boolean":
      return "boolean";
    case "integer":
    case "number":
      return "number";
    case "array":
      return arrayType(schema.items);
    case "object":
      return "Record<string, unknown>";
    case "string":
      if (schema.enum) return schema.enum.map(q).join(" | ");
      return "string";
    default:
      return "unknown";
  }
}
function arrayType(items) {
  const inner = tsType(items);
  return /[ |]/.test(inner) ? "(" + inner + ")[]" : inner + "[]";
}

function emitTypes() {
  const out = [banner("أنواع العقد — مخطّطاً واحداً لكل مخطّط في components.schemas.")];
  out.push('import type { Money } from "../money";');
  if (BRAND_NAMES.length) {
    out.push('import type { ' + BRAND_NAMES.join(", ") + ' } from "./brands";');
  }
  out.push("");
  out.push(
    "/* المال يصل هنا **مغلّفاً**: Money كائن يرمي عند أي تحويل ضمني إلى نصّ أو رقم.",
    "   وبقيّة الصيغ النصّية المنشورة أنواع محتجزة (" + BRAND_NAMES.join(" · ") + ").",
    "   ولا حقل مالي واحد نوعه number — لا هنا ولا في أي ملف مكتوب بيد.",
    "   Money is an object whose implicit coercions throw; the other published string",
    "   formats are branded types. No monetary field is ever typed `number`. */",
    ""
  );
  for (const name of schemaNames) {
    if (BRANDED[name]) {
      out.push(
        docComment(schemas[name].description, 0) +
          "/* " + name + " مُعرَّف في ../money كنوع محتجز وقت التشغيل. */"
      );
      out.push("");
      continue;
    }
    const s = schemas[name];
    out.push(docComment(s.description, 0).trimEnd());
    if (s.type === "object" && s.properties) {
      const required = new Set(s.required || []);
      out.push("export interface " + name + " {");
      for (const key of Object.keys(s.properties).sort()) {
        const p = s.properties[key];
        const c = docComment(p.description, 2);
        if (c) out.push(c.trimEnd());
        out.push("  " + key + (required.has(key) ? "" : "?") + ": " + tsType(p) + ";");
      }
      out.push("}");
    } else {
      out.push("export type " + name + " = " + tsType(s) + ";");
    }
    out.push("");
  }
  return out.join(NL).replace(/\n{3,}/g, "\n\n");
}

/* ──────────────────────── واصفات وقت التشغيل: أين يقع المال في كل مخطّط ──
   العقد يعرف مواضع المال؛ فاكّ التشفير لا يخمّنها ولا تُكتب بيد.            */
function fieldDescriptor(schema) {
  if (schema.$ref) {
    const name = refName(schema.$ref);
    if (BRANDED[name]) {
      return BRANDED[name].kind === "money" ? { k: "money" } : { k: "brand", b: name };
    }
    return { k: "ref", r: name };
  }
  if (schema.oneOf) {
    const nonNull = schema.oneOf.filter((o) => o.type !== "null");
    const nullable = schema.oneOf.length !== nonNull.length;
    if (nonNull.length === 1) {
      const inner = fieldDescriptor(nonNull[0]);
      return nullable ? { ...inner, n: true } : inner;
    }
    return { k: "plain" };
  }
  const t = Array.isArray(schema.type) ? schema.type.filter((x) => x !== "null") : [schema.type];
  const nullable = Array.isArray(schema.type) && schema.type.includes("null");
  if (t[0] === "array") {
    const inner = fieldDescriptor(schema.items);
    return nullable ? { k: "array", i: inner, n: true } : { k: "array", i: inner };
  }
  /* المجموعة المغلقة تعبر إلى وقت التشغيل: شاشةٌ تعرض قائمة أدوار مكتوبة بيد
     تنحرف عن العقد عند أول إضافة، فتُرسل دوراً لا يعرفه الخادم أو تُسقط دوراً
     يعرفه. والأعضاء هنا **تُعرَض ولا تُفرَض**: فاكّ الترميز لا يرفض عضواً لا
     يعرفه، لأن إضافة عضو من الخادم إلى العميل تبقى في v1 بنصّ سياسة الإصدار —
     وعميلٌ يرفضها ينكسر على تغيير مسموح. */
  if (t[0] === "string" && Array.isArray(schema.enum)) {
    return nullable ? { k: "plain", e: schema.enum, n: true } : { k: "plain", e: schema.enum };
  }
  return nullable ? { k: "plain", n: true } : { k: "plain" };
}
function stable(value) {
  if (Array.isArray(value)) return "[" + value.map(stable).join(",") + "]";
  if (value && typeof value === "object") {
    return (
      "{" +
      Object.keys(value)
        .sort()
        .map((k) => q(k) + ":" + stable(value[k]))
        .join(",") +
      "}"
    );
  }
  return JSON.stringify(value);
}

function emitRuntimeSchema() {
  const out = [
    banner(
      "واصفات وقت التشغيل: لكل مخطّط، أي حقوله مال وأيها أعداد طويلة وأيها مخطّط آخر.\n   فاكّ التشفير يمشي عليها فيلفّ المال — ولا موضع مال واحد مكتوب بيد."
    ),
  ];
  out.push('export type FieldKind = "plain" | "money" | "brand" | "ref" | "array";');
  out.push("");
  out.push("export interface FieldShape {");
  out.push("  /** النوع · kind */ k: FieldKind;");
  out.push("  /** اسم المخطّط عند k===\"ref\" · referenced schema */ r?: string;");
  out.push("  /** اسم الصيغة المحتجزة عند k===\"brand\" · branded format */ b?: string;");
  out.push("  /** شكل العنصر عند k===\"array\" · item shape */ i?: FieldShape;");
  out.push("  /** أعضاء المجموعة المغلقة حين يكون الحقل تعداداً · closed-set members */ e?: readonly string[];");
  out.push("  /** يقبل null · nullable */ n?: boolean;");
  out.push("}");
  out.push("");
  out.push("export interface SchemaShape {");
  out.push("  /** الحقول الإلزامية · required properties */ required: readonly string[];");
  out.push("  /** شكل كل حقل معروف · shape of each known property */ fields: Readonly<Record<string, FieldShape>>;");
  out.push("}");
  out.push("");
  out.push("export const SCHEMAS: Readonly<Record<string, SchemaShape>> = {");
  for (const name of schemaNames) {
    if (BRANDED[name]) continue;
    const s = schemas[name];
    if (!(s.type === "object" && s.properties)) continue;
    const fields = {};
    for (const key of Object.keys(s.properties).sort()) {
      fields[key] = fieldDescriptor(s.properties[key]);
    }
    out.push(
      "  " +
        name +
        ": { required: " +
        stable((s.required || []).slice().sort()) +
        ", fields: " +
        stable(fields) +
        " },"
    );
  }
  out.push("};");
  out.push("");
  out.push("/** أسماء المخطّطات كما وردت في العقد. / Schema names as published. */");
  out.push("export const SCHEMA_NAMES = " + stable(schemaNames) + " as const;");
  out.push("");
  return out.join(NL);
}

/* ─────────────────────────────────────────────── الصيغ النصّية المنشورة ── */
function emitFormats() {
  const out = [
    banner(
      "الأنماط المنشورة، منقولةً حرفياً من العقد. المدقّقات تستعملها ولا تُعيد كتابتها."
    ),
  ];
  const patterns = {};
  for (const name of schemaNames) {
    const s = schemas[name];
    if (s.type === "string" && s.pattern) patterns["SCHEMA_" + name] = s.pattern;
  }
  /* أنماط المعاملات أيضاً — رمز الفترة مثلاً يُتحقَّق منه قبل مغادرة المتصفّح. */
  for (const [route, item] of Object.entries(doc.paths).sort()) {
    for (const p of item.parameters || []) {
      if (p.schema && p.schema.pattern) {
        patterns["PARAM_" + operationKeyOf(route) + "_" + p.name] = p.schema.pattern;
      }
    }
    for (const [method, op] of Object.entries(item)) {
      if (method === "parameters") continue;
      for (const p of op.parameters || []) {
        if (p.schema && p.schema.pattern) {
          patterns["PARAM_" + op.operationId + "_" + p.name] = p.schema.pattern;
        }
      }
    }
  }
  for (const key of Object.keys(patterns).sort()) {
    out.push("export const " + key + " = " + q(patterns[key]) + ";");
    out.push("export const " + key + "_RE = new RegExp(" + q(patterns[key]) + ");");
  }
  out.push("");
  return out.join(NL);
}
function operationKeyOf(route) {
  return route
    .replace(/[^A-Za-z0-9]+/g, "_")
    .replace(/^_+|_+$/g, "")
    .toUpperCase();
}

/* ───────────────────────────────────────── الصيغ النصّية المحتجزة ────── */
function emitBrands() {
  const out = [
    banner(
      "الصيغ النصّية المنشورة، محتجزةً بالنوع ومعها مدقّقاتها.\n   نصٌّ لا يطابق النمط لا يصير قيمةً من هذه الأنواع أبداً."
    ),
  ];
  out.push('import * as F from "./formats";');
  out.push("");
  for (const name of BRAND_NAMES) {
    const s = schemas[name];
    out.push(docComment(s.description, 0).trimEnd());
    out.push(
      "export type " + name + " = string & { readonly __" + name + ": unique symbol };"
    );
    out.push("");
    out.push("/**");
    out.push(" * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.");
    out.push(" * @param text النصّ كما ورد. / the text as received.");
    out.push(" */");
    out.push("export function as" + name + "(text: unknown): " + name + " {");
    out.push('  if (typeof text !== "string" || !F.SCHEMA_' + name + "_RE.test(text)) {");
    out.push("    throw new TypeError(");
    out.push(
      '      "as' + name + ': نصّ لا يطابق النمط المنشور " + F.SCHEMA_' + name + " + \" — «\" + String(text) + \"». / does not match the published pattern.\""
    );
    out.push("    );");
    out.push("  }");
    out.push("  return text as " + name + ";");
    out.push("}");
    out.push("");
  }
  out.push("/** مدقّق لكل صيغة، مفهرساً باسمها في العقد. / A validator per published format. */");
  out.push("export const BRANDS: Readonly<Record<string, (text: unknown) => string>> = {");
  for (const name of BRAND_NAMES) out.push("  " + name + ": as" + name + ",");
  out.push("};");
  out.push("");
  return out.join(NL);
}

/* ───────────────────────────────────────────────────── بطاقة هوية العقد ── */
function emitContract() {
  const operations = [];
  for (const [route, item] of Object.entries(doc.paths).sort()) {
    for (const [method, op] of Object.entries(item)) {
      if (method === "parameters") continue;
      operations.push({ id: op.operationId, method: method.toUpperCase(), path: route });
    }
  }
  operations.sort((a, b) => (a.id < b.id ? -1 : a.id > b.id ? 1 : 0));
  const out = [banner("هوية العقد الذي وُلِّد منه هذا العميل.")];
  out.push("export const CONTRACT = {");
  out.push("  title: " + q(visible(doc.info.title)) + ",");
  out.push("  version: " + q(doc.info.version) + ",");
  out.push("  openapi: " + q(doc.openapi) + ",");
  out.push("  /** بصمة بايتات contracts/openapi/v1.json وقت التوليد. */");
  out.push("  sourceSha256: " + q(sha256) + ",");
  out.push("  operationCount: " + operations.length + ",");
  out.push("  schemaCount: " + schemaNames.length + ",");
  out.push("  operations: " + stable(operations) + ",");
  out.push("} as const;");
  out.push("");
  return out.join(NL);
}

/* ─────────────────────────────────────────────────────────────── العميل ── */
function paramType(p) {
  return tsType(p.schema || { type: "string" });
}
function emitClient() {
  const out = [
    banner(
      "العميل: دالّة واحدة لكل عملية في العقد. لا مسار مكتوب بيد، ولا اسم حقل\n   مكتوب بيد، ولا رمز حالة مكتوب بيد."
    ),
  ];
  out.push('import type * as T from "./types";');
  out.push('import { SCHEMAS } from "./runtime-schema";');
  out.push('import { decodeSchema, encodeSchema, type Transport, ProblemError } from "../transport";');
  out.push("");
  out.push('export type { Transport } from "../transport";');
  out.push("");

  const ops = [];
  for (const [route, item] of Object.entries(doc.paths).sort()) {
    const shared = item.parameters || [];
    for (const [method, op] of Object.entries(item)) {
      if (method === "parameters") continue;
      ops.push({ route, method, op, params: [...shared, ...(op.parameters || [])] });
    }
  }
  ops.sort((a, b) => (a.op.operationId < b.op.operationId ? -1 : 1));

  for (const { route, method, op, params } of ops) {
    const pathParams = params.filter((p) => p.in === "path");
    const queryParams = params.filter((p) => p.in === "query");
    const bodyRef =
      op.requestBody &&
      op.requestBody.content &&
      op.requestBody.content["application/json"] &&
      op.requestBody.content["application/json"].schema;
    /* حمولة متعدّدة الأجزاء: بابا إيداع المرفق وتصحيحه يرفعان **بايتات ملفّ**، لا
       جسم JSON — لأن جسم JSON يعني base64 يعني انتفاخ الثلث وصورةً كاملة في سجلّ
       الطلب. ولا مخطّط مسمّى لها في العقد عمداً: مخطّطٌ باسمٍ كان سيُولِّد هنا
       واجهةً حقلُها `content: string` وهي كذبة عن حقلٍ بايتاته ملفّ. فالوسيط
       `FormData` نفسه، ويمرّ من النقل بلا JSON.stringify وبلا ترويسة نوع محتوى —
       المتصفّح وحده يكتب الحدّ الفاصل. */
    const multipart = Boolean(
      op.requestBody && op.requestBody.content && op.requestBody.content["multipart/form-data"]
    );
    const okStatuses = Object.keys(op.responses)
      .filter((s) => /^2/.test(s))
      .sort();
    const okSchema =
      op.responses[okStatuses[0]] &&
      op.responses[okStatuses[0]].content &&
      op.responses[okStatuses[0]].content["application/json"] &&
      op.responses[okStatuses[0]].content["application/json"].schema;
    /* استجابةٌ بايتات لا JSON: باب تنزيل المرفق. تُعاد Blob، ولا تمرّ على فاكّ
       الترميز لأن لا مخطّط يُفكّ به — وإرجاع void كان سيجعل دالّةً تُنزّل ملفّاً
       ولا تسلّمه لأحد. */
    const binary = Boolean(
      op.responses[okStatuses[0]] &&
      op.responses[okStatuses[0]].content &&
      op.responses[okStatuses[0]].content["application/octet-stream"]
    );
    /* استجابةُ JSON قد تكون **مخطّطاً مسمّى** وقد تكون شكلاً مضمَّناً بلا $ref.
       والحالة الثانية ليست إغفالاً في العقد: باب /openapi/v1.json يخدم وثيقة OpenAPI
       نفسها، وليس لها نموذج مجال في هذه الشيفرة يُسمّى — واختراع اسم لها كان سيضع في
       العقد مخطّطاً لا يقابله نوع. فتُعطى `unknown`: نوعٌ يُجبر القارئ على التحقّق قبل
       الاستعمال، ولا يُمرَّر على فاكّ التشفير لأن لا مخطّط يُفكّ به. وقبل هذا الباب لم
       يكن في العقد استجابةُ JSON بلا $ref قطّ، فانكسر المولّد عليها بـ
       `Cannot read properties of undefined` — وهو انكسارٌ بصوت عالٍ، وهو الصحيح. */
    const okRef = okSchema && okSchema.$ref ? okSchema : null;
    const resultName = okRef ? refName(okRef.$ref) : null;
    const resultTs = okRef ? "T." + resultName : okSchema ? "unknown" : binary ? "Blob" : "void";

    /* نوع الوسائط */
    const argLines = [];
    for (const p of pathParams.sort((a, b) => (a.name < b.name ? -1 : 1))) {
      argLines.push(docComment(p.description, 2).trimEnd());
      argLines.push("  " + p.name + ": " + paramType(p) + ";");
    }
    for (const p of queryParams.sort((a, b) => (a.name < b.name ? -1 : 1))) {
      argLines.push(docComment(p.description, 2).trimEnd());
      argLines.push("  " + p.name + (p.required ? "" : "?") + ": " + paramType(p) + ";");
    }
    if (bodyRef) {
      argLines.push("  /** جسم الطلب. / The request body. */");
      argLines.push("  body: T." + refName(bodyRef.$ref) + ";");
    } else if (multipart) {
      argLines.push(
        "  /** حمولة multipart: جزءٌ اسمه content يحمل البايتات. / The multipart payload: a part named content carries the bytes. */"
      );
      argLines.push("  body: FormData;");
    }
    const argsType = op.operationId[0].toUpperCase() + op.operationId.slice(1) + "Args";
    if (argLines.length) {
      out.push("export interface " + argsType + " {");
      out.push(...argLines.filter(Boolean));
      out.push("}");
      out.push("");
    }

    out.push(docComment((op.summary || "") + (op.description ? "\n\n" + op.description : ""), 0).trimEnd());
    const argDecl = argLines.length ? "args: " + argsType + ", " : "";
    out.push(
      "export async function " +
        op.operationId +
        "(transport: Transport, " +
        argDecl +
        "signal?: AbortSignal): Promise<" +
        resultTs +
        "> {"
    );
    /* المسار */
    let expr = q(route);
    for (const p of pathParams) {
      expr =
        expr.replace(
          "{" + p.name + "}",
          '" + encodeURIComponent(args.' + p.name + ') + "'
        );
    }
    out.push("  const path = " + expr + ";");
    if (queryParams.length) {
      out.push("  const query = new URLSearchParams();");
      for (const p of queryParams.sort((a, b) => (a.name < b.name ? -1 : 1))) {
        if (p.required) {
          out.push("  query.set(" + q(p.name) + ", args." + p.name + ");");
        } else {
          out.push(
            "  if (args." + p.name + " !== undefined && args." + p.name + " !== null) query.set(" +
              q(p.name) +
              ", args." +
              p.name +
              ");"
          );
        }
      }
      out.push('  const url = query.size > 0 ? path + "?" + query.toString() : path;');
    } else {
      out.push("  const url = path;");
    }
    if (bodyRef) {
      out.push(
        "  const body = encodeSchema(SCHEMAS, " + q(refName(bodyRef.$ref)) + ", args.body as unknown);"
      );
    } else if (multipart) {
      out.push("  const body = args.body;");
    }
    out.push(
      "  const response = await transport({ method: " +
        q(method.toUpperCase()) +
        ", url, " +
        (bodyRef || multipart ? "body, " : "") +
        (binary ? "binary: true, " : "") +
        "signal });"
    );
    out.push("  if (!response.ok) throw ProblemError.from(response);");
    if (okRef) {
      out.push(
        "  return decodeSchema(SCHEMAS, " +
          q(resultName) +
          ", response.json) as " +
          resultTs +
          ";"
      );
    } else if (okSchema) {
      out.push("  return response.json as unknown;");
    } else if (binary) {
      out.push("  if (!response.bytes) {");
      out.push(
        '    throw new TypeError("' +
          "استجابة ناجحة بلا بايتات · a successful response carried no bytes: " +
          '" + url);'
      );
      out.push("  }");
      out.push("  return response.bytes;");
    }
    out.push("}");
    out.push("");
  }
  return out.join(NL).replace(/\n{3,}/g, "\n\n");
}

/* ──────────────────────────────────────────────────────────── الكتابة ── */
const files = {
  "brands.ts": emitBrands(),
  "contract.ts": emitContract(),
  "types.ts": emitTypes(),
  "runtime-schema.ts": emitRuntimeSchema(),
  "formats.ts": emitFormats(),
  "client.ts": emitClient(),
};

if (CHECK) {
  const problems = [];
  const onDisk = fs.existsSync(OUT) ? fs.readdirSync(OUT).sort() : [];
  const expected = Object.keys(files).sort();
  for (const extra of onDisk.filter((f) => !expected.includes(f))) {
    problems.push("ملف زائد في المجلد المُولَّد · unexpected file: " + extra);
  }
  for (const name of expected) {
    const target = path.join(OUT, name);
    if (!fs.existsSync(target)) {
      problems.push("ملف مفقود · missing: " + name);
      continue;
    }
    const actual = fs.readFileSync(target, "utf8");
    if (actual !== files[name]) {
      const a = actual.split("\n");
      const b = files[name].split("\n");
      let line = 0;
      while (line < Math.max(a.length, b.length) && a[line] === b[line]) line++;
      problems.push(
        "انحراف · drift in " +
          name +
          " عند السطر " +
          (line + 1) +
          "\n      على القرص · on disk:  " +
          JSON.stringify(a[line] ?? "<نهاية الملف>") +
          "\n      من العقد  · from contract: " +
          JSON.stringify(b[line] ?? "<نهاية الملف>")
      );
    }
  }
  /* حارس اللافراغ: فحصٌ لم يقارن شيئاً يمرّ دائماً. */
  if (expected.length === 0 || schemaNames.length === 0) {
    console.error("✗ المولّد قرأ صفر مخطّطات — الفحص ضامر ولا يفحص شيئاً.");
    process.exit(2);
  }
  console.log(
    "عقد · contract: " +
      doc.info.version +
      "  ·  مخطّطات · schemas: " +
      schemaNames.length +
      "  ·  ملفات مُقارَنة · files compared: " +
      expected.length +
      "  ·  بصمة · sha256: " +
      sha256.slice(0, 16) +
      "…"
  );
  if (problems.length) {
    console.error("");
    console.error("✗ الملفات المُولَّدة تخالف العقد. أعِد التوليد: npm run gen");
    console.error("✗ Generated files diverge from the contract. Regenerate: npm run gen");
    console.error("");
    for (const p of problems) console.error("   " + p);
    process.exit(1);
  }
  console.log("✓ لا انحراف: إعادة التوليد لا تنتج أي اختلاف.");
  console.log("✓ no drift: regenerating produces no diff.");
  process.exit(0);
}

fs.mkdirSync(OUT, { recursive: true });
for (const [name, content] of Object.entries(files)) {
  fs.writeFileSync(path.join(OUT, name), content, "utf8");
}
console.log(
  "وُلِّد · generated: " +
    Object.keys(files).length +
    " ملفات · files  |  " +
    schemaNames.length +
    " مخطّطاً · schemas  |  " +
    doc.info.version
);
console.log("  " + Object.keys(files).sort().join(", "));
console.log("  sha256(contract) = " + sha256);
