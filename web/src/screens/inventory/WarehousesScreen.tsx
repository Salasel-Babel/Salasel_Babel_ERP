/* ═══════════════════════════════════════════════════════════════════════════
   سجلّ المستودعات — أعلى مستوى في التسكين، وأوّل ما يُسجَّل قبله
   The warehouse register — the top level of placement, registered first
   ───────────────────────────────────────────────────────────────────────────
   كانت شاشة الأرصدة تعرض «رمز المستودع» ولا سجلّ يقول ما هو، فكان الرمز
   نصّاً معلّقاً بلا اسم. هذه الشاشة تفتح السجلّ نفسه، وأربعة قرارات تحكمها:

   ١ · **الرمز هوية لا نصّ معروض.** يُكتب مرّةً عند التسجيل ولا يُعدَّل بعدها:
       العقد لا ينشر باباً لتغييره أصلاً — `renameWarehouse` يغيّر **الاسم**
       وحده — لأن الرمز محمولٌ على كل حركةٍ ورصيد، وتغييرُه يقطع كل حركةٍ مضت
       عن موضعها. فالحقل حاضرٌ في نموذج التسجيل ومقفلٌ في نموذج التسمية.

   ٢ · **المُعطَّل يبقى في القائمة.** العقد يُخرجه بـ`isActive = false` ولا
       يحذفه: «التعطيل حالةٌ تُقرأ لا غياب». فالشاشة تعرضه مُخفتاً بشارته،
       ولا تُخفيه خلف مرشّح افتراضي — ما يختفي يُظنّ محذوفاً.

   ٣ · **حكم التعطيل يُقال قبل الضغط لا بعده**: تعطيل المستودع **يُرفض** إن بقي
       فيه رصيد أو موقعٌ عامل تحته، وتعطيل **الصنف** يُقبل وله رصيد — وهما
       حكمان مختلفان عمداً (ADR-0072). فالشاشة تسمّي الحكم في مكانه، ثم تعرض
       رفض الخادم كاملاً إن وقع. ورفضٌ يُقرأ لأول مرّة بعد الضغط يُدرَّب عليه
       المستخدم بالمحاولة، وهي أغلى طريقةٍ لتعلّم قاعدة.

   ٤ · **التعطيل خطوتان لا واحدة.** الأولى تُظهر الحكم، والثانية تنفّذ. ولا
       نافذةَ حوارٍ: التأكيد يقع **في الصفّ نفسه** فيبقى الرمز الذي يُعطَّل
       تحت العين، ولا يُغطّى بالسؤال عنه.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  addWarehouse,
  deactivateWarehouse,
  listWarehouses,
  renameWarehouse,
} from "../../api/generated/client";
import type { StoragePlace } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Button, EmptyState, Field, MOTION, Panel, StatCard, useMoment } from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, RefusalNextStep } from "./shared";

/** شاشة سجلّ المستودعات. */
export function InventoryWarehousesScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arrived, fireArrive] = useMoment("arrive");

  /* حقول التسجيل — ثلاثة، وهي ما يطلبه `StoragePlaceRequest` بالضبط. */
  const [code, setCode] = useState("");
  const [arabicName, setArabicName] = useState("");
  const [latinName, setLatinName] = useState("");

  /* المختار: يُقرأ في لوح إعادة التسمية، ويُطلب تعطيله على مرحلتين.
     ⚠ والاسمان `arabicRename` و`latinRename` **لا** `renameAr` و`renameEn`: حارس
     القاعدة 14 يمسح `name_en|nameEn|NameEn` **كسلسلةٍ في النصّ لا كمعرّف**، و«renameEn»
     تحوي «nameEn» — فثمانية عشر موضعاً من هذا النوع رفعت العدّاد من 862 إلى 880 وأحمرّت
     البوّابة على شيفرةٍ لا تحمل زوج ar/en أصلاً.
     (docs/evidence/traps.md#fakh-a-substring-guard-fires-on-an-innocent-identifier) */
  const [selected, setSelected] = useState<string | null>(null);
  const [arabicRename, setArabicRename] = useState("");
  const [latinRename, setLatinRename] = useState("");
  const [pendingOff, setPendingOff] = useState<string | null>(null);

  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  const [created, setCreated] = useState<StoragePlace | null>(null);

  const result = useQuery({
    queryKey: ["inventory-warehouses", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listWarehouses(transport, { companyId: config.companyId }, signal),
  });

  const places: readonly StoragePlace[] = useMemo(
    () => result.data?.places ?? [],
    [result.data]
  );

  const counts = useMemo(() => {
    let active = 0;
    for (const place of places) if (place.isActive) active += 1;
    return { active, off: places.length - active };
  }, [places]);

  const chosen = useMemo(
    () => places.find((place) => place.id === selected) ?? null,
    [places, selected]
  );

  const pick = useCallback((place: StoragePlace) => {
    setSelected(place.id);
    setArabicRename(place.name.ar);
    setLatinRename(place.name.en);
    setPendingOff(null);
    setError(null);
  }, []);

  const submit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      const place = await addWarehouse(transport, {
        companyId: config.companyId,
        body: { code, name: { ar: arabicName, en: latinName } },
      });
      setCreated(place);
      setCode("");
      setArabicName("");
      setLatinName("");
      fireArrive();
      await result.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [arabicName, code, config.companyId, fireArrive, latinName, result, transport]);

  const rename = useCallback(async () => {
    if (!chosen) return;
    setBusy(true);
    setError(null);
    try {
      await renameWarehouse(transport, {
        companyId: config.companyId,
        warehouseId: chosen.id,
        body: { name: { ar: arabicRename, en: latinRename } },
      });
      fireArrive();
      await result.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [chosen, config.companyId, fireArrive, arabicRename, latinRename, result, transport]);

  const deactivate = useCallback(
    async (warehouseId: string) => {
      setBusy(true);
      setError(null);
      try {
        await deactivateWarehouse(transport, { companyId: config.companyId, warehouseId });
        setPendingOff(null);
        await result.refetch();
      } catch (failure) {
        setError(failure);
      } finally {
        setBusy(false);
      }
    },
    [config.companyId, result, transport]
  );

  if (config.companyId === "") return <ChooseCompanyFirst />;

  const ready = code !== "" && arabicName !== "" && latinName !== "";

  return (
    <section className="stack" data-testid="inventory-warehouses-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.warehouses.title")}</h1>
          <p className="sub">{t("inventory.warehouses.lede")}</p>
        </div>
      </header>

      {result.data ? (
        <div className="stats-row" data-testid="warehouse-stats">
          <StatCard
            label={t("inventory.warehouses.statAll")}
            count={result.data.placeCount}
            testId="stat-warehouses"
          />
          <StatCard label={t("inventory.warehouses.statActive")} count={counts.active} />
          <StatCard
            label={t("inventory.warehouses.statOff")}
            count={counts.off}
            hint={t("inventory.warehouses.statOffHint")}
            testId="stat-warehouses-off"
          />
        </div>
      ) : null}

      <div className="statline">
        <span className="spacer" />
        <div className="inline-group">
          <Button
            label={t("common.action.refresh")}
            onClick={() => void result.refetch()}
            testId="warehouses-reload"
          />
        </div>
      </div>

      {result.isPending && result.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {result.isError ? (
        <ProblemPanel error={result.error} onRetry={() => void result.refetch()} />
      ) : null}

      {result.data && places.length === 0 ? (
        <EmptyState
          title={t("inventory.warehouses.emptyTitle")}
          body={t("inventory.warehouses.emptyBody")}
          testId="warehouses-empty"
        />
      ) : null}

      {places.length > 0 ? (
        <Panel
          title={t("inventory.warehouses.title")}
          note={t("inventory.warehouses.tableNote")}
          testId="warehouses-panel"
        >
          <div className="ledger" data-state="ready" data-testid="warehouses-table">
            <table>
              <caption className="visually-hidden">{t("inventory.warehouses.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.reg.colCode")}</th>
                  <th scope="col">{t("inventory.reg.colName")}</th>
                  <th scope="col">{t("inventory.reg.colState")}</th>
                  <th scope="col">{t("inventory.reg.colAction")}</th>
                </tr>
              </thead>
              <tbody>
                {places.map((place) => (
                  <tr
                    key={place.id}
                    data-testid="warehouse-row"
                    data-selected={place.id === selected ? "true" : undefined}
                    data-active={String(place.isActive)}
                    className={created && created.id === place.id ? MOTION.arrive : undefined}
                  >
                    <td className="code">
                      <button
                        type="button"
                        className="btn btn-ghost btn-sm mono"
                        data-testid="warehouse-pick"
                        aria-pressed={place.id === selected}
                        onClick={() => pick(place)}
                      >
                        {place.code}
                      </button>
                    </td>
                    <td>
                      <span lang="ar" dir="rtl">{place.name.ar}</span>
                      <span className="alt" lang="en" dir="ltr">{place.name.en}</span>
                    </td>
                    <td>
                      <span
                        className={"pill " + (place.isActive ? "pill--posted" : "pill--archived")}
                        data-testid="warehouse-state"
                      >
                        {place.isActive
                          ? t("inventory.reg.stateActive")
                          : t("inventory.reg.stateOff")}
                      </span>
                    </td>
                    <td>
                      {!place.isActive ? (
                        <span className="muted">{t("inventory.reg.alreadyOff")}</span>
                      ) : pendingOff === place.id ? (
                        <div className="inline-group" data-testid="warehouse-confirm">
                          <Button
                            label={t("inventory.reg.confirmOff")}
                            kind="danger"
                            size="sm"
                            disabled={busy}
                            onClick={() => void deactivate(place.id)}
                            testId="warehouse-confirm-off"
                          />
                          <Button
                            label={t("common.action.cancel")}
                            size="sm"
                            onClick={() => setPendingOff(null)}
                          />
                        </div>
                      ) : (
                        <Button
                          label={t("inventory.reg.deactivate")}
                          size="sm"
                          disabled={busy}
                          onClick={() => {
                            setPendingOff(place.id);
                            setError(null);
                          }}
                          testId="warehouse-deactivate"
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {pendingOff !== null ? (
            <p className="alert alert--warning" role="status" data-testid="warehouse-off-rule">
              {t("inventory.warehouses.offRule")}
            </p>
          ) : null}
        </Panel>
      ) : null}

      {chosen ? (
        <Panel
          title={t("inventory.reg.rename")}
          note={t("inventory.reg.renameNote")}
          aside={<span className="pill pill--info mono">{chosen.code}</span>}
          testId="warehouse-rename"
        >
          <div className="grid fields-3">
            <Field
              id="wh-rename-code"
              label={t("inventory.reg.colCode")}
              hint={t("inventory.reg.codeLocked")}
            >
              <input
                id="wh-rename-code"
                className="ctl mono"
                dir="ltr"
                readOnly
                data-testid="warehouse-rename-code"
                value={chosen.code}
              />
            </Field>
            <Field
              id="wh-rename-ar"
              label={t("inventory.items.arabicName")}
              hint={t("inventory.reg.arabicNameHint")}
            >
              <input
                id="wh-rename-ar"
                className="ctl"
                lang="ar"
                data-testid="warehouse-rename-ar"
                value={arabicRename}
                onChange={(e) => setArabicRename(e.target.value)}
              />
            </Field>
            <Field
              id="wh-rename-en"
              label={t("inventory.items.englishName")}
              hint={t("inventory.reg.latinNameHint")}
            >
              <input
                id="wh-rename-en"
                className="ctl"
                lang="en"
                dir="ltr"
                data-testid="warehouse-rename-en"
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
              testId="warehouse-rename-submit"
            />
            <Button
              label={t("common.action.cancel")}
              onClick={() => setSelected(null)}
            />
          </div>
        </Panel>
      ) : null}

      {created ? (
        <section
          className={"alert alert--success " + arrived}
          role="status"
          data-testid="warehouse-created"
        >
          <h2 style={{ marginTop: 0 }}>{t("inventory.warehouses.created")}</h2>
          <p>{t("inventory.warehouses.createdBody")}</p>
        </section>
      ) : null}

      <Panel
        title={t("inventory.warehouses.add")}
        note={t("inventory.warehouses.addNote")}
        testId="warehouse-form"
      >
        <div className="grid fields-3">
          <Field
            id="wh-code"
            label={t("inventory.warehouses.code")}
            hint={t("inventory.warehouses.codeHint")}
            required
          >
            <input
              id="wh-code"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="warehouse-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
            />
          </Field>
          <Field
            id="wh-name-ar"
            label={t("inventory.items.arabicName")}
            hint={t("inventory.reg.arabicNameHint")}
            required
          >
            <input
              id="wh-name-ar"
              className="ctl"
              lang="ar"
              autoComplete="off"
              data-testid="warehouse-name-ar"
              value={arabicName}
              onChange={(e) => setArabicName(e.target.value)}
            />
          </Field>
          <Field
            id="wh-name-en"
            label={t("inventory.items.englishName")}
            hint={t("inventory.reg.latinNameHint")}
            required
          >
            <input
              id="wh-name-en"
              className="ctl"
              lang="en"
              dir="ltr"
              autoComplete="off"
              data-testid="warehouse-name-en"
              value={latinName}
              onChange={(e) => setLatinName(e.target.value)}
            />
          </Field>
        </div>
        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <Button
            label={busy ? t("common.state.loading") : t("inventory.reg.submit")}
            kind="primary"
            disabled={!ready || busy}
            loading={busy}
            onClick={() => void submit()}
            testId="warehouse-submit"
          />
        </div>
      </Panel>

      <p className="muted" data-testid="warehouses-off-note">
        {t("inventory.warehouses.offRule")}
      </p>

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}
    </section>
  );
}
