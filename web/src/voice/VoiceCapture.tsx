/* ═══════════════════════════════════════════════════════════════════════════
   الإدخال الصوتي العربي — مكوّن مستقلّ قابل للتركيب.
   ───────────────────────────────────────────────────────────────────────────
   الأثر المقصود ليس الزرّ: هو أن **يمتلئ الحقل والمستخدم ما زال يتكلّم**، وأن
   يحمل كلّ حقل **لون مصدره**. والمصدر السادس «منطوق» يُعاد استعماله من نموذج
   المصادر القائم ولا يُخترَع بجانبه مفهوم ثانٍ.

   والضغط المستمرّ لا الاستماع الدائم: الاستماع الدائم يقطع في منتصف رقم، ويلتقط
   زميلاً يتكلّم في المكتب. والضغط يُلغي سؤال «متى انتهى الكلام؟» بدل أن يجيب عنه.

   ولا شيء هنا يصير حقيقة محاسبية: يملأ مسوّدة يؤكّدها إنسان (ADR-0024).
   ═══════════════════════════════════════════════════════════════════════════ */
import "./voice.css";
import { useCallback, useEffect, useMemo, useRef, useState, type ReactNode } from "react";
import { useT } from "../i18n/react";
import { FIELD, readInvoiceIntent, type SpokenIntent, type SpokenValue } from "./intent";
import { listen, speechSupport, type SpeechSession, type SpeechUnavailable } from "./speech";

/** حقل تعرضه اللوحة. مفتاحه هو مفتاح الحقل في مسوّدة الخادم حرفياً. */
export interface VoiceField {
  readonly key: string;
  /** مفتاح مورد لتسمية الحقل. */
  readonly labelKey: string;
}

/** الحقول الافتراضية: فاتورة مورد. */
export const INVOICE_FIELDS: readonly VoiceField[] = [
  { key: FIELD.sellerName, labelKey: "screen.voice.field.sellerName" },
  { key: FIELD.invoiceNumber, labelKey: "screen.voice.field.invoiceNumber" },
  { key: FIELD.grossTotal, labelKey: "screen.voice.field.grossTotal" },
  { key: FIELD.taxRate, labelKey: "screen.voice.field.taxRate" },
  { key: FIELD.issuedOn, labelKey: "screen.voice.field.issuedOn" },
  { key: FIELD.suggestedEvent, labelKey: "screen.voice.field.suggestedEvent" },
];

/** واجهة المكوّن — **مستقرّة**: ما يُضاف يُضاف اختيارياً، ولا يتغيّر معنى موجود. */
export interface VoiceCaptureProps {
  /** لغة التفريغ. الافتراضي العربية السعودية. */
  readonly lang?: string;
  /** تاريخ اليوم بصيغة ISO — يُحقن كي يكون السلوك حتمياً في الاختبار وفي التسجيل. */
  readonly today?: string;
  /** النسبة النظامية حين لا تُنطق. نصّ لا رقم. */
  readonly statutoryTaxRate?: string;
  /** الحقول المعروضة. */
  readonly fields?: readonly VoiceField[];
  /**
   * قراءة النيّة بنموذج بدل القارئ الحتمي. اختياري تماماً:
   * غيابُه يعني أن المكوّن يعمل بلا شبكة وبلا مفتاح.
   */
  readonly resolveIntent?: (transcript: string) => Promise<SpokenIntent>;
  /** يُستدعى عند كل تغيّر — بما فيه النتائج الأوّلية أثناء الكلام. */
  readonly onChange?: (intent: SpokenIntent, transcript: string) => void;
  /** يُستدعى مرّة عند إفلات الزرّ بالنصّ النهائي. */
  readonly onCommit?: (intent: SpokenIntent, transcript: string) => void;
  /** يُستدعى حين يتعذّر التفريغ، بسببٍ مُسمّى. */
  readonly onUnavailable?: (reason: SpeechUnavailable) => void;
  /** إتاحة إدخال التفريغ نصّاً حين يتعذّر الصوت. الافتراضي: نعم. */
  readonly allowManualTranscript?: boolean;
  /** تفريغ يُحقن بدل الميكروفون. **يُوسَم على الشاشة وسماً ظاهراً**. */
  readonly simulatedTranscript?: string;
}

const NUMERIC_FIELDS = new Set<string>([FIELD.grossTotal, FIELD.taxRate]);

/**
 * لوحة الإدخال الصوتي.
 * @param props الخصائص.
 */
export function VoiceCapture(props: VoiceCaptureProps): ReactNode {
  const { t } = useT();
  const fields = props.fields ?? INVOICE_FIELDS;
  const lang = props.lang ?? "ar-SA";
  const statutoryTaxRate = props.statutoryTaxRate ?? "0.15";

  const [transcript, setTranscript] = useState("");
  const [listening, setListening] = useState(false);
  const [unavailable, setUnavailable] = useState<SpeechUnavailable | null>(null);
  const [values, setValues] = useState<readonly SpokenValue[]>([]);
  const [faults, setFaults] = useState<readonly string[]>([]);
  const [simulated, setSimulated] = useState(false);
  const [manual, setManual] = useState("");

  const session = useRef<SpeechSession | null>(null);
  const finalText = useRef("");
  const support = useMemo(() => speechSupport(), []);

  const apply = useCallback(
    (text: string, commit: boolean) => {
      const intent = readInvoiceIntent(text, { statutoryTaxRate, today: props.today });
      setValues(intent.values);
      setFaults(intent.faults);
      props.onChange?.(intent, text);
      if (!commit) return;

      /* النموذج — إن رُكِّب — يُسأل **بعد** أن يملأ القارئ الحتمي الشاشة، لا قبله.
         فالمستخدم يرى الامتلاء فوراً، وما يزيده النموذج يصل متأخّراً بلا شاشة فارغة. */
      const resolve = props.resolveIntent;
      if (resolve) {
        void resolve(text)
          .then((better) => {
            setValues(better.values);
            setFaults(better.faults);
            props.onCommit?.(better, text);
          })
          .catch(() => props.onCommit?.(intent, text));
        return;
      }

      props.onCommit?.(intent, text);
    },
    [props, statutoryTaxRate]
  );

  const stop = useCallback(() => {
    session.current?.stop();
    session.current = null;
    setListening(false);
    if (finalText.current.trim().length > 0) apply(finalText.current, true);
  }, [apply]);

  const start = useCallback(() => {
    if (listening) return;
    setUnavailable(null);
    setSimulated(false);
    finalText.current = "";
    setTranscript("");

    /* المسار المُحاكى: نصّ يُحقن يمرّ بنفس خطّ المعالجة تماماً — ويُوسَم. */
    if (props.simulatedTranscript) {
      setSimulated(true);
      setTranscript(props.simulatedTranscript);
      finalText.current = props.simulatedTranscript;
      apply(props.simulatedTranscript, false);
      setListening(true);
      return;
    }

    setListening(true);
    session.current = listen({
      lang,
      onChunk: (chunk) => {
        const next = chunk.final ? finalText.current + chunk.text : finalText.current + chunk.text;
        if (chunk.final) finalText.current += chunk.text;
        setTranscript(next);
        apply(next, false);
      },
      onFail: (reason) => {
        setUnavailable(reason);
        setListening(false);
        session.current = null;
        props.onUnavailable?.(reason);
      },
      onEnd: () => setListening(false),
    });
  }, [apply, lang, listening, props]);

  useEffect(() => () => session.current?.abort(), []);

  const blocked = support !== "supported" ? support : unavailable;
  const showManual = (props.allowManualTranscript ?? true) && blocked !== null;

  return (
    <section className="vc" data-testid="voice-capture" dir="auto">
      <h2 className="vc-title">{t("screen.voice.title")}</h2>
      <p className="vc-note" data-testid="voice-not-a-fact">
        {t("screen.voice.notAFact")}
      </p>

      <button
        type="button"
        className={"vc-hold" + (listening ? " vc-hold-live" : "")}
        data-testid="voice-hold"
        aria-pressed={listening}
        disabled={blocked !== null && !props.simulatedTranscript}
        onPointerDown={start}
        onPointerUp={stop}
        onPointerLeave={listening ? stop : undefined}
        onKeyDown={(event) => {
          if (event.key === " " || event.key === "Enter") {
            event.preventDefault();
            start();
          }
        }}
        onKeyUp={(event) => {
          if (event.key === " " || event.key === "Enter") stop();
        }}
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
          {t("screen.voice.unavailable." + reasonKey(blocked))}
        </p>
      ) : null}

      {showManual ? (
        <div className="vc-manual">
          <label className="vc-manual-label" htmlFor="vc-manual-input">
            {t("screen.voice.manualLabel")}
          </label>
          <input
            id="vc-manual-input"
            className="vc-manual-input"
            data-testid="voice-manual-input"
            value={manual}
            onChange={(event) => setManual(event.target.value)}
          />
          <button
            type="button"
            className="vc-manual-apply"
            data-testid="voice-manual-apply"
            onClick={() => {
              setSimulated(true);
              setTranscript(manual);
              apply(manual, true);
            }}
          >
            {t("screen.voice.manualApply")}
          </button>
        </div>
      ) : null}

      <p className="vc-transcript" data-testid="voice-transcript" aria-live="polite">
        {transcript || t("screen.voice.empty")}
      </p>

      <dl className="vc-fields">
        {fields.map((field) => {
          const value = values.find((candidate) => candidate.field === field.key);
          return (
            <div
              key={field.key}
              className="vc-field"
              data-testid={"voice-field-" + field.key}
              data-provenance={value?.provenance ?? "none"}
            >
              <dt className="vc-field-label">{t(field.labelKey)}</dt>
              <dd className="vc-field-value" data-testid={"voice-value-" + field.key}>
                {value ? format(value) : t("screen.voice.blank")}
              </dd>
              <dd className="vc-field-source" data-testid={"voice-source-" + field.key}>
                {value ? t("screen.voice.provenance." + value.provenance) : ""}
              </dd>
            </div>
          );
        })}
      </dl>

      {faults.length > 0 ? (
        <ul className="vc-faults" data-testid="voice-faults">
          {faults.map((code) => (
            <li key={code} className="vc-fault">
              {t("screen.voice.fault." + short(code))}
            </li>
          ))}
        </ul>
      ) : null}
    </section>
  );
}

/* «no-audio» ← «noAudio»: اصطلاح المفاتيح في هذا المستودع لا يقبل الشرطة،
   ورموز المتصفّح تحملها. الاشتقاق في موضع واحد لا في كل نداء. */
function reasonKey(reason: SpeechUnavailable): string {
  return reason.replace(/-([a-z])/g, (_m, c: string) => c.toUpperCase());
}

/* القيمة تُعرض كما هي نصّاً: لا parseFloat ولا toLocaleString على مبلغ. */
function format(value: SpokenValue): string {
  return NUMERIC_FIELDS.has(value.field) ? value.text : value.text;
}

/* «ai.voice.no_amount_heard» ← «noAmountHeard»: مفتاح العرض يُشتقّ من رمز
   الخطأ اشتقاقاً واحداً، فلا تُكتب خريطة تُنسى نصفُها. */
function short(code: string): string {
  const tail = code.slice(code.lastIndexOf(".") + 1);
  return tail.replace(/_([a-z])/g, (_m, c: string) => c.toUpperCase());
}
