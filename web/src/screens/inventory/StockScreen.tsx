/* ═══════════════════════════════════════════════════════════════════════════
   الأرصدة والتسكين — أين يقع الصنف فعلاً في المستودع
   Balances and bin placement — where an item actually sits in the warehouse
   ───────────────────────────────────────────────────────────────────────────
   هذه الشاشة تحمل القدرة المميِّزة الثانية للقسم: **تسكين القطع**. ومفتاح
   الرصيد في العقد أربعة أبعاد — المنشأة والصنف والمستودع **والموقع** — فالموقع
   بُعدٌ في المفتاح لا وصفٌ على الصفّ، وهذه الشاشة تعرضه كذلك: شجرةً من
   مستودعٍ إلى مواقعه إلى أصنافه، لا عموداً خامساً في جدولٍ مسطّح.

   وثلاثة أشياء **تُعلَن ولا تُخفى**، ولكلٍّ منها أثرٌ يمنع شيئاً:

     · `DEFAULT` — مستودعٌ لم يُسكَّن بعد. الرصيد قائم وموضعه غير معلوم،
       والعقد يسمّي هذه القيمة صراحةً. فهي **قرارٌ مُعلَن** لا نقصُ بيانات.

     · `hasCostBasis = false` — ولذلك **لا تُعرض تكلفة الوحدة صفراً هنا**:
       الصفر رقم، وغياب الأساس ليس رقماً. والعقد جعل الحقلين منفصلين عمداً
       لهذا بالضبط. وأي صرفٍ من هذا الموقع يُرفض بـ`inventory.no_cost_basis`.

     · الكمّية السالبة — بيعٌ قبل إدخال الاستلام، وهو واقعةٌ يومية في منشأة
       عاملة. تُوسَم ولا تُمنع، **لكنها تمنع إقفال الفترة**.

   **ولا مجموع قيمةٍ يُحسب هنا.** جمع المال قرارٌ عشري يقع في الاستعلام لا في
   المتصفّح (`api/money.ts` يمنعه بالسلوك لا بالتعليق)، والمجموع الموثوق يُقرأ
   من شاشة التقييم والمطابقة حيث يحسبه الخادم بثلاث طرق. وعرضُ مجموعٍ محسوبٍ
   هنا كان سيصير رقماً رابعاً لا يطابق الثلاثة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { listItems, readStockBalances } from "../../api/generated/client";
import type { StockBalance } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, Rendered, useLocale, useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import {
  Button,
  EmptyState,
  Panel,
  QuantityValue,
  StatCard,
  magnitudeIsNegative,
} from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, SurfaceGap } from "./shared";

/** القيمة التي يعرفها العقد لمستودعٍ لم يُسكَّن بعد. */
const UNBINNED = "DEFAULT";

/* ═══════════════════════════════════════════════ تكلفة الوحدة معروضةً
   مقياسها **ستٌّ لا أربع** بنصّ العقد: صنفٌ يُشترى بألف حبّة بمئة ريال تكلفة
   وحدته 0.100000، وبمقياس أربعة تصير 0.1000 فيتراكم الفرق على كل صرف. وهي
   ليست `Money` على السلك — لها مخطّطها — فتمرّ بطبقة التدويل بمقياسها هي.
   ولا تُمرَّر على `Number` في أي خطوة. */
function UnitCostText(props: { value: string }): ReactNode {
  const { i18n, locale } = useLocale();
  const { value } = props;
  const display = useMemo(() => {
    void locale;
    return i18n.amount(value, { scale: 6 });
  }, [i18n, locale, value]);
  return <Rendered display={display} className="mono" title={value} />;
}

/* ════════════════════════════════════════════════════ أعلامُ الرصيد */

/**
 * ما يجب أن يُرى على رصيدٍ ما: كلٌّ منها يمنع شيئاً لا يزيّن.
 * و`showUnbinned` تُطفأ داخل عرض التسكين: الموقع هناك **عنوانٌ فوق الجدول**،
 * فتكرارُ الشارة في كل صفٍّ تحته ضجيجٌ يُدرَّب المستخدم على تجاهله.
 */
function BalanceFlags(props: { balance: StockBalance; showUnbinned?: boolean }): ReactNode {
  const { t } = useT();
  const { balance } = props;
  const unbinned = props.showUnbinned !== false && balance.locationId === UNBINNED;
  const negative = magnitudeIsNegative(balance.quantity.magnitude);
  if (!unbinned && balance.hasCostBasis && !negative) return null;
  return (
    <div className="inv-flags">
      {unbinned ? (
        <span className="pill pill--pending" title={t("inventory.stock.unbinnedWhy")}>
          {t("inventory.stock.unbinned")}
        </span>
      ) : null}
      {!balance.hasCostBasis ? (
        <span
          className="pill pill--rejected"
          data-testid="flag-no-basis"
          title={t("inventory.stock.noBasisWhy")}
        >
          {t("inventory.stock.noBasis")}
        </span>
      ) : null}
      {negative ? (
        <span
          className="pill pill--rejected"
          data-testid="flag-negative"
          title={t("inventory.stock.negativeWhy")}
        >
          {t("inventory.stock.negative")}
        </span>
      ) : null}
    </div>
  );
}

/* ═════════════════════════════════════════════════ جدول أرصدةٍ واحد */

/**
 * جدول أرصدة. يُستعمل مسطّحاً في عرض الصنف، وداخل كل موقعٍ في عرض التسكين.
 * @param props الأرصدة وهل يظهر عمودا المستودع والموقع.
 */
function BalanceTable(props: {
  readonly rows: readonly StockBalance[];
  readonly withPlace: boolean;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  return (
    <div className="ledger" data-state="ready" data-testid={props.testId}>
      <table>
        <caption className="visually-hidden">{t("inventory.stock.title")}</caption>
        <thead>
          <tr>
            <th scope="col">{t("inventory.stock.colItem")}</th>
            {props.withPlace ? (
              <>
                <th scope="col">{t("inventory.stock.colWarehouse")}</th>
                <th scope="col">{t("inventory.stock.colLocation")}</th>
              </>
            ) : null}
            <th scope="col" className="n">{t("inventory.stock.colQuantity")}</th>
            <th scope="col" className="n">{t("inventory.stock.colUnitCost")}</th>
            <th scope="col" className="n">{t("inventory.stock.colValue")}</th>
            <th scope="col">{t("inventory.stock.colFlags")}</th>
          </tr>
        </thead>
        <tbody>
          {props.rows.map((balance) => (
            <tr
              key={balance.itemId + "|" + balance.warehouseId + "|" + balance.locationId}
              data-testid="balance-row"
            >
              <td className="code">{balance.itemId}</td>
              {props.withPlace ? (
                <>
                  <td className="code">{balance.warehouseId}</td>
                  <td className="code">{balance.locationId}</td>
                </>
              ) : null}
              <td className="n">
                <QuantityValue
                  magnitude={balance.quantity.magnitude}
                  unit={balance.quantity.unit}
                />
              </td>
              <td className="n">
                {balance.hasCostBasis ? (
                  <UnitCostText value={balance.unitCost} />
                ) : (
                  <span className="inv-nobasis" data-testid="cell-no-basis">
                    {t("inventory.stock.noBasis")}
                  </span>
                )}
              </td>
              <td className="n">
                <Amount value={balance.value} />
              </td>
              <td>
                <BalanceFlags balance={balance} showUnbinned={props.withPlace} />
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ══════════════════════════════════════ عرض التسكين: مستودعٌ ثم مواقعه */

/** مستودعٌ ومواقعه وأرصدة كل موقع. */
interface Warehouse {
  readonly id: string;
  readonly locations: readonly { readonly id: string; readonly rows: readonly StockBalance[] }[];
}

/** يجمع الأرصدة في شجرة مستودعٍ ← موقع، محافظاً على ترتيب الخادم. */
function groupByPlace(balances: readonly StockBalance[]): readonly Warehouse[] {
  const order: string[] = [];
  const byWarehouse = new Map<string, Map<string, StockBalance[]>>();
  for (const balance of balances) {
    let locations = byWarehouse.get(balance.warehouseId);
    if (!locations) {
      locations = new Map<string, StockBalance[]>();
      byWarehouse.set(balance.warehouseId, locations);
      order.push(balance.warehouseId);
    }
    const rows = locations.get(balance.locationId);
    if (rows) rows.push(balance);
    else locations.set(balance.locationId, [balance]);
  }
  return order.map((id) => ({
    id,
    locations: [...(byWarehouse.get(id) ?? new Map<string, StockBalance[]>())].map(
      ([locationId, rows]) => ({ id: locationId, rows })
    ),
  }));
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة الأرصدة والتسكين. */
export function InventoryStockScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [view, setView] = useState<"place" | "item">("place");
  const [item, setItem] = useState("");

  const balancesQuery = useQuery({
    queryKey: ["inventory-balances", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readStockBalances(transport, { companyId: config.companyId }, signal),
  });

  /* الكتالوج يُقرأ ليُختار الصنف باسمه لا برمزه وحده — ورفضُ هذه القراءة
     لا يُسقط الشاشة: الأرصدة تُعرض، والمُنتقي وحده يبقى بالرموز. */
  const itemsQuery = useQuery({
    queryKey: ["inventory-items", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listItems(transport, { companyId: config.companyId }, signal),
  });

  const balances: readonly StockBalance[] = useMemo(
    () => balancesQuery.data?.balances ?? [],
    [balancesQuery.data]
  );

  const shown = useMemo(
    () => (item === "" ? balances : balances.filter((b) => b.itemId === item)),
    [balances, item]
  );

  const places = useMemo(() => groupByPlace(shown), [shown]);

  const counts = useMemo(() => {
    const warehouses = new Set<string>();
    const locations = new Set<string>();
    let noBasis = 0;
    let negative = 0;
    let unbinned = 0;
    for (const balance of balances) {
      warehouses.add(balance.warehouseId);
      locations.add(balance.warehouseId + "|" + balance.locationId);
      if (!balance.hasCostBasis) noBasis += 1;
      if (balance.locationId === UNBINNED) unbinned += 1;
      if (magnitudeIsNegative(balance.quantity.magnitude)) negative += 1;
    }
    return { warehouses: warehouses.size, locations: locations.size, noBasis, negative, unbinned };
  }, [balances]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="inventory-stock-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.stock.title")}</h1>
          <p className="sub">{t("inventory.stock.lede")}</p>
        </div>
      </header>

      {balancesQuery.data ? (
        <div className="stats-row" data-testid="stock-stats">
          <StatCard
            label={t("inventory.stock.statBalances")}
            count={balancesQuery.data.balanceCount}
            testId="stat-balances"
          />
          <StatCard label={t("inventory.stock.statWarehouses")} count={counts.warehouses} />
          <StatCard label={t("inventory.stock.statLocations")} count={counts.locations} />
          <StatCard
            label={t("inventory.stock.statNoBasis")}
            count={counts.noBasis}
            tone={counts.noBasis > 0 ? "bad" : "good"}
            testId="stat-no-basis"
          />
          <StatCard
            label={t("inventory.stock.statNegative")}
            count={counts.negative}
            tone={counts.negative > 0 ? "bad" : "good"}
            testId="stat-negative"
          />
        </div>
      ) : null}

      {/* شرحُ الأعلام **لما وقع منها فقط**: ثلاث فقرات دائمة تُقرأ مرّةً ثم
          تُتخطّى، وفقرةٌ تظهر لأن حالتها قائمة تُقرأ في كل مرّة. */}
      {balancesQuery.data && (counts.unbinned > 0 || counts.noBasis > 0 || counts.negative > 0) ? (
        <div className="card card-pad stack" data-testid="stock-legend">
          {counts.unbinned > 0 ? (
            <p className="muted">
              <span className="pill pill--pending">{t("inventory.stock.unbinned")}</span>{" "}
              {t("inventory.stock.unbinnedWhy")}
            </p>
          ) : null}
          {counts.noBasis > 0 ? (
            <p className="muted">
              <span className="pill pill--rejected">{t("inventory.stock.noBasis")}</span>{" "}
              {t("inventory.stock.noBasisWhy")}
            </p>
          ) : null}
          {counts.negative > 0 ? (
            <p className="muted">
              <span className="pill pill--rejected">{t("inventory.stock.negative")}</span>{" "}
              {t("inventory.stock.negativeWhy")}
            </p>
          ) : null}
        </div>
      ) : null}

      <div className="filterbar" role="search">
        <div className="field wide">
          <label htmlFor="inv-stock-item">{t("inventory.stock.pickItem")}</label>
          <select
            id="inv-stock-item"
            className="ctl mono"
            data-testid="stock-item-pick"
            value={item}
            onChange={(e) => setItem(e.target.value)}
          >
            <option value="">{t("inventory.stock.pickNone")}</option>
            {(itemsQuery.data?.items ?? []).map((one) => (
              <option key={one.id} value={one.code}>
                {one.code + " — " + one.name.ar}
              </option>
            ))}
          </select>
          <span className="hint">{t("inventory.stock.pickItemHint")}</span>
        </div>
        <div className="inline-group" role="group" aria-label={t("common.label.type")}>
          <button
            type="button"
            className={"btn" + (view === "place" ? " btn-primary" : "")}
            aria-pressed={view === "place"}
            data-testid="view-place"
            onClick={() => setView("place")}
          >
            {t("inventory.stock.byLocation")}
          </button>
          <button
            type="button"
            className={"btn" + (view === "item" ? " btn-primary" : "")}
            aria-pressed={view === "item"}
            data-testid="view-item"
            onClick={() => setView("item")}
          >
            {t("inventory.stock.byItem")}
          </button>
          <Button
            label={t("common.action.refresh")}
            onClick={() => void balancesQuery.refetch()}
            testId="stock-reload"
          />
        </div>
      </div>

      {balancesQuery.isPending && balancesQuery.fetchStatus === "fetching" ? (
        <ReadingSkeleton />
      ) : null}
      {balancesQuery.isError ? (
        <ProblemPanel error={balancesQuery.error} onRetry={() => void balancesQuery.refetch()} />
      ) : null}

      {balancesQuery.data && balances.length === 0 ? (
        <EmptyState
          title={t("inventory.stock.emptyTitle")}
          body={t("inventory.stock.emptyBody")}
          testId="stock-empty"
        />
      ) : null}

      {balancesQuery.data && balances.length > 0 && shown.length === 0 ? (
        <EmptyState
          small
          title={t("inventory.stock.placementEmpty")}
          body={t("inventory.stock.placementEmptyBody")}
          action={<Button label={t("inventory.stock.pickNone")} onClick={() => setItem("")} />}
          testId="stock-placement-empty"
        />
      ) : null}

      {shown.length > 0 && view === "place" ? (
        <Panel
          title={item === "" ? t("inventory.stock.byLocation") : t("inventory.stock.placementTitle")}
          note={t("inventory.stock.pickItemHint")}
          testId="stock-by-place"
        >
          {places.map((warehouse) => (
            <div className="inv-wh" key={warehouse.id} data-testid="warehouse-group">
              <div className="inv-wh__hd">
                <span className="muted">{t("inventory.stock.colWarehouse")}</span>
                <span className="inv-wh__name">{warehouse.id}</span>
              </div>
              {warehouse.locations.map((location) => (
                <div
                  className="inv-loc"
                  key={location.id}
                  data-testid="location-group"
                  data-unbinned={location.id === UNBINNED ? "true" : "false"}
                >
                  <div className="inv-loc__hd">
                    <span className="muted">{t("inventory.stock.colLocation")}</span>
                    <span className="inv-loc__name">{location.id}</span>
                    {location.id === UNBINNED ? (
                      <span className="pill pill--pending">{t("inventory.stock.unbinned")}</span>
                    ) : null}
                  </div>
                  <BalanceTable rows={location.rows} withPlace={false} />
                </div>
              ))}
            </div>
          ))}
        </Panel>
      ) : null}

      {shown.length > 0 && view === "item" ? (
        <Panel
          title={t("inventory.stock.byItem")}
          note={t("inventory.stock.lede")}
          testId="stock-by-item"
        >
          <BalanceTable rows={shown} withPlace testId="stock-flat-table" />
        </Panel>
      ) : null}

      {balancesQuery.data ? <p className="muted">{t("inventory.stock.noTotal")}</p> : null}

      <SurfaceGap
        title={t("inventory.stock.moveGapTitle")}
        body={t("inventory.stock.moveGapBody")}
        owed={t("inventory.stock.moveGapNext")}
        testId="stock-move-gap"
      />
    </section>
  );
}
