/* ═══════════════════════════════════════════════════════════════════════════
   /design — صفحة العرض الحيّة لنظام التصميم
   ───────────────────────────────────────────────────────────────────────────
   **هذه الصفحة هي العقد.** خمسةُ وكلاءٍ يبنون الأقسام الخمسة فوق ما فيها،
   وما لا يظهر هنا يخترع كلٌّ منهم بديلاً له. ولذلك تعرض **كل شيء حيّاً**: كل
   رمز، وكل مفردة حركةٍ ومعها متى تُستعمل، وكل مكوّنٍ بحالاته الأربع، لا
   لقطاتٍ ولا وصفاً.

   وتُبنى من `catalogue.ts` لا من نسخةٍ ثانية من القائمة: فهرسٌ يُكتب مرّتين
   ينحرف عند أول إضافة، فتُعرض على المالك لوحةٌ ليست لوحة المنتج.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useEffect, useState, type CSSProperties, type ReactNode } from "react";
import { useT } from "../../i18n/react";
import {
  AlertBell,
  Button,
  ConfidenceMeter,
  EmptyState,
  Field,
  InferredValue,
  LedgerTable,
  MOTION,
  Panel,
  PresencePanel,
  ProgressBar,
  QuantityValue,
  ProvenanceMark,
  RefusalPanel,
  StatCard,
  StatusBadge,
  StreamingReveal,
  Surface,
  useMoment,
  VoiceTrace,
  type TraceStep,
} from "../../ui";
import {
  DOC_STATES,
  GLOWS,
  MOTIONS,
  PALETTE,
  PROVENANCES,
  QUANTITY_SAMPLES,
  type MotionEntry,
} from "./catalogue";
import {
  DEMO_DIFFERENCE,
  DEMO_TOTAL_CREDIT,
  DEMO_TOTAL_DEBIT,
  DEMO_VAT,
  demoRows,
} from "./data";
import "./design.css";

/* ══════════════════════════════════════════════════════ اللوحة · palette */

function Swatch(props: { token: string; roleKey: string; kind: string }): ReactNode {
  const { t } = useT();
  const style = { "--swatch": `var(${props.token})` } as CSSProperties;
  return (
    <div className="swatch" data-kind={props.kind} style={style}>
      <span className="swatch__chip" aria-hidden="true" />
      <span className="swatch__body">
        <span className="swatch__role">{t(props.roleKey)}</span>
        <code className="swatch__token mono">{props.token}</code>
      </span>
    </div>
  );
}

/* ════════════════════════════════════════════ مقياس الحركة · motion scale */

function MotionTile(props: { entry: MotionEntry }): ReactNode {
  const { t } = useT();
  const [cls, fire] = useMoment(props.entry.name);
  return (
    <div className="mtile">
      <div className="mtile__head">
        <strong>{t(props.entry.titleKey)}</strong>
        <Button label={t("screen.design.motion.play")} size="sm" onClick={fire} />
      </div>
      <p className="mtile__when">{t(props.entry.whenKey)}</p>
      <div className={"mtile__stage " + cls} data-motion={props.entry.name}>
        <span className="mtile__sample n mono">{"802,871.25"}</span>
      </div>
      <p className="mtile__spec mono" dir="ltr">
        {props.entry.duration + " · " + props.entry.ease}
      </p>
    </div>
  );
}

/* ═══════════════════════════════════════ طبقة الحضور · presence section */

const TRACE_KEYS = ["hear", "transcribe", "intent", "fill"] as const;

function PresenceSection(): ReactNode {
  const { t } = useT();
  const [step, setStep] = useState(0);
  /* الصفحة تُفتح والقيم مكشوفةٌ سلفاً — صفحةُ عقدٍ تُفتح ناقصةً تُقرأ ناقصة.
     و«شغّل» يطوي الكشف ثم يعيده، فتُرى الرحلة كاملةً بطلبٍ لا بانتظار. */
  const [streaming, setStreaming] = useState(true);

  useEffect(() => {
    if (step === 0 || step > TRACE_KEYS.length) return;
    const id = setTimeout(() => setStep((s) => s + 1), 700);
    return () => clearTimeout(id);
  }, [step]);

  const run = useCallback(() => {
    setStep(1);
    setStreaming(false);
    setTimeout(() => setStreaming(true), 700 * TRACE_KEYS.length);
  }, []);

  const steps: readonly TraceStep[] = TRACE_KEYS.map((key, index) => ({
    key,
    label: t("screen.design.trace." + key),
    value: step > index + 1 || step > TRACE_KEYS.length ? t("screen.design.traceValue." + key) : undefined,
    state: step === index + 1 ? "active" : step > index + 1 ? "done" : "idle",
  }));

  const revealed: readonly ReactNode[] = [
    <div className="kv" key="seller">
      <span className="kv__k">{t("screen.voice.field.sellerName")}</span>
      <span className="kv__v">
        <InferredValue inferred>{t("screen.design.sample.seller")}</InferredValue>
      </span>
      <ConfidenceMeter percent="96" label={t("screen.design.presence.confidence")} />
    </div>,
    <div className="kv" key="total">
      <span className="kv__k">{t("screen.voice.field.grossTotal")}</span>
      <span className="kv__v n mono">
        <InferredValue inferred>{"4,312.50"}</InferredValue>
      </span>
      <ConfidenceMeter percent="88" label={t("screen.design.presence.confidence")} />
    </div>,
    <div className="kv" key="rate">
      <span className="kv__k">{t("screen.voice.field.taxRate")}</span>
      <span className="kv__v n mono">
        <InferredValue inferred>{"15.00"}</InferredValue>
      </span>
      <ConfidenceMeter percent="62" label={t("screen.design.presence.confidence")} />
    </div>,
    <div className="kv" key="event">
      <span className="kv__k">{t("screen.voice.field.suggestedEvent")}</span>
      <span className="kv__v mono" dir="ltr">
        <InferredValue inferred>{"purchase.invoice.recognise"}</InferredValue>
      </span>
      <ConfidenceMeter percent="41" label={t("screen.design.presence.confidence")} />
    </div>,
  ];

  return (
    <div className="grid2">
      <PresencePanel
        title={t("screen.design.presence.trace")}
        note={t("screen.design.presence.traceNote")}
        working={step !== 0 && step <= TRACE_KEYS.length}
        aside={<Button label={t("screen.design.motion.play")} size="sm" onClick={run} />}
        testId="presence-trace"
      >
        <VoiceTrace steps={steps} testId="voice-trace" />
        <StreamingReveal items={revealed} on={streaming} testId="streaming-reveal" />
      </PresencePanel>

      <div className="stack">
        <Panel title={t("screen.design.presence.confidence")} note={t("screen.design.presence.confidenceNote")}>
          <div className="stack">
            <div className="kv">
              <span className="kv__k">{t("screen.design.band.high")}</span>
              <ConfidenceMeter percent="96" label={t("screen.design.band.high")} />
            </div>
            <div className="kv">
              <span className="kv__k">{t("screen.design.band.medium")}</span>
              <ConfidenceMeter percent="71" label={t("screen.design.band.medium")} />
            </div>
            <div className="kv">
              <span className="kv__k">{t("screen.design.band.low")}</span>
              <ConfidenceMeter percent="43" label={t("screen.design.band.low")} />
            </div>
            <p className="muted">{t("screen.design.presence.notOnTyped")}</p>
          </div>
        </Panel>

        <Panel title={t("screen.design.presence.provenance")} note={t("screen.design.presence.provenanceNote")}>
          <div className="row">
            {PROVENANCES.map((p) => (
              <ProvenanceMark key={p} source={p} label={t("screen.voice.provenance." + p)} />
            ))}
          </div>
          <div className="kv kv--split">
            <span className="kv__k">{t("screen.design.presence.typed")}</span>
            <span className="kv__v n mono">{"1,250.00"}</span>
          </div>
          <div className="kv kv--split">
            <span className="kv__k">{t("screen.design.presence.inferredLabel")}</span>
            <span className="kv__v n mono">
              <InferredValue inferred title={t("screen.design.presence.inferredLabel")}>
                {"187.50"}
              </InferredValue>
            </span>
          </div>
        </Panel>
      </div>
    </div>
  );
}

/* ══════════════════════════════════════════ الأوّليّات · the primitives */

function PrimitivesSection(): ReactNode {
  const { t } = useT();
  const [postCls, firePost] = useMoment("post");
  const [refuseCls, fireRefuse] = useMoment("refuse");

  return (
    <div className="stack">
      <div className="stats-row">
        <StatCard label={t("acct.debitTotal")} amount={DEMO_TOTAL_DEBIT} tone="debit" testId="stat-debit" />
        <StatCard label={t("acct.creditTotal")} amount={DEMO_TOTAL_CREDIT} tone="credit" />
        <StatCard label={t("acct.tax.output")} amount={DEMO_VAT} />
        <StatCard
          label={t("acct.difference")}
          amount={DEMO_DIFFERENCE}
          tone="good"
          hint={t("acct.balanced")}
        />
        <StatCard label={t("acct.accountCount")} count={11} />
      </div>

      <div className="grid2">
        <Panel title={t("screen.design.prim.field")} note={t("screen.design.prim.fieldNote")}>
          <Field id="d-typed" label={t("field.book.label")} hint={t("field.book.hint")} source="typed">
            <input id="d-typed" className="ctl" defaultValue="MAIN" dir="ltr" />
          </Field>
          <Field
            id="d-inferred"
            label={t("acct.tax.rate")}
            hint={t("screen.design.prim.inferredHint")}
            source="inferred"
          >
            <input id="d-inferred" className="ctl amt-input cine-infer" defaultValue="15.00" readOnly />
          </Field>
          <Field
            id="d-bad"
            label={t("field.periodCode.label")}
            error={t("field.periodCode.bad")}
            source="typed"
            required
          >
            <input id="d-bad" className="ctl" defaultValue="2026-13" aria-invalid="true" dir="ltr" />
          </Field>
        </Panel>

        <Panel title={t("screen.design.prim.button")} note={t("screen.design.prim.buttonNote")}>
          <div className="row">
            <Button label={t("screen.voucher.posted")} kind="primary" onClick={firePost} testId="fire-post" />
            <Button label={t("screen.design.prim.refuseAction")} kind="danger" onClick={fireRefuse} />
            <Button label={t("common.action.clearFilters")} />
            <Button label={t("common.action.keyboardHelp")} kind="ghost" />
            <Button label={t("common.state.loading")} loading />
            <Button label={t("screen.design.prim.disabled")} disabled />
          </div>
          <div className="row">
            {DOC_STATES.map((s) => (
              <StatusBadge key={s} state={s} label={t("acct.status." + s)} />
            ))}
            <AlertBell count={3} label={t("screen.design.prim.bell")} />
          </div>
          <div className="stack">
            <ProgressBar percent="72" label={t("screen.design.prim.progress")} tone="brand" />
            <ProgressBar percent="100" label={t("screen.design.prim.progress")} indeterminate />
          </div>
        </Panel>
      </div>

      <div className="grid2">
        <Surface testId="posting-surface">
          <div className={"card-pad " + postCls}>
            <h3 className="mtile__head">{t("screen.voucher.posted")}</h3>
            <p className="muted">{t("screen.voucher.postedBody")}</p>
            <div className="row">
              <StatusBadge state="posted" label={t("acct.status.posted")} />
              <span className="mono" dir="ltr">
                {"JV-2026-000418"}
              </span>
            </div>
          </div>
        </Surface>

        <RefusalPanel
          title={t("screen.design.refusal.title")}
          titleEn="Posting refused: no approved rate"
          body={t("screen.design.refusal.body")}
          code="ledger.posting.rate_not_approved"
          codeLabel={t("common.problem.code")}
          subject={t("screen.design.refusal.subject")}
          subjectLabel={t("common.problem.field")}
          next={t("screen.design.refusal.next")}
          moment={refuseCls}
          testId="refusal-panel"
        />
      </div>

      <Panel
        title={t("inventory.design.quantity")}
        note={t("inventory.design.quantityNote")}
        testId="section-quantity"
      >
        <div className="stack">
          {QUANTITY_SAMPLES.map((sample) => (
            <div className="kv kv--split" key={sample.key}>
              <span className="kv__k">{t(sample.labelKey)}</span>
              <span className="kv__v">
                <QuantityValue
                  magnitude={sample.magnitude}
                  unit={sample.unit}
                  testId={"quantity-" + sample.key}
                />
              </span>
            </div>
          ))}
        </div>
      </Panel>

      <Panel title={t("screen.design.prim.empty")} note={t("screen.design.prim.emptyNote")}>
        <EmptyState
          title={t("common.state.emptyTitle")}
          body={t("screen.design.prim.emptyBody")}
          action={<Button label={t("screen.design.prim.emptyAction")} kind="primary" />}
          testId="empty-state"
        />
      </Panel>
    </div>
  );
}

/* ════════════════════════════════════════════════ الجدول المالي · ledger */

function LedgerSection(): ReactNode {
  const { t } = useT();
  const [tick, setTick] = useState(0);
  const [state, setState] = useState<"ready" | "loading" | "empty" | "refused">("ready");

  const labels = {
    caption: t("screen.design.ledger.caption"),
    code: t("acct.columns.account"),
    account: t("acct.columns.memo"),
    debit: t("acct.debit"),
    credit: t("acct.credit"),
    total: t("acct.total"),
  };

  const placeholder =
    state === "loading" ? (
      <div className="ledger-state stack" style={{ inlineSize: "100%" }}>
        <span className="skeleton-row cine-live" />
        <span className="skeleton-row cine-live" />
        <span className="skeleton-row cine-live" />
        <span>{t("common.state.loadingBody")}</span>
      </div>
    ) : state === "empty" ? (
      <EmptyState
        small
        title={t("common.state.noAccountMatch")}
        body={t("common.state.noAccountMatchBody")}
      />
    ) : (
      <div className="ledger-state">
        <RefusalPanel
          title={t("common.problem.title")}
          titleEn="The request did not complete"
          body={t("common.problem.network")}
          code="ledger.read.unreachable"
          codeLabel={t("common.problem.code")}
          moment={MOTION.refuse}
        />
      </div>
    );

  return (
    <div className="stack">
      <div className="row">
        <Button
          label={t("screen.design.ledger.refetch")}
          kind="primary"
          onClick={() => {
            setState("ready");
            setTick((n) => n + 1);
          }}
          testId="ledger-refetch"
        />
        <Button label={t("screen.design.state.loading")} onClick={() => setState("loading")} />
        <Button label={t("screen.design.state.empty")} onClick={() => setState("empty")} />
        <Button label={t("screen.design.state.refused")} onClick={() => setState("refused")} />
      </div>
      <LedgerTable
        key={tick}
        rows={demoRows(tick > 0)}
        labels={labels}
        totalDebit={DEMO_TOTAL_DEBIT}
        totalCredit={DEMO_TOTAL_CREDIT}
        state={state}
        placeholder={placeholder}
        testId="design-ledger"
      />
      <p className="muted">{t("screen.design.ledger.note")}</p>
      <p className="muted">{t("screen.design.ledger.contrast")}</p>
    </div>
  );
}

/* ═══════════════════════════════════════════════════════ الصفحة كاملةً */

/** صفحة العرض الحيّة لنظام التصميم. */
export function DesignScreen(): ReactNode {
  const { t } = useT();
  return (
    <div className="stack" data-testid="design-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.design.title")}</h1>
          <p className="sub">{t("screen.design.lede")}</p>
        </div>
      </header>
      <p className="muted">{t("screen.design.contract")}</p>

      <Panel
        title={t("screen.design.sec.palette")}
        note={t("screen.design.palette.note")}
        testId="section-palette"
      >
        <div className="swatches">
          {PALETTE.map((entry) => (
            <Swatch key={entry.token} token={entry.token} roleKey={entry.roleKey} kind={entry.kind} />
          ))}
        </div>
        <h3 className="subhead">{t("screen.design.palette.glows")}</h3>
        <div className="swatches">
          {GLOWS.map((entry) => (
            <div key={entry.token} className="swatch" data-kind="glow" style={{ boxShadow: `var(${entry.token})` }}>
              <span className="swatch__body">
                <span className="swatch__role">{t(entry.roleKey)}</span>
                <code className="swatch__token mono">{entry.token}</code>
              </span>
            </div>
          ))}
        </div>
      </Panel>

      <Panel
        title={t("screen.design.sec.motion")}
        note={t("screen.design.motion.note")}
        testId="section-motion"
      >
        <div className="mtiles">
          {MOTIONS.map((entry) => (
            <MotionTile key={entry.name} entry={entry} />
          ))}
        </div>
        <p className="muted">{t("screen.design.motion.reduced")}</p>
      </Panel>

      <Panel
        title={t("screen.design.sec.presence")}
        note={t("screen.design.presence.note")}
        testId="section-presence"
      >
        <PresenceSection />
      </Panel>

      <Panel
        title={t("screen.design.sec.primitives")}
        note={t("screen.design.prim.note")}
        testId="section-primitives"
      >
        <PrimitivesSection />
      </Panel>

      <Panel
        title={t("screen.design.sec.ledger")}
        note={t("screen.design.ledger.intro")}
        testId="section-ledger"
      >
        <LedgerSection />
      </Panel>
    </div>
  );
}
