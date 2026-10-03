/* ═══════════════════════════════════════════════════════════════════════════
   شاشاتُ الإدخال نوافذٌ منبثقة (ADR-0097)
   ───────────────────────────────────────────────────────────────────────────
   · شاشةُ الإدخال تُرسَم داخل `entry-dialog` بمسارها نفسه، والسجلُّ لا يُغلَّف.
   · الإغلاق يعود إلى صفحة القسم الأولى، وEscape داخل حقلٍ لا يُغلق.
   · والرايةُ على المستندات والسندات وحدها: لا على صفحة البداية ولا على التقارير.
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { act, cleanup, fireEvent, render, screen } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider, type AnyRouter } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS, isEntryScreen } from "../src/app/shell/sections";
import type { RawResponse, Transport } from "../src/api/transport";

const COMPANY = "11111111-1111-4111-8111-111111111111";

const EMPTY: Transport = ({ url }) => Promise.resolve<RawResponse>({ ok: false, status: 404, json: null, url });

async function mount(initialPath: string): Promise<AnyRouter> {
  const router = createAppRouter({ memory: true, initialPath });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider transport={EMPTY}>
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
  return router;
}

beforeEach(() => {
  globalThis.localStorage.setItem("sb-show-advanced", "1");
  globalThis.localStorage.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: COMPANY, book: "MAIN", period: "" })
  );
});

afterEach(() => {
  cleanup();
  globalThis.localStorage.clear();
});

describe("نافذةُ الإدخال", () => {
  it("سندُ الصرف يُرسَم داخل نافذةٍ منبثقة بمسارها، والشجرةُ خلفها حاضرة", async () => {
    await mount("/purchasing/payment");
    const dialog = screen.getByTestId("entry-dialog");
    expect(dialog.getAttribute("role")).toBe("dialog");
    expect(dialog.getAttribute("aria-modal")).toBe("true");
    expect(dialog.querySelector('[data-testid="acc-supplier-payment-screen"]')).not.toBeNull();
    expect(screen.getByTestId("brand-home")).toBeDefined();
    expect(document.getElementById("main")?.getAttribute("data-entry")).toBe("true");
  });

  it("التقريرُ صفحةٌ لا نافذة", async () => {
    await mount("/sales/receivables");
    expect(screen.queryByTestId("entry-dialog")).toBeNull();
    expect(document.getElementById("main")?.getAttribute("data-entry")).toBeNull();
  });

  it("الإغلاق يعود إلى صفحة القسم الأولى، وEscape داخل حقلٍ لا يُغلق", async () => {
    const router = await mount("/hr/payroll");
    const field = screen.getByTestId("entry-dialog").querySelector("input, select, textarea");
    expect(field, "شاهدٌ إيجابي: في الشاشة حقلٌ واحد على الأقل").not.toBeNull();
    await act(async () => {
      fireEvent.keyDown(field as Element, { key: "Escape" });
      await Promise.resolve();
    });
    expect(router.state.location.pathname).toBe("/hr/payroll");

    await act(async () => {
      fireEvent.click(screen.getByTestId("entry-dialog-close"));
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(router.state.location.pathname).toBe("/hr");
    expect(screen.queryByTestId("entry-dialog")).toBeNull();
  });

  it("Escape من خارج الحقول يُغلق إلى صفحة القسم", async () => {
    const router = await mount("/sales/invoice");
    await act(async () => {
      fireEvent.keyDown(screen.getByTestId("entry-dialog"), { key: "Escape" });
      await Promise.resolve();
      await Promise.resolve();
    });
    expect(router.state.location.pathname).toBe("/");
  });

  it("الرايةُ على المستندات والسندات وحدها", () => {
    const entries = SCREENS.filter((s) => s.entry === true).map((s) => s.path);
    expect(entries).toHaveLength(24);
    for (const path of ["/home", "/", "/sales/receivables", "/inventory/items", "/hr", "/setup", "/admin/members"]) {
      expect(isEntryScreen(path), path + " صفحة").toBe(false);
    }
    for (const path of ["/voucher", "/purchasing/payment", "/sales/invoice", "/hr/payroll", "/realestate/lease"]) {
      expect(isEntryScreen(path), path + " نافذة").toBe(true);
    }
    for (const s of SCREENS.filter((s) => s.entry === true)) expect(s.universal, s.path).toBeUndefined();
  });
});
