/* ═══════════════════════════════════════════════════════════════════════════
   لوحة الإدخال الصوتي — سلوكها حين يعمل الصوت وحين لا يعمل.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ وهذا الاختبار يعمل في jsdom **بلا webkitSpeechRecognition** — وهي بالضبط
   حال المتصفّح بلا رأس المقيسة في هذا المستودع. أي أن المسار البديل هنا ليس
   ترفاً: هو المسار الوحيد القابل للاختبار آلياً، ولذلك يجب أن يكون **موسوماً**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { VoiceCapture } from "../src/voice";
import type { SpokenIntent } from "../src/voice";

function Wrap(props: { children: React.ReactNode }) {
  return (
    <LocaleProvider i18n={createI18n()} initial="ar">
      {props.children}
    </LocaleProvider>
  );
}

/* globals:false في vitest.config.ts، فلا تنظيف تلقائياً — وبدونه تتراكم
   الشجرات فتصير كل استعلامات testid مزدوجة. */
afterEach(cleanup);

const UTTERANCE = "فاتورة مصروف من مؤسسة النور بمبلغ ألف وخمسمئة ريال وضريبة خمسة عشر بالمئة";

describe("<VoiceCapture>", () => {
  it("الضغط المستمرّ يملأ الحقول من الكلام، ولكل حقل مصدره", () => {
    const commits: SpokenIntent[] = [];
    render(
      <Wrap>
        <VoiceCapture
          simulatedTranscript={UTTERANCE}
          today="2026-08-26"
          onCommit={(intent) => commits.push(intent)}
        />
      </Wrap>
    );

    const button = screen.getByTestId("voice-hold");
    fireEvent.pointerDown(button);

    /* الامتلاء يقع **قبل** الإفلات: هذا هو الأثر المقصود كله. */
    expect(screen.getByTestId("voice-value-gross_total").textContent).toBe("1500");
    expect(screen.getByTestId("voice-value-seller_name").textContent).toBe("مؤسسة النور");
    expect(screen.getByTestId("voice-value-tax_rate").textContent).toBe("0.15");

    fireEvent.pointerUp(button);
    expect(commits).toHaveLength(1);
  });

  it("المصدر السادس «منطوق» يُعاد استعماله ولا يُخترَع مفهوم ثانٍ", () => {
    render(
      <Wrap>
        <VoiceCapture simulatedTranscript={UTTERANCE} today="2026-08-26" />
      </Wrap>
    );
    fireEvent.pointerDown(screen.getByTestId("voice-hold"));

    expect(screen.getByTestId("voice-field-gross_total").getAttribute("data-provenance")).toBe("spoken");
    /* الحدث اقتراحٌ لا نُطق: مصدره «مُستنتَج» وواجبه «قرِّر». */
    expect(screen.getByTestId("voice-field-suggested_event").getAttribute("data-provenance")).toBe("inferred");
    expect(screen.getByTestId("voice-value-suggested_event").textContent).toBe("purchasing.invoice.expense.posted");
    /* النسبة النظامية حين لا تُنطق: مصدرها «من الإعدادات» ويُعرَض. */
    expect(screen.getByTestId("voice-field-issued_on").getAttribute("data-provenance")).toBe("defaulted");
  });

  it("المقطع المُحاكى يحمل وسماً ظاهراً على الشاشة", () => {
    render(
      <Wrap>
        <VoiceCapture simulatedTranscript={UTTERANCE} today="2026-08-26" />
      </Wrap>
    );
    expect(screen.queryByTestId("voice-simulated")).toBeNull();
    fireEvent.pointerDown(screen.getByTestId("voice-hold"));

    const marker = screen.getByTestId("voice-simulated");
    expect(marker.textContent).toContain("مُحاكاة");
    /* ووسمٌ في الوسم وحده لا يكفي: العنصر يُعلَن حالةً كي يقرأه قارئ الشاشة. */
    expect(marker.getAttribute("role")).toBe("status");
  });

  it("حين يتعذّر التفريغ يُسمّى السبب ويُعرَض بديل مُعلَن", () => {
    /* jsdom بلا الواجهة أصلاً — لا محاكاة ولا تزييف. */
    render(
      <Wrap>
        <VoiceCapture today="2026-08-26" />
      </Wrap>
    );

    const notice = screen.getByTestId("voice-unavailable");
    expect(notice.textContent).toContain("متصفّح");
    expect(screen.getByTestId("voice-hold").hasAttribute("disabled")).toBe(true);

    const input = screen.getByTestId("voice-manual-input");
    fireEvent.change(input, { target: { value: UTTERANCE } });
    fireEvent.click(screen.getByTestId("voice-manual-apply"));

    expect(screen.getByTestId("voice-value-gross_total").textContent).toBe("1500");
    expect(screen.getByTestId("voice-simulated")).not.toBeNull();
  });

  it("ما لا يُسمَع لا يُخترَع: كلامٌ بلا مبلغ يُعلن غيابه ولا يملأ رقماً", () => {
    render(
      <Wrap>
        <VoiceCapture simulatedTranscript="فاتورة مصروف من مؤسسة النور" today="2026-08-26" />
      </Wrap>
    );
    fireEvent.pointerDown(screen.getByTestId("voice-hold"));

    expect(screen.getByTestId("voice-value-gross_total").textContent).toBe("—");
    expect(screen.getByTestId("voice-faults").textContent).toContain("لا مبلغ");
  });

  it("النموذج — إن رُكِّب — يُسأل بعد الامتلاء لا قبله", async () => {
    const resolve = vi.fn(async (): Promise<SpokenIntent> => ({
      values: [{ field: "gross_total", text: "1725", provenance: "inferred", confidence: 0.6 }],
      faults: [],
    }));

    render(
      <Wrap>
        <VoiceCapture simulatedTranscript={UTTERANCE} today="2026-08-26" resolveIntent={resolve} />
      </Wrap>
    );

    const button = screen.getByTestId("voice-hold");
    fireEvent.pointerDown(button);
    /* قبل النموذج: الشاشة ممتلئة سلفاً بالقارئ الحتمي. */
    expect(screen.getByTestId("voice-value-gross_total").textContent).toBe("1500");

    fireEvent.pointerUp(button);
    await vi.waitFor(() =>
      expect(screen.getByTestId("voice-value-gross_total").textContent).toBe("1725")
    );
    expect(resolve).toHaveBeenCalledWith(UTTERANCE);
  });

  it("لا شيء هنا حقيقة محاسبية — والنصّ يقول ذلك على الشاشة", () => {
    render(
      <Wrap>
        <VoiceCapture today="2026-08-26" />
      </Wrap>
    );
    expect(screen.getByTestId("voice-not-a-fact").textContent).toContain("مسوّدة");
  });
});
