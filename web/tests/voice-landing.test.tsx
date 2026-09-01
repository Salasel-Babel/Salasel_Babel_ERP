/* ═══════════════════════════════════════════════════════════════════════════
   الهبوط: من الكلمة إلى شاشة المستند، والمسوّدة عليها  ·  The landing
   ───────────────────────────────────────────────────────────────────────────
   هذا هو الإثبات الذي كان ناقصاً في الفرع السابق: كان المسار المنطوق ينتهي
   عند نداءٍ خارج (`onDispatch`) ولا أحد يعرف ماذا يقع بعده.

   ويُقاس هنا الطريق كاملاً:
     ١ · يُنطَق الأمر ويُؤكَّد، فتخرج **مسوّدةٌ تسمّي عمليةً منشورة** — لا ترحيلاً.
     ٢ · تُودَع، فتظهر على **الشاشة التي هبط عليها المستخدم**، بقيمها ومصادرها.
     ٣ · وقيمةُ الفترة تصل **حقل النموذج نفسه** في شاشة مسيّر الرواتب.
     ٤ · **ولا شيء رُحِّل**: زرّا الشاشة كما هما، ولا نداءَ ترحيلٍ عبر السلك.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { destinationOf, registeredPaths } from "../src/app/voice-destinations";
import type { RawResponse, Transport } from "../src/api/transport";
import {
  VOICE_INTENTS,
  VoiceConsole,
  dropVoiceDraft,
  stashVoiceDraft,
  type VoiceCaller,
  type VoiceDraftHandoff,
} from "../src/voice";

/* منشأةٌ **تخصّ هذا الإثبات وحده**. ولا يتقاسم إثباتان منشأةً في هذا المستودع
   (‏فخ-132): المنشأة وحدة عزلٍ لا قيمةَ زينة، ومَن يتقاسمها يقرأ أثر جاره. */
const COMPANY = "7c3f19b4-6d20-4a8e-9f51-2b8d40c6a913";
const HEALTH = { apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" };

const CALLER: VoiceCaller = {
  companyId: COMPANY,
  companyNameAr: "سلاسل بابل",
  permittedIntentIds: VOICE_INTENTS.map((intent) => intent.id),
};

/** ما عبر السلك — يُفحَص أنه لا يحمل ترحيلاً. */
const sent: { method: string; url: string }[] = [];

const transport: Transport = ({ method, url }) => {
  sent.push({ method, url });
  const path = url.split("?")[0] ?? url;
  if (path === "/health") {
    return Promise.resolve<RawResponse>({ ok: true, status: 200, json: HEALTH, url });
  }
  if (path === "/api/v1/companies/" + COMPANY + "/payroll-settings") {
    return Promise.resolve<RawResponse>({ ok: true, status: 200, json: { itemCount: 0, items: [] }, url });
  }
  return Promise.resolve<RawResponse>({ ok: false, status: 404, json: null, url });
};

async function mount(path: string): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath: path });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider transport={transport}>
            <RouterProvider router={router} />
          </ApiProvider>
        </QueryClientProvider>
      </LocaleProvider>
    );
  }

  await act(async () => {
    render(<Tree />);
    await router.load();
  });
}

/** يُنطَق الأمر في اللوحة ويُؤكَّد، فيخرج التسليم. */
function speakAndConfirm(transcript: string): VoiceDraftHandoff {
  const handed: VoiceDraftHandoff[] = [];
  render(
    <LocaleProvider i18n={createI18n()} initial="ar">
      <VoiceConsole caller={CALLER} today="2026-08-31" onDraft={(h) => handed.push(h)} />
    </LocaleProvider>
  );
  fireEvent.change(screen.getByTestId("voice-manual-input"), { target: { value: transcript } });
  fireEvent.click(screen.getByTestId("voice-manual-apply"));
  fireEvent.click(screen.getByTestId("voice-confirm"));
  cleanup();

  expect(handed).toHaveLength(1);
  return handed[0]!;
}

beforeEach(() => {
  sent.length = 0;
  dropVoiceDraft();
  globalThis.localStorage.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: COMPANY, book: "MAIN", period: "" })
  );
});

afterEach(() => {
  cleanup();
  dropVoiceDraft();
  globalThis.localStorage.clear();
});

describe("من الكلمة إلى شاشة المستند", () => {
  it("«جهّز مسيّر الرواتب لفترة 2026-08» ⟵ مسوّدةٌ تسمّي draftPayrollRun ووجهتُها /hr/payroll", () => {
    const handoff = speakAndConfirm("جهز مسير الرواتب لفترة 2026-08 اليوم");

    expect(handoff.intentId).toBe("hr.payroll_run.draft");
    expect(handoff.operationId).toBe("draftPayrollRun");
    expect(handoff.fields.find((f) => f.name === "periodCode")?.text).toBe("2026-08");

    const paths = registeredPaths(createAppRouter({ memory: true }));
    expect(destinationOf(handoff.intentId, paths)).toBe("/hr/payroll");
  });

  it("المسوّدة تظهر **على الشاشة التي هبط عليها**، بقيمها ومصادرها", async () => {
    stashVoiceDraft(speakAndConfirm("جهز مسير الرواتب لفترة 2026-08 اليوم"));
    await mount("/hr/payroll");

    const landed = screen.getByTestId("voice-draft-landed");
    expect(landed.getAttribute("data-intent")).toBe("hr.payroll_run.draft");
    expect(landed.getAttribute("data-operation")).toBe("draftPayrollRun");
    expect(screen.getByTestId("voice-landed-value-periodCode").textContent).toBe("2026-08");

    /* ومن لا يرى يُعلَن له بها. */
    expect(landed.getAttribute("aria-live")).toBe("polite");
  });

  it("قيمة الفترة تصل **حقل النموذج نفسه**، ولا شيء يُرحَّل", async () => {
    stashVoiceDraft(speakAndConfirm("جهز مسير الرواتب لفترة 2026-08 اليوم"));
    await mount("/hr/payroll");

    const field: HTMLInputElement = screen.getByTestId("hr-run-period");
    expect(field.value).toBe("2026-08");

    /* ⚠ **ولا نداءَ ترحيلٍ واحد عبر السلك**: الزرّ على الشاشة، ولم يُضغط. */
    expect(sent.some((call) => call.url.includes("/posting"))).toBe(false);
    expect(sent.some((call) => call.method === "POST" && call.url.includes("/payroll-runs"))).toBe(false);
  });

  it("بلا مسوّدةٍ مودَعة لا تظهر اللوحة أصلاً", async () => {
    await mount("/hr/payroll");
    expect(screen.queryByTestId("voice-draft-landed")).toBeNull();
  });
});
