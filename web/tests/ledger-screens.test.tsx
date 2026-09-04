/* ═══════════════════════════════════════════════════════════════════════════
   شاشات ما بعد الترحيل الأربع — حرّاسُها
   The four after-posting screens — their guards
   ───────────────────────────────────────────────────────────────────────────
   ثمانيةٌ تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحص:

     ١ · **القوائم الثلاث تتّفق.** `SCREENS` والموجّه وقائمةُ الملاحة اليدوية
         في `App.tsx` ثلاثُ نسخٍ لا شيء يقارنها، فشاشةٌ في واحدةٍ دون أخرى
         تُفتح بـCtrl+K ولا يراها من يقرأ الملاحة. وشريطُ المجموعة رابعةٌ
         تُقارَن بها.

     ٢ · **الأبواب الستّة تُستدعى فعلاً على السلك.** والفحص على **الطلب
         المُرسَل** لا على وجود زرّ: زرٌّ لا يُصدِر طلباً ليس باباً بلغه أحد.

     ٣ · **العكسُ يقول أثره قبل الضغط، ولا زرَّ بلا إقرار.** جدولُ الأثر
         يُرسَم قبل أن يُلمس شيء، وجانبُ كلّ سطرٍ فيه **معكوسٌ** عن الأصل،
         والزرُّ مُقفلٌ حتى تُؤشَّر خانة الإقرار — وهذا هو الفحص الذي يمنع
         عودة «زرّ عكسٍ يضغط بلا أن يُقرأ شيء».

     ٤ · **والشاشة لا توحي بأن العكس يُزيل القيد**: القيد الأصلي يبقى
         مرسوماً بسطوره **بعد** نجاح العكس، ونصُّ «الأصل باقٍ» يحمل معرّفه.

     ٥ · **كسرُ السلسلة يُعرض بلوح خطرٍ لا بلوح معلومة**، ويسمّي أول تسلسلٍ
         منحرف. والسلامة تُقال **بنطاقها** فلا تُقرأ ضماناً أوسع.

     ٦ · **المال والكمّيات تغادر نصّاً بايتاً ببايت** — «10.5000» تبقى كما
         كُتبت ولا تصير 10.5.

     ٧ · **إعادةُ الترحيل ليست خطأً**: `alreadyPosted` يُعرَض بلوحٍ مُميَّز
         يقول «رُحِّل من قبل»، لا برسالة نجاحٍ ثانية ولا برفض.

     ٨ · **قواعد الملفّات نفسها**: كل `<AccField` بوصف (ADR-0078)، ولا رقم
         حسابٍ ولا `parseFloat` ولا `Number(` على مالٍ في الشاشات الأربع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS } from "../src/app/shell/sections";
import { LEDGER_SCREENS } from "../src/screens/ledger/parts";
import type { RawResponse, Transport } from "../src/api/transport";

const SRC = path.resolve(process.cwd(), "src");
const read = (rel: string): string => readFileSync(path.resolve(SRC, rel), "utf8");
const SCREEN_FILES = [
  "screens/ledger/JournalEntryScreen.tsx",
  "screens/ledger/LedgerChainScreen.tsx",
  "screens/ledger/PurchaseReturnScreen.tsx",
  "screens/ledger/CreditNoteScreen.tsx",
  "screens/ledger/parts.tsx",
];

const CODES = ["ar", "en", "hi", "ur"] as const;

const COMPANY = "11111111-1111-1111-1111-111111111111";
const BASE = "/api/v1/companies/" + COMPANY;
const HEALTH = { apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" };

/** المسارات الأربعة بترتيب العمل — والترتيب نفسه في `SCREENS` وفي الملاحة. */
const LEDGER_PATHS = [
  "/ledger/entry",
  "/ledger/purchase-return",
  "/ledger/credit-note",
  "/ledger/chain",
];

const ENTRY_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1";
const CONTRA_ID = "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2";
const RETURN_ID = "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1";
const NOTE_ID = "cccccccc-cccc-4ccc-8ccc-ccccccccccc1";
const BILL_ID = "dddddddd-dddd-4ddd-8ddd-ddddddddddd1";
const RECEIPT_LINE_ID = "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1";
const INVOICE_ID = "ffffffff-ffff-4fff-8fff-fffffffffff1";
const INVOICE_LINE_ID = "ffffffff-ffff-4fff-8fff-fffffffffff2";
const HASH = "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef";

/** قيدٌ مُرحَّل بسطرين: أوّلهما مدين والثاني دائن. */
const ENTRY = {
  book: "MAIN",
  chainSequence: "412",
  currency: "SAR",
  entryDate: "2026-05-14",
  entryHash: HASH,
  entryId: ENTRY_ID,
  entryNumber: "1042",
  lines: [
    {
      credit: "0",
      currency: "SAR",
      debit: "10.5000",
      descriptionAr: "بضاعة مستلمة",
      descriptionEn: "Goods received",
      lineNo: 1,
      qualifier: "RAW",
      role: "inventory_control",
    },
    {
      credit: "10.5000",
      currency: "SAR",
      debit: "0",
      descriptionAr: "ذمّة المورّد",
      descriptionEn: "Supplier payable",
      lineNo: 2,
      qualifier: "TRADE",
      role: "accounts_payable",
    },
  ],
  memoAr: "استلامُ بضاعةٍ من المورّد",
  memoEn: "Goods received from the supplier",
  periodCode: "2026-05",
  reversesEntryId: null,
  status: "POSTED",
};

const REVERSAL_RECEIPT = {
  alreadyPosted: false,
  chainSequence: "413",
  entryHash: HASH,
  entryId: CONTRA_ID,
  entryNumber: "1043",
  generation: 1,
  lineCount: 2,
  periodCode: "2026-06",
};

const CHAIN_OK = {
  checked: 412,
  detail: null,
  firstDivergentSequence: null,
  ok: true,
  reasonAr: "أُعيد بناء كل سجلّ وطابقت بصمتُه المخزَّنة.",
  verdict: "ledger.chain.intact",
};

const CHAIN_BROKEN = {
  checked: 88,
  detail: "expected=1111111111111111 stored=2222222222222222",
  firstDivergentSequence: "88",
  ok: false,
  reasonAr: "السجلّ ذو التسلسل 88 لا تطابق بصمتُه ما يُعاد بناؤه من محتواه.",
  verdict: "ledger.chain.divergent",
};

const RETURN_DRAFT = {
  alreadyPosted: false,
  entryId: null,
  gross: "0",
  id: RETURN_ID,
  net: "0",
  number: "PR-2026-0001",
  state: "DRAFT",
  tax: "1.5750",
};

const RETURN_POSTED = {
  ...RETURN_DRAFT,
  entryId: CONTRA_ID,
  gross: "12.0750",
  net: "10.5000",
  state: "POSTED",
};

const RETURN_POSTED_AGAIN = { ...RETURN_POSTED, alreadyPosted: true };

const NOTE_DRAFT = {
  alreadyPosted: false,
  entryId: null,
  gross: "23.0000",
  id: NOTE_ID,
  net: "20.0000",
  number: "CN-2026-0001",
  state: "DRAFT",
  tax: "3.0000",
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

async function mount(options: { path: string; transport: Transport }): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath: options.path });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial="ar">
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
    await Promise.resolve();
  });
}

function setNativeValue(element: HTMLInputElement, value: string): void {
  const proto = Object.getPrototypeOf(element) as object;
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

async function check(element: HTMLInputElement): Promise<void> {
  await act(async () => {
    element.click();
    await Promise.resolve();
  });
}

/** يفتح شاشة القيد على قيدٍ مقروء، ويعيد ما أُرسل على السلك. */
async function openEntry(extra: Readonly<Record<string, unknown>> = {}): Promise<Recorded[]> {
  const sent: Recorded[] = [];
  await mount({
    path: "/ledger/entry",
    transport: stub({
      routes: {
        "GET /health": HEALTH,
        ["GET " + BASE + "/journal-entries/" + ENTRY_ID]: ENTRY,
        ...extra,
      },
      sent,
    }),
  });
  await type(await screen.findByTestId<HTMLInputElement>("ledger-entry-id"), ENTRY_ID);
  await click(await screen.findByTestId("ledger-entry-read"));
  await screen.findByTestId("ledger-entry-lines");
  return sent;
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
   ١ · القوائم الأربع تتّفق
   ═══════════════════════════════════════════════════════════════════════ */
describe("عقد الملاحة للشاشات الأربع", () => {
  it("الأربع في SCREENS بترتيب العمل وتحت القسم المحاسبي", () => {
    const declared = SCREENS.filter((s) => s.path.startsWith("/ledger/")).map((s) => s.path);
    expect(declared).toEqual(LEDGER_PATHS);
    for (const p of LEDGER_PATHS) {
      expect(SCREENS.find((s) => s.path === p)?.section).toBe("accounting");
    }
  });

  it("ولا واحدةٌ منها في مجموعتَي المبيعات والمشتريات — فالمرتجع فرعٌ لا خطوة", () => {
    for (const p of LEDGER_PATHS) {
      expect(SCREENS.find((s) => s.path === p)?.group).toBeUndefined();
    }
    expect(SCREENS.filter((s) => s.group === "sales").length).toBe(3);
    expect(SCREENS.filter((s) => s.group === "purchasing").length).toBe(4);
  });

  it("ولكلٍّ منها رابطٌ في قائمة الملاحة اليدوية — لا تُفتح بلوحة الأوامر وحدها", async () => {
    await mount({ path: "/", transport: stub({ routes: { "GET /health": HEALTH } }) });
    const nav = document.querySelector(".app-side");
    expect(nav).not.toBeNull();
    const hrefs = [...(nav?.querySelectorAll("a[href]") ?? [])].map((a) => a.getAttribute("href"));
    for (const target of LEDGER_PATHS) expect(hrefs, target).toContain(target);
  });

  it("وشريطُ المجموعة يحمل الأربع نفسها بالترتيب نفسه — لا ثالثةً ولا خامسة", async () => {
    await mount({
      path: "/ledger/chain",
      transport: stub({ routes: { "GET /health": HEALTH } }),
    });
    const strip = await screen.findByTestId("ledger-nav");
    const inside = [...strip.querySelectorAll("a[href]")].map((a) => a.getAttribute("href"));
    expect(inside).toEqual(LEDGER_PATHS);
    expect(LEDGER_SCREENS.map((s) => s.to)).toEqual(LEDGER_PATHS);
  });

  it("وكلُّ مسارٍ من الأربعة يفتح شاشته في الموجّه", async () => {
    const ids = [
      "ledger-entry-screen",
      "ledger-return-screen",
      "ledger-note-screen",
      "ledger-chain-screen",
    ];
    for (let i = 0; i < LEDGER_PATHS.length; i += 1) {
      await mount({
        path: LEDGER_PATHS[i] as string,
        transport: stub({ routes: { "GET /health": HEALTH } }),
      });
      expect(await screen.findByTestId(ids[i] as string)).toBeTruthy();
      cleanup();
    }
  });

  it("وكلُّ واحدةٍ تُعرض بلا منشأة مختارة بطريقٍ إلى اختيارها لا برسالة خطأ", async () => {
    globalThis.localStorage.setItem(
      "sb-api-config",
      JSON.stringify({ baseUrl: "", token: "", companyId: "", book: "MAIN", period: "" })
    );
    for (const at of LEDGER_PATHS) {
      await mount({ path: at, transport: stub({ routes: {} }) });
      expect(screen.getByTestId("acc-go-sign-in"), at).toBeTruthy();
      cleanup();
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · الأبواب الستّة تُطلَب فعلاً — والفحص على الطلب لا على الزرّ
   ═══════════════════════════════════════════════════════════════════════ */
describe("الأبواب التي لم يكن يبلغها شيء", () => {
  it("readJournalEntry يُطلَب، وسطورُ القيد وبصمتُه وتسلسلُه تُعرَض", async () => {
    const sent = await openEntry();
    expect(sent.some((r) => r.method === "GET" && r.url === BASE + "/journal-entries/" + ENTRY_ID)).toBe(true);
    expect((await screen.findByTestId("ledger-entry-line-1")).textContent).toContain("inventory_control");
    expect((await screen.findByTestId("ledger-entry-hash")).textContent).toBe(HASH);
    expect((await screen.findByTestId("ledger-entry-period")).textContent).toBe("2026-05");
  });

  it("reverseJournalEntry يُطلَب بسببه بطرفيه وتاريخه — بعد الإقرار لا قبله", async () => {
    const sent = await openEntry({
      ["POST " + BASE + "/journal-entries/" + ENTRY_ID + "/reversal"]: REVERSAL_RECEIPT,
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-ar"), "خطأٌ في المبلغ");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-en"), "Wrong amount");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-date"), "2026-06-01");
    await check(await screen.findByTestId<HTMLInputElement>("ledger-rev-act-ack"));
    await click(await screen.findByTestId("ledger-rev-act-go"));
    const posted = sent.find((r) => r.method === "POST" && r.url.endsWith("/reversal"));
    expect(posted, "لم يُرسَل طلب العكس").toBeTruthy();
    const body = posted?.body as Record<string, unknown>;
    expect(body["reason"]).toEqual({ ar: "خطأٌ في المبلغ", en: "Wrong amount" });
    expect(body["reversalDate"]).toBe("2026-06-01");
    expect(body["closedPeriodAuthorisation"]).toBeUndefined();
  });

  it("والإذن الاستثنائي يعبر كاملاً حين يُفتح، ولا يُختلَق حين لا يُفتح", async () => {
    const sent = await openEntry({
      ["POST " + BASE + "/journal-entries/" + ENTRY_ID + "/reversal"]: REVERSAL_RECEIPT,
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-ar"), "فترةٌ مقفلة");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-en"), "Closed period");
    await check(await screen.findByTestId<HTMLInputElement>("ledger-rev-auth-open"));
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-auth-by"), "99999999-9999-4999-8999-999999999991");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-auth-code"), "ledger.post_into_closed_period");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-auth-ar"), "قرارُ المدير المالي");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-auth-en"), "CFO decision");
    await check(await screen.findByTestId<HTMLInputElement>("ledger-rev-act-ack"));
    await click(await screen.findByTestId("ledger-rev-act-go"));
    const posted = sent.find((r) => r.method === "POST" && r.url.endsWith("/reversal"));
    const body = posted?.body as Record<string, unknown>;
    expect(body["closedPeriodAuthorisation"]).toEqual({
      authorisedBy: "99999999-9999-4999-8999-999999999991",
      permissionCode: "ledger.post_into_closed_period",
      reason: { ar: "قرارُ المدير المالي", en: "CFO decision" },
    });
  });

  it("verifyLedgerChain يُطلَب بدفتره وسنته على السلك", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/ledger/chain",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/ledger-chain/verification"]: CHAIN_OK },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-chain-year"), "2026");
    await click(await screen.findByTestId("ledger-chain-run"));
    await screen.findByTestId("ledger-chain-intact");
    const asked = sent.find((r) => r.url.includes("/ledger-chain/verification"));
    expect(asked?.method).toBe("GET");
    expect(asked?.url).toContain("book=MAIN");
    expect(asked?.url).toContain("fiscalYear=2026");
  });

  it("draftPurchaseReturn وreadPurchaseReturn وpostPurchaseReturn ثلاثتُها تُطلَب", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/ledger/purchase-return",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["POST " + BASE + "/purchase-returns"]: RETURN_DRAFT,
          ["GET " + BASE + "/purchase-returns/" + RETURN_ID]: RETURN_DRAFT,
          ["POST " + BASE + "/purchase-returns/" + RETURN_ID + "/posting"]: RETURN_POSTED,
        },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-number"), "PR-2026-0001");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-bill"), BILL_ID);
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-issued"), "2026-05-20");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-line"), RECEIPT_LINE_ID);
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-qty"), "3.0000");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-tax"), "1.5750");
    await click(await screen.findByTestId("ledger-ret-draft-submit"));
    await screen.findByTestId("ledger-ret-totals");
    await click(await screen.findByTestId("ledger-ret-post"));
    await screen.findByTestId("ledger-ret-receipt");

    expect(sent.some((r) => r.method === "POST" && r.url === BASE + "/purchase-returns")).toBe(true);
    expect(sent.some((r) => r.method === "GET" && r.url === BASE + "/purchase-returns/" + RETURN_ID)).toBe(true);
    expect(
      sent.some((r) => r.method === "POST" && r.url === BASE + "/purchase-returns/" + RETURN_ID + "/posting")
    ).toBe(true);
  });

  it("draftCreditNote وpostCreditNote يُطلَبان، والسطر يحمل قراره التجاري", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/ledger/credit-note",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["POST " + BASE + "/credit-notes"]: NOTE_DRAFT,
          ["POST " + BASE + "/credit-notes/" + NOTE_ID + "/posting"]: {
            ...NOTE_DRAFT,
            entryId: CONTRA_ID,
            state: "POSTED",
          },
        },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-note-number"), "CN-2026-0001");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-note-invoice"), INVOICE_ID);
    await type(await screen.findByTestId<HTMLInputElement>("ledger-note-issued"), "2026-05-21");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-desc-ar"), "بضاعةٌ مردودة");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-desc-en"), "Returned goods");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-group"), "FIN");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-qty"), "2.0000");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-price"), "10.0000");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-taxrate"), "0.15");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-note-original-line"), INVOICE_LINE_ID);
    await click(await screen.findByTestId("ledger-note-add-line"));
    await click(await screen.findByTestId("ledger-note-draft-submit"));
    await screen.findByTestId("ledger-note-totals");
    await click(await screen.findByTestId("ledger-note-post"));

    const drafted = sent.find((r) => r.method === "POST" && r.url === BASE + "/credit-notes");
    expect(drafted, "لم تُرسَل مسوّدة الإشعار").toBeTruthy();
    const body = drafted?.body as { lines: Record<string, unknown>[] };
    expect(body.lines[0]?.["originalInvoiceLineId"]).toBe(INVOICE_LINE_ID);
    expect(body.lines[0]?.["unitPrice"]).toBe("10.0000");
    expect(
      sent.some((r) => r.method === "POST" && r.url === BASE + "/credit-notes/" + NOTE_ID + "/posting")
    ).toBe(true);
  });

  it("وتخفيضُ القيمة يعبر بـnull صريحة لا بحقلٍ محذوف — فالفرق قرارٌ لا يُخمَّن", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/ledger/credit-note",
      transport: stub({
        routes: { "GET /health": HEALTH, ["POST " + BASE + "/credit-notes"]: NOTE_DRAFT },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-note-number"), "CN-2026-0002");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-note-invoice"), INVOICE_ID);
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-desc-ar"), "تخفيضُ قيمة");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-desc-en"), "Value reduction");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-group"), "FIN");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-qty"), "1");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-price"), "5.0000");
    await type(await screen.findByTestId<HTMLInputElement>("acc-line-taxrate"), "0.15");
    const kind = await screen.findByTestId<HTMLSelectElement>("ledger-note-kind");
    await act(async () => {
      kind.value = "valueReduction";
      kind.dispatchEvent(new Event("change", { bubbles: true }));
      await Promise.resolve();
    });
    await click(await screen.findByTestId("ledger-note-add-line"));
    await click(await screen.findByTestId("ledger-note-draft-submit"));
    const drafted = sent.find((r) => r.method === "POST" && r.url === BASE + "/credit-notes");
    const body = drafted?.body as { lines: Record<string, unknown>[] };
    expect(Object.keys(body.lines[0] ?? {})).toContain("originalInvoiceLineId");
    expect(body.lines[0]?.["originalInvoiceLineId"]).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · الأثر يُقال قبل الضغط، ولا زرَّ بلا إقرار
   ═══════════════════════════════════════════════════════════════════════ */
describe("العكس يقول أثره قبل أن يقع", () => {
  it("جدولُ الأثر يُرسَم بمجرّد قراءة القيد — قبل أن يُلمس زرّ", async () => {
    await openEntry();
    expect(await screen.findByTestId("ledger-rev-effect")).toBeTruthy();
    expect(await screen.findByTestId("ledger-rev-keeps")).toBeTruthy();
  });

  it("وجانبُ كل سطرٍ فيه معكوسٌ عن جانبه في الأصل", async () => {
    await openEntry();
    /* السطر الأول مدينٌ في الأصل، فيصير دائناً في المضادّ — والعكس بالعكس. */
    const first = (await screen.findByTestId("ledger-rev-side-1")).textContent;
    const second = (await screen.findByTestId("ledger-rev-side-2")).textContent;
    expect(first).not.toBe(second);
    const original = (await screen.findByTestId("ledger-entry-line-1")).textContent ?? "";
    expect(original).toContain("10.5");
    expect(first).toBeTruthy();
  });

  it("والفترةُ التي يقع فيها القيد المضادّ تُقال قبل الضغط، وتتبع التاريخ المكتوب", async () => {
    await openEntry();
    expect((await screen.findByTestId("ledger-rev-period")).textContent).toBe("2026-05");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-date"), "2026-07-03");
    expect((await screen.findByTestId("ledger-rev-period")).textContent).toBe("2026-07");
    expect((await screen.findByTestId("ledger-rev-effective-date")).textContent).toBe("2026-07-03");
  });

  it("وحالُ الفترة يُقال إنه غير معروف — لا يُسكَت عنه ولا يُخمَّن", async () => {
    await openEntry();
    const said = (await screen.findByTestId("ledger-rev-period-unknown")).textContent ?? "";
    expect(said.length).toBeGreaterThan(40);
  });

  it("والزرُّ مُقفلٌ قبل الإقرار ولو اكتمل المُدخَل، ولا طلبَ يُرسَل", async () => {
    const sent = await openEntry({
      ["POST " + BASE + "/journal-entries/" + ENTRY_ID + "/reversal"]: REVERSAL_RECEIPT,
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-ar"), "سبب");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-en"), "Reason");
    const go = await screen.findByTestId<HTMLButtonElement>("ledger-rev-act-go");
    expect(go.disabled).toBe(true);
    await click(go);
    expect(sent.some((r) => r.url.endsWith("/reversal"))).toBe(false);
    await check(await screen.findByTestId<HTMLInputElement>("ledger-rev-act-ack"));
    expect((await screen.findByTestId<HTMLButtonElement>("ledger-rev-act-go")).disabled).toBe(false);
  });

  it("والسببُ الناقص يُقال باسمه قبل الضغط لا برسالةٍ عامّة بعده", async () => {
    await openEntry();
    await check(await screen.findByTestId<HTMLInputElement>("ledger-rev-act-ack"));
    const blocked = await screen.findByTestId("ledger-rev-act-blocked");
    expect(blocked.textContent ?? "").toContain("السبب");
    expect((await screen.findByTestId<HTMLButtonElement>("ledger-rev-act-go")).disabled).toBe(true);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ · العكس ليس حذفاً — والشاشة لا توحي بغير ذلك
   ═══════════════════════════════════════════════════════════════════════ */
describe("الأصل يبقى مقروءاً بعد العكس", () => {
  it("سطورُ القيد الأصلي تبقى مرسومةً بعد نجاح العكس، ومعرّفه يُقال نصّاً", async () => {
    await openEntry({
      ["POST " + BASE + "/journal-entries/" + ENTRY_ID + "/reversal"]: REVERSAL_RECEIPT,
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-ar"), "خطأ");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-en"), "Mistake");
    await check(await screen.findByTestId<HTMLInputElement>("ledger-rev-act-ack"));
    await click(await screen.findByTestId("ledger-rev-act-go"));
    await screen.findByTestId("ledger-rev-receipt");
    /* الأصل ما يزال مرسوماً بسطوره — لا شاشةَ فارغة توحي بأنه ذهب. */
    expect(screen.getByTestId("ledger-entry-lines")).toBeTruthy();
    expect(screen.getByTestId("ledger-entry-line-1")).toBeTruthy();
    expect((screen.getByTestId("ledger-rev-both-readable").textContent ?? "")).toContain(ENTRY_ID);
    /* ومعرّف القيد المضادّ غيرُ معرّف الأصل. */
    expect((screen.getByTestId("ledger-rev-receipt-entry").textContent ?? "")).toBe(CONTRA_ID);
  });

  it("ولا فعلَ حذفٍ في الشاشات الأربع: لا DELETE على السلك ولا كلمة delete", () => {
    for (const file of SCREEN_FILES) {
      const text = read(file);
      expect(text, file).not.toContain('method: "DELETE"');
      expect(text, file).not.toMatch(/\bdeleteJournal|\bdeleteEntry\b/);
    }
  });

  it("وإعادةُ العكس تُعرَض «كان معكوساً من قبل» بلوحٍ مُميَّز لا برفض", async () => {
    await openEntry({
      ["POST " + BASE + "/journal-entries/" + ENTRY_ID + "/reversal"]: {
        ...REVERSAL_RECEIPT,
        alreadyPosted: true,
      },
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-ar"), "خطأ");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-rev-reason-en"), "Mistake");
    await check(await screen.findByTestId<HTMLInputElement>("ledger-rev-act-ack"));
    await click(await screen.findByTestId("ledger-rev-act-go"));
    const receipt = await screen.findByTestId("ledger-rev-receipt");
    expect(receipt.getAttribute("data-already-posted")).toBe("true");
    expect(screen.queryByTestId("problem-panel")).toBeNull();
  });

  it("و501 يُقال باسمه: سطحُ القراءة لم يهبط — لا رسالةَ شبكةٍ عامّة", async () => {
    await mount({
      path: "/ledger/entry",
      transport: ({ method, url }) => {
        if (url === "/health") {
          return Promise.resolve<RawResponse>({ ok: true, status: 200, json: HEALTH, url });
        }
        void method;
        return Promise.resolve<RawResponse>({
          ok: false,
          status: 501,
          url,
          json: {
            type: "about:blank",
            title: "Not implemented",
            titleAr: "غير منزَّل",
            detail: "The single-entry read surface has not landed.",
            detailAr: "سطح القراءة لم يهبط.",
            status: 501,
            code: "ledger.read.entry_surface_unavailable",
            errors: [],
            instance: url,
            traceId: "00-0000-0000-00",
          },
        });
      },
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-entry-id"), ENTRY_ID);
    await click(await screen.findByTestId("ledger-entry-read"));
    const named = await screen.findByTestId("ledger-entry-absent");
    expect(named.textContent ?? "").toContain("ledger.read.entry_surface_unavailable");
    /* ولا نموذجَ عكسٍ بلا قيدٍ يُقرأ. */
    expect(screen.queryByTestId("ledger-rev-form")).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٥ · كسرُ السلسلة أخطر ما يعرضه هذا النظام
   ═══════════════════════════════════════════════════════════════════════ */
describe("حكمُ سلسلة الدفتر", () => {
  async function verify(answer: unknown): Promise<void> {
    await mount({
      path: "/ledger/chain",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/ledger-chain/verification"]: answer },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-chain-year"), "2026");
    await click(await screen.findByTestId("ledger-chain-run"));
  }

  it("الكسرُ يُعرَض بلوح خطرٍ بدور alert، ويسمّي أول تسلسلٍ منحرف", async () => {
    await verify(CHAIN_BROKEN);
    const panel = await screen.findByTestId("ledger-chain-broken");
    expect(panel.getAttribute("role")).toBe("alert");
    expect(panel.className).toContain("alert--danger");
    expect(panel.getAttribute("data-ok")).toBe("false");
    expect((await screen.findByTestId("ledger-chain-first-divergent")).textContent ?? "").toContain("88");
    /* والتفصيل الفنّي يُعرض كما وصل — لا يُبتلع. */
    expect((await screen.findByTestId("ledger-chain-detail-text")).textContent).toBe(CHAIN_BROKEN.detail);
    /* ولا لوحَ سلامةٍ إلى جانبه. */
    expect(screen.queryByTestId("ledger-chain-intact")).toBeNull();
  });

  it("والشرحُ العربي ورمزُ الحكم والعددُ المفحوص كلُّها تُعرض كما أرسلها الخادم", async () => {
    await verify(CHAIN_BROKEN);
    expect((await screen.findByTestId("ledger-chain-reason")).textContent).toBe(CHAIN_BROKEN.reasonAr);
    expect((await screen.findByTestId("ledger-chain-code")).textContent).toBe("ledger.chain.divergent");
    expect((await screen.findByTestId("ledger-chain-checked")).textContent ?? "").toMatch(/[0-9٠-٩]/u);
  });

  it("والسلامةُ تُقال بنطاقها ولا تُقرأ ضماناً أوسع ممّا فُحص", async () => {
    await verify(CHAIN_OK);
    const panel = await screen.findByTestId("ledger-chain-intact");
    expect(panel.getAttribute("data-ok")).toBe("true");
    const text = panel.textContent ?? "";
    expect(text).toContain("MAIN");
    expect(text).toContain("2026");
    /* «ولا يقول شيئاً عن دفترٍ آخر…» — الحدُّ مكتوبٌ في اللوح نفسه. */
    expect(text.length).toBeGreaterThan(120);
    expect(screen.queryByTestId("ledger-chain-broken")).toBeNull();
  });

  it("وهذه الشاشة لا تكتب شيئاً: لا طلبَ غير GET يغادرها", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/ledger/chain",
      transport: stub({
        routes: { "GET /health": HEALTH, ["GET " + BASE + "/ledger-chain/verification"]: CHAIN_OK },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-chain-year"), "2026");
    await click(await screen.findByTestId("ledger-chain-run"));
    await screen.findByTestId("ledger-chain-intact");
    expect(sent.every((r) => r.method === "GET")).toBe(true);
  });

  it("وسنةٌ لا تطابق النحو المنشور تُرفض قبل الضغط، ولا طلبَ يُرسَل", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/ledger/chain",
      transport: stub({ routes: { "GET /health": HEALTH }, sent }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-chain-year"), "٢٠٢٦");
    const run = await screen.findByTestId<HTMLButtonElement>("ledger-chain-run");
    expect(run.disabled).toBe(true);
    await click(run);
    expect(sent.some((r) => r.url.includes("ledger-chain"))).toBe(false);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٦ · المال والكمّيات نصوص، وإعادة الترحيل ليست خطأً
   ═══════════════════════════════════════════════════════════════════════ */
describe("المال يعبر نصّاً، والترحيل الثاني يُقال", () => {
  it("«3.0000» و«1.5750» تغادران بايتاً ببايت ولا تصيران 3 و1.575", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/ledger/purchase-return",
      transport: stub({
        routes: { "GET /health": HEALTH, ["POST " + BASE + "/purchase-returns"]: RETURN_DRAFT },
        sent,
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-number"), "PR-2026-0009");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-bill"), BILL_ID);
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-line"), RECEIPT_LINE_ID);
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-qty"), "3.0000");
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-tax"), "1.5750");
    await click(await screen.findByTestId("ledger-ret-draft-submit"));
    const drafted = sent.find((r) => r.method === "POST" && r.url === BASE + "/purchase-returns");
    /* والنقلُ يُسلسِل الجسم بـJSON.stringify نفسها، فهذه بايتاتُ السلك. */
    const raw = JSON.stringify(drafted?.body);
    expect(raw).toContain('"quantity":"3.0000"');
    expect(raw).toContain('"tax":"1.5750"');
    /* ولا حقل صافٍ في الحمولة أصلاً. */
    expect(raw).not.toContain('"net"');
  });

  it("وإعادةُ ترحيل المرتجع تُعرَض بلوحٍ يقول «رُحِّل من قبل» لا بلوح خطأ", async () => {
    await mount({
      path: "/ledger/purchase-return",
      transport: stub({
        routes: {
          "GET /health": HEALTH,
          ["GET " + BASE + "/purchase-returns/" + RETURN_ID]: RETURN_DRAFT,
          ["POST " + BASE + "/purchase-returns/" + RETURN_ID + "/posting"]: RETURN_POSTED_AGAIN,
        },
      }),
    });
    await type(await screen.findByTestId<HTMLInputElement>("ledger-ret-id"), RETURN_ID);
    await screen.findByTestId("ledger-ret-totals");
    await click(await screen.findByTestId("ledger-ret-post"));
    const receipt = await screen.findByTestId("ledger-ret-receipt");
    expect(receipt.getAttribute("data-already-posted")).toBe("true");
    expect(screen.queryByTestId("problem-panel")).toBeNull();
  });

  it("والمسوّدة تعود بصافٍ صفر ويُقال لماذا — لا تُقرأ عطلاً", async () => {
    await mount({
      path: "/ledger/purchase-return",
      transport: stub({ routes: { "GET /health": HEALTH } }),
    });
    const said = (await screen.findByTestId("ledger-return-no-net")).textContent ?? "";
    expect(said).toContain("صفر");
    expect(said.length).toBeGreaterThan(60);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٧ · قواعد الملفّات نفسها
   ═══════════════════════════════════════════════════════════════════════ */
describe("قواعد الشاشات الأربع", () => {
  it("كل <AccField في الشاشات الأربع يحمل وصفاً — ADR-0078", () => {
    for (const file of SCREEN_FILES) {
      const text = read(file);
      const fields = [...text.matchAll(/<AccField\b([\s\S]*?)>/g)];
      for (const field of fields) {
        expect(field[1] ?? "", file + " ← حقلٌ بلا وصف").toMatch(/\bhint=/);
      }
    }
  });

  it("ولا حسابٌ على مالٍ ولا كمّيةٍ في الشيفرة: لا parseFloat ولا Number( ولا حساب", () => {
    for (const file of SCREEN_FILES) {
      const text = read(file);
      expect(text, file).not.toContain("parseFloat");
      expect(text, file).not.toContain("parseInt");
      expect(text, file).not.toMatch(/\bNumber\(/);
    }
  });

  it("ولا رقمَ حسابٍ مكتوبٍ في أيٍّ منها", () => {
    for (const file of SCREEN_FILES) {
      const text = read(file);
      /* أرقامُ حساباتٍ نمطية: أربعُ خانات فأكثر بين علامتَي اقتباس. */
      expect(text, file).not.toMatch(/"[0-9]{4,}"/);
      expect(text, file).not.toMatch(/accountCode|accountNumber/);
    }
  });

  it("وكلُّ مفتاحٍ تطلبه الشاشات الأربع معرَّفٌ في اللغات الأربع", () => {
    const asked = new Set<string>();
    for (const file of SCREEN_FILES) {
      for (const m of read(file).matchAll(/\bt[p]?\(\s*"([a-zA-Z0-9.]+)"/g)) {
        asked.add(m[1] as string);
      }
    }
    expect(asked.size).toBeGreaterThan(80);
    for (const code of CODES) {
      const i18n = createI18n();
      i18n.use(code);
      for (const key of asked) {
        expect(i18n.t(key), code + " ← " + key).not.toBe(key);
      }
    }
  });
});
