/* ═══════════════════════════════════════════════════════════════════════════
   قطعٌ مشتركة بين شاشات التأسيس الأربع  ·  Pieces shared by the four setup screens
   ───────────────────────────────────────────────────────────────────────────
   وخمسةٌ منها تحمل قراراتٍ لا شكلاً:

   ١ · **{@link SetupField} يفرض وصفاً واحداً لكل حقل** — لا صفراً ولا اثنين
       (ADR-0078). والخطأ **يحلّ محلّ** الوصف ولا يُضاف إليه، فيبقى عدد خانات
       الحقل ثلاثاً سواءٌ ظهر الرفض أو لم يظهر. **والوعاء `.grid` المُسجَّل**
       في `styles/components.css` — «الصفُّ يملك المسارات» (ADR-0067) — فلا
       يخترع هذا القسم وعاءً ثانياً ولا سطرَ CSS واحداً لصفّ حقول.

   ٢ · **{@link TranslationComposer} يمنع الوسم العربي قبل الضغط.** العربية
       سجلٌّ لا ترجمة (ADR-0021)، والخادم يردّ `company_setup.arabic_is_not_a_translation`
       — فالشاشة تقوله **باسمه** قبل الإرسال بدل أن تُرسل ما تعرف أنه مرفوض.

   ٣ · **{@link RecordName} يُعلن الارتداد ولا يقع صامتاً.** اسمٌ بلا ترجمةٍ
       للغة العرض يُعرض بسجلّه العربي، ويُقال إنه ارتداد.

   ٤ · **{@link DeclaredGap} يقول ما لا يستطيعه العقد** أو ما لم يُحسم بعد،
       بدل أن يُخترَع أو يُسكَت عنه.

   ٥ · **ولا رقم حسابٍ مكتوبٍ في هذا الملفّ ولا في أيّ شاشةٍ تستعمله.** رموزُ
       الحسابات ورموزُ مراكز التكلفة تأتي من الخادم وتُعرض كما وصلت — ومصفوفة
       الترحيل في `data/posting-matrix/` هي التي تقرّر، لا الواجهة.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useState, type ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import type { NameValue } from "../../api/generated/types";
import { resolveTranslatedName, RECORD_TAG } from "../../app/translated-name";
import { useT } from "../../i18n/react";
import { Button, Field, Panel, StatusBadge, type DocState, type Provenance } from "../../ui";
import "./setup.css";

/* ═══════════════════════════ ١ · الملاحة داخل مجموعة التأسيس ══════════════
   أربعُ شاشاتٍ بترتيب العمل لا بترتيب الحروف: ما يقع مرّةً فيؤسّس المنشأة ←
   ما يُبوَّب عليه كلُّ سطرٍ بعده ← ما يُرخَّص من حقول المستندات ← ما يقبل
   السطر أصلاً. */

/** شاشات التأسيس الأربع بمساراتها — والترتيب هو ترتيب الشريط والملاحة. */
export const SETUP_SCREENS = [
  { to: "/setup", key: "app.nav.companySetup" },
  { to: "/setup/cost-centers", key: "app.nav.costCenters" },
  { to: "/setup/document-shapes", key: "app.nav.documentShapes" },
  { to: "/setup/chart-of-accounts", key: "app.nav.chartOfAccounts" },
] as const;

/**
 * شريط شاشات التأسيس.
 * @param props الشاشة الحالية بمسارها.
 */
export function SetupSectionNav(props: { readonly current: string }): ReactNode {
  const { t } = useT();
  return (
    <nav className="stp-tabs" aria-label={t("screen.setup.navLabel")} data-testid="setup-tabs">
      {SETUP_SCREENS.map((screen) => (
        <Link
          key={screen.to}
          to={screen.to}
          className="stp-tab"
          data-testid={"setup-tab-" + screen.to}
          aria-current={props.current === screen.to ? "page" : undefined}
        >
          {t(screen.key)}
        </Link>
      ))}
    </nav>
  );
}

/* ═══════════════════════════════════════════ ٢ · الحقل والصفّ المستوي */

/** خصائص حقل التأسيس. */
export interface SetupFieldProps {
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
export function SetupField(props: SetupFieldProps): ReactNode {
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

/* ═══════════════════════════════════════════ ٣ · لوحٌ بحالاته الأربع */

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
        <div className="stack" data-testid="setup-loading">
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

/* ═══════════════════════════════ ٤ · حين لا منشأة مختارة */

/** لا شاشة تأسيسٍ تعمل بلا منشأة — والطريق إليها لا حقلٌ يُكتب بيد. */
export function ChooseCompanyFirst(props: { readonly testId: string }): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid={props.testId}>
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("screen.setup.needCompany")}</h3>
      <p>{t("screen.setup.needCompanyBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="setup-go-sign-in">
          {t("screen.setup.goSignIn")}
        </Link>
      </div>
    </section>
  );
}

/* ═════════════════════ ٥ · الاسم كما يُعرض: سجلٌّ وترجمةٌ وارتدادٌ مُعلَن */

/**
 * اسمٌ من العقد: السجلّ العربي، ثم الترجمة إلى لغة العرض إن وُجدت.
 * **والارتداد يُعلَن** — لا يُعرض المفتاح ولا الفراغ (ADR-0021).
 * @param props السجلّ العربي وترجماته ولغة العرض.
 */
export function RecordName(props: {
  readonly nameAr: string;
  readonly translations: readonly NameValue[];
  readonly locale: string;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  const resolved = resolveTranslatedName(props.nameAr, props.translations, props.locale);
  return (
    <span data-testid={props.testId}>
      <span lang={RECORD_TAG} dir="rtl">{props.nameAr}</span>
      {resolved.fallback || props.locale === RECORD_TAG ? null : (
        <>
          {" "}
          <span className="alt" lang={resolved.tag} dir="auto">{resolved.text}</span>
        </>
      )}
      {resolved.fallback ? (
        <>
          {" "}
          <span className="hint">{t("screen.setup.nameFellBack")}</span>
        </>
      ) : null}
    </span>
  );
}

/* ═════════════════════════ ٦ · وسم اللغة — يُفحص قبل الضغط لا بعده */

/** أقصى طول وسم لغة كما تعلنه النواة. */
export const MAXIMUM_TAG_LENGTH = 16;

/** رمز الخادم لوسمٍ مُشوَّه. */
export const TAG_MALFORMED_CODE = "company_setup.language_tag_malformed";

/** رمز الخادم لوسمٍ عربي في خريطة الترجمات. */
export const ARABIC_NOT_TRANSLATION_CODE = "company_setup.arabic_is_not_a_translation";

/** رمز الخادم لترجمةٍ بلا نصّ. */
export const TRANSLATION_EMPTY_CODE = "company_setup.translation_empty";

/**
 * هل الوسم سليم الشكل؟ **والقاعدة نسخةٌ من حكم النواة لا اجتهادٌ ثانٍ**:
 * لاتينيٌّ أوّلُه حرف، ولا ينتهي بشَرطة، ولا يحمل شَرطتين متتاليتين.
 * @param tag الوسم كما كُتب.
 */
export function isWellFormedTag(tag: string): boolean {
  if (tag.length === 0 || tag.length > MAXIMUM_TAG_LENGTH) return false;
  return /^[A-Za-z][A-Za-z0-9-]*$/.test(tag) && !tag.endsWith("-") && !tag.includes("--");
}

/** هل الوسم هو وسم السجلّ نفسه (أو فرعٌ منه)؟ العربية سجلٌّ لا ترجمة. */
export function isRecordTag(tag: string): boolean {
  const lower = tag.toLowerCase();
  return lower === RECORD_TAG || lower.startsWith(RECORD_TAG + "-");
}

/** ما يمنع إضافة هذه الترجمة، أو `null` حين لا مانع — والرمز رمز الخادم. */
export function translationRefusal(
  tag: string,
  value: string,
  existing: readonly NameValue[]
): string | null {
  if (tag === "" || value === "") return null;
  if (!isWellFormedTag(tag)) return TAG_MALFORMED_CODE;
  if (isRecordTag(tag)) return ARABIC_NOT_TRANSLATION_CODE;
  if (value.trim() === "") return TRANSLATION_EMPTY_CODE;
  if (existing.some((entry) => entry.name === tag)) return "company_setup.language_tag_repeated";
  return null;
}

/**
 * مؤلِّفُ ترجمات: صفٌّ واحد (وسم · نصّ · زرّ) وقائمةٌ بما أُضيف.
 * <p>
 * **صفٌّ واحد لا صفٌّ لكل ترجمة**: الأوصاف تحت الحقول تتكرّر بعدد الصفوف
 * فتصير جدارَ نصّ، والصفُّ الواحد يُبقي وصفاً واحداً لكلّ حقل كما يوجب
 * ADR-0078 بلا تكرار.
 * </p>
 * @param props الترجمات الحالية وما يقع عند تغيّرها.
 */
export function TranslationComposer(props: {
  readonly idPrefix: string;
  readonly testId: string;
  readonly value: readonly NameValue[];
  readonly onChange: (next: readonly NameValue[]) => void;
}): ReactNode {
  const { t } = useT();
  const [tag, setTag] = useState("");
  const [text, setText] = useState("");
  const refusal = translationRefusal(tag, text, props.value);
  const ready = tag !== "" && text.trim() !== "" && refusal === null;

  return (
    <div className="stack" data-testid={props.testId}>
      <div className="grid fields-3">
        <SetupField
          id={props.idPrefix + "-tag"}
          label={t("screen.setup.tagLabel")}
          hint={t("screen.setup.tagHint")}
          {...(refusal ? { error: t("screen.setup.refusal." + refusalKey(refusal)) } : {})}
          source="typed"
        >
          <input
            id={props.idPrefix + "-tag"}
            className="ctl mono"
            dir="ltr"
            lang="en"
            autoComplete="off"
            spellCheck={false}
            aria-invalid={refusal !== null}
            data-testid={props.testId + "-tag"}
            value={tag}
            onChange={(e) => setTag(e.target.value)}
          />
        </SetupField>
        <SetupField
          id={props.idPrefix + "-text"}
          label={t("screen.setup.tagTextLabel")}
          hint={t("screen.setup.tagTextHint")}
          source="typed"
        >
          <input
            id={props.idPrefix + "-text"}
            className="ctl"
            dir="auto"
            autoComplete="off"
            data-testid={props.testId + "-text"}
            value={text}
            onChange={(e) => setText(e.target.value)}
          />
        </SetupField>
        <div className="rowctl">
          <Button
            label={t("screen.setup.tagAdd")}
            disabled={!ready}
            onClick={() => {
              props.onChange([...props.value, { name: tag, value: text.trim() }]);
              setTag("");
              setText("");
            }}
            testId={props.testId + "-add"}
          />
          <span className="hint">{t("screen.setup.tagAddHint")}</span>
        </div>
      </div>
      {props.value.length === 0 ? (
        <p className="hint" data-testid={props.testId + "-none"}>{t("screen.setup.tagNone")}</p>
      ) : (
        <ul className="stp-tags" data-testid={props.testId + "-list"}>
          {props.value.map((entry) => (
            <li key={entry.name}>
              <span className="mono" dir="ltr">{entry.name}</span>
              <span dir="auto">{entry.value}</span>
              <Button
                label={t("screen.setup.tagDrop")}
                kind="ghost"
                size="sm"
                onClick={() => props.onChange(props.value.filter((x) => x.name !== entry.name))}
                testId={props.testId + "-drop-" + entry.name}
              />
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** يحوّل رمز الرفض إلى مقطع المفتاح الأخير — والرمز هو نقطة الاعتماد. */
function refusalKey(code: string): string {
  if (code === TAG_MALFORMED_CODE) return "tagMalformed";
  if (code === ARABIC_NOT_TRANSLATION_CODE) return "arabicIsRecord";
  if (code === TRANSLATION_EMPTY_CODE) return "translationEmpty";
  return "tagRepeated";
}

/* ═════════════════════════ ٧ · شارةُ حالةٍ تُقرأ ولا تُخفى */

/**
 * شارةُ حالةٍ من مجموعةٍ مغلقة في العقد. **والحالة التي لا تعرفها الشاشة
 * تُعرض كما وصلت** — لا تُسقَط ولا تُسمّى باسم غيرها.
 * @param props الحالة كما وصلت، ونغمتها.
 */
export function SetupBadge(props: {
  readonly label: string;
  readonly tone: DocState;
  readonly title?: string;
  readonly testId?: string;
}): ReactNode {
  return (
    <StatusBadge
      state={props.tone}
      label={props.label}
      {...(props.title ? { title: props.title } : {})}
      {...(props.testId ? { testId: props.testId } : {})}
    />
  );
}

/* ═══════════════════════════════ ٨ · ما لا يستطيعه العقد — مُعلَناً */

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
    <section className="stp-gap" role="note" data-testid={props.testId}>
      <div className="stp-gap__head">
        <span className="pill pill--pending">{t("screen.setup.gapBadge")}</span>
        <strong>{props.title}</strong>
      </div>
      <p>{props.body}</p>
      <p className="hint">
        <span className="stp-gap__owed">{t("screen.setup.gapOwed")}</span> {props.owed}
      </p>
    </section>
  );
}
