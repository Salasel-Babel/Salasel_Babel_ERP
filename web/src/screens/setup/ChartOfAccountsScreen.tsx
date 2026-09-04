/* ═══════════════════════════════════════════════════════════════════════════
   /setup/chart-of-accounts — دليلُ الحسابات بشروط الترحيل  ·  The chart
   ───────────────────────────────────────────────────────────────────────────
   شاشةُ **قراءةٍ لا تكتب شيئاً**، وستّةٌ تحكمها:

   ١ · **ولا رقمَ حسابٍ مكتوبٍ في هذه الشاشة إطلاقاً.** ما يُعرض يأتي من
       الخادم، ومصفوفةُ الترحيل في `data/posting-matrix/` هي التي تقرّر أيّ
       حسابٍ لأيّ دور. وشاشةٌ تكتب رمزاً في شيفرتها تصير مصدرَ حقيقةٍ ثانياً
       يفترق عن الأول عند أوّل دليلِ عميلٍ يخالفه.

   ٢ · **والترقيم نفسه قرارٌ مفتوحٌ على المالك ولم يُحسم**، وهو الصفّ ٢١ في
       `docs/decisions/قرارات-على-المالك.md`: وثيقتان مرجعيتان في هذا
       المستودع تحملان ترقيمين مختلفين للحسابات نفسها. فهذه الشاشة **تعرض ما
       يردّه الخادم وتُعلن الحدّ** ولا تحسمه ولا تسكت عنه.

   ٣ · **والشجرة تُبنى من `parentCode` لا من بادئة الرمز.** نصُّ العقد:
       «البادئة تصدق على هذا الدليل وتكذب على أول دليل عميل يخالفها».

   ٤ · **والعدّادان يُقارَنان بما وصل فيُرى النقص.** `accountCount` يصل
       محسوباً من الخادم، فطولُ `accounts` الذي يخالفه استجابةٌ ناقصة —
       وعميلٌ يعدّ بنفسه لا يملك ما يقارن به.

   ٥ · **والدليل يُعرض كاملاً بآبائه التجميعية**، لا مقتصراً على ما يقبل
       الترحيل: قائمةُ الأوراق وحدها تدفع القارئ إلى اختراع تجميعٍ من بادئات
       الرموز. والمرشّحان مُطفآن ابتداءً، فلا يختفي شيءٌ قبل أن يُطلب.

   ٦ · **وشروطُ الترحيل هي المعلومة التي كانت مجهولةً عند العميل**: نوعُ طرف
       الأستاذ المساعد، والأبعاد الإلزامية، ونمط العملة. وكان الدفتر يرفض بـ
       `ledger.posting.missing_subledger` و`guard.GR-COA-002` ولا يبلغهما
       العميل إلا **بأن يُرحِّل فيُرفَض**.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { readChartOfAccounts } from "../../api/generated/client";
import type { PostingChartEntry } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { RECORD_TAG, resolveTranslatedName } from "../../app/translated-name";
import { useLocale, useT } from "../../i18n/react";
import { EmptyState, StatCard, useMoment } from "../../ui";
import {
  ChooseCompanyFirst,
  DeclaredGap,
  SetupBadge,
  SetupField,
  SetupSectionNav,
  StatePanel,
} from "./parts";
import "./setup.css";

/** رمز الحارس الذي يرفض سطراً بلا بُعدٍ إلزامي. */
const DIMENSION_GUARD = "guard.GR-COA-002";

/** رمز الرفض حين يغيب طرف الأستاذ المساعد. */
const SUBLEDGER_CODE = "ledger.posting.missing_subledger";

/** القيمة التي يحملها `subledgerType` حين لا يطلب الحساب طرفاً. */
const NO_SUBLEDGER = "none";

/**
 * مقطعُ مفتاحِ نمطِ العملة. **ولا يُشتقّ المفتاح من القيمة حرفاً**: قيمةُ
 * العقد `company_only` تحمل شَرطةً سفلية، ومفاتيح هذه الطبقة لا تقبلها —
 * فيُحوَّل الرمز إلى مقطعه هنا، ويبقى الرمز نفسه هو نقطة الاعتماد.
 */
function currencyKey(mode: PostingChartEntry["currencyMode"]): string {
  if (mode === "company_only") return "companyOnly";
  return mode;
}

/** الشاشة كاملةً. */
export function ChartOfAccountsScreen(): ReactNode {
  const { t } = useT();
  const { transport, config } = useApi();
  const { locale } = useLocale();
  const [arriveCls] = useMoment("arrive");

  const [filter, setFilter] = useState("");
  /* **مُطفآن ابتداءً**: ما يختفي بلا أن يُطلب يُظنّ غير موجود. */
  const [postableOnly, setPostableOnly] = useState(false);
  const [activeOnly, setActiveOnly] = useState(false);

  const chart = useQuery({
    queryKey: ["setup", "chart-of-accounts", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => readChartOfAccounts(transport, { companyId: config.companyId }, signal),
  });

  const data = chart.data ?? null;
  const accounts: readonly PostingChartEntry[] = data?.accounts ?? [];

  /* ــ النقص يُرى لأن العدّاد وصل محسوباً ــــــــــــــــــــــــــــــــ */
  const short = data !== null && data.accountCount !== data.accounts.length;

  const shown = useMemo(() => {
    const needle = filter.trim().toLocaleLowerCase();
    return accounts.filter((entry) => {
      if (postableOnly && !entry.postable) return false;
      if (activeOnly && !entry.active) return false;
      if (needle === "") return true;
      return (
        entry.accountCode.toLocaleLowerCase().includes(needle) ||
        entry.nameAr.includes(filter.trim()) ||
        entry.nameTranslations.some((n) => n.value.toLocaleLowerCase().includes(needle))
      );
    });
  }, [accounts, activeOnly, filter, postableOnly]);

  if (config.companyId === "") return <ChooseCompanyFirst testId="setup-coa-needs-company" />;

  return (
    <section className="stack" data-testid="setup-chart-screen">
      <header className="pagehead">
        <div>
          <h1>{t("screen.coa.pageTitle")}</h1>
          <p className="sub">{t("screen.coa.pageLede")}</p>
        </div>
      </header>

      <SetupSectionNav current="/setup/chart-of-accounts" />

      {/* ══════════════════════════ ١ · العدّادان ومِصفاةُ العرض ════════ */}
      <StatePanel
        title={t("screen.coa.countsTitle")}
        note={t("screen.coa.countsNote")}
        loading={chart.isPending && chart.fetchStatus === "fetching"}
        testId="setup-coa-counts"
      >
        {chart.isError ? (
          <ProblemPanel error={chart.error} onRetry={() => void chart.refetch()} />
        ) : data === null ? null : (
          <div className={"stack " + arriveCls}>
            <div className="stats-row">
              <StatCard
                label={t("screen.coa.declared")}
                count={data.accountCount}
                hint={t("screen.coa.declaredHint")}
                testId="setup-coa-declared"
              />
              <StatCard
                label={t("screen.coa.arrived")}
                count={data.accounts.length}
                hint={t("screen.coa.arrivedHint")}
                tone={short ? "bad" : "neutral"}
                testId="setup-coa-arrived"
              />
              <StatCard
                label={t("screen.coa.postable")}
                count={data.postableCount}
                hint={t("screen.coa.postableHint")}
                tone="good"
                testId="setup-coa-postable"
              />
            </div>

            {short ? (
              <div className="alert alert--warning" role="alert" data-testid="setup-coa-short">
                <div className="body">
                  <span className="title">{t("screen.coa.shortTitle")}</span>
                  <p>{t("screen.coa.shortBody")}</p>
                </div>
              </div>
            ) : null}

            <div className="grid fields-3">
              <SetupField
                id="stp-coa-filter"
                label={t("screen.coa.filterLabel")}
                hint={t("screen.coa.filterHint")}
                source="typed"
              >
                <input
                  id="stp-coa-filter"
                  className="ctl"
                  autoComplete="off"
                  data-testid="setup-coa-filter"
                  value={filter}
                  onChange={(e) => setFilter(e.target.value)}
                />
              </SetupField>
              <SetupField
                id="stp-coa-postable"
                label={t("screen.coa.postableOnlyLabel")}
                hint={t("screen.coa.postableOnlyHint")}
                source="typed"
              >
                <select
                  id="stp-coa-postable"
                  className="ctl"
                  data-testid="setup-coa-postable-only"
                  value={postableOnly ? "yes" : "no"}
                  onChange={(e) => setPostableOnly(e.target.value === "yes")}
                >
                  <option value="no">{t("screen.coa.showAll")}</option>
                  <option value="yes">{t("screen.coa.showPostable")}</option>
                </select>
              </SetupField>
              <SetupField
                id="stp-coa-active"
                label={t("screen.coa.activeOnlyLabel")}
                hint={t("screen.coa.activeOnlyHint")}
                source="typed"
              >
                <select
                  id="stp-coa-active"
                  className="ctl"
                  data-testid="setup-coa-active-only"
                  value={activeOnly ? "yes" : "no"}
                  onChange={(e) => setActiveOnly(e.target.value === "yes")}
                >
                  <option value="no">{t("screen.coa.showAll")}</option>
                  <option value="yes">{t("screen.coa.showActive")}</option>
                </select>
              </SetupField>
            </div>
          </div>
        )}
      </StatePanel>

      {/* ══════════════════ ٢ · الدليل وشروطُ الترحيل على كل حساب ═══════ */}
      <StatePanel
        title={t("screen.coa.tableTitle")}
        note={
          postableOnly || activeOnly || filter.trim() !== ""
            ? t("screen.coa.filteredNote")
            : t("screen.coa.tableNote")
        }
        loading={chart.isPending && chart.fetchStatus === "fetching"}
        testId="setup-coa-table-panel"
      >
        {data === null ? null : shown.length === 0 ? (
          <EmptyState
            title={t("screen.coa.emptyTitle")}
            body={t("screen.coa.emptyBody")}
            testId="setup-coa-empty"
          />
        ) : (
          <div className="tablewrap" data-testid="setup-coa-table">
            <table className="data">
              <caption className="visually-hidden">{t("screen.coa.tableTitle")}</caption>
              <thead>
                <tr>
                  <th scope="col" className="start">{t("screen.coa.colAccount")}</th>
                  <th scope="col" className="start">{t("screen.coa.colType")}</th>
                  <th scope="col" className="start">{t("screen.coa.colPostable")}</th>
                  <th scope="col" className="start">{t("screen.coa.colSubledger")}</th>
                  <th scope="col" className="start">{t("screen.coa.colDimensions")}</th>
                  <th scope="col" className="start">{t("screen.coa.colCurrency")}</th>
                </tr>
              </thead>
              <tbody>
                {shown.map((entry) => {
                  const resolved = resolveTranslatedName(
                    entry.nameAr,
                    entry.nameTranslations,
                    locale
                  );
                  return (
                    <tr
                      key={entry.accountCode}
                      data-level={entry.level}
                      data-testid={"setup-coa-row-" + entry.accountCode}
                    >
                      {/* الإزاحة **بالمستوى الواصل** لا بطول الرمز ولا ببادئته. */}
                      <td className="tree start">
                        <span className="indent" aria-hidden="true" />
                        <span className="acct-code">{entry.accountCode}</span>
                        <span lang={RECORD_TAG} dir="rtl">{entry.nameAr}</span>
                        {resolved.fallback || locale === RECORD_TAG ? null : (
                          <>
                            {" "}
                            <span className="alt" lang={resolved.tag} dir="auto">
                              {resolved.text}
                            </span>
                          </>
                        )}
                      </td>
                      <td className="start">
                        <SetupBadge
                          label={t("screen.coa.type." + entry.accountType)}
                          tone={entry.accountType === "asset" || entry.accountType === "expense" ? "info" : "pending"}
                          testId={"setup-coa-type-" + entry.accountCode}
                        />
                        {entry.contra ? (
                          <>
                            {" "}
                            <span className="pill pill--pending">{t("screen.coa.contra")}</span>
                          </>
                        ) : null}
                        {" "}
                        <span className="muted">{t("screen.coa.side." + entry.naturalSide)}</span>
                      </td>
                      <td className="start">
                        <SetupBadge
                          label={
                            entry.postable ? t("screen.coa.postableYes") : t("screen.coa.postableNo")
                          }
                          tone={entry.postable ? "posted" : "archived"}
                          title={entry.postable ? undefined : t("screen.coa.summaryTitle")}
                          testId={"setup-coa-postable-" + entry.accountCode}
                        />
                        {entry.active ? null : (
                          <>
                            {" "}
                            <span
                              className="pill pill--pending"
                              data-testid={"setup-coa-inactive-" + entry.accountCode}
                            >
                              {t("screen.coa.inactive")}
                            </span>
                          </>
                        )}
                      </td>
                      <td className="start">
                        {entry.subledgerType === NO_SUBLEDGER ? (
                          <span className="muted">{t("screen.coa.noSubledger")}</span>
                        ) : (
                          <span
                            className="mono"
                            dir="ltr"
                            title={t("screen.coa.subledgerTitle")}
                            data-testid={"setup-coa-subledger-" + entry.accountCode}
                          >
                            {entry.subledgerType}
                          </span>
                        )}
                      </td>
                      <td className="start">
                        {entry.requiredDimensions.length === 0 ? (
                          <span className="muted">{t("screen.coa.noDimensions")}</span>
                        ) : (
                          entry.requiredDimensions.map((dimension) => (
                            <span
                              key={dimension}
                              className="mono"
                              dir="ltr"
                              data-testid={"setup-coa-dim-" + entry.accountCode + "-" + dimension}
                            >
                              {dimension}{" "}
                            </span>
                          ))
                        )}
                      </td>
                      <td className="start">
                        <span className="muted">{t("screen.coa.currency." + currencyKey(entry.currencyMode))}</span>
                        {entry.currencyCode === null ? null : (
                          <>
                            {" "}
                            <span className="mono" dir="ltr">{entry.currencyCode}</span>
                          </>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        )}
        <p className="hint">
          {t("screen.coa.refusalNote")}{" "}
          <span className="mono" dir="ltr">{SUBLEDGER_CODE}</span>{" "}
          <span className="mono" dir="ltr">{DIMENSION_GUARD}</span>
        </p>
      </StatePanel>

      {/* ═════════ ٣ · قرارٌ مفتوحٌ على المالك — يُعلَن ولا يُحسم هنا ═══ */}
      <DeclaredGap
        title={t("screen.coa.gapNumberingTitle")}
        body={t("screen.coa.gapNumberingBody")}
        owed={t("screen.coa.gapNumberingOwed")}
        testId="setup-coa-gap-numbering"
      />
    </section>
  );
}
