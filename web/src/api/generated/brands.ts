/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     86abd6e8d0b4da52d6d189807af79da88b2767424f0b4dc69734f68f662f4bb9
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

/** كمّية نصّاً بمقياس لا يتجاوز أربعاً، بالنحو الذي تخضع له المبالغ. وهي ليست مبلغاً — ولذلك لها مخطّطها — لكنها تُضرب في مبلغ، فأي فقدان دقّة فيها يصل إلى المال. / A quantity as a string with at most four decimal places, under the grammar that governs amounts. It is not an amount — hence its own schema — but it is multiplied by one, so any precision lost in it reaches the money. */
export type Quantity = string & { readonly __Quantity: unique symbol };

/**
 * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.
 * @param text النصّ كما ورد. / the text as received.
 */
export function asQuantity(text: unknown): Quantity {
  if (typeof text !== "string" || !F.SCHEMA_Quantity_RE.test(text)) {
    throw new TypeError(
      "asQuantity: نصّ لا يطابق النمط المنشور " + F.SCHEMA_Quantity + " — «" + String(text) + "». / does not match the published pattern."
    );
  }
  return text as Quantity;
}

/** نسبة الضريبة **كسراً عشرياً لا نسبة مئوية**: خمسة عشر بالمئة تُكتب 0.15 لا 15. والمقياس ثمانٍ لا أربع: النسبة ليست مبلغاً ولا تُقرَّب إلى الهللة. / The tax rate as a **decimal fraction, not a percentage**: fifteen percent is written 0.15, never 15. The scale is eight, not four: a rate is not an amount and is not rounded to the halala. */
export type TaxRate = string & { readonly __TaxRate: unique symbol };

/**
 * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.
 * @param text النصّ كما ورد. / the text as received.
 */
export function asTaxRate(text: unknown): TaxRate {
  if (typeof text !== "string" || !F.SCHEMA_TaxRate_RE.test(text)) {
    throw new TypeError(
      "asTaxRate: نصّ لا يطابق النمط المنشور " + F.SCHEMA_TaxRate + " — «" + String(text) + "». / does not match the published pattern."
    );
  }
  return text as TaxRate;
}

/** مدقّق لكل صيغة، مفهرساً باسمها في العقد. / A validator per published format. */
export const BRANDS: Readonly<Record<string, (text: unknown) => string>> = {
  ExchangeRate: asExchangeRate,
  Int64String: asInt64String,
  Quantity: asQuantity,
  TaxRate: asTaxRate,
};
