/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     e2ba8112a2421c49813d9475f8849856d96e8af4f94d9cc0d071498442a6e2ca
   المولّد · generator: web/scripts/generate-client.mjs

   لإعادة التوليد:  npm run gen
   بوابة الانحراف:  npm run gen:check   (تفشل عند أي اختلاف بايت واحد)

   الصيغ النصّية المنشورة، محتجزةً بالنوع ومعها مدقّقاتها.
   نصٌّ لا يطابق النمط لا يصير قيمةً من هذه الأنواع أبداً.
   ═══════════════════════════════════════════════════════════════════════ */

import * as F from "./formats";

/** سعر صرف نصّاً بمقياس لا يتجاوز ثمانياً، بالقواعد نفسها التي تحكم المبالغ. / An exchange rate as a string with at most eight decimal places, under the same rules as amounts. */
export type ExchangeRate = string & { readonly __ExchangeRate: unique symbol };

/**
 * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.
 * @param text النصّ كما ورد. / the text as received.
 */
export function asExchangeRate(text: unknown): ExchangeRate {
  if (typeof text !== "string" || !F.SCHEMA_ExchangeRate_RE.test(text)) {
    throw new TypeError(
      "asExchangeRate: نصّ لا يطابق النمط المنشور " + F.SCHEMA_ExchangeRate + " — «" + String(text) + "». / does not match the published pattern."
    );
  }
  return text as ExchangeRate;
}

/** عدد صحيح 64 بت نصّاً: Number في JavaScript يفقد الدقّة فوق 2^53، ورقم القيد معرّف لا كمّية. / A 64-bit integer as a string: JavaScript Number loses precision above 2^53, and an entry number is an identifier, not a quantity. */
export type Int64String = string & { readonly __Int64String: unique symbol };

/**
 * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.
 * @param text النصّ كما ورد. / the text as received.
 */
export function asInt64String(text: unknown): Int64String {
  if (typeof text !== "string" || !F.SCHEMA_Int64String_RE.test(text)) {
    throw new TypeError(
      "asInt64String: نصّ لا يطابق النمط المنشور " + F.SCHEMA_Int64String + " — «" + String(text) + "». / does not match the published pattern."
    );
  }
  return text as Int64String;
}

/** مدقّق لكل صيغة، مفهرساً باسمها في العقد. / A validator per published format. */
export const BRANDS: Readonly<Record<string, (text: unknown) => string>> = {
  ExchangeRate: asExchangeRate,
  Int64String: asInt64String,
};
