/* قارئ الأعداد المنطوقة في المتصفّح — يقرأ **نفس** ملف المتجهات الذي يقرؤه
   نظيره في الخادم. تنفيذان بملفَّي متجهات ينحرفان ولا يُكتشف الانحراف. */
import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { readArabicNumber, normaliseToken } from "../src/voice/arabic-number";

/* المسار من جذر المستودع لا من ملفّ الاختبار: vitest يشغّل من web/. */
const VECTORS_PATH = path.resolve(
  process.cwd(),
  "../tests/Babel.Ai.Tests/golden/arabic-spoken-numbers.v1.json"
);

interface Vectors {
  accepted: { phrase: string; value: string }[];
  rejected: { phrase: string; code: string }[];
}

const vectors: Vectors = JSON.parse(readFileSync(VECTORS_PATH, "utf8"));

describe("قارئ الأعداد العربية المنطوقة", () => {
  it("مجموعة المتجهات ليست ضامرة", () => {
    /* حارس لا فراغ: ملفٌّ فارغ يجعل كل ما تحته يمرّ بلا أن يقرأ شيئاً. */
    expect(vectors.accepted.length).toBeGreaterThanOrEqual(20);
    expect(vectors.rejected.length).toBeGreaterThanOrEqual(6);
  });

  it.each(vectors.accepted.map((v) => [v.phrase, v.value] as const))(
    "يقرأ «%s» = %s",
    (phrase, value) => {
      const read = readArabicNumber(phrase);
      expect(read.ok, phrase + " رُفضت وهي مقبولة").toBe(true);
      if (read.ok) expect(Number(read.text)).toBe(Number(value));
    }
  );

  it.each(vectors.rejected.map((v) => [v.phrase, v.code] as const))(
    "يرفض «%s» برمز %s",
    (phrase, code) => {
      const read = readArabicNumber(phrase);
      expect(read.ok, phrase + " قُبلت وهي مرفوضة").toBe(false);
      if (!read.ok) expect(read.code).toBe(code);
    }
  );

  it("كل نظام أرقام على حدة يُطبَّع، والخلط داخل الكلمة يُرفض", () => {
    for (const token of ["١٢٣", "۱۲۳", "१२३", "123"]) {
      const r = normaliseToken(token);
      expect(r.ok).toBe(true);
      if (r.ok) expect(r.text).toBe("123");
    }
    const mixed = normaliseToken("١٢3");
    expect(mixed.ok).toBe(false);
    if (!mixed.ok) expect(mixed.code).toBe("ai.voice.mixed_digit_systems");
  });
});
