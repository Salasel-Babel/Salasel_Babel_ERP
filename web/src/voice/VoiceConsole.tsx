/* ═══════════════════════════════════════════════════════════════════════════
   لوحة الأمر المنطوق — للأقسام الخمسة، لا لتدفّقٍ واحد.
   ───────────────────────────────────────────────────────────────────────────
   كان الصوت في هذا المنتج يخدم شاشةً واحدة (التقاط فاتورة مورد). وهذه اللوحة
   تفتحه على **المخزون والمقاولات والموارد البشرية والعقارات** معه، من سجلٍّ
   تُسهم فيه الوحدات ولا يعرف هذا الملفّ واحدةً منها بالاسم.

   ⚠ **وقاعدةٌ واحدة تحكم كل ما هنا:** ما يكتب في الدفتر، أو يحرّك مخزوناً، أو
   يصرف لإنسان، أو يوقّع عقداً — **يُقرأ على قائله ثم يُؤكَّد صراحةً**. والاستعلام
   وحده يمرّ بلا تأكيد.

   ⚠ **والملخّص المرتدّ نصٌّ واحد يُعرض ويُنطَق معاً.** من لا يسمع يقرؤه في لوحةٍ
   لها إطارٌ ودورٌ مُعلَن، ومن لا يرى يسمعه ويُعلَن له بـaria-live. ونصّان
   ينحرفان فيؤكّد كلٌّ منهما ما لم يؤكّده الآخر — ولذلك واحد.

   ⚠ **ولا شيء هنا يبلغ الدفتر.** اللوحة تُنتج **أمراً مؤكَّداً** تُسلّمه شاشةُ
   القسم إلى عملية الوحدة المنشورة؛ والترحيل — إن وقع — يمرّ بمصفوفة الترحيل
   وحدها، ولا يُسمّى في هذا الملفّ رمزُ حسابٍ واحد (القاعدة 2 · ADR-0024).
   ═══════════════════════════════════════════════════════════════════════════ */
import "./voice.css";
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useT } from "../i18n/react";
import { VOICE_SECTIONS, intentsOf, type VoiceSection } from "./catalogue";
import {
  authorise,
  isSpokenCancellation,
  isSpokenConfirmation,
  readCommand,
  type VoiceCaller,
  type VoiceDispatch,
  type VoiceResolution,
} from "./command";
import { dropVoiceDraft, handoffOf, type VoiceDraftHandoff } from "./handoff";
import { matchPlan, readCurrentStep, startPlan, type VoicePlanRun } from "./plan";
import { VoicePlanPanel } from "./VoicePlanPanel";
import { canSpeak, hush, speak } from "./speak";
import { listen, speechSupport, type SpeechSession, type SpeechUnavailable } from "./speech";

/** خصائص اللوحة. ما يُضاف يُضاف اختيارياً، ولا يتغيّر معنى موجود. */
export interface VoiceConsoleProps {
  /** القسم المفتوح ابتداءً. الافتراضي: المحاسبة. */
  readonly section?: VoiceSection;
  /** لغة التفريغ والنُّطق. */
  readonly lang?: string;
  /** تاريخ اليوم بصيغة ISO — **يُحقن** كي يكون السلوك حتمياً. بدونه لا يُملأ تاريخ. */
  readonly today?: string;
  /** النسبة النظامية حين لا تُنطق. نصّ لا رقم. */
  readonly statutoryTaxRate?: string;
  /** المتكلّم ومنشأته وصلاحياته. */
  readonly caller: VoiceCaller;
  /** يُستدعى بالأمر بعد أن يجتاز البوابة. */
  readonly onDispatch?: (dispatch: VoiceDispatch) => void;
  /**
   * يُستدعى بـ**تسليم المسوّدة** — وهو ما تُخرجه اللوحة فعلاً بعد التأكيد.
   * ومن يتلقّاه يُودعه لشاشة المستند وينتقل إليها؛ <b>واللوحة لا تنادي باباً</b>.
   */
  readonly onDraft?: (handoff: VoiceDraftHandoff) => void;
  /**
   * نصّ الوجهة كما تعرفها طبقةُ التطبيق — «شاشة مستخلص العميل» مثلاً — أو `null`
   * حين لم تهبط شاشة هذا المستند بعد. <b>ويُعرض كما هو</b>، فلا يقفز المستخدم إلى
   * لا شيء ولا يظنّ أن أمره ضاع.
   */
  readonly destinationOf?: (intentId: string) => string | null;
  /** تفريغ يُحقن بدل الميكروفون. **يُوسَم على الشاشة وسماً ظاهراً**. */
  readonly simulatedTranscript?: string;
  /** هل يُنطَق الملخّص تلقائياً؟ الافتراضي: نعم، وللمستخدم إطفاؤه. */
  readonly speakReadback?: boolean;
}

/** حال اللوحة بعد آخر فعل. */
type Outcome =
  | { readonly kind: "none" }
  | { readonly kind: "refused"; readonly codes: readonly string[] }
  | {
      readonly kind: "confirmed";
      readonly dispatch: VoiceDispatch;
      readonly handoff: VoiceDraftHandoff | null;
      readonly destination: string | null;
    }
  | { readonly kind: "cancelled" };

/* «ai.voice.slot_missing» ← «slotMissing»: مفتاح العرض يُشتقّ من رمز الرفض
   اشتقاقاً واحداً، فلا تُكتب خريطة يُنسى نصفُها. */
function refusalKey(code: string): string {
  const tail = code.slice(code.lastIndexOf(".") + 1);
  return tail.replace(/_([a-z])/g, (_m, c: string) => c.toUpperCase());
}

/**
 * اللوحة.
 * @param props الخصائص.
 */
export function VoiceConsole(props: VoiceConsoleProps): ReactNode {
  const { t } = useT();
  const lang = props.lang ?? "ar-SA";
  const speakOn = props.speakReadback ?? true;

  const [section, setSection] = useState<VoiceSection>(props.section ?? "Accounting");
  const [transcript, setTranscript] = useState("");
  const [manual, setManual] = useState("");
  const [listening, setListening] = useState(false);
  const [simulated, setSimulated] = useState(false);
  const [unavailable, setUnavailable] = useState<SpeechUnavailable | null>(null);
  const [resolution, setResolution] = useState<VoiceResolution | null>(null);
  const [refusalCodes, setRefusalCodes] = useState<readonly string[]>([]);
  const [outcome, setOutcome] = useState<Outcome>({ kind: "none" });
  /* **جريانُ الخطّة — في الذاكرة، ويموت مع إعادة التحميل** كحافظة المسوّدة وللسبب
     نفسه بقوّةٍ أكبر: خطّةٌ تنجو تُستأنف بملخّصٍ سمعه صاحبُها ولم يعد يذكره. */
  const [planRun, setPlanRun] = useState<VoicePlanRun | null>(null);

  const session = useRef<SpeechSession | null>(null);
  const finalText = useRef("");
  const support = useMemo(() => speechSupport(), []);
  const spoken = useMemo(() => canSpeak(), []);

  const options = useMemo(
    () => ({ today: props.today, statutoryTaxRate: props.statutoryTaxRate }),
    [props.today, props.statutoryTaxRate]
  );

  const intents = useMemo(() => intentsOf(section), [section]);

  /** يقرأ نصّاً ويُظهر ما فهمه — **ولا ينفّذ شيئاً**. */
  const read = useCallback(
    (text: string) => {
      setTranscript(text);
      setOutcome({ kind: "none" });

      /* **الخطّة تُجرَّب أوّلاً — ودليلُها أقوى.** مطابقتُها تطلب طلباً **وشرطاً**، وهو
         أكثر ممّا تطلبه النيّة المفردة. وجملةٌ بلا شرطٍ لا تُطابق خطّةً أصلاً، فلا
         تنكسر جملةٌ تعمل اليوم. */
      const plan = matchPlan(text);
      if (plan !== null) {
        const started = readCurrentStep(startPlan(plan, text), options);
        setPlanRun(started);
        setResolution(null);
        setRefusalCodes([]);
        if (speakOn) speak(started.orientationAr, lang);
        return;
      }

      setPlanRun(null);
      const reading = readCommand(text, options);
      if (!reading.ok) {
        setResolution(null);
        setRefusalCodes(reading.codes);
        return;
      }

      setResolution(reading.resolution);
      setRefusalCodes([]);

      /* النُّطق **بعد** أن تمتلئ الشاشة لا قبله: من يرى يقرأ فوراً، ومن لا يرى يسمع. */
      if (speakOn && reading.resolution.intent.requiresConfirmation) {
        speak(reading.resolution.readbackAr, lang);
      }
    },
    [lang, options, speakOn]
  );

  const stop = useCallback(() => {
    session.current?.stop();
    session.current = null;
    setListening(false);
    if (finalText.current.trim().length > 0) read(finalText.current);
  }, [read]);

  const start = useCallback(() => {
    if (listening) return;
    setUnavailable(null);
    setSimulated(false);
    finalText.current = "";

    if (props.simulatedTranscript) {
      setSimulated(true);
      finalText.current = props.simulatedTranscript;
      read(props.simulatedTranscript);
      return;
    }

    setListening(true);
    session.current = listen({
      lang,
      onChunk: (chunk) => {
        if (chunk.final) finalText.current += chunk.text;
        setTranscript(finalText.current + (chunk.final ? "" : chunk.text));
      },
      onFail: (reason) => {
        setUnavailable(reason);
        setListening(false);
        session.current = null;
      },
      onEnd: () => setListening(false),
    });
  }, [lang, listening, props.simulatedTranscript, read]);

  useEffect(() => () => {
    session.current?.abort();
    hush();
  }, []);

  /**
   * يُسلّم أمراً اجتاز البوابة. <b>ولا ينادي باباً</b>: يبني تسليم المسوّدة ويُعلنه،
   * وشاشةُ المستند هي التي تنادي عمليتها المنشورة وتملك زرّ الترحيل.
   */
  const hand = useCallback(
    (dispatch: VoiceDispatch) => {
      const handoff = handoffOf(dispatch);
      const destination = handoff === null ? null : (props.destinationOf?.(dispatch.intent.id) ?? null);

      setOutcome({ kind: "confirmed", dispatch, handoff, destination });
      props.onDispatch?.(dispatch);
      if (handoff !== null) props.onDraft?.(handoff);

      /* **والهبوط يُسمع كما يُرى**: من لا يرى يعرف أين ذهبت مسوّدته، ومن لا يسمع
         يقرأ النصّ نفسه في `voice-outcome`. ونصٌّ واحد لا اثنان. */
      if (speakOn && dispatch.confirmedByHuman) {
        speak(
          destination === null
            ? t("screen.voice.console.draftHeld", { name: dispatch.intent.nameAr })
            : t("screen.voice.console.draftHanded", { name: dispatch.intent.nameAr }),
          lang
        );
      }
    },
    [lang, props, speakOn, t]
  );

  /** التأكيد — **الباب الوحيد** إلى أمرٍ ينفَّذ. */
  const confirm = useCallback(() => {
    if (!resolution) return;
    hush();

    const decided = authorise(resolution, props.caller, resolution.confirmationToken);
    if (!decided.ok) {
      setOutcome({ kind: "refused", codes: decided.codes });
      return;
    }

    hand(decided.dispatch);
  }, [hand, props.caller, resolution]);

  /** التنفيذ بلا تأكيد — مسموحٌ للاستعلام وحده، والبوابة هي التي تقرّر لا هذا الملفّ. */
  const run = useCallback(() => {
    if (!resolution) return;

    const decided = authorise(resolution, props.caller, null);
    if (!decided.ok) {
      setOutcome({ kind: "refused", codes: decided.codes });
      return;
    }

    hand(decided.dispatch);
  }, [hand, props.caller, resolution]);

  const cancel = useCallback(() => {
    hush();
    dropVoiceDraft();
    setOutcome({ kind: "cancelled" });
    setResolution(null);
    setPlanRun(null);
  }, []);

  /* «تأكيد» و«إلغاء» منطوقتان: من يعمل بيدين مشغولتين يؤكّد بصوته أيضاً. */
  const applyManual = useCallback(() => {
    const text = manual;
    if (resolution && isSpokenConfirmation(text)) {
      confirm();
      return;
    }
    if (resolution && isSpokenCancellation(text)) {
      cancel();
      return;
    }
    setSimulated(true);
    read(text);
  }, [cancel, confirm, manual, read, resolution]);

  const blocked = support !== "supported" ? support : unavailable;
  const needsConfirmation = resolution?.intent.requiresConfirmation ?? false;
  const complete = resolution?.missingSlots.length === 0;

  return (
    <section className="vc vcx" data-testid="voice-console" dir="auto">
      <h2 className="vc-title">{t("screen.voice.console.title")}</h2>
      <p className="vc-note">{t("screen.voice.console.hint")}</p>

      {/* ── الأقسام الخمسة: الصوت مدخلٌ إلى النظام لا ميزةٌ في شاشة ── */}
      <div className="vcx-sections" role="tablist" aria-label={t("screen.voice.console.sectionLabel")}>
        {VOICE_SECTIONS.map((entry) => (
          <button
            key={entry.id}
            type="button"
            role="tab"
            className="vcx-section"
            data-testid={"voice-section-" + entry.id}
            aria-selected={entry.id === section}
            data-active={entry.id === section ? "true" : "false"}
            onClick={() => setSection(entry.id)}
          >
            {t(entry.labelKey)}
          </button>
        ))}
      </div>

      <h3 className="vcx-subtitle">{t("screen.voice.console.intentsLabel")}</h3>
      <ul className="vcx-intents" data-testid="voice-intents">
        {intents.map((intent) => (
          <li key={intent.id} className="vcx-intent" data-testid={"voice-intent-" + intent.id}>
            <span className="vcx-intent-name">{intent.nameAr}</span>
            <span className="vcx-intent-say">{intent.phrases[0]}</span>
            <span
              className="vcx-intent-gate"
              data-gate={intent.requiresConfirmation ? "confirm" : "direct"}
            >
              {intent.requiresConfirmation
                ? t("screen.voice.console.gateConfirm")
                : t("screen.voice.console.gateDirect")}
            </span>
          </li>
        ))}
      </ul>

      <button
        type="button"
        className={"vc-hold" + (listening ? " vc-hold-live" : "")}
        data-testid="voice-hold"
        aria-pressed={listening}
        disabled={blocked !== null && !props.simulatedTranscript}
        onPointerDown={start}
        onPointerUp={stop}
        onPointerLeave={listening ? stop : undefined}
      >
        {listening ? t("screen.voice.listening") : t("screen.voice.hold")}
      </button>

      {simulated ? (
        <p className="vc-simulated" data-testid="voice-simulated" role="status">
          {t("screen.voice.simulated")}
        </p>
      ) : null}

      {blocked ? (
        <p className="vc-blocked" data-testid="voice-unavailable" role="status">
          {t("screen.voice.unavailable." + blocked.replace(/-([a-z])/g, (_m, c: string) => c.toUpperCase()))}
        </p>
      ) : null}

      {spoken ? null : (
        <p className="vc-note" data-testid="voice-silent-browser">
          {t("screen.voice.console.silentBrowser")}
        </p>
      )}

      {/* المسار المكتوب ليس بديلاً للمعاقين وحدهم: هو المسار الوحيد القابل
          للاختبار آلياً في متصفّح بلا رأس، ولذلك **حاضرٌ دائماً**. */}
      <div className="vc-manual">
        <label className="vc-manual-label" htmlFor="vcx-manual">
          {t("screen.voice.console.transcriptLabel")}
        </label>
        <input
          id="vcx-manual"
          className="vc-manual-input"
          data-testid="voice-manual-input"
          value={manual}
          onChange={(event) => setManual(event.target.value)}
        />
        <button
          type="button"
          className="vc-manual-apply"
          data-testid="voice-manual-apply"
          onClick={applyManual}
        >
          {t("screen.voice.console.apply")}
        </button>
      </div>

      <p className="vc-transcript" data-testid="voice-transcript" aria-live="polite">
        {transcript || t("screen.voice.empty")}
      </p>

      {refusalCodes.length > 0 ? (
        <ul className="vc-faults" data-testid="voice-refusals" role="alert">
          {refusalCodes.map((code) => (
            <li key={code}>{t("screen.voice.refusal." + refusalKey(code))}</li>
          ))}
        </ul>
      ) : null}

      {planRun !== null ? (
        <VoicePlanPanel
          run={planRun}
          caller={props.caller}
          options={options}
          lang={lang}
          speakOn={speakOn}
          say={speak}
          onRun={setPlanRun}
          onDraft={props.onDraft}
          destinationOf={props.destinationOf}
          onCancel={cancel}
        />
      ) : null}

      {resolution ? (
        <div className="vcx-understood" data-testid="voice-understood">
          <h3 className="vcx-subtitle" data-testid="voice-intent-name">
            {resolution.intent.nameAr}
          </h3>

          <dl className="vc-fields">
            {resolution.intent.slots.map((slot) => {
              const value = resolution.slots.find((candidate) => candidate.name === slot.name);
              return (
                <div
                  key={slot.name}
                  className="vc-field"
                  data-testid={"voice-field-" + slot.name}
                  data-provenance={value?.provenance ?? "none"}
                >
                  <dt className="vc-field-label">{slot.nameAr}</dt>
                  <dd className="vc-field-value" data-testid={"voice-value-" + slot.name}>
                    {value ? value.text + (value.unit ? " " + value.unit : "") : t("screen.voice.blank")}
                  </dd>
                  <dd className="vc-field-source">
                    {value ? t("screen.voice.provenance." + value.provenance) : ""}
                  </dd>
                </div>
              );
            })}
          </dl>

          {resolution.missingSlots.length > 0 ? (
            <ul className="vc-faults" data-testid="voice-missing" role="alert">
              {resolution.missingSlots.map((name) => (
                <li key={name}>
                  {t("screen.voice.refusal.slotMissing", {
                    slot:
                      resolution.intent.slots.find((slot) => slot.name === name)?.nameAr ?? name,
                  })}
                </li>
              ))}
            </ul>
          ) : null}

          {resolution.faults.length > 0 ? (
            <ul className="vc-faults" data-testid="voice-faults" role="alert">
              {resolution.faults.map((code) => (
                <li key={code}>{t("screen.voice.refusal." + refusalKey(code))}</li>
              ))}
            </ul>
          ) : null}

          {resolution.intent.status === "AwaitingOwnerDecision" ? (
            <p className="vc-blocked" data-testid="voice-awaiting-owner" role="status">
              {t("screen.voice.refusal.ownerDecisionPending")}
            </p>
          ) : null}

          {/* ── القراءة المرتدّة: تُرى وتُسمع، والتأكيد لا يُتجاوَز ── */}
          {needsConfirmation ? (
            <div
              className="vcx-readback"
              data-testid="voice-readback"
              role="group"
              aria-labelledby="vcx-readback-title"
            >
              <h4 className="vcx-subtitle" id="vcx-readback-title">
                {t("screen.voice.console.readbackTitle")}
              </h4>
              <p className="vcx-readback-text" data-testid="voice-readback-text" aria-live="assertive">
                {resolution.readbackAr}
              </p>
              <div className="vcx-actions">
                <button
                  type="button"
                  className="vc-manual-apply"
                  data-testid="voice-confirm"
                  disabled={!complete || resolution.intent.status === "AwaitingOwnerDecision"}
                  onClick={confirm}
                >
                  {t("screen.voice.console.confirm")}
                </button>
                <button
                  type="button"
                  className="vc-manual-apply"
                  data-testid="voice-cancel"
                  onClick={cancel}
                >
                  {t("screen.voice.console.cancel")}
                </button>
                <button
                  type="button"
                  className="vc-manual-apply"
                  data-testid="voice-speak-again"
                  disabled={!spoken}
                  onClick={() => speak(resolution.readbackAr, lang)}
                >
                  {t("screen.voice.console.speakAgain")}
                </button>
              </div>
            </div>
          ) : (
            <div className="vcx-actions">
              <button
                type="button"
                className="vc-manual-apply"
                data-testid="voice-run"
                onClick={run}
              >
                {t("screen.voice.console.run")}
              </button>
            </div>
          )}
        </div>
      ) : null}

      {/* ── ما بعد التأكيد: المسوّدة تُرى كما تُسمع، ووجهتُها تُقال باسمها ── */}
      {outcome.kind === "confirmed" ? (
        <div className="vcx-handoff" data-testid="voice-handoff">
          <p className="vcx-outcome" data-testid="voice-outcome" role="status" aria-live="polite">
            {outcome.dispatch.confirmedByHuman
              ? outcome.destination === null
                ? t("screen.voice.console.draftHeld", { name: outcome.dispatch.intent.nameAr })
                : t("screen.voice.console.draftHanded", { name: outcome.dispatch.intent.nameAr })
              : t("screen.voice.console.queryReady")}
          </p>

          {outcome.handoff ? (
            <>
              <p className="vc-note" data-testid="voice-handoff-operation" data-operation={outcome.handoff.operationId}>
                {t("screen.voice.console.handoffOperation", { operation: outcome.handoff.operationId })}
              </p>
              <p className="vc-note" data-testid="voice-handoff-destination" data-destination={outcome.destination ?? ""}>
                {outcome.destination === null
                  ? t("screen.voice.console.screenNotLanded")
                  : t("screen.voice.console.destination", { path: outcome.destination })}
              </p>
              <dl className="vc-fields" data-testid="voice-handoff-fields">
                {outcome.handoff.fields.map((field) => (
                  <div key={field.name} className="vc-field" data-provenance={field.provenance}>
                    <dt className="vc-field-label">{field.nameAr}</dt>
                    <dd className="vc-field-value" data-testid={"voice-handoff-value-" + field.name}>
                      {field.text + (field.unit ? " " + field.unit : "")}
                    </dd>
                    <dd className="vc-field-source">{t("screen.voice.provenance." + field.provenance)}</dd>
                  </div>
                ))}
              </dl>
              <p className="vc-not-a-fact" data-testid="voice-post-is-not-spoken">
                {t("screen.voice.console.postIsNotSpoken")}
              </p>
            </>
          ) : null}
        </div>
      ) : null}

      {outcome.kind === "cancelled" ? (
        <p className="vcx-outcome" data-testid="voice-outcome" role="status" aria-live="polite">
          {t("screen.voice.console.cancelled")}
        </p>
      ) : null}

      {outcome.kind === "refused" ? (
        <ul className="vc-faults" data-testid="voice-outcome-refusals" role="alert">
          {outcome.codes.map((code, index) => (
            <li key={code + index}>{t("screen.voice.refusal." + refusalKey(code))}</li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}
