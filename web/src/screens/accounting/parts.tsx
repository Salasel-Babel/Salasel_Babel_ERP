/* ═══════════════════════════════════════════════════════════════════════════
   قطعٌ مشتركة بين شاشات دورة المستندات  ·  Pieces shared by the document screens
   ───────────────────────────────────────────────────────────────────────────
   وثلاثةٌ منها تحمل قراراتٍ لا شكلاً:

   ١ · **{@link AccField} يفرض وصفاً واحداً لكل حقل** — لا صفراً ولا اثنين.
       وهذا ليس تفضيلاً في الصياغة بل شرطُ استقامة الصفّ: «قاعُ الحبر» الذي
       تراه العين هو أسفلُ آخر ما يُرى في الحقل، فحقلٌ بلا وصفٍ ينتهي حبره
       عند قاع عنصر تحكّمه بينما ينتهي حبرُ جاره تحت وصفه — وينكسر الصفّ من
       طرفٍ لم يُنظر إليه. و**الخطأ يحلّ محلّ الوصف ولا يُضاف إليه**، فيبقى
       العدد واحداً في الحالين. (والتفصيل في صدر `accounting.css`.)

   ٢ · **{@link PostingReceipt} يفصل الترحيل الأول عن الثاني.** هوية الإحكام
       تضمن أن نداءً مكرَّراً يُعيد **الإيصال نفسه** و`alreadyPosted = true`،
       وذلك **ليس خطأً**: فيُقال نصّاً «رُحِّل من قبل، وهذا إيصاله» بلوحٍ
       مُميَّز — لا برسالة نجاحٍ ثانية تُقرأ «رُحِّل مرّتين»، ولا برفضٍ يُخيف.

   ٣ · **{@link EntryRef} لا يكتب شَرطةً صامتة.** مسوّدةٌ بلا قيدٍ حالةٌ
       معروفة تُقال بالكلمات: «لم يُرحَّل بعد» — والشَرطة تُقرأ عطلاً.

   ولا رقمَ حسابٍ في هذا الملفّ: ما يُعرض من الحساب يأتي من إيصال الترحيل
   العائد، ولا تسمّيه الشاشة.
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import {
  SCHEMA_Money_RE,
  SCHEMA_Quantity_RE,
  SCHEMA_TaxRate_RE,
} from "../../api/generated/formats";
import type { CommercialDocument } from "../../api/generated/types";
import { useT } from "../../i18n/react";
import { Button, Panel, StatCard, StatusBadge, type DocState, type Provenance } from "../../ui";
import { Field } from "../../ui";
import { APPROVED, CANCELLED, DRAFT, KNOWN_STATES, POSTED, REVERSED } from "./contract";

/* ═════════════════════════════════════════════════ ١ · حين لا منشأة مختارة */

/** لا شاشة في هذا القسم تعمل بلا منشأة — والطريق إليها لا حقلٌ يُكتب بيد. */
export function ChooseCompanyFirst(props: { readonly testId: string }): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid={props.testId}>
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("accounting.need.company")}</h3>
      <p>{t("accounting.need.companyBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="acc-go-sign-in">
          {t("accounting.need.signIn")}
        </Link>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════ ٢ · الملاحة داخل القسم
   الدورة **مجموعتان**: ما يخرج إلى العميل، وما يدخل من المورّد. وشريطٌ واحد
   بسبع شاشاتٍ متجاورة يُخفي أن الأربع الأخيرة سلسلةٌ مرتَّبة: أمرٌ ← استلام
   ← فاتورة ← صرف. فالمجموعتان مُسمّاتان، والحالية موسومةٌ بـ`aria-current`. */

/** شاشات المبيعات الثلاث بمساراتها. */
export const SALES_SCREENS = [
  { to: "/sales/invoice", key: "accounting.nav.salesInvoice" },
  { to: "/sales/receipt", key: "accounting.nav.customerReceipt" },
  { to: "/sales/receivables", key: "accounting.nav.receivables" },
] as const;

/** شاشات المشتريات الأربع، **بترتيب الدورة** لا بترتيب الحروف. */
export const PURCHASING_SCREENS = [
  { to: "/purchasing/order", key: "accounting.nav.purchaseOrder" },
  { to: "/purchasing/goods-receipt", key: "accounting.nav.goodsReceipt" },
  { to: "/purchasing/bill", key: "accounting.nav.supplierBill" },
  { to: "/purchasing/payment", key: "accounting.nav.supplierPayment" },
] as const;

/**
 * شريط شاشات المجموعة.
 * @param props المجموعة والشاشة الحالية بمسارها.
 */
export function AccSectionNav(props: {
  readonly group: "sales" | "purchasing";
  readonly current: string;
}): ReactNode {
  const { t } = useT();
  const screens = props.group === "sales" ? SALES_SCREENS : PURCHASING_SCREENS;
  return (
    <nav
      className="acc-tabs"
      aria-label={t("accounting.nav." + props.group)}
      data-testid={"acc-tabs-" + props.group}
    >
      {screens.map((screen) => (
        <Link
          key={screen.to}
          to={screen.to}
          className="acc-tab"
          data-testid={"acc-tab-" + screen.to}
          aria-current={props.current === screen.to ? "page" : undefined}
        >
          {t(screen.key)}
        </Link>
      ))}
    </nav>
  );
}

/* ═══════════════════════════════════ ٣ · الصفّ المستوي وحقلُه */

/** خصائص الصفّ. */
export interface AccRowProps {
  /** عدد الخانات على الشاشة الواسعة — وينهار إلى واحدة على الهاتف. */
  readonly cols: 2 | 3 | 4;
  readonly children: ReactNode;
  readonly testId?: string;
}

/**
 * صفٌّ تستوي فيه **الحقول أنفسها** لا قِمَمُ صناديقها، ويستوي قاعُ حبرها.
 * والآليّة في `accounting.css` — ثلاثةُ مساراتٍ يستعيرها كل حقل.
 * @param props عدد الخانات والحقول.
 */
export function AccRow(props: AccRowProps): ReactNode {
  return (
    <div className={"acc-row acc-row--" + props.cols} data-testid={props.testId}>
      {props.children}
    </div>
  );
}

/** خصائص الحقل. */
export interface AccFieldProps {
  readonly id: string;
  readonly label: string;
  /** **إلزامي** — وصفٌ واحد لكل حقل، وبه يستوي قاعُ الحبر. */
  readonly hint: string;
  /** رسالةُ رفضٍ على الحقل — **تحلّ محلّ الوصف** ولا تُضاف إليه. */
  readonly error?: string;
  readonly required?: boolean;
  readonly source?: Provenance;
  readonly children: ReactNode;
}

/**
 * حقلٌ في صفٍّ مستوٍ: تسمية، ثم عنصر تحكّم، ثم **وصفٌ واحد** — لا صفر ولا
 * اثنان. وهو يلفّ {@link Field} ولا يستبدله: التسمية والتلميح ورسالة الرفض
 * وأثرُ المصدر كلّها من طبقة التصميم كما هي.
 * @param props المعرّف والتسمية والوصف والمحتوى.
 */
export function AccField(props: AccFieldProps): ReactNode {
  return (
    <Field
      id={props.id}
      label={props.label}
      {...(props.error ? { error: props.error } : { hint: props.hint })}
      {...(props.required ? { required: true } : {})}
      {...(props.source ? { source: props.source } : {})}
    >
      {props.children}
    </Field>
  );
}

/**
 * خليّةُ فعلٍ داخل صفّ حقول — الزرّ يقف على خطّ عناصر التحكّم.
 * @param props الزرّ.
 */
export function AccAction(props: { readonly children: ReactNode }): ReactNode {
  return <div className="acc-act">{props.children}</div>;
}

/* ══════════════════════════════════════════════════════ ٤ · حالة المستند */

/** يحوّل حالةَ الوحدة إلى حالةِ شارةٍ من المجموعة المعتمدة في طبقة التصميم. */
function toneOf(state: string): DocState {
  if (state === POSTED) return "posted";
  if (state === DRAFT) return "draft";
  if (state === APPROVED) return "pending";
  if (state === REVERSED) return "reversed";
  if (state === CANCELLED) return "archived";
  return "info";
}

/**
 * شارة حالة. والحالة التي لا تعرفها الشاشة **تُعرض كما وصلت** — لا تُسقَط
 * ولا تُسمّى باسم غيرها.
 * @param props الحالة كما وصلت من السطح.
 */
export function AccState(props: { readonly state: string; readonly testId?: string }): ReactNode {
  const { t } = useT();
  const known = KNOWN_STATES.includes(props.state);
  return (
    <StatusBadge
      state={toneOf(props.state)}
      label={known ? t("accounting.state." + props.state) : props.state}
      title={known ? undefined : t("accounting.state.unknown")}
      testId={props.testId}
    />
  );
}

/* ═════════════════════════════════════════════════ ٥ · معرّف القيد */

/**
 * معرّف قيد المستند. و**الفراغ يُقرأ «لم يُرحَّل بعد»** — ولذلك يُكتب نصّاً
 * لا شَرطةً صامتة.
 * @param props المعرّف أو غيابه.
 */
export function EntryRef(props: {
  readonly entryId: string | null;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  if (!props.entryId) {
    return (
      <span className="muted" data-testid={props.testId}>
        {t("accounting.entry.notYet")}
      </span>
    );
  }
  return (
    <span className="mono acc-id" data-testid={props.testId}>
      {props.entryId}
    </span>
  );
}

/* ═════════════════════════════════════════ ٦ · مجاميع المستند الثلاثة */

/**
 * الصافي والضريبة والإجمالي — **تحسبها الوحدة ولا ترسلها الشاشة**، فهي
 * تُقرأ من الجواب ولا تُجمع هنا. والجمع على المال قرارٌ محاسبي يقع في الخادم.
 * @param props المستند وهل وصل للتوّ.
 */
export function DocumentTotals(props: {
  readonly document: CommercialDocument;
  readonly moment?: string;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  const { document: doc } = props;
  return (
    <div className="acc-stats acc-stats--3" data-testid={props.testId ?? "acc-totals"}>
      <StatCard
        label={t("accounting.total.net")}
        amount={doc.net}
        hint={t("accounting.total.netHint")}
        {...(props.moment ? { moment: props.moment } : {})}
        testId="acc-total-net"
      />
      <StatCard
        label={t("accounting.total.tax")}
        amount={doc.tax}
        hint={t("accounting.total.taxHint")}
        testId="acc-total-tax"
      />
      <StatCard
        label={t("accounting.total.gross")}
        amount={doc.gross}
        hint={t("accounting.total.grossHint")}
        tone="good"
        {...(props.moment ? { moment: props.moment } : {})}
        testId="acc-total-gross"
      />
    </div>
  );
}

/* ═══════════════════════════════ ٧ · إيصال الترحيل — والثاني منه */

/**
 * إيصالُ ترحيل. و**الترحيل الثاني يقول الحقيقة**: `alreadyPosted` يعني أن
 * هذه الهوية كانت مُرحَّلةً قبل هذا الطلب، فيُقال ذلك ويُعرض الإيصال نفسه —
 * ولا يُعدّ خطأً ولا نجاحاً ثانياً.
 * @param props المستند بعد نداء الترحيل.
 */
export function PostingReceipt(props: {
  readonly document: CommercialDocument;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  const { document: doc } = props;
  const again = doc.alreadyPosted;
  return (
    <div
      className={"acc-receipt" + (again ? " acc-receipt--again" : "")}
      data-already-posted={again ? "true" : "false"}
      data-testid={props.testId ?? "acc-posting-receipt"}
    >
      <div className="acc-receipt__head">
        <strong>{again ? t("accounting.post.againTitle") : t("accounting.post.doneTitle")}</strong>
        <AccState state={doc.state} testId="acc-receipt-state" />
      </div>
      <p className="muted">{again ? t("accounting.post.againBody") : t("accounting.post.doneBody")}</p>
      <div className="kv">
        <div>
          <div className="k">{t("accounting.field.number")}</div>
          <div className="v mono acc-id" data-testid="acc-receipt-number">{doc.number}</div>
        </div>
        <div>
          <div className="k">{t("accounting.field.entryId")}</div>
          <div className="v"><EntryRef entryId={doc.entryId} testId="acc-receipt-entry" /></div>
        </div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════ ٨ · لوحٌ بحالاته الأربع */

/**
 * لوحٌ يعرض واحدةً من الحالات، فلا تكتب كل شاشةٍ سلّمها الخاصّ.
 * @param props العنوان والحالة والمحتوى.
 */
export function StatePanel(props: {
  readonly title: string;
  readonly note?: string;
  readonly aside?: ReactNode;
  readonly loading?: boolean;
  readonly children: ReactNode;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  return (
    <Panel
      title={props.title}
      {...(props.note ? { note: props.note } : {})}
      {...(props.aside ? { aside: props.aside } : {})}
      {...(props.testId ? { testId: props.testId } : {})}
    >
      {props.loading ? (
        <div className="stack" data-testid="acc-loading">
          <span className="skeleton-row cine-live" />
          <span className="skeleton-row cine-live" />
          <span className="skeleton-row cine-live" />
          <p className="muted">{t("common.state.loadingBody")}</p>
        </div>
      ) : (
        props.children
      )}
    </Panel>
  );
}

/* ═════════════════════════════════════ ٩ · ما لم يُنشَر بابه — مُعلَناً */

/**
 * **بابٌ غير موجود في العقد، مُعلَناً لا مسكوتاً عنه.** فشاشةٌ تخفي ما لا
 * تستطيع تُقرأ نظاماً أصغر مما هو، وشاشةٌ **تخترع** ما لا تستطيع تكذب على من
 * يبني عليها قراراً.
 * @param props العنوان والسبب والقرار المطلوب.
 */
export function DeclaredGap(props: {
  readonly title: string;
  readonly body: string;
  readonly owed: string;
  readonly testId: string;
}): ReactNode {
  const { t } = useT();
  return (
    <section className="acc-gap" role="note" data-testid={props.testId}>
      <div className="acc-gap__head">
        <span className="pill pill--pending">{t("accounting.gap.badge")}</span>
        <strong>{props.title}</strong>
      </div>
      <p>{props.body}</p>
      <p className="hint">
        <span className="acc-gap__owed">{t("accounting.gap.owed")}</span>{" "}
        {props.owed}
      </p>
    </section>
  );
}

/* ═══════════════════════════════════ ١٠ · مدقّقات الشكل المنشور
   **ولا نمط مكتوب هنا**: الأنماط مُولَّدة من العقد في `generated/formats.ts`،
   فتضييقُ نحوٍ في الخادم يصل إلى هذه الحقول بلا سطرٍ يُكتب. */

/** هل النصّ مبلغٌ بالنحو المنشور؟ (مقياسٌ ≤ ٤). */
export function isMoneyText(text: string): boolean {
  return SCHEMA_Money_RE.test(text);
}

/** هل النصّ كمّيةٌ بالنحو المنشور؟ (مقياسٌ ≤ ٤). */
export function isQuantityText(text: string): boolean {
  return SCHEMA_Quantity_RE.test(text);
}

/** هل النصّ نسبةً ضريبيةً بالنحو المنشور؟ **كسرٌ عشري لا نسبة مئوية**. */
export function isTaxRateText(text: string): boolean {
  return SCHEMA_TaxRate_RE.test(text);
}

/** اليوم بصيغة yyyy-MM-dd ميلادية — من حقل التاريخ لا من تنسيق ثقافة. */
export function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/* ═══════════════════════════════════════════ ١١ · زرّ حذف سطر */

/**
 * زرُّ إسقاط سطرٍ من مسوّدةٍ **لم تُرسَل بعد**. ولا علاقة له بالخادم.
 * @param props الفعل وتسميته.
 */
export function DropLineButton(props: {
  readonly onClick: () => void;
  readonly testId: string;
}): ReactNode {
  const { t } = useT();
  return (
    <Button
      label={t("accounting.act.dropLine")}
      kind="ghost"
      size="sm"
      onClick={props.onClick}
      testId={props.testId}
    />
  );
}
