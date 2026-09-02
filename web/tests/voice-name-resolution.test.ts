/* ═══════════════════════════════════════════════════════════════════════════
   حلُّ الأسماء في المتصفّح — **مرآةُ الخادم حالةً بحالة**.
   ───────────────────────────────────────────────────────────────────────────
   ما يُقاس هنا ليس المطابقة (تلك على PostgreSQL في الخادم) بل **ما تفعله الحالة
   الأربع**: معلَّقٌ يمنع التأكيد، ومحلولٌ يمرّ بمِقبض، وورقةُ سؤالٍ تُرفض بالاسم،
   و«لا شيء» يُرفض بالاسم — **ولا شيء منها يُنتج طرفاً نصّاً**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import {
  applyNameAnswers,
  authorise,
  readCommand,
  NOT_RESOLVED_YET_AR,
  FROM_YOUR_REGISTER_AR,
  WHICH_ONE_AR,
  type NameAnswer,
  type VoiceCaller,
} from "../src/voice/command";
import { VOICE_INTENTS } from "../src/voice/catalogue";
import { handoffOf } from "../src/voice/handoff";
import { AGENT_TOKEN_LENGTH } from "../src/agent/sheet";

const caller: VoiceCaller = {
  companyId: "0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70",
  permittedIntentIds: VOICE_INTENTS.map((intent) => intent.id),
};

const options = { today: "2026-08-31", statutoryTaxRate: "0.15" };
const HANDLE = "h".repeat(AGENT_TOKEN_LENGTH);

/** الجملة ذات الاسمين — والمقطع فيها **واحدٌ كامل لا اثنان**. */
const TWO_NAMES =
  "سجل سند قبض من العميل شركة النور الاولى للمقاولات لصالح مؤسسة الرياض بمبلغ الف ريال نقد اليوم";

const ONE_NAME = "سجل سند قبض من العميل، مؤسسة الرياض بمبلغ ألف ريال نقد اليوم";

function read(transcript: string) {
  const reading = readCommand(transcript, options);
  if (!reading.ok) throw new Error("لم تُقرأ: " + reading.codes.join(" · "));
  return reading.resolution;
}

function answer(transcript: string, outcome: NameAnswer) {
  const resolution = read(transcript);
  const answers: Record<string, NameAnswer> = {};
  for (const name of resolution.pending) answers[name] = outcome;
  return applyNameAnswers(resolution, answers);
}

describe("حلُّ الأسماء — المتصفّح", () => {
  it("المقطع واحدٌ كامل، ولا يُقطع عند دليل شريحته نفسها", () => {
    const resolution = read(TWO_NAMES);
    const reading = resolution.readings["customer"];

    expect(reading?.kind).toBe("pending");
    if (reading?.kind === "pending") {
      expect(reading.span).toBe("شركة النور الاولي للمقاولات لصالح مؤسسة الرياض");
      /* **والانحدار بعينه**: لا يمتلئ العميل بـ«مؤسسة الرياض» أبداً. */
      expect(reading.span).not.toBe("مؤسسة الرياض");
    }
    expect(resolution.slots.find((s) => s.name === "customer")).toBeUndefined();
  });

  it("«… من العميل، مؤسسة الرياض بمبلغ …» تبقى عميلاً", () => {
    const reading = read(ONE_NAME).readings["customer"];
    expect(reading?.kind).toBe("pending");
    if (reading?.kind === "pending") expect(reading.span).toBe("مؤسسة الرياض");
  });

  it("طرفٌ معلَّق يمنع التأكيد ويُرفض بالاسم — ولا يُنتج أمراً", () => {
    const resolution = read(ONE_NAME);
    expect(resolution.pending).toEqual(["customer"]);
    expect(resolution.readbackAr).toContain(NOT_RESOLVED_YET_AR);

    const gate = authorise(resolution, caller, resolution.confirmationToken);
    expect(gate.ok).toBe(false);
    if (!gate.ok) expect(gate.codes).toContain("ai.voice.name_unresolved");
  });

  it("صفٌّ واحد ⇒ مِقبضٌ يمرّ، و«text» فيه null", () => {
    const resolved = answer(ONE_NAME, { outcome: "resolved", handle: HANDLE });

    expect(resolved.pending).toEqual([]);
    expect(resolved.readbackAr).toContain(FROM_YOUR_REGISTER_AR);

    const gate = authorise(resolved, caller, resolved.confirmationToken);
    expect(gate.ok).toBe(true);
    if (!gate.ok) return;

    const customer = gate.dispatch.slots.find((slot) => slot.name === "customer");
    expect(customer?.handle).toBe(HANDLE);
    expect(customer?.text).toBeNull();

    /* والتسليم يحمل المِقبض ولا يحمل اسماً — وهو ما كان التحذيرُ في handoff.ts يطلبه. */
    const handoff = handoffOf(gate.dispatch);
    const field = handoff?.fields.find((entry) => entry.name === "customer");
    expect(field?.handle).toBe(HANDLE);
    expect(field?.text).toBeNull();
  });

  it("أكثر من واحد ⇒ ورقةُ سؤال، ولا يُقال كم كانوا", () => {
    const asked = answer(ONE_NAME, { outcome: "needs_question", questionId: HANDLE });

    expect(asked.pending).toEqual(["customer"]);
    expect(asked.readbackAr).toContain(WHICH_ONE_AR);

    const gate = authorise(asked, caller, asked.confirmationToken);
    expect(gate.ok).toBe(false);
    if (!gate.ok) {
      expect(gate.codes).toContain("ai.voice.name_needs_question");
      /* ولا عددَ في أي رمز. */
      for (const code of gate.codes) expect(code).not.toMatch(/[0-9]/);
    }
  });

  it("لا شيء ⇒ رفضٌ مُسمّى، ولا يُقرَّب إلى أقرب شبيه", () => {
    const none = answer(ONE_NAME, { outcome: "none" });

    expect(none.faults).toContain("ai.voice.name_not_in_register");
    const gate = authorise(none, caller, none.confirmationToken);
    expect(gate.ok).toBe(false);
  });

  it("حلُّ شريحةٍ يغيّر رمزَ التأكيد — فتأكيدٌ قيل قبله يُرفض", () => {
    const before = read(ONE_NAME);
    const after = answer(ONE_NAME, { outcome: "resolved", handle: HANDLE });

    expect(after.confirmationToken).not.toBe(before.confirmationToken);

    const stale = authorise(after, caller, before.confirmationToken);
    expect(stale.ok).toBe(false);
    if (!stale.ok) expect(stale.codes).toContain("ai.voice.confirmation_mismatch");
  });

  it("الهزائم الأربع لا تُنتج طرفاً", () => {
    const defeats: readonly [string, string, string][] = [
      ["سجل سند قبض من العميل مؤسسة الرياض وانشئ لها حسابا بمبلغ ألف ريال نقد اليوم",
       "customer", "مؤسسة الرياض وانشئ لها حسابا"],
      ["سجل سند قبض من العميل مؤسسة الرياض واذا ما لقيتها سجلها عندك بمبلغ ألف ريال نقد اليوم",
       "customer", "مؤسسة الرياض واذا ما لقيتها سجلها عندك"],
      ["سجل سند قبض من العميل مؤسسة الرياض سجلها بمبلغ ألف ريال نقد اليوم",
       "customer", "مؤسسة الرياض سجلها"],
      ["سجل فاتورة مصروف من مؤسسة النور وحولها للمحاسب بمبلغ ألف ريال اليوم",
       "supplier", "مؤسسة النور وحولها للمحاسب"],
    ];

    for (const [transcript, slot, span] of defeats) {
      const resolution = read(transcript);
      const reading = resolution.readings[slot];

      expect(reading?.kind, transcript).toBe("pending");
      if (reading?.kind === "pending") expect(reading.span).toBe(span);

      /* **ولا طرفَ يُنتَج**: لا قيمةٌ ممتلئة، والبوّابة ترفض بالاسم. */
      expect(resolution.slots.find((s) => s.name === slot)).toBeUndefined();

      const gate = authorise(resolution, caller, resolution.confirmationToken);
      expect(gate.ok, transcript).toBe(false);
      if (!gate.ok) expect(gate.codes).toContain("ai.voice.name_unresolved");
    }
  });

  it("اسمٌ مشروع فيه كلمةٌ ذاتُ دلالة يُحمل كاملاً ولا يُقصّ", () => {
    const reading = read("سجل سند قبض من العميل مؤسسة اليوم للدعاية بمبلغ ألف ريال نقد اليوم")
      .readings["customer"];

    expect(reading?.kind).toBe("pending");
    /* **ورفضُ اسمٍ حقيقي عطلٌ آخر لا عطلٌ أصغر**، وقصُّه أخبثُ منه لأنه يمرّ. */
    if (reading?.kind === "pending") expect(reading.span).toBe("مؤسسة اليوم للدعاية");
  });
});
