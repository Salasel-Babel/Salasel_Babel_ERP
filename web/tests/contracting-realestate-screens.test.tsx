/* ═══════════════════════════════════════════════════════════════════════════
   الشاشات الأربع الجديدة في المقاولات والعقارات — حرّاسها
   The four new contracting and real-estate screens — their guards
   ───────────────────────────────────────────────────────────────────────────
   سبعةٌ تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحص:

     ١ · **القوائم الثلاث تتّفق.** `SCREENS` وشريطُ القسم وقائمةُ الملاحة
         اليدوية ثلاثُ نسخٍ لشيءٍ واحد. وسبعُ شاشاتٍ قائمة في هذين القسمين
         كانت **غائبةً عن الملاحة اليدوية كلَّها** ولم يُشتكَ منها، لأن لوحة
         الأوامر تفتحها. والحارس الذي كان يفحص الموارد البشرية وحدها يفحص
         هنا القسمين الآخرين بالمعيار نفسه.
     ٢ · **الأبواب اليتيمة لها بيت.** ‏`readChangeOrder` و`readSubcontractorAdvance`
         و`addGuarantee` كانت منشورةً ولا يبلغها شيء. والفحص **يرصد الطلب على
         السلك** لا وجودَ زرّ: زرٌّ لا يُصدِر طلباً ليس باباً بلغه أحد.
     ٣ · **المال يغادر السلك نصّاً** بايتاً ببايت، وخانتُه الرابعة هي ما
         يفقده العائم.
     ٤ · **المسوّدة ثمّ الترحيل خطوتان**، وإعادةُ الترحيل تقول «رُدّ إليك
         القيد الأول» ولا تُظهر نجاحاً ثانياً على عملٍ لم يقع.
     ٥ · **ما لا يُرحَّل يُقال إنه لا يُرحَّل.** الأمر التغييري وخطاب الضمان
         لا يحملان `entryId` ولا `alreadyPosted` في العقد المُولَّد — والفحص
         يقرأ ذلك **من العقد** لا من قائمةٍ مكتوبة هنا، فيوم يُنشر لهما ترحيل
         يسقط الفحص بدل أن تبقى الشاشة تكذب.
     ٦ · **واحدٌ من الاثنين**: الضمان يخصّ عقد عميلٍ أو عقد باطن، والآخر
         يعبر السلك `null` صراحةً لا حقلاً فارغاً.
     ٧ · **صفوف الحقول تقف على مسارات وعاءٍ مُعلَن**، والقائمة تُقرأ من
         `components.css` نفسها لا مكتوبةً هنا.

   **ولا بيان شخصي في هذا الملفّ**: لا اسم مستأجرٍ ولا مالك، ولا رقم هوية،
   ولا آيبان، ولا عنوان — والمعرّفات كلّها أصفارٌ لا تدلّ على أحد.
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
import { resetContractingSelection } from "../src/screens/contracting/selection";
import type { RawResponse, Transport } from "../src/api/transport";

const WEB = path.resolve(__dirname, "..");
const COMPANY = "11111111-1111-1111-1111-111111111111";
const BASE = "/api/v1/companies/" + COMPANY;
const HEALTH = { apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" };

/** مبلغٌ بأربع منازل، خانتُه الأخيرة هي ما يفقده العائم. */
const EXACT_WIRE = "1000000000000.4013";

const CONTRACT_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1";
const SUBCONTRACT_ID = "cccccccc-cccc-4ccc-8ccc-ccccccccccc1";
const ADVANCE_ID = "dddddddd-dddd-4ddd-8ddd-ddddddddddd1";
const ORDER_ID = "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1";
const GUARANTEE_ID = "ffffffff-ffff-4fff-8fff-fffffffffff1";

const CHANGE_ORDER = {
  id: ORDER_ID,
  number: "CO-2026-0001",
  contractId: CONTRACT_ID,
  issuedOn: "2026-04-01",
  reasonAr: "تعديل نطاقٍ باعتماد الاستشاري",
  approvedBy: "استشاري المشروع",
  addedItems: [
    {
      id: "11111111-1111-4111-8111-11111111aaa1",
      lineNo: 1,
      code: "B-01",
      descriptionAr: "خرسانة إضافية",
      contractQuantity: { magnitude: "120.000000", unit: "M3" },
      unitRate: "250.0000",
      changeOrderId: ORDER_ID,
    },
  ],
};

const GUARANTEE = {
  id: GUARANTEE_ID,
  number: "LG-2026-0001",
  kind: "performance",
  issuerNameAr: "مصرفٌ محلّي",
  amount: EXACT_WIRE,
  effectiveFrom: "2026-01-01",
  expiresOn: "2027-01-01",
  attachmentId: "att-0000000000000001",
  contractId: CONTRACT_ID,
  subcontractId: null,
};

function advance(over: { state: string; entryId: string | null; alreadyPosted: boolean }) {
  return {
    id: ADVANCE_ID,
    number: "ADV-2026-0001",
    amount: EXACT_WIRE,
    ...over,
  };
}

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
  /* الواصف على النموذج الأصلي عمداً — React يضع مُتعقِّباً على النسخة نفسها. */
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

async function choose(element: HTMLSelectElement, value: string): Promise<void> {
  await act(async () => {
    setNativeValue(element, value);
    element.dispatchEvent(new Event("change", { bubbles: true }));
    await Promise.resolve();
  });
}

beforeEach(() => {
  resetContractingSelection();
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
  for (const section of ["contracting", "realestate"] as const) {
    it("كل شاشة في «" + section + "» لها رابطٌ في قائمة الملاحة اليدوية — والحارس كان يفحص الموارد البشرية وحدها", async () => {
      await mount({ path: "/", transport: stub({ routes: { "GET /health": HEALTH } }) });
      const nav = document.querySelector(".app-side");
      expect(nav).not.toBeNull();
      const hrefs = [...(nav?.querySelectorAll("a[href]") ?? [])].map((a) => a.getAttribute("href"));
      const declared = SCREENS.filter((s) => s.section === section).map((s) => s.path);
      expect(declared.length).toBeGreaterThan(3);
      for (const target of declared) expect(hrefs).toContain(target);
    });
  }

  it("وشريطُ المقاولات داخل القسم يحمل السبع نفسها — لا سادسةً ولا ثامنة", async () => {
    await mount({
      path: "/contracting/guarantees",
      transport: stub({ routes: { "GET /health": HEALTH } }),
    });
    const strip = await screen.findByTestId("contracting-nav");
    const inside = [...strip.querySelectorAll("a[href]")].map((a) => a.getAttribute("href"));
    const declared = SCREENS.filter((s) => s.section === "contracting").map((s) => s.path);
    expect([...inside].sort()).toEqual([...declared].sort());
  });

  it("وشريطُ العقارات يحمل الأربع نفسها", async () => {
    await mount({ path: "/realestate/parties", transport: stub({ routes: { "GET /health": HEALTH } }) });
    const tabs = [...document.querySelectorAll(".re-tabs a[href]")].map((a) => a.getAttribute("href"));
    const declared = SCREENS.filter((s) => s.section === "realestate").map((s) => s.path);
    expect([...tabs].sort()).toEqual([...declared].sort());
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · الأبواب اليتيمة لها بيت — والفحص يرصد الطلب لا الزرّ
   ═══════════════════════════════════════════════════════════════════════ */
describe("الأبواب التي لم يكن يبلغها شيء", () => {
  it("readChangeOrder يُطلَب فعلاً من شاشة أوامر التغيير، وبنودُ الأمر تُعرَض", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/contracting/change-orders",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/change-orders/" + ORDER_ID]: CHANGE_ORDER },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("co-read-id"), ORDER_ID);
    await click(screen.getByTestId("co-read-go"));
    await waitFor(() => expect(screen.getByTestId("co-read-out")).toBeTruthy());

    expect(sent.some((r) => r.method === "GET" && r.url === BASE + "/change-orders/" + ORDER_ID)).toBe(true);
    expect(screen.getByTestId("change-order-number").textContent).toBe("CO-2026-0001");
    expect(screen.getByTestId("change-order-approver").textContent).toBe("استشاري المشروع");
    /* الكمّية بمقياسها كما وصلت — ليُقارَن العمود بعمود جدول الكميات. */
    expect(screen.getByTestId("co-read-items").textContent).toContain("120.000000");
  });

  it("readSubcontractorAdvance يُطلَب فعلاً، ومسوّدةٌ أُعيد فتحها تُرحَّل من مكانها", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/contracting/advances",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/subcontractor-advances/" + ADVANCE_ID]: advance({
            state: "DRAFT",
            entryId: null,
            alreadyPosted: false,
          }),
          ["POST " + BASE + "/subcontractor-advances/" + ADVANCE_ID + "/posting"]: advance({
            state: "POSTED",
            entryId: "99999999-9999-4999-8999-999999999991",
            alreadyPosted: false,
          }),
        },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ad-open-id"), ADVANCE_ID);
    await click(screen.getByTestId("ad-open-go"));
    await waitFor(() => expect(screen.getByTestId("ad-open-out")).toBeTruthy());
    expect(sent.some((r) => r.method === "GET" && r.url === BASE + "/subcontractor-advances/" + ADVANCE_ID)).toBe(true);

    await click(screen.getByTestId("ad-open-post"));
    await waitFor(() =>
      expect(screen.getByTestId("ad-open-receipt").getAttribute("data-already-posted")).toBe("false")
    );
  });

  it("addGuarantee يُطلَب فعلاً — وهو الباب الذي لم تكن تستدعيه شاشةٌ واحدة", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/contracting/guarantees",
      transport: stub({ routes: { "GET /health": HEALTH, ["POST " + BASE + "/guarantees"]: GUARANTEE }, sent }),
    });
    await fillGuarantee();
    await click(screen.getByTestId("guarantee-save"));
    await waitFor(() => expect(screen.getByTestId("guarantee-session-list")).toBeTruthy());
    expect(sent.some((r) => r.method === "POST" && r.url === BASE + "/guarantees")).toBe(true);
  });
});

/** يملأ نموذج الضمان بأدنى ما يقبله، على عقد باطنٍ فلا يحتاج اختيار عقد. */
async function fillGuarantee(): Promise<void> {
  await choose(await screen.findByTestId<HTMLSelectElement>("gu-holder"), "subcontract");
  await type(screen.getByTestId<HTMLInputElement>("gu-subcontract"), SUBCONTRACT_ID);
  await type(screen.getByTestId<HTMLInputElement>("gu-number"), "LG-2026-0001");
  await type(screen.getByTestId<HTMLInputElement>("gu-kind"), "performance");
  await type(screen.getByTestId<HTMLInputElement>("gu-issuer"), "مصرفٌ محلّي");
  await type(screen.getByTestId<HTMLInputElement>("gu-amount"), EXACT_WIRE);
  await type(screen.getByTestId<HTMLInputElement>("gu-from"), "2026-01-01");
  await type(screen.getByTestId<HTMLInputElement>("gu-to"), "2027-01-01");
  await type(screen.getByTestId<HTMLInputElement>("gu-attachment"), "att-0000000000000001");
}

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · المال نصٌّ على السلك · ٦ · واحدٌ من الاثنين
   ═══════════════════════════════════════════════════════════════════════ */
describe("المال نصّ، والحاملُ واحدٌ من اثنين", () => {
  it("مبلغ الضمان يغادر السلك نصّاً بايتاً ببايت، والحاملُ الآخر null صراحةً", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/contracting/guarantees",
      transport: stub({ routes: { "GET /health": HEALTH, ["POST " + BASE + "/guarantees"]: GUARANTEE }, sent },),
    });
    await fillGuarantee();
    await click(screen.getByTestId("guarantee-save"));
    await waitFor(() => expect(screen.getByTestId("guarantee-session-list")).toBeTruthy());

    const post = sent.find((r) => r.method === "POST" && r.url === BASE + "/guarantees");
    const body = post?.body as Record<string, unknown>;
    expect(body.amount).toBe(EXACT_WIRE);
    expect(typeof body.amount).toBe("string");
    expect(JSON.stringify(body)).toContain('"' + EXACT_WIRE + '"');
    /* واحدٌ من الاثنين، والآخر null — لا حقلٌ فارغ ولا حقلٌ محذوف. */
    expect(body.subcontractId).toBe(SUBCONTRACT_ID);
    expect(body.contractId).toBeNull();
  });

  it("مبلغ الدفعة المقدمة كذلك، ولا يمرّ برقمٍ عائم في أي خطوة", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/contracting/advances",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["POST " + BASE + "/subcontractor-advances"]: advance({
            state: "DRAFT",
            entryId: null,
            alreadyPosted: false,
          }),
        },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ad-subc-id"), SUBCONTRACT_ID);
    await click(screen.getByTestId("ad-subc-read"));
    await type(await screen.findByTestId<HTMLInputElement>("ad-number"), "ADV-2026-0001");
    await type(screen.getByTestId<HTMLInputElement>("ad-amount"), EXACT_WIRE);
    await type(screen.getByTestId<HTMLInputElement>("ad-method"), "bank");
    await type(screen.getByTestId<HTMLInputElement>("ad-treasury"), "BANK-0001");
    await click(screen.getByTestId("advance-draft"));
    await waitFor(() => expect(screen.getByTestId("advance-receipt")).toBeTruthy());

    const post = sent.find((r) => r.method === "POST" && r.url === BASE + "/subcontractor-advances");
    expect((post?.body as Record<string, unknown>).amount).toBe(EXACT_WIRE);
  });

  it("ولا مبلغ في الشاشات الأربع يمرّ بـNumber أو parseFloat في المصدر", () => {
    for (const file of SCREEN_FILES) {
      const src = readFileSync(path.resolve(WEB, file), "utf8");
      /* `\b` قبل `Number` عمداً: `setNumber(` ليس تحويلاً إلى عائم. */
      expect(src, file).not.toMatch(/\bparseFloat\b|\bparseInt\b|(?<![A-Za-z])Number\s*\(/);
      expect(src, file).not.toMatch(/toFixed|valueOf\(\)\s*\*|\* *1\b/);
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ · المسوّدة ثمّ الترحيل، وإعادةُ الترحيل تقول الحقيقة
   ═══════════════════════════════════════════════════════════════════════ */
describe("المسوّدة والترحيل فعلان لا فعل", () => {
  it("زرُّ الترحيل لا يظهر قبل المسوّدة، ويظهر بعدها", async () => {
    await mount({
      path: "/contracting/advances",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["POST " + BASE + "/subcontractor-advances"]: advance({
            state: "DRAFT",
            entryId: null,
            alreadyPosted: false,
          }),
        },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ad-subc-id"), SUBCONTRACT_ID);
    await click(screen.getByTestId("ad-subc-read"));
    await screen.findByTestId("ad-number");
    expect(screen.queryByTestId("advance-post")).toBeNull();

    await type(screen.getByTestId<HTMLInputElement>("ad-number"), "ADV-2026-0001");
    await type(screen.getByTestId<HTMLInputElement>("ad-amount"), EXACT_WIRE);
    await type(screen.getByTestId<HTMLInputElement>("ad-method"), "bank");
    await type(screen.getByTestId<HTMLInputElement>("ad-treasury"), "BANK-0001");
    await click(screen.getByTestId("advance-draft"));
    await waitFor(() => expect(screen.getByTestId("advance-post")).toBeTruthy());
  });

  it("إعادةُ الترحيل تُعرَض «لم يقع ترحيلٌ جديد» ولا تُلبَس ثوب النجاح", async () => {
    await mount({
      path: "/contracting/advances",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/subcontractor-advances/" + ADVANCE_ID]: advance({
            state: "POSTED",
            entryId: "99999999-9999-4999-8999-999999999991",
            alreadyPosted: true,
          }),
        },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ad-open-id"), ADVANCE_ID);
    await click(screen.getByTestId("ad-open-go"));
    await waitFor(() => expect(screen.getByTestId("ad-open-receipt")).toBeTruthy());
    const receipt = screen.getByTestId("ad-open-receipt");
    expect(receipt.getAttribute("data-already-posted")).toBe("true");
    expect(receipt.className).toContain("alert--info");
    expect(receipt.className).not.toContain("alert--success");
    expect(document.querySelector(".problem")).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٥ · ما لا يُرحَّل يُقال إنه لا يُرحَّل — والعقد هو من يقول ذلك
   ═══════════════════════════════════════════════════════════════════════ */
describe("ما لا يُرحَّل", () => {
  for (const schema of ["ChangeOrder", "Guarantee"] as const) {
    it(schema + " لا يحمل entryId ولا alreadyPosted في العقد المُولَّد", () => {
      const shape = SCHEMAS[schema];
      expect(shape, schema + " غير منشور في العقد المُولَّد").toBeTruthy();
      const fields = Object.keys(shape?.fields ?? {});
      expect(fields.length).toBeGreaterThan(3);
      expect(fields).not.toContain("entryId");
      expect(fields).not.toContain("alreadyPosted");
    });
  }

  it("وشاشة أوامر التغيير تقول ذلك ولا تعرض زرَّ ترحيل", async () => {
    await mount({
      path: "/contracting/change-orders",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/change-orders/" + ORDER_ID]: CHANGE_ORDER },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("co-read-id"), ORDER_ID);
    await click(screen.getByTestId("co-read-go"));
    await waitFor(() => expect(screen.getByTestId("co-read-out")).toBeTruthy());
    expect(screen.getAllByTestId("change-order-no-posting").length).toBeGreaterThan(0);
    expect(screen.queryByTestId("advance-post")).toBeNull();
    expect(document.querySelector('[data-testid$="-post"]')).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٧ · المحاذاة بنيويّاً · ولا رمز حساب
   ═══════════════════════════════════════════════════════════════════════ */
const SCREEN_FILES = [
  "src/screens/contracting/ChangeOrdersScreen.tsx",
  "src/screens/contracting/GuaranteesScreen.tsx",
  "src/screens/contracting/SubcontractorAdvancesScreen.tsx",
  "src/screens/realestate/PartiesScreen.tsx",
];

describe("الصفّ يملك المسارات، ولا رمز حسابٍ في القسمين", () => {
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
      const src = readFileSync(path.resolve(WEB, file), "utf8");
      const container = /<div className="([^"]+)">\s*(?:\{[^}]*\}\s*)?<(?:Field|div className="(?:field|rowctl))/g;
      let m: RegExpExecArray | null;
      while ((m = container.exec(src))) {
        const classes = (m[1] ?? "").split(/\s+/);
        if (!classes.some((c) => owners.includes(c))) problems.push(file + " ← " + m[1]);
      }
    }
    expect(problems).toEqual([]);
  });

  it("ولا محاذاةٌ بيد، ولا خاصيةٌ فيزيائية، ولا tabular-nums حرفاً", () => {
    for (const file of SCREEN_FILES) {
      const src = readFileSync(path.resolve(WEB, file), "utf8");
      expect(src, file).not.toMatch(/align-items/);
      expect(src, file).not.toMatch(/marginLeft|marginRight|paddingLeft|paddingRight/);
      expect(src, file).not.toMatch(/tabular-nums/);
    }
  });

  it("ولا رمز حسابٍ ولا اسم حسابٍ مكتوبٌ في الشاشات الأربع — المصفوفة وحدها تقرّر", () => {
    for (const file of SCREEN_FILES) {
      const src = readFileSync(path.resolve(WEB, file), "utf8");
      /* أربعة أرقام متتالية فأكثر معزولة = رمز حساب مكتوب بيد. */
      expect(src, file).not.toMatch(/["'][0-9]{4,}["']/);
      expect(src, file).not.toMatch(/accountCode|accountNumber/);
    }
  });
});
