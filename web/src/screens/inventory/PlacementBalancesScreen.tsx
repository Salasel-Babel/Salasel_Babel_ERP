/* ═══════════════════════════════════════════════════════════════════════════
   الأرصدة بالتسكين — الرصيد ومعه اسم موضعه، والرمز غير المسجَّل مُعلَناً
   Placement balances — a balance with its place's name, unregistered flagged
   ───────────────────────────────────────────────────────────────────────────
   شاشة الأرصدة القائمة تعرض **رموز** المواضع؛ وهذه تعرض الرصيد نفسه ومعه
   **أسماء** مواضعه من سجلّ التسكين. وأربعة قرارات تحكمها:

   ١ · **الرمز غير المسجَّل يخرج ويُوسَم، ولا يُحذف ولا يُخترَع له اسم.**
       العقد يحمل `warehouseRegistered` و`locationRegistered` صراحةً، ويُخرج
       الاسم مساوياً للرمز حين لا تسجيل. وحذفُ الصفّ كان سيجعل مجموع الأرصدة
       المقروءة **أقلّ من مجموعها الفعلي** — انحرافٌ لا يُظهره أي فحص توازن —
       واختراعُ اسمٍ كان سيجعل السجلّ يبدو أشمل ممّا هو. فالشاشة تعرض الاسم
       كما وصل، وتضع عليه شارةً تقول إنه الرمز نفسه لا اسمٌ مسجَّل.

   ٢ · **مستوى القراءة هو مستوى الرصيد: الموقع** (ADR-0070). ولا رفّ في هذه
       الشاشة ولا عمودٌ له: قائمةٌ تُرجع صفراً عن رفٍّ فيه بضاعة أسوأ من
       قائمةٍ لا تذكره.

   ٣ · **«لا أساس تكلفة» كلمةٌ لا صفر.** تُعرض بلون التحذير وشَرطةً بدل
       العدد: الصفر رقمٌ، وغياب الأساس ليس رقماً — والفرق بينهما هو الفرق
       بين رقمٍ صحيح ورقمٍ مخترَع. وأي صرفٍ من هذا الموضع يُرفض بـ
       `inventory.no_cost_basis`.

   ٤ · **ولا مجموع قيمةٍ يُحسب هنا** — كما في شاشة الأرصدة حرفاً بحرف: جمع
       المال قرارٌ عشري يقع في الاستعلام لا في المتصفّح، والمجموع الموثوق
       يُقرأ من شاشة التقييم والمطابقة حيث يحسبه الخادم بثلاث طرق.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readPlacementBalances } from "../../api/generated/client";
import type { PlacementBalance } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, Rendered, useLocale, useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import {
  Button,
  EmptyState,
  Field,
  Panel,
  QuantityValue,
  StatCard,
  magnitudeIsNegative,
} from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton } from "./shared";

/** القيمة التي يعرفها العقد لمستودعٍ لم يُسكَّن بعد. */
const UNBINNED = "DEFAULT";

/* ═══════════════════════════════════════════════ تكلفة الوحدة معروضةً
   مقياسها **ستٌّ لا أربع** بنصّ العقد، وهي ليست `Money` على السلك — لها
   مخطّطها — فتمرّ بطبقة التدويل بمقياسها هي، ولا تمرّ على `Number`. */
function UnitCostText(props: { value: string }): ReactNode {
  const { i18n, locale } = useLocale();
  const { value } = props;
  const display = useMemo(() => {
    void locale;
    return i18n.amount(value, { scale: 6 });
  }, [i18n, locale, value]);
  return <Rendered display={display} className="mono" title={value} />;
}

/** موقعٌ بأرصدته، داخل مستودع. */
interface LocationGroup {
  readonly id: string;
  readonly name: string;
  readonly registered: boolean;
  readonly rows: PlacementBalance[];
}

/** مستودعٌ بمواقعه. */
interface WarehouseGroup {
  readonly id: string;
  readonly name: string;
  readonly registered: boolean;
  readonly locations: LocationGroup[];
}

/**
 * يجمع الأرصدة مستودعاً ثم موقعاً — **بترتيب ورودها من الخادم**، فلا يُعاد
 * ترتيبٌ ثقافي في المتصفّح على معرّفاتٍ ترتيبُها حرفيٌّ ثابت في الاستعلام.
 * @param rows الأرصدة كما وصلت.
 */
function groupByPlace(rows: readonly PlacementBalance[]): WarehouseGroup[] {
  const out: WarehouseGroup[] = [];
  for (const row of rows) {
    let warehouse = out.find((one) => one.id === row.warehouseId);
    if (!warehouse) {
      warehouse = {
        id: row.warehouseId,
        name: row.warehouseName.ar,
        registered: row.warehouseRegistered,
        locations: [],
      };
      out.push(warehouse);
    }
    let location = warehouse.locations.find((one) => one.id === row.locationId);
    if (!location) {
      location = {
        id: row.locationId,
        name: row.locationName.ar,
        registered: row.locationRegistered,
        rows: [],
      };
      warehouse.locations.push(location);
    }
    location.rows.push(row);
  }
  return out;
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة الأرصدة بالتسكين. */
export function InventoryPlacementBalancesScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [query, setQuery] = useState("");

  const result = useQuery({
    queryKey: ["inventory-placement-balances", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) =>
      readPlacementBalances(transport, { companyId: config.companyId }, signal),
  });

  const balances: readonly PlacementBalance[] = useMemo(
    () => result.data?.balances ?? [],
    [result.data]
  );

  /* مرشّحٌ نصّي على الرمز والاسم — بلا إعادة ترتيب. */
  const shown = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    if (!needle) return balances;
    return balances.filter(
      (row) =>
        row.itemId.toLocaleLowerCase().includes(needle) ||
        row.warehouseId.toLocaleLowerCase().includes(needle) ||
        row.locationId.toLocaleLowerCase().includes(needle) ||
        row.warehouseName.ar.toLocaleLowerCase().includes(needle) ||
        row.locationName.ar.toLocaleLowerCase().includes(needle)
    );
  }, [balances, query]);

  const groups = useMemo(() => groupByPlace(shown), [shown]);

  const counts = useMemo(() => {
    let unregistered = 0;
    let noBasis = 0;
    let negative = 0;
    for (const row of balances) {
      if (!row.warehouseRegistered || !row.locationRegistered) unregistered += 1;
      if (!row.hasCostBasis) noBasis += 1;
      if (magnitudeIsNegative(row.quantity.magnitude)) negative += 1;
    }
    return { unregistered, noBasis, negative };
  }, [balances]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="inventory-placement-balances-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.balances.title")}</h1>
          <p className="sub">{t("inventory.balances.lede")}</p>
        </div>
      </header>

      {result.data ? (
        <div className="stats-row" data-testid="placement-balance-stats">
          <StatCard
            label={t("inventory.balances.statBalances")}
            count={result.data.balanceCount}
            testId="stat-placement-balances"
          />
          <StatCard
            label={t("inventory.balances.statUnregistered")}
            count={counts.unregistered}
            tone={counts.unregistered > 0 ? "bad" : "good"}
            hint={t("inventory.balances.unregisteredWhy")}
            testId="stat-unregistered"
          />
          <StatCard
            label={t("inventory.stock.statNoBasis")}
            count={counts.noBasis}
            tone={counts.noBasis > 0 ? "bad" : "good"}
            testId="stat-placement-no-basis"
          />
          <StatCard
            label={t("inventory.stock.statNegative")}
            count={counts.negative}
            tone={counts.negative > 0 ? "bad" : "good"}
            testId="stat-placement-negative"
          />
        </div>
      ) : null}

      <div className="filterbar" role="search">
        <Field
          id="pb-search"
          label={t("inventory.balances.search")}
          hint={t("inventory.balances.searchHint")}
        >
          <input
            id="pb-search"
            className="ctl"
            type="search"
            data-testid="placement-balances-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </Field>
        <div className="rowctl">
          <div className="inline-group">
            <Button
              label={t("common.action.refresh")}
              onClick={() => void result.refetch()}
              testId="placement-balances-reload"
            />
          </div>
        </div>
      </div>

      {result.isPending && result.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {result.isError ? (
        <ProblemPanel error={result.error} onRetry={() => void result.refetch()} />
      ) : null}

      {result.data && balances.length === 0 ? (
        <EmptyState
          title={t("inventory.balances.emptyTitle")}
          body={t("inventory.balances.emptyBody")}
          testId="placement-balances-empty"
        />
      ) : null}

      {result.data && balances.length > 0 && shown.length === 0 ? (
        <EmptyState
          small
          title={t("inventory.items.noMatchTitle")}
          body={t("inventory.items.noMatchBody")}
          action={<Button label={t("common.action.clearSearch")} onClick={() => setQuery("")} />}
          testId="placement-balances-no-match"
        />
      ) : null}

      {counts.unregistered > 0 ? (
        <p className="alert alert--warning" role="status" data-testid="unregistered-legend">
          {t("inventory.balances.unregisteredWhy")}
        </p>
      ) : null}

      {groups.length > 0 ? (
        <Panel
          title={t("inventory.balances.title")}
          note={t("inventory.balances.binLevel")}
          testId="placement-balances-panel"
        >
          {groups.map((warehouse) => (
            <div className="inv-wh" key={warehouse.id} data-testid="pb-warehouse-group">
              <div className="inv-wh__hd">
                <span className="muted">{t("inventory.stock.colWarehouse")}</span>
                <span className="inv-wh__name">{warehouse.id}</span>
                <span lang="ar" dir="rtl">{warehouse.name}</span>
                {!warehouse.registered ? (
                  <span
                    className="pill pill--pending"
                    data-testid="warehouse-unregistered"
                    title={t("inventory.balances.unregisteredWhy")}
                  >
                    {t("inventory.balances.unregistered")}
                  </span>
                ) : null}
              </div>
              {warehouse.locations.map((location) => (
                <div
                  className="inv-loc"
                  key={location.id}
                  data-testid="pb-location-group"
                  data-unbinned={location.id === UNBINNED ? "true" : "false"}
                >
                  <div className="inv-loc__hd">
                    <span className="muted">{t("inventory.stock.colLocation")}</span>
                    <span className="inv-loc__name">{location.id}</span>
                    <span lang="ar" dir="rtl">{location.name}</span>
                    {location.id === UNBINNED ? (
                      <span className="pill pill--pending">{t("inventory.stock.unbinned")}</span>
                    ) : null}
                    {!location.registered ? (
                      <span
                        className="pill pill--pending"
                        data-testid="location-unregistered"
                        title={t("inventory.balances.unregisteredWhy")}
                      >
                        {t("inventory.balances.unregistered")}
                      </span>
                    ) : null}
                  </div>
                  <div className="ledger" data-state="ready" data-testid="pb-table">
                    <table>
                      <caption className="visually-hidden">
                        {t("inventory.balances.title")}
                      </caption>
                      <thead>
                        <tr>
                          <th scope="col">{t("inventory.stock.colItem")}</th>
                          <th scope="col" className="n">{t("inventory.stock.colQuantity")}</th>
                          <th scope="col" className="n">{t("inventory.stock.colUnitCost")}</th>
                          <th scope="col" className="n">{t("inventory.stock.colValue")}</th>
                          <th scope="col">{t("inventory.stock.colFlags")}</th>
                        </tr>
                      </thead>
                      <tbody>
                        {location.rows.map((row) => (
                          <tr key={row.itemId} data-testid="pb-row">
                            <td className="code">{row.itemId}</td>
                            <td className="n">
                              <QuantityValue
                                magnitude={row.quantity.magnitude}
                                unit={row.quantity.unit}
                                testId="pb-quantity"
                              />
                            </td>
                            <td className="n">
                              {row.hasCostBasis ? (
                                <UnitCostText value={row.unitCost} />
                              ) : (
                                <span className="inv-nobasis" data-testid="pb-no-basis-cell">
                                  {t("common.label.dash")}
                                </span>
                              )}
                            </td>
                            <td className="n">
                              <Amount value={row.value} />
                            </td>
                            <td>
                              <div className="inv-flags">
                                {!row.hasCostBasis ? (
                                  <span
                                    className="pill pill--rejected"
                                    data-testid="pb-flag-no-basis"
                                    title={t("inventory.stock.noBasisWhy")}
                                  >
                                    {t("inventory.stock.noBasis")}
                                  </span>
                                ) : null}
                                {magnitudeIsNegative(row.quantity.magnitude) ? (
                                  <span
                                    className="pill pill--rejected"
                                    data-testid="pb-flag-negative"
                                    title={t("inventory.stock.negativeWhy")}
                                  >
                                    {t("inventory.stock.negative")}
                                  </span>
                                ) : null}
                              </div>
                            </td>
                          </tr>
                        ))}
                      </tbody>
                    </table>
                  </div>
                </div>
              ))}
            </div>
          ))}
          <p className="muted">{t("inventory.stock.noTotal")}</p>
        </Panel>
      ) : null}
    </section>
  );
}
