/* ═══════════════════════════════════════════════════════════════════════════
   /hr/payslip — القسيمة بلغة صاحبها  ·  The payslip, in its owner's language
   ───────────────────────────────────────────────────────────────────────────
   **قسيمةٌ لا يقرؤها صاحبها قسيمةٌ سيُنازَع فيها.** ومن يعمل في هذه السوق
   يقرأ الأردية أو الهندية أو الإنجليزية، لا العربية وحدها. ولذلك:

     · **أسماء مكوّنات الأجر تُحلّ إلى لغة الواجهة** — لا إلى الإنجليزية
       دائماً: الإنجليزية واحدة من N، والارتداد عند غياب الترجمة **إلى السجلّ
       العربي** لا إلى لغةٍ ثالثة (ADR-0021).
     · ومكوّنٌ **لا اسم له بلغةٍ ما** يُعرَض برمزه ومعه جملةٌ تقول إن الاسم لم
       يُترجَم بعد — ولا يُخترع له اسم، ولا يُترجَم رمزُه آلياً.
     · والمبالغ تُعرَض بأرقام اللغة عبر طبقة التدويل، ونصُّ السلك يبقى في
       السمة `title` بايتاً ببايت: العرض تقريبٌ مُعلَن لا قيمةٌ بديلة.

   ── ولا معرّف شخصي على هذه الورقة ──────────────────────────────────────
   القسيمة كما ينشرها العقد تحمل **الرمز المعتم** ومركز التكلفة والمبالغ.
   ولا تحمل اسماً ولا هويةً ولا آيباناً — وهذا **ليس نقصاً في العرض**: هو
   البنية نفسها التي تمنع المعرّف الشخصي من بلوغ الدفتر. فالشاشة تقول ذلك
   صراحةً بدل أن يقرأه أحدٌ حقلاً نُسي.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useQuery } from "@tanstack/react-query";
import { listPayComponents, readPayslip } from "../../api/generated/client";
import type { HrPayComponent } from "../../api/generated/types";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { resolveTranslatedName } from "../../app/translated-name";
import { Amount, Num, useLocale, useT } from "../../i18n/react";
import { SOURCE } from "../../i18n/engine";
import { Button, EmptyState, Field, Panel, StatCard, useMoment } from "../../ui";
import { useHrFocus } from "./focus";
import {
  AmountsRow,
  ChooseCompanyFirst,
  HrSectionNav,
  EntryRef,
  HrState,
  OpaqueCode,
  StatePanel,
} from "./parts";
import "./hr.css";

/** الشاشة كاملةً. */
export function PayslipScreen(): ReactNode {
  const { t, tp } = useT();
  const { locale, meta, i18n } = useLocale();
  const { transport, config } = useApi();
  const [focus, setFocus] = useHrFocus();

  const [typedId, setTypedId] = useState(focus.payslipId);
  const [payslipId, setPayslipId] = useState(focus.payslipId);
  const [arriveCls, fireArrive] = useMoment("arrive");

  const payslip = useQuery({
    queryKey: ["hr", "payslip", config.baseUrl, config.token, config.companyId, payslipId],
    enabled: config.companyId !== "" && payslipId !== "",
    retry: false,
    queryFn: ({ signal }) => readPayslip(transport, { companyId: config.companyId, payslipId }, signal),
  });

  const components = useQuery({
    queryKey: ["hr", "pay-components", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listPayComponents(transport, { companyId: config.companyId }, signal),
  });

  /* خريطة الرمز إلى تعريفه — تُبنى مرّةً لا مرّةً لكل سطر. */
  const byCode = useMemo(() => {
    const map = new Map<string, HrPayComponent>();
    for (const component of components.data?.items ?? []) map.set(component.code, component);
    return map;
  }, [components.data]);

  const open = useCallback(
    (id: string) => {
      setPayslipId(id);
      setTypedId(id);
      setFocus({ payslipId: id });
      fireArrive();
    },
    [fireArrive, setFocus]
  );

  const slip = payslip.data ?? null;
  /* اتجاه اللغة من فهرس اللغات نفسه — لغة خامسة تعمل بلا سطر هنا. */
  const uiDir = i18n.catalogue.find((entry) => entry.code === locale)?.dir ?? "ltr";

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-payslip-needs-company" />;

  return (
    <section className="stack" data-testid="hr-payslip-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.payslipTitle")}</h1>
          <p className="sub">{t("hr.page.payslipLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr/payslip" />

      <Panel title={t("hr.payslip.lookup")} note={t("hr.payslip.lookupNote")} testId="hr-payslip-lookup">
        <div className="grid fields-2">
          <Field
            id="hr-payslip-id"
            label={t("hr.field.payslipId")}
            hint={t("hr.field.payslipIdHint")}
            source="typed"
          >
            <input
              id="hr-payslip-id"
              className="ctl mono"
              dir="ltr"
              autoComplete="off"
              spellCheck={false}
              data-testid="hr-payslip-id"
              value={typedId}
              onChange={(e) => setTypedId(e.target.value)}
              onKeyDown={(e) => {
                if (e.key === "Enter" && typedId !== "") open(typedId);
              }}
              placeholder="00000000-0000-0000-0000-000000000000"
            />
          </Field>
          <div className="rowctl hr-act">
            <Button
              label={t("hr.act.read")}
              kind="primary"
              disabled={typedId === ""}
              onClick={() => open(typedId)}
              testId="hr-payslip-read"
            />
          </div>
        </div>
      </Panel>

      {payslip.isError ? (
        <ProblemPanel error={payslip.error} onRetry={() => void payslip.refetch()} />
      ) : null}

      {payslipId === "" ? (
        <EmptyState
          title={t("hr.payslip.emptyTitle")}
          body={t("hr.payslip.emptyBody")}
          testId="hr-payslip-empty"
        />
      ) : null}

      {payslipId !== "" && payslip.isPending && payslip.fetchStatus === "fetching" ? (
        <StatePanel title={t("hr.payslip.card")} loading testId="hr-payslip-loading">
          {null}
        </StatePanel>
      ) : null}

      {slip ? (
        <article className={"hr-payslip " + arriveCls} lang={locale} dir={uiDir} data-testid="hr-payslip-card">
          <header className="hr-payslip__head">
            <div className="stack">
              <strong>{t("hr.payslip.card")}</strong>
              <span className="hr-payslip__lang" data-testid="hr-payslip-language">
                {t("hr.payslip.readsIn", { language: meta.native })}
              </span>
            </div>
            <span className="spacer" />
            <HrState state={slip.state} testId="hr-payslip-state" />
          </header>

          <div className="kv">
            <div>
              <div className="k">{t("hr.code.label")}</div>
              <div className="v">
                <OpaqueCode code={slip.employeeCode} testId="hr-payslip-employee-code" />
              </div>
            </div>
            <div>
              <div className="k">{t("hr.field.costCenter")}</div>
              <div className="v mono" dir="ltr">{slip.costCenterId}</div>
            </div>
            <div>
              <div className="k">{t("hr.entry.label")}</div>
              <div className="v">
                <EntryRef entryId={slip.entryId} testId="hr-payslip-entry" />
              </div>
            </div>
            <div>
              <div className="k">{t("hr.payslip.alreadyPosted")}</div>
              <div className="v" data-testid="hr-payslip-already">
                {slip.alreadyPosted ? t("hr.payslip.alreadyYes") : t("hr.payslip.alreadyNo")}
              </div>
            </div>
          </div>

          <p className="hint" data-testid="hr-payslip-no-identity">{t("hr.payslip.noIdentity")}</p>

          <div className="stats-row hr-one">
            <StatCard
              label={t("hr.run.contributoryWage")}
              amount={slip.contributoryWage}
              hint={t("hr.payslip.wageHint")}
              testId="hr-payslip-wage"
            />
          </div>

          <AmountsRow amounts={slip.amounts} testId="hr-payslip-amounts" />

          <h2 className="hr-subhead">{t("hr.payslip.components")}</h2>
          <p className="muted">{tp("hr.count.components", slip.components.length)}</p>

          {slip.components.length === 0 ? (
            <EmptyState
              small
              title={t("hr.payslip.componentsEmpty")}
              body={t("hr.payslip.componentsEmptyBody")}
              testId="hr-payslip-components-empty"
            />
          ) : (
            <div className="hr-table" data-testid="hr-payslip-components">
              <table>
                <caption className="visually-hidden">{t("hr.payslip.components")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("hr.payslip.lineNo")}</th>
                    <th scope="col">{t("hr.payslip.componentName")}</th>
                    <th scope="col">{t("hr.field.kind")}</th>
                    <th scope="col">{t("hr.payslip.entersWage")}</th>
                    <th scope="col" className="n">{t("hr.field.amount")}</th>
                  </tr>
                </thead>
                <tbody>
                  {slip.components.map((line) => {
                    const defined = byCode.get(line.componentCode);
                    const resolved = defined
                      ? resolveTranslatedName(defined.nameAr, defined.nameTranslations, locale)
                      : null;
                    const untranslated = resolved !== null && resolved.fallback && locale !== SOURCE;
                    return (
                      <tr key={line.lineNo}>
                        <td><Num value={line.lineNo} /></td>
                        <td>
                          {resolved ? (
                            <span className="hr-name">
                              <span lang={untranslated ? SOURCE : resolved.tag} dir={untranslated ? "rtl" : uiDir}>
                                {resolved.text}
                              </span>
                              <span className="alt mono" dir="ltr">{line.componentCode}</span>
                              {untranslated ? (
                                <span className="alt" data-testid="hr-component-untranslated">
                                  {t("hr.payslip.untranslated")}
                                </span>
                              ) : null}
                            </span>
                          ) : (
                            <span className="hr-name">
                              <span className="mono" dir="ltr">{line.componentCode}</span>
                              <span className="alt" data-testid="hr-component-unknown">
                                {t("hr.payslip.unknownComponent")}
                              </span>
                            </span>
                          )}
                        </td>
                        <td>
                          <span
                            className={"pill " + (line.kind === "earning" ? "pill--debit" : "pill--credit")}
                            data-testid="hr-component-kind"
                          >
                            {t("hr.kind." + line.kind)}
                          </span>
                        </td>
                        <td>
                          {line.entersContributoryWage ? t("hr.payslip.entersYes") : t("hr.payslip.entersNo")}
                        </td>
                        <td className="n"><Amount value={line.amount} /></td>
                      </tr>
                    );
                  })}
                </tbody>
              </table>
            </div>
          )}

          {components.isError ? (
            <ProblemPanel error={components.error} onRetry={() => void components.refetch()} />
          ) : null}

          <p className="hint">{t("hr.payslip.footnote")}</p>
        </article>
      ) : null}
    </section>
  );
}
