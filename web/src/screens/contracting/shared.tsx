/* ═══════════════════════════════════════════════════════════════════════════
   المقاولات — ما تشترك فيه الشاشات الأربع
   Contracting — what the four screens share
   ───────────────────────────────────────────────────────────────────────────
   ثلاثة أشياء تعيش هنا لأن نسختين منها تنحرفان عند أول تعديل:

   ١ · **لوحة البنود المعلَّقة.** وحدة المقاولات تُرحّل ما قِيس وترفض ما لم
       يُحسَم، وترسل مع كل عقدٍ ومستخلص قائمة `pendingPolicy` بأسمائها. وهذه
       اللوحة **حالةٌ أولى دائمة** لا تنبيهاً يزول: تُسمّي كل بند برمزه
       وعنوانه وموضع سؤاله، وتقول الخطوة التالية — ولا تُخفي شيئاً خلف «ينقص
       إعداد».

   ٢ · **مُنتقي المشروع والعقد.** ولا حقل معرّفٍ يُكتب بيد حيث يوجد باب قراءة:
       `listProjects` يردّ المشاريع وتحتها عقودها، فيُختار العقد من قائمة لا
       يُلصَق معرّفه. وحيث لا باب قائمة — عقود الباطن والمستخلصات المفردة —
       يبقى المعرّف مُلصَقاً **ويُقرأ من الخادم قبل أن يُبنى عليه شيء**.

   ٣ · **إيصال الترحيل.** والفرق بين ترحيلٍ أول وإرسالٍ ثانٍ بالهوية نفسها
       يُقال صراحةً: `alreadyPosted` يعني «رُدّ إليك القيدُ الأول» لا «رُحِّل
       مرّتين»، وإخفاؤه يجعل المستخدم يقرأ عملاً جديداً حيث لا عمل.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useEffect, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { Link, useRouterState } from "@tanstack/react-router";
import { listProjects } from "../../api/generated/client";
import { SCHEMA_Magnitude_RE, SCHEMA_Money_RE, SCHEMA_Rate_RE } from "../../api/generated/formats";
import type { NameValue, PendingPolicyItem, Project, ProjectsDocument } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { resolveTranslatedName } from "../../app/translated-name";
import { useLocale, useT } from "../../i18n/react";
import { Button, EmptyState, MOTION, Panel, RefusalPanel, StatusBadge, useMoment } from "../../ui";
import { selectContracting, type ContractingSelection } from "./selection";
import "./contracting.css";

/* ═══════════════════════════════════════════════ ١ · حدود قبل كل شيء */

/** اليوم بصيغة yyyy-MM-dd ميلادية — من حقل التاريخ لا من تنسيق ثقافة. */
export function todayIso(): string {
  const now = new Date();
  const pad = (n: number) => String(n).padStart(2, "0");
  return String(now.getFullYear()) + "-" + pad(now.getMonth() + 1) + "-" + pad(now.getDate());
}

/** هل النصّ مبلغٌ بالنحو المنشور؟ الفراغ ليس مبلغاً. */
export function isMoneyText(text: string): boolean {
  return text !== "" && SCHEMA_Money_RE.test(text);
}

/** هل النصّ مقدار كمّية بالنحو المنشور؟ */
export function isMagnitudeText(text: string): boolean {
  return text !== "" && SCHEMA_Magnitude_RE.test(text);
}

/** هل النصّ نسبة تعاقدية بالنحو المنشور؟ */
export function isRateText(text: string): boolean {
  return text !== "" && SCHEMA_Rate_RE.test(text);
}

/**
 * عددٌ صحيح موجب مكتوباً بيد — التسلسل وفترة الضمان. ولا `parseInt` هنا:
 * النصّ يُفحص بمحارفه ثم يُبنى منه العدد بـ`Number` **بعد** أن يُثبت أنه
 * سلسلة أرقام قصيرة، فلا يمرّ عليه تحويلٌ يفقد دقّة.
 */
export function isCountText(text: string): boolean {
  return /^[0-9]{1,6}$/.test(text);
}

/** يحوّل نصّ عددٍ صحيح مفحوصاً إلى عدد. @param text النصّ. */
export function countOf(text: string): number {
  return Number(text);
}

/* ═════════════════════════════════════════ ٢ · لا شركة مختارة بعد */

/** حين لا شركة مختارة: الطريق إلى الاختيار، لا حقل معرّف يُكتب بيد. */
export function NeedsCompany(): ReactNode {
  const { t } = useT();
  return (
    <section className="empty" data-testid="contracting-needs-company">
      <div className="ico" aria-hidden="true">{"∅"}</div>
      <h3>{t("contracting.common.needCompany")}</h3>
      <p>{t("contracting.common.needCompanyBody")}</p>
      <div className="actions">
        <Link to="/sign-in" className="btn btn-primary" data-testid="contracting-go-sign-in">
          {t("contracting.common.chooseCompany")}
        </Link>
      </div>
    </section>
  );
}

/* ══════════════════════════════════════════════ ٣ · رأس الشاشة */

/**
 * شاشات القسم الأربع بمساراتها — والملاحة بينها داخل القسم، لا في هيكل
 * التطبيق: شاشاتُ قسمٍ تُقرأ معاً، ورفعُها إلى الملاحة العامّة يُغرقها.
 */
const CONTRACTING_SCREENS = [
  { path: "/contracting", key: "contracting.nav.register" },
  { path: "/contracting/certificate", key: "contracting.nav.certificate" },
  { path: "/contracting/subcontracting", key: "contracting.nav.subcontracting" },
  { path: "/contracting/retention", key: "contracting.nav.retention" },
] as const;

/** ملاحةٌ داخل القسم — أربع شاشات، والقائمة تُقرأ من موضعٍ واحد. */
export function ContractingNav(): ReactNode {
  const { t } = useT();
  const path = useRouterState({ select: (state) => state.location.pathname });
  return (
    <nav className="con-nav" aria-label={t("contracting.nav.label")} data-testid="contracting-nav">
      {CONTRACTING_SCREENS.map((screen) => (
        <Link
          key={screen.path}
          to={screen.path}
          className={"btn btn-sm" + (path === screen.path ? " btn-primary" : "")}
          aria-current={path === screen.path ? "page" : undefined}
          data-testid={"con-nav-" + screen.path}
        >
          {t(screen.key)}
        </Link>
      ))}
    </nav>
  );
}

/**
 * رأسٌ موحّد لشاشات القسم: ملاحةُ القسم، ثم عنوانٌ وسطرُ تعريف وما يُوضع في نهايته.
 * @param props العنوان والتعريف والملحق.
 */
export function ContractingHead(props: {
  readonly title: string;
  readonly lede: string;
  readonly aside?: ReactNode;
  readonly testId?: string;
}): ReactNode {
  return (
    <div className="stack">
      <ContractingNav />
      <header className="pagehead con-head" data-testid={props.testId}>
        <div>
          <h1>{props.title}</h1>
          <p className="sub">{props.lede}</p>
        </div>
        {props.aside ? <div className="actions">{props.aside}</div> : null}
      </header>
    </div>
  );
}

/* ═══════════════════════════════════ ٤ · البنود المعلَّقة — الرفض المُسمّى */

/**
 * لوحة البنود المعلَّقة — **حالةٌ أولى دائمة لا تنبيهٌ يزول**.
 * <p>
 * وهي تُعرض على العقد والمستخلص قبل أي محاولة مالية: من يقرأ العقد يعرف سلفاً
 * ما الذي سيرفضه الترحيل ولماذا، بدل أن يكتشفه عند أول ضغطة. وكل بندٍ يظهر
 * برمزه الثابت — وهو نقطة الاعتماد — وبعنوانه بالعربية والإنجليزية وبموضع
 * سؤاله الكامل.
 * </p>
 * @param props البنود والموضوع الذي تمنعه.
 */
export function PendingPolicyPanel(props: {
  readonly items: readonly PendingPolicyItem[];
  /** ما الذي تمنعه هذه البنود: عقدٌ بعينه أو مستخلص. */
  readonly subject: string;
  readonly testId?: string;
}): ReactNode {
  const { t, tp } = useT();
  const [moment, fire] = useMoment("refuse");
  const count = props.items.length;

  useEffect(() => {
    if (count > 0) fire();
  }, [count, fire]);

  if (count === 0) return null;

  return (
    <RefusalPanel
      title={t("contracting.pending.title")}
      titleEn="Posting is refused: items still pending an accountant's decision"
      body={t("contracting.pending.body")}
      subject={props.subject}
      subjectLabel={t("contracting.pending.subject")}
      next={t("contracting.pending.next")}
      moment={moment}
      testId={props.testId ?? "pending-policy"}
    >
      <p className="refusal-count">{tp("contracting.count.pending", count)}</p>
      <ol className="refusal-items">
        {props.items.map((item) => (
          <li key={item.code} className="refusal-item" data-code={item.code}>
            <code className="mono refusal-item__code" dir="ltr">
              {item.code}
            </code>
            <span className="refusal-item__ar">{item.titleAr}</span>
            <span className="refusal-item__en" lang="en" dir="ltr">
              {item.titleEn}
            </span>
            <span className="refusal-item__src">
              <span className="refusal-item__srck">{t("contracting.pending.source")}</span>
              <span className="mono" dir="ltr">
                {item.sourceRef}
              </span>
            </span>
          </li>
        ))}
      </ol>
    </RefusalPanel>
  );
}

/** حين لا بند معلَّق: القول صراحةً أن الباب مفتوح، لا صمت. */
export function PolicySettledNote(props: { readonly testId?: string }): ReactNode {
  const { t } = useT();
  return (
    <p className="alert alert--success" role="status" data-testid={props.testId ?? "policy-settled"}>
      {t("contracting.pending.none")}
    </p>
  );
}

/* ═════════════════════════════════════════════ ٥ · الاسم المُترجَم */

/**
 * اسمٌ عربيٌّ سجلّاً وترجمتُه بلغة الواجهة تحته — القاعدة نفسها في كل شاشة.
 * @param props السجلّ العربي وترجماته.
 */
export function TranslatedName(props: {
  readonly nameAr: string;
  readonly translations: readonly NameValue[];
}): ReactNode {
  const { locale } = useLocale();
  const resolved = useMemo(
    () => resolveTranslatedName(props.nameAr, props.translations, locale),
    [locale, props.nameAr, props.translations]
  );
  return (
    <>
      <span>{props.nameAr}</span>
      {resolved.fallback ? null : (
        <span className="alt" lang={resolved.tag}>
          {resolved.text}
        </span>
      )}
    </>
  );
}

/* ═══════════════════════════════════════ ٦ · مُنتقي المشروع والعقد */

/** ما يردّه المُنتقي عن حالته. */
export interface ProjectsFeed {
  /** المشاريع كما وصلت، أو قائمة فارغة. */
  readonly projects: readonly Project[];
  /** هل ما يزال يقرأ؟ */
  readonly loading: boolean;
  /** الرفض إن وقع. */
  readonly error: unknown;
  /** إعادة القراءة. */
  readonly reload: () => void;
}

/** يقرأ مشاريع المنشأة — استعلامٌ واحد تتقاسمه الشاشات بمفتاحه. */
export function useProjects(): ProjectsFeed {
  const { transport, config } = useApi();
  const result = useQuery({
    queryKey: ["contracting", "projects", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listProjects(transport, { companyId: config.companyId }, signal),
  });
  return {
    projects: result.data?.projects ?? [],
    loading: result.isPending && result.fetchStatus === "fetching",
    error: result.isError ? result.error : null,
    reload: () => {
      void result.refetch();
    },
  };
}

/**
 * مُنتقي المشروع ثم العقد — من قائمةٍ ردّها الخادم، لا من معرّفٍ يُلصَق.
 * @param props ما اختير الآن وما يُقرأ.
 */
export function ProjectContractPicker(props: {
  readonly feed: ProjectsFeed;
  readonly selection: ContractingSelection;
  /** هل يُعرض اختيار العقد أصلاً؟ شاشةُ الباطن تحتاج المشروع وحده. */
  readonly contracts?: boolean;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  const { feed, selection } = props;
  const showContracts = props.contracts !== false;

  const project = feed.projects.find((p) => p.id === selection.projectId) ?? null;

  return (
    <div className="filterbar con-picker" role="group" aria-label={t("contracting.picker.label")} data-testid={props.testId ?? "contracting-picker"}>
      <div className="field wide">
        <label htmlFor="con-project">{t("contracting.common.project")}</label>
        <select
          id="con-project"
          className="ctl"
          data-testid="picker-project"
          value={selection.projectId}
          onChange={(e) => {
            const chosen = feed.projects.find((p) => p.id === e.target.value);
            selectContracting({
              projectId: chosen?.id ?? "",
              projectCode: chosen?.code ?? "",
              contractId: "",
              contractNumber: "",
            });
          }}
        >
          <option value="">{t("contracting.picker.noProject")}</option>
          {feed.projects.map((p) => (
            <option key={p.id} value={p.id}>
              {p.code + " · " + p.nameAr}
            </option>
          ))}
        </select>
        <span className="hint">{t("contracting.picker.projectHint")}</span>
      </div>

      {showContracts ? (
        <div className="field wide">
          <label htmlFor="con-contract">{t("contracting.common.contract")}</label>
          <select
            id="con-contract"
            className="ctl"
            data-testid="picker-contract"
            disabled={!project}
            value={selection.contractId}
            onChange={(e) => {
              const chosen = project?.contracts.find((c) => c.id === e.target.value);
              selectContracting({
                contractId: chosen?.id ?? "",
                contractNumber: chosen?.number ?? "",
              });
            }}
          >
            <option value="">{t("contracting.picker.noContract")}</option>
            {(project?.contracts ?? []).map((c) => (
              <option key={c.id} value={c.id}>
                {c.number + " · " + c.currencyCode}
              </option>
            ))}
          </select>
          <span className="hint">{t("contracting.picker.contractHint")}</span>
        </div>
      ) : null}

      <div className="rowctl">
        <div className="inline-group">
          <Button label={t("contracting.common.refresh")} onClick={feed.reload} testId="picker-reload" />
        </div>
      </div>
    </div>
  );
}

/* ═══════════════════════════════════════════ ٧ · حالاتُ القراءة الأربع */

/** لوحُ تحميلٍ مصمَّم: يقول ماذا يُقرأ، ولا يترك الشاشة بيضاء. */
export function LoadingPanel(props: { readonly what: string; readonly testId?: string }): ReactNode {
  const { t } = useT();
  return (
    <div className="card card-pad" data-testid={props.testId ?? "contracting-loading"}>
      <strong>{t("contracting.common.loading")}</strong>
      <p className="muted">{props.what}</p>
      <div className="skel skel-text w-90" />
      <div className="skel skel-text w-75" />
      <div className="skel skel-text w-60" />
    </div>
  );
}

/**
 * لوحُ خطأ الطلب — وهو نفسه في كل شاشة.
 * @param props الخطأ وإعادة المحاولة.
 */
export function ReadProblem(props: { readonly error: unknown; readonly onRetry?: () => void }): ReactNode {
  return <ProblemPanel error={props.error} onRetry={props.onRetry} />;
}

/* ═══════════════════════════════════════════════ ٨ · إيصال الترحيل */

/**
 * إيصال ترحيل مستندٍ مالي في المقاولات — **ويفرّق صراحةً** بين ترحيلٍ أول
 * وإرسالٍ ثانٍ بالهوية نفسها ردّ القيد الأول.
 * @param props المستند وما يُفعل بعده.
 */
export function DocumentReceipt(props: {
  readonly document: ProjectsDocument;
  readonly onRepeat?: () => void;
  readonly busy?: boolean;
  readonly testId?: string;
}): ReactNode {
  const { t } = useT();
  const [moment, fire] = useMoment("post");
  const { document: doc } = props;
  const again = doc.alreadyPosted;

  useEffect(() => {
    fire();
  }, [doc.id, doc.entryId, again, fire]);

  return (
    <section
      className={"alert " + (again ? "alert--info" : "alert--success") + " " + moment}
      role="status"
      data-testid={props.testId ?? "contracting-receipt"}
      data-already-posted={String(again)}
    >
      <div className="body">
        <p className="title">
          {again ? t("contracting.posting.again") : t("contracting.posting.done")}
        </p>
        <p>{again ? t("contracting.posting.againBody") : t("contracting.posting.doneBody")}</p>
        <div className="kv">
          <div>
            <div className="k">{t("contracting.common.number")}</div>
            <div className="v mono" data-testid="receipt-number">
              {doc.number}
            </div>
          </div>
          <div>
            <div className="k">{t("contracting.common.state")}</div>
            <div className="v">
              <StatusBadge
                state={doc.state === "POSTED" ? "posted" : "draft"}
                label={t("contracting.state." + doc.state)}
              />
            </div>
          </div>
          <div>
            <div className="k">{t("contracting.posting.entry")}</div>
            <div className="v mono" dir="ltr" data-testid="receipt-entry">
              {doc.entryId ?? t("contracting.common.dash")}
            </div>
          </div>
        </div>
        {props.onRepeat ? (
          <div className="actions">
            <Button
              label={t("contracting.posting.repeat")}
              onClick={props.onRepeat}
              disabled={props.busy}
              testId="receipt-repeat"
            />
          </div>
        ) : null}
        <p className="muted">{t("contracting.posting.identity")}</p>
      </div>
    </section>
  );
}

/* ═══════════════════════════════════════ ٩ · لوحٌ يُطوى بلا فقدان حالته */

/**
 * لوحٌ قابل للطيّ — نماذج الكتابة تُطوى فلا تزاحم القراءة، ولا تُفقد ما كُتب
 * فيها: الطيّ يخفي ولا يُفكّك.
 * @param props العنوان والمحتوى.
 */
export function Foldable(props: {
  readonly title: string;
  readonly note?: string;
  readonly openLabel: string;
  readonly closeLabel: string;
  readonly children: ReactNode;
  readonly defaultOpen?: boolean;
  readonly testId?: string;
}): ReactNode {
  const [open, setOpen] = useState(props.defaultOpen ?? false);
  return (
    <Panel
      title={props.title}
      note={props.note}
      testId={props.testId}
      aside={
        <Button
          label={open ? props.closeLabel : props.openLabel}
          size="sm"
          onClick={() => setOpen((v) => !v)}
          testId={(props.testId ?? "foldable") + "-toggle"}
        />
      }
    >
      <div hidden={!open} className={open ? MOTION.reveal : undefined}>
        {props.children}
      </div>
    </Panel>
  );
}

/* ═══════════════════════════════════════════ ١٠ · فراغٌ يقول لماذا */

/**
 * فراغٌ مشروح — وفي هذا القسم للفراغ سببٌ يُقال: السجلّات المشتقّة من
 * المُرحَّل تبقى فارغة ما دام أول مستخلصٍ محجوباً.
 * @param props العنوان والسبب.
 */
export function ExplainedEmpty(props: {
  readonly title: string;
  readonly body: string;
  readonly action?: ReactNode;
  readonly testId?: string;
}): ReactNode {
  return <EmptyState title={props.title} body={props.body} action={props.action} testId={props.testId} />;
}
