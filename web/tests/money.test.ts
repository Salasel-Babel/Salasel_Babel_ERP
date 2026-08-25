/* ═══════════════════════════════════════════════════════════════════════════
   المال لا يُفسَد — الإثبات لا الادّعاء
   ───────────────────────────────────────────────────────────────────────────
   الاختبار الأول هنا يُثبت **الثقب** قبل أن يُسدّ: القيمة التي تفقد دقّتها
   حين تمرّ على Number. والبقيّة تُثبت أن العميل المُولَّد لا يمرّ بها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import { Money, isMoney } from "../src/api/money";
import { decodeSchema, encodeSchema, ProblemError } from "../src/api/transport";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import { SCHEMA_Money, SCHEMA_Money_RE } from "../src/api/generated/formats";
import type { TrialBalance } from "../src/api/generated/types";
import { buildTrialBalance, problem } from "../scripts/mock-api.mjs";

/** القيمة المقيسة في هذا المستودع: تفقد أربع خانات على الفاصلة العائمة الثنائية. */
const HAZARD = "1000000000000.4013";

describe("الثقب نفسه · the hole itself", () => {
  it("يُفسِد Number هذه القيمة فعلاً — وهذا سبب وجود كل ما تحته", () => {
    /* القياس على هذا الجهاز، لا نقلاً: أقرب double للقيمة هو
       1000000000000.40124511718750 فيخرج نصّها …4012 لا …4013.
       ولاحظ صغر الفرق — خانةٌ واحدة في المرتبة الرابعة. عطلٌ بهذا الحجم لا
       يلتقطه نظرُ محاسب في عمود من خمسمئة صفّ، ويلتقطه ميزانٌ لا يتوازن
       بعد شهر. */
    const throughDouble = String(Number(HAZARD));
    expect(throughDouble).not.toBe(HAZARD);
    expect(Number(HAZARD).toFixed(20)).toBe("1000000000000.40124511718750000000");
    expect(throughDouble).toBe("1000000000000.4012");
  });

  it("يُفسِدها JSON.parse على رمز رقمي كذلك — فالرفض على السلك ليس زينة", () => {
    const parsed = JSON.parse('{"debit":' + HAZARD + "}") as { debit: number };
    expect(String(parsed.debit)).toBe("1000000000000.4012");
    expect(String(parsed.debit)).not.toBe(HAZARD);
  });

  it("والدقّة تضيع فوق 2^53 كذلك — ولهذا رقم القيد نصّ لا رقم", () => {
    expect(String(Number("9007199254740993"))).toBe("9007199254740992");
  });
});

describe("Money — النوع المحتجز", () => {
  it("يعبر نصّ السلك بايتاً ببايت في الاتجاهين", () => {
    const m = Money.wire(HAZARD);
    expect(m.text).toBe(HAZARD);
    const encoded = encodeSchema(SCHEMAS, "NamedAmount", { name: "Gross", value: m }) as Record<string, unknown>;
    expect(encoded.value).toBe(HAZARD);
    expect(JSON.stringify(encoded)).toContain('"' + HAZARD + '"');
  });

  it("يرفض الرمز الرقمي في حقل مالي — كما ترفضه الخلفية", () => {
    /* الرقم يُبنى من النصّ لا يُكتب حرفياً: ESLint نفسه يرفض كتابته
       (‏no-loss-of-precision) — وهو شاهدٌ ثالث على أن الحرفيّة تفقد الدقّة. */
    expect(() => Money.wire(Number(HAZARD))).toThrow(TypeError);
  });

  it("يرفض ما لا يطابق النحو المنشور", () => {
    expect(SCHEMA_Money).toBe("^-?(0|[1-9][0-9]*)(\\.[0-9]{1,4})?$");
    for (const bad of ["1e3", "01", "+1", " 1", "1.00000", "١٢٣", "1,000.00", ""]) {
      expect(() => Money.wire(bad), bad).toThrow(TypeError);
    }
    for (const good of ["0", "100", "-1250.0000", "0.4013"]) {
      expect(SCHEMA_Money_RE.test(good), good).toBe(true);
      expect(Money.wire(good).text).toBe(good);
    }
  });

  it("لا يتحوّل ضمنياً بأي طريق — والرمي هو الحارس، لا النوع", () => {
    const m = Money.wire("12.3400");
    expect(() => `${m as unknown as string}`).toThrow(TypeError);
    expect(() => (m as unknown as number) * 2).toThrow(TypeError);
    expect(() => (m as unknown as number) + 1).toThrow(TypeError);
    expect(() => Number(m)).toThrow(TypeError);
    expect(() => JSON.stringify(m)).toThrow(TypeError);
    expect(() => JSON.stringify({ debit: m })).toThrow(TypeError);
    expect(() => String(m)).toThrow(TypeError);
    expect(() => m.toString()).toThrow(TypeError);
    expect(() => m.valueOf()).toThrow(TypeError);
    expect(() => (m as unknown as { localeCompare(x: string): number }).localeCompare("x")).toThrow(TypeError);
    /* والمخرج المسموح يبقى مفتوحاً. */
    expect(m.text).toBe("12.3400");
  });

  it("يقارن عشرياً بلا فاصلة عائمة", () => {
    const rows = ["10", "9.9999", "-1", "0", "1000000000000.4013", "1000000000000.4012"];
    const sorted = rows.map((r) => Money.wire(r)).sort((a, b) => a.compare(b)).map((m) => m.text);
    expect(sorted).toEqual(["-1", "0", "9.9999", "10", "1000000000000.4012", "1000000000000.4013"]);
    /* الخانة الرابعة هي بالضبط ما يضيع على Number: لو مرّت المقارنة عليه
       لتساوى الطرفان. */
    expect(Number("1000000000000.4013") === Number("1000000000000.4012")).toBe(true);
    expect(Money.wire("1000000000000.4013").compare(Money.wire("1000000000000.4012"))).toBe(1);
  });

  it("يعرف الصفر والسالب نصّياً", () => {
    expect(Money.wire("0").isZero).toBe(true);
    expect(Money.wire("0.0000").isZero).toBe(true);
    expect(Money.wire("-0.0000").isZero).toBe(true);
    expect(Money.wire("-0.0000").isNegative).toBe(false);
    expect(Money.wire("-0.0001").isNegative).toBe(true);
    expect(Money.wire("0.0001").isZero).toBe(false);
  });
});

describe("فكّ الترميز المُولَّد", () => {
  it("يلفّ كل حقل مالي في الميزان — والمواضع من العقد لا من قائمة مكتوبة", () => {
    const wire = buildTrialBalance(8, "MAIN", "2026-05");
    const decoded = decodeSchema(SCHEMAS, "TrialBalance", wire) as TrialBalance;
    expect(decoded.rows).toHaveLength(8);
    expect(isMoney(decoded.totalDebit)).toBe(true);
    expect(isMoney(decoded.totalCredit)).toBe(true);
    for (const row of decoded.rows) {
      expect(isMoney(row.debit)).toBe(true);
      expect(isMoney(row.credit)).toBe(true);
    }
    /* الرحلة كاملة: سلك → عميل → سلك، بايتاً ببايت. */
    const hazardRow = decoded.rows.find((r) => r.credit.text === "1000000000000.4013");
    expect(hazardRow, "الصفّ الحامل للقيمة الخطرة موجود").toBeDefined();
    expect(hazardRow?.credit.text).toBe("1000000000000.4013");
    expect(decoded.totalDebit.text).toBe(wire.totalDebit);
    expect(decoded.totalCredit.text).toBe(wire.totalCredit);
  });

  it("يرفض جسماً ينقصه حقل إلزامي يفرضه العقد", () => {
    const wire = buildTrialBalance(2, "MAIN", null) as Record<string, unknown>;
    delete wire.totalDebit;
    expect(() => decodeSchema(SCHEMAS, "TrialBalance", wire)).toThrow(/totalDebit/);
  });

  it("يمرّر حقلاً جديداً لا يعرفه العقد — إضافة الاختياري تبقى في v1", () => {
    const wire = buildTrialBalance(1, "MAIN", null) as Record<string, unknown>;
    wire.futureField = "قيمة من إصدار أحدث";
    const decoded = decodeSchema(SCHEMAS, "TrialBalance", wire) as Record<string, unknown>;
    expect(decoded.futureField).toBe("قيمة من إصدار أحدث");
  });

  it("يرفض ترميز طلب فيه رقم مكان المال", () => {
    expect(() =>
      encodeSchema(SCHEMAS, "NamedAmount", { name: "Gross", value: 12.34 })
    ).toThrow(/Money/);
  });
});

describe("سطح الخطأ", () => {
  it("يحمل الرمز والرسالتين وكل الأخطاء لا أوّلها", () => {
    const body = problem(403, "auth.company_out_of_scope", "/api/v1/companies/x/trial-balance");
    const error = ProblemError.from({ ok: false, status: 403, json: body, url: "/x" });
    expect(error.code).toBe("auth.company_out_of_scope");
    expect(error.problem?.titleAr).toBe("ممنوع");
    expect(error.problem?.errors).toHaveLength(2);
    expect(error.problem?.errors[0]?.messageAr).toContain("الاعتماد");
  });

  it("لا يبتلع استجابة لا تنطق العقد", () => {
    const error = ProblemError.from({ ok: false, status: 502, json: "<html>gateway</html>", url: "/x" });
    expect(error.problem).toBeNull();
    expect(error.code).toBe("http.502");
  });
});
