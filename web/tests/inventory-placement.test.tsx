/* ═══════════════════════════════════════════════════════════════════════════
   التسكين والنقل والوحدات — الحرّاس التي تمنع الشاشات الخمس من الكذب
   Placement, transfer and units — the guards that stop the five from lying
   ───────────────────────────────────────────────────────────────────────────
   ستّة أشياء تُفحص هنا، وكلّها **فروقٌ مقصودة في العقد** ينهار المنتج إن
   طُمست في الواجهة:

     ١ · تعطيل **موضعٍ** فيه رصيد يُرفض، وتعطيل **صنفٍ** له رصيد يُقبل
         (ADR-0072) — والشاشة تقول الحكم **قبل** الضغط لا بعده.
     ٢ · تعطيل **الرفّ** لا فحص رصيدٍ عليه، بخلاف الموقع — وهو فرقٌ ثالث
         لا يجوز أن يُوحَّد في نصٍّ واحد.
     ٣ · التحويل يقع بلا باقٍ **أو يُرفض باسمه** (ADR-0073) — والمسبار يُظهر
         الرفض رفضاً مُسمّى، ولا يُظهر «0.583333» ولا صفراً.
     ٤ · النقل داخل المنشأة **لا يكتب قيداً** (ADR-0071) — فلا عمود قيد في
         جدوله ولا شارة ترحيل ولا كلمة «مُرحَّل».
     ٥ · الكمّية نصٌّ من أوّلها إلى آخرها: لا `Number` ولا `parseFloat` على
         مقدارٍ في أي من الملفّات الخمسة.
     ٦ · الرمز غير المسجَّل في الأرصدة **يخرج ويُوسَم**، ولا يُحذف من القائمة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { readFileSync } from "node:fs";
import path from "node:path";
import type { ReactNode } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor, within } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { LocaleProvider } from "../src/i18n/react";
import { createI18n } from "../src/i18n/setup";
import { ApiProvider } from "../src/app/api-context";
import type { RawResponse, Transport } from "../src/api/transport";
import { InventoryWarehousesScreen } from "../src/screens/inventory/WarehousesScreen";
import { InventoryPlacementScreen } from "../src/screens/inventory/PlacementScreen";
import { InventoryTransfersScreen } from "../src/screens/inventory/TransfersScreen";
import { InventoryUnitsScreen } from "../src/screens/inventory/UnitsScreen";
import { InventoryPlacementBalancesScreen } from "../src/screens/inventory/PlacementBalancesScreen";
import { INVENTORY_NEXT_STEP } from "../src/screens/inventory/shared";
import { SCREENS } from "../src/app/shell/sections";

afterEach(cleanup);

const SRC = path.resolve(process.cwd(), "src");
const read = (rel: string) => readFileSync(path.resolve(SRC, rel), "utf8");
const COMPANY = "00000000-0000-4000-8000-000000000001";
const CODES = ["ar", "en", "ur", "hi"] as const;

/** الملفّات الخمسة التي أُضيفت، بأسمائها. */
const NEW_SCREENS = [
  "screens/inventory/WarehousesScreen.tsx",
  "screens/inventory/PlacementScreen.tsx",
  "screens/inventory/TransfersScreen.tsx",
  "screens/inventory/UnitsScreen.tsx",
  "screens/inventory/PlacementBalancesScreen.tsx",
];

/* ═══════════════════════════════════════════ نقلٌ وهمي مطابق للعقد */

type Routes = Readonly<Record<string, unknown>>;

function stubTransport(routes: Routes, failures: Readonly<Record<string, unknown>> = {}): Transport {
  return ({ method, url }) => {
    const key = method + " " + url.split("?")[0];
    if (key in failures) {
      return Promise.resolve({ ok: false, status: 422, json: failures[key], url } satisfies RawResponse);
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
    traceId: "trace-0002",
    code,
    errors: [{ code, field: null, messageAr: detailAr, messageEn: detail }],
  };
}

function Wrap(props: { children: ReactNode; locale?: string; transport: Transport }): ReactNode {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false, gcTime: 0 } } });
  return (
    <LocaleProvider i18n={createI18n()} initial={props.locale ?? "ar"}>
      <QueryClientProvider client={client}>
        <ApiProvider transport={props.transport}>{props.children}</ApiProvider>
      </QueryClientProvider>
    </LocaleProvider>
  );
}

/** أوّلُ ما يطابق، **ويرمي إن لم يطابق شيء**: فحصٌ ينقر على `undefined` يمرّ
    خضراءَ في TypeScript ويسقط بغموضٍ في المتصفّح. */
function first(elements: readonly HTMLElement[]): HTMLElement {
  const one = elements[0];
  if (!one) throw new Error("لا عنصر يطابق — الفحص أعمى لا ناجح.");
  return one;
}

function withCompany(): void {
  globalThis.history.replaceState(null, "", "/?companyId=" + COMPANY);
}

/* ═══════════════════════════════════════════════════ عيّنات على شكل العقد */

const WH_RIYADH = {
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa1",
  code: "WH-RIYADH",
  name: { ar: "مستودع الرياض", en: "Riyadh warehouse" },
  level: "WAREHOUSE",
  parentCode: "",
  isActive: true,
};

const WH_OLD = {
  id: "aaaaaaaa-aaaa-4aaa-8aaa-aaaaaaaaaaa2",
  code: "WH-OLD",
  name: { ar: "المستودع القديم", en: "Old warehouse" },
  level: "WAREHOUSE",
  parentCode: "",
  isActive: false,
};

const LOC_A = {
  id: "bbbbbbbb-bbbb-4bbb-8bbb-bbbbbbbbbbb1",
  code: "A-01-3",
  name: { ar: "الممرّ أ رفّ ٣", en: "Aisle A shelf 3" },
  level: "LOCATION",
  parentCode: "WH-RIYADH",
  isActive: true,
};

const BIN_A = {
  id: "cccccccc-cccc-4ccc-8ccc-ccccccccccc1",
  code: "A-01-3-B",
  name: { ar: "الصندوق ب", en: "Box B" },
  level: "BIN",
  parentCode: "A-01-3",
  isActive: true,
};

const WAREHOUSES = { placeCount: 2, places: [WH_RIYADH, WH_OLD] };
const LOCATIONS = { placeCount: 1, places: [LOC_A] };
const BINS = { placeCount: 1, places: [BIN_A] };

const ITEM = {
  id: "11111111-1111-4111-8111-111111111111",
  code: "ITM-001",
  name: { ar: "ماء معدني ٦٠٠ مل", en: "Mineral water 600ml" },
  itemGroup: "beverages",
  baseUnit: "PCS",
  units: [{ unitCode: "CTN", numerator: 12, denominator: 1 }],
};
const ITEMS = { itemCount: 1, items: [ITEM] };

const DRAFT_TRANSFER = {
  id: "dddddddd-dddd-4ddd-8ddd-ddddddddddd1",
  number: "TR-0001",
  occurredOn: "2026-05-11",
  itemId: "ITM-001",
  itemGroup: "beverages",
  fromWarehouseId: "WH-RIYADH",
  fromLocationId: "A-01-3",
  toWarehouseId: "WH-RIYADH",
  toLocationId: "A-02-1",
  quantity: { magnitude: "9007199254740993.500000", unit: "PCS" },
  value: "0.0000",
  state: "DRAFT",
  alreadyMoved: false,
};

const TRANSFERS = { transferCount: 1, transfers: [DRAFT_TRANSFER] };

const UNITS = {
  unitCount: 2,
  units: [
    {
      id: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee1",
      code: "PCS",
      name: { ar: "حبة", en: "Piece" },
      quantityClass: "COUNT",
      isActive: true,
    },
    {
      id: "eeeeeeee-eeee-4eee-8eee-eeeeeeeeeee2",
      code: "KG",
      name: { ar: "كيلوغرام", en: "Kilogram" },
      quantityClass: "WEIGHT",
      isActive: true,
    },
  ],
};

const CONVERSIONS = {
  conversionCount: 1,
  conversions: [
    {
      id: "ffffffff-ffff-4fff-8fff-fffffffffff1",
      fromUnit: "PCS",
      toUnit: "CTN",
      numerator: 1,
      denominator: 12,
      quantityClass: "COUNT",
    },
  ],
};

const PLACEMENT_BALANCES = {
  balanceCount: 2,
  balances: [
    {
      itemId: "ITM-001",
      warehouseId: "WH-RIYADH",
      warehouseName: { ar: "مستودع الرياض", en: "Riyadh warehouse" },
      warehouseRegistered: true,
      locationId: "A-01-3",
      locationName: { ar: "الممرّ أ رفّ ٣", en: "Aisle A shelf 3" },
      locationRegistered: true,
      quantity: { magnitude: "1440.000000", unit: "PCS" },
      unitCost: "0.100000",
      value: "144.0000",
      hasCostBasis: true,
    },
    {
      itemId: "ITM-002",
      warehouseId: "WH-GHOST",
      warehouseName: { ar: "WH-GHOST", en: "WH-GHOST" },
      warehouseRegistered: false,
      locationId: "DEFAULT",
      locationName: { ar: "DEFAULT", en: "DEFAULT" },
      locationRegistered: false,
      quantity: { magnitude: "-6.500000", unit: "KG" },
      unitCost: "0.000000",
      value: "0.0000",
      hasCostBasis: false,
    },
  ],
};

const base = "/api/v1/companies/" + COMPANY;
const WAREHOUSES_URL = "GET " + base + "/warehouses";
const LOCATIONS_URL = "GET " + base + "/warehouses/" + WH_RIYADH.id + "/locations";
const BINS_URL = "GET " + base + "/warehouses/" + WH_RIYADH.id + "/locations/" + LOC_A.id + "/bins";
const WAREHOUSE_OFF_URL = "POST " + base + "/warehouses/" + WH_RIYADH.id + "/deactivation";
const LOCATION_OFF_URL =
  "POST " + base + "/warehouses/" + WH_RIYADH.id + "/locations/" + LOC_A.id + "/deactivation";
const ITEMS_URL = "GET " + base + "/items";
const TRANSFERS_URL = "GET " + base + "/stock-transfers";
const MOVE_URL = "POST " + base + "/stock-transfers/" + DRAFT_TRANSFER.id + "/movement";
const UNITS_URL = "GET " + base + "/units-of-measure";
const CONVERSIONS_URL = "GET " + base + "/unit-conversions";
const TRIAL_URL = "POST " + base + "/unit-conversions/trials";
const PLACEMENT_BALANCES_URL = "GET " + base + "/placement-balances";

/* ═══════════════════════ ١ · التعطيل قرارٌ مُسمّى، وحكمُه يختلف بالمستوى */

describe("التعطيل — الحكم يُقال قبل الضغط، ويختلف بالمستوى", () => {
  it("سجلّ المستودعات يعرض المُعطَّل ولا يُخفيه، ويسمّي حكم التعطيل", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [WAREHOUSES_URL]: WAREHOUSES })}>
        <InventoryWarehousesScreen />
      </Wrap>
    );

    await screen.findByTestId("warehouses-table");
    const rows = screen.getAllByTestId("warehouse-row");
    expect(rows).toHaveLength(2);
    /* المُعطَّل باقٍ في القائمة بحالته المقروءة، لا محذوف. */
    expect(rows.some((r) => r.getAttribute("data-active") === "false")).toBe(true);

    /* والحكم منشورٌ على الشاشة بلا أن يضغط أحد شيئاً. */
    const note = screen.getByTestId("warehouses-off-note").textContent ?? "";
    expect(note).toContain("يُرفض");
    /* ويسمّي الفرق عن الصنف صراحةً — وهو الفرق المقصود في ADR-0072. */
    expect(note).toContain("الصنف");
  });

  it("التعطيل خطوتان: الأولى تُظهر الحكم ولا تُرسل شيئاً", async () => {
    withCompany();
    const sent: string[] = [];
    const transport: Transport = ({ method, url }) => {
      sent.push(method + " " + url.split("?")[0]);
      const key = method + " " + url.split("?")[0];
      const body = key === WAREHOUSES_URL ? WAREHOUSES : null;
      return Promise.resolve({ ok: body !== null, status: body !== null ? 200 : 404, json: body, url });
    };
    render(
      <Wrap transport={transport}>
        <InventoryWarehousesScreen />
      </Wrap>
    );
    await screen.findByTestId("warehouses-table");
    const before = sent.length;

    fireEvent.click(first(screen.getAllByTestId("warehouse-deactivate")));
    await screen.findByTestId("warehouse-confirm");
    /* لا نداء وقع: الخطوة الأولى قرارٌ يُقرأ لا فعلٌ يُرسَل. */
    expect(sent.length).toBe(before);
    expect(screen.getByTestId("warehouse-off-rule").textContent ?? "").toContain("يُرفض");
  });

  it("رفضُ تعطيل موضعٍ فيه رصيد يبقى مُسمّىً بخطوته التالية", async () => {
    withCompany();
    const transport = stubTransport(
      { [WAREHOUSES_URL]: WAREHOUSES },
      {
        [WAREHOUSE_OFF_URL]: problem(
          "inventory.storage_place_still_holds_stock",
          "لا يُعطَّل المستودع «WH-RIYADH» وفيه رصيد: الصنف «ITM-001» فيه 1440 PCS.",
          "The warehouse 'WH-RIYADH' cannot be deactivated while it holds stock."
        ),
      }
    );
    render(
      <Wrap transport={transport}>
        <InventoryWarehousesScreen />
      </Wrap>
    );
    await screen.findByTestId("warehouses-table");

    fireEvent.click(first(screen.getAllByTestId("warehouse-deactivate")));
    fireEvent.click(await screen.findByTestId("warehouse-confirm-off"));

    const panel = await screen.findByTestId("problem-panel");
    expect(within(panel).getByTestId("problem-code").textContent).toBe(
      "inventory.storage_place_still_holds_stock"
    );
    /* رسالة الخادم تُعرض كما هي — تسمّي الصنف وكمّيته. */
    expect(panel.textContent).toContain("ITM-001");

    /* والخطوة التالية **في هذه الواجهة** إلى جانبها لا بدلاً منها. */
    const next = await screen.findByTestId("inventory-next-step");
    expect(next.getAttribute("data-code")).toBe("inventory.storage_place_still_holds_stock");

    /* والرفض يبقى: لا نخبةَ تختفي بعد ثانيتين. */
    await new Promise((r) => setTimeout(r, 60));
    expect(screen.getByTestId("problem-panel")).toBeTruthy();
  });

  it("حكمُ الرفّ غيرُ حكم الموقع: لا فحص رصيدٍ على الرفّ", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [WAREHOUSES_URL]: WAREHOUSES,
          [LOCATIONS_URL]: LOCATIONS,
          [BINS_URL]: BINS,
        })}
      >
        <InventoryPlacementScreen />
      </Wrap>
    );

    await screen.findByTestId("placement-tree");

    /* اختيار مستودعٍ ثم موقعٍ ثم رفّ — والانتماء بنيةٌ في المسار لا حقل. */
    fireEvent.click(first(within(screen.getByTestId("rung-warehouse")).getAllByTestId("place-pick")));
    const locationRung = await screen.findByTestId("rung-location");
    await waitFor(() => expect(within(locationRung).queryAllByTestId("place-pick")).toHaveLength(1));

    const locationPanelRule = () =>
      (screen.getByTestId("placement-chosen").textContent ?? "");
    expect(locationPanelRule()).toContain("WH-RIYADH");

    fireEvent.click(first(within(locationRung).getAllByTestId("place-pick")));
    await waitFor(() =>
      expect(screen.getByTestId("placement-chosen").textContent ?? "").toContain("A-01-3")
    );
    const locationRule = screen.getByTestId("placement-chosen").textContent ?? "";
    expect(locationRule).toContain("يُرفض");

    const binRung = await screen.findByTestId("rung-bin");
    await waitFor(() => expect(within(binRung).queryAllByTestId("place-pick")).toHaveLength(1));
    fireEvent.click(first(within(binRung).getAllByTestId("place-pick")));
    await waitFor(() =>
      expect(screen.getByTestId("placement-chosen").textContent ?? "").toContain("A-01-3-B")
    );
    const binRule = screen.getByTestId("placement-chosen").textContent ?? "";
    /* الحكمان **مختلفان نصّاً**، ولا يُوحَّدان في جملةٍ واحدة تُخفي الفرق. */
    expect(binRule).toContain("لا فحص رصيد");
    expect(binRule).not.toBe(locationRule);
  });

  it("رفضُ تعطيل موقعٍ فيه رصيد يصل من مسار الموقع لا من مسار المستودع", async () => {
    withCompany();
    const transport = stubTransport(
      { [WAREHOUSES_URL]: WAREHOUSES, [LOCATIONS_URL]: LOCATIONS, [BINS_URL]: BINS },
      {
        [LOCATION_OFF_URL]: problem(
          "inventory.storage_place_still_holds_stock",
          "لا يُعطَّل الموقع «A-01-3» وفيه رصيد: الصنف «ITM-001» فيه 1440 PCS.",
          "The location 'A-01-3' cannot be deactivated while it holds stock."
        ),
      }
    );
    render(
      <Wrap transport={transport}>
        <InventoryPlacementScreen />
      </Wrap>
    );
    await screen.findByTestId("placement-tree");
    fireEvent.click(first(within(screen.getByTestId("rung-warehouse")).getAllByTestId("place-pick")));
    const locationRung = await screen.findByTestId("rung-location");
    await waitFor(() => expect(within(locationRung).queryAllByTestId("place-pick")).toHaveLength(1));
    fireEvent.click(first(within(locationRung).getAllByTestId("place-pick")));

    fireEvent.click(await screen.findByTestId("place-deactivate"));
    fireEvent.click(await screen.findByTestId("place-confirm-off"));

    const panel = await screen.findByTestId("problem-panel");
    expect(within(panel).getByTestId("problem-code").textContent).toBe(
      "inventory.storage_place_still_holds_stock"
    );
    expect(panel.textContent).toContain("A-01-3");
  });
});

/* ═══════════════════════════ ٢ · التحويل يقع بلا باقٍ أو يُرفض باسمه */

describe("مسبار التحويل — رفضٌ مُسمّى لا رقمٌ مقرَّب", () => {
  it("التحويل غير المضبوط يُعرض رفضاً مُسمّى، ولا يُعرض صفراً ولا كسراً", async () => {
    withCompany();
    const transport = stubTransport(
      { [UNITS_URL]: UNITS, [CONVERSIONS_URL]: CONVERSIONS },
      {
        [TRIAL_URL]: problem(
          "inventory.unit_conversion_not_exact",
          "تحويل المقدار 7 بالمعامل 1/12 لا يقع بلا باقٍ، فالناتج كسرٌ يُقرَّب.",
          "Converting magnitude 7 by factor 1/12 does not divide exactly."
        ),
      }
    );
    render(
      <Wrap transport={transport}>
        <InventoryUnitsScreen />
      </Wrap>
    );
    await screen.findByTestId("units-table");

    const probe = screen.getByTestId("conversion-probe");
    fireEvent.change(within(probe).getByTestId("probe-magnitude"), { target: { value: "7" } });
    fireEvent.change(within(probe).getByTestId("probe-from"), { target: { value: "PCS" } });
    fireEvent.change(within(probe).getByTestId("probe-to"), { target: { value: "KG" } });

    const run = screen.getByTestId<HTMLButtonElement>("probe-run");
    await waitFor(() => expect(run.disabled).toBe(false));
    fireEvent.click(run);

    const refusal = await screen.findByTestId("probe-refused");
    expect(refusal.textContent ?? "").toContain("رُفض");
    /* ‏**لا جواب رقمي على الإطلاق**: لا لوح جواب، ولا معامل، ولا كمّية خارجة.
       والشرح تحته يذكر «0.583333» بوصفه **ما لا يُعرض** — فالفحص على الجواب
       نفسه لا على نصّ الصفحة كلّه. */
    expect(screen.queryByTestId("probe-answer")).toBeNull();
    expect(screen.queryByTestId("probe-factor")).toBeNull();
    expect(screen.queryByTestId("probe-to-value")).toBeNull();

    const panel = await screen.findByTestId("problem-panel");
    expect(within(panel).getByTestId("problem-code").textContent).toBe(
      "inventory.unit_conversion_not_exact"
    );
    const next = await screen.findByTestId("inventory-next-step");
    expect(next.getAttribute("data-code")).toBe("inventory.unit_conversion_not_exact");
  });

  it("خلطُ صنفَي كمّية رفضٌ مُسمّى — ويُقال قبل الإرسال أيضاً", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [UNITS_URL]: UNITS, [CONVERSIONS_URL]: CONVERSIONS })}>
        <InventoryUnitsScreen />
      </Wrap>
    );
    await screen.findByTestId("units-table");

    const form = screen.getByTestId("conversion-form");
    fireEvent.change(within(form).getByTestId("conversion-from"), { target: { value: "PCS" } });
    fireEvent.change(within(form).getByTestId("conversion-to"), { target: { value: "KG" } });

    const warning = await screen.findByTestId("conversion-class-warning");
    expect(warning.textContent ?? "").toContain("كثافة");
    /* والزرّ مُقفَل: لا يُرسَل ما يُعرف رفضه. */
    const submit = screen.getByTestId<HTMLButtonElement>("conversion-submit");
    expect(submit.disabled).toBe(true);
  });

  it("جوابُ التحويل المضبوط يُعرض كمّيتين ومعاملاً، بلا حسابٍ في المتصفّح", async () => {
    withCompany();
    const answer = {
      from: { magnitude: "12.000000", unit: "PCS" },
      to: { magnitude: "1.000000", unit: "CTN" },
      numerator: 1,
      denominator: 12,
      quantityClass: "COUNT",
    };
    const transport: Transport = ({ method, url }) => {
      const key = method + " " + url.split("?")[0];
      const routes: Routes = {
        [UNITS_URL]: UNITS,
        [CONVERSIONS_URL]: CONVERSIONS,
        [TRIAL_URL]: answer,
      };
      const body = key in routes ? routes[key] : null;
      return Promise.resolve({ ok: body !== null, status: body !== null ? 200 : 404, json: body, url });
    };
    render(
      <Wrap transport={transport}>
        <InventoryUnitsScreen />
      </Wrap>
    );
    await screen.findByTestId("units-table");
    const probe = screen.getByTestId("conversion-probe");
    fireEvent.change(within(probe).getByTestId("probe-magnitude"), { target: { value: "12" } });
    fireEvent.change(within(probe).getByTestId("probe-from"), { target: { value: "PCS" } });
    fireEvent.change(within(probe).getByTestId("probe-to"), { target: { value: "KG" } });
    fireEvent.click(screen.getByTestId("probe-run"));

    const box = await screen.findByTestId("probe-answer");
    /* «1.000000» تُعرض «1» — قصُّ أصفارٍ نصّي لا تقريب. والمقياس من الأوّليّة. */
    expect(within(box).getByTestId("probe-to-value").textContent ?? "").toContain("CTN");
    expect(within(box).getByTestId("probe-factor").textContent).toBe("1/12");
  });
});

/* ═══════════════════════════ ٣ · النقل لا يكتب قيداً، ولا يُوهم به */

describe("النقل بين موقعين — لا أثر محاسبي", () => {
  it("جدول النقل بلا عمود قيدٍ وبلا شارة ترحيل، والشاشة تقول ذلك صراحةً", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [TRANSFERS_URL]: TRANSFERS,
          [ITEMS_URL]: ITEMS,
          [WAREHOUSES_URL]: WAREHOUSES,
        })}
      >
        <InventoryTransfersScreen />
      </Wrap>
    );

    await screen.findByTestId("transfers-table");
    const table = screen.getByTestId("transfers-table");
    const headers = [...table.querySelectorAll("th")].map((th) => th.textContent ?? "");
    expect(headers.some((h) => h.includes("القيد"))).toBe(false);
    expect(table.textContent ?? "").not.toContain("مُرحَّل");

    const claim = screen.getByTestId("transfer-no-entry").textContent ?? "";
    expect(claim).toContain("لا أثر محاسبي");
  });

  it("الكمّية تُعرض من نصّها بلا عائم — والقيمة التي يفسدها Number تصل صحيحة", async () => {
    withCompany();
    render(
      <Wrap
        transport={stubTransport({
          [TRANSFERS_URL]: TRANSFERS,
          [ITEMS_URL]: ITEMS,
          [WAREHOUSES_URL]: WAREHOUSES,
        })}
      >
        <InventoryTransfersScreen />
      </Wrap>
    );
    await screen.findByTestId("transfers-table");
    const qty = first(screen.getAllByTestId("transfer-quantity"));
    /* ‏9007199254740993.5 لا يُمثَّل في عائم مزدوج: لو مرّ بـNumber لصار
       …992 أو …994. والنصّ الأصلي كاملٌ في العنوان. */
    const title = qty.querySelector("[title]")?.getAttribute("title");
    expect(title).toBe("9007199254740993.500000");
  });

  it("التنفيذ الثاني بالهوية نفسها يُقال «كان مُنفَّذاً» ولا يُقال «نُفِّذ»", async () => {
    withCompany();
    const already = { ...DRAFT_TRANSFER, state: "MOVED", alreadyMoved: true, value: "144.0000" };
    const transport: Transport = ({ method, url }) => {
      const key = method + " " + url.split("?")[0];
      const routes: Routes = {
        [TRANSFERS_URL]: TRANSFERS,
        [ITEMS_URL]: ITEMS,
        [WAREHOUSES_URL]: WAREHOUSES,
        [MOVE_URL]: already,
      };
      const body = key in routes ? routes[key] : null;
      return Promise.resolve({ ok: body !== null, status: body !== null ? 200 : 404, json: body, url });
    };
    render(
      <Wrap transport={transport}>
        <InventoryTransfersScreen />
      </Wrap>
    );
    await screen.findByTestId("transfers-table");
    fireEvent.click(first(screen.getAllByTestId("transfer-move")));

    const banner = await screen.findByTestId("transfer-moved");
    expect(banner.getAttribute("data-already")).toBe("true");
    expect(banner.textContent ?? "").toContain("سلفاً");
  });

  it("رفضُ النقل بكمّيةٍ تتجاوز الرصيد يُسمّى بخطوته التالية", async () => {
    withCompany();
    const transport = stubTransport(
      { [TRANSFERS_URL]: TRANSFERS, [ITEMS_URL]: ITEMS, [WAREHOUSES_URL]: WAREHOUSES },
      {
        [MOVE_URL]: problem(
          "inventory.transfer_exceeds_balance",
          "النقل 20 PCS يتجاوز رصيد الصنف «ITM-001» في «WH-RIYADH/A-01-3» وهو 12 PCS.",
          "Transferring 20 PCS exceeds the balance."
        ),
      }
    );
    render(
      <Wrap transport={transport}>
        <InventoryTransfersScreen />
      </Wrap>
    );
    await screen.findByTestId("transfers-table");
    fireEvent.click(first(screen.getAllByTestId("transfer-move")));

    const panel = await screen.findByTestId("problem-panel");
    expect(within(panel).getByTestId("problem-code").textContent).toBe(
      "inventory.transfer_exceeds_balance"
    );
    const next = await screen.findByTestId("inventory-next-step");
    expect(next.getAttribute("data-code")).toBe("inventory.transfer_exceeds_balance");
  });
});

/* ═══════════════════════════ ٤ · الأرصدة بالتسكين: الرمز غير المسجَّل */

describe("الأرصدة بالتسكين — الرمز غير المسجَّل يُوسَم ولا يُحذف", () => {
  it("الصفّ غير المسجَّل يبقى في القائمة ويحمل شارته", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [PLACEMENT_BALANCES_URL]: PLACEMENT_BALANCES })}>
        <InventoryPlacementBalancesScreen />
      </Wrap>
    );
    await screen.findByTestId("placement-balances-panel");

    /* المستودعان اثنان: المسجَّل والشبح — والثاني لم يُحذف. */
    expect(screen.getAllByTestId("pb-warehouse-group")).toHaveLength(2);
    expect(screen.getAllByTestId("warehouse-unregistered").length).toBeGreaterThan(0);
    expect(screen.getByTestId("unregistered-legend")).toBeTruthy();
  });

  it("«لا أساس تكلفة» كلمةٌ وشَرطة، لا صفرٌ يُقرأ رقماً", async () => {
    withCompany();
    render(
      <Wrap transport={stubTransport({ [PLACEMENT_BALANCES_URL]: PLACEMENT_BALANCES })}>
        <InventoryPlacementBalancesScreen />
      </Wrap>
    );
    await screen.findByTestId("placement-balances-panel");
    const cell = screen.getByTestId("pb-no-basis-cell");
    expect(cell.textContent).toBe("—");
    expect(screen.getByTestId("pb-flag-no-basis")).toBeTruthy();
    expect(screen.getByTestId("pb-flag-negative")).toBeTruthy();
  });
});

/* ═══════════════════════════ ٥ · قواعد الملفّات نفسها */

describe("قواعد الشيفرة — تُفحص على النصّ لأن لا اختبارَ يراها", () => {
  it("لا Number ولا parseFloat على مقدارٍ في الشاشات الخمس", () => {
    const offenders: string[] = [];
    for (const file of NEW_SCREENS) {
      const text = read(file).replace(/\/\*[\s\S]*?\*\//g, " ");
      for (const line of text.split("\n")) {
        if (/parseFloat|parseInt/.test(line)) offenders.push(file + " ← " + line.trim());
        /* ‏`Number(` مسموحٌ **على البسط والمقام وحدهما** — عددان صحيحان
           ينشرهما العقد `integer` بحدٍّ يقع كاملاً داخل المدى الدقيق. وما
           عداهما ممنوع. */
        if (/\bNumber\(/.test(line) && !/numerator|denominator/.test(line)) {
          offenders.push(file + " ← " + line.trim());
        }
      }
    }
    expect(offenders).toEqual([]);
  });

  it("لا خاصية اتجاه فيزيائية في أنماط القسم", () => {
    const css = read("screens/inventory/inventory.css").replace(/\/\*[\s\S]*?\*\//g, " ");
    expect(css).not.toMatch(/(margin|padding|border|inset|float|clear|text-align)-(left|right)\s*:/);
  });

  it("لا tabular-nums حرفيةً في أنماط القسم — الرمز وحده", () => {
    const css = read("screens/inventory/inventory.css");
    expect(css).not.toMatch(/font-variant-numeric\s*:\s*tabular-nums/);
  });

  it("الرموز الجديدة في خريطة الخطوة التالية كلُّها مفاتيح مترجَمة في اللغات الأربع", () => {
    const i18n = createI18n();
    const added = [
      "inventory.storage_place_still_holds_stock",
      "inventory.storage_place_has_active_children",
      "inventory.duplicate_storage_place_code",
      "inventory.storage_place_parent_inactive",
      "inventory.duplicate_transfer_number",
      "inventory.transfer_to_same_place",
      "inventory.transfer_exceeds_balance",
      "inventory.unit_class_mismatch",
      "inventory.no_conversion_between_units",
      "inventory.unit_conversion_overflow",
    ];
    for (const code of added) expect(INVENTORY_NEXT_STEP[code]).toBeTruthy();
    for (const locale of CODES) {
      i18n.use(locale);
      for (const code of added) {
        const key = INVENTORY_NEXT_STEP[code] ?? "";
        expect(key).not.toBe("");
        expect(i18n.t(key), locale + " ← " + key).not.toBe(key);
      }
    }
  });

  it("الشاشات الخمس مسجّلةٌ في عقد الملاحة بمساراتها", () => {
    const paths = SCREENS.filter((s) => s.section === "inventory").map((s) => s.path);
    for (const path of [
      "/inventory/warehouses",
      "/inventory/placement",
      "/inventory/placement-balances",
      "/inventory/transfers",
      "/inventory/units",
    ]) {
      expect(paths, "لا صفّ في SCREENS للمسار " + path).toContain(path);
    }
  });

  /*
   * حارسُ ADR-جديد · every-field-in-a-row-carries-a-description.
   * حقلٌ بلا وصفٍ يقف في صفٍّ لجيرانه أوصاف **لا يُخرج صندوق وصفٍ أصلاً**، فيقف قاعُ
   * حبره عند أسفل عنصر تحكّمه — أعلى من جيرانه بارتفاع مسار الوصف كلّه. مقيسٌ 61.17px
   * في صفّ «تسجيل وحدة قياس» قبل الإصلاح، وصفرٌ بعده.
   */
  it("كل حقلٍ في الشاشات الخمس يحمل وصفاً — وإلّا سُنّن قاعُ صفّه", () => {
    const offenders: string[] = [];
    for (const file of NEW_SCREENS) {
      const text = read(file).replace(/\/\*[\s\S]*?\*\//g, " ");
      /* كل وسمٍ من `<Field` إلى أول `>` يغلقه — والفحص على نصّ الوسم وحده. */
      for (const m of text.matchAll(/<Field\b[\s\S]*?>/g)) {
        const tag = m[0];
        if (/\bhint=/.test(tag) || /\berror=/.test(tag)) continue;
        offenders.push(file + " ← " + tag.replace(/\s+/g, " ").slice(0, 70));
      }
    }
    expect(offenders).toEqual([]);
  });

  it("حارسُ لافراغ: الشاشات الخمس تحوي حقولاً أصلاً", () => {
    let fields = 0;
    for (const file of NEW_SCREENS) {
      fields += [...read(file).matchAll(/<Field\b/g)].length;
    }
    /* العدد مقيس: 22 حقلاً في الشاشات الخمس. وحارسٌ لا يمسح شيئاً يمرّ دائماً. */
    expect(fields).toBeGreaterThanOrEqual(20);
  });

  it("كل حقلٍ في الشاشات الخمس له خانةُ وصفٍ واحدة — الصفّ يملك المسارات", () => {
    /* حقلٌ بابنَين مباشرَين من صنف الوصف يضع ساكنَين في مسارٍ واحد فيتراكبان
       (‏ADR-0067). والأوّليّة `Field` تجمعهما في `field__desc` واحد؛ وهذا
       الحارس يمنع العودة إلى `.field` مكتوبٍ بيد فيه `.hint` و`.field-error`
       أخوَين. */
    const offenders: string[] = [];
    for (const file of NEW_SCREENS) {
      const text = read(file).replace(/\/\*[\s\S]*?\*\//g, " ");
      if (/className="field"/.test(text)) offenders.push(file);
    }
    expect(offenders).toEqual([]);
  });
});
