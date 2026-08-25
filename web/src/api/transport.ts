/* ═══════════════════════════════════════════════════════════════════════════
   النقل وفكّ الترميز — الحدّ بين JSON والأنواع المُولَّدة
   Transport and codec — the boundary between JSON and the generated types
   ───────────────────────────────────────────────────────────────────────────
   ثلاثة أشياء تقع هنا ولا تقع في أي مكان آخر:

   ١ · لفّ المال: كل حقل يقول العقد إنه Money يصير كائن Money، وكل ما عداه
       يمرّ كما هو. مواضع المال تأتي من runtime-schema المُولَّد لا من قائمة
       مكتوبة بيد؛ فحقلٌ مالي جديد في العقد يُلَفّ من تلقائه.

   ٢ · الترميز في الاتجاه المعاكس: الطلب يُبنى بـ.text فلا يمرّ رمز رقمي في
       حقل مالي أبداً — وهو ما ترفضه الخلفية صراحةً.

   ٣ · الخطأ: استجابة غير ناجحة تصير ProblemError يحمل Problem كاملاً بصيغة
       RFC 9457 — بالرسالتين العربية والإنجليزية اللتين ترسلهما الخلفية،
       وبالرمز الثابت الذي هو **نقطة الاعتماد البرمجية الوحيدة**. لا يُقرأ
       نصّ رسالة لاتخاذ قرار.
   ═══════════════════════════════════════════════════════════════════════════ */

import { Money } from "./money";
import { BRANDS } from "./generated/brands";
import type { SchemaShape, FieldShape } from "./generated/runtime-schema";
import type { Problem } from "./generated/types";

/** استجابة خام كما يسلّمها النقل. / A raw response as the transport hands it over. */
export interface RawResponse {
  /** هل الرمز في المدى 2xx؟ */ ok: boolean;
  /** رمز الحالة. */ status: number;
  /** الجسم مُحلَّلاً، أو null. */ json: unknown;
  /** المسار المطلوب — يظهر في رسالة الخطأ حين لا يرسل الخادم instance. */ url: string;
}

/** النقل: دالّة واحدة، فالاختبار يستبدلها بلا شبكة. */
export type Transport = (request: {
  method: string;
  url: string;
  body?: unknown;
  signal?: AbortSignal;
}) => Promise<RawResponse>;

/* ─────────────────────────────────────────────────── فكّ الترميز ─────── */

/**
 * يفكّ ترميز جسم استجابة إلى نوع العقد، ويلفّ كل حقل مالي.
 * @param schemas خريطة المخطّطات المُولَّدة.
 * @param name اسم المخطّط.
 * @param value الجسم كما وصل.
 */
export function decodeSchema(
  schemas: Readonly<Record<string, SchemaShape>>,
  name: string,
  value: unknown
): unknown {
  const shape = schemas[name];
  if (!shape) throw new TypeError("decodeSchema: مخطّط غير معروف · unknown schema: " + name);
  if (value === null || typeof value !== "object" || Array.isArray(value)) {
    throw new TypeError(
      "decodeSchema: يُتوقّع كائن للمخطّط " + name + " · an object is expected for schema " + name
    );
  }
  const source = value as Record<string, unknown>;
  for (const key of shape.required) {
    if (!(key in source)) {
      throw new TypeError(
        "decodeSchema: حقل إلزامي مفقود «" + key + "» في " + name +
          " · required field missing. العقد يُلزم به. / The contract requires it."
      );
    }
  }
  const out: Record<string, unknown> = {};
  for (const [key, raw] of Object.entries(source)) {
    const field = shape.fields[key];
    /* حقل لا يعرفه العقد يمرّ كما هو: إضافة حقل اختياري تبقى في v1 بنصّ سياسة
       الإصدار، فعميلٌ مطابق لا يجوز أن ينكسر بها. */
    out[key] = field ? decodeField(schemas, field, raw, name + "." + key) : raw;
  }
  return out;
}

function decodeField(
  schemas: Readonly<Record<string, SchemaShape>>,
  field: FieldShape,
  raw: unknown,
  where: string
): unknown {
  if (raw === null || raw === undefined) {
    if (field.n || raw === undefined) return raw ?? null;
    throw new TypeError("decodeField: null في حقل لا يقبله · null in a non-nullable field: " + where);
  }
  switch (field.k) {
    case "money":
      return Money.wire(raw);
    case "brand": {
      const brand = BRANDS[field.b as string];
      if (!brand) throw new TypeError("decodeField: صيغة غير معروفة · unknown format: " + field.b);
      return brand(raw);
    }
    case "ref":
      return decodeSchema(schemas, field.r as string, raw);
    case "array":
      if (!Array.isArray(raw)) {
        throw new TypeError("decodeField: يُتوقّع مصفوفة · an array is expected: " + where);
      }
      return raw.map((item, i) =>
        decodeField(schemas, field.i as FieldShape, item, where + "[" + i + "]")
      );
    default:
      return raw;
  }
}

/* ───────────────────────────────────────────────────── الترميز ──────── */

/**
 * يرمّز جسم طلب: المال يخرج بنصّه، لا برمز رقمي.
 * @param schemas خريطة المخطّطات المُولَّدة.
 * @param name اسم المخطّط.
 * @param value الجسم بأنواع العقد.
 */
export function encodeSchema(
  schemas: Readonly<Record<string, SchemaShape>>,
  name: string,
  value: unknown
): unknown {
  const shape = schemas[name];
  if (!shape) throw new TypeError("encodeSchema: مخطّط غير معروف · unknown schema: " + name);
  if (value === null || typeof value !== "object") {
    throw new TypeError("encodeSchema: يُتوقّع كائن · an object is expected: " + name);
  }
  const out: Record<string, unknown> = {};
  for (const [key, raw] of Object.entries(value as Record<string, unknown>)) {
    if (raw === undefined) continue;
    const field = shape.fields[key];
    out[key] = field ? encodeField(schemas, field, raw) : raw;
  }
  return out;
}

function encodeField(
  schemas: Readonly<Record<string, SchemaShape>>,
  field: FieldShape,
  raw: unknown
): unknown {
  if (raw === null) return null;
  switch (field.k) {
    case "money":
      if (!(raw instanceof Money)) {
        throw new TypeError(
          "encodeField: حقل مالي لا يحمل Money. الرقم لا يعبر السلك — " +
            "استعمل Money.wire(\"…\"). / a monetary field must hold a Money."
        );
      }
      return raw.text;
    case "brand":
      /* الصيغ المحتجزة نصوص أصلاً؛ وأي شيء آخر خطأ برمجي يُرفَع لا يُحوَّل. */
      if (typeof raw !== "string") {
        throw new TypeError(
          "encodeField: صيغة محتجزة لا تحمل نصّاً · a branded format must hold a string."
        );
      }
      return raw;
    case "ref":
      return encodeSchema(schemas, field.r as string, raw);
    case "array":
      return (raw as unknown[]).map((item) => encodeField(schemas, field.i as FieldShape, item));
    default:
      return raw;
  }
}

/* ────────────────────────────────────────────── خطأ العقد ───────────── */

/** استجابة غير ناجحة، بتفاصيل RFC 9457 كما ينشرها العقد. */
export class ProblemError extends Error {
  /** رمز الحالة. */
  readonly status: number;
  /** الرمز الثابت — نقطة الاعتماد البرمجية الوحيدة. */
  readonly code: string;
  /** تفاصيل المشكلة كما وصلت، أو null إن لم تكن بالصيغة المنشورة. */
  readonly problem: Problem | null;

  private constructor(status: number, code: string, message: string, problem: Problem | null) {
    super(message);
    this.name = "ProblemError";
    this.status = status;
    this.code = code;
    this.problem = problem;
  }

  /**
   * يبني الخطأ من استجابة خام. جسمٌ لا يطابق الصيغة لا يُخفي الخطأ:
   * يبقى الرمز والحالة، ويصير problem = null، فيعرف السطح أن الخادم لم ينطق العقد.
   * @param response الاستجابة الخام.
   */
  static from(response: RawResponse): ProblemError {
    const body = response.json as Partial<Problem> | null;
    const looksLikeProblem =
      body !== null &&
      typeof body === "object" &&
      typeof body.code === "string" &&
      typeof body.titleAr === "string" &&
      typeof body.detailAr === "string";
    return new ProblemError(
      response.status,
      looksLikeProblem ? (body.code as string) : "http." + response.status,
      looksLikeProblem
        ? (body.code as string) + ": " + (body.detail ?? "")
        : "HTTP " + response.status + " " + response.url,
      looksLikeProblem ? (body as Problem) : null
    );
  }
}

/* ────────────────────────────────────────────── نقل fetch ───────────── */

/**
 * نقل يعتمد fetch. يُبقي الجسم نصّاً غير مُحلَّل حتى يتأكّد أنه JSON.
 * @param options الأصل والاعتماد.
 */
export function fetchTransport(options: {
  baseUrl: string;
  token?: string;
  fetch?: typeof globalThis.fetch;
}): Transport {
  const doFetch = options.fetch ?? globalThis.fetch.bind(globalThis);
  const base = options.baseUrl.replace(/\/+$/, "");
  return async ({ method, url, body, signal }) => {
    const headers: Record<string, string> = { Accept: "application/json, application/problem+json" };
    if (options.token) headers.Authorization = "Bearer " + options.token;
    if (body !== undefined) headers["Content-Type"] = "application/json";
    const response = await doFetch(base + url, {
      method,
      headers,
      body: body === undefined ? undefined : JSON.stringify(body),
      signal,
    });
    const text = await response.text();
    let json: unknown = null;
    if (text.length > 0) {
      try {
        json = JSON.parse(text);
      } catch {
        json = null;
      }
    }
    return { ok: response.ok, status: response.status, json, url };
  };
}
