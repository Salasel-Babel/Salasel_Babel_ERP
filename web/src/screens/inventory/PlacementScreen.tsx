/* ═══════════════════════════════════════════════════════════════════════════
   شجرة التسكين — مستودع ← موقع ← رفّ، ومستوى الرصيد وسطُها لا قاعُها
   The placement tree — warehouse → location → bin, valued at the middle level
   ───────────────────────────────────────────────────────────────────────────
   طلب صاحب المصلحة كان «المخزني الشامل **لتسكين القطع**». والتسكين ثلاثة
   مستويات في العقد، **ولها حكمان مختلفان لا حكم واحد**:

   ١ · **الموقع هو مستوى الرصيد المُقيَّم** (ADR-0070): مفتاح الرصيد
       (منشأة × صنف × مستودع × موقع)، والرفّ تحته **ليس بُعد تقييم**. فهذه
       الشاشة لا تعرض رصيداً على رفّ ولا صفراً بجانبه: «لا رصيد» على رفٍّ فيه
       بضاعة أسوأ من ألّا يُذكر الرصيد أصلاً.

   ٢ · **ولذلك يختلف حكم التعطيل بين المستويين**: تعطيل موقعٍ فيه رصيد
       **يُرفض** (ADR-0072) لأن البضاعة تبقى بقيمتها في الحساب الضابط بلا
       بابٍ تخرج منه؛ وتعطيل رفٍّ **لا فحص رصيدٍ عليه أصلاً** لأنه ليس بُعداً
       في المفتاح، وفحصٌ يبحث عمّا لا يوجد يُرجع «لا رصيد» دائماً فيبدو حارساً
       وهو لا يحرس شيئاً. والشاشة تقول الحكمين **في مكانيهما** قبل الضغط.

   ٣ · **والانتماء بنيةٌ في العنوان لا حقلٌ في الجسم**: `/warehouses/{w}/
       locations/{l}/bins`. فلا يُختار رفٌّ بلا موقعه ولا موقعٌ بلا مستودعه،
       ولا يُكتب أيٌّ منها بيد — الدرج نفسه هو ما يبني المسار. وموضعان
       بالرمز نفسه تحت أبوين شيئان مختلفان (`inventory.storage_place_not_
       under_parent`)، فالعنوان إفادةٌ تُصدَّق لا زينة.

   ٤ · **والمُعطَّل يبقى في الدرج مُخفتاً.** العقد يُخرجه بـ`isActive=false`
       ولا يحذفه؛ وإخفاؤه هنا كان سيجعله يُظنّ محذوفاً، ثم يُسجَّل رمزُه
       ثانيةً فيُرفض بتكرارٍ لا يفهمه أحد.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  addStorageBin,
  addStorageLocation,
  deactivateStorageBin,
  deactivateStorageLocation,
  deactivateWarehouse,
  listStorageBins,
  listStorageLocations,
  listWarehouses,
  renameStorageBin,
  renameStorageLocation,
  renameWarehouse,
} from "../../api/generated/client";
import type { StoragePlace } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Button, EmptyState, Field, Panel, StatCard } from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, RefusalNextStep } from "./shared";

/** المستويات الثلاثة بأسمائها في العقد حرفاً بحرف. */
type Level = StoragePlace["level"];

/** مفتاح اسم المستوى في طبقة اللغة. */
const LEVEL_LABEL: Readonly<Record<Level, string>> = {
  WAREHOUSE: "inventory.placement.rungWarehouse",
  LOCATION: "inventory.placement.rungLocation",
  BIN: "inventory.placement.rungBin",
};

/** حكمُ التعطيل على كل مستوى — **مُعلَنٌ قبل الضغط لا بعده**. */
const LEVEL_OFF_RULE: Readonly<Record<Level, string>> = {
  WAREHOUSE: "inventory.placement.offRuleWarehouse",
  LOCATION: "inventory.placement.offRuleLocation",
  BIN: "inventory.placement.offRuleBin",
};

/* ═══════════════════════════════════════════════════════ درجةٌ من الدرج */

/**
 * درجةٌ في الشجرة: عنوانٌ وقائمةُ مواضع تُنتقى منها واحدة.
 * @param props المستوى والمواضع والمنتقى وما يقع عند الانتقاء.
 */
function Rung(props: {
  readonly level: Level;
  readonly places: readonly StoragePlace[];
  readonly picked: string | null;
  readonly onPick: (place: StoragePlace) => void;
  readonly waiting: string | null;
  readonly empty: string;
  readonly testId: string;
}): ReactNode {
  const { t } = useT();
  return (
    <section className="inv-rung" data-picked={props.picked !== null ? "true" : "false"} data-testid={props.testId}>
      <div className="inv-rung__hd">
        <span className="inv-rung__k">{t(LEVEL_LABEL[props.level])}</span>
        <span className="pill pill--info">{String(props.places.length)}</span>
      </div>
      {props.waiting !== null ? (
        <p className="muted" data-testid={props.testId + "-waiting"}>{props.waiting}</p>
      ) : props.places.length === 0 ? (
        <p className="muted" data-testid={props.testId + "-empty"}>{props.empty}</p>
      ) : (
        props.places.map((place) => (
          <div
            className="inv-place"
            key={place.id}
            data-testid="place-row"
            data-active={String(place.isActive)}
          >
            <button
              type="button"
              className="btn btn-ghost btn-sm inv-place__pick"
              data-testid="place-pick"
              aria-pressed={place.id === props.picked}
              onClick={() => props.onPick(place)}
            >
              <span className="inv-place__code">{place.code}</span>
            </button>
            <span
              className={"pill " + (place.isActive ? "pill--posted" : "pill--archived")}
              data-testid="place-state"
            >
              {place.isActive ? t("inventory.reg.stateActive") : t("inventory.reg.stateOff")}
            </span>
          </div>
        ))
      )}
    </section>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة شجرة التسكين. */
export function InventoryPlacementScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();

  const [warehouse, setWarehouse] = useState<StoragePlace | null>(null);
  const [location, setLocation] = useState<StoragePlace | null>(null);
  const [bin, setBin] = useState<StoragePlace | null>(null);

  const [arabicRename, setArabicRename] = useState("");
  const [latinRename, setLatinRename] = useState("");
  const [pendingOff, setPendingOff] = useState(false);

  const [locCode, setLocCode] = useState("");
  const [locAr, setLocAr] = useState("");
  const [locEn, setLocEn] = useState("");
  const [binCode, setBinCode] = useState("");
  const [binAr, setBinAr] = useState("");
  const [binEn, setBinEn] = useState("");

  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const scope = [config.baseUrl, config.token, config.companyId] as const;

  const warehouses = useQuery({
    queryKey: ["inventory-placement-warehouses", ...scope],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listWarehouses(transport, { companyId: config.companyId }, signal),
  });

  const locations = useQuery({
    queryKey: ["inventory-placement-locations", ...scope, warehouse?.id ?? ""],
    enabled: config.companyId !== "" && warehouse !== null,
    retry: false,
    queryFn: ({ signal }) =>
      listStorageLocations(
        transport,
        { companyId: config.companyId, warehouseId: warehouse?.id ?? "" },
        signal
      ),
  });

  const bins = useQuery({
    queryKey: ["inventory-placement-bins", ...scope, warehouse?.id ?? "", location?.id ?? ""],
    enabled: config.companyId !== "" && warehouse !== null && location !== null,
    retry: false,
    queryFn: ({ signal }) =>
      listStorageBins(
        transport,
        {
          companyId: config.companyId,
          warehouseId: warehouse?.id ?? "",
          locationId: location?.id ?? "",
        },
        signal
      ),
  });

  /* المختار هو **أعمق** ما انتُقي: الرفّ إن وُجد، وإلّا الموقع، وإلّا المستودع.
     ولا مختارَ ضمنيّ: من لم ينتقِ شيئاً لا يُفتح له لوحُ تعديل. */
  const chosen: StoragePlace | null = bin ?? location ?? warehouse;

  const pickWarehouse = useCallback((place: StoragePlace) => {
    setWarehouse(place);
    setLocation(null);
    setBin(null);
    setArabicRename(place.name.ar);
    setLatinRename(place.name.en);
    setPendingOff(false);
    setError(null);
  }, []);

  const pickLocation = useCallback((place: StoragePlace) => {
    setLocation(place);
    setBin(null);
    setArabicRename(place.name.ar);
    setLatinRename(place.name.en);
    setPendingOff(false);
    setError(null);
  }, []);

  const pickBin = useCallback((place: StoragePlace) => {
    setBin(place);
    setArabicRename(place.name.ar);
    setLatinRename(place.name.en);
    setPendingOff(false);
    setError(null);
  }, []);

  const reload = useCallback(async () => {
    await warehouses.refetch();
    if (warehouse) await locations.refetch();
    if (location) await bins.refetch();
  }, [bins, location, locations, warehouse, warehouses]);

  const rename = useCallback(async () => {
    if (!chosen) return;
    setBusy(true);
    setError(null);
    const body = { name: { ar: arabicRename, en: latinRename } };
    try {
      if (chosen.level === "WAREHOUSE") {
        await renameWarehouse(transport, {
          companyId: config.companyId,
          warehouseId: chosen.id,
          body,
        });
      } else if (chosen.level === "LOCATION") {
        await renameStorageLocation(transport, {
          companyId: config.companyId,
          warehouseId: warehouse?.id ?? "",
          locationId: chosen.id,
          body,
        });
      } else {
        await renameStorageBin(transport, {
          companyId: config.companyId,
          warehouseId: warehouse?.id ?? "",
          locationId: location?.id ?? "",
          binId: chosen.id,
          body,
        });
      }
      await reload();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [chosen, config.companyId, location, reload, arabicRename, latinRename, transport, warehouse]);

  const deactivate = useCallback(async () => {
    if (!chosen) return;
    setBusy(true);
    setError(null);
    try {
      if (chosen.level === "WAREHOUSE") {
        await deactivateWarehouse(transport, {
          companyId: config.companyId,
          warehouseId: chosen.id,
        });
      } else if (chosen.level === "LOCATION") {
        await deactivateStorageLocation(transport, {
          companyId: config.companyId,
          warehouseId: warehouse?.id ?? "",
          locationId: chosen.id,
        });
      } else {
        await deactivateStorageBin(transport, {
          companyId: config.companyId,
          warehouseId: warehouse?.id ?? "",
          locationId: location?.id ?? "",
          binId: chosen.id,
        });
      }
      setPendingOff(false);
      await reload();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [chosen, config.companyId, location, reload, transport, warehouse]);

  const addLocation = useCallback(async () => {
    if (!warehouse) return;
    setBusy(true);
    setError(null);
    try {
      await addStorageLocation(transport, {
        companyId: config.companyId,
        warehouseId: warehouse.id,
        body: { code: locCode, name: { ar: locAr, en: locEn } },
      });
      setLocCode("");
      setLocAr("");
      setLocEn("");
      await locations.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, locAr, locCode, locEn, locations, transport, warehouse]);

  const addBin = useCallback(async () => {
    if (!warehouse || !location) return;
    setBusy(true);
    setError(null);
    try {
      await addStorageBin(transport, {
        companyId: config.companyId,
        warehouseId: warehouse.id,
        locationId: location.id,
        body: { code: binCode, name: { ar: binAr, en: binEn } },
      });
      setBinCode("");
      setBinAr("");
      setBinEn("");
      await bins.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [binAr, binCode, binEn, bins, config.companyId, location, transport, warehouse]);

  const warehousePlaces = useMemo(() => warehouses.data?.places ?? [], [warehouses.data]);
  const locationPlaces = useMemo(() => locations.data?.places ?? [], [locations.data]);
  const binPlaces = useMemo(() => bins.data?.places ?? [], [bins.data]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="inventory-placement-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.placement.title")}</h1>
          <p className="sub">{t("inventory.placement.lede")}</p>
        </div>
      </header>

      <div className="stats-row" data-testid="placement-stats">
        <StatCard
          label={t("inventory.placement.rungWarehouse")}
          count={warehousePlaces.length}
          testId="stat-warehouses"
        />
        <StatCard
          label={t("inventory.placement.rungLocation")}
          count={locationPlaces.length}
          hint={t("inventory.placement.balanceLevel")}
          testId="stat-locations"
        />
        <StatCard
          label={t("inventory.placement.rungBin")}
          count={binPlaces.length}
          hint={t("inventory.placement.binNotValued")}
          testId="stat-bins"
        />
      </div>

      <div className="statline">
        <span className="spacer" />
        <div className="inline-group">
          <Button
            label={t("common.action.refresh")}
            onClick={() => void reload()}
            testId="placement-reload"
          />
        </div>
      </div>

      {warehouses.isPending && warehouses.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {warehouses.isError ? (
        <ProblemPanel error={warehouses.error} onRetry={() => void warehouses.refetch()} />
      ) : null}
      {locations.isError ? <ProblemPanel error={locations.error} /> : null}
      {bins.isError ? <ProblemPanel error={bins.error} /> : null}

      {warehouses.data && warehousePlaces.length === 0 ? (
        <EmptyState
          title={t("inventory.placement.emptyTitle")}
          body={t("inventory.placement.emptyBody")}
          testId="placement-empty"
        />
      ) : null}

      {warehousePlaces.length > 0 ? (
        <Panel
          title={t("inventory.placement.treeTitle")}
          note={t("inventory.placement.treeNote")}
          testId="placement-tree"
        >
          <div className="inv-tree">
            <Rung
              level="WAREHOUSE"
              places={warehousePlaces}
              picked={warehouse?.id ?? null}
              onPick={pickWarehouse}
              waiting={null}
              empty={t("inventory.placement.emptyTitle")}
              testId="rung-warehouse"
            />
            <Rung
              level="LOCATION"
              places={locationPlaces}
              picked={location?.id ?? null}
              onPick={pickLocation}
              waiting={warehouse === null ? t("inventory.placement.needWarehouse") : null}
              empty={t("inventory.placement.emptyLocations")}
              testId="rung-location"
            />
            <Rung
              level="BIN"
              places={binPlaces}
              picked={bin?.id ?? null}
              onPick={pickBin}
              waiting={location === null ? t("inventory.placement.needLocation") : null}
              empty={t("inventory.placement.emptyBins")}
              testId="rung-bin"
            />
          </div>
          <p className="muted">{t("inventory.placement.balanceLevelWhy")}</p>
        </Panel>
      ) : null}

      {chosen ? (
        <Panel
          title={t("inventory.placement.chosen")}
          note={t(LEVEL_OFF_RULE[chosen.level])}
          aside={<span className="pill pill--info mono">{chosen.code}</span>}
          testId="placement-chosen"
        >
          <div className="grid fields-3">
            <Field
              id="pl-code"
              label={t("inventory.reg.colCode")}
              hint={t("inventory.reg.codeLocked")}
            >
              <input
                id="pl-code"
                className="ctl mono"
                dir="ltr"
                readOnly
                data-testid="place-code"
                value={chosen.code}
              />
            </Field>
            <Field
              id="pl-name-ar"
              label={t("inventory.items.arabicName")}
              hint={t("inventory.reg.arabicNameHint")}
            >
              <input
                id="pl-name-ar"
                className="ctl"
                lang="ar"
                data-testid="place-name-ar"
                value={arabicRename}
                onChange={(e) => setArabicRename(e.target.value)}
              />
            </Field>
            <Field
              id="pl-name-en"
              label={t("inventory.items.englishName")}
              hint={t("inventory.reg.latinNameHint")}
            >
              <input
                id="pl-name-en"
                className="ctl"
                lang="en"
                dir="ltr"
                data-testid="place-name-en"
                value={latinRename}
                onChange={(e) => setLatinRename(e.target.value)}
              />
            </Field>
          </div>

          <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
            <Button
              label={t("inventory.reg.renameSubmit")}
              kind="primary"
              disabled={busy || arabicRename === "" || latinRename === ""}
              loading={busy}
              onClick={() => void rename()}
              testId="place-rename-submit"
            />
            {!chosen.isActive ? (
              <span className="muted">{t("inventory.reg.alreadyOff")}</span>
            ) : pendingOff ? (
              <>
                <Button
                  label={t("inventory.reg.confirmOff")}
                  kind="danger"
                  disabled={busy}
                  onClick={() => void deactivate()}
                  testId="place-confirm-off"
                />
                <Button label={t("common.action.cancel")} onClick={() => setPendingOff(false)} />
              </>
            ) : (
              <Button
                label={t("inventory.reg.deactivate")}
                disabled={busy}
                onClick={() => {
                  setPendingOff(true);
                  setError(null);
                }}
                testId="place-deactivate"
              />
            )}
          </div>

          {pendingOff ? (
            <p className="alert alert--warning" role="status" data-testid="place-off-rule">
              {t(LEVEL_OFF_RULE[chosen.level])}
            </p>
          ) : null}
        </Panel>
      ) : null}

      <Panel
        title={t("inventory.placement.addLocation")}
        note={t("inventory.placement.addLocationNote")}
        aside={
          warehouse ? <span className="pill pill--info mono">{warehouse.code}</span> : undefined
        }
        testId="placement-add-location"
      >
        {warehouse === null ? (
          <p className="muted" data-testid="add-location-needs-warehouse">
            {t("inventory.placement.needWarehouse")}
          </p>
        ) : null}
        {
          /* **النموذج لا يظهر ويختفي.** ظهورُ حقولٍ واختفاؤها بتغيّر اختيارٍ
             فوقها يقفز بالتخطيط ويُفقد ما كُتب؛ فالحقول ثابتة، **والفعل وحده
             مُقفَل** وسببُه مكتوب أعلاه. ومن كتب الرمز والاسم ثم اختار أباه
             لا يُعيد الكتابة. */
          <>
            <div className="grid fields-3">
              <Field
                id="pl-loc-code"
                label={t("inventory.placement.locationCode")}
                hint={t("inventory.placement.locationCodeHint")}
                required
              >
                <input
                  id="pl-loc-code"
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  data-testid="location-code"
                  value={locCode}
                  onChange={(e) => setLocCode(e.target.value)}
                />
              </Field>
              <Field
                id="pl-loc-ar"
                label={t("inventory.items.arabicName")}
                hint={t("inventory.reg.arabicNameHint")}
                required
              >
                <input
                  id="pl-loc-ar"
                  className="ctl"
                  lang="ar"
                  autoComplete="off"
                  data-testid="location-name-ar"
                  value={locAr}
                  onChange={(e) => setLocAr(e.target.value)}
                />
              </Field>
              <Field
                id="pl-loc-en"
                label={t("inventory.items.englishName")}
                hint={t("inventory.reg.latinNameHint")}
                required
              >
                <input
                  id="pl-loc-en"
                  className="ctl"
                  lang="en"
                  dir="ltr"
                  autoComplete="off"
                  data-testid="location-name-en"
                  value={locEn}
                  onChange={(e) => setLocEn(e.target.value)}
                />
              </Field>
            </div>
            <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
              <Button
                label={t("inventory.reg.submit")}
                kind="primary"
                disabled={
                  busy || warehouse === null || locCode === "" || locAr === "" || locEn === ""
                }
                loading={busy}
                onClick={() => void addLocation()}
                testId="location-submit"
              />
            </div>
          </>
        }
      </Panel>

      <Panel
        title={t("inventory.placement.addBin")}
        note={t("inventory.placement.addBinNote")}
        aside={location ? <span className="pill pill--info mono">{location.code}</span> : undefined}
        testId="placement-add-bin"
      >
        {location === null ? (
          <p className="muted" data-testid="add-bin-needs-location">
            {t("inventory.placement.needLocation")}
          </p>
        ) : null}
        {
          /* **النموذج لا يظهر ويختفي.** ظهورُ حقولٍ واختفاؤها بتغيّر اختيارٍ
             فوقها يقفز بالتخطيط ويُفقد ما كُتب؛ فالحقول ثابتة، **والفعل وحده
             مُقفَل** وسببُه مكتوب أعلاه. ومن كتب الرمز والاسم ثم اختار أباه
             لا يُعيد الكتابة. */
          <>
            <div className="grid fields-3">
              <Field
                id="pl-bin-code"
                label={t("inventory.placement.binCode")}
                hint={t("inventory.placement.binCodeHint")}
                required
              >
                <input
                  id="pl-bin-code"
                  className="ctl mono"
                  dir="ltr"
                  autoComplete="off"
                  data-testid="bin-code"
                  value={binCode}
                  onChange={(e) => setBinCode(e.target.value)}
                />
              </Field>
              <Field
                id="pl-bin-ar"
                label={t("inventory.items.arabicName")}
                hint={t("inventory.reg.arabicNameHint")}
                required
              >
                <input
                  id="pl-bin-ar"
                  className="ctl"
                  lang="ar"
                  autoComplete="off"
                  data-testid="bin-name-ar"
                  value={binAr}
                  onChange={(e) => setBinAr(e.target.value)}
                />
              </Field>
              <Field
                id="pl-bin-en"
                label={t("inventory.items.englishName")}
                hint={t("inventory.reg.latinNameHint")}
                required
              >
                <input
                  id="pl-bin-en"
                  className="ctl"
                  lang="en"
                  dir="ltr"
                  autoComplete="off"
                  data-testid="bin-name-en"
                  value={binEn}
                  onChange={(e) => setBinEn(e.target.value)}
                />
              </Field>
            </div>
            <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
              <Button
                label={t("inventory.reg.submit")}
                kind="primary"
                disabled={
                  busy || location === null || binCode === "" || binAr === "" || binEn === ""
                }
                loading={busy}
                onClick={() => void addBin()}
                testId="bin-submit"
              />
            </div>
          </>
        }
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
