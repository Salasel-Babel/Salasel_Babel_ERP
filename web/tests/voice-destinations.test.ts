/* ═══════════════════════════════════════════════════════════════════════════
   أين تهبط المسوّدة المنطوقة — مقيساً على الموجّه القائم، لا على جدولٍ يُصدَّق.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ جدولُ الوجهات نصٌّ يُكتب بيد، والمسارات تُسجَّل في مكانٍ آخر. ومسارٌ يُعاد
   تسميته في `router.tsx` يجعل الجدول يشير إلى لا شيء — **بصمت**، لأن الوجهة
   الغائبة سلوكٌ مشروع في هذه الطبقة (شاشةٌ لم تهبط بعد). فيُقاس هنا الاتجاهان:
   لا مسار في الجدول إلا وهو مسجَّل، **ولا قسمٌ بُنيت شاشاتُه ونيّاتُه بلا وجهة**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { describe, expect, it } from "vitest";
import { createAppRouter } from "../src/app/router";
import { VOICE_DESTINATIONS, destinationOf, registeredPaths } from "../src/app/voice-destinations";
import { VOICE_INTENTS } from "../src/voice";

const paths = registeredPaths(createAppRouter({ memory: true }));

describe("وجهات المسوّدة المنطوقة", () => {
  it("قراءةُ مسارات الموجّه ليست ضامرة", () => {
    /* حارس لا فراغ: قائمةٌ فارغة تجعل كل ما تحته يمرّ على «لا شاشة هبطت». */
    expect(paths.length).toBeGreaterThanOrEqual(10);
    expect(paths).toContain("/voice");
  });

  it("كل مسارٍ في جدول الوجهات مسجَّل فعلاً في الموجّه", () => {
    const declared = [...new Set(Object.values(VOICE_DESTINATIONS))];
    expect(declared.length).toBeGreaterThanOrEqual(10);

    for (const path of declared) {
      expect(paths, path + " في الجدول وليس في الموجّه").toContain(path);
    }
  });

  it("كل نيّةٍ في الأقسام الأربعة المبنيّة تهبط على شاشة", () => {
    const built = ["Contracting", "HumanResources", "Inventory", "RealEstate"];
    let landed = 0;

    for (const intent of VOICE_INTENTS) {
      if (!built.includes(intent.section)) continue;
      /* النيّة التي تنتظر قراراً لا تُنفَّذ أصلاً، فلا وجهة لها. */
      if (intent.status === "AwaitingOwnerDecision") continue;

      const to = destinationOf(intent.id, paths);
      expect(to, intent.id + " بلا وجهة").not.toBeNull();
      landed++;
    }

    expect(landed).toBeGreaterThanOrEqual(29);
  });

  it("قسم المحاسبة بلا وجهةٍ واحدة — والغياب مُعلَن لا مسكوتٌ عنه", () => {
    /* شاشاتُ مستندات المحاسبة لم تهبط في أي فرع (خطة الصوت §13.4). واللوحة
       تقول ذلك نصّاً على الشاشة، ولا تقفز إلى مسارٍ غير مسجَّل. */
    const accounting = VOICE_INTENTS.filter((intent) => intent.section === "Accounting");
    expect(accounting.length).toBeGreaterThanOrEqual(13);

    for (const intent of accounting) {
      expect(destinationOf(intent.id, paths), intent.id).toBeNull();
    }
  });

  it("مسارٌ غير مسجَّل لا يُقفَز إليه ولو كان في الجدول", () => {
    expect(destinationOf("hr.payroll_run.draft", ["/voice"])).toBeNull();
    expect(destinationOf("hr.payroll_run.draft", paths)).toBe("/hr/payroll");
  });
});
