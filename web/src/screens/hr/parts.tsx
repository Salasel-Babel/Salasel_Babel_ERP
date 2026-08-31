/* ═══════════════════════════════════════════════════════════════════════════
   قطعٌ مشتركة بين شاشات الموارد البشرية  ·  Pieces shared by the HR screens
   ───────────────────────────────────────────────────────────────────────────
   **وأهمّها لوحة الهوية المقنَّعة.** والقاعدة التي تحكمها ليست تفضيلاً في
   العرض بل بنيةٌ في النظام كلّه:

     · السطح المنشور **لا يُعيد رقم الهوية ولا الآيبان إطلاقاً** — يُعيد
       قناعيهما: آخر أربعة محارف، وما قبلها نجومٌ **بعدد ثابت** لا بعدد طول
       الأصل، لأن الطول نفسه يُميّز بلد إصدار الآيبان.
     · فليس في هذه الواجهة ما تكشفه: **لا باب كشفٍ منشور في العقد**، ولا
       حقلَ أصلٍ يصل إليها، ولا سبيل إلى إعادة تركيب المقنَّع من المقنَّع.
       و«اعرض غير مقنَّع» زرٌّ لا يستطيع أن يوجد هنا؛ ولو وُجد لوجب أن يكون
       فعلاً مُصرَّحاً به ومُدقَّقاً على الخادم، لا اختياراً في متصفّح.
     · **والرمز المعتم وحده هو ما يعبر إلى الدفتر**: لا هوية، ولا آيبان، ولا
       اسم — لأن حقول الدفتر داخل البايتات المُجزَّأة، وما دخلها لا يُمحى.

   ولذلك تحمل اللوحة **جملةً تقول ذلك للمستخدم**: الفراغُ بلا شرحٍ يُقرأ
   «البيانات ناقصة»، والقناعُ مشروحاً يُقرأ «النظام لا يُظهرها عمداً».
   ═══════════════════════════════════════════════════════════════════════════ */
import type { ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import type { HrMaskedIdentity, HrPayrollAmounts, NameValue } from "../../api/generated/types";
import { SCHEMA_Money_RE, SCHEMA_TaxRate_RE } from "../../api/generated/formats";
import { resolveTranslatedName } from "../../app/translated-name";
import { SOURCE } from "../../i18n/engine";
import { useLocale, useT } from "../../i18n/react";
import { Panel, StatCard, StatusBadge, type DocState } from "../../ui";
import { ACTIVE, DRAFT, KNOWN_STATES, POSTED, TERMINATED } from "./contract";

/* ═════════════════════════════════════════════════ ١ · حين لا منشأة مختارة */

/** لا شاشة في هذا القسم تعمل بلا منشأة — والطريق إلى اختيارها لا حقلٌ يُكتب بيد. */
export function ChooseCompanyFirst(props: { readonly testId: string }): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid={props.testId}>
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("hr.need.company")}</h3>
      <p>{t("hr.need.companyBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="hr-go-sign-in">
          {t("hr.need.signIn")}
        </Link>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════ ١٫٥ · الملاحة داخل القسم
   ملاحةُ الهيكل تحمل **الأقسام** لا شاشاتِ كلٍّ منها، ولوحةُ الأوامر تفتح على
   ما يُعرَف اسمه. وبينهما يبقى من دخل القسم ولا يعرف ماذا فيه — فشريطٌ داخل
   القسم يقول أربعَ شاشاته، والحالية موسومةٌ بـ`aria-current`. */

/** شاشات القسم الأربع بمساراتها. */
const HR_SCREENS = [
  { to: "/hr", key: "hr.nav.register" },
  { to: "/hr/payroll", key: "hr.nav.payroll" },
  { to: "/hr/payslip", key: "hr.nav.payslip" },
  { to: "/hr/end-of-service", key: "hr.nav.endOfService" },
] as const;

/**
 * شريط شاشات القسم.
 * @param props الشاشة الحالية بمسارها.
 */
export function HrSectionNav(props: { readonly current: string }): ReactNode {
  const { t } = useT();
  return (
    <nav className="hr-tabs" aria-label={t("hr.nav.label")} data-testid="hr-tabs">
      {HR_SCREENS.map((screen) => (
        <Link
          key={screen.to}
          to={screen.to}
          className="hr-tab"
          data-testid={"hr-tab-" + screen.to}
          aria-current={props.current === screen.to ? "page" : undefined}
        >
          {t(screen.key)}
        </Link>
      ))}
    </nav>
  );
}

/* ══════════════════════════════════════════════════ ٢ · الهوية المقنَّعة */

/**
 * لوحة الهوية — **مقنَّعةً دائماً، ولا زرّ كشفٍ فيها**.
 * @param props القناعان كما وصلا من السطح.
 */
export function MaskedIdentityPanel(props: {
  readonly identity: HrMaskedIdentity;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  return (
    <div className="hr-mask" data-testid={props.testId ?? "hr-masked-identity"}>
      <div className="hr-mask__head">
        <span className="prov" data-source="attested" aria-hidden="true" />
        <strong>{t("hr.mask.title")}</strong>
        <span className="pill pill--archived" data-testid="hr-mask-no-reveal">
          {t("hr.mask.noReveal")}
        </span>
      </div>
      <div className="kv">
        <div>
          <div className="k">{t("hr.mask.nationalId")}</div>
          <div className="v mono hr-mask__value" dir="ltr" data-testid="hr-mask-national-id">
            {props.identity.nationalIdMask}
          </div>
        </div>
        <div>
          <div className="k">{t("hr.mask.iban")}</div>
          <div className="v mono hr-mask__value" dir="ltr" data-testid="hr-mask-iban">
            {props.identity.ibanMask}
          </div>
        </div>
      </div>
      <p className="hint">{t("hr.mask.rule")}</p>
      <p className="hint">{t("hr.mask.ledger")}</p>
    </div>
  );
}

/* ═══════════════════════════════════════════════════ ٣ · الرمز المعتم */

/** الرمز المعتم — لاتينيٌّ معزول، ومعه ما يقوله لمن لا يعرفه. */
export function OpaqueCode(props: { readonly code: string; readonly testId?: string }): ReactNode {
  const { t } = useT();
  return (
    <span
      className="mono hr-code"
      dir="ltr"
      title={t("hr.code.hint")}
      data-testid={props.testId ?? "hr-opaque-code"}
    >
      {props.code}
    </span>
  );
}

/* ══════════════════════════════════════════════════════ ٤ · حالة المستند */

/** يحوّل حالةَ الوحدة إلى حالةِ شارةٍ من المجموعة المعتمدة في طبقة التصميم. */
function toneOf(state: string): DocState {
  if (state === POSTED) return "posted";
  if (state === DRAFT) return "draft";
  if (state === ACTIVE) return "pending";
  if (state === TERMINATED) return "archived";
  return "info";
}

/**
 * شارة حالة. والحالة التي لا تعرفها الشاشة **تُعرض كما وصلت** — لا تُسقَط
 * ولا تُسمّى باسم غيرها.
 * @param props الحالة كما وصلت من السطح.
 */
export function HrState(props: { readonly state: string; readonly testId?: string }): ReactNode {
  const { t } = useT();
  const known = KNOWN_STATES.includes(props.state);
  return (
    <StatusBadge
      state={toneOf(props.state)}
      label={known ? t("hr.state." + props.state) : props.state}
      title={known ? undefined : t("hr.state.unknown")}
      testId={props.testId}
    />
  );
}

/* ═════════════════════════════════════════════════ ٥ · معرّف القيد */

/**
 * معرّف قيد المستند. و**الفراغ يُقرأ «لم يُرحَّل بعد» لا «لا يُرحَّل»** —
 * ولذلك يُكتب نصّاً لا شَرطةً صامتة.
 * @param props المعرّف أو غيابه.
 */
export function EntryRef(props: { readonly entryId: string | null; readonly testId?: string }): ReactNode {
  const { t } = useT();
  if (!props.entryId) {
    return (
      <span className="muted" data-testid={props.testId}>
        {t("hr.entry.notYet")}
      </span>
    );
  }
  return (
    <span className="mono" dir="ltr" data-testid={props.testId}>
      {props.entryId}
    </span>
  );
}

/* ═══════════════════════════════════════════ ٦ · المبالغ الستّة */

/**
 * المبالغ الستّة **بأسماء مفردات مصفوفة الترحيل نفسها** — فما يُقرأ على
 * الشاشة هو ما يُرحَّل، لا اسمٌ ثانٍ له.
 * @param props المبالغ وهل وصلت للتوّ.
 */
export function AmountsRow(props: {
  readonly amounts: HrPayrollAmounts;
  readonly moment?: string;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  const { amounts } = props;
  return (
    <div className="stats-row" data-testid={props.testId}>
      <StatCard
        label={t("hr.amount.gross")}
        amount={amounts.grossEntitlements}
        tone="debit"
        moment={props.moment}
        testId="hr-amount-gross"
      />
      <StatCard label={t("hr.amount.employerSi")} amount={amounts.employerSocialInsurance} tone="debit" />
      <StatCard label={t("hr.amount.employeeSi")} amount={amounts.employeeSocialInsurance} tone="credit" />
      <StatCard label={t("hr.amount.advance")} amount={amounts.advanceInstalment} tone="credit" />
      <StatCard label={t("hr.amount.deductions")} amount={amounts.deductions} tone="credit" />
      <StatCard
        label={t("hr.amount.net")}
        amount={amounts.netPayable}
        tone="good"
        hint={t("hr.amount.netHint")}
        moment={props.moment}
        testId="hr-amount-net"
      />
    </div>
  );
}

/* ═════════════════════════════════════════════ ٧ · الاسم بلغة الواجهة */

/**
 * اسمٌ بسجلّه العربي وترجمة **لغة الواجهة** — لا الإنجليزية دائماً
 * (ADR-0021). والارتداد إلى السجلّ لا إلى الفراغ ولا إلى لغةٍ ثالثة.
 * @param props السجلّ وترجماته.
 */
export function TranslatedName(props: {
  readonly nameAr: string;
  readonly translations: readonly NameValue[];
  readonly testId?: string;
}): ReactNode {
  const { locale, i18n } = useLocale();
  const resolved = resolveTranslatedName(props.nameAr, props.translations, locale);
  /* اتجاه الترجمة من فهرس اللغات نفسه — لغة خامسة تأخذ اتجاهها منه بلا سطر هنا. */
  const dir = i18n.catalogue.find((entry) => entry.code === resolved.tag)?.dir ?? "ltr";
  return (
    <span className="hr-name" data-testid={props.testId}>
      <span lang="ar" dir="rtl">
        {props.nameAr}
      </span>
      {locale !== SOURCE && !resolved.fallback ? (
        <span className="alt" lang={resolved.tag} dir={dir}>
          {resolved.text}
        </span>
      ) : null}
    </span>
  );
}

/* ═══════════════════════════════════════ ٨ · مدقّقات الشكل المنشور */

/** هل النصّ مبلغٌ بالنحو **المنشور**؟ ولا نمط مكتوب هنا. */
export function isMoneyText(text: string): boolean {
  return SCHEMA_Money_RE.test(text);
}

/** هل النصّ نسبةً بالنحو المنشور؟ */
export function isRateText(text: string): boolean {
  return SCHEMA_TaxRate_RE.test(text);
}

/** اليوم بصيغة yyyy-MM-dd ميلادية — من حقل التاريخ لا من تنسيق ثقافة. */
export function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/* ═════════════════════════════════════ ٩ · ما لم يُنشَر بابه — مُعلَناً */

/**
 * **بابٌ غير موجود في العقد، مُعلَناً لا مسكوتاً عنه.** والفرق ليس أدباً:
 * شاشةٌ تخفي ما لا تستطيع تُقرأ نظاماً أصغر مما هو، وشاشةٌ **تخترع** ما لا
 * تستطيع تكذب على من يبني عليها قراراً.
 * @param props العنوان والسبب والقرار المطلوب من المالك.
 */
export function DeclaredGap(props: {
  readonly title: string;
  readonly body: string;
  readonly owed: string;
  readonly testId: string;
}): ReactNode {
  const { t } = useT();
  return (
    <section className="hr-gap" role="note" data-testid={props.testId}>
      <div className="hr-gap__head">
        <span className="pill pill--pending">{t("hr.gap.badge")}</span>
        <strong>{props.title}</strong>
      </div>
      <p>{props.body}</p>
      <p className="hint">
        <span className="hr-gap__owed">{t("hr.gap.owed")}</span>
        {" "}
        {props.owed}
      </p>
    </section>
  );
}

/* ═══════════════════════════════════════ ١٠ · لوحٌ بحالاته الأربع */

/**
 * لوحٌ يعرض واحدةً من الحالات الأربع، فلا تكتب كل شاشةٍ سلّمها الخاصّ.
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
    <Panel title={props.title} note={props.note} aside={props.aside} testId={props.testId}>
      {props.loading ? (
        <div className="stack" data-testid="hr-loading">
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
