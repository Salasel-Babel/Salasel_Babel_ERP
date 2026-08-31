/* ═══════════════════════════════════════════════════════════════════════════
   لوحة الأمر المنطوق — القراءة المرتدّة تُرى وتُسمع، والتأكيد لا يُتجاوَز.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ يعمل هذا الاختبار في jsdom **بلا webkitSpeechRecognition وبلا
   speechSynthesis** — وهي بالضبط حال المتصفّح بلا رأس المقيسة في هذا المستودع.
   أي أن ما يُقاس هنا هو **المسار الذي يجب أن يعمل لمستخدمٍ لا يسمع**: النصّ
   معروض كاملاً، والأزرار حقيقية، والرفض مكتوب.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { VoiceConsole, VOICE_INTENTS, type VoiceCaller, type VoiceDispatch } from "../src/voice";

function Wrap(props: { children: React.ReactNode }) {
  return (
    <LocaleProvider i18n={createI18n()} initial="ar">
      {props.children}
    </LocaleProvider>
  );
}

/* globals:false في vitest.config.ts، فلا تنظيف تلقائياً. */
afterEach(cleanup);

const CALLER: VoiceCaller = {
  companyId: "0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70",
  companyNameAr: "سلاسل بابل",
  permittedIntentIds: VOICE_INTENTS.map((intent) => intent.id),
};

const RECEIPT = "سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم";
const BALANCE = "كم رصيد العميل مؤسسة الرياض";

function open(props: Partial<React.ComponentProps<typeof VoiceConsole>> = {}) {
  const dispatched: VoiceDispatch[] = [];
  render(
    <Wrap>
      <VoiceConsole caller={CALLER} today="2026-08-31" onDispatch={(d) => dispatched.push(d)} {...props} />
    </Wrap>
  );
  return dispatched;
}

function say(text: string) {
  fireEvent.change(screen.getByTestId("voice-manual-input"), { target: { value: text } });
  fireEvent.click(screen.getByTestId("voice-manual-apply"));
}

describe("<VoiceConsole> — الأقسام الخمسة", () => {
  it("الأقسام الخمسة كلّها معروضة، ولكلٍّ نيّاتُه", () => {
    open();
    for (const id of ["Accounting", "Contracting", "HumanResources", "Inventory", "RealEstate"]) {
      expect(screen.getByTestId("voice-section-" + id)).toBeTruthy();
    }

    /* والصوت متاح في القسم غير المحاسبي أيضاً، لا في المحاسبة وحدها. */
    fireEvent.click(screen.getByTestId("voice-section-Inventory"));
    expect(screen.getByTestId("voice-intent-inventory.count_adjustment.record")).toBeTruthy();

    fireEvent.click(screen.getByTestId("voice-section-RealEstate"));
    expect(screen.getByTestId("voice-intent-realestate.tenant_receipt.record")).toBeTruthy();
  });

  it("القائمة تقول أيُّ أمرٍ يحتاج تأكيداً **قبل** أن يُنطق", () => {
    open();
    const list = screen.getByTestId("voice-intents");
    const gates = [...list.querySelectorAll("[data-gate]")].map((el) => el.getAttribute("data-gate"));
    expect(gates).toContain("confirm");
    expect(gates).toContain("direct");
  });
});

describe("<VoiceConsole> — بوّابة التأكيد", () => {
  it("عملية تُغيّر الحال تُقرأ مرتدّةً **على الشاشة** ولا تُنفَّذ قبل التأكيد", () => {
    const dispatched = open();
    say(RECEIPT);

    /* الملخّص مرئي كاملاً — وهذا شرط أن يستطيع من لا يسمع أن يؤكّد. */
    const readback = screen.getByTestId("voice-readback-text");
    expect(readback.textContent).toContain("سند قبض من عميل");
    expect(readback.textContent).toContain("1000");
    expect(readback.textContent).toContain("قل «تأكيد»");

    /* وهو مُعلَن لقارئ الشاشة — وهذا شرط أن يستطيع من لا يرى أن يؤكّد. */
    expect(readback.getAttribute("aria-live")).toBe("assertive");

    /* ولا شيء نُفِّذ بمجرّد أن فُهم الأمر. */
    expect(dispatched).toHaveLength(0);
    expect(screen.queryByTestId("voice-outcome")).toBeNull();

    fireEvent.click(screen.getByTestId("voice-confirm"));
    expect(dispatched).toHaveLength(1);
    expect(dispatched[0]!.intent.id).toBe("accounting.customer_receipt.record");
    expect(dispatched[0]!.confirmedByHuman).toBe(true);
    expect(screen.getByTestId("voice-outcome").textContent).toContain("أُكِّد الأمر");
  });

  it("الإلغاء لا ينفّذ شيئاً ويُعلن ذلك", () => {
    const dispatched = open();
    say(RECEIPT);
    fireEvent.click(screen.getByTestId("voice-cancel"));

    expect(dispatched).toHaveLength(0);
    expect(screen.getByTestId("voice-outcome").textContent).toContain("أُلغي الأمر");
    expect(screen.queryByTestId("voice-readback")).toBeNull();
  });

  it("«تأكيد» منطوقة تعمل عمل الزرّ — لمن يداه مشغولتان", () => {
    const dispatched = open();
    say(RECEIPT);
    expect(dispatched).toHaveLength(0);
    say("تأكيد");
    expect(dispatched).toHaveLength(1);
  });

  it("الاستعلام لا يُعرض له تأكيد ولا يُوسَم مؤكَّداً", () => {
    const dispatched = open();
    say(BALANCE);

    expect(screen.queryByTestId("voice-readback")).toBeNull();
    fireEvent.click(screen.getByTestId("voice-run"));

    expect(dispatched).toHaveLength(1);
    expect(dispatched[0]!.confirmedByHuman).toBe(false);
    expect(screen.getByTestId("voice-outcome").textContent).toContain("استعلامٌ جاهز");
  });

  it("زرّ التأكيد **معطَّل** ما دامت شريحةٌ لازمة ناقصة، والناقص يُسمّى", () => {
    const dispatched = open();
    say("سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال اليوم");

    const missing = screen.getByTestId("voice-missing");
    expect(missing.textContent).toContain("طريقة القبض");
    expect(missing.getAttribute("role")).toBe("alert");

    const confirm = screen.getByTestId<HTMLButtonElement>("voice-confirm");
    expect(confirm.disabled).toBe(true);
    fireEvent.click(confirm);
    expect(dispatched).toHaveLength(0);
  });

  it("الكمّية بلا وحدة تُرفض بالاسم ولا تُفسَّر بوحدة الأساس", () => {
    open();
    fireEvent.click(screen.getByTestId("voice-section-Inventory"));
    say("سجل جرد الصنف اسمنت كمية عشرين المستودع الرئيسي اليوم");

    expect(screen.getByTestId("voice-faults").textContent).toContain("وحدة الكمّية");
    expect(screen.getByTestId("voice-value-quantity").textContent).toBe("—");
  });

  it("النيّة التي تنتظر قرار المالك تُعرض وتُفهَم ولا تُنفَّذ", () => {
    const dispatched = open();
    fireEvent.click(screen.getByTestId("voice-section-Inventory"));
    say("تسكين القطع الصنف اسمنت كمية خمسة أكياس المستودع الرئيسي من الرف واحد الى الرف اثنين");

    expect(screen.getByTestId("voice-awaiting-owner")).toBeTruthy();
    const confirm = screen.getByTestId<HTMLButtonElement>("voice-confirm");
    expect(confirm.disabled).toBe(true);
    expect(dispatched).toHaveLength(0);
  });

  it("ما لا يفهمه المحرّك يُرفض بجملة «لم أفهم» لا برمز", () => {
    open();
    say("ودّي أطير");

    const refusals = screen.getByTestId("voice-refusals");
    expect(refusals.textContent).toContain("لم أفهم");
    expect(refusals.getAttribute("role")).toBe("alert");
    expect(screen.queryByTestId("voice-understood")).toBeNull();
  });

  it("شركة منطوقة غير المفتوحة تُرفض عند التأكيد", () => {
    const dispatched = open();
    say(RECEIPT + " في شركة الفروع");
    fireEvent.click(screen.getByTestId("voice-confirm"));

    expect(dispatched).toHaveLength(0);
    expect(screen.getByTestId("voice-outcome-refusals").textContent).toContain("الشركة المنطوقة");
  });

  it("ما لا يملكه المتكلّم يُرفض بجملة «لا أملك صلاحية»", () => {
    const dispatched = open({ caller: { ...CALLER, permittedIntentIds: [] } });
    say(BALANCE);
    fireEvent.click(screen.getByTestId("voice-run"));

    expect(dispatched).toHaveLength(0);
    expect(screen.getByTestId("voice-outcome-refusals").textContent).toContain("لا أملك صلاحية");
  });
});

describe("<VoiceConsole> — الوصول", () => {
  it("متصفّح لا ينطق يقول ذلك، ويبقى التأكيد ممكناً بالضغط", () => {
    const dispatched = open();
    expect(screen.getByTestId("voice-silent-browser")).toBeTruthy();

    say(RECEIPT);
    fireEvent.click(screen.getByTestId("voice-confirm"));
    expect(dispatched).toHaveLength(1);
  });

  it("حين ينطق المتصفّح يُنطَق **نفس** النصّ المعروض لا نصّ ثانٍ", () => {
    const spoken: string[] = [];
    class Utterance {
      lang = "";
      rate = 1;
      constructor(public text: string) {}
    }
    vi.stubGlobal("SpeechSynthesisUtterance", Utterance);
    vi.stubGlobal("speechSynthesis", {
      speak: (u: { text: string }) => spoken.push(u.text),
      cancel: () => undefined,
    });

    try {
      open();
      say(RECEIPT);
      expect(spoken).toHaveLength(1);
      expect(spoken[0]).toBe(screen.getByTestId("voice-readback-text").textContent);
    } finally {
      vi.unstubAllGlobals();
    }
  });

  it("لوحة القراءة المرتدّة مجموعةٌ مُسمّاة، وأزرارها أزرارٌ حقيقية", () => {
    open();
    say(RECEIPT);

    const panel = screen.getByTestId("voice-readback");
    expect(panel.getAttribute("role")).toBe("group");
    expect(panel.getAttribute("aria-labelledby")).toBe("vcx-readback-title");
    expect(document.getElementById("vcx-readback-title")).toBeTruthy();

    for (const id of ["voice-confirm", "voice-cancel", "voice-speak-again"]) {
      expect(screen.getByTestId(id).tagName).toBe("BUTTON");
    }

    /* وحقل النصّ البديل مرتبطٌ بتسميته — لا عنصرٌ بلا اسم. */
    const input = screen.getByTestId("voice-manual-input");
    expect(document.querySelector('label[for="' + input.id + '"]')).toBeTruthy();
  });
});

describe("الوصول إلى اللوحة — من كل شاشة لا من شاشةٍ واحدة", () => {
  it("للوحة مسارٌ مُعلَن في الموجّه، وهي مُدرَجة في لوحة الأوامر", async () => {
    const { createAppRouter } = await import("../src/app/router");
    const { SCREENS } = await import("../src/app/shell/sections");

    const router = createAppRouter({ memory: true, initialPath: "/voice" });
    expect(Object.keys(router.routesByPath)).toContain("/voice");

    /* ولوحة الأوامر تُبنى من SCREENS، فوجودُها هناك هو وجودُها في Ctrl+K. */
    expect(SCREENS.map((s) => s.path)).toContain("/voice");
  });

  it("زرّ الصوت الحاضر دائماً يقود إلى اللوحة، فيفي بما يَعِد به", async () => {
    const source = await import("node:fs").then((fs) =>
      fs.readFileSync("src/app/shell/VoiceDock.tsx", "utf8")
    );
    /* حارسٌ نصّي لا رسمٌ للهيكل كلّه: الزرّ يعيش داخل مزوّدَي الموجّه والاستعلام
       معاً، ورسمُهما هنا يُثبت تركيبَ الهيكل لا وعدَ الزرّ. والمقصود إثباتُ أن
       الزرّ **يقود** إلى مسارٍ قائم، وأن ذلك المسار هو /voice. */
    expect(source).toContain('navigate({ to: "/voice" })');
  });
});
