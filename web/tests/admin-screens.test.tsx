/* ═══════════════════════════════════════════════════════════════════════════
   شاشات الإدارة الأربع — حرّاسها
   The four administration screens — their guards
   ───────────────────────────────────────────────────────────────────────────
   ثمانيةُ أشياء تُفحص هنا، وكلٌّ منها ينكسر بصمت لو لم يُفحص:

     ١ · القوائم الثلاث تتّفق. SCREENS والموجّه وقائمةُ الملاحة اليدوية في
         App.tsx ثلاثُ نسخٍ ولا شيء يقارنها، فشاشةٌ في واحدةٍ دون الأخرى
         تُفتح بلوحة الأوامر ولا يراها من يقرأ الملاحة.
     ٢ · لا اعتماد يصل إلى DOM. والاختبار يبحث عن نصّ الاعتماد نفسه في
         الصفحة كلّها — نصّاً وسماتٍ وقيمَ حقولٍ معاً — لا عن غياب مكوّنٍ
         بعينه.
     ٣ · دورُ «قراءةٌ فقط» يُقال قبل الضغط ويُظهَر رفضُه بعده — ولا زرَّ
         يُعطَّل. أربعةُ فحوصٍ لا واحد: الإعلان، وغيابُه لمن ليس قارئاً،
         وأنّ الزرّ باقٍ عاملاً والطلبَ يغادر فعلاً، وأنّ الرفض يخرج برمزه
         المنشور ومعه الخطوة التالية.
     ٤ · إبطال الجلسة يقول أثره قبل الضغط، والزرّ مُقفَلٌ قبل الإقرار، ولا
         يغادر طلبٌ واحد قبله.
     ٥ · وبعد الإبطال يُمسح الاعتماد من الإعداد، فلا تبقى شاشةٌ تعرض رفضاً
         بلا سبب مفهوم.
     ٦ · الانقطاع يُري ما يتوقّف قبل التنفيذ: القراءة لا تتوقّف، وعددُ
         الوحدات الكاتبة، وأرضيّةُ كلّ وحدة — ولا تُخترَع أرضيةٌ لا يسمّيها
         العقد.
     ٧ · كل حقلٍ في صفّ يحمل وصفاً (ADR-0078)، والوعاء هو `.grid` المُسجَّل
         في `styles/components.css` لا وعاءٌ يُخترَع.
     ٨ · لا رقم على مالٍ ولا على معرّف: لا `Number(` ولا `parseFloat` في
         ملفّات هذا القسم.

   ولا بيان شخصي في هذا الملفّ: الأسماء أدناه أسماءٌ اصطلاحية («عضوٌ للقياس»)
   لا أسماءُ أشخاص، ولا بريدَ ولا جوّالَ ولا هويّة — ولا حاجة إليها، فالعضوية
   في العقد اسمٌ ودورٌ ولحظةُ منح لا أكثر. والاعتمادات أدناه قيمُ اختبارٍ
   مُعلَنة بأسمائها، لا أسرارٌ.
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
import { releaseRefreshCredential } from "../src/screens/admin/credential-hold";
import type { RawResponse, Transport } from "../src/api/transport";

const SRC = path.resolve(process.cwd(), "src");
const COMPANY = "11111111-1111-4111-8111-111111111111";
const TENANT = "11111111-1111-4111-8111-111111111110";
const ME = "11111111-1111-4111-8111-11111111110a";
const OTHER = "11111111-1111-4111-8111-11111111110b";

/** المسارات الخمسة بترتيب العمل — والترتيب نفسه في الملاحة وفي SCREENS. */
const ADMIN_PATHS = [
  "/admin/enrolment",
  "/admin/session",
  "/admin/members",
  "/admin/subscription",
  "/admin/plans",
];

/** قيمُ اختبارٍ مُعلَنة لا أسرار: أطوالها تطابق أدنى ما يقبله العقد. */
const FAKE_ENROLMENT = "enrolment-value-for-the-test-only";
const FAKE_ACCESS = "access-value-for-the-test-only";
const FAKE_REFRESH = "refresh-value-for-the-test-only";

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

function memberships(myRole: string) {
  return {
    companyId: COMPANY,
    memberCount: 2,
    members: [
      {
        userId: ME,
        displayNameAr: "عضوٌ للقياس",
        role: myRole,
        grantedAt: "2026-01-01T00:00:00.0000000Z",
      },
      {
        userId: OTHER,
        displayNameAr: "عضوٌ ثانٍ للقياس",
        role: "Contributor",
        grantedAt: "2026-02-01T00:00:00.0000000Z",
      },
    ],
  };
}

const SUBSCRIPTION = {
  currency: "SAR",
  endsOn: null,
  includedUsers: 8,
  modules: [
    { code: "AP", nameAr: "الذمم الدائنة", postsJournal: true, state: "Entitled" },
    { code: "AR", nameAr: "الذمم المدينة", postsJournal: true, state: "Entitled" },
    { code: "POS", nameAr: "نقاط البيع", postsJournal: false, state: "NotEntitled" },
    { code: "REP", nameAr: "التقارير", postsJournal: false, state: "Entitled" },
  ],
  monthlyPrice: "1800.0000",
  nameAr: "مستأجرُ قياس",
  perUserPrice: "55.0000",
  planCode: "GROWTH",
  planNameAr: "النامية",
  renewsOn: "2026-10-01",
  startedOn: "2026-01-01",
  state: "Active",
  subscriptionId: "22222222-2222-4222-8222-222222222222",
  tenantCode: "T-0001",
  tenantId: TENANT,
  tenantStatus: "Active",
};

/** خطّتان من كتالوج المنصّة (ADR-0092): منشورةٌ ومسودّة — والمال نصٌّ على السلك. */
const PLAN_ESSENTIAL = {
  code: "ESSENTIAL",
  nameAr: "الأساسية",
  nameTranslations: [{ name: "en", value: "Essential" }],
  monthlyPrice: "900.0000",
  perUserPrice: "60.0000",
  currency: "SAR",
  includedUsers: 3,
  modules: ["AP", "AR", "CORE"],
  published: true,
};
const PLAN_FULL = {
  ...PLAN_ESSENTIAL,
  code: "FULL",
  nameAr: "الشاملة",
  nameTranslations: [{ name: "en", value: "Full" }],
  monthlyPrice: "3600.0000",
  perUserPrice: "45.0000",
  includedUsers: 20,
  modules: ["AP", "AR", "CORE", "INV", "POS", "REP"],
};
const PLAN_DRAFT = {
  ...PLAN_ESSENTIAL,
  code: "TRIAL",
  nameAr: "تجربة",
  nameTranslations: [{ name: "en", value: "Trial" }],
  monthlyPrice: "0.0000",
  perUserPrice: "0.0000",
  includedUsers: 1,
  modules: ["CORE", "INV"],
  published: false,
};

/* ══════════════════════════════════════════════════════════ أدوات ═════ */

interface Recorded {
  method: string;
  url: string;
  body?: unknown;
}

interface Refusal {
  status: number;
  code: string;
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
  refuse?: Readonly<Record<string, Refusal>>;
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

/** يختار قيمةً في قائمةٍ منسدلة — React يستمع إلى change على select. */
async function choose(element: HTMLSelectElement, value: string): Promise<void> {
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

/** يقرأ حقلَ إدخالٍ بمعرّفه — ويرمي إن لم يكن حقلاً. */
function input(testId: string): HTMLInputElement {
  const found = screen.getByTestId(testId);
  if (!(found instanceof HTMLInputElement)) throw new Error("ليس حقلاً: " + testId);
  return found;
}

/** كلُّ نصٍّ في الصفحة وكلُّ قيمة سمة وكلُّ قيمة حقل — فالبحث يشمل `value`. */
function everythingOnThePage(): string {
  const parts: string[] = [document.body.innerHTML];
  for (const element of document.querySelectorAll("*")) {
    for (const attribute of element.attributes) parts.push(attribute.value);
    if (element instanceof HTMLInputElement) parts.push(element.value);
  }
  return parts.join(" ");
}

/** ينزع التعليقات: الحارس يفحص الشيفرة، والنثرُ الذي يصف امتناعاً ليس مخالفة. */
function stripComments(text: string): string {
  return text.replace(/\/\*[\s\S]*?\*\//g, " ").replace(/(^|[^:"'`])\/\/[^\n]*/g, "$1");
}

/** زرٌّ بمعرّفه — والنوع مضبوطٌ في موضعٍ واحد لا في كلّ فحص. */
function button(testId: string): HTMLButtonElement {
  const found = screen.getByTestId(testId);
  if (!(found instanceof HTMLButtonElement)) throw new Error("ليس زرّاً: " + testId);
  return found;
}

function sourceOf(file: string): string {
  return readFileSync(path.resolve(SRC, "screens/admin/" + file), "utf8");
}

const SCREEN_FILES = [
  "EnrolmentScreen.tsx",
  "SessionScreen.tsx",
  "MembersScreen.tsx",
  "SubscriptionScreen.tsx",
  "PlansScreen.tsx",
  "parts.tsx",
];

beforeEach(() => {
  releaseRefreshCredential();
  globalThis.localStorage.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: COMPANY, book: "MAIN", period: "" })
  );
});

afterEach(() => {
  cleanup();
  releaseRefreshCredential();
  globalThis.localStorage.clear();
});

/* ═══════════════════════════════════════════════════════════════════════
   ١ · القوائم الثلاث تتّفق
   ═══════════════════════════════════════════════════════════════════════ */
describe("الملاحة اليدوية ونسختها في العقد", () => {
  it("كل شاشة إدارةٍ في SCREENS لها رابطٌ في قائمة الملاحة اليدوية", async () => {
    await mount({ path: "/admin/enrolment", transport: stub({ routes: {} }) });
    const nav = document.querySelector(".app-side");
    expect(nav).not.toBeNull();
    const hrefs = [...(nav?.querySelectorAll("a[href]") ?? [])].map((a) => a.getAttribute("href"));
    const declared = SCREENS.filter((s) => s.path.startsWith("/admin/")).map((s) => s.path);
    expect(declared).toEqual(ADMIN_PATHS);
    for (const target of declared) expect(hrefs).toContain(target);
  });

  it("والشريط داخل المجموعة يحمل الأربع نفسها — لا ثالثةً ولا خامسة", async () => {
    await mount({ path: "/admin/session", transport: stub({ routes: {} }) });
    const tabs = await screen.findByTestId("admin-tabs");
    const inside = [...tabs.querySelectorAll("a[href]")].map((a) => a.getAttribute("href"));
    expect(inside).toEqual(ADMIN_PATHS);
  });

  it("وكل مسارٍ من الأربعة يفتح شاشته في الموجّه", async () => {
    for (const at of ADMIN_PATHS) {
      const suffix = at.split("/")[2];
      await mount({ path: at, transport: stub({ routes: {} }) });
      expect(await screen.findByTestId("admin-" + suffix + "-screen")).toBeTruthy();
      cleanup();
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٢ · لا اعتماد يصل إلى DOM
   ═══════════════════════════════════════════════════════════════════════ */
describe("الاعتماد لا يُرسَم", () => {
  it("اعتماد الانتساب الصادر عن الدعوة لا يظهر في الصفحة — لا نصّاً ولا في سمة", async () => {
    const granted = {
      companyId: COMPANY,
      enrolmentCredential: FAKE_ENROLMENT,
      enrolmentExpiresAt: "2026-03-01T00:00:00.0000000Z",
      member: {
        userId: OTHER,
        displayNameAr: "مدعوٌّ للقياس",
        role: "Contributor",
        grantedAt: "2026-02-02T00:00:00.0000000Z",
      },
    };
    await mount({
      path: "/admin/members",
      transport: stub({
        routes: {
          "GET /api/v1/session": SESSION,
          ["GET /api/v1/companies/" + COMPANY + "/memberships"]: memberships("Owner"),
          ["POST /api/v1/companies/" + COMPANY + "/memberships"]: granted,
        },
      }),
    });
    await type(await screen.findByTestId("admin-members-invite-name"), "مدعوٌّ للقياس");
    await click(await screen.findByTestId("admin-members-invite-go"));
    await waitFor(() => expect(screen.getByTestId("admin-members-granted")).toBeTruthy());

    /* اللوح ظهر — أي أنّ الاعتماد وصل فعلاً إلى الشاشة. وهذا شرط: فحصٌ يمرّ
       لأنّ الطلب فشل ليس فحصاً. */
    expect(screen.getByTestId("admin-members-granted-expires").textContent).toContain("2026-03-01");
    expect(everythingOnThePage()).not.toContain(FAKE_ENROLMENT);
  });

  it("واعتمادا الجلسة المفتوحة لا يظهران في الصفحة", async () => {
    const opened = {
      accessCredential: FAKE_ACCESS,
      accessExpiresAt: "2026-02-02T01:00:00.0000000Z",
      generation: 1,
      memberships: [{ companyId: COMPANY, role: "Reader" }],
      refreshCredential: FAKE_REFRESH,
      refreshExpiresAt: "2026-03-02T00:00:00.0000000Z",
      sessionId: "33333333-3333-4333-8333-333333333333",
      tenantId: TENANT,
      userId: ME,
      writeReachesNothing: true,
    };
    await mount({
      path: "/admin/enrolment",
      transport: stub({ routes: { "POST /api/v1/access/sessions": opened } }),
    });
    await type(await screen.findByTestId("admin-enrolment-credential"), FAKE_ENROLMENT);
    await click(await screen.findByTestId("admin-enrolment-open-go"));
    await waitFor(() => expect(screen.getByTestId("admin-enrolment-session")).toBeTruthy());

    /* والجلسة فُتحت فعلاً: الدورة معروضة. */
    expect(screen.getByTestId("admin-enrolment-generation").textContent).toBeTruthy();
    /* و«لا تكتب في أي منشأة» تُقال لأنّ الخادم قالها في حقلٍ منشور. */
    expect(screen.getByTestId("admin-enrolment-write-reaches-nothing")).toBeTruthy();
    const page = everythingOnThePage();
    expect(page).not.toContain(FAKE_ACCESS);
    expect(page).not.toContain(FAKE_REFRESH);
    expect(page).not.toContain(FAKE_ENROLMENT);
  });

  it("ولا اعتماد يُودَع على القرص ولا يُسجَّل في طرفية", () => {
    for (const file of SCREEN_FILES) {
      const text = stripComments(sourceOf(file));
      expect(/localStorage|sessionStorage|document\.cookie/.test(text), file).toBe(false);
      expect(/console\.(log|warn|error|info)/.test(text), file).toBe(false);
    }
    /* والحجز في متغيّر وحدةٍ لا على القرص. والتعليقات تُنزع قبل الفحص:
       الملفّ **يسمّي** ما يمتنع عنه في صدره، وحارسٌ يقرأ النثر يقرأ الامتناع
       مخالفةً. */
    const hold = stripComments(
      readFileSync(path.resolve(SRC, "screens/admin/credential-hold.ts"), "utf8")
    );
    expect(/localStorage|sessionStorage|document\.cookie|indexedDB/.test(hold)).toBe(false);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ · دورُ «قراءةٌ فقط» — مقيساً لا موصوفاً
   ═══════════════════════════════════════════════════════════════════════ */
describe("دورُ قراءةٍ فقط", () => {
  async function mountMembers(role: string, refuse?: Readonly<Record<string, Refusal>>) {
    await mount({
      path: "/admin/members",
      transport: stub({
        routes: {
          "GET /api/v1/session": SESSION,
          ["GET /api/v1/companies/" + COMPANY + "/memberships"]: memberships(role),
        },
        ...(refuse ? { refuse } : {}),
      }),
    });
    await screen.findByTestId("admin-members-table");
  }

  it("يُعلَن قبل الضغط، ودورُ صاحب الجلسة مقروءٌ من القائمة نفسها", async () => {
    await mountMembers("Reader");
    const notice = await screen.findByTestId("admin-members-read-only");
    expect(notice.getAttribute("data-role")).toBe("Reader");
    expect(notice.textContent).toContain("membership.read_only");
  });

  it("ولا يظهر لمن ليس قارئاً — فالإعلان معلومةٌ لا زينة", async () => {
    await mountMembers("Owner");
    expect(screen.queryByTestId("admin-members-read-only")).toBeNull();
  });

  it("والزرّ يبقى عاملاً: الإخفاء ليس منعاً، والمنع في الخادم", async () => {
    await mountMembers("Reader");
    await screen.findByTestId("admin-members-invite-go");
    /* مُقفَلٌ لأنّ الاسم فارغ — لا لأنّ الدور قارئ. */
    expect(button("admin-members-invite-go").disabled).toBe(true);
    await type(await screen.findByTestId("admin-members-invite-name"), "مدعوٌّ للقياس");
    expect(button("admin-members-invite-go").disabled).toBe(false);
  });

  it("ورفضُ الخادم يظهر برمزه المنشور ومعه الخطوة التالية", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/admin/members",
      transport: stub({
        routes: {
          "GET /api/v1/session": SESSION,
          ["GET /api/v1/companies/" + COMPANY + "/memberships"]: memberships("Reader"),
        },
        refuse: {
          ["POST /api/v1/companies/" + COMPANY + "/memberships"]: {
            status: 403,
            code: "membership.read_only",
          },
        },
        sent,
      }),
    });
    await screen.findByTestId("admin-members-table");
    await type(await screen.findByTestId("admin-members-invite-name"), "مدعوٌّ للقياس");
    await click(await screen.findByTestId("admin-members-invite-go"));

    /* الطلب غادر فعلاً: الشاشة لم تمنع، والخادم هو من ردّ. */
    expect(sent.some((r) => r.method === "POST" && r.url.endsWith("/memberships"))).toBe(true);
    await waitFor(() =>
      expect(screen.getByTestId("problem-code").textContent).toBe("membership.read_only")
    );
    expect(screen.getByTestId("admin-members-invite-next").getAttribute("data-code")).toBe(
      "membership.read_only"
    );
    /* ولا لوحةَ نجاحٍ بعد رفض. */
    expect(screen.queryByTestId("admin-members-granted")).toBeNull();
  });

  it("ورمزُ الاستحقاق يفترق عن رمز الدور — ولكلٍّ خطوةٌ أخرى", async () => {
    await mount({
      path: "/admin/members",
      transport: stub({
        routes: {
          "GET /api/v1/session": SESSION,
          ["GET /api/v1/companies/" + COMPANY + "/memberships"]: memberships("Owner"),
        },
        refuse: {
          ["POST /api/v1/companies/" + COMPANY + "/memberships"]: {
            status: 403,
            code: "entitlement.read_only",
          },
        },
      }),
    });
    await screen.findByTestId("admin-members-table");
    await type(await screen.findByTestId("admin-members-invite-name"), "مدعوٌّ للقياس");
    await click(await screen.findByTestId("admin-members-invite-go"));
    await waitFor(() =>
      expect(screen.getByTestId("admin-members-invite-next").getAttribute("data-code")).toBe(
        "entitlement.read_only"
      )
    );
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٣ب · بريدُ الدخول وكلمتُه — البابُ الذي بلا شاشةٍ يبقى مقفلاً
   ───────────────────────────────────────────────────────────────────────
   العقد يفتح `PUT /api/v1/access/password` منذ ADR-0094، وبلا لوحٍ يناديه
   لا يستطيع أحدٌ أن يضبط كلمةً — فتصير البوّابةُ الأمامية باباً بلا مفتاح:
   حقلُ بريدٍ لا يقبل بريداً لأن أحداً لم يُسجَّل قطّ.
   ═══════════════════════════════════════════════════════════════════════ */
describe("ضبط بريد الدخول وكلمته", () => {
  const SET = { handle: "owner@example.sa", setAt: "2026-02-02T02:00:00.0000000Z" };

  it("الزرّ مُقفَلٌ حتى يصحّ البريد ويبلغ الطولُ حدَّه، ولا طلبَ يغادر", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/admin/session",
      transport: stub({ routes: { "GET /api/v1/session": SESSION }, sent }),
    });
    const save = await screen.findByTestId("admin-session-signin-save");
    expect(button("admin-session-signin-save").disabled).toBe(true);

    /* بريدٌ بلا @ وكلمةٌ قصيرة: الحدّان معاً يُقاسان، لا أحدهما. */
    await type(input("admin-session-handle"), "owner");
    await type(input("admin-session-password"), "short");
    expect(button("admin-session-signin-save").disabled).toBe(true);

    await type(input("admin-session-handle"), "owner@example.sa");
    expect(button("admin-session-signin-save").disabled).toBe(true);

    await click(save);
    expect(sent.some((r) => r.url.includes("/access/password"))).toBe(false);
  });

  it("ويُرسل البريدَ والكلمة، ثم يُقرّ بما حُفظ ويمحو الكلمة من الحقل", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/admin/session",
      transport: stub({
        routes: { "GET /api/v1/session": SESSION, "PUT /api/v1/access/password": SET },
        sent,
      }),
    });
    await screen.findByTestId("admin-session-signin-form");
    await type(input("admin-session-handle"), "owner@example.sa");
    await type(input("admin-session-password"), "a-long-enough-password");
    await click(screen.getByTestId("admin-session-signin-save"));

    const put = sent.find((r) => r.method === "PUT" && r.url.includes("/access/password"));
    expect(put).toBeTruthy();
    expect((put?.body as { handle: string }).handle).toBe("owner@example.sa");

    const done = await screen.findByTestId("admin-session-signin-done");
    expect(done.textContent ?? "").toContain("owner@example.sa");
    /* **والكلمةُ تُمحى، والبريدُ يبقى:** حقلٌ يُبقي كلمةَ مرورٍ مكتوبةً في شاشةٍ
       مفتوحة على مكتبٍ مشترك هو تسريبٌ صامت، والبريدُ ليس سرّاً. */
    expect(input("admin-session-password").value).toBe("");
    expect(input("admin-session-handle").value).toBe("owner@example.sa");
  });

  it("والرفضُ يُقال برمزه — البابُ هذا يسمّي ما فشل، بخلاف باب الدخول", async () => {
    await mount({
      path: "/admin/session",
      transport: stub({
        routes: { "GET /api/v1/session": SESSION },
        refuse: {
          "PUT /api/v1/access/password": { status: 422, code: "access.handle_taken" },
        },
      }),
    });
    await screen.findByTestId("admin-session-signin-form");
    await type(input("admin-session-handle"), "owner@example.sa");
    await type(input("admin-session-password"), "a-long-enough-password");
    await click(screen.getByTestId("admin-session-signin-save"));
    await waitFor(() =>
      expect(screen.getByTestId("problem-code").textContent).toBe("access.handle_taken")
    );
    expect(screen.queryByTestId("admin-session-signin-done")).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٤ و٥ · إبطال الجلسة: أثرُه قبل الضغط، وأثرُه بعده
   ═══════════════════════════════════════════════════════════════════════ */
describe("إبطال الجلسة", () => {
  const revocation = {
    reason: "signed_out",
    revokedAt: "2026-02-02T02:00:00.0000000Z",
    sessionId: "33333333-3333-4333-8333-333333333333",
  };

  it("يقول من يخرج — والجواب: صاحب الجلسة نفسه وكلُّ أجهزته", async () => {
    await mount({
      path: "/admin/session",
      transport: stub({ routes: { "GET /api/v1/session": SESSION } }),
    });
    const effects = await screen.findByTestId("admin-session-revoke-effects");
    const items = [...effects.querySelectorAll("li")].map((li) => li.textContent ?? "");
    expect(items.length).toBe(4);
    /* أربعةُ بنودٍ لا جملةٌ واحدة، وكلٌّ منها يقول شيئاً لا يقوله غيره. */
    expect(new Set(items).size).toBe(4);
    for (const item of items) expect(item.length).toBeGreaterThan(20);
  });

  it("والزرّ مُقفَلٌ قبل الإقرار، ولا طلبَ يغادر", async () => {
    const sent: Recorded[] = [];
    await mount({
      path: "/admin/session",
      transport: stub({ routes: { "GET /api/v1/session": SESSION }, sent }),
    });
    const go = await screen.findByTestId("admin-session-revoke-confirm-go");
    expect(button("admin-session-revoke-confirm-go").disabled).toBe(true);
    await click(go);
    expect(sent.some((r) => r.url.includes("/access/sessions/revocation"))).toBe(false);
  });

  it("ويُفتح بعد الإقرار — والإقرار نصُّه هو الأثر لا «هل أنت متأكّد؟»", async () => {
    await mount({
      path: "/admin/session",
      transport: stub({ routes: { "GET /api/v1/session": SESSION } }),
    });
    const ack = await screen.findByTestId("admin-session-revoke-confirm-ack");
    expect((ack.parentElement?.textContent ?? "").length).toBeGreaterThan(30);
    await click(ack);
    await waitFor(() =>
      expect(button("admin-session-revoke-confirm-go").disabled).toBe(false)
    );
  });

  it("وبعد وقوعه يُمسح الاعتماد من الإعداد، ويُعرض سببُ الإبطال كما ورد", async () => {
    await mount({
      path: "/admin/session",
      transport: stub({
        routes: {
          "GET /api/v1/session": SESSION,
          "POST /api/v1/access/sessions/revocation": revocation,
        },
      }),
    });
    await click(await screen.findByTestId("admin-session-revoke-confirm-ack"));
    await click(await screen.findByTestId("admin-session-revoke-confirm-go"));

    /* ‏**وما يُرى بعد الإبطال صار البوّابةَ الأمامية لا لوحَ «أُبطلت».** وهذا
       تغيُّرُ سلوكٍ مقصود جاء مع البوّابة (ADR-0094): إبطالُ جلستك خروجٌ، والخروجُ
       يُغلق النظام خلفك في اللحظة نفسها. ولوحٌ يبقى مرسوماً بعد محو الاعتماد
       كان يعني أن الشاشةَ باقيةٌ وقد زالت جلستُها — وهو المشهد الذي جاءت
       البوّابة تمنعه. والسببُ `signed_out` معلومٌ لفاعله ولا يحتاج إعلاناً؛
       والإبطالُ الذي **لم** يطلبه صاحبه يُقرأ في مسارٍ آخر: فشلُ التجديد
       الصامت في `App.tsx`، وهو يُنزل البوّابة كذلك. */
    await waitFor(() => expect(screen.getByTestId("session-gate")).toBeTruthy());

    const config = JSON.parse(globalThis.localStorage.getItem("sb-api-config") ?? "{}") as {
      token?: string;
      refreshToken?: string;
    };
    expect(config.token).toBe("");
    expect(config.refreshToken ?? "").toBe("");
  });

  it("واعتمادُ التزويد الذي لا عائلة له يُقال برمزه لا بجواب «تمّ»", async () => {
    await mount({
      path: "/admin/session",
      transport: stub({
        routes: { "GET /api/v1/session": SESSION },
        refuse: {
          "POST /api/v1/access/sessions/revocation": {
            status: 409,
            code: "access.session_not_issued_here",
          },
        },
      }),
    });
    await click(await screen.findByTestId("admin-session-revoke-confirm-ack"));
    await click(await screen.findByTestId("admin-session-revoke-confirm-go"));
    await waitFor(() =>
      expect(screen.getByTestId("problem-code").textContent).toBe("access.session_not_issued_here")
    );
    expect(screen.getByTestId("admin-session-provisioning")).toBeTruthy();
    expect(screen.queryByTestId("admin-session-revoked")).toBeNull();
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٦ · الانقطاع يُري ما يتوقّف قبل التنفيذ
   ═══════════════════════════════════════════════════════════════════════ */
describe("الاشتراك", () => {
  async function mountSubscription(over: Record<string, unknown> = {}) {
    await mount({
      path: "/admin/subscription",
      transport: stub({
        routes: {
          "GET /api/v1/session": SESSION,
          ["GET /api/v1/tenants/" + TENANT + "/subscription"]: { ...SUBSCRIPTION, ...over },
          "GET /api/v1/plans": { plans: [PLAN_ESSENTIAL, PLAN_FULL] },
        },
      }),
    });
    await screen.findByTestId("admin-subscription-modules-table");
  }

  it("الخطّةُ الجديدة تُختار من المنشور الذي قرأه الخادم، ووحداتُها تُعرض قبل التنفيذ", async () => {
    await mountSubscription();
    const select = screen.getByTestId("admin-subscription-plan-input");
    if (!(select instanceof HTMLSelectElement)) throw new Error("ليس قائمةً منسدلة");
    await waitFor(() =>
      expect(screen.getAllByTestId("admin-subscription-plan-option").length).toBe(2)
    );
    const options = screen
      .getAllByTestId("admin-subscription-plan-option")
      .map((o) => (o as HTMLOptionElement).value);
    expect(options).toEqual(["ESSENTIAL", "FULL"]);
    /* قبل الاختيار: لا ادّعاء بما تغطّيه. */
    expect(screen.getByTestId("admin-subscription-plan-unchosen")).toBeTruthy();
    await choose(select, "FULL");
    expect(screen.getByTestId("admin-subscription-plan-modules").textContent).toBe(
      "AP · AR · CORE · INV · POS · REP"
    );
    expect(screen.queryByTestId("admin-subscription-plan-unchosen")).toBeNull();
  });

  it("جدولُ الوحدات هو الجواب على «ماذا يتوقّف؟» — وحالةُ كلٍّ كما وصلت", async () => {
    await mountSubscription();
    const rows = screen.getAllByTestId("admin-subscription-module");
    expect(rows.length).toBe(4);
    expect(rows.map((r) => r.getAttribute("data-module"))).toEqual(["AP", "AR", "POS", "REP"]);
    expect(rows.map((r) => r.getAttribute("data-state"))).toEqual([
      "Entitled",
      "Entitled",
      "NotEntitled",
      "Entitled",
    ]);
  });

  it("وأرضيّةُ ما لا يبلغ الدفتر لا تُخترَع: تُقال «لا يسمّيها العقد»", async () => {
    await mountSubscription();
    const floors = screen
      .getAllByTestId("admin-subscription-module-floor")
      .map((c) => c.textContent);
    /* AP وAR يبلغان الدفتر فأرضيّتهما قراءة؛ وPOS وREP لا يبلغانه. */
    expect(new Set(floors).size).toBe(2);
    expect(floors[0]).toBe(floors[1]);
    expect(floors[2]).toBe(floors[3]);
    expect(floors[0]).not.toBe(floors[2]);
  });

  it("ولوحُ الانقطاع يقول قبل الضغط أنّ القراءة لا تتوقّف، ويعدّ ما يكتب", async () => {
    await mountSubscription();
    const effects = await screen.findByTestId("admin-subscription-move-effects");
    const items = [...effects.querySelectorAll("li")].map((li) => li.textContent ?? "");
    expect(items.length).toBe(5);
    expect(screen.getByTestId("admin-subscription-lapse-effect-read")).toBeTruthy();
    /* ثلاثُ وحداتٍ مستحقّة، اثنتان منها تبلغان الدفتر — والعددان معروضان. */
    expect(items[1]).toMatch(/[0-9٠-٩]/u);
    expect(items[2]).toMatch(/[0-9٠-٩]/u);
  });

  it("والاشتراك المنقطع يُعرض له لوحُ استئنافٍ لا لوحُ انقطاع", async () => {
    await mountSubscription({ state: "Lapsed", renewsOn: null });
    const effects = await screen.findByTestId("admin-subscription-move-effects");
    expect([...effects.querySelectorAll("li")].length).toBe(3);
    expect(screen.queryByTestId("admin-subscription-lapse-effect-read")).toBeNull();
    expect(screen.getByTestId("admin-subscription-renews").textContent).not.toContain("2026");
  });

  it("وزرُّ التنفيذ مُقفَلٌ بلا سند — وسببُه مكتوبٌ أنه نقصُ مُدخَلٍ لا منعُ صلاحية", async () => {
    await mountSubscription();
    expect(button("admin-subscription-move-confirm-go").disabled).toBe(true);
    expect(screen.getByTestId("admin-subscription-move-confirm-blocked").textContent).toBeTruthy();
  });

  it("والمال يبقى نصّاً: لا Number ولا parseFloat في شيفرة هذا القسم", () => {
    for (const file of SCREEN_FILES) {
      const text = sourceOf(file);
      expect(/\bNumber\s*\(|parseFloat\s*\(|parseInt\s*\(/.test(text), file).toBe(false);
    }
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٧ · كل حقلٍ في صفّ يحمل وصفاً، والوعاء مُسجَّل
   ═══════════════════════════════════════════════════════════════════════ */
describe("استقامةُ الصفّ — الشرط البنيوي", () => {
  it("كل AdminField يحمل hint أو error (ADR-0078)", () => {
    let seen = 0;
    for (const file of SCREEN_FILES) {
      const text = sourceOf(file);
      for (const match of text.matchAll(/<AdminField\b([\s\S]*?)>/g)) {
        seen += 1;
        expect(/\bhint=|\berror=/.test(match[1] ?? ""), file + " — حقلٌ بلا وصف").toBe(true);
      }
    }
    /* حارس لافراغ: مسحٌ لا يقرأ حقلاً واحداً يمرّ دائماً. */
    expect(seen).toBeGreaterThanOrEqual(10);
  });

  it("وأوعيةُ الصفوف هي grid وحدها — وهي مُسجَّلة في styles/components.css", () => {
    const registry = readFileSync(path.resolve(SRC, "styles/components.css"), "utf8");
    const declared = new Set(
      [...registry.matchAll(/:is\(([^)]*)\)\s*>\s*:is\(\.field,\.rowctl\)/g)]
        .flatMap((m) => (m[1] ?? "").split(","))
        .map((s) => s.trim())
    );
    expect(declared.has(".grid"), "grid ليس في سجلّ الأوعية").toBe(true);

    let rows = 0;
    for (const file of SCREEN_FILES) {
      const text = sourceOf(file);
      for (const match of text.matchAll(/className="([^"]*\bgrid\b[^"]*)"/g)) {
        rows += 1;
        expect((match[1] ?? "").split(/\s+/)).toContain("grid");
      }
      /* ولا وعاءَ صفٍّ يُخترَع في هذا القسم. */
      expect(/className="[^"]*\badm-row\b/.test(text), file).toBe(false);
    }
    expect(rows).toBeGreaterThanOrEqual(5);
  });
});

/* ═══════════════════════════════════════════════════════════════════════
   ٨ · كتالوج الخطط — بياناتُ المنصّة لا شيفرتُها (ADR-0092)
   ═══════════════════════════════════════════════════════════════════════ */
describe("كتالوج الخطط", () => {
  it("مشغّلُ المنصّة يرى الكتالوج كلّه، والمسودّةُ موسومة، والوحداتُ المعروفة من الخطط نفسها", async () => {
    await mount({
      path: "/admin/plans",
      transport: stub({
        routes: {
          "GET /api/v1/platform/plans": { plans: [PLAN_ESSENTIAL, PLAN_DRAFT] },
          "GET /api/v1/plans": { plans: [PLAN_ESSENTIAL] },
        },
      }),
    });
    await screen.findByTestId("admin-plans-table");
    const rows = screen.getAllByTestId("admin-plans-row");
    expect(rows.map((r) => r.getAttribute("data-plan"))).toEqual(["ESSENTIAL", "TRIAL"]);
    expect(rows.map((r) => r.getAttribute("data-published"))).toEqual(["true", "false"]);
    /* الخانات اتّحادُ وحدات الخطط المقروءة — لا كتالوجٌ مكتوبٌ في الشاشة. */
    for (const module of ["AP", "AR", "CORE", "INV"]) {
      expect(screen.getByTestId("admin-plans-module-" + module)).toBeTruthy();
    }
    expect(screen.queryByTestId("admin-plans-module-POS")).toBeNull();
  });

  it("اعتمادُ منشأةٍ يُردّ باسمه ويبقى المنشورُ مقروءاً — والنموذج ظاهرٌ لأن المنع في الخادم", async () => {
    await mount({
      path: "/admin/plans",
      transport: stub({
        routes: { "GET /api/v1/plans": { plans: [PLAN_ESSENTIAL] } },
        refuse: {
          "GET /api/v1/platform/plans": { status: 403, code: "platform.operator_required" },
        },
      }),
    });
    const next = await screen.findByTestId("admin-plans-list-next");
    expect(next.getAttribute("data-code")).toBe("platform.operator_required");
    await screen.findByTestId("admin-plans-table");
    expect(
      screen.getAllByTestId("admin-plans-row").map((r) => r.getAttribute("data-plan"))
    ).toEqual(["ESSENTIAL"]);
    expect(screen.getByTestId("admin-plans-form")).toBeTruthy();
  });

  it("تعديلُ صفٍّ يملأ النموذج، والكتابةُ تُرسل الجسم كما كُتب إلى مسار الرمز وتقرأ الجواب", async () => {
    const sent: Recorded[] = [];
    const written = { ...PLAN_DRAFT, monthlyPrice: "1234.5000", published: true };
    await mount({
      path: "/admin/plans",
      transport: stub({
        routes: {
          "GET /api/v1/platform/plans": { plans: [PLAN_ESSENTIAL, PLAN_DRAFT] },
          "GET /api/v1/plans": { plans: [PLAN_ESSENTIAL] },
          "PUT /api/v1/platform/plans/TRIAL": written,
        },
        sent,
      }),
    });
    await screen.findByTestId("admin-plans-table");

    /* الزرّ مقفلٌ لأن المُدخَل ناقص — لا لأن الشاشة تمنع. */
    expect(screen.getByTestId("admin-plans-confirm-blocked").textContent).toBeTruthy();

    await click(screen.getByTestId("admin-plans-edit-TRIAL"));
    expect(input("admin-plans-code").value).toBe("TRIAL");
    expect(input("admin-plans-monthly").value).toBe("0.0000");
    expect(input("admin-plans-module-INV").checked).toBe(true);
    expect(input("admin-plans-module-AP").checked).toBe(false);

    await type(input("admin-plans-monthly"), "1234.5000");
    await click(input("admin-plans-publish"));
    await type(input("admin-plans-authority"), "PRICE-2026-01");
    await type(input("admin-plans-reason"), "تسعيرُ خطّة التجربة");
    expect(screen.queryByTestId("admin-plans-confirm-blocked")).toBeNull();

    await click(screen.getByTestId("admin-plans-confirm-ack"));
    await click(screen.getByTestId("admin-plans-confirm-go"));
    await waitFor(() =>
      expect(screen.getByTestId("admin-plans-written-code").textContent).toBe("TRIAL")
    );

    const put = sent.find((s) => s.method === "PUT");
    expect(put?.url).toBe("/api/v1/platform/plans/TRIAL");
    expect(put?.body).toMatchObject({
      nameAr: "تجربة",
      nameTranslations: [{ name: "en", value: "Trial" }],
      monthlyPrice: "1234.5000",
      perUserPrice: "0.0000",
      includedUsers: 1,
      modules: ["CORE", "INV"],
      published: true,
      authority: "PRICE-2026-01",
      reasonAr: "تسعيرُ خطّة التجربة",
    });
  });

  it("الرفضُ باسم علّته يُعرض كما ورد وفوقه الخطوةُ التالية", async () => {
    await mount({
      path: "/admin/plans",
      transport: stub({
        routes: {
          "GET /api/v1/platform/plans": { plans: [PLAN_ESSENTIAL] },
          "GET /api/v1/plans": { plans: [PLAN_ESSENTIAL] },
        },
        refuse: {
          "PUT /api/v1/platform/plans/NEWPLAN": { status: 422, code: "platform.plan_refused" },
        },
      }),
    });
    await screen.findByTestId("admin-plans-table");
    await type(input("admin-plans-code"), "NEWPLAN");
    await type(input("admin-plans-name-ar"), "جديدة");
    await type(input("admin-plans-name-en"), "New");
    await type(input("admin-plans-monthly"), "100.0000");
    await type(input("admin-plans-per-user"), "1.0000");
    await type(input("admin-plans-included"), "2");
    await type(input("admin-plans-other-modules"), "core, xyz");
    expect(screen.getByTestId("admin-plans-effect-modules").textContent).toBe("CORE · XYZ");
    await type(input("admin-plans-authority"), "PRICE-2026-02");
    await type(input("admin-plans-reason"), "سبب");
    await click(screen.getByTestId("admin-plans-confirm-ack"));
    await click(screen.getByTestId("admin-plans-confirm-go"));
    await waitFor(() =>
      expect(screen.getByTestId("problem-code").textContent).toBe("platform.plan_refused")
    );
    expect(screen.getByTestId("admin-plans-next").getAttribute("data-code")).toBe(
      "platform.plan_refused"
    );
  });
});
