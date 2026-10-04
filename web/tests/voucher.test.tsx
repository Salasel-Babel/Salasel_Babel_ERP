/* ═══════════════════════════════════════════════════════════════════════════
   القيد اليدوي يسمّي حساباً (ADR-0096)
   ───────────────────────────────────────────────────────────────────────────
   · الحسابات تُقرأ من الدليل المنشور، والقابلُ للترحيل العامل وحده يُعرض.
   · السطر يحمل `accountCode` ولا يحمل `role` ولا `qualifier` على السلك.
   · الطرف يظهر حين يكون الحساب ضابطاً، والفرع حين يكون بُعداً إلزامياً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import type { RawResponse, Transport } from "../src/api/transport";

const COMPANY = "11111111-1111-4111-8111-111111111111";
const AT = "/api/v1/companies/" + COMPANY;

const SETUP = {
  nameAr: "مؤسسة",
  nameTranslations: [],
  decimalPlaces: 2,
  currencyCode: "SAR",
  minorUnits: 2,
  defaultCostCenter: "cc.001",
  costCenters: [{ code: "cc.001", nameAr: "الرئيسي", nameTranslations: [], state: "Active", isDefault: true, suspensionReason: "" }],
};

const CHART = {
  accountCount: 3,
  postableCount: 2,
  accounts: [
    { accountCode: "1000", accountType: "asset", active: true, contra: false, currencyCode: null, currencyMode: "any", level: 1, nameAr: "الأصول", nameTranslations: [], naturalSide: "debit", parentCode: null, postable: false, requiredDimensions: [], subledgerType: "none" },
    { accountCode: "1201", accountType: "asset", active: true, contra: false, currencyCode: null, currencyMode: "any", level: 2, nameAr: "البنك", nameTranslations: [], naturalSide: "debit", parentCode: "1000", postable: true, requiredDimensions: [], subledgerType: "bank_account" },
    { accountCode: "4101", accountType: "revenue", active: true, contra: false, currencyCode: null, currencyMode: "any", level: 2, nameAr: "الإيراد", nameTranslations: [], naturalSide: "credit", parentCode: null, postable: true, requiredDimensions: ["branch"], subledgerType: "none" },
  ],
};

const RECEIPT = {
  entryId: "00000000-0000-4000-8000-000000000001",
  entryNumber: "1",
  entryHash: "00",
  alreadyPosted: false,
  chainSequence: "1",
  periodCode: "2026-10",
  generation: 1,
  lineCount: 2,
};

interface Recorded {
  method: string;
  url: string;
  body?: unknown;
}

function stub(routes: Readonly<Record<string, unknown>>, sent: Recorded[]): Transport {
  return ({ method, url, body }) => {
    sent.push({ method, url, body });
    const at = url.split("?")[0] ?? url;
    const found = routes[method + " " + at];
    if (found === undefined) return Promise.resolve<RawResponse>({ ok: false, status: 404, json: null, url });
    return Promise.resolve<RawResponse>({ ok: true, status: method === "POST" ? 201 : 200, json: found, url });
  };
}

async function mount(transport: Transport): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath: "/voucher" });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
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

function setNativeValue(element: HTMLInputElement | HTMLSelectElement, value: string): void {
  const proto = Object.getPrototypeOf(element) as object;
  // eslint-disable-next-line @typescript-eslint/unbound-method
  const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
  if (setter) setter.call(element, value);
  else element.value = value;
}

async function set(element: HTMLInputElement | HTMLSelectElement, value: string): Promise<void> {
  await act(async () => {
    setNativeValue(element, value);
    element.dispatchEvent(new Event(element instanceof HTMLSelectElement ? "change" : "input", { bubbles: true }));
    await Promise.resolve();
  });
}

async function click(element: Element): Promise<void> {
  await act(async () => {
    (element as HTMLElement).click();
    await Promise.resolve();
    await Promise.resolve();
  });
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

describe("القيد اليدوي بالحسابات", () => {
  it("الحسابات من الدليل المنشور: القابل للترحيل العامل وحده، ولا قائمة أدوار", async () => {
    const sent: Recorded[] = [];
    await mount(stub({ ["GET " + AT + "/setup"]: SETUP, ["GET " + AT + "/chart-of-accounts"]: CHART }, sent));
    const pickers = await screen.findAllByTestId<HTMLSelectElement>("voucher-account");
    await screen.findAllByText("1201 — البنك");
    const values = [...(pickers[0] as HTMLSelectElement).options].map((o) => o.value);
    expect(values).toEqual(["", "1201", "4101"]);
    expect(screen.queryByTestId("voucher-role")).toBeNull();
    expect(screen.queryByTestId("voucher-qualifier")).toBeNull();
  });

  it("الطرف يظهر للحساب الضابط والفرع للبُعد الإلزامي، والجسم يحمل accountCode لا role", async () => {
    const sent: Recorded[] = [];
    await mount(
      stub(
        { ["GET " + AT + "/setup"]: SETUP, ["GET " + AT + "/chart-of-accounts"]: CHART, ["POST " + AT + "/journal-entries"]: RECEIPT },
        sent
      )
    );
    await screen.findAllByText("1201 — البنك");
    const pickers = screen.getAllByTestId<HTMLSelectElement>("voucher-account");

    /* قبل الاختيار في الواجهة المبسّطة: لا طرف ولا فرع. */
    expect(screen.queryByTestId("voucher-party")).toBeNull();
    expect(screen.queryByTestId("voucher-branch")).toBeNull();

    await set(pickers[0] as HTMLSelectElement, "1201");
    await set(pickers[1] as HTMLSelectElement, "4101");
    expect(screen.getAllByTestId("voucher-party")).toHaveLength(1);
    expect(screen.getAllByTestId("voucher-branch")).toHaveLength(1);
    /* نوع الطرف اشتُقّ من نوع الدفتر المساعد في الدليل. */
    expect(screen.getByTestId<HTMLSelectElement>("voucher-subledger-kind").value).toBe("Treasury");

    await set(screen.getByTestId<HTMLInputElement>("voucher-party"), "BANK-0001");
    await set(screen.getByTestId<HTMLInputElement>("voucher-branch"), "BR-01");
    const amounts = screen.getAllByTestId<HTMLInputElement>("voucher-amount");
    await set(amounts[0] as HTMLInputElement, "100.0000");
    await set(amounts[1] as HTMLInputElement, "100.0000");
    await set(screen.getByTestId<HTMLInputElement>("voucher-memo-ar"), "قيد اختبار");
    await set(screen.getByTestId<HTMLInputElement>("voucher-memo-en"), "Test entry");

    await click(screen.getByTestId("voucher-post"));
    await screen.findByTestId("voucher-receipt");

    const post = sent.find((r) => r.method === "POST");
    expect(post).toBeDefined();
    const lines = (post?.body as { lines: Record<string, unknown>[] }).lines;
    expect(lines).toHaveLength(2);
    expect(lines[0]).toMatchObject({ accountCode: "1201", side: "Debit", subledger: { kind: "Treasury", partyId: "BANK-0001" } });
    expect(lines[1]).toMatchObject({ accountCode: "4101", side: "Credit", scope: { branchId: "BR-01" } });
    for (const line of lines) {
      expect(line).not.toHaveProperty("role");
      expect(line).not.toHaveProperty("qualifier");
    }
  });
});
