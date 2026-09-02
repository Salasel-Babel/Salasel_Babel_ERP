/* ═══════════════════════════════════════════════════════════════════════════
   شاشات الموارد البشرية الأربع الجديدة — حرّاسها
   The four new HR screens — their guards
   ───────────────────────────────────────────────────────────────────────────
   ستّة أشياء تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحص:

     ١ · **القائمتان تتّفقان.** ملاحةُ الهيكل نسخةٌ ثانية من `SCREENS` ولا
         شيء يقارنهما، فشاشةٌ في واحدةٍ دون الأخرى تُفتح بلوحة الأوامر ولا
         يراها من يقرأ الملاحة — وهو أسوأ من رابطٍ مكسور لأنه لا يُشتكى منه.
     ٢ · **المال يغادر السلك نصّاً.** لا `number` ولا `parseFloat` في أي خطوة،
         والخانة الرابعة هي ما يفقده العائم.
     ٣ · **المسوّدة ثمّ الترحيل خطوتان**، وإعادةُ الترحيل تقول الحقيقة ولا
         تُظهر نجاحاً ثانياً على عملٍ لم يقع.
     ٤ · **صفر انحرافٍ ليس تقريراً نظيفاً** ما لم يتطابق شيء: التقرير الذي لم
         يقرأ شيئاً يُعلَن كذلك ولا يُلبَس ثوب النجاح.
     ٥ · **المجموعة المغلقة تُقرأ من العقد**، ولكل عضوٍ فيها كلمةٌ في اللغات
         الأربع — وعضوٌ يُضاف بلا كلمة يُحمِّر هذا الحارس بدل أن يُعرض بلا اسم.
     ٦ · **صفوف الحقول تقف على مسارات وعاءٍ مُعلَن.** الإصلاح البنيوي في
         ADR-0067 يعمل على أوعيةٍ بأسمائها، فصفٌّ في وعاءٍ لم يُسجَّل يعود إلى
         عطل «كل حقلٍ يقيس نفسه منفرداً» بلا أن يسقط اختبار.

   ‏**ولا بيان شخصي في هذا الملفّ**: لا اسم، ولا رقم هوية، ولا آيبان — ولا
   حاجة إليها أصلاً، فالمستندات الأربعة لا تحمل في العقد حقلاً واحداً منها.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import { SCREENS } from "../src/app/shell/sections";
import { DIVERGENCE_REASONS, keySegment } from "../src/screens/hr/contract";
import { resetHrFocus } from "../src/screens/hr/focus";
import type { RawResponse, Transport } from "../src/api/transport";

const COMPANY = "11111111-1111-1111-1111-111111111111";
const BASE = "/api/v1/companies/" + COMPANY;
const HEALTH = { apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" };

/** مبلغٌ بأربع منازل، خانتُه الأخيرة هي ما يفقده العائم. */
const EXACT_WIRE = "1000000000000.4013";

/** رمزٌ معتم — وهو كل ما يعرفه الدفتر عن إنسان. */
const CODE = "emp-0000000000000042";

const ADVANCE_ID = "a0000000-0000-0000-0000-000000000001";
const SI_ID = "p0000000-0000-0000-0000-000000000001";

const ADVANCE = {
  id: ADVANCE_ID,
  number: "ADV-2026-0001",
  employeeId: "e0000000-0000-0000-0000-000000000001",
  employeeCode: CODE,
  amount: "6000.0000",
  outstandingAmount: "4000.0000",
  issuedOn: "2026-06-01",
  settlementMethod: "bank",
  treasuryPartyId: "BANK-0001",
  state: "DRAFT",
  instalments: [
    { lineNo: 1, periodCode: "2026-07", amount: "2000.0000", consumedByPayslipId: "s0000000-0000-0000-0000-000000000001" },
    { lineNo: 2, periodCode: "2026-08", amount: "2000.0000", consumedByPayslipId: null },
    { lineNo: 3, periodCode: "2026-09", amount: "2000.0000", consumedByPayslipId: null },
  ],
};

function siPayment(over: { state: string; entryId: string | null; alreadyPosted: boolean }) {
  return {
    id: SI_ID,
    number: "GOSI-2026-06",
    periodCode: "2026-06",
    amount: "18400.0000",
    accruedForPeriod: "18250.0000",
    paidOn: "2026-07-05",
    settlementMethod: "bank",
    treasuryPartyId: "BANK-0001",
    ...over,
  };
}

function reconciliation(over: Record<string, unknown>) {
  return { asOf: "2026-06-30", isReconciled: true, matchedDocuments: 0, divergences: [], ...over };
}

const DIVERGENCE = {
  documentType: "hr.payslip",
  documentId: "s0000000-0000-0000-0000-000000000001",
  partyId: CODE,
  controlEffect: "8000.0000",
  subledgerEffect: "7500.0000",
  divergence: "500.0000",
  reasonCode: "amount_mismatch",
};

/* ══════════════════════════════════════════════════════════ أدوات ═════ */

interface Recorded {
  method: string;
  url: string;
  body?: unknown;
}

function stub(options: {
  routes: Readonly<Record<string, unknown>>;
  sent?: Recorded[];
}): Transport {
  return ({ method, url, body }) => {
    options.sent?.push({ method, url, body });
    const at = url.split("?")[0] ?? url;
    const found = options.routes[method + " " + at];
    if (found === undefined) {
      return Promise.resolve<RawResponse>({ ok: false, status: 404, json: null, url });
    }
    return Promise.resolve<RawResponse>({ ok: true, status: 200, json: found, url });
  };
}

async function mount(options: { path: string; transport: Transport; locale?: string }): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath: options.path });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial={options.locale ?? "ar"}>
        <QueryClientProvider client={client}>
          <ApiProvider transport={options.transport}>
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

async function click(element: Element): Promise<void> {
  await act(async () => {
    (element as HTMLElement).click();
    await Promise.resolve();
  });
}

function setNativeValue(element: HTMLInputElement | HTMLSelectElement, value: string): void {
  const proto = Object.getPrototypeOf(element) as object;
  /* الواصف على **النموذج الأصلي** عمداً: React يضع مُتعقِّباً على النسخة
     نفسها، والكتابة المباشرة تُحدِّث ذاكرته فيظنّ أن القيمة لم تتغيّر. */
  // eslint-disable-next-line @typescript-eslint/unbound-method
  const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
  if (setter) setter.call(element, value);
  else element.value = value;
}

async function type(element: HTMLInputElement, value: string): Promise<void> {
  await act(async () => {
    setNativeValue(element, value);
    element.dispatchEvent(new Event("input", { bubbles: true }));
    await Promise.resolve();
  });
}

beforeEach(() => {
  resetHrFocus();
  globalThis.localStorage.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: COMPANY, book: "MAIN", period: "" })
  );
});

afterEach(() => {
  cleanup();
  globalThis.localStorage.clear();
});

/* ═══════════════════════════════════════════════════════════════════════
   ١ · القائمتان تتّفقان
   ═══════════════════════════════════════════════════════════════════════ */
describe("الملاحة اليدوية ونسختها في العقد", () => {
  it("كل شاشة موارد بشرية في SCREENS لها رابطٌ في قائمة الملاحة اليدوية — وهذا هو الحارس المفقود", async () => {
    await mount({ path: "/hr/pay-components", transport: stub({ routes: { "GET /health": HEALTH } }) });
    const nav = document.querySelector(".app-side");
    expect(nav).not.toBeNull();
    const hrefs = [...(nav?.querySelectorAll("a[href]") ?? [])].map((a) => a.getAttribute("href"));
    const declared = SCREENS.filter((s) => s.section === "hr").map((s) => s.path);
    expect(declared.length).toBe(8);
    for (const target of declared) expect(hrefs).toContain(target);
  });

  it("والشريط داخل القسم يحمل الثماني نفسها — لا سابعة ولا تاسعة", async () => {
    await mount({ path: "/hr/social-insurance", transport: stub({ routes: { "GET /health": HEALTH } }) });
    const tabs = await screen.findByTestId("hr-tabs");
    const inside = [...tabs.querySelectorAll("a[href]")].map((a) => a.getAttribute("href"));
    const declared = SCREENS.filter((s) => s.section === "hr").map((s) => s.path);
    expect([...inside].sort()).toEqual([...declared].sort());
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · المال نصٌّ على السلك
   ═══════════════════════════════════════════════════════════════════════ */
describe("المال نصّ", () => {
  it("مبلغ السلفة وأقساطها يغادران السلك نصّاً بايتاً ببايت — لا رمزاً رقمياً", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/hr/advances-deductions",
      transport: stub({
        routes: { "GET /health": HEALTH, ["POST " + BASE + "/employee-advances"]: ADVANCE },
        sent,
      }),
    });

    await type(await screen.findByTestId<HTMLInputElement>("hr-advance-number"), "ADV-2026-0001");
    await type(screen.getByTestId<HTMLInputElement>("hr-advance-employee"), ADVANCE.employeeId);
    await type(screen.getByTestId<HTMLInputElement>("hr-advance-amount"), EXACT_WIRE);
    await type(screen.getByTestId<HTMLInputElement>("hr-advance-issued"), "2026-06-01");
    await type(screen.getByTestId<HTMLInputElement>("hr-advance-treasury"), "BANK-0001");
    await type(screen.getByTestId<HTMLInputElement>("hr-instalment-period"), "2026-07");
    await type(screen.getByTestId<HTMLInputElement>("hr-instalment-amount"), EXACT_WIRE);

    await click(screen.getByTestId("hr-advance-submit"));

    const posted = sent.find((r) => r.method === "POST");
    expect(posted).toBeDefined();
    const body = posted?.body as { amount: unknown; instalments: { amount: unknown }[] };
    expect(typeof body.amount).toBe("string");
    expect(body.amount).toBe(EXACT_WIRE);
    expect(typeof body.instalments[0]?.amount).toBe("string");
    expect(body.instalments[0]?.amount).toBe(EXACT_WIRE);
    /* ولا مجموعَ أقساطٍ يُحسب في المتصفّح ويُرسَل حقلاً ثالثاً. */
    expect(Object.keys(body as object)).not.toContain("instalmentTotal");
  });

  it("مبلغٌ لا يطابق نحو المال المنشور يُوسَم خطأً ولا يُرسَل", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/hr/social-insurance",
      transport: stub({ routes: { "GET /health": HEALTH }, sent }),
    });
    const amount = await screen.findByTestId<HTMLInputElement>("hr-si-amount");
    await type(amount, "18,400.00");
    expect(amount.getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByTestId<HTMLButtonElement>("hr-si-draft").disabled).toBe(true);
    expect(sent.filter((r) => r.method === "POST").length).toBe(0);
  });

  it("المستحقّ والمسدَّد يقفان متجاورين، ولا ثالثَ بينهما محسوبٌ في المتصفّح", async () => {
    await mount({
      path: "/hr/social-insurance",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/social-insurance-payments/" + SI_ID]: siPayment({
            state: "DRAFT",
            entryId: null,
            alreadyPosted: false,
          }),
        },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("hr-si-lookup-id"), SI_ID);
    await click(screen.getByTestId("hr-si-open"));

    const paid = await screen.findByTestId("hr-si-amount-out");
    const accrued = screen.getByTestId("hr-si-accrued");
    /* نصّ السلك باقٍ بايتاً ببايت في سمة كل مبلغ: العرض تقريبٌ مُعلَن. */
    expect(paid.querySelector("[title]")?.getAttribute("title")).toBe("18400.0000");
    expect(accrued.querySelector("[title]")?.getAttribute("title")).toBe("18250.0000");
    /* ولا فارقَ محسوب: «150» لا يظهر في أي مكان على هذه الشاشة. */
    expect(screen.getByTestId("hr-si-card").textContent ?? "").not.toContain("150.00");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · المسوّدة ثمّ الترحيل — وإعادةُ الترحيل تقول الحقيقة
   ═══════════════════════════════════════════════════════════════════════ */
describe("المسوّدة ثمّ الترحيل", () => {
  it("الإنشاء لا يُرحّل، والترحيل فعلٌ ثانٍ", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/hr/social-insurance",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["POST " + BASE + "/social-insurance-payments"]: siPayment({
            state: "DRAFT",
            entryId: null,
            alreadyPosted: false,
          }),
          ["POST " + BASE + "/social-insurance-payments/" + SI_ID + "/posting"]: siPayment({
            state: "POSTED",
            entryId: "j0000000-0000-0000-0000-00000000000a",
            alreadyPosted: false,
          }),
        },
        sent,
      }),
    });

    await type(await screen.findByTestId<HTMLInputElement>("hr-si-number"), "GOSI-2026-06");
    await type(screen.getByTestId<HTMLInputElement>("hr-si-period"), "2026-06");
    await type(screen.getByTestId<HTMLInputElement>("hr-si-amount"), "18400.0000");
    await type(screen.getByTestId<HTMLInputElement>("hr-si-paid-on"), "2026-07-05");
    await type(screen.getByTestId<HTMLInputElement>("hr-si-treasury"), "BANK-0001");

    await click(screen.getByTestId("hr-si-draft"));
    await screen.findByTestId("hr-si-card");
    /* لا ترحيل وقع بعد — ولا زرَّ يجمع الفعلين في نقرة. */
    expect(sent.filter((r) => r.url.endsWith("/posting")).length).toBe(0);
    expect(screen.queryByTestId("hr-si-receipt")).toBeNull();

    await click(screen.getByTestId("hr-si-post"));
    const receipt = await screen.findByTestId("hr-si-receipt");
    expect(receipt.getAttribute("data-already")).toBe("false");
    expect(sent.filter((r) => r.url.endsWith("/posting")).length).toBe(1);
  });

  it("ترحيلٌ ثانٍ: لا نجاحَ ثانٍ بل «لم يقع ترحيلٌ جديد» — وإعادةُ الإيصال ليست خطأً", async () => {
    await mount({
      path: "/hr/social-insurance",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/social-insurance-payments/" + SI_ID]: siPayment({
            state: "POSTED",
            entryId: "j0000000-0000-0000-0000-00000000000a",
            alreadyPosted: false,
          }),
          ["POST " + BASE + "/social-insurance-payments/" + SI_ID + "/posting"]: siPayment({
            state: "POSTED",
            entryId: "j0000000-0000-0000-0000-00000000000a",
            alreadyPosted: true,
          }),
        },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("hr-si-lookup-id"), SI_ID);
    await click(screen.getByTestId("hr-si-open"));
    await click(await screen.findByTestId("hr-si-post"));

    const receipt = await screen.findByTestId("hr-si-receipt");
    await waitFor(() => expect(receipt.getAttribute("data-already")).toBe("true"));
    expect(receipt.textContent).toContain("لم يقع ترحيلٌ جديد");
    expect(screen.getByTestId("hr-si-already").textContent).toContain("نعم");
    /* ولا لوحةَ خطأ: إعادةُ الترحيل حالةٌ صحيحة لا رفض. */
    expect(document.querySelector('[role="alert"]')).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ · صفر انحرافٍ ليس تقريراً نظيفاً
   ═══════════════════════════════════════════════════════════════════════ */
describe("المطابقة", () => {
  it("صفرُ انحرافٍ مع صفرِ تطابقٍ يُعلَن «لم يُفحص شيء» ولا يُلبَس ثوب النجاح", async () => {
    await mount({
      path: "/hr/subledger-reconciliation",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/employee-subledger-reconciliation"]: reconciliation({}),
        },
      }),
    });
    await click(await screen.findByTestId("hr-recon-run"));
    await screen.findByTestId("hr-recon-nothing");
    expect(screen.getByTestId("hr-recon-matched").querySelector(".num")?.textContent).toBe("0");
    expect(screen.queryByTestId("hr-recon-clean")).toBeNull();
  });

  it("صفرُ انحرافٍ مع تطابقٍ مقروء يُعلَن نظيفاً — والفرق بينهما هو عدد ما تطابق", async () => {
    await mount({
      path: "/hr/subledger-reconciliation",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/employee-subledger-reconciliation"]: reconciliation({ matchedDocuments: 42 }),
        },
      }),
    });
    await click(await screen.findByTestId("hr-recon-run"));
    await screen.findByTestId("hr-recon-clean");
    expect(screen.queryByTestId("hr-recon-nothing")).toBeNull();
    expect(screen.getByTestId("hr-recon-verdict").getAttribute("data-state")).toBe("posted");
  });

  it("سطر الانحراف يحمل طرفيه ورمزه المعتم — ولا اسمَ ولا رقمَ حساب", async () => {
    await mount({
      path: "/hr/subledger-reconciliation",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/employee-subledger-reconciliation"]: reconciliation({
            isReconciled: false,
            matchedDocuments: 3,
            divergences: [DIVERGENCE],
          }),
        },
      }),
    });
    await click(await screen.findByTestId("hr-recon-run"));
    const table = await screen.findByTestId("hr-recon-table");
    expect(table.textContent).toContain(CODE);
    /* والسبب مُسمّى بالكلمات **ومعه رمزه** — فترجمةٌ لم تلحق عضواً جديداً لا تُخفيه. */
    expect(screen.getByTestId("hr-recon-reason-code").textContent).toBe("amount_mismatch");
    expect(screen.getByTestId("hr-recon-reason-word").textContent).not.toBe("amount_mismatch");
    /* ولا حقلَ حسابٍ في العقد أصلاً — الشاشة لا تسمّي حساباً لأن المصفوفة هي التي تقرّر. */
    expect(Object.keys(SCHEMAS.HrReconciliationDivergence?.fields ?? {})).not.toContain("accountCode");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٥ · المجموعة المغلقة تُقرأ من العقد، ولكل عضوٍ كلمة في اللغات الأربع
   ═══════════════════════════════════════════════════════════════════════ */
describe("أسباب الانحراف — من العقد لا من قائمةٍ مكتوبة", () => {
  it("الأعضاء الأربعة يأتون من المخطّط المُولَّد", () => {
    expect([...DIVERGENCE_REASONS].sort()).toEqual([
      "amount_mismatch",
      "missing_in_control",
      "missing_in_subledger",
      "posting_unresolved",
    ]);
    expect(SCHEMAS.HrReconciliationDivergence?.fields.reasonCode?.e).toEqual([...DIVERGENCE_REASONS]);
  });

  it("لكل عضوٍ كلمةٌ متمايزة في اللغات الأربع — وعضوٌ بلا كلمة يُحمِّر هذا الحارس", () => {
    const i18n = createI18n();
    for (const code of ["ar", "en", "hi", "ur"]) {
      const flat = i18n.messages(code);
      const words = new Set<string>();
      for (const member of DIVERGENCE_REASONS) {
        const key = "hr.recon.reason." + keySegment(member);
        const value = flat[key];
        expect(typeof value, code + " ← " + key).toBe("string");
        words.add(typeof value === "string" ? value : "");
      }
      /* ومتمايزة: عضوان بكلمةٍ واحدة يُقرآن سبباً واحداً. */
      expect(words.size, code).toBe(DIVERGENCE_REASONS.length);
    }
  });

  it("محوّل مقطع المفتاح قاعدةٌ واحدة لا قائمة", () => {
    expect(keySegment("amount_mismatch")).toBe("amountMismatch");
    expect(keySegment("missing_in_control")).toBe("missingInControl");
    expect(keySegment("posting_unresolved")).toBe("postingUnresolved");
    /* وعضوٌ بلا شرطة يعبر كما هو. */
    expect(keySegment("earning")).toBe("earning");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٦ · لا وسمَ نظاميّ الأثر يُختار عن المحاسب
   ═══════════════════════════════════════════════════════════════════════ */
describe("وسما المكوّن", () => {
  it("الوسمان يبدآن بلا اختيار، والتعريف معطَّل حتى يُقال فيهما «نعم» أو «لا»", async () => {
    await mount({
      path: "/hr/pay-components",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/pay-components"]: { itemCount: 0, items: [] } },
      }),
    });
    const wage = await screen.findByTestId<HTMLSelectElement>("hr-component-wage");
    const eos = screen.getByTestId<HTMLSelectElement>("hr-component-eos");
    expect(wage.value).toBe("");
    expect(eos.value).toBe("");

    await type(screen.getByTestId<HTMLInputElement>("hr-component-code"), "HOUSING");
    await type(screen.getByTestId<HTMLInputElement>("hr-component-name-ar"), "بدل سكن");
    expect(screen.getByTestId<HTMLButtonElement>("hr-component-submit").disabled).toBe(true);
  });

  it("الوسمان يعبران منطقاً لا نصّاً، والاسم عربيّه سجلٌّ وترجماته صفوف", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/hr/pay-components",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/pay-components"]: { itemCount: 0, items: [] },
          ["POST " + BASE + "/pay-components"]: {
            id: "c0000000-0000-0000-0000-000000000001",
            code: "HOUSING",
            nameAr: "بدل سكن",
            nameTranslations: [{ name: "en", value: "Housing allowance" }],
            kind: "earning",
            entersContributoryWage: true,
            entersEndOfServiceBase: false,
          },
        },
        sent,
      }),
    });

    await type(await screen.findByTestId<HTMLInputElement>("hr-component-code"), "HOUSING");
    await type(screen.getByTestId<HTMLInputElement>("hr-component-name-ar"), "بدل سكن");
    await type(screen.getByTestId<HTMLInputElement>("hr-component-name-en"), "Housing allowance");
    await act(async () => {
      const wage = screen.getByTestId<HTMLSelectElement>("hr-component-wage");
      setNativeValue(wage, "yes");
      wage.dispatchEvent(new Event("change", { bubbles: true }));
      await Promise.resolve();
    });
    await act(async () => {
      const eos = screen.getByTestId<HTMLSelectElement>("hr-component-eos");
      setNativeValue(eos, "no");
      eos.dispatchEvent(new Event("change", { bubbles: true }));
      await Promise.resolve();
    });

    await click(screen.getByTestId("hr-component-submit"));

    const posted = sent.find((r) => r.method === "POST");
    const body = posted?.body as {
      entersContributoryWage: unknown;
      entersEndOfServiceBase: unknown;
      nameTranslations: { name: string; value: string }[];
    };
    expect(body.entersContributoryWage).toBe(true);
    expect(body.entersEndOfServiceBase).toBe(false);
    /* والترجمة الفارغة لا تُودَع صفّاً فارغاً. */
    expect(body.nameTranslations).toEqual([{ name: "en", value: "Housing allowance" }]);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٧ · الرمز المعتم وحده — لا معرّف شخصي على هذه المستندات
   ═══════════════════════════════════════════════════════════════════════ */
describe("لا معرّف شخصي", () => {
  it("العقد نفسه لا يحمل على السلفة ولا الاستقطاع اسماً ولا هوية ولا آيباناً", () => {
    for (const name of ["HrAdvance", "HrDeduction", "HrSocialInsurancePayment", "HrReconciliationDivergence"]) {
      const fields = Object.keys(SCHEMAS[name]?.fields ?? {});
      expect(fields.length, name).toBeGreaterThan(0);
      for (const forbidden of ["nameAr", "nationalId", "iban", "identity", "birthDate"]) {
        expect(fields, name).not.toContain(forbidden);
      }
    }
  });

  it("بطاقة السلفة تعرض الرمز المعتم وقسطاً استُهلك وآخر لم يُستهلك", async () => {
    await mount({
      path: "/hr/advances-deductions",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/employee-advances/" + ADVANCE_ID]: ADVANCE,
        },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("hr-advance-lookup-id"), ADVANCE_ID);
    await click(screen.getByTestId("hr-advance-open"));

    const card = await screen.findByTestId("hr-advance-card");
    expect(card.textContent).toContain(CODE);
    const schedule = screen.getByTestId("hr-advance-schedule");
    const cells = [...schedule.querySelectorAll('[data-testid="hr-instalment-consumed"]')];
    expect(cells.length).toBe(3);
    expect(cells[0]?.textContent).toContain("s0000000-0000-0000-0000-000000000001");
    expect(cells[1]?.textContent).toContain("لم يُستقطع بعد");
  });

  it("وصرفُ السلفة بلا باب ترحيل — والغياب مُعلَن على الشاشة لا مسكوتٌ عنه", async () => {
    await mount({ path: "/hr/advances-deductions", transport: stub({ routes: { "GET /health": HEALTH } }) });
    const gap = await screen.findByTestId("hr-gap-advance-posting");
    expect(gap.textContent).toContain("القرار المطلوب من المالك");
    /* ولا زرَّ ترحيلٍ على أيٍّ من المستندين. */
    expect(screen.queryByTestId("hr-advance-post")).toBeNull();
    expect(screen.queryByTestId("hr-deduction-post")).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٨ · صفوف الحقول تقف على مسارات وعاءٍ مُعلَن (ADR-0067)
   ═══════════════════════════════════════════════════════════════════════ */
describe("وعاء الصفّ", () => {
  /* الجذر من مجلّد التنفيذ كما تفعل بقيّة حرّاس المصدر في هذا المجلّد. */
  const WEB = process.cwd();
  const SCREEN_FILES = [
    "PayComponentsScreen.tsx",
    "AdvancesDeductionsScreen.tsx",
    "SocialInsuranceScreen.tsx",
    "SubledgerReconciliationScreen.tsx",
  ];

  /** أوعية المسارات كما تُعلنها `components.css` نفسها — لا قائمةٌ مكتوبة هنا. */
  function trackOwners(): string[] {
    const css = readFileSync(path.resolve(WEB, "src/styles/components.css"), "utf8");
    const rule = /:is\(([^)]*)\)\s*>\s*:is\(\.field,\.rowctl\)/.exec(css);
    expect(rule, "قاعدة استعارة المسارات لم تعد موجودة في components.css").not.toBeNull();
    return (rule?.[1] ?? "").split(",").map((s) => s.trim().replace(/^\./, ""));
  }

  it("كل وعاءٍ يحمل حقلين فأكثر في الشاشات الأربع مُسجَّلٌ في قاعدة استعارة المسارات", () => {
    const owners = trackOwners();
    expect(owners.length).toBeGreaterThan(2);
    const problems: string[] = [];
    for (const file of SCREEN_FILES) {
      const src = readFileSync(path.resolve(WEB, "src/screens/hr", file), "utf8");
      /* وعاءٌ = عنصرٌ بصنفٍ حرفيّ يليه مباشرةً `<Field` أو `<div className="rowctl` */
      const container = /<div className="([^"]+)">\s*(?:\{[^}]*\}\s*)?<(?:Field|div className="rowctl)/g;
      let m: RegExpExecArray | null;
      while ((m = container.exec(src))) {
        const classes = (m[1] ?? "").split(/\s+/);
        if (!classes.some((c) => owners.includes(c))) {
          problems.push(file + " ← " + m[1]);
        }
      }
    }
    expect(problems).toEqual([]);
  });

  it("الشاشات الأربع لا تكتب محاذاةً بيدها ولا خاصيةً فيزيائية في سمة نمط", () => {
    for (const file of SCREEN_FILES) {
      const src = readFileSync(path.resolve(WEB, "src/screens/hr", file), "utf8");
      expect(src, file).not.toMatch(/align-items/);
      expect(src, file).not.toMatch(/marginLeft|marginRight|paddingLeft|paddingRight/);
      expect(src, file).not.toMatch(/tabular-nums/);
    }
  });
});
