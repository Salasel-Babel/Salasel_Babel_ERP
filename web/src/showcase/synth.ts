/* ═══════════════════════════════════════════════════════════════════════════
   مُركِّب الأجوبة من العقد — شكلٌ منقولٌ لا شكلٌ مُخترَع
   ───────────────────────────────────────────────────────────────────────────
   طبقةُ العرض تُجيب عن مئةٍ وستّين باباً، ولا يُكتب لها مئةٌ وستّون جسماً
   بيد: جسمٌ مكتوب بيد ينحرف عن العقد بحقلٍ ناقص فيسقط فاكُّ الترميز، أو
   بحقلٍ زائد فيَعِد بما لا ينشره الخادم.

   ولذلك يُبنى كل جسمٍ من `SCHEMAS` المُولَّد: الحقول الإلزامية وحدها، وكلٌّ
   بنوعه — والمال والكمّية والنسبة **نصوصٌ** لأن العقد يقول إنها كذلك، لا لأن
   أحداً تذكّر ذلك هنا. وما يُعرَض على المالك من أرقام يأتي من `seed.ts`
   فوق هذا الهيكل، لا منه.

   **وهذا هيكلٌ لا بيانات.** رقمٌ خرج من هنا وحده يخرج صفراً، لأن الصفر
   المُعلَن أصدق من رقمٍ يبدو ذا معنى ولا معنى له.
   ═══════════════════════════════════════════════════════════════════════════ */

import { SCHEMAS, type FieldShape } from "../api/generated/runtime-schema";
import { PLAIN_TYPES } from "./operations";

/** قيمةٌ صفرية بمقياس الصيغة المحتجزة، كما ينشر العقد أنماطها. */
const BRAND_ZERO: Readonly<Record<string, string>> = {
  ExchangeRate: "0",
  Int64String: "0",
  Magnitude: "0",
  Money: "0.0000",
  Quantity: "0.0000",
  Rate: "0",
  TaxRate: "0",
  UnitCost: "0",
};

function plainValue(schema: string, field: string): unknown {
  const kind = PLAIN_TYPES[schema]?.[field];
  if (kind === "boolean") return false;
  if (kind === "integer" || kind === "number") return 0;
  return "";
}

function fieldValue(schema: string, field: string, shape: FieldShape, depth: number): unknown {
  if (shape.n === true) return null;
  switch (shape.k) {
    case "money":
      return "0.0000";
    case "brand":
      return BRAND_ZERO[shape.b ?? ""] ?? "0";
    case "array":
      return [];
    case "ref":
      return depth > 6 ? null : build(shape.r as string, depth + 1);
    default:
      if (shape.e && shape.e.length > 0) return shape.e[0];
      return plainValue(schema, field);
  }
}

function build(name: string, depth: number): Record<string, unknown> {
  const shape = SCHEMAS[name];
  if (!shape) throw new TypeError("مُركِّب العرض: مخطّط غير معروف · unknown schema: " + name);
  const out: Record<string, unknown> = {};
  for (const key of shape.required) {
    const field = shape.fields[key];
    out[key] = field ? fieldValue(name, key, field, depth) : null;
  }
  return out;
}

/**
 * يبني جسماً بأدنى ما يقبله العقد لمخطّطٍ ما: حقوله الإلزامية وحدها، وكلٌّ
 * بنوعه المنشور. ولا يُضاف حقلٌ لا ينشره العقد.
 * @param name اسم المخطّط.
 */
export function shapeOf(name: string): Record<string, unknown> {
  return build(name, 0);
}

/**
 * يبني جسماً من العقد ثمّ يُلبسه قيماً معلومة. القيم تُفحص ضدّ العقد: مفتاحٌ
 * لا يعرفه المخطّط يُرفَع خطأً هنا، لا يصل إلى الشاشة صامتاً.
 * @param name اسم المخطّط.
 * @param values القيم المعلومة.
 */
export function shaped(name: string, values: Readonly<Record<string, unknown>>): Record<string, unknown> {
  const shape = SCHEMAS[name];
  if (!shape) throw new TypeError("مُركِّب العرض: مخطّط غير معروف · unknown schema: " + name);
  for (const key of Object.keys(values)) {
    if (!(key in shape.fields)) {
      throw new TypeError(
        "مُركِّب العرض: حقل «" + key + "» لا ينشره المخطّط " + name +
          " · the contract publishes no such field."
      );
    }
  }
  return { ...build(name, 0), ...values };
}
