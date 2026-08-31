/* ═══════════════════════════════════════════════════════════════════════════
   الجدول المالي — الأوّليّة التي يقضي المحاسب عمره فيها
   ───────────────────────────────────────────────────────────────────────────
   خمس قواعد لا يُبنى جدولٌ ماليٌّ في هذا النظام بدونها:
     ١ · أعمدة الأرقام بخطٍّ أحادي المسافة و`tabular-nums`، فتقع الفاصلة
         العشرية تحت أختها في اللغات الأربع.
     ٢ · كل خانة رقمية صندوق `ltr` **معزول** ومحاذاةٌ إلى النهاية — وإلا
         انهار العمود في الإنجليزية والهندية.
     ٣ · المدين والدائن **لونان يحملان معنى**، لا زخرفة.
     ٤ · رأسٌ لاصق ومجاميع لاصقة: من ينزل مئة صفّ يبقى يعرف أي عمودٍ يقرأ.
     ٥ · المبلغ يمرّ بـ`<Amount>` — نصٌّ منسَّق، ولا تحويل إلى عائم أبداً.

   والحالات أربع، وكلّها **حالاتٌ أولى**: عادي · محمَّل · فارغ · مرفوض. ولا
   يُعرَض جدولٌ فارغ بلا سبب: الفراغ في هذا المنتج قرارٌ يُشرَح (جداول
   الإعدادات النظامية تُسلَّم فارغة عمداً).
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import type { Money } from "../api/money";
import { Amount } from "../i18n/react";
import { MOTION } from "./motion";

/** صفٌّ في جدولٍ ماليّ. */
export interface LedgerRow {
  readonly id: string;
  /** رمز الحساب — لاتينيٌّ معزول. */
  readonly code: string;
  /** الاسم العربي — وهو السجلّ. */
  readonly name: string;
  /** الاسم المرافق بلغة الواجهة — وهو عرضٌ لا سجلّ (ADR-0021). */
  readonly alt?: string;
  readonly debit: Money;
  readonly credit: Money;
  /** وصل من الخادم في هذه الدورة — يُشعل مفردة `arrive`. */
  readonly arrived?: boolean;
  /** قيمةُ هذا الصفّ **مُستنتَجة** لا مُدخَلة — تُوسَم بصرياً. */
  readonly inferred?: boolean;
}

/** تسميات أعمدة الجدول ومجاميعه — كلّها مترجَمة تأتي من الشاشة. */
export interface LedgerLabels {
  readonly caption: string;
  readonly code: string;
  readonly account: string;
  readonly debit: string;
  readonly credit: string;
  readonly total: string;
}

/** خصائص الجدول المالي. */
export interface LedgerTableProps {
  readonly rows: readonly LedgerRow[];
  readonly labels: LedgerLabels;
  readonly totalDebit?: Money;
  readonly totalCredit?: Money;
  /** الحالة المعروضة. `ready` تعرض الصفوف. */
  readonly state?: "ready" | "loading" | "empty" | "refused";
  /** ما يُعرض بدل الصفوف في الحالات الثلاث الأخرى. */
  readonly placeholder?: ReactNode;
  readonly testId?: string;
}

/**
 * جدولٌ ماليّ برأسٍ لاصق ومجاميع لاصقة وحالاته الأربع.
 * @param props الصفوف والتسميات والحالة.
 */
export function LedgerTable(props: LedgerTableProps): ReactNode {
  const state = props.state ?? "ready";

  if (state !== "ready") {
    return (
      <div className="ledger" data-state={state} data-testid={props.testId}>
        {props.placeholder}
      </div>
    );
  }

  return (
    <div className="ledger" data-state="ready" data-testid={props.testId}>
      <table>
        <caption className="visually-hidden">{props.labels.caption}</caption>
        <thead>
          <tr>
            <th scope="col">{props.labels.code}</th>
            <th scope="col">{props.labels.account}</th>
            <th scope="col" className="n h-debit">
              {props.labels.debit}
            </th>
            <th scope="col" className="n h-credit">
              {props.labels.credit}
            </th>
          </tr>
        </thead>
        <tbody>
          {props.rows.map((row) => (
            <tr
              key={row.id}
              className={row.arrived ? MOTION.arrive : undefined}
              data-inferred={row.inferred ? "true" : undefined}
            >
              <td className="code">{row.code}</td>
              <td className={row.inferred ? "inferred-cell" : undefined}>
                {row.name}
                {row.alt ? <span className="alt">{row.alt}</span> : null}
              </td>
              <td className="n">
                <Amount value={row.debit} className="amt--debit" />
              </td>
              <td className="n">
                <Amount value={row.credit} className="amt--credit" />
              </td>
            </tr>
          ))}
        </tbody>
        {props.totalDebit && props.totalCredit ? (
          <tfoot>
            <tr>
              <td colSpan={2}>{props.labels.total}</td>
              <td className="n d">
                <Amount value={props.totalDebit} className="amt--total" />
              </td>
              <td className="n c">
                <Amount value={props.totalCredit} className="amt--total" />
              </td>
            </tr>
          </tfoot>
        ) : null}
      </table>
    </div>
  );
}
