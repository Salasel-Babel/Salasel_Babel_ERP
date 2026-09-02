/* ═══════════════════════════════════════════════════════════════════════════
   قارئ الأمر المنطوق في المتصفّح — يقرأ **نفس** ملفّ المتجهات الذي يقرؤه نظيره
   في الخادم. تنفيذان بملفَّي متجهات ينحرفان، ولا يُكتشف الانحراف إلا على شاشة
   صاحب المصلحة (ADR-0030 خامساً).
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import {
  adjudicateSpan,
  authorise,
  isSpokenCancellation,
  isSpokenConfirmation,
  maskPersonal,
  disclosureFault,
  readCommand,
  CODE_WORD_LIMIT,
  CONFIRM_CALL_AR,
  NAME_WORD_LIMIT,
  type VoiceCaller,
} from "../src/voice/command";
import { VOICE_INTENTS, VOICE_SECTIONS, intentById } from "../src/voice/catalogue";
import { FREE_TEXT_SLOTS, fieldsAwaitingResolution, handoffOf } from "../src/voice/handoff";

/* المسار من جذر المستودع لا من ملفّ الاختبار: vitest يشغّل من web/. */
const VECTORS_PATH = path.resolve(
  process.cwd(),
  "../tests/Babel.Ai.Tests/golden/voice-intents.v1.json"
);

interface Vectors {
  today: string;
  statutoryTaxRate: string;
  companyNameAr: string;
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
    slots: { name: string; kind: string; nameAr: string; required: boolean; cues: string[]; choices: string[] }[];
  }[];
  utterances: { transcript: string; intent: string; slots: Record<string, string>; units?: Record<string, string> }[];
  missing: { transcript: string; intent: string; missing: string[]; faults?: string[]; withoutToday?: boolean }[];
  refusals: { transcript: string; code: string }[];
}

const vectors: Vectors = JSON.parse(readFileSync(VECTORS_PATH, "utf8"));
const options = { today: vectors.today, statutoryTaxRate: vectors.statutoryTaxRate };

const caller: VoiceCaller = {
  companyId: "0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70",
  companyNameAr: vectors.companyNameAr,
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
        const value = read.resolution.slots.find((s) => s.name === name);
        expect(value, name + " في «" + transcript + "»").toBeDefined();
        expect(value!.text, name).toBe(expected);
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

  /* ‏**استبدال الطرف الصامت** — نظير الإثبات في الخادم حرفاً
     (‏SpokenCommandTests.المقطع_المرفوض_لا_يُستبدَل_صامتاً_بطرفٍ_آخر).
     الرفض كان يُخزَّن ويُمضى إلى الدليل التالي، فيعود **طرفٌ آخر** بلا عطلٍ واحد. */
  it("المقطع المرفوض لا يُستبدَل صامتاً بطرفٍ آخر في الجملة", () => {
    const read = readCommand(
      "سجل سند قبض من العميل شركة النور الاولى للمقاولات لصالح مؤسسة الرياض بمبلغ الف ريال نقد اليوم",
      options
    );

    expect(read.ok).toBe(true);
    if (!read.ok) return;

    // ‏١ · الشريحة تبقى فارغة، والنقص مُسمّى، والرفض مسموع.
    expect(read.resolution.slots.find((slot) => slot.name === "customer")).toBeUndefined();
    expect(read.resolution.missingSlots).toContain("customer");
    expect(read.resolution.faults).toContain("ai.voice.name_not_bounded");

    // ‏٢ · **والحدّ**: الطرف الثاني لا يتسرّب إلى شيء يُقرأ أو يُنفَّذ عليه.
    expect(read.resolution.readbackAr).not.toContain("مؤسسة الرياض");
    expect(read.resolution.slots.some((slot) => slot.text.includes("الرياض"))).toBe(false);

    // ‏٣ · حارس لا فراغ: الرفض مقصورٌ على شريحته لا سقوطٌ عامّ.
    expect(read.resolution.slots.find((slot) => slot.name === "amount")?.text).toBe("1000");
    expect(read.resolution.slots.find((slot) => slot.name === "method")?.text).toBe("نقد");
  });

  it("بلا حقن تاريخ اليوم لا يُملأ حقل تاريخ إطلاقاً", () => {
    const read = readCommand("سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد", {});
    expect(read.ok).toBe(true);
    if (!read.ok) return;
    expect(read.resolution.missingSlots).toContain("receivedOn");
  });
});

describe("حكم المقطع — يُقبل كاملاً أو يُرفض باسمه", () => {
  const kindOf = new Map<string, string>();
  for (const intent of vectors.intents) {
    for (const slot of intent.slots) kindOf.set(intent.id + "/" + slot.name, slot.kind);
  }

  /* الرقم **بياناتٌ مملوكة** لا سحرٌ مكتوب بيد: يُعاد حسابه من الملفّ الذي يصف
     المنتج، فإن احتاج متجهٌ جديد اسماً أطول حمرّ هذا وطلب تعديلاً **يُراجَع**. */
  it("حدّا الاسم والرمز يُعاد حسابهما من المتجهات لا يُكتبان بيد", () => {
    let longestName = 0;
    let longestCode = 0;

    for (const vector of vectors.utterances) {
      for (const [name, value] of Object.entries(vector.slots)) {
        const kind = kindOf.get(vector.intent + "/" + name);
        const count = value.split(" ").filter((word) => word.length > 0).length;
        if (kind === "Text") longestName = Math.max(longestName, count);
        if (kind === "Code") longestCode = Math.max(longestCode, count);
      }
    }

    /* حارس لا فراغ: صفرٌ هنا يعني أن المُحلِّل لم يقرأ شيئاً فمرّ. */
    expect(longestName).toBeGreaterThan(0);
    expect(longestCode).toBeGreaterThan(0);

    expect(longestName).toBe(NAME_WORD_LIMIT);
    expect(longestCode).toBe(CODE_WORD_LIMIT);
  });

  /* الحكم يقيس **شكل المقطع**، لا كلماتٍ بأعيانها. فالصيغ المصرَّفة التي لا تُطابقها
     قائمةُ كلماتٍ كاملة أبداً تُلتقَط هنا، والأسماء الحقيقية تمرّ. */
  it("الضمير المتّصل يُقاس بالشكل، والأسماء الحقيقية تمرّ", () => {
    for (const tail of [
      ["مؤسسة", "الرياض", "سجلها"],
      ["مؤسسة", "الرياض", "راجعهم"],
      ["شركة", "النور", "اكتبها"],
      ["مؤسسة", "النور", "وحولها"],
      ["شركة", "النور", "ارسلهن"],
      ["مؤسسة", "النور", "بلغكم"],
    ]) {
      expect(adjudicateSpan(tail, NAME_WORD_LIMIT), tail.join(" ")).toBe("predicationTail");
    }

    for (const name of [
      ["شركة", "الركن", "الذهبي"],
      ["مؤسسة", "المساكن"],
      ["شركة", "الاسهم"],
      ["مؤسسة", "الحكم"],
      ["شركة", "النور", "للمساكن"],
      ["مها", "العتيبي"],
      ["مؤسسة", "سجل", "الرياض"],
      ["شركة", "سهم"],
      ["مؤسسة", "درهم"],
    ]) {
      expect(adjudicateSpan(name, NAME_WORD_LIMIT), name.join(" ")).toBe("admitted");
    }

    /* وكلمةٌ واحدة لا تُحاكَم بالضمير أصلاً: «مها» وحدها اسمٌ تامّ. */
    expect(adjudicateSpan(["مها"], NAME_WORD_LIMIT)).toBe("admitted");
    expect(adjudicateSpan(["شركة", "النور", "الاولى", "للمقاولات"], NAME_WORD_LIMIT)).toBe("tooManyWords");
  });

  /* **انحدارُ الترقيم لا يعود**: حكمُ المقطع لا يُقسّم الجملة ولا يُضيف حدّاً، فالفاصلة
     تبقى مُهمَلة كما كانت. ويُقاس ذلك خاصّيةً على المتجهات كلّها لا قيمةً واحدة. */
  it("الفاصلة قبل القيمة لا تُغيّر القراءة في أي متجه", () => {
    let measured = 0;

    for (const vector of vectors.utterances) {
      const plain = readCommand(vector.transcript, options);
      expect(plain.ok, vector.transcript).toBe(true);
      if (!plain.ok) continue;

      for (const value of Object.values(vector.slots)) {
        const at = vector.transcript.indexOf(" " + value);
        if (at < 0) continue;

        const twin =
          vector.transcript.slice(0, at) + "، " + vector.transcript.slice(at + 1);
        const comma = readCommand(twin, options);
        expect(comma.ok, twin).toBe(true);
        if (!comma.ok) continue;
        measured++;

        expect(comma.resolution.intent.id).toBe(plain.resolution.intent.id);
        expect(comma.resolution.slots).toEqual(plain.resolution.slots);
        expect(comma.resolution.missingSlots).toEqual(plain.resolution.missingSlots);
        expect(comma.resolution.faults).toEqual(plain.resolution.faults);
        expect(comma.resolution.spokenCompany).toBe(plain.resolution.spokenCompany);
        expect(comma.resolution.readbackAr).toBe(plain.resolution.readbackAr);
        expect(comma.resolution.confirmationToken).toBe(plain.resolution.confirmationToken);
      }
    }

    expect(measured).toBeGreaterThanOrEqual(40);
  });

  /* الرمز المنطوق **لا يُبتَر عند الحدّ**: بترُه كان يُنتج رقم ضمانٍ صحيح الشكل
     لضمانٍ لا وجود له، وهي صورةُ الفساد الصامت نفسها. */
  it("الرمز الذي لا يُعرف أين ينتهي يُرفض باسمه ولا يُبتَر", () => {
    const read = readCommand(
      "سجل ضمان بنكي للعقد برج الياسمين رقم الضمان ض-4410 ينتهي 2027-03-31",
      options
    );
    expect(read.ok).toBe(true);
    if (!read.ok) return;
    expect(read.resolution.missingSlots).toContain("guaranteeNumber");
    expect(read.resolution.faults).toContain("ai.voice.name_not_bounded");
    expect(read.resolution.slots.find((s) => s.name === "guaranteeNumber")).toBeUndefined();
  });

  /* **الثقب المُعلَن — مقيسٌ هنا قبل أن يجده أحد.** فعلٌ بمفعوله يبدأ بشكل أداة
     التعريف («التقطها»)، وذيلُ إسنادٍ بلا ضمير داخل الحدّ («وحول المبلغ») — لا قاعدة
     إملائية تفصلهما عن اسمٍ حقيقي، والجواب هو الطبقة الثانية لا بندٌ يُضاف لقائمة. */
  it("الثقب المُعلَن في الطبقة الأولى مقيس لا موصوف", () => {
    const article = readCommand(
      "سجل سند قبض من العميل مؤسسة الرياض التقطها بمبلغ الف ريال نقد اليوم",
      options
    );
    expect(article.ok).toBe(true);
    if (!article.ok) return;
    expect(article.resolution.slots.find((s) => s.name === "customer")!.text).toBe(
      "مؤسسة الرياض التقطها"
    );

    const tail = readCommand(
      "سجل سند قبض من العميل النور وحول المبلغ بمبلغ الف ريال نقد اليوم",
      options
    );
    expect(tail.ok).toBe(true);
    if (!tail.ok) return;
    expect(tail.resolution.slots.find((s) => s.name === "customer")!.text).toBe("النور وحول المبلغ");

    /* ٣ · الهاء المفردة مفعولاً — مُخرَجة عمداً: التفريغ يكتب التاء المربوطة هاءً بلا
       قاعدة، فمنعُها يرفض «شركة صيانه» و«مؤسسة تجاره». ورفضُ اسمٍ حقيقي عطلٌ آخر. */
    const he = readCommand(
      "سجل جرد الصنف اسمنت مقاوم وسجله كمية عشرين كيس المستودع الرئيسي اليوم",
      options
    );
    expect(he.ok).toBe(true);
    if (!he.ok) return;
    expect(he.resolution.slots.find((s) => s.name === "item")!.text).toBe("اسمنت مقاوم وسجله");

    /* ٤ · وثمنُ الحدّ مُعلَن كذلك: اسمٌ حقيقيّ من أربع كلمات **يُرفض ولا يُبتَر**. */
    const four = readCommand(
      "سجل سند قبض من العميل شركة النور الأولى للمقاولات بمبلغ الف ريال نقد اليوم",
      options
    );
    expect(four.ok).toBe(true);
    if (!four.ok) return;
    expect(four.resolution.missingSlots).toContain("customer");
    expect(four.resolution.faults).toContain("ai.voice.name_not_bounded");

    /* وما مرّ من الطبقة الأولى يقف عند الثانية: الحقل يخرج موسوماً بأنه يلزمه معرّف. */
    const allowed = authorise(article.resolution, caller, article.resolution.confirmationToken);
    expect(allowed.ok).toBe(true);
    if (!allowed.ok) return;
    const handoff = handoffOf(allowed.dispatch)!;
    expect(fieldsAwaitingResolution(handoff).map((f) => f.name)).toContain("customer");
  });
});

describe("الطبقة الثانية — الاسم لا يغادر الشاشة إلا معرّفاً", () => {
  /* قطبُ القائمة هو الحارس: ما ليس في `FREE_TEXT_SLOTS` **يلزمه حلٌّ**، فشريحةٌ
     تُضاف غداً تبدأ مطلوبةَ الحلّ افتراضاً — والنسيان يُنتج تشدّداً لا تساهلاً. */
  it("كل شريحة نصّية ليست في قائمة النصّ الحرّ تلزمها الحلّ إلى معرّف", () => {
    let entities = 0;

    for (const intent of VOICE_INTENTS) {
      for (const slot of intent.slots) {
        if (slot.kind !== "Text") continue;
        if (FREE_TEXT_SLOTS.includes(slot.name)) continue;
        entities++;
      }
    }

    expect(entities).toBeGreaterThanOrEqual(15);
    expect(FREE_TEXT_SLOTS.length).toBeLessThanOrEqual(4);
  });

  it("التسليم يحمل الوسم لكل حقل، والأسماء وحدها هي التي تنتظر حلّاً", () => {
    const read = readCommand(
      "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم",
      options
    );
    expect(read.ok).toBe(true);
    if (!read.ok) return;

    const allowed = authorise(read.resolution, caller, read.resolution.confirmationToken);
    expect(allowed.ok).toBe(true);
    if (!allowed.ok) return;

    const handoff = handoffOf(allowed.dispatch);
    expect(handoff).not.toBeNull();
    if (handoff === null) return;

    for (const field of handoff.fields) {
      expect(typeof field.requiresResolution, field.name).toBe("boolean");
    }

    expect(fieldsAwaitingResolution(handoff).map((f) => f.name)).toEqual(["customer"]);
    expect(handoff.fields.find((f) => f.name === "amount")!.requiresResolution).toBe(false);
    expect(handoff.fields.find((f) => f.name === "receivedOn")!.requiresResolution).toBe(false);
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
      const read = readCommand(transcript, options);
      expect(read.ok).toBe(true);
      if (!read.ok) return;

      const refused = authorise(read.resolution, caller, null);
      expect(refused.ok).toBe(false);
      if (!refused.ok) expect(refused.codes).toContain("ai.voice.confirmation_required");

      /* والملخّص يدعو إلى التأكيد نصّاً — يُقرأ ويُعرض معاً. */
      expect(read.resolution.readbackAr).toContain(CONFIRM_CALL_AR);

      const allowed = authorise(read.resolution, caller, read.resolution.confirmationToken);
      expect(allowed.ok, transcript).toBe(true);
      if (allowed.ok) expect(allowed.dispatch.confirmedByHuman).toBe(true);

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

  it("شركة منطوقة غير المفتوحة تُرفض ولا يُنتقَل إليها داخل أمر آخر", () => {
    const read = readCommand(
      "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم في شركة الفروع",
      options
    );
    expect(read.ok).toBe(true);
    if (!read.ok) return;
    expect(read.resolution.spokenCompany).toBe("الفروع");
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
