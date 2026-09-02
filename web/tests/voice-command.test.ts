/* ═══════════════════════════════════════════════════════════════════════════
   قارئ الأمر المنطوق في المتصفّح — يقرأ **نفس** ملفّ المتجهات الذي يقرؤه نظيره
   في الخادم. تنفيذان بملفَّي متجهات ينحرفان، ولا يُكتشف الانحراف إلا على شاشة
   صاحب المصلحة (ADR-0030 خامساً).
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  authorise,
  isSpokenCancellation,
  isSpokenConfirmation,
  maskPersonal,
  disclosureFault,
  readCommand,
  CONFIRM_CALL_AR,
  applyNameAnswers,
  type NameAnswer,
  type VoiceCaller,
} from "../src/voice/command";
import { AGENT_TOKEN_LENGTH } from "../src/agent/sheet";
import { VOICE_INTENTS, VOICE_SECTIONS, intentById } from "../src/voice/catalogue";

/* المسار من جذر المستودع لا من ملفّ الاختبار: vitest يشغّل من web/. */
const VECTORS_PATH = path.resolve(
  process.cwd(),
  "../tests/Babel.Ai.Tests/golden/voice-intents.v1.json"
);

interface Vectors {
  today: string;
  statutoryTaxRate: string;
  intents: {
    id: string;
    section: string;
    kind: string;
    status: string;
    ledgerEffect: string;
    eventCode: string | null;
    operationId: string | null;
    requiresConfirmation: boolean;
    readsPersonalData: boolean;
    nameAr: string;
    phrases: string[];
    slots: {
      name: string;
      kind: string;
      nameAr: string;
      required: boolean;
      cues: string[];
      choices: string[];
      registerKey?: string;
    }[];
  }[];
  utterances: { transcript: string; intent: string; slots: Record<string, string>; units?: Record<string, string> }[];
  missing: { transcript: string; intent: string; missing: string[]; faults?: string[]; withoutToday?: boolean }[];
  refusals: { transcript: string; code: string }[];
  /** المقاطع التي تُحلّ إلى صفٍّ واحد، لكل سجلّ. **مِفصلٌ لا مُطابِق** — انظر صدر الملفّ. */
  registers: Record<string, string[]>;
}

const vectors: Vectors = JSON.parse(readFileSync(VECTORS_PATH, "utf8"));
const options = { today: vectors.today, statutoryTaxRate: vectors.statutoryTaxRate };

/**
 * **يقرأ ثم يحلّ** — والخطوتان معاً هما ما يصل البوّابة. والأجوبة تُشتقّ من كتلة
 * `registers` في ملفّ المتجهات نفسه، فالتنفيذان يقرآن **مِفصلاً واحداً**.
 */
function readAndResolve(transcript: string, opts = options) {
  const read = readCommand(transcript, opts);
  if (!read.ok) return read;

  const answers: Record<string, NameAnswer> = {};

  for (const slot of read.resolution.intent.slots) {
    const reading = read.resolution.readings[slot.name];
    if (!reading || reading.kind !== "pending") continue;

    const known = vectors.registers[reading.registerKey] ?? [];
    answers[slot.name] = known.includes(reading.span)
      ? { outcome: "resolved", handle: "h".repeat(AGENT_TOKEN_LENGTH) }
      : { outcome: "none" };
  }

  return { ok: true as const, resolution: applyNameAnswers(read.resolution, answers) };
}

const caller: VoiceCaller = {
  companyId: "0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70",
  permittedIntentIds: VOICE_INTENTS.map((intent) => intent.id),
};

describe("سجلّ النيّات في المتصفّح", () => {
  it("مجموعة المتجهات ليست ضامرة", () => {
    /* حارس لا فراغ: ملفٌّ فارغ يجعل كل ما تحته يمرّ بلا أن يقرأ شيئاً. */
    expect(vectors.intents.length).toBeGreaterThanOrEqual(40);
    expect(vectors.utterances.length).toBeGreaterThanOrEqual(40);
    expect(vectors.missing.length).toBeGreaterThanOrEqual(40);
    expect(vectors.refusals.length).toBeGreaterThanOrEqual(3);
  });

  it("مرآة المتصفّح تطابق سجلّ الخادم نيّةً نيّة", () => {
    expect(VOICE_INTENTS.map((i) => i.id)).toEqual(vectors.intents.map((i) => i.id));

    for (const declared of vectors.intents) {
      const mirrored = intentById(declared.id);
      expect(mirrored, declared.id).not.toBeNull();
      expect(mirrored).toEqual(declared);
    }
  });

  /* ‏**والسقف سقط بسقوط المعيار القديم**: عددُ نيّات القسم صار مشتقّاً من عدد عمليات
     المسوّدة المنشورة فيه، وهو ينمو بنموّ المنتج. وسقفٌ مكتوب كان سيمنع نيّةً صحيحة
     لأنها السابعة. */
  it("الأقسام الخمسة كلّها مسكونة، ولكلٍّ خمسُ نيّاتٍ فأكثر", () => {
    expect(VOICE_SECTIONS).toHaveLength(5);
    for (const section of VOICE_SECTIONS) {
      const count = VOICE_INTENTS.filter((intent) => intent.section === section.id).length;
      expect(count, section.id).toBeGreaterThanOrEqual(5);
    }
  });

  /* ⚠ **الحارس الذي يمنع خطأ الغد في المتصفّح**: لا نيّة تبلغ عملية ترحيلٍ أو توقيعٍ
     أو اعتماد. والفعلُ يُفحص لا الاسم، فعمليةٌ تُنشر غداً بفعلٍ لم يُصنَّف لا تمرّ.
     ونظيرُه في الخادم يقرأ هذا الملفّ نفسه ويطابقه بالعقد المنشور. */
  it("لا نيّة تبلغ عملية ترحيل ولا توقيع ولا اعتماد", () => {
    const forbidden = ["post", "activate", "sign", "approve", "terminate", "revoke", "reverse", "lapse", "delete", "forfeit", "void"];
    const permitted = ["draft", "create", "add", "record", "read", "list", "reconcile", "verify"];
    let measured = 0;

    for (const intent of VOICE_INTENTS) {
      if (intent.status === "AwaitingOwnerDecision") {
        expect(intent.operationId, intent.id).toBeNull();
        continue;
      }

      measured++;
      expect(intent.operationId, intent.id).not.toBeNull();
      const verb = /^[a-z]+/.exec(intent.operationId!)?.[0] ?? "";
      expect(forbidden, intent.id + " → " + intent.operationId).not.toContain(verb);
      expect(permitted, intent.id + " → " + intent.operationId).toContain(verb);
    }

    expect(measured).toBeGreaterThanOrEqual(40);
  });

  it("كل نيّة تُغيّر الحال تطلب تأكيداً، ولا استعلام يطلبه", () => {
    let changing = 0;
    for (const intent of VOICE_INTENTS) {
      if (intent.kind === "StateChange") {
        changing++;
        expect(intent.requiresConfirmation, intent.id).toBe(true);
      } else {
        expect(intent.requiresConfirmation, intent.id).toBe(false);
      }
    }
    expect(changing).toBeGreaterThanOrEqual(26);
  });
});

describe("القراءة الحتمية", () => {
  it.each(vectors.utterances.map((v) => [v.transcript, v.intent] as const))(
    "«%s» ← %s",
    (transcript, intentId) => {
      const vector = vectors.utterances.find((v) => v.transcript === transcript)!;
      const read = readCommand(transcript, options);
      expect(read.ok, transcript).toBe(true);
      if (!read.ok) return;

      expect(read.resolution.intent.id).toBe(intentId);
      expect(read.resolution.missingSlots).toEqual([]);

      for (const [name, expected] of Object.entries(vector.slots)) {
        const slot = read.resolution.intent.slots.find((s) => s.name === name);

        /* **شريحة الطرف لا تُقرأ قيمةً**: القيمة المتوقَّعة في المتجه هي **المقطع**. */
        if (slot?.kind === "Entity") {
          const reading = read.resolution.readings[name];
          expect(reading?.kind, name + " في «" + transcript + "»").toBe("pending");
          if (reading?.kind === "pending") {
            expect(reading.span, name).toBe(expected);
            expect(reading.registerKey, name).toBe(slot.registerKey);
          }
          expect(read.resolution.slots.find((s) => s.name === name)).toBeUndefined();
          continue;
        }

        const value = read.resolution.slots.find((s) => s.name === name);
        expect(value, name + " في «" + transcript + "»").toBeDefined();
        expect(value!.text, name).toBe(expected);
      }

      /* ولكل شريحةٍ معلَنة قراءةٌ واحدة، لا أقلّ ولا أكثر. */
      expect(Object.keys(read.resolution.readings).length).toBe(read.resolution.intent.slots.length);

      /* ثم يُسأل السجلّ، فيصير كلُّ طرفٍ مِقبضاً — وهذه هي السباكة كاملةً. */
      const answered = readAndResolve(transcript);
      expect(answered.ok).toBe(true);
      if (answered.ok) {
        expect(answered.resolution.pending).toEqual([]);
        for (const slot of answered.resolution.intent.slots) {
          if (slot.kind !== "Entity") continue;
          const reading = answered.resolution.readings[slot.name];
          expect(reading?.kind, slot.name + " بعد السؤال").toBe("resolved");
        }
      }
      for (const [name, unit] of Object.entries(vector.units ?? {})) {
        expect(read.resolution.slots.find((s) => s.name === name)!.unit, name).toBe(unit);
      }
    }
  );

  it.each(vectors.missing.map((v) => [v.transcript, v.missing.join(",")] as const))(
    "«%s» ينقصها %s",
    (transcript) => {
      const vector = vectors.missing.find((v) => v.transcript === transcript)!;
      /* ‏**بلا حقنِ تاريخِ اليوم لا يُملأ حقلُ تاريخٍ إطلاقاً** — والمتجه الذي يطلب
         ذلك يقيس القاعدة نفسها في التنفيذين معاً. */
      const read = readCommand(
        transcript,
        vector.withoutToday ? { statutoryTaxRate: vectors.statutoryTaxRate } : options
      );
      expect(read.ok, transcript).toBe(true);
      if (!read.ok) return;

      expect([...read.resolution.missingSlots].sort()).toEqual([...vector.missing].sort());
      for (const name of vector.missing) {
        expect(read.resolution.slots.find((s) => s.name === name)).toBeUndefined();
      }
      for (const code of vector.faults ?? []) {
        expect(read.resolution.faults).toContain(code);
      }
    }
  );

  it.each(vectors.refusals.map((v) => [v.transcript, v.code] as const))(
    "يرفض «%s» برمز %s",
    (transcript, code) => {
      const read = readCommand(transcript, options);
      expect(read.ok).toBe(false);
      if (read.ok) return;
      expect(read.codes).toContain(code);
    }
  );

  it("جملة تطابق نيّتين تُرفض ولا يُختار أحدهما بالقرعة", () => {
    const read = readCommand("سجل سند قبض وسجل سند صرف", options);
    expect(read.ok).toBe(false);
    if (!read.ok) expect(read.codes).toContain("ai.voice.intent_ambiguous");
  });

  it("بلا حقن تاريخ اليوم لا يُملأ حقل تاريخ إطلاقاً", () => {
    const read = readCommand("سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد", {});
    expect(read.ok).toBe(true);
    if (!read.ok) return;
    expect(read.resolution.missingSlots).toContain("receivedOn");
  });
});

describe("البوابة في المتصفّح", () => {
  const changing = vectors.utterances.filter((v) => {
    const intent = intentById(v.intent)!;
    return intent.kind === "StateChange" && intent.status === "Published";
  });

  it("قائمة العمليات المُغيِّرة ليست ضامرة", () => {
    expect(changing.length).toBeGreaterThanOrEqual(11);
  });

  it.each(changing.map((v) => [v.transcript, v.intent] as const))(
    "«%s» لا تمرّ بلا تأكيد",
    (transcript) => {
      /* **قبل السؤال: كلُّ طرفٍ معلَّق، والبوّابة ترفضه بالاسم.** */
      const raw = readCommand(transcript, options);
      expect(raw.ok).toBe(true);
      if (!raw.ok) return;

      if (raw.resolution.pending.length > 0) {
        const unresolved = authorise(raw.resolution, caller, raw.resolution.confirmationToken);
        expect(unresolved.ok).toBe(false);
        if (!unresolved.ok) expect(unresolved.codes).toContain("ai.voice.name_unresolved");
      }

      const read = readAndResolve(transcript);
      expect(read.ok).toBe(true);
      if (!read.ok) return;

      const refused = authorise(read.resolution, caller, null);
      expect(refused.ok).toBe(false);
      if (!refused.ok) expect(refused.codes).toContain("ai.voice.confirmation_required");

      /* والملخّص يدعو إلى التأكيد نصّاً — يُقرأ ويُعرض معاً. */
      expect(read.resolution.readbackAr).toContain(CONFIRM_CALL_AR);

      const allowed = authorise(read.resolution, caller, read.resolution.confirmationToken);
      expect(allowed.ok, transcript).toBe(true);
      if (allowed.ok) {
        expect(allowed.dispatch.confirmedByHuman).toBe(true);

        /* **ولا نصَّ لطرفٍ في الأمر**: الطرف مِقبضٌ و`text` فيه `null`. */
        for (const slot of allowed.dispatch.intent.slots) {
          if (slot.kind !== "Entity") continue;
          const value = allowed.dispatch.slots.find((v) => v.name === slot.name);
          expect(value?.handle, slot.name).toBeTruthy();
          expect(value?.text, slot.name).toBeNull();
        }
      }

      const stale = authorise(read.resolution, caller, read.resolution.confirmationToken + "|تغيّر");
      expect(stale.ok).toBe(false);
      if (!stale.ok) expect(stale.codes).toContain("ai.voice.confirmation_mismatch");
    }
  );

  it("ما لا يملكه المتكلّم يُرفض قبل كل شيء", () => {
    const read = readCommand("كم رصيد العميل مؤسسة الرياض", options);
    expect(read.ok).toBe(true);
    if (!read.ok) return;
    const refused = authorise(read.resolution, { ...caller, permittedIntentIds: [] }, null);
    expect(refused.ok).toBe(false);
    if (!refused.ok) expect(refused.codes).toEqual(["ai.voice.not_permitted"]);
  });

  it("دليل شركة داخل أمر آخر يُرفض — ولا يُحلَّل له اسم", () => {
    const read = readCommand(
      "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم في شركة الفروع",
      options
    );
    expect(read.ok).toBe(true);
    if (!read.ok) return;
    /* **الدليل هو الإشارة، لا الاسمُ المُحلَّل**: مقارنةُ نصٍّ بنصٍّ حكماً على الهوية حُذفت. */
    expect(read.resolution.companyCueHeard).toBe(true);
    const refused = authorise(read.resolution, caller, read.resolution.confirmationToken);
    expect(refused.ok).toBe(false);
    if (!refused.ok) expect(refused.codes).toContain("ai.voice.company_not_switched");
  });

  it("النيّة التي تنتظر قرار المالك تُفهَم ولا تُنفَّذ ولو اكتملت وأُكِّدت", () => {
    const read = readCommand(
      "تسكين القطع الصنف اسمنت كمية خمسة أكياس المستودع الرئيسي من الرف واحد الى الرف اثنين",
      options
    );
    expect(read.ok).toBe(true);
    if (!read.ok) return;
    expect(read.resolution.missingSlots).toEqual([]);
    const refused = authorise(read.resolution, caller, read.resolution.confirmationToken);
    expect(refused.ok).toBe(false);
    if (!refused.ok) expect(refused.codes).toEqual(["ai.voice.owner_decision_pending"]);
  });

  it("كلمة التأكيد المنطوقة مغلقة ولا تُقارَب بأقرب شبيه", () => {
    expect(isSpokenConfirmation("تأكيد")).toBe(true);
    expect(isSpokenConfirmation("تمام اعتمد")).toBe(true);
    expect(isSpokenConfirmation("تقريباً")).toBe(false);
    expect(isSpokenCancellation("إلغاء")).toBe(true);
    expect(isSpokenCancellation("تأكيد")).toBe(false);
  });
});

describe("حارس الإفشاء في المتصفّح", () => {
  it("القناع هو قناع الموارد البشرية نفسه", () => {
    expect(maskPersonal("1092837465")).toBe("••••7465");
    expect(maskPersonal("SA0380000000608010167519")).toBe("••••7519");
    expect(maskPersonal("123")).toBe("••••");
    expect(maskPersonal(null)).toBe("••••");
  });

  it("نصّ يحمل هويةً أو آيباناً كاملاً يُرفض نُطقُه، والمُقنَّع يمرّ", () => {
    expect(disclosureFault("الهوية 1092837465")).toBe("ai.voice.masked_read_required");
    expect(disclosureFault("الآيبان SA0380000000608010167519")).toBe("ai.voice.masked_read_required");
    expect(disclosureFault("الهوية ••••7465 والآيبان ••••7519")).toBeNull();
  });

  it("قراءة تُنتج ملخصاً يحمل هويةً غير مقنّعة تُرفض قبل أن تعود", () => {
    const read = readCommand("حالة الوحدة 1092837465", options);
    expect(read.ok).toBe(false);
    if (!read.ok) expect(read.codes).toContain("ai.voice.masked_read_required");
  });
});
