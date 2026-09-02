/* ═══════════════════════════════════════════════════════════════════════════
   المستودعات ومواقعها — حيث يصير «المكان» شيئاً موجوداً لا نصّاً حرّاً
   Warehouses and their locations — where 'place' becomes a thing that exists
   ───────────────────────────────────────────────────────────────────────────
   أربعة قرارات تحكم هذه الشاشة، وكلّها مقيسة على العقد لا مفترَضة:

   ١ · **منشأ الاسم يُعرض، لا يُخفى.** الترقية تملأ الكتالوج **بالملاحظة**:
       لكل نصّ مستودعٍ وُجد في حركةٍ أو رصيد صفٌّ اسمُه رمزُه ومنشؤه
       `OBSERVED`. وشاشةٌ تعرض ذلك بلا وسم تبدو ككتالوجٍ كتبه إنسان وهي صدى
       نصٍّ وُجد في البيانات — فالوسم هنا **يسأل** عن اسمٍ حقيقي بدل أن
       يدّعي أنّ عنده واحداً.

   ٢ · **المعطَّل يبقى معروضاً موسوماً.** إخفاؤه يترك رصيداً قائماً بلا
       مستودعٍ يفسّره في شاشة الأرصدة، وهو أسوأ من صفٍّ مكتوب عليه «معطَّل».

   ٣ · **الموقع مورد فرعي، وهو كذلك في هذه الشاشة أيضاً.** لا قائمةَ مواقعٍ
       عامّة: تُقرأ مواقع مستودعٍ **بعد اختياره**، لأن «A-01» بلا مستودعه
       ليس هوية.

   ٤ · **ولا رقم حساب في أي حقل.** `qualifier` مؤهّل دور تقرؤه مصفوفة
       الترحيل، والشاشة تقول ذلك في تلميح الحقل ولا تعرض حساباً ولا تطلبه.

   ولا عدد واحد في هذا الملفّ: لا كمّية ولا مال يعبران هذه الشاشة أصلاً —
   الأرصدة شاشتها، وهذه كتالوج. فلا تحويلَ نصٍّ إلى عدد، ولا تقريبَ ولا قسمة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  activateLocation,
  activateWarehouse,
  addLocation,
  addWarehouse,
  deactivateLocation,
  deactivateWarehouse,
  listLocations,
  listWarehouses,
} from "../../api/generated/client";
import type { Location, LocationRequest, Warehouse, WarehouseRequest } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Button, EmptyState, MOTION, Panel, useMoment } from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, RefusalNextStep, SurfaceGap } from "./shared";

/* ═════════════════════════════════════════════════ وسم منشأ الاسم */

/**
 * منشأ الاسم رقاقةً: ما كتبه إنسان، وما هو صدى نصٍّ وُجد في البيانات.
 * @param props المنشأ كما نشره العقد.
 */
function OriginChip(props: { origin: "DECLARED" | "OBSERVED" }): ReactNode {
  const { t } = useT();
  const observed = props.origin === "OBSERVED";
  return (
    <span
      className={"pill " + (observed ? "pill--pending" : "pill--info")}
      data-testid="place-origin"
      data-origin={props.origin}
    >
      {t(observed ? "inventory.places.observed" : "inventory.places.declared")}
    </span>
  );
}

/** حالة المكان رقاقةً — والمعطَّل معروضٌ لا مخفيّ. */
function StateChip(props: { active: boolean }): ReactNode {
  const { t } = useT();
  return (
    <span
      className={"pill " + (props.active ? "pill--posted" : "pill--archived")}
      data-testid="place-state"
      data-active={props.active ? "true" : "false"}
    >
      {t(props.active ? "inventory.places.active" : "inventory.places.inactive")}
    </span>
  );
}

/* ═══════════════════════════════════════════════════ مواقع مستودع */

/**
 * مواقع المستودع المختار — **مورد فرعي**: تُقرأ بعد اختياره لا قبله.
 * @param props المستودع المختار.
 */
function LocationsPanel(props: { warehouse: Warehouse }): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const { warehouse } = props;

  const [code, setCode] = useState("");
  const [arabicName, setArabicName] = useState("");
  const [latinName, setLatinName] = useState("");
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);

  const result = useQuery({
    queryKey: ["inventory-locations", config.baseUrl, config.token, config.companyId, warehouse.id],
    retry: false,
    queryFn: ({ signal }) =>
      listLocations(transport, { companyId: config.companyId, warehouseId: warehouse.id }, signal),
  });

  const rows: readonly Location[] = useMemo(() => result.data?.locations ?? [], [result.data]);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const body: LocationRequest = {
        code,
        nameAr: arabicName,
        nameTranslations: latinName === "" ? [] : [{ name: "en", value: latinName }],
      };
      await addLocation(transport, {
        companyId: config.companyId,
        warehouseId: warehouse.id,
        body,
      });
      setCode("");
      setArabicName("");
      setLatinName("");
      await result.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [arabicName, code, config.companyId, latinName, result, transport, warehouse.id]);

  const toggle = useCallback(
    async (location: Location) => {
      setError(null);
      try {
        const args = {
          companyId: config.companyId,
          warehouseId: warehouse.id,
          locationId: location.id,
        };
        if (location.isActive) await deactivateLocation(transport, args);
        else await activateLocation(transport, args);
        await result.refetch();
      } catch (failure) {
        setError(failure);
      }
    },
    [config.companyId, result, transport, warehouse.id]
  );

  return (
    <Panel
      title={t("inventory.places.locationsOf", { code: warehouse.code })}
      note={t("inventory.places.locationsNote")}
      aside={<span className="pill pill--info">{warehouse.code}</span>}
      testId="locations-panel"
    >
      {result.isPending && result.fetchStatus === "fetching" ? <ReadingSkeleton testId="locations-loading" /> : null}
      {result.isError ? <ProblemPanel error={result.error} onRetry={() => void result.refetch()} /> : null}

      {result.data && rows.length === 0 ? (
        <EmptyState
          small
          title={t("inventory.places.noLocationsTitle")}
          body={t("inventory.places.noLocationsBody")}
          testId="locations-empty"
        />
      ) : null}

      {rows.length > 0 ? (
        <>
          <p className="muted" data-testid="location-count">
            {tp("inventory.places.locationCount", result.data?.locationCount ?? rows.length)}
          </p>
          <div className="ledger" data-state="ready" data-testid="locations-table">
            <table>
              <caption className="visually-hidden">{t("inventory.places.locations")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.places.colCode")}</th>
                  <th scope="col">{t("inventory.places.colName")}</th>
                  <th scope="col">{t("inventory.places.colOrigin")}</th>
                  <th scope="col">{t("inventory.places.colState")}</th>
                  <th scope="col">{t("inventory.places.colAction")}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((location) => (
                  <tr key={location.id} data-testid="location-row">
                    <td className="code">{location.code}</td>
                    <td>
                      <span lang="ar" dir="rtl">{location.nameAr}</span>
                      {location.nameTranslations.map((name) => (
                        <span className="alt" key={name.name} lang={name.name} dir="ltr">
                          {name.value}
                        </span>
                      ))}
                    </td>
                    <td><OriginChip origin={location.origin} /></td>
                    <td><StateChip active={location.isActive} /></td>
                    <td>
                      <Button
                        label={t(location.isActive ? "inventory.places.deactivate" : "inventory.places.activate")}
                        kind={location.isActive ? "danger" : undefined}
                        size="sm"
                        onClick={() => void toggle(location)}
                        testId="location-toggle"
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </>
      ) : null}

      <div className="grid fields-3" style={{ marginTop: "var(--space-16)" }}>
        <div className="field">
          <label htmlFor="inv-loc-code">{t("inventory.places.locationCode")}</label>
          <input
            id="inv-loc-code"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="location-code"
            value={code}
            onChange={(e) => setCode(e.target.value)}
          />
          <span className="hint">{t("inventory.places.locationCodeHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="inv-loc-name-ar">{t("inventory.places.arabicName")}</label>
          <input
            id="inv-loc-name-ar"
            className="ctl"
            lang="ar"
            data-testid="location-name-ar"
            value={arabicName}
            onChange={(e) => setArabicName(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="inv-loc-name-en">{t("inventory.places.latinName")}</label>
          <input
            id="inv-loc-name-en"
            className="ctl"
            lang="en"
            dir="ltr"
            data-testid="location-name-en"
            value={latinName}
            onChange={(e) => setLatinName(e.target.value)}
          />
          <span className="hint">{t("inventory.places.latinNameHint")}</span>
        </div>
      </div>

      <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
        <Button
          label={busy ? t("common.state.loading") : t("inventory.places.addLocation")}
          kind="primary"
          disabled={busy || code === "" || arabicName === "" || !warehouse.isActive}
          loading={busy}
          onClick={() => void submit()}
          testId="location-submit"
        />
      </div>

      {!warehouse.isActive ? (
        <p className="alert alert--info" role="status" data-testid="location-blocked">
          {t("inventory.places.warehouseInactiveNote")}
        </p>
      ) : null}

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}
    </Panel>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة المستودعات ومواقعها. */
export function InventoryWarehousesScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [arrived, fireArrive] = useMoment("arrive");

  const [code, setCode] = useState("");
  const [arabicName, setArabicName] = useState("");
  const [latinName, setLatinName] = useState("");
  const [qualifier, setQualifier] = useState("");
  const [created, setCreated] = useState<Warehouse | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const result = useQuery({
    queryKey: ["inventory-warehouses", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listWarehouses(transport, { companyId: config.companyId }, signal),
  });

  const warehouses: readonly Warehouse[] = useMemo(() => result.data?.warehouses ?? [], [result.data]);

  /* بحثٌ نصّي على الرمز والاسم — بلا إعادة ترتيب: الترتيب حرفيٌّ ثابت من الخادم. */
  const shown = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    if (!needle) return warehouses;
    return warehouses.filter(
      (row) =>
        row.code.toLocaleLowerCase().includes(needle) ||
        row.nameAr.toLocaleLowerCase().includes(needle) ||
        row.nameTranslations.some((name) => name.value.toLocaleLowerCase().includes(needle))
    );
  }, [query, warehouses]);

  const chosen = useMemo(
    () => warehouses.find((row) => row.id === selected) ?? null,
    [selected, warehouses]
  );

  /* عددُ ما اسمُه صدى نصٍّ لا اسمٌ كتبه إنسان — وهو ما تسأل عنه الشاشة. */
  const observed = useMemo(
    () => warehouses.filter((row) => row.origin === "OBSERVED").length,
    [warehouses]
  );

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const body: WarehouseRequest = {
        code,
        nameAr: arabicName,
        nameTranslations: latinName === "" ? [] : [{ name: "en", value: latinName }],
        qualifier,
      };
      const warehouse = await addWarehouse(transport, { companyId: config.companyId, body });
      setCreated(warehouse);
      setSelected(warehouse.id);
      fireArrive();
      await result.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [arabicName, code, config.companyId, fireArrive, latinName, qualifier, result, transport]);

  const toggle = useCallback(
    async (warehouse: Warehouse) => {
      setError(null);
      try {
        const args = { companyId: config.companyId, warehouseId: warehouse.id };
        if (warehouse.isActive) await deactivateWarehouse(transport, args);
        else await activateWarehouse(transport, args);
        await result.refetch();
      } catch (failure) {
        setError(failure);
      }
    },
    [config.companyId, result, transport]
  );

  const startAnother = useCallback(() => {
    setCreated(null);
    setError(null);
    setCode("");
    setArabicName("");
    setLatinName("");
    setQualifier("");
  }, []);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="inventory-warehouses-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.places.title")}</h1>
          <p className="sub">{t("inventory.places.lede")}</p>
        </div>
      </header>

      <div className="statline">
        {result.data ? (
          <span className={"pill " + arrived} data-testid="warehouse-count">
            {tp("inventory.places.count", result.data.warehouseCount)}
          </span>
        ) : null}
        <span className="spacer" />
        <div className="inline-group">
          <Button
            label={t("common.action.refresh")}
            onClick={() => void result.refetch()}
            testId="warehouses-reload"
          />
        </div>
      </div>

      {observed > 0 ? (
        <p className="alert alert--warning" role="status" data-testid="warehouses-observed">
          {tp("inventory.places.observedCount", observed)} {t("inventory.places.observedWhy")}
        </p>
      ) : null}

      <div className="filterbar" role="search">
        <div className="field wide">
          <label htmlFor="inv-wh-search">{t("inventory.places.search")}</label>
          <input
            id="inv-wh-search"
            className="ctl"
            type="search"
            data-testid="warehouses-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
            placeholder={t("inventory.places.searchPh")}
          />
        </div>
      </div>

      {result.isPending && result.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {result.isError ? (
        <ProblemPanel error={result.error} onRetry={() => void result.refetch()} />
      ) : null}

      {result.data && warehouses.length === 0 ? (
        <EmptyState
          title={t("inventory.places.emptyTitle")}
          body={t("inventory.places.emptyBody")}
          testId="warehouses-empty"
        />
      ) : null}

      {result.data && warehouses.length > 0 && shown.length === 0 ? (
        <EmptyState
          small
          title={t("inventory.places.noMatchTitle")}
          body={t("inventory.places.noMatchBody")}
          action={<Button label={t("common.action.clearSearch")} onClick={() => setQuery("")} />}
          testId="warehouses-no-match"
        />
      ) : null}

      {shown.length > 0 ? (
        <Panel
          title={t("inventory.places.title")}
          note={t("inventory.places.selectHint")}
          testId="warehouses-panel"
        >
          <div className="ledger" data-state="ready" data-testid="warehouses-table">
            <table>
              <caption className="visually-hidden">{t("inventory.places.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.places.colCode")}</th>
                  <th scope="col">{t("inventory.places.colName")}</th>
                  <th scope="col">{t("inventory.places.colQualifier")}</th>
                  <th scope="col">{t("inventory.places.colOrigin")}</th>
                  <th scope="col">{t("inventory.places.colState")}</th>
                  <th scope="col">{t("inventory.places.colAction")}</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((warehouse) => (
                  <tr
                    key={warehouse.id}
                    data-testid="warehouse-row"
                    data-selected={warehouse.id === selected ? "true" : undefined}
                    className={created && created.id === warehouse.id ? MOTION.arrive : undefined}
                  >
                    <td className="code">
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm mono"
                        data-testid="warehouse-pick"
                        aria-pressed={warehouse.id === selected}
                        onClick={() => setSelected(warehouse.id)}
                      >
                        {warehouse.code}
                      </button>
                    </td>
                    <td>
                      <span lang="ar" dir="rtl">{warehouse.nameAr}</span>
                      {warehouse.nameTranslations.map((name) => (
                        <span className="alt" key={name.name} lang={name.name} dir="ltr">
                          {name.value}
                        </span>
                      ))}
                    </td>
                    <td className="code">
                      {warehouse.qualifier === "" ? (
                        <span className="muted">{t("inventory.places.noQualifier")}</span>
                      ) : (
                        warehouse.qualifier
                      )}
                    </td>
                    <td><OriginChip origin={warehouse.origin} /></td>
                    <td><StateChip active={warehouse.isActive} /></td>
                    <td>
                      <Button
                        label={t(warehouse.isActive ? "inventory.places.deactivate" : "inventory.places.activate")}
                        kind={warehouse.isActive ? "danger" : undefined}
                        size="sm"
                        onClick={() => void toggle(warehouse)}
                        testId="warehouse-toggle"
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      ) : null}

      {chosen ? <LocationsPanel warehouse={chosen} key={chosen.id} /> : null}

      {created ? (
        <section
          className={"alert alert--success " + arrived}
          role="status"
          data-testid="warehouse-created"
        >
          <h2 style={{ marginTop: 0 }}>{t("inventory.places.created")}</h2>
          <p>{t("inventory.places.createdBody")}</p>
          <div className="inline-group">
            <Button
              label={t("inventory.places.another")}
              kind="primary"
              onClick={startAnother}
              testId="warehouse-another"
            />
          </div>
        </section>
      ) : null}

      <Panel
        title={t("inventory.places.add")}
        note={t("inventory.places.addNote")}
        testId="warehouse-form"
      >
        <div className="grid fields-half">
          <div className="field">
            <label htmlFor="inv-wh-code">{t("inventory.places.code")}</label>
            <input
              id="inv-wh-code"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="warehouse-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
            />
            <span className="hint">{t("inventory.places.codeHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="inv-wh-qualifier">{t("inventory.places.qualifier")}</label>
            <input
              id="inv-wh-qualifier"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="warehouse-qualifier"
              value={qualifier}
              onChange={(e) => setQualifier(e.target.value)}
            />
            <span className="hint">{t("inventory.places.qualifierHint")}</span>
          </div>
        </div>

        <div className="grid fields-half" style={{ marginTop: "var(--space-12)" }}>
          <div className="field">
            <label htmlFor="inv-wh-name-ar">{t("inventory.places.arabicName")}</label>
            <input
              id="inv-wh-name-ar"
              className="ctl"
              lang="ar"
              data-testid="warehouse-name-ar"
              value={arabicName}
              onChange={(e) => setArabicName(e.target.value)}
            />
            <span className="hint">{t("inventory.places.nameHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="inv-wh-name-en">{t("inventory.places.latinName")}</label>
            <input
              id="inv-wh-name-en"
              className="ctl"
              lang="en"
              dir="ltr"
              data-testid="warehouse-name-en"
              value={latinName}
              onChange={(e) => setLatinName(e.target.value)}
            />
            <span className="hint">{t("inventory.places.latinNameHint")}</span>
          </div>
        </div>

        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <Button
            label={busy ? t("common.state.loading") : t("inventory.places.submit")}
            kind="primary"
            disabled={busy || code === "" || arabicName === ""}
            loading={busy}
            onClick={() => void submit()}
            testId="warehouse-submit"
          />
        </div>
      </Panel>

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}

      <SurfaceGap
        title={t("inventory.places.gapTitle")}
        body={t("inventory.places.gapBody")}
        owed={t("inventory.places.gapNext")}
        testId="warehouses-gap"
      />
    </section>
  );
}
