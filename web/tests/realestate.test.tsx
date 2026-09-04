/* ═══════════════════════════════════════════════════════════════════════════
   قسم العقارات — الحرّاس التي تمنع انحرافه عن قواعده
   The real-estate section — the guards that keep it from drifting
   ───────────────────────────────────────────────────────────────────────────
   خمسةٌ تُفحص هنا، وكلّها مواضع **يكلّف انحرافها مالاً أو ثقة**:
     ١ · الرفض **يُرسَم ويبقى** — لا نخبٌ يختفي قبل أن يُقرأ.
     ٢ · حالة الفراغ **مصمَّمة** وتقول لماذا، لا جدولٌ فارغ بلا سبب.
     ٣ · المال **لا يمرّ برقم** في أي اتجاه: لا صعوداً إلى السلك ولا نزولاً
         إلى الشاشة. والقيمة المقيسة هنا هي التي يفقدها Number بعينها.
     ٤ · الاتجاه من اليمين إلى اليسار قائمٌ في الشجرة كلّها.
     ٥ · **العقدان المتداخلان يُعرَضان تعارضاً لا يُكدَّسان**: مسارٌ ثانٍ ووسمٌ
         صريح — ومن رسمهما فوق بعضهما رسم وحدةً مؤجَّرة مرّتين وكأنها سليمة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { StrictMode } from "react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS, SECTIONS, sectionOf } from "../src/app/shell/sections";
import { Money } from "../src/api/money";
import { encodeSchema } from "../src/api/transport";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import type { Transport } from "../src/api/transport";
import { PeriodBand, dayNumber, overlappingSpans, uncoveredGaps } from "../src/ui";
import { closedSet, isMoneyText, ownershipLabelKey } from "../src/screens/realestate/parts";

const COMPANY = "00000000-0000-4000-8000-00000000cafe";

/** ما يُطلب فعلاً من السلك — يُفحص بعد الرسم فلا يُصدَّق ادّعاء. */
interface Sent {
  method: string;
  url: string;
  body?: unknown;
}

/** مشكلة بصيغة RFC 9457 كما ترسلها الخلفية. */
function problem(status: number, code: string, ar: string, en: string) {
  return {
    type: "https://babel.sa/problems/" + code,
    title: en,
    titleAr: ar,
    status,
    detail: en,
    detailAr: ar,
    instance: "/api/v1/companies/" + COMPANY,
    code,
    traceId: "00-t-0",
    errors: [{ code, field: null, messageAr: ar, messageEn: en }],
  };
}

/** نقلٌ مزيّف: خريطة من «الطريقة والمسار» إلى الجواب، وسجلٌّ لما أُرسل. */
function fakeTransport(
  routes: readonly { match: RegExp; method?: string; status?: number; json: unknown }[],
  sent: Sent[]
): Transport {
  return async ({ method, url, body }) => {
    await Promise.resolve();
    sent.push({ method, url, body });
    if (url === "/health") {
      return {
        ok: true,
        status: 200,
        json: { apiVersion: "v1", calendar: "GregorianCalendar", culture: "ar-SA", status: "ok" },
        url,
      };
    }
    for (const route of routes) {
      if (route.match.test(url) && (!route.method || route.method === method)) {
        const status = route.status ?? 200;
        return { ok: status < 400, status, json: route.json, url };
      }
    }
    return { ok: false, status: 404, json: problem(404, "http.not_found", "غير موجود", "Not found"), url };
  };
}

function renderApp(path: string, transport: Transport, locale = "ar") {
  const router = createAppRouter({ memory: true, initialPath: path });
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <StrictMode>
      <LocaleProvider i18n={createI18n()} initial={locale}>
        <QueryClientProvider client={queryClient}>
          <ApiProvider transport={transport}>
            <RouterProvider router={router} />
          </ApiProvider>
        </QueryClientProvider>
      </LocaleProvider>
    </StrictMode>
  );
}

beforeEach(() => {
  globalThis.localStorage?.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: COMPANY, book: "MAIN", period: "" })
  );
});

afterEach(() => {
  globalThis.localStorage?.clear();
});

/* ═══════════════════════════════════ ١ · عقد الملاحة والمسارات ══════ */

describe("عقد الأقسام", () => {
  it("العقارات قسمٌ مبنيّ بمسارٍ يعمل، وشاشاته الأربع مسجَّلة", () => {
    const section = SECTIONS.find((s) => s.id === "realestate");
    expect(section).toBeDefined();
    expect(section?.built).toBe(true);
    expect(section?.path).toBe("/realestate");
    const paths = SCREENS.filter((s) => s.section === "realestate").map((s) => s.path);
    /* والترتيب ترتيبُ العمل (ADR-0080): العقارُ ووحداته ← طرفا العقد ← العقد
       وجدوله ← ما تأخّر وما قُبض. */
    expect(paths).toEqual([
      "/realestate",
      "/realestate/parties",
      "/realestate/lease",
      "/realestate/arrears",
    ]);
    /* لا شاشة عقارية تُنسَب إلى قسمٍ آخر — واللون يتبع القسم في الهيكل. */
    for (const path of paths) expect(sectionOf(path).id).toBe("realestate");
  });

  it("كل شاشة عقارية مسجَّلة لها مسارٌ يفتح فعلاً", async () => {
    for (const path of SCREENS.filter((s) => s.section === "realestate").map((s) => s.path)) {
      const view = renderApp(path, fakeTransport([], []));
      await waitFor(() => expect(document.querySelector("[data-testid^='realestate-']")).not.toBeNull());
      view.unmount();
    }
  });
});

/* ══════════════════════════════ ٢ · المجموعات المغلقة من العقد ══════ */

describe("المجموعات المغلقة", () => {
  it("تُقرأ من العقد المُولَّد لا من قائمةٍ مكتوبة بيد", () => {
    expect([...closedSet("PropertyRequest", "ownershipModel")].sort()).toEqual([
      "managed_for_others",
      "own_property",
    ]);
    expect([...closedSet("UnitRequest", "usage")].sort()).toEqual(["commercial", "residential"]);
    expect([...closedSet("UnitRequest", "vatTreatment")].sort()).toEqual(["exempt", "standard"]);
  });

  it("حقلٌ ليس مجموعةً مغلقة يرمي بدل أن يُعرَض فارغاً", () => {
    expect(() => closedSet("PropertyRequest", "code")).toThrow(TypeError);
  });

  it("لكل عضوٍ مفتاح لغةٍ صريح، ولا يُلصَق العضو ببادئة", () => {
    /* أعضاء العقد تحمل شرطةً سفلية، واصطلاح المفاتيح لا يقبلها. */
    expect(ownershipLabelKey("own_property")).toBe("realestate.ownership.own");
    expect(ownershipLabelKey("managed_for_others")).toBe("realestate.ownership.managed");
    for (const member of closedSet("PropertyRequest", "ownershipModel")) {
      expect(/^[a-zA-Z0-9.]+$/.test(ownershipLabelKey(member))).toBe(true);
    }
  });
});

/* ══════════════════════════════════════ ٣ · الرفض يُرسَم ويبقى ══════ */

describe("الرفض حالةٌ أولى مقيمة", () => {
  it("رفض «الوحدة مؤجَّرة» يُعرَض برمزه ورسالتيه، ولا يختفي مع الوقت", async () => {
    const sent: Sent[] = [];
    const transport = fakeTransport(
      [
        {
          match: /\/lease-registrations\/[^/]+$/,
          method: "GET",
          json: {
            id: "L1",
            ejarContractNumber: "EJR-2026-000001",
            propertyId: "P1",
            unitId: "U1",
            lesseeId: "T1",
            startsOn: "2026-01-01",
            endsOn: "2026-12-31",
            state: "DRAFT",
            totalRent: "120000.0000",
          },
        },
        {
          match: /\/billing-approval$/,
          method: "POST",
          status: 409,
          json: problem(
            409,
            "realestate.lease_term_overlaps",
            "مدّة عقد إيجار «EJR-2026-000001» تتداخل مع مدّة قيدٍ آخر معتمَدٍ للفوترة على الوحدة نفسها.",
            "The term of Ejar contract 'EJR-2026-000001' overlaps another registration approved for billing on the same unit."
          ),
        },
      ],
      sent
    );
    const view = renderApp("/realestate/lease", transport);

    const input = await screen.findByTestId("re-lease-id");
    input.setAttribute("value", "L1");
    const { fireEvent } = await import("@testing-library/react");
    fireEvent.change(input, { target: { value: "L1" } });
    fireEvent.click(screen.getByTestId("re-lease-open-go"));

    await screen.findByTestId("re-lease-approval");
    fireEvent.click(screen.getByTestId("re-lease-approve"));

    const panel = await screen.findByTestId("re-lease-approval-refusal");
    expect(within(panel).getByTestId("problem-code").textContent).toBe(
      "realestate.lease_term_overlaps"
    );
    expect(panel.textContent).toContain("تتداخل مع مدّة قيدٍ آخر معتمَدٍ للفوترة");
    expect(panel.textContent).toContain("overlaps another registration approved for billing");

    /* والخطوة التالية تُقال — فاللوحة لا تكون شكوى. */
    expect(screen.getByTestId("realestate-next-step").textContent).toContain("الخطوة التالية");

    /* واللوحة الخاصّة بالتداخل تقول **من أين جاء الحكم**. */
    const overlap = screen.getByTestId("re-lease-overlap");
    expect(overlap.textContent).toContain("قاعدة البيانات");

    /* تبقى: لا مؤقّت يرفعها، ولا إعادة رسمٍ تُسقطها. */
    await new Promise((resolve) => setTimeout(resolve, 1400));
    expect(screen.getByTestId("re-lease-approval-refusal")).toBeTruthy();
    expect(screen.getByTestId("re-lease-overlap")).toBeTruthy();
    view.unmount();
  });

  it("رفض التسجيل يُسمّى قبل الإرسال: نموذجٌ مُدار بلا مالك", async () => {
    const { fireEvent } = await import("@testing-library/react");
    const view = renderApp("/realestate", fakeTransport([], []));
    const model = await screen.findByTestId("re-prop-model");
    fireEvent.change(model, { target: { value: "managed_for_others" } });
    const said = screen.getByTestId("re-prop-needs-owner");
    expect(said.getAttribute("role")).toBe("alert");
    expect(said.textContent).toContain("مالكاً مسجَّلاً");
    view.unmount();
  });
});

/* ══════════════════════════════════════ ٤ · حالات الفراغ مصمَّمة ════ */

describe("حالات الفراغ", () => {
  it("تقرير المتأخّرات بلا مستأجرين يقول لماذا فرغ، لا جدولاً فارغاً", async () => {
    const { fireEvent } = await import("@testing-library/react");
    const zero = {
      notDue: "0.0000",
      days1To30: "0.0000",
      days31To60: "0.0000",
      days61To90: "0.0000",
      over90: "0.0000",
      total: "0.0000",
    };
    const view = renderApp(
      "/realestate/arrears",
      fakeTransport(
        [
          {
            match: /tenant-arrears-aging/,
            json: {
              asOf: "2026-08-31",
              parties: [],
              totals: zero,
              controlTotal: "0.0000",
              divergence: "0.0000",
              isReconciled: true,
            },
          },
        ],
        []
      )
    );

    /* قبل القراءة: حالةُ «لم يُقرأ بعد» — لا رقمَ قديم تحت تاريخٍ جديد. */
    expect(await screen.findByTestId("re-arrears-idle")).toBeTruthy();

    fireEvent.click(screen.getByTestId("re-arrears-load"));
    const empty = await screen.findByTestId("re-arrears-empty");
    expect(empty.textContent).toContain("لا متأخّرات في هذا التاريخ");
    expect(empty.textContent).toContain("خبراً حسناً");
    view.unmount();
  });

  it("سجلّ الجلسة الفارغ يقول إنه ذاكرة تبويبة لا سجلّ منشأة", async () => {
    const view = renderApp("/realestate", fakeTransport([], []));
    const empty = await screen.findByTestId("re-session-empty");
    expect(empty.textContent).toContain("لم يُسجَّل شيء بعد");
    /* وما لا ينشره العقد مُعلَنٌ بأبوابه، لا مسكوتٌ عنه. */
    const pending = screen.getByTestId("realestate-no-list");
    expect(pending.textContent).toContain("GET /api/v1/companies/{companyId}/properties");
    view.unmount();
  });
});

/* ═════════════════════════════════ ٥ · المال لا يمرّ برقم إطلاقاً ═══ */

describe("المال نصّ في الاتجاهين", () => {
  it("المبلغ ينزل من السلك بلا فقدِ خانة، ويبقى أصله في السمة", async () => {
    const { fireEvent } = await import("@testing-library/react");
    /* القيمة التي يفقدها Number بعينها — مقيسة في هذا المستودع. */
    const exact = "1000000000000.4013";
    const bands = {
      notDue: "0.0000",
      days1To30: exact,
      days31To60: "0.0000",
      days61To90: "0.0000",
      over90: "0.0000",
      total: exact,
    };
    const view = renderApp(
      "/realestate/arrears",
      fakeTransport(
        [
          {
            match: /tenant-arrears-aging/,
            json: {
              asOf: "2026-08-31",
              parties: [
                {
                  partyId: "T1",
                  code: "TEN-1",
                  nameAr: "شركة النور",
                  nameTranslations: [{ name: "en", value: "Al-Noor Co." }],
                  bands,
                },
              ],
              totals: bands,
              controlTotal: exact,
              divergence: "0.0000",
              isReconciled: true,
            },
          },
        ],
        []
      )
    );
    fireEvent.click(await screen.findByTestId("re-arrears-load"));
    const row = await screen.findByTestId("re-arrears-row");
    const amounts = row.querySelectorAll("span.amt");
    const titles = [...amounts].map((a) => a.getAttribute("title"));
    expect(titles).toContain(exact);
    /* والمعروض تقريبٌ مُعلَن لا قيمةٌ بديلة. */
    const shown = [...amounts].find((a) => a.getAttribute("title") === exact);
    expect(shown?.textContent).toBe("1,000,000,000,000.40");
    view.unmount();
  });

  it("المبلغ يصعد إلى السلك نصّاً بايتاً ببايت، ولا يقبل المُرمِّز رقماً", () => {
    const exact = "1000000000000.4013";
    const encoded = encodeSchema(SCHEMAS, "LeaseRegistrationRequest", {
      ejarContractNumber: "EJR-2026-000001",
      unitId: "U1",
      lesseeId: "T1",
      startsOn: "2026-01-01",
      endsOn: "2026-12-31",
      totalRent: Money.wire(exact),
      instalments: [
        {
          periodFrom: "2026-01-01",
          periodTo: "2026-01-31",
          dueOn: "2026-01-05",
          amount: Money.wire(exact),
        },
      ],
    }) as Record<string, unknown>;
    expect(encoded.totalRent).toBe(exact);
    expect(JSON.stringify(encoded)).toContain('"' + exact + '"');
    /* ولا طريق يمرّ منه رقم: المُرمِّز يرمي، والنوع يرمي قبله. والرمز الرقمي
       هنا مبنيٌّ بـJSON.parse لا مكتوباً حرفيّاً — لأن كتابته حرفيّاً تفقد
       الخانة قبل أن يبدأ الاختبار، وهو العطل نفسه الذي يقيسه. */
    const asNumber = JSON.parse(exact) as unknown;
    expect(typeof asNumber).toBe("number");
    expect(() => encodeSchema(SCHEMAS, "LeaseRegistrationRequest", { totalRent: asNumber })).toThrow(TypeError);
    expect(() => Money.wire(asNumber as string)).toThrow(TypeError);
  });

  it("نحو المال يُفحص بالنمط المنشور: الفراغ ليس صفراً وخمس خانات مرفوضة", () => {
    expect(isMoneyText("")).toBe(false);
    expect(isMoneyText("0")).toBe(true);
    expect(isMoneyText("1200.4013")).toBe(true);
    expect(isMoneyText("1200.40135")).toBe(false);
    expect(isMoneyText("١٢٠٠")).toBe(false);
    expect(isMoneyText("+12")).toBe(false);
    expect(isMoneyText("1e3")).toBe(false);
  });

  it("لا مبلغ في هذا القسم يمرّ بـNumber أو parseFloat في المصدر", async () => {
    const { readFileSync, readdirSync } = await import("node:fs");
    const dir = "src/screens/realestate";
    const files = readdirSync(dir).filter((f) => f.endsWith(".tsx"));
    expect(files.length).toBeGreaterThanOrEqual(3);
    for (const file of files) {
      const text = readFileSync(dir + "/" + file, "utf8");
      expect(text, file).not.toMatch(/\bparseFloat\(|\bparseInt\(|\bNumber\(/);
    }
  });
});

/* ══════════════════════════════════════════ ٦ · الاتجاه والوصولية ═══ */

describe("الاتجاه والوصولية", () => {
  it("الجذر عربيٌّ من اليمين إلى اليسار، والخانات الرقمية معزولة", async () => {
    const { fireEvent } = await import("@testing-library/react");
    const view = renderApp(
      "/realestate/arrears",
      fakeTransport(
        [
          {
            match: /tenant-arrears-aging/,
            json: {
              asOf: "2026-08-31",
              parties: [],
              totals: {
                notDue: "1.0000",
                days1To30: "0.0000",
                days31To60: "0.0000",
                days61To90: "0.0000",
                over90: "0.0000",
                total: "1.0000",
              },
              controlTotal: "1.0000",
              divergence: "0.0000",
              isReconciled: true,
            },
          },
        ],
        []
      )
    );
    await screen.findByTestId("realestate-arrears");
    expect(document.documentElement.getAttribute("dir")).toBe("rtl");
    expect(document.documentElement.getAttribute("lang")).toBe("ar");

    fireEvent.click(screen.getByTestId("re-arrears-load"));
    const control = await screen.findByTestId("re-arrears-control");
    const amount = control.querySelector("span.amt");
    expect(amount?.getAttribute("dir")).toBe("ltr");
    view.unmount();
  });

  it("الشريط يضع المقاطع بخصائص منطقية فينقلب مع اللغة بلا سطرٍ ثانٍ", () => {
    const { container } = render(
      <LocaleProvider i18n={createI18n()} initial="ar">
        <PeriodBand
          from="2026-01-01"
          to="2026-12-31"
          spans={[
            { key: "a", from: "2026-01-01", to: "2026-03-31", label: "1", title: "q1" },
            { key: "b", from: "2026-07-01", to: "2026-09-30", label: "3", title: "q3" },
          ]}
          labels={{ caption: "مدّة", gap: "فجوة" }}
          testId="band"
        />
      </LocaleProvider>
    );
    const spans = container.querySelectorAll("[data-testid='band-span']");
    expect(spans).toHaveLength(2);
    for (const span of spans) {
      const style = (span as HTMLElement).style;
      expect(style.insetInlineStart).not.toBe("");
      expect(style.getPropertyValue("left")).toBe("");
      expect(style.inlineSize).not.toBe("");
    }
  });
});

/* ══════════════════ ٧ · التداخل يُعرَض تعارضاً لا يُكدَّس ═══════════ */

describe("مدّتان على وحدةٍ واحدة", () => {
  it("المدى مُغلَق الطرفين: يومٌ مشترك تداخلٌ لا ملامسة", () => {
    const touching = [
      { key: "a", from: "2026-01-01", to: "2026-01-31", label: "1", title: "a" },
      { key: "b", from: "2026-02-01", to: "2026-02-28", label: "2", title: "b" },
    ];
    expect(overlappingSpans(touching)).toEqual([]);
    const sharing = [
      { key: "a", from: "2026-01-01", to: "2026-02-01", label: "1", title: "a" },
      { key: "b", from: "2026-02-01", to: "2026-02-28", label: "2", title: "b" },
    ];
    expect([...overlappingSpans(sharing)].sort()).toEqual(["a", "b"]);
  });

  it("المتداخلان يُرفعان إلى مسارين ويُوسَمان تعارضاً — ولا يُرسمان فوق بعضهما", () => {
    const { container } = render(
      <LocaleProvider i18n={createI18n()} initial="ar">
        <PeriodBand
          from="2026-01-01"
          to="2026-12-31"
          spans={[
            { key: "a", from: "2026-01-01", to: "2026-06-30", label: "1", title: "a" },
            { key: "b", from: "2026-05-01", to: "2026-10-31", label: "2", title: "b" },
          ]}
          labels={{ caption: "مدّة", gap: "فجوة" }}
          testId="band"
        />
      </LocaleProvider>
    );
    const conflicts = container.querySelectorAll("[data-testid='band-conflict']");
    expect(conflicts).toHaveLength(2);
    /* مساران لا مسار: الكدس يجعل الاثنين يبدوان واحداً سليماً. */
    const lanes = container.querySelectorAll(".band__lane");
    expect(lanes).toHaveLength(2);
    /* ولا مقطع «عادي» بقي غير موسوم. */
    expect(container.querySelectorAll("[data-testid='band-span']")).toHaveLength(0);
  });

  it("الفجوة تُحسب على أيام المدّة لا تُخمَّن", () => {
    const gaps = uncoveredGaps("2026-01-01", "2026-12-31", [
      { key: "a", from: "2026-01-01", to: "2026-03-31", label: "1", title: "a" },
      { key: "b", from: "2026-07-01", to: "2026-12-31", label: "2", title: "b" },
    ]);
    expect(gaps).toEqual([{ from: "2026-04-01", to: "2026-06-30" }]);
    expect(
      uncoveredGaps("2026-01-01", "2026-01-31", [
        { key: "a", from: "2026-01-01", to: "2026-01-31", label: "1", title: "a" },
      ])
    ).toEqual([]);
  });

  it("تاريخٌ خارج الصيغة المنشورة يرمي بدل أن يرسم شريطاً كاذباً", () => {
    expect(() => dayNumber("2026-13-01")).toThrow(TypeError);
    expect(() => dayNumber("١٤٤٧-٠١-٠١")).toThrow(TypeError);
    expect(dayNumber("1970-01-02")).toBe(1);
  });
});

/* ════════════════════════════ ٨ · لا رمز حسابٍ يعبر هذه الشاشات ════ */

describe("الوحدات لا تسمّي حساباً", () => {
  it("لا رمز حسابٍ ولا اسم حسابٍ مكتوبٌ في مصدر القسم", async () => {
    const { readFileSync, readdirSync } = await import("node:fs");
    const dir = "src/screens/realestate";
    for (const file of readdirSync(dir).filter((f) => f.endsWith(".tsx"))) {
      const text = readFileSync(dir + "/" + file, "utf8");
      /* رمز حسابٍ في هذا المستودع سلسلةُ أرقامٍ طويلة داخل نصّ. */
      expect(text, file).not.toMatch(/["'][0-9]{6,}["']/);
      expect(text, file).not.toMatch(/accountCode|account_code/);
    }
  });

  it("حدث الترحيل يصل من الخادم ويُعرَض كما هو", async () => {
    const { fireEvent } = await import("@testing-library/react");
    const view = renderApp(
      "/realestate/arrears",
      fakeTransport(
        [
          {
            match: /tenant-receipts$/,
            method: "POST",
            json: {
              id: "R1",
              number: "RCV-1",
              received: "1500.0000",
              state: "DRAFT",
              entryId: null,
              allocationEntryId: null,
              isAllocated: false,
              alreadyPosted: false,
              eventCode: "realestate.collection.unallocated",
            },
          },
        ],
        []
      )
    );
    await screen.findByTestId("re-receipt");
    fireEvent.change(screen.getByTestId("re-receipt-no"), { target: { value: "RCV-1" } });
    fireEvent.change(screen.getByTestId("re-receipt-amount"), { target: { value: "1500.0000" } });
    fireEvent.change(screen.getByTestId("re-receipt-treasury"), { target: { value: "CASH-01" } });
    fireEvent.click(screen.getByTestId("re-receipt-draft"));
    const event = await screen.findByTestId("re-receipt-event");
    expect(event.textContent).toBe("realestate.collection.unallocated");
    view.unmount();
  });
});
