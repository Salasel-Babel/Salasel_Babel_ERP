/* ═══════════════════════════════════════════════════════════════════════════
   لوحةُ الخطّة المنطوقة — تُرى كما تُسمع، خطوةً خطوة.
   ───────────────────────────────────────────────────────────────────────────
   ⚠ **وهي في `web/src/voice/` بقصد**، كالمُشغّل: حارسُ المعمارية يُعدِّد هذا
   المجلّد كلَّه ويمسح كل .ts/.tsx فيه. فما يُكتب هنا محروسٌ من يوم كُتب.

   ⚠ **للأصمّ وللأعمى معاً، ونصٌّ واحد لا نصّان.** التوجيه يُنطَق مرّةً، وبنودُه
   بعينها هي بنودُ القائمة المرقّمة على الشاشة. وحالُ كل خطوة **نصٌّ لا لون** —
   من لا يميّز الألوان يقرأ ما يقرؤه غيره. و`aria-current="step"` على العاملة،
   وإعلانٌ لطيف عند كل انتقال، والملخّص المرتدّ يبقى `assertive` كما كان.

   ⚠ **والتوجيه لا زرّ له.** يقول الخطّة ولا يأذن بشيء؛ وزرٌّ بجانبه يعلّم الناس
   أن يضغطوا على ما لم يقرأوه، فيصير التأكيدُ الحقيقي بعده عادةً لا قراءة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { type ReactNode, useState } from "react";
import { useT } from "../i18n/react";
import { intentById } from "./catalogue";
import type { VoiceCaller, VoiceReadingOptions } from "./command";
import type { VoiceDraftHandoff } from "./handoff";
import {
  abandonCurrentStep,
  answerCondition,
  answerCurrentStep,
  completeCurrentStep,
  confirmCurrentStep,
  currentStep,
  numeral,
  planLedgerArabic,
  planStepPrefix,
  readCurrentStep,
  type VoicePlanRun,
} from "./plan";

/** خصائص اللوحة. */
export interface VoicePlanPanelProps {
  readonly run: VoicePlanRun;
  readonly caller: VoiceCaller;
  readonly options: VoiceReadingOptions;
  readonly lang: string;
  /** هل يُنطَق النصّ؟ */
  readonly speakOn: boolean;
  /** ينطق نصّاً — يُحقن كي تبقى اللوحة قابلة للاختبار بلا متصفّح ناطق. */
  readonly say: (text: string, lang: string) => void;
  readonly onRun: (run: VoicePlanRun) => void;
  /** يُستدعى بتسليم المسوّدة — وشاشةُ المستند هي التي تنادي عمليتها. */
  readonly onDraft?: (handoff: VoiceDraftHandoff) => void;
  /** وجهةُ نيّةٍ إن كانت شاشتُها مسجَّلة، وإلّا `null` — **ويُقال ولا يُخترَع**. */
  readonly destinationOf?: (intentId: string) => string | null;
  readonly onCancel: () => void;
}

/** اللوحة. */
export function VoicePlanPanel(props: VoicePlanPanelProps): ReactNode {
  const { t } = useT();
  const { run, caller, options } = props;
  const [answer, setAnswer] = useState("");

  const entry = currentStep(run);
  const total = run.steps.length;
  const ordinal = run.at + 1;
  const intent = entry === null ? null : intentById(entry.step.intentId);

  const push = (next: VoicePlanRun): void => props.onRun(next);

  /* الشرط: يُسأل الإنسان لأن المسار المنطوق لا يستطيع أن يبحث — ولا يوجد في العقد
     بحثٌ عن عميلٍ بالاسم أصلاً. فيُسأل ولا يُخمَّن. */
  const condition = (found: boolean): void => {
    const next = found ? answerCondition(run, true) : run;
    const advanced = found ? readCurrentStep(next, options) : next;
    if (found) {
      const stepped = currentStep(advanced);
      if (stepped !== null && props.speakOn) {
        props.say(
          t("screen.voice.plan.announce", {
            n: numeral(advanced.at + 1),
            total: numeral(total),
            purpose: stepped.step.purposeAr,
          }),
          props.lang
        );
      }
    }
    push(advanced);
  };

  const confirm = (): void => {
    const next = confirmCurrentStep(run, caller);
    const stepped = currentStep(next);
    if (stepped?.state === "handedOff" && stepped.handoff !== null) props.onDraft?.(stepped.handoff);
    push(next);
  };

  const done = (): void => {
    const next = completeCurrentStep(run);
    const stepped = currentStep(next);
    if (stepped !== null) {
      const read = readCurrentStep(next, options);
      if (props.speakOn) {
        props.say(
          t("screen.voice.plan.announce", {
            n: numeral(read.at + 1),
            total: numeral(total),
            purpose: stepped.step.purposeAr,
          }),
          props.lang
        );
      }
      push(read);
      return;
    }
    push(next);
  };

  const apply = (): void => {
    if (answer.trim().length === 0) return;
    push(answerCurrentStep(run, answer, options));
    setAnswer("");
  };

  const destination = entry?.handoff ? (props.destinationOf?.(entry.step.intentId) ?? null) : null;

  return (
    <section className="vcx-plan" data-testid="voice-plan" data-plan={run.plan.id} dir="auto">
      <h3 className="vcx-subtitle">{t("screen.voice.plan.title", { name: run.plan.nameAr })}</h3>

      {/* ── التوجيه: نصٌّ واحد يُعرض ويُنطَق، **ولا زرّ له ولا رمز**. ── */}
      <div className="vcx-plan-orientation" role="group" aria-labelledby="vcx-plan-orientation-title">
        <h4 className="vcx-subtitle" id="vcx-plan-orientation-title">
          {t("screen.voice.plan.orientationTitle")}
        </h4>
        <p className="vcx-readback-text" data-testid="voice-plan-readback-text">
          {run.orientationAr}
        </p>
      </div>

      {/* ── الخطوات: قائمةٌ مرقّمة، والحالُ نصٌّ لا لون ── */}
      <ol className="vcx-plan-steps" data-testid="voice-plan-steps">
        {run.steps.map((step, index) => (
          <li
            key={step.step.stepId}
            className="vcx-plan-step"
            data-testid={"voice-plan-step-" + String(index + 1)}
            data-state={step.state}
            aria-current={index === run.at ? "step" : undefined}
          >
            <span className="vcx-plan-step-n">
              {t("screen.voice.plan.stepLabel", { n: numeral(index + 1), total: numeral(total) })}
            </span>
            <span className="vcx-plan-step-purpose">{step.step.purposeAr}</span>
            <span className="vcx-plan-step-state" data-testid={"voice-plan-state-" + String(index + 1)}>
              {t("screen.voice.plan.state." + step.state)}
            </span>
          </li>
        ))}
      </ol>

      <p className="vc-note" data-testid="voice-plan-announce" role="status" aria-live="polite">
        {entry === null
          ? t("screen.voice.plan.finished")
          : t("screen.voice.plan.announce", {
              n: numeral(ordinal),
              total: numeral(total),
              purpose: entry.step.purposeAr,
            })}
      </p>

      {/* ── السؤال عن الشرط: الإنسان هو من يبحث ── */}
      {entry !== null && entry.state === "pending" && entry.step.condition === "WhenHumanFindsNothing" ? (
        <div className="vcx-plan-condition" data-testid="voice-plan-condition">
          <p className="vc-note">{t("screen.voice.plan.foundQuestion")}</p>
          <div className="vcx-actions">
            <button type="button" className="vc-manual-apply" data-testid="voice-plan-found" onClick={() => condition(true)}>
              {t("screen.voice.plan.found")}
            </button>
            <button
              type="button"
              className="vc-manual-apply"
              data-testid="voice-plan-not-found"
              onClick={() => condition(false)}
            >
              {t("screen.voice.plan.notFound")}
            </button>
          </div>
        </div>
      ) : null}

      {/* ── ما امتلأ في هذه الخطوة، ومعه ما سقط من الاسم إن سقط ── */}
      {entry?.resolution && intent !== null ? (
        <dl className="vc-fields" data-testid="voice-plan-fields">
          {intent.slots.map((slot) => {
            const value = entry.resolution!.slots.find((candidate) => candidate.name === slot.name);
            return (
              <div
                key={slot.name}
                className="vc-field"
                data-testid={"voice-plan-field-" + slot.name}
                data-provenance={value?.provenance ?? "none"}
              >
                <dt className="vc-field-label">{slot.nameAr}</dt>
                <dd className="vc-field-value" data-testid={"voice-plan-value-" + slot.name}>
                  {value ? value.text + (value.unit ? " " + value.unit : "") : t("screen.voice.blank")}
                </dd>
                {/* **القصّ يُرى ولا يُطمَر**: اسمٌ قُصّ خطأً يُصحَّح، واسمٌ قُصّ بصمت يُوقَّع عليه. */}
                {value?.dropped ? (
                  <dd className="vc-field-source" data-testid={"voice-plan-dropped-" + slot.name}>
                    {t("screen.voice.plan.dropped", { tail: value.dropped })}
                  </dd>
                ) : null}
              </div>
            );
          })}
        </dl>
      ) : null}

      {/* ── ما تطلبه الشاشةُ ولا يطلبه الصوت — مُسمّى، لا مُلمَّح إليه ── */}
      {entry !== null && entry.step.screenAsksForAr.length > 0 ? (
        <p className="vc-note" data-testid="voice-plan-screen-asks">
          {t("screen.voice.plan.screenAsksFor", { fields: entry.step.screenAsksForAr.join("، ") })}
        </p>
      ) : null}

      {/* ── واقفةٌ تسأل: تُسمّى الشريحة الناقصة، ولا تُخترَع ── */}
      {entry !== null && entry.state === "asking" && intent !== null ? (
        <div className="vcx-plan-asking" data-testid="voice-plan-asking">
          <ul className="vc-faults" role="alert">
            {entry.resolution!.missingSlots.map((name) => (
              <li key={name}>
                {t("screen.voice.refusal.slotMissing", {
                  slot: intent.slots.find((slot) => slot.name === name)?.nameAr ?? name,
                })}
              </li>
            ))}
          </ul>
          <label className="vc-manual-label" htmlFor="vcx-plan-answer">
            {t("screen.voice.plan.answerLabel")}
          </label>
          <input
            id="vcx-plan-answer"
            className="vc-manual-input"
            data-testid="voice-plan-answer"
            value={answer}
            onChange={(event) => setAnswer(event.target.value)}
          />
          <button type="button" className="vc-manual-apply" data-testid="voice-plan-answer-apply" onClick={apply}>
            {t("screen.voice.plan.answerApply")}
          </button>
        </div>
      ) : null}

      {/* ── الملخّص المرتدّ للخطوة: **البوابة نفسها، لكلّ خطوةٍ مرّة** ── */}
      {entry !== null && entry.state === "pending" && entry.resolution !== null
      && entry.step.condition === "Always" ? (
        <div className="vcx-readback" data-testid="voice-plan-step-readback" role="group">
          <p className="vcx-readback-text" data-testid="voice-plan-step-readback-text" aria-live="assertive">
            {planStepPrefix(ordinal, total) + entry.resolution.readbackAr}
          </p>
          <div className="vcx-actions">
            <button
              type="button"
              className="vc-manual-apply"
              data-testid="voice-plan-confirm"
              disabled={entry.resolution.missingSlots.length > 0}
              onClick={confirm}
            >
              {t("screen.voice.plan.confirmStep", { n: numeral(ordinal) })}
            </button>
            <button type="button" className="vc-manual-apply" data-testid="voice-plan-cancel" onClick={props.onCancel}>
              {t("screen.voice.console.cancel")}
            </button>
          </div>
        </div>
      ) : null}

      {/* ــ الخطوة المشروطة بعد أن قال «لم أجده»: تُقرأ وتُؤكَّد كغيرها ــ */}
      {entry !== null && entry.state === "pending" && entry.resolution !== null
      && entry.step.condition === "WhenHumanFindsNothing" ? (
        <div className="vcx-readback" data-testid="voice-plan-step-readback" role="group">
          <p className="vcx-readback-text" data-testid="voice-plan-step-readback-text" aria-live="assertive">
            {planStepPrefix(ordinal, total) + entry.resolution.readbackAr}
          </p>
          <div className="vcx-actions">
            <button
              type="button"
              className="vc-manual-apply"
              data-testid="voice-plan-confirm-conditional"
              disabled={entry.resolution.missingSlots.length > 0}
              onClick={confirm}
            >
              {t("screen.voice.plan.confirmStep", { n: numeral(ordinal) })}
            </button>
          </div>
        </div>
      ) : null}

      {/* ── سُلّمت: الوجهة تُقال باسمها، والغيابُ يُقال ولا يُخترَع ── */}
      {entry !== null && entry.state === "handedOff" && entry.handoff !== null ? (
        <div className="vcx-handoff" data-testid="voice-plan-handoff">
          <p className="vc-note" data-operation={entry.handoff.operationId}>
            {t("screen.voice.console.handoffOperation", { operation: entry.handoff.operationId })}
          </p>
          <p className="vc-note" data-testid="voice-plan-destination" data-destination={destination ?? ""}>
            {destination === null
              ? t("screen.voice.console.screenNotLanded")
              : t("screen.voice.console.destination", { path: destination })}
          </p>
          <div className="vcx-actions">
            <button type="button" className="vc-manual-apply" data-testid="voice-plan-step-done" onClick={done}>
              {t("screen.voice.plan.stepDone")}
            </button>
            <button
              type="button"
              className="vc-manual-apply"
              data-testid="voice-plan-step-abandon"
              onClick={() => push(abandonCurrentStep(run))}
            >
              {t("screen.voice.plan.stepAbandoned")}
            </button>
          </div>
          <p className="vc-not-a-fact">{t("screen.voice.console.postIsNotSpoken")}</p>
        </div>
      ) : null}

      {entry !== null && entry.state === "refused" ? (
        <ul className="vc-faults" data-testid="voice-plan-refusals" role="alert">
          {entry.refusals.map((code, index) => (
            <li key={code + String(index)}>
              {t("screen.voice.refusal." + code.slice(code.lastIndexOf(".") + 1).replace(/_([a-z])/g, (_m, c: string) => c.toUpperCase()))}
            </li>
          ))}
        </ul>
      ) : null}

      {/* ── دفترُ الخطّة: ما تمّ وما لم يتمّ — **إفصاحٌ لا تراجع** ── */}
      {entry === null ? (
        <div className="vcx-plan-ledger" data-testid="voice-plan-ledger" role="status" aria-live="polite">
          <h4 className="vcx-subtitle">{t("screen.voice.plan.ledgerTitle")}</h4>
          <p className="vcx-outcome" data-testid="voice-plan-ledger-text">
            {planLedgerArabic(run)}
          </p>
        </div>
      ) : null}
    </section>
  );
}
