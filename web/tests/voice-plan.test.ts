/* ═══════════════════════════════════════════════════════════════════════════
   الخطّة المنطوقة — على ملفّ المتجهات نفسه الذي يقرؤه الخادم.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { describe, expect, it } from "vitest";
import { VOICE_PLANS, VOICE_INTENTS, intentById, planById } from "../src/voice/catalogue";
import {
  abandonCurrentStep,
  answerCondition,
  answerCurrentStep,
  completeCurrentStep,
  confirmCurrentStep,
  currentStep,
  matchPlan,
  numeral,
  planLedgerArabic,
  planReadbackArabic,
  planStepPrefix,
  planUncertainAr,
  readCurrentStep,
  startPlan,
} from "../src/voice/plan";
import { readCommand, type VoiceCaller } from "../src/voice/command";

const VECTORS_PATH = path.resolve(process.cwd(), "../tests/Babel.Ai.Tests/golden/voice-intents.v1.json");
const vectors = JSON.parse(readFileSync(VECTORS_PATH, "utf8")) as {
  today: string;
  statutoryTaxRate: string;
  companyNameAr: string;
  plans: unknown[];
};
const options = { today: vectors.today, statutoryTaxRate: vectors.statutoryTaxRate };

const caller: VoiceCaller = {
  companyId: "0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70",
  companyNameAr: vectors.companyNameAr,
  permittedIntentIds: VOICE_INTENTS.map((intent) => intent.id),
};

/** جملة المالك كما نطقها على الخادم الحيّ، حرفاً. */
const OWNER =
  "سجل سند قبض من شركة المسار الامثل فان لم تجدها انشيء لها حسابا ثم سند قبض بقيمة 20000 ريال سعودي بتاريخ اليوم طبعا";

const PLAN_ID = "accounting.customer_receipt.with_new_customer";

describe("سجلّ الخطط في المتصفّح", () => {
  it("المرآة ليست ضامرة وتطابق ملفّ المتجهات خطّةً خطّة", () => {
    expect(VOICE_PLANS.length).toBeGreaterThanOrEqual(1);
    expect(VOICE_PLANS).toEqual(vectors.plans);
  });

  it("كل خطوةٍ في كل خطّة تسمّي نيّةً منشورة في السجلّ — ولا تسمّي عمليةً بحال", () => {
    const forbidden = ["post", "activate", "sign", "approve", "terminate", "revoke", "reverse", "lapse", "delete", "forfeit", "void"];
    let steps = 0;

    for (const plan of VOICE_PLANS) {
      for (const step of plan.steps) {
        steps++;
        const intent = intentById(step.intentId);
        expect(intent, step.intentId).not.toBeNull();
        expect(intent!.status).toBe("Published");
        /* الخطوة تحمل نيّةً لا عملية: لا يوجد في نوعها حقلٌ اسمه operationId أصلاً. */
        expect(Object.keys(step)).not.toContain("operationId");
        const verb = /^[a-z]+/.exec(intent!.operationId!)?.[0] ?? "";
        expect(forbidden, plan.id + " → " + intent!.operationId).not.toContain(verb);
      }
    }
    expect(steps).toBeGreaterThanOrEqual(2);
  });

  it("لا خطّة تحمل أكثر من مستندٍ واحد يُرحَّل", () => {
    for (const plan of VOICE_PLANS) {
      const posting = plan.steps.filter((step) => intentById(step.intentId)?.ledgerEffect === "Posts");
      expect(posting.length, plan.id).toBeLessThanOrEqual(1);
    }
  });
});

describe("مطابقة الخطّة", () => {
  it("جملة المالك تُطابق الخطّة — والطلبُ والشرطُ غيرُ متجاورين فيها", () => {
    const plan = matchPlan(OWNER);
    expect(plan, "جملة المالك لم تُطابق أي خطّة").not.toBeNull();
    expect(plan!.id).toBe(PLAN_ID);
  });

  it("جملةٌ بلا شرطٍ تبقى نيّةً مفردة ولا تسرقها الخطّة", () => {
    const plain = "سجل سند قبض من شركة المسار الامثل نقد بقيمة 20000 ريال بتاريخ اليوم";
    expect(matchPlan(plain)).toBeNull();
    const read = readCommand(plain, options);
    expect(read.ok).toBe(true);
    if (read.ok) expect(read.resolution.intent.id).toBe("accounting.customer_receipt.record");
  });

  it("شرطٌ بلا طلبٍ ليس خطّة", () => {
    expect(matchPlan("فان لم تجدها انشيء لها حسابا")).toBeNull();
  });
});

describe("توجيه الخطّة — يُعرض ويُنطَق ولا يأذن", () => {
  it("نصٌّ واحد يسمّي الخطوتين، ويقول ما تطلبه الشاشةُ ولا يطلبه الصوت", () => {
    const plan = planById(PLAN_ID)!;
    const text = planReadbackArabic(plan);

    expect(text).toContain(plan.nameAr);
    expect(text).toContain("(١)");
    expect(text).toContain("(٢)");
    /* ‏**الثلاثةُ تُقال جهراً**: جوابُ «كان يفترض أن يطلب بيانات أكثر». */
    expect(text).toContain("رمز العميل");
    expect(text).toContain("حدّ الائتمان");
    expect(text).toContain("مهلة السداد");
    /* ولا يُرحَّل شيء بالصوت — تُقال في التوجيه نفسه. */
    expect(text).toContain("ولا يُرحَّل شيء بالصوت");
    /* ولا رمزَ تأكيدٍ فيه: التوجيه لا يأذن. */
    expect(text).not.toContain("قل «تأكيد»");
  });

  it("ترويسةُ الخطوة تُلصَق أمام الملخّص القائم بلا تغييره", () => {
    expect(planStepPrefix(2, 3)).toBe("الخطوة ٢ من ٣ — ");
    expect(numeral(12)).toBe("١٢");
  });
});

describe("المسار السعيد — جملة المالك من طرفها إلى طرفها", () => {
  it("الخطوة الأولى تقرأ اسم العميل نظيفاً من الشرط", () => {
    let run = startPlan(planById(PLAN_ID)!, OWNER);
    run = readCurrentStep(run, options);

    const step = currentStep(run)!;
    expect(step.step.stepId).toBe("create-customer");
    expect(step.step.condition).toBe("WhenHumanFindsNothing");
    const name = step.resolution!.slots.find((s) => s.name === "name")!;
    expect(name.text).toBe("شركة المسار الامثل");
    /* ‏**والذيل المقصوص محمولٌ لا مطروح** — يُعرض كي يرى الإنسان لماذا. */
    expect(name.dropped).toBe("فان لم تجدها انشيء لها حسابا");
  });

  it("إن قال الإنسان إنه وجد العميل تُتخطّى الخطوة الأولى ولا تُنفَّذ", () => {
    let run = startPlan(planById(PLAN_ID)!, OWNER);
    run = readCurrentStep(run, options);
    run = answerCondition(run, true);

    expect(run.steps[0]!.state).toBe("skipped");
    expect(run.at).toBe(1);
    expect(currentStep(run)!.step.stepId).toBe("draft-receipt");
  });

  it("الخطّة كاملةً: إنشاءُ العميل ثم مسوّدةُ السند — ولا شيء يُرحَّل", () => {
    let run = startPlan(planById(PLAN_ID)!, OWNER);

    /* الخطوة ١ — لم يجد العميل، فتبقى قائمة. */
    run = readCurrentStep(run, options);
    run = answerCondition(run, false);
    expect(currentStep(run)!.step.stepId).toBe("create-customer");
    run = confirmCurrentStep(run, caller);
    expect(run.steps[0]!.state).toBe("handedOff");
    expect(run.steps[0]!.handoff!.operationId).toBe("addCustomer");
    expect(run.steps[0]!.handoff!.fields.find((f) => f.name === "name")!.text).toBe("شركة المسار الامثل");
    run = completeCurrentStep(run);

    /* الخطوة ٢ — ينقصها طريقة القبض، والخطّة لا تُعفي من شريحة. */
    run = readCurrentStep(run, options);
    expect(currentStep(run)!.state).toBe("asking");
    expect(currentStep(run)!.resolution!.missingSlots).toEqual(["method"]);

    /* يُجاب بالشريحة وحدها، وتُقرأ في نيّة هذه الخطوة **دون غيرها**. */
    run = answerCurrentStep(run, "نقد", options);
    expect(currentStep(run)!.state).toBe("pending");
    expect(currentStep(run)!.resolution!.missingSlots).toEqual([]);

    run = confirmCurrentStep(run, caller);
    const receipt = run.steps[1]!;
    expect(receipt.state).toBe("handedOff");
    expect(receipt.handoff!.operationId).toBe("draftCustomerReceipt");

    const value = (name: string) => receipt.handoff!.fields.find((f) => f.name === name)!.text;
    expect(value("customer")).toBe("شركة المسار الامثل");
    expect(value("amount")).toBe("20000");
    expect(value("method")).toBe("نقد");
    expect(value("receivedOn")).toBe(vectors.today);

    run = completeCurrentStep(run);
    expect(run.at).toBe(-1);
    expect(run.steps.map((s) => s.state)).toEqual(["done", "done"]);
  });
});

describe("الوقوف والسؤال — ولا يُخترَع شيء", () => {
  it("طريقةُ القبض تبقى رفضاً صحيحاً: الخطّة لا تُعفي من شريحة لازمة", () => {
    let run = startPlan(planById(PLAN_ID)!, OWNER);
    run = readCurrentStep(run, options);
    run = answerCondition(run, true);
    run = readCurrentStep(run, options);

    expect(currentStep(run)!.state).toBe("asking");
    expect(currentStep(run)!.resolution!.missingSlots).toEqual(["method"]);

    /* والبوابة ترفض ما دام ناقصاً — لا تسليم بلا اكتمال. */
    const blocked = confirmCurrentStep(run, caller);
    expect(blocked.steps[1]!.state).toBe("refused");
    expect(blocked.steps[1]!.refusals).toContain("ai.voice.slot_missing");
    expect(blocked.steps[1]!.handoff).toBeNull();
  });

  it("جوابُ الإنسان يُقرأ في نيّة الخطوة وحدها — ولا يُعاد على السجلّ", () => {
    let run = startPlan(planById(PLAN_ID)!, OWNER);
    run = readCurrentStep(run, options);
    run = answerCondition(run, true);
    run = readCurrentStep(run, options);

    /* «صرف» تُطابق نيّاتٍ أخرى تماماً لو مرّت على المطابقة العامّة. */
    const stray = readCommand("نقد", options);
    expect(stray.ok).toBe(false);

    run = answerCurrentStep(run, "نقد", options);
    expect(currentStep(run)!.resolution!.intent.id).toBe("accounting.customer_receipt.record");
    expect(currentStep(run)!.resolution!.slots.find((s) => s.name === "method")!.text).toBe("نقد");
    /* وما قيل أوّلاً لم يضِع: الجواب يُضاف ولا يُبدّل. */
    expect(currentStep(run)!.resolution!.slots.find((s) => s.name === "amount")!.text).toBe("20000");
  });
});

describe("حين تسقط خطوةٌ في الوسط — إفصاحٌ لا تراجع", () => {
  it("دفترُ الخطّة يسمّي ما تمّ وما لم يتمّ، ولا يقترح حذفاً", () => {
    let run = startPlan(planById(PLAN_ID)!, OWNER);
    run = readCurrentStep(run, options);
    run = answerCondition(run, false);
    run = confirmCurrentStep(run, caller);
    run = completeCurrentStep(run);

    /* ثم يترك الإنسانُ شاشةَ السند. */
    run = readCurrentStep(run, options);
    run = abandonCurrentStep(run);

    const ledger = planLedgerArabic(run);
    expect(ledger).toContain("الخطوة ١ تمّت");
    expect(ledger).toContain("الخطوة ٢ تُركت");
    expect(ledger).toContain("ولا شيء يُحذف");
    /* ‏**ولا تراجعَ ولا تعويض**: `delete` فعلٌ ممنوع، ولا حذف في هذا النظام أصلاً. */
    expect(ledger).not.toContain("حذف العميل");

    /* والجريان يقف: لا يُستأنف تلقائياً بعد انقطاع. */
    expect(run.at).toBe(-1);
    expect(currentStep(run)).toBeNull();
  });

  it("ما لا يُعرَف يُرفض ولا يُعاد — فإعادتُه تُنشئ نظيراً مكرّراً", () => {
    const said = planUncertainAr(2);
    expect(said).toContain("لا أعرف إن تمّت الخطوة ٢");
    expect(said).toContain("تحقّق قبل أن تعيدها");
  });
});
