/* ═══════════════════════════════════════════════════════════════════════════
   القسم المخزني — الحرّاس التي تمنع شاشاته من الكذب
   The inventory section — the guards that stop its screens from lying
   ───────────────────────────────────────────────────────────────────────────
   خمسة أشياء تُفحص هنا، وكلّها أعطالٌ **وقعت في منتجات محاسبية حقيقية**:

     ١ · الرفض يبقى على الشاشة ويسمّي البند — لا نخبة تختفي بعد ثانيتين.
     ٢ · الفراغ يقول لماذا وما الخطوة التالية — لا جدولٌ أبيض بلا سبب.
     ٣ · الكمّية لا تمرّ بـ`number` في أي خطوة — والقيمة التي يفسدها العائم
         تُعرض هنا صحيحةً بايتاً ببايت.
     ٤ · الاتجاه يصمد في اللغات الأربع، والوحدات والأرقام معزولة.
     ٥ · «لا أساس تكلفة» يُعرض كلمةً لا صفراً — والفرق بينهما محاسبيّ لا تجميلي.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { RouterProvider } from "@tanstack/react-router";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import type { RawResponse, Transport } from "../src/api/transport";
import { encodeSchema } from "../src/api/transport";
import { SCHEMAS } from "../src/api/generated/runtime-schema";
import { magnitudeIsNegative, magnitudeScale, QuantityValue } from "../src/ui";
import { InventoryItemsScreen } from "../src/screens/inventory/ItemsScreen";
import { InventoryStockScreen } from "../src/screens/inventory/StockScreen";
import { InventoryMovementsScreen } from "../src/screens/inventory/MovementsScreen";
import { InventoryValuationScreen } from "../src/screens/inventory/ValuationScreen";
import { InventoryWarehousesScreen } from "../src/screens/inventory/WarehousesScreen";
import { INVENTORY_NEXT_STEP } from "../src/screens/inventory/shared";
import { SCREENS, SECTIONS } from "../src/app/shell/sections";
import { DesignScreen } from "../src/screens/design/DesignScreen";
import { QUANTITY_SAMPLES } from "../src/screens/design/catalogue";
import { createAppRouter } from "../src/app/router";

/* ‏`globals: false` في إعداد vitest يعني أن التنظيف التلقائي لمكتبة الاختبار
   لا يُسجَّل: لا `afterEach` عامّ يلتقطه. وبدونه تتراكم الشجرة بين الاختبارات
   فيجد الاستعلام عنصرين حيث ينتظر واحداً — وهو عطلٌ يبدو خطأً في الشاشة. */
afterEach(cleanup);

const SRC = path.resolve(process.cwd(), "src");
const read = (rel: string) => readFileSync(path.resolve(SRC, rel), "utf8");
const COMPANY = "00000000-0000-4000-8000-000000000001";
const CODES = ["ar", "en", "ur", "hi"] as const;

/* ═══════════════════════════════════════════ نقلٌ وهمي مطابق للعقد
   يُسلّم JSON **خاماً** كما يسلّمه الخادم، فيمرّ بالفاكّ المُولَّد نفسه: أي
   قيمةٍ لا تطابق نحو العقد تُسقط الشاشة هنا كما تُسقطها في المتصفّح. */
type Routes = Readonly<Record<string, unknown>>;

function stubTransport(routes: Routes, failures: Readonly<Record<string, unknown>> = {}): Transport {
  return ({ method, url }) => {
    const key = method + " " + url.split("?")[0];
    if (key in failures) {
      return Promise.resolve({
        ok: false,
        status: 422,
        json: failures[key],
        url,
      } satisfies RawResponse);
    }
    const body = key in routes ? routes[key] : null;
    return Promise.resolve({ ok: body !== null, status: body !== null ? 200 : 404, json: body, url });
  };
}

function problem(code: string, detailAr: string, detail: string): unknown {
  return {
    type: "about:blank",
    title: "Unprocessable",
    titleAr: "تعذّر تنفيذ الطلب",
    status: 422,
    detail,
    detailAr,
    instance: "/api/v1/x",
    traceId: "trace-0001",
    code,
    errors: [{ code, field: null, messageAr: detailAr, messageEn: detail }],
  };
}

function Wrap(props: { children: ReactNode; locale?: string; transport: Transport }): ReactNode {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false, gcTime: 0 } },
  });
  return (
    <LocaleProvider i18n={createI18n()} initial={props.locale ?? "ar"}>
      <QueryClientProvider client={client}>
        <ApiProvider transport={props.transport}>{props.children}</ApiProvider>
      </QueryClientProvider>
    </LocaleProvider>
  );
}

/* الإعداد يُقرأ من نصّ الاستعلام (`app/config.ts`)، فالشركة تُثبَّت به. */
function withCompany(): void {
  globalThis.history.replaceState(null, "", "/?companyId=" + COMPANY);
}
function withoutCompany(): void {
  globalThis.history.replaceState(null, "", "/?companyId=");
}

/* ═══════════════════════════════════════════════════ عيّنات على شكل العقد */

const CARTON_ITEM = {
  id: "11111111-1111-4111-8111-111111111111",
  code: "ITM-001",
  name: { ar: "ماء معدني ٦٠٠ مل", en: "Mineral water 600ml" },
  itemGroup: "beverages",
  baseUnit: "PCS",
  units: [
    { unitCode: "CTN", numerator: 12, denominator: 1 },
    { unitCode: "PLT", numerator: 1440, denominator: 1 },
  ],
};

const BASE_ONLY_ITEM = {
  id: "22222222-2222-4222-8222-222222222222",
  code: "ITM-002",
  name: { ar: "أسمنت سائب", en: "Bulk cement" },
  itemGroup: "materials",
  baseUnit: "KG",
  units: [],
};

const ITEM_LIST = { itemCount: 2, items: [CARTON_ITEM, BASE_ONLY_ITEM] };
const EMPTY_ITEM_LIST = { itemCount: 0, items: [] };

const BALANCES = {
  balanceCount: 3,
  balances: [
    {
      itemId: "ITM-001",
      warehouseId: "WH-RIYADH",
      locationId: "A-01-3",
      quantity: { magnitude: "1440.000000", unit: "PCS" },
      unitCost: "0.100000",
      value: "144.0000",
      hasCostBasis: true,
    },
    {
      itemId: "ITM-002",
      warehouseId: "WH-RIYADH",
      locationId: "DEFAULT",
      quantity: { magnitude: "-6.500000", unit: "KG" },
      unitCost: "0.000000",
      value: "0.0000",
      hasCostBasis: false,
    },
    {
      itemId: "ITM-001",
      warehouseId: "WH-JEDDAH",
      locationId: "B-02-1",
      quantity: { magnitude: "12.000000", unit: "PCS" },
      unitCost: "0.100000",
      value: "1.2000",
      hasCostBasis: true,
    },
  ],
};

const DRAFT_MOVEMENT = {
  id: "33333333-3333-4333-8333-333333333333",
  number: "SM-0001",
  occurredOn: "2026-05-11",
  direction: "IN",
  itemId: "ITM-001",
  itemGroup: "beverages",
  warehouseId: "WH-RIYADH",
  locationId: "A-01-3",
  quantity: { magnitude: "120.000000", unit: "PCS" },
  cost: "144.0000",
  state: "DRAFT",
  entryId: null,
  alreadyPosted: false,
};

const MOVEMENTS = { movementCount: 1, movements: [DRAFT_MOVEMENT] };

const VALUATION_OFF = {
  asOf: "2026-05-31",
  subledgerTotal: "145.2000",
  balanceTotal: "145.2000",
  controlTotal: "144.0000",
  divergence: "1.2000",
  isReconciled: false,
  divergences: [
    {
      documentType: "StockMovement",
      documentId: "SM-0002",
      itemId: "ITM-001",
      reasonCode: "missing_in_control",
      subledgerEffect: "1.2000",
      controlEffect: "0.0000",
      divergence: "1.2000",
    },
  ],
};

const WAREHOUSE_ID = "44444444-4444-4444-8444-444444444444";

/* مستودعٌ كتبه إنسان، وآخرُ اسمُه صدى نصٍّ وُجد في البيانات ومعطَّل. والفرق
   بينهما هو ما يجب أن تقوله الشاشة، لا ما يجب أن تُخفيه. */
const WAREHOUSES = {
  warehouseCount: 2,
  warehouses: [
    {
      id: WAREHOUSE_ID,
      code: "WH-RIYADH",
      nameAr: "مستودع الرياض",
      nameTranslations: [{ name: "en", value: "Riyadh warehouse" }],
      qualifier: "dry_goods",
      origin: "DECLARED",
      isActive: true,
    },
    {
      id: "55555555-5555-4555-8555-555555555555",
      code: "WH-JEDDAH",
      nameAr: "WH-JEDDAH",
      nameTranslations: [],
      qualifier: "",
      origin: "OBSERVED",
      isActive: false,
    },
  ],
};

const LOCATIONS = {
  locationCount: 2,
  locations: [
    {
      id: "66666666-6666-4666-8666-666666666666",
      warehouseCode: "WH-RIYADH",
      code: "A-01",
      nameAr: "الرفّ الأول",
      nameTranslations: [{ name: "en", value: "First rack" }],
      origin: "DECLARED",
      isActive: true,
    },
    {
      id: "77777777-7777-4777-8777-777777777777",
      warehouseCode: "WH-RIYADH",
      code: "DEFAULT",
      nameAr: "DEFAULT",
      nameTranslations: [],
      origin: "OBSERVED",
      isActive: true,
    },
  ],
};

const WAREHOUSES_URL = "GET /api/v1/companies/" + COMPANY + "/warehouses";
const LOCATIONS_URL =
  "GET /api/v1/companies/" + COMPANY + "/warehouses/" + WAREHOUSE_ID + "/locations";
const ADD_WAREHOUSE_URL = "POST /api/v1/companies/" + COMPANY + "/warehouses";

const ITEMS_URL = "GET /api/v1/companies/" + COMPANY + "/items";
const BALANCES_URL = "GET /api/v1/companies/" + COMPANY + "/stock-balances";
const MOVEMENTS_URL = "GET /api/v1/companies/" + COMPANY + "/stock-movements";
const VALUATION_URL = "GET /api/v1/companies/" + COMPANY + "/inventory-valuation";
const DRAFT_MOVEMENT_URL = "POST /api/v1/companies/" + COMPANY + "/stock-movements";

/* ═══════════════════════════════════ ١ · الكمّية لا تمرّ بـnumber أبداً */

describe("الكمّية نصٌّ بوحدتها — ولا `number` في الطريق", () => {
  it("قيمةٌ يفسدها العائم تُعرض صحيحةً، ونصّ السلك يبقى في السمة", () => {
    /* 9007199254740993 هو 2^53+1: لا يوجد له تمثيل في عائمٍ مزدوج، فأي مرور
       على `Number` يحوّله إلى 9007199254740992 صامتاً. */
    const wire = "9007199254740993.500000";
    const { container } = render(
      <Wrap transport={stubTransport({})}>
        <QuantityValue magnitude={wire} unit="PCS" />
      </Wrap>
    );
    const number = container.querySelector("span.qty__n");
    expect(number?.textContent).toBe("9,007,199,254,740,993.5");
    expect(number?.getAttribute("title")).toBe(wire);
    expect(container.querySelector("span.qty__u")?.textContent).toBe("PCS");
  });

  it("المقياس المعروض مقياس القيمة نفسها، مقصوصةً أصفاره — بلا تقريب", () => {
    expect(magnitudeScale("100.000000")).toBe(0);
    expect(magnitudeScale("1.500000")).toBe(1);
    expect(magnitudeScale("0.000001")).toBe(6);
    expect(magnitudeScale("42")).toBe(0);
  });

  it("السالب يُعرف نصّياً، والصفر السالب ليس سالباً", () => {
    expect(magnitudeIsNegative("-6.500000")).toBe(true);
    expect(magnitudeIsNegative("-0.000000")).toBe(false);
    expect(magnitudeIsNegative("0")).toBe(false);
    expect(magnitudeIsNegative("6.5")).toBe(false);
  });

  it("رمزٌ رقمي في حقلٍ مالي لا يعبر السلك — المُرمِّز يرفضه", () => {
    expect(() =>
      encodeSchema(SCHEMAS, "StockMovementRequest", {
        number: "SM-1",
        occurredOn: "2026-05-11",
        direction: "IN",
        itemId: "ITM-001",
        itemGroup: "beverages",
        warehouseId: "WH-1",
        locationId: "DEFAULT",
        quantity: { magnitude: "1.000000", unit: "PCS" },
        cost: 144.0,
      })
    ).toThrow(TypeError);
  });

  it("لا `parseFloat` ولا `parseInt` ولا `toFixed` في شاشات القسم", () => {
    const files = ["ItemsScreen", "StockScreen", "MovementsScreen", "ValuationScreen", "WarehousesScreen", "shared"];
    for (const name of files) {
      const source = read("screens/inventory/" + name + (name === "shared" ? ".tsx" : ".tsx"));
      expect(source, name).not.toMatch(/parseFloat|parseInt|\.toFixed\(/);
    }
  });

  it("الاستعمال الوحيد لـ`Number` في القسم هو البسط والمقام — وهما `integer` في العقد", () => {
    const files = ["ItemsScreen", "StockScreen", "MovementsScreen", "ValuationScreen", "WarehousesScreen", "shared"];
    const calls: string[] = [];
    for (const name of files) {
      for (const line of read("screens/inventory/" + name + ".tsx").split("\n")) {
        if (/\bNumber\(/.test(line)) calls.push(name + ": " + line.trim());
      }
    }
    /* حارس لافراغ: فحصٌ لا يجد شيئاً يمرّ دائماً. */
    expect(calls.length).toBe(2);
    expect(calls.every((line) => /numerator|denominator/.test(line))).toBe(true);
  });
});

/* ═════════════════════════════════════════ ٢ · الرفض حالةٌ أولى تبقى */

describe("الرفض — يُسمّى ويبقى على الشاشة", () => {
  it("رفض تحويل الوحدة يعرض رسالة الخادم ورمزه وخطوةً تالية، ولا يختفي", async () => {
    withCompany();
    const transport = stubTransport(
      { [ITEMS_URL]: ITEM_LIST, [MOVEMENTS_URL]: MOVEMENTS },
      {
        [DRAFT_MOVEMENT_URL]: problem(
          "inventory.unit_not_convertible",
          "لا معامل تحويل من الوحدة «BOX» إلى وحدة أساس الصنف «ITM-001» وهي «PCS».",
          "There is no conversion factor from unit 'BOX' to the base unit 'PCS'."
        ),
      }
    );
    render(
      <Wrap transport={transport}>
        <InventoryMovementsScreen />
      </Wrap>
    );

    await screen.findByTestId("movements-table");

    /* الطلب يُرسَل من زرّ النموذج، والنموذج يحتاج حقولاً — فيُملأ أدناه.
       والوحدة تُختار من سلّم الصنف، والرفض يقع في الخادم لا هنا. */
    const form = screen.getByTestId("movement-form");
    const set = (testId: string, value: string) => {
      fireEvent.change(within(form).getByTestId(testId), { target: { value } });
    };

    set("movement-number", "SM-0002");
    set("movement-item", "ITM-001");
    set("movement-warehouse", "WH-RIYADH");
    set("movement-location", "A-01-3");
    set("movement-magnitude", "10.000000");
    set("movement-unit", "CTN");
    set("movement-cost", "120.0000");

    const post = screen.getByTestId<HTMLButtonElement>("movement-create");
    await waitFor(() => expect(post.disabled).toBe(false));
    fireEvent.click(post);

    const panel = await screen.findByTestId("problem-panel");
    /* رسالة الخادم بالعربية والإنجليزية معاً، ورمزه الثابت. */
    expect(screen.getByTestId("problem-code").textContent).toBe("inventory.unit_not_convertible");
    expect(panel.textContent).toContain("لا معامل تحويل من الوحدة");
    expect(panel.textContent).toContain("There is no conversion factor");

    /* والخطوة التالية في هذه الواجهة — إلى جانب رسالة الخادم لا بدلاً منها. */
    const next = screen.getByTestId("inventory-next-step");
    expect(next.getAttribute("data-code")).toBe("inventory.unit_not_convertible");
    expect(next.textContent).toContain("سجّل المعامل على الصنف");

    /* **ويبقى**: لوحة رفضٍ لا نخبةٌ تختفي. مرّت ثانيتان، وهو ما يزال هناك. */
    await new Promise((resolve) => setTimeout(resolve, 2000));
    expect(screen.getByTestId("problem-panel")).toBeTruthy();
    expect(screen.getByTestId("inventory-next-step")).toBeTruthy();
  }, 15000);

  it("كل رمزٍ في خريطة الخطوة التالية له نصٌّ في اللغات الأربع", () => {
    const i18n = createI18n();
    const keys = Object.values(INVENTORY_NEXT_STEP);
    expect(keys.length).toBeGreaterThanOrEqual(9);
    for (const code of CODES) {
      i18n.use(code);
      for (const key of keys) {
        const text = i18n.t(key);
        expect(text, code + " ← " + key).not.toBe(key);
        expect(text.length).toBeGreaterThan(0);
      }
    }
  });
});

/* ═══════════════════════════════════════════ ٣ · الفراغ حالةٌ مصمَّمة */

describe("الفراغ — يقول لماذا ويعطي الخطوة التالية", () => {
  it("كتالوجٌ فارغ يُشرَح ولا يُترك جدولاً أبيض", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [ITEMS_URL]: EMPTY_ITEM_LIST })}>
        <InventoryItemsScreen />
      </Wrap>
    );
    const empty = await screen.findByTestId("items-empty");
    expect(empty.textContent).toContain("لا أصناف في هذه المنشأة بعد");
    expect(empty.textContent).toContain("يُسلَّم فارغاً عمداً");
    expect(screen.queryByTestId("items-table")).toBeNull();
  });

  it("أرصدةٌ فارغة تقول ما الذي يجعلها تظهر", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [BALANCES_URL]: { balanceCount: 0, balances: [] },
          [ITEMS_URL]: EMPTY_ITEM_LIST,
        })}
      >
        <InventoryStockScreen />
      </Wrap>
    );
    const empty = await screen.findByTestId("stock-empty");
    expect(empty.textContent).toContain("لا أرصدة في هذه المنشأة");
    expect(empty.textContent).toContain("سجّل رصيداً افتتاحياً");
  });

  it("لا مستندات: الفراغ يفرّق بين «لا شيء بعد» و«لا شيء أصلاً»", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [MOVEMENTS_URL]: { movementCount: 0, movements: [] },
          [ITEMS_URL]: ITEM_LIST,
        })}
      >
        <InventoryMovementsScreen />
      </Wrap>
    );
    const empty = await screen.findByTestId("movements-empty");
    expect(empty.textContent).toContain("لم يُنشَأ مستند حركة مخزونٍ واحد");
  });

  it("بلا منشأةٍ مختارة: الطريق إلى الاختيار لا جدولٌ فارغ", async () => {
    /* هذا الاختبار وحده يركّب الموجّه الحقيقي: الطريق المعروض **رابط**، ورابطٌ
       بلا موجّه لا يُرسَم — فاختبارٌ يتجنّب الموجّه كان سيُثبت أن النصّ موجود
       ولا يُثبت أنه يقود إلى مكان. وهو يثبت في الوقت نفسه أن المسار مُسجَّل. */
    withoutCompany();
    const router = createAppRouter({ memory: true, initialPath: "/inventory/valuation" });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider transport={stubTransport({})}>
            <RouterProvider router={router} />
          </ApiProvider>
        </QueryClientProvider>
      </LocaleProvider>
    );
    const gate = await screen.findByTestId("inventory-needs-company");
    expect(gate.textContent).toContain("اختر المنشأة أولاً");
    const link = screen.getByTestId("inventory-go-sign-in");
    expect(link.getAttribute("href")).toBe("/sign-in");
  });
});

/* ═════════════════════════════════ ٤ · التسكين وأساس التكلفة والسالب */

describe("الأرصدة والتسكين — ما يجب أن يُرى", () => {
  it("يجمع الأرصدة في شجرة مستودعٍ ← موقع، ويسمّي غير المُسكَّن", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [BALANCES_URL]: BALANCES, [ITEMS_URL]: ITEM_LIST })}>
        <InventoryStockScreen />
      </Wrap>
    );
    await screen.findByTestId("stock-by-place");
    expect(screen.getAllByTestId("warehouse-group")).toHaveLength(2);
    const locations = screen.getAllByTestId("location-group");
    expect(locations).toHaveLength(3);
    const unbinned = locations.filter((el) => el.getAttribute("data-unbinned") === "true");
    expect(unbinned).toHaveLength(1);
    expect(unbinned[0]?.textContent).toContain("غير مُسكَّن");
  });

  it("«لا أساس تكلفة» تُعرض كلمةً لا صفراً — والفرق محاسبي", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [BALANCES_URL]: BALANCES, [ITEMS_URL]: ITEM_LIST })}>
        <InventoryStockScreen />
      </Wrap>
    );
    await screen.findByTestId("stock-by-place");
    const cell = screen.getByTestId("cell-no-basis");
    expect(cell.textContent).toBe("لا أساس تكلفة");
    /* ولا رقمٌ في الخليّة نفسها: الصفر رقم، وغياب الأساس ليس رقماً. */
    expect(cell.textContent).not.toMatch(/[0-9٠-٩]/);
    expect(screen.getByTestId("flag-no-basis")).toBeTruthy();
    expect(screen.getByTestId("flag-negative")).toBeTruthy();
  });

  it("لا مجموع قيمةٍ يُحسب في المتصفّح — والشاشة تقول ذلك", async () => {
    withCompany();
    const { container } = render(
      <Wrap transport={stubTransport({ [BALANCES_URL]: BALANCES, [ITEMS_URL]: ITEM_LIST })}>
        <InventoryStockScreen />
      </Wrap>
    );
    await screen.findByTestId("stock-by-place");
    expect(container.textContent).toContain("ولا مجموع قيمةٍ يُحسب في هذه الشاشة");
  });

  it("النقل بين موقعين نقصٌ مُعلَن بقرارٍ مستحقّ، لا شاشةٌ تدّعي", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [BALANCES_URL]: BALANCES, [ITEMS_URL]: ITEM_LIST })}>
        <InventoryStockScreen />
      </Wrap>
    );
    const gap = await screen.findByTestId("stock-move-gap");
    expect(gap.textContent).toContain("قيد البناء");
    expect(gap.textContent).toContain("لا باب له في العقد اليوم");
    expect(gap.textContent).toContain("القرار المستحقّ على المالك");
  });
});

/* ═══════════════════════════════════ ٥ · وحدات القياس المتعدّدة */

describe("سلّم الوحدات — نسبةٌ لا عدد عشري، ورفضٌ لا افتراض", () => {
  it("يعرض المعامل بسطاً ومقاماً، ويشرح ما يعنيه بالكلمات", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [ITEMS_URL]: ITEM_LIST })}>
        <InventoryItemsScreen />
      </Wrap>
    );
    await screen.findByTestId("items-table");
    const picks = screen.getAllByTestId("item-pick");
    fireEvent.click(picks[0] as HTMLElement);
    const ladder = await screen.findByTestId("item-ladder");
    expect(ladder.textContent).toContain("CTN");
    expect(ladder.textContent).toContain("12");
    expect(ladder.textContent).toContain("PCS");
    /* ولا عدد عشري في السلّم أبداً. */
    expect(ladder.textContent).not.toMatch(/\d+\.\d/);
  });

  it("صنفٌ بوحدة أساسه وحدها يُعلَن، ويسمّي الرفض الذي سيقع", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [ITEMS_URL]: ITEM_LIST })}>
        <InventoryItemsScreen />
      </Wrap>
    );
    await screen.findByTestId("items-table");
    const picks = screen.getAllByTestId("item-pick");
    fireEvent.click(picks[1] as HTMLElement);
    const note = await screen.findByTestId("item-base-only");
    expect(note.textContent).toContain("inventory.unit_not_convertible");
    expect(note.textContent).toContain("لا تُقرَّب ولا تُفترَض");
  });

  it("مُنتقي الوحدة يعرض سلّم الصنف المختار وحده — ولا خيارَ يُعرَف أنه سيُرفض", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [ITEMS_URL]: ITEM_LIST, [MOVEMENTS_URL]: MOVEMENTS })}>
        <InventoryMovementsScreen />
      </Wrap>
    );
    await screen.findByTestId("movement-form");
    const item = screen.getByTestId<HTMLSelectElement>("movement-item");
    /* الانتظار حتى يصل الكتالوج: ضبطُ قيمةٍ لا خيار لها يُبقي المُنتقي فارغاً،
       فيبدو الاختبار وكأنه أثبت شيئاً وهو لم يختر صنفاً أصلاً. */
    await waitFor(() => expect([...item.options].map((o) => o.value)).toContain("ITM-002"));
    fireEvent.change(item, { target: { value: "ITM-002" } });

    await waitFor(() => {
      const unit = screen.getByTestId<HTMLSelectElement>("movement-unit");
      const options = [...unit.options].map((o) => o.value).filter((v) => v !== "");
      expect(options).toEqual(["KG"]);
    });
    expect(screen.getByTestId("movement-unit-hint").textContent).toContain(
      "يُمسَك بوحدة أساسه وحدها"
    );
  });

  it("الصادر تكلفته صفرٌ مُقفَل، والقاعدة معروضة لا مخفيّة", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [ITEMS_URL]: ITEM_LIST, [MOVEMENTS_URL]: MOVEMENTS })}>
        <InventoryMovementsScreen />
      </Wrap>
    );
    await screen.findByTestId("movement-form");
    const direction = screen.getByTestId<HTMLSelectElement>("movement-direction");
    fireEvent.change(direction, { target: { value: "OUT" } });

    await waitFor(() => {
      const cost = screen.getByTestId<HTMLInputElement>("movement-cost");
      expect(cost.value).toBe("0");
      expect(cost.readOnly).toBe(true);
    });
    expect(screen.getByTestId("movement-cost-hint").textContent).toContain(
      "المتوسط المرجّح المتحرّك"
    );
  });

  it("الموقع قرارٌ يُتَّخذ: DEFAULT خيارٌ باسمه ومعناه لا حقلٌ يُملأ صامتاً", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [ITEMS_URL]: ITEM_LIST, [MOVEMENTS_URL]: MOVEMENTS })}>
        <InventoryMovementsScreen />
      </Wrap>
    );
    await screen.findByTestId("movement-form");
    const named = screen.getByTestId<HTMLInputElement>("location-mode-named");
    expect(named.checked).toBe(true);
    fireEvent.click(screen.getByTestId("location-mode-default"));
    const why = await screen.findByTestId("location-default-why");
    expect(why.textContent).toContain("لا افتراضٌ صامت");
  });
});

/* ═══════════════════════════════════════ ٦ · المطابقة بثلاث طرق */

describe("التقييم والمطابقة", () => {
  it("يعرض الطرق الثلاثة والفارق، ويسمّي كل مستندٍ منحرف بسببه", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [VALUATION_URL]: VALUATION_OFF })}>
        <InventoryValuationScreen />
      </Wrap>
    );
    await screen.findByTestId("valuation-routes");
    expect(screen.getByTestId("reconciled-pill").getAttribute("data-reconciled")).toBe("false");
    expect(screen.getByTestId("reconciled-note").textContent).toContain("لم تصل إلى الرقم نفسه");

    const row = screen.getByTestId("divergence-row");
    expect(row.textContent).toContain("StockMovement");
    expect(row.textContent).toContain("SM-0002");
    expect(row.querySelector("[data-reason]")?.getAttribute("data-reason")).toBe(
      "missing_in_control"
    );
    expect(row.textContent).toContain("حركةٌ بلا نظير في نقطة الضبط");
  });

  it("المطابق التامّ حالةٌ مصمَّمة تقول «صفرٌ بالضبط»، لا جدولٌ فارغ", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [VALUATION_URL]: {
            ...VALUATION_OFF,
            controlTotal: "145.2000",
            divergence: "0.0000",
            isReconciled: true,
            divergences: [],
          },
        })}
      >
        <InventoryValuationScreen />
      </Wrap>
    );
    await screen.findByTestId("valuation-routes");
    expect(screen.getByTestId("reconciled-note").textContent).toContain("صفرٌ بالضبط");
    const none = screen.getByTestId("valuation-no-divergences");
    expect(none.textContent).toContain("لا مستند منحرف");
  });
});

/* ═══════════════════════════════ ٦٫٥ · الترحيل والوصول الثاني بالهوية */

describe("الترحيل — واقعةٌ لا رجعة فيها، ووصولٌ ثانٍ يُقال صراحةً", () => {
  const POST_URL =
    "POST /api/v1/companies/" + COMPANY + "/stock-movements/" + DRAFT_MOVEMENT.id + "/posting";

  it("الترحيل الأول يعرض القيد ولا يقول «رُحِّل مرّتين»", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [ITEMS_URL]: ITEM_LIST,
          [MOVEMENTS_URL]: MOVEMENTS,
          [POST_URL]: {
            ...DRAFT_MOVEMENT,
            state: "POSTED",
            entryId: "44444444-4444-4444-8444-444444444444",
            alreadyPosted: false,
          },
        })}
      >
        <InventoryMovementsScreen />
      </Wrap>
    );
    await screen.findByTestId("movements-table");
    fireEvent.click(screen.getByTestId("movement-post"));
    const panel = await screen.findByTestId("movement-posted");
    expect(panel.getAttribute("data-already-posted")).toBe("false");
    expect(panel.textContent).toContain("رُحِّل المستند");
    expect(screen.getByTestId("posted-entry").textContent).toBe(
      "44444444-4444-4444-8444-444444444444"
    );
  });

  it("الوصول الثاني بالهوية نفسها يُعلَن — لا نجاحٌ ثانٍ يُقرأ ترحيلاً مكرّراً", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [ITEMS_URL]: ITEM_LIST,
          [MOVEMENTS_URL]: MOVEMENTS,
          [POST_URL]: {
            ...DRAFT_MOVEMENT,
            state: "POSTED",
            entryId: "44444444-4444-4444-8444-444444444444",
            alreadyPosted: true,
          },
        })}
      >
        <InventoryMovementsScreen />
      </Wrap>
    );
    await screen.findByTestId("movements-table");
    fireEvent.click(screen.getByTestId("movement-post"));
    const panel = await screen.findByTestId("movement-posted");
    expect(panel.getAttribute("data-already-posted")).toBe("true");
    expect(panel.textContent).toContain("كانت مُرحَّلة سلفاً");
    expect(panel.textContent).toContain("ولم يقع ترحيلٌ جديد الآن");
  });
});

/* ═══════════════════ ٦٫٦ · فهرس التصميم يبقى صادقاً عن الأوّليّة الجديدة */

describe("فهرس التصميم", () => {
  it("يعرض الكمّية ووحدتها حيّةً — فما يبني عليه القسم مذكورٌ في العقد المرئي", async () => {
    const { container } = render(
      <Wrap transport={stubTransport({})}>
        <DesignScreen />
      </Wrap>
    );
    const section = await screen.findByTestId("section-quantity");
    expect(section.textContent).toContain("الكمّية ووحدتها");
    for (const sample of QUANTITY_SAMPLES) {
      const shown = within(section).getByTestId("quantity-" + sample.key);
      expect(shown.querySelector(".qty__n")?.getAttribute("title")).toBe(sample.magnitude);
      expect(shown.querySelector(".qty__u")?.textContent).toBe(sample.unit);
    }
    /* السالب موسومٌ في الفهرس كما هو موسومٌ في الشاشة. */
    expect(
      container.querySelector('[data-testid="quantity-negative"]')?.getAttribute("data-negative")
    ).toBe("true");
  });
});

/* ══════════════════════════════════════ ٧ · الاتجاه في اللغات الأربع */

describe("الاتجاه — يصمد في اللغات الأربع", () => {
  it("جذر الوثيقة يتبع اللغة، والقسم يُرسَم في كلٍّ منها", async () => {
    const expected: Record<string, string> = { ar: "rtl", ur: "rtl", en: "ltr", hi: "ltr" };
    for (const code of CODES) {
      withCompany();
      const view = render(
        <Wrap locale={code} transport={stubTransport({ [BALANCES_URL]: BALANCES, [ITEMS_URL]: ITEM_LIST })}>
          <InventoryStockScreen />
        </Wrap>
      );
      await screen.findByTestId("stock-by-place");
      expect(document.documentElement.getAttribute("dir"), code).toBe(expected[code]);
      /* الرصيد يُرسَم في كل لغة — لا شاشةٌ تعمل في العربية وحدها. */
      expect(view.container.querySelectorAll('[data-testid="balance-row"]').length).toBe(3);
      view.unmount();
    }
  });

  it("رمز الوحدة والمقدار معزولان اتجاهياً في ورقة الأنماط", () => {
    const css = read("styles/primitives.css");
    const block = css.slice(css.indexOf(".qty{"));
    expect(block).toContain("unicode-bidi:isolate");
    /* ولا اتجاه مفروض على رمز الوحدة: قد يكون «PCS» وقد يكون «حبة». */
    expect(block.slice(0, block.indexOf(".qty[data-negative"))).not.toMatch(/direction\s*:/);
  });

  it("ورقة أنماط القسم بلا خاصية فيزيائية — الاتجاه منطقيٌّ وحده", () => {
    const css = readFileSync(
      path.resolve(SRC, "screens/inventory/inventory.css"),
      "utf8"
    ).replace(/\/\*[\s\S]*?\*\//g, " ");
    expect(css).not.toMatch(/(margin|padding|border|inset|float|clear|text-align)-(left|right)\s*:/);
    expect(css).not.toMatch(/translateX\(/);
  });
});

/* ═══════════════════════════════════ ٨ · عقد الملاحة مع بقيّة النظام */

describe("عقد الملاحة", () => {
  it("القسم المخزني مبنيٌّ ومساره من شاشاته", () => {
    const inventory = SECTIONS.find((s) => s.id === "inventory");
    expect(inventory?.built).toBe(true);
    expect(inventory?.path).toBe("/inventory/stock");
    const paths = SCREENS.filter((s) => s.section === "inventory").map((s) => s.path);
    expect(paths).toContain(inventory?.path);
    expect(paths).toHaveLength(5);
  });

  /*
   * ‏**الملاحة الجانبية نسخةٌ ثانية من `SCREENS` ولا شيء يقارنهما** (‏App.tsx).
   * وهذا الحارس **مقصورٌ على شاشات هذا القسم عمداً**: لو عمّ كل الشاشات لأحمَرَّ
   * فرعَ من يبني قسماً آخر بملفٍّ لم يلمسه. فهو يحرس ما أضفتُه، ويترك التوصية
   * الأوسع — أن تُقاد القائمة من `SCREENS` — قراراً للمالك.
   */
  it("كل شاشةٍ مخزنية يبلغها من يقرأ الملاحة، لا من يعرف اختصار لوحة الأوامر وحده", async () => {
    withCompany();
    const router = createAppRouter({ memory: true, initialPath: "/inventory/stock" });
    const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
    render(
      <LocaleProvider i18n={createI18n()} initial="ar">
        <QueryClientProvider client={client}>
          <ApiProvider transport={stubTransport({ [BALANCES_URL]: BALANCES, [ITEMS_URL]: ITEM_LIST })}>
            <RouterProvider router={router} />
          </ApiProvider>
        </QueryClientProvider>
      </LocaleProvider>
    );
    await screen.findByTestId("inventory-stock-screen");

    const wanted = SCREENS.filter((s) => s.section === "inventory").map((s) => s.path);
    expect(wanted).toHaveLength(5);
    const reachable = [...document.querySelectorAll('nav [data-testid^="nav-inventory-"]')].map(
      (a) => a.getAttribute("href")
    );
    for (const path of wanted) {
      expect(reachable, "لا رابط في الملاحة إلى " + path).toContain(path);
    }
  });

  it("كل شاشةٍ مخزنية اسمها مترجَمٌ في اللغات الأربع", () => {
    const i18n = createI18n();
    const screens = SCREENS.filter((s) => s.section === "inventory");
    expect(screens.length).toBeGreaterThan(0);
    for (const code of CODES) {
      i18n.use(code);
      for (const entry of screens) {
        expect(i18n.t(entry.labelKey), code + " ← " + entry.labelKey).not.toBe(entry.labelKey);
      }
    }
  });
});

/* ══════════════════════════════ ٩ · المستودعات: منشأ الاسم يُقال لا يُخفى */

describe("كتالوج المستودعات والمواقع", () => {
  it("المعطَّل يبقى معروضاً موسوماً، ومنشأ الاسم يُعرض — ولا رقم حساب في الشاشة", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [WAREHOUSES_URL]: WAREHOUSES })}>
        <InventoryWarehousesScreen />
      </Wrap>
    );

    await screen.findByTestId("warehouses-table");
    const rows = screen.getAllByTestId("warehouse-row");

    /* ‏**الاثنان معاً**: إخفاء المعطَّل يترك رصيداً قائماً بلا مستودعٍ يفسّره
       في شاشة الأرصدة، وهو أسوأ من صفٍّ مكتوب عليه «معطَّل». */
    expect(rows).toHaveLength(2);

    const first = rows[0] as HTMLElement;
    const second = rows[1] as HTMLElement;

    expect(within(first).getByTestId("place-origin").getAttribute("data-origin")).toBe("DECLARED");
    expect(within(second).getByTestId("place-origin").getAttribute("data-origin")).toBe("OBSERVED");

    expect(within(first).getByTestId("place-state").getAttribute("data-active")).toBe("true");
    expect(within(second).getByTestId("place-state").getAttribute("data-active")).toBe("false");

    /* والشاشة **تسأل** عن الأسماء التي لم يكتبها أحد بدل أن تدّعي أنّ عندها أسماء. */
    expect(screen.getByTestId("warehouses-observed").textContent ?? "").not.toBe("");
  });

  it("المواقع مورد فرعي: لا تُقرأ إلا بعد اختيار مستودعها", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [WAREHOUSES_URL]: WAREHOUSES, [LOCATIONS_URL]: LOCATIONS })}>
        <InventoryWarehousesScreen />
      </Wrap>
    );

    await screen.findByTestId("warehouses-table");

    /* قبل الاختيار: لا لوح مواقع أصلاً — لأن «A-01» بلا مستودعه ليس هوية. */
    expect(screen.queryByTestId("locations-panel")).toBeNull();

    fireEvent.click(screen.getAllByTestId("warehouse-pick")[0] as HTMLElement);

    await screen.findByTestId("locations-table");
    expect(screen.getAllByTestId("location-row")).toHaveLength(2);
  });

  it("رفض تسجيل مستودع يُعرض برمزه ورسالة الخادم، ولا يختفي", async () => {
    withCompany();
    const transport = stubTransport(
      { [WAREHOUSES_URL]: WAREHOUSES },
      {
        [ADD_WAREHOUSE_URL]: problem(
          "inventory.duplicate_warehouse_code",
          "رمز المستودع «WH-RIYADH» مستعمَل في هذه المنشأة.",
          "Warehouse code 'WH-RIYADH' is already used in this company."
        ),
      }
    );

    render(
      <Wrap transport={transport}>
        <InventoryWarehousesScreen />
      </Wrap>
    );

    await screen.findByTestId("warehouses-table");

    fireEvent.change(screen.getByTestId("warehouse-code"), { target: { value: "WH-RIYADH" } });
    fireEvent.change(screen.getByTestId("warehouse-name-ar"), { target: { value: "مستودع" } });
    fireEvent.click(screen.getByTestId("warehouse-submit"));

    const panel = await screen.findByTestId("problem-panel");
    expect(panel.textContent ?? "").toContain("WH-RIYADH");

    /* والرفض حالةٌ أولى تبقى: لا يختفي بعد لحظة ولا يُستبدل بجدولٍ فارغ. */
    await waitFor(() => expect(screen.getByTestId("problem-panel")).toBeTruthy());
  });
});
