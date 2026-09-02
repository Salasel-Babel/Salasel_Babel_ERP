/* ═══════════════════════════════════════════════════════════════════════════
   مستندات حركة المخزون — المسوّدة، ثم الترحيل الذي يجعلها واقعة
   Stock movement documents — the draft, then the posting that makes it a fact
   ───────────────────────────────────────────────────────────────────────────
   هذه ثاني شاشةٍ **تكتب** في هذا المنتج بعد قيد اليومية، وأول شاشةٍ تكتب
   كمّية. وستّة قرارات تحكمها:

   ١ · **الكمّية تحمل وحدتها دائماً.** المقدار نصٌّ يُفحص بالنحو المنشور
       (`Magnitude`: ستّ خانات) ويعبر السلك نصّاً؛ والوحدة تُختار **من سلّم
       وحدات الصنف المختار وحده**. ولا خيار «وحدة أخرى» في هذه الشاشة: وحدةٌ
       بلا معامل ترفضها الوحدة بـ`inventory.unit_not_convertible` — وعرضُ
       خيارٍ يُعرَف سلفاً أنه سيُرفض إهانةٌ لا خدمة.

   ٢ · **الاتجاهان يُقرآن من العقد وقت التشغيل**، لا يُكتبان هنا. قائمةٌ
       مكتوبة بيد تنحرف عند أول إضافة فتُرسل عضواً لا يعرفه الخادم.

   ٣ · **التكلفة على الوارد وحده.** الصادر تحسب الوحدة تكلفته بالمتوسط
       المرجّح المتحرّك ولا تُملى (ADR-0039)، فيُرسَل عليه "0". والحقل هنا
       **مُقفَل لا مخفيّ**: القاعدة تُرى فتُفهَم، وإخفاؤها يجعلها سحراً.

   ٤ · **الموقع قرارٌ يُتَّخذ لا حقلٌ يُترك فارغاً.** العقد يعرف `DEFAULT`
       لمستودعٍ لم يُسكَّن، فالشاشة تعرضه **خياراً صريحاً باسمه ومعناه** بدل
       أن تملأه صامتةً — والملء الصامت هو بعينه «اختراع افتراضٍ ليبدو النموذج
       مكتملاً».

   ٥ · **مجموعة الصنف تأتي من الكتالوج ولا تُكتب.** هي مؤهّل الدور عند
       مصفوفة الترحيل، وكتابتها بيدٍ هنا تفتح باب انحرافها عن الصنف نفسه.
       وتُوسَم `defaulted` — أي «من الإعدادات» — فيعرف القارئ مصدرها.

   ٦ · **الترحيل حصينٌ ضد التكرار، والشاشة تقول ذلك.** الوصول الثاني بالهوية
       نفسها يعيد المستند ذاته و`alreadyPosted = true`؛ وعرضُ نجاحٍ ثانٍ
       يُقرأ «رُحِّل مرّتين»، وهو أسوأ من رسالة غامضة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type CSSProperties, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import {
  draftStockMovement,
  listItems,
  listStockMovements,
  postStockMovement,
} from "../../api/generated/client";
import type { Item, StockMovement, StockMovementRequest } from "../../api/generated/types";
import { SCHEMAS } from "../../api/generated/runtime-schema";
import { SCHEMA_Magnitude_RE, SCHEMA_Money_RE } from "../../api/generated/formats";
import { asMagnitude } from "../../api/generated/brands";
import { Money } from "../../api/money";
import { useApi } from "../../app/api-context";
import { Amount, useT } from "../../i18n/react";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import {
  Button,
  EmptyState,
  MOTION,
  Panel,
  ProvenanceMark,
  QuantityValue,
  StatusBadge,
  useMoment,
} from "../../ui";
import { ChooseCompanyFirst, ReadingSkeleton, RefusalNextStep, SurfaceGap } from "./shared";

/* ═════════════════════════════ المجموعات المغلقة من العقد لا من اليد */

/** أعضاء مجموعةٍ مغلقة كما ينشرها العقد لحقلٍ بعينه. */
function members(schema: string, field: string): readonly string[] {
  const found = SCHEMAS[schema]?.fields[field]?.e;
  if (!found || found.length === 0) {
    throw new TypeError(
      "الحقل " + schema + "." + field + " ليس مجموعة مغلقة في العقد المُولَّد. " +
        "/ is not a closed set in the generated contract."
    );
  }
  return found;
}

const DIRECTIONS = members("StockMovementRequest", "direction");

/** الوارد والصادر بأسمائهما في العقد لا بترتيبهما فيه. */
const INBOUND = "IN";
const OUTBOUND = "OUT";

/** تسمية كل اتجاه. والحارس تحتها يكسر الشاشة بصوتٍ عالٍ عند عضوٍ جديد. */
const DIRECTION_LABEL: Readonly<Record<string, string>> = {
  IN: "inventory.movements.directionIn",
  OUT: "inventory.movements.directionOut",
};
const DIRECTION_WHY: Readonly<Record<string, string>> = {
  IN: "inventory.movements.directionInWhy",
  OUT: "inventory.movements.directionOutWhy",
};
for (const direction of DIRECTIONS) {
  if (!DIRECTION_LABEL[direction] || !DIRECTION_WHY[direction]) {
    throw new TypeError("اتجاهٌ في العقد بلا تسمية · a published direction with no label: " + direction);
  }
}

/** القيمة التي يعرفها العقد لمستودعٍ لم يُسكَّن بعد. */
const UNBINNED = "DEFAULT";

/** ما يُرسَل على الصادر: الوحدة تحسب قيمته ولا تُملى. */
const OUTBOUND_COST = "0";

/** الحالة التي تقبل الترحيل. */
const DRAFT = "DRAFT";

/** اليوم بصيغة yyyy-MM-dd ميلادية — من حقل التاريخ لا من تنسيق ثقافة. */
function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/* ═══════════════════════════════════════════════════════ لوح الترحيل */

/** إيصال الترحيل — ويفرّق صراحةً بين ترحيلٍ أول ووصولٍ ثانٍ بالهوية نفسها. */
function PostedPanel(props: { movement: StockMovement; moment: string }): ReactNode {
  const { t } = useT();
  const { movement } = props;
  const again = movement.alreadyPosted;
  return (
    <section
      className={"alert " + (again ? "alert--info" : "alert--success") + " " + props.moment}
      role="status"
      data-testid="movement-posted"
      data-already-posted={String(again)}
    >
      <h2 style={{ marginTop: 0 }}>
        {again ? t("inventory.movements.alreadyPosted") : t("inventory.movements.posted")}
      </h2>
      <p>{again ? t("inventory.movements.alreadyPostedBody") : t("inventory.movements.postedBody")}</p>
      <div className="kv">
        <div>
          <div className="k">{t("inventory.movements.colNumber")}</div>
          <div className="v mono" dir="ltr" data-testid="posted-number">{movement.number}</div>
        </div>
        <div>
          <div className="k">{t("inventory.movements.colCost")}</div>
          <div className="v"><Amount value={movement.cost} /></div>
        </div>
        <div>
          <div className="k">{t("inventory.movements.entryId")}</div>
          <div className="v mono" dir="ltr" data-testid="posted-entry">
            {movement.entryId ?? t("common.label.dash")}
          </div>
        </div>
        <div>
          <div className="k">{t("common.label.status")}</div>
          <div className="v mono" dir="ltr">{movement.state}</div>
        </div>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════════════════════ الشاشة كاملةً */

/** شاشة مستندات حركة المخزون. */
export function InventoryMovementsScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();

  const [number, setNumber] = useState("");
  const [occurredOn, setOccurredOn] = useState(todayIso);
  const [direction, setDirection] = useState<string>(INBOUND);
  const [itemCode, setItemCode] = useState("");
  const [warehouse, setWarehouse] = useState("");
  const [binned, setBinned] = useState(true);
  const [location, setLocation] = useState("");
  const [magnitude, setMagnitude] = useState("");
  const [unit, setUnit] = useState("");
  const [cost, setCost] = useState("");

  const [draft, setDraft] = useState<StockMovement | null>(null);
  const [posted, setPosted] = useState<StockMovement | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [busy, setBusy] = useState(false);
  const [postCls, firePost] = useMoment("post");
  const [arriveCls, fireArrive] = useMoment("arrive");

  const movements = useQuery({
    queryKey: ["inventory-movements", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listStockMovements(transport, { companyId: config.companyId }, signal),
  });

  const itemsQuery = useQuery({
    queryKey: ["inventory-items", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listItems(transport, { companyId: config.companyId }, signal),
  });

  const items: readonly Item[] = useMemo(() => itemsQuery.data?.items ?? [], [itemsQuery.data]);
  const chosen = useMemo(
    () => items.find((one) => one.code === itemCode) ?? null,
    [items, itemCode]
  );

  /* سلّم وحدات الصنف المختار: وحدة الأساس ثم كل وحدةٍ لها معامل. ولا شيء
     غيرها — وهذا هو موضع «الرفض قبل الإرسال» الوحيد المشروع هنا، لأنه
     **بنيويّ** (هل الوحدة في السلّم؟) لا حسابيّ (هل يقع التحويل بلا باقٍ؟). */
  const ladder = useMemo<readonly string[]>(
    () => (chosen ? [chosen.baseUnit, ...chosen.units.map((u) => u.unitCode)] : []),
    [chosen]
  );

  const magnitudeBad = magnitude !== "" && !SCHEMA_Magnitude_RE.test(magnitude);
  const costBad = direction === INBOUND && cost !== "" && !SCHEMA_Money_RE.test(cost);

  const locationId = binned ? location : UNBINNED;

  const ready =
    number !== "" &&
    occurredOn !== "" &&
    itemCode !== "" &&
    warehouse !== "" &&
    locationId !== "" &&
    magnitude !== "" &&
    !magnitudeBad &&
    unit !== "" &&
    (direction === OUTBOUND || (cost !== "" && !costBad));

  const create = useCallback(async () => {
    if (!chosen) return;
    setBusy(true);
    setError(null);
    setPosted(null);
    try {
      const body: StockMovementRequest = {
        number,
        occurredOn,
        direction: direction as "IN" | "OUT",
        itemId: chosen.code,
        /* من الكتالوج لا من حقلٍ في هذه الشاشة. */
        itemGroup: chosen.itemGroup,
        warehouseId: warehouse,
        locationId,
        quantity: { magnitude: asMagnitude(magnitude), unit },
        /* المال يصير `Money` هنا، والمُرمِّز المُولَّد يرفض أي شيء آخر في
           حقلٍ مالي — فلا طريق يمرّ منه رقمٌ إلى السلك. */
        cost: Money.wire(direction === OUTBOUND ? OUTBOUND_COST : cost),
      };
      const created = await draftStockMovement(transport, { companyId: config.companyId, body });
      setDraft(created);
      fireArrive();
      await movements.refetch();
    } catch (failure) {
      setError(failure);
    } finally {
      setBusy(false);
    }
  }, [
    chosen, config.companyId, cost, direction, fireArrive, locationId, magnitude, movements,
    number, occurredOn, transport, unit, warehouse,
  ]);

  const post = useCallback(
    async (movementId: string) => {
      setBusy(true);
      setError(null);
      try {
        const result = await postStockMovement(transport, {
          companyId: config.companyId,
          movementId,
        });
        setPosted(result);
        firePost();
        await movements.refetch();
      } catch (failure) {
        setError(failure);
      } finally {
        setBusy(false);
      }
    },
    [config.companyId, firePost, movements, transport]
  );

  if (config.companyId === "") return <ChooseCompanyFirst />;

  const rows: readonly StockMovement[] = movements.data?.movements ?? [];

  return (
    <section className="stack" data-testid="inventory-movements-screen">
      <header className="pagehead">
        <div>
          <h1>{t("inventory.movements.title")}</h1>
          <p className="sub">{t("inventory.movements.lede")}</p>
        </div>
      </header>

      <div className="statline">
        {movements.data ? (
          <span className={"pill " + arriveCls} data-testid="movement-count">
            {tp("inventory.movements.count", movements.data.movementCount)}
          </span>
        ) : null}
        <span className="spacer" />
        <Button
          label={t("common.action.refresh")}
          onClick={() => void movements.refetch()}
          testId="movements-reload"
        />
      </div>

      {posted ? <PostedPanel movement={posted} moment={postCls} /> : null}

      {movements.isPending && movements.fetchStatus === "fetching" ? <ReadingSkeleton /> : null}
      {movements.isError ? (
        <ProblemPanel error={movements.error} onRetry={() => void movements.refetch()} />
      ) : null}

      {movements.data && rows.length === 0 ? (
        <EmptyState
          title={t("inventory.movements.emptyTitle")}
          body={t("inventory.movements.emptyBody")}
          testId="movements-empty"
        />
      ) : null}

      {rows.length > 0 ? (
        <Panel
          title={t("inventory.movements.title")}
          note={t("inventory.movements.gapRead")}
          testId="movements-panel"
        >
          <div className="ledger" data-state="ready" data-testid="movements-table">
            <table>
              <caption className="visually-hidden">{t("inventory.movements.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("inventory.movements.colNumber")}</th>
                  <th scope="col">{t("inventory.movements.colDate")}</th>
                  <th scope="col">{t("inventory.movements.colDirection")}</th>
                  <th scope="col">{t("inventory.movements.colItem")}</th>
                  <th scope="col">{t("inventory.movements.colWhere")}</th>
                  <th scope="col" className="n">{t("inventory.movements.colQuantity")}</th>
                  <th scope="col" className="n">{t("inventory.movements.colCost")}</th>
                  <th scope="col">{t("inventory.movements.colState")}</th>
                  <th scope="col">{t("inventory.movements.colAction")}</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((movement) => (
                  <tr
                    key={movement.id}
                    data-testid="movement-row"
                    className={draft && draft.id === movement.id ? MOTION.arrive : undefined}
                  >
                    <td className="code">{movement.number}</td>
                    <td className="code">{movement.occurredOn}</td>
                    <td>
                      <span
                        className={
                          "pill " + (movement.direction === INBOUND ? "pill--debit" : "pill--credit")
                        }
                        title={t(DIRECTION_WHY[movement.direction] ?? "common.label.dash")}
                      >
                        {t(DIRECTION_LABEL[movement.direction] ?? "common.label.dash")}
                      </span>
                    </td>
                    <td className="code">{movement.itemId}</td>
                    <td className="code">
                      {movement.warehouseId + " · " + movement.locationId}
                      {movement.locationId === UNBINNED ? (
                        <span className="alt">{t("inventory.stock.unbinned")}</span>
                      ) : null}
                    </td>
                    <td className="n">
                      <QuantityValue
                        magnitude={movement.quantity.magnitude}
                        unit={movement.quantity.unit}
                      />
                    </td>
                    <td className="n">
                      <Amount value={movement.cost} />
                    </td>
                    <td>
                      <StatusBadge
                        state={movement.state === DRAFT ? "draft" : "posted"}
                        label={
                          movement.state === DRAFT ? t("acct.status.draft") : t("acct.status.posted")
                        }
                      />
                    </td>
                    <td>
                      {movement.state === DRAFT ? (
                        <Button
                          label={t("inventory.movements.post")}
                          kind="primary"
                          size="sm"
                          disabled={busy}
                          onClick={() => void post(movement.id)}
                          testId="movement-post"
                        />
                      ) : (
                        <span className="muted mono" dir="ltr" data-testid="movement-entry">
                          {movement.entryId ?? t("inventory.movements.noEntry")}
                        </span>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        </Panel>
      ) : null}

      {draft ? (
        <section className={"alert alert--info " + arriveCls} role="status" data-testid="movement-drafted">
          <h2 style={{ marginTop: 0 }}>{t("inventory.movements.created")}</h2>
          <p>{t("inventory.movements.createdBody")}</p>
        </section>
      ) : null}

      <Panel
        title={t("inventory.movements.newTitle")}
        note={t("inventory.movements.newNote")}
        testId="movement-form"
      >
        {itemsQuery.data && items.length === 0 ? (
          <p className="alert alert--warning" role="status" data-testid="movements-no-items">
            {t("inventory.movements.itemsEmpty")}
          </p>
        ) : null}

        <div className="grid fields-3">
          <div className="field">
            <label htmlFor="mv-number">{t("inventory.movements.number")}</label>
            <input
              id="mv-number"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="movement-number"
              value={number}
              onChange={(e) => setNumber(e.target.value)}
            />
            <span className="hint">{t("inventory.movements.numberHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="mv-date">{t("inventory.movements.date")}</label>
            <input
              id="mv-date"
              className="ctl mono"
              type="date"
              dir="ltr"
              data-testid="movement-date"
              value={occurredOn}
              onChange={(e) => setOccurredOn(e.target.value)}
            />
            <span className="hint">{t("inventory.movements.dateHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="mv-direction">{t("inventory.movements.direction")}</label>
            <select
              id="mv-direction"
              className="ctl"
              data-testid="movement-direction"
              value={direction}
              onChange={(e) => setDirection(e.target.value)}
            >
              {DIRECTIONS.map((one) => (
                <option key={one} value={one}>
                  {t(DIRECTION_LABEL[one] as string)}
                </option>
              ))}
            </select>
            <span className="hint">{t(DIRECTION_WHY[direction] as string)}</span>
          </div>
        </div>

        <div className="grid fields-3" style={{ "--grid-lead": "var(--space-12)" } as CSSProperties}>
          <div className="field">
            <label htmlFor="mv-item">{t("inventory.movements.item")}</label>
            <select
              id="mv-item"
              className="ctl mono"
              data-testid="movement-item"
              value={itemCode}
              onChange={(e) => {
                setItemCode(e.target.value);
                /* الوحدة تُمسَح مع تغيّر الصنف: وحدةٌ من سلّم صنفٍ آخر ليست
                   وحدةً لهذا، وتركُها هو بعينه الافتراض الصامت. */
                setUnit("");
              }}
            >
              <option value="">{t("inventory.movements.itemNone")}</option>
              {items.map((one) => (
                <option key={one.id} value={one.code}>
                  {one.code + " — " + one.name.ar}
                </option>
              ))}
            </select>
            <span className="hint">{t("inventory.movements.itemHint")}</span>
          </div>
          <div className="field">
            <label htmlFor="mv-group">{t("inventory.movements.group")}</label>
            <input
              id="mv-group"
              className="ctl mono"
              dir="ltr"
              readOnly
              data-testid="movement-group"
              value={chosen?.itemGroup ?? ""}
            />
            <span className="hint">
              <ProvenanceMark source="defaulted" label={t("screen.voice.provenance.defaulted")} />{" "}
              {t("inventory.movements.groupFrom")}
            </span>
          </div>
          <div className="field">
            <label htmlFor="mv-warehouse">{t("inventory.movements.warehouse")}</label>
            <input
              id="mv-warehouse"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="movement-warehouse"
              value={warehouse}
              onChange={(e) => setWarehouse(e.target.value)}
            />
            <span className="hint">{t("inventory.movements.warehouseHint")}</span>
          </div>
        </div>

        <fieldset className="card card-pad" style={{ marginTop: "var(--space-12)" }}>
          <legend className="k">{t("inventory.movements.locationMode")}</legend>
          <div className="inline-group" role="radiogroup" aria-label={t("inventory.movements.locationMode")}>
            <label className="inline-group">
              <input
                type="radio"
                name="mv-location-mode"
                data-testid="location-mode-named"
                checked={binned}
                onChange={() => setBinned(true)}
              />
              <span>{t("inventory.movements.locationNamed")}</span>
            </label>
            <label className="inline-group">
              <input
                type="radio"
                name="mv-location-mode"
                data-testid="location-mode-default"
                checked={!binned}
                onChange={() => setBinned(false)}
              />
              <span className="mono">{t("inventory.movements.locationDefault")}</span>
            </label>
          </div>
          {binned ? (
            <div className="field" style={{ marginTop: "var(--space-10)" }}>
              <label htmlFor="mv-location">{t("inventory.movements.location")}</label>
              <input
                id="mv-location"
                className="ctl mono"
                dir="ltr"
                autoComplete="off"
                data-testid="movement-location"
                value={location}
                onChange={(e) => setLocation(e.target.value)}
              />
              <span className="hint">{t("inventory.movements.locationHint")}</span>
            </div>
          ) : (
            <p className="muted" data-testid="location-default-why">
              {t("inventory.movements.locationDefaultWhy")}
            </p>
          )}
        </fieldset>

        <div className="grid fields-3" style={{ "--grid-lead": "var(--space-12)" } as CSSProperties}>
          <div className="field">
            <label htmlFor="mv-magnitude">{t("inventory.movements.magnitude")}</label>
            <input
              id="mv-magnitude"
              className={"ctl amt-input" + (magnitudeBad ? " is-invalid" : "")}
              dir="ltr"
              inputMode="decimal"
              autoComplete="off"
              spellCheck={false}
              aria-invalid={magnitudeBad}
              data-testid="movement-magnitude"
              value={magnitude}
              onChange={(e) => setMagnitude(e.target.value)}
              placeholder="0.000000"
            />
            <span className="hint">
              {magnitudeBad
                ? t("inventory.movements.magnitudeBad")
                : t("inventory.movements.magnitudeHint")}
            </span>
          </div>
          <div className="field">
            <label htmlFor="mv-unit">{t("inventory.movements.unit")}</label>
            <select
              id="mv-unit"
              className="ctl mono"
              data-testid="movement-unit"
              disabled={ladder.length === 0}
              value={unit}
              onChange={(e) => setUnit(e.target.value)}
            >
              <option value="">{t("common.label.select")}</option>
              {ladder.map((one) => (
                <option key={one} value={one}>
                  {one}
                </option>
              ))}
            </select>
            <span className="hint" data-testid="movement-unit-hint">
              {!chosen
                ? t("inventory.movements.unitPickItem")
                : chosen.units.length === 0
                  ? t("inventory.movements.unitBaseOnly")
                  : t("inventory.movements.unitHint")}
            </span>
          </div>
          <div className="field">
            <label htmlFor="mv-cost">{t("inventory.movements.cost")}</label>
            <input
              id="mv-cost"
              className={
                "ctl amt-input" +
                (direction === INBOUND ? " is-debit" : "") +
                (costBad ? " is-invalid" : "")
              }
              dir="ltr"
              inputMode="decimal"
              autoComplete="off"
              spellCheck={false}
              readOnly={direction === OUTBOUND}
              aria-invalid={costBad}
              data-testid="movement-cost"
              value={direction === OUTBOUND ? OUTBOUND_COST : cost}
              onChange={(e) => setCost(e.target.value)}
              placeholder="0.0000"
            />
            <span className="hint" data-testid="movement-cost-hint">
              {costBad
                ? t("inventory.movements.costBad")
                : direction === OUTBOUND
                  ? t("inventory.movements.costHintOut")
                  : t("inventory.movements.costHintIn")}
            </span>
          </div>
        </div>

        <div className="inline-group" style={{ marginTop: "var(--space-12)" }}>
          <Button
            label={busy ? t("common.state.loading") : t("inventory.movements.create")}
            kind="primary"
            disabled={!ready || busy}
            loading={busy}
            onClick={() => void create()}
            testId="movement-create"
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
        title={t("inventory.movements.warehouse")}
        body={t("inventory.movements.warehouseGap")}
        testId="movements-warehouse-gap"
      />
    </section>
  );
}
