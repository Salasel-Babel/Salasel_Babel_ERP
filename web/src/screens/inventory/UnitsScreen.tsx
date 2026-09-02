/* ═══════════════════════════════════════════════════════════════════════════
   وحدات القياس ومعاملاتها — ومسبارٌ يقول «لا» باسمها
   Units of measure, their factors, and a probe that says no by name
   ───────────────────────────────────────────────────────────────────────────
   طلب صاحب المصلحة كان «القطع **ووحداتها المختلفة**». وهذه الشاشة تفتح
   السجلّ الذي يجعل ذلك ممكناً، وأربعة قرارات تحكمها:

   ١ · **صنف الكمّية هو الحقل الذي يبرّر السجلّ كلّه.** المعامل بين وحدتين من
       صنفٍ واحد **واقعةٌ فيزيائية**: الكيلوغرام ألف غرام دائماً. وبين صنفين
       مختلفين **ليس معاملاً بل كثافةَ مادّة**: «كم كيلوغراماً في اللتر؟»
       يختلف بين الماء والزيت والرصاص، ويختلف للمادّة الواحدة بالحرارة. فـ
       «كجم ← م» **خطأٌ يُرفض** لا معاملٌ ناقص — والشاشة تقول ذلك قبل
       الإرسال، ثم تعرض رفض الخادم `inventory.unit_class_mismatch` كاملاً.

   ٢ · **المعامل بسطٌ ومقام صحيحان لا عددٌ عشري** — كما في شاشة الأصناف
       حرفاً بحرف: «الحبّة ثلث علبة» = 1/3، ولا يُمثَّل عشرياً بلا خسارة.
       والعددان يُفحص شكلهما **بنمطٍ نصّي قبل** أي تحويل، فلا يمرّ «12.5»
       ولا «1e3» ولا فراغ.

   ٣ · **المسبار يُظهر الرفض رفضاً مُسمّى ولا يُظهره صفراً** (ADR-0073):
       «١٢ حبّة ← كرتون» تُجيب «١»، و«٧ حبّات ← كرتون» **تُرفض** بـ
       `inventory.unit_conversion_not_exact` ولا تُجيب «0.583333». وهذا هو
       سببُ وجود المسبار أصلاً: أن يكون الرفض **جواباً يُقرأ** لا سلوكاً
       يُستنتَج من مستندٍ فشل. **ولا حساب في هذا الملفّ**: المتصفّح لا يقسم
       ولا يضرب ولا يقرّب — الجواب كلّه يأتي من الخادم نصّاً.

   ٤ · **تعطيل الوحدة لا فحص رصيدٍ عليه** — بخلاف تعطيل موضع التسكين
       (ADR-0072): الوحدة ليست بُعداً في مفتاح الرصيد بل **مقياسُ ما فيه**،
       والرصيد المُمسَك بها يبقى مقروءاً ومصروفاً. والتعطيل يمنع **تسجيل
       معاملٍ جديد** عليها لا أكثر. والشاشة تقول ذلك عند الزرّ.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  addUnitConversion,
  addUnitOfMeasure,
  convertQuantity,
  deactivateUnitOfMeasure,
  listUnitConversions,
  listUnitsOfMeasure,
} from "../../api/generated/client";
import { asMagnitude } from "../../api/generated/brands";
import { SCHEMA_Magnitude_RE } from "../../api/generated/formats";
import type { ConversionResult, UnitConversion, UnitOfMeasure } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import {
  Button,
  EmptyState,
  Field,
  Panel,
  QuantityValue,
  StatCard,
  useMoment,
} from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, RefusalNextStep } from "./shared";

/** نمطُ العدد الصحيح الموجب في حدود العقد — نصّيٌّ عمداً، كما في شاشة الأصناف. */
const POSITIVE_INTEGER_RE = /^(?:[1-9][0-9]{0,8}|1000000000)$/;

/** أصناف الكمّية الخمسة، بأسمائها في العقد حرفاً بحرف. */
const CLASSES = ["COUNT", "WEIGHT", "VOLUME", "LENGTH", "AREA"] as const;

/** مفتاح اسم صنف الكمّية في طبقة اللغة. */
const CLASS_LABEL: Readonly<Record<UnitOfMeasure["quantityClass"], string>> = {
  COUNT: "inventory.units.classCount",
  WEIGHT: "inventory.units.classWeight",
  VOLUME: "inventory.units.classVolume",
  LENGTH: "inventory.units.classLength",
  AREA: "inventory.units.classArea",
};

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة وحدات القياس ومعاملات التحويل ومسبار التحويل. */
export function InventoryUnitsScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [arrived, fireArrive] = useMoment("arrive");

  /* تسجيل وحدة. */
  const [code, setCode] = useState("");
  const [arabicName, setArabicName] = useState("");
  const [latinName, setLatinName] = useState("");
  const [quantityClass, setQuantityClass] =
    useState<UnitOfMeasure["quantityClass"]>("COUNT");
  const [pendingOff, setPendingOff] = useState<string | null>(null);

  /* تسجيل معامل. */
  const [fromUnit, setFromUnit] = useState("");
  const [toUnit, setToUnit] = useState("");
  const [numerator, setNumerator] = useState("");
  const [denominator, setDenominator] = useState("1");

  /* المسبار. */
  const [probeMagnitude, setProbeMagnitude] = useState("");
  const [probeFrom, setProbeFrom] = useState("");
  const [probeTo, setProbeTo] = useState("");
  const [answer, setAnswer] = useState<ConversionResult | null>(null);
  const [probeError, setProbeError] = useState<unknown>(null);

  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const scope = [config.baseUrl, config.token, config.companyId] as const;

  const units = useQuery({
    queryKey: ["inventory-units", ...scope],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listUnitsOfMeasure(transport, { companyId: config.companyId }, signal),
  });

  const conversions = useQuery({
    queryKey: ["inventory-unit-conversions", ...scope],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listUnitConversions(transport, { companyId: config.companyId }, signal),
  });

  const unitRows: readonly UnitOfMeasure[] = useMemo(() => units.data?.units ?? [], [units.data]);
  const conversionRows: readonly UnitConversion[] = useMemo(
    () => conversions.data?.conversions ?? [],
    [conversions.data]
  );
  const activeUnits = useMemo(() => unitRows.filter((one) => one.isActive), [unitRows]);

  const ratioBad =
    (numerator !== "" && !POSITIVE_INTEGER_RE.test(numerator)) ||
    (denominator !== "" && !POSITIVE_INTEGER_RE.test(denominator));

  const probeBad = probeMagnitude !== "" && !SCHEMA_Magnitude_RE.test(probeMagnitude);

  /* صنفا الكمّية على طرفَي المعامل — يُقرآن من السجلّ لا يُخمَّنان. والشاشة
     تُظهر الاختلاف **قبل** الإرسال، ثم يبقى رفض الخادم هو الحكم. */
  const classOf = useCallback(
    (unitCode: string) => unitRows.find((one) => one.code === unitCode)?.quantityClass ?? null,
    [unitRows]
  );
  const mixedClasses =
    fromUnit !== "" &&
    toUnit !== "" &&
    classOf(fromUnit) !== null &&
    classOf(toUnit) !== null &&
    classOf(fromUnit) !== classOf(toUnit);

  const addUnit = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      await addUnitOfMeasure(transport, {
        companyId: config.companyId,
        body: { code, name: { ar: arabicName, en: latinName }, quantityClass },
      });
      setCode("");
      setArabicName("");
      setLatinName("");
      fireArrive();
      await units.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [arabicName, code, config.companyId, fireArrive, latinName, quantityClass, transport, units]);

  const deactivate = useCallback(
    async (unitId: string) => {
      setBusy(true);
      setError(null);
      try {
        await deactivateUnitOfMeasure(transport, { companyId: config.companyId, unitId });
        setPendingOff(null);
        await units.refetch();
      } catch (failure) {
        setError(failure);
      } finally {
        setBusy(false);
      }
    },
    [config.companyId, transport, units]
  );

  const addConversion = useCallback(async () => {
    setBusy(true);
    setError(null);
    try {
      /* التحويل الوحيد إلى عدد في هذا الملفّ، و**بعد** فحص الشكل بنمطٍ نصّي:
         العقد ينشر البسط والمقام `integer` بحدٍّ يقع كاملاً داخل المدى الدقيق
         للعائم المزدوج — بخلاف المال والكمّية. */
      await addUnitConversion(transport, {
        companyId: config.companyId,
        body: {
          fromUnit,
          toUnit,
          numerator: Number(numerator),
          denominator: Number(denominator),
        },
      });
      setNumerator("");
      setDenominator("1");
      fireArrive();
      await conversions.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, conversions, denominator, fireArrive, fromUnit, numerator, toUnit, transport]);

  const runProbe = useCallback(async () => {
    setBusy(true);
    setProbeError(null);
    setAnswer(null);
    try {
      const result = await convertQuantity(transport, {
        companyId: config.companyId,
        body: {
          quantity: { magnitude: asMagnitude(probeMagnitude), unit: probeFrom },
          toUnit: probeTo,
        },
      });
      setAnswer(result);
    } catch (failure) {
      setProbeError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, probeFrom, probeMagnitude, probeTo, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  const unitReady = code !== "" && arabicName !== "" && latinName !== "";
  const conversionReady =
    fromUnit !== "" && toUnit !== "" && numerator !== "" && denominator !== "" && !ratioBad;
  const probeReady = probeMagnitude !== "" && !probeBad && probeFrom !== "" && probeTo !== "";

  return (
    <section className="stack" data-testid="inventory-units-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.units.title")}</h1>
          <p className="sub">{t("inventory.units.lede")}</p>
        </div>
      </header>

      <div className="stats-row" data-testid="units-stats">
        <StatCard
          label={t("inventory.units.statUnits")}
          count={unitRows.length}
          testId="stat-units"
        />
        <StatCard
          label={t("inventory.units.statConversions")}
          count={conversionRows.length}
          hint={t("inventory.units.statConversionsHint")}
          testId="stat-conversions"
        />
        <StatCard
          label={t("inventory.units.statActive")}
          count={activeUnits.length}
          testId="stat-units-active"
        />
      </div>

      <div className="statline">
        <span className="spacer" />
        <div className="inline-group">
          <Button
            label={t("common.action.refresh")}
            onClick={() => {
              void units.refetch();
              void conversions.refetch();
            }}
            testId="units-reload"
          />
        </div>
      </div>

      {units.isPending && units.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {units.isError ? (
        <ProblemPanel error={units.error} onRetry={() => void units.refetch()} />
      ) : null}
      {conversions.isError ? <ProblemPanel error={conversions.error} /> : null}

      {/* ─────────────────────────── المسبار: أوّلاً لأنه ما يُجرَّب لا ما يُسجَّل */}
      <Panel
        title={t("inventory.units.probeTitle")}
        note={t("inventory.units.probeNote")}
        testId="conversion-probe"
      >
        <div className="grid fields-3">
          <Field
            id="pb-magnitude"
            label={t("inventory.movements.magnitude")}
            hint={t("inventory.movements.magnitudeHint")}
            error={probeBad ? t("inventory.movements.magnitudeBad") : undefined}
            required
          >
            <input
              id="pb-magnitude"
              className={"ctl amt-input" + (probeBad ? " is-invalid" : "")}
              dir="ltr"
              inputMode="decimal"
              autoComplete="off"
              aria-invalid={probeBad}
              data-testid="probe-magnitude"
              value={probeMagnitude}
              onChange={(e) => setProbeMagnitude(e.target.value)}
            />
          </Field>
          <Field
            id="pb-from"
            label={t("inventory.units.probeFrom")}
            hint={t("inventory.units.pickUnitHint")}
            required
          >
            <select
              id="pb-from"
              className="ctl mono"
              data-testid="probe-from"
              value={probeFrom}
              onChange={(e) => setProbeFrom(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {unitRows.map((one) => (
                <option key={one.id} value={one.code}>
                  {one.code}
                </option>
              ))}
            </select>
          </Field>
          <Field
            id="pb-to"
            label={t("inventory.units.probeTo")}
            hint={t("inventory.units.pickUnitHint")}
            required
          >
            <select
              id="pb-to"
              className="ctl mono"
              data-testid="probe-to"
              value={probeTo}
              onChange={(e) => setProbeTo(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {unitRows.map((one) => (
                <option key={one.id} value={one.code}>
                  {one.code}
                </option>
              ))}
            </select>
          </Field>
        </div>

        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <Button
            label={t("inventory.units.probeRun")}
            kind="primary"
            disabled={!probeReady || busy}
            loading={busy}
            onClick={() => void runProbe()}
            testId="probe-run"
          />
        </div>

        {answer ? (
          <div className={"inv-probe " + arrived} data-testid="probe-answer">
            <QuantityValue
              magnitude={answer.from.magnitude}
              unit={answer.from.unit}
              testId="probe-from-value"
            />
            <span className="inv-leg__sep" aria-hidden="true">{"="}</span>
            <QuantityValue
              magnitude={answer.to.magnitude}
              unit={answer.to.unit}
              testId="probe-to-value"
            />
            <span className="inv-probe__ratio" dir="ltr" data-testid="probe-factor">
              {String(answer.numerator) + "/" + String(answer.denominator)}
            </span>
            <span className="pill pill--info" data-testid="probe-class">
              {t(CLASS_LABEL[answer.quantityClass])}
            </span>
          </div>
        ) : null}

        {probeError ? (
          <>
            <p
              className="alert alert--danger cine-refuse"
              role="status"
              data-testid="probe-refused"
            >
              {t("inventory.units.probeRefused")}
            </p>
            <ProblemPanel error={probeError} />
            <RefusalNextStep error={probeError} />
          </>
        ) : null}

        <p className="muted">{t("inventory.units.probeNoRound")}</p>
      </Panel>

      {/* ─────────────────────────────────────────────── سجلّ وحدات القياس */}
      {units.data && unitRows.length === 0 ? (
        <EmptyState
          title={t("inventory.units.emptyUnits")}
          body={t("inventory.units.emptyUnitsBody")}
          testId="units-empty"
        />
      ) : null}

      {unitRows.length > 0 ? (
        <Panel
          title={t("inventory.units.unitsTitle")}
          note={t("inventory.units.unitsNote")}
          testId="units-panel"
        >
          <div className="ledger" data-state="ready" data-testid="units-table">
            <table>
              <caption className="visually-hidden">{t("inventory.units.unitsTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.reg.colCode")}</th>
                  <th scope="col">{t("inventory.reg.colName")}</th>
                  <th scope="col">{t("inventory.units.colClass")}</th>
                  <th scope="col">{t("inventory.reg.colState")}</th>
                  <th scope="col">{t("inventory.reg.colAction")}</th>
                </tr>
              </thead>
              <tbody>
                {unitRows.map((one) => (
                  <tr key={one.id} data-testid="unit-row" data-active={String(one.isActive)}>
                    <td className="code">{one.code}</td>
                    <td>
                      <span lang="ar" dir="rtl">{one.name.ar}</span>
                      <span className="alt" lang="en" dir="ltr">{one.name.en}</span>
                    </td>
                    <td data-class={one.quantityClass}>{t(CLASS_LABEL[one.quantityClass])}</td>
                    <td>
                      <span
                        className={"pill " + (one.isActive ? "pill--posted" : "pill--archived")}
                        data-testid="unit-state"
                      >
                        {one.isActive
                          ? t("inventory.reg.stateActive")
                          : t("inventory.reg.stateOff")}
                      </span>
                    </td>
                    <td>
                      {!one.isActive ? (
                        <span className="muted">{t("inventory.reg.alreadyOff")}</span>
                      ) : pendingOff === one.id ? (
                        <div className="inline-group">
                          <Button
                            label={t("inventory.reg.confirmOff")}
                            kind="danger"
                            size="sm"
                            disabled={busy}
                            onClick={() => void deactivate(one.id)}
                            testId="unit-confirm-off"
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
                            setPendingOff(one.id);
                            setError(null);
                          }}
                          testId="unit-deactivate"
                        />
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          <p className="muted" data-testid="unit-off-rule">{t("inventory.units.unitOffRule")}</p>
        </Panel>
      ) : null}

      <Panel
        title={t("inventory.units.addUnit")}
        note={t("inventory.units.addUnitNote")}
        testId="unit-form"
      >
        <div className="grid fields-4">
          <Field
            id="um-code"
            label={t("inventory.units.code")}
            hint={t("inventory.units.codeHint")}
            required
          >
            <input
              id="um-code"
              className="ctl mono"
              autoComplete="off"
              data-testid="unit-code"
              value={code}
              onChange={(e) => setCode(e.target.value)}
            />
          </Field>
          <Field
            id="um-name-ar"
            label={t("inventory.items.arabicName")}
            hint={t("inventory.reg.arabicNameHint")}
            required
          >
            <input
              id="um-name-ar"
              className="ctl"
              lang="ar"
              autoComplete="off"
              data-testid="unit-name-ar"
              value={arabicName}
              onChange={(e) => setArabicName(e.target.value)}
            />
          </Field>
          <Field
            id="um-name-en"
            label={t("inventory.items.englishName")}
            hint={t("inventory.reg.latinNameHint")}
            required
          >
            <input
              id="um-name-en"
              className="ctl"
              lang="en"
              dir="ltr"
              autoComplete="off"
              data-testid="unit-name-en"
              value={latinName}
              onChange={(e) => setLatinName(e.target.value)}
            />
          </Field>
          <Field
            id="um-class"
            label={t("inventory.units.quantityClass")}
            hint={t("inventory.units.quantityClassHint")}
            required
          >
            <select
              id="um-class"
              className="ctl"
              data-testid="unit-class"
              value={quantityClass}
              onChange={(e) =>
                setQuantityClass(e.target.value as UnitOfMeasure["quantityClass"])
              }
            >
              {CLASSES.map((one) => (
                <option key={one} value={one}>
                  {t(CLASS_LABEL[one])}
                </option>
              ))}
            </select>
          </Field>
        </div>
        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <Button
            label={t("inventory.units.submitUnit")}
            kind="primary"
            disabled={!unitReady || busy}
            loading={busy}
            onClick={() => void addUnit()}
            testId="unit-submit"
          />
        </div>
      </Panel>

      {/* ───────────────────────────────────────────── معاملات التحويل */}
      {conversions.data && conversionRows.length === 0 ? (
        <EmptyState
          small
          title={t("inventory.units.emptyConversions")}
          body={t("inventory.units.emptyConversionsBody")}
          testId="conversions-empty"
        />
      ) : null}

      {conversionRows.length > 0 ? (
        <Panel
          title={t("inventory.units.conversionsTitle")}
          note={t("inventory.units.conversionsNote")}
          testId="conversions-panel"
        >
          <div className="ledger" data-state="ready" data-testid="conversions-table">
            <table>
              <caption className="visually-hidden">
                {t("inventory.units.conversionsTitle")}
              </caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.units.colConvFrom")}</th>
                  <th scope="col">{t("inventory.units.colConvTo")}</th>
                  <th scope="col">{t("inventory.units.colClass")}</th>
                  <th scope="col" className="n">{t("inventory.items.colNumerator")}</th>
                  <th scope="col" className="n">{t("inventory.items.colDenominator")}</th>
                  <th scope="col">{t("inventory.units.colMeans")}</th>
                </tr>
              </thead>
              <tbody>
                {conversionRows.map((one) => (
                  <tr key={one.id} data-testid="conversion-row">
                    <td className="code">{one.fromUnit}</td>
                    <td className="code">{one.toUnit}</td>
                    <td data-class={one.quantityClass}>{t(CLASS_LABEL[one.quantityClass])}</td>
                    <td className="n mono" dir="ltr">{String(one.numerator)}</td>
                    <td className="n mono" dir="ltr">{String(one.denominator)}</td>
                    <td>
                      {t("inventory.units.means", {
                        from: one.fromUnit,
                        to: one.toUnit,
                        numerator: String(one.numerator),
                        denominator: String(one.denominator),
                      })}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      ) : null}

      <Panel
        title={t("inventory.units.addConversion")}
        note={t("inventory.units.addConversionNote")}
        testId="conversion-form"
      >
        <div className="grid fields-4">
          <Field
            id="uc-from"
            label={t("inventory.units.colConvFrom")}
            hint={t("inventory.units.pickUnitHint")}
            required
          >
            <select
              id="uc-from"
              className="ctl mono"
              data-testid="conversion-from"
              value={fromUnit}
              onChange={(e) => setFromUnit(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {activeUnits.map((one) => (
                <option key={one.id} value={one.code}>
                  {one.code}
                </option>
              ))}
            </select>
          </Field>
          <Field
            id="uc-to"
            label={t("inventory.units.colConvTo")}
            hint={t("inventory.units.pickUnitHint")}
            error={mixedClasses ? t("inventory.units.classRule") : undefined}
            required
          >
            <select
              id="uc-to"
              className={"ctl mono" + (mixedClasses ? " is-invalid" : "")}
              aria-invalid={mixedClasses}
              data-testid="conversion-to"
              value={toUnit}
              onChange={(e) => setToUnit(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {activeUnits.map((one) => (
                <option key={one.id} value={one.code}>
                  {one.code}
                </option>
              ))}
            </select>
          </Field>
          <Field
            id="uc-num"
            label={t("inventory.items.numerator")}
            hint={t("inventory.units.ratioHint")}
            required
          >
            <input
              id="uc-num"
              className={"ctl mono" + (ratioBad ? " is-invalid" : "")}
              dir="ltr"
              inputMode="numeric"
              autoComplete="off"
              aria-invalid={ratioBad}
              data-testid="conversion-numerator"
              value={numerator}
              onChange={(e) => setNumerator(e.target.value)}
            />
          </Field>
          <Field
            id="uc-den"
            label={t("inventory.items.denominator")}
            hint={t("inventory.units.ratioHint")}
            required
          >
            <input
              id="uc-den"
              className={"ctl mono" + (ratioBad ? " is-invalid" : "")}
              dir="ltr"
              inputMode="numeric"
              autoComplete="off"
              aria-invalid={ratioBad}
              data-testid="conversion-denominator"
              value={denominator}
              onChange={(e) => setDenominator(e.target.value)}
            />
          </Field>
        </div>

        {mixedClasses ? (
          <p
            className="alert alert--danger cine-refuse"
            role="status"
            data-testid="conversion-class-warning"
          >
            {t("inventory.units.classRule")}
          </p>
        ) : null}

        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <Button
            label={t("inventory.units.submitConversion")}
            kind="primary"
            disabled={!conversionReady || busy}
            loading={busy}
            onClick={() => void addConversion()}
            testId="conversion-submit"
          />
        </div>
      </Panel>

      {error ? (
        <>
          <ProblemPanel error={error} />
          <RefusalNextStep error={error} />
        </>
      ) : null}

      <p className="muted" data-testid="units-register-note">
        {t("inventory.units.registerNote")}
      </p>
    </section>
  );
}
