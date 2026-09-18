/* ═══════════════════════════════════════════════════════════════════════════
   البوّابةُ الأمامية — الدخول ببريدٍ وكلمة مرور، والخروج، والتجديد الصامت
   ───────────────────────────────────────────────────────────────────────────
   **ما يقيسه هذا الملفّ هو بالضبط ما وصفه المالك بأنه يمنع البيع:** لم يكن في
   الواجهة حارسُ مسارٍ واحد. كلُّ شاشةٍ تُفتح بلا جلسة وتقول «اختر المنشأة
   أوّلاً» — فيبدو نظاماً مفتوحاً معطوباً لا نظاماً محمياً.

   **وأربعةٌ تنكسر صامتةً لولا هذه الشواهد:**
     ‏١ · أن النظام **محجوب** فعلاً: لا قائمةَ جانبية ولا شاشةَ تُرسَم بلا جلسة.
     ‏٢ · وأن البابين المفتوحين في العقد يبقيان مفتوحين — وإلّا صار من لا اعتماد
         له لا يستطيع أن يطلب اعتماداً، وهو قفلٌ بلا مفتاح.
     ‏٣ · وأن الدخول بالبريد **يحفظ الجلسة كاملة**: الفاعل والتجديد وانقضاؤه.
         وبلا اعتماد التجديد يصير عمرُ الاعتماد القصير طرداً كلَّ ربع ساعة.
     ‏٤ · وأن الخروج **يمحو** ولا يُخفي، فلا يبقى اعتمادٌ في المخزن بعد «خرجتُ».
   ═══════════════════════════════════════════════════════════════════════════ */
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { needsRenewal, applySession, clearSession, hasSession } from "../src/app/session";
import type { AccessSession } from "../src/api/generated/types";
import { DEFAULT_CONFIG } from "../src/app/config";
import { isOpenScreen, OPEN_SCREENS } from "../src/app/shell/SessionGate";

const COMPANY = "d3305e1e-0000-4000-8000-000000000001";
const TENANT = "d3305e1e-0000-4000-8000-0000000000ff";
const ME = "d3305e1e-0000-4000-8000-0000000000a1";

/* ── ولماذا الانقضاءُ بعيدٌ في هذه العيّنة ───────────────────────────────
   لأن التجديدَ الصامت **يقرأ الساعة**: عيّنةٌ انقضاؤها في الماضي تجعل الشاشة
   تُجدِّد فوراً بدل أن تَدخل، فيقيس الشاهدُ التجديدَ ويظنّ أنه يقيس الدخول.
   وقد وقع ذلك فعلاً: مضى موعدُ العيّنة فصار الشاهدُ يسقط بلا تغييرٍ في شيفرة.
   فالبُعدُ هنا ليس زينة — هو ما يجعل هذا الملفّ يقيس ما سُمّي به. */
const FAR = "2099-01-01T00:00:00.0000000Z";

const OPENED: AccessSession = {
  sessionId: "01920000-0000-7000-8000-000000000001",
  tenantId: TENANT,
  userId: ME,
  generation: 1,
  accessCredential: "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
  accessExpiresAt: FAR,
  refreshCredential: "BBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBBB",
  refreshExpiresAt: FAR,
  writeReachesNothing: false,
  memberships: [{ companyId: COMPANY, role: "Owner" }],
};

const SESSION = {
  tenantId: TENANT,
  userId: ME,
  companyCount: 1,
  companies: [
    {
      companyId: COMPANY,
      state: "Ready",
      nameAr: "منشأةُ قياس",
      nameTranslations: [],
      decimalPlaces: 2,
      defaultCostCenter: "cc.main",
      currencyCode: "SAR",
      minorUnits: 2,
    },
  ],
};

/** الكتابةُ في حقلٍ مضبوط: الواصفُ من النموذج الأصلي لا من النسخة.
    فـReact يضع مُتعقِّباً على النسخة نفسها، والكتابةُ المباشرة تُحدِّث ذاكرته
    فيظنّ أن القيمة لم تتغيّر — فلا يجري `onChange` ولا تُقاس الشاشة. */
function setNativeValue(element: HTMLInputElement, value: string): void {
  const proto = Object.getPrototypeOf(element) as object;
  // eslint-disable-next-line @typescript-eslint/unbound-method
  const setter = Object.getOwnPropertyDescriptor(proto, "value")?.set;
  if (setter) setter.call(element, value);
  else element.value = value;
}

/** يردّ جسماً على مسارٍ ما، و404 على ما عداه. */
function serve(routes: Readonly<Record<string, unknown>>): typeof globalThis.fetch {
  return (input, init) => {
    const url = typeof input === "string" ? input : input instanceof URL ? input.href : input.url;
    const method = (init?.method ?? "GET").toUpperCase();
    const at = (url.split("?")[0] ?? url).replace(/^https?:\/\/[^/]+/, "");
    const found = routes[method + " " + at];
    const ok = found !== undefined;
    return Promise.resolve(
      new Response(JSON.stringify(ok ? found : { type: "about:blank", status: 404 }), {
        status: ok ? 200 : 404,
        headers: { "content-type": ok ? "application/json" : "application/problem+json" },
      })
    );
  };
}

/** يُمهل الدورةَ التالية: نداءٌ يعبر الشبكة ثم يكتب حالةً لا يكتمل في اللحظة نفسها. */
function settled(): Promise<void> {
  return new Promise((resolve) => setTimeout(resolve, 0));
}

function stored(): Record<string, string> {
  return JSON.parse(globalThis.localStorage.getItem("sb-api-config") ?? "{}") as Record<string, string>;
}

async function mount(path: string): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath: path });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  function Tree(): ReactNode {
    return (
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider>
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
  globalThis.localStorage.clear();
  globalThis.history.replaceState(null, "", "/");
});

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  globalThis.localStorage.clear();
});

describe("الحجب — لا يُرسَم شيءٌ من النظام بلا جلسة", () => {
  it("مسارٌ عميق بلا جلسة يُرسَم بوّابةً، ولا قائمةَ جانبية خلفها", async () => {
    await mount("/hr/payroll");
    expect(screen.getByTestId("session-gate")).toBeTruthy();
    /* والقشرةُ غائبة **بأجزائها**: قائمةٌ جانبية أو مُشغّلُ أنظمةٍ خلف بوّابةٍ
       يعني أن الحجب رسمٌ لا منع. */
    expect(document.querySelector(".app-side")).toBeNull();
    expect(screen.queryByTestId("open-launcher")).toBeNull();
    expect(screen.queryByTestId("hr-payroll-screen")).toBeNull();
  });

  it("والمسارُ المطلوب يُقال، فلا يُقرأ الحجبُ ضياعاً", async () => {
    await mount("/inventory/items");
    expect(screen.getByTestId("gate-return").textContent).toContain("/inventory/items");
  });

  it("والبابان المفتوحان يُرسَمان داخل البوّابة — بلا قشرةٍ حولهما", async () => {
    /* شاشةُ الانتساب تُفتح بلا جلسة — وهذا حقٌّ لها في العقد المنشور — ولكنّها
       لا تستحقّ قائمةً جانبية حولها أكثر ممّا تستحقّها شاشةُ الدخول. */
    await mount("/admin/enrolment");
    expect(screen.getByTestId("admin-enrolment-screen")).toBeTruthy();
    expect(document.querySelector(".app-side")).toBeNull();
    expect(screen.queryByTestId("sign-in-password-form")).toBeNull();
    cleanup();

    await mount("/sign-in");
    expect(screen.getByTestId("sign-in-password-form")).toBeTruthy();
    expect(document.querySelector(".app-side")).toBeNull();
  });

  it("والقائمةُ مقفلةٌ لا بادئة — فلا يفتح «/admin/» الأعضاءَ والاشتراكَ معه", () => {
    expect(OPEN_SCREENS).toEqual(["/sign-in", "/admin/enrolment"]);
    expect(isOpenScreen("/admin/members")).toBe(false);
    expect(isOpenScreen("/admin/subscription")).toBe(false);
    expect(isOpenScreen("/admin/plans")).toBe(false);
    expect(isOpenScreen("/admin/enrolment")).toBe(true);
  });
});

describe("الدخول ببريدٍ وكلمة مرور", () => {
  it("يفتح جلسةً، ويحفظ الفاعلَ والتجديدَ وانقضاءه، وتزول البوّابة", async () => {
    vi.stubGlobal(
      "fetch",
      serve({
        "POST /api/v1/access/sessions/password": OPENED,
        "GET /api/v1/session": SESSION,
        "GET /health": { status: "ok", culture: "ar-SA", calendar: "GregorianCalendar", apiVersion: "v1" },
      })
    );

    await mount("/hr/payroll");
    expect(screen.getByTestId("session-gate")).toBeTruthy();

    const handle = screen.getByTestId<HTMLInputElement>("sign-in-handle");
    const password = screen.getByTestId<HTMLInputElement>("sign-in-password");

    await act(async () => {
      setNativeValue(handle, "owner@example.sa");
      handle.dispatchEvent(new Event("input", { bubbles: true }));
      setNativeValue(password, "a-long-enough-password");
      password.dispatchEvent(new Event("input", { bubbles: true }));
      await settled();
    });

    await act(async () => {
      screen.getByTestId<HTMLButtonElement>("sign-in-password-submit").click();
      await settled();
    });

    await waitFor(() => expect(stored()["token"]).toBe(OPENED.accessCredential));
    expect(stored()["refreshToken"]).toBe(OPENED.refreshCredential);
    expect(stored()["tokenExpiresAt"]).toBe(OPENED.accessExpiresAt);
    /* وعضويةٌ واحدة تُفتح على منشأتها: من له منشأةٌ واحدة لا يُسأل أيَّها يريد. */
    expect(stored()["companyId"]).toBe(COMPANY);
  });
});

describe("الخروج — محوٌ لا إخفاء", () => {
  it("زرُّ الرأس يمحو الاعتمادَ والتجديد، وتعود البوّابة", async () => {
    globalThis.localStorage.setItem(
      "sb-api-config",
      JSON.stringify({ ...DEFAULT_CONFIG, token: "t", refreshToken: "r", companyId: COMPANY })
    );
    vi.stubGlobal("fetch", serve({}));

    await mount("/");
    expect(screen.queryByTestId("session-gate")).toBeNull();

    await act(async () => {
      screen.getByTestId<HTMLButtonElement>("sign-out-shell").click();
      await settled();
    });

    await waitFor(() => expect(screen.getByTestId("session-gate")).toBeTruthy());
    expect(stored()["token"]).toBe("");
    expect(stored()["refreshToken"]).toBe("");
  });
});

describe("حسابُ التجديد — دالّةٌ خالصة تُقاس بلا ساعة", () => {
  const base = { ...DEFAULT_CONFIG, token: "t", refreshToken: "r" };

  it("لا تجديدَ بلا اعتماد تجديد، ولا بلحظةٍ لا تُقرأ", () => {
    expect(needsRenewal({ ...base, refreshToken: "" }, 0)).toBe(false);
    expect(needsRenewal({ ...base, tokenExpiresAt: "ليس تاريخاً" }, 0)).toBe(false);
  });

  it("يُجدَّد قبل الانقضاء بدقيقتين لا بعده — والفرقُ هو ألّا يُطرَد أحدٌ في منتصف قيد", () => {
    const now = Date.parse("2026-09-18T20:00:00Z");
    const far = { ...base, tokenExpiresAt: "2026-09-18T20:10:00Z" };
    const near = { ...base, tokenExpiresAt: "2026-09-18T20:01:00Z" };
    expect(needsRenewal(far, now)).toBe(false);
    expect(needsRenewal(near, now)).toBe(true);
  });

  it("والجلسةُ تُقاس بالاعتماد الفاعل وحده — فرمزُ العرض بلا تجديدٍ جلسةٌ صحيحة", () => {
    expect(hasSession({ ...DEFAULT_CONFIG, token: "demo-token" })).toBe(true);
    expect(hasSession(DEFAULT_CONFIG)).toBe(false);
    expect(hasSession(clearSession({ ...base, token: "x" }))).toBe(false);
  });

  it("وتطبيقُ الجلسة يكتب الثلاثة معاً — ولا يُخمِّن منشأةً حين تكون أكثر من واحدة", () => {
    const two: AccessSession = {
      ...OPENED,
      memberships: [
        { companyId: COMPANY, role: "Owner" },
        { companyId: TENANT, role: "Reader" },
      ],
    };
    expect(applySession(DEFAULT_CONFIG, two).companyId).toBe("");
    expect(applySession(DEFAULT_CONFIG, OPENED).companyId).toBe(COMPANY);
    expect(applySession(DEFAULT_CONFIG, OPENED).refreshToken).toBe(OPENED.refreshCredential);
  });
});
