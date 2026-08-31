/* ═══════════════════════════════════════════════════════════════════════════
   الموارد البشرية — الحرّاس التي لا يجوز أن تحمرّ يوماً
   ───────────────────────────────────────────────────────────────────────────
   خمسة أشياء تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحَص:

     ١ · **قيمةٌ مقنَّعة لا تُعرَض مكشوفةً أبداً** — ولا في سمة، ولا في تلميح،
         ولا في نصٍّ مخفيّ. والحارس الأقوى بنيويّ: العقد نفسه لا يستطيع أن
         يسلّم القيمة الأصلية، فلا يوجد طريقٌ تسلكه إلى الشاشة.
     ٢ · **الرفض حالةٌ أولى تبقى** — لا فقاعةٌ تختفي: يُعرَض، ويُسمّي البند،
         ويبقى بعد أن يكتب المستخدم في حقلٍ آخر.
     ٣ · **حالة الفراغ مصمَّمة** — والجدول الفارغ عمداً يقول لماذا هو فارغ.
     ٤ · **المال لا يمرّ بـ`number` في أي خطوة** — لا داخلاً ولا خارجاً.
     ٥ · **الاتجاه صحيح** — الجذر يتبع اللغة، والخانات الآلية معزولة.

   وسادسٌ يخصّ هذه الوحدة: **الترحيل المكرَّر يقول الحقيقة** ولا يُظهر نجاحاً
   ثانياً على عملٍ لم يقع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import { SECTIONS, SCREENS, sectionOf } from "../src/app/shell/sections";
import { Money } from "../src/api/money";
import type { RawResponse, Transport } from "../src/api/transport";
import { resetHrFocus, setHrFocus } from "../src/screens/hr/focus";

/* ═══════════════════════════════════ القيم التي يجب ألّا تُرى مكشوفة ═══ */

/** رقم هوية مميّز يستحيل أن يظهر مصادفةً — فبحثٌ يجده يجده لأنه كُتب. */
const RAW_NATIONAL_ID = "1099887766";
/** آيبان مميّز، وطولُه يخالف طول الهوية عمداً. */
const RAW_IBAN = "SA4420000001234567891234";
/** والقناعان متساويا الطول رغم اختلاف طولَي الأصلين. */
const MASK_NATIONAL_ID = "**********7766";
const MASK_IBAN = "**********1234";

/** مبلغٌ بأربع منازل، خانتُه الأخيرة هي ما يفقده العائم. */
const EXACT_WIRE = "1000000000000.4013";

const COMPANY = "11111111-1111-1111-1111-111111111111";

/* ═══════════════════════════════════════════════ أجسام الاستجابة ═══════ */

const HEALTH = { apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" };

const EMPLOYEE = {
  id: "e0000000-0000-0000-0000-000000000001",
  code: "emp-0000000000000042",
  nameAr: "عبد الله محمد",
  nameTranslations: [
    { name: "en", value: "Abdullah Muhammad" },
    { name: "ur", value: "عبد اللہ محمد" },
    { name: "hi", value: "अब्दुल्लाह मुहम्मद" },
  ],
  classCode: "class-private",
  costCenterId: "cc-ops",
  employmentId: "m0000000-0000-0000-0000-000000000001",
  startedOn: "2024-03-01",
  endedOn: null,
  state: "ACTIVE",
  identity: { nationalIdMask: MASK_NATIONAL_ID, ibanMask: MASK_IBAN },
};

const PAY_COMPONENTS = {
  itemCount: 1,
  items: [
    {
      id: "c0000000-0000-0000-0000-000000000001",
      code: "BASIC",
      nameAr: "الراتب الأساسي",
      nameTranslations: [
        { name: "en", value: "Basic salary" },
        { name: "ur", value: "بنیادی تنخواہ" },
        { name: "hi", value: "मूल वेतन" },
      ],
      kind: "earning",
      entersContributoryWage: true,
      entersEndOfServiceBase: true,
    },
  ],
};

const PAY_ELEMENTS = {
  itemCount: 1,
  items: [
    {
      id: "p0000000-0000-0000-0000-000000000001",
      componentCode: "BASIC",
      effectiveFrom: "2026-01-01",
      amount: EXACT_WIRE,
    },
  ],
};

const AMOUNTS = {
  grossEntitlements: "8000.0000",
  employerSocialInsurance: "0.0000",
  employeeSocialInsurance: "0.0000",
  advanceInstalment: "0.0000",
  deductions: "0.0000",
  netPayable: "8000.0000",
};

const RUN_ID = "r0000000-0000-0000-0000-000000000001";
const PAYSLIP_ID = "s0000000-0000-0000-0000-000000000001";

function run(state: string) {
  return {
    id: RUN_ID,
    number: "RUN-2026-06",
    periodCode: "2026-06",
    periodStart: "2026-06-01",
    periodEnd: "2026-06-30",
    state,
    amounts: AMOUNTS,
    payslipCount: 1,
  };
}

function payslip(options: { state: string; entryId: string | null; alreadyPosted: boolean }) {
  return {
    id: PAYSLIP_ID,
    runId: RUN_ID,
    employeeId: EMPLOYEE.id,
    employmentId: EMPLOYEE.employmentId,
    employeeCode: EMPLOYEE.code,
    costCenterId: "cc-ops",
    contributoryWage: "8000.0000",
    amounts: AMOUNTS,
    components: [
      { lineNo: 1, componentCode: "BASIC", kind: "earning", entersContributoryWage: true, amount: EXACT_WIRE },
      { lineNo: 2, componentCode: "GHOST", kind: "deduction", entersContributoryWage: false, amount: "25.0000" },
    ],
    state: options.state,
    entryId: options.entryId,
    alreadyPosted: options.alreadyPosted,
  };
}

const SETTLEMENT_ID = "x0000000-0000-0000-0000-000000000001";

function settlement(options: { state: string; entryId: string | null; alreadyPosted: boolean }) {
  return {
    id: SETTLEMENT_ID,
    number: "EOS-S-2026-0007",
    employmentId: EMPLOYEE.employmentId,
    employeeCode: EMPLOYEE.code,
    settledOn: "2026-06-30",
    settlementDue: "41250.0000",
    provisionBalance: "38900.0000",
    amountPaid: "41250.0000",
    shortfall: "2350.0000",
    excess: "0.0000",
    provisionUtilised: "38900.0000",
    scenarioCode: "short",
    measurementRef: "أساس القياس المعتمد ٢٠٢٥/٤",
    settlementMethod: "bank",
    treasuryPartyId: "BANK-0001",
    state: options.state,
    entryId: options.entryId,
    alreadyPosted: options.alreadyPosted,
  };
}

/** رفضٌ بصيغة RFC 9457 كما ترسله الخلفية — بالرسالتين وبالرمز الثابت. */
function refusal(code: string, ar: string, en: string) {
  return {
    code,
    title: "Unprocessable content",
    titleAr: "طلب غير قابل للتنفيذ",
    detail: en,
    detailAr: ar,
    errors: [{ code, field: null, messageAr: ar, messageEn: en }],
    instance: "/api/v1/companies/" + COMPANY + "/payroll-runs",
    status: 422,
    traceId: "00-test0000000000000000000000-0000000000000000-01",
    type: "https://salasel-babel.example/problems/" + code,
  };
}

const SETTINGS_REFUSAL = refusal(
  "hr.payroll_settings_missing",
  "لا صفَّ نِسَبٍ معتمداً يغطّي التصنيف «class-private» في 2026-06-30.",
  "No approved rate row covers class 'class-private' on 2026-06-30."
);

/* ═══════════════════════════════════════════════════ نقلٌ بلا شبكة ═════ */

interface Recorded {
  method: string;
  url: string;
  body?: unknown;
}

interface StubOptions {
  /** ما يُعاد لكل مسار؛ المفتاح "METHOD /path". */
  readonly routes: Readonly<Record<string, unknown>>;
  /** مسارات ترفض، بجسم المشكلة. */
  readonly refusals?: Readonly<Record<string, unknown>>;
  /** سجلّ ما أُرسل — لفحص ما يعبر السلك فعلاً. */
  readonly sent?: Recorded[];
}

function stub(options: StubOptions): Transport {
  return ({ method, url, body }) => {
    options.sent?.push({ method, url, body });
    const path = url.split("?")[0] ?? url;
    const key = method + " " + path;
    const bad = options.refusals?.[key];
    if (bad !== undefined) {
      return Promise.resolve<RawResponse>({ ok: false, status: 422, json: bad, url });
    }
    const good = options.routes[key];
    if (good === undefined) {
      return Promise.resolve<RawResponse>({ ok: false, status: 404, json: null, url });
    }
    return Promise.resolve<RawResponse>({ ok: true, status: 200, json: good, url });
  };
}

const base = "/api/v1/companies/" + COMPANY;

function routesFor(overrides: Readonly<Record<string, unknown>> = {}): Record<string, unknown> {
  return {
    "GET /health": HEALTH,
    ["GET " + base + "/employees/" + EMPLOYEE.id]: EMPLOYEE,
    ["GET " + base + "/employees/" + EMPLOYEE.id + "/pay-elements"]: PAY_ELEMENTS,
    ["GET " + base + "/pay-components"]: PAY_COMPONENTS,
    ["GET " + base + "/payroll-settings"]: { itemCount: 0, items: [] },
    ["GET " + base + "/payroll-runs/" + RUN_ID]: run("DRAFT"),
    ["GET " + base + "/payroll-runs/" + RUN_ID + "/payslips"]: {
      itemCount: 1,
      items: [payslip({ state: "DRAFT", entryId: null, alreadyPosted: false })],
    },
    ["GET " + base + "/payslips/" + PAYSLIP_ID]: payslip({
      state: "POSTED",
      entryId: "j0000000-0000-0000-0000-000000000009",
      alreadyPosted: false,
    }),
    ...overrides,
  };
}

/* ══════════════════════════════════════════════════════ التركيب ═══════ */

async function mount(options: {
  path: string;
  transport: Transport;
  locale?: string;
}): Promise<void> {
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
   ١ · القناع
   ═══════════════════════════════════════════════════════════════════════ */
describe("القناع — ولا كشف", () => {
  it("العقد نفسه لا يستطيع تسليم الهوية مكشوفة: الحقل يشير إلى مخطّط قناعين لا أكثر", () => {
    /* هذا هو الحارس البنيوي: ما لا يُنشَر لا يُعرَض، ولا حاجة إلى الثقة بشاشة. */
    const identity = SCHEMAS.HrEmployee?.fields.identity;
    expect(identity?.k).toBe("ref");
    expect(identity?.r).toBe("HrMaskedIdentity");
    const mask = SCHEMAS.HrMaskedIdentity;
    expect(Object.keys(mask?.fields ?? {}).sort()).toEqual(["ibanMask", "nationalIdMask"]);
    /* ولا باب في العميل المُولَّد يَعِد بكشفٍ أو بفكّ قناع. */
    expect(SCHEMAS).not.toHaveProperty("HrUnmaskedIdentity");
  });

  it("بطاقة الموظف تعرض القناعين ولا تعرض الأصل — لا في نصّ ولا في سمة", async () => {
    setHrFocus({ employeeId: EMPLOYEE.id });
    await mount({ path: "/hr", transport: stub({ routes: routesFor() }) });

    await screen.findByTestId("hr-masked-identity");
    expect(screen.getByTestId("hr-mask-national-id").textContent).toBe(MASK_NATIONAL_ID);
    expect(screen.getByTestId("hr-mask-iban").textContent).toBe(MASK_IBAN);

    /* الصفحة كلّها — نصّاً وسماتٍ — خاليةٌ من القيمتين الأصليتين. */
    const html = document.body.innerHTML;
    expect(html).not.toContain(RAW_NATIONAL_ID);
    expect(html).not.toContain(RAW_IBAN);
    /* والقناعان متساويا الطول، فلا يُقرأ منهما طولُ ما تحتهما. */
    expect(MASK_NATIONAL_ID.length).toBe(MASK_IBAN.length);
    expect(RAW_NATIONAL_ID.length).not.toBe(RAW_IBAN.length);
  });

  it("لا زرَّ كشفٍ ولا فعلَ كشفٍ في الشاشة — وتقول ذلك صراحةً", async () => {
    setHrFocus({ employeeId: EMPLOYEE.id });
    await mount({ path: "/hr", transport: stub({ routes: routesFor() }) });

    const panel = await screen.findByTestId("hr-masked-identity");
    expect(within(panel).getByTestId("hr-mask-no-reveal")).toBeTruthy();
    /* ولا عنصر تفاعليّ داخل لوحة القناع إطلاقاً: لا زرّ، ولا مربّع اختيار،
       ولا رابط. فما لا يوجد لا يُنقَر بالخطأ ولا يُفتح باختصار لوحة مفاتيح. */
    expect(panel.querySelectorAll("button, input, a, [role='button']").length).toBe(0);
  });

  it("الرمز المعتم وحده هو ما يظهر على القسيمة — ولا اسم ولا هوية", async () => {
    setHrFocus({ payslipId: PAYSLIP_ID });
    await mount({ path: "/hr/payslip", transport: stub({ routes: routesFor() }) });

    await screen.findByTestId("hr-payslip-card");
    expect(screen.getByTestId("hr-payslip-employee-code").textContent).toBe(EMPLOYEE.code);
    const card = screen.getByTestId("hr-payslip-card");
    expect(card.textContent).not.toContain(EMPLOYEE.nameAr);
    expect(card.innerHTML).not.toContain(RAW_NATIONAL_ID);
    expect(card.innerHTML).not.toContain(RAW_IBAN);
    expect(screen.getByTestId("hr-payslip-no-identity").textContent?.length).toBeGreaterThan(20);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · الرفض حالةٌ أولى تبقى
   ═══════════════════════════════════════════════════════════════════════ */
describe("الرفض", () => {
  it("رفضُ «لا صفَّ نِسَبٍ معتمد» يُعرَض مُسمّى، ويبقى بعد أن يكتب المستخدم في حقلٍ آخر", async () => {
    const transport = stub({
      routes: routesFor(),
      refusals: { ["POST " + base + "/payroll-runs"]: SETTINGS_REFUSAL },
    });
    await mount({ path: "/hr/payroll", transport });

    const number = await screen.findByTestId<HTMLInputElement>("hr-run-number");
    const period = screen.getByTestId<HTMLInputElement>("hr-run-period");
    const start = screen.getByTestId<HTMLInputElement>("hr-run-start");
    const end = screen.getByTestId<HTMLInputElement>("hr-run-end");

    await type(number, "RUN-2026-06");
    await type(period, "2026-06");
    await type(start, "2026-06-01");
    await type(end, "2026-06-30");

    await click(screen.getByTestId("hr-run-draft-submit"));

    const refusalPanel = await screen.findByTestId("hr-refusal-settings");
    expect(refusalPanel.textContent).toContain("hr.payroll_settings_missing");
    /* رسالة الخادم بلغتيها تُعرَض كما وصلت ولا تُترجَم في الواجهة. */
    expect(screen.getByTestId("problem-panel").textContent).toContain("class-private");
    expect(screen.getByTestId("problem-code").textContent).toBe("hr.payroll_settings_missing");

    /* والآن الاختبار الحقيقي: الرفض **يبقى**. الفقاعة تختفي، والحالة الأولى لا. */
    await type(number, "RUN-2026-07");
    expect(screen.getByTestId("hr-refusal-settings")).toBeTruthy();
    expect(screen.getByTestId("problem-panel")).toBeTruthy();
  });

  it("الرفض يقود إلى بابه: اللوحة تسمّي جدول النِّسَب وهو حاضرٌ في الشاشة نفسها", async () => {
    const transport = stub({
      routes: routesFor(),
      refusals: { ["POST " + base + "/payroll-runs"]: SETTINGS_REFUSAL },
    });
    await mount({ path: "/hr/payroll", transport });
    expect(await screen.findByTestId("hr-rates")).toBeTruthy();
    expect(screen.getByTestId("hr-rates-empty")).toBeTruthy();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · حالة الفراغ مصمَّمة
   ═══════════════════════════════════════════════════════════════════════ */
describe("الفراغ", () => {
  it("جدول النِّسَب يُسلَّم فارغاً عمداً، وحالته تقول لماذا وتعطي الخطوة التالية", async () => {
    await mount({ path: "/hr/payroll", transport: stub({ routes: routesFor() }) });
    const empty = await screen.findByTestId("hr-rates-empty");
    expect(empty.textContent).toContain("عمداً");
    /* وفيها فعلٌ يُمضي: ليست شكوى. */
    expect(empty.querySelector("button")).toBeTruthy();
  });

  it("شاشة القسيمة بلا قسيمة مفتوحة تعرض حالة فراغٍ تقول من أين تُفتَح", async () => {
    await mount({ path: "/hr/payslip", transport: stub({ routes: routesFor() }) });
    const empty = await screen.findByTestId("hr-payslip-empty");
    expect(empty.textContent?.length).toBeGreaterThan(20);
  });

  it("بابٌ غير منشور يُعلَن ولا يُخفى، ومعه القرار المطلوب من المالك", async () => {
    setHrFocus({ employeeId: EMPLOYEE.id });
    await mount({ path: "/hr", transport: stub({ routes: routesFor() }) });
    const gap = await screen.findByTestId("hr-gap-no-list");
    expect(gap.textContent).toContain("لا سردَ للموظفين");
    /* والقرار المطلوب مكتوبٌ باسمه، لا «قريباً». */
    expect(gap.textContent).toContain("القرار المطلوب من المالك");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ · المال لا يمرّ بـ number
   ═══════════════════════════════════════════════════════════════════════ */
describe("المال نصّ", () => {
  it("رمزٌ رقمي في حقلٍ مالي يُرفَض عند الحدّ ولا يصل إلى الشاشة", () => {
    expect(() => Money.wire(1000.4013)).toThrow(TypeError);
    expect(() => Money.wire(Number(EXACT_WIRE))).toThrow(TypeError);
  });

  it("المبلغ يصل إلى الشاشة بايتاً ببايت: العرض مقرَّب، والأصل في السمة", async () => {
    setHrFocus({ employeeId: EMPLOYEE.id });
    await mount({ path: "/hr", transport: stub({ routes: routesFor() }) });

    const table = await screen.findByTestId("hr-elements-table");
    const amount = table.querySelector("span.amt");
    expect(amount?.getAttribute("title")).toBe(EXACT_WIRE);
    /* وما يُقرأ بالعين مقرَّبٌ إلى منزلتين — ولو مرّ بعائم لضاعت الرابعة أصلاً. */
    expect(amount?.textContent).toBe("1,000,000,000,000.40");
    /* والقيمة التي كانت ستنتج عن العائم لا تظهر في الصفحة إطلاقاً. */
    expect(document.body.innerHTML).not.toContain("1000000000000.4012");
  });

  it("المبلغ يغادر السلك نصّاً لا رمزاً رقمياً", async () => {
    const sent: Recorded[] = [];
    setHrFocus({ employeeId: EMPLOYEE.id });
    await mount({
      path: "/hr",
      transport: stub({
        routes: routesFor({
          ["POST " + base + "/employees/" + EMPLOYEE.id + "/pay-elements"]: PAY_ELEMENTS.items[0],
        }),
        sent,
      }),
    });

    await screen.findByTestId("hr-element-code");
    await select(screen.getByTestId<HTMLSelectElement>("hr-element-code"), "BASIC");
    await type(screen.getByTestId<HTMLInputElement>("hr-element-from"), "2026-06-01");
    await type(screen.getByTestId<HTMLInputElement>("hr-element-amount"), EXACT_WIRE);
    await click(screen.getByTestId("hr-element-add"));

    const posted = sent.find((r) => r.method === "POST" && r.url.endsWith("/pay-elements"));
    expect(posted).toBeDefined();
    const body = posted?.body as { amount: unknown };
    expect(typeof body.amount).toBe("string");
    expect(body.amount).toBe(EXACT_WIRE);
    /* والدليل القاطع: النصّ المُرمَّز لا يحمل رمزاً رقمياً في هذا الحقل. */
    expect(JSON.stringify(posted?.body)).toContain('"amount":"' + EXACT_WIRE + '"');
  });

  it("مبلغٌ لا يطابق النحو المنشور يُوسَم خطأً ولا يُرسَل", async () => {
    const sent: Recorded[] = [];
    setHrFocus({ employeeId: EMPLOYEE.id });
    await mount({ path: "/hr", transport: stub({ routes: routesFor(), sent }) });

    await screen.findByTestId("hr-element-amount");
    const amount = screen.getByTestId<HTMLInputElement>("hr-element-amount");
    await type(amount, "12.345678");
    expect(amount.getAttribute("aria-invalid")).toBe("true");
    expect(screen.getByTestId<HTMLButtonElement>("hr-element-add").disabled).toBe(true);
    expect(sent.some((r) => r.method === "POST")).toBe(false);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٥ · الاتجاه
   ═══════════════════════════════════════════════════════════════════════ */
describe("الاتجاه", () => {
  it("الجذر يتبع اللغة: العربية والأردية rtl، والهندية ltr", async () => {
    await mount({ path: "/hr/payroll", transport: stub({ routes: routesFor() }), locale: "ar" });
    expect(document.documentElement.getAttribute("dir")).toBe("rtl");
    cleanup();

    await mount({ path: "/hr/payroll", transport: stub({ routes: routesFor() }), locale: "ur" });
    expect(document.documentElement.getAttribute("dir")).toBe("rtl");
    cleanup();

    await mount({ path: "/hr/payroll", transport: stub({ routes: routesFor() }), locale: "hi" });
    expect(document.documentElement.getAttribute("dir")).toBe("ltr");
  });

  it("الرمز المعتم والقناع نصّان آليّان معزولان بـltr داخل صفحةٍ rtl", async () => {
    setHrFocus({ employeeId: EMPLOYEE.id });
    await mount({ path: "/hr", transport: stub({ routes: routesFor() }) });
    await screen.findByTestId("hr-masked-identity");
    expect(screen.getByTestId("hr-opaque-code").getAttribute("dir")).toBe("ltr");
    expect(screen.getByTestId("hr-mask-national-id").getAttribute("dir")).toBe("ltr");
    expect(document.documentElement.getAttribute("dir")).toBe("rtl");
  });

  it("القسيمة تُقرأ بلغة قارئها: اسم المكوّن يأتي بلغة الواجهة لا بالإنجليزية دائماً", async () => {
    setHrFocus({ payslipId: PAYSLIP_ID });
    await mount({ path: "/hr/payslip", transport: stub({ routes: routesFor() }), locale: "ur" });
    const components = await screen.findByTestId("hr-payslip-components");
    expect(components.textContent).toContain("بنیادی تنخواہ");
    expect(components.textContent).not.toContain("Basic salary");
    cleanup();

    setHrFocus({ payslipId: PAYSLIP_ID });
    await mount({ path: "/hr/payslip", transport: stub({ routes: routesFor() }), locale: "hi" });
    const hindi = await screen.findByTestId("hr-payslip-components");
    expect(hindi.textContent).toContain("मूल वेतन");
    expect(hindi.textContent).not.toContain("Basic salary");
  });

  it("مكوّنٌ لا تعريف له يُعرَض برمزه ولا يُخترع له اسم", async () => {
    setHrFocus({ payslipId: PAYSLIP_ID });
    await mount({ path: "/hr/payslip", transport: stub({ routes: routesFor() }) });
    await screen.findByTestId("hr-payslip-components");
    expect(screen.getByTestId("hr-component-unknown")).toBeTruthy();
    expect(screen.getByTestId("hr-payslip-components").textContent).toContain("GHOST");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٦ · الترحيل المكرَّر يقول الحقيقة
   ═══════════════════════════════════════════════════════════════════════ */
describe("الإحكام", () => {
  it("ترحيلٌ أول: يقول إن قيداً كُتب لكل قسيمة", async () => {
    await mount({
      path: "/hr/payroll",
      transport: stub({
        routes: routesFor({
          ["POST " + base + "/payroll-runs/" + RUN_ID + "/posting"]: {
            itemCount: 1,
            items: [payslip({ state: "POSTED", entryId: "j1", alreadyPosted: false })],
          },
        }),
      }),
    });

    await type(screen.getByTestId<HTMLInputElement>("hr-run-id"), RUN_ID);
    await screen.findByTestId("hr-post-submit");
    await click(screen.getByTestId("hr-post-submit"));

    const receipt = await screen.findByTestId("hr-post-receipt");
    expect(receipt.getAttribute("data-already")).toBe("false");
    expect(screen.getByTestId("hr-posted-fresh").textContent).toContain("1");
  });

  it("ترحيلٌ ثانٍ بالهوية نفسها: لا نجاحَ ثانٍ، بل «لم يقع ترحيلٌ جديد»", async () => {
    await mount({
      path: "/hr/payroll",
      transport: stub({
        routes: routesFor({
          ["POST " + base + "/payroll-runs/" + RUN_ID + "/posting"]: {
            itemCount: 1,
            items: [payslip({ state: "POSTED", entryId: "j1", alreadyPosted: true })],
          },
        }),
      }),
    });

    await type(screen.getByTestId<HTMLInputElement>("hr-run-id"), RUN_ID);
    await screen.findByTestId("hr-post-submit");
    await click(screen.getByTestId("hr-post-submit"));

    const receipt = await screen.findByTestId("hr-post-receipt");
    expect(receipt.getAttribute("data-already")).toBe("true");
    expect(screen.getByTestId("hr-posted-fresh").textContent).toContain("0");
    expect(screen.getByTestId("hr-posted-already").textContent).toContain("1");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٧ · عقد الملاحة
   ═══════════════════════════════════════════════════════════════════════ */
describe("الملاحة", () => {
  it("قسم الموارد البشرية صار مبنيّاً، وشاشاته الأربع مُعلَنة في عقد الملاحة", () => {
    const hr = SECTIONS.find((s) => s.id === "hr");
    expect(hr?.built).toBe(true);
    expect(hr?.path).toBe("/hr");
    expect(hr?.tint).toBe("var(--section-hr)");
    const paths = SCREENS.filter((s) => s.section === "hr").map((s) => s.path);
    expect(paths).toEqual(["/hr", "/hr/payroll", "/hr/payslip", "/hr/end-of-service"]);
    for (const path of paths) expect(sectionOf(path).id).toBe("hr");
  });

  it("لا صفَّ قسمٍ آخر مسّته هذه الإضافة", () => {
    expect(SECTIONS.map((s) => s.id)).toEqual([
      "accounting",
      "inventory",
      "hr",
      "contracting",
      "realestate",
    ]);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٨ · المسوّدة تُرى قبل أن تُرحَّل
   ═══════════════════════════════════════════════════════════════════════ */
describe("المسوّدة قبل الترحيل", () => {
  it("مسوّدة المخالصة تُظهر السيناريو والعجز **قبل** الترحيل، والترحيل فعلٌ ثانٍ", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/hr/end-of-service",
      transport: stub({
        routes: routesFor({
          ["POST " + base + "/end-of-service-settlements"]: settlement({
            state: "DRAFT",
            entryId: null,
            alreadyPosted: false,
          }),
          ["POST " + base + "/end-of-service-settlements/" + SETTLEMENT_ID + "/posting"]: settlement({
            state: "POSTED",
            entryId: "j0000000-0000-0000-0000-00000000000e",
            alreadyPosted: false,
          }),
        }),
        sent,
      }),
    });

    await type(await screen.findByTestId<HTMLInputElement>("hr-settlement-number"), "EOS-S-2026-0007");
    await type(screen.getByTestId<HTMLInputElement>("hr-settlement-employment"), EMPLOYEE.employmentId);
    await type(screen.getByTestId<HTMLInputElement>("hr-settlement-date"), "2026-06-30");
    await type(screen.getByTestId<HTMLInputElement>("hr-settlement-due"), "41250.0000");
    await type(screen.getByTestId<HTMLInputElement>("hr-settlement-ref"), "أساس القياس المعتمد");
    await type(screen.getByTestId<HTMLInputElement>("hr-settlement-treasury"), "BANK-0001");

    /* الترحيل معطَّل ما دامت المسوّدة لم تُبنَ: الفعل الذي لا رجعة فيه لا يُبدأ منه. */
    expect(screen.getByTestId<HTMLButtonElement>("hr-settlement-submit").disabled).toBe(true);

    await click(screen.getByTestId("hr-settlement-draft"));

    const receipt = await screen.findByTestId("hr-settlement-receipt");
    expect(receipt.getAttribute("data-state")).toBe("DRAFT");
    /* والسيناريو **يصل مُسمّى من الخادم**، ولا تستنتجه الشاشة بمقارنة مبلغين. */
    expect(screen.getByTestId("hr-settlement-scenario").textContent).not.toBe("");
    expect(receipt.textContent).toContain("2,350.00");
    expect(screen.getByTestId("hr-settlement-entry").textContent).not.toBe("");

    /* ولا ترحيل وقع بعد. */
    expect(sent.filter((r) => r.url.endsWith("/posting")).length).toBe(0);

    await click(screen.getByTestId("hr-settlement-submit"));
    await waitFor(() =>
      expect(screen.getByTestId("hr-settlement-receipt").getAttribute("data-state")).toBe("POSTED")
    );
    expect(sent.filter((r) => r.url.endsWith("/posting")).length).toBe(1);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٩ · ما لم يُبنَ مُعلَنٌ لا مسكوتٌ عنه
   ═══════════════════════════════════════════════════════════════════════ */
describe("الأبواب المُعلَنة", () => {
  it("الإجازات والغياب: لا جدول ولا باب ولا حدث — والقسم يقول ذلك", async () => {
    await mount({ path: "/hr", transport: stub({ routes: routesFor() }) });
    const gap = await screen.findByTestId("hr-gap-leave");
    expect(gap.textContent).toContain("لا إجازات");
    expect(gap.textContent).toContain("القرار المطلوب من المالك");
  });

  it("السلف والجزاءات: البابان منشوران والشاشة لم تُبنَ — فالصفر يُقرأ على حقيقته", async () => {
    await mount({ path: "/hr/payroll", transport: stub({ routes: routesFor() }) });
    await type(screen.getByTestId<HTMLInputElement>("hr-run-id"), RUN_ID);
    const gap = await screen.findByTestId("hr-gap-registers");
    expect(gap.textContent).toContain("لا شيء مسجَّل في السجلّ المعتمد");
  });
});

/* ═══════════════════════════════════════════════════ أدوات صغيرة ══════ */

async function click(element: Element): Promise<void> {
  await act(async () => {
    (element as HTMLElement).click();
    await Promise.resolve();
  });
}

async function type(element: HTMLInputElement, value: string): Promise<void> {
  await act(async () => {
    setNativeValue(element, value);
    element.dispatchEvent(new Event("input", { bubbles: true }));
    await Promise.resolve();
  });
}

async function select(element: HTMLSelectElement, value: string): Promise<void> {
  await act(async () => {
    element.value = value;
    element.dispatchEvent(new Event("change", { bubbles: true }));
    await Promise.resolve();
  });
}

/** React يراقب قيمة الحقل عبر واصفٍ على النموذج الأصلي، فيُكتَب من هناك. */
function setNativeValue(element: HTMLInputElement, value: string): void {
  const proto = Object.getPrototypeOf(element) as object;
  /* الواصف على **النموذج الأصلي** عمداً: React يضع مُتعقِّباً على النسخة
     نفسها، والكتابة المباشرة تُحدِّث ذاكرته فيظنّ أن القيمة لم تتغيّر. */
  // eslint-disable-next-line @typescript-eslint/unbound-method
  const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
  if (setter) setter.call(element, value);
  else element.value = value;
}
