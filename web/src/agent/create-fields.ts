/* ═══════════════════════════════════════════════════════════════════════════
   ورقةُ الإنشاء تُشتقّ من العقد المنشور — لا تُكتب بيد ولا يتخيّلها النموذج
   The create sheet is derived from the published contract
   ───────────────────────────────────────────────────────────────────────────
   حين يختار الإنسان «جديد» تُفتح ورقةٌ ثانية تسأل عن **ما يطلبه العقد لهذه
   العملية بالضبط** — لا أكثر ولا أقلّ. وقائمةٌ مكتوبةٌ بيدٍ هنا تنحرف عن العقد
   في أوّل إيداعٍ يغيّره، فيملأ الإنسان نموذجاً يرفضه الخادم بعد أن كتبه كلّه.

   ولذلك كلُّ ما هنا مقروءٌ من `src/api/generated/` — وهي مُولَّدةٌ من
   `contracts/openapi/v1.json` وتحمل بصمته، و`npm run gen:check` يُحمِّر البوّابة
   عند اختلاف بايتٍ واحد. **فما يدرك الانحرافَ بوّابةٌ لا شاشة.**

   **والمكتوب بيدٍ هنا جدولٌ واحد**: أيُّ عمليةٍ منشورة تُنشئ أيَّ نوع. وهو حكمُ
   منتَجٍ لا واقعُ عقد، فيحرسه اختبارٌ يقرأ `contracts/openapi/v1.json` نفسه
   ويطابق المعرّف والمخطّط — تقنية القاعدة ١٨.

   **وما لا يُملأ من العقد يُرفض باسمه**، ولا تُرسَم له ورقةٌ ناقصة:
     · فعلٌ لا يبلغه الوكيل  (`registerEmployee` فعلُه `register`، وليس في
       `VoiceOperationGuard.PermittedVerbs`)؛
     · مسارٌ يسمّي أباً  (`createUnit` تحت `{propertyId}`، والورقة لا تختار عقاراً)؛
     · حقلٌ إلزاميٌّ قائمة  (`ItemRequest.units` و`ProjectRequest.nameTranslations`)
       — وورقةُ سؤالٍ لا تبني قوائم.
   فيبقى ما يبقى: العميل والمورّد. وهما ما ينصّ عليه العقد، **مُستنتَجَين لا
   مكتوبين**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { CONTRACT } from "../api/generated/contract";
import * as FORMATS from "../api/generated/formats";
import { SCHEMAS, type FieldShape } from "../api/generated/runtime-schema";
import type { AgentEntityKind } from "./sheet";

/* ═══════════════════════════════ ١ · مرآة حارس الأفعال في الخادم */

/**
 * **مرآة `VoiceOperationGuard.PermittedVerbs`** — والانحراف يُحمِّر اختباراً
 * يقرأ ملفّ الخادم نفسه، لا شاشةً وقت التشغيل. مرتَّبة ترتيباً معجمياً كي
 * تكون المقارنة على مجموعةٍ لا على ترتيب كتابة.
 */
export const AGENT_PERMITTED_VERBS: readonly string[] = [
  "add",
  "create",
  "draft",
  "list",
  "read",
  "reconcile",
  "record",
  "verify",
];

/** الفعل الأوّل في معرّف العملية — المقطع الصغير الذي يبدأ به. */
function leadingVerb(operationId: string): string {
  const match = /^[a-z]+/.exec(operationId);
  return match ? match[0] : "";
}

/* ═════════════════════════ ٢ · أيّ عمليةٍ تُنشئ أيّ نوع — الجدول الوحيد */

/** العملية المنشورة التي تُنشئ كلَّ نوع، ومخطّط جسمها. */
export interface CreateOperationRef {
  readonly operationId: string;
  readonly requestSchema: string;
}

/**
 * الجدول المكتوب بيد — ستّة صفوف، **ويطابقها اختبارٌ بالعقد المنشور**.
 * ووجودُ صفٍّ هنا لا يعني أن الورقة تُرسم: القواعد أدناه قد ترفضه باسمه.
 */
export const AGENT_CREATE_OPERATIONS: Readonly<Record<AgentEntityKind, CreateOperationRef>> = {
  customer: { operationId: "addCustomer", requestSchema: "CustomerRequest" },
  supplier: { operationId: "addSupplier", requestSchema: "SupplierRequest" },
  employee: { operationId: "registerEmployee", requestSchema: "HrEmployeeRequest" },
  inventoryItem: { operationId: "addItem", requestSchema: "ItemRequest" },
  propertyUnit: { operationId: "createUnit", requestSchema: "UnitRequest" },
  project: { operationId: "addProject", requestSchema: "ProjectRequest" },
};

/* ═══════════════════════════════════════════ ٣ · شكل الحقل المعروض */

/** كيف يُعرض الحقل — مشتقٌّ من صيغة العقد لا مُختار في الشاشة. */
export type AgentCreateFieldKind = "text" | "money" | "decimal" | "choice";

/** حقلٌ واحد على ورقة الإنشاء. */
export interface AgentCreateField {
  /** مسار الحقل في جسم الطلب: `code` أو `name.ar`. */
  readonly path: string;
  /** آخر مقطعٍ من المسار — للعرض. */
  readonly name: string;
  /** إلزاميٌّ في العقد؟ */
  readonly required: boolean;
  /** كيف يُعرض. */
  readonly kind: AgentCreateFieldKind;
  /** النمط المنشور، منقولاً حرفياً. غيابه يعني أن العقد لم ينشر نمطاً — ولا يُخترَع. */
  readonly pattern?: string;
  /** أعضاء المجموعة المغلقة حين يكون الحقل تعداداً. */
  readonly choices?: readonly string[];
}

/** سببُ رفضِ رسمِ ورقةِ إنشاء — كلٌّ يسمّي البند المسؤول. */
export type AgentCreateRefusal =
  | "operationAbsent"
  | "verbNotPermitted"
  | "parentRequired"
  | "schemaAbsent"
  | "fieldIsAList";

/** خطّة ورقة الإنشاء: إمّا حقولٌ تُرسَم، وإمّا رفضٌ يسمّي بنده. */
export type AgentCreatePlan =
  | {
      readonly ok: true;
      readonly operationId: string;
      readonly requestSchema: string;
      readonly fields: readonly AgentCreateField[];
      /**
       * حقولٌ اختيارية من نوع القائمة لا تُعرض هنا. **مُعلَنةٌ لا مبتلَعة**:
       * الورقة تقول ما لم تسأل عنه، فلا يظنّ أحد أنها سألت عن كل شيء.
       */
      readonly omitted: readonly string[];
    }
  | {
      readonly ok: false;
      readonly operationId: string;
      readonly reason: AgentCreateRefusal;
      /** البند المسؤول: اسم الحقل، أو مقطع المسار، أو الفعل. */
      readonly subject: string;
    };

/* ═════════════════════════════════════════════ ٤ · اشتقاق الحقول */

/** النمط المنشور لصيغةٍ محتجزة، أو `undefined` إن لم ينشر العقد لها نمطاً. */
function brandPattern(brand: string): string | undefined {
  const table = FORMATS as unknown as Readonly<Record<string, string | RegExp>>;
  const value = table["SCHEMA_" + brand];
  return typeof value === "string" ? value : undefined;
}

/** نتيجةُ توسيع مخطّطٍ واحد: حقولٌ ومتروكات، أو بندٌ لا يُملأ. */
interface Expansion {
  readonly fields: AgentCreateField[];
  readonly omitted: string[];
  readonly listField?: string;
}

/**
 * يوسّع مخطّطاً إلى حقولٍ مسطّحة. يعاود على المخطّطات المتداخلة (`LocalizedText`
 * تصير `name.ar` و`name.en`)، ويقف عند أوّل حقلٍ إلزاميٍّ من نوع القائمة.
 */
function expand(schemaName: string, prefix: string, parentRequired: boolean, depth: number): Expansion {
  const fields: AgentCreateField[] = [];
  const omitted: string[] = [];
  const schema = SCHEMAS[schemaName];
  if (!schema || depth > 4) return { fields, omitted, listField: prefix || schemaName };

  const required = new Set(schema.required);
  for (const name of Object.keys(schema.fields).sort()) {
    const shape: FieldShape | undefined = schema.fields[name];
    if (!shape) continue;
    const path = prefix ? prefix + "." + name : name;
    const isRequired = parentRequired && required.has(name);

    if (shape.k === "array") {
      /* قائمةٌ إلزامية: الورقة لا تبنيها، فتُرفض الورقة كلّها باسم الحقل.
         وقائمةٌ اختيارية: تُترك وتُعلَن — الطلب صحيحٌ بدونها. */
      if (isRequired) return { fields, omitted, listField: path };
      omitted.push(path);
      continue;
    }
    if (shape.k === "ref") {
      const inner = expand(shape.r ?? "", path, isRequired, depth + 1);
      if (inner.listField) return { fields, omitted, listField: inner.listField };
      fields.push(...inner.fields);
      omitted.push(...inner.omitted);
      continue;
    }
    if (shape.k === "money") {
      fields.push({ path, name, required: isRequired, kind: "money", pattern: FORMATS.SCHEMA_Money });
      continue;
    }
    if (shape.k === "brand") {
      const pattern = brandPattern(shape.b ?? "");
      fields.push({
        path,
        name,
        required: isRequired,
        kind: "decimal",
        ...(pattern ? { pattern } : {}),
      });
      continue;
    }
    if (shape.e && shape.e.length > 0) {
      fields.push({ path, name, required: isRequired, kind: "choice", choices: shape.e });
      continue;
    }
    fields.push({ path, name, required: isRequired, kind: "text" });
  }
  return { fields, omitted };
}

/**
 * يخطّط ورقة الإنشاء لنوعٍ من الكيانات — **من العقد المنشور وحده**.
 * @param kind نوع الكيان.
 */
export function planAgentCreateSheet(kind: AgentEntityKind): AgentCreatePlan {
  const ref = AGENT_CREATE_OPERATIONS[kind];
  const operation = CONTRACT.operations.find((candidate) => candidate.id === ref.operationId);
  if (!operation) {
    return { ok: false, operationId: ref.operationId, reason: "operationAbsent", subject: ref.operationId };
  }

  const verb = leadingVerb(ref.operationId);
  if (!AGENT_PERMITTED_VERBS.includes(verb)) {
    return { ok: false, operationId: ref.operationId, reason: "verbNotPermitted", subject: verb };
  }

  /* مسارٌ يسمّي أباً: `{propertyId}` في مسار الوحدة. والورقة تعرف الشركة من
     الجلسة ولا تعرف العقار، واختيارُه بالتخمين يُنشئ وحدةً تحت عقارٍ آخر. */
  const parents = [...operation.path.matchAll(/\{([A-Za-z0-9]+)\}/g)]
    .map((match) => match[1] ?? "")
    .filter((name) => name !== "companyId");
  if (parents.length > 0) {
    return { ok: false, operationId: ref.operationId, reason: "parentRequired", subject: parents[0] ?? "" };
  }

  if (!SCHEMAS[ref.requestSchema]) {
    return { ok: false, operationId: ref.operationId, reason: "schemaAbsent", subject: ref.requestSchema };
  }

  const expanded = expand(ref.requestSchema, "", true, 0);
  if (expanded.listField) {
    return { ok: false, operationId: ref.operationId, reason: "fieldIsAList", subject: expanded.listField };
  }
  return {
    ok: true,
    operationId: ref.operationId,
    requestSchema: ref.requestSchema,
    fields: expanded.fields,
    omitted: expanded.omitted,
  };
}

/* ═══════════════════════════════════ ٥ · فحص ما كُتب قبل إرساله */

/** بندٌ مرفوض على الورقة: الحقل وسببه. */
export interface AgentCreateFault {
  readonly path: string;
  readonly reason: "missing" | "pattern";
}

/**
 * يفحص القيم المكتوبة على حقول الخطّة. **لا يُصلح ولا يقصّ**: يسمّي البند
 * ويترك التصحيح لصاحبه — على قاعدة `VoiceRefusals` نفسها.
 * @param fields حقول الخطّة.
 * @param values ما كُتب.
 */
export function agentCreateFaults(
  fields: readonly AgentCreateField[],
  values: Readonly<Record<string, string>>
): readonly AgentCreateFault[] {
  const faults: AgentCreateFault[] = [];
  for (const field of fields) {
    const raw = (values[field.path] ?? "").trim();
    if (!raw) {
      if (field.required) faults.push({ path: field.path, reason: "missing" });
      continue;
    }
    if (field.pattern && !new RegExp(field.pattern).test(raw)) {
      faults.push({ path: field.path, reason: "pattern" });
      continue;
    }
    if (field.choices && !field.choices.includes(raw)) {
      faults.push({ path: field.path, reason: "pattern" });
    }
  }
  return faults;
}
