/* ═══════════════════════════════════════════════════════════════════════════
   لوحةُ الخطّة — تُرى كما تُسمع، وجملةُ المالك من طرفها إلى طرفها.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ يعمل هذا الاختبار في jsdom بلا نُطقٍ وبلا ميكروفون — أي في حال **من لا
   يسمع**: النصّ كلُّه معروض، والأزرار حقيقية، والحالُ نصٌّ لا لون.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { VoiceConsole, VOICE_INTENTS, type VoiceCaller } from "../src/voice";
import type { VoiceDraftHandoff } from "../src/voice/handoff";

function Wrap(props: { children: React.ReactNode }) {
  return (
    <LocaleProvider i18n={createI18n()} initial="ar">
      {props.children}
    </LocaleProvider>
  );
}

afterEach(cleanup);

const CALLER: VoiceCaller = {
  companyId: "0d0e2b7a-9c1f-4a55-9d2e-6f8a1b3c5d70",
  companyNameAr: "سلاسل بابل",
  permittedIntentIds: VOICE_INTENTS.map((intent) => intent.id),
};

/** جملة المالك كما نطقها على الخادم الحيّ، حرفاً. */
const OWNER =
  "سجل سند قبض من شركة المسار الامثل فان لم تجدها انشيء لها حسابا ثم سند قبض بقيمة 20000 ريال سعودي بتاريخ اليوم طبعا";

function open() {
  const drafts: VoiceDraftHandoff[] = [];
  render(
    <Wrap>
      <VoiceConsole caller={CALLER} today="2026-08-31" onDraft={(h) => drafts.push(h)} />
    </Wrap>
  );
  return drafts;
}

function say(text: string) {
  fireEvent.change(screen.getByTestId("voice-manual-input"), { target: { value: text } });
  fireEvent.click(screen.getByTestId("voice-manual-apply"));
}

describe("<VoiceConsole> — الخطّة المنطوقة", () => {
  it("جملة المالك تفتح خطّةً لا أمراً واحداً، ويظهر توجيهُها نصّاً", () => {
    open();
    say(OWNER);

    expect(screen.getByTestId("voice-plan")).toBeTruthy();
    const text = screen.getByTestId("voice-plan-readback-text").textContent ?? "";
    expect(text).toContain("(١)");
    expect(text).toContain("(٢)");
    expect(text).toContain("ولا يُرحَّل شيء بالصوت");
  });

  it("التوجيه لوحةٌ لها دورٌ مُعلَن — **ولا زرّ فيه**، فلا يُتعلَّم الضغط على ما لم يُقرأ", () => {
    open();
    say(OWNER);

    const orientation = screen.getByTestId("voice-plan-readback-text").closest("div")!;
    expect(orientation.getAttribute("role")).toBe("group");
    expect(orientation.querySelectorAll("button").length).toBe(0);
  });

  it("للأصمّ: الخطوات قائمةٌ مرقّمة، وحالُ كلٍّ **نصٌّ** لا لون، والعاملةُ مُعلَّمة", () => {
    open();
    say(OWNER);

    const first = screen.getByTestId("voice-plan-step-1");
    expect(first.getAttribute("aria-current")).toBe("step");
    expect(first.getAttribute("data-state")).toBe("pending");
    /* الحال مكتوبةٌ نصّاً — لا يقرؤها اللون وحده. */
    expect(screen.getByTestId("voice-plan-state-1").textContent).toBe("لم تبدأ");
    expect(screen.getByTestId("voice-plan-step-2").getAttribute("aria-current")).toBeNull();
  });

  it("للأعمى: إعلانٌ لطيف عن الخطوة العاملة، والملخّص المرتدّ يبقى حازماً", () => {
    open();
    say(OWNER);

    const announce = screen.getByTestId("voice-plan-announce");
    expect(announce.getAttribute("aria-live")).toBe("polite");
    expect(announce.textContent).toContain("الخطوة ١ من ٢");

    fireEvent.click(screen.getByTestId("voice-plan-not-found"));
    expect(screen.getByTestId("voice-plan-step-readback-text").getAttribute("aria-live")).toBe("assertive");
  });

  it("اسمُ العميل نظيفٌ من الشرط، وما سقط منه **يُعرض** ولا يُطمَر", () => {
    open();
    say(OWNER);

    expect(screen.getByTestId("voice-plan-value-name").textContent).toBe("شركة المسار الامثل");
    expect(screen.getByTestId("voice-plan-dropped-name").textContent).toContain("فان لم تجدها انشيء لها حسابا");
  });

  it("ما تطلبه الشاشةُ ولا يطلبه الصوت — مُسمّى على الشاشة وفي التوجيه", () => {
    open();
    say(OWNER);

    const asks = screen.getByTestId("voice-plan-screen-asks").textContent ?? "";
    expect(asks).toContain("رمز العميل");
    expect(asks).toContain("حدّ الائتمان");
    expect(asks).toContain("مهلة السداد");
  });

  it("الخطّة كاملةً بالضغط: إنشاءُ العميل، ثم السؤال عمّا ينقص، ثم مسوّدةُ السند", () => {
    const drafts = open();
    say(OWNER);

    /* ١ — لم يجده، فتُنشأ. */
    fireEvent.click(screen.getByTestId("voice-plan-not-found"));
    fireEvent.click(screen.getByTestId("voice-plan-confirm-conditional"));
    expect(screen.getByTestId("voice-plan-step-1").getAttribute("data-state")).toBe("handedOff");
    expect(drafts[0]!.operationId).toBe("addCustomer");

    fireEvent.click(screen.getByTestId("voice-plan-step-done"));

    /* ٢ — ينقصها طريقة القبض. **والخطّة لا تُعفي من شريحة.** */
    expect(screen.getByTestId("voice-plan-step-2").getAttribute("data-state")).toBe("asking");
    expect(screen.getByTestId("voice-plan-asking").textContent).toContain("ينقصني طريقة القبض");

    fireEvent.change(screen.getByTestId("voice-plan-answer"), { target: { value: "نقد" } });
    fireEvent.click(screen.getByTestId("voice-plan-answer-apply"));

    fireEvent.click(screen.getByTestId("voice-plan-confirm"));
    expect(drafts[1]!.operationId).toBe("draftCustomerReceipt");
    const field = (name: string) => drafts[1]!.fields.find((f) => f.name === name)!.text;
    expect(field("customer")).toBe("شركة المسار الامثل");
    expect(field("amount")).toBe("20000");
    expect(field("method")).toBe("نقد");
    expect(field("receivedOn")).toBe("2026-08-31");

    fireEvent.click(screen.getByTestId("voice-plan-step-done"));

    /* ودفترُ الخطّة يقول ما تمّ. */
    expect(screen.getByTestId("voice-plan-ledger-text").textContent).toContain("الخطوة ١ تمّت");
    expect(screen.getByTestId("voice-plan-ledger-text").textContent).toContain("الخطوة ٢ تمّت");
  });

  it("إن سقطت الخطوة الثانية: يُقال ما تمّ وما لم يتمّ — **ولا حذف يُقترح**", () => {
    open();
    say(OWNER);

    fireEvent.click(screen.getByTestId("voice-plan-not-found"));
    fireEvent.click(screen.getByTestId("voice-plan-confirm-conditional"));
    fireEvent.click(screen.getByTestId("voice-plan-step-done"));

    /* ثم يترك الإنسانُ شاشة السند بلا أن يكملها. */
    fireEvent.change(screen.getByTestId("voice-plan-answer"), { target: { value: "نقد" } });
    fireEvent.click(screen.getByTestId("voice-plan-answer-apply"));
    fireEvent.click(screen.getByTestId("voice-plan-confirm"));
    fireEvent.click(screen.getByTestId("voice-plan-step-abandon"));

    const ledger = screen.getByTestId("voice-plan-ledger-text").textContent ?? "";
    expect(ledger).toContain("الخطوة ١ تمّت");
    expect(ledger).toContain("الخطوة ٢ تُركت");
    expect(ledger).toContain("ولا شيء يُحذف");
    expect(screen.getByTestId("voice-plan-step-2").getAttribute("data-state")).toBe("abandoned");
  });

  it("جملةٌ بلا شرطٍ تبقى أمراً واحداً — فلا تنكسر جملةٌ تعمل اليوم", () => {
    open();
    say("سجل سند قبض من العميل مؤسسة الرياض بمبلغ ألف ريال نقد اليوم");

    expect(screen.queryByTestId("voice-plan")).toBeNull();
    expect(screen.getByTestId("voice-understood")).toBeTruthy();
  });

  it("وشاشةُ المستند لم تهبط بعد — يُقال نصّاً ولا يُخترَع انتقال", () => {
    open();
    say(OWNER);
    fireEvent.click(screen.getByTestId("voice-plan-not-found"));
    fireEvent.click(screen.getByTestId("voice-plan-confirm-conditional"));

    expect(screen.getByTestId("voice-plan-destination").getAttribute("data-destination")).toBe("");
    expect(screen.getByTestId("voice-plan-destination").textContent).toContain("لم تهبط بعد");
  });
});
