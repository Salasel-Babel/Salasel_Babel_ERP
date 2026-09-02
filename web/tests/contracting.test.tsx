/* ═══════════════════════════════════════════════════════════════════════════
   المقاولات — ما يجب أن يمسكه اختبارٌ لا عين
   Contracting — what a test must catch, not an eye
   ───────────────────────────────────────────────────────────────────────────
   خمسة أعطالٍ لا تُرى بالنظر، وكلٌّ منها يُنتج شاشةً تبدو سليمة:

   ١ · **رفضٌ يزول.** لوحة البنود المعلَّقة حالةٌ أولى دائمة؛ ولو صارت تنبيهاً
       يختفي بعد مدّة الحركة لبقيت الشاشة تُقرأ «كل شيء على ما يرام».
   ٢ · **فراغٌ بلا سبب.** سجلّ المحتجزات فارغٌ **بحقّ** اليوم، وفراغٌ صامت
       يُقرأ عطلاً في القراءة فيُبحث عن العطل في المكان الخطأ.
   ٣ · **رقمٌ مرّ بعائم.** ‏`1000000000000.400013` تصير `…400000` إن مرّت على
       ‏`Number` — خانةٌ لا تُرى في عمودٍ من خمسمئة صفّ.
   ٤ · **تراكميٌّ خُلط بفترة.** عمودٌ واحد اسمه «الكمّية» بدل عمودين، أو فرقٌ
       يُحسب في المتصفّح، يُنتج إيراداً مضاعفاً أو ناقصاً بلا رسالة.
   ٥ · **إرسالٌ ثانٍ يُقرأ عملاً جديداً.** ‏`alreadyPosted` إن لم يُقَل صراحةً
       جعل المستخدم يقرأ ترحيلاً ثانياً لم يقع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import type { ReactNode } from "react";
import { afterEach, beforeEach, describe, expect, it } from "vitest";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import { createAppRouter } from "../src/app/router";
import { SCREENS, SECTIONS } from "../src/app/shell/sections";
import { decodeSchema, type RawResponse, type Transport } from "../src/api/transport";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import type { Certificate, PendingPolicyItem, ProjectsDocument } from "../src/api/generated/types";
import { QuantityValue, RateValue } from "../src/ui";
import { CertificateLines } from "../src/screens/contracting/CertificateScreen";
import {
  DocumentReceipt,
  PendingPolicyPanel,
} from "../src/screens/contracting/shared";
import { resetContractingSelection, selectContracting } from "../src/screens/contracting/selection";

/* ═══════════════════════════════════════════════════ أدوات الاختبار */

const COMPANY = "11111111-1111-1111-1111-111111111111";

/** البنود الأربعة كما ترسلها الوحدة — بأسمائها ورموزها الثابتة. */
const PENDING: PendingPolicyItem[] = [
  {
    code: "retention_base_and_advance_recovery",
    titleAr: "وعاء نسبة المحتجز وقاعدة استرداد الدفعة المقدمة",
    titleEn: "The base the retention rate applies to and the advance recovery rule",
    sourceRef: "posting-matrix.md §5.1",
  },
  {
    code: "tax_classification_level",
    titleAr: "مستوى التصنيف الضريبي",
    titleEn: "The tax classification level",
    sourceRef: "posting-matrix.md §5.1",
  },
  {
    code: "rounding_policy",
    titleAr: "موضع التقريب",
    titleEn: "Where rounding falls",
    sourceRef: "Money enforces a scale of four",
  },
  {
    code: "retention_control_effect",
    titleAr: "ظهور المحتجز المدين في مطابقة العميل",
    titleEn: "Whether debit retention appears in the customer reconciliation",
    sourceRef: "accounts.csv vs subledger-types.csv",
  },
];

/** مستخلصٌ بسطرين: عملٌ تراكميّ فوق سابقه، وغرامة. */
const CERTIFICATE_JSON = {
  id: "c1",
  number: "IPC-0002",
  ownerId: "k1",
  sequenceNo: 2,
  periodFrom: "2026-05-01",
  periodTo: "2026-05-31",
  state: "DRAFT",
  retentionRate: "0.10",
  entryId: null,
  alreadyPosted: false,
  pendingPolicy: PENDING,
  lines: [
    {
      id: "l1",
      lineNo: 1,
      lineKind: "WORK",
      itemId: "b1",
      itemCode: "CIV-010",
      descriptionAr: "خرسانة القواعد",
      cumulativeQuantity: { magnitude: "120.000000", unit: "M3" },
      previousQuantity: { magnitude: "45.000000", unit: "M3" },
      amount: "0.0000",
    },
    {
      id: "l2",
      lineNo: 2,
      lineKind: "PENALTY",
      itemId: null,
      itemCode: "",
      descriptionAr: "غرامة تأخير",
      cumulativeQuantity: { magnitude: "0", unit: "" },
      previousQuantity: { magnitude: "0", unit: "" },
      amount: "5000.0000",
    },
  ],
};

function certificate(): Certificate {
  return decodeSchema(SCHEMAS, "Certificate", CERTIFICATE_JSON) as Certificate;
}

function projectsDocument(alreadyPosted: boolean): ProjectsDocument {
  return decodeSchema(SCHEMAS, "ProjectsDocument", {
    id: "d1",
    number: "ADV-0001",
    state: "POSTED",
    amount: "250000.0000",
    entryId: "e-9",
    alreadyPosted,
  }) as ProjectsDocument;
}

/** غلافٌ خفيف: لغةٌ وحدها، لمن لا يحتاج موجّهاً ولا نقلاً. */
function Wrap(props: { children: ReactNode; locale?: string }): ReactNode {
  return (
    <LocaleProvider i18n={createI18n()} initial={props.locale ?? "ar"}>
      {props.children}
    </LocaleProvider>
  );
}

/** نقلٌ يجيب من خريطة مسارات — ولا شبكة ولا خادم. */
function transportOf(routes: Readonly<Record<string, unknown>>): Transport {
  return ({ url }) => {
    const path = url.split("?")[0] as string;
    const body = routes[path];
    const response: RawResponse = body
      ? { ok: true, status: 200, json: body, url }
      : {
          ok: false,
          status: 404,
          json: {
            type: "about:blank",
            title: "Not found",
            titleAr: "غير موجود",
            detail: "no fixture for " + path,
            detailAr: "لا بيانات لهذا المسار",
            status: 404,
            code: "http.not_found",
            instance: path,
            traceId: "t-0",
            errors: [],
          },
          url,
        };
    return Promise.resolve(response);
  };
}

/** يركّب التطبيق كلّه على مسارٍ بعينه — الموجّه في الذاكرة، ولا متصفّح. */
async function mountApp(path: string, routes: Readonly<Record<string, unknown>>): Promise<void> {
  const router = createAppRouter({ memory: true, initialPath: path });
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(
    <LocaleProvider i18n={createI18n()} initial="ar">
      <QueryClientProvider client={client}>
        <ApiProvider transport={transportOf(routes)}>
          <RouterProvider router={router} />
        </ApiProvider>
      </QueryClientProvider>
    </LocaleProvider>
  );
  await act(async () => {
    await router.load();
  });
}

const HEALTH = { apiVersion: "v1", calendar: "GregorianCalendar", culture: "en-US", status: "ok" };

beforeEach(() => {
  resetContractingSelection();
  globalThis.localStorage?.setItem(
    "sb-api-config",
    JSON.stringify({ baseUrl: "", token: "t", companyId: COMPANY, book: "MAIN", period: "" })
  );
});

afterEach(() => {
  cleanup();
  globalThis.localStorage?.clear();
});

/* ═════════════════════ ١ · الرفض حالةٌ أولى، ولا يزول ═════════════════ */

describe("لوحة البنود المعلَّقة", () => {
  it("تُسمّي كل بندٍ برمزه وعنوانيه وموضع سؤاله، وتعطي الخطوة التالية", () => {
    render(
      <Wrap>
        <PendingPolicyPanel items={PENDING} subject="العقد C-1" />
      </Wrap>
    );
    const panel = screen.getByTestId("pending-policy");
    expect(panel.getAttribute("role")).toBe("alert");
    for (const item of PENDING) {
      expect(panel.textContent).toContain(item.code);
      expect(panel.textContent).toContain(item.titleAr);
      expect(panel.textContent).toContain(item.titleEn);
      expect(panel.textContent).toContain(item.sourceRef);
    }
    /* رفضٌ بلا خطوةٍ تالية شكوى: */
    expect(panel.textContent).toContain("الخطوة التالية");
    /* والبند المُسمّى يظهر كما هو، لا مجموعاً في جملة «ينقص إعداد»: */
    expect(panel.querySelectorAll("li")).toHaveLength(PENDING.length);
  });

  it("تبقى ظاهرةً بعد انقضاء مدّة الحركة — حالةٌ أولى لا تنبيهٌ يختفي", async () => {
    render(
      <Wrap>
        <PendingPolicyPanel items={PENDING} subject="العقد C-1" />
      </Wrap>
    );
    expect(screen.getByTestId("pending-policy")).toBeTruthy();
    /* مدّة `refuse` ٣٤٠ ملّي، ومدّة `dwell` ١١٠٠ — والانتظار يتجاوزهما معاً. */
    await act(async () => {
      await new Promise((resolve) => setTimeout(resolve, 1400));
    });
    const still = screen.getByTestId("pending-policy");
    expect(still).toBeTruthy();
    expect(still.textContent).toContain(PENDING[0]?.code as string);
  });

  it("لا تُعرض حين لا بند معلَّق — ولا لوحَ رفضٍ فارغ", () => {
    const { container } = render(
      <Wrap>
        <PendingPolicyPanel items={[]} subject="العقد C-1" />
      </Wrap>
    );
    expect(container.querySelector('[data-testid="pending-policy"]')).toBeNull();
  });
});

/* ═════════════════ ٢ · المال والكمّية والنسبة نصوصٌ لا أعداد ═══════════ */

describe("المال والكمّية والنسبة لا تمرّ بعائم", () => {
  it("الكمّية تحتفظ بخاناتها الستّ — والعائم كان سيُسقط الأربع الأخيرة", () => {
    /* 1000000000000.400013 →(Number)→ 1000000000000.4 : خانةٌ تختفي بصمت. */
    const wire = "1000000000000.400013";
    render(
      <Wrap>
        <QuantityValue magnitude={wire} unit="M3" testId="q" />
      </Wrap>
    );
    const shown = screen.getByTestId("q");
    expect(shown.textContent).toContain("1,000,000,000,000.400013");
    /* والوحدة معه دائماً: «عشرة» ليست معلومة. */
    expect(shown.textContent).toContain("M3");
    /* وما لو مرّ بعائم: */
    expect(String(Number(wire))).toBe("1000000000000.4");
  });

  it("النسبة كسرٌ عشري بثمان خانات، بلا علامة مئوية وبلا ضربٍ في مئة", () => {
    render(
      <Wrap>
        <RateValue rate="0.12345678" testId="r" />
      </Wrap>
    );
    const shown = screen.getByTestId("r");
    expect(shown.textContent).toContain("0.12345678");
    expect(shown.textContent).not.toContain("%");
    /* ولا «12.345678» ولا «10» مكان «0.10»: النصّ الأصلي محفوظ في السمة. */
    expect(shown.getAttribute("data-rate")).toBe("0.12345678");
  });

  it("مبلغ السطر يصل Money ويحمل نصّه الأصلي في السمة", () => {
    const view = certificate();
    render(
      <Wrap>
        <CertificateLines certificate={view} />
      </Wrap>
    );
    const amounts = document.querySelectorAll("span.amt");
    const titles = [...amounts].map((node) => node.getAttribute("title"));
    expect(titles).toContain("5000.0000");
  });
});

/* ═══════════════ ٣ · التراكمي والسابق لا يُخلطان ولا يُطرحان ═════════ */

describe("سطور المستخلص", () => {
  it("تعرض عمودين مُسمّيين: التراكمي المقيس والسابق من آخر مُرحَّل", () => {
    render(
      <Wrap>
        <CertificateLines certificate={certificate()} />
      </Wrap>
    );
    const table = screen.getByTestId("certificate-lines");
    const headers = [...table.querySelectorAll("th")].map((th) => th.textContent ?? "");
    expect(headers.some((h) => h.includes("التراكمية"))).toBe(true);
    expect(headers.some((h) => h.includes("السابقة"))).toBe(true);
    /* ورأسُ العمود يقول **من أين** جاء الرقم، لا اسمه فقط: */
    expect(headers.some((h) => h.includes("آخر مستخلصٍ مُرحَّل"))).toBe(true);

    const cumulative = screen.getAllByTestId("line-cumulative")[0] as HTMLElement;
    const previous = screen.getAllByTestId("line-previous")[0] as HTMLElement;
    expect(cumulative.textContent).toContain("120.000000");
    expect(previous.textContent).toContain("45.000000");
  });

  it("لا تحسب فرق الفترة: الطرح لا يقع في المتصفّح", () => {
    render(
      <Wrap>
        <CertificateLines certificate={certificate()} />
      </Wrap>
    );
    const text = screen.getByTestId("certificate-lines").textContent ?? "";
    /* 120 − 45 = 75 — والخادم لا ينشر قيمة الفترة، فلا يخترعها المتصفّح.
       والرقمان المختاران لا يحتوي أيٌّ منهما فرقَهما نصّاً، فالفحص قاطع. */
    expect(text).not.toContain("75.000000");
    expect(text).not.toContain("75.00");
  });

  it("سطر الغرامة يُميَّز بصنفه، ولا كمّية له", () => {
    render(
      <Wrap>
        <CertificateLines certificate={certificate()} />
      </Wrap>
    );
    const rows = screen.getAllByTestId("certificate-line");
    expect(rows[1]?.getAttribute("data-kind")).toBe("PENALTY");
    expect(rows[1]?.querySelector('[data-testid="line-cumulative"]')?.textContent).not.toContain("0.000000");
  });
});

/* ═════════════════ ٤ · إعادة الترحيل تُقال صدقاً ═══════════════════════ */

describe("إيصال الترحيل", () => {
  it("يفرّق بين ترحيلٍ أول وإرسالٍ ثانٍ بالهوية نفسها", () => {
    const first = render(
      <Wrap>
        <DocumentReceipt document={projectsDocument(false)} />
      </Wrap>
    );
    const receipt = screen.getByTestId("contracting-receipt");
    expect(receipt.getAttribute("data-already-posted")).toBe("false");
    expect(receipt.textContent).toContain("وقع الترحيل الآن");
    first.unmount();

    render(
      <Wrap>
        <DocumentReceipt document={projectsDocument(true)} />
      </Wrap>
    );
    const again = screen.getByTestId("contracting-receipt");
    expect(again.getAttribute("data-already-posted")).toBe("true");
    expect(again.textContent).toContain("لم يقع عملٌ جديد");
    /* ومعرّف القيد هو **الأول** لا قيدٌ ثانٍ: */
    expect(screen.getByTestId("receipt-entry").textContent).toBe("e-9");
  });
});

/* ═══════════════════ ٥ · حالات الفراغ مصمَّمة لا مفترَضة ════════════════ */

describe("سجلّ المحتجزات", () => {
  const routes = {
    "/health": HEALTH,
    ["/api/v1/companies/" + COMPANY + "/retention-register"]: {
      asOf: "2026-05-31",
      rows: [],
      receivableTotal: "0.0000",
      payableTotal: "0.0000",
    },
    ["/api/v1/companies/" + COMPANY + "/subcontractor-statement"]: {
      asOf: "2026-05-31",
      rows: [],
      subledgerTotal: "0.0000",
      controlTotal: "0.0000",
      divergence: "0.0000",
      isReconciled: true,
    },
  };

  it("يعرض فراغاً يقول لماذا هو فارغ — لا «لا نتائج»", async () => {
    await mountApp("/contracting/retention", routes);
    const empty = await screen.findByTestId("retention-empty");
    expect(empty.textContent).toContain("تُشتقّ من المستخلصات المُرحَّلة وحدها");
    expect(empty.textContent).toContain("بنودٍ معلَّقة");
  });

  it("يقرأ المجاميع مبالغَ لا أعداداً، ويحكم على المطابقة بالصفر بالضبط", async () => {
    await mountApp("/contracting/retention", routes);
    await waitFor(() => {
      expect(screen.getByTestId("statement-verdict").textContent).toContain("مطابِق");
    });
    const divergence = screen.getByTestId("statement-divergence");
    expect(divergence.querySelector("span.amt")?.getAttribute("title")).toBe("0.0000");
  });
});

/* ═══════════════════════ ٦ · الاتجاه والملاحة والعقد ═══════════════════ */

describe("القسم في هيكل التطبيق", () => {
  it("قسم المقاولات مبنيٌّ ومساره مسجَّل، وشاشاته السبع في الفهرس بترتيب العمل", () => {
    const section = SECTIONS.find((s) => s.id === "contracting");
    expect(section?.built).toBe(true);
    expect(section?.path).toBe("/contracting");
    const paths = SCREENS.filter((s) => s.section === "contracting").map((s) => s.path);
    /* والترتيب ترتيبُ العمل لا ترتيبُ الحروف (ADR-0078): المشروع وعقده ← ما
       يغيّر نطاقه ← ما يُوثَّق عليه ← المستخلص ← الباطن ← دفعته ← المحتجزات. */
    expect(paths).toEqual([
      "/contracting",
      "/contracting/change-orders",
      "/contracting/guarantees",
      "/contracting/certificate",
      "/contracting/subcontracting",
      "/contracting/advances",
      "/contracting/retention",
    ]);
  });

  it("الاتجاه من اليمين إلى اليسار، والخانة الرقمية معزولةٌ إلى اليسار داخله", async () => {
    await mountApp("/contracting/retention", {
      "/health": HEALTH,
      ["/api/v1/companies/" + COMPANY + "/retention-register"]: {
        asOf: "2026-05-31",
        rows: [],
        receivableTotal: "0.0000",
        payableTotal: "0.0000",
      },
      ["/api/v1/companies/" + COMPANY + "/subcontractor-statement"]: {
        asOf: "2026-05-31",
        rows: [],
        subledgerTotal: "0.0000",
        controlTotal: "0.0000",
        divergence: "0.0000",
        isReconciled: true,
      },
    });
    expect(document.documentElement.getAttribute("dir")).toBe("rtl");
    await waitFor(() => {
      expect(screen.getByTestId("retention-receivable")).toBeTruthy();
    });
    const amount = screen.getByTestId("retention-receivable").querySelector("span.amt");
    expect(amount?.getAttribute("dir")).toBe("ltr");
  });

  it("شاشة السجلّ تطلب اختيار منشأة قبل أن تقرأ شيئاً", async () => {
    globalThis.localStorage?.clear();
    await mountApp("/contracting", { "/health": HEALTH });
    expect(screen.getByTestId("contracting-needs-company")).toBeTruthy();
  });
});

/* ═════════════════ ٧ · سجلّ المشاريع: الفراغ والوصول ═══════════════════ */

describe("سجلّ المشاريع", () => {
  it("يشرح لماذا يُسلَّم فارغاً بدل أن يعتذر", async () => {
    await mountApp("/contracting", {
      "/health": HEALTH,
      ["/api/v1/companies/" + COMPANY + "/projects"]: { projectCount: 0, projects: [] },
    });
    const empty = await screen.findByTestId("register-empty");
    expect(empty.textContent).toContain("يُسلَّم فارغاً عمداً");
  });

  it("يعرض العقد ببنوده المعلَّقة قبل أن يُطلب منه مال", async () => {
    const contractId = "k1";
    await mountApp("/contracting", {
      "/health": HEALTH,
      ["/api/v1/companies/" + COMPANY + "/projects"]: {
        projectCount: 1,
        projects: [
          {
            id: "p1",
            code: "PRJ-01",
            nameAr: "مشروع الطريق الدائري",
            nameTranslations: [],
            startedOn: "2026-01-01",
            isActive: true,
            contracts: [{ id: contractId, number: "CON-01", currencyCode: "SAR" }],
          },
        ],
      },
      ["/api/v1/companies/" + COMPANY + "/project-contracts/" + contractId]: {
        id: contractId,
        number: "CON-01",
        projectId: "p1",
        projectCode: "PRJ-01",
        customerPartyId: "CUS-1",
        currencyCode: "SAR",
        signedOn: "2026-01-05",
        retentionRate: "0.10",
        guaranteeMonths: 12,
        pendingPolicy: PENDING,
      },
      ["/api/v1/companies/" + COMPANY + "/project-contracts/" + contractId + "/position"]: {
        contractId,
        contractNumber: "CON-01",
        postedCertificateCount: 0,
        retentionOutstanding: "0.0000",
        advanceOutstanding: "0.0000",
        pendingPolicy: PENDING,
      },
      ["/api/v1/companies/" + COMPANY + "/project-contracts/" + contractId + "/boq-items"]: {
        itemCount: 0,
        items: [],
      },
      ["/api/v1/companies/" + COMPANY + "/project-contracts/" + contractId + "/change-orders"]: {
        changeOrderCount: 0,
        changeOrders: [],
      },
      ["/api/v1/companies/" + COMPANY + "/project-contracts/" + contractId + "/client-certificates"]: {
        certificateCount: 0,
        certificates: [],
      },
    });

    const card = await screen.findByTestId("project-card");
    act(() => {
      selectContracting({ projectId: "p1", projectCode: "PRJ-01" });
    });
    expect(card).toBeTruthy();

    const chip = await screen.findByTestId("contract-chip");
    fireEvent.click(chip);

    const pending = await screen.findByTestId("contract-pending");
    expect(pending.querySelectorAll("li")).toHaveLength(PENDING.length);

    /* والنسبة تُعرض كسراً كما وصلت — لا مضروبةً في مئة ولا بعلامة: */
    const rate = await screen.findByTestId("contract-retention-rate");
    expect(rate.textContent).toContain("0.10");
    expect(rate.textContent).not.toContain("%");
  });
});
