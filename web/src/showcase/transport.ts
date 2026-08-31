/* ═══════════════════════════════════════════════════════════════════════════
   نقل العرض — بديلُ الفتحة الوحيدة، بلا شبكة
   ───────────────────────────────────────────────────────────────────────────
   كل عملية في العميل المُولَّد تمرّ بدالّةٍ واحدة: `Transport`. وهذه هي
   الفتحة، وهي ما يُستبدَل هنا — **ولا يُمَسّ العميل المُولَّد ولا العقد**،
   فبوّابة الانحراف `gen:check` تبقى خضراء.

   والمسار يُطابَق بقوالب العقد لا بنصٍّ مكتوب بيد: مسارٌ لا يعرفه العقد
   يرتدّ ٤٠٤ كما يرتدّ من الخادم، لا بجوابٍ مُختلَق.

   **ولا بايت يغادر الصفحة.** ما لا يُعرَف يُرفَض، ولا يُحاوَل عليه اتصال.
   ═══════════════════════════════════════════════════════════════════════════ */

import type { RawResponse, Transport } from "../api/transport";
import { OPERATIONS } from "./operations";
import { ANSWERS, type Ask } from "./fixtures";
import { shapeOf } from "./synth";
import { NOT_IN_SHOWCASE, problemBody, type Refusal } from "./refusals";

/** مسارٌ مطابق: العملية ووسائطها. */
interface Match {
  readonly operation: (typeof TEMPLATES)[number]["operation"];
  readonly params: Readonly<Record<string, string>>;
}

const TEMPLATES = Object.entries(OPERATIONS).map(([key, operation]) => {
  const space = key.indexOf(" ");
  return {
    key,
    operation,
    method: key.slice(0, space),
    segments: key.slice(space + 1).split("/"),
  };
});

function match(method: string, path: string): Match | null {
  const segments = path.split("/");
  for (const template of TEMPLATES) {
    if (template.method !== method || template.segments.length !== segments.length) continue;
    const params: Record<string, string> = {};
    let hit = true;
    for (let i = 0; i < segments.length; i++) {
      const expected = template.segments[i] ?? "";
      const actual = segments[i] ?? "";
      if (expected.startsWith("{") && expected.endsWith("}")) {
        params[expected.slice(1, -1)] = decodeURIComponent(actual);
        continue;
      }
      if (expected !== actual) { hit = false; break; }
    }
    if (hit) return { operation: template.operation, params };
  }
  return null;
}

function refuse(refusal: Refusal, url: string): RawResponse {
  return { ok: false, status: refusal.status, json: problemBody(refusal, url.split("?")[0] ?? url), url };
}

/**
 * يجيب عن طلبٍ واحد من طبقة العرض.
 * @param method الفعل.
 * @param url المسار كاملاً بمعاملاته.
 * @param body الجسم المُرسَل.
 */
export function answer(method: string, url: string, body: unknown): RawResponse {
  const [rawPath, search = ""] = url.split("?");
  const path = rawPath ?? url;
  const hit = match(method.toUpperCase(), path);
  if (!hit) {
    return {
      ok: false,
      status: 404,
      json: problemBody(
        {
          code: "http.not_found",
          status: 404,
          ar: "لا بابَ في العقد المنشور على هذا المسار: " + method.toUpperCase() + " " + path + ".",
          en: "The published contract has no operation at " + method.toUpperCase() + " " + path + ".",
        },
        path
      ),
      url,
    };
  }

  const { operation } = hit;
  const responder = ANSWERS[operation.id];
  const ask: Ask = { params: hit.params, query: new URLSearchParams(search), body };

  if (responder) {
    const result = responder(ask);
    if (!result.ok) return refuse(result.refuse, url);
    return { ok: true, status: operation.status, json: result.body, url };
  }

  /* بابٌ بلا جوابٍ مُعَدّ. القراءة تُجيب بمخطّطها فارغاً — شكلٌ صحيح وقيمٌ
     صفرية مُعلَنة — والكتابة تُرفَض: عرضٌ يقول «تمّ» على فعلٍ لا يقع يكذب في
     أخصّ ما يبيعه هذا النظام. */
  if (method.toUpperCase() !== "GET") return refuse(NOT_IN_SHOWCASE, url);
  if (operation.schema === null) return refuse(NOT_IN_SHOWCASE, url);
  return { ok: true, status: operation.status, json: shapeOf(operation.schema), url };
}

/** نقلٌ يجيب من طبقة العرض وحدها. */
export function showcaseTransport(): Transport {
  return ({ method, url, body }) => Promise.resolve(answer(method, url, body));
}
