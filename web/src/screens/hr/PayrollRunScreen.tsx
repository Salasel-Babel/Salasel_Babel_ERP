/* ═══════════════════════════════════════════════════════════════════════════
   /hr/payroll — مسيّر الرواتب  ·  The payroll run
   ───────────────────────────────────────────────────────────────────────────
   **هنا يكسب هذا القسم قيمته**، وثلاث جملٍ تحكم الشاشة كلّها:

   ١ · **ما سيُرحَّل يُرى قبل أن يُرحَّل.** المسيّر يُنشأ مسوّدةً، وقسائمه تُقرأ
       صفّاً صفّاً بمبالغها الستّة، ثم — وحينئذ فقط — يُضغَط الترحيل. ولا زرَّ
       يجمع «أنشئ ورحّل» في نقرةٍ واحدة: الفعل الذي لا رجعة فيه لا يُخبَّأ خلف
       فعلٍ يُراجَع.

   ٢ · **الرفض يُسمّي البند ولا يُخترَع بديلٌ عنه.** جدول نِسَب الاشتراك في هذا
       النظام **يُسلَّم فارغاً عمداً**: النسبة وحدّا الأجر الخاضع غير محسومَين
       (البند م-14)، ولا يُكتب واحدٌ منها في شيفرة ولا في اختبار — «فرقمٌ في
       اختبار يُنسخ إلى إنتاج بعد شهرين». فالمسيّر على فترةٍ لا يغطّيها صفٌّ
       معتمد **يُرفض**، والرفض يُعرَض لوحةً باقية تسمّي التصنيف والتاريخ، ومعها
       الباب الذي يُودَع فيه الصفّ. ولا قيمة افتراضية، ولا صفرٌ صامت.

   ٣ · **الترحيل الثاني يقول الحقيقة.** هوية الإحكام في هذا النظام تضمن أن
       نداءً مكرَّراً يُعيد **الإيصال نفسه** و`alreadyPosted = true`. فالشاشة
       تعدّ القسائم التي رُحِّلت الآن وتلك التي كانت مُرحَّلة، وتقول العددين —
       ولا تُظهر نجاحاً ثانياً يُقرأ «رُحِّل مرّتين».

   **والقيدُ لكل قسيمة لا للمسيّر**: معرّف القسيمة هو `DocumentId` في هوية
   الإحكام، و`entryId` قيدُها هي وحدها. ولذلك يُعرَض عمود القيد على القسيمة.

   ── ولا رمزَ حسابٍ في هذه الشاشة إطلاقاً ────────────────────────────────
   الوحدات لا تسمّي حساباً (القاعدة 2): الرواتب تبلغ الدفتر عبر مصفوفة
   الترحيل، والشاشة تعرض **مبالغ بأسماء مفردات المصفوفة** لا أرقام حسابات.
   ═══════════════════════════════════════════════════════════════════════════ */
import { useCallback, useMemo, useState, type ReactNode } from "react";
import { useNavigate } from "@tanstack/react-router";
import { useQuery } from "@tanstack/react-query";
import {
  depositPayrollSettings,
  draftPayrollPayment,
  draftPayrollRun,
  listPayrollSettings,
  listPayslips,
  postPayrollPayment,
  postPayrollRun,
  readPayrollRun,
} from "../../api/generated/client";
import type {
  HrPayrollPayment,
  HrPayrollPaymentRequest,
  HrPayrollRun,
  HrPayslip,
} from "../../api/generated/types";
import { asTaxRate } from "../../api/generated/brands";
import { PARAM_readTrialBalance_period_RE } from "../../api/generated/formats";
import { Money } from "../../api/money";
import { useApi } from "../../app/api-context";
import { ProblemError } from "../../api/transport";
import { ProblemPanel } from "../../app/shell/ProblemPanel";
import { Amount, Num, useT } from "../../i18n/react";
import { Button, EmptyState, Field, Panel, RefusalPanel, StatCard, useMoment } from "../../ui";
import { useHrFocus } from "./focus";
import {
  ChooseCompanyFirst,
  HrSectionNav,
  EntryRef,
  HrState,
  OpaqueCode,
  StatePanel,
  isMoneyText,
  isRateText,
  todayIso,
  AmountsRow,
} from "./parts";
import {
  DUPLICATE_NUMBER,
  NO_PAYSLIPS,
  PERIOD_HAS_RUN,
  POSTED,
  SETTINGS_MISSING,
  SETTLEMENT_METHODS,
  TREASURY_MISSING,
} from "./contract";
import "./hr.css";

/** صفُّ نِسَبٍ كما يُكتب قبل أن يعبر — كلّه نصوص، ولا عائم في خطوة. */
interface DraftRates {
  classCode: string;
  effectiveFrom: string;
  employerRate: string;
  employeeRate: string;
  minimumContributoryWage: string;
  maximumContributoryWage: string;
  approvedBy: string;
  approvedOn: string;
  sourceRef: string;
}

function emptyRates(): DraftRates {
  return {
    classCode: "",
    effectiveFrom: "",
    employerRate: "",
    employeeRate: "",
    minimumContributoryWage: "",
    maximumContributoryWage: "",
    approvedBy: "",
    approvedOn: todayIso(),
    sourceRef: "",
  };
}

/** الشاشة كاملةً. */
export function PayrollRunScreen(): ReactNode {
  const { t, tp } = useT();
  const { transport, config } = useApi();
  const navigate = useNavigate();
  const [focus, setFocus] = useHrFocus();

  const [postCls, firePost] = useMoment("post");
  const [refuseCls, fireRefuse] = useMoment("refuse");
  const [arriveCls, fireArrive] = useMoment("arrive");

  /* ── الصفّ المعتمد ─────────────────────────────────────────────────── */
  const [rates, setRates] = useState<DraftRates>(emptyRates);
  const [ratesBusy, setRatesBusy] = useState(false);
  const [ratesError, setRatesError] = useState<unknown>(null);
  const [ratesOpen, setRatesOpen] = useState(false);

  /* ── المسيّر ───────────────────────────────────────────────────────── */
  const [number, setNumber] = useState("");
  const [periodCode, setPeriodCode] = useState("");
  const [periodStart, setPeriodStart] = useState("");
  const [periodEnd, setPeriodEnd] = useState("");
  const [draftBusy, setDraftBusy] = useState(false);
  const [runError, setRunError] = useState<unknown>(null);
  const [runId, setRunId] = useState(focus.runId);

  /* ── الترحيل ───────────────────────────────────────────────────────── */
  const [posted, setPosted] = useState<readonly HrPayslip[] | null>(null);
  const [postBusy, setPostBusy] = useState(false);
  const [postError, setPostError] = useState<unknown>(null);

  /* ── سند الصرف ─────────────────────────────────────────────────────── */
  const [payNumber, setPayNumber] = useState("");
  const [paidOn, setPaidOn] = useState(todayIso);
  /* المؤهّل يأتي من العقد نصّاً، و TypeScript لا يعرف عن نصٍّ قرأه وقت
     التشغيل أنه عضوٌ في المجموعة المغلقة — والتحويل هنا وعند الحدّ وحده. */
  type Method = HrPayrollPaymentRequest["settlementMethod"];
  const [method, setMethod] = useState<Method>((SETTLEMENT_METHODS[0] ?? "") as Method);
  const [treasury, setTreasury] = useState("");
  const [payment, setPayment] = useState<HrPayrollPayment | null>(null);
  const [payBusy, setPayBusy] = useState(false);
  const [payError, setPayError] = useState<unknown>(null);

  const settings = useQuery({
    queryKey: ["hr", "payroll-settings", config.baseUrl, config.token, config.companyId],
    enabled: config.companyId !== "",
    retry: false,
    queryFn: ({ signal }) => listPayrollSettings(transport, { companyId: config.companyId }, signal),
  });

  const run = useQuery({
    queryKey: ["hr", "payroll-run", config.baseUrl, config.token, config.companyId, runId],
    enabled: config.companyId !== "" && runId !== "",
    retry: false,
    queryFn: ({ signal }) => readPayrollRun(transport, { companyId: config.companyId, runId }, signal),
  });

  const payslips = useQuery({
    queryKey: ["hr", "payslips", config.baseUrl, config.token, config.companyId, runId],
    enabled: config.companyId !== "" && runId !== "",
    retry: false,
    queryFn: ({ signal }) => listPayslips(transport, { companyId: config.companyId, runId }, signal),
  });

  const periodValid = periodCode === "" || PARAM_readTrialBalance_period_RE.test(periodCode);

  const submitRates = useCallback(async () => {
    setRatesBusy(true);
    setRatesError(null);
    try {
      await depositPayrollSettings(transport, {
        companyId: config.companyId,
        body: {
          classCode: rates.classCode,
          effectiveFrom: rates.effectiveFrom,
          /* النِّسَب صيغةٌ محتجزة في العقد، والاحتجاز يتحقّق من النمط المنشور
             قبل أن يصير النصّ قيمة — فلا نسبةٌ لا يقبلها الخادم تغادر هنا. */
          employerRate: asTaxRate(rates.employerRate),
          employeeRate: asTaxRate(rates.employeeRate),
          minimumContributoryWage: Money.wire(rates.minimumContributoryWage),
          maximumContributoryWage: Money.wire(rates.maximumContributoryWage),
          approvedBy: rates.approvedBy,
          approvedOn: rates.approvedOn,
          sourceRef: rates.sourceRef,
        },
      });
      setRates(emptyRates());
      await settings.refetch();
      fireArrive();
    } catch (failure) {
      setRatesError(failure);
      fireRefuse();
    } finally {
      setRatesBusy(false);
    }
  }, [config.companyId, fireArrive, fireRefuse, rates, settings, transport]);

  const submitDraft = useCallback(async () => {
    setDraftBusy(true);
    setRunError(null);
    setPosted(null);
    setPayment(null);
    try {
      const created = await draftPayrollRun(transport, {
        companyId: config.companyId,
        body: { number, periodCode, periodStart, periodEnd },
      });
      setRunId(created.id);
      setFocus({ runId: created.id });
      fireArrive();
    } catch (failure) {
      setRunError(failure);
      fireRefuse();
    } finally {
      setDraftBusy(false);
    }
  }, [config.companyId, fireArrive, fireRefuse, number, periodCode, periodEnd, periodStart, setFocus, transport]);

  const submitPosting = useCallback(async () => {
    setPostBusy(true);
    setPostError(null);
    try {
      const result = await postPayrollRun(transport, { companyId: config.companyId, runId });
      setPosted(result.items);
      await run.refetch();
      await payslips.refetch();
      /* **الحركة تتبع ما وقع فعلاً**: مفردة الترحيل لا تُصرَف على نداءٍ لم
         يُرحِّل شيئاً — وصرفُها هناك يُفقدها معناها في المرّة التي تعني. */
      if (result.items.some((slip) => !slip.alreadyPosted)) firePost();
      else fireArrive();
    } catch (failure) {
      setPostError(failure);
      fireRefuse();
    } finally {
      setPostBusy(false);
    }
  }, [config.companyId, firePost, fireArrive, fireRefuse, payslips, run, runId, transport]);

  const submitPayment = useCallback(async () => {
    setPayBusy(true);
    setPayError(null);
    try {
      const drafted = await draftPayrollPayment(transport, {
        companyId: config.companyId,
        body: { number: payNumber, runId, paidOn, settlementMethod: method, treasuryPartyId: treasury },
      });
      const settled = await postPayrollPayment(transport, {
        companyId: config.companyId,
        paymentId: drafted.id,
      });
      setPayment(settled);
      if (!settled.alreadyPosted) firePost();
      else fireArrive();
    } catch (failure) {
      setPayError(failure);
      fireRefuse();
    } finally {
      setPayBusy(false);
    }
  }, [config.companyId, firePost, fireArrive, fireRefuse, method, paidOn, payNumber, runId, transport, treasury]);

  const openPayslip = useCallback(
    (id: string) => {
      setFocus({ payslipId: id });
      void navigate({ to: "/hr/payslip" });
    },
    [navigate, setFocus]
  );

  /* ── الرفض: نتصرّف على الرمز لا على نصّ الرسالة ───────────────────────── */
  const runCode = runError instanceof ProblemError ? runError.code : null;
  const postCode = postError instanceof ProblemError ? postError.code : null;
  const payCode = payError instanceof ProblemError ? payError.code : null;

  const rows: readonly HrPayslip[] = payslips.data?.items ?? [];
  const current: HrPayrollRun | null = run.data ?? null;

  const tally = useMemo(() => {
    if (!posted) return null;
    let already = 0;
    for (const slip of posted) if (slip.alreadyPosted) already += 1;
    return { total: posted.length, already, fresh: posted.length - already };
  }, [posted]);

  const ratesReady =
    rates.classCode !== "" &&
    rates.effectiveFrom !== "" &&
    isRateText(rates.employerRate) &&
    isRateText(rates.employeeRate) &&
    isMoneyText(rates.minimumContributoryWage) &&
    isMoneyText(rates.maximumContributoryWage) &&
    rates.approvedBy !== "" &&
    rates.approvedOn !== "" &&
    rates.sourceRef !== "";

  const draftReady =
    number !== "" && periodCode !== "" && periodValid && periodStart !== "" && periodEnd !== "";

  if (config.companyId === "") return <ChooseCompanyFirst testId="hr-payroll-needs-company" />;

  return (
    <section className="stack" data-testid="hr-payroll-screen">
      <header className="pagehead">
        <div>
          <h1>{t("hr.page.payrollTitle")}</h1>
          <p className="sub">{t("hr.page.payrollLede")}</p>
        </div>
      </header>

      <HrSectionNav current="/hr/payroll" />

      {/* ═════════════════════════════════ ١ · صفوف النِّسَب المعتمدة ═════ */}
      <StatePanel
        title={t("hr.rates.title")}
        note={t("hr.rates.note")}
        aside={<span className="muted">{tp("hr.count.rates", settings.data?.itemCount ?? 0)}</span>}
        loading={settings.isPending && settings.fetchStatus === "fetching"}
        testId="hr-rates"
      >
        {settings.isError ? (
          <ProblemPanel error={settings.error} onRetry={() => void settings.refetch()} />
        ) : (settings.data?.items.length ?? 0) === 0 ? (
          <EmptyState
            title={t("hr.rates.emptyTitle")}
            body={t("hr.rates.emptyBody")}
            action={
              <Button
                label={t("hr.act.deposit")}
                kind="primary"
                onClick={() => setRatesOpen(true)}
                testId="hr-rates-open"
              />
            }
            testId="hr-rates-empty"
          />
        ) : (
          <div className={"hr-table " + arriveCls} data-testid="hr-rates-table">
            <table>
              <caption className="visually-hidden">{t("hr.rates.title")}</caption>
              <thead>
                <tr>
                  <th scope="col">{t("hr.field.classCode")}</th>
                  <th scope="col">{t("hr.field.effectiveFrom")}</th>
                  <th scope="col" className="n">{t("hr.field.employerRate")}</th>
                  <th scope="col" className="n">{t("hr.field.employeeRate")}</th>
                  <th scope="col" className="n">{t("hr.field.minWage")}</th>
                  <th scope="col" className="n">{t("hr.field.maxWage")}</th>
                  <th scope="col">{t("hr.field.approvedBy")}</th>
                  <th scope="col">{t("hr.field.sourceRef")}</th>
                </tr>
              </thead>
              <tbody>
                {(settings.data?.items ?? []).map((row) => (
                  <tr key={row.id}>
                    <td><span className="mono" dir="ltr">{row.classCode}</span></td>
                    <td><span className="mono" dir="ltr">{row.effectiveFrom}</span></td>
                    <td className="n"><span className="mono" dir="ltr">{row.employerRate}</span></td>
                    <td className="n"><span className="mono" dir="ltr">{row.employeeRate}</span></td>
                    <td className="n"><Amount value={row.minimumContributoryWage} /></td>
                    <td className="n"><Amount value={row.maximumContributoryWage} /></td>
                    <td>{row.approvedBy}</td>
                    <td><span className="mono" dir="ltr">{row.sourceRef}</span></td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
        )}

        <div className="inline-group">
          <Button
            label={ratesOpen ? t("hr.act.close") : t("hr.act.deposit")}
            onClick={() => setRatesOpen(!ratesOpen)}
            testId="hr-rates-toggle"
          />
        </div>

        {ratesOpen ? (
          <div className="stack" data-testid="hr-rates-form">
            <p className="muted">{t("hr.rates.depositNote")}</p>
            <div className="grid fields-4">
              <Field id="hr-r-class" label={t("hr.field.classCode")} hint={t("hr.field.classCodeHint")} source="typed" required>
                <input id="hr-r-class" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
                  data-testid="hr-rates-class" value={rates.classCode}
                  onChange={(e) => setRates({ ...rates, classCode: e.target.value })} />
              </Field>
              <Field id="hr-r-from" label={t("hr.field.effectiveFrom")} source="typed" required>
                <input id="hr-r-from" className="ctl mono" type="date" dir="ltr"
                  data-testid="hr-rates-from" value={rates.effectiveFrom}
                  onChange={(e) => setRates({ ...rates, effectiveFrom: e.target.value })} />
              </Field>
              <Field
                id="hr-r-er"
                label={t("hr.field.employerRate")}
                hint={t("hr.field.rateHint")}
                error={rates.employerRate !== "" && !isRateText(rates.employerRate) ? t("hr.field.rateBad") : undefined}
                source="typed"
                required
              >
                <input id="hr-r-er" className="ctl amt-input" inputMode="decimal" dir="ltr" autoComplete="off"
                  aria-invalid={rates.employerRate !== "" && !isRateText(rates.employerRate)}
                  data-testid="hr-rates-employer" value={rates.employerRate}
                  onChange={(e) => setRates({ ...rates, employerRate: e.target.value })} placeholder="0.00000000" />
              </Field>
              <Field
                id="hr-r-ee"
                label={t("hr.field.employeeRate")}
                hint={t("hr.field.rateHint")}
                error={rates.employeeRate !== "" && !isRateText(rates.employeeRate) ? t("hr.field.rateBad") : undefined}
                source="typed"
                required
              >
                <input id="hr-r-ee" className="ctl amt-input" inputMode="decimal" dir="ltr" autoComplete="off"
                  aria-invalid={rates.employeeRate !== "" && !isRateText(rates.employeeRate)}
                  data-testid="hr-rates-employee" value={rates.employeeRate}
                  onChange={(e) => setRates({ ...rates, employeeRate: e.target.value })} placeholder="0.00000000" />
              </Field>
            </div>
            <div className="grid fields-4">
              <Field id="hr-r-min" label={t("hr.field.minWage")} hint={t("hr.field.amountHint")} source="typed" required>
                <input id="hr-r-min" className="ctl amt-input" inputMode="decimal" dir="ltr" autoComplete="off"
                  aria-invalid={rates.minimumContributoryWage !== "" && !isMoneyText(rates.minimumContributoryWage)}
                  data-testid="hr-rates-min" value={rates.minimumContributoryWage}
                  onChange={(e) => setRates({ ...rates, minimumContributoryWage: e.target.value })} placeholder="0.0000" />
              </Field>
              <Field id="hr-r-max" label={t("hr.field.maxWage")} hint={t("hr.field.maxWageHint")} source="typed" required>
                <input id="hr-r-max" className="ctl amt-input" inputMode="decimal" dir="ltr" autoComplete="off"
                  aria-invalid={rates.maximumContributoryWage !== "" && !isMoneyText(rates.maximumContributoryWage)}
                  data-testid="hr-rates-max" value={rates.maximumContributoryWage}
                  onChange={(e) => setRates({ ...rates, maximumContributoryWage: e.target.value })} placeholder="0.0000" />
              </Field>
              <Field id="hr-r-by" label={t("hr.field.approvedBy")} source="typed" required>
                <input id="hr-r-by" className="ctl" autoComplete="off"
                  data-testid="hr-rates-by" value={rates.approvedBy}
                  onChange={(e) => setRates({ ...rates, approvedBy: e.target.value })} />
              </Field>
              <Field id="hr-r-on" label={t("hr.field.approvedOn")} source="typed" required>
                <input id="hr-r-on" className="ctl mono" type="date" dir="ltr"
                  data-testid="hr-rates-on" value={rates.approvedOn}
                  onChange={(e) => setRates({ ...rates, approvedOn: e.target.value })} />
              </Field>
            </div>
            <Field id="hr-r-src" label={t("hr.field.sourceRef")} hint={t("hr.field.sourceRefHint")} source="typed" required>
              <input id="hr-r-src" className="ctl" autoComplete="off"
                data-testid="hr-rates-source" value={rates.sourceRef}
                onChange={(e) => setRates({ ...rates, sourceRef: e.target.value })} />
            </Field>
            <div className="inline-group">
              <Button
                label={t("hr.act.deposit")}
                kind="primary"
                loading={ratesBusy}
                disabled={!ratesReady || ratesBusy}
                onClick={() => void submitRates()}
                testId="hr-rates-submit"
              />
            </div>
            {ratesError ? <ProblemPanel error={ratesError} /> : null}
          </div>
        ) : null}
      </StatePanel>

      {/* ═════════════════════════════════════ ٢ · إنشاء المسيّر مسوّدة ═══ */}
      <Panel title={t("hr.run.draftTitle")} note={t("hr.run.draftNote")} testId="hr-run-draft">
        <div className="grid fields-4">
          <Field id="hr-run-number" label={t("hr.field.number")} hint={t("hr.field.numberHint")} source="typed" required>
            <input id="hr-run-number" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
              data-testid="hr-run-number" value={number} onChange={(e) => setNumber(e.target.value)}
              placeholder="RUN-2026-06" />
          </Field>
          <Field
            id="hr-run-period"
            label={t("hr.field.periodCode")}
            hint={periodValid ? t("hr.field.periodHint") : t("hr.field.periodBad")}
            error={periodValid ? undefined : t("hr.field.periodBad")}
            source="typed"
            required
          >
            <input id="hr-run-period" className={"ctl mono" + (periodValid ? "" : " is-invalid")} dir="ltr"
              autoComplete="off" aria-invalid={!periodValid}
              data-testid="hr-run-period" value={periodCode} onChange={(e) => setPeriodCode(e.target.value)}
              placeholder="2026-06" />
          </Field>
          <Field id="hr-run-start" label={t("hr.field.periodStart")} source="typed" required>
            <input id="hr-run-start" className="ctl mono" type="date" dir="ltr"
              data-testid="hr-run-start" value={periodStart} onChange={(e) => setPeriodStart(e.target.value)} />
          </Field>
          <Field id="hr-run-end" label={t("hr.field.periodEnd")} hint={t("hr.field.periodEndHint")} source="typed" required>
            <input id="hr-run-end" className="ctl mono" type="date" dir="ltr"
              data-testid="hr-run-end" value={periodEnd} onChange={(e) => setPeriodEnd(e.target.value)} />
          </Field>
        </div>
        <div className="grid fields-2">
          <Field id="hr-run-id" label={t("hr.field.runId")} hint={t("hr.field.runIdHint")}>
            <input id="hr-run-id" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
              data-testid="hr-run-id" value={runId}
              onChange={(e) => { setRunId(e.target.value); setFocus({ runId: e.target.value }); }} />
          </Field>
          <div className="hr-act">
            <Button
              label={t("hr.act.draft")}
              kind="primary"
              loading={draftBusy}
              disabled={!draftReady || draftBusy}
              onClick={() => void submitDraft()}
              testId="hr-run-draft-submit"
            />
          </div>
        </div>

        {runError ? (
          <div className="stack">
            <ProblemPanel error={runError} />
            {runCode === SETTINGS_MISSING ? (
              <RefusalPanel
                title={t("hr.refusal.settingsTitle")}
                titleEn="No approved contribution-rate row covers this period"
                body={t("hr.refusal.settingsBody")}
                code={SETTINGS_MISSING}
                codeLabel={t("common.problem.code")}
                subject={t("hr.rates.title")}
                subjectLabel={t("common.problem.field")}
                next={t("hr.refusal.settingsNext")}
                moment={refuseCls}
                testId="hr-refusal-settings"
              />
            ) : null}
            {runCode === PERIOD_HAS_RUN ? (
              <RefusalPanel
                title={t("hr.refusal.periodTitle")}
                titleEn="The period already has a run"
                body={t("hr.refusal.periodBody")}
                code={PERIOD_HAS_RUN}
                codeLabel={t("common.problem.code")}
                next={t("hr.refusal.periodNext")}
                moment={refuseCls}
                testId="hr-refusal-period"
              />
            ) : null}
            {runCode === NO_PAYSLIPS ? (
              <RefusalPanel
                title={t("hr.refusal.noPayslipsTitle")}
                titleEn="No active employment enters this run"
                body={t("hr.refusal.noPayslipsBody")}
                code={NO_PAYSLIPS}
                codeLabel={t("common.problem.code")}
                next={t("hr.refusal.noPayslipsNext")}
                moment={refuseCls}
                testId="hr-refusal-no-payslips"
              />
            ) : null}
            {runCode === DUPLICATE_NUMBER ? (
              <RefusalPanel
                title={t("hr.refusal.duplicateTitle")}
                titleEn="The document number is already used"
                body={t("hr.refusal.duplicateBody")}
                code={DUPLICATE_NUMBER}
                codeLabel={t("common.problem.code")}
                next={t("hr.refusal.duplicateNext")}
                moment={refuseCls}
                testId="hr-refusal-duplicate"
              />
            ) : null}
          </div>
        ) : null}
      </Panel>

      {/* ═══════════════════════════════════ ٣ · المسيّر وقسائمه ═════════ */}
      {run.isError ? <ProblemPanel error={run.error} onRetry={() => void run.refetch()} /> : null}

      {current ? (
        <Panel
          title={t("hr.run.title")}
          note={t("hr.run.note")}
          aside={<HrState state={current.state} testId="hr-run-state" />}
          testId="hr-run-card"
        >
          <div className="kv">
            <div>
              <div className="k">{t("hr.field.number")}</div>
              <div className="v mono" dir="ltr" data-testid="hr-run-card-number">{current.number}</div>
            </div>
            <div>
              <div className="k">{t("hr.field.periodCode")}</div>
              <div className="v mono" dir="ltr" data-testid="hr-run-card-period">{current.periodCode}</div>
            </div>
            <div>
              <div className="k">{t("hr.field.periodEnd")}</div>
              <div className="v mono" dir="ltr">{current.periodEnd}</div>
            </div>
            <div>
              <div className="k">{t("hr.run.payslipCount")}</div>
              <div className="v" data-testid="hr-run-payslip-count">
                <Num value={current.payslipCount} />
              </div>
            </div>
          </div>
          <AmountsRow amounts={current.amounts} moment={arriveCls} testId="hr-run-amounts" />
          <p className="muted">{t("hr.run.amountsNote")}</p>
        </Panel>
      ) : null}

      {current ? (
        <StatePanel
          title={t("hr.run.payslipsTitle")}
          note={t("hr.run.payslipsNote")}
          aside={<span className="muted">{tp("hr.count.payslips", payslips.data?.itemCount ?? 0)}</span>}
          loading={payslips.isPending && payslips.fetchStatus === "fetching"}
          testId="hr-payslips"
        >
          {payslips.isError ? (
            <ProblemPanel error={payslips.error} onRetry={() => void payslips.refetch()} />
          ) : rows.length === 0 ? (
            <EmptyState
              small
              title={t("hr.run.payslipsEmpty")}
              body={t("hr.run.payslipsEmptyBody")}
              testId="hr-payslips-empty"
            />
          ) : (
            <div className="hr-table" data-testid="hr-payslips-table">
              <table>
                <caption className="visually-hidden">{t("hr.run.payslipsTitle")}</caption>
                <thead>
                  <tr>
                    <th scope="col">{t("hr.code.label")}</th>
                    <th scope="col">{t("hr.field.costCenter")}</th>
                    <th scope="col" className="n">{t("hr.run.contributoryWage")}</th>
                    <th scope="col" className="n">{t("hr.amount.gross")}</th>
                    <th scope="col" className="n">{t("hr.amount.deductions")}</th>
                    <th scope="col" className="n">{t("hr.amount.net")}</th>
                    <th scope="col">{t("hr.entry.label")}</th>
                    <th scope="col">{t("common.label.status")}</th>
                  </tr>
                </thead>
                <tbody>
                  {rows.map((slip) => (
                    <tr key={slip.id} className={slip.state === POSTED ? postCls : undefined}>
                      <td>
                        <button
                          type="button"
                          className="hr-open"
                          data-testid="hr-payslip-open"
                          onClick={() => openPayslip(slip.id)}
                        >
                          <OpaqueCode code={slip.employeeCode} testId="hr-payslip-code" />
                        </button>
                      </td>
                      <td><span className="mono" dir="ltr">{slip.costCenterId}</span></td>
                      <td className="n"><Amount value={slip.contributoryWage} /></td>
                      <td className="n"><Amount value={slip.amounts.grossEntitlements} /></td>
                      <td className="n"><Amount value={slip.amounts.deductions} /></td>
                      <td className="n"><Amount value={slip.amounts.netPayable} /></td>
                      <td><EntryRef entryId={slip.entryId} testId="hr-payslip-entry" /></td>
                      <td><HrState state={slip.state} /></td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
          )}
        </StatePanel>
      ) : null}

      {/* ═══════════════════════════════════════ ٤ · الترحيل وإيصاله ════ */}
      {current ? (
        <Panel title={t("hr.run.postTitle")} note={t("hr.run.postNote")} testId="hr-post">
          <div className="inline-group">
            <Button
              label={t("hr.act.post")}
              kind="primary"
              loading={postBusy}
              disabled={postBusy || rows.length === 0}
              onClick={() => void submitPosting()}
              testId="hr-post-submit"
            />
            {posted ? (
              <Button
                label={t("hr.act.postAgain")}
                loading={postBusy}
                disabled={postBusy}
                onClick={() => void submitPosting()}
                testId="hr-post-again"
              />
            ) : null}
          </div>

          {tally ? (
            <div
              className={"hr-receipt " + (tally.fresh > 0 ? postCls : arriveCls)}
              data-already={String(tally.fresh === 0)}
              role="status"
              data-testid="hr-post-receipt"
            >
              <h2>{tally.fresh > 0 ? t("hr.run.receiptNew") : t("hr.run.receiptAgain")}</h2>
              <p>{tally.fresh > 0 ? t("hr.run.receiptNewBody") : t("hr.run.receiptAgainBody")}</p>
              <div className="stats-row">
                <StatCard label={t("hr.run.postedNow")} count={tally.fresh} tone="good" testId="hr-posted-fresh" />
                <StatCard label={t("hr.run.postedBefore")} count={tally.already} testId="hr-posted-already" />
                <StatCard label={t("hr.run.entriesWritten")} count={tally.total} hint={t("hr.run.entryPerPayslip")} />
              </div>
              <p className="hint">{t("hr.run.idempotencyNote")}</p>
            </div>
          ) : null}

          {postError ? (
            <div className="stack">
              <ProblemPanel error={postError} />
              {postCode === SETTINGS_MISSING ? (
                <RefusalPanel
                  title={t("hr.refusal.settingsTitle")}
                  titleEn="No approved contribution-rate row covers this period"
                  body={t("hr.refusal.settingsBody")}
                  code={SETTINGS_MISSING}
                  codeLabel={t("common.problem.code")}
                  next={t("hr.refusal.settingsNext")}
                  moment={refuseCls}
                  testId="hr-refusal-settings-post"
                />
              ) : null}
            </div>
          ) : null}
        </Panel>
      ) : null}

      {/* ═══════════════════════════════ ٥ · صرف الرواتب بعد الترحيل ════ */}
      {current && current.state === POSTED ? (
        <Panel title={t("hr.payment.title")} note={t("hr.payment.note")} testId="hr-payment">
          <div className="grid fields-4">
            <Field id="hr-pay-number" label={t("hr.field.number")} source="typed" required>
              <input id="hr-pay-number" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
                data-testid="hr-payment-number" value={payNumber} onChange={(e) => setPayNumber(e.target.value)}
                placeholder="PAY-2026-06" />
            </Field>
            <Field id="hr-pay-on" label={t("hr.field.paidOn")} source="typed" required>
              <input id="hr-pay-on" className="ctl mono" type="date" dir="ltr"
                data-testid="hr-payment-date" value={paidOn} onChange={(e) => setPaidOn(e.target.value)} />
            </Field>
            <Field id="hr-pay-method" label={t("hr.field.settlementMethod")} hint={t("hr.field.settlementMethodHint")} source="typed" required>
              <select id="hr-pay-method" className="ctl" data-testid="hr-payment-method"
                value={method} onChange={(e) => setMethod(e.target.value as Method)}>
                {SETTLEMENT_METHODS.map((name) => (
                  <option key={name} value={name}>{t("hr.method." + name)}</option>
                ))}
              </select>
            </Field>
            <Field id="hr-pay-treasury" label={t("hr.field.treasuryParty")} hint={t("hr.field.treasuryPartyHint")} source="typed" required>
              <input id="hr-pay-treasury" className="ctl mono" dir="ltr" autoComplete="off" spellCheck={false}
                data-testid="hr-payment-treasury" value={treasury} onChange={(e) => setTreasury(e.target.value)}
                placeholder="BANK-0001" />
            </Field>
          </div>
          <div className="inline-group">
            <Button
              label={t("hr.act.pay")}
              kind="primary"
              loading={payBusy}
              disabled={payBusy || payNumber === "" || paidOn === "" || treasury === ""}
              onClick={() => void submitPayment()}
              testId="hr-payment-submit"
            />
          </div>

          {payment ? (
            <div
              className={"hr-receipt " + (payment.alreadyPosted ? arriveCls : postCls)}
              data-already={String(payment.alreadyPosted)}
              role="status"
              data-testid="hr-payment-receipt"
            >
              <h2>{payment.alreadyPosted ? t("hr.payment.again") : t("hr.payment.done")}</h2>
              <p>{payment.alreadyPosted ? t("hr.payment.againBody") : t("hr.payment.doneBody")}</p>
              <div className="kv">
                <div>
                  <div className="k">{t("hr.payment.netPayable")}</div>
                  <div className="v"><Amount value={payment.netPayable} /></div>
                </div>
                <div>
                  <div className="k">{t("common.label.status")}</div>
                  <div className="v"><HrState state={payment.state} /></div>
                </div>
                <div>
                  <div className="k">{t("hr.payment.lineCount")}</div>
                  <div className="v"><Num value={payment.lines.length} /></div>
                </div>
              </div>
              <div className="hr-table" data-testid="hr-payment-lines">
                <table>
                  <caption className="visually-hidden">{t("hr.payment.lines")}</caption>
                  <thead>
                    <tr>
                      <th scope="col">{t("hr.code.label")}</th>
                      <th scope="col" className="n">{t("hr.field.amount")}</th>
                      <th scope="col">{t("hr.entry.label")}</th>
                    </tr>
                  </thead>
                  <tbody>
                    {payment.lines.map((line) => (
                      <tr key={line.payslipId}>
                        <td><OpaqueCode code={line.employeeCode} /></td>
                        <td className="n"><Amount value={line.amount} /></td>
                        <td><EntryRef entryId={line.entryId} /></td>
                      </tr>
                    ))}
                  </tbody>
                </table>
              </div>
            </div>
          ) : null}

          {payError ? (
            <div className="stack">
              <ProblemPanel error={payError} />
              {payCode === TREASURY_MISSING ? (
                <RefusalPanel
                  title={t("hr.refusal.treasuryTitle")}
                  titleEn="The document carries no treasury party"
                  body={t("hr.refusal.treasuryBody")}
                  code={TREASURY_MISSING}
                  codeLabel={t("common.problem.code")}
                  next={t("hr.refusal.treasuryNext")}
                  moment={refuseCls}
                  testId="hr-refusal-treasury"
                />
              ) : null}
            </div>
          ) : null}
        </Panel>
      ) : null}
    </section>
  );
}
