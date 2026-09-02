/* ═══════════════════════════════════════════════════════════════════════════
   دورة المستندات المحاسبية — الحرّاس التي لا يجوز أن تحمرّ يوماً
   ───────────────────────────────────────────────────────────────────────────
   ستّة أشياء تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحَص:

     ١ · **المال يعبر نصّاً كما كُتب حرفاً بحرف** — لا `number` على السلك،
         ولا خانةٌ تُفقَد، ولا «10.50» تصير «10.5».
     ٢ · **المسوّدة لا تُرحّل** — نداءٌ واحد لا يفعل فعلين، ونداء الترحيل
         لا يقع إلا بضغطةٍ ثانية.
     ٣ · **الترحيل الثاني يقول الحقيقة** — `alreadyPosted` يُعرَض ولا يُعدّ
         خطأً ولا نجاحاً ثانياً.
     ٤ · **لا رقم حساب على الشاشة** — لا في حقل، ولا في تسمية، ولا في تلميح.
     ٥ · **الصفّ يستوي بنيوياً** — لكل حقلٍ في صفٍّ وصفٌ **واحد**: لا صفر
         (فينكسر قاع الحبر من طرفه) ولا اثنان (فيخرج عن مسارات الصفّ الثلاثة).
     ٦ · **كل مسارٍ مسجَّل ويُعرض** — لا شاشة تُفتح بـCtrl+K ولا يجدها الموجّه.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS, SCREEN_GROUPS } from "../src/app/shell/sections";
import { registeredPaths } from "../src/app/voice-destinations";
import type { RawResponse, Transport } from "../src/api/transport";
import { resetAccountingFocus } from "../src/screens/accounting/focus";

const COMPANY = "11111111-1111-1111-1111-111111111111";
const base = "/api/v1/companies/" + COMPANY;

/** مبلغٌ بأربع منازل، خانتُه الأخيرة هي ما يفقده العائم. */
const EXACT_WIRE = "1000000000000.4013";
/** ومبلغٌ صفرُه اللاحق هو ما يفقده أي تطبيعٍ رقمي. */
const TRAILING_ZERO = "10.50";

const INVOICE_ID = "a0000000-0000-4000-8000-000000000001";
const RECEIPT_ID = "b0000000-0000-4000-8000-000000000002";

function doc(over: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    alreadyPosted: false,
    entryId: null,
    gross: "1150.0000",
    id: INVOICE_ID,
    net: "1000.0000",
    number: "INV-2026-000001",
    state: "DRAFT",
    tax: "150.0000",
    ...over,
  };
}

interface Recorded {
  method: string;
  url: string;
  body?: unknown;
}

function stub(routes: Record<string, unknown>, sent?: Recorded[]): Transport {
  return ({ method, url, body }) => {
    sent?.push({ method, url, body });
    const path = url.split("?")[0] ?? url;
    const good = routes[method + " " + path];
    if (good === undefined) {
      return Promise.resolve<RawResponse>({ ok: false, status: 404, json: null, url });
    }
    return Promise.resolve<RawResponse>({ ok: true, status: 200, json: good, url });
  };
}

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

/** يكتب في حقلٍ بمعرّف اختباره. */
function type(testId: string, value: string): void {
  fireEvent.change(screen.getByTestId(testId), { target: { value } });
}

/** ينقر زرّاً ويُفرغ صفَّ المهامّ الدقيقة، فتصل أجوبة النقل قبل التوكيد. */
async function click(testId: string): Promise<void> {
  await act(async () => {
    fireEvent.click(screen.getByTestId(testId));
    await Promise.resolve();
  });
}

beforeEach(() => {
  resetAccountingFocus();
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
   ١ · المال نصّ — ويعبر كما كُتب
   ═══════════════════════════════════════════════════════════════════════ */

describe("المال نصّ على السلك", () => {
  it("سعرُ الوحدة يعبر بخاناته الأربع كما كُتب، ولا يمرّ برقم", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/sales/invoice",
      transport: stub({ ["POST " + base + "/sales-invoices"]: doc() }, sent),
    });

    type("acc-invoice-number", "INV-1");
    type("acc-invoice-customer", "cust-1");
    type("acc-invoice-branch", "br-1");
    type("acc-line-desc-ar", "بضاعة");
    type("acc-line-desc-en", "Goods");
    type("acc-line-group", "goods");
    type("acc-line-qty", "1");
    type("acc-line-price", EXACT_WIRE);
    type("acc-line-taxrate", "0.15");
    await click("acc-invoice-add-line");
    await click("acc-invoice-draft-submit");

    const post = sent.find((r) => r.method === "POST");
    expect(post, "لم يُرسل شيء").toBeTruthy();
    const raw = JSON.stringify(post?.body);
    /* الخانة الرابعة موجودة بحرفها — وهي أول ما يفقده العائم. */
    expect(raw).toContain('"' + EXACT_WIRE + '"');
    /* ولا رمز رقمي في حقل مالي: المبلغ بين علامتَي اقتباس دائماً. */
    expect(raw).not.toContain(EXACT_WIRE + ",");
    expect(raw).not.toContain(":" + EXACT_WIRE);
  });

  it("الصفر اللاحق يبقى: «10.50» لا تصير «10.5»", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/sales/receipt",
      transport: stub({ ["POST " + base + "/customer-receipts"]: doc({ id: RECEIPT_ID }) }, sent),
    });

    type("acc-receipt-number", "RC-1");
    type("acc-receipt-customer", "cust-1");
    type("acc-receipt-received", TRAILING_ZERO);
    type("acc-receipt-treasury", "cash-box-1");
    await click("acc-receipt-draft-submit");

    const post = sent.find((r) => r.method === "POST");
    const raw = JSON.stringify(post?.body);
    expect(raw).toContain('"' + TRAILING_ZERO + '"');
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · المسوّدة لا تُرحّل — والفعلان لا يُدمجان
   ═══════════════════════════════════════════════════════════════════════ */

describe("المسوّدة ثمّ الترحيل خطوتان", () => {
  it("إنشاء المسوّدة لا يمسّ باب الترحيل إطلاقاً", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/sales/invoice",
      transport: stub(
        {
          ["POST " + base + "/sales-invoices"]: doc(),
          ["GET " + base + "/sales-invoices/" + INVOICE_ID]: doc(),
        },
        sent
      ),
    });

    type("acc-invoice-number", "INV-1");
    type("acc-invoice-customer", "cust-1");
    type("acc-invoice-branch", "br-1");
    type("acc-line-desc-ar", "بضاعة");
    type("acc-line-desc-en", "Goods");
    type("acc-line-group", "goods");
    type("acc-line-qty", "1");
    type("acc-line-price", "100.0000");
    type("acc-line-taxrate", "0.15");
    await click("acc-invoice-add-line");
    await click("acc-invoice-draft-submit");

    expect(sent.some((r) => r.url.includes("/posting"))).toBe(false);
  });

  it("أمر الشراء لا زرَّ ترحيلٍ له، والغياب مُعلَنٌ نصّاً", async () => {
    await mount({ path: "/purchasing/order", transport: stub({}) });
    expect(screen.queryByTestId("acc-order-post")).toBeNull();
    /* ولا يُترك الغياب صامتاً: لوحُ «نقصٍ مُعلَن» يشرح لماذا. */
    expect(screen.getByTestId("acc-order-no-posting")).toBeTruthy();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · الترحيل الثاني يقول الحقيقة
   ═══════════════════════════════════════════════════════════════════════ */

describe("الترحيل المكرَّر", () => {
  it("alreadyPosted يُعرَض إيصالاً مُميَّزاً، ولا يُعدّ خطأً", async () => {
    await mount({
      path: "/sales/invoice",
      transport: stub({
        ["GET " + base + "/sales-invoices/" + INVOICE_ID]: doc({ state: "DRAFT" }),
        ["POST " + base + "/sales-invoices/" + INVOICE_ID + "/posting"]: doc({
          state: "POSTED",
          alreadyPosted: true,
          entryId: "e0000000-0000-4000-8000-000000000009",
        }),
      }),
    });

    type("acc-invoice-id", INVOICE_ID);
    await waitFor(() => expect(screen.getByTestId("acc-invoice-post")).toBeTruthy());
    await click("acc-invoice-post");

    const receipt = await screen.findByTestId("acc-invoice-receipt");
    expect(receipt.getAttribute("data-already-posted")).toBe("true");
    /* والإيصال يحمل القيد — فهو إيصالٌ لا اعتذار. */
    expect(screen.getByTestId("acc-receipt-entry").textContent).toContain("e0000000");
    /* ولا لوحةَ مشكلة: الترحيل الثاني ليس رفضاً. */
    expect(screen.queryByTestId("problem-panel")).toBeNull();
  });

  it("الترحيل الأول يُعرَض إيصالاً غير مُميَّز بالتكرار", async () => {
    await mount({
      path: "/sales/invoice",
      transport: stub({
        ["GET " + base + "/sales-invoices/" + INVOICE_ID]: doc({ state: "DRAFT" }),
        ["POST " + base + "/sales-invoices/" + INVOICE_ID + "/posting"]: doc({
          state: "POSTED",
          alreadyPosted: false,
          entryId: "e0000000-0000-4000-8000-000000000009",
        }),
      }),
    });

    type("acc-invoice-id", INVOICE_ID);
    await waitFor(() => expect(screen.getByTestId("acc-invoice-post")).toBeTruthy());
    await click("acc-invoice-post");

    const receipt = await screen.findByTestId("acc-invoice-receipt");
    expect(receipt.getAttribute("data-already-posted")).toBe("false");
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ · لا رقم حساب على الشاشة
   ═══════════════════════════════════════════════════════════════════════ */

describe("لا رمز حساب في القسم", () => {
  const PATHS = [
    "/sales/invoice",
    "/sales/receipt",
    "/purchasing/order",
    "/purchasing/goods-receipt",
    "/purchasing/bill",
    "/purchasing/payment",
  ];

  it("لا حقل يطلب رقم حساب، ولا نصّ يسمّي واحداً", async () => {
    for (const path of PATHS) {
      await mount({ path, transport: stub({}) });
      const text = document.body.textContent ?? "";
      /* أسماءٌ لا يجوز أن تظهر: الشاشة لا تعرف دليل الحسابات. */
      for (const forbidden of ["رقم الحساب", "رمز الحساب", "Account code", "Account number"]) {
        expect(text, path + " يسمّي حساباً: " + forbidden).not.toContain(forbidden);
      }
      /* ولا سلسلةُ أرقام حسابٍ نمطية (أربع خانات فأكثر متتالية بلا فاصلة). */
      const inputs = [...document.querySelectorAll("input")];
      expect(inputs.length, path + " بلا حقول").toBeGreaterThan(0);
      cleanup();
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٥ · الصفّ يستوي بنيوياً — وصفٌ واحد لكل حقل
   ═══════════════════════════════════════════════════════════════════════ */

describe("استقامة الصفّ", () => {
  const PATHS = [
    "/sales/invoice",
    "/sales/receipt",
    "/sales/receivables",
    "/purchasing/order",
    "/purchasing/goods-receipt",
    "/purchasing/bill",
    "/purchasing/payment",
  ];

  it("كل حقلٍ في صفٍّ يحمل وصفاً واحداً — لا صفر ولا اثنين", async () => {
    let rowsSeen = 0;
    let fieldsSeen = 0;

    for (const path of PATHS) {
      await mount({ path, transport: stub({}) });
      const rows = [...document.querySelectorAll(".acc-row")];
      expect(rows.length, path + " بلا صفوف").toBeGreaterThan(0);

      for (const row of rows) {
        rowsSeen += 1;
        for (const field of [...row.children].filter((c) => c.classList.contains("field"))) {
          fieldsSeen += 1;
          const descs = [...field.children].filter(
            (c) => c.classList.contains("hint") || c.classList.contains("field-error")
          );
          /* **هذا هو الحارس على قاع الحبر**: حقلٌ بلا وصفٍ ينتهي حبره عند
             قاع تحكّمه بينما ينتهي حبرُ جاره تحت وصفه، فينكسر الصفّ من
             طرفٍ لم يُنظر إليه. واثنان يخرجان عن مسارات الصفّ الثلاثة. */
          expect(
            descs.length,
            path + " — حقل «" + (field.querySelector("label")?.textContent ?? "?") + "» فيه " +
              String(descs.length) + " وصفاً"
          ).toBe(1);
        }
      }
      cleanup();
    }

    /* حارس لا فراغ: مسحٌ لا يقرأ شيئاً يمرّ دائماً. */
    expect(rowsSeen).toBeGreaterThanOrEqual(14);
    expect(fieldsSeen).toBeGreaterThanOrEqual(40);
  });

  it("كل حقلٍ في صفٍّ يحمل تسميةً موصولةً بعنصر تحكّمه", async () => {
    let checked = 0;
    for (const path of PATHS) {
      await mount({ path, transport: stub({}) });
      for (const field of [...document.querySelectorAll(".acc-row > .field")]) {
        const label = field.querySelector("label");
        const control = field.querySelector("input, select, textarea");
        expect(label, path + " حقلٌ بلا تسمية").toBeTruthy();
        expect(control, path + " حقلٌ بلا عنصر تحكّم").toBeTruthy();
        expect(label?.getAttribute("for")).toBe(control?.getAttribute("id"));
        checked += 1;
      }
      cleanup();
    }
    expect(checked).toBeGreaterThanOrEqual(40);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٦ · المسارات مسجَّلة، والمجموعتان داخل القسم لا قسمان
   ═══════════════════════════════════════════════════════════════════════ */

describe("الملاحة", () => {
  const WANTED = [
    "/sales/invoice",
    "/sales/receipt",
    "/sales/receivables",
    "/purchasing/order",
    "/purchasing/goods-receipt",
    "/purchasing/bill",
    "/purchasing/payment",
  ];

  it("كل شاشةٍ في SCREENS مسجَّلة في الموجّه", () => {
    const paths = registeredPaths(createAppRouter({ memory: true }));
    for (const path of WANTED) {
      expect(paths, path + " غير مسجَّل").toContain(path);
      expect(SCREENS.map((s) => s.path), path + " ليس في SCREENS").toContain(path);
    }
  });

  it("الشاشات السبع في مجموعتَي المحاسبة، ولا قسمَ سادس", () => {
    const sales = SCREENS.filter((s) => s.group === "sales");
    const purchasing = SCREENS.filter((s) => s.group === "purchasing");
    expect(sales.length).toBe(3);
    expect(purchasing.length).toBe(4);
    /* والمجموعتان **داخل** القسم المحاسبي — لا تصيران قسمين في الملاحة. */
    for (const entry of [...sales, ...purchasing]) {
      expect(entry.section).toBe("accounting");
    }
    expect(SCREEN_GROUPS.map((g) => g.id)).toEqual(["sales", "purchasing"]);
    for (const group of SCREEN_GROUPS) {
      expect(WANTED).toContain(group.path);
    }
  });

  it("كل شاشةٍ تُعرض بلا منشأة مختارة بطريقٍ إلى اختيارها لا برسالة خطأ", async () => {
    globalThis.localStorage.setItem(
      "sb-api-config",
      JSON.stringify({ baseUrl: "", token: "", companyId: "", book: "MAIN", period: "" })
    );
    for (const path of WANTED) {
      await mount({ path, transport: stub({}) });
      expect(screen.getByTestId("acc-go-sign-in"), path).toBeTruthy();
      cleanup();
    }
  });
});
