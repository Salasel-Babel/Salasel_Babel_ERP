/* ═══════════════════════════════════════════════════════════════════════════
   النقل بين موقعين — حركةُ مكانٍ لا حركةُ قيمة
   Transfer between two locations — a movement of place, not of value
   ───────────────────────────────────────────────────────────────────────────
   خمسة قرارات تحكم هذه الشاشة، وكلّها مقروءة من العقد لا مفترَضة:

   ١ · **لا أثر محاسبي يُعرض، لأنه لا يوجد** (ADR-0071). النقل داخل المنشأة
       نفسها لا يغيّر قيمة المخزون: الصنف واحدٌ على الطرفين فمجموعته واحدة،
       ومؤهّل دور `inventory_control` في المصفوفة هو مجموعة الصنف — فالحساب
       الذي كان سيصير مديناً هو الذي كان سيصير دائناً بالمبلغ نفسه. فلا عمود
       «معرّف القيد» في هذا الجدول ولا شارة «مُرحَّل»، **والمورد الفرعي
       `movement` لا `posting`** لأن «الترحيل» في هذا العقد يعني «صار له قيد».
       وشاشةٌ تعرض عموداً فارغاً اسمه «القيد» تُعلّم قارئها أن القيد ناقص.

   ٢ · **ولا حقل تكلفة في النموذج**، وهو مقصود في العقد: المنقول يخرج بتكلفة
       مصدره المتحرّكة **لحظة النقل** وتحسبها وحدة المخزون. وحقلٌ هنا كان
       سيسمح بنقلٍ «يُعيد تسعير» البضاعة وهو ينقلها.

   ٣ · **المواضع تُختار ولا تُكتب.** `draftStockTransfer` يوجب أن يكون
       الطرفان **مسجَّلين وعاملين** — بخلاف `draftStockMovement` الذي يقبل
       رمزاً غير مسجَّل — فالنموذج يعرض ما يقبله الباب وحده. وحقلُ نصٍّ حرّ
       هنا كان سيصنع رفضاً يستحقّه المستخدم ولم يكن ليعرفه قبل الإرسال.

   ٤ · **ومجموعة الصنف تُقرأ من الكتالوج ولا يكتبها أحد.** العقد يوجب
       `itemGroup` مع `itemId` في الطلب، ولا باب يشتقّ أحدهما من الآخر —
       فالشاشة تقرأ الكتالوج وتملأ الحقل، وتعرضه مقفلاً كي يُرى مصدره.

   ٥ · **الكمّية نصٌّ من أوّلها إلى آخرها.** لا `Number` ولا `parseFloat` في
       هذا الملفّ: الشكل يُفحص بنمط العقد النصّي، ثم يُحتجَز بـ`asMagnitude`،
       ثم يعبر السلك كما كُتب.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftStockTransfer,
  listItems,
  listStockTransfers,
  listStorageLocations,
  listWarehouses,
  moveStockTransfer,
} from "../../api/generated/client";
import { asMagnitude } from "../../api/generated/brands";
import { SCHEMA_Magnitude_RE } from "../../api/generated/formats";
import type { Item, StockTransfer, StoragePlace } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { Amount, useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import {
  Button,
  EmptyState,
  Field,
  MOTION,
  Panel,
  QuantityValue,
  StatCard,
  useMoment,
} from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, RefusalNextStep } from "./shared";

/** اليوم بصيغة yyyy-MM-dd ميلادية بأرقام لاتينية. */
function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/** المواضع العاملة وحدها — وهي ما يقبله الباب. */
function activeOnly(places: readonly StoragePlace[]): readonly StoragePlace[] {
  return places.filter((place) => place.isActive);
}

/* ═════════════════════════════════════════════════════ طرفا النقل معروضَين */

/**
 * طرفٌ من طرفَي النقل: مستودعٌ وموقعٌ فيه، معزولان اتّجاهياً.
 * @param props رمزا المستودع والموقع.
 */
function Leg(props: { warehouse: string; location: string }): ReactNode {
  return (
    <span className="inv-leg">
      <span>{props.warehouse}</span>
      <span className="inv-leg__arrow" aria-hidden="true">{"·"}</span>
      <span>{props.location}</span>
    </span>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة النقل بين موقعين. */
export function InventoryTransfersScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arrived, fireArrive] = useMoment("arrive");

  const [number, setNumber] = useState("");
  const [occurredOn, setOccurredOn] = useState(todayIso);
  const [itemId, setItemId] = useState("");
  const [fromWarehouse, setFromWarehouse] = useState("");
  const [fromLocation, setFromLocation] = useState("");
  const [toWarehouse, setToWarehouse] = useState("");
  const [toLocation, setToLocation] = useState("");
  const [magnitude, setMagnitude] = useState("");
  const [unit, setUnit] = useState("");

  const [created, setCreated] = useState<StockTransfer | null>(null);
  const [movedDoc, setMovedDoc] = useState<StockTransfer | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const scope = [config.baseUrl, config.token, config.companyId] as const;

  const transfers = useQuery({
    queryKey: ["inventory-transfers", ...scope],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listStockTransfers(transport, { companyId: config.companyId }, signal),
  });

  const items = useQuery({
    queryKey: ["inventory-transfer-items", ...scope],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listItems(transport, { companyId: config.companyId }, signal),
  });

  const warehouses = useQuery({
    queryKey: ["inventory-transfer-warehouses", ...scope],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listWarehouses(transport, { companyId: config.companyId }, signal),
  });

  const catalogue: readonly Item[] = useMemo(() => items.data?.items ?? [], [items.data]);
  const chosenItem = useMemo(
    () => catalogue.find((one) => one.id === itemId) ?? null,
    [catalogue, itemId]
  );

  /* سلّم وحدات الصنف المختار: وحدة الأساس ثم كل وحدةٍ لها معامل عليه. ولا
     وحدةَ ثالثة تُعرَض — الوحدة التي لا معامل لها تُرفض ولا تُفترَض. */
  const unitLadder = useMemo(
    () => (chosenItem ? [chosenItem.baseUnit, ...chosenItem.units.map((u) => u.unitCode)] : []),
    [chosenItem]
  );

  const activeWarehouses = useMemo(
    () => activeOnly(warehouses.data?.places ?? []),
    [warehouses.data]
  );

  /* **الرمز في الجسم والمعرّف في المسار.** `StockTransferRequest` يحمل
     `fromWarehouseId` **رمزاً** (يُطابَق بـ`row.Code` في الخادم)، بينما
     `/warehouses/{w}/locations` يحمل **معرّفاً**. فالنموذج يختار الرمز —
     وهو ما يقرأه المستخدم على الرفّ — والمعرّف يُشتقّ منه هنا للقراءة
     وحدها. وخلطُ الاثنين كان يُنتج رفض `storage_place_not_found` على
     مستودعٍ قائم. */
  const idOfCode = useCallback(
    (code: string) => activeWarehouses.find((place) => place.code === code)?.id ?? "",
    [activeWarehouses]
  );
  const fromWarehouseKey = idOfCode(fromWarehouse);
  const toWarehouseKey = idOfCode(toWarehouse);

  const fromLocations = useQuery({
    queryKey: ["inventory-transfer-from-locations", ...scope, fromWarehouseKey],
    enabled: config.companyId !== "" && fromWarehouseKey !== "",
    retry: false,
    queryFn: ({ signal }) =>
      listStorageLocations(
        transport,
        { companyId: config.companyId, warehouseId: fromWarehouseKey },
        signal
      ),
  });

  const toLocations = useQuery({
    queryKey: ["inventory-transfer-to-locations", ...scope, toWarehouseKey],
    enabled: config.companyId !== "" && toWarehouseKey !== "",
    retry: false,
    queryFn: ({ signal }) =>
      listStorageLocations(
        transport,
        { companyId: config.companyId, warehouseId: toWarehouseKey },
        signal
      ),
  });
  const activeFrom = useMemo(() => activeOnly(fromLocations.data?.places ?? []), [fromLocations.data]);
  const activeTo = useMemo(() => activeOnly(toLocations.data?.places ?? []), [toLocations.data]);

  const magnitudeBad = magnitude !== "" && !SCHEMA_Magnitude_RE.test(magnitude);
  const samePlace =
    fromWarehouse !== "" &&
    fromWarehouse === toWarehouse &&
    fromLocation !== "" &&
    fromLocation === toLocation;

  const ready =
    number !== "" &&
    occurredOn !== "" &&
    chosenItem !== null &&
    fromWarehouse !== "" &&
    fromLocation !== "" &&
    toWarehouse !== "" &&
    toLocation !== "" &&
    magnitude !== "" &&
    !magnitudeBad &&
    unit !== "" &&
    !samePlace;

  const rows: readonly StockTransfer[] = useMemo(
    () => transfers.data?.transfers ?? [],
    [transfers.data]
  );

  const counts = useMemo(() => {
    let drafts = 0;
    for (const row of rows) if (row.state === "DRAFT") drafts += 1;
    return { drafts, moved: rows.length - drafts };
  }, [rows]);

  const submit = useCallback(async () => {
    if (!chosenItem) return;
    setBusy(true);
    setError(null);
    try {
      const transfer = await draftStockTransfer(transport, {
        companyId: config.companyId,
        body: {
          number,
          occurredOn,
          itemId: chosenItem.code,
          itemGroup: chosenItem.itemGroup,
          fromWarehouseId: fromWarehouse,
          fromLocationId: fromLocation,
          toWarehouseId: toWarehouse,
          toLocationId: toLocation,
          quantity: { magnitude: asMagnitude(magnitude), unit },
        },
      });
      setCreated(transfer);
      setMovedDoc(null);
      fireArrive();
      await transfers.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [
    chosenItem, config.companyId, fireArrive, fromLocation, fromWarehouse, magnitude, number,
    occurredOn, toLocation, toWarehouse, transfers, transport, unit,
  ]);

  const move = useCallback(
    async (transferId: string) => {
      setBusy(true);
      setError(null);
      try {
        const moved = await moveStockTransfer(transport, {
          companyId: config.companyId,
          transferId,
        });
        setMovedDoc(moved);
        fireArrive();
        await transfers.refetch();
      } catch (failure) {
        setError(failure);
      } finally {
        setBusy(false);
      }
    },
    [config.companyId, fireArrive, transfers, transport]
  );

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="inventory-transfers-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.transfers.title")}</h1>
          <p className="sub">{t("inventory.transfers.lede")}</p>
        </div>
      </header>

      {/* الجملة التي تمنع سؤالاً: «أين قيد النقل؟» — ولا قيد، بالبناء. */}
      <p className="alert alert--info" role="note" data-testid="transfer-no-entry">
        {t("inventory.transfers.noEntry")}
      </p>

      {transfers.data ? (
        <div className="stats-row" data-testid="transfer-stats">
          <StatCard
            label={t("inventory.transfers.statAll")}
            count={transfers.data.transferCount}
            testId="stat-transfers"
          />
          <StatCard
            label={t("inventory.transfers.stateDraft")}
            count={counts.drafts}
            hint={t("inventory.transfers.draftHint")}
            testId="stat-transfer-drafts"
          />
          <StatCard
            label={t("inventory.transfers.stateMoved")}
            count={counts.moved}
            testId="stat-transfer-moved"
          />
        </div>
      ) : null}

      <div className="statline">
        <span className="spacer" />
        <div className="inline-group">
          <Button
            label={t("common.action.refresh")}
            onClick={() => void transfers.refetch()}
            testId="transfers-reload"
          />
        </div>
      </div>

      {transfers.isPending && transfers.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {transfers.isError ? (
        <ProblemPanel error={transfers.error} onRetry={() => void transfers.refetch()} />
      ) : null}

      {transfers.data && rows.length === 0 ? (
        <EmptyState
          title={t("inventory.transfers.emptyTitle")}
          body={t("inventory.transfers.emptyBody")}
          testId="transfers-empty"
        />
      ) : null}

      {rows.length > 0 ? (
        <Panel
          title={t("inventory.transfers.title")}
          note={t("inventory.transfers.tableNote")}
          testId="transfers-panel"
        >
          <div className="ledger" data-state="ready" data-testid="transfers-table">
            <table>
              <caption className="visually-hidden">{t("inventory.transfers.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.movements.colNumber")}</th>
                  <th scope="col">{t("inventory.movements.colDate")}</th>
                  <th scope="col">{t("inventory.movements.colItem")}</th>
                  <th scope="col">{t("inventory.transfers.colFrom")}</th>
                  <th scope="col">{t("inventory.transfers.colTo")}</th>
                  <th scope="col" className="n">{t("inventory.movements.colQuantity")}</th>
                  <th scope="col" className="n">{t("inventory.transfers.colValue")}</th>
                  <th scope="col">{t("inventory.movements.colState")}</th>
                  <th scope="col">{t("inventory.movements.colAction")}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => (
                  <tr
                    key={row.id}
                    data-testid="transfer-row"
                    data-state={row.state}
                    className={created && created.id === row.id ? MOTION.arrive : undefined}
                  >
                    <td className="code">{row.number}</td>
                    <td className="mono" dir="ltr">{row.occurredOn}</td>
                    <td className="code">{row.itemId}</td>
                    <td>
                      <Leg warehouse={row.fromWarehouseId} location={row.fromLocationId} />
                    </td>
                    <td>
                      <Leg warehouse={row.toWarehouseId} location={row.toLocationId} />
                    </td>
                    <td className="n">
                      <QuantityValue
                        magnitude={row.quantity.magnitude}
                        unit={row.quantity.unit}
                        testId="transfer-quantity"
                      />
                    </td>
                    <td className="n">
                      <Amount value={row.value} />
                    </td>
                    <td>
                      <span
                        className={"pill " + (row.state === "MOVED" ? "pill--posted" : "pill--draft")}
                        data-testid="transfer-state"
                      >
                        {row.state === "MOVED"
                          ? t("inventory.transfers.stateMoved")
                          : t("inventory.transfers.stateDraft")}
                      </span>
                    </td>
                    <td>
                      {row.state === "DRAFT" ? (
                        <Button
                          label={t("inventory.transfers.move")}
                          kind="primary"
                          size="sm"
                          disabled={busy}
                          onClick={() => void move(row.id)}
                          testId="transfer-move"
                        />
                      ) : (
                        <span className="muted">{t("inventory.transfers.movedAlready")}</span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted">{t("inventory.transfers.valueNote")}</p>
        </Panel>
      ) : null}

      {movedDoc ? (
        <section
          className={"alert " + (movedDoc.alreadyMoved ? "alert--warning" : "alert--success") + " " + arrived}
          role="status"
          data-testid="transfer-moved"
          data-already={String(movedDoc.alreadyMoved)}
        >
          <h2 style={{ marginTop: 0 }}>
            {movedDoc.alreadyMoved
              ? t("inventory.transfers.alreadyMoved")
              : t("inventory.transfers.moved")}
          </h2>
          <p>
            {movedDoc.alreadyMoved
              ? t("inventory.transfers.alreadyMovedBody")
              : t("inventory.transfers.movedBody")}
          </p>
        </section>
      ) : null}

      {created ? (
        <section
          className={"alert alert--success " + arrived}
          role="status"
          data-testid="transfer-created"
        >
          <h2 style={{ marginTop: 0 }}>{t("inventory.transfers.created")}</h2>
          <p>{t("inventory.transfers.createdBody")}</p>
        </section>
      ) : null}

      <Panel
        title={t("inventory.transfers.newTitle")}
        note={t("inventory.transfers.newNote")}
        testId="transfer-form"
      >
        <div className="grid fields-3">
          <Field
            id="tr-number"
            label={t("inventory.movements.number")}
            hint={t("inventory.movements.numberHint")}
            required
          >
            <input
              id="tr-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="transfer-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
          </Field>
          <Field
            id="tr-date"
            label={t("inventory.movements.date")}
            hint={t("inventory.movements.dateHint")}
            required
          >
            <input
              id="tr-date"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="transfer-date"
              value={occurredOn}
              onChange={(e) => setOccurredOn(e.target.value)}
            />
          </Field>
          <Field
            id="tr-item"
            label={t("inventory.movements.item")}
            hint={
              catalogue.length === 0
                ? t("inventory.movements.itemsEmpty")
                : t("inventory.movements.itemHint")
            }
            required
          >
            <select
              id="tr-item"
              className="ctl mono"
              data-testid="transfer-item"
              value={itemId}
              onChange={(e) => {
                setItemId(e.target.value);
                setUnit("");
              }}
            >
              <option value="">{t("inventory.movements.itemNone")}</option>
              {catalogue.map((one) => (
                <option key={one.id} value={one.id}>
                  {one.code + " — " + one.name.ar}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="grid fields-3" style={{ marginTop: "var(--space-12)" }}>
          <Field
            id="tr-group"
            label={t("inventory.movements.group")}
            hint={t("inventory.transfers.groupFrom")}
            source="read"
          >
            <input
              id="tr-group"
              className="ctl mono"
              dir="ltr"
              readOnly
              data-testid="transfer-group"
              value={chosenItem?.itemGroup ?? ""}
            />
          </Field>
          <Field
            id="tr-magnitude"
            label={t("inventory.movements.magnitude")}
            hint={t("inventory.movements.magnitudeHint")}
            error={magnitudeBad ? t("inventory.movements.magnitudeBad") : undefined}
            required
          >
            <input
              id="tr-magnitude"
              className={"ctl amt-input" + (magnitudeBad ? " is-invalid" : "")}
              dir="ltr"
              inputMode="decimal"
              autoComplete="off"
              aria-invalid={magnitudeBad}
              data-testid="transfer-magnitude"
              value={magnitude}
              onChange={(e) => setMagnitude(e.target.value)}
            />
          </Field>
          <Field
            id="tr-unit"
            label={t("inventory.movements.unit")}
            hint={
              chosenItem === null
                ? t("inventory.movements.unitPickItem")
                : t("inventory.movements.unitHint")
            }
            required
          >
            <select
              id="tr-unit"
              className="ctl mono"
              disabled={chosenItem === null}
              data-testid="transfer-unit"
              value={unit}
              onChange={(e) => setUnit(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {unitLadder.map((code) => (
                <option key={code} value={code}>
                  {code}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="grid fields-half" style={{ marginTop: "var(--space-12)" }}>
          <Field
            id="tr-from-wh"
            label={t("inventory.transfers.fromWarehouse")}
            hint={t("inventory.transfers.placeHint")}
            required
          >
            <select
              id="tr-from-wh"
              className="ctl mono"
              data-testid="transfer-from-warehouse"
              value={fromWarehouse}
              onChange={(e) => {
                setFromWarehouse(e.target.value);
                setFromLocation("");
              }}
            >
              <option value="">{t("common.label.select")}</option>
              {activeWarehouses.map((place) => (
                <option key={place.id} value={place.code}>
                  {place.code + " — " + place.name.ar}
                </option>
              ))}
            </select>
          </Field>
          <Field
            id="tr-from-loc"
            label={t("inventory.transfers.fromLocation")}
            hint={
              fromWarehouse === ""
                ? t("inventory.transfers.pickWarehouseFirst")
                : t("inventory.transfers.placeHint")
            }
            required
          >
            <select
              id="tr-from-loc"
              className="ctl mono"
              disabled={fromWarehouse === ""}
              data-testid="transfer-from-location"
              value={fromLocation}
              onChange={(e) => setFromLocation(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {activeFrom.map((place) => (
                <option key={place.id} value={place.code}>
                  {place.code + " — " + place.name.ar}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="grid fields-half" style={{ marginTop: "var(--space-12)" }}>
          <Field
            id="tr-to-wh"
            label={t("inventory.transfers.toWarehouse")}
            hint={t("inventory.transfers.placeHint")}
            required
          >
            <select
              id="tr-to-wh"
              className="ctl mono"
              data-testid="transfer-to-warehouse"
              value={toWarehouse}
              onChange={(e) => {
                setToWarehouse(e.target.value);
                setToLocation("");
              }}
            >
              <option value="">{t("common.label.select")}</option>
              {activeWarehouses.map((place) => (
                <option key={place.id} value={place.code}>
                  {place.code + " — " + place.name.ar}
                </option>
              ))}
            </select>
          </Field>
          <Field
            id="tr-to-loc"
            label={t("inventory.transfers.toLocation")}
            hint={
              toWarehouse === ""
                ? t("inventory.transfers.pickWarehouseFirst")
                : t("inventory.transfers.placeHint")
            }
            error={samePlace ? t("inventory.transfers.samePlace") : undefined}
            required
          >
            <select
              id="tr-to-loc"
              className={"ctl mono" + (samePlace ? " is-invalid" : "")}
              disabled={toWarehouse === ""}
              aria-invalid={samePlace}
              data-testid="transfer-to-location"
              value={toLocation}
              onChange={(e) => setToLocation(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {activeTo.map((place) => (
                <option key={place.id} value={place.code}>
                  {place.code + " — " + place.name.ar}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <p className="muted" style={{ marginTop: "var(--space-12)" }}>
          {t("inventory.transfers.noCostField")}
        </p>

        <div className="inline-group">
          <Button
            label={busy ? t("common.state.loading") : t("inventory.transfers.create")}
            kind="primary"
            disabled={!ready || busy}
            loading={busy}
            onClick={() => void submit()}
            testId="transfer-submit"
          />
        </div>
      </Panel>

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}
    </section>
  );
}
