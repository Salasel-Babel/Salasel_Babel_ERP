/* ═══════════════════════════════════════════════════════════════════════════
   شريط المدّة — مدىً زمنيّ ومقاطعه، والتداخل يُرى لا يُكدَّس
   The period band — a term, its spans, and overlap shown rather than stacked
   ───────────────────────────────────────────────────────────────────────────
   **لماذا أوّليّةٌ عامّة لا مكوّنٌ عقاري:** المدى الزمني ومقاطعه شكلٌ يتكرّر في
   أكثر من قسم — مدّة عقد إيجار وأقساطها، وفترات مستخلَصات مقاولة، ودورات
   رواتب. وما يجمعها القاعدة الواحدة التي يفرضها هذا الملفّ:

   ┌─ ثلاث قواعد لا تُخالَف ─────────────────────────────────────────────┐
   │ ١ · **التداخل حالةٌ تُعرَض لا صمتٌ يُكدَّس.** مقطعان يتقاطعان يُوضَعان  │
   │     على مسارين ويُوسَمان `conflict`؛ ولا يُرسَمان فوق بعضهما فيبدوان   │
   │     مقطعاً واحداً سليماً. والوحدة لا تُؤجَّر مرّتين في يوم واحد.        │
   │ ٢ · **الفجوة تُعرَض كذلك.** مدّةٌ لا يغطّيها قسط يومٌ بلا اعترافٍ       │
   │     ينسب إليه، فيُرى بياضها موسوماً لا يُترك ليُقرأ «لا شيء هنا».       │
   │ ٣ · **الحساب على الأيام لا على المال.** الموضع والعرض يُشتقّان من       │
   │     فارق التواريخ وحده — والمبلغ يبقى `Money` يُعرَض بطبقة التدويل      │
   │     ولا يدخل في أي قسمة. شريطٌ عرضُه بمقدار المبلغ يوجب تحويله إلى      │
   │     عائم، وذلك ممنوع في هذا المستودع.                                  │
   └────────────────────────────────────────────────────────────────────────┘

   **والاتجاه:** المواضع بخصائص منطقية (`inset-inline-start`)، فالشريط يقرأ من
   اليمين في العربية والأردية ومن اليسار في الإنجليزية والهندية بلا سطرٍ ثانٍ.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { CSSProperties, ReactNode } from "react";

/** يومٌ واحد بالملّي ثانية — ثابتُ التقويم الميلادي المستعمَل في العقد. */
const DAY_MS = 86_400_000;

/** نمط التاريخ المنشور: ميلاديٌّ yyyy-MM-dd بأرقام لاتينية. */
const ISO_DATE = /^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$/;

/**
 * يحوّل تاريخاً منشوراً إلى عدد أيامٍ منذ الحقبة. **يرمي** على أي صيغة أخرى:
 * تقويمٌ آخر يُقرأ فترة مالية مختلفة، وتمريره صامتاً يرسم شريطاً كاذباً.
 * @param date التاريخ بصيغة yyyy-MM-dd ميلادية.
 */
export function dayNumber(date: string): number {
  if (!ISO_DATE.test(date)) {
    throw new TypeError(
      "dayNumber: تاريخ لا يطابق الصيغة المنشورة yyyy-MM-dd — «" + date + "». " +
        "/ a date outside the published yyyy-MM-dd form."
    );
  }
  const year = Number(date.slice(0, 4));
  const month = Number(date.slice(5, 7));
  const day = Number(date.slice(8, 10));
  return Date.UTC(year, month - 1, day) / DAY_MS;
}

/** حالةُ مقطعٍ على الشريط — قائمةٌ مغلقة، ولكلٍّ لونه ومعناه. */
export type SpanState = "plain" | "done" | "conflict";

/** مقطعٌ على الشريط: مدىً مُغلَق الطرفين، كما ينشره العقد. */
export interface BandSpan {
  readonly key: string;
  /** بداية المدى — داخلة. */
  readonly from: string;
  /** نهاية المدى — داخلة. */
  readonly to: string;
  /** ما يُكتب داخل المقطع — عقدةٌ تبنيها الشاشة، فالرقم يمرّ بطبقة التدويل. */
  readonly label: ReactNode;
  /** وصفٌ يُقرأ بالتحويم وبقارئ الشاشة. */
  readonly title: string;
  readonly state?: SpanState;
}

/** فجوةٌ في التغطية: أيامٌ من المدّة لا يغطّيها مقطع. */
export interface BandGap {
  readonly from: string;
  readonly to: string;
}

/** يعيد يوماً بصيغة yyyy-MM-dd من عدد أيامٍ منذ الحقبة. */
function isoOf(day: number): string {
  const at = new Date(day * DAY_MS);
  const pad = (n: number) => String(n).padStart(2, "0");
  return (
    String(at.getUTCFullYear()) + "-" + pad(at.getUTCMonth() + 1) + "-" + pad(at.getUTCDate())
  );
}

/**
 * مفاتيح المقاطع التي **يتقاطع** مداها مع مقطعٍ آخر. والمدى مُغلَق الطرفين،
 * فيومٌ واحد مشترك تداخلٌ لا ملامسة.
 * @param spans المقاطع كما وصلت.
 */
export function overlappingSpans(spans: readonly BandSpan[]): readonly string[] {
  const clashing = new Set<string>();
  for (let i = 0; i < spans.length; i++) {
    for (let j = i + 1; j < spans.length; j++) {
      const a = spans[i] as BandSpan;
      const b = spans[j] as BandSpan;
      if (dayNumber(a.from) <= dayNumber(b.to) && dayNumber(b.from) <= dayNumber(a.to)) {
        clashing.add(a.key);
        clashing.add(b.key);
      }
    }
  }
  return [...clashing];
}

/**
 * أيام المدّة التي لا يغطّيها أي مقطع، مُجمَّعةً في فجوات متّصلة.
 * @param from بداية المدّة — داخلة.
 * @param to نهاية المدّة — داخلة.
 * @param spans المقاطع.
 */
export function uncoveredGaps(
  from: string,
  to: string,
  spans: readonly BandSpan[]
): readonly BandGap[] {
  const start = dayNumber(from);
  const end = dayNumber(to);
  if (end < start) return [];
  const ordered = [...spans]
    .map((s) => ({ a: dayNumber(s.from), b: dayNumber(s.to) }))
    .sort((x, y) => x.a - y.a);
  const gaps: BandGap[] = [];
  let cursor = start;
  for (const span of ordered) {
    if (span.a > cursor) gaps.push({ from: isoOf(cursor), to: isoOf(Math.min(span.a - 1, end)) });
    cursor = Math.max(cursor, span.b + 1);
    if (cursor > end) break;
  }
  if (cursor <= end) gaps.push({ from: isoOf(cursor), to: isoOf(end) });
  return gaps;
}

/**
 * يوزّع المقاطع على مسارات فلا يُرسَم متداخلان فوق بعضهما. المسار الأول
 * يأخذ ما لا يتقاطع، والثاني ما تقاطع معه، وهكذا — فالتداخل يُرى بارتفاعه.
 * @param spans المقاطع.
 */
function lanesOf(spans: readonly BandSpan[]): readonly (readonly BandSpan[])[] {
  const ordered = [...spans].sort((a, b) => dayNumber(a.from) - dayNumber(b.from));
  const lanes: BandSpan[][] = [];
  for (const span of ordered) {
    const lane = lanes.find(
      (row) => (row[row.length - 1] as BandSpan) && dayNumber((row[row.length - 1] as BandSpan).to) < dayNumber(span.from)
    );
    if (lane) lane.push(span);
    else lanes.push([span]);
  }
  return lanes;
}

/** تسميات الشريط — كلّها مترجَمة تأتي من الشاشة. */
export interface BandLabels {
  /** وصف الشريط لقارئ الشاشة. */
  readonly caption: string;
  /** اسم الفجوة، يُقرأ بالتحويم. */
  readonly gap: string;
}

/** خصائص شريط المدّة. */
export interface PeriodBandProps {
  /** بداية المدّة — داخلة. */
  readonly from: string;
  /** نهاية المدّة — داخلة. */
  readonly to: string;
  readonly spans: readonly BandSpan[];
  readonly labels: BandLabels;
  /** مفاتيح المقاطع المُبرَزة الآن — اختيارٌ في الشاشة. */
  readonly selected?: readonly string[];
  readonly testId?: string;
}

/** موضعُ مدىً داخل المدّة، أسلوباً منطقيّ الاتجاه. */
function placement(start: number, total: number, a: number, b: number): CSSProperties {
  const offset = ((a - start) / total) * 100;
  const width = ((b - a + 1) / total) * 100;
  return {
    insetInlineStart: offset.toFixed(4) + "%",
    inlineSize: Math.max(width, 0.4).toFixed(4) + "%",
  };
}

/**
 * شريط مدّةٍ بمقاطعه: التداخل على مسارٍ ثانٍ وموسومٌ، والفجوة مُعلَّمة.
 * @param props المدّة والمقاطع والتسميات.
 */
export function PeriodBand(props: PeriodBandProps): ReactNode {
  const start = dayNumber(props.from);
  const end = dayNumber(props.to);
  const total = Math.max(end - start + 1, 1);
  const clashing = new Set(overlappingSpans(props.spans));
  const gaps = uncoveredGaps(props.from, props.to, props.spans);
  const lanes = lanesOf(props.spans);
  const selected = new Set(props.selected ?? []);

  return (
    <div className="band" data-testid={props.testId} role="group" aria-label={props.labels.caption}>
      <div className="band__gaps" aria-hidden="true">
        {gaps.map((gap) => (
          <span
            key={gap.from}
            className="band__gap"
            data-testid="band-gap"
            title={props.labels.gap}
            style={placement(start, total, dayNumber(gap.from), dayNumber(gap.to))}
          />
        ))}
      </div>
      {lanes.map((lane, index) => (
        <div className="band__lane" key={index}>
          {lane.map((span) => {
            const state = clashing.has(span.key) ? "conflict" : (span.state ?? "plain");
            return (
              <span
                key={span.key}
                className="band__span"
                data-state={state}
                data-selected={selected.has(span.key) ? "true" : undefined}
                data-testid={state === "conflict" ? "band-conflict" : "band-span"}
                title={span.title}
                style={placement(start, total, dayNumber(span.from), dayNumber(span.to))}
              >
                <span className="band__text">{span.label}</span>
              </span>
            );
          })}
        </div>
      ))}
    </div>
  );
}
