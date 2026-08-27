/* ═══════════════════════════════════════════════════════════════════════════
   المال نصّ — ونوعٌ يمنع أن يصير رقماً
   Money is text — and a type that stops it becoming a number
   ───────────────────────────────────────────────────────────────────────────
   العقد يقول: المبلغ نصّ decimal بمقياس ≤ ٤. والخلفية ترفض الرمز الرقمي في
   حقل مالي رفضاً صريحاً، لأن العميل الذي يمرّره على Number (IEEE-754 ثنائي)
   يُفسده قبل أن يغادر المتصفّح. والمقيس في هذا المستودع:
       1000000000000.4013  →  1000000000000.4      (فقدان أربع خانات)

   TypeScript يُمحى وقت التشغيل، فالنوع وحده لا يحمي. ولذلك المال هنا **كائن**
   لا نصّ: كل تحويل ضمني يرمي — كما في SB.Display تماماً.

       row.debit * 2            → TypeError
       `${row.debit}`           → TypeError
       JSON.stringify(row)      → TypeError   (toJSON يرمي)
       Number(row.debit)        → TypeError
       row.debit + ""           → TypeError

   والمخرجان الوحيدان:
       .text            — نصّ السلك الأصلي، ASCII، بايت ببايت كما وصل.
       fmt.amount(m)    — عرضٌ محلّي يعيد Display (وهو بدوره لا يصير نصّاً).

   ولماذا لا حساب هنا إطلاقاً؟ لأن الجمع على المال قرار محاسبي يقع في SQL
   داخل الاستعلام نفسه (انظر وصف TrialBalance في العقد). والمقارنة الوحيدة
   المسموحة هنا للفرز والعرض، وهي عشرية نصّية بلا فاصلة عائمة في أي خطوة.
   ═══════════════════════════════════════════════════════════════════════════ */

import { SCHEMA_Money_RE } from "./generated/formats";

const TEXT: WeakMap<object, string> = new WeakMap();

function refuse(what: string): () => never {
  return function (): never {
    throw new TypeError(
      "Money: قيمة مالية لا تُحوَّل ضمنياً (" +
        what +
        "). استعمل .text للسلك أو fmt.amount() للعرض. " +
        "[Money never coerces; use .text for the wire or fmt.amount() for display.]"
    );
  };
}

/**
 * مبلغ كما وصل على السلك. غير قابل للتغيير، ولا يتحوّل ضمنياً إلى نصّ أو رقم.
 * A wire amount. Immutable, and never implicitly coerced to string or number.
 */
export class Money {
  /** لا يُبنى مباشرةً: {@link Money.wire} وحدها تتحقّق من النحو المنشور. */
  private constructor(text: string) {
    TEXT.set(this, text);
    Object.freeze(this);
  }

  /**
   * يبني مبلغاً من نصّ السلك، ويرفض كل ما لا يطابق نحو العقد.
   * @param text نصّ المبلغ كما ورد في JSON.
   */
  static wire(text: unknown): Money {
    if (typeof text === "number") {
      throw new TypeError(
        "Money.wire: وصل رمز رقمي في حقل مالي — وهذا ما يرفضه الخادم أصلاً، " +
          "لأن JSON بلا نوع عشري و Number ثنائي عائم فيفقد الدقّة. " +
          "[a JSON number token in a monetary field: refused]"
      );
    }
    if (typeof text !== "string") {
      throw new TypeError("Money.wire: يُتوقّع نصّ. / a string is expected. got " + typeof text);
    }
    if (!SCHEMA_Money_RE.test(text)) {
      throw new TypeError(
        "Money.wire: نصّ لا يطابق نحو المال المنشور " +
          SCHEMA_Money_RE.source +
          " — «" +
          text +
          "». / does not match the published Money grammar."
      );
    }
    return new Money(text);
  }

  /** نصّ السلك كما وصل، بايتاً ببايت. / The wire text, byte for byte. */
  get text(): string {
    return TEXT.get(this) as string;
  }

  /** هل هو صفر؟ (نصّياً، بلا حساب) / Is it zero? (textually, no arithmetic) */
  get isZero(): boolean {
    return /^-?0(\.0+)?$/.test(this.text);
  }

  /** هل هو سالب؟ والصفر السالب ليس سالباً. / Is it negative? Negative zero is not. */
  get isNegative(): boolean {
    return this.text.charAt(0) === "-" && !this.isZero;
  }

  /**
   * مقارنة عشرية نصّية — للفرز والعرض فقط، بلا فاصلة عائمة في أي خطوة.
   * Textual decimal comparison — for ordering only; no floating point anywhere.
   * @param other الطرف الآخر.
   */
  compare(other: Money): -1 | 0 | 1 {
    return compareDecimalText(this.text, other.text);
  }

  /* ── الحدود: كل تحويل ضمني يرمي. وهي على النموذج الأصلي (prototype) لا على
     كل نسخة: ٥٠٠ صفّ × عمودين = ألف مبلغ، ولا يجوز أن يحمل كلٌّ منها خمس دوالّ. */
  /** يرمي دائماً. / Always throws. */
  toString(): never {
    return refuse("toString")();
  }
  /** يرمي دائماً. / Always throws. */
  valueOf(): never {
    return refuse("valueOf")();
  }
  /** يرمي دائماً — فلا يصل مبلغ إلى JSON.stringify بلا ترميز صريح. */
  toJSON(): never {
    return refuse("toJSON")();
  }
  /** يرمي دائماً. / Always throws. */
  localeCompare(): never {
    return refuse("localeCompare")();
  }
  /** يرمي دائماً — يمنع `${m}` و `+m` و `m * 2`. */
  [Symbol.toPrimitive](): never {
    return refuse("implicit coercion")();
  }
}

/** هل القيمة مبلغاً؟ / Is the value a Money? */
export function isMoney(value: unknown): value is Money {
  return value instanceof Money;
}

/* ── مقارنة عشرية على النصّ ────────────────────────────────────────────────
   بلا parseFloat وبلا BigInt على الكسر: نُساوي طول الجزأين ثم نقارن حرفياً. */
function compareDecimalText(a: string, b: string): -1 | 0 | 1 {
  const A = split(a);
  const B = split(b);
  if (A.neg !== B.neg) return A.neg ? -1 : 1;
  const magnitude = compareMagnitude(A, B);
  return (A.neg ? (-magnitude as -1 | 0 | 1) : magnitude);
}
function split(text: string): { neg: boolean; int: string; frac: string } {
  const neg = text.charAt(0) === "-";
  const body = neg ? text.slice(1) : text;
  const dot = body.indexOf(".");
  const int = dot === -1 ? body : body.slice(0, dot);
  const frac = dot === -1 ? "" : body.slice(dot + 1);
  return { neg: neg && !/^0+(\.0*)?$/.test(body), int, frac };
}
function compareMagnitude(
  a: { int: string; frac: string },
  b: { int: string; frac: string }
): -1 | 0 | 1 {
  const ai = a.int.replace(/^0+(?=\d)/, "");
  const bi = b.int.replace(/^0+(?=\d)/, "");
  if (ai.length !== bi.length) return ai.length < bi.length ? -1 : 1;
  if (ai !== bi) return ai < bi ? -1 : 1;
  const width = Math.max(a.frac.length, b.frac.length);
  const af = a.frac.padEnd(width, "0");
  const bf = b.frac.padEnd(width, "0");
  if (af === bf) return 0;
  return af < bf ? -1 : 1;
}
