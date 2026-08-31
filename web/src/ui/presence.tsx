/* ═══════════════════════════════════════════════════════════════════════════
   طبقة الحضور الذكي — المكوّنات  ·  The intelligent-presence components
   ───────────────────────────────────────────────────────────────────────────
   هذه هي المكوّنات التي تجعل الذكاء **مرئياً وصادقاً**. وكلمة «صادقاً» هي
   القيد: كلّ مكوّن هنا **يمتنع عن الظهور** حين لا يكون له ما يقوله.
     · درجة الثقة لا تُعرض على قيمةٍ كتبها المستخدم — النظام لا «يثق» بما لم يقله.
     · وسم المصدر لا يُوضَع على المُدخَل — الأصل لا يُوسَم، والاستثناء يُوسَم.
     · الكشف المتدفّق يكشف ما وُجد فعلاً، ولا يخترع صفوفاً ليبدو مشغولاً.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { CSSProperties, ReactNode } from "react";
import { Num } from "../i18n/react";
import { revealAt } from "./motion";
import type { Provenance } from "./primitives";

/* ═══════════════════════════════════════════════════ ١ · درجة الثقة */

/** نطاق الثقة الثلاثي: امضِ · راجع · لا تعتمد عليه. */
export type ConfidenceBand = "high" | "medium" | "low";

/**
 * يصنّف نسبة ثقةٍ **نصّية** إلى نطاقها. النسبة نصٌّ ولا تصير عائماً.
 * @param percent النسبة من ٠ إلى ١٠٠ نصّاً بأرقام لاتينية.
 */
export function bandOf(percent: string): ConfidenceBand {
  /* مقارنةٌ نصّية بالطول ثم بالمحارف: لا `Number` ولا `parseInt` في هذا
     المستودع، والنسبة قد تأتي من السلك فتخضع للقاعدة نفسها التي يخضع لها
     المال. والمقارنة هنا على أعدادٍ صحيحة ٠–١٠٠ فقط. */
  const digits = percent.trim();
  const width = digits.length;
  if (width >= 3) return "high";
  if (width <= 1) return "low";
  const first = digits.charCodeAt(0);
  if (first >= 0x38) return "high"; /* 8 أو 9 */
  if (first >= 0x36) return "medium"; /* 6 أو 7 */
  return "low";
}

/**
 * درجة ثقة. **تُعرض حين يستنتج النظام، ولا تُعرض حين يكون الرقم مُدخَلاً.**
 * @param props النسبة نصّاً وتسميتها لقارئ الشاشة.
 */
export function ConfidenceMeter(props: {
  readonly percent: string;
  readonly label: string;
  readonly testId?: string;
}): ReactNode {
  const band = bandOf(props.percent);
  return (
    <span
      className="confidence"
      data-band={band}
      data-testid={props.testId}
      title={props.label}
    >
      <span
        className="confidence__rail"
        role="meter"
        aria-label={props.label}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuetext={props.percent}
      >
        <span
          className="confidence__fill"
          style={{ "--confidence": props.percent } as CSSProperties}
        />
      </span>
      <span className="confidence__value" dir="ltr">
        <Num value={props.percent} />
      </span>
    </span>
  );
}

/* ═══════════════════════════════════════════════════ ٢ · وسم المصدر */

/**
 * وسمٌ بصريّ يفرّق المُستنتَج عن المُدخَل — **مطلبٌ محاسبي لا تجميلي**:
 * من يقرأ القائمة يجب أن يعرف مصدر كل رقم.
 * @param props المصدر ونصّه المترجَم.
 */
export function ProvenanceMark(props: {
  readonly source: Provenance;
  readonly label: string;
  readonly testId?: string;
}): ReactNode {
  return (
    <span className="prov" data-source={props.source} data-testid={props.testId}>
      {props.label}
    </span>
  );
}

/**
 * قيمةٌ داخل نصّ أو خليّة، موسومةٌ حين يكون النظام هو من اشتقّها.
 * @param props المحتوى وهل هو مُستنتَج.
 */
export function InferredValue(props: {
  readonly children: ReactNode;
  readonly inferred: boolean;
  readonly title?: string;
}): ReactNode {
  if (!props.inferred) return <>{props.children}</>;
  return (
    <span className="inferred" title={props.title}>
      {props.children}
    </span>
  );
}

/* ══════════════════════════════════════════════ ٣ · الكشف المتدفّق */

/**
 * كشفٌ متدفّق لما يُستنتَج — بدل ظهورٍ مفاجئ. كل ابنٍ يتأخّر بترتيبه.
 * @param props الأبناء وهل بدأ الكشف.
 */
export function StreamingReveal(props: {
  readonly items: readonly ReactNode[];
  readonly on: boolean;
  readonly testId?: string;
}): ReactNode {
  return (
    <div className="stack" data-testid={props.testId}>
      {props.items.map((item, index) => (
        <div
          key={index}
          className={props.on ? "cine-reveal" : undefined}
          style={props.on ? revealAt(index) : undefined}
        >
          {item}
        </div>
      ))}
    </div>
  );
}

/* ═══════════════════════════════════════════════════ ٤ · أثر الصوت */

/** خطوةٌ في رحلة الصوت. */
export interface TraceStep {
  readonly key: string;
  readonly label: string;
  /** ما أنتجته الخطوة — فارغٌ حين لم تقع بعد. */
  readonly value?: string;
  readonly state: "idle" | "active" | "done";
}

/**
 * أثر الصوت: النظام **يسمع → يفرّغ → يفهم نيّة → يملأ حقولاً**. الرحلة كلّها
 * مرئية، لا نتيجتها وحدها — فمن يرى الخطوة التي وقف عندها النظام يعرف لماذا
 * الحقل فارغ.
 * @param props الخطوات.
 */
export function VoiceTrace(props: {
  readonly steps: readonly TraceStep[];
  readonly testId?: string;
}): ReactNode {
  return (
    <div className="trace" data-testid={props.testId}>
      {props.steps.map((step) => (
        <div
          key={step.key}
          className={"trace__step" + (step.state === "active" ? " cine-live" : "")}
          data-state={step.state}
        >
          <span className="trace__k">{step.label}</span>
          <span className="trace__v">{step.value ?? ""}</span>
        </div>
      ))}
    </div>
  );
}

/* ═════════════════════════════════════════════════ ٥ · لوح الحضور */

/**
 * لوحٌ يقول «النظام يفكّر الآن» — الوعاء الذي يجمع الثقة والأثر والكشف.
 * @param props العنوان والملاحظة والمحتوى.
 */
export function PresencePanel(props: {
  readonly title: string;
  readonly note?: string;
  readonly aside?: ReactNode;
  readonly children: ReactNode;
  readonly working?: boolean;
  readonly testId?: string;
}): ReactNode {
  return (
    <section className="presence" data-testid={props.testId} data-working={props.working ? "true" : "false"}>
      <div className="presence__head">
        <span className={"prov" + (props.working ? " cine-live" : "")} data-source="inferred" aria-hidden="true" />
        <span>{props.title}</span>
        {props.aside ? <span className="spacer">{props.aside}</span> : null}
      </div>
      {props.note ? <p className="presence__note">{props.note}</p> : null}
      {props.children}
    </section>
  );
}
