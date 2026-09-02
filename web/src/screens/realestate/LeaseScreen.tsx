/* ═══════════════════════════════════════════════════════════════════════════
   عقد الإيجار — مدّته، وأقساطه، وتفعيله، وفاتورة أجرته
   The lease — its term, its instalments, its activation and its rent invoice
   ───────────────────────────────────────────────────────────────────────────
   هذه الشاشة عمود القسم، وسبع قراراتٍ تحكمها وكلّها مقيسة لا مفترَضة:

   ١ · **لا مجموع للأقساط هنا.** «مجموع الأقساط = قيمة العقد بالضبط» ثابتةٌ
       مكتوبة في مصفوفة الترحيل، والحكم فيها للخادم عند التفعيل ويصل برمز
       `realestate.instalments_do_not_sum_to_the_contract`. وجمعُها في المتصفّح
       يوجب حساباً عشرياً على المال — وهو ممنوع — ثم يوهم المستخدم أن الفرق
       يُصلَح بينما **سياسة التقريب قرارُ مالكٍ مفتوح (ق-ع-3)**.

   ٢ · **ولا توليد لجدول الدفعات.** الخادم يرفض توليده من قيمةٍ وعدد أقساط
       للسبب نفسه، فالأقساط تصل **مصرَّحاً بها بفتراتها**، والشاشة تعطي محرّراً
       لا مولّداً.

   ٣ · **الفترة قبل تاريخ الاستحقاق** في ترتيب الحقول — كما رتّبها العقد
       صراحةً: الاعتراف يستند إلى مدى الفترة لا إلى يوم السداد، وقسطٌ بلا
       فترته لا ينتمي إلى شهرٍ في قائمة دخل.

   ٤ · **التداخل يُرى قبل أن يُرفَض، ويُرفَض من قاعدة البيانات.** شريط المدّة
       يضع كل قسطٍ في موضعه، ويرفع المتقاطعين إلى مسارين ويصبغهما بلون الرفض.
       والحكم النهائي ليس هنا: قيد استبعادٍ زمني في القاعدة يمنع مدّتين
       ساريتين على وحدةٍ واحدة، ويصل رفضه برمز `realestate.lease_term_overlaps`.
       وفحصٌ في الواجهة يقرأ ثم يكتب يمرّ بينه وبين الكتابة نداءٌ آخر.

   ٥ · **رمز الحدث يصل من الخادم.** الوحدة تختاره من **نموذج الملكية المسجَّل**
       لا من الطلب، والشاشة تعرضه ولا تخترعه ولا تسمّي حساباً واحداً.

   ٦ · **القسط لا يُفوتَر مرّتين.** `isInvoiced` يصل مع كل سطر، والمفوتَر يُعرَض
       ولا يُختار — والعرض ثم الرفض إهانة لا خدمة.

   ٧ · **الإعفاء بلا سببٍ علامةٌ ظاهرة.** `exemptionReasonPending` حقلٌ في العقد
       لا تعليقٌ في شيفرة، فيُعرَض تنبيهاً قائماً على الفاتورة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import {
  activateLeaseContract,
  draftLeaseContract,
  draftRentInvoice,
  postRentInvoice,
  readLeaseContract,
  readRentInvoice,
  readLeaseSchedule,
} from "../../api/generated/client";
import type { Lease, LeaseSchedule, RentInvoice } from "../../api/generated/types";
import { Money } from "../../api/money";
import { useApi } from "../../app/api-context";
import { Amount, Num, useT } from "../../i18n/react";
import {
  InferredValue,
  Panel,
  PeriodBand,
  PresencePanel,
  ProvenanceMark,
  StatCard,
  StatusBadge,
  StreamingReveal,
  dayNumber,
  overlappingSpans,
  uncoveredGaps,
  type BandGap,
  type BandSpan,
} from "../../ui";
import {
  NeedsCompany,
  Refusal,
  SectionHead,
  isIsoDate,
  isMoneyText,
  refusalCode,
  todayIso,
  useWrite,
} from "./parts";

/** حالة العقد السارية — بالاسم الذي ينشره العقد لا بترتيبه فيه. */
const ACTIVE = "ACTIVE";

/** حالة المستند المُرحَّل. */
const POSTED = "POSTED";

/** رمز الرفض الذي يصل من قيد الاستبعاد الزمني في قاعدة البيانات. */
const OVERLAP_CODE = "realestate.lease_term_overlaps";

/** قسطٌ كما يُحرَّر — والمبلغ **نصّ** حتى لحظة الإرسال. */
interface DraftInstalment {
  key: string;
  periodFrom: string;
  periodTo: string;
  dueOn: string;
  amount: string;
}

let sequence = 0;

/** قسطٌ جديد فارغ. */
function newInstalment(): DraftInstalment {
  sequence += 1;
  return { key: "i" + String(sequence), periodFrom: "", periodTo: "", dueOn: "", amount: "" };
}

/** الشاشة كاملةً. */
export function LeaseScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const [lease, setLease] = useState<Lease | null>(null);
  const [schedule, setSchedule] = useState<LeaseSchedule | null>(null);

  if (config.companyId === "") return <NeedsCompany />;

  return (
    <section className="stack" data-testid="realestate-lease">
      <SectionHead
        here="lease"
        title={t("realestate.lease.title")}
        lede={t("realestate.lease.lede")}
        aside={
          lease ? (
            <StatusBadge
              state={lease.state === ACTIVE ? "posted" : "draft"}
              label={t("realestate.docState." + lease.state)}
              testId="re-lease-state"
            />
          ) : undefined
        }
      />

      {/* مدخلا العقد يُطويان حين يكون على الشاشة عقدٌ مفتوح: استمارتان
          طويلتان فوق العقد المقروء تدفعانه تحت الطيّة، فيُقرأ الجدول بعد
          تمريرتين. ويبقيان مبلوغين بضغطة واحدة ومُعلَنين بعنوانهما. */}
      <details className="card card-pad" open={lease === null} data-testid="re-lease-entry">
        <summary className="k">{t("realestate.lease.another")}</summary>
        <div className="stack" style={{ marginBlockStart: "var(--space-12)" }}>
          <OpenLease
            companyId={config.companyId}
            transport={transport}
            onOpen={(found) => {
              setLease(found);
              setSchedule(null);
            }}
          />

          <DraftLease
            companyId={config.companyId}
            transport={transport}
            onDrafted={(created) => {
              setLease(created);
              setSchedule(null);
            }}
          />
        </div>
      </details>

      {lease ? (
        <>
          <LeaseHeader lease={lease} />
          <Activation
            companyId={config.companyId}
            transport={transport}
            lease={lease}
            onActivated={(next) => {
              setLease(next);
              setSchedule(null);
            }}
          />
          <Schedule
            companyId={config.companyId}
            transport={transport}
            lease={lease}
            schedule={schedule}
            onLoaded={setSchedule}
          />
        </>
      ) : null}
    </section>
  );
}

type Transport = ReturnType<typeof useApi>["transport"];

/* ═══════════════════════════════════════════════ فتح عقدٍ بمعرّفه ════ */

function OpenLease(props: {
  companyId: string;
  transport: Transport;
  onOpen: (lease: Lease) => void;
}): ReactNode {
  const { t } = useT();
  const [id, setId] = useState("");
  const read = useWrite<Lease>("arrive");

  const submit = useCallback(() => {
    void read.run(async () => {
      const found = await readLeaseContract(props.transport, {
        companyId: props.companyId,
        leaseId: id,
      });
      props.onOpen(found);
      return found;
    });
  }, [id, props, read]);

  return (
    <Panel
      title={t("realestate.lease.open")}
      note={t("realestate.lease.openNote")}
      testId="re-lease-open"
    >
      <div className="grid fields-half">
        <div className="field">
          <label htmlFor="re-lease-id">{t("realestate.lease.leaseId")}</label>
          <input
            id="re-lease-id"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-lease-id"
            value={id}
            onChange={(e) => setId(e.target.value)}
            placeholder="00000000-0000-0000-0000-000000000000"
          />
        </div>
      </div>
      <div className="inline-group">
        <button
          type="button"
          className="btn"
          data-testid="re-lease-open-go"
          disabled={id === "" || read.busy}
          onClick={submit}
        >
          {read.busy ? t("common.state.loading") : t("realestate.common.read")}
        </button>
      </div>
      {read.error ? <Refusal error={read.error} testId="re-lease-open-refusal" /> : null}
    </Panel>
  );
}

/* ════════════════════════════════════════════════ مسوّدة عقد جديد ═══ */

function DraftLease(props: {
  companyId: string;
  transport: Transport;
  onDrafted: (lease: Lease) => void;
}): ReactNode {
  const { t } = useT();
  const [contractNo, setContractNo] = useState("");
  const [unitId, setUnitId] = useState("");
  const [lesseeId, setLesseeId] = useState("");
  const [startsOn, setStartsOn] = useState(todayIso);
  const [endsOn, setEndsOn] = useState("");
  const [totalRent, setTotalRent] = useState("");
  const [rows, setRows] = useState<readonly DraftInstalment[]>(() => [newInstalment()]);
  const write = useWrite<Lease>("arrive");

  const update = useCallback((key: string, patch: Partial<DraftInstalment>) => {
    setRows((current) => current.map((row) => (row.key === key ? { ...row, ...patch } : row)));
  }, []);

  const badAmounts = rows.filter((row) => row.amount !== "" && !isMoneyText(row.amount));
  const totalBad = totalRent !== "" && !isMoneyText(totalRent);
  const termKnown = isIsoDate(startsOn) && isIsoDate(endsOn);
  const complete = rows.every(
    (row) =>
      isIsoDate(row.periodFrom) && isIsoDate(row.periodTo) && isIsoDate(row.dueOn) && isMoneyText(row.amount)
  );

  /* معاينةُ المدّة قبل الإرسال: الأقساط المكتملة وحدها تُرسَم — وصفٌّ نصفه
     فارغ لا يُخترَع له مدى. */
  const spans: readonly BandSpan[] = useMemo(
    () =>
      rows
        .filter((row) => isIsoDate(row.periodFrom) && isIsoDate(row.periodTo))
        .map((row, index) => ({
          key: row.key,
          from: row.periodFrom,
          to: row.periodTo,
          label: <Num value={index + 1} />,
          title: row.periodFrom + " → " + row.periodTo,
        })),
    [rows]
  );

  const clashing = termKnown && spans.length > 1 ? overlappingSpans(spans) : [];
  const gaps = termKnown ? uncoveredGaps(startsOn, endsOn, spans) : [];

  const submit = useCallback(() => {
    void write.run(async () => {
      const created = await draftLeaseContract(props.transport, {
        companyId: props.companyId,
        body: {
          contractNo,
          unitId,
          lesseeId,
          startsOn,
          endsOn,
          totalRent: Money.wire(totalRent),
          instalments: rows.map((row) => ({
            periodFrom: row.periodFrom,
            periodTo: row.periodTo,
            dueOn: row.dueOn,
            amount: Money.wire(row.amount),
          })),
        },
      });
      props.onDrafted(created);
      return created;
    });
  }, [contractNo, endsOn, lesseeId, props, rows, startsOn, totalRent, unitId, write]);

  return (
    <Panel
      title={t("realestate.lease.draft")}
      note={t("realestate.lease.draftNote")}
      testId="re-lease-draft"
    >
      <div className="grid fields-3">
        <div className="field">
          <label htmlFor="re-lease-no">
            {t("realestate.lease.contractNo")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-lease-no"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-lease-no"
            value={contractNo}
            onChange={(e) => setContractNo(e.target.value)}
            placeholder="LSE-2026-001"
          />
        </div>
        <div className="field">
          <label htmlFor="re-lease-unit">
            {t("realestate.lease.unitId")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-lease-unit"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-lease-unit"
            value={unitId}
            onChange={(e) => setUnitId(e.target.value)}
          />
          <span className="hint">{t("realestate.lease.unitIdHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-lease-lessee">
            {t("realestate.lease.lesseeId")}
            <span className="req" aria-hidden="true">{"*"}</span>
          </label>
          <input
            id="re-lease-lessee"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-lease-lessee"
            value={lesseeId}
            onChange={(e) => setLesseeId(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="re-lease-from">{t("realestate.lease.startsOn")}</label>
          <input
            id="re-lease-from"
            className="ctl mono"
            type="date"
            dir="ltr"
            data-testid="re-lease-from"
            value={startsOn}
            onChange={(e) => setStartsOn(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="re-lease-to">{t("realestate.lease.endsOn")}</label>
          <input
            id="re-lease-to"
            className="ctl mono"
            type="date"
            dir="ltr"
            data-testid="re-lease-to"
            value={endsOn}
            onChange={(e) => setEndsOn(e.target.value)}
          />
          <span className="hint">{t("realestate.lease.endsOnHint")}</span>
        </div>
        <div className="field">
          <label htmlFor="re-lease-total">{t("realestate.lease.totalRent")}</label>
          <input
            id="re-lease-total"
            className={"ctl amt-input" + (totalBad ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            spellCheck={false}
            aria-invalid={totalBad}
            data-testid="re-lease-total"
            value={totalRent}
            onChange={(e) => setTotalRent(e.target.value)}
            placeholder="0.0000"
          />
          <span className={totalBad ? "field-error" : "hint"} role={totalBad ? "alert" : undefined}>
            {totalBad ? t("realestate.common.moneyBad") : t("realestate.common.moneyHint")}
          </span>
        </div>
      </div>

      <h3 className="k">{t("realestate.lease.instalments")}</h3>
      <p className="muted" data-testid="re-lease-sum-note">
        {t("realestate.lease.sumNote")}
      </p>

      <div className="stack">
        {rows.map((row, index) => (
          <fieldset key={row.key} className="card card-pad" data-testid="re-instalment">
            <legend className="k">
              <Num value={index + 1} />
            </legend>
            <div className="grid fields-4">
              <div className="field">
                <label htmlFor={"re-inst-from-" + row.key}>{t("realestate.lease.periodFrom")}</label>
                <input
                  id={"re-inst-from-" + row.key}
                  className="ctl mono"
                  type="date"
                  dir="ltr"
                  data-testid="re-inst-from"
                  value={row.periodFrom}
                  onChange={(e) => update(row.key, { periodFrom: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor={"re-inst-to-" + row.key}>{t("realestate.lease.periodTo")}</label>
                <input
                  id={"re-inst-to-" + row.key}
                  className="ctl mono"
                  type="date"
                  dir="ltr"
                  data-testid="re-inst-to"
                  value={row.periodTo}
                  onChange={(e) => update(row.key, { periodTo: e.target.value })}
                />
              </div>
              <div className="field">
                <label htmlFor={"re-inst-due-" + row.key}>{t("realestate.lease.dueOn")}</label>
                <input
                  id={"re-inst-due-" + row.key}
                  className="ctl mono"
                  type="date"
                  dir="ltr"
                  data-testid="re-inst-due"
                  value={row.dueOn}
                  onChange={(e) => update(row.key, { dueOn: e.target.value })}
                />
                <span className="hint">{t("realestate.lease.dueOnHint")}</span>
              </div>
              <div className="field">
                <label htmlFor={"re-inst-amount-" + row.key}>{t("realestate.lease.amount")}</label>
                <input
                  id={"re-inst-amount-" + row.key}
                  className={
                    "ctl amt-input" +
                    (row.amount !== "" && !isMoneyText(row.amount) ? " is-invalid" : "")
                  }
                  inputMode="decimal"
                  dir="ltr"
                  autoComplete="off"
                  spellCheck={false}
                  aria-invalid={row.amount !== "" && !isMoneyText(row.amount)}
                  data-testid="re-inst-amount"
                  value={row.amount}
                  onChange={(e) => update(row.key, { amount: e.target.value })}
                  placeholder="0.0000"
                />
              </div>
            </div>
            <div className="inline-group">
              <button
                type="button"
                className="btn btn-danger-soft"
                data-testid="re-inst-remove"
                disabled={rows.length <= 1}
                onClick={() => setRows((current) => current.filter((r) => r.key !== row.key))}
              >
                {t("common.action.deleteLine")}
              </button>
            </div>
          </fieldset>
        ))}
        <button
          type="button"
          className="addline"
          data-testid="re-inst-add"
          onClick={() => setRows((current) => [...current, newInstalment()])}
        >
          {t("common.action.addLine")}
        </button>
      </div>

      {termKnown && spans.length > 0 ? (
        <>
          <h3 className="k">{t("realestate.lease.preview")}</h3>
          <PeriodBand
            from={startsOn}
            to={endsOn}
            spans={spans}
            labels={{
              caption: t("realestate.lease.bandCaption"),
              gap: t("realestate.lease.gapTitle"),
            }}
            testId="re-lease-preview-band"
          />
          <BandKey />
          {clashing.length > 0 ? (
            <p className="alert alert--danger" role="alert" data-testid="re-lease-preview-overlap">
              {t("realestate.lease.previewOverlap")}
            </p>
          ) : null}
          {gaps.length > 0 ? (
            <p className="alert alert--warning" role="status" data-testid="re-lease-preview-gap">
              {t("realestate.lease.previewGap")}
            </p>
          ) : null}
        </>
      ) : null}

      <div className="inline-group">
        <button
          type="button"
          className="btn btn-primary"
          data-testid="re-lease-save"
          disabled={
            contractNo === "" ||
            unitId === "" ||
            lesseeId === "" ||
            !termKnown ||
            !isMoneyText(totalRent) ||
            !complete ||
            badAmounts.length > 0 ||
            write.busy
          }
          onClick={submit}
        >
          {write.busy ? t("common.state.loading") : t("realestate.lease.draftAction")}
        </button>
      </div>

      {write.error ? <Refusal error={write.error} testId="re-lease-draft-refusal" /> : null}
    </Panel>
  );
}

/** مفتاح قراءة الشريط — ثلاث حالاتٍ وفجوة، بأسمائها لا بألوانها وحدها. */
function BandKey(): ReactNode {
  const { t } = useT();
  const states = ["plain", "done", "conflict", "gap"] as const;
  return (
    <div className="band-key re-legend" data-testid="re-band-key">
      {states.map((state) => (
        <span className="band-key__item" key={state}>
          <span className="band-key__chip" data-state={state} aria-hidden="true" />
          <span>{t("realestate.lease.key." + state)}</span>
        </span>
      ))}
    </div>
  );
}

/* ═══════════════════════════════════════════════════ رأس العقد ══════ */

function LeaseHeader(props: { lease: Lease }): ReactNode {
  const { t } = useT();
  const { lease } = props;
  return (
    <Panel
      title={t("realestate.lease.summary")}
      aside={
        <StatusBadge
          state={lease.state === ACTIVE ? "posted" : "draft"}
          label={t("realestate.docState." + lease.state)}
        />
      }
      testId="re-lease-header"
    >
      <div className="stats-row">
        <StatCard label={t("realestate.lease.totalRent")} amount={lease.totalRent} testId="re-lease-total-stat" />
      </div>
      <div className="kv">
        <div>
          <div className="k">{t("realestate.lease.contractNo")}</div>
          <div className="v code" data-testid="re-lease-contract-no">
            {lease.contractNo}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.lease.startsOn")}</div>
          <div className="v code">{lease.startsOn}</div>
        </div>
        <div>
          <div className="k">{t("realestate.lease.endsOn")}</div>
          <div className="v code">{lease.endsOn}</div>
        </div>
        <div>
          <div className="k">{t("realestate.lease.unitId")}</div>
          <div className="v re-id">{lease.unitId}</div>
        </div>
        <div>
          <div className="k">{t("realestate.register.propertyId")}</div>
          <div className="v re-id">{lease.propertyId}</div>
        </div>
        <div>
          <div className="k">{t("realestate.lease.lesseeId")}</div>
          <div className="v re-id">{lease.lesseeId}</div>
        </div>
      </div>
      <p className="re-id" data-testid="re-lease-identity">
        {lease.id}
      </p>
    </Panel>
  );
}

/* ══════════════════════════════════════════════════ التفعيل ═════════ */

function Activation(props: {
  companyId: string;
  transport: Transport;
  lease: Lease;
  onActivated: (lease: Lease) => void;
}): ReactNode {
  const { t } = useT();
  const write = useWrite<Lease>("post");
  const active = props.lease.state === ACTIVE;
  const overlapped = refusalCode(write.error) === OVERLAP_CODE;

  const submit = useCallback(() => {
    void write.run(async () => {
      const next = await activateLeaseContract(props.transport, {
        companyId: props.companyId,
        leaseId: props.lease.id,
      });
      props.onActivated(next);
      return next;
    });
  }, [props, write]);

  return (
    <Panel
      title={t("realestate.lease.activation")}
      note={t("realestate.lease.activationNote")}
      testId="re-lease-activation"
    >
      <div className={"re-steps " + (active ? write.moment : "")}>
        <span className="re-step" data-state={active ? "done" : "active"}>
          <span className="re-step__dot" aria-hidden="true" />
          {t("realestate.docState.DRAFT")}
        </span>
        <span className="re-step" data-state={active ? "done" : undefined}>
          <span className="re-step__dot" aria-hidden="true" />
          {t("realestate.docState.ACTIVE")}
        </span>
      </div>

      <div className="inline-group">
        <button
          type="button"
          className="btn btn-primary"
          data-testid="re-lease-activate"
          disabled={active || write.busy}
          onClick={submit}
        >
          {write.busy ? t("common.state.loading") : t("realestate.lease.activate")}
        </button>
      </div>

      {active ? (
        <p className="alert alert--success" role="status" data-testid="re-lease-active">
          {t("realestate.lease.activated")}
        </p>
      ) : null}

      {write.error ? (
        <>
          <Refusal error={write.error} testId="re-lease-activation-refusal" />
          {overlapped ? (
            <div className="problem" role="alert" data-testid="re-lease-overlap">
              <h2>{t("realestate.lease.overlapTitle")}</h2>
              <p>{t("realestate.lease.overlapBody")}</p>
              <p className="muted">{t("realestate.lease.overlapWhere")}</p>
            </div>
          ) : null}
        </>
      ) : null}
    </Panel>
  );
}

/* ═════════════════════════════════════════ جدول الدفعات والفوترة ════ */

function Schedule(props: {
  companyId: string;
  transport: Transport;
  lease: Lease;
  schedule: LeaseSchedule | null;
  onLoaded: (schedule: LeaseSchedule) => void;
}): ReactNode {
  const { t, tp } = useT();
  const read = useWrite<LeaseSchedule>("arrive");
  const [picked, setPicked] = useState<readonly string[]>([]);
  const { schedule } = props;

  const load = useCallback(() => {
    void read.run(async () => {
      const found = await readLeaseSchedule(props.transport, {
        companyId: props.companyId,
        leaseId: props.lease.id,
      });
      props.onLoaded(found);
      setPicked([]);
      return found;
    });
  }, [props, read]);

  const spans: readonly BandSpan[] = useMemo(
    () =>
      (schedule?.lines ?? []).map((line) => ({
        key: line.id,
        from: line.periodFrom,
        to: line.periodTo,
        label: <Num value={line.seq} />,
        title: line.periodFrom + " → " + line.periodTo,
        state: line.isInvoiced ? ("done" as const) : ("plain" as const),
      })),
    [schedule]
  );

  const clashing = new Set(spans.length > 1 ? overlappingSpans(spans) : []);
  const gaps =
    schedule && spans.length > 0
      ? uncoveredGaps(props.lease.startsOn, props.lease.endsOn, spans)
      : [];

  const toggle = useCallback((id: string) => {
    setPicked((current) =>
      current.includes(id) ? current.filter((one) => one !== id) : [...current, id]
    );
  }, []);

  return (
    <Panel
      title={t("realestate.lease.schedule")}
      note={t("realestate.lease.scheduleNote")}
      aside={
        <button
          type="button"
          className="btn btn-sm"
          data-testid="re-schedule-load"
          disabled={read.busy}
          onClick={load}
        >
          {read.busy ? t("common.state.loading") : t("common.action.refresh")}
        </button>
      }
      testId="re-lease-schedule"
    >
      {read.error ? <Refusal error={read.error} testId="re-schedule-refusal" /> : null}

      {!schedule && !read.error ? (
        <div className="empty empty--sm" data-testid="re-schedule-idle">
          <div className="ico" aria-hidden="true">{"∅"}</div>
          <h3>{t("realestate.lease.scheduleIdle")}</h3>
          <p>{t("realestate.lease.scheduleIdleBody")}</p>
        </div>
      ) : null}

      {schedule && schedule.lines.length === 0 ? (
        <div className="empty empty--sm" data-testid="re-schedule-empty">
          <div className="ico" aria-hidden="true">{"∅"}</div>
          <h3>{t("realestate.lease.scheduleEmpty")}</h3>
          <p>{t("realestate.lease.scheduleEmptyBody")}</p>
        </div>
      ) : null}

      {schedule && schedule.lines.length > 0 ? (
        <div className="stack">
          <p className="muted" data-testid="re-schedule-count">
            {tp("realestate.lease.lineCount", schedule.lines.length)}
          </p>

          <PeriodBand
            from={props.lease.startsOn}
            to={props.lease.endsOn}
            spans={spans}
            selected={picked}
            labels={{
              caption: t("realestate.lease.bandCaption"),
              gap: t("realestate.lease.gapTitle"),
            }}
            testId="re-schedule-band"
          />
          <BandKey />

          <Readings lines={schedule.lines} gaps={gaps} clashing={clashing.size} />

          {clashing.size > 0 ? (
            <p className="alert alert--danger" role="alert" data-testid="re-schedule-overlap">
              {t("realestate.lease.scheduleOverlap")}
            </p>
          ) : null}
          {gaps.length > 0 ? (
            <p className="alert alert--warning" role="status" data-testid="re-schedule-gap">
              {t("realestate.lease.previewGap")}
            </p>
          ) : null}

          <div className="ledger" data-state="ready" data-testid="re-schedule-table">
            <table>
              <caption className="visually-hidden">{t("realestate.lease.schedule")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("realestate.lease.pick")}</th>
                  <th scope="col" className="n">{t("realestate.lease.seq")}</th>
                  <th scope="col">{t("realestate.lease.period")}</th>
                  <th scope="col">{t("realestate.lease.dueOn")}</th>
                  <th scope="col" className="n">
                    {t("realestate.lease.amount")}
                  </th>
                  <th scope="col">{t("realestate.lease.invoiced")}</th>
                </tr>
              </thead>
              <tbody>
                {schedule.lines.map((line) => (
                  <tr key={line.id} data-testid="re-schedule-row" data-invoiced={String(line.isInvoiced)}>
                    <td>
                      <input
                        type="checkbox"
                        data-testid="re-schedule-pick"
                        aria-label={t("realestate.lease.pick")}
                        disabled={line.isInvoiced}
                        checked={picked.includes(line.id)}
                        onChange={() => toggle(line.id)}
                      />
                    </td>
                    <td className="n">
                      <Num value={line.seq} />
                    </td>
                    <td className="code">{line.periodFrom + " → " + line.periodTo}</td>
                    <td className="code">{line.dueOn}</td>
                    <td className="n">
                      <Amount value={line.amount} />
                    </td>
                    <td>
                      <StatusBadge
                        state={line.isInvoiced ? "posted" : "draft"}
                        label={t(line.isInvoiced ? "realestate.lease.yes" : "realestate.lease.no")}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          <Invoicing
            companyId={props.companyId}
            transport={props.transport}
            lease={props.lease}
            picked={picked}
            onInvoiced={load}
          />
        </div>
      ) : null}
    </Panel>
  );
}

/* ═══════════════════════ ما قرأه النظام في المدّة — قراءاتٌ مشتقّة ══ */

/**
 * قراءاتٌ **اشتقّها النظام** من جدول الخادم — لا أدخلها مستخدم ولا اخترعها
 * الخادم. ولذلك تُعرَض في لوح الحضور موسومةً `inferred`: القاعدة في هذا
 * المنتج أن من يقرأ رقماً يجب أن يعرف من أين جاء، والوسم هو ما يفرّق
 * المشتقّ عن المُدخَل.
 *
 * <b>ولا مبلغ في هذه القراءات.</b> كلّها أعداد صحيحة وتواريخ: الحساب على
 * الأيام مسموح، والحساب على المال ممنوع في هذا المستودع.
 * @param props سطور الجدول، والفجوات، وعدد المتقاطعين.
 */
function Readings(props: {
  lines: LeaseSchedule["lines"];
  gaps: readonly BandGap[];
  clashing: number;
}): ReactNode {
  const { t } = useT();
  const invoiced = props.lines.filter((line) => line.isInvoiced).length;
  const uncoveredDays = props.gaps.reduce(
    (total, gap) => total + (dayNumber(gap.to) - dayNumber(gap.from) + 1),
    0
  );
  /* أقرب استحقاقٍ لم يُفوتَر: مقارنةٌ نصّية على yyyy-MM-dd وهي ترتيبٌ زمني
     صحيح بحكم الصيغة نفسها — فلا تُبنى تواريخ لمجرّد المقارنة. */
  const nextDue = props.lines
    .filter((line) => !line.isInvoiced)
    .map((line) => line.dueOn)
    .sort()[0];

  const mark = <ProvenanceMark source="inferred" label={t("screen.voice.provenance.inferred")} />;

  const items: readonly ReactNode[] = [
    <div className="kv kv--split" key="lines">
      <span className="kv__k">{t("realestate.lease.read.instalments")}</span>
      <span className="kv__v">
        <InferredValue inferred>
          <Num value={props.lines.length} />
        </InferredValue>
        {mark}
      </span>
    </div>,
    <div className="kv kv--split" key="invoiced">
      <span className="kv__k">{t("realestate.lease.read.invoiced")}</span>
      <span className="kv__v">
        <InferredValue inferred>
          <Num value={invoiced} />
        </InferredValue>
        {mark}
      </span>
    </div>,
    <div className="kv kv--split" key="uncovered">
      <span className="kv__k">{t("realestate.lease.read.uncoveredDays")}</span>
      <span className="kv__v" data-testid="re-read-uncovered">
        <InferredValue inferred>
          <Num value={uncoveredDays} />
        </InferredValue>
        {mark}
      </span>
    </div>,
    <div className="kv kv--split" key="overlaps">
      <span className="kv__k">{t("realestate.lease.read.overlaps")}</span>
      <span className="kv__v" data-testid="re-read-overlaps">
        <InferredValue inferred>
          <Num value={props.clashing} />
        </InferredValue>
        {mark}
      </span>
    </div>,
    <div className="kv kv--split" key="next">
      <span className="kv__k">{t("realestate.lease.read.nextDue")}</span>
      <span className="kv__v code" data-testid="re-read-next-due">
        <InferredValue inferred>{nextDue ?? t("common.label.dash")}</InferredValue>
        {mark}
      </span>
    </div>,
  ];

  return (
    <PresencePanel
      title={t("realestate.lease.read.title")}
      note={t("realestate.lease.read.note")}
      testId="re-lease-readings"
    >
      <StreamingReveal items={items} on testId="re-lease-readings-list" />
    </PresencePanel>
  );
}

/* ════════════════════════════════════════ فاتورة الأجرة وترحيلها ════ */

function Invoicing(props: {
  companyId: string;
  transport: Transport;
  lease: Lease;
  picked: readonly string[];
  onInvoiced: () => void;
}): ReactNode {
  const { t } = useT();
  const [number, setNumber] = useState("");
  const [issuedOn, setIssuedOn] = useState(todayIso);
  const [taxRate, setTaxRate] = useState("");
  const [openId, setOpenId] = useState("");
  const draft = useWrite<RentInvoice>("arrive");
  const post = useWrite<RentInvoice>("post");
  const invoice = post.value ?? draft.value;
  const rateBad = taxRate !== "" && !isMoneyText(taxRate);

  const submitDraft = useCallback(() => {
    post.reset();
    void draft.run(() =>
      draftRentInvoice(props.transport, {
        companyId: props.companyId,
        body: {
          leaseId: props.lease.id,
          number,
          issuedOn,
          taxRate: Money.wire(taxRate),
          scheduleLineIds: [...props.picked],
        },
      })
    );
  }, [draft, issuedOn, number, post, props, taxRate]);

  const submitPost = useCallback(() => {
    const current = draft.value;
    if (!current) return;
    void post
      .run(() =>
        postRentInvoice(props.transport, {
          companyId: props.companyId,
          invoiceId: current.id,
        })
      )
      .then(() => props.onInvoiced());
  }, [draft.value, post, props]);

  /* فتحُ فاتورةٍ قائمة بمعرّفها: مسوّدةٌ حُفظت في جلسةٍ سابقة تُقرأ وتُرحَّل،
     ولا باب يسرد الفواتير فالمعرّف هو الطريق. */
  const openExisting = useCallback(() => {
    post.reset();
    void draft.run(() =>
      readRentInvoice(props.transport, { companyId: props.companyId, invoiceId: openId })
    );
  }, [draft, openId, post, props]);

  return (
    <div className="card card-pad" data-testid="re-invoice">
      <h3 className="k">{t("realestate.invoice.title")}</h3>
      <p className="muted">{t("realestate.invoice.note")}</p>

      <div className="grid fields-half">
        <div className="field">
          <label htmlFor="re-invoice-open">{t("realestate.common.id")}</label>
          <div className="row">
            <input
              id="re-invoice-open"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="re-invoice-open-id"
              value={openId}
              onChange={(e) => setOpenId(e.target.value)}
            />
            <button
              type="button"
              className="btn btn-sm"
              data-testid="re-invoice-open-go"
              disabled={openId === "" || draft.busy}
              onClick={openExisting}
            >
              {t("realestate.common.read")}
            </button>
          </div>
          <span className="hint">{t("realestate.invoice.openHint")}</span>
        </div>
      </div>

      <div className="grid fields-3">
        <div className="field">
          <label htmlFor="re-invoice-no">{t("realestate.invoice.number")}</label>
          <input
            id="re-invoice-no"
            className="ctl mono"
            dir="ltr"
            autoComplete="off"
            data-testid="re-invoice-no"
            value={number}
            onChange={(e) => setNumber(e.target.value)}
            placeholder="RNT-2026-0001"
          />
        </div>
        <div className="field">
          <label htmlFor="re-invoice-date">{t("realestate.invoice.issuedOn")}</label>
          <input
            id="re-invoice-date"
            className="ctl mono"
            type="date"
            dir="ltr"
            data-testid="re-invoice-date"
            value={issuedOn}
            onChange={(e) => setIssuedOn(e.target.value)}
          />
        </div>
        <div className="field">
          <label htmlFor="re-invoice-rate">{t("realestate.invoice.taxRate")}</label>
          <input
            id="re-invoice-rate"
            className={"ctl amt-input" + (rateBad ? " is-invalid" : "")}
            inputMode="decimal"
            dir="ltr"
            autoComplete="off"
            spellCheck={false}
            aria-invalid={rateBad}
            data-testid="re-invoice-rate"
            value={taxRate}
            onChange={(e) => setTaxRate(e.target.value)}
            placeholder="0.1500"
          />
          <span className={rateBad ? "field-error" : "hint"} role={rateBad ? "alert" : undefined}>
            {rateBad ? t("realestate.common.moneyBad") : t("realestate.invoice.taxRateHint")}
          </span>
        </div>
      </div>

      {props.picked.length === 0 ? (
        <p className="alert alert--warning" role="status" data-testid="re-invoice-nothing-picked">
          {t("realestate.invoice.pickFirst")}
        </p>
      ) : null}

      <div className="inline-group">
        <button
          type="button"
          className="btn"
          data-testid="re-invoice-draft"
          disabled={
            number === "" || !isIsoDate(issuedOn) || !isMoneyText(taxRate) ||
            props.picked.length === 0 || draft.busy
          }
          onClick={submitDraft}
        >
          {draft.busy ? t("common.state.loading") : t("realestate.invoice.draftAction")}
        </button>
        <button
          type="button"
          className="btn btn-primary"
          data-testid="re-invoice-post"
          disabled={!draft.value || post.busy}
          onClick={submitPost}
        >
          {post.busy ? t("common.state.loading") : t("common.action.post")}
        </button>
      </div>

      {draft.error ? <Refusal error={draft.error} testId="re-invoice-draft-refusal" /> : null}
      {post.error ? <Refusal error={post.error} testId="re-invoice-post-refusal" /> : null}

      {invoice ? <InvoiceCard invoice={invoice} moment={post.value ? post.moment : draft.moment} /> : null}
    </div>
  );
}

/** بطاقة الفاتورة — والصافي والضريبة والإجمالي كما حسبها الخادم لا المتصفّح. */
function InvoiceCard(props: { invoice: RentInvoice; moment: string }): ReactNode {
  const { t } = useT();
  const { invoice } = props;
  const posted = invoice.state === POSTED;
  return (
    <section
      className={"alert " + (posted ? "alert--success " : "alert--info ") + props.moment}
      role="status"
      data-testid="re-invoice-card"
      data-state={invoice.state}
      data-already-posted={String(invoice.alreadyPosted)}
    >
      <h3 style={{ marginTop: 0 }}>
        {posted
          ? invoice.alreadyPosted
            ? t("realestate.invoice.alreadyPosted")
            : t("realestate.invoice.posted")
          : t("realestate.invoice.drafted")}
      </h3>

      <div className="stats-row">
        <StatCard label={t("realestate.invoice.net")} amount={invoice.net} testId="re-invoice-net" />
        <StatCard label={t("realestate.invoice.tax")} amount={invoice.tax} testId="re-invoice-tax" />
        <StatCard
          label={t("realestate.invoice.gross")}
          amount={invoice.gross}
          tone="good"
          testId="re-invoice-gross"
        />
      </div>

      <div className="kv">
        <div>
          <div className="k">{t("realestate.invoice.number")}</div>
          <div className="v code">{invoice.number}</div>
        </div>
        <div>
          <div className="k">{t("realestate.vat.label")}</div>
          <div className="v">{t("realestate.vat." + invoice.vatTreatment)}</div>
        </div>
        <div>
          <div className="k">{t("realestate.invoice.event")}</div>
          <div className="v code" data-testid="re-invoice-event">
            {invoice.eventCode}
          </div>
        </div>
        <div>
          <div className="k">{t("realestate.invoice.entry")}</div>
          <div className="v re-id" data-testid="re-invoice-entry">
            {invoice.entryId ?? t("common.label.dash")}
          </div>
        </div>
      </div>

      {invoice.exemptionReasonPending ? (
        <p className="alert alert--warning" role="alert" data-testid="re-invoice-exemption-pending">
          {t("realestate.invoice.exemptionPending")}
        </p>
      ) : null}
      {invoice.exemptionReasonCode !== "" ? (
        <p className="muted mono" dir="ltr" data-testid="re-invoice-exemption-code">
          {invoice.exemptionReasonCode}
        </p>
      ) : null}
      <p className="muted">{t("realestate.invoice.matrixNote")}</p>
    </section>
  );
}
