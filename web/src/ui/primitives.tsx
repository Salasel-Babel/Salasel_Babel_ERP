/* ═══════════════════════════════════════════════════════════════════════════
   الأوّليّات — ما يبني عليه الخمسة  ·  The primitives the five agents build on
   ───────────────────────────────────────────────────────────────────────────
   **قاعدةٌ مالية تكسر أرقاماً إن خُولفت:** المال والكمّية والنسبة **نصوصٌ على
   السلك** في هذا المستودع — لا `number` ولا `double`. فكل أوّليّة هنا تستقبل
   {@link Money} أو نصّاً، وتعرضه بـ`<Amount>` أو `<Num>` عبر طبقة التدويل،
   و**لا تُحوِّل إلى عائم أبداً**. ومن يمرّر رقماً إلى حقلٍ مالي يرمي `Money`
   قبل أن يصل إلى الشاشة، لا بعد أن يصل خطأً.

   **ولا نصّ مرئي مكتوب هنا:** كل أوّليّة تستقبل نصّها من الشاشة، والشاشة
   تأخذه من `useT()`. وهذا يفرضه `scripts/audit.mjs` فحصاً حاكماً.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, type CSSProperties, type ReactNode } from "react";
import type { Money } from "../api/money";
import { Amount, Decimal, Num, Rendered, useLocale } from "../i18n/react";

/* ═══════════════════════════════════════════════════════ ١ · سطح ولوح */

/** خصائص السطح. */
export interface SurfaceProps {
  readonly children: ReactNode;
  /** درجة الارتفاع — تختار الظلّ من سلّم `--elevation-*`. */
  readonly lift?: "flat" | "raised" | "floating";
  readonly className?: string;
  readonly testId?: string;
}

/**
 * سطحٌ مرتفع بلا رأس ولا حشو مفروض — أدنى وعاءٍ في النظام.
 * @param props المحتوى ودرجة الارتفاع.
 */
export function Surface(props: SurfaceProps): ReactNode {
  const lift = props.lift ?? "raised";
  return (
    <div
      className={"card " + (props.className ?? "")}
      data-testid={props.testId}
      style={{ boxShadow: `var(--elevation-${lift})` }}
    >
      {props.children}
    </div>
  );
}

/** خصائص اللوح. */
export interface PanelProps {
  /** عنوان اللوح — نصٌّ مترجَم تمرّره الشاشة. */
  readonly title: string;
  /** ملاحظةٌ تحت العنوان. */
  readonly note?: string;
  /** ما يوضع في نهاية شريط الرأس: شارة، زرّ. */
  readonly aside?: ReactNode;
  readonly children: ReactNode;
  readonly className?: string;
  readonly testId?: string;
}

/**
 * لوحٌ ذو رأسٍ وجسم — الوعاء الأشيع في الشاشات.
 * @param props العنوان والملاحظة والمحتوى.
 */
export function Panel(props: PanelProps): ReactNode {
  return (
    <section className={"card " + (props.className ?? "")} data-testid={props.testId}>
      <div className="card-hd">
        <strong>{props.title}</strong>
        {props.aside ? <span className="spacer">{props.aside}</span> : null}
      </div>
      <div className="card-pad">
        {props.note ? <p className="muted">{props.note}</p> : null}
        {props.children}
      </div>
    </section>
  );
}

/* ═════════════════════════════════════════════════ ٢ · بطاقة إحصاء */

/** خصائص بطاقة الإحصاء. */
export interface StatCardProps {
  /** اسم القيمة. */
  readonly label: string;
  /** المبلغ — يُعرض بطبقة التدويل ولا يصير رقماً. */
  readonly amount?: Money;
  /** بديلٌ عن المبلغ: عددٌ صحيح أو نصٌّ مُعدّ سلفاً. */
  readonly count?: number | string;
  /** شرحٌ صغير تحت القيمة. */
  readonly hint?: string;
  /** نغمةُ القيمة: طرف القيد أو حالة. */
  readonly tone?: "neutral" | "debit" | "credit" | "good" | "bad";
  /** صنفُ حركةٍ من {@link MOTION} حين تصل القيمة من الخادم. */
  readonly moment?: string;
  readonly testId?: string;
}

/**
 * بطاقة إحصاء: اسمٌ وقيمةٌ كبيرة وشرح.
 * @param props الاسم والقيمة والنغمة.
 */
export function StatCard(props: StatCardProps): ReactNode {
  const tone = props.tone ?? "neutral";
  return (
    <div
      className={"stat " + (props.moment ?? "")}
      data-tone={tone}
      data-testid={props.testId}
    >
      <span className="k">{props.label}</span>
      <span className="v">
        {props.amount ? (
          <Amount value={props.amount} className={tone === "debit" ? "amt--debit" : tone === "credit" ? "amt--credit" : ""} />
        ) : props.count !== undefined ? (
          <Num value={props.count} />
        ) : null}
      </span>
      {props.hint ? <span className="s">{props.hint}</span> : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════════ ٣ · حقل وزرّ */

/** خصائص الحقل. */
export interface FieldProps {
  readonly id: string;
  readonly label: string;
  readonly hint?: string;
  /** رسالة رفضٍ على الحقل — تُعرض ويُوسَم الحقل بها لقارئ الشاشة. */
  readonly error?: string;
  readonly required?: boolean;
  /** مصدرُ القيمة، إن كان النظام هو من ملأها. */
  readonly source?: Provenance;
  readonly children: ReactNode;
}

/** مصادر القيمة الستّة — بأسماء الخادم نفسها. */
export type Provenance = "attested" | "read" | "spoken" | "inferred" | "defaulted" | "typed";

/**
 * حقلٌ بتسمية وتلميح ورسالة رفض، ويحمل أثر مصدره على بدايته.
 * @param props المعرّف والتسمية والمحتوى.
 */
export function Field(props: FieldProps): ReactNode {
  return (
    <div
      className={"field" + (props.source ? " prov-field" : "")}
      data-source={props.source}
    >
      <label htmlFor={props.id}>
        {props.label}
        {props.required ? <span className="req" aria-hidden="true">{"*"}</span> : null}
      </label>
      {props.children}
      {props.hint ? <span className="hint">{props.hint}</span> : null}
      {props.error ? (
        <span className="field-error" role="alert">
          {props.error}
        </span>
      ) : null}
    </div>
  );
}

/** خصائص الزرّ. */
export interface ButtonProps {
  readonly label: string;
  readonly onClick?: () => void;
  readonly kind?: "default" | "primary" | "danger" | "ghost";
  readonly size?: "base" | "sm";
  readonly loading?: boolean;
  readonly disabled?: boolean;
  readonly testId?: string;
  readonly ariaKeyshortcuts?: string;
}

/**
 * زرّ. النصّ يأتي مترجَماً من الشاشة، ولا يُكتب هنا.
 * @param props التسمية والصنف والحدث.
 */
export function Button(props: ButtonProps): ReactNode {
  const kind = props.kind ?? "default";
  const classes = [
    "btn",
    kind === "primary" ? "btn-primary" : kind === "danger" ? "btn-danger" : kind === "ghost" ? "btn-ghost" : "",
    props.size === "sm" ? "btn-sm" : "",
  ]
    .filter(Boolean)
    .join(" ");
  return (
    <button
      type="button"
      className={classes}
      data-loading={props.loading ? "true" : undefined}
      disabled={props.disabled}
      onClick={props.onClick}
      data-testid={props.testId}
      aria-keyshortcuts={props.ariaKeyshortcuts}
    >
      {props.label}
    </button>
  );
}

/* ══════════════════════════════════════════════════ ٤ · شارة الحالة */

/** حالات المستند المحاسبي — لا تُخترع سابعة في الواجهة. */
export type DocState =
  | "draft"
  | "posted"
  | "reversed"
  | "pending"
  | "rejected"
  | "archived"
  | "debit"
  | "credit"
  | "info";

/**
 * شارة حالة. اللون يأتي من الرمز الدلالي للحالة لا من اختيارٍ في الشاشة.
 * @param props الحالة ونصّها.
 */
export function StatusBadge(props: {
  readonly state: DocState;
  readonly label: string;
  readonly title?: string;
  readonly testId?: string;
}): ReactNode {
  return (
    <span
      className={"pill pill--" + props.state}
      title={props.title}
      data-state={props.state}
      data-testid={props.testId}
    >
      {props.label}
    </span>
  );
}

/* ═════════════════════════════════════ ٥ · لوحة الرفض ثنائية اللغة
   الرفض **حالةٌ أولى لا خطأ يُخفى**: يسمّي البند، ويقول ما لم يقع، ويعطي
   الخطوة التالية. والعنوان الإنجليزي إلى جانب العربي لأن رسالة الخادم
   المنشورة تحملهما معاً (RFC 9457 + ADR-0021). */

/** خصائص لوحة الرفض. */
export interface RefusalProps {
  /** العنوان العربي — وهو السجلّ. */
  readonly title: string;
  /** العنوان الإنجليزي — وهو العرض. */
  readonly titleEn?: string;
  /** ما لم يقع، بجملةٍ واحدة. */
  readonly body: string;
  /** رمز الرفض المنشور، لاتينيٌّ معزول. */
  readonly code?: string;
  /** تسمية الرمز — «الرمز» لا اسم البند. */
  readonly codeLabel?: string;
  /** البند الذي سُمّي: حقل، حساب، بُعد. */
  readonly subject?: string;
  /** اسمُ البند بالكلمات. */
  readonly subjectLabel?: string;
  /** الخطوة التالية — بلا خطوةٍ تالية تصير اللوحة شكوى. */
  readonly next?: string;
  /** صنفُ حركةٍ من {@link MOTION} عند وقوع الرفض. */
  readonly moment?: string;
  /**
   * تفصيلُ الرفض حين يكون **بنوداً مُسمّاة** لا جملةً واحدة: رفضٌ يُعدِّد أربعة
   * بنودٍ معلَّقة يجب أن يُريها كلّها، لا أن يجمعها في سطر. ويُعرض بين الجسم
   * والرمز، فيبقى ترتيب القراءة: ماذا وقع ← ما البنود ← الرمز ← الخطوة التالية.
   */
  readonly children?: ReactNode;
  readonly testId?: string;
}

/**
 * لوحة رفضٍ ثنائية اللغة تسمّي البند وتعطي الخطوة التالية.
 * @param props العنوان والجسم والرمز والخطوة التالية.
 */
export function RefusalPanel(props: RefusalProps): ReactNode {
  return (
    <div
      className={"problem " + (props.moment ?? "")}
      role="alert"
      data-testid={props.testId}
    >
      <h2>{props.title}</h2>
      {props.titleEn ? (
        <span className="en" lang="en" dir="ltr">
          {props.titleEn}
        </span>
      ) : null}
      <p>{props.body}</p>
      {props.children}
      {props.code || props.subject ? (
        <dl>
          {props.code ? (
            <>
              <dt>{props.codeLabel}</dt>
              <dd className="mono">{props.code}</dd>
            </>
          ) : null}
          {props.subject ? (
            <>
              <dt>{props.subjectLabel}</dt>
              <dd>{props.subject}</dd>
            </>
          ) : null}
        </dl>
      ) : null}
      {props.next ? <p className="muted">{props.next}</p> : null}
    </div>
  );
}

/* ══════════════════════════════════════════════════ ٦ · شريط التقدّم */

/**
 * شريط تقدّم. القيمة نسبةٌ **نصّاً** (٠–١٠٠) فلا تمرّ بعائم.
 * @param props القيمة والتسمية.
 */
export function ProgressBar(props: {
  readonly percent: string;
  readonly label: string;
  /** غير محدّد المدى: يعمل ولا يُعرف متى ينتهي. */
  readonly indeterminate?: boolean;
  readonly tone?: "brand" | "good" | "warn";
  readonly testId?: string;
}): ReactNode {
  const tone = props.tone ?? "brand";
  return (
    <div
      className="confidence confidence--progress"
      data-band={tone === "good" ? "high" : tone === "warn" ? "medium" : undefined}
      style={{ color: tone === "brand" ? "var(--color-primary)" : undefined }}
      data-testid={props.testId}
    >
      <div
        className={"confidence__rail" + (props.indeterminate ? " cine-live" : "")}
        role="progressbar"
        aria-label={props.label}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuetext={props.indeterminate ? undefined : props.percent}
      >
        <span
          className="confidence__fill"
          style={{ "--confidence": props.indeterminate ? "100" : props.percent } as CSSProperties}
        />
      </div>
    </div>
  );
}

/* ═════════════════════════════ ٦٫٥ · النسبة التعاقدية — نصٌّ لا رقم
   **لماذا أوّليّة لا `<Amount>`:** المال مقياسه المعروض منزلتان، والنسبة
   التعاقدية (Rate) مقياسها ثمانٍ — فعرضُها بمُنسِّق المال يُسقط أربع خانات
   صامتاً. وهي تمرّ بـ`<Decimal>`: أرقامُ اللغة، والمقياس **مقروءٌ من النصّ
   الواصل** لا مفترَض، ولا عائم في أي خطوة.

   **والكمّية أختُها، وهي في §٩ أدناه** لأنها بُنيت مرّتين على التوازي
   ووُحِّدت هناك بخاصّةٍ مُسمّاة — والقصّة كاملةً في صدر ذلك القسم. */

/**
 * نسبة تعاقدية **كسراً عشرياً لا نسبة مئوية** — كما ينصّ العقد المنشور:
 * عشرة بالمئة تُكتب `0.10` لا `10`.
 * <p>
 * ولا علامة `%` هنا ولا ضربٌ في مئة: الضرب حسابٌ على قيمةٍ مالية الأثر،
 * وعلامةٌ على كسرٍ تجعل «0.10» تُقرأ عُشر بالمئة. فالقيمة تُعرض كما وصلت،
 * وتسميتُها من الشاشة تقول إنها كسر.
 * </p>
 * @param props النسبة نصّاً.
 */
export function RateValue(props: {
  /** النسبة كما وصلت نصّاً — Rate، ولا تصير رقماً. */
  readonly rate: string;
  readonly className?: string;
  readonly testId?: string;
}): ReactNode {
  return (
    <span className={"rate " + (props.className ?? "")} data-testid={props.testId} data-rate={props.rate}>
      <Decimal value={props.rate} />
    </span>
  );
}

/* ═══════════════════════════════════════════ ٧ · حالة فراغٍ ذات معنى
   «لا نتائج» ليست حالة فراغ: حالةُ الفراغ تقول **لماذا** الجدول فارغ وما
   الخطوة التالية. وفي هذا المنتج للفراغ معنىً خاصّ: جداول الإعدادات النظامية
   **تُسلَّم فارغة عمداً** — فالفراغ قرارٌ يُشرَح، لا نقصٌ يُعتذَر عنه. */

/**
 * حالة فراغٍ تقول لماذا، وتعطي الخطوة التالية.
 * @param props العنوان والسبب والفعل.
 */
export function EmptyState(props: {
  readonly title: string;
  readonly body: string;
  readonly action?: ReactNode;
  readonly small?: boolean;
  readonly testId?: string;
}): ReactNode {
  return (
    <div className={"empty" + (props.small ? " empty--sm" : "")} data-testid={props.testId}>
      <div className="ico" aria-hidden="true">{"\u2205"}</div>
      <h3>{props.title}</h3>
      <p>{props.body}</p>
      {props.action ? <div className="actions">{props.action}</div> : null}
    </div>
  );
}

/* ═════════════════════════════════════════════════════ ٨ · جرس التنبيه */

/**
 * جرسُ تنبيهٍ يحمل عدد ما لم يُقرأ. العدد يُعرض بأرقام اللغة.
 * @param props العدد والتسمية.
 */
export function AlertBell(props: {
  readonly count: number;
  readonly label: string;
  readonly onClick?: () => void;
  readonly testId?: string;
}): ReactNode {
  return (
    <button
      type="button"
      className="iconbtn bell"
      data-unread={props.count > 0 ? "true" : "false"}
      aria-label={props.label}
      onClick={props.onClick}
      data-testid={props.testId}
    >
      <span aria-hidden="true">{"⊙"}</span>
      {props.count > 0 ? (
        <span className="visually-hidden">
          <Num value={props.count} />
        </span>
      ) : null}
    </button>
  );
}

/* ═══════════════════════ ٩ · كمّية بوحدتها · a quantity with its unit
   **«عشرة» ليست معلومة.** عشر حبّات أم عشر كراتين؟ والعقد يعرف ذلك فلا يُمرِّر
   كمّيةً مجرّدة أبداً: كل كمّية `Measure` — مقدارٌ **نصّاً** ووحدةٌ معه. وهذه
   الأوّليّة هي مقابل `<Amount>` في جهة الكمّيات، ووُجدت لأن الطبقة كانت تحمل
   عرضاً للمال ولا تحمل عرضاً للكمّية، فكان كل قسمٍ سيخترع واحداً.

   ┌─ **ولماذا فيها `scale` وليست دالّةً واحدة** ────────────────────────────┐
   │ بُنيت هذه الأوّليّة **مرّتين على التوازي** — في القسم المخزني وفي قسم    │
   │ المقاولات — ولم يعرف أحدهما بالآخر. والاسمان والخصائص متطابقان،         │
   │ و**سياسة المقياس المعروض مختلفة، وكلتاهما مُبرَّرة بإثباتٍ قائم**:      │
   │                                                                        │
   │  · المخزون يقرأ «100.000000» ويعرض «100»: قصُّ الأصفار اللاحقة **لا     │
   │    يغيّر قيمة**، ورصيدٌ صحيح يُعرض بستّ أصفارٍ يُقرأ ضجيجاً.             │
   │    (`inventory.test.tsx`: «9007199254740993.500000» ⇒ «…993.5»)        │
   │  · المقاولات تعرض «120.000000» كما وصلت: عمودُ الكمّية التراكمية        │
   │    يُقارَن بعمود الكمّية السابقة صفّاً بصفّ، والمقياس الموحَّد هو ما     │
   │    يجعل المقارنة بالعين ممكنة. (`contracting.test.tsx`: `line-cumulative`│
   │    يحوي «120.000000» و`line-previous` يحوي «45.000000»)                 │
   │                                                                        │
   │ ولا دالّة واحدة تُخرج الجوابين من المُدخَل نفسه. فالاختلاف **يُصرَّح به  │
   │ خاصّةً مُسمّاة** بدل أن يُحسم بأخذ أحد الطرفين: أوّليّةٌ تعرض كمّيةً     │
   │ خطأً لقسمٍ أسوأ من تعارض دمج. و`"natural"` هو الافتراض لأنه ما يتوقّعه   │
   │ قارئُ رصيد، و`"wire"` يُطلب صراحةً حيث تُقارَن الأعمدة.                 │
   └────────────────────────────────────────────────────────────────────────┘

   وثلاثة قرارات تحكمها، وكلّها مقيسة لا مفترَضة:

   ١ · **المقدار لا يمرّ بـ`Number` ولا بـ`parseFloat`.** مقياسه ستٌّ لا أربع
       (لأنه يُضرب في تكلفة الوحدة)، و`Number` يفقده فوق ٢^٥٣ وفي الكسر معاً.
       فيُسلَّم نصّه إلى طبقة التدويل كما وصل، وتُعرَض قيمةُ عرضٍ لا نصّ.

   ٢ · **لا تقريب يقع أبداً.** القصّ على أصفارٍ لاحقة لا يغيّر قيمة، والنصّ
       الأصلي يبقى كاملاً في `title` كما يفعل `<Amount>` — فالعرض تقريبٌ
       مُعلَن لا قيمةٌ بديلة.

   ٣ · **رمز الوحدة معرّفٌ لا نصّ معروض** (العقد: «لا يُترجَم ولا يُطابَق بلا
       حساسية حالة»). فيُعرض **كما سجّله المستأجر** — بلا تحويلٍ إلى حروف
       كبيرة، لأن ذلك يغيّر معرّفاً — و**معزولاً اتجاهياً بلا اتجاه مفروض**:
       قد يكون «PCS» وقد يكون «حبة»، والصفحة قد تكون بأي من اللغات الأربع. */

/** سياسةُ المقياس المعروض — تُصرَّح ولا تُخمَّن. */
export type MagnitudeScalePolicy =
  /** خانات القيمة بعد قصّ أصفارها اللاحقة: «1.500000» ⇒ «1.5». */
  | "natural"
  /** خاناتُها كما وصلت على السلك: «1.500000» ⇒ «1.500000». */
  | "wire";

/**
 * يحسب مقياس العرض الطبيعي لمقدارٍ نصّي: خاناتُه العشرية بعد قصّ الأصفار
 * اللاحقة. **نصّيٌّ بالكامل** — لا `Number` ولا `parseFloat` في أي خطوة.
 * @param text المقدار كما وصل على السلك.
 */
export function magnitudeScale(text: string): number {
  const dot = text.indexOf(".");
  if (dot < 0) return 0;
  let end = text.length;
  while (end > dot + 1 && text.charAt(end - 1) === "0") end -= 1;
  return end - dot - 1;
}

/**
 * مقياسُ النصّ كما وصل: عدد خاناته بعد الفاصلة بلا قصّ.
 * @param text المقدار كما وصل على السلك.
 */
function wireScale(text: string): number {
  const dot = text.indexOf(".");
  return dot < 0 ? 0 : text.length - dot - 1;
}

/**
 * هل المقدار سالب؟ **نصّياً بلا حساب**، والصفر السالب ليس سالباً — كما في
 * `Money.isNegative` حرفاً بحرف، فلا قاعدتان لسؤالٍ واحد.
 * @param text المقدار كما وصل على السلك.
 */
export function magnitudeIsNegative(text: string): boolean {
  return text.charAt(0) === "-" && !/^-0(\.0+)?$/.test(text);
}

/** خصائص الكمّية. */
export interface QuantityValueProps {
  /** المقدار نصّاً كما وصل — `Magnitude` أو `Quantity` في العقد. */
  readonly magnitude: string;
  /** رمز الوحدة كما سجّله المستأجر. معرّفٌ لا يُترجَم. */
  readonly unit: string;
  /** سياسة المقياس المعروض. الافتراض `"natural"`. */
  readonly scale?: MagnitudeScalePolicy;
  readonly className?: string;
  readonly testId?: string;
}

/**
 * كمّيةٌ ووحدتها. المقدار يمرّ بطبقة التدويل، والوحدة تُعرض كما هي معزولة.
 * @param props المقدار والوحدة وسياسة المقياس.
 */
export function QuantityValue(props: QuantityValueProps): ReactNode {
  const { i18n, locale } = useLocale();
  const { magnitude, scale = "natural" } = props;
  const display = useMemo(() => {
    void locale;
    return i18n.amount(magnitude, {
      scale: scale === "wire" ? wireScale(magnitude) : magnitudeScale(magnitude),
    });
  }, [i18n, locale, magnitude, scale]);

  return (
    <span
      className={"qty " + (props.className ?? "")}
      data-negative={magnitudeIsNegative(magnitude) ? "true" : undefined}
      data-testid={props.testId}
    >
      <Rendered display={display} className="qty__n" title={magnitude} />
      <span className="qty__u mono">{props.unit}</span>
    </span>
  );
}
