/* ═══════════════════════════════════════════════════════════════════════════
   قطعٌ مشتركة بين شاشات الإدارة الأربع  ·  Pieces shared by the four admin screens
   ───────────────────────────────────────────────────────────────────────────
   وأربعةٌ منها تحمل قراراتٍ لا شكلاً:

   ١ · **{@link AdminField} يفرض وصفاً واحداً لكل حقل** — لا صفراً ولا اثنين
       (ADR-0078). والخطأ **يحلّ محلّ** الوصف ولا يُضاف إليه، فيبقى عدد
       خانات الحقل ثلاثاً سواءٌ ظهر الرفض أو لم يظهر.

   ٢ · **{@link Irreversible} يقول أثر الفعل قبل الضغط، ويطلب إقراراً.**
       فعلٌ لا رجعة فيه خلف زرٍّ واحد ليس ميزة: من ضغطه لا يعرف أنه ضغط.
       واللوح يسمّي **من يخرج** و**ماذا يتوقّف** — لا «هل أنت متأكّد؟».

   ٣ · **{@link ReadOnlyNotice} يُعلن الدور ولا يُعطّل زرّاً.** الإخفاء ليس
       منعاً: المنع في الخادم، وجلسةُ Reader تُردّ على كل فعلٍ غير آمن بـ
       `membership.read_only`. فالشاشة **تقول ذلك قبل الضغط** ثم **تُظهر
       الرفض باسمه** بعده — ولا تدّعي أنها هي التي منعت.

   ٤ · **{@link DeclaredGap} يقول ما لا يستطيعه العقد** بدل أن يُخترَع أو
       يُسكَت عنه.

   **ولا اعتماد يُكتب ولا يُعرض في هذا الملفّ ولا في أي شاشةٍ تستعمله.**
   الاعتمادات في هذا المجال ثلاثة (انتساب · فاعل · تجديد)، وكلٌّ منها يخرج
   من الخادم **مرّة واحدة**؛ فما وصل يُمرَّر إلى بابه أو يُنسخ إلى الحافظة،
   ولا يُرسَم في DOM ولا يُكتب في رابط ولا يُسجَّل.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useState, type ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { useT } from "../../i18n/react";
import { Button, Field, Panel, StatusBadge, type DocState, type Provenance } from "../../ui";
import "./admin.css";

/* ═══════════════════════════════ ١ · الملاحة داخل مجموعة الإدارة ══════════
   أربعُ شاشاتٍ بترتيب العمل لا بترتيب الحروف: كيف أدخل أوّل مرّة ← ما الذي
   بيدي الآن ← من يدخل معي ← ماذا اشتريتُ وما الذي يعمل. */

/** شاشات الإدارة الأربع بمساراتها — والترتيب هو ترتيب الشريط والملاحة. */
export const ADMIN_SCREENS = [
  { to: "/admin/enrolment", key: "app.nav.enrolment" },
  { to: "/admin/session", key: "app.nav.mySession" },
  { to: "/admin/members", key: "app.nav.members" },
  { to: "/admin/subscription", key: "app.nav.subscription" },
] as const;

/**
 * شريط شاشات الإدارة.
 * @param props الشاشة الحالية بمسارها.
 */
export function AdminSectionNav(props: { readonly current: string }): ReactNode {
  const { t } = useT();
  return (
    <nav className="adm-tabs" aria-label={t("screen.admin.navLabel")} data-testid="admin-tabs">
      {ADMIN_SCREENS.map((screen) => (
        <Link
          key={screen.to}
          to={screen.to}
          className="adm-tab"
          data-testid={"admin-tab-" + screen.to}
          aria-current={props.current === screen.to ? "page" : undefined}
        >
          {t(screen.key)}
        </Link>
      ))}
    </nav>
  );
}

/* ═══════════════════════════════════════════ ٢ · الحقل والصفّ المستوي
   والوعاء `.grid` مُسجَّلٌ في `styles/components.css` — «الصفُّ يملك المسارات»
   (ADR-0067) — فلا يخترع هذا القسم وعاءً ثانياً ولا سطرَ CSS واحداً. */

/** خصائص حقل الإدارة. */
export interface AdminFieldProps {
  readonly id: string;
  readonly label: string;
  /** **إلزامي** — وصفٌ واحد لكل حقل، وبه يستوي قاعُ الحبر (ADR-0078). */
  readonly hint: string;
  /** رسالةُ رفضٍ على الحقل — **تحلّ محلّ الوصف** ولا تُضاف إليه. */
  readonly error?: string;
  readonly required?: boolean;
  readonly source?: Provenance;
  readonly children: ReactNode;
}

/**
 * حقلٌ في صفٍّ مستوٍ: تسمية · تحكّم · وصفٌ واحد.
 * @param props المعرّف والتسمية والوصف والمحتوى.
 */
export function AdminField(props: AdminFieldProps): ReactNode {
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

/* ═══════════════════════════════════════ ٣ · الدور، وما يعنيه عملياً */

/** الأدوار الثلاثة كما ينشرها العقد — مجموعةٌ مغلقة تُقرأ ولا تُخترع. */
export const ROLES = ["Reader", "Contributor", "Owner"] as const;

/** الدور الذي لا يكتب — والرمز الذي يردّ به الخادم كتابته. */
export const READER: (typeof ROLES)[number] = "Reader";

/** رمز رفض الخادم لجلسة قارئ على فعلٍ غير آمن. */
export const READ_ONLY_CODE = "membership.read_only";

/** رمز رفض الخادم لاستحقاقٍ منقطع — ويفترق عن الأول عمداً. */
export const ENTITLEMENT_READ_ONLY_CODE = "entitlement.read_only";

/**
 * شارة دور. والدور الذي لا تعرفه الشاشة يُعرض كما وصل ولا يُسمّى باسم غيره.
 * @param props الدور كما وصل من السطح.
 */
export function RoleBadge(props: { readonly role: string; readonly testId?: string }): ReactNode {
  const { t } = useT();
  const known = (ROLES as readonly string[]).includes(props.role);
  const tone: DocState =
    props.role === "Owner" ? "posted" : props.role === READER ? "info" : "pending";
  return (
    <StatusBadge
      state={tone}
      label={known ? t("screen.admin.role." + props.role) : props.role}
      title={known ? undefined : t("screen.admin.roleUnknown")}
      testId={props.testId}
    />
  );
}

/**
 * **إعلانُ دورٍ لا يكتب — ولا زرَّ يُعطَّل به.**
 * <p>
 * الإخفاء ليس منعاً. فالحدّ في الخادم، وهذه اللوحة تقول ذلك **قبل** الضغط
 * كي لا يُقاد القارئ إلى فعلٍ يُردّ؛ والرفض بعد الضغط يُعرض **باسمه** في
 * لوحة الرفض نفسها.
 * </p>
 * @param props الدور المقروء، وهل عُرف أصلاً.
 */
export function ReadOnlyNotice(props: { readonly role: string; readonly testId: string }): ReactNode {
  const { t } = useT();
  if (props.role !== READER) return null;
  return (
    <div className="alert alert--info" role="status" data-testid={props.testId} data-role={props.role}>
      <div className="body">
        <span className="title">{t("screen.admin.readOnlyTitle")}</span>
        <p>
          {t("screen.admin.readOnlyBody")}{" "}
          <span className="mono" dir="ltr">{READ_ONLY_CODE}</span>
        </p>
      </div>
    </div>
  );
}

/* ═══════════════════════════ ٤ · فعلٌ لا رجعة فيه — أثرُه قبل الضغط */

/** خصائص لوح الفعل الذي لا رجعة فيه. */
export interface IrreversibleProps {
  /** عنوان الفعل. */
  readonly title: string;
  /** ما يقع بالضبط — بجملةٍ تسمّي من يخرج أو ما يتوقّف. */
  readonly effect: string;
  /** بنودُ الأثر واحداً واحداً — تُعرض قائمةً لا جملةً واحدة. */
  readonly children?: ReactNode;
  /** نصّ الإقرار الذي يُؤشَّر قبل أن يُفتح الزرّ. */
  readonly acknowledge: string;
  /** تسمية الزرّ. */
  readonly action: string;
  readonly onConfirm: () => void;
  readonly busy?: boolean;
  /** سببُ تعذّرٍ **تقنيّ** — لا سببُ صلاحية. */
  readonly blocked?: string;
  readonly testId: string;
}

/**
 * لوحُ فعلٍ لا رجعة فيه: يقول أثره، ثم يطلب إقراراً، ثم يفتح زرّه.
 * <p>
 * **والإقرار ليس «هل أنت متأكّد؟»**: هو خانةٌ نصُّها هو الأثر نفسه، فمن
 * أشّرها قرأ ما يقع. والزرّ مُقفلٌ قبلها لأن **المُدخَل ناقص**، لا لأن
 * الشاشة تمنع فعلاً يسمح به الخادم.
 * </p>
 * @param props الأثر والإقرار والفعل.
 */
export function Irreversible(props: IrreversibleProps): ReactNode {
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
            label={props.action}
            kind="danger"
            loading={props.busy}
            disabled={!acked || props.busy === true || props.blocked !== undefined}
            onClick={props.onConfirm}
            testId={props.testId + "-go"}
          />
        </div>
        <p className="hint">{acked ? t("screen.admin.ackDone") : t("screen.admin.ackFirst")}</p>
      </div>
    </div>
  );
}

/* ═══════════════════════════════ ٥ · ما لا يستطيعه العقد — مُعلَناً */

/**
 * **بابٌ غير موجود، أو قرارٌ غير محسوم — مُعلَناً لا مسكوتاً عنه.**
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
    <section className="adm-gap" role="note" data-testid={props.testId}>
      <div className="adm-gap__head">
        <span className="pill pill--pending">{t("screen.admin.gapBadge")}</span>
        <strong>{props.title}</strong>
      </div>
      <p>{props.body}</p>
      <p className="hint">
        <span className="adm-gap__owed">{t("screen.admin.gapOwed")}</span> {props.owed}
      </p>
    </section>
  );
}

/* ═══════════════════════════════════════════ ٦ · حين لا منشأة مختارة */

/** لا شاشة عضويّاتٍ تعمل بلا منشأة — والطريق إليها لا حقلٌ يُكتب بيد. */
export function ChooseCompanyFirst(props: { readonly testId: string }): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid={props.testId}>
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("screen.admin.needCompany")}</h3>
      <p>{t("screen.admin.needCompanyBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="admin-go-sign-in">
          {t("screen.admin.goSignIn")}
        </Link>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════════ ٧ · لوحٌ بحالاته الأربع */

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
        <div className="stack" data-testid="admin-loading">
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

/* ═══════════════════════════════════ ٨ · لحظةٌ بصيغة ISO كما وصلت */

/**
 * لحظةٌ من السطح. **تُعرض كما وصلت، معزولةً اتجاهياً** — ولا تُعاد صياغتها
 * بتقويمٍ ثانٍ: العقد ينصّ أنها ISO 8601 دوّارة بتوقيت UTC وبأرقام لاتينية،
 * وتحويلُها هنا يجعل لحظةً تُكتب بشكلٍ وتُقرأ بآخر.
 * @param props اللحظة كما وصلت.
 */
export function Instant(props: { readonly value: string; readonly testId?: string }): ReactNode {
  return (
    <span className="mono" dir="ltr" data-testid={props.testId}>
      {props.value}
    </span>
  );
}
