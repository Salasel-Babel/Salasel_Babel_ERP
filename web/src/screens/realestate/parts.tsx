/* ═══════════════════════════════════════════════════════════════════════════
   قسم العقارات — ما تشترك فيه شاشاته الثلاث
   The real-estate section — what its three screens share
   ───────────────────────────────────────────────────────────────────────────
   **خمس قواعد يفرضها هذا الملفّ على القسم كلّه:**

   ١ · **المجموعات المغلقة تُقرأ من العقد وقت التشغيل** لا تُكتب هنا: نموذج
       الملكية، والاستعمال، والمعاملة الضريبية، والإقامة الضريبية. قائمةٌ
       مكتوبة بيد تنحرف عند أول إضافة فتُرسل عضواً لا يعرفه الخادم — وتحت كل
       مجموعة حارسُ تسمية يكسر الإقلاع **بصوت عالٍ** إن دخل عضوٌ بلا اسم.

   ٢ · **المال نصّ في الاتجاهين.** الأجرة والقسط والمقبوض ونسبة الضريبة كلّها
       تُكتب في حقل نصّي، وتُفحص بالنحو **المنشور** لا بنمطٍ مكتوب هنا، ثم
       تصير `Money` عند الإرسال. ولا عملية حسابية واحدة عليها في هذا القسم:
       مجموع الأقساط مقابل قيمة العقد **يحكم فيه الخادم** ويردّ برمز
       `realestate.instalments_do_not_sum_to_the_contract`؛ وإجراؤه هنا يُعيد
       الفخّ من بابه الثاني — ويزيد عليه أن سياسة التقريب قرار مالك مفتوح.

   ٣ · **لا رمز حساب في هذا القسم إطلاقاً.** الأجرة والقبض يبلغان الدفتر عبر
       مصفوفة الترحيل، ورمز الحدث الذي تعرضه الشاشة **يصل من الخادم** في جسم
       المستند (`eventCode`) ولا يُخترَع ولا يُختار.

   ٤ · **الرفض حالةٌ أولى مقيمة.** كل رفضٍ يُعرَض بلوحته، ويبقى حتى يتغيّر
       المُدخَل — ولا يمرّ في نخبٍ يختفي. والتصرّف على **الرمز الثابت** لا على
       نصّ الرسالة.

   ٥ · **العقد لا ينشر باباً يسرد.** لا سرد عقارات ولا وحدات ولا عقود: كل
       قراءة بمعرّف. فما تعرضه الشاشة من قوائم هو **ما سُجِّل في هذه الجلسة**
       وحده، مقولاً صراحةً — وقائمةٌ تُسمّى «السجلّ» وهي ذاكرةُ تبويبةٍ تكذب.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { Link } from "@tanstack/react-router";
import { SCHEMAS } from "../../api/generated/runtime-schema";
import { SCHEMA_Money_RE } from "../../api/generated/formats";
import { ProblemError } from "../../api/transport";
import type { NameValue } from "../../api/generated/types";
import { useLocale, useT } from "../../i18n/react";
import { resolveTranslatedName } from "../../app/translated-name";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { EmptyState, MOTION, useMoment } from "../../ui";
import "./realestate.css";

/* ═══════════════════════════════════ ١ · المجموعات المغلقة من العقد ═══ */

/**
 * أعضاء مجموعة مغلقة كما ينشرها العقد لحقلٍ بعينه.
 * @param schema اسم المخطّط.
 * @param field اسم الحقل.
 */
export function closedSet(schema: string, field: string): readonly string[] {
  const shape = SCHEMAS[schema];
  const found = shape ? shape.fields[field]?.e : undefined;
  if (!found || found.length === 0) {
    throw new TypeError(
      "الحقل " + schema + "." + field + " ليس مجموعة مغلقة في العقد المُولَّد. " +
        "/ is not a closed set in the generated contract."
    );
  }
  return found;
}

/** نماذج الملكية، والاستعمال، والمعاملة الضريبية، والإقامة — كلّها من العقد. */
export const OWNERSHIP_MODELS = closedSet("PropertyRequest", "ownershipModel");
export const UNIT_USAGES = closedSet("UnitRequest", "usage");
export const VAT_TREATMENTS = closedSet("UnitRequest", "vatTreatment");
export const TAX_RESIDENCIES = closedSet("RealEstatePartyRequest", "taxResidency");
export const LEASE_STATES = closedSet("Lease", "state");
export const INVOICE_STATES = closedSet("RentInvoice", "state");

/**
 * جدول تسميةٍ لكل مجموعة مغلقة: العضو كما ينشره العقد ← مفتاحه في طبقة اللغة.
 * <b>ولماذا جدولٌ لا بادئةٌ تُلصَق بالعضو:</b> أعضاء العقد تحمل شرطةً سفلية
 * (<c>own_property</c>)، واصطلاح المفاتيح في هذا المستودع لا يقبلها — فلصقُها
 * يولّد مفتاحاً مخالفاً يمرّ صامتاً إلى الترجمة. والجدول يجعل الوصلة صريحة.
 */
const OWNERSHIP_LABEL: Readonly<Record<string, string>> = {
  own_property: "realestate.ownership.own",
  managed_for_others: "realestate.ownership.managed",
};

const RESIDENCY_LABEL: Readonly<Record<string, string>> = {
  resident: "realestate.residency.resident",
  non_resident: "realestate.residency.nonResident",
};

/**
 * حارس التسمية: عضوٌ في العقد بلا مفتاحٍ في طبقة اللغة يكسر الإقلاع **بصوت
 * عالٍ** بدل أن يُعرَض بلا اسم أو باسم جاره.
 * @param members الأعضاء كما نشرها العقد.
 * @param what اسم المجموعة في رسالة العطل.
 * @param known المفاتيح المعروفة.
 */
function requireLabels(members: readonly string[], what: string, known: readonly string[]): void {
  for (const member of members) {
    if (!known.includes(member)) {
      throw new TypeError(
        "عضوٌ في العقد بلا تسمية · a published member with no label: " + what + "." + member
      );
    }
  }
}

requireLabels(OWNERSHIP_MODELS, "ownershipModel", Object.keys(OWNERSHIP_LABEL));
requireLabels(TAX_RESIDENCIES, "taxResidency", Object.keys(RESIDENCY_LABEL));
requireLabels(UNIT_USAGES, "usage", ["residential", "commercial"]);
requireLabels(VAT_TREATMENTS, "vatTreatment", ["standard", "exempt"]);
requireLabels(LEASE_STATES, "leaseState", ["DRAFT", "ACTIVE"]);
requireLabels(INVOICE_STATES, "invoiceState", ["DRAFT", "POSTED"]);

/**
 * مفتاح تسمية نموذج الملكية.
 * @param model العضو كما نشره العقد.
 */
export function ownershipLabelKey(model: string): string {
  return OWNERSHIP_LABEL[model] ?? "realestate.ownership.label";
}

/**
 * مفتاح تسمية الإقامة الضريبية.
 * @param residency العضو كما نشره العقد.
 */
export function residencyLabelKey(residency: string): string {
  return RESIDENCY_LABEL[residency] ?? "realestate.residency.label";
}

/** «مُدار لصالح الغير» — النموذج الذي يستلزم مالكاً مسجَّلاً. */
export const MANAGED_FOR_OTHERS = "managed_for_others";

/** طرق التسوية المستعمَلة اليوم، كما يقولها وصف الحقل في العقد نفسه.
    وهي **ليست مجموعة مغلقة**: الحقل نصٌّ يقرؤه مؤهِّل الدور في المصفوفة،
    فتُعرَض اقتراحاً في `datalist` ولا تُحبَس في قائمةٍ يخترعها هذا الملفّ. */
export const SETTLEMENT_METHODS = ["cash", "bank", "card_clearing"] as const;

/* ═══════════════════════════════════════════ ٢ · نحو المال والتاريخ ═══ */

/** نمط التاريخ المنشور — ميلاديٌّ بأرقام لاتينية. */
export const ISO_DATE_RE = /^[0-9]{4}-(0[1-9]|1[0-2])-(0[1-9]|[12][0-9]|3[01])$/;

/**
 * هل يطابق النصّ نحو المال المنشور؟ الفراغ ليس مبلغاً ولا يُعامَل صفراً.
 * @param text النصّ كما كُتب.
 */
export function isMoneyText(text: string): boolean {
  return text !== "" && SCHEMA_Money_RE.test(text);
}

/** هل يطابق النصّ صيغة التاريخ المنشورة؟ */
export function isIsoDate(text: string): boolean {
  return ISO_DATE_RE.test(text);
}

/** اليوم بصيغة yyyy-MM-dd ميلادية — من الساعة لا من تنسيق ثقافة. */
export function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/* ═════════════════════════════════════════════ ٣ · حالة «لا منشأة» ═══ */

/** حين لا منشأة مختارة: الطريق إلى الاختيار، لا حقل معرّف يُكتب بيد. */
export function NeedsCompany(): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid="realestate-needs-company">
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("realestate.common.companyNeeded")}</h3>
      <p>{t("realestate.common.companyNeededBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="realestate-go-sign-in">
          {t("screen.signIn.action")}
        </Link>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════ ٤ · رأس شاشةٍ في هذا القسم ═══ */

/** أين نحن من شاشات القسم — والقائمة تُقرأ من موضعٍ واحد. */
export type RealEstateHere = "register" | "parties" | "lease" | "arrears";

/**
 * شاشات القسم الأربع بمساراتها، **بترتيب العمل لا بترتيب الحروف**: العقار
 * ووحداته يُعرَّفان مرّةً ← ثم طرفا العقد ← ثم العقد وجدوله ← ثم ما يُحصَّل
 * وما تأخّر. وهذا الترتيب **واحدٌ في ثلاثة مواضع**: `SCREENS`، وهذا الشريط،
 * وقائمة الملاحة اليدوية في `App.tsx` — وعليه حارس.
 */
const REALESTATE_SCREENS: readonly { readonly here: RealEstateHere; readonly path: string; readonly key: string }[] = [
  { here: "register", path: "/realestate", key: "realestate.nav.register" },
  { here: "parties", path: "/realestate/parties", key: "realestate.nav.parties" },
  { here: "lease", path: "/realestate/lease", key: "realestate.nav.lease" },
  { here: "arrears", path: "/realestate/arrears", key: "realestate.nav.arrears" },
];

/** رأس الشاشة: العنوان، والمقدّمة، والملاحة بين شاشات القسم الأربع. */
export function SectionHead(props: {
  readonly title: string;
  readonly lede: string;
  readonly here: RealEstateHere;
  readonly aside?: ReactNode;
}): ReactNode {
  const { t } = useT();
  return (
    <header className="stack re-head">
      <div className="pagehead">
        <div>
          <h1>{props.title}</h1>
          <p className="sub">{props.lede}</p>
        </div>
        {props.aside ? <div className="actions">{props.aside}</div> : null}
      </div>
      <nav className="re-tabs" aria-label={t("realestate.common.sectionNav")}>
        {REALESTATE_SCREENS.map((screen) => (
          <Link
            key={screen.path}
            to={screen.path}
            className="btn btn-sm"
            aria-current={props.here === screen.here ? "page" : undefined}
            data-testid={"re-tab-" + screen.here}
          >
            {t(screen.key)}
          </Link>
        ))}
      </nav>
    </header>
  );
}

/* ═════════════════════════════════ ٥ · الرفض: لوحةٌ تبقى، وخطوةٌ تالية ═══ */

/**
 * الخطوة التالية لكل رمز رفضٍ **تعرفه هذه الشاشة**. وما ليس هنا يُعرَض برسالة
 * الخادم وحدها — وهي رسالةٌ تسمّي البند أصلاً، فلا تُستبدل بتخمين.
 */
const NEXT_STEP: Readonly<Record<string, string>> = {
  "realestate.managed_property_needs_an_owner": "realestate.next.managedNeedsOwner",
  "realestate.owned_property_takes_no_owner": "realestate.next.ownedTakesNoOwner",
  "realestate.owner_share_split_not_decided": "realestate.next.shareSplit",
  "realestate.duplicate_code": "realestate.next.duplicateCode",
  "realestate.schedule_is_not_generated": "realestate.next.noSchedule",
  "realestate.instalments_do_not_sum_to_the_contract": "realestate.next.instalmentSum",
  "realestate.lease_term_overlaps": "realestate.next.overlap",
  "realestate.lease_is_not_active": "realestate.next.notActive",
  "realestate.lease_is_already_active": "realestate.next.alreadyActive",
  "realestate.schedule_line_already_invoiced": "realestate.next.alreadyInvoiced",
  "realestate.invoice_has_no_lines": "realestate.next.noLines",
  "realestate.document_is_not_a_draft": "realestate.next.notADraft",
  "realestate.receipt_is_not_posted": "realestate.next.receiptNotPosted",
  "realestate.receipt_is_already_allocated": "realestate.next.alreadyAllocated",
  "realestate.receipt_was_not_unallocated": "realestate.next.notUnallocated",
  "realestate.allocation_needs_a_lessee": "realestate.next.needsLessee",
  "realestate.property_not_found": "realestate.next.notFound",
  "realestate.unit_not_found": "realestate.next.notFound",
  "realestate.party_not_found": "realestate.next.notFound",
  "realestate.lease_not_found": "realestate.next.notFound",
  "realestate.document_not_found": "realestate.next.notFound",
};

/**
 * لوحة رفضٍ مقيمة: رسالة الخادم كما هي، وتحتها الخطوة التالية حين تعرفها
 * الشاشة. ولا تختفي بمؤقّت — الرفض يبقى حتى يتغيّر المُدخَل.
 * @param props الخطأ كما وصل.
 */
export function Refusal(props: { readonly error: unknown; readonly testId?: string }): ReactNode {
  const { t } = useT();
  const code = props.error instanceof ProblemError ? props.error.code : null;
  const next = code ? NEXT_STEP[code] : undefined;
  return (
    <div className={"stack " + MOTION.refuse} data-testid={props.testId ?? "realestate-refusal"}>
      <ProblemPanel error={props.error} />
      {next ? (
        <p className="alert alert--warning" role="status" data-testid="realestate-next-step">
          {t(next)}
        </p>
      ) : null}
    </div>
  );
}

/** رمز الرفض إن كان الخطأ مشكلةً منشورة، وإلا `null`. */
export function refusalCode(error: unknown): string | null {
  return error instanceof ProblemError ? error.code : null;
}

/* ═══════════════════════════ ٦ · «قيد البناء»: ما لا ينشره العقد بعد ═══ */

/**
 * حالةٌ صريحة لما **لا باب له في العقد**. وشاشةٌ ترسم قائمةً من بياناتٍ
 * مُختلَقة لتبدو كاملة أسوأ من شاشةٍ ناقصة تقول ما ينقصها.
 * @param props ما ينقص ولماذا وما يترتّب على المالك.
 */
export function NotYetPublished(props: {
  readonly title: string;
  readonly body: string;
  readonly operations: readonly string[];
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  return (
    <section className="re-pending" data-testid={props.testId ?? "realestate-pending"}>
      <div className="re-pending__head">
        <span className="pill pill--pending">{t("app.section.soon")}</span>
        <strong>{props.title}</strong>
      </div>
      <p className="muted">{props.body}</p>
      <ul className="re-pending__ops">
        {props.operations.map((operation) => (
          <li key={operation} className="mono" dir="ltr">
            {operation}
          </li>
        ))}
      </ul>
      <p className="muted">{t("realestate.common.ownerDecision")}</p>
    </section>
  );
}

/* ═════════════════════════════════════ ٧ · الاسم المُترجَم في هذا القسم ═══ */

/** الاسم كما يُعرض: السجلّ العربي، ومعه المرافق حين تختلف لغة العرض. */
export function TranslatedName(props: {
  readonly nameAr: string;
  readonly translations: readonly NameValue[];
}): ReactNode {
  const { locale } = useLocale();
  const resolved = resolveTranslatedName(props.nameAr, props.translations, locale);
  return (
    <>
      <span lang="ar">{props.nameAr}</span>
      {resolved.fallback || resolved.text === props.nameAr ? null : (
        <span className="alt" lang={resolved.tag}>
          {resolved.text}
        </span>
      )}
    </>
  );
}

/* ═══════════════════════════════════ ٨ · محرّر الترجمات (اسم/قيمة) ═══ */

/** صفٌّ في محرّر الترجمات — الوسم والقيمة، وكلاهما نصّ. */
export interface TranslationRow {
  readonly key: string;
  readonly tag: string;
  readonly value: string;
}

let translationSequence = 0;

/** صفُّ ترجمةٍ جديد فارغ. */
export function newTranslationRow(): TranslationRow {
  translationSequence += 1;
  return { key: "tr" + String(translationSequence), tag: "", value: "" };
}

/**
 * محرّر ترجمات الاسم. **العربية سجلٌّ ولا تُكتب هنا** (ADR-0021): هذا الحقل
 * للترجمات وحدها، وصفٌّ ناقصٌ لا يعبر السلك.
 * @param props الصفوف وما يغيّرها.
 */
export function TranslationEditor(props: {
  readonly idPrefix: string;
  readonly rows: readonly TranslationRow[];
  readonly onChange: (rows: readonly TranslationRow[]) => void;
}): ReactNode {
  const { t } = useT();
  const { rows, onChange } = props;

  const update = useCallback(
    (key: string, patch: Partial<TranslationRow>) => {
      onChange(rows.map((row) => (row.key === key ? { ...row, ...patch } : row)));
    },
    [rows, onChange]
  );

  return (
    <div className="stack">
      {rows.map((row) => (
        <div className="grid fields-half" key={row.key}>
          <div className="field">
            <label htmlFor={props.idPrefix + "-tag-" + row.key}>
              {t("realestate.common.langTag")}
            </label>
            <input
              id={props.idPrefix + "-tag-" + row.key}
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              data-testid="re-translation-tag"
              value={row.tag}
              onChange={(e) => update(row.key, { tag: e.target.value })}
              placeholder="en"
            />
          </div>
          <div className="field">
            <label htmlFor={props.idPrefix + "-val-" + row.key}>
              {t("realestate.common.langValue")}
            </label>
            <div className="row">
              <input
                id={props.idPrefix + "-val-" + row.key}
                className="ctl"
                autoComplete="off"
                data-testid="re-translation-value"
                value={row.value}
                onChange={(e) => update(row.key, { value: e.target.value })}
              />
              <button
                type="button"
                className="btn btn-sm"
                data-testid="re-translation-remove"
                onClick={() => onChange(rows.filter((r) => r.key !== row.key))}
              >
                {t("realestate.common.removeTranslation")}
              </button>
            </div>
          </div>
        </div>
      ))}
      <div className="row">
        <button
          type="button"
          className="btn btn-sm"
          data-testid="re-translation-add"
          onClick={() => onChange([...rows, newTranslationRow()])}
        >
          {t("realestate.common.addTranslation")}
        </button>
      </div>
    </div>
  );
}

/** الترجمات كما تعبر السلك: الصفوف المكتملة وحدها، وبلا صفٍّ نصفه فارغ. */
export function wireTranslations(rows: readonly TranslationRow[]): NameValue[] {
  return rows
    .filter((row) => row.tag !== "" && row.value !== "")
    .map((row) => ({ name: row.tag, value: row.value }));
}

/* ═══════════════════════════════════════ ٩ · سجلّ الجلسة، مقولاً صراحةً ═══ */

/** بندٌ سُجِّل في هذه الجلسة — وهو **ذاكرة تبويبة** لا سجلّ منشأة. */
export interface SessionEntry {
  readonly id: string;
  readonly kind: string;
  readonly code: string;
  readonly nameAr: string;
  readonly translations: readonly NameValue[];
  readonly note?: string;
}

/**
 * قائمة ما سُجِّل في هذه الجلسة. **تُسمّى بما هي**: العقد لا ينشر باباً يسرد،
 * وتسميتها «السجلّ» تجعل المستخدم يظنّ أنه يرى كل ما في المنشأة.
 * @param props البنود وما يُفتَح منها.
 */
export function SessionLog(props: {
  readonly entries: readonly SessionEntry[];
  readonly onPick?: (entry: SessionEntry) => void;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  if (props.entries.length === 0) {
    return (
      <EmptyState
        small
        title={t("realestate.common.nothingYet")}
        body={t("realestate.common.nothingYetBody")}
        testId="re-session-empty"
      />
    );
  }
  return (
    <div className="ledger" data-state="ready" data-testid={props.testId ?? "re-session-log"}>
      <table>
        <caption className="visually-hidden">{t("realestate.common.sessionOnly")}</caption>
        <thead>
          <tr>
            <th scope="col">{t("realestate.common.kind")}</th>
            <th scope="col">{t("realestate.common.code")}</th>
            <th scope="col">{t("realestate.common.nameAr")}</th>
            <th scope="col">{t("realestate.common.id")}</th>
          </tr>
        </thead>
        <tbody>
          {props.entries.map((entry) => (
            <tr key={entry.id} data-testid="re-session-row">
              <td>{t("realestate.kind." + entry.kind)}</td>
              <td className="code">{entry.code}</td>
              <td>
                <TranslatedName nameAr={entry.nameAr} translations={entry.translations} />
                {entry.note ? <span className="alt">{entry.note}</span> : null}
              </td>
              <td className="code">
                {props.onPick ? (
                  <button
                    type="button"
                    className="btn btn-sm btn-ghost mono"
                    data-testid="re-session-pick"
                    onClick={() => props.onPick?.(entry)}
                  >
                    {entry.id}
                  </button>
                ) : (
                  entry.id
                )}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

/* ══════════════════════════════════ ١٠ · حالة نداءٍ يكتب: ثلاثٌ لا اثنتان ═══ */

/** ما يعيده {@link useWrite}: التنفيذ، والانشغال، والنتيجة، والرفض. */
export interface WriteState<T> {
  readonly busy: boolean;
  readonly value: T | null;
  readonly error: unknown;
  readonly run: (task: () => Promise<T>) => Promise<void>;
  readonly reset: () => void;
  /** صنفُ حركة الترحيل — يُشعَل مرّةً عند نجاح فعلٍ لا رجعة فيه. */
  readonly moment: string;
  readonly fireMoment: () => void;
}

/**
 * نداءٌ يكتب، بحالاته الثلاث. والرفض **يُمسَح عند بدء نداءٍ جديد** لا عند
 * أوّل ضغطة مفتاح: لوحةٌ تختفي بمجرّد لمس حقلٍ تُقرأ قبل أن تُفهَم.
 * @param moment مفردة الحركة التي تُشعَل عند النجاح.
 */
export function useWrite<T>(moment: "post" | "arrive"): WriteState<T> {
  const [busy, setBusy] = useState(false);
  const [value, setValue] = useState<T | null>(null);
  const [error, setError] = useState<unknown>(null);
  const [momentClass, fireMoment] = useMoment(moment);

  const run = useCallback(
    async (task: () => Promise<T>) => {
      setBusy(true);
      setError(null);
      try {
        const result = await task();
        setValue(result);
        fireMoment();
      } catch (failure) {
        setError(failure);
      } finally {
        setBusy(false);
      }
    },
    [fireMoment]
  );

  const reset = useCallback(() => {
    setValue(null);
    setError(null);
  }, []);

  return useMemo(
    () => ({ busy, value, error, run, reset, moment: momentClass, fireMoment }),
    [busy, value, error, run, reset, momentClass, fireMoment]
  );
}
