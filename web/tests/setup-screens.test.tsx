/* ═══════════════════════════════════════════════════════════════════════════
   شاشات التأسيس الأربع — حرّاسها
   The four setup screens — their guards
   ───────────────────────────────────────────────────────────────────────────
   عشرةُ أشياء تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحص:

     ١ · القوائم الثلاث تتّفق. `SCREENS` والموجّه وقائمةُ الملاحة اليدوية في
         `App.tsx` ثلاثُ نسخٍ ولا شيء يقارنها، فشاشةٌ في واحدةٍ دون الأخرى
         تُفتح بلوحة الأوامر ولا يراها من يقرأ الملاحة.
     ٢ · **`admitDocument` حكمٌ لا كتابة**: الضغط يُصدر طلباً واحداً إلى مسار
         الأحكام، جسمُه **أسماءٌ فقط**، ولا يغادر معه أيُّ طلبِ كتابةٍ آخر.
     ٣ · **قدرةٌ مُطفأة تُعرض مُسمّاةً**، والحقلُ الخارج عن الشكل يُسمّى
         **قبل الضغط** ولا يغادر عنه طلب.
     ٤ · **الموقوف يبقى في الجدول**: صفُّه موجود بحالته بلا مرشّحٍ يُخفيه.
     ٥ · **المركز الافتراضي لا يُوقَف**، ويُقال ذلك بالرمز قبل الضغط والزرُّ
         مُقفَل — وهو تعذّرُ حالةٍ مقروءة لا تخمينُ صلاحية.
     ٦ · **الرمز لا يُرسَل في جسمٍ ولا يتغيّر بإعادة التسمية**: يعبر في المسار
         وحده، وجسمُ الطلب اسمٌ وترجمات لا أكثر.
     ٧ · **التأسيس الثاني لا يُرسَم**، و«لم تُؤسَّس بعد» لا تُعرض عطلاً.
     ٨ · **الاستبدال كلّي**: جسمُ كتابة الملفّ يحمل أنواع المستندات كلَّها
         وقيمَها الافتراضية كما وصلت، والسحبُ يوجب سبباً.
     ٩ · **العدّادان يكشفان النقص** في دليل الحسابات.
    ١٠ · **ولا رقمَ حسابٍ ولا محوّلٌ عددي** في ملفّات هذا القسم، وكلُّ حقلٍ
         في صفٍّ يحمل وصفاً (ADR-0078).

   ولا بيان شخصي في هذا الملفّ: الأسماء أدناه أسماءٌ اصطلاحية للقياس، ولا
   اعتمادَ ولا مفتاحَ ولا عنوانَ مضيف.
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
import { SCREENS } from "../src/app/shell/sections";
import type { RawResponse, Transport } from "../src/api/transport";

const SRC = path.resolve(process.cwd(), "src");
const COMPANY = "11111111-1111-4111-8111-111111111111";

/** المسارات الأربع بترتيب العمل — والترتيب نفسه في الملاحة وفي SCREENS. */
const SETUP_PATHS = [
  "/setup",
  "/setup/cost-centers",
  "/setup/document-shapes",
  "/setup/chart-of-accounts",
  "/setup/parameters",
];

const SETUP = {
  costCenters: [
    {
      code: "cc.main",
      isDefault: true,
      nameAr: "المركز الرئيسي",
      nameTranslations: [{ name: "en", value: "Head office" }],
      state: "Active",
      suspensionReason: "",
    },
    {
      code: "cc.branch",
      isDefault: false,
      nameAr: "فرعٌ للقياس",
      nameTranslations: [],
      state: "Active",
      suspensionReason: "",
    },
    {
      code: "cc.closed",
      isDefault: false,
      nameAr: "فرعٌ مغلقٌ للقياس",
      nameTranslations: [],
      state: "Suspended",
      suspensionReason: "أُغلق الفرع عند إقفال الفترة",
    },
  ],
  decimalPlaces: 2,
  defaultCostCenter: "cc.main",
  nameAr: "منشأةُ قياس",
  nameTranslations: [{ name: "en", value: "A measured entity" }],
};

const BILL_SHAPE = {
  availableCapabilities: ["landed_cost", "retention", "three_way_match"],
  defaults: [{ name: "currency", value: "SAR" }],
  documentType: "purchasing.supplier_bill",
  enabledCapabilities: ["three_way_match"],
  fields: ["costCenter", "currency", "issuedOn", "matchedReceipt", "supplier"],
  module: "Purchasing",
  nameAr: "فاتورة المورّد",
  nameKey: "document.purchasing.supplier_bill",
};

const INVOICE_SHAPE = {
  availableCapabilities: ["advance", "cost_of_sales"],
  defaults: [],
  documentType: "sales.invoice",
  enabledCapabilities: ["cost_of_sales"],
  fields: ["costOfSales", "customer", "issuedOn"],
  module: "Sales",
  nameAr: "فاتورة المبيعات",
  nameKey: "document.sales.invoice",
};

const PROFILE = { documents: [BILL_SHAPE, INVOICE_SHAPE] };

const CHART = {
  accountCount: 3,
  accounts: [
    {
      accountCode: "A",
      accountType: "asset",
      active: true,
      contra: false,
      currencyCode: null,
      currencyMode: "any",
      level: 1,
      nameAr: "أصولٌ للقياس",
      nameTranslations: [],
      naturalSide: "debit",
      parentCode: null,
      postable: false,
      requiredDimensions: [],
      subledgerType: "none",
    },
    {
      accountCode: "A-1",
      accountType: "asset",
      active: true,
      contra: false,
      currencyCode: "SAR",
      currencyMode: "fixed",
      level: 4,
      nameAr: "ذممٌ للقياس",
      nameTranslations: [{ name: "en", value: "A measured receivable" }],
      naturalSide: "debit",
      parentCode: "A",
      postable: true,
      requiredDimensions: ["cost_center"],
      subledgerType: "customer",
    },
    {
      accountCode: "A-2",
      accountType: "expense",
      active: false,
      contra: true,
      currencyCode: null,
      currencyMode: "company_only",
      level: 4,
      nameAr: "مصروفٌ معطَّلٌ للقياس",
      nameTranslations: [],
      naturalSide: "debit",
      parentCode: "A",
      postable: true,
      requiredDimensions: [],
      subledgerType: "none",
    },
  ],
  postableCount: 2,
};

/* ══════════════════════════════════════════════════════════ أدوات ═════ */

interface Recorded {
  method: string;
  url: string;
  body?: unknown;
}

function problem(status: number, code: string, at: string) {
  return {
    code,
    detail: "Refused.",
    detailAr: "رُفض الطلب.",
    errors: [],
    instance: at,
    status,
    title: "Refused",
    titleAr: "رفض",
    traceId: "trace-for-the-test",
    type: "about:blank",
  };
}

function stub(options: {
  routes: Readonly<Record<string, unknown>>;
  refuse?: Readonly<Record<string, { status: number; code: string }>>;
  sent?: Recorded[];
}): Transport {
  return ({ method, url, body }) => {
    options.sent?.push({ method, url, body });
    const at = url.split("?")[0] ?? url;
    const key = method + " " + at;
    const refusal = options.refuse?.[key];
    if (refusal) {
      return Promise.resolve<RawResponse>({
        ok: false,
        status: refusal.status,
        json: problem(refusal.status, refusal.code, at),
        url,
      });
    }
    const found = options.routes[key];
    if (found === undefined) {
      return Promise.resolve<RawResponse>({
        ok: false,
        status: 404,
        json: problem(404, "http.not_found", at),
        url,
      });
    }
    return Promise.resolve<RawResponse>({ ok: true, status: 200, json: found, url });
  };
}

const AT = "/api/v1/companies/" + COMPANY;

/* ── المعامِلات: افتراضُ منصّةٍ غير معتمَد، وتجاوزٌ للمنشأة ────────────────
   والقيم هنا **متجهات اختبار** لا إفادةٌ عن نظام: الشاشة لا تكتب رقماً ولا
   رمزَ مجموعة، فما يُبذر هنا هو ما يردّه خادمٌ مُحاكى. */
const PARAMETERS = {
  itemCount: 2,
  items: [
    {
      id: "0f6a1d00-0000-4000-8000-000000000001",
      setCode: "tax.value_added",
      scope: "platform",
      effectiveFrom: "0001-01-01",
      approval: "platform_default",
      approvedBy: "",
      approvedOn: "",
      sourceRef: "افتراضُ منصّة غير مُعتمَد — لا مصدرَ نظاميّاً مُسمّى له.",
      values: [{ key: "standard_rate", kind: "rate", value: "0.15" }],
    },
    {
      id: "019de28d-0000-7000-8000-000000000002",
      setCode: "tax.value_added",
      scope: "tenant",
      effectiveFrom: "2026-06-01",
      approval: "tenant_approved",
      approvedBy: "مديرة المالية",
      approvedOn: "2026-05-20",
      sourceRef: "قرار مجلس الإدارة رقم 12",
      values: [{ key: "standard_rate", kind: "rate", value: "0.10" }],
    },
  ],
};

const REVIEW = {
  itemCount: 2,
  items: [
    { version: PARAMETERS.items[0], usageCount: 0, usages: [] },
    {
      version: PARAMETERS.items[1],
      usageCount: 1,
      usages: [
        {
          module: "Purchasing",
          documentType: "SUPPLIER_BILL",
          documentId: "019de28d-0000-7000-8000-0000000000aa",
          postedOn: "2026-06-09",
        },
      ],
    },
  ],
};

function fullRoutes(): Record<string, unknown> {
  return {
    ["GET " + AT + "/setup"]: SETUP,
    ["GET " + AT + "/capability-profile"]: PROFILE,
    ["GET " + AT + "/chart-of-accounts"]: CHART,
    ["GET " + AT + "/document-shapes/purchasing.supplier_bill"]: BILL_SHAPE,
    ["GET " + AT + "/document-shapes/sales.invoice"]: INVOICE_SHAPE,
    ["GET " + AT + "/parameters"]: PARAMETERS,
    ["GET " + AT + "/parameter-review"]: REVIEW,
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

async function type(element: HTMLInputElement, value: string): Promise<void> {
  await act(async () => {
    const proto = Object.getPrototypeOf(element) as object;
    /* الواصف على النموذج الأصلي عمداً: React يضع مُتعقِّباً على النسخة نفسها. */
    // eslint-disable-next-line @typescript-eslint/unbound-method
    const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
    if (setter) setter.call(element, value);
    else element.value = value;
    element.dispatchEvent(new Event("input", { bubbles: true }));
    await Promise.resolve();
  });
}

async function pick(element: HTMLSelectElement, value: string): Promise<void> {
  await act(async () => {
    const proto = Object.getPrototypeOf(element) as object;
    // eslint-disable-next-line @typescript-eslint/unbound-method
    const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
    if (setter) setter.call(element, value);
    else element.value = value;
    element.dispatchEvent(new Event("change", { bubbles: true }));
    await Promise.resolve();
  });
}

/** ينزع التعليقات: الحارس يفحص الشيفرة، والنثرُ الذي يصف امتناعاً ليس مخالفة. */
function stripComments(text: string): string {
  return text.replace(/\/\*[\s\S]*?\*\//g, " ").replace(/(^|[^:"'`])\/\/[^\n]*/g, "$1");
}

function button(testId: string): HTMLButtonElement {
  const found = screen.getByTestId(testId);
  if (!(found instanceof HTMLButtonElement)) throw new Error("ليس زرّاً: " + testId);
  return found;
}

function input(testId: string): HTMLInputElement {
  const found = screen.getByTestId(testId);
  if (!(found instanceof HTMLInputElement)) throw new Error("ليس حقلاً: " + testId);
  return found;
}

function select(testId: string): HTMLSelectElement {
  const found = screen.getByTestId(testId);
  if (!(found instanceof HTMLSelectElement)) throw new Error("ليست قائمة: " + testId);
  return found;
}

const SCREEN_FILES = [
  "CompanySetupScreen.tsx",
  "CostCentersScreen.tsx",
  "DocumentShapesScreen.tsx",
  "ChartOfAccountsScreen.tsx",
  "parts.tsx",
];

function sourceOf(file: string): string {
  return readFileSync(path.resolve(SRC, "screens/setup/" + file), "utf8");
}

beforeEach(() => {
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
   ١ · القوائم الثلاث تتّفق
   ═══════════════════════════════════════════════════════════════════════ */
describe("الملاحة اليدوية ونسختها في العقد", () => {
  it("كل شاشة تأسيسٍ في SCREENS لها رابطٌ في قائمة الملاحة اليدوية", async () => {
    await mount({ path: "/setup", transport: stub({ routes: fullRoutes() }) });
    const nav = document.querySelector(".app-side");
    expect(nav).not.toBeNull();
    const hrefs = [...(nav?.querySelectorAll("a[href]") ?? [])].map((a) => a.getAttribute("href"));
    const declared = SCREENS.filter((s) => s.path.startsWith("/setup")).map((s) => s.path);
    expect(declared).toEqual(SETUP_PATHS);
    for (const target of declared) expect(hrefs).toContain(target);
  });

  it("والشريط داخل المجموعة يحمل الخمس نفسها — لا رابعةً ولا سادسة", async () => {
    await mount({ path: "/setup/cost-centers", transport: stub({ routes: fullRoutes() }) });
    const tabs = await screen.findByTestId("setup-tabs");
    const inside = [...tabs.querySelectorAll("a[href]")].map((a) => a.getAttribute("href"));
    expect(inside).toEqual(SETUP_PATHS);
  });

  it("وكل مسارٍ من الخمسة يفتح شاشته في الموجّه", async () => {
    const expected = [
      "setup-company-screen",
      "setup-cost-centers-screen",
      "setup-document-shapes-screen",
      "setup-chart-screen",
      "setup-parameters-screen",
    ];
    for (let i = 0; i < SETUP_PATHS.length; i += 1) {
      const at = SETUP_PATHS[i] as string;
      await mount({ path: at, transport: stub({ routes: fullRoutes() }) });
      expect(await screen.findByTestId(expected[i] as string)).toBeTruthy();
      cleanup();
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · العرض على الملفّ حكمٌ لا كتابة
   ═══════════════════════════════════════════════════════════════════════ */
describe("admitDocument حكمٌ لا كتابة", () => {
  it("الضغط يُصدر طلباً واحداً إلى مسار الأحكام، وجسمُه أسماءٌ فقط", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/document-shapes",
      transport: stub({
        routes: {
          ...fullRoutes(),
          ["POST " + AT + "/document-shapes/purchasing.supplier_bill/admissions"]: {
            admitted: true,
            documentType: "purchasing.supplier_bill",
            fields: ["currency", "supplier"],
          },
        },
        sent,
      }),
    });
    await waitFor(() => expect(screen.getByTestId("setup-shape-type")).toBeTruthy());
    await pick(select("setup-shape-type"), "purchasing.supplier_bill");
    await waitFor(() => expect(screen.getByTestId("setup-shape-copy-fields")).toBeTruthy());

    await type(input("setup-shape-field-input"), "supplier");
    await click(button("setup-shape-add-field"));

    const before = sent.length;
    await click(button("setup-shape-admit-go"));
    await waitFor(() => expect(screen.getByTestId("setup-shape-verdict")).toBeTruthy());

    const after = sent.slice(before);
    const writes = after.filter((r) => r.method !== "GET");
    expect(writes).toHaveLength(1);
    const only = writes[0] as Recorded;
    expect(only.url).toContain("/document-shapes/purchasing.supplier_bill/admissions");
    /* أسماءٌ فقط — ولا حقل قيمةٍ يعبر */
    expect(Object.keys(only.body as object)).toEqual(["fields"]);
    expect((only.body as { fields: string[] }).fields).toEqual(["supplier"]);
  });

  it("والرفض يخرج مشكلةً بالرمز 422 لا حكماً في حقل", async () => {
    await mount({
      path: "/setup/document-shapes",
      transport: stub({
        routes: fullRoutes(),
        refuse: {
          ["POST " + AT + "/document-shapes/purchasing.supplier_bill/admissions"]: {
            status: 422,
            code: "document.field_not_licensed",
          },
        },
      }),
    });
    await waitFor(() => expect(screen.getByTestId("setup-shape-type")).toBeTruthy());
    await pick(select("setup-shape-type"), "purchasing.supplier_bill");
    await click(button("setup-shape-admit-go"));
    const panel = await screen.findByTestId("problem-panel");
    expect(panel.getAttribute("data-code")).toBe("document.field_not_licensed");
    expect(screen.queryByTestId("setup-shape-verdict")).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · قدرةٌ مُطفأة، وحقلٌ خارج الشكل — يُقالان قبل الضغط
   ═══════════════════════════════════════════════════════════════════════ */
describe("القدرةُ المُطفأة تُسمّى، والغريبُ يُسمّى قبل الضغط", () => {
  it("المُطفأة معروضةٌ باسمها ولا تُحذف من القائمة", async () => {
    await mount({ path: "/setup/document-shapes", transport: stub({ routes: fullRoutes() }) });
    await waitFor(() => expect(screen.getByTestId("setup-shape-type")).toBeTruthy());
    await pick(select("setup-shape-type"), "purchasing.supplier_bill");
    await waitFor(() => expect(screen.getByTestId("setup-shape-capabilities")).toBeTruthy());
    /* المتاح ثلاثٌ والمُشغَّل واحدة — والمُطفأتان معروضتان بأسمائهما. */
    expect(screen.getByTestId("setup-shape-cap-landed_cost")).toBeTruthy();
    expect(screen.getByTestId("setup-shape-cap-retention")).toBeTruthy();
    expect(screen.getByTestId("setup-shape-cap-state-landed_cost").textContent).not.toBe("");
    expect(screen.getByTestId("setup-shape-off-warning")).toBeTruthy();
  });

  it("واسمٌ خارج الشكل يُسمّى قبل الضغط ولا يغادر عنه طلب", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/document-shapes",
      transport: stub({ routes: fullRoutes(), sent }),
    });
    await waitFor(() => expect(screen.getByTestId("setup-shape-type")).toBeTruthy());
    await pick(select("setup-shape-type"), "purchasing.supplier_bill");
    await waitFor(() => expect(screen.getByTestId("setup-shape-field-input")).toBeTruthy());

    /* حقلٌ ترخّصه قدرةٌ مُطفأة (retention) — فليس في شكل هذا الملفّ. */
    await type(input("setup-shape-field-input"), "retentionPercent");
    await click(button("setup-shape-add-field"));

    const strangers = await screen.findByTestId("setup-shape-strangers");
    expect(strangers.textContent).toContain("retentionPercent");
    expect(screen.getByTestId("setup-shape-strangers-cause").textContent).toContain("retention");
    /* ولا طلبَ كتابةٍ غادر لقول ذلك. */
    expect(sent.filter((r) => r.method !== "GET")).toHaveLength(0);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ · التعطيل حالةٌ تُقرأ لا غياب
   ═══════════════════════════════════════════════════════════════════════ */
describe("الموقوف يبقى في الجدول", () => {
  it("صفُّ المركز الموقوف موجودٌ بحالته، ولا مرشّحَ افتراضي يُخفيه", async () => {
    await mount({ path: "/setup/cost-centers", transport: stub({ routes: fullRoutes() }) });
    const row = await screen.findByTestId("setup-cc-row-cc.closed");
    expect(row.getAttribute("data-state")).toBe("Suspended");
    expect(screen.getByTestId("setup-cc-reason-cc.closed").textContent).not.toBe("");
    /* والثلاثة كلُّها معروضة: العامل والموقوف معاً. */
    expect(screen.getByTestId("setup-cc-row-cc.main")).toBeTruthy();
    expect(screen.getByTestId("setup-cc-row-cc.branch")).toBeTruthy();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٥ · المركز الافتراضي لا يُوقَف — ويُقال قبل الضغط
   ═══════════════════════════════════════════════════════════════════════ */
describe("رفضُ إيقاف الافتراضي يُقال باسمه قبل الضغط", () => {
  it("اختيارُ الافتراضي يُقفل الزرّ ويسمّي الرمز، ولا طلبَ يغادر", async () => {
    const sent: Recorded[] = [];
    await mount({ path: "/setup/cost-centers", transport: stub({ routes: fullRoutes(), sent }) });
    await click(await screen.findByTestId("setup-cc-pick-cc.main"));
    const blocked = await screen.findByTestId("setup-cc-suspend-blocked");
    expect(blocked.getAttribute("data-code")).toBe("cost_center.default_cannot_be_suspended");
    expect(blocked.textContent).toContain("cost_center.default_cannot_be_suspended");
    expect(button("setup-cc-suspend-go").disabled).toBe(true);
    expect(sent.filter((r) => r.method !== "GET")).toHaveLength(0);
  });

  it("والمركز الموقوف فعلاً يُقال عنه ذلك برمزه هو لا برمز الافتراضي", async () => {
    await mount({ path: "/setup/cost-centers", transport: stub({ routes: fullRoutes() }) });
    await click(await screen.findByTestId("setup-cc-pick-cc.closed"));
    const blocked = await screen.findByTestId("setup-cc-suspend-blocked");
    expect(blocked.getAttribute("data-code")).toBe("cost_center.already_suspended");
  });

  it("والعامل غير الافتراضي يُوقَف بسببٍ مكتوب، والسبب وحده يعبر في الجسم", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/cost-centers",
      transport: stub({
        routes: {
          ...fullRoutes(),
          ["POST " + AT + "/cost-centers/cc.branch/suspension"]: SETUP,
        },
        sent,
      }),
    });
    await click(await screen.findByTestId("setup-cc-pick-cc.branch"));
    /* سببٌ أقصر من ثمانية محارف يُقفل الزرّ ولا يُرسَل. */
    await type(input("setup-cc-reason"), "قصير");
    expect(button("setup-cc-suspend-go").disabled).toBe(true);
    await type(input("setup-cc-reason"), "أُغلق الفرع عند إقفال الفترة");
    expect(button("setup-cc-suspend-go").disabled).toBe(false);
    await click(button("setup-cc-suspend-go"));
    await waitFor(() =>
      expect(sent.some((r) => r.method === "POST" && r.url.includes("/suspension"))).toBe(true)
    );
    const wrote = sent.find((r) => r.method === "POST" && r.url.includes("/suspension"));
    expect(Object.keys(wrote?.body as object)).toEqual(["reason"]);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٦ · الرمز يعبر في المسار وحده
   ═══════════════════════════════════════════════════════════════════════ */
describe("الرمز يسكّه الخادم ولا يُرسله العميل", () => {
  it("جسمُ الإضافة اسمٌ وترجمات لا رمز", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/cost-centers",
      transport: stub({ routes: { ...fullRoutes(), ["POST " + AT + "/cost-centers"]: SETUP }, sent }),
    });
    await waitFor(() => expect(screen.getByTestId("setup-cc-add-name")).toBeTruthy());
    await type(input("setup-cc-add-name"), "مركزٌ جديدٌ للقياس");
    await click(button("setup-cc-add-go"));
    await waitFor(() => expect(sent.some((r) => r.method === "POST")).toBe(true));
    const wrote = sent.find((r) => r.method === "POST");
    expect(Object.keys(wrote?.body as object).sort()).toEqual(["nameAr", "nameTranslations"]);
  });

  it("وإعادة التسمية تحمل الرمز في المسار ولا تحمله في الجسم", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/cost-centers",
      transport: stub({
        routes: { ...fullRoutes(), ["PUT " + AT + "/cost-centers/cc.branch"]: SETUP },
        sent,
      }),
    });
    await click(await screen.findByTestId("setup-cc-pick-cc.branch"));
    await type(input("setup-cc-new-name"), "اسمٌ جديدٌ للقياس");
    await click(button("setup-cc-rename-go"));
    await waitFor(() => expect(sent.some((r) => r.method === "PUT")).toBe(true));
    const wrote = sent.find((r) => r.method === "PUT");
    expect(wrote?.url).toContain("/cost-centers/cc.branch");
    expect(Object.keys(wrote?.body as object).sort()).toEqual(["nameAr", "nameTranslations"]);
  });

  it("والوسم العربي يُرفض قبل الضغط باسم رمز الخادم", async () => {
    await mount({ path: "/setup/cost-centers", transport: stub({ routes: fullRoutes() }) });
    await waitFor(() => expect(screen.getByTestId("setup-cc-add-translations-tag")).toBeTruthy());
    await type(input("setup-cc-add-translations-tag"), "ar");
    await type(input("setup-cc-add-translations-text"), "اسمٌ عربيٌّ ثانٍ");
    expect(button("setup-cc-add-translations-add").disabled).toBe(true);
    expect(screen.getByTestId("setup-cc-add-translations").textContent).toContain(
      "company_setup.arabic_is_not_a_translation"
    );
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٧ · التأسيس يقع مرّةً
   ═══════════════════════════════════════════════════════════════════════ */
describe("التأسيس مرّةً واحدة", () => {
  it("منشأةٌ مؤسَّسة: لا نموذجَ ثانٍ، والرمز مُسمّى", async () => {
    await mount({ path: "/setup", transport: stub({ routes: fullRoutes() }) });
    const already = await screen.findByTestId("setup-company-already");
    expect(already.textContent).toContain("company_setup.already_initialised");
    expect(screen.queryByTestId("setup-company-found")).toBeNull();
    expect(screen.getByTestId("setup-company-default").textContent).toBe("cc.main");
  });

  it("ومنشأةٌ لم تُؤسَّس: النموذج يُرسم ولا يُعرض لوح رفض", async () => {
    await mount({
      path: "/setup",
      transport: stub({
        routes: {},
        refuse: { ["GET " + AT + "/setup"]: { status: 404, code: "company_setup.not_found" } },
      }),
    });
    expect(await screen.findByTestId("setup-company-found")).toBeTruthy();
    expect(screen.queryByTestId("problem-panel")).toBeNull();
    expect(screen.getByTestId("setup-company-unfounded")).toBeTruthy();
  });

  it("والجواب «واحد» لا يُرسل اسمَ أوّلِ مركز، و«عدّة» يوجبه", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup",
      transport: stub({
        routes: { ["PUT " + AT + "/setup"]: SETUP },
        refuse: { ["GET " + AT + "/setup"]: { status: 404, code: "company_setup.not_found" } },
        sent,
      }),
    });
    await waitFor(() => expect(screen.getByTestId("setup-company-name-input")).toBeTruthy());
    await type(input("setup-company-name-input"), "منشأةٌ للقياس");
    /* «واحد»: الحقل غير مرسوم أصلاً، والسبب مكتوبٌ برمزه. */
    expect(screen.queryByTestId("setup-company-first")).toBeNull();
    expect(screen.getByTestId("setup-company-first-absent").textContent).toContain(
      "company_setup.first_cost_center_name_not_expected"
    );

    await pick(select("setup-company-answer"), "Multiple");
    expect(button("setup-company-found").disabled).toBe(true);
    await type(input("setup-company-first"), "أوّلُ مركزٍ للقياس");
    expect(button("setup-company-found").disabled).toBe(false);

    await click(button("setup-company-found"));
    await waitFor(() => expect(sent.some((r) => r.method === "PUT")).toBe(true));
    const wrote = sent.find((r) => r.method === "PUT")?.body as Record<string, unknown>;
    expect(wrote.costCenters).toBe("Multiple");
    expect(wrote.firstCostCenterNameAr).toBe("أوّلُ مركزٍ للقياس");
    /* وعددُ الخانات صحيحٌ لا نصّ: العقد يُعلنه integer. */
    expect(typeof wrote.decimalPlaces).toBe("number");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٨ · الاستبدال كلّي، والسحب يوجب سبباً
   ═══════════════════════════════════════════════════════════════════════ */
describe("كتابةُ الملفّ استبدالٌ كلّي", () => {
  it("الجسم يحمل النوعين معاً وقيمَهما الافتراضية كما وصلت", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/document-shapes",
      transport: stub({
        routes: { ...fullRoutes(), ["PUT " + AT + "/capability-profile"]: PROFILE },
        sent,
      }),
    });
    await waitFor(() => expect(screen.getByTestId("setup-shape-save")).toBeTruthy());
    await click(button("setup-shape-save"));
    await waitFor(() => expect(sent.some((r) => r.method === "PUT")).toBe(true));
    const wrote = sent.find((r) => r.method === "PUT")?.body as {
      documents: { documentType: string; defaults: { name: string; value: string }[] }[];
    };
    expect(wrote.documents.map((d) => d.documentType).sort()).toEqual([
      "purchasing.supplier_bill",
      "sales.invoice",
    ]);
    const bill = wrote.documents.find((d) => d.documentType === "purchasing.supplier_bill");
    expect(bill?.defaults).toEqual([{ name: "currency", value: "SAR" }]);

  });

  it("وإطفاءُ قدرةٍ كانت مُشغَّلة يُسمّيها ويوجب سبباً قبل الضغط", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/document-shapes",
      transport: stub({
        routes: { ...fullRoutes(), ["PUT " + AT + "/capability-profile"]: PROFILE },
        sent,
      }),
    });
    const key = "purchasing.supplier_bill/three_way_match";
    await click(await screen.findByTestId("setup-shape-switch-" + key));
    const panel = await screen.findByTestId("setup-shape-withdrawals");
    expect(panel.textContent).toContain(key);
    /* السحبُ مُعلَنٌ والسببُ لم يُكتب بعد — فالزرّ مُقفَلٌ لنقص المُدخَل. */
    expect(button("setup-shape-save").disabled).toBe(true);
    await type(input("setup-shape-reason"), "قصير");
    expect(button("setup-shape-save").disabled).toBe(true);
    await type(input("setup-shape-reason"), "أُوقفت المطابقة الثلاثية بقرار الإقفال");
    expect(button("setup-shape-save").disabled).toBe(false);
    await click(button("setup-shape-save"));
    await waitFor(() => expect(sent.some((r) => r.method === "PUT")).toBe(true));
    const wrote = sent.find((r) => r.method === "PUT")?.body as { withdrawalReason?: string };
    expect(wrote.withdrawalReason).toBe("أُوقفت المطابقة الثلاثية بقرار الإقفال");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٩ · العدّادان يكشفان النقص
   ═══════════════════════════════════════════════════════════════════════ */
describe("دليل الحسابات", () => {
  it("العدد المُعلَن يطابق الواصل فلا لوحَ نقص", async () => {
    await mount({ path: "/setup/chart-of-accounts", transport: stub({ routes: fullRoutes() }) });
    await waitFor(() => expect(screen.getByTestId("setup-coa-table")).toBeTruthy());
    expect(screen.queryByTestId("setup-coa-short")).toBeNull();
    /* والشجرة تُبنى بالمستوى الواصل، والأب التجميعي معروضٌ مع أوراقه. */
    expect(screen.getByTestId("setup-coa-row-A").getAttribute("data-level")).toBe("1");
    expect(screen.getByTestId("setup-coa-row-A-1").getAttribute("data-level")).toBe("4");
  });

  it("وعددٌ مُعلَنٌ يخالف الواصل يُعلَن نقصاً", async () => {
    await mount({
      path: "/setup/chart-of-accounts",
      transport: stub({
        routes: { ...fullRoutes(), ["GET " + AT + "/chart-of-accounts"]: { ...CHART, accountCount: 9 } },
      }),
    });
    expect(await screen.findByTestId("setup-coa-short")).toBeTruthy();
  });

  it("وشروطُ الترحيل معروضةٌ على الحساب نفسه: الطرف والبُعد", async () => {
    await mount({ path: "/setup/chart-of-accounts", transport: stub({ routes: fullRoutes() }) });
    await waitFor(() => expect(screen.getByTestId("setup-coa-table")).toBeTruthy());
    expect(screen.getByTestId("setup-coa-subledger-A-1").textContent).toBe("customer");
    expect(screen.getByTestId("setup-coa-dim-A-1-cost_center")).toBeTruthy();
    /* والمعطَّل يبقى معروضاً موسوماً، ولا يُخفى خلف مرشّحٍ افتراضي. */
    expect(screen.getByTestId("setup-coa-inactive-A-2")).toBeTruthy();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ١٠ · حرّاسٌ نصّية على الشيفرة نفسها
   ═══════════════════════════════════════════════════════════════════════ */
describe("حرّاسٌ على شيفرة القسم", () => {
  it("كل حقلٍ في صفٍّ يحمل وصفاً أو رفضاً (ADR-0078)", () => {
    const problems: string[] = [];
    for (const file of SCREEN_FILES) {
      const text = stripComments(sourceOf(file));
      /* `SetupField` وحده: الأوّليّة `Field` تُلَفّ في `parts.tsx` بنشرٍ
         يمرّر `hint` أو `error`، فمطابقتُها هناك تقيس اللفّافة لا الحقل. */
      const fields = text.match(/<SetupField\b[\s\S]*?>/g) ?? [];
      fields.forEach((one, index) => {
        if (!/\bhint=/.test(one) && !/\berror=/.test(one)) {
          problems.push(file + " · حقل رقم " + String(index + 1));
        }
      });
    }
    expect(problems).toEqual([]);
  });

  it("والوعاء هو `.grid` المُسجَّل في components.css لا وعاءٌ يُخترَع", () => {
    const registered = readFileSync(path.resolve(SRC, "styles/components.css"), "utf8");
    /* القاعدة تُقرأ من ورقة الأنماط نفسها، فلا تنحرف نسخةٌ مكتوبة في الاختبار. */
    const line = registered.match(/:is\(([^)]*)\) > \*\{grid-row:span 3\}/);
    expect(line).not.toBeNull();
    const containers = (line?.[1] ?? "").split(",").map((c) => c.trim().replace(/^\./, ""));
    expect(containers).toContain("grid");

    const problems: string[] = [];
    for (const file of SCREEN_FILES) {
      const text = stripComments(sourceOf(file));
      for (const m of text.matchAll(/className="([^"]*\bfields-[^"]*)"/g)) {
        const classes = m[1] ?? "";
        const tokens = classes.split(/\s+/);
        if (!tokens.some((token) => containers.includes(token))) problems.push(file + " · " + classes);
      }
    }
    expect(problems).toEqual([]);
  });

  it("ولا محوّل عددي على مالٍ ولا على معرّف", () => {
    const problems: string[] = [];
    for (const file of SCREEN_FILES) {
      const text = stripComments(sourceOf(file));
      if (/\bparseFloat\b|\bparseInt\b|\bNumber\s*\(/.test(text)) problems.push(file);
    }
    expect(problems).toEqual([]);
  });

  it("ولا رقمَ حسابٍ مكتوبٍ في شيفرة هذا القسم", () => {
    /* رمزُ الحساب يأتي من الخادم، ومصفوفةُ الترحيل هي التي تقرّر. وشكلُ رقم
       الحساب نصٌّ **كلُّه أرقامٌ وفواصل** وفيه ثلاثة أرقام فأكثر — ورمزُ
       حارسٍ مثل `guard.GR-COA-002` ليس منه لأنه يحمل حروفاً. */
    const looksLikeAccountCode = (literal: string): boolean =>
      /^[0-9.-]+$/.test(literal) && (literal.match(/[0-9]/g) ?? []).length >= 3;
    const problems: string[] = [];
    for (const file of SCREEN_FILES) {
      const text = stripComments(sourceOf(file));
      for (const m of text.matchAll(/"([^"]*)"/g)) {
        const literal = m[1] ?? "";
        if (looksLikeAccountCode(literal)) problems.push(file + " · " + literal);
      }
    }
    /* شاهدٌ إيجابي: الكاشف يلتقط شكلَ رقم الحساب حين يُعرَض عليه. */
    expect(looksLikeAccountCode("1302")).toBe(true);
    expect(looksLikeAccountCode("guard.GR-COA-002")).toBe(false);
    expect(problems).toEqual([]);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٦ · المعامِلات: الحالةُ تُرى، والإيداعُ يُرسل المجموعة كاملةً
   ═══════════════════════════════════════════════════════════════════════ */
describe("شاشة المعامِلات", () => {
  it("افتراضُ المنصّة يُعرض موسوماً «غير مُعتمَد» ولا يُخفى، وبلا معتمِد", async () => {
    await mount({ path: "/setup/parameters", transport: stub({ routes: fullRoutes() }) });

    const platform = await screen.findByTestId(
      "setup-parameters-approval-0f6a1d00-0000-4000-8000-000000000001"
    );
    expect(platform.textContent).toContain("غير مُعتمَد");

    /* ‏**والصفّ موجودٌ في الجدول لا محذوفٌ منه** — وهذا هو الفرق كلّه: قيمةٌ
       تعمل ولا تُرى تُرحَّل بها المنشأة وهي لا تعرف أنها مفترَضة. */
    expect(
      screen.getByTestId("setup-parameters-noapprover-0f6a1d00-0000-4000-8000-000000000001")
    ).toBeTruthy();

    /* والعدّاد يقول العدد نفسه، فلا يقرأ أحدٌ الجدول ليعدّ. */
    expect(screen.getByTestId("setup-parameters-count-unapproved").textContent).toContain("1");
  });

  it("والقيمة تُعرض كما وصلت — كسراً لا مئوية، وبلا ضربٍ في مئة", async () => {
    await mount({ path: "/setup/parameters", transport: stub({ routes: fullRoutes() }) });

    const cell = await screen.findByTestId(
      "setup-parameters-value-0f6a1d00-0000-4000-8000-000000000001-standard_rate"
    );

    /* ‏٠٫١٥ بأرقام اللغة، **ولا علامة ٪**: علامةٌ على كسرٍ تجعل «0.15» تُقرأ
       خمس عشرة بالمئة من واحد. */
    expect(cell.textContent).not.toContain("%");
    expect(cell.getAttribute("data-rate") ?? cell.textContent).toBeTruthy();
  });

  it("والإيداع يُرسل مجموعةً كاملةً إلى بابها، ولا يرسل حالةَ «افتراضُ منصّة»", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/setup/parameters",
      transport: stub({
        routes: {
          ...fullRoutes(),
          ["POST " + AT + "/parameters"]: PARAMETERS.items[1],
        },
        sent,
      }),
    });

    await waitFor(() => expect(screen.getByTestId("setup-parameters-set")).toBeTruthy());
    await pick(select("setup-parameters-set"), "tax.value_added");

    await waitFor(() => expect(screen.getByTestId("setup-parameters-input-standard_rate")).toBeTruthy());

    await type(input("setup-parameters-effective"), "2026-07-01");
    await type(input("setup-parameters-approved-by"), "مديرة المالية");
    await type(input("setup-parameters-approved-on"), "2026-06-20");
    await type(input("setup-parameters-source"), "قرار مجلس الإدارة رقم 13");
    await type(input("setup-parameters-input-standard_rate"), "0.12");

    const before = sent.length;
    await click(button("setup-parameters-submit"));

    await waitFor(() => expect(sent.length).toBeGreaterThan(before));
    const writes = sent.slice(before).filter((r) => r.method !== "GET");
    expect(writes).toHaveLength(1);

    const body = writes[0]?.body as Record<string, unknown>;
    expect(body["setCode"]).toBe("tax.value_added");
    expect(body["approval"]).toBe("tenant_approved");
    expect((body["values"] as unknown[]).length).toBe(1);
  });

  it("وقائمةُ المراجعة تُقرأ من بابها، وتُسمّي المستند المُرحَّل الذي استعمل الإصدار", async () => {
    await mount({ path: "/setup/parameters", transport: stub({ routes: fullRoutes() }) });

    /* الإصدارُ الذي لم يستعمله مستندٌ بعد يبقى في القائمة — وحاجتُه إلى
       التوقيع لا تسقط بعدم استعماله. */
    expect(
      await screen.findByTestId("setup-parameters-unused-0f6a1d00-0000-4000-8000-000000000001")
    ).toBeTruthy();

    const used = await screen.findByTestId(
      "setup-parameters-review-row-019de28d-0000-7000-8000-000000000002"
    );
    expect(used.textContent).toContain("SUPPLIER_BILL");
  });
});
