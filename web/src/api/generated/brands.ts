/* ═══════════════════════════════════════════════════════════════════════
   مُولَّد آلياً — لا تُحرِّر هذا الملف بيدك.
   GENERATED FILE — DO NOT EDIT BY HAND.

   المصدر · source:  contracts/openapi/v1.json
   بصمة المصدر · source sha256:
     e25d2ecb3673e9e5364e6e1bac84e2e55f24100d5f24ddf15175661144024721
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

/** مقدار كمّية نصّاً بمقياس لا يتجاوز **ستّاً**. والكمّية ليست مبلغاً — ولذلك لها مقياسها — لكنها تُضرب في تكلفة الوحدة، فأي دقّة تُفقد فيها تصل إلى المال. والكيلوغرامات واللترات والأمتار تُكسَر إلى ما دون الهللة، ومقياسٌ مالي عليها يُنتج تقريباً صامتاً يتراكم على كل حركة. / A quantity magnitude as a string with at most **six** decimal places. A quantity is not an amount — hence its own scale — but it is multiplied by a unit cost, so any precision lost in it reaches the money. Kilograms, litres, and metres divide below the halala, and a money scale over them produces a silent rounding that accumulates on every movement. */
export type Magnitude = string & { readonly __Magnitude: unique symbol };

/**
 * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.
 * @param text النصّ كما ورد. / the text as received.
 */
export function asMagnitude(text: unknown): Magnitude {
  if (typeof text !== "string" || !F.SCHEMA_Magnitude_RE.test(text)) {
    throw new TypeError(
      "asMagnitude: نصّ لا يطابق النمط المنشور " + F.SCHEMA_Magnitude + " — «" + String(text) + "». / does not match the published pattern."
    );
  }
  return text as Magnitude;
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

/** نسبة تعاقدية **كسراً عشرياً لا نسبة مئوية**: عشرة بالمئة تُكتب 0.10 لا 10. والمقياس ثمانٍ لا أربع: النسبة ليست مبلغاً ولا تُقرَّب إلى الهللة. **وهي تأتي من العقد لا من قيمة ثابتة في الكود** — نصّ مصفوفة الترحيل على المحتجز بحرفه. / A contractual rate as a **decimal fraction, not a percentage**: ten percent is written 0.10, never 10. The scale is eight, not four: a rate is not an amount and is not rounded to the halala. **It comes from the contract, never from a constant in code** — the posting matrix's text on retention, verbatim. */
export type Rate = string & { readonly __Rate: unique symbol };

/**
 * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.
 * @param text النصّ كما ورد. / the text as received.
 */
export function asRate(text: unknown): Rate {
  if (typeof text !== "string" || !F.SCHEMA_Rate_RE.test(text)) {
    throw new TypeError(
      "asRate: نصّ لا يطابق النمط المنشور " + F.SCHEMA_Rate + " — «" + String(text) + "». / does not match the published pattern."
    );
  }
  return text as Rate;
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

/** متوسط تكلفة الوحدة نصّاً بمقياس **ستّ خانات لا أربع**: صنفٌ يُشترى بألف حبّة بمئة ريال تكلفة وحدته 0.100000، وبمقياس أربعة تصير 0.1000 والفرق لا يظهر — لكنه يتراكم على كل صرف حتى ينحرف رصيد القيمة عن مجموع حركاته. / The moving average unit cost as a string with **six** decimal places rather than four: an item bought at a thousand pieces for a hundred riyals has a unit cost of 0.100000, which at scale four becomes 0.1000 and the difference disappears — yet it accumulates on every issue until the value balance no longer equals the sum of its movements. */
export type UnitCost = string & { readonly __UnitCost: unique symbol };

/**
 * يتحقّق من النمط المنشور ثم يحتجز النوع. / Validates then brands.
 * @param text النصّ كما ورد. / the text as received.
 */
export function asUnitCost(text: unknown): UnitCost {
  if (typeof text !== "string" || !F.SCHEMA_UnitCost_RE.test(text)) {
    throw new TypeError(
      "asUnitCost: نصّ لا يطابق النمط المنشور " + F.SCHEMA_UnitCost + " — «" + String(text) + "». / does not match the published pattern."
    );
  }
  return text as UnitCost;
}

/** مدقّق لكل صيغة، مفهرساً باسمها في العقد. / A validator per published format. */
export const BRANDS: Readonly<Record<string, (text: unknown) => string>> = {
  ExchangeRate: asExchangeRate,
  Int64String: asInt64String,
  Magnitude: asMagnitude,
  Quantity: asQuantity,
  Rate: asRate,
  TaxRate: asTaxRate,
  UnitCost: asUnitCost,
};
