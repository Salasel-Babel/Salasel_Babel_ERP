#!/usr/bin/env node
/* ═══════════════════════════════════════════════════════════════════════════
   كتالوج أدوات الوكيل — يُولَّد من العقد المنشور، ولا يُكتب بيد.
   ───────────────────────────────────────────────────────────────────────────
       node tools/agent/build-tool-catalogue.mjs            # يكتب data/agent/tool-catalogue.json
       node tools/agent/build-tool-catalogue.mjs --check    # يقارن ولا يكتب؛ 1 عند الانحراف

   ‏**لماذا مُولَّد:** الأداة التي يراها النموذج هي **العقد** كما نُشر — لا كما
   يتذكّره كاتبٌ. ومخطّطٌ يُكتب بيدٍ ينحرف عن العقد بصمت، فيُنتج النموذج جسماً
   يردّه الخادم ‎400 ولا يفهم لماذا. والانحراف هنا يُحمِّر البناء لا الشاشة.

   ‏**ولماذا بصمة العقد في الترويسة:** نفس تقنية القاعدة 18 — الملفّ المُولَّد
   يحمل ‎sha256 مصدره، وحارسٌ في مجموعة الاختبارات يقارنها بالعقد على القرص.
   فمن غيّر العقد ولم يُعِد التوليد يسقط في بوّابة الخلفية، لا في الإنتاج.

   ‏**وما لا يدخل الكتالوج:** كلّ عملية ليس فعلها الأول «draft». والتصفية
   تمرّ بقائمة الأفعال الممنوعة والمسموحة نفسها المكتوبة في
   ‏`VoiceOperationGuard` — مكرَّرةً هنا بالنصّ لأن ‎node لا يقرأ ‎C#، ومطابقتُها
   مفروضة باختبارٍ يقرأ الملفّين معاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { createHash } from "node:crypto";
import { readFileSync, writeFileSync } from "node:fs";
import { fileURLToPath } from "node:url";
import { dirname, join } from "node:path";

const root = join(dirname(fileURLToPath(import.meta.url)), "..", "..");
const contractPath = join(root, "contracts", "openapi", "v1.json");
const cataloguePath = join(root, "data", "agent", "tool-catalogue.json");

const contractBytes = readFileSync(contractPath);
const contractSha256 = createHash("sha256").update(contractBytes).digest("hex");
const contract = JSON.parse(contractBytes.toString("utf8"));
const schemas = contract.components.schemas;

/* الأفعال الممنوعة — نسخةٌ نصّية من VoiceOperationGuard.ForbiddenVerbs، ومطابقتها مفروضة باختبار. */
const forbiddenVerbs = [
  "post", "activate", "sign", "approve", "terminate", "revoke",
  "reverse", "lapse", "delete", "forfeit", "void",
];

/* المقاطع التي لا تُعكَس — نسخةٌ من NoVoiceIntentReachesAPostingOperation. */
const irreversibleSegments = [
  "/posting", "/activation", "/approval", "/termination",
  "/revocation", "/reversal", "/lapse",
];

/* سجلّات الأسماء — مفردةٌ **مغلقة**، لا تُشتقّ من المصادر المسجَّلة.
   واشتقاقُها من التسجيل كان سيجعل الكتالوج يختلف بين ناشرَين، وهو بعينه
   ما يُبطل ذاكرة البادئة (‏tools تُرسَل في الموضع صفر). */
const registerKeys = [
  "customer", "employee", "inventory_item", "project", "property_unit", "supplier",
];

/* يفكّ $ref مع حارس دورة: بعض المخطّطات تُشير إلى نفسها. */
function resolve(node, seen) {
  if (Array.isArray(node)) return node.map((item) => resolve(item, seen));
  if (node === null || typeof node !== "object") return node;
  if (typeof node.$ref === "string") {
    const name = node.$ref.split("/").pop();
    if (seen.has(name)) throw new Error("مخطّط دوريّ لا يُسطَّح: " + name);
    return resolve(schemas[name], new Set([...seen, name]));
  }
  const out = {};
  for (const key of Object.keys(node)) out[key] = resolve(node[key], seen);
  return out;
}

/* حقلٌ شكلُه معرّف: ينتهي بـ«Id» بحرفٍ كبير — فـ«paid» ليس منها. */
const isIdField = (name) => name.length > 2 && name.endsWith("Id");

const handleDescriptionAr =
  "مِقبضٌ معتِم من lookup_entity أو ask_question. ولا يُكتب هنا معرّفٌ خام ولا اسم — يُرفض قبل التنفيذ.";

/* يعيد كتابة وصف كل حقلٍ شكلُه معرّف، ويجمع مساراتها. */
function rewriteIdFields(schema, path, collected) {
  if (schema === null || typeof schema !== "object") return schema;
  if (schema.type === "array" && schema.items) {
    return { ...schema, items: rewriteIdFields(schema.items, path + "[]." , collected) };
  }
  if (!schema.properties) return schema;
  const properties = {};
  for (const name of Object.keys(schema.properties).sort()) {
    const child = schema.properties[name];
    if (isIdField(name)) {
      collected.push(path + name);
      properties[name] = { ...child, description: handleDescriptionAr };
    } else {
      properties[name] = rewriteIdFields(child, path + name + ".", collected);
    }
  }
  return { ...schema, properties };
}

/* ترتيبٌ مُعجميّ ثابت للمفاتيح في كل عقدة — البايتات هي ما يُذاكَر، لا المعنى. */
function ordered(value) {
  if (Array.isArray(value)) return value.map(ordered);
  if (value === null || typeof value !== "object") return value;
  const out = {};
  for (const key of Object.keys(value).sort()) out[key] = ordered(value[key]);
  return out;
}

/* الأدوات الثلاث التي لا تأتي من العقد: الخطّة والبحث والسؤال. */
const protocolTools = [
  {
    name: "propose_plan",
    operationId: null,
    path: null,
    method: null,
    idFields: [],
    description:
      "يُعلن خطوات الطلب المركَّب قبل تنفيذ أولاها، فتُعرض على المستخدم ويُرى موضعُ كلّ "
      + "خطوة. وهي إعلانٌ لا سلطة: لا تفتح باباً ولا تُنفّذ خطوة، وكلّ خطوةٍ تمرّ "
      + "بالبوّابة نفسها حين يحين دورها.",
    inputSchema: {
      additionalProperties: false,
      properties: {
        steps: {
          description:
            "الخطوات بترتيبها. كلٌّ سطرٌ عربيّ قصير يقول ما سيقع — ولا معرّف فيه ولا رقم.",
          items: { maxLength: 200, type: "string" },
          maxItems: 12,
          minItems: 2,
          type: "array",
        },
      },
      required: ["steps"],
      type: "object",
    },
  },
  {
    name: "lookup_entity",
    operationId: null,
    path: null,
    method: null,
    idFields: [],
    description:
      "يسأل الخادم: هل في سجلّ هذه المنشأة اسمٌ كهذا؟ ويعود بأحد ثلاثة: none · resolved ومعه "
      + "مِقبض · needs_question ومعه معرّف ورقة. ولا يعود باسمٍ ولا بصفٍّ ولا بعددِ مرشّحين.",
    inputSchema: {
      additionalProperties: false,
      properties: {
        kind: {
          description: "أي سجلّ يُسأل. مفردةٌ مغلقة.",
          enum: registerKeys,
          type: "string",
        },
        text: {
          description: "كلام المستخدم نفسه بأسمائه، لا معرّفاً ولا رمزاً.",
          maxLength: 200,
          type: "string",
        },
      },
      required: ["kind", "text"],
      type: "object",
    },
  },
  {
    name: "ask_question",
    operationId: null,
    path: null,
    method: null,
    idFields: [],
    description:
      "يعرض على المستخدم ورقةَ السؤال التي رسمها الخادم من بياناته المحلّية، ويعود بمِقبضٍ "
      + "واحد. ولا تُسهم هذه الأداة بعنوانٍ ولا بخيارٍ ولا باسم حقل — معرّف الورقة وحده.",
    inputSchema: {
      additionalProperties: false,
      properties: {
        questionId: {
          description: "معرّف الورقة كما عاد من lookup_entity، حرفاً بحرف.",
          maxLength: 200,
          type: "string",
        },
      },
      required: ["questionId"],
      type: "object",
    },
  },
];

const draftTools = [];

for (const [routePath, item] of Object.entries(contract.paths)) {
  for (const [method, operation] of Object.entries(item)) {
    if (operation === null || typeof operation !== "object") continue;
    const operationId = operation.operationId;
    if (typeof operationId !== "string") continue;

    const verb = /^[a-z]+/.exec(operationId)?.[0] ?? "";
    if (verb !== "draft") continue;

    if (forbiddenVerbs.includes(verb)) throw new Error("فعلٌ ممنوع مرّ التصفية: " + operationId);
    /* **بالمقاطع لا بالنهاية** — وهي القراءة نفسها في AgentToolCatalogue.IrreversibleSegmentIn.
       كان `endsWith` يُمرّر «…/sales-invoices/posting/confirm» من هنا ومن التركيب معاً. */
    for (const part of routePath.split("/")) {
      if (part && irreversibleSegments.includes("/" + part)) {
        throw new Error("مسارٌ لا يُعكَس مرّ التصفية: " + routePath);
      }
    }

    const body = operation.requestBody?.content?.["application/json"]?.schema;
    if (!body) throw new Error("عمليةُ مسوّدةٍ بلا جسم: " + operationId);

    const idFields = [];
    const schema = rewriteIdFields(resolve(body, new Set()), "", idFields);

    draftTools.push({
      name: operationId,
      operationId,
      path: routePath,
      method,
      idFields: idFields.sort(),
      description: (operation.summary ?? "").split(" / ")[0],
      inputSchema: ordered(schema),
    });
  }
}

const tools = [...protocolTools, ...draftTools].sort((a, b) => (a.name < b.name ? -1 : a.name > b.name ? 1 : 0));

const catalogue = {
  contractSha256,
  generatedFrom: "contracts/openapi/v1.json",
  generator: "tools/agent/build-tool-catalogue.mjs",
  registerKeys,
  tools,
};

const rendered = JSON.stringify(catalogue, null, 2) + "\n";

if (process.argv.includes("--check")) {
  const onDisk = readFileSync(cataloguePath, "utf8");
  if (onDisk !== rendered) {
    console.error("✗ كتالوج الأدوات انحرف عن العقد المنشور. أعِد التوليد:");
    console.error("    node tools/agent/build-tool-catalogue.mjs");
    process.exit(1);
  }
  console.log("✓ كتالوج الأدوات يطابق العقد — " + tools.length + " أداة · " + contractSha256.slice(0, 12));
} else {
  writeFileSync(cataloguePath, rendered, "utf8");
  console.log("✓ كُتب " + tools.length + " أداة إلى data/agent/tool-catalogue.json — " + contractSha256.slice(0, 12));
}
