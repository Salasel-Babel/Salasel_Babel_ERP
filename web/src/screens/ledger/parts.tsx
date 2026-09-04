/* ═══════════════════════════════════════════════════════════════════════════
   ما بعد الترحيل — قطعٌ مشتركة  ·  After posting — the shared pieces
   ───────────────────────────────────────────────────────────────────────────
   أربع شاشاتٍ تجيب سؤالاً واحداً بأربع أيدٍ: **ما رُحّل خطأً، كيف يُصحَّح،
   وكيف نُثبت أنه لم يُعدَّل؟** وثلاثةٌ تحكم هذا الملفّ:

   ١ · **لا لغةَ بصريةٍ ثانية.** الصفّ والحقل واللوح وإيصال الترحيل وشارة
       الحالة كلُّها من `screens/accounting/parts.tsx` و`accounting.css`
       كما هي — تُستورَد ولا تُنسَخ ولا تُعدَّل. وما يضيفه هذا الملفّ
       شيئان لا ثالث لهما: شريطُ المجموعة، ولوحُ الفعل الذي لا رجعة فيه.

   ٢ · **الفعلُ الذي لا رجعة فيه يقول أثره قبل الضغط.** {@link Irrevocable}
       يعرض الأثر نصّاً، ثم يطلب إقراراً بخانةٍ نصُّها **هو الأثر نفسه** لا
       «هل أنت متأكّد؟»، ثم يفتح زرّه. وهو الشكل نفسه الذي أقرّه ADR-0075
       لأفعال الإدارة — ومنقولٌ إلى هذا القسم بمفاتيحه لا بمفاتيح ذاك.

   ٣ · **لا حساب على المال ولا على الكمّيات هنا** — ولا في القسم كلّه.
       و`periodOf` أدناه **ليس حساباً**: هو اقتطاعُ `yyyy-MM` من نصّ تاريخٍ
       ميلاديٍّ نحوُه منشور، وحكمُ الفترة النهائي يعود على إيصال الترحيل.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useState, type ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import type { PostingReceipt as Receipt } from "../../api/generated/types";
import { Num, useT } from "../../i18n/react";
import { Button } from "../../ui";
import "../accounting/accounting.css";

/* ═════════════════════════════════════════ ١ · الشريط داخل المجموعة
   **ترتيب العمل لا ترتيب الحروف**: ما يُصحَّح بقيدٍ مضادّ على الدفتر نفسه ←
   ثم ما يُصحَّح بمستندٍ تجاري تجاه المورّد ← ثم تجاه العميل ← ثم الحكمُ على
   سلامة ما بقي. والترتيب هنا هو ترتيبه في `SCREENS` وفي الملاحة اليدوية. */

/** الشاشات الأربع بمساراتها — والقائمة واحدة في ثلاثة مواضع. */
export const LEDGER_SCREENS = [
  { to: "/ledger/entry", key: "accounting.ledger.nav.entry" },
  { to: "/ledger/purchase-return", key: "accounting.ledger.nav.purchaseReturn" },
  { to: "/ledger/credit-note", key: "accounting.ledger.nav.creditNote" },
  { to: "/ledger/chain", key: "accounting.ledger.nav.chain" },
] as const;

/**
 * شريط شاشات المجموعة، والحالية موسومةٌ بـ`aria-current`.
 * @param props مسار الشاشة الحالية.
 */
export function LedgerSectionNav(props: { readonly current: string }): ReactNode {
  const { t } = useT();
  return (
    <nav
      className="acc-tabs"
      aria-label={t("accounting.ledger.nav.group")}
      data-testid="ledger-nav"
    >
      {LEDGER_SCREENS.map((screen) => (
        <Link
          key={screen.to}
          to={screen.to}
          className="acc-tab"
          data-testid={"ledger-tab-" + screen.to}
          aria-current={props.current === screen.to ? "page" : undefined}
        >
          {t(screen.key)}
        </Link>
      ))}
    </nav>
  );
}

/* ═══════════════════════════ ٢ · الفعل الذي لا رجعة فيه */

/** خصائص لوح الفعل الذي لا رجعة فيه. */
export interface IrrevocableProps {
  /** عنوان الفعل. */
  readonly title: string;
  /** **الأثر نصّاً**: ما الذي سيقع، على ماذا، وأين. يُقرأ قبل الضغط. */
  readonly effect: string;
  /** نصّ الإقرار — وهو الأثر نفسه بصيغة المتكلّم، لا «هل أنت متأكّد؟». */
  readonly acknowledge: string;
  /** تسمية الزرّ. */
  readonly label: string;
  /** ما يُعرض بين الأثر والإقرار: جدولُ ما سيُكتب، وحقولُ الطلب. */
  readonly children?: ReactNode;
  /** سببُ تعذّرٍ في المُدخَل — لا سببُ صلاحية، فالصلاحية للخادم. */
  readonly blocked?: string;
  readonly busy?: boolean;
  readonly onConfirm: () => void;
  readonly testId: string;
}

/**
 * لوحُ فعلٍ لا رجعة فيه: يقول أثره، ثم يطلب إقراراً، ثم يفتح زرّه.
 * <p>
 * والزرّ **مُقفلٌ قبل الإقرار لأن المُدخَل ناقص**، لا لأن الشاشة تمنع فعلاً
 * يسمح به الخادم: الصلاحية حكمُ الخادم، وهذه خانةُ قراءةٍ لا حارسُ إذن.
 * </p>
 * @param props الأثر والإقرار والفعل.
 */
export function Irrevocable(props: IrrevocableProps): ReactNode {
  const { t } = useT();
  const [acked, setAcked] = useState(false);
  const id = props.testId + "-ack";
  return (
    <div className="alert alert--warning" data-testid={props.testId}>
      <div className="body">
        <span className="title">{props.title}</span>
        <p data-testid={props.testId + "-effect"}>{props.effect}</p>
        {props.children}
        <label className="check" htmlFor={id}>
          <input
            id={id}
            type="checkbox"
            checked={acked}
            data-testid={id}
            onChange={(e) => setAcked(e.target.checked)}
          />
          <span>{props.acknowledge}</span>
        </label>
        {props.blocked ? (
          <p className="hint" data-testid={props.testId + "-blocked"}>
            {props.blocked}
          </p>
        ) : null}
        <div className="actions">
          <Button
            label={props.label}
            kind="danger"
            loading={props.busy === true}
            disabled={!acked || props.busy === true || !!props.blocked}
            onClick={props.onConfirm}
            testId={props.testId + "-go"}
          />
        </div>
        <p className="hint">
          {acked ? t("accounting.ledger.act.ackDone") : t("accounting.ledger.act.ackFirst")}
        </p>
      </div>
    </div>
  );
}

/* ═══════════════════════════ ٣ · إيصال قيدٍ كُتب في الدفتر */

/**
 * إيصالُ قيدٍ كُتب — لا مستندٍ رُحّل. و`alreadyPosted` هنا يعني أن هذه
 * الهوية كانت مكتوبةً **قبل** هذا الطلب، فيُقال ذلك ويُعاد الإيصال نفسه؛
 * ولا قيدَ ثانٍ يُكتب، ولا يُعدّ ذلك خطأً.
 * @param props الإيصال العائد.
 */
export function EntryReceipt(props: {
  readonly receipt: Receipt;
  readonly testId: string;
}): ReactNode {
  const { t } = useT();
  const again = props.receipt.alreadyPosted;
  const r = props.receipt;
  return (
    <div
      className={"acc-receipt" + (again ? " acc-receipt--again" : "")}
      data-already-posted={again ? "true" : "false"}
      data-testid={props.testId}
    >
      <div className="acc-receipt__head">
        <strong>
          {again
            ? t("accounting.ledger.receipt.againTitle")
            : t("accounting.ledger.receipt.doneTitle")}
        </strong>
      </div>
      <p className="muted">
        {again
          ? t("accounting.ledger.receipt.againBody")
          : t("accounting.ledger.receipt.doneBody")}
      </p>
      <div className="kv">
        <div>
          <div className="k">{t("accounting.ledger.field.newEntryId")}</div>
          <div className="v mono acc-id" data-testid={props.testId + "-entry"}>{r.entryId}</div>
        </div>
        <div>
          <div className="k">{t("accounting.ledger.field.entryNumber")}</div>
          <div className="v acc-id"><Num value={r.entryNumber} /></div>
        </div>
        <div>
          <div className="k">{t("accounting.ledger.field.periodCode")}</div>
          <div className="v mono acc-id" data-testid={props.testId + "-period"}>{r.periodCode}</div>
        </div>
        <div>
          <div className="k">{t("accounting.ledger.field.chainSequence")}</div>
          <div className="v acc-id"><Num value={r.chainSequence} /></div>
        </div>
        <div>
          <div className="k">{t("accounting.ledger.field.lineCount")}</div>
          <div className="v acc-id"><Num value={r.lineCount} /></div>
        </div>
        <div>
          <div className="k">{t("accounting.ledger.field.generation")}</div>
          <div className="v acc-id"><Num value={r.generation} /></div>
        </div>
        <div>
          <div className="k">{t("accounting.ledger.field.entryHash")}</div>
          <div className="v mono acc-id">{r.entryHash}</div>
        </div>
      </div>
    </div>
  );
}

/* ═══════════════════════════ ٤ · الفترة التي يقع فيها تاريخ */

/**
 * الفترة التي يقع فيها تاريخٌ ميلادي — **اقتطاعٌ لا حساب**.
 * <p>
 * نحو التاريخ منشور `yyyy-MM-dd`، ونحو الفترة `yyyy-MM`؛ فالأوّل يبدأ
 * بالثاني حرفاً بحرف. وما يعود على إيصال الترحيل هو الحكم النهائي، وهذا
 * إخبارٌ عن **التاريخ** لا وعدٌ عن الفترة.
 * </p>
 * @param dateIso التاريخ بصيغته المنشورة، أو نصّ فارغ.
 */
export function periodOf(dateIso: string): string {
  return /^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$/.test(dateIso)
    ? dateIso.slice(0, 7)
    : "";
}

/** هل النصّ تاريخٌ بالنحو المنشور؟ (ميلادي `yyyy-MM-dd` بأرقام لاتينية). */
export function isDateText(text: string): boolean {
  return /^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$/.test(text);
}
