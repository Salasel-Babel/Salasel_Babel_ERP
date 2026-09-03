/* ═══════════════════════════════════════════════════════════════════════════
   حالُ الصنف وصيانتُه — يُقرأ، ويُصحَّح تعريفُه، ويُوقَف عن التداول
   An item's state and upkeep — read it, correct its definition, discontinue it
   ───────────────────────────────────────────────────────────────────────────
   السؤال الذي تجيبه هذه الشاشة واحد: **«هذا الصنف: أمتداوَلٌ هو، وهل بقي له
   رصيد وفي كم موضع؟ وأُصحّح تعريفه أم أُوقفه؟»** وهي **شاشةٌ واحدة لا
   شاشتان**، والسبب مقيس: `ADR-0080` يجعل الحدَّ **نموذجَي كتابة**، وهذان
   اثنان بالضبط — تصحيحٌ وإيقاف. وفصلُهما كان سيجعل من يجلس أمام صنفٍ يفتح
   صفحتين ليجيب سؤالاً واحداً، **ويقرأ دورة الحياة مرّتين** — وهي القراءة
   التي يتوقّف عليها الجوابان معاً.

   وخمسة قرارات تحكمها، وكلّها مقروءة من العقد لا مفترَضة:

   ١ · **التعطيل حالةٌ تُقرأ لا غياب.** `deactivateItem` لا يحذف، والصنف
       المُعطَّل **يبقى في القائمة** ولا يُخفى خلف مرشّحٍ افتراضي: ما يختفي
       يُظنّ محذوفاً، ثمّ يُرفض تسجيل رمزه ثانيةً بتكرارٍ لا يفهمه أحد.

   ٢ · **ويُقبل التعطيل وللصنف رصيد** — وهذا **يخالف عمداً** حكمَ
       `deactivateWarehouse` و`deactivateStorageLocation` اللذين يُرفضان فوق
       رصيد (`ADR-0072`). والنصّان مختلفان لأن الحكمين مختلفان: رفضُ تعطيل
       الصنف فوق رصيدٍ يصنع **دائرةً مغلقة** — لا يُوقَف حتى ينفد، ولا ينفد
       إلا ببيعٍ يقتضي أن يكون عاملاً. **ولا يُوحَّد النصّان**، وحارسٌ في
       `web/tests/` يمنع توحيدهما.

   ٣ · **والتعطيل ليس صامتاً**: الجواب يحمل `holdsStock` و`placementsWithStock`،
       فتعرضهما الشاشة بعده — كي لا يظنّ أحدٌ أن البضاعة ذهبت مع الإيقاف.
       وأثرُه يُقال بنصّه: **الوارد الجديد يُرفض بـ`inventory.item_inactive`،
       والصادر يبقى حتى ينفد الرصيد**.

   ٤ · **وحدة الأساس تُقفَل بالتاريخ لا بالمبدأ.** فحين يكون للصنف رصيد،
       تغييرُها **رفضٌ مؤكَّد** (`inventory.base_unit_locked_by_history`) —
       فتُعطَّل الخانة ويُقال السبب باسمه **قبل** الضغط. وحين لا رصيد له،
       **قد تكون عليه حركاتٌ لا يعرفها المتصفّح**، فالشاشة لا تدّعي المعرفة:
       تسمح، وتقول إن الحكم عند الخادم. وادّعاءُ يقينٍ لا يملكه العميل أسوأ
       من الاعتراف بحدّه.

   ٥ · **ولا رمز صنفٍ يُعدَّل**: العقد يقرؤه من المسار ولا يقبله في الجسم،
       لأنه هوية تحملها قيود سنةٍ مضت. فالخانة معروضةٌ ومقفلة، والسبب مكتوب.

   ⚠ **ونقصُ سطحٍ مُعلَن**: `listItems` **لا يقول أيّ أصنافه مُوقَف** — الحالة
   موردٌ فرعيّ لكل صنف على حدة، بنصّ العقد. فالشاشة تعرض حالة ما قُرئ وحده،
   ولا تخترع حالةً لما لم يُقرأ، ولا تُطلق قراءةً لكل صفٍّ في الكتالوج.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  deactivateItem,
  listItems,
  readItem,
  readItemLifecycle,
  updateItem,
} from "../../api/generated/client";
import type { Item, ItemLifecycle, UnitFactor } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Num, useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel, StatCard } from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, SurfaceGap, problemCodeOf } from "./shared";

/* نمطُ العدد الصحيح الموجب في حدود العقد: من ١ إلى ١٬٠٠٠٬٠٠٠٬٠٠٠. */
const POSITIVE_INTEGER_RE = /^(?:[1-9][0-9]{0,8}|1000000000)$/;

/**
 * رموزٌ يردّها هذا الباب وحده، وخطوةُ كلٍّ منها في هذه الواجهة.
 * **وهي منفصلة عن خريطة القسم** لأن نصّها يخصّ الصنف لا الموضع، وتوحيدُهما
 * هو بالضبط ما يمنعه `ADR-0072`.
 */
const LIFECYCLE_NEXT_STEP: Readonly<Record<string, string>> = {
  "inventory.base_unit_locked_by_history": "inventory.life.nextBaseLocked",
  "inventory.item_inactive": "inventory.life.nextItemInactive",
  "inventory.item_not_found": "inventory.life.nextNotFound",
};

/** الخطوة التالية بعد رفضٍ مُسمّى — إلى جانب رسالة الخادم لا بدلاً منها. */
function LifecycleNextStep(props: { error: unknown }): ReactNode {
  const { t } = useT();
  const code = problemCodeOf(props.error);
  const key = code === null ? undefined : LIFECYCLE_NEXT_STEP[code];
  if (!key) return null;
  return (
    <p
      className="alert alert--warning cine-refuse"
      role="status"
      data-testid="life-next-step"
      data-code={code}
    >
      {t(key)}
    </p>
  );
}

/** سطر وحدةٍ أكبر كما يُحرَّر — العددان **نصّان** حتى الإرسال. */
interface DraftUnit {
  key: string;
  unitCode: string;
  numerator: string;
  denominator: string;
}

let sequence = 0;
function draftOf(units: readonly UnitFactor[]): DraftUnit[] {
  return units.map((unit) => {
    sequence += 1;
    return {
      key: "e" + String(sequence),
      unitCode: unit.unitCode,
      numerator: String(unit.numerator),
      denominator: String(unit.denominator),
    };
  });
}

function newUnit(): DraftUnit {
  sequence += 1;
  return { key: "n" + String(sequence), unitCode: "", numerator: "", denominator: "1" };
}

/* ══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة حال الصنف وصيانته. */
export function InventoryItemLifecycleScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();

  const [query, setQuery] = useState("");
  const [selected, setSelected] = useState<string | null>(null);
  const [item, setItem] = useState<Item | null>(null);
  /* **ما قُرئ يُعرض، وما لم يُقرأ لا يُدّعى.** خريطةٌ لما قُرئت حالته في هذه
     الجلسة — لأن `listItems` لا يحمل الحالة، والقراءةُ لكل صفٍّ في كتالوجٍ
     من آلاف الأصناف طلبٌ لكل صفّ. */
  const [states, setStates] = useState<Readonly<Record<string, ItemLifecycle>>>({});
  const [reading, setReading] = useState(false);
  const [readError, setReadError] = useState<unknown>(null);

  /* حقول التصحيح. */
  const [arabicName, setArabicName] = useState("");
  const [latinName, setLatinName] = useState("");
  const [group, setGroup] = useState("");
  const [baseUnit, setBaseUnit] = useState("");
  const [units, setUnits] = useState<DraftUnit[]>([]);
  const [updated, setUpdated] = useState<Item | null>(null);

  /* الإيقاف. */
  const [confirming, setConfirming] = useState(false);
  const [stopped, setStopped] = useState<ItemLifecycle | null>(null);

  const [writeError, setWriteError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);

  const list = useQuery({
    queryKey: ["item-lifecycle-list", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listItems(transport, { companyId: config.companyId }, signal),
  });

  const items: readonly Item[] = useMemo(() => list.data?.items ?? [], [list.data]);

  const shown = useMemo(() => {
    const needle = query.trim().toLocaleLowerCase();
    if (!needle) return items;
    return items.filter(
      (one) =>
        one.code.toLocaleLowerCase().includes(needle) ||
        one.name.ar.toLocaleLowerCase().includes(needle) ||
        one.name.en.toLocaleLowerCase().includes(needle) ||
        one.itemGroup.toLocaleLowerCase().includes(needle)
    );
  }, [items, query]);

  const lifecycle = selected === null ? undefined : states[selected];

  /** يقرأ الصنف ودورة حياته معاً — البابان يجيبان نصفَي السؤال الواحد. */
  const open = useCallback(
    async (id: string) => {
      setReading(true);
      setReadError(null);
      setWriteError(null);
      setUpdated(null);
      setStopped(null);
      setConfirming(false);
      setSelected(id);
      try {
        const [definition, state] = await Promise.all([
          readItem(transport, { companyId: config.companyId, itemId: id }),
          readItemLifecycle(transport, { companyId: config.companyId, itemId: id }),
        ]);
        setItem(definition);
        setStates((current) => ({ ...current, [id]: state }));
        setArabicName(definition.name.ar);
        setLatinName(definition.name.en);
        setGroup(definition.itemGroup);
        setBaseUnit(definition.baseUnit);
        setUnits(draftOf(definition.units));
      } catch (failure) {
        setItem(null);
        setReadError(failure);
      } finally {
        setReading(false);
      }
    },
    [config.companyId, transport]
  );

  /** يقرأ حالة صفٍّ بلا فتحه — كي تُرى الحالة في القائمة بلا مغادرتها. */
  const peek = useCallback(
    async (id: string) => {
      try {
        const state = await readItemLifecycle(transport, {
          companyId: config.companyId,
          itemId: id,
        });
        setStates((current) => ({ ...current, [id]: state }));
      } catch (failure) {
        setReadError(failure);
      }
    },
    [config.companyId, transport]
  );

  const badRatios = useMemo(
    () =>
      units
        .filter(
          (unit) =>
            !POSITIVE_INTEGER_RE.test(unit.numerator) || !POSITIVE_INTEGER_RE.test(unit.denominator)
        )
        .map((unit) => unit.key),
    [units]
  );

  const baseChanged = item !== null && baseUnit !== item.baseUnit;
  /* رفضٌ **مؤكَّد**: رصيدٌ قائم يُقفل وحدة الأساس بنصّ العقد. */
  const baseLocked = lifecycle !== undefined && lifecycle.holdsStock;

  const readyToUpdate =
    item !== null &&
    arabicName !== "" &&
    latinName !== "" &&
    group !== "" &&
    baseUnit !== "" &&
    units.every((unit) => unit.unitCode !== "") &&
    badRatios.length === 0 &&
    !(baseChanged && baseLocked);

  const update = useCallback(
    (key: string, patch: Partial<DraftUnit>) => {
      setUnits((current) => current.map((u) => (u.key === key ? { ...u, ...patch } : u)));
    },
    []
  );

  const submitRevision = useCallback(async () => {
    if (!item) return;
    setBusy(true);
    setWriteError(null);
    try {
      /* التحويل الوحيد إلى عدد في هذا الملفّ، **وبعد** فحص الشكل بنمطٍ نصّي:
         البسط والمقام `integer` بحدٍّ يقع كاملاً داخل المدى الدقيق للعائم
         المزدوج، بخلاف المال والكمّية. */
      const wireUnits: UnitFactor[] = units.map((unit) => ({
        unitCode: unit.unitCode,
        numerator: Number(unit.numerator),
        denominator: Number(unit.denominator),
      }));
      const next = await updateItem(transport, {
        companyId: config.companyId,
        itemId: item.id,
        body: {
          name: { ar: arabicName, en: latinName },
          itemGroup: group,
          baseUnit,
          units: wireUnits,
        },
      });
      setItem(next);
      setUpdated(next);
      setUnits(draftOf(next.units));
      await list.refetch();
    } catch (failure) {
      setWriteError(failure);
    } finally {
      setBusy(false);
    }
  }, [arabicName, baseUnit, config.companyId, group, item, latinName, list, transport, units]);

  const stop = useCallback(async () => {
    if (!item) return;
    setBusy(true);
    setWriteError(null);
    try {
      const state = await deactivateItem(transport, {
        companyId: config.companyId,
        itemId: item.id,
      });
      setStates((current) => ({ ...current, [item.id]: state }));
      setStopped(state);
      setConfirming(false);
    } catch (failure) {
      setWriteError(failure);
    } finally {
      setBusy(false);
    }
  }, [config.companyId, item, transport]);

  if (config.companyId === "") return <ChooseCompanyFirst />;

  return (
    <section className="stack" data-testid="item-lifecycle-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.life.title")}</h1>
          <p className="sub">{t("inventory.life.lede")}</p>
        </div>
      </header>

      <div className="filterbar" role="search">
        <Field
          id="life-search"
          label={t("inventory.life.search")}
          hint={t("inventory.life.searchHint")}
        >
          <input
            id="life-search"
            className="ctl"
            type="search"
            data-testid="life-search"
            value={query}
            onChange={(e) => setQuery(e.target.value)}
          />
        </Field>
        <div className="rowctl">
          <div className="inline-group">
            <Button
              label={t("common.action.refresh")}
              onClick={() => void list.refetch()}
              testId="life-reload"
            />
          </div>
        </div>
      </div>

      {list.isPending && list.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {list.isError ? (
        <ProblemPanel error={list.error} onRetry={() => void list.refetch()} />
      ) : null}

      {list.data && items.length === 0 ? (
        <EmptyState
          title={t("inventory.life.emptyTitle")}
          body={t("inventory.life.emptyBody")}
          testId="life-empty"
        />
      ) : null}

      {shown.length > 0 ? (
        <Panel
          title={t("inventory.life.catalogue")}
          note={t("inventory.life.catalogueNote")}
          testId="life-list-panel"
        >
          <div className="ledger" data-state="ready" data-testid="life-table">
            <table>
              <caption className="visually-hidden">{t("inventory.life.catalogue")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.items.colCode")}</th>
                  <th scope="col">{t("inventory.items.colName")}</th>
                  <th scope="col">{t("inventory.items.colGroup")}</th>
                  <th scope="col">{t("inventory.life.colState")}</th>
                  <th scope="col">{t("inventory.reg.colAction")}</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((one) => {
                  const known = states[one.id];
                  return (
                    <tr
                      key={one.id}
                      data-testid="life-row"
                      data-selected={one.id === selected ? "true" : undefined}
                      data-active={known === undefined ? undefined : String(known.isActive)}
                    >
                      <td className="code">
                        <button
                          type="button"
                          className="btn btn-ghost btn-sm mono"
                          data-testid="life-pick"
                          aria-pressed={one.id === selected}
                          onClick={() => void open(one.id)}
                        >
                          {one.code}
                        </button>
                      </td>
                      <td>
                        <span lang="ar" dir="rtl">{one.name.ar}</span>
                        <span className="alt" lang="en" dir="ltr">{one.name.en}</span>
                      </td>
                      <td className="code">{one.itemGroup}</td>
                      <td>
                        {known === undefined ? (
                          <span className="muted" data-testid="life-state-unknown">
                            {t("inventory.life.stateUnknown")}
                          </span>
                        ) : (
                          <span
                            className={"pill " + (known.isActive ? "pill--posted" : "pill--archived")}
                            data-testid="life-state"
                          >
                            {known.isActive
                              ? t("inventory.reg.stateActive")
                              : t("inventory.reg.stateOff")}
                          </span>
                        )}
                      </td>
                      <td>
                        <Button
                          label={t("inventory.life.peek")}
                          size="sm"
                          onClick={() => void peek(one.id)}
                          testId="life-peek"
                        />
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <p className="muted" data-testid="life-list-gap">{t("inventory.life.listGap")}</p>
        </Panel>
      ) : null}

      {reading ? <ReadingSkeleton testId="life-reading" /> : null}
      {readError ? (
        <>
          <ProblemPanel error={readError} />
          <LifecycleNextStep error={readError} />
        </>
      ) : null}

      {item && lifecycle ? (
        <div className="stats-row" data-testid="life-stats">
          <StatCard
            label={t("inventory.life.statState")}
            count={lifecycle.isActive ? t("inventory.reg.stateActive") : t("inventory.reg.stateOff")}
            tone={lifecycle.isActive ? "good" : "neutral"}
            hint={t("inventory.life.statStateHint")}
            testId="stat-life-state"
          />
          <StatCard
            label={t("inventory.life.statStock")}
            count={lifecycle.holdsStock ? t("inventory.life.yes") : t("inventory.life.no")}
            tone={lifecycle.holdsStock ? "bad" : "good"}
            hint={t("inventory.life.statStockHint")}
            testId="stat-life-stock"
          />
          <StatCard
            label={t("inventory.life.statPlaces")}
            count={lifecycle.placementsWithStock}
            hint={t("inventory.life.statPlacesHint")}
            testId="stat-life-places"
          />
        </div>
      ) : null}

      {item ? (
        <Panel
          title={t("inventory.life.revise")}
          note={t("inventory.life.reviseNote")}
          aside={<span className="pill pill--info mono">{item.code}</span>}
          testId="life-revise-form"
        >
          <div className="grid fields-3">
            <Field
              id="life-code"
              label={t("inventory.items.code")}
              hint={t("inventory.life.codeLocked")}
            >
              <input
                id="life-code"
                className="ctl mono"
                dir="ltr"
                readOnly
                data-testid="life-code"
                value={item.code}
              />
            </Field>
            <Field
              id="life-group"
              label={t("inventory.items.group")}
              hint={t("inventory.life.groupHint")}
            >
              <input
                id="life-group"
                className="ctl mono"
                dir="ltr"
                autoComplete="off"
                data-testid="life-group"
                value={group}
                onChange={(e) => setGroup(e.target.value)}
              />
            </Field>
            <Field
              id="life-base"
              label={t("inventory.items.baseUnit")}
              hint={
                baseLocked ? t("inventory.life.baseLockedHint") : t("inventory.life.baseFreeHint")
              }
              error={
                baseChanged && baseLocked ? t("inventory.life.baseLockedNow") : undefined
              }
            >
              <input
                id="life-base"
                className={"ctl mono" + (baseChanged && baseLocked ? " is-invalid" : "")}
                autoComplete="off"
                disabled={baseLocked}
                aria-invalid={baseChanged && baseLocked}
                data-testid="life-base"
                value={baseUnit}
                onChange={(e) => setBaseUnit(e.target.value)}
              />
            </Field>
          </div>

          <div className="grid fields-half">
            <Field
              id="life-name-ar"
              label={t("inventory.items.arabicName")}
              hint={t("inventory.reg.arabicNameHint")}
              required
            >
              <input
                id="life-name-ar"
                className="ctl"
                lang="ar"
                data-testid="life-name-ar"
                value={arabicName}
                onChange={(e) => setArabicName(e.target.value)}
              />
            </Field>
            <Field
              id="life-name-en"
              label={t("inventory.items.englishName")}
              hint={t("inventory.reg.latinNameHint")}
              required
            >
              <input
                id="life-name-en"
                className="ctl"
                lang="en"
                dir="ltr"
                data-testid="life-name-en"
                value={latinName}
                onChange={(e) => setLatinName(e.target.value)}
              />
            </Field>
          </div>

          <h3 className="card-hd"><strong>{t("inventory.items.ladder")}</strong></h3>
          <p className="muted">{t("inventory.life.unitsReplace")}</p>

          {units.length === 0 ? (
            <p className="muted" data-testid="life-no-units">{t("inventory.items.noUnits")}</p>
          ) : null}

          <div className="stack">
            {units.map((unit) => (
              <fieldset key={unit.key} className="card card-pad" data-testid="life-unit-row">
                <div className="grid fields-4">
                  <Field
                    id={"life-u-" + unit.key}
                    label={t("inventory.items.unitCode")}
                    hint={t("inventory.life.unitCodeHint")}
                  >
                    <input
                      id={"life-u-" + unit.key}
                      className="ctl mono"
                      autoComplete="off"
                      data-testid="life-unit-code"
                      value={unit.unitCode}
                      onChange={(e) => update(unit.key, { unitCode: e.target.value })}
                    />
                  </Field>
                  <Field
                    id={"life-n-" + unit.key}
                    label={t("inventory.items.numerator")}
                    hint={t("inventory.life.numeratorHint")}
                    error={badRatios.includes(unit.key) ? t("inventory.life.ratioBad") : undefined}
                  >
                    <input
                      id={"life-n-" + unit.key}
                      className={"ctl mono" + (badRatios.includes(unit.key) ? " is-invalid" : "")}
                      dir="ltr"
                      inputMode="numeric"
                      autoComplete="off"
                      aria-invalid={badRatios.includes(unit.key)}
                      data-testid="life-unit-numerator"
                      value={unit.numerator}
                      onChange={(e) => update(unit.key, { numerator: e.target.value })}
                    />
                  </Field>
                  <Field
                    id={"life-d-" + unit.key}
                    label={t("inventory.items.denominator")}
                    hint={t("inventory.life.denominatorHint")}
                    error={badRatios.includes(unit.key) ? t("inventory.life.ratioBad") : undefined}
                  >
                    <input
                      id={"life-d-" + unit.key}
                      className={"ctl mono" + (badRatios.includes(unit.key) ? " is-invalid" : "")}
                      dir="ltr"
                      inputMode="numeric"
                      autoComplete="off"
                      aria-invalid={badRatios.includes(unit.key)}
                      data-testid="life-unit-denominator"
                      value={unit.denominator}
                      onChange={(e) => update(unit.key, { denominator: e.target.value })}
                    />
                  </Field>
                  <div className="rowctl">
                    <Button
                      label={t("inventory.items.removeUnit")}
                      kind="danger"
                      size="sm"
                      onClick={() => setUnits((c) => c.filter((u) => u.key !== unit.key))}
                    />
                  </div>
                </div>
              </fieldset>
            ))}
          </div>

          <button
            type="button"
            className="addline"
            data-testid="life-add-unit"
            onClick={() => setUnits((c) => [...c, newUnit()])}
          >
            {t("inventory.items.addUnit")}
          </button>

          <div className="inline-group">
            <Button
              label={busy ? t("common.state.loading") : t("inventory.life.reviseSubmit")}
              kind="primary"
              disabled={!readyToUpdate || busy}
              loading={busy}
              onClick={() => void submitRevision()}
              testId="life-revise-submit"
            />
          </div>

          {updated ? (
            <p className="alert alert--success" role="status" data-testid="life-updated">
              {t("inventory.life.updated")}
            </p>
          ) : null}
        </Panel>
      ) : null}

      {item ? (
        <Panel
          title={t("inventory.life.stop")}
          note={t("inventory.life.stopNote")}
          testId="life-stop-form"
        >
          {/* ⚠ نصُّ حكم الصنف **مستقلٌّ عن نصّ حكم الموضع** عمداً — ADR-0072. */}
          <p className="alert alert--info" role="status" data-testid="life-off-rule">
            {t("inventory.life.offRuleItem")}
          </p>

          {lifecycle && !lifecycle.isActive ? (
            <p className="muted" data-testid="life-already-off">
              {t("inventory.life.alreadyOff")}
            </p>
          ) : confirming ? (
            <div className="inline-group" data-testid="life-confirm">
              <Button
                label={t("inventory.reg.confirmOff")}
                kind="danger"
                disabled={busy}
                loading={busy}
                onClick={() => void stop()}
                testId="life-confirm-off"
              />
              <Button
                label={t("common.action.cancel")}
                onClick={() => setConfirming(false)}
                testId="life-cancel-off"
              />
            </div>
          ) : (
            <div className="inline-group">
              <Button
                label={t("inventory.reg.deactivate")}
                kind="danger"
                disabled={busy}
                onClick={() => {
                  setConfirming(true);
                  setWriteError(null);
                }}
                testId="life-deactivate"
              />
            </div>
          )}

          {stopped ? (
            <section className="alert alert--success" role="status" data-testid="life-stopped">
              <p>{t("inventory.life.stopped")}</p>
              <div className="kv">
                <div>
                  <div className="k">{t("inventory.life.statStock")}</div>
                  <div className="v">
                    {stopped.holdsStock ? t("inventory.life.yes") : t("inventory.life.no")}
                  </div>
                </div>
                <div>
                  <div className="k">{t("inventory.life.statPlaces")}</div>
                  <div className="v n"><Num value={stopped.placementsWithStock} /></div>
                </div>
              </div>
              <p className="muted">{t("inventory.life.stoppedWhatNow")}</p>
            </section>
          ) : null}
        </Panel>
      ) : null}

      {writeError ? (
        <>
          <ProblemPanel error={writeError} />
          <LifecycleNextStep error={writeError} />
        </>
      ) : null}

      <SurfaceGap
        title={t("inventory.life.gapTitle")}
        body={t("inventory.life.gapBody")}
        owed={t("inventory.life.gapOwed")}
        testId="life-gap-no-reactivate"
      />
    </section>
  );
}
