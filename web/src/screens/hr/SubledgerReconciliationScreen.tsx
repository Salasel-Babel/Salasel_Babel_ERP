/* ═══════════════════════════════════════════════════════════════════════════
   /hr/subledger-reconciliation — مطابقة دفتر الموظفين المساعد
   The employee subledger reconciliation
   ───────────────────────────────────────────────────────────────────────────
   **«صفر انحراف» و«لم يُفحص شيء» يتشابهان على الشاشة، ولا يتشابهان في
   المعنى.** ولذلك يقف على هذه الشاشة رقمان لا رقمٌ واحد: عددُ المستندات التي
   **تطابق طرفاها بالضبط**، وعددُ ما انحرف. وتقريرٌ بصفرَين ليس تقريراً
   نظيفاً: هو تقريرٌ لم يقرأ شيئاً.

   ── ولا رقمَ واحدٌ اسمه «رصيد الموظف» — وهذا قرارٌ لا نقص ───────────────
   قارئ نقطة الضبط يجمّع **بلا تفصيل بالحساب** ويعيد صافياً واحداً، ودفتر
   الموظف يمتدّ على **أصلٍ واحد وثلاثة خصوم**: سلفة، وراتب مستحق، واستقطاع
   محتجَز، ومخصص نهاية خدمة. فصافٍ واحد يقاصّ سلفةً بمخصص خدمة براتب مستحق،
   **ويعلن التطابق وهو أعمى** — انحرافان متقابلان يُلغيان بعضهما. فالمطابقة
   هنا **مستنداً بمستند وطرفاً بطرف**، ولا يُعرض هنا رصيدٌ مجمَّع.

   ── ولماذا هذه المطابقة ممكنة أصلاً ───────────────────────────────────
   لأن الطرفين **متساويا الحبيبيّة**: قيدٌ لكل قسيمة يعني حركةً واحدة في نقطة
   الضبط لكل قسيمة وصفَّ محاولةٍ واحداً في جدول الوحدة لكل قسيمة. ولو رُحِّل
   المسيّر قيداً واحداً لصار الطرفان بحبيبيّتين مختلفتين ولاستحال هذا الباب.

   ── والفرز الذي على هذه الشاشة فرزُ عرضٍ لا بابُ استعلام ───────────────
   الباب المنشور يقبل `asOf` وحده. ومرشّحا السبب ونوع المستند أدناه يعملان
   على **ما وصل بالفعل**، ولا يُرسَل منهما شيء إلى الخادم — ومرشّحٌ يوحي
   باستعلامٍ لا وجود له يُعلّم من يقرأه أن يثق بترشيحٍ لم يقع.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { reconcileEmployeeSubledger } from "../../api/generated/client";
import type { HrReconciliation } from "../../api/generated/types";
import { PARAM_reconcileEmployeeSubledger_asOf_RE } from "../../api/generated/formats";
import { useApi } from "../../app/api-context";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Amount, useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel, StatCard, StatusBadge, useMoment } from "../../ui";
import { ChooseCompanyFirst, HrSectionNav, OpaqueCode, todayIso } from "./parts";
import { DIVERGENCE_REASONS, keySegment } from "./contract";
import "./hr.css";

/** قيمة «الكل» في مرشّحات العرض — ولا تعبر إلى الخادم. */
const ANY = "";

/** الشاشة كاملةً. */
export function SubledgerReconciliationScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();

  const [asOf, setAsOf] = useState(todayIso);
  const [reason, setReason] = useState(ANY);
  const [documentType, setDocumentType] = useState(ANY);
  const [report, setReport] = useState<HrReconciliation | null>(null);
  const [busy, setBusy] = useState(false);
  const [failure, setFailure] = useState<unknown>(null);
  const [arriveCls, fireArrive] = useMoment("arrive");
  const [refuseCls, fireRefuse] = useMoment("refuse");

  const asOfValid = asOf === "" || PARAM_reconcileEmployeeSubledger_asOf_RE.test(asOf);

  const run = useCallback(async () => {
    setBusy(true);
    setFailure(null);
    try {
      const found = await reconcileEmployeeSubledger(transport, {
        companyId: config.companyId,
        asOf,
      });
      setReport(found);
      fireArrive();
    } catch (problem) {
      setFailure(problem);
      fireRefuse();
    } finally {
      setBusy(false);
    }
  }, [asOf, config.companyId, fireArrive, fireRefuse, transport]);

  /* أنواع المستندات **من الصفوف التي وصلت** لا من قائمةٍ مكتوبة بيد: نوعٌ
     جديد ترسله الوحدة يظهر في المرشّح وحده، ولا يسقط من الترشيح صامتاً. */
  const documentTypes = useMemo(() => {
    const seen = new Set<string>();
    for (const row of report?.divergences ?? []) seen.add(row.documentType);
    return [...seen].sort();
  }, [report]);

  const rows = useMemo(
    () =>
      (report?.divergences ?? []).filter(
        (row) =>
          (reason === ANY || row.reasonCode === reason) &&
          (documentType === ANY || row.documentType === documentType)
      ),
    [documentType, reason, report]
  );

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-recon-needs-company" />;

  return (
    <section className="stack" data-testid="hr-reconciliation-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.reconTitle")}</h1>
          <p className="sub">{t("hr.page.reconLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr/subledger-reconciliation" />

      <Panel title={t("hr.recon.title")} note={t("hr.recon.note")} testId="hr-recon-ask">
        <div className="grid fields-2">
          <Field
            id="hr-recon-asof"
            label={t("hr.field.asOf")}
            hint={t("hr.field.asOfHint")}
            error={asOfValid ? undefined : t("hr.field.asOfBad")}
            source="typed"
            required
          >
            <input
              id="hr-recon-asof"
              className="ctl mono"
              type="date"
              dir="ltr"
              aria-invalid={!asOfValid}
              data-testid="hr-recon-asof"
              value={asOf}
              onChange={(e) => setAsOf(e.target.value)}
            />
          </Field>
          <div className="rowctl hr-act">
            <Button
              label={t("hr.act.reconcile")}
              kind="primary"
              loading={busy}
              disabled={!asOfValid || asOf === "" || busy}
              onClick={() => void run()}
              testId="hr-recon-run"
            />
          </div>
        </div>
        <p className="hint" data-testid="hr-recon-no-balance">{t("hr.recon.noBalance")}</p>
      </Panel>

      {failure ? (
        <div className={refuseCls}>
          <ProblemPanel error={failure} onRetry={() => void run()} />
        </div>
      ) : null}

      {report === null ? (
        <EmptyState title={t("hr.recon.emptyTitle")} body={t("hr.recon.emptyBody")} testId="hr-recon-empty" />
      ) : (
        <Panel
          title={t("hr.recon.resultTitle")}
          note={t("hr.recon.resultNote")}
          aside={
            <StatusBadge
              state={report.isReconciled ? "posted" : "rejected"}
              label={report.isReconciled ? t("hr.recon.clean") : t("hr.recon.dirty")}
              testId="hr-recon-verdict"
            />
          }
          testId="hr-recon-result"
        >
          <div className={"stats-row " + arriveCls}>
            <StatCard
              label={t("hr.recon.asOfLabel")}
              count={report.asOf}
              hint={t("hr.recon.asOfCardHint")}
              testId="hr-recon-asof-out"
            />
            <StatCard
              label={t("hr.recon.matched")}
              count={report.matchedDocuments}
              tone="good"
              hint={t("hr.recon.matchedHint")}
              testId="hr-recon-matched"
            />
            <StatCard
              label={t("hr.recon.divergent")}
              count={report.divergences.length}
              tone={report.divergences.length === 0 ? "neutral" : "bad"}
              hint={t("hr.recon.divergentHint")}
              testId="hr-recon-divergent"
            />
          </div>

          {report.matchedDocuments === 0 && report.divergences.length === 0 ? (
            <EmptyState
              title={t("hr.recon.nothingTitle")}
              body={t("hr.recon.nothingBody")}
              testId="hr-recon-nothing"
            />
          ) : report.divergences.length === 0 ? (
            <EmptyState
              small
              title={t("hr.recon.cleanTitle")}
              body={t("hr.recon.cleanBody")}
              testId="hr-recon-clean"
            />
          ) : (
            <>
              <div className="filterbar" data-testid="hr-recon-filters">
                <Field
                  id="hr-recon-reason"
                  label={t("hr.recon.reasonLabel")}
                  hint={t("hr.recon.reasonFilterHint")}
                  source="typed"
                >
                  <select
                    id="hr-recon-reason"
                    className="ctl"
                    data-testid="hr-recon-reason-filter"
                    value={reason}
                    onChange={(e) => setReason(e.target.value)}
                  >
                    <option value={ANY}>{t("common.label.all")}</option>
                    {DIVERGENCE_REASONS.map((code) => (
                      <option key={code} value={code}>
                        {t("hr.recon.reason." + keySegment(code))}
                      </option>
                    ))}
                  </select>
                </Field>
                <Field
                  id="hr-recon-doctype"
                  label={t("hr.recon.documentType")}
                  hint={t("hr.recon.documentTypeHint")}
                  source="typed"
                >
                  <select
                    id="hr-recon-doctype"
                    className="ctl mono"
                    data-testid="hr-recon-doctype-filter"
                    value={documentType}
                    onChange={(e) => setDocumentType(e.target.value)}
                  >
                    <option value={ANY}>{t("common.label.all")}</option>
                    {documentTypes.map((kind) => (
                      <option key={kind} value={kind}>
                        {kind}
                      </option>
                    ))}
                  </select>
                </Field>
              </div>

              <p className="muted">{tp("hr.count.divergences", rows.length)}</p>

              <div className="hr-table" data-testid="hr-recon-table">
                <table>
                  <caption className="visually-hidden">{t("hr.recon.resultTitle")}</caption>
                  <thead>
                    <tr>
                      <th scope="col">{t("hr.recon.documentType")}</th>
                      <th scope="col">{t("hr.recon.documentId")}</th>
                      <th scope="col">{t("hr.recon.party")}</th>
                      <th scope="col" className="n">{t("hr.recon.controlEffect")}</th>
                      <th scope="col" className="n">{t("hr.recon.subledgerEffect")}</th>
                      <th scope="col" className="n">{t("hr.recon.divergence")}</th>
                      <th scope="col">{t("hr.recon.reasonLabel")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {rows.map((row) => (
                      <tr key={row.documentType + "|" + row.documentId + "|" + row.partyId}>
                        <td><span className="mono" dir="ltr">{row.documentType}</span></td>
                        <td><span className="mono" dir="ltr">{row.documentId}</span></td>
                        <td>
                          <OpaqueCode code={row.partyId} testId="hr-recon-party" />
                        </td>
                        <td className="n"><Amount value={row.controlEffect} /></td>
                        <td className="n"><Amount value={row.subledgerEffect} /></td>
                        <td className="n"><Amount value={row.divergence} /></td>
                        <td>
                          {/* السبب مُسمّى بالكلمات **ومعه رمزه كما نشره العقد**:
                              فترجمةٌ لم تلحق عضواً جديداً لا تُخفي الرمز. */}
                          <span className="hr-name">
                            <span data-testid="hr-recon-reason-word">
                              {t("hr.recon.reason." + keySegment(row.reasonCode))}
                            </span>
                            <span className="alt mono" dir="ltr" data-testid="hr-recon-reason-code">
                              {row.reasonCode}
                            </span>
                          </span>
                        </td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
              <p className="hint">{t("hr.recon.grainNote")}</p>
            </>
          )}
        </Panel>
      )}
    </section>
  );
}
